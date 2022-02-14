namespace HydraHttp.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> Echo(HttpRequest request)
        {
            var body = request.Body;
            var response = new HttpResponse(200, body);

            if (request.Headers.TryGetValue("Content-Length", out var contentLength)) response.Headers["Content-Length"] = contentLength;
            if (request.Headers.TryGetValue("Content-Type", out var contentType)) response.Headers["Content-Type"] = contentType;

            return Task.FromResult(response);
        }
    }
}
