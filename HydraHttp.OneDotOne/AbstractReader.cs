using HydraHttp.Core;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
{
    /// <summary>
    /// An abstract reader containing fields and methods shared across specific formats
    /// </summary>
    public abstract class AbstractReader
    {
        /// <summary>
        /// Maximum length to process in attempt to parse a single header before bailing
        /// </summary>
        public int MaxHeaderLength = 8192;

        public readonly PipeReader Reader;

        public AbstractReader(PipeReader reader)
        {
            Reader = reader;
        }

        /// <summary>
        /// Reads a single header
        /// </summary>
        /// <returns>
        /// <see cref="Status.Complete"/> and the header on success,
        /// <see cref="Status.Finished"/> if there are no more headers left,
        /// or <see cref="Status.Incomplete"/> if parsing cannot complete
        /// </returns>
        public async ValueTask<Result<Header>> ReadHeader(CancellationToken cancellationToken = default)
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
                    var status = ParseHeader(ref bytes, out var name, out var value);
                    if (status != Status.Incomplete)
                    {
                        consumed = bytes.Position;
                        examined = consumed;
                    }

                    if (status == Status.Complete) return new(Status.Complete, new(name!, value!));
                    if (status == Status.Finished) return new(Status.Finished);

                    if (buffer.Length > MaxHeaderLength) throw new HeaderTooLongException();
                    if (result.IsCompleted) return new(Status.Incomplete);
                }
                finally
                {
                    Reader.AdvanceTo(consumed, examined);
                }
            }
        }

        /// <summary>
        /// Skips empty lines
        /// </summary>
        /// <returns>false if the data is incomplete</returns>
        protected static bool SkipEmptyLines(ref Bytes bytes)
        {
            while (bytes.Peek(out byte b))
            {
                if (b == '\r')
                {
                    bytes.Bump();
                    if (!bytes.Next(out b)) return false;
                    if (b != '\n') throw new InvalidNewlineException();
                }
                else if (b == '\n') bytes.Bump();
                else break;
            }

            bytes.Consume();
            return true;
        }

        /// <summary>
        /// Parses a single header
        /// </summary>
        /// <param name="name">Header name</param>
        /// <param name="value">Header value</param>
        /// <returns>false if the data is incomplete</returns>
        protected static Status ParseHeader(ref Bytes bytes, out string? name, out string? value)
        {
            name = null;
            value = null;
            byte b;

            while (bytes.Next(out b))
            {
                if (b == '\r')
                {
                    if (!bytes.Next(out b)) return Status.Incomplete;
                    else if (b != '\n') throw new InvalidNewlineException();
                    return Status.Finished;
                }
                else if (b == '\n') return Status.Finished;
                else if (b == ':')
                {
                    name = bytes.Read(-1).AsAscii();
                    break;
                }
                else if (!b.IsAsciiHeaderName()) throw new InvalidHeaderNameException();
            }
            if (name is null) return Status.Incomplete;

            while (bytes.Peek(out b))
            {
                if (b == ' ' || b == '\t') bytes.Bump();
                else break;
            }
            bytes.Consume();

            int valueEndOffset = 0;
            while (bytes.Next(out b))
            {
                if (b == '\r')
                {
                    if (!bytes.Next(out b)) return Status.Incomplete;
                    else if (b != '\n') throw new InvalidNewlineException();

                    value = bytes.Read(valueEndOffset - 2).AsAscii();
                    break;
                }
                else if (b == '\n')
                {
                    value = bytes.Read(valueEndOffset - 1).AsAscii();
                    break;
                }
                else if (b == ' ' || b == '\t') valueEndOffset--;
                else if (!b.IsAsciiHeaderValue()) throw new InvalidHeaderValueException();
                else valueEndOffset = 0;
            }
            if (value is null) return Status.Incomplete;

            return Status.Complete;
        }

        /// <summary>
        /// Consumes a single newline
        /// </summary>
        /// <returns>false if the data is incomplete</returns>
        internal static bool ConsumeNewline(ref Bytes bytes)
        {
            if (!bytes.Next(out byte b)) return false;
            if (b == '\r')
            {
                if (!bytes.Next(out b)) return false;
                else if (b != '\n') throw new InvalidNewlineException();
                return true;
            }
            else if (b != '\n') throw new InvalidNewlineException();

            bytes.Consume();
            return true;
        }
    }
}
