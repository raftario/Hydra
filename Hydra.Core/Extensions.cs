﻿using System;
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
            using var buffer = ArrayPool<byte>.Shared.RentDisposable(4 * 1024);

            int read = 1;
            while (read > 0) read = await stream.ReadAsync(buffer, cancellationToken);
        }

        public static async ValueTask<byte?> ReadByteAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            using var buffer = ArrayPool<byte>.Shared.RentDisposable(1);

            int read = await stream.ReadAsync(buffer[..1], cancellationToken);
            return read > 0 ? buffer[0] : null;
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

    public static class StringExtensions
    {
        public static int Utf8Length(this string s) => Encoding.UTF8.GetByteCount(s);
    }

    public static class SemaphoreSlimeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<SemaphoreSlimLock> LockAsync(this SemaphoreSlim s, CancellationToken cancellationToken = default)
        {
            await s.WaitAsync(cancellationToken);
            return new(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask LockAndReleaseAsync(this SemaphoreSlim s, CancellationToken cancellationToken = default)
        {
            await s.WaitAsync(cancellationToken);
            s.Release();
        }
    }
    public readonly record struct SemaphoreSlimLock(SemaphoreSlim Semaphore) : IDisposable
    {
        public void Dispose() => Semaphore.Release();
    }

    public static class ArrayPoolExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableBuffer<T> RentDisposable<T>(this ArrayPool<T> pool, int minimumLength) => new(pool.Rent(minimumLength), pool);
    }
    public readonly record struct DisposableBuffer<T>(T[] Buffer, ArrayPool<T> Source) : IDisposable
    {
        public static implicit operator T[](DisposableBuffer<T> buffer) => buffer.Buffer;
        public static implicit operator Memory<T>(DisposableBuffer<T> buffer) => buffer.Buffer.AsMemory();
        public static implicit operator Span<T>(DisposableBuffer<T> buffer) => buffer.Buffer.AsSpan();
        public static implicit operator ReadOnlyMemory<T>(DisposableBuffer<T> buffer) => buffer.Buffer.AsMemory();
        public static implicit operator ReadOnlySpan<T>(DisposableBuffer<T> buffer) => buffer.Buffer.AsSpan();

        public T this[Index index] {
            get => Buffer[index];
            set => Buffer[index] = value;
        }
        public Memory<T> this[Range range] => Buffer.AsMemory()[range];

        public int Length => Buffer.Length;

        public void Dispose() => Source.Return(Buffer);
    }
}
