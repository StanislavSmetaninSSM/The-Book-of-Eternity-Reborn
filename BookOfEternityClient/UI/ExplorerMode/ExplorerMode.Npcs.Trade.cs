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
{
    private async Task ShowNpcTradeCommand()
    {
        var npcId = _currentCommandRemainder.Trim();
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            await ShowNpcTradePanel(await ResolveNpcTradeCommandTargetIdAsync(npcId) ?? npcId);
            return;
        }

        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null)
        {
            ShowEmptyPanel("Торговля с НПС", "Торговцы не обнаружены");
            return;
        }

        var npcs = CollectNpcListEntries(doc);
        if (npcs.Count == 0)
        {
            ShowEmptyPanel("Торговля с НПС", "Торговцы не обнаружены");
            return;
        }

        var renameMap = BuildNpcRenameMap(doc);
        var (currentLocationId, currentLocationName) = await ReadCurrentLocationIdentityAsync();
        var merchantChoices = new List<(string Label, string NpcId)>();

        foreach (var npc in npcs)
        {
            var trade = NpcTradeService.EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
            if (!trade.IsMerchant)
                continue;

            var id = GetPrimaryNpcId(npc);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var name = ResolveNpcDisplayName(npc, renameMap);
            var status = trade.TradeAvailable
                ? "доступна"
                : trade.BlockReason ?? "сейчас недоступна";
            merchantChoices.Add((
                ConsoleLayout.PlainChoiceLabel(
                    $"🛒 {name}",
                    trade.MerchantProfileDisplay,
                    status),
                id));
        }

        if (merchantChoices.Count == 0)
        {
            ShowEmptyPanel("Торговля с НПС", "В текущей сцене нет НПС с торговой витриной");
            return;
        }

        while (true)
        {
            var choices = merchantChoices.Select(static item => item.Label).ToList();
            choices.Add("← Назад");
            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold purple]🛒 Торговля с НПС[/]  [dim](выберите торговца)[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= merchantChoices.Count)
                return;

            await ShowNpcTradePanel(merchantChoices[index].NpcId);
            Clear();
        }
    }

    private async Task<string?> ResolveNpcTradeCommandTargetIdAsync(string query)
    {
        var normalizedQuery = NormalizeNpcTradeLookupToken(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return null;

        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null)
            return null;

        var npcs = CollectNpcListEntries(doc);
        if (npcs.Count == 0)
            return null;

        var renameMap = BuildNpcRenameMap(doc);
        var (currentLocationId, currentLocationName) = await ReadCurrentLocationIdentityAsync();
        string? firstNameMatch = null;
        string? firstMerchantNameMatch = null;
        string? firstAvailableMerchantNameMatch = null;

        foreach (var npc in npcs)
        {
            var id = GetPrimaryNpcId(npc);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (NpcTradeLookupEquals(id, normalizedQuery))
                return id;

            if (!NpcTradeNameMatches(npc, renameMap, normalizedQuery))
                continue;

            firstNameMatch ??= id;
            var trade = NpcTradeService.EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
            if (trade.IsMerchant)
            {
                firstMerchantNameMatch ??= id;
                if (trade.TradeAvailable)
                    firstAvailableMerchantNameMatch ??= id;
            }
        }

        return firstAvailableMerchantNameMatch ?? firstMerchantNameMatch ?? firstNameMatch;
    }

    private static bool NpcTradeNameMatches(JsonElement npc, IReadOnlyDictionary<string, string> renameMap, string normalizedQuery)
    {
        foreach (var candidate in EnumerateNpcTradeLookupNames(npc, renameMap))
        {
            if (NpcTradeLookupEquals(candidate, normalizedQuery))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateNpcTradeLookupNames(JsonElement npc, IReadOnlyDictionary<string, string> renameMap)
    {
        yield return ResolveNpcDisplayName(npc, renameMap);
        yield return GetStr(npc, "NPCName", "");
        yield return GetStr(npc, "npcName", "");
        yield return GetStr(npc, "name", "");
    }

    private static bool NpcTradeLookupEquals(string candidate, string normalizedQuery) =>
        string.Equals(NormalizeNpcTradeLookupToken(candidate), normalizedQuery, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeNpcTradeLookupToken(string value) =>
        value.Trim().Trim('"', '\'', '«', '»');

    private async Task ShowNpcTradePanel(string npcId)
    {
        if (_npcTradeService == null)
        {
            MarkupLine("[red]❌ Сервис торговли НПС недоступен.[/]");
            WaitForKey();
            return;
        }

        while (true)
        {
            var view = await _npcTradeService.EnsureTradeInventoryAsync(npcId, await TryReadCurrentTurnNumberAsync());
            if (view == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                _pendingGmAction = view.PendingGmAction;

            if (view.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var totalOffers = view.Offers.Count;
            var availableOffers = view.Offers.Count(offer => !offer.SoldOut);
            var availableBuybackOffers = view.BuybackOffers.Count;
            var headerLines = new List<string>
            {
                $"[bold purple]🛒 Торговля с {Markup.Escape(view.NpcName)}[/]",
                $"[dim]Профиль: {Markup.Escape(view.MerchantProfileDisplay)} • Торговля NPC: {view.NpcTrade} • Торговля игрока: {view.PlayerTrade}[/]",
                $"[dim]Отношение: {view.RelationshipLevel} • Деньги: {view.CurrentMoney}[/]",
                $"[dim]Товаров в витрине: {availableOffers}/{totalOffers} доступно • Выкуп обратно: {availableBuybackOffers} • {Markup.Escape(DescribeNpcTradeRefresh(view))}[/]"
            };
            if (!view.InventoryReady && !string.IsNullOrWhiteSpace(view.InventoryStatusMessage))
                headerLines.Add($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage)}[/]");
            if (!view.InventoryReady && view.InventoryRequestPending)
                headerLines.Add("[dim]Покупка товаров откроется после ответа GM и подтверждения витрины.[/]");

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", headerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Purple),
                Padding = new Padding(1, 1),
                Expand = true
            });

            if (!view.InventoryReady && !string.IsNullOrWhiteSpace(view.PendingGmAction))
            {
                MarkupLine("[yellow]⏳ Запрос на торговую витрину отправлен ГМ сейчас; дождитесь ответа и откройте торговлю снова.[/]");
                return;
            }

            var sectionChoices = new List<string>();
            if (view.InventoryReady)
                sectionChoices.Add("🛍 Купить товары");
            else
                sectionChoices.Add("🔄 Проверить витрину");
            sectionChoices.Add("🔁 Выкупить обратно");
            sectionChoices.Add("💰 Продать товары");
            sectionChoices.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите раздел:[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(sectionChoices));

            if (choice.Contains("Назад"))
                return;

            if (choice.Contains("Проверить"))
            {
                MarkupLine($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage ?? "Витрина торговца ещё подготавливается.")}[/]");
                WaitForKey();
                Clear();
                continue;
            }

            if (choice.Contains("Купить"))
            {
                await ShowNpcBuyMenu(npcId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
                continue;
            }

            if (choice.Contains("Выкупить"))
            {
                await ShowNpcBuybackMenu(npcId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
                continue;
            }

            if (choice.Contains("Продать"))
            {
                await ShowNpcSellMenu(npcId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
            }
        }
    }


    private async Task ShowNpcBuybackMenu(string npcId)
    {
        if (_npcTradeService == null)
            return;

        while (true)
        {
            var tradeView = await _npcTradeService.EnsureTradeInventoryAsync(npcId, await TryReadCurrentTurnNumberAsync());
            if (tradeView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить данные торговца.[/]");
                WaitForKey();
                return;
            }

            if (tradeView.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(tradeView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            if (tradeView.BuybackOffers.Count == 0)
            {
                MarkupLine("[dim]У этого торговца нет доступных товаров для обратного выкупа.[/]");
                WaitForKey();
                return;
            }

            var offerChoices = BuildUniqueChoiceOptions(tradeView.BuybackOffers, offer =>
                ConsoleLayout.PlainChoiceLabel(
                    $"🔁 {offer.Name}",
                    GetNpcTradeChoiceMeta(offer.ItemData),
                    offer.Rarity,
                    $"💰 {offer.Price}"));
            var choices = offerChoices.Select(item => item.Label).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Обратный выкуп[/] [dim](доступно: {tradeView.BuybackOffers.Count} • деньги: {tradeView.CurrentMoney})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(20)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedOffer = offerChoices.FirstOrDefault(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;
            if (selectedOffer == null)
                return;

            var offer = selectedOffer;
            if (!ShowNpcTradeBuybackPreview(offer, tradeView.CurrentMoney, tradeView.CurrentMoney >= offer.Price))
                continue;

            var result = await _npcTradeService.BuyBackAsync(npcId, offer.BuybackEntryId, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }


    private async Task ShowNpcBuyMenu(string npcId)
    {
        if (_npcTradeService == null)
            return;

        while (true)
        {
            var refreshedView = await _npcTradeService.EnsureTradeInventoryAsync(npcId, await TryReadCurrentTurnNumberAsync());
            if (refreshedView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (refreshedView.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(refreshedView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            if (!refreshedView.InventoryReady)
            {
                MarkupLine($"[yellow]⏳ {Markup.Escape(refreshedView.InventoryStatusMessage ?? "Витрина торговца ещё подготавливается.")}[/]");
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

            var selected = Prompt(new SelectionPrompt<string>()
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

            var result = await _npcTradeService.BuyAsync(npcId, offer.SlotId, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
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

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
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

        var action = Prompt(new SelectionPrompt<string>()
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
            var tradeView = await _npcTradeService.EnsureTradeInventoryAsync(npcId, await TryReadCurrentTurnNumberAsync());
            if (tradeView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину торговца.[/]");
                WaitForKey();
                return;
            }

            if (tradeView.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(tradeView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var offers = await _npcTradeService.GetSellableItemsAsync(npcId);
            if (offers.Count == 0)
            {
                MarkupLine("[dim]В инвентаре нет товаров смертной жизни, доступных для продажи.[/]");
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

            var selected = Prompt(new SelectionPrompt<string>()
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

            var result = await _npcTradeService.SellAsync(npcId, offer.ItemId, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
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

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 💰 Продажа товара ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("💰 Продать", "← Назад к списку"));

        return action.Contains("Продать", StringComparison.OrdinalIgnoreCase);
    }


    private bool ShowNpcTradeBuybackPreview(Services.NpcTradeService.NpcBuybackOffer offer, int currentMoney, bool canBuy)
    {
        using var itemDoc = JsonDocument.Parse(offer.ItemData.ToJsonString());
        var lines = BuildInventoryItemDetailLines(offer.Name, itemDoc.RootElement);
        lines.Insert(1, $"  💰 Цена обратного выкупа: [yellow]{offer.Price}[/]");
        lines.Insert(2, $"  [dim]Ранее продано этому торговцу за {offer.SoldForPrice}. Ход продажи: {offer.SoldAtTurn}.[/]");
        lines.Insert(3, $"  💰 У вас сейчас: [gold1]{currentMoney}[/]");

        if (!canBuy)
            lines.Insert(4, "  [yellow]Статус покупки: пока не хватает денег для обратного выкупа.[/]");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🔁 Обратный выкуп ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var actions = new List<string>();
        if (canBuy)
            actions.Add("🔁 Выкупить");
        actions.Add("← Назад к списку");

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(actions));

        return action.Contains("Выкупить", StringComparison.OrdinalIgnoreCase);
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


    private static string GetNpcMerchantProfileDisplay(string? merchantProfile) =>
        Services.NpcTradeService.GetMerchantProfileDisplayName(merchantProfile);


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

    /// <summary>Hard caps where Breakthrough Quests are required (Rule 19.G).</summary>

}
