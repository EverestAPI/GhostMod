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
    public class GhostNetLocalConnection : GhostNetConnection {

        public GhostNetLocalConnection()
            : base() {
            ManagementEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            UpdateEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }

        public override void SendManagement(GhostNetFrame frame, bool release) {
            ReceiveManagement(ManagementEndPoint, (GhostNetFrame) frame.Clone());
        }

        public override void SendUpdate(GhostNetFrame frame, bool release) {
            ReceiveUpdate(UpdateEndPoint, (GhostNetFrame) frame.Clone());
        }

        public override void SendUpdate(GhostNetFrame frame, IPEndPoint remote, bool release) {
            throw new NotSupportedException("Local connections don't support sending updates to another client.");
        }

    }
}
