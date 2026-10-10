using System;
using System.Collections.Generic;
using System.Linq;
using Detective.Core.Logic;
using Detective.Core.Model;
using Detective.Data;
using Detective.Runtime;
using Detective.Tests.Core;
using NUnit.Framework;
using UnityEditor;

namespace Detective.Tests.Runtime
{
    // 자동 플레이어: FlowRunner를 한 걸음씩 돌리면서 CaseSession에 입력을 넣어 한 판을 끝까지 진행한다.
    public class AutoPlayerTests
    {
        const string PackFolder = "Assets/Packs/SchoolFestival";
        const int SeedCount = 100;

        // ---- 자동 플레이어 ----

        // 러너가 끝날 때까지, 걸음마다 현재 페이즈에 맞는 입력을 넣는다. 결과 페이즈의 복기를 돌려준다.
        static CaseReview Play(CaseSession session, CaseFlow flow, Action<CaseSession> player)
        {
            CaseReview review = null;
            var run = new FlowRunner().Run(flow, session);
            int steps = 0;
            while (run.MoveNext())
            {
                Assert.Less(++steps, 500, "러너가 끝나지 않습니다. 현재 페이즈: " + session.Phase);
                if (session.CurrentPhase is ResultPhase result)
                {
                    review = result.Review;
                    result.Finish();
                }
                else player(session);
            }
            Assert.IsNotNull(review, "결과 페이즈에 도달하지 못했습니다.");
            return review;
        }

        // 정답 경로: 필요한 아이템만 얻고 → 맞는 증거로 반박하고 → 정답을 지목한다.
        static void SolvingPlayer(CaseSession s)
        {
            var c = s.Instance;
            switch (s.CurrentPhase)
            {
                case DialoguePhase dialogue:
                    dialogue.Advance();
                    break;

                case InvestigatePhase investigate:
                    string keyLine = c.Clues.Where(x => x.IsRebuttalReward).Select(x => x.RewardLineId).FirstOrDefault();
                    foreach (var p in c.Placements)
                    {
                        bool needed = p.IsEvidence
                            ? c.Evidence[p.Index].RebutsLineId == keyLine
                            : c.Clues[p.Index].IsEssential && !c.Clues[p.Index].IsRebuttalReward;
                        if (!needed) continue;

                        for (int tries = 0; tries < 20 && !Has(s, p); tries++)
                            Assert.AreEqual(ActionResult.Found, Act(s, p), $"seed {c.Seed}: {p} 에서 아이템을 얻지 못했습니다.");
                        Assert.IsTrue(Has(s, p), $"seed {c.Seed}: 필요한 아이템을 못 얻었습니다. {p}");
                    }
                    investigate.Finish();
                    break;

                case TestimonyPhase testimony:
                    Assert.IsNotNull(testimony.Testimony, $"seed {c.Seed}: 증언할 사람이 없습니다.");
                    foreach (var line in testimony.Testimony.Lines.Where(l => l.Kind == TestimonyLineKind.Lie))
                    {
                        int evidence = IndexOfEvidenceFor(c, line.Id);
                        Assert.IsTrue(s.HasEvidence(evidence), $"seed {c.Seed}: 반박에 쓸 증거가 없습니다.");
                        Assert.AreEqual(RebutResult.Success, s.Rebut(line.Id, evidence), $"seed {c.Seed}");
                    }
                    Assert.IsTrue(testimony.Finish(), $"seed {c.Seed}: 반박했는데 증언이 끝나지 않습니다.");
                    break;

                case AccusePhase accuse:
                    Assert.IsTrue(accuse.Accuse(c.GetTruth()), $"seed {c.Seed}: 정답 지목이 실패했습니다.");
                    break;
            }
        }

