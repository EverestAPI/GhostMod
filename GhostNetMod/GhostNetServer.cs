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
    public class GhostNetServer : GameComponent {

        public bool IsRunning { get; protected set; } = false;

        public TcpListener ManagementListener;
        public UdpClient UpdateClient;

        public List<GhostNetConnection> Connections = new List<GhostNetConnection>();

        public Thread UpdateThread;

        public GhostNetServer(Game game)
            : base(game) {
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
        }

        protected virtual void ListenerLoop() {
            while (IsRunning) {
                Thread.Sleep(0);

                while (ManagementListener.Pending()) {
                    Connections.Add(new GhostNetConnection(
                        ManagementListener.AcceptTcpClient(),
                        UpdateClient,
                        OnReceiveManagement,
                        OnReceiveUpdate
                    ));
                }
            }
        }

        protected virtual void OnReceiveManagement(GhostNetConnection con, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetManagement0)
                return;

            frame.PlayerID = (uint) Connections.IndexOf(con);

            foreach (GhostNetConnection other in Connections)
                if (other != con)
                    other.SendManagement(frame);
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetUpdate0)
                return;

            // TODO: Only send updates to clients in matching rooms.

            frame.PlayerID = (uint) Connections.IndexOf(con);

            foreach (GhostNetConnection other in Connections)
                if (other != con)
                    other.SendUpdate(frame);
        }

        public void Start() {
            if (IsRunning) {
                Logger.Log("ghostnet-s", "Server already running, restarting");
                Stop();
            }

            Logger.Log("ghostnet-s", "Starting server");
            IsRunning = true;

            ManagementListener = new TcpListener(IPAddress.Any, GhostNetModule.Settings.Port);
            ManagementListener.Start();

            UpdateClient = new UdpClient(GhostNetModule.Settings.Port);

            UpdateThread = new Thread(ListenerLoop);
            UpdateThread.IsBackground = true;
            UpdateThread.Start();
        }

        public void Stop() {
            if (!IsRunning)
                return;
            Logger.Log("ghostnet-s", "Stopping server");
            IsRunning = false;

            UpdateThread.Join();

            ManagementListener.Stop();

            foreach (GhostNetConnection connection in Connections) {
                connection.UpdateClient = null; // Closed separately.
                connection.Dispose();
            }

            UpdateClient.Close();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }

    }
}
