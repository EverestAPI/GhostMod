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
    /// <summary>
    /// A simple chat message.
    /// </summary>
    public struct GhostChunkNetMChat {

        public const string Chunk = "nMC";
        public bool IsValid {
            get {
                return !string.IsNullOrWhiteSpace(Text);
            }
            set {
                if (!value)
                    Text = "";
            }
        }

        /// <summary>
        /// Server-internal field.
        /// </summary>
        public bool KeepColor;
        /// <summary>
        /// Server-internal field.
        /// </summary>
        public bool Logged;

        public uint ID;
        public string Text;
        public Color Color;
        public DateTime Date;

        public void Read(BinaryReader reader) {
            ID = reader.ReadUInt32();
            Text = reader.ReadNullTerminatedString();
            Color = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 1f);
            Date = DateTime.FromBinary(reader.ReadInt64());
        }

        public void Write(BinaryWriter writer) {
            writer.Write(ID);
            writer.WriteNullTerminatedString(Text);
            writer.Write(Color.R);
            writer.Write(Color.G);
            writer.Write(Color.B);
            writer.Write(Date.ToBinary());
        }

    }
}
