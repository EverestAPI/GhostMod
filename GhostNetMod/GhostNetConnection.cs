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
    public class GhostNetConnection : IDisposable {

        public EndPoint EndPoint;

        public TcpClient ManagementClient;
        public NetworkStream ManagementStream;
        public BinaryReader ManagementReader;
        public BinaryWriter ManagementWriter;

        public UdpClient UpdateClient;

        public Thread ReceiveManagementThread;
        public Thread ReceiveUpdateThread;
        public Thread TransferUpdateThread;

        protected Queue<Tuple<IPEndPoint, GhostNetFrame>> UpdateQueue = new Queue<Tuple<IPEndPoint, GhostNetFrame>>();

        protected static TcpClient GetTCP(string host, int ip) {
            return new TcpClient(host, ip);
        }

        protected static UdpClient GetUDP(string host, int ip) {
            return new UdpClient(host, ip);
        }

        public GhostNetConnection() {
        }

        public GhostNetConnection(
            string host, int ip,
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) : this(
            GetTCP(host, ip), GetUDP(host, ip),
            onReceiveManagement, onReceiveUpdate
        ) {
        }
        public GhostNetConnection(
            TcpClient managementClient, UdpClient updateClient,
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) {
            if (managementClient != null) {
                ManagementClient = managementClient;
                EndPoint = managementClient.Client.RemoteEndPoint;

                ManagementStream = ManagementClient.GetStream();
                ManagementReader = new BinaryReader(ManagementStream);
                ManagementWriter = new BinaryWriter(ManagementStream);

                if (onReceiveManagement != null) {
                    ReceiveManagementThread = new Thread(ReceiveManagementLoop(onReceiveManagement));
                    ReceiveManagementThread.IsBackground = true;
                    ReceiveManagementThread.Start();
                }
            }

            if (updateClient != null) {
                UpdateClient = updateClient;

                if (onReceiveUpdate != null) {
                    ReceiveUpdateThread = new Thread(ReceiveUpdateLoop(onReceiveUpdate));
                    ReceiveUpdateThread.IsBackground = true;
                    ReceiveUpdateThread.Start();
                }

                TransferUpdateThread = new Thread(TransferUpdateLoop);
                TransferUpdateThread.IsBackground = true;
                TransferUpdateThread.Start();
            }
        }

        public void SendManagement(GhostNetFrame frame) {
            frame.WriteManagement(ManagementWriter);
            ManagementWriter.Flush();
        }

        public void SendUpdate(GhostNetFrame frame) {
            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(default(IPEndPoint), frame));
            }
        }

        public void SendUpdate(IPEndPoint remote, GhostNetFrame frame) {
            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(remote, frame));
            }
        }

        protected virtual ThreadStart ReceiveManagementLoop(Action<GhostNetConnection, GhostNetFrame> onReceive) => () => {
            while (ManagementClient.Connected) {
                Thread.Sleep(0);

                // Let's just hope that the reader always reads a full frame...
                GhostNetFrame frame = new GhostNetFrame();
                frame.Read(ManagementReader);
                onReceive(this, frame);
            }
        };

        protected virtual ThreadStart ReceiveUpdateLoop(Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceive) => () => {
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryReader bufferReader = new BinaryReader(bufferStream)) {
                while (ManagementClient.Connected) {
                    Thread.Sleep(0);

                    IPEndPoint remote = EndPoint as IPEndPoint;
                    byte[] data = UpdateClient.Receive(ref remote);
                    bufferStream.Write(data, 0, data.Length);
                    bufferStream.Flush();
                    bufferStream.Seek(0, SeekOrigin.Begin);

                    GhostNetFrame frame = new GhostNetFrame();
                    frame.Read(bufferReader);

                    onReceive(this, remote, frame);
                }
            }
        };

        // We need to actively transfer the update data from a separate thread. UDP isn't streamed.
        protected virtual void TransferUpdateLoop() {
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                while (ManagementClient.Connected) {
                    Thread.Sleep(0);

                    if (UpdateQueue.Count == 0)
                        continue;

                    lock (UpdateQueue) {
                        while (UpdateQueue.Count > 0) {
                            Tuple<IPEndPoint, GhostNetFrame> entry = UpdateQueue.Dequeue();
                            entry.Item2.WriteUpdate(bufferWriter);

                            bufferWriter.Flush();
                            bufferStream.Seek(0, SeekOrigin.Begin);
                            int length = (int) bufferStream.Position;
                            UpdateClient.Send(bufferStream.GetBuffer(), length, entry.Item1 ?? (EndPoint as IPEndPoint));
                        }
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing) {
            ManagementReader?.Dispose();
            ManagementReader = null;

            ManagementWriter?.Dispose();
            ManagementWriter = null;

            ManagementStream?.Dispose();
            ManagementStream = null;

            ManagementClient?.Close();
            ManagementClient = null;

            UpdateClient?.Close();
            UpdateClient = null;
        }

        public void Dispose() {
            Dispose(true);
        }

    }
}
