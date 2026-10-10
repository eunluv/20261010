using System.Collections.Generic;

namespace Detective.Core.Model
{
    internal static class ReadOnly
    {
        static class Empty<T>
        {
            public static readonly T[] Value = new T[0];
        }

        // null이면 빈 목록, 아니면 복사본.
        public static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            if (source == null) return Empty<T>.Value;
            return new List<T>(source).AsReadOnly();
        }
    }
}
