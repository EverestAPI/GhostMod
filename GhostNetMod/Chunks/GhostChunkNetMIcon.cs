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
    /// Make an icon spawn above the player.
    /// </summary>
    public struct GhostChunkNetMIcon {

        public const string Chunk = "nMI";
        public bool IsValid;

        public string Icon;

        public void Read(BinaryReader reader) {
            IsValid = true;

            Icon = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Icon);
        }

    }
}
