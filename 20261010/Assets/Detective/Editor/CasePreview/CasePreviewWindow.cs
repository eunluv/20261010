using System;
using System.Collections.Generic;
using System.Diagnostics;
using Detective.Core.Logic;
using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 시드를 넣으면 생성된 사건(정답, 축별 후보, 단서)을 바로 보여 주는 창 (tool-design 8-2, 1차 버전).
    public class CasePreviewWindow : EditorWindow
    {
        static readonly string[] DifficultyNames = { "템플릿 값 사용", "Easy", "Normal", "Hard" };

        [SerializeField] WorldPack pack;
        [SerializeField] CaseTemplate template;
        [SerializeField] int seed = 1;
        [SerializeField] int difficultyChoice; // 0 = 템플릿 값, 1~3 = Easy/Normal/Hard
        [SerializeField] bool showBulk = true;
        [SerializeField] int bulkStartSeed;
        [SerializeField] int bulkCount = 1000;

        Vector2 scroll;
        CaseBatchReport report;
        CaseInstance instance;
        double elapsedMs;
        readonly List<string> errors = new List<string>();
        GUIStyle wrapLabel, clueBox, miniWrap;

        [MenuItem("Detective/Case Preview")]
        public static void Open()
        {
            var window = GetWindow<CasePreviewWindow>();
            window.titleContent = new GUIContent("Case Preview");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        void OnEnable()
        {
            if (pack != null) return;
            var packs = PackAssetOps.FindAllPacks();
            if (packs.Count > 0) pack = packs[0];
        }

        // 상태를 바꾸는 일은 OnGUI 밖으로 미룬다 (그리는 도중에 컨트롤 수가 달라지지 않게).
        void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                action();
                Repaint();
            };
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawInputs();
            DrawBulkInputs();

            EditorGUILayout.Space(4);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (report != null) DrawReport(report);
            if (instance != null) DrawInstance(instance);
            else if (errors.Count == 0 && report == null)
                EditorGUILayout.HelpBox("팩과 사건 템플릿을 고르고 '생성'을 누르세요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        void EnsureStyles()
        {
            if (wrapLabel != null) return;
            wrapLabel = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 13 };
            miniWrap = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            clueBox = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 6) };
        }

        // ---- 입력 ----

        void DrawInputs()
        {
            EditorGUI.BeginChangeCheck();
            var newPack = (WorldPack)EditorGUILayout.ObjectField(new GUIContent("팩"), pack, typeof(WorldPack), false);
            if (EditorGUI.EndChangeCheck()) Defer(() => { pack = newPack; template = null; instance = null; errors.Clear(); });

            var templates = new List<CaseTemplate>();
            if (pack != null)
            {
                foreach (var t in pack.templates)
                    if (t != null) templates.Add(t);
            }

            var names = new string[templates.Count];
            for (int i = 0; i < names.Length; i++) names[i] = PackGui.Name(templates[i].title, templates[i].id);
            int index = templates.IndexOf(template);
            using (new EditorGUI.DisabledScope(templates.Count == 0))
            {
                int picked = EditorGUILayout.Popup(new GUIContent("사건 템플릿"), index, names);
                if (picked != index && picked >= 0)
                {
                    var next = templates[picked];
                    Defer(() => { template = next; instance = null; errors.Clear(); });
                }
            }

            seed = EditorGUILayout.IntField(new GUIContent("시드", "같은 시드는 항상 같은 사건을 만듭니다"), seed);
            difficultyChoice = EditorGUILayout.Popup(new GUIContent("난이도", "템플릿에 적힌 난이도 대신 쓸 값"), difficultyChoice, DifficultyNames);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(pack == null))
                {
                    if (GUILayout.Button("생성", GUILayout.Height(26))) Defer(Generate);
                    if (GUILayout.Button(new GUIContent("다음 시드", "시드를 1 올리고 다시 생성합니다"), GUILayout.Height(26)))
                        Defer(() => { seed = unchecked(seed + 1); Generate(); });
                }
                using (new EditorGUI.DisabledScope(instance == null))
                {
                    if (GUILayout.Button(new GUIContent("JSON 복사", "버그 보고용으로 사건 전체를 클립보드에 복사합니다"), GUILayout.Height(26)))
                        Defer(CopyJson);
                }
                using (new EditorGUI.DisabledScope(pack == null || EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("이 시드로 플레이", "프로토타입 씬을 열고 지금 설정(팩·템플릿·시드·난이도)으로 플레이 모드에 들어갑니다"), GUILayout.Height(26)))
                        Defer(PlayThisSeed);
                }
            }
        }

        // 프로토타입 씬을 열고, 설정을 EditorPrefs로 넘긴 뒤 플레이 모드로 들어간다.
        void PlayThisSeed()
        {
            if (pack == null || EditorApplication.isPlaying) return;

            if (!System.IO.File.Exists(Detective.Runtime.PrototypeLaunch.ScenePath))
            {
                EditorUtility.DisplayDialog("이 시드로 플레이",
                    "프로토타입 씬이 없습니다. 메뉴 Detective > Setup > Create Prototype Scene 을 먼저 실행하세요.", "확인");
                return;
            }

            PackAssetOps.Sync(pack);
            if (template == null || !pack.templates.Contains(template)) template = pack.templates.Find(t => t != null);
            if (template == null)
            {
                EditorUtility.DisplayDialog("이 시드로 플레이", "이 팩에 사건 템플릿이 없습니다.", "확인");
                return;
            }
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();

            EditorPrefs.SetBool(Detective.Runtime.PrototypeLaunch.PendingKey, true);
            EditorPrefs.SetInt(Detective.Runtime.PrototypeLaunch.SeedKey, seed);
            EditorPrefs.SetInt(Detective.Runtime.PrototypeLaunch.DifficultyKey, difficultyChoice);
            EditorPrefs.SetString(Detective.Runtime.PrototypeLaunch.PackGuidKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(pack)));
            EditorPrefs.SetString(Detective.Runtime.PrototypeLaunch.TemplateGuidKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template)));

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Detective.Runtime.PrototypeLaunch.ScenePath);
            EditorApplication.isPlaying = true;
        }

        // 고른 팩과 템플릿을 Core 데이터로 바꾼다. 못 바꾸면 errors에 이유를 적고 null.
        CaseTemplateData ConvertSelected()
        {
            errors.Clear();
            GUI.FocusControl(null);

            if (pack == null) { errors.Add("팩을 고르세요."); return null; }
            PackAssetOps.Sync(pack);
            if (template == null || !pack.templates.Contains(template))
                template = pack.templates.Find(t => t != null);
            if (template == null) { errors.Add("이 팩에 사건 템플릿이 없습니다. Pack Editor에서 먼저 만드세요."); return null; }

            try
            {
                return PackConverter.Convert(template, pack);
            }
            catch (PackConversionException e)
            {
                errors.Add("팩 데이터에 오류가 있어 변환하지 못했습니다. Pack Editor의 문제 목록을 확인하세요.");
                errors.AddRange(e.Errors);
                return null;
            }
        }

        Difficulty? SelectedDifficulty() =>
            difficultyChoice == 0 ? (Difficulty?)null : (Difficulty)(difficultyChoice - 1);

        void Generate()
        {
            instance = null;
            var data = ConvertSelected();
            if (data == null) return;

            Difficulty? difficulty = SelectedDifficulty();

            var watch = Stopwatch.StartNew();
            instance = CaseGenerator.Generate(data, seed, difficulty);
            watch.Stop();
            elapsedMs = watch.Elapsed.TotalMilliseconds;

            if (instance == null)
                errors.Add($"{CaseGenerator.MaxAttempts}번 시도했지만 정답을 하나로 좁히는 사건을 만들지 못했습니다. " +
                           "축의 후보 수가 '뽑을 수'보다 적거나, 어떤 축을 좁힐 단서 규칙이 없을 수 있습니다. Pack Editor의 문제 목록을 확인하세요.");
        }

        // ---- 대량 테스트 ----

        void DrawBulkInputs()
        {
            EditorGUILayout.Space(4);
            bool show = EditorGUILayout.Foldout(showBulk, "대량 테스트", true);
            if (show != showBulk) Defer(() => showBulk = show);
            if (!showBulk) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 70;
                bulkStartSeed = EditorGUILayout.IntField(new GUIContent("시작 시드"), bulkStartSeed);
                bulkCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("개수"), bulkCount), 1, 100000);
                EditorGUIUtility.labelWidth = 0;

                using (new EditorGUI.DisabledScope(pack == null))
                {
                    if (GUILayout.Button(new GUIContent("실행", "위에서 고른 사건 템플릿과 난이도로 시드를 연속으로 돌립니다"), GUILayout.Width(80)))
                        Defer(RunBulk);
                }
                using (new EditorGUI.DisabledScope(report == null))
                {
                    if (GUILayout.Button("결과 지우기", GUILayout.Width(80))) Defer(() => report = null);
                }
            }
        }

        void RunBulk()
        {
            report = null;
            var data = ConvertSelected();
            if (data == null) return;

            try
            {
                EditorUtility.DisplayProgressBar("대량 테스트", $"시드 {bulkCount}개를 돌리는 중…", 0.5f);
                report = CaseBatch.Run(data, bulkStartSeed, bulkCount, SelectedDifficulty());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void DrawReport(CaseBatchReport r)
        {
            Section($"대량 테스트 결과 — {r.TemplateId}, 시드 {r.StartSeed}부터 {r.Count}개, 난이도 {r.Difficulty}");

            int failedCount = r.Count - r.Succeeded;
            EditorGUILayout.LabelField("성공률", $"{r.SuccessRate * 100.0:0.0}%  ({r.Succeeded}/{r.Count})");
            EditorGUILayout.LabelField("평균 시도 횟수", $"{r.AverageAttempts:0.00}회");
            EditorGUILayout.LabelField("단서 수 (가짜 제외)", $"평균 {r.AverageClues:0.0}개 · 최대 {r.MaxClues}개");
            EditorGUILayout.LabelField("필요 조사 행동", $"평균 {r.AverageRequiredActions:0.0}회 · 최대 {r.MaxRequiredActions}회");
            EditorGUILayout.LabelField("생성 시간", $"평균 {r.AverageMs:0.000} ms · 최대 {r.MaxMs:0.000} ms");

            if (r.Unsolvable > 0)
                EditorGUILayout.HelpBox($"조건을 어긴 사건이 {r.Unsolvable}개 나왔습니다 (생성기 버그).\n{string.Join("\n", r.Violations)}", MessageType.Error);
            if (failedCount > 0)
                EditorGUILayout.HelpBox($"생성에 실패한 시드가 {failedCount}개 있습니다. 예: {string.Join(", ", r.FailedSeeds)}", MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("규칙별 사용 횟수 (진짜 단서 / 가짜 단서)", EditorStyles.miniBoldLabel);

            int max = 1;
            foreach (var usage in r.RuleUsages) max = Mathf.Max(max, usage.RealUses);

            foreach (var usage in r.RuleUsages)
            {
                bool unused = usage.RealUses == 0 && usage.HerringUses == 0;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var old = GUI.color;
                    if (unused) GUI.color = new Color(1f, 0.55f, 0.45f);
                    GUILayout.Label((unused ? "⚠ " : "") + usage.RuleId, GUILayout.Width(170));
                    GUILayout.Label(unused ? "한 번도 안 쓰임" : $"{usage.RealUses} / {usage.HerringUses}", GUILayout.Width(110));
                    GUI.color = old;

                    var bar = GUILayoutUtility.GetRect(60f, 16f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(bar, (float)usage.RealUses / max, "");
                }
            }
            EditorGUILayout.Space(8);
        }

        // ---- 출력 ----

        void DrawInstance(CaseInstance c)
        {
            int real = 0, herrings = 0;
            foreach (var clue in c.Clues)
            {
                if (clue.IsRedHerring) herrings++;
                else real++;
            }

            EditorGUILayout.LabelField(
                $"시드 {c.Seed} · 시도 {c.Attempts}회 (실제 시드 {c.UsedSeed}) · 생성 {elapsedMs:0.00} ms · 난이도 {c.Difficulty} · 정답 후보 {c.Space.CombinationCount}개",
                miniWrap);

            Section("정답");
            for (int a = 0; a < c.Space.AxisCount; a++)
                EditorGUILayout.LabelField(c.Space.AxisId(a), $"{c.DisplayName(a, c.TruthOption(a))}  ({c.TruthOptionId(a)})");

            Section("축별 후보");
            for (int a = 0; a < c.Space.AxisCount; a++)
            {
                var parts = new List<string>();
                for (int o = 0; o < c.Space.OptionCount(a); o++)
                    parts.Add((o == c.TruthOption(a) ? "★ " : "") + c.DisplayName(a, o));
                EditorGUILayout.LabelField($"{c.Space.AxisId(a)} ({parts.Count})", string.Join(",  ", parts), wrapLabel);
            }

            Section($"단서 {real}개" + (herrings > 0 ? $" + 가짜 {herrings}개" : ""));
            for (int i = 0; i < c.Clues.Count; i++)
            {
                var clue = c.Clues[i];
                using (new EditorGUILayout.VerticalScope(clueBox))
                {
                    string text = string.IsNullOrEmpty(clue.Text) ? "(문장 없음)" : clue.Text;
                    EditorGUILayout.LabelField($"{i + 1}. {(clue.IsRedHerring ? "[가짜] " : "")}{text}", wrapLabel);
                    string effect = clue.IsRedHerring
                        ? $"필요 없는 단서 (혼자서는 {clue.EliminatedAlone}개 지움)"
                        : $"후보 {clue.Eliminated}개 지움 (혼자서는 {clue.EliminatedAlone}개)";
                    if (!clue.IsRedHerring && !clue.IsEssential) effect += " · 보조(없어도 풀림)";
                    EditorGUILayout.LabelField($"{clue.DebugText}   ·   규칙 {clue.RuleId}   ·   {effect}", miniWrap);

                    var old = GUI.color;
                    if (clue.IsRebuttalReward) GUI.color = RewardColor;
                    EditorGUILayout.LabelField("획득: " + PlacementText(c, false, i), miniWrap);
                    GUI.color = old;
                }
            }

            EditorGUILayout.LabelField(
                $"해결에 꼭 필요한 조사 행동: {c.RequiredActions}회  (행동력 {c.Template.ActionPoints} - 여유값 {c.Template.ActionMargin} = {c.Template.ActionPoints - c.Template.ActionMargin}회 이내)",
                miniWrap);

            if (c.Timeline != null)
            {
                DrawTimeline(c);
                DrawTestimonies(c);
                DrawEvidence(c);
            }
            else
            {
                EditorGUILayout.HelpBox("이 템플릿에는 용의자 축·장소 축·시간대 중 빠진 것이 있어 타임라인과 증언이 없습니다.", MessageType.Info);
            }
            EditorGUILayout.Space(8);
        }

        // ---- 타임라인·증언·증거 ----

        static readonly Color LieColor = new Color(1f, 0.45f, 0.4f);
        static readonly Color HiddenColor = new Color(0.62f, 0.62f, 0.62f);
        static readonly Color ExaggeratedColor = new Color(1f, 0.88f, 0.35f);
        static readonly Color RewardColor = new Color(0.55f, 0.85f, 1f);

        void DrawTimeline(CaseInstance c)
        {
            var t = c.Timeline;
            int culprit = c.TruthOption(t.SuspectAxis);
            int crimePlace = c.TruthOption(t.PlaceAxis);

            Section("타임라인 (시간대 × 용의자)");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(70));
                for (int s = 0; s < t.SuspectCount; s++)
                    GUILayout.Label((s == culprit ? "★ " : "") + c.DisplayName(t.SuspectAxis, s), EditorStyles.miniBoldLabel, GUILayout.Width(120));
            }
            for (int slot = 0; slot < t.SlotCount; slot++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string time = slot < c.Template.TimeSlots.Count ? c.Template.TimeSlots[slot] : "?";
                    GUILayout.Label((slot == t.CrimeSlot ? "★ " : "") + time, EditorStyles.miniBoldLabel, GUILayout.Width(70));
                    for (int s = 0; s < t.SuspectCount; s++)
                    {
                        int place = t.LocationOf(s, slot);
                        var old = GUI.color;
                        if (slot == t.CrimeSlot && place == crimePlace) GUI.color = LieColor;
                        GUILayout.Label(c.DisplayName(t.PlaceAxis, place), GUILayout.Width(120));
                        GUI.color = old;
                    }
                }
            }
            if (t.ItemAxis >= 0)
                EditorGUILayout.LabelField($"범인이 만진 도구: {c.DisplayName(t.ItemAxis, c.TruthOption(t.ItemAxis))}", miniWrap);
        }

        void DrawTestimonies(CaseInstance c)
        {
            var t = c.Timeline;
            Section("증언");
            using (new EditorGUILayout.HorizontalScope())
            {
                Legend("거짓 (증거로 반박)", LieColor);
                Legend("숨김 (추궁 시 공개)", HiddenColor);
                Legend("과장 (내용은 사실)", ExaggeratedColor);
                GUILayout.FlexibleSpace();
            }

            foreach (var testimony in c.Testimonies)
            {
                using (new EditorGUILayout.VerticalScope(clueBox))
                {
                    string title = $"{c.DisplayName(t.SuspectAxis, testimony.Suspect)}  ·  성향 {testimony.Style}";
                    if (testimony.IsCulprit) title += "  ·  ★ 범인";
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                    foreach (var line in testimony.Lines)
                    {
                        string text = string.IsNullOrEmpty(line.Text)
                            ? FactTextRenderer.Describe(c.Template, c.Space, t, line.Stated)
                            : line.Text;

                        var old = GUI.color;
                        string note = "";
                        switch (line.Kind)
                        {
                            case TestimonyLineKind.Lie:
                                GUI.color = LieColor;
                                note = "   ← 거짓. 실제: " + FactTextRenderer.Describe(c.Template, c.Space, t, TestimonyBuilder.ContradictingFact(t, line.Stated));
                                break;
                            case TestimonyLineKind.Hidden:
                                GUI.color = HiddenColor;
                                note = "   ← 숨김";
                                break;
                            case TestimonyLineKind.Exaggerated:
                                GUI.color = ExaggeratedColor;
                                note = "   ← 과장";
                                break;
                        }
                        EditorGUILayout.LabelField("· " + text + note, wrapLabel);
                        GUI.color = old;
                    }
                }
            }
        }

        static void Legend(string text, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUILayout.Label("■ " + text, EditorStyles.miniLabel);
            GUI.color = old;
        }

        void DrawEvidence(CaseInstance c)
        {
            var t = c.Timeline;
            Section($"반박 증거 {c.Evidence.Count}개");

            for (int e = 0; e < c.Evidence.Count; e++)
            {
                var evidence = c.Evidence[e];
                var line = c.FindLine(evidence.RebutsLineId);
                using (new EditorGUILayout.VerticalScope(clueBox))
                {
                    string text = string.IsNullOrEmpty(evidence.Text)
                        ? FactTextRenderer.Describe(c.Template, c.Space, t, evidence.Fact)
                        : evidence.Text;
                    EditorGUILayout.LabelField($"{e + 1}. {text}", wrapLabel);

                    string target = line == null
                        ? evidence.RebutsLineId
                        : $"{c.DisplayName(t.SuspectAxis, line.Stated.Who)}의 \"{(string.IsNullOrEmpty(line.Text) ? FactTextRenderer.Describe(c.Template, c.Space, t, line.Stated) : line.Text)}\"";
                    EditorGUILayout.LabelField("반박 대상: " + target, miniWrap);

                    var rewards = new List<string>();
                    for (int i = 0; i < c.Clues.Count; i++)
                        if (c.Clues[i].RewardLineId == evidence.RebutsLineId) rewards.Add($"단서 {i + 1}");
                    string reward = rewards.Count > 0 ? "   ·   반박 성공 시: " + string.Join(", ", rewards) + " 획득" : "";
                    EditorGUILayout.LabelField("획득: " + PlacementText(c, true, e) + reward +
                                               (evidence.RevealsFact ? "" : "   ·   (실제 위치는 밝히지 않는 증거)"), miniWrap);
                }
            }
        }

        // 단서나 증거가 놓인 곳을 글로.
        static string PlacementText(CaseInstance c, bool isEvidence, int index)
        {
            foreach (var p in c.Placements)
            {
                if (p.IsEvidence != isEvidence || p.Index != index) continue;
                var t = c.Timeline;
                switch (p.Via)
                {
                    case AcquireVia.Investigate:
                        return "조사 — " + (t != null && p.Target >= 0 ? c.DisplayName(t.PlaceAxis, p.Target) : "(장소 미정)");
                    case AcquireVia.Interview:
                        return "탐문 — " + (t != null && p.Target >= 0 ? c.DisplayName(t.SuspectAxis, p.Target) : "(대상 미정)");
                    case AcquireVia.SmallTalk:
                        return "잡담 — " + (t != null && p.Target >= 0 ? c.DisplayName(t.SuspectAxis, p.Target) : "(대상 미정)");
                    default:
                        var line = c.FindLine(p.LineId);
                        string who = line != null && t != null ? c.DisplayName(t.SuspectAxis, line.Stated.Who) : p.LineId;
                        return $"반박 보상 — {who}의 거짓 증언을 깨면 획득";
                }
            }
            return "(배치 없음)";
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // ---- JSON ----

        [Serializable]
        class CaseJson
        {
            public string template;
            public int seed;
            public int usedSeed;
            public int attempts;
            public string difficulty;
            public double generationMs;
            public List<AxisJson> axes = new List<AxisJson>();
            public List<ClueJson> clues = new List<ClueJson>();
        }

        [Serializable]
        class AxisJson
        {
            public string axisId;
            public List<string> options = new List<string>();
            public string truth;
        }

        [Serializable]
        class ClueJson
        {
            public string ruleId;
            public string text;
            public string debugText;
            public bool redHerring;
            public int eliminated;
            public int eliminatedAlone;
        }

        void CopyJson()
        {
            if (instance == null) return;

            var json = new CaseJson
            {
                template = instance.Template.Id,
                seed = instance.Seed,
                usedSeed = instance.UsedSeed,
                attempts = instance.Attempts,
                difficulty = instance.Difficulty.ToString(),
                generationMs = Math.Round(elapsedMs, 3),
            };
            for (int a = 0; a < instance.Space.AxisCount; a++)
            {
                var axis = new AxisJson { axisId = instance.Space.AxisId(a), truth = instance.TruthOptionId(a) };
                axis.options.AddRange(instance.Space.Axes[a].OptionIds);
                json.axes.Add(axis);
            }
            foreach (var clue in instance.Clues)
            {
                json.clues.Add(new ClueJson
                {
                    ruleId = clue.RuleId,
                    text = clue.Text,
                    debugText = clue.DebugText,
                    redHerring = clue.IsRedHerring,
                    eliminated = clue.Eliminated,
                    eliminatedAlone = clue.EliminatedAlone,
                });
            }

            EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(json, true);
            ShowNotification(new GUIContent("JSON을 클립보드에 복사했습니다"));
        }
    }
}
