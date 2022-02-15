using System;
using System.IO;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// A read only stream that doesn't own any resources
    /// </summary>
    public abstract class ReadOnlyStream : Stream
    {
        public override bool CanWrite => false;

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
