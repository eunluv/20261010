using System.Collections;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 생성된 사건이 지켜야 할 조건을 전부 다시 확인한다. 대량 테스트와 자동 테스트가 함께 쓴다.
    // 돌려주는 목록이 비어 있으면 문제 없음.
    public static class CaseVerifier
    {
        public static List<string> FindViolations(CaseInstance c)
        {
            var v = new List<string>();
            if (c == null) { v.Add("사건이 null입니다."); return v; }

            var truth = c.GetTruth();
            var solver = new Solver(c.Space);
            CheckClues(c, truth, solver, v);
            CheckPlacements(c, v);
            if (c.Timeline != null)
            {
                CheckTimeline(c, truth, v);
                CheckTestimonies(c, truth, v);
                CheckRewards(c, solver, v);
            }
            return v;
        }

        // 모든 단서가 정답에 대해 참이고, 진짜 단서만으로 남는 후보가 정확히 그 정답 하나다.
        static void CheckClues(CaseInstance c, int[] truth, Solver solver, List<string> v)
        {
            var alive = solver.AllAlive();
            var essentialAlive = solver.AllAlive();
            foreach (var clue in c.Clues)
            {
                if (!clue.Constraint.Holds(truth)) v.Add($"정답에 대해 거짓인 단서: {clue.DebugText}");
                if (clue.IsRedHerring)
                {
                    if (clue.IsEssential || clue.IsRebuttalReward) v.Add($"가짜 단서가 필수/보상으로 표시됨: {clue.DebugText}");
                    continue;
                }
                var mask = solver.MaskOf(clue.Constraint);
                alive.And(mask);
                if (clue.IsEssential) essentialAlive.And(mask);
            }

            var remaining = solver.AliveCombos(alive);
            if (remaining.Count != 1) { v.Add($"진짜 단서를 모두 적용한 뒤 남은 후보가 {remaining.Count}개입니다."); return; }
            for (int a = 0; a < truth.Length; a++)
                if (remaining[0][a] != truth[a]) { v.Add("단서로 좁힌 답이 사건의 정답과 다릅니다."); break; }

            if (solver.CountAlive(essentialAlive) != 1) v.Add("필수 단서만으로는 정답이 하나로 좁혀지지 않습니다.");
        }

        // 모든 단서와 증거가 정확히 한 곳에 놓여 있고, 필요한 행동 수가 행동력 안에 들어온다.
        static void CheckPlacements(CaseInstance c, List<string> v)
        {
            var clueCount = new int[c.Clues.Count];
            var evidenceCount = new int[c.Evidence.Count];

            foreach (var p in c.Placements)
            {
                var counts = p.IsEvidence ? evidenceCount : clueCount;
                if (p.Index < 0 || p.Index >= counts.Length) { v.Add($"배치가 없는 항목을 가리킵니다: {p}"); continue; }
                counts[p.Index]++;

                if (p.Via == AcquireVia.RebuttalReward)
                {
                    if (p.IsEvidence) v.Add($"증거가 반박 보상으로 배치됨: {p}");
                    else if (c.Clues[p.Index].RewardLineId != p.LineId) v.Add($"보상 단서의 배치 줄이 다릅니다: {p}");
                    continue;
                }
                if (!p.IsEvidence && c.Clues[p.Index].IsRebuttalReward) v.Add($"보상 단서가 조사로 얻게 배치됨: {p}");
                if (c.Timeline == null) continue;

                int limit = p.Via == AcquireVia.Investigate ? c.Timeline.PlaceCount : c.Timeline.SuspectCount;
                if (p.Target < 0 || p.Target >= limit) v.Add($"배치 대상이 범위 밖입니다: {p}");
            }

            for (int i = 0; i < clueCount.Length; i++)
                if (clueCount[i] != 1) v.Add($"단서 {i}의 배치가 {clueCount[i]}개입니다.");
            for (int i = 0; i < evidenceCount.Length; i++)
                if (evidenceCount[i] != 1) v.Add($"증거 {i}의 배치가 {evidenceCount[i]}개입니다.");

            int required = 0;
            bool anyReward = false;
            foreach (var clue in c.Clues)
            {
                if (clue.IsRebuttalReward) anyReward = true;
                else if (clue.IsEssential) required++;
            }
            if (anyReward) required++;

            if (required != c.RequiredActions) v.Add($"필요 행동 수가 {c.RequiredActions}로 적혀 있지만 계산하면 {required}입니다.");
            int budget = c.Template.ActionPoints - c.Template.ActionMargin;
            if (required > budget) v.Add($"필요 행동 수 {required}가 행동력 - 여유값({budget})을 넘습니다.");
        }

        // 타임라인의 모든 사실이 정답과 모순되지 않는다.
        static void CheckTimeline(CaseInstance c, int[] truth, List<string> v)
        {
            var t = c.Timeline;
            int culprit = truth[t.SuspectAxis];
            int crimePlace = truth[t.PlaceAxis];

            for (int s = 0; s < t.SuspectCount; s++)
            {
                bool atScene = t.LocationOf(s, t.CrimeSlot) == crimePlace;
                if (s == culprit && !atScene) v.Add("범인이 사건 시간대에 사건 장소에 없습니다.");
                if (s != culprit && atScene) v.Add($"범인이 아닌 용의자 {s}가 사건 시간대에 사건 장소에 있습니다.");
            }

            int handled = 0;
            foreach (var fact in t.Facts)
            {
                if (!t.IsTrue(fact)) v.Add($"타임라인 안에서 앞뒤가 안 맞는 사실: {fact}");
                if (fact.Kind != FactKind.Handled) continue;
                handled++;
                if (fact.Who != culprit || fact.Where != crimePlace || fact.When != t.CrimeSlot ||
                    (t.ItemAxis >= 0 && fact.Target != truth[t.ItemAxis]))
                    v.Add($"정답과 다른 Handled 사실: {fact}");
            }
            if (t.ItemAxis >= 0 && handled != 1) v.Add($"Handled 사실이 {handled}개입니다.");

            // 모든 논리 단서의 문장이 참인 사실에서 나온다.
            foreach (var clue in c.Clues)
            {
                if (clue.SourceFact.HasValue && !t.IsTrue(clue.SourceFact.Value))
                    v.Add($"단서 문장의 근거 사실이 거짓입니다: {clue.DebugText} ← {clue.SourceFact.Value}");
                if (clue.Constraint is IsNot isNot && isNot.Axis == t.SuspectAxis)
                {
                    if (!clue.SourceFact.HasValue) v.Add($"알리바이 단서에 근거 사실이 없습니다: {clue.DebugText}");
                    else if (clue.SourceFact.Value.Who != isNot.Option || clue.SourceFact.Value.When != t.CrimeSlot)
                        v.Add($"알리바이 단서의 근거 사실이 다른 사람·시간의 것입니다: {clue.DebugText}");
                }
            }
        }

        // 거짓 줄은 실제로 거짓이고 증거가 정확히 하나 있다. 나머지 줄은 참이다. 범인은 사건 시간대 위치를 속인다.
        static void CheckTestimonies(CaseInstance c, int[] truth, List<string> v)
        {
            var t = c.Timeline;
            int culprit = truth[t.SuspectAxis];
            if (c.Testimonies.Count != t.SuspectCount) v.Add($"증언 수 {c.Testimonies.Count}가 용의자 수 {t.SuspectCount}와 다릅니다.");

            int liars = 0;
            foreach (var testimony in c.Testimonies)
            {
                if (testimony.HasLie) liars++;
                if (testimony.Lines.Count < 3 || testimony.Lines.Count > 5)
                    v.Add($"용의자 {testimony.Suspect}의 증언이 {testimony.Lines.Count}줄입니다 (3~5줄이어야 함).");
                if (testimony.IsCulprit != (testimony.Suspect == culprit)) v.Add($"용의자 {testimony.Suspect}의 범인 표시가 틀렸습니다.");

                bool liedAboutCrimeTime = false;
                foreach (var line in testimony.Lines)
                {
                    bool stated = t.IsTrue(line.Stated);
                    if (!t.IsTrue(line.Original)) v.Add($"증언 줄의 원래 사실이 거짓입니다: {line}");

                    if (line.Kind == TestimonyLineKind.Lie)
                    {
                        if (stated) v.Add($"거짓 줄인데 내용이 참입니다: {line}");
                        if (line.Original.Kind == FactKind.Presence && line.Original.When == t.CrimeSlot) liedAboutCrimeTime = true;
                        CheckEvidenceFor(c, line, v);
                    }
                    else
                    {
                        if (!stated) v.Add($"거짓 줄이 아닌데 내용이 거짓입니다: {line}");
                        if (line.Stated != line.Original) v.Add($"거짓 줄이 아닌데 말한 내용이 원래 사실과 다릅니다: {line}");
                    }
                }
                if (testimony.IsCulprit && !liedAboutCrimeTime) v.Add("범인이 사건 시간대의 위치를 속이지 않았습니다.");
            }

            int allowed = c.Template.LieCount < 1 ? 1 : c.Template.LieCount;
            if (liars > allowed) v.Add($"거짓말한 용의자가 {liars}명입니다 (최대 {allowed}명).");

            foreach (var evidence in c.Evidence)
            {
                var line = c.FindLine(evidence.RebutsLineId);
                if (line == null || line.Kind != TestimonyLineKind.Lie) v.Add($"증거가 거짓 줄이 아닌 것을 가리킵니다: {evidence}");
            }
        }

        static void CheckEvidenceFor(CaseInstance c, TestimonyLine line, List<string> v)
        {
            int count = 0;
            for (int e = 0; e < c.Evidence.Count; e++)
            {
                var evidence = c.Evidence[e];
                if (evidence.RebutsLineId != line.Id) continue;
                count++;

                if (!c.Timeline.IsTrue(evidence.Fact)) v.Add($"증거의 사실이 거짓입니다: {evidence}");
                // 증거의 사실과 거짓말이 동시에 참일 수 없어야 한다: 같은 사람·같은 시간인데 장소가 다르다.
                bool samePerson = evidence.Fact.Who == line.Stated.Who ||
                                  (line.Stated.Kind == FactKind.Saw && evidence.Fact.Who == line.Stated.Target);
                if (evidence.Fact.Kind != FactKind.Presence || !samePerson || evidence.Fact.When != line.Stated.When ||
                    evidence.Fact.Where == line.Stated.Where)
                    v.Add($"증거가 거짓 줄과 모순되지 않습니다: {evidence} vs {line}");

                bool obtainable = false;
                foreach (var p in c.Placements)
                    if (p.IsEvidence && p.Index == e && p.Via != AcquireVia.RebuttalReward && p.Target >= 0) obtainable = true;
                if (!obtainable) v.Add($"증거를 얻을 수 있는 배치가 없습니다: {evidence}");
            }
            if (count != 1) v.Add($"거짓 줄 {line.Id}의 반박 증거가 {count}개입니다.");
        }

        // 반박 보상 단서가 1~2개 있고, 모두 같은 거짓 줄에 걸려 있으며, 하나라도 빼면 후보가 2개 이상 남는다.
        static void CheckRewards(CaseInstance c, Solver solver, List<string> v)
        {
            var rewards = new List<int>();
            string lineId = null;
            for (int i = 0; i < c.Clues.Count; i++)
            {
                var clue = c.Clues[i];
                if (!clue.IsRebuttalReward) continue;
                rewards.Add(i);
                if (!clue.IsEssential) v.Add($"보상 단서가 필수 단서가 아닙니다: {clue.DebugText}");
                if (lineId == null) lineId = clue.RewardLineId;
                else if (lineId != clue.RewardLineId) v.Add("보상 단서가 서로 다른 증언 줄에 걸려 있습니다.");
            }

            if (rewards.Count < 1 || rewards.Count > 2) { v.Add($"반박 보상 단서가 {rewards.Count}개입니다 (1~2개여야 함)."); return; }

            var keyLine = c.FindLine(lineId);
            if (keyLine == null || keyLine.Kind != TestimonyLineKind.Lie) v.Add($"보상이 걸린 줄 {lineId}이 거짓 줄이 아닙니다.");

            // 가짜 단서까지 전부 가지고 있어도, 보상 단서 하나가 없으면 풀리지 않아야 한다.
            foreach (int reward in rewards)
            {
                BitArray alive = solver.AllAlive();
                for (int i = 0; i < c.Clues.Count; i++)
                    if (i != reward) alive.And(solver.MaskOf(c.Clues[i].Constraint));
                int count = solver.CountAlive(alive);
                if (count < 2) v.Add($"보상 단서 '{c.Clues[reward].DebugText}'를 빼도 후보가 {count}개만 남습니다.");
            }
        }
    }
}
