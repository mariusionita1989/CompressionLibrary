using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CompressionLibrary.Main
{
    ref struct RangeDecoder
    {
        private ReadOnlySpan<byte> _buf;
        private ref byte Ptr => ref MemoryMarshal.GetReference(_buf);
        private int _pos;
        private uint _low, _high, _mid;
        private readonly byte _prec;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public RangeDecoder(ReadOnlySpan<byte> buffer, int precision)
        {
            _buf = buffer;
            _pos = 0;
            _low = 0;
            _high = 0xFFFFFFFFu;
            _mid = 0;
            _prec = (byte)precision;
            for (int i = 0; i < 4; i++) _mid = (_mid << 8) | buffer[_pos++];
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int Decode(ReadOnlySpan<int> cm, int alphabetSize)
        {
            ulong range = (ulong)(_mid - _low) << _prec;
            uint div = _high - _low;
            uint midPoint = div == 0 ? 0 : (uint)(range / div);
            int lo = 0, hi = alphabetSize;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if ((uint)cm[m + 1] <= midPoint) lo = m + 1;
                else hi = m;
            }
            int i = lo;

            range = _high - _low;
            _high = _low + (uint)((range * (ulong)cm[i + 1]) >> _prec);
            _low += (uint)((range * (ulong)cm[i]) >> _prec) + 1u;

            if ((_high - _low) < (1u << _prec)) _high = _low;

            while ((_low ^ _high) < (1u << 24))
            {
                _low <<= 8;
                _high = (_high << 8) | 0xFFu;
                _mid = (_mid << 8) | Unsafe.Add(ref Ptr, _pos++);
            }

            return i;
        }
    }
}
