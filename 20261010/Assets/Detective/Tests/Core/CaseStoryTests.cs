using System.Collections.Generic;
using System.Linq;
using Detective.Core.Logic;
using Detective.Core.Model;
using NUnit.Framework;

namespace Detective.Tests.Core
{
    // 타임라인·증언·거짓말·반박 증거·반박 보상·배치 (tool-design 6장).
    public class CaseStoryTests
    {
        const int SeedCount = 200;

        static IEnumerable<CaseInstance> Cases(CaseTemplateData template, int count, Difficulty? difficulty = null)
        {
            for (int seed = 0; seed < count; seed++)
            {
                var instance = CaseGenerator.Generate(template, seed, difficulty);
                Assert.IsNotNull(instance, $"seed {seed}: 생성 실패");
                yield return instance;
            }
        }

        // ---- 전체 조건 ----

        [TestCase(Difficulty.Easy)]
        [TestCase(Difficulty.Normal)]
        [TestCase(Difficulty.Hard)]
        public void TwoHundredSeeds_NoViolations(Difficulty difficulty)
        {
            foreach (var c in Cases(TestPack.Create(), SeedCount, difficulty))
            {
                var violations = CaseVerifier.FindViolations(c);
                Assert.IsEmpty(violations, $"seed {c.Seed}:\n{string.Join("\n", violations)}\n\n{c.ToDebugString()}");
            }
        }

        // ---- 타임라인 ----

        [Test]
        public void Timeline_AgreesWithTruth()
        {
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                var t = c.Timeline;
                Assert.IsNotNull(t, $"seed {c.Seed}");
                int culprit = c.TruthOption(t.SuspectAxis);
                int crimePlace = c.TruthOption(t.PlaceAxis);

                for (int s = 0; s < t.SuspectCount; s++)
                    Assert.AreEqual(s == culprit, t.LocationOf(s, t.CrimeSlot) == crimePlace,
                        $"seed {c.Seed}: 사건 시간대에 사건 장소에 있는 사람은 범인뿐이어야 합니다.");

                var handled = t.Facts.Where(f => f.Kind == FactKind.Handled).ToList();
                Assert.AreEqual(1, handled.Count);
                Assert.AreEqual(Fact.Handled(culprit, crimePlace, t.CrimeSlot, c.TruthOption(t.ItemAxis)), handled[0]);

                foreach (var fact in t.Facts) Assert.IsTrue(t.IsTrue(fact), $"seed {c.Seed}: {fact}");
                Assert.AreEqual(t.SuspectCount * t.SlotCount, t.Facts.Count(f => f.Kind == FactKind.Presence));
            }
        }

        [Test]
        public void Timeline_SawFacts_ExactlyWhenSamePlaceAndTime()
        {
            foreach (var c in Cases(TestPack.Create(), 50))
            {
                var t = c.Timeline;
                for (int slot = 0; slot < t.SlotCount; slot++)
                    for (int a = 0; a < t.SuspectCount; a++)
                        for (int b = 0; b < t.SuspectCount; b++)
                        {
                            if (a == b) continue;
                            bool together = t.LocationOf(a, slot) == t.LocationOf(b, slot);
                            bool recorded = t.Facts.Contains(Fact.Saw(a, t.LocationOf(a, slot), slot, b));
                            Assert.AreEqual(together, recorded, $"seed {c.Seed}: slot {slot}, {a} → {b}");
                        }
            }
        }

        // ---- 단서 문장 ----

