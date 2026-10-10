using Detective.Core.Logic;

namespace Detective.Core.Model
{
    // 생성된 사건의 단서 하나.
    public sealed class CaseClue
    {
        public string RuleId { get; }
        public IConstraint Constraint { get; }
        public string DebugText => Constraint.DebugText;

        // 플레이어에게 보일 문장 (규칙에 문장 변형이 없으면 빈 문자열).
        public string Text { get; }
        public int VariantIndex { get; }

        // 참이지만 정답을 좁히는 데 필요하지 않은 단서.
        public bool IsRedHerring { get; }

        // 해결 경로에 들어가는 단서. Normal/Hard에서는 진짜 단서 전부, Easy에서는 그중 꼭 필요한 최소 묶음.
        public bool IsEssential { get; }

        // 증언 반박에 성공해야 얻는 단서면 그 증언 줄의 id, 아니면 null.
        public string RewardLineId { get; }
        public bool IsRebuttalReward => RewardLineId != null;

        // 문장의 근거가 된 타임라인 사실 (알리바이 단서: 그 용의자가 사건 시간대에 있던 곳). 없으면 null.
        public Fact? SourceFact { get; }

        // 단서를 목록 순서대로 적용했을 때 이 단서가 새로 지운 후보 수. 가짜 단서는 항상 0.
        public int Eliminated { get; }

        // 다른 단서 없이 이 단서 하나만 적용했을 때 지우는 후보 수.
        public int EliminatedAlone { get; }

        public ClueSource Sources { get; }

        public CaseClue(string ruleId, IConstraint constraint, string text, int variantIndex, bool isRedHerring,
            int eliminated, int eliminatedAlone, ClueSource sources,
            bool isEssential = false, string rewardLineId = null, Fact? sourceFact = null)
        {
            RuleId = ruleId;
            Constraint = constraint;
            Text = text ?? "";
            VariantIndex = variantIndex;
            IsRedHerring = isRedHerring;
            Eliminated = eliminated;
            EliminatedAlone = eliminatedAlone;
            Sources = sources;
            IsEssential = isEssential;
            RewardLineId = rewardLineId;
            SourceFact = sourceFact;
        }

        public override string ToString() => $"{RuleId}: {DebugText}";
    }
}
