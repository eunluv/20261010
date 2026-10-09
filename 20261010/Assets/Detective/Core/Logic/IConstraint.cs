namespace Detective.Core.Logic
{
    // 정답 후보 하나를 받아 참/거짓을 돌려주는 규칙. 단서 하나 = 조건 하나.
    // 구현은 불변이어야 하고, picks를 수정하면 안 된다.
    public interface IConstraint
    {
        // 이 후보 조합(picks[축] = 옵션 번호)에서 조건이 참인가.
        bool Holds(int[] picks);

        // 사람이 읽는 설명 (디버그·미리보기용). 플레이어용 문장 렌더링은 별도 단계에서 한다.
        string DebugText { get; }
    }
}
