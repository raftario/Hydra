using Hydra.WebSocket13;
using System.IO;

namespace Hydra
{
    public class WebSocketBinaryMessage : WebSocketMessage
    {
        public WebSocketBinaryMessage(Stream body) : base(WebSocketOpcode.Binary, body) { }
    }
}
