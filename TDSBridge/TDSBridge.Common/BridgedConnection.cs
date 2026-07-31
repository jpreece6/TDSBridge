using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using TDSBridge.Common.Header;
using TDSBridge.Common.Message;
using TDSBridge.Common.Packet;

namespace TDSBridge.Common
{
    public enum ConnectionType { ClientBridge, BridgeSQL };

    
    
    public class BridgedConnection
    {
        public BridgeAcceptor BridgeAcceptor { get; protected set; }
        public SocketCouple SocketCouple { get; protected set; }

        private ConcurrentQueue<TDSMessage> _messages = new();
        
        readonly IMemoryCache  _cache;
        

        public BridgedConnection(BridgeAcceptor BridgeAcceptor, SocketCouple SocketCouple, IMemoryCache cache)
        {
            this.BridgeAcceptor = BridgeAcceptor;
            this.SocketCouple = SocketCouple;
            this._cache = cache;
        }

        public void Start()
        {
            Thread tIn = new Thread(new ThreadStart(ClientBridgeThread));
            tIn.IsBackground = true;
            tIn.Start();

            Thread tOut = new Thread(new ThreadStart(BridgeSQLThread));
            tOut.IsBackground = true;
            tOut.Start();
        }

        protected virtual void ClientBridgeThread()
        {
            try
            {
                byte[] bBuffer = null;
                byte[] bHeader = new byte[Header.TDSHeader.HEADER_SIZE];
                int iReceived = 0;

                Message.TDSMessage tdsMessage = null;

                while ((iReceived = SocketCouple.ClientBridgeSocket.Receive(bHeader, Header.TDSHeader.HEADER_SIZE, SocketFlags.None)) > 0)
                //while ((iReceived = sc.InputSocket.Receive(bBuffer, SocketFlags.None)) > 0)
                {
                    Header.TDSHeader header = new Header.TDSHeader(bHeader);

                    int iMinBufferSize = Math.Max(0x1000, header.LengthIncludingHeader + 1);
                    if ((bBuffer == null) || (bBuffer.Length < iMinBufferSize))
                    {
                        bBuffer = new byte[iMinBufferSize];
                    }

                    //Console.WriteLine(header.Type);

                    if (header.Type == (HeaderType)23)
                    {
                        iReceived = SocketCouple.ClientBridgeSocket.Receive(bBuffer, 0, 0x1000 - Header.TDSHeader.HEADER_SIZE, SocketFlags.None);
                    }
                    else if(header.PayloadSize > 0)
                    {
                        //Console.WriteLine("\t{0:N0} bytes package", header.LengthIncludingHeader);
                        SocketCouple.ClientBridgeSocket.Receive(bBuffer, 0, header.PayloadSize, SocketFlags.None);
                    }
                    TDSPacket tdsPacket = new TDSPacket(bHeader, bBuffer, header.PayloadSize);
                    OnTDSPacketReceived(tdsPacket);

                    if (tdsMessage == null)
                        tdsMessage = Message.TDSMessage.CreateFromFirstPacket(tdsPacket);
                    else
                        tdsMessage.Packets.Add(tdsPacket);

                    if ((header.StatusBitMask & StatusBitMask.END_OF_MESSAGE) == StatusBitMask.END_OF_MESSAGE)
                    {
                        SQLBatchMessage bm = tdsMessage as SQLBatchMessage;

                        if (bm != null)
                        {
                            var query = bm.GetBatchText();
                            
                            if (query.StartsWith("select", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_cache.TryGetValue(query, out List<ServerResponse> cacheMessage))
                                {
                                    Console.WriteLine("Returned cache copy");

                                    foreach (var item in cacheMessage)
                                    {
                                        SocketCouple.ClientBridgeSocket.Send(item.Payload, item.Received, SocketFlags.None);
                                    }

                                    
                                    
                                    continue;
                                }
                                else
                                {
                                    _messages.Enqueue(tdsMessage);
                                }
                            }
                        }
                        
                        
                        OnTDSMessageReceived(tdsMessage);
                        tdsMessage = null;
                    }

                    Console.WriteLine("Send header to server");
                    SocketCouple.BridgeSQLSocket.Send(bHeader, bHeader.Length, SocketFlags.None);

                    if (header.Type == (HeaderType)23)
                    {
                        SocketCouple.BridgeSQLSocket.Send(bBuffer, iReceived, SocketFlags.None);
                    }
                    else
                    {
                        Console.WriteLine("Send data header to server");
                        SocketCouple.BridgeSQLSocket.Send(bBuffer, header.PayloadSize, SocketFlags.None);
                    }

                    

                    //sc.OutputSocket.Send(bBuffer, header.LengthIncludingHeader, SocketFlags.None);
                    //sc.OutputSocket.Send(bBuffer, iReceived, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                OnBridgeException(ConnectionType.ClientBridge, e);
            }

            OnConnectionDisconnected(ConnectionType.ClientBridge);
            //Console.WriteLine("Closing InputThread");
        }

        protected virtual void BridgeSQLThread()
        {
            try
            {
                byte[] bBuffer = new byte[4096];
                int iReceived = 0;

                List<ServerResponse> tempMessageBugffer = new List<ServerResponse>();

                while ((iReceived = SocketCouple.BridgeSQLSocket.Receive(bBuffer, SocketFlags.None)) > 0)
                {
                    Header.TDSHeader header = new Header.TDSHeader(bBuffer);
                    
                    if (_messages.TryDequeue(out TDSMessage msg))
                    {
                        if (bBuffer[1] == 1)
                        {
                            // Add last packet
                            tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));
                            
                            // Store in cache
                            var bm = msg as SQLBatchMessage;
                            var query = bm.GetBatchText();
                            if (_cache.TryGetValue(query, out List<ServerResponse> cachedData))
                            {
                                tempMessageBugffer = (cachedData);
                            }
                            else
                            {
                                _cache.Set(query, new List<ServerResponse> (tempMessageBugffer));
                            }

                            foreach (var item in tempMessageBugffer)
                            {
                                SocketCouple.ClientBridgeSocket.Send(item.Payload, item.Received, SocketFlags.None);
                            }

                            tempMessageBugffer.Clear();
                            
                            //SocketCouple.ClientBridgeSocket.Send(tempMessageBugffer[0].Payload, tempMessageBugffer[0].Received, SocketFlags.None);
                        }
                        else
                        {
                            _messages.Enqueue(msg);
                            tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));
                            //SocketCouple.ClientBridgeSocket.Send(bBuffer, iReceived, SocketFlags.None);
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Send {iReceived}");
                        SocketCouple.ClientBridgeSocket.Send(bBuffer, iReceived, SocketFlags.None);
                    }

                    //if (header.Type == HeaderType.TabularResult || header.Type == HeaderType.SQLBatch)
                    //{
                    //    if (iReceived >= 4096)
                    //    {
                    //        tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));
                    //        continue;
                    //    }

                    //    if (tempMessageBugffer.Count > 0)
                    //    {
                    //        tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));
                    //    }


                    //    _messages.TryDequeue(out TDSMessage msg);

                    //    if (msg is SQLBatchMessage)
                    //    {
                    //        SQLBatchMessage bm = (SQLBatchMessage)msg;
                    //        var query = bm.GetBatchText();

                    //        if (query.StartsWith("select", StringComparison.OrdinalIgnoreCase))
                    //        {

                    //            if (_cache.TryGetValue(query, out _))
                    //            {
                    //                //Console.WriteLine("Returned cache copy");
                    //                continue;
                    //            }
                    //            else
                    //            {
                    //                Console.WriteLine("Returned real");
                    //                //_cache.Set(query, new CachedMessage(tempMessageBugffer));

                    //            }
                    //        }
                    //    }


                    //    if (tempMessageBugffer.Count > 0)
                    //    {
                    //        foreach (var packet in tempMessageBugffer)
                    //        {
                    //            SocketCouple.ClientBridgeSocket.Send(packet.Payload, packet.Received, SocketFlags.None);
                    //        }

                    //        tempMessageBugffer.Clear();
                    //    }
                    //    else
                    //    {
                    //        SocketCouple.ClientBridgeSocket.Send(bBuffer, iReceived, SocketFlags.None);
                    //    }
                    //}
                    //else
                    //{
                    //    SocketCouple.ClientBridgeSocket.Send(bBuffer, iReceived, SocketFlags.None);
                    //}

                }
            }
            catch (Exception e)
            {
                OnBridgeException(ConnectionType.BridgeSQL, e);
            }

