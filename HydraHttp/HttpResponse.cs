using HydraHttp.OneDotOne;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    public class HttpResponse
    {
        public int Status { get; set; }
        public string Reason { get; set; }
        public HttpHeaders Headers { get; } = new();
        public Stream? Body { get; set; }

        public HttpResponse(int status, Stream? body = null, string reason = "")
        {
            Status = status;
            Reason = reason;
            Body = body;
        }
    }

    public static class WriterExtensions
    {
        public static async ValueTask<bool> WriteResponse(this HttpWriter writer, HttpResponse response, string requestMethod, CancellationToken cancellationToken = default)
        {
            var needsClose = response.Headers.TryGetValue("Transfer-Encoding", out var te)
                && !te.ToString().TrimEnd().EndsWith("chunked", StringComparison.OrdinalIgnoreCase);
            needsClose = needsClose || !response.Headers.ContainsKey("Content-Length");

            if (requestMethod == "HEAD") response.Body = HttpEmptyBodyStream.Body;
            else if (requestMethod == "CONNECT" && response.Status >= 200 && response.Status < 300) response.Body = HttpEmptyBodyStream.Body;
            else if ((response.Status >= 100 && response.Status < 200) || response.Status == 204 || response.Status == 304) response.Body = HttpEmptyBodyStream.Body;

            writer.WriteStatusLine(new(response.Status, response.Reason));
            foreach (var (name, values) in response.Headers) writer.WriteHeader(new(name, values));
            await writer.Send(response.Body ?? HttpEmptyBodyStream.Body, cancellationToken);

            return needsClose;
        }
    }
}