        // 오답 경로: 증거를 하나 구해서, 거짓이 아닌 줄에 무작위로 다섯 번 들이민다.
        static Action<CaseSession> BlunderingPlayer(int seed)
        {
            var rng = new Random(seed);
            return s =>
            {
                var c = s.Instance;
                switch (s.CurrentPhase)
                {
                    case DialoguePhase dialogue:
                        dialogue.Advance();
                        break;

                    case InvestigatePhase investigate:
                        var first = c.Placements.First(p => p.IsEvidence);
                        for (int tries = 0; tries < 20 && s.Evidence.Count == 0; tries++) Act(s, first);
                        Assert.Greater(s.Evidence.Count, 0, $"seed {c.Seed}: 증거를 하나도 못 구했습니다.");
                        investigate.Finish();
                        break;

                    case TestimonyPhase testimony:
                        var honest = testimony.Testimony.Lines.Where(l => l.Kind == TestimonyLineKind.Plain).ToList();
                        Assert.IsNotEmpty(honest, $"seed {c.Seed}: 평범한 줄이 없습니다.");
                        var target = honest[rng.Next(honest.Count)];
                        Assert.AreEqual(RebutResult.Wrong, s.Rebut(target.Id, s.Evidence[0]), $"seed {c.Seed}");
                        break;

                    case AccusePhase _:
                        Assert.Fail($"seed {c.Seed}: 신뢰도가 0인데 지목 단계에 들어왔습니다.");
                        break;
                }
            };
        }

        static ActionResult Act(CaseSession s, CasePlacement p)
        {
            switch (p.Via)
            {
                case AcquireVia.Investigate: return s.Investigate(p.Target);
                case AcquireVia.Interview: return s.Interview(p.Target);
                case AcquireVia.SmallTalk: return s.SmallTalk(p.Target);
                default: return ActionResult.NotAllowed;
            }
        }

        static bool Has(CaseSession s, CasePlacement p) => p.IsEvidence ? s.HasEvidence(p.Index) : s.HasClue(p.Index);

        static int IndexOfEvidenceFor(CaseInstance c, string lineId)
        {
            for (int i = 0; i < c.Evidence.Count; i++)
                if (c.Evidence[i].RebutsLineId == lineId) return i;
            return -1;
        }

        static void AssertSolvedCleanly(CaseSession s, CaseReview review)
        {
            int seed = s.Instance.Seed;
            Assert.AreEqual(CaseOutcome.Solved, s.Outcome, $"seed {seed}");
            Assert.AreEqual(CaseOutcome.Solved, review.Outcome, $"seed {seed}");
            Assert.AreEqual(s.MaxTrust, s.Trust, $"seed {seed}: 정답 경로에서 신뢰도가 깎였습니다.");
            Assert.AreEqual(s.Instance.RequiredActions, s.ActionsUsed,
                $"seed {seed}: 생성기가 센 필요 행동 수와 실제로 쓴 행동 수가 다릅니다.");
            Assert.LessOrEqual(s.ActionsUsed, s.Instance.Template.ActionPoints - s.Instance.Template.ActionMargin, $"seed {seed}");
            Assert.AreEqual(CaseGrade.S, review.Grade, $"seed {seed}");
            CollectionAssert.AreEqual(review.Truth, review.Accusation);
            Assert.AreEqual(PhaseKind.None, s.Phase, "러너가 끝났는데 페이즈가 남아 있습니다.");

            // 얻은 단서만으로 실제로 풀린다 (필수 단서 + 반박 보상).
            var solver = new Solver(s.Instance.Space);
            var held = s.Clues.Select(i => s.Instance.Clues[i].Constraint);
            Assert.AreEqual(1, solver.CountAlive(solver.Apply(solver.AllAlive(), held)), $"seed {seed}: 얻은 단서로 정답이 하나로 좁혀지지 않습니다.");
        }

        static void AssertFailedByTrust(CaseSession s, CaseReview review)
        {
            int seed = s.Instance.Seed;
            Assert.AreEqual(CaseOutcome.Failed, s.Outcome, $"seed {seed}");
            Assert.AreEqual(CaseOutcome.Failed, review.Outcome, $"seed {seed}");
            Assert.AreEqual(0, s.Trust, $"seed {seed}");
            Assert.AreEqual(5, s.WrongRebuttals, $"seed {seed}: 오답 반박 5번에 실패해야 합니다.");
            Assert.AreEqual(CaseGrade.C, review.Grade);
            Assert.IsNull(review.Accusation, "지목 단계를 건너뛰었어야 합니다.");
        }

        // ---- 학원 축제 팩 (에셋 + 팩의 CaseFlow) ----

        static WorldPack LoadSchoolFestivalPack()
        {
            if (AssetDatabase.IsValidFolder(PackFolder))
            {
                foreach (var path in AssetDatabase.FindAssets("t:WorldPack", new[] { PackFolder }).Select(AssetDatabase.GUIDToAssetPath)
                             .OrderBy(p => p, StringComparer.Ordinal))
                {
                    var pack = AssetDatabase.LoadAssetAtPath<WorldPack>(path);
                    if (pack != null) return pack;
                }
            }
            Assert.Ignore("학원 축제 팩 에셋이 없습니다. 메뉴 Detective > Setup > Create School Festival Pack 을 먼저 실행하세요.");
            return null;
        }

