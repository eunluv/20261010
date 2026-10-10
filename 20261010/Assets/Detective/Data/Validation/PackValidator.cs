using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Detective.Core.Model;

namespace Detective.Data
{
    // 팩 전체를 훑어 빠진 값, 끊긴 연결, 후보 부족, 쓸 수 없는 규칙을 찾는다 (tool-design 8-1).
    public static class PackValidator
    {
        static readonly StringComparer Ids = StringComparer.Ordinal;
        static readonly Regex VariablePattern = new Regex(@"\{([^{}]*)\}");

        // 문장에 쓸 수 있는 변수. 렌더러가 아는 것과 같아야 하므로 렌더러의 목록을 그대로 쓴다.
        public static IReadOnlyList<string> TextVariables => Detective.Core.Logic.ClueTextRenderer.Variables;

        public static List<PackProblem> Validate(WorldPack pack)
        {
            var problems = new List<PackProblem>();
            if (pack == null) return problems;

            if (string.IsNullOrEmpty(pack.displayName)) Warn(problems, pack, "팩 이름이 비어 있습니다.");

            var tagSet = new HashSet<TagDef>();
            var categories = new HashSet<string>(Ids);
            ValidateTags(pack, problems, tagSet, categories);

            var entitySet = new HashSet<EntityDef>();
            ValidateEntities(pack, problems, tagSet, entitySet);
            ValidateProfiles(pack, problems, entitySet);

            var ruleSet = new HashSet<ClueRule>();
            ValidateRules(pack, problems, tagSet, categories, entitySet, ruleSet);
            ValidateTemplates(pack, problems, tagSet, ruleSet);
            ValidateTestimonyText(pack, problems);

            return problems;
        }

        // 축 하나의 후보 풀: 종류가 맞고 필수 태그를 모두 가진 팩의 엔티티.
        public static List<EntityDef> Candidates(WorldPack pack, CaseTemplate.AxisSlot slot)
        {
            var result = new List<EntityDef>();
            if (pack == null || slot == null || pack.entities == null) return result;

            foreach (var entity in pack.entities)
            {
                if (entity == null || entity.kind != slot.kind) continue;
                bool hasAll = true;
                if (slot.requiredTags != null)
                {
                    foreach (var tag in slot.requiredTags)
                    {
                        if (tag == null) continue;
                        if (entity.tags == null || !entity.tags.Contains(tag)) { hasAll = false; break; }
                    }
                }
                if (hasAll) result.Add(entity);
            }
            return result;
        }

        // 문장에서 렌더러가 모르는 변수를 찾는다.
        public static List<string> FindUnknownVariables(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var known = TextVariables;
            foreach (Match match in VariablePattern.Matches(text))
            {
                string name = match.Groups[1].Value;
                bool isKnown = false;
                for (int i = 0; i < known.Count; i++)
                    if (Ids.Equals(known[i], name)) { isKnown = true; break; }
                if (!isKnown && !result.Contains(name)) result.Add(name);
            }
            return result;
        }

        // ---- 태그 ----

        static void ValidateTags(WorldPack pack, List<PackProblem> problems, HashSet<TagDef> tagSet, HashSet<string> categories)
        {
            if (pack.tags == null) return;

            var used = new HashSet<TagDef>();
            if (pack.entities != null)
            {
                foreach (var entity in pack.entities)
                {
                    if (entity == null || entity.tags == null) continue;
                    foreach (var tag in entity.tags)
                        if (tag != null) used.Add(tag);
                }
            }

            var seenIds = new HashSet<string>(Ids);
            for (int i = 0; i < pack.tags.Count; i++)
            {
                var tag = pack.tags[i];
                if (tag == null) { Error(problems, pack, $"태그 목록 {i}번이 비어 있습니다."); continue; }
                if (!tagSet.Add(tag)) continue;

                string label = Label(tag.displayName, tag.id, tag.name);
                if (string.IsNullOrEmpty(tag.id)) Error(problems, tag, $"태그 '{label}': id가 비어 있습니다.");
                else if (!seenIds.Add(tag.id)) Error(problems, tag, $"태그 '{label}': id '{tag.id}'가 다른 태그와 겹칩니다.");

                if (string.IsNullOrEmpty(tag.displayName)) Warn(problems, tag, $"태그 '{label}': 표시 이름이 비어 있습니다.");
                if (string.IsNullOrEmpty(tag.category)) Error(problems, tag, $"태그 '{label}': 카테고리가 비어 있습니다.");
                else categories.Add(tag.category);

                if (!used.Contains(tag)) Warn(problems, tag, $"태그 '{label}': 어떤 엔티티에도 붙어 있지 않습니다.");
            }
        }

