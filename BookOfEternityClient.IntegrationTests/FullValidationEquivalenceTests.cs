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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static ValidationIssueSnapshot[] Snapshot(
        IEnumerable<ValidationIssue> issues) =>
        issues.Select(issue => new ValidationIssueSnapshot(
                issue.Severity,
                issue.Code,
                issue.FilePath,
                issue.Message))
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
        IssueSeverity Severity,
        string? Code,
        string FilePath,
        string Message);
}
