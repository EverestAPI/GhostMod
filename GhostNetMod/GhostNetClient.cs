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

        public GhostManager GhostManager;
        public GhostRecorder GhostRecorder;

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

            // TODO: Update client-side ghosts.

        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetUpdate0)
                return;

            // TODO: Update client-side ghosts.

        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (isFromLoader) {
                GhostManager?.RemoveSelf();
                GhostManager = null;
                GhostRecorder?.RemoveSelf();
                GhostRecorder = null;
            }

            Step(level);
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            if (mode == LevelExit.Mode.Completed ||
                mode == LevelExit.Mode.CompletedInterlude) {
                Step(level);
            }
        }

        public void Step(Level level) {
            if (!IsRunning)
                return;

            string target = level.Session.Level;
            Logger.Log("ghost-c", $"Stepping into {level.Session.Area.GetSID()} {target}");

            Player player = level.Tracker.GetEntity<Player>();

            GhostManager?.RemoveSelf();
            level.Add(GhostManager = new GhostManager(player, level));

            if (GhostRecorder != null)
                GhostRecorder.RemoveSelf();
            level.Add(GhostRecorder = new GhostRecorder(player));

            Connection.SendManagement(new GhostNetFrame {
                HasNetManagement0 = true,

                Name = GhostModule.Settings.Name,
                SID = level.Session.Area.GetSID(),
                Level = level.Session.Level
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
                new TcpClient(GhostNetModule.Settings.Host, GhostNetModule.Settings.Port),
                new UdpClient(GhostNetModule.Settings.Port),
                OnReceiveManagement,
                OnReceiveUpdate
            );

            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExit;
        }

        public void Stop() {
            if (!IsRunning)
                return;
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
