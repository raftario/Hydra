using HydraHttp;
using System.Text;

var server = await HttpServer.At("localhost", 8080, (req) =>
{
    var s = $"[{req.Client}] {req.Method} {req.Uri}";
    Console.WriteLine(s);

    HttpResponse response;

    if (req.Uri.StartsWith("/echo"))
    {
        if (!req.Headers.TryGetValue("Content-Length", out var cls) || !int.TryParse(cls, out var length)) length = 0;
        var body = new CappedReadStream(req.Body, length);
        response = new HttpResponse(200, "OK", body);
        response.Headers["Content-Length"] = length.ToString();
        if (req.Headers.TryGetValue("Content-Type", out var ct)) response.Headers["Content-Type"] = ct;
    }
    else
    {
        var body = Encoding.UTF8.GetBytes(s);
        var length = body.Length;
        response = new HttpResponse(200, "OK", new MemoryStream(body));
        response.Headers["Content-Length"] = length.ToString();
    }

    return Task.FromResult(response);
});

await server.Run();
