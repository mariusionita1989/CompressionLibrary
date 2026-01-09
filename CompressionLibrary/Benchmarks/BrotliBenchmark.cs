using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class BrotliBenchmark
    {
        private string SmallText = "";
        private string MediumText = "";
        private string LargeText = "";
        private string SmallCompressed = "";
        private string MediumCompressed = "";
        private string LargeCompressed = "";

        [GlobalSetup]
        public void Setup()
        {
            SmallText = new string('A', 128);                       // 128 bytes
            MediumText = new string('B', 1024 * 1024);              // 1 MB
            LargeText = new string('C', 4 * 1024 * 1024);          // 4 MB
            SmallCompressed = Brotli.Compress(SmallText);
            MediumCompressed = Brotli.Compress(MediumText);
            LargeCompressed = Brotli.Compress(LargeText);
        }

        // -------------------------
        // Compress Benchmarks
        // -------------------------

        [Benchmark]
        public string Compress_Small() => Brotli.Compress(SmallText);

        [Benchmark]
        public string Compress_1MB() => Brotli.Compress(MediumText);

        [Benchmark]
        public string Compress_4MB() => Brotli.Compress(LargeText);

        // -------------------------
        // Decompress Benchmarks
        // -------------------------

        [Benchmark]
        public string Decompress_Small() => Brotli.Decompress(SmallCompressed);

        [Benchmark]
        public string Decompress_1MB() => Brotli.Decompress(MediumCompressed);

        [Benchmark]
        public string Decompress_4MB() => Brotli.Decompress(LargeCompressed);
    }
}
