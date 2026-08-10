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

public sealed class BrowserShiningActionsParityTests : IDisposable
{
    private const string OwnerId = "browser-shining-actions-test";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;

    public BrowserShiningActionsParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-shining-actions-" + Guid.NewGuid().ToString("N"));
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
    [Trait("Category", "ShiningActions")]
    public async Task ExecuteAsync_ShiningNativeFactionDiscovery_ReturnsPromptWithCostsAndConfirmation()
    {
        await SeedShiningActionsStateAsync();

        var result = await ExecuteCommandAsync("/shining_native_faction_discovery");

        Assert.True(result.State == CommandExecutionState.RequiresInput, CollectResultAndPromptText(result));
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        Assert.Contains(result.Prompts, prompt => prompt.Id == "confirm_shining_core_action_write");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Открытие нативной фракции", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25", text, StringComparison.Ordinal);
        Assert.Contains("20", text, StringComparison.Ordinal);
        Assert.Contains("Черниль", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Искр", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningNativeFactionDiscovery_WritesExistingCoreActionRequest()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_native_faction_discovery");
        Assert.NotNull(prompt.InteractiveSession);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));

        var request = AssertSingleCoreActionRequest();
        Assert.StartsWith("shining_core_action_", request["requestId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction, request["actionType"]!.GetValue<string>());
        Assert.Equal(3, request["radianceTierAtRequest"]!.GetValue<int>());
        Assert.Equal(25, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(20, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task ExecuteAsync_ShiningFactionInvestment_ReturnsPromptWithVisibleEligibleFactionsAndCosts()
    {
        await SeedShiningActionsStateAsync();

        var result = await ExecuteCommandAsync("/shining_faction_investment");

        Assert.True(result.State == CommandExecutionState.RequiresInput, CollectResultAndPromptText(result));
        var factionPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "faction_id"));
        Assert.Contains(factionPrompt.Options, option => option.Value == "faction_lanterns" && option.Label.Contains("Дом Фонарей", StringComparison.Ordinal));
        Assert.DoesNotContain(factionPrompt.Options, option => option.Value == "faction_mirrors");
        Assert.DoesNotContain(factionPrompt.Options, option => option.Value == "faction_hidden");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "confirm_shining_core_action_write");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Инвестиция", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10", text, StringComparison.Ordinal);
        Assert.Contains("5", text, StringComparison.Ordinal);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningFactionInvestment_WritesExistingCoreActionRequest()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_faction_investment");
        Assert.NotNull(prompt.InteractiveSession);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("faction_id", "faction_lanterns"),
                ("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeInvestInFaction, request["actionType"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal("Дом Фонарей", request["factionName"]!.GetValue<string>());
        Assert.Equal(10, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(5, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task ExecuteAsync_ShiningProjectSupport_ReturnsOnlyVisibleCompletedUnsupportedProjects()
    {
        await SeedShiningActionsStateAsync();

        var result = await ExecuteCommandAsync("/shining_project_support");

        Assert.True(result.State == CommandExecutionState.RequiresInput, CollectResultAndPromptText(result));
        var projectPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "project_choice"));
        Assert.Contains(projectPrompt.Options, option => option.Value == "faction_lanterns|project_dawn" && option.Label.Contains("Проект Рассвета", StringComparison.Ordinal));
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_memory", StringComparison.Ordinal));
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_active", StringComparison.Ordinal));
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_hidden", StringComparison.Ordinal));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningProjectSupport_WritesExistingCoreActionRequest()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_project_support");
        Assert.NotNull(prompt.InteractiveSession);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("project_choice", "faction_lanterns|project_dawn"),
                ("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeSupportProject, request["actionType"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal("project_dawn", request["projectId"]!.GetValue<string>());
        Assert.Equal("Проект Рассвета", request["projectDisplayName"]!.GetValue<string>());
        Assert.Equal(0, request["quotedCostFeathers"]!.GetValue<int>());
        Assert.Equal(0, request["quotedCostLightSparks"]!.GetValue<int>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task ExecuteAsync_ShiningProjectUnsupport_ReturnsOnlyVisibleSupportedProjects()
    {
        await SeedShiningActionsStateAsync();

        var result = await ExecuteCommandAsync("/shining_project_unsupport");

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var projectPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "project_choice"));
        Assert.Contains(projectPrompt.Options, option => option.Value == "faction_lanterns|project_memory" && option.Label.Contains("Проект Памяти", StringComparison.Ordinal));
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_dawn", StringComparison.Ordinal));
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_hidden", StringComparison.Ordinal));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningProjectUnsupport_WritesExistingCoreActionRequest()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_project_unsupport");
        Assert.NotNull(prompt.InteractiveSession);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("project_choice", "faction_lanterns|project_memory"),
                ("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeUnsupportProject, request["actionType"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal("project_memory", request["projectId"]!.GetValue<string>());
        Assert.Equal("Проект Памяти", request["projectDisplayName"]!.GetValue<string>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningProjectRetirement_WritesExistingCoreActionRequest()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_project_retirement");
        Assert.NotNull(prompt.InteractiveSession);
        var projectPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(prompt.Prompts, prompt => prompt.Id == "project_choice"));
        Assert.Contains(projectPrompt.Options, option => option.Value == "faction_lanterns|project_dawn");
        Assert.Contains(projectPrompt.Options, option => option.Value == "faction_lanterns|project_memory");
        Assert.DoesNotContain(projectPrompt.Options, option => option.Value.Contains("project_active", StringComparison.Ordinal));

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("project_choice", "faction_lanterns|project_dawn"),
                ("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeRetireProject, request["actionType"]!.GetValue<string>());
        Assert.Equal("faction_lanterns", request["factionId"]!.GetValue<string>());
        Assert.Equal("project_dawn", request["projectId"]!.GetValue<string>());
        Assert.Equal("Проект Рассвета", request["projectDisplayName"]!.GetValue<string>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Theory]
    [Trait("Category", "ShiningActions")]
    [InlineData("/shining_native_faction_discovery")]
    [InlineData("/shining_faction_investment")]
    [InlineData("/shining_project_support")]
    [InlineData("/shining_project_unsupport")]
    [InlineData("/shining_project_retirement")]
    public async Task ExecuteAsync_ShiningActionCommandOutsideShiningAbode_ReturnsRealmBlockerWithoutPrompt(string command)
    {
        await SeedShiningActionsStateAsync("Mortal World");

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
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningActionPromptAfterRealmSwitchToMortalWorld_ReturnsRealmBlockerWithoutPendingWrite()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_project_support");
        Assert.NotNull(prompt.InteractiveSession);
        await SeedSoulRealmAsync("Mortal World");

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("project_choice", "faction_lanterns|project_dawn"),
                ("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningActions")]
    public async Task SubmitAsync_ShiningActionPromptAfterAnotherPendingCoreActionAppears_ReturnsPlayerFacingBlocker()
    {
        await SeedShiningActionsStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_project_support");
        Assert.NotNull(prompt.InteractiveSession);
        await WriteBlockingPendingCoreActionRequestAsync();

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("project_choice", "faction_lanterns|project_dawn"),
                ("confirm_shining_core_action_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        var text = CollectResultAndPromptText(result);
        AssertNoRawShiningDiagnosticText(text);
        var request = AssertSingleCoreActionRequest();
        Assert.Equal("existing_blocker", request["requestId"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue811ShiningActionsCommandsAreCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[]
                 {
                     "shining_native_faction_discovery",
                     "shining_faction_investment",
                     "shining_project_support",
                     "shining_project_unsupport",
                     "shining_project_retirement"
                 })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#811", command.FollowUpIssue, StringComparison.Ordinal);
            AssertNoRawShiningDiagnosticText(command.PrimaryActionLabel + "\n" + command.Reason + "\n" + command.GapSummary);
        }

        foreach (var commandId in new[] { "shining_abode", "shining_treasury" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.DoesNotContain("#811", command.FollowUpIssue, StringComparison.Ordinal);
            Assert.DoesNotContain("project", command.GapSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("проект", command.GapSummary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_ShiningActionsCommandsAreListedInAfterlifeHelp()
    {
        await SeedShiningActionsStateAsync();

        var result = await ExecuteCommandAsync("/help");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("натив", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("влож", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("поддерж", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("истор", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AfterlifeShiningPlayerFacingSourceGuard")]
    public void BrowserAfterlifeWriteService_MustReuseExistingShiningCoreActionWriter()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains("ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.WriteRequestAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeInvestInFaction", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeSupportProject", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeUnsupportProject", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypeRetireProject", source, StringComparison.Ordinal);
    }

    private Task<ExplorerCommandResult> ExecuteCommandAsync(string command) =>
        _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: OwnerId,
            OwnerLabel: "Browser Shining actions test"));

    private Task<ExplorerCommandResult> SubmitPromptAsync(ExplorerCommandResult prompt, Dictionary<string, JsonNode?> answers) =>
        _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            answers,
            OwnerId: OwnerId));

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-shining-actions-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedShiningActionsStateAsync(string realm = "Shining Abode")
    {
        await SeedStoryTurnAsync(88);
        await SeedSoulRealmAsync(realm);
        await SeedGuardiansAsync();

        var lanterns = CreateFaction("faction_lanterns", "Дом Фонарей", "hall_lanterns", investCount: 1);
        lanterns["projects"] = new JsonArray
        {
            CreateProject("project_dawn", "Проект Рассвета", ShiningAbodeState.ProjectStatusCompleted, supported: false),
            CreateProject("project_memory", "Проект Памяти", ShiningAbodeState.ProjectStatusCompleted, supported: true),
            CreateProject("project_active", "Проект В Работе", "active", supported: false)
        };
        var capped = CreateFaction("faction_mirrors", "Дом Зеркал", "hall_mirrors", investCount: 3);
        capped["projects"] = new JsonArray
        {
            CreateProject("project_mirror", "Проект Зеркал", ShiningAbodeState.ProjectStatusCompleted, supported: false)
        };
        var hidden = CreateFaction("faction_hidden", "Скрытый Дом", "hall_hidden", investCount: 0);
        hidden["isPlayerVisible"] = false;
        hidden["playerVisible"] = false;
        hidden["visibility"] = "hidden";
        hidden["projects"] = new JsonArray
        {
            CreateProject("project_hidden", "Скрытый проект", ShiningAbodeState.ProjectStatusCompleted, supported: false)
        };
        ShiningFactionTestMaterialization.Apply(
            lanterns,
            materializedAtTurn: 88,
            hasResidentAffiliations: true,
            canTrade: false);
        ShiningFactionTestMaterialization.Apply(
            capped,
            materializedAtTurn: 88,
            hasResidentAffiliations: false,
            canTrade: false);
        ShiningFactionTestMaterialization.Apply(
            hidden,
            materializedAtTurn: 88,
            hasResidentAffiliations: false,
            canTrade: false);

        var root = new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 450,
                ["tier"] = 3
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
                CreateHall("hall_lanterns", "Зал Фонарей"),
                CreateHall("hall_mirrors", "Зал Зеркал"),
                CreateHall("hall_hidden", "Скрытый зал")
            },
            ["factions"] = new JsonArray { lanterns, capped, hidden },
            ["shiningPoliticalActors"] = new JsonArray()
        };
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var residents = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident("resident_alen", "Ален", "faction_lanterns", "Дом Фонарей"),
                CreateResident("resident_mira", "Мира", "faction_lanterns", "Дом Фонарей")
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

    private async Task WriteBlockingPendingCoreActionRequestAsync()
    {
        var root = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "existing_blocker",
                    ["actionType"] = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
                    ["radianceTierAtRequest"] = 3,
                    ["quotedCostFeathers"] = 25,
                    ["quotedCostLightSparks"] = 20,
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
        ["serviceTags"] = new JsonArray(ShiningAbodeState.HallServiceTagSocial)
    };

    private static JsonObject CreateFaction(
        string factionId,
        string factionName,
        string hallId,
        int investCount) => new()
    {
        ["factionId"] = factionId,
        ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
        ["hallId"] = hallId,
        ["isPlayerVisible"] = true,
        ["visibility"] = "revealed",
        ["baseStrength"] = 58,
        ["factionStrength"] = 58,
        ["investCountThisAscension"] = investCount,
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
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] = ShiningAbodeState.HeadActorTypeResident,
            ["headActorId"] = "resident_alen"
        },
        ["projects"] = new JsonArray(),
        [ShiningAbodeState.FactionChronicleProperty] = new JsonArray(),
        [ShiningAbodeState.FactionInfluenceProperty] = new JsonArray(),
        [ShiningAbodeState.FactionResourceLedgerProperty] = new JsonArray()
    };

    private static JsonObject CreateProject(string projectId, string displayName, string status, bool supported) => new()
    {
        ["projectId"] = projectId,
        ["displayName"] = displayName,
        ["summary"] = "Тестовый сияющий проект.",
        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
        ["tier"] = 1,
        ["status"] = status,
        ["isSupported"] = supported,
        ["strengthReward"] = 5,
        ["completedAtTurn"] = 77,
        ["completedAtUtc"] = "2026-06-01T00:00:00.0000000Z"
    };

    private static JsonObject CreateResident(
        string residentId,
        string displayName,
        string factionId,
        string factionName) => new()
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
        ["factionLoyaltyLevel"] = 70,
        ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(70),
        ["factionRestlessness"] = 15,
        ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(70, 15)
    };

    private JsonObject AssertSingleCoreActionRequest()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath).GetAwaiter().GetResult())!)!.AsObject();
        return Assert.Single(root[ShiningCoreActionRequestState.RequestsProperty]!.AsArray())!.AsObject();
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
        UiTestTextCollector.CollectResultAndPromptText(result);

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
        Assert.DoesNotContain("pending Shining", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("core action", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actionType", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("discover_native_faction", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invest_in_faction", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("support_project", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsupport_project", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("retire_project", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no-op", text, StringComparison.OrdinalIgnoreCase);
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
