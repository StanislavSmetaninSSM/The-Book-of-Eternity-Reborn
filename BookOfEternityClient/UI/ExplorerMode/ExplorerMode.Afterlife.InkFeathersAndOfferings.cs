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
    private async Task ShowSoulInfo()
    {
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (soulDoc == null)
        {
            ShowEmptyPanel("🌊 Душа", "Состояние души не обнаружено");
            WaitForKey();
            return;
        }

        var root = soulDoc.RootElement;
        var soulName = GetStr(root, "soulName", "Безымянная душа");
        var currentRealm = GetStr(root, "currentRealm", _stateManager.CurrentState.CurrentRealm);
        var currentIncarnation = GetInt(root, "currentIncarnation", 0);
        var currentFeathers = ReadInkFeathersCurrent(root);
        var totalFeathers = ReadInkFeathersTotal(root);
        var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
        var unread = notifications
            .Where(notification => string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(notification => notification.CreatedAtTurn)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .ToList();

        var lines = new List<string>
        {
            $"[bold white]{Markup.Escape(soulName)}[/]",
            $"  🌌 Текущая фаза: [cyan]{Markup.Escape(currentRealm)}[/]",
            $"  🔄 Инкарнация: [yellow]{currentIncarnation}[/]",
            $"  🪶 Чернильные Перья сейчас: [gold1]{currentFeathers}[/]",
            $"  🧾 Всего получено Чернильных Перьев: [gold1]{totalFeathers}[/]"
        };

        lines.Add("");
        lines.Add("[bold mediumpurple1]✨ Просветление[/]");
        if (root.TryGetProperty("enlightenment", out var enlightenmentNode) && enlightenmentNode.ValueKind == JsonValueKind.Object)
        {
            var tier = GetStr(enlightenmentNode, "currentTier", GetStr(enlightenmentNode, "level", ""));
            var experience = GetInt(enlightenmentNode, "experience", -1);
            var level = GetInt(enlightenmentNode, "level", -1);
            var progressPercent = GetInt(enlightenmentNode, "progressPercent", -1);
            if (!string.IsNullOrWhiteSpace(tier))
                lines.Add($"  ✨ Текущий тир: [mediumpurple1]{Markup.Escape(tier)}[/]");
            if (level >= 0)
                lines.Add($"  🧭 Уровень: [mediumpurple1]{level}[/]");
            if (experience >= 0)
                lines.Add($"  📈 Опыт просветления: [mediumpurple1]{experience}[/]");
            if (progressPercent >= 0)
                lines.Add($"  📊 Прогресс до следующего тира: [mediumpurple1]{progressPercent}%[/]");
        }
        else if (root.TryGetProperty("enlightenment", out var numericEnlightenmentNode) &&
                 numericEnlightenmentNode.ValueKind == JsonValueKind.Number)
        {
            lines.Add($"  ✨ Числовое значение просветления: [mediumpurple1]{numericEnlightenmentNode}[/]");
        }
        else
        {
            lines.Add("  [dim]Данные о просветлении пока отсутствуют.[/]");
        }

        lines.Add("");
        lines.Add("[bold cyan]🪞 История души[/]");
        var previousSoulNames = ReadSoulPreviousNames(root, soulName);
        if (previousSoulNames.Count > 0)
        {
            lines.Add($"  🏷️ Прежние имена: [white]{Markup.Escape(string.Join(", ", previousSoulNames))}[/]");
        }
        else
        {
            lines.Add("  [dim]Прежние имена души ещё не зафиксированы.[/]");
        }

        AppendPendingMemoryLegacyOverview(lines, root);

        if (unread.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold yellow]📬 Непрочитанные ответы Хранителей: {unread.Count}[/]");
            foreach (var notification in unread.Take(3))
                lines.Add($"  • {Markup.Escape(notification.Summary)}");
            if (unread.Count > 3)
                lines.Add($"  • [dim]…и ещё {unread.Count - 3}. Откройте «Ответы Хранителей», чтобы увидеть все записи полностью.[/]");
        }

        var manifestationRequests = await GuardianAbodeResidentRequestState.ReadManifestationRequestsAsync(_fs);
        var currentManifestationRequests = manifestationRequests
            .Where(request => request.TargetIncarnation == currentIncarnation)
            .ToList();
        if (currentManifestationRequests.Count > 0 &&
            RealmSemantics.IsMortalRealm(currentRealm))
        {
            lines.Add("");
            lines.Add($"[bold magenta]👤 Эхо спутников ищет путь в эту жизнь: {currentManifestationRequests.Count}[/]");
            foreach (var request in currentManifestationRequests)
            {
                var displayName = string.IsNullOrWhiteSpace(request.CompanionNameHint) ? request.RelicName : request.CompanionNameHint;
                var snapshotSummary = GuardianAbodeResidentRequestState.DescribeManifestationRequestSnapshot(request);
                var detailLine = string.IsNullOrWhiteSpace(snapshotSummary)
                    ? $"{displayName} должно проявиться как ранняя встреча или путь личного квеста души."
                    : $"{displayName} должно проявиться как ранняя встреча или путь личного квеста души [{snapshotSummary.TrimStart(',', ' ')}].";
                lines.Add($"  [dim]{Markup.Escape(detailLine)}[/]");
            }
        }

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌊 Состояние души ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1)
        };

        while (true)
        {
            Clear();
            Write(panel);
            WriteJsonAuditPanel("Полный JSON game_state/meta/soul_state.json", root, Color.Cyan1);

            var isPendingBootstrap = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap;
            var choices = new List<string>();
            if (!isPendingBootstrap)
            {
                choices.Add("✏️ Сменить имя души");
                choices.Add("🪶 Чернильные Перья");
                choices.Add("💎 Реликвии души");
                choices.Add("🌟 Квесты души");
                choices.Add("📬 Ответы Хранителей");
                if (currentManifestationRequests.Count > 0 &&
                    RealmSemantics.IsMortalRealm(currentRealm))
                {
                    choices.Add("👤 Осмотреть пути воплощения спутников");
                }
            }
            choices.Add("← Назад");

            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Действие души[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Сменить имя", StringComparison.Ordinal))
            {
                if (_soulIdentityService == null)
                {
                    MarkupLine("[red]Сервис переименования души недоступен.[/]");
                    WaitForKey();
                    continue;
                }

                var requestedName = Ask("[cyan]Новое имя души[/]");
                if (!await ConfirmSoulRenameLocalPreviewAsync(root, requestedName))
                    continue;

                var result = await _soulIdentityService.RenameSoulAsync(requestedName);
                if (!result.Success)
                {
                    MarkupLine($"[red]{Markup.Escape(result.ErrorMessage ?? "Не удалось сменить имя души.")}[/]");
                    WaitForKey();
                    continue;
                }

                await _stateManager.RefreshGameStateAsync();
                continue;
            }

            if (choice.Contains("Перья", StringComparison.Ordinal))
                await ShowInkFeathersMenu();
            else if (choice.Contains("Реликвии", StringComparison.Ordinal))
                await ShowSoulRelics();
            else if (choice.Contains("Квесты", StringComparison.Ordinal))
                await ShowSoulQuests();
            else if (choice.Contains("Ответы", StringComparison.Ordinal))
                await ShowAfterlifeInbox();
            else if (choice.Contains("пути воплощения", StringComparison.OrdinalIgnoreCase))
                await ShowManifestationRequestInspectionAsync(currentManifestationRequests);
        }
    }

    private async Task<bool> ConfirmSoulRenameLocalPreviewAsync(JsonElement currentSoulRoot, string requestedName)
    {
        var normalizedNewName = SoulIdentityService.NormalizeSoulName(requestedName);
        var currentSoulName = GetStr(currentSoulRoot, "soulName", "");
        var previousSoulNames = new List<string>();
        if (currentSoulRoot.TryGetProperty("previousSoulNames", out var previousNamesNode) &&
            previousNamesNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in previousNamesNode.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    var normalized = SoulIdentityService.NormalizeSoulName(entry.GetString());
                    if (!string.IsNullOrWhiteSpace(normalized) &&
                        !previousSoulNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        previousSoulNames.Add(normalized);
                    }
                }
            }
        }

        var blockers = new List<string>();
        if (string.IsNullOrWhiteSpace(normalizedNewName))
            blockers.Add("новое имя души пустое после нормализации");

        var changed = !string.Equals(currentSoulName, normalizedNewName, StringComparison.OrdinalIgnoreCase);
        var nextPreviousNames = previousSoulNames
            .Where(name => !string.Equals(name, normalizedNewName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (changed && !string.IsNullOrWhiteSpace(currentSoulName) &&
            !nextPreviousNames.Contains(currentSoulName, StringComparer.OrdinalIgnoreCase))
        {
            nextPreviousNames.Add(currentSoulName);
        }

        JsonObject beforeRoot;
        JsonObject afterRoot;
        try
        {
            beforeRoot = JsonNode.Parse(currentSoulRoot.GetRawText()) as JsonObject ?? new JsonObject();
            afterRoot = JsonNode.Parse(currentSoulRoot.GetRawText()) as JsonObject ?? new JsonObject();
        }
        catch
        {
            blockers.Add("текущий soul_state.json не удалось преобразовать в JSON preview");
            beforeRoot = new JsonObject();
            afterRoot = new JsonObject();
        }

        if (blockers.Count == 0)
        {
            afterRoot["soulName"] = normalizedNewName;
            afterRoot["previousSoulNames"] = new JsonArray(nextPreviousNames.Select(name => JsonValue.Create(name)).ToArray<JsonNode?>());
        }

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var touchesPendingGuardianCreation = false;
        if (!string.IsNullOrWhiteSpace(guardiansJson))
        {
            try
            {
                touchesPendingGuardianCreation = JsonNode.Parse(guardiansJson) is JsonObject guardiansRoot &&
                    guardiansRoot["pendingGuardianCreation"] is JsonObject;
            }
            catch
            {
                touchesPendingGuardianCreation = false;
            }
        }

        Clear();
        var lines = new List<string>
        {
            "[bold cyan]Смена имени души[/]",
            "",
            "[bold]Тип изменения:[/] client-local; GM turn не отправляется.",
            $"[bold]Имя:[/] {Markup.Escape(currentSoulName)} -> {Markup.Escape(normalizedNewName)}",
            $"[bold]Changed:[/] {(changed ? "yes" : "no; будет только canonical previousSoulNames normalization при необходимости")}",
            "[bold]Affected files:[/]",
            "  • game_state/meta/soul_state.json",
            touchesPendingGuardianCreation
                ? "  • game_state/meta/guardians.json [dim](pendingGuardianCreation.soulName sync)[/]"
                : "  • game_state/meta/guardians.json [dim](не меняется: pendingGuardianCreation не найден)[/]",
            "",
            "[bold]Последствия:[/]",
            "  • Старое имя попадёт в previousSoulNames, если имя действительно меняется.",
            "  • Реликвии, Перья, Архив, realm и инкарнация не должны меняться.",
            "  • Отмена на этом экране ничего не пишет."
        };

        if (blockers.Count > 0)
        {
            lines.Add("");
            lines.Add("[red]Блокеры:[/]");
            foreach (var blocker in blockers)
                lines.Add($"  • {Markup.Escape(blocker)}");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌊 Предпросмотр имени души ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(blockers.Count == 0 ? Color.Cyan1 : Color.Red),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("До: soul_state.json", beforeRoot, Color.Grey);
        WriteJsonAuditPanel("После: soul_state.json preview", afterRoot, blockers.Count == 0 ? Color.Cyan1 : Color.Red);

        if (blockers.Count > 0)
        {
            WaitForKey();
            return false;
        }

        return Confirm("[yellow]Применить локальное переименование души?[/]", true);
    }

    private async Task ShowManifestationRequestInspectionAsync(
        IReadOnlyList<GuardianAbodeResidentRequestState.PendingResidentCompanionManifestationRequest> requests)
    {
        if (requests.Count == 0)
        {
            MarkupLine("[yellow]Сейчас нет ожидающих manifestation requests текущей инкарнации.[/]");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choices = requests
                .Select(request =>
                {
                    var displayName = string.IsNullOrWhiteSpace(request.CompanionNameHint) ? request.RelicName : request.CompanionNameHint;
                    return $"👤 {Markup.Escape(displayName)} [dim]({Markup.Escape(request.RequestId)})[/]";
                })
                .ToList();
            choices.Add("← Назад");

            Clear();
            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Пути воплощения спутников[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(choices));
            if (selected.Contains("Назад", StringComparison.Ordinal))
                return;

            var requestIndex = choices.IndexOf(selected);
            if (requestIndex < 0 || requestIndex >= requests.Count)
                return;

            var request = requests[requestIndex];
            var lines = new List<string>
            {
                $"[bold magenta]👤 {Markup.Escape(string.IsNullOrWhiteSpace(request.CompanionNameHint) ? request.RelicName : request.CompanionNameHint)}[/]",
                "",
                $"  Идентификатор запроса: [dim]{Markup.Escape(request.RequestId)}[/]",
                $"  Источник воплощения: [white]{Markup.Escape(request.ManifestationSource)}[/]",
                $"  Целевая инкарнация: [white]{request.TargetIncarnation}[/]",
                $"  Реликвия-носитель: [white]{Markup.Escape(request.RelicName)}[/] [dim]({Markup.Escape(request.RelicId)})[/]"
            };
            if (!string.IsNullOrWhiteSpace(request.SourceGuardianName) || !string.IsNullOrWhiteSpace(request.SourceGuardianId))
                lines.Add($"  Источник-Хранитель: [dim]{Markup.Escape(string.IsNullOrWhiteSpace(request.SourceGuardianName) ? request.SourceGuardianId : request.SourceGuardianName)}[/]");
            if (!string.IsNullOrWhiteSpace(request.SourceResidentId))
                lines.Add($"  Источник-резидент: [dim]{Markup.Escape(request.SourceResidentId)}[/]");
            if (!string.IsNullOrWhiteSpace(request.SourceImprintId))
                lines.Add($"  Источник-слепок: [dim]{Markup.Escape(request.SourceImprintId)}[/]");
            if (!string.IsNullOrWhiteSpace(request.OriginWorldSummary))
                lines.Add($"  Мир происхождения: [dim]{Markup.Escape(request.OriginWorldSummary)}[/]");
            if (!string.IsNullOrWhiteSpace(request.BondReason))
                lines.Add($"  Связь: [dim]{Markup.Escape(request.BondReason)}[/]");
            if (!string.IsNullOrWhiteSpace(request.FutureCompanionPrompt))
                lines.Add($"  Примета будущего спутника: [dim]{Markup.Escape(request.FutureCompanionPrompt)}[/]");
            if (request.AppearanceMotifs.Count > 0)
                lines.Add($"  Мотивы облика: [dim]{Markup.Escape(string.Join(", ", request.AppearanceMotifs))}[/]");

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 👤 Точное воплощение ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Magenta1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var actions = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.SourceResidentId))
                actions.Add("👤 Открыть источник-резидента");
            if (!string.IsNullOrWhiteSpace(request.RelicId))
                actions.Add("💎 Открыть реликвию-носитель");
            actions.Add("← Назад");

            var action = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Действие[/]")
                    .HighlightStyle(new Style(Color.Gold1))
                    .AddChoices(actions));
            if (action.Contains("резидента", StringComparison.OrdinalIgnoreCase))
                await ShowGuardianAbodeResidentDetailByIdAsync(request.SourceResidentId);
            else if (action.Contains("реликвию", StringComparison.OrdinalIgnoreCase))
                await ShowSoulRelicDetailByIdAsync(request.RelicId);
        }
    }

    private static int ReadInkFeathersCurrent(JsonElement root)
    {
        if (!root.TryGetProperty("inkFeathers", out var feathersNode))
            return 0;

        if (feathersNode.ValueKind == JsonValueKind.Number && feathersNode.TryGetInt32(out var numericCurrent))
            return numericCurrent;

        if (feathersNode.ValueKind == JsonValueKind.Object &&
            feathersNode.TryGetProperty("current", out var currentNode) &&
            currentNode.ValueKind == JsonValueKind.Number &&
            currentNode.TryGetInt32(out var objectCurrent))
        {
            return objectCurrent;
        }

        return 0;
    }

    private static int ReadInkFeathersTotal(JsonElement root)
    {
        if (!root.TryGetProperty("inkFeathers", out var feathersNode))
            return 0;

        if (feathersNode.ValueKind == JsonValueKind.Number && feathersNode.TryGetInt32(out var numericTotal))
            return numericTotal;

        if (feathersNode.ValueKind == JsonValueKind.Object &&
            feathersNode.TryGetProperty("total", out var totalNode) &&
            totalNode.ValueKind == JsonValueKind.Number &&
            totalNode.TryGetInt32(out var objectTotal))
        {
            return objectTotal;
        }

        return ReadInkFeathersCurrent(root);
    }

    private static void AppendPendingMemoryLegacyOverview(List<string> lines, JsonElement root)
    {
        lines.Add("");
        lines.Add("[bold magenta]🧠 Наследие памяти для следующей жизни[/]");
        if (!root.TryGetProperty("pendingMemoryLegacy", out var pendingMemoryLegacy) ||
            pendingMemoryLegacy.ValueKind != JsonValueKind.Object)
        {
            lines.Add("  [dim]Активное наследие памяти сейчас отсутствует.[/]");
            return;
        }

        AddSoulInfoDetailLine(lines, "ID наследия", GetStr(pendingMemoryLegacy, "legacyId", ""));
        AddSoulInfoDetailLine(lines, "Тип наследия", DescribePendingMemoryLegacyType(GetStr(pendingMemoryLegacy, "legacyType", "")));
        AddSoulInfoDetailLine(lines, "Источник дара", DescribePendingMemoryLegacyGrantSource(GetStr(pendingMemoryLegacy, "grantSource", "")));
        AddSoulInfoDetailLine(lines, "Состояние применения", DescribePendingMemoryLegacyApplicationState(GetStr(pendingMemoryLegacy, "applicationState", "")));
        AddSoulInfoDetailLine(lines, "Подсказка об исходной жизни", GetStr(pendingMemoryLegacy, "sourceLifeHint", ""));
        AddSoulInfoDetailLine(lines, "Выдано", GetStr(pendingMemoryLegacy, "grantedAtUtc", ""));
        AddSoulInfoDetailLine(lines, "Характеристика", GetStr(pendingMemoryLegacy, "characteristic", ""));
        AddSoulInfoDetailLine(lines, "Навык", GetStr(pendingMemoryLegacy, "skillName", ""));
        AddSoulInfoDetailLine(lines, "Бонус игроку", GetStr(pendingMemoryLegacy, "playerStatBonus", ""));

        if (pendingMemoryLegacy.TryGetProperty("bonus", out var bonusNode) &&
            bonusNode.ValueKind == JsonValueKind.Number &&
            bonusNode.TryGetInt32(out var bonus))
        {
            lines.Add($"  ➕ Размер бонуса: [white]{bonus}[/]");
        }

        if (pendingMemoryLegacy.TryGetProperty("grantSnapshot", out var grantSnapshot) &&
            grantSnapshot.ValueKind == JsonValueKind.Object)
        {
            lines.Add("  [bold]Снимок дара:[/]");
            foreach (var snapshotLine in BuildElementInspectionLines(grantSnapshot, "    "))
                lines.Add(snapshotLine);
        }
        else
        {
            lines.Add("  [dim]Снимок дара пока отсутствует.[/]");
        }
    }

    private static void AddSoulInfoDetailLine(List<string> lines, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        lines.Add($"  • {Markup.Escape(label)}: [white]{Markup.Escape(value)}[/]");
    }

    private static IEnumerable<string> BuildElementInspectionLines(JsonElement element, string indent)
    {
        if (element.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in element.EnumerateObject())
        {
            var label = ResolveSoulInspectionLabel(property.Name);
            var value = DescribeSoulInspectionValue(property.Name, property.Value);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            yield return $"{indent}• {Markup.Escape(label)}: [dim]{Markup.Escape(value)}[/]";
        }
    }

    private static string ResolveSoulInspectionLabel(string propertyName) => propertyName switch
    {
        "legacyId" => "ID наследия",
        "legacyType" => "Тип наследия",
        "sourceLifeHint" => "Подсказка об исходной жизни",
        "grantSource" => "Источник дара",
        "applicationState" => "Состояние применения",
        "grantedAtUtc" => "Выдано",
        "characteristic" => "Характеристика",
        "bonus" => "Размер бонуса",
        "skillName" => "Навык",
        "playerStatBonus" => "Бонус игроку",
        "structuredBonuses" => "Структурированные бонусы",
        "summary" => "Сводка",
        _ => propertyName
    };

    private static string DescribeSoulInspectionValue(string propertyName, JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => DescribeSoulInspectionStringValue(propertyName, element.GetString() ?? string.Empty),
        JsonValueKind.Number => element.ToString(),
        JsonValueKind.True => "да",
        JsonValueKind.False => "нет",
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Array => string.Join(", ", element.EnumerateArray()
            .Select(item => DescribeSoulInspectionValue(propertyName, item))
            .Where(value => !string.IsNullOrWhiteSpace(value))),
        JsonValueKind.Object => string.Join("; ", element.EnumerateObject()
            .Select(property => $"{ResolveSoulInspectionLabel(property.Name)} — {DescribeSoulInspectionValue(property.Name, property.Value)}")
            .Where(value => !string.IsNullOrWhiteSpace(value))),
        _ => element.ToString()
    };

    private static string DescribeSoulInspectionStringValue(string propertyName, string value) => propertyName switch
    {
        "legacyType" => DescribePendingMemoryLegacyType(value),
        "grantSource" => DescribePendingMemoryLegacyGrantSource(value),
        "applicationState" => DescribePendingMemoryLegacyApplicationState(value),
        _ => value
    };

    private static string DescribePendingMemoryLegacyType(string? legacyType) =>
        (legacyType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "stat_bonus" => "усиление характеристики",
            "skill_knowledge" => "наследие знания",
            "startingcharacteristicbonus" => "усиление стартовой характеристики",
            "startingpassiveknowledgeskill" => "пассивный навык знания",
            _ => legacyType ?? string.Empty
        };

    private static string DescribePendingMemoryLegacyGrantSource(string? grantSource) =>
        (grantSource ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "memory_gates" => "Врата Памяти",
            "memorylegacygrant" => "дар наследия памяти",
            "archive" => "Архив души",
            _ => grantSource ?? string.Empty
        };

    private static string DescribePendingMemoryLegacyApplicationState(string? applicationState) =>
        (applicationState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" => "ожидает следующей жизни",
            "applied" => "уже перенесено в новую жизнь",
            "consumed" => "наследие уже исчерпано",
            _ => applicationState ?? string.Empty
        };

    private async Task ShowInkFeathersMenu()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Чернильные Перья"))
            return;

        _diceRevealed = false;
        while (true)
        {
            var feathers = await ReadInkFeathersBalance();
            var isAfterlifeRealm = IsOrdinaryAfterlifeInteractionState;
            Services.PendingTurnState? pendingTurnState = null;
            if (!isAfterlifeRealm && _pendingTurnState != null)
            {
                pendingTurnState = await _pendingTurnState.GetOrCreateAsync();
                _diceRevealed = pendingTurnState.IsFateLocked;
            }

            var phaseLabel = isAfterlifeRealm
                ? $"[blue]{Markup.Escape(_stateManager.CurrentState.CurrentRealm)}[/]"
                : "[green]Смертная жизнь[/]";
            var pendingLegacySummary = isAfterlifeRealm ? await ReadPendingMemoryLegacySummaryAsync() : null;
            Write(new Panel(new Markup(
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

            if (!isAfterlifeRealm)
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

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Выберите действие:[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (choice.Contains("Назад")) return;

            if (choice.Contains("🔒"))
            {
                MarkupLine("[yellow]⚠️ Недостаточно Чернильных Перьев или условие не выполнено.[/]");
                WaitForKey();
                continue;
            }

            // Route to handler
            if (!isAfterlifeRealm)
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
                            $"Повысь репутацию с текущим Хранителем ровно на {Math.Min(25, Math.Max(15, cost / 3))} по формуле reputationChange = min(25, max(15, cost / 3)). " +
                            "Перья уже списаны клиентом.",
                        cost => new[]
                        {
                            "Формула: reputationChange = min(25, max(15, cost / 3)).",
                            $"При текущей цене {cost} Чернильных Перьев expected reputationChange = {Math.Min(25, Math.Max(15, cost / 3))}.",
                            $"Чернильные Перья: {feathers} -> {Math.Max(0, feathers - cost)}.",
                            "GM обязан указать target guardian identity: guardianId, guardianName и текущий relationship/reputation context.",
                            "GM обязан реально изменить game_state/meta/guardians.json для текущего Хранителя и показать before/after reputation delta.",
                            "output/ink_feather_action_result.json должен содержать stateEvidence.guardianId, stateEvidence.guardianName, stateEvidence.reputationChange и affectedFiles."
                        },
                        cost => $"Репутация текущего Хранителя вырастет ровно на {Math.Min(25, Math.Max(15, cost / 3))}.");
                else if (choice.Contains("Культивировать Просветление"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(20, (int)(feathers * 0.25)),
                        "✨ Культивировать Просветление",
                        cost => $"[INK_FEATHER_ACTION: CULTIVATE_ENLIGHTENMENT] Игрок тратит {cost} Чернильных Перьев на Культивирование Просветления. " +
                            $"Добавь ровно {cost * 2} experience в soul_state.enlightenment.experience по формуле experienceGain = costInFeathers * 2. " +
                            "Перья уже списаны клиентом.",
                        cost => new[]
                        {
                            "Формула: experienceGain = costInFeathers * 2.",
                            $"При текущей цене {cost} Чернильных Перьев expected experienceGain = {cost * 2}.",
                            "GM обязан реально изменить game_state/meta/soul_state.json: enlightenment.experience должен вырасти не меньше заявленного gain.",
                            "output/ink_feather_action_result.json должен содержать stateEvidence.experienceGain."
                        },
                        cost => $"Просветление получит ровно {cost * 2} experience.");
                else if (choice.Contains("Попросить об услуге"))
                    await HandleGuardianFavor(feathers);
                else if (choice.Contains("Открыть Врата Памяти"))
                {
                    var currentSoulRoot = await ReadSoulRootForInkFeatherPreviewAsync();
                    await HandleGmFeatherAction(feathers,
                        Math.Max(15, (int)(feathers * 0.20)),
                        "🧠 Открыть Врата Памяти",
                        cost => $"[INK_FEATHER_ACTION: MEMORY_GATES] Игрок тратит {cost} Чернильных Перьев на Открытие Врат Памяти. " +
                            "Создай одно active pendingMemoryLegacy для следующей смертной жизни. " +
                            "Выбери ровно один механический бонус: либо +2 к одной стартовой характеристике, либо один новый пассивный навык знаний. " +
                            "Запиши structured metaStateUpdates.memoryLegacyGrant и замени старое наследие, если оно уже существовало. " +
                            "Перья уже списаны клиентом.",
                        cost => BuildMemoryGatesPreviewAuditLines(cost, feathers, currentSoulRoot),
                        _ => "Будет создано или заменено одно наследие памяти для следующей жизни.");
                }
                else if (choice.Contains("Создать Слепок Души"))
                    await HandleGmFeatherAction(feathers,
                        Math.Max(100, (int)(feathers * 0.50)),
                        "👤 Создать Слепок Души",
                        cost => $"[INK_FEATHER_ACTION: SOUL_IMPRINT] Игрок тратит {cost} Чернильных Перьев на Создание Слепка Души текущего компаньона. " +
                            "Создай soulImprint запись с идентичностью, traits/personality markers и summary текущего NPC-компаньона. " +
                            "Слепок не обязан немедленно создавать mortal-world NPC, но должен подготовить будущий imprint/companion путь. " +
                            "Перья уже списаны клиентом.",
                        cost => BuildSoulImprintPreviewAuditLines(cost, feathers),
                        _ => "Будет сохранён soulImprint текущего компаньона для будущего пути воплощения.");
            }

            // If a GM action was set, break out of the loop
            if (_pendingGmAction != null) return;
        }
    }

    private async Task<JsonObject?> ReadSoulRootForInkFeatherPreviewAsync()
    {
        var raw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    internal static IReadOnlyList<string> BuildMemoryGatesPreviewAuditLines(int costInFeathers, int currentFeathers, JsonObject? currentSoulRoot)
    {
        var lines = new List<string>
        {
            "Результат: создаётся active pendingMemoryLegacy для следующей смертной жизни.",
            "Разрешён ровно один механический бонус: +2 к одной стартовой характеристике ИЛИ один пассивный Knowledge-навык.",
            "GM обязан писать structured metaStateUpdates.memoryLegacyGrant; canonical pendingMemoryLegacy должен быть его projection.",
            $"Чернильные Перья: {currentFeathers} -> {Math.Max(0, currentFeathers - costInFeathers)}.",
            "Если уже есть active pendingMemoryLegacy, новая трата заменяет старое наследие, а не складывается с ним."
        };

        if (currentSoulRoot?["pendingMemoryLegacy"] is JsonObject existingLegacy)
        {
            lines.Add("Текущее наследие перед заменой:");
            lines.Add($"  legacyId={GetNodeString(existingLegacy["legacyId"]) ?? "unknown"}; legacyType={GetNodeString(existingLegacy["legacyType"]) ?? "unknown"}; grantSource={GetNodeString(existingLegacy["grantSource"]) ?? "unknown"}; applicationState={GetNodeString(existingLegacy["applicationState"]) ?? "unknown"}.");
            lines.Add($"  full before payload={existingLegacy.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed).Replace("\r", "").Replace("\n", " ")}");
        }
        else
        {
            lines.Add("Текущее наследие перед созданием: pendingMemoryLegacy отсутствует; это первое наследие памяти.");
        }

        lines.Add("Canonical after payload schema:");
        lines.Add("  pendingMemoryLegacy.legacyId: new non-empty id.");
        lines.Add("  pendingMemoryLegacy.legacyType: startingCharacteristicBonus или startingPassiveKnowledgeSkill.");
        lines.Add("  pendingMemoryLegacy.grantSource: memoryLegacyGrant.");
        lines.Add("  pendingMemoryLegacy.applicationState: pending.");
        lines.Add("  pendingMemoryLegacy.grantSnapshot: exact structured metaStateUpdates.memoryLegacyGrant object, including legacyId, legacyType, source turn/request/cost and selected bonus.");
        lines.Add("  pendingMemoryLegacy for startingCharacteristicBonus: characteristic plus bonus=2.");
        lines.Add("  pendingMemoryLegacy for startingPassiveKnowledgeSkill: skillName, skillDescription, group=Knowledge and non-empty structuredBonuses.");
        lines.Add("  source ids/context: carry sourceLifeHint/sourceGuardianId/sourceActionTag when known.");

        return lines;
    }

    internal static IReadOnlyList<string> BuildSoulImprintPreviewAuditLines(int costInFeathers, int currentFeathers)
    {
        var projectedFeathers = Math.Max(0, currentFeathers - costInFeathers);
        return new[]
        {
            "Результат: game_state/meta/soul_state.json получает soulImprint entry.",
            $"Чернильные Перья: {currentFeathers} -> {projectedFeathers}.",
            "Аудит source/target: GM должен явно назвать текущего companion/NPC source, включая sourceCompanionId или NPCId, если известны, companionName/NPCName, relationship role и текущий контекст связи.",
            "Обязательный soulImprint payload: imprintId, sourceCompanionId или NPCId, companionName/NPCName, summary, coreTraits, personalityMarkers, relationshipMarkers, appearanceMotifs и futureCompanionPrompt/companion path hint.",
            "output/ink_feather_action_result.json должен содержать stateEvidence.imprintId, stateEvidence.companionName, stateEvidence.affectedFiles с game_state/meta/soul_state.json и summary реального stateful результата.",
            "Это подготовка будущего companion/imprint пути; немедленный Mortal World NPC в afterlife-ходе не создаётся."
        };
    }

    internal static JsonObject BuildInkFeatherActionAuditNode(
        string actionTag,
        string playerAction,
        int costInFeathers,
        int currentFeathers,
        IReadOnlyList<string> effectLines) =>
        new()
        {
            ["actionTag"] = actionTag,
            ["playerAction"] = playerAction,
            ["costInFeathers"] = costInFeathers,
            ["currentFeathers"] = currentFeathers,
            ["projectedFeathers"] = Math.Max(0, currentFeathers - costInFeathers),
            ["alreadyDeductedByClient"] = true,
            ["requiredOutputFile"] = "output/ink_feather_action_result.json",
            ["requiredReceiptFields"] = new JsonArray
            {
                "sessionId",
                "requestId",
                "turnNumber",
                "actionTag",
                "resolved",
                "costInFeathers",
                "resolutionType",
                "summary",
                "stateEvidence"
            },
            ["requiredStateEvidence"] = new JsonArray
            {
                "affectedFiles",
                "statefulResultId or domain-specific identity",
                "before/after delta where applicable"
            },
            ["effectPreview"] = new JsonArray(effectLines.Select(line => JsonValue.Create(line)).ToArray<JsonNode?>())
        };

    private static string ExtractInkFeatherActionTag(string gmAction)
    {
        const string prefix = "[INK_FEATHER_ACTION:";
        var start = gmAction.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "INK_FEATHER_ACTION";

        start += prefix.Length;
        var end = gmAction.IndexOf(']', start);
        if (end <= start)
            return "INK_FEATHER_ACTION";

        return gmAction[start..end].Trim();
    }

    internal static IReadOnlyList<string> BuildAbodeOfferingPreviewAuditLines(
        string offeringType,
        string consumedObjectLabel,
        string consumedObjectId,
        string consumedObjectKind,
        string consumedObjectRarity,
        int currentPower,
        int basePowerGain,
        int? currentFeathers = null,
        int? costInFeathers = null,
        int? capRemainingBefore = null)
    {
        var projectedPower = AbodePowerRules.ClampCurrentPower(currentPower + Math.Max(0, basePowerGain));
        var finalDelta = Math.Max(0, projectedPower - AbodePowerRules.ClampCurrentPower(currentPower));
        var lines = new List<string>
        {
            "[bold]Прогноз state/resource delta:[/]",
            $"  • Abode Power: {AbodePowerRules.ClampCurrentPower(currentPower)} -> {projectedPower} (baseDelta={Math.Max(0, basePowerGain)}, finalDelta={finalDelta}).",
            $"  • offeringType: {offeringType}.",
            $"  • Consumed object: {consumedObjectLabel} ({consumedObjectId}); kind={consumedObjectKind}; rarity={consumedObjectRarity}.",
            "  • Обязательный guardianPowerEvents audit: reasonType=offering, sourceSurface=guardianAbodeOffering, returnCycleId, baseDelta, finalDelta and powerEventId.",
            "  • Обязательное affected state: game_state/meta/guardians.json plus game_state/meta/abode_power_journal.json; relic/archive offerings also affect game_state/meta/soul_state.json."
        };

        if (currentFeathers.HasValue && costInFeathers.HasValue)
            lines.Add($"  • Ink Feathers: {Math.Max(0, currentFeathers.Value)} -> {Math.Max(0, currentFeathers.Value - Math.Max(0, costInFeathers.Value))}.");
        if (capRemainingBefore.HasValue)
            lines.Add($"  • Return-cycle offering cap remaining before: {Math.Max(0, capRemainingBefore.Value)}.");

        return lines;
    }

    private async Task HandleRevealFate(int feathers)
    {
        var cost = Math.Max(5, (int)(feathers * 0.10));
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";

        var confirm = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]🔮 Открыть Судьбу — потратить {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, потратить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (_pendingTurnState == null)
        {
            MarkupLine("[red]❌ Сервис судьбы недоступен.[/]");
            WaitForKey();
            return;
        }

        var pendingState = await _pendingTurnState.GetOrCreateAsync();
        if (!await DeductInkFeathers(cost))
        {
            MarkupLine("[red]❌ Не удалось списать перья.[/]");
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
            $"[bold]🎰 Гача-база:[/] [{rarityColor}]{Markup.Escape(DescribeRarityLabel(gacha.BaseRarity ?? "Common"))}[/] (счёт: {gacha.BaseScore})",
            "[dim]Гача-база вычислена отдельно от этих кубиков и не сдвигает ваш dice pool.[/]",
            "",
            $"[dim]Списано: {cost} 🪶[/]"
        };

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🔮 Судьба открыта ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1)
        };
        Write(panel);
        WaitForKey();

        _diceRevealed = true;
    }

    private async Task HandleRewriteFate(int feathers)
    {
        var cost = Math.Max(15, (int)(feathers * 0.25));
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";

        var confirm = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]✍️ Переписать Судьбу — потратить {Markup.Escape(costDisplay)}?[/]")
            .AddChoices("✅ Да, потратить", "❌ Отмена"));
        if (confirm.Contains("Отмена")) return;

        if (_pendingTurnState == null)
        {
            MarkupLine("[red]❌ Сервис судьбы недоступен.[/]");
            WaitForKey();
            return;
        }

        var currentState = await _pendingTurnState.GetOrCreateAsync();
        if (!currentState.IsFateLocked)
        {
            MarkupLine("[yellow]⚠️ Сначала нужно открыть судьбу, чтобы зафиксировать текущие кости.[/]");
            WaitForKey();
            return;
        }

        // Deduct feathers FIRST (before any dice modification)
        if (!await DeductInkFeathers(cost))
        {
            MarkupLine("[red]❌ Не удалось списать перья (недостаточно или ошибка).[/]");
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
            $"  Гача: [{oldRarityColor}]{Markup.Escape(DescribeRarityLabel(oldGacha.BaseRarity ?? "Common"))}[/] ({oldGacha.BaseScore})",
            "",
            "[bold]🎲 Новые кости:[/]",
            FormatDiceDisplay(newDice),
            $"  Гача: [{newRarityColor}]{Markup.Escape(DescribeRarityLabel(newGacha.BaseRarity ?? "Common"))}[/] ({newGacha.BaseScore})",
            "",
            "[dim]Новый фиксированный набор сохранится до вашего следующего обычного хода.[/]",
            "",
            $"[dim]Списано: {cost} 🪶[/]"
        };

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" ✍️ Судьба переписана ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        };
        Write(panel);
        WaitForKey();

        _diceRevealed = true;
    }

    private async Task HandleGmFeatherAction(
        int feathers,
        int cost,
        string actionName,
        Func<int, string> buildGmAction,
        Func<int, IReadOnlyList<string>>? buildEffectLines = null,
        Func<int, string>? buildConfirmationSummary = null)
    {
        var costDisplay = $"{cost} 🪶 (останется {feathers - cost})";
        var effectLines = buildEffectLines?.Invoke(cost) ?? Array.Empty<string>();
        var confirmationSummary = buildConfirmationSummary?.Invoke(cost);
        var gmAction = buildGmAction(cost);
        var actionTag = ExtractInkFeatherActionTag(gmAction);
        var previewLines = new List<string>
        {
            $"[bold yellow]{Markup.Escape(actionName)}[/]",
            $"Action tag: [white]{Markup.Escape(actionTag)}[/]",
            $"Цена: [yellow]{cost} Чернильных Перьев[/]",
            $"Чернильные Перья: [gold1]{feathers}[/] -> [gold1]{Math.Max(0, feathers - cost)}[/]",
            "",
            "[bold]GM closure contract:[/]",
            "  • output/ink_feather_action_result.json обязателен.",
            "  • Required receipt fields: sessionId, requestId, turnNumber, actionTag, resolved, costInFeathers, resolutionType, summary, stateEvidence.",
            "  • stateEvidence обязан содержать affectedFiles и доказательство реального canonical state результата.",
            "  • Перья уже списывает клиент; GM не списывает их второй раз.",
            "",
            "[bold]Ожидаемый результат:[/]"
        };
        if (effectLines.Count > 0)
            previewLines.AddRange(effectLines.Select(line => $"  • {Markup.Escape(line)}"));
        else
            previewLines.Add("  • GM выбирает stateful результат согласно actionTag и доказывает его через stateEvidence.");
        if (!string.IsNullOrWhiteSpace(confirmationSummary))
            previewLines.Add($"  • Итог перед подтверждением: {Markup.Escape(confirmationSummary)}");
        AppendChaosSeaCommonContractRules(previewLines);

        if (!ConfirmChaosSeaContractPreview(
                $"Полный контракт Ink Feather action — {actionName}",
                previewLines,
                BuildInkFeatherActionAuditNode(actionTag, gmAction, cost, feathers, effectLines),
                "Полный JSON-аудит output/ink_feather_action_result.json",
                confirmChoice: $"✅ Да, потратить {Markup.Escape(costDisplay)}"))
        {
            return;
        }

        await EnsurePendingLocalTurnRollbackSnapshotAsync("game_state/meta/soul_state.json");
        if (!await DeductInkFeathers(cost))
        {
            await DiscardPendingLocalTurnRollbackSnapshotAsync();
            MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        MarkupLine($"[green]✅ Списано {cost} 🪶. Действие отправлено Мастеру Игры.[/]");
        WaitForKey();

        _pendingGmAction = gmAction +
            " Также обязательно запиши output/ink_feather_action_result.json с exact sessionId/requestId/turnNumber текущего turn_request, actionTag, resolved=true, costInFeathers, resolutionType, summary и stateEvidence. stateEvidence обязан содержать affectedFiles и минимальное подтверждение реального stateful результата.";
    }

    private void ShowInkFeatherActionEffectPreview(string actionName, int cost, int feathers, IReadOnlyList<string> effectLines)
    {
        var lines = new List<string>
        {
            $"[bold yellow]{Markup.Escape(actionName)}[/]",
            $"Цена: [yellow]{cost} Чернильных Перьев[/] (останется {Math.Max(0, feathers - cost)})",
            "",
            "[bold]Что произойдёт после подтверждения:[/]"
        };
        lines.AddRange(effectLines.Select(line => $"  • {Markup.Escape(line)}"));

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Механика afterlife Ink Feather action ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
    }

    private async Task HandleGuardianFavor(int feathers)
    {
        var inputCost = Prompt(new TextPrompt<int>(
            $"[bold yellow]🤝 Сколько Перьев предложить Хранителю? (у вас {feathers} 🪶, мин. 10):[/]")
            .Validate(val =>
            {
                if (val < 10) return ValidationResult.Error("[red]Минимум 10 перьев[/]");
                if (val > feathers) return ValidationResult.Error($"[red]У вас только {feathers} 🪶[/]");
                return ValidationResult.Success();
            }));

        var costDisplay = $"{inputCost} 🪶 (останется {feathers - inputCost})";
        var gmAction = $"[INK_FEATHER_ACTION: GUARDIAN_FAVOR] Игрок предлагает Хранителю {inputCost} Чернильных Перьев в обмен на услугу. " +
            "Игрок может просить о чём-то, а может просто передавать перья в дар. " +
            "Гарантированный механический минимум: репутация с текущим Хранителем должна вырасти. " +
            "Перья уже списаны клиентом. Обязательно запиши output/ink_feather_action_result.json с guardianId, reputationChange и stateEvidence; всё остальное зависит от ролеплея и может быть добавлено дополнительно.";
        var effectLines = new[]
        {
            "Гарантированный минимум: репутация с текущим Хранителем должна реально вырасти.",
            "Дополнительная услуга может быть нарративной или stateful, но не заменяет обязательный рост репутации.",
            $"Чернильные Перья: {feathers} -> {Math.Max(0, feathers - inputCost)}.",
            "GM обязан указать target guardian identity: guardianId, guardianName и текущий relationship/reputation context.",
            "GM обязан реально изменить game_state/meta/guardians.json, показать before/after reputation delta и записать guardianId/reputationChange в stateEvidence.",
            "Цена переменная; валидатор проверяет факт положительного reputationChange, а не фиксированную формулу."
        };
        var previewLines = new List<string>
        {
            "[bold yellow]🤝 Попросить Хранителя об услуге[/]",
            "Action tag: [white]GUARDIAN_FAVOR[/]",
            $"Цена: [yellow]{inputCost} Чернильных Перьев[/]",
            $"Чернильные Перья: [gold1]{feathers}[/] -> [gold1]{Math.Max(0, feathers - inputCost)}[/]",
            "",
            "[bold]GM closure contract:[/]",
            "  • output/ink_feather_action_result.json обязателен.",
            "  • Required fields: sessionId, requestId, turnNumber, actionTag, resolved, costInFeathers, resolutionType, summary, stateEvidence.",
            "  • stateEvidence обязан содержать guardianId, guardianName, reputationChange, affectedFiles и before/after reputation audit.",
            "  • Перья уже списывает клиент; GM не списывает их второй раз.",
            "",
            "[bold]Ожидаемый результат:[/]"
        };
        previewLines.AddRange(effectLines.Select(line => $"  • {Markup.Escape(line)}"));
        AppendChaosSeaCommonContractRules(previewLines);

        if (!ConfirmChaosSeaContractPreview(
                "Полный контракт услуги Хранителя",
                previewLines,
                BuildInkFeatherActionAuditNode("GUARDIAN_FAVOR", gmAction, inputCost, feathers, effectLines),
                "Полный JSON-аудит GUARDIAN_FAVOR receipt",
                confirmChoice: $"✅ Да, предложить {Markup.Escape(costDisplay)}"))
        {
            return;
        }

        await EnsurePendingLocalTurnRollbackSnapshotAsync("game_state/meta/soul_state.json");
        if (!await DeductInkFeathers(inputCost))
        {
            await DiscardPendingLocalTurnRollbackSnapshotAsync();
            MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        MarkupLine($"[green]✅ Списано {inputCost} 🪶. Запрос услуги отправлен Хранителю.[/]");
        WaitForKey();

        _pendingGmAction = gmAction;
    }

    private async Task ShowAbodeOffering()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Подношение Обители"))
            return;

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc == null)
        {
            ShowEmptyPanel("🏛 Подношение Обители", "Данные хранителей недоступны.");
            return;
        }

        var guardians = CollectGuardianDisplayEntries(guardiansDoc.RootElement);
        if (guardians.Count == 0)
        {
            ShowEmptyPanel("🏛 Подношение Обители", "В этой фазе ещё нет известных Хранителей.");
            return;
        }

        var journalDoc = await _stateManager.LoadGameStateFileAsync(GuardianPowerEventState.JournalPath);
        var returnCycleId = GuardianAbodeOfferingState.BuildReturnCycleId(_stateManager.CurrentState.Incarnation);
        var feathers = await ReadInkFeathersBalance();
        var pendingOffering = await GuardianAbodeOfferingState.ReadAsync(_fs);
        if (pendingOffering != null)
        {
            WriteJsonAuditPanel(
                "Текущий pending JSON подношения Обители",
                JsonSerializer.SerializeToNode(pendingOffering, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                Color.Gold1);
        }

        var choices = guardians
            .Select(guardian =>
            {
                var guardianId = GetStr(guardian, "guardianId", "");
                var displayName = GuardianManifestation.GetDisplayName(guardian);
                var currentPower = AbodePowerRules.GetCurrentPower(guardian);
                var alreadyOffered = journalDoc == null
                    ? 0
                    : GuardianAbodeOfferingState.CountOfferedInkFeathersForReturnCycle(journalDoc.RootElement, guardianId, returnCycleId);
                var remainingCap = Math.Max(0, 150 - alreadyOffered);
                return ($"🏛 {Markup.Escape(displayName)} [dim](сила {currentPower}/100 • лимит {remainingCap} 🪶)[/]", guardian);
            })
            .ToList();

        choices.Add(("[dim]← Назад[/]", default(JsonElement)));

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold gold1]🏛 Выберите Обитель для подношения:[/]")
            .PageSize(12)
            .AddChoices(choices.Select(item => item.Item1)));

        if (selected.Contains("Назад", StringComparison.Ordinal))
            return;

        var selectedIndex = choices.FindIndex(item => item.Item1 == selected);
        if (selectedIndex < 0 || selectedIndex >= guardians.Count)
            return;

        var chosenGuardian = guardians[selectedIndex];
        var guardianIdChosen = GetStr(chosenGuardian, "guardianId", "");
        var guardianNameChosen = GuardianManifestation.GetDisplayName(chosenGuardian);
        var currentAbodePowerChosen = AbodePowerRules.GetCurrentPower(chosenGuardian);
        var alreadyOfferedChosen = journalDoc == null
            ? 0
            : GuardianAbodeOfferingState.CountOfferedInkFeathersForReturnCycle(journalDoc.RootElement, guardianIdChosen, returnCycleId);
        var remainingCapChosen = Math.Max(0, 150 - alreadyOfferedChosen);
        var storedRelics = await ReadStoredSoulRelicsForAbodeOfferingAsync();
        var archiveEntries = (await ReadStoredAfterlifeArchiveEntriesAsync())
            .Where(entry => !entry.IsReserved)
            .ToList();

        var offeringModes = new List<string>();
        if (remainingCapChosen >= 50 && feathers >= 50)
            offeringModes.Add("🪶 Чернильные Перья");
        if (storedRelics.Count > 0)
            offeringModes.Add("💎 Реликвия Души");
        if (archiveEntries.Any(entry => string.Equals(entry.EntryType, AfterlifeArchiveState.EntryTypeLoreFragment, StringComparison.OrdinalIgnoreCase)))
            offeringModes.Add("📚 Фрагмент Знания");
        if (archiveEntries.Any(entry => string.Equals(entry.EntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase)))
            offeringModes.Add("🕯 Запись Тайны");
        offeringModes.Add("← Назад");

        if (offeringModes.Count == 1)
        {
            ShowEmptyPanel("🏛 Подношение Обители", "Сейчас нечего поднести: не хватает Перьев, нет доступных Реликвий Души и Архив души пуст.");
            return;
        }

        var offeringMode = Prompt(new SelectionPrompt<string>()
            .Title($"[bold gold1]Чем поднести Обители {Markup.Escape(guardianNameChosen)}?[/]")
            .AddChoices(offeringModes));

        if (offeringMode.Contains("Назад", StringComparison.Ordinal))
            return;

        if (offeringMode.StartsWith("💎", StringComparison.Ordinal))
        {
            var relicChoices = storedRelics
                .Select(relic =>
                    relic.HasValidRarity
                        ? $"{Markup.Escape(relic.Name)} [dim]({Markup.Escape(relic.Rarity)} via {Markup.Escape(relic.RaritySource)})[/]"
                        : $"{Markup.Escape(relic.Name)} [red](invalid rarity: {Markup.Escape(relic.RarityIssue)})[/]")
                .ToList();
            relicChoices.Add("[dim]← Назад[/]");

            var selectedRelic = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Какую Реликвию Души поднести Обители {Markup.Escape(guardianNameChosen)}?[/]")
                .PageSize(12)
                .AddChoices(relicChoices));

            if (selectedRelic.Contains("Назад", StringComparison.Ordinal))
                return;

            var relicIndex = relicChoices.IndexOf(selectedRelic);
            if (relicIndex < 0 || relicIndex >= storedRelics.Count)
                return;

            var relic = storedRelics[relicIndex];
            if (!relic.HasValidRarity)
            {
                MarkupLine($"[red]❌ Нельзя поднести реликвию «{Markup.Escape(relic.Name)}»: {Markup.Escape(relic.RarityIssue)}.[/]");
                MarkupLine($"[dim]Для destructive offering требуется одно из полей rarity, quality, relicRarity со значением: {Markup.Escape(GuardianAbodeOfferingState.DescribeCanonicalSoulRelicRarities())}.[/]");
                WaitForKey();
                return;
            }

            var relicPowerGain = GuardianAbodeOfferingState.ResolvePowerGainForSoulRelicOffering(relic.Rarity);
            var relicConfirm = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Поднести реликвию {Markup.Escape(relic.Name)} Обители {Markup.Escape(guardianNameChosen)}?[/]\n" +
                       $"[dim]Ожидаемый прирост силы: +{relicPowerGain}. Реликвия будет удалена из хранилища души.[/]")
                .AddChoices("✅ Да, поднести", "❌ Отмена"));
            if (relicConfirm.Contains("Отмена", StringComparison.Ordinal))
                return;

            var relicRequest = new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
            {
                GuardianId = guardianIdChosen,
                GuardianName = guardianNameChosen,
                OfferingType = GuardianAbodeOfferingState.OfferingTypeSoulRelic,
                RelicId = relic.RelicId,
                RelicName = relic.Name,
                RelicRarity = relic.Rarity,
                ReturnCycleId = returnCycleId
            };
            var relicLines = new List<string>
            {
                "[bold gold1]Подношение Реликвии Души Обители[/]",
                "",
                $"  Guardian: [white]{Markup.Escape(guardianNameChosen)}[/] [dim]({Markup.Escape(guardianIdChosen)})[/]",
                $"  Relic: [white]{Markup.Escape(relic.Name)}[/] [dim]({Markup.Escape(relic.RelicId)})[/]",
                $"  Rarity: [dim]{Markup.Escape(relic.Rarity)}[/]",
                $"  Power gain formula: [dim]ResolvePowerGainForSoulRelicOffering({Markup.Escape(relic.Rarity)}) = +{relicPowerGain}[/]",
                "",
                "[bold]Client-local pre-state change:[/]",
                "  • Реликвия будет изъята из soulRelics.stored до отправки GM.",
                "  • Pending request фиксирует offeringType=soul_relic и relic identity.",
                ""
            };
            relicLines.AddRange(BuildAbodeOfferingPreviewAuditLines(
                GuardianAbodeOfferingState.OfferingTypeSoulRelic,
                relic.Name,
                relic.RelicId,
                "soul_relic",
                relic.Rarity,
                currentAbodePowerChosen,
                relicPowerGain));
            relicLines.Add("");
            relicLines.Add("[bold]GM closure contract:[/]");
            relicLines.Add("  • guardianPowerEvents reasonType=offering, sourceSurface=guardianAbodeOffering.");
            relicLines.Add("  • audit: offeringType=soul_relic, relicId, relicName, relicRarity, returnCycleId, baseDelta, finalDelta.");
            relicLines.Add("  • guardian.abodePower.currentPower меняется только через power event.");
            AppendChaosSeaPendingFileRule(relicLines, GuardianAbodeOfferingState.PendingRequestPath);
            AppendChaosSeaCommonContractRules(relicLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр подношения реликвии",
                    relicLines,
                    ToChaosSeaAuditNode(relicRequest),
                    "Полный JSON pending abode offering request"))
            {
                return;
            }

            await EnsurePendingLocalTurnRollbackSnapshotAsync(
                "game_state/meta/soul_state.json",
                GuardianAbodeOfferingState.PendingRequestPath);

            try
            {
                await GuardianAbodeOfferingState.WriteAsync(_fs, relicRequest);
                if (!await RemoveStoredSoulRelicForOfferingLocal(relic.RelicId, relic.Name))
                {
                    await RestorePendingLocalTurnRollbackSnapshotAsync();
                    MarkupLine("[red]❌ Не удалось изъять реликвию из хранилища души.[/]");
                    WaitForKey();
                    return;
                }
            }
            catch
            {
                await RestorePendingLocalTurnRollbackSnapshotAsync();
                throw;
            }

            MarkupLine($"[green]✅ Реликвия «{Markup.Escape(relic.Name)}» подготовлена как подношение Обители.[/]");
            WaitForKey();

            _pendingGmAction =
                $"[ABODE_OFFERING] Игрок подносит Реликвию Души {relic.Name} ({relic.RelicId}, rarity={relic.Rarity}) Обители Хранителя {guardianNameChosen} ({guardianIdChosen}). " +
                $"Обязательно прочитай {GuardianAbodeOfferingState.PendingRequestPath} как client-authored contract. " +
                "Реликвия уже изъята клиентом из soulRelics.stored. " +
                "Запиши guardianPowerEvents с reasonType=offering и audit { offeringType=soul_relic, relicId, relicName, relicRarity, returnCycleId, baseDelta, finalDelta }. " +
                "Не меняй guardian.abodePower.currentPower напрямую без этого power event.";
            return;
        }

        if (offeringMode.StartsWith("📚", StringComparison.Ordinal) || offeringMode.StartsWith("🕯", StringComparison.Ordinal))
        {
            var targetEntryType = offeringMode.StartsWith("📚", StringComparison.Ordinal)
                ? AfterlifeArchiveState.EntryTypeLoreFragment
                : AfterlifeArchiveState.EntryTypeSecretRecord;
            var filteredEntries = archiveEntries
                .Where(entry => string.Equals(entry.EntryType, targetEntryType, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (filteredEntries.Count == 0)
            {
                ShowEmptyPanel("🏛 Подношение Обители", "В Архиве души нет подходящих записей для этого типа подношения.");
                return;
            }

            var archiveChoices = filteredEntries
                .Select(entry => $"{Markup.Escape(entry.Title)} [dim]({Markup.Escape(entry.Rarity)})[/]")
                .ToList();
            archiveChoices.Add("[dim]← Назад[/]");

            var selectedArchive = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Какую запись Архива души поднести Обители {Markup.Escape(guardianNameChosen)}?[/]")
                .PageSize(12)
                .AddChoices(archiveChoices));

            if (selectedArchive.Contains("Назад", StringComparison.Ordinal))
                return;

            var archiveIndex = archiveChoices.IndexOf(selectedArchive);
            if (archiveIndex < 0 || archiveIndex >= filteredEntries.Count)
                return;

            var archiveEntry = filteredEntries[archiveIndex];
            var archivePowerGain = AfterlifeArchiveState.ResolvePowerGainForArchiveRarity(archiveEntry.Rarity);
            var archiveConfirm = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Поднести запись «{Markup.Escape(archiveEntry.Title)}» Обители {Markup.Escape(guardianNameChosen)}?[/]\n" +
                       $"[dim]Ожидаемый прирост силы: +{archivePowerGain}. Запись будет удалена из Архива души.[/]")
                .AddChoices("✅ Да, поднести", "❌ Отмена"));
            if (archiveConfirm.Contains("Отмена", StringComparison.Ordinal))
                return;

            if (!AfterlifeArchiveState.TryGetOfferingTypeForEntryType(archiveEntry.EntryType, out var archiveOfferingType))
                return;

            var archiveRequest = new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
            {
                GuardianId = guardianIdChosen,
                GuardianName = guardianNameChosen,
                OfferingType = archiveOfferingType,
                ArchiveId = archiveEntry.ArchiveId,
                ArchiveTitle = archiveEntry.Title,
                ArchiveEntryType = archiveEntry.EntryType,
                ArchiveRarity = archiveEntry.Rarity,
                ReturnCycleId = returnCycleId
            };
            var archiveLines = new List<string>
            {
                "[bold gold1]Подношение записи Архива души Обители[/]",
                "",
                $"  Guardian: [white]{Markup.Escape(guardianNameChosen)}[/] [dim]({Markup.Escape(guardianIdChosen)})[/]",
                $"  Archive entry: [white]{Markup.Escape(archiveEntry.Title)}[/] [dim]({Markup.Escape(archiveEntry.ArchiveId)})[/]",
                $"  Type/rarity: [dim]{Markup.Escape(archiveEntry.EntryType)} / {Markup.Escape(archiveEntry.Rarity)}[/]",
                $"  offeringType: [dim]{Markup.Escape(archiveOfferingType)}[/]",
                $"  Power gain formula: [dim]ResolvePowerGainForArchiveRarity({Markup.Escape(archiveEntry.Rarity)}) = +{archivePowerGain}[/]",
                "",
                "[bold]Client-local pre-state change:[/]",
                "  • Запись будет изъята из soul_state.afterlifeArchive.stored до отправки GM.",
                "  • Pending request фиксирует archive identity, entryType, rarity и returnCycleId.",
                ""
            };
            archiveLines.AddRange(BuildAbodeOfferingPreviewAuditLines(
                archiveOfferingType,
                archiveEntry.Title,
                archiveEntry.ArchiveId,
                archiveEntry.EntryType,
                archiveEntry.Rarity,
                currentAbodePowerChosen,
                archivePowerGain));
            archiveLines.Add("");
            archiveLines.Add("[bold]GM closure contract:[/]");
            archiveLines.Add("  • guardianPowerEvents reasonType=offering, sourceSurface=guardianAbodeOffering.");
            archiveLines.Add("  • audit: offeringType, archiveId, archiveTitle, archiveEntryType, archiveRarity, returnCycleId, baseDelta, finalDelta.");
            archiveLines.Add("  • output/ink_feather_action_result.json не нужен, если offeringType не ink_feathers.");
            AppendChaosSeaPendingFileRule(archiveLines, GuardianAbodeOfferingState.PendingRequestPath);
            AppendChaosSeaCommonContractRules(archiveLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр подношения Архива",
                    archiveLines,
                    ToChaosSeaAuditNode(archiveRequest),
                    "Полный JSON pending abode offering request"))
            {
                return;
            }

            await EnsurePendingLocalTurnRollbackSnapshotAsync(
                "game_state/meta/soul_state.json",
                GuardianAbodeOfferingState.PendingRequestPath);

            try
            {
                await GuardianAbodeOfferingState.WriteAsync(_fs, archiveRequest);
                if (!await RemoveAfterlifeArchiveEntryForOfferingLocal(archiveEntry.ArchiveId, archiveEntry.Title))
                {
                    await RestorePendingLocalTurnRollbackSnapshotAsync();
                    MarkupLine("[red]❌ Не удалось изъять запись из Архива души.[/]");
                    WaitForKey();
                    return;
                }
            }
            catch
            {
                await RestorePendingLocalTurnRollbackSnapshotAsync();
                throw;
            }

            MarkupLine($"[green]✅ Запись «{Markup.Escape(archiveEntry.Title)}» подготовлена как подношение Обители.[/]");
            WaitForKey();

            _pendingGmAction =
                $"[ABODE_OFFERING] Игрок подносит запись Архива души {archiveEntry.Title} ({archiveEntry.ArchiveId}, type={archiveEntry.EntryType}, rarity={archiveEntry.Rarity}) Обители Хранителя {guardianNameChosen} ({guardianIdChosen}). " +
                $"Обязательно прочитай {GuardianAbodeOfferingState.PendingRequestPath} как client-authored contract. " +
                "Запись уже изъята клиентом из soul_state.afterlifeArchive.stored. " +
                "Запиши guardianPowerEvents с reasonType=offering и audit { offeringType, archiveId, archiveTitle, archiveEntryType, archiveRarity, returnCycleId, baseDelta, finalDelta }. " +
                "Не меняй guardian.abodePower.currentPower напрямую без этого power event.";
            return;
        }

        if (remainingCapChosen < 50)
        {
            ShowEmptyPanel("🏛 Подношение Обители", $"Для {guardianNameChosen} лимит подношений в этом возвращении уже исчерпан.");
            return;
        }

        var inputCost = Prompt(new TextPrompt<int>(
            $"[bold yellow]Сколько Перьев поднести Обители {Markup.Escape(guardianNameChosen)}? " +
            $"(у вас {feathers} 🪶, кратно 50, осталось по лимиту {remainingCapChosen} 🪶):[/]")
            .Validate(val =>
            {
                if (val < 50)
                    return ValidationResult.Error("[red]Минимум 50 Перьев[/]");
                if (val % 50 != 0)
                    return ValidationResult.Error("[red]Сумма должна быть кратна 50[/]");
                if (val > feathers)
                    return ValidationResult.Error($"[red]У вас только {feathers} 🪶[/]");
                if (val > remainingCapChosen)
                    return ValidationResult.Error($"[red]В этом возвращении можно поднести ещё только {remainingCapChosen} 🪶[/]");
                return ValidationResult.Success();
            }));

        var powerGain = GuardianAbodeOfferingState.ResolvePowerGainForInkFeatherOffering(inputCost);
        var confirm = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Поднести {inputCost} 🪶 Обители {Markup.Escape(guardianNameChosen)}?[/]\n" +
                   $"[dim]Ожидаемый прирост силы: +{powerGain}. Это отдельное приношение Обители, а не просто услуга Хранителя.[/]")
            .AddChoices("✅ Да, поднести", "❌ Отмена"));
        if (confirm.Contains("Отмена", StringComparison.Ordinal))
            return;

        var request = new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = guardianIdChosen,
            GuardianName = guardianNameChosen,
            OfferingType = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            InkFeathersOffered = inputCost,
            ReturnCycleId = returnCycleId
        };
        var featherLines = new List<string>
        {
            "[bold gold1]Подношение Чернильных Перьев Обители[/]",
            "",
            $"  Guardian: [white]{Markup.Escape(guardianNameChosen)}[/] [dim]({Markup.Escape(guardianIdChosen)})[/]",
            $"  Ink Feathers offered: [white]{inputCost}[/]",
            $"  Balance: [white]{feathers}[/] -> [white]{feathers - inputCost}[/]",
            $"  Return cycle: [dim]{Markup.Escape(returnCycleId)}[/]",
            $"  Cap remaining before: [dim]{remainingCapChosen}[/]",
            $"  Power gain formula: [dim]ResolvePowerGainForInkFeatherOffering({inputCost}) = +{powerGain}[/]",
            "",
            "[bold]Client-local pre-state change:[/]",
            "  • Ink Feathers списываются клиентом до отправки GM.",
            "  • Pending request фиксирует offeringType=ink_feathers и точную сумму.",
            ""
        };
        featherLines.AddRange(BuildAbodeOfferingPreviewAuditLines(
            GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            "Ink Feathers",
            inputCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "ink_feathers",
            "none",
            currentAbodePowerChosen,
            powerGain,
            feathers,
            inputCost,
            remainingCapChosen));
        featherLines.Add("");
        featherLines.Add("[bold]GM closure contract:[/]");
        featherLines.Add("  • guardianPowerEvents reasonType=offering, sourceSurface=guardianAbodeOffering.");
        featherLines.Add("  • output/ink_feather_action_result.json обязателен: actionTag=ABODE_OFFERING, resolved=true, costInFeathers, resolutionType=abodeOffering.");
        featherLines.Add("  • stateEvidence должен иметь powerGain, powerEventId, guardianId и affectedFiles.");
        AppendChaosSeaPendingFileRule(featherLines, GuardianAbodeOfferingState.PendingRequestPath);
        AppendChaosSeaCommonContractRules(featherLines);
        if (!ConfirmChaosSeaContractPreview(
                "Полный предпросмотр подношения Перьев",
                featherLines,
                ToChaosSeaAuditNode(request),
                "Полный JSON pending abode offering request"))
        {
            return;
        }

        await EnsurePendingLocalTurnRollbackSnapshotAsync(
            "game_state/meta/soul_state.json",
            GuardianAbodeOfferingState.PendingRequestPath);

        try
        {
            await GuardianAbodeOfferingState.WriteAsync(_fs, request);
            if (!await DeductInkFeathers(inputCost))
            {
                await RestorePendingLocalTurnRollbackSnapshotAsync();
                MarkupLine("[red]❌ Не удалось списать перья.[/]");
                WaitForKey();
                return;
            }
        }
        catch
        {
            await RestorePendingLocalTurnRollbackSnapshotAsync();
            throw;
        }

        MarkupLine($"[green]✅ Списано {inputCost} 🪶. Подношение Обители подготовлено для Мастера Игры.[/]");
        WaitForKey();

        _pendingGmAction =
            $"[INK_FEATHER_ACTION: {GuardianAbodeOfferingState.ActionTag}] Игрок подносит {inputCost} Чернильных Перьев Обители Хранителя {guardianNameChosen} ({guardianIdChosen}). " +
            $"Обязательно прочитай {GuardianAbodeOfferingState.PendingRequestPath} как client-authored contract. " +
            "Перья уже списаны клиентом. " +
            "Запиши guardianPowerEvents с reasonType=offering и audit { offeringType=ink_feathers, inkFeathersOffered, returnCycleId, capRemainingBefore, baseDelta, finalDelta }. " +
            "Не меняй guardian.abodePower.currentPower напрямую без этого power event. " +
            "Также обязательно запиши output/ink_feather_action_result.json с guardianId, powerGain, returnCycleId, powerEventId, resolved=true, resolutionType=abodeOffering, summary и stateEvidence. " +
            "stateEvidence должен содержать affectedFiles и минимальное подтверждение реального stateful результата.";
    }

    private sealed record AbodeOfferingRelic(
        string RelicId,
        string Name,
        string Rarity,
        string RaritySource,
        bool HasValidRarity,
        string RarityIssue);

    private async Task<List<AbodeOfferingRelic>> ReadStoredSoulRelicsForAbodeOfferingAsync()
    {
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        var result = new List<AbodeOfferingRelic>();
        if (soulDoc == null || soulDoc.RootElement.ValueKind != JsonValueKind.Object ||
            !soulDoc.RootElement.TryGetProperty("soulRelics", out var soulRelics))
        {
            return result;
        }

        if (soulRelics.ValueKind == JsonValueKind.Object &&
            soulRelics.TryGetProperty("stored", out var stored) &&
            stored.ValueKind == JsonValueKind.Array)
        {
            foreach (var relic in stored.EnumerateArray())
            {
                if (relic.ValueKind != JsonValueKind.Object)
                    continue;

                var relicId = GetRelicIdentity(relic);
                var name = GetStr(relic, "name", relicId);
                var rarity = ResolveSoulRelicOfferingRarity(relic, out var raritySource);
                var hasValidRarity = GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(rarity);
                var rarityIssue = hasValidRarity
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(rarity)
                        ? "missing rarity/quality/relicRarity"
                        : $"unsupported {raritySource}='{rarity}'";
                if (!string.IsNullOrWhiteSpace(relicId))
                    result.Add(new AbodeOfferingRelic(relicId, name, rarity, raritySource, hasValidRarity, rarityIssue));
            }
        }

        return result;
    }

    private static string ResolveSoulRelicOfferingRarity(JsonElement relic, out string sourceField)
    {
        foreach (var fieldName in new[] { "rarity", "quality", "relicRarity" })
        {
            if (!relic.TryGetProperty(fieldName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var rarity = value.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(rarity))
            {
                sourceField = fieldName;
                return rarity;
            }
        }

        sourceField = "rarity/quality/relicRarity";
        return string.Empty;
    }

    private async Task<List<AfterlifeArchiveEntrySummary>> ReadStoredAfterlifeArchiveEntriesAsync()
    {
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        var result = new List<AfterlifeArchiveEntrySummary>();
        if (soulDoc == null || soulDoc.RootElement.ValueKind != JsonValueKind.Object ||
            !soulDoc.RootElement.TryGetProperty("afterlifeArchive", out var archive) ||
            archive.ValueKind != JsonValueKind.Object ||
            !archive.TryGetProperty("stored", out var stored) ||
            stored.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in stored.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var archiveId = GetStr(entry, "archiveId", "");
            var title = GetStr(entry, "title", archiveId);
            var entryType = GetStr(entry, "entryType", "");
            var rarity = GetStr(entry, "rarity", "Common");
            var summary = GetStr(entry, "summary", "");
            var content = GetStr(entry, "content", "");
            var sourceLife = GetInt(entry, "sourceLife", 0);
            var sourceKind = GetStr(entry, "sourceKind", AfterlifeArchiveState.SourceKindSystem);
            var sourceEntryId = GetStr(entry, "sourceEntryId", "");
            var acquiredAtUtc = GetStr(entry, "acquiredAtUtc", "");
            var sourceGuardianId = GetStr(entry, "sourceGuardianId", "");
            var sourceGuardianName = GetStr(entry, "sourceGuardianName", "");
            var tags = entry.TryGetProperty("tags", out var tagsNode) && tagsNode.ValueKind == JsonValueKind.Array
                ? tagsNode.EnumerateArray()
                    .Where(tag => tag.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.GetString()))
                    .Select(tag => tag.GetString() ?? string.Empty)
                    .ToArray()
                : Array.Empty<string>();
            var isReserved = false;
            var reservationKind = string.Empty;
            var reservedForGuardianId = string.Empty;
            var reservedForGuardianName = string.Empty;
            var reservedForProjectId = string.Empty;
            var reservedForProjectName = string.Empty;
            if (entry.TryGetProperty("reservation", out var reservationNode) &&
                reservationNode.ValueKind == JsonValueKind.Object)
            {
                reservationKind = GetStr(reservationNode, "reservationKind", "");
                isReserved = AfterlifeArchiveState.IsSupportedReservationKind(reservationKind) &&
                             !string.IsNullOrWhiteSpace(GetStr(reservationNode, "requestId", ""));
                reservedForGuardianId = GetStr(reservationNode, "guardianId", "");
                reservedForGuardianName = GetStr(reservationNode, "guardianName", "");
                reservedForProjectId = GetStr(reservationNode, "targetProjectId", "");
                reservedForProjectName = GetStr(reservationNode, "targetProjectName", "");
            }

            if (!string.IsNullOrWhiteSpace(archiveId))
            {
                result.Add(new AfterlifeArchiveEntrySummary(
                    archiveId,
                    title,
                    entryType,
                    rarity,
                    summary,
                    content,
                    sourceLife,
                    sourceKind,
                    sourceEntryId,
                    acquiredAtUtc,
                    sourceGuardianId,
                    sourceGuardianName,
                    tags,
                    isReserved,
                    reservationKind,
                    reservedForGuardianId,
                    reservedForGuardianName,
                    reservedForProjectId,
                    reservedForProjectName));
            }
        }

        return result;
    }

    private async Task<bool> CanUseArchiveConsultationAsync(AfterlifeArchiveEntrySummary entry)
    {
        if (_afterlifeArchiveConsultationService == null ||
            !IsAfterlifeRealm(_stateManager.CurrentState.CurrentRealm) ||
            !AfterlifeArchiveState.IsAllowedEntryType(entry.EntryType) ||
            entry.IsReserved)
        {
            return false;
        }

        var pendingRequestState = await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs);
        if (pendingRequestState.Exists)
            return false;

        var guardians = await ReadGuardiansForArchiveOperationAsync(entry);
        return guardians.Count > 0;
    }

    private async Task<bool> CanUseArchiveProjectFuelAsync(AfterlifeArchiveEntrySummary entry)
    {
        if (_afterlifeArchiveProjectFuelService == null ||
            !IsAfterlifeRealm(_stateManager.CurrentState.CurrentRealm) ||
            entry.IsReserved)
        {
            return false;
        }

        var pendingRequestState = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs);
        if (pendingRequestState.Exists)
            return false;

        var guardians = await ReadGuardiansForArchiveOperationAsync(entry);
        return guardians.Any(item => item.FuelAvailable);
    }

    private async Task<bool> StartArchiveConsultationAsync(AfterlifeArchiveEntrySummary entry)
    {
        if (_afterlifeArchiveConsultationService == null)
            return false;

        if (!IsAfterlifeRealm(_stateManager.CurrentState.CurrentRealm))
        {
            MarkupLine("[yellow]⚠️ Архивная консультация доступна только в загробном цикле.[/]");
            return false;
        }

        var consultationState = await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs);
        if (consultationState.Exists)
        {
            MarkupLine(consultationState.IsMalformed
                ? "[red]❌ Файл запроса архивной консультации повреждён или неполон. Новый запрос заблокирован, пока эта запись не будет исправлена или очищена.[/]"
                : "[yellow]⚠️ Уже есть незакрытый запрос на архивную консультацию. Дождитесь ответа GM.[/]");
            return false;
        }

        var guardians = await ReadGuardiansForArchiveOperationAsync(entry);
        if (guardians.Count == 0)
        {
            MarkupLine("[yellow]⚠️ Сейчас нет дружественных Хранителей, у которых можно провести архивную консультацию.[/]");
            return false;
        }

        var guardianChoices = guardians
            .Select(choice => ConsoleLayout.PlainChoiceLabel(
                $"🛡️ {choice.GuardianName}",
                $"репутация {choice.Reputation} • {GuardianTradeDisplayDomain(choice.Domain)}",
                "Результат будет явно оформлен GM по разрешённым исходам консультации",
                string.Equals(entry.EntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase)
                    ? "возможны заметные подсказки о нити соперника или предупреждения"
                    : "возможны гарантированный архивный квест или подготовка знания"))
            .ToList();
        guardianChoices.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Кто осмыслит запись «{Markup.Escape(entry.Title)}»?[/]")
            .PageSize(12)
            .HighlightStyle(new Style(Color.Yellow))
            .AddChoices(guardianChoices));

        if (selected.Contains("←", StringComparison.Ordinal))
            return false;

        var index = guardianChoices.IndexOf(selected);
        if (index < 0 || index >= guardians.Count)
            return false;

        var guardian = guardians[index];
        var confirm = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Провести архивную консультацию у {Markup.Escape(guardian.GuardianName)}?[/]\n" +
                   "[dim]Запись будет зарезервирована в Архиве души до ответа GM. При положительном ответе она будет потрачена; при отказе или отмене снова станет доступной.[/]")
            .AddChoices("✅ Да, провести консультацию", "❌ Отмена"));

        if (confirm.Contains("Отмена", StringComparison.Ordinal))
            return false;

        var result = await _afterlifeArchiveConsultationService.CreateRequestAsync(
            guardian.GuardianId,
            guardian.GuardianName,
            entry.ArchiveId,
            Math.Max(1, _stateManager.CurrentState.Incarnation),
            _stateManager.CurrentState.CurrentRealm,
            await TryReadCurrentTurnNumberAsync(),
            commit: false);

        if (result == null)
        {
            MarkupLine("[red]❌ Не удалось провести архивную консультацию.[/]");
            return false;
        }

        var consultationLines = new List<string>
        {
            "[bold yellow]Архивная консультация[/]",
            "",
            $"  Guardian: [white]{Markup.Escape(result.GuardianName)}[/] [dim]({Markup.Escape(result.GuardianId)})[/]",
            $"  Archive entry: [white]{Markup.Escape(result.ArchiveTitle)}[/] [dim]({Markup.Escape(result.ArchiveId)})[/]",
            $"  Type: [dim]{Markup.Escape(result.ArchiveEntryType)}[/]",
            $"  Target incarnation: [dim]{result.TargetIncarnation}[/]",
            "",
            "[bold]Client-local pre-state change:[/]",
            "  • Запись резервируется в soul_state.afterlifeArchive.stored до ответа GM.",
            "  • Pending request фиксирует requestedMode=consultation.",
            "",
            "[bold]GM closure contract:[/]",
            "  • archiveActionResolutions с requestId, archiveId, requestedMode=consultation, guardianId, status.",
            "  • accepted consultation требует machine-readable outcome fields.",
            "  • lore_fragment whitelist: guaranteedArchiveQuestCount, questHookCount, specialQuestLineUnlocks.",
            "  • secret_record whitelist: visibleRivalClueBonus, archiveWarningTierBonus.",
            "  • Отказ или отмена возвращают запись в Архив."
        };
        AppendChaosSeaPendingFileRule(consultationLines, result.PendingRequestPath);
        AppendChaosSeaCommonContractRules(consultationLines);
        if (!ConfirmChaosSeaContractPreview(
                "Полный предпросмотр архивной консультации",
                consultationLines,
                JsonNode.Parse(result.PendingRequestJson),
                "Полный JSON pending archive consultation request"))
        {
            return false;
        }

        if (!await _afterlifeArchiveConsultationService.CommitPreparedRequestAsync(result))
        {
            MarkupLine("[red]❌ Не удалось безопасно зафиксировать архивную консультацию.[/]");
            return false;
        }

        _pendingGmAction = result.PendingGmAction;
        MarkupLine($"[green]✅ Создан запрос на архивную консультацию у {Markup.Escape(result.GuardianName)}.[/]");
        MarkupLine($"[dim]Целевая жизнь: #{result.TargetIncarnation}. {Markup.Escape(result.Summary)}[/]");
        return true;
    }

    private async Task<bool> StartArchiveProjectFuelAsync(AfterlifeArchiveEntrySummary entry)
    {
        if (_afterlifeArchiveProjectFuelService == null)
            return false;

        if (!IsAfterlifeRealm(_stateManager.CurrentState.CurrentRealm))
        {
            MarkupLine("[yellow]⚠️ Архивное вложение в проект доступно только в загробном цикле.[/]");
            return false;
        }

        var projectFuelState = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs);
        if (projectFuelState.Exists)
        {
            MarkupLine(projectFuelState.IsMalformed
                ? "[red]❌ Файл ожидающего запроса на архивную подпитку проекта повреждён или неполон. Новый запрос заблокирован, пока состояние не будет исправлено или очищено.[/]"
                : "[yellow]⚠️ Уже есть незакрытый запрос на архивную подпитку проекта. Дождитесь ответа GM.[/]");
            return false;
        }

        var guardians = (await ReadGuardiansForArchiveOperationAsync(entry))
            .Where(item => item.FuelAvailable)
            .ToList();
        if (guardians.Count == 0)
        {
            MarkupLine("[yellow]⚠️ Сейчас нет дружественных Хранителей с активным проектом для архивной подпитки.[/]");
            return false;
        }

        var guardianChoices = guardians
            .Select(choice => ConsoleLayout.PlainChoiceLabel(
                $"⚙️ {choice.GuardianName}",
                $"репутация {choice.Reputation} • {GuardianTradeDisplayDomain(choice.Domain)}",
                $"целевой проект: {choice.TargetProjectName}",
                string.Equals(entry.EntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase)
                    ? "GM явно оформит ослабление давления на проект"
                    : "GM явно оформит ускорение работы над проектом"))
            .ToList();
        guardianChoices.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Кто вложит запись «{Markup.Escape(entry.Title)}» в активный проект?[/]")
            .PageSize(12)
            .HighlightStyle(new Style(Color.Yellow))
            .AddChoices(guardianChoices));

        if (selected.Contains("←", StringComparison.Ordinal))
            return false;

        var index = guardianChoices.IndexOf(selected);
        if (index < 0 || index >= guardians.Count)
            return false;

        var guardian = guardians[index];
        var confirm = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Вложить запись в проект {Markup.Escape(guardian.GuardianName)}?[/]\n" +
                   "[dim]Запись будет зарезервирована в Архиве души до ответа GM. При положительном ответе она будет потрачена; при отказе или отмене снова станет доступной.[/]")
            .AddChoices("✅ Да, вложить", "❌ Отмена"));

        if (confirm.Contains("Отмена", StringComparison.Ordinal))
            return false;

        var result = await _afterlifeArchiveProjectFuelService.CreateRequestAsync(
            guardian.GuardianId,
            guardian.GuardianName,
            entry.ArchiveId,
            _stateManager.CurrentState.CurrentRealm,
            await TryReadCurrentTurnNumberAsync(),
            commit: false);

        if (result == null)
        {
            MarkupLine("[red]❌ Не удалось вложить запись в проект.[/]");
            return false;
        }

        var fuelLines = new List<string>
        {
            "[bold yellow]Архивная подпитка проекта[/]",
            "",
            $"  Guardian: [white]{Markup.Escape(result.GuardianName)}[/] [dim]({Markup.Escape(result.GuardianId)})[/]",
            $"  Project: [white]{Markup.Escape(result.ProjectName)}[/] [dim]({Markup.Escape(result.ProjectId)})[/]",
            $"  Archive entry: [white]{Markup.Escape(result.ArchiveTitle)}[/] [dim]({Markup.Escape(result.ArchiveId)})[/]",
            "",
            "[bold]Client-local pre-state change:[/]",
            "  • Запись резервируется в soul_state.afterlifeArchive.stored до ответа GM.",
            "  • Pending request фиксирует requestedMode=project_fuel и targetProjectId.",
            "",
            "[bold]GM closure contract:[/]",
            "  • archiveActionResolutions с requestId, archiveId, requestedMode=project_fuel, guardianId, targetProjectId, status.",
            "  • Принятый результат должен иметь resultMode и resultAmount>0.",
            "  • lore_fragment разрешён только project_work.",
            "  • secret_record разрешён только pressure_relief.",
            "  • journal entry eventType=assisted должен сохранить archiveFuelRequestId и archiveId."
        };
        AppendChaosSeaPendingFileRule(fuelLines, result.PendingRequestPath);
        AppendChaosSeaCommonContractRules(fuelLines);
        if (!ConfirmChaosSeaContractPreview(
                "Полный предпросмотр архивной подпитки проекта",
                fuelLines,
                JsonNode.Parse(result.PendingRequestJson),
                "Полный JSON ожидающего запроса подпитки проекта Архивом"))
        {
            return false;
        }

        if (!await _afterlifeArchiveProjectFuelService.CommitPreparedRequestAsync(result))
        {
            MarkupLine("[red]❌ Не удалось безопасно зафиксировать архивную подпитку проекта.[/]");
            return false;
        }

        _pendingGmAction = result.PendingGmAction;
        MarkupLine($"[green]✅ Создан запрос на архивную подпитку проекта {Markup.Escape(result.ProjectName)}.[/]");
        MarkupLine($"[dim]{Markup.Escape(result.Summary)}[/]");
        return true;
    }

    private async Task<List<FriendlyGuardianConsultationChoice>> ReadGuardiansForArchiveOperationAsync(AfterlifeArchiveEntrySummary entry)
    {
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc == null || guardiansDoc.RootElement.ValueKind != JsonValueKind.Object)
            return new List<FriendlyGuardianConsultationChoice>();

        using var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        return CollectGuardianDisplayEntries(guardiansDoc.RootElement)
            .Select(guardian =>
            {
                var guardianId = GetStr(guardian, "guardianId", "");
                var rep = guardian.TryGetProperty("relationshipData", out var relationshipData) && relationshipData.ValueKind == JsonValueKind.Object
                    ? GetInt(relationshipData, "currentReputation", GetInt(guardian, "reputation", 0))
                    : GetInt(guardian, "reputation", 0);
                var guardianName = GuardianManifestation.GetDisplayName(guardian);
                if (string.IsNullOrWhiteSpace(guardianName))
                    guardianName = guardianId;
                var domain = GetStr(guardian, "domain", "—");
                var (fuelAvailable, targetProjectId, targetProjectName) = ResolveArchiveFuelTarget(trackerDoc?.RootElement, guardianId);

                return new FriendlyGuardianConsultationChoice(
                    guardianId,
                    guardianName,
                    rep,
                    domain,
                    fuelAvailable,
                    targetProjectId,
                    targetProjectName);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.GuardianId) && item.Reputation >= 50)
            .OrderByDescending(item => item.Reputation)
            .ThenBy(item => item.GuardianName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (bool available, string targetProjectId, string targetProjectName) ResolveArchiveFuelTarget(JsonElement? trackerRoot, string guardianId)
    {
        if (!trackerRoot.HasValue ||
            trackerRoot.Value.ValueKind != JsonValueKind.Object ||
            !trackerRoot.Value.TryGetProperty("activeProjects", out var activeProjects) ||
            activeProjects.ValueKind != JsonValueKind.Array)
        {
            return (false, string.Empty, string.Empty);
        }

        foreach (var entry in activeProjects.EnumerateArray())
        {
            if (!string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            return (
                true,
                GetStr(project, "projectId", ""),
                GetStr(project, "projectName", GetStr(project, "projectId", "")));
        }

        return (false, string.Empty, string.Empty);
    }

    private async Task<int> TryReadCurrentTurnNumberAsync()
    {
        var raw = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("turnNumber", out var turnNode) &&
                turnNode.ValueKind == JsonValueKind.Number &&
                turnNode.TryGetInt32(out var turn))
            {
                return turn;
            }
        }
        catch
        {
            // ignored
        }

        return 0;
    }

    private static bool IsAfterlifeRealm(string? realm) => RealmSemantics.IsAfterlifeRealm(realm);

    private async Task SyncAfterlifeNotificationsAsync()
    {
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
    }

    private async Task<bool> RemoveStoredSoulRelicForOfferingLocal(string relicId, string relicName)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null)
            return false;

        try
        {
            var node = JsonNode.Parse(json);
            var storedArr = node?["soulRelics"]?["stored"]?.AsArray();
            if (storedArr == null)
                return false;

            for (var i = 0; i < storedArr.Count; i++)
            {
                if (!RelicNodeMatches(storedArr[i], relicId, relicName))
                    continue;

                storedArr.RemoveAt(i);
                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
                await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private async Task<bool> RemoveAfterlifeArchiveEntryForOfferingLocal(string archiveId, string archiveTitle)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null)
            return false;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject root)
                return false;

            AfterlifeArchiveState.NormalizeShape(root);
            var storedArr = root["afterlifeArchive"]?["stored"]?.AsArray();
            if (storedArr == null)
                return false;

            for (var i = 0; i < storedArr.Count; i++)
            {
                if (storedArr[i] is not JsonObject entry)
                    continue;

                var currentArchiveId = entry["archiveId"]?.GetValue<string>() ?? string.Empty;
                if (!string.Equals(currentArchiveId, archiveId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (AfterlifeArchiveState.IsReserved(entry))
                    return false;

                storedArr.RemoveAt(i);
                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
                await _fs.WriteFileAtomicAsync(path, root.ToJsonString(opts));
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
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

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🎰 Гача ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1)
        };

        Write(panel);
        WriteLine();

        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (soulDoc != null)
            WriteJsonAuditPanel("Полный JSON состояния души перед direct gacha", soulDoc.RootElement, Color.Gold1);

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc != null)
            WriteJsonAuditPanel("Полный JSON guardians gacha systems", guardiansDoc.RootElement, Color.Gold1);

        var choice = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Выберите действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(
                "🎰 Вытянуть реликвию из Моря Хаоса",
                "← Назад"));

        if (choice.Contains("Назад"))
            return;

        if (feathers <= 0)
        {
            MarkupLine("[yellow]⚠️ У вас нет Чернильных Перьев для прямого вытягивания.[/]");
            WaitForKey();
            return;
        }

        var inputCost = Prompt(new TextPrompt<int>(
            $"[bold yellow]Сколько Перьев потратить на прямое вытягивание? (у вас {feathers} 🪶):[/]")
            .Validate(val =>
            {
                if (val <= 0) return ValidationResult.Error("[red]Нужно потратить хотя бы 1 перо[/]");
                if (val > feathers) return ValidationResult.Error($"[red]У вас только {feathers} 🪶[/]");
                return ValidationResult.Success();
            }));

        var pendingState = _pendingTurnState != null
            ? await _pendingTurnState.GetOrCreateAsync()
            : null;
        var gacha = pendingState?.GachaBaseResult ?? new GachaResult();
        var baseRarity = string.IsNullOrWhiteSpace(gacha.BaseRarity) ? "Common" : gacha.BaseRarity;
        var rarityColor = GetRarityColor(baseRarity);
        var mechanicsLines = new List<string>
        {
            "[bold yellow]Прямая гача Моря Хаоса[/]",
            "",
            $"Цена будет передана GM строго как фраза: [yellow]{inputCost} Чернильных Перьев[/].",
            "Эта фраза обязательна: валидатор извлекает из неё prepaid cost direct /gacha.",
            "",
            "[bold]Базовый результат текущего хода:[/]",
            $"  • baseScore: [cyan]{gacha.BaseScore}[/]",
            $"  • baseRarity: [{rarityColor}]{Markup.Escape(DescribeRarityLabel(baseRarity))}[/]",
            $"  • formula: [dim]{Markup.Escape(gacha.Formula ?? "client-computed gacha base (range 4-80)")}[/]",
            "",
            "Пороги: 4-48 Common, 49-67 Uncommon, 68-75 Rare, 76-79 Epic, 80 Legendary.",
            "Итог direct /gacha: finalRarity должен точно совпасть с baseRarity. Апгрейдов, даунгрейдов и guardian modifiers нет.",
            "GM материализует ровно одну новую Soul Relic, не удаляет существующие реликвии и не списывает Чернильные Перья второй раз.",
            "",
            "[bold]Before/after authority:[/]",
            $"  • Чернильные Перья: {feathers} -> {feathers - inputCost}; это уже сделает клиент до отправки GM.",
            "  • Soul Relics before: validator берёт pre-turn snapshot из game_state/control/pending_turn_snapshot.",
            "  • Soul Relics after: ровно один новый relicId в soulRelics.stored/equipped; unrelated soul fields unchanged.",
            "  • Accepted response proof: metaStateUpdates.soulRelicOperations.addRelic plus direct_chaos_gacha_result receipt data."
        };
        AppendChaosSeaCommonContractRules(mechanicsLines);
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", mechanicsLines)))
        {
            Header = new PanelHeader(" Механика direct /gacha ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel(
            "Полный JSON direct Chaos Sea gacha contract",
            BuildChaosSeaDirectActionAudit(
                "CHAOS_SEA_DIRECT_GACHA",
                $"[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит {inputCost} Чернильных Перьев.",
                ("costInFeathers", inputCost),
                ("currentFeathers", feathers),
                ("projectedFeathers", feathers - inputCost),
                ("baseScore", gacha.BaseScore),
                ("baseRarity", baseRarity),
                ("formula", gacha.Formula ?? "client-computed gacha base (range 4-80)"),
                ("snapshotAuthorityPath", "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"),
                ("acceptedPreTurnAuthority", "validated snapshot soul_state before GM output"),
                ("expectedSoulRelicDelta", "append exactly one new Soul Relic; do not delete, replace, or mutate existing relics"),
                ("forbiddenSurfaces", new JsonArray
                {
                    "TriggerLifeEnd",
                    "TriggerIncarnation",
                    "currentLocationData",
                    "worldEventsLog",
                    "UpdateNPCs",
                    "Mortal World factions",
                    "second Ink Feather spend"
                })),
            Color.Gold1);

        var costDisplay = $"{inputCost} 🪶 (останется {feathers - inputCost})";
        var confirm = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Прямое вытягивание из Моря Хаоса[/]\n" +
                   $"[dim]Текущий Хранитель не участвует. Модификаторы будут нейтральными. GM получит точную cost-фразу: {inputCost} Чернильных Перьев.[/]\n" +
                   $"[bold]Потратить {Markup.Escape(costDisplay)} на вытягивание реликвии?[/]")
            .AddChoices("✅ Да, тянуть", "❌ Отмена"));
        if (confirm.Contains("Отмена"))
            return;

        await EnsurePendingLocalTurnRollbackSnapshotAsync("game_state/meta/soul_state.json");
        if (!await DeductInkFeathers(inputCost))
        {
            await DiscardPendingLocalTurnRollbackSnapshotAsync();
            MarkupLine("[red]❌ Не удалось списать перья.[/]");
            WaitForKey();
            return;
        }

        MarkupLine($"[green]✅ Списано {inputCost} 🪶. Вы вытягиваете реликвию напрямую из Моря Хаоса.[/]");
        WaitForKey();

        _pendingGmAction =
            $"[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит {inputCost} Чернильных Перьев. " +
            "Это НЕ гача через текущего Хранителя: не применять репутацию Хранителя, его скидки, штрафы, социальные факторы, улучшенные или ухудшенные шансы. " +
            "Результат должен быть нейтральным: finalRarity обязан точно совпадать с turn_request.gachaBaseResult.baseRarity, без апгрейдов или даунгрейдов. " +
            "Реликвию нужно добавить напрямую в soul state игрока через metaStateUpdates.soulRelicOperations.addRelic как ровно одну новую Soul Relic; существующие реликвии не удалять. Перья уже списаны клиентом, GM не списывает их второй раз.";
    }

    // ═══ New commands: Effects, Combat, Weather/Time, Chronicle ═══
}

