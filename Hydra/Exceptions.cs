using Hydra.Http11;
using Hydra.WebSocket13;

namespace Hydra
{
    /// <summary>
    /// An exception thrown by the server if a request has both
    /// Transfer-Encoding and Content-Length headers
    /// 
    /// The spec recommends rejecting such requests because they have a
    /// high chance of being spoofed.
    /// </summary>
    public class TransferEncodingAndContentLengthException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown by the server if a request has an invalid
    /// Content-Length header
    /// </summary>
    public class InvalidContentLengthException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown by the server if a request has an invalid
    /// or missing Host header
    /// </summary>
    public class InvalidHostException : HttpBadRequestException { }
    public class LoneContinuationFrameException : WebSocketInvalidFrameException { }
    public class NonFrameableMessageFramedException : WebSocketInvalidFrameException { }
    /// <summary>
    /// An exception thrown by the server if it can't determine the length of a request body.
    /// </summary>
    public class UnknownBodyLengthException : HttpBadRequestException { }
    public class UnmaskedBodyException : WebSocketInvalidFrameException { }
}
