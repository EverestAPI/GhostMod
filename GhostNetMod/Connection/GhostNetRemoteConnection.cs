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
    public class GhostNetRemoteConnection : GhostNetConnection {

        public TcpClient ManagementClient;
        public NetworkStream ManagementStream;
        public BinaryReader ManagementReader;

        public UdpClient UpdateClient;

        public Thread ReceiveManagementThread;
        public Thread TransferManagementThread;
        public Thread ReceiveUpdateThread;
        public Thread TransferUpdateThread;

        public bool DisposeOnFailure = true;

        protected Queue<Tuple<GhostNetFrame, bool>> ManagementQueue = new Queue<Tuple<GhostNetFrame, bool>>();
        protected Queue<Tuple<GhostNetFrame, bool, IPEndPoint>> UpdateQueue = new Queue<Tuple<GhostNetFrame, bool, IPEndPoint>>();

        protected static TcpClient GetTCP(string host, int port) {
            return new TcpClient(host, port);
        }

        protected static UdpClient GetUDP(string host, int port) {
            return new UdpClient(port);
        }

        public GhostNetRemoteConnection(string host, int port)
            : this(GetTCP(host, port), GetUDP(host, port)) {
        }
        public GhostNetRemoteConnection(TcpClient managementClient, UdpClient updateClient)
            : base() {
            if (managementClient != null) {
                ManagementClient = managementClient;
                ManagementEndPoint = managementClient.Client.RemoteEndPoint as IPEndPoint;

                ManagementStream = ManagementClient.GetStream();
                ManagementReader = new BinaryReader(ManagementStream);

                ReceiveManagementThread = new Thread(ReceiveManagementLoop);
                ReceiveManagementThread.Name = $"GhostNetConnection ReceiveManagementThread {Context} {ManagementEndPoint}";
                ReceiveManagementThread.IsBackground = true;
                ReceiveManagementThread.Start();

                TransferManagementThread = new Thread(TransferManagementLoop);
                TransferManagementThread.Name = $"GhostNetConnection TransferManagementThread {Context} {ManagementEndPoint}";
                TransferManagementThread.IsBackground = true;
                TransferManagementThread.Start();
            }

            if (updateClient != null) {
                UpdateClient = updateClient;

                ReceiveUpdateThread = new Thread(ReceiveUpdateLoop);
                ReceiveUpdateThread.Name = $"GhostNetConnection ReceiveUpdateThread {Context} {ManagementEndPoint}";
                ReceiveUpdateThread.IsBackground = true;
                ReceiveUpdateThread.Start();

                TransferUpdateThread = new Thread(TransferUpdateLoop);
                TransferUpdateThread.Name = $"GhostNetConnection TransferUpdateThread {Context} {ManagementEndPoint}";
                TransferUpdateThread.IsBackground = true;
                TransferUpdateThread.Start();
            }
        }

        public override void SendManagement(GhostNetFrame frame, bool release) {
            lock (ManagementQueue) {
                ManagementQueue.Enqueue(Tuple.Create(frame, release));
            }
        }

        public override void SendUpdate(GhostNetFrame frame, bool release) {
            if (GhostNetModule.Settings.SendUFramesInMStream) {
                SendManagement(frame, release);
                return;
            }

            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(frame, release, default(IPEndPoint)));
            }
        }

        public override void SendUpdate(GhostNetFrame frame, IPEndPoint remote, bool release) {
            if (GhostNetModule.Settings.SendUFramesInMStream) {
                throw new NotSupportedException("Sending updates to another client not supported if SendUFramesInMStream enabled.");
            }

            lock (UpdateQueue) {
                UpdateQueue.Enqueue(Tuple.Create(frame, release, remote));
            }
        }

        protected virtual void ReceiveManagementLoop() {
            while (ManagementClient?.Connected ?? false) {
                Thread.Sleep(0);

                GhostNetFrame frame = new GhostNetFrame();
                try {
                    // Let's just hope that the reader always reads a full frame...
                    frame.Read(ManagementReader);
                } catch (Exception e) {
                    if (!(ManagementClient?.Connected ?? false)) {
                        Dispose();
                        return;
                    }

                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving management frame");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                    if (DisposeOnFailure) {
                        Dispose();
                        return;
                    }
                }
                // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Received management frame");
                ReceiveManagement(ManagementEndPoint, frame);
            }
            // Not connected - dispose.
            Dispose();
        }

        // We need to actively transfer the update data from a separate thread. TCP is streamed, but blocking.
        protected virtual void TransferManagementLoop() {
            while (ManagementClient?.Connected ?? false) {
                Thread.Sleep(0);

                if (ManagementQueue.Count == 0)
                    continue;

                lock (ManagementQueue) {
                    while (ManagementQueue.Count > 0) {
                        Tuple<GhostNetFrame, bool> entry = ManagementQueue.Dequeue();
                        using (MemoryStream bufferStream = new MemoryStream())
                        using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                            entry.Item1.Write(bufferWriter);

                            bufferWriter.Flush();
                            byte[] buffer = bufferStream.ToArray();
                            try {
                                ManagementStream.Write(buffer, 0, buffer.Length);
                                ManagementStream.Flush();
                                // Logger.Log(LogLevel.Warn, "ghostnet-con", "Sent management frame");
                            } catch (Exception e) {
                                if (!(ManagementClient?.Connected ?? false)) {
                                    Dispose();
                                    return;
                                }

                                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending management frame");
                                LogContext(LogLevel.Warn);
                                e.LogDetailed();
                                if (DisposeOnFailure) {
                                    Dispose();
                                    return;
                                }
                            }

                            bufferStream.Seek(0, SeekOrigin.Begin);
                        }
                    }
                }
            }
            // Not connected - dispose.
            Dispose();
        }

        protected virtual void ReceiveUpdateLoop() {
            while (UpdateClient != null) {
                Thread.Sleep(0);

                IPEndPoint known = UpdateEndPoint ?? ManagementEndPoint;
                IPEndPoint remote = known;
                byte[] data = null;
                try {
                    // Let's just hope that we always receive a full frame...
                    // Console.WriteLine("Starting receive update");
                    data = UpdateClient?.Receive(ref remote);
                    // Console.WriteLine($"Finished receive update from {remote}: {data.ToHexadecimalString()}");
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed receiving update frame");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                    if (DisposeOnFailure) {
                        Dispose();
                        return;
                    }
                }
                if (known != null && !remote.Address.Equals(known.Address)) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", $"Received update data from unknown remote {remote}: {data.ToHexadecimalString()}");
                    continue;
                }

                try {
                    using (MemoryStream bufferStream = new MemoryStream(data))
                    using (BinaryReader bufferReader = new BinaryReader(bufferStream)) {
                        GhostNetFrame frame = new GhostNetFrame();
                        frame.Read(bufferReader);
                        // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Received update frame");
                        ReceiveUpdate(remote, frame);
                    }
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed parsing update frame");
                    LogContext(LogLevel.Warn);
                    e.LogDetailed();
                    Console.WriteLine("Data:");
                    Console.WriteLine(data.ToHexadecimalString());
                    // Don't dispose - maybe upcoming data isn't broken?
                }
            }
        }

        // We need to actively transfer the update data from a separate thread. UDP isn't streamed.
        protected virtual void TransferUpdateLoop() {
            while (UpdateClient != null) {
                Thread.Sleep(0);

                if (UpdateQueue.Count == 0)
                    continue;

                lock (UpdateQueue) {
                    while (UpdateQueue.Count > 0) {
                        Tuple<GhostNetFrame, bool, IPEndPoint> entry = UpdateQueue.Dequeue();
                        using (MemoryStream bufferStream = new MemoryStream())
                        using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                            entry.Item1.Write(bufferWriter);

                            bufferWriter.Flush();
                            byte[] buffer = bufferStream.ToArray();
                            try {
                                // Let's just hope that we always send a full frame...
                                UpdateClient.Send(buffer, buffer.Length, entry.Item3 ?? UpdateEndPoint ?? ManagementEndPoint);
                                // Logger.Log(LogLevel.Verbose, "ghostnet-con", "Sent update frame");
                            } catch (Exception e) {
                                bufferStream.Seek(0, SeekOrigin.Begin);

                                Logger.Log(LogLevel.Warn, "ghostnet-con", "Failed sending update frame");
                                LogContext(LogLevel.Warn);
                                e.LogDetailed();
                                if (DisposeOnFailure) {
                                    Dispose();
                                    return;
                                }
                            }

                            bufferStream.Seek(0, SeekOrigin.Begin);
                        }
                    }
                }
            }
        }

        private object DisposeLock = new object();
        private bool Disposed = false;
        protected override void Dispose(bool disposing) {
            lock (DisposeLock) {
                if (Disposed)
                    return;
                Disposed = true;

                base.Dispose(disposing);

                ManagementReader?.Dispose();
                ManagementReader = null;

                ManagementStream?.Dispose();
                ManagementStream = null;

                ManagementClient?.Close();
                ManagementClient = null;

                UpdateClient?.Close();
                UpdateClient = null;
            }
        }

    }
}
