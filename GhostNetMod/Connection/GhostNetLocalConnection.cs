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

        public GhostNetLocalConnection()
            : base() {
            ManagementEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            UpdateEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }

        public override void SendManagement(GhostNetFrame frame) {
            ReceiveManagement(ManagementEndPoint, frame);
        }

        public override void SendUpdate(GhostNetFrame frame) {
            ReceiveUpdate(UpdateEndPoint, frame);
        }

        public override void SendUpdate(IPEndPoint remote, GhostNetFrame frame) {
            // Local connections don't support sending updates to another client.
            throw new NotSupportedException();
        }

    }
}
