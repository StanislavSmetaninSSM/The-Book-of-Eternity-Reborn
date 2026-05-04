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

        if (shiningRoot == null)
            return null;

        var hasMalformedLegacyPendingDiscovery =
            !string.IsNullOrWhiteSpace(ShiningAbodeState.ValidateLegacyPendingNativeFactionDiscoveryShape(shiningRoot));

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

        if (!hasMalformedLegacyPendingDiscovery)
            ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        return new ShiningContext(shiningRoot, residentRoot, guardiansRoot, soulRoot);
    }

    private async Task<bool> SaveShiningRootAsync(JsonObject root)
    {
        if (!EnsureNoMalformedLegacyPendingDiscoveryForLocalShiningSave(root))
            return false;

        var liveShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (!string.IsNullOrWhiteSpace(liveShiningJson))
        {
            JsonObject? liveRoot;
            try
            {
                liveRoot = JsonNode.Parse(liveShiningJson) as JsonObject;
            }
            catch
            {
                MarkupLine($"[yellow]Нельзя локально сохранить {Markup.Escape(ShiningAbodeState.StatePath)}: live Shining state повреждён. Сначала выполните repair.[/]");
                return false;
            }

            if (liveRoot == null)
            {
                MarkupLine($"[yellow]Нельзя локально сохранить {Markup.Escape(ShiningAbodeState.StatePath)}: live Shining state не является JSON object. Сначала выполните repair.[/]");
                return false;
            }

            if (!EnsureNoMalformedLegacyPendingDiscoveryForLocalShiningSave(liveRoot))
                return false;
        }

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();
        return true;
    }

    private bool EnsureNoMalformedLegacyPendingDiscoveryForLocalShiningSave(JsonObject root)
    {
        var issue = ShiningAbodeState.ValidateLegacyPendingNativeFactionDiscoveryShape(root);
        if (string.IsNullOrWhiteSpace(issue))
            return true;

        MarkupLine($"[yellow]{Markup.Escape(issue)}[/]");
        MarkupLine($"[dim]• {Markup.Escape(ShiningAbodeState.StatePath)}.pendingNativeFactionDiscovery[/]");
        return false;
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
        else if (shiningRoot.ContainsKey("pendingNativeFactionDiscovery") &&
                 shiningRoot["pendingNativeFactionDiscovery"] is not null)
        {
            lines.Add("");
            lines.Add("[bold red]Repair-only blocker:[/]");
            lines.Add($"  • {Markup.Escape(ShiningAbodeState.StatePath)}.pendingNativeFactionDiscovery повреждён; local saves/actions blocked until repair or closure.");
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
                var hallId = GetNodeString(hall["hallId"]) ?? string.Empty;
                var hallIdSuffix = string.IsNullOrWhiteSpace(hallId)
                    ? string.Empty
                    : $" [dim](hallId={Markup.Escape(hallId)})[/]";
                lines.Add($"  • {Markup.Escape(hallName)}{hallIdSuffix} — {Markup.Escape(string.IsNullOrWhiteSpace(description) ? "описание пока не заполнено" : description)}{summarySuffix}");
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
                var actorId = GetNodeString(actor["actorId"]) ?? string.Empty;
                var currentFactionId = GetNodeString(actor["currentFactionId"]) ?? string.Empty;
                var actorIdSuffix = string.IsNullOrWhiteSpace(actorId)
                    ? string.Empty
                    : $" [dim](actorId={Markup.Escape(actorId)})[/]";
                var factionName = ResolveShiningFactionLabel(shiningRoot, currentFactionId);
                var factionSuffix = !string.IsNullOrWhiteSpace(factionName) && factionName != "?"
                    ? $" [dim](сейчас: {Markup.Escape(factionName)}; currentFactionId={Markup.Escape(string.IsNullOrWhiteSpace(currentFactionId) ? "none" : currentFactionId)})[/]"
                    : string.Empty;
                lines.Add($"  • {Markup.Escape(actorName)}{actorIdSuffix} — {Markup.Escape(string.IsNullOrWhiteSpace(summary) ? "без сводки" : summary)} [dim]({Markup.Escape(status)})[/]{factionSuffix}");
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
                var hallId = GetNodeString(faction["hallId"]) ?? string.Empty;
                var idSuffix = string.IsNullOrWhiteSpace(hallId)
                    ? $"factionId={Markup.Escape(factionId)}"
                    : $"factionId={Markup.Escape(factionId)}, hallId={Markup.Escape(hallId)}";
                lines.Add($"  • {Markup.Escape(factionName)} [dim]({idSuffix})[/] — сила [white]{strength}[/] [dim]({Markup.Escape(band)})[/], участников {memberCount}, торговля {tradeState} [dim](уровень торговли {tradeTier}, витрина {tradeStock}, потолок {Markup.Escape(tradeRarity)})[/], услуги x{serviceMultiplier:0.00}, глава {Markup.Escape(BuildHeadActorLabel(leaderType, leaderId, residentRoot, guardiansRoot, shiningRoot))}");
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
            ? "currentReturnCycleId не синхронизирован"
            : $"currentReturnCycleId={currentReturnCycleId}";
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
            var coreRequests = await ShiningCoreActionRequestState.ReadRequestsAsync(_fs);
            Clear();
            Write(BuildShiningOverviewPanel(context.Root, context.ResidentRoot, context.GuardiansRoot));
            MarkupLine($"[dim]Перья: {feathers} • Искры Света: {GetNodeInt(context.Root["lightSparks"])}[/]");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Основные действия Сияющей Обители[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(BuildShiningActionChoices(context, feathers, coreRequests)));

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

    private static IReadOnlyList<string> BuildShiningActionChoices(
        ShiningContext context,
        int feathers,
        IReadOnlyList<ShiningCoreActionRequestState.PendingShiningCoreActionRequest> coreRequests)
    {
        var shiningRoot = context.Root;
        var lightSparks = GetNodeInt(shiningRoot["lightSparks"]);
        var pendingBlocker = coreRequests.Count > 0 ? $"pending core action {coreRequests[0].RequestId}" : null;
        var discoveryCost = ShiningAbodeState.GetNativeDiscoveryCost();
        var radianceTier = GetNodeInt(shiningRoot["radiance"]?["tier"]);
        var choices = new List<string>
        {
            BuildShiningActionChoiceWithState(
                "🔍 Запросить открытие нативной фракции",
                pendingBlocker == null &&
                shiningRoot["pendingNativeFactionDiscovery"] is not JsonObject &&
                radianceTier >= 1 &&
                feathers >= discoveryCost.Feathers &&
                lightSparks >= discoveryCost.LightSparks,
                $"{discoveryCost.Feathers} Перьев / {discoveryCost.LightSparks} Искр",
                pendingBlocker ??
                (shiningRoot["pendingNativeFactionDiscovery"] is JsonObject pendingDiscovery
                    ? $"legacy pendingNativeFactionDiscovery {GetNodeString(pendingDiscovery["requestId"]) ?? "без requestId"}"
                    : radianceTier < 1
                        ? "нужен Radiance tier 1+"
                        : feathers < discoveryCost.Feathers
                            ? "не хватает Перьев"
                            : lightSparks < discoveryCost.LightSparks
                                ? "не хватает Искр Света"
                                : null))
        };
        if (shiningRoot["pendingNativeFactionDiscovery"] is JsonObject)
            choices.Add("🔎 Осмотреть ожидающее открытие нативной фракции");

        var factionCount = (shiningRoot["factions"] as JsonArray)?.OfType<JsonObject>().Count() ?? 0;
        var investCost = ShiningAbodeState.GetFactionInvestmentCost();
        var investEligibleCount = (shiningRoot["factions"] as JsonArray)?.OfType<JsonObject>()
            .Count(faction => GetNodeInt(faction["investCountThisAscension"]) < 3) ?? 0;
        var completedProjects = CountShiningProjects(shiningRoot, project =>
            string.Equals(GetNodeString(project["status"]), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase));
        var supportEligible = CountShiningProjects(shiningRoot, project =>
            string.Equals(GetNodeString(project["status"]), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
            !GetNodeBool(project["isSupported"]));
        var supportedProjects = CountShiningProjects(shiningRoot, project => GetNodeBool(project["isSupported"]));
        var supportedProjectCap = ShiningAbodeState.GetSupportedProjectCap(radianceTier);
        var supportCapAvailable = ShiningAbodeState.CountSupportedProjectsAcrossState(shiningRoot) < supportedProjectCap;

        choices.AddRange(new[]
        {
            BuildShiningActionChoiceWithState(
                "📈 Инвестировать во фракцию",
                pendingBlocker == null && factionCount > 0 && investEligibleCount > 0 && feathers >= investCost.Feathers && lightSparks >= investCost.LightSparks,
                $"{investCost.Feathers} Перьев / {investCost.LightSparks} Искр; eligible factions {investEligibleCount}",
                pendingBlocker ?? (factionCount == 0 ? "нет фракций" : investEligibleCount == 0 ? "лимит инвестиций исчерпан" : feathers < investCost.Feathers ? "не хватает Перьев" : lightSparks < investCost.LightSparks ? "не хватает Искр Света" : null)),
            BuildShiningActionChoiceWithState(
                "🧩 Завершить проект",
                pendingBlocker == null && factionCount > 0,
                "quote зависит от tier/projectDraft; preview покажет exact cost до записи pending",
                pendingBlocker ?? (factionCount == 0 ? "нет фракций" : null)),
            BuildShiningActionChoiceWithState(
                "🪄 Поддержать проект",
                pendingBlocker == null && supportEligible > 0 && supportCapAvailable,
                $"0 Перьев / 0 Искр; eligible completed projects {supportEligible}; support cap {supportedProjects}/{supportedProjectCap}",
                pendingBlocker ?? (supportEligible == 0 ? "нет completed unsupported projects" : !supportCapAvailable ? "лимит поддерживаемых проектов исчерпан" : null)),
            BuildShiningActionChoiceWithState(
                "↩️ Снять поддержку проекта",
                pendingBlocker == null && supportedProjects > 0,
                $"0 Перьев / 0 Искр; supported projects {supportedProjects}",
                pendingBlocker ?? (supportedProjects == 0 ? "нет supported projects" : null)),
            BuildShiningActionChoiceWithState(
                "🕯️ Отправить проект в историю",
                pendingBlocker == null && completedProjects > 0,
                $"0 Перьев / 0 Искр; completed projects {completedProjects}",
                pendingBlocker ?? (completedProjects == 0 ? "нет completed projects" : null)),
            "← Назад"
        });
        return choices;
    }

    private static string BuildShiningActionChoiceWithState(string label, bool enabled, string quote, string? disabledReason)
    {
        var state = enabled
            ? $"[green]доступно[/], {quote}"
            : $"[red]заблокировано[/]: {disabledReason ?? "условия не выполнены"}; {quote}";
        return $"{label} [dim]— {state}[/]";
    }

    private static int CountShiningProjects(JsonObject shiningRoot, Func<JsonObject, bool> predicate)
    {
        if (shiningRoot["factions"] is not JsonArray factions)
            return 0;

        return factions.OfType<JsonObject>()
            .SelectMany(faction => (faction["projects"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            .Count(predicate);
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
        MarkupLine(BuildShiningCorePostConfirmMarkup(
            request,
            "На принятом ходу нужно материализовать новую фракцию, новый hallId/factionId, resident/project arrays и coreActionReceipts[] с тем же requestId."));
        WaitForKey();
    }

    private void ShowPendingNativeFactionDiscoveryInspectionPanel(JsonObject shiningRoot)
    {
        var pendingNode = shiningRoot["pendingNativeFactionDiscovery"];
        var pendingDiscovery = pendingNode as JsonObject;
        var lines = new List<string>
        {
            "[bold yellow]🔎 Ожидающее открытие нативной фракции[/]",
            ""
        };

        if (pendingNode == null)
        {
            lines.Add("[dim]Ожидающего открытия сейчас нет.[/]");
        }
        else if (pendingDiscovery == null)
        {
            lines.Add("[bold red]Repair-only blocker:[/]");
            lines.Add("  pendingNativeFactionDiscovery присутствует, но не является JSON object.");
            lines.Add("  Это malformed legacy discovery evidence: local Shining saves/actions blocked until repair; GM не должен silently null/erase this value.");
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
        WriteJsonAuditPanel(
            "Полный JSON shining_abode_state.pendingNativeFactionDiscovery",
            pendingNode,
            pendingDiscovery == null && pendingNode != null ? Color.Red : Color.Orange1);
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
        MarkupLine(BuildShiningCorePostConfirmMarkup(
            request,
            "На принятом ходу нужно применить каноническое усиление factionStrength/radiance и coreActionReceipts[] с тем же requestId."));
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
