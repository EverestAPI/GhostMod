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
    public class GhostNetLocalConnection : GhostNetConnection {

        public GhostNetLocalConnection(
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) : base(onReceiveManagement, onReceiveUpdate) {
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }

        public override void SendManagement(GhostNetFrame frame) {
            ReceiveManagement(frame);
        }

        public override void SendUpdate(GhostNetFrame frame) {
            ReceiveUpdate(frame);
        }

        public override void SendUpdate(IPEndPoint remote, GhostNetFrame frame) {
            throw new NotSupportedException();
        }

    }
}
