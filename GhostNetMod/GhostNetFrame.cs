using Celeste.Mod.Helpers;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Helpers;
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
    public delegate void GhostNetFrameHandler(GhostNetConnection con, ref GhostNetFrame frame);
    public delegate IChunk GhostNetChunkParser(BinaryReader reader);
    /// <summary>
    /// A GhostNetFrame is a collection of many individual Chunks, which can have an individual or combined meaning.
    /// </summary>
    public class GhostNetFrame {

        public static IDictionary<string, GhostNetChunkParser> ChunkParsers = new FastDictionary<string, GhostNetChunkParser>();

        static GhostNetFrame() {
            // Find all chunk types in all mods.
            foreach (Type type in FakeAssembly.GetFakeEntryAssembly().GetTypes()) {
                ChunkAttribute chunkInfo = type.GetCustomAttribute<ChunkAttribute>();
                if (chunkInfo == null)
                    continue;
                // TODO: Can be optimized. Who wants to write a DynamicMethod generator for this? :^)
                ChunkParsers[chunkInfo.ID] = reader => {
                    IChunk chunk = (IChunk) Activator.CreateInstance(type);
                    chunk.Read(reader);
                    return chunk;
                };
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

        public IDictionary<Type, IChunk> ChunkMap = new FastDictionary<Type, IChunk>();

        #region Standard Chunks

        // Head chunk, added by server.
        public ChunkHHead HHead {
            get {
                return Get<ChunkHHead>();
            }
            set {
                Set(value);
            }
        }

        public ChunkMServerInfo MServerInfo {
            get {
                return Get<ChunkMServerInfo>();
            }
            set {
                Set(value);
            }
        }
        public ChunkMPlayer MPlayer {
            get {
                return Get<ChunkMPlayer>();
            }
            set {
                Set(value);
            }
        }
        public ChunkMRequest MRequest {
            get {
                return Get<ChunkMRequest>();
            }
            set {
                Set(value);
            }
        }
        public ChunkMSession MSession {
            get {
                return Get<ChunkMSession>();
            }
            set {
                Set(value);
            }
        }
        public ChunkMEmote MEmote {
            get {
                return Get<ChunkMEmote>();
            }
            set {
                Set(value);
            }
        }
        public ChunkMChat MChat {
            get {
                return Get<ChunkMChat>();
            }
            set {
                Set(value);
            }
        }

        // Update chunks.
        public ChunkUUpdate UUpdate {
            get {
                return Get<ChunkUUpdate>();
            }
            set {
                Set(value);
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
                            ChunkMap[chunk.GetType()] = chunk;
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
            foreach (IChunk chunk in ChunkMap.Values)
                if (chunk.IsValid)
                    GhostFrame.WriteChunk(writer, chunk.Write, chunk.GetType().GetCustomAttribute<ChunkAttribute>().ID);

            if (Extra != null)
                writer.Write(Extra);

            writer.WriteNullTerminatedString(GhostFrame.End);
        }

        public void Set<T>(T chunk) where T : IChunk {
            ChunkMap[typeof(T)] = chunk;
        }

        public T Get<T>() where T : IChunk {
            IChunk chunk;
            if (ChunkMap.TryGetValue(typeof(T), out chunk) && chunk != null && chunk.IsValid)
                return (T) chunk;
            return default(T);
        }

        public void Remove<T>() {
            ChunkMap[typeof(T)] = null;
        }

    }
}
