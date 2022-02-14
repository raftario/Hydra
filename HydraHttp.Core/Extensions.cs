using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace HydraHttp.Core
{
    public static class ReadOnlyByteSequenceExtensions
    {
        /// <summary>
        /// Returns an instance of <see cref="Bytes"/> for this sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Bytes Bytes(this ReadOnlySequence<byte> sequence) => new(sequence);

        /// <summary>
        /// Decodes the contents of this sequence as ASCII
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string AsAscii(this ReadOnlySequence<byte> sequence)
        {
            if (sequence.IsSingleSegment) return Encoding.ASCII.GetString(sequence.FirstSpan);

            var sb = new StringBuilder((int)sequence.Length);
            foreach (var memory in sequence) sb.Append(Encoding.ASCII.GetString(memory.Span));
            return sb.ToString();
        }
    }
}
