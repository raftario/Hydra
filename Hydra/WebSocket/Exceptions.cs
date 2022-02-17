using Hydra.WebSocket13;

namespace Hydra
{
    public class LoneContinuationFrameException : WebSocketInvalidFrameException { }
    public class NonFrameableMessageFramedException : WebSocketInvalidFrameException { }
    public class UnmaskedBodyException : WebSocketInvalidFrameException { }
}