        // ---- 엔티티 ----

        static void ValidateEntities(WorldPack pack, List<PackProblem> problems, HashSet<TagDef> tagSet, HashSet<EntityDef> entitySet)
        {
            if (pack.entities == null) return;

            var withProfile = new HashSet<EntityDef>();
            if (pack.suspectProfiles != null)
            {
                foreach (var profile in pack.suspectProfiles)
                    if (profile != null && profile.entity != null) withProfile.Add(profile.entity);
            }

            var seenIds = new HashSet<string>(Ids);
            for (int i = 0; i < pack.entities.Count; i++)
            {
                var entity = pack.entities[i];
                if (entity == null) { Error(problems, pack, $"엔티티 목록 {i}번이 비어 있습니다."); continue; }
                if (!entitySet.Add(entity)) continue;

                string label = Label(entity.displayName, entity.id, entity.name);
                if (string.IsNullOrEmpty(entity.id)) Error(problems, entity, $"엔티티 '{label}': id가 비어 있습니다.");
                else if (!seenIds.Add(entity.id)) Error(problems, entity, $"엔티티 '{label}': id '{entity.id}'가 다른 엔티티와 겹칩니다.");

                if (string.IsNullOrEmpty(entity.displayName)) Warn(problems, entity, $"엔티티 '{label}': 표시 이름이 비어 있습니다.");

                if (entity.tags != null)
                {
                    foreach (var tag in entity.tags)
                    {
                        if (tag == null) Error(problems, entity, $"엔티티 '{label}': 연결이 끊긴 태그가 있습니다.");
                        else if (!tagSet.Contains(tag)) Error(problems, entity, $"엔티티 '{label}': 태그 '{tag.name}'가 이 팩의 태그가 아닙니다.");
                    }
                }

                if (entity.kind == EntityKind.Suspect && !withProfile.Contains(entity))
                    Warn(problems, entity, $"용의자 '{label}': 용의자 프로필(말버릇·거짓말 성향)이 없습니다.");
            }
        }

        static void ValidateProfiles(WorldPack pack, List<PackProblem> problems, HashSet<EntityDef> entitySet)
        {
            if (pack.suspectProfiles == null) return;

            var seen = new HashSet<EntityDef>();
            for (int i = 0; i < pack.suspectProfiles.Count; i++)
            {
                var profile = pack.suspectProfiles[i];
                if (profile == null) { Error(problems, pack, $"용의자 프로필 목록 {i}번이 비어 있습니다."); continue; }

                var entity = profile.entity;
                if (entity == null) { Error(problems, profile, $"용의자 프로필 '{profile.name}': 대상 엔티티가 비어 있습니다."); continue; }

                string label = Label(entity.displayName, entity.id, entity.name);
                if (!entitySet.Contains(entity)) Error(problems, profile, $"용의자 프로필 '{profile.name}': 엔티티 '{label}'가 이 팩의 엔티티가 아닙니다.");
                if (entity.kind != EntityKind.Suspect) Error(problems, profile, $"'{label}': 종류가 Suspect가 아닌데 용의자 프로필이 붙어 있습니다.");
                if (!seen.Add(entity)) Error(problems, profile, $"'{label}': 용의자 프로필이 둘 이상입니다.");
            }
        }

