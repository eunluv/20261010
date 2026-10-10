using UnityEngine;

namespace Detective.Data
{
    [CreateAssetMenu(menuName = "Detective/Tag", fileName = "Tag_", order = 0)]
    public class TagDef : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("태그 고유 id. 영어 소문자와 점으로 적는다. 예: club.drama, floor.2")]
        public string id;

        [Tooltip("화면에 보일 이름. 예: 연극부, 2층")]
        public string displayName;

        [Header("분류")]
        [Tooltip("태그 카테고리 id. 같은 카테고리끼리 SameTag 조건으로 비교한다. 예: club, floor, trait")]
        public string category;
    }
}