        [Test]
        public void SchoolFestival_100Seeds_SolvingPlayer_ReachesSuccessEnding()
        {
            var pack = LoadSchoolFestivalPack();
            foreach (var template in pack.templates)
            {
                Assert.IsNotNull(template.flow, $"템플릿 '{template.id}'에 흐름이 없습니다. 팩 생성 메뉴를 다시 실행하세요.");
                var data = PackConverter.Convert(template, pack);
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    var session = new CaseSession(CaseGenerator.Generate(data, seed));
                    var review = Play(session, template.flow, SolvingPlayer);
                    AssertSolvedCleanly(session, review);
                }
            }
        }

        [Test]
        public void SchoolFestival_100Seeds_FiveWrongRebuttals_ReachFailureEnding()
        {
            var pack = LoadSchoolFestivalPack();
            foreach (var template in pack.templates)
            {
                var data = PackConverter.Convert(template, pack);
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    var session = new CaseSession(CaseGenerator.Generate(data, seed));
                    var review = Play(session, template.flow, BlunderingPlayer(seed));
                    AssertFailedByTrust(session, review);
                }
            }
        }

        [Test]
        public void SchoolFestival_Flow_IsTheShortCaseFlow()
        {
            var pack = LoadSchoolFestivalPack();
            foreach (var template in pack.templates)
            {
                var kinds = template.flow.phases.Select(p => p.GetType()).ToList();
                CollectionAssert.AreEqual(
                    new[] { typeof(DialogueDef), typeof(InvestigateDef), typeof(TestimonyDef), typeof(AccuseDef), typeof(ResultDef) }, kinds);

                var testimony = (TestimonyDef)template.flow.phases[2];
                Assert.AreEqual(RoleSlot.AnyLiar, testimony.speaker);
                Assert.IsTrue(testimony.mustRebut);
            }
        }

        // ---- 테스트 팩 (에셋 없이, 기본 흐름) ----

        [TestCase(Difficulty.Easy)]
        [TestCase(Difficulty.Normal)]
        [TestCase(Difficulty.Hard)]
        public void DefaultFlow_100Seeds_SolvingPlayer_ReachesSuccessEnding(Difficulty difficulty)
        {
            var data = TestPack.Create();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var session = new CaseSession(CaseGenerator.Generate(data, seed, difficulty));
                var review = Play(session, null, SolvingPlayer);
                AssertSolvedCleanly(session, review);
            }
        }

        [Test]
        public void DefaultFlow_100Seeds_FiveWrongRebuttals_ReachFailureEnding()
        {
            var data = TestPack.Create();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var session = new CaseSession(CaseGenerator.Generate(data, seed));
                var review = Play(session, null, BlunderingPlayer(seed));
                AssertFailedByTrust(session, review);
            }
        }

        // ---- 세션 규칙 ----

        // 조사 단계까지 진행한 세션과 러너.
        static (CaseSession session, System.Collections.IEnumerator run) StartAt<T>(int seed, CaseTemplateData data = null) where T : class, IPhase
        {
            // 증거를 전부 모아도 남도록 행동력을 넉넉히 준다.
            var session = new CaseSession(CaseGenerator.Generate(data ?? TestPack.Create(actionPoints: 30), seed));
            var run = new FlowRunner().Run(null, session);
            for (int steps = 0; steps < 200 && run.MoveNext(); steps++)
            {
                if (session.CurrentPhase is T) return (session, run);
                switch (session.CurrentPhase)
                {
                    case InvestigatePhase investigate:
                        // 다음 단계에서 쓸 수 있게 증거는 전부 구해 둔다.
                        foreach (var p in session.Instance.Placements.Where(p => p.IsEvidence))
                            for (int tries = 0; tries < 20 && !Has(session, p); tries++) Act(session, p);
                        investigate.Finish();
                        break;
                    case TestimonyPhase testimony:
                        testimony.GiveUp();
                        break;
                }
            }
            Assert.Fail($"{typeof(T).Name}에 도달하지 못했습니다.");
            return (null, null);
        }

        [Test]
        public void Investigate_FoundItemCostsOneAction_EmptySpotCostsNothing()
        {
            var (session, _) = StartAt<InvestigatePhase>(1);
            int before = session.ActionPoints;
            Assert.AreEqual(30, before, "조사 단계는 템플릿의 행동력으로 시작한다");

            var placement = session.Instance.Placements.First(p => p.Via == AcquireVia.Investigate);
            Assert.AreEqual(ActionResult.Found, session.Investigate(placement.Target));
            Assert.AreEqual(before - 1, session.ActionPoints);
            Assert.AreEqual(1, session.ActionsUsed);
            Assert.AreEqual(1, session.Clues.Count + session.Evidence.Count, "행동 1회에 아이템은 1개");

            // 그 장소를 다 털 때까지 조사한 뒤에는 행동력이 줄지 않는다.
            while (session.Investigate(placement.Target) == ActionResult.Found) { }
            int left = session.ActionPoints;
            Assert.AreEqual(session.ActionPoints > 0 ? ActionResult.Nothing : ActionResult.NoActionPoints, session.Investigate(placement.Target));
            Assert.AreEqual(left, session.ActionPoints);
        }

        [Test]
        public void Investigate_NotAllowedOutsideInvestigatePhase()
        {
            var (session, _) = StartAt<TestimonyPhase>(1);
            Assert.AreEqual(ActionResult.NotAllowed, session.Investigate(0));
        }

        [Test]
        public void Testimony_MustRebut_BlocksFinish_GiveUpCostsTrust()
        {
            var (session, run) = StartAt<TestimonyPhase>(2);
            var phase = (TestimonyPhase)session.CurrentPhase;

            Assert.IsTrue(phase.MustRebut);
            Assert.IsFalse(phase.CanFinish);
            Assert.IsFalse(phase.Finish());
            Assert.IsFalse(phase.IsDone);

            phase.GiveUp();
            Assert.IsTrue(phase.IsDone);
            Assert.AreEqual(session.MaxTrust - 1, session.Trust);

            run.MoveNext();
            Assert.AreEqual(PhaseKind.Accuse, session.Phase);
        }

        [Test]
        public void Rebut_CorrectEvidence_GivesRewardClues()
        {
            var (session, _) = StartAt<TestimonyPhase>(3);
            var phase = (TestimonyPhase)session.CurrentPhase;
            var lie = phase.Testimony.Lines.Single(l => l.Kind == TestimonyLineKind.Lie);
            var rewards = session.Instance.Clues.Select((c, i) => (c, i)).Where(x => x.c.RewardLineId == lie.Id).Select(x => x.i).ToList();

            Assert.IsNotEmpty(rewards, "AnyLiar는 보상이 걸린 사람이어야 합니다.");
            Assert.IsFalse(rewards.Any(session.HasClue));

            Assert.AreEqual(RebutResult.Success, session.Rebut(lie.Id, IndexOfEvidenceFor(session.Instance, lie.Id)));
            Assert.IsTrue(rewards.All(session.HasClue));
            Assert.IsTrue(session.IsRebutted(lie.Id));
            Assert.AreEqual(session.MaxTrust, session.Trust);
            Assert.IsTrue(phase.CanFinish);
            Assert.AreEqual(RebutResult.AlreadyRebutted, session.Rebut(lie.Id, IndexOfEvidenceFor(session.Instance, lie.Id)));
        }

        [Test]
        public void Rebut_WrongEvidenceOnLie_CostsTrust()
        {
            // 거짓말쟁이가 둘 이상이어서 남의 증거가 있는 사건을 찾는다.
            for (int seed = 0; seed < 50; seed++)
            {
                var (session, _) = StartAt<TestimonyPhase>(seed, TestPack.Create(lieCount: 3, actionPoints: 30));
                var lie = ((TestimonyPhase)session.CurrentPhase).Testimony.Lines.Single(l => l.Kind == TestimonyLineKind.Lie);
                var others = session.Evidence.Where(i => session.Instance.Evidence[i].RebutsLineId != lie.Id).ToList();
                if (others.Count == 0) continue;
                int wrong = others[0];

                Assert.AreEqual(RebutResult.Wrong, session.Rebut(lie.Id, wrong));
                Assert.AreEqual(session.MaxTrust - 1, session.Trust);
                Assert.IsFalse(session.IsRebutted(lie.Id));
                return;
            }
            Assert.Fail("남의 증거가 있는 사건을 찾지 못했습니다.");
        }

        [Test]
        public void Rebut_ExaggeratedLine_IsAGag_NoTrustLoss()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var (session, _) = StartAt<TestimonyPhase>(seed);
                var line = ((TestimonyPhase)session.CurrentPhase).Testimony.Lines.FirstOrDefault(l => l.Kind == TestimonyLineKind.Exaggerated);
                if (line == null) continue;

                Assert.AreEqual(RebutResult.Exaggeration, session.Rebut(line.Id, session.Evidence[0]));
                Assert.AreEqual(session.MaxTrust, session.Trust);
                Assert.AreEqual(0, session.WrongRebuttals);
                return;
            }
            Assert.Fail("과장하는 증언자가 나오는 사건을 찾지 못했습니다.");
        }

        [Test]
        public void Press_RevealsHiddenLine_OnlyOnce()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var (session, _) = StartAt<TestimonyPhase>(seed);
                var testimony = ((TestimonyPhase)session.CurrentPhase).Testimony;
                var hidden = testimony.Lines.FirstOrDefault(l => l.Kind == TestimonyLineKind.Hidden);
                if (hidden == null) continue;

                var visible = testimony.Lines.First(session.IsLineVisible);
                Assert.IsFalse(session.IsLineVisible(hidden));
                Assert.AreEqual(RebutResult.NotAllowed, session.Rebut(hidden.Id, session.Evidence[0]), "안 보이는 줄은 반박할 수 없다");

                Assert.IsTrue(session.Press(visible.Id));
                Assert.IsTrue(session.IsLineVisible(hidden));
                Assert.IsFalse(session.Press(visible.Id), "더 숨긴 줄이 없으면 false");
                Assert.AreEqual(session.MaxTrust, session.Trust, "추궁은 신뢰도를 깎지 않는다");
                return;
            }
            Assert.Fail("숨기는 증언자가 나오는 사건을 찾지 못했습니다.");
        }

        [Test]
        public void Accuse_Wrong_CostsTrustAndAllowsRetry_ThenFiveWrongFails()
        {
            var (session, run) = StartAt<AccusePhase>(4);
            var phase = (AccusePhase)session.CurrentPhase;
            int trust = session.Trust;

            var wrong = session.Instance.GetTruth();
            wrong[0] = (wrong[0] + 1) % session.Instance.Space.OptionCount(0);

            Assert.IsFalse(phase.Accuse(wrong));
            Assert.AreEqual(trust - 1, session.Trust);
            Assert.IsFalse(phase.IsDone);
            CollectionAssert.AreEqual(wrong, session.GetLastAccusation());

            Assert.IsTrue(phase.Accuse(session.Instance.GetTruth()), "틀린 뒤에도 다시 지목할 수 있다");
            Assert.AreEqual(CaseOutcome.Solved, session.Outcome);

            run.MoveNext();
            var review = ((ResultPhase)session.CurrentPhase).Review;
            Assert.AreNotEqual(CaseGrade.S, review.Grade, "신뢰도를 잃었으면 S가 아니다");
            Assert.IsTrue(review.Clues.Any(c => !c.Obtained), "놓친 단서가 복기에 나와야 한다");
            Assert.IsTrue(review.Clues.All(c => !string.IsNullOrEmpty(c.Where) && c.Where != "?"));
        }

        [Test]
        public void Events_FireOnClueTrustPhaseAndLog()
        {
            var session = new CaseSession(CaseGenerator.Generate(TestPack.Create(), 5));
            var clues = new List<CaseClue>();
            var trust = new List<int>();
            var phases = new List<PhaseKind>();
            int logs = 0;
            session.OnClueGained += clues.Add;
            session.OnTrustChanged += trust.Add;
            session.OnPhaseChanged += phases.Add;
            session.OnLog += _ => logs++;

            Play(session, null, SolvingPlayer);

            Assert.AreEqual(session.Clues.Count, clues.Count);
            Assert.IsEmpty(trust, "정답 경로에서는 신뢰도가 변하지 않는다");
            CollectionAssert.AreEqual(
                new[] { PhaseKind.Investigate, PhaseKind.Testimony, PhaseKind.Accuse, PhaseKind.Result, PhaseKind.None }, phases);
            Assert.AreEqual(session.Log.Count, logs);
            Assert.Greater(logs, 5);
        }
    }
}
