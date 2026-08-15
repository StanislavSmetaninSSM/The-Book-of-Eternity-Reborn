using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectAcceptedTurnInputComposer
{
    internal const string WorldTimePath = "game_state/world/world_time.json";

    internal const string PlayerWoundsPath = "game_state/player/wounds.json";

    internal static readonly string[] SameTurnOwnerAuthorityPaths =
    {
        "game_state/player/skills_active.json",
        "game_state/player/skills_passive.json",
        PlayerWoundsPath,
        "game_state/quests/regular_quests.json",
        "game_state/quests/soul_quests.json",
        "game_state/world/world_events.json",
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath
    };

    internal static readonly string[] SourceAuthorityPaths =
    {
        "game_state/player/skills_active.json",
        "game_state/player/skills_passive.json",
        "game_state/inventory/items.json",
        PlayerWoundsPath,
        "game_state/quests/regular_quests.json",
        "game_state/quests/soul_quests.json",
        MortalLocationMaterializationContract.WorldMapPath,
        MortalLocationMaterializationContract.CurrentLocationPath,
        "game_state/world/world_events.json",
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath,
        EffectCarrierCatalog.AfterlifeProfilesPath
    };

    private static readonly SourceDescriptor[] SkillSourceDescriptors =
    {
        new("skill", "skillId")
    };

    private static readonly SourceDescriptor[] ItemSourceDescriptors =
    {
        new("item", "itemId")
    };

    private static readonly SourceDescriptor[] WoundSourceDescriptors =
    {
        new("wound", "woundId")
    };

    private static readonly SourceDescriptor[] QuestSourceDescriptors =
    {
        new("quest", "questId")
    };

    private static readonly SourceDescriptor[] LocationSourceDescriptors =
    {
        new("location", "locationId")
    };

    private static readonly SourceDescriptor[] HazardSourceDescriptors =
    {
        new("hazard", "hazardId")
    };

    private static readonly SourceDescriptor[] FactionSourceDescriptors =
    {
        new("faction", "factionId")
    };

    private static readonly SourceDescriptor[] WorldEventSourceDescriptors =
    {
        new("world_event", "eventId")
    };

    private static readonly SourceDescriptor[] FateCardSourceDescriptors =
    {
        new("fate_card", "cardId")
    };

    private static readonly SourceDescriptor[] CombatSourceDescriptors =
    {
        new("combat_action", "combatActionId"),
        new("combat_action", "actionId")
    };

    private static readonly SourceDescriptor[] SpiritualArtSourceDescriptors =
    {
        new("spiritual_art", "artId")
    };

    private static readonly SourceDescriptor[] AfterlifeFateCardSourceDescriptors =
    {
        new("fate_card", "cardId")
    };

    private static readonly HashSet<string> TerminalSourceStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Completed", "Failed", "Abandoned", "cancelled", "resolved",
            "removed", "inactive", "disabled", "locked", "healed", "closed",
            "consumed", "destroyed"
        };

    internal static EffectAcceptedTurnInput Compose(
        string sessionId,
        string snapshotToken,
        int turn,
        JsonObject rawCommands,
        EffectCarrierCatalogInput preTurnCarriers,
        EffectCarrierCatalogInput acceptedCarriers,
        JsonObject? preTurnIdentityIndex,
        IReadOnlyDictionary<string, JsonNode?> preTurnSourceRoots,
        IReadOnlyList<EffectSourceExport>? acceptedPlanSourceExports = null,
        IReadOnlyList<EffectTargetExport>? acceptedPlanTargetExports = null,
        IReadOnlySet<EffectSourceOwnerKey>? replacedSourceOwners = null,
        IReadOnlySet<EffectTargetKey>? replacedTargets = null,
        long? currentWorldTime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(rawCommands);
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(acceptedCarriers);
        ArgumentNullException.ThrowIfNull(preTurnSourceRoots);

        var replacedSources = replacedSourceOwners ??
            new HashSet<EffectSourceOwnerKey>();
        var preTurnSourceExports = CollectSources(preTurnSourceRoots, sameTurn: false)
            .Where(export => !replacedSources.Contains(new EffectSourceOwnerKey(
                export.Realm,
                export.Kind,
                export.SourceId)))
            .ToArray();
        var planSourceExports = acceptedPlanSourceExports ?? Array.Empty<EffectSourceExport>();
        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            preTurnSourceExports,
            planSourceExports,
            new HashSet<string>(StringComparer.Ordinal)));
        var preTurnTargets = CollectTargets(
            preTurnCarriers,
            preTurnSourceRoots,
            includeTemporaryRefs: false)
            .Where(target => replacedTargets == null ||
                !replacedTargets.Contains(new EffectTargetKey(
                    target.Realm,
                    target.Kind,
                    target.TargetId)))
            .ToArray();
        var preTurnTargetKeys = preTurnTargets
            .Select(static target => (target.Realm, target.Kind, target.TargetId))
            .ToHashSet();
        var sameTurnTargets = (acceptedPlanTargetExports ?? Array.Empty<EffectTargetExport>())
            .Where(target => !preTurnTargetKeys.Contains(
                (target.Realm, target.Kind, target.TargetId)))
            .Select(static target => target with { SameTurn = true })
            .ToArray();
        var targetAuthorityInput = new EffectTargetAuthorityInput(
            preTurnTargets,
            sameTurnTargets,
            new HashSet<string>(StringComparer.Ordinal),
            null);
        var targetAuthority = EffectTargetAuthority.Build(targetAuthorityInput);

        return new EffectAcceptedTurnInput(
            sessionId,
            snapshotToken,
            rawCommands.DeepClone().AsObject(),
            sourceAuthority,
            targetAuthority,
            BuildAcceptedEventInput(turn, rawCommands, currentWorldTime),
            PreTurnCarriers: CloneCarriers(acceptedCarriers),
            PreTurnIdentityIndex: preTurnIdentityIndex?.DeepClone().AsObject(),
            TargetAuthorityInput: targetAuthorityInput);
    }

    internal static JsonObject CreateEmptyCommandRoot() => new()
    {
        ["effectChanges"] = new JsonArray(),
        ["effectResolutionReceipts"] = new JsonArray()
    };

    internal static bool HasPendingCombatantRefs(EffectCarrierCatalogInput carriers) =>
        ContainsCombatantRef(carriers.EnemyCombatants, "enemiesData") ||
        ContainsCombatantRef(carriers.AllyCombatants, "alliesData");

    private static bool ContainsCombatantRef(JsonObject? root, string collection) =>
        root?[collection] is JsonArray combatants &&
        combatants.OfType<JsonObject>().Any(static combatant =>
            combatant.ContainsKey("combatantRef"));

    internal static JsonObject BuildAcceptedEventInput(
        int turn,
        JsonObject rawCommands,
        long? currentWorldTime = null)
    {
        var operationCount = rawCommands["effectChanges"] is JsonArray changes
            ? changes.Count
            : 0;
        var events = new JsonArray();
        for (var index = 0; index < Math.Max(1, operationCount); index++)
        {
            var ordinal = index + 1;
            events.Add(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = ordinal == 1
                    ? $"turn_{turn}"
                    : $"turn_{turn}_effect_{ordinal}",
                ["eventRef"] = ordinal == 1
                    ? $"turn_{turn}:accepted_effect"
                    : $"turn_{turn}:accepted_effect:{ordinal}"
            });
        }
        var result = new JsonObject
        {
            ["turn"] = turn,
            ["events"] = events,
            ["lifecycleEvents"] = new JsonArray(new JsonObject
            {
                ["eventRef"] = $"turn_{turn}:lifecycle:owner_turn_end:player_current",
                ["turn"] = turn,
                ["phase"] = "owner_turn_end",
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["triggerId"] = null,
                ["currentTime"] = currentWorldTime,
                ["currentSceneId"] = null,
                ["sceneClosed"] = false,
                ["sourceSatisfied"] = null,
                ["conditionSatisfied"] = null,
                ["currentRealm"] = "mortal_world"
            })
        };
        if (currentWorldTime.HasValue)
        {
            result["currentTime"] = currentWorldTime.Value;
            result["timeAuthority"] =
                EffectSourceDefinitionContract.CanonicalWorldTimeAuthority;
        }
        return result;
    }

    internal static long? ReadCanonicalWorldTime(string? json)
    {
        if (json == null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
                setWorldTime.ValueKind != JsonValueKind.Null)
            {
                if (setWorldTime.ValueKind == JsonValueKind.Object &&
                    TryReadNonNegativeLong(
                        setWorldTime,
                        "currentTimeInMinutes",
                        out var acceptedAbsolute))
                {
                    return acceptedAbsolute;
                }
                return null;
            }
            if (TryReadNonNegativeLong(root, "currentTimeInMinutes", out var direct))
                return direct;
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static bool TryReadNonNegativeLong(
        JsonElement root,
        string field,
        out long value)
    {
        value = -1;
        return root.TryGetProperty(field, out var node) &&
            node.ValueKind == JsonValueKind.Number &&
            node.TryGetInt64(out value) &&
            value >= 0;
    }

    internal static EffectAcceptedOwnerExports CollectValidatedSameTurnOwnerExports(
        IReadOnlyDictionary<string, JsonNode?> preTurnSourceRoots,
        IReadOnlyDictionary<string, JsonNode?> acceptedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(preTurnSourceRoots);
        ArgumentNullException.ThrowIfNull(acceptedSourceRoots);
        var allowedPaths = SameTurnOwnerAuthorityPaths.ToHashSet(StringComparer.Ordinal);
        var changedPaths = acceptedSourceRoots.Keys
            .Where(allowedPaths.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var preTurnRoots = preTurnSourceRoots
            .Where(pair => changedPaths.Contains(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);
        var validatedRoots = acceptedSourceRoots
            .Where(pair => changedPaths.Contains(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);
        var beforeSources = CollectSources(preTurnRoots, sameTurn: false);
        var composedRoots = validatedRoots.ToDictionary(
            static pair => pair.Key,
            pair => ComposeAcceptedOwnerRoot(
                pair.Key,
                preTurnRoots.GetValueOrDefault(pair.Key),
                pair.Value),
            StringComparer.Ordinal);
        var afterSources = CollectSources(composedRoots, sameTurn: true);
        var beforeTargets = new List<EffectTargetExport>();
        var afterTargets = new List<EffectTargetExport>();
        CollectNpcTargets(preTurnRoots, includeTemporaryRefs: false, beforeTargets);
        CollectNpcTargets(composedRoots, includeTemporaryRefs: true, afterTargets);
        var acceptedStableTargetKeys = afterTargets
            .Where(static target => target.TargetRef == null)
            .Select(static target => new EffectTargetKey(
                target.Realm,
                target.Kind,
                target.TargetId))
            .ToHashSet();
        return new EffectAcceptedOwnerExports(
            afterSources,
            afterTargets
                .Where(static target => target.TargetRef != null)
                .Select(static target => target with { SameTurn = true })
                .ToArray(),
            beforeSources
                .Concat(afterSources)
                .Select(static source => new EffectSourceOwnerKey(
                    source.Realm,
                    source.Kind,
                    source.SourceId))
                .ToHashSet(),
            beforeTargets
                .Select(static target => new EffectTargetKey(
                    target.Realm,
                    target.Kind,
                    target.TargetId))
                .Where(target => !acceptedStableTargetKeys.Contains(target))
                .ToHashSet());
    }

    private static JsonNode? ComposeAcceptedOwnerRoot(
        string path,
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot) =>
        path switch
        {
            "game_state/player/skills_active.json" =>
                ComposeSkillAcceptedRoot(
                    preTurnRoot,
                    acceptedRoot,
                    "activeSkillChanges",
                    "removeActiveSkills"),
            "game_state/player/skills_passive.json" =>
                ComposeSkillAcceptedRoot(
                    preTurnRoot,
                    acceptedRoot,
                    "passiveSkillChanges",
                    "removePassiveSkills"),
            "game_state/quests/regular_quests.json" =>
                ComposeQuestRoot(preTurnRoot, acceptedRoot, "UpdateQuests"),
            "game_state/quests/soul_quests.json" =>
                ComposeQuestRoot(preTurnRoot, acceptedRoot, "UpdateSoulQuests"),
            _ => acceptedRoot?.DeepClone()
        };

    internal static JsonObject ComposeSkillAcceptedRoot(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string changeProperty,
        string removalProperty,
        string? path = null)
    {
        var issues = new List<ValidationIssue>();
        var result = ComposeSkillAcceptedRootCore(
            preTurnRoot,
            acceptedRoot,
            changeProperty,
            removalProperty,
            path ?? changeProperty,
            issues);
        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Skill composition is ambiguous at '{path ?? changeProperty}': " +
                issues[0].Code);
        }
        return result;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAcceptedSkillComposition(
        IReadOnlyDictionary<string, JsonNode?> preTurnRoots,
        IReadOnlyDictionary<string, JsonNode?> acceptedRoots)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoots);
        ArgumentNullException.ThrowIfNull(acceptedRoots);
        var issues = new List<ValidationIssue>();
        foreach (var (path, changeProperty, removalProperty) in new[]
                 {
                     ("game_state/player/skills_active.json", "activeSkillChanges", "removeActiveSkills"),
                     ("game_state/player/skills_passive.json", "passiveSkillChanges", "removePassiveSkills")
                 })
        {
            var preTurnRoot = preTurnRoots.GetValueOrDefault(path);
            var acceptedRoot = acceptedRoots.GetValueOrDefault(path);
            if (acceptedRoot == null || JsonNode.DeepEquals(preTurnRoot, acceptedRoot))
                continue;
            _ = ComposeSkillAcceptedRootCore(
                preTurnRoot,
                acceptedRoot,
                changeProperty,
                removalProperty,
                path,
                issues);
        }
        return issues;
    }

    private static JsonObject ComposeSkillAcceptedRootCore(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string changeProperty,
        string removalProperty,
        string path,
        List<ValidationIssue> issues)
    {
        var skills = EnumerateSkillEntries(preTurnRoot, changeProperty)
            .Select(static skill => skill.DeepClone().AsObject())
            .ToList();
        var updateIndex = 0;
        foreach (var skill in EnumerateSkillEntries(acceptedRoot, changeProperty))
        {
            ApplySkillUpdate(
                skills,
                skill,
                $"{path}.{changeProperty}[{updateIndex++}]",
                issues);
        }

        if (acceptedRoot is JsonObject acceptedObject &&
            acceptedObject[removalProperty] is JsonArray removals)
        {
            var removalIndex = 0;
            foreach (var removal in removals)
            {
                if (!TryReadExact(removal, out var selector))
                {
                    removalIndex++;
                    continue;
                }
                ApplySkillRemoval(
                    skills,
                    selector,
                    $"{path}.{removalProperty}[{removalIndex++}]",
                    issues);
            }
        }

        ValidateComposedSkillNames(skills, path, changeProperty, issues);
        var result = new JsonObject
        {
            [changeProperty] = new JsonArray(
                skills.Select(static skill => (JsonNode?)skill).ToArray())
        };
        CopySkillMetadata(preTurnRoot, result);
        CopySkillMetadata(acceptedRoot, result);
        return result;
    }

    private static void CopySkillMetadata(JsonNode? source, JsonObject target)
    {
        if (source is not JsonObject sourceObject)
            return;
        foreach (var property in sourceObject.Where(static property =>
                     property.Key.StartsWith("_", StringComparison.Ordinal)))
        {
            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static IEnumerable<JsonObject> EnumerateSkillEntries(
        JsonNode? root,
        string changeProperty)
    {
        if (root is JsonArray array)
        {
            foreach (var skill in array.OfType<JsonObject>())
                yield return skill.DeepClone().AsObject();
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in new[] { changeProperty, "skills" })
        {
            if (objectRoot[property] is not JsonArray entries)
                continue;
            foreach (var skill in entries.OfType<JsonObject>())
                yield return skill.DeepClone().AsObject();
        }
    }

    private static void ApplySkillUpdate(
        List<JsonObject> skills,
        JsonObject candidate,
        string path,
        List<ValidationIssue> issues)
    {
        var candidateId = ReadFirstExact(candidate, "skillId");
        if (candidateId != null)
        {
            var exact = FindSkillIndexes(skills, "skillId", candidateId, confusable: false);
            var aliases = FindSkillIndexes(skills, "skillId", candidateId, confusable: true);
            if (exact.Count > 1 || aliases.Count > 1)
            {
                AddSkillCompositionIssue(issues, path + ".skillId",
                    "effect_skill_composition_update_ambiguous",
                    "one exact/confusable skillId update target", candidateId);
                return;
            }
            if (exact.Count == 0 && aliases.Count == 1)
            {
                AddSkillCompositionIssue(issues, path + ".skillId",
                    "effect_skill_composition_update_confusable",
                    "exact skillId or one new non-confusable skillId", candidateId);
                return;
            }
            if (exact.Count == 1)
                skills[exact[0]] = candidate.DeepClone().AsObject();
            else
                skills.Add(candidate.DeepClone().AsObject());
            return;
        }

        var candidateName = ReadFirstExact(candidate, "skillName", "name");
        if (candidateName == null)
        {
            skills.Add(candidate.DeepClone().AsObject());
            return;
        }
        var exactNames = FindSkillNameIndexes(skills, candidateName, confusable: false);
        var aliasNames = FindSkillNameIndexes(skills, candidateName, confusable: true);
        if (exactNames.Count > 1 || aliasNames.Count > 1)
        {
            AddSkillCompositionIssue(issues, path + ".skillName",
                "effect_skill_composition_update_ambiguous",
                "one exact/confusable skillName update target", candidateName);
            return;
        }
        if (exactNames.Count == 0 && aliasNames.Count == 1)
        {
            AddSkillCompositionIssue(issues, path + ".skillName",
                "effect_skill_composition_update_confusable",
                "exact skillName or one new non-confusable skillName", candidateName);
            return;
        }
        if (exactNames.Count == 1)
            skills[exactNames[0]] = candidate.DeepClone().AsObject();
        else
            skills.Add(candidate.DeepClone().AsObject());
    }

    private static void ApplySkillRemoval(
        List<JsonObject> skills,
        string selector,
        string path,
        List<ValidationIssue> issues)
    {
        var exact = FindSkillNameIndexes(skills, selector, confusable: false);
        var aliases = FindSkillNameIndexes(skills, selector, confusable: true);
        if (exact.Count == 1 && aliases.Count == 1)
        {
            skills.RemoveAt(exact[0]);
            return;
        }

        var code = exact.Count == 0 && aliases.Count == 0
            ? "effect_skill_composition_selector_unknown"
            : exact.Count == 0 && aliases.Count == 1
                ? "effect_skill_composition_selector_confusable"
                : "effect_skill_composition_selector_ambiguous";
        AddSkillCompositionIssue(issues, path, code,
            "one exact, non-confusable skillName removal target", selector);
    }

    private static void ValidateComposedSkillNames(
        IReadOnlyList<JsonObject> skills,
        string path,
        string changeProperty,
        List<ValidationIssue> issues)
    {
        var exactNames = new HashSet<string>(StringComparer.Ordinal);
        var aliasNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skills.Count; index++)
        {
            var name = ReadFirstExact(skills[index], "skillName", "name");
            if (name == null)
                continue;
            if (!exactNames.Add(name))
            {
                AddSkillCompositionIssue(issues,
                    $"{path}.{changeProperty}[{index}].skillName",
                    "effect_skill_composition_name_ambiguous",
                    "globally exact-unique composed skillName", name);
            }
            else if (!aliasNames.Add(MortalLocationIdentityState.BuildConfusableKey(name)))
            {
                AddSkillCompositionIssue(issues,
                    $"{path}.{changeProperty}[{index}].skillName",
                    "effect_skill_composition_name_confusable",
                    "globally exact/confusable-unique composed skillName", name);
            }
        }
    }

    private static List<int> FindSkillIndexes(
        IReadOnlyList<JsonObject> skills,
        string field,
        string selector,
        bool confusable)
    {
        var result = new List<int>();
        var key = confusable
            ? MortalLocationIdentityState.BuildConfusableKey(selector)
            : selector;
        for (var index = 0; index < skills.Count; index++)
        {
            if (!TryReadExact(skills[index][field], out var value))
                continue;
            var candidate = confusable
                ? MortalLocationIdentityState.BuildConfusableKey(value)
                : value;
            if (string.Equals(candidate, key, StringComparison.Ordinal))
                result.Add(index);
        }
        return result;
    }

    private static List<int> FindSkillNameIndexes(
        IReadOnlyList<JsonObject> skills,
        string selector,
        bool confusable)
    {
        var result = new List<int>();
        var key = confusable
            ? MortalLocationIdentityState.BuildConfusableKey(selector)
            : selector;
        for (var index = 0; index < skills.Count; index++)
        {
            var name = ReadFirstExact(skills[index], "skillName", "name");
            if (name == null)
                continue;
            var candidate = confusable
                ? MortalLocationIdentityState.BuildConfusableKey(name)
                : name;
            if (string.Equals(candidate, key, StringComparison.Ordinal))
                result.Add(index);
        }
        return result;
    }

    private static void AddSkillCompositionIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Player skill composition requires one exact, non-confusable owner target.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use complete skill objects with stable exact skillId values and exact unique skillName selectors for removals."));
    }

    private static JsonObject ComposeQuestRoot(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string updateProperty)
    {
        var quests = new List<JsonObject>();
        foreach (var quest in EnumerateQuestEntries(preTurnRoot, updateProperty))
            UpsertQuest(quests, quest);
        foreach (var quest in EnumerateQuestEntries(acceptedRoot, updateProperty))
            UpsertQuest(quests, quest);
        return new JsonObject
        {
            ["quests"] = new JsonArray(
                quests.Select(static quest => (JsonNode?)quest).ToArray())
        };
    }

    private static IEnumerable<JsonObject> EnumerateQuestEntries(
        JsonNode? root,
        string updateProperty)
    {
        if (root is JsonArray array)
        {
            foreach (var quest in array.OfType<JsonObject>())
                yield return quest.DeepClone().AsObject();
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in new[] { "quests", updateProperty })
        {
            if (objectRoot[property] is not JsonArray entries)
                continue;
            foreach (var quest in entries.OfType<JsonObject>())
            {
                var clone = quest.DeepClone().AsObject();
                clone.Remove("newDetailsLogEntry");
                yield return clone;
            }
        }
    }

    private static void UpsertQuest(
        List<JsonObject> quests,
        JsonObject candidate)
    {
        var existing = quests.FirstOrDefault(quest =>
            QuestIdentityMatches(quest, candidate));
        if (existing == null)
        {
            quests.Add(candidate.DeepClone().AsObject());
            return;
        }
        foreach (var property in candidate)
            existing[property.Key] = property.Value?.DeepClone();
    }

    private static bool QuestIdentityMatches(
        JsonObject left,
        JsonObject right)
    {
        var leftQuestId = ReadFirstExact(left, "questId");
        var rightQuestId = ReadFirstExact(right, "questId");
        if (leftQuestId != null || rightQuestId != null)
        {
            return leftQuestId != null &&
                   rightQuestId != null &&
                   string.Equals(leftQuestId, rightQuestId, StringComparison.Ordinal);
        }

        var leftInitialId = ReadFirstExact(left, "initialId");
        var rightInitialId = ReadFirstExact(right, "initialId");
        if (leftInitialId != null || rightInitialId != null)
        {
            return leftInitialId != null &&
                   rightInitialId != null &&
                   string.Equals(leftInitialId, rightInitialId, StringComparison.Ordinal);
        }

        foreach (var field in new[] { "questName", "title", "name" })
        {
            if (TryReadExact(left[field], out var leftValue) &&
                TryReadExact(right[field], out var rightValue))
            {
                return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
            }
        }
        return false;
    }

    internal static EffectSourceAuthority BuildCanonicalSourceAuthority(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        return EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            CollectSources(sourceRoots, sameTurn: false),
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
    }

    internal static IReadOnlyList<ValidationIssue> ValidateRegisteredSourceOwners(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        var issues = new List<ValidationIssue>();
        foreach (var root in sourceRoots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var candidate in EnumerateRegisteredSourceObjects(
                         root.Value,
                         root.Key,
                         InferRealm(root.Key)))
            {
                if (candidate.Owner["activeEffectDefinitions"] is not JsonArray)
                    continue;
                if (TryResolveSourceDescriptor(candidate, out _, out _))
                    continue;
                issues.Add(new ValidationIssue(
                    root.Key + ".activeEffectDefinitions",
                    IssueSeverity.Error,
                    "Effect source owner requires one exact stable identity on its registered owning contour.",
                    code: "effect_source_authority_invalid_owner_identity",
                    section: "effect_materialization",
                    expected: "exact source-kind identity with no conflicting identity selectors",
                    actual: candidate.Owner.ToJsonString(),
                    repairHint: "Add the exact stable identity required by this source contour and remove identity fields belonging to other source kinds."));
            }
        }
        return issues;
    }

    internal static EffectTargetAuthority BuildCanonicalTargetAuthority(
        EffectCarrierCatalogInput carriers,
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(sourceRoots);
        return EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            CollectTargets(carriers, sourceRoots, includeTemporaryRefs: false),
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
    }

    internal static IReadOnlyList<EffectSourceExport> CollectLocationPlanSources(
        MortalLocationAcceptedTurnPlan? plan)
    {
        if (plan == null || plan.LocationIdsByInitialId.Count == 0)
            return Array.Empty<EffectSourceExport>();

        var locationsById = (plan.FinalWorldMap["locations"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static location => TryReadExact(location["locationId"], out _))
            .ToDictionary(
                static location => location["locationId"]!.GetValue<string>(),
                static location => location,
                StringComparer.Ordinal) ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var exports = new List<EffectSourceExport>();
        foreach (var mapping in plan.LocationIdsByInitialId.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (!locationsById.TryGetValue(mapping.Value, out var location))
                continue;

            var definitions = location["activeEffectDefinitions"] as JsonArray;
            exports.Add(new EffectSourceExport(
                "mortal_world",
                "location",
                mapping.Value,
                definitions?.DeepClone().AsArray() ?? new JsonArray(),
                Materializable: true,
                Active: true,
                SameTurn: true,
                SourceRef: mapping.Key));

            foreach (var hazard in EnumerateArrayOwners(
                         location,
                         "hazards",
                         "activeHazards"))
            {
                if (!TryReadExact(hazard["hazardId"], out var hazardId))
                {
                    continue;
                }
                var hazardDefinitions = hazard["activeEffectDefinitions"] as JsonArray;

                var active = IsSourceCurrentlyActive(
                    MortalLocationMaterializationContract.WorldMapPath,
                    "hazard",
                    hazard);
                exports.Add(new EffectSourceExport(
                    "mortal_world",
                    "hazard",
                    hazardId,
                    hazardDefinitions?.DeepClone().AsArray() ?? new JsonArray(),
                    Materializable: true,
                    Active: active,
                    SameTurn: true,
                    SatisfiedPredicates: BuildSatisfiedPredicates(
                        MortalLocationMaterializationContract.WorldMapPath,
                        "hazard",
                        hazardId,
                        hazard,
                        plan.FinalWorldMap,
                        active)));
            }
        }
        return exports;
    }

    private static IReadOnlyList<EffectSourceExport> CollectSources(
        IReadOnlyDictionary<string, JsonNode?> roots,
        bool sameTurn)
    {
        roots.TryGetValue(
            MortalLocationMaterializationContract.CurrentLocationPath,
            out var currentLocationRoot);
        var currentLocation = currentLocationRoot is JsonObject currentRoot &&
                              currentRoot["currentLocationData"] is JsonObject wrappedCurrent
            ? wrappedCurrent
            : currentLocationRoot as JsonObject;
        var selectedLocationId = currentLocation == null
            ? null
            : ReadFirstExact(currentLocation, "locationId");
        var selectedLocationSources = roots.TryGetValue(
                MortalLocationMaterializationContract.CurrentLocationPath,
                out currentLocationRoot)
            ? EnumerateRegisteredSourceObjects(
                    currentLocationRoot,
                    MortalLocationMaterializationContract.CurrentLocationPath,
                    "mortal_world")
                .Select(candidate => TryResolveSourceDescriptor(
                        candidate,
                        out var descriptor,
                        out var sourceId)
                    ? new EffectSourceOwnerKey("mortal_world", descriptor.Kind, sourceId)
                    : null)
                .OfType<EffectSourceOwnerKey>()
                .ToHashSet()
            : new HashSet<EffectSourceOwnerKey>();
        var exports = new List<EffectSourceExport>();
        foreach (var root in roots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var candidate in EnumerateRegisteredSourceObjects(
                         root.Value,
                         root.Key,
                         InferRealm(root.Key)))
            {
                var owner = candidate.Owner;
                if (!TryResolveSourceDescriptor(
                        candidate,
                        out var descriptor,
                        out var sourceId))
                {
                    continue;
                }
                if (string.Equals(
                        root.Key,
                        MortalLocationMaterializationContract.WorldMapPath,
                        StringComparison.Ordinal) &&
                    (descriptor.Kind is "location" or "hazard") &&
                    selectedLocationId != null &&
                    string.Equals(
                        candidate.OwningLocationId,
                        selectedLocationId,
                        StringComparison.Ordinal) &&
                    selectedLocationSources.Contains(new EffectSourceOwnerKey(
                        "mortal_world",
                        descriptor.Kind,
                        sourceId)))
                {
                    continue;
                }
                var definitions = owner["activeEffectDefinitions"] as JsonArray;

                var active = IsSourceCurrentlyActive(
                    root.Key,
                    descriptor.Kind,
                    owner);
                exports.Add(new EffectSourceExport(
                    candidate.Realm,
                    descriptor.Kind,
                    sourceId,
                    definitions?.DeepClone().AsArray() ?? new JsonArray(),
                    Materializable: true,
                    Active: active,
                    SameTurn: sameTurn,
                    SatisfiedPredicates: BuildSatisfiedPredicates(
                        root.Key,
                        descriptor.Kind,
                        sourceId,
                        owner,
                        root.Value,
                        active)));
            }
        }
        return exports;
    }

    private static IReadOnlySet<string> BuildSatisfiedPredicates(
        string path,
        string kind,
        string sourceId,
        JsonObject owner,
        JsonNode? root,
        bool active)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal);
        if (active)
            predicates.Add("active");

        switch (kind)
        {
            case "item":
                if (MortalItemLocalActionPolicy.IsCarriedByPlayer(owner))
                    predicates.Add("carried");
                if (string.Equals(
                        path,
                        InventoryEquipmentService.ItemsPath,
                        StringComparison.Ordinal) &&
                    root is JsonObject inventoryRoot &&
                    MortalItemEquipmentAuthority.TryRead(
                        inventoryRoot,
                        inventoryRoot["items"] as JsonArray,
                        path,
                        out var equipment,
                        out _) &&
                    equipment.EquippedItemIds().Contains(sourceId))
                {
                    predicates.Add("equipped");
                }
                break;
            case "skill":
            case "spiritual_art":
            case "fate_card":
            case "combat_action":
                if (active)
                    predicates.Add("unlocked");
                break;
        }

        return predicates;
    }

    private static bool IsSourceCurrentlyActive(
        string path,
        string kind,
        JsonObject owner)
    {
        if (TryReadBoolean(owner["isInactive"], out var inactive) && inactive)
            return false;

        var status = ReadFirstExact(owner, "status", "state", "availability");
        switch (kind)
        {
            case "quest":
                return StatusIs(
                    status,
                    "Active",
                    "Updated",
                    "ready_to_turn_in");
            case "world_event":
                return TryReadBoolean(owner["isActive"], out var eventActive)
                    ? eventActive
                    : StatusIs(status, "Active", "ongoing", "current");
            case "item":
                return string.Equals(
                           path,
                           "game_state/inventory/items.json",
                           StringComparison.Ordinal) &&
                       MortalItemLocalActionPolicy.IsCarriedByPlayer(owner);
            case "location":
            case "hazard":
                return (string.Equals(
                            path,
                            MortalLocationMaterializationContract.CurrentLocationPath,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            path,
                            MortalLocationMaterializationContract.WorldMapPath,
                            StringComparison.Ordinal)) &&
                       !IsTerminalSourceStatus(status);
            case "wound":
                return (!TryReadBoolean(owner["isHealed"], out var healed) || !healed) &&
                       !IsTerminalSourceStatus(status);
            case "fate_card":
                if (string.Equals(
                        path,
                        EffectCarrierCatalog.AfterlifeProfilesPath,
                        StringComparison.Ordinal))
                {
                    return StatusIs(status, "unlocked", "active");
                }
                return TryReadBoolean(owner["isUnlocked"], out var unlocked)
                    ? unlocked
                    : StatusIs(status, "unlocked", "active");
            case "skill":
            case "spiritual_art":
            case "faction":
            case "combat_action":
                return !IsTerminalSourceStatus(status);
            default:
                return false;
        }
    }

    private static bool IsTerminalSourceStatus(string? status) =>
        status != null && TerminalSourceStatuses.Contains(status);

    private static bool StatusIs(string? status, params string[] expected) =>
        status != null && expected.Contains(status, StringComparer.OrdinalIgnoreCase);

    private static bool TryResolveSourceDescriptor(
        SourceObjectCandidate candidate,
        out SourceDescriptor descriptor,
        out string sourceId)
    {
        var matchingDescriptors = candidate.Descriptors
            .Where(allowed =>
                TryReadExact(candidate.Owner[allowed.IdentityField], out _))
            .ToArray();
        descriptor = matchingDescriptors.Length == 1
            ? matchingDescriptors[0]
            : null!;
        if (descriptor != null &&
            TryReadExact(candidate.Owner[descriptor.IdentityField], out sourceId))
        {
            return true;
        }

        sourceId = string.Empty;
        if (!TryReadExact(candidate.Owner["initialId"], out var initialId))
            return false;
        var temporaryDescriptor = candidate.Descriptors.FirstOrDefault(allowed =>
            allowed.Kind is "quest" or "faction");
        if (temporaryDescriptor != null)
        {
            descriptor = temporaryDescriptor;
            sourceId = initialId;
            return true;
        }
        return false;
    }

    private static IReadOnlyList<EffectTargetExport> CollectTargets(
        EffectCarrierCatalogInput carriers,
        IReadOnlyDictionary<string, JsonNode?> sourceRoots,
        bool includeTemporaryRefs)
    {
        var result = new List<EffectTargetExport>
        {
            new("mortal_world", "player", "player_current", SameTurn: false)
        };

        CollectCombatants(carriers.EnemyCombatants, "enemiesData", result);
        CollectCombatants(carriers.AllyCombatants, "alliesData", result);
        CollectNpcTargets(sourceRoots, includeTemporaryRefs, result);
        return result;
    }

    private static void CollectNpcTargets(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots,
        bool includeTemporaryRefs,
        List<EffectTargetExport> targets)
    {
        if (!sourceRoots.TryGetValue("game_state/npcs/npc_core.json", out var root) ||
            root is not JsonObject npcRoot)
            return;
        foreach (var collectionName in new[] { "NPCsInScene", "UpdateNPCs" })
        {
            if (npcRoot[collectionName] is not JsonArray npcs)
                continue;
            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var temporaryRef = ReadFirstExact(npc, "npcRef", "initialId");
                var targetId = ReadFirstExact(npc, "NPCId", "npcId") ?? temporaryRef;
                if (targetId == null)
                    continue;
                targets.Add(new EffectTargetExport(
                    "mortal_world",
                    "npc",
                    targetId,
                    SameTurn: false,
                    TargetRef: includeTemporaryRefs ? temporaryRef : null));
            }
        }
    }

    private static void CollectCombatants(
        JsonObject? root,
        string collection,
        List<EffectTargetExport> targets)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            if (TryReadExact(combatant["combatantId"], out var combatantId))
            {
                var boundNpcId = ReadFirstExact(combatant, "NPCId");
                targets.Add(new EffectTargetExport(
                    "mortal_world",
                    "combatant",
                    combatantId,
                    SameTurn: false,
                    BoundNpcId: boundNpcId));
            }
        }
    }

    private static IEnumerable<SourceObjectCandidate>
        EnumerateRegisteredSourceObjects(
            JsonNode? root,
            string path,
            string inheritedRealm)
    {
        switch (path)
        {
            case "game_state/player/skills_active.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "activeSkillChanges",
                             "skills"))
                    yield return Candidate(owner, inheritedRealm, SkillSourceDescriptors);
                yield break;
            case "game_state/player/skills_passive.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "passiveSkillChanges",
                             "skills"))
                    yield return Candidate(owner, inheritedRealm, SkillSourceDescriptors);
                yield break;
            case "game_state/inventory/items.json":
                foreach (var owner in EnumerateArrayOwners(root, "items"))
                    yield return Candidate(owner, inheritedRealm, ItemSourceDescriptors);
                yield break;
            case PlayerWoundsPath:
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "playerWoundChanges",
                             "wounds"))
                    yield return Candidate(owner, inheritedRealm, WoundSourceDescriptors);
                yield break;
            case "game_state/quests/regular_quests.json":
            case "game_state/quests/soul_quests.json":
                foreach (var owner in EnumerateArrayOwners(root, "quests"))
                    yield return Candidate(owner, inheritedRealm, QuestSourceDescriptors);
                yield break;
            case MortalLocationMaterializationContract.WorldMapPath:
                foreach (var location in EnumerateArrayOwners(root, "locations"))
                {
                    var locationId = ReadFirstExact(location, "locationId");
                    yield return Candidate(
                        location,
                        inheritedRealm,
                        LocationSourceDescriptors,
                        locationId);
                    foreach (var hazard in EnumerateArrayOwners(
                                 location,
                                 "hazards",
                                 "activeHazards"))
                    {
                        yield return Candidate(
                            hazard,
                            inheritedRealm,
                            HazardSourceDescriptors,
                            locationId);
                    }
                }
                yield break;
            case MortalLocationMaterializationContract.CurrentLocationPath:
                var current = root is JsonObject currentRoot &&
                              currentRoot["currentLocationData"] is JsonObject wrappedCurrent
                    ? wrappedCurrent
                    : root as JsonObject;
                if (current != null)
                {
                    var locationId = ReadFirstExact(current, "locationId");
                    yield return Candidate(
                        current,
                        inheritedRealm,
                        LocationSourceDescriptors,
                        locationId);
                    foreach (var hazard in EnumerateArrayOwners(
                                 current,
                                 "hazards",
                                 "activeHazards"))
                    {
                        yield return Candidate(
                            hazard,
                            inheritedRealm,
                            HazardSourceDescriptors,
                            locationId);
                    }
                }
                yield break;
            case "game_state/world/world_events.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "worldEventsLog"))
                    yield return Candidate(owner, inheritedRealm, WorldEventSourceDescriptors);
                yield break;
            case "game_state/factions/faction_core.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "factions",
                             "factionDataChanges"))
                    yield return Candidate(owner, inheritedRealm, FactionSourceDescriptors);
                yield break;
            case "game_state/npcs/npc_core.json":
                foreach (var actor in EnumerateArrayOwners(
                             root,
                             "NPCsInScene",
                             "UpdateNPCs"))
                {
                    foreach (var skill in EnumerateArrayOwners(
                                 actor,
                                 "activeSkills",
                                 "passiveSkills"))
                        yield return Candidate(skill, inheritedRealm, SkillSourceDescriptors);
                    foreach (var card in EnumerateArrayOwners(actor, "fateCards"))
                        yield return Candidate(card, inheritedRealm, FateCardSourceDescriptors);
                    foreach (var action in EnumerateArrayOwners(
                                 actor,
                                 "actions",
                                 "combatActions"))
                        yield return Candidate(action, inheritedRealm, CombatSourceDescriptors);
                }
                yield break;
            case EffectCarrierCatalog.EnemiesPath:
            case EffectCarrierCatalog.AlliesPath:
                foreach (var combatant in EnumerateArrayOwners(
                             root,
                             "enemiesData",
                             "alliesData"))
                {
                    foreach (var action in EnumerateArrayOwners(
                                 combatant,
                                 "actions",
                                 "combatActions"))
                        yield return Candidate(action, inheritedRealm, CombatSourceDescriptors);
                }
                yield break;
            case EffectCarrierCatalog.AfterlifeProfilesPath:
                foreach (var profile in EnumerateArrayOwners(root, "profiles"))
                {
                    var realm = ResolveObjectRealm(path, profile, inheritedRealm);
                    foreach (var art in EnumerateArrayOwners(
                                 profile,
                                 "specialArts"))
                        yield return Candidate(art, realm, SpiritualArtSourceDescriptors);
                    foreach (var card in EnumerateArrayOwners(profile, "fateCards"))
                        yield return Candidate(card, realm, AfterlifeFateCardSourceDescriptors);
                }
                yield break;
        }
    }

    private static SourceObjectCandidate Candidate(
        JsonObject owner,
        string realm,
        IReadOnlyList<SourceDescriptor> descriptors,
        string? owningLocationId = null) =>
        new(owner, realm, descriptors, owningLocationId);

    private static IEnumerable<JsonObject> EnumerateArrayOwners(
        JsonNode? root,
        params string[] properties)
    {
        if (root is JsonArray arrayRoot)
        {
            foreach (var owner in arrayRoot.OfType<JsonObject>())
                yield return owner;
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in properties)
        {
            if (objectRoot[property] is not JsonArray owners)
                continue;
            foreach (var owner in owners.OfType<JsonObject>())
                yield return owner;
        }
    }

    private static string ResolveObjectRealm(
        string path,
        JsonObject owner,
        string inheritedRealm)
    {
        if (string.Equals(
                path,
                EffectCarrierCatalog.AfterlifeProfilesPath,
                StringComparison.Ordinal))
        {
            return TryReadExact(owner["actorType"], out _) &&
                   TryReadExact(owner["actorId"], out _) &&
                   owner["realm"] is JsonValue profileRealm &&
                   profileRealm.TryGetValue<string>(out var declaredProfileRealm) &&
                   AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                       declaredProfileRealm,
                       out var normalizedProfileRealm)
                ? normalizedProfileRealm
                : inheritedRealm;
        }

        return TryReadExact(owner["realm"], out var declaredRealm)
            ? declaredRealm
            : inheritedRealm;
    }

    private static EffectCarrierCatalogInput CloneCarriers(EffectCarrierCatalogInput input) =>
        new(
            input.PlayerEffects?.DeepClone().AsObject(),
            input.NpcEffects?.DeepClone().AsObject(),
            input.EnemyCombatants?.DeepClone().AsObject(),
            input.AllyCombatants?.DeepClone().AsObject(),
            input.AfterlifeProfiles?.DeepClone().AsObject(),
            input.SpiritualConflict?.DeepClone().AsObject());

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadBoolean(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static string? ReadFirstExact(JsonObject root, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (TryReadExact(root[field], out var value))
                return value;
        }
        return null;
    }

    private static string InferRealm(string path) =>
        path.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("shining", StringComparison.OrdinalIgnoreCase)
            ? "shining_abode"
            : "mortal_world";

    private sealed record SourceDescriptor(string Kind, string IdentityField);

    private sealed record SourceObjectCandidate(
        JsonObject Owner,
        string Realm,
        IReadOnlyList<SourceDescriptor> Descriptors,
        string? OwningLocationId);
}

internal sealed record EffectAcceptedOwnerExports(
    IReadOnlyList<EffectSourceExport> Sources,
    IReadOnlyList<EffectTargetExport> Targets,
    IReadOnlySet<EffectSourceOwnerKey> ReplacedSourceOwners,
    IReadOnlySet<EffectTargetKey> ReplacedTargets)
{
    internal static EffectAcceptedOwnerExports Empty { get; } =
        new(
            Array.Empty<EffectSourceExport>(),
            Array.Empty<EffectTargetExport>(),
            new HashSet<EffectSourceOwnerKey>(),
            new HashSet<EffectTargetKey>());
}

internal sealed record EffectSourceOwnerKey(
    string Realm,
    string Kind,
    string SourceId);
