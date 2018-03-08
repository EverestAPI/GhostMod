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
    /// Sent by the server, chunk containing some basic connection info.
    /// </summary>
    public struct GhostChunkNetMServerInfo {

        public const string Chunk = "nM?";
        public bool IsValid;

        // PlayerID contained in HHead.

        public void Read(BinaryReader reader) {
            IsValid = true;
        }

        public void Write(BinaryWriter writer) {
        }

    }
}
