using CompressionLibrary.Helpers;
using SevenZip.Compression.LZMA;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class Lzma
    {
        private const int HeaderSize = 5 + 8; // properties + uncompressed size

        // Reusable (thread-static) encoder and decoder – removes per-call allocation costs
        [ThreadStatic] private static Encoder _encoder = null!;
        [ThreadStatic] private static Decoder _decoder = null!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Encoder GetEncoder() => _encoder ??= new Encoder();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Decoder GetDecoder() => _decoder ??= new Decoder();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] Compress(ReadOnlySpan<byte> input)
        {
            var encoder = GetEncoder();

            // Borrow a buffer large enough for input
            byte[] inBuffer = ArrayPool<byte>.Shared.Rent(input.Length);
            input.CopyTo(inBuffer);

            using var inStream = new PooledStream(inBuffer, input.Length);
            using var outStream = new MemoryStream(input.Length);

            encoder.WriteCoderProperties(outStream);

            long size = input.Length;
            for (int i = 0; i < 8; i++)
                outStream.WriteByte((byte)(size >> (8 * i)));

            encoder.Code(inStream, outStream, input.Length, -1, null);

            var result = outStream.ToArray();
            ArrayPool<byte>.Shared.Return(inBuffer);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static byte[] Decompress(ReadOnlySpan<byte> input)
        {
            var decoder = GetDecoder();

            // Rent buffer for input
            byte[] inBuffer = ArrayPool<byte>.Shared.Rent(input.Length);
            input.CopyTo(inBuffer);

            using var inStream = new PooledStream(inBuffer, input.Length);
            using var outStream = new MemoryStream();

            Span<byte> properties = stackalloc byte[5];
            if (inStream.Read(properties) != 5)
                throw new InvalidDataException("Invalid LZMA stream: missing properties");

            decoder.SetDecoderProperties(properties.ToArray());

            long outSize = 0;
            Span<byte> sizeBuf = stackalloc byte[8];

            if (inStream.Read(sizeBuf) != 8)
                throw new InvalidDataException("Invalid LZMA stream: missing size header");

            for (int i = 0; i < 8; i++)
                outSize |= (long)sizeBuf[i] << (8 * i);

            decoder.Code(inStream, outStream, inStream.Remaining, outSize, null);

            ArrayPool<byte>.Shared.Return(inBuffer);
            return outStream.ToArray();
        }
    }
}
