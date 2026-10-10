using System;
using System.Collections.Generic;
using Detective.Core.Logic;
using Detective.Core.Model;
using Detective.Data;
using UnityEngine;

namespace Detective.Runtime
{
    // 버튼과 글자만으로 한 판을 끝까지 플레이하는 임시 화면.
    // 상태는 전부 CaseSession에 있고, 여기서는 그리기와 입력 전달만 한다.
    public class PrototypeGameUI : MonoBehaviour
    {
        [Header("시작 설정")]
        [Tooltip("플레이할 팩")]
        public WorldPack pack;

        [Tooltip("플레이할 사건 템플릿. 비우면 팩의 첫 번째 템플릿")]
        public CaseTemplate template;

        [Tooltip("사건 시드. 0이면 실행할 때마다 무작위")]
        public int seed;

        [Tooltip("난이도 덮어쓰기. 0 = 템플릿 값, 1 = Easy, 2 = Normal, 3 = Hard")]
        [Range(0, 3)]
        public int difficultyOverride;

        [Header("표시")]
        [Tooltip("한국어가 깨지면 한글 폰트를 넣는다. 비우면 Unity 기본 폰트")]
        public Font font;

        [Min(10)]
        public int fontSize = 16;

        CaseSession session;
        Coroutine running;
        string error;
        int usedSeed;

        int selectedEvidence = -1;
        int[] accusePicks;
        Vector2 centerScroll, notebookScroll, logScroll, actionScroll;

        GUIStyle label, small, title, button, box, selected;
        Font styledFont;
        int styledSize;

        static readonly Color LieColor = new Color(1f, 0.5f, 0.45f);
        static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.6f);
        static readonly Color DimColor = new Color(0.7f, 0.7f, 0.7f);

        void Start()
        {
            bool launched = ApplyEditorLaunch();
            StartCase(seed, launched);
        }

        // 에디터의 "이 시드로 플레이"가 넘긴 값이 있으면 인스펙터 값 대신 쓴다.
        // 넘겨받은 값이 있었으면 true (이때는 시드 0도 무작위가 아니라 0번 시드로 쓴다).
        bool ApplyEditorLaunch()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.GetBool(PrototypeLaunch.PendingKey, false)) return false;
            UnityEditor.EditorPrefs.DeleteKey(PrototypeLaunch.PendingKey);

            seed = UnityEditor.EditorPrefs.GetInt(PrototypeLaunch.SeedKey, seed);
            difficultyOverride = Mathf.Clamp(UnityEditor.EditorPrefs.GetInt(PrototypeLaunch.DifficultyKey, 0), 0, 3);

            string packPath = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.EditorPrefs.GetString(PrototypeLaunch.PackGuidKey, ""));
            var launchPack = string.IsNullOrEmpty(packPath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<WorldPack>(packPath);
            if (launchPack != null) pack = launchPack;

            string templatePath = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.EditorPrefs.GetString(PrototypeLaunch.TemplateGuidKey, ""));
            var launchTemplate = string.IsNullOrEmpty(templatePath) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<CaseTemplate>(templatePath);
            if (launchTemplate != null) template = launchTemplate;
            return true;
#else
            return false;
