using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost {
    public static class GhostModuleBackCompat {

        private static BitTag _TagSubHUD;
        public static BitTag TagSubHUD {
            get {
                if (_TagSubHUD != null)
                    return _TagSubHUD;

                return _TagSubHUD =
                    (Assembly.GetEntryAssembly().GetType("Celeste.TagsExt")?.GetField("SubHUD")?.GetValue(null) as BitTag) ??
                    Tags.HUD;
            }
        }

    }
}
