namespace Hydra.Example
{
    public static partial class Handlers
    {
        private readonly static HttpClient httpClient = new();

        public static async ValueTask<HttpResponse> Cats(HttpRequest request)
        {
            var cats = await httpClient.GetAsync("https://cataas.com/cat");
            var body = await cats.Content.ReadAsStreamAsync();

            var response = new HttpResponse(200, body);
            response.Headers["Content-Length"] = cats.Content.Headers.ContentLength?.ToString();
            response.Headers["Content-Type"] = cats.Content.Headers.ContentType?.ToString();

            return response;
        }
    }
}
