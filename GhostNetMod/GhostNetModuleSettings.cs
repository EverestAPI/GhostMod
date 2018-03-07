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

        [SettingIgnore]
        [YamlMember(Alias = "Enabled")]
        public bool _Enabled { get; set; } = false;
        [YamlIgnore]
        public bool Enabled {
            get {
                return _Enabled;
            }
            set {
                _Enabled = value;

                if (value) {
                    GhostNetModule.Instance.Start();
                } else {
                    GhostNetModule.Instance.Stop();
                }
            }
        }

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

                if (Enabled)
                    GhostNetModule.Instance.Start();
            }
        }

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

    }
}
