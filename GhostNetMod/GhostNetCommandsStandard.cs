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
    public static class GhostNetCommandsStandard {

        [GhostNetCommandField]
        public static GhostNetCommand Help = new GhostNetDCommand {
            Name = "help",
            Args = "[page] | [command]",
            Help = "Get help on how to use commands.",
            OnRun = (cmd, env, args) => {
                if (args.Length == 1) {
                    if (args[0].Type == GhostNetCommandArg.EType.Int) {
                        env.Send(Help_GetCommandPage(env, args[0].Int));
                        return;
                    }

                    env.Send(Help_GetCommandSnippet(env, args[0].String));
                    return;
                }

                env.Send(Help_GetCommandPage(env, 0));
            }
        };

        public static string Help_GetCommandPage(GhostNetCommandEnv env, int page = 0) {
            const int pageSize = 8;

            string prefix = GhostNetModule.Settings.ServerCommandPrefix;
            StringBuilder builder = new StringBuilder();

            int pages = (int) Math.Ceiling(env.Server.Commands.Count / (float) pageSize);
            if (page < 0 || pages <= page)
                throw new Exception("Page out of range!");

            for (int i = page * pageSize; i < (page + 1) * pageSize && i< env.Server.Commands.Count; i++) {
                GhostNetCommand cmd = env.Server.Commands[i];
                builder
                    .Append(prefix)
                    .Append(cmd.Name)
                    .Append(" ")
                    .Append(cmd.Args)
                    .AppendLine();
            }

            builder
                .Append("Page ")
                .Append(page + 1)
                .Append("/")
                .Append(pages);

            return builder.ToString().Trim();
        }

        public static string Help_GetCommandSnippet(GhostNetCommandEnv env, string cmdName) {
            GhostNetCommand cmd = env.Server.GetCommand(cmdName);
            if (cmd == null)
                throw new Exception($"Command {cmdName} not found!");

            return Help_GetCommandSnippet(env, cmd);
        }

        public static string Help_GetCommandSnippet(GhostNetCommandEnv env, GhostNetCommand cmd) {
            string prefix = GhostNetModule.Settings.ServerCommandPrefix;
            StringBuilder builder = new StringBuilder();

            builder
                .Append(prefix)
                .Append(cmd.Name)
                .Append(" ")
                .Append(cmd.Args)
                .AppendLine()
                .AppendLine(cmd.Help);

            return builder.ToString().Trim();
        }

        [GhostNetCommandField]
        public static GhostNetCommand OP = new GhostNetDCommand {
            Name = "op",
            Args = "<id>",
            Help = "OP: Make another player an OP.",
            OnRun = (cmd, env, args) => {
                if (!env.IsOP)
                    throw new Exception("You're not OP!");
                if (args.Length != 1)
                    throw new Exception("Exactly 1 argument required!");

                int id = args[0].Int;
                if (env.Server.OPs.Contains((uint) id))
                    throw new Exception($"#{id} already OP!");

                env.Server.OPs.Add((uint) id);
            }
        };

        [GhostNetCommandField]
        public static GhostNetCommand Kick = new GhostNetDCommand {
            Name = "kick",
            Args = "<id>",
            Help = "OP: Kick a player from the server.",
            OnRun = (cmd, env, args) => {
                if (!env.IsOP)
                    throw new Exception("You're not OP!");
                if (args.Length != 1)
                    throw new Exception("Exactly 1 argument required!");

                int id = args[0].Int;
                if (env.Server.OPs.IndexOf((uint) id) < env.Server.OPs.IndexOf(env.PlayerID))
                    throw new Exception("Cannot kick a higher OP!");

                GhostNetConnection other = args[0].Connection;
                other.Dispose();
            }
        };

        [GhostNetCommandField]
        public static GhostNetCommand Broadcast = new GhostNetDCommand {
            Name = "broadcast",
            Args = "<text>",
            Help = "OP: Broadcast something as the server.",
            OnParse = GhostNetDCommand.Parsers.Everything,
            OnRun = (cmd, env, args) => {
                if (!env.IsOP)
                    throw new Exception("You're not OP!");
                if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                    return;

                env.Server.BroadcastMChat(env.Frame, args[0], fillVars: false);
            }
        };

        [GhostNetCommandField]
        public static GhostNetCommand Teleport = new GhostNetDCommand {
            Name = "tp",
            Args = "<id>",
            Help = "Teleport to another player.",
            OnRun = (cmd, env, args) => {
                if (args.Length != 1)
                    throw new Exception("Exactly 1 argument required!");

                ChunkMPlayer other = args[0].Player;
                if (string.IsNullOrEmpty(other.SID))
                    throw new Exception("Player in menu!");

                ChunkMChat msg = env.Send($"Teleporting to {other.Name}#{args[0].Int}...");

                ChunkMSession session;
                // Request the current session information from the other player.
                env.Server.Request(args[0].Connection, out session);

                env.Connection.SendManagement(new GhostNetFrame {
                    env.HHead,

                    new ChunkMPlayer {
                        Name = env.MPlayer.Name,
                        SID = other.SID,
                        Mode = other.Mode,
                        Level = other.Level
                    },

                    session
                }, true);

                msg.Text = $"Teleported to {other.Name}#{args[0].Int}";
                env.Connection.SendManagement(new GhostNetFrame {
                    env.HHead,
                    msg
                }, true);

            }
        };

        [GhostNetCommandField]
        public static GhostNetCommand Emote = new GhostNetDCommand {
            Name = "emote",
            Args = "<text> | i:<img> | p:<img>",
            Help =
@"Send an emote appearing over your player.
Normal text appears over your player.
This syntax also works for your ""favorites"" (settings file).
i:TEXTURE shows TEXTURE from the GUI atlas.
p:TEXTURE shows TEXTURE from the Portraits atlas.
p:FRM1 FRM2 FRM3 plays an animation, 7 FPS by default.
p:10 FRM1 FRM2 FRM3 plays the animation at 10 FPS.",
            OnParse = GhostNetDCommand.Parsers.Everything,
            OnRun = (cmd, env, args) => {
                if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                    return;

                env.Server.Handle(env.Connection, new GhostNetFrame {
                    env.HHead,
                    new ChunkMEmote {
                        Value = args[0]
                    }
                });
            }
        };

    }
}
