using System;
using System.Collections;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 정답 먼저, 단서는 나중에 (tool-design 5-3, 6장).
    // 시드 → 축 옵션 뽑기 → 정답 → 타임라인 → 규칙 확장(정답에 참인 것만) → 후보가 1개가 될 때까지 단서 선택
    // → 군더더기 제거 → 증언·증거 → 반박 보상 지정 → 가짜 단서 → 배치와 행동력 검사.
    public static class CaseGenerator
    {
        public const int MaxAttempts = 50;

        // 파생 시드(seed * 31 + 시도 번호)로 최대 50번 시도한다. 같은 seed는 항상 같은 사건을 만든다.
        // 끝까지 실패하면 null.
        public static CaseInstance Generate(CaseTemplateData template, int seed, Difficulty? difficulty = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int derived = unchecked(seed * 31 + attempt);
                var instance = Build(template, seed, derived, attempt + 1, difficulty ?? template.Difficulty);
                if (instance != null) return instance;
            }
            return null;
        }

        // 주어진 시드로 한 번만 시도한다. 정답을 하나로 좁히지 못하거나 행동력 안에 풀 수 없으면 null.
        public static CaseInstance TryGenerate(CaseTemplateData template, int seed, Difficulty? difficulty = null)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return Build(template, seed, seed, 1, difficulty ?? template.Difficulty);
        }

        static CaseInstance Build(CaseTemplateData template, int seed, int usedSeed, int attempts, Difficulty difficulty)
        {
            var rng = new Random(usedSeed);

            // 1. 축마다 후보 풀에서 옵션 뽑기
            var axes = new List<AxisDef>();
            foreach (var pool in template.Axes)
            {
                if (pool.PickCount < 1 || pool.CandidateIds.Count < pool.PickCount) return null;
                axes.Add(new AxisDef(pool.AxisId, PickOptions(pool, rng)));
            }
            if (axes.Count == 0) return null;

            var space = BuildSpace(template, axes);
            if (space == null) return null;

            // 2. 정답과 타임라인
            var truth = new int[space.AxisCount];
            for (int a = 0; a < truth.Length; a++) truth[a] = rng.Next(space.OptionCount(a));
            var timeline = TimelineBuilder.Build(template, space, truth, rng);

            // 3. 단서 후보 (정답에 참인 것만)
            var candidates = RuleExpander.Expand(template, space, truth);
            var solver = new Solver(space);
            var masks = new BitArray[candidates.Count];
            for (int i = 0; i < masks.Length; i++) masks[i] = solver.MaskOf(candidates[i].Constraint);

            // 4. 남은 후보가 1개가 될 때까지 단서 선택
            var alive = solver.AllAlive();
            int aliveCount = solver.ComboCount;
            var chosen = new List<int>();
            var picked = new bool[candidates.Count];
            var usefulIndex = new List<int>();
            var usefulEliminated = new List<int>();

            while (aliveCount > 1)
            {
                usefulIndex.Clear();
                usefulEliminated.Clear();
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (picked[i]) continue;
                    int eliminated = solver.CountEliminated(alive, masks[i]);
                    if (eliminated == 0) continue;
                    usefulIndex.Add(i);
                    usefulEliminated.Add(eliminated);
                }
                if (usefulIndex.Count == 0) return null; // 막힘 → 호출부에서 다음 시드

                int k = PickByDifficulty(candidates, usefulIndex, usefulEliminated, aliveCount, difficulty, rng);
                int pick = usefulIndex[k];
                picked[pick] = true;
                chosen.Add(pick);
                alive.And(masks[pick]);
                aliveCount -= usefulEliminated[k];
            }

            // 5. 꼭 필요한 최소 묶음. Normal/Hard는 이것만 남기고, Easy는 겹치는 단서도 남겨 둔다.
            var essential = new List<int>(chosen);
            TrimRedundant(essential, masks, solver);
            if (difficulty != Difficulty.Easy) chosen = new List<int>(essential);

            // 6. 증언과 증거
            var story = timeline != null ? TestimonyBuilder.Build(template, space, timeline, truth, rng) : null;

            // 7. 반박 보상: 필수 단서 1~2개를 거짓 줄 하나에 건다. 그 단서 없이는 정답이 하나로 좁혀지지 않아야 한다.
            var rewards = new List<int>();
            string keyLineId = null;
            if (story != null && story.Evidence.Count > 0 && essential.Count > 0)
            {
                keyLineId = story.Evidence[rng.Next(story.Evidence.Count)].RebutsLineId;

                var rewardPool = new List<int>(essential);
                int rewardCount = Math.Min(rewardPool.Count, rng.Next(1, 3));
                for (int n = 0; n < rewardCount; n++)
                {
                    int k = rng.Next(rewardPool.Count);
                    rewards.Add(rewardPool[k]);
                    rewardPool.RemoveAt(k);
                }

                // Easy: 보상 단서를 대신할 수 있는 겹치는 단서를 뒤에서부터 뺀다.
                // (essential만 남으면 최소 묶음이므로 조건이 반드시 성립한다.)
                foreach (int reward in rewards)
                {
                    while (solver.CountAlive(AliveWithout(chosen, reward, masks, solver)) == 1)
                    {
                        int removable = -1;
                        for (int i = chosen.Count - 1; i >= 0; i--)
                            if (!essential.Contains(chosen[i])) { removable = i; break; }
                        if (removable < 0) break;
                        chosen.RemoveAt(removable);
                    }
                }
            }

            // 8. 행동력 검사 (tool-design 6-5): 필수 단서 중 보상이 아닌 것 + 반박에 쓸 증거 1개
            int requiredActions = rewards.Count > 0 ? 1 : 0;
            foreach (int i in essential)
                if (!rewards.Contains(i)) requiredActions++;
            if (requiredActions > template.ActionPoints - template.ActionMargin) return null;

            // 9. 가짜 단서: 참이지만 고르지 않은 단서. 보상 단서를 대신할 수 있는 것은 쓰지 않는다.
            var herrings = PickRedHerrings(candidates, chosen, rewards, masks, solver, template.RedHerringCount, rng);

            // 10. 단서 목록
            var clues = new List<CaseClue>();
            alive = solver.AllAlive();
            foreach (int i in chosen)
            {
                int eliminated = solver.CountEliminated(alive, masks[i]);
                alive.And(masks[i]);
                clues.Add(MakeClue(template, space, solver, timeline, candidates[i], masks[i], false, eliminated,
                    essential.Contains(i), rewards.Contains(i) ? keyLineId : null, rng));
            }
            foreach (int i in herrings)
                clues.Add(MakeClue(template, space, solver, timeline, candidates[i], masks[i], true, 0, false, null, rng));

            // 11. 배치
            var placements = PlaceItems(clues, story, timeline, space, rng);

            return new CaseInstance(template, seed, usedSeed, attempts, difficulty, space, truth, clues,
                timeline,
                story != null ? story.Testimonies : null,
                story != null ? story.Evidence : null,
                placements, requiredActions);
        }

        // 후보 풀에서 PickCount개를 고르고 id 순으로 정렬한다.
        static List<string> PickOptions(AxisPoolData pool, Random rng)
        {
            var ids = new List<string>(pool.CandidateIds);
            for (int i = 0; i < pool.PickCount; i++)
            {
                int j = rng.Next(i, ids.Count);
                string tmp = ids[i];
                ids[i] = ids[j];
                ids[j] = tmp;
            }
            ids.RemoveRange(pool.PickCount, ids.Count - pool.PickCount);
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        static CaseSpace BuildSpace(CaseTemplateData template, List<AxisDef> axes)
        {
            var catalog = new TagCatalog();
            foreach (var tag in template.Tags) catalog.Add(tag.Id, tag.Category);

            var tags = new OptionTagTable();
            foreach (var axis in axes)
            {
                foreach (var optionId in axis.OptionIds)
                {
                    var entity = template.FindEntity(optionId);
                    if (entity == null) return null;
                    foreach (var tagId in entity.TagIds) tags.Add(axis.Id, optionId, tagId);
                }
            }
            return new CaseSpace(axes, catalog, tags);
        }

        // 난이도별 가중치로 쓸모 있는 단서 하나를 고른다. 돌려주는 값은 useful 목록 안의 위치.
        //   Easy : 후보를 많이 지우는 단서와 IsNot을 선호
        //   Hard : 조금씩 지우는 단서와 Implies·SameTag를 선호
        // 규칙의 기본 가중치와 Easy/Hard 배율(ClueRuleData.WeightFor)이 곱해진다.
        static int PickByDifficulty(List<ClueCandidate> candidates, List<int> usefulIndex, List<int> usefulEliminated,
            int aliveCount, Difficulty difficulty, Random rng)
        {
            var weights = new double[usefulIndex.Count];
            double total = 0;
            for (int k = 0; k < weights.Length; k++)
            {
                var rule = candidates[usefulIndex[k]].Rule;
                double fraction = (double)usefulEliminated[k] / aliveCount; // 0 초과 1 미만
                double weight = Math.Max(0f, rule.WeightFor(difficulty));

                if (difficulty == Difficulty.Easy)
                {
                    weight *= 0.2 + 2.0 * fraction;
                    if (rule.Type == ConstraintType.IsNot) weight *= 2.0;
                }
                else if (difficulty == Difficulty.Hard)
                {
                    weight *= 0.2 + 2.0 * (1.0 - fraction);
                    if (rule.Type == ConstraintType.Implies || rule.Type == ConstraintType.SameTag) weight *= 2.0;
                }

                weights[k] = weight;
                total += weight;
            }

            // 가중치가 전부 0이면 고르게 뽑는다.
            if (total <= 0) return rng.Next(weights.Length);

            double roll = rng.NextDouble() * total;
            for (int k = 0; k < weights.Length; k++)
            {
                roll -= weights[k];
                if (roll < 0) return k;
            }
            return weights.Length - 1;
        }

        // 단서를 하나씩 빼 보고, 빼도 정답이 하나로 정해지면 제거한다.
        static void TrimRedundant(List<int> chosen, BitArray[] masks, Solver solver)
        {
            int i = 0;
            while (i < chosen.Count)
            {
                if (solver.CountAlive(AliveWithout(chosen, chosen[i], masks, solver)) == 1) chosen.RemoveAt(i);
                else i++;
            }
        }

        // 단서 하나(without)를 빼고 나머지를 모두 적용했을 때 살아남는 후보.
        static BitArray AliveWithout(List<int> chosen, int without, BitArray[] masks, Solver solver)
        {
            var alive = solver.AllAlive();
            foreach (int i in chosen)
                if (i != without) alive.And(masks[i]);
            return alive;
        }

        static List<int> PickRedHerrings(List<ClueCandidate> candidates, List<int> chosen, List<int> rewards, BitArray[] masks,
            Solver solver, int count, Random rng)
        {
            var result = new List<int>();
            if (count <= 0) return result;

            // 이미 고른 단서와 내용이 같은 것은 가짜 단서로 쓰지 않는다 (조회 전용).
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (int i in chosen) taken.Add(candidates[i].Constraint.DebugText);

            // 혼자서는 아무 후보도 못 지우는 단서(하나 마나 한 말)는 뺀다.
            var pool = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                if (!chosen.Contains(i) && solver.CountAlive(masks[i]) < solver.ComboCount) pool.Add(i);

            // 보상 단서마다 "그 단서 없이 남는 후보". 가짜 단서를 더해도 2개 이상 남아야 반박을 건너뛸 수 없다.
            var withoutReward = new List<BitArray>();
            foreach (int reward in rewards) withoutReward.Add(AliveWithout(chosen, reward, masks, solver));

            while (result.Count < count && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                int pick = pool[k];
                pool.RemoveAt(k);
                if (taken.Contains(candidates[pick].Constraint.DebugText)) continue;

                bool replacesReward = false;
                foreach (var alive in withoutReward)
                    if (solver.CountAlive(new BitArray(alive).And(masks[pick])) < 2) { replacesReward = true; break; }
                if (replacesReward) continue;

                foreach (var alive in withoutReward) alive.And(masks[pick]);
                taken.Add(candidates[pick].Constraint.DebugText);
                result.Add(pick);
            }
            return result;
        }

        static CaseClue MakeClue(CaseTemplateData template, CaseSpace space, Solver solver, Timeline timeline,
            ClueCandidate candidate, BitArray mask, bool isRedHerring, int eliminated, bool isEssential, string rewardLineId,
            Random rng)
        {
            var variants = candidate.Rule.TextVariants;
            int variantIndex = variants.Count > 0 ? rng.Next(variants.Count) : -1;
            string text = variantIndex >= 0 ? ClueTextRenderer.Render(variants[variantIndex], template, space, candidate, timeline) : "";
            int alone = solver.ComboCount - solver.CountAlive(mask);

            return new CaseClue(candidate.Rule.Id, candidate.Constraint, text, variantIndex, isRedHerring,
                eliminated, alone, candidate.Rule.Sources,
                isEssential, rewardLineId, ClueTextRenderer.AlibiFact(candidate, timeline));
        }

        // 단서와 증거를 획득 경로에 놓는다 (tool-design 6-5).
        //   단서: 규칙의 획득 경로 중 하나. 조사면 장소 하나, 탐문·잡담이면 용의자 한 명.
        //   반박 보상 단서: 해당 증언 줄.
        //   증거: 장소 조사 또는 거짓말한 본인이 아닌 용의자 탐문.
        static List<CasePlacement> PlaceItems(List<CaseClue> clues, TestimonyResult story, Timeline timeline, CaseSpace space, Random rng)
        {
            var result = new List<CasePlacement>();
            int placeCount = timeline != null ? timeline.PlaceCount : 0;
            int suspectCount = timeline != null ? timeline.SuspectCount : 0;
            var vias = new List<AcquireVia>();

            for (int i = 0; i < clues.Count; i++)
            {
                var clue = clues[i];
                if (clue.IsRebuttalReward)
                {
                    result.Add(new CasePlacement(false, i, AcquireVia.RebuttalReward, -1, clue.RewardLineId));
                    continue;
                }

                vias.Clear();
                if ((clue.Sources & ClueSource.Investigate) != 0) vias.Add(AcquireVia.Investigate);
                if ((clue.Sources & ClueSource.Interview) != 0) vias.Add(AcquireVia.Interview);
                if ((clue.Sources & ClueSource.SmallTalk) != 0) vias.Add(AcquireVia.SmallTalk);
                if (vias.Count == 0) vias.Add(AcquireVia.Investigate);

                var via = vias[rng.Next(vias.Count)];
                int targets = via == AcquireVia.Investigate ? placeCount : suspectCount;
                result.Add(new CasePlacement(false, i, via, targets > 0 ? rng.Next(targets) : -1, null));
            }

            if (story == null) return result;

            for (int e = 0; e < story.Evidence.Count; e++)
            {
                int liar = OwnerOf(story, story.Evidence[e].RebutsLineId);
                if (suspectCount > 1 && rng.Next(2) == 0)
                {
                    int witness = rng.Next(suspectCount - 1);
                    if (witness >= liar) witness++;
                    result.Add(new CasePlacement(true, e, AcquireVia.Interview, witness, null));
                }
                else
                {
                    result.Add(new CasePlacement(true, e, AcquireVia.Investigate, rng.Next(placeCount), null));
                }
            }
            return result;
        }

        static int OwnerOf(TestimonyResult story, string lineId)
        {
            foreach (var testimony in story.Testimonies)
                foreach (var line in testimony.Lines)
                    if (line.Id == lineId) return testimony.Suspect;
            return -1;
        }
    }
}
