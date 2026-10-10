using System;

namespace Detective.Core.Model
{
    // 이 enum들은 ScriptableObject에 정수로 직렬화된다. 값을 바꾸거나 순서를 바꾸지 말고 뒤에 추가만 한다.

    // 후보 엔티티의 종류. 축마다 어떤 종류의 엔티티를 후보로 쓸지 정한다.
    public enum EntityKind
    {
        Suspect = 0,
        Place = 1,
        Item = 2,
        Motive = 3,
    }

    // 용의자의 증언 처리 방식 (tool-design 6-3).
    public enum LieStyle
    {
        Liar = 0,
        Omitter = 1,
        Exaggerator = 2,
        Honest = 3,
    }

    // 코드에 고정된 조건 타입 7종 (tool-design 4-1).
    public enum ConstraintType
    {
        IsNot = 0,
        HasTag = 1,
        LacksTag = 2,
        SameTag = 3,
        Implies = 4,
        OneOf = 5,
        NotTogether = 6,
    }

    // 단서 획득 경로.
    [Flags]
    public enum ClueSource
    {
        None = 0,
        Investigate = 1 << 0,
        Interview = 1 << 1,
        SmallTalk = 1 << 2,
    }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public static class ClueSources
    {
        // 정의된 비트 전체. 인스펙터의 "Everything"(-1) 같은 값을 걸러낼 때 쓴다.
        public const ClueSource All = ClueSource.Investigate | ClueSource.Interview | ClueSource.SmallTalk;
    }
}
