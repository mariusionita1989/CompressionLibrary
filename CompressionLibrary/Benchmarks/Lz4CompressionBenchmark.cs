using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class Lz4CompressionBenchmark
    {
        [Params(2048, 4096, 16384, 65536, 262144, 1048576)]
        public int InputSize;
        private byte[] _input = null!;
        private byte[] _compressed = null!;
        private byte[] _decompressed = null!;
        private int _compressedLength;

        [GlobalSetup]
        public void Setup()
        {
            _input = new byte[InputSize];
            _compressed = new byte[InputSize << 1];   
            _decompressed = new byte[InputSize];
            var rng = new Random(12345);
            rng.NextBytes(_input);
            _compressedLength = Lz4.Compress(_input, _compressed);
            if (_compressedLength == 0)
                throw new Exception("Compression failed during setup.");

            int decompressedLength = Lz4.Decompress(new ReadOnlySpan<byte>(_compressed, 0, _compressedLength), _decompressed);
            if (decompressedLength != InputSize)
                throw new Exception("Decompression size mismatch.");

            for (int i = 0; i < InputSize; i++)
            {
                if (_input[i] != _decompressed[i])
                    throw new Exception("Round–trip failed: data mismatch.");
            }
        }

        [Benchmark]
        public int Compress()
        {
            return Lz4.Compress(_input, _compressed);
        }

        [Benchmark]
        public int Decompress()
        {
            return Lz4.Decompress(new ReadOnlySpan<byte>(_compressed, 0, _compressedLength), _decompressed);
        }
    }
}
