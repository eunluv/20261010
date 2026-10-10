namespace Detective.Core.Model
{
    // 단서·증거를 얻는 방법.
    public enum AcquireVia
    {
        Investigate = 0,    // 장소 조사
        Interview = 1,      // 용의자 탐문
        SmallTalk = 2,      // 용의자와 잡담
        RebuttalReward = 3, // 증언 반박 성공 보상
    }

    // 단서나 증거 하나가 어디에 놓였는가 (tool-design 6-5). 행동 1회 = 아이템 1개.
    public sealed class CasePlacement
    {
        public bool IsEvidence { get; }
        // IsEvidence면 CaseInstance.Evidence의, 아니면 CaseInstance.Clues의 번호.
        public int Index { get; }
        public AcquireVia Via { get; }

        // Investigate: 장소 축 옵션 번호. Interview/SmallTalk: 용의자 축 옵션 번호. 그 외 또는 해당 축이 없으면 -1.
        public int Target { get; }
        // RebuttalReward: 반박해야 하는 증언 줄 id.
        public string LineId { get; }

        public CasePlacement(bool isEvidence, int index, AcquireVia via, int target, string lineId)
        {
            IsEvidence = isEvidence;
            Index = index;
            Via = via;
            Target = target;
            LineId = lineId;
        }

        public override string ToString() => $"{(IsEvidence ? "evidence" : "clue")} {Index} via {Via} @{Target} {LineId}";
    }
}
