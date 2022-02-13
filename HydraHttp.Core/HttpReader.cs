using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.Core
{
    public class HttpReader
    {
        public enum Status
        {
            Complete,
            Incomplete,
            Finished,
        }
        public readonly record struct Result<T>(Status Status, T? Value = null) where T: struct
        {
            public bool Complete([NotNullWhen(true)] out T? value)
            {
                value = Value;
                return Status == Status.Complete;
            }
            public bool Incomplete => Status == Status.Incomplete;
            public bool Finished => Status == Status.Finished;
        }

        public int MaxStartLineLength = 8192;
        public int MaxHeaderLength = 8192;

        private PipeReader reader;

        private ReadResult result;
        private ReadOnlySequence<byte> buffer;
        private Bytes bytes;

        private SequencePosition consumed;
        private SequencePosition examined;

        public HttpReader(Stream stream)
        {
            reader = PipeReader.Create(stream);
        }

        public Stream Body => reader.AsStream();

        public async Task<Result<StartLine>> ReadStartLine(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                result = await reader.ReadAsync(cancellationToken);
                buffer = result.Buffer;
                bytes = buffer.ByteWalker();

                consumed = buffer.Start;
                examined = buffer.End;

                try
                {
                    if (ParseStartLine(ref bytes, out var method, out var uri, out var version))
                    {
                        consumed = bytes.Position;
                        examined = consumed;

                        return new(Status.Complete, new(method!, uri!, version));
                    }

                    if (buffer.Length > MaxStartLineLength) throw new StartLineTooLongException();
                    if (result.IsCompleted) return new(Status.Incomplete);
                }
                finally
                {
                    reader.AdvanceTo(consumed, examined);
                }
            }
        }

        public async Task<Result<Header>> ReadHeader(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                result = await reader.ReadAsync(cancellationToken);
                buffer = result.Buffer;
                bytes = buffer.ByteWalker();

                consumed = buffer.Start;
                examined = buffer.End;

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
                    reader.AdvanceTo(consumed, examined);
                }
            }
        }

        internal static bool ParseStartLine(ref Bytes bytes, out string? method, out string? uri, out int version)
        {
            method = null;
            uri = null;
            version = default;

            if (!SkipEmptyLines(ref bytes)) return false;
            bytes = bytes.Rest().ByteWalker();

            if (!ParseToken(ref bytes, out method)) return false;
            bytes = bytes.Rest().ByteWalker();

            if (!ParseUri(ref bytes, out uri)) return false;
            bytes = bytes.Rest().ByteWalker();

            if (!ParseVersion(ref bytes, out version)) return false;
            bytes = bytes.Rest().ByteWalker();

            return ParseNewLine(ref bytes);
        }

        internal static bool SkipEmptyLines(ref Bytes bytes)
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
                else return true;
            }
            return true;
        }

        internal static bool ParseToken(ref Bytes bytes, [NotNullWhen(true)] out string? token)
        {
            while (bytes.Next(out byte b))
            {
                if (b == ' ')
                {
                    token = bytes.Consumed(-1).AsText();
                    return true;
                }
                else if (!b.IsAsciiToken()) throw new InvalidTokenException();
            }

            token = null;
            return false;
        }

        internal static bool ParseUri(ref Bytes bytes, [NotNullWhen(true)] out string? uri)
        {
            while (bytes.Next(out byte b))
            {
                if (b == ' ')
                {
                    uri = bytes.Consumed(-1).AsText();
                    return true;
                }
                else if (!b.IsAsciiUri()) throw new InvalidUriException();
            }

            uri = null;
            return false;
        }

        internal static bool ParseVersion(ref Bytes bytes, out int version)
        {
            version = default;
            byte b;

            if (!bytes.Next(out b)) return false; if (b != 'H') throw new InvalidVersionException();
            if (!bytes.Next(out b)) return false; if (b != 'T') throw new InvalidVersionException();
            if (!bytes.Next(out b)) return false; if (b != 'T') throw new InvalidVersionException();
            if (!bytes.Next(out b)) return false; if (b != 'P') throw new InvalidVersionException();
            if (!bytes.Next(out b)) return false; if (b != '/') throw new InvalidVersionException();
            if (!bytes.Next(out b)) return false; if (b != '1') throw new UnsupportedVersionException();
            if (!bytes.Next(out b)) return false; if (b != '.') throw new UnsupportedVersionException();

            if (!bytes.Next(out b)) return false;
            if (b == '0') version = 0;
            else if (b == '1') version = 1;
            else throw new UnsupportedVersionException();

            return true;
        }

        internal static bool ParseNewLine(ref Bytes bytes)
        {
            if (!bytes.Next(out byte b)) return false;
            if (b == '\r')
            {
                if (!bytes.Next(out b)) return false;
                else if (b != '\n') throw new InvalidNewlineException();
                return true;
            }
            else if (b != '\n') throw new InvalidNewlineException();
            return true;
        }

        internal static Status ParseHeader(ref Bytes bytes, out string? name, out string? value)
        {
            name = null;
            value = null;
            ReadOnlySequence<byte>? valueBytes = null;
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
                    name = bytes.Consumed(-1).AsText();
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

            var valueStart = bytes.Position;
            int valueEndOffset = 0;
            while (bytes.Next(out b))
            {
                if (b == '\r')
                {
                    if (!bytes.Next(out b)) return Status.Incomplete;
                    else if (b != '\n') throw new InvalidNewlineException();
                    valueBytes = bytes.Consumed(valueEndOffset - 2);
                    break;
                }
                else if (b == '\n')
                {
                    valueBytes = bytes.Consumed(valueEndOffset - 1);
                    break;
                }
                else if (b == ' ' || b == '\t') valueEndOffset--;
                else if (!b.IsAsciiHeaderValue()) throw new InvalidHeaderValueException();
                else valueEndOffset = 0;
            }
            if (valueBytes is null) return Status.Incomplete;

            value = valueBytes.Value.Slice(valueStart).AsText();
            return Status.Complete;
        }

        public class InvalidNewlineException : Exception { }
        public class InvalidTokenException : Exception { }
        public class InvalidUriException : Exception { }
        public class InvalidVersionException : Exception { }
        public class UnsupportedVersionException : Exception { }
        public class InvalidHeaderNameException : Exception { }
        public class InvalidHeaderValueException : Exception { }
        public class StartLineTooLongException : Exception { }
        public class HeaderTooLongException : Exception { }
    }
}
