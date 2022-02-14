using HydraHttp.Core;
using System.Globalization;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
{
    /// <summary>
    /// A reader for chunked bodies
    /// </summary>
    public class ChunkedReader : AbstractReader
    {
        /// <summary>
        /// Maximum length to process in attempt to parse a chunk length before bailing
        /// </summary>
        public int MaxChunkSizeLength = 2048;

        public ChunkedReader(PipeReader reader) : base(reader) { }

        /// <summary>
        /// Reads a chunk size
        /// </summary>
        /// <param name="prefixNewline">Whether to consume a prefixed newline before reading the length</param>
        /// <returns>
        /// <see cref="Status.Complete"/> and the chunk length on success,
        /// or <see cref="Status.Incomplete"/> if parsing cannot complete
        /// </returns>
        public async ValueTask<Result<int>> ReadChunkSize(bool prefixNewline = false, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var result = await Reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;
                var bytes = buffer.Bytes();

                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (ParseChunkSize(ref bytes, prefixNewline, out var length))
                    {
                        consumed = bytes.Position;
                        examined = consumed;

                        return new(Status.Complete, length);
                    }

                    if (buffer.Length > MaxChunkSizeLength) throw new ChunkSizeTooLongException();
                    if (result.IsCompleted) return new(Status.Incomplete);
                }
                finally
                {
                    Reader.AdvanceTo(consumed, examined);
                }
            }
        }

        /// <summary>
        /// Parses a chunk size
        /// </summary>
        /// <param name="prefixNewline">Whether to consume a prefixed newline before reading the length</param>
        /// <param name="length">Chunk length</param>
        /// <returns>false if the data is incomplete</returns>
        internal static bool ParseChunkSize(ref Bytes bytes, bool prefixNewline, out int length)
        {
            length = default;
            string? hex = null;

            if (prefixNewline && !ConsumeNewline(ref bytes)) return false;

            while (bytes.Peek(out byte b))
            {
                if (b == '\r' || b == '\n')
                {
                    if (hex is null) hex = bytes.Read().AsAscii();
                    break;
                }
                else if (b == ';')
                {
                    if (hex is null) hex = bytes.Read().AsAscii();
                    bytes.Bump();
                }
                else if (hex is null && !b.IsAsciiHexDigit()) throw new InvalidHexNumberException();
                else if (b != '=' && !b.IsAsciiToken()) throw new InvalidChunkExtensionException();
                else bytes.Bump();
            }
            if (hex is null) return false;

            length = int.Parse(hex, NumberStyles.HexNumber);
            return ConsumeNewline(ref bytes);
        }
    }
}
