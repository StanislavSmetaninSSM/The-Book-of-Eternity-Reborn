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
        UpsertProfiles(result, currentRoot?[ResponseProfilesProperty]);
        UpsertProfiles(result, currentRoot?[UpdateProperty]);
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

            profiles[index] = CloneObject(profile);
            return;
        }

        profiles.Add(CloneObject(profile));
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

    private static void UpsertProfiles(JsonObject result, JsonNode? profilesNode)
    {
        if (profilesNode is not JsonArray profiles)
            return;

        var resultProfiles = EnsureProfilesArray(result);
        foreach (var profile in profiles.OfType<JsonObject>())
            UpsertProfile(resultProfiles, profile);
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
        if (changesNode is not JsonArray changes)
            return;

        var profiles = EnsureProfilesArray(result);
        foreach (var change in changes.OfType<JsonObject>())
        {
            var targetKey = BuildIdentityKey(change);
            if (string.IsNullOrWhiteSpace(targetKey))
                continue;

            var profile = profiles
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(BuildIdentityKey(item), targetKey, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                continue;

            if (change["statesToRemove"] is JsonArray removals)
                RemoveCustomStates(profile, removals);

            if (change["statesToAddOrUpdate"] is JsonArray upserts)
                UpsertCustomStates(profile, upserts);
        }
    }

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
        if (overridesNode is not JsonArray overrides)
            return;

        var profiles = EnsureProfilesArray(result);
        foreach (var overrideNode in overrides.OfType<JsonObject>())
        {
            var targetKey = BuildIdentityKey(overrideNode);
            if (string.IsNullOrWhiteSpace(targetKey))
                continue;

            var profile = profiles
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(BuildIdentityKey(item), targetKey, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                continue;

            var cycleKey = GetNodeString(overrideNode["cycleKey"]) ?? "manual";
            ApplyCurrencyDeltas(profile, overrideNode["currencyDeltas"] as JsonObject);
            ApplyStandardArtTierDeltas(profile, overrideNode["standardArtTierDeltas"] as JsonObject);
            ApplyProgressionExperienceDeltas(profile, overrideNode["progressionExperienceDeltas"] as JsonObject);

            var strategy = EnsureObject(profile, "progressionStrategy");
            strategy["lastAutoProgressionCycleKey"] = cycleKey;
            AppendProgressionLedger(profile, new JsonObject
            {
                ["entryId"] = BuildProgressionLedgerEntryId(profile, cycleKey, "gm_override"),
                ["cycleKey"] = cycleKey,
                ["source"] = "gm_override",
                ["summary"] = GetNodeString(overrideNode["summary"]) ??
                              GetNodeString(overrideNode["reason"]) ??
                              "GM override применил прокачку сущности посмертия.",
                ["income"] = new JsonObject
                {
                    ["inkFeathers"] = 0,
                    ["lightSparks"] = 0
                },
                ["spending"] = CloneObject(overrideNode["currencyDeltas"] as JsonObject ?? new JsonObject())
            });
        }
    }

    private static void ApplySpecialArtLearningReceipts(JsonObject result, JsonNode? receiptsNode)
    {
        if (receiptsNode is not JsonArray receipts)
            return;

        var profiles = EnsureProfilesArray(result);
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            var artId = GetNodeString(receipt["artId"]);
            var teacherActorType = GetNodeString(receipt["teacherActorType"]);
            var teacherActorId = GetNodeString(receipt["teacherActorId"]) ?? GetNodeString(receipt["teacherActorRef"]);
            var playerActorId = GetNodeString(receipt["playerActorId"]);
            if (string.IsNullOrWhiteSpace(artId) ||
                string.IsNullOrWhiteSpace(teacherActorType) ||
                string.IsNullOrWhiteSpace(teacherActorId) ||
                string.IsNullOrWhiteSpace(playerActorId))
            {
                continue;
            }

            var teacherProfile = FindProfileByIdentity(profiles, teacherActorType, teacherActorId);
            var playerProfile = FindProfileByIdentity(profiles, "player_soul", playerActorId);
            var sourceArt = FindSpecialArtById(teacherProfile, artId);
            if (playerProfile == null || sourceArt == null)
                continue;

            var learnedArt = CloneObject(sourceArt);
            learnedArt["ownerActorType"] = "player_soul";
            learnedArt["ownerActorId"] = playerActorId;
            learnedArt["tier"] = GetNodeInt(receipt["initialTier"]);
            learnedArt["canTeachPlayer"] = false;
            learnedArt["learnedFromActorType"] = teacherActorType;
            learnedArt["learnedFromActorId"] = teacherActorId;
            learnedArt["learnedAtTurn"] = GetNodeInt(receipt["learnedAtTurn"]);
            learnedArt["learningReceiptId"] = GetNodeString(receipt["receiptId"]);

            var playerArts = EnsureArray(playerProfile, "specialArts");
            UpsertSpecialArt(playerArts, learnedArt);

            AppendLedger(playerProfile, new JsonObject
            {
                ["entryId"] = GetNodeString(receipt["receiptId"]) ?? $"special_art_learning_{artId}",
                ["turnNumber"] = GetNodeInt(receipt["learnedAtTurn"]),
                ["reason"] = "learn_special_art",
                ["summary"] = GetNodeString(receipt["summary"]) ?? $"Игрок изучил особое духовное искусство {artId}."
            });
        }
    }

    private static void ApplyAutomaticProgression(JsonObject result, JsonObject? progressionReportRoot)
    {
        var cycle = ResolveProgressionCycle(progressionReportRoot);
        if (cycle == null)
            return;

        if (result[ProfilesProperty] is not JsonArray profiles)
            return;

        foreach (var profile in profiles.OfType<JsonObject>())
            ApplyAutomaticProgression(profile, cycle.Value);
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
        var income = ResolveIncome(profile, cycle);
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

        foreach (var priorityNode in priorities)
        {
            var priority = GetNodeString(priorityNode);
            if (string.IsNullOrWhiteSpace(priority))
                continue;

            if (StandardArtIds.Contains(priority) &&
                TryUpgradeStandardArt(profile, currencies, priority, ref spending, upgrades))
            {
                return;
            }

            if (string.Equals(priority, "enlightenment", StringComparison.OrdinalIgnoreCase) &&
                TryUpgradeProgressionTrack(profile, currencies, "enlightenment", useLightSparks: false, ref spending, upgrades))
            {
                return;
            }

            if (cycle.IsShining &&
                string.Equals(priority, "radiance", StringComparison.OrdinalIgnoreCase) &&
                TryUpgradeProgressionTrack(profile, currencies, "radiance", useLightSparks: true, ref spending, upgrades))
            {
                return;
            }
        }
    }

    private static bool TryUpgradeStandardArt(
        JsonObject profile,
        JsonObject currencies,
        string artId,
        ref CurrencyDelta spending,
        JsonArray upgrades)
    {
        var arts = EnsureObject(profile, "standardArts");
        var currentTier = Math.Clamp(GetNodeInt(arts[artId]), 0, MaxProfileTier);
        if (currentTier >= MaxProfileTier)
            return false;

        var cost = 10 * (currentTier + 1);
        if (GetNodeInt(currencies["inkFeathers"]) < cost)
            return false;

        AddCurrency(currencies, "inkFeathers", -cost);
        arts[artId] = currentTier + 1;
        spending = spending with { InkFeathers = spending.InkFeathers + cost };
        upgrades.Add($"{artId}:{currentTier}->{currentTier + 1}");
        return true;
    }

    private static bool TryUpgradeProgressionTrack(
        JsonObject profile,
        JsonObject currencies,
        string trackName,
        bool useLightSparks,
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
        if (GetNodeInt(currencies[currencyName]) < cost)
            return false;

        AddCurrency(currencies, currencyName, -cost);
        var nextExperience = Math.Max(0, GetNodeInt(track["experience"])) + 20;
        track["experience"] = nextExperience;
        track["tier"] = Math.Min(MaxProfileTier, Math.Max(tier, nextExperience / 20));

        spending = useLightSparks
            ? spending with { LightSparks = spending.LightSparks + cost }
            : spending with { InkFeathers = spending.InkFeathers + cost };
        upgrades.Add($"{trackName}:experience+20");
        return true;
    }

    private static ProgressionCycle? ResolveProgressionCycle(JsonObject? root)
    {
        if (root == null)
            return null;

        var report = root["progressionProcessingReport"] as JsonObject ?? root;
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

    private static CurrencyDelta ResolveIncome(JsonObject profile, ProgressionCycle cycle)
    {
        var multiplier = Math.Max(1, cycle.CyclesProcessed);
        return cycle.IsShining || IsShiningRealm(profile)
            ? new CurrencyDelta(6 * multiplier, 1 * multiplier)
            : new CurrencyDelta(12 * multiplier, 0);
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

    private static void ApplyStandardArtTierDeltas(JsonObject profile, JsonObject? deltas)
    {
        if (deltas == null)
            return;

        var arts = EnsureObject(profile, "standardArts");
        foreach (var delta in deltas)
        {
            if (!StandardArtIds.Contains(delta.Key))
                continue;

            arts[delta.Key] = Math.Clamp(GetNodeInt(arts[delta.Key]) + GetNodeInt(delta.Value), 0, MaxProfileTier);
        }
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
            var nextExperience = Math.Max(0, GetNodeInt(track["experience"]) + delta);
            track["experience"] = nextExperience;
            track["tier"] = Math.Min(MaxProfileTier, Math.Max(GetNodeInt(track["tier"]), nextExperience / 20));
        }
    }

    private static void AddCurrency(JsonObject currencies, string propertyName, int delta)
    {
        currencies[propertyName] = Math.Max(0, GetNodeInt(currencies[propertyName]) + delta);
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
