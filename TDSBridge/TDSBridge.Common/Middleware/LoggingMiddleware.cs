using Microsoft.Extensions.Logging;
using TDSBridge.Common.Header;

namespace TDSBridge.Common.Middleware;

/// <summary>
/// Inline middleware, same pattern as app.Use(...) in ASP.NET Core.
/// Exposed as an extension method so it reads like app.UseLogging().
/// </summary>
public class LoggingMiddleware : IMiddleware<MessageContext>
{
    readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        HeaderType headerType = context.TdsHeader.Type;
        
        _logger.LogInformation($"[{DateTime.UtcNow:O}] Processing {context.MessageId} | Header = {headerType}");
        
        return next(context);
    }
}