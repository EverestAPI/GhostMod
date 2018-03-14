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

        public static event Action<Level> OnLevelComplete;
        private static EventInfo _OnLevelCompleteEvent;
        private static Delegate _OnLevelCompleteProxy;

        public static class Hooks {

            public static MethodInfo m_Level_RegisterAreaComplete;
            public delegate void d_RegisterAreaComplete(Level self);
            public static d_RegisterAreaComplete orig_RegisterAreaComplete;
            public static void RegisterAreaComplete(Level self) {
                bool completed = self.Completed;
                orig_RegisterAreaComplete(self);
                if (!completed) {
                    OnLevelComplete?.Invoke(self);
                }
            }

        }

        public static void Load() {
            Type hooks = typeof(Hooks);

            _OnLevelCompleteEvent = typeof(Everest.Events.Level).GetEvent("OnComplete");
            if (_OnLevelCompleteEvent != null) {
                _OnLevelCompleteProxy = new Action<Level>(level => OnLevelComplete?.Invoke(level)).CastDelegate(_OnLevelCompleteEvent.EventHandlerType);
                _OnLevelCompleteEvent.AddEventHandler(null, _OnLevelCompleteProxy);
            } else {
                Hooks.m_Level_RegisterAreaComplete = typeof(Level).GetMethod("RegisterAreaComplete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Hooks.orig_RegisterAreaComplete = Hooks.m_Level_RegisterAreaComplete.Detour<Hooks.d_RegisterAreaComplete>(hooks.GetMethod("RegisterAreaComplete"));
            }
        }

        public static void Unload() {
            _OnLevelCompleteEvent?.RemoveEventHandler(null, _OnLevelCompleteProxy);
            Hooks.m_Level_RegisterAreaComplete?.Undetour();
        }

    }
}
