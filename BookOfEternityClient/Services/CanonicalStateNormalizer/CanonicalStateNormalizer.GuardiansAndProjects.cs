using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal static JsonObject BuildGuardianProjectAuthorityRootForValidation(
        JsonObject? preTurnTrackerRoot,
        JsonObject? currentTrackerRoot,
        JsonObject? preTurnGuardiansRoot,
        JsonObject? currentGuardiansRoot,
        int currentTurn,
        int currentIncarnation,
        string? currentRealm)
    {
        var activeProjects = new List<JsonObject>();
        var completedProjects = new List<JsonObject>();
        var temporaryModifiers = new List<JsonObject>();
        var projectedJournalEntries = new List<JsonObject>();
        var projectedPowerEvents = new List<JsonObject>();

        CollectGuardianProjectEntries(preTurnTrackerRoot, "activeProjects", activeProjects);
        CollectGuardianProjectEntries(preTurnTrackerRoot, "completedProjects", completedProjects);
        CollectGuardianProjectEntries(preTurnTrackerRoot, "temporaryProjectModifiers", temporaryModifiers);

        if (currentTrackerRoot != null)
        {
            if (currentTrackerRoot["startGuardianProjects"] is JsonArray startCommands)
                ApplyGuardianProjectStartCommands(
                    activeProjects,
                    completedProjects,
                    temporaryModifiers,
                    startCommands,
                    projectedJournalEntries,
                    currentTurn,
                    currentGuardiansRoot);

            if (currentTrackerRoot["guardianProjectUpdates"] is JsonArray updateCommands)
                ApplyGuardianProjectUpdateCommands(
                    activeProjects,
                    updateCommands,
                    projectedJournalEntries,
                    projectedPowerEvents,
                    currentTurn,
                    currentGuardiansRoot);

            if (currentTrackerRoot["completeGuardianProjects"] is JsonArray completionCommands)
            {
                var guardiansChanged = false;
                ApplyGuardianProjectCompletionCommands(
                    activeProjects,
                    completedProjects,
                    temporaryModifiers,
                    completionCommands,
                    projectedJournalEntries,
                    projectedPowerEvents,
                    currentTurn,
                    currentIncarnation,
                    currentRealm,
                    currentGuardiansRoot,
                    ref guardiansChanged);
            }
        }

        if (preTurnGuardiansRoot != null && currentGuardiansRoot != null)
        {
            ConsumeLoreResearchQuestTokens(completedProjects, preTurnGuardiansRoot, currentGuardiansRoot, currentIncarnation, new List<JsonObject>());
            ConsumeRelicForgingGachaUses(completedProjects, preTurnGuardiansRoot, currentGuardiansRoot, new List<JsonObject>());
        }

        if (currentGuardiansRoot != null && projectedPowerEvents.Count > 0)
        {
            GuardianPowerEventState.ApplyEvents(
                currentGuardiansRoot,
                projectedPowerEvents,
                currentTurn,
                projectedJournalEntries);

            if (currentGuardiansRoot["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians.OfType<JsonObject>())
                {
                    AbodePowerRules.EnsureCanonicalState(guardian);
                    GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                    GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
                }

                GuardianRelationshipRules.EnsureCanonicalNetwork(guardians);
            }

            if (currentGuardiansRoot["activeGuardian"] is JsonObject currentActiveGuardian &&
                currentGuardiansRoot["guardians"] is JsonArray currentGuardians &&
                currentGuardians.OfType<JsonObject>().FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["guardianId"]), GetNodeString(currentActiveGuardian["guardianId"]), StringComparison.OrdinalIgnoreCase)) is JsonObject syncedGuardian)
            {
                currentGuardiansRoot["activeGuardian"] = syncedGuardian.DeepClone();
            }
        }

        var authorityRoot = BuildGuardianProjectsTrackerRoot(activeProjects, completedProjects, temporaryModifiers);
        GuardianProjectState.ExpireLifeBoundEffects(authorityRoot, currentIncarnation);

        return authorityRoot;
    }

    internal static JsonObject BuildGuardianAuthorityRootForValidation(
        JsonObject? preTurnRoot,
        JsonObject? currentRoot,
        IReadOnlyCollection<JsonObject>? authorizedCommands,
        IReadOnlyDictionary<string, JsonObject>? authorizedCreateGuardiansById,
        IReadOnlyCollection<JsonObject>? authorizedPowerEvents,
        int currentTurn,
        IReadOnlyCollection<JsonObject>? authorizedQuestProgressUpdates = null)
    {
        var result = CloneObject(preTurnRoot ?? new JsonObject());
        var pendingPowerEvents = new List<JsonObject>();
        var powerJournalEntries = new List<JsonObject>();

        if (authorizedCommands is { Count: > 0 })
        {
            ApplyGuardianCommands(
                result,
                authorizedCommands,
                currentTurn,
                pendingPowerEvents,
                authorizedCreateGuardiansById);
        }

        if (authorizedPowerEvents != null)
            pendingPowerEvents.AddRange(authorizedPowerEvents.Select(CloneObject));

        if (pendingPowerEvents.Count > 0)
            GuardianPowerEventState.ApplyEvents(result, pendingPowerEvents, currentTurn, powerJournalEntries);

        if (authorizedQuestProgressUpdates is { Count: > 0 })
            ApplyGuardianQuestProgressAuthorityUpdates(result, authorizedQuestProgressUpdates);
        ApplyCurrentGuardianRootAuthoritySurfaces(result, currentRoot, authorizedCreateGuardiansById);

        if (result["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                AbodePowerRules.EnsureCanonicalState(guardian);
                GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
            }

            GuardianRelationshipRules.EnsureCanonicalNetwork(guardians);
        }

        SyncGuardianAuthorityActiveGuardian(result, currentRoot, authorizedCreateGuardiansById);

        result.Remove("UpdateGuardians");
        result.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);
        result.Remove("guardianPowerEvents");
        result.Remove(GuardianProjectState.QuestProgressUpdatesProperty);

        return result;
    }

    private static void ApplyGuardianQuestProgressAuthorityUpdates(
        JsonObject result,
        IReadOnlyCollection<JsonObject> authorizedQuestProgressUpdates)
    {
        if (authorizedQuestProgressUpdates.Count == 0 ||
            result["guardians"] is not JsonArray authorityGuardians)
        {
            return;
        }

        foreach (var update in authorizedQuestProgressUpdates)
        {
            if (!IsAllowedGuardianQuestProgressAuthorityState(update))
                continue;

            var guardianId = GetNodeString(update["guardianId"]);
            var questId = GetNodeString(update["questId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(questId))
                continue;

            var authorityGuardian = FindGuardian(authorityGuardians, guardianId!);
            if (authorityGuardian?["questManagement"] is not JsonObject authorityQuestManagement ||
                authorityQuestManagement["activeQuests"] is not JsonArray authorityActiveQuests)
            {
                continue;
            }

            var authorityQuest = authorityActiveQuests
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["questId"]), questId, StringComparison.OrdinalIgnoreCase));
            if (authorityQuest == null)
                continue;

            foreach (var fieldName in GuardianQuestProgressAuthorityMutableFields)
            {
                if (update.TryGetPropertyValue(fieldName, out var updateValue))
                    authorityQuest[fieldName] = updateValue?.DeepClone();
            }
        }
    }

    private static readonly string[] GuardianQuestProgressAuthorityMutableFields =
    {
        "status",
        "progressSummary",
        "objectiveState",
        "readyToTurnInEvidence",
        "turnInRequirement",
        "readyToTurnInAtTurn",
        "updatedAtTurn",
        "updatedAtUtc"
    };

    private static bool IsAllowedGuardianQuestProgressAuthorityState(JsonObject quest)
    {
        var status = GetNodeString(quest["status"]);
        if (!GuardianProjectState.IsSupportedActiveQuestProgressStatus(status))
            return false;

        var evidenceNode = quest["readyToTurnInEvidence"];
        if (evidenceNode != null && evidenceNode is not JsonObject)
            return false;
        var evidence = evidenceNode as JsonObject;
        if (evidence != null && GuardianProjectState.ContainsForbiddenQuestPhysicalEvidenceField(evidence))
            return false;

        if (!string.Equals(status, GuardianProjectState.QuestStatusReadyToTurnIn, StringComparison.OrdinalIgnoreCase))
            return true;

        if (evidence == null)
            return false;

        if (!GuardianQuestProgressAuthorityEvidenceHasAllowedProof(evidence))
            return false;

        return true;
    }

    private static bool GuardianQuestProgressAuthorityEvidenceHasAllowedProof(JsonObject evidence)
    {
        foreach (var fieldName in new[] { "memoryImprint", "lifeEventEvidence", "itemEcho", "locationWitness", "craftedOutcome", "knowledgeTrace", "soulResonance" })
        {
            var node = evidence[fieldName];
            if (node is JsonObject or JsonArray)
                return true;
            if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                return true;
        }

        return false;
    }

    private static void ApplyCurrentGuardianRootAuthoritySurfaces(
        JsonObject result,
        JsonObject? currentRoot,
        IReadOnlyDictionary<string, JsonObject>? authorizedCreateGuardiansById)
    {
        if (currentRoot == null)
            return;

        ApplyFoundationFormerPatronRootAuthoritySurface(result, currentRoot, authorizedCreateGuardiansById);

        if (currentRoot["chaosSeaNavigation"] is JsonObject navigation)
            result["chaosSeaNavigation"] = navigation.DeepClone();

        if (currentRoot[PlayerGuardianFoundationState.HistoryProperty] is JsonArray foundationHistory)
            result[PlayerGuardianFoundationState.HistoryProperty] = foundationHistory.DeepClone();
    }

    private static void ApplyFoundationFormerPatronRootAuthoritySurface(
        JsonObject result,
        JsonObject currentRoot,
        IReadOnlyDictionary<string, JsonObject>? authorizedCreateGuardiansById)
    {
        if (result["guardians"] is not JsonArray authorityGuardians ||
            currentRoot["guardians"] is not JsonArray currentGuardians ||
            currentRoot[PlayerGuardianFoundationState.HistoryProperty] is not JsonArray foundationHistory ||
            authorizedCreateGuardiansById is not { Count: > 0 })
        {
            return;
        }

        var formerPatronGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var historyEntry in foundationHistory.OfType<JsonObject>())
        {
            if (!IsAuthorizedFoundationCreateHistoryEntry(historyEntry, authorizedCreateGuardiansById))
                continue;

            var formerPatronGuardianId = GetNodeString(historyEntry["formerPatronGuardianId"]);
            if (!string.IsNullOrWhiteSpace(formerPatronGuardianId))
                formerPatronGuardianIds.Add(formerPatronGuardianId);
        }

        if (formerPatronGuardianIds.Count == 0)
            return;

        foreach (var currentGuardian in currentGuardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(currentGuardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                !formerPatronGuardianIds.Contains(guardianId) ||
                !string.Equals(
                    PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(currentGuardian),
                    PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                    StringComparison.OrdinalIgnoreCase) ||
                FindGuardian(authorityGuardians, guardianId) is not JsonObject authorityGuardian)
            {
                continue;
            }

            PlayerGuardianFoundationState.ApplyCanonicalFormerPatronSemantics(authorityGuardian);
        }
    }

    private static bool IsAuthorizedFoundationCreateHistoryEntry(
        JsonObject historyEntry,
        IReadOnlyDictionary<string, JsonObject> authorizedCreateGuardiansById)
    {
        var guardianId = GetNodeString(historyEntry["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId) ||
            !authorizedCreateGuardiansById.TryGetValue(guardianId!, out var createdGuardian))
        {
            return false;
        }

        if (!string.Equals(
                GetNodeString(createdGuardian["originType"]),
                PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestId = GetNodeString(historyEntry["requestId"]);
        if (!string.IsNullOrWhiteSpace(requestId) &&
            !string.Equals(
                GetNodeString(createdGuardian["foundationRequestId"]),
                requestId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var formerPatronGuardianId = GetNodeString(historyEntry["formerPatronGuardianId"]);
        return string.IsNullOrWhiteSpace(formerPatronGuardianId) ||
               string.Equals(
                   GetNodeString(createdGuardian["formerPatronGuardianId"]),
                   formerPatronGuardianId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void SyncGuardianAuthorityActiveGuardian(
        JsonObject result,
        JsonObject? currentRoot,
        IReadOnlyDictionary<string, JsonObject>? authorizedCreateGuardiansById)
    {
        if (result["guardians"] is not JsonArray guardians)
        {
            result.Remove("activeGuardian");
            return;
        }

        if (currentRoot?["activeGuardian"] is JsonObject currentActiveGuardian)
        {
            var currentActiveGuardianId = GetNodeString(currentActiveGuardian["guardianId"]);
            if (!string.IsNullOrWhiteSpace(currentActiveGuardianId) &&
                authorizedCreateGuardiansById?.ContainsKey(currentActiveGuardianId) == true &&
                FindGuardian(guardians, currentActiveGuardianId) is JsonObject syncedCurrentGuardian)
            {
                result["activeGuardian"] = syncedCurrentGuardian.DeepClone();
                return;
            }
        }

        if (result["activeGuardian"] is JsonObject resultActiveGuardian)
        {
            var resultActiveGuardianId = GetNodeString(resultActiveGuardian["guardianId"]);
            if (!string.IsNullOrWhiteSpace(resultActiveGuardianId) &&
                FindGuardian(guardians, resultActiveGuardianId) is JsonObject syncedResultGuardian)
            {
                result["activeGuardian"] = syncedResultGuardian.DeepClone();
                return;
            }
        }

        if (currentRoot?["activeGuardian"] is JsonObject fallbackCurrentActiveGuardian)
        {
            var currentActiveGuardianId = GetNodeString(fallbackCurrentActiveGuardian["guardianId"]);
            if (!string.IsNullOrWhiteSpace(currentActiveGuardianId) &&
                FindGuardian(guardians, currentActiveGuardianId) is JsonObject syncedCurrentGuardian)
            {
                result["activeGuardian"] = syncedCurrentGuardian.DeepClone();
                return;
            }
        }

        result.Remove("activeGuardian");
    }

    private async Task NormalizeGuardiansAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/guardians.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var currentTurn = await TryReadCurrentTurnNumberAsync();
        var pendingPowerEvents = new List<JsonObject>();
        var powerJournalEntries = new List<JsonObject>();
        var keepQuestProgressUpdates = false;

        if (currentNode is JsonObject currentObj)
        {
            MergeObject(result, currentObj);
            if (currentObj["guardians"] is JsonArray guardians)
                result["guardians"] = guardians.DeepClone();
            if (currentObj["activeGuardian"] is JsonObject activeGuardian)
                result["activeGuardian"] = activeGuardian.DeepClone();
            if (currentObj["chaosSeaNavigation"] is JsonObject nav)
                result["chaosSeaNavigation"] = nav.DeepClone();
            if (currentObj["pendingGuardianCreation"] is JsonObject pending)
                result["pendingGuardianCreation"] = pending.DeepClone();
            if (currentObj["UpdateGuardians"] is JsonArray updates)
                ApplyGuardianCommands(result, updates, currentTurn, pendingPowerEvents);
            if (currentObj.TryGetPropertyValue(GuardianProjectState.QuestProgressUpdatesProperty, out var questProgressUpdatesNode))
            {
                keepQuestProgressUpdates = questProgressUpdatesNode != null;
                if (questProgressUpdatesNode is JsonArray questProgressUpdates)
                    _ = TryApplyGuardianQuestProgressUpdates(result, questProgressUpdates, currentTurn);
            }
            if (currentObj["guardianPowerEvents"] is JsonArray powerEvents)
                pendingPowerEvents.AddRange(powerEvents.OfType<JsonObject>().Select(CloneObject));
        }
        else if (currentNode is JsonArray currentArray)
        {
            result["guardians"] = currentArray.DeepClone();
        }

        if (previous is JsonObject previousObj &&
            previousObj[GuardianTradeRequestState.UpdateReceiptsProperty] is JsonArray previousTradeReceiptUpdates)
        {
            GuardianTradeRequestState.ApplyReceiptUpdates(result, previousTradeReceiptUpdates);
        }

        if (currentNode is JsonObject currentRootObj &&
            currentRootObj[GuardianTradeRequestState.UpdateReceiptsProperty] is JsonArray currentTradeReceiptUpdates)
        {
            GuardianTradeRequestState.ApplyReceiptUpdates(result, currentTradeReceiptUpdates);
        }

        if (pendingPowerEvents.Count > 0)
            GuardianPowerEventState.ApplyEvents(result, pendingPowerEvents, currentTurn, powerJournalEntries);

        if (result["guardians"] is JsonArray normalizedGuardians)
        {
            foreach (var guardian in normalizedGuardians.OfType<JsonObject>())
            {
                AbodePowerRules.EnsureCanonicalState(guardian);
                GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
            }

            GuardianRelationshipRules.EnsureCanonicalNetwork(normalizedGuardians);
        }

        if (result["activeGuardian"] is JsonObject activeGuardianRoot)
        {
            if (result["guardians"] is JsonArray guardiansArray &&
                guardiansArray.OfType<JsonObject>().FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["guardianId"]), GetNodeString(activeGuardianRoot["guardianId"]), StringComparison.OrdinalIgnoreCase)) is JsonObject syncedGuardian)
            {
                result["activeGuardian"] = syncedGuardian.DeepClone();
            }
            else
            {
                result.Remove("activeGuardian");
            }
        }

        result.Remove("UpdateGuardians");
        if (!keepQuestProgressUpdates)
            result.Remove(GuardianProjectState.QuestProgressUpdatesProperty);
        result.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);
        result.Remove("guardianPowerEvents");
        await WriteIfChangedAsync(path, currentNode, result);
        if (powerJournalEntries.Count > 0)
            await AppendGuardianPowerJournalEntriesAsync(powerJournalEntries);
    }

    private async Task NormalizeGuardianAbodeResidentsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = GuardianAbodeResidentState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        JsonObject? currentTurnResidentsRoot = null;
        if (currentNode is JsonObject currentTurnResidentObject)
        {
            currentTurnResidentsRoot = CloneObject(currentTurnResidentObject);
            GuardianAbodeResidentState.NormalizeShape(currentTurnResidentsRoot);
        }

        var result = CloneObject(previous ?? new JsonObject());
        var entries = GuardianAbodeResidentState.EnsureEntriesArray(result);
        GuardianAbodeResidentState.EnsureRosterReceiptsArray(result);
        GuardianAbodeResidentState.EnsureInteractionReceiptsArray(result);
        GuardianAbodeResidentState.EnsureHistoryLogArray(result);
        GuardianAbodeResidentState.EnsureThoughtJournalArray(result);
        GuardianAbodeResidentState.EnsureInteractionLogArray(result);

        foreach (var resident in CollectGuardianAbodeResidentEntries(previous))
            GuardianAbodeResidentState.UpsertResident(entries, resident);
        foreach (var resident in CollectGuardianAbodeResidentEntries(currentNode))
            GuardianAbodeResidentState.UpsertResident(entries, resident);

        var previousRosterReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentRosterReceipts(previous))
            previousRosterReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyRosterReceiptUpdates(result, previousRosterReceipts);

        var currentRosterReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentRosterReceipts(currentNode))
            currentRosterReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyRosterReceiptUpdates(result, currentRosterReceipts);

        var previousInteractionReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentInteractionReceipts(previous))
            previousInteractionReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyInteractionReceiptUpdates(result, previousInteractionReceipts);

        var currentInteractionReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentInteractionReceipts(currentNode))
            currentInteractionReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyInteractionReceiptUpdates(result, currentInteractionReceipts);

        var previousTransferReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentTransferReceipts(previous))
            previousTransferReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyTransferReceiptUpdates(result, previousTransferReceipts);

        var currentTransferReceipts = new JsonArray();
        foreach (var receipt in CollectGuardianAbodeResidentTransferReceipts(currentNode))
            currentTransferReceipts.Add(receipt);
        GuardianAbodeResidentState.ApplyTransferReceiptUpdates(result, currentTransferReceipts);

        var previousHistoryLog = new JsonArray();
        foreach (var historyEntry in CollectGuardianAbodeResidentHistoryLogEntries(previous))
            previousHistoryLog.Add(historyEntry);
        GuardianAbodeResidentState.ApplyHistoryLogUpdates(result, previousHistoryLog);

        var currentHistoryLog = new JsonArray();
        foreach (var historyEntry in CollectGuardianAbodeResidentHistoryLogEntries(currentNode))
            currentHistoryLog.Add(historyEntry);
        GuardianAbodeResidentState.ApplyHistoryLogUpdates(result, currentHistoryLog);

        var previousThoughtJournal = new JsonArray();
        foreach (var entry in CollectGuardianAbodeResidentThoughtJournalEntries(previous))
            previousThoughtJournal.Add(entry);
        GuardianAbodeResidentState.ApplyThoughtJournalUpdates(result, previousThoughtJournal);

        var currentThoughtJournal = new JsonArray();
        foreach (var entry in CollectGuardianAbodeResidentThoughtJournalEntries(currentNode))
            currentThoughtJournal.Add(entry);
        GuardianAbodeResidentState.ApplyThoughtJournalUpdates(result, currentThoughtJournal);

        var previousInteractionLog = new JsonArray();
        foreach (var entry in CollectGuardianAbodeResidentInteractionLogEntries(previous))
            previousInteractionLog.Add(entry);
        GuardianAbodeResidentState.ApplyInteractionLogUpdates(result, previousInteractionLog);

        var currentInteractionLog = new JsonArray();
        foreach (var entry in CollectGuardianAbodeResidentInteractionLogEntries(currentNode))
            currentInteractionLog.Add(entry);
        GuardianAbodeResidentState.ApplyInteractionLogUpdates(result, currentInteractionLog);

        var previousGuardiansRoot = await ReadBackupObjectAsync("game_state/meta/guardians.json", backups);
        var previousGuardianPowerById = GuardianAbodeResidentState.CollectGuardianAbodePowerById(previousGuardiansRoot);
        var guardianPowerById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        JsonObject? currentGuardiansRoot = null;
        if (await ReadNodeAsync("game_state/meta/guardians.json") is JsonObject guardiansRoot)
        {
            currentGuardiansRoot = CloneObject(guardiansRoot);
            if (currentGuardiansRoot["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians.OfType<JsonObject>())
                {
                    var guardianId = guardian["guardianId"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(guardianId))
                        guardianPowerById[guardianId] = AbodePowerRules.GetCurrentPower(guardian);
                }
            }
        }

        JsonObject? shiningRoot = null;
        if (await ReadNodeAsync(ShiningAbodeState.StatePath) is JsonObject currentShiningRoot)
        {
            shiningRoot = CloneObject(currentShiningRoot);
            // Materialize active-guardian faction context before resident-side Shining normalization
            // so ascended residents do not lose affiliation when the faction exists only via guardian projection.
            ShiningAbodeState.NormalizeStateRoot(shiningRoot, result, currentGuardiansRoot);
        }

        foreach (var resident in entries.OfType<JsonObject>())
        {
            var guardianId = resident["guardianId"]?.GetValue<string>();
            GuardianAbodeResidentState.NormalizeResidentObject(
                resident,
                !string.IsNullOrWhiteSpace(guardianId) && guardianPowerById.TryGetValue(guardianId, out var currentAbodePower)
                    ? currentAbodePower
                    : null);
            ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot);
        }

        var previousSoulQuestJson = backups != null && backups.TryGetValue("game_state/quests/soul_quests.json", out var previousSoulQuestBackupPath)
            ? await ReadBackupTextAsync(previousSoulQuestBackupPath)
            : null;
        var currentSoulQuestJson = await ReadCanonicalFileAsync("game_state/quests/soul_quests.json");
        var previousQuestFingerprintsByResident = CollectResidentSoulQuestFingerprints(previousSoulQuestJson);
        var currentQuestFingerprintsByResident = CollectResidentSoulQuestFingerprints(currentSoulQuestJson);

        if (previous is JsonObject previousResidentsRoot && currentTurnResidentsRoot != null)
        {
            GuardianAbodeResidentState.NormalizeShape(previousResidentsRoot);
            foreach (var resident in entries.OfType<JsonObject>())
            {
                var residentId = resident["residentId"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(residentId))
                    continue;

                var previousResident = GuardianAbodeResidentState.FindResident(previousResidentsRoot, residentId);
                if (previousResident == null)
                    continue;

                var driftContext = GuardianAbodeResidentState.BuildCanonicalDriftContext(
                    previousResidentsRoot,
                    currentTurnResidentsRoot,
                    previousResident,
                    resident,
                    previousGuardianPowerById,
                    guardianPowerById,
                    previousQuestFingerprintsByResident,
                    currentQuestFingerprintsByResident);
                if (!driftContext.TouchesResidentTurnSurface)
                    continue;

                var projection = GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(previousResident, resident, driftContext);
                GuardianAbodeResidentState.ApplyAbodeDriftProjection(resident, projection);
                ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot);
            }
        }

        result.Remove(GuardianAbodeResidentState.UpdateProperty);
        result.Remove(GuardianAbodeResidentState.UpdateRosterReceiptsProperty);
        result.Remove(GuardianAbodeResidentState.UpdateInteractionReceiptsProperty);
        result.Remove(GuardianAbodeResidentState.UpdateTransferReceiptsProperty);
        result.Remove(GuardianAbodeResidentState.UpdateHistoryLogProperty);
        result.Remove(GuardianAbodeResidentState.UpdateThoughtJournalProperty);
        result.Remove(GuardianAbodeResidentState.UpdateInteractionLogProperty);
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private static Dictionary<string, Dictionary<string, string>> CollectResidentSoulQuestFingerprints(string? soulQuestJson)
    {
        static string GetQuestString(JsonElement quest, string propertyName)
        {
            return quest.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(soulQuestJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(soulQuestJson);
            JsonElement questsArray = default;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("quests", out var quests) &&
                quests.ValueKind == JsonValueKind.Array)
            {
                questsArray = quests;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                     doc.RootElement.TryGetProperty("UpdateSoulQuests", out var updates) &&
                     updates.ValueKind == JsonValueKind.Array)
            {
                questsArray = updates;
            }

            if (questsArray.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var quest in questsArray.EnumerateArray())
            {
                if (quest.ValueKind != JsonValueKind.Object)
                    continue;

                var residentId = GetQuestString(quest, "relatedAfterlifeResidentId");
                var questId = GetQuestString(quest, "questId");
                if (string.IsNullOrWhiteSpace(questId))
                    questId = GetQuestString(quest, "id");
                if (string.IsNullOrWhiteSpace(residentId) || string.IsNullOrWhiteSpace(questId))
                    continue;

                if (!result.TryGetValue(residentId, out var residentFingerprints))
                {
                    residentFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[residentId] = residentFingerprints;
                }

                residentFingerprints[questId] = quest.GetRawText();
            }
        }
        catch
        {
            // ignored; generic validation reports malformed quest state elsewhere
        }

        return result;
    }

    private async Task NormalizeCharacterChronicleAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/character_chronicle.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = EnsureArray(result, "entries");

        foreach (var entry in CollectChronicleEntries(previous))
            AddUniqueNode(entries, entry);
        foreach (var entry in CollectChronicleEntries(currentNode))
            AddUniqueNode(entries, entry);

        result.Remove("characterChronicleUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeGuardianProjectsAsync(GuardianProjectNormalizationInputs inputs)
    {
        var currentObj = inputs.CurrentTrackerRoot;
        var previous = inputs.PreviousTrackerRoot;
        var previousGuardians = inputs.PreviousGuardiansRoot;

        var result = CloneObject(previous ?? new JsonObject());

        var activeProjects = new List<JsonObject>();
        var completedProjects = new List<JsonObject>();
        var temporaryModifiers = new List<JsonObject>();

        CollectGuardianProjectEntries(previous, "activeProjects", activeProjects);
        CollectGuardianProjectEntries(previous, "completedProjects", completedProjects);
        CollectGuardianProjectEntries(previous, "temporaryProjectModifiers", temporaryModifiers);

        var currentTurn = await TryReadCurrentTurnNumberAsync();
        var journalEntries = new List<JsonObject>();
        var powerJournalEntries = new List<JsonObject>();
        var pendingPowerEvents = new List<JsonObject>();
        var guardiansRoot = await ReadCurrentGuardianProjectGuardiansRootAsync(inputs.RequiresReadableCurrentGuardians);
        var currentIncarnation = inputs.CurrentIncarnation;
        var currentRealm = inputs.CurrentRealm;
        var guardiansChanged = false;

        if (currentObj != null)
        {
            if (currentObj["startGuardianProjects"] is JsonArray startCommands)
            {
                ApplyGuardianProjectStartCommands(activeProjects, completedProjects, temporaryModifiers, startCommands, journalEntries, currentTurn, guardiansRoot);
            }

            if (currentObj["guardianProjectUpdates"] is JsonArray updateCommands)
            {
                ApplyGuardianProjectUpdateCommands(activeProjects, updateCommands, journalEntries, pendingPowerEvents, currentTurn, guardiansRoot);
            }

            if (currentObj["completeGuardianProjects"] is JsonArray completionCommands)
            {
                ApplyGuardianProjectCompletionCommands(
                    activeProjects,
                    completedProjects,
                    temporaryModifiers,
                    completionCommands,
                    journalEntries,
                    pendingPowerEvents,
                    currentTurn,
                    currentIncarnation,
                    currentRealm,
                    guardiansRoot,
                    ref guardiansChanged);
            }
        }

        if (guardiansRoot != null)
        {
            ConsumeLoreResearchQuestTokens(completedProjects, previousGuardians, guardiansRoot, currentIncarnation, journalEntries);
            ConsumeRelicForgingGachaUses(completedProjects, previousGuardians, guardiansRoot, journalEntries);
        }

        if (currentObj == null && previous == null && activeProjects.Count == 0 && completedProjects.Count == 0 && temporaryModifiers.Count == 0)
            return;

        result["activeProjects"] = ToArray(activeProjects);
        result["completedProjects"] = ToArray(completedProjects);
        result["temporaryProjectModifiers"] = ToArray(temporaryModifiers);
        GuardianProjectState.ExpireLifeBoundEffects(result, currentIncarnation);
        result.Remove("startGuardianProjects");
        result.Remove("guardianProjectUpdates");
        result.Remove("completeGuardianProjects");

        await WriteIfChangedAsync(GuardianProjectState.TrackerPath, currentObj, result);
        if (journalEntries.Count > 0)
            await AppendGuardianProjectJournalEntriesAsync(journalEntries);

        if (guardiansRoot != null && pendingPowerEvents.Count > 0)
            guardiansChanged = GuardianPowerEventState.ApplyEvents(guardiansRoot, pendingPowerEvents, currentTurn, powerJournalEntries) || guardiansChanged;

        if (powerJournalEntries.Count > 0)
            await AppendGuardianPowerJournalEntriesAsync(powerJournalEntries);
        else
            await RepairGuardianPowerJournalAsync();

        if (guardiansChanged && guardiansRoot != null)
            await WriteCanonicalFileAtomicAsync(GuardiansStatePath, guardiansRoot.ToJsonString(JsonOpts));
    }

    private async Task AppendGuardianPowerJournalEntriesAsync(
        IEnumerable<JsonObject> entries)
    {
        var buffered = entries.Where(static item => item != null).ToList();
        if (buffered.Count == 0)
            return;

        if (_writeLease == null)
        {
            await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, buffered);
            return;
        }

        JsonObject root;
        var existing = await ReadCanonicalFileAsync(GuardianPowerEventState.JournalPath);
        try
        {
            root = string.IsNullOrWhiteSpace(existing)
                ? new JsonObject()
                : JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        var journal = root["entries"] as JsonArray ?? new JsonArray();
        root["entries"] = journal;
        foreach (var entry in buffered)
        {
            var eventId = GetNodeString(entry["eventId"]);
            if (journal.OfType<JsonObject>().Any(existingEntry =>
                    string.Equals(
                        GetNodeString(existingEntry["eventId"]),
                        eventId,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            journal.Add(entry.DeepClone());
        }

        await WriteCanonicalFileAtomicAsync(
            GuardianPowerEventState.JournalPath,
            root.ToJsonString(JsonOpts));
    }

    private async Task RepairGuardianPowerJournalAsync()
    {
        if (_writeLease == null)
        {
            await GuardianPowerEventState.RepairJournalAsync(_fs);
            return;
        }

        // Browser QTE normalization preserves existing journal entries under the
        // same canonical lease. Political legacy backfill remains a turn-level
        // repair concern and must not reacquire canonical authority here.
        _ = await ReadCanonicalFileAsync(GuardianPowerEventState.JournalPath);
    }

    private static bool HasGuardianProjectCommandPayload(JsonObject? trackerRoot)
    {
        return HasCommandEntries(trackerRoot, "startGuardianProjects") ||
               HasCommandEntries(trackerRoot, "guardianProjectUpdates") ||
               HasCommandEntries(trackerRoot, "completeGuardianProjects");
    }

    private static bool HasCommandEntries(JsonObject? trackerRoot, string propertyName)
    {
        return trackerRoot?[propertyName] is JsonArray commands && commands.OfType<JsonObject>().Any();
    }

    private static bool HasGuardianProjectGuardianSideConsumption(
        IEnumerable<JsonObject> completedProjects,
        int currentIncarnation,
        string? currentRealm)
    {
        foreach (var completedProject in completedProjects)
        {
            if (GuardianProjectState.RequiresCurrentGuardianSideReconciliation(completedProject, currentIncarnation, currentRealm))
                return true;
        }

        return false;
    }

    private static GuardianProjectSoulContextRequirements GetGuardianProjectSoulContextRequirementsForCompletions(
        JsonObject? currentTrackerRoot,
        JsonObject? previousTrackerRoot)
    {
        var requirements = default(GuardianProjectSoulContextRequirements);
        if (currentTrackerRoot?["completeGuardianProjects"] is not JsonArray completionCommands)
            return requirements;

        var activeProjects = new List<JsonObject>();
        CollectGuardianProjectEntries(previousTrackerRoot, "activeProjects", activeProjects);

        foreach (var command in completionCommands.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(command["guardianId"]);
            var projectId = GetNodeString(command["projectId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var existing = activeProjects.FirstOrDefault(item =>
                string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GuardianProjectState.GetProjectId(item), projectId, StringComparison.OrdinalIgnoreCase));
            if (existing?["project"] is not JsonObject project)
                continue;

            requirements = requirements.Merge(GuardianProjectState.GetCurrentSoulContextRequirementsForNormalization(project));
            if (requirements.RequiresCurrentIncarnation && requirements.RequiresCurrentRealm)
                return requirements;
        }

        return requirements;
    }

    private static GuardianProjectSoulContextRequirements GetGuardianProjectSoulContextRequirementsForCompletedProjects(JsonObject? previousTrackerRoot)
    {
        var requirements = default(GuardianProjectSoulContextRequirements);
        var completedProjects = new List<JsonObject>();
        CollectGuardianProjectEntries(previousTrackerRoot, "completedProjects", completedProjects);

        foreach (var item in completedProjects)
        {
            requirements = requirements.Merge(
                GuardianProjectState.GetCurrentSoulContextRequirementsForCompletedProjectNormalization(item["project"] as JsonObject));
            if (requirements.RequiresCurrentIncarnation && requirements.RequiresCurrentRealm)
                return requirements;
        }

        return requirements;
    }

    internal static GuardianProjectSoulContextRequirements ResolveRequiredCurrentGuardianProjectSoulContext(
        JsonObject? currentTrackerRoot,
        JsonObject? previousTrackerRoot)
    {
        return GetGuardianProjectSoulContextRequirementsForCompletions(currentTrackerRoot, previousTrackerRoot)
            .Merge(GetGuardianProjectSoulContextRequirementsForCompletedProjects(previousTrackerRoot));
    }

    private static bool RequiresReadableCurrentGuardianProjectGuardians(
        JsonObject? currentTrackerRoot,
        JsonObject? previousTrackerRoot,
        int currentIncarnation,
        string? currentRealm)
    {
        if (HasGuardianProjectCommandPayload(currentTrackerRoot))
            return true;

        var completedProjects = new List<JsonObject>();
        CollectGuardianProjectEntries(previousTrackerRoot, "completedProjects", completedProjects);
        return HasGuardianProjectGuardianSideConsumption(completedProjects, currentIncarnation, currentRealm);
    }

}

