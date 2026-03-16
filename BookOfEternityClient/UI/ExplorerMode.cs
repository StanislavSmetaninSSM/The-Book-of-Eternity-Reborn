using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

/// <summary>
/// Explorer mode: handles local /commands that read game state files
/// and display formatted data without sending to the GM.
/// Supports bilingual commands (Russian/English).
/// </summary>
public class ExplorerMode
{
    private readonly StateManager _stateManager;
    private readonly FileSystemManager _fs;
    private readonly LocalizationManager _loc;
    private readonly Services.ValidationService? _validator;
    private readonly Services.CharacteristicsService? _charService;
    private readonly Services.StoryService? _storyService;
    private readonly Services.ImageService? _imageService;
    private readonly Services.PendingTurnStateService? _pendingTurnState;
    private readonly Services.GuardianTradeService? _guardianTradeService;
    private readonly Services.NpcTradeService? _npcTradeService;
    private readonly Services.SystemModService? _systemModService;
    private readonly Services.WorldDirectiveService? _worldDirectiveService;

    // Set by interactive commands (equip/unequip) to signal an action to send to the GM
    private string? _pendingGmAction;
    // Set by Reveal Fate so Rewrite Fate becomes available
    private bool _diceRevealed;

    // Commands available in ALL realms
    private readonly Dictionary<string, Func<Task>> _universalCommands;
    // Commands ONLY available in Chaos Sea (afterlife)
    private readonly Dictionary<string, Func<Task>> _chaosSeaOnlyCommands;
    // Commands ONLY available in Mortal Life
    private readonly Dictionary<string, Func<Task>> _mortalOnlyCommands;
    // Commands available in both but behave differently
    private readonly HashSet<string> _allCommandNames;

    public ExplorerMode(StateManager stateManager, FileSystemManager fs, LocalizationManager loc,
        Services.ValidationService? validator = null, Services.CharacteristicsService? charService = null,
        Services.StoryService? storyService = null, Services.ImageService? imageService = null,
        Services.PendingTurnStateService? pendingTurnState = null,
        Services.GuardianTradeService? guardianTradeService = null,
        Services.NpcTradeService? npcTradeService = null,
        Services.SystemModService? systemModService = null,
        Services.WorldDirectiveService? worldDirectiveService = null)
    {
        _stateManager = stateManager;
        _validator = validator;
        _charService = charService;
        _storyService = storyService;
        _imageService = imageService;
        _pendingTurnState = pendingTurnState;
        _guardianTradeService = guardianTradeService;
        _npcTradeService = npcTradeService;
        _systemModService = systemModService;
        _worldDirectiveService = worldDirectiveService;
        _fs = fs;
        _loc = loc;

        // Universal commands — available in both realms
        _universalCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/help"] = ShowHelp,
            ["/помощь"] = ShowHelp,
            ["/soul"] = ShowSoulInfo,
            ["/душа"] = ShowSoulInfo,
            ["/soul_relics"] = ShowSoulRelics, // read-only in mortal, manageable hint in chaos sea
            ["/реликвии"] = ShowSoulRelics,
            ["/soul_quests"] = ShowSoulQuests, // visible in both (tasks from guardians)
            ["/квесты_души"] = ShowSoulQuests,
            ["/gm"] = ShowGmThoughts,
            ["/гм"] = ShowGmThoughts,
            ["/debug"] = ShowDebugInfo,
            ["/отладка"] = ShowDebugInfo,
            ["/codex"] = ShowLoreCodex,
            ["/кодекс"] = ShowLoreCodex,
            ["/achievements"] = ShowAchievements,
            ["/достижения"] = ShowAchievements,
            ["/chronicle"] = ShowChronicle,
            ["/хроника"] = ShowChronicle,
            ["/story"] = ShowStory,
            ["/рассказ"] = ShowStory,
            ["/история"] = ShowStory,
            ["/behavior"] = ShowBehaviorAssessment,
            ["/поведение"] = ShowBehaviorAssessment,
            ["/validate"] = ShowValidation,
            ["/валидация"] = ShowValidation,
            ["/lives"] = ShowLivesHistory,
            ["/жизни"] = ShowLivesHistory,
            ["/feathers"] = ShowInkFeathersMenu,
            ["/перья"] = ShowInkFeathersMenu,
            ["/mods"] = ShowSystemMods,
            ["/моды"] = ShowSystemMods,
            ["/world_setup"] = ShowWorldSetup,
            ["/настройка_мира"] = ShowWorldSetup,
            ["/world_rules"] = ShowWorldRules,
            ["/правила_мира"] = ShowWorldRules,
            ["/gallery"] = ShowGallery,
            ["/галерея"] = ShowGallery,
        };

        // Chaos Sea ONLY — blocked in mortal life
        _chaosSeaOnlyCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/guardians"] = ShowGuardians,
            ["/хранители"] = ShowGuardians,
            ["/abodes"] = ShowAbodesNavigation,
            ["/обители"] = ShowAbodesNavigation,
            ["/gacha"] = ShowGachaInfo,
            ["/гача"] = ShowGachaInfo,
        };

        // Mortal Life ONLY — blocked in Chaos Sea (no mortal world to explore there)
        _mortalOnlyCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/inv"] = ShowInventory,
            ["/inventory"] = ShowInventory,
            ["/инв"] = ShowInventory,
            ["/инвентарь"] = ShowInventory,
            ["/npc"] = ShowNPCs,
            ["/npcs"] = ShowNPCs,
            ["/characters"] = ShowNPCs,
            ["/нпс"] = ShowNPCs,
            ["/персонажи"] = ShowNPCs,
            ["/quests"] = ShowQuests,
            ["/квесты"] = ShowQuests,
            ["/map"] = ShowMap,
            ["/карта"] = ShowMap,
            ["/status"] = ShowDetailedStatus,
            ["/статус"] = ShowDetailedStatus,
            ["/where_am_i"] = ShowCurrentLocation,
            ["/где_я"] = ShowCurrentLocation,
            ["/factions"] = ShowFactions,
            ["/фракции"] = ShowFactions,
            ["/skills"] = ShowSkills,
            ["/навыки"] = ShowSkills,
            ["/stats"] = ShowPlayerStats,
            ["/статы"] = ShowPlayerStats,
            ["/характеристики"] = ShowPlayerStats,
            ["/distribute"] = ShowStatDistributionCommand,
            ["/распределить"] = ShowStatDistributionCommand,
            ["/companion_directive"] = SetCompanionDirective,
            ["/директива_компаньону"] = SetCompanionDirective,
            ["/faction_directive"] = SetFactionDirective,
            ["/директива_фракции"] = SetFactionDirective,
            ["/world_news"] = ShowWorldNews,
            ["/новости_мира"] = ShowWorldNews,
            ["/craft"] = ShowCraftMenu,
            ["/ремесло"] = ShowCraftMenu,
            ["/locations"] = ShowLocations,
            ["/локации"] = ShowLocations,
            ["/transport"] = ShowTransport,
            ["/транспорт"] = ShowTransport,
            ["/effects"] = ShowEffects,
            ["/эффекты"] = ShowEffects,
            ["/combat"] = ShowCombat,
            ["/бой"] = ShowCombat,
            ["/weather"] = ShowWeatherTime,
            ["/погода"] = ShowWeatherTime,
            ["/books"] = ShowItemTexts,
            ["/книги"] = ShowItemTexts,
            ["/читать"] = ShowItemTexts,
            ["/storage_access"] = ShowStorageAccess,
            ["/доступ_к_хранилищам"] = ShowStorageAccess,
            ["/interactions"] = ShowPlayerInteractions,
            ["/взаимодействия"] = ShowPlayerInteractions,
        };

        // Build set of all command names for IsCommand()
        _allCommandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in _universalCommands.Keys) _allCommandNames.Add(k);
        foreach (var k in _chaosSeaOnlyCommands.Keys) _allCommandNames.Add(k);
        foreach (var k in _mortalOnlyCommands.Keys) _allCommandNames.Add(k);
    }

    /// <summary>
    /// Try to process as a local command. Returns:
    /// - null: not a recognized command
    /// - "": command handled locally (no GM action needed)
    /// - non-empty string: action to send to the GM (e.g., equip/unequip requests)
    /// Enforces realm restrictions: some commands only work in Chaos Sea, others only in Mortal Life.
    /// </summary>
    public async Task<string?> TryProcessCommand(string input)
    {
        var cmd = input.Trim().Split(' ')[0].ToLower();
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;
        _pendingGmAction = null;

        // Universal commands — always available
        if (_universalCommands.TryGetValue(cmd, out var handler))
        {
            await SafeExecute(handler, cmd);
            return _pendingGmAction ?? "";
        }

        // Chaos Sea only commands
        if (_chaosSeaOnlyCommands.TryGetValue(cmd, out var chaosHandler))
        {
            if (isAfterlife)
            {
                await SafeExecute(chaosHandler, cmd);
                return _pendingGmAction ?? "";
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️ Эта команда доступна только в Море Хаоса (загробная жизнь).[/]");
                AnsiConsole.MarkupLine("[dim]В смертной жизни вы не можете взаимодействовать с хранителями.[/]");
                WaitForKey();
            }
            return "";
        }

        // Mortal Life only commands
        if (_mortalOnlyCommands.TryGetValue(cmd, out var mortalHandler))
        {
            if (!isAfterlife)
            {
                await SafeExecute(mortalHandler, cmd);
                return _pendingGmAction ?? "";
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️ Эта команда доступна только в смертной жизни.[/]");
                AnsiConsole.MarkupLine("[dim]В Море Хаоса у вас нет смертного инвентаря, карты и т.д.[/]");
                AnsiConsole.MarkupLine("[dim]Используйте /воплотиться чтобы войти в смертную жизнь.[/]");
                WaitForKey();
            }
            return "";
        }

        return null;
    }

    public bool IsCommand(string input)
    {
        return input.TrimStart().StartsWith('/');
    }

    private static readonly Dictionary<string, string> SlotLabels = new()
    {
        ["head"] = "🪖 Голова", ["body"] = "🛡️ Тело", ["hands"] = "🧤 Руки",
        ["feet"] = "👢 Ноги", ["mainHand"] = "⚔️ Основная рука", ["offHand"] = "🛡️ Вторая рука",
        ["neck"] = "📿 Шея", ["ring1"] = "💍 Кольцо 1", ["ring2"] = "💍 Кольцо 2"
    };

    // Maps item type keywords → equipment slot key
    private static readonly Dictionary<string, string> TypeToSlot = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = "mainHand", ["оружие"] = "mainHand", ["меч"] = "mainHand", ["посох"] = "mainHand",
        ["armor"] = "body", ["броня"] = "body",
        ["helmet"] = "head", ["шлем"] = "head",
        ["shield"] = "offHand", ["щит"] = "offHand",
        ["boots"] = "feet", ["сапоги"] = "feet",
        ["gloves"] = "hands", ["перчатки"] = "hands",
        ["ring"] = "ring1", ["кольцо"] = "ring1",
        ["necklace"] = "neck", ["ожерелье"] = "neck", ["amulet"] = "neck", ["амулет"] = "neck",
        ["accessory"] = "neck", ["аксессуар"] = "neck"
    };

    private async Task ShowInventory()
    {
        while (true)
        {
            // Re-read each iteration to reflect local equip/unequip changes
            var doc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/items.json");
            if (doc == null)
            {
                ShowEmptyPanel(_loc.T("inventory"), "Инвентарь пуст");
                return;
            }

            var inventoryItems = new List<(string Identity, string Name, string Type, JsonElement Data)>();
            var itemArray = GetPlayerInventoryItemsElement(doc.RootElement);
            if (itemArray.HasValue)
            {
                foreach (var item in itemArray.Value.EnumerateArray())
                {
                    var name = GetInventoryItemName(item);
                    var type = GetStr(item, "type", "");
                    var identity = GetInventoryItemIdentity(item);
                    inventoryItems.Add((identity, name, type, item));
                }
            }

            var equippedEntries = new List<(string SlotKey, string ItemIdentity, string ItemName, JsonElement? Data)>();
            if (doc.RootElement.TryGetProperty("equipment", out var equip) &&
                equip.ValueKind == JsonValueKind.Object)
            {
                foreach (var (key, _) in SlotLabels)
                {
                    if (!equip.TryGetProperty(key, out var slot) || slot.ValueKind == JsonValueKind.Null)
                        continue;

                    var referenceIdentity = GetEquipmentReferenceIdentity(slot);
                    var referenceName = GetEquipmentReferenceName(slot);
                    var matched = inventoryItems.FirstOrDefault(i => InventoryItemMatches(i.Data, referenceIdentity, referenceName));
                    var matchedData = matched.Data.ValueKind != JsonValueKind.Undefined ? matched.Data : (JsonElement?)null;
                    var itemName = matchedData.HasValue
                        ? matched.Name
                        : (!string.IsNullOrWhiteSpace(referenceName) ? referenceName :
                            (!string.IsNullOrWhiteSpace(referenceIdentity) ? referenceIdentity : "???"));
                    var itemIdentity = matchedData.HasValue ? matched.Identity : referenceIdentity;
                    equippedEntries.Add((key, itemIdentity, itemName, matchedData));
                }
            }

            // Auto-discard broken items if setting enabled
            if (_stateManager.Settings.AutoDiscardBrokenItems && inventoryItems.Count > 0)
            {
                var brokenItems = inventoryItems
                    .Where(i =>
                    {
                        if (i.Data.TryGetProperty("isBroken", out var b) && b.ValueKind == JsonValueKind.True) return true;
                        var dur = GetStr(i.Data, "durability", "");
                        return !string.IsNullOrEmpty(dur) && int.TryParse(dur.Replace("%", "").Trim(), out var dv) && dv == 0;
                    })
                    .Select(i => (i.Identity, i.Name)).ToList();
                if (brokenItems.Count > 0)
                {
                    foreach (var broken in brokenItems)
                        await DropItemLocal(broken.Identity, broken.Name);
                    AnsiConsole.MarkupLine($"[dim]Авто-выброс: {brokenItems.Count} сломанных предметов удалено[/]");
                    continue; // re-read inventory after auto-discard
                }
            }

            var itemResourcesDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_resources.json");
            var choices = new List<string>();
            if (equippedEntries.Count > 0)
            {
                foreach (var (slotKey, _, itemName, _) in equippedEntries)
                {
                    var slotLabel = SlotLabels.GetValueOrDefault(slotKey, slotKey);
                    choices.Add($"⚔ {slotLabel}: {itemName}");
                }
            }

            var inventoryChoiceEntries = new List<(string Label, string Identity)>();
            foreach (var (identity, name, type, data) in inventoryItems)
            {
                var qty = GetStr(data, "count", GetStr(data, "quantity", "1"));
                var qtyStr = qty != "1" ? $" x{qty}" : "";
                var typeStr = !string.IsNullOrEmpty(type) ? $" [{type}]" : "";

                // Status flags
                var flags = "";
                var durStr = GetStr(data, "durability", "");
                if (!string.IsNullOrEmpty(durStr) && int.TryParse(durStr.Replace("%", "").Trim(), out var durVal) && durVal == 0)
                    flags += " ⚠ СЛОМАН";
                var resourceEntry = FindInventorySidecarEntry(itemResourcesDoc, identity, name, "entries", "inventoryItemsResources");
                var resStr = GetPreferredStr(resourceEntry, data, "resource");
                var isSidecarEmpty = !string.IsNullOrEmpty(resStr) &&
                                     int.TryParse(resStr.Replace("%", "").Trim(), out var sidecarResVal) &&
                                     sidecarResVal == 0;
                if (isSidecarEmpty)
                    flags += " ⚠ ПУСТО";
                if (data.TryGetProperty("isBroken", out var brk2) && brk2.ValueKind == JsonValueKind.True)
                    flags += " ⚠ СЛОМАН";
                if ((data.TryGetProperty("isEmpty", out var emp2) && emp2.ValueKind == JsonValueKind.True) && !isSidecarEmpty)
                    flags += " ⚠ ПУСТО";

                inventoryChoiceEntries.Add(($"📦 {name}{qtyStr}{typeStr}{flags}", identity));
            }

            choices.AddRange(MakeUniqueChoiceLabels(inventoryChoiceEntries));

            if (choices.Count == 0)
            {
                ShowEmptyPanel(_loc.T("inventory"), "Инвентарь пуст");
                return;
            }

            // Resources/Money section — track how many info-only rows are prepended
            int infoPrefixCount = 0;
            var money = 0;
            var statusDoc = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
            if (statusDoc != null)
                money = GetInt(statusDoc.RootElement, "money", 0);
            if (money <= 0)
                money = GetInt(doc.RootElement, "money", 0);
            if (money > 0)
            {
                choices.Insert(0, $"💰 Деньги: {money}");
                infoPrefixCount++;
            }

            if (doc.RootElement.TryGetProperty("resources", out var resList) && resList.ValueKind == JsonValueKind.Object)
            {
                foreach (var rp in resList.EnumerateObject())
                {
                    if (rp.Name is "money" or "gold" or "coins") continue;
                    var rv = rp.Value.ValueKind == JsonValueKind.Number
                        ? rp.Value.GetRawText()
                        : (rp.Value.ValueKind == JsonValueKind.String ? rp.Value.GetString() ?? "" : "");
                    if (!string.IsNullOrEmpty(rv) && rv != "0")
                    {
                        choices.Insert(infoPrefixCount, $"💎 {rp.Name}: {rv}");
                        infoPrefixCount++;
                    }
                }
            }

            // Location storages link — interactive
            var accessibleStorages = new List<(string name, string storageId, int contCount)>();
            var locStorDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
            if (locStorDoc != null)
            {
                var locStorRoot = locStorDoc.RootElement;
                if (locStorRoot.TryGetProperty("locationStorages", out var lStorages) && lStorages.ValueKind == JsonValueKind.Array && lStorages.GetArrayLength() > 0)
                {
                    foreach (var st in lStorages.EnumerateArray())
                    {
                        var sName = GetStr(st, "name", "Хранилище");
                        var sId = GetStr(st, "storageId", "");
                        var hasAccess = st.TryGetProperty("hasFullAccess", out var ha) && ha.ValueKind == JsonValueKind.True;
                        var contCount = 0;
                        if (st.TryGetProperty("contents", out var cont) && cont.ValueKind == JsonValueKind.Array)
                            contCount = cont.GetArrayLength();
                        if (hasAccess)
                        {
                            choices.Add($"📦 {sName} ({contCount} пр.) → управление");
                            accessibleStorages.Add((sName, sId, contCount));
                        }
                        else
                        {
                            choices.Add($"📦 🔒 {sName} ({contCount} пр.)");
                        }
                    }
                }
            }

            choices.Add("← Назад");

            var weightInfo = "";
            if (doc.RootElement.TryGetProperty("totalWeight", out var tw))
            {
                var max = GetStr(doc.RootElement, "maxWeight", "?");
                var isOver = doc.RootElement.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
                weightInfo = isOver
                    ? $"  [bold red](⚖ {tw}/{max} кг — ПЕРЕГРУЗКА!)[/]"
                    : $"  [dim](⚖ {tw}/{max} кг)[/]";
            }

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]🎒 {_loc.T("inventory")}[/]{weightInfo}" +
                    "  [dim](выберите для просмотра / управления)[/]")
                .PageSize(20)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected == "← Назад") break;

            var idx = choices.IndexOf(selected);
            if (idx < 0) break;

            // Skip non-interactive info rows (money, resources)
            if (idx < infoPrefixCount) continue;

            // Check if storage entry was selected
            if (selected.Contains("→ управление") && accessibleStorages.Count > 0)
            {
                // Find which storage was clicked
                var storIdx = accessibleStorages.FindIndex(s => selected.Contains(s.name, StringComparison.OrdinalIgnoreCase));
                if (storIdx >= 0)
                {
                    var stor = accessibleStorages[storIdx];
                    var storModified = await ShowStorageInteractivePanel(stor.name, stor.storageId);
                    if (storModified) await _stateManager.RefreshGameStateAsync();
                }
                continue;
            }

            // Locked storage — skip
            if (selected.Contains("🔒")) continue;

            var itemIdx = idx - infoPrefixCount; // offset past money/resources

            bool modified;
            if (itemIdx < equippedEntries.Count)
            {
                var slotEntry = equippedEntries[itemIdx];
                modified = await ShowItemDetailPanel(slotEntry.ItemIdentity, slotEntry.ItemName, slotEntry.Data, slotEntry.SlotKey);
            }
            else
            {
                var invIdx = itemIdx - equippedEntries.Count;
                if (invIdx < 0 || invIdx >= inventoryItems.Count) continue;
                var (itemIdentity, itemName, _, itemData) = inventoryItems[invIdx];
                string? equippedInSlot = null;
                foreach (var entry in equippedEntries)
                {
                    if (InventoryItemMatches(itemData, entry.ItemIdentity, entry.ItemName))
                    {
                        equippedInSlot = entry.SlotKey;
                        break;
                    }
                }
                modified = await ShowItemDetailPanel(itemIdentity, itemName, itemData, equippedInSlot);
            }

            if (modified)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    /// <summary>
    /// Detail panel for an inventory item. Equip/unequip modify items.json directly (no GM needed).
    /// Returns true if state was modified.
    /// </summary>
    private async Task<bool> ShowItemDetailPanel(string itemIdentity, string name, JsonElement? itemData, string? equippedSlot,
        bool readOnly = false, string? readOnlyStatusOverride = null, bool allowInventorySidecars = true)
    {
        var lines = new List<string>();
        lines.Add($"[bold yellow]📦 {Markup.Escape(name)}[/]");
        lines.Add("");

        string itemSlot = "";
        string itemType = "";
        JsonElement? resourceEntry = null;

	        if (itemData.HasValue)
	        {
	            var item = itemData.Value;
	            var itemResourcesDoc = allowInventorySidecars
                    ? await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_resources.json")
                    : null;
	            var itemBondsDoc = allowInventorySidecars
                    ? await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_bonds.json")
                    : null;
	            var itemTextDoc = allowInventorySidecars
                    ? await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_text_updates.json")
                    : null;
	            var itemJournalsDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/item_journals.json");
	            resourceEntry = FindInventorySidecarEntry(itemResourcesDoc, itemIdentity, name, "entries", "inventoryItemsResources");
	            var bondEntry = FindInventorySidecarEntry(itemBondsDoc, itemIdentity, name, "entries", "itemBondLevelChanges", "itemFateCardUnlocks");
	            var textEntry = FindInventorySidecarEntry(itemTextDoc, itemIdentity, name, "entries", "updateItemTextContents");
	            var journalEntry = FindInventorySidecarEntry(itemJournalsDoc, itemIdentity, name, "entries", "itemJournals", "itemJournalUpdates");

            var desc = GetStr(item, "description", "");
            if (!string.IsNullOrEmpty(desc)) { lines.Add($"[white]{Markup.Escape(desc)}[/]"); lines.Add(""); }

            itemType = GetStr(item, "type", "");
            if (!string.IsNullOrEmpty(itemType))
                lines.Add($"  📋 Тип: [cyan]{Markup.Escape(itemType)}[/]");

            var rarity = GetStr(item, "quality", GetStr(item, "rarity", ""));
            if (!string.IsNullOrEmpty(rarity))
                lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");

            var weight = GetStr(item, "weight", "");
            if (!string.IsNullOrEmpty(weight))
                lines.Add($"  ⚖ Вес: [white]{Markup.Escape(weight)} кг[/]");

            var price = GetStr(item, "price", "");
            if (!string.IsNullOrEmpty(price))
                lines.Add($"  💰 Цена: [gold1]{Markup.Escape(price)}[/]");

            var durability = GetStr(item, "durability", "");
            var maxDurability = GetStr(item, "maxDurability", "");
            if (!string.IsNullOrEmpty(durability))
            {
                var durNum = int.TryParse(durability.Replace("%", "").Trim(), out var dv) ? dv : -1;
                var maxDurNum = int.TryParse(maxDurability.Replace("%", "").Trim(), out var mdv) ? mdv : durNum;
                if (durNum == 0)
                {
                    lines.Add($"  🔧 Прочность: [bold red]СЛОМАН (0/{Markup.Escape(maxDurability)})[/]");
                }
                else if (durNum > 0 && maxDurNum > 0)
                {
                    var durPct = Math.Clamp(durNum * 100 / maxDurNum, 0, 100);
                    var durColor = durPct > 60 ? "green" : durPct > 25 ? "yellow" : "red";
                    lines.Add($"  🔧 Прочность: {ConsoleLayout.CreateBarFromPercent(durPct, 10, durColor)}  [{durColor}]{Markup.Escape(durability)}/{Markup.Escape(maxDurability)}[/]");
                }
                else
                {
                    lines.Add($"  🔧 Прочность: [white]{Markup.Escape(durability)}[/]");
                }
            }

            var count = GetStr(item, "count", GetStr(item, "quantity", "1"));
            if (count != "1")
                lines.Add($"  📊 Количество: [white]{Markup.Escape(count)}[/]");

            itemSlot = GetStr(item, "equipmentSlot", GetStr(item, "slot", GetStr(item, "equipSlot", "")));
            if (!string.IsNullOrEmpty(itemSlot))
                lines.Add($"  📌 Слот: [cyan]{Markup.Escape(itemSlot)}[/]");
            var accessorySlot = GetStr(item, "accessoryForSlot", "");
            if (!string.IsNullOrEmpty(accessorySlot))
                lines.Add($"  📎 Аксессуар для: [cyan]{Markup.Escape(accessorySlot)}[/]");

            var twoHanded = item.TryGetProperty("requiresTwoHands", out var th) && th.ValueKind == JsonValueKind.True;
            if (twoHanded)
                lines.Add("  🗡️ [yellow]Двуручное[/]");

            var group = GetStr(item, "group", "");
            if (!string.IsNullOrEmpty(group))
                lines.Add($"  📂 Группа: [white]{Markup.Escape(group)}[/]");

            if (item.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
            {
                lines.Add(""); lines.Add("  [bold]📊 Бонусы:[/]");
                foreach (var b in bonuses.EnumerateArray())
                {
                    if (b.ValueKind == JsonValueKind.String)
                        lines.Add($"    • [green]{Markup.Escape(b.GetString() ?? "")}[/]");
                    else if (b.ValueKind == JsonValueKind.Object)
                    {
                        var bName = GetStr(b, "name", GetStr(b, "stat", ""));
                        var bVal = GetStr(b, "value", GetStr(b, "bonus", ""));
                        if (!string.IsNullOrEmpty(bName))
                            lines.Add($"    • [green]{Markup.Escape(bName)}: {Markup.Escape(bVal)}[/]");
                    }
                }
            }

            if (item.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
            {
                lines.Add(""); lines.Add("  [bold]🔮 Эффекты:[/]");
                foreach (var e in effects.EnumerateArray())
                {
                    var eName = e.ValueKind == JsonValueKind.String ? e.GetString() : GetStr(e, "name", GetStr(e, "effect", ""));
                    if (!string.IsNullOrEmpty(eName))
                        lines.Add($"    • [mediumpurple2]{Markup.Escape(eName!)}[/]");
                }
            }

            if (item.TryGetProperty("specialProperties", out var specials) && specials.ValueKind == JsonValueKind.Array)
            {
                lines.Add(""); lines.Add("  [bold]✨ Особые свойства:[/]");
                foreach (var s in specials.EnumerateArray())
                {
                    var sStr = s.ValueKind == JsonValueKind.String ? s.GetString() : s.GetRawText();
                    if (!string.IsNullOrEmpty(sStr))
                        lines.Add($"    • [yellow]{Markup.Escape(sStr!)}[/]");
                }
            }

            if (item.TryGetProperty("lore", out var lore) && lore.ValueKind == JsonValueKind.String)
            {
                lines.Add("");
                lines.Add($"  [dim italic]📖 {Markup.Escape(lore.GetString() ?? "")}[/]");
            }

            // Structured bonuses (Block 10.2)
            if (item.TryGetProperty("structuredBonuses", out var sb) && sb.ValueKind == JsonValueKind.Array && sb.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add("  [bold]📊 Структурные бонусы:[/]");
                foreach (var b in sb.EnumerateArray())
                {
                    var bType = GetStr(b, "bonusType", GetStr(b, "type", "?"));
                    var bTarget = GetStr(b, "target", "");
                    var bValueType = GetStr(b, "valueType", "");
                    var bVal = GetStr(b, "value", "");
                    var bApp = GetStr(b, "application", "");
                    var bCond = GetStr(b, "condition", "");
                    var bonusLine = $"    • [green]{Markup.Escape(bType)}[/]";
                    if (!string.IsNullOrEmpty(bTarget)) bonusLine += $" → {Markup.Escape(bTarget)}";
                    if (!string.IsNullOrEmpty(bVal)) bonusLine += $": [white]{Markup.Escape(bVal)}[/]";
                    if (!string.IsNullOrEmpty(bValueType)) bonusLine += $" [dim][{Markup.Escape(bValueType)}][/]";
                    if (!string.IsNullOrEmpty(bApp) && bApp != "Permanent") bonusLine += $" [dim]({Markup.Escape(bApp)})[/]";
                    if (!string.IsNullOrEmpty(bCond)) bonusLine += $" [dim italic]если: {Markup.Escape(bCond)}[/]";
                    lines.Add(bonusLine);
                }
            }

            // Combat effects (Block 10.4 — Combat Action Objects with actionName, isActivatedEffect, effects[])
            if (item.TryGetProperty("combatEffect", out var ce) && (ce.ValueKind == JsonValueKind.Object || ce.ValueKind == JsonValueKind.Array))
            {
                lines.Add(""); lines.Add("  [bold]⚔ Боевые эффекты:[/]");
                void RenderItemEffect(JsonElement eff)
                {
                    var eType = GetStr(eff, "effectType", "?");
                    var eVal = GetStr(eff, "value", "");
                    var eTgt = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                    var eDur = GetStr(eff, "duration", "");
                    var eDesc = GetStr(eff, "effectDescription", "");
                    var ePoise = GetStr(eff, "poiseDamage", "");
                    var eTgtCount = GetStr(eff, "targetsCount", "");
                    var eDmgThresh = GetStr(eff, "damageThreshold", "");
                    var line = $"      ⚡ [{(eType.ToLower().Contains("damage") ? "red" : "cyan")}]{Markup.Escape(eType)}[/] {Markup.Escape(eVal)}";
                    if (!string.IsNullOrEmpty(eTgt)) line += $" → {Markup.Escape(eTgt)}";
                    if (!string.IsNullOrEmpty(ePoise) && ePoise != "0") line += $" [dim](🛡️ -{Markup.Escape(ePoise)} стойк.)[/]";
                    if (!string.IsNullOrEmpty(eTgtCount) && eTgtCount != "1") line += $" [dim](×{Markup.Escape(eTgtCount)} целей)[/]";
                    if (!string.IsNullOrEmpty(eDmgThresh) && eDmgThresh != "0") line += $" [dim](порог: {Markup.Escape(eDmgThresh)})[/]";
                    if (!string.IsNullOrEmpty(eDur) && eDur != "0") line += $" [dim]({Markup.Escape(eDur)} ход.)[/]";
                    lines.Add(line);
                    if (!string.IsNullOrEmpty(eDesc)) lines.Add($"        [dim]{Markup.Escape(eDesc)}[/]");
                }
                void RenderCombatActionObject(JsonElement cao)
                {
                    // Combat Action Object has actionName, isActivatedEffect, actionCost, effects[]
                    var actName = GetStr(cao, "actionName", "");
                    var isActivated = cao.TryGetProperty("isActivatedEffect", out var iae) && iae.ValueKind == JsonValueKind.True;
                    var actCost = GetStr(cao, "actionCost", "");
                    if (!string.IsNullOrEmpty(actName))
                    {
                        var tag = isActivated ? "[yellow](активируемый)[/]" : "[dim](пассивный)[/]";
                        var costLabel = actCost.ToLower() switch
                        {
                            "main" or "основное" => " [red](основное действие)[/]",
                            "fast" or "быстрое" => " [yellow](быстрое действие)[/]",
                            "free" or "свободное" => " [green](свободное действие)[/]",
                            _ => ""
                        };
                        lines.Add($"    [white]{Markup.Escape(actName)}[/] {tag}{costLabel}");
                    }
                    if (cao.TryGetProperty("effects", out var effs) && effs.ValueKind == JsonValueKind.Array)
                        foreach (var e in effs.EnumerateArray()) RenderItemEffect(e);
                    else if (cao.TryGetProperty("effectType", out _))
                        RenderItemEffect(cao); // Flat effect object fallback
                }
                if (ce.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in ce.EnumerateArray())
                    {
                        if (e.TryGetProperty("effects", out _) || e.TryGetProperty("actionName", out _))
                            RenderCombatActionObject(e); // Combat Action Object
                        else
                            RenderItemEffect(e); // Flat effect
                    }
                }
                else
                    RenderCombatActionObject(ce);
            }

            // Container info (Block 10.2)
            var isContainer = item.TryGetProperty("isContainer", out var ic) && ic.ValueKind == JsonValueKind.True;
            if (isContainer)
            {
                var cap = GetStr(item, "capacity", "?");
                var vol = GetStr(item, "volume", "");
                var contWeight = GetStr(item, "containerWeight", "");
                var wReduction = GetStr(item, "weightReduction", "");
                var contLine = $"  📦 Контейнер: вместимость [white]{Markup.Escape(cap)}[/]";
                if (!string.IsNullOrEmpty(vol)) contLine += $", объём [white]{Markup.Escape(vol)} дм³[/]";
                if (!string.IsNullOrEmpty(contWeight)) contLine += $", пустой [white]{Markup.Escape(contWeight)} кг[/]";
                lines.Add(contLine);
                if (!string.IsNullOrEmpty(wReduction) && wReduction != "0")
                    lines.Add($"    ✨ Снижение веса содержимого: [green]{Markup.Escape(wReduction)}%[/]");
            }

            // Disassembly (Block 9 — materialName, quantity, weight, price, description)
            if (item.TryGetProperty("disassembleTo", out var dis) && dis.ValueKind == JsonValueKind.Array && dis.GetArrayLength() > 0)
            {
                lines.Add("  🔧 Разбирается на:");
                foreach (var p in dis.EnumerateArray())
                {
                    if (p.ValueKind == JsonValueKind.String)
                    {
                        lines.Add($"    • [dim]{Markup.Escape(p.GetString() ?? "?")}[/]");
                        continue;
                    }
                    var matName = GetStr(p, "materialName", GetStr(p, "name", "?"));
                    var matQty = GetStr(p, "quantity", "1");
                    var matWeight = GetStr(p, "weight", "");
                    var matVolume = GetStr(p, "volume", "");
                    var matPrice = GetStr(p, "price", "");
                    var matDesc = GetStr(p, "description", "");
                    var matLine = $"    • [white]{Markup.Escape(matName)}[/] ×{Markup.Escape(matQty)}";
                    var matDims = new List<string>();
                    if (!string.IsNullOrEmpty(matWeight)) matDims.Add($"{Markup.Escape(matWeight)} кг");
                    if (!string.IsNullOrEmpty(matVolume)) matDims.Add($"{Markup.Escape(matVolume)} дм³");
                    if (matDims.Count > 0) matLine += $" [dim]({string.Join(", ", matDims)})[/]";
                    if (!string.IsNullOrEmpty(matPrice)) matLine += $" [yellow]~{Markup.Escape(matPrice)}¤[/]";
                    lines.Add(matLine);
                    if (!string.IsNullOrEmpty(matDesc))
                        lines.Add($"      [dim]{Markup.Escape(matDesc)}[/]");
                }
            }

	            // Text content — readable items (books, notes, scrolls)
	            var renderedTextEntries = new HashSet<string>(StringComparer.Ordinal);
	            JsonElement sidecarTextEntries = default;
	            var hasEmbeddedText = item.TryGetProperty("textContent", out var tc) && tc.ValueKind == JsonValueKind.Array && tc.GetArrayLength() > 0;
	            var hasSidecarText = textEntry.HasValue &&
	                                 textEntry.Value.TryGetProperty("textContent", out sidecarTextEntries) &&
	                                 sidecarTextEntries.ValueKind == JsonValueKind.Array &&
	                                 sidecarTextEntries.GetArrayLength() > 0;
	            if (hasEmbeddedText || hasSidecarText)
	            {
	                lines.Add(""); lines.Add("  [bold]📖 Содержимое:[/]");

	                void RenderTextEntries(JsonElement entries)
	                {
	                    foreach (var page in entries.EnumerateArray())
	                    {
	                        var pageText = page.ValueKind == JsonValueKind.String ? page.GetString() ?? "" : page.GetRawText();
	                        if (!string.IsNullOrWhiteSpace(pageText) && renderedTextEntries.Add(pageText))
	                            lines.Add($"    [white italic]{Markup.Escape(pageText)}[/]");
	                    }
	                }

	                if (hasEmbeddedText)
	                    RenderTextEntries(tc);
	                if (hasSidecarText)
	                    RenderTextEntries(sidecarTextEntries);
	            }

            // Resource/charges (potions, wands, etc.)
            var resource = GetPreferredStr(resourceEntry, item, "resource");
            var maxResource = GetPreferredStr(resourceEntry, item, "maximumResource");
            var resourceType = GetPreferredStr(resourceEntry, item, "resourceType");
            if (string.IsNullOrWhiteSpace(resourceType))
                resourceType = "заряды";
            if (!string.IsNullOrEmpty(resource))
            {
                var resNum = int.TryParse(resource.Replace("%", "").Trim(), out var rv) ? rv : -1;
                if (resNum == 0)
                {
                    lines.Add($"  🔋 {Markup.Escape(resourceType)}: [bold red]ПУСТО (0/{Markup.Escape(maxResource)})[/]");
                }
                else
                {
                    var resLine = $"  🔋 {Markup.Escape(resourceType)}: [yellow]{Markup.Escape(resource)}[/]";
                    if (!string.IsNullOrEmpty(maxResource))
                        resLine += $" / [white]{Markup.Escape(maxResource)}[/]";
                    lines.Add(resLine);
                }
            }

            // Owner bond level (Rare+ items)
            var bondLevel = GetPreferredStr(bondEntry, item, "ownerBondLevelCurrent");
            if (!string.IsNullOrEmpty(bondLevel))
            {
                if (int.TryParse(bondLevel, out var bondInt))
                {
                    lines.Add($"  💎 Связь с владельцем: {ConsoleLayout.CreateBar(Math.Clamp(bondInt / 10, 0, 10), 10, "cyan")} {bondInt}/100");
                }
                else
                    lines.Add($"  💎 Связь с владельцем: [cyan]{Markup.Escape(bondLevel)}[/]");

                var bondReason = bondEntry.HasValue ? GetStr(bondEntry.Value, "lastBondChangeReason", "") : "";
                if (!string.IsNullOrWhiteSpace(bondReason))
                    lines.Add($"    [dim]{Markup.Escape(bondReason)}[/]");
            }

            // Sentient item flag
            if (item.TryGetProperty("isSentient", out var sent) && sent.ValueKind == JsonValueKind.True)
                lines.Add("  🧠 [mediumpurple2]Разумный предмет[/]");

            // Journal entries (Block 10.2)
            JsonElement sidecarJournalEntries = default;
            var hasEmbeddedJournal = item.TryGetProperty("journalEntries", out var je) && je.ValueKind == JsonValueKind.Array && je.GetArrayLength() > 0;
            var hasSidecarJournal = journalEntry.HasValue &&
                                    journalEntry.Value.TryGetProperty("journalEntries", out sidecarJournalEntries) &&
                                    sidecarJournalEntries.ValueKind == JsonValueKind.Array &&
                                    sidecarJournalEntries.GetArrayLength() > 0;
            if (hasEmbeddedJournal || hasSidecarJournal)
            {
                lines.Add(""); lines.Add("  [bold]📝 Записи:[/]");
                var renderedEntries = new HashSet<string>(StringComparer.Ordinal);

                void RenderJournalEntry(JsonElement entry)
                {
                    if (entry.ValueKind == JsonValueKind.String)
                    {
                        var textValue = entry.GetString() ?? "";
                        if (renderedEntries.Add(textValue))
                            lines.Add($"    • [dim]{Markup.Escape(textValue)}[/]");
                        return;
                    }

                    var timestamp = GetStr(entry, "timestamp", "");
                    var eventName = GetStr(entry, "event", "");
                    var description = GetStr(entry, "description", "");
                    var textValueObject = GetStr(entry, "text", GetStr(entry, "content", GetStr(entry, "entry", entry.GetRawText())));
                    var spiritVoice = GetStr(entry, "spiritVoice", "");
                    var resonance = GetStr(entry, "magicalResonance", "");

                    var signature = $"{timestamp}|{eventName}|{description}|{textValueObject}|{spiritVoice}|{resonance}";
                    if (!renderedEntries.Add(signature))
                        return;

                    var line = "    • [dim]";
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        line += $"{Markup.Escape(timestamp)}";
                    if (!string.IsNullOrWhiteSpace(eventName))
                        line += (!string.IsNullOrWhiteSpace(timestamp) ? " — " : "") + $"{Markup.Escape(eventName)}";
                    if (!string.IsNullOrWhiteSpace(description))
                        line += (!string.IsNullOrWhiteSpace(timestamp) || !string.IsNullOrWhiteSpace(eventName) ? ": " : "") + $"{Markup.Escape(description)}";
                    else if (!string.IsNullOrWhiteSpace(textValueObject))
                        line += (!string.IsNullOrWhiteSpace(timestamp) || !string.IsNullOrWhiteSpace(eventName) ? ": " : "") + $"{Markup.Escape(textValueObject)}";
                    line += "[/]";
                    lines.Add(line);

                    if (!string.IsNullOrWhiteSpace(spiritVoice))
                        lines.Add($"      [italic mediumpurple2]«{Markup.Escape(spiritVoice)}»[/]");
                    if (!string.IsNullOrWhiteSpace(resonance))
                        lines.Add($"      [dim]Резонанс: {Markup.Escape(resonance)}[/]");
                }

                if (hasEmbeddedJournal)
                {
                    foreach (var entry in je.EnumerateArray())
                        RenderJournalEntry(entry);
                }

                if (hasSidecarJournal)
                {
                    foreach (var entry in sidecarJournalEntries.EnumerateArray())
                        RenderJournalEntry(entry);
                }
            }

            // Item Fate Cards (Rare+ items, Rule 10.2)
            JsonElement sidecarFateCards = default;
            var hasEmbeddedFateCards = item.TryGetProperty("fateCards", out var ifc) && ifc.ValueKind == JsonValueKind.Array && ifc.GetArrayLength() > 0;
            var hasSidecarFateCards = bondEntry.HasValue &&
                                      bondEntry.Value.TryGetProperty("fateCards", out sidecarFateCards) &&
                                      sidecarFateCards.ValueKind == JsonValueKind.Array &&
                                      sidecarFateCards.GetArrayLength() > 0;
            if (hasEmbeddedFateCards || hasSidecarFateCards)
            {
                lines.Add(""); lines.Add("  [bold gold1]🃏 Карты судьбы предмета:[/]");

                void RenderFateCards(JsonElement arr)
                {
                    foreach (var card in arr.EnumerateArray())
                    {
                        var cardName = GetStr(card, "name", GetStr(card, "cardName", "???"));
                        var cardDesc = GetStr(card, "description", "");
                        var cardUnlocked = card.TryGetProperty("isUnlocked", out var ciu) && ciu.ValueKind == JsonValueKind.True;
                        var statusIcon = cardUnlocked ? " [green]● разблокирована[/]" : " [dim red]🔒 заблокирована[/]";
                        lines.Add($"    🃏 [gold1]{Markup.Escape(cardName)}[/]{statusIcon}");
                        if (!string.IsNullOrEmpty(cardDesc))
                            lines.Add($"      [white]{Markup.Escape(cardDesc)}[/]");

                        if (!cardUnlocked && card.TryGetProperty("unlockConditions", out var uc) && uc.ValueKind == JsonValueKind.Object)
                        {
                            var conditions = new List<string>();
                            var bondReq = GetStr(uc, "ownerBondLevel", "");
                            if (!string.IsNullOrEmpty(bondReq)) conditions.Add($"связь ≥ {Markup.Escape(bondReq)}");
                            var plotReq = GetStr(uc, "plotConditionDescription", "");
                            if (!string.IsNullOrEmpty(plotReq)) conditions.Add(Markup.Escape(plotReq));
                            if (conditions.Count > 0)
                            {
                                var conj = GetStr(uc, "conjunction", "AND").Equals("OR", StringComparison.OrdinalIgnoreCase) ? " ИЛИ " : " И ";
                                lines.Add($"      [dim]Условия: {string.Join(conj, conditions)}[/]");
                            }
                        }
                        if (cardUnlocked && card.TryGetProperty("rewards", out var cr) && cr.ValueKind == JsonValueKind.Object)
                        {
                            var rewardDesc = GetStr(cr, "description", "");
                            if (!string.IsNullOrEmpty(rewardDesc))
                                lines.Add($"      [italic green]📜 {Markup.Escape(rewardDesc)}[/]");
                        }
                    }
                }

                if (hasEmbeddedFateCards)
                    RenderFateCards(ifc);
                if (hasSidecarFateCards)
                    RenderFateCards(sidecarFateCards);
            }

            // Custom properties (Rule 10.2.8) — array of interaction objects or legacy object format
            if (item.TryGetProperty("customProperties", out var cp))
            {
                if (cp.ValueKind == JsonValueKind.Array && cp.GetArrayLength() > 0)
                {
                    lines.Add(""); lines.Add("  [bold]🔧 Особые свойства:[/]");
                    foreach (var cpItem in cp.EnumerateArray())
                    {
                        if (cpItem.ValueKind != JsonValueKind.Object) continue;
                        var iType = GetStr(cpItem, "interactionType", "");
                        var target = GetStr(cpItem, "targetStateName", "");
                        var changeVal = GetStr(cpItem, "changeValue", "");
                        var cpDesc = GetStr(cpItem, "description", "");

                        var triggerLabel = iType switch
                        {
                            "onConsume" => "При употреблении",
                            "onEquip" => "При экипировке",
                            "onUse" => "При использовании",
                            _ => !string.IsNullOrEmpty(iType) ? iType : "Эффект"
                        };

                        if (!string.IsNullOrEmpty(target))
                        {
                            var sign = changeVal.StartsWith("-") ? "" : "+";
                            var changeColor = changeVal.StartsWith("-") ? "green" : "yellow";
                            lines.Add($"    ⚡ [white]{Markup.Escape(triggerLabel)}[/]: [{changeColor}]{sign}{Markup.Escape(changeVal)}[/] к [cyan]{Markup.Escape(target)}[/]");
                        }
                        if (!string.IsNullOrEmpty(cpDesc))
                            lines.Add($"      [dim]{Markup.Escape(cpDesc)}[/]");
                    }
                }
                else if (cp.ValueKind == JsonValueKind.Object)
                {
                    // Legacy: object with key-value pairs
                    lines.Add(""); lines.Add("  [bold]🔧 Особые свойства:[/]");
                    foreach (var prop in cp.EnumerateObject())
                    {
                        var pVal = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? "" : prop.Value.GetRawText();
                        if (pVal.Length < 200)
                            lines.Add($"    • {Markup.Escape(prop.Name)}: [white]{Markup.Escape(pVal)}[/]");
                    }
                }
            }

            var knownProps = new HashSet<string> { "name", "description", "type", "quality", "rarity",
                "weight", "price", "durability", "maxDurability", "count", "quantity", "slot", "equipSlot",
                "bonuses", "effects", "specialProperties", "lore", "id", "itemId", "structuredBonuses",
                "combatEffect", "isContainer", "capacity", "volume", "disassembleTo", "isSentient",
                "journalEntries", "customProperties", "image_prompt", "existedId", "contentsPath",
                "textContent", "updateItemTextContents", "isConsumption", "weightReduction",
                "containerWeight", "requiresTwoHands", "accessoryForSlot", "equipmentSlot", "group",
                "resource", "maximumResource", "resourceType", "ownerBondLevelCurrent",
                "fateCards", "itemJournalUpdates", "isBroken", "isEmpty" };
            foreach (var prop in item.EnumerateObject())
            {
                if (knownProps.Contains(prop.Name)) continue;
                var val = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) ? "" : prop.Value.GetRawText();
                if (val.Length > 0 && val.Length < 200)
                    lines.Add($"  [dim]{Markup.Escape(prop.Name)}: {Markup.Escape(val)}[/]");
            }
        }
        else
        {
            lines.Add("[dim]Подробная информация недоступна[/]");
        }

        // Status line with broken/empty flags
        var isBroken = itemData.HasValue && (
            (itemData.Value.TryGetProperty("isBroken", out var brk) && brk.ValueKind == JsonValueKind.True) ||
            (int.TryParse(GetStr(itemData.Value, "durability", "1").Replace("%", "").Trim(), out var durCheck) && durCheck == 0));
        var preferredResource = itemData.HasValue ? GetPreferredStr(resourceEntry, itemData.Value, "resource") : "";
        var isEmpty = itemData.HasValue && (
            (itemData.Value.TryGetProperty("isEmpty", out var emp) && emp.ValueKind == JsonValueKind.True) ||
            (int.TryParse(preferredResource.Replace("%", "").Trim(), out var resCheck) && resCheck == 0));

        lines.Add("");
        if (isBroken)
            lines.Add("  ⚠ [bold red]СЛОМАН[/]");
        if (isEmpty)
            lines.Add("  ⚠ [bold yellow]ПУСТО (ресурсы израсходованы)[/]");
        if (!string.IsNullOrWhiteSpace(readOnlyStatusOverride))
        {
            lines.Add($"  Статус: [dim]{Markup.Escape(readOnlyStatusOverride)}[/]");
        }
        else if (!string.IsNullOrEmpty(equippedSlot))
        {
            var slotLabel = SlotLabels.GetValueOrDefault(equippedSlot!, equippedSlot!);
            lines.Add($"  Статус: [green]⚔ Экипировано ({slotLabel})[/]");
        }
        else
            lines.Add("  Статус: [dim]📦 В рюкзаке[/]");

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📦 Предмет ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });

        // Determine item count for stack operations
        var itemCount = 1;
        if (itemData.HasValue)
        {
            var cStr = GetStr(itemData.Value, "count", GetStr(itemData.Value, "quantity", "1"));
            int.TryParse(cStr, out itemCount);
            if (itemCount < 1) itemCount = 1;
        }

        // Action menu
        var actions = new List<string>();
        if (!readOnly)
        {
            if (!string.IsNullOrEmpty(equippedSlot))
            {
                actions.Add("📦 Снять (убрать в рюкзак)");
            }
            else
            {
                var isEquippable = !string.IsNullOrEmpty(itemSlot) ||
                    TypeToSlot.ContainsKey(itemType);
                if (isEquippable && !isBroken)
                    actions.Add("⚔ Экипировать");
            }
            if (itemCount > 1)
                actions.Add("✂ Разделить стопку");
            actions.Add("📚 Сложить с другим предметом");
        }
        // Image actions
        var itemImagePrompt = itemData.HasValue ? GetStr(itemData.Value, "image_prompt", "") : "";
        var itemImageKey = !string.IsNullOrWhiteSpace(itemIdentity) ? itemIdentity : name;
        if (_imageService != null && !string.IsNullOrEmpty(itemImagePrompt))
        {
            actions.Add("🖼 Показать изображение");
            if (_imageService.EntityImageExists("item", itemImageKey))
                actions.Add("♻ Пересоздать изображение");
        }
        if (!readOnly)
            actions.Add("[red]🗑 Выбросить[/]");
        actions.Add(readOnly ? "← Назад" : "← Назад к списку");

        var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Yellow))
            .AddChoices(actions));

        if (readOnly)
        {
            if (action.Contains("Показать изображение") && _imageService != null)
            {
                await _imageService.ShowOrGenerateEntityImageAsync(itemImagePrompt, "item", itemImageKey, forceDisplay: true);
                WaitForKey();
            }
            else if (action.Contains("Пересоздать изображение") && _imageService != null)
            {
                await RegenerateEntityImageAsync(itemImagePrompt, "item", itemImageKey);
                WaitForKey();
            }

            return false;
        }

        if (action.Contains("Снять") && !string.IsNullOrEmpty(equippedSlot))
        {
            await UnequipItemLocal(equippedSlot!);
            return true;
        }
        if (action.Contains("Экипировать"))
        {
            var targetSlot = ResolveEquipSlot(itemSlot, itemType);
            if (targetSlot == null)
            {
                // Let user pick a slot
                var slotChoices = SlotLabels.Select(kv => $"{kv.Value} ({kv.Key})").ToList();
                slotChoices.Add("← Отмена");
                var pick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]В какой слот экипировать?[/]")
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(slotChoices));
                if (pick.Contains("Отмена")) return false;
                var m = System.Text.RegularExpressions.Regex.Match(pick, @"\((\w+)\)");
                targetSlot = m.Success ? m.Groups[1].Value : null;
            }
            if (targetSlot != null)
            {
                await EquipItemLocal(itemIdentity, name, targetSlot);
                return true;
            }
        }
        if (action.Contains("Разделить стопку"))
        {
            var splitAmount = AnsiConsole.Prompt(
                new TextPrompt<int>($"[bold]Сколько отделить? (1—{itemCount - 1}):[/]")
                    .ValidationErrorMessage("[red]Введите число[/]")
                    .Validate(n => n >= 1 && n < itemCount
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[red]Введите число от 1 до {itemCount - 1}[/]")));
            await SplitItemStack(itemIdentity, name, splitAmount);
            return true;
        }
        if (action.Contains("Сложить с другим"))
        {
            await MergeItemStacks(itemIdentity, name);
            return true;
        }
        if (action.Contains("Показать изображение") && _imageService != null)
        {
            await _imageService.ShowOrGenerateEntityImageAsync(itemImagePrompt, "item", itemImageKey, forceDisplay: true);
            WaitForKey();
            return false;
        }
        if (action.Contains("Пересоздать изображение") && _imageService != null)
        {
            await RegenerateEntityImageAsync(itemImagePrompt, "item", itemImageKey);
            WaitForKey();
            return false;
        }
        if (action.Contains("Выбросить"))
        {
            var confirm = AnsiConsole.Prompt(new ConfirmationPrompt(
                $"[bold red]Вы уверены, что хотите выбросить «{Markup.Escape(name)}»" +
                (itemCount > 1 ? $" ({itemCount} шт.)" : "") + "?[/]")
            { DefaultValue = false });
            if (confirm)
            {
                await DropItemLocal(itemIdentity, name);
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the equipment slot key from item slot name or type.</summary>
    private static string? ResolveEquipSlot(string itemSlot, string itemType)
    {
        // Try direct slot name match (case-insensitive)
        if (!string.IsNullOrEmpty(itemSlot))
        {
            var lower = itemSlot.ToLower();
            if (SlotLabels.ContainsKey(lower)) return lower;
            // Try matching Russian slot names
            foreach (var (key, label) in SlotLabels)
                if (label.Contains(itemSlot, StringComparison.OrdinalIgnoreCase)) return key;
            // Heuristic: "Neck", "Head", etc.
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Head"] = "head", ["Body"] = "body", ["Hands"] = "hands", ["Feet"] = "feet",
                ["MainHand"] = "mainHand", ["OffHand"] = "offHand", ["Neck"] = "neck",
                ["Ring"] = "ring1", ["Ring1"] = "ring1", ["Ring2"] = "ring2",
                ["Голова"] = "head", ["Тело"] = "body", ["Руки"] = "hands", ["Ноги"] = "feet",
                ["Основная рука"] = "mainHand", ["Вторая рука"] = "offHand", ["Шея"] = "neck",
                ["Кольцо"] = "ring1"
            };
            if (map.TryGetValue(itemSlot, out var mapped)) return mapped;
        }
        // Try type-based mapping
        if (!string.IsNullOrEmpty(itemType) && TypeToSlot.TryGetValue(itemType, out var slotFromType))
            return slotFromType;
        return null;
    }

    private static JsonElement? GetPlayerInventoryItemsElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items;

        if (root.TryGetProperty("UpdateInventory", out var updateInventory) &&
            updateInventory.ValueKind == JsonValueKind.Array)
            return updateInventory;

        return null;
    }

    private static JsonArray? GetPlayerInventoryArrayNode(JsonNode? root, bool createIfMissing)
    {
        if (root is not JsonObject obj)
            return null;

        if (obj["items"] is JsonArray itemsArray)
            return itemsArray;

        if (obj["UpdateInventory"] is JsonArray updateInventoryArray)
            return updateInventoryArray;

        if (!createIfMissing)
            return null;

        var created = new JsonArray();
        obj["UpdateInventory"] = created;
        return created;
    }

    private static string GetInventoryItemIdentity(JsonElement item)
    {
        var existedId = GetStr(item, "existedId", "");
        if (!string.IsNullOrWhiteSpace(existedId)) return existedId;

        var itemId = GetStr(item, "itemId", "");
        if (!string.IsNullOrWhiteSpace(itemId)) return itemId;

        return GetStr(item, "id", "");
    }

    private static string GetInventoryItemIdentity(JsonNode? item)
    {
        if (item is not JsonObject obj) return "";

        var existedId = GetNodeStr(obj, "existedId", "");
        if (!string.IsNullOrWhiteSpace(existedId)) return existedId;

        var itemId = GetNodeStr(obj, "itemId", "");
        if (!string.IsNullOrWhiteSpace(itemId)) return itemId;

        return GetNodeStr(obj, "id", "");
    }

    private static string GetInventoryItemName(JsonElement item) =>
        GetStr(item, "name", GetStr(item, "itemName", "???"));

    private static string GetInventoryItemName(JsonNode? item)
    {
        if (item is not JsonObject obj) return "???";
        return GetNodeStr(obj, "name", GetNodeStr(obj, "itemName", "???"));
    }

    private static string GetRelicIdentity(JsonElement relic)
    {
        var relicId = GetStr(relic, "relicId", "");
        if (!string.IsNullOrWhiteSpace(relicId)) return relicId;
        return GetStr(relic, "id", GetStr(relic, "name", ""));
    }

    private static bool RelicNodeMatches(JsonNode? relicNode, string relicId, string relicName)
    {
        if (relicNode is not JsonObject obj) return false;

        var nodeId = GetNodeStr(obj, "relicId", GetNodeStr(obj, "id", ""));
        if (!string.IsNullOrWhiteSpace(relicId) &&
            string.Equals(nodeId, relicId, StringComparison.OrdinalIgnoreCase))
            return true;

        var nodeName = GetNodeStr(obj, "name", "");
        return !string.IsNullOrWhiteSpace(relicName) &&
               string.Equals(nodeName, relicName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEquipmentReferenceIdentity(JsonElement slotData)
    {
        if (slotData.ValueKind == JsonValueKind.String)
            return slotData.GetString() ?? "";

        if (slotData.ValueKind != JsonValueKind.Object)
            return "";

        var existedId = GetStr(slotData, "existedId", "");
        if (!string.IsNullOrWhiteSpace(existedId)) return existedId;

        var itemId = GetStr(slotData, "itemId", "");
        if (!string.IsNullOrWhiteSpace(itemId)) return itemId;

        return GetStr(slotData, "id", "");
    }

    private static string GetEquipmentReferenceName(JsonElement slotData)
    {
        if (slotData.ValueKind == JsonValueKind.String)
            return slotData.GetString() ?? "";

        if (slotData.ValueKind != JsonValueKind.Object)
            return "";

        return GetStr(slotData, "name", GetStr(slotData, "itemName", ""));
    }

    private static bool InventoryItemMatches(JsonElement item, string itemIdentity, string itemName)
    {
        var identity = GetInventoryItemIdentity(item);
        if (!string.IsNullOrWhiteSpace(itemIdentity) &&
            string.Equals(identity, itemIdentity, StringComparison.OrdinalIgnoreCase))
            return true;

        var name = GetInventoryItemName(item);
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InventoryItemMatches(JsonNode? item, string itemIdentity, string itemName)
    {
        var identity = GetInventoryItemIdentity(item);
        if (!string.IsNullOrWhiteSpace(itemIdentity) &&
            string.Equals(identity, itemIdentity, StringComparison.OrdinalIgnoreCase))
            return true;

        var name = GetInventoryItemName(item);
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InventoryReferenceMatches(JsonNode? referenceNode, string itemIdentity, string itemName)
    {
        if (referenceNode == null) return false;

        if (referenceNode is JsonValue value && value.TryGetValue<string>(out var reference))
        {
            if (!string.IsNullOrWhiteSpace(itemIdentity) &&
                string.Equals(reference, itemIdentity, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(itemName) &&
                   string.Equals(reference, itemName, StringComparison.OrdinalIgnoreCase);
        }

        return InventoryItemMatches(referenceNode, itemIdentity, itemName);
    }

    private static IEnumerable<JsonElement> EnumerateInventorySidecarEntries(JsonElement root, params string[] propertyNames)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    yield return item;
            }
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    yield return item;
            }
        }
    }

    private static JsonElement? FindInventorySidecarEntry(JsonDocument? doc, string itemIdentity, string itemName, params string[] propertyNames)
    {
        if (doc == null)
            return null;

        foreach (var item in EnumerateInventorySidecarEntries(doc.RootElement, propertyNames))
        {
            if (InventoryItemMatches(item, itemIdentity, itemName))
                return item;
        }

        return null;
    }

    private static string GetPreferredStr(JsonElement? primary, JsonElement fallback, params string[] propertyNames)
    {
        if (primary.HasValue)
        {
            foreach (var propertyName in propertyNames)
            {
                var value = GetStr(primary.Value, propertyName, "");
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        foreach (var propertyName in propertyNames)
        {
            var value = GetStr(fallback, propertyName, "");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    private static List<string> MakeUniqueChoiceLabels(IReadOnlyList<(string Label, string Identity)> entries)
    {
        var counts = entries
            .GroupBy(e => e.Label, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            if (!counts.TryGetValue(entry.Label, out var count) || count <= 1)
            {
                result.Add(entry.Label);
                continue;
            }

            seen[entry.Label] = seen.GetValueOrDefault(entry.Label) + 1;
            var suffix = !string.IsNullOrWhiteSpace(entry.Identity)
                ? $" [dim]#{Markup.Escape(ShortIdentity(entry.Identity))}[/]"
                : $" [dim](дубль {seen[entry.Label]})[/]";
            result.Add(entry.Label + suffix);
        }

        return result;
    }

    private static string ShortIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return "?";

        var normalized = identity.Replace("-", "", StringComparison.Ordinal);
        return normalized.Length <= 8 ? normalized : normalized[..8];
    }

    private static int FindInventoryItemIndex(JsonArray items, string itemIdentity, string itemName)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (InventoryItemMatches(items[i], itemIdentity, itemName))
                return i;
        }

        return -1;
    }

    private static void AssignNewInventoryIdentity(JsonObject item)
    {
        var newId = Guid.NewGuid().ToString();
        var hadIdentityField = false;

        foreach (var key in new[] { "existedId", "itemId", "id" })
        {
            if (!item.ContainsKey(key)) continue;
            item[key] = newId;
            hadIdentityField = true;
        }

        if (!hadIdentityField)
            item["existedId"] = newId;
    }

    private static string CreateInventoryMergeSignature(JsonNode? item)
    {
        if (item is not JsonObject obj)
            return item?.ToJsonString() ?? "";

        var clone = JsonNode.Parse(obj.ToJsonString()) as JsonObject;
        if (clone == null)
            return "";

        clone.Remove("count");
        clone.Remove("quantity");
        clone.Remove("id");
        clone.Remove("itemId");
        clone.Remove("existedId");
        clone.Remove("initialId");

        return clone.ToJsonString();
    }

    /// <summary>Sets equipment.{slot} = item identity (or fallback name) in items.json.</summary>
    private async Task EquipItemLocal(string itemIdentity, string itemName, string slotKey)
    {
        const string path = "game_state/inventory/items.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return;

            var equipNode = node["equipment"];
            if (equipNode == null)
            {
                node["equipment"] = new JsonObject();
                equipNode = node["equipment"]!;
            }

            equipNode[slotKey] = !string.IsNullOrWhiteSpace(itemIdentity) ? itemIdentity : itemName;

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));

            var slotLabel = SlotLabels.GetValueOrDefault(slotKey, slotKey);
            AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(itemName)}» экипировано в {slotLabel}![/]");
            AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>Sets equipment.{slot} = null in items.json.</summary>
    private async Task UnequipItemLocal(string slotKey)
    {
        const string path = "game_state/inventory/items.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            if (node?["equipment"] == null) return;

            node["equipment"]![slotKey] = null;

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));

            var slotLabel = SlotLabels.GetValueOrDefault(slotKey, slotKey);
            AnsiConsole.MarkupLine($"[green]✅ Предмет снят с {slotLabel} и убран в рюкзак.[/]");
            AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>Removes an item from items.json entirely (drop/discard).</summary>
    private async Task DropItemLocal(string itemIdentity, string itemName)
    {
        const string path = "game_state/inventory/items.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return;

            var itemsArr = GetPlayerInventoryArrayNode(node, createIfMissing: false);
            if (itemsArr == null) return;

            var itemIndex = FindInventoryItemIndex(itemsArr, itemIdentity, itemName);
            if (itemIndex >= 0)
            {
                itemsArr.RemoveAt(itemIndex);

                // Also clear equipment slot if equipped
                var equipNode = node["equipment"];
                if (equipNode is JsonObject eqObj)
                {
                    foreach (var prop in eqObj.ToArray())
                    {
                        if (InventoryReferenceMatches(prop.Value, itemIdentity, itemName))
                            eqObj[prop.Key] = null;
                    }
                }

                var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
                AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(itemName)}» выброшен.[/]");
                AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
                Console.ReadKey(true);
                return;
            }

            AnsiConsole.MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>Splits an item stack: reduces count of original and creates a new item entry with the split amount.</summary>
    private async Task SplitItemStack(string itemIdentity, string itemName, int splitAmount)
    {
        const string path = "game_state/inventory/items.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var itemsArr = GetPlayerInventoryArrayNode(node, createIfMissing: false);
            if (itemsArr == null) return;

            var itemIndex = FindInventoryItemIndex(itemsArr, itemIdentity, itemName);
            if (itemIndex >= 0)
            {
                var original = itemsArr[itemIndex]!.AsObject();
                var countKey = original.ContainsKey("quantity") ? "quantity" : "count";

                var currentCount = original[countKey]?.GetValue<int>() ?? 1;
                if (splitAmount >= currentCount) { AnsiConsole.MarkupLine("[yellow]Нельзя отделить всё количество.[/]"); WaitForKey(); return; }

                // Reduce original
                original[countKey] = currentCount - splitAmount;

                // Create copy with split amount
                var copy = JsonNode.Parse(original.ToJsonString())!.AsObject();
                copy[countKey] = splitAmount;
                AssignNewInventoryIdentity(copy);

                itemsArr.Add(copy);

                var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));
                AnsiConsole.MarkupLine($"[green]✅ Стопка разделена: {currentCount - splitAmount} + {splitAmount}[/]");
                AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
                Console.ReadKey(true);
                return;
            }

            AnsiConsole.MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>Merges two stacks of the same item name into one.</summary>
    private async Task MergeItemStacks(string itemIdentity, string itemName)
    {
        const string path = "game_state/inventory/items.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var itemsArr = GetPlayerInventoryArrayNode(node, createIfMissing: false);
            if (itemsArr == null) return;

            var selectedIndex = FindInventoryItemIndex(itemsArr, itemIdentity, itemName);
            if (selectedIndex < 0)
            {
                AnsiConsole.MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
                WaitForKey();
                return;
            }

            var selectedItem = itemsArr[selectedIndex];
            var selectedSignature = CreateInventoryMergeSignature(selectedItem);
            var matchingIndices = new List<int> { selectedIndex };
            for (int i = 0; i < itemsArr.Count; i++)
            {
                if (i == selectedIndex) continue;
                if (CreateInventoryMergeSignature(itemsArr[i]) == selectedSignature)
                    matchingIndices.Add(i);
            }

            if (matchingIndices.Count < 2)
            {
                AnsiConsole.MarkupLine("[yellow]Нет другой стопки с таким же именем для объединения.[/]");
                WaitForKey();
                return;
            }

            // Sum all counts into the first stack, remove the rest
            var first = itemsArr[matchingIndices[0]]!.AsObject();
            var countKey = first.ContainsKey("quantity") ? "quantity" : "count";
            var totalCount = 0;
            foreach (var idx in matchingIndices)
            {
                var ck = itemsArr[idx]!.AsObject().ContainsKey("quantity") ? "quantity" : "count";
                totalCount += itemsArr[idx]![ck]?.GetValue<int>() ?? 1;
            }

            first[countKey] = totalCount;

            // Remove duplicates in reverse order to preserve indices
            for (int j = matchingIndices.Count - 1; j >= 1; j--)
                itemsArr.RemoveAt(matchingIndices[j]);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));
            AnsiConsole.MarkupLine($"[green]✅ Стопки объединены: {totalCount} шт.[/]");
            AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    private async Task ShowNPCs()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("npcs"), "НПС не обнаружены"); return; }

        // Collect NPCs
        var npcs = CollectNpcListEntries(doc);
        var renameMap = BuildNpcRenameMap(doc);

        if (npcs.Count == 0) { ShowEmptyPanel(_loc.T("npcs"), "НПС не обнаружены"); return; }

        // Pre-load supplementary data
        var relDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_relationships.json");
        var goalDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_goals.json");
        var actDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_activities.json");
        var npcInvDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_inventory.json");
        var npcEffDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_effects.json");
        var npcSkillDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_skills.json");

        var debugMode = _stateManager.Settings.AllowHistoryManipulation;

        // All NPC documents are loaded here as per API Spec and Rule 19.9.4
        var persDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_personality.json");
        var jourDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_journals.json");
        var memDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_memory.json");
        var maskDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_masks.json");
        var fateDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_fate_cards.json");
        var customDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_custom_states.json");

        while (true)
        {
            var choices = npcs.Select(n =>
            {
                var name = ResolveNpcDisplayName(n, renameMap);
                var rel = GetStr(n, "relationshipLevel", "");
                // Guardians store reputation inside relationshipData
                if (string.IsNullOrEmpty(rel) && n.TryGetProperty("relationshipData", out var rdSel) && rdSel.ValueKind == JsonValueKind.Object)
                    rel = GetStr(rdSel, "currentReputation", "0");
                if (string.IsNullOrEmpty(rel)) rel = "0";
                var loc = GetStr(n, "currentLocation", GetStr(n, "currentLocationId", ""));
                var domain = GetStr(n, "domain", "");
                var locStr = !string.IsNullOrEmpty(loc) ? $"@ {loc}"
                           : !string.IsNullOrEmpty(domain) ? $"🔮 {domain}" : "";
                return ConsoleLayout.PlainChoiceLabel($"👤 {name}", $"♥ {rel}", locStr);
            }).ToList();
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold purple]👥 {_loc.T("npcs")}[/]  [dim](выберите для подробностей)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= npcs.Count) break;

            await ShowNpcDetailPanel(npcs[selIdx], renameMap, relDoc, goalDoc, actDoc, npcInvDoc, npcEffDoc, npcSkillDoc,
                debugMode, persDoc, jourDoc, maskDoc, memDoc, fateDoc, customDoc);
        }
    }

    private async Task ShowNpcDetailPanel(JsonElement npc, Dictionary<string, string> renameMap,
        JsonDocument? relDoc, JsonDocument? goalDoc,
        JsonDocument? actDoc, JsonDocument? invDoc, JsonDocument? effDoc, JsonDocument? skillDoc,
        bool debugMode = false, JsonDocument? persDoc = null, JsonDocument? jourDoc = null,
        JsonDocument? maskDoc = null, JsonDocument? memDoc = null,
        JsonDocument? fateDoc = null, JsonDocument? customDoc = null)
    {
        var originalName = GetStr(npc, "name", "???");
        var name = ResolveNpcDisplayName(npc, renameMap);
        var npcId = GetPrimaryNpcId(npc);
        var content = new Grid().AddColumn(new GridColumn());
        var lines = new List<string>();

        if (debugMode)
        {
            content.AddRow(new Markup("[dim italic magenta1]🔮 Режим манипулирования историей — полные данные НПС[/]"));
        }

        // ── Основная информация (npc_core) ──
        content.AddRow(new Markup($"[bold white]👤 {Markup.Escape(name)}[/]"));
        var summaryTable = ConsoleLayout.CreateInfoTable();

        var desc = GetStr(npc, "shortDescription", GetStr(npc, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            content.AddRow(new Markup($"[white]{Markup.Escape(desc)}[/]"));

        var appearance = GetStr(npc, "appearance", "");
        if (!string.IsNullOrEmpty(appearance))
            summaryTable.AddRow(new Markup("[white]Внешность[/]"), new Markup($"[white]{Markup.Escape(appearance)}[/]"));

        var loc = GetStr(npc, "currentLocation", "");
        if (!string.IsNullOrEmpty(loc))
            summaryTable.AddRow(new Markup("[cyan]Локация[/]"), new Markup($"[cyan]{Markup.Escape(loc)}[/]"));

        // Relationship level — rendered as full progression display in RenderNpcRelationships
        // Here just show a quick summary if numeric
        var relVal = GetStr(npc, "relationshipLevel", "");
        if (!string.IsNullOrEmpty(relVal))
        {
            if (int.TryParse(relVal, out var relNum))
            {
                var (relLabel, relColor) = GetNpcRelationshipTier(relNum);
                summaryTable.AddRow(new Markup($"[{relColor}]Отношение[/]"), new Markup($"[{relColor}]{relNum} — {relLabel}[/]"));
            }
            else
            {
                summaryTable.AddRow(new Markup("[yellow]Отношение[/]"), new Markup($"[yellow]{Markup.Escape(relVal)}[/]"));
            }
        }

        var race = GetStr(npc, "race", "");
        if (!string.IsNullOrEmpty(race))
            summaryTable.AddRow(new Markup("[white]Раса[/]"), new Markup($"[white]{Markup.Escape(race)}[/]"));

        var npcClass = GetStr(npc, "class", "");
        if (!string.IsNullOrEmpty(npcClass))
            summaryTable.AddRow(new Markup("[white]Класс[/]"), new Markup($"[white]{Markup.Escape(npcClass)}[/]"));

        var role = GetStr(npc, "role", GetStr(npc, "occupation", ""));
        if (!string.IsNullOrEmpty(role))
            summaryTable.AddRow(new Markup("[white]Роль[/]"), new Markup($"[white]{Markup.Escape(role)}[/]"));

        var npcRarity = GetStr(npc, "rarity", "");
        if (!string.IsNullOrEmpty(npcRarity))
        {
            var rarColor = npcRarity.ToLowerInvariant() switch
            {
                "common" => "white",
                "uncommon" => "green",
                "rare" => "blue",
                "epic" => "purple",
                "legendary" => "gold1",
                _ => "white"
            };
            summaryTable.AddRow(new Markup($"[{rarColor}]Редкость[/]"), new Markup($"[{rarColor}]{Markup.Escape(npcRarity)}[/]"));
        }

        var npcAge = GetStr(npc, "age", "");
        if (!string.IsNullOrEmpty(npcAge))
            summaryTable.AddRow(new Markup("[white]Возраст[/]"), new Markup($"[white]{Markup.Escape(npcAge)}[/]"));

        var status = GetStr(npc, "status", "");
        if (!string.IsNullOrEmpty(status))
            summaryTable.AddRow(new Markup("[white]Статус[/]"), new Markup($"[white]{Markup.Escape(status)}[/]"));

        // Progression type (Companion/PlotDriven/Static)
        var progType = GetStr(npc, "progressionType", "");
        if (!string.IsNullOrEmpty(progType))
        {
            var (ptLabel, ptColor) = progType.ToLowerInvariant() switch
            {
                "companion" => ("Компаньон", "green"),
                "plotdriven" => ("Сюжетный", "yellow"),
                "static" => ("Статичный", "grey"),
                _ => (progType, "white")
            };
            summaryTable.AddRow(new Markup($"[{ptColor}]Тип развития[/]"), new Markup($"[{ptColor}]{Markup.Escape(ptLabel)}[/]"));
        }

        // NPC level/XP
        var npcLevel = GetStr(npc, "level", "");
        if (!string.IsNullOrEmpty(npcLevel))
        {
            var lvlLine = $"  📈 Уровень: [yellow]{Markup.Escape(npcLevel)}[/]";
            var npcXp = GetStr(npc, "experience", "");
            var npcXpNext = GetStr(npc, "experienceForNextLevel", "");
            if (!string.IsNullOrEmpty(npcXp) && !string.IsNullOrEmpty(npcXpNext))
                lvlLine += $" [dim]({Markup.Escape(npcXp)}/{Markup.Escape(npcXpNext)} XP)[/]";
            summaryTable.AddRow(new Markup("[yellow]Уровень[/]"), new Markup(lvlLine.Replace("  📈 Уровень: ", "")));
        }

        // Companion directive
        var compDirective = GetStr(npc, "playerCompanionDirective", "");
        if (!string.IsNullOrEmpty(compDirective))
        {
            summaryTable.AddRow(new Markup("[cyan]Директива игрока[/]"), new Markup($"[italic cyan]{Markup.Escape(compDirective)}[/]"));
        }
        else if (progType.Equals("Companion", StringComparison.OrdinalIgnoreCase))
        {
            summaryTable.AddRow(new Markup("[dim]Директива игрока[/]"), new Markup("[dim italic]не задана (используйте /директива_компаньону)[/]"));
        }

        // ── Health (embedded in npc_core) ──
        var curHp = GetStr(npc, "currentHealthPercentage", "");
        var maxHp = GetStr(npc, "maxHealthPercentage", "");
        if (!string.IsNullOrEmpty(curHp) || !string.IsNullOrEmpty(maxHp))
        {
            var hpCur = int.TryParse(curHp.Replace("%", "").Trim(), out var hpC) ? hpC : 100;
            var hpMax = int.TryParse(maxHp.Replace("%", "").Trim(), out var hpM) ? hpM : 100;
            var hpPct = hpMax > 0 ? hpCur * 100 / hpMax : 100;
            var hpColor = hpPct > 60 ? "green" : hpPct > 30 ? "yellow" : "red";
            var hpTable = ConsoleLayout.CreateBarMetricTable();
            hpTable.AddRow(
                new Markup($"[{hpColor}]Здоровье[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(hpPct, 16, hpColor)),
                new Markup($"[{hpColor}]{hpCur}%/{hpMax}%[/]"),
                new Markup("[dim]Текущее состояние тела NPC[/]"));
            content.AddRow(summaryTable);
            content.AddRow(hpTable);
            summaryTable = ConsoleLayout.CreateInfoTable();
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        var (currentLocationId, currentLocationName) = await ReadCurrentLocationIdentityAsync();
        var merchantProfile = GetNpcMerchantProfile(npc);
        var npcTradeAvailableHere = NpcTradeAvailableHere(npc, currentLocationId, currentLocationName);
        var npcTradeBlockedReason = GetNpcTradeBlockedReason(npc, currentLocationId, currentLocationName);
        if (!string.IsNullOrWhiteSpace(merchantProfile))
        {
            lines.Add("");
            lines.Add("  [bold]🛒 Локальная торговля:[/]");
            if (!string.IsNullOrWhiteSpace(npcTradeBlockedReason))
                lines.Add($"    [dim]{Markup.Escape(npcTradeBlockedReason)}[/]");
            else if (npcTradeAvailableHere)
                lines.Add($"    [white]Доступна. Профиль торговца: {Markup.Escape(GetNpcMerchantProfileDisplay(npc))}. Витрина обновляется каждые 30 игровых дней.[/]");
        }

        // ── Appearance description (detailed, separate from short appearance) ──
        var appearDesc = GetStr(npc, "appearanceDescription", "");
        if (!string.IsNullOrEmpty(appearDesc))
            lines.Add($"  🎨 Внешность (подробно): [white]{Markup.Escape(appearDesc)}[/]");

        // ── Race & class descriptions ──
        var raceDesc = GetStr(npc, "raceDescription", "");
        if (!string.IsNullOrEmpty(raceDesc))
            lines.Add($"  🧬 Раса (подробно): [dim]{Markup.Escape(raceDesc)}[/]");
        var classDesc = GetStr(npc, "classDescription", "");
        if (!string.IsNullOrEmpty(classDesc))
            lines.Add($"  ⚔ Класс (подробно): [dim]{Markup.Escape(classDesc)}[/]");

        // ── History ──
        var history = GetStr(npc, "history", "");
        if (!string.IsNullOrEmpty(history))
            lines.Add($"  📜 Предыстория: [white italic]{Markup.Escape(history)}[/]");

        // ── Worldview / alignment ──
        var worldview = GetStr(npc, "worldview", "");
        if (!string.IsNullOrEmpty(worldview))
            lines.Add($"  ⚖️ Мировоззрение: [white]{Markup.Escape(worldview)}[/]");

        // ── Attitude (derived from relationship tier) ──
        var attitude = GetStr(npc, "attitude", "");
        if (!string.IsNullOrEmpty(attitude))
            lines.Add($"  🗣️ Отношение: [yellow]{Markup.Escape(attitude)}[/]");

        // ── Cultural layer ──
        var cultural = GetStr(npc, "culturalLayer", "");
        if (!string.IsNullOrEmpty(cultural))
            lines.Add($"  🌍 Культурный слой: [white]{Markup.Escape(cultural)}[/]");
        var culturalStance = GetStr(npc, "culturalStance", "");
        if (!string.IsNullOrEmpty(culturalStance))
        {
            var stanceRu = culturalStance.ToLowerInvariant() switch
            {
                "conformist" => "Конформист",
                "pragmatist" => "Прагматик",
                "dissident" => "Диссидент",
                _ => culturalStance
            };
            lines.Add($"  🏛️ Культурная позиция: [white]{Markup.Escape(stanceRu)}[/]");
        }

        // ── Personality archetype & traits (embedded in npc_core) ──
        var persArchetype = GetStr(npc, "personalityArchetype", "");
        if (!string.IsNullOrEmpty(persArchetype))
            lines.Add($"  🧠 Архетип личности: [magenta1]{Markup.Escape(persArchetype)}[/]");

        if (npc.TryGetProperty("personalityTraits", out var pTraits) && pTraits.ValueKind == JsonValueKind.Array && pTraits.GetArrayLength() > 0)
        {
            lines.Add($"  [bold magenta1]🏷️ Черты личности:[/]");
            foreach (var trait in pTraits.EnumerateArray())
            {
                var traitName = GetStr(trait, "traitName", GetStr(trait, "name", ""));
                var traitDesc = GetStr(trait, "description", "");
                var traitVal = GetInt(trait, "value", -1);
                var traitValDesc = GetStr(trait, "valueDescription", "");
                if (string.IsNullOrEmpty(traitName)) continue;
                var line = $"    • [white]{Markup.Escape(traitName)}[/]";
                if (traitVal >= 0)
                {
                    var tBarW = 10;
                    var tFilled = Math.Clamp(traitVal * tBarW / 10, 0, tBarW);
                    var tColor = traitVal >= 7 ? "green" : traitVal >= 4 ? "yellow" : "red";
                    line += $" [{tColor}]{new string('█', tFilled)}[/][dim]{new string('░', tBarW - tFilled)}[/] [{tColor}]{traitVal}/10[/]";
                }
                if (!string.IsNullOrEmpty(traitValDesc))
                    line += $" [dim]({Markup.Escape(traitValDesc)})[/]";
                lines.Add(line);
                if (!string.IsNullOrEmpty(traitDesc))
                    lines.Add($"      [dim]{Markup.Escape(traitDesc)}[/]");
            }
        }

        // ── Characteristics (12 stats, embedded in npc_core) ──
        if (npc.TryGetProperty("characteristics", out var chars) && chars.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add($"  [bold yellow]📊 Характеристики:[/]");
            foreach (var charName in Characteristics.All)
            {
                var rusName = Characteristics.RussianNames.TryGetValue(charName, out var rn) ? rn : charName;
                var stdProp = $"standard{char.ToUpper(charName[0])}{charName[1..]}";
                var modProp = $"modified{char.ToUpper(charName[0])}{charName[1..]}";
                var stdVal = chars.TryGetProperty(stdProp, out var sv) && sv.ValueKind == JsonValueKind.Number ? sv.GetInt32() : -1;
                var modVal = chars.TryGetProperty(modProp, out var mv) && mv.ValueKind == JsonValueKind.Number ? mv.GetInt32() : -1;
                // Also try flat format: just "strength": 10
                if (stdVal < 0 && modVal < 0)
                {
                    if (chars.TryGetProperty(charName, out var flat) && flat.ValueKind == JsonValueKind.Number)
                    {
                        stdVal = flat.GetInt32();
                        modVal = stdVal;
                    }
                    else continue;
                }
                if (stdVal < 0) stdVal = modVal;
                if (modVal < 0) modVal = stdVal;

                var diff = modVal - stdVal;
                var diffStr = diff > 0 ? $" [green](+{diff})[/]" : diff < 0 ? $" [red]({diff})[/]" : "";
                var barW = 10;
                var filled = Math.Clamp(modVal * barW / 20, 0, barW); // scale: 0-20 typical range
                var barColor = modVal >= 14 ? "green" : modVal >= 8 ? "yellow" : "red";
                lines.Add($"    {Markup.Escape(rusName),-18} [{barColor}]{new string('█', filled)}[/][dim]{new string('░', barW - filled)}[/] [white]{modVal}[/]{diffStr}");
            }
        }

        // ── Relationship lock (embedded in npc_core) ──
        if (npc.TryGetProperty("relationshipLock", out var rLock) && rLock.ValueKind == JsonValueKind.Object)
        {
            var rlIsLocked = rLock.TryGetProperty("isLocked", out var rlL) && rlL.ValueKind == JsonValueKind.True;
            if (rlIsLocked)
            {
                var rlCap = GetInt(rLock, "currentCap", 0);
                var rlQuest = GetStr(rLock, "breakthroughQuestId", "");
                var capInfo = rlCap != 0 ? $" (порог: {rlCap})" : "";
                if (rlQuest == "__UNBREAKABLE__")
                    lines.Add($"  [bold red]🔒 Отношение НЕОБРАТИМО ЗАБЛОКИРОВАНО[/]");
                else
                {
                    lines.Add($"  [bold yellow]🔒 Отношение заблокировано{capInfo}[/]");
                    if (!string.IsNullOrEmpty(rlQuest) && rlQuest != "_clear_")
                        lines.Add($"  [yellow]📜 Квест прорыва назначен[/]");
                }
            }
        }

        // ── Goals (embedded in npc_core) ──
        if (npc.TryGetProperty("goals", out var goals) && goals.ValueKind == JsonValueKind.Object)
        {
            var longTerm = GetStr(goals, "longTerm", "");
            var shortTerm = GetStr(goals, "shortTerm", "");
            if (!string.IsNullOrEmpty(longTerm) || !string.IsNullOrEmpty(shortTerm))
            {
                lines.Add("");
                lines.Add($"  [bold green]🎯 Цели:[/]");
                if (!string.IsNullOrEmpty(longTerm))
                    lines.Add($"    🏆 Долгосрочная: [white]{Markup.Escape(longTerm)}[/]");
                if (!string.IsNullOrEmpty(shortTerm))
                    lines.Add($"    ⚡ Краткосрочная: [white]{Markup.Escape(shortTerm)}[/]");
            }
        }
        var plans = GetStr(npc, "plans", "");
        if (!string.IsNullOrEmpty(plans))
            lines.Add($"    📝 План: [dim]{Markup.Escape(plans)}[/]");

        // ── Personal quests (embedded in npc_core) ──
        if (npc.TryGetProperty("personalQuests", out var pQuests) && pQuests.ValueKind == JsonValueKind.Array && pQuests.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold gold1]📜 Личные квесты:[/]");
            foreach (var q in pQuests.EnumerateArray())
            {
                var qName = GetStr(q, "questName", GetStr(q, "name", "?"));
                var qStatus = GetStr(q, "status", "");
                var qDesc = GetStr(q, "description", "");
                var qBg = GetStr(q, "questBackground", "");
                var qRewards = GetStr(q, "rewards", "");
                var qFail = GetStr(q, "failureConsequences", "");
                var qColor = qStatus.ToLowerInvariant() switch
                {
                    "active" or "активен" => "yellow",
                    "completed" or "завершён" => "green",
                    "failed" or "провален" => "red",
                    _ => "white"
                };
                lines.Add($"    📜 [bold {qColor}]{Markup.Escape(qName)}[/] [{qColor}]({Markup.Escape(qStatus)})[/]");
                if (!string.IsNullOrEmpty(qDesc))
                    lines.Add($"      [white]{Markup.Escape(qDesc)}[/]");
                if (!string.IsNullOrEmpty(qBg))
                    lines.Add($"      [dim italic]Предпосылка: {Markup.Escape(qBg)}[/]");
                // Objectives
                if (q.TryGetProperty("objectives", out var objs) && objs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in objs.EnumerateArray())
                    {
                        var objDesc = GetStr(obj, "description", "");
                        var objSt = GetStr(obj, "status", "");
                        var objIcon = objSt.ToLowerInvariant() switch
                        {
                            "completed" or "завершён" => "[green]✓[/]",
                            "failed" or "провален" => "[red]✗[/]",
                            _ => "[yellow]○[/]"
                        };
                        if (!string.IsNullOrEmpty(objDesc))
                            lines.Add($"      {objIcon} {Markup.Escape(objDesc)}");
                    }
                }
                if (!string.IsNullOrEmpty(qRewards))
                    lines.Add($"      [green]Награда: {Markup.Escape(qRewards)}[/]");
                if (!string.IsNullOrEmpty(qFail))
                    lines.Add($"      [red]При провале: {Markup.Escape(qFail)}[/]");
            }
        }

        // ── Current activity (embedded in npc_core) ──
        if (npc.TryGetProperty("currentActivity", out var curAct) && curAct.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(curAct, "activityName", GetStr(curAct, "name", ""));
            if (!string.IsNullOrEmpty(actName))
            {
                lines.Add("");
                lines.Add($"  [bold yellow]🏃 Текущая активность:[/]");
                var actDesc = GetStr(curAct, "description", "");
                lines.Add($"    ⚡ [white]{Markup.Escape(actName)}[/]");
                if (!string.IsNullOrEmpty(actDesc))
                    lines.Add($"      [dim]{Markup.Escape(actDesc)}[/]");
                var totalTime = GetInt(curAct, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(curAct, "timeSpentMinutes", 0);
                var curStep = GetInt(curAct, "currentStepNumber", 0);
                var totalSteps = GetInt(curAct, "totalStepsInActivity", 0);
                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var filled = pct / 10;
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс: [{barColor}]{new string('█', filled)}[/][dim]{new string('░', 10 - filled)}[/] {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
                if (totalSteps > 0)
                    lines.Add($"      Шаг: [yellow]{curStep}[/]/{totalSteps}");
            }
        }

        // ── Completed activities ──
        if (npc.TryGetProperty("completedActivities", out var compActs) && compActs.ValueKind == JsonValueKind.Array && compActs.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold dim]✅ Завершённые активности:[/]");
            foreach (var ca in compActs.EnumerateArray())
            {
                if (ca.ValueKind == JsonValueKind.String)
                {
                    lines.Add($"    • [dim]{Markup.Escape(ca.GetString() ?? "")}[/]");
                }
                else if (ca.ValueKind == JsonValueKind.Object)
                {
                    var caName = GetStr(ca, "activityName", GetStr(ca, "name", "?"));
                    var caResult = GetStr(ca, "result", GetStr(ca, "outcome", ""));
                    var caLine = $"    • [dim]{Markup.Escape(caName)}[/]";
                    if (!string.IsNullOrEmpty(caResult)) caLine += $" — [dim italic]{Markup.Escape(caResult)}[/]";
                    lines.Add(caLine);
                }
            }
        }

        // ── Faction affiliations (embedded in npc_core) ──
        if (npc.TryGetProperty("factionAffiliations", out var fAff) && fAff.ValueKind == JsonValueKind.Array && fAff.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold blue]🏛️ Фракции:[/]");
            foreach (var fa in fAff.EnumerateArray())
            {
                var fName = GetStr(fa, "factionName", GetStr(fa, "name", "?"));
                var fRank = GetStr(fa, "rank", "");
                var fBranch = GetStr(fa, "branch", "");
                var fMemStatus = GetStr(fa, "membershipStatus", "");
                var fColor = fMemStatus.ToLowerInvariant() switch
                {
                    "active" or "активен" => "green",
                    "former" or "бывший" => "grey",
                    "exiled" or "изгнан" => "red",
                    "undercover" or "под прикрытием" => "yellow",
                    "ally" or "союзник" => "cyan",
                    "enemy" or "враг" => "red",
                    _ => "white"
                };
                var line = $"    🏛️ [white]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(fRank))
                    line += $" — [{fColor}]{Markup.Escape(fRank)}[/]";
                if (!string.IsNullOrEmpty(fBranch))
                    line += $" [dim]({Markup.Escape(fBranch)})[/]";
                if (!string.IsNullOrEmpty(fMemStatus))
                    line += $" [{fColor}]({Markup.Escape(fMemStatus)})[/]";
                lines.Add(line);
            }
        }

        // ── NPC-to-NPC relationships (embedded in npc_core) ──
        if (npc.TryGetProperty("npcRelationships", out var npcRels) && npcRels.ValueKind == JsonValueKind.Array && npcRels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold steelblue1]🤝 Связи с другими НПС:[/]");
            foreach (var nr in npcRels.EnumerateArray())
            {
                var tgtName = GetStr(nr, "targetNpcName", GetStr(nr, "name", "?"));
                var relSt = GetStr(nr, "relationshipStatus", GetStr(nr, "status", ""));
                var reason = GetStr(nr, "statusReason", GetStr(nr, "reason", ""));
                var relIcon = relSt.ToLowerInvariant() switch
                {
                    "ally" or "союзник" => "🤝",
                    "friend" or "друг" => "😊",
                    "rival" or "соперник" => "⚔",
                    "enemy" or "враг" => "💀",
                    "subordinate" or "подчинённый" => "👇",
                    "superior" or "начальник" => "👆",
                    "family" or "семья" => "👨‍👩‍👧",
                    _ => "👤"
                };
                var line = $"    {relIcon} [white]{Markup.Escape(tgtName)}[/] — [yellow]{Markup.Escape(relSt)}[/]";
                if (!string.IsNullOrEmpty(reason))
                    line += $" [dim]({Markup.Escape(reason)})[/]";
                lines.Add(line);
            }
        }

        // ── Embedded fate cards (in npc_core) ──
        if (npc.TryGetProperty("fateCards", out var embFate) && embFate.ValueKind == JsonValueKind.Array && embFate.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold mediumpurple2]🃏 Карты Судьбы:[/]");
            foreach (var fc in embFate.EnumerateArray())
            {
                var fcName = GetStr(fc, "name", "?");
                var fcDesc = GetStr(fc, "description", "");
                var fcUnlocked = fc.TryGetProperty("isUnlocked", out var uv) && uv.ValueKind == JsonValueKind.True;
                var lockIcon = fcUnlocked ? "[green]🔓[/]" : "[red]🔒[/]";
                lines.Add($"    {lockIcon} [mediumpurple2]{Markup.Escape(fcName)}[/]");
                if (fcUnlocked && !string.IsNullOrEmpty(fcDesc))
                    lines.Add($"      [white]{Markup.Escape(fcDesc)}[/]");
                else if (!fcUnlocked)
                {
                    // Show unlock conditions as a hint
                    if (fc.TryGetProperty("unlockConditions", out var uc) && uc.ValueKind == JsonValueKind.Object)
                    {
                        var reqRel = GetInt(uc, "requiredRelationshipLevel", 0);
                        var plotCond = GetStr(uc, "plotConditionDescription", "");
                        if (reqRel > 0)
                            lines.Add($"      [dim]Требуется отношение: {reqRel}+[/]");
                        if (!string.IsNullOrEmpty(plotCond))
                            lines.Add($"      [dim]Условие: {Markup.Escape(plotCond)}[/]");
                    }
                }
                // Show rewards for unlocked cards
                if (fcUnlocked && fc.TryGetProperty("rewards", out var rw) && rw.ValueKind == JsonValueKind.Object)
                {
                    var rwDesc = GetStr(rw, "description", "");
                    if (!string.IsNullOrEmpty(rwDesc))
                        lines.Add($"      [green]Награда: {Markup.Escape(rwDesc)}[/]");
                }
            }
        }

        // ── Embedded skills (activeSkills/passiveSkills in npc_core) ──
        if (npc.TryGetProperty("activeSkills", out var aSkills) && aSkills.ValueKind == JsonValueKind.Array && aSkills.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold cyan]⚔ Активные навыки:[/]");
            foreach (var s in aSkills.EnumerateArray())
            {
                var sn = s.ValueKind == JsonValueKind.String ? (s.GetString() ?? "") : GetStr(s, "name", "?");
                var sDesc = s.ValueKind == JsonValueKind.Object ? GetStr(s, "description", "") : "";
                lines.Add($"    • [cyan]{Markup.Escape(sn)}[/]");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim]{Markup.Escape(sDesc)}[/]");
            }
        }
        if (npc.TryGetProperty("passiveSkills", out var pSkills) && pSkills.ValueKind == JsonValueKind.Array && pSkills.GetArrayLength() > 0)
        {
            lines.Add($"  [bold dim]🛡️ Пассивные навыки:[/]");
            foreach (var s in pSkills.EnumerateArray())
            {
                var sn = s.ValueKind == JsonValueKind.String ? (s.GetString() ?? "") : GetStr(s, "name", "?");
                var sDesc = s.ValueKind == JsonValueKind.Object ? GetStr(s, "description", "") : "";
                lines.Add($"    • [white]{Markup.Escape(sn)}[/]");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim]{Markup.Escape(sDesc)}[/]");
            }
        }

        // ── Embedded inventory & equipment (in npc_core) ──
        if (npc.TryGetProperty("inventory", out var embInv) && embInv.ValueKind == JsonValueKind.Array && embInv.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold orange3]🎒 Инвентарь (встроенный):[/]");
            foreach (var it in embInv.EnumerateArray())
            {
                var iName = GetStr(it, "name", "?");
                var iQty = GetStr(it, "quantity", GetStr(it, "count", ""));
                var iEquipped = it.TryGetProperty("equipped", out var ieq) && ieq.ValueKind == JsonValueKind.True;
                var line = iEquipped
                    ? $"    ⚔ [green]{Markup.Escape(iName)}[/] [green](экипировано)[/]"
                    : $"    • [white]{Markup.Escape(iName)}[/]";
                if (!string.IsNullOrEmpty(iQty) && iQty != "1")
                    line += $" ×{Markup.Escape(iQty)}";
                lines.Add(line);
            }
        }
        if (npc.TryGetProperty("equippedItems", out var embEq) && embEq.ValueKind == JsonValueKind.Object)
        {
            var hasEquip = false;
            foreach (var slot in embEq.EnumerateObject())
            {
                if (slot.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
                if (!hasEquip)
                {
                    lines.Add($"  [bold orange3]🛡️ Экипировка:[/]");
                    hasEquip = true;
                }
                var slotVal = slot.Value.ValueKind == JsonValueKind.String ? (slot.Value.GetString() ?? "") : slot.Value.ToString();
                lines.Add($"    • [dim]{Markup.Escape(NpcFieldToRussian(slot.Name))}:[/] [white]{Markup.Escape(slotVal)}[/]");
            }
        }

        // ── Embedded masks (in npc_core) ──
        var activeMask = GetStr(npc, "activeMaskId", "");
        if (npc.TryGetProperty("masks", out var embMasks) && embMasks.ValueKind == JsonValueKind.Array && embMasks.GetArrayLength() > 0 && debugMode)
        {
            lines.Add("");
            lines.Add($"  [bold red]🎭 Маски (персоны):[/]");
            foreach (var m in embMasks.EnumerateArray())
            {
                var mId = GetStr(m, "maskId", "");
                var mName = GetStr(m, "maskName", GetStr(m, "name", "?"));
                var mArch = GetStr(m, "personalityArchetype", "");
                var mAtt = GetStr(m, "attitude", "");
                var isActive = !string.IsNullOrEmpty(activeMask) && mId == activeMask;
                var activeTag = isActive ? " [green]● АКТИВНА[/]" : "";
                lines.Add($"    🎭 [red]{Markup.Escape(mName)}[/]{activeTag}");
                if (!string.IsNullOrEmpty(mArch))
                    lines.Add($"      [dim]Архетип: {Markup.Escape(mArch)}[/]");
                if (!string.IsNullOrEmpty(mAtt))
                    lines.Add($"      [dim]Отношение: {Markup.Escape(mAtt)}[/]");
            }
        }

        // ── Guardian-specific fields (personalityProfile, relationshipData, questManagement, gachaSystem) ──
        var guardianDomain = GetStr(npc, "domain", "");
        if (!string.IsNullOrEmpty(guardianDomain))
            lines.Add($"  🔮 Домен: [magenta1]{Markup.Escape(guardianDomain)}[/]");

        if (npc.TryGetProperty("personalityProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            var ppArch = GetStr(pp, "archetype", "");
            var ppSpeech = GetStr(pp, "speechPattern", "");
            if (!string.IsNullOrEmpty(ppArch))
                lines.Add($"  🧠 Архетип: [magenta1]{Markup.Escape(ppArch)}[/]");
            if (!string.IsNullOrEmpty(ppSpeech))
                lines.Add($"  🗣️ Стиль речи: [white]{Markup.Escape(ppSpeech)}[/]");
            if (pp.TryGetProperty("coreValues", out var cv) && cv.ValueKind == JsonValueKind.Array)
            {
                var vals = new List<string>();
                foreach (var v in cv.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.String) vals.Add(v.GetString() ?? "");
                if (vals.Count > 0)
                    lines.Add($"  💎 Ценности: [white]{Markup.Escape(string.Join(", ", vals))}[/]");
            }
        }

        var guardianReputation = GetInt(npc, "reputation", 0);
        if (npc.TryGetProperty("relationshipData", out var rd) && rd.ValueKind == JsonValueKind.Object)
        {
            var curRep = GetInt(rd, "currentReputation", int.MinValue);
            if (curRep != int.MinValue)
            {
                guardianReputation = curRep;
                var (tierLbl, tierClr) = GetNpcRelationshipTier(curRep);
                var normalized = Math.Clamp((curRep + 400) * 20 / 800, 0, 20);
                var barClr = curRep >= 251 ? "cyan" : curRep >= 101 ? "green" : curRep >= 0 ? "grey" : curRep >= -50 ? "orange1" : "red";
                lines.Add($"  ♥ Репутация: {ConsoleLayout.CreateBar(normalized, 20, barClr)} [{tierClr}]{curRep} — {tierLbl}[/]");
            }
            if (rd.TryGetProperty("reputationHistory", out var rh) && rh.ValueKind == JsonValueKind.Array && rh.GetArrayLength() > 0)
            {
                lines.Add($"    [dim]История ({rh.GetArrayLength()}):[/]");
                foreach (var e in rh.EnumerateArray())
                {
                    var change = GetInt(e, "change", 0);
                    var reason = GetStr(e, "reason", "");
                    if (!string.IsNullOrEmpty(reason))
                    {
                        var chStr = change > 0 ? $"[green]+{change}[/]" : change < 0 ? $"[red]{change}[/]" : "[dim]±0[/]";
                        lines.Add($"    {chStr} [dim]{Markup.Escape(reason)}[/]");
                    }
                }
            }
        }

        if (npc.TryGetProperty("questManagement", out var qm) && qm.ValueKind == JsonValueKind.Object)
        {
            if (qm.TryGetProperty("availableQuests", out var aq) && aq.ValueKind == JsonValueKind.Array && aq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add($"  [bold gold1]📜 Доступные квесты:[/]");
                foreach (var q in aq.EnumerateArray())
                {
                    var qStr = q.ValueKind == JsonValueKind.String ? (q.GetString() ?? "") : GetStr(q, "name", GetStr(q, "questId", "?"));
                    lines.Add($"    • [yellow]{Markup.Escape(qStr)}[/]");
                }
            }
            if (qm.TryGetProperty("completedQuests", out var cq) && cq.ValueKind == JsonValueKind.Array && cq.GetArrayLength() > 0)
            {
                lines.Add($"  [dim]✓ Завершённые квесты: {cq.GetArrayLength()}[/]");
            }
        }

        if (npc.TryGetProperty("gachaSystem", out var gs) && gs.ValueKind == JsonValueKind.Object)
        {
            var chargesPerReturn = gs.TryGetProperty("chargesPerReturn", out var cpr) && cpr.ValueKind == JsonValueKind.Number && cpr.TryGetInt32(out var parsedCharges)
                ? parsedCharges
                : GuardianGachaChargeRules.GetChargesPerReturnForReputation(guardianReputation);
            var chargesUsedThisReturn = gs.TryGetProperty("chargesUsedThisReturn", out var cur) && cur.ValueKind == JsonValueKind.Number && cur.TryGetInt32(out var parsedUsed)
                ? GuardianGachaChargeRules.ClampUsedCharges(parsedUsed, chargesPerReturn)
                : 0;
            var remainingCharges = Math.Max(0, chargesPerReturn - chargesUsedThisReturn);

            if (chargesPerReturn <= 0)
            {
                lines.Add("  🎰 Вытягивание реликвий: [red]заблокировано репутацией[/]");
            }
            else
            {
                lines.Add($"  🎰 Попытки в этом возвращении: [yellow]{remainingCharges}[/]/[white]{chargesPerReturn}[/]");
                if (remainingCharges <= 0)
                    lines.Add("    [dim]Лимит у этого Хранителя исчерпан до следующего возвращения из смертной жизни.[/]");
            }
        }

        // ── Weight info ──
        var maxWeight = GetStr(npc, "maxWeight", "");
        var totalWeight = GetStr(npc, "totalWeight", "");
        if (!string.IsNullOrEmpty(maxWeight) || !string.IsNullOrEmpty(totalWeight))
        {
            var wLine = "  ⚖️ Вес:";
            if (!string.IsNullOrEmpty(totalWeight))
                wLine += $" [white]{Markup.Escape(totalWeight)}[/]";
            if (!string.IsNullOrEmpty(maxWeight))
                wLine += $"/[dim]{Markup.Escape(maxWeight)} кг[/]";
            var isOver = npc.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
            if (isOver)
                wLine += " [bold red]⚠ ПЕРЕГРУЖЕН[/]";
            lines.Add(wLine);
        }

        // Show remaining non-core string/number/bool properties (catch-all for unknown fields)
        var coreProps = new HashSet<string> { "name", "npcId", "id", "shortDescription", "description",
            "appearance", "currentLocation", "relationshipLevel", "race", "role", "occupation", "status",
            "lastInteraction", "progressionType", "level", "experience", "experienceForNextLevel",
            "playerCompanionDirective", "image_prompt", "currentHealthPercentage", "maxHealthPercentage",
            "appearanceDescription", "raceDescription", "classDescription", "history", "worldview",
            "attitude", "culturalLayer", "culturalStance", "personalityArchetype", "personalityTraits",
            "characteristics", "relationshipLock", "goals", "plans", "personalQuests", "currentActivity",
            "completedActivities", "factionAffiliations", "npcRelationships", "fateCards",
            "activeSkills", "passiveSkills", "inventory", "equippedItems", "masks", "activeMaskId",
            "customStates", "maxWeight", "totalWeight", "isOverloaded", "progressionTrackers",
            "personalityProfile", "guardianId", "domain", "relationshipData", "questManagement",
            "gachaSystem", "rarity", "age", "class" };
        foreach (var prop in npc.EnumerateObject())
        {
            if (coreProps.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString() ?? "";
                if (val.Length > 0)
                    lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [white]{Markup.Escape(val)}[/]");
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [yellow]{prop.Value}[/]");
            }
            else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var boolColor = prop.Value.ValueKind == JsonValueKind.True ? "green" : "red";
                var boolText = prop.Value.ValueKind == JsonValueKind.True ? "да" : "нет";
                lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [{boolColor}]{boolText}[/]");
            }
        }

        if (debugMode)
        {
            var lastInt = GetStr(npc, "lastInteraction", "");
            if (!string.IsNullOrEmpty(lastInt))
                lines.Add($"  🕐 Последнее взаимодействие: [dim]{Markup.Escape(lastInt)}[/]");

            if (!string.IsNullOrEmpty(npcId))
                lines.Add($"  🆔 ID: [dim]{Markup.Escape(npcId)}[/]");
        }

        // ── Отношения (npc_relationships) ──
        RenderNpcRelationships(lines, relDoc, npcId, originalName, debugMode);

        // ── Цели (npc_goals) ──
        RenderNpcGoals(lines, goalDoc, npcId, originalName, debugMode);

        // ── Активность (npc_activities) ──
        RenderNpcActivities(lines, actDoc, npcId, originalName, debugMode);

        // ── Инвентарь (npc_inventory) ──
        RenderNpcInventory(lines, invDoc, npcId, originalName, debugMode);

        // ── Эффекты (npc_effects) ──
        RenderNpcEffects(lines, effDoc, npcId, originalName, debugMode);

        // ── Навыки (npc_skills) ──
        RenderNpcSkills(lines, skillDoc, npcId, originalName, debugMode);

        // ── Черты характера (npc_personality) ──
        RenderNpcPersonality(lines, persDoc, npcId, originalName, debugMode);

        // ── Воспоминания (npc_memory) ──
        RenderNpcMemories(lines, memDoc, npcId, originalName, debugMode);

        // ── Дневник / Мысли (npc_journals) ──
        RenderNpcJournals(lines, jourDoc, npcId, originalName, debugMode);

        // ── Карты судьбы (npc_fate_cards) — разблокированные видны игроку ──
        RenderNpcFateCards(lines, fateDoc, npcId, originalName, debugMode);

        // ── Особые состояния (npc_custom_states) ──
        RenderNpcCustomStates(lines, customDoc, npcId, originalName);

        // ── Debug-only sections ──
        if (debugMode)
        {
            RenderNpcMasks(lines, maskDoc, npcId, originalName);
        }

        if (lines.Count > 0)
            content.AddRow(new Markup(string.Join("\n", lines)));

        AnsiConsole.Write(new Panel(content)
        {
            Header = new PanelHeader($" 👤 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(debugMode ? Color.Magenta1 : Color.Purple),
            Padding = new Padding(2, 1),
            Expand = true
        });
        await ShowNpcDetailActions(npc, originalName, string.IsNullOrWhiteSpace(npcTradeBlockedReason) && !string.IsNullOrWhiteSpace(merchantProfile), invDoc);
    }

    private async Task ShowNpcDetailActions(JsonElement npc, string npcName, bool tradeAvailable, JsonDocument? invDoc)
    {
        var imagePrompt = GetStr(npc, "image_prompt", "");
        var npcImageKey = GetPrimaryNpcId(npc);
        if (string.IsNullOrWhiteSpace(npcImageKey))
            npcImageKey = npcName;
        var hasImageSupport = _imageService != null && !string.IsNullOrWhiteSpace(imagePrompt);
        var npcId = GetPrimaryNpcId(npc);
        var npcInventoryDisplay = invDoc != null ? BuildNpcInventoryDisplay(invDoc, npcId, npcName) : new NpcInventoryDisplay();
        var hasInspectableItems = npcInventoryDisplay.Items.Count > 0;

        if (!tradeAvailable && !hasImageSupport && !hasInspectableItems)
        {
            WaitForKey();
            return;
        }

        while (true)
        {
            var actions = new List<string>();
            if (tradeAvailable)
                actions.Add("🛒 Торговать");
            if (hasInspectableItems)
                actions.Add("📦 Осмотреть предметы");

            if (hasImageSupport)
            {
                var hasImage = _imageService!.EntityImageExists("npc", npcImageKey);
                actions.Add("🖼 Показать изображение");
                if (hasImage)
                    actions.Add("♻ Пересоздать изображение");
            }

            actions.Add("← Назад");

            var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(actions));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Торговать"))
            {
                if (!string.IsNullOrWhiteSpace(npcId))
                    await ShowNpcTradePanel(npcId);
                return;
            }

            if (action.Contains("Осмотреть предметы"))
            {
                await ShowNpcHeldItemInspector(npc, npcName, invDoc);
                continue;
            }

            if (!hasImageSupport)
                continue;

            var imageExists = _imageService!.EntityImageExists("npc", npcImageKey);
            if (action.Contains("Пересоздать") && imageExists)
            {
                await RegenerateEntityImageAsync(imagePrompt, "npc", npcImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Показать"))
            {
                await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, "npc", npcImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }
        }
    }

    private async Task ShowNpcHeldItemInspector(JsonElement npc, string npcName, JsonDocument? invDoc)
    {
        if (invDoc == null)
        {
            ShowEmptyPanel("Инвентарь NPC", "Инвентарь NPC недоступен");
            WaitForKey();
            return;
        }

        var npcId = GetPrimaryNpcId(npc);

        while (true)
        {
            var display = BuildNpcInventoryDisplay(invDoc, npcId, npcName);
            if (display.IsEmpty || display.Items.Count == 0)
            {
                ShowEmptyPanel("Инвентарь NPC", "У NPC нет предметов для осмотра");
                WaitForKey();
                return;
            }

            var choices = new List<string>();
            foreach (var npcItem in display.Items)
            {
                var choiceItemName = GetNodeStr(npcItem.Data, "name", "?");
                var qty = GetNodeStr(npcItem.Data, "quantity", GetNodeStr(npcItem.Data, "count", ""));
                var itemType = GetNodeStr(npcItem.Data, "type", GetNodeStr(npcItem.Data, "category", ""));
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(itemType))
                    meta.Add(itemType);
                if (!string.IsNullOrWhiteSpace(qty) && qty != "1")
                    meta.Add($"×{qty}");
                if (npcItem.IsEquipped)
                    meta.Add("экипировано");

                choices.Add(meta.Count > 0
                    ? ConsoleLayout.PlainChoiceLabel($"📦 {choiceItemName}", string.Join(" • ", meta))
                    : $"📦 {choiceItemName}");
            }

            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold orange3]🎒 Предметы NPC[/]")
                    .HighlightStyle(new Style(Color.Orange3))
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= display.Items.Count)
                continue;

            var selectedItem = display.Items[selIdx];
            var selectedItemName = GetNodeStr(selectedItem.Data, "name", "?");
            var readOnlyStatus = selectedItem.IsEquipped ? "⚔ Экипировано у NPC" : "👤 Находится у NPC";

            await ShowItemDetailPanel(
                selectedItem.Key,
                selectedItemName,
                JsonObjectToElement(selectedItem.Data),
                null,
                readOnly: true,
                readOnlyStatusOverride: readOnlyStatus,
                allowInventorySidecars: false);
        }
    }

    private async Task ShowNpcTradePanel(string npcId)
    {
        if (_npcTradeService == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Сервис торговли НПС недоступен.[/]");
            WaitForKey();
            return;
        }

        while (true)
        {
            var view = await _npcTradeService.EnsureTradeInventoryAsync(npcId);
            if (view == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (view.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var totalOffers = view.Offers.Count;
            var availableOffers = view.Offers.Count(offer => !offer.SoldOut);
            var headerLines = new List<string>
            {
                $"[bold purple]🛒 Торговля с {Markup.Escape(view.NpcName)}[/]",
                $"[dim]Профиль: {Markup.Escape(view.MerchantProfileDisplay)} • Торговля NPC: {view.NpcTrade} • Торговля игрока: {view.PlayerTrade}[/]",
                $"[dim]Отношение: {view.RelationshipLevel} • Деньги: {view.CurrentMoney}[/]",
                $"[dim]Товаров в витрине: {availableOffers}/{totalOffers} доступно • {Markup.Escape(DescribeNpcTradeRefresh(view))}[/]"
            };

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", headerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Purple),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите раздел:[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices("🛍 Купить товары", "💰 Продать товары", "← Назад"));

            if (choice.Contains("Назад"))
                return;

            if (choice.Contains("Купить"))
            {
                await ShowNpcBuyMenu(npcId);
                await _stateManager.RefreshGameStateAsync();
                AnsiConsole.Clear();
                continue;
            }

            if (choice.Contains("Продать"))
            {
                await ShowNpcSellMenu(npcId);
                await _stateManager.RefreshGameStateAsync();
                AnsiConsole.Clear();
            }
        }
    }

    private async Task ShowNpcBuyMenu(string npcId)
    {
        if (_npcTradeService == null)
            return;

        while (true)
        {
            var refreshedView = await _npcTradeService.EnsureTradeInventoryAsync(npcId);
            if (refreshedView == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (refreshedView.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(refreshedView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var displayOffers = refreshedView.Offers
                .OrderBy(offer => offer.SoldOut)
                .ThenBy(offer => GetNpcTradeItemClassSortKey(offer.ItemData))
                .ThenBy(offer => GetJsonNodeString(offer.ItemData["group"]) ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(offer => offer.Price)
                .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var offerChoices = BuildUniqueChoiceOptions(displayOffers, offer =>
            {
                var soldTag = offer.SoldOut ? "РАСПРОДАНО" : "";
                return ConsoleLayout.PlainChoiceLabel(
                    $"📦 {offer.Name}",
                    GetNpcTradeChoiceMeta(offer.ItemData),
                    offer.Rarity,
                    $"💰 {offer.Price}",
                    soldTag);
            });
            var choices = offerChoices.Select(item => item.Label).ToList();
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Покупка товаров[/] [dim](доступно: {displayOffers.Count(offer => !offer.SoldOut)}/{displayOffers.Count} • деньги: {refreshedView.CurrentMoney} • {Markup.Escape(DescribeNpcTradeRefresh(refreshedView))})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(20)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedOffer = offerChoices.FirstOrDefault(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;
            if (selectedOffer == null)
                return;

            var offer = selectedOffer;
            var canBuy = !offer.SoldOut && refreshedView.CurrentMoney >= offer.Price;
            var decision = ShowNpcTradeBuyPreview(offer, refreshedView.CurrentMoney, canBuy);
            if (decision != GuardianTradeBuyDecision.Buy)
                continue;

            var result = await _npcTradeService.BuyAsync(npcId, offer.SlotId);
            AnsiConsole.MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private GuardianTradeBuyDecision ShowNpcTradeBuyPreview(Services.NpcTradeService.NpcTradeOffer offer, int currentMoney, bool canBuy)
    {
        using var itemDoc = JsonDocument.Parse(offer.ItemData.ToJsonString());
        var lines = BuildInventoryItemDetailLines(offer.Name, itemDoc.RootElement);
        lines.Insert(1, $"  💰 Цена: [yellow]{offer.Price}[/]");
        lines.Insert(2, $"  🏪 Профиль торговца: [cyan]{Markup.Escape(GetNpcMerchantProfileDisplay(offer.MerchantProfile))}[/]");
        lines.Insert(3, $"  💰 У вас сейчас: [gold1]{currentMoney}[/]");

        if (offer.SoldOut)
            lines.Insert(4, "  [red]Статус витрины: слот уже распродан в текущем ассортименте.[/]");
        else if (currentMoney < offer.Price)
            lines.Insert(4, "  [yellow]Статус покупки: пока не хватает денег для покупки.[/]");

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Товар торговца ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var actions = new List<string>();
        if (canBuy)
            actions.Add("🛍 Купить");
        actions.Add("← Назад к витрине");

        var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(actions));

        return action.Contains("Купить", StringComparison.OrdinalIgnoreCase)
            ? GuardianTradeBuyDecision.Buy
            : GuardianTradeBuyDecision.Back;
    }

    private async Task ShowNpcSellMenu(string npcId)
    {
        if (_npcTradeService == null)
            return;

        while (true)
        {
            var tradeView = await _npcTradeService.EnsureTradeInventoryAsync(npcId);
            if (tradeView == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (tradeView.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(tradeView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var offers = await _npcTradeService.GetSellableItemsAsync(npcId);
            if (offers.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]В инвентаре нет товаров смертной жизни, доступных для продажи.[/]");
                WaitForKey();
                return;
            }

            var displayOffers = offers
                .OrderBy(offer => GetNpcTradeItemClassSortKey(offer.ItemData))
                .ThenBy(offer => GetJsonNodeString(offer.ItemData["group"]) ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(offer => GetRarityRank(offer.Rarity))
                .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var offerChoices = BuildUniqueChoiceOptions(displayOffers, offer =>
                ConsoleLayout.PlainChoiceLabel(
                    $"📦 {offer.Name}",
                    GetNpcTradeChoiceMeta(offer.ItemData),
                    offer.Rarity,
                    $"💰 {offer.Price}"));
            var choices = offerChoices.Select(item => item.Label).ToList();
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Продажа товаров[/] [dim](обычные товары смертной жизни; без реликвий души и квестовых предметов)[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(20)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedOffer = offerChoices.FirstOrDefault(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;
            if (selectedOffer == null)
                return;

            var offer = selectedOffer;
            if (!ShowNpcTradeSellPreview(offer))
                continue;

            var result = await _npcTradeService.SellAsync(npcId, offer.ItemId);
            AnsiConsole.MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private bool ShowNpcTradeSellPreview(Services.NpcTradeService.NpcSellOffer offer)
    {
        using var itemDoc = JsonDocument.Parse(offer.ItemData.ToJsonString());
        var lines = BuildInventoryItemDetailLines(offer.Name, itemDoc.RootElement);
        lines.Insert(1, $"  💰 Цена продажи: [yellow]{offer.Price}[/]");
        lines.Insert(2, "  [dim]Панель принимает только обычные товары смертной жизни. Квестовые предметы и реликвии души исключены.[/]");

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 💰 Продажа товара ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("💰 Продать", "← Назад к списку"));

        return action.Contains("Продать", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeNpcTradeRefresh(Services.NpcTradeService.NpcTradeView view)
    {
        var remaining = Math.Max(0, view.RefreshAfterWorldTimeMinutes - view.CurrentWorldTimeMinutes);
        var generatedAgo = Math.Max(0, view.CurrentWorldTimeMinutes - view.GeneratedAtWorldTimeMinutes);

        return remaining switch
        {
            0 => "ассортимент готов к обновлению",
            _ => $"обновление через {FormatWorldMinutesSpan(remaining)} (витрина возрастом {FormatWorldMinutesSpan(generatedAgo)})"
        };
    }

    private static string FormatWorldMinutesSpan(int totalMinutes)
    {
        if (totalMinutes <= 0)
            return "меньше 1 дня";

        var days = totalMinutes / 1440;
        var hours = (totalMinutes % 1440) / 60;
        if (days <= 0)
            return $"{Math.Max(1, hours)} ч";
        if (hours <= 0)
            return $"{days} д";
        return $"{days} д {hours} ч";
    }

    private static List<(string Label, T Value)> BuildUniqueChoiceOptions<T>(IEnumerable<T> values, Func<T, string> labelFactory)
        where T : class
    {
        var result = new List<(string Label, T Value)>();
        var seenCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            var baseLabel = labelFactory(value);
            seenCounts.TryGetValue(baseLabel, out var count);
            count++;
            seenCounts[baseLabel] = count;
            var label = count == 1 ? baseLabel : $"{baseLabel} #{count}";
            result.Add((label, value));
        }

        return result;
    }

    private static string GetNpcTradeChoiceMeta(JsonObject itemData)
    {
        var parts = new List<string>();
        var tradeItemClass = GetJsonNodeString(itemData["tradeItemClass"]);
        if (!string.IsNullOrWhiteSpace(tradeItemClass))
            parts.Add(Services.NpcTradeService.GetTradeItemClassDisplayName(tradeItemClass));

        var group = GetJsonNodeString(itemData["group"]);
        if (!string.IsNullOrWhiteSpace(group))
            parts.Add(group);

        var type = GetJsonNodeString(itemData["type"]);
        if (!string.IsNullOrWhiteSpace(type) && !parts.Contains(type, StringComparer.OrdinalIgnoreCase))
            parts.Add(type);

        return string.Join(" • ", parts);
    }

    private static int GetNpcTradeItemClassSortKey(JsonObject itemData)
    {
        var tradeItemClass = GetJsonNodeString(itemData["tradeItemClass"]);
        return tradeItemClass switch
        {
            "Functional" => 0,
            "Material" => 1,
            "FlavorOrUtility" => 2,
            _ => 3
        };
    }

    private static string? GetJsonNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str))
                return str;
            return value.ToJsonString().Trim('"');
        }

        return node.ToJsonString();
    }

    private async Task<(string locationId, string locationName)> ReadCurrentLocationIdentityAsync()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
        if (doc == null)
            return ("", "");

        var root = doc.RootElement;
        if (root.TryGetProperty("currentLocationData", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
            root = wrapped;

        return (GetStr(root, "locationId", ""), GetStr(root, "name", ""));
    }

    private static bool NpcTradeAvailableHere(JsonElement npc, string currentLocationId, string currentLocationName)
    {
        var npcLocationId = GetStr(npc, "currentLocationId", "");
        var npcLocationName = GetStr(npc, "currentLocation", "");
        return
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetNpcMerchantProfile(JsonElement npc)
    {
        var explicitProfile = "";
        if (npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object)
            explicitProfile = GetStr(tradeState, "merchantProfile", "");

        return Services.NpcTradeService.ResolveMerchantProfileCode(
            explicitProfile,
            GetStr(npc, "role", ""),
            GetStr(npc, "occupation", ""),
            GetStr(npc, "class", ""),
            GetStr(npc, "name", ""));
    }

    private static string GetNpcMerchantProfileDisplay(JsonElement npc)
    {
        var profile = GetNpcMerchantProfile(npc);
        return GetNpcMerchantProfileDisplay(profile);
    }

    private static string GetNpcMerchantProfileDisplay(string? merchantProfile) =>
        Services.NpcTradeService.GetMerchantProfileDisplayName(merchantProfile);

    private static string? GetNpcTradeBlockedReason(JsonElement npc, string currentLocationId, string currentLocationName)
    {
        var merchantProfile = GetNpcMerchantProfile(npc);
        if (string.IsNullOrWhiteSpace(merchantProfile))
            return null;

        if (!NpcTradeAvailableHere(npc, currentLocationId, currentLocationName))
            return "Доступна только в текущей локации торговца.";

        if (npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object)
        {
            if (!tradeState.TryGetProperty("canTrade", out var canTradeNode) ||
                (canTradeNode.ValueKind != JsonValueKind.True && canTradeNode.ValueKind != JsonValueKind.False))
                return "Локальная торговля включается только через tradeState.canTrade = true.";

            var canTrade = canTradeNode.ValueKind == JsonValueKind.True;
            if (!canTrade)
                return GetStr(tradeState, "tradeBlockedReason", "Торговля сейчас недоступна.");
        }
        else
        {
            return "Локальная торговля включается только через tradeState.canTrade = true.";
        }

        return null;
    }

    private List<string> BuildInventoryItemDetailLines(string name, JsonElement item)
    {
        var lines = new List<string> { $"[bold yellow]📦 {Markup.Escape(name)}[/]", "" };

        var desc = GetStr(item, "description", "");
        if (!string.IsNullOrEmpty(desc)) { lines.Add($"[white]{Markup.Escape(desc)}[/]"); lines.Add(""); }
        var type = GetStr(item, "type", "");
        if (!string.IsNullOrEmpty(type))
            lines.Add($"  📋 Тип: [cyan]{Markup.Escape(type)}[/]");
        var rarity = GetStr(item, "quality", GetStr(item, "rarity", ""));
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");
        var weight = GetStr(item, "weight", "");
        if (!string.IsNullOrEmpty(weight))
            lines.Add($"  ⚖ Вес: [white]{Markup.Escape(weight)} кг[/]");
        var slot = GetStr(item, "equipmentSlot", GetStr(item, "slot", GetStr(item, "equipSlot", "")));
        if (!string.IsNullOrEmpty(slot))
            lines.Add($"  📌 Слот: [cyan]{Markup.Escape(slot)}[/]");
        var group = GetStr(item, "group", "");
        if (!string.IsNullOrEmpty(group))
            lines.Add($"  📂 Группа: [white]{Markup.Escape(group)}[/]");
        var tradeItemClass = GetStr(item, "tradeItemClass", "");
        if (!string.IsNullOrEmpty(tradeItemClass))
            lines.Add($"  🧭 Класс товара: [white]{Markup.Escape(Services.NpcTradeService.GetTradeItemClassDisplayName(tradeItemClass))}[/]");
        var count = GetStr(item, "count", "");
        if (!string.IsNullOrEmpty(count) && count != "0")
            lines.Add($"  🔢 Количество: [white]{Markup.Escape(count)}[/]");
        var capacity = GetStr(item, "capacity", "");
        if (!string.IsNullOrEmpty(capacity))
            lines.Add($"  📦 Вместимость: [white]{Markup.Escape(capacity)}[/]");
        if (item.TryGetProperty("isConsumption", out var isConsumption) && isConsumption.ValueKind == JsonValueKind.True)
            lines.Add("  🧪 Расходуется при использовании");
        if (item.TryGetProperty("textContent", out var textContent) && textContent.ValueKind == JsonValueKind.Array && textContent.GetArrayLength() > 0)
            lines.Add($"  📄 Содержит записи: [white]{textContent.GetArrayLength()}[/]");

        if (item.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array && bonuses.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Бонусы:[/]");
            foreach (var b in bonuses.EnumerateArray())
            {
                if (b.ValueKind == JsonValueKind.String)
                    lines.Add($"    • [green]{Markup.Escape(b.GetString() ?? "")}[/]");
            }
        }

        if (item.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array && effects.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🔮 Эффекты:[/]");
            foreach (var e in effects.EnumerateArray())
            {
                var effectName = e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : GetStr(e, "name", GetStr(e, "effect", ""));
                if (!string.IsNullOrEmpty(effectName))
                    lines.Add($"    • [mediumpurple2]{Markup.Escape(effectName)}[/]");
            }
        }

        if (item.TryGetProperty("passiveEffects", out var passiveEffects) && passiveEffects.ValueKind == JsonValueKind.Array && passiveEffects.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]✨ Пассивные свойства:[/]");
            foreach (var p in passiveEffects.EnumerateArray())
            {
                var passive = p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.GetRawText();
                if (!string.IsNullOrEmpty(passive))
                    lines.Add($"    • [yellow]{Markup.Escape(passive)}[/]");
            }
        }

        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses) && structuredBonuses.ValueKind == JsonValueKind.Array && structuredBonuses.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Структурные бонусы:[/]");
            foreach (var sb in structuredBonuses.EnumerateArray())
            {
                var bType = GetStr(sb, "bonusType", GetStr(sb, "type", "?"));
                var bTarget = GetStr(sb, "target", "");
                var bValue = GetStr(sb, "value", "");
                var bDesc = GetStr(sb, "description", "");
                var line = $"    • [green]{Markup.Escape(bType)}[/]";
                if (!string.IsNullOrEmpty(bTarget)) line += $" → {Markup.Escape(bTarget)}";
                if (!string.IsNullOrEmpty(bValue)) line += $": [white]{Markup.Escape(bValue)}[/]";
                lines.Add(line);
                if (!string.IsNullOrEmpty(bDesc))
                    lines.Add($"      [dim]{Markup.Escape(bDesc)}[/]");
            }
        }

        return lines;
    }

    // ═════════════════════════════════════════════════════════
    // NPC Detail Section Renderers
    // ═════════════════════════════════════════════════════════

    /// <summary>Relationship tiers on the 800-point scale (-400..+400) per Rule 19.</summary>
    private static readonly (int min, int max, string label, string color, string icon)[] RelationshipTiers = {
        (-400, -201, "Непримиримый Враг", "bold red", "💀"),
        (-200, -51,  "Противник", "red", "⚔"),
        (-50,  -1,   "Неприязнь", "orange1", "😠"),
        (0,    100,  "Нейтралитет", "grey", "😐"),
        (101,  250,  "Доверие и Расположение", "green", "😊"),
        (251,  350,  "Глубокая Связь", "bold cyan", "💙"),
        (351,  400,  "Легендарная Преданность", "bold gold1", "⭐"),
    };

    /// <summary>Hard caps where Breakthrough Quests are required (Rule 19.G).</summary>
    private static readonly (int cap, string nextTier, bool isPositive)[] RelationshipCaps = {
        (100,  "Доверие и Расположение", true),
        (250,  "Глубокая Связь", true),
        (350,  "Легендарная Преданность", true),
        (-50,  "Противник", false),
        (-200, "Непримиримый Враг", false),
    };

    private static (string label, string color) GetNpcRelationshipTier(int rep)
    {
        foreach (var t in RelationshipTiers)
        {
            if (rep >= t.min && rep <= t.max)
                return (t.label, t.color);
        }
        return rep > 400 ? ("Легендарная Преданность", "bold gold1") : ("Непримиримый Враг", "bold red");
    }

    private void RenderNpcRelationships(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold cyan]💬 Отношения:[/]");

        foreach (var entry in entries)
        {
            var lvlStr = GetStr(entry, "newRelationshipLevel", GetStr(entry, "relationshipLevel", ""));
            var reason = GetStr(entry, "changeReason", "");
            var relType = GetStr(entry, "relationshipType", "");
            var turn = GetStr(entry, "turn", GetStr(entry, "turnNumber", ""));

            // ── Numeric relationship level with full progression display ──
            if (int.TryParse(lvlStr, out var lvlNum))
            {
                var (tierLabel, tierColor) = GetNpcRelationshipTier(lvlNum);
                var tierIcon = RelationshipTiers.FirstOrDefault(t => lvlNum >= t.min && lvlNum <= t.max).icon ?? "♥";

                // Reputation bar (-400..+400 mapped to 0..20)
                var normalized = Math.Clamp((lvlNum + 400) * 20 / 800, 0, 20);
                var barColor = lvlNum >= 251 ? "cyan" : lvlNum >= 101 ? "green" : lvlNum >= 0 ? "grey" : lvlNum >= -50 ? "orange1" : "red";
                lines.Add($"    {tierIcon} [{tierColor}]{tierLabel}[/]: {ConsoleLayout.CreateBar(normalized, 20, barColor)} [{tierColor}]{lvlNum}[/]/400");

                // Show relationship type if inter-NPC
                if (!string.IsNullOrEmpty(relType) && !relType.Equals("player", StringComparison.OrdinalIgnoreCase))
                    lines.Add($"      [dim]Тип: {Markup.Escape(relType)}[/]");

                // Last change reason
                if (!string.IsNullOrEmpty(reason))
                    lines.Add($"      [dim italic]Причина: {Markup.Escape(reason)}[/]");

                // ── Lock status (critical for player!) — show ALWAYS, not just debug ──
                var isLocked = entry.TryGetProperty("isLocked", out var lk) && lk.ValueKind == JsonValueKind.True;
                var currentCap = GetInt(entry, "currentCap", 0);
                var questId = GetStr(entry, "breakthroughQuestId", "");
                var isUnbreakable = questId == "__UNBREAKABLE__";

                // Also check nested lockUpdate / relationshipLock
                if (!isLocked && entry.TryGetProperty("relationshipLock", out var rl) && rl.ValueKind == JsonValueKind.Object)
                {
                    isLocked = rl.TryGetProperty("isLocked", out var lk2) && lk2.ValueKind == JsonValueKind.True;
                    if (currentCap == 0) currentCap = GetInt(rl, "currentCap", 0);
                    if (string.IsNullOrEmpty(questId)) questId = GetStr(rl, "breakthroughQuestId", "");
                    isUnbreakable = questId == "__UNBREAKABLE__";
                }

                if (isLocked)
                {
                    if (isUnbreakable)
                    {
                        lines.Add($"      [bold red]🔒 НЕОБРАТИМО ЗАБЛОКИРОВАНО — примирение невозможно[/]");
                    }
                    else
                    {
                        var capInfo = currentCap != 0 ? $" (порог: {currentCap})" : "";
                        lines.Add($"      [bold yellow]🔒 Заблокировано{capInfo} — требуется квест прорыва[/]");
                        if (!string.IsNullOrEmpty(questId) && questId != "_clear_")
                            lines.Add($"      [yellow]📜 Квест прорыва назначен — выполните его для продвижения[/]");
                        else
                            lines.Add($"      [red]⚠ Квест прорыва НЕ назначен — напомните Мастеру Игры![/]");
                    }
                }

                // ── Progression roadmap — show next cap/tier ──
                if (!isUnbreakable)
                {
                    // Find next positive cap above current level
                    var nextCap = RelationshipCaps
                        .Where(c => c.isPositive && c.cap > lvlNum)
                        .OrderBy(c => c.cap)
                        .FirstOrDefault();

                    if (nextCap != default && lvlNum >= 0)
                    {
                        var pointsToNext = nextCap.cap - lvlNum;
                        if (isLocked && currentCap == nextCap.cap)
                        {
                            lines.Add($"      [dim]→ Следующий ранг: [white]{nextCap.nextTier}[/] (порог {nextCap.cap}) — [yellow]заблокирован, нужен квест[/][/]");
                        }
                        else
                        {
                            lines.Add($"      [dim]→ Следующий ранг: [white]{nextCap.nextTier}[/] (порог {nextCap.cap}, осталось {pointsToNext} очков)[/]");
                        }
                    }
                    else if (lvlNum > 350)
                    {
                        lines.Add($"      [dim]→ [gold1]Максимальный ранг достигнут[/][/]");
                    }

                    // Show negative danger zone
                    if (lvlNum < 0 && lvlNum > -400)
                    {
                        var nextNegCap = RelationshipCaps
                            .Where(c => !c.isPositive && c.cap > lvlNum)
                            .OrderByDescending(c => c.cap)
                            .FirstOrDefault();

                        if (nextNegCap != default)
                        {
                            var pointsToNeg = lvlNum - nextNegCap.cap;
                            lines.Add($"      [dim]→ До [red]{nextNegCap.nextTier}[/]: {Math.Abs(pointsToNeg)} очков[/]");
                        }
                    }
                }
            }
            else
            {
                // Non-numeric relationship level (string-based fallback)
                var line = $"    ♥ [yellow]{Markup.Escape(lvlStr)}[/]";
                if (!string.IsNullOrEmpty(relType))
                    line += $" [dim]({Markup.Escape(relType)})[/]";
                if (!string.IsNullOrEmpty(reason))
                    line += $" — {Markup.Escape(reason)}";
                lines.Add(line);

                // Still check for lock in string-based mode
                var isLockedFallback = entry.TryGetProperty("isLocked", out var lkf) && lkf.ValueKind == JsonValueKind.True;
                if (isLockedFallback)
                    lines.Add($"      [bold yellow]🔒 Отношение заблокировано — требуется квест прорыва[/]");
            }

            if (debugMode && !string.IsNullOrEmpty(turn))
                lines.Add($"      [dim grey](ход {Markup.Escape(turn)})[/]");

            if (debugMode)
            {
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "newRelationshipLevel", "relationshipLevel", "changeReason",
                    "relationshipType", "turn", "turnNumber", "isLocked", "currentCap",
                    "breakthroughQuestId", "relationshipLock" }, "      ");
            }
        }

        // ── Tier legend (compact) ──
        lines.Add("");
        lines.Add("    [dim]Диапазон отношений: -400 = непримиримый враг, 0 = нейтралитет, +400 = легендарная преданность.[/]");
    }

    private void RenderNpcGoals(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold green]🎯 Цели:[/]");
        foreach (var entry in entries)
        {
            var goal = GetStr(entry, "goal", GetStr(entry, "description", ""));
            var priority = GetStr(entry, "priority", "");
            var goalStatus = GetStr(entry, "status", "");

            var line = $"    • [white]{Markup.Escape(goal)}[/]";
            if (!string.IsNullOrEmpty(priority))
                line += $" [dim](приоритет: {Markup.Escape(priority)})[/]";
            if (!string.IsNullOrEmpty(goalStatus))
            {
                var gColor = goalStatus.ToLower() switch
                {
                    "completed" or "завершено" => "green",
                    "failed" or "провалено" => "red",
                    "active" or "активна" => "yellow",
                    _ => "grey"
                };
                line += $" [{gColor}]({Markup.Escape(goalStatus)})[/]";
            }
            lines.Add(line);

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "goal", "description", "priority", "status" }, "      ");
        }
    }

    private void RenderNpcActivities(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold yellow]🏃 Активность:[/]");
        foreach (var entry in entries)
        {
            // Try structured activityUpdate format first (Rule 19.F.1)
            if (entry.TryGetProperty("activityUpdate", out var upd) && upd.ValueKind == JsonValueKind.Object)
            {
                var actName = GetStr(upd, "activityName", GetStr(upd, "name", ""));
                var actDesc = GetStr(upd, "description", "");
                var activeState = GetStr(upd, "activeState", "");
                var totalTime = GetInt(upd, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(upd, "timeSpentMinutes", 0);

                if (!string.IsNullOrEmpty(actName))
                {
                    var statColor = activeState.ToLowerInvariant() switch
                    {
                        "completed" => "green",
                        "abandoned" => "red",
                        _ => "yellow"
                    };
                    var line = $"    ⚡ [white]{Markup.Escape(actName)}[/]";
                    if (!string.IsNullOrEmpty(activeState))
                        line += $" [{statColor}]({Markup.Escape(activeState)})[/]";
                    lines.Add(line);
                }
                if (!string.IsNullOrEmpty(actDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(actDesc)}[/]");

                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс выполнения: {ConsoleLayout.CreateBarFromPercent(pct, 10, barColor)} {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
            }
            else
            {
                // Fallback: flat format
                var activity = GetStr(entry, "activityName",
                    GetStr(entry, "activity", GetStr(entry, "description", "")));
                var actLocation = GetStr(entry, "location", "");
                var actStatus = GetStr(entry, "status",
                    GetStr(entry, "activeState", GetStr(entry, "completed", "")));
                var actDesc = GetStr(entry, "description",
                    activity == GetStr(entry, "description", "") ? "" : GetStr(entry, "description", ""));

                var line = $"    ⚡ [white]{Markup.Escape(activity)}[/]";
                if (!string.IsNullOrEmpty(actLocation))
                    line += $" [dim]📍 {Markup.Escape(actLocation)}[/]";
                if (!string.IsNullOrEmpty(actStatus))
                {
                    var statColor = actStatus.ToLowerInvariant() switch
                    {
                        "completed" => "green",
                        "abandoned" => "red",
                        _ => "yellow"
                    };
                    line += $" [{statColor}]({Markup.Escape(actStatus)})[/]";
                }
                lines.Add(line);

                // Show description if different from activity name
                if (!string.IsNullOrEmpty(actDesc) && actDesc != activity)
                    lines.Add($"      [dim italic]{Markup.Escape(actDesc)}[/]");

                // Progress bar if time data available
                var totalTime = GetInt(entry, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(entry, "timeSpentMinutes", 0);
                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс выполнения: {ConsoleLayout.CreateBarFromPercent(pct, 10, barColor)} {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
            }

            // Narrative summary for completed activities
            var narrative = GetStr(entry, "narrativeSummary", "");
            if (!string.IsNullOrEmpty(narrative))
                lines.Add($"      📝 [dim]{Markup.Escape(narrative)}[/]");

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name", "NPCId",
                    "activity", "activityName", "activityUpdate", "description", "location",
                    "status", "activeState", "completed", "narrativeSummary",
                    "totalTimeCostMinutes", "timeSpentMinutes" }, "      ");
        }
    }

    private void RenderNpcInventory(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var display = BuildNpcInventoryDisplay(doc, npcId, npcName);
        if (display.IsEmpty) return;

        if (display.Items.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold orange3]🎒 Инвентарь:[/]");
            foreach (var item in display.Items)
            {
                var itemName = GetNodeStr(item.Data, "name", "?");
                var qty = GetNodeStr(item.Data, "quantity", GetNodeStr(item.Data, "count", ""));
                var itemType = GetNodeStr(item.Data, "type", GetNodeStr(item.Data, "category", ""));
                var resource = GetNodeStr(item.Data, "resource", "");
                var maxResource = GetNodeStr(item.Data, "maximumResource", "");
                var resourceType = GetNodeStr(item.Data, "resourceType", "");
                var durability = GetNodeStr(item.Data, "durability", "");

                var line = item.IsEquipped
                    ? $"    ⚔ [green]{Markup.Escape(itemName)}[/] [green](экипировано)[/]"
                    : $"    • [white]{Markup.Escape(itemName)}[/]";
                if (!string.IsNullOrEmpty(qty) && qty != "1")
                    line += $" ×{Markup.Escape(qty)}";
                if (!string.IsNullOrEmpty(itemType))
                    line += $" [dim]({Markup.Escape(itemType)})[/]";
                if (!string.IsNullOrEmpty(resource))
                {
                    var resourceLabel = !string.IsNullOrEmpty(resourceType) ? $" {Markup.Escape(resourceType)}" : "";
                    var maxLabel = !string.IsNullOrEmpty(maxResource) ? $"/{Markup.Escape(maxResource)}" : "";
                    line += $" [cyan]{Markup.Escape(resource)}{maxLabel}{resourceLabel}[/]";
                }
                if (!string.IsNullOrEmpty(durability))
                    line += $" [dim]прочность: {Markup.Escape(durability)}[/]";
                lines.Add(line);

                if (debugMode)
                    RenderExtraFields(lines, JsonObjectToElement(item.Data), new[] { "name", "quantity", "count",
                        "type", "category", "equipped", "resource", "maximumResource", "resourceType",
                        "durability", "existedId", "initialId" }, "      ");
            }
        }

        if (display.Equipment.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold orange3]🛡️ Экипировка:[/]");
            foreach (var eq in display.Equipment)
                lines.Add($"    • [dim]{Markup.Escape(eq.Slot)}:[/] [white]{Markup.Escape(eq.ItemName)}[/]");
        }
    }

    private void RenderNpcEffects(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold]✨ Эффекты:[/]");
        foreach (var entry in entries)
        {
            var effType = GetStr(entry, "effectType", GetStr(entry, "type", ""));
            var effDesc = GetStr(entry, "description", GetStr(entry, "effect", ""));
            var duration = GetStr(entry, "duration", GetStr(entry, "turnsRemaining", ""));
            var isWound = effType.ToLower().Contains("wound") || effType.ToLower().Contains("ран")
                       || effDesc.ToLower().Contains("wound") || effDesc.ToLower().Contains("ран");
            var isDebuff = effType.ToLower().Contains("debuff") || effType.ToLower().Contains("негатив");
            var color = isWound ? "red" : isDebuff ? "orange3" : "green";
            var icon = isWound ? "🩸" : isDebuff ? "⚠️" : "✨";

            var displayText = !string.IsNullOrEmpty(effDesc) ? effDesc : effType;
            var line = $"    {icon} [{color}]{Markup.Escape(displayText)}[/]";
            if (!string.IsNullOrEmpty(duration))
                line += $" [dim](длительность: {Markup.Escape(duration)})[/]";
            lines.Add(line);

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "effectType", "type", "description", "effect", "duration", "turnsRemaining" }, "      ");
        }
    }

    private void RenderNpcSkills(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var display = BuildNpcSkillDisplay(doc, npcId, npcName);
        if (display.IsEmpty) return;

        if (display.Active.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold cyan]⚔ Активные навыки:[/]");
            foreach (var skill in display.Active)
                RenderNpcSkillEntry(lines, skill, "cyan", debugMode);
        }

        if (display.Passive.Count > 0)
        {
            lines.Add($"  [bold dim]🛡️ Пассивные навыки:[/]");
            foreach (var skill in display.Passive)
                RenderNpcSkillEntry(lines, skill, "white", debugMode);
        }
    }

    private void RenderNpcSkillEntry(List<string> lines, NpcSkillDisplayEntry skill, string color, bool debugMode)
    {
        var name = GetNodeStr(skill.Data, "skillName", GetNodeStr(skill.Data, "name", "?"));
        var type = GetNodeStr(skill.Data, "type", "");
        var cooldown = GetNodeStr(skill.Data, "cooldown", "");
        var description = GetNodeStr(skill.Data, "skillDescription", GetNodeStr(skill.Data, "description", ""));

        var line = $"    • [{color}]{Markup.Escape(name)}[/]";
        if (skill.MasteryLevel.HasValue)
            line += $" [yellow](мастерство {skill.MasteryLevel.Value})[/]";
        if (!string.IsNullOrEmpty(type))
            line += $" [dim]({Markup.Escape(type)})[/]";
        if (!string.IsNullOrEmpty(cooldown))
            line += $" [dim](перезарядка: {Markup.Escape(cooldown)})[/]";
        lines.Add(line);

        if (skill.CurrentMasteryProgress.HasValue || skill.MasteryProgressNeeded.HasValue || skill.MaxMasteryLevel.HasValue)
        {
            var masteryBits = new List<string>();
            if (skill.CurrentMasteryProgress.HasValue || skill.MasteryProgressNeeded.HasValue)
                masteryBits.Add($"прогресс {skill.CurrentMasteryProgress.GetValueOrDefault(0)}/{skill.MasteryProgressNeeded.GetValueOrDefault(0)}");
            if (skill.MaxMasteryLevel.HasValue)
                masteryBits.Add($"макс. {skill.MaxMasteryLevel.Value}");
            lines.Add($"      [dim]{Markup.Escape(string.Join(" • ", masteryBits))}[/]");
        }

        if (!string.IsNullOrEmpty(description))
            lines.Add($"      [dim]{Markup.Escape(description)}[/]");

        if (debugMode)
            RenderExtraFields(lines, JsonObjectToElement(skill.Data), new[] { "skillName", "name",
                "skillDescription", "description", "type", "cooldown" }, "      ");
    }

    // ── Personality (npc_personality.json) — visible to player ──
    private void RenderNpcPersonality(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;
        lines.Add("");
        lines.Add($"  [bold magenta1]🧠 Личность / Черты характера:[/]");
        foreach (var entry in entries) {
            var tName = GetStr(entry, "traitName", "");
            var tVal = GetInt(entry, "value", -1);
            var tValDesc = GetStr(entry, "valueDescription", "");
            if (!string.IsNullOrEmpty(tName)) {
                var line = $"    • [white]{Markup.Escape(tName)}[/]";
                if (tVal >= 0) {
                    var tBarW = 10;
                    var tFilled = Math.Clamp(tVal * tBarW / 10, 0, tBarW);
                    var tColor = tVal >= 7 ? "green" : tVal >= 4 ? "yellow" : "red";
                    line += $" [{tColor}]{new string('█', tFilled)}[/][dim]{new string('░', tBarW - tFilled)}[/] [{tColor}]{tVal}/10[/]";
                }
                if (!string.IsNullOrEmpty(tValDesc)) line += $" [dim]— {Markup.Escape(tValDesc)}[/]";
                lines.Add(line);
            }
            var traits = GetStr(entry, "traits", "");
            var temperament = GetStr(entry, "temperament", "");
            var morality = GetStr(entry, "morality", GetStr(entry, "alignment", ""));
            if (!string.IsNullOrEmpty(traits)) lines.Add($"    🏷️ Черты: [white]{Markup.Escape(traits)}[/]");
            if (!string.IsNullOrEmpty(temperament)) lines.Add($"    🌡️ Темперамент: [white]{Markup.Escape(temperament)}[/]");
            if (!string.IsNullOrEmpty(morality)) lines.Add($"    ⚖️ Мораль: [white]{Markup.Escape(morality)}[/]");
            if (debugMode) RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name", "traits", "temperament", "traitName", "value", "valueDescription", "morality", "alignment" }, "    ");
        }
    }

    // ── Journals (npc_journals.json -> NPCJournals) ──
    private void RenderNpcJournals(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;

        JsonElement target;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("NPCJournals", out var journals))
            target = journals;
        else
            target = doc.RootElement;

        var entries = new List<JsonElement>();
        EnumerateJsonItems(target, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                entries.Add(item);
        });

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold steelblue1]📓 Дневник / Мысли:[/]");
        foreach (var entry in entries)
        {
            var thought = GetStr(entry, "lastJournalNote", "");
            var turn = GetStr(entry, "turn", GetStr(entry, "turnNumber", ""));
            var topic = GetStr(entry, "topic", "");

            var turnTag = !string.IsNullOrEmpty(turn) ? $"[dim]Ход {turn}:[/] " : "";
            var topicTag = !string.IsNullOrEmpty(topic) ? $"[steelblue1][{Markup.Escape(topic)}][/] " : "";
            
            if (!string.IsNullOrEmpty(thought))
                lines.Add($"    {turnTag}{topicTag}[italic]«{Markup.Escape(thought)}»[/]");

            if (entry.TryGetProperty("journalEntries", out var journalEntries) &&
                journalEntries.ValueKind == JsonValueKind.Array &&
                journalEntries.GetArrayLength() > 0)
            {
                foreach (var journalEntry in journalEntries.EnumerateArray())
                {
                    if (journalEntry.ValueKind != JsonValueKind.Object) continue;
                    var timestamp = GetStr(journalEntry, "timestamp", "");
                    var eventName = GetStr(journalEntry, "event", "");
                    var description = GetStr(journalEntry, "description", "");
                    var emotionalImpact = GetStr(journalEntry, "emotionalImpact", "");
                    var relationshipChange = GetStr(journalEntry, "relationshipChange", "");

                    var entryPrefix = new List<string>();
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        entryPrefix.Add(Markup.Escape(timestamp));
                    if (!string.IsNullOrWhiteSpace(eventName))
                        entryPrefix.Add(Markup.Escape(eventName));
                    var prefix = entryPrefix.Count > 0
                        ? $"      [dim]{string.Join(" • ", entryPrefix)}[/]"
                        : "      [dim]Запись[/]";
                    lines.Add(prefix);
                    if (!string.IsNullOrWhiteSpace(description))
                        lines.Add($"        [white]{Markup.Escape(description)}[/]");
                    if (!string.IsNullOrWhiteSpace(emotionalImpact))
                        lines.Add($"        [magenta1]Эмоциональный след:[/] {Markup.Escape(emotionalImpact)}");
                    if (!string.IsNullOrWhiteSpace(relationshipChange))
                        lines.Add($"        [cyan]Изменение отношения:[/] {Markup.Escape(relationshipChange)}");
                }
            }

            if (debugMode)
            {
                var context = GetStr(entry, "context", "");
                if (!string.IsNullOrEmpty(context))
                    lines.Add($"      [dim grey]🔍 {Markup.Escape(context)}[/]");
            }
        }
    }

    // ── Masks (npc_masks.json) — Rule 17 Social Roles ──
    private void RenderNpcMasks(List<string> lines, JsonDocument? doc, string npcId, string npcName)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold red]🎭 Маски (социальные роли):[/]");
        foreach (var entry in entries)
        {
            var maskName = GetStr(entry, "maskName", GetStr(entry, "activeMask", GetStr(entry, "name", "")));
            var maskDesc = GetStr(entry, "description", GetStr(entry, "behavior", ""));
            var isActive = entry.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True;
            var trigger = GetStr(entry, "trigger", GetStr(entry, "condition", ""));

            var activeStr = isActive ? " [green]● активна[/]" : "";
            if (!string.IsNullOrEmpty(maskName))
                lines.Add($"    🎭 [red]{Markup.Escape(maskName)}[/]{activeStr}");
            if (!string.IsNullOrEmpty(maskDesc))
                lines.Add($"      [white]{Markup.Escape(maskDesc)}[/]");
            if (!string.IsNullOrEmpty(trigger))
                lines.Add($"      [dim]Триггер: {Markup.Escape(trigger)}[/]");

            RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                "maskName", "activeMask", "description", "behavior", "isActive",
                "trigger", "condition" }, "      ");
        }
    }

    // ── Memories (npc_memory.json -> NPCUnlockedMemories) ──
    private void RenderNpcMemories(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;

        JsonElement target;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("NPCUnlockedMemories", out var memories))
            target = memories;
        else
            target = doc.RootElement;

        var entries = new List<JsonElement>();
        EnumerateJsonItems(target, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                entries.Add(item);
        });

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold lightslateblue]✨ Воспоминания Души[/]");
        foreach (var entry in entries)
        {
            var rarity = GetStr(entry, "rarity", "Common");
            var rColor = rarity.ToLower() switch
            {
                "rare" or "редкий" => "blue",
                "epic" or "эпический" => "purple",
                "legendary" or "легендарный" => "orange1",
                _ => "white"
            };

            var title = GetStr(entry, "title", "???");
            var desc = GetStr(entry, "content", "");
            var unlockedAt = GetStr(entry, "unlockedAtRelationshipLevel", "");

            var line = $"    • [{rColor}]{Markup.Escape(title)}[/]";
            if (!string.IsNullOrEmpty(rarity))
                line += $" [dim]({Markup.Escape(rarity)})[/]";
            if (!string.IsNullOrEmpty(unlockedAt))
                line += $" [dim]репутация {Markup.Escape(unlockedAt)}[/]";
            lines.Add(line);
            if (!string.IsNullOrEmpty(desc))
                lines.Add($"      [italic]{Markup.Escape(desc)}[/]");

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCId", "npcId", "NPCName", "npcName",
                    "name", "memoryId", "title", "content", "rarity",
                    "unlockedAtRelationshipLevel" }, "      ");
        }
    }

    // ── Fate Cards (npc_fate_cards.json) — unlocked cards visible to player ──
    private void RenderNpcFateCards(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold gold1]🃏 Карты судьбы:[/]");
        lines.Add($"  [dim italic]Вехи развития НПС — открываются через отношения и сюжет[/]");
        foreach (var entry in entries)
        {
            var cardName = GetStr(entry, "cardName", GetStr(entry, "name", ""));
            var cardDesc = GetStr(entry, "description", GetStr(entry, "effect", ""));
            var cardType = GetStr(entry, "cardType", GetStr(entry, "type", ""));
            var isUnlocked = (entry.TryGetProperty("isUnlocked", out var iu) && iu.ValueKind == JsonValueKind.True)
                          || (entry.TryGetProperty("isRevealed", out var ir) && ir.ValueKind == JsonValueKind.True);

            // Unlock conditions
            var reqRelLevel = GetStr(entry, "requiredRelationshipLevel", "");
            var plotCondition = GetStr(entry, "plotConditionDescription", "");
            var conjunction = GetStr(entry, "conjunction", "AND");

            var statusStr = isUnlocked
                ? " [green]● разблокирована[/]"
                : " [dim red]🔒 заблокирована[/]";

            if (!string.IsNullOrEmpty(cardName))
                lines.Add($"    🃏 [gold1]{Markup.Escape(cardName)}[/]{statusStr}");
            if (!string.IsNullOrEmpty(cardType))
                lines.Add($"      Тип: [white]{Markup.Escape(cardType)}[/]");
            if (!string.IsNullOrEmpty(cardDesc))
                lines.Add($"      [white]{Markup.Escape(cardDesc)}[/]");

            // Show unlock conditions for locked cards
            if (!isUnlocked)
            {
                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(reqRelLevel))
                {
                    if (int.TryParse(reqRelLevel, out var reqRep))
                    {
                        var (tierLabel, _) = GetNpcRelationshipTier(reqRep);
                        conditions.Add($"отношение ≥ {reqRep} ({tierLabel})");
                    }
                    else
                        conditions.Add($"отношение: {Markup.Escape(reqRelLevel)}");
                }
                if (!string.IsNullOrEmpty(plotCondition))
                    conditions.Add(Markup.Escape(plotCondition));

                if (conditions.Count > 0)
                {
                    var conjText = conjunction.Equals("OR", StringComparison.OrdinalIgnoreCase) ? " ИЛИ " : " И ";
                    lines.Add($"      [dim]Условия: {string.Join(conjText, conditions)}[/]");
                }
            }

            // Show rewards for unlocked cards
            if (isUnlocked && entry.TryGetProperty("rewards", out var rewards) && rewards.ValueKind == JsonValueKind.Object)
            {
                var rewardDesc = GetStr(rewards, "description", "");
                if (!string.IsNullOrEmpty(rewardDesc))
                    lines.Add($"      [italic green]📜 {Markup.Escape(rewardDesc)}[/]");

                RenderFateCardRewardArray(lines, rewards, "newActiveSkills", "⚔ Новые активные навыки");
                RenderFateCardRewardArray(lines, rewards, "newPassiveSkills", "🛡 Новые пассивные навыки");
                RenderFateCardRewardArray(lines, rewards, "statBoosts", "📈 Усиления характеристик");
                RenderFateCardRewardArray(lines, rewards, "newServices", "🤝 Новые услуги");
                RenderFateCardRewardArray(lines, rewards, "otherNarrativeRewards", "🌟 Особые награды");
                RenderFateCardRewardArray(lines, rewards, "tacticalTriggers", "⚡ Тактические триггеры");
            }

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "cardName", "cardId", "description", "effect", "cardType", "type",
                    "isRevealed", "isUnlocked", "rewards", "requiredRelationshipLevel",
                    "plotConditionDescription", "conjunction", "image_prompt" }, "      ");
        }
    }

    private void RenderFateCardRewardArray(List<string> lines, JsonElement rewards, string fieldName, string label)
    {
        if (!rewards.TryGetProperty(fieldName, out var arr)) return;
        if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
        {
            lines.Add($"      [yellow]{label}:[/]");
            foreach (var item in arr.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? ""
                    : GetStr(item, "name", GetStr(item, "description", item.ToString()));
                if (!string.IsNullOrEmpty(text))
                    lines.Add($"        • [white]{Markup.Escape(text)}[/]");
            }
        }
        else if (arr.ValueKind == JsonValueKind.String)
        {
            var text = arr.GetString() ?? "";
            if (!string.IsNullOrEmpty(text))
                lines.Add($"      [yellow]{label}:[/] [white]{Markup.Escape(text)}[/]");
        }
    }

    // ── Custom States (npc_custom_states.json) — Rule 25.A ──
    private void RenderNpcCustomStates(List<string> lines, JsonDocument? doc, string npcId, string npcName)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold magenta]📊 Особые состояния:[/]");
        foreach (var entry in entries)
        {
            // Support both flat format and nested stateChanges array (Rule 25.A.2)
            var stateItems = new List<JsonElement>();
            if (entry.TryGetProperty("stateChanges", out var sc) && sc.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sc.EnumerateArray()) stateItems.Add(s);
            }
            else
            {
                // Flat format: entry itself is a state object
                stateItems.Add(entry);
            }

            foreach (var item in stateItems)
                RenderCustomStateItem(lines, item, "    ");
        }
    }

    /// <summary>Renders a single Custom State Object (Rule 25.1) with progress bar, thresholds, progression.</summary>
    private static void RenderCustomStateItem(List<string> lines, JsonElement item, string indent)
    {
        var name = GetStr(item, "stateName", GetStr(item, "stateKey", GetStr(item, "key", GetStr(item, "name", ""))));
        if (string.IsNullOrEmpty(name)) return;

        // Try numeric display with progress bar
        var hasNumeric = item.TryGetProperty("currentValue", out var curProp)
            && (curProp.ValueKind == JsonValueKind.Number || curProp.ValueKind == JsonValueKind.String);
        if (hasNumeric)
        {
            var cur = GetInt(item, "currentValue", 0);
            var min = GetInt(item, "minValue", 0);
            var max = GetInt(item, "maxValue", 100);
            var range = max - min;
            var pct = range > 0 ? ((cur - min) * 100 / range) : 0;
            var barColor = pct > 66 ? "green" : pct > 33 ? "yellow" : "red";
            var barFill = Math.Clamp(pct / 5, 0, 20);
            var barEmpty = 20 - barFill;
            lines.Add($"{indent}[{barColor}]{Markup.Escape(name)}[/]: [{barColor}]{new string('█', barFill)}[/][dim]{new string('░', barEmpty)}[/] {cur}/{max}");
        }
        else
        {
            // Fallback: string value
            var stateVal = GetStr(item, "stateValue", GetStr(item, "value", GetStr(item, "currentValue", "")));
            var line = $"{indent}[white]{Markup.Escape(name)}[/]";
            if (!string.IsNullOrEmpty(stateVal))
                line += $": [cyan]{Markup.Escape(stateVal)}[/]";
            lines.Add(line);
        }

        var desc = GetStr(item, "description", "");
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"{indent}  [dim]{Markup.Escape(desc)}[/]");

        // Progression rule
        if (item.TryGetProperty("progressionRule", out var pr) && pr.ValueKind == JsonValueKind.Object)
        {
            var changePerTurn = GetStr(pr, "changePerTurn", "");
            var prDesc = GetStr(pr, "description", "");
            if (!string.IsNullOrEmpty(changePerTurn))
                lines.Add($"{indent}  📈 За ход: [yellow]{Markup.Escape(changePerTurn)}[/]" +
                    (!string.IsNullOrEmpty(prDesc) ? $" [dim]({Markup.Escape(prDesc)})[/]" : ""));
        }

        // Thresholds (Block 25)
        if (item.TryGetProperty("thresholds", out var th) && th.ValueKind == JsonValueKind.Array && th.GetArrayLength() > 0)
        {
            lines.Add($"{indent}  [bold]Пороги:[/]");
            var curVal = GetInt(item, "currentValue", 0);
            foreach (var t in th.EnumerateArray())
            {
                var lvlName = GetStr(t, "levelName", "?");
                var trigVal = GetStr(t, "triggerValue", "");
                var trigCond = GetStr(t, "triggerCondition", "");
                var tColor = "dim";
                if (int.TryParse(trigVal, out var tVal))
                {
                    if (trigCond.Contains("<=") && curVal <= tVal) tColor = "red";
                    else if (trigCond.Contains(">=") && curVal >= tVal) tColor = "red";
                }
                lines.Add($"{indent}    [{tColor}]• {Markup.Escape(lvlName)}: {Markup.Escape(trigCond)} {Markup.Escape(trigVal)}[/]");
                if (t.TryGetProperty("associatedEffects", out var ae) && ae.ValueKind == JsonValueKind.Array)
                {
                    foreach (var eff in ae.EnumerateArray())
                    {
                        var eType = GetStr(eff, "effectType", "?");
                        var eVal = GetStr(eff, "value", "");
                        var eDesc = GetStr(eff, "effectDescription", "");
                        var eLine = $"{indent}      ⚡ [{(eType.ToLower().Contains("damage") ? "red" : "yellow")}]{Markup.Escape(eType)}[/] {Markup.Escape(eVal)}";
                        if (!string.IsNullOrEmpty(eDesc)) eLine += $" [dim]— {Markup.Escape(eDesc)}[/]";
                        lines.Add(eLine);
                    }
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════
    // NPC Helper Methods
    // ═════════════════════════════════════════════════════════

    private sealed class NpcSkillDisplay
    {
        public List<NpcSkillDisplayEntry> Active { get; } = new();
        public List<NpcSkillDisplayEntry> Passive { get; } = new();
        public bool IsEmpty => Active.Count == 0 && Passive.Count == 0;
    }

    private sealed class NpcSkillDisplayEntry
    {
        public JsonObject Data { get; init; } = new();
        public int? MasteryLevel { get; set; }
        public int? CurrentMasteryProgress { get; set; }
        public int? MasteryProgressNeeded { get; set; }
        public int? MaxMasteryLevel { get; set; }
    }

    private sealed class NpcInventoryDisplay
    {
        public List<NpcInventoryItemDisplay> Items { get; } = new();
        public List<NpcEquipmentDisplay> Equipment { get; } = new();
        public bool IsEmpty => Items.Count == 0 && Equipment.Count == 0;
    }

    private sealed class NpcInventoryItemDisplay
    {
        public string Key { get; init; } = "";
        public JsonObject Data { get; init; } = new();
        public bool IsEquipped { get; set; }
    }

    private sealed class NpcEquipmentDisplay
    {
        public string Slot { get; init; } = "";
        public string ItemName { get; init; } = "";
    }

    private static Dictionary<string, string> BuildNpcRenameMap(JsonDocument doc)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

        if (!doc.RootElement.TryGetProperty("NPCsRenameData", out var renames) || renames.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in renames.EnumerateArray())
        {
            var oldName = GetStr(item, "oldName", "");
            var newName = GetStr(item, "newName", "");
            if (!string.IsNullOrWhiteSpace(oldName) && !string.IsNullOrWhiteSpace(newName))
                result[oldName] = newName;
        }

        return result;
    }

    private static string ResolveNpcDisplayName(JsonElement npc, IReadOnlyDictionary<string, string> renameMap)
    {
        var name = GetStr(npc, "name", "???");
        return renameMap.TryGetValue(name, out var renamed) ? renamed : name;
    }

    /// <summary>
    /// Collects list-worthy NPC objects from npc_core.json.
    /// </summary>
    private List<JsonElement> CollectNpcListEntries(JsonDocument doc)
    {
        var result = new List<JsonElement>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddNpc(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object) return;

            var name = GetStr(item, "name", "");
            var npcId = GetPrimaryNpcId(item);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(npcId))
                return;

            var key = !string.IsNullOrWhiteSpace(npcId)
                ? $"id:{npcId}"
                : $"name:{name}";

            if (seenKeys.Add(key))
                result.Add(item);
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in GetNpcCoreArrayKeys())
            {
                if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                        AddNpc(item);
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> GetNpcCoreArrayKeys()
    {
        yield return "NPCsInScene";
        yield return "UpdateNPCs";
    }

    private static IEnumerable<JsonArray> GetNpcCoreArrays(JsonObject root)
    {
        foreach (var key in GetNpcCoreArrayKeys())
            if (root[key] is JsonArray arr)
                yield return arr;
    }

    /// <summary>
    /// Collects all JSON items matching an NPC by ID first, then by name.
    /// </summary>
    private List<JsonElement> CollectNpcEntries(JsonDocument doc, string npcId, string npcName)
    {
        var result = new List<JsonElement>();
        EnumerateJsonItems(doc.RootElement, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                result.Add(item);
        });
        return result;
    }

    private static string GetPrimaryNpcId(JsonElement item)
    {
        return GetStr(item, "NPCId",
            GetStr(item, "npcId",
                GetStr(item, "id", "")));
    }

    private static bool MatchesNpcEntry(JsonElement item, string npcId, string npcName)
    {
        var entryId = GetPrimaryNpcId(item);
        if (!string.IsNullOrWhiteSpace(npcId) &&
            !string.IsNullOrWhiteSpace(entryId) &&
            string.Equals(entryId, npcId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entryName = GetStr(item, "NPCName",
            GetStr(item, "npcName",
                GetStr(item, "name", "")));

        return !string.IsNullOrWhiteSpace(entryName) &&
               string.Equals(entryName, npcName, StringComparison.OrdinalIgnoreCase);
    }

    private NpcSkillDisplay BuildNpcSkillDisplay(JsonDocument doc, string npcId, string npcName)
    {
        var display = new NpcSkillDisplay();
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return display;

        var active = new Dictionary<string, NpcSkillDisplayEntry>(StringComparer.OrdinalIgnoreCase);
        var passive = new Dictionary<string, NpcSkillDisplayEntry>(StringComparer.OrdinalIgnoreCase);

        PopulateNpcSkillChanges(doc.RootElement, "NPCActiveSkillChanges", active, npcId, npcName);
        PopulateNpcSkillChanges(doc.RootElement, "NPCPassiveSkillChanges", passive, npcId, npcName);
        ApplyNpcSkillMastery(doc.RootElement, "NPCSkillMasteryChanges", active, npcId, npcName);
        ApplyNpcSkillMastery(doc.RootElement, "NPCPassiveSkillMasteryChanges", passive, npcId, npcName);

        foreach (var item in active.Values.OrderBy(v => GetNodeStr(v.Data, "skillName", GetNodeStr(v.Data, "name", "")), StringComparer.OrdinalIgnoreCase))
            display.Active.Add(item);
        foreach (var item in passive.Values.OrderBy(v => GetNodeStr(v.Data, "skillName", GetNodeStr(v.Data, "name", "")), StringComparer.OrdinalIgnoreCase))
            display.Passive.Add(item);

        return display;
    }

    private void PopulateNpcSkillChanges(JsonElement root, string propertyName,
        Dictionary<string, NpcSkillDisplayEntry> target, string npcId, string npcName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in arr.EnumerateArray())
        {
            if (!MatchesNpcEntry(entry, npcId, npcName))
                continue;

            if (entry.TryGetProperty("skillChanges", out var changes) && changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var skill in changes.EnumerateArray())
                {
                    var skillName = GetStr(skill, "skillName", GetStr(skill, "name", ""));
                    if (string.IsNullOrWhiteSpace(skillName))
                        continue;

                    target[skillName] = new NpcSkillDisplayEntry
                    {
                        Data = CloneJsonObject(skill)
                    };
                }
            }

            if (entry.TryGetProperty("skillsToRemove", out var removals) && removals.ValueKind == JsonValueKind.Array)
            {
                foreach (var skillName in removals.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString())
                    .Where(v => !string.IsNullOrWhiteSpace(v)))
                {
                    target.Remove(skillName!);
                }
            }
        }
    }

    private void ApplyNpcSkillMastery(JsonElement root, string propertyName,
        Dictionary<string, NpcSkillDisplayEntry> target, string npcId, string npcName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in arr.EnumerateArray())
        {
            if (!MatchesNpcEntry(entry, npcId, npcName))
                continue;

            var skillName = GetStr(entry, "skillName", "");
            if (string.IsNullOrWhiteSpace(skillName))
                continue;

            if (!target.TryGetValue(skillName, out var skill))
            {
                var placeholder = new JsonObject
                {
                    ["skillName"] = skillName
                };
                skill = new NpcSkillDisplayEntry { Data = placeholder };
                target[skillName] = skill;
            }

            var mastery = GetInt(entry, "newMasteryLevel", int.MinValue);
            if (mastery != int.MinValue) skill.MasteryLevel = mastery;

            var progress = GetInt(entry, "newCurrentMasteryProgress", int.MinValue);
            if (progress != int.MinValue) skill.CurrentMasteryProgress = progress;

            var needed = GetInt(entry, "newMasteryProgressNeeded", int.MinValue);
            if (needed != int.MinValue) skill.MasteryProgressNeeded = needed;

            var maxLevel = GetInt(entry, "newMaxMasteryLevel", int.MinValue);
            if (maxLevel != int.MinValue) skill.MaxMasteryLevel = maxLevel;
        }
    }

    private NpcInventoryDisplay BuildNpcInventoryDisplay(JsonDocument doc, string npcId, string npcName)
    {
        var display = new NpcInventoryDisplay();
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return display;

        var itemsByKey = new Dictionary<string, NpcInventoryItemDisplay>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var equippedSlots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.TryGetProperty("NPCInventoryAdds", out var adds) && adds.ValueKind == JsonValueKind.Array)
        {
            var generatedIndex = 0;
            foreach (var entry in adds.EnumerateArray())
            {
                if (!MatchesNpcEntry(entry, npcId, npcName)) continue;
                if (!entry.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) continue;

                var key = ResolveInventoryKey(item, ++generatedIndex);
                var itemDisplay = new NpcInventoryItemDisplay
                {
                    Key = key,
                    Data = CloneJsonObject(item)
                };

                itemsByKey[key] = itemDisplay;
                if (!order.Contains(key, StringComparer.OrdinalIgnoreCase))
                    order.Add(key);
            }
        }

        if (doc.RootElement.TryGetProperty("NPCInventoryUpdates", out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in updates.EnumerateArray())
            {
                if (!MatchesNpcEntry(entry, npcId, npcName)) continue;
                if (!entry.TryGetProperty("itemUpdate", out var itemUpdate) || itemUpdate.ValueKind != JsonValueKind.Object) continue;

                var key = GetStr(itemUpdate, "existedId", "");
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var itemDisplay = GetOrCreateInventoryItem(itemsByKey, order, key, key);
                MergeJsonObject(itemDisplay.Data, CloneJsonObject(itemUpdate));

                var count = GetNodeInt(itemDisplay.Data, "count");
                if (count == 0)
                {
                    itemsByKey.Remove(key);
                    order.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        if (doc.RootElement.TryGetProperty("NPCInventoryResourcesChanges", out var resources) && resources.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in resources.EnumerateArray())
            {
                if (!MatchesNpcEntry(entry, npcId, npcName)) continue;

                var key = GetStr(entry, "itemId", "");
                var itemName = GetStr(entry, "itemName", key);
                if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(itemName))
                    continue;

                var itemDisplay = GetOrCreateInventoryItem(itemsByKey, order, key, itemName);
                if (!string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(GetNodeStr(itemDisplay.Data, "name", "")))
                    itemDisplay.Data["name"] = itemName;

                var newResource = GetInt(entry, "newResourceValue", int.MinValue);
                if (newResource != int.MinValue)
                    itemDisplay.Data["resource"] = newResource;
            }
        }

        if (doc.RootElement.TryGetProperty("NPCInventoryRemovals", out var removals) && removals.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in removals.EnumerateArray())
            {
                if (!MatchesNpcEntry(entry, npcId, npcName)) continue;

                var key = GetStr(entry, "itemId", "");
                if (string.IsNullOrWhiteSpace(key)) continue;

                itemsByKey.Remove(key);
                order.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

                foreach (var slot in equippedSlots.Where(kv => string.Equals(kv.Value, key, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList())
                    equippedSlots.Remove(slot);
            }
        }

        if (doc.RootElement.TryGetProperty("NPCEquipmentChanges", out var equips) && equips.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in equips.EnumerateArray())
            {
                if (!MatchesNpcEntry(entry, npcId, npcName)) continue;

                var action = GetStr(entry, "action", "").ToLowerInvariant();
                var itemId = GetStr(entry, "itemId", "");
                var itemName = GetStr(entry, "itemName", itemId);

                if (action == "equip")
                {
                    var itemDisplay = GetOrCreateInventoryItem(itemsByKey, order, itemId, itemName);
                    if (!string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(GetNodeStr(itemDisplay.Data, "name", "")))
                        itemDisplay.Data["name"] = itemName;

                    if (entry.TryGetProperty("targetSlots", out var targetSlots) && targetSlots.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var slot in targetSlots.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).Where(v => !string.IsNullOrWhiteSpace(v)))
                            equippedSlots[slot!] = itemDisplay.Key;
                    }
                }
                else if (action == "unequip")
                {
                    if (entry.TryGetProperty("sourceSlots", out var sourceSlots) && sourceSlots.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var slot in sourceSlots.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).Where(v => !string.IsNullOrWhiteSpace(v)))
                            equippedSlots.Remove(slot!);
                    }
                    else if (!string.IsNullOrWhiteSpace(itemId))
                    {
                        foreach (var slot in equippedSlots.Where(kv => string.Equals(kv.Value, itemId, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList())
                            equippedSlots.Remove(slot);
                    }
                }
            }
        }

        foreach (var key in order)
        {
            if (!itemsByKey.TryGetValue(key, out var item)) continue;
            item.IsEquipped = equippedSlots.Values.Any(v => string.Equals(v, key, StringComparison.OrdinalIgnoreCase));
            display.Items.Add(item);
        }

        foreach (var slot in equippedSlots.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var itemName = itemsByKey.TryGetValue(slot.Value, out var item)
                ? GetNodeStr(item.Data, "name", slot.Value)
                : slot.Value;

            display.Equipment.Add(new NpcEquipmentDisplay
            {
                Slot = slot.Key,
                ItemName = itemName
            });
        }

        return display;
    }

    private static NpcInventoryItemDisplay GetOrCreateInventoryItem(
        Dictionary<string, NpcInventoryItemDisplay> itemsByKey,
        List<string> order,
        string itemId,
        string itemName)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && itemsByKey.TryGetValue(itemId, out var byId))
            return byId;

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            var byName = itemsByKey.Values.FirstOrDefault(v =>
                string.IsNullOrWhiteSpace(GetNodeStr(v.Data, "existedId", "")) &&
                string.Equals(GetNodeStr(v.Data, "name", ""), itemName, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName;
        }

        var key = !string.IsNullOrWhiteSpace(itemId)
            ? itemId
            : $"name:{itemName}";

        var data = new JsonObject();
        if (!string.IsNullOrWhiteSpace(itemId))
            data["existedId"] = itemId;
        if (!string.IsNullOrWhiteSpace(itemName))
            data["name"] = itemName;

        var item = new NpcInventoryItemDisplay
        {
            Key = key,
            Data = data
        };

        itemsByKey[key] = item;
        if (!order.Contains(key, StringComparer.OrdinalIgnoreCase))
            order.Add(key);

        return item;
    }

    private static string ResolveInventoryKey(JsonElement item, int generatedIndex)
    {
        var existedId = GetStr(item, "existedId", "");
        if (!string.IsNullOrWhiteSpace(existedId))
            return existedId;

        var initialId = GetStr(item, "initialId", "");
        if (!string.IsNullOrWhiteSpace(initialId))
            return initialId;

        var name = GetStr(item, "name", "item");
        return $"name:{name}:{generatedIndex}";
    }

    private static JsonObject CloneJsonObject(JsonElement item)
    {
        return JsonNode.Parse(item.GetRawText())?.AsObject() ?? new JsonObject();
    }

    private static void MergeJsonObject(JsonObject target, JsonObject patch)
    {
        foreach (var prop in patch)
            target[prop.Key] = prop.Value?.DeepClone();
    }

    private static string GetNodeStr(JsonObject obj, string prop, string def)
    {
        var node = obj[prop];
        if (node == null) return def;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s) && s != null) return s;
            if (value.TryGetValue<int>(out var i)) return i.ToString();
            if (value.TryGetValue<long>(out var l)) return l.ToString();
            if (value.TryGetValue<double>(out var d)) return d.ToString();
            if (value.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        }

        return node.ToJsonString();
    }

    private static int GetNodeInt(JsonObject obj, string prop, int def = int.MinValue)
    {
        var node = obj[prop];
        if (node == null) return def;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l) && l <= int.MaxValue && l >= int.MinValue) return (int)l;
            if (value.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed)) return parsed;
        }

        return def;
    }

    private static JsonElement JsonObjectToElement(JsonObject obj)
    {
        return JsonSerializer.SerializeToElement(obj);
    }

    /// <summary>
    /// Renders any JSON object fields that weren't handled by explicit code.
    /// Only shows non-empty string/number/bool fields not in the exclusion set.
    /// </summary>
    private static void RenderExtraFields(List<string> lines, JsonElement obj, string[] excludeNames, string indent)
    {
        if (obj.ValueKind != JsonValueKind.Object) return;
        var exclude = new HashSet<string>(excludeNames);
        foreach (var prop in obj.EnumerateObject())
        {
            if (exclude.Contains(prop.Name)) continue;
            if (prop.Name.StartsWith("_")) continue;
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    var sv = prop.Value.GetString() ?? "";
                    if (sv.Length > 0)
                        lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {Markup.Escape(sv)}[/]");
                    break;
                case JsonValueKind.Number:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {prop.Value}[/]");
                    break;
                case JsonValueKind.True:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: да[/]");
                    break;
                case JsonValueKind.False:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: нет[/]");
                    break;
                case JsonValueKind.Array:
                    var arrItems = new List<string>();
                    foreach (var el in prop.Value.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                            arrItems.Add(el.GetString() ?? "");
                        else
                            arrItems.Add(el.GetRawText());
                    }
                    if (arrItems.Count > 0)
                        lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {Markup.Escape(string.Join(", ", arrItems))}[/]");
                    break;
            }
        }
    }

    private static string NpcFieldToRussian(string fieldName) => fieldName switch
    {
        "npcId" or "id" => "ID",
        "role" or "occupation" => "Роль",
        "race" => "Раса",
        "appearance" => "Внешность",
        "status" => "Статус",
        "relationshipLevel" => "Уровень отношений",
        "currentLocation" or "location" => "Локация",
        "lastInteraction" => "Последнее взаимодействие",
        "interactionType" => "Тип взаимодействия",
        "playerReputation" => "Репутация у игрока",
        "loyalty" => "Лояльность",
        "trust" => "Доверие",
        "fear" => "Страх",
        "respect" => "Уважение",
        "affection" => "Привязанность",
        "mood" or "emotion" => "Настроение",
        "health" or "hitPoints" => "Здоровье",
        "energy" or "stamina" => "Энергия",
        "level" => "Уровень",
        "class" => "Класс",
        "faction" or "factionName" => "Фракция",
        "alignment" or "morality" => "Мораль",
        "isHostile" => "Враждебен",
        "isAlly" => "Союзник",
        "isEssential" => "Ключевой персонаж",
        "isHidden" => "Скрыт",
        "progressionType" => "Тип развития",
        "playerCompanionDirective" => "Директива игрока",
        "personalityArchetype" => "Архетип личности",
        "culturalStance" => "Культурная позиция",
        "worldview" => "Мировоззрение",
        "rarity" => "Редкость",
        "age" => "Возраст",
        "goldAmount" or "money" or "gold" => "Золото",
        "combatStyle" => "Боевой стиль",
        "weakness" or "vulnerabilities" => "Уязвимости",
        "resistances" => "Сопротивления",
        "immunities" => "Иммунитеты",
        "changeReason" or "reason" => "Причина",
        "turn" or "turnNumber" => "Ход",
        "timestamp" => "Время",
        _ => fieldName
    };

    private async Task ShowQuests()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/quests/regular_quests.json");
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        var histDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/quest_history.json");

        // Collect all quests: (displayLabel, element, isSoul)
        var quests = new List<(string label, JsonElement el, bool isSoul)>();

        if (doc != null)
        {
            EnumerateArray(doc.RootElement, "quests", item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                var status = GetStr(item, "status", "Active").ToLower();
                var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                quests.Add(($"{icon} {name}", item, false));
            });
            // Also try root-level array
            if (quests.Count == 0)
                EnumerateJsonItems(doc.RootElement, item =>
                {
                    var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                    var status = GetStr(item, "status", "Active").ToLower();
                    var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                    quests.Add(($"{icon} {name}", item, false));
                });
        }

        if (soulDoc != null)
        {
            EnumerateArray(soulDoc.RootElement, "quests", item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                var status = GetStr(item, "status", "Active").ToLower();
                var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                quests.Add(($"🌟 {icon} {name}", item, true));
            });
            if (!quests.Any(q => q.isSoul))
                EnumerateJsonItems(soulDoc.RootElement, item =>
                {
                    var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                    var status = GetStr(item, "status", "Active").ToLower();
                    var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                    quests.Add(($"🌟 {icon} {name}", item, true));
                });
        }

        // Add history quests
        var historyQuests = new List<(string label, JsonElement el, JsonElement? rewardInfo, List<JsonElement> relatedChains)>();
        if (histDoc != null)
        {
            var rewardByQuestId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var chainEntries = new List<JsonElement>();

            if (histDoc.RootElement.TryGetProperty("questRewards", out var questRewards) &&
                questRewards.ValueKind == JsonValueKind.Array)
            {
                foreach (var reward in questRewards.EnumerateArray())
                {
                    if (reward.ValueKind != JsonValueKind.Object) continue;
                    var rewardQuestId = GetStr(reward, "questId", "");
                    if (!string.IsNullOrWhiteSpace(rewardQuestId))
                        rewardByQuestId[rewardQuestId] = reward;
                }
            }

            if (histDoc.RootElement.TryGetProperty("questChains", out var questChains) &&
                questChains.ValueKind == JsonValueKind.Array)
            {
                foreach (var chain in questChains.EnumerateArray())
                {
                    if (chain.ValueKind == JsonValueKind.Object)
                        chainEntries.Add(chain);
                }
            }

            if (histDoc.RootElement.TryGetProperty("questHistory", out var questHistory) &&
                questHistory.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in questHistory.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var name = GetStr(item, "questName", GetStr(item, "title", GetStr(item, "name", "?")));
                    var outcome = GetStr(item, "outcome", GetStr(item, "status", "")).ToLowerInvariant();
                    var icon = outcome switch { "completed" or "завершён" or "success" => "✅", "failed" or "провален" => "❌", _ => "📋" };
                    var questId = GetStr(item, "questId", "");
                    rewardByQuestId.TryGetValue(questId, out var rewardInfo);
                    var relatedChains = chainEntries
                        .Where(chain => HistoryChainMatchesQuest(chain, questId, name))
                        .Select(chain => chain.Clone())
                        .ToList();
                    historyQuests.Add(($"{icon} 📋 {name}", item, rewardInfo.ValueKind == JsonValueKind.Object ? rewardInfo : (JsonElement?)null, relatedChains));
                }
            }
            else if (histDoc.RootElement.TryGetProperty("quests", out var legacyHistory) &&
                     legacyHistory.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in legacyHistory.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var name = GetStr(item, "questName", GetStr(item, "title", GetStr(item, "name", "?")));
                    var outcome = GetStr(item, "outcome", GetStr(item, "status", "")).ToLowerInvariant();
                    var icon = outcome switch { "completed" or "завершён" or "success" => "✅", "failed" or "провален" => "❌", _ => "📋" };
                    historyQuests.Add(($"{icon} 📋 {name}", item, null, new List<JsonElement>()));
                }
            }
        }

        if (quests.Count == 0 && historyQuests.Count == 0)
        {
            ShowEmptyPanel(_loc.T("quests"), "Квесты не обнаружены");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choices = new List<string>();
            foreach (var (label, _, isSoul) in quests)
                choices.Add(isSoul ? $"[purple]{Markup.Escape(label)}[/]" : Markup.Escape(label));
            foreach (var (label, _, _, _) in historyQuests)
                choices.Add($"[dim]{Markup.Escape(label)}[/]");
            choices.Add("[dim]← Назад[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold green]📜 {_loc.T("quests")}[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0) break;

            if (selIdx < quests.Count)
            {
                await ShowQuestDetailPanel(quests[selIdx].el, quests[selIdx].isSoul, false);
            }
            else
            {
                var histIdx = selIdx - quests.Count;
                if (histIdx >= 0 && histIdx < historyQuests.Count)
                    await ShowQuestDetailPanel(historyQuests[histIdx].el, false, true, historyQuests[histIdx].rewardInfo, historyQuests[histIdx].relatedChains);
            }
        }
    }

    private static void RenderReadableJsonValue(List<string> lines, string label, JsonElement value, string indent, HashSet<string>? excluded = null, int depth = 0)
    {
        if (depth > 5)
            return;

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}:[/]");
                foreach (var prop in value.EnumerateObject())
                {
                    if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (excluded != null && excluded.Contains(prop.Name))
                        continue;
                    RenderReadableJsonValue(lines, prop.Name, prop.Value, indent + "  ", null, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}:[/]");
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        RenderReadableJsonValue(lines, $"элемент {++index}", item, indent + "  ", null, depth + 1);
                    }
                    else
                    {
                        var itemText = item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString() ?? "",
                            JsonValueKind.Number => item.ToString(),
                            JsonValueKind.True => "да",
                            JsonValueKind.False => "нет",
                            _ => item.ToString()
                        };

                        if (!string.IsNullOrWhiteSpace(itemText))
                            lines.Add($"{indent}  [dim]• {Markup.Escape(itemText)}[/]");
                    }
                }
                break;
            case JsonValueKind.String:
                var sv = value.GetString() ?? "";
                if (sv.Length > 0)
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: {Markup.Escape(sv)}[/]");
                break;
            case JsonValueKind.Number:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: {value}[/]");
                break;
            case JsonValueKind.True:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: да[/]");
                break;
            case JsonValueKind.False:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: нет[/]");
                break;
        }
    }

    private async Task ShowQuestDetailPanel(JsonElement q, bool isSoul, bool isHistory, JsonElement? historyRewardInfo = null, List<JsonElement>? relatedChains = null)
    {
        var lines = new List<string>();
        var name = GetStr(q, "questName", GetStr(q, "title", "???"));
        var prefix = isSoul ? "[purple]🌟 Квест души[/] — " : "";
        lines.Add($"{prefix}[bold green]{Markup.Escape(name)}[/]");

        var questGiver = GetStr(q, "questGiver", "");
        if (!string.IsNullOrEmpty(questGiver))
            lines.Add($"  👤 Квестодатель: [cyan]{Markup.Escape(questGiver)}[/]");

        var status = GetStr(q, "status", "Active");
        var statusColor = status.ToLower() switch
        {
            "completed" or "завершён" => "green",
            "failed" or "провален" => "red",
            _ => "yellow"
        };
        lines.Add($"  📌 Статус: [{statusColor}]{Markup.Escape(status)}[/]");

        if (isHistory)
        {
            var outcome = GetStr(q, "outcome", "");
            if (!string.IsNullOrEmpty(outcome))
            {
                var oColor = outcome.ToLower() switch { "completed" or "завершён" => "green", "failed" or "провален" => "red", _ => "grey" };
                lines.Add($"  🏁 Исход: [{oColor}]{Markup.Escape(outcome)}[/]");
            }
            var completionTurn = GetStr(q, "completionTurn", "");
            if (!string.IsNullOrEmpty(completionTurn))
                lines.Add($"  🔢 Ход завершения: [cyan]{Markup.Escape(completionTurn)}[/]");
            var completionDate = GetStr(q, "completionDate", "");
            if (!string.IsNullOrEmpty(completionDate))
                lines.Add($"  🕒 Дата завершения: [cyan]{Markup.Escape(completionDate)}[/]");
            var historyExperience = GetStr(q, "experience", "");
            if (!string.IsNullOrEmpty(historyExperience))
                lines.Add($"  ⭐ Получено опыта: [yellow]{Markup.Escape(historyExperience)}[/]");
            var reputation = GetStr(q, "reputation", "");
            if (!string.IsNullOrEmpty(reputation))
                lines.Add($"  🤝 Репутация: [yellow]{Markup.Escape(reputation)}[/]");
            var incarnationNumber = GetStr(q, "incarnationNumber", "");
            if (!string.IsNullOrEmpty(incarnationNumber))
                lines.Add($"  🔄 Инкарнация: [white]{Markup.Escape(incarnationNumber)}[/]");
        }

        var background = GetStr(q, "questBackground", "");
        if (!string.IsNullOrEmpty(background))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📖 {Markup.Escape(background)}[/]");
        }

        var desc = GetStr(q, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  {Markup.Escape(desc)}");
        }

        // Objectives
        if (q.TryGetProperty("objectives", out var objectives) && objectives.ValueKind == JsonValueKind.Array && objectives.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🎯 Цели:[/]");
            foreach (var obj in objectives.EnumerateArray())
            {
                var objDesc = GetStr(obj, "description", "???");
                var objStatus = GetStr(obj, "status", "Active").ToLower();
                var objIcon = objStatus switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                lines.Add($"    {objIcon} {Markup.Escape(objDesc)}");
            }
        }

        // Rewards
        if (q.TryGetProperty("rewards", out var rewards) && rewards.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]🎁 Награды:[/]");
            var xp = GetInt(rewards, "experience", 0);
            if (xp > 0) lines.Add($"    ⭐ Опыт: [yellow]{xp}[/]");
            var money = GetInt(rewards, "money", 0);
            if (money > 0) lines.Add($"    💰 Деньги: [yellow]{money}[/]");
            if (rewards.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var itemStr = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "?" : item.GetRawText();
                    lines.Add($"    📦 {Markup.Escape(itemStr)}");
                }
            }
            var other = GetStr(rewards, "other", "");
            if (!string.IsNullOrEmpty(other))
                lines.Add($"    🔹 {Markup.Escape(other)}");
        }

        // Failure consequences
        var failCons = GetStr(q, "failureConsequences", "");
        if (!string.IsNullOrEmpty(failCons))
        {
            lines.Add("");
            lines.Add($"  [bold red]⚠ Последствия провала:[/] {Markup.Escape(failCons)}");
        }

        // Details log
        if (q.TryGetProperty("detailsLog", out var detailsLog) && detailsLog.ValueKind == JsonValueKind.Array)
        {
            var logEntries = new List<string>();
            foreach (var entry in detailsLog.EnumerateArray())
            {
                var entryStr = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : entry.GetRawText();
                if (!string.IsNullOrEmpty(entryStr))
                    logEntries.Add(entryStr);
            }
            if (logEntries.Count > 0)
            {
                lines.Add("");
                lines.Add($"  [bold]📝 Журнал ({logEntries.Count} записей):[/]");
                foreach (var entry in logEntries)
                    lines.Add($"    [dim]• {Markup.Escape(entry)}[/]");
            }
        }

        if (isHistory && historyRewardInfo.HasValue && historyRewardInfo.Value.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]🎁 Фактически получено:[/]");

            if (historyRewardInfo.Value.TryGetProperty("itemsReceived", out var itemsReceived) &&
                itemsReceived.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsReceived.EnumerateArray())
                    lines.Add($"    📦 {Markup.Escape(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "?" : item.ToString())}");
            }

            if (historyRewardInfo.Value.TryGetProperty("skillsUnlocked", out var skillsUnlocked) &&
                skillsUnlocked.ValueKind == JsonValueKind.Array)
            {
                foreach (var skill in skillsUnlocked.EnumerateArray())
                    lines.Add($"    ⚔️ Навык: {Markup.Escape(skill.ValueKind == JsonValueKind.String ? skill.GetString() ?? "?" : skill.ToString())}");
            }

            if (historyRewardInfo.Value.TryGetProperty("relationshipChanges", out var relationshipChanges) &&
                relationshipChanges.ValueKind == JsonValueKind.Array)
            {
                foreach (var relation in relationshipChanges.EnumerateArray())
                    lines.Add($"    🤝 Отношение: {Markup.Escape(relation.ValueKind == JsonValueKind.String ? relation.GetString() ?? "?" : relation.ToString())}");
            }
        }

        if (isHistory && relatedChains is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("  [bold]🔗 Связанные цепочки:[/]");
            foreach (var chain in relatedChains)
            {
                var chainId = GetStr(chain, "chainId", "chain");
                var currentQuest = GetStr(chain, "currentQuest", "");
                var progress = GetStr(chain, "progress", "");
                var unlocked = chain.TryGetProperty("unlocked", out var unlockedEl) && unlockedEl.ValueKind == JsonValueKind.True;
                var unlockedLabel = unlocked ? "[green]разблокирована[/]" : "[dim]скрыта[/]";
                var chainLine = $"    🔗 [white]{Markup.Escape(chainId)}[/] — {unlockedLabel}";
                if (!string.IsNullOrEmpty(currentQuest))
                    chainLine += $" [dim](текущий квест: {Markup.Escape(currentQuest)})[/]";
                if (!string.IsNullOrEmpty(progress))
                    chainLine += $" [dim]• {Markup.Escape(progress)}[/]";
                lines.Add(chainLine);
            }
        }

        var borderColor = isSoul ? Color.Purple : Color.Green;
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 📜 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(1, 1),
            Expand = true
        });
        await WaitForKeyWithImage("quest", name, GetStr(q, "image_prompt", ""), GetStr(q, "questId", name));
    }

    private static bool HistoryChainMatchesQuest(JsonElement chain, string questId, string questName)
    {
        var currentQuest = GetStr(chain, "currentQuest", "");
        if (!string.IsNullOrWhiteSpace(questId) &&
            string.Equals(currentQuest, questId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(questName) &&
            string.Equals(currentQuest, questName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private async Task ShowMap()
    {
        var locDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
        var mapDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_map.json");

        if (locDoc == null) { ShowEmptyPanel(_loc.T("map"), "Местоположение неизвестно"); return; }

        var root = locDoc.RootElement;
        var curName = GetStr(root, "name", "Неизвестно");
        var curX = 0; var curY = 0; var curZ = 0;
        if (root.TryGetProperty("coordinates", out var coords))
        {
            curX = GetInt(coords, "x", 0);
            curY = GetInt(coords, "y", 0);
            curZ = GetInt(coords, "z", 0);
        }

        while (true)
        {
            // Build interactive menu: current location + adjacent + discovered
            var menuItems = new List<(string Label, string Action, JsonElement? Data)>();

            // Current location — always first
            menuItems.Add(($"[bold green]📍 {Markup.Escape(curName)}[/] [dim](текущая)[/]", "current", root));

            // Adjacent locations
            if (root.TryGetProperty("adjacencyMap", out var adj) && adj.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in adj.EnumerateArray())
                {
                    var aName = GetStr(entry, "name", "");
                    if (string.IsNullOrEmpty(aName))
                        aName = GetStr(entry, "targetLocationId", "?");
                    var direction = GetStr(entry, "direction", "");
                    var distance = GetStr(entry, "distance", "");
                    var linkState = GetStr(entry, "linkState", "");
                    var stateColor = linkState.ToLower() switch
                    {
                        "dangerous" => "red", "hidden" => "grey", "blocked" => "maroon", _ => "aqua"
                    };
                    var dirStr = !string.IsNullOrEmpty(direction) ? $" ({Markup.Escape(direction)})" : "";
                    var distStr = !string.IsNullOrEmpty(distance) ? $" [dim]{Markup.Escape(distance)}[/]" : "";
                    var stateStr = !string.IsNullOrEmpty(linkState) && linkState.ToLower() != "safe"
                        ? $" [yellow][[{Markup.Escape(linkState)}]][/]" : "";
                    menuItems.Add(($"  🧭 [{stateColor}]{Markup.Escape(aName)}[/]{dirStr}{distStr}{stateStr}", "adjacent", entry));
                }
            }

            var mapRoot = mapDoc != null &&
                          mapDoc.RootElement.TryGetProperty("worldMapUpdates", out var wrappedMapRoot) &&
                          wrappedMapRoot.ValueKind == JsonValueKind.Object
                ? wrappedMapRoot
                : mapDoc?.RootElement;

            // Discovered locations from world_map
            if (mapRoot.HasValue && mapRoot.Value.TryGetProperty("newLocations", out var newLocs) &&
                newLocs.ValueKind == JsonValueKind.Array)
            {
                foreach (var loc in newLocs.EnumerateArray())
                {
                    var n = GetStr(loc, "name", "?");
                    var lt = GetStr(loc, "locationType", "");
                    if (n == curName) continue;
                    menuItems.Add(($"  🗺 [dim]{Markup.Escape(n)}[/]" +
                        (!string.IsNullOrEmpty(lt) ? $" [dim]({Markup.Escape(lt)})[/]" : ""), "discovered", loc));
                }
            }

            if (mapRoot.HasValue && mapRoot.Value.TryGetProperty("locationUpdates", out var updatedLocs) &&
                updatedLocs.ValueKind == JsonValueKind.Array)
            {
                foreach (var loc in updatedLocs.EnumerateArray())
                {
                    var n = GetStr(loc, "name", "?");
                    var lt = GetStr(loc, "locationType", "");
                    if (n == curName) continue;
                    menuItems.Add(($"  🗺 [dim]{Markup.Escape(n)}[/] [dim](обновлено)[/]" +
                        (!string.IsNullOrEmpty(lt) ? $" [dim]({Markup.Escape(lt)})[/]" : ""), "discovered", loc));
                }
            }

            var choices = menuItems.Select(m => m.Label).ToList();
            choices.Add("[grey]← Назад[/]");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold green]🗺 {_loc.T("map")}[/]  [dim](выберите локацию для подробностей)[/]")
                .PageSize(20)
                .HighlightStyle(new Style(Color.Green))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= menuItems.Count) break;

            var (_, action, data) = menuItems[selIdx];
            if (action == "current")
                await ShowLocationDetailPanel(root, true);
            else if (data.HasValue)
                await ShowLocationDetailPanel(data.Value, false);
        }
    }

    private async Task ShowLocationDetailPanel(JsonElement loc, bool isCurrent)
    {
        var name = GetStr(loc, "name", GetStr(loc, "targetLocationId", "Неизвестно"));
        var playerLevel = await GetPlayerLevelAsync();
        var lines = new List<string>();
        lines.Add($"[bold green]{(isCurrent ? "📍" : "🗺")} {Markup.Escape(name)}[/]");
        lines.Add("");

        // Description
        var desc = GetStr(loc, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add($"[white]{Markup.Escape(desc)}[/]");
            lines.Add("");
        }

        var lastEvents = GetStr(loc, "lastEventsDescription", "");
        if (!string.IsNullOrEmpty(lastEvents))
        {
            lines.Add($"[dim italic]📋 {Markup.Escape(lastEvents)}[/]");
            lines.Add("");
        }

        // Basic info
        var locType = GetStr(loc, "locationType", "");
        if (!string.IsNullOrEmpty(locType))
            lines.Add($"  📋 Тип: [cyan]{Markup.Escape(locType)}[/]");

        var indoorType = GetStr(loc, "indoorType", "");
        if (!string.IsNullOrEmpty(indoorType))
        {
            var indoorLabel = indoorType switch
            {
                "Building" => "🏠 Здание",
                "Dungeon" => "🏰 Подземелье",
                "CaveSystem" => "🕳 Пещера",
                "Vehicle" => "🚗 Транспорт",
                "UniqueIndoor" => "✨ Уникальное",
                _ => Markup.Escape(indoorType)
            };
            lines.Add($"  {indoorLabel}");
        }

        var biome = GetStr(loc, "biome", "");
        if (!string.IsNullOrEmpty(biome))
            lines.Add($"  🌿 Биом: [green]{Markup.Escape(biome)}[/]");

        // Features
        if (loc.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
        {
            var featureStrs = new List<string>();
            foreach (var f in features.EnumerateArray())
            {
                var fStr = f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : f.ToString();
                if (!string.IsNullOrEmpty(fStr)) featureStrs.Add(fStr);
            }
            if (featureStrs.Count > 0)
                lines.Add($"  ✦ Особенности: [cyan]{Markup.Escape(string.Join(", ", featureStrs))}[/]");
        }

        if (loc.TryGetProperty("coordinates", out var coords))
            lines.Add($"  📐 Координаты: [dim][[{GetInt(coords, "x", 0)}, {GetInt(coords, "y", 0)}, {GetInt(coords, "z", 0)}]][/]");

        // Faction control
        if (loc.TryGetProperty("factionControl", out var fc) && fc.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in fc.EnumerateArray())
            {
                var fName = GetStr(f, "factionName", GetStr(f, "factionId", GetStr(f, "name", "?")));
                var fLevel = GetStr(f, "controlLevel", "");
                var fType = GetStr(f, "controlType", "");
                var ctLabel = fType.ToLower() switch
                {
                    "military" => "⚔ Военный",
                    "economic" => "💰 Экономический",
                    "social" => "💬 Социальный",
                    "covert" => "🕵 Скрытый",
                    _ => !string.IsNullOrEmpty(fType) ? Markup.Escape(fType) : ""
                };
                var line = $"  🏰 Фракция: [yellow]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(ctLabel)) line += $" [dim]({ctLabel})[/]";
                if (!string.IsNullOrEmpty(fLevel)) line += $" контроль: [white]{Markup.Escape(fLevel)}%[/]";
                lines.Add(line);
            }
        }

        // Difficulty profiles with visual bars + human-readable labels
        void ShowDifficulty(string label, string propName)
        {
            if (!loc.TryGetProperty(propName, out var diff) || diff.ValueKind != JsonValueKind.Object) return;
            var combat = GetInt(diff, "combat", 0);
            var env = GetInt(diff, "environment", 0);
            var social = GetInt(diff, "social", 0);
            var explore = GetInt(diff, "exploration", 0);

            var (overallLabel, overallColor) = GetProfileDifficultyLabel(diff, playerLevel);

            lines.Add("");
            lines.Add($"  [bold]{label}:[/]  [{overallColor}]{overallLabel}[/] [dim](ур. {playerLevel})[/]");
            lines.Add($"    ⚔ Бой:          {DifficultyBar(combat)}  {DifficultyWithLabel(combat, playerLevel)}");
            lines.Add($"    🌿 Окружение:    {DifficultyBar(env)}  {DifficultyWithLabel(env, playerLevel)}");
            lines.Add($"    💬 Социальная:   {DifficultyBar(social)}  {DifficultyWithLabel(social, playerLevel)}");
            lines.Add($"    🔍 Исследование: {DifficultyBar(explore)}  {DifficultyWithLabel(explore, playerLevel)}");
        }

        ShowDifficulty("🔒 Сложность (для своих)", "internalDifficultyProfile");
        ShowDifficulty("⚠ Сложность (для чужих)", "externalDifficultyProfile");

        // Active threats — FULL detail
        if (loc.TryGetProperty("activeThreats", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold red]⚠ Активные угрозы ({threats.GetArrayLength()}):[/]");
            foreach (var t in threats.EnumerateArray())
                RenderThreatFull(lines, t);
        }

        // Adjacent locations — enriched with linkType, description, estimated difficulty
        if (loc.TryGetProperty("adjacencyMap", out var adj) && adj.ValueKind == JsonValueKind.Array && adj.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🧭 Соседние локации:[/]");
            foreach (var entry in adj.EnumerateArray())
            {
                var aName = GetStr(entry, "name", GetStr(entry, "targetLocationId", "?"));
                var dir = GetStr(entry, "direction", "");
                var dist = GetStr(entry, "distance", "");
                var linkState = GetStr(entry, "linkState", "");
                var linkType = GetStr(entry, "linkType", "");
                var shortDesc = GetStr(entry, "shortDescription", "");
                var linkColor = linkState.ToLowerInvariant() switch
                {
                    "dangerous" => "red",
                    "hidden" => "grey",
                    "blocked" or "requires key" => "maroon",
                    "safe" => "green",
                    _ => "cyan"
                };
                var line = $"    → [{linkColor}]{Markup.Escape(aName)}[/]";
                if (!string.IsNullOrEmpty(linkType)) line += $" [dim]⟨{Markup.Escape(linkType)}⟩[/]";
                if (!string.IsNullOrEmpty(dir)) line += $" ({Markup.Escape(dir)})";
                if (!string.IsNullOrEmpty(dist)) line += $" [dim]{Markup.Escape(dist)}[/]";
                if (!string.IsNullOrEmpty(linkState) && linkState.ToLowerInvariant() != "safe")
                    line += $" [{linkColor}]({Markup.Escape(linkState)})[/]";
                lines.Add(line);
                if (!string.IsNullOrEmpty(shortDesc))
                    lines.Add($"      [dim]{Markup.Escape(shortDesc)}[/]");

                // Estimated difficulty for adjacent location
                if (entry.TryGetProperty("estimatedExternalDifficultyProfile", out var estExt) && estExt.ValueKind == JsonValueKind.Object)
                {
                    var (estLabel, estColor) = GetProfileDifficultyLabel(estExt, playerLevel);
                    lines.Add($"      [dim]Сложность: [{estColor}]{estLabel}[/][/]");
                }
                else if (entry.TryGetProperty("estimatedInternalDifficultyProfile", out var estInt) && estInt.ValueKind == JsonValueKind.Object)
                {
                    var (estLabel, estColor) = GetProfileDifficultyLabel(estInt, playerLevel);
                    lines.Add($"      [dim]Сложность: [{estColor}]{estLabel}[/][/]");
                }
            }
        }

        // Location storages
        if (loc.TryGetProperty("locationStorages", out var storages) && storages.ValueKind == JsonValueKind.Array && storages.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📦 Хранилища:[/]");
            foreach (var st in storages.EnumerateArray())
            {
                var sName = GetStr(st, "name", "Хранилище");
                var sCap = GetStr(st, "capacity", "");
                var sVol = GetStr(st, "volume", "");
                var sOwnerName = "";
                var sOwnerType = "";
                if (st.TryGetProperty("owner", out var own) && own.ValueKind == JsonValueKind.Object)
                {
                    sOwnerName = GetStr(own, "ownerName", "");
                    sOwnerType = GetStr(own, "ownerType", "");
                }
                var hasAccess = st.TryGetProperty("hasFullAccess", out var ha) && ha.ValueKind == JsonValueKind.True;
                var accessIcon = hasAccess ? "[green]✓ доступ[/]" : "[red]✗ нет доступа[/]";

                // Owner type label
                var ownerTypeLabel = sOwnerType.ToLower() switch
                {
                    "player" => "👤 Личное",
                    "faction" => "🏛️ Фракционное",
                    "shared" => "🤝 Общее",
                    _ => ""
                };

                var sLine = $"    📦 [white]{Markup.Escape(sName)}[/] {accessIcon}";
                if (!string.IsNullOrEmpty(ownerTypeLabel)) sLine += $" [dim]{ownerTypeLabel}[/]";
                lines.Add(sLine);

                // Capacity and volume on detail line
                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(sCap)) detailParts.Add($"вместимость: {Markup.Escape(sCap)} стаков");
                if (!string.IsNullOrEmpty(sVol)) detailParts.Add($"объём: {Markup.Escape(sVol)} дм³");
                if (!string.IsNullOrEmpty(sOwnerName)) detailParts.Add($"владелец: {Markup.Escape(sOwnerName)}");
                if (detailParts.Count > 0)
                    lines.Add($"      [dim]{string.Join(" │ ", detailParts)}[/]");

                var sDesc = GetStr(st, "description", "");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(sDesc)}[/]");

                // Authorized users for shared storages
                if (st.TryGetProperty("authorizedUsers", out var authUsers) &&
                    authUsers.ValueKind == JsonValueKind.Array && authUsers.GetArrayLength() > 0)
                {
                    var userNames = new List<string>();
                    foreach (var u in authUsers.EnumerateArray())
                    {
                        var uName = GetStr(u, "playerName", GetStr(u, "name", ""));
                        if (!string.IsNullOrEmpty(uName)) userNames.Add(Markup.Escape(uName));
                    }
                    if (userNames.Count > 0)
                        lines.Add($"      🤝 Доступ: [cyan]{string.Join(", ", userNames)}[/]");
                }

                // Contents — show item names, not just count
                if (st.TryGetProperty("contents", out var cont) && cont.ValueKind == JsonValueKind.Array)
                {
                    var contCount = cont.GetArrayLength();
                    if (contCount == 0)
                    {
                        lines.Add($"      [dim]Пусто[/]");
                    }
                    else
                    {
                        lines.Add($"      Предметов: [white]{contCount}[/]");
                        var shown = 0;
                        foreach (var ci in cont.EnumerateArray())
                        {
                            if (++shown > 8)
                            {
                                lines.Add($"        [dim]...и ещё {contCount - 8}[/]");
                                break;
                            }
                            var ciName = GetStr(ci, "name", "?");
                            var ciQty = GetStr(ci, "quantity", "1");
                            var ciLine = $"        • {Markup.Escape(ciName)}";
                            if (!string.IsNullOrEmpty(ciQty) && ciQty != "1") ciLine += $" ×{Markup.Escape(ciQty)}";
                            lines.Add(ciLine);
                        }
                    }
                }
            }
        }

        // Event log / history
        if (loc.TryGetProperty("eventDescriptions", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📜 Журнал событий:[/]");
            var evCount = 0;
            foreach (var ev in events.EnumerateArray())
            {
                if (++evCount > 10) { lines.Add($"    [dim]...и ещё {events.GetArrayLength() - 10}[/]"); break; }
                var evStr = ev.ValueKind == JsonValueKind.String ? ev.GetString() ?? "" : GetStr(ev, "description", ev.GetRawText());
                lines.Add($"    [dim]• {Markup.Escape(evStr)}[/]");
            }
        }

        // Image prompt hint
        var imgPrompt = GetStr(loc, "image_prompt", "");
        if (!string.IsNullOrEmpty(imgPrompt))
        {
            lines.Add("");
            lines.Add($"  [dim italic]🖼️ {Markup.Escape(imgPrompt)}[/]");
        }

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" {(isCurrent ? "📍" : "🗺")} {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        });
        await WaitForKeyWithImage("location", name, imgPrompt, GetStr(loc, "locationId", GetStr(loc, "targetLocationId", name)));
    }

    /// <summary>Returns difficulty value with a colored label, e.g. "25 Нормально".</summary>
    private static string DifficultyWithLabel(int value, int playerLevel)
    {
        var (label, color) = GetDifficultyLabel(value, playerLevel);
        return $"[white]{value}[/] [{color}]{label}[/]";
    }

    /// <summary>Renders a colored bar from 0..200 for difficulty display.</summary>
    private static string DifficultyBar(int value)
    {
        var clamped = Math.Clamp(value, 0, 200);
        var filled = Math.Min(clamped / 10, 10);
        var empty = 10 - filled;
        var color = value switch { <= 20 => "green", <= 40 => "yellow", <= 60 => "orange1", _ => "red" };
        return ConsoleLayout.CreateBar(filled, 10, color);
    }

    /// <summary>
    /// Returns a human-readable difficulty label based on the difficulty value and player level.
    /// Uses the scaling table from Block 20 (Rule 20.0.A).
    /// </summary>
    private static (string label, string color) GetDifficultyLabel(int difficulty, int playerLevel)
    {
        // Scaling thresholds from Block 20.0.A
        var (standardMax, hardMax) = playerLevel switch
        {
            <= 5  => (25, 40),
            <= 10 => (40, 55),
            <= 20 => (55, 70),
            <= 30 => (70, 85),
            <= 45 => (85, 100),
            <= 60 => (100, 120),
            <= 80 => (120, 140),
            <= 100 => (150, 180),
            _ => (150, 180)
        };

        if (difficulty <= 0)
            return ("Безопасно", "green");
        if (difficulty <= standardMax / 2)
            return ("Легко", "green3");
        if (difficulty <= standardMax)
            return ("Нормально", "yellow");
        if (difficulty <= hardMax)
            return ("Сложно", "orange1");
        if (difficulty <= hardMax + (hardMax - standardMax))
            return ("Очень сложно", "red");
        return ("☠ СМЕРТЕЛЬНО", "bold red");
    }

    /// <summary>
    /// Returns the overall difficulty label from a difficulty profile (max of all facets).
    /// </summary>
    private static (string label, string color) GetProfileDifficultyLabel(JsonElement profile, int playerLevel)
    {
        var maxDiff = 0;
        foreach (var facet in new[] { "combat", "environment", "social", "exploration" })
        {
            if (profile.TryGetProperty(facet, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var fv))
                maxDiff = Math.Max(maxDiff, fv);
        }
        return GetDifficultyLabel(maxDiff, playerLevel);
    }

    /// <summary>
    /// Reads the current player level from experience.json or player_status.json.
    /// </summary>
    private async Task<int> GetPlayerLevelAsync()
    {
        var expJson = await _stateManager.LoadGameStateFileAsync("game_state/player/experience.json");
        if (expJson != null)
        {
            if (expJson.RootElement.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number && lvl.TryGetInt32(out var lv))
                return lv;
            if (expJson.RootElement.TryGetProperty("playerLevel", out var pl) && pl.ValueKind == JsonValueKind.Number && pl.TryGetInt32(out var plv))
                return plv;
        }
        var statusJson = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
        if (statusJson != null)
        {
            if (statusJson.RootElement.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number && lvl.TryGetInt32(out var slv))
                return slv;
        }
        return 1;
    }

    /// <summary>Renders a compact one-line threat summary for location overview.</summary>
    private static void RenderThreatSummary(List<string> lines, JsonElement t)
    {
        var tName = GetStr(t, "name", GetStr(t, "threatName", "Неизвестная угроза"));
        var intensity = GetInt(t, "intensity", -1);

        var line = $"    🔥 [red]{Markup.Escape(tName)}[/]";
        if (intensity >= 0)
            line += $" [dim](сила: {intensity})[/]";

        // Show current activity if present
        if (t.TryGetProperty("currentActivity", out var act) && act.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(act, "activityName", GetStr(act, "name", ""));
            if (!string.IsNullOrEmpty(actName))
                line += $" — [yellow]{Markup.Escape(actName)}[/]";
        }

        lines.Add(line);

        // Long-term goal
        var goal = GetStr(t, "longTermGoal", "");
        if (!string.IsNullOrEmpty(goal))
            lines.Add($"      [dim]Цель: {Markup.Escape(goal)}[/]");
    }

    /// <summary>Renders full threat details for the detail panel.</summary>
    private static void RenderThreatFull(List<string> lines, JsonElement t)
    {
        var tName = GetStr(t, "name", GetStr(t, "threatName", "Неизвестная угроза"));
        var intensity = GetInt(t, "intensity", -1);

        lines.Add("");
        var header = $"    🔥 [bold red]{Markup.Escape(tName)}[/]";
        if (intensity >= 0)
            header += $"  [dim](сила: {intensity})[/]";
        lines.Add(header);

        // Threat archetype
        if (t.TryGetProperty("threatArchetype", out var arch) && arch.ValueKind == JsonValueKind.Object)
        {
            var motivation = GetStr(arch, "motivation", GetStr(arch, "customMotivation", ""));
            var method = GetStr(arch, "method", GetStr(arch, "customMethod", ""));
            if (!string.IsNullOrEmpty(motivation) || !string.IsNullOrEmpty(method))
            {
                var archStr = "";
                if (!string.IsNullOrEmpty(motivation)) archStr += $"Мотивация: {Markup.Escape(motivation)}";
                if (!string.IsNullOrEmpty(method)) archStr += (archStr.Length > 0 ? " | " : "") + $"Метод: {Markup.Escape(method)}";
                lines.Add($"      [dim]{archStr}[/]");
            }
        }

        // Long-term goal
        var goal = GetStr(t, "longTermGoal", "");
        if (!string.IsNullOrEmpty(goal))
            lines.Add($"      🎯 Цель: [yellow]{Markup.Escape(goal)}[/]");

        // Current activity with progress
        if (t.TryGetProperty("currentActivity", out var act) && act.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(act, "activityName", GetStr(act, "name", ""));
            var actDesc = GetStr(act, "description", "");
            var totalTime = GetInt(act, "totalTimeCostMinutes", 0);
            var spentTime = GetInt(act, "timeSpentMinutes", 0);
            var activeState = GetStr(act, "activeState", "");

            if (!string.IsNullOrEmpty(actName))
            {
                var actLine = $"      ⚡ Действие: [cyan]{Markup.Escape(actName)}[/]";
                if (!string.IsNullOrEmpty(activeState))
                    actLine += $" [dim]({Markup.Escape(activeState)})[/]";
                lines.Add(actLine);
            }
            if (!string.IsNullOrEmpty(actDesc))
                lines.Add($"        [dim italic]{Markup.Escape(actDesc)}[/]");

            if (totalTime > 0)
            {
                var pct = (int)Math.Clamp((long)spentTime * 100 / totalTime, 0, 100);
                var filled = pct / 10;
                var empty = 10 - filled;
                var barColor = pct >= 80 ? "red" : pct >= 50 ? "orange1" : "yellow";
                lines.Add($"        Прогресс: [{barColor}]{new string('█', filled)}[/][dim]{new string('░', empty)}[/] {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
            }
        }
        else
        {
            lines.Add($"      [dim]💤 Угроза неактивна (бездействует)[/]");
        }

        // Impact profile
        if (t.TryGetProperty("impactProfile", out var imp) && imp.ValueKind == JsonValueKind.Object)
        {
            var target = GetStr(imp, "primaryTargetName", GetStr(imp, "primaryTargetId", ""));
            var targetType = GetStr(imp, "primaryTargetType", "");
            var impact = GetStr(imp, "primaryImpact", "");
            var impValue = GetInt(imp, "baseImpactValue", -1);

            if (!string.IsNullOrEmpty(target) || !string.IsNullOrEmpty(impact))
            {
                var impLine = "      💥 Эффект:";
                if (!string.IsNullOrEmpty(impact)) impLine += $" [orange1]{Markup.Escape(impact)}[/]";
                if (impValue >= 0) impLine += $" (сила: {impValue})";
                if (!string.IsNullOrEmpty(target)) impLine += $" → [white]{Markup.Escape(target)}[/]";
                if (!string.IsNullOrEmpty(targetType)) impLine += $" [dim]({Markup.Escape(targetType)})[/]";
                lines.Add(impLine);
            }
        }
    }

    private static void RenderWorldEventDetailed(List<string> lines, JsonElement item)
    {
        var title = GetStr(item, "eventTitle", GetStr(item, "title", GetStr(item, "name", "")));
        var summary = GetStr(item, "summary", GetStr(item, "narrativeSummary", ""));
        var desc = GetStr(item, "description", "");
        var time = GetStr(item, "timestamp", GetStr(item, "dateTime", GetStr(item, "date", "")));
        var visibility = GetStr(item, "visibility", "");
        var location = GetStr(item, "location", GetStr(item, "eventLocation", ""));
        var category = GetStr(item, "category", GetStr(item, "eventCategory", GetStr(item, "type", "")));

        var headline = !string.IsNullOrEmpty(title) ? title
            : !string.IsNullOrEmpty(summary) ? summary
            : desc;
        if (string.IsNullOrEmpty(headline)) return;

        var visColor = visibility.ToLowerInvariant() switch
        {
            "public" => "green",
            "regional" => "cyan",
            "secret" => "red",
            "faction-internal" => "orange1",
            _ => "dim"
        };

        var line = $"[yellow bold]• {Markup.Escape(headline)}[/]";
        if (!string.IsNullOrEmpty(time))
            line = $"[dim]{Markup.Escape(time)}[/] " + line;
        lines.Add(line);

        if (!string.IsNullOrEmpty(desc) && desc != headline)
            lines.Add($"  [white]{Markup.Escape(desc)}[/]");
        if (!string.IsNullOrEmpty(summary) && summary != headline && summary != desc)
            lines.Add($"  [white]{Markup.Escape(summary)}[/]");

        var meta = new List<string>();
        if (!string.IsNullOrEmpty(visibility))
            meta.Add($"[{visColor}]{Markup.Escape(visibility)}[/]");
        if (!string.IsNullOrEmpty(location))
            meta.Add($"📍 {Markup.Escape(location)}");
        if (!string.IsNullOrEmpty(category))
            meta.Add($"📂 {Markup.Escape(category)}");
        if (meta.Count > 0)
            lines.Add($"  [dim]{string.Join(" │ ", meta)}[/]");

        AppendWorldNewsFlexibleField(lines, "👥 Участники", item, "involvedNPCs");
        AppendWorldNewsFlexibleField(lines, "🏛️ Затронутые фракции", item, "affectedFactions");
        AppendWorldNewsFlexibleField(lines, "📍 Затронутые локации", item, "affectedLocations");
        AppendWorldNewsFlexibleField(lines, "⚖ Последствия", item, "consequences");
        AppendWorldNewsFlexibleField(lines, "🏁 Итог", item, "outcome");
        AppendWorldNewsFlexibleField(lines, "➡ Продолжение", item, "followUp", "followUpEvent", "nextStep");
        AppendWorldNewsFlexibleField(lines, "💥 Влияние", item, "impact");
        if (item.TryGetProperty("impactProfile", out var impactProfile))
            AppendWorldNewsFlexibleValue(lines, "💥 Профиль влияния", impactProfile, "  ");

        lines.Add("");
    }

    private static void RenderNpcActivityNewsDetailed(List<string> lines, JsonElement item)
    {
        var npcName = GetStr(item, "NPCName", GetStr(item, "npcName", GetStr(item, "name", "")));
        if (string.IsNullOrEmpty(npcName)) return;

        JsonElement details = item;
        if (item.TryGetProperty("activityUpdate", out var upd) && upd.ValueKind == JsonValueKind.Object)
            details = upd;

        var actName = GetStr(details, "activityName", GetStr(details, "name", GetStr(item, "activityName", GetStr(item, "activity", ""))));
        if (string.IsNullOrEmpty(actName)) return;

        var activeState = GetStr(details, "activeState", GetStr(details, "status", GetStr(item, "activeState", GetStr(item, "status", ""))));
        var stColor = activeState.ToLowerInvariant() switch
        {
            "completed" => "green",
            "abandoned" => "red",
            _ => "yellow"
        };

        var line = $"  👤 [white]{Markup.Escape(npcName)}[/] → ⚡ [cyan]{Markup.Escape(actName)}[/]";
        if (!string.IsNullOrEmpty(activeState))
            line += $" [{stColor}]({Markup.Escape(activeState)})[/]";
        lines.Add(line);

        var desc = GetStr(details, "description", GetStr(item, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var location = GetStr(details, "location", GetStr(item, "location", GetStr(details, "locationId", "")));
        if (!string.IsNullOrEmpty(location))
            lines.Add($"    📍 [dim]{Markup.Escape(location)}[/]");

        var step = GetInt(details, "currentStep", 0);
        var totalSteps = GetInt(details, "totalSteps", 0);
        if (totalSteps > 0)
            lines.Add($"    📊 Этапы: [cyan]{step}/{totalSteps}[/]");

        var spent = GetInt(details, "timeSpentMinutes", 0);
        var totalTime = GetInt(details, "totalTimeCostMinutes", 0);
        if (totalTime > 0)
            lines.Add($"    🕐 Время: [cyan]{FormatMinutes(spent)}[/] / [cyan]{FormatMinutes(totalTime)}[/]");

        var narrative = GetStr(item, "narrativeSummary", GetStr(details, "narrativeSummary", ""));
        if (!string.IsNullOrEmpty(narrative))
            lines.Add($"    📝 [dim]{Markup.Escape(narrative)}[/]");
    }

    private static void RenderFactionProjectNewsDetailed(List<string> lines, JsonElement item)
    {
        var factionName = GetStr(item, "factionName", GetStr(item, "name", ""));
        var projectName = GetStr(item, "projectName", GetStr(item, "name", ""));
        if (string.IsNullOrEmpty(projectName)) return;

        var state = GetStr(item, "activeState", GetStr(item, "finalState", GetStr(item, "status", "")));
        var stColor = state.ToLowerInvariant() switch
        {
            "completed" => "green",
            "abandoned" => "red",
            _ => "yellow"
        };

        var line = $"  🏛️ [white]{Markup.Escape(factionName)}[/] → 🔨 [orange1]{Markup.Escape(projectName)}[/]";
        if (!string.IsNullOrEmpty(state))
            line += $" [{stColor}]({Markup.Escape(state)})[/]";
        lines.Add(line);

        var desc = GetStr(item, "description", "");
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var narrative = GetStr(item, "narrativeSummary", GetStr(item, "outcome", ""));
        if (!string.IsNullOrEmpty(narrative))
            lines.Add($"    📝 [dim]{Markup.Escape(narrative)}[/]");

        var step = GetInt(item, "currentStep", 0);
        var totalSteps = GetInt(item, "totalSteps", 0);
        if (totalSteps > 0)
            lines.Add($"    📊 Этапы: [cyan]{step}/{totalSteps}[/]");

        var timeSpent = GetInt(item, "timeSpentMinutes", 0);
        var timeTotal = GetInt(item, "totalTimeCostMinutes", 0);
        if (timeTotal > 0)
            lines.Add($"    🕐 Время: [cyan]{FormatMinutes(timeSpent)}[/] / [cyan]{FormatMinutes(timeTotal)}[/]");

        var etaTurn = GetStr(item, "estimatedCompletionTurn", "");
        if (!string.IsNullOrEmpty(etaTurn))
            lines.Add($"    ⏳ Примерное завершение: [dim]ход {Markup.Escape(etaTurn)}[/]");

        var canAssist = item.TryGetProperty("playerCanAssist", out var assist) && assist.ValueKind == JsonValueKind.True;
        var assistDesc = GetStr(item, "assistDescription", "");
        if (canAssist || !string.IsNullOrEmpty(assistDesc))
        {
            var assistLine = canAssist ? "    🤝 [green]Игрок может помочь[/]" : "    🤝 [dim]Помощь игрока[/]";
            if (!string.IsNullOrEmpty(assistDesc))
                assistLine += $" — {Markup.Escape(assistDesc)}";
            lines.Add(assistLine);
        }

        if (item.TryGetProperty("totalResourceCost", out var rc))
            AppendWorldNewsFlexibleValue(lines, "💰 Стоимость", rc, "    ");
        if (item.TryGetProperty("resourcesSpent", out var rs))
            AppendWorldNewsFlexibleValue(lines, "📉 Потрачено", rs, "    ");
    }

    private static void RenderWorldProgressNewsDetailed(List<string> lines, JsonElement item, string scopeLabel)
    {
        var name = GetStr(item, "trackerName", GetStr(item, "name", "?"));
        var cur = GetStr(item, "currentValue", GetStr(item, "progress", "0"));
        var max = GetStr(item, "maxValue", GetStr(item, "target", ""));
        var line = $"  {scopeLabel} → 📈 [white]{Markup.Escape(name)}[/]: [cyan]{Markup.Escape(cur)}[/]" +
            (!string.IsNullOrEmpty(max) ? $"/{Markup.Escape(max)}" : "");
        lines.Add(line);

        var desc = GetStr(item, "description", GetStr(item, "summary", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var stage = GetStr(item, "stageName", GetStr(item, "currentStage", GetStr(item, "stage", "")));
        if (!string.IsNullOrEmpty(stage))
            lines.Add($"    🏷️ Стадия: [yellow]{Markup.Escape(stage)}[/]");

        var reason = GetStr(item, "changeReason", GetStr(item, "lastChangeReason", GetStr(item, "reason", "")));
        if (!string.IsNullOrEmpty(reason))
            lines.Add($"    📝 [dim]{Markup.Escape(reason)}[/]");

        var milestone = GetStr(item, "nextMilestone", GetStr(item, "milestone", ""));
        if (!string.IsNullOrEmpty(milestone))
            lines.Add($"    🎯 Следующая веха: [dim]{Markup.Escape(milestone)}[/]");
    }

    private static void AppendWorldNewsFlexibleField(List<string> lines, string label, JsonElement parent, params string[] propNames)
    {
        foreach (var propName in propNames)
        {
            if (parent.TryGetProperty(propName, out var value))
            {
                AppendWorldNewsFlexibleValue(lines, label, value, "  ");
                return;
            }
        }
    }

    private static void AppendWorldNewsFlexibleValue(List<string> lines, string label, JsonElement value, string indent)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add($"{indent}{label}: [white]{Markup.Escape(text)}[/]");
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                lines.Add($"{indent}{label}: [white]{Markup.Escape(value.ToString())}[/]");
                break;

            case JsonValueKind.Array:
                if (value.GetArrayLength() == 0) return;
                lines.Add($"{indent}{label}:");
                foreach (var item in value.EnumerateArray())
                {
                    var itemText = ExtractWorldNewsDisplayText(item);
                    if (!string.IsNullOrWhiteSpace(itemText))
                        lines.Add($"{indent}  • [white]{Markup.Escape(itemText)}[/]");
                }
                break;

            case JsonValueKind.Object:
                var objectText = ExtractWorldNewsDisplayText(value);
                if (!string.IsNullOrWhiteSpace(objectText))
                    lines.Add($"{indent}{label}: [white]{Markup.Escape(objectText)}[/]");
                else
                {
                    var inner = new List<string>();
                    RenderExtraFields(inner, value, Array.Empty<string>(), $"{indent}  ");
                    if (inner.Count > 0)
                    {
                        lines.Add($"{indent}{label}:");
                        lines.AddRange(inner);
                    }
                }
                break;
        }
    }

    private static string ExtractWorldNewsDisplayText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            JsonValueKind.Object => GetStr(value, "name",
                GetStr(value, "title",
                    GetStr(value, "eventTitle",
                        GetStr(value, "summary",
                            GetStr(value, "description",
                                GetStr(value, "content",
                                    GetStr(value, "factionName",
                                        GetStr(value, "locationName",
                                            GetStr(value, "npcName",
                                                GetStr(value, "value", value.ToString())))))))))),
            _ => ""
        };
    }

    /// <summary>Formats minutes into a human-readable string (e.g. "2ч 30м").</summary>
    private static string FormatMinutes(int totalMinutes)
    {
        if (totalMinutes < 60) return $"{totalMinutes}м";
        var hours = totalMinutes / 60;
        var mins = totalMinutes % 60;
        if (hours < 24) return mins > 0 ? $"{hours}ч {mins}м" : $"{hours}ч";
        var days = hours / 24;
        hours %= 24;
        return hours > 0 ? $"{days}д {hours}ч" : $"{days}д";
    }

    private async Task RenderAsciiMap(int playerX, int playerY, int playerZ, string playerLocName, JsonElement curLoc, JsonDocument? mapDoc)
    {
        // Collect all known points on this z-level
        var points = new Dictionary<(int x, int y), (string name, bool isCurrent)>();
        points[(playerX, playerY)] = (playerLocName, true);

        // From adjacencyMap of current location
        if (curLoc.TryGetProperty("adjacencyMap", out var adj) && adj.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in adj.EnumerateArray())
            {
                var tx = playerX; var ty = playerY; var tz = playerZ;
                if (entry.TryGetProperty("targetCoordinates", out var tc))
                {
                    tx = GetInt(tc, "x", playerX); ty = GetInt(tc, "y", playerY); tz = GetInt(tc, "z", playerZ);
                }
                if (tz == playerZ && !points.ContainsKey((tx, ty)))
                    points[(tx, ty)] = (GetStr(entry, "name", "?"), false);
            }
        }

        // From world_map newLocations on same z-level
        if (mapDoc != null && mapDoc.RootElement.TryGetProperty("newLocations", out var newLocs) && newLocs.ValueKind == JsonValueKind.Array)
        {
            foreach (var loc in newLocs.EnumerateArray())
            {
                if (loc.TryGetProperty("coordinates", out var lc))
                {
                    var lx = GetInt(lc, "x", 0); var ly = GetInt(lc, "y", 0); var lz = GetInt(lc, "z", 0);
                    if (lz == playerZ && !points.ContainsKey((lx, ly)))
                        points[(lx, ly)] = (GetStr(loc, "name", "?"), false);
                }
            }
        }

        if (points.Count < 2) return; // No map to show with only 1 point

        // Determine bounds
        var minX = points.Keys.Min(p => p.x);
        var maxX = points.Keys.Max(p => p.x);
        var minY = points.Keys.Min(p => p.y);
        var maxY = points.Keys.Max(p => p.y);

        // Clamp to reasonable size (±5 from player)
        minX = Math.Max(minX, playerX - 5); maxX = Math.Min(maxX, playerX + 5);
        minY = Math.Max(minY, playerY - 5); maxY = Math.Min(maxY, playerY + 5);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;

        // Build grid — note: Y increases going North, so render top-to-bottom = maxY first
        var lines = new List<string>();
        var legend = new List<string>();
        int legendIdx = 1;
        var legendMap = new Dictionary<(int, int), int>();

        for (int y = maxY; y >= minY; y--)
        {
            var row = "";
            for (int x = minX; x <= maxX; x++)
            {
                if (points.TryGetValue((x, y), out var pt))
                {
                    if (pt.isCurrent)
                        row += "[bold green][@][/]";
                    else
                    {
                        legendMap[(x, y)] = legendIdx;
                        row += $"[cyan][{legendIdx}][/]";
                        legend.Add($"  [cyan][{legendIdx}][/] {Markup.Escape(pt.name)}");
                        legendIdx++;
                    }
                }
                else
                {
                    row += "[dim] · [/]";
                }
            }
            lines.Add(row);
        }

        // Compass labels
        var mapText = new List<string>();
        var centerPad = new string(' ', Math.Max(0, (width * 3) / 2 - 1));
        mapText.Add($"{centerPad}[dim]N[/]");
        mapText.Add($"{centerPad}[dim]↑[/]");
        foreach (var line in lines) mapText.Add($"  {line}");
        mapText.Add($"{centerPad}[dim]↓[/]");
        mapText.Add($"{centerPad}[dim]S[/]");
        if (legend.Count > 0)
        {
            mapText.Add("");
            mapText.Add($"[bold green][[@]][/] = Вы здесь");
            mapText.AddRange(legend);
        }

        var zLabel = playerZ > 0 ? $"↑{playerZ}" : playerZ < 0 ? $"↓{Math.Abs(playerZ)}" : "наземный";

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", mapText)))
        {
            Header = new PanelHeader($" 🗺 Карта (уровень: {zLabel}) ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        });
    }

    private async Task ShowDetailedStatus()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;

        // ── Load supplementary data ──
        var expDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/experience.json");
        var itemsDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/items.json");
        var weightDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/weight_calc.json");
        var transDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/transformation.json");
        var stealthDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/stealth.json");
        var scDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/status_changes.json");
        var effDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/effects.json");
        var wndDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/wounds.json");
        var customStatesDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/custom_states.json");

        // ── Extract level and XP ──
        int level = 1; int totalXp = 0; int xpForNext = 0;
        if (expDoc != null)
        {
            var er = expDoc.RootElement;
            level = GetInt(er, "level", GetInt(er, "playerLevel", 1));
            totalXp = GetInt(er, "totalExperience", 0);
            xpForNext = GetInt(er, "experienceForNextLevel", 0);
        }

        // ── Extract total money from player_status.json first, then items.json fallback ──
        int totalMoney = 0;
        var statusDoc = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
        if (statusDoc != null)
            totalMoney = GetInt(statusDoc.RootElement, "money", 0);
        if (totalMoney == 0 && itemsDoc != null)
        {
            totalMoney = GetInt(itemsDoc.RootElement, "money", 0);
            if (totalMoney == 0 && itemsDoc.RootElement.TryGetProperty("resources", out var res) && res.ValueKind == JsonValueKind.Object)
                totalMoney = GetInt(res, "gold", GetInt(res, "money", GetInt(res, "coins", 0)));
        }

        // ── Extract weight ──
        int totalWeight = 0; int maxWeight = 0; bool isOverloaded = false;
        int additionalEnergyExpenditure = 0;
        if (weightDoc != null)
        {
            var wr = weightDoc.RootElement;
            totalWeight = GetInt(wr, "totalWeight", GetInt(wr, "currentWeight", 0));
            maxWeight = GetInt(wr, "maxWeight", GetInt(wr, "maximumWeight", 0));
            isOverloaded = wr.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
            if (!isOverloaded && wr.TryGetProperty("overloaded", out var oldOv) && oldOv.ValueKind == JsonValueKind.True)
                isOverloaded = true;
            additionalEnergyExpenditure = GetInt(wr, "additionalEnergyExpenditure", 0);
        }
        else if (itemsDoc != null)
        {
            var ir = itemsDoc.RootElement;
            totalWeight = GetInt(ir, "totalWeight", 0);
            maxWeight = GetInt(ir, "maxWeight", 0);
            isOverloaded = ir.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
        }

        // ── Extract auto-combat skill ──
        string autoCombatSkill = "";
        if (transDoc != null)
            autoCombatSkill = GetStr(transDoc.RootElement, "playerAutoCombatSkillChange", GetStr(transDoc.RootElement, "autoCombatSkill", ""));

        var grid = new Grid()
            .AddColumn(new GridColumn())
            .AddColumn(new GridColumn());

        var leftContent = new Grid().AddColumn(new GridColumn());
        leftContent.AddRow(new Markup($"[bold white]👤 {Markup.Escape(state.CharacterName)}[/]"));

        var identityTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").NoWrap().Width(18))
            .AddColumn(new TableColumn(""));
        identityTable.AddRow(new Markup("[dim]Раса[/]"), new Markup($"[white]{Markup.Escape(state.CharacterRace)}[/]"));
        identityTable.AddRow(new Markup("[dim]Класс[/]"), new Markup($"[white]{Markup.Escape(state.CharacterClass)}[/]"));
        identityTable.AddRow(new Markup("[dim]Уровень[/]"), new Markup($"[cyan]{level}[/]"));
        leftContent.AddRow(identityTable);

        var summaryTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").NoWrap().Width(18))
            .AddColumn(new TableColumn("").NoWrap().Width(20))
            .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(18));

        if (xpForNext > 0)
        {
            var xpPct = (int)Math.Clamp((long)totalXp * 100 / Math.Max(1, xpForNext), 0, 100);
            summaryTable.AddRow(
                new Markup("[yellow]Опыт[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(xpPct, 18, "yellow")),
                new Markup($"[yellow]{totalXp}/{xpForNext} ({xpPct}%)[/]"));
        }
        else if (totalXp > 0)
        {
            summaryTable.AddRow(
                new Markup("[yellow]Опыт[/]"),
                new Markup(""),
                new Markup($"[yellow]{totalXp}[/]"));
        }

        var hpPctValue = int.TryParse(state.PlayerStatus.HealthPercentage.Replace("%", "").Trim(), out var hpV) ? hpV : 100;
        var enPctValue = int.TryParse(state.PlayerStatus.EnergyPercentage.Replace("%", "").Trim(), out var enV) ? enV : 100;
        var poPctValue = int.TryParse(state.PlayerStatus.PoisePercentage.Replace("%", "").Trim(), out var poV) ? poV : 100;
        summaryTable.AddRow(
            new Markup("[red]Здоровье[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(hpPctValue, 18, hpPctValue > 60 ? "green" : hpPctValue > 30 ? "yellow" : "red")),
            new Markup($"[red]{Markup.Escape(state.PlayerStatus.HealthPercentage)}[/]"));
        summaryTable.AddRow(
            new Markup("[cyan]Энергия[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(enPctValue, 18, enPctValue > 60 ? "deepskyblue1" : enPctValue > 30 ? "yellow" : "red")),
            new Markup($"[cyan]{Markup.Escape(state.PlayerStatus.EnergyPercentage)}[/]"));
        summaryTable.AddRow(
            new Markup("[blue]Равновесие[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(poPctValue, 18, poPctValue > 60 ? "steelblue" : poPctValue > 30 ? "yellow" : "red")),
            new Markup($"[blue]{Markup.Escape(state.PlayerStatus.PoisePercentage)}[/]"));
        summaryTable.AddRow(
            new Markup("[yellow]Состояние[/]"),
            new Markup(""),
            new Markup($"[yellow]{Markup.Escape(state.PlayerStatus.CurrentCondition)}[/]"));

        if (totalMoney > 0)
            summaryTable.AddRow(new Markup("[gold1]Деньги[/]"), new Markup(""), new Markup($"[gold1]{totalMoney}[/]"));

        if (maxWeight > 0)
        {
            var weightPct = Math.Clamp(totalWeight * 100 / Math.Max(1, maxWeight), 0, 100);
            var wColor = isOverloaded ? "red" : weightPct > 80 ? "yellow" : "green";
            summaryTable.AddRow(
                new Markup($"[{wColor}]Вес[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(weightPct, 18, wColor)),
                new Markup($"[{wColor}]{totalWeight}/{maxWeight} кг{(isOverloaded ? " (ПЕРЕГРУЗКА)" : "")}[/]"));
            if (additionalEnergyExpenditure > 0)
                summaryTable.AddRow(new Markup("[yellow]Доп. расход[/]"), new Markup(""), new Markup($"[yellow]+{additionalEnergyExpenditure}/ход[/]"));
        }

        leftContent.AddRow(summaryTable);

        if (stealthDoc != null)
        {
            var sr = stealthDoc.RootElement;
            var isActive = (sr.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True)
                        || (sr.TryGetProperty("isHidden", out var ih) && ih.ValueKind == JsonValueKind.True);
            var detLevel = GetInt(sr, "detectionLevel", -1);
            var stDesc = GetStr(sr, "description", GetStr(sr, "state", ""));
            if (isActive || detLevel >= 0 || !string.IsNullOrEmpty(stDesc))
            {
                var stealthTable = new Table()
                    .Border(TableBorder.None)
                    .HideHeaders()
                    .Expand()
                    .AddColumn(new TableColumn("").NoWrap().Width(18))
                    .AddColumn(new TableColumn("").NoWrap().Width(20))
                    .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(18));

                if (detLevel >= 0)
                {
                    var (label, color) = detLevel switch
                    {
                        <= 25 => ("Невидим", "green"),
                        <= 50 => ("Незамечен", "green"),
                        <= 75 => ("Подозрение", "yellow"),
                        <= 99 => ("Тревога", "orange1"),
                        _ => ("Обнаружен", "red")
                    };
                    stealthTable.AddRow(
                        new Markup("[green]Скрытность[/]"),
                        new Markup(ConsoleLayout.CreateBarFromPercent(detLevel, 18, color)),
                        new Markup($"[{color}]{label} ({detLevel}%)[/]"));
                }
                else
                {
                    stealthTable.AddRow(new Markup("[green]Скрытность[/]"), new Markup(""), new Markup(isActive ? "[green]Скрыт[/]" : $"[dim]{Markup.Escape(stDesc)}[/]"));
                }
                leftContent.AddRow(stealthTable);
            }
        }

        if (!string.IsNullOrEmpty(autoCombatSkill))
            leftContent.AddRow(new Markup($"[cyan]⚔ Авто-бой:[/] {Markup.Escape(autoCombatSkill)}"));

        if (state.PlayerStatus.ActiveConditions.Length > 0)
        {
            leftContent.AddRow(new Markup("[yellow]Активные состояния:[/]"));
            foreach (var c in state.PlayerStatus.ActiveConditions)
                leftContent.AddRow(new Markup($"[yellow]•[/] {Markup.Escape(c)}"));
        }

        // ── Right: characteristics (use computed if available) ──
        var rightContent = new Grid().AddColumn(new GridColumn());
        rightContent.AddRow(new Markup("[bold]Характеристики:[/]"));
        var compCharDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/computed_characteristics.json");
        var charDoc = await _stateManager.LoadGameStateFileAsync("game_state/misc/characteristics.json");
        // Try to extract base and modified values
        JsonElement? baseChars = null, permChars = null, modChars = null;
        var unspentStatPoints = 0;
        if (compCharDoc != null)
        {
            var cr = compCharDoc.RootElement;
            if (cr.TryGetProperty("characteristics", out var bc) && bc.ValueKind == JsonValueKind.Object) baseChars = bc;
            if (cr.TryGetProperty("permanentlyModifiedCharacteristics", out var pm) && pm.ValueKind == JsonValueKind.Object) permChars = pm;
            if (cr.TryGetProperty("modifiedCharacteristics", out var mc) && mc.ValueKind == JsonValueKind.Object) modChars = mc;
            unspentStatPoints = GetInt(cr, "unspentStatPoints", 0);
        }
        var charSource = modChars ?? permChars ?? baseChars ?? (charDoc != null ? charDoc.RootElement : (JsonElement?)null);
        int statPermCon = 0, statPermStr = 0, statPermInt = 0, statPermWis = 0, statPermFai = 0, statPermLuck = 0;
        if (charSource.HasValue)
        {
            var charTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").NoWrap().Width(12))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(10))
                .AddColumn(new TableColumn(""));

            foreach (var charName in Characteristics.All)
            {
                var ruName = Characteristics.RussianNames[charName];
                var modVal = charSource.Value.TryGetProperty(charName, out var mv) ? mv.GetInt32() : 0;
                var baseVal = baseChars.HasValue && baseChars.Value.TryGetProperty(charName, out var bv) ? bv.GetInt32() : modVal;
                var diff = modVal - baseVal;
                var diffStr = diff > 0 ? $" [green](+{diff})[/]" : diff < 0 ? $" [red]({diff})[/]" : "";
                var barClr = modVal >= 14 ? "green" : modVal >= 8 ? "yellow" : "red";
                var filled = Math.Clamp(modVal * 10 / 20, 0, 10);
                charTable.AddRow(
                    new Markup(Markup.Escape(ruName)),
                    new Markup(ConsoleLayout.CreateBar(filled, 10, barClr)),
                    new Markup($"[white]{modVal}[/]{diffStr}"),
                    new Markup("[dim][/]")
                );
                // Cache for derived stats
                if (charName == Characteristics.Constitution) statPermCon = modVal;
                else if (charName == Characteristics.Strength) statPermStr = modVal;
                else if (charName == Characteristics.Intelligence) statPermInt = modVal;
                else if (charName == Characteristics.Wisdom) statPermWis = modVal;
                else if (charName == Characteristics.Faith) statPermFai = modVal;
                else if (charName == Characteristics.Luck) statPermLuck = modVal;
            }
            rightContent.AddRow(charTable);

            // ── Derived stats summary (compact) ──
            rightContent.AddRow(new Markup("[bold]Производные параметры:[/]"));
            var dMaxHp = 100 + statPermCon * 2 + statPermStr;
            var dMaxEn = 100 + (int)(statPermCon * 0.75) + (int)(statPermInt * 0.75) + (int)(statPermWis * 0.75) + (int)(statPermFai * 0.75);
            var dMaxPoise = 100 + (int)(statPermStr * 1.5) + (int)(statPermCon * 1.5) + (int)(statPermInt * 1.5) + (int)(statPermWis * 1.5);
            var dMaxWeight = 30 + (int)(statPermStr * 1.8 + statPermCon * 0.4);
            var dCritThr = 20 - statPermLuck / 20;
            var derivedTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(16))
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(16));
            derivedTable.AddRow(new Markup("[dim]Макс. здоровье[/]"), new Markup($"[red]{dMaxHp}%[/]"), new Markup("[dim]Энергия[/]"), new Markup($"[cyan]{dMaxEn}%[/]"));
            derivedTable.AddRow(new Markup("[dim]Равновесие[/]"), new Markup($"[blue]{dMaxPoise}%[/]"), new Markup("[dim]Вес[/]"), new Markup($"[white]{dMaxWeight} кг[/]"));
            derivedTable.AddRow(new Markup("[dim]Критический диапазон[/]"), new Markup($"[gold1]{dCritThr}-20[/]"), new Markup(""), new Markup(""));
            rightContent.AddRow(derivedTable);
            if (unspentStatPoints > 0)
                rightContent.AddRow(new Markup($"[green]Свободные очки: {unspentStatPoints}[/] [dim](/распределить)[/]"));
            rightContent.AddRow(new Markup("[dim](подробнее: /статы)[/]"));
        }
        else
        {
            rightContent.AddRow(new Markup("[dim]Нет данных[/]"));
        }

        // Effort tracker
        if (expDoc != null)
        {
            var er = expDoc.RootElement;
            if (er.TryGetProperty("playerEffortTrackerChange", out var eft) && eft.ValueKind == JsonValueKind.Object)
            {
                var lastChar = GetStr(eft, "lastUsedCharacteristic", "");
                var consec = GetInt(eft, "consecutivePartialSuccesses", 0);
                if (consec > 0 || !string.IsNullOrEmpty(lastChar))
                {
                    rightContent.AddRow(new Markup("[bold]📊 Трекер усилий:[/]"));
                    if (!string.IsNullOrEmpty(lastChar))
                    {
                        var ruChar = Characteristics.RussianNames.GetValueOrDefault(lastChar.ToLowerInvariant(), lastChar);
                        rightContent.AddRow(new Markup($"[cyan]Последняя характеристика:[/] {Markup.Escape(ruChar)}"));
                    }
                    rightContent.AddRow(new Markup($"[yellow]Частичных успехов:[/] {consec}/3"));
                }
            }
        }

        grid.AddRow(
            leftContent,
            rightContent
        );

        var panel = new Panel(grid)
        {
            Header = new PanelHeader($" {_loc.T("status")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(panel);

        // ── Additional panel: recent changes ──
        var extraText = new List<string>();

        // Transformation (appearance changes)
        if (transDoc != null)
        {
            var t = transDoc.RootElement;
            var appearance = GetStr(t, "playerAppearanceChange", "");
            var raceDesc = GetStr(t, "playerRaceDescriptionChange", "");
            var classDesc = GetStr(t, "playerClassDescriptionChange", "");
            if (!string.IsNullOrEmpty(appearance))
                extraText.Add($"🎭 Внешность: [white]{Markup.Escape(appearance)}[/]");
            if (!string.IsNullOrEmpty(raceDesc))
                extraText.Add($"🧬 Раса: [white]{Markup.Escape(raceDesc)}[/]");
            if (!string.IsNullOrEmpty(classDesc))
                extraText.Add($"⚔️ Класс: [white]{Markup.Escape(classDesc)}[/]");
        }

        // Status changes (deltas)
        if (scDoc != null)
        {
            var sc = scDoc.RootElement;
            var moneyDelta = GetInt(sc, "moneyChange", 0);
            var hpDelta = GetInt(sc, "currentHealthChange", 0);
            var energyDelta = GetInt(sc, "currentEnergyChange", 0);
            var poiseDelta = GetInt(sc, "currentPoiseChange", 0);
            if (moneyDelta != 0)
                extraText.Add($"💰 Деньги (последнее): [{(moneyDelta > 0 ? "green" : "red")}]{(moneyDelta > 0 ? "+" : "")}{moneyDelta}[/]");
            if (hpDelta != 0)
                extraText.Add($"❤️ Здоровье (последнее): [{(hpDelta > 0 ? "green" : "red")}]{(hpDelta > 0 ? "+" : "")}{hpDelta}[/]");
            if (energyDelta != 0)
                extraText.Add($"⚡ Энергия (последнее): [{(energyDelta > 0 ? "green" : "red")}]{(energyDelta > 0 ? "+" : "")}{energyDelta}[/]");
            if (poiseDelta != 0)
                extraText.Add($"🛡️ Равновесие (последнее): [{(poiseDelta > 0 ? "green" : "red")}]{(poiseDelta > 0 ? "+" : "")}{poiseDelta}[/]");
            var statsUp = FormatCharacteristicArray(sc, "statsIncreased");
            var statsDown = FormatCharacteristicArray(sc, "statsDecreased");
            if (!string.IsNullOrEmpty(statsUp))
                extraText.Add($"[green]📈 Повышены: {statsUp}[/]");
            if (!string.IsNullOrEmpty(statsDown))
                extraText.Add($"[red]📉 Понижены: {statsDown}[/]");
        }

        if (expDoc != null)
        {
            var xpDelta = GetInt(expDoc.RootElement, "experienceGained", 0);
            if (xpDelta != 0)
                extraText.Add($"✨ Опыт (последнее): [yellow]+{xpDelta}[/]");
        }

        // Combat alert — check if enemies exist
        var combatEnemDoc = await _stateManager.LoadGameStateFileAsync("game_state/combat/enemies.json");
        if (combatEnemDoc != null)
        {
            var enemyCount = 0;
            EnumerateJsonItems(combatEnemDoc.RootElement, _ => enemyCount++);
            if (enemyCount > 0)
                extraText.Add($"[bold red]⚔️ ВЫ В БОЮ! Врагов: {enemyCount}[/] [dim](подробнее: /бой)[/]");
        }

        if (effDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusEffectPreview(extraText, effDoc.RootElement);
        }

        if (wndDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusWoundPreview(extraText, wndDoc.RootElement);
        }

        if (customStatesDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusCustomStatePreview(extraText, customStatesDoc.RootElement);
        }

        if (extraText.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", extraText)))
            {
                Header = new PanelHeader(" 📊 Дополнительно ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            });
        }

        // Player portrait
        var playerImgPrompt = "";
        if (transDoc != null)
            playerImgPrompt = GetStr(transDoc.RootElement, "playerImagePromptChange",
                GetStr(transDoc.RootElement, "image_prompt", ""));
        await WaitForKeyWithImage("player", _stateManager.CurrentState.CharacterName ?? "player", playerImgPrompt, "player_portrait");
    }

    private async Task ShowSkills()
    {
        var activeDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skills_active.json");
        var passiveDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skills_passive.json");
        var masteryDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skill_mastery.json");

        // Build mastery lookup: skillName -> element
        var masteryLookup = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (masteryDoc != null)
        {
            if (masteryDoc.RootElement.TryGetProperty("skillMasteryChanges", out var masteryArr) &&
                masteryArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in masteryArr.EnumerateArray())
                {
                    var mName = GetStr(item, "skillName", GetStr(item, "name", ""));
                    if (!string.IsNullOrEmpty(mName))
                        masteryLookup[mName] = item;
                }
            }
            else
            {
                EnumerateJsonItems(masteryDoc.RootElement, item =>
                {
                    var mName = GetStr(item, "skillName", GetStr(item, "name", ""));
                    if (!string.IsNullOrEmpty(mName))
                        masteryLookup[mName] = item;
                });
            }
        }

        // Collect all skills: (displayLabel, element, isActive)
        var skills = new List<(string label, JsonElement el, bool isActive)>();

        if (activeDoc != null)
        {
            void AddActiveSkill(JsonElement item)
            {
                var name = GetStr(item, "skillName", GetStr(item, "name", "???"));
                var rarity = GetStr(item, "rarity", "");
                var sLvl = GetStr(item, "level", "");
                var cat = GetStr(item, "category", "");
                var catTag = cat switch
                {
                    "Magical" => "Магический",
                    "Combat" => "Боевой",
                    "Utility" => "Утилитарный",
                    _ => cat
                };
                skills.Add((ConsoleLayout.PlainChoiceLabel(
                    $"⚡ {name}",
                    string.IsNullOrEmpty(rarity) ? "" : rarity,
                    string.IsNullOrEmpty(sLvl) ? "" : $"Уровень {sLvl}",
                    catTag), item, true));
            }

            if (activeDoc.RootElement.TryGetProperty("activeSkillChanges", out var activeArr) &&
                activeArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in activeArr.EnumerateArray())
                    AddActiveSkill(item);
            }
            else
            {
                EnumerateJsonItems(activeDoc.RootElement, AddActiveSkill);
            }
        }

        if (passiveDoc != null)
        {
            void AddPassiveSkill(JsonElement item)
            {
                var name = GetStr(item, "skillName", GetStr(item, "name", "???"));
                var rarity = GetStr(item, "rarity", "");
                var pType = GetStr(item, "type", "");
                var typeTag = pType switch
                {
                    "KnowledgeBased" => "Знание",
                    "CharacteristicBonus" => "Бонус к характеристике",
                    "BodyModification" => "Модификация тела",
                    "CombatEnhancement" => "Боевое улучшение",
                    "Utility" => "Утилитарный",
                    _ => pType
                };
                skills.Add((ConsoleLayout.PlainChoiceLabel(
                    $"🔮 {name}",
                    string.IsNullOrEmpty(rarity) ? "" : rarity,
                    typeTag), item, false));
            }

            if (passiveDoc.RootElement.TryGetProperty("passiveSkillChanges", out var passiveArr) &&
                passiveArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in passiveArr.EnumerateArray())
                    AddPassiveSkill(item);
            }
            else
            {
                EnumerateJsonItems(passiveDoc.RootElement, AddPassiveSkill);
            }
        }

        if (skills.Count == 0)
        {
            ShowEmptyPanel(_loc.T("skills"), "Навыки не обнаружены");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choices = new List<string>();
            foreach (var (label, _, _) in skills)
                choices.Add(label);
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold yellow]🎓 {_loc.T("skills")}[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= skills.Count) break;

            var (_, el, isActive) = skills[selIdx];
            if (isActive)
                await ShowActiveSkillDetailPanel(el, masteryLookup);
            else
                ShowPassiveSkillDetailPanel(el, masteryLookup);
        }
    }

    private async Task ShowActiveSkillDetailPanel(JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        var lines = new List<string>();
        var name = GetStr(s, "skillName", GetStr(s, "name", "???"));
        lines.Add($"[bold yellow]⚡ {Markup.Escape(name)}[/]");

        var rarity = GetStr(s, "rarity", "");
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");

        var category = GetStr(s, "category", "");
        if (!string.IsNullOrEmpty(category))
        {
            var catLabel = category switch
            {
                "Magical" => "Магический",
                "Combat" => "Боевой",
                "Utility" => "Утилитарный",
                _ => category
            };
            lines.Add($"  📂 Категория: [cyan]{Markup.Escape(catLabel)}[/]");
        }

        var skillLevel = GetStr(s, "level", "");
        if (!string.IsNullOrEmpty(skillLevel))
            lines.Add($"  📊 Уровень навыка: [yellow]{Markup.Escape(skillLevel)}[/]");

        var desc = GetStr(s, "skillDescription", GetStr(s, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"  {Markup.Escape(desc)}");

        lines.Add("");

        var actionCost = GetStr(s, "actionCost", "");
        if (!string.IsNullOrEmpty(actionCost))
        {
            var costColor = actionCost.ToLower() switch
            {
                "main" or "основное" => "red",
                "fast" or "быстрое" => "yellow",
                "free" or "свободное" => "green",
                _ => "white"
            };
            lines.Add($"  ⏱ Действие: [{costColor}]{Markup.Escape(actionCost)}[/]");
        }

        var energyCost = GetStr(s, "energyCost", "");
        if (!string.IsNullOrEmpty(energyCost))
            lines.Add($"  🔋 Энергия: [cyan]{Markup.Escape(energyCost)}[/]");

        var cooldown = GetStr(s, "cooldownTurns", "");
        if (!string.IsNullOrEmpty(cooldown) && cooldown != "0")
            lines.Add($"  ⏳ Перезарядка: [yellow]{Markup.Escape(cooldown)} ход(ов)[/]");

        var timeCost = GetStr(s, "timeCost", "");
        if (!string.IsNullOrEmpty(timeCost) && timeCost != "0")
            lines.Add($"  🕐 Время: [yellow]{FormatMinutes(int.TryParse(timeCost, out var tc) ? tc : 0)}[/]");

        var scaling = GetStr(s, "scalingCharacteristic", "");
        if (!string.IsNullOrEmpty(scaling))
            lines.Add($"  📈 Масштабирование: [cyan]{Markup.Escape(scaling)}[/]");

        // Combat effect → effects[]
        if (s.TryGetProperty("combatEffect", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            var targetPriority = GetStr(ce, "targetPriority", "");
            if (ce.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array && effects.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]⚔ Боевые эффекты:[/]");
                if (!string.IsNullOrEmpty(targetPriority))
                    lines.Add($"    🎯 Приоритет цели: [white]{Markup.Escape(targetPriority)}[/]");
                foreach (var eff in effects.EnumerateArray())
                {
                    var effType = GetStr(eff, "effectType", "???");
                    var effVal = GetStr(eff, "value", "");
                    var effTarget = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                    var effDur = GetStr(eff, "duration", "");
                    var effDesc = GetStr(eff, "effectDescription", "");
                    var poiseDmg = GetStr(eff, "poiseDamage", "");
                    var tgtCount = GetStr(eff, "targetsCount", "");

                    var effLine = $"    • [cyan]{Markup.Escape(effType)}[/]";
                    if (!string.IsNullOrEmpty(effVal)) effLine += $" [yellow]{Markup.Escape(effVal)}[/]";
                    if (!string.IsNullOrEmpty(effTarget)) effLine += $" → {Markup.Escape(effTarget)}";
                    if (!string.IsNullOrEmpty(poiseDmg) && poiseDmg != "0") effLine += $" [dim](🛡️ -{Markup.Escape(poiseDmg)} стойк.)[/]";
                    if (!string.IsNullOrEmpty(tgtCount) && tgtCount != "1") effLine += $" [dim](×{Markup.Escape(tgtCount)} целей)[/]";
                    if (!string.IsNullOrEmpty(effDur)) effLine += $" [dim]({Markup.Escape(effDur)} ход.)[/]";
                    lines.Add(effLine);
                    if (!string.IsNullOrEmpty(effDesc))
                        lines.Add($"      [dim]{Markup.Escape(effDesc)}[/]");
                }
            }
        }

        // ── Scaling estimate: show calculated damage with player's current stats ──
        if (!string.IsNullOrEmpty(scaling) && _charService != null)
        {
            try
            {
                var computed = await _charService.ComputeAsync();
                var scalingLower = scaling.ToLowerInvariant();
                // Map Russian scaling names to English
                var scalingKey = scalingLower switch
                {
                    "сила" or "strength" => Characteristics.Strength,
                    "ловкость" or "dexterity" => Characteristics.Dexterity,
                    "выносливость" or "constitution" => Characteristics.Constitution,
                    "интеллект" or "intelligence" => Characteristics.Intelligence,
                    "мудрость" or "wisdom" => Characteristics.Wisdom,
                    "вера" or "faith" => Characteristics.Faith,
                    "привлекательность" or "attractiveness" => Characteristics.Attractiveness,
                    "торговля" or "trade" => Characteristics.Trade,
                    "убеждение" or "persuasion" => Characteristics.Persuasion,
                    "восприятие" or "perception" => Characteristics.Perception,
                    "удача" or "luck" => Characteristics.Luck,
                    "скорость" or "speed" => Characteristics.Speed,
                    _ => scalingLower
                };

                if (computed.Stats.TryGetValue(scalingKey, out var scaleStat))
                {
                    var charVal = scaleStat.PermanentlyModified + scaleStat.TemporaryBonus;
                    var lvl = computed.PlayerLevel;
                    var mastLvl = int.TryParse(GetStr(s, "currentMasteryLevel", GetStr(s, "masteryLevel", "1")), out var ml) ? ml : 1;

                    // CharBonusPercent = floor(charVal / 10) * 5
                    var charBonusPct = charVal / 10 * 5;
                    // LevelBonusPercent = floor(level / 5) * 8
                    var lvlBonusPct = lvl / 5 * 8;
                    // MasteryBonusPercent = masteryLevel * 4
                    var mastBonusPct = mastLvl * 4;
                    var totalMultiplier = 1.0 + charBonusPct / 100.0 + lvlBonusPct / 100.0 + mastBonusPct / 100.0;

                    lines.Add("");
                    lines.Add("  [bold green]📐 Расчёт масштабирования (Block 7):[/]");
                    var ruScaling = Characteristics.RussianNames.GetValueOrDefault(scalingKey, scaling);
                    lines.Add($"    {Markup.Escape(ruScaling)}: [white]{charVal}[/] → бонус [green]+{charBonusPct}%[/] [dim](значение/10 × 5)[/]");
                    lines.Add($"    Уровень: [white]{lvl}[/] → бонус [green]+{lvlBonusPct}%[/] [dim](уровень/5 × 8)[/]");
                    lines.Add($"    Мастерство: [white]{mastLvl}[/] → бонус [green]+{mastBonusPct}%[/] [dim](мастерство × 4)[/]");
                    lines.Add($"    [bold]Итого множитель: [yellow]×{totalMultiplier:F2}[/][/] [dim](базовый эффект × {totalMultiplier:F2})[/]");

                    // Try to show actual estimated damage for Damage effects
                    if (s.TryGetProperty("combatEffect", out var ce2) && ce2.ValueKind == JsonValueKind.Object
                        && ce2.TryGetProperty("effects", out var effs2) && effs2.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var eff in effs2.EnumerateArray())
                        {
                            var eType = GetStr(eff, "effectType", "").ToLowerInvariant();
                            if (!eType.Contains("damage") && !eType.Contains("heal") && !eType.Contains("урон")) continue;
                            var baseValStr = GetStr(eff, "value", "");
                            if (int.TryParse(baseValStr.Replace("%", "").Trim(), out var baseVal))
                            {
                                var scaledVal = (int)Math.Round(baseVal * totalMultiplier);
                                lines.Add($"    → [bold]{Markup.Escape(GetStr(eff, "effectType", "???"))}[/]: база {baseVal}% × {totalMultiplier:F2} = [bold yellow]{scaledVal}%[/]");
                            }
                        }
                    }
                }
            }
            catch { /* If char service fails, just skip scaling estimate */ }
        }

        // Scaling flags
        var scalesVal = s.TryGetProperty("scalesValue", out var svf) && svf.ValueKind == JsonValueKind.True;
        var scalesDur = s.TryGetProperty("scalesDuration", out var sdf) && sdf.ValueKind == JsonValueKind.True;
        var scalesChn = s.TryGetProperty("scalesChance", out var scf) && scf.ValueKind == JsonValueKind.True;
        if (scalesVal || scalesDur || scalesChn)
        {
            var flags = new List<string>();
            if (scalesVal) flags.Add("значение");
            if (scalesDur) flags.Add("длительность");
            if (scalesChn) flags.Add("шанс");
            lines.Add($"  [dim]Масштабируется: {string.Join(", ", flags)}[/]");
        }

        // Mastery
        AppendMasteryInfo(lines, name, s, masteryLookup);

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" ⚡ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 1),
            Expand = true
        });
        WaitForKey();
    }

    private void ShowPassiveSkillDetailPanel(JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        var lines = new List<string>();
        var name = GetStr(s, "skillName", GetStr(s, "name", "???"));
        lines.Add($"[bold blue]🔮 {Markup.Escape(name)}[/]");

        var rarity = GetStr(s, "rarity", "");
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");

        var type = GetStr(s, "type", "");
        if (!string.IsNullOrEmpty(type))
        {
            var typeLabel = type switch
            {
                "KnowledgeBased" => "Знание",
                "CharacteristicBonus" => "Бонус к характеристике",
                "BodyModification" => "Модификация тела",
                "CombatEnhancement" => "Боевое улучшение",
                "Utility" => "Утилита",
                _ => type
            };
            lines.Add($"  📂 Тип: [cyan]{Markup.Escape(typeLabel)}[/]");
        }

        var group = GetStr(s, "group", "");
        if (!string.IsNullOrEmpty(group))
            lines.Add($"  🏷 Группа: [cyan]{Markup.Escape(group)}[/]");

        var desc = GetStr(s, "skillDescription", GetStr(s, "description", ""));
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  {Markup.Escape(desc)}");
        }

        // Structured bonuses
        if (s.TryGetProperty("structuredBonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array && bonuses.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Бонусы:[/]");
            foreach (var b in bonuses.EnumerateArray())
            {
                var bType = GetStr(b, "bonusType", "???");
                var bTarget = GetStr(b, "target", "");
                var bValue = GetStr(b, "value", "");
                var bApp = GetStr(b, "application", "");
                var bCond = GetStr(b, "condition", "");

                var bLine = $"    • [cyan]{Markup.Escape(bType)}[/]";
                if (!string.IsNullOrEmpty(bTarget)) bLine += $" → {Markup.Escape(bTarget)}";
                if (!string.IsNullOrEmpty(bValue)) bLine += $": [yellow]{Markup.Escape(bValue)}[/]";
                if (!string.IsNullOrEmpty(bApp)) bLine += $" [dim]({Markup.Escape(bApp)})[/]";
                lines.Add(bLine);
                if (!string.IsNullOrEmpty(bCond))
                    lines.Add($"      [dim]Условие: {Markup.Escape(bCond)}[/]");
            }
        }

        // Fallback: playerStatBonus (legacy summary) when structuredBonuses is absent
        if (!(s.TryGetProperty("structuredBonuses", out _)))
        {
            var statBonus = GetStr(s, "playerStatBonus", "");
            if (!string.IsNullOrEmpty(statBonus))
            {
                lines.Add("");
                lines.Add($"  [bold]📊 Бонус:[/] [green]{Markup.Escape(statBonus)}[/]");
            }
        }

        // Effect details
        var effectDetails = GetStr(s, "effectDetails", "");
        if (!string.IsNullOrEmpty(effectDetails))
        {
            lines.Add("");
            lines.Add($"  [bold]✨ Эффект:[/] {Markup.Escape(effectDetails)}");
        }

        // Knowledge domain
        var knowledgeDomain = GetStr(s, "knowledgeDomain", "");
        if (!string.IsNullOrEmpty(knowledgeDomain))
            lines.Add($"  📚 Область знаний: [cyan]{Markup.Escape(knowledgeDomain)}[/]");

        // Unlocked active skills
        var unlockedCount = GetStr(s, "unlockedActiveSkillsCount", "");
        var maxUnlock = GetStr(s, "maxUnlockableActiveSkills", "");
        if (!string.IsNullOrEmpty(unlockedCount) || !string.IsNullOrEmpty(maxUnlock))
            lines.Add($"  🔓 Активных навыков: [yellow]{(string.IsNullOrEmpty(unlockedCount) ? "0" : Markup.Escape(unlockedCount))}[/] / [yellow]{(string.IsNullOrEmpty(maxUnlock) ? "?" : Markup.Escape(maxUnlock))}[/]");

        // Combat effect (if passive has one)
        if (s.TryGetProperty("combatEffect", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            var ceDesc = GetStr(ce, "description", GetStr(ce, "effectDescription", ""));
            if (!string.IsNullOrEmpty(ceDesc))
            {
                lines.Add("");
                lines.Add($"  [bold]⚔ Боевой эффект:[/] {Markup.Escape(ceDesc)}");
            }
            // Effects array
            if (ce.TryGetProperty("effects", out var efx) && efx.ValueKind == JsonValueKind.Array && efx.GetArrayLength() > 0)
            {
                foreach (var ef in efx.EnumerateArray())
                {
                    var efType = GetStr(ef, "effectType", GetStr(ef, "type", "?"));
                    var efVal = GetStr(ef, "value", "");
                    var efTarget = GetStr(ef, "targetTypeDisplayName", GetStr(ef, "targetType", GetStr(ef, "target", "")));
                    var efDesc = GetStr(ef, "effectDescription", "");
                    var efLine = $"    • [cyan]{Markup.Escape(efType)}[/]";
                    if (!string.IsNullOrEmpty(efVal)) efLine += $": [yellow]{Markup.Escape(efVal)}[/]";
                    if (!string.IsNullOrEmpty(efTarget)) efLine += $" [dim]({Markup.Escape(efTarget)})[/]";
                    lines.Add(efLine);
                    if (!string.IsNullOrEmpty(efDesc))
                        lines.Add($"      [dim]{Markup.Escape(efDesc)}[/]");
                }
            }
        }

        // Mastery
        AppendMasteryInfo(lines, name, s, masteryLookup);

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 🔮 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 1),
            Expand = true
        });
        WaitForKey();
    }

    private void AppendMasteryInfo(List<string> lines, string skillName, JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        // Try mastery from lookup first, then from skill element itself
        var masteryLevel = GetStr(s, "currentMasteryLevel", GetStr(s, "masteryLevel", ""));
        var masteryProgress = GetInt(s, "currentMasteryProgress", 0);
        var masteryNeeded = GetInt(s, "masteryProgressNeeded", 0);
        var maxMastery = GetStr(s, "maxMasteryLevel", "");

        if (masteryLookup.TryGetValue(skillName, out var mEl))
        {
            if (string.IsNullOrEmpty(masteryLevel))
                masteryLevel = GetStr(mEl, "currentMasteryLevel", GetStr(mEl, "currentMastery", GetStr(mEl, "level", "")));
            if (masteryProgress == 0)
                masteryProgress = GetInt(mEl, "currentMasteryProgress", GetInt(mEl, "progress", 0));
            if (masteryNeeded == 0)
                masteryNeeded = GetInt(mEl, "masteryProgressNeeded", GetInt(mEl, "progressNeeded", 0));
            if (string.IsNullOrEmpty(maxMastery))
                maxMastery = GetStr(mEl, "maxMasteryLevel", "");
        }

        if (!string.IsNullOrEmpty(masteryLevel) || masteryNeeded > 0)
        {
            lines.Add("");
            var masteryLine = $"  📈 Мастерство: [bold cyan]{Markup.Escape(masteryLevel.Length > 0 ? masteryLevel : "1")}[/]";
            if (!string.IsNullOrEmpty(maxMastery))
                masteryLine += $" / {Markup.Escape(maxMastery)}";
            lines.Add(masteryLine);

            if (masteryNeeded > 0)
            {
                var pct = Math.Min(100, masteryProgress * 100 / Math.Max(1, masteryNeeded));
                lines.Add($"  Прогресс мастерства: {ConsoleLayout.CreateBarFromPercent(pct, 10, "cyan")} {masteryProgress}/{masteryNeeded} ({pct}%)");
            }
        }
    }

    private async Task ShowPlayerStats()
    {
        if (_charService == null)
        {
            ShowEmptyPanel(_loc.T("stats"), "Сервис характеристик недоступен");
            WaitForKey();
            return;
        }

        var result = await _charService.ComputeAsync();
        if (result.Stats.Count == 0)
        {
            ShowEmptyPanel(_loc.T("stats"), "Характеристики не определены");
            return;
        }

        var hasBonuses = result.Stats.Values.Any(s => s.PermanentBonus != 0 || s.TemporaryBonus != 0);
        var lines = new List<string>();

        // Header with level and unspent points
        lines.Add($"  [bold]Уровень:[/] [cyan]{result.PlayerLevel}[/]" +
            (result.UnspentStatPoints > 0
                ? $"  │  [bold yellow]⭐ Нераспределённых очков: {result.UnspentStatPoints}[/] [dim](используйте /распределить)[/]"
                : ""));
        lines.Add("");

        // Build table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Expand()
            .AddColumn(new TableColumn("[bold]Характеристика[/]").NoWrap());

        if (hasBonuses)
        {
            table.AddColumn(new TableColumn("[bold]Базовое значение[/]").Centered().NoWrap());
            table.AddColumn(new TableColumn("[bold]Постоянный бонус[/]").Centered().NoWrap());
            table.AddColumn(new TableColumn("[bold]Итоговое значение[/]").Centered().NoWrap());
        }
        else
        {
            table.AddColumn(new TableColumn("[bold]Значение[/]").Centered().NoWrap());
        }
        table.AddColumn(new TableColumn("[bold]Шкала[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Описание[/]"));

        foreach (var charName in Characteristics.All)
        {
            if (!result.Stats.TryGetValue(charName, out var stat)) continue;
            var ruName = Characteristics.RussianNames[charName];
            var displayVal = hasBonuses ? stat.PermanentlyModified : stat.BaseValue;

            // Visual bar based on the permanently modified value
            int filled = Math.Clamp(displayVal / 5, 0, 20);
            int empty = 20 - filled;
            var barColor = displayVal switch
            {
                >= 80 => "gold1",
                >= 50 => "green",
                >= 25 => "yellow",
                _ => "grey"
            };
            var bar = ConsoleLayout.CreateBar(filled, 20, barColor);

            var desc = Characteristics.Descriptions.TryGetValue(charName, out var d) ? $"[dim]{Markup.Escape(d)}[/]" : "";

            if (hasBonuses)
            {
                var bonusStr = stat.PermanentBonus == 0
                    ? "[dim]—[/]"
                    : stat.PermanentBonus > 0
                        ? $"[green]+{stat.PermanentBonus}[/]"
                        : $"[red]{stat.PermanentBonus}[/]";
                var totalColor = stat.PermanentlyModified > stat.BaseValue ? "green" :
                    stat.PermanentlyModified < stat.BaseValue ? "red" : "white";

                // Add temp bonus indicator if present
                var totalStr = $"[bold {totalColor}]{stat.PermanentlyModified}[/]";
                if (stat.TemporaryBonus != 0)
                {
                    var tmpColor = stat.TemporaryBonus > 0 ? "aqua" : "red";
                    totalStr += $" [{tmpColor}]({(stat.TemporaryBonus > 0 ? "+" : "")}{stat.TemporaryBonus})[/]";
                }

                table.AddRow(ruName, $"[white]{stat.BaseValue}[/]", bonusStr, totalStr, bar, desc);
            }
            else
            {
                table.AddRow(ruName, $"[bold]{stat.BaseValue}[/]", bar, desc);
            }
        }

        // Add the table to lines
        lines.Add(""); // will be replaced by table rendering below

        // Render bonus sources detail if any exist
        var sourceLines = new List<string>();
        foreach (var charName in Characteristics.All)
        {
            if (!result.Stats.TryGetValue(charName, out var stat)) continue;
            if (stat.PermanentSources.Count == 0 && stat.TemporarySources.Count == 0) continue;

            var ruName = Characteristics.RussianNames[charName];
            sourceLines.Add($"  [bold]{Markup.Escape(ruName)}:[/]");
            foreach (var src in stat.PermanentSources)
            {
                var sign = src.Value > 0 ? "+" : "";
                sourceLines.Add($"    [green]📌 {Markup.Escape(src.Origin)}:[/] [white]{sign}{src.Value}[/] [dim](пост.)[/]");
            }
            foreach (var src in stat.TemporarySources)
            {
                var sign = src.Value > 0 ? "+" : "";
                sourceLines.Add($"    [aqua]⏳ {Markup.Escape(src.Origin)}:[/] [white]{sign}{src.Value}[/] [dim](врем.)[/]");
            }
        }

        // Render everything
        var headerPanel = new Panel(new Markup(string.Join("\n", lines.Take(2))))
        {
            Border = BoxBorder.None,
            Padding = new Padding(0, 0)
        };
        AnsiConsole.Write(headerPanel);
        AnsiConsole.Write(table);

        if (sourceLines.Count > 0)
        {
            AnsiConsole.WriteLine();
            var detailPanel = new Panel(new Markup(string.Join("\n", sourceLines)))
            {
                Header = new PanelHeader(" 📊 Источники бонусов ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            };
            AnsiConsole.Write(detailPanel);
        }

        // ── Derived combat parameters (from Rules Block 5, 13, 14) ──
        {
            int GetPerm(string name) => result.Stats.TryGetValue(name, out var s) ? s.PermanentlyModified : 0;
            int GetMod(string name) => result.Stats.TryGetValue(name, out var s) ? (s.PermanentlyModified + s.TemporaryBonus) : 0;
            var lvl = result.PlayerLevel;

            var permStr = GetPerm(Characteristics.Strength);
            var permDex = GetPerm(Characteristics.Dexterity);
            var permCon = GetPerm(Characteristics.Constitution);
            var permInt = GetPerm(Characteristics.Intelligence);
            var permWis = GetPerm(Characteristics.Wisdom);
            var permFai = GetPerm(Characteristics.Faith);
            var permLuck = GetPerm(Characteristics.Luck);
            var permSpd = GetPerm(Characteristics.Speed);

            var modStr = GetMod(Characteristics.Strength);
            var modDex = GetMod(Characteristics.Dexterity);
            var modCon = GetMod(Characteristics.Constitution);
            var modInt = GetMod(Characteristics.Intelligence);
            var modLuck = GetMod(Characteristics.Luck);
            var modSpd = GetMod(Characteristics.Speed);

            // MaxHealth% = 100 + floor(PermanentlyModifiedConstitution * 2.0) + floor(PermanentlyModifiedStrength * 1.0)
            var maxHp = 100 + (int)(permCon * 2.0) + permStr;
            // MaxEnergy% = 100 + floor(Con*0.75) + floor(Int*0.75) + floor(Wis*0.75) + floor(Faith*0.75)
            var maxEnergy = 100 + (int)(permCon * 0.75) + (int)(permInt * 0.75) + (int)(permWis * 0.75) + (int)(permFai * 0.75);
            // MaxPoise% = 100 + floor(Str*1.5) + floor(Con*1.5) + floor(Int*1.5) + floor(Wis*1.5)
            var maxPoise = 100 + (int)(permStr * 1.5) + (int)(permCon * 1.5) + (int)(permInt * 1.5) + (int)(permWis * 1.5);
            // Poise regen per turn
            var poiseRegen = 10 + maxPoise / 10;
            // MaxWeight = 30 + floor(Str*1.8 + Con*0.4)
            var maxWeightKg = 30 + (int)(permStr * 1.8 + permCon * 0.4);

            // Critical hit threshold (lower = better) - d20 must roll >= this
            var critThreshold = 20 - permLuck / 20;
            var critRange = critThreshold <= 20 ? $"{critThreshold}-20" : "20";
            // Critical damage multiplier
            var critBonusPct = modLuck / 2;
            var critMult = 1.5 + critBonusPct / 100.0;

            // Attack bonuses
            var levelAtkBonus = 5 + lvl / 10 * 2;
            var strAtkBonus = modStr / 2;   // floor(Str/2.5) but using int division for simplicity
            var dexAtkBonus = modDex / 2;
            var spdAtkBonus = modSpd / 2;
            // More precise: floor(X / 2.5)
            strAtkBonus = (int)(modStr / 2.5);
            dexAtkBonus = (int)(modDex / 2.5);
            spdAtkBonus = (int)(modSpd / 2.5);

            // Innate resistance
            var levelRes = lvl / 10 * 2;
            var conRes = modCon / 10;
            var innateRes = levelRes + conRes;

            var dLines = new List<string>();
            dLines.Add("[bold white]❤️ Пулы:[/]");
            dLines.Add($"  Максимальное здоровье:   [red]{maxHp}%[/]  [dim](100 + Выносливость×2 + Сила×1)[/]");
            dLines.Add($"  Максимальная энергия:    [cyan]{maxEnergy}%[/]  [dim](100 + Выносливость×0.75 + Интеллект×0.75 + Мудрость×0.75 + Вера×0.75)[/]");
            dLines.Add($"  Максимальное равновесие: [blue]{maxPoise}%[/]  [dim](100 + Сила×1.5 + Выносливость×1.5 + Интеллект×1.5 + Мудрость×1.5)[/]");
            dLines.Add($"  Восстановление равновесия: [blue]{poiseRegen}%/ход[/]  [dim](10 + Максимальное равновесие/10)[/]");
            dLines.Add($"  Грузоподъёмность: [white]{maxWeightKg} кг[/]  [dim](30 + Сила×1.8 + Выносливость×0.4)[/]");

            dLines.Add("");
            dLines.Add("[bold white]⚔️ Атака:[/]");
            dLines.Add($"  Бонус от уровня:  [yellow]+{levelAtkBonus}%[/]  [dim](5 + Уровень/10 × 2)[/]");
            dLines.Add($"  Тяжёлое оружие (Сила):        [orange3]+{strAtkBonus}%[/]  [dim](Сила / 2.5)[/]");
            dLines.Add($"  Точное и дальнобойное оружие: [green]+{dexAtkBonus}%[/]  [dim](Ловкость / 2.5)[/]");
            dLines.Add($"  Лёгкое оружие (Скорость):     [cyan]+{spdAtkBonus}%[/]  [dim](Скорость / 2.5)[/]");

            dLines.Add("");
            dLines.Add("[bold white]🎯 Критические удары:[/]");
            dLines.Add($"  Порог крит. удара: [gold1]{critRange}[/] на d20  [dim](20 − Удача/20)[/]");
            dLines.Add($"  Множитель крита:   [gold1]×{critMult:F2}[/]  [dim](1.5 + Удача/200)[/]");

            dLines.Add("");
            dLines.Add("[bold white]🛡️ Защита:[/]");
            dLines.Add($"  Врождённое сопротивление: [blue]{innateRes}%[/]  [dim](Уровень/10×2 + Выносливость/10)[/]");
            dLines.Add($"  [dim]+ бонусы брони, навыков и эффектов (до макс. 90%)[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", dLines)))
            {
                Header = new PanelHeader(" 📐 Производные боевые параметры ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1),
                Expand = true
            });
        }

        WaitForKey();
    }

    private async Task ShowStatDistributionCommand()
    {
        if (_charService == null)
        {
            AnsiConsole.MarkupLine("[red]Сервис характеристик недоступен.[/]");
            WaitForKey();
            return;
        }

        var unspent = await _charService.GetUnspentStatPoints();
        if (unspent <= 0)
        {
            AnsiConsole.MarkupLine("[yellow]Нет нераспределённых очков характеристик.[/]");
            WaitForKey();
            return;
        }

        // Read current base stats
        var statsJson = await _stateManager.LoadGameStateFileAsync("game_state/misc/characteristics.json");
        if (statsJson == null)
        {
            AnsiConsole.MarkupLine("[red]Характеристики не найдены.[/]");
            WaitForKey();
            return;
        }

        var allStats = Characteristics.All;
        var russianNames = Characteristics.RussianNames;
        var currentValues = new int[allStats.Length];
        var allocated = new int[allStats.Length];

        for (int i = 0; i < allStats.Length; i++)
        {
            if (statsJson.RootElement.TryGetProperty(allStats[i], out var val) &&
                val.ValueKind == JsonValueKind.Number)
                currentValues[i] = val.TryGetInt32(out var iv) ? iv : 1;
            else
                currentValues[i] = 1;
        }

        int remaining = unspent;
        int selected = 0;

        while (remaining > 0)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[gold1]Распределение очков ({remaining} осталось)[/]").RuleStyle("gold1"));
            AnsiConsole.WriteLine();

            var table = new Table().Expand().Border(TableBorder.Rounded);
            table.AddColumn("Характеристика");
            table.AddColumn(new TableColumn("База").Centered());
            table.AddColumn(new TableColumn("+").Centered());
            table.AddColumn(new TableColumn("= Итого").Centered());

            for (int i = 0; i < allStats.Length; i++)
            {
                var name = russianNames.GetValueOrDefault(allStats[i], allStats[i]);
                var baseVal = currentValues[i];
                var alloc = allocated[i];
                var total = baseVal + alloc;

                var marker = i == selected ? "► " : "  ";
                var nameStr = i == selected ? $"[bold cyan]{marker}{Markup.Escape(name)}[/]" : $"  {Markup.Escape(name)}";
                var allocStr = alloc > 0 ? $"[green]+{alloc}[/]" : "[dim]0[/]";
                var totalStr = alloc > 0 ? $"[bold]{total}[/]" : $"{total}";

                table.AddRow(nameStr, $"{baseVal}", allocStr, totalStr);
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]↑↓ выбор  →/+ добавить  ←/- убрать  Enter подтвердить[/]");

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selected = (selected - 1 + allStats.Length) % allStats.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selected = (selected + 1) % allStats.Length;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.OemPlus:
                    if (remaining > 0 && currentValues[selected] + allocated[selected] < 100)
                    {
                        allocated[selected]++;
                        remaining--;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.OemMinus:
                    if (allocated[selected] > 0)
                    {
                        allocated[selected]--;
                        remaining++;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (remaining == 0 || AnsiConsole.Confirm($"[yellow]Осталось {remaining} очков. Оставить на потом?[/]"))
                        goto done;
                    break;
            }
        }

        done:
        // Apply allocations (send increments, not final values)
        var allocDict = new Dictionary<string, int>();
        for (int i = 0; i < allStats.Length; i++)
        {
            if (allocated[i] > 0)
                allocDict[allStats[i]] = allocated[i];
        }

        if (allocDict.Count > 0)
            await _charService.DistributePointsAsync(allocDict);
        AnsiConsole.MarkupLine("[green]✓ Характеристики обновлены![/]");
        WaitForKey();
    }

    /// <summary>Set a strategic directive for a companion NPC.</summary>
    private async Task SetCompanionDirective()
    {
        // Load NPC core data
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null) { ShowEmptyPanel("Компаньоны", "НПС не найдены"); return; }
        var renameMap = BuildNpcRenameMap(doc);

        // Collect companions (progressionType == "Companion")
        var companions = new List<(string originalName, string displayName, string npcId, string currentDirective)>();
        foreach (var item in CollectNpcListEntries(doc))
        {
            var progType = GetStr(item, "progressionType", "");
            if (progType.Equals("Companion", StringComparison.OrdinalIgnoreCase))
            {
                var name = GetStr(item, "name", "???");
                var displayName = ResolveNpcDisplayName(item, renameMap);
                var id = GetStr(item, "npcId", GetStr(item, "id", ""));
                var directive = GetStr(item, "playerCompanionDirective", "");
                companions.Add((name, displayName, id, directive));
            }
        }

        if (companions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]У вас нет активных компаньонов.[/]");
            WaitForKey();
            return;
        }

        // Select companion
        var choices = companions.Select(c =>
        {
            var label = $"👤 {c.displayName}";
            if (!string.IsNullOrEmpty(c.currentDirective))
                label = ConsoleLayout.PlainChoiceLabel(label, $"Текущая директива: {c.currentDirective}");
            else
                label = ConsoleLayout.PlainChoiceLabel(label, "Директива не задана");
            return label;
        }).ToList();
        choices.Add("← Назад");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]Выберите компаньона для директивы:[/]")
                .PageSize(10)
                .AddChoices(choices));

        if (selected == "← Назад") return;

        var selIdx = choices.IndexOf(selected);
        if (selIdx < 0 || selIdx >= companions.Count) return;

        var comp = companions[selIdx];

        // Show current directive
        if (!string.IsNullOrEmpty(comp.currentDirective))
        {
            AnsiConsole.MarkupLine($"[yellow]Текущая директива для {Markup.Escape(comp.displayName)}:[/]");
            AnsiConsole.MarkupLine($"  [italic]{Markup.Escape(comp.currentDirective)}[/]");
            AnsiConsole.WriteLine();
        }

        // Input new directive
        var newDirective = AnsiConsole.Ask<string>("[cyan]Новая директива (или пусто для очистки):[/]", "");

        // Write to npc_core.json
        const string path = "game_state/npcs/npc_core.json";
        var rawJson = await _fs.ReadFileAsync(path);
        if (rawJson == null) { AnsiConsole.MarkupLine("[red]Ошибка чтения файла НПС.[/]"); WaitForKey(); return; }

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null) return;

            bool updated = false;
            void UpdateInArray(JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item == null) continue;
                    var itemName = item["name"]?.GetValue<string>() ?? "";
                    var itemId = item["npcId"]?.GetValue<string>() ?? item["id"]?.GetValue<string>() ?? "";
                    if (itemName.Equals(comp.originalName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(comp.npcId) && itemId.Equals(comp.npcId, StringComparison.OrdinalIgnoreCase)))
                    {
                        item["playerCompanionDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                        break;
                    }
                }
            }

            if (node is JsonArray rootArr)
            {
                AnsiConsole.MarkupLine("[red]Невалидный npc_core.json: корень не должен быть массивом.[/]");
                WaitForKey();
                return;
            }
            else if (node is JsonObject obj)
            {
                foreach (var arr in GetNpcCoreArrays(obj))
                    UpdateInArray(arr);
            }

            if (updated)
            {
                var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
                if (string.IsNullOrWhiteSpace(newDirective))
                    AnsiConsole.MarkupLine($"[green]✓ Директива для {Markup.Escape(comp.displayName)} очищена.[/]");
                else
                    AnsiConsole.MarkupLine($"[green]✓ Директива для {Markup.Escape(comp.displayName)} задана:[/] [italic]{Markup.Escape(newDirective)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]НПС не найден в файле. Директива будет передана ГМ с вашим действием.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
        }
        WaitForKey();
    }

    /// <summary>Set a strategic directive for a player-owned faction.</summary>
    private async Task SetFactionDirective()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_core.json");
        if (doc == null) { ShowEmptyPanel("Фракции", "Фракции не обнаружены"); return; }

        // Collect player factions (isPlayerFaction or isPlayerMember)
        var factions = new List<(string name, string factionId, bool isOwner, string currentDirective)>();
        EnumerateFactionCoreEntries(doc.RootElement, item =>
        {
            var isOwner = item.TryGetProperty("isPlayerFaction", out var pf) && pf.ValueKind == JsonValueKind.True;
            var isMember = item.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
            if (isOwner || isMember)
            {
                var name = GetStr(item, "name", "???");
                var id = GetStr(item, "factionId", "");
                var directive = GetStr(item, "playerStrategyDirective", "");
                factions.Add((name, id, isOwner, directive));
            }
        });

        if (factions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]У вас нет своих фракций или членства.[/]");
            WaitForKey();
            return;
        }

        // Select faction
        var choices = factions.Select(f =>
        {
            var label = $"🏛️ {f.name}";
            if (f.isOwner) label = ConsoleLayout.PlainChoiceLabel(label, "Лидер");
            if (!string.IsNullOrEmpty(f.currentDirective))
                label = ConsoleLayout.PlainChoiceLabel(label, $"Стратегия: {f.currentDirective}");
            return label;
        }).ToList();
        choices.Add("← Назад");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold orange1]Выберите фракцию для стратегической директивы:[/]")
                .PageSize(10)
                .AddChoices(choices));

        if (selected == "← Назад") return;

        var selIdx = choices.IndexOf(selected);
        if (selIdx < 0 || selIdx >= factions.Count) return;

        var faction = factions[selIdx];

        if (!faction.isOwner)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Вы не являетесь лидером этой фракции. Директива может быть проигнорирована.[/]");
        }

        // Show current
        if (!string.IsNullOrEmpty(faction.currentDirective))
        {
            AnsiConsole.MarkupLine($"[yellow]Текущая стратегия {Markup.Escape(faction.name)}:[/]");
            AnsiConsole.MarkupLine($"  [italic]{Markup.Escape(faction.currentDirective)}[/]");
            AnsiConsole.WriteLine();
        }

        var newDirective = AnsiConsole.Ask<string>("[cyan]Новая стратегическая директива (или пусто для очистки):[/]", "");

        // Write to faction_core.json
        const string path = "game_state/factions/faction_core.json";
        var rawJson = await _fs.ReadFileAsync(path);
        if (rawJson == null) { AnsiConsole.MarkupLine("[red]Ошибка чтения файла фракций.[/]"); WaitForKey(); return; }

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null) return;

            bool updated = false;
            void UpdateInArray(JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is not JsonObject itemObj) continue;
                    var itemName = itemObj["name"]?.GetValue<string>() ?? "";
                    var itemId = itemObj["factionId"]?.GetValue<string>() ?? "";
                    if ((!string.IsNullOrEmpty(faction.factionId) && itemId.Equals(faction.factionId, StringComparison.OrdinalIgnoreCase)) ||
                        itemName.Equals(faction.name, StringComparison.OrdinalIgnoreCase))
                    {
                        itemObj["playerStrategyDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                    }
                }
            }

            if (node is JsonArray rootArr)
                UpdateInArray(rootArr);
            else if (node is JsonObject obj)
            {
                if (obj["factionDataChanges"] is JsonArray fd) UpdateInArray(fd);
                if (obj["factions"] is JsonArray fa) UpdateInArray(fa);

                if (!updated)
                {
                    // Single faction object
                    var itemName = obj["name"]?.GetValue<string>() ?? "";
                    var itemId = obj["factionId"]?.GetValue<string>() ?? "";
                    if ((!string.IsNullOrEmpty(faction.factionId) && itemId.Equals(faction.factionId, StringComparison.OrdinalIgnoreCase)) ||
                        itemName.Equals(faction.name, StringComparison.OrdinalIgnoreCase))
                    {
                        obj["playerStrategyDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
                if (string.IsNullOrWhiteSpace(newDirective))
                    AnsiConsole.MarkupLine($"[green]✓ Стратегия {Markup.Escape(faction.name)} очищена.[/]");
                else
                    AnsiConsole.MarkupLine($"[green]✓ Стратегия {Markup.Escape(faction.name)} задана:[/] [italic]{Markup.Escape(newDirective)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Фракция не найдена в файле.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
        }
        WaitForKey();
    }

    private async Task ShowFactions()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_core.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("factions"), "Фракции не обнаружены"); return; }

        // Collect factions
        var factions = new List<(string name, JsonElement el)>();
        EnumerateFactionCoreEntries(doc.RootElement, item =>
        {
            factions.Add((GetStr(item, "name", "???"), item));
        });

        if (factions.Count == 0) { ShowEmptyPanel(_loc.T("factions"), "Фракции не обнаружены"); return; }

        // Load supplementary files
        var projDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_projects.json");
        var strDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_structure.json");
        var chrDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_chronicles.json");
        var resDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_resources.json");
        var custDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_custom.json");

        // Interactive selector loop
        while (true)
        {
            var factionNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (factionName, factionEl) in factions)
            {
                var id = GetStr(factionEl, "factionId", "");
                if (!string.IsNullOrWhiteSpace(id) && !factionNamesById.ContainsKey(id))
                    factionNamesById[id] = factionName;
            }

            var choices = new List<string>();
            foreach (var (name, el) in factions)
            {
                var rep = GetInt(el, "reputation", 0);
                var isMember = el.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
                var lvl = GetStr(el, "level", "");
                var (repLabel, repColor) = GetReputationLabel(rep);

                var label = $"🏛️ {name}";
                if (!string.IsNullOrEmpty(lvl))
                    label = ConsoleLayout.PlainChoiceLabel(label, $"Уровень {lvl}");
                label = ConsoleLayout.PlainChoiceLabel(label, $"{repLabel} ({rep})");
                if (isMember)
                    label = ConsoleLayout.PlainChoiceLabel(label, "Вы связаны с этой фракцией");
                choices.Add(label);
            }
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold orange1]⚔ Фракции[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= factions.Count) break;

            await ShowFactionDetailPanel(factions[selIdx].el, projDoc, strDoc, chrDoc, resDoc, custDoc, factionNamesById);
        }
    }

    /// <summary>Full detailed faction panel with all subsystems.</summary>
    private async Task ShowFactionDetailPanel(JsonElement f, JsonDocument? projDoc,
        JsonDocument? strDoc, JsonDocument? chrDoc, JsonDocument? resDoc, JsonDocument? custDoc,
        Dictionary<string, string> factionNamesById)
    {
        var name = GetStr(f, "name", "???");
        var factionId = GetStr(f, "factionId", "");
        var content = new Grid().AddColumn(new GridColumn());
        var lines = new List<string>();

        // ═══ Header ═══
        var desc = GetStr(f, "description", "");
        var factionColor = GetStr(f, "factionColor", "");
        // Use faction color for header if valid HEX
        var headerColor = "orange1";
        if (!string.IsNullOrEmpty(factionColor) && factionColor.StartsWith("#") && factionColor.Length >= 7)
        {
            headerColor = factionColor; // Spectre.Console supports #RRGGBB
        }
        content.AddRow(new Markup($"[bold {headerColor}]🏛️ {Markup.Escape(name)}[/]"));
        if (!string.IsNullOrEmpty(factionColor))
            content.AddRow(new Markup($"[dim]Цвет фракции: [{headerColor}]██[/] {Markup.Escape(factionColor)}[/]"));
        if (!string.IsNullOrEmpty(desc))
        {
            content.AddRow(new Markup($"[dim italic]{Markup.Escape(desc)}[/]"));
        }

        // ═══ Core stats ═══
        var lvl = GetInt(f, "level", 0);
        var xp = GetInt(f, "experience", 0);
        var xpNext = GetInt(f, "experienceForNextLevel", 0);
        var rep = GetInt(f, "reputation", 0);
        var repDesc = GetStr(f, "reputationDescription", "");
        var playerRank = GetStr(f, "playerRank", "");
        var playerBranch = GetStr(f, "playerBranch", "");
        var isMember = f.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
        var isPlayerFaction = f.TryGetProperty("isPlayerFaction", out var pf) && pf.ValueKind == JsonValueKind.True;
        var archetype = GetStr(f, "developmentArchetype", "");
        var summaryTable = ConsoleLayout.CreateInfoTable();

        // Level + XP bar
        if (lvl > 0)
        {
            var xpLine = $"[bold yellow]{lvl}[/]";
            if (!string.IsNullOrEmpty(archetype))
                xpLine += $" [dim]({Markup.Escape(archetype)})[/]";
            summaryTable.AddRow(new Markup("[yellow]Уровень развития[/]"), new Markup(xpLine));

            if (xpNext > 0)
            {
                var pct = Math.Min(100, xp * 100 / Math.Max(1, xpNext));
                var progressTable = ConsoleLayout.CreateBarMetricTable();
                progressTable.AddRow(
                    new Markup("[cyan]Прогресс развития[/]"),
                    new Markup(ConsoleLayout.CreateBarFromPercent(pct, 16, "cyan")),
                    new Markup($"[cyan]{xp}/{xpNext}[/]"),
                    new Markup($"[dim]{pct}%[/]"));
                content.AddRow(summaryTable);
                content.AddRow(progressTable);
                summaryTable = ConsoleLayout.CreateInfoTable();
            }

            // Custom archetype priorities (Rule 21.1.2)
            if (f.TryGetProperty("customArchetypePriorities", out var cap) && cap.ValueKind == JsonValueKind.Object)
            {
                var primary = GetStr(cap, "primary", "");
                var secondary = GetStr(cap, "secondary", "");
                var tertiary = GetStr(cap, "tertiary", "");
                if (!string.IsNullOrEmpty(primary))
                    summaryTable.AddRow(new Markup("[dim]Приоритеты развития[/]"), new Markup($"[bold]{Markup.Escape(primary)}[/] > [yellow]{Markup.Escape(secondary)}[/] > [dim]{Markup.Escape(tertiary)}[/]"));
            }
        }

        // Reputation with label
        var (repLabel, repColor) = GetReputationLabel(rep);
        summaryTable.AddRow(new Markup($"[{repColor}]Репутация[/]"), new Markup($"[{repColor}]{rep} — {repLabel}[/]"));
        if (!string.IsNullOrEmpty(repDesc))
            summaryTable.AddRow(new Markup("[dim]Пояснение[/]"), new Markup($"[dim]{Markup.Escape(repDesc)}[/]"));

        // Membership
        if (isPlayerFaction)
            summaryTable.AddRow(new Markup("[gold1]Статус игрока[/]"), new Markup("[bold gold1]Вы — лидер этой фракции[/]"));
        else if (isMember)
        {
            var memberLine = "[green]Член фракции[/]";
            if (!string.IsNullOrEmpty(playerRank))
                memberLine += $" | Ранг: [yellow]{Markup.Escape(playerRank)}[/]";
            if (!string.IsNullOrEmpty(playerBranch))
                memberLine += $" [dim]({Markup.Escape(ResolveFactionBranchDisplayName(f, strDoc, name, factionId, playerBranch))})[/]";
            summaryTable.AddRow(new Markup("[green]Статус игрока[/]"), new Markup(memberLine));
        }

        // Strategy directive
        var directive = GetStr(f, "playerStrategyDirective", "");
        if (!string.IsNullOrEmpty(directive))
        {
            summaryTable.AddRow(new Markup("[cyan]Стратегическая директива[/]"), new Markup($"[italic cyan]{Markup.Escape(directive)}[/]"));
        }
        else if (isPlayerFaction)
        {
            summaryTable.AddRow(new Markup("[dim]Стратегическая директива[/]"), new Markup("[dim italic]не задана (используйте /директива_фракции)[/]"));
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        // ═══ Power Profile ═══
        if (f.TryGetProperty("powerProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Профиль силы:[/]");
            var powerNames = new Dictionary<string, string>
            {
                ["military"] = "⚔ Военная",
                ["economic"] = "💰 Экономика",
                ["social"] = "💬 Социальная",
                ["covert"] = "🗡 Тайная",
                ["logistics"] = "📦 Логистика",
                ["stability"] = "🛡 Стабильность",
                ["arcane_tech"] = "✨ Магия/Тех",
                ["exploration"] = "🔍 Исследование"
            };
            foreach (var (key, label) in powerNames)
            {
                if (pp.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var val))
                {
                    var tier = GetPowerTierLabel(val);
                    lines.Add($"    {Markup.Escape(label)}: {PowerBar(val)}  [white]{val}[/] [dim]{tier}[/]");
                }
            }
        }

        // ═══ Resources ═══
        RenderFactionResources(lines, f, resDoc, name, factionId);

        // ═══ Controlled Territories ═══
        if (f.TryGetProperty("controlledTerritories", out var terr) && terr.ValueKind == JsonValueKind.Array && terr.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🗺 Территории:[/]");
            foreach (var t in terr.EnumerateArray())
            {
                var locName = GetStr(t, "locationName", GetStr(t, "locationId", "?"));
                lines.Add($"    📍 [cyan]{Markup.Escape(locName)}[/]");
            }
        }

        // ═══ Relations ═══
        if (f.TryGetProperty("relations", out var rels) && rels.ValueKind == JsonValueKind.Array && rels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🤝 Отношения:[/]");
            foreach (var rel in rels.EnumerateArray())
            {
                var targetFactionId = GetStr(rel, "targetFactionId", "");
                var target = !string.IsNullOrWhiteSpace(targetFactionId) &&
                             factionNamesById.TryGetValue(targetFactionId, out var targetFactionName)
                    ? targetFactionName
                    : GetStr(rel, "targetFactionName", targetFactionId);
                var status = GetStr(rel, "status", "Neutral");
                var relDesc = GetStr(rel, "description", "");
                var (relIcon, relColor) = status.ToLowerInvariant() switch
                {
                    "allied" => ("🤝", "green"),
                    "patron" => ("👑", "green"),
                    "vassal" => ("🔗", "yellow"),
                    "war" => ("⚔", "bold red"),
                    "rivalry" => ("💢", "red"),
                    _ => ("↔", "grey")
                };
                var line = $"    {relIcon} [{relColor}]{Markup.Escape(status)}[/] → [white]{Markup.Escape(target)}[/]";
                if (!string.IsNullOrEmpty(relDesc))
                    line += $" — [dim]{Markup.Escape(relDesc)}[/]";
                lines.Add(line);
            }
        }

        // ═══ Active Projects ═══
        RenderFactionProjects(lines, f, projDoc, name, factionId);

        // ═══ Structured Bonuses ═══
        RenderFactionStructuredBonuses(lines, f, strDoc, name, factionId);

        // ═══ Custom States ═══
        RenderFactionCustomStates(lines, f, custDoc, name, factionId);

        // ═══ Rank Hierarchy ═══
        RenderFactionRanks(lines, f, strDoc, name, factionId, playerRank);

        // ═══ Chronicles ═══
        RenderFactionChronicles(lines, f, chrDoc, name, factionId);

        if (lines.Count > 0)
            content.AddRow(new Markup(string.Join("\n", lines)));

        AnsiConsole.Write(new Panel(content)
        {
            Header = new PanelHeader($" 🏛️ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        await WaitForKeyWithImage("faction", name, GetStr(f, "image_prompt", ""), GetStr(f, "factionId", name));
    }

    // ═══════════════════════════════════════════════════════════
    //  Faction Detail Sub-Renderers
    // ═══════════════════════════════════════════════════════════

    private static bool FactionSidecarMatches(JsonElement item, string factionName, string factionId)
    {
        var itemFactionId = GetStr(item, "factionId", "");
        return !string.IsNullOrWhiteSpace(factionId) &&
               !string.IsNullOrWhiteSpace(itemFactionId) &&
               itemFactionId.Equals(factionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFactionBranchDisplayName(JsonElement faction, JsonDocument? structureDoc,
        string factionName, string factionId, string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
            return branchId;

        JsonElement? ranksEl = null;

        if (structureDoc != null)
        {
            EnumerateJsonItems(structureDoc.RootElement, item =>
            {
                if (ranksEl != null)
                    return;

                if (FactionSidecarMatches(item, factionName, factionId) &&
                    item.TryGetProperty("ranks", out var sidecarRanks) &&
                    sidecarRanks.ValueKind == JsonValueKind.Object)
                {
                    ranksEl = sidecarRanks;
                }
            });
        }

        if (ranksEl == null &&
            faction.TryGetProperty("ranks", out var coreRanks) &&
            coreRanks.ValueKind == JsonValueKind.Object)
        {
            ranksEl = coreRanks;
        }

        if (ranksEl.HasValue &&
            ranksEl.Value.TryGetProperty("branches", out var branches) &&
            branches.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in branches.EnumerateArray())
            {
                var candidateId = GetStr(branch, "branchId", "");
                if (candidateId.Equals(branchId, StringComparison.OrdinalIgnoreCase))
                    return GetStr(branch, "displayName", candidateId);
            }
        }

        return branchId;
    }

	    private static void RenderFactionStructuredBonuses(List<string> lines, JsonElement f,
	        JsonDocument? strDoc, string factionName, string factionId)
	    {
	        JsonElement? bonusesEl = null;
            var sidecarAvailable = strDoc != null;
            var sidecarMatched = false;

	        if (strDoc != null)
	        {
	            EnumerateJsonItems(strDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId) &&
	                    item.TryGetProperty("structuredBonuses", out var sidecarBonuses) &&
	                    sidecarBonuses.ValueKind == JsonValueKind.Array)
	                {
                        sidecarMatched = true;
	                    bonusesEl = sidecarBonuses;
	                }
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            bonusesEl == null &&
	            f.TryGetProperty("structuredBonuses", out var bonuses) &&
	            bonuses.ValueKind == JsonValueKind.Array &&
	            bonuses.GetArrayLength() > 0)
	        {
	            bonusesEl = bonuses;
	        }

	        if (bonusesEl == null || bonusesEl.Value.ValueKind != JsonValueKind.Array || bonusesEl.Value.GetArrayLength() == 0)
	            return;

        lines.Add("");
        lines.Add("  [bold]✨ Бонусы:[/]");
        foreach (var b in bonusesEl.Value.EnumerateArray())
        {
            var bDesc = GetStr(b, "description", "");
            var bType = GetStr(b, "bonusType", "");
            var bTarget = GetStr(b, "target", "");
            var bValueType = GetStr(b, "valueType", "");
            var bVal = GetStr(b, "value", "0");
            var bApp = GetStr(b, "application", "");
            var bCond = GetStr(b, "condition", "");

            var line = $"    ✦ [cyan]{Markup.Escape(bDesc)}[/]";
            if (string.IsNullOrEmpty(bDesc))
                line = $"    ✦ [cyan]{Markup.Escape(bType)}: {Markup.Escape(bTarget)} +{Markup.Escape(bVal)}[/]";
            if (!string.IsNullOrEmpty(bValueType))
                line += $" [dim][{Markup.Escape(bValueType)}][/]";
            if (!string.IsNullOrEmpty(bApp) && bApp.ToLowerInvariant() == "conditional" && !string.IsNullOrEmpty(bCond))
                line += $" [dim](если: {Markup.Escape(bCond)})[/]";
            lines.Add(line);
        }
    }

	    private static void RenderFactionResources(List<string> lines, JsonElement f,
	        JsonDocument? resDoc, string factionName, string factionId)
	    {
	        var hasResources = false;
            var sidecarAvailable = resDoc != null;
            var sidecarMatched = false;

	        void RenderResourceArray(JsonElement arr, string label, string icon)
	        {
	            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return;
            if (!hasResources) { lines.Add(""); lines.Add("  [bold]💰 Ресурсы:[/]"); hasResources = true; }
            lines.Add($"    [dim]{label}:[/]");
            foreach (var r in arr.EnumerateArray())
            {
                var rName = GetStr(r, "resourceName", "?");
                var stock = GetStr(r, "currentStockpile", "0");
                var income = GetStr(r, "incomePerCycle", "");
                var upkeep = GetStr(r, "upkeepPerCycle", "");

                var line = $"      {icon} [white]{Markup.Escape(rName)}[/]: [cyan]{Markup.Escape(stock)}[/]";
                if (!string.IsNullOrEmpty(income) && income != "0")
                    line += $" [green](+{Markup.Escape(income)}/цикл)[/]";
                if (!string.IsNullOrEmpty(upkeep) && upkeep != "0")
                    line += $" [red](-{Markup.Escape(upkeep)}/цикл)[/]";
                lines.Add(line);
            }
        }

	        if (resDoc != null)
	        {
	            EnumerateJsonItems(resDoc.RootElement, item =>
	            {
	                if (!FactionSidecarMatches(item, factionName, factionId)) return;
                    sidecarMatched = true;
	                if (item.TryGetProperty("metaResources", out var mr2))
	                    RenderResourceArray(mr2, "Основные", "💎");
	                if (item.TryGetProperty("strategicGoods", out var sg2))
	                    RenderResourceArray(sg2, "Стратегические товары", "📦");
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            f.TryGetProperty("resources", out var res) &&
	            res.ValueKind == JsonValueKind.Object)
	        {
	            if (res.TryGetProperty("metaResources", out var mr))
	                RenderResourceArray(mr, "Основные", "💎");
	            if (res.TryGetProperty("strategicGoods", out var sg))
	                RenderResourceArray(sg, "Стратегические товары", "📦");
	        }
	    }

	    private static void RenderFactionProjects(List<string> lines, JsonElement f,
	        JsonDocument? projDoc, string factionName, string factionId)
	    {
	        var activeProjects = new List<JsonElement>();
	        var completedProjects = new List<JsonElement>();
            var sidecarAvailable = projDoc != null;
            var sidecarMatched = false;

	        if (projDoc != null)
	        {
	            EnumerateJsonItems(projDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId))
	                {
                        sidecarMatched = true;
	                    if (item.TryGetProperty("finalState", out _) || item.TryGetProperty("completionTurn", out _))
	                        completedProjects.Add(item);
	                    else
	                        activeProjects.Add(item);
	                }
	            });
	        }

	        if (!sidecarAvailable || !sidecarMatched)
	        {
	            if (f.TryGetProperty("activeProjects", out var ap) && ap.ValueKind == JsonValueKind.Array)
	                foreach (var p in ap.EnumerateArray()) activeProjects.Add(p);
	            if (f.TryGetProperty("completedProjects", out var cpCore) && cpCore.ValueKind == JsonValueKind.Array)
	                foreach (var p in cpCore.EnumerateArray()) completedProjects.Add(p);
	        }

        if (activeProjects.Count == 0 && completedProjects.Count == 0) return;

        if (activeProjects.Count > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🔨 Активные проекты:[/]");
            foreach (var p in activeProjects)
            {
                var pName = GetStr(p, "projectName", GetStr(p, "name", "?"));
                var pState = GetStr(p, "activeState", "");
                var pDesc = GetStr(p, "description", "");
                var step = GetInt(p, "currentStep", 0);
                var totalSteps = GetInt(p, "totalSteps", 0);
                var timeSpent = GetInt(p, "timeSpentMinutes", 0);
                var timeTotal = GetInt(p, "totalTimeCostMinutes", 0);

                var stateColor = pState.ToLowerInvariant() switch
                {
                    "completed" => "green",
                    "abandoned" => "red",
                    _ => "yellow"
                };
                lines.Add($"    🔨 [white]{Markup.Escape(pName)}[/] [{stateColor}]({Markup.Escape(pState)})[/]");

                if (!string.IsNullOrEmpty(pDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(pDesc)}[/]");

                if (totalSteps > 0)
                {
                    var stepPct = Math.Min(100, step * 100 / totalSteps);
                    lines.Add($"      Этапы выполнения: {ConsoleLayout.CreateBarFromPercent(stepPct, 10, "cyan")} {step}/{totalSteps}");
                }

                if (timeTotal > 0)
                {
                    var timePct = Math.Min(100, timeSpent * 100 / timeTotal);
                    var barColor = timePct >= 80 ? "green" : timePct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Время выполнения: {ConsoleLayout.CreateBarFromPercent(timePct, 10, barColor)} {FormatMinutes(timeSpent)}/{FormatMinutes(timeTotal)}");
                }

                if (p.TryGetProperty("totalResourceCost", out var rc))
                {
                    var costs = new List<string>();
                    if (rc.ValueKind == JsonValueKind.Array)
                        foreach (var c in rc.EnumerateArray())
                            costs.Add($"{Markup.Escape(GetStr(c, "resourceName", "?"))}: {GetStr(c, "totalAmount", "?")}");
                    else if (rc.ValueKind == JsonValueKind.Object)
                        foreach (var c in rc.EnumerateObject())
                            costs.Add($"{Markup.Escape(c.Name)}: {c.Value}");
                    if (costs.Count > 0)
                    {
                        var spentParts = new List<string>();
                        if (p.TryGetProperty("resourcesSpent", out var rs))
                        {
                            if (rs.ValueKind == JsonValueKind.Array)
                                foreach (var c in rs.EnumerateArray())
                                    spentParts.Add($"{Markup.Escape(GetStr(c, "resourceName", "?"))}: {GetStr(c, "amountSpent", "?")}");
                            else if (rs.ValueKind == JsonValueKind.Object)
                                foreach (var c in rs.EnumerateObject())
                                    spentParts.Add($"{Markup.Escape(c.Name)}: {c.Value}");
                        }
                        var spentStr = spentParts.Count > 0 ? $" [dim](потрачено: {string.Join(", ", spentParts)})[/]" : "";
                        lines.Add($"      💰 Стоимость: {string.Join(", ", costs)}{spentStr}");
                    }
                }
            }
        }

        if (completedProjects.Count > 0)
        {
            lines.Add("    [dim]─── Завершённые: ───[/]");
            foreach (var p in completedProjects)
            {
                var pName = GetStr(p, "projectName", GetStr(p, "name", "?"));
                var finalState = GetStr(p, "finalState", "");
                var turn = GetStr(p, "completionTurn", "");
                var stColor = finalState.ToLowerInvariant() == "abandoned" ? "red" : "green";
                var line = $"    ✓ [dim]{Markup.Escape(pName)}[/] [{stColor}]{Markup.Escape(finalState)}[/]";
                if (!string.IsNullOrEmpty(turn)) line += $" [dim](ход {Markup.Escape(turn)})[/]";
                lines.Add(line);
            }
        }
    }

    private static void RenderFactionCustomStates(List<string> lines,
        JsonElement f, JsonDocument? custDoc, string factionName, string factionId)
    {
        // Collect state items for this faction (supports both flat and nested formats per Rule 21.F.1)
        var stateItems = new List<JsonElement>();
        if (custDoc != null)
        {
            EnumerateJsonItems(custDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId)) return;

                // Nested: statesToAddOrUpdate array (Rule 21.F.1)
                if (item.TryGetProperty("statesToAddOrUpdate", out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in nested.EnumerateArray()) stateItems.Add(s);
                }
                else if (item.TryGetProperty("customStates", out var customStates) && customStates.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in customStates.EnumerateArray()) stateItems.Add(s);
                }
                else if (item.TryGetProperty("stateName", out _) || item.TryGetProperty("currentValue", out _))
                {
                    // Flat format: entry itself is a state
                    stateItems.Add(item);
                }
            });
        }

        if (stateItems.Count == 0 &&
            f.TryGetProperty("customStates", out var coreStates) &&
            coreStates.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in coreStates.EnumerateArray())
                stateItems.Add(s);
        }

        if (stateItems.Count == 0) return;

        lines.Add("");
        lines.Add("  [bold magenta]📊 Особые состояния:[/]");
        foreach (var s in stateItems)
            RenderCustomStateItem(lines, s, "    ");
    }

	    private static void RenderFactionRanks(List<string> lines, JsonElement f,
	        JsonDocument? strDoc, string factionName, string factionId, string currentPlayerRank)
	    {
	        JsonElement? ranksEl = null;
            var sidecarAvailable = strDoc != null;
            var sidecarMatched = false;
	        if (strDoc != null)
	        {
	            EnumerateJsonItems(strDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId) &&
	                    item.TryGetProperty("ranks", out var sr))
                    {
                        sidecarMatched = true;
	                    ranksEl = sr;
                    }
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            ranksEl == null &&
	            f.TryGetProperty("ranks", out var r) &&
	            r.ValueKind == JsonValueKind.Object)
	        {
	            ranksEl = r;
	        }

        if (ranksEl == null) return;
        var re = ranksEl.Value;

        // Branching hierarchy
        if (re.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array && branches.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]👑 Иерархия рангов:[/]");
            var branchNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var branch in branches.EnumerateArray())
            {
                var branchId = GetStr(branch, "branchId", "");
                var branchDisplayName = GetStr(branch, "displayName", branchId);
                if (!string.IsNullOrWhiteSpace(branchId) && !branchNamesById.ContainsKey(branchId))
                    branchNamesById[branchId] = branchDisplayName;
            }

            foreach (var branch in branches.EnumerateArray())
            {
                var brId = GetStr(branch, "branchId", "");
                var brName = GetStr(branch, "displayName", brId);
                var isCore = branch.TryGetProperty("isCoreBranch", out var cb) && cb.ValueKind == JsonValueKind.True;
                var brLabel = isCore ? $"[bold]{Markup.Escape(brName)}[/] [dim](основная)[/]" : Markup.Escape(brName);
                lines.Add($"    🔹 {brLabel}");

                if (branch.TryGetProperty("ranks", out var rankArr) && rankArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rank in rankArr.EnumerateArray())
                    {
                        var rankNameM = GetStr(rank, "rankNameMale", GetStr(rank, "name", "?"));
                        var rankNameF = GetStr(rank, "rankNameFemale", "");
                        var reqRep = GetStr(rank, "requiredReputation", "");
                        var unlockCond = GetStr(rank, "unlockCondition", "");
                        var isJunction = rank.TryGetProperty("isJunctionPoint", out var jp) && jp.ValueKind == JsonValueKind.True;
                        var isCurrent = rankNameM.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrEmpty(rankNameF) && rankNameF.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase));

                        // Rank name (male / female)
                        var displayName = rankNameM;
                        if (!string.IsNullOrEmpty(rankNameF) && rankNameF != rankNameM)
                            displayName += $" / {rankNameF}";

                        var line = isCurrent
                            ? $"      [bold green]► {Markup.Escape(displayName)}[/] [green](ваш ранг)[/]"
                            : $"      • {Markup.Escape(displayName)}";
                        if (!string.IsNullOrEmpty(reqRep)) line += $" [dim](реп. {Markup.Escape(reqRep)}+)[/]";
                        if (isJunction) line += " [yellow]⚡ развилка[/]";
                        lines.Add(line);

                        // Unlock condition (quest-like requirement)
                        if (!string.IsNullOrEmpty(unlockCond))
                            lines.Add($"        🔑 [italic yellow]{Markup.Escape(unlockCond)}[/]");

                        // Benefits (array of strings per Block 21)
                        if (rank.TryGetProperty("benefits", out var benefitsEl))
                        {
                            if (benefitsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var b in benefitsEl.EnumerateArray())
                                {
                                    var bStr = b.ValueKind == JsonValueKind.String ? (b.GetString() ?? "") : "";
                                    if (!string.IsNullOrEmpty(bStr))
                                        lines.Add($"        ✓ [dim]{Markup.Escape(bStr)}[/]");
                                }
                            }
                            else if (benefitsEl.ValueKind == JsonValueKind.String)
                            {
                                var bStr = benefitsEl.GetString() ?? "";
                                if (!string.IsNullOrEmpty(bStr))
                                    lines.Add($"        ✓ [dim]{Markup.Escape(bStr)}[/]");
                            }
                        }

                        // Available branches at junction point
                        if (isJunction && rank.TryGetProperty("availableBranches", out var avBranches)
                            && avBranches.ValueKind == JsonValueKind.Array && avBranches.GetArrayLength() > 0)
                        {
                            lines.Add("        [yellow]Доступные ветки:[/]");
                            foreach (var ab in avBranches.EnumerateArray())
                            {
                                var abName = ab.ValueKind == JsonValueKind.String
                                    ? branchNamesById.GetValueOrDefault(ab.GetString() ?? "", ab.GetString() ?? "?")
                                    : GetStr(ab, "displayName", branchNamesById.GetValueOrDefault(GetStr(ab, "branchId", ""), GetStr(ab, "branchId", "?")));
                                lines.Add($"          ↳ [yellow]{Markup.Escape(abName)}[/]");
                            }
                        }
                    }
                }
            }
        }
        else if (re.ValueKind == JsonValueKind.Array)
        {
            // Simple rank array fallback
            lines.Add("");
            lines.Add("  [bold]👑 Ранги:[/]");
            foreach (var rank in re.EnumerateArray())
            {
                if (rank.ValueKind == JsonValueKind.String)
                {
                    var rn = rank.GetString() ?? "?";
                    var isCur = rn.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase);
                    lines.Add(isCur
                        ? $"    [bold green]► {Markup.Escape(rn)}[/] [green](ваш ранг)[/]"
                        : $"    • {Markup.Escape(rn)}");
                }
                else if (rank.ValueKind == JsonValueKind.Object)
                {
                    var rnM = GetStr(rank, "rankNameMale", GetStr(rank, "name", "?"));
                    var rnF = GetStr(rank, "rankNameFemale", "");
                    var displayName = rnM;
                    if (!string.IsNullOrEmpty(rnF) && rnF != rnM)
                        displayName += $" / {rnF}";
                    var reqRep = GetStr(rank, "requiredReputation", "");
                    var unlockCond = GetStr(rank, "unlockCondition", "");
                    var isCur = rnM.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(rnF) && rnF.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase));

                    var line = isCur
                        ? $"    [bold green]► {Markup.Escape(displayName)}[/] [green](ваш ранг)[/]"
                        : $"    • {Markup.Escape(displayName)}";
                    if (!string.IsNullOrEmpty(reqRep)) line += $" [dim](реп. {Markup.Escape(reqRep)}+)[/]";
                    lines.Add(line);

                    if (!string.IsNullOrEmpty(unlockCond))
                        lines.Add($"      🔑 [italic yellow]{Markup.Escape(unlockCond)}[/]");
                    if (rank.TryGetProperty("benefits", out var benefitsEl2))
                    {
                        if (benefitsEl2.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var b in benefitsEl2.EnumerateArray())
                            {
                                var bStr = b.ValueKind == JsonValueKind.String ? (b.GetString() ?? "") : "";
                                if (!string.IsNullOrEmpty(bStr))
                                    lines.Add($"      ✓ [dim]{Markup.Escape(bStr)}[/]");
                            }
                        }
                        else if (benefitsEl2.ValueKind == JsonValueKind.String)
                        {
                            var bStr = benefitsEl2.GetString() ?? "";
                            if (!string.IsNullOrEmpty(bStr))
                                lines.Add($"      ✓ [dim]{Markup.Escape(bStr)}[/]");
                        }
                    }
                }
            }
        }
    }

    private static void RenderFactionChronicles(List<string> lines, JsonElement f,
        JsonDocument? chrDoc, string factionName, string factionId)
    {
        var entries = new List<string>();

        // From core scribeChronicle
        if (f.TryGetProperty("scribeChronicle", out var sc) && sc.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in sc.EnumerateArray())
            {
                var txt = e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString();
                if (!string.IsNullOrEmpty(txt)) entries.Add(txt);
            }
        }

        // From faction_chronicles.json
        if (chrDoc != null)
        {
            EnumerateJsonItems(chrDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId)) return;
                var entry = GetStr(item, "entry", GetStr(item, "chronicle", GetStr(item, "text", "")));
                if (!string.IsNullOrEmpty(entry) && !entries.Contains(entry))
                    entries.Add(entry);
            });
        }

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold]📜 Хроники ({entries.Count}):[/]");
        for (var i = 0; i < entries.Count; i++)
            lines.Add($"    [dim]{Markup.Escape(entries[i])}[/]");
    }

    // ═══════════════════════════════════════════════════════════
    //  Faction Helper Methods
    // ═══════════════════════════════════════════════════════════

    /// <summary>Reputation label from the -400..+400 scale (Rule 21.1.A).</summary>
    private static (string label, string color) GetReputationLabel(int rep) => rep switch
    {
        <= -201 => ("Заклятый враг", "bold red"),
        <= -51 => ("Враг", "red"),
        <= -1 => ("Недоверие", "orange1"),
        <= 100 => ("Нейтралитет", "grey"),
        <= 250 => ("Сочувствующий", "yellow"),
        <= 350 => ("Почётный член", "green"),
        _ => ("Живая легенда", "bold gold1")
    };

    /// <summary>Power tier label from calibration matrix (Rule 21.3.0).</summary>
    private static string GetPowerTierLabel(int val) => val switch
    {
        <= 10 => "Незначительная",
        <= 30 => "Мелкая",
        <= 60 => "Региональная",
        <= 80 => "Крупная",
        <= 100 => "Мировая угроза",
        _ => "Трансцендентная"
    };

    /// <summary>Colored power bar for 0..100+ values.</summary>
    private static string PowerBar(int value)
    {
        var clamped = Math.Clamp(value, 0, 120);
        var filled = Math.Min(clamped / 10, 10);
        var color = value switch { <= 20 => "grey", <= 50 => "yellow", <= 80 => "orange1", _ => "red" };
        return ConsoleLayout.CreateBar(filled, 10, color);
    }

    private async Task ShowWorldNews()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_events.json");

        var text = new List<string>();
        if (doc != null)
        {
            EnumerateArray(doc.RootElement, "events", item => RenderWorldEventDetailed(text, item));
            // Also try root-level array
            if (text.Count == 0)
                EnumerateJsonItems(doc.RootElement, item => RenderWorldEventDetailed(text, item));
        }

        // Remove trailing empty spacer
        while (text.Count > 0 && string.IsNullOrEmpty(text[^1])) text.RemoveAt(text.Count - 1);
        if (text.Count == 0) text.Add("[dim]Нет мировых событий[/]");

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" 🌍 {_loc.T("world_news")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });

        // ═══ Active location threats across ALL known locations ═══
        var threatLines = new List<string>();
        var locDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
        var mapDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_map.json");

        void CollectThreats(JsonElement rawLoc)
        {
            var loc = GetCurrentLocationRoot(rawLoc);
            if (!loc.TryGetProperty("activeThreats", out var threats) ||
                threats.ValueKind != JsonValueKind.Array || threats.GetArrayLength() == 0) return;
            var locName = GetStr(loc, "name", "?");
            threatLines.Add($"  📍 [bold white]{Markup.Escape(locName)}[/]");
            foreach (var t in threats.EnumerateArray())
                RenderThreatFull(threatLines, t);
        }

        if (locDoc != null) CollectThreats(locDoc.RootElement);
        if (mapDoc != null)
        {
            var mapRoot = mapDoc.RootElement.TryGetProperty("worldMapUpdates", out var wm) && wm.ValueKind == JsonValueKind.Object
                ? wm
                : mapDoc.RootElement;
            EnumerateArray(mapRoot, "newLocations", CollectThreats);
            EnumerateArray(mapRoot, "locationUpdates", CollectThreats);
        }

        if (threatLines.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", threatLines)))
            {
                Header = new PanelHeader(" 🔥 Угрозы локаций ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(1, 0),
                Expand = true
            });
        }

        // ═══ NPC Activities ═══
        var actDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_activities.json");
        if (actDoc != null)
        {
            var npcActLines = new List<string>();
            EnumerateJsonItems(actDoc.RootElement, item => RenderNpcActivityNewsDetailed(npcActLines, item));

            if (npcActLines.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Markup(string.Join("\n", npcActLines)))
                {
                    Header = new PanelHeader(" 🏃 Активности НПС ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Cyan1),
                    Padding = new Padding(1, 0),
                    Expand = true
                });
            }
        }

        // ═══ Faction Projects (active) ═══
        var projDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_projects.json");
        if (projDoc != null)
        {
            var projLines = new List<string>();
            EnumerateJsonItems(projDoc.RootElement, item => RenderFactionProjectNewsDetailed(projLines, item));

            if (projLines.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Markup(string.Join("\n", projLines)))
                {
                    Header = new PanelHeader(" 🔨 Проекты фракций ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Orange1),
                    Padding = new Padding(1, 0),
                    Expand = true
                });
            }
        }

        // ═══ World flags ═══
        var flagDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_flags.json");
        if (flagDoc != null)
        {
            var flagText = new List<string>();
            EnumerateJsonItems(flagDoc.RootElement, item =>
            {
                // Block 21.5: flagId, displayName, value, description
                var displayName = GetStr(item, "displayName", GetStr(item, "flagName", GetStr(item, "name", "")));
                var flagId = GetStr(item, "flagId", GetStr(item, "id", ""));
                var desc = GetStr(item, "description", "");
                var value = GetStr(item, "value", "");
                if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(flagId)) return;
                var label = !string.IsNullOrEmpty(displayName) ? displayName : flagId;
                var line = $"  🏁 [white]{Markup.Escape(label)}[/]";
                if (!string.IsNullOrEmpty(value) && value != "true" && value != "True")
                    line += $": [yellow]{Markup.Escape(value)}[/]";
                if (!string.IsNullOrEmpty(desc))
                    line += $" — [dim]{Markup.Escape(desc)}[/]";
                flagText.Add(line);
            });
            if (flagText.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Markup(string.Join("\n", flagText)))
                {
                    Header = new PanelHeader(" 🏁 Флаги мира ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(1, 0),
                    Expand = true
                });
            }
        }

        // ═══ World progression ═══
        var progDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/progression.json");
        if (progDoc != null)
        {
            var progText = new List<string>();
            if (progDoc.RootElement.TryGetProperty("updateWorldProgressionTracker", out var worldTrackers))
            {
                if (worldTrackers.ValueKind == JsonValueKind.Array)
                    foreach (var item in worldTrackers.EnumerateArray())
                        RenderWorldProgressNewsDetailed(progText, item, "🌍 Мир");
                else if (worldTrackers.ValueKind == JsonValueKind.Object)
                    RenderWorldProgressNewsDetailed(progText, worldTrackers, "🌍 Мир");
            }
            if (progDoc.RootElement.TryGetProperty("updateFactionProgressionTracker", out var factionTrackers))
            {
                if (factionTrackers.ValueKind == JsonValueKind.Array)
                    foreach (var item in factionTrackers.EnumerateArray())
                        RenderWorldProgressNewsDetailed(progText, item, "🏛️ Фракции");
                else if (factionTrackers.ValueKind == JsonValueKind.Object)
                    RenderWorldProgressNewsDetailed(progText, factionTrackers, "🏛️ Фракции");
            }
            if (progText.Count == 0)
                EnumerateJsonItems(progDoc.RootElement, item => RenderWorldProgressNewsDetailed(progText, item, "📈"));
            if (progText.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Markup(string.Join("\n", progText)))
                {
                    Header = new PanelHeader(" 📈 Прогресс мира ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Cyan1),
                    Padding = new Padding(1, 0),
                    Expand = true
                });
            }
        }

        WaitForKey();
    }

    private async Task ShowCraftMenu()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/recipes.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("craft"), "Рецептов нет"); return; }

        var text = new List<string>();
        var recipeCount = 0;

        // Try both "recipes" array and "knownRecipes" array (Block 9)
        Action<JsonElement> renderRecipe = item =>
        {
            recipeCount++;
            var name = GetStr(item, "recipeName", GetStr(item, "name", "???"));
            var desc = GetStr(item, "description", "");
            var craftedItem = GetStr(item, "craftedItemName", "");
            var rank = GetStr(item, "recipeRank", "");
            var outputQty = GetStr(item, "outputQuantity", "1");
            var timeCost = GetStr(item, "timeCost", "");
            var diffMod = GetStr(item, "difficultyModifier", "");

            text.Add($"[bold orange3]📜 {Markup.Escape(name)}[/]" +
                (!string.IsNullOrEmpty(rank) ? $" [dim]({Markup.Escape(rank)})[/]" : ""));
            if (!string.IsNullOrEmpty(desc))
                text.Add($"  [dim]{Markup.Escape(desc)}[/]");
            if (!string.IsNullOrEmpty(craftedItem))
                text.Add($"  ➤ Результат: [white]{Markup.Escape(craftedItem)}[/]" +
                    (outputQty != "1" ? $" ×{Markup.Escape(outputQty)}" : ""));

            // Required knowledge skill
            if (item.TryGetProperty("requiredKnowledgeSkill", out var rks) && rks.ValueKind == JsonValueKind.Object)
            {
                var skillName = GetStr(rks, "skillName", "");
                var masteryLvl = GetStr(rks, "requiredMasteryLevel", "");
                if (!string.IsNullOrEmpty(skillName))
                    text.Add($"  📚 Навык: [cyan]{Markup.Escape(skillName)}[/] (уровень {Markup.Escape(masteryLvl)})");
            }

            // Required materials
            if (item.TryGetProperty("requiredMaterials", out var mats) && mats.ValueKind == JsonValueKind.Array)
            {
                text.Add("  🧱 Материалы:");
                foreach (var m in mats.EnumerateArray())
                {
                    var matName = GetStr(m, "materialName", "?");
                    var matQty = GetStr(m, "quantity", "1");
                    var matLine = $"    • [white]{Markup.Escape(matName)}[/] ×{Markup.Escape(matQty)}";
                    if (m.TryGetProperty("alternatives", out var alts) && alts.ValueKind == JsonValueKind.Array && alts.GetArrayLength() > 0)
                    {
                        var altNames = new List<string>();
                        foreach (var a in alts.EnumerateArray())
                            if (a.ValueKind == JsonValueKind.String) altNames.Add(a.GetString() ?? "");
                        if (altNames.Count > 0)
                            matLine += $" [dim](или: {Markup.Escape(string.Join(", ", altNames))})[/]";
                    }
                    text.Add(matLine);
                }
            }

            // Required tools
            if (item.TryGetProperty("requiredTools", out var tools) && tools.ValueKind == JsonValueKind.Object)
            {
                var toolParts = new List<string>();
                foreach (var category in new[] { "portable", "stationary" })
                {
                    if (tools.TryGetProperty(category, out var arr) && arr.ValueKind == JsonValueKind.Array)
                        foreach (var t in arr.EnumerateArray())
                            toolParts.Add(GetStr(t, "example", GetStr(t, "function", "?")));
                }
                if (toolParts.Count > 0)
                    text.Add($"  🔨 Инструменты: [white]{Markup.Escape(string.Join(", ", toolParts))}[/]");
                if (tools.TryGetProperty("optional", out var opt) && opt.ValueKind == JsonValueKind.Array)
                    foreach (var t in opt.EnumerateArray())
                    {
                        var bonus = GetStr(t, "bonus", "");
                        text.Add($"    [dim]+ {Markup.Escape(GetStr(t, "example", GetStr(t, "function", "?")))} (бонус: {Markup.Escape(bonus)})[/]");
                    }
            }

            // Time cost & difficulty
            var extras = new List<string>();
            if (!string.IsNullOrEmpty(timeCost)) extras.Add($"⏱ {Markup.Escape(timeCost)} мин");
            if (!string.IsNullOrEmpty(diffMod) && diffMod != "0") extras.Add($"⚙ Сложность: {Markup.Escape(diffMod)}");
            if (extras.Count > 0)
                text.Add($"  [dim]{string.Join("  |  ", extras)}[/]");

            text.Add("");
        };

        EnumerateArray(doc.RootElement, "recipes", renderRecipe);
        EnumerateArray(doc.RootElement, "knownRecipes", renderRecipe);
        // Fallback: if root is array
        if (recipeCount == 0 && doc.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var item in doc.RootElement.EnumerateArray()) renderRecipe(item);

        if (recipeCount == 0) { ShowEmptyPanel(_loc.T("craft"), "Рецептов нет"); return; }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" 📜 Рецепты ({recipeCount}) ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange3),
            Padding = new Padding(1, 1),
            Expand = true
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowLoreCodex()
    {
        // Collect all lore sources
        var categories = new List<(string label, string icon, string file, string[] descProps)>
        {
            ("Море Хаоса — Космология", "🌊", "lore/chaos_sea/cosmology.json", new[] { "description", "overview", "content" }),
            ("Система Душ", "✨", "lore/chaos_sea/soul_system_lore.json", new[] { "description", "overview", "content" }),
            ("Хранители", "🛡️", "lore/chaos_sea/guardians_lore.json", new[] { "description", "overview", "content" }),
            ("Артефакты и Реликвии", "💎", "lore/chaos_sea/artifacts_lore.json", new[] { "description", "overview", "content" }),
            ("Хроника Душ", "📜", "lore/chaos_sea/player_chronicle.json", new[] { "description", "overview", "content" }),
            ("Сияющая Обитель", "✨", "lore/shining_abode/realm_lore.json", new[] { "description", "overview", "content" }),
            ("Мир — Общее", "🌍", "lore/current_world/world_setting.json", new[] { "description", "overview", "content" }),
            ("Мир — Директивы", "📜", "lore/current_world/world_directives.json", new[] { "settingSummary", "description", "overview", "content" }),
            ("География", "🗺️", "lore/current_world/geography.json", new[] { "description", "overview", "content" }),
            ("История", "📖", "lore/current_world/history.json", new[] { "description", "overview", "content" }),
            ("Культуры и Народы", "🎭", "lore/current_world/cultures.json", new[] { "description", "overview", "content" }),
            ("NPC и локальные истории", "👥", "lore/current_world/npcs_lore.json", new[] { "description", "overview", "content" }),
            ("Угрозы", "⚠️", "lore/current_world/threats.json", new[] { "description", "overview", "content" }),
        };

        // Load all lore files and codex entries in parallel
        var loreDocs = new Dictionary<string, JsonDocument>();
        foreach (var cat in categories)
        {
            var doc = await _stateManager.LoadGameStateFileAsync(cat.file);
            if (doc != null) loreDocs[cat.file] = doc;
        }
        var codexDoc = await _stateManager.LoadGameStateFileAsync("lore/codex_entries.json");

        // Collect available items for selection
        var choices = new List<(string label, string key)>();
        foreach (var cat in categories)
        {
            if (loreDocs.ContainsKey(cat.file))
                choices.Add(($"{cat.icon} {cat.label}", cat.file));
        }

        // Add codex entries by category if available
        var codexEntries = codexDoc != null ? CollectCodexEntries(codexDoc.RootElement) : new List<JsonElement>();
        if (codexEntries.Count > 0)
        {
            choices.Add(("📚 Записи кодекса", "__codex__"));
        }

        if (choices.Count == 0)
        {
            ShowEmptyPanel(_loc.T("codex"), "Кодекс пуст — знания будут появляться по мере исследования мира");
            WaitForKey();
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            choices.RemoveAll(c => c.key == "__back__" || c.key == "__search__");
            choices.Add(("🔍 Поиск по кодексу", "__search__"));
            var selectItems = choices.Select(c => c.label).Append("← Назад").ToList();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold purple]📚 {_loc.T("codex")}[/] [dim]({choices.Count - 1} разделов)[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .PageSize(15)
                    .AddChoices(selectItems));

            if (choice == "← Назад") break;

            var selected = choices.FirstOrDefault(c => c.label == choice);
            if (selected.key == "__search__")
            {
                SearchLoreCodex(loreDocs, codexDoc, categories);
            }
            else if (selected.key == "__codex__")
            {
                await ShowCodexEntries(codexDoc!);
            }
            else if (selected.key != null && loreDocs.TryGetValue(selected.key, out var doc))
            {
                ShowLoreFileDetail(selected.label, doc);
            }
        }
    }

    private void SearchLoreCodex(
        Dictionary<string, JsonDocument> loreDocs,
        JsonDocument? codexDoc,
        List<(string label, string icon, string file, string[] descProps)> categories)
    {
        var query = AnsiConsole.Ask<string>("[purple]🔍 Поиск:[/]").Trim();
        if (string.IsNullOrEmpty(query)) return;

        var queryLower = query.ToLowerInvariant();
        var results = new List<string>();

        // Search lore files
        foreach (var cat in categories)
        {
            if (!loreDocs.TryGetValue(cat.file, out var doc)) continue;
            var raw = doc.RootElement.GetRawText();
            if (!raw.ToLowerInvariant().Contains(queryLower)) continue;

            // Find matching properties
            SearchJsonElement(doc.RootElement, queryLower, $"{cat.icon} {cat.label}", results, maxDepth: 4);
        }

        // Search codex entries
        if (codexDoc != null)
        {
            foreach (var entry in CollectCodexEntries(codexDoc.RootElement))
            {
                var title = GetStr(entry, "title", GetStr(entry, "name", ""));
                var content = GetStr(entry, "content", GetStr(entry, "description", ""));
                var cat = GetStr(entry, "category", "");
                if (title.ToLowerInvariant().Contains(queryLower) ||
                    content.ToLowerInvariant().Contains(queryLower))
                {
                    var snippet = content.Length > 100 ? content[..97] + "..." : content;
                    results.Add($"  📚 [white]{Markup.Escape(title)}[/] [dim]({Markup.Escape(cat)})[/]");
                    if (!string.IsNullOrEmpty(snippet))
                        results.Add($"    [dim]{Markup.Escape(snippet)}[/]");
                }
            }
        }

        if (results.Count == 0)
        {
            results.Add($"[dim]По запросу «{Markup.Escape(query)}» ничего не найдено[/]");
        }

        var panel = new Panel(new Markup(string.Join("\n", results)))
        {
            Header = new PanelHeader($" 🔍 Результаты: «{Markup.Escape(query)}» ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(1, 1),
            Expand = true
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private static void SearchJsonElement(JsonElement el, string queryLower, string path, List<string> results, int maxDepth, int depth = 0)
    {
        if (depth > maxDepth || results.Count >= 20) return;
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var str = el.GetString() ?? "";
                if (str.ToLowerInvariant().Contains(queryLower))
                {
                    var snippet = str.Length > 120 ? str[..117] + "..." : str;
                    results.Add($"  {path}: [dim]{Markup.Escape(snippet)}[/]");
                }
                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    SearchJsonElement(prop.Value, queryLower, $"{path} → {prop.Name}", results, maxDepth, depth + 1);
                break;
            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in el.EnumerateArray())
                {
                    var itemName = item.ValueKind == JsonValueKind.Object
                        ? GetStr(item, "name", GetStr(item, "title", $"[{idx}]"))
                        : $"[{idx}]";
                    SearchJsonElement(item, queryLower, $"{path} → {itemName}", results, maxDepth, depth + 1);
                    idx++;
                }
                break;
        }
    }

    private void ShowLoreFileDetail(string title, JsonDocument doc)
    {
        AnsiConsole.Clear();
        var text = new List<string>();
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            RenderLoreElement(root, text, depth: 0);
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                RenderLoreElement(item, text, depth: 0);
        }

        if (text.Count == 0) text.Add("[dim italic]Файл пуст[/]");

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {title} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    /// <summary>Recursively renders a JSON element into styled text lines for lore display.</summary>
    private void RenderLoreElement(JsonElement el, List<string> lines, int depth)
    {
        var indent = new string(' ', depth * 2);
        var sectionColors = new[] { "mediumpurple1", "steelblue1_1", "darkseagreen", "lightsalmon1", "plum1", "lightskyblue1" };
        var sectionColor = sectionColors[Math.Min(depth, sectionColors.Length - 1)];

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var prettyName = FormatLorePropertyName(prop.Name);

                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        var val = prop.Value.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(val)) break;
                        if (depth == 0)
                        {
                            lines.Add($"[bold {sectionColor}]{Markup.Escape(prettyName)}[/]");
                            lines.Add($"{indent}  [white]{Markup.Escape(val)}[/]");
                        }
                        else
                        {
                            lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [white]{Markup.Escape(val)}[/]");
                        }
                        lines.Add("");
                        break;

                    case JsonValueKind.Number:
                        lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [yellow]{prop.Value}[/]");
                        lines.Add("");
                        break;

                    case JsonValueKind.True or JsonValueKind.False:
                        var boolVal = prop.Value.GetBoolean() ? "да" : "нет";
                        lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [yellow]{boolVal}[/]");
                        lines.Add("");
                        break;

                    case JsonValueKind.Array:
                        lines.Add($"{indent}[bold {sectionColor}]{Markup.Escape(prettyName)}:[/]");
                        RenderLoreArray(prop.Value, lines, depth + 1, sectionColor);
                        lines.Add("");
                        break;

                    case JsonValueKind.Object:
                        // Section header with decorative line
                        if (depth == 0)
                        {
                            lines.Add($"[bold {sectionColor}]━━━ {Markup.Escape(prettyName)} ━━━[/]");
                        }
                        else
                        {
                            lines.Add($"{indent}[bold {sectionColor}]▸ {Markup.Escape(prettyName)}[/]");
                        }
                        RenderLoreElement(prop.Value, lines, depth + 1);
                        break;
                }
            }
        }
        else if (el.ValueKind == JsonValueKind.String)
        {
            var sv = el.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(sv))
                lines.Add($"{indent}[white]{Markup.Escape(sv)}[/]");
        }
    }

    /// <summary>Renders a JSON array into styled text lines for lore display.</summary>
    private void RenderLoreArray(JsonElement arr, List<string> lines, int depth, string parentColor)
    {
        var indent = new string(' ', depth * 2);
        int idx = 0;
        foreach (var item in arr.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    lines.Add($"{indent}[dim]•[/] [white]{Markup.Escape(item.GetString() ?? "")}[/]");
                    break;

                case JsonValueKind.Object:
                    // Try to find a "name"/"title" field to use as sub-header
                    var itemName = GetStr(item, "name", GetStr(item, "title", ""));
                    var itemDesc = GetStr(item, "description", GetStr(item, "content", GetStr(item, "overview", "")));

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        lines.Add($"{indent}[bold white]◆ {Markup.Escape(itemName)}[/]");
                        if (!string.IsNullOrEmpty(itemDesc))
                            lines.Add($"{indent}  [white]{Markup.Escape(itemDesc)}[/]");

                        // Render remaining properties (skip name/title/description/content/overview)
                        var skipProps = new HashSet<string> { "name", "title", "description", "content", "overview" };
                        foreach (var prop in item.EnumerateObject())
                        {
                            if (skipProps.Contains(prop.Name)) continue;
                            var pName = FormatLorePropertyName(prop.Name);
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var pv = prop.Value.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(pv))
                                    lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/] [white]{Markup.Escape(pv)}[/]");
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/]");
                                RenderLoreArray(prop.Value, lines, depth + 2, parentColor);
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/]");
                                RenderLoreElement(prop.Value, lines, depth + 2);
                            }
                            else
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/] [yellow]{Markup.Escape(prop.Value.ToString())}[/]");
                            }
                        }
                    }
                    else
                    {
                        // No name/title — render all props inline
                        RenderLoreElement(item, lines, depth + 1);
                    }
                    if (idx < arr.GetArrayLength() - 1)
                        lines.Add("");
                    break;

                default:
                    lines.Add($"{indent}[dim]•[/] [yellow]{Markup.Escape(item.ToString())}[/]");
                    break;
            }
            idx++;
        }
    }

    /// <summary>Converts camelCase/snake_case JSON property names into readable Russian-friendly labels.</summary>
    private static string FormatLorePropertyName(string name)
    {
        // Known translations for common lore property names
        var knownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Название",
            ["title"] = "Заголовок",
            ["description"] = "Описание",
            ["overview"] = "Обзор",
            ["content"] = "Содержание",
            ["summary"] = "Сводка",
            ["type"] = "Тип",
            ["genre"] = "Жанр",
            ["tone"] = "Тон",
            ["structure"] = "Структура",
            ["nature"] = "Природа",
            ["cosmology"] = "Космология",
            ["chaosSea"] = "Море Хаоса",
            ["chaos_sea"] = "Море Хаоса",
            ["mortalWorlds"] = "Смертные Миры",
            ["mortal_worlds"] = "Смертные Миры",
            ["radiantAbode"] = "Сияющая Обитель",
            ["radiant_abode"] = "Сияющая Обитель",
            ["soulMechanics"] = "Механика Душ",
            ["soul_mechanics"] = "Механика Душ",
            ["reincarnationCycle"] = "Цикл Реинкарнации",
            ["reincarnation_cycle"] = "Цикл Реинкарнации",
            ["enlightenment"] = "Просветление",
            ["soulRelics"] = "Реликвии Души",
            ["soul_relics"] = "Реликвии Души",
            ["inkFeathers"] = "Чернильные Перья",
            ["ink_feathers"] = "Чернильные Перья",
            ["guardians"] = "Хранители",
            ["artifacts"] = "Артефакты",
            ["history"] = "История",
            ["geography"] = "География",
            ["cultures"] = "Культуры",
            ["threats"] = "Угрозы",
            ["factions"] = "Фракции",
            ["magic"] = "Магия",
            ["creatures"] = "Существа",
            ["characters"] = "Персонажи",
            ["tags"] = "Метки",
            ["category"] = "Категория",
            ["subcategory"] = "Подкатегория",
            ["sourceFile"] = "Источник",
            ["source_file"] = "Источник",
            ["discoveredAt"] = "Обнаружено",
            ["discovered_at"] = "Обнаружено",
            ["discoveryContext"] = "Контекст открытия",
            ["discovery_context"] = "Контекст открытия",
            ["incarnation"] = "Инкарнация",
            ["entries"] = "Записи",
            ["totalEntries"] = "Всего записей",
            ["categories"] = "Категории",
        };

        if (knownNames.TryGetValue(name, out var translated))
            return translated;

        // Convert camelCase / snake_case to readable form
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                sb.Append(' ');
            }
            else if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
                sb.Append(c);
            }
            else
            {
                sb.Append(i == 0 ? char.ToUpper(c) : c);
            }
        }
        return sb.ToString();
    }

    private async Task ShowCodexEntries(JsonDocument codexDoc)
    {
        var entriesList = CollectCodexEntries(codexDoc.RootElement);
        var codexTitlesById = entriesList
            .Select(entry => (entryId: GetStr(entry, "entryId", ""), title: GetStr(entry, "title", "")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.entryId) && !string.IsNullOrWhiteSpace(pair.title))
            .GroupBy(pair => pair.entryId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().title, StringComparer.OrdinalIgnoreCase);

        if (entriesList.Count == 0)
        {
            ShowEmptyPanel("📚 Записи кодекса", "Пока нет записей");
            WaitForKey();
            return;
        }

        // Group by category
        var categoryIcons = new Dictionary<string, string>
        {
            ["cosmology"] = "🌊", ["geography"] = "🗺️", ["history"] = "📖",
            ["cultures"] = "🎭", ["creatures"] = "🐉", ["characters"] = "👤",
            ["artifacts"] = "💎", ["factions"] = "⚔️", ["magic"] = "🔮", ["other"] = "📝"
        };
        var categoryNames = new Dictionary<string, string>
        {
            ["cosmology"] = "Космология", ["geography"] = "География", ["history"] = "История",
            ["cultures"] = "Культуры", ["creatures"] = "Существа", ["characters"] = "Персонажи",
            ["artifacts"] = "Артефакты", ["factions"] = "Фракции", ["magic"] = "Магия", ["other"] = "Прочее"
        };

        var grouped = entriesList
            .GroupBy(e => GetStr(e, "category", "other"))
            .OrderBy(g => g.Key)
            .ToList();

        while (true)
        {
            AnsiConsole.Clear();
            var items = new List<(string label, int idx)>();
            foreach (var g in grouped)
            {
                var icon = categoryIcons.GetValueOrDefault(g.Key, "📝");
                var catName = categoryNames.GetValueOrDefault(g.Key, g.Key);
                foreach (var e in g)
                {
                    var idx = entriesList.IndexOf(e);
                    var title = GetStr(e, "title", "Без названия");
                    items.Add(($"{icon} [dim]{catName}[/]  {title}", idx));
                }
            }

            var selectList = items.Select(i => i.label).Append("← Назад").ToList();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold purple]📚 Записи кодекса[/] [dim]({entriesList.Count} записей)[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .PageSize(15)
                    .AddChoices(selectList));

            if (choice == "← Назад") break;

            var sel = items.FirstOrDefault(i => i.label == choice);
            if (sel.idx >= 0 && sel.idx < entriesList.Count)
            {
                AnsiConsole.Clear();
                var entry = entriesList[sel.idx];
                var title = GetStr(entry, "title", "Без названия");
                var content = GetStr(entry, "content", "");
                var category = GetStr(entry, "category", "other");
                var subcategory = GetStr(entry, "subcategory", "");
                var context = GetStr(entry, "discoveryContext", "");
                var discoveredAt = GetStr(entry, "discoveredAt", "");
                var incarnation = GetInt(entry, "incarnation", -1);
                var sourceFile = GetStr(entry, "sourceFile", "");
                var icon = categoryIcons.GetValueOrDefault(category, "📝");
                var catName = categoryNames.GetValueOrDefault(category, category);

                var text = new List<string>();

                // Title
                text.Add($"[bold white]{Markup.Escape(title)}[/]");
                text.Add("");

                // Category / Subcategory line
                var catLine = $"[dim]Категория:[/] [{(category == "cosmology" ? "mediumpurple1" : "steelblue1_1")}]{Markup.Escape(catName)}[/]";
                if (!string.IsNullOrEmpty(subcategory))
                    catLine += $" [dim]>[/] [steelblue1_1]{Markup.Escape(subcategory)}[/]";
                text.Add(catLine);
                text.Add("");

                // Main content with separator
                if (!string.IsNullOrEmpty(content))
                {
                    text.Add("[dim]────────────────────────────────[/]");
                    text.Add("");
                    text.Add($"[white]{Markup.Escape(content)}[/]");
                    text.Add("");
                }

                // Metadata footer
                var metaLines = new List<string>();
                if (!string.IsNullOrEmpty(context))
                    metaLines.Add($"[dim italic]📍 {Markup.Escape(context)}[/]");
                if (!string.IsNullOrEmpty(discoveredAt))
                {
                    if (DateTime.TryParse(discoveredAt, out var dt))
                        metaLines.Add($"[dim italic]🕐 Обнаружено: {dt:dd.MM.yyyy HH:mm}[/]");
                    else
                        metaLines.Add($"[dim italic]🕐 {Markup.Escape(discoveredAt)}[/]");
                }
                if (incarnation >= 0)
                    metaLines.Add($"[dim italic]🔄 Инкарнация: {incarnation}[/]");
                if (!string.IsNullOrWhiteSpace(sourceFile))
                    metaLines.Add($"[dim italic]📂 Источник: {Markup.Escape(sourceFile)}[/]");

                // Tags
                if (entry.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    var tagStrs = new List<string>();
                    foreach (var t in tags.EnumerateArray())
                    {
                        if (t.ValueKind == JsonValueKind.String)
                            tagStrs.Add($"[grey on grey11] {Markup.Escape(t.GetString() ?? "")} [/]");
                    }
                    if (tagStrs.Count > 0)
                        metaLines.Add(string.Join(" ", tagStrs));
                }

                if (entry.TryGetProperty("relatedEntries", out var relatedEntries) && relatedEntries.ValueKind == JsonValueKind.Array)
                {
                    var links = relatedEntries.EnumerateArray()
                        .Where(link => link.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(link.GetString()))
                        .Select(link =>
                        {
                            var linkId = link.GetString() ?? "";
                            if (codexTitlesById.TryGetValue(linkId, out var relatedTitle) && !string.IsNullOrWhiteSpace(relatedTitle))
                                return $"{Markup.Escape(relatedTitle)} [dim]({Markup.Escape(linkId)})[/]";

                            return Markup.Escape(linkId);
                        })
                        .ToList();
                    if (links.Count > 0)
                        metaLines.Add($"[dim italic]🔗 Связанные записи: {string.Join(", ", links)}[/]");
                }

                if (metaLines.Count > 0)
                {
                    text.Add("[dim]────────────────────────────────[/]");
                    text.AddRange(metaLines);
                }

                var panel = new Panel(new Markup(string.Join("\n", text)))
                {
                    Header = new PanelHeader($" {icon} {Markup.Escape(catName)} ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Purple),
                    Padding = new Padding(2, 1)
                };
                AnsiConsole.Write(panel);
                WaitForKey();
            }
        }
    }

    private async Task ShowLocations()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_map.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("locations"), "Локации не обнаружены"); return; }

        var root = doc.RootElement.TryGetProperty("worldMapUpdates", out var wrappedRoot) &&
                   wrappedRoot.ValueKind == JsonValueKind.Object
            ? wrappedRoot
            : doc.RootElement;

        var text = new List<string>();
        void RenderLocationItem(JsonElement item)
        {
            var name = GetStr(item, "name", "???");
            var type = GetStr(item, "locationType", "");
            var indoorType = GetStr(item, "indoorType", "");
            var shortDesc = GetStr(item, "shortDescription", GetStr(item, "description", ""));
            var factionName = "";
            if (item.TryGetProperty("factionControl", out var fc))
            {
                if (fc.ValueKind == JsonValueKind.Object)
                    factionName = GetStr(fc, "factionName", "");
                else if (fc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fce in fc.EnumerateArray())
                    {
                        factionName = GetStr(fce, "factionName", GetStr(fce, "factionId", ""));
                        if (!string.IsNullOrEmpty(factionName)) break;
                    }
                }
            }

            var line = $"  📍 [white]{Markup.Escape(name)}[/]";
            var tags = new List<string>();
            if (!string.IsNullOrEmpty(type)) tags.Add(Markup.Escape(type));
            if (!string.IsNullOrEmpty(indoorType))
            {
                var indoorLabel = indoorType.ToLower() switch
                {
                    "building" => "Здание",
                    "dungeon" => "Подземелье",
                    "cavesystem" => "Пещера",
                    "vehicle" => "Транспорт",
                    "uniqueindoor" => "Уникальное",
                    _ => Markup.Escape(indoorType)
                };
                tags.Add(indoorLabel);
            }
            if (tags.Count > 0) line += $" [dim]({string.Join(", ", tags)})[/]";
            if (!string.IsNullOrEmpty(factionName)) line += $" [yellow]⚑ {Markup.Escape(factionName)}[/]";
            text.Add(line);

            if (!string.IsNullOrEmpty(shortDesc))
                text.Add($"    [dim]{Markup.Escape(Truncate(shortDesc, 80))}[/]");
        }

        EnumerateArray(root, "newLocations", RenderLocationItem);
        EnumerateArray(root, "locationUpdates", RenderLocationItem);

        if (text.Count == 0) text.Add("[dim]Нет известных локаций[/]");

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("locations")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowTransport()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/misc/vehicles.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("transport"), "Транспорта нет"); return; }

        var vehicles = new List<(string name, string type, bool active, JsonElement el)>();
        EnumerateArray(doc.RootElement, "vehicles", item =>
        {
            var name = GetStr(item, "name", "???");
            var vtype = GetStr(item, "type", "");
            var active = item.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True;
            vehicles.Add((name, vtype, active, item));
        });
        // Also try root-level array
        if (vehicles.Count == 0)
            EnumerateJsonItems(doc.RootElement, item =>
            {
                vehicles.Add((GetStr(item, "name", "???"), GetStr(item, "type", ""), false, item));
            });

        if (vehicles.Count == 0)
        {
            ShowEmptyPanel(_loc.T("transport"), "Транспорта нет");
            return;
        }

        while (true)
        {
            var choices = vehicles.Select(v =>
            {
                var label = v.active ? $"[green]✓[/] {Markup.Escape(v.name)}" : $"  {Markup.Escape(v.name)}";
                if (!string.IsNullOrEmpty(v.type)) label += $" [dim]({Markup.Escape(v.type)})[/]";
                return label;
            }).ToList();
            choices.Add("[dim]← Назад[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]🚗 Транспорт[/]")
                    .PageSize(10)
                    .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var idx = choices.IndexOf(selected);
            if (idx < 0 || idx >= vehicles.Count) break;

            var v = vehicles[idx];
            var lines = new List<string>();
            lines.Add($"[bold]{Markup.Escape(v.name)}[/]");
            if (!string.IsNullOrEmpty(v.type))
            {
                var typeLabel = v.type.ToLower() switch
                {
                    "mount" => "🐴 Ездовое животное",
                    "vehicle" => "🚗 Транспорт",
                    "summonable" => "✨ Призываемый",
                    _ => Markup.Escape(v.type)
                };
                lines.Add($"  Тип: {typeLabel}");
            }
            // Availability (translated)
            var availability = GetStr(v.el, "availability", "");
            var availLabel = availability.ToLower() switch
            {
                "active" => "[green]Активен (оседлан/управляется)[/]",
                "parked" => "[yellow]Припаркован[/]",
                "pocket" => "[cyan]В кармане (призываемый)[/]",
                _ when v.active => "[green]Активен[/]",
                _ when !string.IsNullOrEmpty(availability) => Markup.Escape(availability),
                _ => "[dim]Неактивен[/]"
            };
            lines.Add($"  Статус: {availLabel}");

            var isSentient = v.el.TryGetProperty("isSentient", out var sent) && sent.ValueKind == JsonValueKind.True;
            if (isSentient)
                lines.Add($"  🧠 [mediumpurple2]Разумный[/] [dim](действует самостоятельно в бою)[/]");
            else if (v.el.TryGetProperty("isSentient", out var sentF) && sentF.ValueKind == JsonValueKind.False)
                lines.Add($"  ⚙ [dim]Неразумный (требует действия игрока для управления в бою)[/]");

            var desc = GetStr(v.el, "description", "");
            if (!string.IsNullOrEmpty(desc))
            {
                lines.Add("");
                lines.Add($"  [white]{Markup.Escape(desc)}[/]");
            }

            // Health with visual bar
            var health = GetStr(v.el, "currentHealth", GetStr(v.el, "health", ""));
            var maxHealth = GetStr(v.el, "maxHealth", "");
            if (!string.IsNullOrEmpty(health))
            {
                var hpNum = int.TryParse(health.Replace("%", "").Trim(), out var hv) ? hv : 0;
                var maxHpNum = int.TryParse(maxHealth.Replace("%", "").Trim(), out var mv) ? mv : hpNum;
                var hpPct = maxHpNum > 0 ? Math.Clamp(hpNum * 100 / maxHpNum, 0, 100) : 100;
                var hpColor = hpPct > 60 ? "green" : hpPct > 30 ? "yellow" : "red";
                var barW = 15;
                var filled = Math.Clamp(hpPct * barW / 100, 0, barW);
                var hpBar = $"[{hpColor}]{new string('━', filled)}[/][dim grey]{new string('┄', barW - filled)}[/]";
                var hpLabel = !string.IsNullOrEmpty(maxHealth) ? $"{Markup.Escape(health)}/{Markup.Escape(maxHealth)}" : Markup.Escape(health);
                lines.Add($"  ❤️ Здоровье: {hpBar}  [{hpColor}]{hpLabel}[/]");
            }

            var speed = GetStr(v.el, "speed", "");
            if (!string.IsNullOrEmpty(speed))
                lines.Add($"  💨 Скорость: [cyan]{Markup.Escape(speed)}[/]");

            var speedBonus = GetStr(v.el, "speedBonus", "");
            if (!string.IsNullOrEmpty(speedBonus) && speedBonus != "0")
                lines.Add($"  💨 Бонус скорости: [cyan]+{Markup.Escape(speedBonus)}[/] [dim](к инициативе игрока)[/]");

            var cap = GetStr(v.el, "capacity", "");
            if (!string.IsNullOrEmpty(cap))
                lines.Add($"  📦 Вместимость: [white]{Markup.Escape(cap)}[/]");

            var curLoc = GetStr(v.el, "currentLocationId", GetStr(v.el, "currentLocation", ""));
            if (!string.IsNullOrEmpty(curLoc))
                lines.Add($"  📍 Местоположение: [white]{Markup.Escape(curLoc)}[/]");

            // Resistances
            if (v.el.TryGetProperty("resistances", out var vRes) && vRes.ValueKind == JsonValueKind.Array && vRes.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add("  [bold]🛡️ Сопротивления:[/]");
                foreach (var r in vRes.EnumerateArray())
                {
                    var rName = GetStr(r, "resistTypeDisplayName", GetStr(r, "resistanceName", GetStr(r, "resistType", "?")));
                    var rVal = GetStr(r, "resistanceValue", GetStr(r, "value", GetStr(r, "percentage", "")));
                    lines.Add($"    • {Markup.Escape(rName)}: [white]{Markup.Escape(rVal)}[/]");
                }
            }

            // Actions / combat abilities
            if (v.el.TryGetProperty("actions", out var vAct) && vAct.ValueKind == JsonValueKind.Array && vAct.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add("  [bold]⚔️ Действия:[/]");
                foreach (var a in vAct.EnumerateArray())
                {
                    var aName = GetStr(a, "actionName", GetStr(a, "name", "?"));
                    var aCost = GetStr(a, "actionCost", "");
                    var aCostLabel = aCost.ToLower() switch
                    {
                        "main" => "[yellow]осн.[/]",
                        "fast" => "[cyan]быстр.[/]",
                        "free" => "[green]своб.[/]",
                        _ when !string.IsNullOrEmpty(aCost) => $"[dim]{Markup.Escape(aCost)}[/]",
                        _ => ""
                    };
                    var aLine = $"    • [white]{Markup.Escape(aName)}[/]";
                    if (!string.IsNullOrEmpty(aCostLabel)) aLine += $" {aCostLabel}";

                    // Parse effects array for damage/type info
                    if (a.TryGetProperty("effects", out var effs) && effs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var eff in effs.EnumerateArray())
                        {
                            var eType = GetStr(eff, "effectType", "");
                            var eVal = GetStr(eff, "value", "");
                            var eTarget = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                            var ePoise = GetStr(eff, "poiseDamage", "");
                            var eDur = GetStr(eff, "duration", "");
                            var eDesc = GetStr(eff, "effectDescription", "");

                            if (!string.IsNullOrEmpty(eVal))
                            {
                                var eColor = eType.ToLower().Contains("heal") ? "green" : "red";
                                aLine += $" [{eColor}]{Markup.Escape(eVal)}[/]";
                            }
                            if (!string.IsNullOrEmpty(eTarget)) aLine += $" [dim]({Markup.Escape(eTarget)})[/]";
                            if (!string.IsNullOrEmpty(ePoise) && ePoise != "0%") aLine += $" 🛡️[yellow]{Markup.Escape(ePoise)}[/]";
                            if (!string.IsNullOrEmpty(eDur) && eDur != "0") aLine += $" [dim]{Markup.Escape(eDur)} ход.[/]";
                            if (!string.IsNullOrEmpty(eDesc)) aLine += $" [dim]— {Markup.Escape(eDesc)}[/]";
                        }
                    }
                    else
                    {
                        // Fallback: simple damage/description fields
                        var aDmg = GetStr(a, "damage", "");
                        var aDesc = GetStr(a, "description", "");
                        if (!string.IsNullOrEmpty(aDmg)) aLine += $" [red]{Markup.Escape(aDmg)}[/]";
                        if (!string.IsNullOrEmpty(aDesc)) aLine += $" [dim]— {Markup.Escape(aDesc)}[/]";
                    }
                    lines.Add(aLine);
                }
            }

            // Special abilities
            if (v.el.TryGetProperty("specialAbilities", out var sa) && sa.ValueKind == JsonValueKind.Array)
            {
                lines.Add(""); lines.Add("  [bold]✨ Способности:[/]");
                foreach (var a in sa.EnumerateArray())
                {
                    if (a.ValueKind == JsonValueKind.String)
                        lines.Add($"    • {Markup.Escape(a.GetString() ?? "")}");
                    else
                        lines.Add($"    • {Markup.Escape(GetStr(a, "name", a.GetRawText()))}");
                }
            }

            // Inventory
            if (v.el.TryGetProperty("inventory", out var vInv) && vInv.ValueKind == JsonValueKind.Array && vInv.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add($"  [bold]🎒 Содержимое ({vInv.GetArrayLength()}):[/]");
                foreach (var item in vInv.EnumerateArray())
                {
                    var iName = GetStr(item, "name", "?");
                    var iQty = GetStr(item, "quantity", "");
                    var iLine = $"    • {Markup.Escape(iName)}";
                    if (!string.IsNullOrEmpty(iQty) && iQty != "1") iLine += $" ×{Markup.Escape(iQty)}";
                    lines.Add(iLine);
                }
            }

            // Catch-all for other properties
            var known = new HashSet<string> { "name", "type", "isActive", "description", "speed", "speedBonus",
                "capacity", "currentHealth", "maxHealth", "health", "specialAbilities", "id", "image_prompt",
                "availability", "isSentient", "currentLocationId", "currentLocation", "resistances",
                "actions", "inventory", "vehicleId", "actionName" };
            foreach (var prop in v.el.EnumerateObject())
            {
                if (known.Contains(prop.Name)) continue;
                var pVal = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? ""
                    : (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) ? "" : prop.Value.GetRawText();
                if (pVal.Length > 0 && pVal.Length < 200)
                    lines.Add($"  [dim]{Markup.Escape(prop.Name)}: {Markup.Escape(pVal)}[/]");
            }

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🚗 Транспорт ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var vehicleId = GetStr(v.el, "vehicleId", GetStr(v.el, "id", ""));
            var hasInventory = v.el.TryGetProperty("inventory", out var vehicleInventory) &&
                               vehicleInventory.ValueKind == JsonValueKind.Array;

            if (hasInventory)
            {
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold]Действие с транспортом:[/]")
                        .HighlightStyle(new Style(Color.Yellow))
                        .AddChoices("🎒 Управлять инвентарём транспорта", "← Назад"));

                if (action.Contains("инвентарём"))
                {
                    var modified = await ShowVehicleInventoryInteractivePanel(v.name, vehicleId);
                    if (modified)
                    {
                        await _stateManager.RefreshGameStateAsync();
                        await ShowTransport();
                        return;
                    }
                    continue;
                }
            }

            await WaitForKeyWithImage("vehicle", v.name, GetStr(v.el, "image_prompt", ""), vehicleId);
        }
    }

    private async Task ShowAchievements()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/achievements.json");
        var rarityColors = new Dictionary<string, string>
        {
            ["common"] = "white", ["uncommon"] = "green", ["rare"] = "blue",
            ["epic"] = "purple", ["legendary"] = "yellow"
        };
        var rarityLabels = new Dictionary<string, string>
        {
            ["common"] = "Обычное", ["uncommon"] = "Необычное", ["rare"] = "Редкое",
            ["epic"] = "Эпическое", ["legendary"] = "Легендарное"
        };
        var categoryLabels = new Dictionary<string, string>
        {
            ["combat"] = "⚔️ Бой", ["exploration"] = "🗺️ Исследование", ["story"] = "📖 Сюжет",
            ["social"] = "🤝 Социальное", ["crafting"] = "🔨 Крафт", ["meta"] = "🌌 Мета",
            ["death"] = "💀 Смерть", ["secret"] = "❓ Секрет"
        };

        var unlocked = new List<(string id, string name, string desc, string category, string rarity, string icon, string date, int incarnation, bool hidden, string rewardType, string rewardValue)>();
	        var tracked = new List<(string id, string name, string desc, string category, string rarity, string icon, int current, int target, bool hidden, string rewardType, string rewardValue)>();
        var statsSummary = new List<string>();

        if (doc != null)
        {
            if (doc.RootElement.TryGetProperty("unlockedAchievements", out var uArr) &&
                uArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in uArr.EnumerateArray())
                {
                    unlocked.Add((
                        GetStr(a, "achievementId", ""),
                        GetStr(a, "name", "???"),
                        GetStr(a, "description", ""),
                        GetStr(a, "category", "other"),
                        GetStr(a, "rarity", "common"),
                        GetStr(a, "icon", "🏆"),
                        GetStr(a, "unlockedAt", ""),
                        GetInt(a, "incarnation", -1),
                        a.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True,
                        a.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "type", "")
                            : "",
                        a.TryGetProperty("reward", out reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "value", "")
                            : ""
                    ));
                }
            }

            if (doc.RootElement.TryGetProperty("trackedProgress", out var tArr) &&
                tArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tArr.EnumerateArray())
                {
                    int cur = 0, tar = 1;
                    if (t.TryGetProperty("progress", out var prog))
                    {
                        if (prog.TryGetProperty("current", out var cv)) cur = cv.TryGetInt32(out var ci) ? ci : 0;
                        if (prog.TryGetProperty("target", out var tv)) tar = tv.TryGetInt32(out var ti) ? ti : 1;
                    }
	                    tracked.Add((
	                        GetStr(t, "achievementId", ""),
	                        GetStr(t, "name", "???"),
	                        GetStr(t, "description", ""),
	                        GetStr(t, "category", "other"),
	                        GetStr(t, "rarity", "common"),
	                        GetStr(t, "icon", "📊"),
	                        cur, tar,
	                        t.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True,
	                        t.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object
	                            ? GetStr(reward, "type", "")
	                            : "",
                        t.TryGetProperty("reward", out reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "value", "")
                            : ""
                    ));
                }
            }

            if (doc.RootElement.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                var totalUnlocked = GetInt(stats, "totalUnlocked", unlocked.Count);
                statsSummary.Add($"[bold]Всего открыто:[/] [yellow]{totalUnlocked}[/]");

                if (stats.TryGetProperty("byCategory", out var byCategory) && byCategory.ValueKind == JsonValueKind.Object)
                {
                    var categoryParts = byCategory.EnumerateObject()
                        .Where(prop => prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var value) && value > 0)
                        .Select(prop => $"{Markup.Escape(categoryLabels.GetValueOrDefault(prop.Name, prop.Name))}: {prop.Value}")
                        .ToList();
                    if (categoryParts.Count > 0)
                        statsSummary.Add($"[dim]По категориям: {string.Join(", ", categoryParts)}[/]");
                }

                if (stats.TryGetProperty("byRarity", out var byRarity) && byRarity.ValueKind == JsonValueKind.Object)
                {
                    var rarityParts = byRarity.EnumerateObject()
                        .Where(prop => prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var value) && value > 0)
                        .Select(prop => $"{Markup.Escape(rarityLabels.GetValueOrDefault(prop.Name, prop.Name))}: {prop.Value}")
                        .ToList();
                    if (rarityParts.Count > 0)
                        statsSummary.Add($"[dim]По редкости: {string.Join(", ", rarityParts)}[/]");
                }
            }
        }

        if (unlocked.Count == 0 && tracked.Count == 0)
        {
            ShowEmptyPanel(_loc.T("achievements"), "Пока нет достижений — совершайте подвиги!");
            WaitForKey();
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            var items = new List<string>();

            if (statsSummary.Count > 0)
            {
                AnsiConsole.Write(new Panel(new Markup(string.Join("\n", statsSummary)))
                {
                    Header = new PanelHeader(" 📊 Сводка достижений ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Gold1),
                    Padding = new Padding(1, 0)
                });
                AnsiConsole.WriteLine();
            }

            // Group by category
	            var visibleTracked = tracked.Where(t => !t.hidden).ToList();
	            var allCategories = unlocked.Select(a => a.category)
	                .Concat(visibleTracked.Select(t => t.category))
	                .Distinct()
	                .OrderBy(c => c)
	                .ToList();

            foreach (var cat in allCategories)
            {
                var catLabel = categoryLabels.GetValueOrDefault(cat, cat);
                items.Add($"[dim]── {catLabel} ──[/]");

                foreach (var a in unlocked.Where(a => a.category == cat))
                {
                    var color = rarityColors.GetValueOrDefault(a.rarity, "white");
                    items.Add($"{a.icon} [{color}]{Markup.Escape(a.name)}[/] [dim]({rarityLabels.GetValueOrDefault(a.rarity, a.rarity)})[/]");
                }
	                foreach (var t in visibleTracked.Where(t => t.category == cat))
	                {
	                    var pct = t.target > 0 ? (int)(100.0 * t.current / t.target) : 0;
	                    var color = rarityColors.GetValueOrDefault(t.rarity, "white");
	                    items.Add($"{t.icon} [{color}]{Markup.Escape(t.name)}[/] [dim]({rarityLabels.GetValueOrDefault(t.rarity, t.rarity)}, {pct}%)[/]");
	                }
            }

            items.Add("← Назад");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
	                    .Title($"[bold yellow]🏆 {_loc.T("achievements")}[/] [dim]({unlocked.Count} открыто, {visibleTracked.Count} в процессе)[/]")
                    .HighlightStyle(new Style(Color.Yellow))
                    .PageSize(15)
                    .AddChoices(items));

            if (choice == "← Назад") break;

            // Skip category separator lines
            if (choice.Contains("──")) continue;

            // Find which achievement was selected by matching the choice text
            var uMatch = unlocked.FirstOrDefault(a =>
                choice.Contains(Markup.Escape(a.name)));
            if (uMatch.id != null)
            {
                var color = rarityColors.GetValueOrDefault(uMatch.rarity, "white");
                var cat = categoryLabels.GetValueOrDefault(uMatch.category, uMatch.category);
                var text = new List<string>
                {
                    $"[bold {color}]{uMatch.icon} {Markup.Escape(uMatch.name)}[/]",
                    $"[dim]{rarityLabels.GetValueOrDefault(uMatch.rarity, uMatch.rarity)} • {cat}[/]",
                    "",
                    Markup.Escape(uMatch.desc)
                };
                if (!string.IsNullOrEmpty(uMatch.date))
                {
                    text.Add("");
                    text.Add($"[dim]Получено: {Markup.Escape(uMatch.date)}[/]");
                }
                if (uMatch.incarnation >= 0)
                    text.Add($"[dim]Инкарнация: {uMatch.incarnation}[/]");
                if (uMatch.hidden)
                    text.Add("[dim]Это достижение было скрытым до разблокировки.[/]");
                if (!string.IsNullOrWhiteSpace(uMatch.rewardType) || !string.IsNullOrWhiteSpace(uMatch.rewardValue))
                {
                    text.Add("");
                    text.Add($"[yellow]Награда:[/] {Markup.Escape(FormatAchievementRewardText(uMatch.rewardType, uMatch.rewardValue))}");
                }

                var panel = new Panel(new Markup(string.Join("\n", text)))
                {
                    Header = new PanelHeader(" 🏆 Достижение ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                AnsiConsole.Write(panel);
                WaitForKey();
                continue;
            }
	            var tMatch = visibleTracked.FirstOrDefault(t =>
	                choice.Contains(Markup.Escape(t.name)));
	            if (tMatch.name != null)
	            {
	                var pct = tMatch.target > 0 ? (int)(100.0 * tMatch.current / tMatch.target) : 0;
	                var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
	                var cat = categoryLabels.GetValueOrDefault(tMatch.category, tMatch.category);
	                var text = new List<string>
	                {
	                    $"[bold]{tMatch.icon} {Markup.Escape(tMatch.name)}[/]",
	                    $"[dim]{cat} • {Markup.Escape(rarityLabels.GetValueOrDefault(tMatch.rarity, tMatch.rarity))}[/]",
                    "",
                    Markup.Escape(tMatch.desc),
                    "",
                    $"[yellow]{bar}[/] {tMatch.current}/{tMatch.target} ({pct}%)"
                };
                if (!string.IsNullOrWhiteSpace(tMatch.rewardType) || !string.IsNullOrWhiteSpace(tMatch.rewardValue))
                {
                    text.Add("");
                    text.Add($"[yellow]Награда:[/] {Markup.Escape(FormatAchievementRewardText(tMatch.rewardType, tMatch.rewardValue))}");
                }

                var panel = new Panel(new Markup(string.Join("\n", text)))
                {
                    Header = new PanelHeader(" 📊 Прогресс ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                AnsiConsole.Write(panel);
                WaitForKey();
            }
        }
    }

    private async Task ShowGmThoughts()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("output/debug_logs.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("gm_thoughts"), "Нет данных ГМ"); return; }

        var text = GetStr(doc.RootElement, "gm_thoughts_markdown", "Нет данных");
        var panel = new Panel(new Markup(Markup.Escape(text)))
        {
            Header = new PanelHeader($" {_loc.T("gm_thoughts")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowDebugInfo()
    {
        var files = _fs.GetAllGameStateFiles();
        var text = new List<string>
        {
            $"[bold]Файлов состояния:[/] {files.Length}",
            $"[bold]Сессия:[/] {_stateManager.CurrentState.SessionId}",
            $"[bold]Язык:[/] {_loc.CurrentLanguage}",
            "",
            "[yellow]Файлы:[/]"
        };

        foreach (var file in files.Take(20))
        {
            long size = 0;
            try { size = new FileInfo(file).Length; } catch { /* file may have been deleted */ }
            text.Add($"  {Markup.Escape(Path.GetFileName(file))} [dim]({size} байт)[/]");
        }

        if (files.Length > 20)
            text.Add($"  [dim]...и ещё {files.Length - 20} файлов[/]");

        // Multipliers
        var multDoc = await _stateManager.LoadGameStateFileAsync("game_state/misc/multipliers.json");
        if (multDoc != null)
        {
            text.Add("");
            text.Add("[yellow]Множители:[/]");
            foreach (var prop in multDoc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array || prop.Value.ValueKind == JsonValueKind.Object)
                {
                    EnumerateJsonItems(prop.Value, item =>
                    {
                        var name = GetStr(item, "name", GetStr(item, "id", prop.Name));
                        var val = GetStr(item, "value", GetStr(item, "multiplier", "?"));
                        text.Add($"  {Markup.Escape(name)}: [cyan]{Markup.Escape(val)}[/]");
                    });
                }
                else
                {
                    text.Add($"  {Markup.Escape(prop.Name)}: [cyan]{Markup.Escape(prop.Value.ToString())}[/]");
                }
            }
        }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("debug_info")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowCurrentLocation()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
        if (doc == null)
        {
            ShowEmptyPanel(_loc.T("where_am_i"), "Местоположение неизвестно");
            return;
        }

        var root = GetCurrentLocationRoot(doc.RootElement);
        var playerLevel = await GetPlayerLevelAsync();
        var text = new List<string>
        {
            $"[bold green]📍 {Markup.Escape(GetStr(root, "name", "Неизвестно"))}[/]",
        };

        var locType = GetStr(root, "locationType", "");
        var biome = GetStr(root, "biome", "");
        var typeInfo = new List<string>();
        if (!string.IsNullOrEmpty(locType)) typeInfo.Add(locType);
        if (!string.IsNullOrEmpty(biome)) typeInfo.Add(biome);
        if (typeInfo.Count > 0)
            text.Add($"  [dim]{Markup.Escape(string.Join(" • ", typeInfo))}[/]");

        // Difficulty assessment (use external profile first, fall back to internal)
        var profileProp = root.TryGetProperty("externalDifficultyProfile", out var extP) ? extP
            : root.TryGetProperty("internalDifficultyProfile", out var intP) ? intP
            : (JsonElement?)null;

        if (profileProp.HasValue && profileProp.Value.ValueKind == JsonValueKind.Object)
        {
            var (label, color) = GetProfileDifficultyLabel(profileProp.Value, playerLevel);
            text.Add($"  ⚠ Опасность: [{color}]{label}[/]  [dim](ур. {playerLevel})[/]");
        }
        else
        {
            // Simple difficulty field fallback
            var simpleDiff = GetInt(root, "difficulty", -1);
            if (simpleDiff >= 0)
            {
                var (label, color) = GetDifficultyLabel(simpleDiff, playerLevel);
                text.Add($"  ⚠ Опасность: [{color}]{label}[/]  [dim](ур. {playerLevel})[/]");
            }
        }

        text.Add("");

        var desc = GetStr(root, "description", "");
        if (!string.IsNullOrEmpty(desc))
            text.Add(Markup.Escape(desc));

        // Features
        if (root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
        {
            text.Add("");
            text.Add("[bold]Особенности:[/]");
            foreach (var f in features.EnumerateArray())
            {
                var fStr = f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : f.ToString();
                if (!string.IsNullOrEmpty(fStr))
                    text.Add($"  ✦ [cyan]{Markup.Escape(fStr)}[/]");
            }
        }

        // Faction control
        if (root.TryGetProperty("factionControl", out var fc) && fc.ValueKind == JsonValueKind.Array && fc.GetArrayLength() > 0)
        {
            text.Add("");
            foreach (var f in fc.EnumerateArray())
            {
                var fName = GetStr(f, "factionName", GetStr(f, "factionId", GetStr(f, "name", "?")));
                var fLevel = GetStr(f, "controlLevel", "");
                var fType = GetStr(f, "controlType", "");
                var fLine = $"  🏰 Фракция: [yellow]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(fType)) fLine += $" [dim]({Markup.Escape(fType)})[/]";
                if (!string.IsNullOrEmpty(fLevel)) fLine += $" контроль: [white]{Markup.Escape(fLevel)}%[/]";
                text.Add(fLine);
            }
        }

        // Active threats
        if (root.TryGetProperty("activeThreats", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
        {
            text.Add("");
            text.Add("[bold red]⚠ Активные угрозы:[/]");
            foreach (var t in threats.EnumerateArray())
                RenderThreatSummary(text, t);
        }

        var events = GetStr(root, "lastEventsDescription", "");
        if (!string.IsNullOrEmpty(events))
        {
            text.Add("");
            text.Add("[yellow]Последние события:[/]");
            text.Add(Markup.Escape(events));
        }

        // World time
        var timeDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_time.json");
        if (timeDoc != null)
        {
            AppendWorldTimeLines(text, timeDoc.RootElement, "");
        }

        // Weather
        var wDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/weather.json");
        if (wDoc != null)
        {
            var weatherRoot = GetWeatherRoot(wDoc.RootElement);
            var wDesc = GetStr(weatherRoot, "description", "");
            if (!string.IsNullOrEmpty(wDesc))
                text.Add($"🌤️ Погода: [cyan]{Markup.Escape(wDesc)}[/]");
        }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("where_am_i")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(panel);
        WaitForKey();
    }

    // ═════════════════════════════════════════════════════════
    // WORLD SETUP / DIRECTIVES
    // ═════════════════════════════════════════════════════════

    private async Task ShowWorldSetup()
    {
        if (_worldDirectiveService == null)
        {
            ShowEmptyPanel("Настройка мира", "Сервис world setup недоступен");
            return;
        }

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Настройка мира", "Подготовка следующего мира доступна только в Море Хаоса или Сияющей Обители. Для текущего мира используйте /world_rules.");
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            var pending = await _worldDirectiveService.ReadPendingSetupAsync();
            var profiles = await _worldDirectiveService.GetAvailableProfilesAsync();
            var profilesDir = _worldDirectiveService.GetProfilesDirectoryPath();

            var lines = new List<string>
            {
                "[bold cyan]🌍 Подготовка следующего мира[/]",
                "",
                "[white]Здесь можно заранее задать сеттинг следующей смертной жизни.[/]",
                "[white]Эти данные сохраняются в client-authored файле [bold]game_state/control/incarnation_world_setup.json[/].[/]",
                "[white]Во время воплощения GM обязан читать этот файл и затем клиент перенесёт его в [bold]lore/current_world/world_directives.json[/].[/]",
                $"[dim]Папка профилей: {Markup.Escape(profilesDir)}[/]"
            };

            if (pending == null)
            {
                lines.Add("");
                lines.Add("[yellow]Pending world setup пока не задан.[/]");
            }
            else
            {
                lines.Add("");
                var pendingTitle = string.IsNullOrWhiteSpace(pending.WorldDirectives.WorldTitle)
                    ? "Без названия"
                    : pending.WorldDirectives.WorldTitle;
                lines.Add($"[green]Текущий pending setup:[/] [bold]{Markup.Escape(pendingTitle)}[/]");
                lines.Add($"[dim]Режим: {Markup.Escape(pending.Mode)}[/]");
                if (!string.IsNullOrWhiteSpace(pending.ProfileName))
                    lines.Add($"[dim]Профиль: {Markup.Escape(pending.ProfileName)} ({Markup.Escape(pending.ProfileId ?? "")})[/]");
                AppendWorldDirectiveLines(lines, pending.WorldDirectives, concise: true);
            }

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🌍 World Setup ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();

            var actions = new List<string>
            {
                "👁 Полный просмотр pending setup",
                "📚 Просмотреть профили миров",
                "✅ Применить профиль мира",
                "✏️ Создать / редактировать pending setup",
                "🧹 Очистить pending setup",
                "📂 Открыть папку профилей",
                "← Назад"
            };

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Действие:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(actions));

            if (choice == "← Назад")
                return;

            if (choice == "👁 Полный просмотр pending setup")
            {
                if (pending == null)
                {
                    ShowEmptyPanel("Pending World Setup", "Pending setup пока не задан.");
                }
                else
                {
                    var detailLines = new List<string>();
                    var pendingTitle = string.IsNullOrWhiteSpace(pending.WorldDirectives.WorldTitle)
                        ? "Без названия"
                        : pending.WorldDirectives.WorldTitle;
                    detailLines.Add($"[bold cyan]{Markup.Escape(pendingTitle)}[/]");
                    detailLines.Add($"[dim]Режим: {Markup.Escape(pending.Mode)}[/]");
                    if (!string.IsNullOrWhiteSpace(pending.ProfileName))
                        detailLines.Add($"[dim]Профиль: {Markup.Escape(pending.ProfileName)} ({Markup.Escape(pending.ProfileId ?? "")})[/]");
                    detailLines.Add("");
                    AppendWorldDirectiveLines(detailLines, pending.WorldDirectives, concise: false);

                    AnsiConsole.Write(new Panel(new Markup(string.Join("\n", detailLines)))
                    {
                        Header = new PanelHeader(" 👁 Pending World Setup ", Justify.Center),
                        Border = BoxBorder.Double,
                        BorderStyle = new Style(Color.Cyan1),
                        Padding = new Padding(2, 1),
                        Expand = true
                    });
                    WaitForKey();
                }
                continue;
            }

            if (choice == "📚 Просмотреть профили миров")
            {
                await ShowWorldProfiles(profiles);
                continue;
            }

            if (choice == "✅ Применить профиль мира")
            {
                if (profiles.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]В папке world_profiles пока нет профилей.[/]");
                    WaitForKey();
                    continue;
                }

                var selectedLabel = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Выберите профиль:[/]")
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(profiles.Select(profile => $"{profile.Name} ({profile.FileName})")));
                var profile = profiles.First(p => $"{p.Name} ({p.FileName})" == selectedLabel);
                var setup = _worldDirectiveService.CreatePendingSetupFromProfile(profile);
                await _worldDirectiveService.WritePendingSetupAsync(setup);
                AnsiConsole.MarkupLine($"[green]Профиль мира «{Markup.Escape(profile.Name)}» применён к pending setup.[/]");
                WaitForKey();
                continue;
            }

            if (choice == "✏️ Создать / редактировать pending setup")
            {
                await EditPendingWorldSetupAsync(pending);
                continue;
            }

            if (choice == "🧹 Очистить pending setup")
            {
                if (AnsiConsole.Confirm("[yellow]Очистить сохранённую подготовку следующего мира?[/]", false))
                {
                    _worldDirectiveService.ClearPendingSetup();
                    AnsiConsole.MarkupLine("[green]Pending world setup очищен.[/]");
                    WaitForKey();
                }
                continue;
            }

            if (choice == "📂 Открыть папку профилей")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = profilesDir,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(profilesDir)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    private async Task ShowWorldRules()
    {
        if (_worldDirectiveService == null)
        {
            ShowEmptyPanel("Правила мира", "Сервис world directives недоступен");
            return;
        }

        if (_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Правила мира", "Во время загробного цикла используйте /world_setup для подготовки следующего мира. Активные world directives появляются в смертной жизни.");
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            var directives = await _worldDirectiveService.ReadActiveWorldDirectivesAsync();
            var lines = new List<string>
            {
                "[bold green]📜 Досье текущего мира[/]",
                "",
                "[white]Это persistent player-authored world dossier текущей смертной жизни.[/]",
                "[white]GM должен читать [bold]lore/current_world/world_directives.json[/] на каждом ходе.[/]"
            };

            if (directives == null)
            {
                lines.Add("");
                lines.Add("[yellow]Файл world_directives.json ещё не создан.[/]");
                lines.Add("[dim]Вы можете создать его сейчас и зафиксировать описание мира, ограничения и поправки.[/]");
            }
            else
            {
                lines.Add("");
                AppendWorldDirectiveLines(lines, directives, concise: false);
            }

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📜 World Directives ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green3),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Действие:[/]")
                    .HighlightStyle(new Style(Color.Green3))
                    .AddChoices("✏️ Создать / редактировать world directives", "🧹 Очистить world directives", "← Назад"));

            if (choice == "← Назад")
                return;

            if (choice == "✏️ Создать / редактировать world directives")
            {
                var edited = await PromptWorldDirectivesAsync(directives ?? new WorldDirectiveService.WorldDirectives(), allowProfileMetadataEdit: false);
                await _worldDirectiveService.WriteActiveWorldDirectivesAsync(edited);
                AnsiConsole.MarkupLine("[green]World directives сохранены.[/]");
                WaitForKey();
                continue;
            }

            if (choice == "🧹 Очистить world directives")
            {
                if (AnsiConsole.Confirm("[yellow]Удалить активное досье текущего мира?[/]", false))
                {
                    _fs.DeleteFile(WorldDirectiveService.ActiveDirectivesPath);
                    AnsiConsole.MarkupLine("[green]World directives очищены.[/]");
                    WaitForKey();
                }
            }
        }
    }

    private async Task ShowWorldProfiles(List<WorldDirectiveService.WorldProfileDescriptor> profiles)
    {
        if (profiles.Count == 0)
        {
            ShowEmptyPanel("Профили миров", "В папке world_profiles пока нет профилей.");
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Профили миров:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .PageSize(15)
                    .AddChoices(profiles.Select(profile => $"{profile.Name} ({profile.FileName})").Append("← Назад")));

            if (choice == "← Назад")
                return;

            var profile = profiles.First(p => $"{p.Name} ({p.FileName})" == choice);
            var lines = new List<string>
            {
                $"[bold cyan]{Markup.Escape(profile.Name)}[/]",
                $"[dim]{Markup.Escape(profile.FileName)}[/]"
            };
            if (!string.IsNullOrWhiteSpace(profile.Description))
            {
                lines.Add("");
                lines.Add(GameInterface.EscapeMarkup(profile.Description));
            }

            lines.Add("");
            AppendWorldDirectiveLines(lines, profile.Directives, concise: false);

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📚 World Profile ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WaitForKey();
        }
    }

    private async Task EditPendingWorldSetupAsync(WorldDirectiveService.PendingWorldSetup? existing)
    {
        if (_worldDirectiveService == null)
            return;

        var seed = existing?.WorldDirectives != null
            ? WorldDirectiveService.CloneDirectives(existing.WorldDirectives)
            : new WorldDirectiveService.WorldDirectives();
        var edited = await PromptWorldDirectivesAsync(seed, allowProfileMetadataEdit: true);
        var mode = existing?.Mode ?? "manual";
        if (!string.IsNullOrWhiteSpace(existing?.ProfileId))
            mode = "mixed";

        var setup = new WorldDirectiveService.PendingWorldSetup
        {
            Mode = mode,
            ProfileId = existing?.ProfileId,
            ProfileName = existing?.ProfileName,
            WorldDirectives = edited
        };
        await _worldDirectiveService.WritePendingSetupAsync(setup);
        AnsiConsole.MarkupLine("[green]Pending world setup сохранён.[/]");
        WaitForKey();
    }

    private async Task<WorldDirectiveService.WorldDirectives> PromptWorldDirectivesAsync(
        WorldDirectiveService.WorldDirectives seed,
        bool allowProfileMetadataEdit)
    {
        var directives = WorldDirectiveService.CloneDirectives(seed);
        directives.WorldTitle = PromptOptionalText("Название мира", directives.WorldTitle);
        directives.Genre = PromptOptionalText("Жанр", directives.Genre);
        directives.Era = PromptOptionalText("Эпоха", directives.Era);
        directives.Tone = PromptOptionalText("Тон", directives.Tone);
        directives.SettingSummary = PromptOptionalMultiline("Краткое описание мира", directives.SettingSummary);
        directives.HardRules = PromptCsvList("Жёсткие правила мира", directives.HardRules);
        directives.RequiredElements = PromptCsvList("Обязательные элементы", directives.RequiredElements);
        directives.ForbiddenElements = PromptCsvList("Запрещённые элементы", directives.ForbiddenElements);
        directives.SpecialMechanics = PromptCsvList("Особые механики", directives.SpecialMechanics);
        directives.ContinuityNotes = PromptCsvList("Ноты непрерывности / важные уточнения", directives.ContinuityNotes);
        directives.PlayerAmendments = PromptCsvList("Поправки игрока", directives.PlayerAmendments);

        if (allowProfileMetadataEdit)
        {
            directives.SourceProfileId = directives.SourceProfileId?.Trim();
            directives.SourceProfileName = directives.SourceProfileName?.Trim();
        }

        directives.LastUpdated = DateTime.UtcNow.ToString("o");
        await Task.CompletedTask;
        return directives;
    }

    private static void AppendWorldDirectiveLines(List<string> lines, WorldDirectiveService.WorldDirectives directives, bool concise)
    {
        if (!string.IsNullOrWhiteSpace(directives.WorldTitle))
            lines.Add($"[bold]Название:[/] {Markup.Escape(directives.WorldTitle)}");
        if (!string.IsNullOrWhiteSpace(directives.Genre))
            lines.Add($"[bold]Жанр:[/] {Markup.Escape(directives.Genre)}");
        if (!string.IsNullOrWhiteSpace(directives.Era))
            lines.Add($"[bold]Эпоха:[/] {Markup.Escape(directives.Era)}");
        if (!string.IsNullOrWhiteSpace(directives.Tone))
            lines.Add($"[bold]Тон:[/] {Markup.Escape(directives.Tone)}");
        if (!string.IsNullOrWhiteSpace(directives.SettingSummary))
            lines.Add($"[bold]Описание:[/] {Markup.Escape(directives.SettingSummary)}");
        if (!string.IsNullOrWhiteSpace(directives.SourceProfileName))
            lines.Add($"[dim]Источник: {Markup.Escape(directives.SourceProfileName)} ({Markup.Escape(directives.SourceProfileId ?? "")})[/]");

        AppendStringList(lines, "Жёсткие правила", directives.HardRules, concise);
        AppendStringList(lines, "Обязательные элементы", directives.RequiredElements, concise);
        AppendStringList(lines, "Запрещённые элементы", directives.ForbiddenElements, concise);
        AppendStringList(lines, "Особые механики", directives.SpecialMechanics, concise);
        AppendStringList(lines, "Ноты непрерывности", directives.ContinuityNotes, concise);
        AppendStringList(lines, "Поправки игрока", directives.PlayerAmendments, concise);
    }

    private static void AppendStringList(List<string> lines, string label, IReadOnlyList<string> items, bool concise)
    {
        if (items.Count == 0)
            return;

        if (concise)
        {
            var shown = items.Take(4).Select(Markup.Escape).ToList();
            var suffix = items.Count > 4 ? $" [dim](+{items.Count - 4})[/]" : "";
            lines.Add($"[bold]{label}:[/] {string.Join("; ", shown)}{suffix}");
            return;
        }

        lines.Add($"[bold]{label}:[/]");
        foreach (var item in items)
            lines.Add($"  • {Markup.Escape(item)}");
    }

    private static string PromptOptionalText(string title, string current)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>($"[cyan]{Markup.Escape(title)}:[/]")
                .AllowEmpty()
                .DefaultValue(current ?? string.Empty));
    }

    private static string PromptOptionalMultiline(string title, string current)
    {
        AnsiConsole.MarkupLine($"[cyan]{Markup.Escape(title)}:[/]");
        AnsiConsole.MarkupLine("[dim]Введите текст одной строкой. Пустое значение допустимо.[/]");
        return AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]>[/]")
                .AllowEmpty()
                .DefaultValue(current ?? string.Empty));
    }

    private static List<string> PromptCsvList(string title, IReadOnlyCollection<string> current)
    {
        var currentValue = string.Join(", ", current);
        var raw = AnsiConsole.Prompt(
            new TextPrompt<string>($"[cyan]{Markup.Escape(title)} (через запятую):[/]")
                .AllowEmpty()
                .DefaultValue(currentValue));

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ═════════════════════════════════════════════════════════
    // SYSTEM MODS — read-only global mod inspector
    // ═════════════════════════════════════════════════════════

    private async Task ShowSystemMods()
    {
        if (_systemModService == null)
        {
            ShowEmptyPanel("Системные моды", "Сервис системных модов недоступен");
            return;
        }

        while (true)
        {
            var mods = await _systemModService.GetAvailableModsAsync(includeContent: false);
            var lines = new List<string>
            {
                "[bold yellow]🧩 Глобальные системные моды[/]",
                "",
                "[white]Это глобальные надстройки над всей игрой.[/]",
                "[white]Каноничны только activeMods[] из game_state/core/system_mods.json.[/]",
                "[white]Включение и выключение выполняется через /options.[/]",
                "[yellow]Игрок несёт полную ответственность за совместимость, баланс и работоспособность модов.[/]",
                $"[dim]Папка модов: {Markup.Escape(_systemModService.GetModsDirectoryPath())}[/]"
            };

            if (mods.Count == 0)
            {
                lines.Add("");
                lines.Add("[dim]В game_session/mods пока нет файлов модов.[/]");
            }
            else
            {
                lines.Add("");
                lines.Add($"[bold]Найдено модов:[/] {mods.Count}  [bold]Активно:[/] {mods.Count(m => m.Enabled)}");
                lines.Add("");

                foreach (var mod in mods)
                {
                    var state = mod.Enabled ? "[green]● Активен[/]" : "[dim]○ Выключен[/]";
                    lines.Add($"{state} [white]{Markup.Escape(mod.Name)}[/] [dim]({Markup.Escape(mod.FileName)})[/]");
                    if (!string.IsNullOrWhiteSpace(mod.Description))
                        lines.Add($"  [dim]{Markup.Escape(mod.Description)}[/]");
                }
            }

            AnsiConsole.Clear();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🧩 System Mods ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();

            var actions = new List<string>();
            if (mods.Count > 0)
                actions.AddRange(mods.Select(mod => $"📄 {mod.Name} ({mod.FileName})"));
            actions.Add("📂 Открыть папку модов");
            actions.Add("← Назад");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[gold1]Действие:[/]")
                    .HighlightStyle(new Style(Color.Gold1))
                    .PageSize(18)
                    .AddChoices(actions));

            if (choice == "← Назад")
                return;

            if (choice == "📂 Открыть папку модов")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _systemModService.GetModsDirectoryPath(),
                        UseShellExecute = true
                    });
                }
                catch
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_systemModService.GetModsDirectoryPath())}[/]");
                    WaitForKey();
                }
                continue;
            }

            var selected = mods.FirstOrDefault(mod => choice == $"📄 {mod.Name} ({mod.FileName})");
            if (selected != null)
                await ShowSystemModDetailAsync(selected.FileName);
        }
    }

    private async Task ShowSystemModDetailAsync(string fileName)
    {
        if (_systemModService == null)
            return;

        var mods = await _systemModService.GetAvailableModsAsync(includeContent: true);
        var mod = mods.FirstOrDefault(m => string.Equals(m.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (mod == null)
        {
            ShowEmptyPanel("System Mod", "Файл мода больше не найден.");
            return;
        }

        var lines = new List<string>
        {
            $"[bold yellow]{Markup.Escape(mod.Name)}[/]",
            $"[dim]{Markup.Escape(mod.FileName)}[/]",
            mod.Enabled ? "[green]● Активен[/]" : "[dim]○ Выключен[/]"
        };

        if (!string.IsNullOrWhiteSpace(mod.Description))
        {
            lines.Add("");
            lines.Add(Markup.Escape(mod.Description));
        }

        if (!string.IsNullOrWhiteSpace(mod.Content))
        {
            lines.Add("");
            lines.Add("[bold]Содержимое мода:[/]");
            lines.Add(Markup.Escape(mod.Content));
        }

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📄 System Mod Detail ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    // ═════════════════════════════════════════════════════════
    // LEGACY CUSTOM RULES — deprecated compatibility layer (no longer canonical)
    // ═════════════════════════════════════════════════════════
    private Task ShowHelp()
    {
        var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
        var isShiningAbode = _stateManager.CurrentState.IsInShiningAbode;
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;

        var table = new Table()
            .Border(TableBorder.HeavyEdge)
            .BorderColor(isShiningAbode ? Color.Gold1 : (isAfterlife ? Color.Blue : Color.Green3))
            .AddColumn(new TableColumn("[bold]EN[/]").Width(18))
            .AddColumn(new TableColumn("[bold]RU[/]").Width(20))
            .AddColumn("[bold]Описание[/]");

        if (isShiningAbode)
        {
            table.AddRow("[bold yellow]── СИЯЮЩАЯ ОБИТЕЛЬ ──[/]", "", "");
            table.AddRow("[yellow]/guardians[/]", "[yellow]/хранители[/]", "[yellow]Информация о хранителях[/]");
            table.AddRow("[yellow]/soul[/]", "[yellow]/душа[/]", "[yellow]Состояние души и мета-прогрессия[/]");
            table.AddRow("[yellow]/soul_relics[/]", "[yellow]/реликвии[/]", "[yellow]Реликвии души[/]");
            table.AddRow("[yellow]/soul_quests[/]", "[yellow]/квесты_души[/]", "[yellow]Квесты хранителей[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья[/]");
            table.AddRow("[cyan]/world_setup[/]", "[cyan]/настройка_мира[/]", "[cyan]Подготовить следующий смертный мир[/]");
            table.AddRow("", "", "");
            table.AddRow("[bold gold1]/new_game_plus[/]", "[bold gold1]/новая_игра+[/]", "[bold gold1]Начать Новый Цикл, сохранив Реликвии Души и Хранителей[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]💡 Это финальная зона свободного ролеплея над Морем Хаоса[/]", "", "");
        }
        else if (isChaosSea)
        {
            // Chaos Sea commands
            table.AddRow("[bold blue]── МОРЕ ХАОСА (загробная жизнь) ──[/]", "", "");
            table.AddRow("[blue]/guardians[/]", "[blue]/хранители[/]", "[blue]Информация о хранителях[/]");
            table.AddRow("[blue]/soul[/]", "[blue]/душа[/]", "[blue]Состояние души (перья, просветление, история жизней)[/]");
            table.AddRow("[blue]/soul_relics[/]", "[blue]/реликвии[/]", "[blue]Реликвии души (экипировка, хранилище)[/]");
            table.AddRow("[blue]/soul_quests[/]", "[blue]/квесты_души[/]", "[blue]Квесты от хранителей[/]");
            table.AddRow("[gold1]/gacha[/]", "[gold1]/гача[/]", "[gold1]Прямое вытягивание реликвии из Моря Хаоса (без модификаторов Хранителя)[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья (способности души)[/]");
            table.AddRow("[cyan]/world_setup[/]", "[cyan]/настройка_мира[/]", "[cyan]Подготовить следующий смертный мир[/]");
            table.AddRow("", "", "");
            table.AddRow("[yellow]/incarnate[/]", "[yellow]/воплотиться[/]", "[yellow]⚔️ Войти в смертную жизнь через Врата Души[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]💡 Говорите с Хранителем свободным текстом:[/]", "", "");
            table.AddRow("[dim]   торговать, брать квесты, менять реликвии, сменить хранителя[/]", "", "");
        }
        else
        {
            // Mortal Life commands
            table.AddRow("[bold green]── СМЕРТНАЯ ЖИЗНЬ ──[/]", "", "");
            table.AddRow("/inv", "/инв", "Показать инвентарь");
            table.AddRow("/npc /npcs", "/нпс", "Показать персонажей");
            table.AddRow("/quests", "/квесты", "Показать квесты (смертные)");
            table.AddRow("/map", "/карта", "Показать карту");
            table.AddRow("/status", "/статус", "Детальный статус персонажа");
            table.AddRow("/skills", "/навыки", "Показать навыки");
            table.AddRow("/stats", "/статы", "Показать характеристики");
            table.AddRow("/distribute", "/распределить", "Распределить очки характеристик");
            table.AddRow("/companion_directive", "/директива_компаньону", "Задать указание компаньону");
            table.AddRow("/faction_directive", "/директива_фракции", "Задать стратегию фракции");
            table.AddRow("/effects", "/эффекты", "Эффекты, раны, состояния, опыт");
            table.AddRow("/combat", "/бой", "⚔️ Боевая обстановка (враги, союзники)");
            table.AddRow("/factions", "/фракции", "Показать фракции");
            table.AddRow("/world_news", "/новости_мира", "Мировые события");
            table.AddRow("/craft", "/ремесло", "Рецепты крафта");
            table.AddRow("/locations", "/локации", "Известные локации");
            table.AddRow("/where_am_i", "/где_я", "Текущая локация");
            table.AddRow("/weather", "/погода", "Время и погода");
            table.AddRow("/transport", "/транспорт", "Транспорт");
            table.AddRow("/books", "/книги", "Книги, письма, свитки");
            table.AddRow("/world_rules", "/правила_мира", "📜 Досье и директивы текущего мира");
            table.AddRow("/storage_access", "/доступ_к_хранилищам", "Доступ к хранилищам");
            table.AddRow("/interactions", "/взаимодействия", "Взаимодействия других игроков");
            table.AddRow("", "", "");
            table.AddRow("[blue]/soul_relics[/]", "[blue]/реликвии[/]", "[blue]Реликвии души (только просмотр!)[/]");
            table.AddRow("[blue]/soul_quests[/]", "[blue]/квесты_души[/]", "[blue]Квесты хранителей (только просмотр)[/]");
            table.AddRow("[blue]/soul[/]", "[blue]/душа[/]", "[blue]Состояние души[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья (способности судьбы)[/]");
            table.AddRow("", "", "");
            table.AddRow("[yellow]/end_of_life[/]", "[yellow]/конец_жизни[/]", "[yellow]💀 Завершить жизнь → вернуться в Море Хаоса[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]⚠️ В смертной жизни нельзя: менять реликвии, общаться с хранителями[/]", "", "");
        }

        // Common
        table.AddRow("", "", "");
        table.AddRow("[bold grey]── Общие команды ──[/]", "", "");
        table.AddRow("[grey]/codex[/]", "[grey]/кодекс[/]", "Лор и знания");
        table.AddRow("[grey]/chronicle[/]", "[grey]/хроника[/]", "📖 Хроника и сюжет");
        table.AddRow("[grey]/story[/]", "[grey]/рассказ[/]", "📜 Полная история (все ходы по главам)");
        table.AddRow("[grey]/achievements[/]", "[grey]/достижения[/]", "🏆 Достижения");
        table.AddRow("[grey]/behavior[/]", "[grey]/поведение[/]", "🧠 Оценка поведения и манипуляция историей");
        table.AddRow("[grey]/lives[/]", "[grey]/жизни[/]", "📜 История прошлых жизней");
        table.AddRow("[grey]/validate[/]", "[grey]/валидация[/]", "🔍 Проверка файлов");
        table.AddRow("[grey]/mods[/]", "[grey]/моды[/]", "🧩 Глобальные системные моды");
        table.AddRow("[grey]/gallery[/]", "[grey]/галерея[/]", "🖼 Галерея изображений");
        table.AddRow("[grey]/options[/]", "[grey]/опции[/]", "⚙ Игровое меню");
        table.AddRow("[grey]/gm[/]", "[grey]/гм[/]", "🧠 Мысли Мастера Игры");
        table.AddRow("[grey]/debug[/]", "[grey]/отладка[/]", "🔧 Отладка");
        table.AddRow("[grey]/help[/]", "[grey]/помощь[/]", "❓ Эта справка");
        table.AddRow("[grey]/refresh[/]", "[grey]/обновить[/]", "🔄 Перечитать все данные и перерисовать экран");

        var helpColor = isChaosSea ? Color.Blue : Color.Green3;
        WrapInPanel(table, $"❓ {_loc.T("help")}", helpColor);
        WaitForKey();
        return Task.CompletedTask;
    }

    // ═══ Soul / Meta-game commands ═══

    private async Task ShowSoulInfo()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null)
        {
            ShowEmptyPanel(_loc.T("soul_info"), "Данные души недоступны");
            return;
        }

        var root = doc.RootElement;
        var metaUpdates = root.TryGetProperty("metaStateUpdates", out var msu) && msu.ValueKind == JsonValueKind.Object
            ? msu
            : (JsonElement?)null;
        var text = new List<string>();

        var soulName = GetStr(root, "soulName", "Безымянная");
        var realm = GetStr(root, "currentRealm", "Chaos Sea");
        var incNum = GetInt(root, "currentIncarnation", 0);

        text.Add($"[bold white]👻 {Markup.Escape(soulName)}[/]");
        text.Add($"🌍 Текущий мир: [cyan]{Markup.Escape(realm)}[/]");
        text.Add($"🔄 Инкарнация: [yellow]{incNum}[/]");
        text.Add("");

        // Enlightenment — handle both "soulProgression" and "enlightenment" formats
        if (root.TryGetProperty("soulProgression", out var sp) && sp.ValueKind == JsonValueKind.Object)
        {
            var tierName = GetStr(sp, "tierName", "");
            var tierNum = GetInt(sp, "tier", 0);
            if (string.IsNullOrEmpty(tierName))
                tierName = tierNum switch { 0 => "Смятение", 1 => "Осознание", 2 => "Понимание", 3 => "Мудрость", 4 => "Трансценденция", _ => $"Ур. {tierNum}" };
            var totalExp = GetInt(sp, "totalExperience", 0);
            var expInTier = GetInt(sp, "experienceInCurrentTier", 0);
            var expForNext = GetInt(sp, "experienceForNextTier", 0);
            var progressPct = GetInt(sp, "progressPercent", expForNext > 0 ? (int)(100.0 * expInTier / expForNext) : 0);
            var totalInc = GetInt(sp, "totalIncarnations", 0);

            text.Add($"✨ Просветление: [magenta]{Markup.Escape(tierName)}[/] (ранг {tierNum})");
            if (expForNext > 0)
            {
                var barW = 20;
                var filled = Math.Clamp(progressPct * barW / 100, 0, barW);
                var bar = $"[magenta]{new string('━', filled)}[/][dim grey]{new string('┄', barW - filled)}[/]";
                text.Add($"   {bar}  [dim]{expInTier}/{expForNext} ({progressPct}%)[/]");
            }
            else
            {
                text.Add($"   Опыт: [dim]{totalExp}[/]");
            }
            if (totalInc > 0)
                text.Add($"   Всего инкарнаций: [dim]{totalInc}[/]");
        }
        else if (root.TryGetProperty("enlightenment", out var enl))
        {
            if (enl.ValueKind == JsonValueKind.Object)
            {
                var tier = GetStr(enl, "currentTier", "Новичок");
                var exp = GetInt(enl, "experience", 0);
                var lvl = GetInt(enl, "level", 0);
                text.Add($"✨ Просветление: [magenta]{Markup.Escape(tier)}[/] (Ур. {lvl}, Опыт: {exp})");
            }
            else
            {
                // plain number format (e.g. "enlightenment": 0)
                var enlVal = enl.ValueKind == JsonValueKind.Number ? enl.GetInt32() : 0;
                var currentTier = GetStr(root, "currentTier", "Новичок");
                text.Add($"✨ Просветление: [magenta]{Markup.Escape(currentTier)}[/] (опыт: {enlVal})");
            }
        }

        // Ink Feathers — handle both object and plain number
        if (root.TryGetProperty("inkFeathers", out var feathers))
        {
            if (feathers.ValueKind == JsonValueKind.Object)
            {
                var current = GetInt(feathers, "current", 0);
                var total = GetInt(feathers, "total", 0);
                text.Add($"🪶 Чернильные перья: [yellow]{current}[/] (всего заработано: {total})");
            }
            else if (feathers.ValueKind == JsonValueKind.Number)
            {
                text.Add($"🪶 Чернильные перья: [yellow]{feathers.GetInt32()}[/]");
            }
        }

        if (root.TryGetProperty("pendingMemoryLegacy", out var pendingLegacy) && pendingLegacy.ValueKind == JsonValueKind.Object)
        {
            text.Add("");
            text.Add("[bold]🧠 Наследие Памяти:[/]");
            var legacyType = GetStr(pendingLegacy, "legacyType", "");
            var sourceLifeHint = GetStr(pendingLegacy, "sourceLifeHint", "");
            var applicationState = GetStr(pendingLegacy, "applicationState", "");
            var grantSource = GetStr(pendingLegacy, "grantSource", "");
            var grantedAtUtc = GetStr(pendingLegacy, "grantedAtUtc", "");
            if (legacyType.Equals("startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = GetStr(pendingLegacy, "characteristic", "");
                var bonus = GetInt(pendingLegacy, "bonus", 0);
                var russianStat = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
                text.Add($"  Следующая жизнь получит [white]+{bonus} к {Markup.Escape(russianStat)}[/]");
            }
            else if (legacyType.Equals("startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                var skillName = GetStr(pendingLegacy, "skillName", "Неизвестный навык");
                text.Add($"  Следующая жизнь получит пассивный навык [white]{Markup.Escape(skillName)}[/]");
                var skillDescription = GetStr(pendingLegacy, "skillDescription", "");
                if (!string.IsNullOrWhiteSpace(skillDescription))
                    text.Add($"  [dim]{Markup.Escape(skillDescription)}[/]");

                var playerStatBonus = GetStr(pendingLegacy, "playerStatBonus", "");
                if (!string.IsNullOrWhiteSpace(playerStatBonus))
                    text.Add($"  Бонус игроку: [green]{Markup.Escape(playerStatBonus)}[/]");

                if (pendingLegacy.TryGetProperty("structuredBonuses", out var legacyBonuses) &&
                    legacyBonuses.ValueKind == JsonValueKind.Array &&
                    legacyBonuses.GetArrayLength() > 0)
                {
                    text.Add("  [bold]Структурные бонусы:[/]");
                    foreach (var bonus in legacyBonuses.EnumerateArray())
                    {
                        var bonusType = GetStr(bonus, "bonusType", "???");
                        var target = GetStr(bonus, "target", "");
                        var value = GetStr(bonus, "value", "");
                        var application = GetStr(bonus, "application", "");

                        var bonusLine = $"    • [cyan]{Markup.Escape(bonusType)}[/]";
                        if (!string.IsNullOrWhiteSpace(target))
                            bonusLine += $" → {Markup.Escape(target)}";
                        if (!string.IsNullOrWhiteSpace(value))
                            bonusLine += $": [yellow]{Markup.Escape(value)}[/]";
                        if (!string.IsNullOrWhiteSpace(application))
                            bonusLine += $" [dim]({Markup.Escape(application)})[/]";
                        text.Add(bonusLine);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceLifeHint))
                text.Add($"  [dim]{Markup.Escape(sourceLifeHint)}[/]");
            if (!string.IsNullOrWhiteSpace(applicationState))
                text.Add($"  Состояние применения: [white]{Markup.Escape(applicationState)}[/]");
            if (!string.IsNullOrWhiteSpace(grantSource))
                text.Add($"  Источник: [white]{Markup.Escape(grantSource)}[/]");
            if (!string.IsNullOrWhiteSpace(grantedAtUtc))
                text.Add($"  Получено: [dim]{Markup.Escape(grantedAtUtc)}[/]");

            if (pendingLegacy.TryGetProperty("grantSnapshot", out var grantSnapshot) && grantSnapshot.ValueKind == JsonValueKind.Object)
            {
                text.Add("  [bold]Снимок награды:[/]");
                var excludedSnapshotFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "legacyId", "sourceLifeHint", "legacyType", "grantSource", "grantSnapshot", "applicationState", "grantedAtUtc"
                };

                foreach (var prop in grantSnapshot.EnumerateObject())
                {
                    if (excludedSnapshotFields.Contains(prop.Name))
                        continue;
                    RenderReadableJsonValue(text, prop.Name, prop.Value, "    ");
                }
            }

            if (pendingLegacy.TryGetProperty("applicationAudit", out var applicationAudit) &&
                applicationAudit.ValueKind == JsonValueKind.Object)
            {
                text.Add("  [bold]Аудит применения:[/]");
                var excludedAuditFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "expectedStructuredBonusesCanonical"
                };

                foreach (var prop in applicationAudit.EnumerateObject())
                {
                    if (excludedAuditFields.Contains(prop.Name))
                        continue;
                    RenderReadableJsonValue(text, prop.Name, prop.Value, "    ");
                }
            }
        }

        // Cross-incarnation stats
        if (root.TryGetProperty("crossIncarnationData", out var cross) && cross.ValueKind == JsonValueKind.Object)
        {
            var totalLives = GetInt(cross, "totalLives", 0);
            var questsDone = GetInt(cross, "questsCompleted", 0);
            var expGained = GetInt(cross, "experienceGained", 0);
            if (totalLives > 0 || questsDone > 0 || expGained > 0)
            {
                text.Add("");
                text.Add("[bold]📊 Итоги всех жизней:[/]");
                if (totalLives > 0) text.Add($"  Прожито жизней: [white]{totalLives}[/]");
                if (questsDone > 0) text.Add($"  Квестов выполнено: [white]{questsDone}[/]");
                if (expGained > 0) text.Add($"  Опыта получено: [white]{expGained}[/]");
            }
        }

        if (root.TryGetProperty("livesHistory", out var lives) && lives.ValueKind == JsonValueKind.Array)
        {
            text.Add($"\n📜 Прожитых жизней: [dim]{lives.GetArrayLength()}[/]");
            foreach (var life in lives.EnumerateArray())
            {
                var lifeName = GetStr(life, "characterName", "Безымянный");
                var lifeWorld = GetStr(life, "world", "");
                var outcome = GetStr(life, "outcome", "");
                text.Add($"  • {Markup.Escape(lifeName)} — {Markup.Escape(lifeWorld)} — {Markup.Escape(outcome)}");
            }
        }

        if (root.TryGetProperty("soulRelics", out var soulRelics))
        {
            var equippedCount = 0;
            var storedCount = 0;
            if (soulRelics.ValueKind == JsonValueKind.Object)
            {
                if (soulRelics.TryGetProperty("equipped", out var equipped) && equipped.ValueKind == JsonValueKind.Array)
                    equippedCount = equipped.GetArrayLength();
                if (soulRelics.TryGetProperty("stored", out var stored) && stored.ValueKind == JsonValueKind.Array)
                    storedCount = stored.GetArrayLength();
            }
            else if (soulRelics.ValueKind == JsonValueKind.Array)
            {
                foreach (var relic in soulRelics.EnumerateArray())
                {
                    var isEquipped = relic.TryGetProperty("gameplayStatus", out var gs) &&
                                     gs.TryGetProperty("equipped", out var eq) &&
                                     eq.ValueKind == JsonValueKind.True;
                    if (isEquipped) equippedCount++;
                    else storedCount++;
                }
            }

            text.Add("");
            text.Add($"💎 Реликвии души: [yellow]{equippedCount} экипировано[/], [yellow]{storedCount} в хранилище[/]");
        }

        if (root.TryGetProperty("soulImprint", out var imprint))
        {
            text.Add("");
            text.Add("[bold]👤 Слепок души:[/]");
            if (imprint.ValueKind == JsonValueKind.Object)
            {
                var imprintName = GetStr(imprint, "NPCName", GetStr(imprint, "name", GetStr(imprint, "companionName", "Неизвестный слепок")));
                text.Add($"  Имя: [white]{Markup.Escape(imprintName)}[/]");
                var imprintDesc = GetStr(imprint, "description", GetStr(imprint, "summary", ""));
                if (!string.IsNullOrWhiteSpace(imprintDesc))
                    text.Add($"  [dim]{Markup.Escape(imprintDesc)}[/]");

                if (imprint.TryGetProperty("coreTraitsPreserved", out var coreTraits) && coreTraits.ValueKind == JsonValueKind.Array)
                {
                    var traits = coreTraits.EnumerateArray()
                        .Where(t => t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
                        .Select(t => Markup.Escape(t.GetString() ?? ""))
                        .ToList();
                    if (traits.Count > 0)
                        text.Add($"  Черты: [white]{string.Join(", ", traits)}[/]");
                }

                if (imprint.TryGetProperty("personalityTraitsPreserved", out var personalityTraits) && personalityTraits.ValueKind == JsonValueKind.Array)
                {
                    var traits = personalityTraits.EnumerateArray()
                        .Where(t => t.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(t.GetString()))
                        .Select(t => Markup.Escape(t.GetString() ?? ""))
                        .ToList();
                    if (traits.Count > 0)
                        text.Add($"  Личностные маркеры: [white]{string.Join(", ", traits)}[/]");
                }
            }
            else if (imprint.ValueKind == JsonValueKind.Array)
            {
                text.Add($"  [dim]Сохранено отпечатков: {imprint.GetArrayLength()}[/]");
            }
        }

        if (metaUpdates.HasValue)
        {
            text.Add("");
            text.Add("[bold]🧩 Последние metaStateUpdates:[/]");
            if (metaUpdates.Value.TryGetProperty("inkFeatherChanges", out var featherChanges) && featherChanges.ValueKind == JsonValueKind.Object)
            {
                var add = GetInt(featherChanges, "add", 0);
                var spend = GetInt(featherChanges, "spend", 0);
                var reason = GetStr(featherChanges, "reason", "");
                text.Add($"  🪶 add={add}, spend={spend}" + (!string.IsNullOrWhiteSpace(reason) ? $" [dim]({Markup.Escape(reason)})[/]" : ""));
            }
            if (metaUpdates.Value.TryGetProperty("enlightenmentProgression", out var progression) && progression.ValueKind == JsonValueKind.Object)
            {
                var tier = GetStr(progression, "newTier", progression.TryGetProperty("newTier", out var nt) ? nt.ToString() : "");
                var exp = GetStr(progression, "experience", "");
                text.Add($"  ✨ Просветление: tier={Markup.Escape(tier)}, exp={Markup.Escape(exp)}");
            }
            if (metaUpdates.Value.TryGetProperty("soulRelicOperations", out var relicOps) && relicOps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in relicOps.EnumerateObject())
                {
                    var summary = prop.Value.ValueKind == JsonValueKind.Object
                        ? GetStr(prop.Value, "relicId", GetStr(prop.Value, "slot", prop.Value.ToString()))
                        : prop.Value.ToString();
                    text.Add($"  💎 {Markup.Escape(prop.Name)}: {Markup.Escape(summary)}");
                }
            }
            if (metaUpdates.Value.TryGetProperty("lifeTransitions", out var lifeTransitions) && lifeTransitions.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in lifeTransitions.EnumerateObject())
                    text.Add($"  🔄 {Markup.Escape(prop.Name)}");
            }
        }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("soul_info")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowGuardians()
    {
        while (true)
        {
            var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
            if (doc == null)
            {
                ShowEmptyPanel(_loc.T("guardians_info"), "Данные хранителей недоступны");
                return;
            }

            var root = doc.RootElement;
            var guardians = CollectGuardianDisplayEntries(root);
            if (guardians.Count == 0)
            {
                ShowEmptyPanel(_loc.T("guardians_info"), "Хранители ещё не найдены");
                return;
            }

            var currentAbodeId = "";
            var activeGuardianId = "";
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("chaosSeaNavigation", out var nav) && nav.ValueKind == JsonValueKind.Object)
                currentAbodeId = GetStr(nav, "currentAbodeId", "");
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
                activeGuardianId = GetStr(activeGuardian, "guardianId", "");

            var choices = guardians.Select(g =>
            {
                var name = GetStr(g, "name", "?");
                var domain = GetStr(g, "domain", "");
                int rep = 0;
                if (g.TryGetProperty("relationshipData", out var rd))
                    rep = GetInt(rd, "currentReputation", 0);
                else
                    rep = GetInt(g, "reputation", 0);
                var repTierTag = rep switch
                {
                    <= -51 => "Враждебный",
                    <= -21 => "Недружелюбный",
                    <= 49 => "Нейтральный",
                    <= 129 => "Дружелюбный",
                    <= 229 => "Преданный",
                    _ => "Легендарный"
                };

                // Abode info
                var abodeName = "";
                var abodeId = "";
                if (g.TryGetProperty("abode", out var ab) && ab.ValueKind == JsonValueKind.Object)
                {
                    abodeName = GetStr(ab, "name", "");
                    abodeId = GetStr(ab, "abodeId", "");
                }
                var isCurrent = !string.IsNullOrEmpty(abodeId) && abodeId == currentAbodeId;
                var locTag = isCurrent ? "ТУТ" : "";

                var domainRu = domain switch
                {
                    "Combat" => "Бой",
                    "Magic" => "Магия",
                    "Trade" => "Торговля",
                    "Social" => "Общение",
                    "Crafting" => "Ремесло",
                    "Survival" => "Выживание",
                    "Knowledge" => "Знания",
                    _ => domain
                };

                // Mood tag in list
                var moodTag = "";
                if (g.TryGetProperty("mood", out var moodEl) && moodEl.ValueKind == JsonValueKind.Object)
                {
                    var moodVal = GetStr(moodEl, "current", "");
                    var moodIcon = moodVal.ToLowerInvariant() switch
                    {
                        "welcoming" => "🤗", "contemplative" => "🤔", "energized" => "⚡",
                        "melancholic" => "😔", "irritated" => "😤", "proud" => "😊",
                        "suspicious" => "🧐", "playful" => "😏", "focused" => "🎯",
                        "nostalgic" => "🕰️", _ => ""
                    };
                    if (!string.IsNullOrEmpty(moodIcon)) moodTag = moodIcon;
                }

                return ConsoleLayout.PlainChoiceLabel(
                    $"🛡️ {name}",
                    domainRu,
                    $"♥ {rep}",
                    repTierTag,
                    string.IsNullOrEmpty(moodTag) ? "" : moodTag,
                    string.IsNullOrEmpty(abodeName) ? "" : $"🏛 {abodeName}",
                    locTag);
            }).ToList();

            // Navigation options
            choices.Add("🔍 Искать новую обитель (силой мысли)");
            choices.Add("← Назад");

            // Pending guardian creation notice
            string pendingNotice = "";
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("pendingGuardianCreation", out var pending) &&
                pending.ValueKind == JsonValueKind.Object)
            {
                var pendDesc = GetStr(pending, "description", "");
                pendingNotice = "  [yellow]⏳ Ожидается создание нового хранителя[/]" +
                    (!string.IsNullOrEmpty(pendDesc) ? $"\n  [dim]{Markup.Escape(pendDesc)}[/]" : "");
            }

            // Pending discovery notice
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("chaosSeaNavigation", out var nav2) && nav2.ValueKind == JsonValueKind.Object &&
                nav2.TryGetProperty("pendingDiscovery", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                var hint = GetStr(pd, "hint", "");
                var arrIn = GetInt(pd, "arrivalInTurns", 0);
                pendingNotice += $"\n  [cyan]🌊 Ощущаете далёкий зов...{(arrIn > 0 ? $" (через {arrIn} ход.)" : "")}[/]" +
                    (!string.IsNullOrEmpty(hint) ? $"\n  [dim italic]{Markup.Escape(hint)}[/]" : "");
            }

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🛡️ {_loc.T("guardians_info")} — Обители Моря Хаоса[/]" +
                    (string.IsNullOrEmpty(pendingNotice) ? "" : $"\n{pendingNotice}"))
                .PageSize(20)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            if (selected.Contains("Искать новую обитель"))
            {
                ShowSearchAbodePrompt();
                continue;
            }

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= guardians.Count) break;

            await ShowGuardianDetailPanel(guardians[selIdx], guardians, currentAbodeId, activeGuardianId);
        }
    }

    private void ShowSearchAbodePrompt()
    {
        var lines = new List<string>
        {
            "[bold cyan]🔍 Поиск новой обители[/]",
            "",
            "Вы сосредотачиваетесь, отправляя волну мысли сквозь бесконечность Моря Хаоса...",
            "",
            "[dim]Вы можете указать пожелание — какой домен или тип наставника вы ищете,",
            "или довериться судьбе и отправиться в неизведанное.[/]",
            "",
            "[yellow]Чтобы начать поиск, напишите в чат что-то вроде:[/]",
            "  [white]• \"Ищу хранителя боевых искусств\"[/]",
            "  [white]• \"Хочу найти мудрого наставника магии\"[/]",
            "  [white]• \"Отправляюсь на поиски неизвестной обители\"[/]",
            "  [white]• \"Ищу хранителя, который разбирается в ремесле\"[/]",
            "",
            "[dim]Результат зависит от броска d20 (Block 32_ext.1):[/]",
            "  [red]1-5:[/]   Ничего не найдено (можно повторить)",
            "  [yellow]6-12:[/]  Далёкий сигнал — прибытие на след. ходу",
            "  [green]13-18:[/] Обитель найдена! Мгновенное прибытие",
            "  [gold1]19-20:[/] Найден редкий Хранитель!",
        };

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌊 Поиск в Море Хаоса ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowAbodesNavigation()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (doc == null) { ShowEmptyPanel("Обители", "Данные хранителей недоступны"); return; }

        var root = doc.RootElement;
        var guardians = CollectGuardianDisplayEntries(root);

        // Filter to guardians with abodes
        var abodeGuardians = guardians
            .Where(g => g.TryGetProperty("abode", out var ab) && ab.ValueKind == JsonValueKind.Object)
            .ToList();

        if (abodeGuardians.Count == 0)
        {
            ShowEmptyPanel("Обители", "Обители ещё не открыты. Используйте /хранители для поиска.");
            return;
        }

        // Current abode
        var currentAbodeId = "";
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("chaosSeaNavigation", out var nav) && nav.ValueKind == JsonValueKind.Object)
            currentAbodeId = GetStr(nav, "currentAbodeId", "");

        while (true)
        {
            var choices = abodeGuardians.Select(g =>
            {
                var gName = GetStr(g, "name", "?");
                var ab = g.GetProperty("abode");
                var abName = GetStr(ab, "name", "???");
                var abId = GetStr(ab, "abodeId", "");
                var isCurrent = abId == currentAbodeId;
                var domain = GetStr(g, "domain", "");
                var domainRu = domain switch
                {
                    "Combat" => "Бой", "Magic" => "Магия", "Trade" => "Торговля",
                    "Social" => "Общение", "Crafting" => "Ремесло",
                    "Survival" => "Выживание", "Knowledge" => "Знания", _ => domain
                };
                return ConsoleLayout.PlainChoiceLabel(
                    $"🏛️ {abName}",
                    $"{domainRu} — {gName}",
                    isCurrent ? "ЗДЕСЬ" : "");
            }).ToList();
            choices.Add("🔍 Искать новую обитель");
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]🏛️ Обители Моря Хаоса[/]  [dim](выберите для перемещения)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected == "← Назад") break;
            if (selected.Contains("Искать новую обитель")) { ShowSearchAbodePrompt(); continue; }

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= abodeGuardians.Count) break;

            var selGuardian = abodeGuardians[selIdx];
            var selAbode = selGuardian.GetProperty("abode");
            var selAbodeId = GetStr(selAbode, "abodeId", "");
            var selAbodeName = GetStr(selAbode, "name", "???");
            var selGName = GetStr(selGuardian, "name", "?");

            if (selAbodeId == currentAbodeId)
            {
                AnsiConsole.MarkupLine($"[dim]Вы уже находитесь в обители «{Markup.Escape(selAbodeName)}».[/]");
                WaitForKey();
                continue;
            }

            _pendingGmAction =
                $"[CHAOS_SEA_TRAVEL] Душа выбирает перемещение в обитель '{selAbodeName}'" +
                $"{(string.IsNullOrWhiteSpace(selAbodeId) ? "" : $" (abodeId={selAbodeId})")}, связанную с Хранителем '{selGName}'. " +
                "Обработай само путешествие как полноценный ход: опиши прибытие, реакцию Хранителя и обнови chaosSeaNavigation.currentAbodeId в guardians.json.";

            AnsiConsole.MarkupLine($"[cyan]🌊 Переход в обитель «{Markup.Escape(selAbodeName)}» отправляется Мастеру Игры...[/]");
            return;
        }
    }

    private async Task ShowGuardianDetailPanel(JsonElement g, List<JsonElement>? allGuardians = null, string currentAbodeId = "", string activeGuardianId = "")
    {
        var name = GetStr(g, "name", "Неизвестный");
        var domain = GetStr(g, "domain", "");
        var content = new Grid().AddColumn(new GridColumn());
        content.AddRow(new Markup($"[bold cyan]🛡️ {Markup.Escape(name)}[/]"));

        var summaryTable = ConsoleLayout.CreateInfoTable();
        if (!string.IsNullOrEmpty(domain))
        {
            var domainRu = domain switch
            {
                "Combat" => "Бой",
                "Magic" => "Магия",
                "Trade" => "Торговля",
                "Social" => "Общение",
                "Crafting" => "Ремесло",
                "Survival" => "Выживание",
                "Knowledge" => "Знания",
                _ => domain
            };
            summaryTable.AddRow(new Markup("[yellow]Домен[/]"), new Markup($"[yellow]{Markup.Escape(domainRu)}[/] [dim]({Markup.Escape(domain)})[/]"));
        }

        var lines = new List<string>();
        void FlushLines()
        {
            if (lines.Count == 0)
                return;

            content.AddRow(new Markup(string.Join("\n", lines)));
            lines.Clear();
        }

        // ── Abode info ──
        if (g.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
        {
            var abodeName = GetStr(abode, "name", "");
            var abodeDesc = GetStr(abode, "description", "");
            var atmo = GetStr(abode, "atmosphere", "");
            var abodeId = GetStr(abode, "abodeId", "");
            var isCurrent = !string.IsNullOrEmpty(abodeId) && abodeId == currentAbodeId;

            if (!string.IsNullOrEmpty(abodeName))
            {
                var hereTag = isCurrent ? " [bold green]● Вы здесь[/]" : "";
                summaryTable.AddRow(new Markup("[white]Обитель[/]"), new Markup($"[white]{Markup.Escape(abodeName)}[/]{hereTag}"));
            }
            if (!string.IsNullOrEmpty(abodeDesc))
                lines.Add($"  [dim italic]{Markup.Escape(abodeDesc)}[/]");
            if (!string.IsNullOrEmpty(atmo))
            {
                var atmoRu = atmo switch
                {
                    "Welcoming" => "Гостеприимная",
                    "Imposing" => "Величественная",
                    "Mysterious" => "Загадочная",
                    "Chaotic" => "Хаотичная",
                    "Serene" => "Безмятежная",
                    "Austere" => "Аскетичная",
                    "Opulent" => "Роскошная",
                    _ => atmo
                };
                summaryTable.AddRow(new Markup("[dim]Атмосфера[/]"), new Markup($"[dim]{Markup.Escape(atmoRu)}[/]"));
            }
        }

        // Personality
        if (g.TryGetProperty("personalityProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            var archetype = GetStr(pp, "archetype", "");
            var speech = GetStr(pp, "speechPattern", "");
            if (!string.IsNullOrEmpty(archetype))
                summaryTable.AddRow(new Markup("[mediumpurple2]Архетип[/]"), new Markup($"[mediumpurple2]{Markup.Escape(archetype)}[/]"));
            if (!string.IsNullOrEmpty(speech))
                summaryTable.AddRow(new Markup("[dim]Стиль речи[/]"), new Markup($"[dim]{Markup.Escape(speech)}[/]"));
            if (pp.TryGetProperty("coreValues", out var cv) && cv.ValueKind == JsonValueKind.Array)
            {
                var vals = new List<string>();
                foreach (var v in cv.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.String) vals.Add(v.GetString() ?? "");
                if (vals.Count > 0)
                    summaryTable.AddRow(new Markup("[white]Ценности[/]"), new Markup($"[white]{Markup.Escape(string.Join(", ", vals))}[/]"));
            }
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        // Reputation
        int rep = 0;
        if (g.TryGetProperty("relationshipData", out var rd) && rd.ValueKind == JsonValueKind.Object)
        {
            rep = GetInt(rd, "currentReputation", 0);
            var repTierLabel = rep switch
            {
                <= -51 => "[bold red]Враждебный[/]",
                <= -21 => "[red]Недружелюбный[/]",
                <= 49 => "[grey]Нейтральный[/]",
                <= 129 => "[green]Дружелюбный[/]",
                <= 229 => "[cyan]Преданный[/]",
                _ => "[gold1]Легендарный[/]"
            };
            FlushLines();
            content.AddRow(new Markup(""));
            // Reputation bar (-100..+300 mapped to 0..20)
            var repNorm = Math.Clamp((rep + 100) * 20 / 400, 0, 20);
            var repBarColor = rep >= 230 ? "gold1" : rep >= 130 ? "cyan" : rep >= 50 ? "green" : rep >= -20 ? "grey" : rep >= -50 ? "orange1" : "red";
            var repTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 16, barWidth: 24, valueWidth: 8);
            repTable.AddRow(
                new Markup("[bold]♥ Репутация[/]"),
                new Markup(ConsoleLayout.CreateBar(repNorm, 20, repBarColor)),
                new Markup($"[{(rep >= 0 ? "green" : "red")}]{rep}[/]/300"),
                new Markup(repTierLabel));
            content.AddRow(repTable);

            var repLegend = ConsoleLayout.CreateInfoTable(labelWidth: 14);
            repLegend.AddRow(new Markup("[dim]-100..-51[/]"), new Markup("[red]Враждебный[/]"));
            repLegend.AddRow(new Markup("[dim]-50..-21[/]"), new Markup("[orange1]Недружелюбный[/]"));
            repLegend.AddRow(new Markup("[dim]-20..49[/]"), new Markup("[grey]Нейтральный[/]"));
            repLegend.AddRow(new Markup("[dim]50..129[/]"), new Markup("[green]Дружелюбный[/]"));
            repLegend.AddRow(new Markup("[dim]130..229[/]"), new Markup("[cyan]Преданный[/]"));
            repLegend.AddRow(new Markup("[dim]230..300[/]"), new Markup("[gold1]Легендарный[/]"));
            content.AddRow(new Markup("  [dim]Диапазоны репутации:[/]"));
            content.AddRow(repLegend);

            var lastInteraction = GetStr(rd, "lastInteraction", "");
            if (!string.IsNullOrEmpty(lastInteraction) && lastInteraction.Length >= 10)
                lines.Add($"  [dim]Последняя встреча: {Markup.Escape(lastInteraction[..10])}[/]");

            if (rd.TryGetProperty("reputationHistory", out var rh) && rh.ValueKind == JsonValueKind.Array)
            {
                lines.Add($"  [dim]История ({rh.GetArrayLength()}):[/]");
                foreach (var entry in rh.EnumerateArray())
                {
                    var change = GetInt(entry, "change", 0);
                    var reason = GetStr(entry, "reason", "");
                    var ts = GetStr(entry, "timestamp", "");
                    var changeStr = change > 0 ? $"[green]+{change}[/]" : change < 0 ? $"[red]{change}[/]" : "[dim]±0[/]";
                    var timeStr = "";
                    if (!string.IsNullOrEmpty(ts) && ts.Length >= 10) timeStr = $"[dim]{Markup.Escape(ts[..10])}[/] ";
                    lines.Add($"    {timeStr}{changeStr} {Markup.Escape(reason)}");
                }
            }
        }
        else
        {
            rep = GetInt(g, "reputation", 0);
            lines.Add($"  ♥ Репутация: [{(rep >= 0 ? "green" : "red")}]{rep}[/]");
        }

        // Active quests
        if (g.TryGetProperty("questManagement", out var qm) && qm.ValueKind == JsonValueKind.Object)
        {
            if ((qm.TryGetProperty("activeQuests", out var aq) || qm.TryGetProperty("availableQuests", out aq)) && aq.ValueKind == JsonValueKind.Array && aq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]📜 Активные задания:[/]");
                foreach (var q in aq.EnumerateArray())
                {
                    var qName = GetStr(q, "name", "?");
                    var qDesc = GetStr(q, "description", "");
                    var qDiff = GetStr(q, "difficulty", "");
                    var qStatus = GetStr(q, "status", "");
                    var qTarget = GetStr(q, "targetWorld", "");
                    lines.Add($"    📋 [yellow]{Markup.Escape(qName)}[/]" +
                        (!string.IsNullOrEmpty(qDiff) ? $" [dim]({Markup.Escape(qDiff)})[/]" : "") +
                        (!string.IsNullOrEmpty(qStatus) ? $" [{(qStatus.ToLower().Contains("progress") ? "cyan" : "white")}]{Markup.Escape(qStatus)}[/]" : ""));
                    if (!string.IsNullOrEmpty(qDesc))
                        lines.Add($"       [white]{Markup.Escape(qDesc)}[/]");
                    if (!string.IsNullOrEmpty(qTarget))
                        lines.Add($"       🌍 Мир: [cyan]{Markup.Escape(qTarget)}[/]");
                    if (q.TryGetProperty("rewards", out var rew) && rew.ValueKind == JsonValueKind.Object)
                    {
                        var rewParts = new List<string>();
                        foreach (var rp in rew.EnumerateObject())
                        {
                            var val = rp.Value.ValueKind == JsonValueKind.Number ? rp.Value.ToString() : rp.Value.GetRawText();
                            rewParts.Add($"{rp.Name}: {val}");
                        }
                        if (rewParts.Count > 0)
                            lines.Add($"       🎁 Награды: [green]{Markup.Escape(string.Join(", ", rewParts))}[/]");
                    }
                }
            }

            if (qm.TryGetProperty("completedQuests", out var cq) && cq.ValueKind == JsonValueKind.Array && cq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]✅ Выполненные задания:[/]");
                foreach (var q in cq.EnumerateArray())
                {
                    var qName = GetStr(q, "name", "?");
                    var qResult = GetStr(q, "result", "");
                    var qDate = GetStr(q, "completionDate", "");
                    var resultColor = qResult.ToLower().Contains("success") ? "green" : "white";
                    var dateStr = !string.IsNullOrEmpty(qDate) && qDate.Length >= 10 ? $" [dim]{Markup.Escape(qDate[..10])}[/]" : "";
                    lines.Add($"    ✓ [dim]{Markup.Escape(qName)}[/] [{resultColor}]{Markup.Escape(qResult)}[/]{dateStr}");
                }
            }
        }

        // Gacha system
        var hasGachaSystem = g.TryGetProperty("gachaSystem", out var gs) && gs.ValueKind == JsonValueKind.Object;
        lines.Add("");
        lines.Add("  [bold]🎰 Система гача:[/]");
        var chargesPerReturn = hasGachaSystem && gs.TryGetProperty("chargesPerReturn", out var cpr) && cpr.ValueKind == JsonValueKind.Number && cpr.TryGetInt32(out var parsedCharges)
            ? parsedCharges
            : GuardianGachaChargeRules.GetChargesPerReturnForReputation(rep);
        var chargesUsedThisReturn = hasGachaSystem && gs.TryGetProperty("chargesUsedThisReturn", out var cur) && cur.ValueKind == JsonValueKind.Number && cur.TryGetInt32(out var parsedUsed)
            ? GuardianGachaChargeRules.ClampUsedCharges(parsedUsed, chargesPerReturn)
            : 0;
        var remainingCharges = Math.Max(0, chargesPerReturn - chargesUsedThisReturn);

        if (chargesPerReturn <= 0)
        {
            lines.Add("    [red]Гача через этого Хранителя сейчас заблокирована вашей репутацией.[/]");
        }
        else
        {
            lines.Add($"    Осталось попыток в этом возвращении: [yellow]{remainingCharges}[/]/[white]{chargesPerReturn}[/]");
            if (remainingCharges <= 0)
                lines.Add("    [yellow]Лимит гачи у этого Хранителя исчерпан до следующего возвращения из смертной жизни.[/]");
        }

        if (hasGachaSystem && gs.TryGetProperty("gachaHistory", out var gh) && gh.ValueKind == JsonValueKind.Array && gh.GetArrayLength() > 0)
        {
            lines.Add("    [dim]История призывов:[/]");
            foreach (var h in gh.EnumerateArray())
            {
                var relicId = GetStr(h, "relicId", "?");
                var cost = GetStr(h, "costInFeathers", GetStr(h, "cost", "?"));
                var rarity = GetStr(h, "finalRarity", "");
                var hTs = GetStr(h, "timestamp", "");
                var timeStr = !string.IsNullOrEmpty(hTs) && hTs.Length >= 10 ? $"[dim]{Markup.Escape(hTs[..10])}[/] " : "";
                var rarityTag = string.IsNullOrWhiteSpace(rarity) ? "" : $" [dim](редкость: {Markup.Escape(rarity)})[/]";
                lines.Add($"      {timeStr}💎 {Markup.Escape(relicId)} [dim](стоимость: {Markup.Escape(cost)})[/]{rarityTag}");
            }
        }

        // ── Social Profile (Block 32_ext.3) ──
        if (g.TryGetProperty("socialProfile", out var sp) && sp.ValueKind == JsonValueKind.Object)
        {
            FlushLines();
            content.AddRow(new Markup(""));
            content.AddRow(new Markup("  [bold magenta1]🧠 Социальный профиль:[/]"));

            var jealousy = GetInt(sp, "jealousyFactor", -1);
            var curiosity = GetInt(sp, "curiosityFactor", -1);
            var competitive = GetInt(sp, "competitiveFactor", -1);
            var generosity = GetInt(sp, "generosityFactor", -1);
            var isolationist = GetInt(sp, "isolationistTendency", -1);
            var socialTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 12, valueWidth: 4);

            void AddSocialBar(string label, string icon, int val, string lowDesc, string highDesc)
            {
                if (val < 0) return;
                var barW = 10;
                var filled = Math.Clamp(val * barW / 100, 0, barW);
                var color = val >= 70 ? "red" : val >= 40 ? "yellow" : "green";
                var desc2 = val >= 70 ? highDesc : val <= 30 ? lowDesc : "";
                var description = !string.IsNullOrEmpty(desc2) ? $"[dim]({Markup.Escape(desc2)})[/]" : new string(' ', 0);
                socialTable.AddRow(
                    new Markup($"{icon} {Markup.Escape(label)}"),
                    new Markup(ConsoleLayout.CreateBar(filled, barW, color)),
                    new Markup($"[{color}]{val}[/]"),
                    new Markup(description));
            }

            AddSocialBar("Ревность", "💚", jealousy, "не ревнует", "собственник");
            AddSocialBar("Любопытство", "🔍", curiosity, "безразличен", "жаждет информации");
            AddSocialBar("Конкуренция", "⚔", competitive, "спокоен", "агрессивно соперничает");
            AddSocialBar("Щедрость", "🎁", generosity, "расчётлив", "щедро одаривает");
            AddSocialBar("Изоляция", "🏔", isolationist, "социален", "хочет быть единственным");

            if (socialTable.Rows.Count > 0)
                content.AddRow(socialTable);
        }

        // ── Inter-Guardian Relationships (Block 32_ext.3) ──
        if (g.TryGetProperty("guardianRelationships", out var gRels) && gRels.ValueKind == JsonValueKind.Array && gRels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold steelblue1]🤝 Отношения с другими Хранителями:[/]");
            foreach (var rel in gRels.EnumerateArray())
            {
                var tgtName = GetStr(rel, "targetName", GetStr(rel, "targetGuardianId", "?"));
                // Try to resolve name from allGuardians
                if (allGuardians != null)
                {
                    var tgtId = GetStr(rel, "targetGuardianId", "");
                    foreach (var other in allGuardians)
                    {
                        if (GetStr(other, "guardianId", "") == tgtId)
                        {
                            tgtName = GetStr(other, "name", tgtName);
                            break;
                        }
                    }
                }
                var attitude = GetStr(rel, "attitude", "");
                var reason = GetStr(rel, "reason", "");
                var (attIcon, attColor, attRu) = attitude.ToLowerInvariant() switch
                {
                    "ally" => ("🤝", "green", "Союзник"),
                    "neutral" => ("😐", "grey", "Нейтрален"),
                    "curious" => ("🔍", "cyan", "Любопытствует"),
                    "competitive" => ("⚔", "yellow", "Конкурент"),
                    "rival" => ("⚔", "orange1", "Соперник"),
                    "enemy" => ("💀", "red", "Враг"),
                    _ => ("👤", "white", attitude)
                };
                lines.Add($"    {attIcon} [{attColor}]{Markup.Escape(tgtName)}[/] — [{attColor}]{Markup.Escape(attRu)}[/]");
                if (!string.IsNullOrEmpty(reason))
                    lines.Add($"      [dim italic]{Markup.Escape(reason)}[/]");
            }
        }

        // ── Reputation tier gate info ──
        if (rep >= 0)
        {
            var (nextTierName, nextTierRep) = rep switch
            {
                < 50 => ("Дружелюбный", 50),
                < 130 => ("Преданный", 130),
                < 230 => ("Легендарный", 230),
                _ => ("", 0)
            };

            if (!string.IsNullOrEmpty(nextTierName))
            {
                var repLeft = Math.Max(0, nextTierRep - rep);
                lines.Add("");
                lines.Add($"  [dim]→ До ранга [white]{nextTierName}[/]: {repLeft} репутации");
            }
        }

        // ── Mood ──
        if (g.TryGetProperty("mood", out var moodObj) && moodObj.ValueKind == JsonValueKind.Object)
        {
            var moodCurrent = GetStr(moodObj, "current", "");
            var moodIntensity = GetInt(moodObj, "intensity", 0);
            var moodReason = GetStr(moodObj, "reason", "");
            if (!string.IsNullOrEmpty(moodCurrent))
            {
                var (moodIcon, moodColor, moodRu) = moodCurrent.ToLowerInvariant() switch
                {
                    "welcoming" => ("🤗", "green", "Радушное"),
                    "contemplative" => ("🤔", "steelblue1", "Задумчивое"),
                    "energized" => ("⚡", "yellow", "Воодушевлённое"),
                    "melancholic" => ("😔", "grey", "Меланхоличное"),
                    "irritated" => ("😤", "red", "Раздражённое"),
                    "proud" => ("😊", "gold1", "Гордость"),
                    "suspicious" => ("🧐", "orange1", "Подозрительное"),
                    "playful" => ("😏", "mediumpurple2", "Игривое"),
                    "focused" => ("🎯", "cyan", "Сосредоточенное"),
                    "nostalgic" => ("🕰️", "wheat1", "Ностальгическое"),
                    _ => ("💭", "white", moodCurrent)
                };
                lines.Add("");
                var barW = 10;
                var filled = Math.Clamp(moodIntensity * barW / 100, 0, barW);
                var moodSince = GetInt(moodObj, "since", 0);
                var sinceTag = moodSince > 0 ? $" [dim](с хода {moodSince})[/]" : "";
                lines.Add($"  {moodIcon} Настроение: [{moodColor}]{Markup.Escape(moodRu)}[/]  [{moodColor}]{new string('█', filled)}[/][dim]{new string('░', barW - filled)}[/] [dim]{moodIntensity}%[/]{sinceTag}");
                if (!string.IsNullOrEmpty(moodReason))
                    lines.Add($"    [dim italic]{Markup.Escape(moodReason)}[/]");
            }
        }

        // ── Current Project ──
        if (g.TryGetProperty("currentProject", out var proj) && proj.ValueKind == JsonValueKind.Object)
        {
                var projName = GetStr(proj, "projectName", GetStr(proj, "name", ""));
                var projDesc = GetStr(proj, "description", "");
                var projProgress = GetInt(proj, "progressPercent", 0);
                var canAssist = proj.TryGetProperty("playerCanAssist", out var pa) && pa.ValueKind == JsonValueKind.True;
                var assistDesc = GetStr(proj, "assistDescription", "");

            if (!string.IsNullOrEmpty(projName))
            {
                lines.Add("");
                lines.Add("  [bold]🔬 Текущий проект:[/]");
                lines.Add($"    [white]{Markup.Escape(projName)}[/]");
                if (!string.IsNullOrEmpty(projDesc))
                    lines.Add($"    [dim]{Markup.Escape(projDesc)}[/]");

                var pBarW = 15;
                var pFilled = Math.Clamp(projProgress * pBarW / 100, 0, pBarW);
                var pColor = projProgress >= 80 ? "green" : projProgress >= 40 ? "yellow" : "cyan";
                var etaTurn = GetInt(proj, "estimatedCompletionTurn", 0);
                var etaTurnsLeft = GetInt(proj, "estimatedTurnsLeft", 0);
                var etaTag = etaTurn > 0
                    ? $"  [dim](завершение ~ход {etaTurn})[/]"
                    : etaTurnsLeft > 0
                        ? $"  [dim](ещё ~{etaTurnsLeft} ход.)[/]"
                        : "";
                lines.Add($"    Прогресс: [{pColor}]{new string('━', pFilled)}[/][dim grey]{new string('┄', pBarW - pFilled)}[/] [{pColor}]{projProgress}%[/]{etaTag}");

                if (canAssist && !string.IsNullOrEmpty(assistDesc))
                    lines.Add($"    [mediumpurple2]🤝 Можно помочь:[/] [italic]{Markup.Escape(assistDesc)}[/]");
            }
        }

        // ── Completed Projects (last 3) ──
        if (g.TryGetProperty("completedProjects", out var cProjects) && cProjects.ValueKind == JsonValueKind.Array && cProjects.GetArrayLength() > 0)
        {
            if (cProjects.GetArrayLength() > 0)
            {
                lines.Add($"    [dim]Завершённые проекты ({cProjects.GetArrayLength()}):[/]");
                foreach (var cp in cProjects.EnumerateArray())
                {
                    var cpName = GetStr(cp, "projectName", "?");
                    var cpOutcome = GetStr(cp, "outcome", "");
                    var cpTurn = GetStr(cp, "completionTurn", "");
                    var playerHelped = cp.TryGetProperty("playerInvolved", out var pi) && pi.ValueKind == JsonValueKind.True;
                    var turnStr = !string.IsNullOrEmpty(cpTurn) ? $"[dim](ход {Markup.Escape(cpTurn)})[/] " : "";
                    var helpTag = playerHelped ? " [cyan]★ вы помогали[/]" : "";
                    lines.Add($"      ✓ {turnStr}[white]{Markup.Escape(cpName)}[/]{helpTag}");
                    if (!string.IsNullOrEmpty(cpOutcome))
                        lines.Add($"        [dim italic]{Markup.Escape(cpOutcome)}[/]");
                }
            }
        }

        // ── Lore Fragments ──
        if (g.TryGetProperty("loreFragments", out var lore) && lore.ValueKind == JsonValueKind.Array && lore.GetArrayLength() > 0)
        {
            var unlockedLore = new List<JsonElement>();
            var lockedLore = new List<JsonElement>();
            foreach (var frag in lore.EnumerateArray())
            {
                var isUnlocked = frag.TryGetProperty("isUnlocked", out var ul) && ul.ValueKind == JsonValueKind.True;
                if (isUnlocked)
                    unlockedLore.Add(frag);
                else
                    lockedLore.Add(frag);
            }

            if (unlockedLore.Count > 0 || lockedLore.Count > 0)
            {
                lines.Add("");
                lines.Add($"  [bold]📜 Знания Хранителя[/] [dim]({unlockedLore.Count} открыто, {lockedLore.Count} скрыто)[/]");
                foreach (var frag in unlockedLore)
                {
                    var lTitle = GetStr(frag, "title", "???");
                    var lContent = GetStr(frag, "content", "");
                    var lCategory = GetStr(frag, "category", "");
                    var catIcon = lCategory switch
                    {
                        "personal_history" => "👤",
                        "cosmic_secret" => "🌌",
                        "domain_mastery" => "📚",
                        "lost_world" => "🌍",
                        "other_guardians" => "🛡️",
                        "soul_mechanics" => "✨",
                        _ => "📖"
                    };
                    lines.Add($"    {catIcon} [yellow]{Markup.Escape(lTitle)}[/]");
                    if (!string.IsNullOrEmpty(lContent))
                        lines.Add($"      [dim italic]{Markup.Escape(lContent)}[/]");
                }
                foreach (var frag in lockedLore)
                {
                    var reqRep = GetInt(frag, "requiredReputation", 0);
                    lines.Add($"    🔒 [dim]??? — требуется репутация {reqRep}+[/]");
                }
            }
        }

        // ── Musings (last 5) ──
        if (g.TryGetProperty("musings", out var musings) && musings.ValueKind == JsonValueKind.Array && musings.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]💭 Размышления[/] [dim]({musings.GetArrayLength()} записей)[/]");
            foreach (var m in musings.EnumerateArray())
            {
                var mTurn = GetStr(m, "turn", "");
                var mThought = GetStr(m, "thought", GetStr(m, "text", ""));
                var mTopic = GetStr(m, "topic", "");
                var mMood = GetStr(m, "mood", "");
                var topicIcon = mTopic switch
                {
                    "soul_assessment" => "👁️",
                    "domain_insight" => "📚",
                    "guardian_politics" => "🏛️",
                    "chaos_sea" => "🌊",
                    "personal_reflection" => "🪞",
                    "quest_planning" => "📋",
                    _ => "💭"
                };
                var turnTag = !string.IsNullOrEmpty(mTurn) ? $"[dim]#{Markup.Escape(mTurn)}[/] " : "";
                lines.Add($"    {topicIcon} {turnTag}[italic]{Markup.Escape(mThought)}[/]");
                if (!string.IsNullOrEmpty(mMood))
                    lines.Add($"      [dim]— {Markup.Escape(mMood)}[/]");
            }
        }

        // Description
        var desc = GetStr(g, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📖 {Markup.Escape(desc)}[/]");
        }

        var isActiveGuardian = string.Equals(GetStr(g, "guardianId", ""), activeGuardianId, StringComparison.OrdinalIgnoreCase);
        var tradeAvailableHere = GuardianTradeAvailableHere(g, currentAbodeId);
        var tradeBlockedByReputation = rep <= -51;
        lines.Add("");
        lines.Add("  [bold]🛒 Локальная торговля:[/]");
        if (!tradeAvailableHere)
            lines.Add("    [dim]Доступна только в текущей обители Хранителя.[/]");
        else if (!isActiveGuardian)
            lines.Add("    [dim]Доступна только у текущего активного Хранителя в этой обители.[/]");
        else if (tradeBlockedByReputation)
            lines.Add("    [red]Хранитель отказывается торговать из-за вашей репутации.[/]");
        else
            lines.Add("    [white]Доступна: 4 локальных слота, обновление после нового возвращения из смертной жизни.[/]");

        FlushLines();

        AnsiConsole.Write(new Panel(content)
        {
            Header = new PanelHeader($" 🛡️ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        await ShowGuardianDetailActions(g, name, currentAbodeId, activeGuardianId);
    }

    private static bool GuardianTradeAvailableHere(JsonElement guardian, string currentAbodeId)
    {
        if (string.IsNullOrWhiteSpace(currentAbodeId))
            return false;

        if (!guardian.TryGetProperty("abode", out var abode) || abode.ValueKind != JsonValueKind.Object)
            return false;

        var abodeId = GetStr(abode, "abodeId", "");
        return !string.IsNullOrWhiteSpace(abodeId) &&
               string.Equals(abodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ShowGuardianDetailActions(JsonElement guardian, string guardianName, string currentAbodeId, string activeGuardianId)
    {
        var imagePrompt = GetStr(guardian, "image_prompt", "");
        var guardianImageKey = GetStr(guardian, "guardianId", guardianName);
        var abodeImagePrompt = "";
        var abodeImageKey = "";
        if (guardian.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
        {
            abodeImagePrompt = GetStr(abode, "image_prompt", "");
            abodeImageKey = GetStr(abode, "abodeId", GetStr(abode, "name", $"{guardianImageKey}_abode"));
        }
        var tradeAvailable = GuardianTradeAvailableHere(guardian, currentAbodeId) &&
                             string.Equals(GetStr(guardian, "guardianId", ""), activeGuardianId, StringComparison.OrdinalIgnoreCase) &&
                             GetInt(guardian.TryGetProperty("relationshipData", out var rd) ? rd : guardian, "currentReputation", GetInt(guardian, "reputation", 0)) > -51;

        var hasImageSupport = _imageService != null && !string.IsNullOrWhiteSpace(imagePrompt);
        var hasAbodeImageSupport = _imageService != null && !string.IsNullOrWhiteSpace(abodeImagePrompt);
        if (!tradeAvailable && !hasImageSupport && !hasAbodeImageSupport)
        {
            WaitForKey();
            return;
        }

        while (true)
        {
            var actions = new List<string>();
            if (tradeAvailable)
                actions.Add("🛒 Торговать");

            if (hasImageSupport)
            {
                var hasImage = _imageService!.EntityImageExists("guardian", guardianImageKey);
                actions.Add("🖼 Показать изображение хранителя");
                if (hasImage)
                    actions.Add("♻ Пересоздать изображение хранителя");
            }

            if (hasAbodeImageSupport)
            {
                var hasAbodeImage = _imageService!.EntityImageExists("abode", abodeImageKey);
                actions.Add("🏛 Показать изображение обители");
                if (hasAbodeImage)
                    actions.Add("🏛 ♻ Пересоздать изображение обители");
            }

            actions.Add("← Назад");

            var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actions));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Торговать"))
            {
                var guardianId = GetStr(guardian, "guardianId", "");
                if (!string.IsNullOrWhiteSpace(guardianId))
                    await ShowGuardianTradePanel(guardianId);
                return;
            }

            if (action.Contains("обители", StringComparison.OrdinalIgnoreCase))
            {
                var abodeImageExists = _imageService!.EntityImageExists("abode", abodeImageKey);
                if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && abodeImageExists)
                    await RegenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey);
                else
                    await _imageService.ShowOrGenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }

            if (!hasImageSupport)
                continue;

            var imageExists = _imageService!.EntityImageExists("guardian", guardianImageKey);
            if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && imageExists)
            {
                await RegenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Показать", StringComparison.OrdinalIgnoreCase))
            {
                await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }
        }
    }

    private static string FormatAchievementRewardText(string rewardType, string rewardValue)
    {
        var normalizedType = rewardType.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType) && string.IsNullOrWhiteSpace(rewardValue))
            return "не указана";

        return normalizedType switch
        {
            "inkfeathers" => string.IsNullOrWhiteSpace(rewardValue) ? "Чернильные перья" : $"{rewardValue} Чернильных Перьев",
            "soulxp" => string.IsNullOrWhiteSpace(rewardValue) ? "Опыт души" : $"{rewardValue} опыта души",
            "title" => string.IsNullOrWhiteSpace(rewardValue) ? "Титул" : $"Титул: {rewardValue}",
            "none" => "нет",
            _ when string.IsNullOrWhiteSpace(rewardValue) => rewardType,
            _ => $"{rewardType}: {rewardValue}"
        };
    }

    private async Task ShowGuardianTradePanel(string guardianId)
    {
        if (_guardianTradeService == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Сервис торговли недоступен.[/]");
            WaitForKey();
            return;
        }

        while (true)
        {
            var view = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation);
            if (view == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
                WaitForKey();
                return;
            }

            if (view.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var feathers = await ReadInkFeathersBalance();
            var headerLines = new List<string>
            {
                $"[bold cyan]🛒 Торговля с Хранителем {Markup.Escape(view.GuardianName)}[/]",
                $"[dim]Домен: {Markup.Escape(view.DomainDisplay)} • Репутация: {view.CurrentReputation} ({Markup.Escape(view.ReputationTierLabel)})[/]",
                $"[dim]Чернильные Перья: {feathers}[/]",
                "[dim]Витрина обновляется после нового возвращения из смертной жизни.[/]"
            };

            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", headerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите раздел:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("🛍 Купить реликвии", "💰 Продать реликвии", "← Назад"));

            if (choice.Contains("Назад"))
                return;

            if (choice.Contains("Купить"))
            {
                await ShowGuardianBuyMenu(guardianId);
                await _stateManager.RefreshGameStateAsync();
                AnsiConsole.Clear();
                continue;
            }

            if (choice.Contains("Продать"))
            {
                await ShowGuardianSellMenu(guardianId);
                await _stateManager.RefreshGameStateAsync();
                AnsiConsole.Clear();
            }
        }
    }

    private async Task ShowGuardianBuyMenu(string guardianId)
    {
        if (_guardianTradeService == null)
            return;

        while (true)
        {
            var refreshedView = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation);
            if (refreshedView == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
                WaitForKey();
                return;
            }

            if (refreshedView.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(refreshedView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var feathers = await ReadInkFeathersBalance();
            var choices = refreshedView.Offers.Select(offer =>
            {
                var soldTag = offer.SoldOut ? "РАСПРОДАНО" : "";
                return ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
                    soldTag);
            }).ToList();
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Покупка реликвий[/] [dim](перья: {feathers})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(10)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= refreshedView.Offers.Count)
                return;

            var offer = refreshedView.Offers[selectedIndex];
            var canBuy = !offer.SoldOut && feathers >= offer.PriceInFeathers;
            var decision = ShowGuardianTradeBuyPreview(offer, feathers, canBuy);
            if (decision != GuardianTradeBuyDecision.Buy)
                continue;

            var result = await _guardianTradeService.BuyAsync(guardianId, offer.SlotId, _stateManager.CurrentState.Incarnation);
            AnsiConsole.MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private enum GuardianTradeBuyDecision
    {
        Back,
        Buy
    }

    private GuardianTradeBuyDecision ShowGuardianTradeBuyPreview(Services.GuardianTradeService.GuardianTradeOffer offer, int currentFeathers, bool canBuy)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null);
        lines.Insert(1, $"  💰 Цена: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🛍️ Источник витрины: [cyan]{Markup.Escape(GuardianTradeDisplayDomain(offer.DomainTag))}[/]");
        lines.Insert(3, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");

        if (offer.SoldOut)
        {
            lines.Insert(4, "  [red]Статус витрины: слот уже распродан в текущем возвращении.[/]");
        }
        else if (currentFeathers < offer.PriceInFeathers)
        {
            lines.Insert(4, "  [yellow]Статус покупки: пока не хватает Чернильных Перьев для покупки.[/]");
        }

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Торговая реликвия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var actions = new List<string>();
        if (canBuy)
            actions.Add("🛍 Купить");
        actions.Add("← Назад к витрине");

        var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(actions));

        return action.Contains("Купить", StringComparison.OrdinalIgnoreCase)
            ? GuardianTradeBuyDecision.Buy
            : GuardianTradeBuyDecision.Back;
    }

    private async Task ShowGuardianSellMenu(string guardianId)
    {
        if (_guardianTradeService == null)
            return;

        while (true)
        {
            var tradeView = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation);
            if (tradeView == null)
            {
                AnsiConsole.MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
                WaitForKey();
                return;
            }

            if (tradeView.TradeBlocked)
            {
                AnsiConsole.MarkupLine($"[red]⛔ {Markup.Escape(tradeView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var offers = await _guardianTradeService.GetSellableRelicsAsync(guardianId);
            if (offers.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]В хранилище нет реликвий, доступных для продажи.[/]");
                WaitForKey();
                return;
            }

            var choices = offers.Select(offer =>
                ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}"))
                .ToList();
            choices.Add("← Назад");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Продажа реликвий[/] [dim](только из хранилища)[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(15)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= offers.Count)
                return;

            var offer = offers[selectedIndex];
            var confirm = AnsiConsole.Confirm($"Продать «{offer.Name}» за {offer.PriceInFeathers} 🪶?", false);
            if (!confirm)
                continue;

            var result = await _guardianTradeService.SellAsync(guardianId, offer.RelicId);
            AnsiConsole.MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private async Task ShowSoulRelics()
    {
        var isChaosSea = _stateManager.CurrentState.IsInChaosSea;

        while (true)
        {
            // Re-read file each iteration to see updates after equip/unequip
            var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
            if (doc == null)
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Данные реликвий недоступны");
                return;
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("soulRelics", out var relics) ||
                (relics.ValueKind != JsonValueKind.Object && relics.ValueKind != JsonValueKind.Array))
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Реликвии души ещё не найдены");
                return;
            }

            var allRelics = new List<(string Id, string Name, string Status, JsonElement Data, int IndexInArray)>();
            int idx = 0;

            if (relics.ValueKind == JsonValueKind.Array)
            {
                // Flat array format — determine equipped status from gameplayStatus.equipped
                foreach (var r in relics.EnumerateArray())
                {
                    var isEquipped = false;
                    if (r.TryGetProperty("gameplayStatus", out var gs) && gs.TryGetProperty("equipped", out var eq))
                        isEquipped = eq.ValueKind == JsonValueKind.True;
                    allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), isEquipped ? "equipped" : "stored", r, idx));
                    idx++;
                }
            }
            else
            {
                // Object format with equipped/stored arrays
                if (relics.TryGetProperty("equipped", out var equipped) && equipped.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in equipped.EnumerateArray())
                    {
                        allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), "equipped", r, idx));
                        idx++;
                    }
                }
                idx = 0;
                if (relics.TryGetProperty("stored", out var stored) && stored.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in stored.EnumerateArray())
                    {
                        allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), "stored", r, idx));
                        idx++;
                    }
                }
            }

            if (allRelics.Count == 0)
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Реликвии души ещё не найдены");
                return;
            }

            var choices = MakeUniqueChoiceLabels(allRelics.Select(r =>
            {
                var statusTag = r.Status == "equipped" ? "[green]⚔ экипировано[/]" : "[dim]📦 хранилище[/]";
                var slotStr = "";
                if (r.Status == "equipped")
                {
                    var s = GetStr(r.Data, "slot", "");
                    if (string.IsNullOrEmpty(s) && r.Data.TryGetProperty("equipmentData", out var ed))
                        s = GetStr(ed, "equipSlot", "");
                    if (string.IsNullOrEmpty(s) && r.Data.TryGetProperty("gameplayStatus", out var gs))
                        s = GetStr(gs, "currentSlot", "");
                    if (!string.IsNullOrEmpty(s)) slotStr = $" [[{Markup.Escape(s)}]]";
                }
                return ($"💎 {Markup.Escape(r.Name)}{slotStr} {statusTag}", r.Id);
            }).ToList());
            choices.Add("[grey]← Назад[/]");

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]✨ {_loc.T("soul_relics")}[/]" +
                    (isChaosSea ? "  [dim](выберите для просмотра / управления)[/]"
                                : "  [yellow dim](только просмотр — управление в Море Хаоса)[/]"))
                .PageSize(15)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= allRelics.Count) break;

            var (relicId, relicName, relicStatus, relicData, _) = allRelics[selIdx];
            var shouldRefresh = await ShowRelicDetailPanel(relicId, relicName, relicStatus, relicData, isChaosSea);
            if (shouldRefresh)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    /// <summary>
    /// Displays detailed information about a soul relic.
    /// In Chaos Sea: offers equip/unequip actions that modify soul_state.json directly.
    /// Returns true if state was modified (needs refresh).
    /// </summary>
    private async Task<bool> ShowRelicDetailPanel(string relicId, string name, string status, JsonElement relic, bool isChaosSea)
    {
        var lines = BuildSoulRelicDetailLines(name, relic, status);
        var slot = ResolveRelicSlot(relic);

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 💎 Реликвия души ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });

        // Action menu
        if (isChaosSea)
        {
            var actions = new List<string>();
            if (status == "stored")
                actions.Add("⚔ Экипировать");
            else
                actions.Add("📦 Снять (в хранилище)");
            actions.Add("← Назад к списку");

            var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(actions));

            if (action.Contains("Экипировать"))
            {
                await EquipSoulRelicLocal(relicId, name, slot);
                return true;
            }
            if (action.Contains("Снять"))
            {
                await UnequipSoulRelicLocal(relicId, name);
                return true;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow dim]  ⚠ Управление реликвиями доступно только в Море Хаоса.[/]");
            WaitForKey();
        }

        return false;
    }

    private List<string> BuildSoulRelicDetailLines(string name, JsonElement relic, string? status)
    {
        var lines = new List<string>
        {
            $"[bold yellow]💎 {Markup.Escape(name)}[/]",
            ""
        };

        var desc = GetStr(relic, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add($"[white]{Markup.Escape(desc)}[/]");
            lines.Add("");
        }

        var slot = ResolveRelicSlot(relic);
        if (!string.IsNullOrEmpty(slot))
            lines.Add($"  📌 Слот: [cyan]{Markup.Escape(slot)}[/]");

        var rarity = GetStr(relic, "quality", GetStr(relic, "rarity", ""));
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");

        var category = GetStr(relic, "category", "");
        if (!string.IsNullOrEmpty(category))
            lines.Add($"  📋 Категория: [cyan]{Markup.Escape(category)}[/]");

        var tier = GetStr(relic, "tier", "");
        if (!string.IsNullOrEmpty(tier))
            lines.Add($"  🏆 Ранг: [yellow]{Markup.Escape(tier)}[/]");

        if (relic.TryGetProperty("equipmentData", out var eqd) && eqd.ValueKind == JsonValueKind.Object)
        {
            var req = GetStr(eqd, "enlightenmentRequirement", "");
            if (!string.IsNullOrEmpty(req) && req != "0")
                lines.Add($"  🔒 Требование просветления: [yellow]{Markup.Escape(req)}[/]");
        }

        if (relic.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Эффекты:[/]");
            if (effects.TryGetProperty("characteristicBonuses", out var charBon) && charBon.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in charBon.EnumerateObject())
                {
                    var charName = Characteristics.RussianNames.GetValueOrDefault(prop.Name, prop.Name);
                    lines.Add($"    • [green]{Markup.Escape(charName)} +{prop.Value}[/]");
                }
            }
            if (effects.TryGetProperty("actionCheckBonuses", out var actBon) && actBon.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in actBon.EnumerateObject())
                    lines.Add($"    • [cyan]{Markup.Escape(prop.Name)}: +{prop.Value}[/]");
            }

            var knownEffectProps = new HashSet<string> { "characteristicBonuses", "actionCheckBonuses" };
            foreach (var prop in effects.EnumerateObject())
            {
                if (knownEffectProps.Contains(prop.Name)) continue;
                if (prop.Value.ValueKind == JsonValueKind.String)
                    lines.Add($"    • [green]{Markup.Escape(prop.Name)}: {Markup.Escape(prop.Value.GetString() ?? "")}[/]");
                else if (prop.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    lines.Add($"    • [green]{Markup.Escape(prop.Name)}: {prop.Value}[/]");
            }
        }

        if (relic.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Бонусы:[/]");
            foreach (var b in bonuses.EnumerateArray())
            {
                if (b.ValueKind == JsonValueKind.String)
                    lines.Add($"    • [green]{Markup.Escape(b.GetString() ?? "")}[/]");
                else if (b.ValueKind == JsonValueKind.Object)
                {
                    var bName = GetStr(b, "name", GetStr(b, "stat", ""));
                    var bVal = GetStr(b, "value", GetStr(b, "bonus", ""));
                    if (!string.IsNullOrEmpty(bName))
                        lines.Add($"    • [green]{Markup.Escape(bName)}: {Markup.Escape(bVal)}[/]");
                }
            }
        }

        if (relic.TryGetProperty("passiveEffects", out var passives) && passives.ValueKind == JsonValueKind.Array)
        {
            lines.Add("");
            lines.Add("  [bold]🔮 Пассивные эффекты:[/]");
            foreach (var e in passives.EnumerateArray())
            {
                var eName = e.ValueKind == JsonValueKind.String ? e.GetString() : GetStr(e, "name", GetStr(e, "effect", ""));
                if (!string.IsNullOrEmpty(eName))
                    lines.Add($"    • [mediumpurple2]{Markup.Escape(eName!)}[/]");
            }
        }

        if (relic.TryGetProperty("acquisitionData", out var acq) && acq.ValueKind == JsonValueKind.Object)
        {
            var srcGuardian = GetStr(acq, "sourceGuardian", "");
            if (!string.IsNullOrEmpty(srcGuardian))
            {
                lines.Add("");
                lines.Add($"  🛡️ Источник: [cyan]{Markup.Escape(srcGuardian)}[/]");
            }
            var story = GetStr(acq, "acquisitionStory", "");
            if (!string.IsNullOrEmpty(story))
            {
                if (string.IsNullOrEmpty(srcGuardian)) lines.Add("");
                lines.Add($"  [dim italic]📜 {Markup.Escape(story)}[/]");
            }
        }

        var narrativeOrigin = GetStr(relic, "narrativeOrigin", "");
        if (!string.IsNullOrEmpty(narrativeOrigin))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📜 {Markup.Escape(narrativeOrigin)}[/]");
        }

        if (!string.IsNullOrEmpty(status))
        {
            lines.Add("");
            lines.Add($"  Статус: {(status == "equipped" ? "[green]⚔ Экипировано[/]" : "[dim]📦 В хранилище[/]")}");
        }

        return lines;
    }

    private static string ResolveRelicSlot(JsonElement relic)
    {
        var slot = GetStr(relic, "slot", "");
        if (string.IsNullOrEmpty(slot) && relic.TryGetProperty("equipmentData", out var eqData))
            slot = GetStr(eqData, "equipSlot", "");
        if (string.IsNullOrEmpty(slot) && relic.TryGetProperty("gameplayStatus", out var gpStat))
            slot = GetStr(gpStat, "currentSlot", "");
        return slot;
    }

    private static string GuardianTradeDisplayDomain(string domainTag) => domainTag switch
    {
        "Combat" => "Боевой домен",
        "Magic" => "Магический домен",
        "Social" => "Социальный домен",
        "Crafting" => "Ремесленный домен",
        "Survival" => "Домен выживания",
        "Knowledge" => "Домен знания",
        "Trade" => "Торговый домен",
        _ => domainTag
    };

    /// <summary>
    /// Moves a relic from stored[] to equipped[] in soul_state.json.
    /// </summary>
    private async Task EquipSoulRelicLocal(string relicId, string relicName, string defaultSlot)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var relicsNode = node?["soulRelics"];
            if (relicsNode == null) return;

            var storedArr = relicsNode["stored"]?.AsArray();
            var equippedArr = relicsNode["equipped"]?.AsArray();
            if (storedArr == null || equippedArr == null) return;

            // Find the relic in stored
            JsonNode? target = null;
            int targetIdx = -1;
            for (int i = 0; i < storedArr.Count; i++)
            {
                if (RelicNodeMatches(storedArr[i], relicId, relicName)) { target = storedArr[i]; targetIdx = i; break; }
            }
            if (target == null || targetIdx < 0) return;

            // Remove from stored
            storedArr.RemoveAt(targetIdx);

            // Update gameplay status
            if (target["gameplayStatus"] is JsonObject gs)
            {
                gs["equipped"] = true;
                gs["currentSlot"] = !string.IsNullOrEmpty(defaultSlot) ? defaultSlot : "Default";
            }
            else
            {
                target["gameplayStatus"] = new JsonObject
                {
                    ["equipped"] = true,
                    ["currentSlot"] = !string.IsNullOrEmpty(defaultSlot) ? defaultSlot : "Default"
                };
            }

            // Add to equipped
            equippedArr.Add(target);

            // Write back
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));

            AnsiConsole.MarkupLine($"[green]✅ Реликвия «{Markup.Escape(relicName)}» экипирована![/]");
            AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>
    /// Moves a relic from equipped[] to stored[] in soul_state.json.
    /// </summary>
    private async Task UnequipSoulRelicLocal(string relicId, string relicName)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var relicsNode = node?["soulRelics"];
            if (relicsNode == null) return;

            var storedArr = relicsNode["stored"]?.AsArray();
            var equippedArr = relicsNode["equipped"]?.AsArray();
            if (storedArr == null || equippedArr == null) return;

            JsonNode? target = null;
            int targetIdx = -1;
            for (int i = 0; i < equippedArr.Count; i++)
            {
                if (RelicNodeMatches(equippedArr[i], relicId, relicName)) { target = equippedArr[i]; targetIdx = i; break; }
            }
            if (target == null || targetIdx < 0) return;

            equippedArr.RemoveAt(targetIdx);

            if (target["gameplayStatus"] is JsonObject gs)
            {
                gs["equipped"] = false;
                gs["currentSlot"] = "";
            }

            storedArr.Add(target);

            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));

            AnsiConsole.MarkupLine($"[green]✅ Реликвия «{Markup.Escape(relicName)}» снята и убрана в хранилище.[/]");
            AnsiConsole.MarkupLine("[dim]Нажмите любую клавишу...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>
    /// Interactive storage panel: deposit items from inventory or retrieve items from storage.
    /// Modifies items.json and current_location.json directly (no GM needed).
    /// Returns true if any changes were made.
    /// </summary>
    private async Task<bool> ShowStorageInteractivePanel(string storageName, string storageId)
    {
        bool anyModified = false;

        while (true)
        {
            // Re-read both files each iteration
            var invJson = await _fs.ReadFileAsync("game_state/inventory/items.json");
            var locJson = await _fs.ReadFileAsync("game_state/world/current_location.json");
            if (invJson == null || locJson == null)
            {
                AnsiConsole.MarkupLine("[red]Ошибка чтения файлов инвентаря или локации.[/]");
                WaitForKey();
                return anyModified;
            }

            JsonNode? invNode, locNode;
            try { invNode = JsonNode.Parse(invJson); locNode = JsonNode.Parse(locJson); }
            catch { AnsiConsole.MarkupLine("[red]Ошибка парсинга JSON.[/]"); WaitForKey(); return anyModified; }
            if (invNode == null || locNode == null) return anyModified;

            // Find the storage in current_location
            var storagesArr = locNode["locationStorages"]?.AsArray();
            if (storagesArr == null) { AnsiConsole.MarkupLine("[red]Хранилища не найдены в локации.[/]"); WaitForKey(); return anyModified; }

            JsonNode? storageNode = null;
            int storageIdx = -1;
            for (int i = 0; i < storagesArr.Count; i++)
            {
                var sid = storagesArr[i]?["storageId"]?.GetValue<string>() ?? "";
                var sname = storagesArr[i]?["name"]?.GetValue<string>() ?? "";
                if ((!string.IsNullOrEmpty(storageId) && sid == storageId) ||
                    sname == storageName)
                {
                    storageNode = storagesArr[i];
                    storageIdx = i;
                    break;
                }
            }
            if (storageNode == null) { AnsiConsole.MarkupLine("[red]Хранилище не найдено.[/]"); WaitForKey(); return anyModified; }

            // Gather storage contents
            var contentsArr = storageNode["contents"]?.AsArray() ?? new JsonArray();
            var storageEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < contentsArr.Count; i++)
            {
                var iName = GetInventoryItemName(contentsArr[i]);
                var iQty = contentsArr[i]?["quantity"]?.ToString() ??
                           contentsArr[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(iName);
                if (iQty != "1") label += $" ×{Markup.Escape(iQty)}";
                storageEntries.Add((GetInventoryItemIdentity(contentsArr[i]), iName, label));
            }
            var storageItems = MakeUniqueChoiceLabels(storageEntries.Select(e => (e.Label, e.Identity)).ToList());

            // Gather player inventory items
            var invItemsArr = GetPlayerInventoryArrayNode(invNode, createIfMissing: false) ?? new JsonArray();
            var playerEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < invItemsArr.Count; i++)
            {
                var iName = GetInventoryItemName(invItemsArr[i]);
                var iQty = invItemsArr[i]?["quantity"]?.ToString() ??
                           invItemsArr[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(iName);
                if (iQty != "1") label += $" ×{Markup.Escape(iQty)}";
                playerEntries.Add((GetInventoryItemIdentity(invItemsArr[i]), iName, label));
            }
            var playerItems = MakeUniqueChoiceLabels(playerEntries.Select(e => (e.Label, e.Identity)).ToList());

            // Capacity info
            var capStr = storageNode["capacity"]?.ToString() ?? "";
            var volStr = storageNode["volume"]?.ToString() ?? "";
            var capInfo = "";
            if (!string.IsNullOrEmpty(capStr)) capInfo += $" вместимость: {capStr}";
            if (!string.IsNullOrEmpty(volStr)) capInfo += $" объём: {volStr} дм³";

            // Show action menu
            var actionChoices = new List<string>();
            if (playerItems.Count > 0)
                actionChoices.Add($"📥 Положить предмет в хранилище ({playerItems.Count} в инвентаре)");
            if (storageItems.Count > 0)
                actionChoices.Add($"📤 Забрать предмет из хранилища ({storageItems.Count} внутри)");
            actionChoices.Add("[dim]← Назад к инвентарю[/]");

            var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]📦 {Markup.Escape(storageName)}[/]" +
                    (!string.IsNullOrEmpty(capInfo) ? $"  [dim]({capInfo.Trim()})[/]" : "") +
                    $"\n  [dim]Предметов внутри: {contentsArr.Count}[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actionChoices));

            if (action.Contains("← Назад")) return anyModified;

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            if (action.StartsWith("📥")) // Deposit
            {
                var depositChoices = playerItems.ToList();
                depositChoices.Add("[dim]← Отмена[/]");

                var picked = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для перемещения в хранилище:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(depositChoices));

                if (picked.Contains("← Отмена")) continue;

                var pickedIdx = depositChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= invItemsArr.Count) continue;

                try
                {
                    // Remove from player inventory
                    var itemToMove = invItemsArr[pickedIdx]!;
                    invItemsArr.RemoveAt(pickedIdx);

                    // Add to storage contents
                    if (storageNode["contents"] == null)
                        storageNode["contents"] = new JsonArray();
                    storageNode["contents"]!.AsArray().Add(itemToMove);

                    // Write both files
                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", locNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» перемещён в хранилище «{Markup.Escape(storageName)}»[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
            else if (action.StartsWith("📤")) // Retrieve
            {
                var retrieveChoices = storageItems.ToList();
                retrieveChoices.Add("[dim]← Отмена[/]");

                var picked = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для извлечения из хранилища:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(retrieveChoices));

                if (picked.Contains("← Отмена")) continue;

                var pickedIdx = retrieveChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= contentsArr.Count) continue;

                try
                {
                    // Remove from storage
                    var itemToMove = contentsArr[pickedIdx]!;
                    contentsArr.RemoveAt(pickedIdx);

                    // Add to player inventory
                    var playerInventory = GetPlayerInventoryArrayNode(invNode, createIfMissing: true);
                    playerInventory!.Add(itemToMove);

                    // Write both files
                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", locNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» извлечён из хранилища в инвентарь[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    /// <summary>
    /// Interactive vehicle inventory panel: move items between player inventory and vehicle inventory.
    /// Modifies items.json and vehicles.json directly (no GM needed).
    /// Returns true if any changes were made.
    /// </summary>
    private async Task<bool> ShowVehicleInventoryInteractivePanel(string vehicleName, string vehicleId)
    {
        bool anyModified = false;

        while (true)
        {
            var invJson = await _fs.ReadFileAsync("game_state/inventory/items.json");
            var vehJson = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
            if (invJson == null || vehJson == null)
            {
                AnsiConsole.MarkupLine("[red]Ошибка чтения файлов инвентаря или транспорта.[/]");
                WaitForKey();
                return anyModified;
            }

            JsonNode? invNode;
            JsonNode? vehNode;
            try
            {
                invNode = JsonNode.Parse(invJson);
                vehNode = JsonNode.Parse(vehJson);
            }
            catch
            {
                AnsiConsole.MarkupLine("[red]Ошибка парсинга JSON.[/]");
                WaitForKey();
                return anyModified;
            }

            if (invNode == null || vehNode == null)
                return anyModified;

            var vehicleNode = FindVehicleNode(vehNode, vehicleName, vehicleId);
            if (vehicleNode == null)
            {
                AnsiConsole.MarkupLine("[red]Транспорт не найден.[/]");
                WaitForKey();
                return anyModified;
            }

            var vehicleInventory = vehicleNode["inventory"]?.AsArray() ?? new JsonArray();
            var playerInventory = GetPlayerInventoryArrayNode(invNode, createIfMissing: false) ?? new JsonArray();

            var vehicleEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < vehicleInventory.Count; i++)
            {
                var itemName = GetInventoryItemName(vehicleInventory[i]);
                var qty = vehicleInventory[i]?["quantity"]?.ToString() ??
                          vehicleInventory[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(itemName);
                if (qty != "1") label += $" ×{Markup.Escape(qty)}";
                vehicleEntries.Add((GetInventoryItemIdentity(vehicleInventory[i]), itemName, label));
            }
            var vehicleItems = MakeUniqueChoiceLabels(vehicleEntries.Select(e => (e.Label, e.Identity)).ToList());

            var playerEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < playerInventory.Count; i++)
            {
                var itemName = GetInventoryItemName(playerInventory[i]);
                var qty = playerInventory[i]?["quantity"]?.ToString() ??
                          playerInventory[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(itemName);
                if (qty != "1") label += $" ×{Markup.Escape(qty)}";
                playerEntries.Add((GetInventoryItemIdentity(playerInventory[i]), itemName, label));
            }
            var playerItems = MakeUniqueChoiceLabels(playerEntries.Select(e => (e.Label, e.Identity)).ToList());

            var actionChoices = new List<string>();
            if (playerItems.Count > 0)
                actionChoices.Add($"📥 Положить предмет в транспорт ({playerItems.Count} в инвентаре)");
            if (vehicleItems.Count > 0)
                actionChoices.Add($"📤 Забрать предмет из транспорта ({vehicleItems.Count} внутри)");
            actionChoices.Add("[dim]← Назад к транспорту[/]");

            var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🚗 {Markup.Escape(vehicleName)}[/]\n  [dim]Предметов внутри: {vehicleInventory.Count}[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actionChoices));

            if (action.Contains("← Назад"))
                return anyModified;

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            if (action.StartsWith("📥"))
            {
                var depositChoices = playerItems.ToList();
                depositChoices.Add("[dim]← Отмена[/]");

                var picked = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для перемещения в транспорт:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(depositChoices));

                if (picked.Contains("← Отмена"))
                    continue;

                var pickedIdx = depositChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= playerInventory.Count)
                    continue;

                try
                {
                    var itemToMove = playerInventory[pickedIdx]!;
                    playerInventory.RemoveAt(pickedIdx);

                    if (vehicleNode["inventory"] == null)
                        vehicleNode["inventory"] = new JsonArray();
                    vehicleNode["inventory"]!.AsArray().Add(itemToMove);

                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", vehNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» перемещён в транспорт «{Markup.Escape(vehicleName)}»[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
            else if (action.StartsWith("📤"))
            {
                var retrieveChoices = vehicleItems.ToList();
                retrieveChoices.Add("[dim]← Отмена[/]");

                var picked = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для извлечения из транспорта:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(retrieveChoices));

                if (picked.Contains("← Отмена"))
                    continue;

                var pickedIdx = retrieveChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= vehicleInventory.Count)
                    continue;

                try
                {
                    var itemToMove = vehicleInventory[pickedIdx]!;
                    vehicleInventory.RemoveAt(pickedIdx);

                    var playerInventoryTarget = GetPlayerInventoryArrayNode(invNode, createIfMissing: true);
                    playerInventoryTarget!.Add(itemToMove);

                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", vehNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    AnsiConsole.MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» извлечён из транспорта в инвентарь[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    private static List<JsonElement> CollectCodexEntries(JsonElement root)
    {
        var entries = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("entries", out var existingEntries) && existingEntries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in existingEntries.EnumerateArray())
                    if (entry.ValueKind == JsonValueKind.Object)
                        entries.Add(entry.Clone());
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
                if (entry.ValueKind == JsonValueKind.Object)
                    entries.Add(entry.Clone());
        }

        return entries
            .GroupBy(e => GetStr(e, "entryId", GetStr(e, "title", GetStr(e, "name", e.GetRawText()))), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<JsonElement> CollectGuardianDisplayEntries(JsonElement root)
    {
        var guardians = new List<JsonElement>();
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddGuardian(JsonElement guardian)
        {
            if (guardian.ValueKind != JsonValueKind.Object) return;
            var key = GetStr(guardian, "guardianId", GetStr(guardian, "name", ""));
            if (!string.IsNullOrWhiteSpace(key))
            {
                if (!knownIds.Add(key))
                    return;
            }
            guardians.Add(guardian.Clone());
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in root.EnumerateArray())
                AddGuardian(guardian);
            return guardians;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return guardians;

        if (root.TryGetProperty("guardians", out var guardiansArr) && guardiansArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardiansArr.EnumerateArray())
                AddGuardian(guardian);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
            AddGuardian(activeGuardian);

        if ((root.TryGetProperty("guardianId", out _) || root.TryGetProperty("name", out _)) &&
            !root.TryGetProperty("guardians", out _))
            AddGuardian(root);

        return guardians;
    }

    private static JsonNode? FindVehicleNode(JsonNode root, string vehicleName, string vehicleId)
    {
        JsonArray? vehiclesArray = null;
        if (root is JsonObject obj)
            vehiclesArray = obj["vehicles"]?.AsArray();
        else if (root is JsonArray arr)
            vehiclesArray = arr;

        if (vehiclesArray == null)
            return null;

        foreach (var vehicle in vehiclesArray)
        {
            if (vehicle == null)
                continue;

            var id = vehicle["vehicleId"]?.GetValue<string>() ??
                     vehicle["id"]?.GetValue<string>() ?? "";
            var name = vehicle["name"]?.GetValue<string>() ?? "";

            if ((!string.IsNullOrEmpty(vehicleId) && id == vehicleId) ||
                (!string.IsNullOrEmpty(vehicleName) && name == vehicleName))
                return vehicle;
        }

        return null;
    }

    private async Task ShowSoulQuests()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        if (doc == null)
        {
            ShowEmptyPanel(_loc.T("guardian_quests"), "Мета-квестов нет");
            return;
        }

        var quests = new List<(string label, JsonElement el)>();
        EnumerateArray(doc.RootElement, "quests", item =>
        {
            var name = GetStr(item, "questName", GetStr(item, "name", "???"));
            var status = GetStr(item, "status", "Active").ToLowerInvariant();
            var icon = status switch { "completed" or "завершён" => "✅", "failed" or "провален" => "❌", _ => "🔄" };
            var guardian = GetStr(item, "guardian", GetStr(item, "questGiver", ""));
            var suffix = string.IsNullOrWhiteSpace(guardian) ? "" : $" [dim]({Markup.Escape(guardian)})[/]";
            quests.Add(($"🌟 {icon} {name}{suffix}", item));
        });

        if (quests.Count == 0)
        {
            EnumerateJsonItems(doc.RootElement, item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "name", "???"));
                var status = GetStr(item, "status", "Active").ToLowerInvariant();
                var icon = status switch { "completed" or "завершён" => "✅", "failed" or "провален" => "❌", _ => "🔄" };
                var guardian = GetStr(item, "guardian", GetStr(item, "questGiver", ""));
                var suffix = string.IsNullOrWhiteSpace(guardian) ? "" : $" [dim]({Markup.Escape(guardian)})[/]";
                quests.Add(($"🌟 {icon} {name}{suffix}", item));
            });
        }

        if (quests.Count == 0)
        {
            ShowEmptyPanel(_loc.T("guardian_quests"), "Мета-квестов нет");
            return;
        }

        while (true)
        {
            var choices = quests.Select(q => $"[purple]{Markup.Escape(q.label)}[/]").ToList();
            choices.Add("[dim]← Назад[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold purple]🌟 {_loc.T("guardian_quests")}[/]")
                    .PageSize(12)
                    .AddChoices(choices));

            if (selected.Contains("← Назад"))
                return;

            var questIndex = choices.IndexOf(selected);
            if (questIndex < 0 || questIndex >= quests.Count)
                return;

            await ShowQuestDetailPanel(quests[questIndex].el, true, false);
        }
    }

    // ═══ Ink Feathers menu ═══

    /// <summary>
    /// Reads inkFeathers from soul_state.json, handling both object and number formats.
    /// </summary>
    private async Task<int> ReadInkFeathersBalance()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null) return 0;

        var root = doc.RootElement;
        if (!root.TryGetProperty("inkFeathers", out var feathersEl)) return 0;

        if (feathersEl.ValueKind == JsonValueKind.Number)
            return feathersEl.TryGetInt32(out var n) ? n : 0;

        if (feathersEl.ValueKind == JsonValueKind.Object &&
            feathersEl.TryGetProperty("current", out var cur) &&
            cur.ValueKind == JsonValueKind.Number)
            return cur.TryGetInt32(out var c) ? c : 0;

        return 0;
    }

    private async Task<string?> ReadPendingMemoryLegacySummaryAsync()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null) return null;

        var root = doc.RootElement;
        if (!root.TryGetProperty("pendingMemoryLegacy", out var legacy) || legacy.ValueKind != JsonValueKind.Object)
            return null;

        var legacyType = GetStr(legacy, "legacyType", "");
        if (legacyType.Equals("startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = GetStr(legacy, "characteristic", "");
            var bonus = GetInt(legacy, "bonus", 0);
            if (string.IsNullOrWhiteSpace(characteristic) || bonus <= 0) return null;
            var russianStat = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
            return $"🧠 Активное Наследие Памяти: +{bonus} к {russianStat} в следующей жизни";
        }

        if (legacyType.Equals("startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var skillName = GetStr(legacy, "skillName", "");
            if (string.IsNullOrWhiteSpace(skillName)) return null;
            return $"🧠 Активное Наследие Памяти: пассивный навык «{skillName}» в следующей жизни";
        }

        return null;
    }

    /// <summary>
    /// Deducts feathers from soul_state.json atomically.
    /// Handles both object { "current": N } and plain integer formats.
    /// Returns true on success.
    /// </summary>
    private async Task<bool> DeductInkFeathers(int cost)
    {
        const string path = "game_state/meta/soul_state.json";
        try
        {
            var jsonText = await _fs.ReadFileAsync(path);
            if (jsonText == null) return false;

            var node = JsonNode.Parse(jsonText);
            if (node == null) return false;

            var feathersNode = node["inkFeathers"];
            if (feathersNode == null) return false;

            if (feathersNode is JsonObject inkObj)
            {
                var oldVal = inkObj["current"]?.GetValue<int>() ?? 0;
                if (oldVal < cost) return false;
                inkObj["current"] = oldVal - cost;
            }
            else
            {
                int oldVal;
                try { oldVal = feathersNode.GetValue<int>(); }
                catch { oldVal = int.TryParse(feathersNode.ToString(), out var p) ? p : 0; }
                if (oldVal < cost) return false;
                node["inkFeathers"] = oldVal - cost;
            }

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatDiceDisplay(int[] dice)
    {
        var parts = new List<string>();
        for (int i = 0; i < dice.Length; i++)
        {
            var d = dice[i];
            var color = d switch
            {
                1 => "red",
                20 => "gold1",
                >= 15 => "green",
                >= 10 => "white",
                >= 5 => "yellow",
                _ => "red3"
            };
            parts.Add($"[{color}]{d}[/]");
        }
        return string.Join(" ", parts);
    }

    private async Task ShowInkFeathersMenu()
    {
        _diceRevealed = false;
        while (true)
        {
            var feathers = await ReadInkFeathersBalance();
            var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
            Services.PendingTurnState? pendingTurnState = null;
            if (!isChaosSea && _pendingTurnState != null)
            {
                pendingTurnState = await _pendingTurnState.GetOrCreateAsync();
                _diceRevealed = pendingTurnState.IsFateLocked;
            }

            var phaseLabel = isChaosSea ? "[blue]Море Хаоса[/]" : "[green]Смертная жизнь[/]";
            var pendingLegacySummary = isChaosSea ? await ReadPendingMemoryLegacySummaryAsync() : null;
            AnsiConsole.Write(new Panel(new Markup(
                $"🪶 Чернильные Перья: [bold yellow]{feathers}[/]\n" +
                $"📍 Фаза: {phaseLabel}" +
                (!string.IsNullOrWhiteSpace(pendingLegacySummary) ? $"\n[magenta]{Markup.Escape(pendingLegacySummary)}[/]" : "")))
            {
                Header = new PanelHeader(" 🪶 Чернильные Перья ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1)
            });

            // Build options
            var choices = new List<string>();

            if (!isChaosSea)
            {
                // Mortal Life options
                var costReveal = Math.Max(5, (int)(feathers * 0.10));
                var costRewrite = Math.Max(15, (int)(feathers * 0.25));
                var costSacrifice = Math.Max(25, (int)(feathers * 0.20));
                var costAbsorb = Math.Max(20, (int)(feathers * 0.30));
                var costLearn = Math.Max(10, (int)(feathers * 0.15));
                var costShield = Math.Max(30, (int)(feathers * 0.35));
                var costSeal = Math.Max(50, (int)(feathers * 0.40));

                choices.Add(feathers >= 5
                    ? $"🔮 Открыть Судьбу (−{costReveal} 🪶)"
                    : $"[dim]🔒 Открыть Судьбу (нужно ≥5 🪶)[/]");
                choices.Add(feathers >= 15 && _diceRevealed
                    ? $"✍️ Переписать Судьбу (−{costRewrite} 🪶)"
                    : $"[dim]🔒 Переписать Судьбу ({(!_diceRevealed ? "сначала откройте судьбу" : "нужно ≥15 🪶")})[/]");
                choices.Add(feathers >= 25
                    ? $"🌀 Пожертвовать Хаосу (−{costSacrifice} 🪶)"
                    : $"[dim]🔒 Пожертвовать Хаосу (нужно ≥25 🪶)[/]");
                choices.Add(feathers >= 20
                    ? $"⬆️ Впитать Перья (−{costAbsorb} 🪶)"
                    : $"[dim]🔒 Впитать Перья (нужно ≥20 🪶)[/]");
                choices.Add(feathers >= 10
                    ? $"📚 Познать Перья (−{costLearn} 🪶)"
                    : $"[dim]🔒 Познать Перья (нужно ≥10 🪶)[/]");
                choices.Add(feathers >= 30
                    ? $"🛡️ Щит Судьбы (−{costShield} 🪶)"
                    : $"[dim]🔒 Щит Судьбы (нужно ≥30 🪶)[/]");
                choices.Add(feathers >= 50
                    ? $"🔮 Запечатать в Чернила (−{costSeal} 🪶)"
                    : $"[dim]🔒 Запечатать в Чернила (нужно ≥50 🪶)[/]");
            }
            else
            {
                // Afterlife options
                var costDonate = Math.Max(10, (int)(feathers * 0.15));
                var costCultivate = Math.Max(20, (int)(feathers * 0.25));
                var costMemory = Math.Max(15, (int)(feathers * 0.20));
                var costImprint = Math.Max(100, (int)(feathers * 0.50));

                choices.Add(feathers >= 10
                    ? $"🎁 Пожертвовать Хранителю (−{costDonate} 🪶)"
                    : $"[dim]🔒 Пожертвовать Хранителю (нужно ≥10 🪶)[/]");
                choices.Add(feathers >= 20
                    ? $"✨ Культивировать Просветление (−{costCultivate} 🪶)"
                    : $"[dim]🔒 Культивировать Просветление (нужно ≥20 🪶)[/]");
                choices.Add(feathers >= 10
                    ? "🤝 Попросить об услуге (переменная цена)"
                    : $"[dim]🔒 Попросить об услуге (нужно ≥10 🪶)[/]");
                choices.Add(feathers >= 15
                    ? $"🧠 Открыть Врата Памяти (−{costMemory} 🪶)"
                    : $"[dim]🔒 Открыть Врата Памяти (нужно ≥15 🪶)[/]");
                choices.Add(feathers >= 100
                    ? $"👤 Создать Слепок Души (−{costImprint} 🪶)"
                    : $"[dim]🔒 Создать Слепок Души (нужно ≥100 🪶)[/]");
            }

            choices.Add("← Назад");

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Выберите действие:[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (choice.Contains("Назад")) return;

            if (choice.Contains("🔒"))
            {
                AnsiConsole.MarkupLine("[yellow]⚠️ Недостаточно Чернильных Перьев или условие не выполнено.[/]");
                WaitForKey();
                continue;
            }

            // Route to handler
            if (!isChaosSea)
            {
                if (choice.Contains("Открыть Судьбу"))
                    await HandleRevealFate(feathers);
                else if (choice.Contains("Переписать Судьбу"))
                    await HandleRewriteFate(feathers);
                else if (choice.Contains("Пожертвовать Хаосу"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(25, (int)(feathers * 0.20)),
                        "🌀 Пожертвовать Хаосу",
                        cost => $"[INK_FEATHER_ACTION: SACRIFICE_TO_CHAOS] Игрок жертвует {cost} Чернильных Перьев Хаосу. " +
                            "Создай эпическое случайное событие в мире смертных, влияющее на окружение игрока. " +
                            "Событие должно быть масштабным и запоминающимся.");
                else if (choice.Contains("Впитать Перья"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(20, (int)(feathers * 0.30)),
                        "⬆️ Впитать Перья",
                        cost => $"[INK_FEATHER_ACTION: ABSORB_FEATHERS] Игрок впитывает {cost} Чернильных Перьев. " +
                            $"Добавь существенный опыт (experienceGained), эквивалентный {cost}% от опыта до следующего уровня. " +
                            "Перья уже списаны клиентом.");
                else if (choice.Contains("Познать Перья"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(10, (int)(feathers * 0.15)),
                        "📚 Познать Перья",
                        cost => $"[INK_FEATHER_ACTION: LEARN_SKILL] Игрок расходует {cost} Чернильных Перьев для познания. " +
                            "Выдай случайный навык (активный или пассивный) из воспоминаний прошлых жизней. " +
                            "Навык должен быть тематически связан с текущим миром.");
                else if (choice.Contains("Щит Судьбы"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(30, (int)(feathers * 0.35)),
                        "🛡️ Щит Судьбы",
                        cost => $"[INK_FEATHER_ACTION: FATE_SHIELD] Игрок активирует Щит Судьбы за {cost} Чернильных Перьев. " +
                            "При следующем критическом провале (Natural 1) — превратить его в обычный провал. " +
                            "Добавь этот эффект в playerActiveEffects с маркером 'Щит Судьбы'.");
                else if (choice.Contains("Запечатать в Чернила"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(50, (int)(feathers * 0.40)),
                        "🔮 Запечатать в Чернила",
                        cost => $"[INK_FEATHER_ACTION: SEAL_IN_INK] Игрок тратит {cost} Чернильных Перьев на Запечатывание в Чернила. " +
                            "Подготовь отложенное улучшение качества выбранного игроком предмета на 1 тир (например, Common→Uncommon, Rare→Epic). " +
                            "В этом ходу НЕ повышай предмет напрямую; вместо этого создай persisted pending ink action со status=awaiting-item-choice и предложи выбрать предмет в narrativeResponse.");
            }
            else
            {
                if (choice.Contains("Пожертвовать Хранителю"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(10, (int)(feathers * 0.15)),
                        "🎁 Пожертвовать Хранителю",
                        cost => $"[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] Игрок жертвует {cost} Чернильных Перьев Хранителю. " +
                            "Повысь репутацию с текущим хранителем на 15-25 пунктов (пропорционально количеству потраченных перьев). " +
                            "Перья уже списаны клиентом.");
                else if (choice.Contains("Культивировать Просветление"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(20, (int)(feathers * 0.25)),
                        "✨ Культивировать Просветление",
                        cost => $"[INK_FEATHER_ACTION: CULTIVATE_ENLIGHTENMENT] Игрок тратит {cost} Чернильных Перьев на Культивирование Просветления. " +
                            "Добавь значительный прогресс просветления в soul_state (enlightenment.experience). " +
                            "Перья уже списаны клиентом.");
                else if (choice.Contains("Попросить об услуге"))
                    await HandleGuardianFavor(feathers);
                else if (choice.Contains("Открыть Врата Памяти"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(15, (int)(feathers * 0.20)),
                        "🧠 Открыть Врата Памяти",
                        cost => $"[INK_FEATHER_ACTION: MEMORY_GATES] Игрок тратит {cost} Чернильных Перьев на Открытие Врат Памяти. " +
                            "Создай одно active pendingMemoryLegacy для следующей смертной жизни. " +
                            "Выбери ровно один механический бонус: либо +2 к одной стартовой характеристике, либо один новый пассивный навык знаний. " +
                            "Запиши structured metaStateUpdates.memoryLegacyGrant и замени старое наследие, если оно уже существовало. " +
                            "Перья уже списаны клиентом.");
                else if (choice.Contains("Создать Слепок Души"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(100, (int)(feathers * 0.50)),
                        "👤 Создать Слепок Души",
                        cost => $"[INK_FEATHER_ACTION: SOUL_IMPRINT] Игрок тратит {cost} Чернильных Перьев на Создание Слепка Души текущего компаньона. " +
                            "Предложи выбрать NPC-компаньона и создай soulImprint запись. " +
                            "Перья уже списаны клиентом.");
            }

            // If a GM action was set, break out of the loop
            if (_pendingGmAction != null) return;
        }
    }

    private async Task HandleRevealFate(int feathers)
    {
        var cost = Math.Max(5, (int)(feathers * 0.10));
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";

        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]🔮 Открыть Судьбу — потратить {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, потратить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (_pendingTurnState == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Сервис судьбы недоступен.[/]");
            WaitForKey();
            return;
        }

        var pendingState = await _pendingTurnState.GetOrCreateAsync();
        if (!await DeductInkFeathers(cost))
        {
            AnsiConsole.MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        pendingState = await _pendingTurnState.RevealAsync();
        var dice = pendingState.PreGeneratedDices1d20;
        var gacha = pendingState.GachaBaseResult ?? new GachaResult();

        var rarityColor = GetRarityColor(gacha.BaseRarity ?? "Common");
        var text = new List<string>
        {
            "[bold]🎲 Ваши кости судьбы:[/]",
            "",
            FormatDiceDisplay(dice),
            "",
            $"[bold]🎰 Гача-база:[/] [{rarityColor}]{Markup.Escape(gacha.BaseRarity ?? "Common")}[/] (счёт: {gacha.BaseScore})",
            "[dim]Гача-база вычислена отдельно от этих кубиков и не сдвигает ваш dice pool.[/]",
            "",
            $"[dim]Списано: {cost} 🪶[/]"
        };

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🔮 Судьба открыта ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();

        _diceRevealed = true;
    }

    private async Task HandleRewriteFate(int feathers)
    {
        var cost = Math.Max(15, (int)(feathers * 0.25));
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";

        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]✍️ Переписать Судьбу — потратить {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, потратить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (_pendingTurnState == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Сервис судьбы недоступен.[/]");
            WaitForKey();
            return;
        }

        var currentState = await _pendingTurnState.GetOrCreateAsync();
        if (!currentState.IsFateLocked)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ Сначала нужно открыть судьбу, чтобы зафиксировать текущие кости.[/]");
            WaitForKey();
            return;
        }

        // Deduct feathers FIRST (before any dice modification)
        if (!await DeductInkFeathers(cost))
        {
            AnsiConsole.MarkupLine("[red]❌ Не удалось списать перья (недостаточно или ошибка).[/]");
            WaitForKey();
            return;
        }

        var oldDice = currentState.PreGeneratedDices1d20;
        var oldGacha = currentState.GachaBaseResult ?? new GachaResult();
        var newState = await _pendingTurnState.RewriteAsync();
        var newDice = newState.PreGeneratedDices1d20;
        var newGacha = newState.GachaBaseResult ?? new GachaResult();
        var oldRarityColor = GetRarityColor(oldGacha.BaseRarity ?? "Common");
        var newRarityColor = GetRarityColor(newGacha.BaseRarity ?? "Common");

        var text = new List<string>
        {
            "[bold]🎲 Старые кости:[/]",
            FormatDiceDisplay(oldDice),
            $"  Гача: [{oldRarityColor}]{Markup.Escape(oldGacha.BaseRarity ?? "Common")}[/] ({oldGacha.BaseScore})",
            "",
            "[bold]🎲 Новые кости:[/]",
            FormatDiceDisplay(newDice),
            $"  Гача: [{newRarityColor}]{Markup.Escape(newGacha.BaseRarity ?? "Common")}[/] ({newGacha.BaseScore})",
            "",
            "[dim]Новый фиксированный набор сохранится до вашего следующего обычного хода.[/]",
            "",
            $"[dim]Списано: {cost} 🪶[/]"
        };

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" ✍️ Судьба переписана ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();

        _diceRevealed = true;
    }

    private async Task HandleGmFeatherAction(int feathers, int cost, string actionName, Func<int, string> buildGmAction)
    {
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";

        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(actionName)} — потратить {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, потратить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (!await DeductInkFeathers(cost))
        {
            AnsiConsole.MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.MarkupLine($"[green]✅ Списано {cost} 🪶. Действие отправлено Мастеру Игры.[/]");
        WaitForKey();

        _pendingGmAction = buildGmAction(cost) +
            " Также обязательно запиши output/ink_feather_action_result.json с exact sessionId/requestId/turnNumber текущего turn_request, actionTag, resolved=true, costInFeathers, resolutionType, summary и stateEvidence. stateEvidence обязан содержать affectedFiles и минимальное подтверждение реального stateful результата.";
    }

    private async Task HandleGuardianFavor(int feathers)
    {
        var inputCost = AnsiConsole.Prompt(new TextPrompt<int>(
            $"[bold yellow]🤝 Сколько Перьев предложить Хранителю? (у вас {feathers} 🪶, мин. 10):[/]")
            .Validate(val =>
            {
                if (val < 10) return ValidationResult.Error("[red]Минимум 10 перьев[/]");
                if (val > feathers) return ValidationResult.Error($"[red]У вас только {feathers} 🪶[/]");
                return ValidationResult.Success();
            }));

        var costDisplay = $"{inputCost} 🪶 (останется {feathers - inputCost})";

        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Предложить Хранителю {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, предложить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (!await DeductInkFeathers(inputCost))
        {
            AnsiConsole.MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.MarkupLine($"[green]✅ Списано {inputCost} 🪶. Запрос услуги отправлен Хранителю.[/]");
        WaitForKey();

        _pendingGmAction = $"[INK_FEATHER_ACTION: GUARDIAN_FAVOR] Игрок предлагает Хранителю {inputCost} Чернильных Перьев в обмен на услугу. " +
            "Игрок может просить о чём-то, а может просто передавать перья в дар. " +
            "Гарантированный механический минимум: репутация с текущим Хранителем должна вырасти. " +
            "Перья уже списаны клиентом. Обязательно запиши output/ink_feather_action_result.json с guardianId, reputationChange и stateEvidence; всё остальное зависит от ролеплея и может быть добавлено дополнительно.";
    }

    private async Task ShowGachaInfo()
    {
        var feathers = _stateManager.CurrentState.InkFeathers;

        var text = new List<string>
        {
            "[bold yellow]🎰 Вытягивание реликвии души[/]",
            "",
            "Через эту команду вы тянете реликвию [bold]напрямую из Моря Хаоса[/], а не через текущего Хранителя.",
            "Это означает [yellow]нейтральный результат[/]: без бонусов, штрафов, скидок и влияния репутации Хранителя.",
            "Реликвии — это кристаллизованный опыт прошлых жизней.",
            "",
            $"🪶 Ваши перья: [yellow]{feathers}[/]",
            "",
            "[dim]Обычное получение реликвий через Хранителя по-прежнему возможно в нарративе,",
            "но эта команда использует прямое вытягивание из Моря Хаоса.[/]"
        };

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🎰 Гача ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Выберите действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(
                "🎰 Вытянуть реликвию из Моря Хаоса",
                "← Назад"));

        if (choice.Contains("Назад"))
            return;

        if (feathers <= 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ У вас нет Чернильных Перьев для прямого вытягивания.[/]");
            WaitForKey();
            return;
        }

        var inputCost = AnsiConsole.Prompt(new TextPrompt<int>(
            $"[bold yellow]Сколько Перьев потратить на прямое вытягивание? (у вас {feathers} 🪶):[/]")
            .Validate(val =>
            {
                if (val <= 0) return ValidationResult.Error("[red]Нужно потратить хотя бы 1 перо[/]");
                if (val > feathers) return ValidationResult.Error($"[red]У вас только {feathers} 🪶[/]");
                return ValidationResult.Success();
            }));

        var costDisplay = $"{inputCost} 🪶 (останется {feathers - inputCost})";
        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Прямое вытягивание из Моря Хаоса[/]\n" +
                   $"[dim]Текущий Хранитель не участвует. Модификаторы будут нейтральными.[/]\n" +
                   $"[bold]Потратить {Markup.Escape(costDisplay)} на вытягивание реликвии?[/]")
            .AddChoices("✅ Да, тянуть", "❌ Отмена"));
        if (confirm.Contains("Отмена"))
            return;

        if (!await DeductInkFeathers(inputCost))
        {
            AnsiConsole.MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.MarkupLine($"[green]✅ Списано {inputCost} 🪶. Вы вытягиваете реликвию напрямую из Моря Хаоса.[/]");
        WaitForKey();

        _pendingGmAction =
            $"[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит {inputCost} Чернильных Перьев. " +
            "Это НЕ гача через текущего Хранителя: не применять репутацию Хранителя, его скидки, штрафы, социальные факторы, улучшенные или ухудшенные шансы. " +
            "Результат должен быть нейтральным и опираться на turn_request.gachaBaseResult.baseRarity без дополнительных модификаторов. " +
            "Реликвию нужно добавить напрямую в soul state игрока через metaStateUpdates.soulRelicOperations.addRelic. Перья уже списаны клиентом.";
    }

    // ═══ New commands: Effects, Combat, Weather/Time, Chronicle ═══

    private async Task ShowEffects()
    {
        var content = new Grid().AddColumn(new GridColumn());

        // Active effects
        var effDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/effects.json");
        if (effDoc != null)
        {
            content.AddRow(new Markup("[bold yellow]⚡ Активные эффекты:[/]"));
            var effectTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .Expand()
                .AddColumn(new TableColumn("[bold]Эффект[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Влияние[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Длительность[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Источник / цель[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Пояснение[/]"));
            var hasEffects = false;
            EnumerateJsonItems(effDoc.RootElement, item =>
            {
                hasEffects = true;
                var etype = GetStr(item, "effectType", GetStr(item, "type", "?"));
                var val = GetStr(item, "value", "");
                var dur = GetStr(item, "duration", "?");
                var desc = GetStr(item, "effectDescription", GetStr(item, "description", ""));
                var target = GetStr(item, "targetTypeDisplayName", GetStr(item, "targetType", ""));
                var source = GetStr(item, "sourceSkill", GetStr(item, "source", ""));
                var color = etype.ToLowerInvariant() switch
                {
                    "buff" or "heal" or "healovertime" => "green",
                    "debuff" or "damage" or "damageovertime" or "control" => "red",
                    "damagereduction" => "cyan",
                    _ => "yellow"
                };
                var impact = string.IsNullOrWhiteSpace(val) ? "—" : Markup.Escape(val);
                var sourceTarget = string.Join(" • ", new[] { source, target }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Markup.Escape));
                effectTable.AddRow(
                    $"[{color}]{Markup.Escape(etype)}[/]",
                    $"[{color}]{impact}[/]",
                    $"[white]{Markup.Escape(dur)}[/]",
                    string.IsNullOrWhiteSpace(sourceTarget) ? "[dim]—[/]" : $"[white]{sourceTarget}[/]",
                    string.IsNullOrWhiteSpace(desc) ? "[dim]Без дополнительного пояснения[/]" : $"[dim]{Markup.Escape(desc)}[/]");
            });
            if (hasEffects)
                content.AddRow(effectTable);
            else
                content.AddRow(new Markup("[dim]Нет активных эффектов[/]"));
        }
        else
        {
            content.AddRow(new Markup("[dim]Нет активных эффектов[/]"));
        }

        content.AddRow(new Markup(""));
        var wndDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/wounds.json");
        if (wndDoc != null)
        {
            content.AddRow(new Markup("[bold red]🩸 Раны:[/]"));
            var hasWounds = false;
            var woundTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Red)
                .Expand()
                .AddColumn(new TableColumn("[bold]Рана[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Тяжесть[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Последствия[/]"))
                .AddColumn(new TableColumn("[bold]Лечение[/]"));
            EnumerateJsonItems(wndDoc.RootElement, item =>
            {
                hasWounds = true;
                var name = GetStr(item, "woundName", "Рана");
                var sev = GetStr(item, "severity", "?");
                var desc = GetStr(item, "descriptionOfEffects", "");
                var sevColor = sev.ToLower() switch
                {
                    "light" => "yellow",
                    "moderate" => "orange1",
                    "serious" => "red",
                    "critical" => "red bold",
                    _ => "white"
                };

                var effects = new List<string>();
                // Generated effects — mechanical penalties from this wound
                if (item.TryGetProperty("generatedEffects", out var ge) && ge.ValueKind == JsonValueKind.Array && ge.GetArrayLength() > 0)
                {
                    foreach (var eff in ge.EnumerateArray())
                    {
                        var eType = GetStr(eff, "effectType", "?");
                        var eVal = GetStr(eff, "value", "");
                        var eTgt = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                        var eDur = GetStr(eff, "duration", "");
                        var eLine = Markup.Escape(eType);
                        if (!string.IsNullOrWhiteSpace(eVal))
                            eLine += $": {Markup.Escape(eVal)}";
                        if (!string.IsNullOrWhiteSpace(eTgt))
                            eLine += $" → {Markup.Escape(eTgt)}";
                        if (!string.IsNullOrWhiteSpace(eDur) && eDur != "0")
                            eLine += $" ({Markup.Escape(eDur)} ход.)";
                        effects.Add(eLine);
                    }
                }

                var treatment = new List<string>();
                if (item.TryGetProperty("healingState", out var hs) && hs.ValueKind == JsonValueKind.Object)
                {
                    var state = GetStr(hs, "currentState", "");
                    var prog = GetStr(hs, "treatmentProgress", "0");
                    var need = GetStr(hs, "progressNeeded", "?");
                    var hsDesc = GetStr(hs, "description", "");
                    if (!string.IsNullOrWhiteSpace(state))
                        treatment.Add($"{Markup.Escape(state)} ({Markup.Escape(prog)}/{Markup.Escape(need)})");
                    if (!string.IsNullOrEmpty(hsDesc))
                        treatment.Add(Markup.Escape(hsDesc));
                    if (hs.TryGetProperty("canBeImprovedBy", out var cib) && cib.ValueKind == JsonValueKind.Array && cib.GetArrayLength() > 0)
                    {
                        var ways = new List<string>();
                        foreach (var w in cib.EnumerateArray())
                            if (w.ValueKind == JsonValueKind.String) ways.Add(w.GetString() ?? "");
                        if (ways.Count > 0)
                            treatment.Add($"Улучшить: {Markup.Escape(string.Join(", ", ways))}");
                    }
                }

                var effectsText = string.Join("\n", new[] { desc }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Markup.Escape).Concat(effects));
                woundTable.AddRow(
                    $"[{sevColor}]{Markup.Escape(name)}[/]",
                    $"[{sevColor}]{Markup.Escape(sev)}[/]",
                    string.IsNullOrWhiteSpace(effectsText) ? "[dim]Без подробностей[/]" : $"[white]{effectsText}[/]",
                    treatment.Count == 0 ? "[dim]Нет данных о лечении[/]" : $"[cyan]{string.Join("\n", treatment)}[/]");
            });
            if (hasWounds)
                content.AddRow(woundTable);
            else
                content.AddRow(new Markup("[dim green]Ран нет — вы здоровы[/]"));
        }
        else
        {
            content.AddRow(new Markup("[dim green]Ран нет[/]"));
        }

        // Custom states (hunger, thirst, etc.) — Rule 25.1, with thresholds & progression
        var csDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/custom_states.json");
        if (csDoc != null)
        {
            content.AddRow(new Markup(""));
            content.AddRow(new Markup("[bold magenta]📊 Особые состояния:[/]"));
            var hasStates = false;
            var stateLines = new List<string>();
            EnumerateJsonItems(csDoc.RootElement, item =>
            {
                hasStates = true;
                RenderCustomStateItem(stateLines, item, "  ");
            });
            if (hasStates)
                content.AddRow(new Markup(string.Join("\n", stateLines)));
            else
                content.AddRow(new Markup("[dim]Нет особых состояний[/]"));
        }

        // Stealth state
        var stDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/stealth.json");
        if (stDoc != null)
        {
            var sr = stDoc.RootElement;
            var isActive = (sr.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True)
                        || (sr.TryGetProperty("isHidden", out var ih) && ih.ValueKind == JsonValueKind.True);
            var detLevel = GetInt(sr, "detectionLevel", -1);
            var stDesc = GetStr(sr, "description", GetStr(sr, "state", ""));
            if (isActive || detLevel >= 0 || !string.IsNullOrEmpty(stDesc))
            {
                content.AddRow(new Markup(""));
                content.AddRow(new Markup("[bold]🥷 Скрытность:[/]"));
                if (detLevel >= 0)
                {
                    var label = detLevel switch
                    {
                        <= 25 => "Невидим",
                        <= 50 => "Незамечен",
                        <= 75 => "Подозрение",
                        <= 99 => "Тревога",
                        _ => "Обнаружен"
                    };
                    var sColor = detLevel <= 50 ? "green" : detLevel <= 75 ? "yellow" : "red";
                    var stealthTable = new Table()
                        .Border(TableBorder.None)
                        .HideHeaders()
                        .Expand()
                        .AddColumn(new TableColumn("").NoWrap().Width(18))
                        .AddColumn(new TableColumn("").NoWrap().Width(20))
                        .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(16));
                    stealthTable.AddRow(
                        new Markup($"[{sColor}]Степень заметности[/]"),
                        new Markup(ConsoleLayout.CreateBarFromPercent(detLevel, 18, sColor)),
                        new Markup($"[{sColor}]{label} ({detLevel}%)[/]"));
                    content.AddRow(stealthTable);
                }
                else
                {
                    content.AddRow(new Markup(isActive ? "[green]Скрыт[/]" : Markup.Escape(stDesc)));
                }
                if (!string.IsNullOrEmpty(stDesc) && detLevel >= 0)
                    content.AddRow(new Markup($"[dim]{Markup.Escape(stDesc)}[/]"));
            }
        }

        // Experience
        var expDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/experience.json");
        if (expDoc != null)
        {
            content.AddRow(new Markup(""));
            var xp = GetStr(expDoc.RootElement, "experienceGained", "0");
            var totalXp = GetStr(expDoc.RootElement, "totalExperience", "");
            content.AddRow(new Markup($"[bold yellow]✨ Опыт за текущий ход:[/] [yellow]+{Markup.Escape(xp)}[/]"));
            if (!string.IsNullOrEmpty(totalXp))
                content.AddRow(new Markup($"[white]Общий накопленный опыт:[/] {Markup.Escape(totalXp)}"));
        }

        var panel = new Panel(content)
        {
            Header = new PanelHeader(" ⚡ Эффекты и состояния ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowCombat()
    {
        var text = new List<string>();

        // ── Player combat status ──
        var statusDoc = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
        var playerEffDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/effects.json");
        var playerWndDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/wounds.json");
        var transDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/transformation.json");
        if (statusDoc != null)
        {
            var sr = statusDoc.RootElement;
            var hp = GetStr(sr, "healthPercentage", GetStr(sr, "currentHealth", "?"));
            var energy = GetStr(sr, "energyPercentage", GetStr(sr, "currentEnergy", "?"));
            var poise = GetStr(sr, "poisePercentage", GetStr(sr, "currentPoise", ""));
            var condition = GetStr(sr, "currentCondition", "");

            var hpPct = 100; if (hp.Replace("%", "") is var hpStr && int.TryParse(hpStr, out var hpVal)) hpPct = hpVal;
            var hpColor = hpPct > 60 ? "green" : hpPct > 30 ? "yellow" : "red";

            text.Add("[bold white]👤 Ваш статус:[/]");
            text.Add($"  [{hpColor}]❤️ {Markup.Escape(hp)}[/]  [cyan]⚡ {Markup.Escape(energy)}[/]" +
                (!string.IsNullOrEmpty(poise) ? $"  [blue]🛡️ {Markup.Escape(poise)}[/]" : "") +
                (!string.IsNullOrEmpty(condition) ? $"  [yellow]🎭 {Markup.Escape(condition)}[/]" : ""));

            // Quick active effects summary
            if (playerEffDoc != null)
            {
                var buffList = new List<string>();
                var debuffList = new List<string>();
                EnumerateJsonItems(playerEffDoc.RootElement, eff =>
                {
                    var et = GetStr(eff, "effectType", "").ToLower();
                    var eName = GetStr(eff, "effectDescription", GetStr(eff, "description", GetStr(eff, "effectType", "?")));
                    var eDur = GetStr(eff, "duration", "");
                    var label = Markup.Escape(Truncate(eName, 30));
                    if (!string.IsNullOrEmpty(eDur) && eDur != "0") label += $" ({eDur})";
                    if (et is "buff" or "heal" or "healovertime" or "damagereduction")
                        buffList.Add(label);
                    else
                        debuffList.Add(label);
                });
                if (buffList.Count > 0) text.Add($"  [green]⬆ {string.Join(", ", buffList)}[/]");
                if (debuffList.Count > 0) text.Add($"  [red]⬇ {string.Join(", ", debuffList)}[/]");
            }

            // Wounds summary
            if (playerWndDoc != null)
            {
                var wounds = new List<string>();
                EnumerateJsonItems(playerWndDoc.RootElement, w =>
                {
                    var wName = GetStr(w, "woundName", "Рана");
                    var wSev = GetStr(w, "severity", "");
                    wounds.Add($"{Markup.Escape(wName)} ({Markup.Escape(wSev)})");
                });
                if (wounds.Count > 0) text.Add($"  [red]🩸 Раны: {string.Join(", ", wounds)}[/]");
            }

            // Auto-combat skill
            if (transDoc != null)
            {
                var autoSkill = GetStr(transDoc.RootElement, "playerAutoCombatSkillChange", GetStr(transDoc.RootElement, "autoCombatSkill", ""));
                if (!string.IsNullOrEmpty(autoSkill))
                    text.Add($"  [cyan]⚔ Авто-бой: {Markup.Escape(autoSkill)}[/]");
            }

            text.Add("");
        }

        // Enemies
        var enemDoc = await _stateManager.LoadGameStateFileAsync("game_state/combat/enemies.json");
        if (enemDoc != null)
        {
            text.Add("[bold red]⚔️ Враги:[/]");
            var hasEnemies = false;
            EnumerateJsonItems(enemDoc.RootElement, item =>
            {
                hasEnemies = true;
                var name = GetStr(item, "name", "???");
                var hp = GetStr(item, "currentHealth", "?");
                var maxHp = GetStr(item, "maxHealth", "?");
                var poise = GetStr(item, "currentPoise", "");
                var maxPoise = GetStr(item, "maxPoise", "");
                var etype = GetStr(item, "type", "");
                var desc = GetStr(item, "description", "");
                var isGroup = item.TryGetProperty("isGroup", out var ig) && ig.ValueKind == JsonValueKind.True;

                var typeColor = etype.ToLower() switch
                {
                    "boss" => "red bold", "strong" => "orange1", "moderate" => "yellow",
                    "weak" => "green", "frail" => "dim", _ => "white"
                };
                text.Add($"  [{typeColor}]{Markup.Escape(name)}[/] [dim]({Markup.Escape(etype)})[/]");

                if (isGroup)
                {
                    var count = GetStr(item, "count", "?");
                    var unitName = GetStr(item, "unitName", "");
                    var groupLabel = !string.IsNullOrEmpty(unitName) ? $"{Markup.Escape(count)} × {Markup.Escape(unitName)}" : $"{Markup.Escape(count)} ед.";
                    text.Add($"    Группа: {groupLabel}");
                    if (item.TryGetProperty("healthStates", out var hs) && hs.ValueKind == JsonValueKind.Array)
                    {
                        var states = new List<string>();
                        foreach (var s in hs.EnumerateArray()) states.Add(s.ToString());
                        text.Add($"    Здоровье: {Markup.Escape(string.Join(", ", states))}");
                    }
                }
                else
                {
                    text.Add($"    ❤️ HP: {Markup.Escape(hp)}/{Markup.Escape(maxHp)}");
                }
                if (!string.IsNullOrEmpty(poise))
                {
                    var poiseLabel = !string.IsNullOrEmpty(maxPoise) ? $"{Markup.Escape(poise)}/{Markup.Escape(maxPoise)}" : Markup.Escape(poise);
                    text.Add($"    🛡️ Стойкость: {poiseLabel}");
                }

                // Resistances
                if (item.TryGetProperty("resistances", out var res) && res.ValueKind == JsonValueKind.Array && res.GetArrayLength() > 0)
                {
                    text.Add("    🔰 Сопротивления:");
                    foreach (var r in res.EnumerateArray())
                    {
                        var rName = GetStr(r, "resistanceName", "?");
                        var rVal = GetStr(r, "resistanceValue", "?");
                        var rType = GetStr(r, "resistTypeDisplayName", GetStr(r, "resistType", ""));
                        var rLine = $"      • [cyan]{Markup.Escape(rName)}[/]: [white]{Markup.Escape(rVal)}[/]";
                        if (!string.IsNullOrEmpty(rType)) rLine += $" [dim]({Markup.Escape(rType)})[/]";
                        text.Add(rLine);
                    }
                }

                // Actions
                if (item.TryGetProperty("actions", out var acts) && acts.ValueKind == JsonValueKind.Array && acts.GetArrayLength() > 0)
                {
                    text.Add("    [bold]Действия:[/]");
                    foreach (var act in acts.EnumerateArray())
                    {
                        var aName = GetStr(act, "actionName", "?");
                        var aCost = GetStr(act, "actionCost", "");
                        var priority = GetStr(act, "targetPriority", "");
                        var isGroupAction = act.TryGetProperty("isGroupAction", out var iga) && iga.ValueKind == JsonValueKind.True;
                        var attacksPerTurn = GetStr(act, "attacksPerTurn", "");
                        var costLabel = aCost.ToLower() switch
                        {
                            "main" or "основное" => "[red](осн.)[/]",
                            "fast" or "быстрое" => "[yellow](быстр.)[/]",
                            "free" or "свободное" => "[green](своб.)[/]",
                            _ => ""
                        };
                        var actionLine = $"      ⚡ [yellow]{Markup.Escape(aName)}[/]";
                        if (!string.IsNullOrEmpty(costLabel)) actionLine += $" {costLabel}";
                        if (isGroupAction) actionLine += " [magenta](групп.)[/]";
                        if (!string.IsNullOrEmpty(attacksPerTurn) && attacksPerTurn != "1")
                            actionLine += $" [dim](×{Markup.Escape(attacksPerTurn)} атак/ход)[/]";
                        if (!string.IsNullOrEmpty(priority))
                            actionLine += $" [dim](цель: {Markup.Escape(priority)})[/]";
                        text.Add(actionLine);
                        if (act.TryGetProperty("effects", out var effs) && effs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var eff in effs.EnumerateArray())
                            {
                                var effType = GetStr(eff, "effectType", "?");
                                var effVal = GetStr(eff, "value", "");
                                var effTgt = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                                var effDur = GetStr(eff, "duration", "");
                                var effDesc = GetStr(eff, "effectDescription", "");
                                var poiseDmg = GetStr(eff, "poiseDamage", "");
                                var tgtCount = GetStr(eff, "targetsCount", "");
                                var effLine = $"        [{(effType.ToLower().Contains("damage") ? "red" : "cyan")}]{Markup.Escape(effType)}[/] {Markup.Escape(effVal)}";
                                if (!string.IsNullOrEmpty(effTgt)) effLine += $" → {Markup.Escape(effTgt)}";
                                if (!string.IsNullOrEmpty(poiseDmg) && poiseDmg != "0") effLine += $" [dim](🛡️ -{Markup.Escape(poiseDmg)} стойк.)[/]";
                                if (!string.IsNullOrEmpty(tgtCount) && tgtCount != "1") effLine += $" [dim](×{Markup.Escape(tgtCount)} целей)[/]";
                                if (!string.IsNullOrEmpty(effDur) && effDur != "0") effLine += $" [dim]({Markup.Escape(effDur)} ход.)[/]";
                                text.Add(effLine);
                                if (!string.IsNullOrEmpty(effDesc))
                                    text.Add($"          [dim]{Markup.Escape(effDesc)}[/]");
                            }
                        }
                    }
                }

                // Buffs/Debuffs (expanded)
                void RenderCombatEffectList(JsonElement arr, string label, string color)
                {
                    if (arr.GetArrayLength() == 0) return;
                    text.Add($"    [{color}]{label}:[/]");
                    foreach (var b in arr.EnumerateArray())
                    {
                        var bType = GetStr(b, "effectType", GetStr(b, "description", "?"));
                        var bVal = GetStr(b, "value", "");
                        var bDur = GetStr(b, "duration", "");
                        var bSrc = GetStr(b, "sourceSkill", "");
                        var line = $"      [{color}]{Markup.Escape(bType)}[/] {Markup.Escape(bVal)}";
                        if (!string.IsNullOrEmpty(bDur) && bDur != "0") line += $" [dim]({Markup.Escape(bDur)} ход.)[/]";
                        if (!string.IsNullOrEmpty(bSrc)) line += $" [dim]от {Markup.Escape(bSrc)}[/]";
                        text.Add(line);
                    }
                }
                if (item.TryGetProperty("activeBuffs", out var buffs) && buffs.ValueKind == JsonValueKind.Array)
                    RenderCombatEffectList(buffs, "Баффы", "green");
                if (item.TryGetProperty("activeDebuffs", out var debuffs) && debuffs.ValueKind == JsonValueKind.Array)
                    RenderCombatEffectList(debuffs, "Дебаффы", "red");

                if (!string.IsNullOrEmpty(desc))
                    text.Add($"    [dim]{Markup.Escape(desc)}[/]");
                text.Add("");
            });
            if (!hasEnemies) text.Add("  [dim]Нет врагов[/]");
        }

        // Allies (full data, same as enemies)
        var allyDoc = await _stateManager.LoadGameStateFileAsync("game_state/combat/allies.json");
        if (allyDoc != null)
        {
            text.Add("[bold green]🤝 Союзники:[/]");
            var hasAllies = false;
            EnumerateJsonItems(allyDoc.RootElement, item =>
            {
                hasAllies = true;
                var name = GetStr(item, "name", "???");
                var hp = GetStr(item, "currentHealth", "?");
                var maxHp = GetStr(item, "maxHealth", "?");
                var poise = GetStr(item, "currentPoise", "");
                var maxPoise = GetStr(item, "maxPoise", "");
                var atype = GetStr(item, "type", "");
                var desc = GetStr(item, "description", "");
                var isAllyGroup = item.TryGetProperty("isGroup", out var aig) && aig.ValueKind == JsonValueKind.True;

                text.Add($"  [green]{Markup.Escape(name)}[/] [dim]({Markup.Escape(atype)})[/]");

                if (isAllyGroup)
                {
                    var allyCount = GetStr(item, "count", "?");
                    var allyUnit = GetStr(item, "unitName", "");
                    var grpLabel = !string.IsNullOrEmpty(allyUnit) ? $"{Markup.Escape(allyCount)} × {Markup.Escape(allyUnit)}" : $"{Markup.Escape(allyCount)} ед.";
                    text.Add($"    Группа: {grpLabel}");
                    if (item.TryGetProperty("healthStates", out var ahs) && ahs.ValueKind == JsonValueKind.Array)
                    {
                        var aStates = new List<string>();
                        foreach (var s in ahs.EnumerateArray()) aStates.Add(s.ToString());
                        text.Add($"    Здоровье: {Markup.Escape(string.Join(", ", aStates))}");
                    }
                }
                else
                {
                    text.Add($"    ❤️ HP: {Markup.Escape(hp)}/{Markup.Escape(maxHp)}");
                }

                if (!string.IsNullOrEmpty(poise))
                {
                    var poiseLabel = !string.IsNullOrEmpty(maxPoise) ? $"{Markup.Escape(poise)}/{Markup.Escape(maxPoise)}" : Markup.Escape(poise);
                    text.Add($"    🛡️ Стойкость: {poiseLabel}");
                }
                // Resistances
                if (item.TryGetProperty("resistances", out var res) && res.ValueKind == JsonValueKind.Array && res.GetArrayLength() > 0)
                {
                    text.Add("    🔰 Сопротивления:");
                    foreach (var r in res.EnumerateArray())
                    {
                        var rName = GetStr(r, "resistanceName", "?");
                        var rVal = GetStr(r, "resistanceValue", "?");
                        var rType = GetStr(r, "resistTypeDisplayName", GetStr(r, "resistType", ""));
                        var rLine = $"      • [cyan]{Markup.Escape(rName)}[/]: [white]{Markup.Escape(rVal)}[/]";
                        if (!string.IsNullOrEmpty(rType)) rLine += $" [dim]({Markup.Escape(rType)})[/]";
                        text.Add(rLine);
                    }
                }
                // Buffs/Debuffs — full details with sourceSkill
                void RenderAllyEffectList(JsonElement arr, string label, string color)
                {
                    if (arr.GetArrayLength() == 0) return;
                    text.Add($"    [{color}]{label}:[/]");
                    foreach (var b in arr.EnumerateArray())
                    {
                        var bType = GetStr(b, "effectType", GetStr(b, "description", "?"));
                        var bVal = GetStr(b, "value", "");
                        var bDur = GetStr(b, "duration", "");
                        var bSrc = GetStr(b, "sourceSkill", "");
                        var bLine = $"      [{color}]{Markup.Escape(bType)}[/] {Markup.Escape(bVal)}";
                        if (!string.IsNullOrEmpty(bDur) && bDur != "0") bLine += $" [dim]({Markup.Escape(bDur)} ход.)[/]";
                        if (!string.IsNullOrEmpty(bSrc)) bLine += $" [dim]от {Markup.Escape(bSrc)}[/]";
                        text.Add(bLine);
                    }
                }
                if (item.TryGetProperty("activeBuffs", out var ab) && ab.ValueKind == JsonValueKind.Array)
                    RenderAllyEffectList(ab, "Баффы", "green");
                if (item.TryGetProperty("activeDebuffs", out var ad) && ad.ValueKind == JsonValueKind.Array)
                    RenderAllyEffectList(ad, "Дебаффы", "red");
                // Actions — full Combat Action Object rendering (same as enemies)
                if (item.TryGetProperty("actions", out var acts) && acts.ValueKind == JsonValueKind.Array && acts.GetArrayLength() > 0)
                {
                    text.Add("    [bold]Действия:[/]");
                    foreach (var act in acts.EnumerateArray())
                    {
                        var aName = GetStr(act, "actionName", GetStr(act, "name", "?"));
                        var aCost = GetStr(act, "actionCost", "");
                        var aPriority = GetStr(act, "targetPriority", "");
                        var costLabel = aCost.ToLower() switch
                        {
                            "main" or "основное" => "[red](осн.)[/]",
                            "fast" or "быстрое" => "[yellow](быстр.)[/]",
                            "free" or "свободное" => "[green](своб.)[/]",
                            _ => ""
                        };
                        var actionLine = $"      ⚡ [yellow]{Markup.Escape(aName)}[/]";
                        if (!string.IsNullOrEmpty(costLabel)) actionLine += $" {costLabel}";
                        if (!string.IsNullOrEmpty(aPriority))
                            actionLine += $" [dim](цель: {Markup.Escape(aPriority)})[/]";
                        text.Add(actionLine);
                        if (act.TryGetProperty("effects", out var effs) && effs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var eff in effs.EnumerateArray())
                            {
                                var effType = GetStr(eff, "effectType", "?");
                                var effVal = GetStr(eff, "value", "");
                                var effTgt = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                                var effDur = GetStr(eff, "duration", "");
                                var effDesc = GetStr(eff, "effectDescription", "");
                                var poiseDmg = GetStr(eff, "poiseDamage", "");
                                var tgtCount = GetStr(eff, "targetsCount", "");
                                var effLine = $"        [{(effType.ToLower().Contains("damage") ? "red" : "cyan")}]{Markup.Escape(effType)}[/] {Markup.Escape(effVal)}";
                                if (!string.IsNullOrEmpty(effTgt)) effLine += $" → {Markup.Escape(effTgt)}";
                                if (!string.IsNullOrEmpty(poiseDmg) && poiseDmg != "0") effLine += $" [dim](🛡️ -{Markup.Escape(poiseDmg)} стойк.)[/]";
                                if (!string.IsNullOrEmpty(tgtCount) && tgtCount != "1") effLine += $" [dim](×{Markup.Escape(tgtCount)} целей)[/]";
                                if (!string.IsNullOrEmpty(effDur) && effDur != "0") effLine += $" [dim]({Markup.Escape(effDur)} ход.)[/]";
                                text.Add(effLine);
                                if (!string.IsNullOrEmpty(effDesc))
                                    text.Add($"          [dim]{Markup.Escape(effDesc)}[/]");
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(desc))
                    text.Add($"    [dim]{Markup.Escape(desc)}[/]");
                text.Add("");
            });
            if (!hasAllies) text.Add("  [dim]Нет союзников[/]");
        }

        // Combat log
        var logDoc = await _stateManager.LoadGameStateFileAsync("game_state/combat/combat_log.json");
        if (logDoc != null)
        {
            var log = GetStr(logDoc.RootElement, "combat_log_markdown", "");
            if (!string.IsNullOrEmpty(log))
            {
                text.Add("");
                var logLines = log.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                text.Add($"[bold]📜 Боевой журнал[/] [dim]({logLines.Length} строк)[/]:");
                for (int i = 0; i < logLines.Length; i++)
                    text.Add($"  [dim]{Markup.Escape(logLines[i].Trim())}[/]");
            }
        }

        if (text.Count == 0)
        {
            text.Add("[dim]Нет данных о бое. Вы не в сражении.[/]");
        }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" ⚔️ Боевая обстановка ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowWeatherTime()
    {
        var text = new List<string>();

        var timeDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_time.json");
        if (timeDoc != null)
        {
            AppendWorldTimeLines(text, timeDoc.RootElement, "  ");
        }
        else
        {
            text.Add("[dim]Время неизвестно[/]");
        }

        text.Add("");
        var wDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/weather.json");
        if (wDoc != null)
        {
            var wr = GetWeatherRoot(wDoc.RootElement);
            var desc = GetStr(wr, "description", "");
            var tendency = GetStr(wr, "tendency", "");
            var season = GetStr(wr, "season", "");
            var temp = GetStr(wr, "temperature", "");
            var wind = GetStr(wr, "windSpeed", GetStr(wr, "wind", ""));
            var visibility = GetStr(wr, "visibility", "");
            var mechEffects = GetStr(wr, "mechanicalEffects", "");

            text.Add("[bold cyan]🌤️ Погода:[/]");
            // Show biome context for weather interpretation (Block 27)
            var locDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
            if (locDoc != null)
            {
                var biome = GetStr(locDoc.RootElement, "biome", "");
                if (!string.IsNullOrEmpty(biome))
                    text.Add($"  🌍 Биом: [white]{Markup.Escape(biome)}[/]");
            }
            var weatherState = GetStr(wr, "currentState", GetStr(wr, "state", ""));
            if (!string.IsNullOrEmpty(weatherState))
                text.Add($"  ☁ Состояние: [bold white]{Markup.Escape(weatherState)}[/]");
            if (!string.IsNullOrEmpty(desc))
                text.Add($"  {Markup.Escape(desc)}");
            if (!string.IsNullOrEmpty(season))
                text.Add($"  🍂 Сезон: [white]{Markup.Escape(season)}[/]");
            if (!string.IsNullOrEmpty(temp))
                text.Add($"  🌡️ Температура: [white]{Markup.Escape(temp)}[/]");
            if (!string.IsNullOrEmpty(wind))
                text.Add($"  💨 Ветер: [white]{Markup.Escape(wind)}[/]");
            if (!string.IsNullOrEmpty(visibility))
                text.Add($"  👁 Видимость: [white]{Markup.Escape(visibility)}[/]");
            if (!string.IsNullOrEmpty(tendency) && tendency != "NO_CHANGE")
            {
                var tendLabel = tendency switch
                {
                    "IMPROVE" => "[green]Улучшение ↑[/]",
                    "WORSEN" => "[red]Ухудшение ↓[/]",
                    _ when tendency.StartsWith("JUMP_TO_") => $"[yellow]→ {Markup.Escape(tendency.Replace("JUMP_TO_", ""))}[/]",
                    _ => $"[yellow]{Markup.Escape(tendency)}[/]"
                };
                text.Add($"  📈 Тенденция: {tendLabel}");
            }
            if (!string.IsNullOrEmpty(mechEffects))
                text.Add($"  ⚙ Эффекты: [dim]{Markup.Escape(mechEffects)}[/]");
        }
        else
        {
            text.Add("[dim]Данные о погоде недоступны[/]");
        }

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🌤️ Время и погода ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowStory()
    {
        if (_storyService == null)
        {
            AnsiConsole.MarkupLine("[dim]Сервис рассказов недоступен.[/]");
            WaitForKey();
            return;
        }

        var stories = _storyService.GetAvailableStories();
        if (stories.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("[dim]Рассказ пока пуст. Сыграйте несколько ходов, и ваша история начнёт записываться.[/]"))
            {
                Header = new PanelHeader(" 📜 Рассказ ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1)
            });
            WaitForKey();
            return;
        }

        var currentStoryPath = Services.StoryService.GetStoryPath(
            _stateManager.CurrentState.CurrentRealm ?? "Chaos Sea",
            _stateManager.CurrentState.Incarnation);

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[gold1]📜 Рассказ — Ваша История[/]").RuleStyle("gold1"));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Каждый ваш ход записывается в вечную книгу. Здесь вы можете перечитать свою историю из Мира Смертных и Моря Хаоса.[/]");
            AnsiConsole.WriteLine();

            var choices = stories.Select(s =>
            {
                var isCurrent = string.Equals(s.RelativePath, currentStoryPath, StringComparison.OrdinalIgnoreCase);
                var currentTag = isCurrent ? " [green](текущая глава)[/]" : "";
                return $"📖 {s.DisplayName} ({s.EntryCount} записей){currentTag}";
            }).ToList();
            choices.Add("💾 Экспортировать всё в .txt");
            choices.Add("[dim]← Назад[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Выберите главу:[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            var selIdx = choices.IndexOf(selected);
            if (selIdx == stories.Count)
            {
                // Export all stories
                await ExportAllStoriesToTxt(stories);
                continue;
            }
            if (selIdx < 0 || selIdx >= stories.Count) break;

            var story = stories[selIdx];
            await ShowStoryReader(story);
        }
    }

    private async Task ShowStoryReader(Services.StoryFileInfo storyInfo)
    {
        if (_storyService == null) return;

        const int pageSize = 20;
        var allEntries = await _storyService.ReadStoryAsync(storyInfo.RelativePath);
        if (allEntries.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Эта глава пока пуста.[/]");
            WaitForKey();
            return;
        }

        var totalPages = (allEntries.Count + pageSize - 1) / pageSize;
        var currentPage = totalPages - 1; // Start from the latest page

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[gold1]📖 {Markup.Escape(storyInfo.DisplayName)}[/]").RuleStyle("gold1"));
            AnsiConsole.MarkupLine($"[dim]Страница {currentPage + 1} из {totalPages} | {allEntries.Count} записей[/]\n");

            var startIdx = currentPage * pageSize;
            var endIdx = Math.Min(startIdx + pageSize, allEntries.Count);

            for (var i = startIdx; i < endIdx; i++)
            {
                var e = allEntries[i];
                var isMarker = e.Turn < 0;

                if (isMarker)
                {
                    // Transition marker
                    AnsiConsole.Write(new Rule($"[yellow]✦ {Markup.Escape(e.Player.Trim('[', ']'))} ✦[/]").RuleStyle("yellow"));
                    if (!string.IsNullOrEmpty(e.Narrative))
                        AnsiConsole.MarkupLine($"  [italic yellow]{Markup.Escape(e.Narrative)}[/]");
                    AnsiConsole.WriteLine();
                    continue;
                }

                // Regular turn
                var tsDisplay = DateTime.TryParse(e.Timestamp, out var dt) ? dt.ToLocalTime().ToString("dd.MM HH:mm") : "";
                var locStr = !string.IsNullOrEmpty(e.Location) ? $" [dim]📍 {Markup.Escape(e.Location)}[/]" : "";

                AnsiConsole.MarkupLine($"[dim]─── Ход {e.Turn} {tsDisplay}{locStr} ───[/]");
                AnsiConsole.MarkupLine($"  [cyan]▸ {Markup.Escape(e.Player)}[/]");
                if (!string.IsNullOrEmpty(e.Narrative))
                    AnsiConsole.MarkupLine($"  [white]{Markup.Escape(e.Narrative)}[/]");
                AnsiConsole.WriteLine();
            }

            // Navigation
            var navChoices = new List<string>();
            if (currentPage > 0) navChoices.Add("◀ Предыдущая страница");
            if (currentPage < totalPages - 1) navChoices.Add("▶ Следующая страница");
            navChoices.Add("⏮ В начало");
            navChoices.Add("⏭ В конец");
            navChoices.Add("💾 Экспортировать главу в .txt");
            navChoices.Add("← Назад к списку");

            var nav = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[dim]Страница {currentPage + 1}/{totalPages}[/]")
                    .PageSize(8)
                    .AddChoices(navChoices));

            if (nav.Contains("Предыдущая")) currentPage--;
            else if (nav.Contains("Следующая")) currentPage++;
            else if (nav.Contains("В начало")) currentPage = 0;
            else if (nav.Contains("В конец")) currentPage = totalPages - 1;
            else if (nav.Contains("Экспортировать")) { await ExportStoryToTxt(storyInfo.DisplayName, allEntries); }
            else break;

            currentPage = Math.Clamp(currentPage, 0, totalPages - 1);
        }
    }

    private static string FormatStoryEntryAsText(Services.StoryEntry e)
    {
        if (e.Turn < 0)
        {
            // Marker entry
            var marker = e.Player.Trim('[', ']');
            return $"══════════════ {marker} ══════════════\n{e.Narrative}\n";
        }

        var sb = new System.Text.StringBuilder();
        var tsDisplay = DateTime.TryParse(e.Timestamp, out var dt)
            ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : e.Timestamp;
        var locStr = !string.IsNullOrEmpty(e.Location) ? $" | {e.Location}" : "";

        sb.AppendLine($"--- Ход {e.Turn} | {tsDisplay}{locStr} ---");
        sb.AppendLine($"> {e.Player}");
        if (!string.IsNullOrEmpty(e.Narrative))
        {
            sb.AppendLine();
            sb.AppendLine(e.Narrative);
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task ExportStoryToTxt(string chapterName, List<Services.StoryEntry> entries)
    {
        try
        {
            var safeName = string.Join("_", chapterName.Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{safeName}_{timestamp}.txt";
            var exportDir = _fs.ResolvePath("stories/export");
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
            var fullPath = Path.Combine(exportDir, fileName);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔══════════════════════════════════════════╗");
            sb.AppendLine($"║  {chapterName}");
            sb.AppendLine($"║  Экспорт: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"║  Записей: {entries.Count}");
            sb.AppendLine($"╚══════════════════════════════════════════╝");
            sb.AppendLine();

            foreach (var e in entries)
                sb.Append(FormatStoryEntryAsText(e));

            await File.WriteAllTextAsync(fullPath, sb.ToString(), System.Text.Encoding.UTF8);

            AnsiConsole.MarkupLine($"\n[green]Экспортировано:[/] [link]{Markup.Escape(fullPath)}[/]");
            AnsiConsole.MarkupLine($"[dim]{entries.Count} записей сохранено.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка экспорта: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    private async Task ExportAllStoriesToTxt(List<Services.StoryFileInfo> stories)
    {
        if (_storyService == null) return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"Полная_История_{timestamp}.txt";
            var exportDir = _fs.ResolvePath("stories/export");
            if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
            var fullPath = Path.Combine(exportDir, fileName);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════╗");
            sb.AppendLine("║     Книга Вечности — Полная История     ║");
            sb.AppendLine($"║  Экспорт: {DateTime.Now:dd.MM.yyyy HH:mm}                  ║");
            sb.AppendLine("╚══════════════════════════════════════════╝");
            sb.AppendLine();

            var totalEntries = 0;
            foreach (var story in stories)
            {
                var entries = await _storyService.ReadStoryAsync(story.RelativePath);
                if (entries.Count == 0) continue;

                sb.AppendLine();
                sb.AppendLine($"████████████████████████████████████████████");
                sb.AppendLine($"  {story.DisplayName}");
                sb.AppendLine($"  ({entries.Count} записей)");
                sb.AppendLine($"████████████████████████████████████████████");
                sb.AppendLine();

                foreach (var e in entries)
                    sb.Append(FormatStoryEntryAsText(e));

                totalEntries += entries.Count;
            }

            await File.WriteAllTextAsync(fullPath, sb.ToString(), System.Text.Encoding.UTF8);

            AnsiConsole.MarkupLine($"\n[green]Экспортировано:[/] [link]{Markup.Escape(fullPath)}[/]");
            AnsiConsole.MarkupLine($"[dim]{stories.Count} глав, {totalEntries} записей сохранено.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка экспорта: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    private async Task ShowChronicle()
    {
        var text = new List<string>();

        var chrDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/character_chronicle.json");
        if (chrDoc != null)
        {
            int idx = 0;
            EnumerateJsonItems(chrDoc.RootElement, item =>
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString() ?? "";
                    if (!string.IsNullOrEmpty(s))
                        text.Add($"  📖 {Markup.Escape(s)}");
                    return;
                }

                // Structured entry with title/content/timestamp
                var title = GetStr(item, "title", "");
                var content = GetStr(item, "content",
                    GetStr(item, "entryToAppend",
                        GetStr(item, "entry",
                            GetStr(item, "description", ""))));
                var timestamp = GetStr(item, "timestamp", "");
                var chapterId = GetStr(item, "chapterId", "");

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
                    return;

                if (idx > 0) text.Add("");

                // Chapter header
                if (!string.IsNullOrEmpty(title))
                    text.Add($"  [bold yellow]📖 {Markup.Escape(title)}[/]");

                // Timestamp / turn number
                var turnNumber = GetStr(item, "turnNumber", GetStr(item, "turn", ""));
                if (!string.IsNullOrEmpty(timestamp) || !string.IsNullOrEmpty(turnNumber))
                {
                    var tsLine = "    [dim]";
                    if (!string.IsNullOrEmpty(turnNumber))
                        tsLine += $"🔄 Ход {Markup.Escape(turnNumber)}";
                    if (!string.IsNullOrEmpty(timestamp))
                    {
                        var tsDisplay = DateTime.TryParse(timestamp, out var dt)
                            ? dt.ToString("dd.MM.yyyy HH:mm")
                            : timestamp;
                        if (!string.IsNullOrEmpty(turnNumber)) tsLine += " — ";
                        tsLine += $"🕐 {Markup.Escape(tsDisplay)}";
                    }
                    tsLine += "[/]";
                    text.Add(tsLine);
                }

                // Content body
                if (!string.IsNullOrEmpty(content))
                    text.Add($"    {Markup.Escape(content)}");

                // Any extra fields we don't explicitly handle
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        if (prop.Name is "title" or "content" or "timestamp" or "chapterId"
                            or "entryToAppend" or "entry" or "description"
                            or "turnNumber" or "turn"
                            || prop.Name.StartsWith("_")) continue;
                        var label = NpcFieldToRussian(prop.Name);
                        var val = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? ""
                            : prop.Value.ToString();
                        if (!string.IsNullOrEmpty(val))
                            text.Add($"    [dim]{Markup.Escape(label)}: {Markup.Escape(val)}[/]");
                    }
                }

                idx++;
            });
        }

        var playerChronicleDoc = await _stateManager.LoadGameStateFileAsync("lore/chaos_sea/player_chronicle.json");
        if (playerChronicleDoc != null &&
            playerChronicleDoc.RootElement.TryGetProperty("entries", out var chronicleEntries) &&
            chronicleEntries.ValueKind == JsonValueKind.Array &&
            chronicleEntries.GetArrayLength() > 0)
        {
            if (text.Count > 0)
                text.Add("");
            text.Add("[bold]🌊 Хроника Душ:[/]");

            foreach (var entry in chronicleEntries.EnumerateArray())
            {
                var title = GetStr(entry, "title", GetStr(entry, "lifeTitle", ""));
                var summary = GetStr(entry, "summary", GetStr(entry, "description", GetStr(entry, "content", "")));
                var timestamp = GetStr(entry, "timestamp", GetStr(entry, "completedAtUtc", ""));

                if (!string.IsNullOrWhiteSpace(title))
                    text.Add($"  [bold yellow]{Markup.Escape(title)}[/]");
                if (!string.IsNullOrWhiteSpace(summary))
                    text.Add($"  {Markup.Escape(summary)}");
                if (!string.IsNullOrWhiteSpace(timestamp))
                    text.Add($"  [dim]{Markup.Escape(timestamp)}[/]");
                text.Add("");
            }
        }

        // Plot outline (Block 22 — mainArc, characterSubplots, loomingThreatsOrOpportunities)
        var plotDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/plot_outline.json");
        if (plotDoc != null)
        {
            var plotItems = new List<string>();
            var root = plotDoc.RootElement;

            // Main Arc
            if (root.TryGetProperty("mainArc", out var mainArc) && mainArc.ValueKind == JsonValueKind.Object)
            {
                var summary = GetStr(mainArc, "summary", "");
                var nextStep = GetStr(mainArc, "nextImmediateStep", "");
                var climax = GetStr(mainArc, "potentialClimax", "");
                if (!string.IsNullOrEmpty(summary))
                {
                    plotItems.Add($"  [bold]🎯 Главная арка:[/] [white]{Markup.Escape(summary)}[/]");
                    if (!string.IsNullOrEmpty(nextStep))
                        plotItems.Add($"    ➤ Следующий шаг: [green]{Markup.Escape(nextStep)}[/]");
                    if (!string.IsNullOrEmpty(climax))
                        plotItems.Add($"    ⚡ Возможная кульминация: [dim]{Markup.Escape(climax)}[/]");
                }
            }

            // Character Subplots
            if (root.TryGetProperty("characterSubplots", out var subplots) && subplots.ValueKind == JsonValueKind.Array && subplots.GetArrayLength() > 0)
            {
                plotItems.Add("");
                plotItems.Add("  [bold]👤 Подсюжеты персонажей:[/]");
                foreach (var sp in subplots.EnumerateArray())
                {
                    var charName = GetStr(sp, "characterName", "?");
                    var arcSummary = GetStr(sp, "arcSummary", "");
                    var nextDev = GetStr(sp, "nextStep", GetStr(sp, "nextDevelopment", ""));
                    var conflict = GetStr(sp, "potentialConflictOrResolution", "");
                    plotItems.Add($"    [cyan]{Markup.Escape(charName)}[/]: {Markup.Escape(arcSummary)}");
                    if (!string.IsNullOrEmpty(nextDev))
                        plotItems.Add($"      ➤ {Markup.Escape(nextDev)}");
                    if (!string.IsNullOrEmpty(conflict))
                        plotItems.Add($"      [dim]⚡ {Markup.Escape(conflict)}[/]");
                }
            }

            // Looming Threats or Opportunities
            if (root.TryGetProperty("loomingThreatsOrOpportunities", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
            {
                plotItems.Add("");
                plotItems.Add("  [bold]⚠ Угрозы и возможности:[/]");
                foreach (var t in threats.EnumerateArray())
                {
                    var tText = t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "") : t.GetRawText();
                    if (!string.IsNullOrEmpty(tText))
                        plotItems.Add($"    • [yellow]{Markup.Escape(tText)}[/]");
                }
            }

            // Fallback: generic enumeration for non-Block-22 format
            if (plotItems.Count == 0)
            {
                EnumerateJsonItems(root, item =>
                {
                    var title = GetStr(item, "title", GetStr(item, "name", ""));
                    var desc = GetStr(item, "description", "");
                    if (string.IsNullOrEmpty(title)) return;
                    plotItems.Add($"  📌 [white]{Markup.Escape(title)}[/]");
                    if (!string.IsNullOrEmpty(desc))
                        plotItems.Add($"    [dim]{Markup.Escape(desc)}[/]");
                });
            }

            if (plotItems.Count > 0)
            {
                text.Add("");
                text.Add("[bold yellow]📌 Сюжетная линия:[/]");
                text.AddRange(plotItems);
            }
        }

        if (text.Count == 0) text.Add("[dim]Хроника пуста — ваша история ещё не написана.[/]");

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 📖 Хроника персонажа ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1),
            Expand = true
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    private async Task ShowBehaviorAssessment()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/player_behavior.json");
        if (doc == null)
        {
            ShowEmptyPanel("Оценка поведения", "Данные оценки поведения недоступны");
            return;
        }

        var root = doc.RootElement;
        var assessment = root.TryGetProperty("playerBehaviorAssessment", out var pba) && pba.ValueKind == JsonValueKind.Object
            ? pba
            : root;
        var coeff = root.TryGetProperty("historyManipulationCoefficient", out var hm) && hm.ValueKind == JsonValueKind.Number
            ? hm.GetDouble()
            : (assessment.TryGetProperty("historyManipulationCoefficient", out var nestedHm) && nestedHm.ValueKind == JsonValueKind.Number
                ? nestedHm.GetDouble()
                : 0.0);

        var lines = new List<string>
        {
            "[bold cyan]🧠 Оценка поведения игрока[/]",
            ""
        };

        var coeffColor = coeff switch
        {
            >= 1.0 => "red",
            >= 0.5 => "yellow",
            > 0.0 => "green",
            _ => "grey"
        };
        lines.Add($"  Коэффициент манипуляции историей: [{coeffColor}]{coeff:F2}[/]");

        var coeffMeaning = coeff switch
        {
            >= 1.0 => "Высокий риск грубого вмешательства в историю или правила",
            >= 0.5 => "Заметная попытка повлиять на историю/мета-слой",
            > 0.0 => "Слабые признаки манипуляции историей",
            _ => "Манипулирование историей не обнаружено"
        };
        lines.Add($"  [dim]{Markup.Escape(coeffMeaning)}[/]");

        var known = new[] { "historyManipulationCoefficient" };
        RenderExtraFields(lines, assessment, known, "  ");

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧠 Поведение игрока ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowStorageAccess()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/misc/storage_access.json");
        if (doc == null)
        {
            ShowEmptyPanel("Доступ к хранилищам", "Данные доступа к хранилищам недоступны");
            return;
        }

        var lines = new List<string> { "[bold cyan]📦 Доступ к хранилищам[/]" };
        var rendered = false;
        void RenderAccessArray(string title, JsonElement arr, string color)
        {
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return;
            rendered = true;
            lines.Add("");
            lines.Add($"[bold {color}]{title}:[/]");
            foreach (var item in arr.EnumerateArray())
            {
                var storageId = GetStr(item, "storageId", GetStr(item, "storageName", "хранилище"));
                var playerId = GetStr(item, "playerId", GetStr(item, "targetPlayerId", GetStr(item, "sharedWithPlayerId", "")));
                var line = $"  • {Markup.Escape(storageId)}";
                if (!string.IsNullOrWhiteSpace(playerId))
                    line += $" → [white]{Markup.Escape(playerId)}[/]";
                lines.Add(line);
                RenderExtraFields(lines, item, new[] { "storageId", "storageName", "playerId", "targetPlayerId", "sharedWithPlayerId" }, "    ");
            }
        }

        var root = doc.RootElement;
        if (root.TryGetProperty("grantStorageAccess", out var grants))
            RenderAccessArray("Выдан доступ", grants, "green");
        if (root.TryGetProperty("shareStorageAccess", out var shares))
            RenderAccessArray("Совместный доступ", shares, "yellow");
        if (root.TryGetProperty("revokeStorageAccess", out var revokes))
            RenderAccessArray("Отозван доступ", revokes, "red");

        if (!rendered)
            lines.Add("\n[dim]Нет данных о доступах к хранилищам[/]");

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📦 Storage Access ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowPlayerInteractions()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/misc/player_interactions.json");
        if (doc == null)
        {
            ShowEmptyPanel("Взаимодействия игроков", "Данные взаимодействий недоступны");
            return;
        }

        var lines = new List<string> { "[bold magenta]🤝 Взаимодействия игроков[/]" };
        var root = doc.RootElement;
        var rendered = false;

	        if (root.TryGetProperty("otherPlayersInteractions", out var interactions))
	        {
	            rendered = true;
	            if (interactions.ValueKind == JsonValueKind.Object)
	            {
	                foreach (var playerEntry in interactions.EnumerateObject())
	                {
	                    lines.Add("");
	                    lines.Add($"[bold]👤 Игрок {Markup.Escape(playerEntry.Name)}[/]");

                    void RenderInteractionCommand(string label, JsonElement payload)
                    {
                        lines.Add($"  • [cyan]{Markup.Escape(label)}[/]");
                        if (payload.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in payload.EnumerateObject())
                                RenderReadableJsonValue(lines, prop.Name, prop.Value, "      ");
                        }
                        else if (payload.ValueKind == JsonValueKind.Array)
                        {
                            var arrIndex = 0;
                            foreach (var arrItem in payload.EnumerateArray())
                            {
                                if (arrItem.ValueKind == JsonValueKind.Object)
                                {
                                    lines.Add($"      [dim]элемент {arrIndex + 1}:[/]");
                                    foreach (var prop in arrItem.EnumerateObject())
                                        RenderReadableJsonValue(lines, prop.Name, prop.Value, "        ");
                                }
                                else if (!string.IsNullOrWhiteSpace(arrItem.ToString()))
                                {
                                    lines.Add($"      [dim]{Markup.Escape(arrItem.ToString())}[/]");
                                }
	                                arrIndex++;
	                            }
	                        }
	                        else if (!string.IsNullOrWhiteSpace(payload.ToString()))
	                        {
	                            lines.Add($"      [dim]{Markup.Escape(payload.ToString())}[/]");
	                        }
	                    }

	                    if (playerEntry.Value.ValueKind == JsonValueKind.Object)
	                    {
	                        foreach (var command in playerEntry.Value.EnumerateObject())
	                            RenderInteractionCommand(command.Name, command.Value);
	                    }
	                    else if (playerEntry.Value.ValueKind == JsonValueKind.Array)
	                    {
	                        foreach (var command in playerEntry.Value.EnumerateArray())
	                        {
	                            if (command.ValueKind == JsonValueKind.Object)
	                            {
	                                foreach (var prop in command.EnumerateObject())
	                                    RenderInteractionCommand(prop.Name, prop.Value);
	                            }
	                            else
	                            {
	                                lines.Add($"  • {Markup.Escape(command.ToString())}");
	                            }
                        }
                    }
                    else
                    {
                        lines.Add($"  [dim]{Markup.Escape(playerEntry.Value.ToString())}[/]");
                    }
                }
            }
            else if (interactions.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in interactions.EnumerateArray())
                {
                    var targetPlayer = GetStr(item, "playerId", GetStr(item, "targetPlayerId", "другой игрок"));
                    lines.Add($"  • [white]{Markup.Escape(targetPlayer)}[/]");
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            if (prop.Name is "playerId" or "targetPlayerId")
                                continue;
                            RenderReadableJsonValue(lines, prop.Name, prop.Value, "    ");
                        }
                    }
                }
            }
        }

        if (!rendered)
            lines.Add("\n[dim]Нет данных о взаимодействиях других игроков[/]");

        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🤝 Player Interactions ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Magenta1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    // ═══ Meta-info and additional helpers ═══

    private async Task<bool> IsHistoryManipulationEnabled()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/player_behavior.json");
        if (doc == null) return false;
        var root = doc.RootElement;
        var coeff = root.TryGetProperty("historyManipulationCoefficient", out var hm) && hm.ValueKind == JsonValueKind.Number
            ? hm.GetDouble()
            : (root.TryGetProperty("playerBehaviorAssessment", out var pba) &&
               pba.ValueKind == JsonValueKind.Object &&
               pba.TryGetProperty("historyManipulationCoefficient", out var nestedHm) &&
               nestedHm.ValueKind == JsonValueKind.Number
                ? nestedHm.GetDouble()
                : 0.0);
        return coeff > 0.0;
    }

    private async Task ShowNpcMetaInfo()
    {
        if (!await IsHistoryManipulationEnabled()) return;

        var metaText = new List<string>();
        metaText.Add("[dim italic]🔮 Режим манипулирования историей активен[/]");

        // NPC personality
        var persDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_personality.json");
        if (persDoc != null)
        {
            EnumerateJsonItems(persDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var traits = GetStr(item, "traits", GetStr(item, "description", ""));
                if (!string.IsNullOrEmpty(traits))
                    metaText.Add($"  🧠 [magenta]{Markup.Escape(name)}[/]: {Markup.Escape(traits)}");
            });
        }

        // NPC journals (thought diaries)
        var jourDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_journals.json");
        if (jourDoc != null)
        {
            EnumerateJsonItems(jourDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var entry = GetStr(item, "lastJournalNote", "");
                if (!string.IsNullOrEmpty(entry))
                    metaText.Add($"  📓 [dim]{Markup.Escape(name)}: «{Markup.Escape(entry)}»[/]");
            });
        }

        // NPC masks
        var maskDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_masks.json");
        if (maskDoc != null)
        {
            EnumerateJsonItems(maskDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var mask = GetStr(item, "activeMask", GetStr(item, "maskName", ""));
                if (!string.IsNullOrEmpty(mask))
                    metaText.Add($"  🎭 [red]{Markup.Escape(name)}[/]: маска «{Markup.Escape(mask)}»");
            });
        }

        // NPC memory
        var memDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_memory.json");
        if (memDoc != null)
        {
            EnumerateJsonItems(memDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var mem = GetStr(item, "content", "");
                if (!string.IsNullOrEmpty(mem))
                    metaText.Add($"  💭 [dim]{Markup.Escape(name)}: {Markup.Escape(mem)}[/]");
            });
        }

        // Item journals (sentient items)
        var itemJDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/item_journals.json");
        if (itemJDoc != null)
        {
            EnumerateJsonItems(itemJDoc.RootElement, item =>
            {
                var name = GetStr(item, "itemName", GetStr(item, "name", "?"));
                var entry = GetStr(item, "entry", GetStr(item, "journal", ""));
                if (!string.IsNullOrEmpty(entry))
                    metaText.Add($"  📖 [cyan]{Markup.Escape(name)}[/]: «{Markup.Escape(entry)}»");
                else if (item.TryGetProperty("journalEntries", out var journalEntries) &&
                         journalEntries.ValueKind == JsonValueKind.Array &&
                         journalEntries.GetArrayLength() > 0)
                {
                    var latest = journalEntries.EnumerateArray().Last();
                    var latestText = latest.ValueKind == JsonValueKind.String
                        ? latest.GetString() ?? ""
                        : GetStr(latest, "description", GetStr(latest, "text", GetStr(latest, "spiritVoice", "")));
                    if (!string.IsNullOrWhiteSpace(latestText))
                        metaText.Add($"  📖 [cyan]{Markup.Escape(name)}[/]: «{Markup.Escape(latestText)}»");
                }
            });
        }

        if (metaText.Count > 1)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", metaText)))
            {
                Header = new PanelHeader(" 🔮 Мета-информация (манипулирование историей) ", Justify.Center),
                Border = BoxBorder.Heavy,
                BorderStyle = new Style(Color.Magenta1),
                Padding = new Padding(2, 1)
            });
        }
    }

	    private async Task ShowItemTexts()
	    {
	        var doc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_text_updates.json");
	        var itemsDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/items.json");
            var itemJournalsDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/item_journals.json");

	        var text = new List<string>();
	        var renderedBlocks = new HashSet<string>(StringComparer.Ordinal);

	        // From item_text_updates
	        if (doc != null)
	        {
	            EnumerateJsonItems(doc.RootElement, item =>
	            {
	                var name = GetStr(item, "itemName", GetStr(item, "name", "?"));
	                if (item.TryGetProperty("textContent", out var textContent) &&
	                    textContent.ValueKind == JsonValueKind.Array &&
	                    textContent.GetArrayLength() > 0)
	                {
	                    var entryLines = new List<string>();
	                    foreach (var entry in textContent.EnumerateArray())
	                    {
	                        var content = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : entry.ToString();
	                        if (!string.IsNullOrWhiteSpace(content))
	                            entryLines.Add($"  {Markup.Escape(content)}");
	                    }

	                    if (entryLines.Count > 0)
	                    {
	                        var signature = $"{name}|{string.Join("\n", entryLines)}";
	                        if (renderedBlocks.Add(signature))
	                        {
	                            text.Add($"[bold yellow]📜 {Markup.Escape(name)}[/]");
	                            text.AddRange(entryLines);
	                            text.Add("");
	                        }
	                    }
	                    return;
	                }

	                var appendText = GetStr(item, "textToAppend", GetStr(item, "content", ""));
	                if (string.IsNullOrWhiteSpace(appendText))
	                    return;

	                var appendSignature = $"{name}|{appendText}";
	                if (!renderedBlocks.Add(appendSignature))
	                    return;

	                text.Add($"[bold yellow]📜 {Markup.Escape(name)}[/]");
	                text.Add($"  {Markup.Escape(appendText)}");
	                text.Add("");
	            });
	        }

        // From items with textContent
        var inventoryItems = itemsDoc != null ? GetPlayerInventoryItemsElement(itemsDoc.RootElement) : null;
	        if (inventoryItems.HasValue)
	        {
            foreach (var item in inventoryItems.Value.EnumerateArray())
            {
                if (item.TryGetProperty("textContent", out var tc) && tc.ValueKind == JsonValueKind.Array && tc.GetArrayLength() > 0)
	                {
	                    var name = GetStr(item, "name", "?");
	                    var entryLines = new List<string>();
	                    foreach (var entry in tc.EnumerateArray())
	                    {
	                        var entryStr = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : entry.ToString();
	                        if (!string.IsNullOrWhiteSpace(entryStr))
	                            entryLines.Add($"  {Markup.Escape(entryStr)}");
	                    }

	                    if (entryLines.Count == 0)
	                        continue;

	                    var signature = $"{name}|{string.Join("\n", entryLines)}";
	                    if (!renderedBlocks.Add(signature))
	                        continue;

	                    text.Add($"[bold yellow]📜 {Markup.Escape(name)}[/]");
	                    text.AddRange(entryLines);
	                    text.Add("");
	                }
	            }
	        }

            if (itemJournalsDoc != null)
            {
                EnumerateJsonItems(itemJournalsDoc.RootElement, item =>
                {
                    var name = GetStr(item, "itemName", GetStr(item, "name", "?"));
                    if (!item.TryGetProperty("journalEntries", out var journalEntries) ||
                        journalEntries.ValueKind != JsonValueKind.Array ||
                        journalEntries.GetArrayLength() == 0)
                    {
                        return;
                    }

                    var entryLines = new List<string>();
                    foreach (var entry in journalEntries.EnumerateArray())
                    {
                        if (entry.ValueKind == JsonValueKind.String)
                        {
                            var textEntry = entry.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(textEntry))
                                entryLines.Add($"  {Markup.Escape(textEntry)}");
                            continue;
                        }

                        if (entry.ValueKind != JsonValueKind.Object)
                            continue;

                        var parts = new List<string>();
                        var timestamp = GetStr(entry, "timestamp", "");
                        var eventName = GetStr(entry, "event", "");
                        var description = GetStr(entry, "description", GetStr(entry, "text", GetStr(entry, "spiritVoice", "")));
                        if (!string.IsNullOrWhiteSpace(timestamp))
                            parts.Add($"[dim]{Markup.Escape(timestamp)}[/]");
                        if (!string.IsNullOrWhiteSpace(eventName))
                            parts.Add($"[cyan]{Markup.Escape(eventName)}[/]");
                        if (!string.IsNullOrWhiteSpace(description))
                            parts.Add(Markup.Escape(description));
                        if (parts.Count > 0)
                            entryLines.Add($"  {string.Join(" — ", parts)}");
                    }

                    if (entryLines.Count == 0)
                        return;

                    var signature = $"{name}|journal|{string.Join("\n", entryLines)}";
                    if (!renderedBlocks.Add(signature))
                        return;

                    text.Add($"[bold yellow]📜 {Markup.Escape(name)}[/] [dim](журнал предмета)[/]");
                    text.AddRange(entryLines);
                    text.Add("");
                });
            }

        if (text.Count == 0)
            text.Add("[dim]Нет читаемых предметов (книг, писем, свитков и т.д.)[/]");

        var panel = new Panel(new Markup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 📜 Книги и записи ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }

    // ═══ Helper methods ═══

    private async Task SafeExecute(Func<Task> handler, string commandName)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка при выполнении команды {Markup.Escape(commandName)}:[/]");
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.GetType().Name)}[/]");
            WaitForKey();
        }
    }

    private static int GetInt(JsonElement el, string prop, int def)
    {
        if (!el.TryGetProperty(prop, out var val)) return def;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
        if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var parsed)) return parsed;
        return def;
    }

    private static void ShowEmptyPanel(string title, string message)
    {
        var panel = new Panel(new Markup($"[dim]{message}[/]"))
        {
            Header = new PanelHeader($" {title} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
    }

    private static void WrapInPanel(Table table, string title, Color color)
    {
        WrapInPanel((IRenderable)table, title, color);
    }

    private Task ShowGallery()
    {
        if (_imageService == null)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("image_service_unavailable"))}[/]");
            WaitForKey();
            return Task.CompletedTask;
        }

        var choices = new List<string>
        {
            "🎬 Сцены (ежеходные)",
            "👤 Персонажи (NPC)",
            "📦 Предметы",
            "📍 Локации",
            "🏛️ Фракции",
            "🛡️ Хранители",
            "🏛 Обители",
            "🎭 Игрок",
            "📜 Квесты",
            "🚗 Транспорт",
            "📂 Открыть всю папку изображений",
            "← Назад"
        };

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold purple]🖼 Галерея изображений[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices));

        if (choice.Contains("Назад")) return Task.CompletedTask;
        if (choice.Contains("всю папку")) { _imageService.OpenImagesFolder(); return Task.CompletedTask; }

        var entityType = choice switch
        {
            _ when choice.Contains("Сцены") => "scene",
            _ when choice.Contains("Персонажи") => "npc",
            _ when choice.Contains("Предметы") => "item",
            _ when choice.Contains("Локации") => "location",
            _ when choice.Contains("Фракции") => "faction",
            _ when choice.Contains("Хранители") => "guardian",
            _ when choice.Contains("Обители") => "abode",
            _ when choice.Contains("Игрок") => "player",
            _ when choice.Contains("Квесты") => "quest",
            _ when choice.Contains("Транспорт") => "vehicle",
            _ => "scene"
        };

        _imageService.OpenImagesFolder(entityType);
        return Task.CompletedTask;
    }

    private static void WrapInPanel(IRenderable content, string title, Color color)
    {
        var panel = new Panel(content)
        {
            Header = new PanelHeader($" {title} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(color),
            Expand = true
        };
        AnsiConsole.Write(panel);
    }

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Нажмите любую клавишу...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// After showing entity details, offer image actions if image_prompt exists.
    /// </summary>
    private async Task RegenerateEntityImageAsync(string imagePrompt, string entityType, string entityKey)
    {
        if (_imageService == null)
            return;

        var autoShowAfterGenerate = !_imageService.GenerateWithoutDisplay;
        var generated = await _imageService.GenerateEntityImageAsync(imagePrompt, entityType, entityKey,
            displayAfterGenerate: autoShowAfterGenerate);
        if (!generated || !_imageService.GenerateWithoutDisplay)
            return;

        var showNow = AnsiConsole.Prompt(new ConfirmationPrompt(
            $"[bold]{Markup.Escape(_loc.T("image_regenerated_show_now"))}[/]")
        { DefaultValue = false });
        if (showNow)
            _imageService.ShowEntityImage(entityType, entityKey, forceDisplay: true);
    }

    private async Task WaitForKeyWithImage(string entityType, string entityName, string imagePrompt, string? entityKey = null)
    {
        if (_imageService == null || string.IsNullOrWhiteSpace(imagePrompt))
        {
            WaitForKey();
            return;
        }

        var effectiveKey = string.IsNullOrWhiteSpace(entityKey) ? entityName : entityKey;

        while (true)
        {
            var hasImage = _imageService.EntityImageExists(entityType, effectiveKey);
            var choices = new List<string> { "🖼 Показать изображение" };
            if (hasImage)
                choices.Add("♻ Пересоздать изображение");
            choices.Add("← Назад");

            AnsiConsole.WriteLine();
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Действие:[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .AddChoices(choices));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Пересоздать"))
            {
                await RegenerateEntityImageAsync(imagePrompt, entityType, effectiveKey);
                WaitForKey();
                continue;
            }

            await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, entityType, effectiveKey, forceDisplay: true);
            WaitForKey();
        }
    }

    private static string GetStr(JsonElement el, string prop, string def)
    {
        if (el.TryGetProperty(prop, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.String => val.GetString() ?? def,
                JsonValueKind.Number => val.ToString(),
                _ => val.GetRawText()
            };
        }
        return def;
    }

    private static string GetRarityColor(string rarity) => rarity.ToLower() switch
    {
        "common" or "обычный" => "white",
        "good" or "хороший" => "cyan",
        "uncommon" or "необычный" => "green",
        "rare" or "редкий" => "blue",
        "epic" or "эпический" => "purple",
        "legendary" or "легендарный" => "yellow",
        "unique" or "уникальный" => "orange1",
        _ => "grey"
    };

    private static int GetRarityRank(string rarity) => rarity.ToLowerInvariant() switch
    {
        "common" or "обычный" => 1,
        "good" or "хороший" => 2,
        "uncommon" or "необычный" => 3,
        "rare" or "редкий" => 4,
        "epic" or "эпический" => 5,
        "legendary" or "легендарный" => 6,
        "unique" or "уникальный" => 7,
        _ => 1
    };

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";

    private static string FormatCharacteristicArray(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "";

        var values = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var key = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(key)) continue;
            values.Add(Characteristics.RussianNames.GetValueOrDefault(key, key));
        }

        return values.Count == 0
            ? ""
            : Markup.Escape(string.Join(", ", values));
    }

    private static void EnumerateFactionCoreEntries(JsonElement root, Action<JsonElement> action)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                action(item);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return;

        if (root.TryGetProperty("factionDataChanges", out var factionChanges) && factionChanges.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factionChanges.EnumerateArray())
                action(item);
            return;
        }

        if (root.TryGetProperty("factions", out var factions) && factions.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factions.EnumerateArray())
                action(item);
            return;
        }

        if (root.TryGetProperty("factionId", out _) || root.TryGetProperty("name", out _))
            action(root);
    }

    private static JsonElement GetCurrentLocationRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("currentLocationData", out var locationData) &&
            locationData.ValueKind == JsonValueKind.Object)
            return locationData;

        return root;
    }

    private static JsonElement GetWeatherRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("weatherChange", out var weatherChange) &&
            weatherChange.ValueKind == JsonValueKind.Object)
            return weatherChange;

        return root;
    }

    private static void AppendWorldTimeLines(List<string> lines, JsonElement root, string indent)
    {
        if (TryFormatAbsoluteWorldTime(root, out var absolute))
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  {Markup.Escape(absolute)}");
            return;
        }

        if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
            TryFormatAbsoluteWorldTime(setWorldTime, out absolute))
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  {Markup.Escape(absolute)}");
            return;
        }

        if (TryGetIntLike(root, "timeChange", out var deltaMinutes) && deltaMinutes != 0)
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  Прошло [white]{deltaMinutes}[/] мин. за ход");
        }
    }

    private static bool TryFormatAbsoluteWorldTime(JsonElement source, out string formatted)
    {
        formatted = "";
        if (source.ValueKind != JsonValueKind.Object)
            return false;

        var year = GetStr(source, "year", "");
        var month = GetStr(source, "monthName", "");
        var day = GetStr(source, "dayOfMonth", "");
        var tod = GetStr(source, "timeOfDay", "");

        if (string.IsNullOrWhiteSpace(year) &&
            string.IsNullOrWhiteSpace(month) &&
            string.IsNullOrWhiteSpace(day) &&
            string.IsNullOrWhiteSpace(tod))
            return false;

        var datePart = string.Join(" ", new[] { day, month, year }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        formatted = !string.IsNullOrWhiteSpace(datePart) && !string.IsNullOrWhiteSpace(tod)
            ? $"{datePart}, {tod}"
            : (!string.IsNullOrWhiteSpace(datePart) ? datePart : tod);
        return !string.IsNullOrWhiteSpace(formatted);
    }

    private static bool TryGetIntLike(JsonElement root, string propName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propName, out var field))
            return false;

        if (field.ValueKind == JsonValueKind.Number)
            return field.TryGetInt32(out value);

        return field.ValueKind == JsonValueKind.String &&
               int.TryParse(field.GetString(), out value);
    }

    private static void AppendStatusEffectPreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold yellow]⚡ Активные эффекты:[/]");
        var hasEffects = false;
        EnumerateJsonItems(root, item =>
        {
            if (item.ValueKind != JsonValueKind.Object) return;
            hasEffects = true;
            var effectType = GetStr(item, "effectType", "?");
            var value = GetStr(item, "value", "");
            var duration = GetStr(item, "duration", "");
            var source = GetStr(item, "sourceSkill", GetStr(item, "source", ""));
            var target = GetStr(item, "targetTypeDisplayName", GetStr(item, "targetType", ""));
            var description = GetStr(item, "effectDescription", GetStr(item, "description", ""));
            var color = effectType.ToLowerInvariant() switch
            {
                "buff" or "heal" or "healovertime" => "green",
                "debuff" or "damage" or "damageovertime" or "control" => "red",
                "damagereduction" => "cyan",
                _ => "yellow"
            };

            var line = $"  [{color}]• {Markup.Escape(effectType)}[/]";
            if (!string.IsNullOrEmpty(value))
                line += $" [white]{Markup.Escape(value)}[/]";
            if (!string.IsNullOrEmpty(target))
                line += $" → {Markup.Escape(target)}";
            if (!string.IsNullOrEmpty(duration) && duration != "0")
                line += $" [dim]({Markup.Escape(duration)} ход.)[/]";
            lines.Add(line);

            if (!string.IsNullOrEmpty(source))
                lines.Add($"    [dim]Источник: {Markup.Escape(source)}[/]");
            if (!string.IsNullOrEmpty(description))
                lines.Add($"    [dim]{Markup.Escape(description)}[/]");
        });

        if (!hasEffects)
            lines.Add("  [dim]Нет активных эффектов[/]");
    }

    private static void AppendStatusWoundPreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold red]🩸 Раны:[/]");
        var hasWounds = false;
        EnumerateJsonItems(root, item =>
        {
            if (item.ValueKind != JsonValueKind.Object) return;
            hasWounds = true;
            var woundName = GetStr(item, "woundName", "Рана");
            var severity = GetStr(item, "severity", "?");
            var description = GetStr(item, "descriptionOfEffects", GetStr(item, "description", ""));
            var severityColor = severity.ToLowerInvariant() switch
            {
                "light" => "yellow",
                "moderate" => "orange1",
                "serious" => "red",
                "critical" => "red bold",
                _ => "white"
            };

            lines.Add($"  [{severityColor}]• {Markup.Escape(woundName)} ({Markup.Escape(severity)})[/]");
            if (!string.IsNullOrEmpty(description))
                lines.Add($"    [dim]{Markup.Escape(description)}[/]");

            if (item.TryGetProperty("healingState", out var healingState) && healingState.ValueKind == JsonValueKind.Object)
            {
                var state = GetStr(healingState, "currentState", "");
                var progress = GetStr(healingState, "treatmentProgress", "0");
                var needed = GetStr(healingState, "progressNeeded", "?");
                if (!string.IsNullOrEmpty(state))
                    lines.Add($"    [cyan]Лечение:[/] {Markup.Escape(state)} ({Markup.Escape(progress)}/{Markup.Escape(needed)})");
            }
        });

        if (!hasWounds)
            lines.Add("  [dim green]Ран нет[/]");
    }

    private static void AppendStatusCustomStatePreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold magenta]📊 Особые состояния:[/]");
        var beforeCount = lines.Count;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                RenderCustomStateItem(lines, item, "  ");
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            var renderedFromArray = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                renderedFromArray = true;
                foreach (var item in prop.Value.EnumerateArray())
                    RenderCustomStateItem(lines, item, "  ");
            }

            if (!renderedFromArray)
                RenderCustomStateItem(lines, root, "  ");
        }

        if (lines.Count == beforeCount)
            lines.Add("  [dim]Нет особых состояний[/]");
    }

    private static void EnumerateArray(JsonElement root, string propName, Action<JsonElement> action)
    {
        if (root.TryGetProperty(propName, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var item in arr.EnumerateArray())
                action(item);
    }

    private static void EnumerateJsonItems(JsonElement root, Action<JsonElement> action)
    {
        if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray())
                action(item);
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                        action(item);
                }
        }
    }

    private async Task ShowValidation()
    {
        if (_validator == null)
        {
            AnsiConsole.MarkupLine("[yellow]Сервис валидации недоступен[/]");
            WaitForKey();
            return;
        }

        AnsiConsole.MarkupLine("[dim]Проверка целостности игровых файлов...[/]");
        var issues = await _validator.ValidateGameStateAsync();

        if (issues.Count == 0)
        {
            var okPanel = new Panel(new Markup("[green bold]✅ Все проверки пройдены! Файлы в порядке.[/]"))
            {
                Header = new PanelHeader(" 🔍 Валидация ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(okPanel);
        }
        else
        {
            var summary = issues
                .GroupBy(issue => new
                {
                    issue.Category,
                    Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
                })
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.Category.ToString(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key.Section, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .Select(group => $"{FormatValidationCategory(group.Key.Category)} / {group.Key.Section}: {group.Count()}")
                .ToList();

            if (summary.Count > 0)
            {
                var summaryPanel = new Panel(new Markup(string.Join("\n", summary.Select(item => $"[yellow]• {Markup.Escape(item)}[/]"))))
                {
                    Header = new PanelHeader(" 🧭 Сводка ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(1, 0),
                    Expand = true
                };
                AnsiConsole.Write(summaryPanel);
                AnsiConsole.WriteLine();
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .AddColumn(new TableColumn("[bold]Уровень[/]").Centered())
                .AddColumn(new TableColumn("[bold]Категория[/]"))
                .AddColumn(new TableColumn("[bold]Проблема[/]"))
                .AddColumn(new TableColumn("[bold]Подсказка[/]"));

            foreach (var issue in issues)
            {
                var severityColor = issue.Severity switch
                {
                    Services.IssueSeverity.Error => "red",
                    Services.IssueSeverity.Warning => "yellow",
                    _ => "dim"
                };
                var icon = issue.Severity switch
                {
                    Services.IssueSeverity.Error => "❌",
                    Services.IssueSeverity.Warning => "⚠️",
                    _ => "ℹ️"
                };
                table.AddRow(
                    $"[{severityColor}]{icon} {issue.Severity}[/]",
                    $"[bold]{Markup.Escape(FormatValidationCategory(issue.Category))}[/]\n[dim]{Markup.Escape(issue.Section ?? "General")}[/]",
                    $"[white]{Markup.Escape(issue.Message)}[/]\n[dim]{Markup.Escape(issue.FilePath)}[/]",
                    string.IsNullOrWhiteSpace(issue.RepairHint)
                        ? "[dim]—[/]"
                        : $"[grey]{Markup.Escape(issue.RepairHint)}[/]");
            }

            var panel = new Panel(table)
            {
                Header = new PanelHeader($" 🔍 Валидация ({issues.Count} проблем) ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(issues.Any(i => i.Severity == Services.IssueSeverity.Error) ? Color.Red : Color.Yellow),
                Padding = new Padding(1, 0)
            };
            AnsiConsole.Write(panel);
        }

        WaitForKey();
    }

    private static string FormatValidationCategory(Services.IssueCategory category) => category switch
    {
        Services.IssueCategory.ProtocolViolation => "Протокол",
        Services.IssueCategory.ClientOwnedSurface => "Системный файл клиента",
        _ => "Согласованность состояния"
    };

    private async Task ShowLivesHistory()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null)
        {
            ShowEmptyPanel("История жизней", "Нет данных о прошлых жизнях");
            return;
        }

        var root = doc.RootElement;
        if (!root.TryGetProperty("livesHistory", out var lives) ||
            lives.ValueKind != JsonValueKind.Array || lives.GetArrayLength() == 0)
        {
            var emptyPanel = new Panel(new Markup("[dim italic]Эта душа ещё не прожила ни одной смертной жизни.\n" +
                "Воплотитесь через Врата Души, чтобы начать первую жизнь.[/]"))
            {
                Header = new PanelHeader(" 📜 История жизней ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(emptyPanel);
            WaitForKey();
            return;
        }

        var tree = new Tree("[bold blue]📜 История прожитых жизней[/]");

        var lifeIndex = 0;
        foreach (var life in lives.EnumerateArray())
        {
            static string GetLifeScalar(JsonElement lifeEntry, params string[] propertyNames)
            {
                foreach (var propertyName in propertyNames)
                {
                    if (!lifeEntry.TryGetProperty(propertyName, out var value))
                        continue;

                    return value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? "",
                        JsonValueKind.Number => value.ToString(),
                        JsonValueKind.True => "да",
                        JsonValueKind.False => "нет",
                        _ => ""
                    };
                }

                return "";
            }

            static List<string> ReadLifeStringArray(JsonElement lifeEntry, params string[] propertyNames)
            {
                foreach (var propertyName in propertyNames)
                {
                    if (!lifeEntry.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
                        continue;

                    return value.EnumerateArray()
                        .Select(item => item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString() ?? "",
                            JsonValueKind.Number => item.ToString(),
                            _ => ""
                        })
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();
                }

                return new List<string>();
            }

            lifeIndex++;
            var incarnation = life.TryGetProperty("incarnation", out var inc) ? inc.ToString() : lifeIndex.ToString();
            var summary = GetStr(life, "summary", "Нет описания");
            var endedAt = GetStr(life, "endedAt", GetStr(life, "completionDate", ""));
            var turnsLived = GetStr(life, "turnsLived", "?");

            var charName = GetStr(life, "characterName", "");
            var worldName = GetStr(life, "world", GetStr(life, "worldName", ""));
            var finalLevel = GetStr(life, "finalLevel", "");
            var questsCompleted = GetStr(life, "questsCompleted", "");
            var deathReason = GetStr(life, "deathReason", "");
            var worldGenre = GetLifeScalar(life, "worldGenre");
            var totalSoulQuests = GetLifeScalar(life, "totalSoulQuests", "soulQuestsCompleted");
            var feathersEarned = GetLifeScalar(life, "feathersEarned");
            var gmCoefficient = GetLifeScalar(life, "gmCoefficient");
            var enlightenmentTierReached = GetLifeScalar(life, "enlightenmentTierReached");
            var alignmentAtDeath = GetLifeScalar(life, "alignmentAtDeath", "finalAlignment");
            var worldImpactLevel = GetLifeScalar(life, "worldImpactLevel");
            var moralChoicesRecord = GetLifeScalar(life, "moralChoicesRecord");
            var incarnationStartDate = GetLifeScalar(life, "incarnationStartDate", "startedAt");
            var incarnationDuration = GetLifeScalar(life, "incarnationDuration", "duration");
            var notableAchievements = ReadLifeStringArray(life, "notableAchievements");
            var npcSoulImprints = ReadLifeStringArray(life, "npcSoulImprints");

            var titleParts = new List<string> { $"[bold cyan]Жизнь #{Markup.Escape(incarnation)}[/]" };
            if (!string.IsNullOrEmpty(charName)) titleParts.Add($"[white]{Markup.Escape(charName)}[/]");
            if (!string.IsNullOrEmpty(worldName)) titleParts.Add($"[dim]🌍 {Markup.Escape(worldName)}[/]");
            titleParts.Add($"[dim]({Markup.Escape(turnsLived)} ходов)[/]");

            var lifeNode = tree.AddNode(string.Join("  ", titleParts));
            lifeNode.AddNode($"[white]{Markup.Escape(summary)}[/]");

            var detailParts = new List<string>();
            if (!string.IsNullOrEmpty(finalLevel)) detailParts.Add($"Ур. {Markup.Escape(finalLevel)}");
            if (!string.IsNullOrEmpty(questsCompleted) && questsCompleted != "0") detailParts.Add($"Квестов: {Markup.Escape(questsCompleted)}");
            if (!string.IsNullOrEmpty(deathReason)) detailParts.Add($"Причина: {Markup.Escape(deathReason)}");
            if (detailParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", detailParts)}[/]");

            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(worldGenre)) metaParts.Add($"Жанр: {Markup.Escape(worldGenre)}");
            if (!string.IsNullOrEmpty(totalSoulQuests) && totalSoulQuests != "0") metaParts.Add($"Квестов души: {Markup.Escape(totalSoulQuests)}");
            if (!string.IsNullOrEmpty(feathersEarned)) metaParts.Add($"Перьев: {Markup.Escape(feathersEarned)}");
            if (!string.IsNullOrEmpty(gmCoefficient)) metaParts.Add($"GM-коэфф.: {Markup.Escape(gmCoefficient)}");
            if (!string.IsNullOrEmpty(enlightenmentTierReached)) metaParts.Add($"Тир просветления: {Markup.Escape(enlightenmentTierReached)}");
            if (!string.IsNullOrEmpty(alignmentAtDeath)) metaParts.Add($"Мировоззрение: {Markup.Escape(alignmentAtDeath)}");
            if (!string.IsNullOrEmpty(worldImpactLevel)) metaParts.Add($"Влияние на мир: {Markup.Escape(worldImpactLevel)}");
            if (metaParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", metaParts)}[/]");

            if (life.TryGetProperty("achievements", out var achArr) && achArr.ValueKind == JsonValueKind.Array && achArr.GetArrayLength() > 0)
            {
                var achNames = new List<string>();
                foreach (var ach in achArr.EnumerateArray())
                    achNames.Add(ach.ValueKind == JsonValueKind.String ? (ach.GetString() ?? "") : GetStr(ach, "name", ach.GetRawText()));
                lifeNode.AddNode($"[yellow]🏆 {Markup.Escape(string.Join(", ", achNames))}[/]");
            }

            if (notableAchievements.Count > 0)
                lifeNode.AddNode($"[green]⭐ Значимые достижения: {Markup.Escape(string.Join(", ", notableAchievements))}[/]");

            if (!string.IsNullOrWhiteSpace(moralChoicesRecord))
                lifeNode.AddNode($"[italic]⚖ {Markup.Escape(moralChoicesRecord)}[/]");

            if (npcSoulImprints.Count > 0)
                lifeNode.AddNode($"[mediumpurple2]👤 Слепки души: {Markup.Escape(string.Join(", ", npcSoulImprints))}[/]");

            var timelineParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(incarnationStartDate))
                timelineParts.Add($"Начало: {Markup.Escape(incarnationStartDate)}");
            if (!string.IsNullOrWhiteSpace(incarnationDuration))
                timelineParts.Add($"Длительность: {Markup.Escape(incarnationDuration)}");
            if (!string.IsNullOrEmpty(endedAt))
            {
                if (DateTime.TryParse(endedAt, out var dt))
                    timelineParts.Add($"Завершена: {dt:dd.MM.yyyy HH:mm}");
                else
                    timelineParts.Add($"Завершена: {Markup.Escape(endedAt)}");
            }

            if (timelineParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", timelineParts)}[/]");
        }

        var panel = new Panel(tree)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
        WaitForKey();
    }
}
