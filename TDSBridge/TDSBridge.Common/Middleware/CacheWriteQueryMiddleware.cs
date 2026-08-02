using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TDSBridge.Common.Cache;
using TDSBridge.Common.Message;
using TDSBridge.Common.Utils;

namespace TDSBridge.Common.Middleware;

public class CacheWriteQueryMiddleware : IMiddleware<MessageContext>
{
    readonly ILogger<CacheWriteQueryMiddleware> _logger;
    readonly ICache _cache;

    public CacheWriteQueryMiddleware(ILogger<CacheWriteQueryMiddleware> logger, ICache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public Task InvokeAsync(MessageContext context, MiddlewareDelegate<MessageContext> next)
    {
        var bBuffer = context.Payload;
        var iReceived = context.Received;
        var tempMessageBugffer = context.ServerMessages;
        TDSMessage msg = context.TdsMessage;

        if (msg != null)
        {
            if (bBuffer[1] == 1)
            {
                // Add last packet
                tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));

                // If SQL batch then we want to cache the result
                var bm = msg as SQLBatchMessage;
                var query = bm.GetBatchText();
                if (_cache.TryGetValue(query, out List<ServerResponse> cachedData))
                {
                    tempMessageBugffer = (cachedData);
                }
                else
                {
                    int qHash = query.GetDeterministicHashCode();
                    _cache.Set($"{qHash}", new QueryRecord()
                    {
                        ServerResponse = new List<ServerResponse>(tempMessageBugffer),
                        QueryDate = DateTime.UtcNow,
                    });
                }

                int sum = 0;
                foreach (var item in tempMessageBugffer)
                {
                    sum += item.Received;
                    context.SocketCouple.ClientBridgeSocket.Send(item.Payload, item.Received, SocketFlags.None);
                }
                
                _logger.LogInformation($"Sent client {sum} bytes");

                tempMessageBugffer.Clear();
                
            }
            else
            {
                context.QueueMessages.Enqueue(msg);
                tempMessageBugffer.Add(new ServerResponse(iReceived, bBuffer));
            }
            
            return Task.CompletedTask;
        }
        else
        {
            return next(context);
        }
    }
}