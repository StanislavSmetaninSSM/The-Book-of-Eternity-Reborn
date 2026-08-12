using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class ActorMemoryService
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<ActorMemoryService> _logger;

    public ActorMemoryService(FileSystemManager fs, ILogger<ActorMemoryService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm, int currentTurnNumber)
    {
        try
        {
            if (IsMortalRealm(currentRealm))
                return await BuildMortalNpcDigestAsync(currentTurnNumber, currentRealm);

            if (IsAfterlifeRealm(currentRealm))
                return await BuildAfterlifeGuardianDigestAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось собрать continuity digest.");
        }

        return null;
    }

    private static bool IsAfterlifeRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static bool IsMortalRealm(string? realm) =>
        !string.IsNullOrWhiteSpace(realm) && !IsAfterlifeRealm(realm);

    private async Task<string?> BuildAfterlifeGuardianDigestAsync()
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        using var guardiansDoc = JsonDocument.Parse(guardiansJson);
        if (guardiansDoc.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        var activeGuardian = guardiansDoc.RootElement.TryGetProperty("activeGuardian", out var activeGuardianNode) && activeGuardianNode.ValueKind == JsonValueKind.Object
            ? activeGuardianNode
            : default;
        var guardianId = GetString(activeGuardian, "guardianId");
        if (string.IsNullOrWhiteSpace(guardianId))
            return null;

        var guardianName = GetString(activeGuardian, "canonicalName");
        if (string.IsNullOrWhiteSpace(guardianName))
            guardianName = GetString(activeGuardian, "name");
        if (string.IsNullOrWhiteSpace(guardianName))
            guardianName = guardianId;

        var abodeId = activeGuardian.TryGetProperty("abode", out var abodeNode) && abodeNode.ValueKind == JsonValueKind.Object
            ? GetString(abodeNode, "abodeId")
            : string.Empty;
        var abodePower = AbodePowerRules.GetCurrentPower(activeGuardian);

        var sb = new StringBuilder();
        sb.AppendLine("ACTOR MEMORY DIGEST:");
        sb.AppendLine($"- Active Guardian: {guardianName}");

        await AppendGuardianJournalSectionAsync(sb, guardianId, guardianName);
        await AppendGuardianContinuitySectionAsync(sb, guardianId);
        await AppendResidentDigestSectionAsync(sb, guardianId, abodeId, abodePower);

        return sb.ToString();
    }

    private async Task AppendGuardianJournalSectionAsync(StringBuilder sb, string guardianId, string guardianName)
    {
        var thoughts = await ReadActorJournalEntriesAsync(GuardianThoughtJournalState.StatePath, GuardianThoughtJournalState.ActorIdProperty, guardianId);
        if (thoughts.Count > 0)
        {
            sb.AppendLine("- Guardian thoughts:");
            foreach (var entry in thoughts.Take(2))
                sb.AppendLine($"  • {BuildJournalLine(entry)}");
        }

        var events = await ReadActorJournalEntriesAsync(GuardianSocialJournalState.StatePath, GuardianSocialJournalState.ActorIdProperty, guardianId);
        if (events.Count > 0)
        {
            sb.AppendLine("- Guardian event memory:");
            foreach (var entry in events.Take(3))
                sb.AppendLine($"  • {BuildJournalLine(entry)}");
        }
        else
        {
            sb.AppendLine($"- Guardian event memory: no curated social summary yet for {guardianName}.");
        }
    }

    private async Task AppendGuardianContinuitySectionAsync(StringBuilder sb, string guardianId)
    {
        var projectEntries = await ReadContinuityEntriesAsync("game_state/meta/guardian_project_journal.json", "guardianId", guardianId);
        if (projectEntries.Count > 0)
        {
            sb.AppendLine("- Guardian project continuity:");
            foreach (var entry in projectEntries.Take(2))
                sb.AppendLine($"  • {BuildJournalLine(entry)}");
        }

        var abodePowerEntries = await ReadContinuityEntriesAsync("game_state/meta/abode_power_journal.json", "guardianId", guardianId);
        if (abodePowerEntries.Count > 0)
        {
            sb.AppendLine("- Abode power continuity:");
            foreach (var entry in abodePowerEntries.Take(2))
                sb.AppendLine($"  • {BuildJournalLine(entry)}");
        }
    }

    private async Task AppendResidentDigestSectionAsync(StringBuilder sb, string guardianId, string abodeId, int currentAbodePower)
    {
        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentJson))
            return;

        using var residentDoc = JsonDocument.Parse(residentJson);
        var residents = GuardianAbodeResidentState.CollectEntries(residentDoc.RootElement, guardianId, abodeId, currentAbodePower, presentOnly: true);
        if (residents.Count == 0)
            return;

        var pendingRequests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        var pendingResidentIds = pendingRequests
            .Select(request => request.ResidentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("- Current abode residents:");
        foreach (var resident in residents
                     .OrderByDescending(resident => pendingResidentIds.Contains(resident.ResidentId))
                     .ThenByDescending(resident => resident.BondLevel)
                     .Take(3))
        {
            sb.AppendLine(
                $"  • {resident.DisplayName} — bond {resident.BondTier}/{resident.BondLevel}; abode {resident.AbodeDevotionTier}/{resident.AbodeDevotionLevel}; {resident.MigrationState}");

            if (!string.IsNullOrWhiteSpace(resident.PersonalityProfile.Archetype))
            {
                var values = resident.PersonalityProfile.CoreValues.Take(3).ToList();
                var valuesText = values.Count > 0 ? $" ({string.Join(", ", values)})" : string.Empty;
                sb.AppendLine($"    profile: {resident.PersonalityProfile.Archetype}{valuesText}");
            }

            var thoughts = GuardianAbodeResidentState.CollectThoughtJournalEntries(residentDoc.RootElement, resident.ResidentId);
            if (thoughts.Count > 0)
                sb.AppendLine($"    thoughts: {BuildJournalLine(thoughts[0])}");

            var interactionLog = GuardianAbodeResidentState.CollectInteractionLogEntries(residentDoc.RootElement, resident.ResidentId);
            if (interactionLog.Count > 0)
                sb.AppendLine($"    events: {BuildJournalLine(interactionLog[0])}");

            var history = GuardianAbodeResidentState.CollectHistoryLogEntries(residentDoc.RootElement, resident.ResidentId);
            if (history.Count > 0)
                sb.AppendLine($"    past: {history[0].Title} — {history[0].Summary}");

            if (pendingResidentIds.Contains(resident.ResidentId))
                sb.AppendLine("    pending: unanswered resident interaction request exists.");
        }
    }

    private async Task<string?> BuildMortalNpcDigestAsync(int currentTurnNumber, string? currentRealm)
    {
        var localScope = await new LocalInteractionScopeService(_fs).ResolveAsync(currentRealm);
        if (!localScope.IsResolved || localScope.RealmKind != LocalInteractionRealmKind.Mortal)
            return null;

        var npcJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return null;

        using var npcDoc = JsonDocument.Parse(npcJson);
        var currentSceneNpcs = CollectCurrentSceneNpcs(npcDoc.RootElement, localScope.LocationId);
        if (currentSceneNpcs.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("ACTOR MEMORY DIGEST:");
        sb.AppendLine("- Current-scene NPC memory:");

        foreach (var npc in currentSceneNpcs.Take(3))
        {
            sb.AppendLine($"  • {npc.DisplayName}");

            var thought = await ReadLatestNpcThoughtAsync(npc.NpcId, npc.DisplayName);
            if (!string.IsNullOrWhiteSpace(thought))
                sb.AppendLine($"    thoughts: {thought}");

            var events = await ReadActorJournalEntriesAsync(NpcInteractionJournalState.StatePath, NpcInteractionJournalState.ActorIdProperty, npc.NpcId);
            if (events.Count > 0)
                sb.AppendLine($"    events: {BuildJournalLine(events[0])}");
        }

        await AppendMortalContinuitySectionAsync(sb, currentTurnNumber);

        return sb.ToString();
    }

    private async Task AppendMortalContinuitySectionAsync(StringBuilder sb, int currentTurnNumber)
    {
        var chronicleEntries = await ReadContinuityEntriesAsync("game_state/meta/character_chronicle.json");
        var worldEntries = await ReadContinuityEntriesAsync("game_state/world/world_events.json");
        var factionEntries = await ReadContinuityEntriesAsync("game_state/factions/faction_chronicles.json");

        chronicleEntries = SelectRelevantContinuityEntries(chronicleEntries, currentTurnNumber);
        worldEntries = SelectRelevantContinuityEntries(worldEntries, currentTurnNumber);
        factionEntries = SelectRelevantContinuityEntries(factionEntries, currentTurnNumber);

        if (chronicleEntries.Count == 0 && worldEntries.Count == 0 && factionEntries.Count == 0)
            return;

        sb.AppendLine("- Wider continuity:");
        if (chronicleEntries.Count > 0)
            sb.AppendLine($"  • chronicle: {BuildJournalLine(chronicleEntries[0])}");
        if (worldEntries.Count > 0)
            sb.AppendLine($"  • world: {BuildJournalLine(worldEntries[0])}");
        if (factionEntries.Count > 0)
            sb.AppendLine($"  • factions: {BuildJournalLine(factionEntries[0])}");
    }

    private static List<JsonObject> SelectRelevantContinuityEntries(List<JsonObject> entries, int currentTurnNumber, int recentTurnWindow = 10)
    {
        if (entries.Count == 0)
            return entries;

        if (currentTurnNumber > 0)
        {
            var lowerBound = Math.Max(0, currentTurnNumber - recentTurnWindow);
            var recentEntries = entries
                .Where(entry =>
                {
                    var turn = GetContinuityTurn(entry);
                    return turn > 0 && turn <= currentTurnNumber && turn >= lowerBound;
                })
                .Take(1)
                .ToList();
            if (recentEntries.Count > 0)
                return recentEntries;
        }

        return entries.Take(1).ToList();
    }

    private async Task<string?> ReadLatestNpcThoughtAsync(string npcId, string npcName)
    {
        var json = await _fs.ReadFileAsync("game_state/npcs/npc_journals.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement journals;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("NPCJournals", out journals) &&
                journals.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in journals.EnumerateArray())
                {
                    var entryNpcId = GetString(entry, "NPCId");
                    var entryName = GetString(entry, "NPCName");
                    if (!string.Equals(entryNpcId, npcId, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(entryName, npcName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var lastJournalNote = GetString(entry, "lastJournalNote");
                    if (!string.IsNullOrWhiteSpace(lastJournalNote))
                        return lastJournalNote;

                    if (entry.TryGetProperty("journalEntries", out var journalEntries) && journalEntries.ValueKind == JsonValueKind.Array)
                    {
                        var last = journalEntries.EnumerateArray().LastOrDefault();
                        if (last.ValueKind == JsonValueKind.Object)
                        {
                            var description = GetString(last, "description");
                            if (!string.IsNullOrWhiteSpace(description))
                                return description;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать npc_journals для continuity digest.");
        }

        return null;
    }

    private async Task<List<JsonObject>> ReadActorJournalEntriesAsync(string path, string actorIdProperty, string actorId)
    {
        return await ReadContinuityEntriesAsync(path, actorIdProperty, actorId);
    }

    private async Task<List<JsonObject>> ReadContinuityEntriesAsync(string path, string? actorIdProperty = null, string? actorId = null)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return new List<JsonObject>();

        try
        {
            var root = JsonNode.Parse(json);
            var entries = CollectContinuityEntries(root, actorIdProperty, actorId)
                .OrderByDescending(GetContinuityTurn)
                .ThenByDescending(entry =>
                {
                    var timestamp = GetNodeString(entry["timestamp"]);
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        return timestamp;

                    timestamp = GetNodeString(entry["revealedAtUtc"]);
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        return timestamp;

                    timestamp = GetNodeString(entry["resolvedAtUtc"]);
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        return timestamp;

                    timestamp = GetNodeString(entry["appliedAt"]);
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        return timestamp;

                    return GetNodeString(entry["completionTimestamp"]);
                }, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать continuity journal {Path}.", path);
            return new List<JsonObject>();
        }
    }

    private static IEnumerable<JsonObject> CollectContinuityEntries(JsonNode? root, string? actorIdProperty = null, string? actorId = null)
    {
        if (root is JsonObject obj)
        {
            foreach (var propertyName in new[] { ActorJournalState.EntriesProperty, "events" })
            {
                if (obj[propertyName] is not JsonArray entries)
                    continue;

                foreach (var entry in entries.OfType<JsonObject>())
                {
                    if (!MatchesActor(entry, actorIdProperty, actorId))
                        continue;

                    yield return entry.DeepClone().AsObject();
                }
            }

            yield break;
        }

        if (root is not JsonArray array)
            yield break;

        foreach (var entry in array.OfType<JsonObject>())
        {
            if (!MatchesActor(entry, actorIdProperty, actorId))
                continue;

            yield return entry.DeepClone().AsObject();
        }
    }

    private static bool MatchesActor(JsonObject entry, string? actorIdProperty, string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorIdProperty) || string.IsNullOrWhiteSpace(actorId))
            return true;

        return string.Equals(GetNodeString(entry[actorIdProperty]), actorId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildJournalLine(JsonObject entry)
    {
        var title = GetNodeString(entry["title"]);
        if (string.IsNullOrWhiteSpace(title))
            title = GetNodeString(entry["name"]);
        if (string.IsNullOrWhiteSpace(title))
            title = GetNodeString(entry["eventId"]);

        var summary = GetNodeString(entry["summary"]);
        if (string.IsNullOrWhiteSpace(summary))
            summary = GetNodeString(entry["description"]);
        if (string.IsNullOrWhiteSpace(summary))
            summary = GetNodeString(entry["content"]);
        if (string.IsNullOrWhiteSpace(summary))
            summary = GetNodeString(entry["entry"]);
        if (string.IsNullOrWhiteSpace(summary))
            summary = GetNodeString(entry["reason"]);

        var turn = GetContinuityTurn(entry);
        var eventType = GetNodeString(entry["eventType"]);
        var prefix = turn > 0 ? $"t{turn}: " : string.Empty;
        var typePrefix = string.IsNullOrWhiteSpace(eventType) ? string.Empty : $"{eventType}: ";

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{typePrefix}{title} — {summary}";
        if (!string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{typePrefix}{summary}";
        return $"{prefix}{typePrefix}{title}";
    }

    private static int GetContinuityTurn(JsonObject entry)
    {
        var turn = GetNodeInt(entry["turn"]);
        if (turn > 0)
            return turn;

        turn = GetNodeInt(entry["turnNumber"]);
        if (turn > 0)
            return turn;

        turn = GetNodeInt(entry["revealedAtTurn"]);
        if (turn > 0)
            return turn;

        turn = GetNodeInt(entry["resolvedAtTurn"]);
        if (turn > 0)
            return turn;

        return GetNodeInt(entry["completionTurn"]);
    }

    private static string BuildJournalLine(GuardianAbodeResidentState.JournalEntry entry)
    {
        var prefix = entry.Turn > 0 ? $"t{entry.Turn}: " : string.Empty;
        var typePrefix = string.IsNullOrWhiteSpace(entry.EventType) ? string.Empty : $"{entry.EventType}: ";

        if (!string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.Summary))
            return $"{prefix}{typePrefix}{entry.Title} — {entry.Summary}";
        if (!string.IsNullOrWhiteSpace(entry.Summary))
            return $"{prefix}{typePrefix}{entry.Summary}";
        return $"{prefix}{typePrefix}{entry.Title}";
    }

    private static List<(string NpcId, string DisplayName)> CollectCurrentSceneNpcs(JsonElement root, string currentLocationId)
    {
        var result = new List<(string NpcId, string DisplayName)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasExplicitSceneSection =
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(GuardianPolicyContracts.NpcCoreSceneSectionName, out _);

        foreach (var npc in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root))
        {
            var npcId = GetString(npc, "NPCId") ?? GetString(npc, "npcId") ?? GetString(npc, "id");
            var name = GetString(npc, "name") ?? GetString(npc, "NPCName") ?? GetString(npc, "npcName");
            var locationId = GetString(npc, "currentLocationId");
            var isSceneEntry = string.Equals(locationId, currentLocationId, StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(currentLocationId) && !isSceneEntry && hasExplicitSceneSection)
                continue;

            var dedupeKey = !string.IsNullOrWhiteSpace(npcId) ? npcId : name;
            if (string.IsNullOrWhiteSpace(dedupeKey) || !seen.Add(dedupeKey))
                continue;

            result.Add((npcId, string.IsNullOrWhiteSpace(name) ? dedupeKey : name));
        }

        return result;
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }

    private static string GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text ?? string.Empty;

        return string.Empty;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return (int)longValue;
        }

        return 0;
    }
}
