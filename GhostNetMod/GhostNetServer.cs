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
        public Dictionary<uint, GhostNetFrame> GhostMap = new Dictionary<uint, GhostNetFrame>();

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

            SetNetHead(con, frame);

            // Propagate management to all other players.
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != con)
                    otherCon.SendManagement(frame);

            // Inform the player about all existing ghosts on room change.
            GhostNetFrame prev;
            if (GhostMap.TryGetValue(frame.PlayerID, out prev) &&
                prev.HasNetHead0 &&
                prev.HasNetManagement0
            ) {
                foreach (KeyValuePair<uint, GhostNetFrame> otherFrame in GhostMap) {
                    if (otherFrame.Key == frame.PlayerID ||
                        !otherFrame.Value.HasNetHead0 ||
                        !otherFrame.Value.HasNetManagement0 ||
                        frame.SID != otherFrame.Value.SID ||
                        frame.Level != otherFrame.Value.Level
                    ) {
                        continue;
                    }
                    con.SendManagement(otherFrame.Value);
                }
            }

            GhostMap[frame.PlayerID] = frame;
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, GhostNetFrame frame) {
            if (!frame.HasNetHead0 || !frame.HasNetUpdate0)
                return;

            SetNetHead(con, frame);

            GhostNetFrame managed;
            if (!GhostMap.TryGetValue(frame.PlayerID, out managed) ||
                !managed.HasNetHead0 ||
                !managed.HasNetManagement0
            ) {
                // Ghost not managed - ignore the update.
                return;
            }

            // Propagate update to all active players in the same room.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null || otherCon == con)
                    continue;

                GhostNetFrame otherManaged;
                if (!GhostMap.TryGetValue((uint) i, out otherManaged) ||
                    !otherManaged.HasNetHead0 ||
                    !otherManaged.HasNetManagement0 ||
                    managed.SID != otherManaged.SID ||
                    managed.Level != otherManaged.Level
                ) {
                    continue;
                }

                otherCon.SendUpdate(frame);
            }
        }

        protected virtual void SetNetHead(GhostNetConnection con, GhostNetFrame frame) {
            frame.HasNetHead0 = true;
            frame.PlayerID = (uint) Connections.IndexOf(con);
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

            // Close all management connections.
            foreach (GhostNetConnection connection in Connections) {
                if (connection == null)
                    continue;
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
