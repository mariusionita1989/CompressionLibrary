using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace CompressionLibrary.Main
{
    public static class Brotli
    {
        private const int BufferSize = 8192;
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Compress(string text)
        {
            int maxUtf8ByteCount = Utf8NoBom.GetMaxByteCount(text.Length);
            byte[]? rentedBuffer = null;
            byte[]? rentedCompressed = null;

            try
            {
                rentedBuffer = ArrayPool<byte>.Shared.Rent(maxUtf8ByteCount);
                int actualByteCount = Utf8NoBom.GetBytes(text, rentedBuffer);
                ReadOnlySpan<byte> utf8Span = rentedBuffer.AsSpan(0, actualByteCount);
                int compressedBufferLength = Math.Max(actualByteCount + 256, BufferSize);
                rentedCompressed = ArrayPool<byte>.Shared.Rent(compressedBufferLength);
                using var output = new MemoryStream(rentedCompressed);
                using var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true);
                brotli.Write(utf8Span);
                brotli.Flush(); // Ensure all data is written
                int compressedLength = (int)output.Position;
                return Convert.ToBase64String(rentedCompressed.AsSpan(0, compressedLength));
            }
            finally
            {
                if (rentedBuffer is not null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                if (rentedCompressed is not null)
                    ArrayPool<byte>.Shared.Return(rentedCompressed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Decompress(string base64)
        {
            int base64Length = base64.Length;
            int maxDecodedLength = ((base64Length + 3) >> 2) * 3; // Upper bound
            byte[]? rentedInput = null;
            byte[]? rentedOutput = null;

            try
            {
                rentedInput = ArrayPool<byte>.Shared.Rent(maxDecodedLength);
                int actualInputLength = Convert.TryFromBase64String(base64, rentedInput, out actualInputLength) ? actualInputLength : throw new ArgumentException("Invalid base64 string.");
                int outputBufferSize = Math.Max(actualInputLength << 2, BufferSize);
                rentedOutput = ArrayPool<byte>.Shared.Rent(outputBufferSize);
                using var inputStream = new MemoryStream(rentedInput, 0, actualInputLength);
                using var brotli = new BrotliStream(inputStream, CompressionMode.Decompress);
                int totalRead = 0;
                int bytesRead;
                Span<byte> outputSpan = rentedOutput;
                while ((bytesRead = brotli.Read(outputSpan[totalRead..])) > 0)
                {
                    totalRead += bytesRead;
                    if (totalRead + BufferSize > outputSpan.Length)
                    {
                        // Grow buffer
                        var newBuffer = ArrayPool<byte>.Shared.Rent(outputSpan.Length << 1);
                        rentedOutput.AsSpan(0, totalRead).CopyTo(newBuffer);
                        ArrayPool<byte>.Shared.Return(rentedOutput);
                        rentedOutput = newBuffer;
                        outputSpan = rentedOutput;
                    }
                }

                return Utf8NoBom.GetString(rentedOutput.AsSpan(0, totalRead));
            }
            finally
            {
                if (rentedInput is not null)
                    ArrayPool<byte>.Shared.Return(rentedInput);
                if (rentedOutput is not null)
                    ArrayPool<byte>.Shared.Return(rentedOutput);
            }
        }
    }
}
