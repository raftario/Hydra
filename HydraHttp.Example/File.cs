namespace HydraHttp.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> File(HttpRequest request)
        {
            var body = new FileStream("LICENSE", FileMode.Open);
            var response = new HttpResponse(200, body);
            return Task.FromResult(response);
        }
    }
}
