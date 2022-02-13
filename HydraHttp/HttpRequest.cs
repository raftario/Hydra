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
    public enum HttpVersion
    {
        Http10,
        Http11,
    }

    public class HttpRequest
    {
        internal readonly Socket socket;
        internal readonly HttpReader reader;

        public string Method { get; set; }
        public string Uri { get; set; }
        public HttpVersion Version { get; }
        public HttpHeaders Headers { get; }
        public Stream Body { get; set; }
        public CancellationToken CancellationToken { get; }

        public EndPoint? Remote => socket.RemoteEndPoint;
        public ProtocolType Protocol => socket.ProtocolType;

        internal HttpRequest(
            string method,
            string uri,
            int version,
            HttpHeaders headers,
            Stream body,
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
            CancellationToken = cancellationToken;

            this.socket = socket;
            this.reader = reader;
        }

        public class TransferEncodingAndContentLengthhException : HttpBadRequestException { }
        public class InvalidContentLengthException : HttpBadRequestException { }
        public class UnknownBodyLengthException : HttpBadRequestException { }
    }

    public static class ReaderExtensions
    {
        public static async ValueTask<HttpRequest?> ReadRequest(this HttpReader reader, Socket socket, CancellationToken cancellationToken = default)
        {
            var startLineResult = await reader.ReadStartLine(cancellationToken);
            if (!startLineResult.Complete(out var startLine)) return null;

            var headers = new HttpHeaders();
            if (!await reader.ReadHeaders(headers, cancellationToken)) return null;

            if (headers.ContainsKey("Transfer-Encoding") && headers.ContainsKey("Content-Length")) throw new HttpRequest.TransferEncodingAndContentLengthhException();

            Stream body;
            
            if (headers.TryGetValue("Transfer-Encoding", out var te) && te.ToString().EndsWith("chunked", StringComparison.OrdinalIgnoreCase))
            {
                body = new HttpChunkedBodyStream(reader.Reader);
            }
            else if (headers.TryGetValue("Content-Length", out var cl))
            {
                if (cl.Count == 1 && !int.TryParse(cl, out var length)) throw new HttpRequest.InvalidContentLengthException();

                var distinct = cl.Distinct().ToArray();
                if (distinct.Length != 1 || !int.TryParse(distinct[0], out length)) throw new HttpRequest.InvalidContentLengthException();

                body = new HttpSizedBodyStream(reader.Body, length);
            }
            else throw new HttpRequest.UnknownBodyLengthException();

            return new HttpRequest(
                startLine.Value.Method,
                startLine.Value.Uri,
                startLine.Value.Version,
                headers,
                body,
                socket,
                reader,
                cancellationToken);
        }

        public static async ValueTask<bool> ReadHeaders(this AbstractReader reader, HttpHeaders headers, CancellationToken cancellationToken = default)
        {
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
                else if (headerResult.Incomplete) return false;
                else if (headerResult.Finished) return true;
            }
        }
    }
}
