using System;
using System.Collections.Generic;
using Detective.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Detective.Editor
{
    // 팩 하나의 태그·엔티티·단서 규칙·사건 템플릿을 한 창에서 만들고 고치고 지운다.
    // 에셋 파일은 팩 폴더 아래에 자동으로 만들어지고, WorldPack의 목록은 폴더 내용으로 자동으로 채워진다.
    public partial class PackEditorWindow : EditorWindow
    {
        const float ListWidth = 250f;

        [SerializeField] WorldPack pack;
        [SerializeField] int tab;
        [SerializeField] EntityDef selectedEntity;
        [SerializeField] ClueRule selectedRule;
        [SerializeField] CaseTemplate selectedTemplate;
        [SerializeField] int entityFilter;
        [SerializeField] bool showProblems = true;

        Vector2 listScroll, detailScroll, problemScroll;
        List<WorldPack> allPacks;
        List<PackProblem> problems = new List<PackProblem>();
        readonly Dictionary<Object, ProblemSeverity> worstByTarget = new Dictionary<Object, ProblemSeverity>();
        int errorCount, warningCount;
        bool needsSync = true, needsValidate = true;
        bool creatingPack;
        string newPackName = "";

        GUIStyle listItemStyle, problemStyle, summaryStyle;
        Texture errorIcon, warningIcon;

        [MenuItem("Detective/Pack Editor")]
        public static void Open()
        {
            var window = GetWindow<PackEditorWindow>();
            window.titleContent = new GUIContent("Pack Editor");
            window.minSize = new Vector2(760f, 480f);
            window.Show();
        }

        void OnEnable()
        {
            PackGui.OnChanged = MarkChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            allPacks = null;
            needsSync = true;
            needsValidate = true;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnDestroy()
        {
            AssetDatabase.SaveAssets();
        }

        void OnProjectChange()
        {
            allPacks = null;
            needsSync = true;
            Repaint();
        }

        void OnUndoRedo()
        {
            needsSync = true;
            needsValidate = true;
            Repaint();
        }

        void MarkChanged()
        {
            needsValidate = true;
            Repaint();
        }

        // ---- 그리기 ----

        void OnGUI()
        {
            PackGui.OnChanged = MarkChanged;
            EnsureStyles();
            if (Event.current.type == EventType.Layout) Prepare();

            DrawToolbar();
            if (creatingPack) DrawNewPackRow();

            if (pack == null)
            {
                EditorGUILayout.HelpBox("팩이 없습니다. 위의 '새 팩'으로 만드세요.", MessageType.Info);
                return;
            }
            if (PackAssetOps.FolderOf(pack) == "Assets")
            {
                EditorGUILayout.HelpBox("팩 에셋이 Assets 폴더 바로 아래에 있습니다. Assets/Packs/<팩이름>/ 폴더로 옮겨 주세요.", MessageType.Error);
                return;
            }

            DrawPackHeader();

            string[] tabNames =
            {
                $"태그 ({pack.tags.Count})",
                $"엔티티 ({pack.entities.Count})",
                $"단서 규칙 ({pack.rules.Count})",
                $"사건 템플릿 ({pack.templates.Count})",
            };
            int newTab = GUILayout.Toolbar(tab, tabNames, GUILayout.Height(24));
            if (newTab != tab) PackGui.Later(() => { tab = newTab; listScroll = detailScroll = Vector2.zero; });
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                switch (tab)
                {
                    case 0: DrawTagsTab(); break;
                    case 1: DrawEntitiesTab(); break;
                    case 2: DrawRulesTab(); break;
                    default: DrawTemplatesTab(); break;
                }
            }

            DrawProblems();
        }

        // Layout 이벤트 시작에서만 상태를 바꾼다 (그리는 도중에 컨트롤 수가 달라지지 않게).
        void Prepare()
        {
            PackGui.RunPending();

            if (allPacks == null) allPacks = PackAssetOps.FindAllPacks();
            allPacks.RemoveAll(p => p == null);
            if (pack == null && allPacks.Count > 0)
            {
                pack = allPacks[0];
                needsSync = true;
            }
            if (pack == null) return;

            if (needsSync)
            {
                needsSync = false;
                PackAssetOps.Sync(pack);
                needsValidate = true;
            }
            if (needsValidate)
            {
                needsValidate = false;
                Revalidate();
            }
        }

        void Revalidate()
        {
            problems = PackValidator.Validate(pack);
            worstByTarget.Clear();
            errorCount = warningCount = 0;
            foreach (var problem in problems)
            {
                if (problem.Severity == ProblemSeverity.Error) errorCount++;
                else warningCount++;

                MarkTarget(problem.Target, problem.Severity);
                // 용의자 프로필의 문제는 그 용의자 줄에도 표시한다.
                if (problem.Target is SuspectProfile profile && profile.entity != null) MarkTarget(profile.entity, problem.Severity);
            }
        }

        void MarkTarget(Object target, ProblemSeverity severity)
        {
            if (target == null) return;
            if (!worstByTarget.TryGetValue(target, out var worst) || severity == ProblemSeverity.Error) worstByTarget[target] = severity;
        }

        void EnsureStyles()
        {
            if (listItemStyle != null) return;
            listItemStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fixedHeight = 22f };
            problemStyle = new GUIStyle(EditorStyles.label) { wordWrap = false, fixedHeight = 18f };
            summaryStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };
            errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml").image;
            warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image;
        }

        // ---- 위쪽 도구 막대 ----

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("팩", GUILayout.Width(22));

                var packs = allPacks ?? new List<WorldPack>();
                var names = new string[packs.Count];
                for (int i = 0; i < packs.Count; i++)
                    names[i] = packs[i] == null ? "?" : PackGui.Name(packs[i].displayName, packs[i].name);
                int index = packs.IndexOf(pack);
                int picked = EditorGUILayout.Popup(index, names, EditorStyles.toolbarPopup, GUILayout.Width(220));
                if (picked != index && picked >= 0)
                {
                    var next = packs[picked];
                    PackGui.Later(() => SelectPack(next));
                }

                if (GUILayout.Button("새 팩", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    PackGui.Later(() => { creatingPack = true; newPackName = ""; });

                GUILayout.FlexibleSpace();

                if (pack != null && GUILayout.Button(PackGui.L("폴더 보기", "Project 창에서 이 팩의 폴더를 보여 줍니다"), EditorStyles.toolbarButton))
                    EditorGUIUtility.PingObject(pack);
                if (GUILayout.Button(PackGui.L("다시 읽기", "팩 폴더를 다시 훑어 목록을 갱신합니다"), EditorStyles.toolbarButton))
                    PackGui.Later(() => { allPacks = null; needsSync = true; });
                if (GUILayout.Button(PackGui.L("저장", "바뀐 에셋을 디스크에 저장합니다"), EditorStyles.toolbarButton))
                    EditorApplication.delayCall += AssetDatabase.SaveAssets;
            }
        }

        void SelectPack(WorldPack next)
        {
            pack = next;
            selectedEntity = null;
            selectedRule = null;
            selectedTemplate = null;
            listScroll = detailScroll = Vector2.zero;
            needsSync = true;
        }

        void DrawNewPackRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PrefixLabel("새 팩 이름");
                newPackName = EditorGUILayout.TextField(newPackName);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newPackName)))
                {
                    if (GUILayout.Button("만들기", GUILayout.Width(70)))
                    {
                        string name = newPackName.Trim();
                        EditorApplication.delayCall += () => CreatePack(name);
                    }
                }
                if (GUILayout.Button("취소", GUILayout.Width(50))) PackGui.Later(() => creatingPack = false);
            }
        }

        void CreatePack(string name)
        {
            var created = PackAssetOps.CreatePack(name);
            if (created == null)
            {
                EditorUtility.DisplayDialog("새 팩", $"'{name}' 팩을 만들 수 없습니다. 같은 이름의 폴더에 이미 팩이 있거나 이름을 쓸 수 없습니다.", "확인");
                return;
            }
            creatingPack = false;
            allPacks = null;
            SelectPack(created);
            Repaint();
        }

        void DrawPackHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                PackGui.Text(pack, PackGui.L("팩 이름", "게임에 보일 팩 이름. 예: 학원 축제"), pack.displayName, v => pack.displayName = v);
                EditorGUILayout.LabelField(PackAssetOps.FolderOf(pack), EditorStyles.miniLabel, GUILayout.Width(260));
            }
            EditorGUILayout.Space(2);
        }

        // ---- 아래쪽 문제 목록 ----

        void DrawProblems()
        {
            EditorGUILayout.Space(4);
            string title = problems.Count == 0 ? "문제 없음" : $"문제: 오류 {errorCount}개 · 경고 {warningCount}개";
            bool show = EditorGUILayout.Foldout(showProblems, title, true);
            if (show != showProblems) PackGui.Later(() => showProblems = show);
            if (!showProblems || problems.Count == 0) return;

            problemScroll = EditorGUILayout.BeginScrollView(problemScroll, GUILayout.Height(Mathf.Min(150f, problems.Count * 20f + 6f)));
            foreach (var problem in problems)
            {
                var content = new GUIContent(" " + problem.Message, problem.Severity == ProblemSeverity.Error ? errorIcon : warningIcon,
                    "누르면 해당 항목으로 이동합니다");
                if (GUILayout.Button(content, problemStyle))
                {
                    var target = problem.Target;
                    PackGui.Later(() => GoTo(target));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void GoTo(Object target)
        {
            detailScroll = Vector2.zero;
            switch (target)
            {
                case TagDef _:
                    tab = 0;
                    break;
                case EntityDef entity:
                    tab = 1;
                    entityFilter = 0;
                    selectedEntity = entity;
                    break;
                case SuspectProfile profile:
                    tab = 1;
                    entityFilter = 0;
                    selectedEntity = profile.entity;
                    break;
                case ClueRule rule:
                    tab = 2;
                    selectedRule = rule;
                    break;
                case CaseTemplate template:
                    tab = 3;
                    selectedTemplate = template;
                    break;
                default:
                    if (target != null) EditorGUIUtility.PingObject(target);
                    break;
            }
        }

        // ---- 탭 공통 ----

        // 왼쪽 목록의 한 줄. 문제가 있으면 아이콘을 붙인다.
        void ListItem(Object target, bool selected, string text, Action select)
        {
            Texture icon = null;
            if (worstByTarget.TryGetValue(target, out var worst)) icon = worst == ProblemSeverity.Error ? errorIcon : warningIcon;

            if (GUILayout.Toggle(selected, new GUIContent(" " + text, icon), listItemStyle) && !selected) PackGui.Later(select);
        }

        // 고른 항목의 문제를 상세 화면 맨 위에 보여 준다.
        void DrawProblemsFor(Object a, Object b = null)
        {
            foreach (var problem in problems)
            {
                if (problem.Target == null || (problem.Target != a && problem.Target != b)) continue;
                EditorGUILayout.HelpBox(problem.Message, problem.Severity == ProblemSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }

        static void Header(string text)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        // 확인 창은 OnGUI 밖에서 띄운다.
        void ConfirmLater(string title, string message, Action onYes)
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorUtility.DisplayDialog(title, message, "삭제", "취소")) return;
                onYes();
                needsSync = true;
                needsValidate = true;
                Repaint();
            };
        }
    }
}
