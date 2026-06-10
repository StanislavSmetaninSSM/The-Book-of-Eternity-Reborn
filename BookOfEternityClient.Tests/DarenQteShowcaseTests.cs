using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class DarenQteShowcaseTests : IDisposable
{
    private static readonly string[] RequiredBeatIds =
    [
        "approach_manor",
        "gadget_infiltration",
        "stealth_crossing",
        "lock_pick",
        "rune_memory",
        "physical_pressure",
        "timed_rhythm",
        "route_decision",
        "staff_theft",
        "pursuit",
        "chase_chain",
        "hideout_return"
    ];

    private static readonly string[] RequiredQteTypes =
    [
        "TimingBar",
        "PromptChain",
        "BalanceMeter",
        "ChargeRelease",
        "BranchChoice",
        "MashInput",
        "PatternMemory",
        "RhythmPulse",
        "PrecisionChoice",
        "StealthNoise",
        "LockPinSet"
    ];

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _qte;
    private readonly DarenQteRewardProfileService _profile;
    private readonly QteWebInteractionService _web;

    public DarenQteShowcaseTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-daren-qte-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var characteristics = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        _qte = new QteSceneService(
            _fs,
            settings,
            characteristics,
            null!,
            null!,
            null!,
            null!,
            null!,
            stateManager,
            NullLogger<QteSceneService>.Instance);
        _profile = new DarenQteRewardProfileService(_fs);
        _web = new QteWebInteractionService(_fs, _qte);
    }

    [Fact]
    public void DarenRouteDefinition_IncludesRequiredStoryBeatsAndQteTypes()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        Assert.Equal("daren_qte_showcase", route.RouteId);
        Assert.Equal(RequiredBeatIds, route.Beats.Select(beat => beat.BeatId));
        Assert.All(route.Beats, beat =>
        {
            Assert.False(string.IsNullOrWhiteSpace(beat.Title));
            Assert.False(string.IsNullOrWhiteSpace(beat.PlayerText));
            Assert.DoesNotContain("GM", beat.PlayerText, StringComparison.OrdinalIgnoreCase);
        });

        var routeTypes = route.Offer.Chapters
            .SelectMany(chapter => chapter.Actions)
            .Select(action => action.Check.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(RequiredQteTypes.OrderBy(type => type, StringComparer.OrdinalIgnoreCase), routeTypes);
        Assert.Equal("approach_manor", route.Offer.StartChapterId);
        Assert.Contains("Дарен", route.Offer.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, 100, null, 0)]
    [InlineData(true, 39, null, 0)]
    [InlineData(true, 40, "shadow_on_the_run", 1)]
    [InlineData(true, 54, "shadow_on_the_run", 1)]
    [InlineData(true, 55, "broken_trail", 2)]
    [InlineData(true, 74, "broken_trail", 2)]
    [InlineData(true, 75, "clean_heist", 4)]
    [InlineData(true, 89, "clean_heist", 4)]
    [InlineData(true, 90, "perfect_shadow", 6)]
    public void DarenEndingResolver_UsesExactThresholdsAndBonuses(bool reachedHideout, int score, string? expectedTierId, int expectedBonus)
    {
        var ending = DarenQteRewardProfileService.ResolveEnding(reachedHideout, score);

        Assert.Equal(expectedTierId, ending.TierId);
        Assert.Equal(expectedBonus, ending.InkFeatherBonus);
        if (expectedTierId == null)
        {
            Assert.False(ending.GrantsReward);
            Assert.Equal("no_reward_failure", ending.OutcomeId);
        }
        else
        {
            Assert.True(ending.GrantsReward);
            Assert.False(string.IsNullOrWhiteSpace(ending.DisplayName));
        }
    }

    [Fact]
    public async Task DarenProfile_WritesBestTierAndNeverDowngradesOrStacks()
    {
        var first = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 75),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        var worse = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 55),
            new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc));
        var same = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 75),
            new DateTime(2026, 6, 11, 3, 0, 0, DateTimeKind.Utc));
        var upgrade = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 4, 0, 0, DateTimeKind.Utc));

        Assert.True(first.Updated);
        Assert.False(worse.Updated);
        Assert.False(same.Updated);
        Assert.True(upgrade.Updated);

        var profile = await _profile.ReadProfileAsync();
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("perfect_shadow", profile.DarenShowcase?.BestTierId);
        Assert.Equal("Идеальная тень", profile.DarenShowcase?.BestTierName);
        Assert.Equal(6, profile.DarenShowcase?.InkFeatherBonus);
        Assert.Equal(90, profile.DarenShowcase?.BestScore);
    }

    [Fact]
    public async Task DarenProfile_NormalizesDuplicateAndCorruptRecordsBeforeGranting()
    {
        await WriteClientProfileAsync("""
        {
          "schemaVersion": 1,
          "darenShowcase": {
            "bestTierId": "shadow_on_the_run",
            "bestTierName": "Тень в бегах",
            "inkFeatherBonus": -20,
            "bestScore": 41,
            "completedAtUtc": "2026-06-11T01:00:00Z",
            "source": "daren_qte_showcase"
          },
          "darenShowcases": [
            {
              "bestTierId": "clean_heist",
              "bestTierName": "Чистая кража",
              "inkFeatherBonus": 999,
              "bestScore": 82,
              "completedAtUtc": "2026-06-11T02:00:00Z",
              "source": "daren_qte_showcase"
            },
            {
              "bestTierId": "unknown_shadow",
              "bestTierName": "Unknown",
              "inkFeatherBonus": 50,
              "bestScore": 100,
              "completedAtUtc": "2026-06-11T03:00:00Z",
              "source": "daren_qte_showcase"
            }
          ]
        }
        """);

        var profile = await _profile.ReadProfileAsync();

        Assert.Equal("clean_heist", profile.DarenShowcase?.BestTierId);
        Assert.Equal("Чистая кража", profile.DarenShowcase?.BestTierName);
        Assert.Equal(4, profile.DarenShowcase?.InkFeatherBonus);
        Assert.Equal(82, profile.DarenShowcase?.BestScore);
    }

    [Fact]
    public async Task DarenNewGameReward_AppliesBestTierOnceToFreshSoulStateOnly()
    {
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        var soulRoot = JsonNode.Parse("""
        {
          "soulName": "Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "inkFeathers": { "current": 0, "total": 0 }
        }
        """)!.AsObject();

        var first = await _profile.ApplyBestRewardToNewSoulStateAsync(soulRoot);
        var second = await _profile.ApplyBestRewardToNewSoulStateAsync(soulRoot);

        Assert.True(first.Granted);
        Assert.Equal("Идеальная тень", first.TierName);
        Assert.Contains("Дарен", first.PlayerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("6", first.PlayerMessage, StringComparison.Ordinal);
        Assert.False(second.Granted);
        var inkFeathers = soulRoot["inkFeathers"]!.AsObject();
        var grants = soulRoot["clientRewardGrants"]!.AsObject();
        var darenGrant = grants["darenQteShowcase"]!.AsObject();
        Assert.Equal(6, inkFeathers["current"]!.GetValue<int>());
        Assert.Equal(6, inkFeathers["total"]!.GetValue<int>());
        Assert.Equal("daren_qte_showcase", darenGrant["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task DarenShowcaseAttempt_ReachesRewardEndingWithoutCampaignMutation()
    {
        WriteCampaignSentinels();
        var before = SnapshotGameSessionFiles();

        var attempt = _qte.StartDarenShowcaseAttempt();
        QteSceneService.QteActionResolution? resolution = null;
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(item.ChapterId, attempt.ActiveScene.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            resolution = await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                "success",
                completedAtUtc: new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        }

        var after = SnapshotGameSessionFiles();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("perfect_shadow", resolution!.Completion!.OutcomeId);
        Assert.Contains("Идеальная тень", resolution.Completion.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);
        Assert.True(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenShowcaseAttempt_PreHideoutFailureNeverWritesPermanentRewardEvenWithHighScore()
    {
        WriteCampaignSentinels();
        var before = SnapshotGameSessionFiles();

        var attempt = _qte.StartDarenShowcaseAttempt();
        QteSceneService.QteActionResolution? resolution = null;
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(item.ChapterId, attempt.ActiveScene.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            var grade = string.Equals(chapter.ChapterId, "gadget_infiltration", StringComparison.OrdinalIgnoreCase)
                ? "fail"
                : "success";
            resolution = await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                grade,
                completedAtUtc: new DateTime(2026, 6, 11, 1, 30, 0, DateTimeKind.Utc));
        }

        var after = SnapshotGameSessionFiles();
        var profile = await _profile.ReadProfileAsync();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("no_reward_failure", resolution!.Completion!.OutcomeId);
        Assert.Equal("no_reward_failure", resolution.Completion.ScoreSummary?.Rank?.Id);
        Assert.Equal("Провал вылазки", resolution.Completion.ScoreSummary?.Rank?.Label);
        Assert.DoesNotContain("Чистая кража", resolution.Completion.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(attempt.Ending!.GrantsReward);
        Assert.Null(profile.DarenShowcase);
        Assert.False(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        Assert.Equal(before, after);
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenBrowserState_UsesExistingQteProjectionAndCSharpRewardAuthority()
    {
        var intro = await _web.BuildDarenShowcaseStateAsync();

        Assert.Equal("Intro", intro.State);
        Assert.Contains("Дарен", intro.IntroTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("отдель", intro.BoundaryNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start", intro.AvailableOperations);

        var started = await _web.StartDarenShowcaseAsync();
        Assert.Equal("Active", started.State);
        Assert.NotNull(started.ActiveScene);
        var firstAction = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        Assert.Contains(firstAction.CheckType, RequiredQteTypes);

        var resolved = await _web.ResolveDarenShowcaseActionAsync(new DarenShowcaseActionRequest(firstAction.ActionId, "success"));
        Assert.Equal("Active", resolved.State);
        Assert.NotNull(resolved.Resolution);
        Assert.Contains("submitAction", resolved.AvailableOperations);
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void DarenShowcaseDocsAndSourceGuards_PreserveClientOwnedBoundary()
    {
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var mainMenuSource = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.MainMenu.cs");
        var darenSource = ReadRepoFile("BookOfEternityClient", "Services", "QteSceneService.Daren.cs");

        foreach (var requiredText in new[]
        {
            "Daren showcase",
            "client-owned",
            "not a GM-authored QTE offer",
            "New Game",
            "Ink Feather",
            "QTE Practice Mode must not grant Daren rewards"
        })
        {
            Assert.Contains(requiredText, apiSpec, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(requiredText, qteRules, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(requiredText, qteExample, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("RunDarenShowcaseModeAsync", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("ApplyBestRewardToNewSoulStateAsync", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("clientRewardGrants", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("Ограбление поместья Дареном", darenSource, StringComparison.Ordinal);
        Assert.Contains("Начать вылазку", darenSource, StringComparison.Ordinal);
        Assert.Contains("Чернильных Перьев", darenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", darenSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", darenSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manual-grade", darenSource, StringComparison.OrdinalIgnoreCase);

        var productionGrantCallSites = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("ApplyBestRewardToNewSoulStateAsync", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(["BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs", "BookOfEternityClient/Services/DarenQteRewardProfileService.cs"], productionGrantCallSites);
    }

    private async Task WriteClientProfileAsync(string json)
    {
        var profilePath = Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, json);
    }

    private void WriteCampaignSentinels()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """{ "inkFeathers": { "current": 17, "total": 17 } }""");
        WriteSessionFile("game_state/player/experience.json", """{ "experience": 345, "level": 4 }""");
        WriteSessionFile("game_state/inventory/items.json", """{ "items": [{ "id": "sentinel-staff", "quantity": 1 }] }""");
        WriteSessionFile("game_state/quests/active_quests.json", """{ "quests": [{ "id": "main", "stage": "before_daren" }] }""");
        WriteSessionFile("game_state/control/pending_campaign_action.json", """{ "kind": "ordinary-turn", "status": "pending" }""");
        WriteSessionFile("game_state/history/chat_log.json", """{ "turns": [{ "turnNumber": 7 }] }""");
        WriteSessionFile("game_state/meta/afterlife_state.json", """{ "state": "untouched" }""");
    }

    private void WriteSessionFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(_rootPath, "game_session", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private Dictionary<string, string> SnapshotGameSessionFiles() =>
        Directory.EnumerateFiles(Path.Combine(_rootPath, "game_session"), "*", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(Path.Combine(_rootPath, "game_session"), path).Replace('\\', '/'), Contents: File.ReadAllText(path)))
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Path, item => item.Contents, StringComparer.OrdinalIgnoreCase);

    private void AssertNoCampaignQteFiles()
    {
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteOfferPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteHistoryPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
