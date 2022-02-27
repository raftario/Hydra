using Hydra.Http11;
using System;

namespace Hydra
{
    public class HttpMethodNotAllowedException : Exception { }
    public class HttpUpgradeRequiredException : Exception
    {
        public string Upgrade { get; }

        public HttpUpgradeRequiredException(string upgrade) : base()
        {
            Upgrade = upgrade;
        }
    }

    public class InvalidWebSocketKeyException : HttpBadRequestException { }
    public class UnsupportedVersionException : HttpBadRequestException { }

    public class NonGetWebSocketRequestException : HttpMethodNotAllowedException { }

    public class HttpWebSocketUpgradeRequiredException : HttpUpgradeRequiredException
    {
        public HttpWebSocketUpgradeRequiredException() : base("websocket") { }
    }

    public class InvalidWebSocketUpgradeException : HttpWebSocketUpgradeRequiredException { }
    public class InvalidWebSocketVersionException : HttpWebSocketUpgradeRequiredException { }
}
