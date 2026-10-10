using System.Collections.Generic;
using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 사건 템플릿 탭: 축 구성, 쓸 규칙, 시간대, 진행 값.
    public partial class PackEditorWindow
    {
        static readonly string[] CommonAxisIds = { "culprit", "place", "item", "motive", "accomplice" };

        void DrawTemplatesTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    foreach (var template in pack.templates)
                    {
                        if (template == null) continue;
                        var t = template;
                        ListItem(t, selectedTemplate == t, PackGui.Name(t.title, t.id),
                            () => { selectedTemplate = t; detailScroll = Vector2.zero; });
                    }
                    EditorGUILayout.EndScrollView();

                    if (GUILayout.Button("+ 사건 템플릿 추가", GUILayout.Height(24)))
                        PackGui.Later(() => { selectedTemplate = PackAssetOps.CreateTemplate(pack); needsSync = true; });
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (selectedTemplate == null || !pack.templates.Contains(selectedTemplate))
                        EditorGUILayout.HelpBox("왼쪽에서 사건 템플릿을 고르거나 새로 만드세요.", MessageType.Info);
                    else
                        DrawTemplateDetail(selectedTemplate);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawTemplateDetail(CaseTemplate t)
        {
            DrawProblemsFor(t);

            Header("기본");
            PackGui.Text(t, PackGui.L("Id", "템플릿 고유 id. 예: festival_missing_mascot"), t.id, v =>
            {
                t.id = v.Trim();
                PackAssetOps.RenameToId(t, PackAssetOps.TemplatePrefix, t.id);
                needsSync = true;
            }, true);
            PackGui.Text(t, PackGui.L("제목", "의뢰 게시판에 보일 사건 제목"), t.title, v => t.title = v);
            PackGui.Area(t, PackGui.L("의뢰 문장"), t.requestText, v => t.requestText = v, 2);

            DrawAxes(t);
            DrawTemplateRules(t);
            DrawTimeSlots(t);

            Header("진행 값");
            PackGui.Int(t, PackGui.L("행동력", "조사 행동 수"), t.actionPoints, 1, v => t.actionPoints = v);
            PackGui.Int(t, PackGui.L("행동력 여유값", "해결에 꼭 필요한 행동 수가 '행동력 - 여유값'을 넘는 사건은 만들지 않습니다"),
                t.actionMargin, 0, v => t.actionMargin = v);
            PackGui.Int(t, PackGui.L("거짓말하는 용의자 수", "범인 포함. 범인은 항상 거짓말하므로 0이어도 1명입니다"), t.lieCount, 0, v => t.lieCount = v);
            PackGui.Int(t, PackGui.L("가짜 단서 수", "참이지만 결정적이지 않은 단서 수"), t.redHerringCount, 0, v => t.redHerringCount = v);
            PackGui.EnumField(t, PackGui.L("난이도", "단서 선택 가중치와 군더더기 제거 강도"), t.difficulty, v => t.difficulty = v);
            PackGui.ObjectField(t, PackGui.L("진행 흐름", "페이즈 순서(Case Flow 에셋). 비우면 기본 흐름: 조사 → 증언 → 지목 → 결과"), t.flow, v => t.flow = v);

            EditorGUILayout.Space(16);
            if (GUILayout.Button("이 템플릿 삭제", GUILayout.Width(140)))
            {
                ConfirmLater("사건 템플릿 삭제", $"사건 템플릿 '{PackGui.Name(t.title, t.id)}'를 지웁니다.\n파일은 휴지통으로 이동합니다.", () =>
                {
                    PackAssetOps.DeleteTemplate(pack, t);
                    selectedTemplate = null;
                });
            }
        }

        void DrawAxes(CaseTemplate t)
        {
            Header("정답 축 (플레이어가 맞혀야 할 것)");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(PackGui.L("축 Id", "단서 규칙의 축 A/B와 같은 이름"), EditorStyles.miniBoldLabel, GUILayout.Width(137));
                GUILayout.Label(PackGui.L("후보 종류"), EditorStyles.miniBoldLabel, GUILayout.Width(84));
                GUILayout.Label(PackGui.L("뽑을 수", "사건마다 후보 풀에서 뽑을 옵션 수"), EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label(PackGui.L("필수 태그", "후보가 반드시 가져야 할 태그. 비우면 같은 종류 전부"), EditorStyles.miniBoldLabel, GUILayout.Width(170));
                GUILayout.Label(PackGui.L("후보", "조건에 맞는 엔티티 수"), EditorStyles.miniBoldLabel, GUILayout.Width(50));
            }

            for (int i = 0; i < t.axes.Count; i++)
            {
                var slot = t.axes[i];
                if (slot == null) continue;
                int index = i;
                using (new EditorGUILayout.HorizontalScope())
                {
                    PackGui.TextWithOptions(t, null, slot.axisId, CommonAxisIds, v => slot.axisId = v.Trim(), GUILayout.Width(110));
                    PackGui.EnumField(t, null, slot.kind, v => slot.kind = v, GUILayout.Width(84));
                    PackGui.Int(t, null, slot.pickCount, 1, v => slot.pickCount = v, GUILayout.Width(50));
                    PackGui.TagList(t, null, slot.requiredTags, pack.tags, GUILayout.Width(170));

                    int candidates = PackValidator.Candidates(pack, slot).Count;
                    var old = GUI.color;
                    if (candidates < slot.pickCount) GUI.color = new Color(1f, 0.5f, 0.45f);
                    GUILayout.Label($"{candidates}개", GUILayout.Width(50));
                    GUI.color = old;

                    if (GUILayout.Button(PackGui.L("×", "이 축 지우기"), GUILayout.Width(22)))
                        PackGui.Later(() => PackGui.Apply(t, () => { if (index < t.axes.Count) t.axes.RemoveAt(index); }));
                    GUILayout.FlexibleSpace();
                }
            }

            if (GUILayout.Button("+ 축 추가", GUILayout.Width(140)))
                PackGui.Later(() => PackGui.Apply(t, () => t.axes.Add(new CaseTemplate.AxisSlot())));
        }

        void DrawTemplateRules(CaseTemplate t)
        {
            Header($"사용할 단서 규칙 ({CountRules(t)}/{pack.rules.Count})");
            if (pack.rules.Count == 0)
            {
                EditorGUILayout.LabelField("단서 규칙 탭에서 먼저 규칙을 만드세요.", EditorStyles.miniLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체 선택", EditorStyles.miniButton, GUILayout.Width(70)))
                    PackGui.Later(() => PackGui.Apply(t, () =>
                    {
                        t.rules.Clear();
                        foreach (var rule in pack.rules)
                            if (rule != null) t.rules.Add(rule);
                    }));
                if (GUILayout.Button("전체 해제", EditorStyles.miniButton, GUILayout.Width(70)))
                    PackGui.Later(() => PackGui.Apply(t, () => t.rules.Clear()));
                GUILayout.FlexibleSpace();
            }

            foreach (var rule in pack.rules)
            {
                if (rule == null) continue;
                var r = rule;
                bool on = t.rules.Contains(r);
                bool picked = EditorGUILayout.ToggleLeft($"{PackGui.Name(null, r.id)}  ·  {RuleSummary(r)}", on);
                if (picked != on)
                    PackGui.Later(() => PackGui.Apply(t, () =>
                    {
                        if (picked) { if (!t.rules.Contains(r)) t.rules.Add(r); }
                        else t.rules.RemoveAll(x => x == r);
                    }));
            }

            if (t.rules.Exists(r => r == null) && GUILayout.Button("끊긴 규칙 연결 정리", GUILayout.Width(160)))
                PackGui.Later(() => PackGui.Apply(t, () => t.rules.RemoveAll(r => r == null)));
        }

        static int CountRules(CaseTemplate t)
        {
            int count = 0;
            foreach (var rule in t.rules)
                if (rule != null) count++;
            return count;
        }

        void DrawTimeSlots(CaseTemplate t)
        {
            Header("시간대 (시간 순서대로)");
            PackGui.StringList(t, t.timeSlots, "+ 시간대 추가", false);

            if (t.timeSlots.Count == 0) return;
            var names = new List<string>();
            for (int i = 0; i < t.timeSlots.Count; i++) names.Add($"{i + 1}. {t.timeSlots[i]}");

            EditorGUI.BeginChangeCheck();
            int crime = EditorGUILayout.Popup(PackGui.L("사건이 일어난 시간대"), Mathf.Clamp(t.crimeSlotIndex, 0, names.Count - 1), names.ToArray());
            if (EditorGUI.EndChangeCheck()) PackGui.Later(() => PackGui.Apply(t, () => t.crimeSlotIndex = crime));
        }
    }
}
