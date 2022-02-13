using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    internal class HttpEmptyBodyStream : Stream
    {
        internal HttpEmptyBodyStream() { }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override int Read(Span<byte> buffer) => 0;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);


        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
