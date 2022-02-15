namespace Hydra.WebSocket
{
    public enum WebsocketOpcode : byte
    {
        Continuation = 0x0,
        Test = 0x1,
        Binary = 0x2,
        Close = 0x8,
        Ping = 0x9,
        Pong = 0xA,
    }
}
