using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
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

        [YamlIgnore]
        public bool Connection {
            get {
                return GhostNetModule.Instance.Client?.Connection != null;
            }
            set {
                if (value) {
                    GhostModule.Settings.Mode &= ~GhostModuleMode.Play;
                    GhostNetModule.Instance.Start();
                } else {
                    GhostNetModule.Instance.Stop();
                }
            }
        }
        [YamlIgnore]
        [SettingIgnore]
        public TextMenu.OnOff EnabledEntry { get; protected set; }

        [SettingIgnore]
        [YamlMember(Alias = "Server")]
        public string _Server { get; set; } = "";
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

        [SettingRange(0, 3)]
        public int SendSkip { get; set; } = 0;

        [SettingRange(4, 16)]
        public int ChatLogLength { get; set; } = 8;

        [SettingIgnore]
        public bool SendUFramesInMStream { get; set; } = false;

        [SettingIgnore]
        public string[] Emotes { get; set; }

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
        public string ServerMessageLeave { get; set; } = "Cya, ((player))#((id))!";
        [SettingIgnore]
        public int ServerMaxNameLength { get; set; } = 16;
        [SettingIgnore]
        public int ServerMaxEmoteLength { get; set; } = 64;
        [SettingIgnore]
        public int ServerMaxChatLength { get; set; } = 64;


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

        public void CreateEnabledEntry(TextMenu menu, bool inGame) {
            menu.Add(
                (EnabledEntry = new TextMenu.OnOff("modoptions_ghostnetmodule_enabled".DialogCleanOrNull() ?? "Enabled", Connection))
                .Change(v => Connection = v)
            );
        }

        public void CreateServerEntry(TextMenu menu, bool inGame) {
            menu.Add(
                new TextMenu.Button(("modoptions_ghostnetmodule_server".DialogCleanOrNull() ?? "Server") + ": " + Server)
                .Pressed(() => {
                    Audio.Play("event:/ui/main/savefile_rename_start");
                    menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                        Server,
                        v => Server = v,
                        maxValueLength: 30
                    );
                })
            );
        }

    }
}
