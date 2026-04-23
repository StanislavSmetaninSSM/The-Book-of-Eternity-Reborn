using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class AfterlifeArchiveCandidateService
{
    public const string ManifestPath = "game_state/control/archive_candidate_manifest.json";

    public const string StatusPending = "pending";
    public const string StatusArchived = "archived";
    public const string StatusSkipped = "skipped";

    public const int MaxArchivedPerLife = 3;
    public const int MaxSecretArchivedPerLife = 1;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private static readonly HashSet<string> SecretTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "hidden",
        "forbidden",
        "cosmic_secret"
    };

    private static readonly Dictionary<string, string> BaseRarityByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cosmology"] = "Rare",
        ["artifacts"] = "Rare",
        ["magic"] = "Rare",
        ["history"] = "Uncommon",
        ["geography"] = "Uncommon",
        ["cultures"] = "Uncommon",
        ["factions"] = "Uncommon",
        ["characters"] = "Uncommon",
        ["creatures"] = "Uncommon",
        ["other"] = "Uncommon"
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<AfterlifeArchiveCandidateService> _logger;

    public AfterlifeArchiveCandidateService(FileSystemManager fs, ILogger<AfterlifeArchiveCandidateService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public sealed class CandidateManifest
    {
        [JsonPropertyName("sourceLife")]
        public int SourceLife { get; set; }

        [JsonPropertyName("lastExtractedAt")]
        public string LastExtractedAt { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("candidates")]
        public List<ArchiveCandidate> Candidates { get; set; } = new();
    }

    public sealed class ArchiveCandidate
    {
        [JsonPropertyName("candidateId")]
        public string CandidateId { get; set; } = "";

        [JsonPropertyName("sourceKind")]
        public string SourceKind { get; set; } = AfterlifeArchiveState.SourceKindCodex;

        [JsonPropertyName("sourceEntryId")]
        public string SourceEntryId { get; set; } = "";

        [JsonPropertyName("sourceLife")]
        public int SourceLife { get; set; }

        [JsonPropertyName("proposedEntryType")]
        public string ProposedEntryType { get; set; } = AfterlifeArchiveState.EntryTypeLoreFragment;

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "Uncommon";

        [JsonPropertyName("status")]
        public string Status { get; set; } = StatusPending;

        [JsonPropertyName("discoveredAt")]
        public string? DiscoveredAt { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("archivedAtUtc")]
        public string? ArchivedAtUtc { get; set; }

        [JsonPropertyName("skippedAtUtc")]
        public string? SkippedAtUtc { get; set; }
    }

    public async Task<CandidateManifest?> ReadAsync()
    {
        var raw = await _fs.ReadFileAsync(ManifestPath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CandidateManifest>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать archive candidate manifest");
            return null;
        }
    }

    public async Task RefreshFromCurrentStateAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var codexJson = await _fs.ReadFileAsync("lore/codex_entries.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            _logger.LogWarning("Не удалось обновить archive candidate manifest: current soul_state.json отсутствует или пуст.");
            return;
        }

        if (string.IsNullOrWhiteSpace(codexJson))
        {
            Clear();
            return;
        }

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                _logger.LogWarning("Не удалось обновить archive candidate manifest: current soul_state.json unreadable или не object-root.");
                return;
            }

            var lifeTransitionsJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
            var hasCanonicalTriggerLifeEnd = await CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
                _fs,
                lifeTransitionsJson,
                soulRoot);
            if (GuardianPolicyContracts.TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
                    soulRoot,
                    hasCanonicalTriggerLifeEnd,
                    out var invalidSoulFailure))
            {
                _logger.LogWarning(
                    "Не удалось обновить archive candidate manifest: current soul_state unreadable для strict current-owner path ({FailureDescription})",
                    invalidSoulFailure);
                return;
            }

            using var soulDoc = JsonDocument.Parse(soulJson);
            using var codexDoc = JsonDocument.Parse(codexJson);

            if (!TryReadSourceLife(soulDoc.RootElement, out var sourceLife, out var sourceLifeFailure))
            {
                _logger.LogWarning(
                    "Не удалось обновить archive candidate manifest: current soul_state не даёт canonical source-life authority ({FailureDescription})",
                    sourceLifeFailure);
                return;
            }

            if (sourceLife <= 0)
            {
                Clear();
                return;
            }

            var existing = await ReadAsync();
            var existingStatuses = existing != null && existing.SourceLife == sourceLife
                ? existing.Candidates.ToDictionary(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ArchiveCandidate>(StringComparer.OrdinalIgnoreCase);

            var manifest = new CandidateManifest
            {
                SourceLife = sourceLife,
                LastExtractedAt = DateTime.UtcNow.ToString("o")
            };

            foreach (var candidate in BuildCandidates(codexDoc.RootElement, sourceLife, existingStatuses))
                manifest.Candidates.Add(candidate);

            await _fs.WriteFileAtomicAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось обновить archive candidate manifest");
        }
    }

    public async Task<bool> ArchiveCandidateAsync(string candidateId)
    {
        var manifest = await ReadAsync();
        if (manifest == null)
            return false;

        var candidate = manifest.Candidates.FirstOrDefault(item =>
            string.Equals(item.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase));
        if (candidate == null || !string.Equals(candidate.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
            return false;

        var archivedCount = manifest.Candidates.Count(item => string.Equals(item.Status, StatusArchived, StringComparison.OrdinalIgnoreCase));
        var archivedSecretCount = manifest.Candidates.Count(item =>
            string.Equals(item.Status, StatusArchived, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ProposedEntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase));

        if (archivedCount >= MaxArchivedPerLife)
            return false;

        if (string.Equals(candidate.ProposedEntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase) &&
            archivedSecretCount >= MaxSecretArchivedPerLife)
        {
            return false;
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(soulJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось распарсить soul_state для archive candidate archival");
            return false;
        }

        if (node is not JsonObject root)
            return false;

        GuardianPolicyContracts.EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(root);
        var stored = AfterlifeArchiveState.EnsureStoredArray(root);
        if (stored.OfType<JsonObject>().Any(entry =>
                string.Equals(entry["sourceEntryId"]?.GetValue<string>(), candidate.SourceEntryId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var archiveId = $"archive_{candidate.SourceEntryId}";
        var tagsNode = new JsonArray();
        foreach (var tag in candidate.Tags)
            tagsNode.Add(tag);

        var archiveEntry = new JsonObject
        {
            ["archiveId"] = archiveId,
            ["entryType"] = candidate.ProposedEntryType,
            ["title"] = candidate.Title,
            ["summary"] = candidate.Summary,
            ["content"] = candidate.Content,
            ["rarity"] = candidate.Rarity,
            ["sourceLife"] = candidate.SourceLife,
            ["acquiredAtUtc"] = DateTime.UtcNow.ToString("o"),
            ["sourceKind"] = string.IsNullOrWhiteSpace(candidate.SourceKind) ? AfterlifeArchiveState.SourceKindCodex : candidate.SourceKind,
            ["sourceEntryId"] = candidate.SourceEntryId,
            ["tags"] = tagsNode
        };

        AfterlifeArchiveState.UpsertEntry(stored, archiveEntry);
        candidate.Status = StatusArchived;
        candidate.ArchivedAtUtc = DateTime.UtcNow.ToString("o");
        candidate.SkippedAtUtc = null;

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                root,
                new GuardianPolicyContracts.SoulStatePatchConflictContext(
                    GuardianPolicyContracts.SoulStatePatchTouchedDomains.AfterlifeArchive,
                    affectedArchiveIds: new[] { archiveId })).ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
        return true;
    }

    public async Task<bool> SkipCandidateAsync(string candidateId)
    {
        var manifest = await ReadAsync();
        if (manifest == null)
            return false;

        var candidate = manifest.Candidates.FirstOrDefault(item =>
            string.Equals(item.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase));
        if (candidate == null || !string.Equals(candidate.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
            return false;

        candidate.Status = StatusSkipped;
        candidate.SkippedAtUtc = DateTime.UtcNow.ToString("o");
        await _fs.WriteFileAtomicAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
        return true;
    }

    public void Clear() => _fs.DeleteFile(ManifestPath);

    public static bool IsSupportedStatus(string? status) =>
        string.Equals(status, StatusPending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, StatusArchived, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, StatusSkipped, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadSourceLife(
        JsonElement soulRoot,
        out int sourceLife,
        out string failureDescription)
    {
        sourceLife = 0;
        failureDescription = string.Empty;

        if (!soulRoot.TryGetProperty("currentRealm", out var currentRealm) ||
            currentRealm.ValueKind != JsonValueKind.String)
        {
            failureDescription = "current soul_state.currentRealm must be a string";
            return false;
        }

        if (!IsAfterlifeRealm(currentRealm.GetString()))
            return true;

        if (!soulRoot.TryGetProperty("livesHistory", out var livesHistory) ||
            livesHistory.ValueKind != JsonValueKind.Array ||
            livesHistory.GetArrayLength() == 0)
        {
            failureDescription = "afterlife current soul_state must contain non-empty livesHistory array";
            return false;
        }

        if (!soulRoot.TryGetProperty("currentIncarnation", out var inc) ||
            inc.ValueKind != JsonValueKind.Number ||
            !inc.TryGetInt32(out sourceLife) ||
            sourceLife <= 0)
        {
            failureDescription = "afterlife current soul_state must contain positive integer currentIncarnation";
            return false;
        }

        return true;
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ArchiveCandidate> BuildCandidates(
        JsonElement codexRoot,
        int sourceLife,
        IReadOnlyDictionary<string, ArchiveCandidate> existingStatuses)
    {
        if (!codexRoot.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("incarnation", out var inc) ||
                inc.ValueKind != JsonValueKind.Number ||
                !inc.TryGetInt32(out var entryLife) ||
                entryLife != sourceLife)
            {
                continue;
            }

            var sourceEntryId = GetString(entry, "entryId");
            if (string.IsNullOrWhiteSpace(sourceEntryId))
                continue;

            var tags = ReadTags(entry);
            var isSecret = tags.Any(tag => SecretTags.Contains(tag));
            var proposedType = isSecret
                ? AfterlifeArchiveState.EntryTypeSecretRecord
                : AfterlifeArchiveState.EntryTypeLoreFragment;
            var rarity = ResolveRarity(GetString(entry, "category"), isSecret);
            var candidateId = $"archive_candidate_{sourceEntryId}";

            existingStatuses.TryGetValue(candidateId, out var existing);
            var content = GetString(entry, "content") ?? existing?.Content ?? string.Empty;
            yield return new ArchiveCandidate
            {
                CandidateId = candidateId,
                SourceKind = existing?.SourceKind is { Length: > 0 } existingSourceKind
                    ? existingSourceKind
                    : AfterlifeArchiveState.SourceKindCodex,
                SourceEntryId = sourceEntryId,
                SourceLife = sourceLife,
                ProposedEntryType = proposedType,
                Title = GetString(entry, "title") ?? sourceEntryId,
                Summary = BuildSummary(content),
                Content = content,
                Rarity = rarity,
                Status = existing?.Status is { Length: > 0 } existingStatus && IsSupportedStatus(existingStatus)
                    ? existingStatus
                    : StatusPending,
                DiscoveredAt = GetString(entry, "discoveredAt"),
                Tags = tags,
                ArchivedAtUtc = existing?.ArchivedAtUtc,
                SkippedAtUtc = existing?.SkippedAtUtc
            };
        }
    }

    private static List<string> ReadTags(JsonElement entry)
    {
        var result = new List<string>();
        if (!entry.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
                continue;

            var value = tag.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
        }

        return result;
    }

    private static string ResolveRarity(string? category, bool isSecret)
    {
        var rarity = BaseRarityByCategory.TryGetValue(category ?? "other", out var mapped)
            ? mapped
            : "Uncommon";

        return isSecret ? BumpRarity(rarity) : rarity;
    }

    private static string BumpRarity(string rarity) =>
        rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "Uncommon",
            "uncommon" => "Rare",
            "rare" => "Epic",
            _ => "Epic"
        };

    private static string BuildSummary(string? content)
    {
        var text = (content ?? string.Empty).Trim();
        if (text.Length <= 220)
            return text;

        return text[..217].TrimEnd() + "...";
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
