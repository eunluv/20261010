using System;
using System.Collections.Generic;
using UnityEngine;

namespace Detective.Data
{
    // 흐름 에셋은 구체적인 캐릭터 대신 역할 슬롯을 쓴다. 사건이 생성될 때 실제 캐릭터로 채워진다.
    public enum RoleSlot
    {
        Client = 0,  // 의뢰인 (용의자가 아님)
        Culprit = 1, // 범인
        AnyLiar = 2, // 거짓말한 용의자 중 1명 (반박 보상이 걸린 사람)
        Witness = 3, // 거짓말하지 않은 용의자 중 1명
    }

    // 페이즈 하나의 설정. 종류는 코드에 고정이고, 값만 폼에서 정한다 (tool-design 7-1).
    [Serializable]
    public abstract class PhaseDef
    {
    }

    [Serializable]
    public class DialogueDef : PhaseDef
    {
        [Tooltip("말하는 역할")]
        public RoleSlot speaker = RoleSlot.Client;

        [Tooltip("대사 줄. {title}은 사건 제목, {request}는 의뢰 문장으로 바뀐다")]
        [TextArea(1, 3)]
        public List<string> lines = new List<string>();
    }

    [Serializable]
    public class InvestigateDef : PhaseDef
    {
        [Tooltip("켜면 사건 템플릿의 행동력 대신 아래 값을 쓴다. 생성기는 템플릿의 행동력 기준으로 풀 수 있음을 보장하므로, 더 낮추면 풀 수 없는 판이 나올 수 있다")]
        public bool overrideActionPoints;

        [Tooltip("덮어쓸 행동력")]
        [Min(1)]
        public int actionPoints = 6;
    }

    [Serializable]
    public class TestimonyDef : PhaseDef
    {
        [Tooltip("증언할 역할")]
        public RoleSlot speaker = RoleSlot.AnyLiar;

        [Tooltip("켜면 거짓 줄을 반박해야 다음으로 넘어간다 (포기하면 신뢰도 감소)")]
        public bool mustRebut = true;
    }

    [Serializable]
    public class AccuseDef : PhaseDef
    {
        [Tooltip("맞혀야 할 축 id. 비우면 사건의 모든 축")]
        public List<string> axisIds = new List<string>();

        [Tooltip("틀렸을 때 신뢰도 감소량")]
        [Min(1)]
        public int trustPenalty = 1;
    }

    [Serializable]
    public class ResultDef : PhaseDef
    {
    }

    // 진행 순서. 러너가 위에서부터 하나씩 실행한다.
    [CreateAssetMenu(menuName = "Detective/Case Flow", fileName = "Flow_", order = 7)]
    public class CaseFlow : ScriptableObject
    {
        [SerializeReference]
        public List<PhaseDef> phases = new List<PhaseDef>();
    }
}
