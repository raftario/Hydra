using System;

namespace HydraHttp.OneDotOne
{
    public abstract class HttpBadRequestException : Exception { }
    public abstract class HttpNotImplementedException : Exception { }

    public class ChunkSizeTooLongException : HttpBadRequestException { }
    public class HeaderTooLongException : HttpNotImplementedException { }
    public class InvalidChunkExtensionException : HttpBadRequestException { }
    public class InvalidHeaderNameException : HttpBadRequestException { }
    public class InvalidHeaderValueException : HttpBadRequestException { }
    public class InvalidHexNumberException : HttpBadRequestException { }
    public class InvalidNewlineException : HttpBadRequestException { }
    public class InvalidTokenException : HttpBadRequestException { }
    public class InvalidUriException : HttpBadRequestException { }
    public class InvalidVersionException : HttpBadRequestException { }
    public class StartLineTooLongException : HttpNotImplementedException { }
    public class UnsupportedVersionException : HttpNotImplementedException { }
}
