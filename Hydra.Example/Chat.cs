using System.Collections.Concurrent;
using System.Net;

namespace Hydra.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> Chat(HttpRequest request) => Task.FromResult(WebSocket.Response(request, Example.Chat.Handler));
    }

    internal static class Chat
    {
        internal static readonly ConcurrentDictionary<EndPoint, WebSocket> clients = new();

        internal static async Task Handler(WebSocket socket)
        {
            var remote = socket.Remote;
            if (remote is null)
            {
                await socket.Close();
                return;
            }
            clients[remote] = socket;

            while (true)
            {
                var message = await socket.Receive();
                if (message is null) break;
                if (message is not WebSocketTextMessage chatMessage) continue;

                string chatMessageContents = await chatMessage.Body.ReadToEndAsync();
                await Task.WhenAll(clients.Values.Select(
                    (c) => c.Send(new WebSocketTextMessage(chatMessageContents))));
            }

            clients.TryRemove(remote, out _);
            await socket.Close();
        }
    }
}
