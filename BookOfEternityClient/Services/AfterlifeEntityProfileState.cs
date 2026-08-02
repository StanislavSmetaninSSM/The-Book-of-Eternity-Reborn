using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeEntityProfileState
{
    public const string StatePath = "game_state/meta/afterlife_entity_profiles.json";
    public const string ProfilesProperty = "profiles";
    public const string ResponseProfilesProperty = "afterlifeEntityProfiles";
    public const string UpdateProperty = "afterlifeEntityProfileUpdates";
    public const string CustomStateChangesProperty = "afterlifeEntityCustomStateChanges";
    public const string CustomStatesProperty = "customStates";
    public const string ProgressionOverridesProperty = "afterlifeEntityProgressionOverrides";
    public const string SpecialArtLearningReceiptsProperty = "afterlifeSpecialArtLearningReceipts";
    public const string FateCardUnlocksProperty = "afterlifeFateCardUnlocks";
    public const string GoalUpdatesProperty = "afterlifeActorGoalUpdates";
    public const string QuestUpdatesProperty = "afterlifeActorQuestUpdates";
    public const string ActivityUpdatesProperty = "afterlifeActorActivityUpdates";
    public const string CompleteActivitiesProperty = "completeAfterlifeActorActivities";
    public const string RelationshipChangesProperty = "afterlifeRelationshipChanges";
    public const string RelationshipLockUpdatesProperty = "afterlifeRelationshipLockUpdates";
    public const string BreakthroughQuestUpdatesProperty = "afterlifeBreakthroughQuestUpdates";
    public const string RelationshipsProperty = "relationships";
    public const string RelationshipGateQuestsProperty = "relationshipGateQuests";
    public const string MaskAddsProperty = "afterlifeActorMaskAdds";
    public const string MaskUpdatesProperty = "afterlifeActorMaskUpdates";
    public const string MaskRemovalsProperty = "afterlifeActorMaskRemovals";
    public const string ActiveMaskChangesProperty = "afterlifeActorActiveMaskChanges";
    public const string MasksProperty = "masks";
    public const string ActiveMaskIdProperty = "activeMaskId";
    public const string TrueSelfMaskId = "_true_self_";
    public const string ProgressionLedgerProperty = "progressionLedger";
    public const string LastInvalidProgressionOverrideProperty = "lastInvalidProgressionOverride";
    public const string LastInvalidProgressionOverrideReasonProperty = "lastInvalidProgressionOverrideReason";
    public const string LastInvalidCommandProperty = "lastInvalidProfileCommand";
    public const string LastInvalidCommandReasonProperty = "lastInvalidProfileCommandReason";
    public const string SoulDissipationTierProperty = "soulDissipationTier";
    public const int MaxSoulStabilityCoefficient = MaxProfileTier - 1;
    public const int SchemaVersion = 1;
    public const int MaxProfileTier = 5;

    public static readonly HashSet<string> ActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "player_soul",
        "guardian",
        "resident",
        "shining_resident",
        "shining_faction_head",
        "saref_agent",
        "system_actor",
        "radiant_actor",
        "custom_afterlife_actor"
    };

    public static readonly HashSet<string> Realms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chaos Sea",
        "Море Хаоса",
        "Shining Abode",
        "Сияющая Обитель"
    };

    public static readonly HashSet<string> StandardArtIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "break_binding",
        "binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static readonly HashSet<string> SpecialArtBaseOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "binding",
        "break_binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static readonly HashSet<string> ProgressionSpendCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "standardArts",
        "specialArts",
        "enlightenment",
        "radiance",
        "soulDissipationTier"
    };

    private static readonly HashSet<string> CurrencyDeltaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "inkFeathers",
        "lightSparks"
    };

    private static readonly HashSet<string> ProgressionExperienceDeltaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "enlightenment",
        "radiance"
    };

    private static readonly HashSet<string> ActorQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "blocked",
        "completed",
        "failed",
        "cancelled"
    };

    private static readonly HashSet<string> ActorActivityCompletionOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "failed",
        "cancelled",
        "blocked"
    };

    public static readonly HashSet<string> RelationshipAxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "trust",
        "romance",
        "rivalry",
        "oath",
        "fear",
        "reverence",
        "debt"
    };

    public static readonly HashSet<string> RelationshipLockStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "positive_locked",
        "negative_locked",
        "point_of_no_return"
    };

    public static readonly HashSet<string> RelationshipGateQuestTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "breakthrough",
        "redemption"
    };

    public static readonly HashSet<string> RelationshipGateQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "completed",
        "failed",
        "cancelled"
    };

    internal static readonly string[] FateCardMechanicalEffectProperties =
    {
        "guardianEffects",
        "playerUnlocks",
        "politicalEffects",
        "combatEffects",
        "trainingUnlocks"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [ProfilesProperty] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(
        JsonObject? currentRoot,
        JsonObject? previousRoot,
        JsonObject? progressionReportRoot = null)
    {
        var result = CreateDefaultRoot();
        var commandAuthoredMentorShowcaseKeys = CollectCommandAuthoredMentorShowcaseKeys(currentRoot, previousRoot);

        UpsertProfiles(result, previousRoot?[ProfilesProperty]);
        UpsertProfiles(result, currentRoot?[ProfilesProperty]);
        UpsertProfileCommands(result, currentRoot?[ResponseProfilesProperty], "profile_response_not_object", "profile_response_not_array");
        UpsertProfileCommands(result, currentRoot?[UpdateProperty], "profile_update_not_object", "profile_update_not_array");
        ApplyCustomStateChanges(result, currentRoot?[CustomStateChangesProperty]);
        ApplyFateCardUnlocks(result, currentRoot?[FateCardUnlocksProperty]);
        ApplyActorGoalUpdates(result, currentRoot?[GoalUpdatesProperty]);
        ApplyActorQuestUpdates(result, currentRoot?[QuestUpdatesProperty]);
        ApplyActorActivityUpdates(result, currentRoot?[ActivityUpdatesProperty]);
        ApplyCompleteActorActivities(result, currentRoot?[CompleteActivitiesProperty]);
        ApplyRelationshipChanges(result, currentRoot?[RelationshipChangesProperty]);
        ApplyRelationshipLockUpdates(result, currentRoot?[RelationshipLockUpdatesProperty]);
        ApplyBreakthroughQuestUpdates(result, currentRoot?[BreakthroughQuestUpdatesProperty]);
        ApplyMaskAdds(result, currentRoot?[MaskAddsProperty]);
        ApplyMaskUpdates(result, currentRoot?[MaskUpdatesProperty]);
        ApplyActiveMaskChanges(result, currentRoot?[ActiveMaskChangesProperty]);
        ApplyMaskRemovals(result, currentRoot?[MaskRemovalsProperty]);
        ApplySpecialArtLearningReceipts(result, currentRoot?[SpecialArtLearningReceiptsProperty]);
        ApplyProgressionOverrides(result, currentRoot?[ProgressionOverridesProperty]);
        commandAuthoredMentorShowcaseKeys.UnionWith(ApplyAutomaticProgression(result, progressionReportRoot));
        NormalizeCustomStateProgressionRules(result);
        NormalizeRelationshipLocks(result);
        RefreshCommandAuthoredMentorShowcaseHashes(result, commandAuthoredMentorShowcaseKeys);
        TrainingService.NormalizeAfterlifeMentorShowcaseCosts(result);

        result.Remove(UpdateProperty);
        result.Remove(ResponseProfilesProperty);
        result.Remove(CustomStateChangesProperty);
        result.Remove(FateCardUnlocksProperty);
        result.Remove(GoalUpdatesProperty);
        result.Remove(QuestUpdatesProperty);
        result.Remove(ActivityUpdatesProperty);
        result.Remove(CompleteActivitiesProperty);
        result.Remove(RelationshipChangesProperty);
        result.Remove(RelationshipLockUpdatesProperty);
        result.Remove(BreakthroughQuestUpdatesProperty);
        result.Remove(MaskAddsProperty);
        result.Remove(MaskUpdatesProperty);
        result.Remove(MaskRemovalsProperty);
        result.Remove(ActiveMaskChangesProperty);
        result.Remove(ProgressionOverridesProperty);
        result.Remove(SpecialArtLearningReceiptsProperty);
        return result;
    }

    public static void ApplyPlayerSoulProfileClientAuthority(
        JsonObject root,
        JsonObject? soulRoot,
        JsonObject? shiningRoot)
    {
        if (soulRoot == null || root[ProfilesProperty] is not JsonArray profiles)
            return;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (!IsPlayerSoulProfile(profile))
                continue;

            profile["currencies"] = BuildPlayerSoulCurrencies(soulRoot, shiningRoot);
            profile["progression"] = BuildPlayerSoulProgression(soulRoot, shiningRoot);
            profile["standardArts"] = BuildPlayerSoulStandardArts(soulRoot);
            DisablePlayerSoulProfileAutoProgression(profile);
            RemovePlayerSoulAutomaticProgressionLedgerEntries(profile);
        }
    }

    internal static async Task<CanonicalMutationPublication?>
        ApplyPlayerSoulProfileClientAuthorityAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        Func<Task>? afterInputsReadAsync = null,
        Func<string, Task<CanonicalMutationPublication>>?
            publishReplacementAsync = null)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);

        var currentRoot = await ReadObjectAsync(fs, StatePath);
        if (currentRoot == null)
            return null;

        var soulRoot = await ReadObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot == null)
            return null;

        var projectedRoot = currentRoot.DeepClone().AsObject();
        var shiningRoot = await ReadObjectAsync(fs, ShiningAbodeState.StatePath);
        if (afterInputsReadAsync != null)
            await afterInputsReadAsync();
        ApplyPlayerSoulProfileClientAuthority(projectedRoot, soulRoot, shiningRoot);

        if (JsonNode.DeepEquals(currentRoot, projectedRoot))
            return null;

        var content = projectedRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        if (publishReplacementAsync != null)
            return await publishReplacementAsync(content);

        return await fs.WriteFileAtomicWithPublicationAsync(
            writeLease,
            StatePath,
            content);
    }

    private static HashSet<string> CollectCommandAuthoredMentorShowcaseKeys(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (currentRoot == null)
            return keys;

        CollectCommandAuthoredMentorShowcaseKeys(currentRoot[ResponseProfilesProperty], keys);
        CollectCommandAuthoredMentorShowcaseKeys(currentRoot[UpdateProperty], keys);
        CollectChangedProfileMentorShowcaseKeys(currentRoot[ProfilesProperty], previousRoot?[ProfilesProperty], keys);
        return keys;
    }

    private static void CollectCommandAuthoredMentorShowcaseKeys(JsonNode? node, HashSet<string> keys)
    {
        switch (node)
        {
            case JsonObject profile:
                AddCommandAuthoredMentorShowcaseKey(profile, keys);
                break;
            case JsonArray profiles:
                foreach (var item in profiles.OfType<JsonObject>())
                    AddCommandAuthoredMentorShowcaseKey(item, keys);
                break;
        }
    }

    private static void AddCommandAuthoredMentorShowcaseKey(JsonObject profile, HashSet<string> keys)
    {
        if (profile["mentorTrainingShowcase"] is not JsonObject)
            return;

        var key = BuildIdentityKey(profile);
        if (!string.IsNullOrWhiteSpace(key))
            keys.Add(key);
    }

    private static void CollectChangedProfileMentorShowcaseKeys(
        JsonNode? currentNode,
        JsonNode? previousNode,
        HashSet<string> keys)
    {
        if (currentNode is not JsonArray currentProfiles || previousNode is not JsonArray previousProfiles)
            return;

        var previousByIdentity = previousProfiles
            .OfType<JsonObject>()
            .Select(profile => new { Key = BuildIdentityKey(profile), Profile = profile })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key!, entry => entry.Profile, StringComparer.OrdinalIgnoreCase);

        foreach (var currentProfile in currentProfiles.OfType<JsonObject>())
        {
            if (currentProfile["mentorTrainingShowcase"] is not JsonObject currentShowcase)
                continue;

            var key = BuildIdentityKey(currentProfile);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!previousByIdentity.TryGetValue(key, out var previousProfile) ||
                previousProfile["mentorTrainingShowcase"] is not JsonObject previousShowcase ||
                !JsonNode.DeepEquals(currentShowcase, previousShowcase))
            {
                keys.Add(key);
            }
        }
    }

    private static void RefreshCommandAuthoredMentorShowcaseHashes(
        JsonObject result,
        IReadOnlySet<string> commandAuthoredMentorShowcaseKeys)
    {
        if (commandAuthoredMentorShowcaseKeys.Count == 0 ||
            result[ProfilesProperty] is not JsonArray profiles)
        {
            return;
        }

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            var key = BuildIdentityKey(profile);
            if (string.IsNullOrWhiteSpace(key) ||
                !commandAuthoredMentorShowcaseKeys.Contains(key) ||
                profile["mentorTrainingShowcase"] is not JsonObject showcase)
            {
                continue;
            }

            var expectedActorId = GetNodeString(profile["actorId"]) ?? GetNodeString(profile["actorRef"]);
            var actualActorId = GetNodeString(showcase["sourceActorId"]);
            if (!string.IsNullOrWhiteSpace(actualActorId) &&
                !string.IsNullOrWhiteSpace(expectedActorId) &&
                !string.Equals(actualActorId, expectedActorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            showcase["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(profile);
        }
    }

    public static void UpsertProfile(JsonArray profiles, JsonObject profile)
    {
        var identityKey = BuildIdentityKey(profile);
        if (string.IsNullOrWhiteSpace(identityKey))
        {
            profiles.Add(CloneObject(profile));
            return;
        }

        for (var index = 0; index < profiles.Count; index++)
        {
            if (profiles[index] is not JsonObject existing)
                continue;

            if (!string.Equals(BuildIdentityKey(existing), identityKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var replacement = CloneObject(profile);
            PreserveProgressionSettlement(existing, replacement);
            PreserveHistoricalMaterialization(existing, replacement);
            profiles[index] = replacement;
            return;
        }

        profiles.Add(CloneObject(profile));
    }

    private static void PreserveHistoricalMaterialization(JsonObject existing, JsonObject replacement)
    {
        if (replacement.ContainsKey(ActorMaterializationContract.PropertyName) ||
            existing[ActorMaterializationContract.PropertyName] is not JsonObject historicalEnvelope ||
            !HasExactActorIdentity(existing, replacement))
        {
            return;
        }

        replacement[ActorMaterializationContract.PropertyName] = historicalEnvelope.DeepClone();
    }

    private static bool HasExactActorIdentity(JsonObject existing, JsonObject replacement)
    {
        var existingType = GetNodeString(existing["actorType"]);
        var replacementType = GetNodeString(replacement["actorType"]);
        var existingId = GetNodeString(existing["actorId"]) ?? GetNodeString(existing["actorRef"]);
        var replacementId = GetNodeString(replacement["actorId"]) ?? GetNodeString(replacement["actorRef"]);
        return !string.IsNullOrWhiteSpace(existingType) &&
               !string.IsNullOrWhiteSpace(existingId) &&
               string.Equals(existingType, replacementType, StringComparison.Ordinal) &&
               string.Equals(existingId, replacementId, StringComparison.Ordinal);
    }

    private static void PreserveProgressionSettlement(JsonObject existing, JsonObject replacement)
    {
        if (replacement["progressionStrategy"] is not JsonObject replacementStrategy ||
            existing["progressionStrategy"] is not JsonObject existingStrategy)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(GetNodeString(replacementStrategy["lastAutoProgressionCycleKey"])))
            return;

        var previousCycleKey = GetNodeString(existingStrategy["lastAutoProgressionCycleKey"]);
        if (!string.IsNullOrWhiteSpace(previousCycleKey))
            replacementStrategy["lastAutoProgressionCycleKey"] = previousCycleKey;
    }

    public static string? BuildIdentityKey(JsonObject? profile)
    {
        if (profile == null)
            return null;

        var actorType = GetNodeString(profile["actorType"]);
        var actorId = GetNodeString(profile["actorId"]) ?? GetNodeString(profile["actorRef"]);
        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType.Trim()}:{actorId.Trim()}";
    }

    public static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonValue value && value.TryGetValue<string>(out var result))
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();

        return null;
    }

    public static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var result))
            return result;

        return 0;
    }

    private static bool TryGetNodeInt(JsonNode? node, out int result)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out result))
            return true;

        result = 0;
        return false;
    }

    public static int ResolveSoulStabilityCoefficient(JsonObject? profile)
    {
        if (profile == null)
            return 0;

        var progression = profile["progression"] as JsonObject;
        var enlightenmentTier = ResolveProgressionTier(progression?["enlightenment"] as JsonObject);
        var radianceTier = ResolveProgressionTier(progression?["radiance"] as JsonObject);
        return Math.Clamp(Math.Max(enlightenmentTier, radianceTier), 0, MaxSoulStabilityCoefficient);
    }

    private static int ResolveProgressionTier(JsonObject? progression)
    {
        if (progression == null)
            return 0;

        var tier = GetNodeInt(progression["tier"]);
        if (tier > 0)
            return tier;

        tier = GetNodeInt(progression["currentTier"]);
        if (tier > 0)
            return tier;

        var tierName = GetNodeString(progression["tierName"]);
        if (string.IsNullOrWhiteSpace(tierName))
            return 0;

        return tierName.Trim().ToLowerInvariant() switch
        {
            "dormant" or "unlit" => 0,
            "stirring" or "spark" => 1,
            "focused" or "gleam" => 2,
            "tempered" or "ray" => 3,
            "lucid" or "halo" => 4,
            _ => MaxSoulStabilityCoefficient
        };
    }

    private static void UpsertProfiles(JsonObject result, JsonNode? profilesNode)
    {
        if (profilesNode is not JsonArray profiles)
            return;

        var resultProfiles = EnsureProfilesArray(result);
        foreach (var profile in profiles.OfType<JsonObject>())
            UpsertProfile(resultProfiles, profile);
    }

    private static void UpsertProfileCommands(
        JsonObject result,
        JsonNode? profilesNode,
        string nonObjectReason,
        string nonArrayReason)
    {
        if (profilesNode == null)
            return;

        if (profilesNode is not JsonArray profiles)
        {
            MarkInvalidProfileCommand(result, profilesNode, nonArrayReason);
            return;
        }

        var resultProfiles = EnsureProfilesArray(result);
        foreach (var profileNode in profiles)
        {
            if (profileNode is not JsonObject profile)
            {
                MarkInvalidProfileCommand(result, profileNode, nonObjectReason);
                continue;
            }

            UpsertProfile(resultProfiles, profile);
        }
    }

    private static JsonArray EnsureProfilesArray(JsonObject root)
    {
        if (root[ProfilesProperty] is JsonArray profiles)
            return profiles;

        profiles = new JsonArray();
        root[ProfilesProperty] = profiles;
        return profiles;
    }

    private static void ApplyCustomStateChanges(JsonObject result, JsonNode? changesNode)
    {
        if (changesNode == null)
            return;

        if (changesNode is not JsonArray changes)
        {
            MarkInvalidProfileCommand(result, changesNode, "custom_state_changes_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var changeNode in changes)
        {
            if (changeNode is not JsonObject change)
            {
                MarkInvalidProfileCommand(result, changeNode, "custom_state_change_not_object");
                continue;
            }

            var targetKey = BuildIdentityKey(change);
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                MarkInvalidProfileCommand(result, change, "missing_custom_state_target");
                continue;
            }

            var profile = profiles
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(BuildIdentityKey(item), targetKey, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, change, "unknown_custom_state_target");
                continue;
            }

            if (change.ContainsKey("statesToRemove") && change["statesToRemove"] is not JsonArray)
            {
                MarkInvalidProfileCommand(result, change, "custom_state_removals_not_array");
                continue;
            }

            if (change.ContainsKey("statesToAddOrUpdate") && change["statesToAddOrUpdate"] is not JsonArray)
            {
                MarkInvalidProfileCommand(result, change, "custom_state_upserts_not_array");
                continue;
            }

            if (change["statesToRemove"] is not JsonArray && change["statesToAddOrUpdate"] is not JsonArray)
            {
                MarkInvalidProfileCommand(result, change, "empty_custom_state_change");
                continue;
            }

            var hasRemovalOperations = change["statesToRemove"] is JsonArray removalsToCount && removalsToCount.Count > 0;
            var hasUpsertOperations = change["statesToAddOrUpdate"] is JsonArray upsertsToCount && upsertsToCount.Count > 0;
            if (!hasRemovalOperations && !hasUpsertOperations)
            {
                MarkInvalidProfileCommand(result, change, "empty_custom_state_change");
                continue;
            }

            if (change["statesToRemove"] is JsonArray removalsToValidate &&
                !CustomStateRemovalsAreProjectable(removalsToValidate))
            {
                MarkInvalidProfileCommand(result, change, "custom_state_remove_invalid_id");
                continue;
            }

            if (change["statesToAddOrUpdate"] is JsonArray upsertsToValidate &&
                !CustomStateUpsertsAreProjectable(upsertsToValidate))
            {
                MarkInvalidProfileCommand(result, change, "custom_state_upsert_not_object");
                continue;
            }

            if (change["statesToRemove"] is JsonArray removals)
                RemoveCustomStates(profile, removals);

            if (change["statesToAddOrUpdate"] is JsonArray upserts)
                UpsertCustomStates(profile, upserts);
        }
    }

    private static bool CustomStateRemovalsAreProjectable(JsonArray removals) =>
        removals.All(item => !string.IsNullOrWhiteSpace(GetNodeString(item)));

    private static bool CustomStateUpsertsAreProjectable(JsonArray upserts) =>
        upserts.All(item => item is JsonObject);

    private static void UpsertCustomStates(JsonObject profile, JsonArray upserts)
    {
        var states = EnsureCustomStatesArray(profile);
        foreach (var state in upserts.OfType<JsonObject>())
        {
            var identity = BuildCustomStateIdentity(state);
            if (string.IsNullOrWhiteSpace(identity))
            {
                states.Add(CloneObject(state));
                continue;
            }

            var replaced = false;
            for (var index = 0; index < states.Count; index++)
            {
                if (states[index] is not JsonObject existing)
                    continue;

                if (!string.Equals(BuildCustomStateIdentity(existing), identity, StringComparison.OrdinalIgnoreCase))
                    continue;

                states[index] = CloneObject(state);
                replaced = true;
                break;
            }

            if (!replaced)
                states.Add(CloneObject(state));
        }
    }

    private static void RemoveCustomStates(JsonObject profile, JsonArray removals)
    {
        if (profile[CustomStatesProperty] is not JsonArray states)
            return;

        var removeIds = removals
            .Select(GetNodeString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removeIds.Count == 0)
            return;

        for (var index = states.Count - 1; index >= 0; index--)
        {
            if (states[index] is JsonObject state &&
                removeIds.Contains(BuildCustomStateIdentity(state) ?? string.Empty))
            {
                states.RemoveAt(index);
            }
        }
    }

    private static JsonArray EnsureCustomStatesArray(JsonObject profile)
    {
        if (profile[CustomStatesProperty] is JsonArray states)
            return states;

        states = new JsonArray();
        profile[CustomStatesProperty] = states;
        return states;
    }

    private static void NormalizeCustomStateProgressionRules(JsonObject root)
    {
        if (root[ProfilesProperty] is not JsonArray profiles)
            return;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (profile[CustomStatesProperty] is not JsonArray states)
                continue;

            foreach (var state in states.OfType<JsonObject>())
            {
                if (state["progressionRule"] is not JsonValue progressionRule ||
                    !progressionRule.TryGetValue<string>(out var description) ||
                    string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                state["progressionRule"] = new JsonObject
                {
                    ["changePerTurn"] = 0,
                    ["description"] = description.Trim()
                };
            }
        }
    }

    private static void NormalizeRelationshipLocks(JsonObject root)
    {
        if (root[ProfilesProperty] is not JsonArray profiles)
            return;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (profile[RelationshipsProperty] is not JsonArray relationships)
                continue;

            foreach (var relationship in relationships.OfType<JsonObject>())
            {
                if (relationship["relationshipLock"] is not JsonObject relationshipLock)
                    continue;

                var lockState = GetNodeString(relationshipLock["lockState"]);
                if (string.IsNullOrWhiteSpace(lockState) || !RelationshipLockStates.Contains(lockState))
                {
                    lockState = "none";
                    relationshipLock["lockState"] = lockState;
                }

                var direction = NormalizeRelationshipLockDirection(
                    GetNodeString(relationshipLock["direction"]),
                    relationship,
                    lockState);
                relationshipLock["direction"] = direction;

                if (!TryGetNodeInt(relationshipLock["threshold"], out var threshold) || threshold is < -100 or > 100)
                    relationshipLock["threshold"] = string.Equals(direction, "negative", StringComparison.OrdinalIgnoreCase) ? -50 : 50;

                if (string.IsNullOrWhiteSpace(GetNodeString(relationshipLock["reason"])))
                    relationshipLock["reason"] = BuildDefaultRelationshipLockReason(lockState, direction);

                if (string.IsNullOrWhiteSpace(GetNodeString(relationshipLock["evidence"])))
                {
                    relationshipLock["evidence"] =
                        GetNodeString(relationship["evidence"]) ??
                        GetNodeString(relationship["reason"]) ??
                        "Состояние отношений сохранено в профиле сущности.";
                }

                if (!TryGetNodeInt(relationshipLock["updatedAtTurn"], out var updatedAtTurn) || updatedAtTurn < 0)
                {
                    relationshipLock["updatedAtTurn"] =
                        TryGetNodeInt(relationship["updatedAtTurn"], out var relationshipUpdatedAtTurn) && relationshipUpdatedAtTurn >= 0
                            ? relationshipUpdatedAtTurn
                            : 0;
                }
            }
        }
    }

    private static string NormalizeRelationshipLockDirection(string? rawDirection, JsonObject relationship, string lockState)
    {
        if (string.Equals(rawDirection, "positive", StringComparison.OrdinalIgnoreCase))
            return "positive";
        if (string.Equals(rawDirection, "negative", StringComparison.OrdinalIgnoreCase))
            return "negative";

        if (string.Equals(lockState, "negative_locked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(lockState, "point_of_no_return", StringComparison.OrdinalIgnoreCase))
        {
            return "negative";
        }

        if (string.Equals(lockState, "positive_locked", StringComparison.OrdinalIgnoreCase))
            return "positive";

        if (relationship["relationshipLock"] is JsonObject relationshipLock &&
            TryGetNodeInt(relationshipLock["threshold"], out var threshold) &&
            threshold < 0)
        {
            return "negative";
        }

        return GetNodeInt(relationship["value"]) < 0 ? "negative" : "positive";
    }

    private static string BuildDefaultRelationshipLockReason(string lockState, string direction)
    {
        if (string.Equals(lockState, "positive_locked", StringComparison.OrdinalIgnoreCase))
            return "Отношение достигло положительного порога и требует сцены доверия.";

        if (string.Equals(lockState, "negative_locked", StringComparison.OrdinalIgnoreCase))
            return "Отношение достигло отрицательного порога и требует сцены искупления.";

        if (string.Equals(lockState, "point_of_no_return", StringComparison.OrdinalIgnoreCase))
            return "Отношение пересекло точку невозврата и закреплено драматическим событием.";

        return string.Equals(direction, "negative", StringComparison.OrdinalIgnoreCase)
            ? "Отношение не заблокировано, но наблюдается в отрицательном направлении."
            : "Отношение не заблокировано, но наблюдается в положительном направлении.";
    }

    private static string? BuildCustomStateIdentity(JsonObject state) =>
        GetNodeString(state["stateId"]) ??
        GetNodeString(state["stateKey"]) ??
        GetNodeString(state["key"]) ??
        GetNodeString(state["name"]) ??
        GetNodeString(state["title"]) ??
        GetNodeString(state["stateName"]);

    private static void ApplyFateCardUnlocks(JsonObject result, JsonNode? unlocksNode)
    {
        if (unlocksNode == null)
            return;

        if (unlocksNode is not JsonArray unlocks)
        {
            MarkInvalidProfileCommand(result, unlocksNode, "fate_card_unlocks_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var unlockNode in unlocks)
        {
            if (unlockNode is not JsonObject unlock)
            {
                MarkInvalidProfileCommand(result, unlockNode, "fate_card_unlock_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, unlock);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, unlock, "unknown_fate_card_unlock_target");
                continue;
            }

            var cardId = GetNodeString(unlock["cardId"]);
            if (string.IsNullOrWhiteSpace(cardId) ||
                !TryGetNodeInt(unlock["appliedAtTurn"], out var appliedAtTurn) ||
                appliedAtTurn < 0 ||
                !HasFateCardEvidence(unlock))
            {
                MarkInvalidProfileCommand(result, unlock, "incomplete_fate_card_unlock");
                continue;
            }

            var card = FindFateCardById(profile, cardId);
            if (card == null)
            {
                MarkInvalidProfileCommand(result, unlock, "unknown_fate_card_unlock_card");
                continue;
            }

            card["status"] = "unlocked";
            card["appliedAtTurn"] = appliedAtTurn;
            if (unlock.TryGetPropertyValue("evidence", out var evidence) && evidence != null)
                card["evidence"] = evidence.DeepClone();

            CopyOptionalCommandFields(unlock, card, "storyMeaning", "unlockSummary", "sceneAdvantage", "guardianMemoryFragment");
            foreach (var propertyName in FateCardMechanicalEffectProperties)
            {
                if (unlock.TryGetPropertyValue(propertyName, out var effects) && effects != null)
                    card[propertyName] = effects.DeepClone();
            }
        }
    }

    private static JsonObject? FindFateCardById(JsonObject profile, string cardId)
    {
        if (profile["fateCards"] is not JsonArray fateCards)
            return null;

        return fateCards
            .OfType<JsonObject>()
            .FirstOrDefault(card => string.Equals(GetNodeString(card["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFateCardEvidence(JsonObject command)
    {
        if (command["evidence"] is JsonObject evidence && evidence.Count > 0)
            return true;

        return !string.IsNullOrWhiteSpace(GetNodeString(command["evidenceSummary"]));
    }

    private static void ApplyActorGoalUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "actor_goal_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "actor_goal_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_actor_goal_target");
                continue;
            }

            if (!CommandHasNonEmptyStrings(update, "goalId", "shortTermGoal", "longTermGoal", "plan", "gmThoughtsSummary") ||
                !TryGetNodeInt(update["updatedAtTurn"], out var updatedAtTurn) ||
                updatedAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, update, "incomplete_actor_goal_update");
                continue;
            }

            var goals = new JsonObject();
            CopyCommandFields(update, goals, "goalId", "shortTermGoal", "longTermGoal", "plan", "gmThoughtsSummary");
            CopyOptionalCommandFields(update, goals, "actorBrainRef", "strategyTag");
            goals["updatedAtTurn"] = updatedAtTurn;
            profile["goals"] = goals;
        }
    }

    private static void ApplyActorQuestUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "actor_quest_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "actor_quest_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_actor_quest_target");
                continue;
            }

            var status = GetNodeString(update["status"]);
            if (!CommandHasNonEmptyStrings(update, "questId", "goalId", "title", "status", "planSummary", "successCondition") ||
                !ActorQuestStatuses.Contains(status ?? string.Empty) ||
                !TryGetNodeInt(update["createdAtTurn"], out var createdAtTurn) ||
                createdAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, update, "incomplete_actor_quest_update");
                continue;
            }

            var quest = CloneCommandWithoutTarget(update);
            quest["createdAtTurn"] = createdAtTurn;
            UpsertActorQuest(EnsureArray(profile, "personalQuests"), quest);
        }
    }

    private static void ApplyActorActivityUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "actor_activity_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "actor_activity_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_actor_activity_target");
                continue;
            }

            if (!CommandHasNonEmptyStrings(update, "activityId", "goalId", "linkedQuestId", "activityType", "summary", "status", "gmThoughtsSummary") ||
                !string.Equals(GetNodeString(update["status"]), "active", StringComparison.OrdinalIgnoreCase) ||
                !TryGetNodeInt(update["startedAtTurn"], out var startedAtTurn) ||
                startedAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, update, "incomplete_actor_activity_update");
                continue;
            }

            if (!ActorAgencyLinksAreProjectable(profile, update))
            {
                MarkInvalidProfileCommand(result, update, "actor_activity_missing_goal_or_quest");
                continue;
            }

            var activity = CloneCommandWithoutTarget(update);
            activity["startedAtTurn"] = startedAtTurn;
            profile["currentActivity"] = activity;
        }
    }

    private static void ApplyCompleteActorActivities(JsonObject result, JsonNode? completionsNode)
    {
        if (completionsNode == null)
            return;

        if (completionsNode is not JsonArray completions)
        {
            MarkInvalidProfileCommand(result, completionsNode, "complete_actor_activities_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var completionNode in completions)
        {
            if (completionNode is not JsonObject completion)
            {
                MarkInvalidProfileCommand(result, completionNode, "complete_actor_activity_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, completion);
            var outcome = GetNodeString(completion["outcome"]);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, completion, "unknown_actor_activity_completion_target");
                continue;
            }

            if (!CommandHasNonEmptyStrings(completion, "activityId", "outcome", "summary") ||
                !ActorActivityCompletionOutcomes.Contains(outcome ?? string.Empty) ||
                !TryGetNodeInt(completion["completedAtTurn"], out var completedAtTurn) ||
                completedAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, completion, "incomplete_actor_activity_completion");
                continue;
            }

            if (profile["currentActivity"] is not JsonObject currentActivity ||
                !string.Equals(GetNodeString(currentActivity["activityId"]), GetNodeString(completion["activityId"]), StringComparison.OrdinalIgnoreCase))
            {
                MarkInvalidProfileCommand(result, completion, "actor_activity_completion_without_current_activity");
                continue;
            }

            var completed = CloneObject(currentActivity);
            completed["status"] = outcome;
            completed["outcome"] = outcome;
            completed["completionSummary"] = GetNodeString(completion["summary"]);
            completed["completedAtTurn"] = completedAtTurn;
            CopyOptionalCommandFields(completion, completed, "gmThoughtsSummary", "resultingQuestStatus", "actorBrainRef");
            EnsureArray(profile, "completedActivities").Add(completed);
            ApplyResultingQuestStatus(profile, currentActivity, completion);
            profile.Remove("currentActivity");
        }
    }

    private static void ApplyRelationshipChanges(JsonObject result, JsonNode? changesNode)
    {
        if (changesNode == null)
            return;

        if (changesNode is not JsonArray changes)
        {
            MarkInvalidProfileCommand(result, changesNode, "relationship_changes_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var changeNode in changes)
        {
            if (changeNode is not JsonObject change)
            {
                MarkInvalidProfileCommand(result, changeNode, "relationship_change_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, change);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, change, "unknown_relationship_change_target");
                continue;
            }

            var relationship = FindOrCreateRelationship(profile, change);
            if (relationship == null)
            {
                MarkInvalidProfileCommand(result, change, "incomplete_relationship_change");
                continue;
            }

            var currentValue = GetNodeInt(relationship["value"]);
            if (TryGetNodeInt(change["value"], out var absoluteValue))
                relationship["value"] = Math.Clamp(absoluteValue, -100, 100);
            else if (TryGetNodeInt(change["valueDelta"], out var delta))
                relationship["value"] = Math.Clamp(currentValue + delta, -100, 100);

            CopyOptionalCommandFields(change, relationship, "relationshipTier", "reason", "evidence", "gmThoughtsSummary");
            if (TryGetNodeInt(change["updatedAtTurn"], out var updatedAtTurn) && updatedAtTurn >= 0)
                relationship["updatedAtTurn"] = updatedAtTurn;
        }
    }

    private static void ApplyRelationshipLockUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "relationship_lock_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "relationship_lock_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_relationship_lock_target");
                continue;
            }

            var relationship = FindOrCreateRelationship(profile, update);
            if (relationship == null ||
                update["relationshipLock"] is not JsonObject relationshipLock ||
                string.IsNullOrWhiteSpace(GetNodeString(update["gmThoughtsSummary"])))
            {
                MarkInvalidProfileCommand(result, update, "incomplete_relationship_lock_update");
                continue;
            }

            CopyOptionalCommandFields(update, relationship, "relationshipTier");
            relationship["relationshipLock"] = CloneObject(relationshipLock);
        }
    }

    private static void ApplyBreakthroughQuestUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "breakthrough_quest_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "breakthrough_quest_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_breakthrough_quest_target");
                continue;
            }

            var relationship = FindRelationshipForQuestUpdate(profile, update);
            var status = GetNodeString(update["status"]);
            if (relationship == null ||
                !CommandHasNonEmptyStrings(update, "questId", "questType", "status", "title", "sceneSummary", "successCondition", "gmThoughtsSummary") ||
                !RelationshipGateQuestTypes.Contains(GetNodeString(update["questType"]) ?? string.Empty) ||
                !RelationshipGateQuestStatuses.Contains(status ?? string.Empty) ||
                !TryGetNodeInt(update["updatedAtTurn"], out var updatedAtTurn) ||
                updatedAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, update, "incomplete_breakthrough_quest_update");
                continue;
            }

            var quest = CloneCommandWithoutTarget(update);
            quest["updatedAtTurn"] = updatedAtTurn;
            UpsertRelationshipGateQuest(EnsureArray(relationship, RelationshipGateQuestsProperty), quest);

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) &&
                BreakthroughQuestClearsRelationshipLock(relationship, update))
            {
                relationship.Remove("relationshipLock");
            }
        }
    }

    private static JsonObject? FindOrCreateRelationship(JsonObject profile, JsonObject command)
    {
        var relationshipId = GetNodeString(command["relationshipId"]);
        var axis = GetNodeString(command["axis"]);
        var targetActorType = GetNodeString(command["targetActorType"]);
        var targetActorId = GetNodeString(command["targetActorId"]) ?? GetNodeString(command["targetActorRef"]);
        if (string.IsNullOrWhiteSpace(relationshipId) ||
            string.IsNullOrWhiteSpace(axis) ||
            string.IsNullOrWhiteSpace(targetActorType) ||
            string.IsNullOrWhiteSpace(targetActorId))
        {
            return null;
        }

        var relationships = EnsureArray(profile, RelationshipsProperty);
        foreach (var relationship in relationships.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(relationship["relationshipId"]), relationshipId, StringComparison.OrdinalIgnoreCase))
                return relationship;
        }

        var created = new JsonObject
        {
            ["relationshipId"] = relationshipId,
            ["axis"] = axis,
            ["targetActorType"] = targetActorType,
            ["targetActorId"] = targetActorId,
            ["value"] = 0,
            ["relationshipTier"] = GetNodeString(command["relationshipTier"]) ?? "neutral"
        };
        relationships.Add(created);
        return created;
    }

    private static JsonObject? FindRelationshipForQuestUpdate(JsonObject profile, JsonObject command)
    {
        var relationshipId = GetNodeString(command["relationshipId"]);
        if (string.IsNullOrWhiteSpace(relationshipId))
            return null;

        var relationships = profile[RelationshipsProperty] as JsonArray;
        var existing = relationships?
            .OfType<JsonObject>()
            .FirstOrDefault(relationship => string.Equals(GetNodeString(relationship["relationshipId"]), relationshipId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        return FindOrCreateRelationship(profile, command);
    }

    private static void UpsertRelationshipGateQuest(JsonArray quests, JsonObject quest)
    {
        var questId = GetNodeString(quest["questId"]);
        if (string.IsNullOrWhiteSpace(questId))
        {
            quests.Add(CloneObject(quest));
            return;
        }

        for (var index = 0; index < quests.Count; index++)
        {
            if (quests[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["questId"]), questId, StringComparison.OrdinalIgnoreCase))
            {
                quests[index] = CloneObject(quest);
                return;
            }
        }

        quests.Add(CloneObject(quest));
    }

    private static bool BreakthroughQuestClearsRelationshipLock(JsonObject relationship, JsonObject update)
    {
        if (relationship["relationshipLock"] is not JsonObject relationshipLock)
            return false;

        var questType = GetNodeString(update["questType"]);
        if (string.Equals(questType, "breakthrough", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetNodeString(update["breakthroughQuestId"]), "_clear_", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(relationshipLock["breakthroughQuestId"]), GetNodeString(update["questId"]), StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(questType, "redemption", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetNodeString(update["redemptionQuestId"]), "_clear_", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(relationshipLock["redemptionQuestId"]), GetNodeString(update["questId"]), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static void ApplyMaskAdds(JsonObject result, JsonNode? addsNode)
    {
        if (addsNode == null)
            return;

        if (addsNode is not JsonArray adds)
        {
            MarkInvalidProfileCommand(result, addsNode, "mask_adds_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var addNode in adds)
        {
            if (addNode is not JsonObject add)
            {
                MarkInvalidProfileCommand(result, addNode, "mask_add_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, add);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, add, "unknown_mask_add_target");
                continue;
            }

            if (add["mask"] is not JsonObject mask || !MaskIsProjectable(mask))
            {
                MarkInvalidProfileCommand(result, add, "mask_add_missing_payload");
                continue;
            }

            UpsertMask(EnsureArray(profile, MasksProperty), mask);
        }
    }

    private static void ApplyMaskUpdates(JsonObject result, JsonNode? updatesNode)
    {
        if (updatesNode == null)
            return;

        if (updatesNode is not JsonArray updates)
        {
            MarkInvalidProfileCommand(result, updatesNode, "mask_updates_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var updateNode in updates)
        {
            if (updateNode is not JsonObject update)
            {
                MarkInvalidProfileCommand(result, updateNode, "mask_update_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, update);
            if (profile == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_mask_update_target");
                continue;
            }

            if (update["maskUpdate"] is not JsonObject maskUpdate ||
                string.IsNullOrWhiteSpace(GetNodeString(maskUpdate["maskId"])) ||
                profile[MasksProperty] is not JsonArray masks)
            {
                MarkInvalidProfileCommand(result, update, "mask_update_missing_payload");
                continue;
            }

            var existing = masks
                .OfType<JsonObject>()
                .FirstOrDefault(mask => string.Equals(GetNodeString(mask["maskId"]), GetNodeString(maskUpdate["maskId"]), StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                MarkInvalidProfileCommand(result, update, "unknown_mask_update_id");
                continue;
            }

            foreach (var property in maskUpdate)
            {
                if (string.Equals(property.Key, "maskId", StringComparison.OrdinalIgnoreCase))
                    continue;

                existing[property.Key] = property.Value?.DeepClone();
            }
        }
    }

    private static void ApplyActiveMaskChanges(JsonObject result, JsonNode? changesNode)
    {
        if (changesNode == null)
            return;

        if (changesNode is not JsonArray changes)
        {
            MarkInvalidProfileCommand(result, changesNode, "active_mask_changes_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var changeNode in changes)
        {
            if (changeNode is not JsonObject change)
            {
                MarkInvalidProfileCommand(result, changeNode, "active_mask_change_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, change);
            var activeMaskId = GetNodeString(change[ActiveMaskIdProperty]) ?? GetNodeString(change["newActiveMaskId"]);
            if (profile == null || string.IsNullOrWhiteSpace(activeMaskId))
            {
                MarkInvalidProfileCommand(result, change, "active_mask_change_incomplete");
                continue;
            }

            if (!string.Equals(activeMaskId, TrueSelfMaskId, StringComparison.OrdinalIgnoreCase) &&
                !MaskExists(profile, activeMaskId))
            {
                MarkInvalidProfileCommand(result, change, "active_mask_change_unknown_mask");
                continue;
            }

            profile[ActiveMaskIdProperty] = activeMaskId;
            if (TryGetNodeInt(change["updatedAtTurn"], out var updatedAtTurn) && updatedAtTurn >= 0)
                profile["activeMaskUpdatedAtTurn"] = updatedAtTurn;
            CopyOptionalCommandFields(change, profile, "reason", "evidence", "gmThoughtsSummary");
        }
    }

    private static void ApplyMaskRemovals(JsonObject result, JsonNode? removalsNode)
    {
        if (removalsNode == null)
            return;

        if (removalsNode is not JsonArray removals)
        {
            MarkInvalidProfileCommand(result, removalsNode, "mask_removals_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var removalNode in removals)
        {
            if (removalNode is not JsonObject removal)
            {
                MarkInvalidProfileCommand(result, removalNode, "mask_removal_not_object");
                continue;
            }

            var profile = FindTargetProfile(profiles, removal);
            var maskId = GetNodeString(removal["maskId"]);
            if (profile == null || string.IsNullOrWhiteSpace(maskId))
            {
                MarkInvalidProfileCommand(result, removal, "mask_removal_incomplete");
                continue;
            }

            var activeMaskId = GetNodeString(profile[ActiveMaskIdProperty]);
            if (string.Equals(activeMaskId, maskId, StringComparison.OrdinalIgnoreCase))
            {
                var explicitTrueSelf = string.Equals(GetNodeString(removal[ActiveMaskIdProperty]), TrueSelfMaskId, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(GetNodeString(removal["newActiveMaskId"]), TrueSelfMaskId, StringComparison.OrdinalIgnoreCase);
                if (!explicitTrueSelf)
                {
                    MarkInvalidProfileCommand(result, removal, "active_mask_removal_requires_true_self");
                    continue;
                }

                profile[ActiveMaskIdProperty] = TrueSelfMaskId;
            }

            if (profile[MasksProperty] is not JsonArray masks)
                continue;

            for (var index = masks.Count - 1; index >= 0; index--)
            {
                if (masks[index] is JsonObject mask &&
                    string.Equals(GetNodeString(mask["maskId"]), maskId, StringComparison.OrdinalIgnoreCase))
                {
                    masks.RemoveAt(index);
                }
            }
        }
    }

    private static bool MaskIsProjectable(JsonObject mask) =>
        CommandHasNonEmptyStrings(mask, "maskId", "displayName", "publicArchetype", "visiblePersonality", "concealedTruth", "deceptionRisk") &&
        mask["directives"] is JsonArray &&
        mask["revealConditions"] is JsonArray;

    private static void UpsertMask(JsonArray masks, JsonObject mask)
    {
        var maskId = GetNodeString(mask["maskId"]);
        if (string.IsNullOrWhiteSpace(maskId))
        {
            masks.Add(CloneObject(mask));
            return;
        }

        for (var index = 0; index < masks.Count; index++)
        {
            if (masks[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["maskId"]), maskId, StringComparison.OrdinalIgnoreCase))
            {
                masks[index] = CloneObject(mask);
                return;
            }
        }

        masks.Add(CloneObject(mask));
    }

    private static bool MaskExists(JsonObject profile, string maskId) =>
        profile[MasksProperty] is JsonArray masks &&
        masks.OfType<JsonObject>().Any(mask => string.Equals(GetNodeString(mask["maskId"]), maskId, StringComparison.OrdinalIgnoreCase));

    private static void ApplyResultingQuestStatus(JsonObject profile, JsonObject currentActivity, JsonObject completion)
    {
        var resultingQuestStatus = GetNodeString(completion["resultingQuestStatus"]);
        if (string.IsNullOrWhiteSpace(resultingQuestStatus) ||
            !ActorQuestStatuses.Contains(resultingQuestStatus))
        {
            return;
        }

        var questId = GetNodeString(currentActivity["linkedQuestId"]);
        if (string.IsNullOrWhiteSpace(questId) ||
            profile["personalQuests"] is not JsonArray quests)
        {
            return;
        }

        foreach (var quest in quests.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(quest["questId"]), questId, StringComparison.OrdinalIgnoreCase))
            {
                quest["status"] = resultingQuestStatus;
                if (completion.TryGetPropertyValue("summary", out var summary) && summary != null)
                    quest["lastActivitySummary"] = summary.DeepClone();
                return;
            }
        }
    }

    private static JsonObject? FindTargetProfile(JsonArray profiles, JsonObject command)
    {
        var targetKey = BuildIdentityKey(command);
        if (string.IsNullOrWhiteSpace(targetKey))
            return null;

        return profiles
            .OfType<JsonObject>()
            .FirstOrDefault(profile => string.Equals(BuildIdentityKey(profile), targetKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CommandHasNonEmptyStrings(JsonObject command, params string[] fieldNames) =>
        fieldNames.All(field => !string.IsNullOrWhiteSpace(GetNodeString(command[field])));

    private static JsonObject CloneCommandWithoutTarget(JsonObject command)
    {
        var clone = CloneObject(command);
        clone.Remove("actorType");
        clone.Remove("actorId");
        clone.Remove("actorRef");
        return clone;
    }

    private static void CopyCommandFields(JsonObject source, JsonObject target, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
            target[fieldName] = GetNodeString(source[fieldName]);
    }

    private static void CopyOptionalCommandFields(JsonObject source, JsonObject target, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (source.TryGetPropertyValue(fieldName, out var value) && value != null)
                target[fieldName] = value.DeepClone();
        }
    }

    private static void UpsertActorQuest(JsonArray quests, JsonObject quest)
    {
        var questId = GetNodeString(quest["questId"]);
        if (string.IsNullOrWhiteSpace(questId))
        {
            quests.Add(quest);
            return;
        }

        for (var index = 0; index < quests.Count; index++)
        {
            if (quests[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["questId"]), questId, StringComparison.OrdinalIgnoreCase))
            {
                quests[index] = quest;
                return;
            }
        }

        quests.Add(quest);
    }

    private static bool ActorAgencyLinksAreProjectable(JsonObject profile, JsonObject activity)
    {
        var goalId = GetNodeString(activity["goalId"]);
        var linkedQuestId = GetNodeString(activity["linkedQuestId"]);
        if (string.IsNullOrWhiteSpace(goalId) || string.IsNullOrWhiteSpace(linkedQuestId))
            return false;

        if (profile["goals"] is not JsonObject goals ||
            !string.Equals(GetNodeString(goals["goalId"]), goalId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return profile["personalQuests"] is JsonArray quests &&
               quests.OfType<JsonObject>().Any(quest =>
                   string.Equals(GetNodeString(quest["questId"]), linkedQuestId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(quest["goalId"]), goalId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(quest["status"]), "active", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyProgressionOverrides(JsonObject result, JsonNode? overridesNode)
    {
        if (overridesNode == null)
            return;

        if (overridesNode is not JsonArray overrides)
        {
            MarkInvalidProgressionOverride(result, overridesNode, "progression_overrides_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var overrideEntry in overrides)
        {
            if (overrideEntry is not JsonObject overrideNode)
            {
                MarkInvalidProgressionOverride(result, overrideEntry, "progression_override_not_object");
                continue;
            }

            var targetKey = BuildIdentityKey(overrideNode);
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                MarkInvalidProgressionOverride(result, overrideNode, "missing_target_profile");
                continue;
            }

            var profile = profiles
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(BuildIdentityKey(item), targetKey, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                MarkInvalidProgressionOverride(result, overrideNode, "unknown_target_profile");
                continue;
            }

            var cycleKey = GetNodeString(overrideNode["cycleKey"]);
            var reason = GetNodeString(overrideNode["reason"]);
            var summary = GetNodeString(overrideNode["summary"]);
            if (string.IsNullOrWhiteSpace(cycleKey) ||
                string.IsNullOrWhiteSpace(reason) ||
                string.IsNullOrWhiteSpace(summary))
            {
                MarkInvalidProgressionOverride(result, overrideNode, "incomplete_progression_override");
                continue;
            }

            if (!TryValidateProgressionOverrideDeltasForProjection(profile, overrideNode, out var invalidReason))
            {
                MarkInvalidProgressionOverride(result, overrideNode, invalidReason);
                continue;
            }

            ApplyCurrencyDeltas(profile, overrideNode["currencyDeltas"] as JsonObject);
            ApplyStandardArtTierDeltas(profile, overrideNode["standardArtTierDeltas"] as JsonObject);
            ApplySpecialArtTierDeltas(profile, overrideNode["specialArtTierDeltas"] as JsonObject);
            ApplySoulDissipationTierDelta(profile, overrideNode["soulDissipationTierDelta"]);
            ApplyProgressionExperienceDeltas(profile, overrideNode["progressionExperienceDeltas"] as JsonObject);

            var (overrideIncome, overrideSpending) = SplitSignedCurrencyDeltasForLedger(overrideNode["currencyDeltas"] as JsonObject);
            var strategy = EnsureObject(profile, "progressionStrategy");
            strategy["lastAutoProgressionCycleKey"] = cycleKey;
            AppendProgressionLedger(profile, new JsonObject
            {
                ["entryId"] = BuildProgressionLedgerEntryId(profile, cycleKey, "gm_override"),
                ["cycleKey"] = cycleKey,
                ["source"] = "gm_override",
                ["summary"] = summary,
                ["income"] = new JsonObject
                {
                    ["inkFeathers"] = overrideIncome.InkFeathers,
                    ["lightSparks"] = overrideIncome.LightSparks
                },
                ["spending"] = new JsonObject
                {
                    ["inkFeathers"] = overrideSpending.InkFeathers,
                    ["lightSparks"] = overrideSpending.LightSparks
                },
            });
        }
    }

    private static bool TryValidateProgressionOverrideDeltasForProjection(
        JsonObject profile,
        JsonObject overrideNode,
        out string invalidReason)
    {
        invalidReason = string.Empty;
        var hasDelta = false;

        if (overrideNode.ContainsKey("currencyDeltas"))
        {
            hasDelta = true;
            if (!SignedIntegerObjectIsProjectable(overrideNode["currencyDeltas"], CurrencyDeltaKeys))
            {
                invalidReason = "invalid_currency_delta";
                return false;
            }
        }

        if (overrideNode.ContainsKey("standardArtTierDeltas"))
        {
            hasDelta = true;
            if (!StandardArtTierDeltasAreProjectable(overrideNode["standardArtTierDeltas"]))
            {
                invalidReason = "invalid_standard_art_delta";
                return false;
            }
        }

        if (overrideNode.ContainsKey("specialArtTierDeltas"))
        {
            hasDelta = true;
            if (!SpecialArtTierDeltasAreProjectable(profile, overrideNode["specialArtTierDeltas"], out invalidReason))
                return false;
        }

        if (overrideNode.ContainsKey("soulDissipationTierDelta"))
        {
            hasDelta = true;
            if (!TierDeltaIsProjectable(overrideNode["soulDissipationTierDelta"]))
            {
                invalidReason = "invalid_soul_dissipation_delta";
                return false;
            }
        }

        if (overrideNode.ContainsKey("progressionExperienceDeltas"))
        {
            hasDelta = true;
            if (!SignedIntegerObjectIsProjectable(overrideNode["progressionExperienceDeltas"], ProgressionExperienceDeltaKeys))
            {
                invalidReason = "invalid_progression_delta";
                return false;
            }
        }

        if (!hasDelta)
        {
            invalidReason = "empty_progression_override";
            return false;
        }

        return true;
    }

    private static bool SignedIntegerObjectIsProjectable(JsonNode? node, IReadOnlySet<string> allowedKeys)
    {
        if (node is not JsonObject deltas)
            return false;

        return deltas.Count > 0 &&
               deltas.All(delta => allowedKeys.Contains(delta.Key) && TryGetNodeInt(delta.Value, out _));
    }

    private static bool StandardArtTierDeltasAreProjectable(JsonNode? node)
    {
        if (node is not JsonObject deltas)
            return false;

        return deltas.Count > 0 &&
               deltas.All(delta => StandardArtIds.Contains(delta.Key) && TryGetNodeInt(delta.Value, out _));
    }

    private static bool SpecialArtTierDeltasAreProjectable(
        JsonObject profile,
        JsonNode? node,
        out string invalidReason)
    {
        invalidReason = string.Empty;
        if (node is not JsonObject deltas)
        {
            invalidReason = "invalid_special_art_delta";
            return false;
        }

        if (deltas.Count == 0)
        {
            invalidReason = "invalid_special_art_delta";
            return false;
        }

        foreach (var delta in deltas)
        {
            if (string.IsNullOrWhiteSpace(delta.Key) ||
                !TryGetNodeInt(delta.Value, out var value) ||
                value < -MaxProfileTier ||
                value > MaxProfileTier)
            {
                invalidReason = "invalid_special_art_delta";
                return false;
            }

            if (FindSpecialArtById(profile, delta.Key) == null)
            {
                invalidReason = "unknown_special_art";
                return false;
            }
        }

        return true;
    }

    private static bool TierDeltaIsProjectable(JsonNode? node) =>
        TryGetNodeInt(node, out var value) &&
        value >= -MaxProfileTier &&
        value <= MaxProfileTier;

    private static void ApplySpecialArtLearningReceipts(JsonObject result, JsonNode? receiptsNode)
    {
        if (receiptsNode == null)
            return;

        if (receiptsNode is not JsonArray receipts)
        {
            MarkInvalidProfileCommand(result, receiptsNode, "special_art_learning_receipts_not_array");
            return;
        }

        var profiles = EnsureProfilesArray(result);
        foreach (var receiptNode in receipts)
        {
            if (receiptNode is not JsonObject receipt)
            {
                MarkInvalidProfileCommand(result, receiptNode, "special_art_learning_receipt_not_object");
                continue;
            }

            var artId = GetNodeString(receipt["artId"]);
            var teacherActorType = GetNodeString(receipt["teacherActorType"]);
            var teacherActorId = GetNodeString(receipt["teacherActorId"]) ?? GetNodeString(receipt["teacherActorRef"]);
            var playerActorId = GetNodeString(receipt["playerActorId"]);
            var receiptId = GetNodeString(receipt["receiptId"]);
            var roleplayEvidence = GetNodeString(receipt["roleplayEvidence"]);
            var summary = GetNodeString(receipt["summary"]);
            if (string.IsNullOrWhiteSpace(artId) ||
                string.IsNullOrWhiteSpace(teacherActorType) ||
                string.IsNullOrWhiteSpace(teacherActorId) ||
                string.IsNullOrWhiteSpace(playerActorId) ||
                string.IsNullOrWhiteSpace(receiptId) ||
                string.IsNullOrWhiteSpace(roleplayEvidence) ||
                string.IsNullOrWhiteSpace(summary))
            {
                MarkInvalidProfileCommand(result, receipt, "incomplete_special_art_learning_receipt");
                continue;
            }

            if (!ActorTypes.Contains(teacherActorType))
            {
                MarkInvalidProfileCommand(result, receipt, "invalid_special_art_learning_teacher_actor_type");
                continue;
            }

            if (receipt["trainingConditionSatisfied"] is not JsonValue conditionValue ||
                !conditionValue.TryGetValue<bool>(out var conditionSatisfied) ||
                !conditionSatisfied)
            {
                MarkInvalidProfileCommand(result, receipt, "special_art_learning_condition_not_satisfied");
                continue;
            }

            if (!TryGetNodeInt(receipt["learnedAtTurn"], out var learnedAtTurn) || learnedAtTurn < 0)
            {
                MarkInvalidProfileCommand(result, receipt, "invalid_special_art_learning_turn");
                continue;
            }

            if (receipt.ContainsKey("initialTier") &&
                (!TryGetNodeInt(receipt["initialTier"], out var initialTier) || initialTier != 0))
            {
                MarkInvalidProfileCommand(result, receipt, "invalid_special_art_learning_initial_tier");
                continue;
            }

            var teacherProfile = FindProfileByIdentity(profiles, teacherActorType, teacherActorId);
            var playerProfile = FindProfileByIdentity(profiles, "player_soul", playerActorId);
            var sourceArt = FindSpecialArtById(teacherProfile, artId);
            if (teacherProfile == null)
            {
                MarkInvalidProfileCommand(result, receipt, "unknown_special_art_learning_teacher");
                continue;
            }

            if (playerProfile == null)
            {
                MarkInvalidProfileCommand(result, receipt, "unknown_special_art_learning_player");
                continue;
            }

            if (sourceArt == null)
            {
                MarkInvalidProfileCommand(result, receipt, "unknown_special_art_learning_art");
                continue;
            }

            if (!CanTeachPlayer(sourceArt))
            {
                MarkInvalidProfileCommand(result, receipt, "special_art_learning_not_teachable");
                continue;
            }

            var learnedArt = CloneObject(sourceArt);
            learnedArt["ownerActorType"] = "player_soul";
            learnedArt["ownerActorId"] = playerActorId;
            learnedArt["tier"] = 0;
            learnedArt["canTeachPlayer"] = false;
            learnedArt["learnedFromActorType"] = teacherActorType;
            learnedArt["learnedFromActorId"] = teacherActorId;
            learnedArt["learnedAtTurn"] = learnedAtTurn;
            learnedArt["learningReceiptId"] = receiptId;

            var playerArts = EnsureArray(playerProfile, "specialArts");
            UpsertSpecialArt(playerArts, learnedArt);

            AppendLedger(playerProfile, new JsonObject
            {
                ["entryId"] = receiptId,
                ["turnNumber"] = learnedAtTurn,
                ["reason"] = "learn_special_art",
                ["summary"] = summary
            });
        }
    }

    private static HashSet<string> ApplyAutomaticProgression(JsonObject result, JsonObject? progressionReportRoot)
    {
        var progressedProfileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (progressionReportRoot == null)
            return progressedProfileKeys;

        if (result[ProfilesProperty] is not JsonArray profiles)
            return progressedProfileKeys;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (IsPlayerSoulProfile(profile))
                continue;

            var cycle = ResolveProgressionCycleForProfile(profile, progressionReportRoot);
            if (cycle == null || !ApplyAutomaticProgression(profile, cycle.Value))
                continue;

            if (profile["mentorTrainingShowcase"] is not JsonObject)
                continue;

            var key = BuildIdentityKey(profile);
            if (!string.IsNullOrWhiteSpace(key))
                progressedProfileKeys.Add(key);
        }

        return progressedProfileKeys;
    }

    private static bool IsPlayerSoulProfile(JsonObject profile) =>
        string.Equals(GetNodeString(profile["actorType"]), "player_soul", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(GetNodeString(profile["actorId"]) ?? GetNodeString(profile["actorRef"]), "player_soul", StringComparison.OrdinalIgnoreCase);

    private static JsonObject BuildPlayerSoulCurrencies(JsonObject soulRoot, JsonObject? shiningRoot) =>
        new()
        {
            ["inkFeathers"] = ReadSoulInkFeathersCurrent(soulRoot),
            ["lightSparks"] = IsShiningRealm(GetNodeString(soulRoot["currentRealm"]))
                ? Math.Max(0, GetNodeInt(shiningRoot?["lightSparks"]))
                : 0
        };

    private static JsonObject BuildPlayerSoulProgression(JsonObject soulRoot, JsonObject? shiningRoot)
    {
        var (enlightenmentExperience, enlightenmentTier) = ResolvePlayerSoulEnlightenmentProgression(soulRoot);
        var (radianceExperience, radianceTier) = ResolvePlayerSoulRadianceProgression(shiningRoot);
        return new JsonObject
        {
            ["enlightenment"] = new JsonObject
            {
                ["experience"] = enlightenmentExperience,
                ["tier"] = enlightenmentTier
            },
            ["radiance"] = new JsonObject
            {
                ["experience"] = radianceExperience,
                ["tier"] = radianceTier
            }
        };
    }

    private static JsonObject BuildPlayerSoulStandardArts(JsonObject soulRoot)
    {
        var result = new JsonObject();
        if (soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] is not JsonObject profile ||
            profile["artTiers"] is not JsonObject artTiers)
        {
            return result;
        }

        foreach (var property in artTiers)
        {
            if (!StandardArtIds.Contains(property.Key))
                continue;

            result[property.Key] = Math.Clamp(GetNodeInt(property.Value), 0, MaxProfileTier);
        }

        return result;
    }

    private static void DisablePlayerSoulProfileAutoProgression(JsonObject profile)
    {
        var strategy = profile["progressionStrategy"] as JsonObject;
        if (strategy == null)
            return;

        strategy["autoProgressionEnabled"] = false;
        strategy.Remove("lastAutoProgressionCycleKey");
    }

    private static void RemovePlayerSoulAutomaticProgressionLedgerEntries(JsonObject profile)
    {
        if (profile[ProgressionLedgerProperty] is not JsonArray ledger)
            return;

        for (var index = ledger.Count - 1; index >= 0; index--)
        {
            if (ledger[index] is not JsonObject entry)
                continue;

            if (string.Equals(GetNodeString(entry["source"]), "client_auto_strategy", StringComparison.OrdinalIgnoreCase))
                ledger.RemoveAt(index);
        }
    }

    private static int ReadSoulInkFeathersCurrent(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject feathers)
            return Math.Max(0, GetNodeInt(feathers["current"]));

        return Math.Max(0, GetNodeInt(soulRoot["inkFeathers"]));
    }

    private static (int Experience, int Tier) ResolvePlayerSoulEnlightenmentProgression(JsonObject soulRoot)
    {
        var directProgress = GetNodeInt(soulRoot["enlightenment"]);
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var soulProgression = soulRoot["soulProgression"] as JsonObject;
        var experience = Math.Max(
            Math.Max(directProgress, GetNodeInt(enlightenment?["experience"])),
            Math.Max(GetNodeInt(soulProgression?["totalExperience"]), GetNodeInt(soulProgression?["progressPercent"])));
        var tier = Math.Max(GetNodeInt(enlightenment?["level"]), GetNodeInt(soulProgression?["tier"]));
        return (
            Math.Max(0, experience),
            Math.Clamp(Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.EnlightenmentRanks, experience)), 0, MaxProfileTier));
    }

    private static (int Experience, int Tier) ResolvePlayerSoulRadianceProgression(JsonObject? shiningRoot)
    {
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var experience = Math.Max(0, GetNodeInt(radiance?["experience"]));
        var tier = GetNodeInt(radiance?["tier"]);
        return (
            experience,
            Math.Clamp(Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.RadianceRanks, experience)), 0, MaxProfileTier));
    }

    private static int ResolveRankFromProgress(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int progress) =>
        ranks
            .Where(rank => progress >= rank.RequiredProgress)
            .Select(rank => rank.Rank)
            .DefaultIfEmpty(0)
            .Max();

    private static bool IsShiningRealm(string? realm) =>
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonObject?> ReadObjectAsync(FileSystemManager fs, string relativePath)
    {
        var raw = await fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static bool ApplyAutomaticProgression(JsonObject profile, ProgressionCycle cycle)
    {
        var strategy = profile["progressionStrategy"] as JsonObject;
        if (strategy == null)
            return false;

        if (strategy["autoProgressionEnabled"] is JsonValue enabledValue &&
            enabledValue.TryGetValue<bool>(out var enabled) &&
            !enabled)
        {
            return false;
        }

        if (string.Equals(GetNodeString(strategy["lastAutoProgressionCycleKey"]), cycle.CycleKey, StringComparison.OrdinalIgnoreCase))
            return false;

        var currencies = EnsureObject(profile, "currencies");
        var income = ResolveIncome(cycle);
        AddCurrency(currencies, "inkFeathers", income.InkFeathers);
        AddCurrency(currencies, "lightSparks", income.LightSparks);

        var spending = new CurrencyDelta(0, 0);
        var upgrades = new JsonArray();
        ApplyStrategyUpgrade(profile, cycle, strategy, currencies, ref spending, upgrades);

        strategy["lastAutoProgressionCycleKey"] = cycle.CycleKey;
        AppendProgressionLedger(profile, new JsonObject
        {
            ["entryId"] = BuildProgressionLedgerEntryId(profile, cycle.CycleKey, "auto"),
            ["cycleKey"] = cycle.CycleKey,
            ["source"] = "client_auto_strategy",
            ["summary"] = upgrades.Count > 0
                ? "Автопрокачка по стратегии применила доход и один приоритетный апгрейд."
                : "Автопрокачка по стратегии применила доход; доступного апгрейда не было.",
            ["income"] = new JsonObject
            {
                ["inkFeathers"] = income.InkFeathers,
                ["lightSparks"] = income.LightSparks
            },
            ["spending"] = new JsonObject
            {
                ["inkFeathers"] = spending.InkFeathers,
                ["lightSparks"] = spending.LightSparks
            },
            ["upgrades"] = upgrades
        });

        return true;
    }

    private static void ApplyStrategyUpgrade(
        JsonObject profile,
        ProgressionCycle cycle,
        JsonObject strategy,
        JsonObject currencies,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        if (strategy["priorityOrder"] is not JsonArray priorities)
            return;

        var reserve = ResolveResourceReserve(strategy);
        foreach (var priorityNode in priorities)
        {
            var priority = GetNodeString(priorityNode);
            if (string.IsNullOrWhiteSpace(priority))
                continue;

            var spendCategory = ClassifyProgressionSpend(profile, priority);
            if (string.IsNullOrWhiteSpace(spendCategory) || !SpendAllowedByStrategy(strategy, spendCategory))
                continue;

            if (StandardArtIds.Contains(priority) &&
                TryUpgradeStandardArt(profile, currencies, priority, reserve, ref spending, upgrades))
            {
                return;
            }

            if (TryUpgradeSpecialArt(profile, currencies, priority, reserve, ref spending, upgrades))
                return;

            if (IsSoulDissipationPriority(priority) &&
                TryUpgradeSoulDissipation(profile, currencies, reserve, ref spending, upgrades))
            {
                return;
            }

            if (string.Equals(priority, "enlightenment", StringComparison.OrdinalIgnoreCase) &&
                TryUpgradeProgressionTrack(profile, currencies, "enlightenment", false, reserve, ref spending, upgrades))
            {
                return;
            }

            if (cycle.IsShining &&
                string.Equals(priority, "radiance", StringComparison.OrdinalIgnoreCase) &&
                TryUpgradeProgressionTrack(profile, currencies, "radiance", true, reserve, ref spending, upgrades))
            {
                return;
            }
        }
    }

    private static bool TryUpgradeStandardArt(
        JsonObject profile,
        JsonObject currencies,
        string artId,
        CurrencyDelta reserve,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        var arts = EnsureObject(profile, "standardArts");
        var currentTier = Math.Clamp(GetNodeInt(arts[artId]), 0, MaxProfileTier);
        if (currentTier >= MaxProfileTier)
            return false;

        var cost = new CurrencyDelta(10 * (currentTier + 1), 0);
        if (!CanAfford(currencies, cost, reserve))
            return false;

        Spend(currencies, cost);
        arts[artId] = currentTier + 1;
        spending = AddSpending(spending, cost);
        upgrades.Add($"{artId}:{currentTier}->{currentTier + 1}");
        return true;
    }

    private static bool TryUpgradeSpecialArt(
        JsonObject profile,
        JsonObject currencies,
        string artId,
        CurrencyDelta reserve,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        var specialArt = FindSpecialArtById(profile, artId);
        if (specialArt == null)
            return false;

        var currentTier = Math.Clamp(GetNodeInt(specialArt["tier"]), 0, MaxProfileTier);
        if (currentTier >= MaxProfileTier)
            return false;

        var cost = ResolveSpecialArtUpgradeCost(specialArt);
        if (!CanAfford(currencies, cost, reserve))
            return false;

        Spend(currencies, cost);
        specialArt["tier"] = currentTier + 1;
        spending = AddSpending(spending, cost);
        upgrades.Add($"specialArt:{artId}:{currentTier}->{currentTier + 1}");
        return true;
    }

    private static bool TryUpgradeSoulDissipation(
        JsonObject profile,
        JsonObject currencies,
        CurrencyDelta reserve,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        var currentTier = Math.Clamp(GetNodeInt(profile[SoulDissipationTierProperty]), 0, MaxProfileTier);
        if (currentTier >= MaxProfileTier)
            return false;

        var cost = ResolveSoulDissipationUpgradeCost(profile, currentTier + 1);
        if (!CanAfford(currencies, cost, reserve))
            return false;

        Spend(currencies, cost);
        profile[SoulDissipationTierProperty] = currentTier + 1;
        spending = AddSpending(spending, cost);
        upgrades.Add($"soulDissipation:{currentTier}->{currentTier + 1}");
        return true;
    }

    private static CurrencyDelta ResolveSpecialArtUpgradeCost(JsonObject specialArt)
    {
        var upgradeCost = specialArt["upgradeCost"] as JsonObject;
        return new CurrencyDelta(
            Math.Max(0, GetNodeInt(upgradeCost?["inkFeathers"])),
            Math.Max(0, GetNodeInt(upgradeCost?["lightSparks"])));
    }

    private static CurrencyDelta ResolveSoulDissipationUpgradeCost(JsonObject profile, int nextTier)
    {
        var tier = Math.Clamp(nextTier, 1, MaxProfileTier);
        return IsShiningRealm(profile)
            ? new CurrencyDelta(30 * tier, 2 * tier)
            : new CurrencyDelta(50 * tier, 0);
    }

    private static bool TryUpgradeProgressionTrack(
        JsonObject profile,
        JsonObject currencies,
        string trackName,
        bool useLightSparks,
        CurrencyDelta reserve,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        var progression = EnsureObject(profile, "progression");
        var track = EnsureObject(progression, trackName);
        var tier = Math.Clamp(GetNodeInt(track["tier"]), 0, MaxProfileTier);
        if (tier >= MaxProfileTier)
            return false;

        var currencyName = useLightSparks ? "lightSparks" : "inkFeathers";
        var cost = useLightSparks ? tier + 1 : 10 * (tier + 1);
        var costDelta = useLightSparks ? new CurrencyDelta(0, cost) : new CurrencyDelta(cost, 0);
        if (!CanAfford(currencies, costDelta, reserve))
            return false;

        Spend(currencies, costDelta);
        var nextExperience = SaturatingAddNonNegative(GetNodeInt(track["experience"]), 20);
        track["experience"] = nextExperience;
        track["tier"] = Math.Min(MaxProfileTier, Math.Max(tier, nextExperience / 20));

        spending = AddSpending(spending, costDelta);
        upgrades.Add($"{trackName}:experience+20");
        return true;
    }

    private static bool IsSoulDissipationPriority(string priority) =>
        string.Equals(priority, "soul_dissipation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(priority, "soulDissipation", StringComparison.OrdinalIgnoreCase);

    private static string? ClassifyProgressionSpend(JsonObject profile, string priority)
    {
        if (StandardArtIds.Contains(priority))
            return "standardArts";

        if (FindSpecialArtById(profile, priority) != null)
            return "specialArts";

        if (IsSoulDissipationPriority(priority))
            return "soulDissipationTier";

        if (string.Equals(priority, "enlightenment", StringComparison.OrdinalIgnoreCase))
            return "enlightenment";

        if (string.Equals(priority, "radiance", StringComparison.OrdinalIgnoreCase))
            return "radiance";

        return null;
    }

    private static bool SpendAllowedByStrategy(JsonObject strategy, string category)
    {
        if (strategy["allowedSpends"] is JsonArray allowed && !ArrayContainsString(allowed, category))
            return false;

        return strategy["forbiddenSpends"] is not JsonArray forbidden || !ArrayContainsString(forbidden, category);
    }

    private static bool ArrayContainsString(JsonArray array, string value) =>
        array.Any(item => string.Equals(GetNodeString(item), value, StringComparison.OrdinalIgnoreCase));

    private static CurrencyDelta ResolveResourceReserve(JsonObject strategy)
    {
        var reserve = strategy["resourceReserve"] as JsonObject;
        return new CurrencyDelta(
            Math.Max(0, GetNodeInt(reserve?["inkFeathers"])),
            Math.Max(0, GetNodeInt(reserve?["lightSparks"])));
    }

    private static bool CanAfford(JsonObject currencies, CurrencyDelta cost, CurrencyDelta reserve) =>
        GetNodeInt(currencies["inkFeathers"]) - reserve.InkFeathers >= cost.InkFeathers &&
        GetNodeInt(currencies["lightSparks"]) - reserve.LightSparks >= cost.LightSparks &&
        (cost.InkFeathers > 0 || cost.LightSparks > 0);

    private static void Spend(JsonObject currencies, CurrencyDelta cost)
    {
        AddCurrency(currencies, "inkFeathers", -cost.InkFeathers);
        AddCurrency(currencies, "lightSparks", -cost.LightSparks);
    }

    private static CurrencyDelta AddSpending(CurrencyDelta spending, CurrencyDelta cost) =>
        new(
            SaturateNonNegativeLongToInt((long)spending.InkFeathers + cost.InkFeathers),
            SaturateNonNegativeLongToInt((long)spending.LightSparks + cost.LightSparks));

    private static ProgressionCycle? ResolveProgressionCycleForProfile(JsonObject profile, JsonObject root)
    {
        var report = root["progressionProcessingReport"] as JsonObject ?? root;
        return IsShiningRealm(profile)
            ? ResolveShiningProgressionCycle(report)
            : ResolveChaosProgressionCycle(report);
    }

    private static ProgressionCycle? ResolveShiningProgressionCycle(JsonObject report)
    {
        var shiningCycles = new[]
        {
            GetNodeInt(report["shiningAbodeCyclesProcessed"]),
            GetNodeInt(report["shiningFactionCyclesProcessed"]),
            GetNodeInt(report["shiningTradeCyclesProcessed"])
        }.Max();
        if (shiningCycles > 0)
        {
            var ordinal = new[]
            {
                GetNodeInt(report["newLastShiningAbodeCycleOrdinal"]),
                GetNodeInt(report["newLastShiningFactionCycleOrdinal"]),
                GetNodeInt(report["newLastShiningTradeCycleOrdinal"])
            }.Max();
            return new ProgressionCycle($"shining:{Math.Max(1, ordinal)}", shiningCycles, IsShining: true);
        }

        return null;
    }

    private static ProgressionCycle? ResolveChaosProgressionCycle(JsonObject report)
    {
        var chaosCycles = new[]
        {
            GetNodeInt(report["chaosSeaCyclesProcessed"]),
            GetNodeInt(report["guardianProjectCyclesProcessed"]),
            GetNodeInt(report["residentAgencyCyclesProcessed"])
        }.Max();
        if (chaosCycles <= 0)
            return null;

        var chaosOrdinal = new[]
        {
            GetNodeInt(report["newLastChaosSeaSimulationOrdinal"]),
            GetNodeInt(report["newLastGuardianProjectCycleOrdinal"]),
            GetNodeInt(report["newLastResidentAgencyCycleOrdinal"])
        }.Max();
        return new ProgressionCycle($"chaos:{Math.Max(1, chaosOrdinal)}", chaosCycles, IsShining: false);
    }

    private static CurrencyDelta ResolveIncome(ProgressionCycle cycle)
    {
        var multiplier = Math.Max(1, cycle.CyclesProcessed);
        return cycle.IsShining
            ? new CurrencyDelta(SaturateNonNegativeLongToInt(6L * multiplier), SaturateNonNegativeLongToInt(1L * multiplier))
            : new CurrencyDelta(SaturateNonNegativeLongToInt(12L * multiplier), 0);
    }

    private static bool IsShiningRealm(JsonObject profile)
    {
        var realm = GetNodeString(profile["realm"]);
        return string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyCurrencyDeltas(JsonObject profile, JsonObject? deltas)
    {
        if (deltas == null)
            return;

        var currencies = EnsureObject(profile, "currencies");
        AddCurrency(currencies, "inkFeathers", GetNodeInt(deltas["inkFeathers"]));
        AddCurrency(currencies, "lightSparks", GetNodeInt(deltas["lightSparks"]));
    }

    private static (CurrencyDelta Income, CurrencyDelta Spending) SplitSignedCurrencyDeltasForLedger(JsonObject? deltas)
    {
        var inkFeathers = GetNodeInt(deltas?["inkFeathers"]);
        var lightSparks = GetNodeInt(deltas?["lightSparks"]);
        return (
            new CurrencyDelta(SaturateNonNegativeLongToInt(Math.Max(0L, inkFeathers)), SaturateNonNegativeLongToInt(Math.Max(0L, lightSparks))),
            new CurrencyDelta(SaturateNonNegativeLongToInt(Math.Max(0L, -(long)inkFeathers)), SaturateNonNegativeLongToInt(Math.Max(0L, -(long)lightSparks))));
    }

    private static void ApplyStandardArtTierDeltas(JsonObject profile, JsonObject? deltas)
    {
        if (deltas == null)
            return;

        var arts = EnsureObject(profile, "standardArts");
        foreach (var delta in deltas)
        {
            if (!StandardArtIds.Contains(delta.Key))
                continue;

            arts[delta.Key] = SaturatingAddThenClamp(GetNodeInt(arts[delta.Key]), GetNodeInt(delta.Value), 0, MaxProfileTier);
        }
    }

    private static void MarkInvalidProgressionOverride(JsonObject result, JsonNode? overrideNode, string reason)
    {
        result[LastInvalidProgressionOverrideProperty] = overrideNode is JsonObject overrideObject
            ? CloneObject(overrideObject)
            : new JsonObject
            {
                ["raw"] = overrideNode?.ToJsonString() ?? "missing"
            };
        result[LastInvalidProgressionOverrideReasonProperty] = reason;
    }

    private static void MarkInvalidProfileCommand(JsonObject result, JsonNode? commandNode, string reason)
    {
        result[LastInvalidCommandProperty] = commandNode is JsonObject commandObject
            ? CloneObject(commandObject)
            : new JsonObject
            {
                ["raw"] = commandNode?.ToJsonString() ?? "missing"
            };
        result[LastInvalidCommandReasonProperty] = reason;
    }

    private static void ApplySpecialArtTierDeltas(JsonObject profile, JsonObject? deltas)
    {
        if (deltas == null)
            return;

        foreach (var delta in deltas)
        {
            var specialArt = FindSpecialArtById(profile, delta.Key);
            if (specialArt == null)
                continue;

            specialArt["tier"] = SaturatingAddThenClamp(GetNodeInt(specialArt["tier"]), GetNodeInt(delta.Value), 0, MaxProfileTier);
        }
    }

    private static bool HasUnknownSpecialArtTierDelta(JsonObject profile, JsonObject? deltas, out string? unknownArtId)
    {
        unknownArtId = null;
        if (deltas == null)
            return false;

        foreach (var delta in deltas)
        {
            if (string.IsNullOrWhiteSpace(delta.Key))
                continue;

            if (FindSpecialArtById(profile, delta.Key) != null)
                continue;

            unknownArtId = delta.Key;
            return true;
        }

        return false;
    }

    private static void ApplySoulDissipationTierDelta(JsonObject profile, JsonNode? deltaNode)
    {
        var delta = GetNodeInt(deltaNode);
        if (delta == 0)
            return;

        profile[SoulDissipationTierProperty] = Math.Clamp(
            SaturatingAddThenClamp(GetNodeInt(profile[SoulDissipationTierProperty]), delta, 0, MaxProfileTier),
            0,
            MaxProfileTier);
    }

    private static void ApplyProgressionExperienceDeltas(JsonObject profile, JsonObject? deltas)
    {
        if (deltas == null)
            return;

        var progression = EnsureObject(profile, "progression");
        foreach (var trackName in new[] { "enlightenment", "radiance" })
        {
            var delta = GetNodeInt(deltas[trackName]);
            if (delta == 0)
                continue;

            var track = EnsureObject(progression, trackName);
            var nextExperience = SaturatingAddNonNegative(GetNodeInt(track["experience"]), delta);
            track["experience"] = nextExperience;
            track["tier"] = Math.Min(MaxProfileTier, Math.Max(GetNodeInt(track["tier"]), nextExperience / 20));
        }
    }

    private static void AddCurrency(JsonObject currencies, string propertyName, int delta)
    {
        currencies[propertyName] = SaturatingAddNonNegative(GetNodeInt(currencies[propertyName]), delta);
    }

    private static int SaturatingAddNonNegative(int current, int delta) =>
        SaturateNonNegativeLongToInt((long)Math.Max(0, current) + delta);

    private static int SaturatingAddThenClamp(int current, int delta, int min, int max) =>
        (int)Math.Clamp((long)current + delta, min, max);

    private static int SaturateNonNegativeLongToInt(long value)
    {
        if (value <= 0)
            return 0;

        return value >= int.MaxValue
            ? int.MaxValue
            : (int)value;
    }

    private static JsonObject EnsureObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject obj)
            return obj;

        obj = new JsonObject();
        root[propertyName] = obj;
        return obj;
    }

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static JsonObject? FindProfileByIdentity(JsonArray profiles, string actorType, string actorId)
    {
        var expected = $"{actorType.Trim()}:{actorId.Trim()}";
        return profiles
            .OfType<JsonObject>()
            .FirstOrDefault(profile => string.Equals(BuildIdentityKey(profile), expected, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindSpecialArtById(JsonObject? profile, string artId)
    {
        if (profile?["specialArts"] is not JsonArray arts)
            return null;

        return arts
            .OfType<JsonObject>()
            .FirstOrDefault(art => string.Equals(GetNodeString(art["artId"]), artId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanTeachPlayer(JsonObject specialArt)
    {
        return specialArt["canTeachPlayer"] is JsonValue value &&
               value.TryGetValue<bool>(out var canTeach) &&
               canTeach;
    }

    private static void UpsertSpecialArt(JsonArray arts, JsonObject art)
    {
        var artId = GetNodeString(art["artId"]);
        if (string.IsNullOrWhiteSpace(artId))
        {
            arts.Add(CloneObject(art));
            return;
        }

        for (var index = 0; index < arts.Count; index++)
        {
            if (arts[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["artId"]), artId, StringComparison.OrdinalIgnoreCase))
            {
                arts[index] = CloneObject(art);
                return;
            }
        }

        arts.Add(CloneObject(art));
    }

    private static void AppendLedger(JsonObject profile, JsonObject entry)
    {
        var ledger = EnsureArray(profile, "ledger");
        ledger.Add(entry);
    }

    private static void AppendProgressionLedger(JsonObject profile, JsonObject entry)
    {
        var ledger = profile[ProgressionLedgerProperty] as JsonArray;
        if (ledger == null)
        {
            ledger = new JsonArray();
            profile[ProgressionLedgerProperty] = ledger;
        }

        ledger.Add(entry);
    }

    private static string BuildProgressionLedgerEntryId(JsonObject profile, string cycleKey, string source)
    {
        var identity = BuildIdentityKey(profile) ?? "unknown";
        var safeIdentity = new string(identity.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        var safeCycle = new string(cycleKey.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return $"entity_progression_{source}_{safeIdentity}_{safeCycle}";
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject ?? new JsonObject();

    private readonly record struct ProgressionCycle(string CycleKey, int CyclesProcessed, bool IsShining);

    private readonly record struct CurrencyDelta(int InkFeathers, int LightSparks);
}
