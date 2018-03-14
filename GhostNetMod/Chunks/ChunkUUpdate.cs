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
    /// Update chunk sent on (best case) each frame.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public class ChunkUUpdate : IChunk {

        public const string ChunkID = "nU";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint UpdateIndex;
        public GhostChunkData Data;

        public void Read(BinaryReader reader) {
            UpdateIndex = reader.ReadUInt32();
            Data.Read(reader);
        }

        public void Write(BinaryWriter writer) {
            writer.Write(UpdateIndex);
            Data.Write(writer);
        }

    }
}
