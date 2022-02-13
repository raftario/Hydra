using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
{
    public class HttpWriter
    {
        public readonly PipeWriter Writer;

        private const string version = "HTTP/1.1";

        public HttpWriter(PipeWriter writer)
        {
            Writer = writer;
        }

        public void WriteStatusLine(StatusLine statusLine)
        {
            var statusString = statusLine.Status.ToString();

            int versionIdx = 0;
            int firstSpaceIdx = versionIdx + version.Length;
            int statusIdx = firstSpaceIdx + 1;
            int secondSpaceIdx = statusIdx + statusString.Length;
            int reasonIdx = secondSpaceIdx + 1;
            int crIdx = reasonIdx + statusLine.Reason.Length;
            int lfIdx = crIdx + 1;

            int length = lfIdx + 1;
            var memory = Writer.GetSpan(length);

            Encoding.ASCII.GetBytes(version, memory[versionIdx..]);
            memory[firstSpaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(statusString, memory[statusIdx..]);
            memory[secondSpaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(statusLine.Reason, memory[reasonIdx..]);
            memory[crIdx] = (byte)'\r';
            memory[lfIdx] = (byte)'\n';

            Writer.Advance(length);
        }

        public void WriteHeader(Header header)
        {
            int nameIdx = 0;
            int colonIdx = nameIdx + header.Name.Length;
            int spaceIdx = colonIdx + 1;
            int valueIdx = spaceIdx + 1;
            int crIdx = valueIdx + header.Value.Length;
            int lfIdx = crIdx + 1;

            int length = lfIdx + 1;
            var memory = Writer.GetSpan(length);

            Encoding.ASCII.GetBytes(header.Name, memory[nameIdx..]);
            memory[colonIdx] = (byte)':';
            memory[spaceIdx] = (byte)' ';
            Encoding.ASCII.GetBytes(header.Value, memory[valueIdx..]);
            memory[crIdx] = (byte)'\r';
            memory[lfIdx] = (byte)'\n';

            Writer.Advance(length);
        }

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
