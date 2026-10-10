using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Runtime
{
    public enum CaseGrade
    {
        S,
        A,
        B,
        C,
    }

    // 복기 화면의 단서 한 줄.
    public sealed class ReviewClue
    {
        public int Index { get; }
        public CaseClue Clue { get; }
        public bool Obtained { get; }
        public string Where { get; } // 어디서 얻는 단서였는가

        public ReviewClue(int index, CaseClue clue, bool obtained, string where)
        {
            Index = index;
            Clue = clue;
            Obtained = obtained;
            Where = where;
        }
    }

    // 결말·복기 화면에 필요한 것 전부 (design 5-6): 정답과 내 지목, 단서별로 지운 후보 수, 놓친 단서 위치, 등급.
    public sealed class CaseReview
    {
        public CaseOutcome Outcome { get; }
        public CaseGrade Grade { get; }

        public int[] Truth { get; }
        public int[] Accusation { get; } // 지목한 적 없으면 null

        public int Trust { get; }
        public int MaxTrust { get; }
        public int ActionsUsed { get; }
        public int RequiredActions { get; } // 최단 해결 경로의 행동 수

        public IReadOnlyList<ReviewClue> Clues { get; }

        public CaseReview(CaseSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var instance = session.Instance;

            Outcome = session.Outcome;
            Truth = instance.GetTruth();
            Accusation = session.GetLastAccusation();
            Trust = session.Trust;
            MaxTrust = session.MaxTrust;
            ActionsUsed = session.ActionsUsed;
            RequiredActions = instance.RequiredActions;
            Grade = GradeOf(session);

            var clues = new List<ReviewClue>();
            for (int i = 0; i < instance.Clues.Count; i++)
                clues.Add(new ReviewClue(i, instance.Clues[i], session.HasClue(i), Describe(session, false, i)));
            Clues = clues;
        }

        // 등급: 잃은 신뢰도 1칸 = 2점, 최단 경로보다 더 쓴 행동 1회 = 1점. 0점 S, 2점까지 A, 4점까지 B, 그 이상 C.
        // 해결하지 못했으면 C.
        public static CaseGrade GradeOf(CaseSession session)
        {
            if (session.Outcome != CaseOutcome.Solved) return CaseGrade.C;

            int penalty = (session.MaxTrust - session.Trust) * 2 +
                          Math.Max(0, session.ActionsUsed - session.Instance.RequiredActions);
            if (penalty <= 0) return CaseGrade.S;
            if (penalty <= 2) return CaseGrade.A;
            if (penalty <= 4) return CaseGrade.B;
            return CaseGrade.C;
        }

        public bool IsAxisCorrect(int axis) => Accusation != null && Accusation[axis] == Truth[axis];

        // 단서나 증거를 얻는 곳을 글로.
        public static string Describe(CaseSession session, bool isEvidence, int index)
        {
            foreach (var p in session.Instance.Placements)
            {
                if (p.IsEvidence != isEvidence || p.Index != index) continue;
                switch (p.Via)
                {
                    case AcquireVia.Investigate: return $"{session.PlaceName(p.Target)} 조사";
                    case AcquireVia.Interview: return $"{session.SuspectName(p.Target)} 탐문";
                    case AcquireVia.SmallTalk: return $"{session.SuspectName(p.Target)}와(과) 잡담";
                    default:
                        var line = session.Instance.FindLine(p.LineId);
                        return line != null ? $"{session.SuspectName(line.Stated.Who)}의 거짓 증언 반박" : "증언 반박";
                }
            }
            return "?";
        }
    }
}
