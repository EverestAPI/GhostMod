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
    public class ChunkRListMods : IChunk {

        public const string ChunkID = "nRlM";

        public bool IsValid => true;
        public bool IsSendable => true;

        public Entry[] Entries;

        public void Read(BinaryReader reader) {
            Entries = new Entry[reader.ReadByte()];
            for (int i = 0; i < Entries.Length; i++) {
                Entries[i] = new Entry {
                    Name = reader.ReadNullTerminatedString(),
                    Version = new Version(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())
                };
            }
        }

        public void Write(BinaryWriter writer) {
            if (Entries == null || Entries.Length == 0) {
                writer.Write((byte) 0);
                return;
            }

            writer.Write((byte) Entries.Length);
            for (int i = 0; i < Entries.Length; i++) {
                Entry entry = Entries[i];
                writer.WriteNullTerminatedString(entry.Name);
                writer.Write(entry.Version.Major);
                writer.Write(entry.Version.Minor);
                writer.Write(entry.Version.Build);
                writer.Write(entry.Version.Revision);
            }
        }

        public class Entry {
            public string Name;
            public Version Version;
        }

        public object Clone()
            => new ChunkRListMods {
                Entries = Entries
            };

        public static implicit operator ChunkRListMods(GhostNetFrame frame)
            => frame.Get<ChunkRListMods>();

    }
}
