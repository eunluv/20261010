using System.Collections.Generic;
using System.Linq;
using Detective.Core.Logic;
using Detective.Core.Model;
using NUnit.Framework;

namespace Detective.Tests.Core
{
    public class CaseGeneratorTests
    {
        const int SeedCount = 200;

        // ---- 결정성 ----

        [Test]
        public void SameSeed_ProducesIdenticalCase()
        {
            var template = TestPack.Create();
            for (int seed = 0; seed < 20; seed++)
            {
                var first = CaseGenerator.Generate(template, seed);
                var second = CaseGenerator.Generate(template, seed);
                Assert.IsNotNull(first, $"seed {seed}");
                Assert.AreEqual(first.ToDebugString(), second.ToDebugString(), $"seed {seed}");
            }
        }

        [Test]
        public void SameSeed_FreshTemplateData_ProducesIdenticalCase()
        {
            // 템플릿 데이터를 새로 만들어도(= 게임을 다시 켜도) 결과가 같아야 한다.
            var first = CaseGenerator.Generate(TestPack.Create(), 777);
            var second = CaseGenerator.Generate(TestPack.Create(), 777);
            Assert.AreEqual(first.ToDebugString(), second.ToDebugString());
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentCases()
        {
            var template = TestPack.Create();
            var dumps = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed);
                // 시드 번호 줄을 빼고 내용만 비교한다.
                string dump = instance.ToDebugString();
                dumps.Add(dump.Substring(dump.IndexOf('\n')));
            }
            Assert.Greater(dumps.Count, 20, "시드가 달라도 사건이 거의 같습니다.");
        }

        // ---- 풀 수 있는 사건인가 ----

        [TestCase(Difficulty.Easy)]
        [TestCase(Difficulty.Normal)]
        [TestCase(Difficulty.Hard)]
        public void TwoHundredSeeds_EveryCaseHasExactlyTheIntendedSolution(Difficulty difficulty)
        {
            var template = TestPack.Create();
            int succeeded = 0;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed, difficulty);
                if (instance == null) continue;
                succeeded++;

                var truth = instance.GetTruth();
                var solver = new Solver(instance.Space);

                // 모든 단서가 진짜 정답에 대해 참이다.
                foreach (var clue in instance.Clues)
                    Assert.IsTrue(clue.Constraint.Holds(truth), $"seed {seed}: 거짓 단서 '{clue.DebugText}'\n{instance.ToDebugString()}");

                // 진짜 단서만으로 정답이 정확히 1개이고, 그것이 사건의 정답이다.
                var real = instance.Clues.Where(c => !c.IsRedHerring).Select(c => c.Constraint);
                var remaining = solver.AliveCombos(solver.Apply(solver.AllAlive(), real));
                Assert.AreEqual(1, remaining.Count, $"seed {seed}\n{instance.ToDebugString()}");
                CollectionAssert.AreEqual(truth, remaining[0], $"seed {seed}\n{instance.ToDebugString()}");

