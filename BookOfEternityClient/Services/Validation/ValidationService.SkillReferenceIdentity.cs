using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Adds exact skill-reference validation and replaces only matching legacy diagnoses.
    /// </summary>
    /// <param name="phases">
    /// The selected phases controlling reference and initialization validation.
    /// </param>
    /// <param name="issues">
    /// The diagnostics collected by the selected validation phases.
    /// </param>
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

    /// <summary>
    /// Rejects confusable player mastery references against retained canonical names.
    /// </summary>
    /// <param name="issues">
    /// The diagnostics to extend and replace only for the same mastery reference.
    /// </param>
    /// <returns>
    /// A task that completes after the selected references are checked.
    /// </returns>
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

            var knownActiveSkills = ReadCurrentPlayerActiveSkillNamesSync(StringComparer.Ordinal);
            knownActiveSkills.UnionWith(ParsePlayerSkillNames(
                ReadPreTurnTrackedFileSync("game_state/player/skills_active.json"),
                "activeSkillChanges", StringComparer.Ordinal));
            if (doc.RootElement.TryGetProperty("removeActiveSkills", out var removeActiveSkills) &&
                removeActiveSkills.ValueKind == JsonValueKind.Array)
            {
                foreach (var removedSkill in removeActiveSkills.EnumerateArray())
                {
                    if (removedSkill.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(removedSkill.GetString()))
                    {
                        knownActiveSkills.RemoveWhere(name => string.Equals(
                            name, removedSkill.GetString(), StringComparison.OrdinalIgnoreCase));
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

                issues.RemoveAll(issue => issue.Code == "skill_mastery_unknown_active_skill" &&
                    string.Equals(issue.FilePath, $"{itemContext}.skillName", StringComparison.Ordinal));
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

    /// <summary>
    /// Checks exact mastery coverage for newly introduced player skills.
    /// </summary>
    /// <param name="issues">
    /// The diagnostics to extend with indexed initialization corrections.
    /// </param>
    /// <returns>
    /// A task that completes after the selected references are checked.
    /// </returns>
    private async Task ValidatePlayerActiveSkillInitializationIdentityAsync(List<ValidationIssue> issues)
    {
        const string skillPath = "game_state/player/skills_active.json";
        const string masteryPath = "game_state/player/skill_mastery.json";

        var preSkillJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(skillPath);
        var postSkillJson = await _fs.ReadFileAsync(skillPath);
        var masteryJson = await _fs.ReadFileAsync(masteryPath);

        var preSkills = ParsePlayerSkillNames(preSkillJson, "activeSkillChanges");
        var postSkills = ParsePlayerSkillNames(postSkillJson, "activeSkillChanges", StringComparer.Ordinal);
        if (postSkills.Count == 0)
            return;

        var masteryReferences = ReadMasteryIdentityReferences(masteryJson, "skillMasteryChanges");
        var canonicalNames = MortalSkillReferenceAuthority.Build(postSkills.Concat(
            ParsePlayerSkillNames(preSkillJson, "activeSkillChanges", StringComparer.Ordinal)));
        foreach (var newSkill in postSkills.Where(skill => !preSkills.Contains(skill)).OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase))
        {
            ReportConfusableInitialization(issues, newSkill, masteryReferences, canonicalNames,
                masteryPath, "skillMasteryChanges", "Skills.Active", actor: null);
        }
    }

    /// <summary>
    /// Checks exact mastery coverage for newly introduced skills of each NPC.
    /// </summary>
    /// <param name="issues">
    /// The diagnostics to extend with actor-specific initialization corrections.
    /// </param>
    /// <param name="skillChangesSection">
    /// The active or passive skill-change array.
    /// </param>
    /// <param name="masterySection">
    /// The corresponding mastery array.
    /// </param>
    /// <param name="section">
    /// The diagnostic active or passive section.
    /// </param>
    /// <returns>
    /// A task that completes after the selected references are checked.
    /// </returns>
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
        var postSkillsByNpc = ParseNpcSkillChangesByActor(postJson, skillChangesSection, StringComparer.Ordinal);
        if (postSkillsByNpc.Count == 0)
            return;

        var preExactSkillsByNpc = ParseNpcSkillChangesByActor(preJson, skillChangesSection, StringComparer.Ordinal);
        var masteryReferences = ReadMasteryIdentityReferences(postJson, masterySection);
        foreach (var (npcKey, skillNames) in postSkillsByNpc)
        {
            preSkillsByNpc.TryGetValue(npcKey, out var preSkillNames);
            preExactSkillsByNpc.TryGetValue(npcKey, out var preExactSkills);
            var canonicalNames = MortalSkillReferenceAuthority.Build(skillNames.Concat(
                preExactSkills ?? (IEnumerable<string>)Array.Empty<string>()));
            var actorReferences = masteryReferences.Where(reference => string.Equals(
                reference.Actor, npcKey, StringComparison.OrdinalIgnoreCase)).ToArray();

            foreach (var skillName in skillNames.Where(skill => preSkillNames == null || !preSkillNames.Contains(skill)))
            {
                ReportConfusableInitialization(issues, skillName, actorReferences, canonicalNames,
                    npcSkillsPath, masterySection, section, npcKey);
            }
        }
    }

    /// <summary>
    /// Reads mastery references without collapsing exact names or array coordinates.
    /// </summary>
    /// <param name="json">
    /// The mastery payload; <see langword="null"/>, empty or malformed input yields no references.
    /// </param>
    /// <param name="section">
    /// The mastery array to inspect.
    /// </param>
    /// <returns>
    /// Valid named references with their existing actor key and original array index.
    /// </returns>
    private static IReadOnlyList<(string SkillName, string? Actor, int Index)> ReadMasteryIdentityReferences(
        string? json, string section)
    {
        var result = new List<(string, string?, int)>();
        if (string.IsNullOrWhiteSpace(json))
            return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(section, out var entries) || entries.ValueKind != JsonValueKind.Array)
                return result;
            var index = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                var entryIndex = index++;
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                var name = GetFirstNonEmptyString(entry, "skillName");
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add((name, BuildNpcIdentityKey(entry), entryIndex));
            }
        }
        catch (JsonException)
        {
            // Structural diagnostics are owned by the existing contract validators.
        }
        return result;
    }

    /// <summary>
    /// Reports indexed confusable-only coverage without choosing an ambiguous target.
    /// </summary>
    /// <param name="issues">
    /// The diagnostics to update, replacing only this skill's legacy missing issue.
    /// </param>
    /// <param name="skillName">
    /// The new canonical skill requiring initialization.
    /// </param>
    /// <param name="references">
    /// The owning actor's mastery references and physical array coordinates.
    /// </param>
    /// <param name="canonicalNames">
    /// The actor's exact canonical skill authority.
    /// </param>
    /// <param name="path">
    /// The mastery file's relative path.
    /// </param>
    /// <param name="arrayName">
    /// The mastery array containing the references.
    /// </param>
    /// <param name="section">
    /// The active or passive diagnostic section.
    /// </param>
    /// <param name="actor">
    /// The existing NPC key, or <see langword="null"/> for the player.
    /// </param>
    private static void ReportConfusableInitialization(
        List<ValidationIssue> issues, string skillName,
        IReadOnlyList<(string SkillName, string? Actor, int Index)> references,
        MortalSkillReferenceAuthority canonicalNames, string path, string arrayName, string section, string? actor)
    {
        var coverage = MortalSkillReferenceAuthority.Build(references.Select(reference => reference.SkillName));
        if (coverage.Classify(skillName).Kind != MortalSkillReferenceMatchKind.Confusable)
            return;

        var legacyCode = actor == null
            ? "player_active_skill_missing_mastery_initialization" : "npc_skill_missing_mastery_initialization";
        var legacyExpected = actor == null
            ? $"skillMasteryChanges contains {skillName}" : $"{arrayName} contains {actor} + {skillName}";
        issues.RemoveAll(issue => issue.Code == legacyCode && issue.FilePath == (actor == null ? path : $"{path}.{arrayName}") &&
            issue.Expected == legacyExpected && issue.Section == section &&
            string.Equals(issue.Actor, actor, StringComparison.OrdinalIgnoreCase));

        var key = MortalLocationIdentityState.BuildConfusableKey(skillName);
        foreach (var reference in references.Where(reference =>
                     MortalLocationIdentityState.BuildConfusableKey(reference.SkillName) == key))
        {
            var match = canonicalNames.Classify(reference.SkillName);
            var target = match.Kind == MortalSkillReferenceMatchKind.Confusable && !match.IsAmbiguous
                ? match.CanonicalName : null;
            issues.Add(new ValidationIssue(
                $"{path}.{arrayName}[{reference.Index}].skillName", IssueSeverity.Error,
                $"Новый skill '{skillName}' покрыт mastery initialization только под confusable именем",
                code: actor == null ? "player_active_skill_mastery_initialization_confusable" : "npc_skill_mastery_initialization_confusable",
                actor: actor, section: section,
                expected: target ?? "exact canonical skillName of the owning actor (several distinct skills share this normalized form)",
                actual: reference.SkillName,
                repairHint: target == null
                    ? "Несколько canonical skills имеют одну normalized форму. Останови ремонт: не выбирай имя и не меняй mastery, пока неоднозначность не разрешена."
                    : $"Перепиши только skillName этой записи посимвольно на '{target}'. Не удаляй запись и не меняй progression поля."));
        }
    }
}
