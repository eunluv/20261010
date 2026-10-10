using System.Collections.Generic;
using System.Text;
using Detective.Core.Logic;

namespace Detective.Core.Model
{
    // 생성기가 만든 사건 하나. 같은 템플릿과 같은 Seed는 항상 같은 CaseInstance를 만든다.
    public sealed class CaseInstance
    {
        readonly int[] truth;

        public CaseTemplateData Template { get; }

        // 요청한 시드와, 재시도 끝에 실제로 사건을 만든 파생 시드.
        public int Seed { get; }
        public int UsedSeed { get; }

        // 몇 번째 시도에서 성공했는가 (1부터).
        public int Attempts { get; }

        public Difficulty Difficulty { get; }

        // 축별로 뽑힌 옵션(Space.Axes[a].OptionIds, id 순)과 태그.
        public CaseSpace Space { get; }

        // 진짜 단서(선택된 순서) 뒤에 가짜 단서가 온다.
        public IReadOnlyList<CaseClue> Clues { get; }

        // 타임라인. 템플릿에 용의자 축·장소 축·시간대가 없으면 null이고, 그때는 증언과 증거도 없다.
        public Timeline Timeline { get; }
        public IReadOnlyList<Testimony> Testimonies { get; } // 용의자 옵션 번호 순
        public IReadOnlyList<Evidence> Evidence { get; }     // 거짓 줄마다 하나

        // 단서와 증거가 놓인 곳. 단서 → 증거 순.
        public IReadOnlyList<CasePlacement> Placements { get; }

        // 해결에 꼭 필요한 조사 행동 수: 반박 보상이 아닌 필수 단서 수 + 반박에 필요한 증거 수.
        public int RequiredActions { get; }

        public CaseInstance(CaseTemplateData template, int seed, int usedSeed, int attempts, Difficulty difficulty,
            CaseSpace space, int[] truth, IEnumerable<CaseClue> clues,
            Timeline timeline = null, IEnumerable<Testimony> testimonies = null, IEnumerable<Evidence> evidence = null,
            IEnumerable<CasePlacement> placements = null, int requiredActions = 0)
        {
            Template = template;
            Seed = seed;
            UsedSeed = usedSeed;
            Attempts = attempts;
            Difficulty = difficulty;
            Space = space;
            this.truth = (int[])truth.Clone();
            Clues = ReadOnly.Copy(clues);
            Timeline = timeline;
            Testimonies = ReadOnly.Copy(testimonies);
            Evidence = ReadOnly.Copy(evidence);
            Placements = ReadOnly.Copy(placements);
            RequiredActions = requiredActions;
        }

        // 정답: 축별 옵션 번호.
        public int[] GetTruth() => (int[])truth.Clone();

        public int TruthOption(int axis) => truth[axis];

        public string TruthOptionId(int axis) => Space.OptionId(axis, truth[axis]);

        // 옵션의 표시 이름 (엔티티를 못 찾거나 이름이 비었으면 id).
        public string DisplayName(int axis, int option)
        {
            string id = Space.OptionId(axis, option);
            var entity = Template != null ? Template.FindEntity(id) : null;
            return entity != null && !string.IsNullOrEmpty(entity.DisplayName) ? entity.DisplayName : id;
        }

        // 없으면 null.
        public TestimonyLine FindLine(string lineId)
        {
            foreach (var testimony in Testimonies)
                foreach (var line in testimony.Lines)
                    if (line.Id == lineId) return line;
            return null;
        }

        // 사건 전체를 사람이 읽는 글로 덤프한다. 두 사건이 같은지 비교하는 데도 쓴다.
        public string ToDebugString()
        {
            var sb = new StringBuilder();
            sb.Append("template=").Append(Template != null ? Template.Id : "?")
                .Append(" seed=").Append(Seed)
                .Append(" usedSeed=").Append(UsedSeed)
                .Append(" attempts=").Append(Attempts)
                .Append(" difficulty=").Append(Difficulty)
                .Append(" requiredActions=").Append(RequiredActions).Append('\n');

            for (int a = 0; a < Space.AxisCount; a++)
            {
                sb.Append("axis ").Append(Space.AxisId(a)).Append(": ")
                    .Append(string.Join(", ", Space.Axes[a].OptionIds))
                    .Append("  => ").Append(TruthOptionId(a)).Append('\n');
            }

            foreach (var clue in Clues)
            {
                sb.Append(clue.IsRedHerring ? "herring " : "clue    ")
                    .Append(clue.RuleId).Append(" | ").Append(clue.DebugText)
                    .Append(" | -").Append(clue.Eliminated)
                    .Append(" (alone -").Append(clue.EliminatedAlone).Append(')')
                    .Append(clue.IsEssential ? " essential" : "")
                    .Append(clue.IsRebuttalReward ? " reward<" + clue.RewardLineId + ">" : "")
                    .Append(" | ").Append(clue.Text).Append('\n');
            }

            if (Timeline != null)
            {
                foreach (var fact in Timeline.Facts)
                    sb.Append("fact    ").Append(FactTextRenderer.Describe(Template, Space, Timeline, fact)).Append('\n');
            }
            foreach (var testimony in Testimonies)
            {
                foreach (var line in testimony.Lines)
                    sb.Append("line    ").Append(line.Id).Append(" [").Append(line.Kind).Append("] ")
                        .Append(FactTextRenderer.Describe(Template, Space, Timeline, line.Stated))
                        .Append(" | ").Append(line.Text).Append('\n');
            }
            foreach (var evidence in Evidence)
                sb.Append("evidence ").Append(evidence.Id).Append(" → ").Append(evidence.RebutsLineId).Append(" | ")
                    .Append(FactTextRenderer.Describe(Template, Space, Timeline, evidence.Fact))
                    .Append(" | ").Append(evidence.Text).Append('\n');
            foreach (var placement in Placements)
                sb.Append("place   ").Append(placement).Append('\n');

            return sb.ToString();
        }
    }
}
