using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// A wrapper around a stream that limits the amount of data that can be read from it
    /// </summary>
    internal class SizedStream : WrapperStream
    {
        private readonly int length;
        private int n = 0;

        private int MaxCount(int count) => Math.Min(count, length - n);

        /// <summary>
        /// Wraps the given stream for length limitation
        /// </summary>
        /// <param name="stream">Stream to wrap</param>
        /// <param name="length">Length to limit to</param>
        internal SizedStream(Stream stream, int length, bool ownStream = false) : base(stream, ownStream)
        {
            this.length = length;
        }

        public override int Read(Span<byte> buffer)
        {
            int length = MaxCount(buffer.Length);
            if (length == 0) return 0;

            int read = base.Read(buffer[..length]);
            n += read;
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan()[offset..(offset + count)]);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int length = MaxCount(buffer.Length);
            if (length == 0) return 0;

            int read = await base.ReadAsync(buffer[..length], cancellationToken);
            n += read;
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) =>
            ReadAsync(buffer.AsMemory()[offset..(offset + count)], cancellationToken).AsTask();
    }
}
