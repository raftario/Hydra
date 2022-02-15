using System;

namespace Hydra.WebSocket
{
    public abstract class WebSocketInvalidFrameException : Exception { }

    public class InvalidFrameLengthException : WebSocketInvalidFrameException { }
    public class InvalidOpcodeException : WebSocketInvalidFrameException { }
    public class NonZeroRsvException : WebSocketInvalidFrameException { }
}
