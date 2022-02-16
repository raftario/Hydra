using Hydra.Core;
using Hydra.WebSocket13;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra
{
    public class WebSocket : IAsyncDisposable
    {
        private readonly WebSocketReader reader;
        private readonly WebSocketWriter writer;
        private readonly SemaphoreSlim readerLock = new(1);
        private readonly SemaphoreSlim writerLock = new(1);
        private Stream? currentStream = null;

        internal readonly ConcurrentQueue<(FrameInfo, DisposableBuffer<byte>)> interleaved = new();
        private readonly ConcurrentDictionary<byte, TaskCompletionSource> pings = new();
        private byte ping = 0;

        private const int openState = 0;
        private const int closingState = 1;
        private const int closedState = 2;
        internal int state = openState;

        internal WebSocketCloseMessage closeMessage = default;

        public bool Closed => state is closedState;
        public bool Readable => state is openState;
        public bool Writeable => state is openState;

        public WebSocketCloseMessage CloseMessage => Closed ? closeMessage : throw new InvalidOperationException("Can't access close message before connection is closed");
        public CancellationToken CancellationToken { get; }

        internal WebSocket(PipeReader reader, PipeWriter writer, CancellationToken cancellationToken)
        {
            this.reader = new(reader);
            this.writer = new(writer);
            CancellationToken = cancellationToken;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask Send(WebSocketMessage message, CancellationToken cancellationToken = default)
        {
            if (!Writeable) throw new ClosedException();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            long? length;
            try
            {
                length = message.Body.Length;
                length -= message.Body.Position;
            }
            catch
            {
                length = null;
            }

            using (var _lock = await writerLock.LockAsync(cancellationToken))
            {
                if (length is not null)
                {
                    await writer.WriteSizedMessage(
                        message.Opcode,
                        message.Body,
                        length.Value,
                        cancellationToken);
                }
                else
                {
                    await writer.WriteUnsizedMessage(
                        message.Opcode,
                        message.Body,
                        Interleaver,
                        cancellationToken);
                }
            }

            await message.Body.DisposeAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<WebSocketMessage?> Receive(CancellationToken cancellationToken = default)
        {
            if (!Readable) throw new ClosedException();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            while (true)
            {
                using var _lock = await readerLock.LockAsync(cancellationToken);

                var frameInfo = await StartReceive(cancellationToken);
                if (frameInfo is null) return null;

                if (frameInfo.Value.Opcode == WebSocketOpcode.Ping)
                {
                    using var _wlock = await writerLock.LockAsync(cancellationToken);

                    await writer.WriteSizedMessage(
                        WebSocketOpcode.Pong,
                        currentStream,
                        frameInfo.Value.Length,
                        cancellationToken);
                }
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Pong)
                {
                    if (frameInfo.Value.Length != 1) continue;

                    byte? ping = await currentStream.ReadByteAsync(cancellationToken);
                    if (ping is null)
                    {
                        state = closedState;
                        return null;
                    }

                    if (pings.TryGetValue(ping.Value, out var tcs)) tcs.TrySetResult();
                }
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Close)
                {
                    int length = (int)frameInfo.Value.Length;

                    using var buffer = ArrayPool<byte>.Shared.RentDisposable(length);
                    var bodyBuffer = buffer[..length];

                    if (!await currentStream.ReadAllAsync(bodyBuffer, cancellationToken)) return null;

                    await HandleClose(bodyBuffer, true, cancellationToken);
                    return null;
                }
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Text) return new WebSocketTextMessage(currentStream);
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Binary) return new WebSocketBinaryMessage(currentStream);
                else throw new LoneContinuationFrameException();
            }
        }

        public async ValueTask<Task> Ping(CancellationToken cancellationToken = default)
        {
            if (!Writeable) throw new ClosedException();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            var tcs = new TaskCompletionSource();
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            byte ping = this.ping++;
            pings[ping] = tcs;

            using var _lock = await writerLock.LockAsync(cancellationToken);
            await writer.WriteSizedMessage(WebSocketOpcode.Ping, new SingleByteStream(ping), 1, cancellationToken);

            return tcs.Task;
        }

        public async ValueTask Close(WebSocketCloseMessage message = new(), CancellationToken cancellationToken = default)
        {
            if (Closed) throw new ClosedException();
            if (!StartClose()) return;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            try
            {
                using (var _wlock = await writerLock.LockAsync(cancellationToken))
                {
                    await writer.WriteCloseMessage(message.Code, message.Reason, cancellationToken);
                }

                using var _rlock = await readerLock.LockAsync(cancellationToken);
                while (true)
                {
                    var frameInfo = await StartReceive(cancellationToken);
                    if (frameInfo is null) return;
                    if (frameInfo.Value.Opcode != WebSocketOpcode.Close) continue;

                    int length = (int)frameInfo.Value.Length;

                    using var buffer = ArrayPool<byte>.Shared.RentDisposable(length);
                    var bodyBuffer = buffer[..length];

                    if (!await currentStream.ReadAllAsync(bodyBuffer, cancellationToken)) return;
                    WebSocketReader.ParseCloseBody(bodyBuffer.Span, out ushort? code, out string? reason);

                    closeMessage = new(code, reason);
                }
            } finally { state = closedState; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask<FrameInfo?> StartReceive(CancellationToken cancellationToken)
        {
            if (currentStream is not null)
            {
                await currentStream.Drain(cancellationToken);
                await currentStream.DisposeAsync();
                currentStream = null;
            }

            var frameInfo = await reader.ReadFrameInfo(cancellationToken);
            if (frameInfo is null)
            {
                state = closedState;
                return null;
            }

            if (!frameInfo.Value.Mask) throw new UnmaskedBodyException();

            if (frameInfo.Value.Fin)
            {
                currentStream = new WebSocketMaskedStream(
                    new SizedStream(reader.Reader.AsStream(false),
                    (int)frameInfo.Value.Length),
                    frameInfo.Value.MaskingKey);
            }
            else if (frameInfo.Value.Opcode is not WebSocketOpcode.Text and not WebSocketOpcode.Binary) throw new NonFrameableMessageFramedException();
            else currentStream = null;

            return frameInfo;
        }

        private async ValueTask HandleClose(Memory<byte> body, bool lockk, CancellationToken cancellationToken)
        {
            if (!StartClose()) return;

            WebSocketReader.ParseCloseBody(body.Span, out ushort? code, out string? reason);
            closeMessage = new(code, reason);

            var write = () => writer.WriteCloseMessage(code, reason, cancellationToken);

            try
            {
                if (lockk)
                {
                    using var _lock = await writerLock.LockAsync(cancellationToken);
                    await write();
                }
                else await write();
            }
            finally { state = closedState; }
        }

        private bool StartClose() => Interlocked.CompareExchange(ref state, closingState, openState) == openState;

        private async ValueTask<bool> Interleaver(CancellationToken cancellationToken)
        {
            while (interleaved.TryDequeue(out var frame))
            {
                var (info, body) = frame;
                try
                {
                    if (info.Opcode == WebSocketOpcode.Ping)
                    {
                        await writer.WriteMemoryMessage(WebSocketOpcode.Pong, body, cancellationToken);
                    }
                    else if (info.Opcode == WebSocketOpcode.Pong
                        && body.Length == 1
                        && pings.TryGetValue(body[0], out var tcs))
                    {
                        tcs.TrySetResult();
                    }
                    else if (info.Opcode == WebSocketOpcode.Close)
                    {
                        await HandleClose(body, false, cancellationToken);
                        return false;
                    }
                }
                finally { body.Dispose(); }
            }
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            if (currentStream is not null) await currentStream.DisposeAsync();
            foreach (var (_, body) in interleaved) body.Dispose();
            foreach (var (_, tcs) in pings) tcs.TrySetCanceled();

            readerLock.Dispose();
            writerLock.Dispose();
            GC.SuppressFinalize(this);
        }

        public class ClosedException : Exception { }
    }
}
