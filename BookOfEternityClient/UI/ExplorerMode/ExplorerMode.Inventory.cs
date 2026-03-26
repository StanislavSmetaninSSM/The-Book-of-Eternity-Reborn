using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{    private static readonly Dictionary<string, string> SlotLabels = new()
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
                    MarkupLine($"[dim]Авто-выброс: {brokenItems.Count} сломанных предметов удалено[/]");
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

            var selected = Prompt(new SelectionPrompt<string>()
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

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
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

        var action = Prompt(new SelectionPrompt<string>()
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
                var pick = Prompt(new SelectionPrompt<string>()
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
            var splitAmount = Prompt(
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
            var confirm = Prompt(new ConfirmationPrompt(
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
            MarkupLine($"[green]✅ «{Markup.Escape(itemName)}» экипировано в {slotLabel}![/]");
            MarkupLine("[dim]Нажмите любую клавишу...[/]");
            ReadKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
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
            MarkupLine($"[green]✅ Предмет снят с {slotLabel} и убран в рюкзак.[/]");
            MarkupLine("[dim]Нажмите любую клавишу...[/]");
            ReadKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
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
                MarkupLine($"[green]✅ «{Markup.Escape(itemName)}» выброшен.[/]");
                MarkupLine("[dim]Нажмите любую клавишу...[/]");
                ReadKey();
                return;
            }

            MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
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
                if (splitAmount >= currentCount) { MarkupLine("[yellow]Нельзя отделить всё количество.[/]"); WaitForKey(); return; }

                // Reduce original
                original[countKey] = currentCount - splitAmount;

                // Create copy with split amount
                var copy = JsonNode.Parse(original.ToJsonString())!.AsObject();
                copy[countKey] = splitAmount;
                AssignNewInventoryIdentity(copy);

                itemsArr.Add(copy);

                var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));
                MarkupLine($"[green]✅ Стопка разделена: {currentCount - splitAmount} + {splitAmount}[/]");
                MarkupLine("[dim]Нажмите любую клавишу...[/]");
                ReadKey();
                return;
            }

            MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
            WaitForKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
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
                MarkupLine("[yellow]Предмет не найден в инвентаре.[/]");
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
                MarkupLine("[yellow]Нет другой стопки с таким же именем для объединения.[/]");
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
            MarkupLine($"[green]✅ Стопки объединены: {totalCount} шт.[/]");
            MarkupLine("[dim]Нажмите любую клавишу...[/]");
            ReadKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }
}
