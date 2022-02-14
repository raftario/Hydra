using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// An empty body stream which is always done reading and never returns any data
    /// </summary>
    public class HttpEmptyBodyStream : HttpBodyStream
    {
        /// <summary>
        /// Shared empty body stream
        /// </summary>
        public static readonly HttpBodyStream Body = new HttpEmptyBodyStream();
        private HttpEmptyBodyStream() { }

        public override bool CanRead => true;
        public override bool CanSeek => false;

        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override int Read(Span<byte> buffer) => 0;
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
    }
}
