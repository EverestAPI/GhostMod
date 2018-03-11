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
    /// "Status" chunk sent on connection and on room change.
    /// Server remembers this and responds with all other players in the same room.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public class ChunkMPlayer : IChunk {

        public const string ChunkID = "nM";

        public IChunk Next { get; set; }

        public bool IsValid => true;

        public string Name;

        public string SID;
        public AreaMode Mode;
        public string Level;

        public void Read(BinaryReader reader) {
            Name = reader.ReadNullTerminatedString();

            SID = reader.ReadNullTerminatedString();
            Mode = (AreaMode) reader.ReadByte();
            Level = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Name);

            writer.WriteNullTerminatedString(SID);
            writer.Write((byte) Mode);
            writer.WriteNullTerminatedString(Level);
        }

    }
}
