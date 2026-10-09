using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A의 정답은 X가 아니다.
    public sealed class IsNot : IConstraint
    {
        public int Axis { get; }
        public int Option { get; }
        public string DebugText { get; }

        public IsNot(CaseSpace space, int axis, int option)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckOption(axis, option, nameof(option));

            Axis = axis;
            Option = option;
            DebugText = $"{space.AxisId(axis)} ≠ {space.OptionId(axis, option)}";
        }

        public bool Holds(int[] picks) => picks[Axis] != Option;

        public override string ToString() => DebugText;
    }
}
