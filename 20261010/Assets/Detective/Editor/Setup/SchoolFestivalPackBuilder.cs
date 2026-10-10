using System;
using System.Collections.Generic;
using System.Text;
using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 학원 축제 팩(tool-design 9장)의 에셋을 Assets/Packs/SchoolFestival/ 아래에 만든다.
    // 같은 id의 에셋이 이미 있으면 건드리지 않고 건너뛴다.
    public static class SchoolFestivalPackBuilder
    {
        const string PackName = "SchoolFestival";
        const string Folder = PackAssetOps.PacksRoot + "/" + PackName;

        [MenuItem("Detective/Setup/Create School Festival Pack")]
        public static void Create()
        {
            var log = new BuildLog();
            Build(log);
            Debug.Log(log.ToString());
        }

        [MenuItem("Detective/Setup/Recreate School Festival Pack (Delete Existing)")]
        static void RecreateMenu()
        {
            if (!EditorUtility.DisplayDialog("학원 축제 팩 다시 만들기",
                    $"{Folder} 아래의 팩 에셋(태그·엔티티·프로필·규칙·템플릿·팩)을 모두 휴지통으로 보내고 처음부터 다시 만듭니다.\n손으로 고친 내용은 사라집니다.",
                    "다시 만들기", "취소")) return;
            Recreate();
        }

        // 확인 창 없이 지우고 다시 만든다 (배치 모드: -executeMethod Detective.Editor.SchoolFestivalPackBuilder.Recreate).
        public static void Recreate()
        {
            var log = new BuildLog();
            if (AssetDatabase.IsValidFolder(Folder))
            {
                int removed = 0;
                removed += TrashAll<CaseTemplate>();
                removed += TrashAll<ClueRule>();
                removed += TrashAll<SuspectProfile>();
                removed += TrashAll<EntityDef>();
                removed += TrashAll<TagDef>();
                removed += TrashAll<TestimonyText>();
                removed += TrashAll<CaseFlow>();
                removed += TrashAll<WorldPack>();
                log.Note($"기존 에셋 {removed}개를 휴지통으로 보냈습니다.");
            }
            Build(log);
            Debug.Log(log.ToString());
        }

        static int TrashAll<T>() where T : UnityEngine.Object
        {
            int count = 0;
            foreach (var asset in PackAssetOps.LoadAll<T>(Folder))
                if (AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(asset))) count++;
            return count;
        }

        // ---- 만들기 ----

        static void Build(BuildLog log)
        {
            if (!AssetDatabase.IsValidFolder(PackAssetOps.PacksRoot)) AssetDatabase.CreateFolder("Assets", "Packs");
            PackAssetOps.EnsureFolder(PackAssetOps.PacksRoot, PackName);

            var tags = Index(PackAssetOps.LoadAll<TagDef>(Folder), t => t.id);
            var entities = Index(PackAssetOps.LoadAll<EntityDef>(Folder), e => e.id);
            var profiles = Index(PackAssetOps.LoadAll<SuspectProfile>(Folder), p => p.entity != null ? p.entity.id : null);
            var rules = Index(PackAssetOps.LoadAll<ClueRule>(Folder), r => r.id);
            var templates = Index(PackAssetOps.LoadAll<CaseTemplate>(Folder), t => t.id);

            BuildTags(tags, log);
            BuildEntities(entities, tags, log);
            BuildProfiles(profiles, entities, log);
            BuildRules(rules, tags, log);
            BuildTemplates(templates, rules, log);
            BuildTestimonyText(log);

            // 흐름이 비어 있는 템플릿에만 짧은 사건 흐름을 연결한다 (이미 지정된 흐름은 건드리지 않는다).
            var flow = BuildShortFlow(log);
            var templateIds = new List<string>(templates.Keys);
            templateIds.Sort(StringComparer.Ordinal);
            foreach (var templateId in templateIds)
            {
                var template = templates[templateId];
                if (template.flow != null) continue;
                template.flow = flow;
                EditorUtility.SetDirty(template);
                log.Note($"사건 템플릿 {templateId}에 흐름 {flow.name} 연결.");
            }

            var packs = PackAssetOps.LoadAll<WorldPack>(Folder);
            WorldPack pack;
            if (packs.Count > 0)
            {
                pack = packs[0];
                log.Skipped("팩", pack.name);
            }
            else
            {
                pack = ScriptableObject.CreateInstance<WorldPack>();
                pack.displayName = "학원 축제";
                AssetDatabase.CreateAsset(pack, $"{Folder}/{PackAssetOps.PackPrefix}{PackName}.asset");
                log.Created("팩", pack.name);
            }

            PackAssetOps.Sync(pack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var problems = PackValidator.Validate(pack);
            int errors = 0;
            foreach (var problem in problems)
                if (problem.Severity == ProblemSeverity.Error) errors++;
            log.Note(problems.Count == 0
                ? "검증: 문제 없음."
                : $"검증: 오류 {errors}개, 경고 {problems.Count - errors}개. Detective > Pack Editor의 문제 목록을 확인하세요.");
            foreach (var problem in problems) log.Note("  " + problem);
        }

        static Dictionary<string, T> Index<T>(List<T> assets, Func<T, string> idOf)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal); // 조회 전용
            foreach (var asset in assets)
            {
                string id = idOf(asset);
                if (!string.IsNullOrEmpty(id) && !result.ContainsKey(id)) result.Add(id, asset);
            }
            return result;
        }

        // 같은 id가 이미 있으면 그대로 두고, 없으면 만든다.
        static void GetOrCreate<T>(Dictionary<string, T> existing, string id, string subfolder, string prefix, string kindLabel,
            BuildLog log, Action<T> init) where T : ScriptableObject
        {
            if (existing.ContainsKey(id))
            {
                log.Skipped(kindLabel, id);
                return;
            }

            string folder = PackAssetOps.EnsureFolder(Folder, subfolder);
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}{PackAssetOps.SafeFileName(id)}.asset"));
            existing.Add(id, asset);
            log.Created(kindLabel, id);
        }

        // ---- 태그 ----

        static void BuildTags(Dictionary<string, TagDef> tags, BuildLog log)
        {
            var data = new (string id, string name, string category)[]
            {
                ("club.drama", "연극부", "club"),
                ("club.cooking", "요리부", "club"),
                ("club.broadcast", "방송부", "club"),
                ("club.band", "경음부", "club"),
                ("club.council", "학생회", "club"),
                ("club.art", "미술부", "club"),
                ("floor.1", "1층", "floor"),
                ("floor.2", "2층", "floor"),
                ("floor.roof", "옥상", "floor"),
                ("env.indoor", "실내", "env"),
                ("env.outdoor", "실외", "env"),
                ("trait.lefty", "왼손잡이", "trait"),
                ("trait.tall", "키 큼", "trait"),
            };

            foreach (var (id, name, category) in data)
            {
                GetOrCreate(tags, id, PackAssetOps.TagFolder, PackAssetOps.TagPrefix, "태그", log, tag =>
                {
                    tag.id = id;
                    tag.displayName = name;
                    tag.category = category;
                });
            }
        }

        // ---- 엔티티 ----

        static void BuildEntities(Dictionary<string, EntityDef> entities, Dictionary<string, TagDef> tags, BuildLog log)
        {
            var data = new (string id, string name, EntityKind kind, string description, string[] tags)[]
            {
                ("drama_captain", "연극부 부장", EntityKind.Suspect, "모든 문제를 돈과 박수로 해결하려는 허세 대장.",
                    new[] { "club.drama", "floor.2", "trait.tall" }),
                ("council_treasurer", "학생회 회계", EntityKind.Suspect, "1원이 안 맞으면 잠을 못 자는 완벽주의자.",
                    new[] { "club.council", "floor.1" }),
                ("cooking_freshman", "요리부 1학년", EntityKind.Suspect, "말끝마다 사과하는 소심한 목격자.",
                    new[] { "club.cooking", "floor.1" }),
                ("broadcast_member", "방송부원", EntityKind.Suspect, "교내 방송을 운명의 전파라고 부르는 중2병.",
                    new[] { "club.broadcast", "floor.2", "trait.lefty" }),
                ("band_guitarist", "경음부 기타리스트", EntityKind.Suspect, "입버릇은 '귀찮아'. 거짓말도 귀찮아서 안 한다.",
                    new[] { "club.band", "floor.2", "trait.tall", "trait.lefty" }),
                ("art_captain", "미술부 부장", EntityKind.Suspect, "자칭 명탐정. 늘 우리보다 먼저 결론을 낸다. 대체로 틀린다.",
                    new[] { "club.art", "floor.roof" }),

                ("backstage", "강당 무대 뒤", EntityKind.Place, "의상과 소품이 산처럼 쌓인 연극부의 영역.",
                    new[] { "club.drama", "floor.1", "env.indoor" }),
                ("storage", "창고", EntityKind.Place, "학생회가 관리하는 비품 창고. 장부가 물건보다 많다.",
                    new[] { "club.council", "floor.1", "env.indoor" }),
                ("kitchen", "조리실", EntityKind.Place, "요리부의 성지. 항상 뭔가 타는 냄새가 난다.",
                    new[] { "club.cooking", "floor.1", "env.indoor" }),
                ("broadcast_room", "방송실", EntityKind.Place, "'ON AIR' 불이 꺼진 적이 없는 방.",
                    new[] { "club.broadcast", "floor.2", "env.indoor" }),
                ("music_room", "음악실", EntityKind.Place, "경음부 연습실. 방음은 기분만 낸다.",
                    new[] { "club.band", "floor.2", "env.indoor" }),
                ("rooftop", "옥상", EntityKind.Place, "미술부가 풍경화를 핑계로 점거한 곳.",
                    new[] { "club.art", "floor.roof", "env.outdoor" }),

                ("costume_rack", "의상 행거", EntityKind.Item, "바퀴 달린 연극부 의상 행거. 드레스 사이에 뭐든 숨는다.",
                    new[] { "club.drama" }),
                ("big_pot", "대형 냄비", EntityKind.Item, "50인분 카레용 냄비. 사람 머리 하나쯤은 들어간다.",
                    new[] { "club.cooking" }),
                ("equipment_cart", "방송 장비 카트", EntityKind.Item, "케이블이 국수처럼 늘어진 카트.",
                    new[] { "club.broadcast" }),
                ("amp_case", "앰프 케이스", EntityKind.Item, "앰프보다 케이스가 더 무겁다는 소문.",
                    new[] { "club.band" }),
                ("easel_bag", "이젤 가방", EntityKind.Item, "길쭉하고 수상한 가방. 미술부는 다들 들고 다닌다.",
                    new[] { "club.art" }),
                ("document_box", "서류 상자", EntityKind.Item, "'회계 자료, 열지 마시오'라고 적힌 상자.",
                    new[] { "club.council" }),
            };

            foreach (var row in data)
            {
                GetOrCreate(entities, row.id, PackAssetOps.EntityFolder, PackAssetOps.EntityPrefix, "엔티티", log, entity =>
                {
                    entity.id = row.id;
                    entity.displayName = row.name;
                    entity.kind = row.kind;
                    entity.description = row.description;
                    foreach (var tagId in row.tags)
                        if (tags.TryGetValue(tagId, out var tag)) entity.tags.Add(tag);
                });
            }
        }

        // ---- 용의자 프로필 ----

        static void BuildProfiles(Dictionary<string, SuspectProfile> profiles, Dictionary<string, EntityDef> entities, BuildLog log)
        {
            var data = new (string entityId, string catchphrase, LieStyle lieStyle, string[] smallTalk)[]
            {
                ("drama_captain", "흥, 당연한 거 아냐?", LieStyle.Liar, new[]
                {
                    "이번 공연 의상은 전부 내 사비로 맞췄어. 영수증? 그런 건 품위 없게 안 챙겨.",
                    "주인공은 당연히 나지. 오디션은 형식이었고.",
                }),
                ("council_treasurer", "규정상 그렇습니다.", LieStyle.Liar, new[]
                {
                    "축제 예산은 1원 단위까지 맞습니다. 맞아야 합니다. 맞을 겁니다.",
                    "영수증 없는 지출은 존재하지 않는 지출입니다. 그러니까 저는 아무것도 안 샀습니다.",
                }),
                ("cooking_freshman", "죄, 죄송해요…", LieStyle.Omitter, new[]
                {
                    "푸딩은… 제가 만든 건 맞는데 망친 건 제가 아니에요. 죄송해요.",
                    "선배들이 간 보라고 해서 본 거예요. 스무 번쯤… 죄송해요.",
                }),
                ("broadcast_member", "이것도 운명의 주파수인가…", LieStyle.Exaggerator, new[]
                {
                    "교내 방송은 세계를 향한 내 첫 번째 전파다. 현재 청취자는 급식실의 열두 명.",
                    "마이크를 쥐면 왼손이 욱신거려. 봉인이 풀리려는 거지. …건초염? 그런 이름으로 부르지 마.",
                }),
                ("band_guitarist", "…귀찮아.", LieStyle.Honest, new[]
                {
                    "리허설? 했어. 한 곡. 반 곡인가.",
                    "앰프 옮기는 게 제일 싫어. 그래서 안 옮겨.",
                }),
                ("art_captain", "범인은 이미 알고 있지.", LieStyle.Liar, new[]
                {
                    "추리는 관찰이야. 난 옥상에서 전교를 내려다보거든. 오늘은 비 와서 안 올라갔지만.",
                    "너희 사무소, 아직도 월세 밀렸다며? 이번 조사비는 내가 낼까?",
                }),
            };

            foreach (var row in data)
            {
                if (!entities.TryGetValue(row.entityId, out var entity)) continue;
                GetOrCreate(profiles, row.entityId, PackAssetOps.SuspectFolder, PackAssetOps.SuspectPrefix, "용의자 프로필", log, profile =>
                {
                    profile.entity = entity;
                    profile.catchphrase = row.catchphrase;
                    profile.lieStyle = row.lieStyle;
                    profile.smallTalkLines.AddRange(row.smallTalk);
                });
            }
        }

        // ---- 단서 규칙 ----
        // 문장에서 변수 바로 뒤에 은/는, 이/가, 을/를을 붙이지 않는다. 이름의 받침에 따라 조사가 달라지기 때문이다.

        static void BuildRules(Dictionary<string, ClueRule> rules, Dictionary<string, TagDef> tags, BuildLog log)
        {
            Rule(rules, log, "alibi", ConstraintType.IsNot, "culprit", ClueSource.Interview, 2f, 0.5f, null,
                "{time}에 {suspect} 본 사람 손! …{place2}에서 핫도그 세 개를 들고 있었다는 제보가 열두 건.",
                "{suspect}의 SNS에 {time} 정각 인증샷이 올라와 있다. 배경은 {place2}. 브이까지 하고 있어서 반박이 불가능하다.",
                "{suspect}한테는 알리바이가 있다. {time}에 {place2}에서 담임한테 붙잡혀 30분째 진로 상담을 당하고 있었다.",
                "그 시간에 {suspect}도 봤어요. {place2}에서 풍선을 터뜨리다가 자기가 터뜨려 놓고 놀라 넘어지던데요.");

            Rule(rules, log, "locked_room", ConstraintType.IsNot, "place", ClueSource.Investigate | ClueSource.Interview, 2f, 0.5f, null,
                "{place} 문에는 '페인트칠 중. 만지면 반성문'이라는 종이가 하루 종일 붙어 있었다.",
                "{place} 열쇠는 오후 내내 교무실 서랍 안에 있었다. 그 서랍 열쇠는 교감 선생님 주머니 안에 있었고.",
                "{place}에는 아무도 못 들어갔어요. 문 앞에서 댄스부가 세 시간째 같은 안무를 틀리고 있었거든요.");

            Rule(rules, log, "dusty_item", ConstraintType.IsNot, "item", ClueSource.Investigate, 2f, 0.5f, null,
                "{item}에는 먼지가 그대로 쌓여 있다. 누가 손가락으로 '청소 좀'이라고 써 놓기까지 했다.",
                "{item}에는 '고장. 만지면 네 책임'이라는 딱지가 붙어 있고, 실제로 한쪽이 덜렁거린다.",
                "{item}의 대여 장부는 오늘 날짜 칸이 텅 비어 있다. 담당자가 무서워서 아무도 몰래 못 쓴다.");

            Rule(rules, log, "club_supply", ConstraintType.SameTag, "item", ClueSource.Investigate, 0.7f, 1.5f, rule =>
                {
                    rule.axisB = "place";
                    rule.tagCategory = "club";
                },
                "없어진 물건을 옮긴 도구는 그 장소를 쓰는 동아리의 비품이다. 바닥에 끌린 자국이 딱 맞는다.",
                "도구를 멀리서 들고 온 흔적이 없다. 현장에 원래 있던 걸 그대로 쓴 모양이다.",
                "범인은 남의 동아리 물건에는 손대지 않았다. 그 방에 있던 비품을 썼다. 예의는 바른 편.");

            Rule(rules, log, "floor_sighting", ConstraintType.HasTag, "culprit", ClueSource.Interview | ClueSource.SmallTalk, 1f, 1f, rule =>
                {
                    rule.openTag = true;
                    rule.tagCategory = "floor";
                },
                "{tag}에서 수상한 그림자를 봤어요. 그 층 사람이 아니면 그렇게 자연스럽게 못 숨어요.",
                "범인은 {tag}에 동아리방이 있는 사람이다. 실내화 주머니가 그쪽 신발장에서 나왔다.",
                "{tag}에서 늘 보던 얼굴이었대요. 누군지는 기억 안 나는데 '또 쟤네 동아리네' 싶었다고.");

            Rule(rules, log, "left_hand", ConstraintType.HasTag, "culprit", ClueSource.Investigate, 1f, 1f, rule =>
                {
                    rule.openTag = false;
                    tags.TryGetValue("trait.lefty", out rule.tag);
                },
                "문고리에 왼손으로 잡은 자국이 남아 있다. 떡볶이 양념이 묻은 왼손으로.",
                "현장에 남은 메모의 글씨가 전부 오른쪽으로 번져 있다. 왼손잡이의 숙명이다.",
                "현장의 가위가 왼손잡이용으로 바뀌어 있다. 범인은 도구에 진심인 왼손잡이다.");

            Rule(rules, log, "not_outdoor", ConstraintType.LacksTag, "place", ClueSource.Investigate, 1f, 1f, rule =>
                {
                    rule.openTag = false;
                    tags.TryGetValue("env.outdoor", out rule.tag);
                },
                "물건에는 물기 하나 없었다. 오늘은 오후 내내 비가 왔는데.",
                "숨긴 곳은 지붕이 있는 곳이다. 물건에 묻은 건 빗물이 아니라 에어컨 물이었다.",
                "밖은 아니에요. 오늘 바깥은 풍물패가 점령해서 숨길 틈이 없었어요.");

            // "A가 범인이면 장소는 L" — A가 범인이 아니어도 참이므로 목격담이 아니라 조건문으로 쓴다.
            Rule(rules, log, "seen_heading", ConstraintType.Implies, "culprit", ClueSource.Interview | ClueSource.SmallTalk, 0.5f, 2f, rule =>
                {
                    rule.axisB = "place";
                },
                "{suspect} 짓이라면 장소는 {place}밖에 없어요. 다른 데는 갈 줄도 모르는 사람이라.",
                "만약 {suspect}의 소행이라면 물건은 {place}에 있을 거다. 그쪽 열쇠를 가진 게 그 사람뿐이니까.",
                "'{suspect} 범인설이 맞다면 현장은 {place}!'라고 신문부가 호외를 뿌리고 있다. 근거는 꽤 탄탄하다.");

            Rule(rules, log, "one_of_two", ConstraintType.OneOf, "culprit", ClueSource.Interview, 1f, 1f, null,
                "범인은 {suspect} 아니면 {suspect2}! 목격자가 둘을 맨날 헷갈려서 더는 못 좁힌다.",
                "도망치는 뒷모습을 봤는데, {suspect} 아니면 {suspect2} 둘 중 하나예요. 가방이 똑같거든요.",
                "제보함에 쪽지가 두 장. 한 장에는 '{suspect}', 다른 한 장에는 '{suspect2}'. 둘 중 한 장은 진짜다.");
        }

        static void Rule(Dictionary<string, ClueRule> rules, BuildLog log, string id, ConstraintType type, string axisA,
            ClueSource sources, float easyWeight, float hardWeight, Action<ClueRule> setup, params string[] texts)
        {
            GetOrCreate(rules, id, PackAssetOps.RuleFolder, PackAssetOps.RulePrefix, "단서 규칙", log, rule =>
            {
                rule.id = id;
                rule.constraintType = type;
                rule.axisA = axisA;
                rule.sources = sources;
                rule.difficultyWeight = 1f;
                rule.easyWeight = easyWeight;
                rule.hardWeight = hardWeight;
                rule.textVariants.AddRange(texts);
                setup?.Invoke(rule);
            });
        }

        // ---- 사건 템플릿 ----

        static void BuildTemplates(Dictionary<string, CaseTemplate> templates, Dictionary<string, ClueRule> rules, BuildLog log)
        {
            var data = new (string id, string title, string request, Difficulty difficulty, int redHerrings, int lieCount)[]
            {
                ("festival_missing_mascot", "마스코트 머리 실종",
                    "축제 마스코트 인형의 머리가 사라졌어요! 몸통만 남은 채로 교문에 서 있어서 신입생들이 울고 있다고요.",
                    Difficulty.Normal, 1, 2),
                ("festival_pudding_theft", "시식용 푸딩 도난",
                    "요리부의 한정 푸딩 20개가 감쪽같이… 숟가락은 그대로인데 푸딩만 없어요. 이건 계획범죄예요.",
                    Difficulty.Easy, 0, 1),
                ("festival_stage_lights", "무대 조명 장난",
                    "연극 리허설 중에 조명이 전부 꺼졌어요. 다시 켜졌을 땐 주인공 자리에 대걸레가 서 있었고요.",
                    Difficulty.Hard, 2, 3),
            };

            foreach (var row in data)
            {
                GetOrCreate(templates, row.id, PackAssetOps.TemplateFolder, PackAssetOps.TemplatePrefix, "사건 템플릿", log, template =>
                {
                    template.id = row.id;
                    template.title = row.title;
                    template.requestText = row.request;
                    template.axes.Add(new CaseTemplate.AxisSlot { axisId = "culprit", kind = EntityKind.Suspect, pickCount = 4 });
                    template.axes.Add(new CaseTemplate.AxisSlot { axisId = "place", kind = EntityKind.Place, pickCount = 4 });
                    template.axes.Add(new CaseTemplate.AxisSlot { axisId = "item", kind = EntityKind.Item, pickCount = 4 });

                    var ruleIds = new List<string>(rules.Keys);
                    ruleIds.Sort(StringComparer.Ordinal);
                    foreach (var ruleId in ruleIds) template.rules.Add(rules[ruleId]);

                    template.timeSlots.AddRange(new[] { "13시", "14시", "15시", "16시" });
                    template.crimeSlotIndex = 2;
                    // 필수 단서가 6~8개 나오므로 행동력 6으로는 대부분의 사건을 풀 수 없다. 짧은 사건 범위(6~8)의 위쪽을 쓴다.
                    template.actionPoints = 8;
                    template.actionMargin = 1;
                    template.lieCount = row.lieCount;
                    template.redHerringCount = row.redHerrings;
                    template.difficulty = row.difficulty;
                });
            }
        }

        // ---- 증언·증거 문장 ----
        // 증언은 말하는 사람이 "나"다. 여기서도 변수 뒤에 받침에 따라 달라지는 조사는 붙이지 않는다.

        static void BuildTestimonyText(BuildLog log)
        {
            if (PackAssetOps.LoadAll<TestimonyText>(Folder).Count > 0)
            {
                log.Skipped("증언 문장", "TestimonyText");
                return;
            }

            var text = ScriptableObject.CreateInstance<TestimonyText>();

            text.presenceLines.AddRange(new[]
            {
                "{time}에는 {place}에 있었어. 진짜야.",
                "{time}? 그때는 {place}에서 축제 준비하고 있었는데.",
                "{time}쯤이면 {place}에 있었을 거야. 거기 시계가 5분 빨라서 정확하진 않지만.",
            });
            text.sawLines.AddRange(new[]
            {
                "{time}에 {place}에서 {suspect} 봤어. 눈도 마주쳤어.",
                "{time}에 {place}에 있었는데, {suspect}도 거기 있었어.",
                "{place}에서 {suspect} 본 게 {time}쯤이야. 뭘 열심히 먹고 있던데.",
            });
            text.handledLines.AddRange(new[]
            {
                "{time}에 {place}에서 {item} 좀 만졌어. 그게 뭐?",
                "{place}에 있던 {item}? {time}쯤 잠깐 옮겼을 뿐이야.",
                "{time}에 {place}에서 {item} 정리한 건 맞아.",
            });
            text.exaggerationPrefixes.AddRange(new[]
            {
                "맹세코, ",
                "운명에 이끌리듯, ",
                "하늘에 대고 말하는데, ",
                "내 영혼의 주파수를 걸고, ",
            });

            text.evidencePresenceLines.AddRange(new[]
            {
                "{time}에 {place}에서 찍힌 단체 사진 구석에 {suspect}의 얼굴이 또렷하다.",
                "{place} 출입 명부 {time} 칸에 {suspect}의 서명이 있다. 글씨가 너무 개성 있어서 위조는 불가능.",
                "{time}에 {place}에서 {suspect}한테 길을 물어봤다는 신입생의 증언. 대답은 불친절했다고 한다.",
            });
            text.evidenceSawLines.AddRange(new[]
            {
                "{time}에 {place}에서 {suspect}, {suspect2} 둘이 같이 있는 걸 봤다는 증언이 있다.",
                "{place}의 {time} 사진에 {suspect} 옆자리로 {suspect2}의 뒤통수가 찍혀 있다.",
                "{time}에 {place}에서 {suspect}한테 {suspect2}의 목소리로 누가 말을 걸었다고 한다. 목소리가 커서 다 들렸다고.",
            });
            text.evidenceHandledLines.AddRange(new[]
            {
                "{place}에 있던 {item}에서 {suspect}의 이름표가 나왔다. {time}쯤 떨어뜨린 듯하다.",
                "{item}에 {suspect}의 손자국이 남아 있다. {time}에 {place}에서 묻은 페인트와 색이 같다.",
                "{time}에 {place}에서 {suspect}의 손에 '{item}' 대여증이 들려 있었다는 목격담.",
            });
            text.evidenceAbsenceLines.AddRange(new[]
            {
                "{time}에 {place}에 있던 사람들한테 전부 물어봤다. {suspect} 본 사람은 한 명도 없다.",
                "{time}에 {place}에서 찍힌 영상을 세 번 돌려 봤는데, {suspect}의 그림자도 없다.",
                "{place} 당번이 {time} 내내 자리를 지켰다. \"{suspect}? 안 왔는데요.\"",
            });

            AssetDatabase.CreateAsset(text, $"{Folder}/TestimonyText.asset");
            log.Created("증언 문장", "TestimonyText");
        }

        // ---- 진행 흐름 ----
        // 짧은 사건: 의뢰 대사 → 조사 → 거짓말한 용의자 1명의 증언(반박 필수) → 지목(모든 축) → 결과.
        // 조사 행동력은 덮어쓰지 않고 템플릿 값을 쓴다. 생성기가 그 값 기준으로 풀 수 있음을 보장하기 때문이다.

        static CaseFlow BuildShortFlow(BuildLog log)
        {
            var existing = PackAssetOps.LoadAll<CaseFlow>(Folder);
            if (existing.Count > 0)
            {
                log.Skipped("흐름", existing[0].name);
                return existing[0];
            }

            var flow = ScriptableObject.CreateInstance<CaseFlow>();
            flow.phases.Add(new DialogueDef
            {
                speaker = RoleSlot.Client,
                lines =
                {
                    "{request}",
                    "탐정님들만 믿을게요. …사례비는, 그, 축제 식권으로 드려도 될까요?",
                },
            });
            flow.phases.Add(new InvestigateDef());
            flow.phases.Add(new TestimonyDef { speaker = RoleSlot.AnyLiar, mustRebut = true });
            flow.phases.Add(new AccuseDef());
            flow.phases.Add(new ResultDef());

            string folder = PackAssetOps.EnsureFolder(Folder, "Flows");
            AssetDatabase.CreateAsset(flow, $"{folder}/Flow_Short.asset");
            log.Created("흐름", "Flow_Short");
            return flow;
        }

        // ---- Console 출력 ----

        sealed class BuildLog
        {
            readonly List<string> notes = new List<string>();
            readonly List<string> created = new List<string>();
            readonly List<string> skipped = new List<string>();

            public void Note(string text) => notes.Add(text);
            public void Created(string kind, string id) => created.Add($"{kind} {id}");
            public void Skipped(string kind, string id) => skipped.Add($"{kind} {id}");

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"[학원 축제 팩] 새로 만듦 {created.Count}개, 이미 있어서 건너뜀 {skipped.Count}개  ({Folder})\n");
                if (created.Count > 0) sb.Append("만든 것: ").Append(string.Join(", ", created)).Append('\n');
                if (skipped.Count > 0) sb.Append("건너뛴 것: ").Append(string.Join(", ", skipped)).Append('\n');
                foreach (var note in notes) sb.Append(note).Append('\n');
                return sb.ToString();
            }
        }
    }
}
