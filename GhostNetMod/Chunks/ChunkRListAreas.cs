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
    public class ChunkRListAreas : IChunk {

        public const string ChunkID = "nRlA";

        public bool IsValid => true;
        public bool IsSendable => true;

        public string[] Entries;

        public void Read(BinaryReader reader) {
            Entries = new string[reader.ReadByte()];
            for (int i = 0; i < Entries.Length; i++) {
                Entries[i] = reader.ReadNullTerminatedString();
            }
        }

        public void Write(BinaryWriter writer) {
            if (Entries == null || Entries.Length == 0) {
                writer.Write((byte) 0);
                return;
            }

            writer.Write((byte) Entries.Length);
            for (int i = 0; i < Entries.Length; i++) {
                writer.WriteNullTerminatedString(Entries[i]);
            }
        }

        public class Entry {
            public string Name;
            public Version Version;
        }

        public object Clone()
            => new ChunkRListAreas {
                Entries = Entries
            };

        public static implicit operator ChunkRListAreas(GhostNetFrame frame)
            => frame.Get<ChunkRListAreas>();

    }
}
