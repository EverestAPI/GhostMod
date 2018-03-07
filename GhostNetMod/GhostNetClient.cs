using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost.Net {
    public class GhostNetClient : GameComponent {

        // Prevent killing the network connection by hammering too much UDP data.
        public static int UpdateModulo = 4;

        public bool IsRunning { get; protected set; } = false;

        public GhostNetConnection Connection;

        public int UpdateIndex;

        public Player Player;
        public Session Session;
        public GhostRecorder GhostRecorder;

        public Dictionary<uint, Ghost> GhostMap = new Dictionary<uint, Ghost>();

        public GhostNetClient(Game game)
            : base(game) {
        }

        public override void Update(GameTime gameTime) {
            if ((UpdateIndex % UpdateModulo) == 0) {
                Connection.SendUpdate(new GhostNetFrame {
                    Frame = GhostRecorder.LastFrameData,

                    HasNetUpdate0 = true,
                    UpdateIndex = (uint) UpdateIndex
                });
            }

            UpdateIndex++;

            base.Update(gameTime);
        }

        protected virtual void OnReceiveManagement(GhostNetConnection con, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetManagement0)
                return;

            if (Session == null || Player == null)
                return;

            Ghost ghost;

            if (frame.SID != Session.Area.GetSID() ||
                frame.Level != Session.Level) {
                // Find the ghost and remove it if it exists.
                if (GhostMap.TryGetValue(frame.PlayerID, out ghost) && ghost != null) {
                    ghost.RemoveSelf();
                    GhostMap[frame.PlayerID] = null;
                }
                return;
            }


            if (!GhostMap.TryGetValue(frame.PlayerID, out ghost) || ghost == null) {
                Player.Scene.Add(ghost = new Ghost(Player));
            }

            ghost.Name.Name = frame.Name;
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetUpdate0)
                return;

            if (Session == null || Player == null)
                return;

            Ghost ghost;
            if (!GhostMap.TryGetValue(frame.PlayerID, out ghost) || ghost == null) {
                return;
            }

            ghost.ForcedFrame = frame.Frame;
        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (!IsRunning)
                return;

            Session = level.Session;

            string target = Session.Level;
            Logger.Log("ghost-c", $"Stepping into {Session.Area.GetSID()} {target}");

            Player = level.Tracker.GetEntity<Player>();

            foreach (Ghost ghost in GhostMap.Values)
                ghost?.RemoveSelf();
            GhostMap.Clear();

            GhostRecorder?.RemoveSelf();
            level.Add(GhostRecorder = new GhostRecorder(Player));

            Connection.SendManagement(new GhostNetFrame {
                HasNetManagement0 = true,

                Name = GhostModule.Settings.Name,
                SID = Session.Area.GetSID(),
                Level = Session.Level
            });
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            if (!IsRunning)
                return;

            Session = null;

            Logger.Log("ghost-c", $"Leaving session");

            foreach (Ghost ghost in GhostMap.Values)
                ghost?.RemoveSelf();
            GhostMap.Clear();

            GhostRecorder?.RemoveSelf();
            GhostRecorder = null;

            Connection.SendManagement(new GhostNetFrame {
                HasNetManagement0 = true,

                Name = GhostModule.Settings.Name,
                SID = "",
                Level = ""
            });
        }

        public void Start() {
            if (IsRunning) {
                Logger.Log("ghostnet-c", "Client already running, restarting");
                Stop();
            }

            Logger.Log("ghostnet-c", "Starting client");
            IsRunning = true;

            Connection = new GhostNetConnection(
                GhostNetModule.Settings.Host,
                GhostNetModule.Settings.Port,
                OnReceiveManagement,
                OnReceiveUpdate
            );

            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExit;
        }

        public void Stop() {
            if (!IsRunning)
                return;

            OnExit(null, null, LevelExit.Mode.SaveAndQuit, null, null);

            Logger.Log("ghostnet-c", "Stopping client");
            IsRunning = false;

            Connection.Dispose();

            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            Everest.Events.Level.OnExit -= OnExit;
        }

        private bool disposed = false;

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (disposed)
                return;

            if (disposing) {
                Stop();
            }

            disposed = true;
        }

    }
}
