namespace HydraHttp.Example
{
    public static partial class Handlers
    {
        public static Task<HttpResponse> Echo(HttpRequest request)
        {
            var body = request.Body;
            var response = new HttpResponse(200, body);
            return Task.FromResult(response);
        }
    }
}
