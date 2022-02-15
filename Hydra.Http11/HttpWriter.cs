using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Http11
{
    public class HttpWriter
    {
        public readonly PipeWriter Writer;

        private const string version = "HTTP/1.1";

        public HttpWriter(PipeWriter writer)
        {
            Writer = writer;
        }

        /// <summary>
        /// Writes the given status line
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void WriteStatusLine(int status, string reason)
        {
            string statusString = status.ToString();

            int versionIdx = 0;
            int firstSpaceIdx = versionIdx + version.Length;
            int statusIdx = firstSpaceIdx + 1;
            int secondSpaceIdx = statusIdx + statusString.Length;
            int reasonIdx = secondSpaceIdx + 1;
            int crIdx = reasonIdx + reason.Length;
            int lfIdx = crIdx + 1;

            int length = lfIdx + 1;
            var memory = Writer.GetSpan(length);

            Encoding.ASCII.GetBytes(version, memory[versionIdx..]);
            memory[firstSpaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(statusString, memory[statusIdx..]);
            memory[secondSpaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(reason, memory[reasonIdx..]);
            memory[crIdx] = (byte)'\r';
            memory[lfIdx] = (byte)'\n';

            Writer.Advance(length);
        }

        /// <summary>
        /// Writes a single given header
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void WriteHeader(string name, string value)
        {
            int nameIdx = 0;
            int colonIdx = nameIdx + name.Length;
            int spaceIdx = colonIdx + 1;
            int valueIdx = spaceIdx + 1;
            int crIdx = valueIdx + value.Length;
            int lfIdx = crIdx + 1;

            int length = lfIdx + 1;
            var memory = Writer.GetSpan(length);

            Encoding.ASCII.GetBytes(name, memory[nameIdx..]);
            memory[colonIdx] = (byte)':';
            memory[spaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(value, memory[valueIdx..]);
            memory[crIdx] = (byte)'\r';
            memory[lfIdx] = (byte)'\n';

            Writer.Advance(length);
        }

        /// <summary>
        /// Flushes the status line and headers to the underlying connection and send the given response body
        /// </summary>
        /// <returns>A task that completes once the body is fully written to the underlying connection</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask Send(Stream body, CancellationToken cancellationToken = default)
        {
            var memory = Writer.GetMemory(2);
            memory.Span[0] = (byte)'\r';
            memory.Span[1] = (byte)'\n';
            Writer.Advance(2);

            await Writer.FlushAsync(cancellationToken);
            await body.CopyToAsync(Writer, cancellationToken);
        }
    }
}
