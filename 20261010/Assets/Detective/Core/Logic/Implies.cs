using System;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 축 A가 X면 축 B는 Y다.
    public sealed class Implies : IConstraint
    {
        public int AxisA { get; }
        public int OptionX { get; }
        public int AxisB { get; }
        public int OptionY { get; }
        public string DebugText { get; }

        public Implies(CaseSpace space, int axisA, int optionX, int axisB, int optionY)
        {
            if (space == null) throw new ArgumentNullException(nameof(space));
            space.CheckOption(axisA, optionX, nameof(optionX));
            space.CheckOption(axisB, optionY, nameof(optionY));
            if (axisA == axisB) throw new ArgumentException("Implies의 두 축은 서로 달라야 합니다.", nameof(axisB));

            AxisA = axisA;
            OptionX = optionX;
            AxisB = axisB;
            OptionY = optionY;
            DebugText = $"{space.AxisId(axisA)} = {space.OptionId(axisA, optionX)} → " +
                        $"{space.AxisId(axisB)} = {space.OptionId(axisB, optionY)}";
        }

        public bool Holds(int[] picks) => picks[AxisA] != OptionX || picks[AxisB] == OptionY;

        public override string ToString() => DebugText;
    }
}
