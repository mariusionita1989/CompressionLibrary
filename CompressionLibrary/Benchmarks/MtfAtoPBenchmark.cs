using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class MtfAtoPBenchmark
    {
        private const char FirstChar = 'A';
        private const int AlphabetSize = 16;

        [Params(65536, 131072, 262144)]
        public int Length;

        private char[] _input = null!;
        private byte[] _encoded = null!;
        private char[] _decoded = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var random = new Random(42); // deterministic

            _input = new char[Length];
            for (int j = 0; j < Length; j++)
            {
                _input[j] = (char)(FirstChar + random.Next(AlphabetSize));
            }
            _encoded = new byte[Length];
            _decoded = new char[Length];

            // Pre-encode once so Decode has valid input
            MtfAtoP.Encode(_input, _encoded);
        }

        [Benchmark]
        public byte[] Encode()
        {
            MtfAtoP.Encode(_input, _encoded);
            return _encoded; // prevent dead code elimination
        }

        [Benchmark]
        public char[] Decode()
        {
            MtfAtoP.Decode(_encoded, _decoded);
            return _decoded;
        }
    }
}
