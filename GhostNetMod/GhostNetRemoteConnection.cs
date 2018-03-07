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
    public class GhostNetRemoteConnection : GhostNetConnection {

        public TcpClient ManagementClient;
        public NetworkStream ManagementStream;
        public BinaryReader ManagementReader;
        public BinaryWriter ManagementWriter;

        public UdpClient UpdateClient;

        public Thread ReceiveManagementThread;
        public Thread ReceiveUpdateThread;
        public Thread TransferUpdateThread;

        protected Queue<Tuple<IPEndPoint, GhostNetFrame>> UpdateQueue = new Queue<Tuple<IPEndPoint, GhostNetFrame>>();

        protected static TcpClient GetTCP(string host, int port) {
            return new TcpClient(host, port);
        }

        protected static UdpClient GetUDP(string host, int port) {
            return new UdpClient(port);
        }

        public GhostNetRemoteConnection(
            string host, int port,
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) : this(
            GetTCP(host, port), GetUDP(host, port),
            onReceiveManagement, onReceiveUpdate
        ) {
        }
        public GhostNetRemoteConnection(
            TcpClient managementClient, UdpClient updateClient,
            Action<GhostNetConnection, GhostNetFrame> onReceiveManagement = null, Action<GhostNetConnection, IPEndPoint, GhostNetFrame> onReceiveUpdate = null
        ) : base(onReceiveManagement, onReceiveUpdate) {
            if (managementClient != null) {
                ManagementClient = managementClient;
                EndPoint = managementClient.Client.RemoteEndPoint as IPEndPoint;

                ManagementStream = ManagementClient.GetStream();
                ManagementReader = new BinaryReader(ManagementStream);
                ManagementWriter = new BinaryWriter(ManagementStream);

                if (onReceiveManagement != null) {
                    ReceiveManagementThread = new Thread(ReceiveManagementLoop);
                    ReceiveManagementThread.Name = $"GhostNetConnection ReceiveManagementThread {Context} {EndPoint}";
                    ReceiveManagementThread.IsBackground = true;
                    ReceiveManagementThread.Start();
                }
            }

            if (updateClient != null) {
                UpdateClient = updateClient;

                if (onReceiveUpdate != null) {
                    ReceiveUpdateThread = new Thread(ReceiveUpdateLoop);
                    ReceiveUpdateThread.Name = $"GhostNetConnection ReceiveUpdateThread {Context} {EndPoint}";
                    ReceiveUpdateThread.IsBackground = true;
                    ReceiveUpdateThread.Start();
                }

                TransferUpdateThread = new Thread(TransferUpdateLoop);
                TransferUpdateThread.Name = $"GhostNetConnection TransferUpdateThread {Context} {EndPoint}";
                TransferUpdateThread.IsBackground = true;
                TransferUpdateThread.Start();
            }
        }

        public override void SendManagement(GhostNetFrame frame) {
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                try {
                    // The frame writer seeks to update the frame length.
                    // TODO: Should management frames be sent from a separate thread?
                    frame.WriteManagement(bufferWriter);

                    bufferWriter.Flush();
                    bufferStream.Seek(0, SeekOrigin.Begin);
                    int length = (int) bufferStream.Position;
                    ManagementStream.Write(bufferStream.GetBuffer(), 0, length);
                    ManagementStream.Flush();
                    // Logger.Log(LogLevel.Warn, "ghostnet-con", "Sent management frame");
                } catch (SocketException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending management frame, socket fail");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                } catch (EndOfStreamException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending management frame, EOF");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                } catch (IOException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending management frame, IO fail");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                }
            }
        }

        public override void SendUpdate(GhostNetFrame frame) {
            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(default(IPEndPoint), frame));
            }
        }

        public override void SendUpdate(IPEndPoint remote, GhostNetFrame frame) {
            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(remote, frame));
            }
        }

        protected virtual void ReceiveManagementLoop() {
            while (ManagementClient.Connected) {
                Thread.Sleep(0);

                try {
                    // Let's just hope that the reader always reads a full frame...
                    GhostNetFrame frame = new GhostNetFrame();
                    frame.Read(ManagementReader);
                    // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Received management frame");
                    ReceiveManagement(frame);
                } catch (SocketException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving management frame, socket fail");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                } catch (EndOfStreamException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving management frame, EOF");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                } catch (IOException e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving management frame, IO fail");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                }
            }
        }

        protected virtual void ReceiveUpdateLoop() {
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryReader bufferReader = new BinaryReader(bufferStream)) {
                while (UpdateClient != null) {
                    Thread.Sleep(0);

                    IPEndPoint remote = EndPoint;
                    byte[] data = null;
                    try {
                        // Let's just hope that we always receive a full frame...
                        data = UpdateClient?.Receive(ref remote);
                    } catch (SocketException e) {
                        Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving update frame, socket fail");
                        LogContext(LogLevel.Warn);
                        e.LogDetailed();
                    } catch (EndOfStreamException e) {
                        Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving update frame, EOF");
                        LogContext(LogLevel.Warn);
                        e.LogDetailed();
                    } catch (IOException e) {
                        Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving update frame, IO fail");
                        LogContext(LogLevel.Warn);
                        e.LogDetailed();
                    }
                    if (remote == null || remote.Address != EndPoint.Address ||
                        data == null
                    ) {
                        continue;
                    }

                    bufferStream.Write(data, 0, data.Length);
                    bufferStream.Flush();
                    bufferStream.Seek(0, SeekOrigin.Begin);

                    GhostNetFrame frame = new GhostNetFrame();
                    frame.Read(bufferReader);

                    // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Received update frame");
                    ReceiveUpdate(frame);
                }
            }
        }

        // We need to actively transfer the update data from a separate thread. UDP isn't streamed.
        protected virtual void TransferUpdateLoop() {
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                while (UpdateClient != null) {
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
                            try {
                                // Let's just hope that we always send a full frame...
                                UpdateClient?.Send(bufferStream.GetBuffer(), length, entry.Item1 ?? EndPoint);
                                // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Sent update frame");
                            } catch (SocketException e) {
                                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending update frame, socket fail");
                                LogContext(LogLevel.Warn);
                                e.LogDetailed();
                            } catch (EndOfStreamException e) {
                                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending update frame, EOF");
                                LogContext(LogLevel.Warn);
                                e.LogDetailed();
                            } catch (IOException e) {
                                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending update frame, IO fail");
                                LogContext(LogLevel.Warn);
                                e.LogDetailed();
                            }
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing) {
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

    }
}
