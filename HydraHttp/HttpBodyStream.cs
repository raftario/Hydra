using System;
using System.IO;

namespace HydraHttp
{
    public abstract class HttpBodyStream : Stream
    {
        public HttpHeaders Headers { get; } = new();

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
