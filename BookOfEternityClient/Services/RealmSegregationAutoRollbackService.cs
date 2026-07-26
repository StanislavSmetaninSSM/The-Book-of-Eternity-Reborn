using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class RealmSegregationAutoRollbackService
{
    public const string ReportPath = "game_state/control/validation_auto_rollback_report.json";
    private const string ManifestPath = "game_state/control/pending_turn_snapshot.json";

    private static readonly JsonSerializerOptions ManifestJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReportJsonOpts = new(ManifestJsonOpts)
    {
        WriteIndented = true
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<RealmSegregationAutoRollbackService> _logger;

    public RealmSegregationAutoRollbackService(
        FileSystemManager fs,
        ILogger<RealmSegregationAutoRollbackService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<RealmSegregationAutoRollbackResult> TryRollbackForbiddenRealmMutationsAsync(
        string? sourceRealm,
        IEnumerable<string> forbiddenPaths,
        string source)
    {
        var manifest = await LoadValidatedManifestAsync();
        if (manifest == null)
            return RealmSegregationAutoRollbackResult.Noop("validated pending-turn snapshot is missing or invalid");

        var actions = new List<RealmSegregationAutoRollbackAction>();
        var uniquePaths = forbiddenPaths
            .Select(NormalizeRelativePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in uniquePaths)
        {
            if (!IsSafeGameStatePath(path))
            {
                _logger.LogWarning("Rejected unsafe realm auto-rollback path {Path}.", path);
                continue;
            }

            if (manifest.Files.TryGetValue(path, out var snapshotPath))
            {
                var restored = await TryRestoreFromSnapshotAsync(manifest, path, snapshotPath);
                if (restored != null)
                    actions.Add(restored);
                continue;
            }

            if (!IsKnownBaselineFile(manifest, path) && _fs.FileExists(path))
            {
                _fs.DeleteFile(path);
                actions.Add(new RealmSegregationAutoRollbackAction(
                    "delete",
                    path,
                    null,
                    "Forbidden wrong-realm file was created after the pending-turn snapshot."));
            }
        }

        if (actions.Count == 0)
            return RealmSegregationAutoRollbackResult.Noop("no forbidden paths could be safely rolled back");

        var report = new RealmSegregationAutoRollbackReport(
            SchemaVersion: 1,
            Source: source,
            SourceRealm: string.IsNullOrWhiteSpace(sourceRealm) ? null : sourceRealm,
            SessionId: manifest.SessionId,
            RequestId: manifest.RequestId,
            TurnNumber: manifest.TurnNumber,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Actions: actions);

        await _fs.WriteFileAtomicAsync(ReportPath, JsonSerializer.Serialize(report, ReportJsonOpts));
        return new RealmSegregationAutoRollbackResult(true, actions, ReportPath, null);
    }

    public async Task<RealmSegregationBaselineIssueFilterResult> FilterRestoredForbiddenBaselineIssuesAsync(
        string? sourceRealm,
        IEnumerable<ValidationIssue> issues)
    {
        var issueList = issues.ToList();
        if (issueList.Count == 0)
            return new RealmSegregationBaselineIssueFilterResult(issueList, Array.Empty<ValidationIssue>());

        var manifest = await LoadValidatedManifestAsync();
        if (manifest == null)
            return new RealmSegregationBaselineIssueFilterResult(issueList, Array.Empty<ValidationIssue>());

        sourceRealm = string.IsNullOrWhiteSpace(sourceRealm)
            ? await TryReadSnapshotRealmAsync(manifest)
            : sourceRealm;

        if (!RealmSemantics.IsAfterlifeRealm(sourceRealm))
            return new RealmSegregationBaselineIssueFilterResult(issueList, Array.Empty<ValidationIssue>());

        var remaining = new List<ValidationIssue>();
        var suppressed = new List<ValidationIssue>();

        foreach (var issue in issueList)
        {
            var filePath = ExtractJsonFilePath(issue.FilePath);
            if (filePath != null &&
                IsFrozenBaselineValidationPath(sourceRealm, filePath) &&
                await CurrentFileMatchesValidatedSnapshotAsync(manifest, filePath))
            {
                suppressed.Add(issue);
                continue;
            }

            remaining.Add(issue);
        }

        return new RealmSegregationBaselineIssueFilterResult(remaining, suppressed);
    }

    private async Task<string?> TryReadSnapshotRealmAsync(RealmSegregationSnapshotManifest manifest)
    {
        if (!manifest.Files.TryGetValue("game_state/meta/soul_state.json", out var snapshotPath))
            return null;

        snapshotPath = NormalizeRelativePath(snapshotPath);
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath) ||
            !snapshotPath.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var json = await _fs.ReadFileAsync(snapshotPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("currentRealm", out var realmNode) &&
                   realmNode.ValueKind == JsonValueKind.String
                ? realmNode.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> CurrentFileMatchesValidatedSnapshotAsync(
        RealmSegregationSnapshotManifest manifest,
        string path)
    {
        if (!manifest.Files.TryGetValue(path, out var snapshotPath) ||
            !manifest.SnapshotFileHashes.TryGetValue(path, out var expectedHash))
        {
            return false;
        }

        snapshotPath = NormalizeRelativePath(snapshotPath);
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath) ||
            !snapshotPath.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
        var currentJson = await _fs.ReadFileAsync(path);
        if (snapshotJson == null || currentJson == null)
            return false;

        return string.Equals(PendingTurnSnapshotAuthority.ComputeSha256(snapshotJson), expectedHash, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(snapshotJson, currentJson, StringComparison.Ordinal);
    }

    private async Task<RealmSegregationAutoRollbackAction?> TryRestoreFromSnapshotAsync(
        RealmSegregationSnapshotManifest manifest,
        string path,
        string snapshotPath)
    {
        snapshotPath = NormalizeRelativePath(snapshotPath);
        if (!IsSafeGameStatePath(path) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath) ||
            !snapshotPath.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected unsafe realm auto-rollback restore {Path} from {SnapshotPath}.", path, snapshotPath);
            return null;
        }

        var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
        if (snapshotJson == null)
            return null;

        if (!manifest.SnapshotFileHashes.TryGetValue(path, out var expectedHash) ||
            !string.Equals(PendingTurnSnapshotAuthority.ComputeSha256(snapshotJson), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected realm auto-rollback restore for {Path}: snapshot hash mismatch.", path);
            return null;
        }

        var currentJson = await _fs.ReadFileAsync(path);
        if (string.Equals(currentJson, snapshotJson, StringComparison.Ordinal))
            return null;

        await _fs.WriteFileAtomicAsync(path, snapshotJson);
        return new RealmSegregationAutoRollbackAction(
            "restore",
            path,
            snapshotPath,
            "Forbidden wrong-realm mutation was restored from the validated pending-turn snapshot.");
    }

    private async Task<RealmSegregationSnapshotManifest?> LoadValidatedManifestAsync()
    {
        var manifestJson = await _fs.ReadFileAsync(ManifestPath);
        if (string.IsNullOrWhiteSpace(manifestJson))
            return null;

        JsonObject? manifest;
        try
        {
            manifest = JsonNode.Parse(manifestJson) as JsonObject;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Realm auto-rollback cannot read pending-turn snapshot manifest.");
            return null;
        }

        if (manifest == null)
            return null;

        var authorityJson = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        var isAuthorized = PendingTurnSnapshotAuthority.TryValidateManifestAgainstAuthority(
            manifest,
            authorityJson,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest["manifestPayloadHash"]?.GetValue<string>() ?? string.Empty,
            static (snapshotManifest, hash) => snapshotManifest["manifestPayloadHash"] = hash,
            static snapshotManifest => snapshotManifest["sessionId"]?.GetValue<string>() ?? string.Empty,
            static snapshotManifest => snapshotManifest["requestId"]?.GetValue<string>() ?? string.Empty,
            static snapshotManifest => snapshotManifest["turnNumber"]?.GetValue<int>() ?? 0,
            static snapshotManifest => ToStringDictionary(snapshotManifest["files"] as JsonObject),
            static snapshotManifest => ToStringDictionary(snapshotManifest["snapshotFileHashes"] as JsonObject),
            static snapshotManifest => ToStringDictionary(snapshotManifest["clientOwnedValidationHashes"] as JsonObject),
            static snapshotManifest => ToStringList(snapshotManifest["rollbackBaselineFiles"] as JsonArray),
            static snapshotManifest => snapshotManifest["sourceLabel"]?.GetValue<string>(),
            static snapshotManifest => ToStringDictionary(snapshotManifest["rollbackBackups"] as JsonObject),
            ReadRelativeFile,
            out var payload,
            out var failureCode,
            requireRollbackArtifactPaths: true);

        if (!isAuthorized || payload == null)
        {
            _logger.LogWarning("Realm auto-rollback skipped: pending-turn snapshot authority failed with {FailureCode}.", failureCode);
            return null;
        }

        return new RealmSegregationSnapshotManifest
        {
            SessionId = payload.SessionId,
            RequestId = payload.RequestId,
            TurnNumber = payload.TurnNumber,
            Files = payload.Files,
            SnapshotFileHashes = payload.SnapshotFileHashes,
            ClientOwnedValidationHashes = payload.ClientOwnedValidationHashes,
            RollbackBackups = payload.RollbackBackups,
            RollbackBaselineFiles = payload.RollbackBaselineFiles,
            SourceLabel = payload.SourceLabel,
            ManifestPayloadHash = manifest["manifestPayloadHash"]?.GetValue<string>() ?? string.Empty
        };
    }

    private byte[]? ReadRelativeFile(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
    }

    private static bool IsKnownBaselineFile(RealmSegregationSnapshotManifest manifest, string path)
    {
        return manifest.SnapshotFileHashes.ContainsKey(path) ||
               manifest.RollbackBaselineFiles.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSafeGameStatePath(string path)
    {
        return PendingTurnSnapshotAuthority.IsSafeRelativePath(path) &&
               path.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrozenBaselineValidationPath(string? sourceRealm, string relativePath)
    {
        if (RealmSemantics.IsChaosSea(sourceRealm))
            return IsForbiddenChaosSeaChangedFile(relativePath);

        if (RealmSemantics.IsShiningRealm(sourceRealm))
        {
            return IsForbiddenMortalWorldChangedFile(relativePath) &&
                   !relativePath.Replace('\\', '/').Equals(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsForbiddenChaosSeaChangedFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.Equals("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/regular_quests.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/quest_history.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/quests/plot_outline.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/characteristics.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/vehicles.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/storage_access.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/misc/player_interactions.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenMortalWorldChangedFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianSocialJournalState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianProjectState.TrackerPath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianProjectState.JournalPath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(GuardianPowerEventState.JournalPath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeActiveThreatState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(ChaosSeaGuardianPoliticsState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeChronicleState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(AfterlifeStoryOutlineState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("lore/chaos_sea/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractJsonFilePath(string? path)
    {
        var normalized = NormalizeRelativePath(path);
        var jsonIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
        if (jsonIndex < 0)
            return null;

        var result = normalized[..(jsonIndex + ".json".Length)];
        return IsSafeGameStatePath(result) || result.StartsWith("lore/", StringComparison.OrdinalIgnoreCase)
            ? result
            : null;
    }

    private static string NormalizeRelativePath(string? path)
    {
        return (path ?? string.Empty).Trim().Replace('\\', '/');
    }

    private static Dictionary<string, string> ToStringDictionary(JsonObject? root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root == null)
            return result;

        foreach (var pair in root)
        {
            if (pair.Value is not JsonValue valueNode ||
                !valueNode.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[pair.Key.Replace('\\', '/')] = value.Replace('\\', '/');
        }

        return result;
    }

    private static List<string> ToStringList(JsonArray? root)
    {
        var result = new List<string>();
        if (root == null)
            return result;

        foreach (var item in root)
        {
            if (item is not JsonValue valueNode ||
                !valueNode.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(value.Replace('\\', '/'));
        }

        return result;
    }

    private sealed class RealmSegregationSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }
}

public sealed record RealmSegregationAutoRollbackResult(
    bool RolledBack,
    IReadOnlyList<RealmSegregationAutoRollbackAction> Actions,
    string? ReportPath,
    string? SkipReason)
{
    public static RealmSegregationAutoRollbackResult Noop(string reason) =>
        new(false, Array.Empty<RealmSegregationAutoRollbackAction>(), null, reason);
}

public sealed record RealmSegregationAutoRollbackAction(
    string Action,
    string Path,
    string? SnapshotPath,
    string Reason);

public sealed record RealmSegregationBaselineIssueFilterResult(
    IReadOnlyList<ValidationIssue> RemainingIssues,
    IReadOnlyList<ValidationIssue> SuppressedIssues);

internal sealed record RealmSegregationAutoRollbackReport(
    int SchemaVersion,
    string Source,
    string? SourceRealm,
    string SessionId,
    string RequestId,
    int TurnNumber,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<RealmSegregationAutoRollbackAction> Actions);
