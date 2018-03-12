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
    /// A simple chat message.
    /// </summary>
    public class ChunkMChat : IChunk {

        public const string ChunkID = "nMC";

        public IChunk Next { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Text);

        /// <summary>
        /// Server-internal field.
        /// </summary>
        public bool CreatedByServer;
        /// <summary>
        /// Server-internal field.
        /// </summary>
        public bool Logged;

        public uint ID;
        public string Tag;
        public string Text;
        public Color Color;
        public DateTime Date;

        public void Read(BinaryReader reader) {
            ID = reader.ReadUInt32();
            Tag = reader.ReadNullTerminatedString();
            Text = reader.ReadNullTerminatedString();
            Color = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 1f);
            Date = DateTime.FromBinary(reader.ReadInt64());
        }

        public void Write(BinaryWriter writer) {
            writer.Write(ID);
            writer.WriteNullTerminatedString(Tag);
            writer.WriteNullTerminatedString(Text);
            writer.Write(Color.R);
            writer.Write(Color.G);
            writer.Write(Color.B);
            writer.Write(Date.ToBinary());
        }

    }
}
