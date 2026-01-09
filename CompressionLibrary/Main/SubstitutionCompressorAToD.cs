using CompressionLibrary.Helpers;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class SubstitutionCompressorAToD
    {
        private const int MaxBigrams = 16;
        private const int MaxTrigrams = 64;
        private const string FirstAlphabet = "EFGHIJKLMNOPQRSTUVWXYZ";
        private const string SecondAlphabet = "abcdefghijklmnopqrstuvwxyz";

        // ======================= PUBLIC API =======================
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Compress(string input, out SymbolMap dictionary)
        {
            dictionary = default;
            if (string.IsNullOrEmpty(input))
                return input;
            int len = input.Length;
            var pool = ArrayPool<char>.Shared;
            char[] a = pool.Rent(len << 1);
            char[] b = pool.Rent(len << 1);
            input.AsSpan().CopyTo(a);
            Span<char> cur = a;
            Span<char> work = b;
            var first = new SymbolMap();
            var second = new SymbolMap();
            len = CompressPass(cur, work, len, FirstAlphabet, ref first);
            len = CompressPass(cur, work, len, SecondAlphabet, ref second);
            dictionary = first.Merge(second);
            string result = new string(cur.Slice(0, len));
            pool.Return(a);
            pool.Return(b);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyMemory<char> Decompress(ReadOnlySpan<char> compressed, in SymbolMap dictionary)
        {
            if (compressed.IsEmpty || dictionary.Count == 0)
                return compressed.ToArray();

            int maxLen = compressed.Length << 2;
            var pool = ArrayPool<char>.Shared;
            char[] bufA = pool.Rent(maxLen);
            char[] bufB = pool.Rent(maxLen);
            compressed.CopyTo(bufA);
            int len = compressed.Length;
            Span<char> cur = bufA;
            Span<char> next = bufB;
            for (int i = dictionary.Count - 1; i >= 0; i--)
            {
                ref readonly var e = ref dictionary.Entries[i];
                int p = 0;

                for (int j = 0; j < len; j++)
                {
                    char c = cur[j];
                    if (c == e.Symbol)
                        p = DecodeInto(next, p, e.Code, e.Length);
                    else
                        next[p++] = c;
                }

                Span<char> tmp = cur;
                cur = next;
                next = tmp;

                len = p;
            }

            char[] result = new char[len];
            cur[..len].CopyTo(result);
            pool.Return(bufA);
            pool.Return(bufB);

            return result;
        }

        // ======================= CORE LOGIC =======================
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int CompressPass(Span<char> cur, Span<char> work, int length, ReadOnlySpan<char> alphabet, ref SymbolMap dict)
        {
            Span<int> bigrams = stackalloc int[MaxBigrams];
            Span<int> trigrams = stackalloc int[MaxTrigrams];
            bigrams.Fill(-1);
            trigrams.Fill(-1);
            int symIndex = alphabet.Length;
            bool improved;
            do
            {
                improved = symIndex > 0 && TrySubstituteTrigrams(cur, work, ref length, alphabet[--symIndex], trigrams, ref dict) || symIndex > 0 && TrySubstituteBigrams(cur, work, ref length, alphabet[--symIndex], bigrams, ref dict);
            } while (improved && symIndex > 0);

            return length;
        }

        // ======================= SUBSTITUTION =======================
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static bool TrySubstituteBigrams(Span<char> input, Span<char> output, ref int length, char sym, Span<int> map, ref SymbolMap dict)
        {
            Span<int> counts = stackalloc int[MaxBigrams];
            counts.Clear();

            for (int i = 0; i < length - 1; i++)
                if (TryEncodeBigrams(input[i], input[i + 1], out int code) && map[code] < 0)
                    counts[code]++;

            int best = SelectBest(counts, 2);
            if (best < 0)
                return false;

            map[best] = sym;
            dict.Add(sym, best, 2);

            int p = 0;
            for (int i = 0; i < length;)
            {
                if (i < length - 1 &&
                    TryEncodeBigrams(input[i], input[i + 1], out int c) &&
                    c == best)
                {
                    output[p++] = sym;
                    i += 2;
                }
                else
                {
                    output[p++] = input[i++];
                }
            }

            output[..p].CopyTo(input);
            length = p;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static bool TrySubstituteTrigrams(Span<char> input, Span<char> output, ref int length, char sym, Span<int> map, ref SymbolMap dict)
        {
            Span<int> counts = stackalloc int[MaxTrigrams];
            counts.Clear();

            for (int i = 0; i < length - 2; i++)
                if (TryEncodeTrigrams(input[i], input[i + 1], input[i + 2], out int code) && map[code] < 0)
                    counts[code]++;

            int best = SelectBest(counts, 1);
            if (best < 0)
                return false;

            map[best] = sym;
            dict.Add(sym, best, 3);

            int p = 0;
            for (int i = 0; i < length;)
            {
                if (i < length - 2 &&
                    TryEncodeTrigrams(input[i], input[i + 1], input[i + 2], out int c) &&
                    c == best)
                {
                    output[p++] = sym;
                    i += 3;
                }
                else
                {
                    output[p++] = input[i++];
                }
            }

            output[..p].CopyTo(input);
            length = p;
            return true;
        }

        // ======================= UTILITIES =======================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SelectBest(ReadOnlySpan<int> counts, int minCount)
        {
            int best = -1, bestCount = minCount;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > bestCount)
                    best = i;
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryEncodeBigrams(char a, char b, out int code)
        {
            int v = (a - 'A') | (b - 'A');
            if ((uint)v > 3) { code = 0; return false; }
            code = ((a - 'A') << 2) | (b - 'A');
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryEncodeTrigrams(char a, char b, char c, out int code)
        {
            int v = (a - 'A') | (b - 'A') | (c - 'A');
            if ((uint)v > 3) { code = 0; return false; }
            code = ((a - 'A') << 4) | ((b - 'A') << 2) | (c - 'A');
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DecodeInto(Span<char> dst, int p, int code, int len)
        {
            for (int i = len - 1; i >= 0; i--)
            {
                dst[p + i] = (char)((code & 3) + 'A');
                code >>= 2;
            }
            return p + len;
        }
    }
}
