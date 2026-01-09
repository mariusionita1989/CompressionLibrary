using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CompressionLibrary.Helpers;
using CompressionLibrary.Main;
using System.Buffers;

namespace CompressionLibrary.Benchmarks
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class ZlibBenchmark
    {
        private byte[] _testData = default!;
        private byte[] _poolBuffer = default!;
        private ZlibBuffer _precompressed = default!; // prewarm buffer for decompression

        [Params(4096, 16384, 65536)]
        public int InputSizeInBytes;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var random = new Random(42); // deterministic seed
            _testData = new byte[InputSizeInBytes];
            random.NextBytes(_testData);

            // preallocate ArrayPool buffer for zero-allocation "ToArray" benchmarks
            _poolBuffer = ArrayPool<byte>.Shared.Rent(InputSizeInBytes * 2);

            // prewarm ArrayPool buffer to touch all pages (reduce page faults)
            for (int i = 0; i < _poolBuffer.Length; i += 4096)
                _poolBuffer[i] = 0;

            // prewarm compressed buffer to avoid repeated compression in decompression benchmarks
            _precompressed = Zlib.Compress(_testData, System.IO.Compression.CompressionLevel.Fastest);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            ArrayPool<byte>.Shared.Return(_poolBuffer, clearArray: true);
        }

        // === Zero-allocation compress/decompress ===
        [Benchmark]
        public ZlibBuffer Compress_ZeroAlloc()
        {
            return Zlib.Compress(_testData, System.IO.Compression.CompressionLevel.Fastest);
        }

        [Benchmark]
        public ZlibBuffer Decompress_ZeroAlloc()
        {
            return Zlib.Decompress(_precompressed.Span);
        }

        [Benchmark]
        public bool RoundTrip_ZeroAlloc()
        {
            var decompressed = Zlib.Decompress(_precompressed.Span);
            return _testData.AsSpan().SequenceEqual(decompressed.Span);
        }

        // === Zero-allocation "ToArray"-like using preallocated ArrayPool buffer ===
        [Benchmark]
        public int Compress_ToPoolBuffer()
        {
            var buffer = Zlib.Compress(_testData, System.IO.Compression.CompressionLevel.Fastest);
            var length = buffer.Span.Length;
            buffer.Span.CopyTo(_poolBuffer.AsSpan(0, length));
            return length;
        }

        [Benchmark]
        public int Decompress_ToPoolBuffer()
        {
            var decompressed = Zlib.Decompress(_precompressed.Span);
            var length = decompressed.Span.Length;
            decompressed.Span.CopyTo(_poolBuffer.AsSpan(0, length));
            return length;
        }
    }
}
