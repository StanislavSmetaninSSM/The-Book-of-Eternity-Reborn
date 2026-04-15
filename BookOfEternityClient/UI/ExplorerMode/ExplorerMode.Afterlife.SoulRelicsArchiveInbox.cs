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
                    if (!string.IsNullOrEmpty(s)) slotStr = $" [[{Markup.Escape(s)}]]";
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
                .Title("[bold yellow]📚 Архив души[/] [dim](сохранённые afterlife-записи)[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= entries.Count)
                return;

            var entry = entries[index];
            var lines = new List<string>
            {
                $"[bold yellow]📚 {Markup.Escape(entry.Title)}[/]",
                "",
                $"  Тип: [cyan]{Markup.Escape(AfterlifeArchiveState.GetEntryTypeLabel(entry.EntryType))}[/]",
                $"  Редкость: [{GetRarityColor(entry.Rarity)}]{Markup.Escape(entry.Rarity)}[/]",
                $"  Источник жизни: [yellow]{entry.SourceLife}[/]",
                $"  Источник записи: [dim]{Markup.Escape(AfterlifeArchiveState.GetSourceKindLabel(entry.SourceKind))}[/]"
            };

            if (!string.IsNullOrWhiteSpace(entry.SourceGuardianId))
                lines.Add($"  Связанный хранитель: [white]{Markup.Escape(entry.SourceGuardianId)}[/]");
            if (entry.Tags.Count > 0)
                lines.Add($"  Метки: [dim]{Markup.Escape(string.Join(", ", entry.Tags))}[/]");
            if (entry.IsReserved)
            {
                var reservedFor = !string.IsNullOrWhiteSpace(entry.ReservedForGuardianName)
                    ? entry.ReservedForGuardianName
                    : entry.ReservedForGuardianId;
                lines.Add($"  Статус: [yellow]зарезервирована[/] для [white]{Markup.Escape(reservedFor)}[/] через [yellow]{Markup.Escape(AfterlifeArchiveState.GetReservationLabel(entry.ReservationKind))}[/]");
                if (!string.IsNullOrWhiteSpace(entry.ReservedForProjectName) || !string.IsNullOrWhiteSpace(entry.ReservedForProjectId))
                    lines.Add($"  Целевой проект: [dim]{Markup.Escape(string.IsNullOrWhiteSpace(entry.ReservedForProjectName) ? entry.ReservedForProjectId : entry.ReservedForProjectName)}[/]");
            }
            if (!string.IsNullOrWhiteSpace(entry.Summary))
            {
                lines.Add("");
                lines.Add($"[white]{Markup.Escape(entry.Summary)}[/]");
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
                MarkupLine("[green]✅ Все afterlife-уведомления отмечены как прочитанные.[/]");
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
        if (!string.IsNullOrWhiteSpace(notification.CreatedAtUtc))
            lines.Add($"  Получено: [dim]{Markup.Escape(notification.CreatedAtUtc)}[/]");

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

        if (string.Equals(notification.NotificationType, AfterlifeNotificationState.TypeGuardianQuestAvailable, StringComparison.OrdinalIgnoreCase))
            actions.Add("🛡️ Открыть Хранителей");

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

        if (selected.StartsWith("🛒", StringComparison.Ordinal))
        {
            await ShowGuardianTradePanel(notification.GuardianId);
            return;
        }

        if (selected.StartsWith("📚", StringComparison.Ordinal))
        {
            await ShowAfterlifeArchive();
            return;
        }

        if (selected.StartsWith("🛡️", StringComparison.Ordinal))
        {
            await ShowGuardians();
            return;
        }

        if (selected.StartsWith("🧵", StringComparison.Ordinal))
        {
            await ShowSoulQuests();
            return;
        }

        if (selected.StartsWith("💎", StringComparison.Ordinal))
        {
            await ShowSoulRelics();
            return;
        }

        if (selected.StartsWith("🔬", StringComparison.Ordinal))
        {
            await ShowGuardianProjects();
            return;
        }

        if (selected.Contains("Отметить", StringComparison.OrdinalIgnoreCase))
            await AfterlifeNotificationState.MarkReadAsync(_fs, notification.NotificationId);
    }

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
                $"  Редкость: [{GetRarityColor(candidate.Rarity)}]{Markup.Escape(candidate.Rarity)}[/]",
                $"  Статус: [white]{Markup.Escape(statusLabel)}[/]",
                $"  Жизнь-источник: [yellow]{candidate.SourceLife}[/]",
                $"  Источник записи: [dim]{Markup.Escape(AfterlifeArchiveState.GetSourceKindLabel(candidate.SourceKind))}[/]"
            };

            if (!string.IsNullOrWhiteSpace(candidate.DiscoveredAt))
                lines.Add($"  Обнаружено: [dim]{Markup.Escape(candidate.DiscoveredAt)}[/]");
            if (candidate.Tags.Count > 0)
                lines.Add($"  Метки: [dim]{Markup.Escape(string.Join(", ", candidate.Tags))}[/]");

            lines.Add("");
            lines.Add($"[white]{Markup.Escape(candidate.Summary)}[/]");

            if (!string.Equals(candidate.Status, AfterlifeArchiveCandidateService.StatusPending, StringComparison.OrdinalIgnoreCase))
            {
                Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
                {
                    Header = new PanelHeader(" 🗂 Кандидат в Архив ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1),
                    Expand = true
                });
                WaitForKey();
                continue;
            }

            var action = Prompt(new SelectionPrompt<string>()
                .Title(string.Join("\n", lines) + "\n\n[bold]Действие:[/]")
                .PageSize(6)
                .HighlightStyle(new Style(Color.Yellow))
                .AddChoices("💾 Сохранить в Архив", "⏭ Пропустить", "← Назад"));

            if (action == "← Назад")
                continue;

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

    /// <summary>
    /// Displays detailed information about a soul relic.
    /// In Chaos Sea: offers equip/unequip actions that modify soul_state.json directly.
    /// Returns true if state was modified (needs refresh).
    /// </summary>
    private async Task<bool> ShowRelicDetailPanel(string relicId, string name, string status, JsonElement relic, bool isAfterlifeRealm)
    {
        var lines = BuildSoulRelicDetailLines(name, relic, status);
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
            var actions = new List<string>();
            if (status == "stored")
                actions.Add("⚔ Экипировать");
            else
                actions.Add("📦 Снять (в хранилище)");
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

    private List<string> BuildSoulRelicDetailLines(string name, JsonElement relic, string? status)
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
            lines.Add($"  📌 Слот: [cyan]{Markup.Escape(slot)}[/]");

        var rarity = GetStr(relic, "quality", GetStr(relic, "rarity", ""));
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(rarity)}[/]");

        var category = GetStr(relic, "category", "");
        if (!string.IsNullOrEmpty(category))
            lines.Add($"  📋 Категория: [cyan]{Markup.Escape(category)}[/]");

        var tier = GetStr(relic, "tier", "");
        if (!string.IsNullOrEmpty(tier))
            lines.Add($"  🏆 Ранг: [yellow]{Markup.Escape(tier)}[/]");

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
                    lines.Add($"    • [cyan]{Markup.Escape(prop.Name)}: +{prop.Value}[/]");
            }

            var knownEffectProps = new HashSet<string> { "characteristicBonuses", "actionCheckBonuses" };
            foreach (var prop in effects.EnumerateObject())
            {
                if (knownEffectProps.Contains(prop.Name)) continue;
                if (prop.Value.ValueKind == JsonValueKind.String)
                    lines.Add($"    • [green]{Markup.Escape(prop.Name)}: {Markup.Escape(prop.Value.GetString() ?? "")}[/]");
                else if (prop.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    lines.Add($"    • [green]{Markup.Escape(prop.Name)}: {prop.Value}[/]");
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
                    var bName = GetStr(b, "name", GetStr(b, "stat", ""));
                    var bVal = GetStr(b, "value", GetStr(b, "bonus", ""));
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
                foreach (var snapshotLine in BuildCompanionSeedSnapshotLines(companionSeed))
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

    private static IEnumerable<string> BuildCompanionSeedSnapshotLines(JsonElement companionSeed)
    {
        var lines = new List<string>();

        if (companionSeed.TryGetProperty("personalityProfile", out var personalityProfile) &&
            personalityProfile.ValueKind == JsonValueKind.Object)
        {
            var archetype = GetStr(personalityProfile, "archetype", "");
            var worldview = GetStr(personalityProfile, "worldview", "");
            var coreValues = personalityProfile.TryGetProperty("coreValues", out var coreValuesNode) && coreValuesNode.ValueKind == JsonValueKind.Array
                ? coreValuesNode.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Take(3)
                    .ToArray()
                : Array.Empty<string>();

            var flavorParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(archetype))
                flavorParts.Add(archetype);
            if (!string.IsNullOrWhiteSpace(worldview))
                flavorParts.Add(worldview);
            if (coreValues.Length > 0)
                flavorParts.Add($"ценности: {string.Join(", ", coreValues)}");

            if (flavorParts.Count > 0)
                lines.Add($"  🎭 Снимок личности: [dim]{Markup.Escape(string.Join(" • ", flavorParts))}[/]");
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

