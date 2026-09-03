using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class DarenQteShowcaseTests : IDisposable
{
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

    private static readonly string[] ForbiddenPlayerFacingTechnicalTerms =
    [
        "GM",
        "DTO",
        "API",
        "endpoint",
        "debug",
        "Spec Kit",
        "manual-grade",
        "client-owned",
        "QTE",
        "score",
        "JSON",
        "files",
        "tests",
        "agent"
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
        _web = new QteWebInteractionService(
            _fs,
            _qte,
            new BrowserLocalWriteCoordinator(
                _fs,
                new LocalUiSessionLockService(_fs)));
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
        Assert.Contains("постоян", first.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("будущ", first.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Черниль", first.Message, StringComparison.OrdinalIgnoreCase);

        var profile = await _profile.ReadProfileAsync();
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("perfect_shadow", profile.DarenShowcase?.BestTierId);
        Assert.Equal("Идеальная тень", profile.DarenShowcase?.BestTierName);
        Assert.Equal(6, profile.DarenShowcase?.InkFeatherBonus);
        Assert.Equal(90, profile.DarenShowcase?.BestScore);
    }

    [Fact]
    public async Task ConsoleDarenCompletion_RecoversInterruptedBrowserProfileBeforeWritingNewReward()
    {
        const string trackedPath = "game_state/meta/daren-console-recovery.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(
                reachedHideout: true,
                normalizedScore: 55),
            new DateTime(2026, 7, 28, 1, 0, 0, DateTimeKind.Utc));

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "daren_console_recovery",
                rollbackExternalFileIds:
                [
                    ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId
                ]);
        }

        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(
                reachedHideout: true,
                normalizedScore: 75),
            new DateTime(2026, 7, 28, 2, 0, 0, DateTimeKind.Utc));

        var attempt = _qte.StartDarenShowcaseAttempt();
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(
                    item.ChapterId,
                    attempt.ActiveScene.CurrentChapterId,
                    StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                "success",
                completedAtUtc: new DateTime(
                    2026,
                    7,
                    28,
                    3,
                    0,
                    0,
                    DateTimeKind.Utc));
        }

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/daren-console-recovery-trigger.json",
            "{\"ok\":true}");

        var profile = await _profile.ReadProfileAsync();
        Assert.Equal("perfect_shadow", profile.DarenShowcase?.BestTierId);
        Assert.Equal(100, profile.DarenShowcase?.BestScore);
        Assert.False(_fs.FileExists(transaction.ManifestPath));
    }

    [Fact]
    public async Task DarenBrowserState_ExposesSharedBestRewardProfileSummary()
    {
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 82),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));

        var state = await _web.BuildDarenShowcaseStateAsync();

        Assert.NotNull(state.BestReward);
        var summary = RequiredStringProperty(state.BestReward!, "Summary");
        Assert.Contains("Чистая кража", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("82/100", summary, StringComparison.OrdinalIgnoreCase);
        AssertContainsInkFeatherAmountSignal("best reward summary", 4, summary);
        Assert.Contains("будущ", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("нов", summary, StringComparison.OrdinalIgnoreCase);
        AssertContainsAny("best reward permanence", summary, ["постоян", "сохран"]);
        AssertContainsAny("best reward non-stacking", summary, ["не склады", "не сумм", "один раз"]);
        AssertNoPlayerFacingTechnicalTerms("best reward summary", summary);
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
        Assert.NotNull(attempt.Ending);
        var epilogue = RequiredStringProperty(attempt.Ending!, "Epilogue");
        var rewardExplanation = RequiredStringProperty(attempt.Ending!, "RewardExplanation");
        Assert.Contains(epilogue, resolution.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains(rewardExplanation, resolution.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains(epilogue, resolution.Completion.Response.Response, StringComparison.Ordinal);
        Assert.Contains("Награда:", attempt.Feedback, StringComparison.Ordinal);
        Assert.DoesNotContain(rewardExplanation, attempt.Feedback, StringComparison.Ordinal);
        Assert.Equal(before, after);
        Assert.True(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenShowcaseAttempt_PreHideoutFailureUsesScoreTierAndWritesPermanentReward()
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
        var ending = attempt.Ending!;
        var expectedEnding = DarenQteRewardProfileService.ResolveEnding(
            reachedHideout: true,
            normalizedScore: ending.NormalizedScore);

        Assert.NotNull(resolution?.Completion);
        Assert.Equal(expectedEnding.OutcomeId, resolution!.Completion!.OutcomeId);
        Assert.Equal(expectedEnding.TierId, resolution.Completion.ScoreSummary?.Rank?.Id);
        Assert.Equal(expectedEnding.DisplayName, resolution.Completion.ScoreSummary?.Rank?.Label);
        Assert.Contains(expectedEnding.DisplayName, resolution.Completion.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(ending.GrantsReward);
        Assert.Equal(expectedEnding.InkFeatherBonus, ending.InkFeatherBonus);
        Assert.Equal(expectedEnding.TierId, profile.DarenShowcase?.BestTierId);
        Assert.True(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        Assert.Equal(before, after);
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenShowcaseAttempt_AllPartialValidCompletionCanReachShadowTier()
    {
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
                "partial",
                completedAtUtc: new DateTime(2026, 6, 11, 1, 45, 0, DateTimeKind.Utc));
        }

        var profile = await _profile.ReadProfileAsync();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("shadow_on_the_run", resolution!.Completion!.OutcomeId);
        Assert.Equal("shadow_on_the_run", resolution.Completion.ScoreSummary?.Rank?.Id);
        Assert.Equal("Тень в бегах", resolution.Completion.ScoreSummary?.Rank?.Label);
        Assert.True(attempt.Ending!.GrantsReward);
        Assert.Equal(1, attempt.Ending.InkFeatherBonus);
        Assert.Equal("shadow_on_the_run", profile.DarenShowcase?.BestTierId);
    }

    [Fact]
    public async Task DarenBrowserState_ReplayAfterPerfectKeepsBestFutureRewardSeparateFromLowerEnding()
    {
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));

        var intro = await _web.BuildDarenShowcaseStateAsync();
        var state = await _web.StartDarenShowcaseAsync(
            Assert.IsType<string>(intro.InteractionToken));
        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeAction = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await _web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    activeAction.ActionId,
                    "partial",
                    Assert.IsType<string>(state.InteractionToken)));
        }

        Assert.Equal("Completed", state.State);
        Assert.NotNull(state.BestReward);
        Assert.NotNull(state.Ending);
        Assert.Equal("perfect_shadow", state.BestReward!.TierId);
        Assert.Equal(6, state.BestReward.InkFeatherBonus);
        Assert.Equal("shadow_on_the_run", state.Ending!.TierId);
        Assert.Equal(1, state.Ending.InkFeatherBonus);
        Assert.Contains("луч", state.Ending.RewardMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 Черниль", state.Ending.RewardMessage, StringComparison.OrdinalIgnoreCase);

        var profileSummary = RequiredStringProperty(state.Ending, "RewardProfileSummary");
        Assert.Contains("Текущ", profileSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тень в бегах", profileSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идеальная тень", profileSummary, StringComparison.OrdinalIgnoreCase);
        AssertContainsInkFeatherAmountSignal("lower replay saved best summary", 6, profileSummary);
        Assert.Contains("будущ", profileSummary, StringComparison.OrdinalIgnoreCase);
        AssertContainsAny("lower replay non-downgrade", profileSummary, ["не замен", "не обмен", "не перепис"]);
        AssertContainsAny("lower replay non-stacking", profileSummary, ["не склады", "не сумм"]);
        AssertNoPlayerFacingTechnicalTerms("lower replay profile summary", profileSummary);
    }

    [Fact]
    public async Task DarenBrowserState_ExposesSharedEndingEpilogueAndRewardExplanation()
    {
        var intro = await _web.BuildDarenShowcaseStateAsync();
        var state = await _web.StartDarenShowcaseAsync(
            Assert.IsType<string>(intro.InteractionToken));
        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeAction = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await _web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    activeAction.ActionId,
                    "success",
                    Assert.IsType<string>(state.InteractionToken)));
        }

        Assert.Equal("Completed", state.State);
        Assert.NotNull(state.Ending);
        Assert.NotNull(state.Completion);
        var epilogue = RequiredStringProperty(state.Ending!, "Epilogue");
        var rewardExplanation = RequiredStringProperty(state.Ending!, "RewardExplanation");

        Assert.Equal("perfect_shadow", state.Ending!.TierId);
        Assert.True(state.Ending.GrantsReward);
        Assert.Contains(epilogue, state.Completion!.Summary, StringComparison.Ordinal);
        Assert.Contains(rewardExplanation, state.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains("Черниль", rewardExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("будущ", rewardExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DarenConsoleCompletion_ResponseIsStructuredForPlayerReadability()
    {
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
                completedAtUtc: new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc));
        }

        var response = resolution!.Completion!.Response.Response;
        Assert.False(string.IsNullOrWhiteSpace(response));
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Contains(lines, line => line.StartsWith("Итог:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.StartsWith("Счёт:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.StartsWith("Награда:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("\n\n", response, StringComparison.Ordinal);
        Assert.True(lines[0].Length <= 80, "The first result line should be a readable label, not a dense result paragraph.");
        Assert.True(lines.Length >= 5, "The final Daren response should be split into readable result, reward and epilogue blocks.");
        AssertNoPlayerFacingTechnicalTerms("Daren structured completion response", response);
    }

    [Fact]
    public async Task DarenBrowserState_UsesExistingQteProjectionAndCSharpRewardAuthority()
    {
        var intro = await _web.BuildDarenShowcaseStateAsync();

        Assert.Equal("Intro", intro.State);
        Assert.Contains("Дарен", intro.IntroTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("отдель", intro.BoundaryNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start", intro.AvailableOperations);

        var started = await _web.StartDarenShowcaseAsync(
            Assert.IsType<string>(intro.InteractionToken));
        Assert.Equal("Active", started.State);
        Assert.NotNull(started.ActiveScene);
        var firstAction = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        Assert.Contains(firstAction.CheckType, RequiredQteTypes);

        var resolved = await _web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(
                firstAction.ActionId,
                "success",
                Assert.IsType<string>(started.InteractionToken)));
        Assert.Equal("Active", resolved.State);
        Assert.NotNull(resolved.Resolution);
        Assert.Contains("submitAction", resolved.AvailableOperations);
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
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

    private static string RequiredStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.True(property != null, $"{instance.GetType().Name} must expose shared string property '{propertyName}'.");
        Assert.True(
            property!.PropertyType == typeof(string),
            $"{instance.GetType().Name}.{propertyName} must be a string property.");

        var value = property.GetValue(instance) as string;
        Assert.False(string.IsNullOrWhiteSpace(value),
            $"{instance.GetType().Name}.{propertyName} must be non-empty.");
        return value.Trim();
    }

    private static void AssertNoPlayerFacingTechnicalTerms(string context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var forbidden in ForbiddenPlayerFacingTechnicalTerms)
        {
            Assert.DoesNotContain(
                forbidden,
                value,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertContainsInkFeatherAmountSignal(string context, int amount, string value)
    {
        string[] amountSignals = amount switch
        {
            1 => ["1", "одн"],
            2 => ["2", "дв"],
            4 => ["4", "четыр"],
            6 => ["6", "шест"],
            _ => [amount.ToString(System.Globalization.CultureInfo.InvariantCulture)]
        };

        AssertContainsAny($"{context} Ink Feather amount", value, amountSignals);
    }

    private static void AssertContainsAny(string context, string value, IReadOnlyList<string> terms)
    {
        Assert.True(ContainsAny(value, terms),
            $"Daren chapter '{context}' needs one of these story signals: {string.Join(", ", terms)}.");
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

}
