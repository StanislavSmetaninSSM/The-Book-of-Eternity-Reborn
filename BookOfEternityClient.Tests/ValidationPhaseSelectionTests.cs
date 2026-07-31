using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationPhaseSelectionTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;
    private readonly ValidationService _validator;

    public ValidationPhaseSelectionTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-validation-phase-selection-" + Guid.NewGuid().ToString("N"));
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
    public async Task ValidateGameStateAsync_EmptyPhaseSelection_FailsClosed()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _validator.ValidateGameStateAsync(GameStateValidationPhase.None));

        Assert.Equal("phases", exception.ParamName);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownPhaseSelection_FailsClosed()
    {
        var unknownPhase = (GameStateValidationPhase)(1u << 31);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _validator.ValidateGameStateAsync(unknownPhase));

        Assert.Equal("phases", exception.ParamName);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SinglePhase_DoesNotRunUnselectedPhase()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var jsonIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);
        var requiredFileIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        Assert.Contains(jsonIssues, issue => issue.Code == "invalid_json_file");
        Assert.DoesNotContain(requiredFileIssues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombinedSelection_PreservesCanonicalOrder()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var jsonIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);
        var requiredFileIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        var combinedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles |
            GameStateValidationPhase.JsonIntegrity);

        Assert.Equal(
            Snapshot(jsonIssues.Concat(requiredFileIssues)),
            Snapshot(combinedIssues));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombinedCrossReferenceSelection_DoesNotDuplicateRivalResidentIssues()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/world/rival_soul_arcs.json",
            "{");

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.CrossReferences |
            GameStateValidationPhase.RivalAndResidentCrossReferences);

        Assert.Single(
            issues,
            issue => issue.Code == "rival_arc_invalid_current_state");
    }

    [Fact]
    [Trait("Category", "FullValidation")]
    public async Task ValidateGameStateAsync_ExplicitAll_MatchesPublicFacade()
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
    public async Task ValidateGameStateAsync_ConsecutiveSelections_DoNotLeakPhaseState()
    {
        const string path = "game_state/misc/phase_selection_invalid.json";
        await _fileSystem.WriteFileAtomicAsync(path, "{");

        var firstIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);

        await _fileSystem.WriteFileAtomicAsync(path, "{}");
        var secondIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        Assert.Contains(firstIssues, issue => issue.Code == "invalid_json_file");
        Assert.DoesNotContain(secondIssues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_StateFileSelection_SkipsUnselectedFiles()
    {
        const string selectedPath = "game_state/meta/guardians.json";
        const string unselectedPath = "game_state/meta/soul_state.json";
        await _fileSystem.WriteFileAtomicAsync(selectedPath, "\"invalid root\"");
        await _fileSystem.WriteFileAtomicAsync(unselectedPath, "\"invalid root\"");

        var selection = new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { selectedPath });

        var issues = await _validator.ValidateGameStateAsync(selection);

        Assert.Contains(issues, issue =>
            issue.FilePath == selectedPath &&
            issue.Code == "flexible_state_invalid_root");
        Assert.DoesNotContain(issues, issue => issue.FilePath == unselectedPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static IReadOnlyList<ValidationIssueSnapshot> Snapshot(
        IEnumerable<ValidationIssue> issues)
    {
        return issues
            .Select(issue => new ValidationIssueSnapshot(
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
