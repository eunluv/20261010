using System;
using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 플레이어가 맞혀야 할 항목 하나(범인, 장소, 도구 등)와 그 후보 옵션 목록.
    // 옵션의 순서가 곧 옵션 번호이므로, 호출부는 id 순으로 정렬해서 넘긴다.
    public sealed class AxisDef
    {
        public string Id { get; }
        public IReadOnlyList<string> OptionIds { get; }
        public int OptionCount => OptionIds.Count;

        public AxisDef(string id, IEnumerable<string> optionIds)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("축 id가 비어 있습니다.", nameof(id));
            if (optionIds == null) throw new ArgumentNullException(nameof(optionIds));

            var list = new List<string>(optionIds);
            if (list.Count == 0) throw new ArgumentException($"축 '{id}'에 옵션이 없습니다.", nameof(optionIds));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var optionId in list)
            {
                if (string.IsNullOrEmpty(optionId))
                    throw new ArgumentException($"축 '{id}'에 비어 있는 옵션 id가 있습니다.", nameof(optionIds));
                if (!seen.Add(optionId))
                    throw new ArgumentException($"축 '{id}'에 옵션 '{optionId}'가 중복되었습니다.", nameof(optionIds));
            }

            Id = id;
            OptionIds = list.AsReadOnly();
        }

        // 옵션 번호를 돌려준다. 없으면 -1.
        public int IndexOf(string optionId)
        {
            for (int i = 0; i < OptionIds.Count; i++)
                if (string.Equals(OptionIds[i], optionId, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
