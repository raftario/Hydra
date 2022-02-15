using Hydra.Http11;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    /// <summary>
    /// A wrapper around another stream which decodes its chunked contents and makes them readable in their original encoding
    /// 
    /// This class is not responsible for disposing of the wrapped stream
    /// </summary>
    public class HttpChunkedBodyStream : ReadOnlyStream
    {
        private readonly ChunkedReader reader;
        private readonly HttpHeaders headers;

        /// <summary>
        /// Length of the current chunk
        /// </summary>
        private int currentChunkLength = 0;
        /// <summary>
        /// Bytes of the current chunk which have been read
        /// </summary>
        private int i = 0;

        internal HttpChunkedBodyStream(PipeReader reader, HttpHeaders headers)
        {
            this.reader = new(reader);
            this.headers = headers;
        }

        /// <summary>
        /// Wraps the given stream for decoding
        /// </summary>
        /// <param name="stream">Chunk encoded stream to wrap</param>
        /// <param name="headers">Instance to parse optional trailing headers into</param>
        public HttpChunkedBodyStream(Stream stream, HttpHeaders headers)
        {
            reader = new(PipeReader.Create(stream));
            this.headers = headers;
        }

        public override bool CanRead => true;

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
                if (!nextChunk.Complete(out int? nextChunkLength)) return 0;
                currentChunkLength = nextChunkLength.Value;
                i = 0;

                // we reached the end, just gotta parse the optional trailing headers
                if (currentChunkLength == 0)
                {
                    await reader.ReadHeaders(headers, cancellationToken);
                    return 0;
                }
            }

            // read a less or equal amount of bytes than what's left in the current chunk

            var result = await reader.Reader.ReadAsync(cancellationToken);
            int length = Math.Min(Math.Min(currentChunkLength - i, buffer.Length), (int) result.Buffer.Length);

            result.Buffer.Slice(0, length).CopyTo(buffer.Span[..length]);
            reader.Reader.AdvanceTo(result.Buffer.GetPosition(length));

            i += length;
            return length;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(new(buffer, offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(Span<byte> buffer) => throw new NotSupportedException();
    }
}
