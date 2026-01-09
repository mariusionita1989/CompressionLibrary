namespace CompressionLibrary.Helpers
{
    public readonly record struct Token(int Length, int Distance, char Literal);
}
