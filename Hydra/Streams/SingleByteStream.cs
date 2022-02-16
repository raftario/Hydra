using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    internal class SingleByteStream : ReadOnlyStream
    {
        private readonly byte b;
        private bool done = false;

        public SingleByteStream(byte b)
        {
            this.b = b;
        }

        public override long Length => 1;

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) throw new ArgumentOutOfRangeException(nameof(buffer));
            if (done) return 0;
            buffer[0] = b;
            done = true;
            return 1;
        }
        public override int ReadByte() => done ? -1 : b;

        public override int Read(byte[] buffer, int offset, int count) => Read(new(buffer, offset, count));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromResult(Read(new(buffer, offset, count)));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(Read(buffer.Span));

    }
}
