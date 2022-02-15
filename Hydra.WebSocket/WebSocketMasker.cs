using System;
using System.Runtime.CompilerServices;

namespace Hydra.WebSocket
{
    public struct WebSocketMasker
    {
        private readonly byte[] maskingKey;
        private int i = 0;

        public WebSocketMasker(byte[] maskingKey)
        {
            this.maskingKey = maskingKey;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Mask(Span<byte> payload)
        {
            for (int ii = 0; ii < payload.Length; ii++)
            {
                payload[ii] ^= maskingKey[i];
                i = (i + 1) % 4;
            }
        }
    }
}
