using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowSarefStoryAsync()
    {
        if (IsSarefFindWingsSubcommand(_currentCommandRemainder))
        {
            await ShowSarefFindWingsAsync();
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(SarefMainStoryState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Скрытая нить",
                $"Состояние скрытой линии повреждено ({read.Error}). Сначала нужен repair состояния.");
            if (!string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel("Raw hidden main story state", JsonValue.Create(read.RawPayload), Color.Red);
            return;
        }

        var root = read.Root;
        if (root == null || IsSarefStoryStillUnknown(root))
        {
            ShowEmptyPanel("Скрытая нить", "Ты пока не знаешь, что искать.");
            return;
        }

        var lines = new List<string>
        {
            "[bold gold1]Крылья над Бездной[/]",
            "",
            $"Стадия раскрытия: [white]{Markup.Escape(DescribeSarefRevealStage(GetNodeString(root["revealStage"])))}[/]",
            $"Фрагментов: [white]{CountArray(root["sarefRevelations"])}[/]; преимуществ: [white]{CountArray(root["sarefAdvantages"])}[/]",
            ""
        };

        AppendSarefRevelations(lines, root["sarefRevelations"] as JsonArray);
        AppendSarefAdvantages(lines, root["sarefAdvantages"] as JsonArray);
        AppendSarefAdvantageUses(lines, root["sarefAdvantageUses"] as JsonArray);

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Крылья над Бездной ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel($"Полный JSON {SarefMainStoryState.StatePath}", root, Color.Gold1);
        WaitForKey();
    }

    private async Task ShowSarefFindWingsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Поиск Крыльев Ангелов"))
            return;

        await _stateManager.RefreshGameStateAsync();
        if (!_stateManager.CurrentState.IsInShiningAbode)
        {
            ShowEmptyPanel(
                "Поиск Крыльев Ангелов",
                "Поиск Крыльев доступен только в обычной активной Сияющей Обители. В Море Хаоса можно собирать фрагменты, но нельзя начать внедрение.");
            WaitForKey();
            return;
        }

        var context = await LoadShiningContextAsync();
        if (context == null)
        {
            ShowEmptyPanel(
                "Поиск Крыльев Ангелов",
                "Нужны читаемые game_state/meta/soul_state.json и game_state/meta/shining_abode_state.json.");
            WaitForKey();
            return;
        }

        var pending = await SarefMainStoryState.ReadWingsInfiltrationRequestStateAsync(_fs);
        if (pending.IsMalformed)
        {
            ShowEmptyPanel(
                "Поиск Крыльев Ангелов",
                $"{SarefMainStoryState.PendingWingsInfiltrationPath} повреждён: {pending.Error}. Исправьте pending-файл перед повторной попыткой.");
            WaitForKey();
            return;
        }

        if (pending.Request != null)
        {
            Write(BuildSarefWingsPendingPanel(pending.Request));
            WaitForKey();
            return;
        }

        var pendingBlocker = await TryDescribeSarefWingsPendingBlockerAsync(context.Root);
        if (pendingBlocker != null)
        {
            ShowEmptyPanel("Поиск Крыльев Ангелов", pendingBlocker);
            WaitForKey();
            return;
        }

        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(SarefMainStoryState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Поиск Крыльев Ангелов",
                $"Состояние скрытой линии повреждено ({read.Error}). Сначала нужен repair состояния.");
            WaitForKey();
            return;
        }

        var request = SarefMainStoryState.BuildWingsInfiltrationRequest(
            read.Root,
            Math.Max(1, _stateManager.CurrentState.TurnNumber + 1));
        if (request == null)
        {
            ShowEmptyPanel(
                "Поиск Крыльев Ангелов",
                "Ты пока не знаешь, что искать. Нужен маршрут: все четыре ключевых фрагмента Сарефа или достаточные замены по контракту.");
            WaitForKey();
            return;
        }

        Write(BuildSarefWingsAvailablePanel(request));
        WriteJsonAuditPanel("JSON pending_saref_wings_infiltration.json", request, Color.Gold1);

        if (!Confirm("[yellow]Начать поиск Крыльев Ангелов и отправить ожидающий контракт ГМ?[/]", false))
        {
            MarkupLine("[dim]Поиск Крыльев не начат; ожидающий запрос не создан.[/]");
            WaitForKey();
            return;
        }

        await SarefMainStoryState.WriteWingsInfiltrationRequestAsync(_fs, request);
        var requestId = GetNodeString(request["requestId"]) ?? "?";
        var routeSafety = GetNodeString(request["routeSafety"]) ?? "?";
        var entryMode = GetNodeString(request["entryMode"]) ?? "?";
        _pendingGmAction =
            $"[SAREF_WINGS_INFILTRATION: {requestId}] Душа начинает поиск входа в Крылья Ангелов.\n\n" +
            $"Закрой {SarefMainStoryState.PendingWingsInfiltrationPath} через sarefMainStoryUpdate.mode={SarefMainStoryState.WingsUpdateModeReveal}, " +
            $"{SarefMainStoryState.WingsUpdateModeRefuse} или {SarefMainStoryState.WingsUpdateModeBlock}. " +
            $"Маршрут: {routeSafety}, вход: {entryMode}. " +
            "Если routeSafety=risky/desperate, обязательно примени перечисленные disadvantages. " +
            "При reveal_wings запиши main_story_saref_state.revealStage=wings_revealed, wingsInfiltration.status=revealed, resolvedAtTurn и factionLinks.visibility=revealed. " +
            "Не оставляй pending-файл без accepted closure/repair.";

        MarkupLine("[green]Поиск Крыльев Ангелов начат: pending request создан и GM action подготовлен.[/]");
        WaitForKey();
    }

    private async Task<string?> TryDescribeSarefWingsPendingBlockerAsync(JsonObject shiningRoot)
    {
        if (_fs.FileExists("input/turn_request.json") ||
            _fs.FileExists("game_state/control/pending_turn_snapshot.json") ||
            HasAnyShiningTreasuryPendingTurnSnapshotFile())
        {
            return "Поиск Крыльев заблокирован: найден активный GM-turn lifecycle. Дождитесь завершения, отмены или repair текущего хода.";
        }

        var blocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(_fs, shiningRoot);
        return blocker == null
            ? null
            : $"Поиск Крыльев заблокирован: есть {blocker}.";
    }

    private static bool IsSarefFindWingsSubcommand(string? remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        var normalized = remainder.Trim().ToLowerInvariant().Replace('-', '_');
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized is "найти_крылья" or "найти крылья" or "find_wings" or "find wings";
    }

    private static Panel BuildSarefWingsAvailablePanel(JsonObject request)
    {
        var routeSafety = GetNodeString(request["routeSafety"]) ?? "?";
        var lines = new List<string>
        {
            "[bold gold1]Поиск Крыльев Ангелов[/]",
            "",
            $"Маршрут: [white]{Markup.Escape(DescribeSarefWingsRouteSafety(routeSafety))}[/]",
            $"Режим входа: [white]{Markup.Escape(GetNodeString(request["entryMode"]) ?? "?")}[/]",
            $"Фрагментов маршрута: [white]{CountArray(request["routeFragments"])}[/]; замен: [white]{CountArray(request["substituteFragments"])}[/]",
            $"Доступных преимуществ: [white]{CountArray(request["availableAdvantages"])}[/]",
            "",
            "[dim]После подтверждения клиент создаст pending-файл, а ГМ будет обязан закрыть его accepted closure или repair.[/]"
        };

        if (request["disadvantages"] is JsonArray { Count: > 0 } disadvantages)
        {
            lines.Add("");
            lines.Add("[yellow]Обязательные осложнения маршрута:[/]");
            foreach (var disadvantage in disadvantages)
            {
                var text = GetNodeString(disadvantage);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add($"  • {Markup.Escape(text)}");
            }
        }

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🪽 Поиск Крыльев Ангелов ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static Panel BuildSarefWingsPendingPanel(JsonObject request)
    {
        var lines = new List<string>
        {
            "[bold gold1]Поиск Крыльев Ангелов уже ожидает закрытия ГМ.[/]",
            "",
            $"  • requestId: [white]{Markup.Escape(GetNodeString(request["requestId"]) ?? "?")}[/]",
            $"  • routeSafety: [white]{Markup.Escape(GetNodeString(request["routeSafety"]) ?? "?")}[/]",
            $"  • entryMode: [white]{Markup.Escape(GetNodeString(request["entryMode"]) ?? "?")}[/]",
            $"  • response surface: [white]{Markup.Escape(SarefMainStoryState.ResponseField)}[/]",
            "",
            "[dim]Не создавайте второй запрос; дождитесь reveal_wings/refuse_wings/block_wings или repair pending-файла.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Поиск Крыльев ожидает закрытия ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static bool IsSarefStoryStillUnknown(JsonObject root)
    {
        var revealStage = GetNodeString(root["revealStage"]);
        var hasContent = CountArray(root["sarefRevelations"]) > 0 ||
                         CountArray(root["sarefAdvantages"]) > 0 ||
                         CountArray(root["sarefAdvantageUses"]) > 0;
        return !hasContent &&
               (string.IsNullOrWhiteSpace(revealStage) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendSarefRevelations(List<string> lines, JsonArray? revelations)
    {
        if (revelations == null || revelations.Count == 0)
        {
            lines.Add("[dim]Раскрытые фрагменты пока не записаны.[/]");
            lines.Add("");
            return;
        }

        lines.Add("[bold]Раскрытые фрагменты[/]");
        foreach (var revelation in revelations.OfType<JsonObject>())
        {
            var id = GetNodeString(revelation["revelationId"]) ?? "?";
            var category = GetNodeString(revelation["category"]) ?? "?";
            var summary = GetNodeString(revelation["summary"]);
            lines.Add($"  • [white]{Markup.Escape(id)}[/] — {Markup.Escape(DescribeSarefRevelationCategory(category))}");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
        }
        lines.Add("");
    }

    private static void AppendSarefAdvantages(List<string> lines, JsonArray? advantages)
    {
        if (advantages == null || advantages.Count == 0)
        {
            lines.Add("[dim]Преимуществ против Сарефа пока нет.[/]");
            lines.Add("");
            return;
        }

        lines.Add("[bold]Преимущества против Сарефа[/]");
        foreach (var advantage in advantages.OfType<JsonObject>())
        {
            var id = GetNodeString(advantage["advantageId"]) ?? "?";
            var name = GetNodeString(advantage["displayName"]) ?? GetNodeString(advantage["name"]) ?? id;
            var state = GetNodeString(advantage["state"]) ?? "?";
            var scenes = ReadSarefUiStringArray(advantage["applicableScenes"] as JsonArray).ToList();
            var summary = GetNodeString(advantage["summary"]);

            lines.Add($"  • [white]{Markup.Escape(name)}[/] [dim]({Markup.Escape(id)})[/] — {FormatSarefAdvantageState(state)}");
            if (scenes.Count > 0)
                lines.Add($"    Сцены: [dim]{Markup.Escape(string.Join(", ", scenes))}[/]");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");

            if (advantage["spentAudit"] is JsonObject spentAudit)
            {
                var usageId = GetNodeString(spentAudit["usageId"]) ?? "?";
                var turn = GetNodeInt(spentAudit["usedAtTurn"]);
                var sceneType = GetNodeString(spentAudit["sceneType"]) ?? "?";
                lines.Add($"    Потрачено: [dim]{Markup.Escape(usageId)}; ход {turn}; сцена {Markup.Escape(sceneType)}[/]");
            }

            var suppressionReason = GetNodeString(advantage["suppressionReason"]);
            if (!string.IsNullOrWhiteSpace(suppressionReason))
                lines.Add($"    [red]Причина подавления: {Markup.Escape(suppressionReason)}[/]");
        }
        lines.Add("");
    }

    private static void AppendSarefAdvantageUses(List<string> lines, JsonArray? uses)
    {
        if (uses == null || uses.Count == 0)
            return;

        lines.Add("[bold]Журнал использования преимуществ[/]");
        foreach (var use in uses.OfType<JsonObject>()
                     .OrderByDescending(use => GetNodeInt(use["usedAtTurn"])))
        {
            var usageId = GetNodeString(use["usageId"]) ?? "?";
            var advantageId = GetNodeString(use["advantageId"]) ?? "?";
            var sceneType = GetNodeString(use["sceneType"]) ?? "?";
            var summary = GetNodeString(use["summary"]);
            lines.Add($"  • [white]{Markup.Escape(usageId)}[/]: {Markup.Escape(advantageId)} — {Markup.Escape(sceneType)}; ход {GetNodeInt(use["usedAtTurn"])}");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
        }
    }

    private static IEnumerable<string> ReadSarefUiStringArray(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array)
        {
            var value = GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static int CountArray(JsonNode? node) =>
        node is JsonArray array ? array.Count : 0;

    private static string DescribeSarefRevealStage(string? stage) =>
        stage?.Trim().ToLowerInvariant() switch
        {
            "unknown" => "ты пока не знаешь, что искать",
            "shadow" => "тень без имени",
            "name_revealed" => "имя Сарефа раскрыто",
            "wings_revealed" => "Крылья Ангелов раскрыты",
            "infiltration_active" => "идёт внедрение в Крылья Ангелов",
            "confrontation_available" => "доступна развязка с Сарефом",
            "completed" => "сюжетная линия завершена",
            _ => stage ?? "неизвестно"
        };

    private static string DescribeSarefRevelationCategory(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "identity" => "личность Сарефа",
            "method" => "метод стирания памяти и изгнания",
            "faction" => "Крылья Ангелов",
            "path" => "путь к Крыльям Ангелов",
            "oath_break" => "разрыв клятвы",
            "war_doctrine" => "военная доктрина",
            "structural_weakness" => "слабость структуры",
            "exile_survival" => "выживание после изгнания",
            "false_light_cut" => "разрез ложного света",
            _ => category ?? "неизвестно"
        };

    private static string DescribeSarefWingsRouteSafety(string? routeSafety) =>
        routeSafety?.Trim().ToLowerInvariant() switch
        {
            "safe" => "безопасный маршрут",
            "risky" => "рискованный маршрут",
            "desperate" => "отчаянный маршрут",
            _ => routeSafety ?? "неизвестно"
        };

    private static string FormatSarefAdvantageState(string? state) =>
        state?.Trim().ToLowerInvariant() switch
        {
            "available" => "[green]Доступно[/]",
            "spent" => "[yellow]Потрачено[/]",
            "passive" => "[cyan]Пассивно[/]",
            "disabled" => "[dim]Отключено[/]",
            "suppressed" => "[red]Подавлено[/]",
            _ => Markup.Escape(state ?? "неизвестно")
        };
}
