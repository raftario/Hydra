using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    internal abstract class WrapperStream : ReadOnlyStream
    {
        private readonly Stream stream;

        protected WrapperStream(Stream stream)
        {
            this.stream = stream;
        }

        public override long Length => stream.Length;
        public override long Position { get => stream.Position; set => throw new NotSupportedException(); }

        public override int Read(Span<byte> buffer) => stream.Read(buffer);
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => stream.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => stream.ReadAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing) stream.Dispose();
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
