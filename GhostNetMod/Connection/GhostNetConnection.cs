using Celeste.Mod;
using Microsoft.Xna.Framework;
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
    public abstract class GhostNetConnection : IDisposable {

        public string Context;

        public IPEndPoint ManagementEndPoint;
        public IPEndPoint UpdateEndPoint;

        public Action<GhostNetConnection, IPEndPoint, GhostNetFrame> OnReceiveManagement;
        public Action<GhostNetConnection, IPEndPoint, GhostNetFrame> OnReceiveUpdate;
        public Action<GhostNetConnection> OnDisconnect;

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

        public abstract void SendManagement(GhostNetFrame frame, bool release);

        public abstract void SendUpdate(GhostNetFrame frame, bool release);

        public abstract void SendUpdate(GhostNetFrame frame, IPEndPoint remote, bool release);

        protected virtual void ReceiveManagement(IPEndPoint remote, GhostNetFrame frame) {
            ManagementEndPoint = remote;
            try {
                OnReceiveManagement?.Invoke(this, remote, frame);
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed handling management frame");
                LogContext(LogLevel.Warn);
                e.LogDetailed();
            }
        }

        protected virtual void ReceiveUpdate(IPEndPoint remote, GhostNetFrame frame) {
            UpdateEndPoint = remote;
            try {
                OnReceiveUpdate?.Invoke(this, remote, frame);
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed handling update frame");
                LogContext(LogLevel.Warn);
                e.LogDetailed();
            }
        }

        public void LogContext(LogLevel level) {
            Logger.Log(level, "ghostnet-con", $"Context: {Context} {ManagementEndPoint} {UpdateEndPoint}");
        }

        protected virtual void Dispose(bool disposing) {
            OnDisconnect?.Invoke(this);
        }

        public void Dispose() {
            Dispose(true);
        }

    }
}
