# Hydra

A simple streaming webserver that runs anywhere .NET 6 runs

## Why

ASP.NET Core doesn't support running on MAUI targets (Android, iOS, etc.). It's also pretty bulky and, in my opinion, extremely annoying to use for small to medium sized projects.
After trying alternatives for a project without finding anyting satisfying, a friend made a joke about writing our own. Two days of hyperfocusing later I'm writing this readme.

Unlike ASP, Hydra is very simple and barebones. I like to think it exists at an abstraction level where it's both usable as is and for building frameworks upon.
However, it doesn't support HTTP/2.0 and is nowhere near as feature packed as ASP.

Unlike most alternatives, Hydra is a fully streaming server. Request and response bodies are streams, and handling can start as soon as the headers are parsed without waiting for the full body.
Under the hood, it uses a streaming parser based on [httparse](https://github.com/seanmonstar/httparse) and is built on top of [System.IO.Pipelines](https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines).

Finally, Hydra is unsurprising. It never tries to be smart and only does the bare minimum required to follow [the spec](https://datatracker.ietf.org/doc/html/rfc7230).

## Features

- [x] Simple and lightweight
- [x] Truly cross-platform
- [x] Streaming request and response bodies
- [x] TLS encryption
- [x] Request pipelining
- [x] Chunked encoding
- [x] Unix socket support
- [ ] WebSockets (in progress)

## Usage

You can take a look at usage examples in the [example project](./HydraHttp.Example/) and run them using `dotnet run --project HydraHttp.Example -- <EXAMPLE> [<HOSTNAME>] [<PORT>]`

### Basic

```cs
using HydraHttp;
using System.Text;

using var server = await Server.At("localhost", 8080, (req) =>
{
    var hello = "Hello, Hydra!";
    var body = new MemoryStream(Encoding.UTF8.GetBytes(hello));
    var response = new HttpResponse(200, body);
    return Task.FromResult(response);
});
await server.Run();
```

### TLS

```cs
using var server = await Server.At(hostname, port, handler);
await server.Tls("./tls/cert.pem", "./tls/key.rsa");
await server.Run();
```

### Unix

```cs
var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
socket.Bind(new UnixDomainSocketEndPoint(path));
socket.Listen();

using var server = new Server(socket, handler);
await server.Run();
```
