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

public sealed class BrowserInventoryManagementTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _commandService;
    private readonly BrowserMortalWorldWriteService _mortalWriteService;

    public BrowserInventoryManagementTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-inventory-management-" + Guid.NewGuid().ToString("N"));
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
        var promptSessions = new ExplorerWebPromptSessionService(
            _fs,
            _stateManager,
            lockService: lockService,
            mortalWorldWriteService: _mortalWriteService,
            afterlifeWriteService: new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator));
        _commandService = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation, promptSessions);
    }

    [Fact]
    public async Task InventoryEquipmentAuthority_RequiresCanonicalSlotAndExactRelicIdentity()
    {
        Assert.Equal("MainHand", InventoryEquipmentService.ResolveEquipSlot("mainHand", "weapon"));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("", "weapon"));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("рука", ""));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("Основная рука", ""));

        var prose = CreateCanonicalInventoryItem(
            "item_prose_relic",
            "Soul relic named in prose",
            "soul_relic",
            "Common",
            1,
            "MainHand",
            item => item["group"] = "реликвия души");
        var exact = CreateCanonicalInventoryItem(
            "item_exact_relic",
            "Exact relic",
            "tool",
            "Common",
            1,
            "MainHand",
            item => item["relicId"] = "relic_exact_001");
        await WriteCanonicalInventoryAsync([prose, exact], new JsonObject());

        var context = await InventoryEquipmentService.ReadContextAsync(_fs);
        Assert.NotNull(context);
        var proseRelic = Assert.Single(context!.Items, item => item.Identity == "item_prose_relic");
        var exactRelic = Assert.Single(context.Items, item => item.Identity == "item_exact_relic");

        Assert.False(proseRelic.IsSoulRelic);
        Assert.True(proseRelic.IsEquippable);
        Assert.True(exactRelic.IsSoulRelic);
        Assert.False(exactRelic.IsEquippable);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryEquipmentAuthority_SameNameItemsUseExactIdentityForStateAndActions()
    {
        await SeedInventoryAsync();
        var equipped = CreateCanonicalInventoryItem(
            "itm_twin_blade_a",
            "Парный клинок",
            "weapon",
            "Common",
            1,
            "MainHand");
        var backpack = CreateCanonicalInventoryItem(
            "itm_twin_blade_b",
            "Парный клинок",
            "weapon",
            "Common",
            1,
            "MainHand");
        await WriteCanonicalInventoryAsync(
            new[] { equipped, backpack },
            new JsonObject { ["MainHand"] = "itm_twin_blade_a" });

        var context = Assert.IsType<InventoryEquipmentContext>(
            await InventoryEquipmentService.ReadContextAsync(_fs));
        Assert.Equal("MainHand", Assert.Single(
            context.Items,
            static item => item.Identity == "itm_twin_blade_a").EquippedSlot);
        Assert.Empty(Assert.Single(
            context.Items,
            static item => item.Identity == "itm_twin_blade_b").EquippedSlot);
        Assert.Equal(
            "itm_twin_blade_a",
            Assert.Single(context.Equipped).ItemIdentity);
        Assert.Null(InventoryEquipmentService.FindItem(context.Items, "ITM_TWIN_BLADE_A"));
        Assert.Null(InventoryEquipmentService.FindItem(context.Items, "Парный клинок"));

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));

        Assert.Contains(result.Actions, static action =>
            action.Command == "/снять MainHand" &&
            action.Payload?["itemIdentity"]?.GetValue<string>() == "itm_twin_blade_a");
        Assert.Contains(result.Actions, static action =>
            action.Command == "/экипировать itm_twin_blade_b");
        Assert.DoesNotContain(result.Actions, static action =>
            action.Command == "/экипировать itm_twin_blade_a");
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ExplicitlyNonCarriedItemRemainsVisibleAtLocationButLocalActionsFailClosed()
    {
        static void ConfigureLocationItem(JsonObject item)
        {
            item["isCarried"] = false;
            item["currentLocationName"] = "Алтарь у старой дороги";
        }

        var selected = CreateCanonicalInventoryItem(
            "itm_location_blade",
            "Клинок на алтаре",
            "weapon",
            "Common",
            3,
            "MainHand",
            ConfigureLocationItem);
        var companion = CreateCanonicalInventoryItem(
            "itm_location_blade_companion",
            "Клинок на алтаре",
            "weapon",
            "Common",
            2,
            "MainHand",
            ConfigureLocationItem);
        await WriteCanonicalInventoryAsync([selected, companion], new JsonObject());
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var equipmentContext = Assert.IsType<InventoryEquipmentContext>(
            await InventoryEquipmentService.ReadContextAsync(_fs));
        var locationItem = Assert.Single(
            equipmentContext.Items,
            static item => item.Identity == "itm_location_blade");
        var overview = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var overviewText = CollectResultAndPromptText(overview);

        Assert.False(locationItem.IsEquippable);
        Assert.Contains("В текущей локации", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Алтарь у старой дороги", overviewText, StringComparison.Ordinal);
        Assert.DoesNotContain(overview.Actions, static action =>
            action.Command == "/экипировать itm_location_blade");

        foreach (var command in new[] { "/inventory_drop", "/inventory_split", "/inventory_merge" })
        {
            var prompt = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
                command,
                OwnerId: "browser-inventory-test",
                OwnerLabel: "Browser inventory test"));
            Assert.DoesNotContain(
                prompt.Prompts.OfType<UiSelectionPrompt>().SelectMany(static selection => selection.Options),
                static option => option.Value == "itm_location_blade");
        }

        var equip = await InventoryEquipmentService.ValidateEquipAsync(
            _fs,
            "itm_location_blade",
            "MainHand");
        var drop = await InventoryManagementService.ValidateDropAsync(_fs, "itm_location_blade");
        var split = await InventoryManagementService.ValidateSplitAsync(_fs, "itm_location_blade", 1);
        var merge = await InventoryManagementService.ValidateMergeAsync(_fs, "itm_location_blade");
        var write = await InventoryManagementService.DropAsync(_fs, "itm_location_blade");

        Assert.False(equip.Success);
        Assert.False(drop.Success);
        Assert.False(split.Success);
        Assert.False(merge.Success);
        Assert.False(write.Success);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryDetailPreservesCompleteDisassemblyMaterialSemantics()
    {
        var item = CreateCanonicalInventoryItem(
            "itm_disassembly_semantics",
            "Разборный походный фонарь",
            "tool",
            "Common",
            1,
            configureRaw: raw =>
            {
                raw["disassembleTo"] = new JsonArray(
                    new JsonObject
                    {
                        ["materialName"] = "Латунная пластина",
                        ["quantity"] = 2,
                        ["weight"] = 0.25,
                        ["volume"] = 0.4,
                        ["price"] = 17,
                        ["description"] = "Тёплая латунь с клеймом мастера."
                    });
                raw["materialization"]!["sections"]!["craftingAndDisassembly"] = new JsonObject
                {
                    ["state"] = "populated",
                    ["reason"] = null
                };
            });
        AssertCanonicalItem(item);
        await WriteCanonicalInventoryAsync([item], new JsonObject());

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв предмет itm_disassembly_semantics",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var text = CollectResultAndPromptText(result);

        Assert.Contains("Латунная пластина", text, StringComparison.Ordinal);
        Assert.Contains("0.25", text, StringComparison.Ordinal);
        Assert.Contains("0.4", text, StringComparison.Ordinal);
        Assert.Contains("17", text, StringComparison.Ordinal);
        Assert.Contains("Тёплая латунь с клеймом мастера", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task MalformedEquippedItemsHidesContextsAndLocalMutationsFailClosed()
    {
        var item = CreateCanonicalInventoryItem(
            "itm_malformed_equipment_guard",
            "Клинок строгой проверки",
            "weapon",
            "Common",
            1,
            "MainHand");
        await WriteCanonicalInventoryAsync([item], new JsonObject());
        var inventory = await ReadInventoryAsync();
        inventory["equippedItems"] = new JsonObject
        {
            ["MainHand"] = new JsonObject
            {
                ["itemId"] = "itm_malformed_equipment_guard"
            }
        };
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var before = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var equipmentContext = await InventoryEquipmentService.ReadContextAsync(_fs);
        var managementContext = await InventoryManagementService.ReadContextAsync(_fs);
        var equip = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_malformed_equipment_guard",
            "MainHand");
        var drop = await InventoryManagementService.DropAsync(
            _fs,
            "itm_malformed_equipment_guard");

        Assert.Null(equipmentContext);
        Assert.Null(managementContext);
        Assert.False(equip.Success);
        Assert.False(drop.Success);
        Assert.Equal(before, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task MalformedEquipmentMapDoesNotProjectValidLookingSiblingSlot()
    {
        var item = CreateCanonicalInventoryItem(
            "itm_atomic_equipment_browser",
            "Клинок атомарной экипировки",
            "weapon",
            "Common",
            1,
            "MainHand");
        await WriteCanonicalInventoryAsync(
            [item],
            new JsonObject
            {
                ["MainHand"] = "itm_atomic_equipment_browser",
                ["OffHand"] = null,
                ["UnknownSlot"] = null
            });

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var text = CollectResultAndPromptText(result);

        Assert.Contains("Клинок атомарной экипировки", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Экипировка", text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Actions, static action =>
            action.Command.StartsWith("/снять", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task MissingOptionalEquipmentMapStartsEmptyAndIsCreatedByFirstEquip()
    {
        var item = CreateCanonicalInventoryItem(
            "itm_missing_equipment_map",
            "Клинок без созданной карты экипировки",
            "weapon",
            "Common",
            1,
            "MainHand");
        await WriteCanonicalInventoryAsync([item], new JsonObject());
        var inventory = await ReadInventoryAsync();
        inventory.Remove("equippedItems");
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var context = await InventoryEquipmentService.ReadContextAsync(_fs);
        var equip = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_missing_equipment_map",
            "MainHand");

        Assert.NotNull(context);
        Assert.Empty(context!.Equipped);
        Assert.True(equip.Success, equip.Message);
        var after = await ReadInventoryAsync();
        Assert.Equal(
            "itm_missing_equipment_map",
            after["equippedItems"]!["MainHand"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryEquipmentAuthority_CurrentSchemaEquippedItemsSupportsCanonicalEquipAndUnequip()
    {
        var blade = CreateCanonicalInventoryItem(
            "itm_current_schema_blade",
            "Клинок текущей схемы",
            "weapon",
            "Common",
            1,
            "MainHand");
        await WriteCanonicalInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = null });

        var equip = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_current_schema_blade",
            "mainHand");

        Assert.True(equip.Success, equip.Message);
        Assert.Equal("MainHand", equip.SlotKey);
        var equippedInventory = await ReadInventoryAsync();
        Assert.Null(equippedInventory["equipment"]);
        Assert.Equal(
            "itm_current_schema_blade",
            equippedInventory["equippedItems"]!["MainHand"]!.GetValue<string>());

        var unequip = await InventoryEquipmentService.UnequipAsync(_fs, "mainHand");

        Assert.True(unequip.Success, unequip.Message);
        Assert.Equal("MainHand", unequip.SlotKey);
        var unequippedInventory = await ReadInventoryAsync();
        Assert.Null(unequippedInventory["equipment"]);
        Assert.Null(unequippedInventory["equippedItems"]!["MainHand"]);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryEquipmentAuthority_ArraySlotsRemainVisibleSelectableAndWritable()
    {
        var blade = CreateCanonicalInventoryItem(
            "itm_versatile_blade",
            "Клинок переменного хвата",
            "weapon",
            "Common",
            1,
            "MainHand",
            item =>
            {
                item["equipmentSlot"] = new JsonArray("MainHand", "OffHand");
            });
        AssertCanonicalItem(blade);
        await WriteCanonicalInventoryAsync(
            new[] { blade },
            new JsonObject { ["MainHand"] = null, ["OffHand"] = null });

        var overview = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var overviewText = CollectResultAndPromptText(overview);
        Assert.Contains("Основная рука", overviewText, StringComparison.Ordinal);
        Assert.Contains("Вторая рука", overviewText, StringComparison.Ordinal);
        Assert.Contains(overview.Actions, static action => action.Command == "/экипировать itm_versatile_blade");

        var promptResult = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать itm_versatile_blade",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        Assert.Equal(CommandExecutionState.RequiresInput, promptResult.State);
        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(
            promptResult.Prompts,
            static prompt => prompt.Id == "equipment_slot"));
        Assert.Equal(new[] { "MainHand", "OffHand" }, slotPrompt.Options.Select(static option => option.Value));

        var write = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_versatile_blade",
            "OffHand");

        Assert.True(write.Success, write.Message);
        var inventory = await ReadInventoryAsync();
        Assert.Equal("itm_versatile_blade", inventory["equippedItems"]!["OffHand"]!.GetValue<string>());
        Assert.Null(inventory["equippedItems"]!["MainHand"]);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryEquipmentAuthority_TwoHandedArrayOccupiesAndClearsBothHands()
    {
        var greatAxe = CreateCanonicalInventoryItem(
            "itm_two_handed_axe",
            "Секира двух рук",
            "weapon",
            "Common",
            1,
            "MainHand",
            item =>
            {
                item["equipmentSlot"] = new JsonArray("MainHand", "OffHand");
                item["requiresTwoHands"] = true;
            });
        AssertCanonicalItem(greatAxe);
        await WriteCanonicalInventoryAsync(
            new[] { greatAxe },
            new JsonObject { ["MainHand"] = null, ["OffHand"] = null });

        var equip = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_two_handed_axe",
            "MainHand");

        Assert.True(equip.Success, equip.Message);
        var equipped = await ReadInventoryAsync();
        Assert.Equal("itm_two_handed_axe", equipped["equippedItems"]!["MainHand"]!.GetValue<string>());
        Assert.Equal("itm_two_handed_axe", equipped["equippedItems"]!["OffHand"]!.GetValue<string>());

        var unequip = await InventoryEquipmentService.UnequipAsync(_fs, "OffHand");

        Assert.True(unequip.Success, unequip.Message);
        var unequipped = await ReadInventoryAsync();
        Assert.Null(unequipped["equippedItems"]!["MainHand"]);
        Assert.Null(unequipped["equippedItems"]!["OffHand"]);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task InventoryEquipmentAuthority_AccessoryUsesOnlyFreeUniversalAccessorySlots()
    {
        var bandolier = CreateCanonicalInventoryItem(
            "itm_archive_bandolier",
            "Архивный бандольер",
            "accessory",
            "Common",
            1,
            "MainHand",
            item =>
            {
                item["equipmentSlot"] = null;
                item["accessoryForSlot"] = new JsonArray("Chest", "Back");
            });
        var occupiedAccessory = CreateCanonicalInventoryItem(
            "itm_unresolved_occupied_accessory",
            "Занятый оберег",
            "accessory",
            "Common",
            1,
            "Accessory1");
        AssertCanonicalItem(bandolier);
        AssertCanonicalItem(occupiedAccessory);
        await WriteCanonicalInventoryAsync(
            new[] { bandolier, occupiedAccessory },
            new JsonObject
            {
                ["Accessory1"] = "itm_unresolved_occupied_accessory",
                ["Accessory2"] = null,
                ["Accessory3"] = null,
                ["Accessory4"] = null
            });

        var overview = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/инв",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var overviewText = CollectResultAndPromptText(overview);
        Assert.Contains("Грудь", overviewText, StringComparison.Ordinal);
        Assert.Contains("Спина", overviewText, StringComparison.Ordinal);

        var promptResult = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/экипировать itm_archive_bandolier",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));

        Assert.Equal(CommandExecutionState.RequiresInput, promptResult.State);
        var slotPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(
            promptResult.Prompts,
            static prompt => prompt.Id == "equipment_slot"));
        Assert.Equal(
            new[] { "Accessory2", "Accessory3", "Accessory4" },
            slotPrompt.Options.Select(static option => option.Value));
        Assert.DoesNotContain(slotPrompt.Options, static option => option.Value is "Chest" or "Back");

        var equip = await InventoryEquipmentService.EquipAsync(
            _fs,
            "itm_archive_bandolier",
            "Accessory2");

        Assert.True(equip.Success, equip.Message);
        var inventory = await ReadInventoryAsync();
        Assert.Equal("itm_archive_bandolier", inventory["equippedItems"]!["Accessory2"]!.GetValue<string>());
        Assert.Equal("itm_unresolved_occupied_accessory", inventory["equippedItems"]!["Accessory1"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ExecuteAsync_InventoryDrop_ReturnsConfirmationPromptWithPlayerFacingText()
    {
        await SeedInventoryAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/inventory_drop blade_1",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.True(result.InteractiveSession!.RequiresLocalUiLock);
        var itemPrompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "item_identity"));
        Assert.Contains(itemPrompt.Options, option => option.Value == "blade_1" && option.Label.Contains("Стальной клинок", StringComparison.Ordinal));
        var confirmation = Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_inventory_drop"));
        Assert.False(confirmation.DefaultValue);

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Стальной клинок", text, StringComparison.Ordinal);
        Assert.Contains("экип", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawInventoryDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryDrop_RemovesItemAndClearsEquipment()
    {
        await SeedInventoryAsync();

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_drop blade_1",
            Answers(("item_identity", "blade_1"), ("confirm_inventory_drop", true)),
            Owner("browser-inventory-test"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var inventory = await ReadInventoryAsync();
        var items = inventory["items"]!.AsArray();
        Assert.DoesNotContain(items, item => item!["itemId"]!.GetValue<string>() == "blade_1");
        Assert.Null(inventory["equippedItems"]!["MainHand"]);
        var index = await ReadIdentityIndexAsync();
        var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            candidate => candidate["itemId"]!.GetValue<string>() == "blade_1");
        Assert.Equal("destroyed", entry["state"]!.GetValue<string>());
        Assert.Null(entry["currentCarrier"]);
        Assert.Equal("destroy", entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryDropTechnicalTransitionFailureIsPlayerSafe()
    {
        await SeedInventoryAsync();
        var index = await ReadIdentityIndexAsync();
        var entry = Assert.Single(
            index["entries"]!.AsArray().OfType<JsonObject>(),
            static candidate => candidate["itemId"]!.GetValue<string>() == "blade_1");
        entry["receiptId"] = "PRIVATE_BROWSER_FAILURE_RECEIPT";
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_drop blade_1",
            Answers(("item_identity", "blade_1"), ("confirm_inventory_drop", true)),
            Owner("browser-inventory-test"));
        var playerText = result.Title + "\n" + result.Message;

        Assert.False(result.Success);
        Assert.Contains("состоян", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blade_1", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_BROWSER_FAILURE_RECEIPT", playerText, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("индекс", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materialization", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryDropRejectsMissingConfirmationInvalidItemAndLocalWriteBlockWithoutMutation()
    {
        await SeedInventoryAsync();
        var original = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);

        var missingConfirmation = await _mortalWriteService.TryApplyAsync(
            "/inventory_drop blade_1",
            Answers(("item_identity", "blade_1")),
            Owner("browser-inventory-test"));
        Assert.False(missingConfirmation.Success);
        Assert.True(missingConfirmation.KeepSessionOpen);
        Assert.Equal(original, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));

        var invalidItem = await _mortalWriteService.TryApplyAsync(
            "/inventory_drop missing_blade",
            Answers(("item_identity", "missing_blade"), ("confirm_inventory_drop", true)),
            Owner("browser-inventory-test"));
        Assert.False(invalidItem.Success);
        Assert.True(invalidItem.KeepSessionOpen);
        Assert.Equal(original, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """{"turn":"pending"}""");
        var blocked = await _mortalWriteService.TryApplyAsync(
            "/inventory_drop blade_1",
            Answers(("item_identity", "blade_1"), ("confirm_inventory_drop", true)),
            Owner("browser-inventory-test"));
        Assert.False(blocked.Success);
        Assert.Equal(CommandExecutionState.Blocked, blocked.State);
        AssertNoRawInventoryDiagnosticText(blocked.Title + "\n" + blocked.Message);
        Assert.Equal(original, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ExecuteAsync_InventorySplit_ReturnsQuantityAndConfirmationPrompt()
    {
        await SeedInventoryAsync();

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/inventory_split herb_stack",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.NotNull(result.InteractiveSession);
        Assert.Contains(result.Prompts, prompt => prompt.Id == "item_identity");
        var quantity = Assert.IsType<UiTextInputPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "split_quantity"));
        var quantityPromptText = UiTestTextCollector.CollectPromptText(quantity) + "\n" + CollectResultAndPromptText(result);
        Assert.Contains("1", quantityPromptText, StringComparison.Ordinal);
        Assert.Contains("4", quantityPromptText, StringComparison.Ordinal);
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(result.Prompts, prompt => prompt.Id == "confirm_inventory_split"));

        var text = CollectResultAndPromptText(result);
        Assert.Contains("Лунная трава", text, StringComparison.Ordinal);
        Assert.Contains("5", text, StringComparison.Ordinal);
        AssertNoRawInventoryDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventorySplit_CreatesDerivedStackAndPreservesIdentityEvidence()
    {
        await SeedSingleStackAsync(5);
        var before = Assert.Single((await ReadInventoryAsync())["items"]!.AsArray())!.AsObject();
        var parentReceipt = before["materializationReceipt"]!.DeepClone();

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_split stack_1",
            Answers(("item_identity", "stack_1"), ("split_quantity", 2), ("confirm_inventory_split", true)),
            Owner("browser-inventory-test"));

        Assert.True(result.Success, result.Message);
        var items = (await ReadInventoryAsync())["items"]!.AsArray();
        Assert.Equal(2, items.Count);

        var original = Assert.Single(items.OfType<JsonObject>(), item => item["itemId"]!.GetValue<string>() == "stack_1");
        var split = Assert.Single(items.OfType<JsonObject>(), item => item["itemId"]!.GetValue<string>() != "stack_1");
        Assert.Equal(3, original["count"]!.GetValue<int>());
        Assert.Equal(2, split["count"]!.GetValue<int>());
        Assert.Equal("Лунная трава", split["name"]!.GetValue<string>());
        Assert.StartsWith("itm_", split["itemId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(parentReceipt, original["materializationReceipt"]));
        Assert.Equal("split_derived", split["materializationReceipt"]!["instanceKind"]!.GetValue<string>());
        Assert.Equal("stack_1", Assert.Single(split["materializationReceipt"]!["parentItemIds"]!.AsArray())!.GetValue<string>());

        var index = await ReadIdentityIndexAsync();
        Assert.Equal(2, index["entries"]!.AsArray().Count);
        var childEntry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["itemId"]!.GetValue<string>() == split["itemId"]!.GetValue<string>());
        Assert.Equal("split", childEntry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Theory]
    [Trait("Category", "BrowserInventoryManagement")]
    [InlineData(0)]
    [InlineData(5)]
    public async Task TryApplyAsync_InventorySplitRejectsOutOfBoundsQuantitiesWithoutMutation(int splitQuantity)
    {
        await SeedSingleStackAsync(5);
        var original = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_split stack_1",
            Answers(("item_identity", "stack_1"), ("split_quantity", splitQuantity), ("confirm_inventory_split", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.True(result.KeepSessionOpen);
        Assert.Equal(original, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventorySplitRejectsNameOnlySelectionWithoutMutation()
    {
        var first = CreateCanonicalInventoryItem(
            "itm_same_name_a",
            "Лунная трава",
            "material",
            "Common",
            5);
        var second = CreateCanonicalInventoryItem(
            "itm_same_name_b",
            "Лунная трава",
            "material",
            "Common",
            5);
        await WriteCanonicalInventoryAsync(new[] { first, second }, new JsonObject());
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_split Лунная трава",
            Answers(("item_identity", "Лунная трава"), ("split_quantity", 2), ("confirm_inventory_split", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.True(result.KeepSessionOpen);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ExecuteAsync_InventoryMergeReportsUnavailableWhenNoCompatibleStackExists()
    {
        await SeedSingleStackAsync(3);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            "/inventory_merge stack_1",
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Prompts);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("нет", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("стопк", text, StringComparison.OrdinalIgnoreCase);
        AssertNoRawInventoryDiagnosticText(text);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ValidateMergeAsync_QuantityOverflowReturnsFailure()
    {
        var selected = CreateCanonicalInventoryItem(
            "itm_overflow_selected",
            "Зёрна для переполнения",
            "material",
            "Common",
            int.MaxValue);
        var contributor = CreateCanonicalInventoryItem(
            "itm_overflow_contributor",
            "Зёрна для переполнения",
            "material",
            "Common",
            1);
        await WriteCanonicalInventoryAsync(
            new[] { selected, contributor },
            new JsonObject());

        var outcome = await InventoryManagementService.ValidateMergeAsync(
            _fs,
            "itm_overflow_selected");

        Assert.False(outcome.Success);
        Assert.Contains("слишком велико", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryMerge_SumsCompatibleStacksAndRemovesDuplicates()
    {
        await SeedInventoryAsync();

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge herb_stack",
            Answers(("item_identity", "herb_stack"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.True(result.Success, result.Message);
        var items = (await ReadInventoryAsync())["items"]!.AsArray();
        Assert.Contains(items, item => item!["itemId"]!.GetValue<string>() == "herb_stack");
        Assert.DoesNotContain(items, item => item!["itemId"]!.GetValue<string>() == "herb_stack_2");
        Assert.Contains(items, item => item!["itemId"]!.GetValue<string>() == "herb_stack_rare");

        var merged = Assert.Single(items.OfType<JsonObject>(), item => item["itemId"]!.GetValue<string>() == "herb_stack");
        Assert.Equal(7, merged["count"]!.GetValue<int>());
        var index = await ReadIdentityIndexAsync();
        var survivor = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["itemId"]!.GetValue<string>() == "herb_stack");
        var contributor = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["itemId"]!.GetValue<string>() == "herb_stack_2");
        Assert.Equal(2, survivor["originMaterializationIds"]!.AsArray().Count);
        Assert.Equal("merged", contributor["state"]!.GetValue<string>());
        Assert.Equal("herb_stack", contributor["mergedIntoItemId"]!.GetValue<string>());
    }

    [Theory]
    [Trait("Category", "BrowserInventoryManagement")]
    [InlineData("readable")]
    [InlineData("sentient")]
    [InlineData("bonded")]
    [InlineData("quest")]
    [InlineData("section_reason")]
    public async Task TryApplyAsync_InventoryMergeRejectsGovernedSemanticMismatchWithoutMutation(
        string mismatch)
    {
        var selected = CreateCanonicalInventoryItem(
            "itm_semantic_selected",
            "Лунная трава",
            "material",
            "Common",
            2);
        var incompatible = CreateCanonicalInventoryItem(
            "itm_semantic_incompatible",
            "Лунная трава",
            "material",
            "Common",
            3,
            configureRaw: item => ConfigureSemanticMismatch(item, mismatch));
        AssertCanonicalItem(selected);
        AssertCanonicalItem(incompatible);
        await WriteCanonicalInventoryAsync(new[] { selected, incompatible }, new JsonObject());
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge itm_semantic_selected",
            Answers(("item_identity", "itm_semantic_selected"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.True(result.KeepSessionOpen);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryMergeRejectsEquippedContributorWithoutMutation()
    {
        var selected = CreateCanonicalInventoryItem(
            "itm_equipped_selected",
            "Парные клинки",
            "weapon",
            "Common",
            2,
            equipmentSlot: "MainHand");
        var contributor = CreateCanonicalInventoryItem(
            "itm_equipped_contributor",
            "Парные клинки",
            "weapon",
            "Common",
            3,
            equipmentSlot: "MainHand");
        await WriteCanonicalInventoryAsync(
            new[] { selected, contributor },
            new JsonObject { ["MainHand"] = "itm_equipped_contributor" });
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge itm_equipped_selected",
            Answers(("item_identity", "itm_equipped_selected"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryMergeRejectsEquippedSurvivorWithoutMutation()
    {
        var selected = CreateCanonicalInventoryItem(
            "itm_equipped_selected",
            "Парные клинки",
            "weapon",
            "Common",
            2,
            equipmentSlot: "MainHand");
        var contributor = CreateCanonicalInventoryItem(
            "itm_equipped_contributor",
            "Парные клинки",
            "weapon",
            "Common",
            3,
            equipmentSlot: "MainHand");
        await WriteCanonicalInventoryAsync(
            new[] { selected, contributor },
            new JsonObject { ["MainHand"] = "itm_equipped_selected" });
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge itm_equipped_selected",
            Answers(("item_identity", "itm_equipped_selected"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryMergeRejectsContainerContributorWithoutMutation()
    {
        static void ConfigureContainer(JsonObject item)
        {
            item["isContainer"] = true;
            item["capacity"] = 10;
            item["materialization"]!["sections"]!["container"] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }

        var selected = CreateCanonicalInventoryItem(
            "itm_container_selected",
            "Дорожная сумка",
            "container",
            "Common",
            2,
            configureRaw: ConfigureContainer);
        var contributor = CreateCanonicalInventoryItem(
            "itm_container_contributor",
            "Дорожная сумка",
            "container",
            "Common",
            3,
            configureRaw: ConfigureContainer);
        var child = CreateCanonicalInventoryItem(
            "itm_container_child",
            "Камень внутри сумки",
            "material",
            "Common",
            1);
        child["contentsPath"] = new JsonArray("itm_container_contributor");
        AssertCanonicalItem(selected);
        AssertCanonicalItem(contributor);
        AssertCanonicalItem(child);
        await WriteCanonicalInventoryAsync(
            new[] { selected, contributor, child },
            new JsonObject());
        var index = await ReadIdentityIndexAsync();
        var childEntry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["itemId"]!.GetValue<string>() == "itm_container_child");
        childEntry["currentCarrier"]!["containerPath"] = new JsonArray("itm_container_contributor");
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge itm_container_selected",
            Answers(("item_identity", "itm_container_selected"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task TryApplyAsync_InventoryMergeRejectsContainerSurvivorWithoutMutation()
    {
        static void ConfigureContainer(JsonObject item)
        {
            item["isContainer"] = true;
            item["capacity"] = 10;
            item["materialization"]!["sections"]!["container"] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }

        var selected = CreateCanonicalInventoryItem(
            "itm_container_selected",
            "Дорожная сумка",
            "container",
            "Common",
            2,
            configureRaw: ConfigureContainer);
        var contributor = CreateCanonicalInventoryItem(
            "itm_container_contributor",
            "Дорожная сумка",
            "container",
            "Common",
            3,
            configureRaw: ConfigureContainer);
        var child = CreateCanonicalInventoryItem(
            "itm_container_child",
            "Камень внутри сумки",
            "material",
            "Common",
            1);
        child["contentsPath"] = new JsonArray("itm_container_selected");
        AssertCanonicalItem(selected);
        AssertCanonicalItem(contributor);
        AssertCanonicalItem(child);
        await WriteCanonicalInventoryAsync(
            new[] { selected, contributor, child },
            new JsonObject());
        var index = await ReadIdentityIndexAsync();
        var childEntry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["itemId"]!.GetValue<string>() == "itm_container_child");
        childEntry["currentCarrier"]!["containerPath"] = new JsonArray("itm_container_selected");
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var beforeInventory = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);
        var beforeIndex = await _fs.ReadFileAsync(MortalItemIdentityState.StatePath);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge itm_container_selected",
            Answers(("item_identity", "itm_container_selected"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
        Assert.Equal(beforeIndex, await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public async Task ExecuteAsync_InventoryManagementPrompts_DefaultTextIsPlayerFacing()
    {
        await SeedInventoryAsync();

        var commands = new[] { "/inventory_drop blade_1", "/inventory_split herb_stack", "/inventory_merge herb_stack" };
        foreach (var command in commands)
        {
            var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
                command,
                OwnerId: "browser-inventory-test",
                OwnerLabel: "Browser inventory test"));

            Assert.NotEqual(CommandExecutionState.Failed, result.State);
            AssertNoRawInventoryDiagnosticText(CollectResultAndPromptText(result));
            AssertNoMortalItemAuthorityPayload(JsonSerializer.Serialize(result));
        }
    }

    [Theory]
    [Trait("Category", "BrowserInventoryManagement")]
    [InlineData("/inventory_drop")]
    [InlineData("/inventory_split")]
    [InlineData("/inventory_merge")]
    public async Task ExecuteAsync_InventoryManagementPrompts_IgnoreRawAndIdlessCandidates(
        string command)
    {
        var first = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_raw_local_prompt_one",
            materializationId: "mat_item_raw_local_prompt_one");
        first["id"] = "raw_local_prompt_one";
        first["name"] = "RAW_UNACCEPTED_LOCAL_ITEM_ONE";
        first["count"] = 4;
        var second = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_raw_local_prompt_two",
            materializationId: "mat_item_raw_local_prompt_two");
        second["id"] = "raw_local_prompt_two";
        second["name"] = "RAW_UNACCEPTED_LOCAL_ITEM_TWO";
        second["count"] = 4;
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(first, second),
                ["UpdateInventory"] = new JsonArray(first.DeepClone(), second.DeepClone()),
                ["equippedItems"] = new JsonObject
                {
                    ["MainHand"] = new JsonObject
                    {
                        ["name"] = "RAW_UNACCEPTED_LOCAL_ITEM_ONE"
                    }
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var before = await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath);

        var result = await _commandService.ExecuteAsync(new ExplorerWebCommandRequest(
            command,
            OwnerId: "browser-inventory-test",
            OwnerLabel: "Browser inventory test"));
        var projected = CollectResultAndPromptText(result) + "\n" + JsonSerializer.Serialize(result);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Prompts);
        Assert.DoesNotContain("RAW_UNACCEPTED_LOCAL_ITEM", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("creationRef", projected, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath));
    }

    [Fact]
    [Trait("Category", "BrowserInventoryManagement")]
    public void BrowserCommandCoverage_Issue806InventoryManagementCommandsAreCoveredExplicitly()
    {
        var coverage = BrowserCommandCoverageService.Build();

        foreach (var commandId in new[] { "inventory_drop", "inventory_split", "inventory_merge" })
        {
            var command = Assert.Single(coverage.Commands, item => item.Id == commandId);
            Assert.Equal("covered", command.AuditStatus);
            Assert.Equal(nameof(ExplorerCommandMigrationStatus.MutatingParity), command.BrowserStatus);
            Assert.Equal("guided-form", command.FormMode);
            Assert.Equal("player-default", command.Surface);
            Assert.DoesNotContain("#806", command.FollowUpIssue, StringComparison.Ordinal);
        }

        var inventory = Assert.Single(coverage.Commands, item => item.Id == "inventory");
        Assert.Equal("covered", inventory.AuditStatus);
        Assert.DoesNotContain("#806", inventory.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("stack-management", inventory.GapSummary, StringComparison.OrdinalIgnoreCase);

        var npcs = Assert.Single(coverage.Commands, item => item.Id == "npcs");
        Assert.DoesNotContain("#807", npcs.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("start-conversation", npcs.GapSummary, StringComparison.OrdinalIgnoreCase);
        var storageAccess = Assert.Single(coverage.Commands, item => item.Id == "storage_access");
        Assert.DoesNotContain("#814", storageAccess.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", storageAccess.FollowUpIssue, StringComparison.Ordinal);
        var archive = Assert.Single(coverage.Commands, item => item.Id == "afterlife_archive");
        Assert.DoesNotContain("#816", archive.FollowUpIssue, StringComparison.Ordinal);
        Assert.DoesNotContain("#817", archive.FollowUpIssue, StringComparison.Ordinal);
    }

    private async Task SeedInventoryAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var items = new[]
        {
            CreateCanonicalInventoryItem("blade_1", "Стальной клинок", "weapon", "Common", 1, "MainHand"),
            CreateCanonicalInventoryItem("herb_stack", "Лунная трава", "material", "Common", 5),
            CreateCanonicalInventoryItem("herb_stack_2", "Лунная трава", "material", "Common", 2),
            CreateCanonicalInventoryItem("herb_stack_rare", "Лунная трава", "material", "Rare", 4)
        };
        await WriteCanonicalInventoryAsync(
            items,
            new JsonObject { ["MainHand"] = "blade_1" });
    }

    private async Task SeedSingleStackAsync(int count)
    {
        await WriteCanonicalInventoryAsync(
            new[] { CreateCanonicalInventoryItem("stack_1", "Лунная трава", "material", "Common", count) },
            new JsonObject());
    }

    private async Task WriteCanonicalInventoryAsync(
        IReadOnlyList<JsonObject> items,
        JsonObject equippedItems)
    {
        await _fs.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(items.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equippedItems"] = equippedItems
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(items.ToArray())
                .ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreateCanonicalInventoryItem(
        string itemId,
        string name,
        string type,
        string quality,
        int count,
        string? equipmentSlot = null,
        Action<JsonObject>? configureRaw = null)
    {
        var item = MortalItemTestFixture.CreateRawRoot(
            creationRef: $"new_item_{itemId}",
            materializationId: $"mat_item_{itemId}");
        item["name"] = name;
        item["description"] = $"Тестовый предмет «{name}».";
        item["type"] = type;
        item["quality"] = quality;
        item["rarity"] = quality;
        item["count"] = count;
        item["equipmentSlot"] = equipmentSlot;
        if (equipmentSlot != null)
        {
            item["materialization"]!["sections"]!["equipment"] = new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        }
        configureRaw?.Invoke(item);
        var receipt = MortalItemIdentityState.CreateRootReceipt(item, itemId, acceptedTurn: 42);
        item["itemId"] = itemId;
        item["existedId"] = itemId;
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        return item;
    }

    private static void ConfigureSemanticMismatch(JsonObject item, string mismatch)
    {
        switch (mismatch)
        {
            case "readable":
                item["textContent"] = "Редкая запись о лунных травах.";
                SetPopulatedSection(item, "readableOrSentient");
                break;
            case "sentient":
                item["isSentient"] = true;
                SetPopulatedSection(item, "readableOrSentient");
                break;
            case "bonded":
                item["ownerBondLevelCurrent"] = 1;
                item["ownerBondLevelMax"] = 10;
                SetPopulatedSection(item, "bondsAndFateCards");
                break;
            case "quest":
                item["questLinks"] = new JsonArray(
                    new JsonObject
                    {
                        ["questId"] = "quest_lunar_herbs",
                        ["role"] = "required"
                    });
                SetPopulatedSection(item, "questRole");
                break;
            case "section_reason":
                item["materialization"]!["sections"]!["mechanics"]!["reason"] =
                    "Этот экземпляр намеренно не имеет самостоятельной механики по иной внутримировой причине.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch));
        }
    }

    private static void SetPopulatedSection(JsonObject item, string section) =>
        item["materialization"]!["sections"]![section] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };

    private static void AssertCanonicalItem(JsonObject item)
    {
        using var document = JsonDocument.Parse(item.ToJsonString());
        Assert.Empty(MortalItemMaterializationContract.Validate(
            document.RootElement,
            "browser_inventory_fixture",
            MortalItemMaterializationPhase.CanonicalPostSeal));
    }

    private async Task<JsonObject> ReadInventoryAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();

    private async Task<JsonObject> ReadIdentityIndexAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync(MortalItemIdentityState.StatePath))!)!.AsObject();

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

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser inventory test", TimeSpan.FromSeconds(120));

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

    private static void AssertNoRawInventoryDiagnosticText(string text)
    {
        Assert.DoesNotContain("game_state/", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".json", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protocol", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slotId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contract", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoMortalItemAuthorityPayload(string payload)
    {
        Assert.DoesNotContain("materializationReceipt", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"materialization\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("creationRef", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receiptId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"seal\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originMaterializationIds", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("parentItemIds", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentCarrier", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_identity_index.json", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mortal_item_materialization_repair", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
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
