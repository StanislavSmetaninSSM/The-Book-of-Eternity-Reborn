using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QtePracticeWebInteractionTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteWebInteractionService _web;

    public QtePracticeWebInteractionTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-qte-practice-web-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var characteristics = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        var qte = new QteSceneService(
            _fs,
            settings,
            characteristics,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<QteSceneService>.Instance);
        _web = new QteWebInteractionService(
            _fs,
            qte,
            new BrowserLocalWriteCoordinator(
                _fs,
                new LocalUiSessionLockService(_fs)));
    }

    [Fact]
    public async Task BuildPracticeStateAsync_ListsCatalogWithoutCampaignSessionOrRuntimeWrites()
    {
        var before = SnapshotFiles();

        var state = await _web.BuildPracticeStateAsync();

        Assert.Equal("Catalog", state.State);
        Assert.Equal(QtePracticeModeTests.ImplementedPracticeTypes, state.Catalog.Select(entry => entry.TypeId));
        Assert.Null(state.ActiveScene);
        Assert.Contains("startAttempt", state.AvailableOperations);
        Assert.Contains("без наград", state.LocalScoreNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, SnapshotFiles());
    }

    [Fact]
    public async Task PracticeBrowserAttempt_ProjectsMiniGameAndCompletesWithLocalFeedbackOnly()
    {
        var catalog = await _web.BuildPracticeStateAsync();
        var started = await _web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest(
                "MashInput",
                "normal",
                catalog.InteractionToken));
        Assert.NotNull(started.ActiveScene);
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        var config = Assert.IsAssignableFrom<JsonObject>(action.CheckConfig);

        Assert.Equal("Active", started.State);
        Assert.Equal("MashInput", action.CheckType);
        Assert.True(action.RequiresSubmittedGrade);
        Assert.Equal("MashInput", config["kind"]!.GetValue<string>());
        Assert.Contains("submitAction", started.AvailableOperations);

        var completed = await _web.ResolvePracticeActionAsync(
            new QtePracticeActionRequest(
                action.ActionId,
                "success",
                started.InteractionToken));

        Assert.Equal("Completed", completed.State);
        Assert.Null(completed.ActiveScene);
        Assert.NotNull(completed.Completion);
        Assert.NotNull(completed.Completion.ScoreSummary);
        Assert.Contains("retry", completed.AvailableOperations);
        Assert.Contains("changeDifficulty", completed.AvailableOperations);
        Assert.Contains("chooseAnother", completed.AvailableOperations);
        Assert.Contains("exit", completed.AvailableOperations);
        Assert.Contains("без наград", completed.LocalScoreNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сюжет", completed.Feedback, StringComparison.OrdinalIgnoreCase);
        AssertNoCampaignQteFiles();
    }

    private Dictionary<string, string> SnapshotFiles() =>
        Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(_rootPath, path).Replace('\\', '/'), Contents: File.ReadAllText(path)))
            .Where(item => !item.Path.StartsWith(".boe_runtime/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Path, item => item.Contents, StringComparer.OrdinalIgnoreCase);

    private void AssertNoCampaignQteFiles()
    {
        Assert.False(File.Exists(Path.Combine(_rootPath, QteSceneService.QteOfferPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, QteSceneService.QteHistoryPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
