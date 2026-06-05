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

    [Fact]
    public async Task ExecuteAsync_GalleryWithImages_ReturnsImageBlocks()
    {
        WriteSessionImage("images/npcs/ashen_knight.png");
        WriteSessionImage("images/scenes/scene_001.webp");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/gallery"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var images = result.Blocks.OfType<UiImageBlock>().ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, image =>
        {
            Assert.StartsWith("/api/media/", image.Url, StringComparison.Ordinal);
            Assert.StartsWith("images/", image.RelativePath, StringComparison.Ordinal);
        });
        Assert.Contains(images, image => image.Title.Contains("ashen_knight", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task ExecuteAsync_Inventory_ReturnsRichInventoryBlocksAndFriendlyTitles()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "totalWeight": 17,
          "maxWeight": 30,
          "money": 125,
          "resources": {
            "wood": 4,
            "gold": 0,
            "cloth": "2"
          },
          "equipment": {
            "head": { "name": "Железный шлем" },
            "mainHand": { "itemName": "Кривой меч" },
            "offHand": null
          },
          "items": [
            { "name": "Факел", "type": "utility", "count": 2, "durability": "100%" },
            { "itemName": "Сломанный лук", "type": "weapon", "quantity": 1, "durability": "0%" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_resources.json", """
        {
          "entries": [
            { "itemId": "torch_1", "resource": "oil" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_bonds.json", """
        {
          "entries": [
            { "itemId": "bow_1", "bond": "quest" }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            { "itemId": "note_1", "title": "Записка" }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(result.Blocks, static block =>
            block is UiTextBlock text &&
            text.Text == "⚖ 17 / 30" &&
            text.Tone == UiTone.Muted);
        Assert.Contains(result.Blocks, static block =>
            block is UiTextBlock text &&
            text.Text == "💰 Деньги: 125" &&
            text.Tone == UiTone.Default);

        var resources = Assert.Single(result.Blocks.OfType<UiKeyValueGridBlock>());
        Assert.Contains(resources.Items, static item => item.Key == "💎 wood" && item.Value == "4");
        Assert.Contains(resources.Items, static item => item.Key == "💎 cloth" && item.Value == "2");

        var equipmentPanel = Assert.Single(result.Blocks.OfType<UiPanelBlock>(), static panel => panel.Title == "⚔️ Экипировка");
        var equipmentGrid = Assert.IsType<UiKeyValueGridBlock>(Assert.Single(equipmentPanel.Blocks));
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "🪖 Голова" && item.Value == "Железный шлем");
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "⚔️ Основная рука" && item.Value == "Кривой меч");
        Assert.Contains(equipmentGrid.Items, static item => item.Key == "🛡️ Вторая рука" && item.Value == "— пусто —");

        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal(new[] { "Название", "Тип", "Кол-во", "Прочность", "Статус" }, table.Columns);
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Факел", "utility", "2", "100%", "✓"]));
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Сломанный лук", "weapon", "1", "0%", "⚠ СЛОМАН"]));

        var rawBlocks = result.Blocks.OfType<UiRawJsonBlock>().ToList();
        Assert.Contains(rawBlocks, static raw => raw.Title == "Полный JSON items.json");
        Assert.Contains(rawBlocks, static raw => raw.Title == "Ресурсы предметов");
        Assert.Contains(rawBlocks, static raw => raw.Title == "Связи предметов");
        Assert.Contains(rawBlocks, static raw => raw.Title == "Тексты предметов");

        Assert.DoesNotContain("game_state/inventory/", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_WithoutItemsFile_ShowsEmptyInventoryMessage()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Equal(UiNotificationSeverity.Info, message.Severity);
        Assert.Equal("Инвентарь", message.Title);
        Assert.Equal("Инвентарь пуст или данные ещё не созданы.", message.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_WithItemsButWithoutAuxiliaryFiles_HidesMissingAuxiliaryState()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            { "name": "Факел", "type": "utility", "count": 2 }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inventory"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal("📦 Предметы (1)", table.Title);
        Assert.Contains(table.Rows, static row => row.Cells.SequenceEqual(["Факел", "utility", "2", string.Empty, "✓"]));
        Assert.Contains(result.Blocks.OfType<UiRawJsonBlock>(), static raw => raw.Title == "Полный JSON items.json");
        Assert.DoesNotContain("отсутствует", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/inventory/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_resources.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_bonds.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_text_updates.json", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_inline_1",
              "name": "Письмо с площади",
              "type": "Документ",
              "group": "Документы и медиа",
              "textContent": [
                "Лира просит встретиться у фонтана до рассвета."
              ]
            },
            {
              "existedId": "doc_sidecar_1",
              "name": "Записка с рынка",
              "type": "note",
              "group": "Документы и медиа",
              "textContent": null
            },
            {
              "existedId": "doc_journal_1",
              "name": "Памятная книга",
              "type": "Книга",
              "textContent": null
            },
            {
              "existedId": "doc_sealed_1",
              "name": "Запечатанное письмо",
              "type": "Документ",
              "description": "Письмо запечатано неизвестной печатью.",
              "textContent": null,
              "unreadableReason": "Письмо запечатано неизвестной печатью."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_sidecar_1",
              "itemName": "Не это имя",
              "textContent": [
                "На обороте записки указан путь через северные ворота."
              ]
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "doc_journal_1",
              "itemName": "Другое имя",
              "journalEntries": [
                {
                  "event": "Пробуждение",
                  "description": "Книга шепчет о владельце."
                }
              ]
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/книги"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Письмо с площади", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лира просит встретиться у фонтана до рассвета.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Записка с рынка", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("На обороте записки указан путь через северные ворота.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Памятная книга", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Книга шепчет о владельце.", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Запечатанное письмо", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Письмо запечатано неизвестной печатью.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_text_updates", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Books_WithOnlySealedDocument_DoesNotShowEmptyBooksMessage()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_sealed_only_1",
              "name": "Запечатанное письмо",
              "type": "Документ",
              "textContent": null,
              "unreadableReason": "Печать не позволяет прочесть письмо сейчас."
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/books"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Запечатанное письмо", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печать не позволяет прочесть письмо сейчас.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Данные ещё не созданы.", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Inventory_AddsEquipAndUnequipActionsForOrdinaryItems()
    {
        await SeedInventoryEquipmentItemsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/inv"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var equipAction = Assert.Single(result.Actions, action => action.Label == "Экипировать «Кривой меч»");
        Assert.Equal("inventory-equip-sword_1", equipAction.Id);
        Assert.Equal("/экипировать sword_1", equipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, equipAction.Style);
        Assert.False(equipAction.RequiresConfirmation);

        var unequipAction = Assert.Single(result.Actions, action => action.Label == "Снять «Железный шлем»");
        Assert.Equal("inventory-unequip-head", unequipAction.Id);
        Assert.Equal("/снять head", unequipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, unequipAction.Style);
        Assert.False(unequipAction.RequiresConfirmation);

        Assert.DoesNotContain(result.Actions, action => action.Label.Contains("Факел", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Actions, action => action.Label.Contains("Сломанный лук", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Actions, action => action.Label.Contains("Реликвия души", StringComparison.OrdinalIgnoreCase));
        Assert.All(result.Actions, action =>
        {
            Assert.DoesNotContain("/", action.Label, StringComparison.Ordinal);
            Assert.DoesNotContain("itemId", action.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("API", action.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DTO", action.Label, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task ExecuteAsync_SoulRelics_RendersStatusTableAndPlayerActions()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Chaos Sea",
          "soulRelics": {
            "stored": [
              {
                "relicId": "relic_stored",
                "name": "Клинок Памяти",
                "rarity": "rare",
                "slot": "mainHand",
                "gameplayStatus": { "equipped": false }
              }
            ],
            "equipped": [
              {
                "relicId": "relic_equipped",
                "name": "Шлем Тишины",
                "quality": "legendary",
                "gameplayStatus": { "equipped": true, "currentSlot": "head" }
              }
            ]
          }
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/soul_relics"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>(), block => block.Title == "Реликвии души");
        Assert.Equal(["Статус", "Слот", "Реликвия", "Редкость", "ID"], table.Columns);
        Assert.Contains(table.Rows, row =>
            row.Cells.Contains("Хранилище") &&
            row.Cells.Contains("Клинок Памяти") &&
            row.Cells.Contains("rare"));
        Assert.Contains(table.Rows, row =>
            row.Cells.Contains("Экипировано") &&
            row.Cells.Contains("Шлем Тишины") &&
            row.Cells.Contains("legendary"));

        var equipAction = Assert.Single(result.Actions, action => action.Id == "soul-relic-equip-relic_stored");
        Assert.Equal("/soul_relic_equip relic_stored", equipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, equipAction.Style);
        Assert.False(equipAction.RequiresConfirmation);

        var unequipAction = Assert.Single(result.Actions, action => action.Id == "soul-relic-unequip-head");
        Assert.Equal("/soul_relic_unequip head", unequipAction.Command);
        Assert.Equal(UiActionStyle.Secondary, unequipAction.Style);
        Assert.False(unequipAction.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquipAction_OpensPromptSessionWithItemSlotAndConfirmation()
    {
        await SeedInventoryEquipmentItemsAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        Assert.Equal("/api/explorer/prompt-sessions/submit", result.InteractiveSession.SubmitEndpoint);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var itemPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "item_identity"));
        Assert.True(itemPrompt.Required);
        Assert.Equal("sword_1", itemPrompt.Options.Single().Value);
        Assert.Contains("Кривой меч", itemPrompt.Options.Single().Label, StringComparison.OrdinalIgnoreCase);

        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "equipment_slot"));
        Assert.True(slotPrompt.Required);
        Assert.Contains(slotPrompt.Options, option => option.Value == "mainHand" && option.Label.Contains("Основная рука", StringComparison.OrdinalIgnoreCase));

        var confirmation = Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_inventory_write"));
        Assert.True(confirmation.Required);
        Assert.False(confirmation.DefaultValue);
        var blockText = CollectBlockText(result.Blocks);
        Assert.DoesNotContain("Browser-write", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terminal", blockText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/control", blockText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryEquip_WritesEquipmentAndReleasesLock()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["item_identity"] = JsonValue.Create("sword_1"),
                ["equipment_slot"] = JsonValue.Create("mainHand"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Empty(completed.Prompts);
        Assert.Null(completed.InteractiveSession);
        Assert.Contains("Кривой меч", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("экипирован", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(completed.Blocks, static block =>
            block is UiRawJsonBlock raw && raw.Title.Contains("JSON: результат браузерной записи", StringComparison.OrdinalIgnoreCase));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Equal("sword_1", inventory["equipment"]!["mainHand"]!.GetValue<string>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryUnequip_WritesNullAndReleasesLock()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/снять head",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(started.Prompts, prompt => prompt.Id == "equipment_slot"));
        Assert.Equal("head", slotPrompt.Options.Single().Value);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["equipment_slot"] = JsonValue.Create("head"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Contains("Железный шлем", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("снят", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(completed.Blocks, static block =>
            block is UiRawJsonBlock raw && raw.Title.Contains("JSON: результат браузерной записи", StringComparison.OrdinalIgnoreCase));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equipment"]!["head"]);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_InventoryEquip_WhenItemDisappears_KeepsSessionOpenWithPlayerFacingError()
    {
        await SeedInventoryEquipmentItemsAsync();
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "equipment": {
            "head": "helmet_1",
            "mainHand": null,
            "offHand": null
          },
          "items": [
            { "existedId": "helmet_1", "name": "Железный шлем", "type": "helmet", "durability": "100%" }
          ]
        }
        """);

        var validation = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["item_identity"] = JsonValue.Create("sword_1"),
                ["equipment_slot"] = JsonValue.Create("mainHand"),
                ["confirm_inventory_write"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.RequiresInput, validation.State);
        Assert.NotNull(validation.InteractiveSession);
        var notificationText = string.Join("\n", validation.Notifications.Select(notification => notification.Message));
        Assert.Contains(validation.Notifications, notification =>
            notification.Message.Contains("Предмет не найден", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Browser-write", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", notificationText, StringComparison.OrdinalIgnoreCase);

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equipment"]!["mainHand"]);
        Assert.True(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquip_WithActiveGmTurn_BlocksPromptSession()
    {
        await SeedInventoryEquipmentItemsAsync();
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

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/экипировать sword_1"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Активный ход GM", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        var notificationText = string.Join("\n", result.Notifications.Select(notification => notification.Message));
        Assert.DoesNotContain("Browser-write", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM-turn", notificationText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", notificationText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InventoryEquip_WithOtherLocalLock_BlocksPromptSession()
    {
        await SeedInventoryEquipmentItemsAsync();
        var lockService = new LocalUiSessionLockService(_fs);
        await lockService.AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("console-owner", "console", "Консоль", TimeSpan.FromMinutes(5)),
            "console inventory");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать sword_1",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Contains("Локальная UI-блокировка", CollectBlockText(result.Blocks), StringComparison.OrdinalIgnoreCase);
        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        Assert.Null(inventory["equipment"]!["mainHand"]);
    }

    [Fact]
    public async Task ExecuteAsync_NpcBundle_HidesPathsAndSkipsMissingFiles()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "npcs": [
            { "npcId": "npc_1", "name": "Мирра" }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var table = Assert.Single(result.Blocks.OfType<UiTableBlock>());
        Assert.Equal(new[] { "Раздел", "Состояние" }, table.Columns);
        var row = Assert.Single(table.Rows);
        Assert.Equal(["NPC", "1"], row.Cells);
        Assert.DoesNotContain(table.Rows, static candidate => candidate.Cells.Any(static cell => cell.Contains("отсутствует", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(table.Rows.SelectMany(static candidate => candidate.Cells), static cell => cell.Contains("game_state/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Blocks.OfType<UiRawJsonBlock>(), static raw => raw.Title == "Полный JSON game_state/npcs/npc_core.json");
    }

    [Fact]
    public async Task ExecuteAsync_NpcBundle_WithoutFiles_ShowsNotCreatedMessage()
    {
        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/npc"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var message = Assert.IsType<UiMessageBlock>(Assert.Single(result.Blocks));
        Assert.Equal(UiNotificationSeverity.Info, message.Severity);
        Assert.Equal("Персонажи", message.Title);
        Assert.Equal("Данные ещё не созданы.", message.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Map_ReturnsInteractiveMapBlock()
    {
        await SeedMortalFilesAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_square",
          "name": "Старая площадь",
          "region": "Северный край",
          "description": "Площадь под серым небом.",
          "coordinates": { "x": 4, "y": 7, "z": 0 },
          "adjacencyMap": [
            {
              "targetLocationId": "loc_gate",
              "name": "Северные ворота",
              "direction": "север",
              "linkState": "safe",
              "targetCoordinates": { "x": 4, "y": 8, "z": 0 }
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_map.json", """
        {
          "newLocations": [
            {
              "locationId": "loc_gate",
              "locationName": "Северные ворота",
              "locationType": "gate",
              "coordinates": { "x": 4, "y": 8, "z": 0 }
            }
          ]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Mortal World", mapBlock.Map.Realm);
        Assert.Equal("loc_square", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Старая площадь");
        Assert.Contains(mapBlock.Map.Links, static link => link.TargetNodeId == "loc_gate");
    }

    [Fact]
    public async Task ExecuteAsync_Map_InChaosSea_ReturnsAbodeConstellationMap()
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Chaos Sea", mapBlock.Map.Realm);
        Assert.Equal("abode_azalia", mapBlock.Map.CurrentNodeId);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.IsCurrent && node.Label == "Сад Ночных Роз");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Details.Any(item => item.Key == "Активный Хранитель" && item.Value == "да"));
    }

    [Fact]
    public async Task ExecuteAsync_Map_InShiningAbode_ReturnsCivicAtlasMap()
    {
        await SeedShiningAbodeFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/map"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var mapBlock = Assert.Single(result.Blocks.OfType<UiMapBlock>());
        Assert.Equal("Shining Abode", mapBlock.Map.Realm);
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "hall_dawn" && node.Label == "Зал Рассвета");
        Assert.Contains(mapBlock.Map.Nodes, static node => node.Id == "faction_lanterns" && node.Details.Any(item => item.Key == "Лидерство"));
    }

    [Theory]
    [InlineData("/chaos_sea")]
    [InlineData("/guardians")]
    [InlineData("/abode_power")]
    [InlineData("/guardian_projects")]
    [InlineData("/guardian_politics")]
    [InlineData("/abodes")]
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

    [Fact]
    public async Task ExecuteAsync_Gacha_ReturnsDirectChaosSeaPrompt()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal("/gacha", result.Command);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession.RequiresLocalUiLock);
        var bannerPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "gacha_banner"));
        var banner = Assert.Single(bannerPrompt.Options);
        Assert.Equal("direct_chaos_sea", banner.Value);
        Assert.Contains("Прямой призыв", banner.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1-18", banner.Description, StringComparison.Ordinal);
        Assert.Contains(result.Prompts, prompt => prompt.Id == "feather_cost");
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_gacha_pull"));
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Пороги: 4-48 Common, 49-67 Uncommon, 68-75 Rare, 76-79 Epic, 80 Legendary", text, StringComparison.Ordinal);
        Assert.Contains("Базовая редкость", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rare", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Guardian-mediated", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_WhenPendingDiceMissing_CreatesAuthoritativeBaseForPromptAndSubmit()
    {
        await SeedChaosSeaFilesAsync();

        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.True(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        var pending = JsonNode.Parse((await _fs.ReadFileAsync(PendingTurnStateService.PendingDiceStatePath))!)!.AsObject();
        var gachaBase = pending["gachaBaseResult"]!.AsObject();
        var expectedRarity = gachaBase["baseRarity"]!.GetValue<string>();
        var expectedScore = gachaBase["baseScore"]!.GetValue<int>();
        var expectedDice = gachaBase["diceUsed"]!.AsArray()
            .Select(node => node!.GetValue<int>())
            .ToArray();
        var text = CollectBlockText(result.Blocks);
        Assert.Contains(expectedRarity, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedScore.ToString(), text, StringComparison.Ordinal);
        Assert.Contains("[" + string.Join(", ", expectedDice) + "]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("не подготовлен", text, StringComparison.OrdinalIgnoreCase);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            result.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["gacha_banner"] = JsonValue.Create("direct_chaos_sea"),
                ["feather_cost"] = JsonValue.Create(5),
                ["confirm_gacha_pull"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal(expectedRarity, payload["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(expectedScore, payload["gachaBaseResult"]!["baseScore"]!.GetValue<int>());
        Assert.Equal(expectedDice, payload["gachaBaseResult"]!["diceUsed"]!.AsArray()
            .Select(node => node!.GetValue<int>())
            .ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_InShiningAbode_DoesNotOpenDirectChaosSeaPrompt()
    {
        await SeedShiningAbodeFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Море Хаоса", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_Gacha_WithUnsupportedArgument_DoesNotOpenPrompt()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha guardian_pull",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectBlockText(result.Blocks);
        Assert.Contains("аргумент", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_GachaDirectPull_QueuesGmTurnRequestWithSnapshot()
    {
        await SeedChaosSeaFilesAsync();
        await SeedPendingGachaBaseAsync("Uncommon", 55, [12, 13, 14, 16]);
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/gacha",
            OwnerId: "browser-test",
            OwnerLabel: "Browser test"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["gacha_banner"] = JsonValue.Create("direct_chaos_sea"),
                ["feather_cost"] = JsonValue.Create(5),
                ["confirm_gacha_pull"] = JsonValue.Create(true)
            },
            OwnerId: "browser-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(13, soul["inkFeathers"]!["current"]!.GetValue<int>());
        var payload = Assert.IsType<UiRawJsonBlock>(completed.Blocks.Last()).Json!.AsObject();
        Assert.Equal("CHAOS_SEA_DIRECT_GACHA", payload["playerActionTag"]!.GetValue<string>());
        Assert.Equal("Uncommon", payload["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Contains("5 Чернильных Перьев", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("ровно одну новую Soul Relic", payload["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(_fs.FileExists(BrowserPendingTurnInspector.TurnRequestPath));
        Assert.True(_fs.FileExists(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        Assert.True(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(
            await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath),
            out var authorityPayload));
        Assert.NotNull(authorityPayload);
        var pendingTurn = BrowserPendingTurnInspector.Build(_fs);
        Assert.True(pendingTurn.HasActiveGmTurn);
        Assert.Contains(
            pendingTurn.Artifacts,
            artifact => string.Equals(artifact.Path, BrowserPendingTurnInspector.TurnRequestPath, StringComparison.OrdinalIgnoreCase) &&
                        artifact.Exists);

        var request = JsonNode.Parse((await _fs.ReadFileAsync(BrowserPendingTurnInspector.TurnRequestPath))!)!.AsObject();
        Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", request["playerAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("5 Чернильных Перьев", request["playerAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Uncommon", request["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(55, request["gachaBaseResult"]!["baseScore"]!.GetValue<int>());
        Assert.Equal(
            Enumerable.Range(1, 20),
            request["preGeneratedDices1d20"]!.AsArray().Select(node => node!.GetValue<int>()));

        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath))!)!.AsObject();
        Assert.Equal(request["requestId"]!.GetValue<string>(), manifest["requestId"]!.GetValue<string>());
        Assert.Equal(request["playerAction"]!.GetValue<string>(), manifest["playerAction"]!.GetValue<string>());
        Assert.Equal("Uncommon", manifest["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Equal(request["requestId"]!.GetValue<string>(), authorityPayload!.RequestId);
        Assert.Equal(request["turnNumber"]!.GetValue<int>(), authorityPayload.TurnNumber);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        Assert.True(files.ContainsKey("game_state/meta/soul_state.json"));
        var snapshotSoulPath = files["game_state/meta/soul_state.json"]!.GetValue<string>();
        var snapshotSoul = JsonNode.Parse((await _fs.ReadFileAsync(snapshotSoulPath))!)!.AsObject();
        Assert.Equal(13, snapshotSoul["inkFeathers"]!["current"]!.GetValue<int>());
        var rollbackBackups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]);
        Assert.True(rollbackBackups.ContainsKey("game_state/meta/soul_state.json"));
        var rollbackSoul = JsonNode.Parse((await _fs.ReadFileAsync(rollbackBackups["game_state/meta/soul_state.json"]!.GetValue<string>()))!)!.AsObject();
        Assert.Equal(18, rollbackSoul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_HidesSecretLinks()
    {
        await SeedChaosSeaFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Азалия ищет союзников", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытых записей", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Скрытая зависимость", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_saref_shadow", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_DefaultProjectionOmitsGmOnlyRawState()
    {
        await SeedChaosSeaFilesAsync();
        await WriteGuardianPoliticsRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Азалия ищет союзников", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Публичный архивный пакт", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Скрытых записей", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hiddenRelations", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretProjects", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internalMotivations", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_saref_shadow", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_invisible_false_guardian", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_project_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is_player_visible_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_GuardianPolitics_DebugProjectionIncludesFullRawState()
    {
        await SeedChaosSeaFilesAsync();
        await WriteGuardianPoliticsRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/guardian_politics","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsGuardianPoliticsRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/guardian_politics"));

        AssertContainsGuardianPoliticsRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_ShiningPolitics_DefaultProjectionShowsFactionChroniclesWithoutRawMemory()
    {
        await SeedShiningAbodeFilesAsync();
        await WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_politics"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроника фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Фонари Рассвета", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открыли безопасный проход", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Влияние фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Серебряный Зал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ресурсы фракций", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Искры Света", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("19", text, StringComparison.Ordinal);
        Assert.DoesNotContain("strategicMemory", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resourceLedger", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_strategy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_ledger_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ShiningPolitics_DebugProjectionIncludesFullFactionMemory()
    {
        await SeedShiningAbodeFilesAsync();
        await WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/shining_politics","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsShiningPoliticsRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/shining_politics"));

        AssertContainsShiningPoliticsRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionOmitsHiddenProfileRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хранитель Открытой Розы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая карта клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открытая цель: защитить игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_actor_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_activity_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_fate_card_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_card_story_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_concealed_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_mask_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_saref_agent_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/afterlife_chronicles")]
    [InlineData("/хроники_посмертия")]
    public async Task ExecuteAsync_AfterlifeChronicles_DefaultProjectionShowsPlayerSafeChronology(string command)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроники посмертия", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал зеркальной клятвы", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guardian_scene:guardian_mirror", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок впервые вошёл в зал отражений", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок услышал зов зеркал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал отражений запомнил голос игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Понять, почему зеркала зовут игрока", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранитель Зеркал", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игрок", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret_participant_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_scope_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moon_visible_to_player_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quiet_deal_boolean_secret_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("silent_boolean_hidden_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("closed_gm_only_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmThoughtsSummary", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastInvalidChronicleUpdate", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicles_MissingStateReturnsFriendlyEmptyState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles"));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains("Хроники пока пусты", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlife_chronicles.json пока не создан", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeChronicles_AdvancedProjectionIncludesFullRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeChroniclesRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/afterlife_chronicles","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsAfterlifeChroniclesRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_chronicles"));

        AssertContainsAfterlifeChroniclesRawState(gmThoughtsResult);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionShowsKnownMasksWithoutHiddenInternals()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesMaskProjectionFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var text = CollectBlockText(result.Blocks);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Маски", text, StringComparison.Ordinal);
        Assert.Contains("Хранитель Масок", text, StringComparison.Ordinal);
        Assert.Contains("Активный посланник", text, StringComparison.Ordinal);
        Assert.Contains("дипломат", text, StringComparison.Ordinal);
        Assert.Contains("улыбается и просит доверия", text, StringComparison.Ordinal);
        Assert.Contains("Раскрытая вывеска", text, StringComparison.Ordinal);
        Assert.Contains("known_revealed_truth_marker", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("Скрытый запасной образ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden_active_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_active_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_active_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_dormant_condition_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_threat_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_saref_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DefaultProjectionShowsRelationshipProgressWithoutDebugInternals()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRelationshipGatesFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));
        var relationshipTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static table => table.Title == "Отношения");
        var text = CollectBlockText([relationshipTable]);
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Хранитель Зеркал", text, StringComparison.Ordinal);
        Assert.Contains("Доверие", text, StringComparison.Ordinal);
        Assert.Contains("49", text, StringComparison.Ordinal);
        Assert.Contains("порог 50", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("до порога 1", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Суд зеркальной клятвы", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        Assert.DoesNotContain("guardian_mirror_player_trust", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quest_mirror_oath_trial", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player_soul", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_lock_evidence_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden_gate_gm_thoughts_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_AdvancedProjectionShowsAllMaskDiagnostics()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesMaskProjectionFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_profiles",
            AdvancedEnabled: true));
        var text = CollectBlockText(result.Blocks);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Маски", text, StringComparison.Ordinal);
        Assert.Contains("Активный посланник", text, StringComparison.Ordinal);
        Assert.Contains("Раскрытая вывеска", text, StringComparison.Ordinal);
        Assert.Contains("Скрытый запасной образ", text, StringComparison.Ordinal);
        Assert.Contains("hidden_active_truth_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_active_directive_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_active_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_truth_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_directive_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_dormant_condition_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_threat_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_saref_marker", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_AdvancedProjectionShowsRelationshipGateDiagnostics()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRelationshipGatesFixtureAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/afterlife_profiles",
            AdvancedEnabled: true));
        var relationshipTable = Assert.Single(result.Blocks.OfType<UiTableBlock>(), static table => table.Title == "Отношения");
        var text = CollectBlockText([relationshipTable]);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("guardian_mirror_player_trust", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player_soul", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("positive_locked", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("threshold=50", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quest_mirror_oath_trial", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_lock_evidence_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_gate_gm_thoughts_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Душа выбирает правду.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AfterlifeProfiles_DebugProjectionIncludesFullRawState()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await WriteAfterlifeProfilesRawLeakFixtureAsync();

        var advancedRequest = JsonSerializer.Deserialize<ExplorerWebCommandRequest>(
            """{"command":"/afterlife_profiles","advancedEnabled":true}""",
            JsonOptions)!;
        var advancedResult = await _service.ExecuteAsync(advancedRequest);

        AssertContainsAfterlifeProfilesRawState(advancedResult);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughtsResult = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/afterlife_profiles"));

        AssertContainsAfterlifeProfilesRawState(gmThoughtsResult);
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

    private void WriteSessionImage(string relativePath)
    {
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [137, 80, 78, 71, 13, 10, 26, 10]);
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

        await _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Азалия ищет союзников против охотников памяти.",
              "lastChangedTurn": 12,
              "effects": [ "training_discount" ]
            },
            {
              "relationId": "azalia_hidden_dependency",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "relationType": "hidden_dependency",
              "attitudeScore": -80,
              "visibility": "hidden",
              "reason": "Скрытая зависимость не должна отображаться игроку.",
              "lastChangedTurn": 12,
              "effects": []
            }
          ],
          "projects": [],
          "influenceZones": [],
          "chronicle": []
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

    private async Task SeedPendingGachaBaseAsync(string baseRarity, int baseScore, IReadOnlyList<int> diceUsed)
    {
        var diceArray = new JsonArray();
        foreach (var die in diceUsed)
            diceArray.Add(die);

        await _fs.WriteFileAtomicAsync(
            PendingTurnStateService.PendingDiceStatePath,
            new JsonObject
            {
                ["preGeneratedDices1d20"] = new JsonArray(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20),
                ["gachaBaseResult"] = new JsonObject
                {
                    ["diceUsed"] = diceArray,
                    ["baseScore"] = baseScore,
                    ["baseRarity"] = baseRarity,
                    ["formula"] = "client-computed gacha base (range 4-80)"
                },
                ["isFateLocked"] = false,
                ["createdAtUtc"] = "2026-06-02T00:00:00Z",
                ["lastUpdatedUtc"] = "2026-06-02T00:00:00Z"
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private Task WriteGuardianPoliticsRawLeakFixtureAsync() =>
        _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Азалия ищет союзников против охотников памяти.",
              "lastChangedTurn": 12,
              "effects": [ "training_discount" ]
            },
            {
              "relationId": "azalia_hidden_dependency",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "relationType": "hidden_dependency",
              "attitudeScore": -80,
              "visibility": "hidden",
              "reason": "Скрытая зависимость не должна отображаться игроку.",
              "lastChangedTurn": 12,
              "effects": []
            },
            {
              "relationId": "azalia_player_invisible_false",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "system_invisible_false_guardian",
              "relationType": "alliance",
              "attitudeScore": 10,
              "visibility": "known",
              "isPlayerVisible": false,
              "reason": "is_player_visible_false_marker",
              "lastChangedTurn": 12,
              "effects": []
            }
          ],
          "projects": [
            {
              "projectId": "project_archive_pact",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "projectType": "alliance",
              "status": "active",
              "summary": "Публичный архивный пакт укрепляет безопасные маршруты.",
              "currentProgress": 2,
              "requiredProgress": 5,
              "lastUpdatedTurn": 12,
              "visibility": "known"
            },
            {
              "projectId": "secret_project_marker",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "system_saref_shadow",
              "projectType": "rivalry",
              "status": "active",
              "summary": "Секретный проект не должен попасть в обычный DTO.",
              "currentProgress": 1,
              "requiredProgress": 4,
              "lastUpdatedTurn": 12,
              "visibility": "gm_only"
            }
          ],
          "influenceZones": [],
          "chronicle": [],
          "hiddenRelations": [
            {
              "relationId": "hidden_relations_marker",
              "targetGuardianId": "system_saref_shadow"
            }
          ],
          "secretProjects": [
            {
              "projectId": "secret_project_marker"
            }
          ],
          "internalMotivations": {
            "guardian_azalia": "internal_motivation_marker"
          }
        }
        """);

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

    private Task WriteShiningFactionPoliticalMemoryRawLeakFixtureAsync() =>
        _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
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
              "chronicle": [
                {
                  "entryId": "lanterns_safe_passage_45",
                  "turnNumber": 45,
                  "eventType": "public_aid",
                  "summary": "Открыли безопасный проход для потерянных резидентов.",
                  "visibility": "visible",
                  "consequences": [ "Игрок может просить фракцию о публичной помощи." ],
                  "occurredAtUtc": "2026-05-25T12:00:00Z"
                },
                {
                  "entryId": "lanterns_hidden_oath_46",
                  "turnNumber": 46,
                  "eventType": "hidden_oath",
                  "summary": "hidden_chronicle_marker",
                  "visibility": "hidden",
                  "consequences": [ "hidden_chronicle_marker" ],
                  "occurredAtUtc": "2026-05-25T13:00:00Z"
                }
              ],
              "territorialInfluence": [
                {
                  "zoneId": "lanterns_hall_public",
                  "scopeType": "hall",
                  "scopeId": "hall_dawn",
                  "displayName": "Серебряный Зал",
                  "controlLevel": 64,
                  "influenceValue": 58,
                  "publicStatus": "известное убежище",
                  "updatedAtTurn": 46,
                  "sourceEntryId": "lanterns_safe_passage_45",
                  "summary": "Фракция публично удерживает безопасный прием резидентов."
                }
              ],
              "strategicMemory": {
                "summary": "hidden_strategy_marker",
                "lastUpdatedTurn": 46,
                "recentCampaigns": [ "hidden_strategy_marker" ],
                "losses": [ "hidden_strategy_marker" ],
                "alliances": [ "guardian_azalia" ],
                "enemies": [ "hidden_strategy_marker" ]
              },
              "resourceLedger": [
                {
                  "entryId": "lanterns_light_sparks_45",
                  "turnNumber": 45,
                  "resourceType": "lightSparks",
                  "delta": 3,
                  "balanceAfter": 19,
                  "reason": "Публичная помощь привела к пожертвованиям Искр Света.",
                  "internalNote": "hidden_ledger_marker",
                  "occurredAtUtc": "2026-05-25T12:05:00Z"
                }
              ],
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

    private async Task WriteAfterlifeProfilesMaskProjectionFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_masked_truths",
              "displayName": "Хранитель Масок",
              "realm": "Chaos Sea",
              "locationName": "Театр известных лиц",
              "currencies": { "inkFeathers": 1, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 0 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "activeMaskId": "mask_active_envoy",
              "masks": [
                {
                  "maskId": "mask_active_envoy",
                  "displayName": "Активный посланник",
                  "publicArchetype": "дипломат",
                  "visiblePersonality": "улыбается и просит доверия",
                  "concealedTruth": "hidden_active_truth_marker",
                  "directives": [ "hidden_active_directive_marker" ],
                  "revealConditions": [ "hidden_active_condition_marker" ],
                  "deceptionRisk": "high",
                  "linkedThreatId": "hidden_threat_marker",
                  "linkedSarefAgentId": "hidden_saref_marker",
                  "isRevealed": false
                },
                {
                  "maskId": "mask_revealed_sign",
                  "displayName": "Раскрытая вывеска",
                  "publicArchetype": "бывший судья",
                  "visiblePersonality": "говорит прямее после разоблачения",
                  "concealedTruth": "known_revealed_truth_marker",
                  "directives": [ "known_revealed_directive_marker" ],
                  "revealConditions": [ "known_revealed_condition_marker" ],
                  "deceptionRisk": "medium",
                  "linkedThreatId": "known_revealed_threat_marker",
                  "linkedSarefAgentId": "known_revealed_saref_marker",
                  "isRevealed": true
                },
                {
                  "maskId": "mask_dormant_shadow",
                  "displayName": "Скрытый запасной образ",
                  "publicArchetype": "будущий свидетель",
                  "visiblePersonality": "молчит до сцены раскрытия",
                  "concealedTruth": "hidden_dormant_truth_marker",
                  "directives": [ "hidden_dormant_directive_marker" ],
                  "revealConditions": [ "hidden_dormant_condition_marker" ],
                  "deceptionRisk": "critical",
                  "linkedThreatId": "hidden_dormant_threat_marker",
                  "linkedSarefAgentId": "hidden_dormant_saref_marker",
                  "isRevealed": false
                }
              ],
              "goals": {
                "goalId": "goal_masked_truths",
                "shortTermGoal": "Держать известные лица в порядке"
              },
              "soulDissipationTier": 0
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeProfilesRelationshipGatesFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "locationName": "Зал честных отражений",
              "currencies": { "inkFeathers": 4, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 12 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_trust",
                  "axis": "trust",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 49,
                  "relationshipTier": "trust_breakthrough_required",
                  "relationshipLock": {
                    "lockState": "positive_locked",
                    "direction": "positive",
                    "threshold": 50,
                    "breakthroughQuestId": "quest_mirror_oath_trial",
                    "reason": "Хранитель не доверится глубже без личного испытания.",
                    "evidence": "hidden_lock_evidence_marker",
                    "updatedAtTurn": 41
                  },
                  "relationshipGateQuests": [
                    {
                      "questId": "quest_mirror_oath_trial",
                      "questType": "breakthrough",
                      "status": "active",
                      "title": "Суд зеркальной клятвы",
                      "sceneSummary": "Личное испытание доверия.",
                      "successCondition": "Душа выбирает правду.",
                      "gmThoughtsSummary": "hidden_gate_gm_thoughts_marker",
                      "updatedAtTurn": 41
                    }
                  ]
                }
              ],
              "goals": {
                "goalId": "goal_mirror_guardian",
                "shortTermGoal": "Проверить готовность души к правде"
              },
              "soulDissipationTier": 0
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeProfilesRawLeakFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_open_rose",
              "displayName": "Хранитель Открытой Розы",
              "realm": "Chaos Sea",
              "locationName": "Открытая обитель",
              "currencies": { "inkFeathers": 12, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "tier": 1, "experience": 10 },
                "radiance": { "tier": 0, "experience": 0 }
              },
              "standardArts": { "guard": 1 },
              "fateCards": [
                {
                  "cardId": "visible_oath_card",
                  "nameRu": "Открытая карта клятвы",
                  "status": "available",
                  "storyMeaning": "Игрок знает, что клятва может открыть обучение."
                },
                {
                  "cardId": "hidden_saref_card",
                  "nameRu": "Секретная карта Сарефа hidden_fate_card_marker",
                  "status": "hidden",
                  "isSecret": true,
                  "storyMeaning": "hidden_card_story_marker",
                  "unlockConditions": [ "hidden_condition_marker" ]
                }
              ],
              "activeMaskId": "mask_courteous_envoy",
              "masks": [
                {
                  "maskId": "mask_courteous_envoy",
                  "displayName": "Учтивый посредник",
                  "publicArchetype": "мягкий переговорщик",
                  "visiblePersonality": "улыбается и говорит о мире",
                  "concealedTruth": "hidden_concealed_truth_marker",
                  "directives": [ "hidden_mask_directive_marker" ],
                  "linkedSarefAgentId": "hidden_saref_agent_marker",
                  "isRevealed": false
                }
              ],
              "goals": {
                "goalId": "goal_open_guard",
                "shortTermGoal": "Открытая цель: защитить игрока",
                "longTermGoal": "Сохранить обитель",
                "plan": "Говорить только известные игроку части плана.",
                "gmThoughtsSummary": "hidden_actor_motivation_marker"
              },
              "personalQuests": [
                {
                  "questId": "quest_visible_guard",
                  "goalId": "goal_open_guard",
                  "status": "active",
                  "title": "Видимый личный квест",
                  "planSummary": "Проверить клятву без раскрытия тайных мотивов."
                }
              ],
              "currentActivity": {
                "activityId": "activity_visible_watch",
                "goalId": "goal_open_guard",
                "linkedQuestId": "quest_visible_guard",
                "summary": "Собирает видимые сведения",
                "gmThoughtsSummary": "hidden_activity_motivation_marker"
              },
              "soulDissipationTier": 0
            }
          ]
        }
        """);
    }

    private async Task WriteAfterlifeChroniclesRawLeakFixtureAsync()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeChronicleState.StatePath, """
        {
          "schemaVersion": 1,
          "lastInvalidChronicleUpdate": {
            "chronicleId": "hidden_invalid_update_marker"
          },
          "lastInvalidChronicleUpdateReason": "hidden_invalid_reason_marker",
          "chronicles": [
            {
              "chronicleId": "guardian_scene_mirror",
              "scopeType": "guardian_scene",
              "scopeId": "guardian_mirror",
              "displayName": "Зал зеркальной клятвы",
              "eventDescriptions": [
                "[Turn 4] Игрок впервые вошёл в зал отражений.",
                "hidden_chronicle_marker: GM-only archived event"
              ],
              "lastEventsDescription": "[Turn 5] Игрок услышал зов зеркал.",
              "persistentConsequences": [
                "Зал отражений запомнил голос игрока.",
                "secret_consequence_marker"
              ],
              "openThreads": [
                "Понять, почему зеркала зовут игрока.",
                "Не раскрывать игроку hidden_chronicle_marker"
              ],
              "participants": [
                { "actorId": "player_soul", "displayName": "Игрок", "actorType": "player_soul" },
                { "actorId": "guardian_mirror", "displayName": "Хранитель Зеркал", "actorType": "guardian" },
                { "actorId": "secret_participant_marker", "displayName": "secret_participant_marker", "visibility": "gm_only" },
                { "actorId": "moon_witness", "displayName": "moon_visible_to_player_false_marker", "visibleToPlayer": false },
                { "actorId": "silent_witness", "displayName": "silent_boolean_hidden_marker", "isHidden": true }
              ],
              "linkedActors": [
                { "actorId": "internal_scope_marker", "displayName": "internal_scope_marker", "isPlayerVisible": false },
                { "actorId": "closed_architect", "displayName": "closed_gm_only_marker", "gmOnly": true }
              ],
              "gmThoughtsSummary": "hidden_chronicle_marker",
              "secretPlan": "secret_chronicle_marker",
              "internalNotes": "internal_chronicle_marker",
              "_debug": "hidden_debug_marker",
              "lastUpdatedTurn": 5
            },
            {
              "chronicleId": "moon_witness_scene",
              "scopeType": "guardian_scene",
              "scopeId": "moon_witness",
              "displayName": "moon_visible_to_player_false_marker",
              "visibleToPlayer": false,
              "lastEventsDescription": "[Turn 6] moon_visible_to_player_false_marker sees a closed oath.",
              "lastUpdatedTurn": 6
            },
            {
              "chronicleId": "quiet_deal_scene",
              "scopeType": "guardian_scene",
              "scopeId": "quiet_deal",
              "displayName": "quiet_deal_boolean_secret_marker",
              "isSecret": true,
              "lastEventsDescription": "[Turn 7] quiet_deal_boolean_secret_marker stays behind the curtain.",
              "lastUpdatedTurn": 7
            }
          ]
        }
        """);
    }

    private async Task SeedInventoryEquipmentItemsAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "equipment": {
            "head": "helmet_1",
            "mainHand": null,
            "offHand": null
          },
          "items": [
            { "existedId": "sword_1", "name": "Кривой меч", "type": "weapon", "durability": "100%" },
            { "existedId": "helmet_1", "name": "Железный шлем", "type": "helmet", "durability": "100%" },
            { "existedId": "torch_1", "name": "Факел", "type": "utility", "count": 2 },
            { "existedId": "broken_bow_1", "name": "Сломанный лук", "type": "weapon", "durability": "0%" },
            { "relicId": "soul_relic_1", "name": "Реликвия души", "type": "soul_relic", "equipmentSlot": "ring1" }
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

    private static string SerializeResult(ExplorerCommandResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    private static void AssertContainsGuardianPoliticsRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(ChaosSeaGuardianPoliticsState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hiddenRelations", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secretProjects", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internalMotivations", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("system_saref_shadow", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("system_invisible_false_guardian", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret_project_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_player_visible_false_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal_motivation_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsAfterlifeProfilesRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(AfterlifeEntityProfileState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_actor_motivation_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_activity_motivation_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_fate_card_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_card_story_marker", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_concealed_truth_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_mask_directive_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_saref_agent_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsAfterlifeChroniclesRawState(ExplorerCommandResult result)
    {
        var raw = Assert.Single(result.Blocks.OfType<UiRawJsonBlock>());
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains(AfterlifeChronicleState.StatePath, raw.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gmThoughtsSummary", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lastInvalidChronicleUpdate", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret_participant_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal_scope_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertContainsShiningPoliticsRawState(ExplorerCommandResult result)
    {
        var rawBlocks = result.Blocks.OfType<UiRawJsonBlock>().ToList();
        var raw = Assert.Single(rawBlocks, static block =>
            block.Title.Contains(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase));
        var rawText = raw.Json?.ToJsonString(JsonOptions) ?? string.Empty;
        var payload = SerializeResult(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("strategicMemory", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resourceLedger", rawText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_chronicle_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_strategy_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hidden_ledger_marker", payload, StringComparison.OrdinalIgnoreCase);
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
