using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private async Task OptionsMenu()
    {
        var selectedIndex = 0;
        var lastWidth = -1;
        var lastHeight = -1;
        var menuTop = 0;

        while (true)
        {
            var entries = await BuildOptionsEntriesAsync();
            if (selectedIndex >= entries.Count)
                selectedIndex = Math.Max(0, entries.Count - 1);

            var currentWidth = GetSafeConsoleWidth();
            var currentHeight = GetSafeConsoleHeight();
            if (currentWidth != lastWidth || currentHeight != lastHeight)
            {
                menuTop = RenderOptionsStaticFrame();
                RedrawOptionsMenuArea(entries, selectedIndex, menuTop, currentHeight);
                WriteOptionsMenuObservation(entries, selectedIndex, "options-menu");
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
                    await _stateManager.SaveSettingsAsync();
                    return;
                case ConsoleKey.Enter:
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    chosen = entries[selectedIndex];
                    break;
            }

            if (selectionChanged)
            {
                RedrawOptionsMenuArea(entries, selectedIndex, menuTop, currentHeight);
                WriteOptionsMenuObservation(entries, selectedIndex, "options-menu");
                continue;
            }

            if (chosen == null)
                continue;

            if (chosen.Key == "difficulty")
            {
                await ShowDifficultySelection();
            }
            else if (chosen.Key == "history")
            {
                _stateManager.Settings.AllowHistoryManipulation = !_stateManager.Settings.AllowHistoryManipulation;
            }
            else if (chosen.Key == "show_gm")
            {
                _stateManager.Settings.ShowGmThoughts = !_stateManager.Settings.ShowGmThoughts;
            }
            else if (chosen.Key == "auto_discard")
            {
                _stateManager.Settings.AutoDiscardBrokenItems = !_stateManager.Settings.AutoDiscardBrokenItems;
            }
            else if (chosen.Key == "qte")
            {
                _stateManager.Settings.EnableQteEvents = !_stateManager.Settings.EnableQteEvents;
                await WriteGameSettingsForGm();
            }
            else if (chosen.Key == "music")
            {
                _stateManager.Settings.MusicEnabled = !_stateManager.Settings.MusicEnabled;
                await _audioService.ApplySettingsAsync();
                await RefreshAudioPlaybackContextAsync();
            }
            else if (chosen.Key == "music_volume")
            {
                _stateManager.Settings.MusicVolume = PromptVolume(_loc.T("volume_prompt_music"), _stateManager.Settings.MusicVolume);
                await _audioService.ApplySettingsAsync();
                await RefreshAudioPlaybackContextAsync();
            }
            else if (chosen.Key == "sound")
            {
                _stateManager.Settings.SoundEnabled = !_stateManager.Settings.SoundEnabled;
                await _audioService.ApplySettingsAsync();
            }
            else if (chosen.Key == "sound_volume")
            {
                _stateManager.Settings.SoundVolume = PromptVolume(_loc.T("volume_prompt_sound"), _stateManager.Settings.SoundVolume);
                await _audioService.ApplySettingsAsync();
                _audioService.PlayCue(AudioCue.MenuSelect);
            }
            else if (chosen.Key == "font_size")
            {
                _stateManager.Settings.ConsoleFontSize = PromptFontSize(_stateManager.Settings.ConsoleFontSize);
                if (!_consoleAppearance.TryApplyFontSize(_stateManager.Settings.ConsoleFontSize))
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(_loc.T("font_size_apply_note"))}[/]");
                    _inputSource.ReadKey(intercept: true);
                }
            }
            else if (chosen.Key == "gm_cli_launch_command")
            {
                _stateManager.Settings.GmCliLaunchCommand = PromptGmCliLaunchCommand(_stateManager.Settings.GmCliLaunchCommand);
            }
            else if (chosen.Key == "system_mods")
            {
                await ShowSystemModsMenu();
            }
            else if (chosen.Key == "image_provider")
            {
                var currentPollKey = _stateManager.Settings.PollinationsApiKey;
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
                    initialIndex: _stateManager.Settings.ImageProvider == "pollinations" ? 1 : 0);

                if (providerChoice == null)
                {
                    menuTop = RenderOptionsStaticFrame();
                    RedrawOptionsMenuArea(await BuildOptionsEntriesAsync(), selectedIndex, menuTop, GetSafeConsoleHeight());
                    continue;
                }

                if (providerChoice.Key == "pollinations")
                {
                    _stateManager.Settings.ImageProvider = "pollinations";

                    // Ask for API key
                    var keyPrompt = hasPollKey
                        ? "[cyan]API ключ Pollinations (Enter = оставить текущий):[/]"
                        : "[cyan]Введите API ключ Pollinations (получить на enter.pollinations.ai):[/]";
                    var newKey = PromptTextInput(keyPrompt, allowEmpty: true, preserveNewlines: false);
                    if (!string.IsNullOrWhiteSpace(newKey))
                        _stateManager.Settings.PollinationsApiKey = newKey.Trim();

                    // Ask for model
                    var currentModel = _stateManager.Settings.PollinationsImageModel;
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
                        menuTop = RenderOptionsStaticFrame();
                        RedrawOptionsMenuArea(await BuildOptionsEntriesAsync(), selectedIndex, menuTop, GetSafeConsoleHeight());
                        continue;
                    }

                    if (modelChoice.Key == "custom")
                    {
                        var customModel = PromptTextInput("[cyan]Название модели:[/]",
                            defaultValue: currentModel,
                            allowEmpty: false,
                            preserveNewlines: false);
                        _stateManager.Settings.PollinationsImageModel = customModel.Trim();
                    }
                    else
                    {
                        _stateManager.Settings.PollinationsImageModel = modelChoice.Key;
                    }
                }
                else
                {
                    _stateManager.Settings.ImageProvider = "placeholder";
                }
            }
            else if (chosen.Key == "scene_images")
            {
                _stateManager.Settings.GenerateSceneImages = !_stateManager.Settings.GenerateSceneImages;
            }
            else if (chosen.Key == "image_display")
            {
                _stateManager.Settings.ShowImagesInConsole = !_stateManager.Settings.ShowImagesInConsole;
            }
            else if (chosen.Key == "no_autodisplay")
            {
                _stateManager.Settings.GenerateImagesWithoutDisplay = !_stateManager.Settings.GenerateImagesWithoutDisplay;
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
                var lang = _stateManager.Settings.Language == "ru" ? "en" : "ru";
                _stateManager.Settings.Language = lang;
                _loc.CurrentLanguage = lang;
            }
            else if (chosen.Key == "back")
            {
                await _stateManager.SaveSettingsAsync();
                return;
            }

            menuTop = RenderOptionsStaticFrame();
            RedrawOptionsMenuArea(await BuildOptionsEntriesAsync(), selectedIndex, menuTop, GetSafeConsoleHeight());
        }
    }

    private async Task<List<OptionsMenuEntry>> BuildOptionsEntriesAsync()
    {
        var histStatus = _stateManager.Settings.AllowHistoryManipulation ? _loc.T("enabled") : _loc.T("disabled");
        var gmStatus = _stateManager.Settings.ShowGmThoughts ? _loc.T("enabled") : _loc.T("disabled");
        var autoDiscardStatus = _stateManager.Settings.AutoDiscardBrokenItems ? _loc.T("enabled") : _loc.T("disabled");
        var sceneImgStatus = _stateManager.Settings.GenerateSceneImages ? _loc.T("enabled") : _loc.T("disabled");
        var noDisplayStatus = _stateManager.Settings.GenerateImagesWithoutDisplay ? _loc.T("enabled") : _loc.T("disabled");
        var qteStatus = _stateManager.Settings.EnableQteEvents ? _loc.T("enabled") : _loc.T("disabled");
        var musicStatus = _stateManager.Settings.MusicEnabled ? _loc.T("enabled") : _loc.T("disabled");
        var soundStatus = _stateManager.Settings.SoundEnabled ? _loc.T("enabled") : _loc.T("disabled");
        var systemMods = await _systemModService.GetAvailableModsAsync(includeContent: false);
        var systemModsStatus = _systemModService.GetStatusSummary(systemMods);
        var imgDisplay = _stateManager.Settings.ShowImagesInConsole ? _loc.T("opt_in_console") : _loc.T("opt_in_viewer");
        var imgProvider = _stateManager.Settings.ImageProvider switch
        {
            "pollinations" => $"Pollinations ({_stateManager.Settings.PollinationsImageModel})",
            _ => "Выключено"
        };
        var difficultyLabel = _stateManager.Settings.Difficulty switch
        {
            "hard" => _loc.T("difficulty_hard"),
            "impossible" => _loc.T("difficulty_impossible"),
            _ => _loc.T("difficulty_normal")
        };
        var difficultyColor = _stateManager.Settings.Difficulty switch
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
            new("gm_cli_launch_command", $"🌉 {_loc.T("opt_gm_cli_launch_command")}: [yellow]{Markup.Escape(TruncateDiagnosticValue(_stateManager.Settings.GmCliLaunchCommand, 56))}[/]"),
            new("music", $"🎵 {_loc.T("opt_music")}: [{(musicStatus == _loc.T("enabled") ? "green" : "red")}]{musicStatus}[/]"),
            new("music_volume", $"🎚 {_loc.T("opt_music_volume")}: [yellow]{_stateManager.Settings.MusicVolume}%[/]"),
            new("sound", $"🔊 {_loc.T("opt_sound")}: [{(soundStatus == _loc.T("enabled") ? "green" : "red")}]{soundStatus}[/]"),
            new("sound_volume", $"🎛 {_loc.T("opt_sound_volume")}: [yellow]{_stateManager.Settings.SoundVolume}%[/]"),
            new("font_size", $"🔤 {_loc.T("opt_font_size")}: [yellow]{_stateManager.Settings.ConsoleFontSize}[/]"),
            new("system_mods", $"🧩 {_loc.T("opt_system_mods")}: [yellow]{systemModsStatus}[/]"),
            new("image_provider", $"🎨 Генерация изображений: [yellow]{imgProvider}[/]"),
            new("scene_images", $"🖼️ Изображения сцен (ежеходные): [{(sceneImgStatus == _loc.T("enabled") ? "green" : "red")}]{sceneImgStatus}[/]"),
            new("image_display", $"{_loc.T("opt_image_display")}: [yellow]{imgDisplay}[/]"),
            new("no_autodisplay", $"📁 {_loc.T("opt_image_no_autodisplay")}: [{(noDisplayStatus == _loc.T("enabled") ? "green" : "red")}]{noDisplayStatus}[/]"),
            new("image_cleanup", $"🧹 {_loc.T("opt_image_cleanup")}"),
            new("language", $"{_loc.T("opt_language")}: [yellow]{_stateManager.Settings.Language.ToUpper()}[/]"),
            new("back", _loc.T("back"))
        };
    }

    private int RenderOptionsStaticFrame()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]⚙️ Опции[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        if (_stateManager.Settings.GenerateImagesWithoutDisplay)
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

    private void RedrawOptionsMenuArea(IReadOnlyList<OptionsMenuEntry> entries, int selectedIndex, int menuTop, int consoleHeight)
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
            RenderOptionsStaticFrame();
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

    private void WriteOptionsMenuObservation(IReadOnlyList<OptionsMenuEntry> entries, int selectedIndex, string slug)
    {
        if (_inputSource is not ConsoleE2EScriptedInputSource scriptedInput)
            return;

        var boundedIndex = entries.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, entries.Count - 1);
        var selectedEntry = boundedIndex >= 0 ? entries[boundedIndex] : null;
        var optionTitles = entries.Select(entry => StripMarkup(entry.Label)).ToArray();
        var selectedOption = selectedEntry is null ? null : StripMarkup(selectedEntry.Label);
        var playerText = selectedOption is null
            ? "Клиентские настройки."
            : $"Выбран пункт настроек: {selectedOption}";

        scriptedInput.WriteObservation(
            ConsoleE2EInputMode.Menu,
            "⚙️ Опции",
            playerText,
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
        var current = string.IsNullOrWhiteSpace(currentValue) ? "gemini" : currentValue.Trim();

        AnsiConsole.Clear();
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

    private async Task RefreshAudioPlaybackContextAsync()
    {
        if (!_stateManager.Settings.MusicEnabled || _stateManager.Settings.MusicVolume <= 0)
        {
            await _audioService.StopMusicAsync();
            return;
        }

        if (_inGame)
            await _audioService.PlayInGameMusicAsync();
        else
            await _audioService.PlayMainMenuMusicAsync();
    }

    private async Task ShowDifficultySelection()
    {
        AnsiConsole.Clear();
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
            initialIndex: _stateManager.Settings.Difficulty switch
            {
                "hard" => 1,
                "impossible" => 2,
                _ => 0
            });

        if (selected == null || selected.Key == "back")
            return;

        _stateManager.Settings.Difficulty = selected.Key;

        // Persist to game_state so the GM agent reads it
        await WriteGameSettingsForGm();
    }

    private async Task ShowSystemModsMenu()
    {
        var selectedIndex = 0;
        while (true)
        {
            AnsiConsole.Clear();
            var mods = await _systemModService.GetAvailableModsAsync(includeContent: false);
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

            _stateManager.Settings.EnabledSystemMods = selectedLabels
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _stateManager.SaveSettingsAsync();
            await WriteGameSettingsForGm();

            AnsiConsole.MarkupLine($"[green]{Markup.Escape(_loc.T("system_mods_saved"))}[/]");
            _inputSource.ReadKey(intercept: true);
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
            }

            if (selectionChanged)
                RedrawSingleChoiceMenuArea(items, selectedIndex, headerTop, GetSafeConsoleHeight(), enableCompactMode);
        }
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
        AnsiConsole.Clear();
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
    private async Task WriteGameSettingsForGm()
    {
        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        var activeMods = (await _systemModService.GetAvailableModsAsync(includeContent: false))
            .Where(mod => mod.Enabled)
            .Select(mod => new
            {
                mod.FileName,
                mod.ModId,
                mod.Name
            })
            .ToArray();

        var gameSettings = new
        {
            hardMode = _stateManager.Settings.Difficulty == "hard",
            impossibleMode = _stateManager.Settings.Difficulty == "impossible",
            difficulty = _stateManager.Settings.Difficulty,
            qteEventsEnabled = _stateManager.Settings.EnableQteEvents,
            enabledSystemMods = activeMods,
            _lastUpdated = DateTime.UtcNow.ToString("o")
        };
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json",
            JsonSerializer.Serialize(gameSettings, JsonOpts));
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
        AnsiConsole.Clear();
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

