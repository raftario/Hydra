using System.Buffers;
using System.Text;

namespace HydraHttp.Core
{
    public static class ReadOnlyByteSequenceExtensions
    {
        public static Bytes ByteWalker(this ReadOnlySequence<byte> sequence) => new(sequence);

        public static string AsAscii(this ReadOnlySequence<byte> sequence)
        {
            if (sequence.IsSingleSegment) return Encoding.ASCII.GetString(sequence.FirstSpan);

            var sb = new StringBuilder((int)sequence.Length);
            foreach (var memory in sequence) sb.Append(Encoding.ASCII.GetString(memory.Span));
            return sb.ToString();
        }
    }
}
