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
    /// Chunk sent by the client to notify the player why they left the level.
    /// Combined with MPlayer where SID is null or empty.
    /// </summary>
    public class ChunkMLevelExit : IChunk {

        public const string ChunkID = "nMQ";

        public bool IsValid => true;

        public LevelExit.Mode Mode;

        public void Read(BinaryReader reader) {
            Mode = (LevelExit.Mode) reader.ReadByte();
        }

        public void Write(BinaryWriter writer) {
            writer.Write((byte) Mode);
        }

    }
}
