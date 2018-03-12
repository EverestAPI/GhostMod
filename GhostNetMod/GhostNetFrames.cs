using Celeste.Mod.Helpers;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Helpers;
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
    public static class GhostNetFrames {

        private static Stack<GhostNetFrame> Pool = new Stack<GhostNetFrame>();

        public static GhostNetFrame Get() {
            lock (Pool) {
                if (Pool.Count == 0)
                    return new GhostNetFrame();

                GhostNetFrame frame = Pool.Pop();
                frame.ChunkMap.Clear();
                frame.Extra = null;
                frame.PropagateM = false;
                frame.PropagateU = false;
                return frame;
            }
        }

        public static void Release(this GhostNetFrame frame) {
            // Don't lock pushing, as it can't cause Get() to fail.
            Pool.Push(frame);
        }

    }
}
