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

        public GhostNetConnection UpdateConnection;
        public List<GhostNetConnection> Connections = new List<GhostNetConnection>();
        public Dictionary<IPEndPoint, GhostNetConnection> ConnectionMap = new Dictionary<IPEndPoint, GhostNetConnection>();
        public Dictionary<uint, GhostNetFrame> GhostMap = new Dictionary<uint, GhostNetFrame>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();

        public Thread ListenerThread;

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
                    // Updates are handled via WorldUpdateConnection.
                    // Receive management updates in a separate connection.
                    Connections.Add(new GhostNetConnection(
                        ManagementListener.AcceptTcpClient(),
                        null,
                        OnReceiveManagement,
                        null
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
                if (otherCon != null && otherCon != con)
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

            GhostIndices[frame.PlayerID] = 0;
            GhostMap[frame.PlayerID] = frame;
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            // The receiving con is the WorldUpdateConnection. Get the actual connection.
            if (!ConnectionMap.TryGetValue(remote, out con) || con == null)
                return;

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

            // Prevent unordered outdated frames from being handled.
            if (frame.UpdateIndex <= GhostIndices[frame.PlayerID]) {
                return;
            }
            GhostIndices[frame.PlayerID] = frame.UpdateIndex;

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

                UpdateConnection.SendUpdate(remote, frame);
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
            UpdateConnection = new GhostNetConnection(
                null,
                UpdateClient,
                null,
                OnReceiveUpdate
            );

            ListenerThread = new Thread(ListenerLoop);
            ListenerThread.IsBackground = true;
            ListenerThread.Start();
        }

        public void Stop() {
            if (!IsRunning)
                return;
            Logger.Log("ghostnet-s", "Stopping server");
            IsRunning = false;

            ListenerThread.Join();

            ManagementListener.Stop();

            // Close all management connections.
            foreach (GhostNetConnection connection in Connections) {
                if (connection == null)
                    continue;
                connection.Dispose();
            }

            UpdateConnection.Dispose();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }

    }
}
