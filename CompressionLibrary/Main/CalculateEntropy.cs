using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class CalculateEntropy
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static double GetEntropy(ReadOnlySpan<char> input)
        {
            if (input.IsEmpty)
                return 0.0;
            const int symbolCount = 16; // A-P
            Span<int> frequencies = stackalloc int[symbolCount];
            foreach (var c in input)
            {
                int index = c - 'A';
                if (index >= 0 && index < symbolCount)
                    frequencies[index]++;
                else
                    throw new ArgumentException("Input contains invalid characters.");
            }
            double entropy = 0.0;
            int length = input.Length;
            for (int i = 0; i < symbolCount; i++)
            {
                if (frequencies[i] == 0) continue;

                double p = (double)frequencies[i] / length;
                entropy -= p * Math.Log2(p);
            }

            return entropy;
        }
    }
}
