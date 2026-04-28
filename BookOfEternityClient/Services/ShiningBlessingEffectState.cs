using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ShiningBlessingEffectState
{
    // Final v1 runtime projection for Shining blessings:
    // memory uses bootstrap memory-echo selection,
    // social uses npc_relationships + faction_core relation commits,
    // route/lore/survival use world_events-driven consumers,
    // descent uses primed relic + manifested companion matching,
    // relic entitlements are consumed through Shining forge surfaces.
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public const string SoulStateProperty = "pendingShiningBlessingEffects";
    public const string GenericStatusConsumed = "consumed";
    public const string GenericStatusExpired = "expired";

    public const string ApplicationStateActive = "active";
    public const string ResourceStatusAppliedAtBootstrap = "applied_at_bootstrap";
    public const string MemoryStatusPendingPreTurnOneSelection = "pending_pre_turn_one_selection";
    public const string SocialStatusPendingFirstRelationCommit = "pending_first_relation_commit";
    public const string RouteStatusPendingEarlyRouteSeed = "pending_early_route_seed";
    public const string LoreStatusPendingLoreInsertion = "pending_lore_insertion";
    public const string SurvivalStatusPendingFirstRuinousFailure = "pending_first_ruinous_failure";
    public const string DescentStatusPendingResidentDescent = "pending_resident_descent";
    public const string RelicStatusPendingEntitlement = "pending_relic_entitlement";
    private const string BootstrapResourceGrantMarkersProperty = "_shiningBootstrapResourceGrantIds";

    public sealed record BootstrapMaterializationResult(
        bool Success,
        bool StateChanged,
        JsonObject? EffectsState,
        IReadOnlyList<string> SummaryLines,
        string? ErrorMessage = null);

    public sealed record RuntimeProcessingResult(
        bool Success,
        bool StateChanged,
        IReadOnlyList<string> SummaryLines,
        string? ErrorMessage = null);

    public sealed record MemoryEchoCandidate(
        int Incarnation,
        string LifeHint,
        string Summary);

    public sealed record PendingMemorySelectionState(
        int Options,
        int Rerolls,
        IReadOnlyList<MemoryEchoCandidate> Candidates);

    public static async Task<BootstrapMaterializationResult> MaterializeForBootstrapAsync(
        FileSystemManager fs,
        JsonObject preparedPackage,
        int currentIncarnation)
    {
        var packageIssue = ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(preparedPackage);
        if (!string.IsNullOrWhiteSpace(packageIssue))
        {
            return new BootstrapMaterializationResult(
                false,
                false,
                null,
                Array.Empty<string>(),
                packageIssue);
        }

        var selectedCards = preparedPackage["selectedCards"] as JsonArray;
        if (selectedCards == null || selectedCards.Count == 0)
        {
            return new BootstrapMaterializationResult(
                true,
                false,
                null,
                Array.Empty<string>());
        }

        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot == null)
        {
            return new BootstrapMaterializationResult(
                false,
                false,
                null,
                Array.Empty<string>(),
                "Не удалось прочитать soul_state.json для materialization сияющих благословений.");
        }

        var effectState = BuildPendingEffectsFromPreparedPackage(preparedPackage, currentIncarnation);
        var summaryLines = new List<string>();
        TryPrimeDescentEffects(soulRoot, effectState, 0, summaryLines);
        summaryLines.InsertRange(0, BuildActivationSummaryLines(effectState));

        soulRoot[SoulStateProperty] = effectState;
        var soulStateJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
        var writes = await BuildImmediateBootstrapEffectWritesAsync(fs, effectState);
        writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
            "game_state/meta/soul_state.json",
            soulStateJson,
            GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts),
            RequireCurrentBaseline: true));

        if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, writes.ToArray()))
        {
            return new BootstrapMaterializationResult(
                false,
                false,
                null,
                Array.Empty<string>(),
                "Не удалось атомарно materialize сияющие благословения: состояние изменилось во время bootstrap.");
        }

        return new BootstrapMaterializationResult(
            true,
            true,
            effectState,
            summaryLines);
    }

    public static async Task<RuntimeProcessingResult> ApplyAcceptedTurnRuntimeEffectsAsync(
        FileSystemManager fs,
        int currentTurnNumber,
        string? preTurnShiningJson,
        string? preTurnNpcCoreJson,
        string? preTurnWorldEventsJson,
        string? preTurnNpcRelationshipsJson = null,
        string? preTurnPlayerStatusJson = null,
        string? preTurnFactionCoreJson = null)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState)
            return new RuntimeProcessingResult(true, false, Array.Empty<string>());

        if (!RealmSemantics.IsMortalRealm(GetNodeString(soulRoot["currentRealm"])))
            return new RuntimeProcessingResult(true, false, Array.Empty<string>());

        var currentSoulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json") ?? soulRoot.ToJsonString(JsonOpts);
        var summaryLines = new List<string>();
        var soulChanged = false;
        var npcChanged = false;
        var npcRelationshipsChanged = false;
        var playerStatusChanged = false;
        var worldEventsChanged = false;
        var factionChanged = false;
        var currentShiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var currentWorldEventsJson = await fs.ReadFileAsync("game_state/world/world_events.json");
        var currentWorldEventsRoot = ParseJsonNode(currentWorldEventsJson);
        var currentPlayerStatusJson = await fs.ReadFileAsync("game_state/core/player_status.json");
        var currentPlayerStatusRoot = ParseJsonNode(currentPlayerStatusJson) as JsonObject;
        var currentFactionCoreJson = await fs.ReadFileAsync("game_state/factions/faction_core.json");
        var currentFactionCoreRoot = ParseJsonNode(currentFactionCoreJson) as JsonObject;

        if (ConsumeForgeEntitlementsFromAcceptedReceipts(
                soulRoot,
                effectState,
                currentShiningRoot,
                preTurnShiningJson,
                currentTurnNumber,
                summaryLines))
        {
            soulChanged = true;
        }

        var currentNpcJson = await fs.ReadFileAsync("game_state/npcs/npc_core.json");
        var currentNpcRoot = ParseJsonNode(currentNpcJson) as JsonObject;
        var currentNpcRelationshipsJson = await fs.ReadFileAsync("game_state/npcs/npc_relationships.json");
        var currentNpcRelationshipsRoot = ParseJsonNode(currentNpcRelationshipsJson) as JsonObject;
        var socialRelationCommitObserved = false;
        if (TryApplySocialEffectsFromRelationCommits(
                effectState,
                currentNpcRoot,
                currentNpcRelationshipsRoot,
                preTurnNpcRelationshipsJson,
                currentFactionCoreRoot,
                preTurnFactionCoreJson,
                currentTurnNumber,
                summaryLines,
                out var socialTouchedFaction,
                out socialRelationCommitObserved))
        {
            soulChanged = true;
            npcChanged = true;
            npcRelationshipsChanged = true;
            factionChanged |= socialTouchedFaction;
        }
        else if (!socialRelationCommitObserved &&
                 TryApplySocialEffectsFromNpcCoreDiff(effectState, currentNpcRoot, preTurnNpcCoreJson, currentTurnNumber, summaryLines))
        {
            soulChanged = true;
            npcChanged = true;
        }

        if (TryConsumeSurvivalEffectsFromWorldState(
                effectState,
                currentWorldEventsRoot,
                preTurnWorldEventsJson,
                currentPlayerStatusRoot,
                preTurnPlayerStatusJson,
                currentTurnNumber,
                summaryLines))
        {
            soulChanged = true;
            worldEventsChanged = true;
            playerStatusChanged = true;
        }

        if (TryConsumeRouteEffectsFromWorldEvents(
                effectState,
                currentWorldEventsRoot,
                preTurnWorldEventsJson,
                currentTurnNumber,
                summaryLines))
        {
            soulChanged = true;
        }

        if (TryConsumeLoreEffectsFromWorldEvents(
                effectState,
                currentWorldEventsRoot,
                preTurnWorldEventsJson,
                currentTurnNumber,
                summaryLines))
            soulChanged = true;

        if (TryPrimeDescentEffects(soulRoot, effectState, currentTurnNumber, summaryLines))
            soulChanged = true;

        if (TryConsumeDescentEffectsFromManifestation(effectState, currentNpcRoot, preTurnNpcCoreJson, currentTurnNumber, summaryLines))
            soulChanged = true;

        if (ExpireDeadlineEffects(effectState, currentTurnNumber, summaryLines))
            soulChanged = true;

        if (!soulChanged && !npcChanged && !playerStatusChanged && !worldEventsChanged && !factionChanged)
            return new RuntimeProcessingResult(true, false, Array.Empty<string>());

        NormalizeBlessingState(effectState);
        soulRoot[SoulStateProperty] = effectState;
        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                "game_state/meta/soul_state.json",
                currentSoulJson,
                GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts))
        };

        if (npcChanged && currentNpcRoot != null)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/npcs/npc_core.json",
                currentNpcJson,
                currentNpcRoot.ToJsonString(JsonOpts)));
        }

        if (npcRelationshipsChanged && currentNpcRelationshipsRoot != null)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/npcs/npc_relationships.json",
                currentNpcRelationshipsJson,
                currentNpcRelationshipsRoot.ToJsonString(JsonOpts)));
        }

        if (playerStatusChanged && currentPlayerStatusRoot != null)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/core/player_status.json",
                currentPlayerStatusJson,
                currentPlayerStatusRoot.ToJsonString(JsonOpts)));
        }

        if (worldEventsChanged && currentWorldEventsRoot is JsonNode worldEventsNode)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/world/world_events.json",
                currentWorldEventsJson,
                worldEventsNode.ToJsonString(JsonOpts)));
        }

        if (factionChanged && currentFactionCoreRoot != null)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/factions/faction_core.json",
                currentFactionCoreJson,
                currentFactionCoreRoot.ToJsonString(JsonOpts)));
        }

        if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, writes.ToArray()))
        {
            return new RuntimeProcessingResult(
                false,
                false,
                Array.Empty<string>(),
                "Не удалось безопасно зафиксировать runtime-эффекты сияющих благословений без partial multi-file update.");
        }

        return new RuntimeProcessingResult(true, true, summaryLines);
    }

    public static async Task<PendingMemorySelectionState?> ReadPendingMemorySelectionAsync(FileSystemManager fs)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["memorySelection"] is not JsonObject memorySelection ||
            !string.Equals(GetNodeString(memorySelection["status"]), MemoryStatusPendingPreTurnOneSelection, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new PendingMemorySelectionState(
            Math.Max(0, GetNodeInt(memorySelection["options"], 0)),
            Math.Max(0, GetNodeInt(memorySelection["rerolls"], 0)),
            BuildMemoryEchoCandidates(soulRoot));
    }

    public static async Task<bool> ConsumePendingMemorySelectionAsync(
        FileSystemManager fs,
        int currentTurnNumber,
        MemoryEchoCandidate? selectedCandidate,
        int rerollsSpent)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["memorySelection"] is not JsonObject memorySelection ||
            !string.Equals(GetNodeString(memorySelection["status"]), MemoryStatusPendingPreTurnOneSelection, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        MarkConsumed(memorySelection, currentTurnNumber);
        if (selectedCandidate != null)
        {
            memorySelection["selectedLifeIncarnation"] = selectedCandidate.Incarnation;
            memorySelection["selectedLifeHint"] = selectedCandidate.LifeHint;
            memorySelection["selectedLifeSummary"] = selectedCandidate.Summary;
        }

        memorySelection["rerollsSpent"] = Math.Max(0, rerollsSpent);
        soulRoot[SoulStateProperty] = effectState;
        await fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts));
        return true;
    }

    public static int GetPendingRelicRerolls(JsonObject? soulRoot)
    {
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["relicRefinementEntitlements"] is not JsonObject entitlements ||
            !IsPendingRelicEntitlement(entitlements))
        {
            return 0;
        }

        return Math.Max(0, GetNodeInt(entitlements["rerolls"], 0));
    }

    public static async Task<bool> ConsumeRelicRerollAsync(
        FileSystemManager fs,
        int currentTurnNumber)
        => await ConsumeRelicRerollsAsync(fs, currentTurnNumber, rerollsToConsume: 1);

    public static async Task<bool> ConsumeRelicRerollsAsync(
        FileSystemManager fs,
        int currentTurnNumber,
        int rerollsToConsume)
    {
        if (rerollsToConsume <= 0)
            return true;

        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["relicRefinementEntitlements"] is not JsonObject entitlements ||
            !IsPendingRelicEntitlement(entitlements))
        {
            return false;
        }

        var rerollsRemaining = Math.Max(0, GetNodeInt(entitlements["rerolls"], 0));
        if (rerollsRemaining < rerollsToConsume)
            return false;

        entitlements["rerolls"] = rerollsRemaining - rerollsToConsume;
        entitlements["rerollsSpent"] = GetNodeInt(entitlements["rerollsSpent"], 0) + rerollsToConsume;
        if (GetNodeInt(entitlements["rerolls"], 0) <= 0 &&
            !GetNodeBool(entitlements["freeShape"]) &&
            !GetNodeBool(entitlements["freeRetune"]))
        {
            MarkConsumed(entitlements, currentTurnNumber);
        }

        soulRoot[SoulStateProperty] = effectState;
        await fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts));
        return true;
    }

    public static ShiningAbodeState.ResourceCost AdjustForgeCostForBlessingEntitlements(
        JsonObject? soulRoot,
        string? actionType,
        ShiningAbodeState.ResourceCost baseCost)
    {
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["relicRefinementEntitlements"] is not JsonObject entitlements ||
            !IsPendingRelicEntitlement(entitlements))
        {
            return baseCost;
        }

        var normalizedActionType = (actionType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedActionType == ShiningCoreActionRequestState.ActionTypeForgeRelicReshape &&
            GetNodeBool(entitlements["freeShape"]))
        {
            return new ShiningAbodeState.ResourceCost(0, 0);
        }

        if (normalizedActionType == ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty &&
            GetNodeBool(entitlements["freeRetune"]))
        {
            return new ShiningAbodeState.ResourceCost(0, 0);
        }

        return baseCost;
    }

    public static bool ConsumeForgeEntitlements(
        JsonObject? soulRoot,
        string? actionType,
        int currentTurnNumber,
        string? consumedAtUtc = null)
    {
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState ||
            effectState["relicRefinementEntitlements"] is not JsonObject entitlements ||
            !IsPendingRelicEntitlement(entitlements))
        {
            return false;
        }

        var changed = false;
        var normalizedActionType = (actionType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedActionType == ShiningCoreActionRequestState.ActionTypeForgeRelicReshape &&
            GetNodeBool(entitlements["freeShape"]))
        {
            entitlements["freeShape"] = false;
            changed = true;
        }

        if (normalizedActionType == ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty &&
            GetNodeBool(entitlements["freeRetune"]))
        {
            entitlements["freeRetune"] = false;
            changed = true;
        }

        if (!changed)
            return false;

        if (GetNodeInt(entitlements["rerolls"], 0) <= 0 &&
            !GetNodeBool(entitlements["freeShape"]) &&
            !GetNodeBool(entitlements["freeRetune"]))
        {
            MarkConsumed(entitlements, currentTurnNumber, consumedAtUtc);
        }

        return true;
    }

    public static IReadOnlyList<string> BuildPendingWorldDirectiveLines(JsonObject preparedPackage)
    {
        var state = BuildPendingEffectsFromPreparedPackage(preparedPackage, currentIncarnation: 0);
        return BuildDirectiveLines(state);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(
        FileSystemManager fs,
        string? currentRealm,
        int currentTurnNumber)
    {
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return null;

        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState)
            return null;

        var lines = BuildReminderLines(effectState, currentTurnNumber);
        if (lines.Count == 0)
            return null;

        return "SHINING BLESSINGS:\n" + string.Join("\n", lines.Select(line => $"  - {line}"));
    }

    public static async Task<IReadOnlyList<string>> BuildStatusLinesAsync(FileSystemManager fs, int currentTurnNumber)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (soulRoot?[SoulStateProperty] is not JsonObject effectState)
            return Array.Empty<string>();

        var lines = BuildPlayerFacingStatusLines(effectState, currentTurnNumber);
        AppendStatusLifecycleSummary(lines, effectState, currentTurnNumber);
        return lines;
    }

    private static JsonObject BuildPendingEffectsFromPreparedPackage(JsonObject preparedPackage, int currentIncarnation)
    {
        var selectedCards = preparedPackage["selectedCards"] as JsonArray ?? new JsonArray();
        var selectedCardIds = preparedPackage["selectedCardIds"] as JsonArray
                              ?? new JsonArray(selectedCards
                                  .OfType<JsonObject>()
                                  .Select(card => (JsonNode?)(GetNodeString(card["cardId"]) ?? string.Empty))
                                  .ToArray());

        var materializedAtUtc = DateTime.UtcNow.ToString("o");
        var result = new JsonObject
        {
            ["applicationState"] = ApplicationStateActive,
            ["materializedAtUtc"] = materializedAtUtc,
            ["currentIncarnation"] = currentIncarnation,
            ["sourcePackagePreparedAtTurn"] = GetNodeInt(preparedPackage["preparedAtTurn"], 0),
            ["sourceCardIds"] = selectedCardIds.DeepClone(),
            ["sourceCardCount"] = selectedCards.Count
        };

        var memoryOptions = 0;
        var memoryRerolls = 0;
        var memorySourceCardIds = new JsonArray();

        var resourceMoney = 0;
        var resourceCommon = 0;
        var resourceUncommon = 0;
        var resourceSourceCardIds = new JsonArray();

        var relicRerolls = 0;
        var relicFreeShape = false;
        var relicFreeRetune = false;
        var relicSourceCardIds = new JsonArray();

        var socialEffects = new JsonArray();
        var routeEffects = new JsonArray();
        var loreEffects = new JsonArray();
        var survivalEffects = new JsonArray();
        var descentEffects = new JsonArray();

        foreach (var card in selectedCards.OfType<JsonObject>())
        {
            var sourceCardId = GetNodeString(card["cardId"]) ?? string.Empty;
            var displayName = GetNodeString(card["displayName"]) ?? sourceCardId;
            var displaySummary = GetNodeString(card["displaySummary"]) ?? string.Empty;
            var effectFamily = (GetNodeString(card["effectFamily"]) ?? string.Empty).Trim().ToLowerInvariant();
            var sourceFactionId = GetNodeString(card["sourceFactionId"]) ?? string.Empty;
            var sourceActorId = GetNodeString(card["sourceActorId"]) ?? string.Empty;
            var payload = card["effectPayload"] as JsonObject;
            if (payload == null)
                continue;

            switch (effectFamily)
            {
                case "memory":
                    memoryOptions += Math.Max(0, GetNodeInt(payload["options"], 0));
                    memoryRerolls += Math.Max(0, GetNodeInt(payload["rerolls"], 0));
                    AddUniqueString(memorySourceCardIds, sourceCardId);
                    break;

                case "resource":
                    resourceMoney += Math.Max(0, GetNodeInt(payload["money"], 0));
                    resourceCommon += Math.Max(0, GetNodeInt(payload["common"], 0));
                    resourceUncommon += Math.Max(0, GetNodeInt(payload["uncommon"], 0));
                    AddUniqueString(resourceSourceCardIds, sourceCardId);
                    break;

                case "relic":
                    relicRerolls += Math.Max(0, GetNodeInt(payload["rerolls"], 0));
                    relicFreeShape |= GetNodeBool(payload["freeShape"]);
                    relicFreeRetune |= GetNodeBool(payload["freeRetune"]);
                    AddUniqueString(relicSourceCardIds, sourceCardId);
                    break;

                case "social":
                    socialEffects.Add(new JsonObject
                    {
                        ["effectId"] = sourceCardId,
                        ["sourceCardId"] = sourceCardId,
                        ["sourceFactionId"] = sourceFactionId,
                        ["sourceActorId"] = sourceActorId,
                        ["displayName"] = displayName,
                        ["displaySummary"] = displaySummary,
                        ["delta"] = Math.Max(0, GetNodeInt(payload["delta"], 0)),
                        ["status"] = SocialStatusPendingFirstRelationCommit
                    });
                    break;

                case "route":
                    routeEffects.Add(new JsonObject
                    {
                        ["effectId"] = sourceCardId,
                        ["sourceCardId"] = sourceCardId,
                        ["sourceFactionId"] = sourceFactionId,
                        ["displayName"] = displayName,
                        ["displaySummary"] = displaySummary,
                        ["routeOptions"] = Math.Max(0, GetNodeInt(payload["routeOptions"], 0)),
                        ["latestTurn"] = Math.Max(0, GetNodeInt(payload["latestTurn"], 0)),
                        ["status"] = RouteStatusPendingEarlyRouteSeed
                    });
                    break;

                case "lore":
                    loreEffects.Add(new JsonObject
                    {
                        ["effectId"] = sourceCardId,
                        ["sourceCardId"] = sourceCardId,
                        ["sourceFactionId"] = sourceFactionId,
                        ["displayName"] = displayName,
                        ["displaySummary"] = displaySummary,
                        ["clueCount"] = Math.Max(0, GetNodeInt(payload["clueCount"], 0)),
                        ["latestTurn"] = Math.Max(0, GetNodeInt(payload["latestTurn"], 0)),
                        ["status"] = LoreStatusPendingLoreInsertion
                    });
                    break;

                case "survival":
                    survivalEffects.Add(new JsonObject
                    {
                        ["effectId"] = sourceCardId,
                        ["sourceCardId"] = sourceCardId,
                        ["sourceFactionId"] = sourceFactionId,
                        ["displayName"] = displayName,
                        ["displaySummary"] = displaySummary,
                        ["downgrade"] = Math.Max(0, GetNodeInt(payload["downgrade"], 0)),
                        ["recovery"] = Math.Max(0, GetNodeInt(payload["recovery"], 0)),
                        ["status"] = SurvivalStatusPendingFirstRuinousFailure
                    });
                    break;

                case "descent":
                    descentEffects.Add(new JsonObject
                    {
                        ["effectId"] = sourceCardId,
                        ["sourceCardId"] = sourceCardId,
                        ["sourceFactionId"] = sourceFactionId,
                        ["sourceActorId"] = sourceActorId,
                        ["displayName"] = displayName,
                        ["displaySummary"] = displaySummary,
                        ["latestTurn"] = Math.Max(0, GetNodeInt(payload["latestTurn"], 0)),
                        ["quality"] = Math.Max(0, GetNodeInt(payload["quality"], 0)),
                        ["status"] = DescentStatusPendingResidentDescent
                    });
                    break;
            }
        }

        if (memoryOptions > 0 || memoryRerolls > 0)
        {
            result["memorySelection"] = new JsonObject
            {
                ["options"] = memoryOptions,
                ["rerolls"] = memoryRerolls,
                ["status"] = MemoryStatusPendingPreTurnOneSelection,
                ["sourceCardIds"] = memorySourceCardIds
            };
        }

        if (resourceMoney > 0 || resourceCommon > 0 || resourceUncommon > 0)
        {
            result["resourceGrant"] = new JsonObject
            {
                ["money"] = resourceMoney,
                ["common"] = resourceCommon,
                ["uncommon"] = resourceUncommon,
                ["status"] = ResourceStatusAppliedAtBootstrap,
                ["appliedAtUtc"] = materializedAtUtc,
                ["sourceCardIds"] = resourceSourceCardIds
            };
        }

        if (relicRerolls > 0 || relicFreeShape || relicFreeRetune)
        {
            result["relicRefinementEntitlements"] = new JsonObject
            {
                ["rerolls"] = relicRerolls,
                ["freeShape"] = relicFreeShape,
                ["freeRetune"] = relicFreeRetune,
                ["status"] = RelicStatusPendingEntitlement,
                ["sourceCardIds"] = relicSourceCardIds
            };
        }

        if (socialEffects.Count > 0)
            result["pendingSocialEffects"] = socialEffects;
        if (routeEffects.Count > 0)
            result["pendingRouteEffects"] = routeEffects;
        if (loreEffects.Count > 0)
            result["pendingLoreEffects"] = loreEffects;
        if (survivalEffects.Count > 0)
            result["pendingSurvivalEffects"] = survivalEffects;
        if (descentEffects.Count > 0)
            result["pendingDescentEffects"] = descentEffects;

        return result;
    }

    private static async Task<List<CoordinatedStateWriteHelper.PlannedWrite>> BuildImmediateBootstrapEffectWritesAsync(
        FileSystemManager fs,
        JsonObject effectState)
    {
        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>();
        if (effectState["resourceGrant"] is not JsonObject resourceGrant)
            return writes;

        var money = Math.Max(0, GetNodeInt(resourceGrant["money"], 0));
        var common = Math.Max(0, GetNodeInt(resourceGrant["common"], 0));
        var uncommon = Math.Max(0, GetNodeInt(resourceGrant["uncommon"], 0));
        if (money <= 0 && common <= 0 && uncommon <= 0)
            return writes;

        var grantId = BuildBootstrapResourceGrantId(effectState, resourceGrant);
        resourceGrant["grantId"] = grantId;

        var statusJson = await fs.ReadFileAsync("game_state/core/player_status.json");
        var statusRoot = await ReadJsonObjectAsync(fs, "game_state/core/player_status.json") ?? new JsonObject();
        if (!HasBootstrapResourceGrantMarker(statusRoot, grantId))
        {
            statusRoot["money"] = GetNodeInt(statusRoot["money"], 0) + money;
            AddBootstrapResourceGrantMarker(statusRoot, grantId);
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/core/player_status.json",
                statusJson,
                statusRoot.ToJsonString(JsonOpts),
                RequireCurrentBaseline: true));
        }

        var itemsJson = await fs.ReadFileAsync("game_state/inventory/items.json");
        var itemsRoot = await ReadJsonObjectAsync(fs, "game_state/inventory/items.json") ?? BuildDefaultInventoryRoot();
        if (!HasBootstrapResourceGrantMarker(itemsRoot, grantId))
        {
            var resources = itemsRoot["resources"] as JsonObject ?? new JsonObject();
            resources["common"] = GetNodeInt(resources["common"], 0) + common;
            resources["uncommon"] = GetNodeInt(resources["uncommon"], 0) + uncommon;
            itemsRoot["resources"] = resources;
            AddBootstrapResourceGrantMarker(itemsRoot, grantId);
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                "game_state/inventory/items.json",
                itemsJson,
                itemsRoot.ToJsonString(JsonOpts),
                RequireCurrentBaseline: true));
        }

        return writes;
    }

    private static string BuildBootstrapResourceGrantId(JsonObject effectState, JsonObject resourceGrant)
    {
        var preparedAtTurn = GetNodeInt(effectState["sourcePackagePreparedAtTurn"], 0);
        var incarnation = GetNodeInt(effectState["currentIncarnation"], 0);
        var sourceCardIds = ReadStringArray(resourceGrant["sourceCardIds"])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        return $"shining_bootstrap_resource:{preparedAtTurn}:{incarnation}:{string.Join("+", sourceCardIds)}";
    }

    private static bool HasBootstrapResourceGrantMarker(JsonObject root, string grantId)
    {
        if (root[BootstrapResourceGrantMarkersProperty] is not JsonArray markers)
            return false;

        return markers
            .OfType<JsonValue>()
            .Any(value => value.TryGetValue<string>(out var existing) &&
                          string.Equals(existing, grantId, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddBootstrapResourceGrantMarker(JsonObject root, string grantId)
    {
        var markers = root[BootstrapResourceGrantMarkersProperty] as JsonArray;
        if (markers == null)
        {
            markers = new JsonArray();
            root[BootstrapResourceGrantMarkersProperty] = markers;
        }

        if (!HasBootstrapResourceGrantMarker(root, grantId))
            markers.Add(grantId);
    }

    private static IReadOnlyList<string> BuildActivationSummaryLines(JsonObject effectState)
    {
        var lines = new List<string>();

        if (effectState["resourceGrant"] is JsonObject resourceGrant)
        {
            var money = GetNodeInt(resourceGrant["money"], 0);
            var common = GetNodeInt(resourceGrant["common"], 0);
            var uncommon = GetNodeInt(resourceGrant["uncommon"], 0);
            lines.Add($"Стартовые ресурсы: +{money} денег, common x{common}, uncommon x{uncommon}.");
        }

        if (effectState["memorySelection"] is JsonObject memorySelection)
        {
            lines.Add($"Память следующей жизни: +{GetNodeInt(memorySelection["options"], 0)} вариантов, rerolls {GetNodeInt(memorySelection["rerolls"], 0)}.");
        }

        if (effectState["pendingSocialEffects"] is JsonArray socialEffects)
            AppendPendingCountLine(lines, socialEffects, SocialStatusPendingFirstRelationCommit, "Ожидают применения social effects");
        if (effectState["pendingRouteEffects"] is JsonArray routeEffects)
            AppendPendingCountLine(lines, routeEffects, RouteStatusPendingEarlyRouteSeed, "Ожидают применения route effects");
        if (effectState["pendingLoreEffects"] is JsonArray loreEffects)
            AppendPendingCountLine(lines, loreEffects, LoreStatusPendingLoreInsertion, "Ожидают применения lore effects");
        if (effectState["pendingSurvivalEffects"] is JsonArray survivalEffects)
            AppendPendingCountLine(lines, survivalEffects, SurvivalStatusPendingFirstRuinousFailure, "Ожидают применения survival effects");
        if (effectState["pendingDescentEffects"] is JsonArray descentEffects)
            AppendPendingCountLine(lines, descentEffects, DescentStatusPendingResidentDescent, "Ожидают применения descent effects");

        if (effectState["relicRefinementEntitlements"] is JsonObject relic)
        {
            lines.Add($"Реликтовые права: rerolls {GetNodeInt(relic["rerolls"], 0)}, freeShape={GetNodeBool(relic["freeShape"])}, freeRetune={GetNodeBool(relic["freeRetune"])}.");
        }

        return lines;
    }

    private static List<string> BuildDirectiveLines(JsonObject effectState)
    {
        var lines = new List<string>();

        if (effectState["resourceGrant"] is JsonObject resourceGrant)
        {
            lines.Add(
                $"Shining blessing effect: grant starting resources money={GetNodeInt(resourceGrant["money"], 0)}, common={GetNodeInt(resourceGrant["common"], 0)}, uncommon={GetNodeInt(resourceGrant["uncommon"], 0)} during mortal bootstrap.");
        }

        if (effectState["memorySelection"] is JsonObject memorySelection)
        {
            lines.Add(
                $"Shining blessing effect: before turn 1 offer +{GetNodeInt(memorySelection["options"], 0)} extra memory options and {GetNodeInt(memorySelection["rerolls"], 0)} memory-only rerolls.");
        }

        AppendDirectiveLinesForArray(
            lines,
            effectState["pendingSocialEffects"] as JsonArray,
            effect => $"Shining blessing effect: apply social.delta +{GetNodeInt(effect["delta"], 0)} to the first qualifying non-hostile relation commit.");
        AppendDirectiveLinesForArray(
            lines,
            effectState["pendingRouteEffects"] as JsonArray,
            effect => $"Shining blessing effect: seed {GetNodeInt(effect["routeOptions"], 0)} early route option(s) by or before turn {GetNodeInt(effect["latestTurn"], 0)}.");
        AppendDirectiveLinesForArray(
            lines,
            effectState["pendingLoreEffects"] as JsonArray,
            effect => $"Shining blessing effect: insert {GetNodeInt(effect["clueCount"], 0)} lore clue(s) by or before turn {GetNodeInt(effect["latestTurn"], 0)}.");
        AppendDirectiveLinesForArray(
            lines,
            effectState["pendingSurvivalEffects"] as JsonArray,
            effect => $"Shining blessing effect: downgrade the first ruinous failure by {GetNodeInt(effect["downgrade"], 0)} band and recover {GetNodeInt(effect["recovery"], 0)}%.");
        AppendDirectiveLinesForArray(
            lines,
            effectState["pendingDescentEffects"] as JsonArray,
            effect => $"Shining blessing effect: matching resident descent hook for {GetNodeString(effect["sourceActorId"]) ?? "resident"} must occur by or before turn {GetNodeInt(effect["latestTurn"], 0)} with quality +{GetNodeInt(effect["quality"], 0)}.");

        if (effectState["relicRefinementEntitlements"] is JsonObject relic)
        {
            lines.Add(
                $"Shining blessing effect: grant relic refinement entitlements rerolls={GetNodeInt(relic["rerolls"], 0)}, freeShape={GetNodeBool(relic["freeShape"])}, freeRetune={GetNodeBool(relic["freeRetune"])} for this life.");
        }

        return lines;
    }

    private static List<string> BuildReminderLines(JsonObject effectState, int currentTurnNumber)
    {
        var lines = new List<string>();

        if (effectState["resourceGrant"] is JsonObject resourceGrant)
        {
            lines.Add($"resource applied at bootstrap: +{GetNodeInt(resourceGrant["money"], 0)} money, common x{GetNodeInt(resourceGrant["common"], 0)}, uncommon x{GetNodeInt(resourceGrant["uncommon"], 0)}");
        }

        if (effectState["memorySelection"] is JsonObject memorySelection &&
            string.Equals(GetNodeString(memorySelection["status"]), MemoryStatusPendingPreTurnOneSelection, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"memory selection pending: +{GetNodeInt(memorySelection["options"], 0)} options, rerolls {GetNodeInt(memorySelection["rerolls"], 0)} before turn 1");
        }

        AppendReminderLinesForDeadlineArray(
            lines,
            effectState["pendingRouteEffects"] as JsonArray,
            currentTurnNumber,
            RouteStatusPendingEarlyRouteSeed,
            effect => $"route pending: {GetNodeInt(effect["routeOptions"], 0)} option(s) by turn {GetNodeInt(effect["latestTurn"], 0)}");
        AppendReminderLinesForDeadlineArray(
            lines,
            effectState["pendingLoreEffects"] as JsonArray,
            currentTurnNumber,
            LoreStatusPendingLoreInsertion,
            effect => $"lore pending: {GetNodeInt(effect["clueCount"], 0)} clue(s) by turn {GetNodeInt(effect["latestTurn"], 0)}");
        AppendReminderLinesForDeadlineArray(
            lines,
            effectState["pendingDescentEffects"] as JsonArray,
            currentTurnNumber,
            DescentStatusPendingResidentDescent,
            effect =>
            {
                var line = $"descent pending: {GetNodeString(effect["sourceActorId"]) ?? "resident"} by turn {GetNodeInt(effect["latestTurn"], 0)} with quality +{GetNodeInt(effect["quality"], 0)}";
                var primedRelicId = GetNodeString(effect["primedRelicId"]);
                if (!string.IsNullOrWhiteSpace(primedRelicId))
                    line += $" [primed on {primedRelicId}]";
                return line;
            });

        AppendReminderLinesForArray(
            lines,
            effectState["pendingSocialEffects"] as JsonArray,
            SocialStatusPendingFirstRelationCommit,
            effect => $"social pending: first qualifying relation commit gets +{GetNodeInt(effect["delta"], 0)}");
        AppendReminderLinesForArray(
            lines,
            effectState["pendingSurvivalEffects"] as JsonArray,
            SurvivalStatusPendingFirstRuinousFailure,
            effect => $"survival pending: first ruinous failure downgrade {GetNodeInt(effect["downgrade"], 0)}, recovery {GetNodeInt(effect["recovery"], 0)}%");

        if (effectState["relicRefinementEntitlements"] is JsonObject relic &&
            IsPendingRelicEntitlement(relic))
        {
            lines.Add($"relic entitlements pending: rerolls {GetNodeInt(relic["rerolls"], 0)}, freeShape={GetNodeBool(relic["freeShape"])}, freeRetune={GetNodeBool(relic["freeRetune"])}");
        }

        return lines;
    }

    private static List<string> BuildPlayerFacingStatusLines(JsonObject effectState, int currentTurnNumber)
    {
        var lines = new List<string>();

        if (effectState["resourceGrant"] is JsonObject resourceGrant)
        {
            lines.Add($"Подготовлено: стартовый дар уже выдан (+{GetNodeInt(resourceGrant["money"], 0)} money, common x{GetNodeInt(resourceGrant["common"], 0)}, uncommon x{GetNodeInt(resourceGrant["uncommon"], 0)}).");
        }

        if (effectState["memorySelection"] is JsonObject memorySelection &&
            string.Equals(GetNodeString(memorySelection["status"]), MemoryStatusPendingPreTurnOneSelection, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"Ожидает: выбор эха памяти до первого хода (+{GetNodeInt(memorySelection["options"], 0)} вариант(ов), перебросы {GetNodeInt(memorySelection["rerolls"], 0)}).");
        }

        AppendPlayerFacingReminderLinesForDeadlineArray(
            lines,
            effectState["pendingRouteEffects"] as JsonArray,
            currentTurnNumber,
            RouteStatusPendingEarlyRouteSeed,
            effect => $"Ожидает: ранний маршрут — {GetNodeInt(effect["routeOptions"], 0)} вариант(ов) до хода {GetNodeInt(effect["latestTurn"], 0)}.");
        AppendPlayerFacingReminderLinesForDeadlineArray(
            lines,
            effectState["pendingLoreEffects"] as JsonArray,
            currentTurnNumber,
            LoreStatusPendingLoreInsertion,
            effect => $"Ожидает: подсказка знаний — {GetNodeInt(effect["clueCount"], 0)} след(ов) до хода {GetNodeInt(effect["latestTurn"], 0)}.");
        AppendPlayerFacingReminderLinesForDeadlineArray(
            lines,
            effectState["pendingDescentEffects"] as JsonArray,
            currentTurnNumber,
            DescentStatusPendingResidentDescent,
            effect =>
            {
                var line = $"Ожидает: нисхождение резидента {GetNodeString(effect["sourceActorId"]) ?? "resident"} до хода {GetNodeInt(effect["latestTurn"], 0)} [качество +{GetNodeInt(effect["quality"], 0)}].";
                var primedRelicId = GetNodeString(effect["primedRelicId"]);
                if (!string.IsNullOrWhiteSpace(primedRelicId))
                    line += $" [dim](подготовлено на {primedRelicId})[/]";
                return line;
            });

        AppendPlayerFacingReminderLinesForArray(
            lines,
            effectState["pendingSocialEffects"] as JsonArray,
            SocialStatusPendingFirstRelationCommit,
            effect => $"Ожидает: первая не-враждебная связь получит +{GetNodeInt(effect["delta"], 0)}.");
        AppendPlayerFacingReminderLinesForArray(
            lines,
            effectState["pendingSurvivalEffects"] as JsonArray,
            SurvivalStatusPendingFirstRuinousFailure,
            effect => $"Ожидает: первая ruinous-неудача будет смягчена на {GetNodeInt(effect["downgrade"], 0)} уровень и вернёт {GetNodeInt(effect["recovery"], 0)}% потерь.");

        if (effectState["relicRefinementEntitlements"] is JsonObject relic &&
            IsPendingRelicEntitlement(relic))
        {
            lines.Add($"Ожидает: кузнечные привилегии этой жизни — перебросы {GetNodeInt(relic["rerolls"], 0)}, freeShape={GetNodeBool(relic["freeShape"])}, freeRetune={GetNodeBool(relic["freeRetune"])}.");
        }

        return lines;
    }

    private static void AppendDirectiveLinesForArray(List<string> lines, JsonArray? effects, Func<JsonObject, string> formatter)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
            lines.Add(formatter(effect));
    }

    private static void AppendReminderLinesForArray(List<string> lines, JsonArray? effects, string expectedPendingStatus, Func<JsonObject, string> formatter)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(effect["status"]), expectedPendingStatus, StringComparison.OrdinalIgnoreCase))
                lines.Add(formatter(effect));
        }
    }

    private static void AppendReminderLinesForDeadlineArray(List<string> lines, JsonArray? effects, int currentTurnNumber, string expectedPendingStatus, Func<JsonObject, string> formatter)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), expectedPendingStatus, StringComparison.OrdinalIgnoreCase))
                continue;

            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            var line = formatter(effect);
            if (latestTurn > 0 && currentTurnNumber > latestTurn)
                line += " [OVERDUE]";
            lines.Add(line);
        }
    }

    private static void AppendPendingCountLine(List<string> lines, JsonArray effects, string expectedPendingStatus, string label)
    {
        var pendingCount = effects.OfType<JsonObject>().Count(effect =>
            string.Equals(GetNodeString(effect["status"]), expectedPendingStatus, StringComparison.OrdinalIgnoreCase));
        if (pendingCount > 0)
            lines.Add($"{label}: {pendingCount}.");
    }

    private static void AppendStatusLifecycleSummary(List<string> lines, JsonObject effectState, int currentTurnNumber)
    {
        AppendLifecycleStatusLine(lines, effectState["pendingSocialEffects"] as JsonArray, "social");
        AppendLifecycleStatusLine(lines, effectState["pendingRouteEffects"] as JsonArray, "route");
        AppendLifecycleStatusLine(lines, effectState["pendingLoreEffects"] as JsonArray, "lore");
        AppendLifecycleStatusLine(lines, effectState["pendingSurvivalEffects"] as JsonArray, "survival");
        AppendLifecycleStatusLine(lines, effectState["pendingDescentEffects"] as JsonArray, "descent");
        AppendPrimedDescentDetails(lines, effectState["pendingDescentEffects"] as JsonArray);
        AppendConsumedEffectDetails(lines, effectState["pendingSocialEffects"] as JsonArray, "social");
        AppendConsumedEffectDetails(lines, effectState["pendingRouteEffects"] as JsonArray, "route");
        AppendConsumedEffectDetails(lines, effectState["pendingLoreEffects"] as JsonArray, "lore");
        AppendConsumedEffectDetails(lines, effectState["pendingSurvivalEffects"] as JsonArray, "survival");
        AppendConsumedEffectDetails(lines, effectState["pendingDescentEffects"] as JsonArray, "descent");
        AppendExpiredEffectDetails(lines, effectState["pendingRouteEffects"] as JsonArray, "route", currentTurnNumber);
        AppendExpiredEffectDetails(lines, effectState["pendingLoreEffects"] as JsonArray, "lore", currentTurnNumber);
        AppendExpiredEffectDetails(lines, effectState["pendingDescentEffects"] as JsonArray, "descent", currentTurnNumber);

        if (effectState["memorySelection"] is JsonObject memorySelection &&
            string.Equals(GetNodeString(memorySelection["status"]), GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            var selectedSummary = GetNodeString(memorySelection["selectedLifeSummary"]);
            var rerollsSpent = GetNodeInt(memorySelection["rerollsSpent"], 0);
            if (!string.IsNullOrWhiteSpace(selectedSummary))
            {
                var suffix = rerollsSpent > 0 ? $" Перебросов потрачено: {rerollsSpent}." : string.Empty;
                lines.Add($"Израсходовано: выбор эха памяти завершён — {selectedSummary}.{suffix}");
            }
            else
            {
                var suffix = rerollsSpent > 0 ? $" Перебросов потрачено: {rerollsSpent}." : string.Empty;
                lines.Add($"Израсходовано: выбор эха памяти завершён в этой жизни.{suffix}");
            }
        }

        if (effectState["relicRefinementEntitlements"] is JsonObject entitlements &&
            string.Equals(GetNodeString(entitlements["status"]), GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
        {
            var rerollsSpent = GetNodeInt(entitlements["rerollsSpent"], 0);
            var suffix = rerollsSpent > 0 ? $" Перебросов потрачено: {rerollsSpent}." : string.Empty;
            lines.Add($"Израсходовано: кузнечные привилегии этой жизни исчерпаны.{suffix}");
        }
    }

    private static void AppendLifecycleStatusLine(List<string> lines, JsonArray? effects, string familyName)
    {
        if (effects == null)
            return;

        var consumed = effects.OfType<JsonObject>().Count(effect =>
            string.Equals(GetNodeString(effect["status"]), GenericStatusConsumed, StringComparison.OrdinalIgnoreCase));
        var expired = effects.OfType<JsonObject>().Count(effect =>
            string.Equals(GetNodeString(effect["status"]), GenericStatusExpired, StringComparison.OrdinalIgnoreCase));

        if (consumed > 0)
            lines.Add($"Израсходовано: {familyName} x{consumed}.");
        if (expired > 0)
            lines.Add($"Истекло: {familyName} x{expired}.");
    }

    private static void AppendConsumedEffectDetails(List<string> lines, JsonArray? effects, string familyName)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), GenericStatusConsumed, StringComparison.OrdinalIgnoreCase))
                continue;

            switch (familyName)
            {
                case "social":
                    var socialTarget = GetNodeString(effect["consumedTargetFactionName"]) ??
                                       GetNodeString(effect["consumedTargetNpcName"]) ??
                                       GetNodeString(effect["consumedTargetFactionId"]) ??
                                       GetNodeString(effect["consumedTargetNpcId"]);
                    if (!string.IsNullOrWhiteSpace(socialTarget))
                        lines.Add($"Израсходовано: социальное благословение закрылось через связь с {socialTarget}.");
                    break;

                case "route":
                    var routeSeedIds = ReadStringArray(effect["consumedRouteSeedIds"]);
                    if (routeSeedIds.Count > 0)
                        lines.Add($"Израсходовано: маршрут раскрылся через seed {string.Join(", ", routeSeedIds)}.");
                    break;

                case "lore":
                    var anchorIds = ReadStringArray(effect["consumedAnchorIds"]);
                    if (anchorIds.Count > 0)
                        lines.Add($"Израсходовано: след знания закрепился через anchor {string.Join(", ", anchorIds)}.");
                    break;

                case "survival":
                    var survivalEventId = GetNodeString(effect["consumedEventId"]);
                    if (!string.IsNullOrWhiteSpace(survivalEventId))
                    {
                        var restoredDetails = new List<string>();
                        if (GetNodeInt(effect["restoredHealthPercentagePoints"], 0) > 0)
                            restoredDetails.Add($"health+{GetNodeInt(effect["restoredHealthPercentagePoints"], 0)}");
                        if (GetNodeInt(effect["restoredEnergyPercentagePoints"], 0) > 0)
                            restoredDetails.Add($"energy+{GetNodeInt(effect["restoredEnergyPercentagePoints"], 0)}");
                        if (GetNodeInt(effect["restoredPoisePercentagePoints"], 0) > 0)
                            restoredDetails.Add($"poise+{GetNodeInt(effect["restoredPoisePercentagePoints"], 0)}");
                        var suffix = restoredDetails.Count > 0 ? $" Восстановлено: {string.Join(", ", restoredDetails)}." : string.Empty;
                        lines.Add($"Израсходовано: спасающее благословение сработало через {survivalEventId}.{suffix}");
                    }
                    break;

                case "descent":
                    var descentNpc = GetNodeString(effect["consumedNpcName"]) ?? GetNodeString(effect["consumedNpcId"]);
                    if (!string.IsNullOrWhiteSpace(descentNpc))
                        lines.Add($"Израсходовано: нисхождение закрылось через проявленного спутника {descentNpc}.");
                    break;
            }
        }
    }

    private static void AppendPrimedDescentDetails(List<string> lines, JsonArray? effects)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), DescentStatusPendingResidentDescent, StringComparison.OrdinalIgnoreCase))
                continue;

            var primedRelicId = GetNodeString(effect["primedRelicId"]);
            if (string.IsNullOrWhiteSpace(primedRelicId))
                continue;

            lines.Add($"Подготовлено: нисхождение уже закреплено на реликвии {primedRelicId}.");
        }
    }

    private static void AppendExpiredEffectDetails(List<string> lines, JsonArray? effects, string familyName, int currentTurnNumber)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), GenericStatusExpired, StringComparison.OrdinalIgnoreCase))
                continue;

            var effectId = GetNodeString(effect["effectId"]);
            if (!string.IsNullOrWhiteSpace(effectId))
            {
                var latestTurn = GetNodeInt(effect["latestTurn"], 0);
                if (latestTurn > 0)
                {
                    lines.Add($"Истекло: {familyName} {effectId} не успело раскрыться к ходу {latestTurn} [сейчас ход {currentTurnNumber}].");
                }
                else
                {
                    lines.Add($"Истекло: {familyName} {effectId}.");
                }
            }
        }
    }

    private static void AppendPlayerFacingReminderLinesForArray(List<string> lines, JsonArray? effects, string expectedPendingStatus, Func<JsonObject, string> formatter)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(effect["status"]), expectedPendingStatus, StringComparison.OrdinalIgnoreCase))
                lines.Add(formatter(effect));
        }
    }

    private static void AppendPlayerFacingReminderLinesForDeadlineArray(List<string> lines, JsonArray? effects, int currentTurnNumber, string expectedPendingStatus, Func<JsonObject, string> formatter)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), expectedPendingStatus, StringComparison.OrdinalIgnoreCase))
                continue;

            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            var line = formatter(effect);
            if (latestTurn > 0)
            {
                var turnsLeft = latestTurn - currentTurnNumber;
                line += turnsLeft >= 0
                    ? $" [dim](осталось ходов: {turnsLeft})[/]"
                    : $" [red](срок вышел на ходу {latestTurn})[/]";
            }

            lines.Add(line);
        }
    }

    private static bool TryApplySocialEffectsFromNpcCoreDiff(
        JsonObject effectState,
        JsonObject? currentNpcRoot,
        string? preTurnNpcCoreJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentNpcRoot == null || effectState["pendingSocialEffects"] is not JsonArray socialEffects)
            return false;

        var activeEffects = socialEffects.OfType<JsonObject>()
            .Where(IsPendingSocialEffect)
            .OrderBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var preTurnNpcRoot = ParseJsonNode(preTurnNpcCoreJson) as JsonObject;
        var preTurnNpcIds = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(preTurnNpcRoot)
            .Select(GetNpcId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetNpc = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(currentNpcRoot)
            .Where(npc =>
            {
                var npcId = GetNpcId(npc);
                if (string.IsNullOrWhiteSpace(npcId) || preTurnNpcIds.Contains(npcId))
                    return false;

                return GetNodeInt(npc["relationshipLevel"], int.MinValue) >= 0;
            })
            .OrderBy(npc => GetNpcId(npc), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (targetNpc == null)
            return false;

        var npcId = GetNpcId(targetNpc);
        var npcName = GetNodeString(targetNpc["NPCName"]) ??
                      GetNodeString(targetNpc["name"]) ??
                      npcId;
        var delta = activeEffects.Sum(effect => Math.Max(0, GetNodeInt(effect["delta"], 0)));
        if (delta <= 0)
            return false;

        var nextRelationshipLevel = Math.Clamp(GetNodeInt(targetNpc["relationshipLevel"], 0) + delta, -400, 400);
        targetNpc["relationshipLevel"] = nextRelationshipLevel;
        targetNpc["attitude"] = ResolveNpcAttitude(nextRelationshipLevel);

        foreach (var effect in activeEffects)
        {
            MarkConsumed(effect, currentTurnNumber);
            effect["consumedTargetNpcId"] = npcId;
            effect["consumedTargetNpcName"] = npcName;
        }

        summaryLines.Add($"social blessing applied to first non-hostile NPC contact: {npcName} (+{delta} relationship)");
        return true;
    }

    private static bool TryApplySocialEffectsFromRelationCommits(
        JsonObject effectState,
        JsonObject? currentNpcRoot,
        JsonObject? currentNpcRelationshipsRoot,
        string? preTurnNpcRelationshipsJson,
        JsonObject? currentFactionCoreRoot,
        string? preTurnFactionCoreJson,
        int currentTurnNumber,
        List<string> summaryLines,
        out bool touchedFaction,
        out bool relationCommitObserved)
    {
        touchedFaction = false;
        relationCommitObserved = false;
        if (effectState["pendingSocialEffects"] is not JsonArray socialEffects)
        {
            return false;
        }

        var activeEffects = socialEffects.OfType<JsonObject>()
            .Where(IsPendingSocialEffect)
            .OrderBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var delta = activeEffects.Sum(effect => Math.Max(0, GetNodeInt(effect["delta"], 0)));
        if (delta <= 0)
            return false;

        var candidates = new List<SocialCommitCandidate>();

        if (currentNpcRoot != null && currentNpcRelationshipsRoot != null)
        {
            var preTurnRelationshipsRoot = ParseJsonNode(preTurnNpcRelationshipsJson) as JsonObject;
            var preTurnCommitKeys = EnumerateNpcRelationshipCommitSignals(preTurnRelationshipsRoot)
                .Select(signal => signal.UniqueKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newNpcCommitSignals = EnumerateNpcRelationshipCommitSignals(currentNpcRelationshipsRoot)
                .Where(signal => !preTurnCommitKeys.Contains(signal.UniqueKey))
                .ToList();
            if (newNpcCommitSignals.Count > 0)
                relationCommitObserved = true;

            foreach (var signal in newNpcCommitSignals.Where(signal => signal.NewRelationshipLevel >= 0))
            {
                var targetNpc = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(currentNpcRoot)
                    .FirstOrDefault(npc =>
                        string.Equals(GetNpcId(npc), signal.TargetNpcId, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(signal.TargetNpcName) &&
                         string.Equals(GetNodeString(npc["NPCName"]) ?? GetNodeString(npc["name"]), signal.TargetNpcName, StringComparison.OrdinalIgnoreCase)));
                if (targetNpc == null)
                    continue;

                candidates.Add(new SocialNpcCommitCandidate(
                    signal.TargetNpcId,
                    signal.TargetNpcName,
                    targetNpc,
                    signal));
            }
        }

        var preTurnFactionRoot = ParseJsonNode(preTurnFactionCoreJson);
        var preTurnFactionsById = EnumerateFactionCoreEntries(preTurnFactionRoot)
            .Select(entry => (FactionId: GetNodeString(entry["factionId"]) ?? string.Empty, Entry: entry))
            .Where(item => !string.IsNullOrWhiteSpace(item.FactionId))
            .ToDictionary(item => item.FactionId, item => item.Entry, StringComparer.OrdinalIgnoreCase);
        foreach (var faction in EnumerateFactionCoreEntries(currentFactionCoreRoot))
        {
            var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                continue;
            }

            preTurnFactionsById.TryGetValue(factionId, out var preTurnFaction);
            var hasFactionCommit = HasFactionPlayerRelationCommit(faction, preTurnFaction);
            if (!hasFactionCommit)
                continue;
            relationCommitObserved = true;

            if (!TryReadIntNode(faction["reputation"], out var currentReputation) || currentReputation < 0)
                continue;

            var factionName = GetNodeString(faction["name"]) ??
                              GetNodeString(faction["factionName"]) ??
                              factionId;
            candidates.Add(new SocialFactionCommitCandidate(
                factionId,
                factionName,
                faction,
                currentReputation));
        }

        if (candidates.Count == 0)
            return false;

        var chosen = candidates
            .OrderBy(candidate => candidate.SortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .First();

        switch (chosen)
        {
            case SocialNpcCommitCandidate npcCommit:
                var nextRelationshipLevel = Math.Clamp(GetNodeInt(npcCommit.Npc["relationshipLevel"], npcCommit.Signal.NewRelationshipLevel) + delta, -400, 400);
                npcCommit.Npc["relationshipLevel"] = nextRelationshipLevel;
                npcCommit.Npc["attitude"] = ResolveNpcAttitude(nextRelationshipLevel);
                npcCommit.Signal.Entry["newRelationshipLevel"] = nextRelationshipLevel;
                foreach (var effect in activeEffects)
                {
                    MarkConsumed(effect, currentTurnNumber);
                    effect["consumedTargetNpcId"] = npcCommit.TargetNpcId;
                    effect["consumedTargetNpcName"] = npcCommit.TargetNpcName;
                    effect["consumedRelationshipChangeReason"] = npcCommit.Signal.ChangeReason;
                }

                summaryLines.Add($"social blessing applied to relation commit: {npcCommit.TargetNpcName} (+{delta} relationship)");
                return true;

            case SocialFactionCommitCandidate factionCommit:
                factionCommit.Faction["reputation"] = factionCommit.CurrentReputation + delta;
                foreach (var effect in activeEffects)
                {
                    MarkConsumed(effect, currentTurnNumber);
                    effect["consumedTargetFactionId"] = factionCommit.FactionId;
                    effect["consumedTargetFactionName"] = factionCommit.FactionName;
                }

                summaryLines.Add($"social blessing applied to faction relation commit: {factionCommit.FactionName} (+{delta} reputation)");
                touchedFaction = true;
                return true;
        }

        return true;
    }

    private static bool ConsumeForgeEntitlementsFromAcceptedReceipts(
        JsonObject soulRoot,
        JsonObject effectState,
        JsonObject? currentShiningRoot,
        string? preTurnShiningJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentShiningRoot?["coreActionReceipts"] is not JsonArray currentReceipts ||
            effectState["relicRefinementEntitlements"] is not JsonObject entitlements ||
            !IsPendingRelicEntitlement(entitlements))
        {
            return false;
        }

        var preTurnShiningRoot = ParseJsonNode(preTurnShiningJson) as JsonObject;
        var knownReceiptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preTurnShiningRoot?["coreActionReceipts"] is JsonArray preTurnReceipts)
        {
            foreach (var receipt in preTurnReceipts.OfType<JsonObject>())
            {
                var requestId = GetNodeString(receipt["requestId"]);
                if (!string.IsNullOrWhiteSpace(requestId))
                    knownReceiptIds.Add(requestId);
            }
        }

        var changed = false;
        foreach (var receipt in currentReceipts.OfType<JsonObject>())
        {
            var requestId = GetNodeString(receipt["requestId"]);
            var actionType = GetNodeString(receipt["actionType"]);
            var status = GetNodeString(receipt["status"]);
            if (string.IsNullOrWhiteSpace(requestId) ||
                knownReceiptIds.Contains(requestId) ||
                !string.Equals(status, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
                (!string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicReshape, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var resolvedAtTurn = GetNodeInt(receipt["resolvedAtTurn"], 0);
            var resolvedAtUtc = GetNodeString(receipt["resolvedAtUtc"]);
            if (!ConsumeForgeEntitlements(
                    soulRoot,
                    actionType,
                    resolvedAtTurn > 0 ? resolvedAtTurn : currentTurnNumber,
                    resolvedAtUtc))
                continue;

            summaryLines.Add($"forge blessing entitlement spent on accepted {actionType}");
            changed = true;
        }

        return changed;
    }

    private static bool TryConsumeSurvivalEffectsFromWorldState(
        JsonObject effectState,
        JsonNode? currentWorldEventsRoot,
        string? preTurnWorldEventsJson,
        JsonObject? currentPlayerStatusRoot,
        string? preTurnPlayerStatusJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentPlayerStatusRoot == null || effectState["pendingSurvivalEffects"] is not JsonArray survivalEffects)
            return false;

        var activeEffects = survivalEffects.OfType<JsonObject>()
            .Where(effect => string.Equals(GetNodeString(effect["status"]), SurvivalStatusPendingFirstRuinousFailure, StringComparison.OrdinalIgnoreCase))
            .OrderBy(effect => GetNodeString(effect["sourceCardId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var preTurnWorldEventsRoot = ParseJsonNode(preTurnWorldEventsJson);
        var preTurnVisibleEventIds = EnumerateVisibleWorldEventSignals(preTurnWorldEventsRoot)
            .Select(signal => signal.EventId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ruinousSignal = EnumerateVisibleWorldEventSignals(currentWorldEventsRoot)
            .Where(signal =>
                !preTurnVisibleEventIds.Contains(signal.EventId) &&
                IsRuinousWorldEventSignal(signal))
            .OrderBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (ruinousSignal == null)
            return false;

        var preTurnStatusRoot = ParseJsonNode(preTurnPlayerStatusJson) as JsonObject;
        var restoredHealth = TryRestorePrimaryGaugePercentage(preTurnStatusRoot, currentPlayerStatusRoot, "healthPercentage", activeEffects[0]);
        var restoredEnergy = TryRestorePrimaryGaugePercentage(preTurnStatusRoot, currentPlayerStatusRoot, "energyPercentage", activeEffects[0]);
        var restoredPoise = TryRestorePrimaryGaugePercentage(preTurnStatusRoot, currentPlayerStatusRoot, "poisePercentage", activeEffects[0]);

        if (!string.IsNullOrWhiteSpace(ruinousSignal.SeverityFieldName) &&
            ruinousSignal.Entry != null)
        {
            ruinousSignal.Entry[ruinousSignal.SeverityFieldName] = DowngradeSeverityValue(ruinousSignal.Severity);
        }

        var chosenEffect = activeEffects[0];
        MarkConsumed(chosenEffect, currentTurnNumber);
        chosenEffect["consumedEventId"] = ruinousSignal.EventId;
        if (!string.IsNullOrWhiteSpace(ruinousSignal.SeverityFieldName))
            chosenEffect["consumedSeverityField"] = ruinousSignal.SeverityFieldName;
        if (restoredHealth > 0)
            chosenEffect["restoredHealthPercentagePoints"] = restoredHealth;
        if (restoredEnergy > 0)
            chosenEffect["restoredEnergyPercentagePoints"] = restoredEnergy;
        if (restoredPoise > 0)
            chosenEffect["restoredPoisePercentagePoints"] = restoredPoise;

        summaryLines.Add(
            $"survival blessing applied to ruinous failure: {ruinousSignal.EventId} (restored health +{restoredHealth}, energy +{restoredEnergy}, poise +{restoredPoise})");
        return true;
    }

    private static bool TryConsumeLoreEffectsFromWorldEvents(
        JsonObject effectState,
        JsonNode? currentWorldEventsRoot,
        string? preTurnWorldEventsJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentWorldEventsRoot == null || effectState["pendingLoreEffects"] is not JsonArray loreEffects)
            return false;

        var activeEffects = loreEffects.OfType<JsonObject>()
            .Where(IsPendingLoreEffect)
            .OrderBy(effect => GetNodeInt(effect["latestTurn"], int.MaxValue))
            .ThenBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var preTurnWorldEventsRoot = ParseJsonNode(preTurnWorldEventsJson);
        var preTurnVisibleAnchorIds = EnumerateVisibleWorldEventSignals(preTurnWorldEventsRoot)
            .Select(signal => signal.AnchorId)
            .Where(anchorId => !string.IsNullOrWhiteSpace(anchorId))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableCurrentEvents = EnumerateVisibleWorldEventSignals(currentWorldEventsRoot)
            .Where(signal =>
                !string.IsNullOrWhiteSpace(signal.AnchorId) &&
                !preTurnVisibleAnchorIds.Contains(signal.AnchorId!) &&
                !string.IsNullOrWhiteSpace(signal.AnchorId))
            .GroupBy(signal => signal.AnchorId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(signal => signal.AnchorId ?? signal.EventId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (availableCurrentEvents.Count == 0)
            return false;

        var changed = false;
        foreach (var effect in activeEffects)
        {
            var clueCount = Math.Max(0, GetNodeInt(effect["clueCount"], 0));
            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            if (latestTurn > 0 && currentTurnNumber > latestTurn)
                continue;

            if (clueCount <= 0)
            {
                MarkConsumed(effect, currentTurnNumber);
                changed = true;
                continue;
            }

            if (availableCurrentEvents.Count < clueCount)
                continue;

            var consumedSignals = availableCurrentEvents.Take(clueCount).ToArray();
            availableCurrentEvents.RemoveRange(0, clueCount);
            var consumedIds = consumedSignals.Select(signal => signal.EventId).ToArray();
            var consumedAnchorIds = consumedSignals
                .Select(signal => signal.AnchorId)
                .Where(anchorId => !string.IsNullOrWhiteSpace(anchorId))
                .Cast<string>()
                .ToArray();
            MarkConsumed(effect, currentTurnNumber);
            effect["consumedEventIds"] = new JsonArray(consumedIds.Select(id => (JsonNode?)id).ToArray());
            if (consumedAnchorIds.Length > 0)
                effect["consumedAnchorIds"] = new JsonArray(consumedAnchorIds.Select(id => (JsonNode?)id).ToArray());
            summaryLines.Add($"lore blessing satisfied through visible world events: {string.Join(", ", consumedIds)}");
            changed = true;
        }

        return changed;
    }

    private static bool TryConsumeRouteEffectsFromWorldEvents(
        JsonObject effectState,
        JsonNode? currentWorldEventsRoot,
        string? preTurnWorldEventsJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentWorldEventsRoot == null || effectState["pendingRouteEffects"] is not JsonArray routeEffects)
            return false;

        var activeEffects = routeEffects.OfType<JsonObject>()
            .Where(effect => string.Equals(GetNodeString(effect["status"]), RouteStatusPendingEarlyRouteSeed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(effect => GetNodeInt(effect["latestTurn"], int.MaxValue))
            .ThenBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var preTurnWorldEventsRoot = ParseJsonNode(preTurnWorldEventsJson);
        var preTurnRouteSeedIds = EnumerateVisibleWorldEventSignals(preTurnWorldEventsRoot)
            .Where(signal => !string.IsNullOrWhiteSpace(signal.RouteSeedId))
            .Select(signal => signal.RouteSeedId)
            .Where(routeSeedId => !string.IsNullOrWhiteSpace(routeSeedId))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableCurrentRoutes = EnumerateVisibleWorldEventSignals(currentWorldEventsRoot)
            .Where(signal =>
                !string.IsNullOrWhiteSpace(signal.RouteSeedId) &&
                !preTurnRouteSeedIds.Contains(signal.RouteSeedId!))
            .GroupBy(signal => signal.RouteSeedId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(signal => signal.RouteSeedId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(signal => signal.EventId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (availableCurrentRoutes.Count == 0)
            return false;

        var changed = false;
        foreach (var effect in activeEffects)
        {
            var routeOptions = Math.Max(0, GetNodeInt(effect["routeOptions"], 0));
            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            if (latestTurn > 0 && currentTurnNumber > latestTurn)
                continue;

            if (routeOptions <= 0)
            {
                MarkConsumed(effect, currentTurnNumber);
                changed = true;
                continue;
            }

            if (availableCurrentRoutes.Count < routeOptions)
                continue;

            var consumedSignals = availableCurrentRoutes.Take(routeOptions).ToArray();
            availableCurrentRoutes.RemoveRange(0, routeOptions);
            MarkConsumed(effect, currentTurnNumber);
            effect["consumedEventIds"] = new JsonArray(consumedSignals.Select(signal => (JsonNode?)signal.EventId).ToArray());
            effect["consumedRouteSeedIds"] = new JsonArray(consumedSignals
                .Select(signal => (JsonNode?)signal.RouteSeedId!)
                .ToArray());
            summaryLines.Add($"route blessing satisfied through visible route events: {string.Join(", ", consumedSignals.Select(signal => signal.RouteSeedId))}");
            changed = true;
        }

        return changed;
    }

    private static bool TryPrimeDescentEffects(
        JsonObject soulRoot,
        JsonObject effectState,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (effectState["pendingDescentEffects"] is not JsonArray descentEffects)
            return false;

        var changed = false;
        foreach (var effect in descentEffects.OfType<JsonObject>()
                     .Where(effect => string.Equals(GetNodeString(effect["status"]), DescentStatusPendingResidentDescent, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(effect => GetNodeString(effect["sourceActorId"]), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase))
        {
            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            if (latestTurn > 0 && currentTurnNumber > latestTurn)
                continue;

            var sourceResidentId = GetNodeString(effect["sourceActorId"]);
            if (string.IsNullOrWhiteSpace(sourceResidentId))
                continue;

            var matchingRelic = EnumerateSoulRelics(soulRoot)
                .Where(relic =>
                    relic["companionSeed"] is JsonObject companionSeed &&
                    string.Equals(GetNodeString(companionSeed["sourceResidentId"]), sourceResidentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(relic => GetNodeString(relic["relicId"]), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (matchingRelic == null)
                continue;

            var matchingRelicId = GetNodeString(matchingRelic["relicId"]) ?? string.Empty;
            if (string.Equals(GetNodeString(effect["primedRelicId"]), matchingRelicId, StringComparison.OrdinalIgnoreCase))
                continue;

            var quality = Math.Max(0, GetNodeInt(effect["quality"], 0));
            matchingRelic["companionManifestationQualityBonus"] = GetNodeInt(matchingRelic["companionManifestationQualityBonus"], 0) + quality;
            effect["primedRelicId"] = matchingRelicId;
            effect["primedAtTurn"] = Math.Max(0, currentTurnNumber);
            effect["primedAtUtc"] = DateTime.UtcNow.ToString("o");
            summaryLines.Add($"descent blessing primed on relic {GetNodeString(matchingRelic["name"]) ?? GetNodeString(matchingRelic["relicId"]) ?? "relic"} (+{quality} quality)");
            changed = true;
        }

        return changed;
    }

    private static bool TryConsumeDescentEffectsFromManifestation(
        JsonObject effectState,
        JsonObject? currentNpcRoot,
        string? preTurnNpcCoreJson,
        int currentTurnNumber,
        List<string> summaryLines)
    {
        if (currentNpcRoot == null || effectState["pendingDescentEffects"] is not JsonArray descentEffects)
            return false;

        var activeEffects = descentEffects.OfType<JsonObject>()
            .Where(effect => string.Equals(GetNodeString(effect["status"]), DescentStatusPendingResidentDescent, StringComparison.OrdinalIgnoreCase))
            .OrderBy(effect => GetNodeString(effect["sourceActorId"]), StringComparer.OrdinalIgnoreCase)
            .ThenBy(effect => GetNodeString(effect["effectId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (activeEffects.Count == 0)
            return false;

        var preTurnNpcRoot = ParseJsonNode(preTurnNpcCoreJson) as JsonObject;
        var preTurnCompanionKeys = EnumerateManifestedCompanionKeys(preTurnNpcRoot)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableManifestations = EnumerateManifestedCompanionSignals(currentNpcRoot)
            .Where(signal => !preTurnCompanionKeys.Contains(signal.UniqueKey))
            .OrderBy(signal => signal.UniqueKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (availableManifestations.Count == 0)
            return false;

        var changed = false;
        foreach (var effect in activeEffects)
        {
            var sourceActorId = GetNodeString(effect["sourceActorId"]);
            if (string.IsNullOrWhiteSpace(sourceActorId))
                continue;

            var primedRelicId = GetNodeString(effect["primedRelicId"]);
            var manifestation = availableManifestations.FirstOrDefault(signal =>
                string.Equals(signal.SourceAfterlifeResidentId, sourceActorId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(primedRelicId) ||
                 string.IsNullOrWhiteSpace(signal.SourceCompanionRelicId) ||
                 string.Equals(signal.SourceCompanionRelicId, primedRelicId, StringComparison.OrdinalIgnoreCase)));
            if (manifestation == null)
                continue;

            availableManifestations.Remove(manifestation);
            MarkConsumed(effect, currentTurnNumber);
            effect["consumedNpcId"] = manifestation.NpcId;
            effect["consumedNpcName"] = manifestation.NpcName;
            if (!string.IsNullOrWhiteSpace(manifestation.SourceCompanionRelicId))
                effect["consumedRelicId"] = manifestation.SourceCompanionRelicId;
            summaryLines.Add($"descent blessing resolved through manifested companion: {manifestation.NpcName}");
            changed = true;
        }

        return changed;
    }

    private static bool ExpireDeadlineEffects(JsonObject effectState, int currentTurnNumber, List<string> summaryLines)
    {
        if (currentTurnNumber <= 0)
            return false;

        var changed = false;
        changed |= ExpireDeadlineEffects(
            effectState["pendingRouteEffects"] as JsonArray,
            RouteStatusPendingEarlyRouteSeed,
            currentTurnNumber,
            summaryLines,
            "route blessing expired");
        changed |= ExpireDeadlineEffects(
            effectState["pendingLoreEffects"] as JsonArray,
            LoreStatusPendingLoreInsertion,
            currentTurnNumber,
            summaryLines,
            "lore blessing expired");
        changed |= ExpireDeadlineEffects(
            effectState["pendingDescentEffects"] as JsonArray,
            DescentStatusPendingResidentDescent,
            currentTurnNumber,
            summaryLines,
            "descent blessing expired");
        return changed;
    }

    private static bool ExpireDeadlineEffects(
        JsonArray? effects,
        string pendingStatus,
        int currentTurnNumber,
        List<string> summaryLines,
        string message)
    {
        if (effects == null)
            return false;

        var changed = false;
        foreach (var effect in effects.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(effect["status"]), pendingStatus, StringComparison.OrdinalIgnoreCase))
                continue;

            var latestTurn = GetNodeInt(effect["latestTurn"], 0);
            if (latestTurn <= 0 || currentTurnNumber <= latestTurn)
                continue;

            MarkExpired(effect, currentTurnNumber);
            summaryLines.Add($"{message}: {GetNodeString(effect["effectId"]) ?? "unknown_effect"}");
            changed = true;
        }

        return changed;
    }

    private static void NormalizeBlessingState(JsonObject effectState)
    {
        NormalizeEmptyArray(effectState, "pendingSocialEffects");
        NormalizeEmptyArray(effectState, "pendingRouteEffects");
        NormalizeEmptyArray(effectState, "pendingLoreEffects");
        NormalizeEmptyArray(effectState, "pendingSurvivalEffects");
        NormalizeEmptyArray(effectState, "pendingDescentEffects");
    }

    private static void NormalizeEmptyArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array && array.Count == 0)
            root.Remove(propertyName);
    }

    private static bool IsPendingSocialEffect(JsonObject effect) =>
        string.Equals(GetNodeString(effect["status"]), SocialStatusPendingFirstRelationCommit, StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingLoreEffect(JsonObject effect) =>
        string.Equals(GetNodeString(effect["status"]), LoreStatusPendingLoreInsertion, StringComparison.OrdinalIgnoreCase);

    private static bool IsPendingRelicEntitlement(JsonObject entitlements) =>
        string.Equals(GetNodeString(entitlements["status"]), RelicStatusPendingEntitlement, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ReadStringArray(JsonNode? value)
    {
        if (value is not JsonArray array)
            return Array.Empty<string>();

        return array.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var result) ? result : null)
            .Where(result => !string.IsNullOrWhiteSpace(result))
            .Cast<string>()
            .ToList();
    }

    private static void MarkConsumed(JsonObject value, int currentTurnNumber, string? occurredAtUtc = null)
    {
        value["status"] = GenericStatusConsumed;
        value["consumedAtTurn"] = Math.Max(0, currentTurnNumber);
        value["consumedAtUtc"] = string.IsNullOrWhiteSpace(occurredAtUtc)
            ? DateTime.UtcNow.ToString("o")
            : occurredAtUtc.Trim();
    }

    private static void MarkExpired(JsonObject value, int currentTurnNumber)
    {
        value["status"] = GenericStatusExpired;
        value["expiredAtTurn"] = Math.Max(0, currentTurnNumber);
        value["expiredAtUtc"] = DateTime.UtcNow.ToString("o");
    }

    private static IReadOnlyList<MemoryEchoCandidate> BuildMemoryEchoCandidates(JsonObject soulRoot)
    {
        var result = new List<MemoryEchoCandidate>();
        if (soulRoot["livesHistory"] is not JsonArray livesHistory)
            return result;

        foreach (var life in livesHistory.OfType<JsonObject>())
        {
            var summary = GetNodeString(life["summary"]) ?? GetNodeString(life["sourceLifeHint"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
                continue;

            var incarnation = Math.Max(0, GetNodeInt(life["incarnation"], 0));
            var endedAt = GetNodeString(life["endedAt"]) ?? string.Empty;
            var lifeHint = !string.IsNullOrWhiteSpace(endedAt)
                ? $"life_{incarnation}:{endedAt}"
                : $"life_{incarnation}";
            result.Add(new MemoryEchoCandidate(
                incarnation,
                lifeHint,
                summary));
        }

        return result
            .OrderByDescending(candidate => candidate.Incarnation)
            .ThenByDescending(candidate => candidate.LifeHint, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int TryRestorePrimaryGaugePercentage(
        JsonObject? preTurnStatusRoot,
        JsonObject currentStatusRoot,
        string propertyName,
        JsonObject effect)
    {
        var preTurnPercent = ReadPercentageValue(preTurnStatusRoot, propertyName);
        var currentPercent = ReadPercentageValue(currentStatusRoot, propertyName);
        if (!preTurnPercent.HasValue || !currentPercent.HasValue || currentPercent.Value >= preTurnPercent.Value)
            return 0;

        var recoveryPercent = Math.Max(0, GetNodeInt(effect["recovery"], 0));
        if (recoveryPercent <= 0)
            return 0;

        var lostAmount = preTurnPercent.Value - currentPercent.Value;
        var restoredAmount = (int)Math.Floor(lostAmount * (recoveryPercent / 100.0));
        if (restoredAmount <= 0)
            return 0;

        currentStatusRoot[propertyName] = $"{Math.Min(preTurnPercent.Value, currentPercent.Value + restoredAmount)}%";
        return restoredAmount;
    }

    private static int? ReadPercentageValue(JsonObject? statusRoot, string propertyName)
    {
        var rawValue = GetNodeString(statusRoot?[propertyName]);
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var normalized = rawValue.Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return int.TryParse(normalized, out var parsed)
            ? Math.Clamp(parsed, 0, 100)
            : null;
    }

    private static bool IsRuinousWorldEventSignal(VisibleWorldEventSignal signal)
    {
        if (string.Equals(signal.Severity, "ruinous", StringComparison.OrdinalIgnoreCase))
            return true;

        if (signal.Entry?["tags"] is JsonArray tags)
        {
            return tags.OfType<JsonNode>()
                .Select(GetNodeString)
                .Any(tag => string.Equals(tag, "ruinous", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string DowngradeSeverityValue(string? severity)
    {
        return (severity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ruinous" => "severe",
            "catastrophic" => "ruinous",
            "severe" => "harsh",
            "harsh" => "moderate",
            _ => "severe"
        };
    }

    private static IEnumerable<JsonObject> EnumerateFactionCoreEntries(JsonNode? root)
    {
        if (root == null)
            yield break;

        if (root is JsonArray arrayRoot)
        {
            foreach (var entry in arrayRoot.OfType<JsonObject>())
                yield return entry;
            yield break;
        }

        if (root is not JsonObject objectRoot)
            yield break;

        if (objectRoot["factions"] is JsonArray factions)
        {
            foreach (var faction in factions.OfType<JsonObject>())
                yield return faction;
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(GetNodeString(objectRoot["factionId"])))
            yield return objectRoot;
    }

    private static bool HasFactionPlayerRelationCommit(JsonObject currentFaction, JsonObject? preTurnFaction)
    {
        var currentRelevantStrings = new[]
        {
            GetNodeString(currentFaction["playerRank"]) ?? string.Empty,
            GetNodeString(currentFaction["playerBranch"]) ?? string.Empty,
            GetNodeString(currentFaction["playerStrategyDirective"]) ?? string.Empty,
            GetNodeString(currentFaction["reputationDescription"]) ?? string.Empty
        };
        var preTurnRelevantStrings = preTurnFaction == null
            ? Array.Empty<string>()
            : new[]
            {
                GetNodeString(preTurnFaction["playerRank"]) ?? string.Empty,
                GetNodeString(preTurnFaction["playerBranch"]) ?? string.Empty,
                GetNodeString(preTurnFaction["playerStrategyDirective"]) ?? string.Empty,
                GetNodeString(preTurnFaction["reputationDescription"]) ?? string.Empty
            };

        if (!TryReadIntNode(currentFaction["reputation"], out var currentReputation))
            return false;

        if (preTurnFaction == null)
            return currentReputation != 0 || currentRelevantStrings.Any(value => !string.IsNullOrWhiteSpace(value));

        if (!TryReadIntNode(preTurnFaction["reputation"], out var preTurnReputation))
            preTurnReputation = 0;

        if (currentReputation != preTurnReputation)
            return true;

        for (var index = 0; index < currentRelevantStrings.Length; index++)
        {
            if (!string.Equals(
                    currentRelevantStrings[index],
                    index < preTurnRelevantStrings.Length ? preTurnRelevantStrings[index] : string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadIntNode(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<int>();
            return true;
        }
        catch
        {
            var raw = GetNodeString(node);
            return int.TryParse(raw, out value);
        }
    }

    private static IEnumerable<VisibleWorldEventSignal> EnumerateVisibleWorldEventSignals(JsonNode? root)
    {
        if (root == null)
            yield break;

        if (root is JsonArray arrayRoot)
        {
            foreach (var signal in EnumerateVisibleWorldEventSignals(arrayRoot))
                yield return signal;
            yield break;
        }

        if (root is not JsonObject objectRoot)
            yield break;

        if (objectRoot["events"] is JsonArray eventsArray)
        {
            foreach (var signal in EnumerateVisibleWorldEventSignals(eventsArray))
                yield return signal;
            yield break;
        }

        foreach (var property in objectRoot)
        {
            if (property.Value is JsonArray array)
            {
                foreach (var signal in EnumerateVisibleWorldEventSignals(array))
                    yield return signal;
            }
        }
    }

    private static IEnumerable<VisibleWorldEventSignal> EnumerateVisibleWorldEventSignals(JsonArray eventsArray)
    {
        foreach (var item in eventsArray.OfType<JsonObject>())
        {
            var visibility = GetNodeString(item["visibility"]);
            if (!IsPlayerVisibleWorldEventVisibility(visibility))
                continue;

            var eventId = GetNodeString(item["eventId"]);
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                yield return new VisibleWorldEventSignal(
                    eventId,
                    GetNodeString(item["anchorId"]),
                    GetNodeString(item["routeSeedId"]),
                    GetNodeString(item["severity"]) ?? GetNodeString(item["severityBand"]),
                    item.ContainsKey("severity")
                        ? "severity"
                        : item.ContainsKey("severityBand")
                            ? "severityBand"
                            : null,
                    item);
            }
        }
    }

    private static bool IsPlayerVisibleWorldEventVisibility(string? visibility) =>
        string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);

    private static JsonNode? ParseJsonNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<JsonObject> EnumerateSoulRelics(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is not JsonObject relicRoot)
            yield break;

        foreach (var collectionName in new[] { "equipped", "stored" })
        {
            if (relicRoot[collectionName] is not JsonArray collection)
                continue;

            foreach (var relic in collection.OfType<JsonObject>())
                yield return relic;
        }
    }

    private static IEnumerable<string> EnumerateManifestedCompanionKeys(JsonObject? npcRoot)
    {
        foreach (var signal in EnumerateManifestedCompanionSignals(npcRoot))
            yield return signal.UniqueKey;
    }

    private static List<ManifestedCompanionSignal> EnumerateManifestedCompanionSignals(JsonObject? npcRoot)
    {
        var result = new List<ManifestedCompanionSignal>();
        if (npcRoot == null)
            return result;

        foreach (var sectionName in GuardianPolicyContracts.ManifestedCompanionNpcCarrierSections)
        {
            if (npcRoot[sectionName] is not JsonArray npcs)
                continue;

            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var sourceRelicId = GetNodeString(npc["sourceCompanionRelicId"]) ?? string.Empty;
                var sourceResidentId = GetNodeString(npc["sourceAfterlifeResidentId"]) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceRelicId) && string.IsNullOrWhiteSpace(sourceResidentId))
                    continue;

                var npcId = GetNpcId(npc);
                var safeNpcId = string.IsNullOrWhiteSpace(npcId)
                    ? $"manifested_companion_{result.Count}"
                    : npcId;
                var npcName = GetNodeString(npc["NPCName"]) ??
                              GetNodeString(npc["name"]) ??
                              safeNpcId ??
                              "NPC";
                result.Add(new ManifestedCompanionSignal(
                    $"{sourceRelicId}|{sourceResidentId}|{safeNpcId}",
                    safeNpcId,
                    npcName,
                    sourceRelicId,
                    sourceResidentId));
            }
        }

        return result;
    }

    private sealed record ManifestedCompanionSignal(
        string UniqueKey,
        string NpcId,
        string NpcName,
        string SourceCompanionRelicId,
        string SourceAfterlifeResidentId);

    private sealed record VisibleWorldEventSignal(
        string EventId,
        string? AnchorId,
        string? RouteSeedId,
        string? Severity,
        string? SeverityFieldName,
        JsonObject? Entry)
    {
        public string RouteSeedUniqueKey => $"{RouteSeedId}|{EventId}";
    }

    private abstract record SocialCommitCandidate(
        string SortKey,
        string DisplayName);

    private sealed record SocialNpcCommitCandidate(
        string TargetNpcId,
        string TargetNpcName,
        JsonObject Npc,
        NpcRelationshipCommitSignal Signal)
        : SocialCommitCandidate(TargetNpcId, TargetNpcName);

    private sealed record SocialFactionCommitCandidate(
        string FactionId,
        string FactionName,
        JsonObject Faction,
        int CurrentReputation)
        : SocialCommitCandidate(FactionId, FactionName);

    private sealed record NpcRelationshipCommitSignal(
        string UniqueKey,
        string TargetNpcId,
        string TargetNpcName,
        int NewRelationshipLevel,
        string ChangeReason,
        JsonObject Entry);

    private static string GetNpcId(JsonObject npc) =>
        GetNodeString(npc["NPCId"]) ??
        GetNodeString(npc["npcId"]) ??
        GetNodeString(npc["id"]) ??
        string.Empty;

    private static IEnumerable<NpcRelationshipCommitSignal> EnumerateNpcRelationshipCommitSignals(JsonObject? root)
    {
        if (root?["NPCRelationshipChanges"] is not JsonArray changes)
            yield break;

        foreach (var entry in changes.OfType<JsonObject>())
        {
            var npcId = GetNodeString(entry["NPCId"]) ??
                        GetNodeString(entry["npcId"]) ??
                        GetNodeString(entry["id"]) ??
                        string.Empty;
            var npcName = GetNodeString(entry["NPCName"]) ??
                          GetNodeString(entry["npcName"]) ??
                          GetNodeString(entry["name"]) ??
                          npcId;
            if (string.IsNullOrWhiteSpace(npcId) && string.IsNullOrWhiteSpace(npcName))
                continue;

            var newRelationshipLevel = GetNodeInt(entry["newRelationshipLevel"], GetNodeInt(entry["relationshipLevel"], int.MinValue));
            var changeReason = GetNodeString(entry["changeReason"]) ?? string.Empty;
            yield return new NpcRelationshipCommitSignal(
                entry.ToJsonString(),
                string.IsNullOrWhiteSpace(npcId) ? npcName : npcId,
                npcName,
                newRelationshipLevel == int.MinValue ? 0 : newRelationshipLevel,
                changeReason,
                entry);
        }
    }

    private static string ResolveNpcAttitude(int relationshipLevel) => relationshipLevel switch
    {
        <= -201 => "Implacable Foe",
        <= -51 => "Adversary",
        <= -1 => "Dislike",
        <= 100 => "Neutral",
        <= 250 => "Familiarity & Trust",
        <= 350 => "Deep Bond",
        _ => "Legendary Bond"
    };

    private static void AddUniqueString(JsonArray array, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (array.OfType<JsonValue>().Any(node => node.TryGetValue<string>(out var existing) &&
                                                  string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        array.Add(value);
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path)
    {
        var json = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject BuildDefaultInventoryRoot()
    {
        return new JsonObject
        {
            ["items"] = new JsonArray(),
            ["equipment"] = new JsonObject
            {
                ["head"] = null,
                ["body"] = null,
                ["hands"] = null,
                ["feet"] = null,
                ["mainHand"] = null,
                ["offHand"] = null,
                ["neck"] = null,
                ["ring1"] = null,
                ["ring2"] = null
            },
            ["totalWeight"] = 0,
            ["maxWeight"] = 45,
            ["resources"] = new JsonObject()
        };
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node, int fallback)
    {
        if (node == null)
            return fallback;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return fallback;
        }
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node == null)
            return false;

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }
}
