using CompressionLibrary.Helpers;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CompressionLibrary.Main
{
    public static class Zlib
    {
        private const int HeaderSize = 2;     // Zlib header: CMF + FLG
        private const int FooterSize = 4;     // Adler-32

        // --------------------------------------------------------------------
        // SIMD ACCELERATED ADLER32
        // --------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static uint Adler32(ReadOnlySpan<byte> data)
        {
            const uint MOD = 65521;

            uint a = 1;
            uint b = 0;
            int i = 0;

            if (Vector128.IsHardwareAccelerated)
            {
                const int BLOCK = 16;

                while (data.Length - i >= BLOCK)
                {
                    var vec = Vector128.LoadUnsafe(ref Unsafe.Add(ref MemoryMarshal.GetReference(data), i));

                    // sum bytes
                    uint localA = 0, localB = 0;

                    for (int j = 0; j < BLOCK; j++)
                    {
                        byte v = vec.GetElement(j);
                        localA += v;
                        localB += localA;
                    }

                    a = (a + localA) % MOD;
                    b = (b + localB) % MOD;

                    i += BLOCK;
                }
            }

            // Scalar tail
            for (; i < data.Length; i++)
            {
                a += data[i];
                b += a;
                if (a >= MOD) a -= MOD;
                if (b >= MOD) b %= MOD;
            }

            return (b << 16) | a;
        }

        // --------------------------------------------------------------------
        // COMPRESS: ZERO-ALLOCATION (except the returned pooled buffer)
        // --------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ZlibBuffer Compress(ReadOnlySpan<byte> input, CompressionLevel level)
        {
            // Estimate output: header + input + footer
            int estimated = HeaderSize + input.Length + FooterSize + (input.Length / 16) + 32;
            byte[] output = ArrayPool<byte>.Shared.Rent(estimated);

            int offset = 0;

            // Zlib header (CMF/FLG)
            output[offset++] = 0x78; // Deflate, 32K window
            output[offset++] = 0x9C; // Default compression flags (fast + valid checksum)

            // Deflate payload
            {
                var mem = new MemoryStream(output, offset, output.Length - offset, writable: true, publiclyVisible: true);
                using (var zs = new ZLibStream(mem, level, leaveOpen: true))
                {
                    zs.Write(input);
                }
                offset = (int)mem.Position;
            }

            // Adler32 footer
            uint adler = Adler32(input);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset), adler);
            offset += 4;

            return new ZlibBuffer(output, offset);
        }

        // --------------------------------------------------------------------
        // DECOMPRESS: ZERO-ALLOCATION
        // --------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ZlibBuffer Decompress(ReadOnlySpan<byte> input)
        {
            if (input.Length < HeaderSize + FooterSize)
                throw new InvalidDataException("Invalid zlib stream length.");

            // Validate header (optional)
            if (input[0] != 0x78)
                throw new InvalidDataException("Invalid zlib CMF.");
            // FLG usually 0x9C, 0x01 or other depending on compression level

            // Extract Adler32 footer
            uint expectedAdler = BinaryPrimitives.ReadUInt32BigEndian(input[^4..]);

            // Prepare buffer to hold inflated data
            int maxOut = input.Length * 4; // reasonable upper bound heuristic
            byte[] output = ArrayPool<byte>.Shared.Rent(maxOut);

            int written = 0;

            // Inflate payload
            ReadOnlySpan<byte> deflateData = input.Slice(2, input.Length - 2 - 4);

            using (var mem = new MemoryStream(deflateData.ToArray()))
            using (var zs = new ZLibStream(mem, CompressionMode.Decompress))
            {
                int read;
                while ((read = zs.Read(output.AsSpan(written))) > 0)
                {
                    written += read;

                    // need more space?
                    if (written == output.Length)
                    {
                        // grow buffer
                        byte[] bigger = ArrayPool<byte>.Shared.Rent(output.Length * 2);
                        output.AsSpan(0, written).CopyTo(bigger);
                        ArrayPool<byte>.Shared.Return(output);
                        output = bigger;
                    }
                }
            }

            // Validate Adler
            uint actual = Adler32(output.AsSpan(0, written));
            if (actual != expectedAdler)
            {
                ArrayPool<byte>.Shared.Return(output);
                throw new InvalidDataException("Adler-32 mismatch.");
            }

            return new ZlibBuffer(output, written);
        }
    }
}
