using Hydra.Core;
using Hydra.WebSocket13;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hydra
{
    public class WebSocket : IAsyncDisposable
    {
        private readonly Socket socket;
        private readonly WebSocketReader reader;
        private readonly WebSocketWriter writer;

        private readonly SemaphoreSlim readerLock = new(1);
        private readonly SemaphoreSlim writerLock = new(1);
        private readonly SemaphoreSlim interleaveLock = new(1);

        private Stream? currentStream = null;

        private readonly Channel<(FrameInfo Info, DisposableBuffer<byte> Body)> backgroundFrames;
        private readonly Task backgroundTask;

        private readonly ConcurrentDictionary<uint, TaskCompletionSource> pings = new();
        private uint ping = 0;

        private const int openState = 0;
        private const int closingState = 1;
        private const int closedState = 2;
        internal int state = openState;

        internal WebSocketCloseMessage closeMessage = new();

        public EndPoint? Remote => socket.RemoteEndPoint;

        public bool Closed => state is closedState;
        public bool Readable => state is openState;
        public bool Writeable => state is openState;

        public WebSocketCloseMessage CloseMessage => Closed ? closeMessage : throw new InvalidOperationException("Can't access close message before connection is closed");
        public CancellationToken CancellationToken { get; }

        internal WebSocket(Socket socket, PipeReader reader, PipeWriter writer, int backgroundChannelCapacity, CancellationToken cancellationToken)
        {
            this.socket = socket;
            this.reader = new(reader);
            this.writer = new(writer);

            backgroundFrames = Channel.CreateBounded<(FrameInfo, DisposableBuffer<byte>)>(new BoundedChannelOptions(backgroundChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
            });
            backgroundTask = Task.Run(BackgroundTask, cancellationToken);

            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Creates a response suitable for upgrading a request to a <see cref="WebSocket"/>.
        /// </summary>
        /// <param name="request">HTTP request to validate the WSS headers with</param>
        /// <param name="handler">Delegate which will process the <see cref="WebSocket"/> once the handshake
        /// is complete</param>
        /// <returns>An HTTP response for the upgrade, or alternatively a code 400 response any of the necessary
        /// WSS headers are missing or incorrect</returns>
        public static HttpResponse Response(HttpRequest request, Server.WebSocketHandler handler) => new WebSocketResponse(request, handler);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async Task<bool> Send(WebSocketMessage message, CancellationToken cancellationToken = default)
        {
            if (!Writeable) return false;

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

            using (var _lock = await FullWriterLock(cancellationToken))
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
                        Interleave,
                        cancellationToken);
                }
            }

            await message.Body.DisposeAsync();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async Task<WebSocketMessage?> Receive(CancellationToken cancellationToken = default)
        {
            if (!Readable) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            while (true)
            {
                using var _lock = await readerLock.LockAsync(cancellationToken);

                var frameInfo = await StartReceive(cancellationToken);
                if (frameInfo is null) return null;

                if (frameInfo.Value.Opcode is WebSocketOpcode.Ping or WebSocketOpcode.Pong or WebSocketOpcode.Close)
                {
                    int length = (int)frameInfo.Value.Length;

                    var buffer = ArrayPool<byte>.Shared.RentDisposable(length);
                    var bodyBuffer = buffer[..length];

                    if (!await currentStream.ReadAllAsync(bodyBuffer, cancellationToken)) return null;

                    await backgroundFrames.Writer.WriteAsync((frameInfo.Value, buffer), cancellationToken);
                }
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Text) return new WebSocketTextMessage(currentStream);
                else if (frameInfo.Value.Opcode == WebSocketOpcode.Binary) return new WebSocketBinaryMessage(currentStream);
                else throw new LoneContinuationFrameException();
            }
        }

        public async Task<Task?> Ping(CancellationToken cancellationToken = default)
        {
            if (!Writeable) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            using var _lock = await FullWriterLock(cancellationToken);

            using var buffer = PingBuffer();
            await writer.WriteMemoryMessage(WebSocketOpcode.Ping, buffer[..4], cancellationToken);

            var tcs = new TaskCompletionSource();
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            pings[ping++] = tcs;

            return tcs.Task;
        }

        public async Task<bool> Close(WebSocketCloseMessage message = new(), CancellationToken cancellationToken = default)
        {
            if (Closed || !StartClose()) return false;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken);
            cancellationToken = cts.Token;

            try
            {
                using (var _wlock = await FullWriterLock(cancellationToken))
                {
                    await writer.WriteCloseMessage(message.Code, message.Reason, cancellationToken);
                }

                using var _rlock = await readerLock.LockAsync(cancellationToken);
                while (true)
                {
                    var frameInfo = await StartReceive(cancellationToken);
                    if (frameInfo is null) return false;
                    if (frameInfo.Value.Opcode != WebSocketOpcode.Close) continue;

                    int length = (int)frameInfo.Value.Length;

                    using var buffer = ArrayPool<byte>.Shared.RentDisposable(length);
                    var bodyBuffer = buffer[..length];

                    if (!await currentStream.ReadAllAsync(bodyBuffer, cancellationToken)) return false;
                    WebSocketReader.ParseCloseBody(bodyBuffer.Span, out ushort? code, out string? reason);

                    closeMessage = new(code, reason);
                }
            }
            finally
            { 
                state = closedState;
                socket.Shutdown(SocketShutdown.Both);
                await socket.DisconnectAsync(false, cancellationToken);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task BackgroundTask()
        {
            await foreach (var (info, body) in backgroundFrames.Reader.ReadAllAsync())
            {
                try
                {
                    if (info.Opcode == WebSocketOpcode.Ping)
                    {
                        using var _lock = await InterleavedWriterLock(CancellationToken);
                        await writer.WriteMemoryMessage(WebSocketOpcode.Pong, body, CancellationToken);
                    }
                    else if (info.Opcode == WebSocketOpcode.Pong
                        && body.Length == 4
                        && pings.TryGetValue(BytesToPing(body), out var tcs))
                    {
                        tcs.TrySetResult();
                    }
                    else if (info.Opcode == WebSocketOpcode.Close)
                    {
                        if (!StartClose()) return;

                        WebSocketReader.ParseCloseBody(body, out ushort? code, out string? reason);
                        closeMessage = new(code, reason);

                        try
                        {
                            using var _lock = await InterleavedWriterLock(CancellationToken);
                            await writer.WriteCloseMessage(code, reason, CancellationToken);
                            return;
                        }
                        finally
                        {
                            state = closedState;
                            socket.Shutdown(SocketShutdown.Both);
                            await socket.DisconnectAsync(false, CancellationToken);
                        }
                    }
                }
                finally { body.Dispose(); }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask<bool> Interleave(CancellationToken cancellationToken)
        {
            using var _lock = await interleaveLock.LockAsync(cancellationToken);
            writerLock.Release();
            await Task.Yield();
            await writerLock.WaitAsync(cancellationToken);
            return Writeable;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask<SemaphoreSlimLock> FullWriterLock(CancellationToken cancellationToken)
        {
            var wlock = await writerLock.LockAsync(cancellationToken);
            try
            {
                await interleaveLock.LockAndReleaseAsync(cancellationToken);
            }
            catch
            {
                wlock.Dispose();
                throw;
            }
            return wlock;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<SemaphoreSlimLock> InterleavedWriterLock(CancellationToken cancellationToken) => writerLock.LockAsync(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
                    (int)frameInfo.Value.Length, true),
                    frameInfo.Value.MaskingKey,
                    true);
            }
            else if (frameInfo.Value.Opcode is not WebSocketOpcode.Text and not WebSocketOpcode.Binary) throw new NonFrameableMessageFramedException();
            else currentStream = null; // TODO: Fragmented stream

            return frameInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StartClose() => Interlocked.CompareExchange(ref state, closingState, openState) == openState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DisposableBuffer<byte> PingBuffer()
        {
            var pingBytes = ping.AsRawSpan();
            var buffer = ArrayPool<byte>.Shared.RentDisposable(4);

            buffer[0] = pingBytes[0];
            buffer[1] = pingBytes[1];
            buffer[2] = pingBytes[2];
            buffer[3] = pingBytes[3];

            return buffer;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BytesToPing(Span<byte> bytes)
        {
            uint ping = 0;
            var pingBytes = ping.AsRawSpan();

            pingBytes[0] = bytes[0];
            pingBytes[1] = bytes[1];
            pingBytes[2] = bytes[2];
            pingBytes[3] = bytes[3];

            return ping;
        }

        public async ValueTask DisposeAsync()
        {
            if (currentStream is not null) await currentStream.DisposeAsync();

            backgroundFrames.Writer.Complete();
            await backgroundTask;

            await foreach (var (_, body) in backgroundFrames.Reader.ReadAllAsync()) body.Dispose();
            foreach (var (_, tcs) in pings) tcs.TrySetCanceled();

            readerLock.Dispose();
            writerLock.Dispose();
            interleaveLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
