using System.Runtime.CompilerServices;

namespace CompressionLibrary.Helpers
{
    public struct SymbolMap
    {
        public Entry[] Entries;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Add(char sym, int code, byte len)
        {
            Entries ??= new Entry[96];
            Entries[Count++] = new Entry(sym, code, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public SymbolMap Merge(in SymbolMap other)
        {
            SymbolMap m = this;
            if (other.Count == 0) return m;
            m.Entries ??= new Entry[96];
            Array.Copy(other.Entries, 0, m.Entries, m.Count, other.Count);
            m.Count += other.Count;
            return m;
        }
    }
}
