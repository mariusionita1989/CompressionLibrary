using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CompressionLibrary.Main
{
    public static unsafe class Lz4
    {
        private const int MIN_MATCH = 4;
        private const int HASH_BITS = 16;
        private const int HASH_SIZE = 1 << HASH_BITS;
        private const int MAX_DISTANCE = 0xFFFF;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int Hash32(uint value)
        {
            return (int)((value * 2654435761u) >> (32 - HASH_BITS));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int CountMatch(byte* p, byte* q, byte* end)
        {
            int matched = 0;

            if (Avx2.IsSupported)
            {
                while (p + matched + 32 <= end)
                {
                    Vector256<byte> a = Avx.LoadVector256(p + matched);
                    Vector256<byte> b = Avx.LoadVector256(q + matched);
                    Vector256<byte> cmp = Avx2.CompareEqual(a, b);

                    uint mask = (uint)Avx2.MoveMask(cmp);

                    if (mask != 0xFFFFFFFFu)
                    {
                        uint diff = ~mask;

                        // MoveMask bit 31 = byte0, bit0 = byte31 → must use LeadingZeroCount
                        int idx = BitOperations.LeadingZeroCount(diff);

                        if (idx > 31)
                            idx = 31;

                        return matched + idx;
                    }

                    matched += 32;
                }
            }

            // Scalar tail
            while (p + matched < end && p[matched] == q[matched])
                matched++;

            return matched;
        }

        // Copy with overlap-safe behavior
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void CopyMatch(byte* dst, byte* src, int length)
        {
            if (Avx2.IsSupported && (dst - src) >= 32)
            {
                while (length >= 32)
                {
                    Vector256<byte> v = Avx.LoadVector256(src);
                    Avx.Store(dst, v);
                    src += 32;
                    dst += 32;
                    length -= 32;
                }
            }

            while (length-- > 0)
                *dst++ = *src++;
        }

        // Copy literals (non-overlapping)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void CopyLiterals(byte* dst, byte* src, int length)
        {
            if (Avx2.IsSupported)
            {
                while (length >= 32)
                {
                    Vector256<byte> v = Avx.LoadVector256(src);
                    Avx.Store(dst, v);
                    src += 32;
                    dst += 32;
                    length -= 32;
                }
            }

            while (length-- > 0)
                *dst++ = *src++;
        }

        // -----------------------------------------------------
        // -----------  PUBLIC COMPRESS()  ---------------------
        // -----------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int Compress(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (input.Length == 0) return 0;

            int[] hashTable = GC.AllocateUninitializedArray<int>(HASH_SIZE);
            Array.Fill(hashTable, -1);

            fixed (byte* srcBase = input)
            fixed (byte* dstBase = output)
            fixed (int* hashPtr = hashTable)
            {
                byte* ip = srcBase;
                byte* anchor = ip;
                byte* iend = srcBase + input.Length;
                byte* mflimit = iend - MIN_MATCH;
                byte* op = dstBase;
                byte* oend = dstBase + output.Length;

                while (ip < mflimit)
                {
                    uint seq = Unsafe.ReadUnaligned<uint>(ip);
                    int h = Hash32(seq);
                    int refPos = hashPtr[h];
                    hashPtr[h] = (int)(ip - srcBase);

                    int matchLen = 0;
                    int offset = 0;

                    if (refPos >= 0)
                    {
                        byte* match = srcBase + refPos;
                        offset = (int)(ip - match);

                        if (offset > 0 && offset <= MAX_DISTANCE)
                        {
                            matchLen = CountMatch(ip, match, iend);
                        }
                    }

                    if (matchLen >= MIN_MATCH)
                    {
                        // Encode literals
                        int litLength = (int)(ip - anchor);
                        byte* tokenPtr = op++;
                        if (op >= oend) return 0;

                        int t = litLength < 15 ? litLength : 15;
                        *tokenPtr = (byte)(t << 4);

                        if (litLength >= 15)
                        {
                            int v = litLength - 15;
                            while (v >= 255)
                            {
                                *op++ = 255;
                                v -= 255;
                            }
                            *op++ = (byte)v;
                        }

                        // Copy literals
                        CopyLiterals(op, anchor, litLength);
                        op += litLength;

                        // Write offset
                        *op++ = (byte)offset;
                        *op++ = (byte)(offset >> 8);

                        // Encode match length
                        int ml = matchLen - MIN_MATCH;
                        int mlNibble = ml < 15 ? ml : 15;
                        *tokenPtr |= (byte)mlNibble;

                        if (ml >= 15)
                        {
                            int v = ml - 15;
                            while (v >= 255)
                            {
                                *op++ = 255;
                                v -= 255;
                            }
                            *op++ = (byte)v;
                        }

                        ip += matchLen;
                        anchor = ip;
                        continue;
                    }

                    ip++;
                }

                // Final literals
                int lastLits = (int)(iend - anchor);
                if (lastLits > 0)
                {
                    byte* tokenPtr = op++;
                    int t = Math.Min(lastLits, 15);
                    *tokenPtr = (byte)(t << 4);

                    if (lastLits >= 15)
                    {
                        int v = lastLits - 15;
                        while (v >= 255)
                        {
                            *op++ = 255;
                            v -= 255;
                        }
                        *op++ = (byte)v;
                    }

                    CopyLiterals(op, anchor, lastLits);
                    op += lastLits;
                }

                return (int)(op - dstBase);
            }
        }

        // -----------------------------------------------------
        // -----------  PUBLIC DECOMPRESS()  -------------------
        // -----------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int Decompress(ReadOnlySpan<byte> input, Span<byte> output)
        {
            fixed (byte* ipBase = input)
            fixed (byte* opBase = output)
            {
                byte* ip = ipBase;
                byte* iend = ipBase + input.Length;
                byte* op = opBase;
                byte* oend = opBase + output.Length;

                while (ip < iend)
                {
                    int token = *ip++;
                    int litLength = token >> 4;

                    if (litLength == 15)
                    {
                        byte s;
                        while ((s = *ip++) == 255) litLength += 255;
                        litLength += s;
                    }

                    // Copy literals
                    CopyLiterals(op, ip, litLength);
                    ip += litLength;
                    op += litLength;

                    if (ip >= iend) break;

                    // Offset
                    int offset = ip[0] | (ip[1] << 8);
                    ip += 2;

                    if (offset <= 0 || offset > (op - opBase))
                        throw new Exception("Invalid LZ4 offset");

                    byte* match = op - offset;

                    // Match length
                    int matchLen = token & 15;
                    if (matchLen == 15)
                    {
                        byte s;
                        while ((s = *ip++) == 255) matchLen += 255;
                        matchLen += s;
                    }
                    matchLen += MIN_MATCH;

                    CopyMatch(op, match, matchLen);
                    op += matchLen;
                }

                return (int)(op - opBase);
            }
        }
    }
}
