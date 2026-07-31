using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

internal sealed class BrowserAfterlifeTurnRequestQueue
{
    private const string DirectGachaSourceLabel = "browser-direct-chaos-sea-gacha";
    private const string ValidationRepairRequestPath = "game_state/control/validation_repair_request.json";
    private const string ValidationRepairReadyPath = "game_state/control/validation_repair_ready.json";
    private const string TerminalProtocolFailureRequestPath = "game_state/control/terminal_protocol_failure_request.json";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;

    public BrowserAfterlifeTurnRequestQueue(FileSystemManager fs, StateManager stateManager)
    {
        _fs = fs;
        _stateManager = stateManager;
    }

    public async Task<BrowserQueuedTurnRequest> QueueDirectChaosSeaGachaAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockOwner owner,
        string playerAction,
        PendingTurnState pendingState,
        string soulRollbackPath,
        string currentRealm)
    {
        if (string.IsNullOrWhiteSpace(soulRollbackPath))
            throw new InvalidOperationException("Direct /gacha requires pre-spend soul rollback evidence before queueing a GM turn.");

        var request = new TurnRequest
        {
            SessionId = string.IsNullOrWhiteSpace(owner.OwnerId) ? $"browser:{Environment.MachineName}:{Environment.ProcessId}" : owner.OwnerId,
            TurnNumber = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1),
            PlayerAction = playerAction,
            Timestamp = DateTime.UtcNow.ToString("o"),
            GameMode = _stateManager.Settings.AllowHistoryManipulation ? "debug" : "normal",
            PreGeneratedDices1d20 = pendingState.PreGeneratedDices1d20,
            GachaBaseResult = pendingState.GachaBaseResult,
            AdditionalContext = new AdditionalContext
            {
                Urgency = "medium",
                ExpectedResponse = "materialize exactly one new Soul Relic for direct Chaos Sea gacha"
            },
            ProgressionControl = new ProgressionControl
            {
                CurrentRealm = string.IsNullOrWhiteSpace(currentRealm) ? "Chaos Sea" : currentRealm
            },
            SystemReminder = "Direct Chaos Sea gacha is prepaid by the browser. Use [CHAOS_SEA_DIRECT_GACHA], materialize exactly one new Soul Relic, do not spend Ink Feathers again, and keep finalRarity exactly equal to turn_request.gachaBaseResult.baseRarity."
        };

        var rollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/soul_state.json"] = soulRollbackPath.Replace('\\', '/')
        };

        try
        {
            await WritePendingTurnSnapshotAsync(writeLease, request, rollbackBackups);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                BrowserPendingTurnInspector.TurnRequestPath,
                JsonSerializer.Serialize(request, JsonOpts));
        }
        catch
        {
            CleanupQueuedTurnArtifacts(writeLease);
            throw;
        }

        return new BrowserQueuedTurnRequest(request.SessionId, request.RequestId, request.TurnNumber, request.PlayerAction);
    }

    private async Task WritePendingTurnSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        IReadOnlyDictionary<string, string> rollbackBackups)
    {
        var baselineFiles = EnumerateRollbackBaselineFiles(writeLease)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        baselineFiles.Add("game_state/meta/soul_state.json");

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in baselineFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            await SnapshotFileIfPresentAsync(writeLease, file, files, snapshotHashes);

        var manifest = new BrowserPendingTurnSnapshotManifest
        {
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            TurnNumber = request.TurnNumber,
            RequestTimestamp = request.Timestamp,
            PlayerAction = request.PlayerAction,
            PreGeneratedDices1d20 = request.PreGeneratedDices1d20,
            GachaBaseResult = request.GachaBaseResult == null
                ? null
                : JsonSerializer.SerializeToNode(request.GachaBaseResult, JsonOpts) as JsonObject,
            ProgressionControl = request.ProgressionControl,
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync(writeLease),
            RollbackBackups = new Dictionary<string, string>(rollbackBackups, StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = baselineFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
            SourceLabel = DirectGachaSourceLabel
        };
        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            SnapshotHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);

        var rollbackContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var rollbackPath in rollbackBackups.Values)
        {
            var normalized = rollbackPath.Replace('\\', '/');
            rollbackContents[normalized] = await _fs.ReadFileBytesAsync(writeLease, normalized)
                ?? throw new InvalidOperationException($"Rollback evidence is missing: {normalized}");
        }

        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            SnapshotHashJsonOpts,
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
            relativePath =>
            {
                var normalized = relativePath.Replace('\\', '/');
                return rollbackContents.TryGetValue(normalized, out var bytes)
                    ? bytes
                    : null;
            });

        await _fs.WriteFileAtomicAsync(
            writeLease,
            BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, JsonOpts));
        await _fs.WriteFileAtomicAsync(
            writeLease,
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityJson);
    }

    private IEnumerable<string> EnumerateRollbackBaselineFiles(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allFiles = _fs.EnumerateFiles(writeLease, "*");
        foreach (var relative in allFiles)
        {
            if (relative.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetExtension(relative), ".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!ShouldSkipRollbackBaselineFile(relative))
                    files.Add(relative);
                continue;
            }

            if (relative.StartsWith("lore/", StringComparison.OrdinalIgnoreCase) &&
                !relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(relative);
            }
        }

        foreach (var outputFile in new[]
                 {
                     "output/narrative_response.json",
                     "output/interface_updates.json",
                     "output/debug_logs.json",
                     QteSceneService.QteOfferPath
                 })
        {
            if (_fs.FileExists(writeLease, outputFile))
                files.Add(outputFile);
        }

        return files;
    }

    private static bool ShouldSkipRollbackBaselineFile(string relative) =>
        string.Equals(relative, ValidationRepairReadyPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, ValidationRepairRequestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, TerminalProtocolFailureRequestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, LocalUiSessionLockService.LockPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith($"{BrowserPendingTurnInspector.PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith($"{BrowserPendingTurnInspector.ExplorerRollbackDirectory}/", StringComparison.OrdinalIgnoreCase) ||
        relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase);

    private async Task SnapshotFileIfPresentAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        var content = await _fs.ReadFileBytesAsync(writeLease, relativePath);
        if (content == null)
            return;

        var snapshotPath = $"{BrowserPendingTurnInspector.PendingTurnSnapshotDirectory}/{relativePath}";
        await _fs.WriteFileAtomicBytesAsync(writeLease, snapshotPath, content);
        files[relativePath] = snapshotPath;
        snapshotHashes[relativePath] = PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private async Task<Dictionary<string, string>> CaptureClientOwnedValidationHashesAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/history/chat_log.json"] = await ReadFileHashOrEmptyAsync(
                writeLease,
                "game_state/history/chat_log.json")
        };

        foreach (var storyPath in EnumerateStoryContinuityFiles(writeLease))
            result[storyPath] = await ReadFileHashOrEmptyAsync(writeLease, storyPath);

        return result;
    }

    private async Task<string> ReadFileHashOrEmptyAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath)
    {
        var content = await _fs.ReadFileAsync(writeLease, relativePath);
        return content == null ? string.Empty : PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles(
        FileSystemManager.CanonicalWriteLease writeLease) =>
        _fs.EnumerateFiles(writeLease, "*.jsonl")
            .Where(path => path.StartsWith("stories/", StringComparison.OrdinalIgnoreCase));

    private void CleanupQueuedTurnArtifacts(FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.DeleteFile(writeLease, BrowserPendingTurnInspector.TurnRequestPath);
        _fs.DeleteFile(writeLease, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath);
        _fs.DeleteFile(writeLease, PendingTurnSnapshotAuthority.AuthorityPath);
        _fs.DeleteDirectoryTree(
            writeLease,
            BrowserPendingTurnInspector.PendingTurnSnapshotDirectory);
    }

    private sealed class BrowserPendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }
}

internal sealed record BrowserQueuedTurnRequest(
    string SessionId,
    string RequestId,
    int TurnNumber,
    string PlayerAction);
