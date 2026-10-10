using System.Collections.Generic;
using Detective.Core.Model;
using Detective.Data;

namespace Detective.Runtime
{
    public enum PhaseKind
    {
        None,
        Dialogue,
        Investigate,
        Testimony,
        Accuse,
        Result,
    }

    // 실행 중인 페이즈 하나. UI는 CaseSession.CurrentPhase를 실제 타입으로 보고 화면을 그리고 입력을 전달한다.
    public interface IPhase
    {
        PhaseKind Kind { get; }
        void Enter(CaseSession session);
        bool IsDone { get; }
        void Exit(CaseSession session);
    }

    // 대사 장면: 한 줄씩 넘긴다.
    public sealed class DialoguePhase : IPhase
    {
        readonly DialogueDef def;
        readonly List<string> lines = new List<string>();
        CaseSession session;
        int index;

        public PhaseKind Kind => PhaseKind.Dialogue;
        public string Speaker { get; private set; } = "";
        public string CurrentLine => index < lines.Count ? lines[index] : "";
        public bool IsDone => index >= lines.Count;

        public DialoguePhase(DialogueDef def) { this.def = def; }

        public void Enter(CaseSession s)
        {
            session = s;
            Speaker = s.SuspectName(s.ResolveRole(def.speaker));

            var template = s.Instance.Template;
            foreach (var raw in def.lines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                lines.Add(raw.Replace("{title}", template.Title ?? "").Replace("{request}", template.RequestText ?? ""));
            }
            if (!IsDone) session.AddLog($"{Speaker}: \"{CurrentLine}\"");
        }

        public void Advance()
        {
            if (IsDone) return;
            index++;
            if (!IsDone) session.AddLog($"{Speaker}: \"{CurrentLine}\"");
        }

        public void Exit(CaseSession s) { }
    }

    // 조사 단계: 행동력이 남아 있는 동안 장소 조사·탐문·잡담을 한다. 플레이어가 끝낸다.
    public sealed class InvestigatePhase : IPhase
    {
        readonly InvestigateDef def;
        bool finished;

        public PhaseKind Kind => PhaseKind.Investigate;
        public bool IsDone => finished;

        public InvestigatePhase(InvestigateDef def) { this.def = def; }

        public void Enter(CaseSession s)
        {
            s.BeginInvestigation(def.overrideActionPoints ? def.actionPoints : s.Instance.Template.ActionPoints);
        }

        public void Finish() => finished = true;

        public void Exit(CaseSession s)
        {
            s.CanInvestigate = false;
            s.AddLog("조사를 마쳤다.");
        }
    }

    // 증언 단계: 한 사람의 증언을 듣고 추궁·반박한다.
    public sealed class TestimonyPhase : IPhase
    {
        readonly TestimonyDef def;
        CaseSession session;
        bool finished;

        public PhaseKind Kind => PhaseKind.Testimony;
        public bool IsDone => finished;
        public bool MustRebut => def.mustRebut;

        // 증언하는 사람과 그 증언. 해당 역할이 없는 사건이면 null이고 페이즈는 바로 끝난다.
        public Testimony Testimony { get; private set; }

        public TestimonyPhase(TestimonyDef def) { this.def = def; }

        public void Enter(CaseSession s)
        {
            session = s;
            int speaker = s.ResolveRole(def.speaker);
            foreach (var testimony in s.Instance.Testimonies)
                if (testimony.Suspect == speaker) Testimony = testimony;

            if (Testimony == null)
            {
                finished = true;
                return;
            }
            s.TestimonySpeaker = speaker;
            s.AddLog($"{s.SuspectName(speaker)}의 증언을 듣는다.");
        }

        // 이 사람의 거짓 줄을 전부 깼는가 (거짓 줄이 없으면 true).
        public bool IsRebutted
        {
            get
            {
                if (Testimony == null) return true;
                foreach (var line in Testimony.Lines)
                    if (line.Kind == TestimonyLineKind.Lie && !session.IsRebutted(line.Id)) return false;
                return true;
            }
        }

        public bool CanFinish => !MustRebut || IsRebutted;

        // 증언을 끝낸다. 반박이 필수인데 아직 못 깼으면 끝나지 않고 false.
        public bool Finish()
        {
            if (!CanFinish) return false;
            finished = true;
            return true;
        }

        // 반박을 포기하고 넘어간다 (신뢰도 -1). 증거를 못 구한 채 들어와도 막히지 않게 하기 위한 길이다.
        public void GiveUp()
        {
            if (finished) return;
            if (!CanFinish) session.GiveUpRebuttal();
            finished = true;
        }

        public void Exit(CaseSession s)
        {
            s.TestimonySpeaker = -1;
        }
    }

    // 지목 단계: 축마다 하나씩 골라 맞힌다. 틀리면 신뢰도가 깎이고 다시 고른다.
    public sealed class AccusePhase : IPhase
    {
        readonly AccuseDef def;
        readonly List<int> axes = new List<int>();
        CaseSession session;

        public PhaseKind Kind => PhaseKind.Accuse;
        public bool IsDone => session != null && session.Outcome == CaseOutcome.Solved;

        // 맞혀야 하는 축 번호.
        public IReadOnlyList<int> Axes => axes;

        public AccusePhase(AccuseDef def) { this.def = def; }

        public void Enter(CaseSession s)
        {
            session = s;
            var space = s.Instance.Space;
            if (def.axisIds != null)
            {
                foreach (var axisId in def.axisIds)
                {
                    int axis = space.AxisIndex(axisId);
                    if (axis >= 0 && !axes.Contains(axis)) axes.Add(axis);
                }
            }
            if (axes.Count == 0)
                for (int a = 0; a < space.AxisCount; a++) axes.Add(a);

            s.AddLog("최종 추리. 누가, 어디서, 무엇으로?");
        }

        // picks: 모든 축의 선택 (길이 = 축 수). 맞혔으면 true.
        public bool Accuse(int[] picks) => session.Accuse(picks, axes, def.trustPenalty);

        public void Exit(CaseSession s) { }
    }

    // 결말과 복기.
    public sealed class ResultPhase : IPhase
    {
        bool finished;

        public PhaseKind Kind => PhaseKind.Result;
        public bool IsDone => finished;
        public CaseReview Review { get; private set; }

        public void Enter(CaseSession s)
        {
            Review = s.BuildReview();
            s.AddLog(Review.Outcome == CaseOutcome.Solved ? $"사건 해결! 등급 {Review.Grade}." : "의뢰 실패…");
        }

        public void Finish() => finished = true;

        public void Exit(CaseSession s) { }
    }

    public static class PhaseFactory
    {
        // PhaseDef(데이터) → 실행용 IPhase.
        public static IPhase Create(PhaseDef def)
        {
            switch (def)
            {
                case DialogueDef dialogue: return new DialoguePhase(dialogue);
                case InvestigateDef investigate: return new InvestigatePhase(investigate);
                case TestimonyDef testimony: return new TestimonyPhase(testimony);
                case AccuseDef accuse: return new AccusePhase(accuse);
                case ResultDef _: return new ResultPhase();
                default: throw new System.ArgumentException($"모르는 페이즈 타입: {(def == null ? "null" : def.GetType().Name)}", nameof(def));
            }
        }
    }
}
