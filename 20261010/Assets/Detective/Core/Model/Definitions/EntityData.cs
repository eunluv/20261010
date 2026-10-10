using System.Collections.Generic;

namespace Detective.Core.Model
{
    // EntityDef 에셋의 순수 데이터판. 일러스트는 런타임이 id로 다시 찾는다.
    public sealed class EntityData
    {
        public string Id { get; }
        public string DisplayName { get; }
        public EntityKind Kind { get; }
        public IReadOnlyList<string> TagIds { get; } // Ordinal 정렬, 중복 없음
        public string Description { get; }

        public EntityData(string id, string displayName, EntityKind kind, IEnumerable<string> tagIds, string description)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            TagIds = ReadOnly.Copy(tagIds);
            Description = description;
        }

        public override string ToString() => $"{Kind}:{Id}";
    }
}
