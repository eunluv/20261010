using System;
using System.Collections.Generic;
using System.Diagnostics;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 규칙 하나가 대량 테스트에서 쓰인 횟수.
    public sealed class RuleUsage
    {
        public string RuleId { get; }
        public int RealUses { get; }     // 진짜 단서로 쓰인 횟수
        public int HerringUses { get; }  // 가짜 단서로 쓰인 횟수

        public RuleUsage(string ruleId, int realUses, int herringUses)
        {
            RuleId = ruleId;
            RealUses = realUses;
            HerringUses = herringUses;
        }
    }

    // 시드 여러 개를 돌린 결과 요약 (tool-design 8-3).
    public sealed class CaseBatchReport
    {
        public string TemplateId { get; internal set; }
        public int StartSeed { get; internal set; }
        public int Count { get; internal set; }
        public Difficulty Difficulty { get; internal set; }

        public int Succeeded { get; internal set; }
        // 생성은 됐지만 CaseVerifier의 조건(정답 유일, 단서·타임라인·증언·증거·배치)을 어긴 사건. 0이어야 한다.
        public int Unsolvable { get; internal set; }

        public double SuccessRate => Count == 0 ? 0 : (double)Succeeded / Count;

        // 아래 평균은 성공한 사건 기준. 단서 수는 가짜 단서를 뺀 수.
        public double AverageAttempts { get; internal set; }
        public double AverageClues { get; internal set; }
        public int MaxClues { get; internal set; }

        // 해결에 꼭 필요한 조사 행동 수 (성공한 사건 기준).
        public double AverageRequiredActions { get; internal set; }
        public int MaxRequiredActions { get; internal set; }

        // Unsolvable로 센 사건에서 처음 발견된 위반 내용 (처음 몇 개만).
        public IReadOnlyList<string> Violations { get; internal set; }

        // 생성 시간은 실패한 시드까지 포함한 전체 기준.
        public double AverageMs { get; internal set; }
        public double MaxMs { get; internal set; }

        public IReadOnlyList<RuleUsage> RuleUsages { get; internal set; }  // 규칙 id 순, 0회인 규칙 포함
        public IReadOnlyList<int> FailedSeeds { get; internal set; }       // 처음 몇 개만
        public IReadOnlyList<int> UnsolvableSeeds { get; internal set; }   // 처음 몇 개만
    }

    public static class CaseBatch
    {
        const int MaxListedSeeds = 20;

        public static CaseBatchReport Run(CaseTemplateData template, int startSeed, int count, Difficulty? difficulty = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            // 규칙별 집계 (조회 전용 사전, 출력은 template.Rules 순서로).
            var real = new Dictionary<string, int>(StringComparer.Ordinal);
            var herring = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var rule in template.Rules)
            {
                real[rule.Id] = 0;
                herring[rule.Id] = 0;
            }

            var failed = new List<int>();
            var unsolvable = new List<int>();
            var violations = new List<string>();
            int succeeded = 0, unsolvableCount = 0, maxClues = 0, maxRequired = 0;
            long attempts = 0, clues = 0, required = 0;
            double totalMs = 0, maxMs = 0;
            var watch = new Stopwatch();

            for (int i = 0; i < count; i++)
            {
                int seed = unchecked(startSeed + i);

                watch.Restart();
                var instance = CaseGenerator.Generate(template, seed, difficulty);
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                totalMs += ms;
                if (ms > maxMs) maxMs = ms;

                if (instance == null)
                {
                    if (failed.Count < MaxListedSeeds) failed.Add(seed);
                    continue;
                }

                succeeded++;
                attempts += instance.Attempts;

                int realCount = 0;
                foreach (var clue in instance.Clues)
                {
                    if (clue.IsRedHerring)
                    {
                        if (herring.ContainsKey(clue.RuleId)) herring[clue.RuleId]++;
                    }
                    else
                    {
                        realCount++;
                        if (real.ContainsKey(clue.RuleId)) real[clue.RuleId]++;
                    }
                }
                clues += realCount;
                if (realCount > maxClues) maxClues = realCount;

                var found = CaseVerifier.FindViolations(instance);
                if (found.Count > 0)
                {
                    unsolvableCount++;
                    if (unsolvable.Count < MaxListedSeeds)
                    {
                        unsolvable.Add(seed);
                        violations.Add($"seed {seed}: {found[0]}");
                    }
                }
                required += instance.RequiredActions;
                if (instance.RequiredActions > maxRequired) maxRequired = instance.RequiredActions;
            }

            var usages = new List<RuleUsage>();
            foreach (var rule in template.Rules) usages.Add(new RuleUsage(rule.Id, real[rule.Id], herring[rule.Id]));

            return new CaseBatchReport
            {
                TemplateId = template.Id,
                StartSeed = startSeed,
                Count = count,
                Difficulty = difficulty ?? template.Difficulty,
                Succeeded = succeeded,
                Unsolvable = unsolvableCount,
                AverageAttempts = succeeded == 0 ? 0 : (double)attempts / succeeded,
                AverageClues = succeeded == 0 ? 0 : (double)clues / succeeded,
                MaxClues = maxClues,
                AverageRequiredActions = succeeded == 0 ? 0 : (double)required / succeeded,
                MaxRequiredActions = maxRequired,
                Violations = violations,
                AverageMs = count == 0 ? 0 : totalMs / count,
                MaxMs = maxMs,
                RuleUsages = usages,
                FailedSeeds = failed,
                UnsolvableSeeds = unsolvable,
            };
        }

        // 모든 단서가 정답에 대해 참이고, 진짜 단서만으로 남는 후보가 정확히 그 정답 하나인가.
        public static bool IsSolvable(CaseInstance instance)
        {
            if (instance == null) return false;

            var truth = instance.GetTruth();
            var solver = new Solver(instance.Space);
            var alive = solver.AllAlive();
            foreach (var clue in instance.Clues)
            {
                if (!clue.Constraint.Holds(truth)) return false;
                if (!clue.IsRedHerring) alive.And(solver.MaskOf(clue.Constraint));
            }

            var remaining = solver.AliveCombos(alive);
            if (remaining.Count != 1) return false;
            for (int a = 0; a < truth.Length; a++)
                if (remaining[0][a] != truth[a]) return false;
            return true;
        }
    }
}
