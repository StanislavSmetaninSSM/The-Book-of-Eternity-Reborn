using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public static class AfterlifeSpiritualConflictState
{
    public const string StatePath = "game_state/meta/afterlife_spiritual_conflict_state.json";
    public const string ResponseField = "afterlifeSpiritualConflictUpdate";
    public const string SoulStateProfileProperty = "afterlifeCombatProfile";

    public const string ModeStart = "start";
    public const string ModeExchange = "exchange";
    public const string ModeResolve = "resolve";
    public const string ModeRepairCancel = "repair_cancel";

    public static readonly HashSet<string> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ModeStart,
        ModeExchange,
        ModeResolve,
        ModeRepairCancel
    };

    public static readonly HashSet<string> SideModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "direct_duel",
        "assisted_duel",
        "champion_duel"
    };

    public static readonly HashSet<string> StrainStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "clear",
        "strained",
        "fractured",
        "overwhelmed",
        "broken"
    };

    public static readonly HashSet<string> ConflictPositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "opposition_dominant",
        "opposition_advantaged",
        "contested",
        "player_advantaged",
        "player_dominant"
    };

    public static readonly HashSet<string> ResolutionStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "concession_pending",
        "surrender_pending",
        "retreat_pending",
        "ready_to_resolve",
        "resolved",
        "repair_cancelled"
    };

    public static readonly HashSet<string> OperationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "break_binding",
        "force_binding",
        "force_incarnation",
        "withdraw",
        "surrender",
        "negotiate"
    };

    public static readonly HashSet<string> OperationOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "success",
        "partial_success",
        "blocked",
        "countered",
        "setback",
        "no_effect"
    };

    public static readonly IReadOnlyList<RankDefinition> EnlightenmentRanks =
    [
        new(0, "dormant", 0, 0, "Baseline afterlife conflict participation."),
        new(1, "stirring", 8, 1, "Unlocks tier-1 spiritual art upgrades."),
        new(2, "focused", 18, 1, "Improves strain recovery after ordinary Chaos Sea conflicts."),
        new(3, "tempered", 32, 2, "Unlocks tier-2 spiritual art upgrades."),
        new(4, "lucid", 46, 2, "Improves resistance against ordinary Guardian pressure."),
        new(5, "illuminated", AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience, 3, "Unlocks tier-3 spiritual art upgrades and ascension-ready conflict scale.")
    ];

    public static readonly IReadOnlyList<RankDefinition> RadianceRanks =
    [
        new(0, "unlit", 0, 0, "No persistent Radiant combat advantage."),
        new(1, "spark", 20, 1, "Radiance begins to count as retained combat authority after Shining return."),
        new(2, "gleam", 45, 1, "Unlocks tier-1 Radiant art upgrades."),
        new(3, "ray", 75, 2, "Unlocks tier-2 Radiant art upgrades."),
        new(4, "halo", 110, 2, "Improves side support when a Shining ally is the lead contestant."),
        new(5, "suncrest", 160, 3, "Unlocks tier-3 Radiant art upgrades."),
        new(6, "aurora", 220, 3, "Retained Radiance strongly influences Chaos Sea conflicts after return."),
        new(7, "dawn_throne", 300, 4, "Unlocks tier-4 Radiant art upgrades."),
        new(8, "stellar_mantle", 420, 4, "High-rank Abode actors recognize the soul as a major spiritual combatant."),
        new(9, "radiant_sovereign", 560, 5, "Unlocks tier-5 Radiant art upgrades and top-end afterlife conflict authority.")
    ];

    public static readonly IReadOnlyList<SpiritualArtDefinition> SpiritualArts =
    [
        new("pressure", "Pressure", "Improve direct strain pressure on the opposing lead contestant.", 1),
        new("counter", "Counter", "Improve countering and reversal of a declared incoming operation.", 1),
        new("guard", "Guard", "Improve prevention of incoming strain/consequence against your own side.", 1),
        new("maneuver", "Maneuver", "Improve positional shifts without requiring raw overpowering.", 1),
        new("break_binding", "Break Binding", "Improve resisting or breaking spiritual bindings and forced handoffs.", 2),
        new("binding", "Binding", "Improve imposing a bounded spiritual bind after winning leverage.", 2),
        new("incarnation_resistance", "Incarnation Resistance", "Improve resistance to guardian_forced incarnation attempts.", 2),
        new("champion_coordination", "Champion Coordination", "Improve side-vs-side support when an ally is the lead contestant.", 3)
    ];

    public sealed record RankDefinition(int Rank, string RankId, int RequiredProgress, int UnlocksArtTier, string MechanicalEffect);

    public sealed record SpiritualArtDefinition(string ArtId, string DisplayName, string MechanicalUse, int MinUnlockTier);

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = null,
            ["recentConflicts"] = new JsonArray()
        };

    public static JsonObject CreateDefaultCombatProfile() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["enlightenmentRank"] = 0,
            ["radianceRank"] = 0,
            ["retainedRadianceRank"] = 0,
            ["artTiers"] = new JsonObject(),
            ["capstones"] = new JsonObject(),
            ["lastRecoveryTurn"] = 0
        };

    public static JsonObject NormalizeRoot(JsonObject? root)
    {
        var normalized = root?.DeepClone() as JsonObject ?? new JsonObject();
        if (normalized["schemaVersion"] is not JsonValue schemaVersion ||
            !schemaVersion.TryGetValue<int>(out var schema) ||
            schema <= 0)
        {
            normalized["schemaVersion"] = 1;
        }

        if (!normalized.ContainsKey("activeConflict"))
            normalized["activeConflict"] = null;

        if (normalized["recentConflicts"] is not JsonArray)
            normalized["recentConflicts"] = new JsonArray();

        return normalized;
    }

    public static JsonObject ApplyUpdate(JsonObject? existingRoot, JsonObject update)
    {
        var root = NormalizeRoot(existingRoot);
        var mode = GetNodeString(update["mode"]);
        if (string.IsNullOrWhiteSpace(mode) || !Modes.Contains(mode))
            return MarkInvalidUpdate(root, update, "missing_or_invalid_mode");

        switch (mode.ToLowerInvariant())
        {
            case ModeStart:
                return ApplyStart(root, update);
            case ModeExchange:
                return ApplyExchange(root, update);
            case ModeResolve:
                return ApplyResolve(root, update, repairCancel: false);
            case ModeRepairCancel:
                return ApplyResolve(root, update, repairCancel: true);
            default:
                return MarkInvalidUpdate(root, update, "missing_or_invalid_mode");
        }
    }

    public static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return null;
    }

    public static int GetNodeInt(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return defaultValue;
    }

    public static string? NormalizeAfterlifeRealmKey(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
            return null;

        var trimmed = realm.Trim();
        if (string.Equals(trimmed, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Море Хаоса", StringComparison.OrdinalIgnoreCase))
        {
            return "chaos_sea";
        }

        if (string.Equals(trimmed, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "shining_abode";
        }

        return null;
    }

    public static bool IsAfterlifeRealm(string? realm) =>
        NormalizeAfterlifeRealmKey(realm) != null;

    private static JsonObject ApplyStart(JsonObject root, JsonObject update)
    {
        if (root.TryGetPropertyValue("activeConflict", out var activeConflict) && activeConflict != null)
            return MarkInvalidUpdate(root, update, "start_while_conflict_active");

        var conflict = CloneObject(update["conflictState"] as JsonObject) ??
                       CloneObject(update["activeConflict"] as JsonObject) ??
                       CloneObject(update["conflictSeed"] as JsonObject);
        if (conflict == null)
            return MarkInvalidUpdate(root, update, "missing_conflict_state");

        if (string.IsNullOrWhiteSpace(GetNodeString(conflict["status"])))
            conflict["status"] = "active";
        if (string.IsNullOrWhiteSpace(GetNodeString(conflict["resolutionState"])))
            conflict["resolutionState"] = "active";
        var realm = GetNodeString(conflict["realm"]) ?? GetNodeString(update["realm"]);
        if (string.IsNullOrWhiteSpace(realm))
            return MarkInvalidUpdate(root, update, "start_missing_realm");
        if (!IsAfterlifeRealm(realm))
            return MarkInvalidUpdate(root, update, "start_invalid_realm");
        conflict["realm"] = realm;
        if (conflict["exchangeLog"] is not JsonArray)
            conflict["exchangeLog"] = new JsonArray();

        root["activeConflict"] = conflict;
        ClearInvalidUpdateMarkers(root);
        return root;
    }

    private static JsonObject ApplyExchange(JsonObject root, JsonObject update)
    {
        if (root["activeConflict"] is not JsonObject active)
            return MarkInvalidUpdate(root, update, "exchange_without_active_conflict");

        var exchange = CloneObject(update["exchange"] as JsonObject);
        if (exchange == null)
            return MarkInvalidUpdate(root, update, "exchange_missing_exchange_object");

        var activeConflictId = GetNodeString(active["conflictId"]);
        var exchangeConflictId = GetExchangeConflictIdentity(exchange);
        if (!string.IsNullOrWhiteSpace(exchangeConflictId) &&
            (string.IsNullOrWhiteSpace(activeConflictId) ||
             !string.Equals(exchangeConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase)))
        {
            return MarkInvalidUpdate(root, update, "exchange_conflict_id_mismatch");
        }

        var log = active["exchangeLog"]?.DeepClone() as JsonArray ?? new JsonArray();
        var isNoEffectExchange = string.Equals(GetNodeString(exchange["outcome"]), "no_effect", StringComparison.OrdinalIgnoreCase);

        var replacement = CloneObject(update["activeConflictAfter"] as JsonObject) ??
                          CloneObject(update["conflictStateAfter"] as JsonObject);
        if (replacement != null)
        {
            if (isNoEffectExchange)
                return MarkInvalidUpdate(root, update, "exchange_no_effect_state_replacement");

            var replacementConflictId = GetNodeString(replacement["conflictId"]);
            if (string.IsNullOrWhiteSpace(activeConflictId) ||
                (!string.IsNullOrWhiteSpace(replacementConflictId) &&
                 !string.Equals(replacementConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase)))
            {
                return MarkInvalidUpdate(root, update, "exchange_conflict_id_mismatch");
            }

            if (string.IsNullOrWhiteSpace(replacementConflictId))
                replacement["conflictId"] = active["conflictId"]?.DeepClone();

            log.Add(exchange.DeepClone());
            replacement["exchangeLog"] = MergeExchangeLogs(log, replacement["exchangeLog"] as JsonArray);
            root["activeConflict"] = replacement;
            ClearInvalidUpdateMarkers(root);
            return root;
        }

        log.Add(exchange.DeepClone());
        active["exchangeLog"] = log;
        if (isNoEffectExchange)
        {
            ClearInvalidUpdateMarkers(root);
            return root;
        }

        if (exchange["after"] is JsonObject exchangeAfter)
            CopyConflictStateFields(exchangeAfter, active);

        CopyIfPresent(update, active, "conflictPosition");
        CopyIfPresent(update, active, "playerSideStrain");
        CopyIfPresent(update, active, "oppositionSideStrain");
        CopyIfPresent(update, active, "resolutionState");
        CopyIfPresent(update, active, "status");
        ClearInvalidUpdateMarkers(root);
        return root;
    }

    private static string? GetExchangeConflictIdentity(JsonObject exchange)
    {
        var conflictId = GetNodeString(exchange["conflictId"]);
        return !string.IsNullOrWhiteSpace(conflictId)
            ? conflictId
            : GetNodeString(exchange["id"]);
    }

    private static JsonObject ApplyResolve(JsonObject root, JsonObject update, bool repairCancel)
    {
        var active = root["activeConflict"] as JsonObject;
        if (active == null)
        {
            if (repairCancel)
            {
                root["activeConflict"] = null;
                ClearInvalidUpdateMarkers(root);
                return root;
            }

            return MarkInvalidUpdate(root, update, "resolve_without_active_conflict");
        }

        var resolution = CloneObject(update["resolution"] as JsonObject);
        if (!repairCancel)
        {
            if (resolution == null)
                return MarkInvalidUpdate(root, update, "resolve_missing_resolution");

            var operationType = GetNodeString(resolution["operationType"]);
            if (!string.IsNullOrWhiteSpace(operationType) &&
                OperationTypes.Contains(operationType) &&
                RequiresGuardianResolveEvidence(active, operationType) &&
                HasAnyGuardianResolveReference(resolution) &&
                !ResolutionReferencesGuardianOpponent(resolution, active))
            {
                return MarkInvalidUpdate(root, update, "resolve_guardian_id_mismatch");
            }

            if (!HasCompleteResolveResolution(resolution, active))
                return MarkInvalidUpdate(root, update, "resolve_incomplete_resolution");
        }

        resolution ??= new JsonObject();
        var activeConflictId = GetNodeString(active["conflictId"]);
        var resolutionConflictId = GetNodeString(resolution["conflictId"]);
        if (!string.IsNullOrWhiteSpace(resolutionConflictId) &&
            !string.Equals(resolutionConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase))
        {
            return MarkInvalidUpdate(root, update, "resolve_conflict_id_mismatch");
        }

        if (string.IsNullOrWhiteSpace(resolutionConflictId))
            resolution["conflictId"] = active["conflictId"]?.DeepClone();
        resolution["realm"] ??= active["realm"]?.DeepClone();
        resolution["sideModel"] ??= active["sideModel"]?.DeepClone();

        resolution["resolutionState"] = repairCancel ? "repair_cancelled" : "resolved";
        resolution["resolvedAtUtc"] ??= DateTime.UtcNow.ToString("o");
        resolution["mode"] = repairCancel ? ModeRepairCancel : ModeResolve;

        var recent = root["recentConflicts"] as JsonArray ?? new JsonArray();
        recent.Add(resolution);
        while (recent.Count > 20)
            recent.RemoveAt(0);

        root["recentConflicts"] = recent;
        root["activeConflict"] = null;
        ClearInvalidUpdateMarkers(root);
        return root;
    }

    private static bool HasCompleteResolveResolution(JsonObject resolution, JsonObject activeConflict)
    {
        var operationType = GetNodeString(resolution["operationType"]);
        if (GetNodeInt(resolution["resolvedAtTurn"]) <= 0 ||
            string.IsNullOrWhiteSpace(operationType) ||
            !OperationTypes.Contains(operationType) ||
            !HasResolveOutcomeEvidence(resolution))
        {
            return false;
        }

        if (RequiresGuardianResolveEvidence(activeConflict, operationType))
            return ResolutionReferencesGuardianOpponent(resolution, activeConflict);

        return ResolutionReferencesOppositionLead(resolution, activeConflict);
    }

    private static bool HasResolveOutcomeEvidence(JsonObject resolution)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(resolution["playerOutcome"])))
            return true;

        return IsSupportedLossResolutionKind(GetNodeString(resolution["resolutionKind"]));
    }

    private static bool IsSupportedLossResolutionKind(string? resolutionKind) =>
        string.Equals(resolutionKind, "player_loss", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resolutionKind, "player_surrender", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resolutionKind, "player_concession", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresGuardianResolveEvidence(JsonObject activeConflict, string operationType)
    {
        if (string.Equals(operationType, "force_incarnation", StringComparison.OrdinalIgnoreCase))
            return true;

        var oppositionLead = GetOppositionLead(activeConflict);
        return string.Equals(GetNodeString(oppositionLead?["actorType"]), "guardian", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResolutionReferencesOppositionLead(JsonObject resolution, JsonObject activeConflict)
    {
        var oppositionLead = GetOppositionLead(activeConflict);
        var oppositionActorId = GetNodeString(oppositionLead?["actorId"]);
        if (string.IsNullOrWhiteSpace(oppositionActorId))
            return false;

        return string.Equals(GetNodeString(resolution["resolvedActorId"]), oppositionActorId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["oppositionActorId"]), oppositionActorId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["actorId"]), oppositionActorId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResolutionReferencesGuardianOpponent(JsonObject resolution, JsonObject activeConflict)
    {
        var oppositionLead = GetOppositionLead(activeConflict);
        if (!string.Equals(GetNodeString(oppositionLead?["actorType"]), "guardian", StringComparison.OrdinalIgnoreCase))
            return false;

        var guardianId = GetNodeString(oppositionLead?["actorId"]) ??
                         GetNodeString(oppositionLead?["guardianId"]) ??
                         GetNodeString(oppositionLead?["id"]);
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        return string.Equals(GetNodeString(resolution["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["forcedByGuardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["oppositionGuardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["oppositionLeadActorId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["resolvedActorId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["oppositionActorId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GetNodeString(resolution["actorId"]), guardianId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyGuardianResolveReference(JsonObject resolution)
    {
        return !string.IsNullOrWhiteSpace(GetNodeString(resolution["guardianId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["forcedByGuardianId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["oppositionGuardianId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["oppositionLeadActorId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["resolvedActorId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["oppositionActorId"])) ||
               !string.IsNullOrWhiteSpace(GetNodeString(resolution["actorId"]));
    }

    private static JsonObject? GetOppositionLead(JsonObject activeConflict)
    {
        if (activeConflict["oppositionSide"] is not JsonObject oppositionSide)
            return null;

        return oppositionSide["leadContestant"] as JsonObject;
    }

    private static JsonObject MarkInvalidUpdate(JsonObject root, JsonObject update, string reason)
    {
        root["lastInvalidUpdate"] = update.DeepClone();
        root["lastInvalidUpdateReason"] = reason;
        root["lastInvalidUpdateAtUtc"] = DateTime.UtcNow.ToString("o");
        return root;
    }

    private static void ClearInvalidUpdateMarkers(JsonObject root)
    {
        root.Remove("lastInvalidUpdate");
        root.Remove("lastInvalidUpdateReason");
        root.Remove("lastInvalidUpdateAtUtc");
    }

    private static JsonObject? CloneObject(JsonObject? node) => node?.DeepClone() as JsonObject;

    private static void CopyConflictStateFields(JsonObject source, JsonObject target)
    {
        CopyIfPresent(source, target, "conflictPosition");
        CopyIfPresent(source, target, "playerSideStrain");
        CopyIfPresent(source, target, "oppositionSideStrain");
        CopyIfPresent(source, target, "resolutionState");
        CopyIfPresent(source, target, "status");
    }

    private static JsonArray MergeExchangeLogs(JsonArray canonicalLog, JsonArray? replacementLog)
    {
        var merged = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddExchangeLogItems(canonicalLog, merged, seen);
        AddExchangeLogItems(replacementLog, merged, seen);

        return merged;
    }

    private static void AddExchangeLogItems(JsonArray? source, JsonArray target, HashSet<string> seen)
    {
        if (source == null)
            return;

        foreach (var item in source)
        {
            if (item == null)
                continue;

            var identity = GetExchangeLogItemIdentity(item);
            if (seen.Add(identity))
                target.Add(item.DeepClone());
        }
    }

    private static string GetExchangeLogItemIdentity(JsonNode item)
    {
        if (item is JsonObject obj)
        {
            var exchangeId = GetNodeString(obj["exchangeId"]);
            if (!string.IsNullOrWhiteSpace(exchangeId))
                return $"id:{exchangeId}";
        }

        return $"json:{item.ToJsonString()}";
    }

    private static void CopyIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var node) && node != null)
            target[propertyName] = node.DeepClone();
    }

    public static JsonNode? CloneJsonElement(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined)
            return null;
        return JsonNode.Parse(element.GetRawText());
    }
}
