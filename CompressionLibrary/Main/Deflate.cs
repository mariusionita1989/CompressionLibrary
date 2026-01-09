using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class Deflate
    {
        private const int BufferSize = 8192;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Compress(ReadOnlySpan<byte> input, Stream outputStream)
        {
            if (input.IsEmpty) return;

            using var deflate = new DeflateStream(outputStream, CompressionLevel.SmallestSize, leaveOpen: true);
            int offset = 0;
            while (offset < input.Length)
            {
                int size = Math.Min(BufferSize, input.Length - offset);
                deflate.Write(input.Slice(offset, size));
                offset += size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int Decompress(Stream inputStream, Span<byte> output)
        {
            using var deflate = new DeflateStream(inputStream, CompressionMode.Decompress, leaveOpen: true);
            int totalWritten = 0;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                while (true)
                {
                    int bytesRead = deflate.Read(buffer, 0, BufferSize);
                    if (bytesRead == 0) break;

                    if (totalWritten + bytesRead > output.Length)
                        ThrowOutputBufferTooSmall();

                    buffer.AsSpan(0, bytesRead).CopyTo(output.Slice(totalWritten));
                    totalWritten += bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer); // Do NOT clear — improves perf significantly
            }

            return totalWritten;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void ThrowOutputBufferTooSmall()
        {
            throw new ArgumentException("Output buffer too small for decompressed data.");
        }
    }
}
