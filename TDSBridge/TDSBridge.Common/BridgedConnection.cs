using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using TDSBridge.Common.Header;
using TDSBridge.Common.Message;
using TDSBridge.Common.Packet;
using TDSBridge.Common.Cache;
using TDSBridge.Common.Middleware;

namespace TDSBridge.Common
{
    public enum ConnectionType { ClientBridge, BridgeSQL };

    
    
    public class BridgedConnection
    {
        public BridgeAcceptor BridgeAcceptor { get; protected set; }
        public SocketCouple SocketCouple { get; protected set; }

        private ConcurrentQueue<TDSMessage> _messages = new();
        private TDSMessage? _lastMessage;
        
        readonly MiddlewareDelegate<MessageContext> _outboundPipline;
        readonly MiddlewareDelegate<MessageContext> _inboundPipline;

        public BridgedConnection(BridgeAcceptor BridgeAcceptor, SocketCouple SocketCouple, IServiceProvider  serviceProvider)
        {
            this.BridgeAcceptor = BridgeAcceptor;
            this.SocketCouple = SocketCouple;
            
            // Responsible for packets from client to server
            _outboundPipline = new MiddlewarePipelineBuilder<MessageContext>()
                    .UseMiddleware<LoggingMiddleware>(serviceProvider)
                    .UseMiddleware<ErrorHandlingMiddleware>(serviceProvider)
                    .UseMiddleware<CacheReadQueryMiddleware>(serviceProvider)
                    .UseMiddleware<OutboundServerMiddleware>(serviceProvider)
                    .Build();
            
            
            // Responsible for packets from server to client
            _inboundPipline = new MiddlewarePipelineBuilder<MessageContext>()
                .UseMiddleware<ErrorHandlingMiddleware>(serviceProvider)
                .UseMiddleware<CacheWriteQueryMiddleware>(serviceProvider)
                .UseMiddleware<InboundServerMiddleware>(serviceProvider)
                .Build();
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

        protected virtual async void ClientBridgeThread()
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
                        _lastMessage = tdsMessage;
                        
                        var msg = new MessageContext()
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            ConnectionType = ConnectionType.ClientBridge,
                            TdsMessage = tdsMessage,
                            TdsHeader = header,
                            SocketCouple = SocketCouple,
                            Received = iReceived,
                            QueueMessages = _messages,
                        };
                    
                        Array.Copy(bBuffer, msg.Payload, bBuffer.Length);
                        Array.Copy(bHeader, msg.HeaderPayload, bHeader.Length);
                    
                        await _outboundPipline(msg);
                        
                        OnTDSMessageReceived(tdsMessage);
                        tdsMessage = null;
                    }
                }
            }
            catch (Exception e)
            {
                OnBridgeException(ConnectionType.ClientBridge, e);
            }

            OnConnectionDisconnected(ConnectionType.ClientBridge);
            //Console.WriteLine("Closing InputThread");
        }

        protected virtual async void BridgeSQLThread()
        {
            try
            {
                byte[] bBuffer = new byte[4096];
                int iReceived = 0;

                List<ServerResponse> tempMessageBugffer = new List<ServerResponse>();

                while ((iReceived = SocketCouple.BridgeSQLSocket.Receive(bBuffer, SocketFlags.None)) > 0)
                {
                    Header.TDSHeader header = new Header.TDSHeader(bBuffer);

                    _messages.TryDequeue(out TDSMessage msg);

                    var context = new MessageContext()
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        ConnectionType =  ConnectionType.BridgeSQL,
                        TdsMessage = msg,
                        TdsHeader = header,
                        Received = iReceived,
                        SocketCouple = SocketCouple,
                        ServerMessages = tempMessageBugffer,
                        QueueMessages =  _messages
                    };
                    
                    Array.Copy(bBuffer, context.Payload, bBuffer.Length);
                    
                    await _inboundPipline(context);
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
}
