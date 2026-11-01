using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Fail-closed identity layer for skill references on mastery surfaces (spec
    /// specs/1533-complete-skill-materialization). The legacy validators keep their
    /// case-insensitive semantics and legacy codes; this layer adds exact-name
    /// classification via MortalSkillReferenceAuthority and rejects case-variant or
    /// Unicode-confusable references that the legacy layer would silently accept.
    /// </summary>
    private async Task ValidateSkillReferenceIdentityAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues)
    {
        if (phases.Includes(GameStateValidationPhase.PlayerStateFiles))
            await ValidatePlayerMasterySkillReferenceIdentityAsync(issues);

        if (phases.Includes(GameStateValidationPhase.SkillContractConsistency))
        {
            await ValidatePlayerActiveSkillInitializationIdentityAsync(issues);
            await ValidateNpcSkillInitializationIdentityAsync(
                issues,
                skillChangesSection: "NPCActiveSkillChanges",
                masterySection: "NPCSkillMasteryChanges",
                section: "Skills.Active");
            await ValidateNpcSkillInitializationIdentityAsync(
                issues,
                skillChangesSection: "NPCPassiveSkillChanges",
                masterySection: "NPCPassiveSkillMasteryChanges",
                section: "Skills.Passive");
        }
    }

    private async Task ValidatePlayerMasterySkillReferenceIdentityAsync(List<ValidationIssue> issues)
    {
        const string masteryPath = "game_state/player/skill_mastery.json";
        if (!ShouldValidateStateFile(masteryPath))
            return;

        var masteryJson = await ReadSameTurnOwnerCurrentTextAsync(masteryPath);
        if (string.IsNullOrWhiteSpace(masteryJson))
            return;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(masteryJson);
        }
        catch
        {
            // Malformed JSON is reported by the contract validator.
            return;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("skillMasteryChanges", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var knownActiveSkills = ReadCurrentPlayerActiveSkillNamesSync();
            knownActiveSkills.UnionWith(ParsePlayerSkillNames(
                ReadPreTurnTrackedFileSync("game_state/player/skills_active.json"),
                "activeSkillChanges"));
            if (doc.RootElement.TryGetProperty("removeActiveSkills", out var removeActiveSkills) &&
                removeActiveSkills.ValueKind == JsonValueKind.Array)
            {
                foreach (var removedSkill in removeActiveSkills.EnumerateArray())
                {
                    if (removedSkill.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(removedSkill.GetString()))
                    {
                        knownActiveSkills.Remove(removedSkill.GetString()!);
                    }
                }
            }

            var authority = MortalSkillReferenceAuthority.Build(knownActiveSkills);
            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{masteryPath}.skillMasteryChanges[{index++}]";
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("skillName", out var nameNode) ||
                    nameNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var skillName = nameNode.GetString();
                if (string.IsNullOrWhiteSpace(skillName))
                    continue;

                var match = authority.Classify(skillName);
                if (match.Kind != MortalSkillReferenceMatchKind.Confusable)
                    continue;

                issues.Add(new ValidationIssue(
                    $"{itemContext}.skillName",
                    IssueSeverity.Error,
                    "skillMasteryChanges должен ссылаться на exact canonical skillName, а не на case-variant или Unicode-confusable форму",
                    code: "skill_mastery_confusable_reference",
                    section: "Skills.Active",
                    expected: match.IsAmbiguous
                        ? "exact canonical skillName from game_state/player/skills_active.json (several distinct skills share this normalized form)"
                        : match.CanonicalName!,
                    actual: skillName,
                    repairHint: match.IsAmbiguous
                        ? "Несколько canonical active skills сводятся к одной normalized форме. Перепиши skillName посимвольно на точное имя из game_state/player/skills_active.json. Не удаляй mastery запись — это rename ссылки, а не удаление прогресса."
                        : $"Перепиши skillName посимвольно на точное canonical имя '{match.CanonicalName}' из game_state/player/skills_active.json. Не удаляй mastery запись — это rename ссылки, а не удаление прогресса."));
            }
        }
    }

    private async Task ValidatePlayerActiveSkillInitializationIdentityAsync(List<ValidationIssue> issues)
    {
        const string skillPath = "game_state/player/skills_active.json";
        const string masteryPath = "game_state/player/skill_mastery.json";

        var preSkillJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(skillPath);
        var postSkillJson = await _fs.ReadFileAsync(skillPath);
        var masteryJson = await _fs.ReadFileAsync(masteryPath);

        var preSkills = ParsePlayerSkillNames(preSkillJson, "activeSkillChanges");
        var postSkills = ParsePlayerSkillNames(postSkillJson, "activeSkillChanges");
        if (postSkills.Count == 0)
            return;

        var masteryReferences = MortalSkillReferenceAuthority.Build(
            ParsePlayerSkillMasteryNames(masteryJson));
        foreach (var newSkill in postSkills.Where(skill => !preSkills.Contains(skill)).OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase))
        {
            var match = masteryReferences.Classify(newSkill);
            if (match.Kind != MortalSkillReferenceMatchKind.Confusable)
                continue;

            issues.Add(new ValidationIssue(
                masteryPath,
                IssueSeverity.Error,
                $"Новый active skill '{newSkill}' покрыт mastery initialization только под confusable именем, а не под exact canonical skillName",
                code: "player_active_skill_mastery_initialization_confusable",
                section: "Skills.Active",
                expected: $"skillMasteryChanges contains exact {newSkill}",
                actual: "only a case-variant or Unicode-confusable mastery entry exists",
                repairHint: $"Перепиши skillName соответствующей записи в game_state/player/skill_mastery.json -> skillMasteryChanges посимвольно на '{newSkill}'. Не удаляй mastery запись — initialization обязателен для нового навыка."));
        }
    }

    private async Task ValidateNpcSkillInitializationIdentityAsync(
        List<ValidationIssue> issues,
        string skillChangesSection,
        string masterySection,
        string section)
    {
        const string npcSkillsPath = "game_state/npcs/npc_skills.json";

        var preJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(npcSkillsPath);
        var postJson = await _fs.ReadFileAsync(npcSkillsPath);

        var preSkillsByNpc = ParseNpcSkillChangesByActor(preJson, skillChangesSection);
        var postSkillsByNpc = ParseNpcSkillChangesByActor(postJson, skillChangesSection);
        if (postSkillsByNpc.Count == 0)
            return;

        var masteryByNpc = ParseNpcSkillMasteryByActor(postJson, masterySection);
        foreach (var (npcKey, skillNames) in postSkillsByNpc)
        {
            preSkillsByNpc.TryGetValue(npcKey, out var preSkillNames);
            masteryByNpc.TryGetValue(npcKey, out var masterySkillNames);
            var masteryReferences = MortalSkillReferenceAuthority.Build(
                masterySkillNames ?? (IEnumerable<string>)Array.Empty<string>());

            foreach (var skillName in skillNames.Where(skill => preSkillNames == null || !preSkillNames.Contains(skill)))
            {
                var match = masteryReferences.Classify(skillName);
                if (match.Kind != MortalSkillReferenceMatchKind.Confusable)
                    continue;

                issues.Add(new ValidationIssue(
                    $"{npcSkillsPath}.{masterySection}",
                    IssueSeverity.Error,
                    $"Новый NPC skill '{skillName}' для {npcKey} покрыт mastery initialization только под confusable именем, а не под exact canonical skillName",
                    code: "npc_skill_mastery_initialization_confusable",
                    actor: npcKey,
                    section: section,
                    expected: $"{masterySection} contains {npcKey} + exact {skillName}",
                    actual: "only a case-variant or Unicode-confusable mastery entry exists",
                    repairHint: $"Перепиши skillName соответствующей записи в {masterySection} для {npcKey} посимвольно на '{skillName}'. Не удаляй mastery запись — initialization обязателен для нового NPC skill."));
            }
        }
    }
}
