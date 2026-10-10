using System.Collections;
using System.Collections.Generic;
using Detective.Data;

namespace Detective.Runtime
{
    // CaseFlow의 페이즈를 위에서부터 하나씩 실행한다 (tool-design 7-3).
    // 코루틴으로 돌리거나(StartCoroutine), 테스트에서는 MoveNext()를 직접 불러 한 걸음씩 진행한다.
    public class FlowRunner
    {
        // flow가 없거나 비어 있으면 기본 흐름(조사 → 증언 → 지목 → 결과)을 쓴다.
        public IEnumerator Run(CaseFlow flow, CaseSession session)
        {
            var defs = flow != null && flow.phases != null && flow.phases.Count > 0 ? flow.phases : DefaultFlow();
            bool resultShown = false;

            foreach (var def in defs)
            {
                if (def == null) continue;
                bool isResult = def is ResultDef;

                // 신뢰도가 바닥나면 남은 페이즈를 건너뛰고 결말로 간다.
                if (session.Outcome == CaseOutcome.Failed && !isResult) continue;
                if (isResult) resultShown = true;

                var phase = PhaseFactory.Create(def);
                session.EnterPhase(phase);
                while (!phase.IsDone && (isResult || session.Outcome != CaseOutcome.Failed)) yield return null;
                phase.Exit(session);
            }

            // 흐름에 결과 페이즈가 없어도 결말은 보여 준다.
            if (!resultShown)
            {
                var result = new ResultPhase();
                session.EnterPhase(result);
                while (!result.IsDone) yield return null;
                result.Exit(session);
            }

            session.EnterPhase(null);
        }

        static List<PhaseDef> DefaultFlow() => new List<PhaseDef>
        {
            new InvestigateDef(),
            new TestimonyDef { speaker = RoleSlot.AnyLiar, mustRebut = true },
            new AccuseDef(),
            new ResultDef(),
        };
    }
}
