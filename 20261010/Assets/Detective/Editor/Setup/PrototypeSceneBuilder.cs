using System.IO;
using Detective.Data;
using Detective.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Detective.Editor
{
    // 프로토타입 씬(Assets/Scenes/Prototype.unity)을 만들고 PrototypeGameUI 오브젝트를 넣는다.
    public static class PrototypeSceneBuilder
    {
        [MenuItem("Detective/Setup/Create Prototype Scene")]
        static void CreateMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("프로토타입 씬", "플레이 모드를 끝낸 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (File.Exists(PrototypeLaunch.ScenePath) &&
                !EditorUtility.DisplayDialog("프로토타입 씬", $"{PrototypeLaunch.ScenePath}가 이미 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Create();
        }

        // 확인 창 없이 만든다 (배치 모드: -executeMethod Detective.Editor.PrototypeSceneBuilder.Create).
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 화면은 전부 OnGUI로 그리므로 카메라는 어두운 배경만 칠한다.
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            }

            var go = new GameObject("PrototypeGame");
            var ui = go.AddComponent<PrototypeGameUI>();

            // 학원 축제 팩이 있으면 그것을, 없으면 처음 찾은 팩을 연결한다.
            WorldPack pack = null;
            foreach (var candidate in PackAssetOps.FindAllPacks())
            {
                if (pack == null) pack = candidate;
                if (AssetDatabase.GetAssetPath(candidate).Contains("/SchoolFestival/")) pack = candidate;
            }
            if (pack != null)
            {
                PackAssetOps.Sync(pack);
                ui.pack = pack;
                ui.template = pack.templates.Find(t => t != null);
            }

            EditorSceneManager.SaveScene(scene, PrototypeLaunch.ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[프로토타입 씬] {PrototypeLaunch.ScenePath} 를 만들었습니다. " +
                      (pack != null
                          ? $"팩 '{pack.displayName}', 템플릿 '{(ui.template != null ? ui.template.title : "없음")}' 연결됨. 플레이 버튼을 누르면 시작합니다."
                          : "팩을 찾지 못했습니다. PrototypeGame 오브젝트의 Pack 칸에 WorldPack을 넣으세요."));
        }
    }
}
