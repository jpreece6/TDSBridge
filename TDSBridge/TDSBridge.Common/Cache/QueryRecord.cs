namespace TDSBridge.Common.Cache;

public class QueryRecord
{
    public string ClientId { get; set; }
    public List<ServerResponse> ServerResponse { get; set; }
    public DateTime QueryDate { get; set; }
}