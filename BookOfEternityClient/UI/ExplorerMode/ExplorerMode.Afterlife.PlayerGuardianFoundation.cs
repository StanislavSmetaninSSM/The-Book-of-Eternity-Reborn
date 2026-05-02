using System.Text.Json.Nodes;
using Spectre.Console;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowPlayerGuardianFoundationAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Учредить собственного Хранителя"))
            return;

        var context = await PlayerGuardianFoundationState.ReadContextAsync(_fs);
        if (context.PendingRequest != null)
        {
            var pendingRequest = context.PendingRequest;
            var lines = new List<string>
            {
                "[bold gold1]👑 Ритуал уже подготовлен[/]",
                "",
                $"[bold]Новая мантия:[/] [white]{Markup.Escape(pendingRequest.ProposedDisplayName)}[/]",
                $"[bold]Сущность:[/] [dim]{Markup.Escape(pendingRequest.MantleSummary)}[/]",
                $"[bold]Кредо:[/] [dim]{Markup.Escape(pendingRequest.MantleCreed)}[/]",
                $"[bold]Мотивы:[/] [dim]{Markup.Escape(string.Join(", ", pendingRequest.AppearanceMotifs))}[/]",
                $"[bold]Прежний покровитель:[/] [white]{Markup.Escape(pendingRequest.PreviousGuardianName)}[/]",
                $"[bold]Идентификатор запроса:[/] [dim]{Markup.Escape(pendingRequest.RequestId)}[/]",
                $"[bold]Идентификатор прежнего покровителя:[/] [dim]{Markup.Escape(pendingRequest.PreviousGuardianId)}[/]",
                "",
                "[dim]Запрос на основание уже записан и ждёт следующего обычного хода GM в загробье.[/]",
                "[dim]Это одноразовая поздняя ветка: старые Хранители сохранятся, а новая мантия станет активным Хранителем по умолчанию.[/]",
                $"[dim]После основания мантия получит бонус основания: +{PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn} доп. попытка гачи за возвращение.[/]",
                "[dim]Новая Обитель не переносит старых обитателей автоматически, а начинает притягивать собственных через отдельную ветку заселения.[/]"
            };
            if (!string.IsNullOrWhiteSpace(pendingRequest.DominantAspect))
                lines.Add($"[bold]Доминирующий аспект:[/] [dim]{Markup.Escape(DescribeFoundationAspect(pendingRequest.DominantAspect))}[/]");

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 👑 Основание собственной мантии ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WriteJsonAuditPanel(
                $"Полный JSON {PlayerGuardianFoundationState.PendingRequestPath}",
                ToChaosSeaAuditNode(pendingRequest),
                Color.Gold1);

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices("✉️ Отправить ход GM", "← Назад"));
            if (action.StartsWith("✉️", StringComparison.Ordinal))
                _pendingGmAction = PlayerGuardianFoundationState.BuildPendingGmActionText(pendingRequest);
            return;
        }

        if (context.HasCompletedFoundation)
        {
            var foundedGuardianName = string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianName)
                ? "Основанный Хранитель"
                : context.ExistingFoundedGuardianName;
            var activeGuardianId = context.CurrentActiveGuardianIsFounded
                ? context.ExistingFoundedGuardianId
                : context.PreviousGuardianId;
            var lines = new List<string>
            {
                "[bold gold1]👑 Ветка основания уже завершена[/]",
                "",
                $"[bold]Статус:[/] [gold1]{Markup.Escape(DescribeFoundationStatus(string.IsNullOrWhiteSpace(context.FoundationStatus) ? PlayerGuardianFoundationState.SoulStateFoundationStatusFounded : context.FoundationStatus))}[/]",
                $"[bold]Основанный Хранитель:[/] [white]{Markup.Escape(foundedGuardianName)}[/]",
                $"[bold]Идентификатор основанного Хранителя:[/] [dim]{Markup.Escape(context.ExistingFoundedGuardianId)}[/]",
                $"[bold]Текущий активный Хранитель:[/] [white]{Markup.Escape(context.CurrentActiveGuardianIsFounded ? foundedGuardianName : context.PreviousGuardianName)}[/]",
                $"[bold]Идентификатор активного Хранителя:[/] [dim]{Markup.Escape(activeGuardianId)}[/]"
            };

            if (!string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianAbodeName))
                lines.Add($"[bold]Текущая Обитель:[/] [white]{Markup.Escape(context.ExistingFoundedGuardianAbodeName)}[/]");
            if (!string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianAbodeId))
                lines.Add($"[bold]Идентификатор текущей Обители:[/] [dim]{Markup.Escape(context.ExistingFoundedGuardianAbodeId)}[/]");
            if (!string.IsNullOrWhiteSpace(context.FoundationRequestId))
                lines.Add($"[bold]Идентификатор запроса основания:[/] [dim]{Markup.Escape(context.FoundationRequestId)}[/]");
            if (!string.IsNullOrWhiteSpace(context.FormerPatronGuardianName))
                lines.Add($"[bold]Прежний покровитель:[/] [white]{Markup.Escape(context.FormerPatronGuardianName)}[/] [dim]({DescribeFoundationGuardianRole(PlayerGuardianFoundationState.GuardianRoleFormerPatron)})[/]");
            if (!string.IsNullOrWhiteSpace(context.FormerPatronGuardianId))
                lines.Add($"[bold]Идентификатор прежнего покровителя:[/] [dim]{Markup.Escape(context.FormerPatronGuardianId)}[/]");
            if (context.FoundationResolvedAtTurn > 0)
                lines.Add($"[bold]Ход основания:[/] [white]{context.FoundationResolvedAtTurn}[/]");
            if (!string.IsNullOrWhiteSpace(context.FoundationResolvedAtUtc))
                lines.Add($"[bold]Закреплено:[/] [dim]{Markup.Escape(context.FoundationResolvedAtUtc)}[/]");
            if (context.ExistingFoundedGuardianExtraGachaChargesPerReturn > 0)
                lines.Add($"[bold]Бонус основания:[/] [white]+{context.ExistingFoundedGuardianExtraGachaChargesPerReturn} доп. попытка гачи за возвращение[/]");
            if (!string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianFeatureTitle))
                lines.Add($"[bold]Дар основания:[/] [white]{Markup.Escape(context.ExistingFoundedGuardianFeatureTitle)}[/]");
            if (!string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianFeatureSummary))
                lines.Add($"[dim]{Markup.Escape(context.ExistingFoundedGuardianFeatureSummary)}[/]");

            lines.Add("");
            lines.Add("[dim]Вы остаётесь душой игрока. Новый Хранитель основан из вашей вознесённой души и удерживает неразрывную преданность легендарного уровня.[/]");
            lines.Add("[dim]Это одноразовая ветка для этого сохранения. Старые Хранители не исчезают, но повторно этот путь в v1 недоступен.[/]");
            lines.Add("[dim]Основание не переносит автоматически состав обитателей, торговлю, кузню, политику Сияющей Обители или старые системы Хранителя в новую Обитель.[/]");
            if (!string.IsNullOrWhiteSpace(context.FormerPatronGuardianName))
                lines.Add($"[dim]Прежний покровитель {Markup.Escape(context.FormerPatronGuardianName)} может получить обычное продолжение от GM через разговоры, квесты, события и загробные уведомления.[/]");

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 👑 Основанный Хранитель ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            await WriteCompletedPlayerGuardianFoundationAuditPanelsAsync(context);
            WaitForKey();
            return;
        }

        if (!context.CanCreateRequest)
        {
            var lines = new List<string>
            {
                "[bold gold1]👑 Основание собственной мантии пока недоступно[/]",
                "",
                Markup.Escape(context.BlockingReason)
            };
            if (!string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianName))
            {
                lines.Add("");
                lines.Add($"[bold]Уже основанный Хранитель:[/] [white]{Markup.Escape(context.ExistingFoundedGuardianName)}[/]");
            }

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 👑 Основание собственной мантии ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WaitForKey();
            return;
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", new[]
        {
            "[bold gold1]👑 Учредить собственного Хранителя[/]",
            "",
            "Вы не становитесь обычным Хранителем напрямую.",
            "Но можете вынести из своей вознесённой души собственную Хранительскую мантию и закрепить её в Море Хаоса.",
            "",
            "[dim]Новая мантия станет вашим активным Хранителем по умолчанию, но прежние Хранители не исчезнут.[/]"
        })))
        {
            Header = new PanelHeader(" 👑 Ритуал учреждения ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var proposedDisplayName = PromptRequiredFoundationText("[gold1]Имя новой мантии:[/]", "Имя не может быть пустым.");
        var mantleSummary = PromptRequiredFoundationText("[gold1]Краткая сущность:[/] [dim](кто или что это за Хранитель)[/]", "Краткое определение сущности обязательно.");
        var mantleCreed = PromptRequiredFoundationText("[gold1]Кредо:[/] [dim](какой закон или волю несёт мантия)[/]", "Кредо обязательно.");
        var motifsRaw = PromptRequiredFoundationText("[gold1]Образные мотивы:[/] [dim](через запятую)[/]", "Нужен хотя бы один мотив.");
        var appearanceMotifs = motifsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(motif => !string.IsNullOrWhiteSpace(motif))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (appearanceMotifs.Count == 0)
        {
            ShowEmptyPanel("Учредить собственного Хранителя", "Нужен хотя бы один образный мотив.");
            return;
        }

        var dominantAspectChoice = Prompt(new SelectionPrompt<string>()
            .Title("[gold1]Доминирующий аспект:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(
                "Без доминирующего аспекта",
                "Память",
                "Кузня",
                "Знание",
                "Покровительство",
                "Власть",
                "Путь"));
        var dominantAspect = dominantAspectChoice switch
        {
            "Память" => "memory",
            "Кузня" => "forge",
            "Знание" => "knowledge",
            "Покровительство" => "patronage",
            "Власть" => "power",
            "Путь" => "path",
            _ => string.Empty
        };

        var request = new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            FounderSoulName = context.SoulName,
            PreviousGuardianId = context.PreviousGuardianId,
            PreviousGuardianName = context.PreviousGuardianName,
            SourceShiningAvailability = context.ShiningAvailability,
            ProposedDisplayName = proposedDisplayName,
            MantleSummary = mantleSummary,
            MantleCreed = mantleCreed,
            AppearanceMotifs = appearanceMotifs,
            DominantAspect = dominantAspect,
            CreatedAtTurn = Math.Max(0, _stateManager.CurrentState.TurnNumber)
        };

        var validationError = await PlayerGuardianFoundationState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            ShowEmptyPanel("Учредить собственного Хранителя", validationError);
            WaitForKey();
            return;
        }

        var confirmationLines = new List<string>
        {
            "[bold gold1]👑 Подтверждение ритуала[/]",
            "",
            "Будет создан [white]новый Хранитель[/], а не переписана ваша душа.",
            $"Новый Хранитель: [white]{Markup.Escape(proposedDisplayName)}[/]",
            $"Сущность: [dim]{Markup.Escape(mantleSummary)}[/]",
            $"Кредо: [dim]{Markup.Escape(mantleCreed)}[/]",
            $"Мотивы: [dim]{Markup.Escape(string.Join(", ", appearanceMotifs))}[/]",
            $"Прежний покровитель сохранится: [white]{Markup.Escape(context.PreviousGuardianName)}[/]",
            "[dim]Новая мантия станет вашим активным Хранителем по умолчанию.[/]",
            "[dim]Ветка основания доступна один раз на сохранение и не стирает вашу личность души игрока.[/]",
            $"[dim]После основания вы получите бонус основания: +{PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn} доп. попытка гачи за возвращение.[/]",
            "[dim]Новая Обитель будет притягивать собственных обитателей отдельной веткой заселения, а не автоматически забирать их у прежнего покровителя.[/]",
            "",
            "[bold]GM closure contract:[/]",
            "  • GM закрывает pending через UpdateGuardians.create, canonical guardians/activeGuardian и playerGuardianFoundationHistory.",
            "  • Старый Хранитель сохраняется как former_patron, новая мантия становится activeGuardian.",
            "  • Ритуал не переписывает душу игрока в Хранителя и не переносит жителей/торговлю/Сияющую политику автоматически.",
            "  • Accepted response должен материализовать новую Обитель и foundation bonus; refused/repair не должен удалять pending JSON."
        };
        if (!string.IsNullOrWhiteSpace(dominantAspect))
            confirmationLines.Add($"Аспект: [dim]{Markup.Escape(DescribeFoundationAspect(dominantAspect))}[/]");
        AppendChaosSeaPendingFileRule(confirmationLines, PlayerGuardianFoundationState.PendingRequestPath);
        AppendChaosSeaCommonContractRules(confirmationLines);

        if (!ConfirmChaosSeaContractPreview(
                "Полный pending contract основания Хранителя",
                confirmationLines,
                ToChaosSeaAuditNode(request),
                $"Полный JSON {PlayerGuardianFoundationState.PendingRequestPath}",
                confirmChoice: "✅ Учредить мантию"))
        {
            return;
        }

        await PlayerGuardianFoundationState.WriteAsync(_fs, request);
        _pendingGmAction = PlayerGuardianFoundationState.BuildPendingGmActionText(request);
        MarkupLine($"[gold1]👑 Ритуал учреждения мантии «{Markup.Escape(request.ProposedDisplayName)}» подготовлен. Следующий обычный ход в Море Хаоса отправит запрос GM.[/]");
    }

    private async Task WriteCompletedPlayerGuardianFoundationAuditPanelsAsync(PlayerGuardianFoundationState.FoundationContext context)
    {
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        var guardiansRoot = guardiansDoc == null
            ? null
            : JsonNode.Parse(guardiansDoc.RootElement.GetRawText()) as JsonObject;
        var soulRoot = soulDoc == null
            ? null
            : JsonNode.Parse(soulDoc.RootElement.GetRawText()) as JsonObject;
        var foundedGuardian = PlayerGuardianFoundationState.FindGuardianById(guardiansRoot, context.ExistingFoundedGuardianId) ??
                              PlayerGuardianFoundationState.FindPlayerFoundedGuardian(guardiansRoot);
        var historyEntry = PlayerGuardianFoundationState.FindHistoryEntryByGuardianId(guardiansRoot, context.ExistingFoundedGuardianId) ??
                           PlayerGuardianFoundationState.FindHistoryEntry(guardiansRoot, context.FoundationRequestId);

        WriteJsonAuditPanel(
            "Полный JSON завершенного основания Хранителя: history/founded/navigation",
            new JsonObject
            {
                ["foundationStatus"] = context.FoundationStatus,
                ["foundationHistoryEntry"] = historyEntry?.DeepClone(),
                ["foundedGuardian"] = foundedGuardian?.DeepClone(),
                ["activeGuardian"] = guardiansRoot?["activeGuardian"]?.DeepClone(),
                ["chaosSeaNavigation"] = guardiansRoot?["chaosSeaNavigation"]?.DeepClone(),
                ["soulFoundation"] = new JsonObject
                {
                    ["playerFoundedGuardianId"] = soulRoot?[PlayerGuardianFoundationState.SoulStateGuardianIdProperty]?.DeepClone(),
                    ["playerGuardianFoundationStatus"] = soulRoot?[PlayerGuardianFoundationState.SoulStateFoundationStatusProperty]?.DeepClone()
                }
            },
            Color.Gold1);
    }

    private string PromptRequiredFoundationText(string titleMarkup, string emptyError)
    {
        while (true)
        {
            var value = Prompt(new TextPrompt<string>(titleMarkup)
                .PromptStyle(new Style(Color.Gold1))
                .AllowEmpty());
            value = value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            MarkupLine($"[red]{Markup.Escape(emptyError)}[/]");
        }
    }

    private static string DescribeFoundationAspect(string aspect) => aspect.Trim().ToLowerInvariant() switch
    {
        "memory" => "память",
        "forge" => "кузня",
        "knowledge" => "знание",
        "patronage" => "покровительство",
        "power" => "власть",
        "path" => "путь",
        _ => aspect
    };

    private static string DescribeFoundationStatus(string status) => status.Trim().ToLowerInvariant() switch
    {
        "founded" => "основан",
        _ => status
    };

    private static string DescribeFoundationGuardianRole(string role) => role.Trim().ToLowerInvariant() switch
    {
        "former_patron" => "прежний покровитель",
        _ => role
    };

    private static string DescribeFoundationSource(string source) => source.Trim().ToLowerInvariant() switch
    {
        "shining_return" => "сияющее возвращение",
        _ => source
    };
}
