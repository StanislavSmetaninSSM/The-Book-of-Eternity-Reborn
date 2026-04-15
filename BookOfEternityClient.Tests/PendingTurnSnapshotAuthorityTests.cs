using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnSnapshotAuthorityTests : IDisposable
{
    private sealed class TestManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions ManifestJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public PendingTurnSnapshotAuthorityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-pending-snapshot-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_TamperedRollbackBackup_FailsClosed()
    {
        const string logicalPath = "game_state/meta/soul_state.json";
        const string snapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string rollbackPath = "game_state/meta/soul_state.json.rollback.test";

        await _fs.WriteFileAtomicAsync(snapshotPath, """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync(rollbackPath, """
        {
          "currentRealm": "Mortal World"
        }
        """);

        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 7,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = snapshotPath
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = ComputeSha256(File.ReadAllText(_fs.ResolvePath(snapshotPath), Encoding.UTF8))
            },
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = rollbackPath
            },
            RollbackBaselineFiles = new List<string> { logicalPath },
            SourceLabel = "authority-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            ManifestJsonOpts,
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
            ReadRelativeFile);

        await _fs.WriteFileAtomicAsync(rollbackPath, """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        var valid = PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
            manifest,
            authorityJson,
            ManifestJsonOpts,
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
            out var failureCode);

        Assert.False(valid);
        Assert.Equal("detached_authority_mismatch", failureCode);
    }

    [Fact]
    public void TryValidateManifestAgainstAuthority_UnsafeSnapshotPath_FailsStructure()
    {
        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 8,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "../outside.json"
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "ABC123"
            },
            RollbackBaselineFiles = new List<string> { "game_state/meta/soul_state.json" },
            SourceLabel = "authority-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            ManifestJsonOpts,
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
            ReadRelativeFile);

        var valid = PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
            manifest,
            authorityJson,
            ManifestJsonOpts,
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
            out var failureCode);

        Assert.False(valid);
        Assert.Equal("invalid_manifest_structure", failureCode);
    }

    [Fact]
    public void HasValidatedSnapshotCoverage_MissingRequiredRollbackBaselineRegistration_FailsClosed()
    {
        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 9,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "ABC123"
            },
            RollbackBaselineFiles = new List<string>(),
            SourceLabel = "authority-tests"
        };

        var covered = PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(
            manifest,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            new[] { "game_state/meta/soul_state.json" },
            out var missingPath,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            requireRollbackBaselineRegistration: true);

        Assert.False(covered);
        Assert.Equal("game_state/meta/soul_state.json", missingPath);
    }

    private string? ReadRelativeFile(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    private static string ComputeManifestPayloadHash(TestManifest manifest)
    {
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
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
