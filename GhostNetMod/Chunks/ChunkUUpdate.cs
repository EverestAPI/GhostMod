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
    [Chunk(ChunkID)]
    /// <summary>
    /// Update chunk sent on (best case) each frame.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public class ChunkUUpdate : IChunk {

        public const string ChunkID = "nU";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint UpdateIndex;
        public GhostChunkData Data;

        public Color[] HairColors;
        public string[] HairTextures;

        public void Read(BinaryReader reader) {
            UpdateIndex = reader.ReadUInt32();
            Data.Read(reader, int.MaxValue);

            HairColors = new Color[reader.ReadByte()];
            for (int i = 0; i < HairColors.Length; i++) {
                HairColors[i] = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            }

            HairTextures = new string[reader.ReadByte()];
            for (int i = 0; i < HairColors.Length; i++) {
                HairTextures[i] = reader.ReadNullTerminatedString();
                if (HairTextures[i] == "-")
                    HairTextures[i] = HairTextures[i - 1];
            }
        }

        public void Write(BinaryWriter writer) {
            writer.Write(UpdateIndex);
            Data.Write(writer);

            if (HairColors == null || HairColors.Length == 0) {
                writer.Write((byte) 0);
            } else {
                writer.Write((byte) HairColors.Length);
                for (int i = 0; i < HairColors.Length; i++) {
                    writer.Write(HairColors[i].R);
                    writer.Write(HairColors[i].G);
                    writer.Write(HairColors[i].B);
                    writer.Write(HairColors[i].A);
                }
            }

            if (HairTextures == null || HairTextures.Length == 0) {
                writer.Write((byte) 0);
            } else {
                writer.Write((byte) HairTextures.Length);
                for (int i = 0; i < HairTextures.Length; i++) {
                    if (i > 1 && HairTextures[i] == HairTextures[i - 1])
                        writer.WriteNullTerminatedString("-");
                    else
                        writer.WriteNullTerminatedString(HairTextures[i]);
                }
            }
        }

        public object Clone()
            => new ChunkUUpdate {
                UpdateIndex = UpdateIndex,
                Data = Data,

                HairColors = new List<Color>(HairColors).ToArray(),
                HairTextures = new List<string>(HairTextures).ToArray()
            };

        public static implicit operator ChunkUUpdate(GhostNetFrame frame)
            => frame.Get<ChunkUUpdate>();

    }
}
