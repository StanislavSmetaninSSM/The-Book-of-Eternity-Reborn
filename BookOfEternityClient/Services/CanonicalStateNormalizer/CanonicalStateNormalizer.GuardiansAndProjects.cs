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
        int currentTurn)
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

        if (result["activeGuardian"] is JsonObject resultActiveGuardian &&
            result["guardians"] is JsonArray resultGuardians &&
            resultGuardians.OfType<JsonObject>().FirstOrDefault(item =>
                string.Equals(GetNodeString(item["guardianId"]), GetNodeString(resultActiveGuardian["guardianId"]), StringComparison.OrdinalIgnoreCase)) is JsonObject syncedResultGuardian)
        {
            result["activeGuardian"] = syncedResultGuardian.DeepClone();
        }
        else if (currentRoot?["activeGuardian"] is JsonObject currentActiveGuardian &&
                 result["guardians"] is JsonArray currentAuthorityGuardians &&
                 currentAuthorityGuardians.OfType<JsonObject>().FirstOrDefault(item =>
                     string.Equals(GetNodeString(item["guardianId"]), GetNodeString(currentActiveGuardian["guardianId"]), StringComparison.OrdinalIgnoreCase)) is JsonObject syncedCurrentGuardian)
        {
            result["activeGuardian"] = syncedCurrentGuardian.DeepClone();
        }
        else
        {
            result.Remove("activeGuardian");
        }

        result.Remove("UpdateGuardians");
        result.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);
        result.Remove("guardianPowerEvents");

        return result;
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
        result.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);
        result.Remove("guardianPowerEvents");
        await WriteIfChangedAsync(path, currentNode, result);
        if (powerJournalEntries.Count > 0)
            await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, powerJournalEntries);
    }

    private async Task NormalizeGuardianAbodeResidentsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = GuardianAbodeResidentState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
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

        result.Remove(GuardianAbodeResidentState.UpdateProperty);
        result.Remove(GuardianAbodeResidentState.UpdateRosterReceiptsProperty);
        result.Remove(GuardianAbodeResidentState.UpdateInteractionReceiptsProperty);
        result.Remove(GuardianAbodeResidentState.UpdateHistoryLogProperty);
        result.Remove(GuardianAbodeResidentState.UpdateThoughtJournalProperty);
        result.Remove(GuardianAbodeResidentState.UpdateInteractionLogProperty);
        await WriteIfChangedAsync(path, currentNode, result);
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

    private async Task NormalizeGuardianProjectsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string guardiansPath = "game_state/meta/guardians.json";
        var currentNode = await ReadNodeAsync(GuardianProjectState.TrackerPath);
        var previous = await ReadBackupObjectAsync(GuardianProjectState.TrackerPath, backups);
        var previousGuardians = await ReadBackupObjectAsync(guardiansPath, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var currentObj = currentNode as JsonObject;

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
        var guardiansRoot = await ReadObjectAsync(guardiansPath);
        var soulStateRoot = await ReadObjectAsync("game_state/meta/soul_state.json");
        var currentIncarnation = GetNodeInt(soulStateRoot?["currentIncarnation"], 0);
        var currentRealm = GetNodeString(soulStateRoot?["currentRealm"]);
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

        if (currentNode == null && previous == null && activeProjects.Count == 0 && completedProjects.Count == 0 && temporaryModifiers.Count == 0)
            return;

        result["activeProjects"] = ToArray(activeProjects);
        result["completedProjects"] = ToArray(completedProjects);
        result["temporaryProjectModifiers"] = ToArray(temporaryModifiers);
        GuardianProjectState.ExpireLifeBoundEffects(result, currentIncarnation);
        result.Remove("startGuardianProjects");
        result.Remove("guardianProjectUpdates");
        result.Remove("completeGuardianProjects");

        await WriteIfChangedAsync(GuardianProjectState.TrackerPath, currentNode, result);
        if (journalEntries.Count > 0)
            await AppendGuardianProjectJournalEntriesAsync(journalEntries);

        if (guardiansRoot != null && pendingPowerEvents.Count > 0)
            guardiansChanged = GuardianPowerEventState.ApplyEvents(guardiansRoot, pendingPowerEvents, currentTurn, powerJournalEntries) || guardiansChanged;

        if (powerJournalEntries.Count > 0)
            await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, powerJournalEntries);
        else
            await GuardianPowerEventState.RepairJournalAsync(_fs);

        if (guardiansChanged && guardiansRoot != null)
            await _fs.WriteFileAtomicAsync(guardiansPath, guardiansRoot.ToJsonString(JsonOpts));
    }

}

