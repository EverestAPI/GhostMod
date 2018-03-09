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
        public bool IsValid;

        public string Value;

        public void Read(BinaryReader reader) {
            IsValid = true;

            Value = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Value);
        }

    }
}
