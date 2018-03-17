using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public struct GhostFrame {

        public const string End = "\r\n";

        public GhostChunkData Data;
        public GhostChunkInput Input;

        public void Read(BinaryReader reader) {
            string chunk;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            while ((chunk = reader.ReadNullTerminatedString()) != End) {
                uint length = reader.ReadUInt32();
                switch (chunk) {
                    case GhostChunkData.ChunkV1:
                        Data.Read(reader, 1);
                        break;
                    case GhostChunkData.ChunkV2:
                        Data.Read(reader, 2);
                        break;
                    case GhostChunkInput.Chunk:
                        Input.Read(reader);
                        break;
                    
                    default:
                        // Skip any unknown chunks.
                        reader.BaseStream.Seek(length, SeekOrigin.Current);
                        break;
                }
            }
        }

        public void Write(BinaryWriter writer) {
            if (Data.IsValid)
                WriteChunk(writer, Data.Write, GhostChunkData.Chunk);

            if (Input.IsValid)
                WriteChunk(writer, Input.Write, GhostChunkInput.Chunk);

            writer.WriteNullTerminatedString(End);
        }

        public static void WriteChunk(BinaryWriter writer, Action<BinaryWriter> method, string name) {
            long start = WriteChunkStart(writer, name);
            method(writer);
            WriteChunkEnd(writer, start);
        }

        public static long WriteChunkStart(BinaryWriter writer, string name) {
            writer.WriteNullTerminatedString(name);
            writer.Write(0U); // Filled in later.
            long start = writer.BaseStream.Position;
            return start;
        }

        public static void WriteChunkEnd(BinaryWriter writer, long start) {
            long pos = writer.BaseStream.Position;
            long length = pos - start;

            // Update the chunk length, which consists of the 4 bytes before the chunk data.
            writer.Flush();
            writer.BaseStream.Seek(start - 4, SeekOrigin.Begin);
            writer.Write((int) length);

            writer.Flush();
            writer.BaseStream.Seek(pos, SeekOrigin.Begin);
        }

    }
}
