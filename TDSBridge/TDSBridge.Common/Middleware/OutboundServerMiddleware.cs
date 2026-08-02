using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TDSBridge.Common.Header;

namespace TDSBridge.Common.Middleware;

public class OutboundServerMiddleware : IMiddleware<MessageContext>
{
    readonly ILogger<OutboundServerMiddleware> _logger;
    
    public OutboundServerMiddleware(ILogger<OutboundServerMiddleware> logger)
    {
        _logger = logger;
    }
    
    public Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        try
        {
            var bHeader = context.HeaderPayload;
            var header = context.TdsHeader;
            var bBuffer = context.Payload;
            var iReceived = context.Received;
            
            context.SocketCouple.BridgeSQLSocket.Send(bHeader, bHeader.Length, SocketFlags.None);

            if (header.Type == (HeaderType)23)
            {
                context.SocketCouple.BridgeSQLSocket.Send(bBuffer, iReceived, SocketFlags.None);
                
                _logger.LogInformation($"Sent server (type 23) {iReceived} bytes");
            }
            else
            {
                context.SocketCouple.BridgeSQLSocket.Send(bBuffer, header.PayloadSize, SocketFlags.None);
                
                _logger.LogInformation($"Sent server {header.PayloadSize} bytes");
            }
        }
        catch (Exception ex)
        {
            // TODO save inserts
            _logger.LogError(ex, "Error sending data to server");

            throw;
        }

        return Task.CompletedTask;
    }
}