        [Test]
        public void AlibiClue_TextComesFromTruePresenceFact()
        {
            int seen = 0;
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                var t = c.Timeline;
                foreach (var clue in c.Clues.Where(x => x.RuleId == "alibi"))
                {
                    seen++;
                    var isNot = (IsNot)clue.Constraint;
                    Assert.IsTrue(clue.SourceFact.HasValue, clue.DebugText);

                    var fact = clue.SourceFact.Value;
                    Assert.AreEqual(Fact.Presence(isNot.Option, t.LocationOf(isNot.Option, t.CrimeSlot), t.CrimeSlot), fact);
                    Assert.IsTrue(t.IsTrue(fact));
                    Assert.AreNotEqual(c.TruthOption(t.PlaceAxis), fact.Where, "알리바이 장소가 사건 장소입니다.");

                    StringAssert.Contains(c.DisplayName(t.SuspectAxis, isNot.Option), clue.Text);
                    StringAssert.Contains(c.DisplayName(t.PlaceAxis, fact.Where), clue.Text);
                    StringAssert.Contains("15시", clue.Text);
                    StringAssert.DoesNotContain("{", clue.Text);
                }
            }
            Assert.Greater(seen, 0, "알리바이 단서가 한 번도 안 나왔습니다.");
        }

        // ---- 증언과 거짓말 ----

        [Test]
        public void Testimony_EverySuspect_ThreeToFiveLines_IncludingCrimeTime()
        {
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                var t = c.Timeline;
                Assert.AreEqual(t.SuspectCount, c.Testimonies.Count);
                for (int s = 0; s < t.SuspectCount; s++)
                {
                    var testimony = c.Testimonies[s];
                    Assert.AreEqual(s, testimony.Suspect);
                    Assert.That(testimony.Lines.Count, Is.InRange(3, 5));
                    Assert.IsTrue(testimony.Lines.All(l => l.Original.Who == s), "남의 사실을 말하는 줄이 있습니다.");
                    Assert.AreEqual(1, testimony.Lines.Count(l => l.Original.Kind == FactKind.Presence && l.Original.When == t.CrimeSlot));
                    foreach (var line in testimony.Lines) Assert.IsNotEmpty(line.Text, line.ToString());
                }
            }
        }

        [Test]
        public void Culprit_AlwaysLiesAboutCrimeTimeLocation()
        {
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                var t = c.Timeline;
                var testimony = c.Testimonies[c.TruthOption(t.SuspectAxis)];
                Assert.IsTrue(testimony.IsCulprit);

                var line = testimony.Lines.Single(l => l.Original.Kind == FactKind.Presence && l.Original.When == t.CrimeSlot);
                Assert.AreEqual(TestimonyLineKind.Lie, line.Kind, $"seed {c.Seed}");
                Assert.AreEqual(c.TruthOption(t.PlaceAxis), line.Original.Where);
                Assert.AreNotEqual(c.TruthOption(t.PlaceAxis), line.Stated.Where, "범인이 사건 장소에 있었다고 자백했습니다.");
            }
        }

        [Test]
        public void LieStyles_ShapeTheTestimony()
        {
            bool sawOtherLiar = false;
            foreach (var c in Cases(TestPack.Create(lieCount: 2), 100))
            {
                var t = c.Timeline;
                foreach (var testimony in c.Testimonies)
                {
                    int hidden = testimony.Lines.Count(l => l.Kind == TestimonyLineKind.Hidden);
                    int exaggerated = testimony.Lines.Count(l => l.Kind == TestimonyLineKind.Exaggerated);
                    int lies = testimony.Lines.Count(l => l.Kind == TestimonyLineKind.Lie);

                    Assert.AreEqual(testimony.Style == LieStyle.Omitter ? 1 : 0, hidden, $"seed {c.Seed}: {testimony.Style}");
                    Assert.AreEqual(testimony.Style == LieStyle.Exaggerator ? 1 : 0, exaggerated, $"seed {c.Seed}: {testimony.Style}");
                    Assert.LessOrEqual(lies, 1);

                    if (!testimony.IsCulprit && lies > 0)
                    {
                        sawOtherLiar = true;
                        Assert.AreEqual(LieStyle.Liar, testimony.Style, "Liar 성향이 아닌 용의자가 거짓말했습니다.");
                    }
                    foreach (var line in testimony.Lines.Where(l => l.Kind == TestimonyLineKind.Exaggerated))
                        StringAssert.StartsWith("맹세코, ", line.Text);
                    foreach (var line in testimony.Lines.Where(l => l.Kind != TestimonyLineKind.Lie))
                        Assert.IsTrue(t.IsTrue(line.Stated), $"seed {c.Seed}: 거짓 줄이 아닌데 거짓입니다. {line}");
                }

                Assert.LessOrEqual(c.Testimonies.Count(x => x.HasLie), 2, $"seed {c.Seed}: 거짓말한 사람이 lieCount보다 많습니다.");
            }
            Assert.IsTrue(sawOtherLiar, "범인 말고 거짓말하는 용의자가 한 번도 안 나왔습니다.");
        }

        [Test]
        public void LieCountOne_OnlyCulpritLies()
        {
            foreach (var c in Cases(TestPack.Create(lieCount: 1), 60))
            {
                var liars = c.Testimonies.Where(x => x.HasLie).ToList();
                Assert.AreEqual(1, liars.Count, $"seed {c.Seed}");
                Assert.IsTrue(liars[0].IsCulprit);
            }
        }

        // ---- 반박 증거 ----

        [Test]
        public void EveryLie_HasExactlyOneEvidence_ThatIsPlacedAndContradictsIt()
        {
            foreach (var c in Cases(TestPack.Create(lieCount: 3), 100))
            {
                var t = c.Timeline;
                var lies = c.Testimonies.SelectMany(x => x.Lines).Where(l => l.Kind == TestimonyLineKind.Lie).ToList();
                Assert.AreEqual(lies.Count, c.Evidence.Count, $"seed {c.Seed}");

                foreach (var lie in lies)
                {
                    Assert.IsFalse(t.IsTrue(lie.Stated), $"seed {c.Seed}: 거짓 줄이 사실입니다. {lie}");

                    var matching = c.Evidence.Select((e, i) => (e, i)).Where(x => x.e.RebutsLineId == lie.Id).ToList();
                    Assert.AreEqual(1, matching.Count, $"seed {c.Seed}: {lie.Id}");
                    var (evidence, index) = matching[0];

                    Assert.IsTrue(t.IsTrue(evidence.Fact), "증거의 사실이 거짓입니다.");
                    Assert.AreEqual(lie.Stated.When, evidence.Fact.When);
                    Assert.AreNotEqual(lie.Stated.Where, evidence.Fact.Where, "증거가 거짓말과 모순되지 않습니다.");
                    Assert.IsNotEmpty(evidence.Text);

                    var placements = c.Placements.Where(p => p.IsEvidence && p.Index == index).ToList();
                    Assert.AreEqual(1, placements.Count, "증거의 배치가 하나가 아닙니다.");
                    Assert.AreNotEqual(AcquireVia.RebuttalReward, placements[0].Via);
                    Assert.GreaterOrEqual(placements[0].Target, 0);
                    if (placements[0].Via == AcquireVia.Interview)
                        Assert.AreNotEqual(lie.Stated.Who, placements[0].Target, "거짓말한 본인에게서 증거를 얻게 되어 있습니다.");
                }
            }
        }

        [Test]
        public void CulpritEvidence_DoesNotNameTheCrimeScene()
        {
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                var t = c.Timeline;
                int culprit = c.TruthOption(t.SuspectAxis);
                string crimePlace = c.DisplayName(t.PlaceAxis, c.TruthOption(t.PlaceAxis));
                var line = c.Testimonies[culprit].Lines.Single(l => l.Kind == TestimonyLineKind.Lie);
                var evidence = c.Evidence.Single(e => e.RebutsLineId == line.Id);

                Assert.IsFalse(evidence.RevealsFact);
                StringAssert.DoesNotContain(crimePlace, evidence.Text, $"seed {c.Seed}: 증거 문장이 사건 장소를 그대로 말합니다.");
                StringAssert.Contains(c.DisplayName(t.PlaceAxis, line.Stated.Where), evidence.Text);
            }
        }

        // ---- 반박 보상 ----

        [TestCase(Difficulty.Easy)]
        [TestCase(Difficulty.Normal)]
        [TestCase(Difficulty.Hard)]
        public void WithoutRewardClue_AtLeastTwoCandidatesRemain(Difficulty difficulty)
        {
            foreach (var c in Cases(TestPack.Create(redHerrings: 3), 100, difficulty))
            {
                var solver = new Solver(c.Space);
                var rewards = c.Clues.Where(x => x.IsRebuttalReward).ToList();
                Assert.That(rewards.Count, Is.InRange(1, 2), $"seed {c.Seed}");
                Assert.AreEqual(1, rewards.Select(r => r.RewardLineId).Distinct().Count(), "보상이 여러 줄에 흩어져 있습니다.");
                Assert.AreEqual(TestimonyLineKind.Lie, c.FindLine(rewards[0].RewardLineId).Kind);

                foreach (var reward in rewards)
                {
                    // 가짜 단서까지 전부 모아도 보상 단서 하나가 없으면 풀리지 않는다.
                    var others = c.Clues.Where(x => x != reward).Select(x => x.Constraint);
                    int alive = solver.CountAlive(solver.Apply(solver.AllAlive(), others));
                    Assert.GreaterOrEqual(alive, 2, $"seed {c.Seed}: '{reward.DebugText}' 없이도 풀립니다.\n{c.ToDebugString()}");

                    var placement = c.Placements.Single(p => !p.IsEvidence && c.Clues[p.Index] == reward);
                    Assert.AreEqual(AcquireVia.RebuttalReward, placement.Via);
                    Assert.AreEqual(reward.RewardLineId, placement.LineId);
                }
            }
        }

        // ---- 배치와 행동력 ----

        [Test]
        public void Placement_EveryClueOnce_ViaAllowedSource()
        {
            foreach (var c in Cases(TestPack.Create(), 100))
            {
                for (int i = 0; i < c.Clues.Count; i++)
                {
                    var placements = c.Placements.Where(p => !p.IsEvidence && p.Index == i).ToList();
                    Assert.AreEqual(1, placements.Count, $"seed {c.Seed}: 단서 {i}");
                    if (c.Clues[i].IsRebuttalReward) continue;

                    // 테스트 팩의 규칙은 모두 조사(Investigate)로만 얻는다.
                    Assert.AreEqual(AcquireVia.Investigate, placements[0].Via);
                    Assert.That(placements[0].Target, Is.InRange(0, c.Timeline.PlaceCount - 1));
                }
            }
        }

        [Test]
        public void RequiredActions_FitWithinActionPointsMinusMargin()
        {
            var template = TestPack.Create(actionPoints: 8, actionMargin: 1);
            foreach (var c in Cases(template, 100))
            {
                int expected = c.Clues.Count(x => x.IsEssential && !x.IsRebuttalReward) + 1;
                Assert.AreEqual(expected, c.RequiredActions, $"seed {c.Seed}");
                Assert.LessOrEqual(c.RequiredActions, 7, $"seed {c.Seed}");
            }
        }

        [Test]
        public void TooFewActionPoints_GenerationFails()
        {
            // 세 축을 좁히려면 단서가 적어도 3개 필요하다. 보상 2개를 빼도 행동 2회(단서 1 + 증거 1)는 든다.
            var template = TestPack.Create(actionPoints: 2, actionMargin: 1);
            Assert.IsNull(CaseGenerator.Generate(template, 1));
        }

        [Test]
        public void NoTestimonyText_StillBuildsFactsWithoutSentences()
        {
            var c = CaseGenerator.Generate(TestPack.Create(withTestimonyText: false), 4);
            Assert.IsNotNull(c);
            Assert.IsEmpty(CaseVerifier.FindViolations(c));
            Assert.IsTrue(c.Testimonies.SelectMany(x => x.Lines).All(l => l.Text == ""));
        }
    }
}
