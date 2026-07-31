using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeQuestHistoryAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/quests/quest_history.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var questHistory = new List<JsonObject>();
        foreach (var quest in CollectQuestHistoryEntries(previous).OfType<JsonObject>())
            UpsertByIdentity(questHistory, quest, "questId", "questName", "title", "name");
        foreach (var quest in CollectQuestHistoryEntries(currentNode).OfType<JsonObject>())
            UpsertByIdentity(questHistory, quest, "questId", "questName", "title", "name");

        var questRewards = new List<JsonObject>();
        CollectNamedObjectEntries(previous, "questRewards", questRewards);
        CollectNamedObjectEntries(currentNode, "questRewards", questRewards);

        var questChains = new List<JsonObject>();
        CollectNamedObjectEntries(previous, "questChains", questChains);
        CollectNamedObjectEntries(currentNode, "questChains", questChains);

        result["questHistory"] = ToArray(questHistory);
        if (questRewards.Count > 0)
            result["questRewards"] = ToArray(questRewards);
        else
            result.Remove("questRewards");
        if (questChains.Count > 0)
            result["questChains"] = ToArray(questChains);
        else
            result.Remove("questChains");
        result.Remove("questLog");
        result.Remove("quests");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeQuestStateAsync(string path, string updateProp, IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var quests = EnsureArray(result, "quests");

        foreach (var quest in CollectQuestStateEntries(previous, updateProp))
            UpsertQuestByIdentity(quests, quest);
        foreach (var quest in CollectQuestStateEntries(currentNode, updateProp))
            UpsertQuestByIdentity(quests, quest);

        result.Remove(updateProp);
        await WriteIfChangedAsync(path, currentNode, result);
    }


    private async Task NormalizeRivalSoulArcsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = RivalSoulArcService.StatePath;
        var currentNode = await ReadCurrentAuthorityNodeAsync(
            path,
            required: CanonicalFileExists(path),
            RivalSoulArcCurrentStateReadableRequiredMessage);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var arcs = EnsureArray(result, "arcs");
        var trackerRoot = await ReadObjectAsync(GuardianProjectState.TrackerPath);
        var currentTurn = await TryReadCurrentTurnNumberAsync();
        var projectJournalEntries = new List<JsonObject>();
        const string worldEventsPath = "game_state/world/world_events.json";
        var previousWorldEvents = await ReadBackupNodeAsync(worldEventsPath, backups);

        foreach (var arc in CollectRivalSoulArcEntries(previous))
            UpsertByIdentity(arcs, arc, "arcId");
        foreach (var arc in CollectRivalSoulArcEntries(currentNode))
            UpsertByIdentity(arcs, arc, "arcId");

        var rawCurrentWorldEventsJson = CanonicalFileExists(worldEventsPath)
            ? await ReadCanonicalFileAsync(worldEventsPath)
            : null;
        var currentWorldEventsFileExists = CanonicalFileExists(worldEventsPath);
        var currentWorldEvents = default(JsonNode);
        var currentIncarnation = 0;
        var resolvedCurrentIncarnation = false;
        var requiresCurrentIncarnationPreflight =
            RequiresCurrentIncarnationForVisibleRivalCluePreflight(
                result,
                trackerRoot,
                currentWorldEventsFileExists,
                rawCurrentWorldEventsJson);
        if (trackerRoot != null && requiresCurrentIncarnationPreflight)
        {
            var currentSoulStateRoot = await ReadCurrentGuardianProjectSoulStateRootAsync(required: true);
            (currentIncarnation, _) = await ReadEffectiveGuardianProjectSoulContextAsync(
                backups,
                new GuardianProjectSoulContextRequirements(
                    RequiresCurrentIncarnation: true,
                    RequiresCurrentRealm: false),
                currentSoulStateRoot);
            resolvedCurrentIncarnation = true;

            if (RequiresCurrentWorldEventsForVisibleRivalClueConsumption(
                    result,
                    trackerRoot,
                    currentIncarnation,
                    rawCurrentWorldEventsJson))
            {
                currentWorldEvents = await ReadCurrentAuthorityNodeAsync(
                    worldEventsPath,
                    required: true,
                    RivalWorldEventsCurrentStateReadableRequiredMessage);
            }
        }

        var requiresCurrentIncarnation =
            trackerRoot != null &&
            RequiresCurrentIncarnationForVisibleRivalClueConsumption(previous, result, previousWorldEvents, currentWorldEvents);
        if (requiresCurrentIncarnation && !resolvedCurrentIncarnation)
        {
            var currentSoulStateRoot = await ReadCurrentGuardianProjectSoulStateRootAsync(required: true);
            (currentIncarnation, _) = await ReadEffectiveGuardianProjectSoulContextAsync(
                backups,
                new GuardianProjectSoulContextRequirements(
                    RequiresCurrentIncarnation: true,
                    RequiresCurrentRealm: false),
                currentSoulStateRoot);
        }

        result.Remove("UpdateRivalSoulArcs");
        await WriteIfChangedAsync(path, currentNode, result);

        var worldEventsChanged = false;
        if (trackerRoot != null &&
            requiresCurrentIncarnation &&
            currentIncarnation > 0 &&
            ConsumeLoreResearchVisibleRivalClues(previous, result, previousWorldEvents, currentWorldEvents, trackerRoot, currentIncarnation, currentTurn, projectJournalEntries, out worldEventsChanged))
        {
            await WriteCanonicalFileAtomicAsync(GuardianProjectState.TrackerPath, trackerRoot.ToJsonString(JsonOpts));
            await WriteCanonicalFileAtomicAsync(path, result.ToJsonString(JsonOpts));
            if (worldEventsChanged && currentWorldEvents != null)
                await WriteCanonicalFileAtomicAsync(worldEventsPath, currentWorldEvents.ToJsonString(JsonOpts));
        }

        if (projectJournalEntries.Count > 0)
            await AppendGuardianProjectJournalEntriesAsync(projectJournalEntries);
    }

    internal static bool RawJsonMayContainRelatedRivalWorldEvents(string? rawJson, IEnumerable<string> relatedArcIds, bool requireBonusClueSource)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        if (!rawJson.Contains("relatedRivalArcId", StringComparison.OrdinalIgnoreCase))
            return false;

        if (requireBonusClueSource &&
            !rawJson.Contains("bonusClueSourceProjectId", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var arcId in relatedArcIds)
        {
            if (!string.IsNullOrWhiteSpace(arcId) &&
                rawJson.Contains(arcId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTrimmedWorldEventContainerJson(string? rawJson, out string trimmed)
    {
        trimmed = string.Empty;
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        trimmed = rawJson.Trim();
        if (trimmed.Length <= 1)
            return false;

        return trimmed.StartsWith("[", StringComparison.Ordinal) ||
               trimmed.Contains("\"worldEventsLog\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"events\"", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RawJsonMayContainCurrentWorldEventSurface(string? rawJson)
    {
        if (!TryGetTrimmedWorldEventContainerJson(rawJson, out _))
            return false;

        // When the current pass has no visible sponsored public-signal clue surface, any
        // malformed current world-events container is potentially authority-relevant.
        return true;
    }

    internal static bool RawJsonMayContainCurrentLinkedWorldEventBonusClueSurface(string? rawJson)
    {
        if (!TryGetTrimmedWorldEventContainerJson(rawJson, out var trimmed))
            return false;

        return trimmed.Contains("\"relatedRivalArcId\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"bonusClueSourceProjectId\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"bonusClueRevealId\"", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] RelevantCurrentWorldEventFieldNames =
    {
        "eventId",
        "eventTitle",
        "title",
        "summary",
        "description",
        "visibility",
        "relatedRivalArcId",
        "bonusClueSourceProjectId",
        "bonusClueRevealId"
    };

    private static bool RawJsonMayContainCurrentWorldEventCandidateSurface(string? rawJson)
    {
        if (!TryGetTrimmedWorldEventContainerJson(rawJson, out var trimmed))
            return false;

        return trimmed.Contains("\"eventId\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"eventTitle\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"title\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"summary\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"description\"", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("\"visibility\"", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RawJsonMayContainCurrentWorldEventStartedEntrySurface(string? rawJson)
    {
        if (!TryGetTrimmedWorldEventContainerJson(rawJson, out var trimmed))
            return false;

        var arrayStart = trimmed.IndexOf('[', StringComparison.Ordinal);
        if (arrayStart < 0)
            return false;

        var index = arrayStart + 1;
        while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
            index++;

        if (index >= trimmed.Length || trimmed[index] != '{')
            return false;

        index++;
        while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
            index++;

        // Treat both a bare/truncated event object start and a partially typed
        // relevant field key as authority-relevant on a semantically required
        // current-pass path. This closes the remaining fail-open where malformed
        // current world-events stop after entering the event object but before a
        // full candidate/clue-marker field name is present.
        if (index >= trimmed.Length)
            return true;

        var objectTail = trimmed[index..].TrimEnd();
        if (objectTail.Length == 0)
            return true;

        var lastQuote = objectTail.LastIndexOf('"');
        if (lastQuote < 0)
            return false;

        var partialField = objectTail[(lastQuote + 1)..];
        if (partialField.Length == 0)
            return true;

        if (partialField.IndexOfAny(['"', ':', ',', '{', '}', '[', ']']) >= 0)
            return false;

        return RelevantCurrentWorldEventFieldNames.Any(name =>
            name.StartsWith(partialField, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> CollectVisibleSponsoredPublicSignalBonusClueUsage(JsonObject currentArcsRoot)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return result;

        var countedVisibleBonusClueRevealKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var arcId = GetNodeString(arc["arcId"]);
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(sponsorGuardianId) ||
                arc["publicSignals"] is not JsonArray publicSignals)
            {
                continue;
            }

            foreach (var signal in publicSignals.OfType<JsonObject>())
            {
                var sourceProjectId = GetNodeString(signal["bonusClueSourceProjectId"]);
                if (!GetJsonBool(signal["visibleToPlayer"]) ||
                    string.IsNullOrWhiteSpace(sourceProjectId))
                {
                    continue;
                }

                var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                if (!countedVisibleBonusClueRevealKeys.Add(revealKey))
                    continue;

                var clueCost = Math.Max(1, GetNodeInt(signal["bonusClueCost"], 1));
                var usageKey = $"{sponsorGuardianId}::{sourceProjectId}";
                result[usageKey] = result.GetValueOrDefault(usageKey) + clueCost;
            }
        }

        return result;
    }

    private static bool HasVisibleSponsoredPublicSignalBonusClueSurface(JsonObject currentArcsRoot) =>
        CollectVisibleSponsoredPublicSignalBonusClueUsage(currentArcsRoot).Count > 0;

    internal static bool RequiresCurrentIncarnationForVisibleRivalCluePreflight(
        JsonObject currentArcsRoot,
        JsonObject? trackerRoot,
        bool hasCurrentWorldEventsFile,
        string? rawCurrentWorldEventsJson)
    {
        if (!HasSponsoredVisibleRivalClueBudgetCandidate(currentArcsRoot, trackerRoot))
            return false;

        if (HasVisibleSponsoredPublicSignalBonusClueSurface(currentArcsRoot))
            return true;

        return CurrentPassMayHaveLinkedWorldEventBonusClueSurfaceForPreflight(
            currentArcsRoot,
            hasCurrentWorldEventsFile,
            rawCurrentWorldEventsJson);
    }

    private static HashSet<string> CollectGuardianSponsoredGuardianIds(JsonObject currentArcsRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return result;

        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (!string.IsNullOrWhiteSpace(sponsorGuardianId))
                result.Add(sponsorGuardianId!);
        }

        return result;
    }

    internal static bool HasSponsoredVisibleRivalClueBudgetCandidate(JsonObject currentArcsRoot, JsonObject? trackerRoot)
    {
        if (trackerRoot?["completedProjects"] is not JsonArray completedProjects)
            return false;

        var sponsoredGuardianIds = CollectGuardianSponsoredGuardianIds(currentArcsRoot);
        if (sponsoredGuardianIds.Count == 0)
            return false;

        foreach (var entry in completedProjects.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                !sponsoredGuardianIds.Contains(guardianId!) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetNodeString(project["projectType"]), "lore_research", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (GetPotentialVisibleRivalClueBudget(project) > 0)
                return true;
        }

        return false;
    }

    private static int GetPotentialVisibleRivalClueBudget(JsonObject project)
    {
        var effectState = project["effectState"] as JsonObject;
        var audit = project["projectOutcomeAudit"] as JsonObject;
        var grantedFromEffectState = GetNodeInt(effectState?["visibleRivalClueBudgetGranted"]);
        var grantedFromAudit = GetNodeInt(audit?["visibleRivalClueBonus"]);
        return Math.Max(grantedFromEffectState, grantedFromAudit);
    }

    private static int GetRemainingVisibleRivalClueBudgetForCurrentLife(JsonObject project, int currentIncarnation)
    {
        if (!string.Equals(GetNodeString(project["projectType"]), "lore_research", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(project["finalState"]), "Completed", StringComparison.OrdinalIgnoreCase) ||
            project["effectState"] is not JsonObject effectState ||
            GetNodeInt(effectState["targetIncarnation"]) != currentIncarnation)
        {
            return 0;
        }

        var granted = GuardianProjectState.GetGrantedVisibleRivalClueBudgetForCurrentLife(project, currentIncarnation);
        var spent = GetNodeInt(effectState["visibleRivalClueBudgetSpent"]);
        return Math.Max(0, granted - spent);
    }

    private static bool CurrentPassMayNeedLinkedWorldEventBonusClueSurface(
        JsonObject currentArcsRoot,
        JsonObject? trackerRoot,
        int currentIncarnation)
    {
        if (trackerRoot == null ||
            currentIncarnation <= 0 ||
            trackerRoot["completedProjects"] is not JsonArray completedProjects)
        {
            return false;
        }

        var sponsoredGuardianIds = CollectGuardianSponsoredGuardianIds(currentArcsRoot);
        if (sponsoredGuardianIds.Count == 0)
            return false;

        var visiblePublicSignalUsage = CollectVisibleSponsoredPublicSignalBonusClueUsage(currentArcsRoot);
        foreach (var entry in completedProjects.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                !sponsoredGuardianIds.Contains(guardianId!) ||
                entry["project"] is not JsonObject project)
            {
                continue;
            }

            var sourceProjectId = GetNodeString(project["projectId"]);
            if (string.IsNullOrWhiteSpace(sourceProjectId))
                continue;

            var remainingBudget = GetRemainingVisibleRivalClueBudgetForCurrentLife(project, currentIncarnation);
            if (remainingBudget <= 0)
                continue;

            var usageKey = $"{guardianId}::{sourceProjectId}";
            var visibleUsage = visiblePublicSignalUsage.GetValueOrDefault(usageKey);
            // Equality still needs strict current-world handling: visible publicSignals may
            // already occupy the full remaining budget while malformed current world-events
            // hide one more linked clue that would otherwise escape fail-closed validation.
            if ((visibleUsage == 0 && remainingBudget > 0) ||
                (visibleUsage > 0 && remainingBudget >= visibleUsage))
                return true;
        }

        return false;
    }

    private static bool CurrentPassMayHaveLinkedWorldEventBonusClueSurfaceForPreflight(
        JsonObject currentArcsRoot,
        bool hasCurrentWorldEventsFile,
        string? rawCurrentWorldEventsJson)
    {
        if (!hasCurrentWorldEventsFile || string.IsNullOrWhiteSpace(rawCurrentWorldEventsJson))
            return true;

        if (TryParseCurrentWorldEventsRoot(rawCurrentWorldEventsJson) is JsonNode currentWorldEventsRoot)
            return CurrentWorldEventsContainVisibleLinkedBonusClueSurface(currentArcsRoot, currentWorldEventsRoot);

        return RawJsonMayContainCurrentLinkedWorldEventBonusClueSurface(rawCurrentWorldEventsJson) ||
               RawJsonMayContainCurrentWorldEventCandidateSurface(rawCurrentWorldEventsJson) ||
               RawJsonMayContainCurrentWorldEventStartedEntrySurface(rawCurrentWorldEventsJson);
    }

    private static JsonNode? TryParseCurrentWorldEventsRoot(string? rawCurrentWorldEventsJson)
    {
        if (string.IsNullOrWhiteSpace(rawCurrentWorldEventsJson))
            return null;

        try
        {
            return JsonNode.Parse(rawCurrentWorldEventsJson);
        }
        catch
        {
            return null;
        }
    }

    private static bool CurrentWorldEventsContainVisibleLinkedBonusClueSurface(
        JsonObject currentArcsRoot,
        JsonNode? currentWorldEventsRoot)
    {
        if (currentWorldEventsRoot == null)
            return false;

        var sponsoredArcIds = CollectGuardianSponsoredRivalArcIds(currentArcsRoot);
        if (sponsoredArcIds.Count == 0)
            return false;

        foreach (var worldEvent in EnumerateWorldEventObjects(currentWorldEventsRoot))
        {
            var relatedArcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            var sourceProjectId = GetNodeString(worldEvent["bonusClueSourceProjectId"]);
            if (string.IsNullOrWhiteSpace(relatedArcId) ||
                string.IsNullOrWhiteSpace(sourceProjectId) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent) ||
                !sponsoredArcIds.Contains(relatedArcId!))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal static bool RequiresCurrentWorldEventsForVisibleRivalClueConsumption(
        JsonObject currentArcsRoot,
        JsonObject? trackerRoot,
        int currentIncarnation,
        string? rawCurrentWorldEventsJson)
    {
        var currentPassMayNeedLinkedWorldEvents = CurrentPassMayNeedLinkedWorldEventBonusClueSurface(
            currentArcsRoot,
            trackerRoot,
            currentIncarnation);
        if (!currentPassMayNeedLinkedWorldEvents)
            return false;

        if (string.IsNullOrWhiteSpace(rawCurrentWorldEventsJson))
            return true;

        if (!RawJsonMayContainCurrentWorldEventSurface(rawCurrentWorldEventsJson))
            return false;

        if (RawJsonMayContainCurrentLinkedWorldEventBonusClueSurface(rawCurrentWorldEventsJson))
            return true;

        return RawJsonMayContainCurrentWorldEventCandidateSurface(rawCurrentWorldEventsJson) ||
               RawJsonMayContainCurrentWorldEventStartedEntrySurface(rawCurrentWorldEventsJson);
    }

    private static bool RequiresCurrentIncarnationForVisibleRivalClueConsumption(
        JsonObject? previousArcsRoot,
        JsonObject currentArcsRoot,
        JsonNode? previousWorldEventsRoot,
        JsonNode? currentWorldEventsRoot)
    {
        var previousRevealKeys = CollectVisibleBonusClueRevealKeys(previousArcsRoot, previousWorldEventsRoot);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return false;

        var consumedRevealKeys = new HashSet<string>(previousRevealKeys, StringComparer.OrdinalIgnoreCase);
        var sponsorGuardianByArcId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var arcId = GetNodeString(arc["arcId"]);
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(sponsorGuardianId) ||
                arc["publicSignals"] is not JsonArray publicSignals)
            {
                continue;
            }

            sponsorGuardianByArcId[arcId!] = sponsorGuardianId!;

            foreach (var signal in publicSignals.OfType<JsonObject>())
            {
                if (!GetJsonBool(signal["visibleToPlayer"]))
                    continue;

                var sourceProjectId = GetNodeString(signal["bonusClueSourceProjectId"]);
                if (string.IsNullOrWhiteSpace(sourceProjectId))
                    continue;

                var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                if (GetJsonBool(signal["bonusClueConsumed"]))
                {
                    if (!string.IsNullOrWhiteSpace(revealKey))
                        consumedRevealKeys.Add(revealKey);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
                    continue;

                return true;
            }
        }

        foreach (var worldEvent in EnumerateWorldEventObjects(currentWorldEventsRoot))
        {
            var relatedArcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            var sourceProjectId = GetNodeString(worldEvent["bonusClueSourceProjectId"]);
            if (string.IsNullOrWhiteSpace(relatedArcId) ||
                string.IsNullOrWhiteSpace(sourceProjectId) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent) ||
                !sponsorGuardianByArcId.ContainsKey(relatedArcId!))
            {
                continue;
            }

            var revealKey = BuildVisibleBonusClueRevealKey(relatedArcId!, worldEvent, isWorldEvent: true);
            if (GetJsonBool(worldEvent["bonusClueConsumed"]))
            {
                if (!string.IsNullOrWhiteSpace(revealKey))
                    consumedRevealKeys.Add(revealKey);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
                continue;

            return true;
        }

        return false;
    }

    private static HashSet<string> CollectGuardianSponsoredRivalArcIds(JsonObject currentArcsRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return result;

        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var arcId = GetNodeString(arc["arcId"]);
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (!string.IsNullOrWhiteSpace(arcId) &&
                !string.IsNullOrWhiteSpace(sponsorGuardianId))
            {
                result.Add(arcId!);
            }
        }

        return result;
    }

    private static bool ConsumeLoreResearchVisibleRivalClues(
        JsonObject? previousArcsRoot,
        JsonObject currentArcsRoot,
        JsonNode? previousWorldEventsRoot,
        JsonNode? currentWorldEventsRoot,
        JsonObject trackerRoot,
        int currentIncarnation,
        int currentTurn,
        List<JsonObject> journalEntries,
        out bool worldEventsChanged)
    {
        worldEventsChanged = false;
        var previousRevealKeys = CollectVisibleBonusClueRevealKeys(previousArcsRoot, previousWorldEventsRoot);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return false;

        var changed = false;
        var consumedRevealKeys = new HashSet<string>(previousRevealKeys, StringComparer.OrdinalIgnoreCase);
        var sponsorGuardianByArcId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var arcId = GetNodeString(arc["arcId"]);
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(sponsorGuardianId) ||
                arc["publicSignals"] is not JsonArray publicSignals)
            {
                continue;
            }

            sponsorGuardianByArcId[arcId!] = sponsorGuardianId!;

            foreach (var signal in publicSignals.OfType<JsonObject>())
            {
                if (!GetJsonBool(signal["visibleToPlayer"]))
                    continue;

                var sourceProjectId = GetNodeString(signal["bonusClueSourceProjectId"]);
                if (string.IsNullOrWhiteSpace(sourceProjectId))
                    continue;

                if (GetJsonBool(signal["bonusClueConsumed"]))
                {
                    consumedRevealKeys.Add(BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false));
                    continue;
                }

                var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
                {
                    changed |= MarkBonusClueConsumed(signal, currentTurn, sourceProjectId!);
                    continue;
                }

                var clueCost = Math.Max(1, GetNodeInt(signal["bonusClueCost"], 1));
                if (!GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, sponsorGuardianId!, sourceProjectId!, currentIncarnation, clueCost))
                    continue;

                changed |= MarkBonusClueConsumed(signal, currentTurn, sourceProjectId!);
                if (!string.IsNullOrWhiteSpace(revealKey))
                    consumedRevealKeys.Add(revealKey);
                changed = true;
                journalEntries.Add(new JsonObject
                {
                    ["entryId"] = $"gpj_{sponsorGuardianId}_{sourceProjectId}_{arcId}_clue_{Guid.NewGuid():N}",
                    ["turn"] = currentTurn,
                    ["guardianId"] = sponsorGuardianId,
                    ["projectId"] = sourceProjectId,
                    ["eventType"] = "assisted",
                    ["visibility"] = "player_known",
                    ["title"] = "Израсходован bonus clue для rival-нити",
                    ["summary"] = $"Проект lore_research раскрыл новый видимый сигнал чужой нити судьбы и потратил {clueCost} clue budget.",
                    ["details"] = new JsonArray
                    {
                        $"ArcId: {arcId}",
                        $"SignalId: {GetNodeString(signal["signalId"])}",
                        $"Clue cost: {clueCost}"
                    }
                });
            }
        }

        foreach (var worldEvent in EnumerateWorldEventObjects(currentWorldEventsRoot))
        {
            var relatedArcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            var sourceProjectId = GetNodeString(worldEvent["bonusClueSourceProjectId"]);
            if (string.IsNullOrWhiteSpace(relatedArcId) ||
                string.IsNullOrWhiteSpace(sourceProjectId) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent) ||
                !sponsorGuardianByArcId.TryGetValue(relatedArcId!, out var sponsorGuardianId))
            {
                continue;
            }

            if (GetJsonBool(worldEvent["bonusClueConsumed"]))
            {
                consumedRevealKeys.Add(BuildVisibleBonusClueRevealKey(relatedArcId!, worldEvent, isWorldEvent: true));
                continue;
            }

            var revealKey = BuildVisibleBonusClueRevealKey(relatedArcId!, worldEvent, isWorldEvent: true);
            if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
            {
                worldEventsChanged |= MarkBonusClueConsumed(worldEvent, currentTurn, sourceProjectId!);
                continue;
            }

            var clueCost = Math.Max(1, GetNodeInt(worldEvent["bonusClueCost"], 1));
            if (!GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, sponsorGuardianId, sourceProjectId!, currentIncarnation, clueCost))
                continue;

            worldEventsChanged |= MarkBonusClueConsumed(worldEvent, currentTurn, sourceProjectId!);
            if (!string.IsNullOrWhiteSpace(revealKey))
                consumedRevealKeys.Add(revealKey);
            changed = true;
            journalEntries.Add(new JsonObject
            {
                ["entryId"] = $"gpj_{sponsorGuardianId}_{sourceProjectId}_{relatedArcId}_world_event_clue_{Guid.NewGuid():N}",
                ["turn"] = currentTurn,
                ["guardianId"] = sponsorGuardianId,
                ["projectId"] = sourceProjectId,
                ["eventType"] = "assisted",
                ["visibility"] = "player_known",
                ["title"] = "Израсходован bonus clue для rival-нити через новость мира",
                ["summary"] = $"Проект lore_research раскрыл linked world event чужой нити судьбы и потратил {clueCost} clue budget.",
                ["details"] = new JsonArray
                {
                    $"ArcId: {relatedArcId}",
                    $"WorldEventId: {GetNodeString(worldEvent["eventId"])}",
                    $"Clue cost: {clueCost}"
                }
            });
        }

        return changed;
    }

    private static bool IsPlayerVisibleRivalWorldEvent(JsonObject worldEvent)
    {
        var visibility = GetNodeString(worldEvent["visibility"]);
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> CollectVisibleBonusClueRevealKeys(JsonObject? arcsRoot, JsonNode? worldEventsRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arcsRoot?["arcs"] is JsonArray arcs)
        {
            foreach (var arc in arcs.OfType<JsonObject>())
            {
                var arcId = GetNodeString(arc["arcId"]);
                if (string.IsNullOrWhiteSpace(arcId) || arc["publicSignals"] is not JsonArray publicSignals)
                    continue;

                foreach (var signal in publicSignals.OfType<JsonObject>())
                {
                    if (!GetJsonBool(signal["visibleToPlayer"]) ||
                        string.IsNullOrWhiteSpace(GetNodeString(signal["bonusClueSourceProjectId"])))
                    {
                        continue;
                    }

                    result.Add(BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false));
                }
            }
        }

        foreach (var worldEvent in EnumerateWorldEventObjects(worldEventsRoot))
        {
            var arcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(GetNodeString(worldEvent["bonusClueSourceProjectId"])) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent))
            {
                continue;
            }

            result.Add(BuildVisibleBonusClueRevealKey(arcId!, worldEvent, isWorldEvent: true));
        }

        return result;
    }

    private static string BuildVisibleBonusClueRevealKey(string arcId, JsonObject source, bool isWorldEvent)
    {
        var revealId = GetNodeString(source["bonusClueRevealId"]);
        if (!string.IsNullOrWhiteSpace(revealId))
            return $"{arcId}::reveal::{revealId}";

        if (!isWorldEvent)
        {
            var signalId = GetNodeString(source["signalId"]);
            if (!string.IsNullOrWhiteSpace(signalId))
                return $"{arcId}::signal::{signalId}";

            return $"{arcId}::signal::{GetNodeInt(source["stage"])}::{GetNodeString(source["source"])}::{GetNodeString(source["description"])}";
        }

        var worldEventId = GetNodeString(source["eventId"]);
        if (!string.IsNullOrWhiteSpace(worldEventId))
            return $"{arcId}::world_event::{worldEventId}";

        return $"{arcId}::world_event::{GetNodeString(source["eventTitle"])}::{GetNodeString(source["title"])}::{GetNodeString(source["summary"])}::{GetNodeString(source["description"])}";
    }

    private static bool MarkBonusClueConsumed(JsonObject node, int currentTurn, string sourceProjectId)
    {
        var changed = false;
        if (!GetJsonBool(node["bonusClueConsumed"]))
        {
            node["bonusClueConsumed"] = true;
            changed = true;
        }

        if (GetNodeInt(node["bonusClueConsumedAtTurn"]) != currentTurn)
        {
            node["bonusClueConsumedAtTurn"] = currentTurn;
            changed = true;
        }

        if (!string.Equals(GetNodeString(node["bonusClueConsumedProjectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
        {
            node["bonusClueConsumedProjectId"] = sourceProjectId;
            changed = true;
        }

        return changed;
    }

    private static IEnumerable<JsonObject> EnumerateWorldEventObjects(JsonNode? root)
    {
        if (root is JsonObject obj)
        {
            if (obj["worldEventsLog"] is JsonArray worldEventsLog)
            {
                foreach (var worldEvent in worldEventsLog.OfType<JsonObject>())
                    yield return worldEvent;
                yield break;
            }

            if (obj["events"] is JsonArray events)
            {
                foreach (var worldEvent in events.OfType<JsonObject>())
                    yield return worldEvent;
                yield break;
            }
        }
        else if (root is JsonArray arr)
        {
            foreach (var worldEvent in arr.OfType<JsonObject>())
                yield return worldEvent;
        }
    }

}

