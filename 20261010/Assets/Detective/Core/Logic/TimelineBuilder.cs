using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 정답에 맞는 타임라인을 만든다 (tool-design 6-2).
    //   - 범인은 사건 시간대에 사건 장소에 있고, 사건 도구를 만졌다.
    //   - 다른 용의자는 사건 시간대에 사건 장소가 아닌 곳에 있다.
    //   - 그 밖의 시간대는 자유롭게 배치한다.
    public static class TimelineBuilder
    {
        // 용의자 축이나 장소 축이 없거나, 장소가 2곳 미만이거나, 시간대가 없으면 null (타임라인 없는 사건).
        public static Timeline Build(CaseTemplateData template, CaseSpace space, int[] truth, Random rng)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int suspectAxis = template.FirstAxisOfKind(EntityKind.Suspect);
            int placeAxis = template.FirstAxisOfKind(EntityKind.Place);
            int itemAxis = template.FirstAxisOfKind(EntityKind.Item);
            int slotCount = template.TimeSlots.Count;
            int crimeSlot = template.CrimeSlotIndex;

            if (suspectAxis < 0 || placeAxis < 0) return null;
            if (slotCount == 0 || crimeSlot < 0 || crimeSlot >= slotCount) return null;

            int suspectCount = space.OptionCount(suspectAxis);
            int placeCount = space.OptionCount(placeAxis);
            if (placeCount < 2) return null;

            int culprit = truth[suspectAxis];
            int crimePlace = truth[placeAxis];

            var location = new int[suspectCount][];
            for (int s = 0; s < suspectCount; s++) location[s] = new int[slotCount];

            for (int t = 0; t < slotCount; t++)
            {
                for (int s = 0; s < suspectCount; s++)
                {
                    if (t != crimeSlot) location[s][t] = rng.Next(placeCount);
                    else if (s == culprit) location[s][t] = crimePlace;
                    else
                    {
                        // 사건 장소를 뺀 나머지 중 하나
                        int place = rng.Next(placeCount - 1);
                        location[s][t] = place >= crimePlace ? place + 1 : place;
                    }
                }
            }

            var handled = new List<Fact>();
            if (itemAxis >= 0) handled.Add(Fact.Handled(culprit, crimePlace, crimeSlot, truth[itemAxis]));

            return new Timeline(suspectAxis, placeAxis, itemAxis, placeCount, crimeSlot, location, handled);
        }
    }
}
