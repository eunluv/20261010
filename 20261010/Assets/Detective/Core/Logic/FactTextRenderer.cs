using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 타임라인의 사실을 증언·증거 문장으로 바꾼다. 변수 목록은 TestimonyTextData 주석 참고.
    public static class FactTextRenderer
    {
        public static readonly IReadOnlyList<string> TestimonyVariables = new[] { "time", "place", "suspect", "item" };
        public static readonly IReadOnlyList<string> EvidenceVariables = new[] { "time", "place", "suspect", "suspect2", "item" };

        // 증언 문장: 말하는 사람은 "나"이고, {suspect}는 내가 본 사람이다.
        public static string Testimony(string variant, CaseTemplateData template, CaseSpace space, Timeline timeline, Fact stated)
        {
            if (string.IsNullOrEmpty(variant)) return "";
            var values = Common(template, space, timeline, stated);
            if (stated.Kind == FactKind.Saw) values["suspect"] = Name(template, space, timeline.SuspectAxis, stated.Target);
            return ClueTextRenderer.Substitute(variant, values);
        }

        // 증거 문장: {suspect}는 사실의 주인공, {suspect2}는 그 사람이 본 상대다.
        public static string Evidence(string variant, CaseTemplateData template, CaseSpace space, Timeline timeline, Fact fact)
        {
            if (string.IsNullOrEmpty(variant)) return "";
            var values = Common(template, space, timeline, fact);
            values["suspect"] = Name(template, space, timeline.SuspectAxis, fact.Who);
            if (fact.Kind == FactKind.Saw) values["suspect2"] = Name(template, space, timeline.SuspectAxis, fact.Target);
            return ClueTextRenderer.Substitute(variant, values);
        }

        // 사람이 읽는 설명 (디버그·미리보기용).
        public static string Describe(CaseTemplateData template, CaseSpace space, Timeline timeline, Fact fact)
        {
            string who = Name(template, space, timeline.SuspectAxis, fact.Who);
            string where = Name(template, space, timeline.PlaceAxis, fact.Where);
            string when = TimeName(template, fact.When);
            switch (fact.Kind)
            {
                case FactKind.Presence: return $"{when} {who} @ {where}";
                case FactKind.Saw: return $"{when} {who} @ {where}, saw {Name(template, space, timeline.SuspectAxis, fact.Target)}";
                default: return $"{when} {who} @ {where}, handled {Name(template, space, timeline.ItemAxis, fact.Target)}";
            }
        }

        static Dictionary<string, string> Common(CaseTemplateData template, CaseSpace space, Timeline timeline, Fact fact)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (space == null) throw new ArgumentNullException(nameof(space));
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));

            var values = new Dictionary<string, string>(StringComparer.Ordinal); // 조회 전용
            values["time"] = TimeName(template, fact.When);
            values["place"] = Name(template, space, timeline.PlaceAxis, fact.Where);
            if (fact.Kind == FactKind.Handled && timeline.ItemAxis >= 0)
                values["item"] = Name(template, space, timeline.ItemAxis, fact.Target);
            return values;
        }

        static string TimeName(CaseTemplateData template, int slot) =>
            slot >= 0 && slot < template.TimeSlots.Count ? template.TimeSlots[slot] : "?";

        // 옵션의 표시 이름 (엔티티를 못 찾거나 이름이 비었으면 id).
        public static string Name(CaseTemplateData template, CaseSpace space, int axis, int option)
        {
            if (axis < 0 || axis >= space.AxisCount || option < 0 || option >= space.OptionCount(axis)) return "?";
            string id = space.OptionId(axis, option);
            var entity = template.FindEntity(id);
            return entity != null && !string.IsNullOrEmpty(entity.DisplayName) ? entity.DisplayName : id;
        }
    }
}
