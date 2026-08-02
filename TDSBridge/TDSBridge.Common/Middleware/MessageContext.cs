using System.Collections.Concurrent;
using TDSBridge.Common.Message;

namespace TDSBridge.Common.Middleware;

/// <summary>
/// The "HttpContext" equivalent for this background service — carries
/// everything a middleware might need for one unit of work.
/// </summary>
public class MessageContext : IHasServiceProvider
{
    public required string MessageId { get; init; }
    public IServiceProvider Services { get; init; } = default!;
    public CancellationToken CancellationToken { get; init; }
    
    public SocketCouple SocketCouple { get; set; }
    public TDSMessage TdsMessage { get; set; }
    public Header.TDSHeader TdsHeader { get; set; }
    public byte[] Payload { get; set; } = new byte[4096];
    public int Received { get; set; }
    public byte[] HeaderPayload { get; set; } = new byte[8];
    public List<ServerResponse> ServerMessages { get; set; }
    public ConcurrentQueue<TDSMessage>  QueueMessages { get; set; }
    public ConnectionType  ConnectionType { get; set; }
}
