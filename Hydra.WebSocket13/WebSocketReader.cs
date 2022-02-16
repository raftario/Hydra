using Hydra.Core;
using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hydra.WebSocket13
{
    public readonly record struct FrameInfo(bool Fin, WebSocketOpcode Opcode, bool Mask, long Length, uint MaskingKey);

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
            // this is the max possible frame info size
            var result = await Reader.ReadAtLeastAsync(14, cancellationToken);
            var buffer = result.Buffer;
            var bytes = buffer.Bytes();
            var consumed = buffer.Start;

            try
            {
                if (!ParseFrameInfo(ref bytes, out bool fin, out var opcode, out bool mask, out long length, out uint maskingKey)) return null;
                consumed = bytes.Position;
                return new(fin, opcode, mask, length, maskingKey);
            }
            finally
            {
                Reader.AdvanceTo(consumed, bytes.Position);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParseCloseBody(ReadOnlySpan<byte> body, out ushort? code, out string? reason)
        {
            code = null;
            reason = null;

            if (body.Length >= 2) code = (ushort)((body[0] << 8) | body[1]);
            if (body.Length > 2) reason = Encoding.UTF8.GetString(body[2..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ParseFrameInfo(
            ref Bytes bytes,
            out bool fin,
            out WebSocketOpcode opcode,
            out bool mask,
            out long length,
            out uint maskingKey)
        {
            fin = default; 
            opcode = default;
            mask = default;
            length = default;
            maskingKey = 0;

            if (!bytes.Next(out byte finRsvOpcode)) return false;

            fin = (finRsvOpcode & finMask) != 0;
            if ((finRsvOpcode & rsvMask) != 0) throw new NonZeroRsvException();
            opcode = (WebSocketOpcode)(finRsvOpcode & opcodeMask);
            if (!Enum.IsDefined(opcode)) throw new InvalidOpcodeException();

            if (!bytes.Next(out byte maskLength)) return false;

            mask = (maskLength & maskMask) != 0;
            length = maskLength & lengthMask;

            if (length == 126)
            {
                if (!bytes.Next(out byte l1)) return false;
                if (!bytes.Next(out byte l0)) return false;

                length = (l1 << (8 * 1))
                    & (l0 << (8 * 0));
            }
            else if (length == 127)
            {
                if (!bytes.Next(out byte l7)) return false;
                if ((l7 & 0b1000_0000) != 0) throw new InvalidFrameLengthException();

                if (!bytes.Next(out byte l6)) return false;
                if (!bytes.Next(out byte l5)) return false;
                if (!bytes.Next(out byte l4)) return false;
                if (!bytes.Next(out byte l3)) return false;
                if (!bytes.Next(out byte l2)) return false;
                if (!bytes.Next(out byte l1)) return false;
                if (!bytes.Next(out byte l0)) return false;

                length = ((long)l7 << (8 * 7))
                    & ((long)l6 << (8 * 6))
                    & ((long)l5 << (8 * 5))
                    & ((long)l4 << (8 * 4))
                    & ((long)l3 << (8 * 3))
                    & ((long)l2 << (8 * 2))
                    & ((long)l1 << (8 * 1))
                    & ((long)l0 << (8 * 0));
            }

            maskingKey = 0;
            if (mask)
            {
                var maskingKeySpan = maskingKey.AsRawSpan();
                if (!bytes.Next(out maskingKeySpan[0])) return false;
                if (!bytes.Next(out maskingKeySpan[1])) return false;
                if (!bytes.Next(out maskingKeySpan[2])) return false;
                if (!bytes.Next(out maskingKeySpan[3])) return false;
            }

            return true;
        }
    }
}
