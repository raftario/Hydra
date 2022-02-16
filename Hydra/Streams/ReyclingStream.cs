using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Streams
{
    internal class ReyclingStream : WrapperStream
    {
        private readonly ConcurrentQueue<IAsyncDisposable> trashcan;

        internal ReyclingStream(Stream stream, ConcurrentQueue<IAsyncDisposable> trashcan) : base(stream)
        {
            this.trashcan = trashcan;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            if (read == 0) trashcan.Enqueue(this);
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(new(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer, cancellationToken);
            if (read == 0) trashcan.Enqueue(this);
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) =>
            ReadAsync(new(buffer, offset, count), cancellationToken).AsTask();
    }
}
