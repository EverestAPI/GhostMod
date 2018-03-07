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
    public abstract class GhostNetConnection : IDisposable {

        public string Context;

        public IPEndPoint EndPoint;

        public event Action<GhostNetConnection, GhostNetFrame> OnReceiveManagement;
        public event Action<GhostNetConnection, IPEndPoint, GhostNetFrame> OnReceiveUpdate;

        public GhostNetConnection() {
            // Get the context in which the connection was created.
            StackTrace trace = new StackTrace();
            foreach (StackFrame frame in trace.GetFrames()) {
                MethodBase method = frame.GetMethod();
                if (method.IsConstructor)
                    continue;

                Context = method.DeclaringType?.Name;
                Context = (Context == null ? "" : Context + "::") + method.Name;
                break;
            }
        }

        public GhostNetConnection(
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) : this() {
            OnReceiveManagement = onReceiveManagement;
            OnReceiveUpdate = onReceiveUpdate;
        }

        public abstract void SendManagement(GhostNetFrame frame);

        public abstract void SendUpdate(GhostNetFrame frame);

        public abstract void SendUpdate(IPEndPoint remote, GhostNetFrame frame);

        protected virtual void ReceiveManagement(GhostNetFrame frame)
            => OnReceiveManagement?.Invoke(this, frame);

        protected virtual void ReceiveUpdate(GhostNetFrame frame)
            => OnReceiveUpdate?.Invoke(this, EndPoint, frame);

        public void LogContext(LogLevel level) {
            Logger.Log(level, "ghostnet-con", $"Context: {Context} {EndPoint}");
        }

        protected virtual void Dispose(bool disposing) {
        }

        public void Dispose() {
            Dispose(true);
        }

    }
}
