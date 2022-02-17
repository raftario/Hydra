namespace Hydra.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> File(HttpRequest request)
        {
            var body = new FileStream("LICENSE", FileMode.Open);

            var response = new HttpResponse(200, body);
            response.Headers["Content-Length"] = body.Length.ToString();
            response.Headers["Content-Type"] = "text/plain; encoding=utf-8";

            return ValueTask.FromResult(response);
        }
    }
}
