using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Main;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class RansBenchmark
    {
        private const int SmallSize = 128;
        private const int MediumSize = 4 * 1024;
        private const int LargeSize = 256 * 1024;
        private char[] _small = null!;
        private char[] _medium = null!;
        private char[] _large = null!;
        private byte[] _encodedSmall = null!;
        private byte[] _encodedMedium = null!;
        private byte[] _encodedLarge = null!;

        // ------------------------------------------------------------
        // SETUP
        // ------------------------------------------------------------
        [GlobalSetup]
        public void Setup()
        {
            _small = new char[SmallSize];
            _medium = new char[MediumSize];
            _large = new char[LargeSize];

            RandomStringGenerator.FillRandomAtoP(_small);
            RandomStringGenerator.FillRandomAtoP(_medium);
            RandomStringGenerator.FillRandomAtoP(_large);

            // Pre-encode for decode benchmarks
            _encodedSmall = Rans.Encode(_small);
            _encodedMedium = Rans.Encode(_medium);
            _encodedLarge = Rans.Encode(_large);
        }

        // ------------------------------------------------------------
        // ENCODE
        // ------------------------------------------------------------
        [Benchmark]
        public byte[] Encode_Small() => Rans.Encode(_small);

        [Benchmark]
        public byte[] Encode_Medium() => Rans.Encode(_medium);

        [Benchmark]
        public byte[] Encode_Large() => Rans.Encode(_large);

        // ------------------------------------------------------------
        // DECODE
        // ------------------------------------------------------------
        [Benchmark]
        public string Decode_Small()
            => Rans.Decode(_encodedSmall);

        [Benchmark]
        public string Decode_Medium() => Rans.Decode(_encodedMedium);

        [Benchmark]
        public string Decode_Large() => Rans.Decode(_encodedLarge);

        // ------------------------------------------------------------
        // ROUND TRIP
        // ------------------------------------------------------------
        [Benchmark]
        public string RoundTrip_Medium()
        {
            var encoded = Rans.Encode(_medium);
            return Rans.Decode(encoded);
        }
    }
}
