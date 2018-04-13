using Celeste.Mod.Helpers;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetParticleHelper {

        private readonly static List<ParticleType> AllTypes = (List<ParticleType>) typeof(ParticleType).GetField("AllTypes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        public static int GetID(this ParticleType type) {
            return AllTypes.IndexOf(type);
        }

        public static ParticleType GetType(int id) {
            if (id < 0 || AllTypes.Count <= id)
                return null;
            return AllTypes[id];
        }

    }
}
