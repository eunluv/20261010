using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 사건 템플릿의 축 하나: 어떤 종류의 엔티티 중 무엇을 몇 개 뽑을지와, 조건을 통과한 후보 풀.
    public sealed class AxisPoolData
    {
        public string AxisId { get; }
        public EntityKind Kind { get; }
        public IReadOnlyList<string> RequiredTagIds { get; } // Ordinal 정렬
        public int PickCount { get; }
        public IReadOnlyList<string> CandidateIds { get; }   // 종류·필수 태그를 통과한 엔티티 id, Ordinal 정렬

        public AxisPoolData(string axisId, EntityKind kind, IEnumerable<string> requiredTagIds, int pickCount,
            IEnumerable<string> candidateIds)
        {
            AxisId = axisId;
            Kind = kind;
            RequiredTagIds = ReadOnly.Copy(requiredTagIds);
            PickCount = pickCount;
            CandidateIds = ReadOnly.Copy(candidateIds);
        }

        public override string ToString() => $"{AxisId}: {Kind} {PickCount}/{CandidateIds.Count}";
    }
}
