using System;
using System.Buffers;
using System.Text;

namespace HydraHttp.Core
{
    internal struct Bytes
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;
        private SequencePosition nextPosition;
        private ReadOnlyMemory<byte> memory;
        private int index = 0;

        internal Bytes(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            nextPosition = sequence.Start;
            position = nextPosition;
            sequence.TryGet(ref nextPosition, out memory);
            return;
        }

        internal SequencePosition Position => sequence.GetPosition(index, position);

        internal bool Peek(out byte peek)
        {
            if (index < memory.Length)
            {
                peek = memory.Span[index];
                return true;
            }

            var tmp = nextPosition;
            if (!sequence.TryGet(ref nextPosition, out memory))
            {
                peek = default;
                return false;
            }
            position = tmp;

            index = 0;
            peek = memory.Span[index];
            return true;
        }

        internal void Bump() => index++;

        internal bool Next(out byte next)
        {
            var peeked = Peek(out next);
            if (peeked) Bump();
            return peeked;
        }

        internal ReadOnlySequence<byte> Rest() => sequence.Slice(Position);
        internal ReadOnlySequence<byte> Consumed(int offset = 0) => sequence.Slice(0, sequence.GetPosition(index + offset, position));
    }

    internal static class ByteExtensions
    {
        internal static Bytes ByteWalker(this ReadOnlySequence<byte> sequence) => new(sequence);

        internal static string AsAscii(this ReadOnlySequence<byte> sequence)
        {
            if (sequence.IsSingleSegment) return Encoding.ASCII.GetString(sequence.FirstSpan);

            var sb = new StringBuilder((int) sequence.Length);
            foreach (var memory in sequence) sb.Append(Encoding.ASCII.GetString(memory.Span));
            return sb.ToString();
        }
    }
}
