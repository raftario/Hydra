using System.Text;

namespace Hydra.Example
{
    public static partial class Handlers
    {
        public static async Task<HttpResponse> Headers(HttpRequest request)
        {
            await request.ReadHeaders();

            var body = new MemoryStream();
            foreach (var (name, value) in request.Headers)
                body.Write(Encoding.UTF8.GetBytes($"{name}: {value}\n"));
            body.Position = 0;

            var response = new HttpResponse(200, body);
            response.Headers["Content-Length"] = body.Length.ToString();
            response.Headers["Content-Type"] = "text/plain; encoding=utf-8";

            return response;
        }
    }
}
