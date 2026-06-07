using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserShiningPoliticsParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;

    public BrowserShiningPoliticsParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-shining-politics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        var mortalWriteService = new BrowserMortalWorldWriteService(
            _fs,
            coordinator,
            new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
        var afterlifeWriteService = new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator);
        _promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: mortalWriteService,
            afterlifeWriteService: afterlifeWriteService);
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, _promptSessions);
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task ExecuteAsync_ShiningFactionFounding_ReturnsPromptWithCostsAndEligibleSupporters()
    {
        await SeedShiningPoliticsStateAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_founding",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        Assert.Contains(result.Prompts, prompt => prompt.Id == "faction_name");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "hall_name");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "charter_summary");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "hall_description");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "favored_archetype");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "patron_effect_family");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "supporting_resident_ids");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "confirm_shining_politics_write");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Основание сияющей фракции", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25", text, StringComparison.Ordinal);
        Assert.Contains("Черниль", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15", text, StringComparison.Ordinal);
        Assert.Contains("Искр", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мира", text, StringComparison.Ordinal);
        Assert.Contains("Кай", text, StringComparison.Ordinal);
        Assert.Contains("Солар", text, StringComparison.Ordinal);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionFounding_WritesPendingRequestAndReservesCosts()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_founding",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("faction_name", "Дом Зари"),
                ("hall_name", "Зал Зари"),
                ("charter_summary", "Новая фракция собирает тех, кто помнит утро."),
                ("hall_description", "Светлый зал для советов и обещаний."),
                ("favored_archetype", ShiningAbodeState.ProjectArchetypeAccord),
                ("patron_effect_family", ShiningAbodeState.EffectFamilySocial),
                ("hall_secondary_service_tag", ShiningAbodeState.HallServiceTagLore),
                ("supporting_resident_ids", "resident_mira, resident_kai, resident_solar"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleRequest(ShiningFactionRequestState.PendingFoundingsRequestPath);
        Assert.StartsWith("shining_founding_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("faction_", request["proposedFactionId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Зал Зари", request["proposedHallName"]!.GetValue<string>());
        Assert.Equal("Дом Зари", request["charter"]!["factionName"]!.GetValue<string>());
        Assert.Equal(ShiningFactionRequestState.FactionFoundingCostFeathers, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(ShiningFactionRequestState.FactionFoundingCostLightSparks, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(80, request["reservedInkFeathersBefore"]!.GetValue<int>());
        Assert.Equal(60, request["reservedLightSparksBefore"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        Assert.Equal(["resident_mira", "resident_kai", "resident_solar"], request["supportingResidentIds"]!.AsArray().Select(static item => item!.GetValue<string>()).ToArray());

        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shining = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        Assert.Equal(55, soul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal(45, shining["lightSparks"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task ExecuteAsync_ShiningFactionRealignment_ReturnsPromptWithReadyResidentsAndTargets()
    {
        await SeedShiningPoliticsStateAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        var residentPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "resident_id"));
        Assert.Contains(residentPrompt.Options, option => option.Value == "resident_ember" && option.Label.Contains("Эмбер", StringComparison.Ordinal));
        Assert.DoesNotContain(residentPrompt.Options, option => option.Value == "resident_mira");
        var modePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "realignment_mode"));
        Assert.Contains(modePrompt.Options, option => option.Value == ShiningFactionRequestState.RealignmentModeAcceptedTransfer);
        Assert.Contains(modePrompt.Options, option => option.Value == ShiningFactionRequestState.RealignmentModeDepartureToNeutral);
        var targetPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "target_faction_id"));
        Assert.Contains(targetPrompt.Options, option => option.Value == "faction_mirrors" && option.Label.Contains("Дом Зеркал", StringComparison.Ordinal));

        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task ExecuteAsync_ShiningFactionRealignment_IgnoresResidentsFromHiddenSourceFactions()
    {
        await SeedShiningPoliticsStateAsync();
        await AddHiddenPoliticsEntriesAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var residentPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "resident_id"));
        Assert.DoesNotContain(residentPrompt.Options, option => option.Value == "resident_shadow");
        Assert.DoesNotContain("Скрытый резидент", CollectResultAndPromptText(result), StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionRealignmentAfterTargetHidden_ReturnsBlockerWithoutPendingWrite()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment resident_ember",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SetFactionVisibilityAsync("faction_mirrors", visible: false);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_ember"),
                ("realignment_mode", ShiningFactionRequestState.RealignmentModeAcceptedTransfer),
                ("target_faction_id", "faction_mirrors"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionLeadership_RejectsHiddenRadiantActorWithoutPendingWrite()
    {
        await SeedShiningPoliticsStateAsync();
        await AddHiddenPoliticsEntriesAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_leadership faction_lanterns",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);
        var candidatePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "candidate_head_choice"));
        Assert.DoesNotContain(candidatePrompt.Options, option => option.Value == "radiant_actor:actor_hidden");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("transition_mode", ShiningFactionRequestState.TransitionModePeacefulSuccession),
                ("candidate_head_choice", "radiant_actor:actor_hidden"),
                ("supporting_resident_ids", "resident_kai"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionRealignment_WritesAcceptedTransferRequestWithTurn()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment resident_ember",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);
        var targetPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "target_faction_id"));
        Assert.DoesNotContain(targetPrompt.Options, option => option.Value == "faction_lanterns");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_ember"),
                ("realignment_mode", ShiningFactionRequestState.RealignmentModeAcceptedTransfer),
                ("target_faction_id", "faction_mirrors"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(ShiningFactionRequestState.PendingRealignmentsRequestPath);
        Assert.StartsWith("shining_realignment_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("resident_ember", request["residentId"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["sourceFactionId"]!.GetValue<string>());
        Assert.Equal("faction_mirrors", request["targetFactionId"]!.GetValue<string>());
        Assert.Equal(ShiningFactionRequestState.RealignmentModeAcceptedTransfer, request["realignmentMode"]!.GetValue<string>());
        Assert.Equal(12, request["factionLoyaltyLevel"]!.GetValue<int>());
        Assert.Equal(80, request["factionRestlessness"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionRealignment_WritesDepartureRequestWithoutTarget()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment resident_ember",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_ember"),
                ("realignment_mode", ShiningFactionRequestState.RealignmentModeDepartureToNeutral),
                ("target_faction_id", string.Empty),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(ShiningFactionRequestState.PendingRealignmentsRequestPath);
        Assert.Equal(ShiningFactionRequestState.RealignmentModeDepartureToNeutral, request["realignmentMode"]!.GetValue<string>());
        Assert.Equal(string.Empty, request["targetFactionId"]!.GetValue<string>());
        Assert.Equal(string.Empty, request["targetFactionName"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task ExecuteAsync_ShiningFactionLeadership_ReturnsPromptWithFactionCandidatesAndSupporters()
    {
        await SeedShiningPoliticsStateAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_leadership",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        var factionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "faction_id"));
        Assert.Contains(factionPrompt.Options, option => option.Value == "faction_lanterns" && option.Label.Contains("Дом Фонарей", StringComparison.Ordinal));
        var modePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "transition_mode"));
        Assert.Contains(modePrompt.Options, option => option.Value == ShiningFactionRequestState.TransitionModePeacefulSuccession);
        Assert.Contains(modePrompt.Options, option => option.Value == ShiningFactionRequestState.TransitionModeRevolt);
        var candidatePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "candidate_head_choice"));
        Assert.Contains(candidatePrompt.Options, option => option.Value == "resident:resident_mira" && option.Label.Contains("Мира", StringComparison.Ordinal));
        Assert.Contains(result.Prompts, prompt => prompt.Id == "supporting_resident_ids");
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningFactionLeadership_WritesPendingTransition()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_leadership faction_lanterns",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);
        var factionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "faction_id"));
        Assert.Equal(["faction_lanterns"], factionPrompt.Options.Select(static option => option.Value).ToArray());
        var candidatePrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, item => item.Id == "candidate_head_choice"));
        Assert.DoesNotContain(candidatePrompt.Options, option => option.Value == "resident:resident_echo");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("transition_mode", ShiningFactionRequestState.TransitionModePeacefulSuccession),
                ("candidate_head_choice", "resident:resident_mira"),
                ("supporting_resident_ids", "resident_kai, resident_solar"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleRequest(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);
        Assert.StartsWith("shining_leadership_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal(ShiningFactionRequestState.TransitionModePeacefulSuccession, request["transitionMode"]!.GetValue<string>());
        Assert.Equal(ShiningAbodeState.HeadActorTypeResident, request["incumbentHeadActorType"]!.GetValue<string>());
        Assert.Equal("resident_alen", request["incumbentHeadActorId"]!.GetValue<string>());
        Assert.Equal(ShiningAbodeState.HeadActorTypeResident, request["candidateHeadActorType"]!.GetValue<string>());
        Assert.Equal("resident_mira", request["candidateHeadActorId"]!.GetValue<string>());
        Assert.Equal(["resident_kai", "resident_solar"], request["supportingResidentIds"]!.AsArray().Select(static item => item!.GetValue<string>()).ToArray());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Theory]
    [Trait("Category", "ShiningPolitics")]
    [InlineData("/shining_faction_founding")]
    [InlineData("/shining_faction_realignment")]
    [InlineData("/shining_faction_leadership")]
    public async Task ExecuteAsync_ShiningPoliticsCommandOutsideShiningAbode_ReturnsRealmBlockerWithoutPrompt(string command)
    {
        await SeedShiningPoliticsStateAsync("Mortal World");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningPolitics")]
    public async Task SubmitAsync_ShiningPoliticsPromptAfterRealmSwitchToMortalWorld_ReturnsRealmBlockerWithoutPendingWrite()
    {
        await SeedShiningPoliticsStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_faction_realignment resident_ember",
            OwnerId: "browser-shining-politics-test",
            OwnerLabel: "Browser Shining politics test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SeedSoulRealmAsync("Mortal World");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(
                ("resident_id", "resident_ember"),
                ("realignment_mode", ShiningFactionRequestState.RealignmentModeAcceptedTransfer),
                ("target_faction_id", "faction_mirrors"),
                ("confirm_shining_politics_write", true)),
            OwnerId: "browser-shining-politics-test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue810ShiningPoliticsCommandsAreCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[] { "shining_faction_founding", "shining_faction_realignment", "shining_faction_leadership" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#810", command.FollowUpIssue, StringComparison.Ordinal);
            AssertNoRawShiningDiagnosticText(command.PrimaryActionLabel + "\n" + command.Reason + "\n" + command.GapSummary);
        }

        var politics = Assert.Single(coverage.Commands, item => item.Id == "shining_politics");
        Assert.DoesNotContain("#810", politics.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("founding, regrouping, and leadership actions remain tracked", politics.GapSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_ShiningPoliticsCommandsAreListedInAfterlifeHelp()
    {
        await SeedShiningPoliticsStateAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("/shining_faction_founding", text, StringComparison.Ordinal);
        Assert.Contains("/shining_faction_realignment", text, StringComparison.Ordinal);
        Assert.Contains("/shining_faction_leadership", text, StringComparison.Ordinal);
        Assert.Contains("основание", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("перестрой", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("глав", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AfterlifeShiningPlayerFacingSourceGuard")]
    public void BrowserAfterlifeWriteService_MustReuseExistingShiningPoliticsWriters()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains("WriteFoundingRequestAsync", source, StringComparison.Ordinal);
        Assert.Contains("WriteRealignmentRequestAsync", source, StringComparison.Ordinal);
        Assert.Contains("WriteLeadershipTransitionRequestAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateFoundingRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateRealignmentRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
        Assert.Contains("ValidateLeadershipTransitionRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-shining-politics-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedShiningPoliticsStateAsync(string realm = "Shining Abode")
    {
        await SeedStoryTurnAsync(88);
        await SeedSoulRealmAsync(realm);
        await SeedGuardiansAsync();

        var root = new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 140,
                ["tier"] = 4
            },
            ["lightSparks"] = 60,
            ["treasury"] = ShiningAbodeState.BuildDefaultTreasuryObject(),
            ["gates"] = new JsonObject
            {
                ["hasOpenDraft"] = false,
                ["draftVersion"] = 0,
                ["allCandidateBlessingCards"] = new JsonArray(),
                ["availableBlessingCards"] = new JsonArray(),
                ["shownBlessingCardIds"] = new JsonArray(),
                ["selectedBlessingCardIds"] = new JsonArray()
            },
            ["halls"] = new JsonArray
            {
                CreateHall("hall_lanterns", "Зал Фонарей", ShiningAbodeState.HallServiceTagSocial),
                CreateHall("hall_mirrors", "Зал Зеркал", ShiningAbodeState.HallServiceTagLore)
            },
            ["factions"] = new JsonArray
            {
                CreateFaction("faction_lanterns", "Дом Фонарей", "hall_lanterns", ShiningAbodeState.HeadActorTypeResident, "resident_alen", ShiningAbodeState.LeadershipStateContested, 58),
                CreateFaction("faction_mirrors", "Дом Зеркал", "hall_mirrors", ShiningAbodeState.HeadActorTypeRadiantActor, "actor_mirror", ShiningAbodeState.LeadershipStateSecure, 64)
            },
            ["shiningPoliticalActors"] = new JsonArray
            {
                new JsonObject
                {
                    ["actorId"] = "actor_mirror",
                    ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                    ["displayName"] = "Зеркальный старейшина",
                    ["politicalStatus"] = ShiningAbodeState.PoliticalStatusHead,
                    ["currentFactionId"] = "faction_mirrors",
                    ["summary"] = "Ведёт Дом Зеркал."
                }
            }
        };
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var residents = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident("resident_alen", "Ален", "faction_lanterns", "Дом Фонарей", 74, 18),
                CreateResident("resident_ember", "Эмбер", "faction_lanterns", "Дом Фонарей", 12, 80),
                CreateResident("resident_mira", "Мира", "faction_lanterns", "Дом Фонарей", 72, 12),
                CreateResident("resident_kai", "Кай", "faction_lanterns", "Дом Фонарей", 65, 20),
                CreateResident("resident_solar", "Солар", "faction_lanterns", "Дом Фонарей", 69, 16),
                CreateResident("resident_echo", "Эхо Зеркал", "faction_mirrors", "Дом Зеркал", 66, 18)
            }
        };
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residents.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedSoulRealmAsync(string realm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Тестовая Душа",
          "currentRealm": {{JsonSerializer.Serialize(realm)}},
          "currentIncarnation": 3,
          "inkFeathers": {
            "current": 80,
            "total": 80
          }
        }
        """);
    }

    private async Task SeedGuardiansAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "domain": "Порог Сна",
            "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
          },
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель", "abodePower": 72 }
            }
          ]
        }
        """);
    }

    private static JsonObject CreateHall(string hallId, string hallName, string serviceTag) => new()
    {
        ["hallId"] = hallId,
        ["hallName"] = hallName,
        ["description"] = hallName,
        ["serviceTags"] = new JsonArray(serviceTag)
    };

    private static JsonObject CreateFaction(
        string factionId,
        string factionName,
        string hallId,
        string headActorType,
        string headActorId,
        string leadershipState,
        int strength) => new()
    {
        ["factionId"] = factionId,
        ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
        ["hallId"] = hallId,
        ["isPlayerVisible"] = true,
        ["visibility"] = "public",
        ["factionStrength"] = strength,
        ["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        },
        ["charter"] = new JsonObject
        {
            ["factionName"] = factionName,
            ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
            ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
            ["summary"] = "Тестовая сияющая фракция."
        },
        ["leadership"] = new JsonObject
        {
            ["leadershipState"] = leadershipState,
            ["headActorType"] = headActorType,
            ["headActorId"] = headActorId
        },
        ["projects"] = new JsonArray(),
        [ShiningAbodeState.FactionChronicleProperty] = new JsonArray(),
        [ShiningAbodeState.FactionInfluenceProperty] = new JsonArray(),
        [ShiningAbodeState.FactionResourceLedgerProperty] = new JsonArray()
    };

    private static JsonObject CreateResident(
        string residentId,
        string displayName,
        string factionId,
        string factionName,
        int loyaltyLevel,
        int restlessness)
    {
        var realignmentState = ShiningAbodeState.ResolveFactionRealignmentState(loyaltyLevel, restlessness);
        return new JsonObject
        {
            ["residentId"] = residentId,
            ["displayName"] = displayName,
            ["residentKind"] = "attendant_spirit",
            ["guardianId"] = "guardian_alpha",
            ["abodeId"] = "abode_alpha",
            ["isPresent"] = true,
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["residentRole"] = "council_supporter",
            ["shiningFactionId"] = factionId,
            ["shiningFactionName"] = factionName,
            ["factionLoyaltyLevel"] = loyaltyLevel,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(loyaltyLevel),
            ["factionRestlessness"] = restlessness,
            ["factionRealignmentState"] = realignmentState
        };
    }

    private JsonObject AssertSingleRequest(string path)
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(path).GetAwaiter().GetResult())!)!.AsObject();
        return Assert.Single(root["requests"]!.AsArray())!.AsObject();
    }

    private async Task SetFactionVisibilityAsync(string factionId, bool visible)
    {
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        var faction = shiningRoot["factions"]!.AsArray()
            .OfType<JsonObject>()
            .First(item => string.Equals(item["factionId"]!.GetValue<string>(), factionId, StringComparison.OrdinalIgnoreCase));
        faction["isPlayerVisible"] = visible;
        faction["playerVisible"] = visible;
        faction["visibility"] = visible ? "public" : "hidden";
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task AddHiddenPoliticsEntriesAsync()
    {
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        var hiddenFaction = CreateFaction(
            "faction_wings",
            "Дом Скрытых Крыльев",
            "hall_mirrors",
            ShiningAbodeState.HeadActorTypeRadiantActor,
            "actor_hidden",
            ShiningAbodeState.LeadershipStateSecure,
            77);
        hiddenFaction["isPlayerVisible"] = false;
        hiddenFaction["playerVisible"] = false;
        hiddenFaction["visibility"] = "hidden";
        shiningRoot["factions"]!.AsArray().Add(hiddenFaction);
        shiningRoot["shiningPoliticalActors"]!.AsArray().Add(new JsonObject
        {
            ["actorId"] = "actor_hidden",
            ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
            ["displayName"] = "Скрытый легат",
            ["currentFactionId"] = "faction_lanterns",
            ["politicalStatus"] = ShiningAbodeState.PoliticalStatusHead,
            ["isPlayerVisible"] = false,
            ["playerVisible"] = false,
            ["visibility"] = "hidden"
        });
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var residentsRoot = JsonNode.Parse((await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!.AsObject();
        residentsRoot["entries"]!.AsArray().Add(CreateResident(
            "resident_shadow",
            "Скрытый резидент",
            "faction_wings",
            "Дом Скрытых Крыльев",
            8,
            90));
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residentsRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object Value)[] pairs)
    {
        var answers = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            answers[key] = value switch
            {
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonSerializer.SerializeToNode(value)
            };
        }

        return answers;
    }

    private static string CollectResultAndPromptText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks) + "\n" +
        string.Join("\n", result.Prompts.Select(CollectPromptText)) + "\n" +
        string.Join("\n", result.Notifications.Select(notification => $"{notification.Title}\n{notification.Message}"));

    private static string CollectPromptText(UiPrompt prompt)
    {
        var parts = new List<string> { prompt.Prompt };
        if (prompt is UiSelectionPrompt selection)
        {
            foreach (var option in selection.Options)
            {
                parts.Add(option.Label);
                parts.Add(option.Description);
            }
        }

        if (prompt is UiTextInputPrompt textInput)
            parts.Add(textInput.Placeholder);
        if (prompt is UiLongTextInputPrompt longTextInput)
            parts.Add(longTextInput.Placeholder);

        return string.Join("\n", parts);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);
        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
        }
    }

    private static void AssertNoRawShiningDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
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
            // Ignore temp cleanup failures.
        }
    }
}
