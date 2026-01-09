using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Helpers;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class Lz77Benchmark
    {
        private string _input = null!;
        private Token[] _tokens = null!;
        private byte[] _packed = null!;

        [Params(1024, 16384, 1048576)]
        public int InputSize;

        [GlobalSetup]
        public void Setup()
        {
            var buffer = new char[InputSize];
            RandomStringGenerator.FillRandomAtoP(buffer);
            _input = new string(buffer);

            _tokens = Lz77.Compress(_input.AsSpan());

            // Allocate exactly 3 bytes per token (24 bits)
            _packed = new byte[_tokens.Length * 3];
            int actualSize = Lz77.PackTokens(_tokens, _packed);

            // Trim if needed (shouldn't be, but safe)
            if (actualSize < _packed.Length)
                Array.Resize(ref _packed, actualSize);
        }

        [Benchmark(Baseline = true)]
        public Token[] CompressToTokens()
        {
            return Lz77.Compress(_input.AsSpan());
        }

        [Benchmark]
        public byte[] CompressToBytes()
        {
            var tokens = Lz77.Compress(_input.AsSpan());
            var output = new byte[tokens.Length * 3];
            int size = Lz77.PackTokens(tokens, output);
            return size == output.Length ? output : output[..size].ToArray();
        }

        [Benchmark]
        public string DecompressFromTokens()
        {
            return Lz77.Decompress(_tokens);
        }

        [Benchmark]
        public string DecompressFromBytes()
        {
            var tokens = new Token[_tokens.Length];
            int count = Lz77.UnpackTokens(_packed, tokens);
            return Lz77.Decompress(tokens.AsSpan(..count));
        }
    }
}
