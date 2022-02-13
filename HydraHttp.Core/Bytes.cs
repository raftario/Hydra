using System;
using System.Buffers;
using System.Text;

namespace HydraHttp.Core
{
    public struct Bytes
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition currentPosition;
        private SequencePosition nextPosition;
        private SequencePosition consumedPosition;
        private ReadOnlyMemory<byte> memory;
        private int index = 0;

        public Bytes(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            nextPosition = sequence.Start;
            currentPosition = nextPosition;
            consumedPosition = currentPosition;
            sequence.TryGet(ref nextPosition, out memory);
            return;
        }

        public SequencePosition Position => sequence.GetPosition(index, currentPosition);

        public bool Peek(out byte peek)
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
            currentPosition = tmp;

            index = 0;
            peek = memory.Span[index];
            return true;
        }

        public void Bump() => index++;

        public bool Next(out byte next)
        {
            var peeked = Peek(out next);
            if (peeked) Bump();
            return peeked;
        }

        public ReadOnlySequence<byte> Rest() => sequence.Slice(Position);
        public ReadOnlySequence<byte> Read(int offset = 0, int toConsumeOffset = 0)
        {
            var read = sequence.Slice(consumedPosition, sequence.GetPosition(index + offset, currentPosition));
            Consume(toConsumeOffset);
            return read;
        }
        public void Consume(int offset = 0)
        {
            consumedPosition = sequence.GetPosition(index + offset, currentPosition);
        }
    }
}
