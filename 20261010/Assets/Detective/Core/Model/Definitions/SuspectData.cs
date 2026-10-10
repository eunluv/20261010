using System.Collections.Generic;

namespace Detective.Core.Model
{
    // SuspectProfile 에셋의 순수 데이터판.
    public sealed class SuspectData
    {
        public string EntityId { get; }
        public string Catchphrase { get; }
        public LieStyle LieStyle { get; }
        public IReadOnlyList<string> SmallTalkLines { get; }

        public SuspectData(string entityId, string catchphrase, LieStyle lieStyle, IEnumerable<string> smallTalkLines)
        {
            EntityId = entityId;
            Catchphrase = catchphrase;
            LieStyle = lieStyle;
            SmallTalkLines = ReadOnly.Copy(smallTalkLines);
        }

        public override string ToString() => $"{EntityId} ({LieStyle})";
    }
}
