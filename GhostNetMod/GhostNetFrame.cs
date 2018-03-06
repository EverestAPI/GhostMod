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
    public struct GhostNetFrame {

        public GhostFrame Frame;

        public void Read(BinaryReader reader) {
            string chunk;
            // The last "chunk" type, \r\n (Windows linebreak), doesn't contain a length.
            while ((chunk = reader.ReadNullTerminatedString()) != "\r\n") {
                uint length = reader.ReadUInt32();
                switch (chunk) {
                    case "nH0":
                        ReadChunkNetHead0(reader);
                        break;

                    case "nU0":
                        ReadChunkNetUpdate0(reader);
                        break;

                    case "nM0":
                        ReadChunkNetManagement0(reader);
                        break;

                    case "data":
                        Frame.ReadChunkData(reader);
                        break;

                    default:
                        // Skip any unknown chunks.
                        reader.BaseStream.Seek(length, SeekOrigin.Current);
                        break;
                }
            }
        }

        public void WriteUpdate(BinaryWriter writer) {
            WriteChunkNetHead0(writer);

            WriteChunkNetUpdate0(writer);

            Frame.WriteChunkData(writer);

            writer.WriteNullTerminatedString("\r\n");
        }

        public void WriteManagement(BinaryWriter writer) {
            WriteChunkNetHead0(writer);

            WriteChunkNetManagement0(writer);

            writer.WriteNullTerminatedString("\r\n");
        }


        public bool HasNetHead0;

        public uint PlayerID;

        public void ReadChunkNetHead0(BinaryReader reader) {
            HasNetHead0 = true;

            PlayerID = reader.ReadUInt32();
        }

        public void WriteChunkNetHead0(BinaryWriter writer) {
            if (!HasNetHead0)
                return;
            long start = Frame.WriteChunkStart(writer, "nH0");

            writer.Write(PlayerID);

            Frame.WriteChunkEnd(writer, start);
        }

        public bool HasNetUpdate0;

        public uint UpdateIndex;

        public void ReadChunkNetUpdate0(BinaryReader reader) {
            HasNetUpdate0 = true;

            UpdateIndex = reader.ReadUInt32();
        }

        public void WriteChunkNetUpdate0(BinaryWriter writer) {
            if (!HasNetUpdate0)
                return;
            long start = Frame.WriteChunkStart(writer, "nU0");

            writer.Write(UpdateIndex);

            Frame.WriteChunkEnd(writer, start);
        }


        public bool HasNetManagement0;

        public string Name;
        public string SID;
        public string Level;

        public void ReadChunkNetManagement0(BinaryReader reader) {
            HasNetManagement0 = true;

            Name = reader.ReadNullTerminatedString();
            SID = reader.ReadNullTerminatedString();
            Level = reader.ReadNullTerminatedString();
        }

        public void WriteChunkNetManagement0(BinaryWriter writer) {
            if (!HasNetManagement0)
                return;
            long start = Frame.WriteChunkStart(writer, "nM0");

            writer.WriteNullTerminatedString(Name);
            writer.WriteNullTerminatedString(SID);
            writer.WriteNullTerminatedString(Level);

            Frame.WriteChunkEnd(writer, start);
        }

    }
}
