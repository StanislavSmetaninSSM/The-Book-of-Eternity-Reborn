using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private sealed record ShiningContext(JsonObject Root, JsonObject? ResidentRoot, JsonObject? GuardiansRoot, JsonObject? SoulRoot);

    private async Task<ShiningContext?> LoadShiningContextAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return null;

        JsonObject? shiningRoot;
        try
        {
            shiningRoot = JsonNode.Parse(shiningJson) as JsonObject;
        }
        catch
        {
            return null;
        }

        JsonObject? residentRoot = null;
        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (!string.IsNullOrWhiteSpace(residentJson))
        {
            try
            {
                residentRoot = JsonNode.Parse(residentJson) as JsonObject;
            }
            catch
            {
                residentRoot = null;
            }
        }

        JsonObject? guardiansRoot = null;
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansJson))
        {
            try
            {
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
            }
            catch
            {
                guardiansRoot = null;
            }
        }

        JsonObject? soulRoot = null;
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (!string.IsNullOrWhiteSpace(soulJson))
        {
            try
            {
                soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            }
            catch
            {
                soulRoot = null;
            }
        }

        ShiningAbodeState.NormalizeStateRoot(shiningRoot!, residentRoot, guardiansRoot);
        return new ShiningContext(shiningRoot!, residentRoot, guardiansRoot, soulRoot);
    }

    private async Task SaveShiningRootAsync(JsonObject root)
    {
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();
    }

    private Panel BuildShiningOverviewPanel(JsonObject shiningRoot, JsonObject? residentRoot, JsonObject? guardiansRoot)
    {
        var lines = new List<string>
        {
            "[bold yellow]✨ Сияющая Обитель[/]",
            "",
            "[bold]Текущее состояние:[/]",
            $"  • Мир: [white]{Markup.Escape(_stateManager.CurrentState.CurrentRealm)}[/]",
            $"  • Доступность: [white]{Markup.Escape(DescribeShiningAvailability(GetNodeString(shiningRoot["availability"])))}[/]",
            $"  • Сияние: [yellow]{GetNodeInt(shiningRoot["radiance"]?["experience"])} опыта[/] [dim](уровень сияния {GetNodeInt(shiningRoot["radiance"]?["tier"])})[/]",
            $"  • Искры Света: [gold1]{GetNodeInt(shiningRoot["lightSparks"])}[/]",
            $"  • Сияющая гача: [white]{ShiningAbodeState.GetRemainingShiningGachaCharges(shiningRoot)}[/]/[white]{GetNodeInt(shiningRoot["gachaSystem"]?["chargesPerReturn"])}[/] [dim]({BuildShiningReturnCycleStatusLabel(shiningRoot)})[/]",
            $"  • Залов: [white]{(shiningRoot["halls"] as JsonArray)?.Count ?? 0}[/]",
            $"  • Фракций: [white]{(shiningRoot["factions"] as JsonArray)?.Count ?? 0}[/]",
            $"  • Политических акторов: [white]{(shiningRoot["shiningPoliticalActors"] as JsonArray)?.Count ?? 0}[/]"
        };

        if (shiningRoot["pendingNativeFactionDiscovery"] is JsonObject pendingDiscovery)
        {
            lines.Add("");
            lines.Add("[bold orange1]Ожидает решения:[/]");
            lines.Add($"  • Открытие нативной фракции [dim](уровень сияния при запросе {GetNodeInt(pendingDiscovery["radianceTierAtRequest"])}, запрос {Markup.Escape(GetNodeString(pendingDiscovery["requestId"]) ?? "?")})[/]");
            lines.Add("  • [dim]Откройте подробный осмотр в этом разделе, чтобы увидеть полную стоимость и payload запроса.[/]");
        }

        if (shiningRoot["gates"] is JsonObject gates)
        {
            var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
            lines.Add("");
            lines.Add("[bold]Врата и благословения:[/]");
            if (GetNodeBool(gates["hasOpenDraft"]))
            {
                lines.Add($"  • Набор благословений открыт: показано {(gates["shownBlessingCardIds"] as JsonArray)?.Count ?? 0}, выбрано {(gates["selectedBlessingCardIds"] as JsonArray)?.Count ?? 0}, перебросов {GetNodeInt(gates["rerollsRemaining"])}");
                lines.Add($"  • Лимит выбора: {ShiningAbodeState.GetPickCap(radianceTier)} • расчётный размер набора: {ShiningAbodeState.GetDraftSize(radianceTier)}");
            }
            else
            {
                lines.Add("  • Врата пока закрыты.");
            }

            if (GetNodeBool(gates["isStale"]))
                lines.Add($"  • Черновик устарел: открой Врата заново [dim](версия {GetNodeInt(gates["draftVersion"])})[/]");
        }

        if (shiningRoot["preparedIncarnationPackage"] is JsonObject package)
        {
            var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
            var selectedIds = GetPreparedPackageSelectedCardIds(package);
            var selectedCards = GetConsistentPreparedPackageCards(package);
            var selectedLabels = selectedIds
                .Select(id => ResolveShiningBlessingCardLabel(shiningRoot, id))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();
            lines.Add("");
            lines.Add("[bold khaki1]Следующая жизнь:[/]");
            lines.Add($"  • Подготовлен пакет благословений: {selectedIds.Count} карт(ы)");
            lines.Add($"  • Основан на наборе Врат версии {GetNodeInt(package["generatedFromDraftVersion"])}");
            lines.Add($"  • Лимит выбора: {ShiningAbodeState.GetPickCap(radianceTier)} • расчётный размер набора: {ShiningAbodeState.GetDraftSize(radianceTier)}");
            lines.Add($"  • Зафиксирован на ходу {GetNodeInt(package["preparedAtTurn"])} [dim]({Markup.Escape(GetNodeString(package["preparedAtUtc"]) ?? "UTC не указан")})[/]");
            if (selectedLabels.Count > 0)
                lines.Add($"  • Зафиксированные карты: {Markup.Escape(string.Join(", ", selectedLabels))}");
            if (selectedCards.Count > 0)
            {
                var summaryContext = new ShiningContext(shiningRoot, residentRoot, guardiansRoot, null);
                lines.Add("  • Полный зафиксированный набор карт:");
                foreach (var card in selectedCards)
                    lines.AddRange(BuildShiningBlessingCardInspectionLines(card, summaryContext, isSelected: true));
            }
            else if (selectedIds.Count > 0)
            {
                lines.Add("  • [dim]stored snapshot карт отсутствует или повреждён; показан только canonical id-набор.[/]");
            }
        }

        if (shiningRoot["halls"] is JsonArray halls && halls.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Залы Обители:[/] [dim](показаны все без сокращения)[/]");
            foreach (var hall in halls.OfType<JsonObject>())
            {
                var hallName = GetNodeString(hall["hallName"]) ?? GetNodeString(hall["hallId"]) ?? "?";
                var description = GetNodeString(hall["description"]) ?? string.Empty;
                var serviceTags = (hall["serviceTags"] as JsonArray)?.OfType<JsonValue>()
                    .Where(node => node.TryGetValue<string>(out _))
                    .Select(node => DescribeShiningHallServiceTag(node.GetValue<string>()))
                    .ToList() ?? new List<string>();
                var summarySuffix = serviceTags.Count > 0
                    ? $" [dim](службы: {Markup.Escape(string.Join(", ", serviceTags))})[/]"
                    : string.Empty;
                lines.Add($"  • {Markup.Escape(hallName)} — {Markup.Escape(string.IsNullOrWhiteSpace(description) ? "описание пока не заполнено" : description)}{summarySuffix}");
            }
        }

        if (shiningRoot["shiningPoliticalActors"] is JsonArray actors && actors.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Светозарные акторы:[/] [dim](показаны все без сокращения)[/]");
            foreach (var actor in actors.OfType<JsonObject>())
            {
                var actorName = GetNodeString(actor["displayName"]) ?? GetNodeString(actor["actorId"]) ?? "?";
                var summary = GetNodeString(actor["summary"]) ?? string.Empty;
                var status = DescribeShiningPoliticalStatus(GetNodeString(actor["politicalStatus"]));
                var factionName = ResolveShiningFactionLabel(shiningRoot, GetNodeString(actor["currentFactionId"]));
                var factionSuffix = !string.IsNullOrWhiteSpace(factionName) && factionName != "?"
                    ? $" [dim](сейчас: {Markup.Escape(factionName)})[/]"
                    : string.Empty;
                lines.Add($"  • {Markup.Escape(actorName)} — {Markup.Escape(string.IsNullOrWhiteSpace(summary) ? "без сводки" : summary)} [dim]({Markup.Escape(status)})[/]{factionSuffix}");
            }
        }

        if (shiningRoot["factions"] is JsonArray factions && factions.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Сильнейшие фракции:[/] [dim](показаны все без сокращения)[/]");
            foreach (var faction in factions.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["factionStrength"])))
            {
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                var strength = GetNodeInt(faction["factionStrength"]);
                var band = ShiningAbodeState.GetFactionStrengthBand(strength);
                var tradeTier = ShiningAbodeState.GetTradeTier(strength);
                var tradeStock = ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot);
                var tradeRarity = ShiningAbodeState.GetTradeRarityCeiling(strength);
                var serviceMultiplier = ShiningAbodeState.GetServiceMultiplier(strength);
                var leaderType = GetNodeString(faction["leadership"]?["headActorType"]) ?? "vacant";
                var leaderId = GetNodeString(faction["leadership"]?["headActorId"]) ?? "vacant";
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var memberCount = CountResidentsInFaction(residentRoot, factionId);
                var tradeState = tradeTier >= 1 ? "активна" : "спит";
                lines.Add($"  • {Markup.Escape(factionName)} — сила [white]{strength}[/] [dim]({Markup.Escape(band)})[/], участников {memberCount}, торговля {tradeState} [dim](уровень торговли {tradeTier}, витрина {tradeStock}, потолок {Markup.Escape(tradeRarity)})[/], услуги x{serviceMultiplier:0.00}, глава {Markup.Escape(BuildHeadActorLabel(leaderType, leaderId, residentRoot, guardiansRoot, shiningRoot))}");
            }

            var latestTradeReceipts = factions.OfType<JsonObject>()
                .SelectMany(faction =>
                {
                    var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                    return (faction["tradeInventoryReceipts"] as JsonArray)?.OfType<JsonObject>()
                        .Select(receipt => (FactionName: factionName, Faction: faction, Receipt: receipt))
                        ?? Enumerable.Empty<(string FactionName, JsonObject Faction, JsonObject Receipt)>();
                })
                .OrderByDescending(item => GetNodeInt(item.Receipt["resolvedAtTurn"]))
                .ToList();
            if (latestTradeReceipts.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Все исходы торговли:[/] [dim](без сокращения)[/]");
                foreach (var item in latestTradeReceipts)
                    lines.Add($"  • {Markup.Escape(BuildShiningTradeReceiptSummary(item.FactionName, item.Faction, item.Receipt))}");
            }
        }

        if (shiningRoot["coreActionReceipts"] is JsonArray receipts && receipts.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Все исходы Обители:[/] [dim](без сокращения)[/]");
            foreach (var receipt in receipts.OfType<JsonObject>()
                         .OrderByDescending(item => GetNodeInt(item["resolvedAtTurn"])))
                lines.Add($"  • {Markup.Escape(BuildShiningCoreReceiptSummary(receipt, shiningRoot))}");
        }

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Сияющая Обитель ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static string BuildShiningReturnCycleStatusLabel(JsonObject shiningRoot)
    {
        var currentReturnCycleId = GetNodeString(shiningRoot["gachaSystem"]?["currentReturnCycleId"]);
        return string.IsNullOrWhiteSpace(currentReturnCycleId)
            ? "цикл возвращения не синхронизирован"
            : "цикл возвращения синхронизирован";
    }

    private bool EnsureActiveShiningAbodeAvailable(string title)
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable(title))
            return false;

        if (_stateManager.CurrentState.IsInShiningAbode)
            return true;

        ShowEmptyPanel(title, "Эти действия доступны только в обычной активной Сияющей Обители.");
        return false;
    }

    private async Task ShowShiningCoreActionsAsync()
    {
        if (!EnsureActiveShiningAbodeAvailable("Сияющая Обитель"))
            return;

        while (true)
        {
            var context = await LoadShiningContextAsync();
            if (context == null)
                return;

            var feathers = await ReadInkFeathersBalance();
            Clear();
            Write(BuildShiningOverviewPanel(context.Root, context.ResidentRoot, context.GuardiansRoot));
            MarkupLine($"[dim]Перья: {feathers} • Искры Света: {GetNodeInt(context.Root["lightSparks"])}[/]");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Основные действия Сияющей Обители[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(BuildShiningActionChoices(context.Root)));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("ожидающее открытие", StringComparison.OrdinalIgnoreCase))
            {
                ShowPendingNativeFactionDiscoveryInspectionPanel(context.Root);
                continue;
            }

            if (choice.Contains("Запросить открытие", StringComparison.Ordinal))
                await HandleNativeFactionDiscoveryAsync(context, feathers);
            else if (choice.Contains("Инвестировать", StringComparison.Ordinal))
                await HandleShiningFactionInvestmentAsync(context);
            else if (choice.Contains("Завершить проект", StringComparison.Ordinal))
                await HandleShiningProjectCompletionAsync(context, feathers);
            else if (choice.Contains("Поддержать проект", StringComparison.Ordinal))
                await HandleProjectSupportMutationAsync(context, support: true);
            else if (choice.Contains("Снять поддержку", StringComparison.Ordinal))
                await HandleProjectSupportMutationAsync(context, support: false);
            else if (choice.Contains("историю", StringComparison.Ordinal))
                await HandleProjectRetirementAsync(context);
        }
    }

    private static IReadOnlyList<string> BuildShiningActionChoices(JsonObject shiningRoot)
    {
        var choices = new List<string>
        {
            "🔍 Запросить открытие нативной фракции"
        };
        if (shiningRoot["pendingNativeFactionDiscovery"] is JsonObject)
            choices.Add("🔎 Осмотреть ожидающее открытие нативной фракции");
        choices.AddRange(new[]
        {
            "📈 Инвестировать во фракцию",
            "🧩 Завершить проект",
            "🪄 Поддержать проект",
            "↩️ Снять поддержку проекта",
            "🕯️ Отправить проект в историю",
            "← Назад"
        });
        return choices;
    }

    private async Task HandleNativeFactionDiscoveryAsync(ShiningContext context, int feathers)
    {
        var discoveryCost = ShiningAbodeState.GetNativeDiscoveryCost();
        if (feathers < discoveryCost.Feathers)
        {
            MarkupLine($"[red]Недостаточно Перьев. Нужно {discoveryCost.Feathers}.[/]");
            WaitForKey();
            return;
        }

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            RadianceTierAtRequest = GetNodeInt(context.Root["radiance"]?["tier"]),
            QuotedCostFeathers = discoveryCost.Feathers,
            QuotedCostLightSparks = discoveryCost.LightSparks,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };
        var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningCoreActionRequestPreview(context, request))
            return;

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine("[green]Создан ожидающий запрос действия Обители: открытие нативной фракции. На принятом ходу нужно материализовать новую фракцию и записать подтверждение.[/]");
        WaitForKey();
    }

    private void ShowPendingNativeFactionDiscoveryInspectionPanel(JsonObject shiningRoot)
    {
        var pendingDiscovery = shiningRoot["pendingNativeFactionDiscovery"] as JsonObject;
        var lines = new List<string>
        {
            "[bold yellow]🔎 Ожидающее открытие нативной фракции[/]",
            ""
        };

        if (pendingDiscovery == null)
        {
            lines.Add("[dim]Ожидающего открытия сейчас нет.[/]");
        }
        else
        {
            lines.Add($"  Идентификатор запроса: [dim]{Markup.Escape(GetNodeString(pendingDiscovery["requestId"]) ?? "?")}[/]");
            lines.Add($"  Создан на ходу: [dim]{GetNodeInt(pendingDiscovery["createdAtTurn"])}[/]");
            lines.Add($"  Создан в UTC: [dim]{Markup.Escape(GetNodeString(pendingDiscovery["createdAtUtc"]) ?? "UTC не указан")}[/]");
            lines.Add($"  Уровень сияния при запросе: [white]{GetNodeInt(pendingDiscovery["radianceTierAtRequest"])}[/]");
            lines.Add($"  Стоимость: [white]{GetNodeInt(pendingDiscovery["costFeathers"])} Перьев[/] / [white]{GetNodeInt(pendingDiscovery["costLightSparks"])} Искр Света[/]");
            lines.Add($"  Действие: [white]{DescribeShiningCoreActionLabel(ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction)}[/]");
            lines.Add("  [dim]Этот pending payload ждёт канонического materialization новой фракции и точного closure receipt.[/]");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🔎 Ожидающее открытие ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task HandleShiningFactionInvestmentAsync(ShiningContext context)
    {
        var faction = PromptForFaction(context.Root, "Инвестиция во фракцию");
        if (faction == null)
            return;

        var cost = ShiningAbodeState.GetFactionInvestmentCost();
        var feathers = await ReadInkFeathersBalance();
        if (feathers < cost.Feathers)
        {
            MarkupLine($"[red]Недостаточно Перьев. Нужно {cost.Feathers}.[/]");
            WaitForKey();
            return;
        }

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            FactionId = GetNodeString(faction["factionId"]) ?? string.Empty,
            FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? string.Empty,
            QuotedCostFeathers = cost.Feathers,
            QuotedCostLightSparks = cost.LightSparks,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };
        var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningCoreActionRequestPreview(context, request))
            return;

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine("[green]Создан ожидающий запрос действия Обители: инвестиция во фракцию. На принятом ходу нужно применить каноническое усиление и записать подтверждение.[/]");
        WaitForKey();
    }

    private JsonObject? PromptForFaction(JsonObject shiningRoot, string title)
    {
        if (shiningRoot["factions"] is not JsonArray factions || factions.Count == 0)
        {
            MarkupLine("[yellow]В Сияющей Обители пока нет материализованных фракций.[/]");
            WaitForKey();
            return null;
        }

        var options = factions.OfType<JsonObject>()
            .OrderByDescending(faction => GetNodeInt(faction["factionStrength"]))
            .Select(faction =>
            {
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
                var label = $"{factionName} [dim](сила {GetNodeInt(faction["factionStrength"])}";
                if (!string.IsNullOrWhiteSpace(factionId))
                    label += $" • идентификатор {factionId}";
                label += ")[/]";
                return (label, faction);
            })
            .ToList();

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(options.Select(item => item.label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        return options.First(item =>
            string.Equals(item.label, selected, StringComparison.Ordinal) ||
            item.label.Contains(selected, StringComparison.OrdinalIgnoreCase)).faction;
    }
}
