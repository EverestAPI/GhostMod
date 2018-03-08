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
    /// "Status" chunk sent on connection and on room change.
    /// Server remembers this and responds with all other players in the same room.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public struct GhostChunkNetMPlayer {

        public const string Chunk = "nM";
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
