using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal sealed class LiveTurnPreparationService
{
    internal const string TurnRequestPath = "input/turn_request.json";
    internal const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    internal const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string SourceLabel = "live-test prepare-turn helper";

    private static readonly string[] SnapshotRoots =
    {
        "game_state",
        "lore",
        "world_profiles"
    };

    private static readonly string[] OptionalOutputFiles =
    {
        "output/narrative_response.json",
        "output/interface_updates.json",
        "output/debug_logs.json",
        QteSceneService.QteOfferPath
    };

    internal static readonly JsonSerializerOptions ManifestJsonOptions = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    internal static readonly JsonSerializerOptions ManifestHashJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FileSystemManager _fs;

    public LiveTurnPreparationService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task<LiveTurnPreparationResult> PrepareAsync(LiveTurnPreparationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PlayerAction))
            throw new ArgumentException("Player action is required for prepare-turn.", nameof(options));

        CleanupPreparedTurnArtifacts();
        ClearStalePendingDiceState();

        var currentRealm = string.IsNullOrWhiteSpace(options.CurrentRealm)
            ? await ResolveCurrentRealmAsync()
            : options.CurrentRealm!.Trim();
        var request = new TurnRequest
        {
            SessionId = string.IsNullOrWhiteSpace(options.SessionId)
                ? $"live:{Environment.MachineName}:{Environment.ProcessId}"
                : options.SessionId!.Trim(),
            RequestId = string.IsNullOrWhiteSpace(options.RequestId)
                ? Guid.NewGuid().ToString("N")
                : options.RequestId!.Trim(),
            TurnNumber = options.TurnNumber.GetValueOrDefault(await ResolveNextTurnNumberAsync()),
            PlayerAction = options.PlayerAction.Trim(),
            Timestamp = string.IsNullOrWhiteSpace(options.Timestamp)
                ? DateTime.UtcNow.ToString("o")
                : options.Timestamp!.Trim(),
            GameMode = "normal",
            PreGeneratedDices1d20 = options.PreGeneratedDices1d20 is { Length: > 0 }
                ? options.PreGeneratedDices1d20
                : GameLoop.GenerateSecureRandomDice(),
            GachaBaseResult = GameLoop.ComputeGachaBase(GameLoop.GenerateSecureRandomDice(4)),
            AdditionalContext = new AdditionalContext
            {
                Urgency = "medium",
                ExpectedResponse = "process the live-test player action through the normal GM turn contract"
            },
            ProgressionControl = new ProgressionControl
            {
                CurrentRealm = currentRealm
            },
            SystemReminder = "Prepared by the live-test prepare-turn helper. Use this request as an ordinary player turn and keep all writes inside the game session contract."
        };
        request.AfterlifeSpiritualConflictPreview = await new AfterlifeSpiritualConflictTurnPreviewService(_fs)
            .BuildAsync(request.TurnNumber, request.PreGeneratedDices1d20, currentRealm);

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rollbackBaselineFiles = EnumerateSnapshotFiles()
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in rollbackBaselineFiles)
            await SnapshotFileIfPresentAsync(file, files, snapshotHashes);

        var manifest = new LiveTurnPendingSnapshotManifest
        {
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            TurnNumber = request.TurnNumber,
            RequestTimestamp = request.Timestamp,
            PlayerAction = request.PlayerAction,
            PreGeneratedDices1d20 = request.PreGeneratedDices1d20,
            GachaBaseResult = request.GachaBaseResult == null
                ? null
                : JsonSerializer.SerializeToNode(request.GachaBaseResult, ManifestJsonOptions) as JsonObject,
            ProgressionControl = request.ProgressionControl,
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync(),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = rollbackBaselineFiles,
            SourceLabel = SourceLabel
        };
        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            ManifestHashJsonOptions,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);

        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            ManifestHashJsonOptions,
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

        try
        {
            await _fs.WriteFileAtomicAsync(
                PendingTurnSnapshotManifestPath,
                JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await _fs.WriteFileAtomicAsync(PendingTurnSnapshotAuthority.AuthorityPath, authorityJson);
            await _fs.WriteFileAtomicAsync(TurnRequestPath, SerializeTurnRequestWithCurrentRealm(request, currentRealm));
        }
        catch
        {
            CleanupPreparedTurnArtifacts();
            throw;
        }

        return new LiveTurnPreparationResult(
            TurnRequestPath,
            PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath,
            request.SessionId,
            request.RequestId,
            request.TurnNumber,
            currentRealm,
            files.Count);
    }

    private string SerializeTurnRequestWithCurrentRealm(TurnRequest request, string currentRealm)
    {
        var root = JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)?.AsObject()
                   ?? new JsonObject();
        root["currentRealm"] = currentRealm;
        return root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private IEnumerable<string> EnumerateSnapshotFiles()
    {
        var sessionRoot = GetSessionRoot();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in SnapshotRoots)
        {
            var absoluteRoot = _fs.ResolvePath(root);
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (var absoluteFile in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (!TryGetSafeRelativePath(sessionRoot, absoluteFile, out var relative))
                    continue;
                if (ShouldExcludeSnapshotFile(relative))
                    continue;
                files.Add(relative);
            }
        }

        foreach (var outputFile in OptionalOutputFiles)
        {
            if (_fs.FileExists(outputFile))
                files.Add(outputFile);
        }

        return files;
    }

    private static bool ShouldExcludeSnapshotFile(string relative) =>
        string.Equals(relative, TurnRequestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith($"{PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("game_state/control/gm_context_pack/", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, PendingTurnStateService.PendingDiceStatePath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/gm_daemon_status.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/gm_trajectory_ledger.jsonl", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/validation_auto_rollback_report.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/control/local_ui_session_lock.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(relative, "game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
        relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase);

    private async Task SnapshotFileIfPresentAsync(
        string relativePath,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        if (content == null)
            return;

        var snapshotPath = $"{PendingTurnSnapshotDirectory}/{relativePath}";
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
        var sessionRoot = GetSessionRoot();
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            yield break;

        foreach (var absoluteFile in Directory.EnumerateFiles(storiesRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            if (TryGetSafeRelativePath(sessionRoot, absoluteFile, out var relative))
                yield return relative;
        }
    }

    private async Task<string> ResolveCurrentRealmAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (!string.IsNullOrWhiteSpace(soulJson))
        {
            try
            {
                var soul = JsonNode.Parse(soulJson)?.AsObject();
                var currentRealm = soul?["currentRealm"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(currentRealm))
                    return currentRealm.Trim();
            }
            catch
            {
                // Fall through to an explicit unknown marker; the validator will report bad state if needed.
            }
        }

        return "Unknown";
    }

    private async Task<int> ResolveNextTurnNumberAsync()
    {
        foreach (var path in new[]
                 {
                     "game_state/core/game_state.json",
                     "game_state/meta/soul_state.json"
                 })
        {
            var json = await _fs.ReadFileAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                var root = JsonNode.Parse(json)?.AsObject();
                var turnNumber = TryReadInt(root, "turnNumber") ?? TryReadInt(root, "currentTurnNumber");
                if (turnNumber.HasValue)
                    return Math.Max(1, turnNumber.Value + 1);
            }
            catch
            {
                // Try the next known state file.
            }
        }

        return 1;
    }

    private static int? TryReadInt(JsonObject? root, string propertyName)
    {
        if (root == null || !root.TryGetPropertyValue(propertyName, out var node) || node == null)
            return null;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }

    private string? ReadRelativeFileFromWorkspace(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    private void CleanupPreparedTurnArtifacts()
    {
        _fs.DeleteFile(TurnRequestPath);
        _fs.DeleteFile(PendingTurnSnapshotManifestPath);
        _fs.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
        DeleteDirectoryIfInsideSession(PendingTurnSnapshotDirectory);
    }

    private void ClearStalePendingDiceState() => _fs.DeleteFile(PendingTurnStateService.PendingDiceStatePath);

    private void DeleteDirectoryIfInsideSession(string relativeDirectory)
    {
        var sessionRoot = Path.GetFullPath(GetSessionRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = Path.GetFullPath(_fs.ResolvePath(relativeDirectory));
        var sessionRootPrefix = sessionRoot + Path.DirectorySeparatorChar;

        if (!target.StartsWith(sessionRootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete directory outside game session: {target}");

        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
    }

    private string GetSessionRoot() => Path.GetFullPath(_fs.ResolvePath(""));

    private static bool TryGetSafeRelativePath(string sessionRoot, string absoluteFile, out string relative)
    {
        relative = Path.GetRelativePath(sessionRoot, absoluteFile).Replace('\\', '/');
        return PendingTurnSnapshotAuthority.IsSafeRelativePath(relative);
    }
}

internal sealed class LiveTurnPreparationOptions
{
    public string? SessionId { get; init; }
    public string? RequestId { get; init; }
    public int? TurnNumber { get; init; }
    public string PlayerAction { get; init; } = string.Empty;
    public int[]? PreGeneratedDices1d20 { get; init; }
    public string? CurrentRealm { get; init; }
    public string? Timestamp { get; init; }
}

internal sealed record LiveTurnPreparationResult(
    string TurnRequestPath,
    string ManifestPath,
    string AuthorityPath,
    string SessionId,
    string RequestId,
    int TurnNumber,
    string CurrentRealm,
    int SnapshotFileCount);

internal sealed class LiveTurnPendingSnapshotManifest
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
