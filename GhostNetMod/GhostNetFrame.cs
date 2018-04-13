using Celeste.Mod.Helpers;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost.Net {
    public delegate void GhostNetFrameHandler(GhostNetConnection con, GhostNetFrame frame);
    public delegate IChunk GhostNetChunkParser(BinaryReader reader);
    /// <summary>
    /// A GhostNetFrame is a collection of many individual Chunks, which can have an individual or combined meaning.
    /// </summary>
    public sealed class GhostNetFrame : ICloneable, IEnumerable<IChunk> {

        private static IDictionary<string, GhostNetChunkParser> ChunkParsers = new Dictionary<string, GhostNetChunkParser>();
        private static IDictionary<Type, string> ChunkIDs = new Dictionary<Type, string>();

        // TODO: Wrapper for objects that don't natively implement IChunk, but implement its members.

        public static void RegisterChunk(Type type, string id, Func<BinaryReader, object> parser)
            => RegisterChunk(type, id, reader => parser(reader) as IChunk);
        public static void RegisterChunk(Type type, string id, GhostNetChunkParser parser) {
            ChunkIDs[type] = id;
            ChunkParsers[id] = parser;
        }

        public static string GetChunkID(Type type) {
            string id;
            if (ChunkIDs.TryGetValue(type, out id))
                return id;
            ChunkAttribute chunkInfo = type.GetCustomAttribute<ChunkAttribute>();
            if (chunkInfo != null)
                return ChunkIDs[type] = chunkInfo.ID;
            throw new InvalidDataException("Unregistered chunk type");
        }

        public static void RegisterChunksFromModule(EverestModule module) {
            foreach (Type type in module.GetType().Assembly.GetTypes()) {
                ChunkAttribute chunkInfo = type.GetCustomAttribute<ChunkAttribute>();
                if (chunkInfo == null)
                    continue;
                // TODO: Can be optimized. Who wants to write a DynamicMethod generator for this? :^)
                RegisterChunk(type, chunkInfo.ID, reader => {
                    IChunk chunk = (IChunk) Activator.CreateInstance(type);
                    chunk.Read(reader);
                    return chunk;
                });
            }
        }

        /// <summary>
        /// Server-internal field. Should the frame be propagated after handling?
        /// </summary>
        public bool PropagateM;
        /// <summary>
        /// Server-internal field. Should the frame be propagated after handling?
        /// </summary>
        public bool PropagateU;

        public IDictionary<Type, IChunk> ChunkMap = new Dictionary<Type, IChunk>();

        #region Standard Chunks

        // Head chunk, added by server.
        public ChunkHHead HHead {
            get {
                return Get<ChunkHHead>();
            }
            set {
                Add(value);
            }
        }

        public ChunkMPlayer MPlayer {
            get {
                return Get<ChunkMPlayer>();
            }
            set {
                Add(value);
            }
        }
        public ChunkUUpdate UUpdate {
            get {
                return Get<ChunkUUpdate>();
            }
            set {
                Add(value);
            }
        }

        #endregion

        // Unparsed chunks, modifyable by mods.
        public byte[] Extra;

        public void Read(BinaryReader reader) {
            string id;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            using (MemoryStream extraBuffer = new MemoryStream())
            using (BinaryWriter extraWriter = new BinaryWriter(extraBuffer)) {
                while ((id = reader.ReadNullTerminatedString()) != "\r\n") {
                    uint length = reader.ReadUInt32();
                    GhostNetChunkParser parser;
                    if (ChunkParsers.TryGetValue(id, out parser)) {
                        IChunk chunk = parser(reader);
                        if (chunk != null && chunk.IsValid) {
                            lock (ChunkMap) {
                                ChunkMap[chunk.GetType()] = chunk;
                            }
                        }

                    } else {
                        // Store any unknown chunks.
                        extraWriter.WriteNullTerminatedString(id);
                        extraWriter.Write(length);
                        extraWriter.Write(reader.ReadBytes((int) length));
                        break;
                    }
                }

                extraWriter.Flush();
                Extra = extraBuffer.ToArray();
            }
        }

        public void Write(BinaryWriter writer) {
            lock (ChunkMap) {
                foreach (IChunk chunk in ChunkMap.Values)
                    if (chunk != null && chunk.IsValid && chunk.IsSendable)
                        GhostFrame.WriteChunk(writer, chunk.Write, GetChunkID(chunk.GetType()));
            }

            if (Extra != null)
                writer.Write(Extra);

            writer.WriteNullTerminatedString(GhostFrame.End);
        }

        public GhostNetFrame Add<T>(T chunk) where T : IChunk
            => Add(typeof(T), chunk);
        public GhostNetFrame Add(Type t, IChunk chunk) {
            // Assume that chunk is t for performance reasons.
            lock (ChunkMap) {
                ChunkMap[t] = chunk;
            }
            return this;
        }

        public void Remove<T>() where T : IChunk
            => Remove(typeof(T));
        public void Remove(Type t) {
            lock (ChunkMap) {
                ChunkMap[t] = null;
            }
        }

        public void Get<T>(out T chunk) where T : IChunk
            => chunk = (T) Get(typeof(T));
        public T Get<T>() where T : IChunk
            => (T) Get(typeof(T));
        public IChunk Get(Type t) {
            IChunk chunk;
            if (ChunkMap.TryGetValue(t, out chunk) && chunk != null && chunk.IsValid)
                return chunk;
            return null;
        }

        public bool Has<T>() where T : IChunk
            => Has(typeof(T));
        public bool Has(Type t)
            => Get(t) != null;

        public object Clone() {
            GhostNetFrame clone = new GhostNetFrame();
            lock (ChunkMap) {
                foreach (KeyValuePair<Type, IChunk> entry in ChunkMap)
                    if (entry.Value != null && entry.Value.IsValid && entry.Value.IsSendable)
                        clone.ChunkMap[entry.Key] = (IChunk) entry.Value.Clone();
            }
            return clone;
        }

        public IEnumerator<IChunk> GetEnumerator() {
            return ChunkMap.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ChunkMap.Values.GetEnumerator();
        }
    }
}
