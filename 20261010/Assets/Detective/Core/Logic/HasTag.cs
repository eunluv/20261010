using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A의 정답은 태그 T를 가진다.
    public sealed class HasTag : IConstraint
    {
        readonly bool[] optionHasTag; // 옵션별 태그 보유 여부 미리 계산

        public int Axis { get; }
        public string TagId { get; }
        public string DebugText { get; }

        public HasTag(CaseSpace space, int axis, string tagId)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckAxis(axis, nameof(axis));
            space.RequireTag(tagId, nameof(tagId));

            Axis = axis;
            TagId = tagId;
            optionHasTag = new bool[space.OptionCount(axis)];
            for (int o = 0; o < optionHasTag.Length; o++) optionHasTag[o] = space.HasTag(axis, o, tagId);
            DebugText = $"{space.AxisId(axis)} 태그 {tagId} 있음";
        }

        public bool Holds(int[] picks) => optionHasTag[picks[Axis]];

        public override string ToString() => DebugText;
    }
}
