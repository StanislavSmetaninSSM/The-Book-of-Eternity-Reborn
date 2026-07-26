using System.Text;
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
            await WritePendingTurnSnapshotAsync(request, rollbackBackups);
            await _fs.WriteFileAtomicAsync(
                BrowserPendingTurnInspector.TurnRequestPath,
                JsonSerializer.Serialize(request, JsonOpts));
        }
        catch
        {
            CleanupQueuedTurnArtifacts();
            throw;
        }

        return new BrowserQueuedTurnRequest(request.SessionId, request.RequestId, request.TurnNumber, request.PlayerAction);
    }

    private async Task WritePendingTurnSnapshotAsync(
        TurnRequest request,
        IReadOnlyDictionary<string, string> rollbackBackups)
    {
        var baselineFiles = EnumerateRollbackBaselineFiles().ToHashSet(StringComparer.OrdinalIgnoreCase);
        baselineFiles.Add("game_state/meta/soul_state.json");

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in baselineFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            await SnapshotFileIfPresentAsync(file, files, snapshotHashes);

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
            ClientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync(),
            RollbackBackups = new Dictionary<string, string>(rollbackBackups, StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = baselineFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList(),
            SourceLabel = DirectGachaSourceLabel
        };
        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            SnapshotHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);

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
            ReadRelativeFileFromWorkspace);

        await _fs.WriteFileAtomicAsync(
            BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, JsonOpts));
        await _fs.WriteFileAtomicAsync(PendingTurnSnapshotAuthority.AuthorityPath, authorityJson);
    }

    private IEnumerable<string> EnumerateRollbackBaselineFiles()
    {
        var gameSessionRoot = _fs.ResolvePath("");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var absoluteFile in _fs.GetAllGameStateFiles())
        {
            var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
            if (ShouldSkipRollbackBaselineFile(relative))
                continue;

            files.Add(relative);
        }

        var loreRoot = _fs.ResolvePath("lore");
        if (Directory.Exists(loreRoot))
        {
            foreach (var absoluteFile in Directory.GetFiles(loreRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
                if (!relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
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
            if (_fs.FileExists(outputFile))
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
        string relativePath,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        if (content == null)
            return;

        var snapshotPath = $"{BrowserPendingTurnInspector.PendingTurnSnapshotDirectory}/{relativePath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, content);
        files[relativePath] = snapshotPath;
        snapshotHashes[relativePath] = PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private async Task<Dictionary<string, string>> CaptureClientOwnedValidationHashesAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/history/chat_log.json"] = await ReadFileHashOrEmptyAsync("game_state/history/chat_log.json")
        };

        foreach (var storyPath in EnumerateStoryContinuityFiles())
            result[storyPath] = await ReadFileHashOrEmptyAsync(storyPath);

        return result;
    }

    private async Task<string> ReadFileHashOrEmptyAsync(string relativePath)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        return content == null ? string.Empty : PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles()
    {
        var sessionRoot = _fs.ResolvePath("");
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            yield break;

        foreach (var absoluteFile in Directory.EnumerateFiles(storiesRoot, "*.jsonl", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(sessionRoot, absoluteFile).Replace('\\', '/');
    }

    private byte[]? ReadRelativeFileFromWorkspace(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private void CleanupQueuedTurnArtifacts()
    {
        _fs.DeleteFile(BrowserPendingTurnInspector.TurnRequestPath);
        _fs.DeleteFile(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath);
        _fs.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);

        var sessionRoot = Path.GetFullPath(_fs.ResolvePath("")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var snapshotDirectory = Path.GetFullPath(_fs.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotDirectory));
        var sessionRootPrefix = sessionRoot + Path.DirectorySeparatorChar;
        if (Directory.Exists(snapshotDirectory) &&
            snapshotDirectory.StartsWith(sessionRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(snapshotDirectory, recursive: true);
        }
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
