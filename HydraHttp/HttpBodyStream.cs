using System;
using System.IO;
using System.Threading.Tasks;

namespace HydraHttp
{
    /// <summary>
    /// A stream representation of the body of an HTTP request or response
    /// </summary>
    public abstract class HttpBodyStream : Stream
    {
        /// <summary>
        /// Optional trailing headers
        /// </summary>
        public HttpHeaders Headers { get; } = new();

        public override bool CanWrite => false;

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
