using Hydra.Core;
using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.WebSocket
{
    public readonly record struct FrameInfo(bool Fin, WebsocketOpcode opcode, bool mask, long length, byte[] maskingKey);

    /// <summary>
    /// An reader for websocket messages
    /// </summary>
    public class WebSocketReader
    {
        public readonly PipeReader Reader;

        public WebSocketReader(PipeReader reader)
        {
            Reader = reader;
        }

        private const byte finMask = 0b1000_0000;
        private const byte rsvMask = 0b0111_0000;
        private const byte opcodeMask = 0b1111;
        private const byte maskMask = 0b1000_0000;
        private const byte lengthMask = 0b0111_1111;
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<FrameInfo?> ReadFrameInfo(CancellationToken cancellationToken = default)
        {
            bool fin;
            WebsocketOpcode opcode;
            bool mask;
            long length;
            byte[] maskingKey;

            // this is the max possible frame info size
            var result = await Reader.ReadAtLeastAsync(14, cancellationToken);
            var buffer = result.Buffer;
            var bytes = buffer.Bytes();
            var consumed = buffer.Start;

            try
            {
                if (!bytes.Next(out byte finRsvOpcode)) return null;

                fin = (finRsvOpcode & finMask) != 0;
                if ((finRsvOpcode & rsvMask) != 0) throw new NonZeroRsvException();
                opcode = (WebsocketOpcode)(finRsvOpcode & opcodeMask);
                if (!Enum.IsDefined(opcode)) throw new InvalidOpcodeException();

                if (!bytes.Next(out byte maskLength)) return null;

                mask = (maskLength & maskMask) != 0;
                length = maskLength & lengthMask;

                if (length == 126)
                {
                    if (!bytes.Next(out byte l1)) return null;
                    if (!bytes.Next(out byte l0)) return null;

                    length = (l1 << (8 * 1))
                        & (l0 << (8 * 0));
                }
                else if (length == 127)
                {
                    if (!bytes.Next(out byte l7)) return null;
                    if ((l7 & 0b1000_0000) != 0) throw new InvalidFrameLengthException();

                    if (!bytes.Next(out byte l6)) return null;
                    if (!bytes.Next(out byte l5)) return null;
                    if (!bytes.Next(out byte l4)) return null;
                    if (!bytes.Next(out byte l3)) return null;
                    if (!bytes.Next(out byte l2)) return null;
                    if (!bytes.Next(out byte l1)) return null;
                    if (!bytes.Next(out byte l0)) return null;

                    length = ((long)l7 << (8 * 7))
                        & ((long)l6 << (8 * 6))
                        & ((long)l5 << (8 * 5))
                        & ((long)l4 << (8 * 4))
                        & ((long)l3 << (8 * 3))
                        & ((long)l2 << (8 * 2))
                        & ((long)l1 << (8 * 1))
                        & ((long)l0 << (8 * 0));
                }

                if (mask)
                {
                    maskingKey = new byte[4];
                    if (!bytes.Next(out maskingKey[0])) return null;
                    if (!bytes.Next(out maskingKey[1])) return null;
                    if (!bytes.Next(out maskingKey[2])) return null;
                    if (!bytes.Next(out maskingKey[3])) return null;
                }
                else maskingKey = Array.Empty<byte>();

                consumed = bytes.Position;
                return new(fin, opcode, mask, length, maskingKey);
            }
            finally
            {
                Reader.AdvanceTo(consumed, bytes.Position);
            }
        }
    }
}
