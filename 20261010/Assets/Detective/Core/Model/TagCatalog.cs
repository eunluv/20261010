using System;
using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 태그 사전: 태그 id → 카테고리 id (동아리, 층, 손잡이 등). SameTag 판정에 쓴다.
    public sealed class TagCatalog
    {
        internal readonly struct Entry
        {
            public readonly string TagId;
            public readonly string CategoryId;

            public Entry(string tagId, string categoryId)
            {
                TagId = tagId;
                CategoryId = categoryId;
            }
        }

        // 순서를 지키기 위해 목록으로 보관하고, 조회용 사전은 따로 둔다.
        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<string, string> categoryByTag = new Dictionary<string, string>(StringComparer.Ordinal);

        internal IReadOnlyList<Entry> Entries => entries;

        public TagCatalog Add(string tagId, string categoryId)
        {
            if (string.IsNullOrEmpty(tagId)) throw new ArgumentException("태그 id가 비어 있습니다.", nameof(tagId));
            if (string.IsNullOrEmpty(categoryId))
                throw new ArgumentException($"태그 '{tagId}'의 카테고리가 비어 있습니다.", nameof(categoryId));
            if (categoryByTag.ContainsKey(tagId))
                throw new ArgumentException($"태그 '{tagId}'가 이미 등록되어 있습니다.", nameof(tagId));

            categoryByTag.Add(tagId, categoryId);
            entries.Add(new Entry(tagId, categoryId));
            return this;
        }
    }
}
