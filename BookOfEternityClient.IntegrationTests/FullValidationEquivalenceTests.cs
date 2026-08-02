using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "PreMergeSentinel")]
public sealed class FullValidationEquivalenceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;
    private readonly ValidationService _validator;

    public FullValidationEquivalenceTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-full-validation-equivalence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fileSystem.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fileSystem,
            NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ExplicitAll_MatchesPublicFacade_ForMalformedMultiErrorFixture()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var publicIssues = await _validator.ValidateGameStateAsync();
        var explicitAllIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.All);

        Assert.Equal(Snapshot(publicIssues), Snapshot(explicitAllIssues));
    }

    [Fact]
    public async Task ExplicitAll_MatchesPublicFacade_ForCanonicalValidFixture()
    {
        CopyDirectory(
            TestRepoPaths.BaseSessionRoot,
            _fileSystem.GameSessionPath);

        var publicIssues = await _validator.ValidateGameStateAsync();
        var explicitAllIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.All);

        Assert.Equal(Snapshot(publicIssues), Snapshot(explicitAllIssues));
        Assert.DoesNotContain(publicIssues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Snapshot_CapturesEveryPublicValidationIssueProperty()
    {
        var issueProperties = typeof(ValidationIssue)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var snapshotProperties = typeof(ValidationIssueSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(issueProperties, snapshotProperties);
    }

    [Fact]
    public void ExplicitAll_SelectsFactionMaterializationPhaseExactlyOnce()
    {
        var atomicPhases = Enum.GetValues<GameStateValidationPhase>()
            .Where(phase =>
                phase != GameStateValidationPhase.None &&
                phase != GameStateValidationPhase.All &&
                phase != GameStateValidationPhase.Selectable &&
                uint.IsPow2((uint)phase))
            .ToArray();

        Assert.Single(
            atomicPhases,
            phase => phase ==
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);
        Assert.True(
            GameStateValidationPhase.All.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
    }

    [Fact]
    public async Task ExplicitAll_RunsFactionMaterializationPhaseExactlyOnce()
    {
        const string factionPath = "game_state/factions/faction_core.json";
        await WriteValidatedSnapshotManifestAsync(
            factionPath,
            """
            {
              "factions": [
                {
                  "factionId": "faction_watch",
                  "materialization": {}
                }
              ]
            }
            """);

        var selectedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);
        var allIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.All);

        Assert.Single(selectedIssues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == factionPath);
        Assert.Single(allIssues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == factionPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static ValidationIssueSnapshot[] Snapshot(
        IEnumerable<ValidationIssue> issues) =>
        issues.Select(issue => new ValidationIssueSnapshot(
                issue.FilePath,
                issue.Severity,
                issue.Message,
                issue.Category,
                issue.Code,
                issue.Actor,
                issue.Section,
                issue.Expected,
                issue.Actual,
                issue.RepairHint))
            .ToArray();

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(
                sourceFile,
                Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)),
                overwrite: true);
        }

        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(
                sourceSubdirectory,
                Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory)));
        }
    }

    private async Task WriteValidatedSnapshotManifestAsync(
        string path,
        string json)
    {
        const string sessionId = "session_full_validation_equivalence";
        const string requestId = "request_full_validation_equivalence";
        const int turnNumber = 12;
        const string playerAction = "Validate phase runner equivalence.";

        await _fileSystem.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
        await _fileSystem.WriteFileAtomicAsync(snapshotPath, json);
        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-08-03T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = new JsonObject
            {
                [path] = snapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [path] = PendingTurnSnapshotAuthority.ComputeSha256(json)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(path),
            ["sourceLabel"] = "accepted faction turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fileSystem.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(
            _fileSystem);
    }

    private sealed record ValidationIssueSnapshot(
        string FilePath,
        IssueSeverity Severity,
        string Message,
        IssueCategory Category,
        string? Code,
        string? Actor,
        string? Section,
        string? Expected,
        string? Actual,
        string? RepairHint);
}
