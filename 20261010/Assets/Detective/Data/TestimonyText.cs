using System.Collections.Generic;
using UnityEngine;

namespace Detective.Data
{
    // 증언과 반박 증거의 문장 변형. 팩마다 하나.
    //
    // 증언 문장은 말하는 사람이 "나"다.  변수: {time} {place}, 본 사람 {suspect}, 만진 도구 {item}
    // 증거 문장은 제3자 시점이다.        변수: {suspect} {time} {place}, 본 상대 {suspect2}, 만진 도구 {item}
    [CreateAssetMenu(menuName = "Detective/Testimony Text", fileName = "TestimonyText", order = 6)]
    public class TestimonyText : ScriptableObject
    {
        [Header("증언 문장 (말하는 사람 = 나)")]
        [Tooltip("\"나는 그 시간에 그곳에 있었다\". 변수: {time} {place}")]
        [TextArea(1, 3)]
        public List<string> presenceLines = new List<string>();

        [Tooltip("\"그 시간에 그곳에서 누구를 봤다\". 변수: {time} {place} {suspect}(본 사람)")]
        [TextArea(1, 3)]
        public List<string> sawLines = new List<string>();

        [Tooltip("\"그 시간에 그곳에서 무엇을 만졌다\". 변수: {time} {place} {item}")]
        [TextArea(1, 3)]
        public List<string> handledLines = new List<string>();

        [Tooltip("과장하는 용의자(Exaggerator)가 사실인 줄 앞에 붙이는 말. 예: \"맹세코, \"")]
        public List<string> exaggerationPrefixes = new List<string>();

        [Header("반박 증거 문장 (제3자 시점)")]
        [Tooltip("\"누가 그 시간에 그곳에 있었다\". 변수: {suspect} {time} {place}")]
        [TextArea(1, 3)]
        public List<string> evidencePresenceLines = new List<string>();

        [Tooltip("\"누가 그 시간에 그곳에서 누구를 봤다\". 변수: {suspect} {time} {place} {suspect2}(본 상대)")]
        [TextArea(1, 3)]
        public List<string> evidenceSawLines = new List<string>();

        [Tooltip("\"누가 그 시간에 그곳에서 무엇을 만졌다\". 변수: {suspect} {time} {place} {item}")]
        [TextArea(1, 3)]
        public List<string> evidenceHandledLines = new List<string>();

        [Tooltip("\"누가 그 시간에 그곳에 없었다\". 범인의 거짓말을 깨되 실제로 있던 곳은 밝히지 않는 증거. 변수: {suspect} {time} {place}")]
        [TextArea(1, 3)]
        public List<string> evidenceAbsenceLines = new List<string>();
    }
}
