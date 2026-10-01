using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private async Task OptionsMenu()
    {
        var session = await ConsoleSettingsSession.OpenAsync(_fs, _stateManager, _systemModService);
        var settings = session.Draft;
        await using var preview = new ConsoleSettingsPreview(_stateManager.Settings, settings, _loc,
            _audioService, _consoleAppearance, RefreshAudioPlaybackContextAsync);
        string? notice = null;
        string? reconciliationOutcomeMessage = null;
        var exitAfterReconcile = false;
        async Task<BrowserPreparedWriteResult> SaveDraftAsync()
        {
            var outcome = await session.SaveAsync(async () => { await preview.ApplyAsync(); });
            notice = outcome.Message;
            reconciliationOutcomeMessage = session.RequiresReload ? outcome.Message : null;
            return outcome;
        }
        async Task RestoreLastAcceptedEffectsAsync()
        {
            try { await preview.RestoreLastAcceptedEffectsAsync(); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Last accepted settings effects require follow-up.");
                notice += " Не удалось полностью восстановить прежний вид или звук; требуется повторная проверка.";
            }
        }
        static bool CanLeave(BrowserPreparedWriteResult result) =>
            result.Disposition == BrowserPreparedWriteDisposition.Committed && !result.NeedsFollowUp;

        var selectedIndex = 0;
        var lastWidth = -1;
        var lastHeight = -1;
        var menuTop = 0;

        while (true)
        {
            if (session.RequiresReload)
            {
                await RestoreLastAcceptedEffectsAsync();
                var action = ShowSingleChoiceMenu("Настройки требуют проверки",
                    new List<MenuChoiceItem> { new("reload", "Перечитать сохранённые настройки", "Отменить неподтверждённый черновик после успешного чтения", "yellow") },
                    footer: notice + " Esc — остаться в настройках");
                if (action == null) continue;
                try
                {
                    await session.ReloadAsync(async () => { await preview.ApplyAsync(); });
                    notice = "Подтверждённые настройки перечитаны.";
                    reconciliationOutcomeMessage = null;
                    if (exitAfterReconcile) return;
                }
                catch (Exception ex) when (ex is not ConsoleE2EScriptInputException)
                {
                    _logger.LogWarning(ex, "Console settings refresh did not complete.");
                    notice = (reconciliationOutcomeMessage == null ? string.Empty : reconciliationOutcomeMessage + " ")
                        + "Обновление настроек не завершено. Повторите проверку перед продолжением.";
                    await RestoreLastAcceptedEffectsAsync();
                    continue;
                }
                lastWidth = -1;
            }
            var entries = await BuildOptionsEntriesAsync(session);
            if (selectedIndex >= entries.Count)
                selectedIndex = Math.Max(0, entries.Count - 1);

            var currentWidth = GetSafeConsoleWidth();
            var currentHeight = GetSafeConsoleHeight();
            if (currentWidth != lastWidth || currentHeight != lastHeight)
            {
                menuTop = RenderOptionsStaticFrame(settings, notice);
                RedrawOptionsMenuArea(entries, selectedIndex, menuTop, currentHeight, settings, notice);
                WriteOptionsMenuObservation(entries, selectedIndex, "options-menu", notice);
                lastWidth = currentWidth;
                lastHeight = currentHeight;
            }

            var key = _inputSource.ReadKey(intercept: true);
            var selectionChanged = false;
            OptionsMenuEntry? chosen = null;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + entries.Count) % entries.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % entries.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.Escape:
                    exitAfterReconcile = true;
                    if (CanLeave(await SaveDraftAsync())) return;
                    lastWidth = -1;
                    continue;
                case ConsoleKey.Enter:
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    chosen = entries[selectedIndex];
                    break;
            }

            if (selectionChanged)
            {
                RedrawOptionsMenuArea(entries, selectedIndex, menuTop, currentHeight, settings, notice);
                WriteOptionsMenuObservation(entries, selectedIndex, "options-menu", notice);
                continue;
            }

            if (chosen == null)
                continue;

            exitAfterReconcile = false;
            if (chosen.Key is not ("difficulty" or "qte" or "system_mods" or "back" or "reload" or "gm_worker_profiles" or "image_cleanup"))
                notice = "Предпросмотр: изменения ещё не сохранены.";
            if (chosen.Key == "difficulty")
            {
                if (await ShowDifficultySelection(settings)) await SaveDraftAsync();
            }
            else if (chosen.Key == "history")
            {
                settings.AllowHistoryManipulation = !settings.AllowHistoryManipulation;
            }
            else if (chosen.Key == "show_gm")
            {
                settings.ShowGmThoughts = !settings.ShowGmThoughts;
            }
            else if (chosen.Key == "auto_discard")
            {
                settings.AutoDiscardBrokenItems = !settings.AutoDiscardBrokenItems;
            }
            else if (chosen.Key == "qte")
            {
                settings.EnableQteEvents = !settings.EnableQteEvents;
                await SaveDraftAsync();
            }
            else if (chosen.Key == "music")
            {
                settings.MusicEnabled = !settings.MusicEnabled;
                await preview.ApplyAsync();
            }
            else if (chosen.Key == "music_volume")
            {
                settings.MusicVolume = PromptVolume(_loc.T("volume_prompt_music"), settings.MusicVolume);
                await preview.ApplyAsync();
            }
            else if (chosen.Key == "sound")
            {
                settings.SoundEnabled = !settings.SoundEnabled;
                await preview.ApplyAsync();
            }
            else if (chosen.Key == "sound_volume")
            {
                settings.SoundVolume = PromptVolume(_loc.T("volume_prompt_sound"), settings.SoundVolume);
                await preview.ApplyAsync();
                _audioService.PlayCue(AudioCue.MenuSelect);
            }
            else if (chosen.Key == "font_size")
            {
                settings.ConsoleFontSize = PromptFontSize(settings.ConsoleFontSize);
                if (!await preview.ApplyAsync())
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("font_size_apply_note"))}[/]");
                    _inputSource.ReadKey(intercept: true);
                }
            }
            else if (chosen.Key == "gm_cli_launch_command")
            {
                settings.GmCliLaunchCommand = PromptGmCliLaunchCommand(settings.GmCliLaunchCommand);
            }
            else if (chosen.Key == "gm_worker_profiles")
            {
                await ShowGmWorkerBridgeDiagnostics();
            }
            else if (chosen.Key == "system_mods")
            {
                await ShowSystemModsMenu(session, SaveDraftAsync);
            }
            else if (chosen.Key == "image_provider")
            {
                var currentPollKey = settings.PollinationsApiKey;
                var hasPollKey = !string.IsNullOrWhiteSpace(currentPollKey);
                var pollLabel = hasPollKey
                    ? "Pollinations.ai (API ключ задан ✅)"
                    : "Pollinations.ai (нужно ввести API ключ)";

                var providerChoice = ShowSingleChoiceMenu(
                    "Выберите провайдер генерации изображений",
                    new List<MenuChoiceItem>
                    {
                        new("disabled", "Выключено", "Только текстовые описания", "grey"),
                        new("pollinations", pollLabel, "Генерация через Pollinations.ai", "purple")
                    },
                    footer: "Esc — назад",
                    initialIndex: settings.ImageProvider == "pollinations" ? 1 : 0);

                if (providerChoice == null)
                {
                    menuTop = RenderOptionsStaticFrame(settings, notice);
                    RedrawOptionsMenuArea(await BuildOptionsEntriesAsync(session), selectedIndex, menuTop, GetSafeConsoleHeight(), settings, notice);
                    continue;
                }

                if (providerChoice.Key == "pollinations")
                {
                    settings.ImageProvider = "pollinations";

                    // Ask for API key
                    var keyPrompt = hasPollKey
                        ? "[cyan]API ключ Pollinations (Enter = оставить текущий):[/]"
                        : "[cyan]Введите API ключ Pollinations (получить на enter.pollinations.ai):[/]";
                    var newKey = PromptTextInput(keyPrompt, allowEmpty: true, preserveNewlines: false);
                    if (!string.IsNullOrWhiteSpace(newKey))
                        settings.PollinationsApiKey = newKey.Trim();

                    // Ask for model
                    var currentModel = settings.PollinationsImageModel;
                    var modelChoice = ShowSingleChoiceMenu(
                        "Модель изображений",
                        new List<MenuChoiceItem>
                        {
                            new("flux", "flux", "Flux.1 (быстрая, бесплатная)", "purple"),
                            new("zimage", "zimage", "ZImage v2 6B (2x апскейл)", "purple"),
                            new("flux-2-dev", "flux-2-dev", "Flux 2 Dev (высокое качество)", "purple"),
                            new("gptimage", "gptimage", "GPT Image 1 (платная)", "purple"),
                            new("imagen-4", "imagen-4", "Google Imagen 4 (платная)", "purple"),
                            new("custom", "✏ Ввести вручную", null, "yellow")
                        },
                        footer: $"{_loc.T("current_value")}: {currentModel}",
                        initialIndex: 0);

                    if (modelChoice == null)
                    {
                        menuTop = RenderOptionsStaticFrame(settings, notice);
                        RedrawOptionsMenuArea(await BuildOptionsEntriesAsync(session), selectedIndex, menuTop, GetSafeConsoleHeight(), settings, notice);
                        continue;
                    }

                    if (modelChoice.Key == "custom")
                    {
                        var customModel = PromptTextInput("[cyan]Название модели:[/]",
                            defaultValue: currentModel,
                            allowEmpty: false,
                            preserveNewlines: false);
                        settings.PollinationsImageModel = customModel.Trim();
                    }
                    else
                    {
                        settings.PollinationsImageModel = modelChoice.Key;
                    }
                }
                else
                {
                    settings.ImageProvider = "placeholder";
                }
            }
            else if (chosen.Key == "scene_images")
            {
                settings.GenerateSceneImages = !settings.GenerateSceneImages;
            }
            else if (chosen.Key == "image_display")
            {
                settings.ShowImagesInConsole = !settings.ShowImagesInConsole;
            }
            else if (chosen.Key == "no_autodisplay")
            {
                settings.GenerateImagesWithoutDisplay = !settings.GenerateImagesWithoutDisplay;
            }
            else if (chosen.Key == "image_cleanup")
            {
                if (_imageService == null)
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(_loc.T("image_service_unavailable"))}[/]");
                }
                else
                {
                    var confirm = AnsiConsole.Prompt(new ConfirmationPrompt(
                        $"[bold yellow]{Markup.Escape(_loc.T("image_cleanup_confirm"))}[/]")
                    { DefaultValue = false });

                    if (confirm)
                    {
                        var cleanup = _imageService.CleanupExtraImages();
                        AnsiConsole.MarkupLine(string.Format(
                            _loc.T("image_cleanup_done"),
                            cleanup.DeletedSceneImages,
                            cleanup.DeletedEntityImages));
                    }
                }

                _inputSource.ReadKey(intercept: true);
            }
            else if (chosen.Key == "language")
            {
                var lang = settings.Language == "ru" ? "en" : "ru";
                settings.Language = lang;
                await preview.ApplyAsync();
            }
            else if (chosen.Key == "reload")
            {
                try
                {
                    await session.ReloadAsync(async () => { await preview.ApplyAsync(); });
                    return; // Explicit discard; disposal restores accepted effects.
                }
                catch (Exception ex) when (ex is not ConsoleE2EScriptInputException)
                {
                    _logger.LogWarning(ex, "Console settings refresh did not complete.");
                    notice = (reconciliationOutcomeMessage == null ? string.Empty : reconciliationOutcomeMessage + " ")
                        + "Обновление настроек не завершено. Повторите проверку перед продолжением.";
                    await RestoreLastAcceptedEffectsAsync();
                }
            }
            else if (chosen.Key == "back")
            {
                exitAfterReconcile = true;
                if (CanLeave(await SaveDraftAsync())) return;
            }

            if (session.RequiresReload)
            {
                lastWidth = -1;
                continue; // Reconciliation must precede any further canonical read.
            }
            menuTop = RenderOptionsStaticFrame(settings, notice);
            var updatedEntries = await BuildOptionsEntriesAsync(session);
            RedrawOptionsMenuArea(updatedEntries, selectedIndex, menuTop, GetSafeConsoleHeight(), settings, notice);
            WriteOptionsMenuObservation(updatedEntries, selectedIndex, "options-menu", notice);
        }
    }

    private async Task<List<OptionsMenuEntry>> BuildOptionsEntriesAsync(ConsoleSettingsSession session)
    {
        var settings = session.Draft;
        var histStatus = settings.AllowHistoryManipulation ? _loc.T("enabled") : _loc.T("disabled");
        var gmStatus = settings.ShowGmThoughts ? _loc.T("enabled") : _loc.T("disabled");
        var autoDiscardStatus = settings.AutoDiscardBrokenItems ? _loc.T("enabled") : _loc.T("disabled");
        var sceneImgStatus = settings.GenerateSceneImages ? _loc.T("enabled") : _loc.T("disabled");
        var noDisplayStatus = settings.GenerateImagesWithoutDisplay ? _loc.T("enabled") : _loc.T("disabled");
        var qteStatus = settings.EnableQteEvents ? _loc.T("enabled") : _loc.T("disabled");
        var musicStatus = settings.MusicEnabled ? _loc.T("enabled") : _loc.T("disabled");
        var soundStatus = settings.SoundEnabled ? _loc.T("enabled") : _loc.T("disabled");
        var systemMods = await session.ReadModsAsync();
        var systemModsStatus = _systemModService.GetStatusSummary(systemMods);
        var workerProfilesStatus = BuildGmWorkerBridgeProfileSummary(settings);
        var imgDisplay = settings.ShowImagesInConsole ? _loc.T("opt_in_console") : _loc.T("opt_in_viewer");
        var imgProvider = settings.ImageProvider switch
        {
            "pollinations" => $"Pollinations ({settings.PollinationsImageModel})",
            _ => "Выключено"
        };
        var difficultyLabel = settings.Difficulty switch
        {
            "hard" => _loc.T("difficulty_hard"),
            "impossible" => _loc.T("difficulty_impossible"),
            _ => _loc.T("difficulty_normal")
        };
        var difficultyColor = settings.Difficulty switch
        {
            "hard" => "darkorange",
            "impossible" => "red",
            _ => "green"
        };

        return new List<OptionsMenuEntry>
        {
            new("difficulty", $"⚔️ {_loc.T("opt_difficulty")}: [{difficultyColor}]{difficultyLabel}[/]"),
            new("history", $"{_loc.T("opt_history_manipulation")}: [{(histStatus == _loc.T("enabled") ? "green" : "red")}]{histStatus}[/]"),
            new("show_gm", $"{_loc.T("opt_show_gm")}: [{(gmStatus == _loc.T("enabled") ? "green" : "red")}]{gmStatus}[/]"),
            new("auto_discard", $"🗑️ Авто-выброс сломанных: [{(autoDiscardStatus == _loc.T("enabled") ? "green" : "red")}]{autoDiscardStatus}[/]"),
            new("qte", $"🎬 QTE события: [{(qteStatus == _loc.T("enabled") ? "green" : "red")}]{qteStatus}[/]"),
            new("gm_cli_launch_command", $"🌉 {_loc.T("opt_gm_cli_launch_command")}: [yellow]{Markup.Escape(TruncateDiagnosticValue(settings.GmCliLaunchCommand, 56))}[/]"),
            new("gm_worker_profiles", $"🧵 GM worker bridges: [yellow]{Markup.Escape(workerProfilesStatus)}[/]"),
            new("music", $"🎵 {_loc.T("opt_music")}: [{(musicStatus == _loc.T("enabled") ? "green" : "red")}]{musicStatus}[/]"),
            new("music_volume", $"🎚 {_loc.T("opt_music_volume")}: [yellow]{settings.MusicVolume}%[/]"),
            new("sound", $"🔊 {_loc.T("opt_sound")}: [{(soundStatus == _loc.T("enabled") ? "green" : "red")}]{soundStatus}[/]"),
            new("sound_volume", $"🎛 {_loc.T("opt_sound_volume")}: [yellow]{settings.SoundVolume}%[/]"),
            new("font_size", $"🔤 {_loc.T("opt_font_size")}: [yellow]{settings.ConsoleFontSize}[/]"),
            new("system_mods", $"🧩 {_loc.T("opt_system_mods")}: [yellow]{systemModsStatus}[/]"),
            new("image_provider", $"🎨 Генерация изображений: [yellow]{imgProvider}[/]"),
            new("scene_images", $"🖼️ Изображения сцен (ежеходные): [{(sceneImgStatus == _loc.T("enabled") ? "green" : "red")}]{sceneImgStatus}[/]"),
            new("image_display", $"{_loc.T("opt_image_display")}: [yellow]{imgDisplay}[/]"),
            new("no_autodisplay", $"📁 {_loc.T("opt_image_no_autodisplay")}: [{(noDisplayStatus == _loc.T("enabled") ? "green" : "red")}]{noDisplayStatus}[/]"),
            new("image_cleanup", $"🧹 {_loc.T("opt_image_cleanup")}"),
            new("language", $"{_loc.T("opt_language")}: [yellow]{settings.Language.ToUpper()}[/]"),
            new("reload", "Перечитать сохранённые настройки (отменить черновик)"),
            new("back", _loc.T("back"))
        };
    }

    private int RenderOptionsStaticFrame(GameSettings settings, string? notice)
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Rule("[cyan]⚙️ Опции[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Предпросмотр настроек. Esc/Назад — сохранить; перечитать — отменить несохранённое.[/]");
        if (!string.IsNullOrWhiteSpace(notice))
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(notice)}[/]");

        if (settings.GenerateImagesWithoutDisplay)
        {
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("opt_image_no_autodisplay_hint"))}[/]");
            AnsiConsole.WriteLine();
        }

        try
        {
            return Math.Max(0, Console.CursorTop);
        }
        catch
        {
            return 0;
        }
    }

    private void RedrawOptionsMenuArea(IReadOnlyList<OptionsMenuEntry> entries, int selectedIndex, int menuTop, int consoleHeight, GameSettings settings, string? notice)
    {
        var availableRows = Math.Max(6, consoleHeight - menuTop - 4);
        var visibleCount = Math.Max(5, availableRows - 2);
        var startIndex = Math.Max(0, selectedIndex - visibleCount / 2);
        if (startIndex + visibleCount > entries.Count)
            startIndex = Math.Max(0, entries.Count - visibleCount);

        ClearConsoleRegion(menuTop);
        try
        {
            Console.SetCursorPosition(0, menuTop);
        }
        catch
        {
            RenderOptionsStaticFrame(settings, notice);
            return;
        }

        var body = new Grid();
        body.AddColumn(new GridColumn());

        foreach (var (entry, absoluteIndex) in entries
                     .Select((entry, idx) => (entry, idx))
                     .Skip(startIndex)
                     .Take(visibleCount))
        {
            var isSelected = absoluteIndex == selectedIndex;
            var plainLabel = StripMarkup(entry.Label);
            var line = isSelected
                ? $"[black on cyan1 bold]  ➤ {Markup.Escape(plainLabel)}  [/] "
                : $"  {entry.Label}";
            body.AddRow(new Markup(line));
        }

        body.AddRow(new Text(" "));
        body.AddRow(new Markup("[dim]  ↑/↓ или W/S — выбор • Enter — подтвердить • Esc — назад[/]"));
        AnsiConsole.Write(ConsoleLayout.WithHorizontalMargin(body, 2));
    }

    private void WriteOptionsMenuObservation(IReadOnlyList<OptionsMenuEntry> entries, int selectedIndex, string slug, string? notice = null)
    {
        var boundedIndex = entries.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, entries.Count - 1);
        var selectedEntry = boundedIndex >= 0 ? entries[boundedIndex] : null;
        var optionTitles = entries.Select(entry => StripMarkup(entry.Label)).ToArray();
        var selectedOption = selectedEntry is null ? null : StripMarkup(selectedEntry.Label);
        var playerText = selectedOption is null
            ? "Клиентские настройки."
            : $"Выбран пункт настроек: {selectedOption}";

        RecordConsoleObservation(
            ConsoleE2EInputMode.Menu,
            "⚙️ Опции",
            playerText + (string.IsNullOrWhiteSpace(notice) ? string.Empty : " " + notice),
            optionTitles,
            selectedOption,
            slug);
    }

    private int PromptVolume(string title, int currentValue)
    {
        var steps = Enumerable.Range(0, 11)
            .Select(index => index * 10)
            .ToList();
        var labels = steps.Select(value => value == 0 ? _loc.T("volume_off") : $"{value}%").ToList();
        var currentLabel = currentValue == 0 ? _loc.T("volume_off") : $"{currentValue}%";
        var items = labels.Select((label, index) => new MenuChoiceItem(index.ToString(), label)).ToList();
        var selected = ShowSingleChoiceMenu(
            title,
            items,
            footer: $"{_loc.T("current_value")}: {currentLabel}",
            initialIndex: Math.Max(0, labels.IndexOf(currentLabel)),
            enableCompactMode: true);

        if (selected == null)
            return currentValue;

        return steps[int.Parse(selected.Key)];
    }

    private int PromptFontSize(int currentValue)
    {
        var sizes = new[] { 14, 16, 18, 20, 22, 24, 26, 28, 30, 32 };
        var items = sizes.Select((size, index) => new MenuChoiceItem(index.ToString(), $"{size}")).ToList();
        var selected = ShowSingleChoiceMenu(
            _loc.T("font_size_prompt"),
            items,
            footer: $"{_loc.T("current_value")}: {currentValue}",
            initialIndex: Array.IndexOf(sizes, currentValue) is var found && found >= 0 ? found : 0,
            enableCompactMode: true);

        if (selected == null)
            return currentValue;

        return sizes[int.Parse(selected.Key)];
    }

    private string PromptGmCliLaunchCommand(string currentValue)
    {
        var current = string.IsNullOrWhiteSpace(currentValue)
            ? GameSettings.DefaultGmCliLaunchCommand
            : currentValue.Trim();

        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(_loc.T("opt_gm_cli_launch_command"))}[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("gm_cli_launch_command_hint"))}[/]");
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("gm_cli_launch_command_examples"))}[/]");
        AnsiConsole.WriteLine();

        var entered = PromptTextInput($"[cyan]{Markup.Escape(_loc.T("gm_cli_launch_command_prompt"))}[/]",
            defaultValue: current,
            allowEmpty: false,
            preserveNewlines: false).Trim();
        return string.IsNullOrWhiteSpace(entered) ? current : entered;
    }

    private string BuildGmWorkerBridgeProfileSummary(GameSettings settings)
    {
        var profiles = settings.GmWorkerBridgeProfiles;
        if (profiles.Count == 0)
            return "не настроены";

        var enabled = profiles.Count(profile => profile.Enabled);
        var roles = profiles
            .Where(profile => profile.Enabled)
            .Select(profile => profile.Role.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roleSummary = roles.Length == 0 ? "нет активных ролей" : string.Join(", ", roles);
        return $"{enabled}/{profiles.Count} включено; {roleSummary}";
    }

    private async Task ShowGmWorkerBridgeDiagnostics()
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Rule("[cyan]GM worker bridges[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Справочная диагностика профилей. Этот экран ничего не запускает и не меняет.[/]");
        AnsiConsole.WriteLine();

        var profiles = _stateManager.Settings.GmWorkerBridgeProfiles;
        if (profiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Worker-профили не настроены.[/]");
            AnsiConsole.MarkupLine("[dim]Обычный single-GM режим продолжает работать без фоновых worker-процессов.[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Expand();
            table.AddColumn(new TableColumn("[bold]ID[/]").NoWrap());
            table.AddColumn(new TableColumn("[bold]Роль[/]").NoWrap());
            table.AddColumn(new TableColumn("[bold]Состояние[/]").NoWrap());
            table.AddColumn(new TableColumn("[bold]Задачи[/]"));
            table.AddColumn(new TableColumn("[bold]Таймаут[/]").NoWrap());
            table.AddColumn(new TableColumn("[bold]Команда запуска[/]"));
            table.AddColumn(new TableColumn("[bold]Scope[/]"));

            foreach (var profile in profiles)
            {
                var state = profile.Enabled
                    ? "[green]включен[/]"
                    : "[dim]отключен[/]";
                var taskTypes = profile.Permissions.TaskTypes.Count == 0
                    ? "[dim]нет[/]"
                    : Markup.Escape(string.Join(", ", profile.Permissions.TaskTypes));
                var scope = profile.Permissions.ProposalOnly
                    ? "proposal-only"
                    : $"write: {string.Join(", ", profile.Permissions.ProposalWritePaths)}";
                var launch = $"{TruncateDiagnosticValue(profile.LaunchCommand, 64)} | hidden";

                table.AddRow(
                    Markup.Escape(profile.WorkerId),
                    Markup.Escape(profile.Role.ToString()),
                    state,
                    taskTypes,
                    $"{profile.TimeoutSeconds}s",
                    Markup.Escape(launch),
                    Markup.Escape(scope));
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Proposal inbox[/]").RuleStyle("cyan"));
        var proposals = await new GmWorkerProposalInboxService(_fs).ListAsync();
        if (proposals.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Worker proposals пока не сохранены.[/]");
        }
        else
        {
            var proposalTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Expand();
            proposalTable.AddColumn(new TableColumn("[bold]Proposal[/]").NoWrap());
            proposalTable.AddColumn(new TableColumn("[bold]Worker[/]").NoWrap());
            proposalTable.AddColumn(new TableColumn("[bold]Задача[/]").NoWrap());
            proposalTable.AddColumn(new TableColumn("[bold]Режим[/]").NoWrap());
            proposalTable.AddColumn(new TableColumn("[bold]Состояние[/]").NoWrap());
            proposalTable.AddColumn(new TableColumn("[bold]Сводка[/]"));

            foreach (var proposal in proposals.Take(12))
            {
                var state = proposal.IsReadable
                    ? string.IsNullOrWhiteSpace(proposal.ApplyState) ? proposal.Status?.ToString() ?? "readable" : proposal.ApplyState
                    : "unreadable";
                var summary = proposal.IsReadable
                    ? proposal.Summary
                    : proposal.UnreadableReason;
                proposalTable.AddRow(
                    Markup.Escape(proposal.ProposalId),
                    Markup.Escape(proposal.WorkerId),
                    Markup.Escape(proposal.TaskType?.ToString() ?? "unknown"),
                    Markup.Escape(proposal.ReviewMode),
                    Markup.Escape(state),
                    Markup.Escape(TruncateDiagnosticValue(summary, 96)));
            }

            AnsiConsole.Write(proposalTable);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Proposal-файлы принимаются через BOE_WORKER_PROPOSAL_PATH; canonical state применяет только apply gate.[/]");
        _inputSource.ReadKey(intercept: true);
    }

    private Task RefreshAudioPlaybackContextAsync() => RefreshAudioPlaybackContextAsync(_stateManager.Settings);

    private async Task RefreshAudioPlaybackContextAsync(GameSettings settings)
    {
        if (!settings.MusicEnabled || settings.MusicVolume <= 0)
        {
            await _audioService.StopMusicAsync();
            return;
        }

        if (_inGame)
            await _audioService.PlayInGameMusicAsync();
        else
            await _audioService.PlayMainMenuMusicAsync();
    }

    private Task<bool> ShowDifficultySelection(GameSettings settings)
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Rule("[cyan]⚔️ Сложность[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();
        table.AddColumn(new TableColumn("[bold]Уровень[/]").Width(16));
        table.AddColumn(new TableColumn("[bold]Описание[/]"));

        table.AddRow(
            "[green]🟢 Нормальная[/]",
            "[dim]Стандартный баланс. Враги, проверки действий, опыт и лут — по базовым правилам без модификаторов.[/]");
        table.AddRow(
            "[darkorange]🟠 Тяжёлая[/]",
            "[dim]Враги крепче (×1.75 здоровья, ×1.4 урон). Проверки действий сложнее (×1.5 + 5). " +
            "Награды выше: опыт ×2, шанс 50% повысить редкость лута, ×1.5 количество ресурсов.[/]");
        table.AddRow(
            "[red]🔴 Невозможная[/]",
            "[dim]Экстремальный вызов. Враги (×3.5 здоровья, ×2.8 урон). Проверки (×3.0 + 10). " +
            "Легендарные награды: опыт ×4, гарантированное повышение редкости лута + 25% шанс на второе, ×3 ресурсы.[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var difficultyItems = new List<MenuChoiceItem>
        {
            new("normal", "🟢 Нормальная", "Стандартный баланс", "green"),
            new("hard", "🟠 Тяжёлая", "Сильнее враги, больше награды", "darkorange"),
            new("impossible", "🔴 Невозможная", "Экстремальный вызов, легендарные награды", "red"),
            new("back", _loc.T("back"), null, "grey")
        };

        var selected = ShowSingleChoiceMenu(
            "Выберите уровень сложности",
            difficultyItems,
            footer: "Esc — назад",
            initialIndex: settings.Difficulty switch
            {
                "hard" => 1,
                "impossible" => 2,
                _ => 0
            });

        if (selected == null || selected.Key == "back")
            return Task.FromResult(false);

        settings.Difficulty = selected.Key;

        return Task.FromResult(true);
    }

    private async Task ShowSystemModsMenu(ConsoleSettingsSession session, Func<Task<BrowserPreparedWriteResult>> save)
    {
        var selectedIndex = 0;
        while (true)
        {
            SpectreConsoleSafe.Clear();
            var mods = await session.ReadModsAsync();
            var modsDir = _systemModService.GetModsDirectoryPath();

            AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(_loc.T("system_mods_title"))}[/]").RuleStyle("cyan"));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("system_mods_folder_hint"))}: {Markup.Escape(modsDir)}[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("system_mods_manifest_hint"))}[/]");
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("system_mods_warning"))}[/]");
            AnsiConsole.WriteLine();

            if (mods.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("system_mods_none"))}[/]");
                AnsiConsole.WriteLine();
            }
            else
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .Expand();
                table.AddColumn(new TableColumn($"[bold]{Markup.Escape(_loc.T("system_mods_status_header"))}[/]").NoWrap());
                table.AddColumn(new TableColumn($"[bold]{Markup.Escape(_loc.T("system_mods_mod_header"))}[/]"));
                table.AddColumn(new TableColumn($"[bold]{Markup.Escape(_loc.T("system_mods_file_header"))}[/]").NoWrap());
                table.AddColumn(new TableColumn($"[bold]{Markup.Escape(_loc.T("system_mods_description_header"))}[/]"));

                foreach (var mod in mods)
                {
                    var status = mod.Enabled
                        ? $"[green]● {Markup.Escape(_loc.T("system_mods_status_enabled"))}[/]"
                        : $"[dim]○ {Markup.Escape(_loc.T("system_mods_status_disabled"))}[/]";
                    table.AddRow(
                        status,
                        Markup.Escape(mod.Name),
                        $"[dim]{Markup.Escape(mod.FileName)}[/]",
                        string.IsNullOrWhiteSpace(mod.Description) ? "[dim]—[/]" : Markup.Escape(mod.Description));
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }

            var actions = new List<MenuChoiceItem>
            {
                new("configure", _loc.T("system_mods_configure"), "Включить или отключить моды", "cyan1"),
                new("open_folder", _loc.T("system_mods_open_folder"), "Открыть каталог mods/", "yellow"),
                new("back", _loc.T("back"), null, "grey")
            };

            var choice = ShowSingleChoiceMenu(
                _loc.T("system_mods_title"),
                actions,
                footer: "Esc — назад",
                initialIndex: selectedIndex);

            if (choice == null || choice.Key == "back")
                return;

            selectedIndex = actions.FindIndex(item => item.Key == choice.Key);
            if (choice.Key == "open_folder")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = modsDir,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(modsDir)}[/]");
                }

                _inputSource.ReadKey(intercept: true);
                continue;
            }

            if (mods.Count == 0)
                continue;

            var selectedLabels = ShowMultiChoiceMenu(
                _loc.T("system_mods_select"),
                mods.Select(mod => new MenuChoiceItem(
                    mod.FileName,
                    $"{mod.Name} ({mod.FileName})",
                    string.IsNullOrWhiteSpace(mod.Description) ? null : mod.Description,
                    mod.Enabled ? "green" : "grey")).ToList(),
                new HashSet<string>(mods.Where(mod => mod.Enabled).Select(mod => mod.FileName), StringComparer.OrdinalIgnoreCase),
                _loc.T("system_mods_select_hint"));

            if (selectedLabels == null)
                continue;

            session.Draft.EnabledSystemMods = selectedLabels
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = await save();
            var color = result.Disposition == BrowserPreparedWriteDisposition.Committed ? "green" : "yellow";
            AnsiConsole.MarkupLine($"[{color}]{Markup.Escape(result.Message)}[/]");
            _inputSource.ReadKey(intercept: true);
            if (session.RequiresReload) return;
        }
    }

    private MenuChoiceItem? ShowSingleChoiceMenu(
        string title,
        IReadOnlyList<MenuChoiceItem> items,
        string? footer = null,
        int initialIndex = 0,
        bool enableCompactMode = false)
    {
        if (items.Count == 0)
            return null;

        var selectedIndex = Math.Clamp(initialIndex, 0, items.Count - 1);
        var headerTop = RenderGenericMenuStaticFrame(title, footer);
        RedrawSingleChoiceMenuArea(items, selectedIndex, headerTop, GetSafeConsoleHeight(), enableCompactMode);
        var observationSlug = BuildSingleChoiceMenuObservationSlug(title);
        WriteSingleChoiceMenuObservation(title, items, selectedIndex, observationSlug);

        while (true)
        {
            var key = _inputSource.ReadKey(intercept: true);
            var selectionChanged = false;
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % items.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.Enter:
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    return items[selectedIndex];
                default:
                    if (TryMapMenuNumberSelection(key, items.Count, out var numberIndex))
                    {
                        selectedIndex = numberIndex;
                        selectionChanged = true;
                    }

                    break;
            }

            if (selectionChanged)
            {
                RedrawSingleChoiceMenuArea(items, selectedIndex, headerTop, GetSafeConsoleHeight(), enableCompactMode);
                WriteSingleChoiceMenuObservation(title, items, selectedIndex, observationSlug);
            }
        }
    }

    private void WriteSingleChoiceMenuObservation(
        string title,
        IReadOnlyList<MenuChoiceItem> items,
        int selectedIndex,
        string slug)
    {
        var boundedIndex = items.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, items.Count - 1);
        var selectedItem = boundedIndex >= 0 ? items[boundedIndex] : null;
        var optionTitles = items.Select(item => StripMarkup(item.Label)).ToArray();
        var selectedOption = selectedItem is null ? null : StripMarkup(selectedItem.Label);
        var playerText = BuildSingleChoiceMenuObservationText(title, items, boundedIndex);

        RecordConsoleObservation(
            ConsoleE2EInputMode.Menu,
            StripMarkup(title),
            playerText,
            optionTitles,
            selectedOption,
            slug);
    }

    private string BuildSingleChoiceMenuObservationText(
        string title,
        IReadOnlyList<MenuChoiceItem> items,
        int selectedIndex)
    {
        var builder = new StringBuilder();
        builder.AppendLine(StripMarkup(title));
        builder.AppendLine("Доступные пункты:");

        for (var index = 0; index < items.Count; index++)
        {
            var label = StripMarkup(items[index].Label);
            var marker = index == selectedIndex ? ">" : " ";
            var itemDescription = items[index].Description;
            var description = string.IsNullOrWhiteSpace(itemDescription)
                ? string.Empty
                : $" — {StripMarkup(itemDescription)}";
            builder.AppendLine($"{marker} {index + 1}. {label}{description}");
        }

        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            builder.AppendLine();
            builder.AppendLine($"Выбран пункт: {StripMarkup(items[selectedIndex].Label)}");
        }

        return builder.ToString().Trim();
    }

    private static string BuildSingleChoiceMenuObservationSlug(string title)
    {
        var plainTitle = StripMarkup(title);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainTitle)))
            .Substring(0, 8)
            .ToLowerInvariant();
        return $"single-choice-menu-{hash}";
    }

    private HashSet<string>? ShowMultiChoiceMenu(
        string title,
        IReadOnlyList<MenuChoiceItem> items,
        HashSet<string> initiallySelected,
        string instructions)
    {
        if (items.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var selectedIndex = 0;
        var selected = new HashSet<string>(initiallySelected, StringComparer.OrdinalIgnoreCase);
        var headerTop = RenderGenericMenuStaticFrame(title, instructions);
        RedrawMultiChoiceMenuArea(items, selectedIndex, selected, headerTop, GetSafeConsoleHeight());

        while (true)
        {
            var key = _inputSource.ReadKey(intercept: true);
            var changed = false;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                    changed = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % items.Count;
                    changed = true;
                    break;
                case ConsoleKey.Spacebar:
                    if (!selected.Add(items[selectedIndex].Key))
                        selected.Remove(items[selectedIndex].Key);
                    changed = true;
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    break;
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.Enter:
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    return selected;
            }

            if (changed)
                RedrawMultiChoiceMenuArea(items, selectedIndex, selected, headerTop, GetSafeConsoleHeight());
        }
    }

    private int RenderGenericMenuStaticFrame(string title, string? footer)
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(title)}[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(footer))
        {
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(footer)}[/]");
            AnsiConsole.WriteLine();
        }

        try
        {
            return Math.Max(0, Console.CursorTop);
        }
        catch
        {
            return 0;
        }
    }

    private void RedrawSingleChoiceMenuArea(
        IReadOnlyList<MenuChoiceItem> items,
        int selectedIndex,
        int menuTop,
        int consoleHeight,
        bool enableCompactMode)
    {
        var availableRows = Math.Max(6, consoleHeight - menuTop - 4);
        var compact = enableCompactMode && availableRows < 16;
        var perItemRows = compact ? 1 : 3;
        var visibleCount = Math.Max(5, availableRows / perItemRows);
        var startIndex = Math.Max(0, selectedIndex - visibleCount / 2);
        if (startIndex + visibleCount > items.Count)
            startIndex = Math.Max(0, items.Count - visibleCount);

        ClearConsoleRegion(menuTop);
        try
        {
            Console.SetCursorPosition(0, menuTop);
        }
        catch
        {
            RenderGenericMenuStaticFrame("", null);
            return;
        }

        var body = new Grid();
        body.AddColumn(new GridColumn());

        foreach (var (item, absoluteIndex) in items.Select((item, idx) => (item, idx)).Skip(startIndex).Take(visibleCount))
        {
            var isSelected = absoluteIndex == selectedIndex;
            var titleMarkup = isSelected
                ? $"[black on cyan1 bold]  ➤ {Markup.Escape(item.Label)}  [/] "
                : $"  [{item.AccentColor}]◆[/] {Markup.Escape(item.Label)}";
            body.AddRow(new Markup(titleMarkup));

            if (!compact && !string.IsNullOrWhiteSpace(item.Description))
            {
                var descMarkup = isSelected
                    ? $"[black on cyan1]     {Markup.Escape(item.Description)}[/]"
                    : $"[dim]     {Markup.Escape(item.Description)}[/]";
                body.AddRow(new Markup(descMarkup));
                body.AddRow(new Text(" "));
            }
        }

        body.AddRow(new Text(" "));
        body.AddRow(new Markup(compact
            ? "[dim]  ↑/↓ • W/S • Enter • Esc[/]"
            : "[dim]  ↑/↓ или W/S — выбор • Enter — подтвердить • Esc — назад[/]"));
        AnsiConsole.Write(ConsoleLayout.WithHorizontalMargin(body, 2));
    }

    private void RedrawMultiChoiceMenuArea(
        IReadOnlyList<MenuChoiceItem> items,
        int selectedIndex,
        HashSet<string> selected,
        int menuTop,
        int consoleHeight)
    {
        var availableRows = Math.Max(6, consoleHeight - menuTop - 4);
        var visibleCount = Math.Max(5, availableRows - 2);
        var startIndex = Math.Max(0, selectedIndex - visibleCount / 2);
        if (startIndex + visibleCount > items.Count)
            startIndex = Math.Max(0, items.Count - visibleCount);

        ClearConsoleRegion(menuTop);
        try
        {
            Console.SetCursorPosition(0, menuTop);
        }
        catch
        {
            return;
        }

        var body = new Grid();
        body.AddColumn(new GridColumn());

        foreach (var (item, absoluteIndex) in items.Select((item, idx) => (item, idx)).Skip(startIndex).Take(visibleCount))
        {
            var isSelected = absoluteIndex == selectedIndex;
            var isChecked = selected.Contains(item.Key);
            var marker = isChecked ? "[green]●[/]" : "[dim]○[/]";
            var plainLabel = StripMarkup(item.Label);
            var line = isSelected
                ? $"[black on cyan1 bold]  ➤ [/]{marker} [black on cyan1 bold]{Markup.Escape(plainLabel)}[/]"
                : $"  {marker} {Markup.Escape(item.Label)}";
            body.AddRow(new Markup(line));
        }

        body.AddRow(new Text(" "));
        body.AddRow(new Markup("[dim]  ↑/↓ или W/S — выбор • Space — включить/выключить • Enter — сохранить • Esc — назад[/]"));
        AnsiConsole.Write(ConsoleLayout.WithHorizontalMargin(body, 2));
    }

    private static string StripMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var result = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var ch in text)
        {
            if (ch == '[')
            {
                depth++;
                continue;
            }

            if (ch == ']' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
                result.Append(ch);
        }

        return result.ToString();
    }

    /// <summary>
    /// Writes game_state/core/game_settings.json so the GM agent can read difficulty flags.
    /// Maps client difficulty setting to Context.gameSettings.hardMode / impossibleMode.
    /// </summary>
    private async Task<bool> WriteGameSettingsForGm()
    {
        var session = await ConsoleSettingsSession.OpenAsync(_fs, _stateManager, _systemModService);
        async Task RefreshAcceptedSettingsAsync()
        {
            _loc.CurrentLanguage = _stateManager.Settings.Language;
            _consoleAppearance.ApplyConfiguredFontSize();
            await _audioService.ApplySettingsAsync();
        }
        if (await session.TryAcceptSynchronizedSettingsAsync())
        {
            try { await RefreshAcceptedSettingsAsync(); return true; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Confirmed settings require console refresh follow-up.");
                AnsiConsole.MarkupLine("[yellow]Сохранённые настройки подтверждены, но обновление консоли требует проверки.[/]");
                return false;
            }
        }
        var result = await session.SaveAsync(RefreshAcceptedSettingsAsync);
        if (result.Disposition == BrowserPreparedWriteDisposition.Committed && !result.NeedsFollowUp) return true;
        _logger.LogWarning("Console settings synchronization result: {Disposition}; follow-up={NeedsFollowUp}; {Message}",
            result.Disposition, result.NeedsFollowUp, result.Message);
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(result.Message)}[/]");
        return false;
    }

    private async Task<bool> InGameOptionsMenu()
    {
        var choice = ShowSingleChoiceMenu(
            _loc.T("in_game_options"),
            new List<MenuChoiceItem>
            {
                new("save", _loc.T("save_game"), "Создать сохранение текущего цикла", "cyan1"),
                new("load", _loc.T("load_game_menu"), "Загрузить существующее сохранение", "cyan1"),
                new("options", _loc.T("options"), "Открыть клиентские настройки", "yellow"),
                new("exit", _loc.T("exit_to_menu"), "Вернуться в главное меню", "red"),
                new("back", _loc.T("back"), null, "grey")
            },
            footer: "Esc — назад",
            initialIndex: 0);

        if (choice == null || choice.Key == "back")
            return true; // Back

        if (choice.Key == "save")
        {
            var saveName = PromptTextInput("[cyan]Название сохранения:[/]",
                defaultValue: $"save_turn{_gameLoop.TurnNumber}",
                allowEmpty: false,
                preserveNewlines: false);

            var desc = PromptTextInput("[cyan]Описание (необязательно):[/]",
                allowEmpty: true,
                preserveNewlines: true);

            var ok = await _saveLoad.SaveGameAsync(saveName, desc, turnNumber: _gameLoop.TurnNumber);
            AnsiConsole.MarkupLine(ok ? $"[green]{_loc.T("save_success")}[/]" : $"[red]{_loc.T("save_failed")}[/]");
            _inputSource.ReadKey(intercept: true);
            return true;
        }

        if (choice.Key == "load")
        {
            await LoadGameFlow();
            return true;
        }

        if (choice.Key == "options")
        {
            await OptionsMenu();
            return true;
        }

        if (choice.Key == "exit")
            return false;

        return true;
    }

    private void ShowAbout()
    {
        SpectreConsoleSafe.Clear();
        var panel = new Panel(new Markup(_loc.T("about_text")))
        {
            Header = new PanelHeader(" ℹ️ ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(4, 2)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
        _inputSource.ReadKey(intercept: true);
    }
}
