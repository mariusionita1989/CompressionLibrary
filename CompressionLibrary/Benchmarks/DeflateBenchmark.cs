using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class DeflateBenchmark
    {
        private byte[] _input = null!;
        private byte[] _outputBuffer = null!;
        private byte[] _compressedData = null!;
        private const int InputSize = 1000000; // 1 MB test case

        [GlobalSetup]
        public void Setup()
        {
            var rnd = new Random(12345);

            _input = new byte[InputSize];
            rnd.NextBytes(_input);

            using var ms = new MemoryStream();
            Deflate.Compress(_input, ms);

            _compressedData = ms.ToArray();
            _outputBuffer = new byte[InputSize * 3]; // safe margin
        }

        [Benchmark]
        public void Compress()
        {
            using var ms = new MemoryStream(capacity: (InputSize >> 1));
            Deflate.Compress(_input, ms);
        }

        [Benchmark]
        public void Decompress()
        {
            using var ms = new MemoryStream(_compressedData, writable: false);
            var written = Deflate.Decompress(ms, _outputBuffer);
        }
    }
}
