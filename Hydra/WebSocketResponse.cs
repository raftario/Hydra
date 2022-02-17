namespace Hydra
{
    public class WebSocketResponse : HttpResponse
    {
        internal Server.WebSocketHandler handler;

        public WebSocketResponse(Server.WebSocketHandler handler) : base(101)
        {
            this.handler = handler;
        }
    }
}
