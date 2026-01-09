using CompressionLibrary.Helpers;
using System.Runtime.CompilerServices;

namespace CompressionLibrary.Main
{
    public static class SubstitutionCompressor
    {
        private static readonly char[] FirstPassAlphabet = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly char[] SecondPassAlphabet = "QRSTUVWXYZ0123456789".ToCharArray();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static CompressedResult CompressTwoPass(string input)
        {
            var (firstString, firstPassdict) = SubstitutionPass.Compress(input, FirstPassAlphabet);
            var (secondString, secondPassdict) = SubstitutionPass.Compress(firstString, SecondPassAlphabet);
            return new CompressedResult(secondString, firstPassdict, secondPassdict);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string DecompressTwoPass(CompressedResult result)
        {
            string firstStage = SubstitutionPass.Decompress(result.Compressed, result.SecondPassDictionary);
            string zeroStage = SubstitutionPass.Decompress(firstStage, result.FirstPassDictionary);
            return zeroStage;
        }
    }
}
