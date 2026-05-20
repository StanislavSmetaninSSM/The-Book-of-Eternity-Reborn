using System.Text.Json.Nodes;

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
        "shining_faction_head",
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

        UpsertProfiles(result, previousRoot?[ProfilesProperty]);
        UpsertProfiles(result, currentRoot?[ProfilesProperty]);
        UpsertProfileCommands(result, currentRoot?[ResponseProfilesProperty], "profile_response_not_object", "profile_response_not_array");
        UpsertProfileCommands(result, currentRoot?[UpdateProperty], "profile_update_not_object", "profile_update_not_array");
        ApplyCustomStateChanges(result, currentRoot?[CustomStateChangesProperty]);
        ApplySpecialArtLearningReceipts(result, currentRoot?[SpecialArtLearningReceiptsProperty]);
        ApplyProgressionOverrides(result, currentRoot?[ProgressionOverridesProperty]);
        ApplyAutomaticProgression(result, progressionReportRoot);

        result.Remove(UpdateProperty);
        result.Remove(ResponseProfilesProperty);
        result.Remove(CustomStateChangesProperty);
        result.Remove(ProgressionOverridesProperty);
        result.Remove(SpecialArtLearningReceiptsProperty);
        return result;
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
            profiles[index] = replacement;
            return;
        }

        profiles.Add(CloneObject(profile));
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

    private static string? BuildCustomStateIdentity(JsonObject state) =>
        GetNodeString(state["stateId"]) ??
        GetNodeString(state["stateKey"]) ??
        GetNodeString(state["key"]) ??
        GetNodeString(state["name"]) ??
        GetNodeString(state["title"]) ??
        GetNodeString(state["stateName"]);

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

    private static void ApplyAutomaticProgression(JsonObject result, JsonObject? progressionReportRoot)
    {
        if (progressionReportRoot == null)
            return;

        if (result[ProfilesProperty] is not JsonArray profiles)
            return;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            var cycle = ResolveProgressionCycleForProfile(profile, progressionReportRoot);
            if (cycle != null)
                ApplyAutomaticProgression(profile, cycle.Value);
        }
    }

    private static void ApplyAutomaticProgression(JsonObject profile, ProgressionCycle cycle)
    {
        var strategy = profile["progressionStrategy"] as JsonObject;
        if (strategy == null)
            return;

        if (strategy["autoProgressionEnabled"] is JsonValue enabledValue &&
            enabledValue.TryGetValue<bool>(out var enabled) &&
            !enabled)
        {
            return;
        }

        if (string.Equals(GetNodeString(strategy["lastAutoProgressionCycleKey"]), cycle.CycleKey, StringComparison.OrdinalIgnoreCase))
            return;

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
