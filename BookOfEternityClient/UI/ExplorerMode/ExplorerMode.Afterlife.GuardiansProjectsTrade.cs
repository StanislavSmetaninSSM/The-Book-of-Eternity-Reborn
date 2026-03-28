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
{private static List<string> ReadSoulPreviousNames(JsonElement root, string currentSoulName)
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

            var choices = guardians.Select(g =>
            {
                var name = GuardianManifestation.GetDisplayName(g);
                if (string.IsNullOrWhiteSpace(name))
                    name = "?";
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
                    domainRu,
                    $"♥ {rep}",
                    repTierTag,
                    string.IsNullOrEmpty(moodTag) ? "" : moodTag,
                    string.IsNullOrEmpty(abodeName) ? "" : $"🏛 {abodeName}",
                    locTag);
            }).ToList();

            // Navigation options
            choices.Add("🔍 Искать новую обитель (силой мысли)");
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
                var attraction = await _systemGuardianLibraryService.ReadAttractionRequestAsync();
                if (attraction != null)
                {
                    pendingNotice += $"\n  [magenta1]🧲 Задано притяжение к извечному Хранителю: {Markup.Escape(attraction.TargetPresetDisplayName)}[/]";
                }
            }

            var unreadTradeNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianTradeInventoryReady, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (unreadTradeNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]📬 Непрочитанные ответы по торговле:[/]";
                foreach (var notification in unreadTradeNotifications.Take(3))
                    pendingNotice += $"\n  [dim]• {Markup.Escape(notification.Summary)}[/]";
                if (unreadTradeNotifications.Count > 3)
                    pendingNotice += $"\n  [dim]… и ещё {unreadTradeNotifications.Count - 3}. Откройте /уведомления_загробья[/]";
            }

            var unreadGuardianQuestNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (unreadGuardianQuestNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]📜 Новые квесты Хранителей:[/]";
                foreach (var notification in unreadGuardianQuestNotifications.Take(3))
                    pendingNotice += $"\n  [dim]• {Markup.Escape(notification.Summary)}[/]";
                if (unreadGuardianQuestNotifications.Count > 3)
                    pendingNotice += $"\n  [dim]… и ещё {unreadGuardianQuestNotifications.Count - 3}. Откройте /уведомления_загробья[/]";
            }

            var unreadResidentNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentsReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentRelicGranted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentManifestationReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeCompanionImprintManifestationReady, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTalkAnswered, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRefused, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unreadResidentNotifications.Count > 0)
            {
                pendingNotice += "\n  [yellow]🏛 События Обители:[/]";
                foreach (var notification in unreadResidentNotifications.Take(3))
                    pendingNotice += $"\n  [dim]• {Markup.Escape(notification.Summary)}[/]";
                if (unreadResidentNotifications.Count > 3)
                    pendingNotice += $"\n  [dim]… и ещё {unreadResidentNotifications.Count - 3}. Откройте /уведомления_загробья[/]";
            }

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🛡️ {_loc.T("guardians_info")} — Обители Моря Хаоса[/]" +
                    (string.IsNullOrEmpty(pendingNotice) ? "" : $"\n{pendingNotice}"))
                .PageSize(20)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            if (selected.Contains("Искать новую обитель"))
            {
                await ShowSearchAbodePrompt();
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

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌊 Поиск в Море Хаоса ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowGuardianProjects()
    {
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
                var projectName = GetStr(project, "projectName", GetStr(project, "name", "Проект"));
                var activeState = GetStr(project, "activeState", "");
                var finalState = GetStr(project, "finalState", "");
                var status = string.IsNullOrWhiteSpace(activeState) ? finalState : activeState;
                return ConsoleLayout.PlainChoiceLabel(
                    $"🔬 {projectName}",
                    string.IsNullOrWhiteSpace(guardianName) ? guardianId : guardianName,
                    string.IsNullOrWhiteSpace(status) ? "" : status);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]🔬 Проекты Хранителей[/]")
                .PageSize(18)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= allEntries.Count)
                return;

            ShowGuardianProjectDetailPanel(allEntries[selectedIndex], guardianNames, journalDoc?.RootElement, trackerRoot);
        }
    }

    private async Task ShowAbodePower()
    {
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
            ? history.EnumerateArray().Reverse().Take(6).ToList()
            : new List<JsonElement>();
        var journalEntries = CollectGuardianPowerJournalEntries(journalRoot ?? default, guardianId).Take(8).ToList();

        var lines = new List<string>
        {
            $"[bold gold1]🏛 {Markup.Escape(guardianName)}[/]",
            "",
            $"[bold]Текущее значение:[/] [{derivedState.TierColor}]{currentPower}[/]/100 [dim]({Markup.Escape(derivedState.TierLabel)})[/]",
            $"[bold]Derived-эффекты:[/] [dim]Торговля {derivedState.TradeSlotCount} • Квесты {derivedState.GuardianQuestCap} • Потолок сложности до {FormatGuardianQuestDifficultyLabel(derivedState.GuardianQuestDifficultyCeiling)} • Бонус-гата +{derivedState.BonusGachaCharges} • Бюджет корректив {derivedState.EffectiveNextLifeCorrectionBudgetPoints}[/]",
            $"[bold]Нити судьбы:[/] [dim]Clues {derivedState.EffectiveRivalArcDefenseClues} • Clarity {derivedState.RivalArcClarityTier} • Counter-quest {(derivedState.RivalArcCounterQuestAccess ? "да" : "нет")} • Warning tier {derivedState.RivalArcWarningTier}[/]",
            $"[bold]Hostile cap:[/] [dim]{Markup.Escape(derivedState.RivalArcOffenseCap)}[/]"
        };

        if (derivedState.EffectiveGuardianRarityCeilingBonusSteps > 0 || derivedState.EffectiveUpgradedTradeSlots > 0 || derivedState.EffectiveElevatedTradeSlots > 0)
        {
            lines.Add($"[bold]Ковка реликтов:[/] [dim]Upgraded slots {derivedState.EffectiveUpgradedTradeSlots} • Elevated slots {derivedState.EffectiveElevatedTradeSlots} • Ceiling +{derivedState.EffectiveGuardianRarityCeilingBonusSteps}[/]");
        }

        if (projectEffects.BonusLoreUnlocks > 0 || projectEffects.QuestHookCount > 0 || projectEffects.GuaranteedArchiveQuestCount > 0 || projectEffects.SpecialQuestLineUnlocks > 0 || projectEffects.VisibleRivalClueBonus > 0 || projectEffects.ArchiveWarningTierBonus > 0)
        {
            lines.Add($"[bold]Исследование знания:[/] [dim]Фрагменты {projectEffects.BonusLoreUnlocks} • Quest hooks {projectEffects.QuestHookCount} • Archive quests {projectEffects.GuaranteedArchiveQuestCount} • Special lines {projectEffects.SpecialQuestLineUnlocks} • Bonus clues {projectEffects.VisibleRivalClueBonus} • Warning +{projectEffects.ArchiveWarningTierBonus}[/]");
        }

        if (projectEffects.PreparationBudgetPoints > 0 || projectEffects.PreparationClaimPriorityBonus > 0 || projectEffects.HostilePriorityTokensGranted > 0)
        {
            lines.Add($"[bold]Подготовка души:[/] [dim]Budget +{projectEffects.PreparationBudgetPoints} • Claim priority +{projectEffects.PreparationClaimPriorityBonus} • Hostile tokens {projectEffects.HostilePriorityTokensGranted}[/]");
        }

        if (derivedState.FortificationSafePressureBonus > 0 || derivedState.FortificationDefenseRatingBonus > 0 || temporaryModifiers.Count > 0)
        {
            lines.Add($"[bold]Политическая защита:[/] [dim]Safe pressure +{derivedState.FortificationSafePressureBonus} • Defense rating +{derivedState.FortificationDefenseRatingBonus} • Temp modifiers {temporaryModifiers.Count}[/]");
        }

        if (historyEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Последние изменения в canonical history:[/]");
            foreach (var entry in historyEntries)
            {
                var change = GetInt(entry, "change", 0);
                var title = GetStr(entry, "reason", GetStr(entry, "reasonType", ""));
                var timestamp = GetStr(entry, "timestamp", "");
                var deltaText = change > 0 ? $"[green]+{change}[/]" : $"[red]{change}[/]";
                var tsText = !string.IsNullOrWhiteSpace(timestamp) && timestamp.Length >= 10 ? $"[dim]{Markup.Escape(timestamp[..10])}[/] " : "";
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
        "rival_strike" => "Удар rival-Хранителя",
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
            lines.Add($"{indent}[bold]Политический удар:[/] [dim]{Markup.Escape(string.IsNullOrWhiteSpace(targetGuardianName) ? "цель не указана" : targetGuardianName)} • Power loss {targetLoss} • Pressure +{pressureDelta} • Stability -{stabilityDamage}[/]");
            return;
        }

        if (project.TryGetProperty("projectOutcomeAudit", out var outcomeAudit) && outcomeAudit.ValueKind == JsonValueKind.Object)
        {
            var projectType = GetStr(project, "projectType", "");
            if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"{indent}[bold]Контр-операция:[/] [dim]Pressure relief {GetInt(outcomeAudit, "pressureRelief", 0)} • Stability relief +{GetInt(outcomeAudit, "stabilityRelief", 0)}[/]");
            }
            else if (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"{indent}[bold]Фортификация:[/] [dim]Safe pressure +{GetInt(outcomeAudit, "safePressureBonus", 0)} • Defense rating +{GetInt(outcomeAudit, "defenseRatingBonus", 0)}[/]");
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
                     .ThenBy(item => item.ModifierId, StringComparer.OrdinalIgnoreCase)
                     .Take(4))
        {
            lines.Add($"{indent}  [dim]{Markup.Escape(modifier.ModifierType)} • value {modifier.Value:+#;-#;0} • applications {modifier.RemainingApplications}[/]");
            if (!string.IsNullOrWhiteSpace(modifier.ModifierId))
                lines.Add($"{indent}  [dim]modifierId: {Markup.Escape(modifier.ModifierId)}[/]");
        }
    }

    private static string FormatGuardianQuestDifficultyLabel(string? difficulty) =>
        AbodePowerRules.NormalizeGuardianQuestDifficulty(difficulty) switch
        {
            "easy" => "Лёгкой",
            "hard" => "Тяжёлой",
            "epic" => "Эпической",
            _ => "Нормальной"
        };

    private async Task ShowAbodesNavigation()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (doc == null) { ShowEmptyPanel("Обители", "Данные хранителей недоступны"); return; }

        var root = doc.RootElement;
        var guardians = CollectGuardianDisplayEntries(root);

        // Filter to guardians with abodes
        var abodeGuardians = guardians
            .Where(g => g.TryGetProperty("abode", out var ab) && ab.ValueKind == JsonValueKind.Object)
            .ToList();

        if (abodeGuardians.Count == 0)
        {
            ShowEmptyPanel("Обители", "Обители ещё не открыты. Используйте /хранители для поиска.");
            return;
        }

        // Current abode
        var currentAbodeId = "";
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("chaosSeaNavigation", out var nav) && nav.ValueKind == JsonValueKind.Object)
            currentAbodeId = GetStr(nav, "currentAbodeId", "");

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
                var isCurrent = abId == currentAbodeId;
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
            var selGName = GuardianManifestation.GetDisplayName(selGuardian);
            if (string.IsNullOrWhiteSpace(selGName))
                selGName = "?";

            if (selAbodeId == currentAbodeId)
            {
                MarkupLine($"[dim]Вы уже находитесь в обители «{Markup.Escape(selAbodeName)}».[/]");
                WaitForKey();
                continue;
            }

            _pendingGmAction =
                $"[CHAOS_SEA_TRAVEL] Душа выбирает перемещение в обитель '{selAbodeName}'" +
                $"{(string.IsNullOrWhiteSpace(selAbodeId) ? "" : $" (abodeId={selAbodeId})")}, связанную с Хранителем '{selGName}'. " +
                "Обработай само путешествие как полноценный ход: опиши прибытие, реакцию Хранителя и обнови chaosSeaNavigation.currentAbodeId в guardians.json.";

            MarkupLine($"[cyan]🌊 Переход в обитель «{Markup.Escape(selAbodeName)}» отправляется Мастеру Игры...[/]");
            return;
        }
    }

    private async Task ShowGuardianDetailPanel(JsonElement g, List<JsonElement>? allGuardians = null, string currentAbodeId = "", string activeGuardianId = "", JsonElement? guardianProjectTrackerRoot = null)
    {
        var name = GuardianManifestation.GetDisplayName(g);
        if (string.IsNullOrWhiteSpace(name))
            name = "Неизвестный";
        var guardianId = GetStr(g, "guardianId", "");
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
        var domain = GetStr(g, "domain", "");
        var content = new Grid().AddColumn(new GridColumn());
        content.AddRow(new Markup($"[bold cyan]🛡️ {Markup.Escape(name)}[/]"));

        var summaryTable = ConsoleLayout.CreateInfoTable();
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
            summaryTable.AddRow(new Markup("[yellow]Домен[/]"), new Markup($"[yellow]{Markup.Escape(domainRu)}[/] [dim]({Markup.Escape(domain)})[/]"));
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

        var manifestationStyleLabel = GuardianManifestation.GetPresentationStyleLabel(manifestationStyle);
        if (!string.IsNullOrWhiteSpace(manifestationStyleLabel))
            summaryTable.AddRow(new Markup("[dim]Подача[/]"), new Markup($"[dim]{Markup.Escape(manifestationStyleLabel)}[/]"));

        if (!string.IsNullOrWhiteSpace(manifestationPronouns))
            summaryTable.AddRow(new Markup("[dim]Местоимения[/]"), new Markup($"[dim]{Markup.Escape(manifestationPronouns)}[/]"));

        var formFlexibilityLabel = GuardianManifestation.GetFormFlexibilityLabel(formFlexibility);
        if (!string.IsNullOrWhiteSpace(formFlexibilityLabel))
            summaryTable.AddRow(new Markup("[dim]Гибкость формы[/]"), new Markup($"[dim]{Markup.Escape(formFlexibilityLabel)}[/]"));

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
            lines.Add("");
            lines.Add($"  [bold]🏛️ Сила Обители:[/] {ConsoleLayout.CreateBar(Math.Clamp(abodePowerValue * 20 / 100, 0, 20), 20, derivedState.TierColor)} [{derivedState.TierColor}]{abodePowerValue}[/]/100 [dim]({Markup.Escape(derivedState.TierLabel)})[/]");
            lines.Add($"    [dim]Торговых слотов: {derivedState.TradeSlotCount} • Лимит квестов: {derivedState.GuardianQuestCap} • Потолок сложности: до {FormatGuardianQuestDifficultyLabel(derivedState.GuardianQuestDifficultyCeiling)} • Бонус-гата: +{derivedState.BonusGachaCharges} • Бюджет корректив: {derivedState.EffectiveNextLifeCorrectionBudgetPoints}[/]");
            if (derivedState.EffectiveGuardianRarityCeilingBonusSteps > 0 ||
                derivedState.EffectiveUpgradedTradeSlots > 0)
            {
                lines.Add($"    [dim]Ковка: upgraded {derivedState.EffectiveUpgradedTradeSlots} • elevated {derivedState.EffectiveElevatedTradeSlots} • ceiling +{derivedState.EffectiveGuardianRarityCeilingBonusSteps}[/]");
            }
            if (guardianProjectEffects.BonusLoreUnlocks > 0 || guardianProjectEffects.QuestHookCount > 0 || guardianProjectEffects.GuaranteedArchiveQuestCount > 0 || guardianProjectEffects.SpecialQuestLineUnlocks > 0 || guardianProjectEffects.VisibleRivalClueBonus > 0 || guardianProjectEffects.ArchiveWarningTierBonus > 0)
            {
                lines.Add($"    [dim]Исследование: фрагменты {guardianProjectEffects.BonusLoreUnlocks} • hooks {guardianProjectEffects.QuestHookCount} • archive quests {guardianProjectEffects.GuaranteedArchiveQuestCount} • special lines {guardianProjectEffects.SpecialQuestLineUnlocks} • clues {guardianProjectEffects.VisibleRivalClueBonus} • warning +{guardianProjectEffects.ArchiveWarningTierBonus}[/]");
            }
            if (derivedState.FortificationSafePressureBonus > 0 || derivedState.FortificationDefenseRatingBonus > 0)
            {
                lines.Add($"    [dim]Политический щит: safe pressure +{derivedState.FortificationSafePressureBonus} • defense rating +{derivedState.FortificationDefenseRatingBonus}[/]");
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
                lines.Add($"    [dim]Последний power event: {latestDeltaText} {Markup.Escape(latestReason)}[/]");
            }

            var lastInteraction = GetStr(rd, "lastInteraction", "");
            if (!string.IsNullOrEmpty(lastInteraction) && lastInteraction.Length >= 10)
                lines.Add($"  [dim]Последняя встреча: {Markup.Escape(lastInteraction[..10])}[/]");

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
                    if (!string.IsNullOrEmpty(ts) && ts.Length >= 10) timeStr = $"[dim]{Markup.Escape(ts[..10])}[/] ";
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
                lines.Add($"    [dim]Legacy summary: {projProgress}%[/]");
            }
        }

        var completedTrackerProjects = !string.IsNullOrWhiteSpace(guardianId)
            ? CollectCompletedGuardianProjects(trackerRoot, guardianId)
            : new List<JsonElement>();
        if (completedTrackerProjects.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold]✅ Завершённые проекты:[/] [dim]({completedTrackerProjects.Count})[/]");
            foreach (var completedProject in completedTrackerProjects.Take(5))
            {
                var projectName = GetStr(completedProject, "projectName", GetStr(completedProject, "name", "?"));
                var finalState = GetStr(completedProject, "finalState", "");
                var completionTurn = GetInt(completedProject, "completionTurn", 0);
                var outcome = GetStr(completedProject, "outcome", "");
                var turnTag = completionTurn > 0 ? $" [dim](ход {completionTurn})[/]" : "";
                lines.Add($"    ✓ [white]{Markup.Escape(projectName)}[/] [dim]{Markup.Escape(finalState)}[/]{turnTag}");
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
            if ((qm.TryGetProperty("activeQuests", out var aq) || qm.TryGetProperty("availableQuests", out aq)) && aq.ValueKind == JsonValueKind.Array && aq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]📜 Активные задания:[/]");
                foreach (var q in aq.EnumerateArray())
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
                            var val = rp.Value.ValueKind == JsonValueKind.Number ? rp.Value.ToString() : rp.Value.GetRawText();
                            rewParts.Add($"{rp.Name}: {val}");
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
                    var dateStr = !string.IsNullOrEmpty(qDate) && qDate.Length >= 10 ? $" [dim]{Markup.Escape(qDate[..10])}[/]" : "";
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
            : GuardianGachaChargeRules.GetChargesPerReturnForReputation(rep, derivedState);
        var chargesUsedThisReturn = hasGachaSystem && gs.TryGetProperty("chargesUsedThisReturn", out var cur) && cur.ValueKind == JsonValueKind.Number && cur.TryGetInt32(out var parsedUsed)
            ? GuardianGachaChargeRules.ClampUsedCharges(parsedUsed, chargesPerReturn)
            : 0;
        var remainingCharges = Math.Max(0, chargesPerReturn - chargesUsedThisReturn);

        if (chargesPerReturn <= 0)
        {
            lines.Add("    [red]Гача через этого Хранителя сейчас заблокирована вашей репутацией.[/]");
        }
        else
        {
            lines.Add($"    Осталось попыток в этом возвращении: [yellow]{remainingCharges}[/]/[white]{chargesPerReturn}[/]");
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
                var timeStr = !string.IsNullOrEmpty(hTs) && hTs.Length >= 10 ? $"[dim]{Markup.Escape(hTs[..10])}[/] " : "";
                var rarityTag = string.IsNullOrWhiteSpace(rarity) ? "" : $" [dim](редкость: {Markup.Escape(rarity)})[/]";
                lines.Add($"      {timeStr}💎 {Markup.Escape(relicId)} [dim](стоимость: {Markup.Escape(cost)})[/]{rarityTag}");
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
                var attitude = GetStr(rel, "attitude", "");
                var reason = GetStr(rel, "reason", "");
                var (attIcon, attColor, attRu) = attitude.ToLowerInvariant() switch
                {
                    "ally" => ("🤝", "green", "Союзник"),
                    "neutral" => ("😐", "grey", "Нейтрален"),
                    "curious" => ("🔍", "cyan", "Любопытствует"),
                    "competitive" => ("⚔", "yellow", "Конкурент"),
                    "rival" => ("⚔", "orange1", "Соперник"),
                    "enemy" => ("💀", "red", "Враг"),
                    _ => ("👤", "white", attitude)
                };
                lines.Add($"    {attIcon} [{attColor}]{Markup.Escape(tgtName)}[/] — [{attColor}]{Markup.Escape(attRu)}[/]");
                if (!string.IsNullOrEmpty(reason))
                    lines.Add($"      [dim italic]{Markup.Escape(reason)}[/]");
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
            lines.Add("  [bold]🧠 Актуальные мысли Хранителя:[/]");
            foreach (var entry in guardianThoughtEntries.Take(3))
                lines.Add($"    • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");
        }

        if (guardianSocialEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📚 Краткая память общения:[/]");
            foreach (var entry in guardianSocialEntries.Take(5))
                lines.Add($"    • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");
        }

        if (pendingGuardianTalkRequest != null || pendingGuardianLoreRequest != null)
        {
            lines.Add("");
            lines.Add("  [bold]⏳ Ожидают ответа GM:[/]");
            if (pendingGuardianTalkRequest != null)
                lines.Add($"    • Разговор [yellow]ожидает[/] [dim](requestId={Markup.Escape(pendingGuardianTalkRequest.RequestId)})[/]");
            if (pendingGuardianLoreRequest != null)
                lines.Add($"    • Вопрос о знаниях [yellow]ожидает[/] [dim](requestId={Markup.Escape(pendingGuardianLoreRequest.RequestId)})[/]");
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
                    ? $"[dim]{Markup.Escape(changedAt[..10])}[/] "
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

        var isActiveGuardian = string.Equals(GetStr(g, "guardianId", ""), activeGuardianId, StringComparison.OrdinalIgnoreCase);
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
            lines.Add("    [white]Доступна: 4 локальных слота, обновление после нового возвращения из смертной жизни.[/]");

        FlushLines();

        Write(new Panel(content)
        {
            Header = new PanelHeader($" 🛡️ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        await ShowGuardianDetailActions(g, name, currentAbodeId, activeGuardianId);
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
            infoTable.AddRow(new Markup("[dim]Тип[/]"), new Markup($"[dim]{Markup.Escape(projectType)}[/]"));
        var projectTier = GetStr(project, "projectTier", "");
        if (!string.IsNullOrWhiteSpace(projectTier))
            infoTable.AddRow(new Markup("[dim]Тир[/]"), new Markup($"[dim]{Markup.Escape(projectTier)}[/]"));
        var projectMode = GetStr(project, "projectMode", "");
        if (!string.IsNullOrWhiteSpace(projectMode))
            infoTable.AddRow(new Markup("[dim]Режим[/]"), new Markup($"[dim]{Markup.Escape(projectMode)}[/]"));
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
            infoTable.AddRow(new Markup("[yellow]Статус[/]"), new Markup($"[yellow]{Markup.Escape(statusLabel)}[/]"));

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
            AppendGuardianProjectSummaryLines(panelLines, project, "");
        }
        else
        {
            var outcome = GetStr(project, "outcome", "");
            var completionTurn = GetInt(project, "completionTurn", 0);
            var abodePowerDelta = GetInt(project, "abodePowerDelta", 0);
            if (completionTurn > 0)
                panelLines.Add($"Ход завершения: [white]{completionTurn}[/]");
            if (!string.IsNullOrWhiteSpace(finalState))
                panelLines.Add($"Terminal state: [white]{Markup.Escape(finalState)}[/]");
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
            foreach (var journalEntry in journalEntries.Take(8))
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
                    foreach (var detail in details.EnumerateArray().Take(4))
                    {
                        if (detail.ValueKind == JsonValueKind.String)
                            panelLines.Add($"    [dim]- {Markup.Escape(detail.GetString() ?? "")}[/]");
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
            lines.Add($"{indent}Статус: [yellow]{Markup.Escape(activeState)}[/]");
        if (totalWork > 0)
        {
            var normalized = Math.Clamp(workDone * 18 / Math.Max(1, totalWork), 0, 18);
            lines.Add($"{indent}Работа: [cyan]{new string('━', normalized)}[/][dim]{new string('┄', 18 - normalized)}[/] [white]{workDone}[/]/[dim]{totalWork}[/]");
        }
        if (totalStages > 0)
            lines.Add($"{indent}Стадия: [white]{currentStage}[/]/[dim]{totalStages}[/]");
        lines.Add($"{indent}Pressure: [orange1]{pressure}[/]  •  Stability: [green]{stability}[/]");
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
                lines.Add($"{indent}[bold]Временный эффект:[/] [dim]Trade refresh {Math.Max(0, tradeGranted - tradeSpent)}/{tradeGranted} • Gacha use {Math.Max(0, gachaGranted - gachaSpent)}/{gachaGranted}[/]");
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
                var targetText = targetIncarnation > 0 ? $" • Target life #{targetIncarnation}" : "";
                lines.Add($"{indent}[bold]Остаток эффекта:[/] [dim]Hooks {Math.Max(0, hookGranted - hookSpent)}/{hookGranted} • Archive quests {Math.Max(0, archiveQuestGranted - archiveQuestConsumed)}/{archiveQuestGranted} • Special {Math.Max(0, specialGranted - specialSpent)}/{specialGranted} • Clues {Math.Max(0, clueGranted - clueSpent)}/{clueGranted} • Warning +{warningBonus}{targetText}[/]");
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
                var targetText = targetIncarnation > 0 ? $" • Target life #{targetIncarnation}" : "";
                lines.Add($"{indent}[bold]Остаток эффекта:[/] [dim]Prep budget {Math.Max(0, prepGranted - prepSpent)}/{prepGranted} • Hostile tokens {Math.Max(0, hostileGranted - hostileSpent)}/{hostileGranted} • Consumed {(consumed ? "yes" : "no")}{targetText}[/]");
                break;
            }
        }
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

    private async Task ShowGuardianDetailActions(JsonElement guardian, string guardianName, string currentAbodeId, string activeGuardianId)
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

        var hasImageSupport = _imageService != null && !string.IsNullOrWhiteSpace(imagePrompt);
        var hasAbodeImageSupport = _imageService != null && !string.IsNullOrWhiteSpace(abodeImagePrompt);
        if (!tradeAvailable && !socialAvailable && !hasImageSupport && !hasAbodeImageSupport)
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

            if (hasImageSupport)
            {
                var hasImage = _imageService!.EntityImageExists("guardian", guardianImageKey);
                actions.Add("🖼 Показать изображение хранителя");
                if (hasImage)
                    actions.Add("♻ Пересоздать изображение хранителя");
            }

            if (hasAbodeImageSupport)
            {
                var hasAbodeImage = _imageService!.EntityImageExists("abode", abodeImageKey);
                actions.Add("🏛 Показать изображение обители");
                if (hasAbodeImage)
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
                await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, request);
                _pendingGmAction =
                    $"[GUARDIAN_SOCIAL_TALK_REQUEST] Игрок обращается к Хранителю '{guardianName}' (guardianId={guardianId}, requestId={request.RequestId}) с обычным разговором. " +
                    "В accepted turn отыграй сцену и обязательно закрой запрос через guardianSocialJournalUpdates entry с requestId, guardianId, interactionType=talk, status=accepted|rejected|cancelled, optional responseMode, title, summary, turn и timestamp. " +
                    "guardianThoughtJournalUpdates остаётся рекомендуемым, но matching guardianSocialJournalUpdates entry обязателен.";
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
                await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, request);
                _pendingGmAction =
                    $"[GUARDIAN_SOCIAL_LORE_REQUEST] Игрок просит Хранителя '{guardianName}' (guardianId={guardianId}, requestId={request.RequestId}) поделиться знанием или лором. " +
                    "В accepted turn отыграй сцену и обязательно закрой запрос через guardianSocialJournalUpdates entry с requestId, guardianId, interactionType=lore, status=accepted|rejected|cancelled, optional responseMode=lore_revealed|lore_refused|warning|refusal, title, summary, turn и timestamp. " +
                    "Если знание реально раскрыто, при необходимости добавь guardianThoughtJournalUpdates и любые canonical lore/quest outcomes отдельно от самого social closure.";
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

            if (action.Contains("обители", StringComparison.OrdinalIgnoreCase))
            {
                var abodeImageExists = _imageService!.EntityImageExists("abode", abodeImageKey);
                if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && abodeImageExists)
                    await RegenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey);
                else
                    await _imageService.ShowOrGenerateEntityImageAsync(abodeImagePrompt, "abode", abodeImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }

            if (!hasImageSupport)
                continue;

            var imageExists = _imageService!.EntityImageExists("guardian", guardianImageKey);
            if (action.Contains("Пересоздать", StringComparison.OrdinalIgnoreCase) && imageExists)
            {
                await RegenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Показать", StringComparison.OrdinalIgnoreCase))
            {
                await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, "guardian", guardianImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }
        }
    }

    private async Task ShowGuardianAbodeResidentsPanel(JsonElement guardian)
    {
        var guardianId = GetStr(guardian, "guardianId", "");
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        if (guardian.TryGetProperty("abode", out var abode) is false || abode.ValueKind != JsonValueKind.Object)
        {
            ShowEmptyPanel("Обитатели Обители", "У этого Хранителя нет materialized Обители.");
            return;
        }

        var abodeId = GetStr(abode, "abodeId", "");
        var abodeName = GetStr(abode, "name", "Обитель");
        if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(abodeId))
        {
            ShowEmptyPanel("Обитатели Обители", "Обитель ещё не materialized достаточно явно для resident roster.");
            return;
        }

        while (true)
        {
            var residentsDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
            var residents = residentsDoc != null
                ? GuardianAbodeResidentState.CollectEntries(residentsDoc.RootElement, guardianId, abodeId)
                : new List<GuardianAbodeResidentState.ResidentEntry>();

            if (residents.Count == 0)
            {
                var pendingRequests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
                var matchesCurrentRequest = pendingRequests.Any(pendingRequest =>
                    string.Equals(pendingRequest.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pendingRequest.AbodeId, abodeId, StringComparison.OrdinalIgnoreCase));

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
                        CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
                    };
                    await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, request);
                    _pendingGmAction =
                        $"[ABODE_RESIDENT_ROSTER_REQUEST] Игрок открыл roster Обители Хранителя '{guardianName}' (guardianId={guardianId}, abodeId={abodeId}, abodeName={abodeName}). " +
                        "В accepted turn materialize explicit residents через UpdateGuardianAbodeResidents в guardian_abode_residents.json и закрой request через UpdateGuardianAbodeResidentRosterReceipts. " +
                        "Не выводи roster из домена Хранителя. Авторски создай 2-4 afterlife residents с residentId, residentKind, roleLabel, bondLevel, bondTier, canGrantCompanionRelic, bondRewardState и mortalWorldImprint.";
                }

                var pendingLines = new List<string>
                {
                    $"[bold cyan]🏛 {Markup.Escape(abodeName)}[/]",
                    "",
                    "В глубине Обители начинают проступать иные сущности.",
                    "Roster обитателей запрошен у GM.",
                    "",
                    "[dim]Откройте панель позже, когда explicit resident state будет materialized.[/]"
                };

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
                return (
                    $"👤 {Markup.Escape(resident.DisplayName)} [dim]({Markup.Escape(GuardianAbodeResidentState.GetResidentKindLabel(resident.ResidentKind))})[/] " +
                    $"[{bondColor}]{Markup.Escape(GuardianAbodeResidentState.GetBondTierLabel(resident.BondTier))}[/] [dim]{resident.BondLevel}/100[/]",
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
        var thoughtJournalEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectThoughtJournalEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.JournalEntry>();
        var interactionLogEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectInteractionLogEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.JournalEntry>();
        var historyLogEntries = residentStateDoc != null
            ? GuardianAbodeResidentState.CollectHistoryLogEntries(residentStateDoc.RootElement, resident.ResidentId)
            : new List<GuardianAbodeResidentState.HistoryLogEntry>();
        var pendingInteractionRequests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        var pendingTalkRequest = pendingInteractionRequests.FirstOrDefault(request =>
            string.Equals(request.ResidentId, resident.ResidentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, GuardianAbodeResidentState.InteractionTypeTalk, StringComparison.OrdinalIgnoreCase));
        var pendingHistoryRequest = pendingInteractionRequests.FirstOrDefault(request =>
            string.Equals(request.ResidentId, resident.ResidentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase));

        var lines = new List<string>
        {
            $"[bold cyan]👤 {Markup.Escape(resident.DisplayName)}[/]",
            "",
            $"  Вид: [white]{Markup.Escape(GuardianAbodeResidentState.GetResidentKindLabel(resident.ResidentKind))}[/]",
            $"  Роль: [dim]{Markup.Escape(resident.RoleLabel)}[/]",
            $"  Связь: [white]{Markup.Escape(GuardianAbodeResidentState.GetBondTierLabel(resident.BondTier))}[/] [dim]({resident.BondLevel}/100)[/]",
            $"  История: {(resident.HistoryRevealed ? "[green]раскрыта[/]" : "[dim]ещё не раскрыта[/]")}",
            $"  Награда связи: [dim]{Markup.Escape(GuardianAbodeResidentState.GetRewardStateLabel(resident.BondRewardState))}[/]"
        };

        if (!string.IsNullOrWhiteSpace(resident.Summary))
        {
            lines.Add("");
            lines.Add($"[white]{Markup.Escape(resident.Summary)}[/]");
        }

        if (!string.IsNullOrWhiteSpace(resident.OriginWorldSummary))
            lines.Add($"  Мир-исток: [dim]{Markup.Escape(resident.OriginWorldSummary)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.BondReason))
            lines.Add($"  Причина связи: [dim]{Markup.Escape(resident.BondReason)}[/]");
        if (resident.CoreTraits.Count > 0)
            lines.Add($"  Черты: [dim]{Markup.Escape(string.Join(", ", resident.CoreTraits))}[/]");
        if (resident.ArchetypeHints.Count > 0)
            lines.Add($"  Архетипы: [dim]{Markup.Escape(string.Join(", ", resident.ArchetypeHints))}[/]");
        if (!string.IsNullOrWhiteSpace(resident.LinkedSoulQuestId))
            lines.Add($"  Связанный квест души: [yellow]{Markup.Escape(resident.LinkedSoulQuestId)}[/]");
        if (!string.IsNullOrWhiteSpace(resident.GrantedRelicId))
            lines.Add($"  Дарованная реликвия: [green]{Markup.Escape(resident.GrantedRelicId)}[/]");
        if (pendingTalkRequest != null)
            lines.Add($"  Разговор: [yellow]ожидает ответа GM[/] [dim](requestId={Markup.Escape(pendingTalkRequest.RequestId)})[/]");
        if (pendingHistoryRequest != null)
            lines.Add($"  История: [yellow]ожидает ответа GM[/] [dim](requestId={Markup.Escape(pendingHistoryRequest.RequestId)})[/]");
        if (thoughtJournalEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Актуальные мысли:[/]");
            foreach (var thoughtEntry in thoughtJournalEntries.Take(3))
            {
                var line = string.IsNullOrWhiteSpace(thoughtEntry.Title)
                    ? thoughtEntry.Summary
                    : $"{thoughtEntry.Title} — {thoughtEntry.Summary}";
                lines.Add($"  • [white]{Markup.Escape(line)}[/]");
            }
        }
        if (interactionLogEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Краткая память общения:[/]");
            foreach (var interactionEntry in interactionLogEntries.Take(5))
            {
                var line = string.IsNullOrWhiteSpace(interactionEntry.Title)
                    ? interactionEntry.Summary
                    : $"{interactionEntry.Title} — {interactionEntry.Summary}";
                lines.Add($"  • [white]{Markup.Escape(line)}[/]");
            }
        }
        if (historyLogEntries.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Раскрытые фрагменты прошлого:[/]");
            foreach (var historyEntry in historyLogEntries.Take(5))
            {
                var title = string.IsNullOrWhiteSpace(historyEntry.Title) ? historyEntry.EntryId : historyEntry.Title;
                lines.Add($"  • [white]{Markup.Escape(title)}[/]");
                if (!string.IsNullOrWhiteSpace(historyEntry.Summary))
                    lines.Add($"    [dim]{Markup.Escape(historyEntry.Summary)}[/]");
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Обитатель Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var availableInteractions = resident.AvailableInteractions.Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var useDefaultInteractions = availableInteractions.Count == 0;
        var actions = new List<string>();
        if (useDefaultInteractions || availableInteractions.Contains("talk"))
            actions.Add("💬 Поговорить");
        if (useDefaultInteractions || availableInteractions.Contains("history"))
            actions.Add("📖 Выслушать прошлую историю");
        if (useDefaultInteractions || availableInteractions.Contains("quest"))
            actions.Add("🧵 Помочь с личной просьбой");
        if ((useDefaultInteractions || availableInteractions.Contains("reward")) &&
            resident.CanGrantCompanionRelic &&
            string.Equals(resident.BondRewardState, GuardianAbodeResidentState.RewardStateEligible, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("💎 Принять реликвию связи");
        }
        actions.Add("← Назад");

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices(actions));

        if (action.Contains("← Назад", StringComparison.Ordinal))
            return;

        if (action.StartsWith("💎", StringComparison.Ordinal))
        {
            _pendingGmAction =
                $"[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}, abodeName={abodeName}). " +
                "В accepted turn выдай новую Soul Relic через metaStateUpdates.soulRelicOperations.addRelic. " +
                $"Реликвия должна иметь relicType={GuardianAbodeResidentState.RelicTypeCompanionEcho}, sourceResidentId={resident.ResidentId}, sourceGuardianId={guardianId}, sourceGuardianName={guardianName}, rarity не ниже Rare и complete companionSeed с companionNameHint, originWorldSummary, futureCompanionPrompt, bondReason, coreTraits, archetypeHints, appearanceMotifs. " +
                $"Также обнови resident state через UpdateGuardianAbodeResidents так, чтобы bondRewardState стал '{GuardianAbodeResidentState.RewardStateGranted}', а grantedRelicId указывал на выданную реликвию. " +
                "Не забудь добавить residentInteractionLogUpdates с кратким summary вручения реликвии и его последствия.";
            MarkupLine("[cyan]Реликвия связи запрошена у GM.[/]");
            return;
        }

        if (action.StartsWith("🧵", StringComparison.Ordinal))
        {
            _pendingGmAction =
                $"[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}). " +
                "В accepted turn roleplay the request and materialize/advance an ordinary soul quest via UpdateSoulQuests. " +
                $"Soul quest должен иметь guardianId={guardianId}, relatedAfterlifeResidentId={resident.ResidentId} и player-facing title/description. " +
                "При необходимости также обнови resident bondLevel/bondTier и linkedSoulQuestId через UpdateGuardianAbodeResidents. " +
                "Оставь residentInteractionLogUpdates с коротким summary просьбы/прогресса, чтобы у ГМа была curated память этого шага.";
            MarkupLine("[cyan]Личная просьба обитателя отправлена GM.[/]");
            return;
        }

        if (action.StartsWith("📖", StringComparison.Ordinal))
        {
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
            await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, request);
            _pendingGmAction =
                $"[ABODE_RESIDENT_HISTORY_REQUEST] Игрок просит afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, requestId={request.RequestId}) раскрыть прошлую историю. " +
                "В accepted turn обязательно закрой запрос через UpdateGuardianAbodeResidentInteractionReceipts со status=accepted|rejected|cancelled. " +
                "Если история действительно раскрыта, либо установи historyRevealed=true, либо добавь запись через UpdateGuardianAbodeResidentHistoryLog, либо обнови mortalWorldImprint. " +
                "После accepted ответа обязательно добавь residentThoughtJournalUpdates и/или residentInteractionLogUpdates с краткой памятью результата сцены. " +
                "Обычный отказ допустим, но он тоже должен быть явно закрыт receipt-ом.";
            MarkupLine("[cyan]История обитателя запрошена у GM.[/]");
            return;
        }

        if (action.StartsWith("💬", StringComparison.Ordinal))
        {
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
            await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, request);
            _pendingGmAction =
                $"[ABODE_RESIDENT_TALK] Игрок разговаривает с afterlife resident '{resident.DisplayName}' (residentId={resident.ResidentId}, guardianId={guardianId}, abodeId={abodeId}, abodeName={abodeName}, requestId={request.RequestId}). " +
                "В accepted turn отыграй сцену и обязательно закрой запрос через UpdateGuardianAbodeResidentInteractionReceipts со status=accepted|rejected|cancelled. " +
                "После accepted ответа обязательно оставь residentThoughtJournalUpdates и/или residentInteractionLogUpdates с краткой памятью результата сцены. " +
                "Если были meaningful state changes, обнови resident state через UpdateGuardianAbodeResidents.";
            MarkupLine("[cyan]Разговор с обитателем Обители отправлен GM.[/]");
            return;
        }

        return;
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
            var view = await _guardianTradeService.EnsureTradeInventoryAsync(guardianId, _stateManager.CurrentState.Incarnation, await TryReadCurrentTurnNumberAsync());
            if (view == null)
            {
                MarkupLine("[red]❌ Не удалось загрузить витрину Хранителя.[/]");
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
                headerLines.Add("[dim]Покупка реликвий откроется после ответа GM и materialization витрины.[/]");

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
                return ConsoleLayout.PlainChoiceLabel(
                    $"💎 {offer.Name}",
                    offer.Rarity,
                    $"🪶 {offer.PriceInFeathers}",
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
            var decision = ShowGuardianTradeBuyPreview(offer, feathers, canBuy);
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

    private GuardianTradeBuyDecision ShowGuardianTradeBuyPreview(Services.GuardianTradeService.GuardianTradeOffer offer, int currentFeathers, bool canBuy)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null);
        lines.Insert(1, $"  💰 Цена: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🛍️ Источник витрины: [cyan]{Markup.Escape(GuardianTradeDisplayDomain(offer.DomainTag))}[/]");
        lines.Insert(3, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");

        if (offer.SoldOut)
        {
            lines.Insert(4, "  [red]Статус витрины: слот уже распродан в текущем возвращении.[/]");
        }
        else if (currentFeathers < offer.PriceInFeathers)
        {
            lines.Insert(4, "  [yellow]Статус покупки: пока не хватает Чернильных Перьев для покупки.[/]");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛒 Торговая реликвия ", Justify.Center),
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
                    $"🪶 {offer.PriceInFeathers}"))
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
            var confirm = Confirm($"Продать «{offer.Name}» за {offer.PriceInFeathers} 🪶?", false);
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
                    $"🪶 {offer.PriceInFeathers}"));
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
            if (!ShowGuardianTradeBuybackPreview(offer, feathers, feathers >= offer.PriceInFeathers))
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

    private bool ShowGuardianTradeBuybackPreview(Services.GuardianTradeService.GuardianBuybackOffer offer, int currentFeathers, bool canBuyBack)
    {
        using var relicDoc = JsonDocument.Parse(offer.RelicData.ToJsonString());
        var lines = BuildSoulRelicDetailLines(offer.Name, relicDoc.RootElement, null);
        lines.Insert(1, $"  🔁 Цена обратного выкупа: [yellow]{offer.PriceInFeathers} 🪶[/]");
        lines.Insert(2, $"  🪶 У вас сейчас: [gold1]{currentFeathers}[/]");
        lines.Insert(3, $"  💸 Продана ранее за: [grey]{offer.SoldForPrice} 🪶[/]");
        lines.Insert(4, $"  🕰 Продана на ходу: [grey]{offer.SoldAtTurn}[/]");

        if (currentFeathers < offer.PriceInFeathers)
            lines.Insert(5, "  [yellow]Статус выкупа: пока не хватает Чернильных Перьев.[/]");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🔁 Реликвия обратного выкупа ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

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
}

