using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    public sealed class CompressedResult
    {
        public readonly string Compressed;
        public readonly Dictionary<char, string> FirstPassDictionary;
        public readonly Dictionary<char, string> SecondPassDictionary;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public CompressedResult(string compressed, Dictionary<char, string> firstDictionary, Dictionary<char, string> secondDictionary)
        {
            Compressed = compressed;
            FirstPassDictionary = firstDictionary;
            SecondPassDictionary = secondDictionary;
        }
    }
}
