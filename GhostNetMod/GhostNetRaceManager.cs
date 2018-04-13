using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
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
    public class GhostNetRaceManager {

        public GhostNetServer Server;

        public List<Race> Races = new List<Race>();
        protected Dictionary<uint, Race> PlayerRaceCache = new Dictionary<uint, Race>();

        public static Color ColorDefault = Color.AliceBlue;
        public static Color ColorBroadcast = Color.BlueViolet;

        public GhostNetRaceManager(GhostNetServer server) {
            Server = server;

            server.OnHandle += Handle;
            server.OnDisconnect += Disconnect;

            server.Commands.Add(CommandRace = new GhostNetDCommand {
                Name = "race",
                Args = "+ [<sid> <side>] | - <index> | start | join <id> | leave | list | areas [raceid] | players [raceid]",
                Help =
@"Create, join, start and leave races.
Use + and - to add and remove areas from a race.
If using + outside a race, you're creating a new one.
You can chat with your fellow racers using the rc command.",
                OnRun = RunCommandRace
            });

            server.Commands.Add(CommandRaceChat = new GhostNetDCommand {
                Name = "rc",
                Args = "<message>",
                Help =
@"Chat with your fellow racers!",
                OnParse = GhostNetDCommand.Parsers.Everything,
                OnRun = RunCommandRaceChat
            });
        }

        public static void OnCreateServer(GhostNetServer server) {
            server.ModData["raceman"] = new GhostNetRaceManager(server);
        }

        public void Handle(GhostNetConnection con, GhostNetFrame frame) {
            lock (Races) {
                for (int i = 0; i < Races.Count; i++)
                    Races[i]?.Handle(con, frame);
            }
        }

        public void Disconnect(uint playerID, ChunkMPlayer player) {
            Race race = GetRace(playerID);
            if (race != null) {
                race.RemovePlayer(playerID, $"Player #{playerID} disconnected.");
            }
        }

        public readonly GhostNetCommand CommandRace;
        protected virtual void RunCommandRace(GhostNetCommand cmd, GhostNetCommandEnv env, GhostNetCommandArg[] args) {
            if (args.Length == 0) {
                GhostNetCommandsStandard.Help.Run(env, new GhostNetCommandArg(env).Parse(cmd.Name, 0));
                return;
            }

            Race race;

            switch (args[0]) {
                case "add":
                case "+":
                    race = GetRace(env.PlayerID);
                    if (race != null && race.Players[0] != env.PlayerID)
                        throw new Exception("You don't own the race!");
                    if (race == null) {
                        race = GetOrCreateRace(env.PlayerID);
                        env.Send($"New race created: #{race.ID + 1}");
                    }

                    if (race.HasStarted)
                        throw new Exception("The race has already started!");

                    if (args.Length == 1) {
                        // Add the area the player currently is in.
                        if (string.IsNullOrEmpty(env.MPlayer.SID))
                            throw new Exception("You can't add the menu to the race!");
                        lock (race.Areas) {
                            race.Areas.Add(Tuple.Create(env.MPlayer.SID, env.MPlayer.Mode));
                        }

                    } else if (args.Length < 3) {
                        throw new Exception("Not enough arguments!");
                    } else if (args.Length > 3) {
                        throw new Exception("Too many arguments!");
                    } else {
                        int mode = -1;
                        if (args[2].Type == GhostNetCommandArg.EType.Int)
                            mode = args[2].Int - 1;
                        else if (args[2].String.Length == 1)
                            mode = args[2].String.ToLowerInvariant()[0] - 'a';
                        if (mode < 0 || 2 < mode)
                            throw new Exception("Mode must be one of the following: a 1 b 2 c 3");

                        string area = args[1].String;
                        if (args[1].Type == GhostNetCommandArg.EType.Int) {
                            ChunkRListAreas areas;
                            env.Server.Request(env.Connection, out areas);
                            if (areas == null)
                                throw new Exception("Your client didn't respond to the area list request!");
                            if (args[1].Int < 1 || areas.Entries.Length < args[1].Int)
                                throw new Exception("Not a valid ID!");
                            area = areas.Entries[args[1].Int - 1];
                        }

                        lock (race.Areas) {
                            race.Areas.Add(Tuple.Create(area, (AreaMode) mode));
                        }
                    }

                    env.Send(race.AreaList);
                    return;

                case "remove":
                case "-":
                    race = GetRace(env.PlayerID);
                    if (race == null)
                        throw new Exception($"You're not in a race!");
                    if (race.Players[0] != env.PlayerID)
                        throw new Exception("You don't own the race!");
                    if (race.HasStarted)
                        throw new Exception("The race has already started!");

                    if (args.Length < 2)
                        throw new Exception("Not enough arguments!");
                    if (args.Length > 2)
                        throw new Exception("Too many arguments!");

                    lock (race.Areas) {
                        if (args[1].Type != GhostNetCommandArg.EType.Int ||
                            args[1].Int < 1 || race.Areas.Count < args[1].Int)
                            throw new Exception("Not a valid ID!");

                        race.Areas.RemoveAt(args[1].Int - 1);
                    }

                    env.Send(race.AreaList);
                    return;

                case "start":
                    if (args.Length > 1)
                        throw new Exception("Too many arguments!");

                    race = GetRace(env.PlayerID);
                    if (race == null)
                        throw new Exception($"You're not in a race!");
                    if (race.Players[0] != env.PlayerID)
                        throw new Exception("You don't own the race!");

                    race.Start();
                    return;

                case "join":
                    race = GetRace(env.PlayerID);
                    if (race != null)
                        throw new Exception($"You're already in race #{race.ID}!");

                    if (args.Length < 2)
                        throw new Exception("Not enough arguments!");
                    if (args.Length > 2)
                        throw new Exception("Too many arguments!");

                    if (args[1].Type != GhostNetCommandArg.EType.Int ||
                        args[1].Int < 1 || Races.Count < args[1].Int)
                        throw new Exception("Not a valid ID!");
                    race = Races[args[1].Int - 1];
                    if (race == null)
                        throw new Exception("The race has already ended!");

                    if (race.HasStarted)
                        throw new Exception("You're too late, the race has already started without you!");

                    lock (race.Players) {
                        race.Players.Add(env.PlayerID);
                    }
                    race.Send(null, $"{env.MPlayer.Name}#{env.HHead.PlayerID} joined.");
                    return;

                case "leave":
                    if (args.Length > 1)
                        throw new Exception("Too many arguments!");

                    race = GetRace(env.PlayerID);
                    if (race == null)
                        throw new Exception($"You're not in a race!");

                    race.RemovePlayer(env.PlayerID, $"{env.MPlayer.Name}#{env.HHead.PlayerID} left.");
                    return;

                case "list":
                    if (args.Length > 1)
                        throw new Exception("Too many arguments!");

                    race = GetRace(env.PlayerID);
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    lock (Races) {
                        for (int i = 0; i < Races.Count; i++) {
                            Race raceListed = Races[i];
                            if (raceListed == null)
                                continue;
                            ChunkMPlayer owner;
                            string ownerName = null;
                            if (env.Server.PlayerMap.TryGetValue(raceListed.Players[0], out owner) && owner != null)
                                ownerName = owner.Name;
                            builder
                                .Append(race == raceListed ? '>' : raceListed.HasStarted ? 'X' : '#')
                                .Append(raceListed.ID + 1)
                                .Append(" by ")
                                .Append(string.IsNullOrEmpty(ownerName) ? "???" : ownerName)
                                .AppendLine();
                            count++;
                        }
                    }
                    builder.Append(count).Append(" race");
                    if (count != 1)
                        builder.Append('s');
                    env.Send(builder.ToString().Trim());
                    return;

                case "areas":
                case "players":
                    if (args.Length == 1) {
                        race = GetRace(env.PlayerID);
                        if (race == null)
                            throw new Exception($"You're not in a race!");
                    } else if (args.Length > 2) {
                        throw new Exception("Too many arguments!");
                    } else if (args[1].Type != GhostNetCommandArg.EType.Int ||
                               args[1].Int <= 0 || Races.Count < args[1].Int) {
                        throw new Exception("Not a valid ID!");
                    } else {
                        race = Races[args[1].Int - 1];
                        if (race == null)
                            throw new Exception("Race already ended!");
                    }

                    switch (args[0]) {
                        case "areas":
                            env.Send(race.AreaList);
                            return;

                        case "players":
                            env.Send(race.PlayerList);
                            return;

                        default:
                            throw new Exception($"Can't list {args[0]}!");
                    }

                default:
                    throw new Exception($"Unknown subcommand {args[0]}!");
            }
        }

        public readonly GhostNetCommand CommandRaceChat;
        protected virtual void RunCommandRaceChat(GhostNetCommand cmd, GhostNetCommandEnv env, GhostNetCommandArg[] args) {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return;

            Race race = GetRace(env.PlayerID);
            if (race == null)
                throw new Exception($"You're not in a race!");

            race.Send(env.Frame, args[0], id: env.MChat.ID);
        }

        public Race GetRace(uint playerID) {
            Race race;
            if (PlayerRaceCache.TryGetValue(playerID, out race) && race != null && race.Players.Contains(playerID)) {
                return race;
            }

            lock (Races) {
                for (int i = 0; i < Races.Count; i++) {
                    Race other = Races[i];
                    if (other == null)
                        continue;
                    lock (other.Players) {
                        if (other.Players.Contains(playerID)) {
                            return PlayerRaceCache[playerID] = other;
                        }
                    }
                }
            }

            return PlayerRaceCache[playerID] = null;
        }

        public Race GetOrCreateRace(uint playerID) {
            Race race = GetRace(playerID);
            if (race != null)
                return race;
            lock (Races) {
                race = new Race {
                    Manager = this,
                    ID = Races.Count,
                    Players = new List<uint>() {
                        playerID
                    }
                };
                Races.Add(race);
            }
            return PlayerRaceCache[playerID] = race;
        }

        public class Race {

            public GhostNetRaceManager Manager;

            public int ID;

            public List<uint> Players = new List<uint>();
            public List<uint> PlayersFinished = new List<uint>();
            public List<Tuple<string, AreaMode>> Areas = new List<Tuple<string, AreaMode>>();
            public Dictionary<uint, int> Indices = new Dictionary<uint, int>();
            public Dictionary<uint, TimeSpan> Times = new Dictionary<uint, TimeSpan>();

            public Stopwatch Time = new Stopwatch();

            private HashSet<uint> WaitingForStart = new HashSet<uint>();

            public bool HasStarted;

            public string PlayerList {
                get {
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    int countFinished = 0;
                    lock (Players) {
                        lock (PlayersFinished) {

                            if (PlayersFinished.Count > 0) {
                                builder.AppendLine("Finished:");
                                for (int i = 0; i < PlayersFinished.Count; i++) {
                                    uint playerID = PlayersFinished[i];
                                    ChunkMPlayer player;
                                    if (!Manager.Server.PlayerMap.TryGetValue(playerID, out player) || player == null)
                                        player = null;
                                    builder.Append("#").Append(i).Append(": ").Append(player?.Name ?? "???").Append('#').Append(playerID);

                                    TimeSpan time;
                                    if (Times.TryGetValue(playerID, out time))
                                        builder.Append(" - ").Append(TimeToString(time));

                                    builder.AppendLine();
                                    if (player != null)
                                        countFinished++;
                                }
                            }

                            if (Players.Count - PlayersFinished.Count > 0) {
                                builder.AppendLine("Racing:");
                                foreach (uint playerID in Players) {
                                    if (PlayersFinished.Contains(playerID))
                                        continue;
                                    ChunkMPlayer player;
                                    if (!Manager.Server.PlayerMap.TryGetValue(playerID, out player) || player == null)
                                        continue;
                                    builder.Append(count + 1).Append(": ").Append(player.Name).Append('#').Append(playerID);

                                    int index;
                                    if (Indices.TryGetValue(playerID, out index))
                                        builder.Append(" - ").Append(index).Append("/").Append(Areas.Count);
                                    
                                    builder.AppendLine();
                                    count++;
                                }
                            }

                        }
                    }
                    builder.Append(count + countFinished).Append(" racer");
                    if (count != 1)
                        builder.Append('s');
                    if (countFinished != 0) {
                        builder.Append(", ").Append(countFinished).Append(" finished, ").Append(count).Append(" remaining");
                    }
                    return builder.ToString().Trim();
                }
            }

            public string AreaList {
                get {
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    lock (Areas) {
                        foreach (Tuple<string, AreaMode> area in Areas) {
                            builder.Append(count + 1).Append(": ").Append(area.Item1).Append(' ').Append((char) ('A' + area.Item2)).AppendLine();
                            count++;
                        }
                    }
                    builder.Append(count).Append(" area");
                    if (count != 1)
                        builder.Append('s');
                    return builder.ToString().Trim();
                }
            }

            public static string TimeToString(TimeSpan time)
                => $"{((int) Math.Floor(time.TotalMinutes)).ToString("00")}:{time.Seconds.ToString("00")}:{time.Milliseconds.ToString("000")}";

            public void RemovePlayer(uint playerID, string message) {
                Send(null, message);
                lock (Players) {
                    Players.Remove(playerID);
                    if (Players.Count == 0) {
                        Manager.Races[ID] = null;
                        return;
                    }
                }
            }

            public void Start() {
                if (Areas.Count == 0)
                    throw new Exception("Can't start a race with no areas!");
                if (HasStarted)
                    throw new Exception("The race has already started!");

                HasStarted = true;

                Send(null,
@"The race will start soon!
You will be sent to the menu. Please wait there.
The server will teleport you when the race starts."
                );

                Thread.Sleep(5000);

                lock (Players) {
                    foreach (uint playerID in Players) {
                        WaitingForStart.Add(playerID);
                        Move(playerID, -1);
                    }
                }

                while (WaitingForStart.Count > 0)
                    Thread.Sleep(0);

                Send(null, "Starting the race in 3...");
                Thread.Sleep(1000);
                Send(null, "2...");
                Thread.Sleep(1000);
                Send(null, "1...");
                Thread.Sleep(1000);

                Time.Start();

                lock (Players) {
                    // Note: Even though we're locked, Players can change if the local client (running in same thread!) doesn't have the map.
                    foreach (uint playerID in new List<uint>(Players)) {
                        Progress(playerID);
                    }
                }
                Send(null, "GO!");
            }

            public void Progress(uint playerID, GhostNetFrame frame = null) {
                int index;
                if (!Indices.TryGetValue(playerID, out index))
                    return;
                index++;
                if (index >= Areas.Count) {
                    Finish(playerID);
                } else {
                    Move(playerID, index, frame);
                }
            }

            public void Finish(uint playerID) {
                TimeSpan time = Time.Elapsed;
                ChunkMPlayer player;
                if (!Manager.Server.PlayerMap.TryGetValue(playerID, out player) || player == null)
                    return;
                lock (PlayersFinished) {
                    PlayersFinished.Add(playerID);
                }
                Indices[playerID] = Areas.Count;
                Times[playerID] = time;
                Send(null, $"#{PlayersFinished.Count}: {player.Name}#{playerID} - {TimeToString(time)}");
                if (PlayersFinished.Count == Players.Count)
                    Time.Stop();
            }

            public void Move(uint playerID, int index, GhostNetFrame frame = null) {
                Logger.Log(LogLevel.Verbose, "ghostnet-race", $"Moving player {playerID} to index {index}");
                GhostNetConnection con = Manager.Server.Connections[(int) playerID];
                ChunkMPlayer playerCached;
                if (con == null || !Manager.Server.PlayerMap.TryGetValue(playerID, out playerCached) || playerCached == null)
                    return;
                Indices[playerID] = index;
                if (index == -1 && WaitingForStart.Contains(playerID) && string.IsNullOrEmpty(playerCached.SID)) {
                    WaitingForStart.Remove(playerID);
                    return;
                }

                ChunkMPlayer player = new ChunkMPlayer {
                    Name = playerCached.Name,
                    SID = index == -1 ? "" : Areas[index].Item1,
                    Mode = index == -1 ? AreaMode.Normal : Areas[index].Item2,
                    Level = ""
                };

                if (frame == null) {
                    con.SendManagement(new GhostNetFrame {
                        HHead = new ChunkHHead {
                            PlayerID = playerID
                        },

                        MPlayer = player
                    }, true);

                } else {
                    frame.MPlayer = player;
                    frame.PropagateM = true;
                }
            }

            public ChunkMChat Send(GhostNetFrame frame, string text, string tag = "race", Color? color = null, bool fillVars = false, uint? id = null) {
                ChunkMChat msg = Manager.Server.CreateMChat(frame, text, tag, color ?? (frame != null ? ColorDefault : ColorBroadcast), fillVars, id);
                GhostNetFrame frameMsg = new GhostNetFrame {
                    frame?.HHead ?? new ChunkHHead {
                        PlayerID = uint.MaxValue
                    },
                    msg
                };
                lock (Players) {
                    foreach (uint playerID in Players) {
                        GhostNetConnection con = Manager.Server.Connections[(int) playerID];
                        if (con == null)
                            continue;
                        con.SendManagement(frameMsg, false);
                    }
                }
                return msg;
            }

            public void Handle(GhostNetConnection con, GhostNetFrame frame) {
                if (!HasStarted || Areas.Count == 0 || !Players.Contains(frame.HHead.PlayerID))
                    return;

                if (frame.MPlayer == null || frame.MPlayer.IsCached)
                    return;

                if (WaitingForStart.Contains(frame.HHead.PlayerID)) {
                    WaitingForStart.Remove(frame.HHead.PlayerID);
                    if (string.IsNullOrEmpty(frame.MPlayer.SID)) {
                        // Player has been moved to menu to wait for the race to start.
                        Logger.Log(LogLevel.Verbose, "ghostnet-race", $"Player {frame.HHead.PlayerID} waiting for start");
                    } else {
                        RemovePlayer(frame.HHead.PlayerID, $"{frame.MPlayer.Name}#{frame.HHead.PlayerID} not sent to menu properly!");
                    }

                } else if (!PlayersFinished.Contains(frame.HHead.PlayerID)) {
                    // Player still racing.
                    int index;
                    if (!Indices.TryGetValue(frame.HHead.PlayerID, out index))
                        return; // Index-less player? How did we even land here?
                    Tuple<string, AreaMode> area = index < 0 ? Tuple.Create("", AreaMode.Normal) : Areas[index];
                    if (!string.IsNullOrEmpty(frame.MPlayer.SID) &&
                        frame.MPlayer.SID != area.Item1 ||
                        frame.MPlayer.Mode != area.Item2) {
                        // Player isn't in the level they should be in.
                        RemovePlayer(frame.HHead.PlayerID, $"{frame.MPlayer.Name}#{frame.HHead.PlayerID} went somewhere else.");
                        return;
                    }

                    if (frame.MPlayer.LevelExit == null && !frame.MPlayer.LevelCompleted) {
                        // Player has entered another level.

                    } else if (frame.MPlayer.LevelExit == LevelExit.Mode.GiveUp || frame.MPlayer.LevelExit == LevelExit.Mode.SaveAndQuit) {
                        // Player has quit the level without completion.
                        RemovePlayer(frame.HHead.PlayerID, $"{frame.MPlayer.Name}#{frame.HHead.PlayerID} dropped out.");

                    } else if (frame.MPlayer.LevelCompleted) {
                        // Player completed this level, move to next one.
                        Progress(frame.HHead.PlayerID, frame);
                    }
                }

            }

        }

    }
}
