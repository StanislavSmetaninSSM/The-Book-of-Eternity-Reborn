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

public sealed class BrowserStorageTransportParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;

    public BrowserStorageTransportParityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-storage-transport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        var promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: new BrowserMortalWorldWriteService(
                _fs,
                coordinator,
                new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance),
                TimeProvider.System),
            afterlifeWriteService: new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator));
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, promptSessions);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task ExecuteAsync_StorageMove_ReturnsDirectionStorageAndItemChoicesWithPlayerFacingText()
    {
        await SeedStorageTransportStateAsync();

        var result = await ExecuteAsync("/storage_move");

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        AssertSelection(result, "storage_move_direction", ("deposit", "Положить"), ("retrieve", "Забрать"));
        AssertSelection(result, "storage_key", ("storage_1", "Кедровый сундук"));
        AssertSelection(result, "inventory_item_key", ("blade_1", "Стальной клинок"));
        AssertSelection(result, "storage_item_key", ("map_1", "Карта подвала"));
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_storage_move"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("хранилищ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Запертый шкаф", text, StringComparison.Ordinal);
        AssertNoRawStorageTransportDiagnosticText(text);
    }

    [Theory]
    [Trait("Category", "BrowserStorageTransportParity")]
    [InlineData("/storage_move")]
    [InlineData("/vehicle_move")]
    public async Task ExecuteAsync_StorageTransportMoveOutsideMortalWorld_ReturnsRealmBlockerWithoutPrompt(string command)
    {
        await SeedStorageTransportStateAsync();
        await SeedSoulRealmAsync("Chaos Sea");

        var result = await ExecuteAsync(command);

        Assert.Equal(CommandExecutionState.Blocked, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("смертном мире", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageTransportDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveDepositMovesExactJsonNodeFromInventoryToStorage()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var itemBefore = FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        Assert.Contains("перемещ", CollectResultAndPromptText(submit), StringComparison.OrdinalIgnoreCase);
        var inventoryItems = (await ReadInventoryAsync())["items"]!.AsArray();
        Assert.Null(FindItem(inventoryItems, "blade_1"));
        var storageContents = ReadStorageContents(await ReadLocationAsync(), "storage_1");
        var moved = FindItem(storageContents, "blade_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Equal(2, storageContents.Count);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveDepositAfterRealmSwitch_ReturnsRealmBlockerWithoutMutation()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var originalInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var originalLocation = await _fs.ReadFileAsync("game_state/world/current_location.json");
        await SeedSoulRealmAsync("Chaos Sea");

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Blocked, submit.State);
        var text = CollectResultAndPromptText(submit);
        Assert.Contains("смертном мире", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageTransportDiagnosticText(text);
        Assert.Equal(originalInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(originalLocation, await _fs.ReadFileAsync("game_state/world/current_location.json"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveDepositToNameOnlyAccessibleStorageAfterInaccessiblePredecessorMovesExactJsonNode()
    {
        await SeedNameOnlyStorageAfterInaccessiblePredecessorAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var itemBefore = FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Плетёная кладовая")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        Assert.Null(FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1"));
        var contents = ReadStorageContentsByName(await ReadLocationAsync(), "Плетёная кладовая");
        var moved = FindItem(contents, "blade_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Null(FindItem(ReadStorageContentsByName(await ReadLocationAsync(), "Запертый шкаф"), "blade_1"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveDepositCreatesMissingContentsArrayAndMovesExactJsonNode()
    {
        await SeedStorageTransportStateAsync();
        await RemoveStorageContentsAsync("storage_1");
        var prompt = await ExecuteAsync("/storage_move");
        var itemBefore = FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        Assert.Null(FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1"));
        var storageContents = ReadStorageContents(await ReadLocationAsync(), "storage_1");
        var moved = FindItem(storageContents, "blade_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Single(storageContents);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveRetrieveMovesExactJsonNodeFromStorageToInventory()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var itemBefore = FindItem(ReadStorageContents(await ReadLocationAsync(), "storage_1"), "map_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "retrieve"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("storage_item_key", OptionValue(prompt, "storage_item_key", "Карта подвала")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        var inventoryItems = (await ReadInventoryAsync())["items"]!.AsArray();
        var moved = FindItem(inventoryItems, "map_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Null(FindItem(ReadStorageContents(await ReadLocationAsync(), "storage_1"), "map_1"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveRetrieveFromNameOnlyAccessibleStorageAfterInaccessiblePredecessorMovesExactJsonNode()
    {
        await SeedNameOnlyStorageAfterInaccessiblePredecessorAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var itemBefore = FindItem(ReadStorageContentsByName(await ReadLocationAsync(), "Плетёная кладовая"), "mirror_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("storage_move_direction", "retrieve"),
            ("storage_key", OptionValue(prompt, "storage_key", "Плетёная кладовая")),
            ("storage_item_key", OptionValue(prompt, "storage_item_key", "Зеркальце")),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        var inventoryItems = (await ReadInventoryAsync())["items"]!.AsArray();
        var moved = FindItem(inventoryItems, "mirror_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Null(FindItem(ReadStorageContentsByName(await ReadLocationAsync(), "Плетёная кладовая"), "mirror_1"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task ExecuteAsync_VehicleMove_ReturnsDirectionVehicleAndItemChoicesWithPlayerFacingText()
    {
        await SeedStorageTransportStateAsync();

        var result = await ExecuteAsync("/vehicle_move");

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        AssertSelection(result, "vehicle_move_direction", ("deposit", "Положить"), ("retrieve", "Забрать"));
        AssertSelection(result, "vehicle_key", ("wagon_1", "Старый фургон"));
        AssertSelection(result, "inventory_item_key", ("blade_1", "Стальной клинок"));
        AssertSelection(result, "vehicle_item_key", ("rope_1", "Канат"));
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_vehicle_move"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("транспорт", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageTransportDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_VehicleMoveDepositMovesExactJsonNodeFromInventoryToVehicle()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/vehicle_move");
        var itemBefore = FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "deposit"),
            ("vehicle_key", OptionValue(prompt, "vehicle_key", "Старый фургон")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        Assert.Null(FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1"));
        var vehicleInventory = ReadVehicleInventory(await ReadVehiclesAsync(), "wagon_1");
        var moved = FindItem(vehicleInventory, "blade_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Equal(2, vehicleInventory.Count);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_VehicleMoveDepositAfterRealmSwitch_ReturnsRealmBlockerWithoutMutation()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/vehicle_move");
        var originalInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var originalVehicles = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
        await SeedSoulRealmAsync("Chaos Sea");

        var submit = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "deposit"),
            ("vehicle_key", OptionValue(prompt, "vehicle_key", "Старый фургон")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.Blocked, submit.State);
        var text = CollectResultAndPromptText(submit);
        Assert.Contains("смертном мире", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageTransportDiagnosticText(text);
        Assert.Equal(originalInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(originalVehicles, await _fs.ReadFileAsync("game_state/misc/vehicles.json"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_VehicleMoveDepositCreatesMissingInventoryArrayAndMovesExactJsonNode()
    {
        await SeedStorageTransportStateAsync();
        await RemoveVehicleInventoryAsync("wagon_1");
        var prompt = await ExecuteAsync("/vehicle_move");
        var itemBefore = FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "deposit"),
            ("vehicle_key", OptionValue(prompt, "vehicle_key", "Старый фургон")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        Assert.Null(FindItem((await ReadInventoryAsync())["items"]!.AsArray(), "blade_1"));
        var vehicleInventory = ReadVehicleInventory(await ReadVehiclesAsync(), "wagon_1");
        var moved = FindItem(vehicleInventory, "blade_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Single(vehicleInventory);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_VehicleMoveRetrieveMovesExactJsonNodeFromVehicleToInventory()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/vehicle_move");
        var itemBefore = FindItem(ReadVehicleInventory(await ReadVehiclesAsync(), "wagon_1"), "rope_1")!.DeepClone();

        var submit = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "retrieve"),
            ("vehicle_key", OptionValue(prompt, "vehicle_key", "Старый фургон")),
            ("vehicle_item_key", OptionValue(prompt, "vehicle_item_key", "Канат")),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.Completed, submit.State);
        var inventoryItems = (await ReadInventoryAsync())["items"]!.AsArray();
        var moved = FindItem(inventoryItems, "rope_1");
        Assert.NotNull(moved);
        Assert.True(JsonNode.DeepEquals(itemBefore, moved));
        Assert.Null(FindItem(ReadVehicleInventory(await ReadVehiclesAsync(), "wagon_1"), "rope_1"));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_StorageMoveRechecksStaleStorageItemAndMalformedStateWithoutMutation()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/storage_move");
        var originalInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var originalLocation = await _fs.ReadFileAsync("game_state/world/current_location.json");
        var storageKey = OptionValue(prompt, "storage_key", "Кедровый сундук");
        var itemKey = OptionValue(prompt, "inventory_item_key", "Стальной клинок");
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """{"locationStorages":[]}""");

        var staleStorage = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", storageKey),
            ("inventory_item_key", itemKey),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.RequiresInput, staleStorage.State);
        Assert.Contains("хранилищ", CollectResultAndPromptText(staleStorage), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(
            CommandExecutionState.Completed,
            (await CancelAsync(staleStorage)).State);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", originalLocation!);
        prompt = await ExecuteAsync("/storage_move");
        itemKey = OptionValue(prompt, "inventory_item_key", "Стальной клинок");
        await RemoveInventoryItemAsync("blade_1");

        var staleItem = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("inventory_item_key", itemKey),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.RequiresInput, staleItem.State);
        Assert.Contains("предмет", CollectResultAndPromptText(staleItem), StringComparison.OrdinalIgnoreCase);

        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", "{ malformed");
        var malformed = await SubmitAsync(
            prompt,
            ("storage_move_direction", "deposit"),
            ("storage_key", OptionValue(prompt, "storage_key", "Кедровый сундук")),
            ("inventory_item_key", itemKey),
            ("confirm_storage_move", true));

        Assert.Equal(CommandExecutionState.RequiresInput, malformed.State);
        AssertNoRawStorageTransportDiagnosticText(CollectResultAndPromptText(malformed));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task SubmitAsync_VehicleMoveRechecksStaleVehicleAndLocalWriteBlockerWithoutMutation()
    {
        await SeedStorageTransportStateAsync();
        var prompt = await ExecuteAsync("/vehicle_move");
        var originalInventory = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var originalVehicles = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
        var vehicleKey = OptionValue(prompt, "vehicle_key", "Старый фургон");
        var itemKey = OptionValue(prompt, "inventory_item_key", "Стальной клинок");
        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", """{"vehicles":[]}""");

        var staleVehicle = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "deposit"),
            ("vehicle_key", vehicleKey),
            ("inventory_item_key", itemKey),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.RequiresInput, staleVehicle.State);
        Assert.Contains("транспорт", CollectResultAndPromptText(staleVehicle), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(
            CommandExecutionState.Completed,
            (await CancelAsync(staleVehicle)).State);

        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", originalVehicles!);
        prompt = await ExecuteAsync("/vehicle_move");
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"turn":"pending"}""");

        var blocked = await SubmitAsync(
            prompt,
            ("vehicle_move_direction", "deposit"),
            ("vehicle_key", OptionValue(prompt, "vehicle_key", "Старый фургон")),
            ("inventory_item_key", OptionValue(prompt, "inventory_item_key", "Стальной клинок")),
            ("confirm_vehicle_move", true));

        Assert.Equal(CommandExecutionState.Blocked, blocked.State);
        Assert.Equal(originalInventory, await _fs.ReadFileAsync("game_state/inventory/items.json"));
        Assert.Equal(originalVehicles, await _fs.ReadFileAsync("game_state/misc/vehicles.json"));
        AssertNoRawStorageTransportDiagnosticText(CollectResultAndPromptText(blocked));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task ExecuteAsync_StorageMoveBlocksActiveGmTurnBeforeOpeningSession()
    {
        await SeedStorageTransportStateAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"turn":"pending"}""");

        var result = await ExecuteAsync("/storage_move");

        Assert.Equal(CommandExecutionState.Pending, result.State);
        Assert.Null(result.InteractiveSession);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("ход", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageTransportDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public async Task ExecuteAsync_StorageMoveDuplicateNamesUseUniquePlayerFacingLabels()
    {
        await SeedStorageTransportStateAsync();

        var result = await ExecuteAsync("/storage_move");

        var itemPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "inventory_item_key"));
        var duplicateNameOptions = itemPrompt.Options
            .Where(option => option.Label.Contains("Серебряная монета", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, duplicateNameOptions.Length);
        Assert.Equal(2, duplicateNameOptions.Select(static option => option.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.All(duplicateNameOptions, option =>
        {
            Assert.Contains("вариант", option.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("coin_", option.Label, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("coin_", option.Value, StringComparison.OrdinalIgnoreCase);
        });
        AssertNoRawStorageTransportDiagnosticText(CollectResultAndPromptText(result));
    }

    [Fact]
    [Trait("Category", "BrowserStorageTransportParity")]
    public void BrowserCommandCoverage_Issue814StorageTransportMoveCommandsAreCoveredWithoutParentFollowUp()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[] { "storage_item_move", "vehicle_item_move" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#814", command.FollowUpIssue, StringComparison.Ordinal);
        }

        var storageAccess = Assert.Single(coverage.Commands, item => item.Id == "storage_access");
        Assert.DoesNotContain("#814", storageAccess.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", storageAccess.FollowUpIssue, StringComparison.Ordinal);
        var transport = Assert.Single(coverage.Commands, item => item.Id == "transport");
        Assert.DoesNotContain("#814", transport.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", transport.FollowUpIssue, StringComparison.Ordinal);
    }

    private async Task<ExplorerCommandResult> ExecuteAsync(string command) =>
        await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-storage-transport-test",
            OwnerLabel: "Browser storage transport test"));

    private async Task<ExplorerCommandResult> SubmitAsync(ExplorerCommandResult prompt, params (string Key, object Value)[] values) =>
        await _commandService.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            AssertPromptSession(prompt).SessionId,
            Answers(values),
            OwnerId: "browser-storage-transport-test"));

    private async Task<ExplorerCommandResult> CancelAsync(ExplorerCommandResult prompt) =>
        await _commandService.CancelPromptSessionAsync(new ExplorerPromptSessionCancelRequest(
            AssertPromptSession(prompt).SessionId,
            OwnerId: "browser-storage-transport-test"));

    private static UiPromptSession AssertPromptSession(ExplorerCommandResult result) =>
        Assert.IsType<UiPromptSession>(result.InteractiveSession);

    private static void AssertSelection(
        ExplorerCommandResult result,
        string promptId,
        params (string ValueContains, string LabelContains)[] expectedOptions)
    {
        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == promptId));
        foreach (var (valueContains, labelContains) in expectedOptions)
        {
            Assert.Contains(prompt.Options, option =>
                option.Value.Contains(valueContains, StringComparison.OrdinalIgnoreCase) &&
                option.Label.Contains(labelContains, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string OptionValue(ExplorerCommandResult result, string promptId, string labelContains)
    {
        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == promptId));
        return Assert.Single(prompt.Options, option => option.Label.Contains(labelContains, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private async Task SeedStorageTransportStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "blade_1",
              "name": "Стальной клинок",
              "type": "weapon",
              "count": 1,
              "lore": {
                "inscription": "не терять"
              }
            },
            {
              "existedId": "herb_stack",
              "name": "Лунная трава",
              "type": "material",
              "count": 2
            },
            {
              "existedId": "coin_1",
              "name": "Серебряная монета",
              "count": 1
            },
            {
              "existedId": "coin_2",
              "name": "Серебряная монета",
              "count": 1
            }
          ],
          "equipment": {}
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_1",
          "name": "Двор караван-сарая",
          "locationStorages": [
            {
              "storageId": "storage_1",
              "name": "Кедровый сундук",
              "hasFullAccess": true,
              "capacity": 10,
              "contents": [
                {
                  "existedId": "map_1",
                  "name": "Карта подвала",
                  "count": 1,
                  "notes": {
                    "folded": true
                  }
                }
              ]
            },
            {
              "storageId": "storage_locked",
              "name": "Запертый шкаф",
              "hasFullAccess": false,
              "contents": [
                {
                  "existedId": "locked_1",
                  "name": "Чужая шкатулка",
                  "count": 1
                }
              ]
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", """
        {
          "vehicles": [
            {
              "vehicleId": "wagon_1",
              "name": "Старый фургон",
              "availability": "Active",
              "inventory": [
                {
                  "existedId": "rope_1",
                  "name": "Канат",
                  "quantity": 1,
                  "tags": [
                    "sturdy"
                  ]
                }
              ]
            }
          ]
        }
        """);
    }

    private async Task SeedSoulRealmAsync(string realm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "{{realm}}",
          "currentIncarnation": 1
        }
        """);
    }

    private async Task SeedNameOnlyStorageAfterInaccessiblePredecessorAsync()
    {
        await SeedStorageTransportStateAsync();
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_name_only",
          "name": "Двор караван-сарая",
          "locationStorages": [
            {
              "name": "Запертый шкаф",
              "hasFullAccess": false,
              "contents": [
                {
                  "existedId": "locked_1",
                  "name": "Чужая шкатулка",
                  "count": 1
                }
              ]
            },
            {
              "name": "Плетёная кладовая",
              "hasFullAccess": true,
              "contents": [
                {
                  "existedId": "mirror_1",
                  "name": "Зеркальце",
                  "count": 1,
                  "notes": {
                    "silvered": true
                  }
                }
              ]
            }
          ]
        }
        """);
    }

    private async Task<JsonObject> ReadInventoryAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();

    private async Task<JsonObject> ReadLocationAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync("game_state/world/current_location.json"))!)!.AsObject();

    private async Task<JsonObject> ReadVehiclesAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/vehicles.json"))!)!.AsObject();

    private async Task RemoveInventoryItemAsync(string existedId)
    {
        var inventory = await ReadInventoryAsync();
        var items = inventory["items"]!.AsArray();
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(ReadItemId(items[i]), existedId, StringComparison.OrdinalIgnoreCase))
            {
                items.RemoveAt(i);
                break;
            }
        }

        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task RemoveStorageContentsAsync(string storageId)
    {
        var location = await ReadLocationAsync();
        var storage = Assert.Single(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            string.Equals(ReadString(storage, "storageId"), storageId, StringComparison.OrdinalIgnoreCase));
        Assert.True(storage.Remove("contents"));
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", location.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task RemoveVehicleInventoryAsync(string vehicleId)
    {
        var vehiclesRoot = await ReadVehiclesAsync();
        var vehicle = Assert.Single(vehiclesRoot["vehicles"]!.AsArray().OfType<JsonObject>(), vehicle =>
            string.Equals(ReadString(vehicle, "vehicleId"), vehicleId, StringComparison.OrdinalIgnoreCase));
        Assert.True(vehicle.Remove("inventory"));
        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", vehiclesRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonArray ReadStorageContents(JsonObject location, string storageId)
    {
        var storage = Assert.Single(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            string.Equals(ReadString(storage, "storageId"), storageId, StringComparison.OrdinalIgnoreCase));
        return storage["contents"]!.AsArray();
    }

    private static JsonArray ReadStorageContentsByName(JsonObject location, string name)
    {
        var storage = Assert.Single(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            string.Equals(ReadString(storage, "name"), name, StringComparison.OrdinalIgnoreCase));
        return storage["contents"]!.AsArray();
    }

    private static JsonArray ReadVehicleInventory(JsonObject vehiclesRoot, string vehicleId)
    {
        var vehicle = Assert.Single(vehiclesRoot["vehicles"]!.AsArray().OfType<JsonObject>(), vehicle =>
            string.Equals(ReadString(vehicle, "vehicleId"), vehicleId, StringComparison.OrdinalIgnoreCase));
        return vehicle["inventory"]!.AsArray();
    }

    private static JsonNode? FindItem(JsonArray items, string existedId) =>
        items.FirstOrDefault(item => string.Equals(ReadItemId(item), existedId, StringComparison.OrdinalIgnoreCase));

    private static string ReadItemId(JsonNode? item) =>
        item is JsonObject obj
            ? FirstNonEmpty(ReadString(obj, "existedId"), ReadString(obj, "itemId"), ReadString(obj, "id"))
            : string.Empty;

    private static string ReadString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return string.Empty;
        if (value.TryGetValue<string>(out var text))
            return text ?? string.Empty;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object Value)[] values)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            result[key] = value switch
            {
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                bool flag => JsonValue.Create(flag),
                _ => JsonValue.Create(value.ToString())
            };
        }

        return result;
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

    private static void AssertNoRawStorageTransportDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protocol", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file path", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", text, StringComparison.OrdinalIgnoreCase);
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
