using HydraHttp.OneDotOne;
using System;
using System.Buffers;
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
    /// <summary>
    /// A mid-level HTTP/1.1 and WebSocket server suitable for building abstractions or using directly
    /// </summary>
    public class Server : IDisposable
    {
        /// <summary>
        /// An HTTP request handler
        /// </summary>
        /// <param name="request">The received HTTP request</param>
        /// <returns>The HTTP response to send back</returns>
        public delegate Task<HttpResponse> HttpHandler(HttpRequest request);

        /// <summary>
        /// Listener used to accept clients
        /// </summary>
        private readonly Socket listener;
        /// <summary>
        /// Handler used for HTTP requests
        /// </summary>
        private readonly HttpHandler httpHandler;
        /// <summary>
        /// Optional certificated used to encrypt connections with TLS
        /// </summary>
        private X509Certificate2? cert;

        /// <summary>
        /// Returns a new server which will use the provided listener and handler
        /// </summary>
        /// <param name="listener">Client listener, must be a bound and listening stream socket</param>
        /// <param name="handler">HTTP request handler</param>
        public Server(Socket listener, HttpHandler handler)
        {
            this.listener = listener;
            httpHandler = handler;
        }

        /// <summary>
        /// Returns a new server which will listen for TCP connection on the given endpoint
        /// and use the given handler
        /// </summary>
        /// <param name="endpoint">IP endpoint</param>
        /// <param name="handler">HTTP request handler</param>
        public Server(IPEndPoint endpoint, HttpHandler handler)
        {
            var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endpoint);
            listener.Listen();

            this.listener = listener;
            httpHandler = handler;
        }
        /// <summary>
        /// Returns a new server which will listen for TCP connection on the given address and port
        /// and use the given handler
        /// </summary>
        /// <param name="address">IP address</param>
        /// <param name="port">Port</param>
        /// <param name="handler">HTTP request handler</param>
        public Server(IPAddress address, int port, HttpHandler handler) : this(new IPEndPoint(address, port), handler) { }

        /// <summary>
        /// Returns a new server which will listen for TCP connection on the first bindable address
        /// the given hostname resolves to and on the given port and use the given handler
        /// </summary>
        /// <param name="hostname">Hostname</param>
        /// <param name="port">Port</param>
        /// <param name="handler">HTTP request handler</param>
        public static async Task<Server> At(string hostname, int port, HttpHandler handler)
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

        /// <summary>
        /// Enables TLS on the server using the given certificate
        /// </summary>
        /// <param name="cert">Path to the certificate file</param>
        /// <param name="key">Path to the private key file, if required</param>
        public async Task Tls(string cert, string? key = null)
        {
            string certContents = await File.ReadAllTextAsync(cert);
            if (key is null)
            {
                this.cert = X509Certificate2.CreateFromPem(certContents);
            }
            else
            {
                string keyContents = await File.ReadAllTextAsync(key);
                this.cert = X509Certificate2.CreateFromPem(certContents, keyContents);
            }
        }

        /// <summary>
        /// Runs the server
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token used to stop the server</param>
        /// <returns>A task which will complete once the server is closed using the cancellation token</returns>
        public async Task Run(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var client = await listener.AcceptAsync(cancellationToken);
                _ = Task.Run(() => Client(client, cancellationToken), cancellationToken);
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

                        response = await httpHandler(request);
                        if (response is null) return;
                    }
                    catch (HttpBadRequestException)
                    {
                        httpWriter.WriteStatusLine(new(400, "Bad Request"));
                        httpWriter.WriteHeader(new("Content-Length", "0"));
                        await httpWriter.Send(HttpEmptyBodyStream.Body, cancellationToken);

                        continue;
                    }
                    catch (HttpUriTooLongException)
                    {
                        httpWriter.WriteStatusLine(new(415, "URI Too Long"));
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

                    try
                    {
                        // returns true if we need to close
                        if (await httpWriter.WriteResponse(response, request, cancellationToken)) return;
                        // need to make sure the whole body has been read before parsing the next request
                        await Drain(request.Body, cancellationToken);
                    }
                    finally
                    {
                        if (response.Body is not null) await response.Body.DisposeAsync();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnException(ex);
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        private static async ValueTask Drain(Stream stream, CancellationToken cancellationToken)
        {
            var pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(4096);
            int read = 1;

            try
            {
                while (read > 0) read = await stream.ReadAsync(buffer, cancellationToken);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        public class ExceptionEventArgs : EventArgs
        {
            /// <summary>
            /// Exception that was thrown
            /// </summary>
            public Exception Exception { get; }
            internal ExceptionEventArgs(Exception ex) : base()
            {
                Exception = ex;
            }
        }
        /// <summary>
        /// Event raised when an exception is thrown in a connection task
        /// </summary>
        public event EventHandler<ExceptionEventArgs>? Exception;
        private void OnException(Exception ex) => Exception?.Invoke(this, new(ex));
    }
}
