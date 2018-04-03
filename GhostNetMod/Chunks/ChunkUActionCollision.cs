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
    /// Update chunk sent on (best case) each "collision" frame.
    /// A player always receives this with a HHead.PlayerID of the "colliding" player.
    /// </summary>
    public class ChunkUActionCollision : IChunk {

        public const string ChunkID = "nUaC";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint With;
        public bool Head;

        public void Read(BinaryReader reader) {
            With = reader.ReadUInt32();
            Head = reader.ReadBoolean();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(With);
            writer.Write(Head);
        }

        public object Clone()
            => new ChunkUActionCollision {
                With = With,
                Head = Head
            };

        public static implicit operator ChunkUActionCollision(GhostNetFrame frame)
            => frame.Get<ChunkUActionCollision>();

    }
}
