using System;
using System.Collections.Generic;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 태그 탭: 한 줄이 태그 하나인 표.
    public partial class PackEditorWindow
    {
        void DrawTagsTab()
        {
            EditorGUILayout.HelpBox(
                "태그는 엔티티에 붙이는 꼬리표입니다. 같은 카테고리의 태그끼리 비교합니다 (예: 카테고리 club 안에 연극부·요리부).",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22);
                GUILayout.Label(PackGui.L("Id", "영어 소문자와 점. 예: club.drama"), EditorStyles.miniBoldLabel, GUILayout.Width(180));
                GUILayout.Label(PackGui.L("표시 이름", "화면에 보일 이름. 예: 연극부"), EditorStyles.miniBoldLabel, GUILayout.Width(160));
                GUILayout.Label(PackGui.L("카테고리", "예: club, floor, trait"), EditorStyles.miniBoldLabel, GUILayout.Width(170));
                GUILayout.Label(PackGui.L("사용", "이 태그가 붙은 엔티티 수"), EditorStyles.miniBoldLabel, GUILayout.Width(40));
            }

            var categories = Categories();
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            foreach (var tag in pack.tags)
            {
                if (tag == null) continue;
                var t = tag;
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture icon = null;
                    string tip = "";
                    if (worstByTarget.TryGetValue(t, out var worst))
                    {
                        icon = worst == ProblemSeverity.Error ? errorIcon : warningIcon;
                        tip = ProblemText(t);
                    }
                    GUILayout.Label(new GUIContent(icon, tip), GUILayout.Width(18), GUILayout.Height(18));

                    PackGui.Text(t, null, t.id, v =>
                    {
                        t.id = v.Trim();
                        PackAssetOps.RenameToId(t, PackAssetOps.TagPrefix, t.id);
                        needsSync = true;
                    }, true, GUILayout.Width(180));
                    PackGui.Text(t, null, t.displayName, v => t.displayName = v, false, GUILayout.Width(160));
                    PackGui.TextWithOptions(t, null, t.category, categories, v => t.category = v.Trim(), GUILayout.Width(145));
                    GUILayout.Label(TagUseCount(t).ToString(), GUILayout.Width(40));

                    if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        int uses = TagUseCount(t);
                        string message = $"태그 '{PackGui.Name(t.displayName, t.id)}'를 지웁니다.";
                        if (uses > 0) message += $"\n엔티티 {uses}개에서 이 태그가 빠집니다.";
                        message += "\n파일은 휴지통으로 이동합니다.";
                        ConfirmLater("태그 삭제", message, () => PackAssetOps.DeleteTag(pack, t));
                    }
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 태그 추가", GUILayout.Width(120), GUILayout.Height(24)))
                    PackGui.Later(() => { PackAssetOps.CreateTag(pack); needsSync = true; });
                GUILayout.FlexibleSpace();
            }
        }

        // 팩의 태그 카테고리 목록 (정렬, 중복 없음).
        List<string> Categories()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var tag in pack.tags)
                if (tag != null && !string.IsNullOrEmpty(tag.category)) set.Add(tag.category);
            return new List<string>(set);
        }

        int TagUseCount(TagDef tag)
        {
            int count = 0;
            foreach (var entity in pack.entities)
                if (entity != null && entity.tags != null && entity.tags.Contains(tag)) count++;
            return count;
        }

        string ProblemText(UnityEngine.Object target)
        {
            var lines = new List<string>();
            foreach (var problem in problems)
                if (problem.Target == target) lines.Add(problem.Message);
            return string.Join("\n", lines);
        }
    }
}
