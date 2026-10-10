using System;
using System.Collections.Generic;
using System.Linq;
using Detective.Core.Model;

namespace Detective.Tests.Core
{
    // 생성기 테스트용 작은 팩: 용의자·장소·도구 5개씩(사건마다 4개씩 뽑음), 조건 7종을 모두 쓰는 규칙 10개.
    // 변환기가 내놓는 것과 같은 모양(목록은 id 순)의 순수 데이터로 직접 만든다.
    internal static class TestPack
    {
        public static CaseTemplateData Create(int redHerrings = 1, int pickCount = 4, string[] onlyRuleIds = null,
            int actionPoints = 8, int actionMargin = 1, int lieCount = 2, bool withTestimonyText = true)
        {
            var tags = new List<TagData>
            {
                new TagData("club.band", "경음부", "club"),
                new TagData("club.broadcast", "방송부", "club"),
                new TagData("club.cooking", "요리부", "club"),
                new TagData("club.council", "학생회", "club"),
                new TagData("club.drama", "연극부", "club"),
                new TagData("env.indoor", "실내", "env"),
                new TagData("env.outdoor", "실외", "env"),
                new TagData("floor.1", "1층", "floor"),
                new TagData("floor.2", "2층", "floor"),
                new TagData("trait.lefty", "왼손잡이", "trait"),
                new TagData("trait.tall", "키 큼", "trait"),
            };

            var entities = new List<EntityData>
            {
                Entity("s_broadcast", "방송부원", EntityKind.Suspect, "club.broadcast", "floor.2", "trait.lefty"),
                Entity("s_cook", "요리부 1학년", EntityKind.Suspect, "club.cooking", "floor.1"),
                Entity("s_council", "학생회 회계", EntityKind.Suspect, "club.council", "floor.1"),
                Entity("s_drama", "연극부 부장", EntityKind.Suspect, "club.drama", "floor.2", "trait.tall"),
                Entity("s_guitar", "기타리스트", EntityKind.Suspect, "club.band", "floor.2", "trait.lefty", "trait.tall"),

                Entity("p_backstage", "강당 무대 뒤", EntityKind.Place, "club.drama", "floor.1", "env.indoor"),
                Entity("p_broadcast_room", "방송실", EntityKind.Place, "club.broadcast", "floor.2", "env.indoor"),
                Entity("p_kitchen", "조리실", EntityKind.Place, "club.cooking", "floor.1", "env.indoor"),
                Entity("p_music_room", "음악실", EntityKind.Place, "club.band", "floor.2", "env.indoor"),
                Entity("p_yard", "학생회 앞마당", EntityKind.Place, "club.council", "floor.1", "env.outdoor"),

                Entity("i_amp", "앰프 케이스", EntityKind.Item, "club.band"),
                Entity("i_box", "서류 상자", EntityKind.Item, "club.council"),
                Entity("i_cart", "방송 장비 카트", EntityKind.Item, "club.broadcast"),
                Entity("i_pot", "대형 냄비", EntityKind.Item, "club.cooking"),
                Entity("i_rack", "의상 행거", EntityKind.Item, "club.drama"),
            };
            entities.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var rules = new List<ClueRuleData>
            {
                Rule("alibi", ConstraintType.IsNot, "culprit", openX: true,
                    text: "{suspect}는 {time}에 {place2}에서 친구들과 사진을 찍고 있었다."),
                Rule("locked_room", ConstraintType.IsNot, "place", openX: true,
                    text: "{place}은 오후 내내 잠겨 있었다."),
                Rule("dusty", ConstraintType.IsNot, "item", openX: true,
                    text: "{item}에는 먼지가 그대로 쌓여 있다."),
                Rule("club_supply", ConstraintType.SameTag, "item", axisB: "place", category: "club",
                    text: "없어진 물건을 옮긴 도구는 그 장소 동아리의 비품이다."),
                Rule("floor_seen", ConstraintType.HasTag, "culprit", openTag: true, category: "floor",
                    text: "{tag} 복도에서 수상한 그림자를 봤다."),
                Rule("lefty", ConstraintType.HasTag, "culprit", tag: "trait.lefty",
                    text: "문고리에 왼손으로 잡은 자국이 남아 있다."),
                Rule("not_outdoor", ConstraintType.LacksTag, "place", tag: "env.outdoor",
                    text: "물건에는 물기 하나 없었다. 오늘은 비가 왔는데."),
                Rule("seen_going", ConstraintType.Implies, "culprit", axisB: "place", openX: true, openY: true,
                    text: "{suspect}가 {place} 쪽으로 가는 걸 누가 봤대."),
                Rule("one_of_two", ConstraintType.OneOf, "culprit", openX: true, openY: true,
                    text: "범인은 {suspect} 아니면 {suspect2}다."),
                Rule("cant_carry", ConstraintType.NotTogether, "place", axisB: "item", openX: true, openY: true,
                    text: "{place}에서 {item}을 옮기는 건 불가능하다."),
            };
            if (onlyRuleIds != null) rules = rules.Where(r => onlyRuleIds.Contains(r.Id)).ToList();
            rules.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var axes = new List<AxisPoolData>
            {
                Axis("culprit", EntityKind.Suspect, pickCount, entities),
                Axis("place", EntityKind.Place, pickCount, entities),
                Axis("item", EntityKind.Item, pickCount, entities),
            };

            // 엔티티 id 순.
            var suspects = new List<SuspectData>
            {
                new SuspectData("s_broadcast", "운명의 주파수…", LieStyle.Exaggerator, new string[0]),
                new SuspectData("s_cook", "죄송해요…", LieStyle.Omitter, new string[0]),
                new SuspectData("s_council", "규정상 그렇습니다.", LieStyle.Liar, new string[0]),
                new SuspectData("s_drama", "흥.", LieStyle.Liar, new string[0]),
                new SuspectData("s_guitar", "귀찮아.", LieStyle.Honest, new string[0]),
            };

            var testimonyText = withTestimonyText
                ? new TestimonyTextData(
                    new[] { "{time}에는 {place}에 있었어." },
                    new[] { "{time}에 {place}에서 {suspect} 봤어." },
                    new[] { "{time}에 {place}에서 {item} 만졌어." },
                    new[] { "맹세코, " },
                    new[] { "{suspect}: {time}에 {place}에 있었다는 기록." },
                    new[] { "{suspect}: {time}에 {place}에서 {suspect2} 옆에 있었다." },
                    new[] { "{suspect}: {time}에 {place}에서 {item} 만짐." },
                    new[] { "{suspect}: {time}에 {place}에는 없었다." })
                : null;

            return new CaseTemplateData(
                "test_case", "테스트 사건", "의뢰 문장",
                axes, rules, new[] { "13시", "14시", "15시", "16시" }, 2,
                actionPoints, lieCount, redHerrings, Difficulty.Normal,
                tags, entities, suspects,
                actionMargin, testimonyText);
        }

        static EntityData Entity(string id, string displayName, EntityKind kind, params string[] tagIds)
        {
            var sorted = tagIds.OrderBy(t => t, StringComparer.Ordinal);
            return new EntityData(id, displayName, kind, sorted, "");
        }

        static AxisPoolData Axis(string axisId, EntityKind kind, int pickCount, List<EntityData> entities)
        {
            var candidates = entities.Where(e => e.Kind == kind).Select(e => e.Id);
            return new AxisPoolData(axisId, kind, new string[0], pickCount, candidates);
        }

        static ClueRuleData Rule(string id, ConstraintType type, string axisA, string axisB = null,
            string tag = null, string category = null, bool openTag = false, bool openX = false, bool openY = false,
            string text = null)
        {
            return new ClueRuleData(
                id, type, axisA, axisB,
                tag, category, null, null,
                openTag, openX, openY,
                text != null ? new[] { text } : new string[0], ClueSource.Investigate,
                1f, 1f, 1f);
        }
    }
}
