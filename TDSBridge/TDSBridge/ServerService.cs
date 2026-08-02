using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TDSBridge.Common;
using TDSBridge.Common.Cache;
using TDSBridge.Common.Middleware;

namespace TDSBridge;

public class ServerService : BackgroundService
{
    readonly ILogger<ServerService> _logger;
    readonly IHostApplicationLifetime _applicationLifetime;
    readonly ServerSettings _serverSettings;
    readonly IServiceProvider _serviceProvider;
    
    public ServerService(
        IHostApplicationLifetime appLifetime,
        ILogger<ServerService> logger,
        IOptions<ServerSettings> settings,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _applicationLifetime = appLifetime;
        _serverSettings = settings.Value;
    }

    static int iRPC = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            System.Net.IPHostEntry iphe = System.Net.Dns.GetHostEntry(_serverSettings.ServerAddress);

            BridgeAcceptor b = new BridgeAcceptor(
                _serverSettings.ListeningPort,
                new System.Net.IPEndPoint(iphe.AddressList[0], _serverSettings.ServerPort),
                _serviceProvider
            );

            b.TDSMessageReceived += b_TDSMessageReceived;
            b.TDSPacketReceived += b_TDSPacketReceived;
            b.ConnectionAccepted += b_ConnectionAccepted;
            b.ConnectionDisconnected += b_ConnectionClosed;

            var builder = new MiddlewarePipelineBuilder<MessageContext>();

            
            b.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(500);
            }

            b.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception: {Ex}", ex.ToString());
        }

    }

    void b_ConnectionClosed(object sender, BridgedConnection bc, ConnectionType ct)
    {
        _logger.LogInformation("{DateTime}|Connection {Type} closed ({Couple})", DateTime.Now.FormatDateTime(), ct,
            bc.SocketCouple);
    }

    void b_ConnectionAccepted(object sender, System.Net.Sockets.Socket sAccepted)
    {
        _logger.LogInformation("{DateTime}|New connection from {EndPoint}", DateTime.Now.FormatDateTime(),
            sAccepted.RemoteEndPoint);
    }

    void b_TDSPacketReceived(object sender, BridgedConnection bc, Common.Packet.TDSPacket packet)
    {
        _logger.LogInformation("{DateTime}|{Packet}", DateTime.Now.FormatDateTime(), packet);
    }

    void b_TDSMessageReceived(object sender, BridgedConnection bc, Common.Message.TDSMessage msg)
    {
        _logger.LogInformation("{DateTime}|{Msg}", DateTime.Now.FormatDateTime(), msg);
        if (msg is Common.Message.SQLBatchMessage)
        {
            Common.Message.SQLBatchMessage b = (Common.Message.SQLBatchMessage)msg;
            string strBatchText = b.GetBatchText();
            
            _logger.LogInformation("\tSQLBatch message\n({Length} chars worth of {Size} bytes of data)[{Data}]", strBatchText.Length,
                strBatchText.Length * 2,
                strBatchText);
        }
        else if (msg is Common.Message.RPCRequestMessage)
        {
            try
            {
                Common.Message.RPCRequestMessage rpc = (Common.Message.RPCRequestMessage)msg;
                byte[] bPayload = rpc.AssemblePayload();

#if DEBUG
                //using (System.IO.FileStream fs = new System.IO.FileStream(
                //    "C:\\temp\\dev\\" + (iRPC++) + ".raw",
                //    System.IO.FileMode.Create,
                //    System.IO.FileAccess.Write,
                //    System.IO.FileShare.Read))
                //{
                //    fs.Write(bPayload, 0, bPayload.Length);
                //}
#endif

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception");
            }
        }

    }
}