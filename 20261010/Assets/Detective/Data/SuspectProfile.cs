using System.Collections.Generic;
using Detective.Core.Model;
using UnityEngine;

namespace Detective.Data
{
    // 용의자 엔티티에만 붙는 추가 정보.
    [CreateAssetMenu(menuName = "Detective/Suspect Profile", fileName = "Suspect_", order = 2)]
    public class SuspectProfile : ScriptableObject
    {
        [Header("대상")]
        [Tooltip("이 프로필이 붙는 용의자 엔티티. 종류가 Suspect여야 한다")]
        public EntityDef entity;

        [Header("성격")]
        [Tooltip("증언과 잡담에 섞여 나오는 말버릇")]
        public string catchphrase;

        [Tooltip("증언 처리 방식.\nLiar: 사실 하나를 바꿔 말함\nOmitter: 하나를 빼고 추궁 시 공개\nExaggerator: 사실이지만 과장\nHonest: 그대로 말함")]
        public LieStyle lieStyle = LieStyle.Honest;

        [Header("잡담")]
        [Tooltip("잡담 행동에서 나오는 대사")]
        [TextArea(1, 3)]
        public List<string> smallTalkLines = new List<string>();
    }
}
