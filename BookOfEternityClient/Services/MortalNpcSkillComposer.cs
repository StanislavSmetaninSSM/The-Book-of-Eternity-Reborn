using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Shared pure NPC skill composition, extracted without changing treatment semantics.
/// Already admitted command DATA is reused within the caller's operation; ownership,
/// current-source checks, fingerprints, sealing and publication stay with that caller.
/// </summary>
internal static class MortalNpcSkillComposer
{
    private const string NpcSkillPath = "game_state/npcs/npc_core.json";

    /// <summary>
    /// Exact-owner command shape. The admitting owner freezes the arrays before calling
    /// Compose; this component never retains them or mutates their contents.
    /// </summary>
    internal sealed record Command(
        string NpcId,
        JsonArray? SkillChanges,
        JsonArray? SkillsToRemove);

    internal sealed record MasteryCommand(
        string NpcId, string SkillName, string? SkillId, int NewMasteryLevel,
        int? NewCurrentMasteryProgress = null, int? NewMasteryProgressNeeded = null,
        int? NewMaxMasteryLevel = null);

    /// <summary>
    /// Composes profiles/removals first, then exact owner/kind/name mastery into detached
    /// mirrors. No partial candidate escapes on refusal. Canonical inputs and commands
    /// remain untouched; source admission and publication stay with the calling owner.
    /// </summary>
    internal static JsonObject? ComposeWithMastery(
        JsonObject before,
        IReadOnlyList<Command> activeCommands,
        IReadOnlyList<Command> passiveCommands,
        IReadOnlyList<MasteryCommand> activeMastery,
        IReadOnlyList<MasteryCommand> passiveMastery,
        ICollection<ValidationIssue> issues,
        Func<string, string, string, string, ValidationIssue> issueFactory)
        => ComposeWithMastery(before, activeCommands, passiveCommands, activeMastery, passiveMastery,
            new Dictionary<string, JsonObject>(), new Dictionary<string, JsonObject>(),
            new HashSet<string>(StringComparer.Ordinal), issues, issueFactory);

