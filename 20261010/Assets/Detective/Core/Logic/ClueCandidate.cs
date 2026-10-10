using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 규칙의 빈 파라미터를 채워 만든 단서 후보 하나. 문장 렌더링을 위해 채운 값을 함께 들고 있다.
    public sealed class ClueCandidate
    {
        public ClueRuleData Rule { get; }
        public IConstraint Constraint { get; }

        // 채워진 옵션 X, Y (쓰지 않으면 축 번호 -1).
        public int AxisX { get; }
        public int OptionX { get; }
        public int AxisY { get; }
        public int OptionY { get; }

        // 채워진 태그 (쓰지 않으면 null).
        public string TagId { get; }

        public ClueCandidate(ClueRuleData rule, IConstraint constraint, int axisX = -1, int optionX = -1,
            int axisY = -1, int optionY = -1, string tagId = null)
        {
            Rule = rule;
            Constraint = constraint;
            AxisX = axisX;
            OptionX = optionX;
            AxisY = axisY;
            OptionY = optionY;
            TagId = tagId;
        }

        public override string ToString() => $"{Rule.Id}: {Constraint.DebugText}";
    }
}
