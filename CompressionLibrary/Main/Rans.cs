using System.Buffers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class Rans
    {
        private const uint RANS_L = 1u << 23;   // lower bound
        private const int SYMBOL_COUNT = 16;    // A–P
        private const int FREQ_BITS = 8;        // total freq = 256
        private const uint TOTAL_FREQ = 1u << FREQ_BITS;

        private static readonly byte[] Freq =
        {
            16,16,16,16,16,16,16,16,
            16,16,16,16,16,16,16,16
        };

        private static readonly uint[] Start = new uint[SYMBOL_COUNT];
        private static readonly byte[] DecodeLut = new byte[TOTAL_FREQ];

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        static Rans()
        {
            uint acc = 0;
            for (int i = 0; i < SYMBOL_COUNT; i++)
            {
                Start[i] = acc;
                for (uint j = 0; j < Freq[i]; j++)
                    DecodeLut[acc + j] = (byte)i;
                acc += Freq[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte C2S(char c) => (byte)(c - 'A');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char S2C(byte s) => (char)('A' + s);

        // ============================================================
        // ENCODE
        // ============================================================
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] Encode(ReadOnlySpan<char> input)
        {
            int maxPayload = (input.Length << 1) + 16;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 + maxPayload);

            Span<byte> span = buffer;
            span[0] = (byte)(input.Length >> 24);
            span[1] = (byte)(input.Length >> 16);
            span[2] = (byte)(input.Length >> 8);
            span[3] = (byte)(input.Length);

            int payloadStart = EncodePayload(input, span.Slice(4), out int payloadLen);
            byte[] result = new byte[4 + payloadLen];
            span.Slice(0, 4).CopyTo(result);
            span.Slice(4 + payloadStart, payloadLen).CopyTo(result.AsSpan(4));

            ArrayPool<byte>.Shared.Return(buffer);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int EncodePayload(ReadOnlySpan<char> input, Span<byte> output, out int payloadLen)
        {
            uint state = RANS_L;
            int pos = output.Length;

            for (int i = input.Length - 1; i >= 0; i--)
            {
                byte sym = C2S(input[i]);
                uint f = Freq[sym];
                uint st = Start[sym];

                uint maxState = ((RANS_L >> FREQ_BITS) << 8) * f;
                while (state >= maxState)
                {
                    output[--pos] = (byte)state;
                    state >>= 8;
                }

                uint q = state / f;
                uint r = state % f;
                state = q * TOTAL_FREQ + st + r;
            }

            // Flush final state
            for (int i = 0; i < 4; i++)
            {
                output[--pos] = (byte)state;
                state >>= 8;
            }

            payloadLen = output.Length - pos;
            return pos;
        }

        // ============================================================
        // DECODE
        // ============================================================
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Decode(ReadOnlySpan<byte> encoded)
        {
            int len = (encoded[0] << 24) | (encoded[1] << 16) | (encoded[2] << 8) | encoded[3];
            ReadOnlySpan<byte> payload = encoded.Slice(4);

            int i = 0;
            uint state = ((uint)payload[i++] << 24) | ((uint)payload[i++] << 16) |
                         ((uint)payload[i++] << 8) | payload[i++];

            Span<char> buffer = len <= 1024 ? stackalloc char[len] : new char[len];
            int pos = 0;

            for (int k = 0; k < len; k++)
            {
                uint slot = state & (TOTAL_FREQ - 1);
                byte sym = DecodeLut[slot];

                buffer[pos++] = S2C(sym);

                state = (state / TOTAL_FREQ) * Freq[sym] + (slot - Start[sym]);
                while (state < RANS_L && i < payload.Length)
                    state = (state << 8) | payload[i++];
            }

            return new string(buffer);
        }
    }
}
