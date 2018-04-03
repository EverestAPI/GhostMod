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
    /// Sent by the server, chunk containing some basic connection info.
    /// </summary>
    public class ChunkMServerInfo : IChunk {

        public const string ChunkID = "nM?";

        public bool IsValid => true;
        public bool IsSendable => true;

        // PlayerID contained in HHead.

        public string Name;

        public void Read(BinaryReader reader) {
            Name = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Name);
        }

        public object Clone()
            => new ChunkMServerInfo {
                Name = Name
            };

        public static implicit operator ChunkMServerInfo(GhostNetFrame frame)
            => frame.Get<ChunkMServerInfo>();

    }
}
