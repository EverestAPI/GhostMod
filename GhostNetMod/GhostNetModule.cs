using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;

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

        public VirtualButton ButtonPlayerList;

        public override void LoadSettings() {
            base.LoadSettings();

            if (Settings.Emotes == null || Settings.Emotes.Length == 0) {
                Settings.Emotes = new string[] {
                    "i:collectables/heartgem/0/spin00",
                    "i:collectables/strawberry",
                    "Hi!",
                    "Too slow!"
                };
            }
        }

        public override void Load() {
            Everest.Events.Input.OnInitialize += OnInputInitialize;
            Everest.Events.Input.OnDeregister += OnInputDeregister;
        }

        public override void Unload() {
            Everest.Events.Input.OnInitialize -= OnInputInitialize;
            Everest.Events.Input.OnDeregister -= OnInputDeregister;
            OnInputDeregister();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);

            menu.Add(new TextMenu.Button("modoptions_ghostnetmodule_reload".DialogCleanOrNull() ?? "Reload Settings").Pressed(() => {
                string server = Settings.Server;
                LoadSettings();
                if (Settings.Server != server)
                    Settings.Server = Settings._Server;
            }));
        }

        public void OnInputInitialize() {
            ButtonPlayerList = new VirtualButton(
                new VirtualButton.KeyboardKey(Keys.Tab),
                new VirtualButton.PadButton(Input.Gamepad, Buttons.Back)
            );
            // TODO: Expose this helper and allow custom binding entries.
            // InputExt.AddButtonsTo(PlayerListButton, Settings.BtnPlayerList);
        }

        public void OnInputDeregister() {
            ButtonPlayerList?.Deregister();
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
                if (Settings.EnabledEntry != null) {
                    Settings.EnabledEntry.LeftPressed();
                }
                Stop();
            }
        }

        public void Stop() {
            if (Client != null) {
                Client.Stop();
                Client = null;
            }

            if (Server != null) {
                Server.Stop();
                Server = null;
            }
        }

    }
}