        // ---- 단서 규칙 ----

        static void ValidateRules(WorldPack pack, List<PackProblem> problems, HashSet<TagDef> tagSet, HashSet<string> categories,
            HashSet<EntityDef> entitySet, HashSet<ClueRule> ruleSet)
        {
            if (pack.rules == null) return;

            var usedByTemplate = new HashSet<ClueRule>();
            if (pack.templates != null)
            {
                foreach (var template in pack.templates)
                {
                    if (template == null || template.rules == null) continue;
                    foreach (var rule in template.rules)
                        if (rule != null) usedByTemplate.Add(rule);
                }
            }

            var seenIds = new HashSet<string>(Ids);
            for (int i = 0; i < pack.rules.Count; i++)
            {
                var rule = pack.rules[i];
                if (rule == null) { Error(problems, pack, $"규칙 목록 {i}번이 비어 있습니다."); continue; }
                if (!ruleSet.Add(rule)) continue;

                string label = Label(null, rule.id, rule.name);
                var type = rule.constraintType;

                if (string.IsNullOrEmpty(rule.id)) Error(problems, rule, $"규칙 '{label}': id가 비어 있습니다.");
                else if (!seenIds.Add(rule.id)) Error(problems, rule, $"규칙 '{label}': id가 다른 규칙과 겹칩니다.");

                if (string.IsNullOrEmpty(rule.axisA)) Error(problems, rule, $"규칙 '{label}': 축 A가 비어 있습니다.");
                if (ConstraintTypes.UsesAxisB(type))
                {
                    if (string.IsNullOrEmpty(rule.axisB)) Error(problems, rule, $"규칙 '{label}': {type}은 축 B가 필요합니다.");
                    else if (Ids.Equals(rule.axisA, rule.axisB)) Error(problems, rule, $"규칙 '{label}': 축 A와 축 B가 같습니다.");
                }

                if (ConstraintTypes.UsesTag(type))
                {
                    if (rule.openTag)
                    {
                        if (!string.IsNullOrEmpty(rule.tagCategory) && !categories.Contains(rule.tagCategory))
                            Error(problems, rule, $"규칙 '{label}': 카테고리 '{rule.tagCategory}'인 태그가 팩에 없습니다.");
                    }
                    else if (rule.tag == null) Error(problems, rule, $"규칙 '{label}': 태그가 비어 있습니다.");
                    else if (!tagSet.Contains(rule.tag)) Error(problems, rule, $"규칙 '{label}': 태그 '{rule.tag.name}'가 이 팩의 태그가 아닙니다.");
                }
                else if (type == ConstraintType.SameTag)
                {
                    if (string.IsNullOrEmpty(rule.tagCategory)) Error(problems, rule, $"규칙 '{label}': SameTag는 태그 카테고리가 필요합니다.");
                    else if (!categories.Contains(rule.tagCategory)) Error(problems, rule, $"규칙 '{label}': 카테고리 '{rule.tagCategory}'인 태그가 팩에 없습니다.");
                }

                bool fixedX = ConstraintTypes.UsesOptionX(type) && !rule.openOptionX;
                bool fixedY = ConstraintTypes.UsesOptionY(type) && !rule.openOptionY;
                if (fixedX) CheckFixedOption(problems, rule, label, "X", rule.optionX, entitySet);
                if (fixedY) CheckFixedOption(problems, rule, label, "Y", rule.optionY, entitySet);
                if (type == ConstraintType.OneOf && fixedX && fixedY && rule.optionX != null && rule.optionX == rule.optionY)
                    Error(problems, rule, $"규칙 '{label}': OneOf의 옵션 X와 Y가 같습니다.");

                if (rule.textVariants == null || rule.textVariants.Count == 0)
                {
                    Warn(problems, rule, $"규칙 '{label}': 문장 변형이 하나도 없습니다.");
                }
                else
                {
                    for (int t = 0; t < rule.textVariants.Count; t++)
                    {
                        string text = rule.textVariants[t];
                        if (string.IsNullOrWhiteSpace(text)) { Warn(problems, rule, $"규칙 '{label}': 문장 {t + 1}번이 비어 있습니다."); continue; }
                        foreach (var variable in FindUnknownVariables(text))
                            Error(problems, rule, $"규칙 '{label}': 문장 {t + 1}번에 알 수 없는 변수 {{{variable}}}가 있습니다.");
                    }
                }

                if ((rule.sources & ClueSources.All) == ClueSource.None) Warn(problems, rule, $"규칙 '{label}': 획득 경로가 하나도 선택되지 않았습니다.");
                if (rule.difficultyWeight <= 0f) Warn(problems, rule, $"규칙 '{label}': 기본 가중치가 0이라 뽑히지 않습니다.");
                if (!usedByTemplate.Contains(rule)) Warn(problems, rule, $"규칙 '{label}': 어떤 사건 템플릿에서도 쓰이지 않습니다.");
            }
        }

