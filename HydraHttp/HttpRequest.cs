using System.IO;
using System.Net;
using System.Threading;

namespace HydraHttp
{
    public class HttpRequest
    {
        public string Method { get; }
        public string Uri { get; }
        public HttpHeaders Headers { get; }
        public Stream Body { get; }
        public EndPoint? Client { get; }
        public CancellationToken CancellationToken { get; }

        internal HttpRequest(string method, string uri, HttpHeaders headers, Stream body, EndPoint? client, CancellationToken cancellationToken)
        {
            Method = method;
            Uri = uri;
            Headers = headers;
            Body = body;
            Client = client;
            CancellationToken = cancellationToken;
        }
    }
}
