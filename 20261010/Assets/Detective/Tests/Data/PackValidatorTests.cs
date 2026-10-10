using System.Collections.Generic;
using System.Linq;
using Detective.Core.Model;
using Detective.Data;
using NUnit.Framework;
using UnityEngine;

namespace Detective.Tests.Data
{
    public class PackValidatorTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        WorldPack pack;
        CaseTemplate template;
        TagDef clubDrama, clubCooking;
        EntityDef sDrama, sCook, pBackstage, pKitchen;
        ClueRule alibi, clubMatch;

        [SetUp]
        public void SetUp()
        {
            clubDrama = Tag("club.drama", "club");
            clubCooking = Tag("club.cooking", "club");

            sDrama = Entity("s_drama", EntityKind.Suspect, clubDrama);
            sCook = Entity("s_cook", EntityKind.Suspect, clubCooking);
            pBackstage = Entity("p_backstage", EntityKind.Place, clubDrama);
            pKitchen = Entity("p_kitchen", EntityKind.Place, clubCooking);

            alibi = Rule("alibi", ConstraintType.IsNot, "culprit");
            alibi.textVariants.Add("{suspect}는 {time}에 다른 곳에 있었다.");

            clubMatch = Rule("club_match", ConstraintType.SameTag, "culprit");
            clubMatch.axisB = "place";
            clubMatch.tagCategory = "club";
            clubMatch.textVariants.Add("범인은 그 장소의 동아리 소속이다.");

            template = Make<CaseTemplate>("Case_Test");
            template.id = "test_case";
            template.title = "테스트 사건";
            template.requestText = "의뢰 문장";
            template.axes.Add(new CaseTemplate.AxisSlot { axisId = "culprit", kind = EntityKind.Suspect, pickCount = 2 });
            template.axes.Add(new CaseTemplate.AxisSlot { axisId = "place", kind = EntityKind.Place, pickCount = 2 });
            template.rules.AddRange(new[] { alibi, clubMatch });
            template.timeSlots.AddRange(new[] { "14시", "15시" });
            template.crimeSlotIndex = 1;

            pack = Make<WorldPack>("Pack_Test");
            pack.displayName = "테스트 팩";
            pack.tags.AddRange(new[] { clubDrama, clubCooking });
            pack.entities.AddRange(new[] { sDrama, sCook, pBackstage, pKitchen });
            pack.suspectProfiles.Add(Profile(sDrama));
            pack.suspectProfiles.Add(Profile(sCook));
            pack.rules.AddRange(new[] { alibi, clubMatch });
            pack.templates.Add(template);

            var text = Make<TestimonyText>("TestimonyText");
            text.presenceLines.Add("{time}에는 {place}에 있었어.");
            text.sawLines.Add("{time}에 {place}에서 {suspect}를 봤어.");
            text.evidencePresenceLines.Add("{suspect}는 {time}에 {place}에 있었다.");
            text.evidenceAbsenceLines.Add("{suspect}는 {time}에 {place}에 없었다.");
            pack.testimonyText = text;
        }

        [Test]
        public void MissingTestimonyText_IsWarning()
        {
            pack.testimonyText = null;
            AssertProblem(ProblemSeverity.Warning, pack, "증언 문장");
        }

        [Test]
        public void TestimonyTextWrongVariable_IsError()
        {
            pack.testimonyText.presenceLines.Add("{suspect2}랑 같이 있었어.");
            AssertProblem(ProblemSeverity.Error, pack.testimonyText, "{suspect2}");
        }

