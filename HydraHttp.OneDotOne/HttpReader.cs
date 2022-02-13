using HydraHttp.Core;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
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

        public async ValueTask<Result<StartLine>> ReadStartLine(CancellationToken cancellationToken = default)
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

        public async ValueTask<Result<Header>> ReadHeader(CancellationToken cancellationToken = default)
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
            if (!ParseToken(ref bytes, out method)) return false;
            if (!ParseUri(ref bytes, out uri)) return false;
            if (!ParseVersion(ref bytes, out version)) return false;

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
                else break;
            }

            bytes.Consume();
            return true;
        }

        internal static bool ParseToken(ref Bytes bytes, [NotNullWhen(true)] out string? token)
        {
            while (bytes.Next(out byte b))
            {
                if (b == ' ')
                {
                    token = bytes.Read(-1).AsAscii();
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
                    uri = bytes.Read(-1).AsAscii();
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

        public abstract class BadRequestException : Exception { }
        public abstract class NotImplementedException : Exception { }

        public class InvalidNewlineException : BadRequestException { }
        public class InvalidTokenException : BadRequestException { }
        public class InvalidUriException : BadRequestException { }
        public class InvalidVersionException : BadRequestException { }
        public class UnsupportedVersionException : NotImplementedException { }
        public class InvalidHeaderNameException : BadRequestException { }
        public class InvalidHeaderValueException : BadRequestException { }
        public class StartLineTooLongException : NotImplementedException { }
        public class HeaderTooLongException : NotImplementedException { }
    }
}
