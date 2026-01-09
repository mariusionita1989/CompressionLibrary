using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class BwtAToPBenchmark
    {
        private char[] input = null!;
        private string encoded = null!;

        [Params(64 * 1024, 256 * 1024, 1024 * 1024)]
        public int N;

        [GlobalSetup]
        public void Setup()
        {
            input = new char[N];
            RandomStringGenerator.FillRandomAtoP(input);
            encoded = BwtAToP.Encode(input);
        }

        [Benchmark]
        public string EncodeZeroAlloc()
        {
            return BwtAToP.Encode(input);
        }

        [Benchmark]
        public string DecodeZeroAlloc()
        {
            return BwtAToP.Decode(encoded);
        }
    }
}
