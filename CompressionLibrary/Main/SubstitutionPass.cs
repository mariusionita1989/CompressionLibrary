using CompressionLibrary.Helpers;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace CompressionLibrary.Main
{
    internal static class SubstitutionPass
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static (string Output, Dictionary<char, string> Decode) Compress(string input, ReadOnlySpan<char> substAlphabet)
        {
            if (string.IsNullOrEmpty(input))
                return (input, new());

            ReadOnlySpan<char> s = input.AsSpan();
            int len = s.Length;

            var counts = new Dictionary<uint, int>(4096);

            for (int i = 0; i < len; i++)
            {
                if (i + 1 < len)
                    Increment(counts, Pack2(s[i], s[i + 1]));

                if (i + 2 < len)
                    Increment(counts, Pack3(s[i], s[i + 1], s[i + 2]));
            }

            Span<Pattern> best = stackalloc Pattern[substAlphabet.Length];
            int bestCount = 0;

            foreach (var kv in counts)
            {
                uint key = kv.Key;
                int freq = kv.Value;

                byte plen = (key & 0xFF000000) != 0 ? (byte)3 : (byte)2;
                int gain = freq * (plen - 1);
                if (gain <= 1) continue;

                InsertBest(best, ref bestCount, new Pattern
                {
                    Key = key,
                    Length = plen,
                    Gain = gain
                });
            }

            var encode = new Dictionary<uint, char>(bestCount);
            var decode = new Dictionary<char, string>(bestCount);

            for (int i = 0; i < bestCount; i++)
            {
                char symbol = substAlphabet[i];
                encode[best[i].Key] = symbol;
                decode[symbol] = Unpack(best[i].Key, best[i].Length);
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(len);
            int outPos = 0;

            for (int p = 0; p < len;)
            {
                if (p + 2 < len &&
                    encode.TryGetValue(Pack3(s[p], s[p + 1], s[p + 2]), out char c3))
                {
                    buffer[outPos++] = c3;
                    p += 3;
                }
                else if (p + 1 < len &&
                         encode.TryGetValue(Pack2(s[p], s[p + 1]), out char c2))
                {
                    buffer[outPos++] = c2;
                    p += 2;
                }
                else
                {
                    buffer[outPos++] = s[p++];
                }
            }

            string result = new string(buffer, 0, outPos);
            ArrayPool<char>.Shared.Return(buffer);
            return (result, decode);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Decompress(string input, Dictionary<char, string> dict)
        {
            var sb = new StringBuilder(input.Length << 1);

            foreach (char c in input)
            {
                if (dict.TryGetValue(c, out var repl))
                    sb.Append(repl);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Increment(Dictionary<uint, int> d, uint key) => d[key] = d.TryGetValue(key, out int v) ? v + 1 : 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack2(char a, char b) => (uint)((byte)a | ((uint)(byte)b << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack3(char a, char b, char c) => (uint)((byte)a | ((uint)(byte)b << 8) | ((uint)(byte)c << 16) | 0xFF000000);
        private static string Unpack(uint key, int len)
        {
            Span<char> s = stackalloc char[len];
            s[0] = (char)(key & 0xFF);
            s[1] = (char)((key >> 8) & 0xFF);
            if (len == 3)
                s[2] = (char)((key >> 16) & 0xFF);
            return new string(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void InsertBest(Span<Pattern> best, ref int count, Pattern p)
        {
            int i = count < best.Length ? count++ : best.Length - 1;
            if (i < best.Length && best[i].Gain >= p.Gain) return;

            while (i > 0 && best[i - 1].Gain < p.Gain)
            {
                best[i] = best[i - 1];
                i--;
            }
            best[i] = p;
        }
    }
}
