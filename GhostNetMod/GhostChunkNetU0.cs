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
    public struct GhostChunkNetU0 {

        public const string Chunk = "nU0";
        public bool IsValid;

        public uint UpdateIndex;

        public void Read(BinaryReader reader) {
            IsValid = true;

            UpdateIndex = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(UpdateIndex);
        }

    }
}
