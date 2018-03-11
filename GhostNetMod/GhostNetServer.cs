using Celeste.Mod;
using Celeste.Mod.Helpers;
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
        public Dictionary<IPAddress, GhostNetConnection> UpdateConnectionQueue = new Dictionary<IPAddress, GhostNetConnection>();
        public Dictionary<uint, GhostNetFrame> PlayerMap = new Dictionary<uint, GhostNetFrame>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();

        public List<GhostNetFrame> ChatLog = new List<GhostNetFrame>();

        public Thread ListenerThread;

        public List<GhostNetCommand> Commands = new List<GhostNetCommand>();

        public static event Action<GhostNetServer> OnCreate;
        public event GhostNetFrameParser OnParse;
        // TODO: More events.

        // Allows testing a subset of GhostNetMod's functions in an easy manner.
        public bool AllowLoopbackGhost = false;

        public GhostNetServer(Game game)
            : base(game) {
            // Find all commands in all mods.
            foreach (Type type in FakeAssembly.GetFakeEntryAssembly().GetTypes()) {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    if (!typeof(GhostNetCommand).IsAssignableFrom(field.FieldType) ||
                        field.GetCustomAttribute<GhostNetCommandFieldAttribute>() == null)
                        continue;
                    GhostNetCommand cmd = field.GetValue(null) as GhostNetCommand;
                    if (cmd == null)
                        continue;
                    Commands.Add(cmd);
                }
            }

            OnCreate?.Invoke(this);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
        }

        public GhostNetCommand GetCommand(string cmdName) {
            cmdName = cmdName.ToLowerInvariant();
            for (int i = 0; i < Commands.Count; i++) {
                GhostNetCommand cmd = Commands[i];
                if (cmd.Name == cmdName)
                    return cmd;
            }
            return null;
        }

        public string FillVariables(string input, GhostNetFrame frame)
            => input
                .Replace("((player))", frame.MPlayer.Name)
                .Replace("((id))", frame.HHead.PlayerID.ToString())
                .Replace("((server))", GhostNetModule.Settings.ServerNameAuto);

        public GhostNetFrame CreateMChat(GhostNetConnection con, GhostNetFrame frame, string text, Color? color = null, bool fillVars = false) {
            lock (ChatLog) {
                GhostNetFrame msg = new GhostNetFrame {
                    HHead = {
                        IsValid = true,
                        PlayerID = uint.MaxValue
                    },

                    MChat = {
                        ID = (uint) ChatLog.Count,
                        Text = fillVars ? FillVariables(text, frame) : text,
                        Color = color ?? GhostNetModule.Settings.ServerColorDefault,
                        Date = DateTime.UtcNow,
                        KeepColor = true,
                        Logged = true
                    }
                };
                ChatLog.Add(msg);
                return msg;
            }
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
            uint id = (uint) Connections.Count;
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{id} ({con.ManagementEndPoint}) accepted");
            Connections.Add(con);
            ConnectionMap[con.ManagementEndPoint] = con;
            UpdateConnectionQueue[con.ManagementEndPoint.Address] = con;
            con.SendManagement(new GhostNetFrame {
                HHead = {
                    IsValid = true,
                    PlayerID = id
                },

                MServerInfo = {
                    IsValid = true
                }
            });
        }

        #endregion

        #region Frame Parsers

        protected virtual void SetNetHead(GhostNetConnection con, ref GhostNetFrame frame) {
            frame.HHead = new GhostChunkNetHHead {
                IsValid = true,
                PlayerID = (uint) Connections.IndexOf(con)
            };

            frame.MServerInfo.IsValid = false;
        }

        public virtual void Parse(GhostNetConnection con, ref GhostNetFrame frame) {
            SetNetHead(con, ref frame);

            if (!frame.HHead.IsValid)
                return;

            if (frame.MPlayer.IsValid)
                ParseMPlayer(con, ref frame);

            GhostNetFrame player;
            if (!PlayerMap.TryGetValue(frame.HHead.PlayerID, out player) || !player.HHead.IsValid) {
                // Ghost not managed - ignore the frame.
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Unexpected frame from #{frame.HHead.PlayerID} ({con.ManagementEndPoint}) - statusless ghost, possibly premature");
                return;
            }
            // Temporarily attach the MPlayer chunk to make player identification easier.
            bool mPlayerTemporary = !frame.MPlayer.IsValid;
            frame.MPlayer = player.MPlayer;

            if (frame.MRequest.IsValid) {
                // TODO: Handle requests by client in server.

                frame.MRequest.IsValid = false;
            }

            if (frame.MEmote.IsValid)
                ParseMEmote(con, ref frame);

            if (frame.MChat.IsValid)
                ParseMChat(con, ref frame);

            if (frame.UUpdate.IsValid)
                ParseUUpdate(con, ref frame);

            OnParse?.Invoke(con, ref frame);

            if (mPlayerTemporary)
                frame.MPlayer.IsValid = false;

            if (frame.PropagateM)
                PropagateM(con, ref frame);
            else if (frame.PropagateU)
                PropagateU(con, ref frame);
        }

        public virtual void ParseMPlayer(GhostNetConnection con, ref GhostNetFrame frame) {
            frame.MPlayer.Name = frame.MPlayer.Name.Replace("*", "").Replace("\r", "").Replace("\n", "").Trim();
            if (frame.MPlayer.Name.Length > GhostNetModule.Settings.ServerMaxNameLength)
                frame.MPlayer.Name = frame.MPlayer.Name.Substring(0, GhostNetModule.Settings.ServerMaxNameLength);

            if (string.IsNullOrWhiteSpace(frame.MPlayer.Name))
                frame.MPlayer.Name = "#" + frame.HHead.PlayerID;

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} {frame.MPlayer.Name} in {frame.MPlayer.SID} {(char) ('A' + frame.MPlayer.Mode)} {frame.MPlayer.Level}");

            // Propagate status to all other players.
            frame.PropagateM = true;

            if (!PlayerMap.ContainsKey(frame.HHead.PlayerID)) {
                // Player just connected.
                if (!string.IsNullOrWhiteSpace(GhostNetModule.Settings.ServerMessageGreeting)) {
                    BroadcastMChat(con, frame, GhostNetModule.Settings.ServerMessageGreeting, fillVars: true);
                    SendMChat(con, frame, GhostNetModule.Settings.ServerMessageMOTD, fillVars: true);
                }
            }

            // Inform the player about all existing ghosts.
            foreach (KeyValuePair<uint, GhostNetFrame> otherStatus in PlayerMap) {
                if (!AllowLoopbackGhost && otherStatus.Key == frame.HHead.PlayerID)
                    continue;
                con.SendManagement(new GhostNetFrame {
                    HHead = {
                        IsValid = true,
                        PlayerID = otherStatus.Key
                    },

                    MPlayer = otherStatus.Value.MPlayer
                });
            }

            GhostIndices[frame.HHead.PlayerID] = 0;
            PlayerMap[frame.HHead.PlayerID] = frame;
        }

        public virtual void ParseMEmote(GhostNetConnection con, ref GhostNetFrame frame) {
            // Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} emote: {frame.MEmote.Value}");

            frame.MEmote.Value = frame.MEmote.Value.Trim();
            if (frame.MEmote.Value.Length > GhostNetModule.Settings.ServerMaxEmoteLength)
                frame.MEmote.Value = frame.MEmote.Value.Substring(0, GhostNetModule.Settings.ServerMaxEmoteLength);

            if (GhostNetEmote.IsText(frame.MEmote.Value)) {
                frame.MChat = CreateMChat(con, frame, frame.MEmote.Value, GhostNetModule.Settings.ServerColorEmote).MChat;
            }

            frame.PropagateM = true;
        }

        public virtual void ParseMChat(GhostNetConnection con, ref GhostNetFrame frame) {
            frame.MChat.Text = frame.MChat.Text.TrimEnd();
            // Logger.Log(LogLevel.Info, "ghostnet-s", $"#{frame.HHead.PlayerID} said: {frame.MChat.Text}");

            // Parse commands if necessary.
            if (frame.MChat.Text.StartsWith(GhostNetModule.Settings.ServerCommandPrefix)) {
                // Echo the chat chunk separately.
                con.SendManagement(new GhostNetFrame {
                    HHead = frame.HHead,

                    MChat = CreateMChat(con, frame, frame.MChat.Text, GhostNetModule.Settings.ServerColorCommand).MChat
                });

                GhostNetCommandEnv env = new GhostNetCommandEnv {
                    Server = this,
                    Connection = con,
                    Frame = frame
                };

                string prefix = GhostNetModule.Settings.ServerCommandPrefix;

                // TODO: This is basically a port of disbot-neo's parser.

                string cmdName = env.Text.Substring(prefix.Length);
                cmdName = cmdName.Split(GhostNetCommand.CommandNameDelimiters)[0].ToLowerInvariant();
                if (cmdName.Length == 0)
                    return;

                GhostNetCommand cmd = GetCommand(cmdName);
                if (cmd != null) {
                    GhostNetFrame cmdFrame = frame;
                    Task.Run(() => {
                        try {
                            cmd.Parse(env);
                        } catch (Exception e) {
                            SendMChat(con, cmdFrame, $"Command {cmdName} failed: {e.Message}", color: GhostNetModule.Settings.ServerColorError, fillVars: false);
                            if (e.GetType() != typeof(Exception)) {
                                Logger.Log(LogLevel.Warn, "ghostnet-s", $"cmd failed: {env.Text}");
                                e.LogDetailed();
                            }
                        }
                    });

                } else {
                    SendMChat(con, frame, $"Command {cmdName} not found!", color: GhostNetModule.Settings.ServerColorError, fillVars: false);
                }

                return;
            }

            frame.MChat.Text.Replace("\r", "").Replace("\n", "");
            if (frame.MChat.Text.Length > GhostNetModule.Settings.ServerMaxChatLength)
                frame.MChat.Text = frame.MChat.Text.Substring(0, GhostNetModule.Settings.ServerMaxChatLength);

            if (!frame.MChat.KeepColor)
                frame.MChat.Color = Color.White;

            frame.MChat.Date = DateTime.UtcNow;

            if (!frame.MChat.Logged) {
                lock (ChatLog) {
                    frame.MChat.ID = (uint) ChatLog.Count;
                    ChatLog.Add(frame);
                }
            }

            frame.PropagateM = true;
        }

        public virtual void ParseUUpdate(GhostNetConnection con, ref GhostNetFrame frame) {
            // Prevent unordered outdated frames from being handled.
            uint lastIndex;
            if (GhostIndices.TryGetValue(frame.HHead.PlayerID, out lastIndex) && frame.UUpdate.UpdateIndex < lastIndex) {
                // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Out of order update from #{frame.H0.PlayerID} ({con.UpdateEndPoint}) - got {frame.U0.UpdateIndex}, newest is {lastIndex]}");
                return;
            }
            GhostIndices[frame.HHead.PlayerID] = frame.UUpdate.UpdateIndex;
            PlayerMap[frame.HHead.PlayerID] = frame;

            // Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Received nU0 from #{frame.H0.PlayerID} ({con.UpdateEndPoint})");

            // Propagate update to all active players in the same room.
            frame.PropagateU = true;
        }

        #endregion

        #region Frame Senders

        public void PropagateM(GhostNetConnection con, ref GhostNetFrame frame) {
            foreach (GhostNetConnection otherCon in Connections)
                if (otherCon != null)
                    otherCon.SendManagement(frame);
        }

        public void PropagateU(GhostNetConnection con, ref GhostNetFrame frame) {
            // U is always handled after M. Even if sending this fails, we shouldn't worry about loosing M chunks.
            for (int i = 0; i < Connections.Count; i++) {
                GhostNetConnection otherCon = Connections[i];
                if (otherCon == null || (!AllowLoopbackGhost && otherCon == con))
                    continue;

                GhostNetFrame otherPlayer;
                if (!PlayerMap.TryGetValue((uint) i, out otherPlayer) ||
                    frame.MPlayer.SID != otherPlayer.MPlayer.SID ||
                    frame.MPlayer.Mode != otherPlayer.MPlayer.Mode
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

        public GhostNetFrame BroadcastMChat(GhostNetConnection con, GhostNetFrame frame, string text, Color? color = null, bool fillVars = false) {
            GhostNetFrame msg = CreateMChat(con, frame, text, color ?? GhostNetModule.Settings.ServerColorBroadcast, fillVars);
            PropagateM(con, ref msg);
            return msg;
        }

        public GhostNetFrame SendMChat(GhostNetConnection con, GhostNetFrame frame, string text, Color? color = null, bool fillVars = false) {
            GhostNetFrame msg = CreateMChat(con, frame, text, color, fillVars);
            con.SendManagement(msg);
            return msg;
        }

        #endregion

        #region Connection Handlers

        protected virtual void OnReceiveManagement(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            // We can receive frames from LocalConnectionToServer, which isn't "valid" when we want to send back data.
            // Get the management connection to the remote client.
            if (con == null || !ConnectionMap.TryGetValue(remote, out con) || con == null)
                return;

            // If we received an update via the managed con, forget about the update con.
            if (frame.UUpdate.IsValid) {
                if (con.UpdateEndPoint != null) {
                    ConnectionMap[con.UpdateEndPoint] = null;
                    ConnectionMap[con.ManagementEndPoint] = con; // In case Managed == Update
                    con.UpdateEndPoint = null;
                } else {
                    UpdateConnectionQueue[con.ManagementEndPoint.Address] = null;
                }
            } else {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = con;
            }

            Parse(con, ref frame);
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection conReceived, IPEndPoint remote, GhostNetFrame frame) {
            // Prevent UpdateConnection locking in on a single player.
            if (conReceived == UpdateConnection)
                UpdateConnection.UpdateEndPoint = null;

            GhostNetConnection con;
            // We receive updates either from LocalConnectionToServer or from UpdateConnection.
            // Get the management connection to the remote client.
            if (conReceived == null || !ConnectionMap.TryGetValue(remote, out con) || con == null) {
                // Unlike management connections, which we already know the target port of at the time of connection,
                // updates are sent via UDP (by default) and thus "connectionless."
                // If we've got a queued connection for that address, update it.
                GhostNetConnection queue;
                if (UpdateConnectionQueue.TryGetValue(remote.Address, out queue) && queue != null) {
                    con = queue;
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
            if (!ConnectionMap.TryGetValue(con.ManagementEndPoint, out con) || con == null)
                return; // Probably already disconnected.

            uint id = (uint) Connections.IndexOf(con);
            if (id == uint.MaxValue) {
                Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #? ({con.ManagementEndPoint}) disconnected?");
                return;
            }
            Logger.Log(LogLevel.Verbose, "ghostnet-s", $"Client #{id} ({con.ManagementEndPoint}) disconnected");

            Connections[(int) id] = null;

            ConnectionMap[con.ManagementEndPoint] = null;

            if (con.UpdateEndPoint != null) {
                ConnectionMap[con.UpdateEndPoint] = null;
            } else {
                UpdateConnectionQueue[con.ManagementEndPoint.Address] = null;
            }

            GhostNetFrame player;
            if (PlayerMap.TryGetValue(id, out player) && !string.IsNullOrWhiteSpace(player.MPlayer.Name) &&
                !string.IsNullOrWhiteSpace(GhostNetModule.Settings.ServerMessageLeave)) {
                BroadcastMChat(null, player, GhostNetModule.Settings.ServerMessageLeave, fillVars: true);
            }

            // Propagate disconnect to all other players.
            GhostNetFrame frame = new GhostNetFrame {
                HHead = {
                    IsValid = true,
                    PlayerID = id
                },

                MPlayer = {
                    IsValid = true,
                    Name = "",
                    SID = "",
                    Mode = AreaMode.Normal,
                    Level = ""
                }
            };
            PlayerMap[id] = frame;
            PropagateM(con, ref frame);
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

            Celeste.Instance.Components.Remove(this);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Stop();
        }

    }
}
