using Hydra.WebSocket13;
using System.IO;

namespace Hydra
{
    public class WebSocketCloseMessage : WebSocketMessage
    {
        public ushort Status { get; }

        internal WebSocketCloseMessage(ushort status, Stream body) : base(WebSocketOpcode.Close, body)
        {
            Status = status;
        }
    }
}
