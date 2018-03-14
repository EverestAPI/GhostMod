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
    public interface IChunk : ICloneable {

        bool IsValid { get; }
        bool IsSendable { get; }

        void Read(BinaryReader reader);

        void Write(BinaryWriter writer);

    }
    public class ChunkAttribute : Attribute {

        public string ID;

        public ChunkAttribute(string id) {
            ID = id;
        }

    }
}
