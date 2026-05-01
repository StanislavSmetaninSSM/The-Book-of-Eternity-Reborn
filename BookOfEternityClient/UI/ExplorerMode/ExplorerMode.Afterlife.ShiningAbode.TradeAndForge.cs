using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private enum ShiningTradeBuyDecision
    {
        Back,
        Buy
    }

    private async Task ShowShiningTradeAndForgeAsync()
    {
        if (!EnsureActiveShiningAbodeAvailable("Торговля и кузня Сияющей Обители"))
            return;

        while (true)
        {
            var context = await LoadShiningContextAsync();
            if (context == null)
                return;
            var tradeRequests = await ShiningTradeRequestState.ReadRequestsAsync(_fs);

            Clear();
            Write(BuildShiningOverviewPanel(context.Root, context.ResidentRoot, context.GuardiansRoot));
            Write(BuildShiningTradeAndForgePanel(context.Root, context.SoulRoot, context.ResidentRoot, tradeRequests));

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Торговля и кузня Сияющей Обители[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(
                    "🛒 Торговля фракции",
                    "🧾 Осмотреть торговые циклы",
                    "🎰 Сияющая гача реликвий",
                    "⚒ Создать запрос на перековку",
                    "← Назад"));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Осмотреть торговые циклы", StringComparison.OrdinalIgnoreCase))
            {
                await ShowShiningTradeLifecycleInspectionAsync(context);
                WaitForKey();
            }
            else if (choice.Contains("Торговля", StringComparison.Ordinal))
                await HandleShiningTradeMenuAsync(context);
            else if (choice.Contains("гача", StringComparison.OrdinalIgnoreCase))
                await HandleShiningRelicGachaRequestAsync(context);
            else
                await HandleShiningForgeRequestAsync(context);
        }
    }

    private async Task ShowShiningTradeLifecycleInspectionAsync(ShiningContext context)
    {
        var tradeRequests = await ShiningTradeRequestState.ReadRequestsAsync(_fs);
        var lines = new List<string>
        {
            "[bold yellow]🧾 Полный осмотр торговых циклов[/]"
        };

        if (context.Root["factions"] is not JsonArray factions || factions.Count == 0)
        {
            lines.Add("");
            lines.Add("[dim]Материализованных сияющих фракций пока нет.[/]");
        }
        else
        {
            foreach (var faction in factions.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["factionStrength"])))
            {
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
                var currentContract = BuildShiningTradeContractSnapshot(context.SoulRoot, context.ResidentRoot, faction);
                var matchingRequests = tradeRequests.Where(request =>
                    string.Equals(request.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(request.TradeCycleId, currentContract.TradeCycleId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var pendingRequest = matchingRequests.Count == 1 ? matchingRequests[0] : null;
                var duplicatePendingRequests = matchingRequests.Count > 1;
                var matchingReceipt = pendingRequest != null
                    ? ShiningTradeRequestState.FindMatchingReceipt(faction, pendingRequest)
                    : (!duplicatePendingRequests &&
                       ShiningTradeRequestState.InventoryMatchesRequestContract(faction["tradeInventory"] as JsonObject, currentContract)
                        ? ShiningTradeRequestState.FindLatestAuthoritativeReadyReceiptForCurrentCycle(faction, currentContract.TradeCycleId)
                        : null);
                var sameCycleReceipt = (faction["tradeInventoryReceipts"] as JsonArray)?.OfType<JsonObject>()
                    .Where(receipt => string.Equals(GetNodeString(receipt["tradeCycleId"]), currentContract.TradeCycleId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(receipt => GetNodeInt(receipt["resolvedAtTurn"]))
                    .ThenByDescending(receipt => GetNodeString(receipt["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                var receiptHistory = (faction["tradeInventoryReceipts"] as JsonArray)?.OfType<JsonObject>()
                    .OrderByDescending(receipt => GetNodeInt(receipt["resolvedAtTurn"]))
                    .ThenByDescending(receipt => GetNodeString(receipt["resolvedAtUtc"]), StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<JsonObject>();
                var displayedReceipt = matchingReceipt ?? sameCycleReceipt;
                var tradeInventory = faction["tradeInventory"] as JsonObject;
                var inventoryMatchesCurrentContract = ShiningTradeRequestState.InventoryMatchesRequestContract(tradeInventory, currentContract);
                var soldOutCount = (tradeInventory?["items"] as JsonArray)?.OfType<JsonObject>().Count(item => GetNodeBool(item["soldOut"])) ?? 0;
                var itemCount = (tradeInventory?["items"] as JsonArray)?.Count ?? 0;
                var remainingCount = Math.Max(0, itemCount - soldOutCount);
                var tradeStatus = currentContract.DerivedTradeTier >= 1 ? "активна" : "спит";

                lines.Add("");
                lines.Add($"[bold]{Markup.Escape(factionName)}[/] [dim]({Markup.Escape(factionId)})[/]");
                lines.Add($"  Цикл торговли: [white]{Markup.Escape(currentContract.TradeCycleId)}[/]");
                lines.Add($"  Торговый статус: [white]{Markup.Escape(tradeStatus)}[/]");
                lines.Add($"  Расчётный контракт цикла: [dim]уровень {currentContract.DerivedTradeTier}, слотов {currentContract.DerivedTradeSlotCount}, потолок {Markup.Escape(currentContract.DerivedRarityCeiling)}, услуги x{currentContract.DerivedServiceMultiplier:0.00}[/]");

                if (pendingRequest != null)
                {
                    lines.Add("  Ожидающий запрос:");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(pendingRequest.RequestId)}[/]");
                    lines.Add($"    Создан на ходу: [dim]{pendingRequest.CreatedAtTurn}[/]");
                    lines.Add($"    Создан в UTC: [dim]{Markup.Escape(pendingRequest.CreatedAtUtc)}[/]");
                }
                else if (duplicatePendingRequests)
                {
                    lines.Add("  Ожидающий запрос: [red]для текущего цикла найдено несколько конкурирующих торговых контрактов[/]");
                    foreach (var duplicateRequest in matchingRequests.OrderBy(item => item.CreatedAtTurn).ThenBy(item => item.CreatedAtUtc, StringComparer.OrdinalIgnoreCase))
                    {
                        lines.Add($"    • requestId: [dim]{Markup.Escape(duplicateRequest.RequestId)}[/]");
                        lines.Add($"      Фракция: [dim]{Markup.Escape(duplicateRequest.FactionName)} ({Markup.Escape(duplicateRequest.FactionId)})[/]");
                        lines.Add($"      Цикл: [dim]{Markup.Escape(duplicateRequest.TradeCycleId)}[/]");
                        lines.Add($"      Расчётные значения: [dim]уровень {duplicateRequest.DerivedTradeTier}, слотов {duplicateRequest.DerivedTradeSlotCount}, редкость {Markup.Escape(duplicateRequest.DerivedRarityCeiling)}, услуги x{duplicateRequest.DerivedServiceMultiplier:0.00}[/]");
                        lines.Add($"      Создан: [dim]ход {duplicateRequest.CreatedAtTurn}, UTC {Markup.Escape(duplicateRequest.CreatedAtUtc)}[/]");
                    }
                }
                else
                {
                    lines.Add("  Ожидающий запрос: [dim]для текущего цикла активных запросов нет[/]");
                }

                if (tradeInventory != null)
                {
                    lines.Add("  Подготовленная витрина:");
                    lines.Add($"    Соответствует текущему контракту: {(inventoryMatchesCurrentContract ? "[green]да[/]" : "[yellow]нет[/]")}");
                    lines.Add($"    Идентификатор цикла: [dim]{Markup.Escape(GetNodeString(tradeInventory["tradeCycleId"]) ?? "?")}[/]");
                    if (!string.IsNullOrWhiteSpace(GetNodeString(tradeInventory["generatedAtUtc"])))
                        lines.Add($"    Подготовлена в UTC: [dim]{Markup.Escape(GetNodeString(tradeInventory["generatedAtUtc"])!)}[/]");
                    lines.Add($"    Уровень торговли при подготовке: [dim]{GetNodeInt(tradeInventory["generationTradeTier"])}[/]");
                    lines.Add($"    Потолок редкости при подготовке: [dim]{Markup.Escape(DescribeRarityLabel(GetNodeString(tradeInventory["generationRarityCeiling"]) ?? "?"))}[/]");
                    if (TryGetNodeDouble(tradeInventory["serviceMultiplierSnapshot"], out var serviceSnapshot))
                        lines.Add($"    Коэффициент услуг в момент подготовки: [dim]{serviceSnapshot:0.00}[/]");
                    lines.Add($"    Профиль торговца: [dim]{Markup.Escape(DescribeShiningMerchantProfile(GetNodeString(tradeInventory["merchantProfile"]) ?? ShiningTradeRequestState.MerchantProfileShiningFaction))}[/]");
                    lines.Add($"    Учёт витрины: [dim]слотов {itemCount}, распродано {soldOutCount}, доступно {remainingCount}[/]");

                    if (tradeInventory["items"] is JsonArray inventoryItems && inventoryItems.Count > 0)
                    {
                        lines.Add("    Содержимое витрины:");
                        foreach (var item in inventoryItems.OfType<JsonObject>())
                        {
                            var relicData = item["relicData"] as JsonObject;
                            var relicName = GetNodeString(relicData?["name"]) ?? GetNodeString(relicData?["relicId"]) ?? "реликвия";
                            var relicId = GetNodeString(relicData?["relicId"]) ?? "?";
                            var rarity = GetNodeString(relicData?["quality"]) ?? GetNodeString(relicData?["rarity"]) ?? "?";
                            var availability = GetNodeBool(item["soldOut"]) ? "распродано" : "доступно";
                            var slotId = GetNodeString(item["slotId"]) ?? "?";
                            lines.Add($"      • {Markup.Escape(DescribeShiningTradeSlotLabel(slotId))} — {Markup.Escape(relicName)} [dim]({Markup.Escape(DescribeForgeRarity(rarity))}, {GetNodeInt(item["priceInFeathers"])} 🪶, {Markup.Escape(availability)}; slotId {Markup.Escape(slotId)}; relicId {Markup.Escape(relicId)})[/]");
                        }
                    }
                }
                else
                {
                    lines.Add("  Подготовленная витрина: [dim]ещё не создана для текущего цикла[/]");
                }

                if (matchingReceipt != null)
                {
                    lines.Add("  Подтверждение исхода:");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(GetNodeString(displayedReceipt["requestId"]) ?? "?")}[/]");
                    lines.Add($"    Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(displayedReceipt["status"])))}[/]");
                    lines.Add($"    Подготовлено слотов: [dim]{GetNodeInt(displayedReceipt["itemCount"])}[/]");
                    lines.Add(TryReadIntegerNode(displayedReceipt["soldOutCount"], out var displayedSoldOutCount)
                        ? $"    Распродано: [dim]{displayedSoldOutCount}[/]"
                        : "    Распродано: [dim]не зафиксировано в этой исторической записи[/]");
                    lines.Add($"    Подтверждено на ходу: [dim]{GetNodeInt(displayedReceipt["resolvedAtTurn"])}[/]");
                    if (!string.IsNullOrWhiteSpace(GetNodeString(displayedReceipt["resolvedAtUtc"])))
                        lines.Add($"    Подтверждено в UTC: [dim]{Markup.Escape(GetNodeString(displayedReceipt["resolvedAtUtc"])!)}[/]");
                }
                else if (sameCycleReceipt != null)
                {
                    lines.Add("  Последняя запись этого цикла:");
                    lines.Add("    [dim]Строгое подтверждение текущего контракта не найдено; ниже показана только историческая запись того же цикла.[/]");
                    lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(GetNodeString(sameCycleReceipt["requestId"]) ?? "?")}[/]");
                    lines.Add($"    Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(sameCycleReceipt["status"])))}[/]");
                    lines.Add($"    Подготовлено слотов: [dim]{GetNodeInt(sameCycleReceipt["itemCount"])}[/]");
                    lines.Add(TryReadIntegerNode(sameCycleReceipt["soldOutCount"], out var sameCycleSoldOutCount)
                        ? $"    Распродано: [dim]{sameCycleSoldOutCount}[/]"
                        : "    Распродано: [dim]не зафиксировано в этой исторической записи[/]");
                    lines.Add($"    Подтверждено на ходу: [dim]{GetNodeInt(sameCycleReceipt["resolvedAtTurn"])}[/]");
                    if (!string.IsNullOrWhiteSpace(GetNodeString(sameCycleReceipt["resolvedAtUtc"])))
                        lines.Add($"    Подтверждено в UTC: [dim]{Markup.Escape(GetNodeString(sameCycleReceipt["resolvedAtUtc"])!)}[/]");
                }
                else
                {
                    lines.Add("  Подтверждение исхода: [dim]для текущего цикла ещё не записано[/]");
                }

                if (receiptHistory.Count > 0)
                {
                    lines.Add("  Полная история подтверждений:");
                    foreach (var receipt in receiptHistory)
                    {
                        var status = DescribeShiningResolutionStatus(GetNodeString(receipt["status"]));
                        var tradeCycleId = GetNodeString(receipt["tradeCycleId"]) ?? "?";
                        var requestId = GetNodeString(receipt["requestId"]) ?? "?";
                        var turn = GetNodeInt(receipt["resolvedAtTurn"]);
                        var itemCountForReceipt = GetNodeInt(receipt["itemCount"]);
                        var soldOutLabel = TryReadIntegerNode(receipt["soldOutCount"], out var parsedSoldOutForReceipt)
                            ? parsedSoldOutForReceipt.ToString()
                            : "не зафиксировано";
                        var turnText = turn > 0 ? $"ход {turn}" : "ход ?";
                        var cycleMarker = string.Equals(tradeCycleId, currentContract.TradeCycleId, StringComparison.OrdinalIgnoreCase)
                            ? " [dim](текущий цикл)[/]"
                            : string.Empty;
                        lines.Add($"    • цикл витрины [dim]{Markup.Escape(tradeCycleId)}[/]{cycleMarker} — {Markup.Escape(status)}, слотов {itemCountForReceipt}, распродано {Markup.Escape(soldOutLabel)}, {Markup.Escape(turnText)} [dim](идентификатор запроса {Markup.Escape(requestId)})[/]");
                    }
                }
                else
                {
                    lines.Add("  Полная история подтверждений: [dim]ещё не записана[/]");
                }
            }
        }

        var gachaSystem = ShiningAbodeState.EnsureGachaSystemObject(context.Root);
        if (gachaSystem["gachaHistory"] is JsonArray gachaHistory && gachaHistory.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Полная история сияющих призывов:[/]");
            foreach (var entry in gachaHistory.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["turnNumber"]))
                         .ThenByDescending(item => GetNodeString(item["timestamp"]), StringComparer.OrdinalIgnoreCase))
            {
                var factionName = GetNodeString(entry["factionName"]) ?? GetNodeString(entry["factionId"]) ?? "фракция";
                var factionId = GetNodeString(entry["factionId"]) ?? "?";
                var relicName = GetNodeString(entry["relicName"]) ?? GetNodeString(entry["relicId"]) ?? "реликвия";
                var relicId = GetNodeString(entry["relicId"]) ?? "?";
                var turnNumber = GetNodeInt(entry["turnNumber"]);
                var turnText = turnNumber > 0 ? $" [dim](ход {turnNumber})[/]" : string.Empty;
                lines.Add($"  • {Markup.Escape(factionName)} [dim]({Markup.Escape(factionId)})[/] — {Markup.Escape(DescribeForgeRarity(GetNodeString(entry["baseRarity"])))} -> {Markup.Escape(DescribeForgeRarity(GetNodeString(entry["finalRarity"])))}, {Markup.Escape(relicName)} [dim]({Markup.Escape(relicId)})[/]{turnText}");
                lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(GetNodeString(entry["requestId"]) ?? "?")}[/]");
                lines.Add($"    Цикл возвращения: [dim]{Markup.Escape(GetNodeString(entry["returnCycleId"]) ?? "?")}[/]");
                lines.Add($"    Стоимость в Перьях: [dim]{GetNodeInt(entry["costInFeathers"])}[/]");
                if (!string.IsNullOrWhiteSpace(GetNodeString(entry["timestamp"])))
                    lines.Add($"    Подтверждено в UTC: [dim]{Markup.Escape(GetNodeString(entry["timestamp"])!)}[/]");
            }
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧾 Торговые циклы ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var requestAudit = new JsonArray();
        foreach (var request in tradeRequests)
            requestAudit.Add(JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (requestAudit.Count > 0)
            WriteJsonAuditPanel("Полный JSON pending Shining trade requests", requestAudit, Color.Cyan1);

        if (context.Root["factions"] is JsonArray factionAudit)
            WriteJsonAuditPanel("Полный JSON сияющих фракций: tradeInventory, receipts, gacha history", factionAudit, Color.Gold1);

        if (context.Root["gachaSystem"] is JsonObject gachaAudit)
            WriteJsonAuditPanel("Полный JSON Shining gachaSystem", gachaAudit, Color.Gold1);
    }

    private static string DescribeShiningTradeSlotLabel(string? slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return "слот витрины";

        var digits = new string(slotId.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var slotNumber) && slotNumber > 0
            ? $"слот {slotNumber}"
            : "слот витрины";
    }

    private static string DescribeShiningMerchantProfile(string? merchantProfile) =>
        string.Equals(merchantProfile, ShiningTradeRequestState.MerchantProfileShiningFaction, StringComparison.OrdinalIgnoreCase)
            ? "сияющая фракция"
            : merchantProfile ?? "торговец";

    private static string DescribeReturnCycleStatus(string? returnCycleId) =>
        string.IsNullOrWhiteSpace(returnCycleId)
            ? "цикл возвращения не синхронизирован"
            : $"цикл возвращения «{returnCycleId}»";

    private Panel BuildShiningTradeAndForgePanel(
        JsonObject shiningRoot,
        JsonObject? soulRoot,
        JsonObject? residentRoot,
        IReadOnlyList<ShiningTradeRequestState.PendingShiningTradeInventoryRequest> tradeRequests)
    {
        var lines = new List<string>
        {
            "[bold yellow]⚒ Торговля и кузня[/]",
            ""
        };

        var gachaSystem = ShiningAbodeState.EnsureGachaSystemObject(shiningRoot);
        var chargesPerReturn = GetNodeInt(gachaSystem["chargesPerReturn"]);
        var remainingCharges = ShiningAbodeState.GetRemainingShiningGachaCharges(shiningRoot);
        var currentReturnCycleId = GetNodeString(gachaSystem["currentReturnCycleId"]) ?? "не синхронизирован";
        var gachaCost = ShiningAbodeState.GetShiningGachaPullCost();
        lines.Add($"[bold]Сияющая гача:[/] призыв реликвии за {gachaCost.Feathers} 🪶, попыток {remainingCharges}/{chargesPerReturn}, состояние цикла: [dim]{Markup.Escape(DescribeReturnCycleStatus(currentReturnCycleId))}[/]");

        if (shiningRoot["factions"] is not JsonArray factions || factions.Count == 0)
        {
            lines.Add("[dim]Сияющие фракции ещё не проявлены.[/]");
        }
        else
        {
            foreach (var faction in factions.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["factionStrength"])))
            {
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
                var strength = GetNodeInt(faction["factionStrength"]);
                var tradeTier = ShiningAbodeState.GetTradeTier(strength);
                var stockCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot);
                var rarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(strength);
                var serviceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength);
                var hasRefinement = ShiningAbodeState.FactionHasSupportedProjectArchetype(faction, ShiningAbodeState.ProjectArchetypeRefinement);
                var gachaBonusSteps = ShiningAbodeState.GetProjectedShiningGachaBonusSteps(shiningRoot, residentRoot, faction);
                var tradeStatus = tradeTier >= 1 ? "активна" : "спит";
                var currentContract = BuildShiningTradeContractSnapshot(soulRoot, residentRoot, faction);
                var sameCycleRequests = tradeRequests
                    .Where(request =>
                        string.Equals(request.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(request.TradeCycleId, currentContract.TradeCycleId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchingPendingRequest = sameCycleRequests.Count == 1 ? sameCycleRequests[0] : null;
                var hasDuplicatePendingRequests = sameCycleRequests.Count > 1;
                var tradeInventory = faction["tradeInventory"] as JsonObject;
                var inventoryMatchesCurrentContract = ShiningTradeRequestState.InventoryMatchesRequestContract(tradeInventory, currentContract);
                var inventoryReady = matchingPendingRequest != null
                    ? ShiningTradeRequestState.HasReadyInventoryForCurrentContract(faction, matchingPendingRequest)
                    : ShiningTradeRequestState.FindLatestAuthoritativeReadyReceiptForCurrentCycle(faction, currentContract.TradeCycleId) != null &&
                      inventoryMatchesCurrentContract;
                lines.Add($"• {Markup.Escape(factionName)} [dim]({Markup.Escape(factionId)})[/]");
                lines.Add($"  торговля {tradeStatus}, уровень {tradeTier}, витрина {stockCount}, потолок {Markup.Escape(rarityCeiling)}, услуги x{serviceMultiplier:0.00}, очищение {(hasRefinement ? "раскрыто" : "не раскрыто")}, бонус призыва +{gachaBonusSteps}");
                if (tradeTier >= 1)
                {
                    var inventoryState = inventoryReady
                        ? "готова по текущему контракту"
                        : hasDuplicatePendingRequests
                        ? "несколько pending-запросов одного цикла"
                        : matchingPendingRequest != null
                        ? "ожидает решения"
                        : tradeInventory != null
                        ? inventoryMatchesCurrentContract
                            ? "есть подходящая витрина без canonical ready receipt"
                            : "устарела или не совпадает с текущим контрактом"
                        : "не запрошена";
                    lines.Add($"  витрина: {inventoryState}");
                }
            }
        }

        if (gachaSystem["gachaHistory"] is JsonArray history && history.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Все призывы реликвий:[/] [dim](без сокращения)[/]");
            foreach (var entry in history.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["turnNumber"])))
            {
                var factionName = GetNodeString(entry["factionName"]) ?? GetNodeString(entry["factionId"]) ?? "фракция";
                var factionId = GetNodeString(entry["factionId"]) ?? "?";
                var relicName = GetNodeString(entry["relicName"]) ?? GetNodeString(entry["relicId"]) ?? "реликвия";
                var relicId = GetNodeString(entry["relicId"]) ?? "?";
                lines.Add($"  • {Markup.Escape(factionName)} [dim]({Markup.Escape(factionId)})[/] — {Markup.Escape(DescribeForgeRarity(GetNodeString(entry["baseRarity"])))} -> {Markup.Escape(DescribeForgeRarity(GetNodeString(entry["finalRarity"])))}, {Markup.Escape(relicName)} [dim]({Markup.Escape(relicId)})[/]");
                lines.Add($"    Идентификатор запроса: [dim]{Markup.Escape(GetNodeString(entry["requestId"]) ?? "?")}[/]");
                lines.Add($"    Цикл возвращения: [dim]{Markup.Escape(GetNodeString(entry["returnCycleId"]) ?? "?")}[/]");
                lines.Add($"    Стоимость в Перьях: [dim]{GetNodeInt(entry["costInFeathers"])}[/]");
            }
        }

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚒ Торговля и кузня ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold3),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static ShiningTradeRequestState.PendingShiningTradeInventoryRequest BuildShiningTradeContractSnapshot(
        JsonObject? soulRoot,
        JsonObject? residentRoot,
        JsonObject faction)
    {
        var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
        var strength = GetNodeInt(faction["factionStrength"]);
        return new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            FactionId = factionId,
            FactionName = factionName,
            TradeCycleId = ShiningAbodeState.GetTradeCycleId(GetNodeInt(soulRoot?["currentIncarnation"])),
            DerivedTradeTier = ShiningAbodeState.GetTradeTier(strength),
            DerivedTradeSlotCount = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot),
            DerivedRarityCeiling = ShiningAbodeState.GetTradeRarityCeiling(strength),
            DerivedServiceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength),
            MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction
        };
    }

    private static bool TryGetNodeDouble(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<double>(out value))
            return true;
        if (jsonValue.TryGetValue<float>(out var floatValue))
        {
            value = floatValue;
            return true;
        }
        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            value = (double)decimalValue;
            return true;
        }

        return false;
    }

    private string? PromptForShiningForgeActionType()
    {
        var actionChoices = new[]
        {
            (
                ActionType: ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                Label: "Перековать форму реликвии",
                Summary: "сменить форму реликвии",
                Tier: ShiningAbodeState.GetForgeRequiredRadianceTier(ShiningCoreActionRequestState.ActionTypeForgeRelicReshape)
            ),
            (
                ActionType: ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty,
                Label: "Перенастроить свойство реликвии",
                Summary: "заменить одно свойство на равноценное",
                Tier: ShiningAbodeState.GetForgeRequiredRadianceTier(ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty)
            ),
            (
                ActionType: ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand,
                Label: "Усилить ступень свойства",
                Summary: "поднять одно свойство на следующую ступень",
                Tier: ShiningAbodeState.GetForgeRequiredRadianceTier(ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand)
            ),
            (
                ActionType: ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho,
                Label: "Стабилизировать эхо реликвии",
                Summary: "усилить проявление спутника внутри реликвии",
                Tier: ShiningAbodeState.GetForgeRequiredRadianceTier(ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho)
            ),
            (
                ActionType: ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity,
                Label: "Возвысить редкость реликвии",
                Summary: "поднять реликвию на следующую ступень редкости",
                Tier: ShiningAbodeState.GetForgeRequiredRadianceTier(ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity)
            )
        };

        var choices = actionChoices
            .Select(item => (
                Choice: $"{item.Label} [dim](сияние {item.Tier}: {Markup.Escape(item.Summary)})[/]",
                item.ActionType))
            .ToList();

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Выберите действие кузни[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(choices.Select(item => item.Choice).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        return choices.First(item =>
            string.Equals(item.Choice, selected, StringComparison.Ordinal) ||
            item.Choice.Contains(selected, StringComparison.OrdinalIgnoreCase)).ActionType;
    }

    private bool ConfirmShiningForgeRequest(
        JsonObject faction,
        (string RelicId, string RelicName, JsonObject Relic) relicChoice,
        string actionType,
        string targetFormTag,
        int propertyIndex,
        JsonObject? replacementProperty,
        JsonArray? addedProperties,
        ShiningAbodeState.ResourceCost cost)
    {
        var lines = BuildShiningForgePreviewLines(
            faction,
            relicChoice,
            actionType,
            targetFormTag,
            propertyIndex,
            replacementProperty,
            addedProperties,
            cost);

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚒ Предварительный осмотр перековки ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var choice = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Подтвердить запрос на перековку[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("✅ Создать запрос", "← Отмена"));

        return choice.Contains("Создать запрос", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> BuildShiningForgePreviewLines(
        JsonObject faction,
        (string RelicId, string RelicName, JsonObject Relic) relicChoice,
        string actionType,
        string targetFormTag,
        int propertyIndex,
        JsonObject? replacementProperty,
        JsonArray? addedProperties,
        ShiningAbodeState.ResourceCost cost)
    {
        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "фракция";
        var relic = relicChoice.Relic;
        var requiredTier = ShiningAbodeState.GetForgeRequiredRadianceTier(actionType);
        var lines = new List<string>
        {
            "[bold yellow]⚒ Предварительный осмотр перековки[/]",
            "",
            $"[bold]Действие:[/] {Markup.Escape(DescribeShiningCoreActionLabel(actionType))}",
            $"[bold]Фракция:[/] {Markup.Escape(factionName)}",
            $"[bold]Реликвия:[/] {Markup.Escape(relicChoice.RelicName)}",
            $"[bold]Требуемое сияние:[/] уровень {requiredTier}",
            $"[bold]Цена:[/] {cost.Feathers} 🪶 / {cost.LightSparks} ✨"
        };

        switch (actionType)
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                lines.Add($"[bold]Форма реликвии:[/] {Markup.Escape(DescribeForgeFormTag(GetNodeString(relic["formTag"])))} → {Markup.Escape(DescribeForgeFormTag(targetFormTag))}");
                lines.Add("[bold]Итог:[/] реликвия сменит форму, но сохранит свою сущность и цену перековки.");
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                if (TryGetForgeProperty(relic, propertyIndex, out var currentProperty))
                {
                    lines.Add($"[bold]Выбранное свойство:[/] {Markup.Escape(RenderForgePropertyLabel(currentProperty, propertyIndex))}");
                }

                if (replacementProperty != null)
                {
                    lines.Add($"[bold]Новая версия свойства:[/] {Markup.Escape(RenderForgePropertyLabel(replacementProperty))}");
                }

                lines.Add("[bold]Итог:[/] старое свойство будет заменено на новое той же ступени.");
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                if (TryGetForgeProperty(relic, propertyIndex, out var strengthenedProperty))
                {
                    lines.Add($"[bold]Выбранное свойство:[/] {Markup.Escape(RenderForgePropertyLabel(strengthenedProperty, propertyIndex))}");
                    if (TryDescribeForgeBandUpgrade(strengthenedProperty["band"], out var currentBand, out var upgradedBand))
                        lines.Add($"[bold]Ступень свойства:[/] {Markup.Escape(currentBand)} → {Markup.Escape(upgradedBand)}");
                }

                lines.Add("[bold]Итог:[/] выбранное свойство будет усилено на следующий допустимый шаг.");
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
                lines.Add("[bold]Итог:[/] эхо внутри реликвии станет устойчивее и усилит будущее проявление спутника.");
                var currentBonus = GetNodeInt(relic["companionManifestationQualityBonus"]);
                if (currentBonus > 0)
                    lines.Add($"[bold]Текущий бонус проявления:[/] +{currentBonus}");
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                var currentRarity = ResolveForgeRarityKey(relic);
                var nextRarity = GetNextForgeRarityKey(currentRarity);
                lines.Add($"[bold]Редкость:[/] {Markup.Escape(DescribeForgeRarity(currentRarity))} → {Markup.Escape(DescribeForgeRarity(nextRarity))}");
                var currentPropertyCount = GetForgePropertyCount(relic);
                lines.Add($"[bold]Свойств у реликвии:[/] {currentPropertyCount}");
                var missingPropertyCount = Math.Max(0, GetForgeMinimumPropertyCount(nextRarity) - currentPropertyCount);
                if (missingPropertyCount <= 0)
                {
                    lines.Add("[bold]Дополнительные свойства:[/] не требуются.");
                }
                else if (addedProperties != null && addedProperties.Count > 0)
                {
                    var propertyLabels = addedProperties.OfType<JsonObject>()
                        .Select(property => RenderForgePropertyLabel(property))
                        .ToList();
                    lines.Add($"[bold]Будут добавлены свойства:[/] {Markup.Escape(string.Join("; ", propertyLabels))}");
                }
                else
                {
                    lines.Add($"[bold]Дополнительные свойства:[/] потребуется ещё {missingPropertyCount}.");
                }

                lines.Add("[bold]Итог:[/] редкость реликвии поднимется на следующую ступень, а недостающие свойства будут добавлены.");
                break;
        }

        return lines;
    }

    private async Task HandleShiningTradeMenuAsync(ShiningContext context)
    {
        var faction = PromptForFaction(context.Root, "Выберите сияющую фракцию для торговли");
        if (faction == null)
            return;

        var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
        while (true)
        {
            var view = await ShiningTradeService.ReadTradeViewAsync(_fs, factionId);
            if (view == null)
            {
                MarkupLine("[red]Не удалось загрузить trade view сияющей фракции.[/]");
                WaitForKey();
                return;
            }

            Clear();
            var headerLines = new List<string>
            {
                $"[bold cyan]🛒 Сияющая торговля: {Markup.Escape(view.FactionName)}[/]",
                $"[dim]Сила: {view.FactionStrength} • уровень торговли {view.TradeTier} • слотов витрины {view.StockItemCount} • потолок редкости {Markup.Escape(view.RarityCeiling)} • услуги x{view.ServiceMultiplier:0.00}[/]",
                $"[dim]Чернильные Перья: {await ReadInkFeathersBalance()} • цикл торговли {Markup.Escape(view.TradeCycleId)}[/]"
            };
            if (view.TradeBlocked)
                headerLines.Add($"[red]⛔ {Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
            else if (!string.IsNullOrWhiteSpace(view.InventoryStatusMessage))
                headerLines.Add($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage)}[/]");

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", headerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var actions = new List<string>();
            if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
                actions.Add("🧾 Запросить витрину");
            if (!view.TradeBlocked && view.InventoryReady)
                actions.Add("🛍 Купить реликвии");
            actions.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actions));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Запросить", StringComparison.Ordinal))
            {
                var currentTurn = await TryReadCurrentTurnNumberAsync();
                var request = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    FactionId = view.FactionId,
                    FactionName = view.FactionName,
                    TradeCycleId = view.TradeCycleId,
                    DerivedTradeTier = view.TradeTier,
                    DerivedTradeSlotCount = view.StockItemCount,
                    DerivedRarityCeiling = view.RarityCeiling,
                    DerivedServiceMultiplier = view.ServiceMultiplier,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                    CreatedAtTurn = currentTurn
                };

                var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MarkupLine($"[red]❌ {Markup.Escape(error)}[/]");
                    WaitForKey();
                    continue;
                }

                if (!ConfirmShiningTradeInventoryRequestPreview(request))
                    continue;

                error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MarkupLine($"[red]❌ {Markup.Escape(error)}[/]");
                    WaitForKey();
                    continue;
                }

                await ShiningTradeRequestState.WriteRequestAsync(_fs, request);
                MarkupLine(BuildShiningTradePostConfirmMarkup(request));
                WaitForKey();
                await _stateManager.RefreshGameStateAsync();
                continue;
            }

            if (choice.Contains("Купить", StringComparison.Ordinal))
            {
                await ShowShiningTradeBuyMenuAsync(factionId);
                await _stateManager.RefreshGameStateAsync();
            }
        }
    }

    private async Task HandleShiningRelicGachaRequestAsync(ShiningContext context)
    {
        if (context.SoulRoot == null)
        {
            MarkupLine("[yellow]soul_state.json недоступен для сияющей гачи.[/]");
            WaitForKey();
            return;
        }

        var faction = PromptForFaction(context.Root, "Выберите сияющую фракцию для призыва реликвии");
        if (faction == null)
            return;

        if (!ShiningAbodeState.TryQuoteRelicGachaPull(
                context.Root,
                context.SoulRoot,
                context.ResidentRoot,
                GetNodeString(faction["factionId"]),
                out var cost,
                out var projectedBonusSteps,
                out var returnCycleId,
                out var error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error ?? "Сияющая гача сейчас недоступна.")}[/]");
            WaitForKey();
            return;
        }

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            FactionId = GetNodeString(faction["factionId"]) ?? string.Empty,
            FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? string.Empty,
            RadianceTierAtRequest = GetNodeInt(context.Root["radiance"]?["tier"]),
            QuotedCostFeathers = cost.Feathers,
            QuotedCostLightSparks = cost.LightSparks,
            ReturnCycleId = returnCycleId,
            ProjectedGachaBonusSteps = projectedBonusSteps,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningCoreActionRequestPreview(context, request))
            return;

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine(BuildShiningCorePostConfirmMarkup(
            request,
            $"Следующий подтверждённый ход должен сгенерировать relicId/relicName, взять базовую редкость призыва, применить бонус до +{projectedBonusSteps}, обновить soul_state.json и coreActionReceipts[] с тем же requestId."));
        WaitForKey();
    }

    private async Task ShowShiningTradeBuyMenuAsync(string factionId)
    {
        while (true)
        {
            var view = await ShiningTradeService.ReadTradeViewAsync(_fs, factionId);
            if (view == null)
            {
                MarkupLine("[red]Не удалось загрузить trade view сияющей фракции.[/]");
                WaitForKey();
                return;
            }

            if (view.TradeBlocked)
            {
                MarkupLine($"[red]{Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            if (!view.InventoryReady)
            {
                MarkupLine($"[yellow]{Markup.Escape(view.InventoryStatusMessage ?? "Витрина сияющей фракции ещё не подготовлена.")}[/]");
                WaitForKey();
                return;
            }

            var feathers = await ReadInkFeathersBalance();
            var choices = view.Offers.Select(offer =>
            {
                var soldTag = offer.SoldOut ? "РАСПРОДАНО" : "";
                var relicId = GetNodeString(offer.RelicData["relicId"]) ?? GetNodeString(offer.RelicData["id"]) ?? "?";
                return ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
                    $"slotId {offer.SlotId}",
                    $"relicId {relicId}",
                    soldTag);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Сияющая витрина[/] [dim](перья: {feathers})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(10)
                .AddChoices(choices));
            if (selected.Contains("Назад", StringComparison.Ordinal))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= view.Offers.Count)
                return;

            var offer = view.Offers[selectedIndex];
            var canBuy = !offer.SoldOut && feathers >= offer.PriceInFeathers;
            var decision = ShowShiningTradeBuyPreview(offer, view, feathers, canBuy);
            if (decision != ShiningTradeBuyDecision.Buy)
                continue;

            var result = await ShiningTradeService.BuyAsync(_fs, factionId, offer.SlotId, await TryReadCurrentTurnNumberAsync());
            ShowShiningTradePurchaseResult(offer, view, feathers, result);
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private ShiningTradeBuyDecision ShowShiningTradeBuyPreview(
        ShiningTradeService.ShiningTradeOffer offer,
        ShiningTradeService.ShiningTradeView view,
        int currentFeathers,
        bool canBuy)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var projectedFeathers = Math.Max(0, currentFeathers - Math.Max(0, offer.PriceInFeathers));
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null, residentDoc: null, guardiansDoc: null);
        lines.Insert(1, $"  💰 Цена: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🛍️ Источник витрины: [cyan]{Markup.Escape(view.FactionName)}[/] [dim]({Markup.Escape(view.FactionId)})[/]");
        lines.Insert(3, $"  Цикл торговли: [dim]{Markup.Escape(view.TradeCycleId)}[/], уровень торговли {view.TradeTier}, потолок редкости {Markup.Escape(DescribeForgeRarity(view.RarityCeiling))}, услуги x{view.ServiceMultiplier:0.00}");
        lines.Insert(4, $"  Слот витрины: [dim]{Markup.Escape(offer.SlotId)}[/], распродан: {(offer.SoldOut ? "[red]да[/]" : "[green]нет[/]")}");
        lines.Insert(5, $"  🪶 Чернильные Перья: [gold1]{currentFeathers}[/] -> [gold1]{projectedFeathers}[/]");
        lines.Insert(6, "  Канонический локальный исход: списать Перья, добавить Реликвию Души в soul_state, пометить слот витрины как soldOut=true; GM turn не отправляется.");

        if (offer.SoldOut)
            lines.Insert(7, "  [red]Статус витрины: слот уже распродан.[/]");
        else if (currentFeathers < offer.PriceInFeathers)
            lines.Insert(7, "  [yellow]Статус покупки: пока не хватает Чернильных Перьев для покупки.[/]");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Сияющая реликвия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel("Полный JSON покупки сияющей витрины: предложение, чек и фрагмент состояния", BuildShiningTradeOfferAuditNode(offer, view, currentFeathers), Color.Gold1);

        var actions = new List<string>();
        if (canBuy)
            actions.Add("🛍 Купить");
        actions.Add("← Назад к витрине");

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(actions));

        return action.Contains("Купить", StringComparison.OrdinalIgnoreCase)
            ? ShiningTradeBuyDecision.Buy
            : ShiningTradeBuyDecision.Back;
    }

    private void ShowShiningTradePurchaseResult(
        ShiningTradeService.ShiningTradeOffer offer,
        ShiningTradeService.ShiningTradeView view,
        int prePurchaseFeathers,
        ShiningTradeService.ShiningTradeOperationResult result)
    {
        var relicId = GetNodeString(offer.RelicData["relicId"]) ?? GetNodeString(offer.RelicData["id"]) ?? "?";
        var projectedFeathers = result.Success
            ? Math.Max(0, prePurchaseFeathers - Math.Max(0, offer.PriceInFeathers))
            : prePurchaseFeathers;
        var lines = new List<string>
        {
            result.Success
                ? "[green]✅ Покупка сияющей витрины зафиксирована[/]"
                : "[red]❌ Покупка сияющей витрины не выполнена[/]",
            $"  Сообщение: [dim]{Markup.Escape(result.Message)}[/]",
            $"  Фракция: [white]{Markup.Escape(view.FactionName)}[/] [dim]({Markup.Escape(view.FactionId)})[/]",
            $"  Цикл торговли: [dim]{Markup.Escape(view.TradeCycleId)}[/]",
            $"  Слот: [dim]{Markup.Escape(offer.SlotId)}[/]",
            $"  Реликвия: [white]{Markup.Escape(offer.Name)}[/] [dim]({Markup.Escape(relicId)})[/]",
            $"  Цена: [yellow]{offer.PriceInFeathers} Перьев[/]",
            $"  Баланс Перьев: [gold1]{prePurchaseFeathers}[/] -> [gold1]{projectedFeathers}[/]",
            result.Success
                ? "  Sold-out state: [green]выбранный слот теперь soldOut=true[/]"
                : "  Sold-out state: [dim]не изменён[/]",
            result.Success
                ? "  Receipt/update: локальная покупка не создаёт GM-turn; она обновляет `soul_state.json` и `shining_abode_state.json` coordinated write."
                : "  Receipt/update: состояние не должно измениться."
        };

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Итог сияющей покупки ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(result.Success ? Color.Green : Color.Red),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var audit = BuildShiningTradeOfferAuditNode(offer, view, prePurchaseFeathers);
        audit["purchaseResult"] = new JsonObject
        {
            ["success"] = result.Success,
            ["stateChanged"] = result.StateChanged,
            ["message"] = result.Message,
            ["finalInkFeathers"] = projectedFeathers,
            ["relicId"] = relicId,
            ["slotId"] = offer.SlotId,
            ["tradeCycleId"] = view.TradeCycleId,
            ["soldOutAfterPurchase"] = result.Success
        };
        WriteJsonAuditPanel("Полный JSON итога покупки сияющей витрины", audit, result.Success ? Color.Green : Color.Red);
    }

    private bool ConfirmShiningTradeInventoryRequestPreview(ShiningTradeRequestState.PendingShiningTradeInventoryRequest request)
    {
        var lines = new List<string>
        {
            "[bold yellow]Перед записью pending Shining trade inventory request[/]",
            "",
            $"  Файл: [dim]{Markup.Escape(ShiningTradeRequestState.PendingRequestsPath)}[/]",
            $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
            $"  Фракция: [white]{Markup.Escape(request.FactionName)}[/] [dim]({Markup.Escape(request.FactionId)})[/]",
            $"  tradeCycleId: [dim]{Markup.Escape(request.TradeCycleId)}[/]",
            $"  createdAtTurn: [dim]{request.CreatedAtTurn}[/]",
            $"  createdAtUtc: [dim]{Markup.Escape(request.CreatedAtUtc)}[/]",
            "",
            "[bold]Расчётные значения контракта:[/]",
            $"  • derivedTradeTier: [white]{request.DerivedTradeTier}[/]",
            $"  • derivedTradeSlotCount: [white]{request.DerivedTradeSlotCount}[/]",
            $"  • derivedRarityCeiling: [white]{Markup.Escape(request.DerivedRarityCeiling)}[/]",
            $"  • derivedServiceMultiplier: [white]{request.DerivedServiceMultiplier:0.00}[/]",
            $"  • merchantProfile: [dim]{Markup.Escape(request.MerchantProfile)}[/]",
            "",
            "[bold]Контракт закрытия для GM:[/]",
            "  • accepted/ready: materialize exact `faction.tradeInventory` for this requestId/tradeCycleId.",
            "  • tradeInventory must include generatedAtUtc, generationTradeTier, generationRarityCeiling, serviceMultiplierSnapshot, merchantProfile and `items[]`.",
            "  • Each item requires unique slotId, priceInFeathers, soldOut boolean and nested relicData.",
            "  • Close through `tradeInventoryReceipts[]` with requestId, factionId, tradeCycleId, status=ready, itemCount, soldOutCount, resolvedAtTurn/resolvedAtUtc.",
            "  • Cancel here leaves no pending file changes."
        };

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧾 Предпросмотр сияющей торговой витрины ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel(
            "Полный JSON pending_shining_trade_inventory_requests.json.requests[0]",
            JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) as JsonObject,
            Color.Cyan1);
        WriteJsonAuditPanel(
            "Ожидаемый каркас faction.tradeInventoryReceipts[]",
            BuildShiningTradeInventoryExpectedReceiptAuditNode(request),
            Color.Cyan1);

        var choice = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Подтвердить запрос сияющей витрины[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices("✅ Создать pending request", "← Отмена"));

        return choice.Contains("Создать", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Подтвердить", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject BuildShiningTradeInventoryExpectedReceiptAuditNode(ShiningTradeRequestState.PendingShiningTradeInventoryRequest request) =>
        new()
        {
            ["requestId"] = request.RequestId,
            ["factionId"] = request.FactionId,
            ["factionName"] = request.FactionName,
            ["tradeCycleId"] = request.TradeCycleId,
            ["status"] = ShiningTradeRequestState.ReceiptStatusReady,
            ["itemCount"] = request.DerivedTradeSlotCount,
            ["soldOutCount"] = 0,
            ["resolvedAtTurn"] = "current turn number",
            ["resolvedAtUtc"] = "ISO-8601 UTC timestamp",
            ["stateEvidence"] = new JsonObject
            {
                ["generationTradeTier"] = request.DerivedTradeTier,
                ["generationRarityCeiling"] = request.DerivedRarityCeiling,
                ["serviceMultiplierSnapshot"] = request.DerivedServiceMultiplier,
                ["merchantProfile"] = request.MerchantProfile
            }
        };

    private static JsonObject BuildShiningTradeOfferAuditNode(
        ShiningTradeService.ShiningTradeOffer offer,
        ShiningTradeService.ShiningTradeView view,
        int currentFeathers) =>
        new()
        {
            ["sourceSurface"] = "shining_trade_purchase_preview",
            ["factionId"] = view.FactionId,
            ["factionName"] = view.FactionName,
            ["tradeCycleId"] = view.TradeCycleId,
            ["tradeTier"] = view.TradeTier,
            ["stockItemCount"] = view.StockItemCount,
            ["rarityCeiling"] = view.RarityCeiling,
            ["serviceMultiplier"] = view.ServiceMultiplier,
            ["currentInkFeathers"] = currentFeathers,
            ["projectedInkFeathers"] = Math.Max(0, currentFeathers - Math.Max(0, offer.PriceInFeathers)),
            ["affectedFiles"] = new JsonArray("game_state/meta/soul_state.json", ShiningAbodeState.StatePath),
            ["expectedLocalReceipt"] = new JsonObject
            {
                ["slotId"] = offer.SlotId,
                ["tradeCycleId"] = view.TradeCycleId,
                ["costInFeathers"] = offer.PriceInFeathers,
                ["soldOutAfterPurchase"] = true,
                ["gmTurnSent"] = false
            },
            ["expectedStateFragment"] = new JsonObject
            {
                ["soul_state.inkFeathers.current"] = Math.Max(0, currentFeathers - Math.Max(0, offer.PriceInFeathers)),
                ["soul_state.soulRelics.stored.add"] = offer.RelicData.DeepClone(),
                ["shining_abode_state.factions[].tradeInventory.items[].soldOut"] = true
            },
            ["offer"] = new JsonObject
            {
                ["slotId"] = offer.SlotId,
                ["name"] = offer.Name,
                ["rarity"] = offer.Rarity,
                ["priceInFeathers"] = offer.PriceInFeathers,
                ["soldOut"] = offer.SoldOut,
                ["description"] = offer.Description,
                ["relicData"] = offer.RelicData.DeepClone()
            }
        };

    private async Task HandleShiningForgeRequestAsync(ShiningContext context)
    {
        if (context.SoulRoot == null)
        {
            MarkupLine("[yellow]soul_state.json недоступен для кузни.[/]");
            WaitForKey();
            return;
        }

        var faction = PromptForFaction(context.Root, "Выберите фракцию для кузни");
        if (faction == null)
            return;

        var actionType = PromptForShiningForgeActionType();
        if (string.IsNullOrWhiteSpace(actionType))
            return;

        var relicChoice = PromptForSoulRelic(context.SoulRoot, "Выберите Реликвию Души для перековки");
        if (relicChoice == null)
            return;

        string targetFormTag = string.Empty;
        var propertyIndex = -1;
        var relicRerollsToCommit = 0;
        JsonObject? replacementProperty = null;
        JsonArray? addedProperties = null;

        switch (actionType)
        {
            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
                var reshapeChoice = await PromptForForgeReshapeTargetFormTagAsync(context.SoulRoot, relicChoice.Value.Relic);
                targetFormTag = reshapeChoice.TargetFormTag?.Trim() ?? string.Empty;
                relicRerollsToCommit = reshapeChoice.RerollsSpent;
                if (string.IsNullOrWhiteSpace(targetFormTag))
                    return;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
                propertyIndex = PromptForRelicPropertyIndex(relicChoice.Value.Relic, "Выберите свойство для перенастройки");
                if (propertyIndex < 0)
                    return;
                var retuneChoice = await PromptForForgeReplacementPropertyAsync(context.SoulRoot, relicChoice.Value.Relic, propertyIndex);
                replacementProperty = retuneChoice.ReplacementProperty;
                relicRerollsToCommit = retuneChoice.RerollsSpent;
                if (replacementProperty == null)
                    return;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
                propertyIndex = PromptForRelicPropertyIndex(relicChoice.Value.Relic, "Выберите свойство для усиления");
                if (propertyIndex < 0)
                    return;
                break;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                addedProperties = PromptForForgeAddedProperties(context.SoulRoot, relicChoice.Value.Relic);
                if (addedProperties == null)
                    return;
                break;
        }

        if (!ShiningAbodeState.TryQuoteForgeAction(
                context.Root,
                context.SoulRoot,
                context.ResidentRoot,
                actionType,
                GetNodeString(faction["factionId"]),
                relicChoice.Value.RelicId,
                targetFormTag,
                propertyIndex,
                replacementProperty,
                addedProperties,
                out var cost,
                out var error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error ?? "Действие кузни сейчас недоступно.")}[/]");
            WaitForKey();
            return;
        }

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = actionType,
            FactionId = GetNodeString(faction["factionId"]) ?? string.Empty,
            FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? string.Empty,
            RadianceTierAtRequest = GetNodeInt(context.Root["radiance"]?["tier"]),
            QuotedCostFeathers = cost.Feathers,
            QuotedCostLightSparks = cost.LightSparks,
            RelicId = relicChoice.Value.RelicId,
            RelicName = relicChoice.Value.RelicName,
            TargetFormTag = targetFormTag,
            PropertyIndex = propertyIndex,
            ReplacementProperty = replacementProperty?.DeepClone().AsObject(),
            AddedProperties = addedProperties?.DeepClone().AsArray(),
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningCoreActionRequestPreview(
                context,
                request,
                confirmationTitle: "Подтвердить запрос на перековку",
                confirmChoice: "✅ Создать запрос",
                relicRerollsToCommit: relicRerollsToCommit))
        {
            return;
        }

        if (relicRerollsToCommit > 0 &&
            !await ShiningBlessingEffectState.ConsumeRelicRerollsAsync(_fs, _stateManager.CurrentState.TurnNumber, relicRerollsToCommit))
        {
            MarkupLine("[yellow]Переброс благословением больше недоступен: entitlements изменились до подтверждения. Запрос на перековку не создан.[/]");
            WaitForKey();
            return;
        }

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine(BuildShiningCorePostConfirmMarkup(
            request,
            $"Следующий подтверждённый ход должен закрепить forge delta для relicId, списать {cost.Feathers}/{cost.LightSparks}, обновить soul_state.json и coreActionReceipts[] с тем же requestId."));
        WaitForKey();
    }

    private Task<(string? TargetFormTag, int RerollsSpent)> PromptForForgeReshapeTargetFormTagAsync(JsonObject soulRoot, JsonObject relic)
    {
        var currentFormTag = GetNodeString(relic["formTag"]) ?? string.Empty;
        var currentFormLabel = DescribeForgeFormTag(currentFormTag);
        var suggestions = EnumerateSoulRelics(soulRoot)
            .Select(item => GetNodeString(item.Relic["formTag"]) ?? string.Empty)
            .Where(formTag =>
                !string.IsNullOrWhiteSpace(formTag) &&
                !string.Equals(formTag, currentFormTag, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(formTag => formTag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (suggestions.Count == 0)
            return Task.FromResult<(string? TargetFormTag, int RerollsSpent)>((NormalizeForgeFormTagInput(
                Ask("[cyan]Новая форма реликвии:[/]", currentFormLabel).Trim(),
                currentFormTag,
                new[] { currentFormTag }), 0));

        var suggestionIndex = 0;
        var initialRerolls = ShiningBlessingEffectState.GetPendingRelicRerolls(soulRoot);
        var rerollsReserved = 0;
        while (true)
        {
            var suggestion = suggestions[suggestionIndex % suggestions.Count];
            var rerollsRemaining = Math.Max(0, initialRerolls - rerollsReserved);
            var actions = new List<string>
            {
                $"✅ Использовать предложенную форму: {DescribeForgeFormTag(suggestion)}",
                "✍ Ввести форму вручную",
                "← Отмена"
            };
            if (rerollsRemaining > 0 && suggestions.Count > 1)
                actions.Insert(1, $"🔄 Перебросить благословением ({rerollsRemaining})");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Новая форма реликвии[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(actions));

            if (choice.Contains("Использовать предложенную форму", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(string? TargetFormTag, int RerollsSpent)>((suggestion, rerollsReserved));
            if (choice.Contains("Ввести форму вручную", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(string? TargetFormTag, int RerollsSpent)>((NormalizeForgeFormTagInput(
                    Ask("[cyan]Новая форма реликвии:[/]", DescribeForgeFormTag(suggestion)).Trim(),
                    suggestion,
                    suggestions.Append(currentFormTag)), rerollsReserved));
            if (choice.Contains("Перебросить", StringComparison.OrdinalIgnoreCase))
            {
                if (rerollsRemaining > 0)
                {
                    rerollsReserved += 1;
                    suggestionIndex += 1;
                    continue;
                }

                MarkupLine("[yellow]Нет доступных перебросов благословением для кузни.[/]");
                WaitForKey();
                continue;
            }

            return Task.FromResult<(string? TargetFormTag, int RerollsSpent)>((null, 0));
        }
    }

    private static string NormalizeForgeFormTagInput(
        string? input,
        string fallbackCanonicalFormTag,
        IEnumerable<string> knownFormTags)
    {
        var trimmed = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        foreach (var formTag in knownFormTags
                     .Where(formTag => !string.IsNullOrWhiteSpace(formTag))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(trimmed, formTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, DescribeForgeFormTag(formTag), StringComparison.OrdinalIgnoreCase))
            {
                return formTag;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackCanonicalFormTag) &&
            string.Equals(trimmed, DescribeForgeFormTag(fallbackCanonicalFormTag), StringComparison.OrdinalIgnoreCase))
        {
            return fallbackCanonicalFormTag;
        }

        return trimmed;
    }

    private Task<(JsonObject? ReplacementProperty, int RerollsSpent)> PromptForForgeReplacementPropertyAsync(JsonObject soulRoot, JsonObject relic, int propertyIndex)
    {
        if (!TryGetForgeProperty(relic, propertyIndex, out var currentProperty))
            return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((null, 0));

        var suggestions = BuildForgeReplacementPropertySuggestions(soulRoot, relic, propertyIndex);
        if (suggestions.Count == 0)
        {
            var template = BuildForgeReplacementPropertyTemplate(currentProperty);
            var templateLabel = RenderForgePropertyLabel(template);
            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Новая версия свойства[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(
                    $"✅ Использовать базовый шаблон: {templateLabel}",
                    "✍ Настроить свойство вручную",
                    "← Отмена"));

            if (choice.Contains("Использовать базовый шаблон", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((template, 0));

            if (choice.Contains("Отмена", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((null, 0));

            return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((PromptForStructuredForgePropertyAsync(template, "Новое свойство"), 0));
        }

        var suggestionIndex = 0;
        var initialRerolls = ShiningBlessingEffectState.GetPendingRelicRerolls(soulRoot);
        var rerollsReserved = 0;
        while (true)
        {
            var suggestion = suggestions[suggestionIndex % suggestions.Count];
            var suggestionLabel = RenderForgePropertyLabel(suggestion);
            var rerollsRemaining = Math.Max(0, initialRerolls - rerollsReserved);
            var actions = new List<string>
            {
                $"✅ Использовать предложенный вариант: {suggestionLabel}",
                "✍ Настроить вручную",
                "← Отмена"
            };
            if (rerollsRemaining > 0 && suggestions.Count > 1)
                actions.Insert(1, $"🔄 Перебросить благословением ({rerollsRemaining})");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Новая версия свойства[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(actions));

            if (choice.Contains("Использовать предложенный вариант", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((suggestion.DeepClone().AsObject(), rerollsReserved));
            if (choice.Contains("Настроить вручную", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((PromptForStructuredForgePropertyAsync(suggestion, "Новое свойство"), rerollsReserved));

            if (choice.Contains("Перебросить", StringComparison.OrdinalIgnoreCase))
            {
                if (rerollsRemaining > 0)
                {
                    rerollsReserved += 1;
                    suggestionIndex += 1;
                    continue;
                }

                MarkupLine("[yellow]Нет доступных перебросов благословением для кузни.[/]");
                WaitForKey();
                continue;
            }

            return Task.FromResult<(JsonObject? ReplacementProperty, int RerollsSpent)>((null, 0));
        }
    }

    private JsonArray? PromptForForgeAddedProperties(JsonObject soulRoot, JsonObject relic)
    {
        var currentRarity = ResolveForgeRarityKey(relic);
        var nextRarity = GetNextForgeRarityKey(currentRarity);
        var currentPropertyCount = GetForgePropertyCount(relic);
        var missingPropertyCount = Math.Max(0, GetForgeMinimumPropertyCount(nextRarity) - currentPropertyCount);
        if (missingPropertyCount <= 0)
            return new JsonArray();

        var suggestions = BuildForgeAdditionalPropertySuggestions(soulRoot, relic);
        var template = BuildForgeAddedPropertiesTemplate(suggestions, missingPropertyCount, nextRarity);
        var templatePreview = BuildForgePropertyPreviewSummary(template.OfType<JsonObject>());
        var useTemplateLabel = suggestions.Count >= missingPropertyCount
            ? $"✅ Использовать предложенные свойства: {templatePreview}"
            : $"✅ Использовать подготовленный набор: {templatePreview}";
        var manualEditLabel = suggestions.Count >= missingPropertyCount
            ? "✍ Настроить вручную"
            : "✍ Настроить набор вручную";
        var choice = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Дополнительные свойства для новой редкости[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(
                useTemplateLabel,
                manualEditLabel,
                "← Отмена"));

        if (choice.Contains("Использовать", StringComparison.OrdinalIgnoreCase))
            return template;

        if (choice.Contains("Отмена", StringComparison.OrdinalIgnoreCase))
            return null;

        return PromptForStructuredForgePropertiesAsync(template, nextRarity);
    }

    private (string RelicId, string RelicName, JsonObject Relic)? PromptForSoulRelic(JsonObject soulRoot, string title)
    {
        var relics = EnumerateSoulRelics(soulRoot)
            .OrderBy(item => item.RelicName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (relics.Count == 0)
        {
            MarkupLine("[yellow]У души сейчас нет доступных реликвий души.[/]");
            WaitForKey();
            return null;
        }

        var choices = relics.Select(item =>
        {
            var rarity = GetNodeString(item.Relic["quality"]) ?? GetNodeString(item.Relic["rarity"]) ?? "?";
            var formTag = GetNodeString(item.Relic["formTag"]);
            var propertyCount = GetForgePropertyCount(item.Relic);
            var label = $"{item.RelicName} [dim](relicId={Markup.Escape(item.RelicId)} • {Markup.Escape(rarity)} • {Markup.Escape(DescribeSoulRelicCollection(item.Collection))} • свойств {propertyCount})[/]" +
                        (string.IsNullOrWhiteSpace(formTag) ? string.Empty : $" [grey]форма: {Markup.Escape(DescribeForgeFormTag(formTag))}[/]");
            return (Label: label, Item: item);
        }).ToList();

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(choices.Select(item => item.Label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        var choice = choices.First(item =>
            string.Equals(item.Label, selected, StringComparison.Ordinal) ||
            item.Label.Contains(selected, StringComparison.OrdinalIgnoreCase)).Item;
        return (choice.RelicId, choice.RelicName, choice.Relic);
    }

    private int PromptForRelicPropertyIndex(JsonObject relic, string title)
    {
        if (relic["properties"] is not JsonArray properties || properties.Count == 0)
        {
            MarkupLine("[yellow]У выбранной реликвии нет списка свойств для перековки.[/]");
            WaitForKey();
            return -1;
        }

        var entries = properties
            .Select((node, index) => (Index: index, Property: node as JsonObject))
            .Where(item => item.Property != null)
            .Select(item =>
            {
                var propertyLabel = RenderForgePropertyLabel(item.Property!, item.Index);
                return ($"{propertyLabel}", item.Index);
            })
            .ToList();

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(entries.Select(item => item.Item1).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return -1;

        return entries.First(item =>
            string.Equals(item.Item1, selected, StringComparison.Ordinal) ||
            item.Item1.Contains(selected, StringComparison.OrdinalIgnoreCase)).Index;
    }

    private static List<JsonObject> BuildForgeReplacementPropertySuggestions(JsonObject soulRoot, JsonObject relic, int propertyIndex)
    {
        if (relic["properties"] is not JsonArray currentProperties ||
            propertyIndex < 0 ||
            propertyIndex >= currentProperties.Count ||
            currentProperties[propertyIndex] is not JsonObject currentProperty)
        {
            return new List<JsonObject>();
        }

        var currentBand = GetNodeString(currentProperty["band"]) ?? string.Empty;
        var currentKey = BuildForgePropertySuggestionKey(currentProperty);
        return EnumerateSoulRelics(soulRoot)
            .SelectMany(item => (item.Relic["properties"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            .Where(property =>
                string.Equals(GetNodeString(property["band"]) ?? string.Empty, currentBand, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(BuildForgePropertySuggestionKey(property), currentKey, StringComparison.OrdinalIgnoreCase))
            .GroupBy(BuildForgePropertySuggestionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().DeepClone().AsObject())
            .OrderBy(property => BuildForgePropertySuggestionKey(property), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildForgePropertySuggestionKey(JsonObject property)
    {
        var propertyId = GetNodeString(property["propertyId"]) ?? string.Empty;
        var name = GetNodeString(property["name"]) ?? string.Empty;
        var stat = GetNodeString(property["stat"]) ?? string.Empty;
        var band = GetNodeString(property["band"]) ?? string.Empty;
        return $"{propertyId}|{name}|{stat}|{band}";
    }

    private static string RenderForgePropertyLabel(JsonObject property, int? propertyIndex = null)
    {
        var propertyName = GetNodeString(property["name"]) ??
                           DescribeShiningForgeStat(GetNodeString(property["stat"])) ??
                           HumanizeProtocolToken(GetNodeString(property["propertyId"])) ??
                           "свойство";
        var prefix = propertyIndex.HasValue ? $"Свойство {propertyIndex.Value + 1}: " : string.Empty;
        return $"{prefix}{propertyName} (ступень: {DescribeForgeBand(property["band"])})";
    }

    private static JsonObject BuildForgeReplacementPropertyTemplate(JsonObject currentProperty)
    {
        return new JsonObject
        {
            ["propertyId"] = "new_property",
            ["band"] = currentProperty["band"]?.DeepClone() ?? JsonValue.Create("rare")
        };
    }

    private static JsonArray BuildForgeAddedPropertiesTemplate(
        IReadOnlyList<JsonObject> suggestions,
        int missingPropertyCount,
        string fallbackBand)
    {
        var template = new JsonArray();
        foreach (var suggestion in suggestions.Take(Math.Max(0, missingPropertyCount)))
            template.Add(suggestion.DeepClone());

        var normalizedBand = string.IsNullOrWhiteSpace(fallbackBand) ? "rare" : fallbackBand;
        while (template.Count < missingPropertyCount)
        {
            template.Add(new JsonObject
            {
                ["propertyId"] = $"new_property_{template.Count + 1}",
                ["band"] = normalizedBand
            });
        }

        return template;
    }

    private static string BuildForgePropertyPreviewSummary(IEnumerable<JsonObject> properties)
    {
        var labels = properties
            .Select(property => RenderForgePropertyLabel(property))
            .ToList();
        if (labels.Count == 0)
            return "без дополнительных свойств";

        return string.Join("; ", labels);
    }

    private JsonObject? PromptForStructuredForgePropertyAsync(JsonObject template, string title)
    {
        var displayName = Ask("[cyan]Игровое название свойства[/]", GetNodeString(template["name"]) ?? "").Trim();
        var statChoices = new Dictionary<string, string>
        {
            ["social"] = "Социальное влияние",
            ["resource"] = "Ресурсы",
            ["memory"] = "Память",
            ["route"] = "Путь",
            ["lore"] = "Знание",
            ["relic"] = "Реликвия",
            ["survival"] = "Выживание",
            ["descent"] = "Нисхождение"
        };
        var statDefault = GetNodeString(template["stat"]) ?? "";
        var statChoice = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}: характеристика[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(statChoices.Values.Append("Без привязки")));
        var stat = statChoice == "Без привязки"
            ? string.Empty
            : statChoices.First(choice => choice.Value == statChoice).Key;
        if (string.IsNullOrWhiteSpace(stat) && !string.IsNullOrWhiteSpace(statDefault))
            stat = statDefault;

        var propertyId = BuildStructuredForgePropertyId(template, displayName, stat);
        var bandNode = PromptForStructuredForgeBand(template["band"]);
        if (bandNode == null)
            return null;

        var description = Ask("[cyan]Краткое описание свойства[/]", GetNodeString(template["description"]) ?? "").Trim();

        var property = template.DeepClone().AsObject();
        property["propertyId"] = propertyId;
        property["band"] = bandNode;

        if (string.IsNullOrWhiteSpace(displayName))
            property.Remove("name");
        else
            property["name"] = displayName;

        if (string.IsNullOrWhiteSpace(stat))
            property.Remove("stat");
        else
            property["stat"] = stat;

        if (string.IsNullOrWhiteSpace(description))
            property.Remove("description");
        else
            property["description"] = description;

        return property;
    }

    private static string BuildStructuredForgePropertyId(JsonObject template, string displayName, string stat)
    {
        var existingId = GetNodeString(template["propertyId"]) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(existingId) &&
            !existingId.StartsWith("new_property", StringComparison.OrdinalIgnoreCase))
        {
            return existingId;
        }

        var normalizedName = new string((displayName ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray())
            .Trim('_');

        if (!string.IsNullOrWhiteSpace(normalizedName))
            return $"{(string.IsNullOrWhiteSpace(stat) ? "custom" : stat)}_{normalizedName}";

        return !string.IsNullOrWhiteSpace(existingId)
            ? existingId
            : $"{(string.IsNullOrWhiteSpace(stat) ? "custom" : stat)}_property";
    }

    private JsonNode? PromptForStructuredForgeBand(JsonNode? templateBand)
    {
        if (templateBand is JsonValue numericBandValue && numericBandValue.TryGetValue<int>(out var numericBand))
        {
            var stepChoices = Enumerable.Range(1, Math.Max(5, numericBand + 2))
                .Select(value => (Label: $"Ступень {value}", Value: value))
                .ToList();
            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Ступень свойства[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(stepChoices.Select(choice => choice.Label)));
            return JsonValue.Create(stepChoices.First(choice => choice.Label == selected).Value);
        }

        var rarityChoices = new[]
        {
            ("common", "Обычная"),
            ("uncommon", "Необычная"),
            ("rare", "Редкая"),
            ("epic", "Эпическая"),
            ("legendary", "Легендарная")
        };
        var fallback = (GetNodeString(templateBand) ?? "rare").Trim().ToLowerInvariant();
        var selectedLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Редкость свойства[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(rarityChoices.Select(choice => choice.Item2)));
        var selectedBand = rarityChoices.First(choice => choice.Item2 == selectedLabel).Item1;
        return JsonValue.Create(string.IsNullOrWhiteSpace(selectedBand) ? fallback : selectedBand);
    }

    private JsonArray? PromptForStructuredForgePropertiesAsync(JsonArray template, string fallbackBand)
    {
        var result = new JsonArray();
        var templates = template.OfType<JsonObject>().ToList();
        if (templates.Count == 0)
            return result;

        for (var index = 0; index < templates.Count; index++)
        {
            var propertyTemplate = templates[index].DeepClone().AsObject();
            if (propertyTemplate["band"] == null && !string.IsNullOrWhiteSpace(fallbackBand))
                propertyTemplate["band"] = fallbackBand;

            var property = PromptForStructuredForgePropertyAsync(propertyTemplate, $"Дополнительное свойство {index + 1}");
            if (property == null)
                return null;

            result.Add(property);
        }

        return result;
    }

    private static bool TryGetForgeProperty(JsonObject relic, int propertyIndex, out JsonObject property)
    {
        property = null!;
        if (relic["properties"] is not JsonArray properties ||
            propertyIndex < 0 ||
            propertyIndex >= properties.Count ||
            properties[propertyIndex] is not JsonObject propertyObject)
        {
            return false;
        }

        property = propertyObject;
        return true;
    }

    private static int GetForgePropertyCount(JsonObject relic) => (relic["properties"] as JsonArray)?.Count ?? 0;

    private static string DescribeSoulRelicCollection(string collection) =>
        (collection ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "equipped" => "экипировано",
            "stored" => "хранилище",
            _ => string.IsNullOrWhiteSpace(collection) ? "неизвестно" : collection
        };

    private static string DescribeForgeFormTag(string? formTag)
    {
        if (string.IsNullOrWhiteSpace(formTag))
            return "неопределённая форма";

        var normalized = formTag.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "glass_path" => "стекло пути",
            "solar_crown" => "солнечный венец",
            "lance" => "копьё",
            _ => HumanizeForgeFormTag(normalized)
        };
    }

    private static string HumanizeForgeFormTag(string formTag)
    {
        var words = formTag
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.ToLowerInvariant() switch
            {
                "glass" => "стекло",
                "path" => "путь",
                "solar" => "солнечный",
                "crown" => "венец",
                "lance" => "копьё",
                "blade" => "клинок",
                "spear" => "копьё",
                "lantern" => "фонарь",
                "mirror" => "зеркало",
                "chalice" => "чаша",
                "shape" => "форма",
                "form" => "форма",
                "sigil" => "печать",
                "sun" => "солнце",
                "moon" => "луна",
                "star" => "звезда",
                "oath" => "клятва",
                "memory" => "память",
                "dawn" => "рассвет",
                "radiant" => "сияющий",
                "route" => "путь",
                _ => word
            })
            .ToArray();

        if (words.Length == 0)
            return "неопределённая форма";

        return string.Join(' ', words);
    }

    private static string ResolveForgeRarityKey(JsonObject relic)
    {
        var rarity = GetNodeString(relic["quality"]);
        if (string.IsNullOrWhiteSpace(rarity))
            rarity = GetNodeString(relic["rarity"]);

        return (rarity ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string GetNextForgeRarityKey(string rarityKey) =>
        rarityKey switch
        {
            "common" => "uncommon",
            "uncommon" => "rare",
            "rare" => "epic",
            "epic" => "legendary",
            "legendary" => "legendary",
            _ => rarityKey
        };

    private static int GetForgeMinimumPropertyCount(string rarityKey) =>
        rarityKey switch
        {
            "common" => 1,
            "uncommon" => 2,
            "rare" => 3,
            "epic" => 4,
            "legendary" => 5,
            _ => 1
        };

    private static string DescribeForgeRarity(string rarityKey) =>
        rarityKey switch
        {
            "common" => "обычная",
            "uncommon" => "необычная",
            "rare" => "редкая",
            "epic" => "эпическая",
            "legendary" => "легендарная",
            _ => string.IsNullOrWhiteSpace(rarityKey) ? "неизвестная" : rarityKey
        };

    private static string DescribeForgeBand(JsonNode? bandNode)
    {
        if (bandNode is JsonValue value)
        {
            if (value.TryGetValue<int>(out var numericBand))
                return $"ступень {numericBand}";

            if (value.TryGetValue<string>(out var stringBand))
            {
                var normalized = stringBand.Trim().ToLowerInvariant();
                if (int.TryParse(normalized, out var parsedBand))
                    return $"ступень {parsedBand}";

                return DescribeForgeRarity(normalized);
            }
        }

        return "неизвестна";
    }

    private static bool TryDescribeForgeBandUpgrade(JsonNode? bandNode, out string currentBand, out string upgradedBand)
    {
        currentBand = DescribeForgeBand(bandNode);
        upgradedBand = string.Empty;

        if (bandNode is not JsonValue value)
            return false;

        if (value.TryGetValue<int>(out var numericBand))
        {
            upgradedBand = $"ступень {numericBand + 1}";
            return true;
        }

        if (!value.TryGetValue<string>(out var stringBand))
            return false;

        var normalized = stringBand.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, out var parsedBand))
        {
            upgradedBand = $"ступень {parsedBand + 1}";
            return true;
        }

        upgradedBand = normalized switch
        {
            "common" => "необычная",
            "uncommon" => "редкая",
            "rare" => "эпическая",
            "epic" => "легендарная",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(upgradedBand);
    }

    private static List<JsonObject> BuildForgeAdditionalPropertySuggestions(JsonObject soulRoot, JsonObject relic)
    {
        var existingKeys = (relic["properties"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Select(BuildForgePropertySuggestionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return EnumerateSoulRelics(soulRoot)
            .SelectMany(item => (item.Relic["properties"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            .Where(property => !existingKeys.Contains(BuildForgePropertySuggestionKey(property)))
            .GroupBy(BuildForgePropertySuggestionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().DeepClone().AsObject())
            .OrderBy(property => BuildForgePropertySuggestionKey(property), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<(string RelicId, string RelicName, string Collection, JsonObject Relic)> EnumerateSoulRelics(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                {
                    var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(relicId))
                        continue;

                    var relicName = GetNodeString(relic["name"]) ?? relicId;
                    yield return (relicId, relicName, collectionName, relic);
                }
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
            {
                var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(relicId))
                    continue;

                var relicName = GetNodeString(relic["name"]) ?? relicId;
                yield return (relicId, relicName, "stored", relic);
            }
        }
    }

    private static bool TryParseJsonObjectInput(string raw, out JsonObject? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            value = JsonNode.Parse(raw) as JsonObject;
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonArrayInput(string raw, out JsonArray? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            value = JsonNode.Parse(raw) as JsonArray;
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