            OnConnectionDisconnected(ConnectionType.BridgeSQL);
            //Console.WriteLine("Closing OutputThread");
        }


        #region Event handlers
        protected virtual void OnTDSMessageReceived(Message.TDSMessage msg)
        {
            BridgeAcceptor.OnTDSMessageReceived(this, msg);
        }

        protected virtual void OnTDSPacketReceived(Packet.TDSPacket packet)
        {
            BridgeAcceptor.OnTDSMessageReceived(this, packet);
        }

        protected virtual void OnBridgeException(ConnectionType ct, Exception exce)
        {
            BridgeAcceptor.OnBridgeException(this, ct, exce);
        }

        protected virtual void OnConnectionDisconnected(ConnectionType ct)
        {
            BridgeAcceptor.OnConnectionDisconnected(this, ct);

            switch (ct)
            {
                case ConnectionType.ClientBridge:
                    if(SocketCouple.BridgeSQLSocket.Connected)
                        SocketCouple.BridgeSQLSocket.Disconnect(false);
                    break;
                case ConnectionType.BridgeSQL:
                    if (SocketCouple.ClientBridgeSocket.Connected)                        
                        SocketCouple.ClientBridgeSocket.Disconnect(false);
                    break;
            }
        }
        #endregion
    }

    class ServerResponse
    {
        public readonly int Received;
        public readonly byte[] Payload = new byte[4096];

        public ServerResponse(int received, byte[] payload)
        {
            Received = received;
            Array.Copy(payload, Payload, payload.Length);
        }
    }

    class CachedMessage
    {
        public readonly List<byte[]> Payload;

        public CachedMessage(List<byte[]> payload)
        {
            Payload = payload;
        }
    }
}
