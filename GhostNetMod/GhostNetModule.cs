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
        public VirtualJoystick JoystickEmoteWheel;
        public VirtualButton ButtonEmoteSend;
        public VirtualButton ButtonChat;

        public override void LoadSettings() {
            base.LoadSettings();

            if (Settings.EmoteFavs == null || Settings.EmoteFavs.Length == 0) {
                Settings.EmoteFavs = new string[] {
                    "i:collectables/heartgem/0/spin00",
                    "i:collectables/strawberry",
                    "Hi!",
                    "Too slow!",
                    "p:madeline/normal04",
                    "p:ghost/scoff03",
                    "p:theo/yolo03 theo/yolo02 theo/yolo01 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02 theo/yolo02",
                    "p:granny/laugh00 granny/laugh01 granny/laugh02 granny/laugh03",
                };
            }
        }

        public override void Load() {
            Everest.Events.Input.OnInitialize += OnInputInitialize;
            Everest.Events.Input.OnDeregister += OnInputDeregister;

            GhostNetModuleBackCompat.Load();

            // Example of a MP server mod.
            GhostNetServer.OnCreate += GhostNetRaceManager.OnCreateServer;
        }

        public override void Unload() {
            Everest.Events.Input.OnInitialize -= OnInputInitialize;
            Everest.Events.Input.OnDeregister -= OnInputDeregister;
            Stop();
            OnInputDeregister();

            GhostNetModuleBackCompat.Unload();
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
            AddButtonsTo(ButtonPlayerList, Settings.ButtonPlayerList);

            JoystickEmoteWheel = new VirtualJoystick(true,
                new VirtualJoystick.PadRightStick(Input.Gamepad, 0.2f)
            );
            ButtonEmoteSend = new VirtualButton(
                new VirtualButton.KeyboardKey(Keys.Q),
                new VirtualButton.PadButton(Input.Gamepad, Buttons.RightStick)
            );
            AddButtonsTo(ButtonEmoteSend, Settings.ButtonEmoteSend);

            ButtonChat = new VirtualButton(
                new VirtualButton.KeyboardKey(Keys.T)
            );
            AddButtonsTo(ButtonEmoteSend, Settings.ButtonChat);
        }

        public void OnInputDeregister() {
            ButtonPlayerList?.Deregister();
            JoystickEmoteWheel?.Deregister();
            ButtonEmoteSend?.Deregister();
            ButtonChat?.Deregister();
        }

        private static void AddButtonsTo(VirtualButton vbtn, List<Buttons> buttons) {
            if (buttons == null)
                return;
            foreach (Buttons button in buttons) {
                if (button == Buttons.LeftTrigger) {
                    vbtn.Nodes.Add(new VirtualButton.PadLeftTrigger(Input.Gamepad, 0.25f));
                } else if (button == Buttons.RightTrigger) {
                    vbtn.Nodes.Add(new VirtualButton.PadRightTrigger(Input.Gamepad, 0.25f));
                } else {
                    vbtn.Nodes.Add(new VirtualButton.PadButton(Input.Gamepad, button));
                }
            }
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
