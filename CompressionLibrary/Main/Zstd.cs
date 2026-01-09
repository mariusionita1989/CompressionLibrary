using System.Runtime.CompilerServices;
using ZstdNet;

namespace CompressionLibrary.Main
{
    public static class Zstd
    {
        private const int DefaultCompressionLevel = 3;
        private static readonly CompressionOptions DefaultOptions = new(DefaultCompressionLevel);

        private static CompressionOptions GetOptions(int compressionLevel)
        {
            return compressionLevel == DefaultCompressionLevel ? DefaultOptions : new CompressionOptions(compressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Compress(byte[] data, int compressionLevel = DefaultCompressionLevel)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return Array.Empty<byte>();

            var options = GetOptions(compressionLevel);
            using var compressor = new Compressor(options);
            return compressor.Wrap(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
            if (compressedData.Length == 0) return Array.Empty<byte>();
            using var decompressor = new Decompressor();
            return decompressor.Unwrap(compressedData);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Compress(Stream input, Stream output, int compressionLevel = DefaultCompressionLevel)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            var options = GetOptions(compressionLevel);
            using var compressionStream = new CompressionStream(output, options);
            input.CopyTo(compressionStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Decompress(Stream input, Stream output)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            using var decompressionStream = new DecompressionStream(input);
            decompressionStream.CopyTo(output);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<byte[]> CompressAsync(byte[] data, int compressionLevel = DefaultCompressionLevel)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return Task.Run(() => Compress(data, compressionLevel));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<byte[]> DecompressAsync(byte[] compressedData)
        {
            if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
            return Task.Run(() => Decompress(compressedData));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async Task CompressAsync(Stream input, Stream output, int compressionLevel = DefaultCompressionLevel)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            var options = GetOptions(compressionLevel);
            await using var compressionStream = new CompressionStream(output, options);
            await input.CopyToAsync(compressionStream).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async Task DecompressAsync(Stream input, Stream output)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (output == null) throw new ArgumentNullException(nameof(output));

            await using var decompressionStream = new DecompressionStream(input);
            await decompressionStream.CopyToAsync(output).ConfigureAwait(false);
        }
    }
}
