using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    public readonly struct ZlibBuffer
    {
        public readonly byte[] Buffer;
        public readonly int Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ZlibBuffer(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }

        public ReadOnlySpan<byte> Span => Buffer.AsSpan(0, Length);
    }
}
