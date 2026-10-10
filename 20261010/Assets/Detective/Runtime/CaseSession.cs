using System;
using System.Collections.Generic;
using Detective.Core.Logic;
using Detective.Core.Model;
using Detective.Data;

namespace Detective.Runtime
{
    public enum CaseOutcome
    {
        InProgress,
        Solved, // 정답 지목
        Failed, // 신뢰도 0
    }

    public enum ActionResult
    {
        Found,           // 아이템 하나 획득 (행동력 1 소모)
        Nothing,         // 그곳에 더 찾을 것이 없음 (행동력 소모 없음)
        NoActionPoints,
        NotAllowed,      // 지금은 조사 단계가 아님
    }

    public enum RebutResult
    {
        Success,      // 거짓 줄을 맞는 증거로 깼다
        Exaggeration, // 과장일 뿐 거짓은 아님 (신뢰도 감소 없음)
        Wrong,        // 틀린 반박 (신뢰도 -1)
        AlreadyRebutted,
        NotAllowed,   // 지금 반박할 수 없는 줄이거나 갖고 있지 않은 증거
    }

    // 한 판의 진행 상태. 상태를 바꾸는 것은 이 클래스의 메서드뿐이고, UI는 이벤트를 구독해 화면을 그린다.
    // 나중에 멀티에서는 방장의 세션만 상태를 바꾸고 이벤트를 참가자에게 전파한다.
    public sealed class CaseSession
    {
        public const int DefaultTrust = 5;

        readonly List<int> clues = new List<int>();        // 획득한 단서 번호 (획득 순)
        readonly List<int> evidence = new List<int>();     // 획득한 증거 번호 (획득 순)
        readonly List<string> revealed = new List<string>(); // 추궁으로 공개된 줄 id
        readonly List<string> rebutted = new List<string>(); // 반박에 성공한 줄 id
        readonly List<string> log = new List<string>();
        int[] lastAccusation;

        public CaseInstance Instance { get; }

        public int MaxTrust { get; }
        public int Trust { get; private set; }

        // 남은 행동력과, 이번 판에서 실제로 쓴 행동 수.
        public int ActionPoints { get; private set; }
        public int ActionsUsed { get; private set; }

        public CaseOutcome Outcome { get; private set; }
        public int WrongRebuttals { get; private set; }
        public int WrongAccusations { get; private set; }

        public IPhase CurrentPhase { get; private set; }
        public PhaseKind Phase => CurrentPhase != null ? CurrentPhase.Kind : PhaseKind.None;

        // 페이즈가 켜고 끄는 값.
        public bool CanInvestigate { get; internal set; }
        public int TestimonySpeaker { get; internal set; } = -1; // 지금 증언 중인 용의자, 없으면 -1

        public IReadOnlyList<int> Clues => clues;
        public IReadOnlyList<int> Evidence => evidence;
        public IReadOnlyList<string> Log => log;

        public event Action<CaseClue> OnClueGained;
        public event Action<Evidence> OnEvidenceGained;
        public event Action<int> OnTrustChanged;
        public event Action<PhaseKind> OnPhaseChanged;
        public event Action<string> OnLog;

        public CaseSession(CaseInstance instance, int trust = DefaultTrust)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            MaxTrust = trust;
            Trust = trust;
        }

        // ---- 조회 ----

        public bool HasClue(int index) => clues.Contains(index);
        public bool HasEvidence(int index) => evidence.Contains(index);
        public bool IsRevealed(string lineId) => revealed.Contains(lineId);
        public bool IsRebutted(string lineId) => rebutted.Contains(lineId);

        // 마지막 지목 (축별 옵션 번호). 지목한 적 없으면 null.
        public int[] GetLastAccusation() => lastAccusation != null ? (int[])lastAccusation.Clone() : null;

        // 지금 화면에 보여도 되는 줄인가 (숨긴 줄은 추궁 전까지 안 보인다).
        public bool IsLineVisible(TestimonyLine line) => line.Kind != TestimonyLineKind.Hidden || IsRevealed(line.Id);

        // 역할 슬롯을 이 사건의 용의자 번호로 바꾼다. 해당자가 없거나 의뢰인이면 -1.
        public int ResolveRole(RoleSlot slot)
        {
            var timeline = Instance.Timeline;
            if (timeline == null) return -1;

            switch (slot)
            {
                case RoleSlot.Culprit:
                    return Instance.TruthOption(timeline.SuspectAxis);

                case RoleSlot.AnyLiar:
                    // 반박 보상이 걸린 줄의 주인. 그런 줄이 없으면 거짓말한 아무나.
                    foreach (var clue in Instance.Clues)
                    {
                        if (!clue.IsRebuttalReward) continue;
                        var line = Instance.FindLine(clue.RewardLineId);
                        if (line != null) return line.Stated.Who;
                    }
                    foreach (var testimony in Instance.Testimonies)
                        if (testimony.HasLie) return testimony.Suspect;
                    return -1;

                case RoleSlot.Witness:
                    foreach (var testimony in Instance.Testimonies)
                        if (!testimony.HasLie) return testimony.Suspect;
                    return -1;

                default:
                    return -1;
            }
        }

