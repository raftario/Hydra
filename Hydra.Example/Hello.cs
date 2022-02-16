using System.Text;

namespace Hydra.Example
{
    public static partial class Handlers
    {
        public static ValueTask<HttpResponse> Hello(HttpRequest request)
        {
            string hello = "Hello, Hydra!";
            var body = new MemoryStream(Encoding.UTF8.GetBytes(hello));

            var response = new HttpResponse(200, body);
            response.Headers["Content-Length"] = body.Length.ToString();
            response.Headers["Content-Type"] = "text/plain; encoding=utf-8";

            return ValueTask.FromResult(response);
        }
    }
}
