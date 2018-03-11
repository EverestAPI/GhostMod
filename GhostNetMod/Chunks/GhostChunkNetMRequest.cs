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
    /// Chunk sent by the server if it's requesting another "requestable" from the player.
    /// </summary>
    public struct GhostChunkNetMRequest {

        public const string Chunk = "nMR";
        public bool IsValid {
            get {
                return !string.IsNullOrWhiteSpace(ID);
            }
            set {
                if (!value)
                    ID = "";
            }
        }

        public string ID;

        public void Read(BinaryReader reader) {
            ID = reader.ReadNullTerminatedString().Trim();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(ID);
        }

    }
}
