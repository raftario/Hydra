using HydraHttp.OneDotOne;
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

    public static class HttpWriterExtensions
    {
        public static async ValueTask WriteResponse(this HttpWriter writer, HttpResponse response, CancellationToken cancellationToken = default)
        {
            writer.WriteStatusLine(new(response.Status, response.Reason));
            foreach (var (name, values) in response.Headers) writer.WriteHeader(new(name, string.Join(',', values)));
            await writer.Send(response.Body ?? new HttpEmptyBodyStream(), cancellationToken);
        }
    }
}