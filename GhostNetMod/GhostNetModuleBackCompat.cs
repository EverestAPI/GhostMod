using Celeste.Mod;
using Celeste.Mod.Helpers;
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

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetModuleBackCompat {

        private static EventInfo _OnTextInputEvent;
        public static EventInfo OnTextInputEvent {
            get {
                if (_OnTextInputEvent != null)
                    return _OnTextInputEvent;

                return _OnTextInputEvent =
                    FakeAssembly.GetFakeEntryAssembly().GetType("Celeste.Mod.TextInput")?.GetEvent("OnInput");
            }
        }
        public static bool HasTextInputEvent => OnTextInputEvent != null;
        public static event Action<char> OnTextInput {
            add {
                if (!HasTextInputEvent)
                    return;
                OnTextInputEvent.AddEventHandler(null, value);
            }
            remove {
                if (!HasTextInputEvent)
                    return;
                OnTextInputEvent.RemoveEventHandler(null, value);
            }
        }

    }
}
