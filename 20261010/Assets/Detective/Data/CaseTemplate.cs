using System;
using System.Collections.Generic;
using Detective.Core.Model;
using UnityEngine;

namespace Detective.Data
{
    // 사건 하나의 틀. 축 구성, 사용할 단서 규칙, 시간대, 난이도 값을 정한다.
    [CreateAssetMenu(menuName = "Detective/Case Template", fileName = "Case_", order = 4)]
    public class CaseTemplate : ScriptableObject
    {
        [Serializable]
        public class AxisSlot
        {
            [Tooltip("축 id. 단서 규칙의 축 A/B와 같은 이름을 쓴다. 예: culprit, place, item")]
            public string axisId;

            [Tooltip("이 축의 후보가 될 엔티티 종류")]
            public EntityKind kind;

            [Tooltip("후보가 반드시 가져야 할 태그. 비우면 같은 종류의 모든 엔티티가 후보")]
            public List<TagDef> requiredTags = new List<TagDef>();

            [Tooltip("사건마다 후보 풀에서 뽑을 옵션 수")]
            [Min(1)]
            public int pickCount = 4;
        }

        [Header("식별")]
        [Tooltip("템플릿 고유 id. 예: festival_missing_mascot")]
        public string id;

        [Tooltip("의뢰 게시판에 보일 사건 제목")]
        public string title;

        [Tooltip("의뢰 문장")]
        [TextArea(2, 5)]
        public string requestText;

        [Header("정답 축")]
        [Tooltip("플레이어가 맞혀야 할 축. 위에서부터 축 번호 0, 1, 2…")]
        public List<AxisSlot> axes = new List<AxisSlot>();

        [Header("단서")]
        [Tooltip("이 사건에서 쓸 단서 규칙")]
        public List<ClueRule> rules = new List<ClueRule>();

        [Header("시간대")]
        [Tooltip("시간 순서대로 적은 시간대. 예: 13시, 14시, 15시, 16시")]
        public List<string> timeSlots = new List<string>();

        [Tooltip("사건이 일어난 시간대의 번호 (0부터)")]
        [Min(0)]
        public int crimeSlotIndex;

        [Header("진행 값")]
        [Tooltip("조사 행동력")]
        [Min(1)]
        public int actionPoints = 6;

        [Tooltip("행동력 여유값. 해결에 꼭 필요한 행동 수가 '행동력 - 여유값'을 넘는 사건은 만들지 않는다")]
        [Min(0)]
        public int actionMargin = 1;

        [Tooltip("거짓말하는 용의자 수 (범인 포함). 범인은 항상 거짓말하므로 0이어도 1명")]
        [Min(0)]
        public int lieCount = 1;

        [Tooltip("가짜 단서(참이지만 결정적이지 않은 단서) 수")]
        [Min(0)]
        public int redHerringCount;

        [Tooltip("단서 선택 가중치와 군더더기 제거 강도를 정한다")]
        public Difficulty difficulty = Difficulty.Normal;

        [Header("진행 흐름")]
        [Tooltip("페이즈 순서. 비우면 기본 흐름(조사 → 증언 → 지목 → 결과)을 쓴다")]
        public CaseFlow flow;
    }
}
