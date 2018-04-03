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
    /// Chunk sent by the server if it's requesting another "requestable" chunk from the client.
    /// A client defines what it wants to respond to on its own. The server should be able to deal
    /// with the lack of a response natively.
    /// </summary>
    public class ChunkMRequest : IChunk {

        public const string ChunkID = "nMR";

        public bool IsValid => !string.IsNullOrWhiteSpace(ID);
        public bool IsSendable => true;

        public string ID;

        public void Read(BinaryReader reader) {
            ID = reader.ReadNullTerminatedString().Trim();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(ID);
        }

        public object Clone()
            => new ChunkMRequest {
                ID = ID
            };

        public static implicit operator ChunkMRequest(GhostNetFrame frame)
            => frame.Get<ChunkMRequest>();

    }
}
