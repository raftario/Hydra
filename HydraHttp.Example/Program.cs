using HydraHttp;
using System.Text;

var server = await HttpServer.At("localhost", 8080, (req) =>
{
    var s = $"[{req.Client}] {req.Method} {req.Uri}";
    Console.WriteLine(s);

    Stream body = req.Uri switch
    {
        "/echo" or "/echo/" => req.Body,
        _ => new MemoryStream(Encoding.UTF8.GetBytes(s))
    };

    var response = new HttpResponse(200, body);
    response.Headers["Content-Length"] = body.Length.ToString();
    return Task.FromResult(response);
});

await server.Run();
