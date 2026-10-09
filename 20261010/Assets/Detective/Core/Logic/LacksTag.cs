using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A의 정답은 태그 T가 없다.
    public sealed class LacksTag : IConstraint
    {
        readonly bool[] optionLacksTag; // 옵션별 태그 미보유 여부 미리 계산

        public int Axis { get; }
        public string TagId { get; }
        public string DebugText { get; }

        public LacksTag(CaseSpace space, int axis, string tagId)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckAxis(axis, nameof(axis));
            space.RequireTag(tagId, nameof(tagId));

            Axis = axis;
            TagId = tagId;
            optionLacksTag = new bool[space.OptionCount(axis)];
            for (int o = 0; o < optionLacksTag.Length; o++) optionLacksTag[o] = !space.HasTag(axis, o, tagId);
            DebugText = $"{space.AxisId(axis)} 태그 {tagId} 없음";
        }

        public bool Holds(int[] picks) => optionLacksTag[picks[Axis]];

        public override string ToString() => DebugText;
    }
}