        public string SuspectName(int suspect)
        {
            var timeline = Instance.Timeline;
            return timeline != null && suspect >= 0 ? Instance.DisplayName(timeline.SuspectAxis, suspect) : "의뢰인";
        }

        public string PlaceName(int place)
        {
            var timeline = Instance.Timeline;
            return timeline != null && place >= 0 ? Instance.DisplayName(timeline.PlaceAxis, place) : "?";
        }

        // ---- 러너와 페이즈가 부르는 것 ----

        internal void EnterPhase(IPhase phase)
        {
            CurrentPhase = phase;
            if (phase != null) phase.Enter(this);
            OnPhaseChanged?.Invoke(Phase);
        }

        internal void BeginInvestigation(int actionPoints)
        {
            ActionPoints = Math.Max(0, actionPoints);
            CanInvestigate = true;
            AddLog($"조사 시작. 행동력 {ActionPoints}.");
        }

        public void AddLog(string text)
        {
            log.Add(text);
            OnLog?.Invoke(text);
        }

        // ---- 조사 행동 (행동 1회 = 아이템 1개) ----

        public ActionResult Investigate(int place) => Act(AcquireVia.Investigate, place, $"{PlaceName(place)} 조사");
        public ActionResult Interview(int suspect) => Act(AcquireVia.Interview, suspect, $"{SuspectName(suspect)} 탐문");
        public ActionResult SmallTalk(int suspect) => Act(AcquireVia.SmallTalk, suspect, $"{SuspectName(suspect)}와(과) 잡담");

        ActionResult Act(AcquireVia via, int target, string what)
        {
            if (!CanInvestigate || Outcome != CaseOutcome.InProgress) return ActionResult.NotAllowed;
            if (ActionPoints <= 0)
            {
                AddLog("행동력이 없다.");
                return ActionResult.NoActionPoints;
            }

            // 같은 곳에 여러 개가 있으면 해결에 필요한 것부터 준다. 생성기가 센 "필요 행동 수"가 실제로도 맞게 하기 위해서다.
            CasePlacement best = null;
            int bestRank = int.MaxValue;
            foreach (var placement in Instance.Placements)
            {
                if (placement.Via != via || placement.Target != target) continue;
                if (placement.IsEvidence ? HasEvidence(placement.Index) : HasClue(placement.Index)) continue;
                int rank = Rank(placement);
                if (rank < bestRank)
                {
                    best = placement;
                    bestRank = rank;
                }
            }

            if (best == null)
            {
                AddLog($"{what}: 더 찾을 게 없다. (행동력 소모 없음)");
                return ActionResult.Nothing;
            }

            ActionPoints--;
            ActionsUsed++;
            AddLog($"{what}. (남은 행동력 {ActionPoints})");
            if (best.IsEvidence) GiveEvidence(best.Index);
            else GiveClue(best.Index);
            return ActionResult.Found;
        }

        // 작을수록 먼저: 필수 단서 → 보상이 걸린 줄의 증거 → 보조 단서 → 다른 증거 → 가짜 단서.
        int Rank(CasePlacement placement)
        {
            if (placement.IsEvidence)
            {
                string lineId = Instance.Evidence[placement.Index].RebutsLineId;
                foreach (var clue in Instance.Clues)
                    if (clue.RewardLineId == lineId) return 1;
                return 3;
            }

            var c = Instance.Clues[placement.Index];
            if (c.IsEssential) return 0;
            return c.IsRedHerring ? 4 : 2;
        }

        void GiveClue(int index)
        {
            if (HasClue(index)) return;
            clues.Add(index);
            var clue = Instance.Clues[index];
            AddLog("단서 획득: " + (string.IsNullOrEmpty(clue.Text) ? clue.DebugText : clue.Text));
            OnClueGained?.Invoke(clue);
        }

        void GiveEvidence(int index)
        {
            if (HasEvidence(index)) return;
            evidence.Add(index);
            var item = Instance.Evidence[index];
            AddLog("증거 획득: " + (string.IsNullOrEmpty(item.Text) ? item.ToString() : item.Text));
            OnEvidenceGained?.Invoke(item);
        }

        // ---- 증언: 추궁과 반박 ----

