using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost.Net {
    public class GhostNetModuleSettings : EverestModuleSettings {

        #region Main Settings

        [SettingInGame(false)]
        public string Name {
            get {
                return GhostModule.Settings.Name;
            }
            set {
                GhostModule.Settings.Name = value;
            }
        }

        [YamlIgnore]
        public bool Connection {
            get {
                return GhostNetModule.Instance.Client?.Connection != null;
            }
            set {
                if (value) {
                    GhostNetModule.ResetGhostModuleSettings();

                    GhostNetModule.Instance.Start();
                } else {
                    GhostNetModule.Instance.Stop();
                }
                if (ServerEntry != null)
                    ServerEntry.Disabled = value;
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public TextMenu.OnOff EnabledEntry { get; protected set; }

        [SettingIgnore]
        [YamlMember(Alias = "Server")]
        public string _Server { get; set; } = "celeste.0x0ade.ga";
        [YamlIgnore]
        public string Server {
            get {
                return _Server;
            }
            set {
                _Server = value;

                if (Connection)
                    GhostNetModule.Instance.Start();
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public TextMenu.Button ServerEntry { get; protected set; }

        #endregion

        #region Client Settings

        public bool Collision { get; set; } = true;

        public bool Sounds { get; set; } = true;

        [SettingIgnore]
        // [SettingRange(0, 3)]
        public int SendFrameSkip { get; set; } = 0;

        [SettingIgnore]
        [SettingRange(4, 16)]
        public int ChatLogLength { get; set; } = 8;

        [SettingIgnore]
        public bool SendUFramesInMStream { get; set; } = false;

        [SettingIgnore]
        public string[] EmoteFavs { get; set; }

        #endregion

        #region Server Settings
        [SettingIgnore]
        public string ServerName { get; set; } = "";
        [SettingIgnore]
        [YamlIgnore]
        public string ServerNameAuto =>
            !string.IsNullOrEmpty(ServerName) ? ServerName :
            $"{GhostModule.Settings.Name}'{(GhostModule.Settings.Name.ToLowerInvariant().EndsWith("s") ? "" : "s")} server";

        [SettingIgnore]
        public string ServerMessageGreeting { get; set; } = "Welcome ((player))#((id)), to ((server))!";
        [SettingIgnore]
        public string ServerMessageMOTD { get; set; } =
@"Don't cheat and have fun!
Press T to talk.
Send /help for a list of all commands.";
        [SettingIgnore]
        public string ServerMessageLeave { get; set; } = "Cya, ((player))#((id))!";

        [SettingIgnore]
        public int ServerMaxNameLength { get; set; } = 16;
        [SettingIgnore]
        public int ServerMaxEmoteValueLength { get; set; } = 2048;
        [SettingIgnore]
        public int ServerMaxChatTextLength { get; set; } = 256;
        [SettingIgnore]
        public string ServerCommandPrefix { get; set; } = "/";

        [YamlMember(Alias = "ServerColorInfo")]
        [SettingIgnore]
        public string ServerColorDefaultHex {
            get {
                return ServerColorDefault.R.ToString("X2") + ServerColorDefault.G.ToString("X2") + ServerColorDefault.B.ToString("X2");
            }
            set {
                if (string.IsNullOrEmpty(value))
                    return;
                try {
                    ServerColorDefault = Calc.HexToColor(value);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "rainbowmod", "Invalid ServerColorDefault!");
                    e.LogDetailed();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public Color ServerColorDefault { get; set; } = Color.LightSlateGray;

        [YamlMember(Alias = "ServerColorBroadcast")]
        [SettingIgnore]
        public string ServerColorBroadcastHex {
            get {
                return ServerColorBroadcast.R.ToString("X2") + ServerColorBroadcast.G.ToString("X2") + ServerColorBroadcast.B.ToString("X2");
            }
            set {
                if (string.IsNullOrEmpty(value))
                    return;
                try {
                    ServerColorBroadcast = Calc.HexToColor(value);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "rainbowmod", "Invalid ServerColorBroadcast!");
                    e.LogDetailed();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public Color ServerColorBroadcast { get; set; } = Color.Yellow;

        [YamlMember(Alias = "ServerColorError")]
        [SettingIgnore]
        public string ServerColorErrorHex {
            get {
                return ServerColorError.R.ToString("X2") + ServerColorError.G.ToString("X2") + ServerColorError.B.ToString("X2");
            }
            set {
                if (string.IsNullOrEmpty(value))
                    return;
                try {
                    ServerColorError = Calc.HexToColor(value);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "rainbowmod", "Invalid ServerColorError!");
                    e.LogDetailed();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public Color ServerColorError { get; set; } = Color.MediumVioletRed;

        [YamlMember(Alias = "ServerColorCommand")]
        [SettingIgnore]
        public string ServerColorCommandHex {
            get {
                return ServerColorCommand.R.ToString("X2") + ServerColorCommand.G.ToString("X2") + ServerColorCommand.B.ToString("X2");
            }
            set {
                if (string.IsNullOrEmpty(value))
                    return;
                try {
                    ServerColorCommand = Calc.HexToColor(value);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "rainbowmod", "Invalid ServerColorCommand!");
                    e.LogDetailed();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public Color ServerColorCommand { get; set; } = Color.DarkOliveGreen;

        [YamlMember(Alias = "ServerColorEmote")]
        [SettingIgnore]
        public string ServerColorEmoteHex {
            get {
                return ServerColorEmote.R.ToString("X2") + ServerColorEmote.G.ToString("X2") + ServerColorEmote.B.ToString("X2");
            }
            set {
                if (string.IsNullOrEmpty(value))
                    return;
                try {
                    ServerColorEmote = Calc.HexToColor(value);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "rainbowmod", "Invalid ServerColorEmote!");
                    e.LogDetailed();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public Color ServerColorEmote { get; set; } = Color.LightSeaGreen;

        #endregion

        #region Input Settings

        public List<Buttons> ButtonPlayerList { get; set; } = new List<Buttons>();
        public List<Buttons> ButtonEmoteSend { get; set; } = new List<Buttons>();
        public List<Buttons> ButtonChat { get; set; } = new List<Buttons>();

        #endregion

        #region Helpers

        [SettingIgnore]
        [YamlIgnore]
        public string Host {
            get {
                string server = Server.ToLowerInvariant();
                int indexOfPort;
                int port;
                if (!string.IsNullOrEmpty(Server) &&
                    (indexOfPort = server.LastIndexOf(':')) != -1 &&
                    int.TryParse(server.Substring(indexOfPort + 1), out port)
                ) {
                    return server.Substring(0, indexOfPort);
                }

                return server;
            }
        }
        [SettingIgnore]
        [YamlIgnore]
        public int Port {
            get {
                string server = Server;
                int indexOfPort;
                int port;
                if (!string.IsNullOrEmpty(Server) &&
                    (indexOfPort = server.LastIndexOf(':')) != -1 &&
                    int.TryParse(server.Substring(indexOfPort + 1), out port)
                ) {
                    return port;
                }

                // Default port
                return 2782;
            }
        }

        [SettingIgnore]
        [YamlIgnore]
        public bool IsHost =>
            Host == "localhost" ||
            Host == "127.0.0.1"
        ;

        #endregion

        #region Custom Entry Creators

        public void CreateConnectionEntry(TextMenu menu, bool inGame) {
            if (Celeste.PlayMode == Celeste.PlayModes.Debug)
                menu.Add(new TextMenu.SubHeader("modoptions_ghostnetmodule_debugwarn".DialogCleanOrNull() ?? "WARNING: DEBUG MODE DETECTED!"));
            menu.Add(
                (EnabledEntry = new TextMenu.OnOff("modoptions_ghostnetmodule_connected".DialogCleanOrNull() ?? "Connected", Connection))
                .Change(v => Connection = v)
            );
        }

        public void CreateServerEntry(TextMenu menu, bool inGame) {
            menu.Add(
                (ServerEntry = new TextMenu.Button(("modoptions_ghostnetmodule_server".DialogCleanOrNull() ?? "Server") + ": " + Server))
                .Pressed(() => {
                    Audio.Play("event:/ui/main/savefile_rename_start");
                    menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                        Server,
                        v => Server = v,
                        maxValueLength: 30
                    );
                })
            );
            ServerEntry.Disabled = inGame || Connection;
        }

        #endregion

    }
}
