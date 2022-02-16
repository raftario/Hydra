using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.WebSocket13
{
    public class WebSocketWriter
    {
        public delegate ValueTask<bool> Interleaver(CancellationToken cancellationToken);

        public int MaxFrameLength = 8 * 1024;
        public int MaxFrameInfoLength => FrameInfoLength(MaxFrameLength);

        public readonly PipeWriter Writer;

        private const byte finBit = 0b1000_0000;

        public WebSocketWriter(PipeWriter writer)
        {
            Writer = writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask WriteSizedMessage(WebSocketOpcode opcode, Stream body, long length, CancellationToken cancellationToken)
        {
            int frameInfoLength = FrameInfoLength(length);
            var memory = Writer.GetMemory(frameInfoLength)[..frameInfoLength];

            WriteFrameInfo(true, opcode, length, memory.Span);
            Writer.Advance(frameInfoLength);

            await body.CopyToAsync(Writer, cancellationToken);
            await Writer.FlushAsync(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask WriteUnsizedMessage(WebSocketOpcode opcode, Stream body, Interleaver? interleaver, CancellationToken cancellationToken)
        {
            while (true)
            {
                var frameMemory = Writer.GetMemory(MaxFrameLength);
                var dataMemory = frameMemory[MaxFrameInfoLength..^8];
                int read = await body.ReadAsync(dataMemory, cancellationToken);

                if (read == 0)
                {
                    WriteFrameInfo(true, opcode, 0, frameMemory.Span[..2]);

                    Writer.Advance(2);
                    await Writer.FlushAsync(cancellationToken);
                    return;
                }

                int frameInfoLength = FrameInfoLength(read);
                if (frameInfoLength != MaxFrameInfoLength) dataMemory[..read].CopyTo(frameMemory[frameInfoLength..]);

                WriteFrameInfo(false, opcode, read, frameMemory.Span[..frameInfoLength]);

                Writer.Advance(frameInfoLength + read);
                await Writer.FlushAsync(cancellationToken);
                if (opcode != WebSocketOpcode.Continuation) opcode = WebSocketOpcode.Continuation;

                if (interleaver is not null && !await interleaver(cancellationToken)) return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void WriteFrameInfo(bool fin, WebSocketOpcode opcode, long length, Span<byte> location)
        {
            location[0] = (byte)(fin ? finBit | (byte)opcode : (byte)opcode);

            if (location.Length == 2) location[1] = (byte)length;
            else if (location.Length == 4)
            {
                location[1] = 126;

                location[2] = (byte)((length & 0xFF00) >> 8);
                location[3] = (byte)(length & 0xFF00);
            }
            else
            {
                location[1] = 127;

                location[2] = (byte)((length & (0xFF << (8 * 7))) >> (8 * 7));
                location[3] = (byte)((length & (0xFF << (8 * 6))) >> (8 * 6));
                location[4] = (byte)((length & (0xFF << (8 * 5))) >> (8 * 5));
                location[5] = (byte)((length & (0xFF << (8 * 4))) >> (8 * 4));
                location[6] = (byte)((length & (0xFF << (8 * 3))) >> (8 * 3));
                location[7] = (byte)((length & (0xFF << (8 * 2))) >> (8 * 2));
                location[8] = (byte)((length & (0xFF << (8 * 1))) >> (8 * 1));
                location[9] = (byte)((length & (0xFF << (8 * 0))) >> (8 * 0));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FrameInfoLength(long length) => length switch
        {
            > ushort.MaxValue => 10,
            > 125 => 4,
            _ => 2,
        };
    }
}
