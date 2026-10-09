using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A의 정답은 X 또는 Y다.
    public sealed class OneOf : IConstraint
    {
        public int Axis { get; }
        public int OptionX { get; }
        public int OptionY { get; }
        public string DebugText { get; }

        public OneOf(CaseSpace space, int axis, int optionX, int optionY)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckOption(axis, optionX, nameof(optionX));
            space.CheckOption(axis, optionY, nameof(optionY));
            if (optionX == optionY) throw new ArgumentException("OneOf의 두 옵션은 서로 달라야 합니다.", nameof(optionY));

            Axis = axis;
            OptionX = optionX;
            OptionY = optionY;
            DebugText = $"{space.AxisId(axis)} ∈ {{{space.OptionId(axis, optionX)}, {space.OptionId(axis, optionY)}}}";
        }

        public bool Holds(int[] picks) => picks[Axis] == OptionX || picks[Axis] == OptionY;

        public override string ToString() => DebugText;
    }
}
