using Hydra.Http11;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// An HTTP response
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// Response status code
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// Reason string for the status
        /// 
        /// Can be empty and is almost always ignored by clients
        /// </summary>
        public string Reason { get; set; }
        /// <summary>
        /// Response headers
        /// </summary>
        public HttpHeaders Headers { get; } = new();
        /// <summary>
        /// Response body
        /// </summary>
        public Stream Body { get; set; }

        /// <summary>
        /// Returns a new response
        /// </summary>
        /// <param name="status">Response status</param>
        /// <param name="body">Optional response body, empty by default</param>
        /// <param name="reason">Optional reason string, empty by default</param>
        public HttpResponse(int status, Stream? body = null, string reason = "")
        {
            Status = status;
            Reason = reason;
            Body = body ?? Stream.Null;
        }

        protected HttpResponse(HttpResponse other)
        {
            Status = other.Status;
            Reason = other.Reason;
            Headers = other.Headers;
            Body = other.Body;
        }

        /// <summary>
        /// An exception thrown by the server when the response provided to it is invalid
        /// </summary>
        public class InvalidException : Exception
        {
            /// <summary>
            /// Request that caused the invalid response
            /// </summary>
            public HttpRequest Request { get; }
            /// <summary>
            /// Invalid response
            /// </summary>
            public HttpResponse Response { get; }

            public InvalidException(string message, HttpRequest request, HttpResponse response) : base(message)
            {
                Request = request;
                Response = response;
            }
        }
    }

    public static class WriterExtensions
    {
        /// <summary>
        /// Attempts to write a structured response to the underlying connection
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="response"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true if the underlying connection needs to be closed after sending the response</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async Task<bool> WriteResponse(this HttpWriter writer, HttpResponse response, HttpRequest request, CancellationToken cancellationToken = default)
        {
            // transfer encodings are not supported by HTTP/1.0
            if (request.Version == HttpVersion.Http10 && response.Headers.ContainsKey("Transfer-Encoding"))
                throw new HttpResponse.InvalidException("`Transfer-Encoding` header sent to an HTTP/1.0 client", request, response);
            // responses with both Transfer-Encoding and Content-Length headers are illegal
            if (response.Headers.ContainsKey("Transfer-Encoding") && response.Headers.ContainsKey("Content-Length"))
                throw new HttpResponse.InvalidException("`Transfer-Encoding` and `Content-Length` headers set in the same response", request, response);

            // if this is a head response we there can't be a body
            bool noBody = request.Method == "HEAD";
            // if this is a successful connect response there can't be a body
            noBody = noBody || (request.Method == "CONNECT" && response.Status >= 200 && response.Status < 300);
            // if this is an 1xx informational response there can't be a body
            noBody = noBody || (response.Status >= 100 && response.Status < 200);
            // if this is a reponse defined as not having a body there can't be a body
            noBody = noBody || response.Status == 204 || response.Status == 304;

            // if the client is HTTP/1.0 or indicates it wants the connection to close we need to close the connection once the body is set
            bool needsClose = request.Version == HttpVersion.Http10
                || (request.Headers.TryGetValue("Connection", out var conn) && conn.ToString().Equals("close", StringComparison.OrdinalIgnoreCase));
            // if the response has transfer encodings and the last one isn't chunked the client can't know the length and need to close the connection once the body is sent
            needsClose = needsClose || (!noBody
                && response.Headers.TryGetValue("Transfer-Encoding", out var te)
                && !te.ToString().TrimEnd().EndsWith("chunked", StringComparison.OrdinalIgnoreCase));
            // if we don't have a content length the client can't know the length and need to close the connection once the body is sent
            needsClose = needsClose || (!noBody && !response.Headers.ContainsKey("Content-Length"));

            if (noBody)
            {
                // Transfer-Encoding and Content-Length are illegal on responses without a body that aren't from a HEAD request or a 304
                if (request.Method != "HEAD" && response.Status != 304)
                {
                    if (response.Headers.ContainsKey("Transfer-Encoding"))
                        throw new HttpResponse.InvalidException("`Transfer-Encoding` header present in a response without a body", request, response);
                    if (response.Headers.ContainsKey("Content-Length"))
                        throw new HttpResponse.InvalidException("`Content-Length` header present in a response without a body", request, response);
                }

                try
                {
                    if (response.Body.Length > 0) throw new HttpResponse.InvalidException("Body in a response that can't have one", request, response);
                } catch
                {
                    throw new HttpResponse.InvalidException("Body of unknown length in a response that can't have a one", request, response);
                }
            }
            if (needsClose)
            {
                if (response.Headers.TryGetValue("Connection", out conn) && !conn.ToString().Trim().Equals("close", StringComparison.OrdinalIgnoreCase))
                        throw new HttpResponse.InvalidException("`Connection` header other than `close` present in a connection that must be closed", request, response);
                
                response.Headers["Connection"] = "close";
            }

            writer.WriteStatusLine(response.Status, response.Reason);
            foreach (var (name, values) in response.Headers) writer.WriteHeader(name, values);
            await writer.Send(response.Body, cancellationToken);

            return needsClose;
        }
    }
}
