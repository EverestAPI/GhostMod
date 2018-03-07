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

namespace Celeste.Mod.Ghost.Net {
    public class GhostNetModule : EverestModule {

        public static GhostNetModule Instance;

        public GhostNetModule() {
            Instance = this;
        }

        public override Type SettingsType => typeof(GhostNetModuleSettings);
        public static GhostNetModuleSettings Settings => (GhostNetModuleSettings) Instance._Settings;

        public GhostNetServer Server;
        public GhostNetClient Client;

        public override void Load() {
        }

        public override void Unload() {
        }

        public void Start() {
            Stop();

            if (Settings.IsHost) {
                Server = new GhostNetServer(Celeste.Instance);
                Celeste.Instance.Components.Add(Server);
                Server.Start();
            }

            try {
                Client = new GhostNetClient(Celeste.Instance);
                Celeste.Instance.Components.Add(Client);
                Client.Start();
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "ghostnet", "Failed starting client");
                e.LogDetailed();
                Client?.Stop();
                Client = null;
            }
        }

        public void Stop() {
            if (Client != null) {
                Client.Stop();
                Celeste.Instance.Components.Remove(Client);
                Client = null;
            }

            if (Server != null) {
                Server.Stop();
                Celeste.Instance.Components.Remove(Server);
                Server = null;
            }
        }

    }
}
