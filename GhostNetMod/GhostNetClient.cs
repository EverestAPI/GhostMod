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

        public GhostNetConnection Connection;

        public int UpdateIndex = -1;

        public Player Player;
        public Session Session;
        public GhostRecorder GhostRecorder;

        public uint PlayerID;
        public GhostChunkNetMServerInfo ServerInfo;

        public List<Ghost> Ghosts = new List<Ghost>();
        public Dictionary<uint, Ghost> GhostMap = new Dictionary<uint, Ghost>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();

        public GhostNetClient(Game game)
            : base(game) {
        }

        public override void Update(GameTime gameTime) {
            SendUUpdate();

            bool inputDisabled = MInput.Disabled;
            MInput.Disabled = false;

            // TODO: Replace testing icon with "icon wheel" (right stick).
            // TODO: Show player list on MenuJournal.Check
            if (!(Player?.Scene?.Paused ?? true) && Input.MenuJournal.Pressed)
                SendMIcon("collectables/heartgem/0/spin00");

            MInput.Disabled = inputDisabled;

            // Update ghosts even if the game is paused.
            for (int i = 0; i < Ghosts.Count; i++) {
                Ghost ghost = Ghosts[i];
                if (ghost == null)
                    continue;
                ghost.Update();
                if ((ghost.Scene as Level)?.FrozenOrPaused ?? true)
                    ghost.Hair?.AfterUpdate();
            }

            base.Update(gameTime);
        }

        #region Frame Senders

        public void SendMPlayer() {
            if (Connection == null)
                return;
            Connection.SendManagement(new GhostNetFrame {
                MPlayer = new GhostChunkNetMPlayer {
                    IsValid = true,
                    Name = GhostModule.Settings.Name,
                    SID = Session?.Area.GetSID() ?? "",
                    Level = Session?.Level ?? ""
                }
            });
            UpdateIndex = 0;
        }

        public void SendMIcon(string icon) {
            Connection?.SendManagement(new GhostNetFrame {
                MIcon = new GhostChunkNetMIcon {
                    IsValid = true,
                    Icon = icon
                }
            });
        }

        public void SendUUpdate() {
            if (Connection == null || GhostRecorder == null || UpdateIndex == -1)
                return;

            if ((UpdateIndex % (GhostNetModule.Settings.SendSkip + 1)) != 0)
                return;

            GhostNetFrame frame = new GhostNetFrame {
                UUpdate = new GhostChunkNetUUpdate {
                    IsValid = true,
                    UpdateIndex = (uint) UpdateIndex,
                    Data = GhostRecorder.LastFrameData.Data
                }
            };
            if (GhostNetModule.Settings.SendUFramesInMStream) {
                Connection.SendManagement(frame);
            } else {
                Connection.SendUpdate(frame);
            }

            UpdateIndex++;
        }

        #endregion

        #region Frame Parsers

        public virtual void Parse(GhostNetConnection con, ref GhostNetFrame frame) {
            if (!frame.HHead.IsValid)
                return;

            if (frame.MServerInfo.IsValid) {
                // The client can receive this more than once.
                PlayerID = frame.HHead.PlayerID;
                ServerInfo = frame.MServerInfo;
            }

            if (frame.MPlayer.IsValid)
                ParseMPlayer(con, ref frame);

            if (frame.MIcon.IsValid)
                ParseMIcon(con, ref frame);

            if (frame.UUpdate.IsValid)
                ParseUUpdate(con, ref frame);
        }

        public virtual void ParseMPlayer(GhostNetConnection con, ref GhostNetFrame frame) {
            if (Player?.Scene == null)
                return;

            // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} {frame.MPlayer.Name} in {frame.MPlayer.SID} {frame.MPlayer.Level}");

            if (frame.HHead.PlayerID == PlayerID) {
                // TODO: Server told us to move.
                return;
            }

            Ghost ghost;

            if (frame.MPlayer.SID != Session.Area.GetSID() ||
                frame.MPlayer.Level != Session.Level) {
                // Ghost not in the same room.
                // Find the ghost and remove it if it exists.
                if (GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) && ghost != null) {
                    ghost.RemoveSelf();
                    GhostMap[frame.HHead.PlayerID] = null;
                    int index = Ghosts.IndexOf(ghost);
                    if (index != -1)
                        Ghosts[index] = null;
                }
                return;
            }

            if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null) {
                // No ghost for the player existing.
                // Create a new ghost for the player.
                Player.Scene.Add(ghost = new Ghost(Player));
                GhostMap[frame.HHead.PlayerID] = ghost;
                Ghosts.Add(ghost);
            }

            GhostIndices[frame.HHead.PlayerID] = 0;

            if (ghost != null && ghost.Name != null)
                ghost.Name.Name = frame.MPlayer.Name;
        }

        public virtual void ParseMIcon(GhostNetConnection con, ref GhostNetFrame frame) {
            if (Player?.Scene == null)
                return;

            Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} sent icon: {frame.MIcon.Icon}");

            Ghost ghost = null;
            if (frame.HHead.PlayerID != PlayerID) {
                // We received an icon from somebody else.
                if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null) {
                    // No ghost for the player existing.
                    return;
                }
            }

            if (!GFX.Gui.Has(frame.MIcon.Icon)) {
                // We don't have the icon - ignore it.
                return;
            }
            Player.Scene.Add(new GhostNetIcon(ghost ?? (Entity) Player, GFX.Gui[frame.MIcon.Icon]) {
                Popup = true
            });
        }

        public virtual void ParseUUpdate(GhostNetConnection con, ref GhostNetFrame frame) {
            if (Player?.Scene == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // TODO: Server told us to move.
                return;
            }

            Ghost ghost;
            if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null)
                return;

            uint lastIndex;
            if (GhostIndices.TryGetValue(frame.HHead.PlayerID, out lastIndex) && frame.UUpdate.UpdateIndex < lastIndex) {
                // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Out of order update from #{frame.H0.PlayerID} - got {frame.U0.UpdateIndex}, newest is {lastIndex]}");
                return;
            }
            GhostIndices[frame.HHead.PlayerID] = frame.UUpdate.UpdateIndex;

            // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Received nU0 from #{frame.PlayerID} ({remote}), HasData: {frame.Frame.HasData}");

            ghost.ForcedFrame = new GhostFrame {
                Data = frame.UUpdate.Data
            };
        }

        #endregion

        #region Connection Handlers

        protected virtual void OnReceiveManagement(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            Parse(con, ref frame);
        }

        protected virtual void OnReceiveUpdate(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            Parse(con, ref frame);
        }

        protected virtual void OnDisconnect(GhostNetConnection con) {
            Logger.Log(LogLevel.Info, "ghostnet-c", "Client disconnected");

            Connection = null;

            if (GhostNetModule.Settings.EnabledEntry != null) {
                GhostNetModule.Settings.EnabledEntry.LeftPressed();
            }

            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            Everest.Events.Level.OnExit -= OnExit;

            OnExit(null, null, LevelExit.Mode.SaveAndQuit, null, null);
        }

        #endregion

        #region Celeste Events

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            Session = level.Session;

            string target = Session.Level;
            if (Connection != null)
                Logger.Log(LogLevel.Info, "ghost-c", $"Stepping into {Session.Area.GetSID()} {target}");

            Player = level.Tracker.GetEntity<Player>();

            for (int i = 0; i < Ghosts.Count; i++)
                Ghosts[i]?.RemoveSelf();
            GhostMap.Clear();
            Ghosts.Clear();

            GhostRecorder?.RemoveSelf();
            level.Add(GhostRecorder = new GhostRecorder(Player));

            SendMPlayer();
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            Session = null;

            if (Connection != null)
                Logger.Log(LogLevel.Info, "ghost-c", $"Leaving level");

            Cleanup();

            SendMPlayer();
        }

        #endregion

        public void Start() {
            if (Connection != null) {
                Logger.Log(LogLevel.Warn, "ghostnet-c", "Client already running, restarting");
                Stop();
            }

            Logger.Log(LogLevel.Info, "ghostnet-c", "Starting client");

            if (GhostNetModule.Instance.Server != null) {
                // We're hosting - let's just set up pseudo connections.
                Connection = GhostNetModule.Instance.Server.LocalConnectionToServer;
                GhostNetModule.Instance.Server.Accept(new GhostNetLocalConnection {
                    OnReceiveManagement = OnReceiveManagement,
                    OnReceiveUpdate = OnReceiveUpdate,
                    OnDisconnect = OnDisconnect
                });
            
            } else {
                // Set up a remote connection.
                Connection = new GhostNetRemoteConnection(
                    GhostNetModule.Settings.Host,
                    GhostNetModule.Settings.Port
                ) {
                    OnReceiveManagement = OnReceiveManagement,
                    OnReceiveUpdate = OnReceiveUpdate,
                    OnDisconnect = OnDisconnect
                };
            }

            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExit;

            if (Engine.Scene is Level)
                OnLoadLevel((Level) Engine.Scene, Player.IntroTypes.Transition, true);
        }

        public void Stop() {
            Logger.Log(LogLevel.Info, "ghostnet-c", "Stopping client");

            Connection?.Dispose();
            Connection = null;
        }

        public void Cleanup() {
            Player = null;

            foreach (Ghost ghost in GhostMap.Values)
                ghost?.RemoveSelf();
            GhostMap.Clear();

            GhostRecorder?.RemoveSelf();
            GhostRecorder = null;
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
