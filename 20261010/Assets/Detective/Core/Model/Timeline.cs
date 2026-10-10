using System.Collections.Generic;

namespace Detective.Core.Model
{
    // 사건 전후 시간대별로 모든 용의자가 어디 있었는지의 기록. 단서 문장·증언·반박 증거의 원천이다.
    public sealed class Timeline
    {
        readonly int[][] location; // [용의자][시간대] → 장소 옵션 번호
        readonly List<Fact> facts = new List<Fact>();

        // 어느 축이 용의자·장소·도구인가 (도구 축이 없으면 -1).
        public int SuspectAxis { get; }
        public int PlaceAxis { get; }
        public int ItemAxis { get; }

        public int SuspectCount => location.Length;
        public int PlaceCount { get; }
        public int SlotCount { get; }
        public int CrimeSlot { get; }

        // Presence(시간대 → 용의자 순), Saw, Handled 순.
        public IReadOnlyList<Fact> Facts => facts;

        public Timeline(int suspectAxis, int placeAxis, int itemAxis, int placeCount, int crimeSlot, int[][] location,
            IEnumerable<Fact> handled)
        {
            SuspectAxis = suspectAxis;
            PlaceAxis = placeAxis;
            ItemAxis = itemAxis;
            PlaceCount = placeCount;
            CrimeSlot = crimeSlot;
            SlotCount = location.Length > 0 ? location[0].Length : 0;

            this.location = new int[location.Length][];
            for (int s = 0; s < location.Length; s++) this.location[s] = (int[])location[s].Clone();

            for (int t = 0; t < SlotCount; t++)
                for (int s = 0; s < SuspectCount; s++)
                    facts.Add(Fact.Presence(s, this.location[s][t], t));

            // 같은 시간에 같은 장소에 있던 사람끼리는 서로를 봤다.
            for (int t = 0; t < SlotCount; t++)
                for (int s = 0; s < SuspectCount; s++)
                    for (int o = 0; o < SuspectCount; o++)
                        if (s != o && this.location[s][t] == this.location[o][t])
                            facts.Add(Fact.Saw(s, this.location[s][t], t, o));

            if (handled != null) facts.AddRange(handled);
        }

        public int LocationOf(int suspect, int slot) => location[suspect][slot];

        // 이 사실이 타임라인에서 참인가.
        public bool IsTrue(Fact fact)
        {
            if (fact.Who < 0 || fact.Who >= SuspectCount || fact.When < 0 || fact.When >= SlotCount) return false;

            switch (fact.Kind)
            {
                case FactKind.Presence:
                    return location[fact.Who][fact.When] == fact.Where;
                case FactKind.Saw:
                    return fact.Target >= 0 && fact.Target < SuspectCount && fact.Target != fact.Who &&
                           location[fact.Who][fact.When] == fact.Where && location[fact.Target][fact.When] == fact.Where;
                default:
                    return facts.Contains(fact);
            }
        }
    }
}
