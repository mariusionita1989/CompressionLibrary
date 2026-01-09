using CompressionLibrary.Helpers;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class Lz77
    {
        private const int MaxLookahead = 64;
        private const int MaxWindow = 8192; // 32768
        private const int LengthBits = 5;    // max length = 31
        private const int DistanceBits = 15; // ⬅️ Updated to support larger window (32768 = 2^15)
        private const int LiteralBits = 4;   // 'A'–'P' → 16 symbols
        private const int TokenBits = LengthBits + DistanceBits + LiteralBits; // now 24 bits
        private const int MinLazyLength = 4;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Token[] Compress(ReadOnlySpan<char> input)
        {
            if (input.IsEmpty) return [];

            var tokens = new Token[input.Length];
            int tokenCount = 0;
            int pos = 0;
            int inputLength = input.Length;

            while (pos < inputLength)
            {
                if (pos == inputLength - 1)
                {
                    tokens[tokenCount++] = new Token(0, 0, input[pos]);
                    break;
                }

                int windowStart = Math.Max(0, pos - MaxWindow);
                int searchLen = Math.Min(MaxLookahead, inputLength - pos - 1);
                int bestLen = 0;
                int bestDist = 0;

                for (int i = pos - 1; i >= windowStart; i--)
                {
                    int len = 0;
                    while (len < searchLen && input[pos + len] == input[i + len])
                        len++;

                    if (len > bestLen)
                    {
                        bestLen = len;
                        bestDist = pos - i;
                        if (len == searchLen) break; // optimal match found
                    }
                }

                if (bestLen >= MinLazyLength && bestLen < MaxLookahead)
                {
                    int nextPos = pos + 1;
                    int nextWindowStart = Math.Max(0, nextPos - MaxWindow);
                    int nextSearchLen = Math.Min(MaxLookahead, inputLength - nextPos);

                    int nextBestLen = 0;
                    for (int i = nextPos - 1; i >= nextWindowStart; i--)
                    {
                        int len = 0;
                        while (len < nextSearchLen && input[nextPos + len] == input[i + len])
                            len++;

                        if (len > nextBestLen)
                        {
                            nextBestLen = len;
                            if (len == nextSearchLen) break;
                        }
                    }

                    // Prefer shifting if next match is strictly longer
                    if (nextBestLen > bestLen)
                    {
                        tokens[tokenCount++] = new Token(0, 0, input[pos]);
                        pos++;
                        continue;
                    }
                }

                // Emit token: match (if any) + guaranteed next literal
                char nextChar = input[pos + bestLen];
                tokens[tokenCount++] = new Token(bestLen, bestDist, nextChar);
                pos += bestLen + 1;
            }

            return tokenCount == tokens.Length ? tokens : tokens[..tokenCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Decompress(ReadOnlySpan<Token> tokens)
        {
            if (tokens.IsEmpty) return string.Empty;

            int estimatedCapacity = 0;
            foreach (var t in tokens)
                estimatedCapacity += t.Length + 1;

            if (estimatedCapacity <= 256)
            {
                Span<char> outputSpan = stackalloc char[estimatedCapacity];
                int writePos = 0;

                foreach (var token in tokens)
                {
                    if (token.Length > 0)
                    {
                        int srcPos = writePos - token.Distance;
                        // ⬇️ Safer bounds (redundant but clear)
                        for (int i = 0; i < token.Length; i++)
                            outputSpan[writePos++] = outputSpan[srcPos + i];
                    }
                    outputSpan[writePos++] = token.Literal;
                }

                return outputSpan[..writePos].ToString();
            }
            else
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(estimatedCapacity);
                try
                {
                    Span<char> outputSpan = buffer.AsSpan(0, estimatedCapacity);
                    int writePos = 0;

                    foreach (var token in tokens)
                    {
                        if (token.Length > 0)
                        {
                            int srcPos = writePos - token.Distance;
                            for (int i = 0; i < token.Length; i++)
                                outputSpan[writePos++] = outputSpan[srcPos + i];
                        }
                        outputSpan[writePos++] = token.Literal;
                    }

                    return outputSpan[..writePos].ToString();
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int PackTokens(ReadOnlySpan<Token> tokens, Span<byte> output)
        {
            if (output.IsEmpty && tokens.IsEmpty) return 0;
            if (output.IsEmpty) ThrowNotEnoughSpace();

            int bitPos = 0;
            int byteCount = output.Length;

            foreach (var token in tokens)
            {
                if ((uint)token.Length > 31) ThrowLengthTooLarge();
                if ((uint)token.Distance > 32767) ThrowDistanceTooLarge(); // 2^15 - 1
                int lit = token.Literal - 'A';
                if ((uint)lit > 15) ThrowInvalidLiteral();

                // ⬇️ Reordered to match bit layout: [Length:5][Distance:15][Literal:4]
                uint value = ((uint)token.Length << (DistanceBits + LiteralBits)) |
                             ((uint)token.Distance << LiteralBits) |
                             (uint)lit;

                int wordStartBit = bitPos;
                int wordEndBit = wordStartBit + TokenBits; // 24 bits
                int startByte = wordStartBit / 8;
                int endByte = (wordEndBit - 1) / 8;

                if (endByte >= byteCount) ThrowNotEnoughSpace();

                // Read up to 4 bytes (covers 24 bits + alignment)
                uint current = 0;
                for (int i = 0; i < 4 && startByte + i < byteCount; i++)
                {
                    current |= (uint)output[startByte + i] << (i * 8);
                }

                int bitOffsetInWord = wordStartBit % 8;
                current |= value << bitOffsetInWord;

                // Write back up to 4 bytes
                for (int i = 0; i < 4 && startByte + i < byteCount; i++)
                {
                    output[startByte + i] = (byte)(current >> (i * 8));
                }

                bitPos = wordEndBit;
            }

            return (bitPos + 7) / 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int UnpackTokens(ReadOnlySpan<byte> input, Span<Token> output)
        {
            if (input.IsEmpty) return 0;

            int bitPos = 0;
            int tokenCount = 0;
            int maxTokens = output.Length;

            while (true)
            {
                int nextBitPos = bitPos + TokenBits; // 24
                int endByte = (nextBitPos - 1) / 8;
                if (endByte >= input.Length) break;
                if (tokenCount >= maxTokens) ThrowOutputTooSmall();

                int startByte = bitPos / 8;
                uint word = 0;
                for (int i = 0; i < 4 && startByte + i < input.Length; i++)
                {
                    word |= (uint)input[startByte + i] << (i * 8);
                }

                int bitOffsetInWord = bitPos % 8;
                uint masked = (word >> bitOffsetInWord) & ((1U << TokenBits) - 1);

                int length = (int)((masked >> (DistanceBits + LiteralBits)) & ((1U << LengthBits) - 1));
                int distance = (int)((masked >> LiteralBits) & ((1U << DistanceBits) - 1));
                char literal = (char)('A' + (masked & ((1U << LiteralBits) - 1)));

                output[tokenCount] = new Token(length, distance, literal);
                tokenCount++;
                bitPos = nextBitPos;
            }

            return tokenCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotEnoughSpace() => throw new ArgumentException("Output buffer too small.");
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowLengthTooLarge() => throw new ArgumentException("Length must be ≤ 31.");
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDistanceTooLarge() => throw new ArgumentException("Distance must be ≤ 32767.");
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidLiteral() => throw new ArgumentException("Literal must be 'A' to 'P'.");
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOutputTooSmall() => throw new ArgumentException("Token output buffer too small.");
    }
}
