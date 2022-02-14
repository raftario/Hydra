using System;

namespace HydraHttp.OneDotOne
{
    /// <summary>
    /// Family of exceptions which result in a 400 Bad Request response
    /// </summary>
    public abstract class HttpBadRequestException : Exception { }
    /// <summary>
    /// Family of exceptions which result in a 415 URI Too Long response
    /// </summary>
    public abstract class HttpUriTooLongException : Exception { }
    /// <summary>
    /// Family of exceptions which result in a 501 Not Implemented response
    /// </summary>
    public abstract class HttpNotImplementedException : Exception { }

    /// <summary>
    /// An exception thrown when a chunk size is longer than the reader is willing to parse
    /// </summary>
    public class ChunkSizeTooLongException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when a signel header is longer than the reader is willing to parse
    /// </summary>
    public class HeaderTooLongException : HttpNotImplementedException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a chunk extension
    /// </summary>
    public class InvalidChunkExtensionException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a header name
    /// </summary>
    public class InvalidHeaderNameException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a header value
    /// </summary>
    public class InvalidHeaderValueException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a hexadecimal number
    /// </summary>
    public class InvalidHexNumberException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when a carriage return is not followed by a newline
    /// </summary>
    public class InvalidNewlineException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a token
    /// </summary>
    public class InvalidTokenException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in a URI
    /// </summary>
    public class InvalidUriException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when invalid characters are present in the protocol version string
    /// </summary>
    public class InvalidVersionException : HttpBadRequestException { }
    /// <summary>
    /// An exception thrown when the start line is longer than the reader is willing to parse
    /// </summary>
    public class StartLineTooLongException : HttpUriTooLongException { }
    /// <summary>
    /// An exception thrown when the listed protocol version is not supported by the parser
    /// </summary>
    public class UnsupportedVersionException : HttpNotImplementedException { }
}
