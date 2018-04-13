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

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetExtensions {

        private readonly static FieldInfo f_TrailManager_shapshots = typeof(TrailManager).GetField("snapshots", BindingFlags.NonPublic | BindingFlags.Instance);

        public static TrailManager.Snapshot[] GetSnapshots(this TrailManager self)
            => (TrailManager.Snapshot[]) f_TrailManager_shapshots.GetValue(self);

        public static string Nullify(this string value)
            => string.IsNullOrEmpty(value) ? null : value;

    }
}
