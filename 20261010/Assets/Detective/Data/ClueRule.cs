using System.Collections.Generic;
using Detective.Core.Model;
using UnityEngine;

namespace Detective.Data
{
    // 단서 한 종류의 논리 + 문장. 파라미터 일부를 비워 두면 사건 생성 시 정답에 맞게 채워진다.
    //
    // 조건 타입별로 쓰는 칸:
    //   IsNot        축 A, 옵션 X
    //   HasTag       축 A, 태그
    //   LacksTag     축 A, 태그
    //   SameTag      축 A, 축 B, 태그 카테고리
    //   Implies      축 A, 옵션 X, 축 B, 옵션 Y
    //   OneOf        축 A, 옵션 X, 옵션 Y
    //   NotTogether  축 A, 옵션 X, 축 B, 옵션 Y
    [CreateAssetMenu(menuName = "Detective/Clue Rule", fileName = "Rule_", order = 3)]
    public class ClueRule : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("규칙 고유 id. 예: alibi, locked_room")]
        public string id;

        [Header("논리")]
        [Tooltip("조건 타입.\nIsNot: A는 X가 아니다\nHasTag: A는 태그 T를 가진다\nLacksTag: A는 태그 T가 없다\nSameTag: A와 B가 카테고리 태그를 공유\nImplies: A가 X면 B는 Y\nOneOf: A는 X 또는 Y\nNotTogether: A가 X면 B는 Y가 아니다")]
        public ConstraintType constraintType;

        [Tooltip("대상 축 id (사건 템플릿의 축 id). 예: culprit")]
        public string axisA;

        [Tooltip("두 번째 대상 축 id. SameTag, Implies, NotTogether에서만 쓴다")]
        public string axisB;

        [Header("고정 파라미터")]
        [Tooltip("HasTag/LacksTag의 태그. 아래 Open Tag를 켜면 무시된다")]
        public TagDef tag;

        [Tooltip("SameTag: 공유를 확인할 태그 카테고리 (필수).\nHasTag/LacksTag에서 태그를 비워 둘 때: 채울 태그의 카테고리 (비우면 모든 태그)")]
        public string tagCategory;

        [Tooltip("옵션 X (축 A의 엔티티). IsNot, Implies, OneOf, NotTogether에서 쓴다")]
        public EntityDef optionX;

        [Tooltip("옵션 Y. Implies, NotTogether에서는 축 B의 엔티티, OneOf에서는 축 A의 두 번째 엔티티")]
        public EntityDef optionY;

        [Header("빈 파라미터 (생성 시 채움)")]
        [Tooltip("켜면 태그를 생성 시점에 정답에 맞게 고른다. 예: '범인은 {tag} 소속이다'")]
        public bool openTag;

        [Tooltip("켜면 옵션 X를 생성 시점에 고른다. 예: 알리바이 규칙의 IsNot(범인, ?)")]
        public bool openOptionX = true;

        [Tooltip("켜면 옵션 Y를 생성 시점에 고른다")]
        public bool openOptionY = true;

        [Header("문장")]
        [Tooltip("단서 문장 변형. 쓸 수 있는 변수: {suspect} {place} {item} {motive} {tag} {time}, 같은 종류의 두 번째는 {suspect2} {place2} {item2} {motive2}")]
        [TextArea(1, 3)]
        public List<string> textVariants = new List<string>();

        [Header("획득")]
        [Tooltip("이 단서를 얻을 수 있는 행동 (여러 개 선택 가능)")]
        public ClueSource sources = ClueSource.Investigate;

        [Header("난이도 가중치")]
        [Tooltip("기본 선택 가중치. 클수록 자주 뽑힌다. Normal 난이도는 이 값만 쓴다")]
        [Min(0f)]
        public float difficultyWeight = 1f;

        [Tooltip("Easy 난이도에서 기본 가중치에 곱하는 배율")]
        [Min(0f)]
        public float easyWeight = 1f;

        [Tooltip("Hard 난이도에서 기본 가중치에 곱하는 배율")]
        [Min(0f)]
        public float hardWeight = 1f;
    }
}
