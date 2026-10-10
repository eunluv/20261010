using System.Collections.Generic;
using Detective.Core.Model;
using UnityEngine;

namespace Detective.Data
{
    // 용의자·장소·도구·동기 등 모든 "후보"의 공통 폼.
    [CreateAssetMenu(menuName = "Detective/Entity", fileName = "Entity_", order = 1)]
    public class EntityDef : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("엔티티 고유 id. 팩 안에서 겹치면 안 된다. 예: drama_captain, storage")]
        public string id;

        [Tooltip("화면에 보일 이름. 예: 연극부 부장")]
        public string displayName;

        [Tooltip("이 엔티티가 어떤 축의 후보가 되는지 정하는 종류")]
        public EntityKind kind;

        [Header("태그")]
        [Tooltip("이 엔티티가 가진 태그. 팩의 태그 목록에 등록된 TagDef만 쓸 수 있다")]
        public List<TagDef> tags = new List<TagDef>();

        [Header("표시")]
        [Tooltip("초상화·일러스트. 비워 둬도 된다")]
        public Sprite portrait;

        [Tooltip("설명 문구 (수첩·프로필에 표시)")]
        [TextArea(2, 5)]
        public string description;
    }
}
