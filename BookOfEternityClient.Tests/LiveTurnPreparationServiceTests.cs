using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LiveTurnPreparationServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public LiveTurnPreparationServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-live-turn-prep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task PrepareAsync_WritesLinkedTurnRequestSnapshotAndAuthorityWithoutGeneratedHarnessArtifacts()
    {
        await WriteCanonicalAndGeneratedHarnessFilesAsync();

        var result = await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "live-session",
            RequestId = "live-request-002",
            TurnNumber = 2,
            PlayerAction = "Надеть руническую перчатку и изучить письмо.",
            PreGeneratedDices1d20 = new[] { 14, 8, 17 }
        });

        Assert.Equal("input/turn_request.json", result.TurnRequestPath);
        Assert.Equal("game_state/control/pending_turn_snapshot.json", result.ManifestPath);
        Assert.Equal(PendingTurnSnapshotAuthority.AuthorityPath, result.AuthorityPath);

        var turnRequest = await ReadJsonObjectAsync("input/turn_request.json");
        Assert.Equal("live-session", turnRequest["sessionId"]?.GetValue<string>());
        Assert.Equal("live-request-002", turnRequest["requestId"]?.GetValue<string>());
        Assert.Equal(2, turnRequest["turnNumber"]?.GetValue<int>());
        Assert.Equal("Надеть руническую перчатку и изучить письмо.", turnRequest["playerAction"]?.GetValue<string>());
        Assert.Equal("Mortal World", turnRequest["currentRealm"]?.GetValue<string>());
        Assert.Equal("Mortal World", turnRequest["progressionControl"]?["currentRealm"]?.GetValue<string>());
        Assert.Equal(new[] { 14, 8, 17 }, turnRequest["preGeneratedDices1d20"]?.AsArray().Select(node => node!.GetValue<int>()).ToArray());

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.False(string.IsNullOrWhiteSpace(manifestJson));
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(manifestJson!, LiveTurnPreparationService.ManifestJsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal("live-session", manifest.SessionId);
        Assert.Equal("live-request-002", manifest.RequestId);
        Assert.Equal(2, manifest.TurnNumber);
        Assert.Equal("Надеть руническую перчатку и изучить письмо.", manifest.PlayerAction);
        Assert.Equal("Mortal World", manifest.ProgressionControl?.CurrentRealm);
        Assert.Equal("live-test prepare-turn helper", manifest.SourceLabel);

        Assert.Contains("game_state/meta/soul_state.json", manifest.Files.Keys);
        Assert.Contains("game_state/world/current_location.json", manifest.Files.Keys);
        Assert.Contains("game_state/control/pending_ink_actions.json", manifest.Files.Keys);
        Assert.Contains("lore/codex_entries.json", manifest.Files.Keys);
        Assert.DoesNotContain("game_state/control/gm_bridge_status.json", manifest.Files.Keys);
        Assert.DoesNotContain("game_state/control/gm_daemon_status.json", manifest.Files.Keys);
        Assert.DoesNotContain("game_state/control/validation_repair_request.json", manifest.Files.Keys);
        Assert.DoesNotContain("game_state/control/terminal_protocol_failure_request.json", manifest.Files.Keys);
        Assert.DoesNotContain(manifest.Files.Keys, path => path.StartsWith("game_state/control/gm_context_pack/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.Files.Keys, path => path.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase));

        foreach (var path in manifest.Files.Keys
                     .Concat(manifest.Files.Values)
                     .Concat(manifest.SnapshotFileHashes.Keys)
                     .Concat(manifest.RollbackBaselineFiles))
        {
            AssertSafeNormalizedPath(path);
        }

        foreach (var (logicalPath, snapshotPath) in manifest.Files)
        {
            var content = await _fs.ReadFileAsync(snapshotPath);
            Assert.NotNull(content);
            Assert.True(manifest.SnapshotFileHashes.TryGetValue(logicalPath, out var expectedHash));
            Assert.Equal(expectedHash, PendingTurnSnapshotAuthority.ComputeSha256(content!));
        }

        var authorityJson = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        Assert.True(TryValidateReaderAuthority(manifest, authorityJson, out var failureCode), failureCode);
    }

    [Fact]
    public void LauncherAndQuickstartDocumentPrepareTurnCommand()
    {
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");
        var quickstart = ReadRepoFile("specs/1285-rlm-gm-harness/quickstart.md");

        Assert.Contains("\"prepare-turn\"", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--prepare-live-turn", launcher, StringComparison.Ordinal);
        Assert.Contains("--action", launcher, StringComparison.Ordinal);
        Assert.Contains("--dice", launcher, StringComparison.Ordinal);
        Assert.Contains("prepare-turn", quickstart, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_turn_snapshot.authority.json", quickstart, StringComparison.Ordinal);
    }

    private async Task WriteCanonicalAndGeneratedHarnessFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "room",
          "name": "Покои виконта"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_ink_actions.json", """
        {
          "actions": []
        }
        """);
        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/gm_bridge_status.json", """{"ready":true}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_daemon_status.json", """{"status":"running"}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/context.json", """{"generated":true}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_trajectory_ledger.jsonl", """{"turn":1}""");
        await _fs.WriteFileAtomicAsync("game_state/control/validation_repair_request.json", """{"issues":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/control/terminal_protocol_failure_request.json", """{"failure":true}""");
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/./meta/soul_state.json", """{"stale":true}""");
    }

    private async Task<JsonObject> ReadJsonObjectAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonNode.Parse(json!)!.AsObject();
    }

    private bool TryValidateReaderAuthority(
        LiveTurnPendingSnapshotManifest manifest,
        string? authorityJson,
        out string failureCode) =>
        PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
            manifest,
            authorityJson,
            LiveTurnPreparationService.ManifestHashJsonOptions,
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
            ReadRelativeFile,
            out _,
            out failureCode);

    private string? ReadRelativeFile(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath, Encoding.UTF8)
            : null;
    }

    private static void AssertSafeNormalizedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        Assert.Equal(normalized, path);
        Assert.DoesNotContain("/./", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("/../", normalized, StringComparison.Ordinal);
        Assert.False(normalized.StartsWith("./", StringComparison.Ordinal), normalized);
        Assert.False(normalized.StartsWith("../", StringComparison.Ordinal), normalized);
        Assert.True(PendingTurnSnapshotAuthority.IsSafeRelativePath(normalized), normalized);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = LocateRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string LocateRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "TheBookOfEternityReborn.sln")) ||
                File.Exists(Path.Combine(current, ".git")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
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
