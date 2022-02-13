using HydraHttp.OneDotOne;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    public class HttpServer : IDisposable
    {
        public delegate Task<HttpResponse> Handler(HttpRequest request);

        private Socket listener;
        private Handler handler;

        public HttpServer(Socket listener, Handler handler)
        {
            this.listener = listener;
            this.handler = handler;
        }

        public HttpServer(IPEndPoint endpoint, Handler handler)
        {
            var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endpoint);
            listener.Listen();

            this.listener = listener;
            this.handler = handler;
        }
        public HttpServer(IPAddress address, int port, Handler handler) : this(new IPEndPoint(address, port), handler) { }
        public static async Task<HttpServer> At(string hostname, int port, Handler handler)
        {
            Socket? listener = null;
            SocketException? last = null;

            var entry = await Dns.GetHostEntryAsync(hostname);
            foreach (var address in entry.AddressList)
            {
                try
                {
                    listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    listener.Bind(new IPEndPoint(address, port));
                    listener.Listen();
                }
                catch (SocketException ex)
                {
                    last = ex;
                    if (listener is not null) listener.Dispose();
                }
            }

            return listener is not null
                ? new HttpServer(listener, handler)
                : throw last!;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var client = await listener.AcceptAsync(cancellationToken);
                Task.Run(() => Client(client, handler, cancellationToken));
            }
        }

        private static async Task Client(Socket client, Handler handler, CancellationToken cancellationToken)
        {
            var stream = new NetworkStream(client, true);
            var reader = new HttpReader(stream);
            var writer = new HttpWriter(stream);

            try
            {
                while (true)
                {
                    try
                    {
                        var request = await reader.ReadRequest(client, cancellationToken);
                        if (request is null) return;

                        var response = await handler(request);
                        if (response is null) return;
                        await writer.WriteResponse(response, cancellationToken);
                    }
                    catch (HttpReader.BadRequestException)
                    {
                        writer.WriteStatusLine(new(400, "Bad Request"));
                        writer.WriteHeader(new("Content-Length", "0"));
                        await writer.Send(new HttpEmptyBodyStream(), cancellationToken);
                    }
                    catch (HttpReader.NotImplementedException)
                    {
                        writer.WriteStatusLine(new(501, "Not Implemented"));
                        writer.WriteHeader(new("Content-Length", "0"));
                        await writer.Send(new HttpEmptyBodyStream(), cancellationToken);
                    }
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        public void Dispose()
        {
            listener.Dispose();
        }
    }
}
