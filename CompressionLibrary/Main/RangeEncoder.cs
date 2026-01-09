using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CompressionLibrary.Main
{
    ref struct RangeEncoder
    {
        private Span<byte> _buf;
        private ref byte Ptr => ref MemoryMarshal.GetReference(_buf);
        public int Position;
        private uint _low, _high;
        private readonly byte _prec;
        public int ExtraBytes;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public RangeEncoder(Span<byte> buffer, int precision)
        {
            _buf = buffer;
            Position = 0;
            _low = 0;
            _high = 0xFFFFFFFFu;
            _prec = (byte)precision;
            ExtraBytes = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Encode(int cmin, int cmax)
        {
            ulong range = _high - _low;
            _high = _low + (uint)((range * (ulong)cmax) >> _prec);
            _low += (uint)((range * (ulong)cmin) >> _prec) + 1u;

            if ((_high - _low) < (1u << _prec))
            {
                _high = _low;
                ExtraBytes += 4;
            }

            while ((_low ^ _high) < (1u << 24))
            {
                Unsafe.Add(ref Ptr, Position++) = (byte)(_low >> 24);
                _low <<= 8;
                _high = (_high << 8) | 0xFFu;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Flush()
        {
            _low += 1u;
            BinaryPrimitives.WriteUInt32BigEndian(_buf.Slice(Position, 4), _low);
            Position += 4;
        }
    }
}
