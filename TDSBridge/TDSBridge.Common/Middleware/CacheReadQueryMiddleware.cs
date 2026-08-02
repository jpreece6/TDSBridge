using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TDSBridge.Common.Cache;
using TDSBridge.Common.Message;
using TDSBridge.Common.Utils;

namespace TDSBridge.Common.Middleware;

public class CacheReadQueryMiddleware : IMiddleware<MessageContext>
{
    readonly ILogger<CacheReadQueryMiddleware> _logger;
    readonly ICache _cache;

    public CacheReadQueryMiddleware(ILogger<CacheReadQueryMiddleware> logger, ICache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        _logger.LogInformation("Checking cache for query match");
        
        SQLBatchMessage bm = context.TdsMessage as SQLBatchMessage;

        if (bm != null)
        {
            string query = bm.GetBatchText();

            // Select statements we want to cache the server returned result to we need to
            // store this message so we can link client query to result
            if (query.StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                int qHash = query.GetDeterministicHashCode();
                if (_cache.TryGetValue($"{qHash}", out QueryRecord queryRecord))
                {
                    int sum = 0;
                    foreach (ServerResponse item in queryRecord.ServerResponse)
                    {
                        sum += item.Received;
                        context.SocketCouple.ClientBridgeSocket.Send(item.Payload, item.Received, SocketFlags.None);
                    }

                    _logger.LogInformation($"Returned cached data {sum} bytes");
                    
                    // Stop processing
                    return;
                }
                else
                {
                    context.QueueMessages.Enqueue(context.TdsMessage);
                }
            }
            else if (query.StartsWith("insert into", StringComparison.OrdinalIgnoreCase) ||
                     query.StartsWith("update", StringComparison.OrdinalIgnoreCase) ||
                     query.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
            {
                // write operations need to be stored if the write action fails

                _logger.LogInformation("Write query detected. Ignoring");

            }
            else
            {
                // Everything else gets sent to the server
                _logger.LogInformation("Query not found in cache going to source");
            }
        }

        await next(context);
    }
}