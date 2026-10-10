using System.Collections.Generic;
using System.Linq;
using Detective.Core.Model;
using Detective.Data;
using NUnit.Framework;
using UnityEngine;

namespace Detective.Tests.Data
{
    public class PackConverterTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        WorldPack pack;
        CaseTemplate template;
        TagDef floor1, floor2, clubDrama, lefty;
        EntityDef sDrama, sCook, sBroadcast, pStorage, pMusic, pKitchen, iRack, iPot;
        ClueRule alibi, floorSighting, clubSupply;

        [SetUp]
        public void SetUp()
        {
            // 일부러 id 순이 아닌 순서로 넣는다.
            floor2 = Tag("floor.2", "floor");
            clubDrama = Tag("club.drama", "club");
            lefty = Tag("trait.lefty", "trait");
            floor1 = Tag("floor.1", "floor");

            sDrama = Entity("s_drama", EntityKind.Suspect, clubDrama, floor2);
            sCook = Entity("s_cook", EntityKind.Suspect, floor1);
            sBroadcast = Entity("s_broadcast", EntityKind.Suspect, lefty, floor2);
            pStorage = Entity("p_storage", EntityKind.Place, floor1);
            pMusic = Entity("p_music", EntityKind.Place, floor2);
            pKitchen = Entity("p_kitchen", EntityKind.Place, floor1);
            iRack = Entity("i_rack", EntityKind.Item, clubDrama);
            iPot = Entity("i_pot", EntityKind.Item);

            alibi = Rule("z_alibi", ConstraintType.IsNot, "culprit");
            alibi.axisB = "place"; // IsNot은 축 B를 쓰지 않으므로 변환 결과에서 지워져야 한다
            alibi.textVariants.Add("{suspect}는 {time}에 {place}에 있었다.");

            floorSighting = Rule("a_floor", ConstraintType.HasTag, "culprit");
            floorSighting.openTag = true;
            floorSighting.tagCategory = "floor";

            clubSupply = Rule("m_club", ConstraintType.SameTag, "item");
            clubSupply.axisB = "place";
            clubSupply.tagCategory = "club";

            template = Make<CaseTemplate>("Case_Test");
            template.id = "test_case";
            template.axes.Add(Axis("culprit", EntityKind.Suspect, 3));
            template.axes.Add(Axis("place", EntityKind.Place, 2, floor1));
            template.axes.Add(Axis("item", EntityKind.Item, 2));
            template.rules.AddRange(new[] { alibi, floorSighting, clubSupply });
            template.timeSlots.AddRange(new[] { "13시", "14시", "15시" });
            template.crimeSlotIndex = 2;

            pack = Make<WorldPack>("Pack_Test");
            pack.tags.AddRange(new[] { floor2, clubDrama, lefty, floor1 });
            pack.entities.AddRange(new[] { sDrama, sCook, sBroadcast, pStorage, pMusic, pKitchen, iRack, iPot });
            pack.suspectProfiles.Add(Profile(sDrama, LieStyle.Liar));
            pack.suspectProfiles.Add(Profile(sCook, LieStyle.Omitter));
            pack.rules.AddRange(template.rules);
            pack.templates.Add(template);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        // ---- 정상 변환 ----

        [Test]
        public void Tags_SortedById_WithCategories()
        {
            var data = PackConverter.Convert(template, pack);

            CollectionAssert.AreEqual(new[] { "club.drama", "floor.1", "floor.2", "trait.lefty" }, data.Tags.Select(t => t.Id));
            CollectionAssert.AreEqual(new[] { "club", "floor", "floor", "trait" }, data.Tags.Select(t => t.Category));
        }

        [Test]
        public void Entities_SortedById_WithSortedTagIds()
        {
            var data = PackConverter.Convert(template, pack);

            CollectionAssert.AreEqual(
                new[] { "i_pot", "i_rack", "p_kitchen", "p_music", "p_storage", "s_broadcast", "s_cook", "s_drama" },
                data.Entities.Select(e => e.Id));

            var broadcast = data.Entities.Single(e => e.Id == "s_broadcast");
            Assert.AreEqual(EntityKind.Suspect, broadcast.Kind);
            CollectionAssert.AreEqual(new[] { "floor.2", "trait.lefty" }, broadcast.TagIds, "태그 id는 입력 순서와 상관없이 정렬");
            CollectionAssert.IsEmpty(data.Entities.Single(e => e.Id == "i_pot").TagIds);
        }

        [Test]
        public void Axes_KeepTemplateOrder_CandidatesFilteredAndSorted()
        {
            var data = PackConverter.Convert(template, pack);

            CollectionAssert.AreEqual(new[] { "culprit", "place", "item" }, data.Axes.Select(a => a.AxisId));

            CollectionAssert.AreEqual(new[] { "s_broadcast", "s_cook", "s_drama" }, data.Axes[0].CandidateIds);
            CollectionAssert.AreEqual(new[] { "p_kitchen", "p_storage" }, data.Axes[1].CandidateIds, "필수 태그 floor.1로 거름");
            CollectionAssert.AreEqual(new[] { "i_pot", "i_rack" }, data.Axes[2].CandidateIds);

            CollectionAssert.AreEqual(new[] { "floor.1" }, data.Axes[1].RequiredTagIds);
            Assert.AreEqual(3, data.Axes[0].PickCount);
            Assert.AreEqual(EntityKind.Place, data.Axes[1].Kind);
        }

        [Test]
        public void Rules_SortedById_ParametersNormalized()
        {
            var data = PackConverter.Convert(template, pack);

            CollectionAssert.AreEqual(new[] { "a_floor", "m_club", "z_alibi" }, data.Rules.Select(r => r.Id));

            var floor = data.Rules[0];
            Assert.AreEqual(ConstraintType.HasTag, floor.Type);
            Assert.IsTrue(floor.IsTagOpen);
            Assert.IsNull(floor.TagId);
            Assert.AreEqual("floor", floor.TagCategory);
            Assert.IsFalse(floor.IsOptionXOpen, "HasTag는 옵션 X를 쓰지 않는다");

            var club = data.Rules[1];
            Assert.AreEqual("item", club.AxisA);
            Assert.AreEqual("place", club.AxisB);
            Assert.AreEqual("club", club.TagCategory);
            Assert.IsNull(club.OptionXId);
            Assert.IsFalse(club.IsOptionXOpen);

            var alibiData = data.Rules[2];
            Assert.AreEqual("culprit", alibiData.AxisA);
            Assert.IsNull(alibiData.AxisB, "IsNot은 축 B를 쓰지 않는다");
            Assert.IsTrue(alibiData.IsOptionXOpen);
            Assert.IsNull(alibiData.OptionXId);
            CollectionAssert.AreEqual(alibi.textVariants, alibiData.TextVariants);
        }

        [Test]
        public void Rule_FixedOptionAndTag_MapToIds()
        {
            var implies = Rule("b_seen", ConstraintType.Implies, "culprit");
            implies.axisB = "place";
            implies.openOptionX = false;
            implies.optionX = sCook;
            implies.openOptionY = false;
            implies.optionY = pKitchen;

            var lacks = Rule("c_not_lefty", ConstraintType.LacksTag, "culprit");
            lacks.tag = lefty;

            template.rules.Add(implies);
            template.rules.Add(lacks);
            var data = PackConverter.Convert(template, pack);

            var impliesData = data.Rules.Single(r => r.Id == "b_seen");
            Assert.AreEqual("s_cook", impliesData.OptionXId);
            Assert.AreEqual("p_kitchen", impliesData.OptionYId);
            Assert.IsFalse(impliesData.IsOptionXOpen);
            Assert.IsFalse(impliesData.IsOptionYOpen);

            var lacksData = data.Rules.Single(r => r.Id == "c_not_lefty");
            Assert.AreEqual("trait.lefty", lacksData.TagId);
            Assert.IsFalse(lacksData.IsTagOpen);
        }

        [Test]
        public void Rule_SourcesEverything_MaskedToDefinedFlags()
        {
            alibi.sources = (ClueSource)(-1); // 인스펙터의 Everything
            var data = PackConverter.Convert(template, pack);
            Assert.AreEqual(ClueSources.All, data.Rules.Single(r => r.Id == "z_alibi").Sources);
        }

        [Test]
        public void Rule_WeightFor_AppliesDifficultyMultiplier()
        {
            alibi.difficultyWeight = 2f;
            alibi.easyWeight = 3f;
            alibi.hardWeight = 0.5f;
            var rule = PackConverter.Convert(template, pack).Rules.Single(r => r.Id == "z_alibi");

            Assert.AreEqual(6f, rule.WeightFor(Difficulty.Easy));
            Assert.AreEqual(2f, rule.WeightFor(Difficulty.Normal));
            Assert.AreEqual(1f, rule.WeightFor(Difficulty.Hard));
        }

        [Test]
        public void Suspects_SortedByEntityId()
        {
            var data = PackConverter.Convert(template, pack);

            CollectionAssert.AreEqual(new[] { "s_cook", "s_drama" }, data.Suspects.Select(s => s.EntityId));
            Assert.AreEqual(LieStyle.Omitter, data.Suspects[0].LieStyle);
            Assert.AreEqual(LieStyle.Liar, data.Suspects[1].LieStyle);
        }

        [Test]
        public void Template_ScalarsAndTimeSlotOrderKept()
        {
            template.actionPoints = 7;
            template.lieCount = 2;
            template.redHerringCount = 1;
            template.difficulty = Difficulty.Hard;
            var data = PackConverter.Convert(template, pack);

            Assert.AreEqual("test_case", data.Id);
            CollectionAssert.AreEqual(new[] { "13시", "14시", "15시" }, data.TimeSlots);
            Assert.AreEqual(2, data.CrimeSlotIndex);
            Assert.AreEqual(7, data.ActionPoints);
            Assert.AreEqual(2, data.LieCount);
            Assert.AreEqual(1, data.RedHerringCount);
            Assert.AreEqual(Difficulty.Hard, data.Difficulty);
        }

        // ---- 구조적 오류 ----

        [Test]
        public void Error_EntityTagNotInPack()
        {
            var stray = Tag("trait.tall", "trait"); // 팩 태그 목록에 등록하지 않음
            sDrama.tags.Add(stray);
            AssertConversionError("trait.tall");
        }

        [Test]
        public void Error_DuplicateEntityId()
        {
            pack.entities.Add(Entity("s_cook", EntityKind.Suspect));
            AssertConversionError("s_cook");
        }

        [Test]
        public void Error_FixedOptionMissing()
        {
            alibi.openOptionX = false;
            AssertConversionError("z_alibi");
        }

        [Test]
        public void Error_SameTagWithoutCategory()
        {
            clubSupply.tagCategory = "";
            AssertConversionError("m_club");
        }

        [Test]
        public void Error_CrimeSlotOutOfRange()
        {
            template.crimeSlotIndex = 3;
            AssertConversionError("사건 시간대");
        }

        [Test]
        public void Error_CollectsAllErrorsAtOnce()
        {
            alibi.openOptionX = false;
            clubSupply.tagCategory = "";
            var ex = Assert.Throws<PackConversionException>(() => PackConverter.Convert(template, pack));
            Assert.AreEqual(2, ex.Errors.Count, ex.Message);
        }

        void AssertConversionError(string expectedFragment)
        {
            var ex = Assert.Throws<PackConversionException>(() => PackConverter.Convert(template, pack));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains(expectedFragment)), ex.Message);
        }

        // ---- 헬퍼 ----

        T Make<T>(string assetName) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = assetName;
            created.Add(obj);
            return obj;
        }

        TagDef Tag(string id, string category)
        {
            var tag = Make<TagDef>("Tag_" + id);
            tag.id = id;
            tag.displayName = id;
            tag.category = category;
            return tag;
        }

        EntityDef Entity(string id, EntityKind kind, params TagDef[] tags)
        {
            var entity = Make<EntityDef>("Entity_" + id);
            entity.id = id;
            entity.displayName = id;
            entity.kind = kind;
            entity.tags.AddRange(tags);
            return entity;
        }

        SuspectProfile Profile(EntityDef entity, LieStyle lieStyle)
        {
            var profile = Make<SuspectProfile>("Suspect_" + entity.id);
            profile.entity = entity;
            profile.lieStyle = lieStyle;
            return profile;
        }

        ClueRule Rule(string id, ConstraintType type, string axisA)
        {
            var rule = Make<ClueRule>("Rule_" + id);
            rule.id = id;
            rule.constraintType = type;
            rule.axisA = axisA;
            return rule;
        }

        static CaseTemplate.AxisSlot Axis(string axisId, EntityKind kind, int pickCount, params TagDef[] requiredTags)
        {
            var slot = new CaseTemplate.AxisSlot { axisId = axisId, kind = kind, pickCount = pickCount };
            slot.requiredTags.AddRange(requiredTags);
            return slot;
        }
    }
}
