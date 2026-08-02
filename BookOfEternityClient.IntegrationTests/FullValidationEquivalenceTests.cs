using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
