using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Hydra.Core
{
    /// <summary>
    /// A helper for dealing with <see cref="ReadOnlySequence{byte}"/>
    /// 
    /// It has two cursors, the read cursor in front which is advanced by most operations,
    /// and the consume cursor behind which is only advances when previously read data is consumed
    /// </summary>
    public struct Bytes
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition currentPosition;
        private SequencePosition nextPosition;
        private SequencePosition consumedPosition;
        /// <summary>
        /// Current memory segment
        /// </summary>
        private ReadOnlyMemory<byte> memory;
        /// <summary>
        /// Index in the current memory segment
        /// </summary>
        private int index = 0;

        /// <summary>
        /// Returns a new instance
        /// </summary>
        /// <param name="sequence">Sequence to wrap</param>
        public Bytes(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            nextPosition = sequence.Start;
            currentPosition = nextPosition;
            consumedPosition = currentPosition;
            sequence.TryGet(ref nextPosition, out memory);
            return;
        }

        /// <summary>
        /// Current read cursor position in the sequence
        /// </summary>
        public SequencePosition Position => sequence.GetPosition(index, currentPosition);

        /// <summary>
        /// Returns the next value without advancing the read cursor
        /// </summary>
        /// <param name="peek">Next byte, if there is data left</param>
        /// <returns>true if there is data left, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool Peek(out byte peek)
        {
            // we're still in the same segment
            if (index < memory.Length)
            {
                peek = memory.Span[index];
                return true;
            }

            // need to move to the next segment
            var tmp = nextPosition;
            // we reached the end
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

        /// <summary>
        /// Advances the read cursor
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Bump() => index++;

        /// <summary>
        /// Returns the next value and advances the read cursor
        /// </summary>
        /// <param name="next">Next byte, if there is data left</param>
        /// <returns>true if there is data left, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(out byte next)
        {
            bool peeked = Peek(out next);
            if (peeked) Bump();
            return peeked;
        }

        /// <summary>
        /// Returns the data that has not yet been read
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySequence<byte> Rest() => sequence.Slice(Position);
        /// <summary>
        /// Returns the data that has been read but not consumed and advances the consume cursor
        /// </summary>
        /// <param name="offset">Offset from the read cursor the returned data should end at</param>
        /// <param name="consumeOffset">Offset from the read cursor to advance the consume cursor to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySequence<byte> Read(int offset = 0, int consumeOffset = 0)
        {
            var read = sequence.Slice(consumedPosition, sequence.GetPosition(index + offset, currentPosition));
            Consume(consumeOffset);
            return read;
        }
        /// <summary>
        /// Advances the consume cursor to the position of the read cursor
        /// </summary>
        /// <param name="offset">Offset from the read cursor to advance to</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Consume(int offset = 0)
        {
            consumedPosition = sequence.GetPosition(index + offset, currentPosition);
        }
    }
}
