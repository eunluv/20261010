using System;
using System.Collections.Generic;
using System.Text;
using Detective.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Detective.Editor
{
    // 팩 편집 창의 입력 칸 헬퍼.
    //
    // IMGUI는 한 이벤트를 처리하는 도중에 그려지는 컨트롤 수가 바뀌면 오류를 낸다. 그래서 값 변경은
    // 그 자리에서 적용하지 않고 Later()로 모아 두었다가, 창이 다음 Layout 이벤트 시작에 RunPending()으로 적용한다.
    internal static class PackGui
    {
        public static Action OnChanged;

        static readonly List<Action> pending = new List<Action>();
        static GUIStyle wrapArea;

        static GUIStyle WrapArea => wrapArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        public static GUIContent L(string text, string tooltip = null) => new GUIContent(text, tooltip);

        // ---- 변경 적용 ----

        // 에셋을 바로 고친다 (Undo 기록 + 저장 대상 표시). OnGUI 밖(메뉴 콜백 등)이나 RunPending 안에서만 직접 부른다.
        public static void Apply(Object target, Action change)
        {
            if (target == null) return;
            Undo.RecordObject(target, "팩 편집");
            change();
            EditorUtility.SetDirty(target);
            OnChanged?.Invoke();
        }

        public static void Later(Action action)
        {
            pending.Add(action);
            OnChanged?.Invoke();
        }

        static void Set(Object target, Action change) => Later(() => Apply(target, change));

        public static void RunPending()
        {
            if (pending.Count == 0) return;
            var actions = pending.ToArray();
            pending.Clear();
            foreach (var action in actions) action();
        }

        // ---- 기본 칸 (label이 null이면 라벨 없이 그린다) ----

        public static void Text(Object target, GUIContent label, string value, Action<string> set, bool delayed = false,
            params GUILayoutOption[] options)
        {
            EditorGUI.BeginChangeCheck();
            string v;
            if (label == null)
                v = delayed ? EditorGUILayout.DelayedTextField(value ?? "", options) : EditorGUILayout.TextField(value ?? "", options);
            else
                v = delayed ? EditorGUILayout.DelayedTextField(label, value ?? "", options) : EditorGUILayout.TextField(label, value ?? "", options);
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(v));
        }

        public static void Area(Object target, GUIContent label, string value, Action<string> set, int minLines = 2)
        {
            if (label != null) EditorGUILayout.LabelField(label);
            EditorGUI.BeginChangeCheck();
            string v = EditorGUILayout.TextArea(value ?? "", WrapArea, GUILayout.MinHeight(minLines * 16 + 6));
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(v));
        }

        public static void Int(Object target, GUIContent label, int value, int min, Action<int> set, params GUILayoutOption[] options)
        {
            EditorGUI.BeginChangeCheck();
            int v = label == null ? EditorGUILayout.IntField(value, options) : EditorGUILayout.IntField(label, value, options);
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(Mathf.Max(min, v)));
        }

        public static void Float(Object target, GUIContent label, float value, float min, Action<float> set)
        {
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.FloatField(label, value);
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(Mathf.Max(min, v)));
        }

        public static void Toggle(Object target, GUIContent label, bool value, Action<bool> set)
        {
            EditorGUI.BeginChangeCheck();
            bool v = EditorGUILayout.Toggle(label, value);
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(v));
        }

        public static void EnumField<T>(Object target, GUIContent label, T value, Action<T> set, params GUILayoutOption[] options)
            where T : Enum
        {
            EditorGUI.BeginChangeCheck();
            Enum raw = label == null ? EditorGUILayout.EnumPopup(value, options) : EditorGUILayout.EnumPopup(label, value, options);
            if (EditorGUI.EndChangeCheck())
            {
                var v = (T)(object)raw;
                Set(target, () => set(v));
            }
        }

        public static void FlagsField<T>(Object target, GUIContent label, T value, Action<T> set) where T : Enum
        {
            EditorGUI.BeginChangeCheck();
            Enum raw = EditorGUILayout.EnumFlagsField(label, value);
            if (EditorGUI.EndChangeCheck())
            {
                var v = (T)(object)raw;
                Set(target, () => set(v));
            }
        }

        public static void ObjectField<T>(Object target, GUIContent label, T value, Action<T> set) where T : Object
        {
            EditorGUI.BeginChangeCheck();
            var v = (T)EditorGUILayout.ObjectField(label, value, typeof(T), false);
            if (EditorGUI.EndChangeCheck()) Set(target, () => set(v));
        }

        // ---- 목록에서 고르는 칸 ----

        // 직접 입력도 되고, ▾ 버튼으로 기존 값 중에서 고를 수도 있는 텍스트 칸.
        public static void TextWithOptions(Object target, GUIContent label, string value, IList<string> choices, Action<string> set,
            params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Text(target, label, value, set, false, options);
                if (GUILayout.Button("▾", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    var menu = new GenericMenu();
                    if (choices.Count == 0) menu.AddDisabledItem(new GUIContent("(고를 값이 없습니다)"));
                    foreach (var choice in choices)
                    {
                        string picked = choice;
                        menu.AddItem(new GUIContent(picked), picked == value, () => Apply(target, () => set(picked)));
                    }
                    menu.ShowAsContext();
                }
            }
        }

        // 태그 여러 개를 체크해서 고르는 칸. 버튼을 누르면 카테고리별 메뉴가 열린다.
        public static void TagList(Object target, GUIContent label, List<TagDef> list, IReadOnlyList<TagDef> allTags,
            params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != null) EditorGUILayout.PrefixLabel(label);

                if (GUILayout.Button(new GUIContent(TagSummary(list), TagSummary(list)), EditorStyles.popup, options))
                {
                    var menu = new GenericMenu();
                    if (allTags.Count == 0) menu.AddDisabledItem(new GUIContent("(태그 탭에서 먼저 태그를 만드세요)"));
                    foreach (var tag in allTags)
                    {
                        if (tag == null) continue;
                        var picked = tag;
                        bool on = list.Contains(picked);
                        menu.AddItem(new GUIContent(TagMenuPath(picked)), on, () => Apply(target, () =>
                        {
                            if (on) list.RemoveAll(t => t == picked);
                            else list.Add(picked);
                        }));
                    }
                    if (list.Exists(t => t == null))
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("끊긴 연결 정리"), false, () => Apply(target, () => list.RemoveAll(t => t == null)));
                    }
                    menu.ShowAsContext();
                }
            }
        }

        // 태그 하나를 고르는 칸.
        public static void TagPopup(Object target, GUIContent label, TagDef value, IReadOnlyList<TagDef> allTags, Action<TagDef> set)
        {
            var names = new List<string> { "(없음)" };
            var values = new List<TagDef> { null };
            foreach (var tag in allTags)
            {
                if (tag == null) continue;
                names.Add(TagMenuPath(tag));
                values.Add(tag);
            }
            Popup(target, label, value, names, values, set);
        }

        // 엔티티 하나를 고르는 칸.
        public static void EntityPopup(Object target, GUIContent label, EntityDef value, IReadOnlyList<EntityDef> choices, Action<EntityDef> set)
        {
            var names = new List<string> { "(없음)" };
            var values = new List<EntityDef> { null };
            foreach (var entity in choices)
            {
                if (entity == null) continue;
                names.Add(MenuSafe(Name(entity.displayName, entity.id)) + "  (" + MenuSafe(entity.id) + ")");
                values.Add(entity);
            }
            Popup(target, label, value, names, values, set);
        }

        static void Popup<T>(Object target, GUIContent label, T value, List<string> names, List<T> values, Action<T> set) where T : Object
        {
            int index = values.IndexOf(value);
            if (index < 0)
            {
                // 목록에 없는 값(다른 팩의 에셋, 종류가 안 맞는 엔티티)도 지금 값은 보여 준다.
                names.Add("⚠ " + (value != null ? value.name : "?"));
                values.Add(value);
                index = values.Count - 1;
            }

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(label, index, names.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                var v = values[picked];
                Set(target, () => set(v));
            }
        }

        // ---- 문자열 목록 ----

        public static void StringList(Object target, List<string> list, string addLabel, bool multiline)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((i + 1).ToString(), GUILayout.Width(18));
                    EditorGUI.BeginChangeCheck();
                    string v = multiline
                        ? EditorGUILayout.TextArea(list[i] ?? "", WrapArea, GUILayout.MinHeight(34))
                        : EditorGUILayout.TextField(list[i] ?? "");
                    if (EditorGUI.EndChangeCheck()) Set(target, () => { if (index < list.Count) list[index] = v; });
                    if (GUILayout.Button(L("×", "이 줄 지우기"), GUILayout.Width(22)))
                        Set(target, () => { if (index < list.Count) list.RemoveAt(index); });
                }
            }
            if (GUILayout.Button(addLabel, GUILayout.Width(140))) Set(target, () => list.Add(""));
        }

        // ---- 표시용 문자열 ----

        public static string Name(string displayName, string id)
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return string.IsNullOrEmpty(id) ? "(이름 없음)" : id;
        }

        static string TagSummary(List<TagDef> list)
        {
            if (list == null || list.Count == 0) return "(없음)";
            var sb = new StringBuilder();
            foreach (var tag in list)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(tag == null ? "⚠ 끊김" : Name(tag.displayName, tag.id));
            }
            return sb.ToString();
        }

        static string TagMenuPath(TagDef tag)
        {
            string category = string.IsNullOrEmpty(tag.category) ? "(카테고리 없음)" : MenuSafe(tag.category);
            return category + "/" + MenuSafe(Name(tag.displayName, tag.id)) + "  (" + MenuSafe(tag.id) + ")";
        }

        // 메뉴에서 '/'는 하위 메뉴 구분자라서 이름에 들어 있으면 바꿔 준다.
        static string MenuSafe(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace('/', '∕');
    }
}
