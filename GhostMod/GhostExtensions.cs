using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost {
    public static class GhostExtensions {

        private readonly static FieldInfo f_Player_wasDashB = typeof(Player).GetField("wasDashB", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool GetWasDashB(this Player self)
            => (bool) f_Player_wasDashB.GetValue(self);

    }
}
