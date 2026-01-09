using CompressionLibrary.Main;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    public static class RangeCoderHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressAndDecompressString(int dataSize, int alphabetSize, int precision)
        {
            Console.WriteLine("Generating random string...");
            Span<char> data = dataSize <= 1000000 ? stackalloc char[dataSize] : new char[dataSize];
            RandomStringGenerator.FillRandomAtoP(data);
            Span<int> symbols = dataSize <= 1000000 ? stackalloc int[dataSize] : new int[dataSize];
            for (int i = 0; i < dataSize; i++)
                symbols[i] = Math.Min(data[i] - 'A', alphabetSize - 1);
            double entropy = CalcEntropy(symbols);
            int[] cmArray = new int[alphabetSize + 2];
            Span<int> cm = cmArray;
            MakeRanges(symbols, cm, alphabetSize, precision);
            int estimatedSize = (int)(entropy * dataSize / 8) + 1024;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(estimatedSize);

            try
            {
                var encodeTimer = Stopwatch.StartNew();
                var encoder = new RangeEncoder(buffer.AsSpan(), precision);
                for (int i = 0; i < dataSize; i++)
                    encoder.Encode(cm[symbols[i]], cm[symbols[i] + 1]);
                encoder.Encode(cm[alphabetSize], cm[alphabetSize + 1]); // EOD
                encoder.Flush();
                encodeTimer.Stop();

                int encodedSize = encoder.Position;
                int extraBytes = encoder.ExtraBytes;

                var decodeTimer = Stopwatch.StartNew();
                var decoder = new RangeDecoder(buffer.AsSpan(), precision);
                bool ok = true;
                int decoded = 0;
                while (true)
                {
                    int sym = decoder.Decode(cm, alphabetSize);
                    if (sym == alphabetSize) break; // EOD
                    if (sym != symbols[decoded++]) { ok = false; break; }
                }
                ok &= decoded == dataSize;
                decodeTimer.Stop();

                long inputBytes = dataSize * sizeof(char);
                long outputBytes = encodedSize;

                Console.WriteLine($"Encode time: {encodeTimer.Elapsed.TotalSeconds:F3}s | Decode time: {decodeTimer.Elapsed.TotalSeconds:F3}s");
                Console.WriteLine($"Input size: {inputBytes:N0} bytes | Output size: {outputBytes:N0} bytes | Compression ratio: {(double)inputBytes / outputBytes:F3}");
                Console.WriteLine(ok ? "Round-trip OK" : "Mismatch");
                Console.WriteLine($"Expected compressed size (entropy estimate): {(int)(entropy * dataSize / 8):N0} bytes | Actual: {encodedSize:N0} bytes (extra: {extraBytes})");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static double CalcEntropy(ReadOnlySpan<int> data)
        {
            int min = data[0], max = data[0];
            foreach (var v in data) { if (v < min) min = v; if (v > max) max = v; }

            Span<int> freq = stackalloc int[max - min + 1];
            foreach (var v in data) freq[v - min]++;

            double entropy = 0, log2 = Math.Log(2), total = data.Length;
            foreach (var f in freq)
            {
                if (f > 0)
                {
                    double p = f / total;
                    entropy += p * Math.Log(p) / log2;
                }
            }
            return -entropy;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void MakeRanges(ReadOnlySpan<int> data, Span<int> cm, int alphabetSize, int precision)
        {
            Span<int> freq = stackalloc int[alphabetSize];
            foreach (var s in data) freq[s]++;

            cm[0] = 0;
            for (int i = 0; i < alphabetSize; i++)
                cm[i + 1] = cm[i] + freq[i];

            int total = cm[alphabetSize];
            ulong scale = ((ulong)1 << 32) / (ulong)total;

            for (int i = 0; i <= alphabetSize; i++)
                cm[i] = (int)(((ulong)cm[i] * scale >> (32 - precision)) & 0xFFFFFFFF);

            cm[alphabetSize + 1] = (1 << precision) - 1;

            for (int i = 0; i < alphabetSize; i++) if (cm[i + 1] <= cm[i]) cm[i + 1] = cm[i] + 1;
            for (int i = alphabetSize; i >= 0; i--) if (cm[i] >= cm[i + 1]) cm[i] = cm[i + 1] - 1;
        }
    }
}
