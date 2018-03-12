using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
                Args = "add [<sid> <side>] | start | join <id> | leave | list | areas [raceid] | players [raceid]",
                Help =
@"Create, join, start and leave races.",
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
            foreach (Race race in Races)
                race?.Handle(con, frame);
        }

        public void Disconnect(uint playerID, GhostNetFrame player) {
            Race race = GetRace(playerID);
            if (race != null) {
                race.Players.Remove(playerID);
                if (race.Players.Count == 0)
                    Races[race.ID] = null;
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
                    race = GetRace(env.PlayerID);
                    if (race != null && race.Players[0] != env.PlayerID)
                        throw new Exception("You don't own the race!");
                    if (race == null) {
                        race = GetOrCreateRace(env.PlayerID);
                        env.Send($"New race created: #{race.ID}");
                    }

                    if (args.Length == 1) {
                        // Add the area the player currently is in.
                        if (string.IsNullOrEmpty(env.MPlayer.SID))
                            throw new Exception("You can't add the menu to the race!");
                        race.Areas.Add(new AreaKey(0, env.MPlayer.Mode).SetSID(env.MPlayer.SID));

                    } else if (args.Length < 3) {
                        throw new Exception("Not enough arguments!");
                    } else if (args.Length > 3) {
                        throw new Exception("Too many arguments!");
                    } else {
                        int mode = -1;
                        if (args[2].Type == GhostNetCommandArg.EType.Int)
                            mode = args[2].Int;
                        else if (args[2].String.Length == 1)
                            mode = args[2].String.ToLowerInvariant()[0] - 'a';
                        if (mode < 0 || 2 < mode)
                            throw new Exception("Mode must be one of the following: a 1 b 2 c 3");
                        race.Areas.Add(new AreaKey(0, (AreaMode) mode).SetSID(args[1]));
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
                        args[1].Int < 0 || Races.Count <= args[1].Int)
                        throw new Exception("Not a valid ID!");
                    race = Races[args[1].Int];
                    if (race == null)
                        throw new Exception("Race already ended!");

                    if (race.HasStarted)
                        throw new Exception("You're too late, the race has already started without you!");

                    race.Players.Add(env.PlayerID);
                    race.Send(null, $"{env.MPlayer.Name}#{env.HHead.PlayerID} has joined the race!");
                    return;

                case "leave":
                    if (args.Length > 1)
                        throw new Exception("Too many arguments!");

                    race = GetRace(env.PlayerID);
                    if (race == null)
                        throw new Exception($"You're not in a race!");

                    race.Players.Remove(env.PlayerID);
                    if (race.Players.Count == 0)
                        Races[race.ID] = null;

                    race.Send(null, $"{env.MPlayer.Name}#{env.HHead.PlayerID} has left the race!");
                    return;

                case "list":
                    if (args.Length > 1)
                        throw new Exception("Too many arguments!");

                    race = GetRace(env.PlayerID);
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    foreach (Race raceListed in Races) {
                        if (raceListed == null)
                            continue;
                        GhostNetFrame owner;
                        string ownerName = null;
                        if (env.Server.PlayerMap.TryGetValue(raceListed.Players[0], out owner) && owner != null)
                            ownerName = owner.MPlayer.Name;
                        builder
                            .Append(race == raceListed ? '>' : raceListed.HasStarted ? 'X' : '#')
                            .Append(raceListed.ID)
                            .Append(" by ")
                            .Append(string.IsNullOrEmpty(ownerName) ? "???" : ownerName)
                            .AppendLine();
                        count++;
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
                               args[1].Int < 0 || Races.Count <= args[1].Int) {
                        throw new Exception("Not a valid ID!");
                    } else {
                        race = Races[args[1].Int];
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
                foreach (Race other in Races) {
                    if (other != null && other.Players.Contains(playerID)) {
                        return PlayerRaceCache[playerID] = other;
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
            public List<AreaKey> Areas = new List<AreaKey>();

            public bool HasStarted;

            public string PlayerList {
                get {
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    foreach (uint playerID in Players) {
                        GhostNetFrame player;
                        if (!Manager.Server.PlayerMap.TryGetValue(playerID, out player) || player == null)
                            continue;
                        builder.Append(count + 1).Append(": ").Append(player.MPlayer.Name).Append('#').Append(playerID).AppendLine();
                        count++;
                    }
                    builder.Append(count).Append(" player");
                    if (count != 1)
                        builder.Append('s');
                    return builder.ToString().Trim();
                }
            }

            public string AreaList {
                get {
                    StringBuilder builder = new StringBuilder();
                    int count = 0;
                    foreach (AreaKey area in Areas) {
                        builder.Append(count + 1).Append(": ").Append(area.GetSID()).Append(' ').Append((char) ('A' + area.Mode)).AppendLine();
                        count++;
                    }
                    builder.Append(count).Append(" area");
                    if (count != 1)
                        builder.Append('s');
                    return builder.ToString().Trim();
                }
            }

            public void Start() {
                Send(null, "Starting races still TODO.");
            }

            public void Send(GhostNetFrame frame, string text, string tag = "race", Color? color = null, bool fillVars = false, uint? id = null) {
                ChunkMChat msg = Manager.Server.CreateMChat(frame, text, tag, color ?? (frame != null ? ColorDefault : ColorBroadcast), fillVars, id);
                GhostNetFrame frameMsg = new GhostNetFrame {
                    HHead = frame?.HHead ?? new ChunkHHead {
                        PlayerID = uint.MaxValue
                    },
                    MChat = msg
                };
                foreach (uint playerID in Players) {
                    GhostNetConnection con = Manager.Server.Connections[(int) playerID];
                    if (con == null)
                        continue;
                    con.SendManagement(frameMsg, false);
                }
            }

            public void Handle(GhostNetConnection con, GhostNetFrame frame) {
                // TODO
            }

        }

    }
}
