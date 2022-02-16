using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    internal class LockedStream : WrapperStream
    {
        private readonly SemaphoreSlim lockk;

        internal LockedStream(Stream stream, SemaphoreSlim lockk) : base(stream)
        {
            this.lockk = lockk;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            if (read == 0) lockk.Release();
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan()[offset..(offset + count)]);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer, cancellationToken);
            if (read == 0) lockk.Release();
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) =>
            ReadAsync(buffer.AsMemory()[offset..(offset + count)], cancellationToken).AsTask();
    }
}
