using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Hydra
{
    public class WebSocketResponse : HttpResponse
    {
        private const string WebSocketMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        
        internal Server.WebSocketHandler handler;

        public WebSocketResponse(HttpRequest request, Server.WebSocketHandler handler) : base(101)
        {
            // HTTP/1.0 doesn't support upgrades
            if (request.Version == Http11.HttpVersion.Http10)
                throw new UnsupportedVersionException();
            // client handshake must be a GET request
            if (request.Method != "GET")
                throw new NonGetWebSocketRequestException();
            // client handshake `Upgrade` header must be `websocket`
            if (!request.Headers.TryGetValue("Upgrade", out var upgrade) || !upgrade.ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase))
                throw new InvalidWebSocketUpgradeException();
            // client handshake `Upgrade` header must be `websocket`
            if (!request.Headers.TryGetValue("Connection", out var connection) || !connection.ToString().Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
                throw new InvalidWebSocketUpgradeException();
            // client handhsake websocket version must be `13`
            if (!request.Headers.Contains(new("Sec-WebSocket-Version", "13")))
                throw new InvalidWebSocketVersionException();
            // client handshake must contain a 16 bytes base64 key (will be 24 bytes when encoded)
            if (!request.Headers.TryGetValue("Sec-WebSocket-Key", out var key) || key.ToString().Length != 24)
                throw new InvalidWebSocketKeyException();

            this.handler = handler;

            byte[] hash = SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketMagic));
            string accept = Convert.ToBase64String(hash);
            
            Headers["Upgrade"] = "websocket";
            Headers["Connection"] = "Upgrade";
            Headers["Sec-WebSocket-Accept"] = accept;
        }
    }
}
