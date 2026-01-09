using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CompressionLibrary.Main
{
    [SkipLocalsInit]
    public static class MtfAtoP
    {
        private const int AlphabetSize = 16;
        private const char FirstChar = 'A';

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Span<byte> Encode(ReadOnlySpan<char> input, Span<byte> output)
        {
            // Initialize list as [0,1,...,15] using init expression (C# 13+ stackalloc init)
            Span<byte> list = stackalloc byte[AlphabetSize]
            {
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15
            };

            ref byte listRef = ref MemoryMarshal.GetReference(list);
            ref char inputRef = ref MemoryMarshal.GetReference(input);
            ref byte outputRef = ref MemoryMarshal.GetReference(output);

            nuint length = (nuint)input.Length;
            for (nuint i = 0; i < length; i++)
            {
                int target = Unsafe.Add(ref inputRef, (nint)i) - FirstChar;
                nuint pos = 0;
                while (Unsafe.Add(ref listRef, (nint)pos) != target)
                    pos++;

                Unsafe.Add(ref outputRef, (nint)i) = (byte)pos;

                if (pos != 0)
                {
                    // Move [0..pos-1] → [1..pos]
                    Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref listRef, 1), ref listRef, (uint)(pos));
                    list[0] = (byte)target;
                }
            }

            return output[..(int)length];
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Span<char> Decode(ReadOnlySpan<byte> encoded, Span<char> output)
        {
            Span<byte> list = stackalloc byte[AlphabetSize]
            {
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15
            };

            ref byte listRef = ref MemoryMarshal.GetReference(list);
            ref byte encodedRef = ref MemoryMarshal.GetReference(encoded);
            ref char outputRef = ref MemoryMarshal.GetReference(output);

            nuint length = (nuint)encoded.Length;
            for (nuint i = 0; i < length; i++)
            {
                nuint pos = Unsafe.Add(ref encodedRef, (nint)i); // safe: 0–15

                byte symbol = Unsafe.Add(ref listRef, (nint)pos);
                Unsafe.Add(ref outputRef, (nint)i) = (char)(FirstChar + symbol);

                if (pos != 0)
                {
                    Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref listRef, 1), ref listRef, (uint)pos);
                    list[0] = symbol;
                }
            }

            return output[..(int)length];
        }
    }
}
