﻿using FMOD.Studio;
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
    /// Make an emote spawn above the player.
    /// </summary>
    public class ChunkMEmote : IChunk {

        public const string ChunkID = "nME";

        public IChunk Next { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public string Value;

        public void Read(BinaryReader reader) {
            Value = reader.ReadNullTerminatedString();
        }

        public void Write(BinaryWriter writer) {
            writer.WriteNullTerminatedString(Value);
        }

    }
}