using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A와 축 B의 정답이 지정 카테고리의 태그를 하나 이상 공유한다.
    public sealed class SameTag : IConstraint
    {
        readonly bool[][] shares; // [A 옵션][B 옵션] 미리 계산

        public int AxisA { get; }
        public int AxisB { get; }
        public string CategoryId { get; }
        public string DebugText { get; }

        public SameTag(CaseSpace space, int axisA, int axisB, string categoryId)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckAxis(axisA, nameof(axisA));
            space.CheckAxis(axisB, nameof(axisB));
            if (axisA == axisB) throw new ArgumentException("SameTag의 두 축은 서로 달라야 합니다.", nameof(axisB));
            space.RequireCategory(categoryId, nameof(categoryId));

            AxisA = axisA;
            AxisB = axisB;
            CategoryId = categoryId;

            int countA = space.OptionCount(axisA);
            int countB = space.OptionCount(axisB);
            shares = new bool[countA][];
            for (int x = 0; x < countA; x++)
            {
                shares[x] = new bool[countB];
                for (int y = 0; y < countB; y++) shares[x][y] = space.ShareCategoryTag(axisA, x, axisB, y, categoryId);
            }
            DebugText = $"{space.AxisId(axisA)}·{space.AxisId(axisB)} {categoryId} 태그 공유";
        }

        public bool Holds(int[] picks) => shares[picks[AxisA]][picks[AxisB]];

        public override string ToString() => DebugText;
    }
}
