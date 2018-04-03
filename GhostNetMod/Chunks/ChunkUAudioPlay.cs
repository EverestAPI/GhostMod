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
    /// Update chunk sent when a sound should be played.
    /// The server can ignore or even replace a client's position.
    /// </summary>
    public class ChunkUAudioPlay : IChunk {

        public const string ChunkID = "nUAP";

        public bool IsValid => !string.IsNullOrEmpty(Sound);
        public bool IsSendable => true;

        public string Sound;
        public string Param;
        public float Value;

        public Vector2? Position;

        public void Read(BinaryReader reader) {
            Sound = reader.ReadNullTerminatedString();
            Param = reader.ReadNullTerminatedString();
            if (!string.IsNullOrEmpty(Param)) {
                Value = reader.ReadSingle();
            }

            if (reader.ReadBoolean())
                Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Sound);
            writer.WriteNullTerminatedString(Param);
            if (!string.IsNullOrEmpty(Param)) {
                writer.Write(Value);
            }

            if (Position != null) {
                writer.Write(true);
                writer.Write(Position.Value.X);
                writer.Write(Position.Value.Y);

            } else {
                writer.Write(false);
            }
        }

        public object Clone()
            => new ChunkUAudioPlay {
                Sound = Sound,
                Param = Param,
                Value = Value,

                Position = Position
            };

        public static implicit operator ChunkUAudioPlay(GhostNetFrame frame)
            => frame.Get<ChunkUAudioPlay>();

    }
}
