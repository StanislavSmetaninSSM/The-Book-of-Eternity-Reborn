using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QtePracticeModeTests : IDisposable
{
    public static readonly string[] ImplementedPracticeTypes =
    [
        "BranchChoice",
        "TimingBar",
        "PromptChain",
        "BalanceMeter",
        "ChargeRelease",
        "MashInput",
        "PatternMemory",
        "RhythmPulse",
        "PrecisionChoice",
        "StealthNoise",
        "LockPinSet"
    ];

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _service;

    public QtePracticeModeTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-qte-practice-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new QteSceneService(
            _fs,
            new GameSettings(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<QteSceneService>.Instance);
    }

    public static IEnumerable<object[]> ImplementedTypes() =>
        ImplementedPracticeTypes.Select(type => new object[] { type });

    [Fact]
    public void PracticeCatalog_ListsOnlyImplementedTypesWithSessionOnlyBoundary()
    {
        var catalog = QteSceneService.GetPracticeCatalog();

        Assert.Equal(ImplementedPracticeTypes, catalog.Select(entry => entry.TypeId));
        Assert.All(catalog, entry =>
        {
            Assert.True(entry.Available);
            Assert.Null(entry.UnavailableReason);
            Assert.Contains("browser", entry.SupportedSurfaces);
            Assert.Contains("console", entry.SupportedSurfaces);
            Assert.Contains(entry.Difficulties, difficulty => difficulty.DifficultyId == "normal");

            var playerCopy = $"{entry.Description} {entry.Instructions}";
            Assert.Contains("без наград", playerCopy, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("сюжет", playerCopy, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [MemberData(nameof(ImplementedTypes))]
    public void PracticeAttempt_BuildsRealQteActionForEveryImplementedType(string typeId)
    {
        var attempt = _service.StartPracticeAttempt(typeId, "normal");

        Assert.Equal(typeId, attempt.TypeId);
        Assert.Equal("normal", attempt.DifficultyId);
        Assert.StartsWith("practice_", attempt.AttemptId, StringComparison.Ordinal);
        var offer = attempt.ActiveScene.Offer;
        Assert.NotNull(offer);
        var chapter = Assert.Single(offer!.Chapters);
        var action = Assert.Single(chapter.Actions);
        Assert.Equal(typeId, action.Check.Type);
        Assert.NotNull(action.Routing.Success.TerminalOutcomeId);
        Assert.NotNull(action.Routing.Partial.TerminalOutcomeId);
        Assert.NotNull(action.Routing.Fail.TerminalOutcomeId);
        Assert.NotNull(offer.ScoreModel);
        Assert.NotNull(action.ScoreDeltas);
        Assert.NotEmpty(action.ScoreDeltas!);

        AssertNoCampaignQteFiles();
    }

    [Theory]
    [MemberData(nameof(ImplementedTypes))]
    public async Task PracticeAttempt_GeneratedOfferHasValidQteConfigShape(string typeId)
    {
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var attempt = _service.StartPracticeAttempt(typeId, "normal");
        await _fs.WriteFileAtomicAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.Serialize(attempt.ActiveScene.Offer, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        var issues = await validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_", StringComparison.OrdinalIgnoreCase) == true &&
            !string.Equals(issue.Code, "qte_success_outcome_requires_xp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(issue.Code, "qte_missing_pending_manifest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PracticeAttempt_BranchChoiceUsesAuthoredChoiceGradeWhenBrowserGradeOmitted()
    {
        var attempt = _service.StartPracticeAttempt("BranchChoice", "normal");
        var action = Assert.Single(Assert.Single(attempt.ActiveScene.Offer!.Chapters).Actions);
        var authoredChoiceGrade = action.Check.Config?["choiceGrade"]?.GetValue<string>();

        var resolution = _service.ResolvePracticeAction(attempt, action.ActionId, submittedGrade: null);

        Assert.Equal(authoredChoiceGrade, resolution.Grade);
        Assert.Equal("Completed", resolution.State);
        Assert.NotNull(resolution.Completion);
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public void PracticeAttempt_ResolvesScoredAttemptWithoutMutatingCampaignOrRewardFiles()
    {
        WriteCampaignSentinels();
        var before = SnapshotFiles();

        var attempt = _service.StartPracticeAttempt("LockPinSet", "hard");
        var action = Assert.Single(Assert.Single(attempt.ActiveScene.Offer!.Chapters).Actions);
        var resolution = _service.ResolvePracticeAction(attempt, action.ActionId, submittedGrade: "success");
        var after = SnapshotFiles();

        Assert.Equal(before, after);
        Assert.Equal("Completed", resolution.State);
        Assert.Equal("success", resolution.Grade);
        Assert.NotNull(resolution.Completion);
        Assert.NotNull(resolution.Completion.ScoreSummary);
        Assert.NotNull(resolution.Completion.ScoreSummary.Rank);
        Assert.Contains("трениров", attempt.LocalScoreNotice, StringComparison.OrdinalIgnoreCase);
        AssertNoCampaignQteFiles();
    }

    private void WriteCampaignSentinels()
    {
        Write("game_state/meta/soul_state.json", """{ "inkFeathers": 17, "darenRewardState": { "status": "locked" } }""");
        Write("game_state/player/experience.json", """{ "experience": 345, "level": 4 }""");
        Write("game_state/player/inventory.json", """{ "items": [{ "id": "training-proof", "quantity": 1 }] }""");
        Write("game_state/quests/active_quests.json", """{ "quests": [{ "id": "main", "stage": "before_practice" }] }""");
        Write("game_state/control/pending_campaign_action.json", """{ "kind": "ordinary-turn", "status": "pending" }""");
        Write("game_state/core/turn_state.json", """{ "turnNumber": 12, "phase": "idle" }""");
    }

    private void Write(string relativePath, string contents)
    {
        var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private Dictionary<string, string> SnapshotFiles() =>
        Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(_rootPath, path).Replace('\\', '/'), Contents: File.ReadAllText(path)))
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
