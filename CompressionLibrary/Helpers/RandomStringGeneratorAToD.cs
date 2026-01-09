using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace CompressionLibrary.Helpers
{
    public static unsafe class RandomStringGeneratorAToD
    {
        private const int BlockCount = 32; // 32 chars per iteration
        [ThreadStatic] private static ulong _s0, _s1, _s2, _s3;
        [ThreadStatic] private static bool _initialized;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InitState()
        {
            if (_initialized) return;
            ulong seed = (ulong)Environment.TickCount64;
            _s0 = 0x9E3779B97F4A7C15UL ^ seed;
            _s1 = 0xD1B54A32D192ED03UL ^ (seed << 17);
            _s2 = 0xABC98388FB8FAC03UL ^ (seed >> 13);
            _s3 = 0xDE5FB9D2630458E1UL ^ (seed << 7);
            _initialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong NextU64()
        {
            ulong result = BitOperations.RotateLeft(_s0 + _s3, 23) + _s0;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = BitOperations.RotateLeft(_s3, 45);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> NextVector256()
        {
            return Vector256.Create(NextU64(), NextU64(), NextU64(), NextU64()).AsByte();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void FillRandomAtoD(Span<char> output)
        {
            InitState();
            int length = output.Length;
            int i = 0;
            fixed (char* pOutput = output)
            {
                if (Avx2.IsSupported)
                {
                    Vector256<byte> mask03 = Vector256.Create((byte)0x03); // 2 bits
                    Vector256<byte> baseA = Vector256.Create((byte)'A');

                    while (i + BlockCount <= length)
                    {
                        Vector256<byte> vec = NextVector256();
                        Vector256<byte> values = Avx2.And(vec, mask03);
                        Vector256<byte> ascii = Avx2.Add(values, baseA);

                        Vector128<byte> lo128 = ascii.GetLower();
                        Vector128<byte> hi128 = ascii.GetUpper();

                        Vector256<short> lo16 = Avx2.ConvertToVector256Int16(lo128);
                        Vector256<short> hi16 = Avx2.ConvertToVector256Int16(hi128);

                        Avx.Store((short*)(pOutput + i), lo16);
                        Avx.Store((short*)(pOutput + i + 16), hi16);

                        i += BlockCount;
                    }
                }

                for (; i < length; i++)
                {
                    output[i] = (char)('A' + ((int)NextU64() & 0x03));
                }
            }
        }
    }
}
