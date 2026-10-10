using System.Collections.Generic;

namespace Detective.Core.Model
{
    public enum TestimonyLineKind
    {
        Plain = 0,       // 사실 그대로
        Lie = 1,         // 사실을 바꿔 말함 → 증거로 반박
        Hidden = 2,      // 사실이지만 숨김 → 추궁하면 공개
        Exaggerated = 3, // 사실이지만 과장해서 말함 → 반박하면 개그 반응
    }

    // 증언 한 줄.
    public sealed class TestimonyLine
    {
        public string Id { get; }
        public TestimonyLineKind Kind { get; }

        // 말한 내용. Lie가 아니면 참이다.
        public Fact Stated { get; }
        // 거짓말이 바꾸기 전의 원래 사실. Lie가 아니면 Stated와 같다.
        public Fact Original { get; }

        public string Text { get; }

        public TestimonyLine(string id, TestimonyLineKind kind, Fact stated, Fact original, string text)
        {
            Id = id;
            Kind = kind;
            Stated = stated;
            Original = original;
            Text = text ?? "";
        }

        public override string ToString() => $"{Id} [{Kind}] {Stated}";
    }

    // 용의자 한 명의 증언.
    public sealed class Testimony
    {
        public int Suspect { get; }       // 용의자 축의 옵션 번호
        public LieStyle Style { get; }
        public bool IsCulprit { get; }
        public IReadOnlyList<TestimonyLine> Lines { get; }

        public Testimony(int suspect, LieStyle style, bool isCulprit, IEnumerable<TestimonyLine> lines)
        {
            Suspect = suspect;
            Style = style;
            IsCulprit = isCulprit;
            Lines = ReadOnly.Copy(lines);
        }

        public bool HasLie
        {
            get
            {
                foreach (var line in Lines)
                    if (line.Kind == TestimonyLineKind.Lie) return true;
                return false;
            }
        }
    }

    // 거짓 줄 하나를 깨는 증거.
    public sealed class Evidence
    {
        public string Id { get; }
        // 거짓말과 모순되는 참인 사실.
        public Fact Fact { get; }
        public string RebutsLineId { get; }
        public string Text { get; }

        // false면 문장이 Fact를 그대로 밝히지 않고 "말한 곳에 없었다"만 보여 준다.
        // (범인이 사건 시간대에 사건 장소에 있었다는 사실을 증거 하나로 알려 주지 않기 위해.)
        public bool RevealsFact { get; }

        public Evidence(string id, Fact fact, string rebutsLineId, string text, bool revealsFact)
        {
            Id = id;
            Fact = fact;
            RebutsLineId = rebutsLineId;
            Text = text ?? "";
            RevealsFact = revealsFact;
        }

        public override string ToString() => $"{Id} → {RebutsLineId}: {Fact}";
    }
}
