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
    /// Update chunk sent to emit particles.
    /// </summary>
    public class ChunkUParticles : IChunk {

        public const string ChunkID = "nUP";

        public bool IsValid => Type != null && Type.GetID() != -1;
        public bool IsSendable => true;

        public Systems System;

        public ParticleType Type;

        public int Amount;
        public Vector2 Position;
        public Vector2 PositionRange;
        public Color Color;
        public float Direction;

        public void Read(BinaryReader reader) {
            Type = GhostNetParticleHelper.GetType(reader.ReadInt32());

            System = (Systems) reader.ReadByte();

            Amount = reader.ReadInt32();
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            PositionRange = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Color = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            Direction = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(Type.GetID());

            writer.Write((byte) System);

            writer.Write(Amount);
            writer.Write(Position.X);
            writer.Write(Position.X);
            writer.Write(PositionRange.X);
            writer.Write(PositionRange.Y);
            writer.Write(Color.R);
            writer.Write(Color.G);
            writer.Write(Color.B);
            writer.Write(Color.A);
            writer.Write(Direction);
        }

        public object Clone()
            => new ChunkUParticles {
                System = System,

                Type = Type,

                Amount = Amount,
                Position = Position,
                PositionRange = PositionRange,
                Direction = Direction
            };

        public static implicit operator ChunkUParticles(GhostNetFrame frame)
            => frame.Get<ChunkUParticles>();

        public enum Systems {
            Particles,
            ParticlesBG,
            ParticlesFG
        }

    }
}
