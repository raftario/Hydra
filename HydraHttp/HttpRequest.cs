using HydraHttp.OneDotOne;
using Microsoft.Extensions.Primitives;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    public class HttpRequest
    {
        private Socket socket;

        public string Method { get; set; }
        public string Uri { get; set; }
        public HttpHeaders Headers { get; }
        public Stream Body { get; set; }
        public CancellationToken CancellationToken { get; }

        public EndPoint? Client => socket.RemoteEndPoint;
        public EndPoint? Server => socket.RemoteEndPoint;
        public ProtocolType Protocol => socket.ProtocolType;

        internal HttpRequest(string method, string uri, HttpHeaders headers, Stream body, Socket socket, CancellationToken cancellationToken)
        {
            Method = method;
            Uri = uri;
            Headers = headers;
            Body = body;
            CancellationToken = cancellationToken;
            this.socket = socket;
        }
    }

    public static class HttpReaderExtensions
    {
        public static async ValueTask<HttpRequest?> ReadRequest(this HttpReader reader, Socket socket, CancellationToken cancellationToken = default)
        {
            var startLineResult = await reader.ReadStartLine(cancellationToken);
            if (!startLineResult.Complete(out var startLine)) return null;

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
                else if (headerResult.Incomplete) return null;
                else if (headerResult.Finished) break;
            }

            Stream body = reader.Body;
            if (headers.TryGetValue("Content-Length", out var cls) && int.TryParse(cls, out var cl)) body = new HttpBodyStream(body, cl);
            else if (startLine.Value.Method == "TRACE") body = new HttpEmptyBodyStream();

            return new HttpRequest(startLine.Value.Method, startLine.Value.Uri, headers, body, socket, cancellationToken);
        }
    }
}
