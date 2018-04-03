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
    /// Embedded AudioState chunk, which is a part of another chunk.
    /// </summary>
    public class ChunkEAudioState : IChunk {

        public const string ChunkID = "nEA";

        public bool IsValid => Audio != null;
        public bool IsSendable => true;

        public AudioState Audio;

        public ChunkEAudioState() {
        }

        public ChunkEAudioState(AudioState audio) {
            Audio = audio;
        }

        public void Read(BinaryReader reader) {
            Audio = new AudioState();

            if (reader.ReadBoolean()) {
                ChunkEAudioTrackState track = new ChunkEAudioTrackState();
                track.Read(reader);
                Audio.Music = track.Track;
            }

            if (reader.ReadBoolean()) {
                ChunkEAudioTrackState track = new ChunkEAudioTrackState();
                track.Read(reader);
                Audio.Ambience = track.Track;
            }
        }

        public void Write(BinaryWriter writer) {
            if (Audio.Music != null) {
                writer.Write(true);
                ChunkEAudioTrackState track = new ChunkEAudioTrackState(Audio.Music);
                track.Write(writer);

            } else {
                writer.Write(false);
            }

            if (Audio.Ambience != null) {
                writer.Write(true);
                ChunkEAudioTrackState track = new ChunkEAudioTrackState(Audio.Ambience);
                track.Write(writer);

            } else {
                writer.Write(false);
            }
        }

        public object Clone()
            => new ChunkEAudioState {
                Audio = Audio.Clone()
            };

        public static implicit operator ChunkEAudioState(GhostNetFrame frame)
            => frame.Get<ChunkEAudioState>();

    }
}
