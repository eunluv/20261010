using System;

namespace Detective.Core.Model
{
    public enum FactKind
    {
        Presence = 0, // Who가 When에 Where에 있었다
        Saw = 1,      // Who가 When에 Where에서 Target(용의자)을 봤다
        Handled = 2,  // Who가 When에 Where에서 Target(도구)을 만졌다
    }

    // 타임라인의 사실 하나 (tool-design 6-1). 번호는 모두 사건 공간의 옵션 번호다.
    public readonly struct Fact : IEquatable<Fact>
    {
        public readonly FactKind Kind;
        public readonly int Who;    // 용의자 축의 옵션 번호
        public readonly int Where;  // 장소 축의 옵션 번호
        public readonly int When;   // 시간대 번호
        public readonly int Target; // Saw: 본 용의자, Handled: 만진 도구, Presence: -1

        public Fact(FactKind kind, int who, int where, int when, int target)
        {
            Kind = kind;
            Who = who;
            Where = where;
            When = when;
            Target = target;
        }

        public static Fact Presence(int who, int where, int when) => new Fact(FactKind.Presence, who, where, when, -1);
        public static Fact Saw(int who, int where, int when, int seen) => new Fact(FactKind.Saw, who, where, when, seen);
        public static Fact Handled(int who, int where, int when, int item) => new Fact(FactKind.Handled, who, where, when, item);

        public Fact WithWhere(int where) => new Fact(Kind, Who, where, When, Target);
        public Fact WithWhen(int when) => new Fact(Kind, Who, Where, when, Target);
        public Fact WithTarget(int target) => new Fact(Kind, Who, Where, When, target);

        public bool Equals(Fact other) =>
            Kind == other.Kind && Who == other.Who && Where == other.Where && When == other.When && Target == other.Target;

        public override bool Equals(object obj) => obj is Fact other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 + Who;
                hash = hash * 397 + Where;
                hash = hash * 397 + When;
                hash = hash * 397 + Target;
                return hash;
            }
        }

        public static bool operator ==(Fact a, Fact b) => a.Equals(b);
        public static bool operator !=(Fact a, Fact b) => !a.Equals(b);

        public override string ToString() =>
            Target >= 0 ? $"{Kind}(who {Who}, where {Where}, when {When}, target {Target})" : $"{Kind}(who {Who}, where {Where}, when {When})";
    }
}
