using Microsoft.Extensions.Logging;

namespace TDSBridge.Common.Middleware;

/// <summary>
/// Class-based middleware, same pattern as a class implementing IMiddleware
/// in ASP.NET Core. Constructor-injected dependencies are supported.
/// </summary>
public class ErrorHandlingMiddleware : IMiddleware<MessageContext>
{
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
 
    public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger)
    {
        _logger = logger;
    }
 
    public async Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", context.MessageId);
            
            switch (context.ConnectionType)
            {
                case ConnectionType.ClientBridge:
                    if(context.SocketCouple.BridgeSQLSocket.Connected)
                        context.SocketCouple.BridgeSQLSocket.Disconnect(false);
                    break;
                case ConnectionType.BridgeSQL:
                    if (context.SocketCouple.ClientBridgeSocket.Connected)                        
                        context.SocketCouple.ClientBridgeSocket.Disconnect(false);
                    break;
            }
        }
    }
}