using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 단서 규칙(파라미터 일부가 빈 틀)을 이 사건의 실제 단서 후보로 펼친다 (tool-design 4-2).
    // 결과 순서는 규칙 id 순 → 옵션 번호 순 → 태그 id 순으로 항상 같다.
    public static class RuleExpander
    {
        // 빈 파라미터에 들어갈 수 있는 모든 값을 나열하고, 진짜 정답(truth)에 대해 참인 것만 남긴다.
        public static List<ClueCandidate> Expand(CaseTemplateData template, CaseSpace space, int[] truth)
        {
            if (truth == null) throw new ArgumentNullException(nameof(truth));

            var result = new List<ClueCandidate>();
            foreach (var candidate in ExpandAll(template, space))
                if (candidate.Constraint.Holds(truth)) result.Add(candidate);
            return result;
        }

        // 정답과 상관없이 만들 수 있는 모든 단서 후보.
        public static List<ClueCandidate> ExpandAll(CaseTemplateData template, CaseSpace space)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (space == null) throw new ArgumentNullException(nameof(space));

            var result = new List<ClueCandidate>();
            foreach (var rule in template.Rules) ExpandRule(rule, template, space, result);
            return result;
        }

        // 이 사건의 축 구성에서 쓸 수 없는 규칙(없는 축, 뽑히지 않은 고정 옵션, 모르는 태그)은 조용히 건너뛴다.
        static void ExpandRule(ClueRuleData rule, CaseTemplateData template, CaseSpace space, List<ClueCandidate> result)
        {
            int a = space.AxisIndex(rule.AxisA);
            if (a < 0) return;

            int b = -1;
            if (ConstraintTypes.UsesAxisB(rule.Type))
            {
                b = space.AxisIndex(rule.AxisB);
                if (b < 0 || b == a) return;
            }

            switch (rule.Type)
            {
                case ConstraintType.IsNot:
                    foreach (int x in Options(space, a, rule.IsOptionXOpen, rule.OptionXId))
                        result.Add(new ClueCandidate(rule, new IsNot(space, a, x), a, x));
                    break;

                case ConstraintType.HasTag:
                    foreach (string tagId in Tags(rule, template, space))
                        result.Add(new ClueCandidate(rule, new HasTag(space, a, tagId), tagId: tagId));
                    break;

                case ConstraintType.LacksTag:
                    foreach (string tagId in Tags(rule, template, space))
                        result.Add(new ClueCandidate(rule, new LacksTag(space, a, tagId), tagId: tagId));
                    break;

                case ConstraintType.SameTag:
                    if (space.IsKnownCategory(rule.TagCategory))
                        result.Add(new ClueCandidate(rule, new SameTag(space, a, b, rule.TagCategory)));
                    break;

                case ConstraintType.Implies:
                    foreach (int x in Options(space, a, rule.IsOptionXOpen, rule.OptionXId))
                    foreach (int y in Options(space, b, rule.IsOptionYOpen, rule.OptionYId))
                        result.Add(new ClueCandidate(rule, new Implies(space, a, x, b, y), a, x, b, y));
                    break;

                case ConstraintType.NotTogether:
                    foreach (int x in Options(space, a, rule.IsOptionXOpen, rule.OptionXId))
                    foreach (int y in Options(space, b, rule.IsOptionYOpen, rule.OptionYId))
                        result.Add(new ClueCandidate(rule, new NotTogether(space, a, x, b, y), a, x, b, y));
                    break;

                case ConstraintType.OneOf:
                    bool bothOpen = rule.IsOptionXOpen && rule.IsOptionYOpen;
                    foreach (int x in Options(space, a, rule.IsOptionXOpen, rule.OptionXId))
                    foreach (int y in Options(space, a, rule.IsOptionYOpen, rule.OptionYId))
                    {
                        if (x == y) continue;
                        if (bothOpen && y < x) continue; // {x, y}와 {y, x}는 같은 단서
                        result.Add(new ClueCandidate(rule, new OneOf(space, a, x, y), a, x, a, y));
                    }
                    break;
            }
        }

        // 빈 칸이면 축의 모든 옵션, 고정이면 그 옵션 하나(이 사건에 뽑히지 않았으면 없음).
        static List<int> Options(CaseSpace space, int axis, bool open, string fixedOptionId)
        {
            var result = new List<int>();
            if (open)
            {
                int count = space.OptionCount(axis);
                for (int o = 0; o < count; o++) result.Add(o);
            }
            else
            {
                int index = space.Axes[axis].IndexOf(fixedOptionId);
                if (index >= 0) result.Add(index);
            }
            return result;
        }

        // 빈 칸이면 (카테고리가 있으면 그 카테고리의) 모든 태그, 고정이면 그 태그 하나.
        static List<string> Tags(ClueRuleData rule, CaseTemplateData template, CaseSpace space)
        {
            var result = new List<string>();
            if (!rule.IsTagOpen)
            {
                if (space.IsKnownTag(rule.TagId)) result.Add(rule.TagId);
                return result;
            }

            foreach (var tag in template.Tags) // id 순
            {
                if (!space.IsKnownTag(tag.Id)) continue;
                if (rule.TagCategory != null && !string.Equals(tag.Category, rule.TagCategory, StringComparison.Ordinal)) continue;
                result.Add(tag.Id);
            }
            return result;
        }
    }
}
