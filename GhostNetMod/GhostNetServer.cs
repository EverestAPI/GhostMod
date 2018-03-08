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

        #region Management Connection Listener

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

        public virtual void Accept(GhostNetConnection con) {
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{Connections.Count} ({con.ManagementEndPoint}) accepted");
            Connections.Add(con);
            ConnectionMap[con.ManagementEndPoint] = con;
            Queue<GhostNetConnection> queue;
            if (!UpdateConnectionQueue.TryGetValue(con.ManagementEndPoint.Address, out queue)) {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = queue = new Queue<GhostNetConnection>();
            }
            queue.Enqueue(con);
        }

        #endregion

        #region Frame Parsers

        public virtual void Parse(GhostNetConnection con, ref GhostNetFrame frame) {
            SetNetHead(con, ref frame);

            if (!frame.H0.IsValid)
                return;

            if (frame.M0.IsValid)
                ParseM0(con, ref frame);

            if (frame.U0.IsValid)
                ParseU0(con, ref frame);
        }

        public virtual void ParseM0(GhostNetConnection con, ref GhostNetFrame frame) {
            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.H0.PlayerID} {frame.M0.Name} in {frame.M0.SID} {frame.M0.Level}");

            // Propagate management frame to all other players.
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != null && (AllowLoopbackGhost || otherCon != con))
                    otherCon.SendManagement(frame);

            // Inform the player about all existing ghosts.
            foreach (KeyValuePair<uint, GhostNetFrame> otherFrame in GhostMap) {
                if ((!AllowLoopbackGhost && otherFrame.Key == frame.H0.PlayerID) ||
                    frame.M0.SID != otherFrame.Value.M0.SID ||
                    frame.M0.Level != otherFrame.Value.M0.Level
                ) {
                    continue;
                }
                con.SendManagement(otherFrame.Value);
            }

            GhostIndices[frame.H0.PlayerID] = 0;
            GhostMap[frame.H0.PlayerID] = frame;
        }

        public virtual void ParseU0(GhostNetConnection con, ref GhostNetFrame frame) {
            GhostNetFrame managed;
            if (!GhostMap.TryGetValue(frame.H0.PlayerID, out managed)) {
                // Ghost not managed - ignore the update.
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unknown update from #{frame.H0.PlayerID} ({con.UpdateEndPoint}) - unmanaged ghost, possibly premature");
                return;
            }

            // Prevent unordered outdated frames from being handled.
            uint lastIndex;
            if (GhostIndices.TryGetValue(frame.H0.PlayerID, out lastIndex) && frame.U0.UpdateIndex < lastIndex) {
                // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Out of order update from #{frame.H0.PlayerID} ({con.UpdateEndPoint}) - got {frame.U0.UpdateIndex}, newest is {lastIndex]}");
                return;
            }
            GhostIndices[frame.H0.PlayerID] = frame.U0.UpdateIndex;

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nU0 from #{frame.H0.PlayerID} ({con.UpdateEndPoint})");

            // Propagate update to all active players in the same room.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null || (!AllowLoopbackGhost && otherCon == con))
                    continue;

                GhostNetFrame otherManaged;
                if (!GhostMap.TryGetValue((uint) i, out otherManaged) ||
                    managed.M0.SID != otherManaged.M0.SID ||
                    managed.M0.Level != otherManaged.M0.Level
                ) {
                    continue;
                }

                if (!(otherCon is GhostNetRemoteConnection)) {
                    otherCon.SendUpdate(frame);
                } else if (otherCon.UpdateEndPoint != null) {
                    UpdateConnection.SendUpdate(otherCon.UpdateEndPoint, frame);
                } else {
                    // Fallback for UDP-less clients.
                    otherCon.SendManagement(frame);
                }
            }
        }

        #endregion

        #region Connection Handlers

        protected virtual void SetNetHead(GhostNetConnection con, ref GhostNetFrame frame) {
            frame.H0 = new GhostChunkNetH0 {
                IsValid = true,
                PlayerID = (uint) Connections.IndexOf(con)
            };
        }

        protected virtual void OnReceiveManagement(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            // We can receive frames from LocalConnectionToServer, which isn't "valid" when we want to send back data.
            // Get the management connection to the remote client.
            if (con == null || !ConnectionMap.TryGetValue(remote, out con) || con == null)
                return;

            Parse(con, ref frame);
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

            Parse(con, ref frame);
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
                H0 = new GhostChunkNetH0 {
                    IsValid = true,
                    PlayerID = id
                },

                M0 = new GhostChunkNetM0 {
                    IsValid = true,
                    Name = "",
                    SID = "",
                    Level = ""
                }
            };
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != null && otherCon != con)
                    otherCon.SendManagement(frame);
        }

        #endregion

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
