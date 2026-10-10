using System;
using System.Collections.Generic;
using System.Text;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 단서 문장 변형의 변수를 실제 이름으로 바꾼다.
    //
    //   {suspect} {place} {item} {motive}      단서에 채워진 옵션 중 그 종류의 첫 번째 엔티티 이름
    //   {suspect2} {place2} {item2} {motive2}  같은 종류의 두 번째 엔티티 이름 (OneOf 등)
    //   {tag}                                  단서에 채워진 태그 이름
    //   {time}                                 사건 시간대
    //
    // 채울 값이 없는 변수는 그대로 남긴다 (미리보기에서 눈에 띄도록).
    public static class ClueTextRenderer
    {
        public static readonly IReadOnlyList<string> Variables = new[]
        {
            "suspect", "place", "item", "motive", "suspect2", "place2", "item2", "motive2", "tag", "time",
        };

        // 이 조건 타입의 규칙이 실제로 채워 줄 수 있는 변수. kindA/kindB는 축 A/B의 후보 종류.
        // alibiPlace: 타임라인이 채워 주는 {place2}(그 용의자가 사건 시간대에 실제로 있던 곳)를 쓸 수 있는 규칙인가.
        public static List<string> FillableVariables(ConstraintType type, EntityKind kindA, EntityKind kindB, bool alibiPlace = false)
        {
            var result = new List<string> { "time" };
            if (alibiPlace) result.Add("place2");
            if (ConstraintTypes.UsesTag(type)) result.Add("tag");
            if (ConstraintTypes.UsesOptionX(type)) result.Add(KindKey(kindA));
            if (ConstraintTypes.UsesOptionY(type))
            {
                string key = KindKey(ConstraintTypes.OptionYOnAxisA(type) ? kindA : kindB);
                result.Add(result.Contains(key) ? key + "2" : key);
            }
            return result;
        }

        // "용의자 X는 범인이 아니다" 단서의 근거가 되는 사실: X가 사건 시간대에 있던 곳. 해당하지 않으면 null.
        public static Fact? AlibiFact(ClueCandidate clue, Timeline timeline)
        {
            if (clue == null || timeline == null) return null;
            if (clue.Rule.Type != ConstraintType.IsNot || clue.AxisX != timeline.SuspectAxis) return null;
            return Fact.Presence(clue.OptionX, timeline.LocationOf(clue.OptionX, timeline.CrimeSlot), timeline.CrimeSlot);
        }

        // timeline이 있으면 알리바이 단서의 {place2}를 그 용의자가 사건 시간대에 실제로 있던 장소로 채운다.
        public static string Render(string variant, CaseTemplateData template, CaseSpace space, ClueCandidate clue, Timeline timeline)
        {
            var alibi = AlibiFact(clue, timeline);
            if (alibi.HasValue && !string.IsNullOrEmpty(variant))
            {
                string place = FactTextRenderer.Name(template, space, timeline.PlaceAxis, alibi.Value.Where);
                variant = variant.Replace("{place2}", place);
            }
            return Render(variant, template, space, clue);
        }

        public static string Render(string variant, CaseTemplateData template, CaseSpace space, ClueCandidate clue)
        {
            if (string.IsNullOrEmpty(variant)) return "";
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (clue == null) throw new ArgumentNullException(nameof(clue));

            var values = new Dictionary<string, string>(StringComparer.Ordinal); // 조회 전용
            AddOption(values, template, space, clue.AxisX, clue.OptionX);
            AddOption(values, template, space, clue.AxisY, clue.OptionY);

            if (clue.TagId != null)
            {
                var tag = template.FindTag(clue.TagId);
                values["tag"] = tag != null && !string.IsNullOrEmpty(tag.DisplayName) ? tag.DisplayName : clue.TagId;
            }
            if (template.CrimeSlotIndex >= 0 && template.CrimeSlotIndex < template.TimeSlots.Count)
                values["time"] = template.TimeSlots[template.CrimeSlotIndex];

            return Substitute(variant, values);
        }

        static void AddOption(Dictionary<string, string> values, CaseTemplateData template, CaseSpace space, int axis, int option)
        {
            if (axis < 0 || axis >= template.Axes.Count) return;

            string key = KindKey(template.Axes[axis].Kind);
            string optionId = space.OptionId(axis, option);
            var entity = template.FindEntity(optionId);
            string name = entity != null && !string.IsNullOrEmpty(entity.DisplayName) ? entity.DisplayName : optionId;

            if (!values.ContainsKey(key)) values[key] = name;
            else if (!values.ContainsKey(key + "2")) values[key + "2"] = name;
        }

        static string KindKey(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Suspect: return "suspect";
                case EntityKind.Place: return "place";
                case EntityKind.Item: return "item";
                default: return "motive";
            }
        }

        internal static string Substitute(string text, Dictionary<string, string> values)
        {
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) break;
                int close = text.IndexOf('}', open + 1);
                if (close < 0) break;

                sb.Append(text, i, open - i);
                string key = text.Substring(open + 1, close - open - 1);
                if (values.TryGetValue(key, out var value)) sb.Append(value);
                else sb.Append(text, open, close - open + 1);
                i = close + 1;
            }
            sb.Append(text, i, text.Length - i);
            return sb.ToString();
        }
    }
}
