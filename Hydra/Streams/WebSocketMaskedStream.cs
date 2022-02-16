using Hydra.WebSocket13;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    internal class WebSocketMaskedStream : ReadOnlyStream
    {
        private readonly Stream stream;
        private readonly WebSocketMasker masker;

        internal WebSocketMaskedStream(Stream stream, uint maskingKey)
        {
            this.stream = stream;
            masker = new(maskingKey);
        }

        public override long Length => stream.Length;
        public override long Position { get => stream.Position; set => throw new NotImplementedException(); }

        public override int Read(Span<byte> buffer)
        {
            int read = stream.Read(buffer);
            masker.Mask(buffer[..read]);
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan()[offset..(offset + count)]);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            masker.Mask(buffer.Span[..read]);
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) =>
            ReadAsync(buffer.AsMemory()[offset..(offset + count)], cancellationToken).AsTask();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
                masker.Dispose();
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            masker.Dispose();
            await stream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
