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

public sealed class BrowserTradeParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly ExplorerWebPromptSessionService _promptSessions;
    private readonly BrowserMortalWorldWriteService _mortalWriteService;
    private readonly BrowserAfterlifeWriteService _afterlifeWriteService;

    public BrowserTradeParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-trade-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        _mortalWriteService = new BrowserMortalWorldWriteService(
            _fs,
            coordinator,
            new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
        _afterlifeWriteService = new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator);
        _promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: _mortalWriteService,
            afterlifeWriteService: _afterlifeWriteService);
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, _promptSessions);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_NpcTrade_ReturnsPromptWithBuySellAndBuybackChoices()
    {
        await SeedStoryTurnAsync(12);
        await SeedNpcTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableInventoryItem: true, includeBuybackInventory: true);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_trade npc_merchant_001",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        var choice = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "npc_trade_choice"));
        Assert.Contains(choice.Options, option => option.Value == "buy:npc_trade_slot_001");
        Assert.Contains(choice.Options, option => option.Value == "sell:item_sell_lantern_001");
        Assert.Contains(choice.Options, option => option.Value == "buyback:npc_buyback_001");
        Assert.Contains(result.Prompts, prompt => prompt.Id == "confirm_trade_write");
        Assert.Empty(result.Blocks.SelectMany(EnumerateTables));
        var dossier = Assert.Single(result.Blocks.SelectMany(EnumerateEntityDossiers), static block =>
            block.EntityType == "npc-trade" &&
            block.Title == "Торговля с НПС");
        Assert.Contains(dossier.Sections, static section => section.Id == "npc-trade-buy" && section.Presentation == "cards");
        Assert.Contains(dossier.Sections, static section => section.Id == "npc-trade-sell" && section.Presentation == "cards");
        Assert.Contains(dossier.Sections, static section => section.Id == "npc-trade-buyback" && section.Presentation == "cards");

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Марек", text, StringComparison.Ordinal);
        Assert.Contains("Полевой набор торговца", text, StringComparison.Ordinal);
        Assert.Contains("Походный фонарь", text, StringComparison.Ordinal);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task UniversalTradeInMortalWorld_ListsLocalMerchantThenSubmitsThroughNpcTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedNpcTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableInventoryItem: false, includeBuybackInventory: false);
        await AddRemoteNpcMerchantAsync();

        var selection = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/торговля",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Completed, selection.State);
        Assert.Null(selection.InteractiveSession);
        Assert.Empty(selection.Prompts);
        var merchantCard = Assert.Single(
            selection.Blocks.SelectMany(EnumerateEntityDossiers)
                .SelectMany(dossier => dossier.Sections)
                .SelectMany(section => section.Cards),
            card => card.Title.Contains("Марек", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(merchantCard.PrimaryAction);
        var merchantAction = merchantCard.PrimaryAction!;
        Assert.Equal("/npc_trade npc_merchant_001", merchantAction.Command);
        Assert.DoesNotContain("npc_merchant_001", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Дальний торговец", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        Assert.False(_fs.FileExists(NpcTradeRequestState.PendingRequestPath));

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            merchantAction.Command,
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.Equal("/npc_trade npc_merchant_001", prompt.Command);
        Assert.NotNull(prompt.InteractiveSession);
        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("npc_trade_choice", "buy:npc_trade_slot_001"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var playerStatus = JsonNode.Parse((await _fs.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
        Assert.Equal(390, playerStatus["money"]!.GetValue<int>());
    }

    [Theory]
    [Trait("Category", "BrowserTradeParity")]
    [InlineData("buy:npc_trade_slot_001", "npc_item_merchant_001", 390)]
    [InlineData("sell:item_sell_lantern_001", "item_sell_lantern_001", 506)]
    [InlineData("buyback:npc_buyback_001", "item_sell_lantern_001", 492)]
    public async Task TryApplyAsync_NpcTradeSubmission_DelegatesToNpcTradeService(string operation, string affectedItemId, int expectedMoney)
    {
        await SeedStoryTurnAsync(12);
        await SeedNpcTradeStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableInventoryItem: operation.StartsWith("sell:", StringComparison.Ordinal),
            includeBuybackInventory: operation.StartsWith("buyback:", StringComparison.Ordinal));

        var result = await _mortalWriteService.TryApplyAsync(
            "/npc_trade npc_merchant_001",
            Answers(("npc_trade_choice", operation), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var inventory = JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
        var playerStatus = JsonNode.Parse((await _fs.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
        var npcRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        var items = inventory["items"]!.AsArray();
        var npc = npcRoot["UpdateNPCs"]!.AsArray()[0]!.AsObject();

        Assert.Equal(expectedMoney, playerStatus["money"]!.GetValue<int>());
        if (operation.StartsWith("buy:", StringComparison.Ordinal))
        {
            Assert.Contains(items, item => item!["itemId"]!.GetValue<string>() == affectedItemId);
            Assert.True(npc["tradeInventory"]!["items"]!.AsArray()[0]!["soldOut"]!.GetValue<bool>());
        }
        else if (operation.StartsWith("sell:", StringComparison.Ordinal))
        {
            Assert.DoesNotContain(items, item => item!["itemId"]!.GetValue<string>() == affectedItemId);
            Assert.Equal("available", npc["buybackInventory"]!.AsArray()[0]!["status"]!.GetValue<string>());
        }
        else
        {
            Assert.Contains(items, item => item!["itemId"]!.GetValue<string>() == affectedItemId);
            Assert.Equal("rebought", npc["buybackInventory"]!.AsArray()[0]!["status"]!.GetValue<string>());
        }
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_NpcTradeWithoutInventory_ReturnsPendingGmAction()
    {
        await SeedStoryTurnAsync(12);
        await SeedNpcTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeSellableInventoryItem: false, includeBuybackInventory: false);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc_trade npc_merchant_001",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Contains(NpcTradeRequestState.ActionTag, result.PendingGmAction, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Prompts);
        Assert.Null(result.InteractiveSession);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_ShiningTrade_ReturnsPromptAndDocumentsUnsupportedSellBoundary()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_trade faction_old",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        var choice = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "shining_trade_choice"));
        Assert.Contains(choice.Options, option => option.Value == "buy:slot_1");
        Assert.DoesNotContain(choice.Options, option => option.Value.StartsWith("sell:", StringComparison.Ordinal));

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Старый Дом", text, StringComparison.Ordinal);
        Assert.Contains("Торговая Реликвия", text, StringComparison.Ordinal);
        Assert.Contains("Продажа сияющим фракциям", text, StringComparison.Ordinal);
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task UniversalTradeInShiningAbode_ListsFactionThenSubmitsThroughShiningTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);

        var selection = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/торговля",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Completed, selection.State);
        Assert.Null(selection.InteractiveSession);
        Assert.Empty(selection.Prompts);
        var factionCard = Assert.Single(
            selection.Blocks.SelectMany(EnumerateEntityDossiers)
                .SelectMany(dossier => dossier.Sections)
                .SelectMany(section => section.Cards),
            card => card.Title.Contains("Старый Дом", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(factionCard.PrimaryAction);
        var factionAction = factionCard.PrimaryAction!;
        Assert.Equal("/shining_trade faction_old", factionAction.Command);
        Assert.DoesNotContain("faction_old", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        Assert.False(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            factionAction.Command,
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.Equal("/shining_trade faction_old", prompt.Command);
        Assert.NotNull(prompt.InteractiveSession);
        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("shining_trade_choice", "buy:slot_1"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Contains(soul["soulRelics"]!["stored"]!.AsArray(), relic =>
            relic!["relicId"]!.GetValue<string>() == "relic_trade_1");
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task UniversalTradeDuringShiningPendingBootstrap_BlocksWithoutPromptSessionOrLock()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);
        await SetShiningPendingBootstrapAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/торговля",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        Assert.Contains("обычной активной Сияющей Обители", CollectResultText(result), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_ShiningTrade_ExposesOfferDetailsBeforeBuyConfirmation()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);

        var overview = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_trade faction_old",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.RequiresInput, overview.State);
        var action = Assert.Single(overview.Actions, item => item.Id == "shining-trade-offer-detail-faction_old-slot_1");
        Assert.Equal("/shining_trade faction_old товар slot_1", action.Command);
        Assert.Contains("Торговая Реликвия", action.Label, StringComparison.Ordinal);
        Assert.False(action.RequiresConfirmation);

        var detail = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            action.Command,
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Empty(detail.Prompts);
        var text = CollectBlockText(detail.Blocks);
        Assert.Contains("Товар сияющей торговли: Торговая Реликвия", text, StringComparison.Ordinal);
        Assert.Contains("Старый Дом", text, StringComparison.Ordinal);
        Assert.Contains("70 Чернильных Перьев", text, StringComparison.Ordinal);
        Assert.Contains("Тестовая реликвия сияющей фракции", text, StringComparison.Ordinal);
        Assert.Contains("Видимое сияние", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden_trade_property_marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(detail.Actions, item => item.Command == "/shining_trade faction_old");
        AssertNoRawTradeDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task TryApplyAsync_ShiningTradeRequest_DelegatesToShiningTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: false);
        await _stateManager.RefreshGameStateAsync();

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/shining_trade faction_old",
            Answers(("shining_trade_choice", "request:faction_old"), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Success, result.Message);
        var pendingRaw = await _fs.ReadFileAsync(ShiningTradeRequestState.PendingRequestsPath);
        Assert.Contains("\"factionId\": \"faction_old\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"createdAtTurn\": 12", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task TryApplyAsync_ShiningTradeBuy_DelegatesToShiningTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);
        await _stateManager.RefreshGameStateAsync();

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/shining_trade faction_old",
            Answers(("shining_trade_choice", "buy:slot_1"), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Success, result.Message);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var stored = soul["soulRelics"]!["stored"]!.AsArray();
        Assert.Contains(stored, relic => relic!["relicId"]!.GetValue<string>() == "relic_trade_1");
        Assert.Equal(10, soul["inkFeathers"]!["current"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task SubmitAsync_ShiningTradeFailure_ReturnsPlayerFacingFailureCopyWithoutRollbackLeak()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);
        await _stateManager.RefreshGameStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_trade faction_old",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SetSoulInkFeathersAsync(1);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("shining_trade_choice", "buy:slot_1"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertPlayerFacingFailureCopy(result);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task SubmitAsync_ShiningTradeMalformedPendingFailure_ReturnsPlayerFacingFailureCopyWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);
        await _stateManager.RefreshGameStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/shining_trade faction_old",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.NotNull(prompt.InteractiveSession);
        await _fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{ malformed json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("shining_trade_choice", "buy:slot_1"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertPlayerFacingFailureCopy(result);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task TryApplyAsync_SourceOfLightMalformedPendingFailure_UsesGenericFailureCopyWithoutTradeLeak()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true);
        await _stateManager.RefreshGameStateAsync();
        await _fs.WriteFileAtomicAsync(SourceOfLightCapstoneState.PendingRequestPath, "{ malformed json");

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/source_of_light",
            Answers(("source_of_light_action", "open")),
            Owner("browser-trade-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.Contains("действие временно ждёт проверки ГМ", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("торгов", result.Message, StringComparison.OrdinalIgnoreCase);
        AssertNoRawTradeDiagnosticText(result.Message);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task TryApplyAsync_ShiningTradeSell_ReturnsAuthorityBoundaryWithoutMutation()
    {
        await SeedStoryTurnAsync(12);
        await SeedShiningTradeStateAsync(withReadyInventory: true, includeStoredRelic: true);
        await _stateManager.RefreshGameStateAsync();
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/shining_trade faction_old",
            Answers(("shining_trade_choice", "sell:stored_shining_sell_001"), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Handled);
        Assert.False(result.Success);
        Assert.True(result.KeepSessionOpen);
        Assert.Contains("Продажа сияющим фракциям", result.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_GuardianTrade_ReturnsPromptWithBuySellAndBuybackChoices()
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableRelic: true, includeBuybackEntry: true);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_trade guardian_alpha",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        var choice = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "guardian_trade_choice"));
        Assert.Contains(choice.Options, option => option.Value == "buy:trade_1");
        Assert.Contains(choice.Options, option => option.Value == "sell:relic_sell_001");
        Assert.Contains(choice.Options, option => option.Value == "buyback:guardian_buyback_001");

        var text = CollectBlockText(result.Blocks);
        Assert.Contains("Азалия", text, StringComparison.Ordinal);
        Assert.Contains("Печать Сумеречного Порога", text, StringComparison.Ordinal);
        Assert.Contains("Отзвук Зеркального Двора", text, StringComparison.Ordinal);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task UniversalTradeInChaosSea_ListsLocalGuardianThenSubmitsThroughGuardianTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableRelic: false, includeBuybackEntry: false);
        await AddRemoteGuardianAsync();
        await AddInactiveGuardianInCurrentAbodeAsync();

        var selection = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/торговля",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        Assert.Equal(CommandExecutionState.Completed, selection.State);
        Assert.Null(selection.InteractiveSession);
        Assert.Empty(selection.Prompts);
        var guardianCard = Assert.Single(
            selection.Blocks.SelectMany(EnumerateEntityDossiers)
                .SelectMany(dossier => dossier.Sections)
                .SelectMany(section => section.Cards),
            card => card.Title.Contains("Азалия", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(guardianCard.PrimaryAction);
        var guardianAction = guardianCard.PrimaryAction!;
        Assert.Equal("/guardian_trade guardian_alpha", guardianAction.Command);
        Assert.DoesNotContain("guardian_alpha", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Хранитель Дальнего Берега", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Соседний Хранитель", CollectResultText(selection), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        Assert.False(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));

        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            guardianAction.Command,
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.Equal("/guardian_trade guardian_alpha", prompt.Command);
        Assert.NotNull(prompt.InteractiveSession);
        Assert.DoesNotContain(prompt.Prompts, item => item.Id == "guardian_id");
        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("guardian_trade_choice", "buy:trade_1"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Contains(soul["soulRelics"]!["stored"]!.AsArray(), relic =>
            relic!["relicId"]!.GetValue<string>() == "relic_1");
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task UniversalTradeWithoutResolvedRealm_ReturnsLocalizedFailureWithoutPendingRequest()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{}");

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest("/торговля"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Contains("реальность не определена", CollectResultText(result), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(NpcTradeRequestState.PendingRequestPath));
        Assert.False(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
        Assert.False(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task ExecuteAsync_GuardianTradeDuplicateInventory_ReturnsPlayerFacingStatusWithoutDiagnostics()
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableRelic: true, includeBuybackEntry: true);
        await DuplicateGuardianTradeSlotIdAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_trade guardian_alpha",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));

        var text = CollectResultText(result);
        Assert.Contains("Торговля временно ждёт проверки ГМ", text, StringComparison.Ordinal);
        AssertNoRawTradeDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task TryApplyAsync_GuardianTradeRequest_DelegatesToGuardianTradeService()
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(includeTradeInventory: false, includeTradeReceipt: false, includeSellableRelic: false, includeBuybackEntry: false);
        await _stateManager.RefreshGameStateAsync();

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/guardian_trade guardian_alpha",
            Answers(("guardian_trade_choice", "request:guardian_alpha"), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Success, result.Message);
        var pendingRaw = await _fs.ReadFileAsync(GuardianTradeRequestState.PendingRequestPath);
        Assert.Contains("\"guardianId\": \"guardian_alpha\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"createdAtTurn\": 12", pendingRaw, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Category", "BrowserTradeParity")]
    [InlineData("buy:trade_1", "relic_1", 70)]
    [InlineData("sell:relic_sell_001", "relic_sell_001", 160)]
    [InlineData("buyback:guardian_buyback_001", "relic_buyback_001", 40)]
    public async Task TryApplyAsync_GuardianTradeSubmission_DelegatesToGuardianTradeService(string operation, string affectedRelicId, int expectedFeathers)
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(
            includeTradeInventory: true,
            includeTradeReceipt: true,
            includeSellableRelic: operation.StartsWith("sell:", StringComparison.Ordinal),
            includeBuybackEntry: operation.StartsWith("buyback:", StringComparison.Ordinal));
        await _stateManager.RefreshGameStateAsync();

        var result = await _afterlifeWriteService.TryApplyAsync(
            "/guardian_trade guardian_alpha",
            Answers(("guardian_trade_choice", operation), ("confirm_trade_write", true)),
            Owner("browser-trade-test"));

        Assert.True(result.Success, result.Message);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var stored = soul["soulRelics"]!["stored"]!.AsArray();
        Assert.Equal(expectedFeathers, soul["inkFeathers"]!["current"]!.GetValue<int>());
        if (operation.StartsWith("sell:", StringComparison.Ordinal))
        {
            Assert.DoesNotContain(stored, relic => relic!["relicId"]!.GetValue<string>() == affectedRelicId);
        }
        else
        {
            Assert.Contains(stored, relic => relic!["relicId"]!.GetValue<string>() == affectedRelicId);
        }
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public async Task SubmitAsync_GuardianTradeFailure_ReturnsPlayerFacingFailureCopyWithoutRollbackLeak()
    {
        await SeedStoryTurnAsync(12);
        await SeedGuardianTradeStateAsync(includeTradeInventory: true, includeTradeReceipt: true, includeSellableRelic: false, includeBuybackEntry: false);
        await _stateManager.RefreshGameStateAsync();
        var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/guardian_trade guardian_alpha",
            OwnerId: "browser-trade-test",
            OwnerLabel: "Browser trade test"));
        Assert.NotNull(prompt.InteractiveSession);
        await SetSoulInkFeathersAsync(1);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _promptSessions.SubmitAsync(new ExplorerPromptSessionSubmitRequest(
            prompt.InteractiveSession!.SessionId,
            Answers(("guardian_trade_choice", "buy:trade_1"), ("confirm_trade_write", true)),
            OwnerId: "browser-trade-test"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertPlayerFacingFailureCopy(result);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    [Trait("Category", "BrowserTradeParity")]
    public void BrowserCommandCoverage_Issue805TradeCommandsAreCovered()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[] { "npc_trade", "shining_trade", "guardian_trade" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#805", command.FollowUpIssue, StringComparison.Ordinal);
        }

        var npcs = Assert.Single(coverage.Commands, item => item.Id == "npcs");
        Assert.DoesNotContain("#805", npcs.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("trade and start-conversation flows remain tracked", npcs.GapSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#807", npcs.FollowUpIssue, StringComparison.Ordinal);
    }

    private async Task SeedStoryTurnAsync(int turnNumber)
    {
        await _fs.WriteFileAtomicAsync("stories/web-trade-test.json", $$"""
        {
          "turnNumber": {{turnNumber}}
        }
        """);
    }

    private async Task SeedNpcTradeStateAsync(
        bool includeTradeInventory,
        bool includeTradeReceipt,
        bool includeSellableInventoryItem,
        bool includeBuybackInventory)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "money": 500,
          "trade": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "currentTimeInMinutes": 100
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_market_square",
          "name": "Рыночная площадь"
        }
        """);

        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            includeSellableInventoryItem
                ? """
                  {
                    "items": [
                      {
                        "itemId": "item_sell_lantern_001",
                        "name": "Походный фонарь",
                        "quality": "Common",
                        "type": "tool",
                        "price": 20,
                        "baseSellPrice": 8
                      }
                    ],
                    "equipment": {}
                  }
                  """
                : """
                  {
                    "items": [],
                    "equipment": {}
                  }
                  """);

        var inventoryBlock = includeTradeInventory
            ? """
              ,
              "tradeInventory": {
                "tradeCycleId": "world_trade_0",
                "generatedAtWorldDate": 100,
                "refreshAfterWorldDate": 43200,
                "generationTradeTier": "Good",
                "pricingTradeTier": "Neutral",
                "items": [
                  {
                    "slotId": "npc_trade_slot_001",
                    "itemId": "npc_item_merchant_001",
                    "price": 110,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_001",
                      "name": "Полевой набор торговца",
                      "description": "Тестовый ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Rare",
                      "price": 90,
                      "baseSellPrice": 36,
                      "weight": "1.0",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_002",
                    "itemId": "npc_item_merchant_002",
                    "price": 37,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_002",
                      "name": "Карта соседних кварталов",
                      "description": "Тестовый ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 30,
                      "baseSellPrice": 12,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_003",
                    "itemId": "npc_item_merchant_003",
                    "price": 25,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_003",
                      "name": "Запас крепежа",
                      "description": "Тестовый ассортимент.",
                      "type": "Material",
                      "tradeItemClass": "Material",
                      "quality": "Common",
                      "price": 20,
                      "baseSellPrice": 8,
                      "weight": "0.4",
                      "group": "Материалы"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_004",
                    "itemId": "npc_item_merchant_004",
                    "price": 49,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_004",
                      "name": "Дорожный фонарь",
                      "description": "Тестовый ассортимент.",
                      "type": "Tool",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 40,
                      "baseSellPrice": 16,
                      "weight": "0.8",
                      "group": "Инструменты"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_005",
                    "itemId": "npc_item_merchant_005",
                    "price": 74,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_005",
                      "name": "Плотный плащ",
                      "description": "Тестовый ассортимент.",
                      "type": "Armor",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 60,
                      "baseSellPrice": 24,
                      "weight": "1.5",
                      "group": "Защита"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_006",
                    "itemId": "npc_item_merchant_006",
                    "price": 61,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_006",
                      "name": "Складной кофр",
                      "description": "Тестовый ассортимент.",
                      "type": "Container",
                      "tradeItemClass": "Functional",
                      "quality": "Uncommon",
                      "price": 50,
                      "baseSellPrice": 20,
                      "weight": "1.2",
                      "group": "Контейнеры"
                    }
                  },
                  {
                    "slotId": "npc_trade_slot_007",
                    "itemId": "npc_item_merchant_007",
                    "price": 31,
                    "merchantProfile": "GeneralGoods",
                    "soldOut": false,
                    "itemData": {
                      "itemId": "npc_item_merchant_007",
                      "name": "Записная книжка",
                      "description": "Тестовый ассортимент.",
                      "type": "Document",
                      "tradeItemClass": "FlavorOrUtility",
                      "quality": "Common",
                      "price": 25,
                      "baseSellPrice": 10,
                      "weight": "0.1",
                      "group": "Документы и медиа"
                    }
                  }
                ]
              }
            """
            : "";

        var receiptsBlock = includeTradeInventory && includeTradeReceipt
            ? """
              ,
              "tradeInventoryReceipts": [
                {
                  "requestId": "npc_trade_req_seed_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "tradeCycleId": "world_trade_0",
                  "merchantProfile": "GeneralGoods",
                  "status": "ready",
                  "itemCount": 7,
                  "resolvedAtTurn": 7,
                  "resolvedAtUtc": "2026-03-28T00:05:00Z"
                }
              ]
            """
            : "";

        var buybackBlock = includeBuybackInventory
            ? """
              ,
              "buybackInventory": [
                {
                  "buybackEntryId": "npc_buyback_001",
                  "npcId": "npc_merchant_001",
                  "npcName": "Марек",
                  "itemId": "item_sell_lantern_001",
                  "itemData": {
                    "itemId": "item_sell_lantern_001",
                    "name": "Походный фонарь",
                    "description": "Ранее проданный фонарь.",
                    "type": "tool",
                    "tradeItemClass": "Functional",
                    "quality": "Common",
                    "price": 20,
                    "baseSellPrice": 8
                  },
                  "soldByPlayerAtTurn": 6,
                  "soldByPlayerAtUtc": "2026-03-28T00:04:00Z",
                  "soldAtWorldDate": 95,
                  "soldForPrice": 8,
                  "buybackPrice": 8,
                  "acquiredFromPlayer": true,
                  "sourceMerchantProfile": "GeneralGoods",
                  "status": "available"
                }
              ]
            """
            : "";

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", $$"""
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_merchant_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              }{{inventoryBlock}}{{receiptsBlock}}{{buybackBlock}}
            }
          ]
        }
        """);
    }

    private async Task SeedShiningTradeStateAsync(bool withReadyInventory, bool includeStoredRelic = false)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = includeStoredRelic
                    ? new JsonArray(new JsonObject
                    {
                        ["relicId"] = "stored_shining_sell_001",
                        ["name"] = "Сияющая реликвия продажи",
                        ["rarity"] = "Rare",
                        ["quality"] = "Rare"
                    })
                    : new JsonArray()
            }
        }.ToJsonString());

        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeProvision,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyResource,
                    ["summary"] = "Торговая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 62,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["tradeInventoryReceipts"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };

        if (withReadyInventory)
        {
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            faction["tradeInventory"] = new JsonObject
            {
                ["tradeCycleId"] = "shining_return_2",
                ["generatedAtUtc"] = "2026-04-17T00:10:00Z",
                ["generationTradeTier"] = 2,
                ["generationRarityCeiling"] = "rare",
                ["serviceMultiplierSnapshot"] = 1.25,
                ["merchantProfile"] = "shining_faction",
                ["items"] = new JsonArray
                {
                    CreateShiningTradeSlot("slot_1", "relic_trade_1", "Торговая Реликвия", "Rare", 70),
                    CreateShiningTradeSlot("slot_2", "relic_trade_2", "Малая Реликвия", "Common", 30),
                    CreateShiningTradeSlot("slot_3", "relic_trade_3", "Малая Реликвия II", "Common", 30),
                    CreateShiningTradeSlot("slot_4", "relic_trade_4", "Малая Реликвия III", "Common", 30),
                    CreateShiningTradeSlot("slot_5", "relic_trade_5", "Малая Реликвия IV", "Common", 30),
                    CreateShiningTradeSlot("slot_6", "relic_trade_6", "Малая Реликвия V", "Common", 30)
                }
            };
            faction["tradeInventoryReceipts"] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "shining_trade_existing",
                    ["factionId"] = "faction_old",
                    ["factionName"] = "Старый Дом",
                    ["tradeCycleId"] = "shining_return_2",
                    ["status"] = "ready",
                    ["itemCount"] = 6,
                    ["soldOutCount"] = 0,
                    ["resolvedAtTurn"] = 9,
                    ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
                }
            };
        }

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, new JsonObject
        {
            ["entries"] = new JsonArray()
        }.ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        }.ToJsonString());
    }

    private async Task SetShiningPendingBootstrapAsync()
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        root["preparedIncarnationPackage"] = new JsonObject
        {
            ["generatedFromDraftVersion"] = 4,
            ["preparedAtTurn"] = 12,
            ["preparedAtUtc"] = "2026-04-19T10:00:00Z",
            ["selectedCardIds"] = new JsonArray("card_route_dawn"),
            ["selectedCards"] = new JsonArray
            {
                new JsonObject
                {
                    ["cardId"] = "card_route_dawn",
                    ["dedupeKey"] = "route_dawn",
                    ["sourceType"] = "project",
                    ["sourceFactionId"] = "faction_old",
                    ["sourceActorId"] = "project_passage",
                    ["effectFamily"] = "route",
                    ["rarity"] = "Epic",
                    ["displayName"] = "Тропа возвращения",
                    ["displaySummary"] = "Открывает путь через память.",
                    ["effectPayload"] = new JsonObject
                    {
                        ["routeSeedId"] = "route_dawn",
                        ["remainingUses"] = 1
                    }
                }
            }
        };

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString());
    }

    private static JsonObject CreateShiningTradeSlot(string slotId, string relicId, string name, string quality, int price) => new()
    {
        ["slotId"] = slotId,
        ["priceInFeathers"] = price,
        ["soldOut"] = false,
        ["relicData"] = new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = name,
            ["quality"] = quality,
            ["rarity"] = quality,
            ["description"] = "Тестовая реликвия сияющей фракции.",
            ["properties"] = new JsonArray
            {
                new JsonObject
                {
                    ["displayName"] = "Видимое сияние",
                    ["summary"] = "Игрок видит это свойство перед покупкой."
                },
                new JsonObject
                {
                    ["displayName"] = "hidden_trade_property_marker",
                    ["visibility"] = "gm_only",
                    ["summary"] = "hidden_trade_property_marker"
                }
            }
        }
    };

    private async Task SeedGuardianTradeStateAsync(
        bool includeTradeInventory,
        bool includeTradeReceipt,
        bool includeSellableRelic,
        bool includeBuybackEntry)
    {
        var tradeInventoryJson = includeTradeInventory
            ? """
              ,
              "tradeInventory": {
                "tradeCycleId": "return_1",
                "generatedAtUtc": "2026-03-26T00:00:00Z",
                "generationReputationTier": "Friendly",
                "pricingReputationTier": "Friendly",
                "projectBonusSignature": "0|0|0",
                "upgradedTradeSlots": 0,
                "elevatedTradeSlots": 0,
                "effectiveRarityCeilingBonusSteps": 0,
                "items": [
                  {
                    "slotId": "trade_1",
                    "priceInFeathers": 30,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_1",
                      "name": "Печать Сумеречного Порога",
                      "rarity": "Common",
                      "quality": "Common",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_2",
                    "priceInFeathers": 70,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_2",
                      "name": "Колье Шёпота",
                      "rarity": "Uncommon",
                      "quality": "Uncommon",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_3",
                    "priceInFeathers": 140,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_3",
                      "name": "Знак Грёзы",
                      "rarity": "Rare",
                      "quality": "Rare",
                      "description": "Тестовая явная витрина."
                    }
                  },
                  {
                    "slotId": "trade_4",
                    "priceInFeathers": 140,
                    "domainTag": "Сны и Переходы",
                    "soldOut": false,
                    "rarityBonusStepsApplied": 0,
                    "relicData": {
                      "relicId": "relic_4",
                      "name": "Плащ Межпорога",
                      "rarity": "Rare",
                      "quality": "Rare",
                      "description": "Тестовая явная витрина."
                    }
                  }
                ]
              }
            """
            : "";

        var receiptJson = includeTradeInventory && includeTradeReceipt
            ? """
              ,
              "tradeInventoryReceipts": [
                {
                  "requestId": "guardian_trade_req_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "tradeCycleId": "return_1",
                  "status": "ready",
                  "itemCount": 4,
                  "resolvedAtTurn": 7,
                  "resolvedAtUtc": "2026-03-26T00:10:00Z"
                }
              ]
            """
            : "";

        var buybackJson = includeBuybackEntry
            ? """
              ,
              "buybackRelics": [
                {
                  "buybackEntryId": "guardian_buyback_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "relicId": "relic_buyback_001",
                  "relicData": {
                    "relicId": "relic_buyback_001",
                    "name": "Отзвук Зеркального Двора",
                    "rarity": "Rare",
                    "description": "Ранее проданная реликвия."
                  },
                  "soldByPlayerAtTurn": 11,
                  "soldByPlayerAtUtc": "2026-03-26T00:10:00Z",
                  "soldForPrice": 60,
                  "buybackPrice": 60,
                  "acquiredFromPlayer": true,
                  "status": "available"
                }
              ]
            """
            : "";

        var guardianBody = $$"""
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "domain": "Порог Сна",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 120, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "abode": { "abodeId": "abode_alpha", "name": "Тестовая обитель" },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }{{tradeInventoryJson}}{{receiptJson}}{{buybackJson}}
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", $$"""
        {
          "guardians": [
            {
            {{guardianBody}}
            }
          ],
          "activeGuardian": {
          {{guardianBody}}
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_alpha"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Chaos Sea",
            ["currentIncarnation"] = 1,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 100
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = includeSellableRelic
                    ? new JsonArray(new JsonObject
                    {
                        ["relicId"] = "relic_sell_001",
                        ["name"] = "Реликвия для продажи",
                        ["rarity"] = "Rare",
                        ["quality"] = "Rare",
                        ["description"] = "Тестовая реликвия для продажи."
                    })
                    : new JsonArray()
            }
        }.ToJsonString());
    }

    private async Task DuplicateGuardianTradeSlotIdAsync()
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/guardians.json"))!)!.AsObject();
        foreach (var guardian in new[]
        {
            root["guardians"]!.AsArray()[0]!.AsObject(),
            root["activeGuardian"]!.AsObject()
        })
        {
            var items = guardian["tradeInventory"]!["items"]!.AsArray();
            items[1]!.AsObject()["slotId"] = "trade_1";
        }

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", root.ToJsonString());
    }

    private async Task AddRemoteNpcMerchantAsync()
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        root["UpdateNPCs"]!.AsArray().Add(new JsonObject
        {
            ["npcId"] = "npc_remote_merchant",
            ["name"] = "Дальний торговец",
            ["currentLocationId"] = "loc_remote_market",
            ["currentLocation"] = "Дальний рынок",
            ["tradeState"] = new JsonObject
            {
                ["canTrade"] = true,
                ["merchantProfile"] = "GeneralGoods"
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", root.ToJsonString());
    }

    private async Task AddRemoteGuardianAsync()
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/guardians.json"))!)!.AsObject();
        root["guardians"]!.AsArray().Add(new JsonObject
        {
            ["guardianId"] = "guardian_remote",
            ["canonicalName"] = "Хранитель Дальнего Берега",
            ["domain"] = "Дальние пути",
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "Хранитель Дальнего Берега"
            },
            ["abode"] = new JsonObject
            {
                ["abodeId"] = "abode_remote",
                ["name"] = "Дальний берег"
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", root.ToJsonString());
    }

    private async Task AddInactiveGuardianInCurrentAbodeAsync()
    {
        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/guardians.json"))!)!.AsObject();
        root["guardians"]!.AsArray().Add(new JsonObject
        {
            ["guardianId"] = "guardian_same_abode_inactive",
            ["canonicalName"] = "Соседний Хранитель",
            ["domain"] = "Отражения",
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "Соседний Хранитель"
            },
            ["abode"] = new JsonObject
            {
                ["abodeId"] = "abode_alpha",
                ["name"] = "Тестовая обитель"
            }
        });
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", root.ToJsonString());
    }

    private async Task SetSoulInkFeathersAsync(int amount)
    {
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var inkFeathers = soul["inkFeathers"] as JsonObject ?? new JsonObject();
        inkFeathers["current"] = amount;
        soul["inkFeathers"] = inkFeathers;
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());
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

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser trade test", TimeSpan.FromSeconds(120));

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
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                parts.AddRange(dossier.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
                parts.AddRange(dossier.Metrics.Select(static metric => metric.Label));
                parts.AddRange(dossier.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectCardText(card, parts);
                foreach (var section in dossier.Sections)
                    CollectSectionText(section, parts);
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

    private static void CollectSectionText(UiEntityDossierSection section, List<string> parts)
    {
        parts.Add(section.Title);
        parts.Add(section.Summary);
        parts.AddRange(section.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(section.Metrics.Select(static metric => metric.Label));
        parts.AddRange(section.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(section.List);
        foreach (var card in section.Cards)
            CollectCardText(card, parts);
        foreach (var block in section.Blocks)
            CollectBlockText(block, parts);
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        parts.AddRange(card.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Metrics.Select(static metric => metric.Label));
        parts.AddRange(card.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);
        foreach (var child in card.Nested)
            CollectCardText(child, parts);
        foreach (var child in card.Cards)
            CollectCardText(child, parts);
    }

    private static string CollectResultText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks) + "\n" +
        string.Join("\n", result.Notifications.Select(notification => $"{notification.Title}\n{notification.Message}"));

    private static IEnumerable<UiEntityDossierBlock> EnumerateEntityDossiers(UiBlock block)
    {
        if (block is UiEntityDossierBlock dossier)
        {
            yield return dossier;
            foreach (var section in dossier.Sections)
            foreach (var child in section.Blocks)
            foreach (var nested in EnumerateEntityDossiers(child))
                yield return nested;
            yield break;
        }

        if (block is not UiPanelBlock panel)
            yield break;

        foreach (var child in panel.Blocks)
        foreach (var nested in EnumerateEntityDossiers(child))
            yield return nested;
    }

    private static IEnumerable<UiTableBlock> EnumerateTables(UiBlock block)
    {
        if (block is UiTableBlock table)
        {
            yield return table;
            yield break;
        }

        if (block is UiEntityDossierBlock dossier)
        {
            foreach (var section in dossier.Sections)
            foreach (var child in section.Blocks)
            foreach (var nested in EnumerateTables(child))
                yield return nested;
            yield break;
        }

        if (block is not UiPanelBlock panel)
            yield break;

        foreach (var child in panel.Blocks)
        foreach (var nested in EnumerateTables(child))
            yield return nested;
    }

    private static void AssertPlayerFacingFailureCopy(ExplorerCommandResult result)
    {
        var text = CollectResultText(result);

        Assert.Contains("Ошибка записи", text, StringComparison.Ordinal);
        Assert.Contains("состояние восстановлено", text, StringComparison.Ordinal);
        AssertNoRawTradeDiagnosticText(text);
    }

    private static void AssertNoRawTradeDiagnosticText(string text)
    {
        Assert.DoesNotContain("Browser-write", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_turn", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contract", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestId=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slotId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cleanup", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
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
