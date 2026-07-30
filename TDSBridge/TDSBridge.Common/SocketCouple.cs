using System.Net.Sockets;

namespace TDSBridge.Common
{
    /// <summary>
    /// Couples client and server connections
    /// </summary>
    public class SocketCouple
    {
        public Socket? ClientBridgeSocket { get; set; }
        public Socket? BridgeSQLSocket { get; set; }

        public override string ToString()
        {
            try
            {
                return base.ToString() + "[ClientBridgeSocket.RemoteEndPoint=" + ClientBridgeSocket.RemoteEndPoint + ", BridgeSQLSocket.RemoteEndPoint=" + BridgeSQLSocket.RemoteEndPoint + "]";
            }
            catch
            {
                return base.ToString() + "[ClientBridgeSocket=" + ClientBridgeSocket + ", BridgeSQLSocket=" + BridgeSQLSocket + "]";
            }
        }
    }
}
