using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class SarefMainStoryState
{
    public const string StatePath = "game_state/meta/main_story_saref_state.json";
    public const string PendingWingsInfiltrationPath = "game_state/control/pending_saref_wings_infiltration.json";
    public const string ResponseField = "sarefMainStoryUpdate";
    public const string StateResponseField = "sarefMainStoryState";
    public const int SchemaVersion = 1;

    public const string RevealStageUnknown = "unknown";
    public const string RevealStageShadow = "shadow";
    public const string RevealStageNameRevealed = "name_revealed";
    public const string RevealStageWingsRevealed = "wings_revealed";
    public const string RevealStageInfiltrationActive = "infiltration_active";
    public const string RevealStageConfrontationAvailable = "confrontation_available";
    public const string RevealStageCompleted = "completed";

    public const string CategoryIdentity = "identity";
    public const string CategoryMethod = "method";
    public const string CategoryFaction = "faction";
    public const string CategoryPath = "path";

    public const string QuestStateLatent = "latent";
    public const string QuestStateRecognized = "recognized";
    public const string QuestStateActive = "active";
    public const string QuestStateReadyToTurnIn = "ready_to_turn_in";
    public const string QuestStateCompleted = "completed";

    public const string AdvantageStateAvailable = "available";
    public const string AdvantageStateSpent = "spent";
    public const string AdvantageStatePassive = "passive";
    public const string AdvantageStateDisabled = "disabled";
    public const string AdvantageStateSuppressed = "suppressed";

    public const string SceneAny = "any";
    public const string SceneWingsInfiltration = "wings_infiltration";
    public const string SceneSarefNegotiation = "saref_negotiation";
    public const string SceneSarefConfrontation = "saref_confrontation";
    public const string SceneOathBreak = "oath_break";
    public const string SceneMemoryAttack = "memory_attack";
    public const string SceneFactionConflict = "faction_conflict";
    public const string SceneEscapeOrExile = "escape_or_exile";
    public const string SceneFinalResolution = "final_resolution";

    public const string DefeatUpdateModeRecord = "record_defeat_outcome";
    public const string DefeatOutcomeForcedOath = "forced_oath";
    public const string DefeatOutcomeExileToChaosSea = "exile_to_chaos_sea";
    public const string DefeatOutcomeMemorySuppression = "memory_suppression";
    public const string DefeatOutcomeSoulDissipation = "soul_dissipation";
    public const string DefeatOutcomePyrrhicEscape = "pyrrhic_escape";

    public const string FinalUpdateModeRecord = "record_final_confrontation";
    public const string FinalStatusResolved = "resolved";
    public const string FinalRouteCombat = "combat";
    public const string FinalRoutePolitical = "political";
    public const string FinalRouteOathLaw = "oath_law";
    public const string FinalRouteMetaphysical = "metaphysical";
    public const string FinalRouteHybrid = "hybrid";
    public const string FinalVictoryPyrrhic = "pyrrhic";
    public const string FinalVictoryClean = "clean";
    public const string FinalVictoryDeep = "deep";
    public const string FinalSarefOutcomeDefeated = "defeated";
    public const string FinalWingsOutcomeBroken = "broken";
    public const string FinalWingsOutcomeDissolved = "dissolved";

    public const string WingsUpdateModeReveal = "reveal_wings";
    public const string WingsUpdateModeRefuse = "refuse_wings";
    public const string WingsUpdateModeBlock = "block_wings";

    public const string WingsRouteSafetySafe = "safe";
    public const string WingsRouteSafetyRisky = "risky";
    public const string WingsRouteSafetyDesperate = "desperate";

    public const string WingsStatusRevealed = "revealed";
    public const string WingsStatusRefused = "refused";
    public const string WingsStatusBlocked = "blocked";

    public const string WingsFactionRole = "wings_of_angels";
    public const string FactionVisibilityHidden = "hidden";
    public const string FactionVisibilityRumored = "rumored";
    public const string FactionVisibilityRevealed = "revealed";
    public const string SupporterArchetypeDeceived = "deceived";
    public const string SupporterArchetypeOathbound = "oathbound";
    public const string SupporterArchetypeFanatic = "fanatic";
    public const string SupporterArchetypeOpportunist = "opportunist";
    public const string WingsTraceStageShadow = "shadow";
    public const string WingsTraceStageName = "name";
    public const string WingsTraceStageFaction = "faction";

    public static readonly HashSet<string> RevealStages = new(StringComparer.OrdinalIgnoreCase)
    {
        RevealStageUnknown,
        RevealStageShadow,
        RevealStageNameRevealed,
        RevealStageWingsRevealed,
        RevealStageInfiltrationActive,
        RevealStageConfrontationAvailable,
        RevealStageCompleted
    };

    public static readonly HashSet<string> RevelationCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        CategoryIdentity,
        CategoryMethod,
        CategoryFaction,
        CategoryPath,
        "oath_break",
        "war_doctrine",
        "structural_weakness",
        "exile_survival",
        "false_light_cut"
    };

    public static readonly HashSet<string> QuestProgressStates = new(StringComparer.OrdinalIgnoreCase)
    {
        QuestStateLatent,
        QuestStateRecognized,
        QuestStateActive,
        QuestStateReadyToTurnIn,
        QuestStateCompleted
    };

    public static readonly HashSet<string> LatentTraceStates = new(StringComparer.OrdinalIgnoreCase)
    {
        QuestStateLatent,
        QuestStateRecognized
    };

    public static readonly HashSet<string> MandatoryWingsCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        CategoryIdentity,
        CategoryMethod,
        CategoryFaction,
        CategoryPath
    };

    public static readonly HashSet<string> AdvantageStates = new(StringComparer.OrdinalIgnoreCase)
    {
        AdvantageStateAvailable,
        AdvantageStateSpent,
        AdvantageStatePassive,
        AdvantageStateDisabled,
        AdvantageStateSuppressed
    };

    public static readonly HashSet<string> AdvantageSceneTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        SceneAny,
        SceneWingsInfiltration,
        SceneSarefNegotiation,
        SceneSarefConfrontation,
        SceneOathBreak,
        SceneMemoryAttack,
        SceneFactionConflict,
        SceneEscapeOrExile,
        SceneFinalResolution
    };

    public static readonly HashSet<string> FactionVisibilityStates = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionVisibilityHidden,
        FactionVisibilityRumored,
        FactionVisibilityRevealed
    };

    public static readonly HashSet<string> WingsSupporterArchetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        SupporterArchetypeDeceived,
        SupporterArchetypeOathbound,
        SupporterArchetypeFanatic,
        SupporterArchetypeOpportunist
    };

    public static readonly HashSet<string> WingsAgentInteractionRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "persuade",
        "free",
        "expose",
        "blackmail",
        "defeat"
    };

    public static readonly HashSet<string> WingsTraceStages = new(StringComparer.OrdinalIgnoreCase)
    {
        WingsTraceStageShadow,
        WingsTraceStageName,
        WingsTraceStageFaction
    };

    public static readonly HashSet<string> PersonalBondStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "intrigued",
        "favored",
        "intimate_oath",
        "rejected",
        "hostile",
        "adversarial_romantic"
    };

    public static readonly HashSet<string> PlayerOathStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "offered",
        "oathbound",
        "strained",
        "broken",
        "escaped"
    };

    public static readonly HashSet<string> WingsUpdateModes = new(StringComparer.OrdinalIgnoreCase)
    {
        WingsUpdateModeReveal,
        WingsUpdateModeRefuse,
        WingsUpdateModeBlock
    };

    public static readonly HashSet<string> DefeatOutcomeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        DefeatOutcomeForcedOath,
        DefeatOutcomeExileToChaosSea,
        DefeatOutcomeMemorySuppression,
        DefeatOutcomeSoulDissipation,
        DefeatOutcomePyrrhicEscape
    };

    public static readonly HashSet<string> FinalConfrontationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        FinalStatusResolved
    };

    public static readonly HashSet<string> FinalConfrontationRouteTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        FinalRouteCombat,
        FinalRoutePolitical,
        FinalRouteOathLaw,
        FinalRouteMetaphysical,
        FinalRouteHybrid
    };

    public static readonly HashSet<string> FinalVictoryTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        FinalVictoryPyrrhic,
        FinalVictoryClean,
        FinalVictoryDeep
    };

    public static readonly HashSet<string> FinalSarefOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        FinalSarefOutcomeDefeated,
        "destroyed",
        "banished",
        "redeemed",
        "oathbound_bargain"
    };

    public static readonly HashSet<string> FinalWingsFactionOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        FinalWingsOutcomeBroken,
        FinalWingsOutcomeDissolved,
        "leaderless",
        "reformed",
        "absorbed"
    };

    public static readonly HashSet<string> WingsRouteSafetyStates = new(StringComparer.OrdinalIgnoreCase)
    {
        WingsRouteSafetySafe,
        WingsRouteSafetyRisky,
        WingsRouteSafetyDesperate
    };

    public static readonly HashSet<string> WingsClosureStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        WingsStatusRevealed,
        WingsStatusRefused,
        WingsStatusBlocked
    };

    public sealed record SarefWingsInfiltrationReadState(
        JsonObject? Request,
        bool Exists,
        bool IsMalformed,
        string? Error,
        string? RawPayload);

    private sealed record SarefRouteFragment(
        string RevelationId,
        string Category,
        string? Summary);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["revealStage"] = RevealStageUnknown,
            ["guardianQuestlines"] = new JsonArray(),
            ["latentTraces"] = new JsonArray(),
            ["sarefRevelations"] = new JsonArray(),
            ["sarefAdvantages"] = new JsonArray(),
            ["sarefAdvantageUses"] = new JsonArray(),
            ["wingsInfiltration"] = null,
            ["factionLinks"] = new JsonObject
            {
                ["wingsFactionId"] = null,
                ["visibility"] = FactionVisibilityHidden,
                ["shadowTraces"] = new JsonArray(),
                ["knownAgents"] = new JsonArray()
            },
            ["finalConfrontation"] = null,
            ["defeatOutcomes"] = new JsonArray(),
            ["endings"] = new JsonArray(),
            ["playerOathState"] = null,
            ["sarefPersonalBond"] = null
        };

    public static string SerializeDefaultRoot() =>
        CreateDefaultRoot().ToJsonString(JsonOptions);

    public static async Task<SarefWingsInfiltrationReadState> ReadWingsInfiltrationRequestStateAsync(FileSystemManager fs)
    {
        var raw = await fs.ReadFileAsync(PendingWingsInfiltrationPath);
        return ReadWingsInfiltrationRequestState(raw, exists: raw != null);
    }

    public static SarefWingsInfiltrationReadState ReadWingsInfiltrationRequestState(string? raw, bool exists)
    {
        if (!exists && raw == null)
            return new SarefWingsInfiltrationReadState(null, false, false, null, null);

        if (string.IsNullOrWhiteSpace(raw))
            return new SarefWingsInfiltrationReadState(null, exists, exists, exists ? "empty/whitespace file" : null, raw);

        try
        {
            if (JsonNode.Parse(raw) is not JsonObject request)
                return new SarefWingsInfiltrationReadState(null, true, true, "root is not object", raw);

            var error = ValidateWingsInfiltrationRequestShape(request);
            return error == null
                ? new SarefWingsInfiltrationReadState(request, true, false, null, raw)
                : new SarefWingsInfiltrationReadState(null, true, true, error, raw);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new SarefWingsInfiltrationReadState(null, true, true, ex.GetType().Name, raw);
        }
    }

    public static async Task WriteWingsInfiltrationRequestAsync(FileSystemManager fs, JsonObject request) =>
        await fs.WriteFileAtomicAsync(PendingWingsInfiltrationPath, request.ToJsonString(JsonOptions));

    public static void ClearWingsInfiltrationRequest(FileSystemManager fs) =>
        fs.DeleteFile(PendingWingsInfiltrationPath);

    public static async Task EnsureWingsInfiltrationHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsShiningRealm(currentRealm) || !fs.FileExists(PendingWingsInfiltrationPath))
            return;

        var read = await ReadWingsInfiltrationRequestStateAsync(fs);
        if (read.IsMalformed || read.Request == null)
            return;

        var storyRoot = await ReadJsonObjectAsync(fs, StatePath);
        if (HasMatchingWingsInfiltrationClosure(storyRoot, read.Request))
            ClearWingsInfiltrationRequest(fs);
    }

    public static async Task<string?> BuildWingsSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var read = await ReadWingsInfiltrationRequestStateAsync(fs);
        if (!read.Exists)
            return null;

        if (read.IsMalformed || read.Request == null)
        {
            return "SAREF WINGS INFILTRATION CORRUPTION:\n" +
                   $"  - {PendingWingsInfiltrationPath} unreadable or malformed.\n" +
                   "  - Preserve and repair the client-authored Wings search request before resolving it.";
        }

        if (!RealmSemantics.IsShiningRealm(currentRealm))
        {
            return "SAREF WINGS INFILTRATION WRONG REALM:\n" +
                   $"  - {PendingWingsInfiltrationPath} is Shining Abode-only. Preserve it; do not reveal or refuse Wings from Chaos Sea/Mortal turns.";
        }

        var routeSafety = GetNodeString(read.Request["routeSafety"]) ?? "?";
        var entryMode = GetNodeString(read.Request["entryMode"]) ?? "?";
        var requestId = GetNodeString(read.Request["requestId"]) ?? "?";
        return "SAREF WINGS INFILTRATION:\n" +
               $"  - Pending request: {requestId}; routeSafety={routeSafety}; entryMode={entryMode}.\n" +
               $"  - Resolve through {ResponseField}.mode={WingsUpdateModeReveal}, {WingsUpdateModeRefuse}, or {WingsUpdateModeBlock}.\n" +
               "  - reveal_wings must set main_story_saref_state.revealStage=wings_revealed, wingsInfiltration.status=revealed, and factionLinks.visibility=revealed.\n" +
               "  - Risky/desperate routes require explicit GM disadvantages from the pending request.";
    }

    public static JsonObject? BuildWingsInfiltrationRequest(JsonObject? storyRoot, int createdAtTurn)
    {
        if (!TryBuildWingsUnlockRoute(storyRoot, out var routeSafety, out var routeFragments, out var substituteFragments, out var disadvantages))
            return null;

        var requestId = $"saref_wings_infiltration:{Math.Max(1, createdAtTurn)}";
        return new JsonObject
        {
            ["requestId"] = requestId,
            ["createdAtTurn"] = Math.Max(1, createdAtTurn),
            ["createdAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["routeSafety"] = routeSafety,
            ["entryMode"] = routeSafety switch
            {
                WingsRouteSafetySafe => "safe_infiltration",
                WingsRouteSafetyRisky => "risky_infiltration",
                _ => "desperate_infiltration"
            },
            ["routeFragments"] = ToFragmentArray(routeFragments),
            ["substituteFragments"] = ToFragmentArray(substituteFragments),
            ["availableAdvantages"] = BuildAvailableWingsAdvantageArray(storyRoot),
            ["disadvantages"] = ToStringArray(disadvantages),
            ["expectedResponseSurface"] = ResponseField,
            ["expectedClosure"] = new JsonObject
            {
                ["mode"] = WingsUpdateModeReveal,
                ["requestId"] = requestId,
                ["supportedRefusalModes"] = new JsonArray(WingsUpdateModeRefuse, WingsUpdateModeBlock),
                ["requiredRevealStage"] = RevealStageWingsRevealed,
                ["requiredSceneType"] = SceneWingsInfiltration
            }
        };
    }

    public static bool TryBuildWingsUnlockRoute(
        JsonObject? storyRoot,
        out string routeSafety,
        out IReadOnlyList<JsonObject> routeFragments,
        out IReadOnlyList<JsonObject> substituteFragments,
        out IReadOnlyList<string> disadvantages)
    {
        routeSafety = string.Empty;
        routeFragments = Array.Empty<JsonObject>();
        substituteFragments = Array.Empty<JsonObject>();
        disadvantages = Array.Empty<string>();

        var fragments = EnumerateRouteFragments(storyRoot).ToList();
        var byCategory = fragments
            .GroupBy(fragment => fragment.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var mandatory = MandatoryWingsCategories
            .Where(byCategory.ContainsKey)
            .Select(category => byCategory[category])
            .ToList();
        var additional = fragments
            .Where(fragment => !MandatoryWingsCategories.Contains(fragment.Category))
            .ToList();

        if (mandatory.Count == 4)
        {
            routeSafety = WingsRouteSafetySafe;
            routeFragments = mandatory.Select(ToFragmentObject).ToList();
            return true;
        }

        if (mandatory.Count >= 3 && additional.Count >= 2)
        {
            routeSafety = WingsRouteSafetyRisky;
            routeFragments = mandatory.Select(ToFragmentObject).ToList();
            substituteFragments = additional.Take(2).Select(ToFragmentObject).ToList();
            disadvantages = new[]
            {
                "Неполный маршрут: ГМ обязан добавить подозрение, проверку лояльности или ложный контакт при входе в Крылья Ангелов."
            };
            return true;
        }

        if (mandatory.Count >= 2 && additional.Count >= 4)
        {
            routeSafety = WingsRouteSafetyDesperate;
            routeFragments = mandatory.Select(ToFragmentObject).ToList();
            substituteFragments = additional.Take(4).Select(ToFragmentObject).ToList();
            disadvantages = new[]
            {
                "Отчаянный маршрут: ГМ обязан добавить серьёзную угрозу раскрытия, ловушку или цену входа.",
                "Контакт с Крыльями Ангелов начинается с неблагоприятной позиции игрока."
            };
            return true;
        }

        return false;
    }

    public static JsonObject ApplyUpdate(JsonObject? previousRoot, JsonObject updateRoot)
    {
        var root = previousRoot?.DeepClone()?.AsObject() ?? CreateDefaultRoot();
        root.Remove(ResponseField);

        var mode = GetNodeString(updateRoot["mode"]);
        if (string.IsNullOrWhiteSpace(mode))
            return root;

        if (string.Equals(mode, DefeatUpdateModeRecord, StringComparison.OrdinalIgnoreCase))
        {
            ApplyDefeatOutcomeUpdate(root, updateRoot);
            return root;
        }

        if (string.Equals(mode, FinalUpdateModeRecord, StringComparison.OrdinalIgnoreCase))
        {
            ApplyFinalConfrontationUpdate(root, updateRoot);
            return root;
        }

        if (!WingsUpdateModes.Contains(mode))
            return root;

        var requestId = GetNodeString(updateRoot["requestId"]) ??
                        GetNodeString(updateRoot["wingsInfiltration"]?["requestId"]);
        var resolvedAtTurn = GetNodeInt(updateRoot["resolvedAtTurn"]);
        if (resolvedAtTurn <= 0)
            resolvedAtTurn = GetNodeInt(updateRoot["turnNumber"]);

        var closure = updateRoot["wingsInfiltration"] is JsonObject updateClosure
            ? updateClosure.DeepClone().AsObject()
            : new JsonObject();
        if (!string.IsNullOrWhiteSpace(requestId))
            closure["requestId"] = requestId;
        if (resolvedAtTurn > 0)
            closure["resolvedAtTurn"] = resolvedAtTurn;
        closure["mode"] = mode;
        closure["sceneType"] = SceneWingsInfiltration;

        if (string.Equals(mode, WingsUpdateModeReveal, StringComparison.OrdinalIgnoreCase))
        {
            root["revealStage"] = RevealStageWingsRevealed;
            closure["status"] = WingsStatusRevealed;
            EnsureFactionLinks(root)["visibility"] = FactionVisibilityRevealed;
        }
        else
        {
            closure["status"] = string.Equals(mode, WingsUpdateModeRefuse, StringComparison.OrdinalIgnoreCase)
                ? WingsStatusRefused
                : WingsStatusBlocked;
        }

        foreach (var key in new[] { "routeSafety", "entryMode", "summary", "gmResolution", "reason" })
        {
            if (updateRoot[key] != null)
                closure[key] = updateRoot[key]!.DeepClone();
        }

        if (updateRoot["factionLinks"] is JsonObject factionLinks)
        {
            var target = EnsureFactionLinks(root);
            foreach (var prop in factionLinks)
                target[prop.Key] = prop.Value?.DeepClone();
        }

        root["wingsInfiltration"] = closure;
        return root;
    }

    private static void ApplyDefeatOutcomeUpdate(JsonObject root, JsonObject updateRoot)
    {
        var outcome = updateRoot["defeatOutcome"] is JsonObject defeatOutcome
            ? defeatOutcome.DeepClone().AsObject()
            : updateRoot["defeatOutcomeAudit"] is JsonObject defeatOutcomeAudit
                ? defeatOutcomeAudit.DeepClone().AsObject()
                : new JsonObject();

        foreach (var key in new[]
                 {
                     "outcomeId", "outcomeType", "sceneType", "conflictId", "soulDissipationProofId",
                     "oathId", "summary", "gmMotivation", "reason", "escapeCost"
                 })
        {
            if (outcome[key] == null && updateRoot[key] != null)
                outcome[key] = updateRoot[key]!.DeepClone();
        }

        var resolvedAtTurn = GetNodeInt(outcome["resolvedAtTurn"]);
        if (resolvedAtTurn <= 0)
            resolvedAtTurn = GetNodeInt(updateRoot["resolvedAtTurn"]);
        if (resolvedAtTurn <= 0)
            resolvedAtTurn = GetNodeInt(updateRoot["turnNumber"]);
        if (resolvedAtTurn > 0)
            outcome["resolvedAtTurn"] = resolvedAtTurn;

        if (outcome["sceneType"] == null)
            outcome["sceneType"] = SceneSarefConfrontation;

        if (updateRoot["mitigation"] is JsonObject mitigation && outcome["mitigation"] == null)
            outcome["mitigation"] = mitigation.DeepClone();
        if (updateRoot["memorySuppressionAudit"] is JsonObject memorySuppressionAudit && outcome["memorySuppressionAudit"] == null)
            outcome["memorySuppressionAudit"] = memorySuppressionAudit.DeepClone();
        if (updateRoot["exileAudit"] is JsonObject exileAudit && outcome["exileAudit"] == null)
            outcome["exileAudit"] = exileAudit.DeepClone();

        var outcomes = EnsureArray(root, "defeatOutcomes");
        var outcomeId = GetNodeString(outcome["outcomeId"]);
        if (!string.IsNullOrWhiteSpace(outcomeId))
        {
            for (var i = 0; i < outcomes.Count; i++)
            {
                if (outcomes[i] is JsonObject existing &&
                    string.Equals(GetNodeString(existing["outcomeId"]), outcomeId, StringComparison.OrdinalIgnoreCase))
                {
                    outcomes[i] = outcome;
                    ApplyDefeatOutcomeSideEffects(root, updateRoot);
                    return;
                }
            }
        }

        outcomes.Add(outcome);
        ApplyDefeatOutcomeSideEffects(root, updateRoot);
    }

    private static void ApplyDefeatOutcomeSideEffects(JsonObject root, JsonObject updateRoot)
    {
        if (updateRoot["playerOathState"] is JsonObject playerOathState)
            root["playerOathState"] = playerOathState.DeepClone();
        if (updateRoot["sarefPersonalBond"] is JsonObject personalBond)
            root["sarefPersonalBond"] = personalBond.DeepClone();
        if (updateRoot["finalConfrontation"] is JsonObject finalConfrontation)
            root["finalConfrontation"] = finalConfrontation.DeepClone();

        if (updateRoot["sarefAdvantageUses"] is JsonArray advantageUses)
            MergeArrayById(EnsureArray(root, "sarefAdvantageUses"), advantageUses, "usageId");
    }

    private static void ApplyFinalConfrontationUpdate(JsonObject root, JsonObject updateRoot)
    {
        var final = updateRoot["finalConfrontation"] is JsonObject finalConfrontation
            ? finalConfrontation.DeepClone().AsObject()
            : updateRoot["finalConfrontationAudit"] is JsonObject finalConfrontationAudit
                ? finalConfrontationAudit.DeepClone().AsObject()
                : new JsonObject();

        foreach (var key in new[]
                 {
                     "confrontationId", "status", "routeType", "victoryTier", "directScene", "sceneType",
                     "conflictId", "factionCampaignId", "oathBreakProofId", "metaphysicalProofId",
                     "sarefOutcome", "wingsFactionOutcome", "summary", "gmResolution", "reason"
                 })
        {
            if (final[key] == null && updateRoot[key] != null)
                final[key] = updateRoot[key]!.DeepClone();
        }

        if (final["routeComponents"] == null && updateRoot["routeComponents"] is JsonArray routeComponents)
            final["routeComponents"] = routeComponents.DeepClone();
        if (final["advantageUseIds"] == null && updateRoot["advantageUseIds"] is JsonArray advantageUseIds)
            final["advantageUseIds"] = advantageUseIds.DeepClone();

        var resolvedAtTurn = GetNodeInt(final["resolvedAtTurn"]);
        if (resolvedAtTurn <= 0)
            resolvedAtTurn = GetNodeInt(updateRoot["resolvedAtTurn"]);
        if (resolvedAtTurn <= 0)
            resolvedAtTurn = GetNodeInt(updateRoot["turnNumber"]);
        if (resolvedAtTurn > 0)
            final["resolvedAtTurn"] = resolvedAtTurn;

        if (final["status"] == null)
            final["status"] = FinalStatusResolved;
        if (final["sceneType"] == null)
            final["sceneType"] = SceneFinalResolution;

        root["finalConfrontation"] = final;
        if (string.Equals(GetNodeString(final["status"]), FinalStatusResolved, StringComparison.OrdinalIgnoreCase))
            root["revealStage"] = RevealStageCompleted;

        if (updateRoot["sarefAdvantageUses"] is JsonArray advantageUses)
            MergeArrayById(EnsureArray(root, "sarefAdvantageUses"), advantageUses, "usageId");

        if (updateRoot["ending"] is JsonObject ending)
            MergeEnding(root, ending, final, resolvedAtTurn);
        else if (updateRoot["endingAudit"] is JsonObject endingAudit)
            MergeEnding(root, endingAudit, final, resolvedAtTurn);
        else if (updateRoot["endings"] is JsonArray endings)
            MergeArrayById(EnsureArray(root, "endings"), endings, "endingId");
    }

    private static void MergeEnding(JsonObject root, JsonObject endingSource, JsonObject final, int resolvedAtTurn)
    {
        var ending = endingSource.DeepClone().AsObject();
        if (resolvedAtTurn > 0 && ending["resolvedAtTurn"] == null)
            ending["resolvedAtTurn"] = resolvedAtTurn;
        if (ending["victoryTier"] == null && final["victoryTier"] != null)
            ending["victoryTier"] = final["victoryTier"]!.DeepClone();
        if (ending["sarefOutcome"] == null && final["sarefOutcome"] != null)
            ending["sarefOutcome"] = final["sarefOutcome"]!.DeepClone();

        var endings = EnsureArray(root, "endings");
        var endingId = GetNodeString(ending["endingId"]);
        if (!string.IsNullOrWhiteSpace(endingId))
        {
            for (var i = 0; i < endings.Count; i++)
            {
                if (endings[i] is JsonObject existing &&
                    string.Equals(GetNodeString(existing["endingId"]), endingId, StringComparison.OrdinalIgnoreCase))
                {
                    endings[i] = ending;
                    return;
                }
            }
        }

        endings.Add(ending);
    }

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static void MergeArrayById(JsonArray target, JsonArray source, string idProperty)
    {
        foreach (var item in source.OfType<JsonObject>())
        {
            var itemId = GetNodeString(item[idProperty]);
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                var replaced = false;
                for (var i = 0; i < target.Count; i++)
                {
                    if (target[i] is JsonObject existing &&
                        string.Equals(GetNodeString(existing[idProperty]), itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        target[i] = item.DeepClone();
                        replaced = true;
                        break;
                    }
                }

                if (replaced)
                    continue;
            }

            target.Add(item.DeepClone());
        }
    }

    public static bool HasMatchingWingsInfiltrationClosure(JsonObject? storyRoot, JsonObject request)
    {
        if (storyRoot?["wingsInfiltration"] is not JsonObject closure)
            return false;

        var requestId = GetNodeString(request["requestId"]);
        var closureRequestId = GetNodeString(closure["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId) ||
            !string.Equals(requestId, closureRequestId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = GetNodeString(closure["status"]);
        if (string.IsNullOrWhiteSpace(status) || !WingsClosureStatuses.Contains(status))
            return false;

        if (GetNodeInt(closure["resolvedAtTurn"]) <= 0)
            return false;

        if (!string.Equals(status, WingsStatusRevealed, StringComparison.OrdinalIgnoreCase))
            return true;

        var stage = GetNodeString(storyRoot["revealStage"]);
        var factionVisibility = GetNodeString(storyRoot["factionLinks"]?["visibility"]);
        return StageRank(stage) >= StageRank(RevealStageWingsRevealed) &&
               string.Equals(factionVisibility, FactionVisibilityRevealed, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<JsonObject> GetPlayerVisibleShiningFactions(JsonObject? shiningRoot)
    {
        if (shiningRoot?["factions"] is not JsonArray factions)
            yield break;

        foreach (var faction in factions.OfType<JsonObject>())
        {
            if (!IsHiddenWingsFaction(faction))
                yield return faction;
        }
    }

    public static bool IsHiddenWingsFaction(JsonObject? faction)
    {
        if (faction == null)
            return false;

        var role = GetNodeString(faction["sarefFactionRole"]);
        var visibility = GetNodeString(faction["sarefVisibility"]);
        return string.Equals(role, WingsFactionRole, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(visibility, FactionVisibilityRevealed, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ValidateWingsInfiltrationRequestShape(JsonObject? request)
    {
        if (request == null)
            return "root is not object";

        var requestId = GetNodeString(request["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId) ||
            !requestId.StartsWith("saref_wings_infiltration:", StringComparison.OrdinalIgnoreCase))
        {
            return "missing or invalid requestId";
        }

        if (GetNodeInt(request["createdAtTurn"]) <= 0)
            return "createdAtTurn must be positive";
        if (string.IsNullOrWhiteSpace(GetNodeString(request["createdAtUtc"])))
            return "createdAtUtc must be non-empty";

        var routeSafety = GetNodeString(request["routeSafety"]);
        if (string.IsNullOrWhiteSpace(routeSafety) || !WingsRouteSafetyStates.Contains(routeSafety))
            return "routeSafety invalid";
        if (string.IsNullOrWhiteSpace(GetNodeString(request["entryMode"])))
            return "entryMode missing";
        if (!string.Equals(GetNodeString(request["expectedResponseSurface"]), ResponseField, StringComparison.OrdinalIgnoreCase))
            return "expectedResponseSurface mismatch";
        if (!string.Equals(GetNodeString(request["expectedClosure"]?["mode"]), WingsUpdateModeReveal, StringComparison.OrdinalIgnoreCase))
            return "expectedClosure.mode mismatch";

        if (request["routeFragments"] is not JsonArray routeFragments || routeFragments.Count == 0)
            return "routeFragments missing";
        if (request["substituteFragments"] is not JsonArray)
            return "substituteFragments missing";
        if (request["availableAdvantages"] is not JsonArray)
            return "availableAdvantages missing";
        if (request["disadvantages"] is not JsonArray disadvantages)
            return "disadvantages missing";
        if (!string.Equals(routeSafety, WingsRouteSafetySafe, StringComparison.OrdinalIgnoreCase) &&
            disadvantages.Count == 0)
        {
            return "risky/desperate route requires disadvantages";
        }

        return null;
    }

    public static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var str))
            return str.Trim();

        return null;
    }

    public static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
                return (int)longValue;
        }

        return 0;
    }

    public static int StageRank(string? revealStage)
    {
        if (string.Equals(revealStage, RevealStageShadow, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(revealStage, RevealStageNameRevealed, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(revealStage, RevealStageWingsRevealed, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(revealStage, RevealStageInfiltrationActive, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (string.Equals(revealStage, RevealStageConfrontationAvailable, StringComparison.OrdinalIgnoreCase))
            return 5;
        if (string.Equals(revealStage, RevealStageCompleted, StringComparison.OrdinalIgnoreCase))
            return 6;

        return 0;
    }

    private static IEnumerable<SarefRouteFragment> EnumerateRouteFragments(JsonObject? storyRoot)
    {
        if (storyRoot?["sarefRevelations"] is not JsonArray revelations)
            yield break;

        var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in revelations.OfType<JsonObject>())
        {
            var category = GetNodeString(item["category"]);
            var revelationId = GetNodeString(item["revelationId"]);
            if (string.IsNullOrWhiteSpace(category) ||
                !RevelationCategories.Contains(category) ||
                string.IsNullOrWhiteSpace(revelationId) ||
                !seenCategories.Add(category))
            {
                continue;
            }

            yield return new SarefRouteFragment(
                revelationId,
                category,
                GetNodeString(item["summary"]));
        }
    }

    private static JsonObject ToFragmentObject(SarefRouteFragment fragment) =>
        new()
        {
            ["revelationId"] = fragment.RevelationId,
            ["category"] = fragment.Category,
            ["summary"] = fragment.Summary
        };

    private static JsonArray ToFragmentArray(IEnumerable<JsonObject> fragments)
    {
        var array = new JsonArray();
        foreach (var fragment in fragments)
            array.Add(fragment.DeepClone());
        return array;
    }

    private static JsonArray ToStringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
            array.Add(value);
        return array;
    }

    private static JsonArray BuildAvailableWingsAdvantageArray(JsonObject? storyRoot)
    {
        var array = new JsonArray();
        if (storyRoot?["sarefAdvantages"] is not JsonArray advantages)
            return array;

        foreach (var advantage in advantages.OfType<JsonObject>())
        {
            var state = GetNodeString(advantage["state"]);
            if (!string.Equals(state, AdvantageStateAvailable, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state, AdvantageStatePassive, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var scenes = advantage["applicableScenes"] as JsonArray;
            if (scenes == null ||
                !scenes.Any(scene =>
                {
                    var value = GetNodeString(scene);
                    return string.Equals(value, SceneAny, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, SceneWingsInfiltration, StringComparison.OrdinalIgnoreCase);
                }))
            {
                continue;
            }

            array.Add(new JsonObject
            {
                ["advantageId"] = GetNodeString(advantage["advantageId"]),
                ["displayName"] = GetNodeString(advantage["displayName"]) ?? GetNodeString(advantage["name"]),
                ["state"] = state,
                ["summary"] = GetNodeString(advantage["summary"]),
                ["applicableScenes"] = scenes.DeepClone()
            });
        }

        return array;
    }

    private static JsonObject EnsureFactionLinks(JsonObject root)
    {
        if (root["factionLinks"] is JsonObject factionLinks)
            return factionLinks;

        factionLinks = new JsonObject
        {
            ["wingsFactionId"] = null,
            ["visibility"] = FactionVisibilityHidden,
            ["shadowTraces"] = new JsonArray(),
            ["knownAgents"] = new JsonArray()
        };
        root["factionLinks"] = factionLinks;
        return factionLinks;
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
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
}
