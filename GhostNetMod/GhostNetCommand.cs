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
    public abstract class GhostNetCommand {

        public readonly static char[] CommandNameDelimiters = {
            ' ', '\n'
        };

        public abstract string Name { get; set; }
        public abstract string Args { get; set; }
        public abstract string Help { get; set; }

        public virtual void Handle(GhostNetCommandEnv env) {
            string raw = env.Text;

            int index = GhostNetModule.Settings.ServerCommandPrefix.Length + Name.Length - 1; // - 1 because next space required
            List<GhostNetCommandArg> args = new List<GhostNetCommandArg>();
            while (
                index + 1 < raw.Length &&
                (index = raw.IndexOf(' ', index + 1)) >= 0
            ) {
                int next = index + 1 < raw.Length ? raw.IndexOf(' ', index + 1) : -2;
                if (next < 0) next = raw.Length;

                int argIndex = index + 1;
                int argLength = next - index - 1;
                string argString = raw.Substring(argIndex, argLength);

                // + 1 because space
                args.Add(new GhostNetCommandArg(env).Handle(raw, argIndex, argLength));

                // Handle a range
                if (args.Count >= 3 &&
                    args[args.Count - 3].Type == GhostNetCommandArg.EType.Int &&
                    (args[args.Count - 2].String == "-" || args[args.Count - 2].String == "+") &&
                    args[args.Count - 1].Type == GhostNetCommandArg.EType.Int
                ) {
                    args.Add(new GhostNetCommandArg(env).Handle(raw, args[args.Count - 3].Index, next - args[args.Count - 3].Index));
                    args.RemoveRange(args.Count - 4, 3);
                    continue;
                }
            }

            Run(env, args.ToArray());
        }

        public virtual void Run(GhostNetCommandEnv env, params GhostNetCommandArg[] args) {

        }

    }

    public class GhostNetDCommand : GhostNetCommand {

        public override string Name { get; set; }
        public override string Args { get; set; }
        public override string Help { get; set; }

        public Action<GhostNetCommand, GhostNetCommandEnv> OnHandle;
        public override void Handle(GhostNetCommandEnv env) {
            if (OnHandle != null) {
                OnHandle(this, env);
                return;
            }
            base.Handle(env);
        }

        public Action<GhostNetCommand, GhostNetCommandEnv, GhostNetCommandArg[]> OnRun;
        public override void Run(GhostNetCommandEnv env, params GhostNetCommandArg[] args)
            => OnRun(this, env, args);

        public static class Handlers {
            /// <summary>
            /// Handle everything as one argument and run the command.
            /// </summary>
            public static void Everything(GhostNetCommand cmd, GhostNetCommandEnv env)
                => cmd.Run(env, new GhostNetCommandArg(env).Handle(env.Text, GhostNetModule.Settings.ServerCommandPrefix.Length + cmd.Name.Length + 1));
        }

    }

    public class GhostNetCommandArg {

        public GhostNetCommandEnv Env;

        public string RawText;
        public string String;
        public int Index;

        public EType Type;

        public int Int;
        public long Long;
        public ulong ULong;
        public float Float;

        public int IntRangeFrom;
        public int IntRangeTo;
        public int IntRangeMin => Math.Min(IntRangeFrom, IntRangeTo);
        public int IntRangeMax => Math.Max(IntRangeFrom, IntRangeTo);

        public GhostNetConnection Connection {
            get {
                if (Type != EType.Int)
                    throw new Exception("Argument not an ID!");
                if (Int < 0 || Env.Server.Connections.Count <= Int)
                    throw new Exception("ID out of range!");

                GhostNetConnection con = Env.Server.Connections[Int];
                if (con == null)
                    throw new Exception("ID already disconnected!");

                return con;
            }
        }

        public GhostNetFrame Player {
            get {
                if (Type != EType.Int)
                    throw new Exception("Argument not an ID!");
                if (Int < 0 || Env.Server.Connections.Count <= Int)
                    throw new Exception("ID out of range!");

                GhostNetFrame player;
                if (!Env.Server.PlayerMap.TryGetValue((uint) Int, out player) || string.IsNullOrEmpty(player.MPlayer?.Name))
                    throw new Exception("ID already disconnected!");

                return player;
            }
        }

        public GhostNetCommandArg(GhostNetCommandEnv env) {
            Env = env;
        }

        public virtual GhostNetCommandArg Handle(string raw, int index) {
            RawText = raw;
            if (index < 0 || raw.Length <= index) {
                String = "";
                Index = 0;
                return this;
            }
            String = raw.Substring(index);
            Index = index;

            return Handle();
        }
        public virtual GhostNetCommandArg Handle(string raw, int index, int length) {
            RawText = raw;
            String = raw.Substring(index, length);
            Index = index;

            return Handle();
        }

        public virtual GhostNetCommandArg Handle() {
            if (int.TryParse(String, out Int)) {
                Type = EType.Int;
                Long = IntRangeFrom = IntRangeTo = Int;
                ULong = (ulong) Int;

            } else if (long.TryParse(String, out Long)) {
                Type = EType.Long;
                ULong = (ulong) Long;

            } else if (ulong.TryParse(String, out ULong)) {
                Type = EType.ULong;

            } else if (float.TryParse(String, out Float)) {
                Type = EType.Float;
            }

            if (Type == EType.String) {
                string[] split;
                int from, to;
                if ((split = String.Split('-')).Length == 2) {
                    if (int.TryParse(split[0].Trim(), out from) && int.TryParse(split[1].Trim(), out to)) {
                        Type = EType.IntRange;
                        IntRangeFrom = from;
                        IntRangeTo = to;
                    }
                } else if ((split = String.Split('+')).Length == 2) {
                    if (int.TryParse(split[0].Trim(), out from) && int.TryParse(split[1].Trim(), out to)) {
                        Type = EType.IntRange;
                        IntRangeFrom = from;
                        IntRangeTo = from + to;
                    }
                }
            }

            return this;
        }

        public string Restored {
            get {
                return RawText.Substring(Index);
            }
        }

        public override string ToString() {
            return String;
        }

        public static implicit operator string(GhostNetCommandArg d) {
            return d.String;
        }

        public enum EType {
            String,

            Int,
            IntRange,

            Long,
            ULong,

            Float,
        }

    }

    public struct GhostNetCommandEnv {

        public GhostNetServer Server;
        public GhostNetConnection Connection;
        public GhostNetFrame Frame;

        public string Text => Frame.MChat?.Text;

        public bool IsOP => Frame.HHead.PlayerID == 0;

        public ChunkMChat Send(string text, Color? color = null, bool fillVars = false)
            => Server.SendMChat(Connection, Frame, text, color, fillVars);

    }

    public class GhostNetCommandFieldAttribute : Attribute {

    }
}
