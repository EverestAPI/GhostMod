using Celeste.Mod;
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
    /// Embedded AudioTrackState chunk, which is a part of another chunk.
    /// </summary>
    public class ChunkEAudioTrackState : IChunk {

        public const string ChunkID = "nEAT";

        public bool IsValid => Track != null;
        public bool IsSendable => true;

        public AudioTrackState Track;

        public ChunkEAudioTrackState() {
        }

        public ChunkEAudioTrackState(AudioTrackState track) {
            Track = track;
        }

        public void Read(BinaryReader reader) {
            Track = new AudioTrackState();

            Track.Event = reader.ReadNullTerminatedString();
            Track.Progress = reader.ReadInt32();

            int count = reader.ReadByte();
            for (int i = 0; i < count; i++) {
                MEP param = new MEP();
                param.Key = reader.ReadNullTerminatedString();
                param.Value = reader.ReadSingle();
                Track.Parameters.Add(param);
            }
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Track.Event);
            writer.Write(Track.Progress);

            writer.Write((byte) Track.Parameters.Count);
            for (int i = 0; i < Track.Parameters.Count; i++) {
                MEP param = Track.Parameters[i];
                writer.WriteNullTerminatedString(param.Key);
                writer.Write(param.Value);
            }
        }

        public object Clone()
            => new ChunkEAudioTrackState {
                Track = Track.Clone()
            };

        public static implicit operator ChunkEAudioTrackState(GhostNetFrame frame)
            => frame.Get<ChunkEAudioTrackState>();

    }
}
