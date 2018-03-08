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

namespace Celeste.Mod.Ghost.Net {
    public struct GhostNetFrame {

        public GhostChunkNetH0 H0;

        public GhostChunkNetM0 M0;

        public GhostChunkNetU0 U0;
        public GhostChunkData Data;

        public byte[] Extra;

        public void Read(BinaryReader reader) {
            string chunk;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            using (MemoryStream extraBuffer = new MemoryStream())
            using (BinaryWriter extraWriter = new BinaryWriter(extraBuffer)) {
                while ((chunk = reader.ReadNullTerminatedString()) != "\r\n") {
                    uint length = reader.ReadUInt32();
                    switch (chunk) {
                        case GhostChunkNetH0.Chunk:
                            H0.Read(reader);
                            break;

                        case GhostChunkNetM0.Chunk:
                            M0.Read(reader);
                            break;

                        case GhostChunkNetU0.Chunk:
                            U0.Read(reader);
                            break;
                        case GhostChunkData.Chunk:
                            Data.Read(reader);
                            break;

                        default:
                            // Store any unknown chunks.
                            extraWriter.WriteNullTerminatedString(chunk);
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
            if (H0.IsValid)
                GhostFrame.WriteChunk(writer, H0.Write, GhostChunkNetH0.Chunk);

            if (M0.IsValid)
                GhostFrame.WriteChunk(writer, M0.Write, GhostChunkNetM0.Chunk);

            if (U0.IsValid)
                GhostFrame.WriteChunk(writer, U0.Write, GhostChunkNetU0.Chunk);

            if (Data.IsValid)
                GhostFrame.WriteChunk(writer, Data.Write, GhostChunkData.Chunk);

            if (Extra != null)
                writer.Write(Extra);

            writer.WriteNullTerminatedString(GhostFrame.End);
        }

    }
}
