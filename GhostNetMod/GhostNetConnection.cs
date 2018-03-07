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

        protected Queue<GhostNetFrame> UpdateQueue = new Queue<GhostNetFrame>();

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
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, GhostNetFrame> onReceiveUpdate = null
        ) : this(
            GetTCP(host, ip), GetUDP(host, ip),
            onReceiveManagement, onReceiveUpdate
        ) {
        }
        public GhostNetConnection(
            TcpClient managementClient, UdpClient updateClient,
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, GhostNetFrame> onReceiveUpdate = null
        ) {
            ManagementClient = managementClient;
            EndPoint = managementClient.Client.RemoteEndPoint;

            ManagementStream = ManagementClient.GetStream();
            ManagementReader = new BinaryReader(ManagementStream);
            ManagementWriter = new BinaryWriter(ManagementStream);

            UpdateClient = updateClient;

            if (onReceiveManagement != null) {
                ReceiveManagementThread = new Thread(ReceiveManagementLoop(onReceiveManagement));
                ReceiveManagementThread.IsBackground = true;
                ReceiveManagementThread.Start();
            }

            if (onReceiveUpdate != null) {
                ReceiveUpdateThread = new Thread(ReceiveUpdateLoop(onReceiveUpdate));
                ReceiveUpdateThread.IsBackground = true;
                ReceiveUpdateThread.Start();
            }

            TransferUpdateThread = new Thread(TransferUpdateLoop);
            TransferUpdateThread.IsBackground = true;
            TransferUpdateThread.Start();
        }

        public void SendManagement(GhostNetFrame frame) {
            frame.WriteManagement(ManagementWriter);
            ManagementWriter.Flush();
        }

        public void SendUpdate(GhostNetFrame frame) {
            lock (UpdateQueue) {
                UpdateQueue.Enqueue(frame);
            }
        }

        protected virtual ThreadStart ReceiveManagementLoop(Action<GhostNetConnection, GhostNetFrame> onReceive) => () => {
            while (ManagementClient.Connected) {
                Thread.Sleep(0);

                // TODO: Read management frames.
            }
        };

        protected virtual ThreadStart ReceiveUpdateLoop(Action<GhostNetConnection, GhostNetFrame> onReceive) => () => {
            while (ManagementClient.Connected) {
                Thread.Sleep(0);

                // TODO: Read update frames.
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
                            GhostNetFrame frame = UpdateQueue.Dequeue();

                            frame.WriteUpdate(bufferWriter);

                            bufferWriter.Flush();
                            bufferStream.Seek(0, SeekOrigin.Begin);
                            int length = (int) bufferStream.Position;
                            UpdateClient.Send(bufferStream.GetBuffer(), length);
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
