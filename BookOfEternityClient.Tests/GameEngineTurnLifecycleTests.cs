using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineTurnLifecycleTests : IDisposable
{
    private sealed class PendingTurnSnapshotManifestPayload
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GameEngineTurnLifecycleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-gameengine-turnlifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_MortalPreTurnAndMortalCurrent_IsReadable()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Mortal World",
            out var failureDescription);

        Assert.False(invalid);
        Assert.Equal(string.Empty, failureDescription);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_AfterlifeCurrentRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Chaos Sea",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("currentRealm", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_UnreadablePreTurnRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            null,
            "Mortal World",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("pre-turn mortal realm authority", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleInvalidTriggerLifeEndRuntimeFailure_DeletesSignalAndWritesErrorLog()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        var exception = new GameEngine.TriggerLifeEndRuntimeContextException(
            "Canonical TriggerLifeEnd runtime flow requires mortal pre-turn realm authority.");

        GameEngine.HandleInvalidTriggerLifeEndRuntimeFailure(_fs, exception);

        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));

        var logPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.Contains("TriggerLifeEndRuntimeContextException", log, StringComparison.Ordinal);
        Assert.Contains("mortal pre-turn realm authority", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.True(resolution.IsAuthorized);
        Assert.Equal("authorized", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_InactiveManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("stale-session", "stale-request", 99, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("inactive_manifest", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_StructurallyInvalidManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var manifest = JsonNode.Parse(await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json")!)!.AsObject();
        manifest["snapshotFileHashes"] = new JsonObject();
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(JsonSerializer.Deserialize<PendingTurnSnapshotManifestPayload>(
            manifest.ToJsonString(),
            SnapshotHashJsonOpts)!);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("invalid_manifest", resolution.Code);
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(
            relativePath,
            JsonSerializer.Serialize(payload, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        params string[] trackedPaths)
    {
        var files = trackedPaths.ToDictionary(
            path => path,
            path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = trackedPaths.ToDictionary(
            path => path,
            path =>
            {
                var snapshotPath = _fs.ResolvePath($"game_state/control/pending_turn_snapshot/{path}");
                return ComputeSha256(File.ReadAllText(snapshotPath, Encoding.UTF8));
            },
            StringComparer.OrdinalIgnoreCase);

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "game-engine-turn-lifecycle-test",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = trackedPaths.ToList(),
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            requestTimestamp = manifest.RequestTimestamp,
            playerAction = manifest.PlayerAction,
            progressionControl = manifest.ProgressionControl,
            files,
            snapshotFileHashes = snapshotHashes,
            clientOwnedValidationHashes = manifest.ClientOwnedValidationHashes,
            rollbackBackups = manifest.RollbackBackups,
            rollbackBaselineFiles = manifest.RollbackBaselineFiles,
            sourceLabel = manifest.SourceLabel,
            manifestPayloadHash = manifest.ManifestPayloadHash
        });
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifestPayload manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = string.Empty;
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
