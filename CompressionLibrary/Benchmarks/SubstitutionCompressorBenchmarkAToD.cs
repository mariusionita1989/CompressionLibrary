using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Helpers;
using CompressionLibrary.Main;
using System.Buffers;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class SubstitutionCompressorBenchmarkAToD
    {
        // ===== PARAMETERS =====
        [Params(262144, 1048576, 2097152)]
        public int Length;
        private string _input = null!;
        private string _compressed = null!;
        private SymbolMap _dictionary;

        // ===== SETUP =====
        [GlobalSetup]
        public void Setup()
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(Length);
            try
            {
                Span<char> span = buffer.AsSpan(0, Length);
                RandomStringGeneratorAToD.FillRandomAtoD(span);
                _input = new string(span);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }

            _compressed = SubstitutionCompressorAToD.Compress(_input, out _dictionary);
        }

        // ===== BENCHMARKS =====

        [Benchmark(Baseline = true)]
        public string Compress()
        {
            return SubstitutionCompressorAToD.Compress(_input, out _);
        }

        [Benchmark]
        public ReadOnlyMemory<char> Decompress()
        {
            return SubstitutionCompressorAToD.Decompress(_compressed.AsSpan(), _dictionary);
        }

        [Benchmark]
        public string RoundTrip()
        {
            string compressed = SubstitutionCompressorAToD.Compress(_input, out var dict);
            ReadOnlyMemory<char> decompressed = SubstitutionCompressorAToD.Decompress(compressed.AsSpan(), dict);
            return new string(decompressed.Span);
        }
    }
}
