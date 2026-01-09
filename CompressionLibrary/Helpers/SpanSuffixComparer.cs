using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    internal sealed class SpanSuffixComparer : IComparer<int>
    {
        private readonly ReadOnlyMemory<char> _input;

        public SpanSuffixComparer(ReadOnlySpan<char> input)
        {
            _input = input.ToArray(); // stable backing store
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(int x, int y)
        {
            ReadOnlySpan<char> span = _input.Span;
            int n = span.Length;

            for (int i = 0; i < n; i++)
            {
                char a = span[(x + i) % n];
                char b = span[(y + i) % n];
                if (a != b)
                    return a - b;
            }

            return 0;
        }
    }
}
