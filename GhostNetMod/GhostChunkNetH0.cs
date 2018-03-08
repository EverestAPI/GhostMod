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
    public struct GhostChunkNetH0 {

        public const string Chunk = "nH0";
        public bool IsValid;

        public uint PlayerID;

        public void Read(BinaryReader reader) {
            IsValid = true;

            PlayerID = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(PlayerID);
        }

    }
}
