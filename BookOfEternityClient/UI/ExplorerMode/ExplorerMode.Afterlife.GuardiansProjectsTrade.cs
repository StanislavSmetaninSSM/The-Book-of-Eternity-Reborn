using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private const int GuardianThoughtPreviewCount = 3;
    private const int GuardianSocialPreviewCount = 5;

    private static List<string> ReadSoulPreviousNames(JsonElement root, string currentSoulName)
    {
        if (!root.TryGetProperty("previousSoulNames", out var previousSoulNames) ||
            previousSoulNames.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in previousSoulNames.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
                continue;

            var value = entry.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (string.Equals(value, currentSoulName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add(value))
                continue;

            names.Add(value);
        }

        return names;
    }

    private async Task ShowGuardians()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable(_loc.T("guardians_info")))
            return;

        while (true)
        {
            await SyncAfterlifeNotificationsAsync();
            var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
            var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
            if (doc == null)
            {
                ShowEmptyPanel(_loc.T("guardians_info"), "Данные хранителей недоступны");
                return;
            }

            var root = doc.RootElement;
            var guardians = CollectGuardianDisplayEntries(root);
            var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
            if (guardians.Count == 0)
            {
                ShowEmptyPanel(_loc.T("guardians_info"), "Хранители ещё не найдены");
                return;
            }

            var currentAbodeId = "";
            var activeGuardianId = "";
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("chaosSeaNavigation", out var nav) && nav.ValueKind == JsonValueKind.Object)
                currentAbodeId = GetStr(nav, "currentAbodeId", "");
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
                activeGuardianId = GetStr(activeGuardian, "guardianId", "");
            var activeGuardianDisplayName = guardians
                .Where(g => string.Equals(GetStr(g, "guardianId", ""), activeGuardianId, StringComparison.OrdinalIgnoreCase))
                .Select(GuardianManifestation.GetDisplayName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? string.Empty;

            var choices = guardians.Select(g =>
            {
                var name = GuardianManifestation.GetDisplayName(g);
                if (string.IsNullOrWhiteSpace(name))
                    name = "?";
                var isActiveGuardian = string.Equals(GetStr(g, "guardianId", ""), activeGuardianId, StringComparison.OrdinalIgnoreCase);
                var guardianId = GetStr(g, "guardianId", "");
                var domain = GetStr(g, "domain", "");
                int rep = 0;
                if (g.TryGetProperty("relationshipData", out var rd))
                    rep = GetInt(rd, "currentReputation", 0);
                else
                    rep = GetInt(g, "reputation", 0);
                var repTierTag = ReputationDisplay.GetTier(ReputationScaleKind.Guardian, rep).Label;

                // Abode info
                var abodeName = "";
                var abodeId = "";
                if (g.TryGetProperty("abode", out var ab) && ab.ValueKind == JsonValueKind.Object)
                {
                    abodeName = GetStr(ab, "name", "");
                    abodeId = GetStr(ab, "abodeId", "");
                }
                var isCurrent = !string.IsNullOrEmpty(abodeId) && abodeId == currentAbodeId;
                var locTag = isCurrent ? "ТУТ" : "";

                var domainRu = domain switch
                {
                    "Combat" => "Бой",
                    "Magic" => "Магия",
                    "Trade" => "Торговля",
                    "Social" => "Общение",
                    "Crafting" => "Ремесло",
                    "Survival" => "Выживание",
                    "Knowledge" => "Знания",
                    _ => domain
                };

                // Mood tag in list
                var moodTag = "";
                if (g.TryGetProperty("mood", out var moodEl) && moodEl.ValueKind == JsonValueKind.Object)
                {
                    var moodVal = GetStr(moodEl, "current", "");
                    var moodIcon = moodVal.ToLowerInvariant() switch
                    {
                        "welcoming" => "🤗", "contemplative" => "🤔", "energized" => "⚡",
                        "melancholic" => "😔", "irritated" => "😤", "proud" => "😊",
                        "suspicious" => "🧐", "playful" => "😏", "focused" => "🎯",
                        "nostalgic" => "🕰️", _ => ""
                    };
                    if (!string.IsNullOrEmpty(moodIcon)) moodTag = moodIcon;
                }

                return ConsoleLayout.PlainChoiceLabel(
                    $"🛡️ {name}",
                    isActiveGuardian ? "АКТИВНЫЙ" : "",
                    domainRu,
                    $"♥ {rep}",
                    repTierTag,
                    string.IsNullOrEmpty(moodTag) ? "" : moodTag,
                    string.IsNullOrEmpty(abodeName) ? "" : $"🏛 {abodeName}",
                    string.IsNullOrEmpty(guardianId) ? "" : $"guardianId={guardianId}",
                    string.IsNullOrEmpty(abodeId) ? "" : $"abodeId={abodeId}",
                    locTag);
            }).ToList();

            // Navigation options
            if (isChaosSea)
            {
                choices.Add("🔍 Искать новую обитель (силой мысли)");
                choices.Add("👑 Учредить собственного Хранителя");
            }
            choices.Add("← Назад");

            // Pending guardian creation notice
            string pendingNotice = "";
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("pendingGuardianCreation", out var pending) &&
                pending.ValueKind == JsonValueKind.Object)
            {
                var pendDesc = GetStr(pending, "description", "");
                pendingNotice = "  [yellow]⏳ Ожидается создание нового хранителя[/]" +
                    (!string.IsNullOrEmpty(pendDesc) ? $"\n  [dim]{Markup.Escape(pendDesc)}[/]" : "");
            }

            // Pending discovery notice
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("chaosSeaNavigation", out var nav2) && nav2.ValueKind == JsonValueKind.Object &&
                nav2.TryGetProperty("pendingDiscovery", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                var hint = GetStr(pd, "hint", "");
                var arrIn = GetInt(pd, "arrivalInTurns", 0);
                pendingNotice += $"\n  [cyan]🌊 Ощущаете далёкий зов...{(arrIn > 0 ? $" (через {arrIn} ход.)" : "")}[/]" +
                    (!string.IsNullOrEmpty(hint) ? $"\n  [dim italic]{Markup.Escape(hint)}[/]" : "");
            }

            if (_systemGuardianLibraryService != null)
            {
                var attractionState = await _systemGuardianLibraryService.ReadAttractionRequestDisplayStateAsync();
                if (attractionState.IsMalformed)
                {
                    pendingNotice += "\n  [red]🧲 Притяжение к извечному Хранителю повреждено: pending contract нужно исправить или очистить вручную.[/]";
                }
                else if (attractionState.Request != null)
                {
                    pendingNotice += $"\n  [magenta1]🧲 Задано притяжение к извечному Хранителю: {Markup.Escape(attractionState.Request.TargetPresetDisplayName)}[/]";
                }
            }

            var foundationContext = await PlayerGuardianFoundationState.ReadContextAsync(_fs);
            if (!string.IsNullOrWhiteSpace(activeGuardianDisplayName))
                pendingNotice = $"  [green]🧭 Активный Хранитель: {Markup.Escape(activeGuardianDisplayName)}[/]" +
                    (string.IsNullOrEmpty(pendingNotice) ? "" : $"\n{pendingNotice}");
            if (foundationContext.PendingRequest != null)
            {
                pendingNotice += $"\n  [gold1]👑 Готовится основание собственной мантии: {Markup.Escape(foundationContext.PendingRequest.ProposedDisplayName)}[/]";
            }
            else if (!string.IsNullOrWhiteSpace(foundationContext.ExistingFoundedGuardianName))
            {
                pendingNotice += $"\n  [gold1]👑 Основанный Хранитель: {Markup.Escape(foundationContext.ExistingFoundedGuardianName)}[/]";
                if (!string.IsNullOrWhiteSpace(foundationContext.FormerPatronGuardianName))
                    pendingNotice += $"\n  [dim]Прежний покровитель: {Markup.Escape(foundationContext.FormerPatronGuardianName)}[/]";
                if (!string.IsNullOrWhiteSpace(foundationContext.ExistingFoundedGuardianAbodeName))
                    pendingNotice += $"\n  [dim]Текущая Обитель основанной мантии: {Markup.Escape(foundationContext.ExistingFoundedGuardianAbodeName)}[/]";
                if (foundationContext.ExistingFoundedGuardianExtraGachaChargesPerReturn > 0)
                    pendingNotice += $"\n  [dim]Бонус основания: +{foundationContext.ExistingFoundedGuardianExtraGachaChargesPerReturn} доп. попытка гачи за возвращение[/]";
                if (!string.IsNullOrWhiteSpace(foundationContext.ExistingFoundedGuardianFeatureTitle))
                    pendingNotice += $"\n  [dim]Дар основания: {Markup.Escape(foundationContext.ExistingFoundedGuardianFeatureTitle)}[/]";
                if (!string.IsNullOrWhiteSpace(foundationContext.FormerPatronGuardianName))
                    pendingNotice += $"\n  [dim]Продолжение линии прежнего покровителя остаётся за GM и может проявиться в обычных загробных событиях.[/]";
                pendingNotice += "\n  [dim]Ветка основания завершена и остаётся одноразовой для этого сохранения.[/]";
            }
            else if (foundationContext.HasCompletedFoundation)
            {
                pendingNotice += "\n  [gold1]👑 Ветка основания завершена: в этом сохранении уже основан собственный Хранитель.[/]";
            }

            var unreadTradeNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianTradeInventoryReady, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (unreadTradeNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]📬 Непрочитанные ответы по торговле:[/]";
                foreach (var notification in unreadTradeNotifications)
                    pendingNotice += $"\n  [dim]• {Markup.Escape(FormatAfterlifeNotificationInline(notification))}[/]";
            }

            var unreadGuardianQuestNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (unreadGuardianQuestNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]📜 Новые квесты Хранителей:[/]";
                foreach (var notification in unreadGuardianQuestNotifications)
                    pendingNotice += $"\n  [dim]• {Markup.Escape(FormatAfterlifeNotificationInline(notification))}[/]";
            }

            var unreadResidentNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentsReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentRelicGranted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentManifestationReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeCompanionImprintManifestationReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentWavering, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentRestless, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentConsideringDeparture, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferPending, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferAccepted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferRefused, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferDeparted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTalkAnswered, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRefused, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unreadResidentNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]🏛 События Обители:[/]";
                foreach (var notification in unreadResidentNotifications)
                    pendingNotice += $"\n  [dim]• {Markup.Escape(FormatAfterlifeNotificationInline(notification))}[/]";
            }

            var overviewLines = new List<string>
            {
                isChaosSea
                    ? "[bold cyan]Море Хаоса: оперативный обзор[/]"
                    : "[bold cyan]Сияющая Обитель: обзор Хранителей только для чтения[/]",
                "",
                $"  • Активный Хранитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(activeGuardianDisplayName) ? activeGuardianId : activeGuardianDisplayName)}[/] [dim]({Markup.Escape(activeGuardianId)})[/]",
                $"  • Текущая Обитель: [white]{Markup.Escape(string.IsNullOrWhiteSpace(currentAbodeId) ? "не выбрана" : currentAbodeId)}[/]",
                $"  • Известных Хранителей: [white]{guardians.Count}[/]",
                "",
                "[bold]Куда идти дальше:[/]",
                "  • /status — единый статус ресурсов посмертия, блокеров, контрактов и сигналов Сияющей Обители.",
                "  • /afterlife_inbox — все ответы ГМ по торговле, архиву, резидентам и политике.",
                "  • /feathers, /afterlife_archive, /guardian_projects, /guardian_politics, /abode_offering — детальные ресурсы и изменения состояния."
            };
            if (!isChaosSea)
            {
                overviewLines.Add("");
                overviewLines.Add("[yellow]Действия только Моря Хаоса скрыты:[/] поиск новой Обители и основание собственного Хранителя доступны только в обычном Море Хаоса.");
            }
            overviewLines.Add("");
            overviewLines.AddRange(await BuildAfterlifePendingContractAuditLinesAsync(includeShining: true, includeFullPayload: false));
            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", overviewLines)))
            {
                Header = new PanelHeader(" 🌊 Море Хаоса ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🛡️ {_loc.T("guardians_info")} — Обители Моря Хаоса[/]" +
                    (string.IsNullOrEmpty(pendingNotice) ? "" : $"\n{pendingNotice}"))
                .PageSize(20)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            if (isChaosSea && selected.Contains("Искать новую обитель"))
            {
                await ShowSearchAbodePrompt();
                if (_pendingGmAction != null)
                    return;
                continue;
            }

            if (isChaosSea && selected.Contains("Учредить собственного Хранителя"))
            {
                await ShowPlayerGuardianFoundationAsync();
                if (_pendingGmAction != null)
                    return;
                continue;
            }

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= guardians.Count) break;

            await ShowGuardianDetailPanel(guardians[selIdx], guardians, currentAbodeId, activeGuardianId, trackerDoc?.RootElement);
            if (_pendingGmAction != null)
                return;
        }
    }

    private async Task ShowSearchAbodePrompt()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Поиск новой обители"))
            return;

        while (true)
        {
            var searchModes = new List<string>
            {
                "✍ Свободный поиск мыслью",
                "🧲 Притяжение к извечному хранителю",
                "← Назад"
            };

            var mode = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]🔍 Поиск новой обители[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(searchModes));

            if (mode.StartsWith("←", StringComparison.Ordinal))
                return;

            if (mode.StartsWith("🧲", StringComparison.Ordinal))
            {
                await StartSystemGuardianAttractionAsync();
                return;
            }

            break;
        }

        var lines = new List<string>
        {
            "[bold cyan]🔍 Поиск новой обители[/]",
            "",
            "Вы сосредотачиваетесь, отправляя волну мысли сквозь бесконечность Моря Хаоса...",
            "",
            "[dim]Вы можете указать пожелание — какой домен или тип наставника вы ищете,",
            "или довериться судьбе и отправиться в неизведанное.[/]",
            "[dim]Если нужен конкретный извечный Хранитель, используйте отдельный режим притяжения.[/]",
            "",
            "[bold]Контракт ГМ после вашего свободного текста:[/]",
            "  • Это authored-by-GM ход Моря Хаоса, а не локальное изменение клиента.",
            "  • Клиент не создаёт pending/control file для свободного поиска; GM сверяет контракт с Afterlife Matrix example 23.",
            "  • GM обязан явно показать бросок/исход поиска и не сокращать state consequences.",
            "  • При найденной Обители: материализовать или обновить guardian identity, abode/location binding, relationship/reputation context and current visit state через поддержанные guardian surfaces.",
            "  • При далёком сигнале: описать clue без телепорта; stateful clue допустим только через явные guardian/abode ids и поддержанные guardian surfaces.",
            "  • При провале: не создавать скрытого Хранителя и не менять текущую Обитель.",
            "  • Любой stateful outcome должен иметь проверяемые ids: guardianId, guardianName, abodeId/location label, reason/source='chaos_sea_abode_search'.",
            "",
            "[yellow]Чтобы начать поиск, напишите в чат что-то вроде:[/]",
            "  [white]• \"Ищу хранителя боевых искусств\"[/]",
            "  [white]• \"Хочу найти мудрого наставника магии\"[/]",
            "  [white]• \"Отправляюсь на поиски неизвестной обители\"[/]",
            "  [white]• \"Ищу хранителя, который разбирается в ремесле\"[/]",
            "",
            "[dim]Результат зависит от броска d20 (Block 32_ext.1):[/]",
            "  [red]1-5:[/]   Ничего не найдено (можно повторить)",
            "  [yellow]6-12:[/]  Далёкий сигнал — прибытие на след. ходу",
            "  [green]13-18:[/] Обитель найдена! Мгновенное прибытие",
            "  [gold1]19-20:[/] Найден редкий Хранитель!",
        };
        AppendChaosSeaCommonContractRules(lines);

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌊 Поиск в Море Хаоса ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc?.RootElement.ValueKind == JsonValueKind.Object)
            WriteJsonAuditPanel(
                "Полный JSON текущего Chaos Sea state перед свободным поиском",
                guardiansDoc.RootElement,
                Color.Cyan1);
        WaitForKey();
    }

    private async Task ShowGuardianProjects()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Проекты Хранителей"))
            return;

        var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        if (trackerDoc == null || trackerDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            ShowEmptyPanel("Проекты Хранителей", "Журнал проектов хранителей пока пуст");
            return;
        }

        var trackerRoot = trackerDoc.RootElement;
        var activeEntries = CollectGuardianProjectEntries(trackerRoot, "activeProjects");
        var completedEntries = CollectGuardianProjectEntries(trackerRoot, "completedProjects");
        if (activeEntries.Count == 0 && completedEntries.Count == 0)
        {
            ShowEmptyPanel("Проекты Хранителей", "У хранителей пока нет активных или завершённых проектов");
            return;
        }

        var journalDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.JournalPath);
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var guardianNames = guardiansDoc != null
            ? BuildGuardianNameMap(guardiansDoc.RootElement)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var allEntries = activeEntries.Concat(completedEntries).ToList();
            var choices = allEntries.Select(entry =>
            {
                var guardianId = GetStr(entry, "guardianId", "");
                var guardianName = guardianNames.TryGetValue(guardianId, out var resolvedName)
                    ? resolvedName
                    : guardianId;
                var project = entry.GetProperty("project");
                var projectId = GetStr(project, "projectId", GetStr(project, "id", ""));
                var projectName = GetStr(project, "projectName", GetStr(project, "name", "Проект"));
                var activeState = GetStr(project, "activeState", "");
                var finalState = GetStr(project, "finalState", "");
                var status = FormatGuardianProjectStateLabel(string.IsNullOrWhiteSpace(activeState) ? finalState : activeState);
                return ConsoleLayout.PlainChoiceLabel(
                    $"🔬 {projectName}",
                    $"{(string.IsNullOrWhiteSpace(guardianName) ? guardianId : guardianName)} • guardianId={guardianId} • projectId={projectId}",
                    string.IsNullOrWhiteSpace(status) ? "" : status);
            }).ToList();
            var choiceIndexByLabel = choices
                .Select((choice, index) => (choice, index))
                .ToDictionary(item => item.choice, item => item.index, StringComparer.Ordinal);
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]🔬 Проекты Хранителей[/]")
                .PageSize(18)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected == "← Назад")
                return;

            if (!choiceIndexByLabel.TryGetValue(selected, out var selectedIndex) ||
                selectedIndex < 0 ||
                selectedIndex >= allEntries.Count)
                return;

            ShowGuardianProjectDetailPanel(allEntries[selectedIndex], guardianNames, journalDoc?.RootElement, trackerRoot);
        }
    }

    private async Task ShowAbodePower()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Сила Обители"))
            return;

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc == null || guardiansDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            ShowEmptyPanel("Сила Обители", "Данные о хранителях недоступны.");
            return;
        }

        var guardians = CollectGuardianDisplayEntries(guardiansDoc.RootElement);
        if (guardians.Count == 0)
        {
            ShowEmptyPanel("Сила Обители", "Известных хранителей пока нет.");
            return;
        }

        var journalDoc = await _stateManager.LoadGameStateFileAsync(GuardianPowerEventState.JournalPath);
        var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        var guardianNames = BuildGuardianNameMap(guardiansDoc.RootElement);

        while (true)
        {
            var choices = guardians.Select(guardian =>
            {
                var guardianId = GetStr(guardian, "guardianId", "");
                var guardianName = GuardianManifestation.GetDisplayName(guardian);
                var currentPower = AbodePowerRules.GetCurrentPower(guardian);
                var recentEvent = journalDoc != null
                    ? CollectGuardianPowerJournalEntries(journalDoc.RootElement, guardianId).FirstOrDefault()
                    : default;
                var trailing = recentEvent.ValueKind == JsonValueKind.Object
                    ? GetStr(recentEvent, "title", "")
                    : "";
                return ConsoleLayout.PlainChoiceLabel(
                    $"🏛 {guardianName}",
                    $"{currentPower}/100 — {AbodePowerRules.GetTierLabel(currentPower)}",
                    trailing);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold gold1]🏛 Сила Обители[/]")
                .PageSize(18)
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= guardians.Count)
                return;

            ShowAbodePowerDetailPanel(guardians[selectedIndex], guardianNames, journalDoc?.RootElement, trackerDoc?.RootElement);
        }
    }

    private void ShowAbodePowerDetailPanel(
        JsonElement guardian,
        IReadOnlyDictionary<string, string> guardianNames,
        JsonElement? journalRoot,
        JsonElement? trackerRoot)
    {
        var guardianId = GetStr(guardian, "guardianId", "");
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        var derivedState = GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot ?? default);
        var currentPower = derivedState.CurrentPower;
        var projectEffects = derivedState.ProjectEffects;
        var temporaryModifiers = GuardianProjectState.CollectActiveTemporaryModifiers(trackerRoot ?? default, guardianId);
        var historyEntries = guardian.TryGetProperty("abodePower", out var abodePower) && abodePower.ValueKind == JsonValueKind.Object &&
                             abodePower.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array
            ? history.EnumerateArray().Reverse().ToList()
            : new List<JsonElement>();
        var journalEntries = CollectGuardianPowerJournalEntries(journalRoot ?? default, guardianId).ToList();

        var lines = new List<string>
        {
            $"[bold gold1]🏛 {Markup.Escape(guardianName)}[/]",
            "",
            $"[bold]Текущее значение:[/] [{derivedState.TierColor}]{currentPower}[/]/100 [dim]({Markup.Escape(derivedState.TierLabel)})[/]",
            $"[bold]Что даёт текущая сила:[/] [dim]Торговых мест {derivedState.TradeSlotCount} • Доступных квестов {derivedState.GuardianQuestCap} • Потолок сложности до {FormatGuardianQuestDifficultyLabel(derivedState.GuardianQuestDifficultyCeiling)} • Дополнительных попыток гачи +{derivedState.BonusGachaCharges} • Бюджет корректив {derivedState.EffectiveNextLifeCorrectionBudgetPoints}[/]",
            $"[bold]Нити судьбы:[/] [dim]Явных следов соперника {derivedState.EffectiveRivalArcDefenseClues} • {Markup.Escape(FormatRivalArcClarityLabel(derivedState.RivalArcClarityTier))} • Контр-квест {(derivedState.RivalArcCounterQuestAccess ? "доступен" : "ещё закрыт")} • {Markup.Escape(FormatRivalArcWarningLabel(derivedState.RivalArcWarningTier))}[/]",
            $"[bold]Предел враждебного давления:[/] [dim]{Markup.Escape(FormatRivalArcOffenseCapLabel(derivedState.RivalArcOffenseCap))}[/]"
        };

        if (derivedState.EffectiveGuardianRarityCeilingBonusSteps > 0 || derivedState.EffectiveUpgradedTradeSlots > 0 || derivedState.EffectiveElevatedTradeSlots > 0)
        {
            lines.Add($"[bold]Ковка реликтов:[/] [dim]Усиленных торговых мест {derivedState.EffectiveUpgradedTradeSlots} • Возвышенных торговых мест {derivedState.EffectiveElevatedTradeSlots} • Потолок редкости +{derivedState.EffectiveGuardianRarityCeilingBonusSteps}[/]");
        }

        if (projectEffects.BonusLoreUnlocks > 0 || projectEffects.QuestHookCount > 0 || projectEffects.GuaranteedArchiveQuestCount > 0 || projectEffects.SpecialQuestLineUnlocks > 0 || projectEffects.VisibleRivalClueBonus > 0 || projectEffects.ArchiveWarningTierBonus > 0)
        {
            lines.Add($"[bold]Исследование знания:[/] [dim]Новых фрагментов {projectEffects.BonusLoreUnlocks} • Квестовых зацепок {projectEffects.QuestHookCount} • Гарантированных архивных квестов {projectEffects.GuaranteedArchiveQuestCount} • Особых сюжетных линий {projectEffects.SpecialQuestLineUnlocks} • Явных следов соперника {projectEffects.VisibleRivalClueBonus} • Уровень предупреждения +{projectEffects.ArchiveWarningTierBonus}[/]");
        }

        if (projectEffects.PreparationBudgetPoints > 0 || projectEffects.PreparationClaimPriorityBonus > 0 || projectEffects.HostilePriorityTokensGranted > 0)
        {
            lines.Add($"[bold]Подготовка души:[/] [dim]Очков бюджета подготовки +{projectEffects.PreparationBudgetPoints} • Приоритет выбора +{projectEffects.PreparationClaimPriorityBonus} • Враждебных жетонов приоритета {projectEffects.HostilePriorityTokensGranted}[/]");
        }

        if (derivedState.FortificationSafePressureBonus > 0 || derivedState.FortificationDefenseRatingBonus > 0 || temporaryModifiers.Count > 0)
        {
            lines.Add($"[bold]Политическая защита:[/] [dim]Безопасного давления +{derivedState.FortificationSafePressureBonus} • Рейтинг защиты +{derivedState.FortificationDefenseRatingBonus} • Временных усилений {temporaryModifiers.Count}[/]");
        }

        if (historyEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Последние изменения силы Обители:[/]");
            foreach (var entry in historyEntries)
            {
                var change = GetInt(entry, "change", 0);
                var title = GetStr(entry, "reason", GetStr(entry, "reasonType", ""));
                var timestamp = GetStr(entry, "timestamp", "");
                var deltaText = change > 0 ? $"[green]+{change}[/]" : $"[red]{change}[/]";
                var tsText = !string.IsNullOrWhiteSpace(timestamp) ? $"[dim]{Markup.Escape(timestamp)}[/] " : "";
                lines.Add($"  • {tsText}{deltaText} {Markup.Escape(title)}");
            }
        }

        if (journalEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Журнал причин:[/]");
            foreach (var entry in journalEntries)
            {
                var delta = GetInt(entry, "delta", 0);
                var turn = GetInt(entry, "turn", 0);
                var title = GetStr(entry, "title", "");
                var summary = GetStr(entry, "summary", "");
                var reasonLabel = FormatAbodePowerReasonType(GetStr(entry, "reasonType", ""));
                if (string.Equals(GetStr(entry, "reasonType", ""), "offering", StringComparison.OrdinalIgnoreCase) &&
                    entry.TryGetProperty("audit", out var audit) &&
                    audit.ValueKind == JsonValueKind.Object)
                {
                    var offeringTypeLabel = FormatAbodeOfferingType(GetStr(audit, "offeringType", ""));
                    if (!string.IsNullOrWhiteSpace(offeringTypeLabel))
                        reasonLabel = $"{reasonLabel}: {offeringTypeLabel}";
                }
                var deltaText = delta > 0 ? $"[green]+{delta}[/]" : $"[red]{delta}[/]";
                var turnText = turn > 0 ? $"[dim](ход {turn})[/] " : "";
                lines.Add($"  • {turnText}{deltaText} [white]{Markup.Escape(title)}[/]");
                if (!string.IsNullOrWhiteSpace(reasonLabel))
                    lines.Add($"    [dim]Источник: {Markup.Escape(reasonLabel)}[/]");
                if (!string.IsNullOrWhiteSpace(summary))
                    lines.Add($"    [dim]{Markup.Escape(summary)}[/]");

                var relatedGuardianId = GetStr(entry, "relatedGuardianId", "");
                if (!string.IsNullOrWhiteSpace(relatedGuardianId) && guardianNames.TryGetValue(relatedGuardianId, out var relatedGuardianName))
                    lines.Add($"    [dim]Связанный хранитель: {Markup.Escape(relatedGuardianName)}[/]");
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🏛 Сила Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("Полный JSON Хранителя и abodePower", guardian, Color.Gold1);
        if (journalRoot.HasValue)
            WriteJsonAuditPanel("Полный JSON журнала силы Обители", journalRoot.Value, Color.Gold1);
        if (trackerRoot.HasValue)
            WriteJsonAuditPanel("Полный JSON трекера проектов", trackerRoot.Value, Color.Cyan1);
        WaitForKey();
    }

    private async Task ShowGuardianPoliticsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Политика Хранителей"))
            return;

        var result = await ExplorerChaosSeaCommandResultBuilder.TryBuildAsync("/guardian_politics", _stateManager, _fs);
        if (result == null)
        {
            ShowEmptyPanel("Политика Хранителей", "Команда политики Хранителей недоступна.");
            return;
        }

        Clear();
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        WaitForKey();
    }

    private static string FormatAbodePowerReasonType(string reasonType) => reasonType.Trim().ToLowerInvariant() switch
    {
        "guardian_quest" => "Квест Хранителя",
        "project_assist" => "Помощь проекту",
        "project_completion" => "Завершение проекта",
        "project_failure" => "Провал проекта",
        "offering" => "Подношение Обители",
        "resonance" => "Резонанс жизни",
        "correction_spend" => "Трата на коррективы",
        "rival_strike" => "Удар Хранителя-соперника",
        "rival_defense" => "Защитное действие",
        _ => reasonType
    };

    private static string FormatAbodeOfferingType(string offeringType) => offeringType.Trim().ToLowerInvariant() switch
    {
        "ink_feathers" => "Чернильные перья",
        "soul_relic" => "Реликвия Души",
        "archive_lore_fragment" => "Фрагмент Знания",
        "archive_secret_record" => "Запись Тайны",
        _ => offeringType
    };

    private static void AppendPoliticalProjectAuditLines(
        List<string> lines,
        JsonElement project,
        IReadOnlyDictionary<string, string> guardianNames,
        string indent)
    {
        if (project.TryGetProperty("offensiveImpactAudit", out var offensiveAudit) && offensiveAudit.ValueKind == JsonValueKind.Object)
        {
            var targetLoss = GetInt(offensiveAudit, "targetLoss", 0);
            var pressureDelta = GetInt(offensiveAudit, "pressureDelta", 0);
            var stabilityDamage = GetInt(offensiveAudit, "stabilityDamage", 0);
            var targetGuardianId = GetStr(project, "targetGuardianId", "");
            var targetGuardianName = guardianNames.TryGetValue(targetGuardianId, out var resolvedTargetName)
                ? resolvedTargetName
                : targetGuardianId;
            lines.Add($"{indent}[bold]Политический удар:[/] [dim]{Markup.Escape(string.IsNullOrWhiteSpace(targetGuardianName) ? "цель не указана" : targetGuardianName)} • потеря силы {targetLoss} • внешнее давление +{pressureDelta} • устойчивость замысла -{stabilityDamage}[/]");
            return;
        }

        if (project.TryGetProperty("projectOutcomeAudit", out var outcomeAudit) && outcomeAudit.ValueKind == JsonValueKind.Object)
        {
            var projectType = GetStr(project, "projectType", "");
            if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"{indent}[bold]Контр-операция:[/] [dim]снятие давления {GetInt(outcomeAudit, "pressureRelief", 0)} • восстановление устойчивости +{GetInt(outcomeAudit, "stabilityRelief", 0)}[/]");
            }
            else if (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"{indent}[bold]Фортификация:[/] [dim]запас безопасного давления +{GetInt(outcomeAudit, "safePressureBonus", 0)} • прочность защиты +{GetInt(outcomeAudit, "defenseRatingBonus", 0)}[/]");
            }
        }
    }

    private static void AppendTemporaryModifierLines(List<string> lines, IReadOnlyList<GuardianProjectState.TemporaryModifierSnapshot> modifiers, string indent)
    {
        if (modifiers.Count == 0)
            return;

        lines.Add($"{indent}[bold]Временные модификаторы:[/]");
        foreach (var modifier in modifiers
                     .OrderByDescending(item => item.RemainingApplications)
                     .ThenBy(item => item.ModifierId, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{indent}  [dim]{Markup.Escape(FormatTemporaryModifierTypeLabel(modifier.ModifierType))} • сила эффекта {modifier.Value:+#;-#;0} • осталось срабатываний {modifier.RemainingApplications}[/]");
            if (!string.IsNullOrWhiteSpace(modifier.ModifierId))
                lines.Add($"{indent}  [dim]Идентификатор модификатора: {Markup.Escape(modifier.ModifierId)}[/]");
        }
    }

    private static string FormatTemporaryModifierTypeLabel(string? modifierType) =>
        modifierType?.Trim().ToLowerInvariant() switch
        {
            "next_internal_project_starting_pressure" => "стартовое давление следующего внутреннего проекта",
            _ => HumanizeProjectToken(modifierType)
        };

    private static string FormatGuardianQuestDifficultyLabel(string? difficulty) =>
        AbodePowerRules.NormalizeGuardianQuestDifficulty(difficulty) switch
        {
            "easy" => "Лёгкой",
            "hard" => "Тяжёлой",
            "epic" => "Эпической",
            _ => "Нормальной"
        };

    private static string FormatGuardianProjectTypeLabel(string? projectType) =>
        (projectType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "abode_expansion" => "Расширение Обители",
            "abode_fortification" => "Укрепление Обители",
            "relic_forging" => "Ковка реликтов",
            "lore_research" => "Исследование знаний",
            "soul_preparation" => "Подготовка души",
            "offensive_intrigue" => "Наступательная интрига",
            "counter_rival_operation" => "Операция против чужого замысла",
            _ => HumanizeProjectToken(projectType)
        };

    private static string FormatGuardianProjectModeLabel(string? projectMode) =>
        (projectMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "internal" => "Внутреннее развитие Обители",
            "external" => "Внешнее воздействие",
            "support" => "Поддержка союзной линии",
            "offensive" => "Наступательное давление",
            "defensive" => "Оборонительное сдерживание",
            _ => HumanizeProjectToken(projectMode)
        };

    private static string FormatGuardianProjectTierLabel(string? projectTier) =>
        (projectTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "minor" => "Малый",
            "major" => "Крупный",
            "grand" => "Великий",
            _ => HumanizeProjectToken(projectTier)
        };

    private static string FormatGuardianProjectStateLabel(string? state) =>
        (state ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "surveying" => "Осмотр и замер контуров",
            "binding" => "Закрепление замысла",
            "forging" => "Ковка результата",
            "researching" => "Сбор и расшифровка знаний",
            "preparing" => "Подготовка следующей жизни",
            "completed" => "Завершён",
            "abandoned" => "Оставлен",
            "sabotaged" => "Сорван",
            "collapsed" => "Рухнул",
            _ => HumanizeProjectToken(state)
        };

    private static string FormatProjectPressureLabel(int pressure) =>
        $"Внешнее давление: {pressure}";

    private static string FormatProjectStabilityLabel(int stability) =>
        $"Устойчивость замысла: {stability}";

    private static string FormatRivalArcClarityLabel(int clarityTier) => clarityTier switch
    {
        <= 0 => "картина ещё смутна",
        1 => "картина начинает проступать",
        2 => "картина уже достаточно ясна",
        _ => "картина читается отчётливо"
    };

    private static string FormatRivalArcWarningLabel(int warningTier) => warningTier switch
    {
        <= 0 => "ранних предупреждений нет",
        1 => "ранние предупреждения уже возможны",
        _ => $"уровень предупреждения {warningTier}"
    };

    private static string FormatRivalArcOffenseCapLabel(string? offenseCap) =>
        (offenseCap ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "no_formal_hostile_arc_sponsorship" => "Обитель ещё не может поддерживать оформленную враждебную линию",
            "background_pressure_only" => "Обитель пока может давить лишь фоном и косвенными сигналами",
            "one_minor_hostile_arc" => "Обитель способна выдержать одну малую враждебную линию",
            "one_major_or_direct_minor" => "Обитель выдерживает одну крупную или одну прямую малую враждебную линию",
            "one_major_with_early_signal_privilege" => "Обитель выдерживает одну крупную линию и получает ранний сигнал о чужом ходе",
            _ => HumanizeProjectToken(offenseCap)
        };

    private static string FormatProjectJournalDetailLine(string rawDetail)
    {
        if (string.IsNullOrWhiteSpace(rawDetail))
            return rawDetail;

        return rawDetail
            .Replace("Pressure:", "Внешнее давление:", StringComparison.Ordinal)
            .Replace("Stability:", "Устойчивость замысла:", StringComparison.Ordinal)
            .Replace("Work:", "Работа:", StringComparison.Ordinal);
    }

    private static string HumanizeProjectToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "не указано";

        var raw = token.Trim().Replace('_', ' ');
        var builder = new StringBuilder(raw.Length + 8);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (i > 0 && char.IsUpper(ch) && char.IsLetter(raw[i - 1]) && !char.IsWhiteSpace(raw[i - 1]))
                builder.Append(' ');
            builder.Append(ch);
        }

        return builder.ToString();
    }

    private async Task ShowAbodesNavigation()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Обители"))
            return;
        if (!_stateManager.CurrentState.IsInChaosSea)
        {
            ShowEmptyPanel(
                "Обители",
                "Переход между Обителями через [CHAOS_SEA_TRAVEL] доступен только в обычном Море Хаоса. В Сияющей Обители эта навигация не меняет realm и потому заблокирована.");
            return;
        }

        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (doc == null) { ShowEmptyPanel("Обители", "Данные хранителей недоступны"); return; }

        var root = doc.RootElement;
        var guardians = CollectGuardianDisplayEntries(root);

        var currentAbodeId = "";
        var previousActiveGuardianId = "";
        var previousActiveGuardianName = "";
        JsonElement? previousActiveGuardianNode = null;
        var discoveredAbodeIds = new List<string>();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("chaosSeaNavigation", out var nav) && nav.ValueKind == JsonValueKind.Object)
        {
            currentAbodeId = GetStr(nav, "currentAbodeId", "");
            if (nav.TryGetProperty("discoveredAbodes", out var discoveredAbodes) &&
                discoveredAbodes.ValueKind == JsonValueKind.Array)
            {
                discoveredAbodeIds = discoveredAbodes.EnumerateArray()
                    .Where(node => node.ValueKind == JsonValueKind.String)
                    .Select(node => node.GetString() ?? "")
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("activeGuardian", out var activeGuardian) &&
            activeGuardian.ValueKind == JsonValueKind.Object)
        {
            previousActiveGuardianNode = activeGuardian;
            previousActiveGuardianId = GetStr(activeGuardian, "guardianId", "");
            previousActiveGuardianName = GuardianManifestation.GetDisplayName(activeGuardian);
        }
        var currentAbodeName = guardians
            .Select(g => g.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object
                ? new
                {
                    AbodeId = GetStr(abode, "abodeId", ""),
                    AbodeName = GetStr(abode, "name", "")
                }
                : null)
            .Where(item => item != null)
            .Where(item => string.Equals(item!.AbodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item!.AbodeName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;

        var abodeGuardians = guardians
            .Where(g =>
            {
                if (!g.TryGetProperty("abode", out var ab) || ab.ValueKind != JsonValueKind.Object)
                    return false;

                var abodeId = GetStr(ab, "abodeId", "");
                var isDiscovered = ab.TryGetProperty("isDiscovered", out var isDiscoveredNode) &&
                                   isDiscoveredNode.ValueKind == JsonValueKind.True;
                var isCurrent = !string.IsNullOrWhiteSpace(abodeId) &&
                                string.Equals(abodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
                var isInDiscoveredNavigation = !string.IsNullOrWhiteSpace(abodeId) &&
                                               discoveredAbodeIds.Contains(abodeId, StringComparer.OrdinalIgnoreCase);

                return isCurrent || (isDiscovered && isInDiscoveredNavigation);
            })
            .ToList();

        if (abodeGuardians.Count == 0)
        {
            ShowEmptyPanel("Обители", "Обители ещё не открыты. Используйте /хранители для поиска.");
            return;
        }

        while (true)
        {
            var choices = abodeGuardians.Select(g =>
            {
                var gName = GuardianManifestation.GetDisplayName(g);
                if (string.IsNullOrWhiteSpace(gName))
                    gName = "?";
                var ab = g.GetProperty("abode");
                var abName = GetStr(ab, "name", "???");
                var abId = GetStr(ab, "abodeId", "");
                var isCurrent = !string.IsNullOrWhiteSpace(abId) &&
                                string.Equals(abId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
                var domain = GetStr(g, "domain", "");
                var domainRu = domain switch
                {
                    "Combat" => "Бой", "Magic" => "Магия", "Trade" => "Торговля",
                    "Social" => "Общение", "Crafting" => "Ремесло",
                    "Survival" => "Выживание", "Knowledge" => "Знания", _ => domain
                };
                return ConsoleLayout.PlainChoiceLabel(
                    $"🏛️ {abName}",
                    $"{domainRu} — {gName}",
                    string.IsNullOrWhiteSpace(GetStr(g, "guardianId", "")) ? "" : $"guardianId={GetStr(g, "guardianId", "")}",
                    string.IsNullOrWhiteSpace(abId) ? "" : $"abodeId={abId}",
                    isCurrent ? "ЗДЕСЬ" : "");
            }).ToList();
            choices.Add("🔍 Искать новую обитель");
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]🏛️ Обители Моря Хаоса[/]  [dim](выберите для перемещения)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected == "← Назад") break;
            if (selected.Contains("Искать новую обитель")) { await ShowSearchAbodePrompt(); continue; }

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= abodeGuardians.Count) break;

            var selGuardian = abodeGuardians[selIdx];
            var selAbode = selGuardian.GetProperty("abode");
            var selAbodeId = GetStr(selAbode, "abodeId", "");
            var selAbodeName = GetStr(selAbode, "name", "???");
            var selGuardianId = GetStr(selGuardian, "guardianId", "");
            var selGName = GuardianManifestation.GetDisplayName(selGuardian);
            if (string.IsNullOrWhiteSpace(selGName))
                selGName = "?";
            var targetAlreadyDiscovered = discoveredAbodeIds.Any(id => string.Equals(id, selAbodeId, StringComparison.OrdinalIgnoreCase));
            var discoveredAbodesContract = discoveredAbodeIds.Count == 0 ? "[]" : $"[{string.Join(", ", discoveredAbodeIds)}]";

            if (string.Equals(selAbodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                MarkupLine($"[dim]Вы уже находитесь в обители «{Markup.Escape(selAbodeName)}».[/]");
                WaitForKey();
                continue;
            }

            var travelAction =
                $"[CHAOS_SEA_TRAVEL] Душа выбирает перемещение в обитель '{selAbodeName}' " +
                $"(targetAbodeId={selAbodeId}, targetGuardianId={selGuardianId}, targetGuardianName='{selGName}', " +
                $"previousAbodeId={currentAbodeId}, previousActiveGuardianId={previousActiveGuardianId}, previousActiveGuardianName='{previousActiveGuardianName}', " +
                $"targetAlreadyDiscovered={targetAlreadyDiscovered.ToString().ToLowerInvariant()}, discoveredAbodes={discoveredAbodesContract}). " +
                "Обработай путешествие как полноценный afterlife-ход: опиши прибытие и реакцию Хранителя; в game_state/meta/guardians.json " +
                "синхронно установи activeGuardian на targetGuardianId, chaosSeaNavigation.currentAbodeId на targetAbodeId, " +
                "убедись что targetAbodeId есть в chaosSeaNavigation.discoveredAbodes и что abode.isDiscovered=true у target guardian. " +
                "Не используй currentLocationData, UpdateNPCs, worldEventsLog, weather/time или Mortal World travel/location systems.";
            var travelLines = new List<string>
            {
                "[bold cyan]Переход между Обителями[/]",
                "",
                $"  Source abode: [white]{Markup.Escape(string.IsNullOrWhiteSpace(currentAbodeName) ? "неизвестно" : currentAbodeName)}[/] [dim]({Markup.Escape(currentAbodeId)})[/]",
                $"  Source activeGuardian: [white]{Markup.Escape(string.IsNullOrWhiteSpace(previousActiveGuardianName) ? "неизвестно" : previousActiveGuardianName)}[/] [dim]({Markup.Escape(previousActiveGuardianId)})[/]",
                $"  Target abode: [white]{Markup.Escape(selAbodeName)}[/] [dim]({Markup.Escape(selAbodeId)})[/]",
                $"  Целевой Хранитель: [white]{Markup.Escape(selGName)}[/] [dim]({Markup.Escape(selGuardianId)})[/]",
                $"  Уже открыта игроком: [dim]{targetAlreadyDiscovered.ToString().ToLowerInvariant()}[/]",
                $"  discoveredAbodes до хода: [dim]{Markup.Escape(discoveredAbodesContract)}[/]",
                "",
                "[bold]Canonical accepted outcome:[/]",
                "  • guardians.json.activeGuardian = targetGuardianId.",
                "  • guardians.json.chaosSeaNavigation.currentAbodeId = targetAbodeId.",
                "  • targetAbodeId присутствует в chaosSeaNavigation.discoveredAbodes.",
                "  • target guardian abode.isDiscovered=true.",
                "  • Путешествие отыгрывается как полноценный afterlife ход, но не как Mortal World travel."
            };
            AppendChaosSeaCommonContractRules(travelLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр перехода Моря Хаоса",
                    travelLines,
                    BuildChaosSeaTravelAuditNode(
                        travelAction,
                        currentAbodeId,
                        currentAbodeName,
                        previousActiveGuardianId,
                        previousActiveGuardianName,
                        selAbodeId,
                        selAbodeName,
                        selGuardianId,
                        selGName,
                        targetAlreadyDiscovered,
                        discoveredAbodeIds,
                        previousActiveGuardianNode,
                        selGuardian),
                    "Полный JSON до/после перехода Моря Хаоса"))
            {
                continue;
            }

            _pendingGmAction = travelAction;

            MarkupLine($"[cyan]🌊 Переход в обитель «{Markup.Escape(selAbodeName)}» отправляется Мастеру Игры...[/]");
            return;
        }
    }

    private static JsonObject BuildChaosSeaTravelAuditNode(
        string playerAction,
        string previousAbodeId,
        string previousAbodeName,
        string previousActiveGuardianId,
        string previousActiveGuardianName,
        string targetAbodeId,
        string targetAbodeName,
        string targetGuardianId,
        string targetGuardianName,
        bool targetAlreadyDiscovered,
        IReadOnlyCollection<string> discoveredAbodeIds,
        JsonElement? previousActiveGuardian,
        JsonElement targetGuardian)
    {
        var discoveredBefore = discoveredAbodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var discoveredAfter = discoveredBefore
            .Append(targetAbodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new JsonObject
        {
            ["actionTag"] = "CHAOS_SEA_TRAVEL",
            ["playerAction"] = playerAction,
            ["before"] = new JsonObject
            {
                ["previousAbodeId"] = previousAbodeId,
                ["previousAbodeName"] = previousAbodeName,
                ["previousActiveGuardianId"] = previousActiveGuardianId,
                ["previousActiveGuardianName"] = previousActiveGuardianName,
                ["chaosSeaNavigation.currentAbodeId"] = previousAbodeId,
                ["chaosSeaNavigation.discoveredAbodes"] = new JsonArray(discoveredBefore.Select(id => (JsonNode?)id).ToArray()),
                ["previousActiveGuardianFull"] = previousActiveGuardian.HasValue
                    ? CloneJsonElementForAudit(previousActiveGuardian.Value)
                    : null
            },
            ["after"] = new JsonObject
            {
                ["activeGuardian.guardianId"] = targetGuardianId,
                ["activeGuardian.displayName"] = targetGuardianName,
                ["chaosSeaNavigation.currentAbodeId"] = targetAbodeId,
                ["chaosSeaNavigation.discoveredAbodes"] = new JsonArray(discoveredAfter.Select(id => (JsonNode?)id).ToArray()),
                ["targetGuardian.abode.abodeId"] = targetAbodeId,
                ["targetGuardian.abode.name"] = targetAbodeName,
                ["targetGuardian.abode.isDiscovered"] = true,
                ["targetGuardianFull"] = CloneJsonElementForAudit(targetGuardian),
                ["targetAbodeFull"] = targetGuardian.TryGetProperty("abode", out var targetAbode) && targetAbode.ValueKind == JsonValueKind.Object
                    ? CloneJsonElementForAudit(targetAbode)
                    : null
            },
            ["contract"] = new JsonObject
            {
                ["targetAlreadyDiscoveredBeforeTurn"] = targetAlreadyDiscovered,
                ["mustSetActiveGuardian"] = true,
                ["mustSetNavigationCurrentAbodeId"] = true,
                ["mustEnsureTargetInDiscoveredAbodes"] = true,
                ["mustMarkTargetAbodeDiscovered"] = true
            },
            ["forbiddenSurfaces"] = new JsonArray(
                "currentLocationData",
                "UpdateNPCs",
                "worldEventsLog",
                "world_time.json.timeChange/currentWeather",
                "Mortal World travel/location systems")
        };
    }

    private async Task ShowGuardianDetailPanel(JsonElement g, List<JsonElement>? allGuardians = null, string currentAbodeId = "", string activeGuardianId = "", JsonElement? guardianProjectTrackerRoot = null)
    {
        var name = GuardianManifestation.GetDisplayName(g);
        if (string.IsNullOrWhiteSpace(name))
            name = "Неизвестный";
        var guardianId = GetStr(g, "guardianId", "");
        var isActiveGuardian = string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase);
        var guardianThoughtDoc = await _stateManager.LoadGameStateFileAsync(GuardianThoughtJournalState.StatePath);
        var guardianSocialDoc = await _stateManager.LoadGameStateFileAsync(GuardianSocialJournalState.StatePath);
        var guardianThoughtEntries = CollectActorJournalEntryElements(guardianThoughtDoc, GuardianThoughtJournalState.ActorIdProperty, guardianId);
        var guardianSocialEntries = CollectActorJournalEntryElements(guardianSocialDoc, GuardianSocialJournalState.ActorIdProperty, guardianId);
        var pendingGuardianTalkRequest = await ActorSocialInteractionRequestState.FindPendingGuardianRequestAsync(_fs, guardianId, ActorSocialInteractionRequestState.GuardianInteractionTypeTalk);
        var pendingGuardianLoreRequest = await ActorSocialInteractionRequestState.FindPendingGuardianRequestAsync(_fs, guardianId, ActorSocialInteractionRequestState.GuardianInteractionTypeLore);
        var canonicalName = GuardianManifestation.GetCanonicalName(g);
        var manifestationAppearance = GuardianManifestation.GetAppearanceDescription(g);
        var manifestationStyle = GuardianManifestation.GetPresentationStyle(g);
        var manifestationPronouns = GuardianManifestation.GetPronouns(g);
        var formFlexibility = GuardianManifestation.GetFormFlexibility(g);
        var manifestationReason = g.TryGetProperty("manifestation", out var manifestation) && manifestation.ValueKind == JsonValueKind.Object
            ? GetStr(manifestation, "presentationReason", "")
            : string.Empty;
        var sourcePresetId = g.TryGetProperty("sourcePreset", out var sourcePreset) && sourcePreset.ValueKind == JsonValueKind.Object
            ? GetStr(sourcePreset, "presetId", "")
            : string.Empty;
        var domain = GetStr(g, "domain", "");
        var content = new Grid().AddColumn(new GridColumn());
        content.AddRow(new Markup($"[bold cyan]🛡️ {Markup.Escape(name)}[/]" +
            (isActiveGuardian ? " [green](активный хранитель)[/]" : "")));

        var summaryTable = ConsoleLayout.CreateInfoTable();
        if (!string.IsNullOrWhiteSpace(guardianId))
            summaryTable.AddRow(new Markup("[dim]guardianId[/]"), new Markup($"[dim]{Markup.Escape(guardianId)}[/]"));
        if (!string.IsNullOrWhiteSpace(sourcePresetId))
            summaryTable.AddRow(new Markup("[dim]sourcePreset.presetId[/]"), new Markup($"[dim]{Markup.Escape(sourcePresetId)}[/]"));
        if (isActiveGuardian)
            summaryTable.AddRow(new Markup("[green]Статус[/]"), new Markup("[green]Текущий активный Хранитель[/]"));
        if (!string.IsNullOrEmpty(domain))
        {
            var domainRu = domain switch
            {
                "Combat" => "Бой",
                "Magic" => "Магия",
                "Trade" => "Торговля",
                "Social" => "Общение",
                "Crafting" => "Ремесло",
                "Survival" => "Выживание",
                "Knowledge" => "Знания",
                _ => domain
            };
            summaryTable.AddRow(new Markup("[yellow]Домен[/]"), new Markup($"[yellow]{Markup.Escape(domainRu)}[/]"));
        }

        var lines = new List<string>();
        void FlushLines()
        {
            if (lines.Count == 0)
                return;

            content.AddRow(GameInterface.SafeMarkup(string.Join("\n", lines)));
            lines.Clear();
        }

        // ── Abode info ──
        if (g.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
        {
            var abodeName = GetStr(abode, "name", "");
            var abodeDesc = GetStr(abode, "description", "");
            var atmo = GetStr(abode, "atmosphere", "");
            var abodeId = GetStr(abode, "abodeId", "");
            var isCurrent = !string.IsNullOrEmpty(abodeId) && abodeId == currentAbodeId;

            if (!string.IsNullOrEmpty(abodeName))
            {
                var hereTag = isCurrent ? " [bold green]● Вы здесь[/]" : "";
                summaryTable.AddRow(new Markup("[white]Обитель[/]"), new Markup($"[white]{Markup.Escape(abodeName)}[/]{hereTag}"));
            }
            if (!string.IsNullOrEmpty(abodeDesc))
                lines.Add($"  [dim italic]{Markup.Escape(abodeDesc)}[/]");
            if (!string.IsNullOrEmpty(atmo))
            {
                var atmoRu = atmo switch
                {
                    "Welcoming" => "Гостеприимная",
                    "Imposing" => "Величественная",
                    "Mysterious" => "Загадочная",
                    "Chaotic" => "Хаотичная",
                    "Serene" => "Безмятежная",
                    "Austere" => "Аскетичная",
                    "Opulent" => "Роскошная",
                    _ => atmo
                };
                summaryTable.AddRow(new Markup("[dim]Атмосфера[/]"), new Markup($"[dim]{Markup.Escape(atmoRu)}[/]"));
            }
        }

        // Personality
        if (g.TryGetProperty("personalityProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            var archetype = GetStr(pp, "archetype", "");
            var speech = GetStr(pp, "speechPattern", "");
            if (!string.IsNullOrEmpty(archetype))
                summaryTable.AddRow(new Markup("[mediumpurple2]Архетип[/]"), new Markup($"[mediumpurple2]{Markup.Escape(archetype)}[/]"));
            if (!string.IsNullOrEmpty(speech))
                summaryTable.AddRow(new Markup("[dim]Стиль речи[/]"), new Markup($"[dim]{Markup.Escape(speech)}[/]"));
            if (pp.TryGetProperty("coreValues", out var cv) && cv.ValueKind == JsonValueKind.Array)
            {
                var vals = new List<string>();
                foreach (var v in cv.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.String) vals.Add(v.GetString() ?? "");
                if (vals.Count > 0)
                    summaryTable.AddRow(new Markup("[white]Ценности[/]"), new Markup($"[white]{Markup.Escape(string.Join(", ", vals))}[/]"));
            }
        }

        if (GuardianManifestation.HasDistinctCanonicalName(g))
            summaryTable.AddRow(new Markup("[white]Каноническое имя[/]"), new Markup($"[white]{Markup.Escape(canonicalName)}[/]"));

        if (g.TryGetProperty("nameVariants", out var nameVariants) && nameVariants.ValueKind == JsonValueKind.Object)
        {
            var variantParts = new List<string>();
            foreach (var property in nameVariants.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    variantParts.Add($"{property.Name}={property.Value.GetString()}");
            }

            if (variantParts.Count > 0)
                summaryTable.AddRow(new Markup("[dim]nameVariants[/]"), new Markup($"[dim]{Markup.Escape(string.Join(", ", variantParts))}[/]"));
        }

        var manifestationStyleLabel = GuardianManifestation.GetPresentationStyleLabel(manifestationStyle);
        if (!string.IsNullOrWhiteSpace(manifestationStyleLabel))
            summaryTable.AddRow(new Markup("[dim]Подача[/]"), new Markup($"[dim]{Markup.Escape(manifestationStyleLabel)}[/]"));

        if (!string.IsNullOrWhiteSpace(manifestationPronouns))
            summaryTable.AddRow(new Markup("[dim]Местоимения[/]"), new Markup($"[dim]{Markup.Escape(manifestationPronouns)}[/]"));

        var formFlexibilityLabel = GuardianManifestation.GetFormFlexibilityLabel(formFlexibility);
        if (!string.IsNullOrWhiteSpace(formFlexibilityLabel))
            summaryTable.AddRow(new Markup("[dim]Гибкость формы[/]"), new Markup($"[dim]{Markup.Escape(formFlexibilityLabel)}[/]"));
        if (!string.IsNullOrWhiteSpace(manifestationReason))
            summaryTable.AddRow(new Markup("[dim]manifestation.presentationReason[/]"), new Markup($"[dim]{Markup.Escape(manifestationReason)}[/]"));

        var isPlayerFoundedGuardian = string.Equals(
            GetStr(g, "originType", ""),
            PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
            StringComparison.OrdinalIgnoreCase);
        var founderSoulName = GetStr(g, "founderSoulName", "");
        var founderLoyaltyTier = GetStr(g, "founderLoyaltyTier", "");
        if (isPlayerFoundedGuardian)
        {
            summaryTable.AddRow(new Markup("[gold1]Источник[/]"), new Markup("[gold1]Основан из вознесённой души[/]"));
            if (!string.IsNullOrWhiteSpace(founderSoulName))
                summaryTable.AddRow(new Markup("[white]Основатель[/]"), new Markup($"[white]{Markup.Escape(founderSoulName)}[/]"));
            if (!string.IsNullOrWhiteSpace(founderLoyaltyTier))
                summaryTable.AddRow(new Markup("[gold1]Связь[/]"), new Markup($"[gold1]{Markup.Escape(DescribeFounderLoyaltyTier(founderLoyaltyTier))}[/] [dim](онтологическая преданность)[/]"));
            var founderExtraGachaCharges = PlayerGuardianFoundationState.GetFounderExtraGachaCharges(g);
            if (founderExtraGachaCharges > 0)
                summaryTable.AddRow(new Markup("[gold1]Бонус основания[/]"), new Markup($"[gold1]+{founderExtraGachaCharges}[/] [dim]доп. попытка гачи за возвращение[/]"));
        }

        var derivedState = GuardianProjectState.ResolveGuardianDerivedState(g, guardianProjectTrackerRoot ?? default);
        var abodePowerValue = derivedState.CurrentPower;
        var guardianProjectEffects = derivedState.ProjectEffects;
        summaryTable.AddRow(
            new Markup("[gold1]Сила Обители[/]"),
            new Markup($"[{derivedState.TierColor}]{abodePowerValue} — {Markup.Escape(derivedState.TierLabel)}[/]"));

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        // Reputation
        int rep = 0;
        if (g.TryGetProperty("relationshipData", out var rd) && rd.ValueKind == JsonValueKind.Object)
        {
            rep = GetInt(rd, "currentReputation", 0);
            var guardianRoleToPlayer = GetStr(rd, PlayerGuardianFoundationState.GuardianRoleToPlayerProperty, "");
            FlushLines();
            content.AddRow(new Markup(""));
            var repTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 16, barWidth: 24, valueWidth: 8);
            repTable.AddRow(
                new Markup("[bold]♥ Репутация[/]"),
                new Markup(ReputationDisplay.BuildBarMarkup(rep, ReputationScaleKind.Guardian, 20)),
                new Markup($"[{ReputationDisplay.GetTier(ReputationScaleKind.Guardian, rep).Color}]{rep}[/]/300"),
                new Markup(ReputationDisplay.BuildTierMarkup(rep, ReputationScaleKind.Guardian)));
            content.AddRow(repTable);

            lines.Add("  [dim]Диапазоны репутации:[/]");
            lines.AddRange(ReputationDisplay.BuildLegendLines(ReputationScaleKind.Guardian, "    "));
            if (isPlayerFoundedGuardian)
                lines.Add("  [gold1]👑 Неразрывная мантия:[/] [dim]основанная мантия удерживается как минимум на легендарном ранге и пока не использует обычное снижение до более низкого статуса.[/]");
            if (isPlayerFoundedGuardian)
            {
                var founderFeatureTitle = PlayerGuardianFoundationState.GetFounderAbodeFeatureTitle(g);
                var founderFeatureSummary = PlayerGuardianFoundationState.GetFounderAbodeFeatureSummary(g);
                if (!string.IsNullOrWhiteSpace(founderFeatureTitle))
                    lines.Add($"  [gold1]🏛 Дар основания:[/] [white]{Markup.Escape(founderFeatureTitle)}[/]");
                if (!string.IsNullOrWhiteSpace(founderFeatureSummary))
                    lines.Add($"    [dim]{Markup.Escape(founderFeatureSummary)}[/]");
            }
            if (string.Equals(guardianRoleToPlayer, PlayerGuardianFoundationState.GuardianRoleFormerPatron, StringComparison.OrdinalIgnoreCase))
                lines.Add("  [gold1]👑 Роль:[/] [dim]прежний покровитель после основания вашей собственной мантии.[/]");
            lines.Add("");
            lines.Add($"  [bold]🏛️ Сила Обители:[/] {ConsoleLayout.CreateBar(Math.Clamp(abodePowerValue * 20 / 100, 0, 20), 20, derivedState.TierColor)} [{derivedState.TierColor}]{abodePowerValue}[/]/100 [dim]({Markup.Escape(derivedState.TierLabel)})[/]");
            lines.Add($"    [dim]Торговых мест: {derivedState.TradeSlotCount} • Доступных квестов: {derivedState.GuardianQuestCap} • Потолок сложности: до {FormatGuardianQuestDifficultyLabel(derivedState.GuardianQuestDifficultyCeiling)} • Дополнительных попыток гачи: +{derivedState.BonusGachaCharges} • Бюджет корректив: {derivedState.EffectiveNextLifeCorrectionBudgetPoints}[/]");
            if (derivedState.EffectiveGuardianRarityCeilingBonusSteps > 0 ||
                derivedState.EffectiveUpgradedTradeSlots > 0)
            {
                lines.Add($"    [dim]Ковка реликтов: усиленных торговых мест {derivedState.EffectiveUpgradedTradeSlots} • возвышенных торговых мест {derivedState.EffectiveElevatedTradeSlots} • потолок редкости +{derivedState.EffectiveGuardianRarityCeilingBonusSteps}[/]");
            }
            if (guardianProjectEffects.BonusLoreUnlocks > 0 || guardianProjectEffects.QuestHookCount > 0 || guardianProjectEffects.GuaranteedArchiveQuestCount > 0 || guardianProjectEffects.SpecialQuestLineUnlocks > 0 || guardianProjectEffects.VisibleRivalClueBonus > 0 || guardianProjectEffects.ArchiveWarningTierBonus > 0)
            {
                lines.Add($"    [dim]Исследование знаний: фрагментов {guardianProjectEffects.BonusLoreUnlocks} • квестовых зацепок {guardianProjectEffects.QuestHookCount} • архивных квестов {guardianProjectEffects.GuaranteedArchiveQuestCount} • особых линий {guardianProjectEffects.SpecialQuestLineUnlocks} • явных следов соперника {guardianProjectEffects.VisibleRivalClueBonus} • предупреждение +{guardianProjectEffects.ArchiveWarningTierBonus}[/]");
            }
            if (derivedState.FortificationSafePressureBonus > 0 || derivedState.FortificationDefenseRatingBonus > 0)
            {
                lines.Add($"    [dim]Политический щит: безопасного давления +{derivedState.FortificationSafePressureBonus} • рейтинг защиты +{derivedState.FortificationDefenseRatingBonus}[/]");
            }
            if (derivedState.ActiveTemporaryModifierCount > 0)
                lines.Add($"    [dim]Активные временные модификаторы: {derivedState.ActiveTemporaryModifierCount}[/]");
            if (g.TryGetProperty("abodePower", out var abodePowerNode) &&
                abodePowerNode.ValueKind == JsonValueKind.Object &&
                abodePowerNode.TryGetProperty("history", out var powerHistory) &&
                powerHistory.ValueKind == JsonValueKind.Array &&
                powerHistory.GetArrayLength() > 0)
            {
                var latestPowerChange = powerHistory.EnumerateArray().Reverse().First();
                var latestDelta = GetInt(latestPowerChange, "change", 0);
                var latestReason = GetStr(latestPowerChange, "reason", GetStr(latestPowerChange, "summary", ""));
                var latestDeltaText = latestDelta > 0 ? $"[green]+{latestDelta}[/]" : latestDelta < 0 ? $"[red]{latestDelta}[/]" : "[dim]±0[/]";
                lines.Add($"    [dim]Последнее изменение силы Обители: {latestDeltaText} {Markup.Escape(latestReason)}[/]");
            }

            var lastInteraction = GetStr(rd, "lastInteraction", "");
            if (!string.IsNullOrEmpty(lastInteraction))
                lines.Add($"  [dim]Последняя встреча: {Markup.Escape(lastInteraction)}[/]");

            if (rd.TryGetProperty("reputationHistory", out var rh) && rh.ValueKind == JsonValueKind.Array)
            {
                lines.Add($"  [dim]История ({rh.GetArrayLength()}):[/]");
                foreach (var entry in rh.EnumerateArray())
                {
                    var change = GetInt(entry, "change", 0);
                    var reason = GetStr(entry, "reason", "");
                    var ts = GetStr(entry, "timestamp", "");
                    var changeStr = change > 0 ? $"[green]+{change}[/]" : change < 0 ? $"[red]{change}[/]" : "[dim]±0[/]";
                    var timeStr = "";
                    if (!string.IsNullOrEmpty(ts)) timeStr = $"[dim]{Markup.Escape(ts)}[/] ";
                    lines.Add($"    {timeStr}{changeStr} {Markup.Escape(reason)}");
                }
            }
        }
        else
        {
            rep = GetInt(g, "reputation", 0);
            lines.Add($"  ♥ Репутация: {ReputationDisplay.BuildValueLabelMarkup(rep, ReputationScaleKind.Guardian)}");
        }

        var trackerRoot = guardianProjectTrackerRoot ?? default;
        if (!string.IsNullOrWhiteSpace(guardianId) &&
            TryGetActiveGuardianProject(trackerRoot, guardianId, out var activeTrackerProject))
        {
            lines.Add("");
            lines.Add("  [bold]🔬 Текущий проект Хранителя:[/]");
            AppendGuardianProjectSummaryLines(lines, activeTrackerProject, "    ");
        }
        else if (g.TryGetProperty("currentProject", out var legacyProject) && legacyProject.ValueKind == JsonValueKind.Object)
        {
            var projName = GetStr(legacyProject, "projectName", GetStr(legacyProject, "name", ""));
            var projDesc = GetStr(legacyProject, "description", "");
            var projProgress = GetInt(legacyProject, "progressPercent", 0);
            if (!string.IsNullOrWhiteSpace(projName))
            {
                lines.Add("");
                lines.Add("  [bold]🔬 Текущий проект Хранителя:[/]");
                lines.Add($"    [white]{Markup.Escape(projName)}[/]");
                if (!string.IsNullOrEmpty(projDesc))
                    lines.Add($"    [dim]{Markup.Escape(projDesc)}[/]");
                lines.Add($"    [dim]Прогресс по старой записи: {projProgress}%[/]");
            }
        }

        var completedTrackerProjects = !string.IsNullOrWhiteSpace(guardianId)
            ? CollectCompletedGuardianProjects(trackerRoot, guardianId)
            : new List<JsonElement>();
        if (completedTrackerProjects.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]✅ Завершённые проекты:[/] [dim]({completedTrackerProjects.Count})[/]");
            foreach (var completedProject in completedTrackerProjects)
            {
                var projectName = GetStr(completedProject, "projectName", GetStr(completedProject, "name", "?"));
                var finalState = GetStr(completedProject, "finalState", "");
                var finalStateLabel = string.IsNullOrWhiteSpace(finalState) ? "" : FormatGuardianProjectStateLabel(finalState);
                var completionTurn = GetInt(completedProject, "completionTurn", 0);
                var outcome = GetStr(completedProject, "outcome", "");
                var turnTag = completionTurn > 0 ? $" [dim](ход {completionTurn})[/]" : "";
                lines.Add($"    ✓ [white]{Markup.Escape(projectName)}[/] [dim]{Markup.Escape(finalStateLabel)}[/]{turnTag}");
                if (!string.IsNullOrWhiteSpace(outcome))
                    lines.Add($"      [dim italic]{Markup.Escape(outcome)}[/]");
                AppendProjectSystemEffectLines(lines, completedProject, "      ");
            }
        }
        else if (g.TryGetProperty("completedProjects", out var cProjects) && cProjects.ValueKind == JsonValueKind.Array && cProjects.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]✅ Завершённые проекты:[/] [dim]({cProjects.GetArrayLength()})[/]");
            foreach (var cp in cProjects.EnumerateArray())
            {
                var cpName = GetStr(cp, "projectName", "?");
                var cpOutcome = GetStr(cp, "outcome", "");
                var cpTurn = GetStr(cp, "completionTurn", "");
                var playerHelped = cp.TryGetProperty("playerInvolved", out var pi) && pi.ValueKind == JsonValueKind.True;
                var turnStr = !string.IsNullOrEmpty(cpTurn) ? $"[dim](ход {Markup.Escape(cpTurn)})[/] " : "";
                var helpTag = playerHelped ? " [cyan]★ вы помогали[/]" : "";
                lines.Add($"      ✓ {turnStr}[white]{Markup.Escape(cpName)}[/]{helpTag}");
                if (!string.IsNullOrEmpty(cpOutcome))
                    lines.Add($"        [dim italic]{Markup.Escape(cpOutcome)}[/]");
            }
        }

        // Active quests
        if (g.TryGetProperty("questManagement", out var qm) && qm.ValueKind == JsonValueKind.Object)
        {
            if (qm.TryGetProperty("activeQuests", out var activeQuests) && activeQuests.ValueKind == JsonValueKind.Array && activeQuests.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]📜 Активные задания:[/]");
                foreach (var q in activeQuests.EnumerateArray())
                {
                    var qName = GetStr(q, "name", "?");
                    var qDesc = GetStr(q, "description", "");
                    var qDiff = GetStr(q, "difficulty", "");
                    var qStatus = GetStr(q, "status", "");
                    var qTarget = GetStr(q, "targetWorld", "");
                    lines.Add($"    📋 [yellow]{Markup.Escape(qName)}[/]" +
                        (!string.IsNullOrEmpty(qDiff) ? $" [dim]({Markup.Escape(qDiff)})[/]" : "") +
                        (!string.IsNullOrEmpty(qStatus) ? $" [{(qStatus.ToLower().Contains("progress") ? "cyan" : "white")}]{Markup.Escape(qStatus)}[/]" : ""));
                    if (!string.IsNullOrEmpty(qDesc))
                        lines.Add($"       [white]{Markup.Escape(qDesc)}[/]");
                    if (!string.IsNullOrEmpty(qTarget))
                        lines.Add($"       🌍 Мир: [cyan]{Markup.Escape(qTarget)}[/]");
                    if (q.TryGetProperty("rewards", out var rew) && rew.ValueKind == JsonValueKind.Object)
                    {
                        var rewParts = new List<string>();
                        foreach (var rp in rew.EnumerateObject())
                        {
                            var rewardLabel = NpcFieldToRussian(rp.Name);
                            var rewardValue = DescribeQuestStructuredValue(rp.Value);
                            if (!string.IsNullOrWhiteSpace(rewardValue))
                                rewParts.Add($"{rewardLabel}: {rewardValue}");
                        }
                        if (rewParts.Count > 0)
                            lines.Add($"       🎁 Награды: [green]{Markup.Escape(string.Join(", ", rewParts))}[/]");
                    }
                }
            }

            if (qm.TryGetProperty("availableQuests", out var availableQuests) && availableQuests.ValueKind == JsonValueKind.Array && availableQuests.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]🧭 Доступные задания:[/]");
                foreach (var q in availableQuests.EnumerateArray())
                {
                    var qName = GetStr(q, "name", "?");
                    var qDesc = GetStr(q, "description", "");
                    var qDiff = GetStr(q, "difficulty", "");
                    var qTarget = GetStr(q, "targetWorld", "");
                    lines.Add($"    📋 [yellow]{Markup.Escape(qName)}[/]" +
                        (!string.IsNullOrEmpty(qDiff) ? $" [dim]({Markup.Escape(qDiff)})[/]" : ""));
                    if (!string.IsNullOrEmpty(qDesc))
                        lines.Add($"       [white]{Markup.Escape(qDesc)}[/]");
                    if (!string.IsNullOrEmpty(qTarget))
                        lines.Add($"       🌍 Мир: [cyan]{Markup.Escape(qTarget)}[/]");
                    if (q.TryGetProperty("rewards", out var rew) && rew.ValueKind == JsonValueKind.Object)
                    {
                        var rewParts = new List<string>();
                        foreach (var rp in rew.EnumerateObject())
                        {
                            var rewardLabel = NpcFieldToRussian(rp.Name);
                            var rewardValue = DescribeQuestStructuredValue(rp.Value);
                            if (!string.IsNullOrWhiteSpace(rewardValue))
                                rewParts.Add($"{rewardLabel}: {rewardValue}");
                        }
                        if (rewParts.Count > 0)
                            lines.Add($"       🎁 Награды: [green]{Markup.Escape(string.Join(", ", rewParts))}[/]");
                    }
                }
            }

            if (qm.TryGetProperty("completedQuests", out var cq) && cq.ValueKind == JsonValueKind.Array && cq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]✅ Выполненные задания:[/]");
                foreach (var q in cq.EnumerateArray())
                {
                    var qName = GetStr(q, "name", "?");
                    var qResult = GetStr(q, "result", "");
                    var qDate = GetStr(q, "completionDate", "");
                    var resultColor = qResult.ToLower().Contains("success") ? "green" : "white";
                    var dateStr = !string.IsNullOrEmpty(qDate) ? $" [dim]{Markup.Escape(qDate)}[/]" : "";
                    lines.Add($"    ✓ [dim]{Markup.Escape(qName)}[/] [{resultColor}]{Markup.Escape(qResult)}[/]{dateStr}");
                }
            }
        }

        // Gacha system
        var hasGachaSystem = g.TryGetProperty("gachaSystem", out var gs) && gs.ValueKind == JsonValueKind.Object;
        lines.Add("");
        lines.Add("  [bold]🎰 Система гача:[/]");
        var chargesPerReturn = hasGachaSystem && gs.TryGetProperty("chargesPerReturn", out var cpr) && cpr.ValueKind == JsonValueKind.Number && cpr.TryGetInt32(out var parsedCharges)
            ? parsedCharges
            : GuardianGachaChargeRules.GetChargesPerReturnForGuardian(g);
        var chargesUsedThisReturn = hasGachaSystem && gs.TryGetProperty("chargesUsedThisReturn", out var cur) && cur.ValueKind == JsonValueKind.Number && cur.TryGetInt32(out var parsedUsed)
            ? GuardianGachaChargeRules.ClampUsedCharges(parsedUsed, chargesPerReturn)
            : 0;
        var remainingCharges = Math.Max(0, chargesPerReturn - chargesUsedThisReturn);
        var founderExtraGachaChargesForReturn = PlayerGuardianFoundationState.GetFounderExtraGachaCharges(g);

        if (chargesPerReturn <= 0)
        {
            lines.Add("    [red]Гача через этого Хранителя сейчас заблокирована вашей репутацией.[/]");
        }
        else
        {
            lines.Add($"    Осталось попыток в этом возвращении: [yellow]{remainingCharges}[/]/[white]{chargesPerReturn}[/]");
            if (founderExtraGachaChargesForReturn > 0)
                lines.Add($"    [dim]Бонус основания: +{founderExtraGachaChargesForReturn} доп. попытка за возвращение.[/]");
            if (remainingCharges <= 0)
                lines.Add("    [yellow]Лимит гачи у этого Хранителя исчерпан до следующего возвращения из смертной жизни.[/]");
        }

        if (hasGachaSystem && gs.TryGetProperty("gachaHistory", out var gh) && gh.ValueKind == JsonValueKind.Array && gh.GetArrayLength() > 0)
        {
            lines.Add("    [dim]История призывов:[/]");
            foreach (var h in gh.EnumerateArray())
            {
                var relicId = GetStr(h, "relicId", "?");
                var cost = GetStr(h, "costInFeathers", GetStr(h, "cost", "?"));
                var rarity = GetStr(h, "finalRarity", "");
                var hTs = GetStr(h, "timestamp", "");
                var eventId = GetStr(h, "eventId", "");
                var timeStr = !string.IsNullOrEmpty(hTs) ? $"[dim]{Markup.Escape(hTs)}[/] " : "";
                var rarityTag = string.IsNullOrWhiteSpace(rarity) ? "" : $" [dim](редкость: {Markup.Escape(rarity)})[/]";
                lines.Add($"      {timeStr}💎 {Markup.Escape(relicId)} [dim](стоимость: {Markup.Escape(cost)})[/]{rarityTag}");
                if (!string.IsNullOrWhiteSpace(eventId))
                    lines.Add($"        [dim]eventId: {Markup.Escape(eventId)}[/]");
                if (h.TryGetProperty("gachaBonusAudit", out var bonusAudit) && bonusAudit.ValueKind == JsonValueKind.Object)
                {
                    var baseRarity = GetStr(bonusAudit, "baseRarity", "");
                    var finalRarity = GetStr(bonusAudit, "finalRarity", "");
                    var abodeSteps = GetStr(bonusAudit, "abodePowerBonusSteps", "0");
                    var forgeSteps = GetStr(bonusAudit, "relicForgingBonusSteps", "0");
                    var sourceProjectId = GetStr(bonusAudit, "sourceProjectId", "");
                    var sourcePart = string.IsNullOrWhiteSpace(sourceProjectId) ? string.Empty : $", sourceProjectId={sourceProjectId}";
                    lines.Add($"        [dim]gachaBonusAudit: base={Markup.Escape(baseRarity)}, abodeSteps={Markup.Escape(abodeSteps)}, forgeSteps={Markup.Escape(forgeSteps)}, final={Markup.Escape(finalRarity)}{Markup.Escape(sourcePart)}[/]");
                }
            }
        }

        // ── Social Profile (Block 32_ext.3) ──
        if (g.TryGetProperty("socialProfile", out var sp) && sp.ValueKind == JsonValueKind.Object)
        {
            FlushLines();
            content.AddRow(new Markup(""));
            content.AddRow(new Markup("  [bold magenta1]🧠 Социальный профиль:[/]"));

            var jealousy = GetInt(sp, "jealousyFactor", -1);
            var curiosity = GetInt(sp, "curiosityFactor", -1);
            var competitive = GetInt(sp, "competitiveFactor", -1);
            var generosity = GetInt(sp, "generosityFactor", -1);
            var isolationist = GetInt(sp, "isolationistTendency", -1);
            var socialTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 12, valueWidth: 4);

            void AddSocialBar(string label, string icon, int val, string lowDesc, string highDesc)
            {
                if (val < 0) return;
                var barW = 10;
                var filled = Math.Clamp(val * barW / 100, 0, barW);
                var color = val >= 70 ? "red" : val >= 40 ? "yellow" : "green";
                var desc2 = val >= 70 ? highDesc : val <= 30 ? lowDesc : "";
                var description = !string.IsNullOrEmpty(desc2) ? $"[dim]({Markup.Escape(desc2)})[/]" : new string(' ', 0);
                socialTable.AddRow(
                    new Markup($"{icon} {Markup.Escape(label)}"),
                    new Markup(ConsoleLayout.CreateBar(filled, barW, color)),
                    new Markup($"[{color}]{val}[/]"),
                    new Markup(description));
            }

            AddSocialBar("Ревность", "💚", jealousy, "не ревнует", "собственник");
            AddSocialBar("Любопытство", "🔍", curiosity, "безразличен", "жаждет информации");
            AddSocialBar("Конкуренция", "⚔", competitive, "спокоен", "агрессивно соперничает");
            AddSocialBar("Щедрость", "🎁", generosity, "расчётлив", "щедро одаривает");
            AddSocialBar("Изоляция", "🏔", isolationist, "социален", "хочет быть единственным");

            if (socialTable.Rows.Count > 0)
                content.AddRow(socialTable);
        }

        // ── Inter-Guardian Relationships (Block 32_ext.3) ──
        if (g.TryGetProperty("guardianRelationships", out var gRels) && gRels.ValueKind == JsonValueKind.Array && gRels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold steelblue1]🤝 Отношения с другими Хранителями:[/]");
            foreach (var rel in gRels.EnumerateArray())
            {
                var tgtName = GetStr(rel, "targetName", GetStr(rel, "targetGuardianId", "?"));
                // Try to resolve name from allGuardians
                if (allGuardians != null)
                {
                    var tgtId = GetStr(rel, "targetGuardianId", "");
                    foreach (var other in allGuardians)
                    {
                        if (GetStr(other, "guardianId", "") == tgtId)
                        {
                            tgtName = GetStr(other, "name", tgtName);
                            break;
                        }
                    }
                }
                var attitude = GetStr(rel, "attitudeTier", GetStr(rel, "attitude", ""));
                var attitudeScore = GetInt(rel, "attitudeScore", 0);
                var reason = GetStr(rel, "reason", "");
                var lastChangedAt = GetStr(rel, "lastChangedAt", "");
                var (attIcon, attColor, attRu) = attitude.ToLowerInvariant() switch
                {
                    "trusted" => ("✨", "springgreen2", "Глубокое доверие"),
                    "ally" => ("🤝", "green", "Союзник"),
                    "neutral" => ("😐", "grey", "Нейтрален"),
                    "competitive" => ("⚔", "yellow", "Конкурент"),
                    "rival" => ("⚔", "orange1", "Соперник"),
                    "enemy" => ("💀", "red", "Враг"),
                    _ => ("👤", "white", attitude)
                };
                var scoreText = attitudeScore > 0
                    ? $"+{attitudeScore}"
                    : attitudeScore.ToString();
                lines.Add($"    {attIcon} [{attColor}]{Markup.Escape(tgtName)}[/] — [{attColor}]{Markup.Escape(attRu)}[/] [dim]({Markup.Escape(scoreText)})[/]");
                if (!string.IsNullOrEmpty(reason))
                    lines.Add($"      [dim italic]{Markup.Escape(reason)}[/]");
                if (!string.IsNullOrEmpty(lastChangedAt) && lastChangedAt.Length >= 10)
                    lines.Add($"      [dim]Обновлено: {Markup.Escape(lastChangedAt)}[/]");
            }
        }

        // ── Reputation tier gate info ──
        if (rep >= 0)
        {
            if (ReputationDisplay.TryGetNextThreshold(ReputationScaleKind.Guardian, rep, out var nextTierName, out var nextTierRep))
            {
                var repLeft = Math.Max(0, nextTierRep - rep);
                lines.Add("");
                lines.Add($"  [dim]→ До ранга [white]{nextTierName}[/]: {repLeft} репутации[/]");
            }
        }

        // ── Mood ──
        if (g.TryGetProperty("mood", out var moodObj) && moodObj.ValueKind == JsonValueKind.Object)
        {
            var moodCurrent = GetStr(moodObj, "current", "");
            var moodIntensity = GetInt(moodObj, "intensity", 0);
            var moodReason = GetStr(moodObj, "reason", "");
            if (!string.IsNullOrEmpty(moodCurrent))
            {
                var (moodIcon, moodColor, moodRu) = moodCurrent.ToLowerInvariant() switch
                {
                    "welcoming" => ("🤗", "green", "Радушное"),
                    "contemplative" => ("🤔", "steelblue1", "Задумчивое"),
                    "energized" => ("⚡", "yellow", "Воодушевлённое"),
                    "melancholic" => ("😔", "grey", "Меланхоличное"),
                    "irritated" => ("😤", "red", "Раздражённое"),
                    "proud" => ("😊", "gold1", "Гордость"),
                    "suspicious" => ("🧐", "orange1", "Подозрительное"),
                    "playful" => ("😏", "mediumpurple2", "Игривое"),
                    "focused" => ("🎯", "cyan", "Сосредоточенное"),
                    "nostalgic" => ("🕰️", "wheat1", "Ностальгическое"),
                    _ => ("💭", "white", moodCurrent)
                };
                lines.Add("");
                var barW = 10;
                var filled = Math.Clamp(moodIntensity * barW / 100, 0, barW);
                var moodSince = GetInt(moodObj, "since", 0);
                var sinceTag = moodSince > 0 ? $" [dim](с хода {moodSince})[/]" : "";
                lines.Add($"  {moodIcon} Настроение: [{moodColor}]{Markup.Escape(moodRu)}[/]  [{moodColor}]{new string('█', filled)}[/][dim]{new string('░', barW - filled)}[/] [dim]{moodIntensity}%[/]{sinceTag}");
                if (!string.IsNullOrEmpty(moodReason))
                    lines.Add($"    [dim italic]{Markup.Escape(moodReason)}[/]");
            }
        }

        // ── Lore Fragments ──
        if (g.TryGetProperty("loreFragments", out var lore) && lore.ValueKind == JsonValueKind.Array && lore.GetArrayLength() > 0)
        {
            var unlockedLore = new List<JsonElement>();
            var lockedLore = new List<JsonElement>();
            foreach (var frag in lore.EnumerateArray())
            {
                var isUnlocked = frag.TryGetProperty("isUnlocked", out var ul) && ul.ValueKind == JsonValueKind.True;
                if (isUnlocked)
                    unlockedLore.Add(frag);
                else
                    lockedLore.Add(frag);
            }

            if (unlockedLore.Count > 0 || lockedLore.Count > 0)
            {
                lines.Add("");
                lines.Add($"  [bold]📜 Знания Хранителя[/] [dim]({unlockedLore.Count} открыто, {lockedLore.Count} скрыто)[/]");
                foreach (var frag in unlockedLore)
                {
                    var lTitle = GetStr(frag, "title", "???");
                    var lContent = GetStr(frag, "content", "");
                    var lCategory = GetStr(frag, "category", "");
                    var catIcon = lCategory switch
                    {
                        "personal_history" => "👤",
                        "cosmic_secret" => "🌌",
                        "domain_mastery" => "📚",
                        "lost_world" => "🌍",
                        "other_guardians" => "🛡️",
                        "soul_mechanics" => "✨",
                        _ => "📖"
                    };
                    lines.Add($"    {catIcon} [yellow]{Markup.Escape(lTitle)}[/]");
                    if (!string.IsNullOrEmpty(lContent))
                        lines.Add($"      [dim italic]{Markup.Escape(lContent)}[/]");
                }
                foreach (var frag in lockedLore)
                {
                    var reqRep = GetInt(frag, "requiredReputation", 0);
                    lines.Add($"    🔒 [dim]??? — требуется репутация {reqRep}+[/]");
                }
            }
        }

        // ── Musings (last 5) ──
        if (g.TryGetProperty("musings", out var musings) && musings.ValueKind == JsonValueKind.Array && musings.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]💭 Размышления[/] [dim]({musings.GetArrayLength()} записей)[/]");
            foreach (var m in musings.EnumerateArray())
            {
                var mTurn = GetStr(m, "turn", "");
                var mThought = GetStr(m, "thought", GetStr(m, "text", ""));
                var mTopic = GetStr(m, "topic", "");
                var mMood = GetStr(m, "mood", "");
                var topicIcon = mTopic switch
                {
                    "soul_assessment" => "👁️",
                    "domain_insight" => "📚",
                    "guardian_politics" => "🏛️",
                    "chaos_sea" => "🌊",
                    "personal_reflection" => "🪞",
                    "quest_planning" => "📋",
                    _ => "💭"
                };
                var turnTag = !string.IsNullOrEmpty(mTurn) ? $"[dim]#{Markup.Escape(mTurn)}[/] " : "";
                lines.Add($"    {topicIcon} {turnTag}[italic]{Markup.Escape(mThought)}[/]");
                if (!string.IsNullOrEmpty(mMood))
                    lines.Add($"      [dim]— {Markup.Escape(mMood)}[/]");
            }
        }

        if (guardianThoughtEntries.Count > 0)
        {
            lines.Add("");
            var shownThoughts = Math.Min(GuardianThoughtPreviewCount, guardianThoughtEntries.Count);
            lines.Add($"  [bold]🧠 Актуальные мысли Хранителя:[/] [dim](показано {shownThoughts} из {guardianThoughtEntries.Count})[/]");
            foreach (var entry in guardianThoughtEntries.Take(GuardianThoughtPreviewCount))
                lines.Add($"    • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");
        }

        if (guardianSocialEntries.Count > 0)
        {
            lines.Add("");
            var shownSocial = Math.Min(GuardianSocialPreviewCount, guardianSocialEntries.Count);
            lines.Add($"  [bold]📚 Краткая память общения:[/] [dim](показано {shownSocial} из {guardianSocialEntries.Count})[/]");
            foreach (var entry in guardianSocialEntries.Take(GuardianSocialPreviewCount))
                lines.Add($"    • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");
        }

        if (pendingGuardianTalkRequest != null || pendingGuardianLoreRequest != null)
        {
            lines.Add("");
            lines.Add("  [bold]⏳ Ожидают ответа GM:[/]");
            if (pendingGuardianTalkRequest != null)
                lines.Add($"    • Разговор [yellow]ожидает[/] [dim](идентификатор запроса: {Markup.Escape(pendingGuardianTalkRequest.RequestId)})[/]");
            if (pendingGuardianLoreRequest != null)
                lines.Add($"    • Вопрос о знаниях [yellow]ожидает[/] [dim](идентификатор запроса: {Markup.Escape(pendingGuardianLoreRequest.RequestId)})[/]");
        }

        // Description
        var desc = GetStr(g, "description", "");
        if (!string.IsNullOrWhiteSpace(manifestationAppearance))
        {
            lines.Add("");
            lines.Add("  [bold]🎭 Текущая форма проявления:[/]");
            lines.Add($"    [white]{Markup.Escape(manifestationAppearance)}[/]");
        }

        if (g.TryGetProperty("manifestationHistory", out var manifestationHistory) &&
            manifestationHistory.ValueKind == JsonValueKind.Array &&
            manifestationHistory.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]🪞 Прежние формы проявления[/] [dim]({manifestationHistory.GetArrayLength()} записей)[/]");
            foreach (var entry in manifestationHistory.EnumerateArray())
            {
                var oldDisplayName = GetStr(entry, "displayName", "???");
                var oldStyle = GuardianManifestation.GetPresentationStyleLabel(GetStr(entry, "presentationStyle", ""));
                var oldPronouns = GetStr(entry, "pronouns", "");
                var changedAt = GetStr(entry, "changedAtUtc", "");
                var reason = GetStr(entry, "reason", "");
                var timeTag = !string.IsNullOrWhiteSpace(changedAt) && changedAt.Length >= 10
                    ? $"[dim]{Markup.Escape(changedAt)}[/] "
                    : "";
                var detailParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(oldStyle))
                    detailParts.Add(oldStyle);
                if (!string.IsNullOrWhiteSpace(oldPronouns))
                    detailParts.Add(oldPronouns);
                var detailTag = detailParts.Count > 0
                    ? $" [dim]({Markup.Escape(string.Join(" • ", detailParts))})[/]"
                    : "";

                lines.Add($"    {timeTag}[white]{Markup.Escape(oldDisplayName)}[/]{detailTag}");
                if (!string.IsNullOrWhiteSpace(reason))
                    lines.Add($"      [dim italic]{Markup.Escape(reason)}[/]");
            }
        }

        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📖 {Markup.Escape(desc)}[/]");
        }

        var tradeAvailableHere = GuardianTradeAvailableHere(g, currentAbodeId);
        var tradeBlockedByReputation = rep <= -51;
        lines.Add("");
        lines.Add("  [bold]🛒 Локальная торговля:[/]");
        if (!tradeAvailableHere)
            lines.Add("    [dim]Доступна только в текущей обители Хранителя.[/]");
        else if (!isActiveGuardian)
            lines.Add("    [dim]Доступна только у текущего активного Хранителя в этой обители.[/]");
        else if (tradeBlockedByReputation)
            lines.Add("    [red]Хранитель отказывается торговать из-за вашей репутации.[/]");
        else
            lines.Add($"    [white]Доступна: {derivedState.TradeSlotCount} локальных слотов, обновление после нового возвращения из смертной жизни.[/]");

        FlushLines();

        Write(new Panel(content)
        {
            Header = new PanelHeader($" 🛡️ {Markup.Escape(name)}{(isActiveGuardian ? " · активный" : "")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("Полный JSON Хранителя", g, Color.Cyan1);

        await ShowGuardianDetailActions(g, name, currentAbodeId, activeGuardianId, guardianThoughtEntries, guardianSocialEntries, guardianProjectTrackerRoot);
    }

    private void ShowGuardianProjectDetailPanel(
        JsonElement entry,
        IReadOnlyDictionary<string, string> guardianNames,
        JsonElement? journalRoot,
        JsonElement? trackerRoot)
    {
        var guardianId = GetStr(entry, "guardianId", "");
        var guardianName = guardianNames.TryGetValue(guardianId, out var resolvedGuardianName) ? resolvedGuardianName : guardianId;
        var project = entry.GetProperty("project");
        var projectId = GetStr(project, "projectId", "");
        var projectName = GetStr(project, "projectName", GetStr(project, "name", "Проект"));
        var panelLines = new List<string>
        {
            $"[bold cyan]🔬 {Markup.Escape(projectName)}[/]",
            ""
        };

        var infoTable = ConsoleLayout.CreateInfoTable();
        if (!string.IsNullOrWhiteSpace(guardianName))
            infoTable.AddRow(new Markup("[white]Хранитель[/]"), new Markup($"[white]{Markup.Escape(guardianName)}[/]"));
        var projectType = GetStr(project, "projectType", "");
        if (!string.IsNullOrWhiteSpace(projectType))
            infoTable.AddRow(new Markup("[dim]Категория проекта[/]"), new Markup($"[dim]{Markup.Escape(FormatGuardianProjectTypeLabel(projectType))}[/]"));
        var projectTier = GetStr(project, "projectTier", "");
        if (!string.IsNullOrWhiteSpace(projectTier))
            infoTable.AddRow(new Markup("[dim]Масштаб[/]"), new Markup($"[dim]{Markup.Escape(FormatGuardianProjectTierLabel(projectTier))}[/]"));
        var projectMode = GetStr(project, "projectMode", "");
        if (!string.IsNullOrWhiteSpace(projectMode))
            infoTable.AddRow(new Markup("[dim]Характер проекта[/]"), new Markup($"[dim]{Markup.Escape(FormatGuardianProjectModeLabel(projectMode))}[/]"));
        var targetGuardianId = GetStr(project, "targetGuardianId", "");
        if (!string.IsNullOrWhiteSpace(targetGuardianId))
        {
            var targetGuardianName = guardianNames.TryGetValue(targetGuardianId, out var resolvedTargetName)
                ? resolvedTargetName
                : targetGuardianId;
            infoTable.AddRow(new Markup("[red]Цель[/]"), new Markup($"[red]{Markup.Escape(targetGuardianName)}[/]"));
        }

        var activeState = GetStr(project, "activeState", "");
        var finalState = GetStr(project, "finalState", "");
        var statusLabel = string.IsNullOrWhiteSpace(activeState) ? finalState : activeState;
        if (!string.IsNullOrWhiteSpace(statusLabel))
            infoTable.AddRow(new Markup("[yellow]Статус[/]"), new Markup($"[yellow]{Markup.Escape(FormatGuardianProjectStateLabel(statusLabel))}[/]"));

        if (infoTable.Rows.Count > 0)
            Write(infoTable);

        var description = GetStr(project, "description", "");
        if (!string.IsNullOrWhiteSpace(description))
        {
            panelLines.Add($"[dim]{Markup.Escape(description)}[/]");
            panelLines.Add("");
        }

        if (!string.IsNullOrWhiteSpace(activeState))
        {
            var startedTurn = GetInt(project, "startedTurn", 0);
            var estimatedCompletionTurn = GetInt(project, "estimatedCompletionTurn", 0);
            if (startedTurn > 0)
                panelLines.Add($"Ход начала: [white]{startedTurn}[/]");
            if (estimatedCompletionTurn > 0)
                panelLines.Add($"Ожидаемый ход завершения: [white]{estimatedCompletionTurn}[/]");
            if (project.TryGetProperty("playerCanAssist", out var playerCanAssistNode) &&
                (playerCanAssistNode.ValueKind == JsonValueKind.True || playerCanAssistNode.ValueKind == JsonValueKind.False))
            {
                panelLines.Add($"Помощь души: [white]{(playerCanAssistNode.GetBoolean() ? "доступна" : "недоступна")}[/]");
            }
            var assistDescription = GetStr(project, "assistDescription", "");
            if (!string.IsNullOrWhiteSpace(assistDescription))
                panelLines.Add($"Описание помощи: [dim]{Markup.Escape(assistDescription)}[/]");
            AppendGuardianProjectSummaryLines(panelLines, project, "");
        }
        else
        {
            var outcome = GetStr(project, "outcome", "");
            var completionTurn = GetInt(project, "completionTurn", 0);
            var abodePowerDelta = GetInt(project, "abodePowerDelta", 0);
            var startedTurn = GetInt(project, "startedTurn", 0);
            var estimatedCompletionTurn = GetInt(project, "estimatedCompletionTurn", 0);
            if (startedTurn > 0)
                panelLines.Add($"Ход начала: [white]{startedTurn}[/]");
            if (estimatedCompletionTurn > 0)
                panelLines.Add($"Ожидаемый ход завершения: [white]{estimatedCompletionTurn}[/]");
            if (completionTurn > 0)
                panelLines.Add($"Ход завершения: [white]{completionTurn}[/]");
            if (!string.IsNullOrWhiteSpace(finalState))
                panelLines.Add($"Конечное состояние: [white]{Markup.Escape(FormatGuardianProjectStateLabel(finalState))}[/]");
            if (!string.IsNullOrWhiteSpace(outcome))
                panelLines.Add($"Итог: [dim]{Markup.Escape(outcome)}[/]");
            if (abodePowerDelta != 0)
                panelLines.Add($"Влияние на силу Обители: [{(abodePowerDelta > 0 ? "green" : "red")}]{abodePowerDelta:+#;-#;0}[/]");
            AppendProjectSystemEffectLines(panelLines, project, "");
            AppendProjectEffectStateLines(panelLines, project, "");
        }

        AppendPoliticalProjectAuditLines(panelLines, project, guardianNames, "");

        var temporaryModifiers = GuardianProjectState.CollectActiveTemporaryModifiers(trackerRoot ?? default, guardianId);
        if (temporaryModifiers.Count > 0)
            AppendTemporaryModifierLines(panelLines, temporaryModifiers, "");

        var journalEntries = CollectGuardianProjectJournalEntries(journalRoot ?? default, guardianId, projectId);
        if (journalEntries.Count > 0)
        {
            panelLines.Add("");
            panelLines.Add("[bold]📜 Журнал проявлений:[/]");
            foreach (var journalEntry in journalEntries)
            {
                var turn = GetInt(journalEntry, "turn", 0);
                var title = GetStr(journalEntry, "title", "");
                var summary = GetStr(journalEntry, "summary", "");
                var turnTag = turn > 0 ? $"[dim](ход {turn})[/] " : "";
                panelLines.Add($"  • {turnTag}[white]{Markup.Escape(title)}[/]");
                if (!string.IsNullOrWhiteSpace(summary))
                    panelLines.Add($"    [dim]{Markup.Escape(summary)}[/]");
                if (journalEntry.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.ValueKind == JsonValueKind.String)
                            panelLines.Add($"    [dim]- {Markup.Escape(FormatProjectJournalDetailLine(detail.GetString() ?? ""))}[/]");
                    }
                }
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", panelLines)))
        {
            Header = new PanelHeader(" 🔬 Проект Хранителя ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("Полный JSON проекта и effectState", entry, Color.Cyan1);
        if (journalRoot.HasValue)
            WriteJsonAuditPanel("Полный JSON журнала проектов", journalRoot.Value, Color.Cyan1);
        WaitForKey();
    }

    private static Dictionary<string, string> BuildGuardianNameMap(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
            return result;

        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
            {
                var guardianId = GetStr(guardian, "guardianId", "");
                var displayName = GuardianManifestation.GetDisplayName(guardian);
                if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(displayName))
                    result[guardianId] = displayName;
            }
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
        {
            var guardianId = GetStr(activeGuardian, "guardianId", "");
            var displayName = GuardianManifestation.GetDisplayName(activeGuardian);
            if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(displayName))
                result[guardianId] = displayName;
        }

        return result;
    }

    private static List<JsonElement> CollectGuardianProjectEntries(JsonElement root, string propName)
    {
        var result = new List<JsonElement>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propName, out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object)
                result.Add(entry);
        }

        return result;
    }

    private static bool TryGetActiveGuardianProject(JsonElement root, string guardianId, out JsonElement project)
    {
        project = default;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("activeProjects", out var activeProjects) ||
            activeProjects.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entry in activeProjects.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var projectNode) ||
                projectNode.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            project = projectNode;
            return true;
        }

        return false;
    }

    private static List<JsonElement> CollectCompletedGuardianProjects(JsonElement root, string guardianId)
    {
        var result = new List<JsonElement>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("completedProjects", out var completedProjects) ||
            completedProjects.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var projectNode) ||
                projectNode.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(projectNode);
        }

        return result
            .OrderByDescending(project => GetInt(project, "completionTurn", 0))
            .ToList();
    }

    private static List<JsonElement> CollectGuardianProjectJournalEntries(JsonElement root, string guardianId, string projectId)
    {
        var result = new List<JsonElement>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetStr(entry, "projectId", ""), projectId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(entry);
        }

        return result
            .OrderByDescending(entry => GetInt(entry, "turn", 0))
            .ToList();
    }

    private static List<JsonElement> CollectGuardianPowerJournalEntries(JsonElement root, string guardianId)
    {
        var result = new List<JsonElement>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(entry);
        }

        return result
            .OrderByDescending(entry => GetInt(entry, "turn", 0))
            .ThenByDescending(entry => GetStr(entry, "appliedAt", ""))
            .ToList();
    }

    private static void AppendGuardianProjectSummaryLines(List<string> lines, JsonElement project, string indent)
    {
        var projectName = GetStr(project, "projectName", GetStr(project, "name", "Проект"));
        var activeState = GetStr(project, "activeState", "");
        var totalWork = GetInt(project, "totalWork", 0);
        var workDone = GetInt(project, "workDone", 0);
        var totalStages = GetInt(project, "totalStages", 0);
        var currentStage = GetInt(project, "currentStage", 0);
        var pressure = GetInt(project, "pressure", 0);
        var stability = GetInt(project, "stability", 0);
        if (!string.IsNullOrWhiteSpace(projectName))
            lines.Add($"{indent}[white]{Markup.Escape(projectName)}[/]");
        if (!string.IsNullOrWhiteSpace(activeState))
            lines.Add($"{indent}Статус: [yellow]{Markup.Escape(FormatGuardianProjectStateLabel(activeState))}[/]");
        if (totalWork > 0)
        {
            var normalized = Math.Clamp(workDone * 18 / Math.Max(1, totalWork), 0, 18);
            lines.Add($"{indent}Работа: [cyan]{new string('━', normalized)}[/][dim]{new string('┄', 18 - normalized)}[/] [white]{workDone}[/]/[dim]{totalWork}[/]");
        }
        if (totalStages > 0)
            lines.Add($"{indent}Стадия: [white]{currentStage}[/]/[dim]{totalStages}[/]");
        lines.Add($"{indent}[dim]{Markup.Escape(FormatProjectPressureLabel(pressure))}[/]  •  [dim]{Markup.Escape(FormatProjectStabilityLabel(stability))}[/]");
    }

    private static void AppendProjectSystemEffectLines(List<string> lines, JsonElement project, string indent)
    {
        if (!project.TryGetProperty("systemEffectSummary", out var effectSummary) || effectSummary.ValueKind != JsonValueKind.Array)
            return;

        foreach (var effect in effectSummary.EnumerateArray())
        {
            if (effect.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(effect.GetString()))
                lines.Add($"{indent}[dim]Эффект: {Markup.Escape(effect.GetString()!)}[/]");
        }
    }

    private static void AppendProjectEffectStateLines(List<string> lines, JsonElement project, string indent)
    {
        if (!project.TryGetProperty("effectState", out var effectState) || effectState.ValueKind != JsonValueKind.Object)
            return;

        var projectType = GetStr(project, "projectType", "");
        switch (projectType)
        {
            case "relic_forging":
            {
                var tradeGranted = GetInt(effectState, "tradeRefreshUsesGranted", 0);
                var tradeSpent = GetInt(effectState, "tradeRefreshUsesSpent", 0);
                var gachaGranted = GetInt(effectState, "gachaUsesGranted", 0);
                var gachaSpent = GetInt(effectState, "gachaUsesSpent", 0);
                lines.Add($"{indent}[bold]Временный эффект:[/] [dim]Дополнительных обновлений витрины {Math.Max(0, tradeGranted - tradeSpent)}/{tradeGranted} • Дополнительных попыток гачи {Math.Max(0, gachaGranted - gachaSpent)}/{gachaGranted}[/]");
                break;
            }
            case "lore_research":
            {
                var hookGranted = GetInt(effectState, "questHookTokensGranted", 0);
                var hookSpent = GetInt(effectState, "questHookTokensSpent", 0);
                var archiveQuestGranted = GetInt(effectState, "guaranteedArchiveQuestGranted", 0);
                var archiveQuestConsumed = GetInt(effectState, "guaranteedArchiveQuestConsumed", 0);
                var specialGranted = GetInt(effectState, "specialQuestLineTokensGranted", 0);
                var specialSpent = GetInt(effectState, "specialQuestLineTokensSpent", 0);
                var clueGranted = GetInt(effectState, "visibleRivalClueBudgetGranted", 0);
                var clueSpent = GetInt(effectState, "visibleRivalClueBudgetSpent", 0);
                var warningBonus = GetInt(effectState, "archiveWarningTierBonusGranted", 0);
                var targetIncarnation = GetInt(effectState, "targetIncarnation", 0);
                var targetText = targetIncarnation > 0 ? $" • Для жизни #{targetIncarnation}" : "";
                lines.Add($"{indent}[bold]Остаток эффекта:[/] [dim]Квестовых зацепок {Math.Max(0, hookGranted - hookSpent)}/{hookGranted} • Архивных квестов {Math.Max(0, archiveQuestGranted - archiveQuestConsumed)}/{archiveQuestGranted} • Особых сюжетных линий {Math.Max(0, specialGranted - specialSpent)}/{specialGranted} • Явных следов соперника {Math.Max(0, clueGranted - clueSpent)}/{clueGranted} • Предупреждение +{warningBonus}{targetText}[/]");
                break;
            }
            case "soul_preparation":
            {
                var prepGranted = GetInt(effectState, "preparationBudgetPointsGranted", 0);
                var prepSpent = GetInt(effectState, "preparationBudgetPointsSpent", 0);
                var hostileGranted = GetInt(effectState, "hostilePriorityTokensGranted", 0);
                var hostileSpent = GetInt(effectState, "hostilePriorityTokensSpent", 0);
                var consumed = effectState.TryGetProperty("consumedAtLifeStart", out var consumedNode) && consumedNode.ValueKind == JsonValueKind.True;
                var targetIncarnation = GetInt(effectState, "targetIncarnation", 0);
                var targetText = targetIncarnation > 0 ? $" • Для жизни #{targetIncarnation}" : "";
                lines.Add($"{indent}[bold]Остаток эффекта:[/] [dim]Очков бюджета подготовки {Math.Max(0, prepGranted - prepSpent)}/{prepGranted} • Враждебных жетонов приоритета {Math.Max(0, hostileGranted - hostileSpent)}/{hostileGranted} • Израсходовано {(consumed ? "да" : "нет")}{targetText}[/]");
                break;
            }
        }
    }

    private void ShowGuardianJournalDetailPanel(
        string guardianName,
        IReadOnlyList<JsonElement> guardianThoughtEntries,
        IReadOnlyList<JsonElement> guardianSocialEntries)
    {
        var lines = new List<string>
        {
            $"[bold cyan]📚 Полный журнал Хранителя «{Markup.Escape(guardianName)}»[/]"
        };

        if (guardianThoughtEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]🧠 Все актуальные мысли:[/] [dim]({guardianThoughtEntries.Count} записей)[/]");
            foreach (var entry in guardianThoughtEntries)
                AppendGuardianJournalDetailEntryLines(lines, entry);
        }

        if (guardianSocialEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]📚 Вся память общения:[/] [dim]({guardianSocialEntries.Count} записей)[/]");
            foreach (var entry in guardianSocialEntries)
                AppendGuardianJournalDetailEntryLines(lines, entry);
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Журнал Хранителя ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        if (guardianThoughtEntries.Count > 0)
            WriteJsonAuditPanel("Полный JSON thought journal Хранителя", BuildActorJournalEntriesAuditArray(guardianThoughtEntries), Color.Cyan1);
        if (guardianSocialEntries.Count > 0)
            WriteJsonAuditPanel("Полный JSON social journal Хранителя", BuildActorJournalEntriesAuditArray(guardianSocialEntries), Color.Cyan1);
    }

    private static void AppendGuardianJournalDetailEntryLines(List<string> lines, JsonElement entry)
    {
        lines.Add($"  • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");

        var eventType = GetStr(entry, "eventType", "");
        if (!string.IsNullOrWhiteSpace(eventType))
            lines.Add($"    [dim]eventType: {Markup.Escape(eventType)} ({Markup.Escape(DescribeActorJournalEventType(eventType))})[/]");

        var entryId = GetStr(entry, "entryId", "");
        if (!string.IsNullOrWhiteSpace(entryId))
            lines.Add($"    [dim]Идентификатор записи: {Markup.Escape(entryId)}[/]");

        var guardianId = GetStr(entry, "guardianId", "");
        if (!string.IsNullOrWhiteSpace(guardianId))
            lines.Add($"    [dim]Идентификатор Хранителя: {Markup.Escape(guardianId)}[/]");

        var timestamp = GetStr(entry, "timestamp", "");
        if (!string.IsNullOrWhiteSpace(timestamp))
            lines.Add($"    [dim]Время: {Markup.Escape(timestamp)}[/]");

        var requestId = GetStr(entry, "requestId", "");
        if (!string.IsNullOrWhiteSpace(requestId))
            lines.Add($"    [dim]Идентификатор запроса: {Markup.Escape(requestId)}[/]");

        var interactionType = GetStr(entry, "interactionType", "");
        if (!string.IsNullOrWhiteSpace(interactionType))
            lines.Add($"    [dim]Тип взаимодействия: {Markup.Escape(DescribeGuardianInteractionType(interactionType))}[/]");

        var status = GetStr(entry, "status", "");
        if (!string.IsNullOrWhiteSpace(status))
            lines.Add($"    [dim]Статус: {Markup.Escape(DescribeGuardianJournalStatus(status))}[/]");

        var responseMode = GetStr(entry, "responseMode", "");
        if (!string.IsNullOrWhiteSpace(responseMode))
            lines.Add($"    [dim]Режим ответа: {Markup.Escape(DescribeGuardianResponseMode(responseMode))}[/]");

        var consequence = GetStr(entry, "consequence", "");
        if (!string.IsNullOrWhiteSpace(consequence))
            lines.Add($"    [dim]Последствие: {Markup.Escape(consequence)}[/]");

        var attitude = GetStr(entry, "attitude", "");
        if (!string.IsNullOrWhiteSpace(attitude))
            lines.Add($"    [dim]Отношение: {Markup.Escape(attitude)}[/]");

        var intent = GetStr(entry, "intent", "");
        if (!string.IsNullOrWhiteSpace(intent))
            lines.Add($"    [dim]Намерение: {Markup.Escape(intent)}[/]");

        if (entry.TryGetProperty("tags", out var tagsNode) && tagsNode.ValueKind == JsonValueKind.Array)
        {
            var tags = tagsNode.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList();
            if (tags.Count > 0)
                lines.Add($"    [dim]Метки: {Markup.Escape(string.Join(", ", tags))}[/]");
        }
    }

    private static JsonArray BuildActorJournalEntriesAuditArray(IEnumerable<JsonElement> entries)
    {
        var array = new JsonArray();
        foreach (var entry in entries)
        {
            var node = JsonNode.Parse(entry.GetRawText());
            if (node != null)
                array.Add(node);
        }

        return array;
    }

    private static bool GuardianTradeAvailableHere(JsonElement guardian, string currentAbodeId)
    {
        if (string.IsNullOrWhiteSpace(currentAbodeId))
            return false;

        if (!guardian.TryGetProperty("abode", out var abode) || abode.ValueKind != JsonValueKind.Object)
            return false;

        var abodeId = GetStr(abode, "abodeId", "");
        return !string.IsNullOrWhiteSpace(abodeId) &&
               string.Equals(abodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ShowGuardianDetailActions(
        JsonElement guardian,
        string guardianName,
        string currentAbodeId,
        string activeGuardianId,
        IReadOnlyList<JsonElement> guardianThoughtEntries,
        IReadOnlyList<JsonElement> guardianSocialEntries,
        JsonElement? guardianProjectTrackerRoot)
    {
        var imagePrompt = GetStr(guardian, "image_prompt", "");
        var guardianId = GetStr(guardian, "guardianId", "");
        var guardianImageKey = GetStr(guardian, "guardianId", guardianName);
        var abodeImagePrompt = "";
        var abodeImageKey = "";
        if (guardian.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
        {
            abodeImagePrompt = GetStr(abode, "image_prompt", "");
            abodeImageKey = GetStr(abode, "abodeId", GetStr(abode, "name", $"{guardianImageKey}_abode"));
        }
        var tradeAvailable = GuardianTradeAvailableHere(guardian, currentAbodeId) &&
                             string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase) &&
                             GetInt(guardian.TryGetProperty("relationshipData", out var rd) ? rd : guardian, "currentReputation", GetInt(guardian, "reputation", 0)) > -51;
        var socialAvailable = GuardianTradeAvailableHere(guardian, currentAbodeId) &&
                              string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase) &&
                              !string.IsNullOrWhiteSpace(guardianId);
        var hasGuardianJournalEntries = guardianThoughtEntries.Count > 0 || guardianSocialEntries.Count > 0;
        var trackerRoot = guardianProjectTrackerRoot ?? default;
        var hasGuardianProjects = !string.IsNullOrWhiteSpace(guardianId) &&
                                  (TryGetActiveGuardianProject(trackerRoot, guardianId, out _) ||
                                   CollectCompletedGuardianProjects(trackerRoot, guardianId).Count > 0);

        var hasGuardianImagePrompt = !string.IsNullOrWhiteSpace(imagePrompt);
        var hasExistingGuardianImage = _imageService?.EntityImageExists("guardian", guardianImageKey) == true;
        var hasImageSupport = _imageService != null && (hasGuardianImagePrompt || hasExistingGuardianImage);
        var hasAbodeImagePrompt = !string.IsNullOrWhiteSpace(abodeImagePrompt);
        var hasExistingAbodeImage = _imageService?.EntityImageExists("abode", abodeImageKey) == true;
        var hasAbodeImageSupport = _imageService != null && (hasAbodeImagePrompt || hasExistingAbodeImage);
        if (!tradeAvailable && !socialAvailable && !hasImageSupport && !hasAbodeImageSupport && !hasGuardianJournalEntries && !hasGuardianProjects)
        {
            WaitForKey();
            return;
        }

        while (true)
        {
            var pendingGuardianTalkRequest = socialAvailable
                ? await ActorSocialInteractionRequestState.FindPendingGuardianRequestAsync(_fs, guardianId, ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)
                : null;
            var pendingGuardianLoreRequest = socialAvailable
                ? await ActorSocialInteractionRequestState.FindPendingGuardianRequestAsync(_fs, guardianId, ActorSocialInteractionRequestState.GuardianInteractionTypeLore)
                : null;

            var actions = new List<string>();
            if (socialAvailable)
            {
                actions.Add(pendingGuardianTalkRequest == null ? "💬 Поговорить" : "[dim]💬 Разговор ожидает ответа GM[/]");
                actions.Add(pendingGuardianLoreRequest == null ? "📜 Спросить о знаниях" : "[dim]📜 Вопрос о знаниях ожидает ответа GM[/]");
            }
            if (tradeAvailable)
                actions.Add("🛒 Торговать");
            if (GuardianTradeAvailableHere(guardian, currentAbodeId))
                actions.Add("🏛 Обитатели Обители");
            if (hasGuardianProjects)
                actions.Add("🔬 Открыть проекты Хранителя");
            if (guardianThoughtEntries.Count > 0 || guardianSocialEntries.Count > 0)
                actions.Add("📚 Показать весь журнал Хранителя");

            if (hasImageSupport)
            {
                var hasImage = _imageService!.EntityImageExists("guardian", guardianImageKey);
                actions.Add(hasImage ? "🖼 Показать сохранённое изображение хранителя" : "🖼 Показать/создать изображение хранителя");
                if (hasImage)
                    actions.Add("💾 Экспортировать изображение хранителя");
                if (hasImage && hasGuardianImagePrompt)
                    actions.Add("♻ Пересоздать изображение хранителя");
            }

            if (hasAbodeImageSupport)
            {
                var hasAbodeImage = _imageService!.EntityImageExists("abode", abodeImageKey);
                actions.Add(hasAbodeImage ? "🏛 Показать сохранённое изображение обители" : "🏛 Показать/создать изображение обители");
                if (hasAbodeImage)
                    actions.Add("🏛 💾 Экспортировать изображение обители");
                if (hasAbodeImage && hasAbodeImagePrompt)
                    actions.Add("🏛 ♻ Пересоздать изображение обители");
            }

            actions.Add("← Назад");

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actions));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Поговорить", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingGuardianTalkRequest != null)
                {
                    MarkupLine("[yellow]Уже есть незакрытый разговор с этим Хранителем. Дождитесь ответа GM.[/]");
                    return;
                }

                var request = new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
                {
                    GuardianId = guardianId,
                    GuardianName = guardianName,
                    InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeTalk,
                    CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
                };
                var actionText =
                    $"[GUARDIAN_SOCIAL_TALK_REQUEST] Игрок обращается к Хранителю '{guardianName}' (guardianId={guardianId}, requestId={request.RequestId}) с обычным разговором. " +
                    "В следующем подтверждённом ходе отыграй сцену и обязательно закрой этот разговор записью в журнале общения Хранителя с requestId, guardianId, interactionType=talk, status=accepted|rejected|cancelled, optional responseMode, title, summary, turn и timestamp. " +
                    "Журнал мыслей Хранителя остаётся рекомендуемым дополнением, но запись о закрытии разговора обязательна.";
                var lines = new List<string>
                {
                    "[bold cyan]Разговор с Хранителем[/]",
                    "",
                    $"  Guardian: [white]{Markup.Escape(guardianName)}[/] [dim]({Markup.Escape(guardianId)})[/]",
                    $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
                    $"  interactionType: [dim]{ActorSocialInteractionRequestState.GuardianInteractionTypeTalk}[/]",
                    "",
                    "[bold]Техническое закрытие ГМ:[/]",
                    "  • Закрыть через guardian social journal receipt.",
                    "  • Обязательные поля: requestId, guardianId, interactionType=talk, status, title, summary, turn, timestamp.",
                    "  • status: accepted | rejected | cancelled.",
                    "  • guardian thought journal optional, но social closure entry обязательна."
                };
                AppendChaosSeaPendingFileRule(lines, ActorSocialInteractionRequestState.PendingGuardianRequestPath);
                AppendChaosSeaCommonContractRules(lines);
                if (!ConfirmChaosSeaContractPreview(
                        "Полный предпросмотр разговора с Хранителем",
                        lines,
                        ToChaosSeaAuditNode(request),
                        "Полный JSON pending guardian social request"))
                {
                    return;
                }

                await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, request);
                _pendingGmAction = actionText;
                MarkupLine("[cyan]Разговор с Хранителем отправлен GM.[/]");
                return;
            }

            if (action.Contains("знания", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingGuardianLoreRequest != null)
                {
                    MarkupLine("[yellow]Уже есть незакрытый вопрос о знаниях для этого Хранителя. Дождитесь ответа GM.[/]");
                    return;
                }

                var request = new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
                {
                    GuardianId = guardianId,
                    GuardianName = guardianName,
                    InteractionType = ActorSocialInteractionRequestState.GuardianInteractionTypeLore,
                    CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
                };
                var actionText =
                    $"[GUARDIAN_SOCIAL_LORE_REQUEST] Игрок просит Хранителя '{guardianName}' (guardianId={guardianId}, requestId={request.RequestId}) поделиться знанием или лором. " +
                    "В следующем подтверждённом ходе отыграй сцену и обязательно закрой этот запрос записью в журнале общения Хранителя с requestId, guardianId, interactionType=lore, status=accepted|rejected|cancelled, optional responseMode=lore_revealed|lore_refused|warning|refusal, title, summary, turn и timestamp. " +
                    "Если знание действительно раскрыто, при необходимости добавь журнал мыслей Хранителя и связанные игровые последствия отдельно от самой записи о закрытии запроса.";
                var lines = new List<string>
                {
                    "[bold cyan]Вопрос Хранителю о знании[/]",
                    "",
                    $"  Guardian: [white]{Markup.Escape(guardianName)}[/] [dim]({Markup.Escape(guardianId)})[/]",
                    $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
                    $"  interactionType: [dim]{ActorSocialInteractionRequestState.GuardianInteractionTypeLore}[/]",
                    "",
                    "[bold]Техническое закрытие ГМ:[/]",
                    "  • Закрыть через guardian social journal receipt.",
                    "  • Обязательные поля: requestId, guardianId, interactionType=lore, status, responseMode, title, summary, turn, timestamp.",
                    "  • responseMode whitelist: lore_revealed | lore_refused | warning | refusal.",
                    "  • Если лор раскрыт, все игровые последствия оформляются отдельными canonical updates."
                };
                AppendChaosSeaPendingFileRule(lines, ActorSocialInteractionRequestState.PendingGuardianRequestPath);
                AppendChaosSeaCommonContractRules(lines);
                if (!ConfirmChaosSeaContractPreview(
                        "Полный предпросмотр вопроса о знании",
                        lines,
                        ToChaosSeaAuditNode(request),
                        "Полный JSON pending guardian lore request"))
                {
                    return;
                }

                await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, request);
                _pendingGmAction = actionText;
                MarkupLine("[cyan]Вопрос о знании отправлен GM.[/]");
                return;
            }

            if (action.Contains("Торговать"))
            {
                if (!string.IsNullOrWhiteSpace(guardianId))
                    await ShowGuardianTradePanel(guardianId);
                return;
            }

            if (action.Contains("Обитатели", StringComparison.OrdinalIgnoreCase))
            {
                await ShowGuardianAbodeResidentsPanel(guardian);
                return;
            }

            if (action.Contains("Открыть проекты Хранителя", StringComparison.OrdinalIgnoreCase))
            {
                await ShowGuardianProjectsForGuardianAsync(guardianId, guardianName, trackerRoot);
                continue;
            }

            if (action.Contains("Показать весь журнал Хранителя", StringComparison.OrdinalIgnoreCase))
            {
                ShowGuardianJournalDetailPanel(guardianName, guardianThoughtEntries, guardianSocialEntries);
                WaitForKey();
                continue;
            }

            if (action.Contains("обители", StringComparison.OrdinalIgnoreCase))
            {
                var abodeImageExists = _imageService!.EntityImageExists("abode", abodeImageKey);
                if (action.Contains("Экспортировать", StringComparison.OrdinalIgnoreCase) && abodeImageExists)
                    await ExportEntityImageAsync("abode", abodeImageKey);
                else if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && abodeImageExists && hasAbodeImagePrompt)
                    await RegenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey);
                else if (abodeImageExists)
                    _imageService.ShowEntityImage("abode", abodeImageKey, forceDisplay: true);
                else if (hasAbodeImagePrompt)
                    await _imageService.ShowOrGenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey, forceDisplay: true);
                else
                    MarkupLine("[yellow]Сохранённое изображение обители не найдено.[/]");
                WaitForKey();
                return;
            }

            if (!hasImageSupport)
                continue;

            var imageExists = _imageService!.EntityImageExists("guardian", guardianImageKey);
            if (action.Contains("Экспортировать", StringComparison.OrdinalIgnoreCase) && imageExists)
            {
                await ExportEntityImageAsync("guardian", guardianImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && imageExists && hasGuardianImagePrompt)
            {
                await RegenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Показать", StringComparison.OrdinalIgnoreCase))
            {
                if (imageExists)
                    _imageService.ShowEntityImage("guardian", guardianImageKey, forceDisplay: true);
                else if (hasGuardianImagePrompt)
                    await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }
        }
    }

    private async Task ShowGuardianProjectsForGuardianAsync(string guardianId, string guardianName, JsonElement trackerRoot)
    {
        if (string.IsNullOrWhiteSpace(guardianId) || trackerRoot.ValueKind != JsonValueKind.Object)
        {
            ShowEmptyPanel("Проекты Хранителя", "У этого Хранителя пока нет доступных проектов.");
            return;
        }

        var entries = CollectGuardianProjectEntries(trackerRoot, "activeProjects")
            .Concat(CollectGuardianProjectEntries(trackerRoot, "completedProjects"))
            .Where(entry => string.Equals(GetStr(entry, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (entries.Count == 0)
        {
            ShowEmptyPanel("Проекты Хранителя", "У этого Хранителя пока нет доступных проектов.");
            return;
        }

        var journalDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.JournalPath);
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var guardianNames = guardiansDoc != null
            ? BuildGuardianNameMap(guardiansDoc.RootElement)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        guardianNames[guardianId] = guardianName;

        while (true)
        {
            var choices = entries.Select(entry =>
            {
                var project = entry.GetProperty("project");
                var projectName = GetStr(project, "projectName", GetStr(project, "name", "Проект"));
                var projectId = GetStr(project, "projectId", "");
                var activeState = GetStr(project, "activeState", "");
                var finalState = GetStr(project, "finalState", "");
                var status = FormatGuardianProjectStateLabel(string.IsNullOrWhiteSpace(activeState) ? finalState : activeState);
                return ConsoleLayout.PlainChoiceLabel(
                    $"🔬 {projectName}",
                    guardianName,
                    string.IsNullOrWhiteSpace(projectId) ? "" : $"projectId={projectId}",
                    $"guardianId={guardianId}",
                    string.IsNullOrWhiteSpace(status) ? "" : status);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🔬 Проекты Хранителя {Markup.Escape(guardianName)}[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
                return;

            ShowGuardianProjectDetailPanel(entries[selectedIndex], guardianNames, journalDoc?.RootElement, trackerRoot);
        }
    }

    private async Task ShowGuardianAbodeResidentsPanel(JsonElement guardian)
    {
        var guardianId = GetStr(guardian, "guardianId", "");
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        if (guardian.TryGetProperty("abode", out var abode) is false || abode.ValueKind != JsonValueKind.Object)
        {
            ShowEmptyPanel("Обитатели Обители", "У этого Хранителя ещё нет материализованной Обители.");
            return;
        }

        var abodeId = GetStr(abode, "abodeId", "");
        var abodeName = GetStr(abode, "name", "Обитель");
        var currentAbodePower = AbodePowerRules.GetCurrentPower(guardian);
        if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(abodeId))
        {
            ShowEmptyPanel("Обитатели Обители", "Обитель ещё не проявлена достаточно явно, чтобы открыть состав обитателей.");
            return;
        }

        while (true)
        {
            var residentsDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
            var residents = residentsDoc != null
                ? GuardianAbodeResidentState.CollectEntries(residentsDoc.RootElement, guardianId, abodeId, currentAbodePower, presentOnly: false)
                : new List<GuardianAbodeResidentState.ResidentEntry>();
            var isFoundedGuardian = PlayerGuardianFoundationState.IsPlayerFoundedGuardian(guardian);
            var founderFeatureTitle = PlayerGuardianFoundationState.GetFounderAbodeFeatureTitle(guardian);
            var founderFeatureSummary = PlayerGuardianFoundationState.GetFounderAbodeFeatureSummary(guardian);

            if (residents.Count == 0)
            {
                if (await GuardianAbodeResidentRequestState.IsResidentsRequestFileMalformedAsync(_fs))
                {
                    ShowEmptyPanel("Обитатели Обители", "pending_guardian_abode_residents_request.json повреждён. Новый запрос состава заблокирован, пока pending bundle не будет исправлен или очищен.");
                    WaitForKey();
                    return;
                }

                var pendingRequests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
                var matchingPendingRequests = pendingRequests.Where(pendingRequest =>
                    string.Equals(pendingRequest.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pendingRequest.AbodeId, abodeId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchesCurrentRequest = matchingPendingRequests.Count > 0;

                if (!matchesCurrentRequest)
                {
                    var reputation = guardian.TryGetProperty("relationshipData", out var relationshipData) && relationshipData.ValueKind == JsonValueKind.Object
                        ? GetInt(relationshipData, "currentReputation", 0)
                        : GetInt(guardian, "reputation", 0);
                    var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
                    {
                        GuardianId = guardianId,
                        GuardianName = guardianName,
                        AbodeId = abodeId,
                        AbodeName = abodeName,
                        CurrentReputation = reputation,
                        RequestMode = isFoundedGuardian
                            ? GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction
                            : GuardianAbodeResidentRequestState.ResidentsRequestModeStandardRoster,
                        FounderFeatureTitle = isFoundedGuardian ? founderFeatureTitle : null,
                        FounderFeatureSummary = isFoundedGuardian ? founderFeatureSummary : null,
                        CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
                    };
                    var rosterLines = new List<string>
                    {
                        "[bold cyan]Запрос состава обитателей Обители[/]",
                        "",
                        $"  Хранитель: [white]{Markup.Escape(guardianName)}[/] [dim]({Markup.Escape(guardianId)})[/]",
                        $"  Обитель: [white]{Markup.Escape(abodeName)}[/] [dim]({Markup.Escape(abodeId)})[/]",
                        $"  currentReputation: [dim]{reputation}[/]",
                        $"  requestMode: [dim]{Markup.Escape(request.RequestMode)}[/]",
                        "",
                        "[bold]Контракт материализации для ГМ:[/]",
                        "  • Создать явный guardian_abode_residents.json roster для указанной Обители.",
                        "  • Каждый resident должен иметь полный canonical resident object: residentKind, originType, bond/devotion/restlessness/migration, isPresent, futureCompanionPrompt и т.д.",
                        "  • Старые жители других Обителей не переносятся автоматически.",
                        "  • Закрытие запроса должно оставить state, который валидатор сможет связать с guardianId/abodeId/requestId."
                    };
                    if (isFoundedGuardian)
                    {
                        rosterLines.Add("");
                        rosterLines.Add("[bold]Основанная мантия:[/]");
                        rosterLines.Add($"  • Дар основания: [white]{Markup.Escape(founderFeatureTitle)}[/]");
                            rosterLines.Add($"  • Сводка: [dim]{Markup.Escape(founderFeatureSummary)}[/]");
                    }
                    AppendChaosSeaPendingFileRule(rosterLines, GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
                    AppendChaosSeaCommonContractRules(rosterLines);
                    if (!ConfirmChaosSeaContractPreview(
                            "Полный предпросмотр запроса резидентов Обители",
                            rosterLines,
                            ToChaosSeaAuditNode(request),
                            "Полный JSON pending guardian abode residents request"))
                    {
                        return;
                    }

                    await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, request);
                    _pendingGmAction = GuardianAbodeResidentRequestState.BuildResidentsRosterPendingGmActionText(request);
                }

                var pendingLines = new List<string>
                {
                    $"[bold cyan]🏛 {Markup.Escape(abodeName)}[/]",
                    "",
                    isFoundedGuardian
                        ? "Новая Обитель основанной мантии начинает притягивать первые чужие отклики."
                        : "В глубине Обители начинают проступать иные сущности.",
                    "Состав обитателей запрошен у GM."
                };
                if (isFoundedGuardian && !string.IsNullOrWhiteSpace(founderFeatureTitle))
                    pendingLines.Add($"[bold]Дар основания:[/] [white]{Markup.Escape(founderFeatureTitle)}[/]");
                if (isFoundedGuardian && !string.IsNullOrWhiteSpace(founderFeatureSummary))
                    pendingLines.Add($"[dim]{Markup.Escape(founderFeatureSummary)}[/]");
                if (isFoundedGuardian)
                    pendingLines.Add("[dim]Старые обитатели прежнего покровителя не переносятся автоматически; новая мантия собирает собственный первый состав.[/]");
                if (matchingPendingRequests.Count > 0)
                {
                    pendingLines.Add("");
                    pendingLines.Add("[bold]Ожидает ответа GM:[/]");
                    pendingLines.Add("  • Состав обитателей уже запрошен и пока не материализован.");
                    pendingLines.Add("  • Подробный технический контракт доступен через /status audit.");
                }
                pendingLines.Add("");
                pendingLines.Add("[dim]Откройте панель позже, когда явное состояние обитателей будет материализовано.[/]");

                Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", pendingLines)))
                {
                    Header = new PanelHeader(" Обитатели Обители ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Cyan1),
                    Padding = new Padding(2, 1),
                    Expand = true
                });
                WaitForKey();
                return;
            }

            var choices = MakeUniqueChoiceLabels(residents.Select(resident =>
            {
                var bondColor = resident.BondTier switch
                {
                    GuardianAbodeResidentState.BondTierBound => "gold1",
                    GuardianAbodeResidentState.BondTierTrusted => "green",
                    GuardianAbodeResidentState.BondTierFamiliar => "cyan",
                    _ => "grey"
                };
                var devotionColor = resident.AbodeDevotionTier switch
                {
                    GuardianAbodeResidentState.AbodeDevotionTierSteadfast => "gold1",
                    GuardianAbodeResidentState.AbodeDevotionTierDevoted => "green",
                    GuardianAbodeResidentState.AbodeDevotionTierAttached => "cyan",
                    GuardianAbodeResidentState.AbodeDevotionTierUncertain => "yellow",
                    _ => "grey"
                };
                var presenceTag = resident.IsPresent ? string.Empty : " [grey](покинул Обитель)[/]";
                return (
                    $"👤 {Markup.Escape(resident.DisplayName)} [dim]({Markup.Escape(GuardianAbodeResidentState.GetResidentKindLabel(resident.ResidentKind))})[/] " +
                    $"[{bondColor}]{Markup.Escape(GuardianAbodeResidentState.GetBondTierLabel(resident.BondTier))}[/] [dim]{resident.BondLevel}/100[/] " +
                    $"[dim]•[/] [{devotionColor}]{Markup.Escape(GuardianAbodeResidentState.GetAbodeDevotionTierLabel(resident.AbodeDevotionTier))}[/]{presenceTag}",
                    resident.ResidentId);
            }).ToList());
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🏛 Обитатели «{Markup.Escape(abodeName)}»[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= residents.Count)
                return;

            await ShowGuardianAbodeResidentDetailAsync(guardianId, guardianName, abodeId, abodeName, residents[selectedIndex]);
            if (_pendingGmAction != null)
                return;
        }
    }

    private async Task ShowGuardianAbodeResidentDetailAsync(
        string guardianId,
        string guardianName,
        string abodeId,
        string abodeName,
        GuardianAbodeResidentState.ResidentEntry resident)
    {
        var residentStateDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        var soulQuestDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        var thoughtJournalEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectThoughtJournalEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.JournalEntry>();
        var interactionLogEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectInteractionLogEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.JournalEntry>();
        var historyLogEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectHistoryLogEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.HistoryLogEntry>();
        var transferReceiptEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectTransferReceipts(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.TransferReceiptEntry>();
        var interactionReceiptEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectInteractionReceipts(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.InteractionReceiptEntry>();
        var pendingInteractionRequests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        var pendingTransferRequest = await GuardianAbodeResidentRequestState.FindPendingTransferAsync(_fs, resident.ResidentId);
        var pendingTalkRequest = pendingInteractionRequests.FirstOrDefault(request =>
            string.Equals(request.ResidentId, resident.ResidentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, GuardianAbodeResidentState.InteractionTypeTalk, StringComparison.OrdinalIgnoreCase));
        var pendingHistoryRequest = pendingInteractionRequests.FirstOrDefault(request =>
            string.Equals(request.ResidentId, resident.ResidentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase));
        var residentStateRoot = residentStateDoc != null
            ? JsonNode.Parse(residentStateDoc.RootElement.GetRawText()) as JsonObject
            : null;
        var guardiansRoot = guardiansDoc != null
            ? JsonNode.Parse(guardiansDoc.RootElement.GetRawText()) as JsonObject
            : null;
        var competitionCandidates = GuardianAbodeResidentState.BuildTransferCompetitionCandidates(resident, guardiansRoot, residentStateRoot);
        var bestCompetitionCandidate = competitionCandidates.FirstOrDefault();

        var lines = new List<string>
        {
            $"[bold cyan]👤 {Markup.Escape(resident.DisplayName)}[/]",
            "",
            $"  Вид: [white]{Markup.Escape(GuardianAbodeResidentState.GetResidentKindLabel(resident.ResidentKind))}[/]",
            $"  Роль: [dim]{Markup.Escape(resident.RoleLabel)}[/]",
            $"  Связь: [white]{Markup.Escape(GuardianAbodeResidentState.GetBondTierLabel(resident.BondTier))}[/] [dim]({resident.BondLevel}/100)[/]",
            $"  Преданность Обители: [white]{Markup.Escape(GuardianAbodeResidentState.GetAbodeDevotionTierLabel(resident.AbodeDevotionTier))}[/] [dim]({resident.AbodeDevotionLevel}/100)[/]",
            $"  Внутреннее состояние: [white]{Markup.Escape(GuardianAbodeResidentState.GetMigrationStateLabel(resident.MigrationState))}[/] [dim](неспокойствие {resident.Restlessness}/100)[/]",
            $"  Присутствие: {(resident.IsPresent ? "[green]сейчас в Обители[/]" : "[yellow]уже покинул Обитель[/]")}",
            $"  История: {(resident.HistoryRevealed ? "[green]раскрыта[/]" : "[dim]ещё не раскрыта[/]")}",
            $"  Награда связи: [dim]{Markup.Escape(GuardianAbodeResidentState.GetRewardStateLabel(resident.BondRewardState))}[/]"
        };
        var pressureNarrative = GuardianAbodeResidentState.GetMigrationStatePressureNarrative(resident.MigrationState);
        if (!string.IsNullOrWhiteSpace(pressureNarrative))
            lines.Add($"  Давление ухода: [yellow]{Markup.Escape(pressureNarrative)}[/]");
        if (pendingTransferRequest == null &&
            string.Equals(resident.MigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase))
        {
            if (bestCompetitionCandidate == null)
            {
                lines.Add("  Новая Обитель: [dim]убедительной проявленной цели пока нет; основной выход сейчас — уход без новой Обители[/]");
            }
            else
            {
                var competitionText = $"{GuardianAbodeResidentState.GetTransferCompetitionLabelText(bestCompetitionCandidate.CompetitionLabel)} {bestCompetitionCandidate.CompetitionScore}/100";
                if (string.Equals(bestCompetitionCandidate.CompetitionLabel, GuardianAbodeResidentState.TransferCompetitionLabelWeakPull, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"  Возможная новая Обитель: [dim]убедительной цели пока нет; самый сильный видимый зов: {Markup.Escape(bestCompetitionCandidate.TargetGuardianName)} / {Markup.Escape(bestCompetitionCandidate.TargetAbodeName)} ({Markup.Escape(competitionText)})[/]");
                }
                else
                {
                    lines.Add($"  Возможная новая Обитель: [white]{Markup.Escape(bestCompetitionCandidate.TargetGuardianName)} / {Markup.Escape(bestCompetitionCandidate.TargetAbodeName)}[/] [dim]({Markup.Escape(competitionText)})[/]");
                    lines.Add($"  Причина зова: [dim]{Markup.Escape(bestCompetitionCandidate.CompetitionReason)}[/]");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(resident.Summary))
        {
            lines.Add("");
            lines.Add($"[white]{Markup.Escape(resident.Summary)}[/]");
        }

        if (!string.IsNullOrWhiteSpace(resident.OriginWorldSummary))
            lines.Add($"  Мир-исток: [dim]{Markup.Escape(resident.OriginWorldSummary)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.FutureCompanionPrompt))
            lines.Add($"  Образ будущего спутника: [dim]{Markup.Escape(resident.FutureCompanionPrompt)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.BondReason))
            lines.Add($"  Причина связи: [dim]{Markup.Escape(resident.BondReason)}[/]");
        if (resident.CoreTraits.Count > 0)
            lines.Add($"  Черты: [dim]{Markup.Escape(string.Join(", ", resident.CoreTraits))}[/]");
        if (resident.ArchetypeHints.Count > 0)
            lines.Add($"  Архетипы: [dim]{Markup.Escape(string.Join(", ", resident.ArchetypeHints))}[/]");
        if (resident.AppearanceMotifs.Count > 0)
            lines.Add($"  Образы и мотивы: [dim]{Markup.Escape(string.Join(", ", resident.AppearanceMotifs))}[/]");
        if (!string.IsNullOrWhiteSpace(resident.PersonalityProfile.Archetype))
            lines.Add($"  Личность: [white]{Markup.Escape(resident.PersonalityProfile.Archetype)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.PersonalityProfile.Worldview))
            lines.Add($"  Мировоззрение: [dim]{Markup.Escape(resident.PersonalityProfile.Worldview)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.PersonalityProfile.CulturalLayer))
            lines.Add($"  Культурный слой: [dim]{Markup.Escape(resident.PersonalityProfile.CulturalLayer)}[/]");
        if (resident.PersonalityProfile.CoreValues.Count > 0)
            lines.Add($"  Ключевые ценности: [dim]{Markup.Escape(string.Join(", ", resident.PersonalityProfile.CoreValues))}[/]");
        if (resident.PersonalityProfile.PersonalityTraits.Count > 0)
        {
            lines.Add("  Черты личности:");
            foreach (var trait in resident.PersonalityProfile.PersonalityTraits)
                lines.Add($"    [dim]• {Markup.Escape(trait.TraitName)} {trait.Value}/10 — {Markup.Escape(trait.ValueDescription)}[/]");
        }
        lines.Add($"  Склад Обители: [dim]{Markup.Escape(GuardianAbodeResidentState.GetPowerSensitivityLabel(resident.AbodeDisposition.PowerSensitivity))}; {Markup.Escape(GuardianAbodeResidentState.GetMigrationDispositionLabel(resident.AbodeDisposition.MigrationDisposition))}; {Markup.Escape(GuardianAbodeResidentState.GetCommunalOrientationLabel(resident.AbodeDisposition.CommunalOrientation))}; {Markup.Escape(GuardianAbodeResidentState.GetStabilityNeedLabel(resident.AbodeDisposition.StabilityNeed))}[/]");
        if (!string.IsNullOrWhiteSpace(resident.LinkedSoulQuestId))
        {
            var linkedQuestLabel = ResolveSoulQuestLabel(soulQuestDoc?.RootElement, resident.LinkedSoulQuestId);
            lines.Add($"  Связанный квест души: [yellow]{Markup.Escape(linkedQuestLabel)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(resident.GrantedRelicId))
        {
            var grantedRelicLabel = ResolveSoulRelicLabel(soulDoc?.RootElement, resident.GrantedRelicId);
            lines.Add($"  Дарованная реликвия: [green]{Markup.Escape(grantedRelicLabel)}[/]");
        }
        if (pendingTransferRequest != null)
        {
            var targetLabel = string.Equals(pendingTransferRequest.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase)
                ? "уход без новой Обители"
                : $"{pendingTransferRequest.TargetGuardianName} / {pendingTransferRequest.TargetAbodeName}";
            var competitionSummary = BuildResidentTransferCompetitionSummary(pendingTransferRequest);
            lines.Add($"  Переход: [yellow]ожидает решения GM[/] [dim](режим: {Markup.Escape(DescribeResidentTransferMode(pendingTransferRequest.TransferMode))}, цель: {Markup.Escape(targetLabel)}, идентификатор запроса: {Markup.Escape(pendingTransferRequest.RequestId)}{(string.IsNullOrWhiteSpace(competitionSummary) ? string.Empty : $", {Markup.Escape(competitionSummary)}")})[/]");
        }
        else if (transferReceiptEntries.Count > 0)
        {
            var latestTransfer = transferReceiptEntries[0];
            var targetLabel = string.Equals(latestTransfer.Status, GuardianAbodeResidentState.TransferStatusAccepted, StringComparison.OrdinalIgnoreCase)
                ? $"{latestTransfer.TargetGuardianName} / {latestTransfer.TargetAbodeName}"
                : latestTransfer.SourceAbodeName;
            lines.Add($"  Последний переход: [dim]{Markup.Escape(GuardianAbodeResidentState.GetTransferStatusLabel(latestTransfer.Status))}[/] [dim]({Markup.Escape(targetLabel)}, ход {latestTransfer.ResolvedAtTurn})[/]");
        }
        if (pendingTalkRequest != null)
            lines.Add($"  Разговор: [yellow]ожидает ответа GM[/] [dim](идентификатор запроса: {Markup.Escape(pendingTalkRequest.RequestId)})[/]");
        if (pendingHistoryRequest != null)
            lines.Add($"  История: [yellow]ожидает ответа GM[/] [dim](идентификатор запроса: {Markup.Escape(pendingHistoryRequest.RequestId)})[/]");
        if (thoughtJournalEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]Актуальные мысли:[/] [dim]({thoughtJournalEntries.Count} записей)[/]");
            foreach (var thoughtEntry in thoughtJournalEntries)
                AppendResidentJournalDetailLines(lines, thoughtEntry);
        }
        if (interactionLogEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]Краткая память общения:[/] [dim]({interactionLogEntries.Count} записей)[/]");
            foreach (var interactionEntry in interactionLogEntries)
                AppendResidentJournalDetailLines(lines, interactionEntry);
        }
        if (historyLogEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]Раскрытые фрагменты прошлого:[/] [dim]({historyLogEntries.Count} записей)[/]");
            foreach (var historyEntry in historyLogEntries)
                AppendResidentHistoryDetailLines(lines, historyEntry);
        }
        if (transferReceiptEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]Полная история переходов:[/] [dim]({transferReceiptEntries.Count} записей)[/]");
            foreach (var transferEntry in transferReceiptEntries)
                AppendResidentTransferReceiptLines(lines, transferEntry);
        }
        if (interactionReceiptEntries.Count > 0)
        {
            lines.Add("");
            lines.Add($"[bold]Receipt-история взаимодействий:[/] [dim]({interactionReceiptEntries.Count} закрытий)[/]");
            foreach (var receiptEntry in interactionReceiptEntries)
                AppendResidentInteractionReceiptLines(lines, receiptEntry);
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Обитатель Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var rawResident = residentStateRoot == null
            ? null
            : GuardianAbodeResidentState.FindResident(residentStateRoot, resident.ResidentId);
        WriteJsonAuditPanel("Полный JSON резидента Обители", rawResident, Color.Cyan1);
        if (interactionReceiptEntries.Count > 0)
            WriteJsonAuditPanel(
                "Полный JSON interactionReceipts резидента",
                BuildResidentInteractionReceiptsAuditNode(interactionReceiptEntries),
                Color.Cyan1);
        if (transferReceiptEntries.Count > 0)
            WriteJsonAuditPanel(
                "Полный JSON transferReceipts резидента",
                BuildResidentTransferReceiptsAuditNode(transferReceiptEntries),
                Color.Cyan1);
        if (residentStateRoot != null)
            WriteJsonAuditPanel(
                "Полный JSON guardian_abode_residents.json для сверки roster/history/receipts",
                residentStateRoot.DeepClone(),
                Color.Grey);

        var availableInteractions = resident.AvailableInteractions.Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var useDefaultInteractions = availableInteractions.Count == 0;
        var actions = new List<string>();
        if (useDefaultInteractions || availableInteractions.Contains("talk"))
            actions.Add("💬 Поговорить");
        if (useDefaultInteractions || availableInteractions.Contains("history"))
            actions.Add("📖 Выслушать прошлую историю");
        if (useDefaultInteractions || availableInteractions.Contains("quest"))
            actions.Add("🧵 Помочь с личной просьбой");
        if (!string.IsNullOrWhiteSpace(resident.LinkedSoulQuestId))
            actions.Add("📜 Открыть связанный квест души");
        if (!string.IsNullOrWhiteSpace(resident.GrantedRelicId))
            actions.Add("💎 Открыть дарованную реликвию");
        if ((useDefaultInteractions || availableInteractions.Contains("reward")) &&
            resident.CanGrantCompanionRelic &&
            string.Equals(resident.BondRewardState, GuardianAbodeResidentState.RewardStateEligible, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("💎 Принять реликвию связи");
        }
        if (string.Equals(resident.MigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase) &&
            pendingTransferRequest == null)
        {
            actions.Add("🚪 Разрешить переход в другую Обитель");
        }
        actions.Add("← Назад");

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices(actions));

        if (action.Contains("← Назад", StringComparison.Ordinal))
            return;

        if (action.StartsWith("📜", StringComparison.Ordinal))
        {
            if (!await ShowSoulQuestDetailByIdAsync(resident.LinkedSoulQuestId))
            {
                MarkupLine("[yellow]Не удалось открыть точный квест души: активная или историческая запись сейчас недоступна.[/]");
                WaitForKey();
            }
            return;
        }

        if (action.StartsWith("💎 Открыть", StringComparison.Ordinal))
        {
            if (!await ShowSoulRelicDetailByIdAsync(resident.GrantedRelicId))
            {
                MarkupLine("[yellow]Не удалось открыть точную реликвию: запись исчезла или soul_state недоступен.[/]");
                WaitForKey();
            }
            return;
        }

        if (action.StartsWith("💎", StringComparison.Ordinal))
        {
            var actionText =
                $"[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}, abodeName={abodeName}). " +
                "В следующем подтверждённом ходе выдай новую Реликвию Души через metaStateUpdates.soulRelicOperations.addRelic. " +
                $"Реликвия должна иметь relicType={GuardianAbodeResidentState.RelicTypeCompanionEcho}, sourceResidentId={resident.ResidentId}, sourceGuardianId={guardianId}, sourceGuardianName={guardianName}, rarity не ниже Rare и complete companionSeed с companionNameHint, originWorldSummary, futureCompanionPrompt, bondReason, coreTraits, archetypeHints, appearanceMotifs, rich personalityProfile, abodeDisposition, abodeDevotionLevel/abodeDevotionTier и restlessness/migrationState. " +
                $"Также обнови resident state через UpdateGuardianAbodeResidents так, чтобы bondRewardState стал '{GuardianAbodeResidentState.RewardStateGranted}', а grantedRelicId указывал на выданную реликвию. " +
                "Если вручение заметно меняет отношение резидента к Обители, обнови также abodeDevotionLevel/restlessness/migrationState в bounded canonical steps, выведенных из outcome, текущей силы Обители, abodeDisposition и bondLevel. " +
                "Не забудь добавить residentInteractionLogUpdates с кратким summary вручения реликвии и его последствия.";
            var relicGrantLines = new List<string>
            {
                "[bold cyan]Получение реликвии связи от резидента[/]",
                "",
                $"  Resident: [white]{Markup.Escape(resident.DisplayName)}[/] [dim]({Markup.Escape(resident.ResidentId)})[/]",
                $"  Guardian/Abode: [dim]{Markup.Escape(guardianId)} / {Markup.Escape(abodeId)}[/]",
                $"  Текущий bond: [dim]{Markup.Escape(resident.BondTier)} {resident.BondLevel}/100[/]",
                $"  Текущий reward state: [dim]{Markup.Escape(resident.BondRewardState)}[/]",
                "",
                "[bold]Accepted state changes:[/]",
                $"  • Добавить Soul Relic с relicType={GuardianAbodeResidentState.RelicTypeCompanionEcho}.",
                "  • Редкость новой реликвии не ниже Rare.",
                "  • companionSeed должен быть полным: companionNameHint, originWorldSummary, futureCompanionPrompt, bondReason, traits, archetypeHints, appearanceMotifs, personalityProfile, abodeDisposition.",
                $"  • UpdateGuardianAbodeResidents: bondRewardState={GuardianAbodeResidentState.RewardStateGranted}, grantedRelicId = id новой реликвии.",
                "  • Добавить residentInteractionLogUpdates с памятью вручения."
            };
            AppendChaosSeaCommonContractRules(relicGrantLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр реликвии связи",
                    relicGrantLines,
                    BuildChaosSeaDirectActionAudit(
                        "ABODE_RESIDENT_RELIC_GRANT",
                        actionText,
                        ("residentId", resident.ResidentId),
                        ("guardianId", guardianId),
                        ("abodeId", abodeId),
                        ("requiredRelicType", GuardianAbodeResidentState.RelicTypeCompanionEcho),
                        ("residentFullJsonBefore", rawResident?.DeepClone()),
                        ("soulStateFullJsonBefore", soulDoc == null ? null : CloneJsonElementForAudit(soulDoc.RootElement)))))
            {
                return;
            }

            _pendingGmAction = actionText;
            MarkupLine("[cyan]Реликвия связи запрошена у GM.[/]");
            return;
        }

        if (action.StartsWith("🧵", StringComparison.Ordinal))
        {
            var actionText =
                $"[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}). " +
                "В принятом ходе отыграй просьбу и явно создай или продвинь обычный квест души через UpdateSoulQuests. " +
                $"Квест души должен иметь guardianId={guardianId}, relatedAfterlifeResidentId={resident.ResidentId} и понятные игроку title/description. " +
                "При необходимости также обнови resident bondLevel/bondTier, linkedSoulQuestId и abodeDevotionLevel/restlessness/migrationState через UpdateGuardianAbodeResidents; состояние обитателя в Обители меняй только небольшими каноническими шагами, выведенными из исхода квеста, текущей силы Обители, abodeDisposition и bondLevel. " +
                "Оставь residentInteractionLogUpdates с короткой сводкой просьбы и прогресса, чтобы у ГМа оставалась связная память этого шага.";
            var residentQuestLines = new List<string>
            {
                "[bold cyan]Личная просьба резидента[/]",
                "",
                $"  Resident: [white]{Markup.Escape(resident.DisplayName)}[/] [dim]({Markup.Escape(resident.ResidentId)})[/]",
                $"  Guardian/Abode: [dim]{Markup.Escape(guardianId)} / {Markup.Escape(abodeId)}[/]",
                $"  linkedSoulQuestId сейчас: [dim]{Markup.Escape(resident.LinkedSoulQuestId)}[/]",
                "",
                "[bold]Accepted state changes:[/]",
                "  • Создать или продвинуть обычный Soul Quest через UpdateSoulQuests.",
                "  • Новый/обновлённый квест должен иметь guardianId и relatedAfterlifeResidentId.",
                "  • При изменении связи использовать bounded resident state steps.",
                "  • Добавить residentInteractionLogUpdates с памятью просьбы."
            };
            AppendChaosSeaCommonContractRules(residentQuestLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр просьбы резидента",
                    residentQuestLines,
                    BuildChaosSeaDirectActionAudit(
                        "ABODE_RESIDENT_QUEST_REQUEST",
                        actionText,
                        ("residentId", resident.ResidentId),
                        ("guardianId", guardianId),
                        ("abodeId", abodeId),
                        ("linkedSoulQuestId", resident.LinkedSoulQuestId),
                        ("residentFullJsonBefore", rawResident?.DeepClone()),
                        ("soulQuestsFullJsonBefore", soulQuestDoc == null ? null : CloneJsonElementForAudit(soulQuestDoc.RootElement)))))
            {
                return;
            }

            _pendingGmAction = actionText;
            MarkupLine("[cyan]Личная просьба обитателя отправлена GM.[/]");
            return;
        }

        if (action.StartsWith("📖", StringComparison.Ordinal))
        {
            if (await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs))
            {
                MarkupLine("[red]pending_guardian_abode_resident_interactions.json повреждён. Новый запрос на раскрытие истории заблокирован, пока pending bundle не будет исправлен или очищен.[/]");
                return;
            }

            if (pendingHistoryRequest != null)
            {
                MarkupLine("[yellow]Уже есть незакрытый запрос на раскрытие истории. Дождитесь ответа GM.[/]");
                return;
            }

            var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
            {
                GuardianId = guardianId,
                GuardianName = guardianName,
                AbodeId = abodeId,
                AbodeName = abodeName,
                ResidentId = resident.ResidentId,
                ResidentName = resident.DisplayName,
                InteractionType = GuardianAbodeResidentState.InteractionTypeHistory,
                CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
            };
            var actionText =
                $"[ABODE_RESIDENT_HISTORY_REQUEST] Игрок просит afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, requestId={request.RequestId}) раскрыть прошлую историю. " +
                "В следующем подтверждённом ходе обязательно закрой этот запрос через UpdateGuardianAbodeResidentInteractionReceipts со status=accepted|rejected|cancelled. " +
                "Если история действительно раскрыта, либо установи historyRevealed=true, либо добавь запись через UpdateGuardianAbodeResidentHistoryLog, либо обнови mortalWorldImprint. " +
                "После accepted ответа обязательно добавь residentThoughtJournalUpdates и/или residentInteractionLogUpdates с краткой памятью результата сцены. " +
                "Если сцена заметно меняет отношение резидента к Обители, обнови abodeDevotionLevel/restlessness/migrationState через UpdateGuardianAbodeResidents только в bounded canonical steps, выведенных из responseMode/outcome, текущей силы Обители, abodeDisposition и bondLevel. " +
                "Обычный отказ допустим, но он тоже должен быть явно закрыт receipt-ом.";
            var historyLines = new List<string>
            {
                "[bold cyan]Раскрытие прошлой истории резидента[/]",
                "",
                $"  Resident: [white]{Markup.Escape(resident.DisplayName)}[/] [dim]({Markup.Escape(resident.ResidentId)})[/]",
                $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
                $"  historyRevealed сейчас: [dim]{resident.HistoryRevealed.ToString().ToLowerInvariant()}[/]",
                "",
                "[bold]Техническое закрытие ГМ:[/]",
                "  • UpdateGuardianAbodeResidentInteractionReceipts с requestId, residentId, interactionType=history, status.",
                "  • Если accepted: historyRevealed=true и/или UpdateGuardianAbodeResidentHistoryLog и/или mortalWorldImprint.",
                "  • Добавить residentThoughtJournalUpdates и/или residentInteractionLogUpdates.",
                "  • Отказ или отмена тоже закрываются receipt-ом."
            };
            AppendChaosSeaPendingFileRule(historyLines, GuardianAbodeResidentRequestState.PendingInteractionsRequestPath);
            AppendChaosSeaCommonContractRules(historyLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр истории резидента",
                    historyLines,
                    ToChaosSeaAuditNode(request),
                    "Полный JSON pending resident history request"))
            {
                return;
            }

            await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, request);
            _pendingGmAction = actionText;
            MarkupLine("[cyan]История обитателя запрошена у GM.[/]");
            return;
        }

        if (action.StartsWith("🚪", StringComparison.Ordinal))
        {
            var transferCreated = await StartResidentTransferRequestAsync(resident, guardianId, guardianName, abodeId, abodeName);
            if (transferCreated)
                await _stateManager.RefreshGameStateAsync();
            return;
        }

        if (action.StartsWith("💬", StringComparison.Ordinal))
        {
            if (await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs))
            {
                MarkupLine("[red]pending_guardian_abode_resident_interactions.json повреждён. Новый разговорный запрос заблокирован, пока pending bundle не будет исправлен или очищен.[/]");
                return;
            }

            if (pendingTalkRequest != null)
            {
                MarkupLine("[yellow]Уже есть незакрытый разговор с этим резидентом. Дождитесь ответа GM.[/]");
                return;
            }

            var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
            {
                GuardianId = guardianId,
                GuardianName = guardianName,
                AbodeId = abodeId,
                AbodeName = abodeName,
                ResidentId = resident.ResidentId,
                ResidentName = resident.DisplayName,
                InteractionType = GuardianAbodeResidentState.InteractionTypeTalk,
                CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
            };
            var actionText =
                $"[ABODE_RESIDENT_TALK] Игрок разговаривает с afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}, abodeName={abodeName}, requestId={request.RequestId}). " +
                "В следующем подтверждённом ходе отыграй сцену и обязательно закрой этот разговор через UpdateGuardianAbodeResidentInteractionReceipts со status=accepted|rejected|cancelled. " +
                "После accepted ответа обязательно оставь residentThoughtJournalUpdates и/или residentInteractionLogUpdates с краткой памятью результата сцены. " +
                "Если были meaningful state changes, обнови resident state через UpdateGuardianAbodeResidents; abodeDevotionLevel/restlessness/migrationState меняй только в bounded canonical steps, выведенных из responseMode/outcome, текущей силы Обители, abodeDisposition и bondLevel.";
            var talkLines = new List<string>
            {
                "[bold cyan]Разговор с резидентом Обители[/]",
                "",
                $"  Resident: [white]{Markup.Escape(resident.DisplayName)}[/] [dim]({Markup.Escape(resident.ResidentId)})[/]",
                $"  requestId: [dim]{Markup.Escape(request.RequestId)}[/]",
                $"  bond/devotion: [dim]{Markup.Escape(resident.BondTier)} {resident.BondLevel}/100; {Markup.Escape(resident.AbodeDevotionTier)} {resident.AbodeDevotionLevel}/100[/]",
                "",
                "[bold]Техническое закрытие ГМ:[/]",
                "  • UpdateGuardianAbodeResidentInteractionReceipts с requestId, residentId, interactionType=talk, status.",
                "  • accepted response должен оставить residentThoughtJournalUpdates и/или residentInteractionLogUpdates.",
                "  • State changes только через UpdateGuardianAbodeResidents bounded steps.",
                "  • Отказ или отмена тоже закрываются receipt-ом."
            };
            AppendChaosSeaPendingFileRule(talkLines, GuardianAbodeResidentRequestState.PendingInteractionsRequestPath);
            AppendChaosSeaCommonContractRules(talkLines);
            if (!ConfirmChaosSeaContractPreview(
                    "Полный предпросмотр разговора с резидентом",
                    talkLines,
                    ToChaosSeaAuditNode(request),
                    "Полный JSON pending resident talk request"))
            {
                return;
            }

            await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, request);
            _pendingGmAction = actionText;
            MarkupLine("[cyan]Разговор с обитателем Обители отправлен GM.[/]");
            return;
        }

        return;
    }

    private async Task<bool> ShowGuardianAbodeResidentDetailByIdAsync(string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        var residentStateDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
        if (residentStateDoc?.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        var residentRoot = JsonNode.Parse(residentStateDoc.RootElement.GetRawText()) as JsonObject;
        if (residentRoot == null)
            return false;

        var residentNode = GuardianAbodeResidentState.FindResident(residentRoot, residentId);
        if (residentNode == null)
            return false;

        JsonObject? guardiansRoot = null;
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc?.RootElement.ValueKind == JsonValueKind.Object)
            guardiansRoot = JsonNode.Parse(guardiansDoc.RootElement.GetRawText()) as JsonObject;

        var guardianId = GetNodeString(residentNode["guardianId"]) ?? string.Empty;
        var guardian = ResolveGuardianObject(guardiansRoot, guardianId);
        var currentAbodePower = guardian == null ? (int?)null : AbodePowerRules.GetCurrentPower(guardian);
        var resident = GuardianAbodeResidentState.ReadResidentEntry(residentNode, currentAbodePower);
        var guardianName = guardian == null
            ? guardianId
            : GuardianManifestation.GetDisplayName(guardian) ??
              GetNodeString(guardian["canonicalName"]) ??
              guardianId;
        var abodeName = GetNodeString(guardian?["abode"]?["name"]) ?? resident.AbodeId;

        await ShowGuardianAbodeResidentDetailAsync(
            resident.GuardianId,
            guardianName,
            resident.AbodeId,
            abodeName,
            resident);
        return true;
    }

    private static string ResolveSoulQuestLabel(JsonElement? soulQuestRoot, string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return string.Empty;

        if (soulQuestRoot is { ValueKind: JsonValueKind.Object } root)
        {
            foreach (var collectionName in new[] { "quests", "UpdateSoulQuests" })
            {
                if (!root.TryGetProperty(collectionName, out var quests) || quests.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var quest in quests.EnumerateArray())
                {
                    if (quest.ValueKind != JsonValueKind.Object ||
                        !string.Equals(GetStr(quest, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var title = GetStr(quest, "title", "");
                    return string.IsNullOrWhiteSpace(title) ? questId : $"{title} ({questId})";
                }
            }
        }

        return questId;
    }

    private async Task<bool> ShowSoulQuestDetailByIdAsync(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        using var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        if (TryFindQuestById(soulDoc?.RootElement, questId, out var soulQuest))
        {
            await ShowQuestDetailPanel(soulQuest, isSoul: true, isHistory: false);
            return true;
        }

        using var historyDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/quest_history.json");
        if (historyDoc?.RootElement.ValueKind == JsonValueKind.Object &&
            historyDoc.RootElement.TryGetProperty("questHistory", out var questHistory) &&
            questHistory.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in questHistory.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetStr(entry, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                JsonElement? rewardInfo = null;
                var relatedChains = new List<JsonElement>();
                if (historyDoc.RootElement.TryGetProperty("questRewards", out var questRewards) &&
                    questRewards.ValueKind == JsonValueKind.Array)
                {
                    foreach (var reward in questRewards.EnumerateArray())
                    {
                        if (reward.ValueKind == JsonValueKind.Object &&
                            string.Equals(GetStr(reward, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
                        {
                            rewardInfo = reward;
                            break;
                        }
                    }
                }

                if (historyDoc.RootElement.TryGetProperty("questChains", out var questChains) &&
                    questChains.ValueKind == JsonValueKind.Array)
                {
                    var questName = GetStr(entry, "questName", GetStr(entry, "title", ""));
                    foreach (var chain in questChains.EnumerateArray())
                    {
                        if (chain.ValueKind == JsonValueKind.Object && HistoryChainMatchesQuest(chain, questId, questName))
                            relatedChains.Add(chain.Clone());
                    }
                }

                await ShowQuestDetailPanel(entry, isSoul: false, isHistory: true, rewardInfo, relatedChains);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> ShowGuardianQuestDetailByIdAsync(string guardianId, string questId)
    {
        if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(questId))
            return false;

        using var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc?.RootElement.ValueKind != JsonValueKind.Object ||
            !guardiansDoc.RootElement.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            if (guardian.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetStr(guardian, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryFindGuardianQuestById(guardian, questId, out var quest, out _))
                return false;

            await ShowQuestDetailPanel(quest, isSoul: false, isHistory: false);
            return true;
        }

        return false;
    }

    private static bool TryFindGuardianQuestById(
        JsonElement guardian,
        string questId,
        out JsonElement quest,
        out string collectionLabel)
    {
        quest = default;
        collectionLabel = string.Empty;
        if (guardian.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(questId) ||
            !guardian.TryGetProperty("questManagement", out var questManagement) ||
            questManagement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var (propertyName, label) in new[]
                 {
                     ("activeQuests", "активные задания"),
                     ("availableQuests", "доступные задания"),
                     ("completedQuests", "выполненные задания")
                 })
        {
            if (!questManagement.TryGetProperty(propertyName, out var quests) || quests.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var candidate in quests.EnumerateArray())
            {
                if (candidate.ValueKind == JsonValueKind.Object &&
                    string.Equals(GetStr(candidate, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
                {
                    quest = candidate;
                    collectionLabel = label;
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolveSoulRelicLabel(JsonElement? soulRoot, string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return string.Empty;

        if (soulRoot is { ValueKind: JsonValueKind.Object } root &&
            root.TryGetProperty("soulRelics", out var soulRelics) &&
            soulRelics.ValueKind == JsonValueKind.Object)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (!soulRelics.TryGetProperty(collectionName, out var relics) || relics.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var relic in relics.EnumerateArray())
                {
                    if (relic.ValueKind != JsonValueKind.Object ||
                        !string.Equals(GetStr(relic, "relicId", ""), relicId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = GetStr(relic, "name", relicId);
                    return string.Equals(name, relicId, StringComparison.OrdinalIgnoreCase) ? relicId : $"{name} ({relicId})";
                }
            }
        }

        return relicId;
    }

    private async Task<bool> ShowSoulRelicDetailByIdAsync(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        using var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (soulDoc?.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!TryFindSoulRelicById(soulDoc.RootElement, relicId, out var status, out var relic))
            return false;

        var relicName = GetStr(relic, "name", relicId);
        await ShowRelicDetailPanel(relicId, relicName, status, relic, isAfterlifeRealm: true);
        return true;
    }

    private static bool TryFindQuestById(JsonElement? root, string questId, out JsonElement quest)
    {
        quest = default;
        if (root == null || root.Value.ValueKind == JsonValueKind.Undefined)
            return false;

        if (root.Value.ValueKind == JsonValueKind.Object &&
            root.Value.TryGetProperty("quests", out var quests) &&
            quests.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in quests.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object &&
                    string.Equals(GetStr(entry, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
                {
                    quest = entry;
                    return true;
                }
            }
        }

        foreach (var property in root.Value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetStr(property.Value, "questId", ""), questId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            quest = property.Value;
            return true;
        }

        return false;
    }

    private static bool TryFindSoulRelicById(JsonElement root, string relicId, out string status, out JsonElement relic)
    {
        status = string.Empty;
        relic = default;

        if (!root.TryGetProperty("soulRelics", out var soulRelics) || soulRelics.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var collectionName in new[] { "equipped", "stored" })
        {
            if (!soulRelics.TryGetProperty(collectionName, out var relics) || relics.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in relics.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetStr(entry, "relicId", ""), relicId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                status = collectionName;
                relic = entry;
                return true;
            }
        }

        return false;
    }

    private static void AppendResidentJournalDetailLines(List<string> lines, GuardianAbodeResidentState.JournalEntry entry)
    {
        var line = string.IsNullOrWhiteSpace(entry.Title)
            ? entry.Summary
            : $"{entry.Title} — {entry.Summary}";
        lines.Add($"  • [white]{Markup.Escape(line)}[/]");
        var eventTypeLabel = DescribeActorJournalEventType(entry.EventType);
        if (!string.IsNullOrWhiteSpace(eventTypeLabel))
            lines.Add($"    [dim]Событие: {Markup.Escape(eventTypeLabel)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Consequence))
            lines.Add($"    [dim]Последствие: {Markup.Escape(entry.Consequence)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Attitude))
            lines.Add($"    [dim]Отношение: {Markup.Escape(entry.Attitude)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Intent))
            lines.Add($"    [dim]Намерение: {Markup.Escape(entry.Intent)}[/]");
        if (entry.Tags.Count > 0)
            lines.Add($"    [dim]Метки: {Markup.Escape(string.Join(", ", entry.Tags))}[/]");
        if (entry.Turn > 0)
            lines.Add($"    [dim]Ход: {entry.Turn}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Timestamp))
            lines.Add($"    [dim]Время: {Markup.Escape(entry.Timestamp)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.EntryId))
            lines.Add($"    [dim]Идентификатор записи: {Markup.Escape(entry.EntryId)}[/]");
    }

    private static void AppendResidentInteractionReceiptLines(List<string> lines, GuardianAbodeResidentState.InteractionReceiptEntry entry)
    {
        var typeLabel = string.IsNullOrWhiteSpace(entry.InteractionType)
            ? "interaction"
            : DescribeResidentInteractionType(entry.InteractionType);
        lines.Add($"  • [white]{Markup.Escape(typeLabel)}[/] [dim]requestId={Markup.Escape(entry.RequestId)}, status={Markup.Escape(entry.Status)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.ResponseMode))
            lines.Add($"    [dim]Режим ответа: {Markup.Escape(entry.ResponseMode)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.HistoryEntryId))
            lines.Add($"    [dim]historyEntryId: {Markup.Escape(entry.HistoryEntryId)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Reason))
            lines.Add($"    [dim]Причина: {Markup.Escape(entry.Reason)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.GuardianId) || !string.IsNullOrWhiteSpace(entry.AbodeId))
            lines.Add($"    [dim]guardianId={Markup.Escape(entry.GuardianId)}, abodeId={Markup.Escape(entry.AbodeId)}[/]");
        if (entry.ResolvedAtTurn > 0)
            lines.Add($"    [dim]Ход закрытия: {entry.ResolvedAtTurn}[/]");
        if (!string.IsNullOrWhiteSpace(entry.ResolvedAtUtc))
            lines.Add($"    [dim]UTC закрытия: {Markup.Escape(entry.ResolvedAtUtc)}[/]");
    }

    private static JsonObject BuildResidentInteractionReceiptsAuditNode(IEnumerable<GuardianAbodeResidentState.InteractionReceiptEntry> entries) =>
        new()
        {
            ["surface"] = "game_state/meta/guardian_abode_residents.json.interactionReceipts[]",
            ["entries"] = new JsonArray(entries.Select(entry => (JsonNode?)new JsonObject
            {
                ["requestId"] = entry.RequestId,
                ["residentId"] = entry.ResidentId,
                ["guardianId"] = entry.GuardianId,
                ["abodeId"] = entry.AbodeId,
                ["interactionType"] = entry.InteractionType,
                ["status"] = entry.Status,
                ["responseMode"] = entry.ResponseMode,
                ["historyEntryId"] = entry.HistoryEntryId,
                ["reason"] = entry.Reason,
                ["resolvedAtTurn"] = entry.ResolvedAtTurn,
                ["resolvedAtUtc"] = entry.ResolvedAtUtc
            }).ToArray())
        };

    private static JsonObject BuildResidentTransferReceiptsAuditNode(IEnumerable<GuardianAbodeResidentState.TransferReceiptEntry> entries) =>
        new()
        {
            ["surface"] = "game_state/meta/guardian_abode_residents.json.transferReceipts[]",
            ["entries"] = new JsonArray(entries.Select(entry => (JsonNode?)new JsonObject
            {
                ["requestId"] = entry.RequestId,
                ["residentId"] = entry.ResidentId,
                ["residentName"] = entry.ResidentName,
                ["sourceGuardianId"] = entry.SourceGuardianId,
                ["sourceGuardianName"] = entry.SourceGuardianName,
                ["sourceAbodeId"] = entry.SourceAbodeId,
                ["sourceAbodeName"] = entry.SourceAbodeName,
                ["targetGuardianId"] = entry.TargetGuardianId,
                ["targetGuardianName"] = entry.TargetGuardianName,
                ["targetAbodeId"] = entry.TargetAbodeId,
                ["targetAbodeName"] = entry.TargetAbodeName,
                ["status"] = entry.Status,
                ["transferMode"] = entry.TransferMode,
                ["departureHistoryEntryId"] = entry.DepartureHistoryEntryId,
                ["arrivalHistoryEntryId"] = entry.ArrivalHistoryEntryId,
                ["reason"] = entry.Reason,
                ["resolvedAtTurn"] = entry.ResolvedAtTurn,
                ["resolvedAtUtc"] = entry.ResolvedAtUtc
            }).ToArray())
        };

    private static string DescribeResidentInteractionType(string? interactionType) =>
        (interactionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            GuardianAbodeResidentState.InteractionTypeTalk => "разговор",
            GuardianAbodeResidentState.InteractionTypeHistory => "раскрытие истории",
            "" => "interaction",
            _ => interactionType ?? "interaction"
        };

    private static void AppendResidentHistoryDetailLines(List<string> lines, GuardianAbodeResidentState.HistoryLogEntry entry)
    {
        var title = string.IsNullOrWhiteSpace(entry.Title) ? entry.EntryId : entry.Title;
        lines.Add($"  • [white]{Markup.Escape(title)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Summary))
            lines.Add($"    [dim]{Markup.Escape(entry.Summary)}[/]");
        if (entry.Tags.Count > 0)
            lines.Add($"    [dim]Метки: {Markup.Escape(string.Join(", ", entry.Tags))}[/]");
        if (entry.RevealedAtTurn > 0)
            lines.Add($"    [dim]Раскрыто на ходу: {entry.RevealedAtTurn}[/]");
        if (!string.IsNullOrWhiteSpace(entry.RevealedAtUtc))
            lines.Add($"    [dim]Раскрыто в UTC: {Markup.Escape(entry.RevealedAtUtc)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.EntryId))
            lines.Add($"    [dim]Идентификатор записи: {Markup.Escape(entry.EntryId)}[/]");
    }

    private static void AppendResidentTransferReceiptLines(List<string> lines, GuardianAbodeResidentState.TransferReceiptEntry entry)
    {
        var targetLabel = string.Equals(entry.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase)
            ? "внеэкранный уход"
            : $"{entry.TargetGuardianName} / {entry.TargetAbodeName}";
        lines.Add($"  • [white]{Markup.Escape(GuardianAbodeResidentState.GetTransferStatusLabel(entry.Status))}[/] — {Markup.Escape(targetLabel)}");
        lines.Add($"    [dim]Режим: {Markup.Escape(DescribeResidentTransferMode(entry.TransferMode))}[/]");
        if (!string.IsNullOrWhiteSpace(entry.SourceGuardianName) || !string.IsNullOrWhiteSpace(entry.SourceAbodeName))
            lines.Add($"    [dim]Источник: {Markup.Escape(entry.SourceGuardianName)} / {Markup.Escape(entry.SourceAbodeName)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.Reason))
            lines.Add($"    [dim]Причина: {Markup.Escape(entry.Reason)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.DepartureHistoryEntryId))
            lines.Add($"    [dim]Идентификатор записи ухода: {Markup.Escape(entry.DepartureHistoryEntryId)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.ArrivalHistoryEntryId))
            lines.Add($"    [dim]Идентификатор записи прибытия: {Markup.Escape(entry.ArrivalHistoryEntryId)}[/]");
        if (entry.ResolvedAtTurn > 0)
            lines.Add($"    [dim]Решено на ходу: {entry.ResolvedAtTurn}[/]");
        if (!string.IsNullOrWhiteSpace(entry.ResolvedAtUtc))
            lines.Add($"    [dim]Решено в UTC: {Markup.Escape(entry.ResolvedAtUtc)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.RequestId))
            lines.Add($"    [dim]Идентификатор запроса: {Markup.Escape(entry.RequestId)}[/]");
    }

    private static string DescribeGuardianInteractionType(string? interactionType) =>
        (interactionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "talk" => "разговор",
            "lore" => "вопрос о знании",
            "history" => "раскрытие истории",
            _ => interactionType ?? string.Empty
        };

    private static string DescribeGuardianJournalStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "accepted" => "принято",
            "rejected" => "отклонено",
            "cancelled" => "отменено",
            _ => status ?? string.Empty
        };

    private static string DescribeGuardianResponseMode(string? responseMode) =>
        (responseMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "talk_scene" => "сцена разговора",
            "conversation" => "беседа",
            "lore_revealed" => "знание раскрыто",
            "lore_refused" => "в знании отказано",
            "warning" => "предупреждение",
            "refusal" => "отказ",
            "trust_shift" => "сдвиг доверия",
            "attitude_shift" => "сдвиг отношения",
            "history_revealed" => "история раскрыта",
            "history_refused" => "история скрыта",
            "history_partial" => "история раскрыта частично",
            "bond_shift_only" => "только сдвиг связи",
            _ => responseMode ?? string.Empty
        };

    private static string DescribeResidentTransferMode(string? transferMode) =>
        (transferMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "departure_only" => "уход без новой Обители",
            "accepted_transfer" => "переход в другую Обитель",
            "refused_transfer" => "переход отклонён",
            _ => transferMode ?? string.Empty
        };

    private static string BuildResidentTransferCompetitionSummary(GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest request)
    {
        var parts = new List<string>();
        var selectionLabel = GuardianAbodeResidentRequestState.GetTransferSelectionModeLabel(request.SelectionMode);
        if (!string.IsNullOrWhiteSpace(selectionLabel))
            parts.Add($"выбор: {selectionLabel}");

        if (request.CompetitionScore.HasValue && !string.IsNullOrWhiteSpace(request.CompetitionLabel))
            parts.Add($"системная оценка: {GuardianAbodeResidentState.GetTransferCompetitionLabelText(request.CompetitionLabel)} {request.CompetitionScore.Value}/100");

        if (!string.IsNullOrWhiteSpace(request.CompetitionReason))
            parts.Add($"причина: {request.CompetitionReason}");

        return string.Join(", ", parts);
    }

    private async Task<bool> StartResidentTransferRequestAsync(
        GuardianAbodeResidentState.ResidentEntry resident,
        string sourceGuardianId,
        string sourceGuardianName,
        string sourceAbodeId,
        string sourceAbodeName)
    {
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var residentStateDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
        var guardiansRoot = guardiansDoc != null
            ? JsonNode.Parse(guardiansDoc.RootElement.GetRawText()) as JsonObject
            : null;
        var residentStateRoot = residentStateDoc != null
            ? JsonNode.Parse(residentStateDoc.RootElement.GetRawText()) as JsonObject
            : null;
        var candidates = GuardianAbodeResidentState.BuildTransferCompetitionCandidates(resident, guardiansRoot, residentStateRoot).ToList();
        var strongestCandidate = candidates.FirstOrDefault();
        var hasConvincingCandidate = candidates.Any(candidate => candidate.CompetitionScore >= 50);
        var choices = new List<(string Label, string TransferMode, string TargetGuardianId, string TargetGuardianName, string TargetAbodeId, string TargetAbodeName, string SelectionMode, int? CompetitionScore, string? CompetitionLabel, string? CompetitionReason)>();

        foreach (var candidate in candidates)
        {
            var prefix = candidate.CompetitionScore switch
            {
                >= 70 => "⭐ Рекомендовано",
                >= 50 => "✨ Подходит",
                _ => "▫ Слабое притяжение"
            };
            var competitionText = $"{GuardianAbodeResidentState.GetTransferCompetitionLabelText(candidate.CompetitionLabel)} {candidate.CompetitionScore}/100";
            choices.Add((
                $"{prefix}: {candidate.TargetGuardianName} — {candidate.TargetAbodeName} [{competitionText}]",
                GuardianAbodeResidentState.TransferModeAcceptedTransfer,
                candidate.TargetGuardianId,
                candidate.TargetGuardianName,
                candidate.TargetAbodeId,
                candidate.TargetAbodeName,
                candidate.CompetitionScore >= 50
                    ? GuardianAbodeResidentRequestState.TransferSelectionModeCompetitionRecommended
                    : GuardianAbodeResidentRequestState.TransferSelectionModeManualOverride,
                candidate.CompetitionScore,
                candidate.CompetitionLabel,
                candidate.CompetitionReason));
        }

        choices.Add((
            "🌫 Отпустить без новой Обители",
            GuardianAbodeResidentState.TransferModeDepartureOnly,
            "",
            "",
            "",
            "",
            GuardianAbodeResidentRequestState.TransferSelectionModeDepartureOnly,
            null,
            null,
            null));

        if (choices.Count == 1)
        {
            MarkupLine("[yellow]Нет другой проявленной Обители для целевого перехода. Доступен только уход без новой Обители.[/]");
        }
        else if (strongestCandidate != null)
        {
            var competitionText = $"{GuardianAbodeResidentState.GetTransferCompetitionLabelText(strongestCandidate.CompetitionLabel)} {strongestCandidate.CompetitionScore}/100";
            if (hasConvincingCandidate)
            {
                MarkupLine($"[cyan]Лучший системный зов: {Markup.Escape(strongestCandidate.TargetGuardianName)} — {Markup.Escape(strongestCandidate.TargetAbodeName)} ({Markup.Escape(competitionText)}). {Markup.Escape(strongestCandidate.CompetitionReason)}[/]");
            }
            else
            {
                MarkupLine($"[yellow]Система пока не видит убедительной новой Обители. Самое сильное притяжение остаётся слабым: {Markup.Escape(strongestCandidate.TargetGuardianName)} — {Markup.Escape(strongestCandidate.TargetAbodeName)} ({Markup.Escape(competitionText)}). Offscreen departure остаётся основным путём.[/]");
            }
        }

        var labels = choices.Select(choice => choice.Label).Append("← Назад").ToList();
        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Выберите, как разрешить переход резидента:[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices(labels));

        if (selected.Contains("← Назад", StringComparison.Ordinal))
            return false;

        var choiceIndex = labels.IndexOf(selected);
        if (choiceIndex < 0 || choiceIndex >= choices.Count)
            return false;

        var choice = choices[choiceIndex];
        if (await GuardianAbodeResidentRequestState.IsTransferRequestFileMalformedAsync(_fs))
        {
            MarkupLine("[red]pending_guardian_abode_resident_transfers.json повреждён. Новый запрос на переход заблокирован, пока pending bundle не будет исправлен или очищен.[/]");
            return false;
        }

        var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            ResidentId = resident.ResidentId,
            ResidentName = resident.DisplayName,
            SourceGuardianId = sourceGuardianId,
            SourceGuardianName = sourceGuardianName,
            SourceAbodeId = sourceAbodeId,
            SourceAbodeName = sourceAbodeName,
            TargetGuardianId = choice.TargetGuardianId,
            TargetGuardianName = choice.TargetGuardianName,
            TargetAbodeId = choice.TargetAbodeId,
            TargetAbodeName = choice.TargetAbodeName,
            AbodeDevotionLevel = resident.AbodeDevotionLevel,
            AbodeDevotionTier = resident.AbodeDevotionTier,
            Restlessness = resident.Restlessness,
            MigrationState = resident.MigrationState,
            TransferMode = choice.TransferMode,
            SelectionMode = choice.SelectionMode,
            CompetitionScore = choice.CompetitionScore,
            CompetitionLabel = choice.CompetitionLabel,
            CompetitionReason = choice.CompetitionReason,
            CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
        };

        var transferLines = new List<string>
        {
            "[bold cyan]Переход резидента между Обителями[/]",
            "",
            $"  Resident: [white]{Markup.Escape(resident.DisplayName)}[/] [dim]({Markup.Escape(resident.ResidentId)})[/]",
            $"  Source: [white]{Markup.Escape(sourceGuardianName)} / {Markup.Escape(sourceAbodeName)}[/] [dim]({Markup.Escape(sourceGuardianId)} / {Markup.Escape(sourceAbodeId)})[/]",
            string.Equals(choice.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase)
                ? "  Target: [yellow]уход без новой Обители[/]"
                : $"  Target: [white]{Markup.Escape(choice.TargetGuardianName)} / {Markup.Escape(choice.TargetAbodeName)}[/] [dim]({Markup.Escape(choice.TargetGuardianId)} / {Markup.Escape(choice.TargetAbodeId)})[/]",
            $"  transferMode: [dim]{Markup.Escape(choice.TransferMode)}[/]",
            $"  selectionMode: [dim]{Markup.Escape(choice.SelectionMode)}[/]",
            $"  current devotion/restlessness: [dim]{resident.AbodeDevotionLevel}/100; {resident.Restlessness}/100[/]"
        };
        if (choice.CompetitionScore.HasValue)
        {
            transferLines.Add($"  competitionScore: [dim]{choice.CompetitionScore.Value}/100[/]");
            transferLines.Add($"  competitionLabel: [dim]{Markup.Escape(choice.CompetitionLabel ?? string.Empty)}[/]");
            transferLines.Add($"  competitionReason: [dim]{Markup.Escape(choice.CompetitionReason ?? string.Empty)}[/]");
        }
        transferLines.Add("");
        transferLines.Add("[bold]Техническое закрытие ГМ:[/]");
        transferLines.Add("  • Закрыть через UpdateGuardianAbodeResidentTransferReceipts с requestId, residentId, transferMode, status.");
        transferLines.Add("  • accepted transfer: тот же residentId переносится в target guardian/abode, source departure и target arrival фиксируются history entries.");
        transferLines.Add("  • departure_only: resident перестаёт быть present в source roster и получает departure history entry.");
        transferLines.Add("  • refused: resident остаётся в source abode, receipt.status=refused и создаётся отказная history entry.");
        transferLines.Add("  • Резидент не может одновременно остаться present в source и появиться present в target.");
        AppendChaosSeaPendingFileRule(transferLines, GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        AppendChaosSeaCommonContractRules(transferLines);
        if (!ConfirmChaosSeaContractPreview(
                "Полный предпросмотр перехода резидента",
                transferLines,
                ToChaosSeaAuditNode(request),
                "Полный JSON pending resident transfer request"))
        {
            return false;
        }

        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, request);

        if (string.Equals(choice.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase))
        {
            _pendingGmAction =
                $"[ABODE_RESIDENT_TRANSFER_REQUEST] Игрок разрешает обитателю загробья '{resident.DisplayName}' (residentId={resident.ResidentId}, sourceGuardianId={sourceGuardianId}, sourceAbodeId={sourceAbodeId}, requestId={request.RequestId}) покинуть текущую Обитель без новой явно оформленной цели. " +
                "В принятом ходе закрой запрос через UpdateGuardianAbodeResidentTransferReceipts с transferMode=departure_only и status=departed_only. " +
                "Обитатель должен перестать быть present в текущем resident roster, а guardian_abode_residents.json.historyLog должен получить departure entry. " +
                "Не оформляй новое назначение хранителя или Обители одной только прозой и не оставляй request без canonical receipt.";
            MarkupLine("[cyan]Запрос на уход резидента без новой Обители отправлен GM.[/]");
        }
        else
        {
            var competitionNarrative = GuardianAbodeResidentRequestState.BuildTransferCompetitionNarrative(request);
            _pendingGmAction =
                $"[ABODE_RESIDENT_TRANSFER_REQUEST] Игрок разрешает afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, sourceGuardianId={sourceGuardianId}, sourceAbodeId={sourceAbodeId}, requestId={request.RequestId}) попытаться перейти в Обитель '{choice.TargetAbodeName}' Хранителя {choice.TargetGuardianName} (targetGuardianId={choice.TargetGuardianId}, targetAbodeId={choice.TargetAbodeId}). " +
                (string.IsNullOrWhiteSpace(competitionNarrative) ? string.Empty : $"Выбор цели: {competitionNarrative} ") +
                "В следующем подтверждённом ходе закрой этот запрос через UpdateGuardianAbodeResidentTransferReceipts. " +
                $"Если переход принят, resident должен сохранить тот же residentId, перейти в target guardian/abode через UpdateGuardianAbodeResidents, получить canonical arrival abode state для новой Обители и оставить departure+arrival history entries. " +
                "Если переход отвергнут, resident остаётся в source guardian/abode, а receipt должен иметь status=refused и refusal history entry. " +
                "Competition recommendation advisory only; не разрешай transfer только prose-описанием и не оставляй resident одновременно в source и target Abodes.";
            MarkupLine("[cyan]Запрос на переход резидента в другую Обитель отправлен GM.[/]");
        }

        return true;
    }

    private static string FormatAchievementRewardText(string rewardType, string rewardValue)
    {
        var normalizedType = rewardType.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType) && string.IsNullOrWhiteSpace(rewardValue))
            return "не указана";

        return normalizedType switch
        {
            "inkfeathers" => string.IsNullOrWhiteSpace(rewardValue) ? "Чернильные перья" : $"{rewardValue} Чернильных Перьев",
            "soulxp" => string.IsNullOrWhiteSpace(rewardValue) ? "Опыт души" : $"{rewardValue} опыта души",
            "title" => string.IsNullOrWhiteSpace(rewardValue) ? "Титул" : $"Титул: {rewardValue}",
            "none" => "нет",
            _ when string.IsNullOrWhiteSpace(rewardValue) => rewardType,
            _ => $"{rewardType}: {rewardValue}"
        };
    }

    private async Task ShowGuardianTradePanel(string guardianId)
    {
        if (_guardianTradeService == null)
        {
            MarkupLine("[red]❌ Сервис торговли недоступен.[/]");
            WaitForKey();
            return;
        }

        while (true)
        {
            var view = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync(), createPendingRequests: false);
            if (view == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
                WaitForKey();
                return;
            }

            if (!string.IsNullOrWhiteSpace(view.PendingInventoryRequestJson) &&
                !string.IsNullOrWhiteSpace(view.PendingInventoryRequestPath))
            {
                var requestAudit = JsonNode.Parse(view.PendingInventoryRequestJson);
                var tradeLines = new List<string>
                {
                    "[bold cyan]Подготовка торговой витрины Хранителя[/]",
                    "",
                    $"  Хранитель: [white]{Markup.Escape(view.GuardianName)}[/] [dim]({Markup.Escape(view.GuardianId)})[/]",
                    $"  Цикл возвращения: [dim]{Markup.Escape(view.TradeCycleId)}[/]",
                    $"  Репутация: [dim]{view.CurrentReputation} / {Markup.Escape(view.ReputationTierLabel)}[/]",
                    $"  Домен: [dim]{Markup.Escape(view.DomainDisplay)}[/]",
                    "",
                    "[bold]Контракт материализации для ГМ:[/]",
                    "  • Прочитать pending_guardian_trade_request.json как контракт, созданный клиентом.",
                    "  • Сгенерировать явный guardian.tradeInventory для текущего цикла возвращения.",
                    "  • Не выводить ассортимент только из prose/domain; слоты, потолок редкости и projectBonusSignature берутся из request.",
                    $"  • Закрыть через {GuardianTradeRequestState.UpdateReceiptsProperty} с requestId, tradeCycleId, itemCount, resolvedAtTurn, resolvedAtUtc.",
                    "  • До receipt-а покупки заблокированы, а витрина считается неподтверждённой."
                };
                AppendChaosSeaPendingFileRule(tradeLines, view.PendingInventoryRequestPath);
                AppendChaosSeaCommonContractRules(tradeLines);
                if (!ConfirmChaosSeaContractPreview(
                        "Полный предпросмотр торговли Хранителя",
                        tradeLines,
                        requestAudit,
                        "Полный JSON pending guardian trade request"))
                {
                    return;
                }

                try
                {
                    await GuardianTradeRequestState.WritePreparedJsonAsync(_fs, view.PendingInventoryRequestJson);
                }
                catch (InvalidOperationException ex)
                {
                    MarkupLine($"[red]❌ Не удалось записать pending_guardian_trade_request.json: {Markup.Escape(ex.Message)}[/]");
                    MarkupLine("[yellow]Проверьте текущий pending-файл и откройте торговлю заново после исправления конфликта.[/]");
                    WaitForKey();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                    _pendingGmAction = view.PendingGmAction;
                MarkupLine("[cyan]Витрина Хранителя подготавливается. Запрос на формирование ассортимента отправлен GM.[/]");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(view.PendingGmAction))
                _pendingGmAction = view.PendingGmAction;

            if (view.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(view.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            if (!view.InventoryReady && !string.IsNullOrWhiteSpace(view.InventoryStatusMessage))
                MarkupLine($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage)}[/]");

            var feathers = await ReadInkFeathersBalance();
            var guardianTradeRep = ReputationDisplay.BuildValueLabelMarkup(view.CurrentReputation, ReputationScaleKind.Guardian);
            var headerLines = new List<string>
            {
                $"[bold cyan]🛒 Торговля с Хранителем {Markup.Escape(view.GuardianName)}[/]",
                $"[dim]Домен: {Markup.Escape(view.DomainDisplay)} • Репутация: {guardianTradeRep}[/]",
                $"[dim]Чернильные Перья: {feathers} • Выкуп обратно: {view.BuybackOffers.Count}[/]",
                "[dim]Витрина обновляется после нового возвращения из смертной жизни.[/]"
            };
            if (!view.InventoryReady && !string.IsNullOrWhiteSpace(view.InventoryStatusMessage))
                headerLines.Add($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage)}[/]");
            if (!view.InventoryReady && view.InventoryRequestPending)
                headerLines.Add("[dim]Покупка реликвий откроется после ответа GM и подтверждения витрины.[/]");

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", headerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 1),
                Expand = true
            });

            var sectionChoices = new List<string>();
            if (view.InventoryReady)
                sectionChoices.Add("🛍 Купить реликвии");
            else
                sectionChoices.Add("🔄 Проверить витрину");
            sectionChoices.Add("🔁 Выкупить обратно");
            sectionChoices.Add("💰 Продать реликвии");
            sectionChoices.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите раздел:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(sectionChoices));

            if (choice.Contains("Назад"))
                return;

            if (choice.Contains("Проверить"))
            {
                if (!string.IsNullOrWhiteSpace(view.InventoryStatusMessage))
                    MarkupLine($"[yellow]⏳ {Markup.Escape(view.InventoryStatusMessage)}[/]");
                else
                    MarkupLine("[yellow]⏳ Витрина Хранителя ещё подготавливается.[/]");
                WaitForKey();
                Clear();
                continue;
            }

            if (choice.Contains("Купить"))
            {
                await ShowGuardianBuyMenu(guardianId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
                continue;
            }

            if (choice.Contains("Выкупить"))
            {
                await ShowGuardianBuybackMenu(guardianId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
                continue;
            }

            if (choice.Contains("Продать"))
            {
                await ShowGuardianSellMenu(guardianId);
                await _stateManager.RefreshGameStateAsync();
                Clear();
            }
        }
    }

    private async Task ShowGuardianBuyMenu(string guardianId)
    {
        if (_guardianTradeService == null)
            return;

        while (true)
        {
            var refreshedView = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync());
            if (refreshedView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
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
                MarkupLine($"[yellow]⏳ {Markup.Escape(refreshedView.InventoryStatusMessage ?? "Витрина Хранителя ещё не подготовлена.")}[/]");
                WaitForKey();
                return;
            }

            var feathers = await ReadInkFeathersBalance();
            var choices = refreshedView.Offers.Select(offer =>
            {
                var soldTag = offer.SoldOut ? "РАСПРОДАНО" : "";
                var relicId = GetNodeString(offer.RelicData["relicId"]) ?? GetNodeString(offer.RelicData["id"]) ?? string.Empty;
                return ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
                    string.IsNullOrWhiteSpace(offer.SlotId) ? "" : $"slotId={offer.SlotId}",
                    string.IsNullOrWhiteSpace(relicId) ? "" : $"relicId={relicId}",
                    soldTag);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Покупка реликвий[/] [dim](перья: {feathers})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(10)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= refreshedView.Offers.Count)
                return;

            var offer = refreshedView.Offers[selectedIndex];
            var canBuy = !offer.SoldOut && feathers >= offer.PriceInFeathers;
            var decision = ShowGuardianTradeBuyPreview(refreshedView, offer, feathers, canBuy);
            if (decision != GuardianTradeBuyDecision.Buy)
                continue;

            var result = await _guardianTradeService.BuyAsync(guardianId, offer.SlotId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private enum GuardianTradeBuyDecision
    {
        Back,
        Buy
    }

    private GuardianTradeBuyDecision ShowGuardianTradeBuyPreview(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianTradeOffer offer, int currentFeathers, bool canBuy)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null, residentDoc: null, guardiansDoc: null);
        lines.Insert(1, $"  💰 Цена: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🛍️ Источник витрины: [cyan]{Markup.Escape(GuardianTradeDisplayDomain(offer.DomainTag))}[/]");
        lines.Insert(3, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");
        lines.Insert(4, $"  🪶 После покупки: [gold1]{Math.Max(0, currentFeathers - offer.PriceInFeathers)}[/]");
        lines.Insert(5, $"  guardianId: [dim]{Markup.Escape(view.GuardianId)}[/]");
        lines.Insert(6, $"  guardianName: [white]{Markup.Escape(view.GuardianName)}[/]");
        lines.Insert(7, $"  tradeCycleId: [dim]{Markup.Escape(view.TradeCycleId)}[/]");
        lines.Insert(8, $"  slotId: [dim]{Markup.Escape(offer.SlotId)}[/]");

        if (offer.SoldOut)
        {
            lines.Insert(9, "  [red]Статус витрины: слот уже распродан в текущем возвращении.[/]");
        }
        else if (currentFeathers < offer.PriceInFeathers)
        {
            lines.Insert(9, "  [yellow]Статус покупки: пока не хватает Чернильных Перьев для покупки.[/]");
        }
        lines.Add("");
        lines.Add("[bold]Каноническая локальная операция:[/]");
        lines.Add("  • game_state/meta/soul_state.json: Ink Feathers уменьшаются на priceInFeathers.");
        lines.Add("  • game_state/meta/soul_state.json: relicData клонируется в soulRelics.stored.");
        lines.Add("  • game_state/meta/guardians.json: выбранный tradeInventory.items[].soldOut=true.");
        lines.Add("  • Guardian buyback/sell history не создаётся при покупке; это именно покупка из готовой витрины.");
        lines.Add("  • Ход ГМ не отправляется: это согласованная локальная запись клиента.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Торговая реликвия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel(
            "Полный JSON покупки у Хранителя",
            BuildGuardianBuyAuditNode(view, offer, currentFeathers),
            Color.Gold1);

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

    private async Task ShowGuardianSellMenu(string guardianId)
    {
        if (_guardianTradeService == null)
            return;

        while (true)
        {
            var tradeView = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync());
            if (tradeView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
                WaitForKey();
                return;
            }

            if (tradeView.TradeBlocked)
            {
                MarkupLine($"[red]⛔ {Markup.Escape(tradeView.BlockReason ?? "Торговля недоступна.")}[/]");
                WaitForKey();
                return;
            }

            var offers = await _guardianTradeService.GetSellableRelicsAsync(guardianId);
            if (offers.Count == 0)
            {
                MarkupLine("[dim]В хранилище нет реликвий, доступных для продажи.[/]");
                WaitForKey();
                return;
            }

            var choices = offers.Select(offer =>
                ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
                    string.IsNullOrWhiteSpace(offer.RelicId) ? "" : $"relicId={offer.RelicId}"))
                .ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]Продажа реликвий[/] [dim](только из хранилища)[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(15)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= offers.Count)
                return;

            var offer = offers[selectedIndex];
            var currentFeathers = await ReadInkFeathersBalance();
            var confirm = ShowGuardianTradeSellPreview(tradeView, offer, currentFeathers);
            if (!confirm)
                continue;

            var result = await _guardianTradeService.SellAsync(guardianId, offer.RelicId, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private bool ShowGuardianTradeSellPreview(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianSellOffer offer, int currentFeathers)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null, residentDoc: null, guardiansDoc: null);
        lines.Insert(1, $"  💰 Вы получите: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");
        lines.Insert(3, $"  🪶 После продажи: [gold1]{currentFeathers + offer.PriceInFeathers}[/]");
        lines.Insert(4, $"  🛡️ Покупатель: [cyan]{Markup.Escape(view.GuardianName)}[/]");
        lines.Insert(5, $"  guardianId: [dim]{Markup.Escape(view.GuardianId)}[/]");
        lines.Insert(6, $"  tradeCycleId: [dim]{Markup.Escape(view.TradeCycleId)}[/]");
        lines.Insert(7, "  🔁 После продажи реликвия будет удалена из хранилища души и появится у этого Хранителя в обратном выкупе.");
        lines.Insert(8, "  [yellow]Продажу нельзя откатить без будущего обратного выкупа у того же Хранителя.[/]");
        lines.Add("");
        lines.Add("[bold]Каноническая локальная операция:[/]");
        lines.Add("  • game_state/meta/soul_state.json: Ink Feathers увеличиваются на sellPrice.");
        lines.Add("  • game_state/meta/soul_state.json: relicId удаляется из soulRelics.stored.");
        lines.Add("  • game_state/meta/guardians.json: buybackRelics[] получает новую available запись с relicData, soldForPrice, buybackPrice, soldAtTurn и guardianId.");
        lines.Add("  • Ход ГМ не отправляется: это согласованная локальная запись клиента с полным JSON-аудитом.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 💰 Продажа Реликвии Души ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel("Полный JSON продаваемой Реликвии Души", BuildGuardianSellAuditNode(view, offer, currentFeathers), Color.Gold1);

        return Confirm(
            $"Продать «{offer.Name}» за {offer.PriceInFeathers} 🪶? Реликвия перейдёт в обратный выкуп у Хранителя.",
            false);
    }

    internal static JsonObject BuildGuardianBuyAuditNode(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianTradeOffer offer, int currentFeathers)
    {
        var relicId = GetNodeString(offer.RelicData["relicId"]) ?? GetNodeString(offer.RelicData["id"]) ?? string.Empty;
        return new JsonObject
        {
            ["guardianId"] = view.GuardianId,
            ["guardianName"] = view.GuardianName,
            ["tradeCycleId"] = view.TradeCycleId,
            ["slotId"] = offer.SlotId,
            ["relicId"] = relicId,
            ["relicName"] = offer.Name,
            ["rarity"] = offer.Rarity,
            ["priceInFeathers"] = offer.PriceInFeathers,
            ["currentFeathers"] = currentFeathers,
            ["projectedFeathers"] = Math.Max(0, currentFeathers - offer.PriceInFeathers),
            ["soldOut"] = offer.SoldOut,
            ["domainTag"] = offer.DomainTag,
            ["transactionKind"] = "guardian_trade_buy",
            ["transactionCorrelationId"] = $"guardian_trade_buy:{view.GuardianId}:{view.TradeCycleId}:{offer.SlotId}:{relicId}",
            ["affectedFiles"] = new JsonArray
            {
                "game_state/meta/soul_state.json",
                "game_state/meta/guardians.json"
            },
            ["localTransaction"] = "mark guardian tradeInventory slot soldOut, add relicData to soulRelics.stored, decrease Ink Feathers",
            ["relicData"] = offer.RelicData.DeepClone()
        };
    }

    internal static JsonObject BuildGuardianSellAuditNode(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianSellOffer offer, int currentFeathers) =>
        new()
        {
            ["guardianId"] = view.GuardianId,
            ["guardianName"] = view.GuardianName,
            ["tradeCycleId"] = view.TradeCycleId,
            ["relicId"] = offer.RelicId,
            ["relicName"] = offer.Name,
            ["rarity"] = offer.Rarity,
            ["sellPriceInFeathers"] = offer.PriceInFeathers,
            ["currentFeathers"] = currentFeathers,
            ["projectedFeathers"] = currentFeathers + offer.PriceInFeathers,
            ["transactionKind"] = "guardian_trade_sell",
            ["transactionCorrelationId"] = $"guardian_trade_sell:{view.GuardianId}:{view.TradeCycleId}:{offer.RelicId}",
            ["affectedFiles"] = new JsonArray
            {
                "game_state/meta/soul_state.json",
                "game_state/meta/guardians.json"
            },
            ["localTransaction"] = "remove soulRelics.stored relic, add guardian buybackRelics available entry, increase Ink Feathers",
            ["generatedBuybackEntryFields"] = new JsonArray
            {
                "buybackEntryId",
                "guardianId",
                "guardianName",
                "relicId",
                "relicData",
                "soldForPrice",
                "buybackPrice",
                "soldAtTurn",
                "soldAtUtc",
                "status=available"
            },
            ["relicData"] = offer.RelicData.DeepClone()
        };

    private async Task ShowGuardianBuybackMenu(string guardianId)
    {
        if (_guardianTradeService == null)
            return;

        while (true)
        {
            var tradeView = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync());
            if (tradeView == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить данные торговли Хранителя.[/]");
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
                MarkupLine("[dim]У этого Хранителя нет реликвий, доступных для обратного выкупа.[/]");
                WaitForKey();
                return;
            }

            var feathers = await ReadInkFeathersBalance();
            var offerChoices = BuildUniqueChoiceOptions(tradeView.BuybackOffers, offer =>
                ConsoleLayout.PlainChoiceLabel(
                    $"🔁 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
                    string.IsNullOrWhiteSpace(offer.BuybackEntryId) ? "" : $"buybackEntryId={offer.BuybackEntryId}",
                    string.IsNullOrWhiteSpace(offer.RelicId) ? "" : $"relicId={offer.RelicId}"));
            var choices = offerChoices.Select(item => item.Label).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]Обратный выкуп реликвий[/] [dim](доступно: {tradeView.BuybackOffers.Count} • перья: {feathers})[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .PageSize(15)
                .AddChoices(choices));

            if (selected.Contains("Назад"))
                return;

            var selectedOffer = offerChoices.FirstOrDefault(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;
            if (selectedOffer == null)
                return;

            var offer = selectedOffer;
            if (!ShowGuardianTradeBuybackPreview(tradeView, offer, feathers, feathers >= offer.PriceInFeathers))
                continue;

            var result = await _guardianTradeService.BuyBackAsync(guardianId, offer.BuybackEntryId, await TryReadCurrentTurnNumberAsync());
            MarkupLine(result.Success
                ? $"[green]✅ {Markup.Escape(result.Message)}[/]"
                : $"[red]❌ {Markup.Escape(result.Message)}[/]");
            WaitForKey();

            if (result.StateChanged)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private bool ShowGuardianTradeBuybackPreview(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianBuybackOffer offer, int currentFeathers, bool canBuyBack)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null, residentDoc: null, guardiansDoc: null);
        lines.Insert(1, $"  🔁 Цена обратного выкупа: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");
        lines.Insert(3, $"  🪶 После выкупа: [gold1]{Math.Max(0, currentFeathers - offer.PriceInFeathers)}[/]");
        lines.Insert(4, $"  guardianId: [dim]{Markup.Escape(view.GuardianId)}[/]");
        lines.Insert(5, $"  guardianName: [white]{Markup.Escape(view.GuardianName)}[/]");
        lines.Insert(6, $"  tradeCycleId: [dim]{Markup.Escape(view.TradeCycleId)}[/]");
        lines.Insert(7, $"  buybackEntryId: [dim]{Markup.Escape(offer.BuybackEntryId)}[/]");
        lines.Insert(8, $"  💸 Продана ранее за: [grey]{offer.SoldForPrice} 🪶[/]");
        lines.Insert(9, $"  🕰 Продана на ходу: [grey]{offer.SoldAtTurn}[/]");

        if (currentFeathers < offer.PriceInFeathers)
            lines.Insert(10, "  [yellow]Статус выкупа: пока не хватает Чернильных Перьев.[/]");
        lines.Add("");
        lines.Add("[bold]Каноническая локальная операция:[/]");
        lines.Add("  • game_state/meta/soul_state.json: Ink Feathers уменьшаются на priceInFeathers.");
        lines.Add("  • game_state/meta/soul_state.json: relicData возвращается в soulRelics.stored.");
        lines.Add("  • game_state/meta/guardians.json: buybackRelics[].status меняется с available на rebought for matching buybackEntryId.");
        lines.Add("  • Ход ГМ не отправляется: это согласованная локальная запись клиента с полным JSON-аудитом.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🔁 Реликвия обратного выкупа ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteJsonAuditPanel(
            "Полный JSON обратного выкупа у Хранителя",
            BuildGuardianBuybackAuditNode(view, offer, currentFeathers),
            Color.Gold1);

        var actions = new List<string>();
        if (canBuyBack)
            actions.Add("🔁 Выкупить");
        actions.Add("← Назад к выкупу");

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(actions));

        return action.Contains("Выкупить", StringComparison.OrdinalIgnoreCase);
    }

    internal static JsonObject BuildGuardianBuybackAuditNode(Services.GuardianTradeService.GuardianTradeView view, Services.GuardianTradeService.GuardianBuybackOffer offer, int currentFeathers) =>
        new()
        {
            ["guardianId"] = view.GuardianId,
            ["guardianName"] = view.GuardianName,
            ["tradeCycleId"] = view.TradeCycleId,
            ["buybackEntryId"] = offer.BuybackEntryId,
            ["relicId"] = offer.RelicId,
            ["relicName"] = offer.Name,
            ["rarity"] = offer.Rarity,
            ["priceInFeathers"] = offer.PriceInFeathers,
            ["currentFeathers"] = currentFeathers,
            ["projectedFeathers"] = Math.Max(0, currentFeathers - offer.PriceInFeathers),
            ["statusBefore"] = "available",
            ["statusAfter"] = "rebought",
            ["soldForPrice"] = offer.SoldForPrice,
            ["soldAtTurn"] = offer.SoldAtTurn,
            ["transactionKind"] = "guardian_trade_buyback",
            ["transactionCorrelationId"] = $"guardian_trade_buyback:{view.GuardianId}:{view.TradeCycleId}:{offer.BuybackEntryId}:{offer.RelicId}",
            ["affectedFiles"] = new JsonArray("game_state/meta/soul_state.json", "game_state/meta/guardians.json"),
            ["stateTransition"] = new JsonObject
            {
                ["soul_state.inkFeathers.current"] = $"{currentFeathers} -> {Math.Max(0, currentFeathers - offer.PriceInFeathers)}",
                ["soul_state.soulRelics.stored.add"] = offer.RelicId,
                ["guardians[].buybackRelics[].status"] = "available -> rebought",
                ["guardians[].buybackRelics[].buybackEntryId"] = offer.BuybackEntryId
            },
            ["description"] = offer.Description,
            ["relicData"] = offer.RelicData.DeepClone()
        };

    private static string DescribeFounderLoyaltyTier(string? founderLoyaltyTier) =>
        (founderLoyaltyTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            PlayerGuardianFoundationState.FounderLoyaltyTierSoulbound => "неразрывная верность",
            _ => founderLoyaltyTier ?? string.Empty
        };
}

