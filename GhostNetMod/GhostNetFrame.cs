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

        public bool Propagated;

        // Head chunks, always present.
        public GhostChunkNetHHead HHead;

        // Management chunks.
        public GhostChunkNetMServerInfo MServerInfo;
        public GhostChunkNetMPlayer MPlayer;
        public GhostChunkNetMIcon MIcon;

        // Update chunks.
        public GhostChunkNetUUpdate UUpdate;

        // Extra chunks, modifyable by mods.
        public byte[] Extra;

        public void Read(BinaryReader reader) {
            string chunk;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            using (MemoryStream extraBuffer = new MemoryStream())
            using (BinaryWriter extraWriter = new BinaryWriter(extraBuffer)) {
                while ((chunk = reader.ReadNullTerminatedString()) != "\r\n") {
                    uint length = reader.ReadUInt32();
                    switch (chunk) {
                        case GhostChunkNetHHead.Chunk:
                            HHead.Read(reader);
                            break;

                        case GhostChunkNetMServerInfo.Chunk:
                            MServerInfo.Read(reader);
                            break;
                        case GhostChunkNetMPlayer.Chunk:
                            MPlayer.Read(reader);
                            break;
                        case GhostChunkNetMIcon.Chunk:
                            MIcon.Read(reader);
                            break;
                        
                        case GhostChunkNetUUpdate.Chunk:
                            UUpdate.Read(reader);
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
            if (HHead.IsValid)
                GhostFrame.WriteChunk(writer, HHead.Write, GhostChunkNetHHead.Chunk);

            if (MServerInfo.IsValid)
                GhostFrame.WriteChunk(writer, MServerInfo.Write, GhostChunkNetMServerInfo.Chunk);
            if (MPlayer.IsValid)
                GhostFrame.WriteChunk(writer, MPlayer.Write, GhostChunkNetMPlayer.Chunk);
            if (MIcon.IsValid)
                GhostFrame.WriteChunk(writer, MIcon.Write, GhostChunkNetMIcon.Chunk);

            if (UUpdate.IsValid)
                GhostFrame.WriteChunk(writer, UUpdate.Write, GhostChunkNetUUpdate.Chunk);

            if (Extra != null)
                writer.Write(Extra);

            writer.WriteNullTerminatedString(GhostFrame.End);
        }

    }
}
