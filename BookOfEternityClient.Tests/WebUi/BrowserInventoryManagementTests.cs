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
        Assert.Equal("mainHand", InventoryEquipmentService.ResolveEquipSlot("mainHand", "weapon"));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("", "weapon"));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("рука", ""));
        Assert.Null(InventoryEquipmentService.ResolveEquipSlot("Основная рука", ""));

        await _fs.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, """
        {
          "items": [
            {
              "itemId": "item_prose_relic",
              "name": "Soul relic named in prose",
              "type": "soul_relic",
              "group": "реликвия души",
              "equipmentSlot": "mainHand"
            },
            {
              "itemId": "item_exact_relic",
              "name": "Exact relic",
              "relicId": "relic_exact_001",
              "equipmentSlot": "mainHand"
            }
          ],
          "equipment": {}
        }
        """);

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
        Assert.DoesNotContain(items, item => item!["existedId"]!.GetValue<string>() == "blade_1");
        Assert.Null(inventory["equipment"]!["mainHand"]);
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

    [Theory]
    [Trait("Category", "BrowserInventoryManagement")]
    [InlineData("count")]
    [InlineData("quantity")]
    public async Task TryApplyAsync_InventorySplit_CreatesFreshStackAndPreservesCountField(string countField)
    {
        await SeedSingleStackAsync(countField, 5);

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_split stack_1",
            Answers(("item_identity", "stack_1"), ("split_quantity", 2), ("confirm_inventory_split", true)),
            Owner("browser-inventory-test"));

        Assert.True(result.Success, result.Message);
        var items = (await ReadInventoryAsync())["items"]!.AsArray();
        Assert.Equal(2, items.Count);

        var original = Assert.Single(items.OfType<JsonObject>(), item => item["existedId"]!.GetValue<string>() == "stack_1");
        var split = Assert.Single(items.OfType<JsonObject>(), item => item["existedId"]!.GetValue<string>() != "stack_1");
        Assert.Equal(3, original[countField]!.GetValue<int>());
        Assert.Equal(2, split[countField]!.GetValue<int>());
        Assert.Equal("Лунная трава", split["name"]!.GetValue<string>());
        Assert.DoesNotContain("stack_1", split["existedId"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "BrowserInventoryManagement")]
    [InlineData(0)]
    [InlineData(5)]
    public async Task TryApplyAsync_InventorySplitRejectsOutOfBoundsQuantitiesWithoutMutation(int splitQuantity)
    {
        await SeedSingleStackAsync("count", 5);
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
    public async Task ExecuteAsync_InventoryMergeReportsUnavailableWhenNoCompatibleStackExists()
    {
        await SeedSingleStackAsync("count", 3);

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
    public async Task TryApplyAsync_InventoryMerge_SumsCompatibleStacksAndRemovesDuplicates()
    {
        await SeedInventoryAsync();

        var result = await _mortalWriteService.TryApplyAsync(
            "/inventory_merge herb_stack",
            Answers(("item_identity", "herb_stack"), ("confirm_inventory_merge", true)),
            Owner("browser-inventory-test"));

        Assert.True(result.Success, result.Message);
        var items = (await ReadInventoryAsync())["items"]!.AsArray();
        Assert.Contains(items, item => item!["existedId"]!.GetValue<string>() == "herb_stack");
        Assert.DoesNotContain(items, item => item!["existedId"]!.GetValue<string>() == "herb_stack_2");
        Assert.Contains(items, item => item!["existedId"]!.GetValue<string>() == "herb_stack_rare");

        var merged = Assert.Single(items.OfType<JsonObject>(), item => item["existedId"]!.GetValue<string>() == "herb_stack");
        Assert.Equal(7, merged["count"]!.GetValue<int>());
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
        }
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

        await _fs.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, """
        {
          "items": [
            {
              "existedId": "blade_1",
              "name": "Стальной клинок",
              "type": "weapon",
              "equipmentSlot": "mainHand",
              "count": 1
            },
            {
              "existedId": "herb_stack",
              "name": "Лунная трава",
              "type": "material",
              "quality": "Common",
              "count": 5
            },
            {
              "existedId": "herb_stack_2",
              "name": "Лунная трава",
              "type": "material",
              "quality": "Common",
              "quantity": 2
            },
            {
              "existedId": "herb_stack_rare",
              "name": "Лунная трава",
              "type": "material",
              "quality": "Rare",
              "count": 4
            }
          ],
          "equipment": {
            "mainHand": "blade_1"
          }
        }
        """);
    }

    private async Task SeedSingleStackAsync(string countField, int count)
    {
        await _fs.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, $$"""
        {
          "items": [
            {
              "existedId": "stack_1",
              "name": "Лунная трава",
              "type": "material",
              "quality": "Common",
              "{{countField}}": {{count}}
            }
          ],
          "equipment": {}
        }
        """);
    }

    private async Task<JsonObject> ReadInventoryAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();

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
