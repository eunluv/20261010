using Object = UnityEngine.Object;

namespace Detective.Data
{
    public enum ProblemSeverity
    {
        Error,   // 이대로는 사건을 만들 수 없음
        Warning, // 만들 수는 있지만 확인이 필요함
    }

    // 팩 검증에서 나온 문제 하나. Target은 문제가 있는 에셋(눌러서 이동하는 용도).
    public sealed class PackProblem
    {
        public ProblemSeverity Severity { get; }
        public string Message { get; }
        public Object Target { get; }

        public PackProblem(ProblemSeverity severity, string message, Object target)
        {
            Severity = severity;
            Message = message;
            Target = target;
        }

        public override string ToString() => $"[{Severity}] {Message}";
    }
}
