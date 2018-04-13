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
    /// "Status" chunk sent on connection and on room change.
    /// Server remembers this and responds with all other players in the same room.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public class ChunkMPlayer : IChunk {

        public const string ChunkID = "nM";

        public bool IsValid => true;
        public bool IsSendable => !IsCached;

        /// <summary>
        /// Whether the chunk is what the server / client last remembers about the player, or if it's a newly received chunk.
        /// </summary>
        public bool IsCached;

        public bool IsEcho;

        public string Name;

        public string SID;
        public AreaMode Mode;
        public string Level;

        public bool LevelCompleted = false;
        public LevelExit.Mode? LevelExit;

        public bool Idle;

        public void Read(BinaryReader reader) {
            IsCached = false;

            IsEcho = reader.ReadBoolean();

            Name = reader.ReadNullTerminatedString();

            SID = reader.ReadNullTerminatedString();
            Mode = (AreaMode) reader.ReadByte();
            Level = reader.ReadNullTerminatedString();

            LevelCompleted = reader.ReadBoolean();

            if (reader.ReadBoolean())
                LevelExit = (LevelExit.Mode) reader.ReadByte();

            Idle = reader.ReadBoolean();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(IsEcho);

            writer.WriteNullTerminatedString(Name);

            writer.WriteNullTerminatedString(SID);
            writer.Write((byte) Mode);
            writer.WriteNullTerminatedString(Level);

            writer.Write(LevelCompleted);

            if (LevelExit == null) {
                writer.Write(false);
            } else {
                writer.Write(true);
                writer.Write((byte) LevelExit);
            }

            writer.Write(Idle);
        }

        public object Clone()
            => new ChunkMPlayer {
                IsEcho = IsEcho,

                Name = Name,

                SID = SID,
                Mode = Mode,
                Level = Level,

                LevelCompleted = LevelCompleted,
                LevelExit = LevelExit,

                Idle = Idle
            };

        public static implicit operator ChunkMPlayer(GhostNetFrame frame)
            => frame.Get<ChunkMPlayer>();

    }
}