    internal static JsonObject? ComposeWithMastery(
        JsonObject before, IReadOnlyList<Command> activeCommands, IReadOnlyList<Command> passiveCommands,
        IReadOnlyList<MasteryCommand> activeMastery, IReadOnlyList<MasteryCommand> passiveMastery,
        IDictionary<string, JsonObject> actorBefore, IDictionary<string, JsonObject> actorAfter,
        ISet<string> touchedActors, ICollection<ValidationIssue> issues,
        Func<string, string, string, string, ValidationIssue> issueFactory)
    {
        // Keep collectors private until the whole composition succeeds.
        var localBefore = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var localAfter = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var localTouched = new HashSet<string>(StringComparer.Ordinal);
        var result = Compose(before, activeCommands, passiveCommands,
            localBefore, localAfter, localTouched, issues, issueFactory);
        if (result is null)
            return result;

        // Compose already checked permanent identities and complete mirror equality.
        // These are operation-local references into its detached candidate, not a cache.
        var actors = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (result[section] is not JsonArray rows) continue;
            foreach (var actor in rows.OfType<JsonObject>())
            {
                GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var id);
                if (!actors.TryGetValue(id, out var copies)) actors[id] = copies = [];
                copies.Add(actor);
            }
        }

        foreach (var (commands, active) in new[] { (activeMastery, true), (passiveMastery, false) })
        {
            var property = active ? "activeSkills" : "passiveSkills";
            var section = active ? "NPCSkillMasteryChanges" : "NPCPassiveSkillMasteryChanges";
            var seen = new HashSet<(string Owner, string Name)>();
            var catalogs = new Dictionary<string, Dictionary<string, int>?>(StringComparer.Ordinal);
            for (var row = 0; row < commands.Count; row++)
            {
                var command = commands[row];
                var path = "game_state/npcs/npc_skills.json." + section + $"[{row}]";
                void Refuse(string actual) => issues.Add(issueFactory(path,
                    "mortal_npc_skill_mastery_composition_invalid",
                    "one exact existing owner/kind/skill with unambiguous identity and complete mastery values", actual));

                if (!IsExactText(command.NpcId) || !IsExactText(command.SkillName) ||
                    (command.SkillId is not null && !IsExactText(command.SkillId)) ||
                    !seen.Add((command.NpcId, command.SkillName)) ||
                    command.NewMasteryLevel <= 0 ||
                    (active && (!command.NewCurrentMasteryProgress.HasValue || !command.NewMasteryProgressNeeded.HasValue ||
                        command.NewCurrentMasteryProgress.Value < 0 || command.NewMasteryProgressNeeded.Value <= 0 ||
                        command.NewMaxMasteryLevel.HasValue)))
                {
                    Refuse("invalid or duplicate mastery command");
                    continue;
                }
                if (!actors.TryGetValue(command.NpcId, out var copies))
                {
                    Refuse(command.NpcId);
                    continue;
                }
                if (!catalogs.TryGetValue(command.NpcId, out var catalog))
                {
                    catalog = IndexMasteryCatalog(copies[0][property]);
                    catalogs.Add(command.NpcId, catalog);
                }
                if (catalog is null || !catalog.TryGetValue(command.SkillName, out var index))
                {
                    Refuse(command.SkillName);
                    continue;
                }
                var skill = copies[0][property]![index]!.AsObject();
                if (command.SkillId is not null &&
                    (!TrySkillIdentity(skill, out var id) || !string.Equals(id, command.SkillId, StringComparison.Ordinal)))
                {
                    Refuse(command.SkillId);
                    continue;
                }
                var resultingMaximum = command.NewMaxMasteryLevel ??
                    (TryInteger(skill, "maxMasteryLevel", out var oldMaximum) ? oldMaximum : 0);
                if (!active && (resultingMaximum <= 0 || command.NewMasteryLevel > resultingMaximum))
                {
                    Refuse("passive mastery exceeds its positive resulting maximum");
                    continue;
                }
                var ownerKey = "npc:" + command.NpcId;
                if (localTouched.Add(ownerKey))
                {
                    localBefore[ownerKey + ":active"] = new JsonObject { ["activeSkills"] = copies[0]["activeSkills"]?.DeepClone() ?? new JsonArray() };
                    localBefore[ownerKey + ":passive"] = new JsonObject { ["passiveSkills"] = copies[0]["passiveSkills"]?.DeepClone() ?? new JsonArray() };
                }
                foreach (var copy in copies)
                {
                    var target = copy[property]![index]!.AsObject();
                    target["masteryLevel"] = command.NewMasteryLevel;
                    if (target.ContainsKey("currentMasteryLevel"))
                        target["currentMasteryLevel"] = command.NewMasteryLevel;
                    if (active)
                    {
                        target["currentMasteryProgress"] = command.NewCurrentMasteryProgress!.Value;
                        target["masteryProgressNeeded"] = command.NewMasteryProgressNeeded!.Value;
                    }
                    else if (command.NewMaxMasteryLevel.HasValue)
                        target["maxMasteryLevel"] = command.NewMaxMasteryLevel.Value;
                }
            }
        }
        // Validate the final touched catalogs even for profile-only operations. Do not
        // broaden the older treatment Compose contract or mutate unrelated catalogs.
        foreach (var (owners, active) in new[]
        {
            (activeCommands.Select(c => c.NpcId).Concat(activeMastery.Select(c => c.NpcId)), true),
            (passiveCommands.Select(c => c.NpcId).Concat(passiveMastery.Select(c => c.NpcId)), false)
        })
        {
            var property = active ? "activeSkills" : "passiveSkills";
            foreach (var owner in owners.Distinct(StringComparer.Ordinal))
            {
                if (!actors.TryGetValue(owner, out var copies)) continue;
                var node = copies[0][property];
                if (IndexMasteryCatalog(node) is null ||
                    node!.AsArray().OfType<JsonObject>().Any(skill => !HasConsistentMastery(skill, active)))
                    issues.Add(issueFactory(NpcSkillPath + $".npc[{owner}].{property}",
                        "mortal_npc_skill_mastery_composition_invalid",
                        "consistent exact skill identity and resulting mastery bounds", owner));
            }
        }
        if (issues.Count != 0) return null;
        foreach (var ownerKey in localTouched)
        {
            var finalActor = actors[ownerKey[4..]][0];
            localAfter[ownerKey + ":active"] = new JsonObject { ["activeSkills"] = finalActor["activeSkills"]?.DeepClone() ?? new JsonArray() };
            localAfter[ownerKey + ":passive"] = new JsonObject { ["passiveSkills"] = finalActor["passiveSkills"]?.DeepClone() ?? new JsonArray() };
        }
        foreach (var pair in localBefore) actorBefore[pair.Key] = pair.Value;
        foreach (var pair in localAfter) actorAfter[pair.Key] = pair.Value;
        foreach (var ownerKey in localTouched) touchedActors.Add(ownerKey);
        return result;
    }

    private static bool IsExactText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool TryExactText(JsonNode? node, out string text)
    {
        text = string.Empty;
        if (node is not JsonValue value || !value.TryGetValue<string>(out var result) || !IsExactText(result))
            return false;
        text = result!;
        return true;
    }

    // Index once per touched owner/kind after profile composition. Mastery only changes
    // numeric fields, so identity/index positions remain stable for this invocation.
    private static Dictionary<string, int>? IndexMasteryCatalog(JsonNode? node)
    {
        if (node is not JsonArray skills) return null;
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var nameKeys = new HashSet<string>(StringComparer.Ordinal);
        var idKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skills.Count; index++)
        {
            if (skills[index] is not JsonObject skill || !TryExactText(skill["skillName"], out var name) ||
                !names.TryAdd(name, index) || !nameKeys.Add(MortalLocationIdentityState.BuildConfusableKey(name)))
                return null;
            if (!TrySkillIdentity(skill, out var id) ||
                (id is not null && !idKeys.Add(MortalLocationIdentityState.BuildConfusableKey(id))) ||
                !HasConsistentLevelAliases(skill))
                return null;
        }
        return names;
    }

    private static bool TrySkillIdentity(JsonObject skill, out string? identity)
    {
        identity = null;
        foreach (var key in new[] { "skillId", "id" })
        {
            if (!skill.ContainsKey(key)) continue;
            if (!TryExactText(skill[key], out var value) ||
                (identity is not null && !string.Equals(identity, value, StringComparison.Ordinal)))
                return false;
            identity = value;
        }
        return true;
    }

    private static bool TryInteger(JsonObject skill, string key, out int value)
    {
        value = 0;
        return skill[key] is JsonValue node && node.TryGetValue<int>(out value);
    }

    private static bool HasConsistentLevelAliases(JsonObject skill)
    {
        int? previous = null;
        foreach (var key in new[] { "masteryLevel", "currentMasteryLevel" })
        {
            if (!skill.ContainsKey(key)) continue;
            if (!TryInteger(skill, key, out var level) || level <= 0 ||
                (previous.HasValue && previous.Value != level)) return false;
            previous = level;
        }
        return true;
    }

    private static bool HasConsistentMastery(JsonObject skill, bool active)
    {
        if (!HasConsistentLevelAliases(skill)) return false;
        var hasLevel = TryInteger(skill, "masteryLevel", out var level);
        if (!hasLevel) hasLevel = TryInteger(skill, "currentMasteryLevel", out level);
        if (!hasLevel || level <= 0) return false;
        if (active)
            return !skill.ContainsKey("maxMasteryLevel") &&
                TryInteger(skill, "currentMasteryProgress", out var progress) && progress >= 0 &&
                TryInteger(skill, "masteryProgressNeeded", out var needed) && needed > 0;
        return TryInteger(skill, "maxMasteryLevel", out var maximum) && maximum > 0 && level <= maximum;
    }

    private sealed record NpcActorCopy(string Section, int Index, JsonObject Actor);

    /// <summary>
    /// Composes admitted exact-owner active/passive commands into detached canonical mirrors.
    /// The caller owns command admission, stable input lifetime and publication; this method
    /// retains no DATA, reads no files and never mutates baseline/command JSON or grants write authority.
    /// Existing caller issue codes and paths are preserved through the supplied factory.
    /// </summary>
    internal static JsonObject? Compose(
        JsonObject before,
        IReadOnlyList<Command> activeCommands,
        IReadOnlyList<Command> passiveCommands,
        IDictionary<string, JsonObject> actorBefore,
        IDictionary<string, JsonObject> actorAfter,
        ISet<string> touchedActors,
        ICollection<ValidationIssue> issues,
        Func<string, string, string, string, ValidationIssue> issueFactory)
    {
        var result = before.DeepClone().AsObject();
        var actors = new Dictionary<string, List<NpcActorCopy>>(StringComparer.Ordinal);
        var confusableIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (result[section] is null)
                continue;
            if (result[section] is not JsonArray rows)
            {
                issues.Add(issueFactory(
                    NpcSkillPath + "." + section,
                    "mortal_wound_treatment_publication_npc_skill_command_invalid",
                    "one canonical NPC array",
                    result[section]!.GetType().Name));
                continue;
            }
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] is not JsonObject actor ||
                    !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var npcId))
                {
                    issues.Add(issueFactory(
                        NpcSkillPath + $".{section}[{index}]",
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "one canonical permanent NPC actor",
                        rows[index]?.ToJsonString() ?? "null"));
                    continue;
                }
                var confusable = MortalLocationIdentityState.BuildConfusableKey(npcId);
                if (confusableIds.TryGetValue(confusable, out var previous) &&
                    !string.Equals(previous, npcId, StringComparison.Ordinal))
                {
                    issues.Add(issueFactory(
                        NpcSkillPath + $".{section}[{index}]",
                        "mortal_wound_treatment_publication_npc_selector_ambiguous",
                        "confusable-unique permanent NPC identities",
                        npcId));
                    continue;
                }
                confusableIds[confusable] = npcId;
                if (!actors.TryGetValue(npcId, out var copies))
                    actors[npcId] = copies = new List<NpcActorCopy>();
                copies.Add(new NpcActorCopy(section, index, actor));
            }
        }
        foreach (var (npcId, copies) in actors)
        {
            if (copies.GroupBy(static copy => copy.Section, StringComparer.Ordinal)
                    .Any(static group => group.Count() != 1) ||
                copies.Skip(1).Any(copy => !JsonNode.DeepEquals(copies[0].Actor, copy.Actor)))
            {
                issues.Add(issueFactory(
                    NpcSkillPath,
                    "mortal_wound_treatment_publication_npc_selector_ambiguous",
                    "at most one identical NPC copy per section and identical cross-section mirrors",
                    npcId));
            }
        }
        if (issues.Count != 0)
            return null;

        foreach (var (commands, active) in new[]
                 {
                     (activeCommands, true),
                     (passiveCommands, false)
                 })
        {
            foreach (var command in commands)
            {
                if (!actors.TryGetValue(command.NpcId, out var copies))
                {
                    issues.Add(issueFactory(
                        NpcSkillPath,
                        "mortal_wound_treatment_publication_npc_source_missing",
                        "one exact current NPC actor",
                        command.NpcId));
                    continue;
                }
                var current = copies[0].Actor;
                if (!touchedActors.Contains("npc:" + command.NpcId))
                {
                    actorBefore["npc:" + command.NpcId + ":active"] =
                        new JsonObject { ["activeSkills"] = current["activeSkills"]?.DeepClone() ?? new JsonArray() };
                    actorBefore["npc:" + command.NpcId + ":passive"] =
                        new JsonObject { ["passiveSkills"] = current["passiveSkills"]?.DeepClone() ?? new JsonArray() };
                }
                var changeProperty = active ? "activeSkillChanges" : "passiveSkillChanges";
                var removalProperty = active ? "removeActiveSkills" : "removePassiveSkills";
                var beforeRoot = new JsonObject
                {
                    [changeProperty] = current[active ? "activeSkills" : "passiveSkills"]?.DeepClone() ??
                                       new JsonArray()
                };
                var commandsRoot = new JsonObject();
                if (command.SkillChanges is not null)
                    commandsRoot[changeProperty] = command.SkillChanges.DeepClone();
                if (command.SkillsToRemove is not null)
                    commandsRoot[removalProperty] = command.SkillsToRemove.DeepClone();
                JsonObject composed;
                try
                {
                    composed = EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
                        beforeRoot,
                        commandsRoot,
                        changeProperty,
                        removalProperty,
                        NpcSkillPath + $".npc[{command.NpcId}].{(active ? "activeSkills" : "passiveSkills")}");
                }
                catch (InvalidDataException exception)
                {
                    if (TryComposeExactNpcSkillIdUpdates(
                            beforeRoot,
                            commandsRoot,
                            changeProperty,
                            removalProperty,
                            out composed))
                    {
                        foreach (var copy in copies)
                        {
                            copy.Actor[active ? "activeSkills" : "passiveSkills"] =
                                composed[changeProperty]!.DeepClone();
                        }
                        touchedActors.Add("npc:" + command.NpcId);
                        actorAfter["npc:" + command.NpcId + ":active"] =
                            new JsonObject { ["activeSkills"] = copies[0].Actor["activeSkills"]?.DeepClone() ?? new JsonArray() };
                        actorAfter["npc:" + command.NpcId + ":passive"] =
                            new JsonObject { ["passiveSkills"] = copies[0].Actor["passiveSkills"]?.DeepClone() ?? new JsonArray() };
                        continue;
                    }
                    issues.Add(issueFactory(
                        NpcSkillPath,
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "one unambiguous ordinary NPC skill operation",
                        exception.Message));
                    continue;
                }
                foreach (var copy in copies)
                {
                    copy.Actor[active ? "activeSkills" : "passiveSkills"] =
                        composed[changeProperty]!.DeepClone();
                }
                touchedActors.Add("npc:" + command.NpcId);
                actorAfter["npc:" + command.NpcId + ":active"] =
                    new JsonObject { ["activeSkills"] = copies[0].Actor["activeSkills"]?.DeepClone() ?? new JsonArray() };
                actorAfter["npc:" + command.NpcId + ":passive"] =
                    new JsonObject { ["passiveSkills"] = copies[0].Actor["passiveSkills"]?.DeepClone() ?? new JsonArray() };
            }
        }
        return issues.Count == 0 ? result : null;
    }

    private static bool TryComposeExactNpcSkillIdUpdates(
        JsonObject beforeRoot,
        JsonObject commandsRoot,
        string changeProperty,
        string removalProperty,
        out JsonObject composed)
    {
        composed = null!;
        if (commandsRoot.ContainsKey(removalProperty) ||
            beforeRoot[changeProperty] is not JsonArray current ||
            commandsRoot[changeProperty] is not JsonArray changes ||
            changes.Count == 0)
        {
            return false;
        }

        var result = current.DeepClone().AsArray();
        foreach (var node in changes)
        {
            if (node is not JsonObject candidate ||
                candidate["skillId"] is not JsonValue skillIdNode ||
                !skillIdNode.TryGetValue<string>(out var skillId) ||
                string.IsNullOrWhiteSpace(skillId) ||
                !string.Equals(skillId, skillId.Trim(), StringComparison.Ordinal))
            {
                return false;
            }
            var matches = result
                .Select((value, index) => (value, index))
                .Where(entry => entry.value is JsonObject skill &&
                    string.Equals(
                        skill["skillId"]?.GetValue<string>(),
                        skillId,
                        StringComparison.Ordinal))
                .Select(static entry => entry.index)
                .ToArray();
            if (matches.Length != 1)
                return false;
            result[matches[0]] = candidate.DeepClone();
        }
        composed = new JsonObject { [changeProperty] = result };
        return true;
    }

}
