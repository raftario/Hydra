namespace HydraHttp.OneDotOne
{
    public readonly record struct StartLine(string Method, string Uri, int Version);
    public readonly record struct StatusLine(int Status, string Reason);
    public readonly record struct Header(string Name, string Value);
}