        static void CheckFixedOption(List<PackProblem> problems, ClueRule rule, string label, string xy, EntityDef option,
            HashSet<EntityDef> entitySet)
        {
            if (option == null) Error(problems, rule, $"규칙 '{label}': 옵션 {xy}가 비어 있습니다 (생성 시 채우려면 '생성 시 고름'을 켜세요).");
            else if (!entitySet.Contains(option)) Error(problems, rule, $"규칙 '{label}': 옵션 {xy} '{option.name}'가 이 팩의 엔티티가 아닙니다.");
        }

        // ---- 사건 템플릿 ----

        static void ValidateTemplates(WorldPack pack, List<PackProblem> problems, HashSet<TagDef> tagSet, HashSet<ClueRule> ruleSet)
        {
            if (pack.templates == null) return;

            var seenIds = new HashSet<string>(Ids);
            for (int i = 0; i < pack.templates.Count; i++)
            {
                var template = pack.templates[i];
                if (template == null) { Error(problems, pack, $"템플릿 목록 {i}번이 비어 있습니다."); continue; }

                string label = Label(template.title, template.id, template.name);
                if (string.IsNullOrEmpty(template.id)) Error(problems, template, $"템플릿 '{label}': id가 비어 있습니다.");
                else if (!seenIds.Add(template.id)) Error(problems, template, $"템플릿 '{label}': id '{template.id}'가 다른 템플릿과 겹칩니다.");
                if (string.IsNullOrEmpty(template.title)) Warn(problems, template, $"템플릿 '{label}': 제목이 비어 있습니다.");
                if (string.IsNullOrEmpty(template.requestText)) Warn(problems, template, $"템플릿 '{label}': 의뢰 문장이 비어 있습니다.");

                var axisKinds = new Dictionary<string, EntityKind>(Ids);
                var axisPools = new Dictionary<string, List<EntityDef>>(Ids);
                if (template.axes == null || template.axes.Count == 0)
                {
                    Error(problems, template, $"템플릿 '{label}': 축이 하나도 없습니다.");
                }
                else
                {
                    for (int a = 0; a < template.axes.Count; a++)
                    {
                        var slot = template.axes[a];
                        if (slot == null) continue;
                        if (string.IsNullOrEmpty(slot.axisId)) { Error(problems, template, $"템플릿 '{label}': {a + 1}번째 축의 id가 비어 있습니다."); continue; }
                        if (axisKinds.ContainsKey(slot.axisId)) { Error(problems, template, $"템플릿 '{label}': 축 id '{slot.axisId}'가 겹칩니다."); continue; }

                        if (slot.requiredTags != null)
                        {
                            foreach (var tag in slot.requiredTags)
                            {
                                if (tag == null) Error(problems, template, $"템플릿 '{label}': 축 '{slot.axisId}'에 연결이 끊긴 필수 태그가 있습니다.");
                                else if (!tagSet.Contains(tag)) Error(problems, template, $"템플릿 '{label}': 축 '{slot.axisId}'의 필수 태그 '{tag.name}'가 이 팩의 태그가 아닙니다.");
                            }
                        }

                        var pool = Candidates(pack, slot);
                        axisKinds.Add(slot.axisId, slot.kind);
                        axisPools.Add(slot.axisId, pool);

                        if (slot.pickCount < 1) Error(problems, template, $"템플릿 '{label}': 축 '{slot.axisId}'의 뽑을 수는 1 이상이어야 합니다.");
                        else if (pool.Count < slot.pickCount)
                            Error(problems, template, $"템플릿 '{label}': 축 '{slot.axisId}'에 맞는 {slot.kind} 후보가 {pool.Count}개뿐인데 {slot.pickCount}개를 뽑으려 합니다.");
                    }
                }

                if (template.rules == null || template.rules.Count == 0)
                {
                    Error(problems, template, $"템플릿 '{label}': 사용할 단서 규칙이 하나도 없습니다.");
                }
                else
                {
                    foreach (var rule in template.rules)
                    {
                        if (rule == null) { Error(problems, template, $"템플릿 '{label}': 연결이 끊긴 규칙이 있습니다."); continue; }
                        if (!ruleSet.Contains(rule)) { Error(problems, template, $"템플릿 '{label}': 규칙 '{rule.name}'가 이 팩의 규칙이 아닙니다."); continue; }
                        ValidateRuleInTemplate(problems, template, label, rule, axisKinds, axisPools);
                    }
                }

                int slotCount = template.timeSlots == null ? 0 : template.timeSlots.Count;
                if (slotCount == 0) Error(problems, template, $"템플릿 '{label}': 시간대가 하나도 없습니다.");
                else
                {
                    for (int t = 0; t < slotCount; t++)
                        if (string.IsNullOrWhiteSpace(template.timeSlots[t])) Error(problems, template, $"템플릿 '{label}': {t + 1}번째 시간대가 비어 있습니다.");
                    if (template.crimeSlotIndex < 0 || template.crimeSlotIndex >= slotCount)
                        Error(problems, template, $"템플릿 '{label}': 사건 시간대가 시간대 목록 범위 밖입니다.");
                }

                if (template.actionPoints < 1) Error(problems, template, $"템플릿 '{label}': 행동력은 1 이상이어야 합니다.");
                if (template.actionMargin < 0) Error(problems, template, $"템플릿 '{label}': 행동력 여유값은 0 이상이어야 합니다.");
                else if (template.actionMargin >= template.actionPoints)
                    Error(problems, template, $"템플릿 '{label}': 행동력 여유값({template.actionMargin})이 행동력({template.actionPoints}) 이상이라 어떤 사건도 만들 수 없습니다.");
                if (template.lieCount < 0) Error(problems, template, $"템플릿 '{label}': 거짓 증언 수는 0 이상이어야 합니다.");
                if (template.redHerringCount < 0) Error(problems, template, $"템플릿 '{label}': 가짜 단서 수는 0 이상이어야 합니다.");
            }
        }

