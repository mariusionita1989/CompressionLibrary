using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Helpers;
using CompressionLibrary.Main;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class SubstitutionCompressorBenchmarks
    {
        // You can expand this list as needed
        [Params(64 * 1024, 256 * 1024, 1024 * 1024)]
        public int Length;

        private string _input = null!;
        private CompressedResult _compressed = null!;

        // ---------------------------
        // Global setup
        // ---------------------------
        [GlobalSetup]
        public void Setup()
        {
            var buffer = new char[Length];
            RandomStringGenerator.FillRandomAtoP(buffer);
            _input = new string(buffer);

            // Pre-compress once for decompression benchmark
            _compressed = SubstitutionCompressor.CompressTwoPass(_input);
        }

        // ---------------------------
        // Compression
        // ---------------------------
        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public CompressedResult Compress_TwoPass()
        {
            return SubstitutionCompressor.CompressTwoPass(_input);
        }

        // ---------------------------
        // Decompression
        // ---------------------------
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public string Decompress_TwoPass()
        {
            return SubstitutionCompressor.DecompressTwoPass(_compressed);
        }

        // ---------------------------
        // Full round-trip
        // ---------------------------
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public string RoundTrip_Compress_Decompress()
        {
            var compressed = SubstitutionCompressor.CompressTwoPass(_input);
            var decompressed = SubstitutionCompressor.DecompressTwoPass(compressed);
            if (!ReferenceEquals(decompressed, _input) && decompressed != _input)
                throw new InvalidOperationException("Round-trip corruption detected.");

            return decompressed;
        }
    }
}
