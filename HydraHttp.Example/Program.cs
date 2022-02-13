using HydraHttp;
using System.Text;

using var server = await Server.At("localhost", 8080, (req) =>
{
    var s = $"[{req.Remote}] {req.Method} {req.Uri}";
    Console.WriteLine(s);

    Stream body = req.Uri switch
    {
        "/echo" or "/echo/" => req.Body,
        _ => new MemoryStream(Encoding.UTF8.GetBytes(s))
    };

    var response = new HttpResponse(200, body);
    return Task.FromResult(response);
});

server.Exception += (sender, e) => Console.Error.WriteLine(e.Exception);
await server.Run();
