using System;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Data
{
    // CaseTemplate + WorldPack 에셋 → Core가 쓰는 CaseTemplateData.
    //
    // 여기서는 변환이 불가능한 구조적 오류(빈 id, 중복 id, 끊긴 참조, 팩에 없는 태그, 타입에 필요한 칸 누락)만
    // 모아서 PackConversionException으로 던진다. 후보 부족이나 쓸 수 없는 규칙 같은 내용 균형 문제는
    // 에디터 검증 단계의 몫이다.
    public static class PackConverter
    {
        static readonly StringComparer Ids = StringComparer.Ordinal;

        public static CaseTemplateData Convert(CaseTemplate template, WorldPack pack)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (pack == null) throw new ArgumentNullException(nameof(pack));

            var errors = new List<string>();

            var tags = ConvertTags(pack, errors, out var packTags);
            var entities = ConvertEntities(pack, packTags, errors, out var packEntities);
            var suspects = ConvertSuspects(pack, packEntities, errors);
            var axes = ConvertAxes(template, packTags, entities, errors);
            var rules = ConvertRules(template, packTags, packEntities, errors);

            var timeSlots = new List<string>();
            if (template.timeSlots != null)
            {
                for (int i = 0; i < template.timeSlots.Count; i++)
                {
                    if (string.IsNullOrEmpty(template.timeSlots[i]))
                        errors.Add($"템플릿 '{template.name}': {i}번 시간대가 비어 있습니다.");
                    timeSlots.Add(template.timeSlots[i]);
                }
            }

            if (string.IsNullOrEmpty(template.id)) errors.Add($"템플릿 '{template.name}': id가 비어 있습니다.");
            if (template.crimeSlotIndex < 0 || template.crimeSlotIndex >= timeSlots.Count)
                errors.Add($"템플릿 '{template.name}': 사건 시간대 번호 {template.crimeSlotIndex}가 시간대 범위(0~{timeSlots.Count - 1}) 밖입니다.");
            if (template.actionPoints < 1) errors.Add($"템플릿 '{template.name}': 행동력은 1 이상이어야 합니다.");
            if (template.lieCount < 0) errors.Add($"템플릿 '{template.name}': 거짓 증언 수는 0 이상이어야 합니다.");
            if (template.redHerringCount < 0) errors.Add($"템플릿 '{template.name}': 가짜 단서 수는 0 이상이어야 합니다.");
            if (template.actionMargin < 0) errors.Add($"템플릿 '{template.name}': 행동력 여유값은 0 이상이어야 합니다.");

            if (errors.Count > 0) throw new PackConversionException(errors);

            return new CaseTemplateData(
                template.id, template.title, template.requestText,
                axes, rules, timeSlots, template.crimeSlotIndex,
                template.actionPoints, template.lieCount, template.redHerringCount, template.difficulty,
                tags, entities, suspects,
                template.actionMargin, ConvertTestimonyText(pack.testimonyText));
        }

        // 없으면 null.
        public static TestimonyTextData ConvertTestimonyText(TestimonyText text)
        {
            if (text == null) return null;
            return new TestimonyTextData(
                text.presenceLines, text.sawLines, text.handledLines, text.exaggerationPrefixes,
                text.evidencePresenceLines, text.evidenceSawLines, text.evidenceHandledLines, text.evidenceAbsenceLines);
        }

        // ---- 태그 ----

        static List<TagData> ConvertTags(WorldPack pack, List<string> errors, out HashSet<TagDef> packTags)
        {
            packTags = new HashSet<TagDef>();
            var seenIds = new HashSet<string>(Ids);
            var result = new List<TagData>();
            if (pack.tags == null) return result;

            for (int i = 0; i < pack.tags.Count; i++)
            {
                var tag = pack.tags[i];
                if (tag == null) { errors.Add($"팩 '{pack.name}': 태그 목록 {i}번이 비어 있습니다."); continue; }
                if (!packTags.Add(tag)) continue; // 같은 에셋이 두 번 들어간 것은 무시

                if (string.IsNullOrEmpty(tag.id)) { errors.Add($"태그 '{tag.name}': id가 비어 있습니다."); continue; }
                if (!seenIds.Add(tag.id)) { errors.Add($"태그 id '{tag.id}'가 중복되었습니다 ('{tag.name}')."); continue; }
                if (string.IsNullOrEmpty(tag.category)) errors.Add($"태그 '{tag.id}': 카테고리가 비어 있습니다.");

                result.Add(new TagData(tag.id, tag.displayName, tag.category));
            }

            result.Sort((a, b) => Ids.Compare(a.Id, b.Id));
            return result;
        }

        // ---- 엔티티 ----

        static List<EntityData> ConvertEntities(WorldPack pack, HashSet<TagDef> packTags, List<string> errors,
            out HashSet<EntityDef> packEntities)
        {
            packEntities = new HashSet<EntityDef>();
            var seenIds = new HashSet<string>(Ids);
            var result = new List<EntityData>();
            if (pack.entities == null) return result;

            for (int i = 0; i < pack.entities.Count; i++)
            {
                var entity = pack.entities[i];
                if (entity == null) { errors.Add($"팩 '{pack.name}': 엔티티 목록 {i}번이 비어 있습니다."); continue; }
                if (!packEntities.Add(entity)) continue;

                if (string.IsNullOrEmpty(entity.id)) { errors.Add($"엔티티 '{entity.name}': id가 비어 있습니다."); continue; }
                if (!seenIds.Add(entity.id)) { errors.Add($"엔티티 id '{entity.id}'가 중복되었습니다 ('{entity.name}')."); continue; }

                var tagIds = new SortedSet<string>(Ids);
                if (entity.tags != null)
                {
                    for (int t = 0; t < entity.tags.Count; t++)
                    {
                        var tag = entity.tags[t];
                        if (tag == null) { errors.Add($"엔티티 '{entity.id}': 태그 {t}번이 비어 있습니다."); continue; }
                        if (!packTags.Contains(tag)) { errors.Add($"엔티티 '{entity.id}': 태그 '{tag.name}'가 팩의 태그 목록에 없습니다."); continue; }
                        tagIds.Add(tag.id);
                    }
                }

                result.Add(new EntityData(entity.id, entity.displayName, entity.kind, tagIds, entity.description));
            }

            result.Sort((a, b) => Ids.Compare(a.Id, b.Id));
            return result;
        }

        // ---- 용의자 프로필 ----

        static List<SuspectData> ConvertSuspects(WorldPack pack, HashSet<EntityDef> packEntities, List<string> errors)
        {
            var seenEntities = new HashSet<string>(Ids);
            var result = new List<SuspectData>();
            if (pack.suspectProfiles == null) return result;

            for (int i = 0; i < pack.suspectProfiles.Count; i++)
            {
                var profile = pack.suspectProfiles[i];
                if (profile == null) { errors.Add($"팩 '{pack.name}': 용의자 프로필 목록 {i}번이 비어 있습니다."); continue; }

                var entity = profile.entity;
                if (entity == null) { errors.Add($"용의자 프로필 '{profile.name}': 대상 엔티티가 비어 있습니다."); continue; }
                if (!packEntities.Contains(entity)) { errors.Add($"용의자 프로필 '{profile.name}': 엔티티 '{entity.name}'가 팩의 엔티티 목록에 없습니다."); continue; }
                if (entity.kind != EntityKind.Suspect) { errors.Add($"용의자 프로필 '{profile.name}': 엔티티 '{entity.id}'의 종류가 Suspect가 아닙니다."); continue; }
                if (string.IsNullOrEmpty(entity.id)) continue; // 엔티티 단계에서 이미 오류로 기록됨
                if (!seenEntities.Add(entity.id)) { errors.Add($"엔티티 '{entity.id}'에 용의자 프로필이 둘 이상 있습니다."); continue; }

                result.Add(new SuspectData(entity.id, profile.catchphrase, profile.lieStyle, profile.smallTalkLines));
            }

            result.Sort((a, b) => Ids.Compare(a.EntityId, b.EntityId));
            return result;
        }

        // ---- 축 ----

        static List<AxisPoolData> ConvertAxes(CaseTemplate template, HashSet<TagDef> packTags,
            List<EntityData> entities, List<string> errors)
        {
            var result = new List<AxisPoolData>();
            var seenIds = new HashSet<string>(Ids);
            if (template.axes == null || template.axes.Count == 0)
            {
                errors.Add($"템플릿 '{template.name}': 축이 하나도 없습니다.");
                return result;
            }

            for (int i = 0; i < template.axes.Count; i++)
            {
                var slot = template.axes[i];
                if (slot == null) { errors.Add($"템플릿 '{template.name}': {i}번 축이 비어 있습니다."); continue; }
                if (string.IsNullOrEmpty(slot.axisId)) { errors.Add($"템플릿 '{template.name}': {i}번 축의 id가 비어 있습니다."); continue; }
                if (!seenIds.Add(slot.axisId)) { errors.Add($"템플릿 '{template.name}': 축 id '{slot.axisId}'가 중복되었습니다."); continue; }
                if (slot.pickCount < 1) errors.Add($"템플릿 '{template.name}': 축 '{slot.axisId}'의 뽑을 수는 1 이상이어야 합니다.");

                var required = new SortedSet<string>(Ids);
                if (slot.requiredTags != null)
                {
                    for (int t = 0; t < slot.requiredTags.Count; t++)
                    {
                        var tag = slot.requiredTags[t];
                        if (tag == null) { errors.Add($"템플릿 '{template.name}': 축 '{slot.axisId}'의 필수 태그 {t}번이 비어 있습니다."); continue; }
                        if (!packTags.Contains(tag)) { errors.Add($"템플릿 '{template.name}': 축 '{slot.axisId}'의 필수 태그 '{tag.name}'가 팩의 태그 목록에 없습니다."); continue; }
                        required.Add(tag.id);
                    }
                }

                // entities는 이미 id 순이므로 후보도 id 순이 된다.
                var candidates = new List<string>();
                foreach (var entity in entities)
                {
                    if (entity.Kind != slot.kind) continue;
                    if (HasAll(entity.TagIds, required)) candidates.Add(entity.Id);
                }

                result.Add(new AxisPoolData(slot.axisId, slot.kind, required, slot.pickCount, candidates));
            }
            return result;
        }

        static bool HasAll(IReadOnlyList<string> sortedTagIds, SortedSet<string> required)
        {
            foreach (var tagId in required)
            {
                bool found = false;
                for (int i = 0; i < sortedTagIds.Count; i++)
                {
                    if (Ids.Equals(sortedTagIds[i], tagId)) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        // ---- 단서 규칙 ----

        static List<ClueRuleData> ConvertRules(CaseTemplate template, HashSet<TagDef> packTags,
            HashSet<EntityDef> packEntities, List<string> errors)
        {
            var result = new List<ClueRuleData>();
            var seenRules = new HashSet<ClueRule>();
            var seenIds = new HashSet<string>(Ids);
            if (template.rules == null) return result;

            for (int i = 0; i < template.rules.Count; i++)
            {
                var rule = template.rules[i];
                if (rule == null) { errors.Add($"템플릿 '{template.name}': 규칙 목록 {i}번이 비어 있습니다."); continue; }
                if (!seenRules.Add(rule)) continue;
                if (string.IsNullOrEmpty(rule.id)) { errors.Add($"규칙 '{rule.name}': id가 비어 있습니다."); continue; }
                if (!seenIds.Add(rule.id)) { errors.Add($"템플릿 '{template.name}': 규칙 id '{rule.id}'가 중복되었습니다."); continue; }

                var data = ConvertRule(rule, packTags, packEntities, errors);
                if (data != null) result.Add(data);
            }

            result.Sort((a, b) => Ids.Compare(a.Id, b.Id));
            return result;
        }

        static ClueRuleData ConvertRule(ClueRule rule, HashSet<TagDef> packTags, HashSet<EntityDef> packEntities,
            List<string> errors)
        {
            var type = rule.constraintType;
            bool usesAxisB = ConstraintTypes.UsesAxisB(type);
            bool usesTag = ConstraintTypes.UsesTag(type);
            bool usesX = ConstraintTypes.UsesOptionX(type);
            bool usesY = ConstraintTypes.UsesOptionY(type);

            int errorCount = errors.Count;
            string context = $"규칙 '{rule.id}' ({type})";

            if (string.IsNullOrEmpty(rule.axisA)) errors.Add($"{context}: 축 A가 비어 있습니다.");
            string axisB = null;
            if (usesAxisB)
            {
                axisB = rule.axisB;
                if (string.IsNullOrEmpty(axisB)) errors.Add($"{context}: 축 B가 비어 있습니다.");
                else if (Ids.Equals(axisB, rule.axisA)) errors.Add($"{context}: 축 A와 축 B가 같습니다.");
            }

            string tagId = null, tagCategory = null;
            bool tagOpen = false;
            if (usesTag)
            {
                tagOpen = rule.openTag;
                if (tagOpen)
                {
                    tagCategory = string.IsNullOrEmpty(rule.tagCategory) ? null : rule.tagCategory;
                }
                else if (rule.tag == null) errors.Add($"{context}: 태그가 비어 있습니다 (생성 시 채우려면 Open Tag를 켜세요).");
                else if (!packTags.Contains(rule.tag)) errors.Add($"{context}: 태그 '{rule.tag.name}'가 팩의 태그 목록에 없습니다.");
                else tagId = rule.tag.id;
            }
            else if (type == ConstraintType.SameTag)
            {
                if (string.IsNullOrEmpty(rule.tagCategory)) errors.Add($"{context}: 태그 카테고리가 비어 있습니다.");
                else tagCategory = rule.tagCategory;
            }

            string optionX = usesX ? ResolveOption(rule.openOptionX, rule.optionX, "X", context, packEntities, errors) : null;
            string optionY = usesY ? ResolveOption(rule.openOptionY, rule.optionY, "Y", context, packEntities, errors) : null;

            if (errors.Count > errorCount) return null;

            return new ClueRuleData(
                rule.id, type,
                rule.axisA, axisB,
                tagId, tagCategory, optionX, optionY,
                tagOpen, usesX && rule.openOptionX, usesY && rule.openOptionY,
                rule.textVariants, rule.sources & ClueSources.All,
                rule.difficultyWeight, rule.easyWeight, rule.hardWeight);
        }

        static string ResolveOption(bool open, EntityDef entity, string xy, string context,
            HashSet<EntityDef> packEntities, List<string> errors)
        {
            if (open) return null;
            if (entity == null) { errors.Add($"{context}: 옵션 {xy}가 비어 있습니다 (생성 시 채우려면 Open Option {xy}를 켜세요)."); return null; }
            if (!packEntities.Contains(entity)) { errors.Add($"{context}: 옵션 {xy} '{entity.name}'가 팩의 엔티티 목록에 없습니다."); return null; }
            return entity.id;
        }
    }
}
