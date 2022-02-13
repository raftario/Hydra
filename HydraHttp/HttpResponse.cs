using System.IO;

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
}
