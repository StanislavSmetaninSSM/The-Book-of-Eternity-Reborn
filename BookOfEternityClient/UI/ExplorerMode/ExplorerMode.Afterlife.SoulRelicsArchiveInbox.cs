using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{private async Task ShowSoulRelics()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable(_loc.T("soul_relics")))
            return;

        var isAfterlifeRealm = IsOrdinaryAfterlifeInteractionState;

        while (true)
        {
            // Re-read file each iteration to see updates after equip/unequip
            var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
            if (doc == null)
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Данные реликвий недоступны");
                return;
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("soulRelics", out var relics) ||
                (relics.ValueKind != JsonValueKind.Object && relics.ValueKind != JsonValueKind.Array))
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Реликвии души ещё не найдены");
                return;
            }

            var allRelics = new List<(string Id, string Name, string Status, JsonElement Data, int IndexInArray)>();
            int idx = 0;

            if (relics.ValueKind == JsonValueKind.Array)
            {
                // Flat array format — determine equipped status from gameplayStatus.equipped
                foreach (var r in relics.EnumerateArray())
                {
                    var isEquipped = false;
                    if (r.TryGetProperty("gameplayStatus", out var gs) && gs.TryGetProperty("equipped", out var eq))
                        isEquipped = eq.ValueKind == JsonValueKind.True;
                    allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), isEquipped ? "equipped" : "stored", r, idx));
                    idx++;
                }
            }
            else
            {
                // Object format with equipped/stored arrays
                if (relics.TryGetProperty("equipped", out var equipped) && equipped.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in equipped.EnumerateArray())
                    {
                        allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), "equipped", r, idx));
                        idx++;
                    }
                }
                idx = 0;
                if (relics.TryGetProperty("stored", out var stored) && stored.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in stored.EnumerateArray())
                    {
                        allRelics.Add((GetRelicIdentity(r), GetStr(r, "name", "Неизвестная реликвия"), "stored", r, idx));
                        idx++;
                    }
                }
            }

            if (allRelics.Count == 0)
            {
                ShowEmptyPanel(_loc.T("soul_relics"), "Реликвии души ещё не найдены");
                return;
            }

            var choices = MakeUniqueChoiceLabels(allRelics.Select(r =>
            {
                var statusTag = r.Status == "equipped" ? "[green]⚔ экипировано[/]" : "[dim]📦 хранилище[/]";
                var slotStr = "";
                if (r.Status == "equipped")
                {
                    var s = GetStr(r.Data, "slot", "");
                    if (string.IsNullOrEmpty(s) && r.Data.TryGetProperty("equipmentData", out var ed))
                        s = GetStr(ed, "equipSlot", "");
                    if (string.IsNullOrEmpty(s) && r.Data.TryGetProperty("gameplayStatus", out var gs))
                        s = GetStr(gs, "currentSlot", "");
                    if (!string.IsNullOrEmpty(s)) slotStr = $" [[{Markup.Escape(FormatSoulRelicSlotLabel(s))}]]";
                }
                return ($"💎 {Markup.Escape(r.Name)}{slotStr} {statusTag}", r.Id);
            }).ToList());
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]✨ {_loc.T("soul_relics")}[/]" +
                    (isAfterlifeRealm ? "  [dim](выберите для просмотра / управления)[/]"
                                      : "  [yellow dim](только просмотр — управление в загробном цикле)[/]"))
                .PageSize(15)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= allRelics.Count) break;

            var (relicId, relicName, relicStatus, relicData, _) = allRelics[selIdx];
            var shouldRefresh = await ShowRelicDetailPanel(relicId, relicName, relicStatus, relicData, isAfterlifeRealm);
            if (shouldRefresh)
                await _stateManager.RefreshGameStateAsync();
        }
    }

    private async Task ShowAfterlifeArchive()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Архив души"))
            return;

        while (true)
        {
            await SyncAfterlifeNotificationsAsync();
            var entries = await ReadStoredAfterlifeArchiveEntriesAsync();
            if (entries.Count == 0)
            {
                ShowEmptyPanel("📚 Архив души", "Архив души пока пуст.");
                return;
            }

            var unreadArchiveNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
                .Where(notification =>
                    string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                    notification.NotificationType.StartsWith("archive_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (unreadArchiveNotifications.Count > 0)
            {
                var bannerLines = new List<string>
                {
                    "[bold yellow]📬 Непрочитанные ответы Хранителей по Архиву[/]"
                };
                foreach (var notification in unreadArchiveNotifications.Take(3))
                    bannerLines.Add($"• {Markup.Escape(notification.Summary)}");
                if (unreadArchiveNotifications.Count > 3)
                    bannerLines.Add($"[dim]… и ещё {unreadArchiveNotifications.Count - 3}. Откройте /уведомления_загробья[/]");

                Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", bannerLines)))
                {
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(1, 1),
                    Expand = true
                });
            }

            var choices = MakeUniqueChoiceLabels(entries.Select(entry =>
            {
                var typeLabel = AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType);
                var rarityColor = GetRarityColor(entry.Rarity);
                var reservationLabel = entry.IsReserved
                    ? $" [yellow][ожидает: {Markup.Escape(AfterlifeArchiveState.GetReservationLabel(entry.ReservationKind))}][/]"
                    : string.Empty;
                return ($"📚 {Markup.Escape(entry.Title)} [dim]({Markup.Escape(typeLabel)})[/] [{rarityColor}]{Markup.Escape(entry.Rarity)}[/]{reservationLabel}", entry.ArchiveId);
            }).ToList());
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]📚 Архив души[/] [dim](сохранённые загробные записи)[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= entries.Count)
                return;

            var entry = entries[index];
            var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
            var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
            var sourceGuardianLabel = ResolveArchiveGuardianLabel(entry, guardiansDoc?.RootElement);
            var targetProjectLabel = ResolveArchiveProjectLabel(entry, trackerDoc?.RootElement);
            var lines = new List<string>
            {
                $"[bold yellow]📚 {Markup.Escape(entry.Title)}[/]",
                "",
                $"  Тип: [cyan]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType))}[/]",
                $"  Редкость: [{GetRarityColor(entry.Rarity)}]{Markup.Escape(DescribeRarityLabel(entry.Rarity))}[/]",
                $"  Источник жизни: [yellow]{entry.SourceLife}[/]",
                $"  Источник записи: [dim]{Markup.Escape(AfterlifeArchiveState.GetSourceKindLabel(entry.SourceKind))}[/]"
            };

            if (!string.IsNullOrWhiteSpace(sourceGuardianLabel))
                lines.Add($"  Связанный хранитель: [white]{Markup.Escape(sourceGuardianLabel)}[/]");
            if (!string.IsNullOrWhiteSpace(entry.SourceGuardianId))
                lines.Add($"  [dim]Идентификатор хранителя: {Markup.Escape(entry.SourceGuardianId)}[/]");
            if (!string.IsNullOrWhiteSpace(entry.SourceEntryId))
                lines.Add($"  Исходная запись Кодекса: [dim]{Markup.Escape(entry.SourceEntryId)}[/]");
            if (!string.IsNullOrWhiteSpace(entry.AcquiredAtUtc))
                lines.Add($"  Сохранено в Архив: [dim]{Markup.Escape(entry.AcquiredAtUtc)}[/]");
            if (entry.Tags.Count > 0)
                lines.Add($"  Метки: [dim]{Markup.Escape(string.Join(", ", entry.Tags))}[/]");
            if (entry.IsReserved)
            {
                var reservedFor = !string.IsNullOrWhiteSpace(entry.ReservedForGuardianName)
                    ? entry.ReservedForGuardianName
                    : entry.ReservedForGuardianId;
                lines.Add($"  Статус: [yellow]зарезервирована[/] для [white]{Markup.Escape(reservedFor)}[/] через [yellow]{Markup.Escape(AfterlifeArchiveState.GetReservationLabel(entry.ReservationKind))}[/]");
                if (!string.IsNullOrWhiteSpace(targetProjectLabel))
                    lines.Add($"  Целевой проект: [white]{Markup.Escape(targetProjectLabel)}[/]");
                if (!string.IsNullOrWhiteSpace(entry.ReservedForProjectId))
                    lines.Add($"  [dim]Идентификатор проекта: {Markup.Escape(entry.ReservedForProjectId)}[/]");
            }
            var entryBody = string.IsNullOrWhiteSpace(entry.Content) ? entry.Summary : entry.Content;
            if (!string.IsNullOrWhiteSpace(entryBody))
            {
                lines.Add("");
                lines.Add($"[white]{Markup.Escape(entryBody)}[/]");
            }

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📚 Архив души ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var actions = new List<string>();
            if (!string.IsNullOrWhiteSpace(entry.SourceEntryId))
                actions.Add("📖 Открыть исходную запись Кодекса");
            if (!string.IsNullOrWhiteSpace(entry.SourceGuardianId))
                actions.Add("🛡 Открыть связанного Хранителя");
            if (!string.IsNullOrWhiteSpace(entry.ReservedForProjectId))
                actions.Add("🔬 Открыть целевой проект");
            var consultationAvailable = await CanUseArchiveConsultationAsync(entry);
            if (consultationAvailable)
                actions.Add("🔮 Консультация с дружественным Хранителем");
            var projectFuelAvailable = await CanUseArchiveProjectFuelAsync(entry);
            if (projectFuelAvailable)
                actions.Add("⚙️ Вложить запись в активный проект Хранителя");
            if (entry.IsReserved)
                actions.Add("[dim]Запись ожидает ответа GM[/]");
            actions.Add("← Назад");

            if (actions.Count == 1 || (actions.Count == 2 && entry.IsReserved))
            {
                WaitForKey();
                continue;
            }

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(actions));

            if (action.StartsWith("⚙️", StringComparison.Ordinal))
            {
                var fuelResult = await StartArchiveProjectFuelAsync(entry);
                if (fuelResult)
                {
                    await _stateManager.RefreshGameStateAsync();
                    continue;
                }
            }
            else if (action.StartsWith("🔮", StringComparison.Ordinal))
            {
                var consultationResult = await StartArchiveConsultationAsync(entry);
                if (consultationResult)
                {
                    await _stateManager.RefreshGameStateAsync();
                    continue;
                }
            }
            else if (action.StartsWith("🛡", StringComparison.Ordinal))
            {
                if (await TryShowLinkedArchiveGuardianAsync(entry.SourceGuardianId))
                {
                    await _stateManager.RefreshGameStateAsync();
                    continue;
                }
            }
            else if (action.StartsWith("📖", StringComparison.Ordinal))
            {
                if (await TryShowCodexEntryByIdAsync(entry.SourceEntryId))
                    continue;
            }
            else if (action.StartsWith("🔬", StringComparison.Ordinal))
            {
                if (await TryShowLinkedArchiveProjectAsync(entry.ReservedForProjectId))
                {
                    await _stateManager.RefreshGameStateAsync();
                    continue;
                }
            }

            WaitForKey();
        }
    }

    private async Task ShowAfterlifeInbox()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Уведомления загробья"))
            return;

        while (true)
        {
            await SyncAfterlifeNotificationsAsync();
            var notifications = await AfterlifeNotificationState.ReadAsync(_fs);
            if (notifications.Count == 0)
            {
                ShowEmptyPanel("📬 Уведомления загробья", "Пока нет ответов Хранителей по торговле, Архиву или резидентам Обители.");
                return;
            }

            var choices = MakeUniqueChoiceLabels(notifications.Select(notification =>
            {
                var marker = string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase)
                    ? "[yellow]●[/]"
                    : "[dim]○[/]";
                return ($"{marker} {Markup.Escape(AfterlifeNotificationState.GetTypeLabel(notification.NotificationType))} [dim]— {Markup.Escape(notification.Summary)}[/]", notification.NotificationId);
            }).ToList());
            choices.Add("[green]✅ Отметить всё как прочитанное[/]");
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title("[bold yellow]📬 Уведомления загробья[/] [dim](торговля, Архив, резиденты Обители)[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            if (selected.Contains("Отметить всё", StringComparison.OrdinalIgnoreCase))
            {
                await AfterlifeNotificationState.MarkAllReadAsync(_fs);
                MarkupLine("[green]✅ Все загробные уведомления отмечены как прочитанные.[/]");
                continue;
            }

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= notifications.Count)
                return;

            await ShowAfterlifeNotificationDetailAsync(notifications[index]);
        }
    }

    private async Task ShowAfterlifeNotificationDetailAsync(AfterlifeNotificationState.NotificationEntry notification)
    {
        var lines = new List<string>
        {
            $"[bold yellow]📬 {Markup.Escape(AfterlifeNotificationState.GetTypeLabel(notification.NotificationType))}[/]",
            "",
            $"  Статус: {(string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) ? "[yellow]непрочитано[/]" : "[dim]прочитано[/]")}",
            $"  Сводка: {Markup.Escape(notification.Summary)}"
        };

        if (!string.IsNullOrWhiteSpace(notification.GuardianName))
            lines.Add($"  Хранитель: [cyan]{Markup.Escape(notification.GuardianName)}[/]");
        if (!string.IsNullOrWhiteSpace(notification.ArchiveTitle))
            lines.Add($"  Запись Архива: [white]{Markup.Escape(notification.ArchiveTitle)}[/]");
        if (!string.IsNullOrWhiteSpace(notification.TargetProjectName))
            lines.Add($"  Проект: [white]{Markup.Escape(notification.TargetProjectName)}[/]");
        if (notification.CreatedAtTurn > 0)
            lines.Add($"  Ход: [dim]{notification.CreatedAtTurn}[/]");
        if (!string.IsNullOrWhiteSpace(notification.CreatedAtUtc))
            lines.Add($"  Получено: [dim]{Markup.Escape(notification.CreatedAtUtc)}[/]");

        await AppendShiningNotificationDetailLinesAsync(notification, lines);
        await AppendPlayerGuardianFoundationNotificationDetailLinesAsync(notification, lines);
        await AppendExactAfterlifeNotificationDetailLinesAsync(notification, lines);

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📬 Ответ Хранителя ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var actions = new List<string>();
        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianTradeInventoryReady, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(notification.GuardianId))
        {
            actions.Add("🛒 Открыть торговлю");
        }

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypePlayerGuardianFoundationResolved, StringComparison.OrdinalIgnoreCase))
            actions.Add("🛡️ Открыть Хранителей");

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeShiningTradeInventoryReady, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeShiningCoreActionResolved, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("🧾 Открыть точное решение");
            actions.Add("✨ Открыть Сияющую Обитель");
        }

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeShiningFactionFoundingResolved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeShiningFactionRealignmentResolved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeShiningFactionLeadershipResolved, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("🧾 Открыть точное решение");
            actions.Add("🏛 Открыть политику Сияющей Обители");
        }

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("🧵 Открыть квест Хранителя");
            actions.Add("🛡️ Открыть Хранителей");
        }

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeCompanionImprintManifestationReady, StringComparison.OrdinalIgnoreCase))
            actions.Add("💎 Открыть реликвии души");

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentsReady, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentRelicGranted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentManifestationReady, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentWavering, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentRestless, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentConsideringDeparture, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferPending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferAccepted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferRefused, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTransferDeparted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentTalkAnswered, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRevealed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentHistoryRefused, StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("🛡️ Открыть Хранителей");
        }

        if (!string.IsNullOrWhiteSpace(notification.ResidentId))
            actions.Add("👤 Открыть резидента");

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase))
            actions.Add("🧵 Открыть квесты души");

        if (notification.NotificationType.StartsWith("archive_", StringComparison.OrdinalIgnoreCase))
            actions.Add("📚 Открыть Архив души");

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeArchiveConsultationAccepted, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(notification.GuardianId))
        {
            actions.Add("🛡️ Открыть Хранителей");
        }

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeArchiveProjectFuelAccepted, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(notification.TargetProjectId))
        {
            actions.Add("🔬 Открыть проекты Хранителей");
        }

        if (string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase))
            actions.Add("✅ Отметить как прочитанное");
        actions.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Yellow))
            .AddChoices(actions));

        if (selected.StartsWith("🧾", StringComparison.Ordinal))
        {
            await ShowShiningNotificationExactResolutionAsync(notification);
            return;
        }

        if (selected.StartsWith("🛒", StringComparison.Ordinal))
        {
            if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianTradeInventoryReady, StringComparison.OrdinalIgnoreCase))
                await ShowGuardianTradePanel(notification.GuardianId);
            else
                await ShowShiningTradeAndForgeAsync();
            return;
        }

        if (selected.StartsWith("✨", StringComparison.Ordinal))
        {
            await ShowShiningAbodeOverview();
            return;
        }

        if (selected.StartsWith("🏛", StringComparison.Ordinal))
        {
            await ShowShiningPoliticsOverview();
            return;
        }

        if (selected.StartsWith("📚", StringComparison.Ordinal))
        {
            if (!await ShowAfterlifeArchiveEntryDetailByIdAsync(notification.ArchiveId))
                await ShowAfterlifeArchive();
            return;
        }

        if (selected.StartsWith("🛡️", StringComparison.Ordinal))
        {
            await ShowGuardians();
            return;
        }

        if (selected.StartsWith("👤", StringComparison.Ordinal))
        {
            if (!await ShowGuardianAbodeResidentDetailByIdAsync(notification.ResidentId))
            {
                MarkupLine("[yellow]Не удалось открыть точную карточку резидента: текущий resident state недоступен или запись исчезла.[/]");
                WaitForKey();
            }
            return;
        }

        if (selected.StartsWith("🧵", StringComparison.Ordinal))
        {
            if (TryResolveGuardianQuestNotificationKey(notification, out var guardianId, out var guardianQuestId))
            {
                if (!await ShowGuardianQuestDetailByIdAsync(guardianId, guardianQuestId))
                    await ShowGuardians();
            }
            else if (!TryResolveResidentQuestNotificationQuestId(notification, out var questId) ||
                     !await ShowSoulQuestDetailByIdAsync(questId))
            {
                await ShowSoulQuests();
            }
            return;
        }

        if (selected.StartsWith("💎", StringComparison.Ordinal))
        {
            await ShowSoulRelics();
            return;
        }

        if (selected.StartsWith("🔬", StringComparison.Ordinal))
        {
            if (!await TryShowLinkedArchiveProjectAsync(notification.TargetProjectId))
                await ShowGuardianProjects();
            return;
        }

        if (selected.Contains("Отметить", StringComparison.OrdinalIgnoreCase))
            await AfterlifeNotificationState.MarkReadAsync(_fs, notification.NotificationId);
    }

    private async Task ShowShiningNotificationExactResolutionAsync(AfterlifeNotificationState.NotificationEntry notification)
    {
        if (!IsShiningNotificationType(notification.NotificationType))
            return;

        var context = await LoadShiningContextAsync();
        if (context == null)
        {
            MarkupLine("[yellow]Точное решение Сияющей Обители сейчас недоступно: состояние не читается.[/]");
            WaitForKey();
            return;
        }

        var lines = new List<string>
        {
            $"[bold yellow]🧾 Точное решение: {Markup.Escape(AfterlifeNotificationState.GetTypeLabel(notification.NotificationType))}[/]"
        };

        if (!string.IsNullOrWhiteSpace(notification.RequestId))
            lines.Add($"  Идентификатор запроса: [dim]{Markup.Escape(notification.RequestId)}[/]");
        if (notification.CreatedAtTurn > 0)
            lines.Add($"  Ход уведомления: [dim]{notification.CreatedAtTurn}[/]");
        if (!string.IsNullOrWhiteSpace(notification.CreatedAtUtc))
            lines.Add($"  Получено в UTC: [dim]{Markup.Escape(notification.CreatedAtUtc)}[/]");

        switch (notification.NotificationType)
        {
            case AfterlifeNotificationState.TypeShiningTradeInventoryReady:
                AppendShiningTradeNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningCoreActionResolved:
                AppendShiningCoreNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionFoundingResolved:
                AppendShiningFoundingNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionRealignmentResolved:
                AppendShiningRealignmentNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionLeadershipResolved:
                AppendShiningLeadershipNotificationDetails(context.Root, notification, lines);
                break;
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧾 Точное решение Сияющей Обители ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task AppendExactAfterlifeNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        await AppendExactArchiveNotificationDetailLinesAsync(notification, lines);
        await AppendExactProjectNotificationDetailLinesAsync(notification, lines);
        await AppendExactGuardianNotificationDetailLinesAsync(notification, lines);
        await AppendExactResidentNotificationDetailLinesAsync(notification, lines);
    }

    private async Task AppendExactArchiveNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(notification.ArchiveId))
            return;

        if (!string.IsNullOrWhiteSpace(notification.ArchiveEntryType) ||
            !string.IsNullOrWhiteSpace(notification.ArchiveRarity) ||
            !string.IsNullOrWhiteSpace(notification.ArchiveSummary))
        {
            lines.Add("");
            lines.Add("  [bold]Связанная запись Архива:[/]");
            lines.Add($"  Название: [white]{Markup.Escape(notification.ArchiveTitle ?? notification.ArchiveId)}[/]");
            if (!string.IsNullOrWhiteSpace(notification.ArchiveEntryType))
                lines.Add($"  Тип: [dim]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(notification.ArchiveEntryType))}[/]");
            if (!string.IsNullOrWhiteSpace(notification.ArchiveRarity))
                lines.Add($"  Редкость: [dim]{Markup.Escape(DescribeRarityLabel(notification.ArchiveRarity))}[/]");
            if (!string.IsNullOrWhiteSpace(notification.ArchiveSummary))
                lines.Add($"  Сводка записи: [dim]{Markup.Escape(notification.ArchiveSummary)}[/]");
            return;
        }

        using var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (soulDoc?.RootElement.ValueKind != JsonValueKind.Object ||
            !soulDoc.RootElement.TryGetProperty("afterlifeArchive", out var archiveRoot) ||
            archiveRoot.ValueKind != JsonValueKind.Object ||
            !archiveRoot.TryGetProperty("stored", out var storedEntries) ||
            storedEntries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var entry in storedEntries.EnumerateArray())
        {
            if (!string.Equals(GetStr(entry, "archiveId", ""), notification.ArchiveId, StringComparison.OrdinalIgnoreCase))
                continue;

            var archiveTitle = GetStr(entry, "title", notification.ArchiveTitle ?? notification.ArchiveId);
            var archiveType = GetStr(entry, "entryType", "?");
            var archiveRarity = GetStr(entry, "rarity", "?");
            var archiveSummary = GetStr(entry, "summary", "");
            lines.Add("");
            lines.Add("  [bold]Связанная запись Архива:[/]");
            lines.Add($"  Название: [white]{Markup.Escape(archiveTitle)}[/]");
            lines.Add($"  Тип: [dim]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(archiveType))}[/]");
            lines.Add($"  Редкость: [dim]{Markup.Escape(DescribeRarityLabel(archiveRarity))}[/]");
            if (!string.IsNullOrWhiteSpace(archiveSummary))
                lines.Add($"  Сводка записи: [dim]{Markup.Escape(archiveSummary)}[/]");
            return;
        }
    }

    private async Task AppendExactProjectNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(notification.TargetProjectId))
            return;

        if (!string.IsNullOrWhiteSpace(notification.TargetProjectStateLabel) ||
            notification.TargetProjectProgressPercent >= 0)
        {
            lines.Add("");
            lines.Add("  [bold]Связанный проект:[/]");
            lines.Add($"  Проект: [white]{Markup.Escape(notification.TargetProjectName ?? notification.TargetProjectId)}[/]");
            if (!string.IsNullOrWhiteSpace(notification.GuardianName))
                lines.Add($"  Хранитель: [white]{Markup.Escape(notification.GuardianName)}[/]");
            if (!string.IsNullOrWhiteSpace(notification.TargetProjectStateLabel))
                lines.Add($"  Состояние: [dim]{Markup.Escape(FormatGuardianProjectStateLabel(notification.TargetProjectStateLabel))}[/]");
            if (notification.TargetProjectProgressPercent >= 0)
                lines.Add($"  Прогресс: [dim]{notification.TargetProjectProgressPercent}%[/]");
            return;
        }

        using var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        if (trackerDoc?.RootElement.ValueKind != JsonValueKind.Object)
            return;

        foreach (var propertyName in new[] { "activeProjects", "completedProjects" })
        {
            if (!trackerDoc.RootElement.TryGetProperty(propertyName, out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object)
                    continue;
                if (!string.Equals(GetStr(project, "projectId", ""), notification.TargetProjectId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var guardianName = GetStr(entry, "guardianName", notification.GuardianName ?? "");
                lines.Add("");
                lines.Add("  [bold]Связанный проект:[/]");
                var projectName = GetStr(project, "projectName", GetStr(project, "name", notification.TargetProjectName ?? notification.TargetProjectId));
                lines.Add($"  Проект: [white]{Markup.Escape(projectName)}[/]");
                if (!string.IsNullOrWhiteSpace(guardianName))
                    lines.Add($"  Хранитель: [white]{Markup.Escape(guardianName)}[/]");
                var stateLabel = GetStr(project, "activeState", GetStr(project, "finalState", ""));
                if (!string.IsNullOrWhiteSpace(stateLabel))
                    lines.Add($"  Состояние: [dim]{Markup.Escape(FormatGuardianProjectStateLabel(stateLabel))}[/]");
                var progressPercent = GetInt(project, "progressPercent", -1);
                if (progressPercent >= 0)
                    lines.Add($"  Прогресс: [dim]{progressPercent}%[/]");
                return;
            }
        }
    }

    private async Task AppendExactGuardianNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(notification.GuardianId))
            return;

        using var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc?.RootElement.ValueKind != JsonValueKind.Object ||
            !guardiansDoc.RootElement.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            if (!string.Equals(GetStr(guardian, "guardianId", ""), notification.GuardianId, StringComparison.OrdinalIgnoreCase))
                continue;

            var guardianName = GuardianManifestation.GetDisplayName(guardian) ?? GetStr(guardian, "canonicalName", notification.GuardianName ?? notification.GuardianId);
            lines.Add("");
            lines.Add("  [bold]Связанный Хранитель:[/]");
            lines.Add($"  Имя: [white]{Markup.Escape(guardianName)}[/]");
            var domain = GetStr(guardian, "domain", "");
            if (!string.IsNullOrWhiteSpace(domain))
                lines.Add($"  Домен: [dim]{Markup.Escape(GuardianTradeDisplayDomain(domain))}[/]");

            if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianTradeInventoryReady, StringComparison.OrdinalIgnoreCase) &&
                guardian.TryGetProperty("tradeInventoryReceipts", out var receipts) &&
                receipts.ValueKind == JsonValueKind.Array)
            {
                foreach (var receipt in receipts.EnumerateArray())
                {
                    if (!string.Equals(GetStr(receipt, "requestId", ""), notification.RequestId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var tradeCycleId = GetStr(receipt, "tradeCycleId", "?");
                    var itemCount = GetInt(receipt, "itemCount", 0);
                    lines.Add($"  Торговый цикл: [dim]{Markup.Escape(tradeCycleId)}[/]");
                    lines.Add($"  Подготовлено слотов: [dim]{itemCount}[/]");
                    return;
                }
            }

            if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase) &&
                TryResolveGuardianQuestNotificationKey(notification, out _, out var questId) &&
                TryFindGuardianQuestById(guardian, questId, out var quest, out var questCollectionLabel))
            {
                var questTitle = GetStr(quest, "title", GetStr(quest, "name", questId));
                lines.Add("");
                lines.Add("  [bold]Точный квест Хранителя:[/]");
                lines.Add($"  Название: [white]{Markup.Escape(questTitle)}[/]");
                lines.Add($"  Раздел: [dim]{Markup.Escape(questCollectionLabel)}[/]");
                var questStatus = GetStr(quest, "status", "");
                if (!string.IsNullOrWhiteSpace(questStatus))
                    lines.Add($"  Статус: [dim]{Markup.Escape(HumanizeProtocolToken(questStatus))}[/]");
                var questDescription = GetStr(quest, "description", "");
                if (!string.IsNullOrWhiteSpace(questDescription))
                    lines.Add($"  Описание: [dim]{Markup.Escape(questDescription)}[/]");
                var targetWorld = GetStr(quest, "targetWorld", "");
                if (!string.IsNullOrWhiteSpace(targetWorld))
                    lines.Add($"  Целевой мир: [dim]{Markup.Escape(targetWorld)}[/]");
                lines.Add($"  Идентификатор квеста: [dim]{Markup.Escape(questId)}[/]");
                return;
            }

            return;
        }
    }

    private async Task AppendExactResidentNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(notification.ResidentId))
            return;

        var stableResidentLabel = string.IsNullOrWhiteSpace(notification.ResidentName)
            ? notification.ResidentId
            : $"{notification.ResidentName} ({notification.ResidentId})";

        lines.Add("");
        lines.Add("  [bold]Связанный резидент:[/]");
        lines.Add($"  Резидент: [white]{Markup.Escape(stableResidentLabel)}[/]");
        if (!string.IsNullOrWhiteSpace(notification.GuardianName))
            lines.Add($"  Хранитель: [white]{Markup.Escape(notification.GuardianName)}[/]");

        using var residentsDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
        if (residentsDoc?.RootElement.ValueKind != JsonValueKind.Object)
            return;

        var residentRoot = JsonNode.Parse(residentsDoc.RootElement.GetRawText()) as JsonObject;
        var resident = residentRoot == null
            ? null
            : GuardianAbodeResidentState.FindResident(residentRoot, notification.ResidentId);
        if (resident == null)
            return;

        var displayName = GetNodeString(resident["displayName"]) ??
                          GetNodeString(resident["residentName"]) ??
                          notification.ResidentName ??
                          notification.ResidentId;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(displayName, notification.ResidentName, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"  Текущее имя: [dim]{Markup.Escape(displayName)}[/]");
        }

        if (resident.TryGetPropertyValue("isPresent", out var isPresentNode) &&
            isPresentNode is JsonValue isPresentValue &&
            isPresentValue.TryGetValue<bool>(out var isPresent))
        {
            lines.Add($"  Присутствие: [dim]{(isPresent ? "сейчас в Обители" : "уже покинул Обитель")}[/]");
        }
    }

    private async Task AppendShiningNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (!IsShiningNotificationType(notification.NotificationType))
            return;

        var context = await LoadShiningContextAsync();
        if (context == null)
            return;

        switch (notification.NotificationType)
        {
            case AfterlifeNotificationState.TypeShiningTradeInventoryReady:
                AppendShiningTradeNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningCoreActionResolved:
                AppendShiningCoreNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionFoundingResolved:
                AppendShiningFoundingNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionRealignmentResolved:
                AppendShiningRealignmentNotificationDetails(context.Root, notification, lines);
                break;
            case AfterlifeNotificationState.TypeShiningFactionLeadershipResolved:
                AppendShiningLeadershipNotificationDetails(context.Root, notification, lines);
                break;
        }
    }

    private async Task AppendPlayerGuardianFoundationNotificationDetailLinesAsync(AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        if (!string.Equals(notification.NotificationType, AfterlifeNotificationState.TypePlayerGuardianFoundationResolved, StringComparison.OrdinalIgnoreCase))
            return;

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson) || JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot)
            return;

        var historyEntry = PlayerGuardianFoundationState.FindHistoryEntry(guardiansRoot, notification.RequestId);
        if (historyEntry == null)
            return;
        var foundedGuardian = PlayerGuardianFoundationState.FindGuardianById(
            guardiansRoot,
            GetNodeString(historyEntry["guardianId"]) ?? notification.GuardianId);

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add("  Вознесённая душа учредила собственного Хранителя.");
        lines.Add("  [bold]Что затронуто:[/]");
        lines.Add($"  Новый Хранитель: [white]{Markup.Escape(GetNodeString(historyEntry["guardianDisplayName"]) ?? notification.GuardianName ?? notification.GuardianId)}[/]");
        lines.Add($"  Прежний покровитель: [white]{Markup.Escape(GetNodeString(historyEntry["formerPatronGuardianName"]) ?? GetNodeString(historyEntry["formerPatronGuardianId"]) ?? "?")}[/]");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Источник: [dim]{Markup.Escape(DescribeFoundationSource(GetNodeString(historyEntry["foundationSource"]) ?? PlayerGuardianFoundationState.FoundationSourceShiningReturn))}[/]");
        var resolvedAtTurn = GetNodeInt(historyEntry["resolvedAtTurn"]);
        if (resolvedAtTurn > 0)
            lines.Add($"  Решено на ходу: [dim]{resolvedAtTurn}[/]");
        if (foundedGuardian != null)
        {
            var founderBonusCharges = PlayerGuardianFoundationState.GetFounderExtraGachaCharges(foundedGuardian);
            var founderFeatureTitle = PlayerGuardianFoundationState.GetFounderAbodeFeatureTitle(foundedGuardian);
            var founderFeatureSummary = PlayerGuardianFoundationState.GetFounderAbodeFeatureSummary(foundedGuardian);
            if (founderBonusCharges > 0)
                lines.Add($"  Бонус основания: [dim]+{founderBonusCharges} доп. попытка гачи за возвращение[/]");
            if (!string.IsNullOrWhiteSpace(founderFeatureTitle))
                lines.Add($"  Дар основания: [dim]{Markup.Escape(founderFeatureTitle)}[/]");
            if (!string.IsNullOrWhiteSpace(founderFeatureSummary))
                lines.Add($"  [dim]{Markup.Escape(founderFeatureSummary)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(GetNodeString(historyEntry["formerPatronGuardianName"])))
            lines.Add("  [dim]Прежний покровитель может получить дальнейшее продолжение от GM через разговоры, квесты и обычные загробные события.[/]");
        lines.Add("  [bold]Связанный экран:[/] [white]/guardians[/]");
    }

    private static void AppendShiningTradeNotificationDetails(JsonObject shiningRoot, AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        var match = ShiningAbodeState.EnsureFactionsArray(shiningRoot).OfType<JsonObject>()
            .SelectMany(faction =>
            {
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                return (faction["tradeInventoryReceipts"] as JsonArray)?.OfType<JsonObject>()
                    .Select(receipt => (FactionName: factionName, Faction: faction, Receipt: receipt))
                    ?? Enumerable.Empty<(string FactionName, JsonObject Faction, JsonObject Receipt)>();
            })
            .FirstOrDefault(item => string.Equals(GetNodeString(item.Receipt["requestId"]), notification.RequestId, StringComparison.OrdinalIgnoreCase));

        if (match.Receipt == null)
            return;

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add("  Торговая витрина сияющей фракции готова.");
        lines.Add("  [bold]Что затронуто:[/]");
        var stableFactionName = GetNodeString(match.Receipt["factionName"]) ?? GetNodeString(match.Receipt["factionId"]) ?? match.FactionName;
        lines.Add($"  Фракция: [white]{Markup.Escape(stableFactionName)}[/]");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Цикл: [dim]{Markup.Escape(GetNodeString(match.Receipt["tradeCycleId"]) ?? "?")}[/]");
        lines.Add($"  Слотов в витрине: [dim]{GetNodeInt(match.Receipt["itemCount"])}[/]");
        if (TryReadIntegerNode(match.Receipt["soldOutCount"], out var soldOutCount) && soldOutCount > 0)
        {
            var itemCount = Math.Max(GetNodeInt(match.Receipt["itemCount"]), soldOutCount);
            lines.Add($"  Распродано: [dim]{soldOutCount}/{itemCount}[/]");
        }
        lines.Add("  [bold]Связанный экран:[/] [white]/shining_abode[/]");
    }

    private static void AppendShiningCoreNotificationDetails(JsonObject shiningRoot, AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        var receipt = ShiningAbodeState.EnsureCoreActionReceiptsArray(shiningRoot).OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["requestId"]), notification.RequestId, StringComparison.OrdinalIgnoreCase));
        if (receipt == null)
            return;

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add($"  {Markup.Escape(DescribeShiningCoreActionLabel(GetNodeString(receipt["actionType"])))}.");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(receipt["status"])))}[/]");
        lines.Add($"  Итог: [dim]{Markup.Escape(BuildShiningCoreReceiptSummary(receipt, shiningRoot))}[/]");

        var resolvedAtTurn = GetNodeInt(receipt["resolvedAtTurn"]);
        if (resolvedAtTurn > 0)
            lines.Add($"  Решено на ходу: [dim]{resolvedAtTurn}[/]");
        lines.Add("  [bold]Связанный экран:[/] [white]/shining_abode[/]");
    }

    private static void AppendShiningFoundingNotificationDetails(JsonObject shiningRoot, AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        var receipt = ShiningAbodeState.EnsureFactionFoundingReceiptsArray(shiningRoot).OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["requestId"]), notification.RequestId, StringComparison.OrdinalIgnoreCase));
        if (receipt == null)
            return;

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add("  Основание новой сияющей фракции.");
        lines.Add("  [bold]Что затронуто:[/]");
        lines.Add($"  Зал: [white]{Markup.Escape(GetNodeString(receipt["hallName"]) ?? GetNodeString(receipt["hallId"]) ?? "?")}[/]");
        var stableFactionName = GetNodeString(receipt["factionName"]) ??
                                GetNodeString(receipt["proposedFactionName"]) ??
                                GetNodeString(receipt["factionId"]) ??
                                GetNodeString(receipt["proposedFactionId"]) ??
                                "?";
        lines.Add($"  Фракция: [white]{Markup.Escape(stableFactionName)}[/]");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Сторонников: [dim]{(receipt["supportingResidentIds"] as JsonArray)?.Count ?? 0}[/]");
        lines.Add($"  Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(receipt["status"])))}[/]");
        lines.Add("  [bold]Связанный экран:[/] [white]/shining_politics[/]");
    }

    private static void AppendShiningRealignmentNotificationDetails(JsonObject shiningRoot, AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        var receipt = ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(shiningRoot).OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["requestId"]), notification.RequestId, StringComparison.OrdinalIgnoreCase));
        if (receipt == null)
            return;

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add("  Политическая перестройка резидента.");
        lines.Add("  [bold]Что затронуто:[/]");
        lines.Add($"  Резидент: [white]{Markup.Escape(GetNodeString(receipt["residentName"]) ?? GetNodeString(receipt["residentId"]) ?? "?")}[/]");
        var sourceFaction = string.IsNullOrWhiteSpace(GetNodeString(receipt["sourceFactionName"]))
            ? GetNodeString(receipt["sourceFactionId"]) ?? "?"
            : GetNodeString(receipt["sourceFactionName"])!;
        var targetFaction = string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionName"]))
            ? (string.IsNullOrWhiteSpace(GetNodeString(receipt["targetFactionId"]))
                ? "нейтраль"
                : GetNodeString(receipt["targetFactionId"]) ?? "нейтраль")
            : GetNodeString(receipt["targetFactionName"])!;
        lines.Add($"  Переход: [dim]{Markup.Escape(sourceFaction)} -> {Markup.Escape(targetFaction)}[/]");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Режим: [dim]{Markup.Escape(DescribeShiningRealignmentMode(GetNodeString(receipt["realignmentMode"])))}[/]");
        lines.Add($"  Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(receipt["status"])))}[/]");
        lines.Add("  [bold]Связанный экран:[/] [white]/shining_politics[/]");
    }

    private static void AppendShiningLeadershipNotificationDetails(JsonObject shiningRoot, AfterlifeNotificationState.NotificationEntry notification, List<string> lines)
    {
        var match = ShiningAbodeState.EnsureFactionsArray(shiningRoot).OfType<JsonObject>()
            .SelectMany(faction =>
            {
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? "?";
                return (faction["leadershipReceipts"] as JsonArray)?.OfType<JsonObject>()
                    .Select(receipt => (FactionName: factionName, Receipt: receipt))
                    ?? Enumerable.Empty<(string FactionName, JsonObject Receipt)>();
            })
            .FirstOrDefault(item => string.Equals(GetNodeString(item.Receipt["requestId"]), notification.RequestId, StringComparison.OrdinalIgnoreCase));

        if (match.Receipt == null)
            return;

        lines.Add("");
        lines.Add("  [bold]Что произошло:[/]");
        lines.Add("  Смена главы сияющей фракции.");
        lines.Add("  [bold]Что затронуто:[/]");
        var stableFactionName = string.IsNullOrWhiteSpace(GetNodeString(match.Receipt["factionName"])) ? match.FactionName : GetNodeString(match.Receipt["factionName"])!;
        lines.Add($"  Фракция: [white]{Markup.Escape(stableFactionName)}[/]");
        lines.Add("  [bold]Результат:[/]");
        lines.Add($"  Переход: [dim]{Markup.Escape(DescribeShiningLeadershipMode(GetNodeString(match.Receipt["transitionMode"])))}[/]");
        lines.Add($"  Новый глава: [white]{Markup.Escape(string.IsNullOrWhiteSpace(GetNodeString(match.Receipt["newHeadLabel"])) ? BuildHeadActorLabel(GetNodeString(match.Receipt["newHeadActorType"]), GetNodeString(match.Receipt["newHeadActorId"])) : GetNodeString(match.Receipt["newHeadLabel"])!)}[/]");
        lines.Add($"  Статус: [white]{Markup.Escape(DescribeShiningResolutionStatus(GetNodeString(match.Receipt["status"])))}[/]");
        lines.Add("  [bold]Связанный экран:[/] [white]/shining_politics[/]");
    }

    private static bool IsShiningNotificationType(string? notificationType) =>
        string.Equals(notificationType, AfterlifeNotificationState.TypeShiningTradeInventoryReady, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(notificationType, AfterlifeNotificationState.TypeShiningCoreActionResolved, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(notificationType, AfterlifeNotificationState.TypeShiningFactionFoundingResolved, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(notificationType, AfterlifeNotificationState.TypeShiningFactionRealignmentResolved, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(notificationType, AfterlifeNotificationState.TypeShiningFactionLeadershipResolved, StringComparison.OrdinalIgnoreCase);

    private async Task ShowAfterlifeArchiveCandidates()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Кандидаты в Архив"))
            return;

        if (_afterlifeArchiveCandidateService == null)
        {
            ShowEmptyPanel("Кандидаты в Архив", "Сервис кандидатов в Архив души недоступен.");
            return;
        }

        var realm = _stateManager.CurrentState.CurrentRealm ?? "Chaos Sea";
        var isAfterlifeRealm = RealmSemantics.IsAfterlifeRealm(realm);

        if (!isAfterlifeRealm)
        {
            MarkupLine("[yellow]⚠️ Кандидаты в Архив доступны только в загробном цикле.[/]");
            return;
        }

        await _afterlifeArchiveCandidateService.RefreshFromCurrentStateAsync();

        while (true)
        {
            var manifest = await _afterlifeArchiveCandidateService.ReadAsync();
            if (manifest == null || manifest.Candidates.Count == 0)
            {
                ShowEmptyPanel("Кандидаты в Архив", "Для последней завершённой жизни пока нет структурированных кандидатов из Кодекса.");
                return;
            }

            var candidates = manifest.Candidates
                .OrderBy(candidate => GetArchiveCandidateStatusOrder(candidate.Status))
                .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
                .Select(candidate => new AfterlifeArchiveCandidateSummary(
                    candidate.CandidateId,
                    candidate.SourceKind,
                    candidate.SourceEntryId,
                    candidate.SourceLife,
                    candidate.ProposedEntryType,
                    candidate.Title,
                    candidate.Summary,
                    candidate.Content,
                    candidate.Rarity,
                    candidate.Status,
                    candidate.DiscoveredAt ?? "",
                    candidate.Tags))
                .ToList();

            var archivedCount = manifest.Candidates.Count(item =>
                string.Equals(item.Status, AfterlifeArchiveCandidateService.StatusArchived, StringComparison.OrdinalIgnoreCase));
            var archivedSecretCount = manifest.Candidates.Count(item =>
                string.Equals(item.Status, AfterlifeArchiveCandidateService.StatusArchived, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.ProposedEntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase));

            var choices = MakeUniqueChoiceLabels(candidates.Select(candidate =>
            {
                var (statusLabel, statusColor) = GetArchiveCandidateStatusLabel(candidate.Status);
                var rarityColor = GetRarityColor(candidate.Rarity);
                var typeLabel = AfterlifeArchiveState.GetEntryTypeLabel(candidate.ProposedEntryType);
                return ($"{GetArchiveCandidateStatusIcon(candidate.Status)} {Markup.Escape(candidate.Title)} [dim]({Markup.Escape(typeLabel)})[/] [{rarityColor}]{Markup.Escape(candidate.Rarity)}[/] [{statusColor}]{statusLabel}[/]", candidate.CandidateId);
            }).ToList());
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold yellow]🗂 Кандидаты в Архив[/] [dim](жизнь #{manifest.SourceLife}; сохранено {archivedCount}/{AfterlifeArchiveCandidateService.MaxArchivedPerLife}, тайн {archivedSecretCount}/{AfterlifeArchiveCandidateService.MaxSecretArchivedPerLife})[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= candidates.Count)
                return;

            var candidate = candidates[index];
            var (statusLabel, _) = GetArchiveCandidateStatusLabel(candidate.Status);
            var lines = new List<string>
            {
                $"[bold yellow]🗂 {Markup.Escape(candidate.Title)}[/]",
                "",
                $"  Тип архива: [cyan]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(candidate.ProposedEntryType))}[/]",
                $"  Редкость: [{GetRarityColor(candidate.Rarity)}]{Markup.Escape(DescribeRarityLabel(candidate.Rarity))}[/]",
                $"  Статус: [white]{Markup.Escape(statusLabel)}[/]",
                $"  Жизнь-источник: [yellow]{candidate.SourceLife}[/]",
                $"  Источник записи: [dim]{Markup.Escape(AfterlifeArchiveState.GetSourceKindLabel(candidate.SourceKind))}[/]"
            };

            if (!string.IsNullOrWhiteSpace(candidate.DiscoveredAt))
                lines.Add($"  Обнаружено: [dim]{Markup.Escape(candidate.DiscoveredAt)}[/]");
            if (!string.IsNullOrWhiteSpace(candidate.SourceEntryId))
                lines.Add($"  Исходная запись Кодекса: [dim]{Markup.Escape(candidate.SourceEntryId)}[/]");
            if (candidate.Tags.Count > 0)
                lines.Add($"  Метки: [dim]{Markup.Escape(string.Join(", ", candidate.Tags))}[/]");

            lines.Add("");
            var candidateBody = string.IsNullOrWhiteSpace(candidate.Content) ? candidate.Summary : candidate.Content;
            lines.Add($"[white]{Markup.Escape(candidateBody)}[/]");

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🗂 Кандидат в Архив ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var candidateActions = new List<string>();
            if (!string.IsNullOrWhiteSpace(candidate.SourceEntryId))
                candidateActions.Add("📖 Открыть исходную запись Кодекса");
            if (string.Equals(candidate.Status, AfterlifeArchiveCandidateService.StatusPending, StringComparison.OrdinalIgnoreCase))
            {
                candidateActions.Add("💾 Сохранить в Архив");
                candidateActions.Add("⏭ Пропустить");
            }
            candidateActions.Add("← Назад");

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .PageSize(6)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(candidateActions));

            if (action == "← Назад")
                continue;

            if (action.StartsWith("📖", StringComparison.Ordinal))
            {
                await TryShowCodexEntryByIdAsync(candidate.SourceEntryId);
                continue;
            }

            if (action.StartsWith("💾", StringComparison.Ordinal))
            {
                if (await _afterlifeArchiveCandidateService.ArchiveCandidateAsync(candidate.CandidateId))
                {
                    await _stateManager.RefreshGameStateAsync();
                    MarkupLine($"[green]✅ Запись «{Markup.Escape(candidate.Title)}» сохранена в Архив души.[/]");
                }
                else
                {
                    MarkupLine("[yellow]⚠️ Не удалось сохранить запись. Возможно, исчерпан лимит или запись уже была обработана.[/]");
                }

                WaitForKey();
                continue;
            }

            if (await _afterlifeArchiveCandidateService.SkipCandidateAsync(candidate.CandidateId))
            {
                MarkupLine($"[yellow]Пропущено:[/] {Markup.Escape(candidate.Title)}");
            }
            else
            {
                MarkupLine("[yellow]⚠️ Не удалось обновить статус кандидата.[/]");
            }

            WaitForKey();
        }
    }

    private async Task<bool> TryShowCodexEntryByIdAsync(string? sourceEntryId)
    {
        if (string.IsNullOrWhiteSpace(sourceEntryId))
            return false;

        var codexDoc = await _stateManager.LoadGameStateFileAsync("lore/codex_entries.json");
        if (codexDoc == null)
        {
            ShowEmptyPanel("📚 Кодекс", "Записи кодекса недоступны.");
            WaitForKey();
            return false;
        }

        var entry = CollectCodexEntries(codexDoc.RootElement)
            .FirstOrDefault(item => string.Equals(GetStr(item, "entryId", ""), sourceEntryId, StringComparison.OrdinalIgnoreCase));
        if (entry.ValueKind != JsonValueKind.Object)
        {
            MarkupLine("[yellow]⚠️ Исходная запись Кодекса не найдена в текущем codex_entries.json.[/]");
            WaitForKey();
            return false;
        }

        var title = GetStr(entry, "title", sourceEntryId);
        var content = GetStr(entry, "content", "");
        var category = GetStr(entry, "category", "other");
        var subcategory = GetStr(entry, "subcategory", "");
        var discoveredAt = GetStr(entry, "discoveredAt", "");
        var context = GetStr(entry, "discoveryContext", "");
        var sourceFile = GetStr(entry, "sourceFile", "");

        var lines = new List<string>
        {
            $"[bold purple]📚 {Markup.Escape(title)}[/]",
            "",
            $"  Идентификатор записи: [dim]{Markup.Escape(sourceEntryId)}[/]"
        };
        if (!string.IsNullOrWhiteSpace(category))
            lines.Add($"  Категория: [white]{Markup.Escape(DescribeCodexCategoryLabel(category))}[/]");
        if (!string.IsNullOrWhiteSpace(subcategory))
            lines.Add($"  Подкатегория: [dim]{Markup.Escape(DescribeCodexSubcategoryLabel(subcategory))}[/]");
        if (!string.IsNullOrWhiteSpace(discoveredAt))
            lines.Add($"  Обнаружено: [dim]{Markup.Escape(discoveredAt)}[/]");
        if (!string.IsNullOrWhiteSpace(context))
            lines.Add($"  Контекст: [dim]{Markup.Escape(context)}[/]");
        if (!string.IsNullOrWhiteSpace(sourceFile))
            lines.Add($"  Источник файла: [dim]{Markup.Escape(sourceFile)}[/]");
        if (!string.IsNullOrWhiteSpace(content))
        {
            lines.Add("");
            lines.Add($"[white]{Markup.Escape(content)}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📚 Исходная запись Кодекса ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
        return true;
    }

    private static int GetArchiveCandidateStatusOrder(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveCandidateService.StatusPending => 0,
            AfterlifeArchiveCandidateService.StatusArchived => 1,
            AfterlifeArchiveCandidateService.StatusSkipped => 2,
            _ => 9
        };

    private static string GetArchiveCandidateStatusIcon(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveCandidateService.StatusArchived => "✅",
            AfterlifeArchiveCandidateService.StatusSkipped => "⏭",
            _ => "🗂"
        };

    private static (string label, string color) GetArchiveCandidateStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            AfterlifeArchiveCandidateService.StatusArchived => ("сохранено", "green"),
            AfterlifeArchiveCandidateService.StatusSkipped => ("пропущено", "grey"),
            _ => ("ожидает решения", "yellow")
        };

    private static string DescribeCodexCategoryLabel(string? category) =>
        (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "cosmology" => "Космология",
            "geography" => "География",
            "history" => "История",
            "cultures" => "Культуры",
            "creatures" => "Существа",
            "characters" => "Персонажи",
            "artifacts" => "Артефакты",
            "factions" => "Фракции",
            "magic" => "Магия",
            "other" => "Прочее",
            _ => HumanizeProtocolToken(category)
        };

    private static string DescribeCodexSubcategoryLabel(string? subcategory) =>
        (subcategory ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "system_guardian" => "системный хранитель",
            "domain_truth" => "истина домена",
            "domain_secret" => "тайна домена",
            "abode_truth" => "истина обители",
            "personal_history" => "личная история",
            "cosmic_secret" => "космическая тайна",
            "world_lore" => "мировое знание",
            "other_guardians" => "другие хранители",
            "soul_mechanics" => "механика души",
            "domain_mastery" => "власть над доменом",
            "lost_world" => "потерянный мир",
            _ => HumanizeProtocolToken(subcategory)
        };

    private static string DescribeSoulRelicCategoryLabel(string? category) =>
        (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "companion_echo" => "эхо спутника",
            "memory_legacy" => "наследие памяти",
            "guardian_mantle" => "мантия хранителя",
            "route_fragment" => "осколок пути",
            "archive_resonance" => "архивный отзвук",
            _ => HumanizeProtocolToken(category)
        };

    private static string HumanizeProtocolToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var normalizedKey = trimmed.Replace("-", "_").Trim('_');
        var knownLabel = normalizedKey.ToLowerInvariant() switch
        {
            "echosignature" or "echo_signature" => "Сигнатура эха",
            "resonanceboost" or "resonance_boost" => "Усиление резонанса",
            "sourcecompanionrelicid" or "source_companion_relic_id" => "Реликвия-источник спутника",
            "meetingtag" or "meeting_tag" => "Тема встречи",
            "routeseedid" or "route_seed_id" => "Семя маршрута",
            "remaininguses" or "remaining_uses" => "Осталось использований",
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(knownLabel))
            return knownLabel;

        var words = Regex.Replace(trimmed.Replace('_', ' ').Replace('-', ' '), "([a-zа-яё0-9])([A-ZА-ЯЁ])", "$1 $2");
        words = Regex.Replace(words, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(words)
            ? string.Empty
            : char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
    }

    /// <summary>
    /// Displays detailed information about a soul relic.
    /// In Chaos Sea: offers equip/unequip actions that modify soul_state.json directly.
    /// Returns true if state was modified (needs refresh).
    /// </summary>
    private async Task<bool> ShowRelicDetailPanel(string relicId, string name, string status, JsonElement relic, bool isAfterlifeRealm)
    {
        var residentDoc = await _stateManager.LoadGameStateFileAsync(GuardianAbodeResidentState.StatePath);
        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var lines = BuildSoulRelicDetailLines(name, relic, status, residentDoc, guardiansDoc);
        await EnrichManifestedCompanionDetailsAsync(lines, relic);
        var slot = ResolveRelicSlot(relic);

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 💎 Реликвия души ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });

        // Action menu
        if (isAfterlifeRealm)
        {
            var sourceResidentId = relic.TryGetProperty("companionSeed", out var companionSeedNode) && companionSeedNode.ValueKind == JsonValueKind.Object
                ? GetStr(companionSeedNode, "sourceResidentId", "")
                : "";
            var sourceGuardianId = relic.TryGetProperty("companionSeed", out companionSeedNode) && companionSeedNode.ValueKind == JsonValueKind.Object
                ? GetStr(companionSeedNode, "sourceGuardianId", "")
                : "";
            var actions = new List<string>();
            if (status == "stored")
                actions.Add("⚔ Экипировать");
            else
                actions.Add("📦 Снять (в хранилище)");
            if (!string.IsNullOrWhiteSpace(sourceResidentId))
                actions.Add("🏛 Открыть резидента-источник");
            if (!string.IsNullOrWhiteSpace(sourceGuardianId))
                actions.Add("🛡 Открыть Хранителя-источник");
            actions.Add("← Назад к списку");

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(actions));

            if (action.Contains("Экипировать"))
            {
                await EquipSoulRelicLocal(relicId, name, slot);
                return true;
            }
            if (action.Contains("Снять"))
            {
                await UnequipSoulRelicLocal(relicId, name);
                return true;
            }
            if (action.StartsWith("🏛", StringComparison.Ordinal))
            {
                await ShowGuardianAbodeResidentDetailByIdAsync(sourceResidentId);
                return false;
            }
            if (action.StartsWith("🛡", StringComparison.Ordinal))
            {
                if (await TryShowLinkedArchiveGuardianAsync(sourceGuardianId))
                    return false;
            }
        }
        else
        {
            MarkupLine("[yellow dim]  ⚠ Управление реликвиями доступно только в загробном цикле.[/]");
            WaitForKey();
        }

        return false;
    }

    private async Task EnrichManifestedCompanionDetailsAsync(List<string> lines, JsonElement relic)
    {
        var manifestationStatus = GetStr(relic, "companionManifestationStatus", "");
        if (!string.Equals(manifestationStatus, "materialized", StringComparison.OrdinalIgnoreCase))
            return;

        var resolvedNpcId = GetStr(relic, "companionManifestationResolvedNpcId", "");
        if (string.IsNullOrWhiteSpace(resolvedNpcId))
            return;

        var resolvedDisplayName = await ResolveNpcDisplayNameAsync(resolvedNpcId);
        if (string.IsNullOrWhiteSpace(resolvedDisplayName) ||
            string.Equals(resolvedDisplayName, resolvedNpcId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var existingLineIndex = lines.FindIndex(line => line.Contains("👣 Проявившийся спутник:", StringComparison.Ordinal));
        var displayNameLine = $"  👣 Проявившийся спутник: [white]{Markup.Escape(resolvedDisplayName)}[/]";
        var idLine = $"    [dim]ID: {Markup.Escape(resolvedNpcId)}[/]";

        if (existingLineIndex >= 0)
        {
            lines[existingLineIndex] = displayNameLine;
            lines.Insert(existingLineIndex + 1, idLine);
            return;
        }

        lines.Add(displayNameLine);
        lines.Add(idLine);
    }

    private async Task<string> ResolveNpcDisplayNameAsync(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return string.Empty;

        var npcDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (npcDoc == null)
            return string.Empty;

        foreach (var npc in EnumerateNpcObjects(npcDoc.RootElement))
        {
            var currentNpcId = GetStr(npc, "NPCId", GetStr(npc, "npcId", GetStr(npc, "id", "")));
            if (!string.Equals(currentNpcId, npcId, StringComparison.OrdinalIgnoreCase))
                continue;

            return GetStr(npc, "displayName",
                GetStr(npc, "NPCName",
                    GetStr(npc, "npcName",
                        GetStr(npc, "name", npcId))));
        }

        return string.Empty;
    }

    private static IEnumerable<JsonElement> EnumerateNpcObjects(JsonElement root) =>
        GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root);

    private List<string> BuildSoulRelicDetailLines(string name, JsonElement relic, string? status, JsonDocument? residentDoc, JsonDocument? guardiansDoc)
    {
        var lines = new List<string>
        {
            $"[bold yellow]💎 {Markup.Escape(name)}[/]",
            ""
        };

        var desc = GetStr(relic, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add($"[white]{Markup.Escape(desc)}[/]");
            lines.Add("");
        }

        var slot = ResolveRelicSlot(relic);
        if (!string.IsNullOrEmpty(slot))
            lines.Add($"  📌 Слот: [cyan]{Markup.Escape(FormatSoulRelicSlotLabel(slot))}[/]");

        var rarity = GetStr(relic, "quality", GetStr(relic, "rarity", ""));
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(DescribeRarityLabel(rarity))}[/]");

        var category = GetStr(relic, "category", "");
        if (!string.IsNullOrEmpty(category))
            lines.Add($"  📋 Категория: [cyan]{Markup.Escape(DescribeSoulRelicCategoryLabel(category))}[/]");

        var tier = GetStr(relic, "tier", "");
        if (!string.IsNullOrEmpty(tier))
            lines.Add($"  🏆 Ранг: [yellow]{Markup.Escape(tier)}[/]");

        var formTag = GetStr(relic, "formTag", "");
        if (!string.IsNullOrWhiteSpace(formTag))
            lines.Add($"  🛠 Форма ковки: [cyan]{Markup.Escape(DescribeForgeFormTag(formTag))}[/]");

        if (relic.TryGetProperty("properties", out var forgeProperties) &&
            forgeProperties.ValueKind == JsonValueKind.Array &&
            forgeProperties.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🛠 Свойства ковки:[/]");
            var propertyIndex = 0;
            foreach (var property in forgeProperties.EnumerateArray())
            {
                if (property.ValueKind != JsonValueKind.Object)
                {
                    propertyIndex++;
                    continue;
                }

                var propertyObject = JsonNode.Parse(property.GetRawText()) as JsonObject;
                if (propertyObject == null)
                {
                    propertyIndex++;
                    continue;
                }

                lines.Add($"    • {Markup.Escape(RenderForgePropertyLabel(propertyObject, propertyIndex))}");
                propertyIndex++;
            }
        }

        if (relic.TryGetProperty("equipmentData", out var eqd) && eqd.ValueKind == JsonValueKind.Object)
        {
            var req = GetStr(eqd, "enlightenmentRequirement", "");
            if (!string.IsNullOrEmpty(req) && req != "0")
                lines.Add($"  🔒 Требование просветления: [yellow]{Markup.Escape(req)}[/]");
        }

        if (relic.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Эффекты:[/]");
            if (effects.TryGetProperty("characteristicBonuses", out var charBon) && charBon.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in charBon.EnumerateObject())
                {
                    var charName = Characteristics.RussianNames.GetValueOrDefault(prop.Name, prop.Name);
                    lines.Add($"    • [green]{Markup.Escape(charName)} +{prop.Value}[/]");
                }
            }
            if (effects.TryGetProperty("actionCheckBonuses", out var actBon) && actBon.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in actBon.EnumerateObject())
                {
                    var actionLabel = DescribeSoulRelicActionCheckLabel(prop.Name);
                    if (string.IsNullOrWhiteSpace(actionLabel))
                        lines.Add($"    • [cyan]Бонус к особой проверке действия:[/] +{prop.Value}");
                    else
                        lines.Add($"    • [cyan]{Markup.Escape(actionLabel)}: +{prop.Value}[/]");
                }
            }

            var knownEffectProps = new HashSet<string> { "characteristicBonuses", "actionCheckBonuses" };
            var technicalEffectProps = new List<string>();
            foreach (var prop in effects.EnumerateObject())
            {
                if (knownEffectProps.Contains(prop.Name)) continue;
                var label = HumanizeProtocolToken(prop.Name);
                var value = DescribeQuestStructuredValue(prop.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    technicalEffectProps.Add($"      • {Markup.Escape(label)}: {Markup.Escape(value)}");
            }

            if (technicalEffectProps.Count > 0)
            {
                lines.Add("    • [dim]У реликвии есть дополнительные свойства, которые усиливают её необычный эффект.[/]");
                lines.Add("    [dim]Дополнительные свойства эффекта:[/]");
                lines.AddRange(technicalEffectProps);
            }
        }

        if (relic.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Бонусы:[/]");
            foreach (var b in bonuses.EnumerateArray())
            {
                if (b.ValueKind == JsonValueKind.String)
                    lines.Add($"    • [green]{Markup.Escape(b.GetString() ?? "")}[/]");
                else if (b.ValueKind == JsonValueKind.Object)
                {
                    var bName = GetStr(b, "name", HumanizeProtocolToken(GetStr(b, "stat", "")));
                    var bVal = DescribeQuestStructuredValue(b);
                    if (!string.IsNullOrEmpty(bName))
                        lines.Add($"    • [green]{Markup.Escape(bName)}: {Markup.Escape(bVal)}[/]");
                }
            }
        }

        if (relic.TryGetProperty("passiveEffects", out var passives) && passives.ValueKind == JsonValueKind.Array)
        {
            lines.Add("");
            lines.Add("  [bold]🔮 Пассивные эффекты:[/]");
            foreach (var e in passives.EnumerateArray())
            {
                var eName = e.ValueKind == JsonValueKind.String ? e.GetString() : GetStr(e, "name", GetStr(e, "effect", ""));
                if (!string.IsNullOrEmpty(eName))
                    lines.Add($"    • [mediumpurple2]{Markup.Escape(eName!)}[/]");
            }
        }

        if (relic.TryGetProperty("acquisitionData", out var acq) && acq.ValueKind == JsonValueKind.Object)
        {
            var srcGuardian = GetStr(acq, "sourceGuardian", "");
            if (!string.IsNullOrEmpty(srcGuardian))
            {
                lines.Add("");
                lines.Add($"  🛡️ Источник: [cyan]{Markup.Escape(srcGuardian)}[/]");
            }
            var story = GetStr(acq, "acquisitionStory", "");
            if (!string.IsNullOrEmpty(story))
            {
                if (string.IsNullOrEmpty(srcGuardian)) lines.Add("");
                lines.Add($"  [dim italic]📜 {Markup.Escape(story)}[/]");
            }
        }

        var narrativeOrigin = GetStr(relic, "narrativeOrigin", "");
        if (!string.IsNullOrEmpty(narrativeOrigin))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📜 {Markup.Escape(narrativeOrigin)}[/]");
        }

        var companionNameHint = ResolveRelicCompanionNameHint(relic);
        var manifestationStatus = GetStr(relic, "companionManifestationStatus", "");
        var manifestationSourceLabel = ResolveRelicManifestationSourceLabel(relic);
        if (!string.IsNullOrEmpty(companionNameHint) || !string.IsNullOrEmpty(manifestationStatus))
        {
            lines.Add("");
            if (!string.IsNullOrEmpty(companionNameHint))
                lines.Add($"  👤 Нить спутника: [white]{Markup.Escape(companionNameHint)}[/]");
            if (!string.IsNullOrEmpty(manifestationSourceLabel))
                lines.Add($"  🧭 Источник воплощения: [dim]{Markup.Escape(manifestationSourceLabel)}[/]");

            var resolvedNpcId = GetStr(relic, "companionManifestationResolvedNpcId", "");
            var resolvedTurn = GetInt(relic, "companionManifestationResolvedAtTurn", 0);
            switch (manifestationStatus.Trim().ToLowerInvariant())
            {
                case "pending":
                    lines.Add("  🕯️ Путь воплощения: [yellow]ожидает проявления в смертной жизни[/]");
                    break;
                case "materialized":
                    var materializedLine = "  🕯️ Путь воплощения: [green]спутник уже проявился в смертной жизни[/]";
                    if (resolvedTurn > 0)
                        materializedLine += $" [dim](ход {resolvedTurn})[/]";
                    lines.Add(materializedLine);
                    if (!string.IsNullOrEmpty(resolvedNpcId))
                        lines.Add($"  👣 Проявившийся спутник: [dim]{Markup.Escape(resolvedNpcId)}[/]");
                    break;
            }

            if (relic.TryGetProperty("companionSeed", out var companionSeed) &&
                companionSeed.ValueKind == JsonValueKind.Object)
            {
                foreach (var snapshotLine in BuildCompanionSeedSnapshotLines(companionSeed, residentDoc?.RootElement, guardiansDoc?.RootElement))
                    lines.Add(snapshotLine);
            }
        }

        if (!string.IsNullOrEmpty(status))
        {
            lines.Add("");
            lines.Add($"  Статус: {(status == "equipped" ? "[green]⚔ Экипировано[/]" : "[dim]📦 В хранилище[/]")}");
        }

        return lines;
    }

    private static string ResolveRelicSlot(JsonElement relic)
    {
        var slot = GetStr(relic, "slot", "");
        if (string.IsNullOrEmpty(slot) && relic.TryGetProperty("equipmentData", out var eqData))
            slot = GetStr(eqData, "equipSlot", "");
        if (string.IsNullOrEmpty(slot) && relic.TryGetProperty("gameplayStatus", out var gpStat))
            slot = GetStr(gpStat, "currentSlot", "");
        return slot;
    }

    private static string FormatSoulRelicSlotLabel(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return string.Empty;

        if (string.Equals(slot, "Default", StringComparison.OrdinalIgnoreCase))
            return "По умолчанию";

        return SlotLabels.TryGetValue(slot, out var label)
            ? label
            : slot;
    }

    private static string? DescribeSoulRelicActionCheckLabel(string? key) =>
        (key ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "social" => "Бонус к социальной проверке",
            "lore" => "Бонус к проверке знания",
            "memory" => "Бонус к проверке памяти",
            "route" => "Бонус к проверке пути",
            "descent" => "Бонус к проверке нисхождения",
            "survival" => "Бонус к проверке выживания",
            "resource" => "Бонус к ресурсной проверке",
            "relic" => "Бонус к реликтовой проверке",
            "archive" => "Бонус к архивной проверке",
            "talk" => "Бонус к разговорной проверке",
            "history" => "Бонус к исторической проверке",
            "quest" => "Бонус к квестовой проверке",
            "reward" => "Бонус к проверке награды",
            _ => null
        };

    private static string ResolveArchiveGuardianLabel(AfterlifeArchiveEntrySummary entry, JsonElement? guardiansRoot)
    {
        if (!string.IsNullOrWhiteSpace(entry.SourceGuardianName))
            return entry.SourceGuardianName;
        if (guardiansRoot is { ValueKind: JsonValueKind.Object } root &&
            root.TryGetProperty("guardians", out var guardians) &&
            guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
            {
                if (guardian.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetStr(guardian, "guardianId", ""), entry.SourceGuardianId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = GuardianManifestation.GetDisplayName(guardian);
                if (string.IsNullOrWhiteSpace(name))
                    name = GetStr(guardian, "guardianName", GetStr(guardian, "name", GetStr(guardian, "canonicalName", entry.SourceGuardianId)));

                return name;
            }
        }

        return entry.SourceGuardianId;
    }

    private static string ResolveArchiveProjectLabel(AfterlifeArchiveEntrySummary entry, JsonElement? trackerRoot)
    {
        if (!string.IsNullOrWhiteSpace(entry.ReservedForProjectName))
            return entry.ReservedForProjectName;
        if (trackerRoot is { ValueKind: JsonValueKind.Object } root)
        {
            foreach (var collectionName in new[] { "activeProjects", "completedProjects" })
            {
                if (!root.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in collection.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("project", out var project) ||
                        project.ValueKind != JsonValueKind.Object ||
                        !string.Equals(GetStr(project, "projectId", ""), entry.ReservedForProjectId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var projectName = GetStr(project, "projectName", GetStr(project, "name", entry.ReservedForProjectId));
                    return projectName;
                }
            }
        }

        return entry.ReservedForProjectId;
    }

    private async Task<bool> ShowAfterlifeArchiveEntryDetailByIdAsync(string archiveId)
    {
        if (string.IsNullOrWhiteSpace(archiveId))
            return false;

        var entry = (await ReadStoredAfterlifeArchiveEntriesAsync())
            .FirstOrDefault(item => string.Equals(item.ArchiveId, archiveId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        var sourceGuardianLabel = ResolveArchiveGuardianLabel(entry, guardiansDoc?.RootElement);
        var targetProjectLabel = ResolveArchiveProjectLabel(entry, trackerDoc?.RootElement);
        var lines = new List<string>
        {
            $"[bold yellow]📚 {Markup.Escape(entry.Title)}[/]",
            "",
            $"  Тип: [cyan]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType))}[/]",
            $"  Редкость: [{GetRarityColor(entry.Rarity)}]{Markup.Escape(DescribeRarityLabel(entry.Rarity))}[/]",
            $"  Источник жизни: [yellow]{entry.SourceLife}[/]",
            $"  Источник записи: [dim]{Markup.Escape(AfterlifeArchiveState.GetSourceKindLabel(entry.SourceKind))}[/]"
        };

        if (!string.IsNullOrWhiteSpace(sourceGuardianLabel))
            lines.Add($"  Связанный хранитель: [white]{Markup.Escape(sourceGuardianLabel)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.SourceGuardianId))
            lines.Add($"  [dim]Идентификатор хранителя: {Markup.Escape(entry.SourceGuardianId)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.SourceEntryId))
            lines.Add($"  Исходная запись Кодекса: [dim]{Markup.Escape(entry.SourceEntryId)}[/]");
        if (!string.IsNullOrWhiteSpace(entry.AcquiredAtUtc))
            lines.Add($"  Сохранено в Архив: [dim]{Markup.Escape(entry.AcquiredAtUtc)}[/]");
        if (entry.Tags.Count > 0)
            lines.Add($"  Метки: [dim]{Markup.Escape(string.Join(", ", entry.Tags))}[/]");
        if (entry.IsReserved)
        {
            lines.Add($"  Резервация: [yellow]{Markup.Escape(AfterlifeArchiveState.GetReservationLabel(entry.ReservationKind))}[/]");
            if (!string.IsNullOrWhiteSpace(targetProjectLabel))
                lines.Add($"  Целевой проект: [white]{Markup.Escape(targetProjectLabel)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            lines.Add("");
            lines.Add("[bold]Сводка:[/]");
            lines.Add($"  {Markup.Escape(entry.Summary)}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Content))
        {
            lines.Add("");
            lines.Add("[bold]Содержимое:[/]");
            lines.Add($"  {Markup.Escape(entry.Content)}");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📚 Точная архивная запись ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
        return true;
    }

    private static bool TryResolveResidentQuestNotificationQuestId(AfterlifeNotificationState.NotificationEntry notification, out string questId)
    {
        questId = string.Empty;
        if (!string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase))
            return false;

        var requestId = notification.RequestId ?? string.Empty;
        var separatorIndex = requestId.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex >= requestId.Length - 1)
            return false;

        questId = requestId[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(questId);
    }

    private static bool TryResolveGuardianQuestNotificationKey(
        AfterlifeNotificationState.NotificationEntry notification,
        out string guardianId,
        out string questId)
    {
        guardianId = string.Empty;
        questId = string.Empty;
        if (!string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase))
            return false;

        var requestId = notification.RequestId ?? string.Empty;
        var separatorIndex = requestId.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= requestId.Length - 1)
            return false;

        guardianId = requestId[..separatorIndex];
        questId = requestId[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(questId);
    }

    private async Task<bool> TryShowLinkedArchiveGuardianAsync(string guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        if (guardiansDoc == null || guardiansDoc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        var guardians = CollectGuardianDisplayEntries(guardiansDoc.RootElement);
        var guardian = guardians.FirstOrDefault(item =>
            string.Equals(GetStr(item, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase));
        if (guardian.ValueKind != JsonValueKind.Object)
            return false;

        using var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        var currentAbodeId = guardiansDoc.RootElement.TryGetProperty("chaosSeaNavigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object
            ? GetStr(navigation, "currentAbodeId", "")
            : string.Empty;
        var activeGuardianId = guardiansDoc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object
            ? GetStr(activeGuardian, "guardianId", "")
            : string.Empty;
        await ShowGuardianDetailPanel(guardian, guardians, currentAbodeId, activeGuardianId, trackerDoc?.RootElement);
        return true;
    }

    private async Task<bool> TryShowLinkedArchiveProjectAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return false;

        using var trackerDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.TrackerPath);
        if (trackerDoc == null || trackerDoc.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        JsonElement? projectEntry = null;
        foreach (var collectionName in new[] { "activeProjects", "completedProjects" })
        {
            if (!trackerDoc.RootElement.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in collection.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetStr(project, "projectId", ""), projectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                projectEntry = entry;
                break;
            }

            if (projectEntry.HasValue)
                break;
        }

        if (!projectEntry.HasValue)
            return false;

        var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var guardianNames = guardiansDoc != null
            ? BuildGuardianNameMap(guardiansDoc.RootElement)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var journalDoc = await _stateManager.LoadGameStateFileAsync(GuardianProjectState.JournalPath);
        ShowGuardianProjectDetailPanel(projectEntry.Value, guardianNames, journalDoc?.RootElement, trackerDoc.RootElement);
        return true;
    }

    private static string ResolveRelicCompanionNameHint(JsonElement relic)
    {
        if (relic.TryGetProperty("companionSeed", out var companionSeed) && companionSeed.ValueKind == JsonValueKind.Object)
        {
            var companionNameHint = GetStr(companionSeed, "companionNameHint", "");
            if (!string.IsNullOrEmpty(companionNameHint))
                return companionNameHint;
        }

        if (relic.TryGetProperty("soulImprint", out var soulImprint) && soulImprint.ValueKind == JsonValueKind.Object)
            return GetStr(soulImprint, "NPCName", GetStr(soulImprint, "npcName", GetStr(soulImprint, "name", GetStr(soulImprint, "companionName", GetStr(soulImprint, "originalName", "")))));

        if (relic.TryGetProperty("npcSoulImprint", out var npcSoulImprint) && npcSoulImprint.ValueKind == JsonValueKind.Object)
            return GetStr(npcSoulImprint, "NPCName", GetStr(npcSoulImprint, "npcName", GetStr(npcSoulImprint, "name", GetStr(npcSoulImprint, "companionName", GetStr(npcSoulImprint, "originalName", "")))));

        return "";
    }

    private static string ResolveRelicManifestationSourceLabel(JsonElement relic)
    {
        if (relic.TryGetProperty("companionSeed", out var companionSeed) &&
            companionSeed.ValueKind == JsonValueKind.Object &&
            !string.IsNullOrEmpty(GetStr(companionSeed, "sourceResidentId", "")))
        {
            return "связь с резидентом Обители";
        }

        if ((relic.TryGetProperty("soulImprint", out var soulImprint) && soulImprint.ValueKind == JsonValueKind.Object) ||
            (relic.TryGetProperty("npcSoulImprint", out var npcSoulImprint) && npcSoulImprint.ValueKind == JsonValueKind.Object))
        {
            return "слепок души";
        }

        return "";
    }

    private static IEnumerable<string> BuildCompanionSeedSnapshotLines(JsonElement companionSeed, JsonElement? residentRoot, JsonElement? guardiansRoot)
    {
        var lines = new List<string>();
        var originWorldSummary = GetStr(companionSeed, "originWorldSummary", "");
        if (!string.IsNullOrWhiteSpace(originWorldSummary))
            lines.Add($"  🌍 Мир происхождения: [dim]{Markup.Escape(originWorldSummary)}[/]");

        var futureCompanionPrompt = GetStr(companionSeed, "futureCompanionPrompt", "");
        if (!string.IsNullOrWhiteSpace(futureCompanionPrompt))
            lines.Add($"  🪶 Образ будущего спутника: [dim]{Markup.Escape(futureCompanionPrompt)}[/]");

        var bondReason = GetStr(companionSeed, "bondReason", "");
        if (!string.IsNullOrWhiteSpace(bondReason))
            lines.Add($"  🫀 Причина связи: [dim]{Markup.Escape(bondReason)}[/]");

        var sourceResidentId = GetStr(companionSeed, "sourceResidentId", "");
        if (!string.IsNullOrWhiteSpace(sourceResidentId))
        {
            var resolvedResident = ResolveCompanionSeedSourceResidentLabel(residentRoot, sourceResidentId);
            lines.Add($"  🏛️ Резидент-источник: [dim]{Markup.Escape(resolvedResident)}[/]");
        }

        var sourceGuardianId = GetStr(companionSeed, "sourceGuardianId", "");
        if (!string.IsNullOrWhiteSpace(sourceGuardianId))
        {
            var resolvedGuardian = ResolveCompanionSeedSourceGuardianLabel(guardiansRoot, sourceGuardianId);
            lines.Add($"  🛡️ Хранитель-источник: [dim]{Markup.Escape(resolvedGuardian)}[/]");
        }

        var coreTraits = ReadCanonicalStringArray(companionSeed, "coreTraits");
        if (coreTraits.Count > 0)
            lines.Add($"  🧬 Ключевые черты: [dim]{Markup.Escape(string.Join(", ", coreTraits))}[/]");

        var archetypeHints = ReadCanonicalStringArray(companionSeed, "archetypeHints");
        if (archetypeHints.Count > 0)
            lines.Add($"  🧭 Архетипические намёки: [dim]{Markup.Escape(string.Join(", ", archetypeHints))}[/]");

        var appearanceMotifs = ReadCanonicalStringArray(companionSeed, "appearanceMotifs");
        if (appearanceMotifs.Count > 0)
            lines.Add($"  🎨 Образы и мотивы: [dim]{Markup.Escape(string.Join(", ", appearanceMotifs))}[/]");

        if (companionSeed.TryGetProperty("personalityProfile", out var personalityProfile) &&
            personalityProfile.ValueKind == JsonValueKind.Object)
        {
            var archetype = GetStr(personalityProfile, "archetype", "");
            var worldview = GetStr(personalityProfile, "worldview", "");
            var culturalLayer = GetStr(personalityProfile, "culturalLayer", "");
            var coreValues = personalityProfile.TryGetProperty("coreValues", out var coreValuesNode) && coreValuesNode.ValueKind == JsonValueKind.Array
                ? coreValuesNode.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray()
                : Array.Empty<string>();
            var personalityTraits = new List<string>();
            var detailedTraits = new List<string>();
            if (personalityProfile.TryGetProperty("personalityTraits", out var traitsNode) && traitsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in traitsNode.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var trait = item.GetString();
                        if (!string.IsNullOrWhiteSpace(trait))
                            personalityTraits.Add(trait!);
                        continue;
                    }

                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var traitName = GetStr(item, "traitName", "");
                    var valueText = item.TryGetProperty("value", out var valueNode) && valueNode.ValueKind == JsonValueKind.Number
                        ? valueNode.ToString()
                        : string.Empty;
                    var valueDescription = GetStr(item, "valueDescription", "");
                    if (string.IsNullOrWhiteSpace(traitName))
                        continue;

                    personalityTraits.Add(traitName);
                    var detail = string.IsNullOrWhiteSpace(valueText)
                        ? traitName
                        : $"{traitName} {valueText}/10";
                    if (!string.IsNullOrWhiteSpace(valueDescription))
                        detail += $" — {valueDescription}";
                    detailedTraits.Add(detail);
                }
            }

            var flavorParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(archetype))
                flavorParts.Add(archetype);
            if (!string.IsNullOrWhiteSpace(worldview))
                flavorParts.Add(worldview);
            if (!string.IsNullOrWhiteSpace(culturalLayer))
                flavorParts.Add($"культурный слой: {culturalLayer}");
            if (coreValues.Length > 0)
                flavorParts.Add($"ценности: {string.Join(", ", coreValues)}");
            if (personalityTraits.Count > 0)
                flavorParts.Add($"черты: {string.Join(", ", personalityTraits)}");

            if (flavorParts.Count > 0)
                lines.Add($"  🎭 Снимок личности: [dim]{Markup.Escape(string.Join(" • ", flavorParts))}[/]");
            if (detailedTraits.Count > 0)
            {
                lines.Add("  Черты личности:");
                foreach (var trait in detailedTraits)
                    lines.Add($"    [dim]• {Markup.Escape(trait)}[/]");
            }
        }

        var abodeDevotionLevel = 0;
        var hasDevotion = companionSeed.TryGetProperty("abodeDevotionLevel", out var abodeDevotionLevelNode) &&
                          abodeDevotionLevelNode.ValueKind == JsonValueKind.Number &&
                          abodeDevotionLevelNode.TryGetInt32(out abodeDevotionLevel);
        var abodeDevotionTier = GetStr(companionSeed, "abodeDevotionTier", "");
        var restlessness = 0;
        var hasRestlessness = companionSeed.TryGetProperty("restlessness", out var restlessnessNode) &&
                              restlessnessNode.ValueKind == JsonValueKind.Number &&
                              restlessnessNode.TryGetInt32(out restlessness);
        var migrationState = GetStr(companionSeed, "migrationState", "");
        if (hasDevotion || hasRestlessness || !string.IsNullOrWhiteSpace(migrationState))
        {
            var devotionParts = new List<string>();
            if (hasDevotion)
            {
                var resolvedTier = !string.IsNullOrWhiteSpace(abodeDevotionTier)
                    ? abodeDevotionTier
                    : GuardianAbodeResidentState.ResolveAbodeDevotionTier(abodeDevotionLevel);
                devotionParts.Add($"{GuardianAbodeResidentState.GetAbodeDevotionTierLabel(resolvedTier)} {abodeDevotionLevel}/100");
            }

            if (hasRestlessness)
                devotionParts.Add($"неспокойствие {restlessness}/100");

            if (!string.IsNullOrWhiteSpace(migrationState))
                devotionParts.Add(GuardianAbodeResidentState.GetMigrationStateLabel(migrationState));

            if (devotionParts.Count > 0)
                lines.Add($"  🫀 Состояние Обители: [dim]{Markup.Escape(string.Join(" • ", devotionParts))}[/]");
        }

        if (companionSeed.TryGetProperty("abodeDisposition", out var abodeDisposition) &&
            abodeDisposition.ValueKind == JsonValueKind.Object)
        {
            var dispositionParts = new List<string>();
            var powerSensitivity = GetStr(abodeDisposition, "powerSensitivity", "");
            if (!string.IsNullOrWhiteSpace(powerSensitivity))
                dispositionParts.Add(GuardianAbodeResidentState.GetPowerSensitivityLabel(powerSensitivity));

            var migrationDisposition = GetStr(abodeDisposition, "migrationDisposition", "");
            if (!string.IsNullOrWhiteSpace(migrationDisposition))
                dispositionParts.Add(GuardianAbodeResidentState.GetMigrationDispositionLabel(migrationDisposition));

            var communalOrientation = GetStr(abodeDisposition, "communalOrientation", "");
            if (!string.IsNullOrWhiteSpace(communalOrientation))
                dispositionParts.Add(GuardianAbodeResidentState.GetCommunalOrientationLabel(communalOrientation));

            var stabilityNeed = GetStr(abodeDisposition, "stabilityNeed", "");
            if (!string.IsNullOrWhiteSpace(stabilityNeed))
                dispositionParts.Add(GuardianAbodeResidentState.GetStabilityNeedLabel(stabilityNeed));

            if (dispositionParts.Count > 0)
                lines.Add($"  🧭 Склонности Обители: [dim]{Markup.Escape(string.Join(" • ", dispositionParts))}[/]");
        }

        return lines;
    }

    private static string ResolveCompanionSeedSourceResidentLabel(JsonElement? residentRoot, string residentId)
    {
        if (residentRoot is not JsonElement residentElement ||
            residentElement.ValueKind != JsonValueKind.Object ||
            !residentElement.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return residentId;
        }

        foreach (var resident in entries.EnumerateArray())
        {
            if (!string.Equals(GetStr(resident, "residentId", ""), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            var residentName = GetStr(resident, "displayName", GetStr(resident, "residentName", residentId));
            return string.Equals(residentName, residentId, StringComparison.OrdinalIgnoreCase)
                ? residentId
                : $"{residentName} ({residentId})";
        }

        return residentId;
    }

    private static string ResolveCompanionSeedSourceGuardianLabel(JsonElement? guardiansRoot, string guardianId)
    {
        if (guardiansRoot is not JsonElement guardiansElement || guardiansElement.ValueKind != JsonValueKind.Object)
            return guardianId;

        if (guardiansElement.TryGetProperty("activeGuardian", out var activeGuardian) &&
            activeGuardian.ValueKind == JsonValueKind.Object &&
            string.Equals(GetStr(activeGuardian, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            var guardianName = GetStr(activeGuardian, "canonicalName",
                GetStr(activeGuardian, "name", guardianId));
            return string.Equals(guardianName, guardianId, StringComparison.OrdinalIgnoreCase)
                ? guardianId
                : $"{guardianName} ({guardianId})";
        }

        if (!guardiansElement.TryGetProperty("guardians", out var guardians) || guardians.ValueKind != JsonValueKind.Array)
            return guardianId;

        foreach (var guardian in guardians.EnumerateArray())
        {
            if (!string.Equals(GetStr(guardian, "guardianId", ""), guardianId, StringComparison.OrdinalIgnoreCase))
                continue;

            var guardianName = GetStr(guardian, "canonicalName",
                GetStr(guardian, "name", guardianId));
            return string.Equals(guardianName, guardianId, StringComparison.OrdinalIgnoreCase)
                ? guardianId
                : $"{guardianName} ({guardianId})";
        }

        return guardianId;
    }

    private static List<string> ReadCanonicalStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyNode) || propertyNode.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return propertyNode.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }

    private static string GuardianTradeDisplayDomain(string domainTag) => domainTag switch
    {
        "Combat" => "Боевой домен",
        "Magic" => "Магический домен",
        "Social" => "Социальный домен",
        "Crafting" => "Ремесленный домен",
        "Survival" => "Домен выживания",
        "Knowledge" => "Домен знания",
        "Trade" => "Торговый домен",
        _ => domainTag
    };

    /// <summary>
    /// Moves a relic from stored[] to equipped[] in soul_state.json.
    /// </summary>
    private async Task EquipSoulRelicLocal(string relicId, string relicName, string defaultSlot)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var relicsNode = node?["soulRelics"];
            if (relicsNode == null) return;

            var storedArr = relicsNode["stored"]?.AsArray();
            var equippedArr = relicsNode["equipped"]?.AsArray();
            if (storedArr == null || equippedArr == null) return;

            // Find the relic in stored
            JsonNode? target = null;
            int targetIdx = -1;
            for (int i = 0; i < storedArr.Count; i++)
            {
                if (RelicNodeMatches(storedArr[i], relicId, relicName)) { target = storedArr[i]; targetIdx = i; break; }
            }
            if (target == null || targetIdx < 0) return;

            // Remove from stored
            storedArr.RemoveAt(targetIdx);

            // Update gameplay status
            if (target["gameplayStatus"] is JsonObject gs)
            {
                gs["equipped"] = true;
                gs["currentSlot"] = !string.IsNullOrEmpty(defaultSlot) ? defaultSlot : "Default";
            }
            else
            {
                target["gameplayStatus"] = new JsonObject
                {
                    ["equipped"] = true,
                    ["currentSlot"] = !string.IsNullOrEmpty(defaultSlot) ? defaultSlot : "Default"
                };
            }

            // Add to equipped
            equippedArr.Add(target);

            // Write back
                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
            await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));

            MarkupLine($"[green]✅ Реликвия «{Markup.Escape(relicName)}» экипирована![/]");
            MarkupLine("[dim]Нажмите любую клавишу...[/]");
            ReadKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>
    /// Moves a relic from equipped[] to stored[] in soul_state.json.
    /// </summary>
    private async Task UnequipSoulRelicLocal(string relicId, string relicName)
    {
        const string path = "game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(path);
        if (json == null) return;

        try
        {
            var node = JsonNode.Parse(json);
            var relicsNode = node?["soulRelics"];
            if (relicsNode == null) return;

            var storedArr = relicsNode["stored"]?.AsArray();
            var equippedArr = relicsNode["equipped"]?.AsArray();
            if (storedArr == null || equippedArr == null) return;

            JsonNode? target = null;
            int targetIdx = -1;
            for (int i = 0; i < equippedArr.Count; i++)
            {
                if (RelicNodeMatches(equippedArr[i], relicId, relicName)) { target = equippedArr[i]; targetIdx = i; break; }
            }
            if (target == null || targetIdx < 0) return;

            equippedArr.RemoveAt(targetIdx);

            if (target["gameplayStatus"] is JsonObject gs)
            {
                gs["equipped"] = false;
                gs["currentSlot"] = "";
            }

            storedArr.Add(target);

                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
            await _fs.WriteFileAtomicAsync(path, node!.ToJsonString(opts));

            MarkupLine($"[green]✅ Реликвия «{Markup.Escape(relicName)}» снята и убрана в хранилище.[/]");
            MarkupLine("[dim]Нажмите любую клавишу...[/]");
            ReadKey();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    /// <summary>
    /// Interactive storage panel: deposit items from inventory or retrieve items from storage.
    /// Modifies items.json and current_location.json directly (no GM needed).
    /// Returns true if any changes were made.
    /// </summary>
    private async Task<bool> ShowStorageInteractivePanel(string storageName, string storageId)
    {
        bool anyModified = false;

        while (true)
        {
            // Re-read both files each iteration
            var invJson = await _fs.ReadFileAsync("game_state/inventory/items.json");
            var locJson = await _fs.ReadFileAsync("game_state/world/current_location.json");
            if (invJson == null || locJson == null)
            {
                MarkupLine("[red]Ошибка чтения файлов инвентаря или локации.[/]");
                WaitForKey();
                return anyModified;
            }

            JsonNode? invNode, locNode;
            try { invNode = JsonNode.Parse(invJson); locNode = JsonNode.Parse(locJson); }
            catch { MarkupLine("[red]Ошибка парсинга JSON.[/]"); WaitForKey(); return anyModified; }
            if (invNode == null || locNode == null) return anyModified;

            // Find the storage in current_location
            var storagesArr = locNode["locationStorages"]?.AsArray();
            if (storagesArr == null) { MarkupLine("[red]Хранилища не найдены в локации.[/]"); WaitForKey(); return anyModified; }

            JsonNode? storageNode = null;
            int storageIdx = -1;
            for (int i = 0; i < storagesArr.Count; i++)
            {
                var sid = storagesArr[i]?["storageId"]?.GetValue<string>() ?? "";
                var sname = storagesArr[i]?["name"]?.GetValue<string>() ?? "";
                if ((!string.IsNullOrEmpty(storageId) && sid == storageId) ||
                    sname == storageName)
                {
                    storageNode = storagesArr[i];
                    storageIdx = i;
                    break;
                }
            }
            if (storageNode == null) { MarkupLine("[red]Хранилище не найдено.[/]"); WaitForKey(); return anyModified; }

            // Gather storage contents
            var contentsArr = storageNode["contents"]?.AsArray() ?? new JsonArray();
            var storageEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < contentsArr.Count; i++)
            {
                var iName = GetInventoryItemName(contentsArr[i]);
                var iQty = contentsArr[i]?["quantity"]?.ToString() ??
                           contentsArr[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(iName);
                if (iQty != "1") label += $" ×{Markup.Escape(iQty)}";
                storageEntries.Add((GetInventoryItemIdentity(contentsArr[i]), iName, label));
            }
            var storageItems = MakeUniqueChoiceLabels(storageEntries.Select(e => (e.Label, e.Identity)).ToList());

            // Gather player inventory items
            var invItemsArr = GetPlayerInventoryArrayNode(invNode, createIfMissing: false) ?? new JsonArray();
            var playerEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < invItemsArr.Count; i++)
            {
                var iName = GetInventoryItemName(invItemsArr[i]);
                var iQty = invItemsArr[i]?["quantity"]?.ToString() ??
                           invItemsArr[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(iName);
                if (iQty != "1") label += $" ×{Markup.Escape(iQty)}";
                playerEntries.Add((GetInventoryItemIdentity(invItemsArr[i]), iName, label));
            }
            var playerItems = MakeUniqueChoiceLabels(playerEntries.Select(e => (e.Label, e.Identity)).ToList());

            // Capacity info
            var capStr = storageNode["capacity"]?.ToString() ?? "";
            var volStr = storageNode["volume"]?.ToString() ?? "";
            var capInfo = "";
            if (!string.IsNullOrEmpty(capStr)) capInfo += $" вместимость: {capStr}";
            if (!string.IsNullOrEmpty(volStr)) capInfo += $" объём: {volStr} дм³";

            // Show action menu
            var actionChoices = new List<string>();
            if (playerItems.Count > 0)
                actionChoices.Add($"📥 Положить предмет в хранилище ({playerItems.Count} в инвентаре)");
            if (storageItems.Count > 0)
                actionChoices.Add($"📤 Забрать предмет из хранилища ({storageItems.Count} внутри)");
            actionChoices.Add("[dim]← Назад к инвентарю[/]");

            var action = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]📦 {Markup.Escape(storageName)}[/]" +
                    (!string.IsNullOrEmpty(capInfo) ? $"  [dim]({capInfo.Trim()})[/]" : "") +
                    $"\n  [dim]Предметов внутри: {contentsArr.Count}[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actionChoices));

            if (action.Contains("← Назад")) return anyModified;

            var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

            if (action.StartsWith("📥")) // Deposit
            {
                var depositChoices = playerItems.ToList();
                depositChoices.Add("[dim]← Отмена[/]");

                var picked = Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для перемещения в хранилище:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(depositChoices));

                if (picked.Contains("← Отмена")) continue;

                var pickedIdx = depositChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= invItemsArr.Count) continue;

                try
                {
                    // Remove from player inventory
                    var itemToMove = invItemsArr[pickedIdx]!;
                    invItemsArr.RemoveAt(pickedIdx);

                    // Add to storage contents
                    if (storageNode["contents"] == null)
                        storageNode["contents"] = new JsonArray();
                    storageNode["contents"]!.AsArray().Add(itemToMove);

                    // Write both files
                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", locNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» перемещён в хранилище «{Markup.Escape(storageName)}»[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
            else if (action.StartsWith("📤")) // Retrieve
            {
                var retrieveChoices = storageItems.ToList();
                retrieveChoices.Add("[dim]← Отмена[/]");

                var picked = Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для извлечения из хранилища:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(retrieveChoices));

                if (picked.Contains("← Отмена")) continue;

                var pickedIdx = retrieveChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= contentsArr.Count) continue;

                try
                {
                    // Remove from storage
                    var itemToMove = contentsArr[pickedIdx]!;
                    contentsArr.RemoveAt(pickedIdx);

                    // Add to player inventory
                    var playerInventory = GetPlayerInventoryArrayNode(invNode, createIfMissing: true);
                    playerInventory!.Add(itemToMove);

                    // Write both files
                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", locNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» извлечён из хранилища в инвентарь[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    /// <summary>
    /// Interactive vehicle inventory panel: move items between player inventory and vehicle inventory.
    /// Modifies items.json and vehicles.json directly (no GM needed).
    /// Returns true if any changes were made.
    /// </summary>
    private async Task<bool> ShowVehicleInventoryInteractivePanel(string vehicleName, string vehicleId)
    {
        bool anyModified = false;

        while (true)
        {
            var invJson = await _fs.ReadFileAsync("game_state/inventory/items.json");
            var vehJson = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
            if (invJson == null || vehJson == null)
            {
                MarkupLine("[red]Ошибка чтения файлов инвентаря или транспорта.[/]");
                WaitForKey();
                return anyModified;
            }

            JsonNode? invNode;
            JsonNode? vehNode;
            try
            {
                invNode = JsonNode.Parse(invJson);
                vehNode = JsonNode.Parse(vehJson);
            }
            catch
            {
                MarkupLine("[red]Ошибка парсинга JSON.[/]");
                WaitForKey();
                return anyModified;
            }

            if (invNode == null || vehNode == null)
                return anyModified;

            var vehicleNode = FindVehicleNode(vehNode, vehicleName, vehicleId);
            if (vehicleNode == null)
            {
                MarkupLine("[red]Транспорт не найден.[/]");
                WaitForKey();
                return anyModified;
            }

            var vehicleInventory = vehicleNode["inventory"]?.AsArray() ?? new JsonArray();
            var playerInventory = GetPlayerInventoryArrayNode(invNode, createIfMissing: false) ?? new JsonArray();

            var vehicleEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < vehicleInventory.Count; i++)
            {
                var itemName = GetInventoryItemName(vehicleInventory[i]);
                var qty = vehicleInventory[i]?["quantity"]?.ToString() ??
                          vehicleInventory[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(itemName);
                if (qty != "1") label += $" ×{Markup.Escape(qty)}";
                vehicleEntries.Add((GetInventoryItemIdentity(vehicleInventory[i]), itemName, label));
            }
            var vehicleItems = MakeUniqueChoiceLabels(vehicleEntries.Select(e => (e.Label, e.Identity)).ToList());

            var playerEntries = new List<(string Identity, string Name, string Label)>();
            for (int i = 0; i < playerInventory.Count; i++)
            {
                var itemName = GetInventoryItemName(playerInventory[i]);
                var qty = playerInventory[i]?["quantity"]?.ToString() ??
                          playerInventory[i]?["count"]?.ToString() ?? "1";
                var label = Markup.Escape(itemName);
                if (qty != "1") label += $" ×{Markup.Escape(qty)}";
                playerEntries.Add((GetInventoryItemIdentity(playerInventory[i]), itemName, label));
            }
            var playerItems = MakeUniqueChoiceLabels(playerEntries.Select(e => (e.Label, e.Identity)).ToList());

            var actionChoices = new List<string>();
            if (playerItems.Count > 0)
                actionChoices.Add($"📥 Положить предмет в транспорт ({playerItems.Count} в инвентаре)");
            if (vehicleItems.Count > 0)
                actionChoices.Add($"📤 Забрать предмет из транспорта ({vehicleItems.Count} внутри)");
            actionChoices.Add("[dim]← Назад к транспорту[/]");

            var action = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🚗 {Markup.Escape(vehicleName)}[/]\n  [dim]Предметов внутри: {vehicleInventory.Count}[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actionChoices));

            if (action.Contains("← Назад"))
                return anyModified;

            var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

            if (action.StartsWith("📥"))
            {
                var depositChoices = playerItems.ToList();
                depositChoices.Add("[dim]← Отмена[/]");

                var picked = Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для перемещения в транспорт:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(depositChoices));

                if (picked.Contains("← Отмена"))
                    continue;

                var pickedIdx = depositChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= playerInventory.Count)
                    continue;

                try
                {
                    var itemToMove = playerInventory[pickedIdx]!;
                    playerInventory.RemoveAt(pickedIdx);

                    if (vehicleNode["inventory"] == null)
                        vehicleNode["inventory"] = new JsonArray();
                    vehicleNode["inventory"]!.AsArray().Add(itemToMove);

                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", vehNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» перемещён в транспорт «{Markup.Escape(vehicleName)}»[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
            else if (action.StartsWith("📤"))
            {
                var retrieveChoices = vehicleItems.ToList();
                retrieveChoices.Add("[dim]← Отмена[/]");

                var picked = Prompt(new SelectionPrompt<string>()
                    .Title("[bold]Выберите предмет для извлечения из транспорта:[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(Color.Yellow))
                    .AddChoices(retrieveChoices));

                if (picked.Contains("← Отмена"))
                    continue;

                var pickedIdx = retrieveChoices.IndexOf(picked);
                if (pickedIdx < 0 || pickedIdx >= vehicleInventory.Count)
                    continue;

                try
                {
                    var itemToMove = vehicleInventory[pickedIdx]!;
                    vehicleInventory.RemoveAt(pickedIdx);

                    var playerInventoryTarget = GetPlayerInventoryArrayNode(invNode, createIfMissing: true);
                    playerInventoryTarget!.Add(itemToMove);

                    await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", invNode.ToJsonString(opts));
                    await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", vehNode.ToJsonString(opts));

                    var movedName = itemToMove["name"]?.GetValue<string>() ?? "предмет";
                    MarkupLine($"[green]✅ «{Markup.Escape(movedName)}» извлечён из транспорта в инвентарь[/]");
                    anyModified = true;
                }
                catch (Exception ex)
                {
                    MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    private static List<JsonElement> CollectCodexEntries(JsonElement root)
    {
        var entries = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("entries", out var existingEntries) && existingEntries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in existingEntries.EnumerateArray())
                    if (entry.ValueKind == JsonValueKind.Object)
                        entries.Add(entry.Clone());
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
                if (entry.ValueKind == JsonValueKind.Object)
                    entries.Add(entry.Clone());
        }

        return entries
            .GroupBy(e => GetStr(e, "entryId", GetStr(e, "title", GetStr(e, "name", e.GetRawText()))), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<JsonElement> CollectGuardianDisplayEntries(JsonElement root)
    {
        var guardians = new List<JsonElement>();
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddGuardian(JsonElement guardian)
        {
            if (guardian.ValueKind != JsonValueKind.Object) return;
            var key = GetStr(guardian, "guardianId", GuardianManifestation.GetCanonicalName(guardian));
            if (!string.IsNullOrWhiteSpace(key))
            {
                if (!knownIds.Add(key))
                    return;
            }
            guardians.Add(guardian.Clone());
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in root.EnumerateArray())
                AddGuardian(guardian);
            return guardians;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return guardians;

        if (root.TryGetProperty("guardians", out var guardiansArr) && guardiansArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardiansArr.EnumerateArray())
                AddGuardian(guardian);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
            AddGuardian(activeGuardian);

        if ((root.TryGetProperty("guardianId", out _) || root.TryGetProperty("canonicalName", out _) || root.TryGetProperty("manifestation", out _)) &&
            !root.TryGetProperty("guardians", out _))
            AddGuardian(root);

        return guardians;
    }

    private static JsonNode? FindVehicleNode(JsonNode root, string vehicleName, string vehicleId)
    {
        JsonArray? vehiclesArray = null;
        if (root is JsonObject obj)
            vehiclesArray = obj["vehicles"]?.AsArray();
        else if (root is JsonArray arr)
            vehiclesArray = arr;

        if (vehiclesArray == null)
            return null;

        foreach (var vehicle in vehiclesArray)
        {
            if (vehicle == null)
                continue;

            var id = vehicle["vehicleId"]?.GetValue<string>() ??
                     vehicle["id"]?.GetValue<string>() ?? "";
            var name = vehicle["name"]?.GetValue<string>() ?? "";

            if ((!string.IsNullOrEmpty(vehicleId) && id == vehicleId) ||
                (!string.IsNullOrEmpty(vehicleName) && name == vehicleName))
                return vehicle;
        }

        return null;
    }

    private async Task ShowSoulQuests()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable(_loc.T("guardian_quests")))
            return;

        await SyncAfterlifeNotificationsAsync();
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        if (doc == null)
        {
            ShowEmptyPanel(_loc.T("guardian_quests"), "Мета-квестов нет");
            return;
        }

        var unreadGuardianQuestNotifications = (await AfterlifeNotificationState.ReadAsync(_fs))
            .Where(notification =>
                string.Equals(notification.Status, AfterlifeNotificationState.StatusUnread, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeAbodeResidentQuestAvailable, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (unreadGuardianQuestNotifications.Count > 0)
        {
            var bannerLines = new List<string> { "[bold yellow]📬 Новые квесты и просьбы из загробья[/]" };
            foreach (var notification in unreadGuardianQuestNotifications.Take(3))
                bannerLines.Add($"[dim]• {Markup.Escape(notification.Summary)}[/]");
            if (unreadGuardianQuestNotifications.Count > 3)
                bannerLines.Add($"[dim]… и ещё {unreadGuardianQuestNotifications.Count - 3}. Откройте /уведомления_загробья[/]");

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", bannerLines)))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(1, 1),
                Expand = true
            });
        }

        var quests = new List<(string label, JsonElement el)>();
        EnumerateArray(doc.RootElement, "quests", item =>
        {
            var name = GetStr(item, "questName", GetStr(item, "name", "???"));
            var status = GetStr(item, "status", "Active").ToLowerInvariant();
            var icon = status switch { "completed" or "завершён" => "✅", "failed" or "провален" => "❌", _ => "🔄" };
            var guardian = GetStr(item, "guardian", GetStr(item, "questGiver", ""));
            var suffix = string.IsNullOrWhiteSpace(guardian) ? "" : $" [dim]({Markup.Escape(guardian)})[/]";
            var rivalPrefix = HasRelatedRivalArc(item) ? "🧵 " : "";
            quests.Add(($"{rivalPrefix}🌟 {icon} {name}{suffix}", item));
        });

        if (quests.Count == 0)
        {
            EnumerateJsonItems(doc.RootElement, item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "name", "???"));
                var status = GetStr(item, "status", "Active").ToLowerInvariant();
                var icon = status switch { "completed" or "завершён" => "✅", "failed" or "провален" => "❌", _ => "🔄" };
                var guardian = GetStr(item, "guardian", GetStr(item, "questGiver", ""));
                var suffix = string.IsNullOrWhiteSpace(guardian) ? "" : $" [dim]({Markup.Escape(guardian)})[/]";
                var rivalPrefix = HasRelatedRivalArc(item) ? "🧵 " : "";
                quests.Add(($"{rivalPrefix}🌟 {icon} {name}{suffix}", item));
            });
        }

        if (quests.Count == 0)
        {
            ShowEmptyPanel(_loc.T("guardian_quests"), "Мета-квестов нет");
            return;
        }

        while (true)
        {
            var choices = quests.Select(q => $"[purple]{Markup.Escape(q.label)}[/]").ToList();
            choices.Add("[dim]← Назад[/]");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold purple]🌟 {_loc.T("guardian_quests")}[/]")
                    .PageSize(12)
                    .AddChoices(choices));

            if (selected.Contains("← Назад"))
                return;

            var questIndex = choices.IndexOf(selected);
            if (questIndex < 0 || questIndex >= quests.Count)
                return;

            await ShowQuestDetailPanel(quests[questIndex].el, true, false);
        }
    }

    private async Task ShowRivalSoulThreads()
    {
        var doc = await _stateManager.LoadGameStateFileAsync(RivalSoulArcService.StatePath);
        var worldEventsDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_events.json");
        if (doc == null)
        {
            ShowEmptyPanel("🧵 Чужие нити судьбы", "В этой жизни проявления чужих нитей судьбы пока не замечены.");
            return;
        }

        var visibleArcs = ReadVisibleRivalSoulThreads(doc.RootElement, worldEventsDoc?.RootElement);
        if (visibleArcs.Count == 0)
        {
            ShowEmptyPanel("🧵 Чужие нити судьбы", "В этой жизни проявления чужих нитей судьбы пока не замечены.");
            return;
        }

        while (true)
        {
            var choices = visibleArcs
                .Select(arc => arc.ListLabel)
                .ToList();
            choices.Add("[dim]← Назад[/]");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold purple]🧵 Чужие нити судьбы[/]")
                    .PageSize(12)
                    .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= visibleArcs.Count)
                return;

            await ShowRivalSoulThreadDetailPanel(visibleArcs[index]);
        }
    }

    // ═══ Ink Feathers menu ═══

    /// <summary>
    /// Reads inkFeathers from soul_state.json, handling both object and number formats.
    /// </summary>
    private async Task<int> ReadInkFeathersBalance()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null) return 0;

        var root = doc.RootElement;
        if (!root.TryGetProperty("inkFeathers", out var feathersEl)) return 0;

        if (feathersEl.ValueKind == JsonValueKind.Number)
            return feathersEl.TryGetInt32(out var n) ? n : 0;

        if (feathersEl.ValueKind == JsonValueKind.Object &&
            feathersEl.TryGetProperty("current", out var cur) &&
            cur.ValueKind == JsonValueKind.Number)
            return cur.TryGetInt32(out var c) ? c : 0;

        return 0;
    }

    private async Task<string?> ReadPendingMemoryLegacySummaryAsync()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null) return null;

        var root = doc.RootElement;
        if (!root.TryGetProperty("pendingMemoryLegacy", out var legacy) || legacy.ValueKind != JsonValueKind.Object)
            return null;

        var legacyType = GetStr(legacy, "legacyType", "");
        if (legacyType.Equals("startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = GetStr(legacy, "characteristic", "");
            var bonus = GetInt(legacy, "bonus", 0);
            if (string.IsNullOrWhiteSpace(characteristic) || bonus <= 0) return null;
            var russianStat = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
            return $"🧠 Активное Наследие Памяти: +{bonus} к {russianStat} в следующей жизни";
        }

        if (legacyType.Equals("startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var skillName = GetStr(legacy, "skillName", "");
            if (string.IsNullOrWhiteSpace(skillName)) return null;
            return $"🧠 Активное Наследие Памяти: пассивный навык «{skillName}» в следующей жизни";
        }

        return null;
    }

    /// <summary>
    /// Deducts feathers from soul_state.json atomically.
    /// Handles both object { "current": N } and plain integer formats.
    /// Returns true on success.
    /// </summary>
    private async Task<bool> DeductInkFeathers(int cost)
    {
        const string path = "game_state/meta/soul_state.json";
        try
        {
            var jsonText = await _fs.ReadFileAsync(path);
            if (jsonText == null) return false;

            var node = JsonNode.Parse(jsonText);
            if (node == null) return false;

            var feathersNode = node["inkFeathers"];
            if (feathersNode == null) return false;

            if (feathersNode is JsonObject inkObj)
            {
                var oldVal = inkObj["current"]?.GetValue<int>() ?? 0;
                if (oldVal < cost) return false;
                inkObj["current"] = oldVal - cost;
            }
            else
            {
                int oldVal;
                try { oldVal = feathersNode.GetValue<int>(); }
                catch { oldVal = int.TryParse(feathersNode.ToString(), out var p) ? p : 0; }
                if (oldVal < cost) return false;
                node["inkFeathers"] = oldVal - cost;
            }

            var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatDiceDisplay(int[] dice)
    {
        var parts = new List<string>();
        for (int i = 0; i < dice.Length; i++)
        {
            var d = dice[i];
            var color = d switch
            {
                1 => "red",
                20 => "gold1",
                >= 15 => "green",
                >= 10 => "white",
                >= 5 => "yellow",
                _ => "red3"
            };
            parts.Add($"[{color}]{d}[/]");
        }
        return string.Join(" ", parts);
    }
}

