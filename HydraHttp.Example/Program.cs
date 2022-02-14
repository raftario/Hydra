using HydraHttp;
using HydraHttp.Example;

string? handlerName = args.Length >= 1 ? args[0] : null;
Server.HttpHandler handler = handlerName?.ToLower() switch
{
    "echo" => Handlers.Echo,
    "headers" => Handlers.Headers,
    "file" => Handlers.File,
    "cats" => Handlers.Cats,
    _ => Handlers.Hello,
};

string hostname = args.Length >= 2 ? args[1] : "localhost";
var port = args.Length >= 3 ? int.Parse(args[2]) : 8080;

Console.WriteLine($"Starting server at `http://{hostname}:{port}`");

using var server = await Server.At(hostname, port, async (req) =>
{
    var res = await handler(req);
    Console.WriteLine($"[{req.Remote}]: {req.Method} {req.Uri} => {res.Status}");
    return res;
});
server.Exception += (s, e) => Console.Error.WriteLine(e.Exception);
await server.Run();
