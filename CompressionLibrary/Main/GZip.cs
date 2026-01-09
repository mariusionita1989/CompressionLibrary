using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace CompressionLibrary.Main
{
    public static class GZip
    {
        private const int MaxStack = 32768;
        private const int BufferSize = 8192;

        // === Compression: always use ArrayPool (safe and fast) ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Compress(ReadOnlySpan<byte> input)
        {
            if (input.Length == 0) return Array.Empty<byte>();

            using var ms = new MemoryStream();
            // Dispose GZipStream to flush header/footer
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(input);
            }

            return ms.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Compress(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();

            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                int written = Encoding.UTF8.GetBytes(s.AsSpan(), rented);
                return Compress(rented.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async ValueTask<byte[]> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
        {
            if (input.Length == 0) return Array.Empty<byte>();

            using var ms = new MemoryStream();
            await using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                await gzip.WriteAsync(input, ct).ConfigureAwait(false);
            }

            return ms.ToArray();
        }

        // === Decompression: use stack for small inputs ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(ReadOnlySpan<byte> compressed)
        {
            if (compressed.Length == 0) return Array.Empty<byte>();

            if (compressed.Length <= MaxStack)
            {
                Span<byte> dst = stackalloc byte[MaxStack];
                using var input = new MemoryStream(compressed.ToArray());
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                int written = gzip.Read(dst);
                return dst[..written].ToArray();
            }

            return DecompressHeap(compressed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DecompressToString(ReadOnlySpan<byte> compressed)
        {
            if (compressed.Length == 0) return string.Empty;

            if (compressed.Length <= MaxStack)
            {
                Span<byte> bytes = stackalloc byte[MaxStack];
                using var input = new MemoryStream(compressed.ToArray());
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                int byteCount = gzip.Read(bytes);
                if (byteCount <= MaxStack)
                {
                    Span<char> chars = stackalloc char[MaxStack];
                    int charCount = Encoding.UTF8.GetChars(bytes[..byteCount], chars);
                    return new string(chars[..charCount]);
                }
            }

            return Encoding.UTF8.GetString(DecompressHeap(compressed));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async ValueTask<byte[]> DecompressAsync(ReadOnlyMemory<byte> compressed, CancellationToken ct = default)
        {
            if (compressed.Length == 0) return Array.Empty<byte>();

            using var input = new MemoryStream(compressed.ToArray());
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);

            using var output = new MemoryStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                int read;
                while ((read = await gzip.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static async ValueTask<string> DecompressToStringAsync(ReadOnlyMemory<byte> compressed, CancellationToken ct = default) => Encoding.UTF8.GetString(await DecompressAsync(compressed, ct).ConfigureAwait(false));

        // === Heap fallbacks ===

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static byte[] DecompressHeap(ReadOnlySpan<byte> compressed)
        {
            using var input = new MemoryStream(compressed.ToArray());
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
