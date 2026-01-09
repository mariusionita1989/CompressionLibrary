using CompressionLibrary.Helpers;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class BwtAToP
    {
        private const int AlphabetSize = 16;       // A–P
        private const int IndexEncodedLength = 6;  // 24-bit index

        /* ============================================================
         * ENCODE
         * ============================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static string Encode(ReadOnlySpan<char> input)
        {
            int n = input.Length;
            if (n == 0)
                return string.Empty;

            // Rent suffix index array
            int[] indices = ArrayPool<int>.Shared.Rent(n);
            for (int i = 0; i < n; i++)
                indices[i] = i;

            // Sort suffixes (unchanged algorithmic choice)
            Array.Sort(indices, 0, n, new SpanSuffixComparer(input));

            char[] output = new char[n + IndexEncodedLength];
            int originalIndex = 0;

            // Build BWT column
            for (int i = 0; i < n; i++)
            {
                int src = indices[i];
                output[i + IndexEncodedLength] =
                    input[(uint)(src - 1) < (uint)n ? src - 1 : n - 1];

                if (src == 0)
                    originalIndex = i;
            }

            // Encode original row index (base-16 A–P)
            int v = originalIndex;
            for (int i = IndexEncodedLength - 1; i >= 0; i--)
            {
                output[i] = (char)('A' + (v & 0xF));
                v >>= 4;
            }

            ArrayPool<int>.Shared.Return(indices, clearArray: false);
            return new string(output);
        }

        /* ============================================================
         * DECODE
         * ============================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static string Decode(ReadOnlySpan<char> input)
        {
            if ((uint)input.Length < IndexEncodedLength)
                throw new ArgumentException("Input too short.");

            int n = input.Length - IndexEncodedLength;
            if (n == 0)
                return string.Empty;

            // Decode original row index
            int originalIndex = 0;
            for (int i = 0; i < IndexEncodedLength; i++)
                originalIndex = (originalIndex << 4) | (input[i] - 'A');

            ReadOnlySpan<char> bwt = input.Slice(IndexEncodedLength, n);

            // Frequency count (stackalloc)
            Span<int> freq = stackalloc int[AlphabetSize];
            for (int i = 0; i < n; i++)
                freq[bwt[i] - 'A']++;

            // Prefix sums in-place (C array)
            int sum = 0;
            for (int i = 0; i < AlphabetSize; i++)
            {
                int tmp = freq[i];
                freq[i] = sum;
                sum += tmp;
            }

            // LF mapping (L → F)
            int[] next = ArrayPool<int>.Shared.Rent(n);
            for (int i = 0; i < n; i++)
            {
                int c = bwt[i] - 'A';
                next[i] = freq[c]++;
            }

            // Reconstruct original string (backwards)
            char[] output = new char[n];
            int idx = originalIndex;

            for (int i = n - 1; i >= 0; i--)
            {
                output[i] = bwt[idx];
                idx = next[idx];
            }

            ArrayPool<int>.Shared.Return(next, clearArray: false);
            return new string(output);
        }
    }
}
