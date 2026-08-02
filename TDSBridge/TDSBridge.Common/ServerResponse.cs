namespace TDSBridge.Common;

public class ServerResponse
{
    public int Received { get; set; }
    public byte[] Payload { get; set; } = new byte[4096];

    public ServerResponse(int received, byte[] payload)
    {
        Received = received;
        Array.Copy(payload, Payload, payload.Length);
    }
}