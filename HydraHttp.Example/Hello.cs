using System.Text;

namespace HydraHttp.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> Hello(HttpRequest request)
        {
            string hello = "Hello, Hydra!";
            var body = new MemoryStream(Encoding.UTF8.GetBytes(hello));
            var response = new HttpResponse(200, body);
            return Task.FromResult(response);
        }
    }
}
