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

        // TODO: Timeout? Auth? Proper server functionality?

        public bool IsRunning { get; protected set; } = false;

        public TcpListener ManagementListener;
        public UdpClient UpdateClient;

        // Pseudo-connection because sending / receiving data on the same machine sucks on Windows.
        public GhostNetConnection LocalConnectionToServer;

        // Used to broadcast updates.
        public GhostNetConnection UpdateConnection;

        // All managed player connections.
        public List<GhostNetConnection> Connections = new List<GhostNetConnection>();
        public Dictionary<IPEndPoint, GhostNetConnection> ConnectionMap = new Dictionary<IPEndPoint, GhostNetConnection>();
        public Dictionary<IPAddress, Queue<GhostNetConnection>> UpdateConnectionQueue = new Dictionary<IPAddress, Queue<GhostNetConnection>>();
        public Dictionary<uint, GhostNetFrame> GhostMap = new Dictionary<uint, GhostNetFrame>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();

        public Thread ListenerThread;

        // Allows testing a subset of GhostNetMod's functions in an easy manner.
        public bool AllowLoopbackGhost = false;

        public GhostNetServer(Game game)
            : base(game) {
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
        }

        public void Accept(GhostNetConnection con) {
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{Connections.Count} ({con.ManagementEndPoint}) accepted");
            Connections.Add(con);
            ConnectionMap[con.ManagementEndPoint] = con;
            Queue<GhostNetConnection> queue;
            if (!UpdateConnectionQueue.TryGetValue(con.ManagementEndPoint.Address, out queue)) {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = queue = new Queue<GhostNetConnection>();
            }
            queue.Enqueue(con);
        }

        protected virtual void ListenerLoop() {
            while (IsRunning) {
                Thread.Sleep(0);

                while (ManagementListener.Pending()) {
                    // Updates are handled via WorldUpdateConnection.
                    // Receive management updates in a separate connection.
                    TcpClient client = ManagementListener.AcceptTcpClient();
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{Connections.Count} ({client.Client.RemoteEndPoint}) connected");
                    Accept(new GhostNetRemoteConnection(
                        client,
                        null
                    ) {
                        OnReceiveManagement = OnReceiveManagement,
                        OnDisconnect = OnDisconnect
                    });
                }
            }
        }

        protected virtual void OnReceiveManagement(GhostNetConnection conReceived, IPEndPoint remote, GhostNetFrame frame) {
            GhostNetConnection con;
            // We can receive frames from LocalConnectionToServer, which isn't "valid" when we want to send back data.
            // Get the management connection to the remote client.
            if (conReceived == null || !ConnectionMap.TryGetValue(remote, out con) || con == null)
                return;

            if (!frame.HasNetManagement0)
                return;

            SetNetHead(con, ref frame);

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.PlayerID} {frame.Name} in {frame.SID} {frame.Level}");

            GhostIndices[frame.PlayerID] = 0;
            GhostMap[frame.PlayerID] = frame;

            // Propagate management to all other players.
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != null && (AllowLoopbackGhost || otherCon != con))
                    otherCon.SendManagement(frame);

            // Inform the player about all existing ghosts.
            GhostNetFrame prev;
            if (!GhostMap.TryGetValue(frame.PlayerID, out prev) ||
                (prev.HasNetHead0 && prev.HasNetManagement0 && (prev.SID != frame.SID || prev.Level != frame.Level))
            ) {
                foreach (KeyValuePair<uint, GhostNetFrame> otherFrame in GhostMap) {
                    if ((!AllowLoopbackGhost && otherFrame.Key == frame.PlayerID) ||
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
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection conReceived, IPEndPoint remote, GhostNetFrame frame) {
            GhostNetConnection con;
            // We receive updates either from LocalConnectionToServer or from UpdateConnection.
            // Get the management connection to the remote client.
            if (conReceived == null || !ConnectionMap.TryGetValue(remote, out con) || con == null) {
                // Unlike management connections, which we already know the target port of at the time of connection,
                // updates are sent via UDP (by default) and thus "connectionless."
                // If we've got a queued connection for that address, update it.
                Queue<GhostNetConnection> queue;
                if (UpdateConnectionQueue.TryGetValue(remote.Address, out queue) && queue.Count > 0) {
                    con = queue.Dequeue();
                    con.UpdateEndPoint = remote;
                    ConnectionMap[con.UpdateEndPoint] = con;
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Mapped update connection ({con.ManagementEndPoint}, {con.UpdateEndPoint})");
                } else {
                    // If the address is completely unknown, drop the frame.
                    Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unknown update from {remote} - unknown connection, possibly premature");
                    return;
                }
            }

            if (!frame.HasNetUpdate0) {
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Update-less frame from {remote}");
                return;
            }

            SetNetHead(con, ref frame);

            GhostNetFrame managed;
            if (!GhostMap.TryGetValue(frame.PlayerID, out managed)) {
                // Ghost not managed - ignore the update.
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unknown update from #{frame.PlayerID} ({remote}) - unmanaged ghost");
                return;
            }

            // Prevent unordered outdated frames from being handled.
            if (frame.UpdateIndex < GhostIndices[frame.PlayerID]) {
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Out of order update from #{frame.PlayerID} ({remote}) - got {frame.UpdateIndex}, newest is {GhostIndices[frame.PlayerID]}");
                return;
            }
            GhostIndices[frame.PlayerID] = frame.UpdateIndex;

            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nU0 from #{frame.PlayerID} ({remote})");

            // Propagate update to all active players in the same room.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null || (!AllowLoopbackGhost && otherCon == con))
                    continue;

                GhostNetFrame otherManaged;
                if (!GhostMap.TryGetValue((uint) i, out otherManaged) ||
                    managed.SID != otherManaged.SID ||
                    managed.Level != otherManaged.Level
                ) {
                    continue;
                }

                // TODO: Make otherCon.SendUpdate call UpdateConnection.SendUpdate if it's "shared."
                if (!(otherCon is GhostNetRemoteConnection)) {
                    otherCon.SendUpdate(frame);
                } else if (otherCon.UpdateEndPoint != null) {
                    UpdateConnection.SendUpdate(otherCon.UpdateEndPoint, frame);
                }
            }
        }

        protected virtual void OnDisconnect(GhostNetConnection con) {
            uint id = (uint) Connections.IndexOf(con);
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{id} ({con.ManagementEndPoint}) disconnected");

            Connections[(int) id] = null;

            ConnectionMap[con.ManagementEndPoint] = null;

            Queue<GhostNetConnection> queue;
            if (con.UpdateEndPoint != null) {
                ConnectionMap[con.UpdateEndPoint] = null;
            } else if (UpdateConnectionQueue.TryGetValue(con.ManagementEndPoint.Address, out queue) && queue.Count > 0) {
                queue.Dequeue();
            }

            // Propagate disconnect to all other players.
            GhostNetFrame frame = new GhostNetFrame {
                HasNetHead0 = true,
                PlayerID = id,

                HasNetManagement0 = true,
                Name = "",
                SID = "",
                Level = ""
            };
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != null && otherCon != con)
                    otherCon.SendManagement(frame);
        }

        protected virtual void SetNetHead(GhostNetConnection con, ref GhostNetFrame frame) {
            frame.HasNetHead0 = true;
            frame.PlayerID = (uint) Connections.IndexOf(con);
        }

        public void Start() {
            if (IsRunning) {
                Logger.Log(LogLevel.Warn, "ghostnet-s", "Server already running, restarting");
                Stop();
            }

            Logger.Log(LogLevel.Info, "ghostnet-s", "Starting server");
            IsRunning = true;

            ManagementListener = new TcpListener(IPAddress.Any, GhostNetModule.Settings.Port);
            ManagementListener.Start();

            UpdateClient = new UdpClient(GhostNetModule.Settings.Port);
            UpdateConnection = new GhostNetRemoteConnection(
                null,
                UpdateClient
            ) {
                OnReceiveUpdate = OnReceiveUpdate
            };

            // Fake connection for any local clients running in the same instance.
            LocalConnectionToServer = new GhostNetLocalConnection {
                OnReceiveManagement = OnReceiveManagement,
                OnReceiveUpdate = OnReceiveUpdate,
                OnDisconnect = OnDisconnect
            };

            ListenerThread = new Thread(ListenerLoop);
            ListenerThread.IsBackground = true;
            ListenerThread.Start();
        }

        public void Stop() {
            if (!IsRunning)
                return;
            Logger.Log(LogLevel.Info, "ghostnet-s", "Stopping server");
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

            LocalConnectionToServer.Dispose();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }

    }
}
