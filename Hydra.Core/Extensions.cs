using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.Core
{
    public static class ReadOnlyByteSequenceExtensions
    {
        /// <summary>
        /// Returns an instance of <see cref="Bytes"/> for this sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public static class StructExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsRawSpan<T>(this ref T value) where T : struct =>
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));
    }

    public static class StreamExtensions
    {
        public static async ValueTask Drain(this Stream stream, CancellationToken cancellationToken = default)
        {
            var pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(4 * 1024);
            int read = 1;

            try
            {
                while (read > 0) read = await stream!.ReadAsync(buffer, cancellationToken);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public static async ValueTask<byte?> ReadByteAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            var pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(1);

            try
            {
                int read = await stream.ReadAsync(buffer.AsMemory()[..1], cancellationToken);
                return read > 0 ? buffer[0] : null;
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public static async ValueTask<bool> ReadAllAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int total = 0;
            int read = 1;

            while (total < buffer.Length && read > 0)
            {
                read = await stream.ReadAsync(buffer[total..], cancellationToken);
                total += read;
            }

            return total == buffer.Length;
        }
    }

    public static class SemaphoreSlimeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<SemaphoreSlimLock> LockAsync(this SemaphoreSlim s, CancellationToken cancellationToken = default)
        {
            await s.WaitAsync(cancellationToken);
            return new(s);
        }
    }
    public readonly record struct SemaphoreSlimLock(SemaphoreSlim s) : IDisposable
    {
        public void Dispose() => s.Release();
    }
}
