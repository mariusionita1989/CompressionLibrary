using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;
using System.Text;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class ZstdBenchmarks
    {
        private byte[]? _smallCompressible;
        private byte[]? _mediumCompressible;
        private byte[]? _largeCompressible;

        private byte[]? _smallIncompressible;
        private byte[]? _mediumIncompressible;
        private byte[]? _largeIncompressible;

        [GlobalSetup]
        public void GlobalSetup()
        {
            const int small = 1024;         // 1 KB
            const int medium = 128000;      // ~125 KB
            const int large = 4000000;     // ~4 MB

            _smallCompressible = GenerateCompressibleData(small);
            _mediumCompressible = GenerateCompressibleData(medium);
            _largeCompressible = GenerateCompressibleData(large);

            _smallIncompressible = GenerateIncompressibleData(small);
            _mediumIncompressible = GenerateIncompressibleData(medium);
            _largeIncompressible = GenerateIncompressibleData(large);
        }

        [Benchmark]
        public byte[] Compress_Small_Compressible() =>
            Zstd.Compress(_smallCompressible!);

        [Benchmark]
        public byte[] Decompress_Small_Compressible()
        {
            var compressed = Zstd.Compress(_smallCompressible!);
            return Zstd.Decompress(compressed);
        }

        [Benchmark]
        public byte[] Compress_Medium_Compressible() =>
            Zstd.Compress(_mediumCompressible!);

        [Benchmark]
        public byte[] Compress_Large_Compressible() =>
            Zstd.Compress(_largeCompressible!);

        [Benchmark]
        public byte[] Compress_Large_Incompressible() =>
            Zstd.Compress(_largeIncompressible!);

        [Benchmark]
        public void Compress_Stream_Small()
        {
            using var input = new MemoryStream(_smallCompressible!);
            using var output = new MemoryStream();
            Zstd.Compress(input, output);
        }

        [Benchmark]
        public void Decompress_Stream_Small()
        {
            var compressed = Zstd.Compress(_smallCompressible!);
            using var input = new MemoryStream(compressed);
            using var output = new MemoryStream();
            Zstd.Decompress(input, output);
        }

        [Benchmark]
        public async Task<byte[]> CompressAsync_Small_Compressible() =>
            await Zstd.CompressAsync(_smallCompressible!);

        [Benchmark]
        public async Task<byte[]> DecompressAsync_Small_Compressible()
        {
            var compressed = Zstd.Compress(_smallCompressible!);
            return await Zstd.DecompressAsync(compressed);
        }

        [Benchmark]
        public async Task CompressAsync_Stream_Small()
        {
            using var input = new MemoryStream(_smallCompressible!);
            using var output = new MemoryStream();
            await Zstd.CompressAsync(input, output);
        }

        // --- Helper methods ---

        private static byte[] GenerateCompressibleData(int length)
        {
            const string pattern = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG. ";
            var data = new byte[length];
            var patternBytes = Encoding.UTF8.GetBytes(pattern);
            for (int i = 0; i < length; i++)
            {
                data[i] = patternBytes[i % patternBytes.Length];
            }
            return data;
        }

        private static byte[] GenerateIncompressibleData(int length)
        {
            var data = new byte[length];
            new Random(42).NextBytes(data); // Fixed seed for reproducibility
            return data;
        }
    }
}
