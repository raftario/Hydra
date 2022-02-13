using HydraHttp.Core;
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

        private CancellationTokenSource cts = new();
        private Task? task = null;

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
                }
            }

            return listener is not null
                ? new HttpServer(listener, handler)
                : throw last!;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptAsync(cancellationToken);
                    Task.Run(() => Client(client, cancellationToken));
                }
            }
            catch (TaskCanceledException) { }
        }

        public void Start()
        {
            task = Task.Run(() => Run(cts.Token));
            return;
        }
        public Task Stop()
        {
            var task = this.task;
            if (task is null) throw new InvalidOperationException("Server is not running");

            cts.Cancel();
            cts.Dispose();
            cts = new();

            this.task = null;
            return task;
        }

        private async Task Client(Socket client, CancellationToken cancellationToken = default)
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
                        var startLineResult = await reader.ReadStartLine(cancellationToken);
                        if (!startLineResult.Complete(out var startLine)) return;

                        var headers = new HttpHeaders();
                        while (true)
                        {
                            var headerResult = await reader.ReadHeader(cancellationToken);
                            if (headerResult.Complete(out var header))
                            {
                                var name = header.Value.Name;
                                var values = header.Value.Value.Split(',').Select((s) => s.Trim()).ToArray();
                                if (headers.TryGetValue(name, out var otherValues)) headers[name] = StringValues.Concat(otherValues, values);
                                else headers.Add(name, values);
                            }
                            else if (headerResult.Incomplete) return;
                            else if (headerResult.Finished) break;
                        }

                        Stream body = reader.Body;
                        if (headers.TryGetValue("Content-Length", out var cls) && int.TryParse(cls, out var cl)) body = new CappedReadStream(body, cl);

                        var request = new HttpRequest(startLine.Value.Method, startLine.Value.Uri, headers, body, client.RemoteEndPoint, cancellationToken);
                        var response = await handler(request);

                        if (response is not null)
                        {
                            writer.WriteStatusLine(new(response.Status, response.Reason));
                            foreach (var (name, values) in response.Headers) writer.WriteHeader(new(name, string.Join(',', values)));
                            await writer.Send(response.Body ?? Stream.Null, cancellationToken);
                        }
                        else break;
                    }
                    catch (HttpReader.BadRequestException)
                    {
                        writer.WriteStatusLine(new(400, "Bad Request"));
                        writer.WriteHeader(new("Content-Length", "0"));
                        await writer.Send(Stream.Null, cancellationToken);
                    }
                    catch (HttpReader.NotImplementedException)
                    {
                        writer.WriteStatusLine(new(501, "Not Implemented"));
                        writer.WriteHeader(new("Content-Length", "0"));
                        await writer.Send(Stream.Null, cancellationToken);
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
            cts.Cancel();
            cts.Dispose();
            listener.Dispose();
        }
    }
}
