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

public sealed class BrowserShiningIncarnationGatesParityTests : IDisposable
{
    private const string OwnerId = "browser-shining-gates-test";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;

    public BrowserShiningIncarnationGatesParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-shining-gates-" + Guid.NewGuid().ToString("N"));
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
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesOpen_WritesExistingOpenGatesCoreActionRequest()
    {
        await SeedShiningGatesStateAsync(hasOpenDraft: false);
        var prompt = await ExecuteCommandAsync("/shining_gates_open");

        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.True(prompt.InteractiveSession?.RequiresLocalUiLock);
        Assert.Contains(prompt.Prompts, item => item.Id == "confirm_shining_core_action_write");
        var promptText = CollectResultAndPromptText(prompt);
        Assert.Contains("Врата", promptText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("благослов", promptText, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(promptText);

        var result = await SubmitPromptAsync(prompt, Answers(("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeOpenGates, request["actionType"]!.GetValue<string>());
        Assert.Equal(89, request["createdAtTurn"]!.GetValue<int>());
        Assert.False(request.ContainsKey("selectedCards"));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesSelectAndDeselect_MutatesOnlyExistingGatesState()
    {
        await SeedShiningGatesStateAsync();
        var selectPrompt = await ExecuteCommandAsync("/shining_gates_select");

        Assert.Equal(CommandExecutionState.RequiresInput, selectPrompt.State);
        var cardPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(selectPrompt.Prompts, item => item.Id == "blessing_card_id"));
        Assert.Contains(cardPrompt.Options, option => option.Value == "card_social" && option.Label.Contains("Песнь Рассвета", StringComparison.Ordinal));
        Assert.Contains(selectPrompt.Prompts, item => item.Id == "confirm_shining_gates_local_write");
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(selectPrompt));

        var selectResult = await SubmitPromptAsync(
            selectPrompt,
            Answers(
                ("blessing_card_id", "card_social"),
                ("confirm_shining_gates_local_write", true)));

        Assert.Equal(CommandExecutionState.Completed, selectResult.State);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.Equal(["card_social"], ReadSelectedBlessingCardIds());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(selectResult));

        var deselectPrompt = await ExecuteCommandAsync("/shining_gates_deselect");
        var selectedPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(deselectPrompt.Prompts, item => item.Id == "blessing_card_id"));
        Assert.Contains(selectedPrompt.Options, option => option.Value == "card_social" && option.Label.Contains("Песнь Рассвета", StringComparison.Ordinal));

        var deselectResult = await SubmitPromptAsync(
            deselectPrompt,
            Answers(
                ("blessing_card_id", "card_social"),
                ("confirm_shining_gates_local_write", true)));

        Assert.Equal(CommandExecutionState.Completed, deselectResult.State);
        Assert.Empty(ReadSelectedBlessingCardIds());
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(deselectResult));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesReroll_UsesExistingGatesRerollSemantics()
    {
        await SeedShiningGatesStateAsync();
        var before = ReadAvailableBlessingCardIds();
        var prompt = await ExecuteCommandAsync("/shining_gates_reroll");

        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.Contains(prompt.Prompts, item => item.Id == "confirm_shining_gates_local_write");
        Assert.Contains("обнов", CollectResultAndPromptText(prompt), StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(prompt));

        var result = await SubmitPromptAsync(prompt, Answers(("confirm_shining_gates_local_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var after = ReadAvailableBlessingCardIds();
        Assert.NotEqual(before, after);
        Assert.DoesNotContain("card_social", after);
        Assert.DoesNotContain("card_memory", after);
        Assert.Contains("card_route", after);
        Assert.Contains("card_relic", after);
        Assert.Equal(0, ReadGatesInt("rerollsRemaining"));
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningIncarnationPrepare_WritesPreparePackageRequestWithCanonicalSelectedSnapshot()
    {
        await SeedShiningGatesStateAsync(selectedIds: ["card_social", "card_memory"]);
        var prompt = await ExecuteCommandAsync("/shining_incarnation_prepare");

        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.Contains(prompt.Prompts, item => item.Id == "confirm_shining_core_action_write");
        var promptText = CollectResultAndPromptText(prompt);
        Assert.Contains("Песнь Рассвета", promptText, StringComparison.Ordinal);
        Assert.Contains("Память Эха", promptText, StringComparison.Ordinal);
        AssertNoRawShiningDiagnosticText(promptText);

        var result = await SubmitPromptAsync(prompt, Answers(("confirm_shining_core_action_write", true)));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
        var request = AssertSingleCoreActionRequest();
        Assert.Equal(ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, request["actionType"]!.GetValue<string>());
        Assert.Equal(4, request["sourceDraftVersion"]!.GetValue<int>());
        Assert.Equal(new[] { "card_social", "card_memory" }, request["selectedCardIds"]!.AsArray().Select(ReadString).ToArray());
        var selectedCards = request["selectedCards"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, selectedCards.Length);
        Assert.Equal("card_social", selectedCards[0]["cardId"]!.GetValue<string>());
        Assert.Equal("Песнь Рассвета", selectedCards[0]["displayName"]!.GetValue<string>());
        Assert.Equal("card_memory", selectedCards[1]["cardId"]!.GetValue<string>());
    }

    [Theory]
    [Trait("Category", "ShiningAbode")]
    [InlineData("/shining_gates_open")]
    [InlineData("/shining_gates_select")]
    [InlineData("/shining_gates_deselect")]
    [InlineData("/shining_gates_reroll")]
    [InlineData("/shining_incarnation_prepare")]
    public async Task ExecuteAsync_ShiningGatesCommandOutsideShiningAbode_ReturnsRealmBlockerWithoutMutation(string command)
    {
        await SeedShiningGatesStateAsync(realm: "Mortal World", selectedIds: ["card_social"]);

        var result = await ExecuteCommandAsync(command);

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Сияющей Обители", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.Equal(["card_social"], ReadSelectedBlessingCardIds());
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesSelectAfterDraftStales_ReturnsBlockerWithoutMutation()
    {
        await SeedShiningGatesStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_gates_select");
        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.NotNull(prompt.InteractiveSession);
        await MutateGatesAsync(gates => gates["isStale"] = true);

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("blessing_card_id", "card_social"),
                ("confirm_shining_gates_local_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.Empty(ReadSelectedBlessingCardIds());
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        var text = CollectResultAndPromptText(result);
        Assert.Contains("устар", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesSelectAfterCardDisappears_ReturnsBlockerWithoutMutation()
    {
        await SeedShiningGatesStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_gates_select");
        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.NotNull(prompt.InteractiveSession);
        await MutateGatesAsync(gates =>
        {
            gates["availableBlessingCards"] = new JsonArray(CreateCard("card_memory", "Память Эха", ShiningAbodeState.EffectFamilyMemory, ShiningAbodeState.RarityRare));
            gates["shownBlessingCardIds"] = new JsonArray("card_memory");
        });

        var result = await SubmitPromptAsync(
            prompt,
            Answers(
                ("blessing_card_id", "card_social"),
                ("confirm_shining_gates_local_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.Empty(ReadSelectedBlessingCardIds());
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task ExecuteAsync_ShiningGatesRerollWithoutRerolls_ReturnsPlayerFacingBlocker()
    {
        await SeedShiningGatesStateAsync(rerollsRemaining: 0);

        var result = await ExecuteCommandAsync("/shining_gates_reroll");

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("обнов", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task ExecuteAsync_ShiningGatesOpenWithPreparedPackage_ReturnsPlayerFacingBlocker()
    {
        await SeedShiningGatesStateAsync(hasOpenDraft: false, preparedPackage: true);

        var result = await ExecuteCommandAsync("/shining_gates_open");

        Assert.NotEqual(CommandExecutionState.RequiresInput, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("новую жизнь", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(text);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    [Trait("Category", "ShiningAbode")]
    public async Task SubmitAsync_ShiningGatesLocalMutationWithPendingCoreAction_ReturnsPlayerFacingBlocker()
    {
        await SeedShiningGatesStateAsync();
        var prompt = await ExecuteCommandAsync("/shining_gates_reroll");
        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        Assert.NotNull(prompt.InteractiveSession);
        await WriteBlockingPendingCoreActionRequestAsync();

        var result = await SubmitPromptAsync(prompt, Answers(("confirm_shining_gates_local_write", true)));

        Assert.NotEqual(CommandExecutionState.Completed, result.State);
        Assert.Equal(new[] { "card_social", "card_memory" }, ReadAvailableBlessingCardIds());
        Assert.Equal(1, ReadGatesInt("rerollsRemaining"));
        var request = AssertSingleCoreActionRequest();
        Assert.Equal("existing_blocker", request["requestId"]!.GetValue<string>());
        AssertNoRawShiningDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void BrowserCommandCoverage_Issue812ShiningGatesCommandsAreCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[]
                 {
                     "shining_gates_open",
                     "shining_gates_select",
                     "shining_gates_deselect",
                     "shining_gates_reroll",
                     "shining_incarnation_prepare"
                 })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#812", command.FollowUpIssue, StringComparison.Ordinal);
            AssertNoRawShiningDiagnosticText(command.PrimaryActionLabel + "\n" + command.Reason + "\n" + command.GapSummary);
        }

        var shiningAbode = Assert.Single(coverage.Commands, item => item.Id == "shining_abode");
        Assert.DoesNotContain("#817", shiningAbode.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#812", shiningAbode.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("incarnation", shiningAbode.GapSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task Help_ShiningGatesCommandsAreListedInAfterlifeHelp()
    {
        await SeedShiningGatesStateAsync();

        var result = await ExecuteCommandAsync("/help");

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Врата", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("благослов", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("новую жизнь", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AfterlifeShiningPlayerFacingSourceGuard")]
    public void BrowserAfterlifeWriteService_MustReuseExistingShiningGatesAuthority()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "WebUi",
            "BrowserAfterlifeWriteService.cs"));

        Assert.Contains("ShiningCoreActionRequestState.ActionTypeOpenGates", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningCoreActionRequestState.WriteRequestAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShiningAbodeState.TrySelectBlessingCard", source, StringComparison.Ordinal);
        Assert.Contains("ShiningAbodeState.TryDeselectBlessingCard", source, StringComparison.Ordinal);
        Assert.Contains("ShiningAbodeState.TryRerollGatesDraft", source, StringComparison.Ordinal);
    }

    private Task<ExplorerCommandResult> ExecuteCommandAsync(string command) =>
        _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: OwnerId,
            OwnerLabel: "Browser Shining gates test"));

    private Task<ExplorerCommandResult> SubmitPromptAsync(ExplorerCommandResult prompt, Dictionary<string, JsonNode?> answers) =>
        _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            answers,
            OwnerId: OwnerId));

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-shining-gates-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedShiningGatesStateAsync(
        string realm = "Shining Abode",
        bool hasOpenDraft = true,
        bool preparedPackage = false,
        string[]? selectedIds = null,
        int rerollsRemaining = 1)
    {
        selectedIds ??= [];
        await SeedStoryTurnAsync(88);
        await SeedSoulRealmAsync(realm);
        await SeedGuardiansAsync();

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
            ["halls"] = new JsonArray
            {
                CreateHall("hall_lanterns", "Зал Фонарей"),
                CreateHall("hall_mirrors", "Зал Зеркал")
            },
            ["factions"] = new JsonArray
            {
                CreateFaction(
                    "faction_lanterns",
                    "Дом Фонарей",
                    "hall_lanterns",
                    "resident_alen",
                    "project_memory"),
                CreateFaction(
                    "faction_mirrors",
                    "Дом Зеркал",
                    "hall_mirrors",
                    "resident_mira",
                    "project_route")
            },
            ["shiningPoliticalActors"] = new JsonArray(),
            ["gates"] = CreateGates(hasOpenDraft, selectedIds, rerollsRemaining)
        };

        if (preparedPackage)
        {
            root["preparedIncarnationPackage"] = new JsonObject
            {
                ["generatedFromDraftVersion"] = 4,
                ["preparedAtTurn"] = 88,
                ["preparedAtUtc"] = "2026-06-07T00:00:00.0000000Z",
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["selectedCards"] = new JsonArray(CreateCard("card_social", "Песнь Рассвета", ShiningAbodeState.EffectFamilySocial, ShiningAbodeState.RarityUncommon))
            };
        }

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var residents = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray
            {
                CreateResident("resident_alen", "Ален", "faction_lanterns", "Дом Фонарей"),
                CreateResident("resident_mira", "Мира", "faction_mirrors", "Дом Зеркал")
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

    private async Task MutateGatesAsync(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        mutate(root["gates"]!.AsObject());
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateGates(bool hasOpenDraft, IReadOnlyList<string> selectedIds, int rerollsRemaining)
    {
        if (!hasOpenDraft)
        {
            return new JsonObject
            {
                ["hasOpenDraft"] = false,
                ["draftVersion"] = 0,
                ["isStale"] = false,
                ["allCandidateBlessingCards"] = new JsonArray(),
                ["availableBlessingCards"] = new JsonArray(),
                ["shownBlessingCardIds"] = new JsonArray(),
                ["selectedBlessingCardIds"] = new JsonArray(),
                ["nextCandidateCursor"] = 0,
                ["rerollsRemaining"] = 0
            };
        }

        return new JsonObject
        {
            ["hasOpenDraft"] = true,
            ["draftVersion"] = 4,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", "Песнь Рассвета", ShiningAbodeState.EffectFamilySocial, ShiningAbodeState.RarityUncommon),
                CreateCard("card_memory", "Память Эха", ShiningAbodeState.EffectFamilyMemory, ShiningAbodeState.RarityRare),
                CreateCard("card_route", "Тропа Первого Света", ShiningAbodeState.EffectFamilyRoute, ShiningAbodeState.RarityEpic),
                CreateCard("card_relic", "Искра Реликвии", ShiningAbodeState.EffectFamilyRelic, ShiningAbodeState.RarityRare)
            },
            ["availableBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", "Песнь Рассвета", ShiningAbodeState.EffectFamilySocial, ShiningAbodeState.RarityUncommon),
                CreateCard("card_memory", "Память Эха", ShiningAbodeState.EffectFamilyMemory, ShiningAbodeState.RarityRare)
            },
            ["shownBlessingCardIds"] = new JsonArray("card_social", "card_memory"),
            ["selectedBlessingCardIds"] = new JsonArray(selectedIds.Select(id => (JsonNode?)id).ToArray()),
            ["nextCandidateCursor"] = 2,
            ["rerollsRemaining"] = rerollsRemaining
        };
    }

    private static JsonObject CreateCard(string cardId, string displayName, string effectFamily, string rarity) => new()
    {
        ["cardId"] = cardId,
        ["dedupeKey"] = $"{effectFamily}:{cardId}",
        ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
        ["sourceFactionId"] = "faction_lanterns",
        ["sourceActorId"] = "project_memory",
        ["effectFamily"] = effectFamily,
        ["rarity"] = rarity,
        ["displayName"] = displayName,
        ["displaySummary"] = $"Благословение: {displayName}.",
        ["effectPayload"] = new JsonObject
        {
            ["type"] = "browser_test"
        }
    };

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
        string headActorId,
        string projectId)
    {
        var faction = new JsonObject
        {
            ["factionId"] = factionId,
            ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
            ["hallId"] = hallId,
            ["isPlayerVisible"] = true,
            ["visibility"] = "revealed",
            ["baseStrength"] = 58,
            ["factionStrength"] = 77,
            ["investCountThisAscension"] = 1,
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
                ["headActorId"] = headActorId
            },
            ["projects"] = new JsonArray
            {
                new JsonObject
                {
                    ["projectId"] = projectId,
                    ["displayName"] = "Проект Памяти",
                    ["summary"] = "Даёт Вратам материал для благословений.",
                    ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRemembrance,
                    ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyMemory,
                    ["tier"] = 1,
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

        return ShiningFactionTestMaterialization.Apply(
            faction,
            materializedAtTurn: 77,
            hasResidentAffiliations: true,
            canTrade: false);
    }

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

    private string[] ReadSelectedBlessingCardIds()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(ShiningAbodeState.StatePath).GetAwaiter().GetResult())!)!.AsObject();
        return root["gates"]!["selectedBlessingCardIds"]!.AsArray()
            .Select(ReadString)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    private string[] ReadAvailableBlessingCardIds()
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(ShiningAbodeState.StatePath).GetAwaiter().GetResult())!)!.AsObject();
        return root["gates"]!["availableBlessingCards"]!.AsArray()
            .OfType<JsonObject>()
            .Select(card => card["cardId"]?.GetValue<string>() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    private int ReadGatesInt(string property)
    {
        var root = JsonNode.Parse((_fs.ReadFileAsync(ShiningAbodeState.StatePath).GetAwaiter().GetResult())!)!.AsObject();
        return root["gates"]![property]!.GetValue<int>();
    }

    private static string ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

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
        Assert.DoesNotContain("core action", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actionType", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("open_gates", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prepare_incarnation_package", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("selectedCardIds", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceDraftVersion", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("draftVersion", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
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
