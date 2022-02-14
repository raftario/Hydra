using HydraHttp.OneDotOne;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    public class HttpChunkedBodyStream : HttpBodyStream
    {
        private readonly ChunkedReader reader;
        private int currentChunkLength = 0;
        private int i = -1;

        internal HttpChunkedBodyStream(PipeReader reader)
        {
            this.reader = new(reader);
        }
        public HttpChunkedBodyStream(Stream stream)
        {
            reader = new(PipeReader.Create(stream));
        }
        public HttpChunkedBodyStream(HttpRequest request)
        {
            reader = new(request.reader.Reader);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (i < 0 || currentChunkLength - i == 0)
            {
                var nextChunk = await reader.ReadChunkSize(i > 0, cancellationToken);
                if (!nextChunk.Complete(out var nextChunkLength)) return 0;
                currentChunkLength = nextChunkLength.Value;
                i = 0;

                if (currentChunkLength == 0)
                {
                    await reader.ReadHeaders(Headers, cancellationToken);
                    return 0;
                }
            }

            var result = await reader.Reader.ReadAsync(cancellationToken);
            var length = (int) Math.Min(Math.Min(currentChunkLength - i, buffer.Length), result.Buffer.Length);

            result.Buffer.Slice(0, length).CopyTo(buffer.Span[..length]);
            reader.Reader.AdvanceTo(result.Buffer.GetPosition(length));

            i += length;
            return length;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).Result;
    }
}
