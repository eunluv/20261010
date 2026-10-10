using System;
using System.Collections.Generic;
using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 단서 규칙 탭: 조건 타입에 필요한 칸만 보여 준다.
    public partial class PackEditorWindow
    {
        const string OpenTip = "켜면 사건을 만들 때마다 정답에 맞는 값을 자동으로 고릅니다. 끄면 아래에서 고른 값으로 고정됩니다.";

        void DrawRulesTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    foreach (var rule in pack.rules)
                    {
                        if (rule == null) continue;
                        var r = rule;
                        ListItem(r, selectedRule == r, $"{PackGui.Name(null, r.id)}  ·  {r.constraintType}",
                            () => { selectedRule = r; detailScroll = Vector2.zero; });
                    }
                    EditorGUILayout.EndScrollView();

                    if (GUILayout.Button("+ 규칙 추가", GUILayout.Height(24)))
                        PackGui.Later(() =>
                        {
                            var axes = AxisIds();
                            selectedRule = PackAssetOps.CreateRule(pack, axes.Count > 0 ? axes[0] : null);
                            needsSync = true;
                        });
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (selectedRule == null || !pack.rules.Contains(selectedRule))
                        EditorGUILayout.HelpBox("왼쪽에서 규칙을 고르거나 새로 만드세요.", MessageType.Info);
                    else
                        DrawRuleDetail(selectedRule);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawRuleDetail(ClueRule r)
        {
            DrawProblemsFor(r);

            var type = r.constraintType;
            var axisIds = AxisIds();
            var categories = Categories();

            Header("기본");
            PackGui.Text(r, PackGui.L("Id", "규칙 고유 id. 예: alibi, locked_room"), r.id, v =>
            {
                r.id = v.Trim();
                PackAssetOps.RenameToId(r, PackAssetOps.RulePrefix, r.id);
                needsSync = true;
            }, true);

            Header("논리");
            PackGui.EnumField(r, PackGui.L("조건 타입",
                    "IsNot: A는 X가 아니다\nHasTag: A는 태그를 가진다\nLacksTag: A는 태그가 없다\nSameTag: A와 B가 같은 카테고리 태그를 가진다\nImplies: A가 X면 B는 Y다\nOneOf: A는 X 또는 Y다\nNotTogether: A가 X면 B는 Y가 아니다"),
                r.constraintType, v => r.constraintType = v);
            EditorGUILayout.LabelField("뜻: " + RuleSummary(r), summaryStyle);
            EditorGUILayout.Space(4);

            PackGui.TextWithOptions(r, PackGui.L("축 A", "대상 축 id. 사건 템플릿의 축 id와 같아야 합니다"), r.axisA, axisIds, v => r.axisA = v.Trim());
            if (ConstraintTypes.UsesAxisB(type))
                PackGui.TextWithOptions(r, PackGui.L("축 B", "두 번째 축 id"), r.axisB, axisIds, v => r.axisB = v.Trim());

            if (ConstraintTypes.UsesTag(type))
            {
                PackGui.Toggle(r, PackGui.L("태그: 생성 시 고름", OpenTip), r.openTag, v => r.openTag = v);
                if (r.openTag)
                    PackGui.TextWithOptions(r, PackGui.L("   고를 카테고리", "이 카테고리의 태그 중에서만 고릅니다. 비우면 모든 태그"),
                        r.tagCategory, categories, v => r.tagCategory = v.Trim());
                else
                    PackGui.TagPopup(r, PackGui.L("   태그", "고정할 태그"), r.tag, pack.tags, v => r.tag = v);
            }
            else if (type == ConstraintType.SameTag)
            {
                PackGui.TextWithOptions(r, PackGui.L("태그 카테고리", "두 축의 정답이 이 카테고리의 태그를 함께 가져야 합니다. 예: club"),
                    r.tagCategory, categories, v => r.tagCategory = v.Trim());
            }

            if (ConstraintTypes.UsesOptionX(type))
            {
                PackGui.Toggle(r, PackGui.L("X: 생성 시 고름", OpenTip), r.openOptionX, v => r.openOptionX = v);
                if (!r.openOptionX)
                    PackGui.EntityPopup(r, PackGui.L("   X", "축 A의 엔티티"), r.optionX, EntitiesForAxis(r.axisA), v => r.optionX = v);
            }
            if (ConstraintTypes.UsesOptionY(type))
            {
                string axisOfY = ConstraintTypes.OptionYOnAxisA(type) ? r.axisA : r.axisB;
                PackGui.Toggle(r, PackGui.L("Y: 생성 시 고름", OpenTip), r.openOptionY, v => r.openOptionY = v);
                if (!r.openOptionY)
                    PackGui.EntityPopup(r, PackGui.L("   Y", $"축 '{axisOfY}'의 엔티티"), r.optionY, EntitiesForAxis(axisOfY), v => r.optionY = v);
            }

            Header("문장 변형");
            EditorGUILayout.LabelField(
                PackGui.L("쓸 수 있는 변수: {suspect} {place} {item} {motive} {tag} {time}",
                    "{suspect} {place} {item} {motive}: 이 단서에 채워진 그 종류의 엔티티 이름\n{suspect2} {place2} {item2} {motive2}: 같은 종류의 두 번째 엔티티 (OneOf 등)\n{tag}: 채워진 태그 이름\n{time}: 사건 시간대"),
                EditorStyles.miniLabel);
            PackGui.StringList(r, r.textVariants, "+ 문장 추가", true);

            Header("획득과 가중치");
            PackGui.FlagsField(r, PackGui.L("획득 경로", "이 단서를 얻을 수 있는 행동. 여러 개 고를 수 있습니다"), r.sources, v => r.sources = v);
            PackGui.Float(r, PackGui.L("기본 가중치", "클수록 자주 뽑힙니다. Normal 난이도는 이 값만 씁니다"), r.difficultyWeight, 0f, v => r.difficultyWeight = v);
            PackGui.Float(r, PackGui.L("Easy 배율", "Easy 난이도에서 기본 가중치에 곱합니다"), r.easyWeight, 0f, v => r.easyWeight = v);
            PackGui.Float(r, PackGui.L("Hard 배율", "Hard 난이도에서 기본 가중치에 곱합니다"), r.hardWeight, 0f, v => r.hardWeight = v);

            Header("쓰는 사건 템플릿");
            var users = TemplatesUsing(r);
            EditorGUILayout.LabelField(users.Count == 0 ? "(없음 — 사건 템플릿 탭에서 체크하세요)" : string.Join(", ", users));

            EditorGUILayout.Space(16);
            if (GUILayout.Button("이 규칙 삭제", GUILayout.Width(140)))
            {
                string message = $"규칙 '{PackGui.Name(null, r.id)}'를 지웁니다.";
                if (users.Count > 0) message += $"\n사건 템플릿 {users.Count}개에서 이 규칙이 빠집니다.";
                message += "\n파일은 휴지통으로 이동합니다.";
                ConfirmLater("규칙 삭제", message, () =>
                {
                    PackAssetOps.DeleteRule(pack, r);
                    selectedRule = null;
                });
            }
        }

        // 규칙을 한 문장으로 풀어 쓴다.
        static string RuleSummary(ClueRule r)
        {
            string a = Blank(r.axisA, "(축 A)");
            string b = Blank(r.axisB, "(축 B)");
            string x = r.openOptionX ? "(생성 시 고른 후보)" : EntityName(r.optionX);
            string y = r.openOptionY ? "(생성 시 고른 후보)" : EntityName(r.optionY);
            string tag = r.openTag
                ? (string.IsNullOrEmpty(r.tagCategory) ? "(생성 시 고른 태그)" : $"(생성 시 고른 {r.tagCategory} 태그)")
                : (r.tag == null ? "(태그 없음)" : PackGui.Name(r.tag.displayName, r.tag.id));

            switch (r.constraintType)
            {
                case ConstraintType.IsNot: return $"{a}의 정답은 {x}이(가) 아니다.";
                case ConstraintType.HasTag: return $"{a}의 정답은 '{tag}' 태그를 가진다.";
                case ConstraintType.LacksTag: return $"{a}의 정답은 '{tag}' 태그가 없다.";
                case ConstraintType.SameTag: return $"{a}의 정답과 {b}의 정답은 같은 {Blank(r.tagCategory, "(카테고리)")} 태그를 가진다.";
                case ConstraintType.Implies: return $"{a}의 정답이 {x}이면, {b}의 정답은 {y}이다.";
                case ConstraintType.OneOf: return $"{a}의 정답은 {x} 또는 {y}이다.";
                case ConstraintType.NotTogether: return $"{a}의 정답이 {x}이면, {b}의 정답은 {y}이(가) 아니다.";
                default: return r.constraintType.ToString();
            }
        }

        static string Blank(string text, string fallback) => string.IsNullOrEmpty(text) ? fallback : text;

        static string EntityName(EntityDef entity) => entity == null ? "(없음)" : PackGui.Name(entity.displayName, entity.id);

        // 팩의 사건 템플릿들이 쓰는 축 id (정렬, 중복 없음).
        List<string> AxisIds()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var template in pack.templates)
            {
                if (template == null || template.axes == null) continue;
                foreach (var slot in template.axes)
                    if (slot != null && !string.IsNullOrEmpty(slot.axisId)) set.Add(slot.axisId);
            }
            return new List<string>(set);
        }

        // 축 id에 해당하는 종류의 엔티티만 고르게 한다. 축을 모르면 전부 보여 준다.
        List<EntityDef> EntitiesForAxis(string axisId)
        {
            EntityKind? kind = null;
            foreach (var template in pack.templates)
            {
                if (template == null || template.axes == null) continue;
                foreach (var slot in template.axes)
                {
                    if (slot == null || !string.Equals(slot.axisId, axisId, StringComparison.Ordinal)) continue;
                    kind = slot.kind;
                    break;
                }
                if (kind.HasValue) break;
            }

            var result = new List<EntityDef>();
            foreach (var entity in pack.entities)
                if (entity != null && (!kind.HasValue || entity.kind == kind.Value)) result.Add(entity);
            return result;
        }

        List<string> TemplatesUsing(ClueRule rule)
        {
            var result = new List<string>();
            foreach (var template in pack.templates)
                if (template != null && template.rules != null && template.rules.Contains(rule))
                    result.Add(PackGui.Name(template.title, template.id));
            return result;
        }
    }
}
