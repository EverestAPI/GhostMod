using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    public class GhostNetClient : DrawableGameComponent {

        public GhostNetConnection Connection;

        protected float time;

        public int UpdateIndex = -1;

        public Player Player;
        public Session Session;
        public GhostRecorder GhostRecorder;
        public GhostName PlayerName;
        public GhostNetEmoteWheel EmoteWheel;

        public uint PlayerID;
        public GhostChunkNetMPlayer PlayerInfo;
        public GhostChunkNetMServerInfo ServerInfo;

        public Dictionary<uint, GhostChunkNetMPlayer> PlayerMap = new Dictionary<uint, GhostChunkNetMPlayer>();

        public List<Ghost> Ghosts = new List<Ghost>();
        public Dictionary<uint, Ghost> GhostMap = new Dictionary<uint, Ghost>();
        public Dictionary<uint, uint> GhostIndices = new Dictionary<uint, uint>();

        public List<ChatLine> ChatLog = new List<ChatLine>();
        public string ChatInput = "";

        protected bool _ChatVisible;
        protected bool _ChatWasPaused;
        protected Overlay _ChatLevelOverlay;
        protected int _ChatConsumeInput = 0;
        public bool ChatVisible {
            get {
                return _ChatVisible;
            }
            set {
                if (_ChatVisible == value)
                    return;

                if (value) {
                    _ChatWasPaused = Engine.Scene.Paused;
                    Engine.Scene.Paused = true;
                    // If we're in a level, add a dummy overlay to prevent the pause menu from handling input.
                    if (Engine.Scene is Level)
                        ((Level) Engine.Scene).Overlay = _ChatLevelOverlay = new Overlay();

                } else {
                    ChatInput = "";
                    Engine.Scene.Paused = _ChatWasPaused;
                    _ChatConsumeInput = 2;
                    if (_ChatLevelOverlay != null && (Engine.Scene as Level)?.Overlay == _ChatLevelOverlay)
                        ((Level) Engine.Scene).Overlay = null;
                }

                _ChatVisible = value;
            }
        }

        protected string PlayerListText;
        public bool PlayerListVisible;

        public static event Action<GhostNetClient> OnCreate;
        public event GhostNetFrameParser OnParse;
        // TODO: More events.

        public GhostNetClient(Game game)
            : base(game) {
            OnCreate?.Invoke(this);
        }

        public override void Update(GameTime gameTime) {
            SendUUpdate();

            time += Engine.DeltaTime;

            bool inputDisabled = MInput.Disabled;
            MInput.Disabled = false;

            if (!(Player?.Scene?.Paused ?? true)) {
                EmoteWheel.Shown = GhostNetModule.Instance.JoystickEmoteWheel.Value.LengthSquared() >= 0.36f;
                if (EmoteWheel.Shown && EmoteWheel.Selected != -1 && GhostNetModule.Instance.ButtonEmoteSend.Pressed) {
                    SendMEmote(EmoteWheel.Selected);
                    EmoteWheel.Selected = -1;
                }
            } else if (EmoteWheel != null) {
                EmoteWheel.Shown = false;
                EmoteWheel.Selected = -1;
            }

            if (!ChatVisible && GhostNetModule.Instance.ButtonChat.Pressed) {
                // Was hidden, but player pressed chat button.
                ChatVisible = true;

            } else if (ChatVisible && MInput.Keyboard.Pressed(Keys.Enter)) {
                // Was visible and player pressed enter to send.
                SendMChat(ChatInput);
                ChatVisible = false;

            } else if (ChatVisible && (Input.ESC.Pressed || Input.Pause.Pressed)) {
                // Was visible and player escaped.
                ChatVisible = false;
            }

            if (!ChatVisible) {
                if (MInput.Keyboard.Pressed(Keys.D1))
                    SendMEmote(0);
                else if (MInput.Keyboard.Pressed(Keys.D2))
                    SendMEmote(1);
                else if (MInput.Keyboard.Pressed(Keys.D3))
                    SendMEmote(2);
                else if (MInput.Keyboard.Pressed(Keys.D4))
                    SendMEmote(3);
                else if (MInput.Keyboard.Pressed(Keys.D5))
                    SendMEmote(4);
                else if (MInput.Keyboard.Pressed(Keys.D6))
                    SendMEmote(5);
                else if (MInput.Keyboard.Pressed(Keys.D7))
                    SendMEmote(6);
                else if (MInput.Keyboard.Pressed(Keys.D8))
                    SendMEmote(7);
                else if (MInput.Keyboard.Pressed(Keys.D9))
                    SendMEmote(8);
                else if (MInput.Keyboard.Pressed(Keys.D0))
                    SendMEmote(9);
            }

            // Prevent the menus from reacting to player input after exiting the chat.
            if (_ChatConsumeInput > 0) {
                Input.MenuConfirm.ConsumeBuffer();
                Input.MenuConfirm.ConsumePress();
                Input.ESC.ConsumeBuffer();
                Input.ESC.ConsumePress();
                Input.Pause.ConsumeBuffer();
                Input.Pause.ConsumePress();
                _ChatConsumeInput--;
            }

            if (!(Player?.Scene?.Paused ?? false) && GhostNetModule.Instance.ButtonPlayerList.Pressed)
                PlayerListVisible = !PlayerListVisible;

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

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);

            int viewWidth = Engine.ViewWidth;
            int viewHeight = Engine.ViewHeight;

            Monocle.Draw.SpriteBatch.Begin();

            if (ChatVisible) {
                Monocle.Draw.Rect(10f, viewHeight - 50f, viewWidth - 20f, 40f, Color.Black * 0.8f);

                if (!GhostNetModuleBackCompat.HasTextInputEvent) {
                    Monocle.Draw.SpriteBatch.DrawString(
                        Monocle.Draw.DefaultFont,
                        "TextInput not found - update Everest to 0.0.305 or newer!",
                        new Vector2(20f, viewHeight - 42f),
                        Color.Red
                    );

                } else {
                    string text = ">" + ChatInput;
                    if (Calc.BetweenInterval(time, 0.5f))
                        text += "_";
                    Monocle.Draw.SpriteBatch.DrawString(
                        Monocle.Draw.DefaultFont,
                        text,
                        new Vector2(20f, viewHeight - 42f),
                        Color.White
                    );
                }
            }

            lock (ChatLog) {
                if (ChatLog.Count > 0) {
                    DateTime now = DateTime.UtcNow;
                    float y = viewHeight - 20f;
                    if (ChatVisible)
                        y -= 40f + 2f;
                    for (int i = 0; i < ChatLog.Count && i < GhostNetModule.Settings.ChatLogLength; i++) {
                        ChatLine line = ChatLog[i];

                        float alpha = 1f;

                        float delta = (float) (now - line.Date).TotalSeconds;
                        if (!ChatVisible && delta > 3f) {
                            alpha = 1f - Ease.CubeIn(delta - 3f);
                        }

                        if (alpha <= 0f)
                            continue;

                        string text = Escape(
                            $"[{line.Date.ToLocalTime().ToLongTimeString()}] {line.PlayerName}{(line.PlayerID == uint.MaxValue ? "" : $"#{line.PlayerID}")}:{(line.Text.Contains('\n') ? "\n" : " ")}{line.Text}",
                            Monocle.Draw.DefaultFont
                        );
                        Vector2 size = Monocle.Draw.DefaultFont.MeasureString(text);
                        float height = 20f + size.Y;

                        y -= height;

                        Monocle.Draw.Rect(10f, y, size.X + 20f, height, Color.Black * 0.8f * alpha);
                        Monocle.Draw.SpriteBatch.DrawString(
                            Monocle.Draw.DefaultFont,
                            text,
                            new Vector2(20f, y + 10f),
                            line.Color * alpha * (line.Unconfirmed ? 0.6f : 1f)
                        );
                    }
                }
            }

            if (PlayerListVisible) {
                float y = 0f;
                if ((Settings.Instance?.SpeedrunClock ?? SpeedrunType.Off) != SpeedrunType.Off) {
                    y += 192f * (viewHeight / 1920f);
                }
                Vector2 size = Monocle.Draw.DefaultFont.MeasureString(PlayerListText);
                Monocle.Draw.Rect(10f, 10f + y, size.X + 20f, size.Y + 20f, Color.Black * 0.8f);
                Monocle.Draw.SpriteBatch.DrawString(
                    Monocle.Draw.DefaultFont,
                    PlayerListText,
                    new Vector2(20f, 20f + y),
                    Color.White
                );
            }

            Monocle.Draw.SpriteBatch.End();
        }

        protected virtual void RebuildPlayerList() {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<uint, GhostChunkNetMPlayer> player in PlayerMap) {
                if (string.IsNullOrWhiteSpace(player.Value.Name))
                    continue;
                builder
                    .Append(Escape(player.Value.Name, Monocle.Draw.DefaultFont))
                    .Append("#")
                    .Append(player.Key)
                ;
                if (!string.IsNullOrWhiteSpace(player.Value.SID)) {
                    builder
                        .Append(" @ ")
                        .Append(Escape(AreaDataExt.Get(player.Value.SID)?.Name?.DialogCleanOrNull(Dialog.Languages["english"]) ?? player.Value.SID, Monocle.Draw.DefaultFont))
                        .Append(" ")
                        .Append((char) ('A' + (int) player.Value.Mode))
                        .Append(" ")
                        .Append(Escape(player.Value.Level, Monocle.Draw.DefaultFont))
                    ;
                }
                builder.AppendLine();
            }
            PlayerListText = builder.ToString().Trim();
        }

        public static string Escape(string text, SpriteFont font) {
            StringBuilder escaped = new StringBuilder();
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (!font.Characters.Contains(c) && c != '\n')
                    c = ' ';
                escaped.Append(c);
            }
            return escaped.ToString();
        }

        #region Frame Senders

        public void SendMPlayer() {
            if (Connection == null)
                return;
            Connection.SendManagement(new GhostNetFrame {
                MPlayer = {
                    IsValid = true,
                    Name = GhostModule.Settings.Name,
                    SID = Session?.Area.GetSID() ?? "",
                    Mode = Session?.Area.Mode ?? AreaMode.Normal,
                    Level = Session?.Level ?? ""
                }
            });
        }

        public void SendMSession() {
            if (Connection == null)
                return;
            if (Session == null) {
                Connection.SendManagement(new GhostNetFrame {
                    MSession = {
                        IsValid = true,
                        InSession = false
                    }
                });
                return;
            }
            Connection.SendManagement(new GhostNetFrame {
                MSession = {
                    IsValid = true,
                    InSession = true,

                    RespawnPoint = Session.RespawnPoint,
                    Inventory = Session.Inventory,
                    Flags = Session.Flags,
                    LevelFlags = Session.LevelFlags,
                    Strawberries = Session.Strawberries,
                    DoNotLoad = Session.DoNotLoad,
                    Keys = Session.Keys,
                    Counters = Session.Counters,
                    FurthestSeenLevel = Session.FurthestSeenLevel,
                    StartCheckpoint = Session.StartCheckpoint,
                    ColorGrade = Session.ColorGrade,
                    SummitGems = Session.SummitGems,
                    FirstLevel = Session.FirstLevel,
                    Cassette = Session.Cassette,
                    HeartGem = Session.HeartGem,
                    Dreaming = Session.Dreaming,
                    GrabbedGolden = Session.GrabbedGolden,
                    HitCheckpoint = Session.HitCheckpoint,
                    LightingAlphaAdd = Session.LightingAlphaAdd,
                    BloomBaseAdd = Session.BloomBaseAdd,
                    DarkRoomAlpha = Session.DarkRoomAlpha,
                    CoreMode = Session.CoreMode
                }
            });
        }

        public void SendMEmote(int index) {
            string[] emotes = GhostNetModule.Settings.EmoteFavs;
            if (index < 0 || emotes.Length <= index)
                return;
            SendMEmote(emotes[index]);
        }

        public void SendMEmote(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return;
            Connection?.SendManagement(new GhostNetFrame {
                MEmote = {
                    Value = value.Trim()
                }
            });
        }

        public void SendMChat(string text) {
            text = text.TrimEnd();
            if (string.IsNullOrWhiteSpace(text))
                return;
            ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, PlayerInfo.Name, text));
            Connection?.SendManagement(new GhostNetFrame {
                MChat = {
                    Text = text
                }
            });
        }

        public void SendUUpdate() {
            if (Connection == null || GhostRecorder == null || UpdateIndex == -1)
                return;

            if ((UpdateIndex % (GhostNetModule.Settings.SendFrameSkip + 1)) != 0)
                return;

            GhostNetFrame frame = new GhostNetFrame {
                UUpdate = {
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

            GhostChunkNetMPlayer player;
            if (!PlayerMap.TryGetValue(frame.HHead.PlayerID, out player) || !player.IsValid) {
                // Ghost not managed, possibly the server.
            }
            // Temporarily attach the MPlayer chunk to make player identification easier.
            frame.MPlayer = player;

            if (frame.MRequest.IsValid)
                ParseMRequest(con, ref frame);

            if (frame.MEmote.IsValid)
                ParseMEmote(con, ref frame);

            if (frame.MChat.IsValid)
                ParseMChat(con, ref frame);

            if (frame.UUpdate.IsValid)
                ParseUUpdate(con, ref frame);

            OnParse?.Invoke(con, ref frame);
        }

        public virtual void ParseMPlayer(GhostNetConnection con, ref GhostNetFrame frame) {
            // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} {frame.MPlayer.Name} in {frame.MPlayer.SID} {frame.MPlayer.Level}");

            PlayerMap[frame.HHead.PlayerID] = frame.MPlayer;
            RebuildPlayerList();

            if (frame.HHead.PlayerID == PlayerID) {
                // Server told us to move... or just told us about our proper name.
                PlayerInfo = frame.MPlayer;
                if (PlayerName != null)
                    PlayerName.Name = frame.MPlayer.Name;

                if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                    frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal) ||
                    frame.MPlayer.Level != (Session?.Level ?? "")) {
                    // Server told us to move.

                    if (SaveData.Instance == null) {
                        return;
                    }

                    AreaData area = AreaDataExt.Get(frame.MPlayer.SID);
                    if (area != null) {
                        if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                            frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal)) {
                            // Different SID or mode - create new session.
                            Session = new Session(SaveData.Instance.LastArea = area.ToKey(frame.MPlayer.Mode), null, SaveData.Instance.Areas[area.ID]);
                            if (Session != null && frame.MSession.IsValid && frame.MSession.InSession) {
                                // We received additional session data from the server.
                                ParseMSession(con, ref frame);
                            }

                        }

                        if (!string.IsNullOrEmpty(frame.MPlayer.Level) && Session.MapData.Get(frame.MPlayer.Level) != null) {
                            Session.Level = frame.MPlayer.Level;
                            Session.FirstLevel = false;
                        }
                        Engine.Scene = new LevelLoader(Session, frame.UUpdate.IsValid ? frame.UUpdate.Data.Position : default(Vector2?));

                    } else {
                        string message = Dialog.Get("postcard_levelgone");
                        if (string.IsNullOrEmpty(frame.MPlayer.SID)) {
                            message = Dialog.Has("postcard_ghostnetmodule_backtomenu") ? Dialog.Get("postcard_ghostnetmodule_backtomenu") :
@"{big}Oops!{/big}{n}The server has sent you back to the main menu.";
                        }

                        message = message.Replace("((player))", SaveData.Instance.Name);
                        message = message.Replace("((sid))", frame.MPlayer.SID);

                        LevelEnterExt.ErrorMessage = message;
                        LevelEnter.Go(new Session(new AreaKey(1).SetSID("")), false);
                    }
                }

                return;
            }

            if (Player?.Scene == null)
                return;

            Ghost ghost;

            if (frame.MPlayer.SID != Session.Area.GetSID() ||
                frame.MPlayer.Mode != Session.Area.Mode) {
                // Ghost not in the same level.
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

        public virtual void ParseMRequest(GhostNetConnection con, ref GhostNetFrame frame) {
            // TODO: Event for request by server in client.
            switch (frame.MRequest.ID) {
                case GhostChunkNetMPlayer.Chunk:
                    SendMPlayer();
                    break;
                case GhostChunkNetMSession.Chunk:
                    SendMSession();
                    break;

                case GhostChunkNetUUpdate.Chunk:
                    SendUUpdate();
                    break;

                default:
                    break;
            }
        }

        public virtual void ParseMSession(GhostNetConnection con, ref GhostNetFrame frame) {
            if (Session == null)
                return;

            Session.RespawnPoint = frame.MSession.RespawnPoint;
            Session.Inventory = frame.MSession.Inventory;
            Session.Flags = frame.MSession.Flags;
            Session.LevelFlags = frame.MSession.LevelFlags;
            Session.Strawberries = frame.MSession.Strawberries;
            Session.DoNotLoad = frame.MSession.DoNotLoad;
            Session.Keys = frame.MSession.Keys;
            Session.Counters = frame.MSession.Counters;
            Session.FurthestSeenLevel = frame.MSession.FurthestSeenLevel;
            Session.StartCheckpoint = frame.MSession.StartCheckpoint;
            Session.ColorGrade = frame.MSession.ColorGrade;
            Session.SummitGems = frame.MSession.SummitGems;
            Session.FirstLevel = frame.MSession.FirstLevel;
            Session.Cassette = frame.MSession.Cassette;
            Session.HeartGem = frame.MSession.HeartGem;
            Session.Dreaming = frame.MSession.Dreaming;
            Session.GrabbedGolden = frame.MSession.GrabbedGolden;
            Session.HitCheckpoint = frame.MSession.HitCheckpoint;
            Session.LightingAlphaAdd = frame.MSession.LightingAlphaAdd;
            Session.BloomBaseAdd = frame.MSession.BloomBaseAdd;
            Session.DarkRoomAlpha = frame.MSession.DarkRoomAlpha;
            Session.CoreMode = frame.MSession.CoreMode;
        }

        public virtual void ParseMEmote(GhostNetConnection con, ref GhostNetFrame frame) {
            if (Player?.Scene == null)
                return;

            // Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} emote: {frame.MEmote.Value}");

            Ghost ghost = null;
            if (frame.HHead.PlayerID != PlayerID) {
                // We received an icon from somebody else.
                if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null) {
                    // No ghost for the player existing.
                    return;
                }
            }

            GhostNetEmote emote = new GhostNetEmote(ghost ?? (Entity) Player, frame.MEmote.Value) {
                Pop = true
            };
            Player.Scene.Add(emote);
        }

        public virtual void ParseMChat(GhostNetConnection con, ref GhostNetFrame frame) {
            // Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} chat: {frame.MChat.Text}");

            string playerName;
            if (frame.HHead.PlayerID == uint.MaxValue) {
                // We've received a message from the server.
                playerName = "**SERVER**";

            } else if (frame.MPlayer.IsValid) {
                // We've received a message from a living ghost.
                playerName = frame.MPlayer.Name;

            } else {
                // We've received a message from a dead ghost.
                return;
            }

            ChatLine line = new ChatLine(frame.MChat.ID, frame.HHead.PlayerID, playerName, frame.MChat.Text, frame.MChat.Color);

            // If there's already a chat line with the same message ID, replace it.
            // Also remove any "unconfirmed" messages.
            bool locked = false;
            try {
                for (int i = ChatLog.Count - 1; i > -1; --i) {
                    ChatLine existing = ChatLog[i];
                    if (existing.MessageID == line.MessageID) {
                        ChatLog[i] = line;
                        return;
                    }
                    if (existing.PlayerID == line.PlayerID && existing.Unconfirmed) {
                        if (!locked) {
                            // Enter the lock only if we remove items.
                            Monitor.Enter(ChatLog, ref locked);
                        }
                        ChatLog.RemoveAt(i);
                    }
                }
            } finally {
                if (locked)
                    Monitor.Exit(ChatLog);
            }

            ChatLog.Insert(0, line);
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
            GhostNetModuleBackCompat.OnTextInput -= OnTextInput;

            OnExit(null, null, LevelExit.Mode.SaveAndQuit, null, null);

            Celeste.Instance.Components.Remove(this);
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

            PlayerName?.RemoveSelf();
            level.Add(PlayerName = new GhostName(Player, PlayerInfo.Name));

            EmoteWheel?.RemoveSelf();
            level.Add(EmoteWheel = new GhostNetEmoteWheel(Player));

            SendMPlayer();
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            Session = null;

            if (Connection != null)
                Logger.Log(LogLevel.Info, "ghost-c", $"Leaving level");

            Cleanup();

            SendMPlayer();
        }

        public void OnTextInput(char c) {
            if (!ChatVisible)
                return;
            if (c == (char) 13) {
                // Enter - send.
                // Handled in Update.

            } else if (c == (char) 8) {
                // Backspace - trim.
                if (ChatInput.Length > 0)
                    ChatInput = ChatInput.Substring(0, ChatInput.Length - 1);

            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                ChatInput += c;
            }
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
            GhostNetModuleBackCompat.OnTextInput += OnTextInput;

            if (Engine.Scene is Level)
                OnLoadLevel((Level) Engine.Scene, Player.IntroTypes.Transition, true);
            else
                SendMPlayer();
        }

        public void Stop() {
            Logger.Log(LogLevel.Info, "ghostnet-c", "Stopping client");

            Celeste.Instance.Components.Remove(this);

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

            PlayerName?.RemoveSelf();
            PlayerName = null;

            EmoteWheel?.RemoveSelf();
            EmoteWheel = null;
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

        public struct ChatLine {

            public uint MessageID;
            public uint PlayerID;
            public string PlayerName;
            public string Text;
            public Color Color;
            public DateTime Date;
            public bool Unconfirmed => MessageID == uint.MaxValue;

            public ChatLine(uint messageID, uint playerID, string playerName, string text)
                : this(messageID, playerID, playerName, text, Color.White) {
            }
            public ChatLine(uint messageID, uint playerID, string playerName, string text, Color color) {
                MessageID = messageID;
                PlayerID = playerID;
                PlayerName = playerName;
                Text = text;
                Color = color;
                Date = DateTime.UtcNow;
            }

        }

    }
}
