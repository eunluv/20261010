using System;
using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 축별·옵션별로 붙는 태그 입력표. CaseSpace가 만들어질 때 정렬된 배열로 고정된다.
    public sealed class OptionTagTable
    {
        internal readonly struct Entry
        {
            public readonly string AxisId;
            public readonly string OptionId;
            public readonly string TagId;

            public Entry(string axisId, string optionId, string tagId)
            {
                AxisId = axisId;
                OptionId = optionId;
                TagId = tagId;
            }
        }

        readonly List<Entry> entries = new List<Entry>();

        internal IReadOnlyList<Entry> Entries => entries;

        public OptionTagTable Add(string axisId, string optionId, params string[] tagIds)
        {
            if (string.IsNullOrEmpty(axisId)) throw new ArgumentException("축 id가 비어 있습니다.", nameof(axisId));
            if (string.IsNullOrEmpty(optionId)) throw new ArgumentException("옵션 id가 비어 있습니다.", nameof(optionId));
            if (tagIds == null) throw new ArgumentNullException(nameof(tagIds));

            foreach (var tagId in tagIds)
            {
                if (string.IsNullOrEmpty(tagId))
                    throw new ArgumentException($"'{axisId}/{optionId}'에 비어 있는 태그 id가 있습니다.", nameof(tagIds));
                entries.Add(new Entry(axisId, optionId, tagId));
            }
            return this;
        }
    }
}
