using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;
    private readonly ValidationService _validationService;

    public ExplorerWebCommandServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-command-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _validationService = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), _validationService);
    }

    [Fact]
    public async Task ExecuteAsync_MigratedHelp_ReturnsCompletedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        Assert.Equal("/help", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks, static block => block is UiTableBlock table && table.Columns.Contains("Описание"));
    }

    [Fact]
    public async Task ExecuteAsync_HelpInAfterlife_IncludesMemorySceneCommand()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/help"));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("/воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("/воспоминание_начать", text, StringComparison.Ordinal);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Врата Памяти", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/status")]
    [InlineData("/inv")]
    [InlineData("/chaos_sea")]
    [InlineData("/shining_abode")]
    [InlineData("/spiritual_combat_help")]
    [InlineData("/spiritual_action")]
    public async Task ExecuteAsync_RepresentativeMigratedCommands_MatchDirectDtoBuilders(string command)
    {
        await SeedUniversalMetaFilesAsync();
        await SeedMortalFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedShiningAbodeFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var expected = await BuildDirectMigratedResultAsync(command);
        var actual = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.True(
            JsonNode.DeepEquals(ToJsonNode(expected), ToJsonNode(WithoutInteractiveSession(actual))),
            $"Web command service diverged from the shared DTO builder for {command}.");
    }

    [Fact]
    public async Task ExecuteAsync_RepresentativeMigratedDto_RendersThroughConsoleAdapter()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/spiritual_combat_help"));
        var console = new TestExplorerConsole();

        ExplorerCommandResultConsoleRenderer.Render(console, result);

        Assert.NotEmpty(console.Rendered);
    }

    [Fact]
    public async Task ExecuteAsync_MathCommandWithExpression_ReturnsBrowserDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math 2 + 3 * 5"));

        Assert.Equal("/math 2 + 3 * 5", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks, static block => block is UiPanelBlock panel && panel.Title.Contains("Математик", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Blocks, static block => block is UiRawJsonBlock raw && raw.Title.Contains("JSON", StringComparison.OrdinalIgnoreCase));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Результат", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("17", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/validate", "Валидация")]
    [InlineData("/world_setup", "Подготовка следующего мира")]
    [InlineData("/distribute", "Распределение характеристик")]
    [InlineData("/companion_directive", "Директивы компаньонов")]
    [InlineData("/faction_directive", "Директивы фракций")]
    [InlineData("/craft", "Ремесло")]
    [InlineData("/abode_offering", "Подношение Обители")]
    [InlineData("/found_guardian_mantle", "Основание собственной мантии")]
    [InlineData("/spiritual_action", "Духовное действие")]
    public async Task ExecuteAsync_LifecycleAndLocalTurnCommands_ReturnProtocolDtos(string command, string expectedRussianLabel)
    {
        await SeedUniversalMetaFilesAsync();
        await SeedMortalFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.NotEqual(CommandExecutionState.Blocked, result.State);
        Assert.NotEmpty(result.Blocks);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedRussianLabel, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Локальный ход", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_LocalTurnCommandWithActiveGmTurn_ShowsPendingTurnProtocolState()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "playerAction": "Тестовый ход",
          "timestamp": "2026-05-20T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", """
        {
          "sessionId": "session_web",
          "requestId": "request_web",
          "turnNumber": 12,
          "files": {}
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/world_setup"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Активный ход GM", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input/turn_request.json", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("game_state/control/pending_turn_snapshot.json", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_PromptCommand_AttachesBrowserPromptSessionAndLocalLock()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.False(string.IsNullOrWhiteSpace(result.InteractiveSession.SessionId));
        Assert.Equal("/api/explorer/prompt-sessions/submit", result.InteractiveSession.SubmitEndpoint);
        Assert.Equal("/api/explorer/prompt-sessions/cancel", result.InteractiveSession.CancelEndpoint);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ValidAnswers_CompletesWithoutConsoleInputAndReleasesLock()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("create_or_edit"),
                ["world_title"] = JsonValue.Create("Королевство пепельных колоколов"),
                ["world_directives"] = JsonValue.Create("Тёмное фэнтези, падшие династии, запрет на лёгкий тон.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Empty(completed.Prompts);
        Assert.Null(completed.InteractiveSession);
        var text = CollectBlockText(completed.Blocks);
        Assert.Contains("Подготовка мира записана", text, StringComparison.OrdinalIgnoreCase);
        var submittedJson = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last());
        Assert.Equal("Королевство пепельных колоколов", submittedJson.Json?["worldDirectives"]?["worldTitle"]?.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_WorldSetupCreate_WritesPendingSetupAndScenarioCore()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("create_or_edit"),
                ["world_title"] = JsonValue.Create("Королевство пепельных колоколов"),
                ["world_directives"] = JsonValue.Create("Тёмное фэнтези, падшие династии, запрет на лёгкий тон.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var setup = JsonNode.Parse((await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath))!)!.AsObject();
        Assert.Equal("manual", setup["mode"]!.GetValue<string>());
        Assert.Equal("Королевство пепельных колоколов", setup["worldDirectives"]!["worldTitle"]!.GetValue<string>());
        Assert.Contains("Тёмное фэнтези", setup["worldDirectives"]!["settingSummary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(_fs.FileExists(ScenarioCoreService.ManifestPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_WorldSetupClear_DeletesPendingSetupAndScenarioCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, """
        {
          "mode": "manual",
          "worldDirectives": { "worldTitle": "Старый мир" }
        }
        """);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """
        {
          "sourcePath": "game_state/control/incarnation_world_setup.json",
          "candidateAssertions": [],
          "scenarioCoreAssertions": [],
          "openCorrectionSlots": []
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["world_setup_mode"] = JsonValue.Create("clear")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.False(_fs.FileExists(WorldDirectiveService.PendingSetupPath));
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_Distribute_AppliesAllocationsAndReleasesLock()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", "{ \"unspentStatPoints\": 3 }");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "strength": 1,
          "wisdom": 2
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/distribute",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["stat_allocation_json"] = JsonValue.Create("{ \"strength\": 2, \"wisdom\": 1 }")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var stats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        var points = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/stat_points.json"))!)!.AsObject();
        Assert.Equal(3, stats["strength"]!.GetValue<int>());
        Assert.Equal(3, stats["wisdom"]!.GetValue<int>());
        Assert.Equal(0, points["unspentStatPoints"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_DistributeOverBudget_KeepsSessionOpenAndDoesNotMutate()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", "{ \"unspentStatPoints\": 1 }");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", "{ \"strength\": 1 }");
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/distribute",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["stat_allocation_json"] = JsonValue.Create("{ \"strength\": 2 }")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotNull(validation.InteractiveSession);
        Assert.Contains(validation.Notifications, static item =>
            item.Severity == UiNotificationSeverity.Error &&
            item.Message.Contains("Недостаточно", StringComparison.OrdinalIgnoreCase));
        var stats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        var points = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/stat_points.json"))!)!.AsObject();
        Assert.Equal(1, stats["strength"]!.GetValue<int>());
        Assert.Equal(1, points["unspentStatPoints"]!.GetValue<int>());
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_CompanionDirective_UpdatesNpcCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра", "progressionType": "Companion" }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/companion_directive",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["companion_id"] = JsonValue.Create("npc_1"),
                ["companion_directive"] = JsonValue.Create("Оберегай раненых и не вступай в бой первым.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var npc = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        Assert.Equal("Оберегай раненых и не вступай в бой первым.", npc["npcs"]![0]!["playerCompanionDirective"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_FactionDirective_UpdatesFactionCore()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            { "factionId": "faction_1", "name": "Серые знамена", "isPlayerFaction": true }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/faction_directive",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["faction_id"] = JsonValue.Create("faction_1"),
                ["faction_directive"] = JsonValue.Create("Укрепить северные заставы и искать союз с ремесленниками.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var factions = JsonNode.Parse((await _fs.ReadFileAsync("game_state/factions/faction_core.json"))!)!.AsObject();
        Assert.Equal("Укрепить северные заставы и искать союз с ремесленниками.", factions["factions"]![0]!["playerStrategyDirective"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_Craft_WritesPendingCraftRequest()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/inventory/recipes.json", """
        {
          "recipes": [
            { "recipeId": "healing_salve", "recipeName": "Лечебная мазь", "craftedItemName": "Припарка" }
          ]
        }
        """);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/craft",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["recipe_id"] = JsonValue.Create("healing_salve"),
                ["craft_intent"] = JsonValue.Create("Сделать припарку из трав, не расходуя редкие реагенты.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_craft_request.json"))!)!.AsObject();
        Assert.Equal("healing_salve", request["recipeId"]!.GetValue<string>());
        Assert.Equal("Сделать припарку из трав, не расходуя редкие реагенты.", request["craftIntent"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ShiningTreasuryDeposit_UpdatesTreasuryAndSoulFeathers()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_treasury",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["treasury_operation"] = JsonValue.Create("deposit"),
                ["treasury_amount"] = JsonValue.Create(4)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(24, shining["treasury"]!["depositedInkFeathers"]!.GetValue<int>());
        Assert.Equal(20, soul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_ShiningTreasuryDeposit_IgnoresNonCostCorePendingAction()
    {
        await SeedShiningAbodeFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_treasury",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["treasury_operation"] = JsonValue.Create("deposit"),
                ["treasury_amount"] = JsonValue.Create(4)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        Assert.Equal(24, shining["treasury"]!["depositedInkFeathers"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SourceOfLight_WritesPendingCapstoneRequest()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        var shining = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        shining["radiance"] = new JsonObject { ["experience"] = 580, ["tier"] = 4 };
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", shining.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/source_of_light",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["source_of_light_action"] = JsonValue.Create("open")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(SourceOfLightCapstoneState.PendingRequestPath))!)!.AsObject();
        Assert.StartsWith("source_of_light_capstone:", request["requestId"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(580, request["radianceExperienceAtRequest"]!.GetValue<int>());
        Assert.Equal(4, request["radianceTierAtRequest"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_AfterlifeInboxMarkAllRead_UpdatesNotifications()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_inbox",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["notification_action"] = JsonValue.Create("mark_all_read")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var notifications = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeNotificationState.NotificationsPath))!)!.AsObject();
        Assert.Equal(AfterlifeNotificationState.StatusRead, notifications["notifications"]![0]!["status"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualArtsUpgrade_UpdatesSoulProfile()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 200, ["total"] = 200 };
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create("pressure"),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var updated = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(1, updated["afterlifeCombatProfile"]!["artTiers"]!["pressure"]!.GetValue<int>());
        Assert.Equal(75, updated["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualArtsSpecialUpgrade_UpdatesEntityProfile()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 200, ["total"] = 200 };
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var profiles = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var specialArt = profiles["profiles"]!.AsArray()[0]!["specialArts"]!.AsArray()[0]!.AsObject();
        specialArt["upgradeCost"] = new JsonObject { ["inkFeathers"] = 90, ["lightSparks"] = 0 };
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create("rose_mirror_counter"),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var updatedSoul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var updatedProfiles = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var updatedSpecialArt = updatedProfiles["profiles"]!.AsArray()[0]!["specialArts"]!.AsArray()[0]!.AsObject();
        Assert.Equal(2, updatedSpecialArt["tier"]!.GetValue<int>());
        Assert.Equal(110, updatedSoul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Contains(updatedProfiles["profiles"]!.AsArray()[0]!["ledger"]!.AsArray().OfType<JsonObject>(), entry =>
            string.Equals(entry["reason"]?.GetValue<string>(), "special_art_local_upgrade", StringComparison.Ordinal));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SpiritualAction_ReturnsGmActionPayload()
    {
        await SeedUniversalMetaFilesAsync();
        await SeedChaosSeaFilesAsync();
        await SeedAfterlifeCombatAndEntityFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_action",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["operation_type"] = JsonValue.Create("pressure"),
                ["spiritual_action_text"] = JsonValue.Create("Я давлю на трещину в клятве противника и заставляю его отступить.")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("AFTERLIFE_SPIRITUAL_ACTION", payload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("afterlifeSpiritualConflictUpdate", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_SarefFindWingsWithoutRoute_HidesSpoilers()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, SarefMainStoryState.SerializeDefaultRoot());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Ты пока не знаешь, что искать", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Сареф", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Крыл", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(SarefMainStoryState.PendingWingsInfiltrationPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefFindWings_WritesPendingRequestAndGmPayload()
    {
        await SeedShiningAbodeFilesAsync();
        _fs.DeleteFile("game_state/control/pending_shining_abode_actions.json");
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefWingsRouteState());
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/сареф найти_крылья",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.NotNull(started.InteractiveSession);
        Assert.True(started.InteractiveSession.RequiresLocalUiLock);
        Assert.Contains(started.Prompts, prompt => prompt.Id == "saref_wings_action");

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_wings_action"] = JsonValue.Create("start")
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var request = JsonNode.Parse((await _fs.ReadFileAsync(SarefMainStoryState.PendingWingsInfiltrationPath))!)!.AsObject();
        Assert.Equal("safe", request["routeSafety"]!.GetValue<string>());
        Assert.Equal("safe_infiltration", request["entryMode"]!.GetValue<string>());
        Assert.Equal("sarefMainStoryUpdate", request["expectedResponseSurface"]!.GetValue<string>());
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_WINGS_INFILTRATION", payload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("sarefMainStoryUpdate", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefAdvantage_ReturnsGmPayload()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф преимущество"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.Contains(started.Prompts, prompt => prompt.Id == "saref_advantage_id");

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_advantage_id"] = JsonValue.Create("adv_lucian_oath_cut"),
                ["saref_scene_type"] = JsonValue.Create("oath_break"),
                ["saref_action_summary"] = JsonValue.Create("Разрезать одну ложную печать клятвы Сарефа.")
            }));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_ADVANTAGE_USE", payload["playerActionTag"]!.GetValue<string>());
        Assert.Equal("adv_lucian_oath_cut", payload["advantageId"]!.GetValue<string>());
        Assert.Contains("sarefAdvantageUses", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_SarefConfrontationAndOathBreak_ReturnGmPayloads()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActionReadyState());

        var confrontation = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф конфронтация"));
        Assert.Equal(CommandExecutionState.RequiresInput, confrontation.State);
        var confrontationResult = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            confrontation.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_route_type"] = JsonValue.Create("combat"),
                ["saref_resolution_intent"] = JsonValue.Create("defeat_saref"),
                ["saref_action_summary"] = JsonValue.Create("Вызвать Сарефа на прямой духовный бой.")
            }));

        Assert.Equal(CommandExecutionState.Completed, confrontationResult.State);
        var confrontationPayload = Assert.IsType<UiRawJsonBlock>(confrontationResult.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_FINAL_CONFRONTATION", confrontationPayload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("record_final_confrontation", confrontationPayload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);

        var oathBreak = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф разорвать_клятву"));
        Assert.Equal(CommandExecutionState.RequiresInput, oathBreak.State);
        var oathBreakResult = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            oathBreak.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["saref_oath_break_route"] = JsonValue.Create("lucian"),
                ["saref_action_summary"] = JsonValue.Create("Использовать лунный разрез как путь разрыва клятвы.")
            }));

        Assert.Equal(CommandExecutionState.Completed, oathBreakResult.State);
        var oathBreakPayload = Assert.IsType<UiRawJsonBlock>(oathBreakResult.Blocks.Last()).Json!.AsObject();
        Assert.Equal("SAREF_OATH_BREAK", oathBreakPayload["playerActionTag"]!.GetValue<string>());
        Assert.Contains("record_oath_break", oathBreakPayload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_MissingRequiredAnswer_KeepsSessionOpenWithValidationError()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>(),
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotEmpty(validation.Prompts);
        Assert.NotNull(validation.InteractiveSession);
        Assert.Contains(validation.Notifications, static notification =>
            notification.Severity == UiNotificationSeverity.Error &&
            notification.Message.Contains("world_setup_mode", StringComparison.OrdinalIgnoreCase));
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task CancelPromptSessionAsync_ReleasesLocalLock()
    {
        await SeedUniversalMetaFilesAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/world_setup",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var cancelled = await _service.CancelPromptSessionAsync(new ExplorerPromptSessionCancelRequest(
            started.InteractiveSession!.SessionId,
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, cancelled.State);
        Assert.Contains("отменена", CollectBlockText(cancelled.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/soul")]
    [InlineData("/codex")]
    [InlineData("/story")]
    [InlineData("/debug")]
    [InlineData("/галерея")]
    [InlineData("/saref")]
    public async Task ExecuteAsync_MigratedUniversalMetaCommands_ReturnCompletedDtos(string command)
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_MemoryScene_ReturnsPlayerReadableDto()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "title": "Ложа белых перьев",
            "status": "active",
            "layer": "Воспоминание",
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4,
            "role": { "roleId": "azalia_white_lodge_witness", "displayName": "Свидетель ложи", "summary": "Роль внутри старого предательства." },
            "boundaries": [ { "summary": "Сареф уже вошёл в ложу; это нельзя отменить." } ],
            "abilities": [
              { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." },
              { "abilityId": "hold_memory", "name": "Удержать память", "summary": "Не дать сцене рассыпаться." },
              { "abilityId": "name_traitor", "name": "Назвать предателя", "summary": "Связать образ с будущей правдой." }
            ],
            "requiredStoryNodes": [ { "status": "pending", "summary": "Увидеть предательство." } ],
            "successCondition": { "summary": "Распознать связь ложи с Крыльями Ангелов.", "satisfied": false },
            "closureTarget": { "guardianId": "azalia", "questId": "azalia_saref_q4", "questOrdinal": 4 }
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/воспоминание_начать"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Ложа белых перьев", text, StringComparison.Ordinal);
        Assert.Contains("Свидетель ложи", text, StringComparison.Ordinal);
        Assert.Contains("Прочитать клятву", text, StringComparison.Ordinal);
        Assert.Contains("Это не Врата Памяти", text, StringComparison.Ordinal);
        Assert.Contains("не Наследие Памяти", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Memory Gates", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MemorySceneSubcommand_RoutesThroughSharedParser()
    {
        await SeedUniversalMetaFilesAsync();
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, """
        {
          "schemaVersion": 1,
          "revealStage": "name_revealed",
          "guardianQuestlines": [],
          "latentTraces": [],
          "sarefRevelations": [],
          "sarefAdvantages": [],
          "sarefAdvantageUses": [],
          "memoryScene": {
            "sceneId": "memory_scene_azalia_q4",
            "title": "Ложа белых перьев",
            "status": "active",
            "layer": "Воспоминание",
            "guardianId": "azalia",
            "questId": "azalia_saref_q4",
            "questOrdinal": 4,
            "role": { "roleId": "azalia_white_lodge_witness", "displayName": "Свидетель ложи", "summary": "Роль внутри старого предательства." },
            "boundaries": [ { "summary": "Сареф уже вошёл в ложу; это нельзя отменить." } ],
            "abilities": [
              { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." }
            ],
            "requiredStoryNodes": [ { "status": "pending", "summary": "Увидеть предательство." } ],
            "successCondition": { "summary": "Распознать связь ложи с Крыльями Ангелов.", "satisfied": false },
            "closureTarget": { "guardianId": "azalia", "questId": "azalia_saref_q4", "questOrdinal": 4 }
          },
          "factionLinks": { "visibility": "hidden" },
          "defeatOutcomes": [],
          "endings": [],
          "playerOathState": null,
          "sarefPersonalBond": null
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/воспоминание начать"));

        Assert.Equal("/воспоминание_начать", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Воспоминание", text, StringComparison.Ordinal);
        Assert.Contains("Ложа белых перьев", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSubcommand_ReturnsRussianParserError()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф неизвестная_ветка"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Неизвестная подкоманда", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("неизвестная_ветка", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RecognizedSarefWriteSubcommand_HidesSpoilersBeforeDiscovery()
    {
        await SeedUniversalMetaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/сареф найти_крылья"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Ты пока не знаешь, что искать", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#592", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedArguments_ReturnsRussianParserError()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/math \"2 + 3"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Некорректные аргументы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("кавыч", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/inv")]
    [InlineData("/npc")]
    [InlineData("/quests")]
    [InlineData("/map")]
    [InlineData("/stats")]
    [InlineData("/combat")]
    [InlineData("/weather")]
    [InlineData("/books")]
    [InlineData("/interactions")]
    public async Task ExecuteAsync_MigratedMortalReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await SeedMortalFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/chaos_sea")]
    [InlineData("/guardians")]
    [InlineData("/abode_power")]
    [InlineData("/guardian_projects")]
    [InlineData("/abodes")]
    [InlineData("/gacha")]
    public async Task ExecuteAsync_MigratedChaosSeaReadOnlyCommands_ReturnCompletedDtos(string command)
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/shining_abode", CommandExecutionState.Completed)]
    [InlineData("/shining_politics", CommandExecutionState.Completed)]
    [InlineData("/shining_treasury", CommandExecutionState.RequiresInput)]
    [InlineData("/source_of_light", CommandExecutionState.RequiresInput)]
    public async Task ExecuteAsync_MigratedShiningAbodeCommands_ReturnDtos(string command, CommandExecutionState expectedState)
    {
        await SeedShiningAbodeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(expectedState, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/afterlife_profiles", "Профили сущностей посмертия", CommandExecutionState.Completed)]
    [InlineData("/afterlife_inbox", "Уведомления загробья", CommandExecutionState.RequiresInput)]
    [InlineData("/spiritual_conflict", "Духовный конфликт", CommandExecutionState.Completed)]
    [InlineData("/spiritual_combat_log", "Журнал духовного боя", CommandExecutionState.Completed)]
    [InlineData("/spiritual_combat_help", "Духовный бой", CommandExecutionState.Completed)]
    [InlineData("/spiritual_arts", "Духовные искусства", CommandExecutionState.RequiresInput)]
    public async Task ExecuteAsync_MigratedAfterlifeCombatAndEntityCommands_ReturnDtos(
        string command,
        string expectedRussianLabel,
        CommandExecutionState expectedState)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(command, result.Command);
        Assert.Equal(expectedState, result.State);
        Assert.NotEmpty(result.Blocks);
        Assert.Contains(expectedRussianLabel, CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Blocks,
            static block => block is UiMessageBlock message &&
                            message.Title.Contains("пока недоступна", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ReturnsFailedDto()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("   "));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Contains("пустая", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static string BuildSarefWingsRouteState() => """
    {
      "schemaVersion": 1,
      "revealStage": "name_revealed",
      "guardianQuestlines": [
        {
          "guardianId": "azalia",
          "questStates": [
            { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
            { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
            { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
            { "questOrdinal": 4, "status": "completed", "questId": "azalia_saref_q4" }
          ]
        }
      ],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "sourceGuardianId": "azalia", "sourceQuestId": "azalia_saref_q4", "sourceQuestOrdinal": 4, "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [],
      "sarefAdvantageUses": [],
      "factionLinks": { "visibility": "hidden" },
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;

    private static string BuildSarefActionReadyState() => """
    {
      "schemaVersion": 1,
      "revealStage": "confrontation_available",
      "guardianQuestlines": [],
      "latentTraces": [],
      "sarefRevelations": [
        { "revelationId": "rev_identity", "category": "identity", "revealedAtTurn": 50 },
        { "revelationId": "rev_method", "category": "method", "revealedAtTurn": 51 },
        { "revelationId": "rev_faction", "category": "faction", "revealedAtTurn": 52 },
        { "revelationId": "rev_path", "category": "path", "revealedAtTurn": 53 }
      ],
      "sarefAdvantages": [
        {
          "advantageId": "adv_lucian_oath_cut",
          "displayName": "Лунный Разрез Клятвы",
          "state": "available",
          "applicableScenes": [ "oath_break", "saref_confrontation" ],
          "summary": "Можно рассечь одну ложную печать клятвы."
        }
      ],
      "sarefAdvantageUses": [],
      "factionLinks": { "visibility": "revealed", "wingsFactionId": "wings_of_angels" },
      "wingsInfiltration": { "status": "revealed", "requestId": "saref_wings_infiltration:80", "resolvedAtTurn": 81 },
      "finalConfrontation": { "status": "active", "sceneType": "saref_confrontation" },
      "postStoryAgenda": {
        "state": "oathbound_to_saref",
        "currentObjective": "Подчинить последнюю независимую фракцию.",
        "assignments": [],
        "dominationScene": null
      },
      "playerOathState": { "state": "oathbound", "oathId": "saref_oath_001" },
      "defeatOutcomes": [],
      "endings": [],
      "sarefPersonalBond": null
    }
    """;

    private async Task SeedUniversalMetaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3,
          "inkFeathers": { "current": 12, "total": 34 },
          "enlightenment": { "currentTier": "Искра", "experience": 42 },
          "livesHistory": [
            { "incarnation": 1, "summary": "Первая жизнь" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": [
            { "title": "Первый знак", "content": "Тестовая запись кодекса" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "Тестовые мысли ГМ"
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":1,"timestamp":"2026-05-20T00:00:00Z","realm":"Chaos Sea","player":"test","narrative":"story"}

        """);
    }

    private async Task SeedMortalFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            { "itemId": "blade_1", "itemName": "Старый клинок", "quantity": 1 }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра", "status": "alive" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", """
        {
          "activeQuests": [
            { "questId": "quest_1", "title": "Найти след", "status": "active" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationName": "Старый тракт",
          "region": "Северный край",
          "description": "Дорога под серым небом."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            { "locationId": "old_road", "locationName": "Старый тракт" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/combat/enemies.json", """
        {
          "enemies": [
            { "enemyId": "wolf_1", "name": "Волк", "status": "hostile" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        { "currentTime": "ночь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", """
        { "currentState": "дождь" }
        """);

        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            { "itemId": "letter_1", "title": "Письмо" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/player_interactions.json", """
        {
          "interactions": [
            { "interactionId": "int_1", "summary": "Игроки встретились на тракте." }
          ]
        }
        """);
    }

    private async Task SeedChaosSeaFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 18, "total": 55 },
          "enlightenment": { "currentTier": "Пепельная искра", "experience": 70 }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": { "guardianId": "guardian_azalia", "guardianName": "Азалия" },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_azalia",
            "currentAbodeName": "Сад Ночных Роз",
            "knownAbodes": [
              { "abodeId": "abode_azalia", "name": "Сад Ночных Роз", "guardianId": "guardian_azalia" }
            ]
          },
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "Social",
              "relationshipData": { "currentReputation": 12 },
              "abode": { "abodeId": "abode_azalia", "name": "Сад Ночных Роз" },
              "abodePower": { "currentPower": 30, "maxPower": 100 },
              "gachaSystem": {
                "chargesPerReturn": 1,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_projects.json", """
        {
          "projects": [
            { "projectId": "project_1", "guardianId": "guardian_azalia", "status": "active", "title": "Садовая клятва" }
          ],
          "journal": [
            { "entryId": "entry_1", "guardianId": "guardian_azalia", "summary": "Проект начат." }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "guardianPowerEvents": [
            { "eventId": "power_1", "guardianId": "guardian_azalia", "reasonType": "offering", "finalDelta": 5 }
          ]
        }
        """);
    }

    private async Task SeedShiningAbodeFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 24, "total": 90 },
          "afterlifeCombatProfile": { "capstones": {} }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "lightSparks": 7,
          "radiance": { "experience": 260, "tier": 3 },
          "gates": { "hasOpenDraft": true },
          "treasury": {
            "depositedInkFeathers": 20,
            "claimableInkFeatherInterest": 2,
            "lastInterestSettlementCycleId": "cycle_5",
            "exchangeCycleId": "cycle_5",
            "exchangeThisCycleLightSparks": 1
          },
          "gachaSystem": {
            "chargesPerReturn": 2,
            "chargesUsedThisReturn": 1,
            "currentReturnCycleId": "cycle_5",
            "gachaHistory": []
          },
          "halls": [
            { "hallId": "hall_dawn", "hallName": "Зал Рассвета" }
          ],
          "factions": [
            {
              "factionId": "faction_lanterns",
              "hallId": "hall_dawn",
              "factionStrength": 40,
              "charter": { "factionName": "Фонари Рассвета" },
              "leadership": { "headActorType": "resident", "headActorId": "resident_1", "leadershipState": "secure" },
              "projects": [
                { "projectId": "project_light", "displayName": "Световой мост", "status": "active", "tier": 1 }
              ]
            }
          ],
          "shiningPoliticalActors": [
            { "actorId": "actor_1", "displayName": "Светозарный судья", "politicalStatus": "elder" }
          ],
          "coreActionReceipts": [
            { "receiptId": "receipt_1", "actionType": "draft_incarnation_package" }
          ],
          "sourceOfLightCapstone": { "completed": false }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
        {
          "entries": [
            {
              "residentId": "resident_1",
              "displayName": "Лиара",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_lanterns",
              "factionLoyaltyLevel": 60,
              "factionLoyaltyTier": "attached"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            { "guardianId": "guardian_azalia", "canonicalName": "Азалия" }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_shining_abode_actions.json", """
        {
          "requests": [
            { "requestId": "core_req_1", "actionType": "draft_incarnation_package" }
          ]
        }
        """);
    }

    private async Task SeedAfterlifeCombatAndEntityFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Test Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 6,
          "inkFeathers": { "current": 40, "total": 120 },
          "enlightenment": { "currentTier": "Пламенный знак", "experience": 160 },
          "afterlifeCombatProfile": {
            "enlightenmentTier": 3,
            "radianceTier": 1,
            "spiritFocusTier": 2,
            "standardArts": {
              "pressure": 2,
              "guard": 1,
              "counter": 1,
              "maneuver": 2,
              "binding": 1
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Test Soul",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 40, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 3, "experience": 160 },
                "radiance": { "tier": 1, "experience": 20 }
              },
              "standardArts": {
                "pressure": 2,
                "guard": 1,
                "counter": 1,
                "maneuver": 2,
                "binding": 1,
                "recover_spiritual_power": 1
              },
              "specialArts": [
                {
                  "artId": "rose_mirror_counter",
                  "displayName": "Зеркало Ночной Розы",
                  "baseOperation": "counter",
                  "tier": 1,
                  "effectSummary": "Контрприём оставляет болезненный образ в клятве противника.",
                  "costMultiplierPercent": 150,
                  "canTeachPlayer": true
                }
              ],
              "customStates": [
                { "stateId": "memory_echo", "stateName": "Эхо памяти", "currentValue": 2, "maxValue": 5 }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "summary": "Сначала усилить защиту и манёвр.",
                "priorityOrder": [ "guard", "maneuver", "pressure" ],
                "lastAutoProgressionCycleKey": "cycle_6"
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_spiritual_conflict_state.json", """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "conflict_1",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "conflictPosition": "player_advantaged",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "strained",
            "resolutionState": "active",
            "controlState": {
              "controlId": "control_1",
              "controllerSide": "player",
              "level": "hindered",
              "sourceOperation": "binding",
              "restrictedOperations": [ "maneuver" ],
              "summary": "Оковы держат противника у края клятвы."
            },
            "actionEconomy": {
              "player": { "current": 7, "max": 8, "source": "spirit_focus" },
              "opposition": { "current": 5, "max": 6, "source": "profile" }
            },
            "playerSide": { "leadContestant": { "actorId": "player_soul", "displayName": "Test Soul" } },
            "oppositionSide": { "leadContestant": { "actorId": "guardian_shadow", "displayName": "Тень Хранителя" } },
            "exchangeLog": [
              {
                "exchangeId": "exchange_1",
                "operationType": "pressure",
                "outcome": "success",
                "exchangeAtTurn": 6,
                "before": { "conflictPosition": "contested", "oppositionSideStrain": "clear" },
                "after": { "conflictPosition": "player_advantaged", "oppositionSideStrain": "strained" },
                "diceAudit": {
                  "rolls": [
                    { "side": "player", "value": 15 },
                    { "side": "opposition", "value": 9 }
                  ],
                  "playerTotal": 18,
                  "oppositionTotal": 11,
                  "margin": 7
                },
                "rewardAudit": {
                  "currency": "ink_feathers",
                  "finalAmount": 3,
                  "resolvedAtTurn": 6
                }
              }
            ]
          },
          "recentConflicts": [
            {
              "conflictId": "conflict_done",
              "resolutionState": "resolved",
              "operationType": "pressure",
              "playerOutcome": "victory",
              "resolvedAtTurn": 5,
              "rewardAudit": {
                "currency": "ink_feathers",
                "finalAmount": 2,
                "resolvedAtTurn": 5
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/afterlife_notifications.json", """
        {
          "notifications": [
            {
              "notificationId": "notification_1",
              "notificationType": "guardian_quest_available",
              "requestId": "quest_req_1",
              "status": "unread",
              "guardianId": "guardian_azalia",
              "guardianName": "Азалия",
              "summary": "Хранитель предлагает тёмный след из прошлой жизни.",
              "createdAtTurn": 6,
              "createdAtUtc": "2026-05-20T00:00:00Z"
            }
          ]
        }
        """);
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
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Columns);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                break;
        }
    }

    private async Task<ExplorerCommandResult> BuildDirectMigratedResultAsync(string command)
    {
        await _stateManager.RefreshGameStateAsync();
        if (string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "/помощь", StringComparison.OrdinalIgnoreCase))
        {
            var state = _stateManager.CurrentState;
            return ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
            {
                Command = command,
                Title = new LocalizationManager().T("help"),
                IsChaosSea = state.IsInChaosSea,
                IsShiningAbode = state.IsInShiningAbode,
                IsPendingShiningAbodeBootstrap = state.IsInShiningAbodePendingBootstrap,
                CanReenterShiningAbode = state.CanReenterShiningAbode
            });
        }

        if (ExplorerUniversalMetaCommandResultBuilder.CanBuild(command))
        {
            var universal = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
                command,
                _stateManager,
                _fs,
                new LocalizationManager());
            if (universal != null)
                return universal;
        }

        if (ExplorerMortalWorldCommandResultBuilder.CanBuild(command))
        {
            var mortal = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs);
            if (mortal != null)
                return mortal;
        }

        if (ExplorerChaosSeaCommandResultBuilder.CanBuild(command))
        {
            var chaos = await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs);
            if (chaos != null)
                return chaos;
        }

        if (ExplorerShiningAbodeCommandResultBuilder.CanBuild(command))
        {
            var shining = await ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs);
            if (shining != null)
                return shining;
        }

        if (ExplorerAfterlifeCombatCommandResultBuilder.CanBuild(command))
        {
            var afterlife = await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(command, _stateManager, _fs);
            if (afterlife != null)
                return afterlife;
        }

        if (ExplorerLifecycleLocalTurnCommandResultBuilder.CanBuild(command))
        {
            var lifecycle = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
                command,
                _stateManager,
                _fs,
                _validationService);
            if (lifecycle != null)
                return lifecycle;
        }

        throw new InvalidOperationException($"No direct DTO builder for migrated command {command}.");
    }

    private static JsonNode ToJsonNode(ExplorerCommandResult result) =>
        JsonSerializer.SerializeToNode(result, JsonOptions)!;

    private static ExplorerCommandResult WithoutInteractiveSession(ExplorerCommandResult result) => new()
    {
        Command = result.Command,
        State = result.State,
        Blocks = result.Blocks,
        Actions = result.Actions,
        Prompts = result.Prompts,
        Notifications = result.Notifications,
        InteractiveSession = null
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
