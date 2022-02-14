using HydraHttp.OneDotOne;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp
{
    /// <summary>
    /// A wrapper around another stream which decodes its chunked contents and makes them readable in their original encoding
    /// </summary>
    public class HttpChunkedBodyStream : HttpBodyStream
    {
        private readonly ChunkedReader reader;
        /// <summary>
        /// Length of the current chunk
        /// </summary>
        private int currentChunkLength = 0;
        /// <summary>
        /// Bytes of the current chunk which have been read
        /// </summary>
        private int i = 0;

        internal HttpChunkedBodyStream(PipeReader reader)
        {
            this.reader = new(reader);
        }
        /// <summary>
        /// Wraps the given stream for decoding
        /// </summary>
        /// <param name="stream">Chunk encoded stream to wrap</param>
        public HttpChunkedBodyStream(Stream stream)
        {
            reader = new(PipeReader.Create(stream));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // we need to read the next chunk
            if (currentChunkLength - i == 0)
            {
                // i can only be 0 if this is a fresh stream,
                // which is the only case where we don't want to parse the prefix newline
                var nextChunk = await reader.ReadChunkSize(i > 0, cancellationToken);
                if (!nextChunk.Complete(out var nextChunkLength)) return 0;
                currentChunkLength = nextChunkLength.Value;
                i = 0;

                // we reached the end, just gotta parse the optional trailing headers
                if (currentChunkLength == 0)
                {
                    await reader.ReadHeaders(Headers, cancellationToken);
                    return 0;
                }
            }

            // read a less or equal amount of bytes than what's left in the current chunk

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
