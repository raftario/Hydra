using HydraHttp;
using HydraHttp.Example;

var handlerName = args.Length >= 1 ? args[0] : null;
Server.HttpHandler handler = handlerName?.ToLower() switch
{
    "echo" => Handlers.Echo,
    "headers" => Handlers.Headers,
    "file" => Handlers.File,
    _ => Handlers.Hello,
};

var hostname = args.Length >= 2 ? args[1] : "localhost";
var port = args.Length >= 3 ? int.Parse(args[2]) : 8080;

Console.WriteLine($"Starting server at `http://{hostname}:{port}`");

using var server = await Server.At(hostname, port, async (req) =>
{
    var res = await handler(req);
    Console.WriteLine($"[{req.Remote}]: {req.Method} {req.Uri} => {res.Status}");
    return res;
});
await server.Run();
