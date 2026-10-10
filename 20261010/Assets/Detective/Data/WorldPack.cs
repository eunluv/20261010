using System.Collections.Generic;
using UnityEngine;

namespace Detective.Data
{
    // 세계관 팩 하나에 속한 에셋 묶음.
    [CreateAssetMenu(menuName = "Detective/World Pack", fileName = "Pack_", order = 5)]
    public class WorldPack : ScriptableObject
    {
        [Header("팩")]
        [Tooltip("팩 이름. 예: 학원 축제")]
        public string displayName;

        [Header("콘텐츠")]
        [Tooltip("팩의 모든 엔티티 (용의자·장소·도구·동기)")]
        public List<EntityDef> entities = new List<EntityDef>();

        [Tooltip("용의자 엔티티에 붙는 프로필")]
        public List<SuspectProfile> suspectProfiles = new List<SuspectProfile>();

        [Tooltip("태그 사전. 엔티티와 규칙이 쓰는 태그는 모두 여기에 있어야 한다")]
        public List<TagDef> tags = new List<TagDef>();

        [Tooltip("팩에서 쓰는 단서 규칙")]
        public List<ClueRule> rules = new List<ClueRule>();

        [Tooltip("사건 템플릿")]
        public List<CaseTemplate> templates = new List<CaseTemplate>();

        [Header("문장")]
        [Tooltip("증언과 반박 증거의 문장 변형. 비어 있으면 증언이 문장 없이 만들어진다")]
        public TestimonyText testimonyText;
    }
}
