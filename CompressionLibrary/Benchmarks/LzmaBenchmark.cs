using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class LzmaBenchmark
    {
        private byte[] _data = null!;
        private byte[] _compressed = null!;

        [Params(65536, 262144, 1048576)]
        public int DataSize;

        [GlobalSetup]
        public void Setup()
        {
            _data = new byte[DataSize];
            var rnd = new Random(12345);
            rnd.NextBytes(_data);

            // Use the new overload without 'out' parameter
            _compressed = Lzma.Compress(_data);
        }

        [Benchmark(Baseline = true)]
        public byte[] Compress()
        {
            return Lzma.Compress(_data); // overload without 'out'
        }

        [Benchmark]
        public byte[] Decompress()
        {
            return Lzma.Decompress(_compressed); // overload without 'out'
        }
    }
}
