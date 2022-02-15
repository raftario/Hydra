using Hydra.WebSocket13;
using System.IO;

namespace Hydra
{
    public abstract class WebSocketMessage
    {
        public WebSocketOpcode Opcode { get; }
        public Stream Body { get; }

        protected WebSocketMessage(WebSocketOpcode opcode, Stream body)
        {
            Opcode = opcode;
            Body = body;
        }
    }
}
