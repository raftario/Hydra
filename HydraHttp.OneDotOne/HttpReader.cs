using HydraHttp.Core;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
{
    /// <summary>
    /// A reader for HTTP/1.1 requests
    /// </summary>
    public class HttpReader : AbstractReader
    {
        /// <summary>
        /// Maximum length to process in attempt to parse the start line before bailing
        /// </summary>
        public int MaxStartLineLength = 8192;
        public Stream Body => Reader.AsStream();

        public HttpReader(PipeReader reader) : base(reader) { }

        /// <summary>
        /// Reads the start line
        /// </summary>
        /// <returns>
        /// <see cref="Status.Complete"/> and the start line on success,
        /// or <see cref="Status.Incomplete"/> if parsing cannot complete
        /// </returns>
        public async ValueTask<Result<StartLine>> ReadStartLine(CancellationToken cancellationToken = default)
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
                    if (ParseStartLine(ref bytes, out string? method, out string? uri, out int version))
                    {
                        consumed = bytes.Position;
                        examined = consumed;

                        return new(Status.Complete, new(method, uri, version));
                    }

                    if (buffer.Length > MaxStartLineLength) throw new StartLineTooLongException();
                    if (result.IsCompleted) return new(Status.Incomplete);
                }
                finally
                {
                    Reader.AdvanceTo(consumed, examined);
                }
            }
        }

        /// <summary>
        /// Parses the start line
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="uri">URI</param>
        /// <param name="version">Minor version</param>
        /// <returns>false if the data is incomplete</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static bool ParseStartLine(ref Bytes bytes, [NotNullWhen(true)] out string? method, [NotNullWhen(true)] out string? uri, out int version)
        {
            method = null;
            uri = null;
            version = default;

            if (!SkipEmptyLines(ref bytes)) return false;
            if (!ParseToken(ref bytes, out method)) return false;
            if (!ParseUri(ref bytes, out uri)) return false;
            if (!ParseVersion(ref bytes, out version)) return false;

            return ConsumeNewline(ref bytes);
        }

        /// <summary>
        /// Parses a single token
        /// </summary>
        /// <param name="token">Token</param>
        /// <returns>false if the data is incomplete</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        /// <summary>
        /// Parses the URI
        /// </summary>
        /// <param name="uri">URI</param>
        /// <returns>false if the data is incomplete</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        /// <summary>
        /// Parses the version
        /// </summary>
        /// <param name="version">Minor version</param>
        /// <returns>false if the data is incomplete</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
    }
}