        // 이 템플릿의 축 구성에서 규칙을 쓸 수 있는지.
        static void ValidateRuleInTemplate(List<PackProblem> problems, CaseTemplate template, string label, ClueRule rule,
            Dictionary<string, EntityKind> axisKinds, Dictionary<string, List<EntityDef>> axisPools)
        {
            var type = rule.constraintType;
            string ruleLabel = Label(null, rule.id, rule.name);

            bool hasA = !string.IsNullOrEmpty(rule.axisA) && axisKinds.ContainsKey(rule.axisA);
            bool needsB = ConstraintTypes.UsesAxisB(type);
            bool hasB = needsB && !string.IsNullOrEmpty(rule.axisB) && axisKinds.ContainsKey(rule.axisB);

            if (!hasA && !string.IsNullOrEmpty(rule.axisA))
                Error(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'의 축 '{rule.axisA}'가 이 템플릿에 없습니다.");
            if (needsB && !hasB && !string.IsNullOrEmpty(rule.axisB))
                Error(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'의 축 '{rule.axisB}'가 이 템플릿에 없습니다.");

            if (hasA && ConstraintTypes.UsesOptionX(type) && !rule.openOptionX && rule.optionX != null &&
                rule.optionX.kind != axisKinds[rule.axisA])
                Error(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'의 옵션 X는 {rule.optionX.kind}인데 축 '{rule.axisA}'는 {axisKinds[rule.axisA]}입니다.");

            if (ConstraintTypes.UsesOptionY(type) && !rule.openOptionY && rule.optionY != null)
            {
                bool onA = ConstraintTypes.OptionYOnAxisA(type);
                string axis = onA ? rule.axisA : rule.axisB;
                if ((onA ? hasA : hasB) && rule.optionY.kind != axisKinds[axis])
                    Error(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'의 옵션 Y는 {rule.optionY.kind}인데 축 '{axis}'는 {axisKinds[axis]}입니다.");
            }

            if (!hasA) return;
            var poolA = axisPools[rule.axisA];

            // 문장 변수 대조: 문장이 쓰는 변수를 이 규칙이 채워 줄 수 있는가.
            if ((!needsB || hasB) && rule.textVariants != null)
            {
                var kindA = axisKinds[rule.axisA];
                var kindB = needsB ? axisKinds[rule.axisB] : kindA;
                // 알리바이 단서(첫 용의자 축의 IsNot)는 타임라인이 {place2}를 채워 준다.
                bool alibiPlace = type == ConstraintType.IsNot &&
                                  Ids.Equals(FirstAxisOfKind(template, EntityKind.Suspect), rule.axisA) &&
                                  FirstAxisOfKind(template, EntityKind.Place) != null;
                var fillable = Detective.Core.Logic.ClueTextRenderer.FillableVariables(type, kindA, kindB, alibiPlace);
                var known = TextVariables;

                for (int t = 0; t < rule.textVariants.Count; t++)
                {
                    string text = rule.textVariants[t];
                    if (string.IsNullOrEmpty(text)) continue;

                    var reported = new List<string>();
                    foreach (Match match in VariablePattern.Matches(text))
                    {
                        string name = match.Groups[1].Value;
                        if (fillable.Contains(name) || reported.Contains(name)) continue;

                        bool isKnown = false;
                        for (int i = 0; i < known.Count; i++)
                            if (Ids.Equals(known[i], name)) { isKnown = true; break; }
                        if (!isKnown) continue; // 모르는 변수는 규칙 검사에서 이미 알렸다

                        reported.Add(name);
                        Error(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'의 문장 {t + 1}번에 {{{name}}}이 있지만 이 규칙은 {name}을 채우지 않습니다.");
                    }
                }
            }

            if (ConstraintTypes.UsesTag(type) && !rule.openTag && rule.tag != null && poolA.Count > 0)
            {
                int tagged = 0;
                foreach (var entity in poolA)
                    if (entity.tags != null && entity.tags.Contains(rule.tag)) tagged++;

                if (type == ConstraintType.HasTag && tagged == 0)
                    Warn(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'은 쓸 수 없습니다. 축 '{rule.axisA}' 후보 중 태그 '{rule.tag.id}'를 가진 것이 없습니다.");
                if (type == ConstraintType.LacksTag && tagged == poolA.Count)
                    Warn(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'은 쓸 수 없습니다. 축 '{rule.axisA}' 후보가 모두 태그 '{rule.tag.id}'를 가지고 있습니다.");
            }

            if (type == ConstraintType.SameTag && hasB && !string.IsNullOrEmpty(rule.tagCategory))
            {
                if (!AnyHasCategory(poolA, rule.tagCategory) || !AnyHasCategory(axisPools[rule.axisB], rule.tagCategory))
                    Warn(problems, template, $"템플릿 '{label}': 규칙 '{ruleLabel}'은 쓸 수 없습니다. 두 축 후보가 모두 '{rule.tagCategory}' 카테고리 태그를 가져야 합니다.");
            }
        }

        // 그 종류를 후보로 쓰는 첫 번째 축의 id. 없으면 null.
        static string FirstAxisOfKind(CaseTemplate template, EntityKind kind)
        {
            if (template.axes == null) return null;
            foreach (var slot in template.axes)
                if (slot != null && slot.kind == kind && !string.IsNullOrEmpty(slot.axisId)) return slot.axisId;
            return null;
        }

        // ---- 증언 문장 ----

        static void ValidateTestimonyText(WorldPack pack, List<PackProblem> problems)
        {
            var text = pack.testimonyText;
            if (text == null)
            {
                Warn(problems, pack, "증언 문장(Testimony Text)이 없습니다. 증언과 증거가 문장 없이 만들어집니다.");
                return;
            }

            var testimony = Detective.Core.Logic.FactTextRenderer.TestimonyVariables;
            var evidence = Detective.Core.Logic.FactTextRenderer.EvidenceVariables;
            CheckLines(problems, text, "증언: 있었다", text.presenceLines, testimony, true);
            CheckLines(problems, text, "증언: 봤다", text.sawLines, testimony, true);
            CheckLines(problems, text, "증언: 만졌다", text.handledLines, testimony, false);
            CheckLines(problems, text, "과장 접두어", text.exaggerationPrefixes, new string[0], false);
            CheckLines(problems, text, "증거: 있었다", text.evidencePresenceLines, evidence, true);
            CheckLines(problems, text, "증거: 봤다", text.evidenceSawLines, evidence, false);
            CheckLines(problems, text, "증거: 만졌다", text.evidenceHandledLines, evidence, false);
            CheckLines(problems, text, "증거: 없었다", text.evidenceAbsenceLines, evidence, true);
        }

        static void CheckLines(List<PackProblem> problems, TestimonyText target, string label, List<string> lines,
            IReadOnlyList<string> allowed, bool required)
        {
            if (lines == null || lines.Count == 0)
            {
                if (required) Warn(problems, target, $"증언 문장 '{label}': 문장이 하나도 없습니다.");
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                var reported = new List<string>();
                foreach (Match match in VariablePattern.Matches(lines[i]))
                {
                    string name = match.Groups[1].Value;
                    bool ok = false;
                    for (int k = 0; k < allowed.Count; k++)
                        if (Ids.Equals(allowed[k], name)) { ok = true; break; }
                    if (ok || reported.Contains(name)) continue;
                    reported.Add(name);
                    Error(problems, target, $"증언 문장 '{label}' {i + 1}번: 여기서는 {{{name}}}을 쓸 수 없습니다.");
                }
            }
        }

        static bool AnyHasCategory(List<EntityDef> pool, string category)
        {
            foreach (var entity in pool)
            {
                if (entity.tags == null) continue;
                foreach (var tag in entity.tags)
                    if (tag != null && Ids.Equals(tag.category, category)) return true;
            }
            return false;
        }

        // ---- 헬퍼 ----

        static string Label(string displayName, string id, string assetName)
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            if (!string.IsNullOrEmpty(id)) return id;
            return assetName;
        }

        static void Error(List<PackProblem> problems, UnityEngine.Object target, string message) =>
            problems.Add(new PackProblem(ProblemSeverity.Error, message, target));

        static void Warn(List<PackProblem> problems, UnityEngine.Object target, string message) =>
            problems.Add(new PackProblem(ProblemSeverity.Warning, message, target));
    }
}
