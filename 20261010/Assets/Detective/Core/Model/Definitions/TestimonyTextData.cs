using System.Collections.Generic;

namespace Detective.Core.Model
{
    // TestimonyText 에셋의 순수 데이터판: 증언과 반박 증거의 문장 변형.
    //
    // 증언 문장(말하는 사람이 "나")에서 쓰는 변수:
    //   {time} {place}       말하는 시간대와 장소
    //   {suspect}            (Saw) 내가 본 사람
    //   {item}               (Handled) 내가 만진 도구
    // 증거 문장(제3자 시점)에서 쓰는 변수:
    //   {suspect} {time} {place}   누가 언제 어디에
    //   {suspect2}                 (Saw) 본 상대
    //   {item}                     (Handled) 만진 도구
    public sealed class TestimonyTextData
    {
        public IReadOnlyList<string> PresenceLines { get; }
        public IReadOnlyList<string> SawLines { get; }
        public IReadOnlyList<string> HandledLines { get; }
        public IReadOnlyList<string> ExaggerationPrefixes { get; }

        public IReadOnlyList<string> EvidencePresenceLines { get; }
        public IReadOnlyList<string> EvidenceSawLines { get; }
        public IReadOnlyList<string> EvidenceHandledLines { get; }
        // "{suspect}는 {time}에 {place}에 없었다" — 사실을 밝히지 않고 거짓말만 깨는 증거.
        public IReadOnlyList<string> EvidenceAbsenceLines { get; }

        public TestimonyTextData(
            IEnumerable<string> presenceLines, IEnumerable<string> sawLines, IEnumerable<string> handledLines,
            IEnumerable<string> exaggerationPrefixes,
            IEnumerable<string> evidencePresenceLines, IEnumerable<string> evidenceSawLines,
            IEnumerable<string> evidenceHandledLines, IEnumerable<string> evidenceAbsenceLines)
        {
            PresenceLines = ReadOnly.Copy(presenceLines);
            SawLines = ReadOnly.Copy(sawLines);
            HandledLines = ReadOnly.Copy(handledLines);
            ExaggerationPrefixes = ReadOnly.Copy(exaggerationPrefixes);
            EvidencePresenceLines = ReadOnly.Copy(evidencePresenceLines);
            EvidenceSawLines = ReadOnly.Copy(evidenceSawLines);
            EvidenceHandledLines = ReadOnly.Copy(evidenceHandledLines);
            EvidenceAbsenceLines = ReadOnly.Copy(evidenceAbsenceLines);
        }

        public IReadOnlyList<string> TestimonyLinesFor(FactKind kind)
        {
            switch (kind)
            {
                case FactKind.Presence: return PresenceLines;
                case FactKind.Saw: return SawLines;
                default: return HandledLines;
            }
        }

        public IReadOnlyList<string> EvidenceLinesFor(FactKind kind)
        {
            switch (kind)
            {
                case FactKind.Presence: return EvidencePresenceLines;
                case FactKind.Saw: return EvidenceSawLines;
                default: return EvidenceHandledLines;
            }
        }
    }
}
