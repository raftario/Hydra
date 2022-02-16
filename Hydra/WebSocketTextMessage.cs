using Hydra.WebSocket13;
using System.IO;
using System.Text;

namespace Hydra
{
    public class WebSocketTextMessage : WebSocketMessage
    {
        internal WebSocketTextMessage(Stream body) : base(WebSocketOpcode.Text, body)
        {
            Body = new StreamReader(base.Body);
        }

        public WebSocketTextMessage(string body) :
            base(WebSocketOpcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(body)))
        {
            Body = new StreamReader(base.Body);
        }

        public new StreamReader Body { get; }
    }
}
