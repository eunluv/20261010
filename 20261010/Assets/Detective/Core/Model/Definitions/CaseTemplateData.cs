using System;
using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 사건 템플릿 하나와, 그 템플릿이 속한 팩의 태그·엔티티·용의자 정보를 묶은 순수 데이터.
    // id가 있는 목록은 모두 Ordinal id 순으로 정렬되어 있다. 축과 시간대는 템플릿에 적힌 순서를 유지한다.
    public sealed class CaseTemplateData
    {
        // 조회 전용 사전 (순회하지 않는다).
        readonly Dictionary<string, TagData> tagById = new Dictionary<string, TagData>(StringComparer.Ordinal);
        readonly Dictionary<string, EntityData> entityById = new Dictionary<string, EntityData>(StringComparer.Ordinal);
        readonly Dictionary<string, SuspectData> suspectById = new Dictionary<string, SuspectData>(StringComparer.Ordinal);

        public string Id { get; }
        public string Title { get; }
        public string RequestText { get; }

        public IReadOnlyList<AxisPoolData> Axes { get; }   // 템플릿 순서 (축 번호)
        public IReadOnlyList<ClueRuleData> Rules { get; }  // id 순
        public IReadOnlyList<string> TimeSlots { get; }    // 시간 순 (템플릿 순서)
        public int CrimeSlotIndex { get; }

        public int ActionPoints { get; }
        // 해결에 필요한 행동 수가 ActionPoints - ActionMargin 이하여야 한다.
        public int ActionMargin { get; }
        public int LieCount { get; }
        public int RedHerringCount { get; }
        public Difficulty Difficulty { get; }

        public IReadOnlyList<TagData> Tags { get; }         // 팩의 모든 태그, id 순
        public IReadOnlyList<EntityData> Entities { get; }  // 팩의 모든 엔티티, id 순
        public IReadOnlyList<SuspectData> Suspects { get; } // 팩의 모든 용의자 프로필, 엔티티 id 순

        // 증언·증거 문장. 없으면 null (문장 없이 사실만 만들어진다).
        public TestimonyTextData TestimonyText { get; }

        public CaseTemplateData(
            string id, string title, string requestText,
            IEnumerable<AxisPoolData> axes, IEnumerable<ClueRuleData> rules,
            IEnumerable<string> timeSlots, int crimeSlotIndex,
            int actionPoints, int lieCount, int redHerringCount, Difficulty difficulty,
            IEnumerable<TagData> tags, IEnumerable<EntityData> entities, IEnumerable<SuspectData> suspects,
            int actionMargin = 1, TestimonyTextData testimonyText = null)
        {
            Id = id;
            Title = title;
            RequestText = requestText;
            Axes = ReadOnly.Copy(axes);
            Rules = ReadOnly.Copy(rules);
            TimeSlots = ReadOnly.Copy(timeSlots);
            CrimeSlotIndex = crimeSlotIndex;
            ActionPoints = actionPoints;
            ActionMargin = actionMargin;
            LieCount = lieCount;
            RedHerringCount = redHerringCount;
            Difficulty = difficulty;
            Tags = ReadOnly.Copy(tags);
            Entities = ReadOnly.Copy(entities);
            Suspects = ReadOnly.Copy(suspects);
            TestimonyText = testimonyText;

            foreach (var tag in Tags)
                if (tag != null && tag.Id != null) tagById[tag.Id] = tag;
            foreach (var entity in Entities)
                if (entity != null && entity.Id != null) entityById[entity.Id] = entity;
            foreach (var suspect in Suspects)
                if (suspect != null && suspect.EntityId != null) suspectById[suspect.EntityId] = suspect;
        }

        // 없으면 null.
        public TagData FindTag(string tagId) =>
            tagId != null && tagById.TryGetValue(tagId, out var tag) ? tag : null;

        // 없으면 null.
        public EntityData FindEntity(string entityId) =>
            entityId != null && entityById.TryGetValue(entityId, out var entity) ? entity : null;

        // 없으면 null.
        public SuspectData FindSuspect(string entityId) =>
            entityId != null && suspectById.TryGetValue(entityId, out var suspect) ? suspect : null;

        // 그 종류의 엔티티를 후보로 쓰는 첫 번째 축 번호. 없으면 -1.
        // 타임라인은 Suspect 축을 "범인", Place 축을 "사건 장소", Item 축을 "사건 도구"로 본다.
        public int FirstAxisOfKind(EntityKind kind)
        {
            for (int a = 0; a < Axes.Count; a++)
                if (Axes[a].Kind == kind) return a;
            return -1;
        }

        public override string ToString() => Id;
    }
}
