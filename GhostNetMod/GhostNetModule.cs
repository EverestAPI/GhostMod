using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
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

        private bool _StartServer;
        private bool _StartHeadless;

        public override void LoadSettings() {
            base.LoadSettings();

            if (Settings.EmoteFavs == null || Settings.EmoteFavs.Length == 0) {
                Settings.EmoteFavs = new string[] {
                    "i:collectables/heartgem/0/spin",
                    "i:collectables/strawberry",
                    "Hi!",
                    "Too slow!",
                    "p:madeline/normal04",
                    "p:ghost/scoff03",
                    "p:theo/yolo03 theo/yolo02 theo/yolo01 theo/yolo02 END",
                    "p:granny/laugh",
                };
            }
        }

        public override void Load() {
            Everest.Events.Input.OnInitialize += OnInputInitialize;
            Everest.Events.Input.OnDeregister += OnInputDeregister;

            GhostNetHooks.Load();

            // Example of a MP server mod.
            GhostNetServer.OnCreate += GhostNetRaceManager.OnCreateServer;

            base.Initialize();

            Queue<string> args = new Queue<string>(Everest.Args);
            while (args.Count > 0) {
                string arg = args.Dequeue();
                if (arg == "--server") {
                    _StartServer = true;
                } else if (arg == "--headless") {
                    _StartHeadless = true;
                }
            }

            GhostModule.SettingsOverridden = true;
            ResetGhostModuleSettings();

            if (_StartServer && _StartHeadless) {
                // We don't care about other mods.
                GhostNetFrame.RegisterChunksFromModule(this);

                Start(true, true);
                RunDedicated();
                Environment.Exit(0);
            }
        }

        public override void Initialize() {
            base.Initialize();

            // Register after all mods have loaded.
            foreach (EverestModule module in Everest.Modules)
                GhostNetFrame.RegisterChunksFromModule(module);

            if (_StartServer && !_StartHeadless) {
                Start(true, true);
            }
        }

        public override void Unload() {
            Everest.Events.Input.OnInitialize -= OnInputInitialize;
            Everest.Events.Input.OnDeregister -= OnInputDeregister;
            Stop();
            OnInputDeregister();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);

            menu.Add(new TextMenu.Button("modoptions_ghostnetmodule_reloadhint".DialogCleanOrNull() ?? "More in ModSettings/...") {
                Disabled = true
            });

            menu.Add(new TextMenu.Button("modoptions_ghostnetmodule_reload".DialogCleanOrNull() ?? "Reload Settings").Pressed(() => {
                string server = Settings.Server;
                LoadSettings();
                if (Settings.Server != server)
                    Settings.Server = Settings._Server;
            }));
        }

        public static void ResetGhostModuleSettings() {
            string name = GhostModule.Settings.Name;
            GhostModule.Instance._Settings = new GhostModuleSettings();
            GhostModule.Settings.Mode = GhostModuleMode.Off;
            GhostModule.Settings.Name = name;
            GhostModule.Settings.NameFilter = "";
            GhostModule.Settings.ShowNames = true;
            GhostModule.Settings.ShowDeaths = true;
            GhostModule.Settings.InnerOpacity = 8;
            GhostModule.Settings.InnerHairOpacity = 8;
            GhostModule.Settings.OuterOpacity = 8;
            GhostModule.Settings.OuterHairOpacity = 8;
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

        public void Start(bool server = false, bool client = false) {
            Stop();

            if (Settings.IsHost || server) {
                Server = new GhostNetServer(Celeste.Instance);
                if (!_StartHeadless)
                    Celeste.Instance.Components.Add(Server);
                Server.OPs.Add(0);
                Server.Start();
            }

            if (!Settings.IsHost && server && !client)
                return;

            try {
                Client = new GhostNetClient(Celeste.Instance);
                if (!_StartHeadless)
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

        public void RunDedicated() {
            Logger.Log("ghostnet-s", "GhostNet headless server is online.");
            Logger.Log("ghostnet-s", $"Make sure to forward the ports {Settings.Port} TCP and UDP");
            Logger.Log("ghostnet-s", "and to let your firewall allow incoming connections.");
            Console.WriteLine("");
            Client.OnHandle += (con, frame) => {
                if (frame.Get<ChunkMChat>() != null)
                    Logger.Log("ghostnet-chat", new GhostNetClient.ChatLine(frame).ToString());
            };
            while (Server.IsRunning) {
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) 
                    continue;
                line = line.TrimEnd();
                if (line == "/quit") {
                    Stop();
                    return;
                }
                Client.SendMChat(line);
            }
        }

    }
}
