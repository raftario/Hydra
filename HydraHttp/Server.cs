using HydraHttp.OneDotOne;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    public class Server : IDisposable
    {
        public delegate Task<HttpResponse> Handler(HttpRequest request);

        private Socket listener;
        private Handler handler;
        private X509Certificate2? cert;

        public Server(Socket listener, Handler handler)
        {
            this.listener = listener;
            this.handler = handler;
        }

        public Server(IPEndPoint endpoint, Handler handler)
        {
            var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endpoint);
            listener.Listen();

            this.listener = listener;
            this.handler = handler;
        }
        public Server(IPAddress address, int port, Handler handler) : this(new IPEndPoint(address, port), handler) { }
        public static async Task<Server> At(string hostname, int port, Handler handler)
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
                ? new Server(listener, handler)
                : throw last!;
        }

        public async Task Tls(string cert, string? key = null)
        {
            var certContents = await File.ReadAllTextAsync(cert);
            if (key is null)
            {
                this.cert = X509Certificate2.CreateFromPem(certContents);
            }
            else
            {
                var keyContents = await File.ReadAllTextAsync(key);
                this.cert = X509Certificate2.CreateFromPem(certContents, keyContents);
            }
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var client = await listener.AcceptAsync(cancellationToken);
                Task.Run(() => Client(client, cancellationToken));
            }
        }

        private async Task Client(Socket client, CancellationToken cancellationToken)
        {
            Stream stream = new NetworkStream(client, true);
            if (cert is not null)
            {
                var tlsStream = new SslStream(stream, false);
                await tlsStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = cert }, cancellationToken);
                stream = tlsStream;
            }

            var reader = PipeReader.Create(stream);
            var writer = PipeWriter.Create(stream);

            var httpReader = new HttpReader(reader);
            var httpWriter = new HttpWriter(writer);

            try
            {
                while (true)
                {
                    HttpResponse? response = null;
                    HttpRequest? request = null;

                    try
                    {
                        request = await httpReader.ReadRequest(client, cancellationToken);
                        if (request is null) return;

                        response = await handler(request);
                        if (response is null) return;
                    }
                    catch (HttpBadRequestException)
                    {
                        httpWriter.WriteStatusLine(new(400, "Bad Request"));
                        httpWriter.WriteHeader(new("Content-Length", "0"));
                        await httpWriter.Send(HttpEmptyBodyStream.Body, cancellationToken);

                        continue;
                    }
                    catch (HttpNotImplementedException)
                    {
                        httpWriter.WriteStatusLine(new(501, "Not Implemented"));
                        httpWriter.WriteHeader(new("Content-Length", "0"));
                        await httpWriter.Send(HttpEmptyBodyStream.Body, cancellationToken);

                        continue;
                    }

                    if (await httpWriter.WriteResponse(response, request.Method, cancellationToken)) return;
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                OnException(ex);
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        public class ExceptionEventArgs : EventArgs
        {
            public Exception Exception { get; }
            public ExceptionEventArgs(Exception ex) : base()
            {
                Exception = ex;
            }
        }
        public event EventHandler<ExceptionEventArgs>? Exception;
        private void OnException(Exception ex) => Exception?.Invoke(this, new(ex));
    }
}
