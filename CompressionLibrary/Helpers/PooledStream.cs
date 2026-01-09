using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    public sealed class PooledStream : Stream
    {
        private readonly byte[] _buffer;
        private readonly int _length;
        private int _position;

        public int Remaining => _length - _position;

        public PooledStream(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
            _position = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Read(Span<byte> destination)
        {
            int toCopy = Math.Min(destination.Length, Remaining);
            if (toCopy <= 0) return 0;

            new ReadOnlySpan<byte>(_buffer, _position, toCopy).CopyTo(destination);
            _position += toCopy;
            return toCopy;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override int Read(byte[] buffer, int offset, int count)
        {
            int toCopy = Math.Min(count, Remaining);
            if (toCopy <= 0) return 0;

            Buffer.BlockCopy(_buffer, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override void Flush() { }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override void SetLength(long value) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
