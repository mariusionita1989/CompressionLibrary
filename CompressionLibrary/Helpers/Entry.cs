namespace CompressionLibrary.Helpers
{
    public readonly struct Entry
    {
        public readonly char Symbol;
        public readonly int Code;
        public readonly byte Length;

        public Entry(char s, int c, byte l)
        {
            Symbol = s;
            Code = c;
            Length = l;
        }
    }
}
