using Hydra.Core;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hydra.WebSocket13
{
    public struct WebSocketMasker : IDisposable
    {
        private static readonly int vecLength = Vector<byte>.Count;
        private static readonly int keyLength = 4 + vecLength - 1;

        private readonly byte[] key;
        private int i = 0;

        private Vector<byte> KeyVec => new(key.AsSpan()[i..]);

        public WebSocketMasker(uint key)
        {
            this.key = ArrayPool<byte>.Shared.Rent(keyLength);
            var keySpan = key.AsRawSpan();
            for (int ki = 0; ki < keyLength; ki++) this.key[ki] = keySpan[ki % 4];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Mask(Span<byte> payload)
        {
            int pi = 0;
            var keyVec = KeyVec;

            for (; pi + vecLength <= payload.Length; pi += vecLength)
            {
                var payloadVec = new Vector<byte>(payload[pi..]);
                (payloadVec ^ keyVec).CopyTo(payload[pi..]);
            }
            for (; pi < payload.Length; pi++)
            {
                payload[pi] ^= key[i];
                i = (i + 1) % 4;
            }
        }

        public void Dispose() => ArrayPool<byte>.Shared.Return(key);
    }
}
