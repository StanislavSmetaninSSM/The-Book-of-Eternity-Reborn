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

public sealed class BrowserShiningRelicForgeParityTests : IDisposable
{
    private const string OwnerId = "browser-shining-relic-forge-test";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;

    public BrowserShiningRelicForgeParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-shining-relic-forge-" + Guid.NewGuid().ToString("N"));
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
    [Trait("Category", "ShiningRelicForge")]
    public async Task ExecuteAsync_ShiningRelicForge_ReturnsPromptWithFactionActionRelicAndForgeChoices()
    {
        await SeedShiningRelicForgeStateAsync();

        var result = await ExecuteCommandAsync("/shining_relic_forge");

        Assert.True(result.State == CommandExecutionState.RequiresInput, CollectResultAndPromptText(result));
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);

        var factionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "faction_id"));
        Assert.Contains(factionPrompt.Options, option => option.Value == "faction_lanterns" && option.Label.Contains("Дом Фонарей", StringComparison.Ordinal));
        Assert.DoesNotContain(factionPrompt.Options, option => option.Value == "faction_hidden");

        var actionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "forge_action_type"));
        Assert.Contains(actionPrompt.Options, option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicReshape && option.Label.Contains("форм", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actionPrompt.Options, option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty && option.Label.Contains("свойств", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actionPrompt.Options, option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand && option.Label.Contains("усил", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actionPrompt.Options, option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho && option.Label.Contains("эх", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actionPrompt.Options, option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity && option.Label.Contains("редк", StringComparison.OrdinalIgnoreCase));

        var relicPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "relic_id"));
        Assert.Contains(relicPrompt.Options, option => option.Value == "relic_blade" && option.Label.Contains("Клинок Рассвета", StringComparison.Ordinal));
        Assert.Contains(relicPrompt.Options, option => option.Value == "relic_echo" && option.Label.Contains("Эхо Спутника", StringComparison.Ordinal));

        Assert.Contains(result.Prompts, prompt => prompt.Id == "target_form_tag");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "property_choice");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "replacement_property_choice");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "added_properties_choice");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "relic_rerolls_to_commit");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "confirm_shining_relic_forge_write");

        var detailAction = Assert.Single(result.Actions, action => action.Id == "shining-forge-relic-detail-relic_blade");
        Assert.Equal("/soul_relics реликвия relic_blade", detailAction.Command);
        Assert.Contains("Клинок Рассвета", detailAction.Label, StringComparison.Ordinal);
        Assert.False(detailAction.RequiresConfirmation);

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Ковка реликвий", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Черниль", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Искр", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task ExecuteAsync_ShiningRelicForge_WithForgeSupportQuotesDiscountedStrengthenAction()
    {
        await SeedShiningRelicForgeStateAsync(
            inkFeathers: 25,
            lightSparks: 15,
            residentRole: ShiningAbodeState.ResidentRoleForgeSupport);

        var result = await ExecuteCommandAsync("/shining_relic_forge");

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var actionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "forge_action_type"));
        var strengthenOption = Assert.Single(
            actionPrompt.Options,
            option => option.Value == ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand);
        Assert.False(strengthenOption.Disabled);
        Assert.Contains("25 Перьев", strengthenOption.Description, StringComparison.Ordinal);
        Assert.Contains("15 Искр", strengthenOption.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("30 Перьев", strengthenOption.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("20 Искр", strengthenOption.Description, StringComparison.Ordinal);

        var submitResult = await SubmitPromptAsync(
            result,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand),
                ("relic_id", "relic_blade"),
                ("property_choice", SelectPromptOptionValue(result, "property_choice", "Пламя Рассвета")),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, submitResult.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand, request["actionType"]!.GetValue<string>());
        Assert.Equal(25, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(15, request["quotedCostLightSparks"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgeReshape_WritesExistingForgeRequestAndCommitsRelicReroll()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicReshape),
                ("relic_id", "relic_blade"),
                ("target_form_tag", "lance"),
                ("relic_rerolls_to_commit", 1),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicReshape, request["actionType"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal("Дом Фонарей", request["factionName"]!.GetValue<string>());
        Assert.Equal("relic_blade", request["relicId"]!.GetValue<string>());
        Assert.Equal("Клинок Рассвета", request["relicName"]!.GetValue<string>());
        Assert.Equal("lance", request["targetFormTag"]!.GetValue<string>());
        Assert.Equal(10, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(10, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        Assert.Equal(1, ReadPendingRelicRerolls());
        Assert.Equal(1, ReadSpentRelicRerolls());
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgeRetune_WritesReplacementPropertyThroughCoreActionRequest()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        var propertyChoice = SelectPromptOptionValue(prompt, "property_choice", "Пламя Рассвета");
        var replacementChoice = SelectPromptOptionValue(prompt, "replacement_property_choice", "Печать Памяти");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty),
                ("relic_id", "relic_blade"),
                ("property_choice", propertyChoice),
                ("replacement_property_choice", replacementChoice),
                ("relic_rerolls_to_commit", 1),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty, request["actionType"]!.GetValue<string>());
        Assert.Equal("relic_blade", request["relicId"]!.GetValue<string>());
        Assert.Equal(0, request["propertyIndex"]!.GetValue<int>());
        Assert.Equal("memory_seal", request["replacementProperty"]!["propertyId"]!.GetValue<string>());
        Assert.Equal("rare", request["replacementProperty"]!["band"]!.GetValue<string>());
        Assert.Equal(20, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(15, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(1, ReadPendingRelicRerolls());
        Assert.Equal(1, ReadSpentRelicRerolls());
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgeStrengthen_WritesPropertyIndexAndQuotedCost()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        var propertyChoice = SelectPromptOptionValue(prompt, "property_choice", "Пламя Рассвета");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand),
                ("relic_id", "relic_blade"),
                ("property_choice", propertyChoice),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand, request["actionType"]!.GetValue<string>());
        Assert.Equal("relic_blade", request["relicId"]!.GetValue<string>());
        Assert.Equal(0, request["propertyIndex"]!.GetValue<int>());
        Assert.Equal(30, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(20, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(2, ReadPendingRelicRerolls());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgeStabilize_WritesCoreRequestWithoutBrowserRelicMutation()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho),
                ("relic_id", "relic_echo"),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho, request["actionType"]!.GetValue<string>());
        Assert.Equal("relic_echo", request["relicId"]!.GetValue<string>());
        Assert.Equal(25, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(15, request["quotedCostLightSparks"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgeUplift_WritesAddedPropertiesAndQuotedCost()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        var addedChoice = SelectPromptOptionValue(prompt, "added_properties_choice", "Печать Памяти");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity),
                ("relic_id", "relic_seed"),
                ("added_properties_choice", addedChoice),
                ("confirm_shining_relic_forge_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity, request["actionType"]!.GetValue<string>());
        Assert.Equal("relic_seed", request["relicId"]!.GetValue<string>());
        Assert.Equal(45, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(30, request["quotedCostLightSparks"]!.GetValue<int>());
        var addedProperties = request["addedProperties"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.NotEmpty(addedProperties);
        Assert.Equal("memory_seal", addedProperties[0]["propertyId"]!.GetValue<string>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Theory]
    [Trait("Category", "ShiningRelicForge")]
    [InlineData("/shining_relic_forge")]
    [InlineData("/сияющая_ковка")]
    public async Task ExecuteAsync_ShiningRelicForgeOutsideShiningAbode_ReturnsRealmBlockerWithoutPrompt(string command)
    {
        await SeedShiningRelicForgeStateAsync(realm: "Mortal World");

        var result = await ExecuteCommandAsync(command);

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgePromptAfterRealmSwitch_ReturnsBlockerWithoutPendingWrite()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        await SeedSoulRealmAsync("Mortal World");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicReshape),
                ("relic_id", "relic_blade"),
                ("target_form_tag", "lance"),
                ("confirm_shining_relic_forge_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgePromptAfterRelicRemoved_ReturnsBlockerWithoutPendingWrite()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        await RemoveRelicAsync("relic_blade");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicReshape),
                ("relic_id", "relic_blade"),
                ("target_form_tag", "lance"),
                ("confirm_shining_relic_forge_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgePromptAfterRerollEntitlementExhausted_ReturnsBlockerWithoutPendingWrite()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        await SetRelicRerollsAsync(0);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicReshape),
                ("relic_id", "relic_blade"),
                ("target_form_tag", "lance"),
                ("relic_rerolls_to_commit", 1),
                ("confirm_shining_relic_forge_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.Equal(0, ReadPendingRelicRerolls());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningRelicForge")]
    public async Task SubmitAsync_ShiningRelicForgePromptAfterAnotherPendingCoreActionAppears_ReturnsPlayerFacingBlocker()
    {
        await SeedShiningRelicForgeStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_relic_forge");
        await WriteBlockingPendingCoreActionRequestAsync();

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand),
                ("relic_id", "relic_blade"),
                ("property_choice", SelectPromptOptionValue(prompt, "property_choice", "Пламя Рассвета")),
                ("confirm_shining_relic_forge_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal("existing_blocker", request["requestId"]!.GetValue<string>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue813ShiningRelicForgeCommandIsCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        var command = Assert.Single(coverage.Commands, item => item.Id == "shining_relic_forge");
        Assert.Equal("covered", command.AuditStatus);
        Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
        Assert.Equal("guided-form", command.FormMode);
        Assert.Equal("player-default", command.Surface);
        Assert.DoesNotContain("#813", command.FollowUpIssue, StringComparison.Ordinal);
        AssertNoRawShiningDiagnosticText(command.PrimaryActionLabel + "\n" + command.Reason + "\n" + command.GapSummary);

        var treasury = Assert.Single(coverage.Commands, item => item.Id == "shining_treasury");
        Assert.DoesNotContain("#813", treasury.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", treasury.FollowUpIssue, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_ShiningRelicForgeCommandIsListedInAfterlifeHelp()
    {
        await SeedShiningRelicForgeStateAsync();

        var result = await ExecuteCommandAsync("/help");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("/shining_relic_forge", text, StringComparison.Ordinal);
        Assert.Contains("/сияющая_ковка", text, StringComparison.Ordinal);
        Assert.Contains("реликв", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ков", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AfterlifeShiningPlayerFacingSourceGuard")]
    public void BrowserAfterlifeWriteService_MustReuseExistingShiningForgeAuthority()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains("ShiningAbodeState.TryQuoteForgeAction", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.WriteForgeRequestWithRelicRerollCommitAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeForgeRelicReshape", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity", source, StringComparison.Ordinal);
    }

    private Task<ExplorerCommandResult> ExecuteCommandAsync(string command) =>
        _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: OwnerId,
            OwnerLabel: "Browser Shining relic forge test"));

    private Task<ExplorerCommandResult> SubmitPromptAsync(ExplorerCommandResult prompt, Dictionary<string, JsonNode?> answers) =>
        _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            AssertPromptSession(prompt).SessionId,
            answers,
            OwnerId: OwnerId));

    private static UiPromptSession AssertPromptSession(ExplorerCommandResult prompt)
    {
        Assert.True(prompt.State == CommandExecutionState.RequiresInput, CollectResultAndPromptText(prompt));
        return Assert.IsType<UiPromptSession>(prompt.InteractiveSession);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-shining-relic-forge-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedShiningRelicForgeStateAsync(
        string realm = "Shining Abode",
        int inkFeathers = 120,
        int lightSparks = 120,
        string residentRole = "council_supporter")
    {
        await SeedStoryTurnAsync(88);
        await SeedSoulRealmAsync(realm, inkFeathers);
        await SeedGuardiansAsync();

        var root = new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 700,
                ["tier"] = 4
            },
            ["lightSparks"] = lightSparks,
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
                CreateHall("hall_lanterns", "Зал Фонарей"),
                CreateHall("hall_hidden", "Скрытый зал")
            },
            ["factions"] = new JsonArray
            {
                CreateFaction("faction_lanterns", "Дом Фонарей", "hall_lanterns", visible: true),
                CreateFaction("faction_hidden", "Скрытый Дом", "hall_hidden", visible: false)
            },
            ["shiningPoliticalActors"] = new JsonArray()
        };
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var residents = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident("resident_alen", "Ален", "faction_lanterns", "Дом Фонарей", residentRole)
            }
        };
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residents.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedSoulRealmAsync(string realm, int inkFeathers = 120)
    {
        var root = new JsonObject
        {
            ["soulName"] = "Тестовая Душа",
            ["currentRealm"] = realm,
            ["currentIncarnation"] = 3,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = inkFeathers,
                ["total"] = inkFeathers
            },
            [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
            {
                ["relicRefinementEntitlements"] = new JsonObject
                {
                    ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
                    ["rerolls"] = 2,
                    ["rerollsSpent"] = 0,
                    ["freeShape"] = false,
                    ["freeRetune"] = false
                }
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray
                {
                    CreateRelic(
                        "relic_blade",
                        "Клинок Рассвета",
                        "blade",
                        ShiningAbodeState.RarityRare,
                        CreateProperty("dawn_flame", "Пламя Рассвета", ShiningAbodeState.EffectFamilyRelic, ShiningAbodeState.RarityRare))
                },
                ["stored"] = new JsonArray
                {
                    CreateRelic(
                        "relic_memory",
                        "Перстень Памяти",
                        "ring",
                        ShiningAbodeState.RarityRare,
                        CreateProperty("memory_seal", "Печать Памяти", ShiningAbodeState.EffectFamilyMemory, ShiningAbodeState.RarityRare)),
                    CreateRelic(
                        "relic_echo",
                        "Эхо Спутника",
                        "companion_echo",
                        ShiningAbodeState.RarityRare,
                        CreateProperty("companion_chord", "Созвучие Спутника", ShiningAbodeState.EffectFamilySocial, ShiningAbodeState.RarityRare),
                        GuardianAbodeResidentState.RelicTypeCompanionEcho),
                    CreateRelic(
                        "relic_seed",
                        "Зерно Света",
                        "seed",
                        ShiningAbodeState.RarityCommon,
                        CreateProperty("seed_core", "Ядро Семени", ShiningAbodeState.EffectFamilyResource, ShiningAbodeState.RarityCommon))
                }
            }
        };

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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

    private async Task RemoveRelicAsync(string relicId)
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        foreach (var collectionName in new[] { "equipped", "stored" })
        {
            var collection = root["soulRelics"]?[collectionName] as JsonArray;
            if (collection == null)
                continue;

            for (var index = collection.Count - 1; index >= 0; index--)
            {
                if (collection[index] is JsonObject relic &&
                    string.Equals(GetString(relic, "relicId", string.Empty), relicId, StringComparison.OrdinalIgnoreCase))
                {
                    collection.RemoveAt(index);
                }
            }
        }

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SetRelicRerollsAsync(int rerolls)
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        root[ShiningBlessingEffectState.SoulStateProperty]!["relicRefinementEntitlements"]!["rerolls"] = Math.Max(0, rerolls);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WriteBlockingPendingCoreActionRequestAsync()
    {
        var root = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "existing_blocker",
                    ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    ["createdAtTurn"] = 88,
                    ["createdAtUtc"] = "2026-06-07T00:00:00.0000000Z"
                }
            }
        };

        await _fs.WriteFileAtomicAsync(
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateHall(string hallId, string hallName) => new()
    {
        ["hallId"] = hallId,
        ["hallName"] = hallName,
        ["description"] = hallName,
        ["serviceTags"] = new JsonArray(ShiningAbodeState.HallServiceTagRelic)
    };

    private static JsonObject CreateFaction(string factionId, string factionName, string hallId, bool visible) => new()
    {
        ["factionId"] = factionId,
        ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
        ["hallId"] = hallId,
        ["isPlayerVisible"] = visible,
        ["playerVisible"] = visible,
        ["visibility"] = visible ? "public" : "hidden",
        ["factionStrength"] = 64,
        ["investCountThisAscension"] = 1,
        ["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        },
        ["charter"] = new JsonObject
        {
            ["factionName"] = factionName,
            ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
            ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
            ["summary"] = "Тестовая сияющая фракция кузнецов."
        },
        ["leadership"] = new JsonObject
        {
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] = ShiningAbodeState.HeadActorTypeResident,
            ["headActorId"] = "resident_alen"
        },
        ["projects"] = new JsonArray
        {
            new JsonObject
            {
                ["projectId"] = $"project_refinement_{factionId}",
                ["displayName"] = "Проект Огранки",
                ["summary"] = "Поддерживает кузню реликвий.",
                ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                ["tier"] = 2,
                ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                ["isSupported"] = true,
                ["strengthReward"] = 8,
                ["completedAtTurn"] = 77,
                ["completedAtUtc"] = "2026-06-01T00:00:00.0000000Z"
            }
        },
        [ShiningAbodeState.FactionChronicleProperty] = new JsonArray(),
        [ShiningAbodeState.FactionInfluenceProperty] = new JsonArray(),
        [ShiningAbodeState.FactionResourceLedgerProperty] = new JsonArray()
    };

    private static JsonObject CreateResident(
        string residentId,
        string displayName,
        string factionId,
        string factionName,
        string residentRole = "council_supporter") => new()
    {
        ["residentId"] = residentId,
        ["displayName"] = displayName,
        ["residentKind"] = "attendant_spirit",
        ["guardianId"] = "guardian_alpha",
        ["abodeId"] = "abode_alpha",
        ["isPresent"] = true,
        ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
        ["residentRole"] = residentRole,
        ["shiningFactionId"] = factionId,
        ["shiningFactionName"] = factionName,
        ["factionLoyaltyLevel"] = 70,
        ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(70),
        ["factionRestlessness"] = 15,
        ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(70, 15)
    };

    private static JsonObject CreateRelic(
        string relicId,
        string name,
        string formTag,
        string rarity,
        JsonObject property,
        string relicType = "soul_relic") => new()
    {
        ["relicId"] = relicId,
        ["name"] = name,
        ["formTag"] = formTag,
        ["quality"] = rarity,
        ["rarity"] = rarity,
        ["relicType"] = relicType,
        ["properties"] = new JsonArray(property)
    };

    private static JsonObject CreateProperty(string propertyId, string name, string stat, string band) => new()
    {
        ["propertyId"] = propertyId,
        ["name"] = name,
        ["stat"] = stat,
        ["band"] = band,
        ["description"] = $"Свойство {name}."
    };

    private JsonObject AssertSingleCoreActionRequest()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath).GetAwaiter().GetResult())!)!.AsObject();
        return Assert.Single(root[ShiningCoreActionRequestState.RequestsProperty]!.AsArray())!.AsObject();
    }

    private int ReadPendingRelicRerolls()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync("game_state/meta/soul_state.json").GetAwaiter().GetResult())!)!.AsObject();
        return root[ShiningBlessingEffectState.SoulStateProperty]?["relicRefinementEntitlements"]?["rerolls"]?.GetValue<int>() ?? 0;
    }

    private int ReadSpentRelicRerolls()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync("game_state/meta/soul_state.json").GetAwaiter().GetResult())!)!.AsObject();
        return root[ShiningBlessingEffectState.SoulStateProperty]?["relicRefinementEntitlements"]?["rerollsSpent"]?.GetValue<int>() ?? 0;
    }

    private static string SelectPromptOptionValue(ExplorerCommandResult result, string promptId, string labelFragment)
    {
        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, item => item.Id == promptId));
        return Assert.Single(
                prompt.Options,
                option => option.Label.Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
            .Value;
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

    private static string GetString(JsonObject? obj, string propertyName, string fallback) =>
        obj?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text ?? fallback
            : fallback;

    private static void AssertNoRawShiningDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending Shining", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("core action", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actionType", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forge_relic", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetFormTag", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("propertyIndex", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replacementProperty", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("addedProperties", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
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
