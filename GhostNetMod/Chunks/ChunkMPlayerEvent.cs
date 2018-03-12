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
    /// Chunk sent by the client to signify a given event. Standard event IDs are stored in the nested IDs class.
    /// </summary>
    public class ChunkMPlayerEvent : IChunk {

        public const string ChunkID = "nM!";

        public IChunk Next { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(ID);

        public string ID;

        public void Read(BinaryReader reader) {
            ID = reader.ReadNullTerminatedString().Trim();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(ID);
        }

        public static class IDs {
            // TODO: More event types.
            public const string Complete = "fin";
        }

    }
}
