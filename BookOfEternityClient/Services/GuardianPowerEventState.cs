using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static class GuardianPowerEventState
{
    private static readonly JsonSerializerOptions PendingSnapshotManifestHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed class PendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }

    private sealed record PendingTurnRequestContext(
        string SessionId,
        string RequestId,
        int TurnNumber);

    private sealed record PoliticalProjectAuditBackfill(
        string ProjectGuardianId,
        string ProjectId,
        string ProjectName,
        string ProjectType,
        string ProjectTier,
        string? FinalState,
        string? TargetGuardianId);

    public const string JournalPath = "game_state/meta/abode_power_journal.json";

    public static readonly string[] AllowedReasonTypes =
    {
        "guardian_quest",
        "project_assist",
        "project_completion",
        "project_failure",
        "offering",
        "resonance",
        "correction_spend",
        "rival_strike",
        "rival_defense"
    };

    public static readonly string[] AllowedVisibility =
    {
        "player_known",
        "hidden"
    };

    public static bool IsValidReasonType(string? value) =>
        AllowedReasonTypes.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidVisibility(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        AllowedVisibility.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static string GetEventId(JsonObject evt) => GetNodeString(evt["eventId"]);

    public static string GetGuardianId(JsonObject evt) => GetNodeString(evt["guardianId"]);

    public static string GetEventId(JsonElement evt) => GetString(evt, "eventId");

    public static string GetGuardianId(JsonElement evt) => GetString(evt, "guardianId");

    public static bool ApplyEvents(
        JsonObject guardiansRoot,
        IEnumerable<JsonObject> events,
        int currentTurn,
        List<JsonObject> journalEntries)
    {
        var changed = false;
        foreach (var evt in events)
        {
            if (evt == null)
                continue;

            changed = ApplySingleEvent(guardiansRoot, evt, currentTurn, journalEntries) || changed;
        }

        return changed;
    }

    public static async Task AppendJournalEntriesAsync(FileSystemManager fs, IEnumerable<JsonObject> entries)
    {
        var buffered = entries.Where(item => item != null).ToList();
        if (buffered.Count == 0)
            return;

        JsonObject root;
        var existing = await fs.ReadFileAsync(JournalPath);
        if (string.IsNullOrWhiteSpace(existing))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }

        var entriesArray = root["entries"] as JsonArray ?? new JsonArray();
        root["entries"] = entriesArray;
        var trackerJson = await fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var preTurnTrackerJson = await ReadPreTurnTrackedFileAsync(fs, GuardianProjectState.TrackerPath);
        var politicalBackfillIndex = BuildPoliticalProjectAuditBackfillIndex(preTurnTrackerJson, trackerJson);

        foreach (var existingEntry in entriesArray.OfType<JsonObject>())
            BackfillLegacyPoliticalJournalEntry(existingEntry, politicalBackfillIndex);

        foreach (var entry in buffered)
            BackfillLegacyPoliticalJournalEntry(entry, politicalBackfillIndex);

        foreach (var entry in buffered)
        {
            var eventId = GetNodeString(entry["eventId"]);
            var duplicate = entriesArray
                .OfType<JsonObject>()
                .Any(existingEntry =>
                    string.Equals(GetNodeString(existingEntry["eventId"]), eventId, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
                continue;

            entriesArray.Add(entry.DeepClone());
        }

        await fs.WriteFileAtomicAsync(JournalPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    public static async Task RepairJournalAsync(FileSystemManager fs)
    {
        var existing = await fs.ReadFileAsync(JournalPath);
        if (string.IsNullOrWhiteSpace(existing))
            return;

        JsonObject root;
        try
        {
            root = JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return;
        }

        var trackerJson = await fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        var preTurnTrackerJson = await ReadPreTurnTrackedFileAsync(fs, GuardianProjectState.TrackerPath);
        var politicalBackfillIndex = BuildPoliticalProjectAuditBackfillIndex(preTurnTrackerJson, trackerJson);
        if (!BackfillLegacyPoliticalJournalEntries(root, politicalBackfillIndex))
            return;

        await fs.WriteFileAtomicAsync(JournalPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    public static JsonObject BuildEvent(
        string eventId,
        string guardianId,
        int delta,
        string reasonType,
        string sourceSurface,
        string sourceId,
        string title,
        string summary,
        JsonObject audit,
        string? relatedGuardianId = null,
        string visibility = "player_known",
        string? appliedAt = null)
    {
        return new JsonObject
        {
            ["eventId"] = eventId,
            ["guardianId"] = guardianId,
            ["delta"] = delta,
            ["reasonType"] = reasonType,
            ["sourceSurface"] = sourceSurface,
            ["sourceId"] = sourceId,
            ["title"] = title,
            ["summary"] = summary,
            ["visibility"] = visibility,
            ["appliedAt"] = appliedAt ?? DateTime.UtcNow.ToString("o"),
            ["relatedGuardianId"] = relatedGuardianId,
            ["audit"] = audit.DeepClone()
        };
    }

    private static bool ApplySingleEvent(
        JsonObject guardiansRoot,
        JsonObject evt,
        int currentTurn,
        List<JsonObject> journalEntries)
    {
        var guardianId = GetGuardianId(evt);
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        var appliedAt = GetNodeString(evt["appliedAt"]);
        if (string.IsNullOrWhiteSpace(appliedAt))
            appliedAt = DateTime.UtcNow.ToString("o");
        evt["appliedAt"] = appliedAt;

        var requestedDelta = GetNodeInt(evt["delta"]);
        if (requestedDelta == 0)
            return false;

        if (guardiansRoot["guardians"] is not JsonArray guardians)
            return false;

        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
        if (guardian == null)
            return false;

        var guardianName = GuardianManifestation.GetDisplayName(ToJsonElement(guardian));
        var abodePower = AbodePowerRules.EnsureCanonicalState(guardian);
        var currentPower = AbodePowerRules.GetCurrentPower(guardian);
        var nextPower = AbodePowerRules.ClampCurrentPower(currentPower + requestedDelta);
        var appliedDelta = nextPower - currentPower;
        if (appliedDelta == 0)
            return false;

        abodePower["currentPower"] = nextPower;
        abodePower["tier"] = AbodePowerRules.GetTierLabel(nextPower);
        abodePower["lastUpdatedAt"] = appliedAt;
        var history = abodePower["history"] as JsonArray ?? new JsonArray();
        history.Add(new JsonObject
        {
            ["eventId"] = GetNodeString(evt["eventId"]),
            ["timestamp"] = appliedAt,
            ["change"] = appliedDelta,
            ["reason"] = GetNodeString(evt["title"]),
            ["summary"] = GetNodeString(evt["summary"]),
            ["reasonType"] = GetNodeString(evt["reasonType"]),
            ["source"] = GetNodeString(evt["sourceSurface"]),
            ["sourceId"] = GetNodeString(evt["sourceId"]),
            ["relatedGuardianId"] = evt["relatedGuardianId"]?.DeepClone(),
            ["audit"] = evt["audit"]?.DeepClone()
        });
        abodePower["history"] = history;
        guardian["abodePower"] = abodePower;
        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            activeGuardian["abodePower"] = abodePower.DeepClone();
            GuardianGachaChargeRules.NormalizeGuardianGachaState(activeGuardian);
        }

        journalEntries.Add(new JsonObject
        {
            ["entryId"] = $"abode_power_{guardianId}_{GetNodeString(evt["eventId"])}",
            ["eventId"] = GetNodeString(evt["eventId"]),
            ["turn"] = currentTurn,
            ["guardianId"] = guardianId,
            ["guardianName"] = guardianName,
            ["delta"] = appliedDelta,
            ["reasonType"] = GetNodeString(evt["reasonType"]),
            ["sourceSurface"] = GetNodeString(evt["sourceSurface"]),
            ["sourceId"] = GetNodeString(evt["sourceId"]),
            ["title"] = GetNodeString(evt["title"]),
            ["summary"] = GetNodeString(evt["summary"]),
            ["visibility"] = string.IsNullOrWhiteSpace(GetNodeString(evt["visibility"])) ? "player_known" : GetNodeString(evt["visibility"]),
            ["relatedGuardianId"] = evt["relatedGuardianId"]?.DeepClone(),
            ["appliedAt"] = appliedAt,
            ["audit"] = evt["audit"]?.DeepClone()
        });

        return true;
    }

    private static string GetNodeString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;

        try
        {
            return value.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return node.ToJsonString();
        }
    }

    private static int GetNodeInt(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsed))
                return parsed;
            if (value.TryGetValue<long>(out var parsedLong) &&
                parsedLong <= int.MaxValue &&
                parsedLong >= int.MinValue)
            {
                return (int)parsedLong;
            }
            if (value.TryGetValue<string>(out var parsedString) && int.TryParse(parsedString, out var parsedFromString))
                return parsedFromString;
        }

        return defaultValue;
    }

    private static async Task<string?> ReadPreTurnTrackedFileAsync(FileSystemManager fs, string relativePath)
    {
        var manifestJson = await fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        if (string.IsNullOrWhiteSpace(manifestJson))
            return null;

        PendingTurnSnapshotManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(
                manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }

        if (manifest == null ||
            !PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
                manifest,
                await fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath),
                PendingSnapshotManifestHashJsonOpts,
                static snapshotManifest => snapshotManifest.ManifestPayloadHash,
                static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
                static snapshotManifest => snapshotManifest.SessionId,
                static snapshotManifest => snapshotManifest.RequestId,
                static snapshotManifest => snapshotManifest.TurnNumber,
                static snapshotManifest => snapshotManifest.Files,
                static snapshotManifest => snapshotManifest.SnapshotFileHashes,
                static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
                static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
                static snapshotManifest => snapshotManifest.SourceLabel,
                static snapshotManifest => snapshotManifest.RollbackBackups,
                relativePath => ReadRelativeFileBytesFromWorkspace(fs, relativePath),
                out var authorityPayload,
                out _))
        {
            return null;
        }

        if (!await IsCurrentPendingTurnSnapshotAsync(fs, manifest))
            return null;

        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath))
        {
            return null;
        }

        if (manifest.SnapshotFileHashes == null ||
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return null;
        }

        var snapshotContent = await fs.ReadFileBytesAsync(snapshotPath);
        if (snapshotContent == null || authorityPayload == null)
            return null;

        var actualSnapshotHash = PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(
            authorityPayload,
            snapshotContent);
        if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
            return null;

        return DecodeSnapshotText(snapshotContent);
    }

    private static string DecodeSnapshotText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static byte[]? ReadRelativeFileBytesFromWorkspace(FileSystemManager fs, string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllBytes(fullPath);
    }

    private static async Task<bool> IsCurrentPendingTurnSnapshotAsync(FileSystemManager fs, PendingTurnSnapshotManifest manifest)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = await ReadPendingTurnRequestContextFromFileAsync(fs, repairRequestPath);
        if (DoesPendingTurnRequestContextMatchManifest(manifest, repairContext))
            return true;

        var turnContext = await ReadPendingTurnRequestContextFromFileAsync(fs, "input/turn_request.json");
        return DoesPendingTurnRequestContextMatchManifest(manifest, turnContext);
    }

    private static bool DoesPendingTurnRequestContextMatchManifest(
        PendingTurnSnapshotManifest manifest,
        PendingTurnRequestContext? context)
    {
        if (context == null)
            return false;

        if (manifest.TurnNumber != context.TurnNumber)
            return false;

        if (!DoesPendingTurnContextIdMatch(manifest.SessionId, context.SessionId))
            return false;

        if (!DoesPendingTurnContextIdMatch(manifest.RequestId, context.RequestId))
            return false;

        return true;
    }

    private static bool DoesPendingTurnContextIdMatch(string manifestId, string contextId)
    {
        return PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifestId, contextId);
    }

    private static async Task<PendingTurnRequestContext?> ReadPendingTurnRequestContextFromFileAsync(FileSystemManager fs, string path)
    {
        var requestJson = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(requestJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("turnNumber", out var turnNode) ||
                turnNode.ValueKind != JsonValueKind.Number ||
                !turnNode.TryGetInt32(out var turnNumber))
            {
                return null;
            }

            var sessionId = GetNodeStringFromElement(doc.RootElement, "sessionId");
            var requestId = GetNodeStringFromElement(doc.RootElement, "requestId");
            return new PendingTurnRequestContext(sessionId, requestId, turnNumber);
        }
        catch
        {
            return null;
        }
    }

    private static string GetNodeStringFromElement(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            PendingSnapshotManifestHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }


    private static Dictionary<string, PoliticalProjectAuditBackfill> BuildPoliticalProjectAuditBackfillIndex(params string?[] trackerJsonSources)
    {
        var result = new Dictionary<string, PoliticalProjectAuditBackfill>(StringComparer.OrdinalIgnoreCase);
        var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trackerJson in trackerJsonSources)
        {
            if (string.IsNullOrWhiteSpace(trackerJson))
                continue;

            try
            {
                var root = JsonNode.Parse(trackerJson) as JsonObject;
                if (root == null)
                    continue;

                MergePoliticalProjectAuditBackfillEntries(result, ambiguousKeys, root["activeProjects"] as JsonArray, completed: false);
                MergePoliticalProjectAuditBackfillEntries(result, ambiguousKeys, root["completedProjects"] as JsonArray, completed: true);
            }
            catch
            {
                // ignored
            }
        }

        return result;
    }

    private static void MergePoliticalProjectAuditBackfillEntries(
        IDictionary<string, PoliticalProjectAuditBackfill> result,
        ISet<string> ambiguousKeys,
        JsonArray? entries,
        bool completed)
    {
        if (entries == null)
            return;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var project = entry["project"] as JsonObject;
            if (project == null)
                continue;

            var projectId = GetNodeString(project["projectId"]);
            var projectName = GetNodeString(project["projectName"]);
            var projectType = GetNodeString(project["projectType"]);
            var projectTier = GetNodeString(project["projectTier"]);
            if (string.IsNullOrWhiteSpace(projectId) ||
                string.IsNullOrWhiteSpace(projectName) ||
                string.IsNullOrWhiteSpace(projectType) ||
                string.IsNullOrWhiteSpace(projectTier))
            {
                continue;
            }

            var finalState = completed ? GetNodeString(project["finalState"]) : string.Empty;
            var targetGuardianId = GetNodeString(project["targetGuardianId"]);
            var projectGuardianId = GetNodeString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(projectGuardianId))
                continue;

            var key = GuardianProjectState.BuildKey(projectGuardianId, projectId);
            if (ambiguousKeys.Contains(key))
                continue;

            var snapshot = new PoliticalProjectAuditBackfill(
                projectGuardianId,
                projectId,
                projectName,
                projectType,
                projectTier,
                finalState,
                targetGuardianId);

            if (result.TryGetValue(key, out var existingSnapshot))
            {
                if (!Equals(existingSnapshot, snapshot))
                {
                    result.Remove(key);
                    ambiguousKeys.Add(key);
                }

                continue;
            }

            result[key] = snapshot;
        }
    }

    private static bool BackfillLegacyPoliticalJournalEntries(
        JsonObject root,
        IReadOnlyDictionary<string, PoliticalProjectAuditBackfill> politicalBackfillIndex)
    {
        if (root["entries"] is not JsonArray entries)
            return false;

        var changed = false;
        foreach (var entry in entries.OfType<JsonObject>())
            changed = BackfillLegacyPoliticalJournalEntry(entry, politicalBackfillIndex) || changed;

        return changed;
    }

    private static bool BackfillLegacyPoliticalJournalEntry(
        JsonObject entry,
        IReadOnlyDictionary<string, PoliticalProjectAuditBackfill> politicalBackfillIndex)
    {
        var reasonType = GetNodeString(entry["reasonType"]);
        if (!IsPoliticalReasonType(reasonType))
            return false;

        var audit = entry["audit"] as JsonObject;
        var createdAudit = false;
        if (audit == null)
        {
            audit = new JsonObject();
            createdAudit = true;
        }

        var lookupProjectId = GetNodeString(audit["projectId"]);
        if (string.IsNullOrWhiteSpace(lookupProjectId))
            lookupProjectId = GetNodeString(entry["sourceId"]);
        if (string.IsNullOrWhiteSpace(lookupProjectId))
            return false;

        var snapshot = ResolvePoliticalProjectBackfillSnapshot(entry, audit, reasonType, lookupProjectId, politicalBackfillIndex);
        if (snapshot == null)
            return false;

        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(entry["sourceSurface"]), "completeGuardianProjects", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(snapshot.TargetGuardianId))
        {
            var targetGuardianId = GetNodeString(entry["guardianId"]);
            if (!string.Equals(snapshot.TargetGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var changed = false;
        if (createdAudit)
        {
            entry["audit"] = audit;
            changed = true;
        }
        changed = SetCanonicalValue(audit, "projectGuardianId", snapshot.ProjectGuardianId) || changed;
        changed = SetCanonicalValue(audit, "projectId", snapshot.ProjectId) || changed;
        changed = SetCanonicalValue(audit, "projectName", snapshot.ProjectName) || changed;
        changed = SetCanonicalValue(audit, "projectType", snapshot.ProjectType) || changed;
        changed = SetCanonicalValue(audit, "projectTier", snapshot.ProjectTier) || changed;
        if (!string.IsNullOrWhiteSpace(snapshot.FinalState))
            changed = SetCanonicalValue(audit, "finalState", snapshot.FinalState) || changed;
        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(entry["sourceSurface"]), "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            changed = SetCanonicalValue(entry, "relatedGuardianId", snapshot.ProjectGuardianId) || changed;
        }

        return changed;
    }

    private static PoliticalProjectAuditBackfill? ResolvePoliticalProjectBackfillSnapshot(
        JsonObject entry,
        JsonObject audit,
        string reasonType,
        string lookupProjectId,
        IReadOnlyDictionary<string, PoliticalProjectAuditBackfill> politicalBackfillIndex)
    {
        var projectGuardianId = GetNodeString(audit["projectGuardianId"]);
        if (string.IsNullOrWhiteSpace(projectGuardianId))
            projectGuardianId = ResolvePoliticalProjectGuardianId(entry, reasonType);

        var lookupKey = BuildPoliticalProjectLookupKey(projectGuardianId, lookupProjectId);
        if (!string.IsNullOrWhiteSpace(lookupKey) &&
            politicalBackfillIndex.TryGetValue(lookupKey, out var ownerBoundSnapshot))
        {
            if (IsEligiblePoliticalProjectBackfillSnapshot(ownerBoundSnapshot, GetNodeString(entry["sourceSurface"]), reasonType))
                return ownerBoundSnapshot;
        }

        var targetAwareSnapshot = TryResolveCompletionSourcedRivalStrikeBackfillByProjectIdAndTarget(
            politicalBackfillIndex,
            lookupProjectId,
            GetNodeString(entry["guardianId"]),
            GetNodeString(entry["sourceSurface"]),
            reasonType);
        if (targetAwareSnapshot != null)
            return targetAwareSnapshot;

        var uniqueSnapshot = TryResolveUniquePoliticalProjectBackfillByProjectId(politicalBackfillIndex, lookupProjectId);
        return IsEligiblePoliticalProjectBackfillSnapshot(uniqueSnapshot, GetNodeString(entry["sourceSurface"]), reasonType)
            ? uniqueSnapshot
            : null;
    }

    private static string ResolvePoliticalProjectGuardianId(JsonObject entry, string reasonType)
    {
        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(entry["sourceSurface"]), "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            return GetNodeString(entry["relatedGuardianId"]);
        }

        return GetNodeString(entry["guardianId"]);
    }

    private static string BuildPoliticalProjectLookupKey(string? projectGuardianId, string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectGuardianId) || string.IsNullOrWhiteSpace(projectId))
            return string.Empty;

        return GuardianProjectState.BuildKey(projectGuardianId, projectId);
    }

    private static PoliticalProjectAuditBackfill? TryResolveUniquePoliticalProjectBackfillByProjectId(
        IReadOnlyDictionary<string, PoliticalProjectAuditBackfill> politicalBackfillIndex,
        string projectId)
    {
        PoliticalProjectAuditBackfill? match = null;
        foreach (var snapshot in politicalBackfillIndex.Values)
        {
            if (!string.Equals(snapshot.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match != null)
                return null;

            match = snapshot;
        }

        return match;
    }

    private static PoliticalProjectAuditBackfill? TryResolveCompletionSourcedRivalStrikeBackfillByProjectIdAndTarget(
        IReadOnlyDictionary<string, PoliticalProjectAuditBackfill> politicalBackfillIndex,
        string projectId,
        string? targetGuardianId,
        string? sourceSurface,
        string reasonType)
    {
        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(targetGuardianId))
        {
            return null;
        }

        PoliticalProjectAuditBackfill? match = null;
        foreach (var snapshot in politicalBackfillIndex.Values)
        {
            if (!string.Equals(snapshot.ProjectId, projectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.ProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.FinalState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.TargetGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match != null)
                return null;

            match = snapshot;
        }

        return match;
    }

    private static bool IsEligiblePoliticalProjectBackfillSnapshot(
        PoliticalProjectAuditBackfill? snapshot,
        string? sourceSurface,
        string reasonType)
    {
        if (snapshot == null)
            return false;

        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(snapshot.ProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(snapshot.FinalState, "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPoliticalReasonType(string? reasonType) =>
        string.Equals(reasonType, "project_assist", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "project_completion", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "project_failure", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "rival_defense", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase);

    private static bool SetCanonicalValue(JsonObject obj, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var currentValue = GetNodeString(obj[propertyName]);
        if (string.Equals(currentValue, value, StringComparison.OrdinalIgnoreCase))
            return false;

        obj[propertyName] = value;
        return true;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static JsonElement ToJsonElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
}
