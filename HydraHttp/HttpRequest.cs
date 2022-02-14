using HydraHttp.OneDotOne;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    /// <summary>
    /// An HTTP protocol version
    /// </summary>
    public enum HttpVersion
    {
        /// <summary>
        /// HTTP/1.0
        /// </summary>
        Http10,
        /// <summary>
        /// HTTP/1.1
        /// </summary>
        Http11,
    }

    /// <summary>
    /// An HTTP request
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// Underlying socket the request originated from
        /// </summary>
        internal readonly Socket socket;
        internal readonly HttpReader reader;

        /// <summary>
        /// Request method
        /// </summary>
        public string Method { get; set; }
        /// <summary>
        /// Request URI
        /// 
        /// Contains the absolute path and query
        /// </summary>
        public string Uri { get; set; }
        /// <summary>
        /// Request protocol version
        /// </summary>
        public HttpVersion Version { get; }
        /// <summary>
        /// Request headers
        /// </summary>
        public HttpHeaders Headers { get; }
        /// <summary>
        /// Request body
        /// </summary>
        public HttpBodyStream Body { get; set; }
        /// <summary>
        /// Request body encoding
        /// 
        /// The Transfer-Encoding and Content-Encoding headers should be left intact
        /// and users should instead push and pop from this stack to indicate the
        /// current encoding layers of the body
        /// </summary>
        public Stack<string> Encoding { get; }

        /// <summary>
        /// Endpoint of the client the request originated from
        /// 
        /// In most cases an <see cref="IPEndPoint"/> but can be a <see cref="UnixDomainSocketEndPoint"/>
        /// if the server is listening on a Unix socket
        /// </summary>
        public EndPoint? Remote => socket.RemoteEndPoint;

        /// <summary>
        /// Cancellation token passed to the server at startup if any
        /// </summary>
        public CancellationToken CancellationToken { get; }

        internal HttpRequest(
            string method,
            string uri,
            int version,
            HttpHeaders headers,
            HttpBodyStream body,
            Stack<string> encoding,
            Socket socket,
            HttpReader reader,
            CancellationToken cancellationToken)
        {
            Method = method;
            Uri = uri;
            Version = version switch
            {
                0 => HttpVersion.Http10,
                1 => HttpVersion.Http11,
                _ => throw new NotSupportedException(),
            };
            Headers = headers;
            Body = body;
            Encoding = encoding;
            CancellationToken = cancellationToken;

            this.socket = socket;
            this.reader = reader;
        }

        /// <summary>
        /// An exception thrown by the server if a request has both
        /// Transfer-Encoding and Content-Length headers
        /// 
        /// The spec recommends rejecting such requests because they have a
        /// high chance of being spoofed.
        /// </summary>
        public class TransferEncodingAndContentLengthException : HttpBadRequestException { }
        /// <summary>
        /// An exception thrown by the server if a request has an invalid
        /// Content-Length header
        /// </summary>
        public class InvalidContentLengthException : HttpBadRequestException { }
        /// <summary>
        /// An exception thrown by the server if a request has an invalid
        /// or missing Host header
        /// </summary>
        public class InvalidHostException : HttpBadRequestException { }
        /// <summary>
        /// An exception thrown by the server if it can't determine the length of a request body.
        /// </summary>
        public class UnknownBodyLengthException : HttpBadRequestException { }
    }

    public static class ReaderExtensions
    {
        /// <summary>
        /// Attempts to read a request from the underlying connection into a structured class
        /// </summary>
        /// <param name="socket">Underlying socket the request originated from</param>
        /// <returns>An instance of <see cref="HttpRequest"/> or null if the connection is closed before receiving a full request</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async ValueTask<HttpRequest?> ReadRequest(this HttpReader reader, Socket socket, CancellationToken cancellationToken = default)
        {
            // read the start line
            var startLineResult = await reader.ReadStartLine(cancellationToken);
            if (!startLineResult.Complete(out var startLine)) return null;

            // read the headers
            var headers = new HttpHeaders();
            if (!await reader.ReadHeaders(headers, cancellationToken)) return null;

            // HTTP/1.1 requests must contain a Host header
            if (startLine.Value.Version == 1 && (!headers.TryGetValue("Host", out var host) || host.Count > 1)) throw new HttpRequest.InvalidHostException();

            // requests containing both Transfer-Encoding and Content-Length headers are invalid and should be rejected
            if (headers.ContainsKey("Transfer-Encoding") && headers.ContainsKey("Content-Length")) throw new HttpRequest.TransferEncodingAndContentLengthException();

            HttpBodyStream body;
            var encoding = new Stack<string>();

            // push content encodings to our stack first if any
            if (headers.TryGetValue("Content-Encoding", out var ce))
            {
                foreach (string value in ce) encoding.Push(value);
            }
            
            if (headers.TryGetValue("Transfer-Encoding", out var te))
            {
                // push transfer encodings to our stack
                foreach (string value in te) encoding.Push(value);

                // if we have transfer encodings the outermost one must be chunk or the body isn't readable
                if (encoding.TryPeek(out string? e) && e.Equals("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    body = new HttpChunkedBodyStream(reader.Reader);
                    encoding.Pop();
                }
                else throw new HttpRequest.UnknownBodyLengthException();
            }
            else if (headers.TryGetValue("Content-Length", out var cl))
            {
                int length;

                // there is one content length value but it's not readable as an integer
                if (cl.Count == 1)
                {
                    if (!int.TryParse(cl, out length)) throw new HttpRequest.InvalidContentLengthException();
                }
                else
                {
                    // if there are many content length values they must all be identical and readable as integers
                    string[] distinct = cl.Distinct().ToArray();
                    if (distinct.Length != 1 || !int.TryParse(distinct[0], out length)) throw new HttpRequest.InvalidContentLengthException();
                }

                body = new HttpSizedBodyStream(reader.Body, length);
            }
            // if a request has neither Transfer-Encoding nor Content-Length headers it is assumed to have an empty body
            else body = HttpEmptyBodyStream.Body;

            return new HttpRequest(
                startLine.Value.Method,
                startLine.Value.Uri,
                startLine.Value.Version,
                headers,
                body,
                encoding,
                socket,
                reader,
                cancellationToken);
        }

        /// <summary>
        /// Attempts to read a collection of headers from the underlying connection into the provided instance
        /// </summary>
        /// <param name="headers">Instance to write the read headers to</param>
        /// <returns>true on success or false if the connection is closed before receiving the full headers</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async ValueTask<bool> ReadHeaders(this AbstractReader reader, HttpHeaders headers, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var headerResult = await reader.ReadHeader(cancellationToken);
                if (headerResult.Complete(out var header))
                {
                    string name = header.Value.Name;
                    string[] values = header.Value.Value.Split(',').Select((s) => s.Trim()).ToArray();
                    headers.Add(name, values);
                }
                else if (headerResult.Incomplete) return false;
                else if (headerResult.Finished) return true;
            }
        }
    }
}
