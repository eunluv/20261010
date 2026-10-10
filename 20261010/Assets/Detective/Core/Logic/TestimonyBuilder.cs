using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    public sealed class TestimonyResult
    {
        public List<Testimony> Testimonies { get; } = new List<Testimony>(); // 용의자 옵션 번호 순
        public List<Evidence> Evidence { get; } = new List<Evidence>();       // 거짓 줄 순
    }

    // 타임라인에서 용의자별 증언과, 거짓 줄을 깨는 증거를 만든다 (tool-design 6-3, 6-4).
    //
    //   - 증언은 자기 관련 사실 3~5줄. 사건 시간대에 어디 있었는지는 항상 들어간다.
    //   - 범인은 성향과 상관없이 사건 시간대의 위치를 거짓으로 말한다.
    //   - 그 밖에 LieCount - 1명까지, 거짓말 성향이 Liar인 용의자가 한 줄을 바꿔 말한다 (장소·시간·본 사람 중 하나).
    //   - Omitter는 한 줄을 숨기고(추궁 시 공개), Exaggerator는 한 줄을 과장한다. 둘 다 내용은 참이다.
    //   - 거짓 줄마다, 그 말과 모순되는 참인 사실을 담은 증거가 정확히 하나 생긴다.
    public static class TestimonyBuilder
    {
        const int MinLines = 3, MaxLines = 5;

        public static TestimonyResult Build(CaseTemplateData template, CaseSpace space, Timeline timeline, int[] truth, Random rng)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var result = new TestimonyResult();
            int suspectCount = timeline.SuspectCount;
            int culprit = truth[timeline.SuspectAxis];
            int crimePlace = truth[timeline.PlaceAxis];
            int crimeSlot = timeline.CrimeSlot;

            var styles = new LieStyle[suspectCount];
            for (int s = 0; s < suspectCount; s++)
            {
                var profile = template.FindSuspect(space.OptionId(timeline.SuspectAxis, s));
                styles[s] = profile != null ? profile.LieStyle : LieStyle.Honest;
            }

            // 거짓말할 사람: 범인 + Liar 성향 중 LieCount - 1명
            var lying = new bool[suspectCount];
            lying[culprit] = true;
            var liarPool = new List<int>();
            for (int s = 0; s < suspectCount; s++)
                if (s != culprit && styles[s] == LieStyle.Liar) liarPool.Add(s);
            for (int extra = Math.Max(0, template.LieCount - 1); extra > 0 && liarPool.Count > 0; extra--)
            {
                int k = rng.Next(liarPool.Count);
                lying[liarPool[k]] = true;
                liarPool.RemoveAt(k);
            }

            for (int s = 0; s < suspectCount; s++)
            {
                // 1. 말할 사실 고르기
                var crimeFact = Fact.Presence(s, timeline.LocationOf(s, crimeSlot), crimeSlot);
                var others = new List<Fact>();
                foreach (var fact in timeline.Facts)
                {
                    if (fact.Who != s || fact.Kind == FactKind.Handled) continue;
                    if (fact.Kind == FactKind.Presence && fact.When == crimeSlot) continue;
                    others.Add(fact);
                }

                int want = Math.Min(others.Count, rng.Next(MinLines, MaxLines + 1) - 1);
                for (int i = 0; i < want; i++)
                {
                    int j = rng.Next(i, others.Count);
                    var tmp = others[i];
                    others[i] = others[j];
                    others[j] = tmp;
                }

                var original = new List<Fact> { crimeFact };
                for (int i = 0; i < want; i++) original.Add(others[i]);
                original.Sort(CompareFacts);

                var stated = new List<Fact>(original);
                var kinds = new TestimonyLineKind[original.Count];
                int crimeLine = original.IndexOf(crimeFact);

                // 2. 거짓말
                if (s == culprit)
                {
                    // 사건 장소가 아닌 곳에 있었다고 말한다.
                    int place = rng.Next(timeline.PlaceCount - 1);
                    if (place >= crimePlace) place++;
                    stated[crimeLine] = crimeFact.WithWhere(place);
                    kinds[crimeLine] = TestimonyLineKind.Lie;
                }
                else if (lying[s])
                {
                    int start = rng.Next(original.Count);
                    for (int step = 0; step < original.Count; step++)
                    {
                        int line = (start + step) % original.Count;
                        var altered = Alter(original, line, timeline, crimePlace, rng);
                        if (!altered.HasValue) continue;
                        stated[line] = altered.Value;
                        kinds[line] = TestimonyLineKind.Lie;
                        break;
                    }
                }

                // 3. 성향: 숨기기 / 과장하기 (거짓말이 아닌 줄 하나)
                if (styles[s] == LieStyle.Omitter) MarkOnePlainLine(kinds, TestimonyLineKind.Hidden, rng);
                else if (styles[s] == LieStyle.Exaggerator) MarkOnePlainLine(kinds, TestimonyLineKind.Exaggerated, rng);

                // 4. 문장과 증거
                string suspectId = space.OptionId(timeline.SuspectAxis, s);
                var lines = new List<TestimonyLine>();
                for (int i = 0; i < original.Count; i++)
                {
                    string lineId = suspectId + ":" + i;
                    string text = Pick(template.TestimonyText != null ? template.TestimonyText.TestimonyLinesFor(stated[i].Kind) : null, rng);
                    text = FactTextRenderer.Testimony(text, template, space, timeline, stated[i]);
                    if (kinds[i] == TestimonyLineKind.Exaggerated && template.TestimonyText != null)
                        text = Pick(template.TestimonyText.ExaggerationPrefixes, rng) + text;

                    lines.Add(new TestimonyLine(lineId, kinds[i], stated[i], original[i], text));

                    if (kinds[i] == TestimonyLineKind.Lie)
                        result.Evidence.Add(MakeEvidence(template, space, timeline, lineId, stated[i], s == culprit && i == crimeLine, rng));
                }

                result.Testimonies.Add(new Testimony(s, styles[s], s == culprit, lines));
            }

            return result;
        }

        static int CompareFacts(Fact a, Fact b)
        {
            if (a.When != b.When) return a.When.CompareTo(b.When);
            if (a.Kind != b.Kind) return ((int)a.Kind).CompareTo((int)b.Kind);
            return a.Target.CompareTo(b.Target);
        }

        // 한 줄을 장소·시간·본 사람 중 하나만 바꿔 거짓으로 만든다. 바꿀 방법이 없으면 null.
        static Fact? Alter(List<Fact> lines, int index, Timeline timeline, int crimePlace, Random rng)
        {
            var fact = lines[index];
            var groups = new List<List<Fact>>();

            // 장소 바꾸기. 사건 시간대에 사건 장소에 있었다고는 말하지 않는다 (스스로 범인이라고 하는 꼴).
            var byPlace = new List<Fact>();
            for (int p = 0; p < timeline.PlaceCount; p++)
            {
                if (p == fact.Where) continue;
                if (fact.When == timeline.CrimeSlot && p == crimePlace) continue;
                var candidate = fact.WithWhere(p);
                if (!timeline.IsTrue(candidate)) byPlace.Add(candidate);
            }
            if (byPlace.Count > 0) groups.Add(byPlace);

            // 시간 바꾸기. 같은 증언의 다른 줄과 겹치는 시간대는 피한다 (앞뒤가 안 맞는 증언이 되므로).
            if (fact.Kind == FactKind.Presence)
            {
                var byTime = new List<Fact>();
                for (int t = 0; t < timeline.SlotCount; t++)
                {
                    if (t == fact.When) continue;
                    bool taken = false;
                    for (int i = 0; i < lines.Count; i++)
                        if (i != index && lines[i].Kind == FactKind.Presence && lines[i].When == t) { taken = true; break; }
                    if (taken) continue;
                    if (t == timeline.CrimeSlot && fact.Where == crimePlace) continue;

                    var candidate = fact.WithWhen(t);
                    if (!timeline.IsTrue(candidate)) byTime.Add(candidate);
                }
                if (byTime.Count > 0) groups.Add(byTime);
            }

            // 본 사람 바꾸기.
            if (fact.Kind == FactKind.Saw)
            {
                var byTarget = new List<Fact>();
                for (int o = 0; o < timeline.SuspectCount; o++)
                {
                    if (o == fact.Who || o == fact.Target) continue;
                    var candidate = fact.WithTarget(o);
                    if (!timeline.IsTrue(candidate)) byTarget.Add(candidate);
                }
                if (byTarget.Count > 0) groups.Add(byTarget);
            }

            if (groups.Count == 0) return null;
            var group = groups[rng.Next(groups.Count)];
            return group[rng.Next(group.Count)];
        }

        static void MarkOnePlainLine(TestimonyLineKind[] kinds, TestimonyLineKind mark, Random rng)
        {
            var plain = new List<int>();
            for (int i = 0; i < kinds.Length; i++)
                if (kinds[i] == TestimonyLineKind.Plain) plain.Add(i);
            if (plain.Count > 0) kinds[plain[rng.Next(plain.Count)]] = mark;
        }

        // 거짓 줄과 모순되는 참인 사실: 말한 사람(또는 봤다고 한 사람)이 그 시간에 실제로 있던 곳.
        public static Fact ContradictingFact(Timeline timeline, Fact stated)
        {
            int who = stated.Who;
            if (stated.Kind == FactKind.Saw && timeline.LocationOf(stated.Who, stated.When) == stated.Where) who = stated.Target;
            return Fact.Presence(who, timeline.LocationOf(who, stated.When), stated.When);
        }

        static Evidence MakeEvidence(CaseTemplateData template, CaseSpace space, Timeline timeline, string lineId, Fact stated,
            bool isCulpritCrimeLine, Random rng)
        {
            var fact = ContradictingFact(timeline, stated);
            var texts = template.TestimonyText;
            string text;

            if (isCulpritCrimeLine)
            {
                // 범인이 실제로 있던 곳(사건 장소)은 밝히지 않고, 말한 곳에 없었다는 것만 보여 준다.
                var claimed = Fact.Presence(stated.Who, stated.Where, stated.When);
                text = FactTextRenderer.Evidence(Pick(texts != null ? texts.EvidenceAbsenceLines : null, rng), template, space, timeline, claimed);
            }
            else
            {
                text = FactTextRenderer.Evidence(Pick(texts != null ? texts.EvidenceLinesFor(fact.Kind) : null, rng), template, space, timeline, fact);
            }

            return new Evidence("ev:" + lineId, fact, lineId, text, !isCulpritCrimeLine);
        }

        static string Pick(IReadOnlyList<string> variants, Random rng)
        {
            if (variants == null || variants.Count == 0) return "";
            return variants[rng.Next(variants.Count)] ?? "";
        }
    }
}
