using System.Collections.Generic;

namespace Detective.Core.Model
{
    // ClueRule 에셋의 순수 데이터판. 파라미터 일부를 비워 둔 "틀"이고, 생성기가 빈 칸을 채워 IConstraint로 만든다.
    //
    // 조건 타입별로 쓰는 파라미터 (쓰지 않는 칸은 null/false):
    //   IsNot        AxisA, X
    //   HasTag       AxisA, Tag
    //   LacksTag     AxisA, Tag
    //   SameTag      AxisA, AxisB, TagCategory
    //   Implies      AxisA, X, AxisB, Y
    //   OneOf        AxisA, X, Y
    //   NotTogether  AxisA, X, AxisB, Y
    // 빈 파라미터(Is...Open = true)는 id가 null이다. 태그를 비운 HasTag/LacksTag에서
    // TagCategory가 있으면 그 카테고리의 태그로만 채운다.
    public sealed class ClueRuleData
    {
        public string Id { get; }
        public ConstraintType Type { get; }

        public string AxisA { get; }
        public string AxisB { get; }

        public string TagId { get; }
        public string TagCategory { get; }
        public string OptionXId { get; }
        public string OptionYId { get; }

        public bool IsTagOpen { get; }
        public bool IsOptionXOpen { get; }
        public bool IsOptionYOpen { get; }

        public IReadOnlyList<string> TextVariants { get; }
        public ClueSource Sources { get; }

        public float DifficultyWeight { get; }
        public float EasyWeight { get; }
        public float HardWeight { get; }

        public ClueRuleData(
            string id, ConstraintType type,
            string axisA, string axisB,
            string tagId, string tagCategory, string optionXId, string optionYId,
            bool isTagOpen, bool isOptionXOpen, bool isOptionYOpen,
            IEnumerable<string> textVariants, ClueSource sources,
            float difficultyWeight, float easyWeight, float hardWeight)
        {
            Id = id;
            Type = type;
            AxisA = axisA;
            AxisB = axisB;
            TagId = tagId;
            TagCategory = tagCategory;
            OptionXId = optionXId;
            OptionYId = optionYId;
            IsTagOpen = isTagOpen;
            IsOptionXOpen = isOptionXOpen;
            IsOptionYOpen = isOptionYOpen;
            TextVariants = ReadOnly.Copy(textVariants);
            Sources = sources;
            DifficultyWeight = difficultyWeight;
            EasyWeight = easyWeight;
            HardWeight = hardWeight;
        }

        // 난이도에 따른 선택 가중치. Normal은 기본 가중치 그대로, Easy/Hard는 배율을 곱한다.
        public float WeightFor(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return DifficultyWeight * EasyWeight;
                case Difficulty.Hard: return DifficultyWeight * HardWeight;
                default: return DifficultyWeight;
            }
        }

        public override string ToString() => $"{Id} ({Type})";
    }
}
