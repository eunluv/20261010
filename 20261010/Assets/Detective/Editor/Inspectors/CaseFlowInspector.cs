using System;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // [SerializeReference] 목록은 기본 인스펙터에 하위 타입을 고르는 메뉴가 없어서 직접 그린다.
    // "페이즈 추가 ▾"로 종류를 골라 넣고, 줄마다 위/아래 이동과 삭제를 한다.
    [CustomEditor(typeof(CaseFlow))]
    public class CaseFlowInspector : UnityEditor.Editor
    {
        static readonly (string label, Func<PhaseDef> create)[] PhaseTypes =
        {
            ("Dialogue — 대사 장면", () => new DialogueDef()),
            ("Investigate — 조사", () => new InvestigateDef()),
            ("Testimony — 증언과 반박", () => new TestimonyDef()),
            ("Accuse — 지목", () => new AccuseDef()),
            ("Result — 결말과 복기", () => new ResultDef()),
        };

        public override void OnInspectorGUI()
        {
            var flow = (CaseFlow)target;
            serializedObject.Update();
            var phases = serializedObject.FindProperty("phases");

            EditorGUILayout.HelpBox("위에서부터 차례로 실행됩니다. 신뢰도가 0이 되면 남은 페이즈를 건너뛰고 Result로 갑니다.", MessageType.None);

            for (int i = 0; i < phases.arraySize; i++)
            {
                int index = i; // 람다가 나중에 실행되므로 복사해 둔다
                var element = phases.GetArrayElementAtIndex(i);
                var def = i < flow.phases.Count ? flow.phases[i] : null;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{i + 1}. {Title(def)}", EditorStyles.boldLabel);

                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button(new GUIContent("▲", "위로"), EditorStyles.miniButtonLeft, GUILayout.Width(26))) Later(flow, index, f => Swap(f, index, index - 1));
                        using (new EditorGUI.DisabledScope(i == phases.arraySize - 1))
                            if (GUILayout.Button(new GUIContent("▼", "아래로"), EditorStyles.miniButtonMid, GUILayout.Width(26))) Later(flow, index, f => Swap(f, index, index + 1));
                        if (GUILayout.Button(new GUIContent("✕", "삭제"), EditorStyles.miniButtonRight, GUILayout.Width(26))) Later(flow, index, f => f.phases.RemoveAt(index));
                    }

                    if (def == null)
                    {
                        EditorGUILayout.HelpBox("비어 있는 페이즈입니다. 삭제하세요.", MessageType.Warning);
                        continue;
                    }

                    // 이 페이즈 타입의 필드를 그대로 그린다.
                    EditorGUI.indentLevel++;
                    var child = element.Copy();
                    var end = element.GetEndProperty();
                    bool enter = true;
                    while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
                    {
                        EditorGUILayout.PropertyField(child, true);
                        enter = false;
                    }
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            if (EditorGUILayout.DropdownButton(new GUIContent("페이즈 추가 ▾"), FocusType.Keyboard, GUILayout.Height(24)))
            {
                var menu = new GenericMenu();
                foreach (var (label, create) in PhaseTypes)
                {
                    var make = create;
                    menu.AddItem(new GUIContent(label), false, () => Apply(flow, f => f.phases.Add(make())));
                }
                menu.ShowAsContext();
            }
        }

        static string Title(PhaseDef def)
        {
            switch (def)
            {
                case DialogueDef d: return $"Dialogue  ·  {d.speaker}, {d.lines.Count}줄";
                case InvestigateDef i: return i.overrideActionPoints ? $"Investigate  ·  행동력 {i.actionPoints}" : "Investigate  ·  템플릿 행동력";
                case TestimonyDef t: return $"Testimony  ·  {t.speaker}" + (t.mustRebut ? ", 반박 필수" : "");
                case AccuseDef a: return "Accuse  ·  " + (a.axisIds.Count == 0 ? "모든 축" : string.Join(", ", a.axisIds));
                case ResultDef _: return "Result";
                default: return "(없음)";
            }
        }

        static void Swap(CaseFlow flow, int a, int b)
        {
            if (a < 0 || b < 0 || a >= flow.phases.Count || b >= flow.phases.Count) return;
            var tmp = flow.phases[a];
            flow.phases[a] = flow.phases[b];
            flow.phases[b] = tmp;
        }

        // 목록 구조를 바꾸는 일은 OnGUI 밖에서 한다.
        void Later(CaseFlow flow, int index, Action<CaseFlow> change)
        {
            EditorApplication.delayCall += () =>
            {
                if (flow == null || index >= flow.phases.Count) return;
                Apply(flow, change);
            };
        }

        void Apply(CaseFlow flow, Action<CaseFlow> change)
        {
            if (flow == null) return;
            Undo.RecordObject(flow, "흐름 편집");
            change(flow);
            EditorUtility.SetDirty(flow);
            Repaint();
        }
    }
}
