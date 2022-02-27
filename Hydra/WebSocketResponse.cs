using System;
using System.Security.Cryptography;
using System.Text;

namespace Hydra
{
    public class WebSocketResponse : HttpResponse
    {
        private const string WebSocketMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        
        internal Server.WebSocketHandler handler;

        public WebSocketResponse(string clientKey, Server.WebSocketHandler handler) : base(101)
        {
            this.handler = handler;

            byte[] acceptKeyBytes = SHA1.HashData(Encoding.UTF8.GetBytes(clientKey + WebSocketMagic));
            
            Headers["Upgrade"] = "websocket";
            Headers["Connection"] = "Upgrade";
            Headers["Sec-WebSocket-Accept"] =Convert.ToBase64String(acceptKeyBytes);
        }
    }
}
