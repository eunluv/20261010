namespace Detective.Core.Model
{
    // 조건 타입별로 어떤 파라미터를 쓰는지. 변환기·검증·편집 창이 같은 표를 본다.
    //   IsNot        축 A, X
    //   HasTag       축 A, 태그
    //   LacksTag     축 A, 태그
    //   SameTag      축 A, 축 B, 태그 카테고리
    //   Implies      축 A, X, 축 B, Y
    //   OneOf        축 A, X, Y(축 A의 두 번째 옵션)
    //   NotTogether  축 A, X, 축 B, Y
    public static class ConstraintTypes
    {
        public static bool UsesAxisB(ConstraintType type) =>
            type == ConstraintType.SameTag || type == ConstraintType.Implies || type == ConstraintType.NotTogether;

        public static bool UsesTag(ConstraintType type) =>
            type == ConstraintType.HasTag || type == ConstraintType.LacksTag;

        public static bool UsesOptionX(ConstraintType type) =>
            type == ConstraintType.IsNot || type == ConstraintType.Implies ||
            type == ConstraintType.OneOf || type == ConstraintType.NotTogether;

        public static bool UsesOptionY(ConstraintType type) =>
            type == ConstraintType.Implies || type == ConstraintType.OneOf || type == ConstraintType.NotTogether;

        // 옵션 Y가 속한 축: OneOf는 축 A, 나머지는 축 B.
        public static bool OptionYOnAxisA(ConstraintType type) => type == ConstraintType.OneOf;
    }
}
