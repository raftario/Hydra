using System.Text;

namespace HydraHttp.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> Headers(HttpRequest request)
        {
            var body = new MemoryStream();
            foreach (var (name, value) in request.Headers)
                body.Write(Encoding.UTF8.GetBytes($"{name}: {value}\n"));
            body.Position = 0;

            var response = new HttpResponse(200, body);
            return Task.FromResult(response);
        }
    }
}
