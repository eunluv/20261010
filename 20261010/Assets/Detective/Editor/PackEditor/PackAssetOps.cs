using System;
using System.Collections.Generic;
using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Detective.Editor
{
    // 팩 에셋 파일의 생성·삭제·이름 변경과, 팩 폴더를 훑어 WorldPack 목록을 채우는 일을 맡는다.
    // 팩 폴더 = WorldPack 에셋이 들어 있는 폴더. 그 아래의 에셋은 모두 그 팩의 것으로 본다.
    internal static class PackAssetOps
    {
        public const string PacksRoot = "Assets/Packs";

        public const string TagFolder = "Tags", EntityFolder = "Entities", SuspectFolder = "Suspects",
            RuleFolder = "Rules", TemplateFolder = "Templates";

        public const string TagPrefix = "Tag_", EntityPrefix = "Entity_", SuspectPrefix = "Suspect_",
            RulePrefix = "Rule_", TemplatePrefix = "Case_", PackPrefix = "Pack_";

        static readonly StringComparer Ids = StringComparer.Ordinal;

        // ---- 찾기 ----

        public static List<WorldPack> FindAllPacks() => LoadAll<WorldPack>("Assets");

        public static string FolderOf(WorldPack pack)
        {
            string path = AssetDatabase.GetAssetPath(pack);
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.LastIndexOf('/');
            return slash < 0 ? null : path.Substring(0, slash);
        }

        // 폴더(하위 폴더 포함)에 있는 T 타입 에셋을 경로 순으로 모은다.
        public static List<T> LoadAll<T>(string folder) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(Ids);

            var result = new List<T>();
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && !result.Contains(asset)) result.Add(asset);
            }
            return result;
        }

        // ---- 팩 목록 자동 채우기 ----

        // 팩 폴더 아래의 에셋을 모아 WorldPack의 목록을 id 순으로 다시 채운다. 바뀐 게 있으면 true.
        public static bool Sync(WorldPack pack)
        {
            string folder = FolderOf(pack);
            if (folder == null || folder == "Assets") return false; // 프로젝트 전체를 팩으로 삼지 않는다

            var tags = LoadAll<TagDef>(folder);
            var entities = LoadAll<EntityDef>(folder);
            var profiles = LoadAll<SuspectProfile>(folder);
            var rules = LoadAll<ClueRule>(folder);
            var templates = LoadAll<CaseTemplate>(folder);

            // LoadAll이 경로 순으로 주고 Sort가 안정적이지 않으므로, id가 같을 때는 에셋 이름으로 순서를 정한다.
            tags.Sort((a, b) => Compare(a.id, a.name, b.id, b.name));
            entities.Sort((a, b) => Compare(a.id, a.name, b.id, b.name));
            profiles.Sort((a, b) => Compare(ProfileKey(a), a.name, ProfileKey(b), b.name));
            rules.Sort((a, b) => Compare(a.id, a.name, b.id, b.name));
            templates.Sort((a, b) => Compare(a.id, a.name, b.id, b.name));

            // 증언 문장은 팩마다 하나. 연결이 비어 있을 때만 폴더에서 찾아 채운다.
            var testimonyText = pack.testimonyText;
            if (testimonyText == null)
            {
                var found = LoadAll<TestimonyText>(folder);
                if (found.Count > 0) testimonyText = found[0];
            }

            bool changed = !Same(pack.tags, tags) || !Same(pack.entities, entities) || !Same(pack.suspectProfiles, profiles) ||
                           !Same(pack.rules, rules) || !Same(pack.templates, templates) || pack.testimonyText != testimonyText;
            if (!changed) return false;

            pack.testimonyText = testimonyText;
            pack.tags = tags;
            pack.entities = entities;
            pack.suspectProfiles = profiles;
            pack.rules = rules;
            pack.templates = templates;
            EditorUtility.SetDirty(pack);
            return true;
        }

        static string ProfileKey(SuspectProfile profile) => profile.entity != null ? profile.entity.id : null;

        static int Compare(string idA, string nameA, string idB, string nameB)
        {
            int c = Ids.Compare(idA ?? "", idB ?? "");
            return c != 0 ? c : Ids.Compare(nameA, nameB);
        }

        static bool Same<T>(List<T> a, List<T> b) where T : Object
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // ---- 만들기 ----

        // Assets/Packs/<이름>/Pack_<이름>.asset 을 만든다. 같은 폴더에 이미 팩이 있으면 null.
        public static WorldPack CreatePack(string displayName)
        {
            string safe = SafeFileName(displayName);
            if (string.IsNullOrEmpty(safe)) return null;

            if (!AssetDatabase.IsValidFolder(PacksRoot)) AssetDatabase.CreateFolder("Assets", "Packs");
            string folder = EnsureFolder(PacksRoot, safe);
            if (LoadAll<WorldPack>(folder).Count > 0) return null;

            var pack = ScriptableObject.CreateInstance<WorldPack>();
            pack.displayName = displayName;
            AssetDatabase.CreateAsset(pack, $"{folder}/{PackPrefix}{safe}.asset");
            AssetDatabase.SaveAssets();
            return pack;
        }

        public static TagDef CreateTag(WorldPack pack)
        {
            string category = pack.tags.Count > 0 && pack.tags[pack.tags.Count - 1] != null
                ? pack.tags[pack.tags.Count - 1].category
                : "";
            string id = UniqueId(pack.tags.ConvertAll(t => t != null ? t.id : null), "new_tag");
            return Create<TagDef>(pack, TagFolder, TagPrefix, id, tag =>
            {
                tag.id = id;
                tag.displayName = "새 태그";
                tag.category = category;
            });
        }

        public static EntityDef CreateEntity(WorldPack pack, EntityKind kind)
        {
            string id = UniqueId(pack.entities.ConvertAll(e => e != null ? e.id : null), "new_" + kind.ToString().ToLowerInvariant());
            var entity = Create<EntityDef>(pack, EntityFolder, EntityPrefix, id, e =>
            {
                e.id = id;
                e.displayName = "새 " + KindLabel(kind);
                e.kind = kind;
            });
            if (kind == EntityKind.Suspect) CreateProfile(pack, entity);
            return entity;
        }

        public static SuspectProfile CreateProfile(WorldPack pack, EntityDef entity)
        {
            return Create<SuspectProfile>(pack, SuspectFolder, SuspectPrefix, entity.id, profile => profile.entity = entity);
        }

        public static ClueRule CreateRule(WorldPack pack, string defaultAxis)
        {
            string id = UniqueId(pack.rules.ConvertAll(r => r != null ? r.id : null), "new_rule");
            return Create<ClueRule>(pack, RuleFolder, RulePrefix, id, rule =>
            {
                rule.id = id;
                rule.constraintType = ConstraintType.IsNot;
                rule.axisA = defaultAxis ?? "";
                rule.textVariants.Add("");
            });
        }

        // 새 템플릿은 짧은 사건의 기본 구성(범인·장소·도구 3축, 시간대 4개)으로 시작한다. 만든 뒤 자유롭게 고친다.
        public static CaseTemplate CreateTemplate(WorldPack pack)
        {
            string id = UniqueId(pack.templates.ConvertAll(t => t != null ? t.id : null), "new_case");
            return Create<CaseTemplate>(pack, TemplateFolder, TemplatePrefix, id, template =>
            {
                template.id = id;
                template.title = "새 사건";
                template.axes.Add(new CaseTemplate.AxisSlot { axisId = "culprit", kind = EntityKind.Suspect, pickCount = 4 });
                template.axes.Add(new CaseTemplate.AxisSlot { axisId = "place", kind = EntityKind.Place, pickCount = 4 });
                template.axes.Add(new CaseTemplate.AxisSlot { axisId = "item", kind = EntityKind.Item, pickCount = 4 });
                template.timeSlots.AddRange(new[] { "13시", "14시", "15시", "16시" });
                template.crimeSlotIndex = 2;
                foreach (var rule in pack.rules)
                    if (rule != null) template.rules.Add(rule);
            });
        }

        static T Create<T>(WorldPack pack, string subfolder, string prefix, string id, Action<T> init) where T : ScriptableObject
        {
            string folder = EnsureFolder(FolderOf(pack), subfolder);
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}{SafeFileName(id)}.asset");
            AssetDatabase.CreateAsset(asset, path);
            Sync(pack);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static string EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        static string UniqueId(List<string> existing, string baseId)
        {
            if (!existing.Contains(baseId)) return baseId;
            for (int n = 2; ; n++)
            {
                string candidate = baseId + "_" + n;
                if (!existing.Contains(candidate)) return candidate;
            }
        }

        // ---- 이름 맞추기 ----

        // id가 바뀌면 파일 이름도 <prefix><id>로 맞춘다. 같은 이름이 이미 있으면 그대로 둔다.
        public static void RenameToId(Object asset, string prefix, string id)
        {
            if (asset == null || string.IsNullOrEmpty(id)) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return;

            string newName = prefix + SafeFileName(id);
            if (asset.name == newName) return;
            AssetDatabase.RenameAsset(path, newName);
        }

        public static string SafeFileName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var chars = text.Trim().ToCharArray();
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\') chars[i] = '_';
            return new string(chars);
        }

        // ---- 지우기 (휴지통으로 보내고, 쓰던 곳의 연결을 정리한다) ----

        public static void DeleteTag(WorldPack pack, TagDef tag)
        {
            foreach (var entity in pack.entities)
            {
                if (entity == null || entity.tags == null) continue;
                if (entity.tags.RemoveAll(t => t == tag) > 0) EditorUtility.SetDirty(entity);
            }
            foreach (var template in pack.templates)
            {
                if (template == null || template.axes == null) continue;
                foreach (var slot in template.axes)
                {
                    if (slot == null || slot.requiredTags == null) continue;
                    if (slot.requiredTags.RemoveAll(t => t == tag) > 0) EditorUtility.SetDirty(template);
                }
            }
            foreach (var rule in pack.rules)
            {
                if (rule == null || rule.tag != tag) continue;
                rule.tag = null;
                EditorUtility.SetDirty(rule);
            }
            Trash(pack, tag);
        }

        public static void DeleteEntity(WorldPack pack, EntityDef entity)
        {
            foreach (var profile in new List<SuspectProfile>(pack.suspectProfiles))
            {
                if (profile != null && profile.entity == entity) AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(profile));
            }
            foreach (var rule in pack.rules)
            {
                if (rule == null) continue;
                if (rule.optionX == entity) { rule.optionX = null; EditorUtility.SetDirty(rule); }
                if (rule.optionY == entity) { rule.optionY = null; EditorUtility.SetDirty(rule); }
            }
            Trash(pack, entity);
        }

        public static void DeleteProfile(WorldPack pack, SuspectProfile profile) => Trash(pack, profile);

        public static void DeleteRule(WorldPack pack, ClueRule rule)
        {
            foreach (var template in pack.templates)
            {
                if (template == null || template.rules == null) continue;
                if (template.rules.RemoveAll(r => r == rule) > 0) EditorUtility.SetDirty(template);
            }
            Trash(pack, rule);
        }

        public static void DeleteTemplate(WorldPack pack, CaseTemplate template) => Trash(pack, template);

        static void Trash(WorldPack pack, Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)) AssetDatabase.MoveAssetToTrash(path);
            Sync(pack);
            AssetDatabase.SaveAssets();
        }

        // ---- 표시용 ----

        public static string KindLabel(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.Suspect: return "용의자";
                case EntityKind.Place: return "장소";
                case EntityKind.Item: return "도구";
                case EntityKind.Motive: return "동기";
                default: return kind.ToString();
            }
        }
    }
}
