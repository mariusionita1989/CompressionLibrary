using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;
using System.Text;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class GZipSizeBenchmark
    {
        private byte[]? _smallBytes, _mediumBytes, _largeBytes;
        private string? _smallString, _mediumString, _largeString;
        private byte[]? _smallCompressed, _mediumCompressed, _largeCompressed;

        [GlobalSetup]
        public void Setup()
        {
            // Small payload (~100 bytes)
            _smallString = "The quick brown fox jumps over the lazy dog.";
            _smallBytes = Encoding.UTF8.GetBytes(_smallString);
            _smallCompressed = GZip.Compress(_smallBytes!);

            // Medium payload (~10 KB)
            _mediumString = string.Concat(Enumerable.Repeat(_smallString, 200));
            _mediumBytes = Encoding.UTF8.GetBytes(_mediumString);
            _mediumCompressed = GZip.Compress(_mediumBytes!);

            // Large payload (~1 MB)
            _largeString = string.Concat(Enumerable.Repeat(_smallString, 20000));
            _largeBytes = Encoding.UTF8.GetBytes(_largeString);
            _largeCompressed = GZip.Compress(_largeBytes!);
        }

        // === Compression Benchmarks ===
        [Benchmark] public byte[] Compress_Small_Bytes() => GZip.Compress(_smallBytes!);
        [Benchmark] public byte[] Compress_Medium_Bytes() => GZip.Compress(_mediumBytes!);
        [Benchmark] public byte[] Compress_Large_Bytes() => GZip.Compress(_largeBytes!);

        [Benchmark] public byte[] Compress_Small_String() => GZip.Compress(_smallString!);
        [Benchmark] public byte[] Compress_Medium_String() => GZip.Compress(_mediumString!);
        [Benchmark] public byte[] Compress_Large_String() => GZip.Compress(_largeString!);

        // === Decompression Benchmarks ===
        [Benchmark] public byte[] Decompress_Small() => GZip.Decompress(_smallCompressed!);
        [Benchmark] public byte[] Decompress_Medium() => GZip.Decompress(_mediumCompressed!);
        [Benchmark] public byte[] Decompress_Large() => GZip.Decompress(_largeCompressed!);

        [Benchmark]
        public async Task<byte[]> DecompressAsync_Small()
            => await GZip.DecompressAsync(_smallCompressed!);

        [Benchmark]
        public async Task<byte[]> DecompressAsync_Medium()
            => await GZip.DecompressAsync(_mediumCompressed!);

        [Benchmark]
        public async Task<byte[]> DecompressAsync_Large()
            => await GZip.DecompressAsync(_largeCompressed!);

        [Benchmark]
        public string DecompressToString_Small()
            => GZip.DecompressToString(_smallCompressed!);

        [Benchmark]
        public string DecompressToString_Medium()
            => GZip.DecompressToString(_mediumCompressed!);

        [Benchmark]
        public string DecompressToString_Large()
            => GZip.DecompressToString(_largeCompressed!);

        [Benchmark]
        public async Task<string> DecompressToStringAsync_Small()
            => await GZip.DecompressToStringAsync(_smallCompressed!);

        [Benchmark]
        public async Task<string> DecompressToStringAsync_Medium()
            => await GZip.DecompressToStringAsync(_mediumCompressed!);

        [Benchmark]
        public async Task<string> DecompressToStringAsync_Large()
            => await GZip.DecompressToStringAsync(_largeCompressed!);
    }
}
