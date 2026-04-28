using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task HandleShiningProjectCompletionAsync(ShiningContext context, int feathers)
    {
        var faction = PromptForFaction(context.Root, "Завершение проекта");
        if (faction == null)
            return;

        var projectDraft = PromptForProjectDraft(context.Root, faction);
        if (projectDraft == null)
            return;

        if (!ShiningAbodeState.TryQuoteProjectCompletion(
                context.Root,
                context.ResidentRoot,
                GetNodeString(faction["factionId"]) ?? string.Empty,
                projectDraft,
                out var cost,
                out var quoteError))
        {
            MarkupLine($"[yellow]{Markup.Escape(quoteError ?? "Проект не прошёл проверку.")}[/]");
            WaitForKey();
            return;
        }

        if (feathers < cost.Feathers)
        {
            MarkupLine($"[red]Недостаточно Перьев. Нужно {cost.Feathers}.[/]");
            WaitForKey();
            return;
        }

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypeCompleteProject,
            FactionId = GetNodeString(faction["factionId"]) ?? string.Empty,
            FactionName = GetNodeString(faction["charter"]?["factionName"]) ?? string.Empty,
            ProjectDraft = projectDraft.DeepClone().AsObject(),
            ProjectDisplayName = GetNodeString(projectDraft["displayName"]) ?? string.Empty,
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

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine($"[green]Создан ожидающий запрос действия Обители: завершение проекта. На принятом ходу нужно материализовать завершённый проект и списать {cost.Feathers} / {cost.LightSparks}. Любимый архетип влияет только на эту цену; strengthReward проекта определяется только tier: 8/12/16.[/]");
        WaitForKey();
    }

    private async Task HandleProjectSupportMutationAsync(ShiningContext context, bool support)
    {
        var project = PromptForProject(context.Root, support ? "Поддержка проекта" : "Снятие поддержки", requireCompleted: true);
        if (project.Project == null)
            return;

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = support
                ? ShiningCoreActionRequestState.ActionTypeSupportProject
                : ShiningCoreActionRequestState.ActionTypeUnsupportProject,
            FactionId = project.FactionId,
            ProjectId = project.ProjectId,
            ProjectDisplayName = GetNodeString(project.Project?["displayName"]) ?? string.Empty,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };
        var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine(support
            ? "[green]Создан ожидающий запрос действия Обители: поддержка проекта. На принятом ходу нужно включить поддержку проекта и пометить черновик Врат устаревшим, если он открыт.[/]"
            : "[green]Создан ожидающий запрос действия Обители: снятие поддержки проекта. На принятом ходу нужно снять поддержку и пометить черновик Врат устаревшим, если он открыт.[/]");
        WaitForKey();
    }

    private async Task HandleProjectRetirementAsync(ShiningContext context)
    {
        var project = PromptForProject(context.Root, "Отправить проект в историю", requireCompleted: true);
        if (project.Project == null)
            return;

        var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            ActionType = ShiningCoreActionRequestState.ActionTypeRetireProject,
            FactionId = project.FactionId,
            ProjectId = project.ProjectId,
            ProjectDisplayName = GetNodeString(project.Project?["displayName"]) ?? string.Empty,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };
        var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
        MarkupLine("[green]Создан ожидающий запрос действия Обители: отправка проекта в историю. На принятом ходу нужно перенести проект в историю, пересчитать силу и записать подтверждение.[/]");
        WaitForKey();
    }

    private async Task ShowShiningGatesActionsAsync()
    {
        if (!EnsureActiveShiningAbodeAvailable("Врата Сияющей Обители"))
            return;

        while (true)
        {
            var context = await LoadShiningContextAsync();
            if (context == null)
                return;

            Clear();
            Write(BuildShiningOverviewPanel(context.Root, context.ResidentRoot, context.GuardiansRoot));

            var gates = context.Root["gates"] as JsonObject;
            var draftOpen = gates != null && GetNodeBool(gates["hasOpenDraft"]);
            var isStale = gates != null && GetNodeBool(gates["isStale"]);
            var choices = new List<string> { "🚪 Открыть Врата" };
            if (draftOpen || context.Root["preparedIncarnationPackage"] is JsonObject)
                choices.Add("🔎 Осмотреть набор и пакет");
            if (draftOpen)
            {
                choices.Add("🎴 Выбрать или снять благословение");
                choices.Add("🔁 Обновить набор благословений");
                choices.Add("🌱 Подготовить новую жизнь");
            }

            choices.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Врата Сияющей Обители[/] [dim]({(draftOpen ? "набор открыт" : "набор закрыт")}{(isStale ? ", устарел" : "")})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Открыть Врата", StringComparison.Ordinal))
            {
                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
                };
                var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
                    WaitForKey();
                    continue;
                }

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                MarkupLine("[green]Создан ожидающий запрос действия Обители: открытие Врат. На принятом ходу нужно материализовать зафиксированный набор и записать подтверждение.[/]");
                WaitForKey();
                continue;
            }

            if (choice.Contains("Осмотреть набор и пакет", StringComparison.OrdinalIgnoreCase))
            {
                ShowShiningGatesInspectionPanel(context);
                WaitForKey();
                continue;
            }

            if (choice.Contains("благословение", StringComparison.Ordinal))
            {
                if (!await EnsureNoPendingShiningCoreActionForLocalGatesMutationAsync("выбор благословения"))
                    continue;

                await HandleBlessingSelectionAsync(context.Root);
                continue;
            }

            if (choice.Contains("Обновить набор", StringComparison.Ordinal))
            {
                if (!await EnsureNoPendingShiningCoreActionForLocalGatesMutationAsync("обновление набора благословений"))
                    continue;

                if (!ShiningAbodeState.TryRerollGatesDraft(context.Root, out var error))
                {
                    MarkupLine($"[yellow]{Markup.Escape(error ?? "Набор благословений пока нельзя обновить.")}[/]");
                    WaitForKey();
                    continue;
                }

                await SaveShiningRootAsync(context.Root);
                MarkupLine("[green]Набор благословений обновлён.[/]");
                WaitForKey();
                continue;
            }

            if (choice.Contains("новую жизнь", StringComparison.Ordinal))
            {
                var selectedIds = (context.Root["gates"]?["selectedBlessingCardIds"] as JsonArray)?
                    .OfType<JsonValue>()
                    .Where(node => node.TryGetValue<string>(out _))
                    .Select(node => node.GetValue<string>())
                    .ToList() ?? new List<string>();
                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                    SourceDraftVersion = GetNodeInt(context.Root["gates"]?["draftVersion"]),
                    SelectedCardIds = selectedIds,
                    SelectedCards = BuildSelectedBlessingCardSnapshot(context.Root, selectedIds),
                    CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
                };
                var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
                    WaitForKey();
                    continue;
                }

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                MarkupLine("[green]Создан ожидающий запрос действия Обители: подготовка новой жизни. На принятом ходу нужно записать зафиксированный пакет и перевести runtime в режим стартовой передачи.[/]");
                WaitForKey();
                return;
            }
        }
    }

    private async Task<bool> EnsureNoPendingShiningCoreActionForLocalGatesMutationAsync(string actionLabel)
    {
        var pendingState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(_fs);
        if (pendingState.IsMalformed)
        {
            MarkupLine($"[yellow]Нельзя выполнить {Markup.Escape(actionLabel)}: pending_shining_abode_actions.json повреждён. Сначала исправьте или очистите pending core-action contract.[/]");
            WaitForKey();
            return false;
        }

        if (pendingState.Requests.Count > 0)
        {
            MarkupLine($"[yellow]Нельзя выполнить {Markup.Escape(actionLabel)}, пока существует pending Shining core action. Сначала дождитесь принятого хода ГМа или repair для pending_shining_abode_actions.json.[/]");
            MarkupLine($"[dim]Активный requestId: {Markup.Escape(pendingState.Requests[0].RequestId)}; actionType: {Markup.Escape(pendingState.Requests[0].ActionType)}[/]");
            WaitForKey();
            return false;
        }

        return true;
    }

    private async Task HandleBlessingSelectionAsync(JsonObject shiningRoot)
    {
        var gates = shiningRoot["gates"] as JsonObject;
        if (gates == null || !GetNodeBool(gates["hasOpenDraft"]))
        {
            MarkupLine("[yellow]Сначала открой Врата.[/]");
            WaitForKey();
            return;
        }

        if (GetNodeBool(gates["isStale"]))
        {
            MarkupLine("[yellow]Текущий черновик устарел. Открой Врата заново.[/]");
            WaitForKey();
            return;
        }

        var selectedIds = (gates["selectedBlessingCardIds"] as JsonArray)?
            .OfType<JsonValue>()
            .Where(node => node.TryGetValue<string>(out _))
            .Select(node => node.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var availableCards = (gates["availableBlessingCards"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        if (availableCards.Count == 0)
        {
            MarkupLine("[yellow]В текущем черновике нет доступных карт.[/]");
            WaitForKey();
            return;
        }

        var entries = availableCards.Select(card =>
        {
            var cardId = GetNodeString(card["cardId"]) ?? string.Empty;
            var name = GetNodeString(card["displayName"]) ?? cardId;
            var summary = GetNodeString(card["displaySummary"]) ?? string.Empty;
            var marker = selectedIds.Contains(cardId) ? "[green]✓[/]" : "[dim]•[/]";
            return ($"{marker} {name} [dim]({GetNodeString(card["rarity"])}, {GetNodeString(card["effectFamily"])})[/] [grey]{summary}[/]", cardId);
        }).ToList();

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Текущее благословение[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(entries.Select(item => item.Item1).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return;

        var cardId = entries.First(item => item.Item1 == selected).cardId;
        var toggledOff = selectedIds.Contains(cardId);
        var success = toggledOff
            ? ShiningAbodeState.TryDeselectBlessingCard(shiningRoot, cardId, out var error)
            : ShiningAbodeState.TrySelectBlessingCard(shiningRoot, cardId, out error);
        if (!success)
        {
            MarkupLine($"[yellow]{Markup.Escape(error ?? "Не удалось обновить выбор благословения.")}[/]");
            WaitForKey();
            return;
        }

        await SaveShiningRootAsync(shiningRoot);
        MarkupLine(toggledOff
            ? "[green]Благословение снято с выбора.[/]"
            : "[green]Благословение выбрано.[/]");
        WaitForKey();
    }

    private void ShowShiningGatesInspectionPanel(ShiningContext context)
    {
        var radianceTier = GetNodeInt(context.Root["radiance"]?["tier"]);
        var pickCap = ShiningAbodeState.GetPickCap(radianceTier);
        var draftSize = ShiningAbodeState.GetDraftSize(radianceTier);
        var lines = new List<string>
        {
            "[bold yellow]🔎 Полный осмотр Врат и пакета[/]",
            "",
            "[bold]Текущий набор Врат:[/]"
        };

        if (context.Root["gates"] is JsonObject gates && GetNodeBool(gates["hasOpenDraft"]))
        {
            var selectedIds = (gates["selectedBlessingCardIds"] as JsonArray)?.OfType<JsonValue>()
                .Where(node => node.TryGetValue<string>(out _))
                .Select(node => node.GetValue<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shownIds = (gates["shownBlessingCardIds"] as JsonArray)?.OfType<JsonValue>()
                .Where(node => node.TryGetValue<string>(out _))
                .Select(node => node.GetValue<string>())
                .ToList() ?? new List<string>();
            var availableCards = (gates["availableBlessingCards"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
            var shownCards = shownIds
                .Select(id => FindBlessingCardInGates(gates, id))
                .Where(card => card != null)
                .Cast<JsonObject>()
                .GroupBy(card => GetNodeString(card["cardId"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => group.First())
                .ToList();
            if (shownCards.Count == 0)
                shownCards = availableCards;

            lines.Add($"  • Версия черновика: [white]{GetNodeInt(gates["draftVersion"])}[/]");
            lines.Add($"  • Лимит выбора: [white]{pickCap}[/]");
            lines.Add($"  • Размер набора по сиянию: [white]{draftSize}[/]");
            var shownLabels = shownIds
                .Select(id => ResolveShiningBlessingCardLabel(context.Root, id))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();
            var selectedLabels = selectedIds
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Select(id => ResolveShiningBlessingCardLabel(context.Root, id))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();
            lines.Add($"  • Показанные карты: {Markup.Escape(shownLabels.Count == 0 ? "нет" : string.Join(", ", shownLabels))}");
            lines.Add($"  • Выбранные карты: {Markup.Escape(selectedLabels.Count == 0 ? "нет" : string.Join(", ", selectedLabels))}");
            lines.Add($"  • Перебросов осталось: [white]{GetNodeInt(gates["rerollsRemaining"])}[/]");
            lines.Add($"  • Следующий индекс набора: [white]{GetNodeInt(gates["nextCandidateCursor"])}[/]");
            lines.Add($"  • Состояние черновика: {Markup.Escape(GetNodeBool(gates["isStale"]) ? "устарел" : "актуален")}");

            if (shownCards.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Карты, показанные Вратами:[/]");
                foreach (var card in shownCards)
                {
                    var cardId = GetNodeString(card["cardId"]);
                    var normalizedCardId = cardId ?? string.Empty;
                    lines.AddRange(BuildShiningBlessingCardInspectionLines(card, context, selectedIds.Contains(normalizedCardId)));
                    if (!availableCards.Any(available => string.Equals(GetNodeString(available["cardId"]), normalizedCardId, StringComparison.OrdinalIgnoreCase)))
                        lines.Add("    [dim]Эта карта уже не входит в текущий набор выбора, но остаётся в истории показанных кандидатов Врат.[/]");
                }
            }
        }
        else
        {
            lines.Add("  • Врата сейчас закрыты: текущий набор ещё не материализован.");
        }

        lines.Add("");
        lines.Add("[bold khaki1]Подготовленный пакет новой жизни:[/]");
        if (context.Root["preparedIncarnationPackage"] is JsonObject package)
        {
            var selectedIds = GetPreparedPackageSelectedCardIds(package);
            var selectedCards = GetConsistentPreparedPackageCards(package);
            lines.Add($"  • Основан на версии Врат: [white]{GetNodeInt(package["generatedFromDraftVersion"])}[/]");
            lines.Add($"  • Лимит выбора: [white]{pickCap}[/]");
            lines.Add($"  • Размер исходного набора: [white]{draftSize}[/]");
            lines.Add($"  • Выбрано карт: [white]{selectedIds.Count}[/]");
            var selectedPackageLabels = selectedIds
                .Select(id => ResolveShiningBlessingCardLabel(context.Root, id))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();
            lines.Add($"  • Зафиксированные карты: {Markup.Escape(selectedPackageLabels.Count == 0 ? "нет" : string.Join(", ", selectedPackageLabels))}");
            lines.Add($"  • Зафиксирован на ходу: [white]{GetNodeInt(package["preparedAtTurn"])}[/]");
            lines.Add($"  • Зафиксирован в UTC: {Markup.Escape(GetNodeString(package["preparedAtUtc"]) ?? "не указан")}");

            if (selectedCards.Count > 0)
            {
                lines.Add("");
                lines.Add("[bold]Карты подготовленного пакета:[/]");
                foreach (var card in selectedCards)
                    lines.AddRange(BuildShiningBlessingCardInspectionLines(card, context, isSelected: true));
            }
            else if (selectedIds.Count > 0)
            {
                lines.Add("  • [dim]stored snapshot карт отсутствует или повреждён; доступен только canonical id-набор.[/]");
            }
        }
        else
        {
            lines.Add("  • Пакет новой жизни пока не подготовлен.");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🔎 Врата и пакет ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (context.Root["gates"] is JsonObject gatesAudit)
            WriteJsonAuditPanel("Полный canonical JSON Врат", gatesAudit, Color.Gold1);

        if (context.Root["preparedIncarnationPackage"] is JsonObject packageAudit)
            WriteJsonAuditPanel("Полный frozen JSON preparedIncarnationPackage", packageAudit, Color.Khaki1);
    }

    private IEnumerable<string> BuildShiningBlessingCardInspectionLines(JsonObject card, ShiningContext context, bool isSelected)
    {
        var cardId = GetNodeString(card["cardId"]) ?? string.Empty;
        var name = GetNodeString(card["displayName"]) ?? cardId;
        var summary = GetNodeString(card["displaySummary"]) ?? string.Empty;
        var effectFamily = DescribeShiningEffectFamily(GetNodeString(card["effectFamily"]));
        var rarity = GetNodeString(card["rarity"]) ?? "?";
        var rarityColor = GetRarityColor(rarity);
        var marker = isSelected ? "[green]✓[/]" : "[dim]•[/]";
        var lines = new List<string>
        {
            $"{marker} {Markup.Escape(name)} [{rarityColor}]{Markup.Escape(DescribeRarityLabel(rarity))}[/] [dim]({Markup.Escape(effectFamily)})[/]",
            $"    Идентификатор карты: [dim]{Markup.Escape(cardId)}[/]",
            $"    Источник: {Markup.Escape(BuildShiningBlessingSourceLabel(card, context))}"
        };

        if (!string.IsNullOrWhiteSpace(summary))
            lines.Add($"    Сводка: {Markup.Escape(summary)}");

        var effectDetails = BuildShiningBlessingEffectDetailLines(card);
        if (effectDetails.Count > 0)
        {
            lines.Add("    Эффект:");
            foreach (var effectLine in effectDetails)
                lines.Add($"      {Markup.Escape(effectLine)}");
        }

        return lines;
    }

    private static JsonObject? FindBlessingCardInGates(JsonObject gates, string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        foreach (var arrayName in new[] { "availableBlessingCards", "allCandidateBlessingCards" })
        {
            if (gates[arrayName] is not JsonArray cards)
                continue;

            var card = cards.OfType<JsonObject>()
                .FirstOrDefault(entry => string.Equals(GetNodeString(entry["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
            if (card != null)
                return card;
        }

        return null;
    }

    private static IEnumerable<string> FormatShiningJsonNodeForDisplay(JsonNode? node)
    {
        var json = (node ?? new JsonObject()).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        return json.Split('\n', StringSplitOptions.None)
            .Select(line => Markup.Escape(line.TrimEnd('\r')));
    }

    private static List<string> BuildShiningBlessingEffectDetailLines(JsonObject card)
    {
        var payload = card["effectPayload"] as JsonObject;
        if (payload == null)
            return new List<string>();

        var effectFamily = (GetNodeString(card["effectFamily"]) ?? string.Empty).Trim().ToLowerInvariant();
        var lines = new List<string>();
        switch (effectFamily)
        {
            case "lore":
                if (GetNodeInt(payload["clueCount"]) > 0)
                    lines.Add($"Количество сюжетных подсказок: {GetNodeInt(payload["clueCount"])}");
                if (GetNodeInt(payload["latestTurn"]) > 0)
                    lines.Add($"Не позже хода: {GetNodeInt(payload["latestTurn"])}");
                break;
            case "social":
                var socialDelta = GetNodeInt(payload["delta"]);
                if (socialDelta <= 0)
                    socialDelta = GetNodeInt(payload["relationshipBoost"]);
                if (socialDelta > 0)
                    lines.Add($"Первый союзник стартует ближе на: +{socialDelta}");
                break;
            case "resource":
                if (GetNodeInt(payload["money"]) > 0)
                    lines.Add($"Стартовые деньги: +{GetNodeInt(payload["money"])}");
                if (GetNodeInt(payload["common"]) > 0)
                    lines.Add($"Обычных ресурсов: {GetNodeInt(payload["common"])}");
                if (GetNodeInt(payload["uncommon"]) > 0)
                    lines.Add($"Необычных ресурсов: {GetNodeInt(payload["uncommon"])}");
                break;
            case "memory":
                if (GetNodeInt(payload["options"]) > 0)
                    lines.Add($"Дополнительных вариантов памяти: {GetNodeInt(payload["options"])}");
                if (GetNodeInt(payload["rerolls"]) > 0)
                    lines.Add($"Перебросов памяти: {GetNodeInt(payload["rerolls"])}");
                break;
            case "descent":
                if (GetNodeInt(payload["latestTurn"]) > 0)
                    lines.Add($"Спутник должен проявиться не позже хода: {GetNodeInt(payload["latestTurn"])}");
                if (GetNodeInt(payload["quality"]) > 0)
                    lines.Add($"Бонус качества проявления: +{GetNodeInt(payload["quality"])}");
                break;
            case "survival":
                if (GetNodeInt(payload["downgrade"]) > 0)
                    lines.Add($"Ослаблений рокового провала: {GetNodeInt(payload["downgrade"])}");
                if (GetNodeInt(payload["recovery"]) > 0)
                    lines.Add($"Восстановление потерь: {GetNodeInt(payload["recovery"])}%");
                break;
            case "relic":
                if (GetNodeInt(payload["rerolls"]) > 0)
                    lines.Add($"Перебросов реликвии: {GetNodeInt(payload["rerolls"])}");
                lines.Add($"Бесплатная смена формы: {(GetNodeBool(payload["freeShape"]) ? "да" : "нет")}");
                lines.Add($"Бесплатная перенастройка: {(GetNodeBool(payload["freeRetune"]) ? "да" : "нет")}");
                break;
            case "route":
                if (GetNodeInt(payload["routeOptions"]) > 0)
                    lines.Add($"Ранних вариантов пути: {GetNodeInt(payload["routeOptions"])}");
                if (GetNodeInt(payload["latestTurn"]) > 0)
                    lines.Add($"Не позже хода: {GetNodeInt(payload["latestTurn"])}");
                if (GetNodeInt(payload["remainingUses"]) > 0)
                    lines.Add($"Осталось использований: {GetNodeInt(payload["remainingUses"])}");
                break;
        }

        return lines;
    }

    private static string BuildShiningBlessingSourceLabel(JsonObject card, ShiningContext context)
    {
        var sourceType = (GetNodeString(card["sourceType"]) ?? string.Empty).Trim().ToLowerInvariant();
        var sourceFactionName = GetNodeString(card["sourceFactionName"]) ??
                                GetNodeString(card["sourceFactionId"]) ??
                                ResolveShiningFactionLabel(context.Root, GetNodeString(card["sourceFactionId"]));
        var sourceActorName = GetNodeString(card["sourceActorName"]);
        var sourceActorId = GetNodeString(card["sourceActorId"]) ?? string.Empty;
        var sourceActorLabel = string.IsNullOrWhiteSpace(sourceActorName)
            ? ResolveShiningBlessingSourceActorLabel(card, context)
            : sourceActorName;

        return sourceType switch
        {
            "head" => string.IsNullOrWhiteSpace(sourceActorLabel)
                ? $"глава фракции «{sourceFactionName}»"
                : $"глава фракции «{sourceFactionName}» — {sourceActorLabel}",
            "project" => string.IsNullOrWhiteSpace(sourceActorLabel)
                ? $"проект фракции «{sourceFactionName}» [источник сейчас недоступен]"
                : $"проект «{sourceActorLabel}»",
            "resident_descent" => string.IsNullOrWhiteSpace(sourceActorLabel)
                ? $"нисхождение резидента фракции «{sourceFactionName}» [источник сейчас недоступен]"
                : $"нисхождение резидента {sourceActorLabel}",
            _ => string.IsNullOrWhiteSpace(sourceActorLabel) ? sourceFactionName : $"{sourceFactionName} / {sourceActorLabel}"
        };
    }

    private static string ResolveShiningBlessingSourceActorLabel(JsonObject card, ShiningContext context)
    {
        var sourceType = (GetNodeString(card["sourceType"]) ?? string.Empty).Trim().ToLowerInvariant();
        var sourceActorId = GetNodeString(card["sourceActorId"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceActorId))
            return string.Empty;

        return sourceType switch
        {
            "project" => ResolveShiningBlessingProjectLabel(context.Root, sourceActorId),
            "resident_descent" => ResolveShiningBlessingResidentLabel(context.ResidentRoot, sourceActorId),
            _ => sourceActorId
        };
    }

    private static string ResolveShiningBlessingProjectLabel(JsonObject shiningRoot, string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || shiningRoot["factions"] is not JsonArray factions)
            return string.Empty;

        foreach (var faction in factions.OfType<JsonObject>())
        {
            if (faction["projects"] is not JsonArray projects)
                continue;

            foreach (var project in projects.OfType<JsonObject>())
            {
                if (!string.Equals(GetNodeString(project["projectId"]), projectId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return GetNodeString(project["displayName"]) ?? projectId;
            }
        }

        return string.Empty;
    }

    private static string ResolveShiningBlessingResidentLabel(JsonObject? residentRoot, string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId) || residentRoot?["entries"] is not JsonArray residents)
            return string.Empty;

        foreach (var resident in residents.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(resident["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            return GetNodeString(resident["displayName"]) ?? residentId;
        }

        return string.Empty;
    }

    private static JsonArray BuildSelectedBlessingCardSnapshot(JsonObject shiningRoot, IReadOnlyList<string> selectedIds)
    {
        var snapshot = new JsonArray();
        if (selectedIds.Count == 0 ||
            shiningRoot["gates"] is not JsonObject gates)
        {
            return snapshot;
        }

        var availableCards = gates["availableBlessingCards"] as JsonArray;
        var allCandidateCards = gates["allCandidateBlessingCards"] as JsonArray;
        foreach (var selectedId in selectedIds)
        {
            var card = FindBlessingCardById(availableCards, selectedId) ??
                       FindBlessingCardById(allCandidateCards, selectedId);
            if (card != null)
                snapshot.Add(card.DeepClone());
        }

        return snapshot;
    }

    private static JsonObject? FindBlessingCardById(JsonArray? cards, string cardId)
    {
        if (cards == null || string.IsNullOrWhiteSpace(cardId))
            return null;

        return cards.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["cardId"]), cardId, StringComparison.OrdinalIgnoreCase));
    }

    private JsonObject? PromptForProjectDraft(JsonObject shiningRoot, JsonObject faction)
    {
        var displayName = Ask("[cyan]Имя проекта[/]");
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        var summary = Ask("[cyan]Краткое описание проекта[/]");
        if (string.IsNullOrWhiteSpace(summary))
            return null;

        var toneTags = PromptForProjectToneTags();
        if (toneTags.Length == 0)
            return null;

        var archetypeChoices = new[]
        {
            (Value: ShiningAbodeState.ProjectArchetypeRevelation, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRevelation)),
            (Value: ShiningAbodeState.ProjectArchetypeAccord, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeAccord)),
            (Value: ShiningAbodeState.ProjectArchetypeProvision, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeProvision)),
            (Value: ShiningAbodeState.ProjectArchetypeRemembrance, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRemembrance)),
            (Value: ShiningAbodeState.ProjectArchetypeRefinement, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRefinement)),
            (Value: ShiningAbodeState.ProjectArchetypePassage, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypePassage)),
            (Value: ShiningAbodeState.ProjectArchetypeWarding, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeWarding)),
            (Value: ShiningAbodeState.ProjectArchetypeSubversion, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeSubversion))
        };
        var archetypeLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Архетип проекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(archetypeChoices.Select(choice => choice.Label)));
        var archetype = archetypeChoices.First(choice => choice.Label == archetypeLabel).Value;

        var compatibleFamilies = new[]
            {
                ShiningAbodeState.EffectFamilyLore,
                ShiningAbodeState.EffectFamilySocial,
                ShiningAbodeState.EffectFamilyResource,
                ShiningAbodeState.EffectFamilyMemory,
                ShiningAbodeState.EffectFamilyDescent,
                ShiningAbodeState.EffectFamilySurvival,
                ShiningAbodeState.EffectFamilyRelic,
                ShiningAbodeState.EffectFamilyRoute
            }
            .Where(family => ShiningAbodeState.IsProjectFamilyCompatible(archetype, family))
            .ToArray();

        var outputFamilyChoices = compatibleFamilies
            .Select(family => (Value: family, Label: DescribeShiningEffectFamily(family)))
            .ToList();
        var outputFamilyLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Семейство эффекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(outputFamilyChoices.Select(choice => choice.Label)));
        var outputFamily = outputFamilyChoices.First(choice => choice.Label == outputFamilyLabel).Value;

        var tierValue = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Уровень проекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("1", "2", "3"));

        var targetFactionIds = new JsonArray();
        if (string.Equals(archetype, ShiningAbodeState.ProjectArchetypeSubversion, StringComparison.OrdinalIgnoreCase))
        {
            var sourceFactionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            var targetFaction = PromptForFaction(shiningRoot, "Целевая фракция для подрыва");
            if (targetFaction == null)
                return null;
            var targetFactionId = GetNodeString(targetFaction["factionId"]) ?? string.Empty;
            if (string.Equals(targetFactionId, sourceFactionId, StringComparison.OrdinalIgnoreCase))
            {
                MarkupLine("[yellow]Подрыв не может быть направлен против той же самой фракции.[/]");
                WaitForKey();
                return null;
            }

            targetFactionIds.Add(targetFactionId);
        }

        var toneTagsArray = new JsonArray();
        foreach (var toneTag in toneTags)
            toneTagsArray.Add(toneTag);

        return new JsonObject
        {
            ["displayName"] = displayName,
            ["summary"] = summary,
            ["toneTags"] = toneTagsArray,
            ["targetFactionIds"] = targetFactionIds,
            ["projectArchetype"] = archetype,
            ["outputEffectFamily"] = outputFamily,
            ["tier"] = int.Parse(tierValue)
        };
    }

    private string[] PromptForProjectToneTags()
    {
        var toneChoices = new (string Value, string Label)[]
        {
            ("radiant", "Сияющий"),
            ("choral", "Хоровой"),
            ("solemn", "Торжественный"),
            ("tender", "Мягкий"),
            ("martial", "Воинственный"),
            ("scholarly", "Созерцательный"),
            ("pilgrim", "Страннический"),
            ("memorial", "Поминальный")
        };

        var primaryLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Главная тональность проекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(toneChoices.Select(choice => choice.Label)));
        var primary = toneChoices.First(choice => choice.Label == primaryLabel).Value;

        var secondaryLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Дополнительная тональность проекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(toneChoices.Where(choice => !string.Equals(choice.Value, primary, StringComparison.OrdinalIgnoreCase))
                .Select(choice => choice.Label)
                .Append("Без второй тональности")));

        var tags = new List<string> { primary };
        if (!secondaryLabel.Contains("Без", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(toneChoices.First(choice => choice.Label == secondaryLabel).Value);
        }

        return tags.ToArray();
    }

    private (JsonObject? Project, string FactionId, string ProjectId) PromptForProject(JsonObject shiningRoot, string title, bool requireCompleted)
    {
        if (shiningRoot["factions"] is not JsonArray factions || factions.Count == 0)
        {
            MarkupLine("[yellow]В Сияющей Обители пока нет фракций и проектов.[/]");
            WaitForKey();
            return (null, "", "");
        }

        var options = new List<(string Label, JsonObject Project, string FactionId, string ProjectId)>();
        foreach (var faction in factions.OfType<JsonObject>())
        {
            var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
            if (faction["projects"] is not JsonArray projects)
                continue;

            foreach (var project in projects.OfType<JsonObject>())
            {
                var status = GetNodeString(project["status"]) ?? "?";
                if (requireCompleted && !string.Equals(status, ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
                    continue;

                var projectId = GetNodeString(project["projectId"]) ?? string.Empty;
                var displayName = GetNodeString(project["displayName"]) ?? projectId;
                var supportLabel = GetNodeBool(project["isSupported"]) ? "поддержан" : "без поддержки";
                options.Add(($"{factionName} → {displayName} [dim]({DescribeShiningProjectStatus(status)}, {supportLabel})[/]", project, factionId, projectId));
            }
        }

        if (options.Count == 0)
        {
            MarkupLine("[yellow]Подходящих проектов не найдено.[/]");
            WaitForKey();
            return (null, "", "");
        }

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(options.Select(item => item.Label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return (null, "", "");

        var entry = options.First(item => item.Label == selected);
        return (entry.Project, entry.FactionId, entry.ProjectId);
    }
}
