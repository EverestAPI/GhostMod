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

        public GhostNetRaceManager(GhostNetServer server) {
            server.Commands.Add(CommandRace);
            server.OnHandle += (con, frame) => Races.ForEach(race => race.Handle(con, frame));
        }

        public static void OnCreateServer(GhostNetServer server) {
            server.ModData["races"] = new GhostNetRaceManager(server);
        }

        public GhostNetCommand CommandRace = new GhostNetDCommand {
            Name = "race",
            Args = "add <sid> <side> | all | join <id> | start | leave",
            Help =
@"Create, join, start and leave races.",
            OnHandle = GhostNetDCommand.Handlers.Everything,
            OnRun = (cmd, env, args) => {
                if (args.Length == 0)
                    throw new Exception("At least one argument expected!");

                switch (args[0]) {
                    case "all":
                        if (args.Length != 1)
                            throw new Exception("Too many arguments!");

                        return;

                    case "add":
                        if (args.Length < 3)
                            throw new Exception("Not enough arguments!");
                        else if (args.Length > 3)
                            throw new Exception("Too many arguments!");

                        return;

                    case "join":

                        return;

                    case "start":

                        return;

                    case "leave":

                        return;

                    default:
                        throw new Exception($"Unknown subcommand {args[0]}!");
                }
            }
        };

        public class Race {

            public GhostNetRaceManager Manager;

            public List<int> Players = new List<int>();
            public List<AreaKey> Areas = new List<AreaKey>();

            public void Handle(GhostNetConnection con, GhostNetFrame frame) {

            }

        }

    }
}