        TestimonyLine SpeakerLine(string lineId)
        {
            if (Phase != PhaseKind.Testimony || TestimonySpeaker < 0 || Outcome != CaseOutcome.InProgress) return null;
            foreach (var testimony in Instance.Testimonies)
            {
                if (testimony.Suspect != TestimonySpeaker) continue;
                foreach (var line in testimony.Lines)
                    if (line.Id == lineId) return line;
            }
            return null;
        }

        // 추궁: 보이는 줄을 더 캐묻는다. 말하는 사람이 숨긴 줄이 있으면 하나가 공개된다. 새로 공개됐으면 true.
        // (숨긴 줄은 화면에 없으므로, 어느 줄을 추궁하든 숨긴 줄이 나온다.)
        public bool Press(string lineId)
        {
            if (SpeakerLine(lineId) == null) return false;

            foreach (var testimony in Instance.Testimonies)
            {
                if (testimony.Suspect != TestimonySpeaker) continue;
                foreach (var line in testimony.Lines)
                {
                    if (line.Kind != TestimonyLineKind.Hidden || IsRevealed(line.Id)) continue;
                    revealed.Add(line.Id);
                    AddLog($"추궁 성공! {SuspectName(TestimonySpeaker)}: \"…사실은, {line.Text}\"");
                    return true;
                }
            }

            AddLog($"{SuspectName(TestimonySpeaker)}: \"그건 아까 말한 그대로야.\"");
            return false;
        }

        // 반박: 증언 줄에 증거를 들이민다.
        public RebutResult Rebut(string lineId, int evidenceIndex)
        {
            var line = SpeakerLine(lineId);
            if (line == null || !HasEvidence(evidenceIndex) || !IsLineVisible(line)) return RebutResult.NotAllowed;
            if (IsRebutted(lineId)) return RebutResult.AlreadyRebutted;

            string speaker = SuspectName(TestimonySpeaker);
            var item = Instance.Evidence[evidenceIndex];

            if (line.Kind == TestimonyLineKind.Lie && item.RebutsLineId == lineId)
            {
                rebutted.Add(lineId);
                AddLog($"반박 성공! {speaker}의 증언이 무너졌다.");
                for (int i = 0; i < Instance.Clues.Count; i++)
                    if (Instance.Clues[i].RewardLineId == lineId) GiveClue(i);
                return RebutResult.Success;
            }

            if (line.Kind == TestimonyLineKind.Exaggerated)
            {
                AddLog($"{speaker}: \"과, 과장 좀 했을 뿐이야! 거짓말은 아니라고!\" (신뢰도 변화 없음)");
                return RebutResult.Exaggeration;
            }

            WrongRebuttals++;
            AddLog($"{speaker}: \"그게 내 말이랑 무슨 상관인데?\" 틀린 반박이다.");
            ChangeTrust(-1);
            return RebutResult.Wrong;
        }

        // 반박해야 하는 증언을 포기하고 넘어간다.
        internal void GiveUpRebuttal()
        {
            AddLog("반박을 포기했다.");
            ChangeTrust(-1);
        }

        // ---- 지목 ----

        // axes: 맞혀야 하는 축 번호. picks: 모든 축의 선택(길이 = 축 수).
        public bool Accuse(int[] picks, IReadOnlyList<int> axes, int trustPenalty)
        {
            if (Phase != PhaseKind.Accuse || Outcome != CaseOutcome.InProgress) return false;
            if (picks == null || picks.Length != Instance.Space.AxisCount) throw new ArgumentException("picks의 길이는 축 수와 같아야 합니다.", nameof(picks));

            lastAccusation = (int[])picks.Clone();
            bool correct = true;
            foreach (int axis in axes)
                if (picks[axis] != Instance.TruthOption(axis)) correct = false;

            if (correct)
            {
                Outcome = CaseOutcome.Solved;
                AddLog("지목 성공! 사건 해결.");
                return true;
            }

            WrongAccusations++;
            AddLog("지목이 틀렸다. 의뢰인의 표정이 굳는다.");
            ChangeTrust(-Math.Max(1, trustPenalty));
            return false;
        }

        void ChangeTrust(int delta)
        {
            int before = Trust;
            Trust = Math.Max(0, Math.Min(MaxTrust, Trust + delta));
            if (Trust == before) return;

            AddLog($"신뢰도 {before} → {Trust}");
            OnTrustChanged?.Invoke(Trust);
            if (Trust == 0 && Outcome == CaseOutcome.InProgress)
            {
                Outcome = CaseOutcome.Failed;
                AddLog("신뢰도가 바닥났다. 의뢰 실패.");
            }
        }

        // ---- 결말 ----

        public CaseReview BuildReview() => new CaseReview(this);
    }
}
