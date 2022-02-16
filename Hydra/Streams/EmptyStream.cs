using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// An empty stream which is always done reading and never returns any data
    /// </summary>
    internal class EmptyStream : ReadOnlyStream
    {
        /// <summary>
        /// Shared empty stream
        /// </summary>
        internal static readonly Stream Stream = new EmptyStream();
        private EmptyStream() { }

        public override long Length => 0;

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override int Read(Span<byte> buffer) => 0;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
    }
}
