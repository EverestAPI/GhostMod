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
    public struct GhostChunkNetM0 {

        public const string Chunk = "nM0";
        public bool IsValid;

        public string Name;

        public string SID;
        public string Level;

        public void Read(BinaryReader reader) {
            IsValid = true;

            Name = reader.ReadNullTerminatedString();

            SID = reader.ReadNullTerminatedString();
            Level = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Name);

            writer.WriteNullTerminatedString(SID);
            writer.WriteNullTerminatedString(Level);
        }

    }
}
