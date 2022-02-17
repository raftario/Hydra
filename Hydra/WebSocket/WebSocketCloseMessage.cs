namespace Hydra
{
    public readonly record struct WebSocketCloseMessage(ushort? Code = null, string? Reason = null)
    {
        public static implicit operator WebSocketCloseMessage(ushort code) => new(code);
    }
}
