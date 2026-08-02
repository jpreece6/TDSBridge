using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace TDSBridge.Common.Middleware;

public class InboundServerMiddleware : IMiddleware<MessageContext>
{
    readonly ILogger<InboundServerMiddleware> _logger;
    
    public InboundServerMiddleware(ILogger<InboundServerMiddleware> logger)
    {
        _logger = logger;
    }
    
    public Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        try
        {
            byte[] bBuffer = context.Payload;
            int iReceived = context.Received;

            context.SocketCouple.ClientBridgeSocket.Send(bBuffer, iReceived, SocketFlags.None);
            
            _logger.LogInformation($"Returned server data {iReceived} bytes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);

            throw;
        }

        return next(context);
    }
}