        [Test]
        public void AlibiRule_MayUsePlace2()
        {
            alibi.textVariants.Add("{suspect}는 {time}에 {place2}에 있었다.");
            var problems = PackValidator.Validate(pack);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void MarginNotBelowActionPoints_IsError()
        {
            template.actionMargin = template.actionPoints;
            AssertProblem(ProblemSeverity.Error, template, "여유값");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void CleanPack_HasNoProblems()
        {
            var problems = PackValidator.Validate(pack);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void CandidateShortage_IsError()
        {
            template.axes[1].pickCount = 3; // 장소는 2개뿐
            AssertProblem(ProblemSeverity.Error, template, "후보가 2개뿐");
        }

        [Test]
        public void RequiredTag_NarrowsCandidates()
        {
            template.axes[1].requiredTags.Add(clubDrama);
            Assert.AreEqual(1, PackValidator.Candidates(pack, template.axes[1]).Count);
            AssertProblem(ProblemSeverity.Error, template, "후보가 1개뿐");
        }

        [Test]
        public void UnknownTextVariable_IsError()
        {
            alibi.textVariants.Add("{weapon}이 사라졌다.");
            AssertProblem(ProblemSeverity.Error, alibi, "{weapon}");
        }

        [Test]
        public void TextVariableRuleCannotFill_IsError()
        {
            // IsNot(culprit, ?)는 용의자만 채운다. {item}은 채울 값이 없다.
            alibi.textVariants.Add("{suspect}는 {item}을 들고 있었다.");
            AssertProblem(ProblemSeverity.Error, template, "item을 채우지 않습니다");
        }

        [Test]
        public void TextVariableSecondSlot_OkForOneOf()
        {
            var oneOf = Rule("one_of", ConstraintType.OneOf, "culprit");
            oneOf.textVariants.Add("범인은 {suspect} 아니면 {suspect2}다.");
            pack.rules.Add(oneOf);
            template.rules.Add(oneOf);

            var problems = PackValidator.Validate(pack);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void FindUnknownVariables_AcceptsKnownOnes()
        {
            CollectionAssert.IsEmpty(PackValidator.FindUnknownVariables("{suspect} {place} {item} {time} {tag}"));
            CollectionAssert.AreEqual(new[] { "floor", "" }, PackValidator.FindUnknownVariables("{floor} 복도, {} 그리고 {floor}"));
        }

        [Test]
        public void EntityWithTagOutsidePack_IsError()
        {
            var stray = Tag("trait.tall", "trait");
            sDrama.tags.Add(stray);
            AssertProblem(ProblemSeverity.Error, sDrama, "이 팩의 태그가 아닙니다");
        }

        [Test]
        public void SuspectWithoutProfile_IsWarning()
        {
            pack.suspectProfiles.RemoveAt(1);
            AssertProblem(ProblemSeverity.Warning, sCook, "용의자 프로필");
        }

        [Test]
        public void RuleAxisMissingFromTemplate_IsError()
        {
            alibi.axisA = "motive";
            AssertProblem(ProblemSeverity.Error, template, "축 'motive'가 이 템플릿에 없습니다");
        }

        [Test]
        public void FixedOptionOfWrongKind_IsError()
        {
            alibi.openOptionX = false;
            alibi.optionX = pKitchen; // culprit 축은 Suspect
            AssertProblem(ProblemSeverity.Error, template, "옵션 X는 Place");
        }

        [Test]
        public void HasTagNobodyHas_IsUnusableWarning()
        {
            var lefty = Tag("trait.lefty", "trait");
            pack.tags.Add(lefty);
            var rule = Rule("lefty", ConstraintType.HasTag, "culprit");
            rule.tag = lefty;
            rule.textVariants.Add("왼손 자국이 남아 있다.");
            pack.rules.Add(rule);
            template.rules.Add(rule);

            AssertProblem(ProblemSeverity.Warning, template, "쓸 수 없습니다");
        }

        [Test]
        public void SameTagUnknownCategory_IsError()
        {
            clubMatch.tagCategory = "floor";
            AssertProblem(ProblemSeverity.Error, clubMatch, "카테고리 'floor'");
        }

        [Test]
        public void UnusedRule_IsWarning()
        {
            template.rules.Remove(alibi);
            AssertProblem(ProblemSeverity.Warning, alibi, "쓰이지 않습니다");
        }

        [Test]
        public void DuplicateEntityId_IsError()
        {
            sCook.id = "s_drama";
            AssertProblem(ProblemSeverity.Error, sCook, "겹칩니다");
        }

        void AssertProblem(ProblemSeverity severity, Object target, string fragment)
        {
            var problems = PackValidator.Validate(pack);
            Assert.IsTrue(problems.Any(p => p.Severity == severity && p.Target == target && p.Message.Contains(fragment)),
                $"'{fragment}' 문제를 찾지 못했습니다. 나온 문제:\n" + string.Join("\n", problems));
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

        SuspectProfile Profile(EntityDef entity)
        {
            var profile = Make<SuspectProfile>("Suspect_" + entity.id);
            profile.entity = entity;
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
    }
}
