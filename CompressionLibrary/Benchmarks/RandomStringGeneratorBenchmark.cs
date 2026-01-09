using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class RandomStringGeneratorBenchmark
    {
        private char[] buffer = null!;

        [Params(64 * 1024, 256 * 1024, 1024 * 1024)] // Test small and large buffers
        public int Length;

        [GlobalSetup]
        public void Setup()
        {
            buffer = new char[Length];
        }

        [Benchmark(Baseline = true)]
        public void FillRandomAtoP_Baseline()
        {
            var span = buffer.AsSpan();
            RandomStringGenerator.FillRandomAtoP(span);
        }

        [Benchmark]
        public string FillRandomAtoP_ToString()
        {
            var span = buffer.AsSpan();
            RandomStringGenerator.FillRandomAtoP(span);
            return new string(span); // include conversion to string
        }
    }
}
