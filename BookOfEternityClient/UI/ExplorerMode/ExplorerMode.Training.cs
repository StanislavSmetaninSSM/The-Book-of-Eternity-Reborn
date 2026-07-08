using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowTrainingAsync()
    {
        if (_trainingService == null)
        {
            MarkupLine("[red]❌ Сервис обучения недоступен.[/]");
            WaitForKey();
            return;
        }

        var view = await _trainingService.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync());
        if (ShouldDispatchTrainingPendingRequest(view))
        {
            MarkPendingInPlaceVitrineRequest(view.PendingGmAction!, "Подготовка витрины обучения");
            if (string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase))
                RenderAfterlifeTrainingOverview(view);
            else
                RenderMortalTrainingOverview(view);

            MarkupLine($"[yellow]⏳ {Markup.Escape(VitrinePreparationWaitingMessage)}.[/]");
            return;
        }
        if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
            _pendingGmAction = view.PendingGmAction;

        if (string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase))
            await ShowAfterlifeTrainingAsync(view);
        else
            await ShowMortalTrainingAsync(view);
    }

    private async Task ShowMortalTrainingAsync(TrainingService.TrainingView view)
    {
        if (view.Teachers.Count == 0)
        {
            ShowEmptyPanel("Обучение", "В текущих данных нет NPC, помеченных как учителя.");
            WaitForKey();
            return;
        }

        while (true)
        {
            RenderMortalTrainingOverview(view);

            var labels = view.Teachers
                .Select(BuildTeacherChoiceLabel)
                .Append("← Закрыть обучение")
                .ToArray();
            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]🎓 Выберите учителя[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(labels));

            if (selected.Contains("Закрыть", StringComparison.Ordinal))
                return;

            var index = Array.IndexOf(labels, selected);
            if (index < 0 || index >= view.Teachers.Count)
                return;

            var teacher = view.Teachers[index];
            if (!teacher.ShowcaseReady)
            {
                MarkupLine($"[yellow]⏳ {Markup.Escape(teacher.BlockReason ?? "Витрина обучения ещё не готова.")}[/]");
                if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                    MarkupLine("[dim]Запрос к ГМ уже создан и будет отправлен сейчас, без ожидания следующего обычного хода.[/]");
                WaitForKey();
                Clear();
                view = await _trainingService!.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync());
                if (ShouldDispatchTrainingPendingRequest(view))
                {
                    MarkPendingInPlaceVitrineRequest(view.PendingGmAction!, "Подготовка витрины обучения");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                    _pendingGmAction = view.PendingGmAction;
                continue;
            }

            await ShowTeacherTrainingOffersAsync(teacher);
            if (!string.IsNullOrWhiteSpace(_pendingGmAction))
                return;

            await _stateManager.RefreshGameStateAsync();
            Clear();
            view = await _trainingService!.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync(), createPendingRequests: false);
        }
    }

    private async Task ShowTeacherTrainingOffersAsync(TrainingService.TrainingTeacherView teacher)
    {
        while (true)
        {
            RenderTeacherTrainingPanel(teacher);
            if (teacher.Offers.Count == 0)
            {
                MarkupLine("[dim]У этого учителя пока нет предложений обучения.[/]");
                WaitForKey();
                return;
            }

            var labels = teacher.Offers
                .Select(BuildTrainingOfferChoiceLabel)
                .Append("← К учителям")
                .ToArray();
            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]🎓 {Markup.Escape(teacher.SourceActorName)}: предложения[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(labels));

            if (selected.Contains("К учителям", StringComparison.Ordinal))
                return;

            var index = Array.IndexOf(labels, selected);
            if (index < 0 || index >= teacher.Offers.Count)
                return;

            var offer = teacher.Offers[index];
            RenderTrainingOfferDetails(teacher, offer);
            if (!offer.Available)
            {
                WaitForKey();
                Clear();
                continue;
            }

            var confirm = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Подтвердить обучение?[/]")
                .AddChoices("Купить обучение", "← Отмена"));
            if (confirm.Contains("Отмена", StringComparison.Ordinal))
            {
                Clear();
                continue;
            }

            var result = await _trainingService!.BuyTrainingAsync(
                teacher.SourceActorId,
                offer.OfferId,
                await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
                ? $"[green]✓ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            if (!string.IsNullOrWhiteSpace(result.PendingGmAction))
            {
                _pendingGmAction = result.PendingGmAction;
                MarkupLine("[yellow]⏳ Запрос отправлен ГМ сейчас; дождитесь ответа мастера.[/]");
                return;
            }

            WaitForKey();
            return;
        }
    }

    private async Task ShowAfterlifeTrainingAsync(TrainingService.TrainingView view)
    {
        while (true)
        {
            RenderAfterlifeTrainingOverview(view);

            var labels = view.Teachers
                .Select(BuildTeacherChoiceLabel)
                .Append("◇ Самостоятельная прокачка души")
                .Append("← Закрыть обучение")
                .ToArray();
            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]🎓 Выберите наставника или способ обучения[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(labels));

            if (selected.Contains("Закрыть", StringComparison.Ordinal))
                return;

            if (selected.Contains("Самостоятельная прокачка", StringComparison.Ordinal))
            {
                await ShowAfterlifeSelfTrainingAsync(view);
                await _stateManager.RefreshGameStateAsync();
                Clear();
                view = await _trainingService!.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync(), createPendingRequests: false);
                continue;
            }

            var index = Array.IndexOf(labels, selected);
            if (index < 0 || index >= view.Teachers.Count)
                return;

            var teacher = view.Teachers[index];
            if (!teacher.ShowcaseReady)
            {
                MarkupLine($"[yellow]⏳ {Markup.Escape(teacher.BlockReason ?? "Витрина наставника ещё не готова.")}[/]");
                if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                    MarkupLine("[dim]Запрос к ГМ уже создан и будет отправлен сейчас, без ожидания следующего обычного хода.[/]");
                WaitForKey();
                Clear();
                view = await _trainingService!.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync());
                if (ShouldDispatchTrainingPendingRequest(view))
                {
                    MarkPendingInPlaceVitrineRequest(view.PendingGmAction!, "Подготовка витрины обучения");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                    _pendingGmAction = view.PendingGmAction;
                continue;
            }

            await ShowTeacherTrainingOffersAsync(teacher);
            if (!string.IsNullOrWhiteSpace(_pendingGmAction))
                return;

            await _stateManager.RefreshGameStateAsync();
            Clear();
            view = await _trainingService!.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync(), createPendingRequests: false);
        }
    }

    private async Task ShowAfterlifeSelfTrainingAsync(TrainingService.TrainingView view)
    {
        if (view.SelfTrainingOffers.Count == 0)
        {
            ShowEmptyPanel("Обучение души", "Нет доступных предложений самостоятельной прокачки.");
            WaitForKey();
            return;
        }

        while (true)
        {
            var labels = view.SelfTrainingOffers
                .Select(BuildTrainingOfferChoiceLabel)
                .Append("← К обучению души")
                .ToArray();
            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]🎓 Самостоятельная прокачка[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(labels));

            if (selected.Contains("К обучению души", StringComparison.Ordinal))
                return;

            var index = Array.IndexOf(labels, selected);
            if (index < 0 || index >= view.SelfTrainingOffers.Count)
                return;

            var offer = view.SelfTrainingOffers[index];
            RenderTrainingOfferDetails(null, offer);
            if (offer.Available)
            {
                var confirm = Prompt(new SelectionPrompt<string>()
                    .Title("[bold yellow]Подтвердить самостоятельную прокачку?[/]")
                    .AddChoices("Купить обучение", "← Отмена"));
                if (!confirm.Contains("Отмена", StringComparison.Ordinal))
                {
                    var result = await _trainingService!.BuyTrainingAsync("self", offer.OfferId, await TryReadCurrentTurnNumberAsync());
                    MarkupLine(result.Success
                        ? $"[green]✓ {Markup.Escape(result.Message)}[/]"
                        : $"[red]❌ {Markup.Escape(result.Message)}[/]");
                    WaitForKey();
                    view = await _trainingService.EnsureTrainingAsync(await TryReadCurrentTurnNumberAsync(), createPendingRequests: false);
                }
                else
                {
                    WaitForKey();
                }
            }
            else
            {
                WaitForKey();
            }

            Clear();
        }
    }

    private void RenderAfterlifeTrainingOverview(TrainingService.TrainingView view)
    {
        var ready = view.Teachers.Count(teacher => teacher.ShowcaseReady);
        var pending = view.Teachers.Count(teacher => !teacher.ShowcaseReady);
        var lines = new List<string>
        {
            "[bold yellow]🎓 Обучение души[/]",
            "Наставники дают более выгодные витрины духовных искусств, но не могут учить выше собственного уровня и выше открытого уровня вашей прогрессии.",
            "Самостоятельная прокачка доступна как дорогой запасной путь.",
            "",
            $"[dim]Наставников: {view.Teachers.Count} • готовых витрин: {ready} • ожидают ГМ: {pending} • самостоятельных предложений: {view.SelfTrainingOffers.Count}[/]"
        };
        if (view.RequestPending)
            lines.Add("[yellow]⏳ Есть ожидающий запрос к ГМ на подготовку или обновление витрины наставника.[/]");

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Обучение ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private static bool ShouldDispatchTrainingPendingRequest(TrainingService.TrainingView view) =>
        !string.IsNullOrWhiteSpace(view.PendingGmAction) &&
        view.RequestPending;

    private void RenderMortalTrainingOverview(TrainingService.TrainingView view)
    {
        var ready = view.Teachers.Count(teacher => teacher.ShowcaseReady);
        var pending = view.Teachers.Count(teacher => !teacher.ShowcaseReady);
        if (view.RequestPending && pending == 0)
            pending = 1;
        var lines = new List<string>
        {
            "[bold yellow]🎓 Обучение[/]",
            $"Учителей: [white]{view.Teachers.Count}[/] • готовых витрин: [green]{ready}[/] • ожидают ГМ: [yellow]{pending}[/]",
            "Покупка обучения в смертном мире списывает деньги и часть опыта текущего уровня. Клиент не допускает списание опыта в минус."
        };
        if (view.RequestPending)
            lines.Add("[yellow]⏳ Есть ожидающий запрос к ГМ на подготовку или обновление витрины обучения.[/]");

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Обучение ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private void RenderTeacherTrainingPanel(TrainingService.TrainingTeacherView teacher)
    {
        var available = teacher.Offers.Count(offer => offer.Available);
        var lines = new List<string>
        {
            $"[bold yellow]🎓 {Markup.Escape(teacher.SourceActorName)}[/]",
            $"[dim]Предложений: {teacher.Offers.Count} • доступно сейчас: {available}[/]"
        };
        if (!string.IsNullOrWhiteSpace(teacher.BlockReason))
            lines.Add($"[yellow]{Markup.Escape(teacher.BlockReason)}[/]");

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private void RenderTrainingOfferDetails(
        TrainingService.TrainingTeacherView? teacher,
        TrainingService.TrainingOffer offer)
    {
        var lines = new List<string>
        {
            $"[bold yellow]🎓 {Markup.Escape(offer.TargetName)}[/]",
            $"Тип: [white]{Markup.Escape(FormatTrainingTargetKind(offer.TargetKind))}[/]",
            $"Уровень: [cyan]{offer.CurrentValue}[/] → [cyan]{offer.TargetValue}[/] • предел источника: [cyan]{offer.SourceCap}[/]",
            $"Стоимость: {Markup.Escape(FormatTrainingCost(offer.Cost))}"
        };

        if (teacher != null)
            lines.Insert(1, $"Учитель: [white]{Markup.Escape(teacher.SourceActorName)}[/]");

        var summary = GetOfferDetailString(offer, "summary");
        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add("");
            lines.Add(Markup.Escape(summary));
        }

        if (!offer.Available)
        {
            lines.Add("");
            lines.Add($"[red]Недоступно: {Markup.Escape(offer.BlockReason ?? "причина не указана")}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Предложение обучения ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(offer.Available ? Color.Green3 : Color.Red),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private static string BuildTeacherChoiceLabel(TrainingService.TrainingTeacherView teacher)
    {
        var status = teacher.ShowcaseReady
            ? $"{teacher.Offers.Count(offer => offer.Available)}/{teacher.Offers.Count} доступно"
            : teacher.ShowcaseStale ? "витрина устарела" : "ожидает ГМ";
        return ConsoleLayout.PlainChoiceLabel($"🎓 {teacher.SourceActorName}", status);
    }

    private static string BuildTrainingOfferChoiceLabel(TrainingService.TrainingOffer offer)
    {
        var status = offer.Available
            ? $"{offer.CurrentValue}->{offer.TargetValue}; {FormatTrainingCost(offer.Cost)}"
            : $"закрыто: {offer.BlockReason}";
        return ConsoleLayout.PlainChoiceLabel($"• {offer.TargetName}", FormatTrainingTargetKind(offer.TargetKind), status);
    }

    private static string FormatTrainingCost(TrainingService.TrainingCost cost)
    {
        var parts = new List<string>();
        if (cost.Money > 0)
            parts.Add($"{cost.Money} денег");
        if (cost.CurrentLevelExperiencePoints > 0 || cost.CurrentLevelExperiencePercent > 0)
            parts.Add($"{cost.CurrentLevelExperiencePoints} опыта текущего уровня ({cost.CurrentLevelExperiencePercent}%)");
        if (cost.InkFeathers > 0)
            parts.Add($"{cost.InkFeathers} Чернильных Перьев");
        if (cost.LightSparks > 0)
            parts.Add($"{cost.LightSparks} Искр Света");
        return parts.Count == 0 ? "нет цены" : string.Join(", ", parts);
    }

    private static string FormatTrainingTargetKind(string kind) =>
        kind switch
        {
            "active_skill_mastery" => "активный навык: мастерство",
            "passive_skill_mastery" => "пассивный навык: мастерство",
            "active_skill_unlock" => "новый активный навык",
            "passive_skill_unlock" => "новый пассивный навык",
            "spiritual_art_self_training" => "духовное искусство: самостоятельная прокачка",
            "standard_spiritual_art" => "духовное искусство",
            "spiritual_art" => "духовное искусство",
            "spiritual_art_training" => "духовное искусство",
            "spirit_focus" => "Средоточие Души",
            "spirit_focus_training" => "Средоточие Души",
            "spirit_focus_self_training" => "Средоточие Души: самостоятельная прокачка",
            "special_spiritual_art" => "особое духовное искусство",
            "special_spiritual_art_training" => "особое духовное искусство",
            "special_spiritual_art_self_training" => "особое духовное искусство: самостоятельная прокачка",
            _ => kind
        };

    private static string GetOfferDetailString(TrainingService.TrainingOffer offer, string key)
    {
        if (offer.Details.TryGetPropertyValue(key, out var node) &&
            node is JsonValue value &&
            value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return string.Empty;
    }
}
