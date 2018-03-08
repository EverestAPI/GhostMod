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
    /// <summary>
    /// Update chunk sent on (best case) each frame.
    /// If the player receives this with their own player ID, the server is moving the player.
    /// </summary>
    public struct GhostChunkNetUUpdate {

        public const string Chunk = "nU";
        public bool IsValid;

        public uint UpdateIndex;
        public GhostChunkData Data;

        public void Read(BinaryReader reader) {
            IsValid = true;

            UpdateIndex = reader.ReadUInt32();
            Data.Read(reader);
        }

        public void Write(BinaryWriter writer) {
            writer.Write(UpdateIndex);
            Data.Write(writer);
        }

    }
}