#endif
        }

        // 버튼을 누른 결과는 그리는 도중이 아니라 다음 Update에서 적용한다 (IMGUI는 한 이벤트 안에서 컨트롤 수가 바뀌면 오류를 낸다).
        Action pending;

        void Do(Action action) => pending += action;

        void Update()
        {
            if (pending == null) return;
            var actions = pending;
            pending = null;
            actions();
        }

        // requestedSeed가 0이면 무작위 시드로 시작한다. exactSeed가 true면 0도 그대로 시드로 쓴다.
        public void StartCase(int requestedSeed, bool exactSeed = false)
        {
            if (running != null) StopCoroutine(running);
            running = null;
            session = null;
            error = null;
            selectedEvidence = -1;
            accusePicks = null;

            if (pack == null) { error = "팩이 지정되지 않았습니다. PrototypeGameUI의 Pack 칸에 WorldPack 에셋을 넣으세요."; return; }
            if (template == null) template = pack.templates.Find(t => t != null);
            if (template == null) { error = "팩에 사건 템플릿이 없습니다."; return; }

            usedSeed = exactSeed || requestedSeed != 0 ? requestedSeed : new System.Random(Environment.TickCount).Next(1, int.MaxValue);

            CaseTemplateData data;
            try
            {
                data = PackConverter.Convert(template, pack);
            }
            catch (PackConversionException e)
            {
                error = e.Message;
                return;
            }

            Difficulty? difficulty = difficultyOverride == 0 ? (Difficulty?)null : (Difficulty)(difficultyOverride - 1);
            var instance = CaseGenerator.Generate(data, usedSeed, difficulty);
            if (instance == null)
            {
                error = $"시드 {usedSeed}로 사건을 만들지 못했습니다. Detective > Pack Editor의 문제 목록을 확인하세요.";
                return;
            }

            session = new CaseSession(instance);
            session.OnLog += _ => logScroll.y = float.MaxValue;
            session.AddLog($"사건: {data.Title}  (시드 {usedSeed}, 난이도 {instance.Difficulty})");
            running = StartCoroutine(new FlowRunner().Run(template.flow, session));
        }

        // ---- 그리기 ----

        void EnsureStyles()
        {
            if (label != null && styledFont == font && styledSize == fontSize) return;
            styledFont = font;
            styledSize = fontSize;

            label = Styled(GUI.skin.label, fontSize);
            label.wordWrap = true;
            small = Styled(GUI.skin.label, Mathf.Max(10, fontSize - 3));
            small.wordWrap = true;
            title = Styled(GUI.skin.label, fontSize + 4);
            title.fontStyle = FontStyle.Bold;
            title.wordWrap = true;
            button = Styled(GUI.skin.button, fontSize);
            button.wordWrap = true;
            box = Styled(GUI.skin.box, fontSize);
            box.alignment = TextAnchor.UpperLeft;
            box.wordWrap = true;
            box.padding = new RectOffset(8, 8, 6, 6);
            selected = Styled(GUI.skin.button, fontSize);
            selected.wordWrap = true;
            selected.fontStyle = FontStyle.Bold;
            selected.normal = selected.active;
        }

        GUIStyle Styled(GUIStyle source, int size)
        {
            var style = new GUIStyle(source) { fontSize = size, richText = false };
            if (font != null) style.font = font;
            return style;
        }

        void OnGUI()
        {
            EnsureStyles();

            const float pad = 8f, top = 44f, logHeight = 150f;
            float w = Screen.width, h = Screen.height;
            float left = Mathf.Clamp(w * 0.2f, 200f, 300f);
            float right = Mathf.Clamp(w * 0.3f, 280f, 460f);
            float bodyY = top + pad;
            float bodyH = h - bodyY - logHeight - pad * 2;

            GUILayout.BeginArea(new Rect(pad, pad, w - pad * 2, top - pad), box);
            DrawStatusBar();
            GUILayout.EndArea();

            if (session == null)
            {
                GUILayout.BeginArea(new Rect(pad, bodyY, w - pad * 2, bodyH), box);
                GUILayout.Label(error ?? "사건을 준비하는 중…", label);
                if (GUILayout.Button("다시 시도", button, GUILayout.Width(160))) Do(() => StartCase(seed));
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(pad, bodyY, left, bodyH), box);
            DrawActions();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(pad * 2 + left, bodyY, w - left - right - pad * 4, bodyH), box);
            centerScroll = GUILayout.BeginScrollView(centerScroll);
            DrawCenter();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(w - right - pad, bodyY, right, bodyH), box);
            DrawNotebook();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(pad, h - logHeight - pad, w - pad * 2, logHeight), box);
            DrawLog();
            GUILayout.EndArea();
        }

        void DrawStatusBar()
        {
            GUILayout.BeginHorizontal();
            if (session == null)
            {
                GUILayout.Label("추리 탐정 사무소 — 프로토타입", label);
            }
            else
            {
                GUILayout.Label($"{session.Instance.Template.Title}", label, GUILayout.Width(260));
                GUILayout.Label($"단계: {PhaseName(session.Phase)}", label, GUILayout.Width(170));
                GUILayout.Label($"행동력: {session.ActionPoints}", label, GUILayout.Width(120));
                GUILayout.Label("신뢰도: " + new string('●', session.Trust) + new string('○', session.MaxTrust - session.Trust), label, GUILayout.Width(190));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"시드 {usedSeed}", small);
            }
            GUILayout.EndHorizontal();
        }

        static string PhaseName(PhaseKind kind)
        {
            switch (kind)
            {
                case PhaseKind.Dialogue: return "대화";
                case PhaseKind.Investigate: return "조사";
                case PhaseKind.Testimony: return "증언";
                case PhaseKind.Accuse: return "지목";
                case PhaseKind.Result: return "결말";
                default: return "-";
            }
        }

        // ---- 왼쪽: 행동 ----

        void DrawActions()
        {
            GUILayout.Label("행동", title);
            var investigate = session.CurrentPhase as InvestigatePhase;
            if (investigate == null)
            {
                GUILayout.Label("조사 단계에서만 움직일 수 있다.", small);
                return;
            }

            var timeline = session.Instance.Timeline;
            actionScroll = GUILayout.BeginScrollView(actionScroll);
            if (timeline != null)
            {
                GUILayout.Label("장소 조사", small);
                for (int p = 0; p < timeline.PlaceCount; p++)
                    if (GUILayout.Button(session.PlaceName(p), button)) { int place = p; Do(() => session.Investigate(place)); }

                GUILayout.Space(6);
                GUILayout.Label("탐문 / 잡담", small);
                for (int s = 0; s < timeline.SuspectCount; s++)
                {
                    GUILayout.Label(session.SuspectName(s), small);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("탐문", button)) { int who = s; Do(() => session.Interview(who)); }
                    if (GUILayout.Button("잡담", button)) { int who = s; Do(() => session.SmallTalk(who)); }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            if (GUILayout.Button("조사 끝내기 →", button, GUILayout.Height(fontSize * 2.4f))) Do(investigate.Finish);
        }

        // ---- 가운데: 페이즈별 화면 ----

        void DrawCenter()
        {
            switch (session.CurrentPhase)
            {
                case DialoguePhase dialogue: DrawDialogue(dialogue); break;
                case InvestigatePhase _: DrawInvestigate(); break;
                case TestimonyPhase testimony: DrawTestimony(testimony); break;
                case AccusePhase accuse: DrawAccuse(accuse); break;
                case ResultPhase result: DrawResult(result); break;
                default: GUILayout.Label("…", label); break;
            }
        }

        void DrawDialogue(DialoguePhase dialogue)
        {
            GUILayout.Label(dialogue.Speaker, title);
            GUILayout.Space(8);
            GUILayout.Label(dialogue.CurrentLine, label);
            GUILayout.Space(16);
            if (GUILayout.Button("다음 ▶", button, GUILayout.Width(160), GUILayout.Height(fontSize * 2.4f))) Do(dialogue.Advance);
        }

        void DrawInvestigate()
        {
            var instance = session.Instance;
            GUILayout.Label("조사", title);
            GUILayout.Label(instance.Template.RequestText, label);
            GUILayout.Space(8);
            GUILayout.Label("왼쪽에서 장소를 조사하거나 용의자에게 말을 건다. 뭔가를 얻을 때마다 행동력이 1 줄어든다. " +
                            "얻은 단서와 증거는 오른쪽 수첩에 쌓인다. 다 모을 수는 없으니 어디부터 볼지 고르자.", small);

            GUILayout.Space(12);
            GUILayout.Label("후보", title);
            for (int a = 0; a < instance.Space.AxisCount; a++)
            {
                var names = new List<string>();
                for (int o = 0; o < instance.Space.OptionCount(a); o++) names.Add(instance.DisplayName(a, o));
                GUILayout.Label($"{instance.Space.AxisId(a)}: {string.Join(", ", names)}", label);
            }
        }

        void DrawTestimony(TestimonyPhase phase)
        {
            var testimony = phase.Testimony;
            if (testimony == null) return;

            GUILayout.Label($"{session.SuspectName(testimony.Suspect)}의 증언", title);
            GUILayout.Label(selectedEvidence >= 0
                ? "이상한 줄에 '반박'을 누르면 수첩에서 고른 증거를 들이민다. 틀리면 신뢰도가 깎인다."
                : "오른쪽 수첩에서 증거를 하나 고른 뒤, 이상한 줄의 '반박'을 누른다. '추궁'은 더 캐묻는다.", small);
            GUILayout.Space(8);

            foreach (var line in testimony.Lines)
            {
                if (!session.IsLineVisible(line)) continue;
                bool broken = session.IsRebutted(line.Id);

                GUILayout.BeginHorizontal();
                var old = GUI.color;
                if (broken) GUI.color = LieColor;
                GUILayout.Label((broken ? "✗ " : "· ") + line.Text, label);
                GUI.color = old;

                GUI.enabled = !broken;
                if (GUILayout.Button("추궁", button, GUILayout.Width(70))) { string id = line.Id; Do(() => session.Press(id)); }
                GUI.enabled = !broken && selectedEvidence >= 0;
                if (GUILayout.Button("반박", button, GUILayout.Width(70)))
                {
                    string id = line.Id;
                    int shown = selectedEvidence;
                    Do(() => { if (session.Rebut(id, shown) == RebutResult.Success) selectedEvidence = -1; });
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            GUILayout.Space(12);
            if (phase.CanFinish)
            {
                if (GUILayout.Button("증언 끝내기 →", button, GUILayout.Width(220), GUILayout.Height(fontSize * 2.4f))) Do(() => phase.Finish());
            }
            else
            {
                GUILayout.Label("이 증언에는 거짓이 있다. 깨야 넘어갈 수 있다.", small);
                if (GUILayout.Button("반박 포기 (신뢰도 -1) →", button, GUILayout.Width(260))) Do(phase.GiveUp);
            }
        }

        void DrawAccuse(AccusePhase phase)
        {
            var instance = session.Instance;
            if (accusePicks == null || accusePicks.Length != instance.Space.AxisCount)
                accusePicks = new int[instance.Space.AxisCount];

            GUILayout.Label("최종 추리", title);
            GUILayout.Label("축마다 하나씩 고르고 '지목'을 누른다. 틀리면 신뢰도가 깎인다.", small);

            foreach (int axis in phase.Axes)
            {
                GUILayout.Space(8);
                GUILayout.Label(instance.Space.AxisId(axis), label);
                GUILayout.BeginHorizontal();
                for (int o = 0; o < instance.Space.OptionCount(axis); o++)
                {
                    bool isPicked = accusePicks[axis] == o;
                    if (GUILayout.Button(instance.DisplayName(axis, o), isPicked ? selected : button)) accusePicks[axis] = o;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(16);
            if (GUILayout.Button("지목!", button, GUILayout.Width(220), GUILayout.Height(fontSize * 2.6f))) { var picks = (int[])accusePicks.Clone(); Do(() => phase.Accuse(picks)); }
        }

        void DrawResult(ResultPhase phase)
        {
            var review = phase.Review;
            var instance = session.Instance;
            if (review == null) return;

            var old = GUI.color;
            GUI.color = review.Outcome == CaseOutcome.Solved ? GoodColor : LieColor;
            GUILayout.Label(review.Outcome == CaseOutcome.Solved ? $"사건 해결!  등급 {review.Grade}" : "의뢰 실패…", title);
            GUI.color = old;
            GUILayout.Label($"남은 신뢰도 {review.Trust}/{review.MaxTrust} · 쓴 행동 {review.ActionsUsed}회 (최단 경로 {review.RequiredActions}회)", small);

            GUILayout.Space(10);
            GUILayout.Label("진상", title);
            for (int a = 0; a < instance.Space.AxisCount; a++)
            {
                string mine = review.Accusation == null ? "지목 안 함" : instance.DisplayName(a, review.Accusation[a]);
                GUI.color = review.IsAxisCorrect(a) ? GoodColor : LieColor;
                GUILayout.Label($"{instance.Space.AxisId(a)}: {instance.DisplayName(a, review.Truth[a])}   (내 지목: {mine})", label);
                GUI.color = old;
            }

            GUILayout.Space(10);
            GUILayout.Label("복기: 단서별로 지운 후보 수", title);
            foreach (var item in review.Clues)
            {
                var clue = item.Clue;
                string text = string.IsNullOrEmpty(clue.Text) ? clue.DebugText : clue.Text;
                string tag = clue.IsRedHerring ? "[가짜] " : clue.IsRebuttalReward ? "[반박 보상] " : "";
                string effect = clue.IsRedHerring ? "필요 없는 단서" : $"후보 {clue.Eliminated}개 지움";

                GUI.color = item.Obtained ? old : DimColor;
                GUILayout.Label($"{(item.Obtained ? "✓" : "✗ 놓침")}  {tag}{text}", label);
                GUILayout.Label($"      {effect} · 얻는 곳: {item.Where}", small);
                GUI.color = old;
            }

            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("같은 사건 다시", button, GUILayout.Height(fontSize * 2.4f))) Do(() => { phase.Finish(); StartCase(usedSeed, true); });
            if (GUILayout.Button("다음 사건 (시드 +1)", button, GUILayout.Height(fontSize * 2.4f))) Do(() => { phase.Finish(); StartCase(unchecked(usedSeed + 1), true); });
            if (GUILayout.Button("무작위 사건", button, GUILayout.Height(fontSize * 2.4f))) Do(() => { phase.Finish(); StartCase(0); });
            GUILayout.EndHorizontal();
        }

        // ---- 오른쪽: 수첩 ----

        void DrawNotebook()
        {
            var instance = session.Instance;
            GUILayout.Label("수첩", title);
            notebookScroll = GUILayout.BeginScrollView(notebookScroll);

            GUILayout.Label($"단서 {session.Clues.Count}개", small);
            foreach (int index in session.Clues)
            {
                var clue = instance.Clues[index];
                GUILayout.Label("· " + (string.IsNullOrEmpty(clue.Text) ? clue.DebugText : clue.Text), label);
            }

            GUILayout.Space(10);
            GUILayout.Label($"증거 {session.Evidence.Count}개 (눌러서 반박에 쓸 증거 고르기)", small);
            foreach (int index in session.Evidence)
            {
                var item = instance.Evidence[index];
                string text = string.IsNullOrEmpty(item.Text) ? item.ToString() : item.Text;
                if (GUILayout.Button(text, selectedEvidence == index ? selected : button))
                    selectedEvidence = selectedEvidence == index ? -1 : index;
            }

            GUILayout.EndScrollView();
        }

        // ---- 아래: 로그 ----

        void DrawLog()
        {
            logScroll = GUILayout.BeginScrollView(logScroll);
            int start = Mathf.Max(0, session.Log.Count - 60);
            for (int i = start; i < session.Log.Count; i++) GUILayout.Label(session.Log[i], small);
            GUILayout.EndScrollView();
        }
    }
}