                // 가짜 단서를 섞어도 결과가 같다.
                var all = instance.Clues.Select(c => c.Constraint);
                Assert.AreEqual(1, solver.CountAlive(solver.Apply(solver.AllAlive(), all)), $"seed {seed}");
            }

            // 이 팩은 세 축 모두 직접 배제 규칙이 있어서 항상 만들 수 있어야 한다.
            Assert.AreEqual(SeedCount, succeeded, "생성에 실패한 시드가 있습니다.");
        }

        [Test]
        public void EliminatedCounts_AddUpToWholeSpace()
        {
            var template = TestPack.Create();
            for (int seed = 0; seed < 50; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed);
                int eliminated = instance.Clues.Sum(c => c.Eliminated);
                Assert.AreEqual(instance.Space.CombinationCount - 1, eliminated, $"seed {seed}\n{instance.ToDebugString()}");
                foreach (var clue in instance.Clues.Where(c => !c.IsRedHerring))
                    Assert.Greater(clue.Eliminated, 0, $"seed {seed}: 아무것도 못 지운 단서 '{clue.DebugText}'");
            }
        }

        [TestCase(Difficulty.Normal)]
        [TestCase(Difficulty.Hard)]
        public void Trimmed_NoRealClueIsRedundant(Difficulty difficulty)
        {
            var template = TestPack.Create();
            for (int seed = 0; seed < 50; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed, difficulty);
                var solver = new Solver(instance.Space);
                var real = instance.Clues.Where(c => !c.IsRedHerring).ToList();

                for (int skip = 0; skip < real.Count; skip++)
                {
                    var others = real.Where((c, i) => i != skip).Select(c => c.Constraint);
                    int alive = solver.CountAlive(solver.Apply(solver.AllAlive(), others));
                    Assert.Greater(alive, 1, $"seed {seed}: '{real[skip].DebugText}'를 빼도 풀립니다.\n{instance.ToDebugString()}");
                }
            }
        }

        // ---- 구성 ----

        [Test]
        public void Options_PickedFromPool_SortedById()
        {
            var template = TestPack.Create();
            for (int seed = 0; seed < 30; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed);
                for (int a = 0; a < template.Axes.Count; a++)
                {
                    var options = instance.Space.Axes[a].OptionIds;
                    Assert.AreEqual(template.Axes[a].AxisId, instance.Space.AxisId(a));
                    Assert.AreEqual(template.Axes[a].PickCount, options.Count);
                    CollectionAssert.IsSubsetOf(options, template.Axes[a].CandidateIds);
                    CollectionAssert.AreEqual(options.OrderBy(id => id, System.StringComparer.Ordinal), options);
                }
            }
        }

        [Test]
        public void RedHerrings_RequestedCount_NotDuplicatingRealClues()
        {
            var template = TestPack.Create(redHerrings: 2);
            for (int seed = 0; seed < 50; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed);
                var herrings = instance.Clues.Where(c => c.IsRedHerring).ToList();
                Assert.AreEqual(2, herrings.Count, $"seed {seed}");

                var texts = instance.Clues.Select(c => c.DebugText).ToList();
                Assert.AreEqual(texts.Count, texts.Distinct().Count(), $"seed {seed}: 같은 단서가 두 번 나왔습니다.\n{instance.ToDebugString()}");
                foreach (var herring in herrings) Assert.Greater(herring.EliminatedAlone, 0, "하나 마나 한 가짜 단서");

                // 진짜 단서가 먼저, 가짜 단서가 뒤에.
                int firstHerring = instance.Clues.ToList().FindIndex(c => c.IsRedHerring);
                Assert.IsTrue(instance.Clues.Skip(firstHerring).All(c => c.IsRedHerring));
            }
        }

        [Test]
        public void TryGenerate_NotEnoughCandidates_ReturnsNull()
        {
            var template = TestPack.Create(pickCount: 6); // 후보는 축마다 5개
            Assert.IsNull(CaseGenerator.TryGenerate(template, 1));
            Assert.IsNull(CaseGenerator.Generate(template, 1));
        }

        [Test]
        public void Generate_NoRuleForAnAxis_ReturnsNull()
        {
            // 범인 축만 좁힐 수 있으면 장소·도구는 절대 하나로 정해지지 않는다.
            var template = TestPack.Create(onlyRuleIds: new[] { "alibi" });
            Assert.IsNull(CaseGenerator.Generate(template, 1));
        }

        [Test]
        public void Generate_AttemptsAndUsedSeed_FollowDerivation()
        {
            var template = TestPack.Create();
            var instance = CaseGenerator.Generate(template, 5);
            Assert.AreEqual(5, instance.Seed);
            Assert.AreEqual(unchecked(5 * 31 + (instance.Attempts - 1)), instance.UsedSeed);

            var direct = CaseGenerator.TryGenerate(template, instance.UsedSeed);
            Assert.AreEqual(1, direct.Attempts);
            CollectionAssert.AreEqual(instance.GetTruth(), direct.GetTruth());
        }

        // ---- 규칙 확장 ----

        [Test]
        public void Expand_KeepsOnlyCluesTrueForTruth()
        {
            var template = TestPack.Create();
            var instance = CaseGenerator.Generate(template, 3);
            var truth = instance.GetTruth();

            var all = RuleExpander.ExpandAll(template, instance.Space);
            var kept = RuleExpander.Expand(template, instance.Space, truth);

            Assert.Greater(all.Count, kept.Count);
            Assert.IsTrue(kept.All(c => c.Constraint.Holds(truth)));
            Assert.AreEqual(all.Count(c => c.Constraint.Holds(truth)), kept.Count);

            // 알리바이(IsNot culprit ?)는 범인이 아닌 사람 수만큼 나온다.
            Assert.AreEqual(instance.Space.OptionCount(0) - 1, kept.Count(c => c.Rule.Id == "alibi"));
        }

        [Test]
        public void Expand_OpenTag_OnlyTagsOfThatCategory()
        {
            var template = TestPack.Create();
            var instance = CaseGenerator.Generate(template, 3);
            var floorClues = RuleExpander.ExpandAll(template, instance.Space).Where(c => c.Rule.Id == "floor_seen").ToList();

            CollectionAssert.AreEqual(new[] { "floor.1", "floor.2" }, floorClues.Select(c => c.TagId));
        }

        [Test]
        public void Expand_OneOfBothOpen_NoMirroredPairs()
        {
            var template = TestPack.Create();
            var instance = CaseGenerator.Generate(template, 3);
            int n = instance.Space.OptionCount(0);
            int pairs = RuleExpander.ExpandAll(template, instance.Space).Count(c => c.Rule.Id == "one_of_two");
            Assert.AreEqual(n * (n - 1) / 2, pairs);
        }

        // ---- 문장 ----

        [Test]
        public void ClueText_VariablesReplacedWithDisplayNames()
        {
            var template = TestPack.Create();
            bool sawAlibi = false, sawFloor = false, sawOneOf = false;

            for (int seed = 0; seed < 60; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed, Difficulty.Easy);
                foreach (var clue in instance.Clues)
                {
                    Assert.IsFalse(clue.Text.Contains("{"), $"안 바뀐 변수: {clue.Text}");

                    if (clue.RuleId == "alibi")
                    {
                        sawAlibi = true;
                        var isNot = (IsNot)clue.Constraint;
                        StringAssert.Contains(instance.DisplayName(isNot.Axis, isNot.Option), clue.Text);
                        StringAssert.Contains("15시", clue.Text);
                    }
                    if (clue.RuleId == "floor_seen")
                    {
                        sawFloor = true;
                        Assert.IsTrue(clue.Text.Contains("1층") || clue.Text.Contains("2층"), clue.Text);
                    }
                    if (clue.RuleId == "one_of_two")
                    {
                        sawOneOf = true;
                        var oneOf = (OneOf)clue.Constraint;
                        StringAssert.Contains(instance.DisplayName(oneOf.Axis, oneOf.OptionX), clue.Text);
                        StringAssert.Contains(instance.DisplayName(oneOf.Axis, oneOf.OptionY), clue.Text);
                    }
                }
            }

            Assert.IsTrue(sawAlibi && sawFloor && sawOneOf, "60개 시드에서 확인할 규칙이 한 번도 안 나왔습니다.");
        }

        [Test]
        public void ClueText_UnboundVariable_LeftAsIs()
        {
            var template = TestPack.Create();
            var instance = CaseGenerator.Generate(template, 3);
            var candidate = RuleExpander.ExpandAll(template, instance.Space).First(c => c.Rule.Id == "alibi");

            string text = ClueTextRenderer.Render("{suspect}는 {item}을 못 봤다 {nope}", template, instance.Space, candidate);
            StringAssert.Contains("{item}", text);
            StringAssert.Contains("{nope}", text);
            StringAssert.DoesNotContain("{suspect}", text);
        }
    }
}
