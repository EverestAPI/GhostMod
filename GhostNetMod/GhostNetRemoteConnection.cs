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

        public bool DisposeOnFailure = true;

        protected Queue<Tuple<IPEndPoint, GhostNetFrame>> UpdateQueue = new Queue<Tuple<IPEndPoint, GhostNetFrame>>();

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
                ManagementWriter = new BinaryWriter(ManagementStream);

                ReceiveManagementThread = new Thread(ReceiveManagementLoop);
                ReceiveManagementThread.Name = $"GhostNetConnection ReceiveManagementThread {Context} {ManagementEndPoint}";
                ReceiveManagementThread.IsBackground = true;
                ReceiveManagementThread.Start();
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

        public override void SendManagement(GhostNetFrame frame) {
            if (ManagementStream == null || ManagementClient == null || !ManagementClient.Connected)
                return;

            // The frame writer seeks to update the frame length.
            // Write it into a buffer, then into the network.
            using (MemoryStream bufferStream = new MemoryStream())
            using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                // TODO: Should management frames be sent from a separate thread?
                frame.WriteManagement(bufferWriter);

                bufferWriter.Flush();
                byte[] buffer = bufferStream.ToArray();

                try {
                    ManagementStream.Write(buffer, 0, buffer.Length);
                    // Logger.Log(LogLevel.Warn, "ghostnet-con", "Sent management frame");
                } catch (Exception e) {
                    if (!ManagementClient.Connected) {
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
                    ReceiveManagement(ManagementEndPoint, frame);
                } catch (Exception e) {
                    if (!ManagementClient.Connected) {
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
                if (remote == null ||
                    (known != null && !remote.Address.Equals(known.Address)) ||
                    data == null
                ) {
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
                        Tuple<IPEndPoint, GhostNetFrame> entry = UpdateQueue.Dequeue();
                        using (MemoryStream bufferStream = new MemoryStream())
                        using (BinaryWriter bufferWriter = new BinaryWriter(bufferStream)) {
                            entry.Item2.WriteUpdate(bufferWriter);

                            bufferWriter.Flush();
                            byte[] buffer = bufferStream.ToArray();
                            try {
                                // Let's just hope that we always send a full frame...
                                UpdateClient.Send(buffer, buffer.Length, entry.Item1 ?? UpdateEndPoint ?? ManagementEndPoint);
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

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

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
