using Hydra.WebSocket13;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Hydra
{
    public class WebSocketTextMessage : WebSocketMessage
    {
        public WebSocketTextMessage(string body) :
            base(WebSocketOpcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(body))) { }

        public Task<string> ReadBody()
        {
            var reader = new StreamReader(Body, Encoding.UTF8);
            return reader.ReadToEndAsync();
        }
    }
}
