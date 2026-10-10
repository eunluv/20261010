using System.Collections.Generic;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // WorldPack 인스펙터에 "검증" 버튼을 붙인다 (tool-design 8-1).
    // 결과 항목을 누르면 문제가 있는 에셋을 Project 창에서 보여 준다.
    [CustomEditor(typeof(WorldPack))]
    public class WorldPackInspector : UnityEditor.Editor
    {
        List<PackProblem> problems;

        public override void OnInspectorGUI()
        {
            var pack = (WorldPack)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("검증", "빠진 값, 끊긴 연결, 후보 부족, 쓸 수 없는 규칙, 문장 변수를 검사합니다"), GUILayout.Height(28)))
                    EditorApplication.delayCall += () => Validate(pack);
                if (GUILayout.Button(new GUIContent("Pack Editor 열기", "이 팩을 한 창에서 편집합니다"), GUILayout.Height(28)))
                    EditorApplication.delayCall += PackEditorWindow.Open;
            }

            DrawProblems();

            EditorGUILayout.Space(8);
            DrawDefaultInspector();
        }

        void Validate(WorldPack pack)
        {
            if (pack == null) return;
            PackAssetOps.Sync(pack); // 폴더 내용으로 목록을 먼저 맞춘다
            problems = PackValidator.Validate(pack);
            Repaint();
        }

        void DrawProblems()
        {
            if (problems == null) return;

            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("문제가 없습니다.", MessageType.Info);
                return;
            }

            int errors = 0;
            foreach (var problem in problems)
                if (problem.Severity == ProblemSeverity.Error) errors++;
            EditorGUILayout.LabelField($"오류 {errors}개 · 경고 {problems.Count - errors}개  (항목을 누르면 해당 에셋을 보여 줍니다)", EditorStyles.miniLabel);

            foreach (var problem in problems)
            {
                EditorGUILayout.HelpBox(problem.Message, problem.Severity == ProblemSeverity.Error ? MessageType.Error : MessageType.Warning);

                // HelpBox 위에 투명한 버튼을 겹쳐서 누를 수 있게 한다.
                var rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && problem.Target != null)
                    EditorGUIUtility.PingObject(problem.Target);
            }
        }
    }
}
