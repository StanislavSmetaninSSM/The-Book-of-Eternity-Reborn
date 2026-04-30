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
    private async Task ShowMainMenu()
    {
        await _audioService.PlayMainMenuMusicAsync();
        var options = await BuildMainMenuOptionsAsync();
        var selectedIndex = 0;
        var lastWidth = -1;
        var lastHeight = -1;
        var layout = MainMenuLayoutMode.Medium;
        var menuTop = 0;
        bool previousCursorVisible = false;
        var cursorVisibilityCaptured = false;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                previousCursorVisible = Console.CursorVisible;
                cursorVisibilityCaptured = true;
                Console.CursorVisible = false;
            }
            catch
            {
                cursorVisibilityCaptured = false;
            }
        }

        while (_isRunning)
        {
            var currentWidth = GetSafeConsoleWidth();
            var currentHeight = GetSafeConsoleHeight();
            if (currentWidth != lastWidth || currentHeight != lastHeight)
            {
                layout = GetMainMenuLayoutMode(currentWidth, currentHeight);
                menuTop = RenderMainMenuStaticFrame(options, selectedIndex, layout);
                RedrawMainMenuMenuArea(options, selectedIndex, layout, menuTop);
                lastWidth = currentWidth;
                lastHeight = currentHeight;
            }

            var key = Console.ReadKey(true);
            var selectionChanged = false;
            MainMenuOption? chosen = null;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selectedIndex = (selectedIndex + 1) % options.Count;
                    selectionChanged = true;
                    break;
                case ConsoleKey.Enter:
                    _audioService.PlayCue(AudioCue.MenuSelect);
                    chosen = options[selectedIndex];
                    break;
                default:
                    if (TryMapMenuNumberSelection(key, options.Count, out var numericIndex))
                    {
                        selectedIndex = numericIndex;
                        selectionChanged = true;
                    }
                    break;
            }

            if (selectionChanged)
            {
                RedrawMainMenuMenuArea(options, selectedIndex, layout, menuTop);
                continue;
            }

            if (chosen == null)
                continue;

            try
            {
                if (cursorVisibilityCaptured)
                    Console.CursorVisible = previousCursorVisible;
            }
            catch
            {
                // Ignore cursor restore issues on exotic hosts.
            }

            if (chosen.Key == "continue_game")
            {
                await ContinueCurrentSessionFlow();
                return;
            }

            if (chosen.Key == "new_game")
            {
                await NewGameFlow();
                return;
            }

            if (chosen.Key == "load_game")
            {
                await LoadGameFlow();
                return;
            }

            if (chosen.Key == "options")
            {
                await OptionsMenu();
                await _audioService.PlayMainMenuMusicAsync();
                options = await BuildMainMenuOptionsAsync();
                if (selectedIndex >= options.Count)
                    selectedIndex = Math.Max(0, options.Count - 1);
                try
                {
                    if (cursorVisibilityCaptured)
                        Console.CursorVisible = false;
                }
                catch
                {
                    // Ignore cursor visibility failures.
                }

                lastWidth = -1;
                lastHeight = -1;
                continue;
            }

            if (chosen.Key == "about")
            {
                ShowAbout();
                options = await BuildMainMenuOptionsAsync();
                if (selectedIndex >= options.Count)
                    selectedIndex = Math.Max(0, options.Count - 1);
                try
                {
                    if (cursorVisibilityCaptured)
                        Console.CursorVisible = false;
                }
                catch
                {
                    // Ignore cursor visibility failures.
                }

                lastWidth = -1;
                lastHeight = -1;
                continue;
            }

            if (chosen.Key == "exit")
            {
                await _audioService.StopAllAsync();
                _isRunning = false;
                return;
            }
        }

        try
        {
            if (cursorVisibilityCaptured)
                Console.CursorVisible = previousCursorVisible;
        }
        catch
        {
            // Ignore cursor restore failures on shutdown.
        }
    }

    private int RenderMainMenuStaticFrame(IReadOnlyList<MainMenuOption> options, int selectedIndex, MainMenuLayoutMode layout)
    {
        AnsiConsole.Clear();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildMainMenuHero(layout));
        try
        {
            return Math.Max(0, Console.CursorTop);
        }
        catch
        {
            return 0;
        }
    }

    private Spectre.Console.Rendering.IRenderable BuildMainMenuHero(MainMenuLayoutMode layout)
    {
        var sideMargin = layout == MainMenuLayoutMode.VeryCompact ? 1 : 2;
        var hero = new Grid();
        hero.AddColumn(new GridColumn());
        hero.AddRow(BuildMainMenuTitle(layout));
        hero.AddRow(ConsoleLayout.WithHorizontalMargin(
            new Rule("[bold cyan]✦ Возрождение ✦[/]").RuleStyle("cyan").Centered(),
            sideMargin));
        hero.AddRow(ConsoleLayout.WithHorizontalMargin(
            new Markup($"[italic grey]{Markup.Escape(_loc.T("main_menu_tagline"))}[/]"),
            sideMargin));

        if (layout == MainMenuLayoutMode.VeryCompact)
        {
            hero.AddRow(new Text(" "));
            hero.AddRow(ConsoleLayout.WithHorizontalMargin(BuildMainMenuStatusRenderable(layout), sideMargin));
            hero.AddRow(new Text(" "));
            return hero;
        }

        if (layout is MainMenuLayoutMode.Medium or MainMenuLayoutMode.Wide)
        {
            var introPanel = new Panel(new Markup(Markup.Escape(_loc.T("main_menu_intro_body"))))
            {
                Header = new PanelHeader($" ✨ {Markup.Escape(_loc.T("main_menu_intro_title"))} ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            };

            hero.AddRow(new Text(" "));
            hero.AddRow(ConsoleLayout.WithHorizontalMargin(introPanel, sideMargin));
        }

        hero.AddRow(new Text(" "));
        hero.AddRow(ConsoleLayout.WithHorizontalMargin(BuildMainMenuStatusRenderable(layout), sideMargin));
        hero.AddRow(new Text(" "));
        return hero;
    }

    private Spectre.Console.Rendering.IRenderable BuildMainMenuStatusRenderable(MainMenuLayoutMode layout)
    {
        var musicSummary = _stateManager.Settings.MusicEnabled
            ? $"{_stateManager.Settings.MusicVolume}%"
            : _loc.T("disabled");
        var soundSummary = _stateManager.Settings.SoundEnabled
            ? $"{_stateManager.Settings.SoundVolume}%"
            : _loc.T("disabled");

        if (layout == MainMenuLayoutMode.VeryCompact)
        {
            var compact = $"[grey]{Markup.Escape(_loc.T("opt_language"))}:[/] [yellow]{Markup.Escape(_stateManager.Settings.Language.ToUpperInvariant())}[/]  " +
                          $"[grey]{Markup.Escape(_loc.T("opt_difficulty"))}:[/] [green]{Markup.Escape(GetDifficultyLabel())}[/]  " +
                          $"[grey]{Markup.Escape(_loc.T("opt_music"))}:[/] [yellow]{Markup.Escape(musicSummary)}[/]  " +
                          $"[grey]{Markup.Escape(_loc.T("opt_sound"))}:[/] [yellow]{Markup.Escape(soundSummary)}[/]  " +
                          $"[grey]{Markup.Escape(_loc.T("opt_font_size"))}:[/] [yellow]{_stateManager.Settings.ConsoleFontSize}[/]";
            if (!string.IsNullOrWhiteSpace(_mainMenuSessionWarning))
                compact += $"\n[red]{Markup.Escape(_mainMenuSessionWarning)}[/]";
            return new Markup(compact);
        }

        var statusTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").NoWrap().Width(14))
            .AddColumn(new TableColumn("").NoWrap().Width(1))
            .AddColumn(new TableColumn(""));

        statusTable.AddRow(
            $"[grey]{Markup.Escape(_loc.T("opt_language"))}[/]",
            "[dim]:[/]",
            $"[bold yellow]{Markup.Escape(_stateManager.Settings.Language.ToUpperInvariant())}[/]");
        statusTable.AddRow(
            $"[grey]{Markup.Escape(_loc.T("opt_difficulty"))}[/]",
            "[dim]:[/]",
            $"[bold green]{Markup.Escape(GetDifficultyLabel())}[/]");
        statusTable.AddRow(
            $"[grey]{Markup.Escape(_loc.T("opt_music"))}[/]",
            "[dim]:[/]",
            $"[bold yellow]{Markup.Escape(musicSummary)}[/]");
        statusTable.AddRow(
            $"[grey]{Markup.Escape(_loc.T("opt_sound"))}[/]",
            "[dim]:[/]",
            $"[bold yellow]{Markup.Escape(soundSummary)}[/]");
        statusTable.AddRow(
            $"[grey]{Markup.Escape(_loc.T("opt_font_size"))}[/]",
            "[dim]:[/]",
            $"[bold yellow]{_stateManager.Settings.ConsoleFontSize}[/]");
        if (!string.IsNullOrWhiteSpace(_mainMenuSessionWarning))
        {
            statusTable.AddRow(
                "[grey]session[/]",
                "[dim]:[/]",
                $"[bold red]{Markup.Escape(_mainMenuSessionWarning)}[/]");
        }

        return new Panel(statusTable)
        {
            Header = new PanelHeader($" ⚙ {Markup.Escape(_loc.T("main_menu_status_title"))} ", Justify.Center),
            Border = layout == MainMenuLayoutMode.Compact ? BoxBorder.Ascii : BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = layout == MainMenuLayoutMode.Compact ? new Padding(1, 0) : new Padding(2, 1),
            Expand = true
        };
    }

    private async Task<List<MainMenuOption>> BuildMainMenuOptionsAsync()
    {
        _mainMenuSessionWarning = null;
        var options = new List<MainMenuOption>();
        var nextIndex = 1;

        if (await HasCurrentSessionAsync())
        {
            options.Add(new MainMenuOption(
                "continue_game",
                _loc.T("continue_game"),
                await BuildContinueDescriptionAsync(),
                "cyan1",
                nextIndex++));
        }

        options.AddRange(new[]
        {
            new MainMenuOption("new_game", _loc.T("new_game"), _loc.T("main_menu_new_desc"), "green", nextIndex++),
            new MainMenuOption("load_game", _loc.T("load_game"), _loc.T("main_menu_load_desc"), "cyan1", nextIndex++),
            new MainMenuOption("options", _loc.T("options"), _loc.T("main_menu_options_desc"), "yellow", nextIndex++),
            new MainMenuOption("about", _loc.T("about"), _loc.T("main_menu_about_desc"), "blue", nextIndex++),
            new MainMenuOption("exit", _loc.T("exit"), _loc.T("main_menu_exit_desc"), "red", nextIndex)
        });

        return options;
    }

    private async Task<bool> HasCurrentSessionAsync()
    {
        if (!_fs.FileExists("game_state/meta/soul_state.json"))
            return false;

        await NormalizeRuntimeUiArtifactsAsync();
        await EnsureClientOwnedSystemFilesHealthyAsync();
        var sessionHealth = await _criticalStateHealth.AssessCurrentSessionHealthAsync();
        if (sessionHealth.HasRecoverableSessionError)
        {
            _mainMenuSessionWarning = sessionHealth.UserMessage;
            return false;
        }

        await _stateManager.RefreshGameStateAsync();
        return !string.IsNullOrWhiteSpace(_stateManager.CurrentState.SoulName) ||
               !string.IsNullOrWhiteSpace(_stateManager.CurrentState.SessionId);
    }

    private async Task<string> BuildContinueDescriptionAsync()
    {
        await RefreshRuntimeStateAsync();
        var state = _stateManager.CurrentState;
        var turnNumber = await DetectCurrentSessionTurnNumberAsync();

        var primaryName = !string.IsNullOrWhiteSpace(state.CharacterName)
            ? state.CharacterName
            : !string.IsNullOrWhiteSpace(state.SoulName)
                ? state.SoulName
                : _loc.T("main_menu_continue_desc");

        var realm = state.IsInShiningAbodePendingBootstrap
            ? "Сияющая Обитель (handoff)"
            : state.IsInShiningAbode
                ? _loc.T("realm_shining_abode")
            : state.IsInChaosSea
                ? _loc.T("realm_chaos_sea")
                : string.IsNullOrWhiteSpace(state.CurrentRealm)
                    ? _loc.T("realm_mortal")
                    : state.CurrentRealm;

        if (state.Incarnation > 0 && !state.IsInAfterlifeRealm)
            return $"{primaryName} • {realm} • {_loc.T("turn")} {turnNumber} • #{state.Incarnation}";

        return $"{primaryName} • {realm} • {_loc.T("turn")} {turnNumber}";
    }

    private async Task<int> DetectCurrentSessionTurnNumberAsync()
    {
        var maxTurn = 0;
        foreach (var story in _storyService.GetAvailableStories())
        {
            var entries = await _storyService.ReadStoryAsync(story.RelativePath);
            foreach (var entry in entries)
            {
                if (entry.Turn > maxTurn)
                    maxTurn = entry.Turn;
            }
        }

        return Math.Max(0, maxTurn);
    }

    private async Task ContinueCurrentSessionFlow()
    {
        if (!await HasCurrentSessionAsync())
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("continue_game_unavailable"))}[/]");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(_loc.T("press_any_key"))}[/]");
            Console.ReadKey(true);
            return;
        }

        await NormalizeRuntimeUiArtifactsAsync();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var hasPendingTerminalSignal = _fs.FileExists("ready/turn_complete.json") || _fs.FileExists("ready/turn_error.json");

        await RefreshRuntimeStateAsync();
        var state = _stateManager.CurrentState;
        var sessionId = !string.IsNullOrWhiteSpace(state.SessionId)
            ? state.SessionId
            : Guid.NewGuid().ToString();
        var turnNumber = await DetectCurrentSessionTurnNumberAsync();
        _gameLoop.SetSession(sessionId, turnNumber);

        if (string.IsNullOrWhiteSpace(_lastResponse?.Response) &&
            (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable || hasPendingTerminalSignal))
        {
            var response = await BuildGameResponseFromFiles();
            if (response != null)
                _lastResponse = response;
        }

        await EnterGameLoop();
    }

    private Spectre.Console.Rendering.IRenderable BuildMainMenuMenu(IReadOnlyList<MainMenuOption> options, int selectedIndex, MainMenuLayoutMode layout)
    {
        var sideMargin = layout == MainMenuLayoutMode.VeryCompact ? 1 : 2;
        var menuGrid = new Grid();
        menuGrid.AddColumn(new GridColumn());
        menuGrid.AddRow(ConsoleLayout.WithHorizontalMargin(
            new Markup($"[bold cyan]{Markup.Escape(_loc.T("main_menu_choice_title"))}[/]"),
            sideMargin));
        menuGrid.AddRow(new Text(" "));
        var showDescriptions = layout is MainMenuLayoutMode.Medium or MainMenuLayoutMode.Wide;
        var showGaps = layout is MainMenuLayoutMode.Medium or MainMenuLayoutMode.Wide;

        foreach (var option in options.Select((option, index) => (option, index)))
        {
            var isSelected = option.index == selectedIndex;
            var titleMarkup = isSelected
                ? $"[black on cyan1 bold]  ➤ {option.option.Index}. {Markup.Escape(option.option.Title)}  [/]"
                : $"[{option.option.AccentColor}]◆[/] [bold white]{option.option.Index}. {Markup.Escape(option.option.Title)}[/]";
            var descriptionMarkup = !showDescriptions
                ? null
                : isSelected
                    ? $"[black on cyan1]     {Markup.Escape(option.option.Description)}[/]"
                    : $"[dim]     {Markup.Escape(option.option.Description)}[/]";

            menuGrid.AddRow(new Markup(titleMarkup));
            if (!string.IsNullOrWhiteSpace(descriptionMarkup))
                menuGrid.AddRow(new Markup(descriptionMarkup));
            if (showGaps)
                menuGrid.AddRow(new Text(" "));
        }

        menuGrid.AddRow(new Markup(
            layout == MainMenuLayoutMode.VeryCompact
                ? $"[dim]  ↑/↓ • W/S • 1-{options.Count} • Enter[/]"
                : $"[dim]  ↑/↓ или W/S — выбор • 1-{options.Count} — быстрый выбор • Enter — подтвердить[/]"));
        return ConsoleLayout.WithHorizontalMargin(menuGrid, sideMargin);
    }

    private void RedrawMainMenuMenuArea(IReadOnlyList<MainMenuOption> options, int selectedIndex, MainMenuLayoutMode layout, int menuTop)
    {
        ClearConsoleRegion(menuTop);
        try
        {
            Console.SetCursorPosition(0, menuTop);
        }
        catch
        {
            // If the host rejects cursor positioning, fall back to full redraw.
            RenderMainMenuStaticFrame(options, selectedIndex, layout);
            return;
        }

        AnsiConsole.Write(BuildMainMenuMenu(options, selectedIndex, layout));
    }

    private Spectre.Console.Rendering.IRenderable BuildMainMenuTitle(MainMenuLayoutMode layout)
    {
        if (layout == MainMenuLayoutMode.Compact)
        {
            var compactTitle = new Panel(new Markup("[bold cyan]Book of Eternity[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = false
            };
            return ConsoleLayout.WithHorizontalMargin(compactTitle, 2);
        }

        if (layout == MainMenuLayoutMode.VeryCompact)
        {
            return ConsoleLayout.WithHorizontalMargin(
                new Markup("[bold cyan]Book of Eternity[/]"),
                1);
        }

        if (layout == MainMenuLayoutMode.Medium)
        {
            var top = new FigletText("Book of")
                .Color(Color.Cyan1)
                .Centered();
            var bottom = new FigletText("Eternity")
                .Color(Color.Cyan1)
                .Centered();
            var titleGrid = new Grid();
            titleGrid.AddColumn(new GridColumn());
            titleGrid.AddRow(ConsoleLayout.WithHorizontalMargin(top, 2));
            titleGrid.AddRow(ConsoleLayout.WithHorizontalMargin(bottom, 2));
            return titleGrid;
        }

        var titleFiglet = new FigletText("Book of Eternity")
            .Color(Color.Cyan1)
            .Centered();
        return ConsoleLayout.WithHorizontalMargin(titleFiglet, 2);
    }

    private static MainMenuLayoutMode GetMainMenuLayoutMode(int width, int height)
    {
        if (height < 30 || width < 90)
            return MainMenuLayoutMode.VeryCompact;
        if (width < 100)
            return MainMenuLayoutMode.Compact;
        if (width < 145 || height < 38)
            return MainMenuLayoutMode.Medium;
        return MainMenuLayoutMode.Wide;
    }

    private static int GetSafeConsoleWidth()
    {
        try
        {
            return Math.Max(80, Console.WindowWidth);
        }
        catch
        {
            return 120;
        }
    }

    private static int GetSafeConsoleHeight()
    {
        try
        {
            return Math.Max(24, Console.WindowHeight);
        }
        catch
        {
            return 40;
        }
    }

    private static void ClearConsoleRegion(int top)
    {
        try
        {
            var width = Math.Max(1, Console.WindowWidth);
            var height = Math.Max(0, Console.WindowHeight - top);
            for (var row = 0; row < height; row++)
            {
                Console.SetCursorPosition(0, top + row);
                Console.Write(new string(' ', width));
            }
        }
        catch
        {
            // Ignore console clearing failures; caller will still attempt redraw.
        }
    }

    private static bool TryMapMenuNumberSelection(ConsoleKeyInfo key, int optionsCount, out int index)
    {
        index = -1;

        int? numeric = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
            _ => null
        };

        if (!numeric.HasValue || numeric.Value > optionsCount)
            return false;

        index = numeric.Value - 1;
        return true;
    }

    private string GetDifficultyLabel() => _stateManager.Settings.Difficulty switch
    {
        "hard" => _loc.T("difficulty_hard"),
        "impossible" => _loc.T("difficulty_impossible"),
        _ => _loc.T("difficulty_normal")
    };

    private string PromptTextInput(
        string promptMarkup,
        string? defaultValue = null,
        bool allowEmpty = true,
        string? emptyError = null,
        bool preserveNewlines = false)
    {
        return TextComposer.Read(
            StandardTextComposerConsole.Instance,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = promptMarkup,
                DefaultValue = defaultValue,
                AllowEmpty = allowEmpty,
                EmptyError = emptyError,
                PreserveNewlines = preserveNewlines
            });
    }

    // ═══════════════════════════════════════════════
    // NEW GAME FLOW
    // ═══════════════════════════════════════════════

    private async Task NewGameFlow()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]🌟 Новая Игра[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        // Step 1: Soul name
        var soulName = PromptTextInput($"[cyan]{_loc.T("enter_soul_name")}[/]", allowEmpty: false, emptyError: "Имя не может быть пустым");

        AnsiConsole.WriteLine();

        // Step 2: Guardian
        var guardianChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Выберите способ создания Хранителя:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(
                    _loc.T("create_guardian"),
                    _loc.T("choose_guardian")
                ));

        JsonObject pendingGuardianCreation;
        if (guardianChoice == _loc.T("create_guardian"))
        {
            var guardianDescription = PromptTextInput($"[cyan]{_loc.T("guardian_prompt")}[/]",
                allowEmpty: false,
                emptyError: "Описание не может быть пустым",
                preserveNewlines: true);
            pendingGuardianCreation = _systemGuardianLibraryService.BuildFreeformPendingGuardianCreationNode(guardianDescription, soulName);
        }
        else
        {
            var selectedPreset = await PromptSystemGuardianPresetSelectionAsync();
            if (selectedPreset == null)
                return;

            pendingGuardianCreation = _systemGuardianLibraryService.BuildPendingGuardianCreationNode(selectedPreset, soulName);
        }

        // Step 3: Enter the Chaos Sea — NO character/world description at this point
        // The mortal world is NOT described at the start. Player enters it later through incarnation.
        await InitializeChaosSea(soulName, pendingGuardianCreation);

        // CRITICAL: Wait for the GM to describe the Guardian's abode before entering the loop
        // Without this, the player sees a blank screen after starting a new game
        if (!await WaitForGmResponse())
            return;

        // Enter game loop in Chaos Sea phase
        await EnterGameLoop();
    }

    /// <summary>
    /// Initialize a new game in the Chaos Sea (afterlife hub).
    /// No mortal character or world is created yet — that happens when the player incarnates.
    /// </summary>
    private async Task InitializeChaosSea(string soulName, JsonObject pendingGuardianCreation)
    {
        // Generate session ID once — used for both chat_log.json and GameLoop
        var sessionId = Guid.NewGuid().ToString();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .Start(_loc.T("soul_awakens"), ctx =>
            {
                // Clear old state
                _fs.ClearGameState();
                _afterlifeArchiveCandidateService.Clear();

                // Initialize soul state — realm is Chaos Sea
                var soulState = new
                {
                    soulName,
                    previousSoulNames = Array.Empty<string>(),
                    currentRealm = "Chaos Sea",
                    currentIncarnation = 0, // Not yet incarnated
                    enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
                    inkFeathers = new { current = 0, total = 0 },
                    soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() },
                    afterlifeArchive = new { stored = Array.Empty<object>() },
                    livesHistory = Array.Empty<object>(),
                    pendingMemoryLegacy = (object?)null
                };
                WriteCanonicalSoulStateAsync(soulState).Wait();

                // Initialize guardian
                var guardian = new
                {
                    guardians = Array.Empty<object>(),
                    activeGuardian = (object?)null,
                    chaosSeaNavigation = new
                    {
                        currentAbodeId = (object?)null
                    },
                    pendingGuardianCreation
                };
                _fs.WriteFileAtomicAsync("game_state/meta/guardians.json",
                    JsonSerializer.Serialize(guardian, JsonOpts)).Wait();

                // Initialize session
                var chatLog = new
                {
                    sessionId,
                    language = "Russian",
                    turns = Array.Empty<object>()
                };
                _fs.WriteFileAtomicAsync("game_state/history/chat_log.json",
                    JsonSerializer.Serialize(chatLog, JsonOpts)).Wait();

                var achievementsState = new
                {
                    unlockedAchievements = Array.Empty<object>(),
                    trackedProgress = Array.Empty<object>(),
                    stats = new
                    {
                        totalUnlocked = 0,
                        byCategory = new
                        {
                            combat = 0,
                            exploration = 0,
                            story = 0,
                            social = 0,
                            crafting = 0,
                            meta = 0,
                            death = 0,
                            secret = 0
                        },
                        byRarity = new
                        {
                            common = 0,
                            uncommon = 0,
                            rare = 0,
                            epic = 0,
                            legendary = 0
                        }
                    }
                };
                _fs.WriteFileAtomicAsync("game_state/meta/achievements.json",
                    JsonSerializer.Serialize(achievementsState, JsonOpts)).Wait();

                var codexState = new
                {
                    entries = Array.Empty<object>(),
                    totalEntries = 0,
                    categories = new
                    {
                        cosmology = 0,
                        geography = 0,
                        history = 0,
                        cultures = 0,
                        creatures = 0,
                        characters = 0,
                        artifacts = 0,
                        factions = 0,
                        magic = 0,
                        other = 0
                    }
                };
                _fs.WriteFileAtomicAsync("lore/codex_entries.json",
                    JsonSerializer.Serialize(codexState, JsonOpts)).Wait();

                var playerChronicle = new
                {
                    entries = Array.Empty<object>()
                };
                _fs.WriteFileAtomicAsync("lore/chaos_sea/player_chronicle.json",
                    JsonSerializer.Serialize(playerChronicle, JsonOpts)).Wait();
            });

        _gameLoop.SetSession(sessionId, 0);
        await RefreshRuntimeStateAsync();

        // Write game settings (difficulty flags) for GM
        await WriteGameSettingsForGm();

        var guardianRequestLabel =
            pendingGuardianCreation["presetDisplayName"]?.GetValue<string>() ??
            pendingGuardianCreation["description"]?.GetValue<string>() ??
            "неизвестный Хранитель";

        // Send initial turn to GM — soul awakens in the Chaos Sea, not in a mortal world
        var firstAction = $"Душа по имени «{soulName}» пробуждается в Море Хаоса. " +
                          $"Хранитель: {guardianRequestLabel}. " +
                          "Опиши обитель Хранителя и первую встречу с ним. " +
                          "Это начало нового пути — душа ещё не воплотилась в смертную жизнь.";

        var request = new TurnRequest
        {
            SessionId = _gameLoop.SessionId,
            TurnNumber = 1,
            PlayerAction = firstAction,
            Timestamp = DateTime.UtcNow.ToString("o"),
            GameMode = "normal",
            SystemReminder = await BuildTurnSystemReminderAsync()
        };
        AttachFreshDiceAndGacha(request);
        request.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync();
        await CreateCanonicalBaselineSnapshotAsync(request, sourceLabel: "первого описания Моря Хаоса");

        ClearTransientOutputFiles();
        await _fs.WriteFileAtomicAsync("input/turn_request.json",
            JsonSerializer.Serialize(request, JsonOpts));

        AnsiConsole.MarkupLine($"[green]🌊 {_loc.T("soul_awakens")}[/]");
    }

    /// <summary>
    /// Handles the transition from Chaos Sea → Mortal Life through the Soul Gates.
    /// Player configures their mortal incarnation here.
    /// </summary>
    private async Task HandleIncarnation()
    {
        AnsiConsole.Clear();

        // Soul Gates banner
        var gateFiglet = new FigletText("Soul Gates")
            .Color(Color.Gold1)
            .Centered();
        AnsiConsole.Write(gateFiglet);
        AnsiConsole.Write(new Rule("[gold1]✦ Врата Души ✦[/]").RuleStyle("gold1"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Вы стоите перед Вратами Души — порталом в мир смертных.[/]");
        AnsiConsole.MarkupLine("[dim]Настройте своё будущее воплощение перед входом.[/]");
        AnsiConsole.WriteLine();

        var incarnationBlockers = await CollectIncarnationBlockersAsync();
        if (incarnationBlockers.Count > 0)
        {
            AnsiConsole.Write(new Panel(string.Join("\n", new[]
            {
                "Нельзя войти в новую смертную жизнь, пока остаются незакрытые загробные контракты.",
                string.Empty,
                string.Join("\n", incarnationBlockers.Select(item => $"• {item}")),
                string.Empty,
                "Сначала дождитесь явного закрытия GM или почините повреждённый pending contract."
            }))
            {
                Header = new PanelHeader(" Врата Души ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(2, 1),
                Expand = true
            });
            _ = Console.ReadKey(true);
            return;
        }

        // Character description
        AnsiConsole.MarkupLine("[cyan]Опишите персонажа в смертной жизни:[/]");
        AnsiConsole.MarkupLine("[dim](Раса, класс, внешность, предыстория... или оставьте пустым)[/]");
        var charDesc = PromptTextInput("[cyan]Персонаж:[/]", allowEmpty: true, preserveNewlines: true);

        AnsiConsole.WriteLine();

        // World description
        AnsiConsole.MarkupLine("[cyan]Опишите мир, в который хотите воплотиться:[/]");
        AnsiConsole.MarkupLine("[dim](Жанр, сеттинг, особенности... или оставьте пустым — Хранитель выберет)[/]");
        var worldDesc = PromptTextInput("[cyan]Мир:[/]", allowEmpty: true, preserveNewlines: true);

        AnsiConsole.WriteLine();

        // Starting circumstances
        AnsiConsole.MarkupLine("[cyan]Обстоятельства начала (необязательно):[/]");
        AnsiConsole.MarkupLine("[dim](Где вы появляетесь? Что происходит вокруг?)[/]");
        var circumstances = PromptTextInput("[cyan]Обстоятельства:[/]", allowEmpty: true, preserveNewlines: true);

        var pendingSetupBeforeRaw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        var pendingSetupSummary = BuildPendingWorldSetupActionSummary(pendingSetupBeforeRaw);

        // Build incarnation action
        var parts = new List<string> { "Душа входит через Врата Души и воплощается в смертную жизнь." };

        if (!string.IsNullOrWhiteSpace(charDesc))
            parts.Add($"Персонаж: {charDesc}.");
        if (!string.IsNullOrWhiteSpace(worldDesc))
            parts.Add($"Мир: {worldDesc}.");
        if (!string.IsNullOrWhiteSpace(circumstances))
            parts.Add($"Обстоятельства начала: {circumstances}.");
        if (string.IsNullOrWhiteSpace(charDesc) && string.IsNullOrWhiteSpace(worldDesc))
        {
            parts.Add(string.IsNullOrWhiteSpace(pendingSetupSummary)
                ? "Хранитель выбирает мир и обстоятельства рождения для души."
                : $"Используй уже подготовленный pending world setup как главный входной контракт для мира и обстоятельств: {pendingSetupSummary}.");
        }
        else if (!string.IsNullOrWhiteSpace(pendingSetupSummary))
        {
            parts.Add($"Учитывай уже подготовленный pending world setup, не противоречь ему и не удаляй его смысл: {pendingSetupSummary}.");
        }

        var action =
            string.Join(" ", parts) +
            " В этом accepted turn не переключай душу локально в Mortal World и не создавай первый mortal bootstrap. " +
            "Сначала выполни только canonical TriggerIncarnation в game_state/control/incarnation_trigger.json, используя pending incarnation_world_setup как входной контракт. " +
            "После принятого TriggerIncarnation клиент сам выполнит локальный переход и запустит отдельный следующий ход для первого Mortal World bootstrap.";

        var pendingSetupAfterPreview = await BuildIncarnationPendingSetupAfterPreviewAsync(charDesc, worldDesc, circumstances);
        if (!ConfirmIncarnationContractPreview(charDesc, worldDesc, circumstances, action, pendingSetupBeforeRaw, pendingSetupAfterPreview))
            return;

        // Each accepted incarnation request must create a fresh mortal-world lore set.
        _fs.ClearCurrentWorldLore();

        await _worldDirectiveService.UpsertPendingSetupFromIncarnationPromptAsync(charDesc, worldDesc, circumstances);
        await _scenarioCoreService.RefreshFromPendingSetupAsync();

        await ProcessPlayerTurn(action);
    }

    private async Task<List<string>> CollectIncarnationBlockersAsync()
    {
        var blockers = new List<string>();

        var pendingConsultationState = await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs);
        if (pendingConsultationState.Exists)
        {
            blockers.Add(await BuildPendingFileBlockerAsync(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                pendingConsultationState.IsMalformed ? "повреждённый запрос на архивную консультацию" : "незакрытый запрос на архивную консультацию",
                "archiveActionResolutions + soul_state.afterlifeArchive.actionReceipts[]"));
        }

        var pendingProjectFuelState = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs);
        if (pendingProjectFuelState.Exists)
        {
            blockers.Add(await BuildPendingFileBlockerAsync(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                pendingProjectFuelState.IsMalformed ? "повреждённый запрос на архивную подпитку проекта" : "незакрытый запрос на архивную подпитку проекта",
                "archiveActionResolutions + project/log effect only if request permits it"));
        }

        if (_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath))
        {
            blockers.Add(await GuardianAbodeOfferingState.ReadAsync(_fs) == null
                ? await BuildPendingFileBlockerAsync(GuardianAbodeOfferingState.PendingRequestPath, "повреждённое подношение Обители", "guardianPowerEvents.reasonType=offering; ink_feathers also require output/ink_feather_action_result.json")
                : await BuildPendingFileBlockerAsync(GuardianAbodeOfferingState.PendingRequestPath, "незакрытое подношение Обители", "guardianPowerEvents.reasonType=offering; ink_feathers also require output/ink_feather_action_result.json"));
        }

        var guardianTradeState = await GuardianTradeRequestState.ReadStateAsync(_fs);
        if (guardianTradeState.Exists)
        {
            blockers.Add(await BuildPendingFileBlockerAsync(
                GuardianTradeRequestState.PendingRequestPath,
                guardianTradeState.IsMalformed ? "повреждённый запрос на торговую витрину Хранителя" : "незакрытый запрос на торговую витрину Хранителя",
                "UpdateGuardians + guardian tradeInventory + tradeInventoryReceipts[]"));
        }

        var foundationState = await PlayerGuardianFoundationState.ReadStateAsync(_fs);
        if (foundationState.Exists)
        {
            blockers.Add(await BuildPendingFileBlockerAsync(
                PlayerGuardianFoundationState.PendingRequestPath,
                foundationState.IsMalformed ? "повреждённый ритуал основания собственного Хранителя" : "незакрытый ритуал основания собственного Хранителя",
                "UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory"));
        }

        var attractionState = await _systemGuardianLibraryService.ReadAttractionRequestDisplayStateAsync();
        if (attractionState.FilePresent)
        {
            blockers.Add(attractionState.IsMalformed
                ? await BuildPendingFileBlockerAsync(SystemGuardianLibraryService.AttractionRequestPath, "system_guardian_attraction.json повреждён: притяжение к извечному Хранителю", "UpdateGuardians + guardians/activeGuardian + chaosSeaNavigation или отмените attraction contract через client cancellation")
                : await BuildPendingFileBlockerAsync(SystemGuardianLibraryService.AttractionRequestPath, "незакрытое притяжение к извечному Хранителю", "UpdateGuardians + guardians/activeGuardian + chaosSeaNavigation или отмените attraction contract через client cancellation"));
        }

        if (_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath))
        {
            blockers.Add(await GuardianAbodeResidentRequestState.IsResidentsRequestFileMalformedAsync(_fs)
                ? await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "повреждённый запрос на обновление состава Обители", "UpdateGuardianAbodeResidents + roster receipts/history")
                : await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "незакрытый запрос на обновление состава Обители", "UpdateGuardianAbodeResidents + roster receipts/history"));
        }

        if (_fs.FileExists(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath))
        {
            blockers.Add(await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs)
                ? await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "повреждённый запрос общения с резидентом Обители", "residentInteractionLogUpdates + matching interaction receipts")
                : await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "незакрытый запрос общения с резидентом Обители", "residentInteractionLogUpdates + matching interaction receipts"));
        }

        if (_fs.FileExists(GuardianAbodeResidentRequestState.PendingTransfersRequestPath))
        {
            blockers.Add(await GuardianAbodeResidentRequestState.IsTransferRequestFileMalformedAsync(_fs)
                ? await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "повреждённый запрос перехода резидента между Обителями", "UpdateGuardianAbodeResidentTransferReceipts + source/target resident state")
                : await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "незакрытый запрос перехода резидента между Обителями", "UpdateGuardianAbodeResidentTransferReceipts + source/target resident state"));
        }

        if (_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath))
        {
            blockers.Add(await GuardianAbodeResidentRequestState.IsManifestationRequestFileMalformedAsync(_fs)
                ? await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "повреждённый mortal-only запрос проявления companion-резидента", "repair pending_resident_companion_manifestation_request.json before Soul Gates")
                : await BuildPendingFileBlockerAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "mortal-only запрос проявления companion-резидента остался в afterlife", "закройте/почините manifestation request before Soul Gates"));
        }

        var guardianSocialState = await ActorSocialInteractionRequestState.ReadGuardianRequestsStateAsync(_fs);
        if (guardianSocialState.FilePresent)
        {
            blockers.Add(guardianSocialState.IsMalformed
                ? await BuildPendingFileBlockerAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "повреждённый социальный запрос к Хранителю", "guardianSocialJournalUpdates with matching requestId/guardianId/interactionType")
                : await BuildPendingFileBlockerAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "незакрытый социальный запрос к Хранителю", "guardianSocialJournalUpdates with matching requestId/guardianId/interactionType"));
        }

        foreach (var shiningPending in await GetBlockingShiningPendingContractPathsAsync())
            blockers.Add(shiningPending);

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (!string.IsNullOrWhiteSpace(shiningJson))
        {
            try
            {
                if (JsonNode.Parse(shiningJson) is JsonObject shiningRoot &&
                    shiningRoot["pendingNativeFactionDiscovery"] is not null)
                {
                    blockers.Add($"{ShiningAbodeState.StatePath}.pendingNativeFactionDiscovery: legacy Shining discovery contract не закрыт\n  закрытие: discover_native_faction legacy receipt + pendingNativeFactionDiscovery=null before Soul Gates");
                }
            }
            catch
            {
                blockers.Add($"{ShiningAbodeState.StatePath}: повреждённый Shining owner state; repair before Soul Gates");
            }
        }

        return blockers;
    }

    private async Task<SystemGuardianLibraryService.SystemGuardianPresetDescriptor?> PromptSystemGuardianPresetSelectionAsync()
    {
        var presets = await _systemGuardianLibraryService.GetAvailablePresetsAsync(includeDossier: true);
        var userDir = _systemGuardianLibraryService.GetUserDirectoryPath();

        if (presets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ В библиотеке извечных хранителей пока нет ни одного пресета.[/]");
            AnsiConsole.MarkupLine($"[dim]Добавьте свои папки в: {Markup.Escape(userDir)}[/]");
            AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);
            return null;
        }

        while (true)
        {
            var choices = presets
                .Select(preset =>
                    $"{preset.DisplayName} [dim]({Markup.Escape(preset.Domain)} • {Markup.Escape(preset.LibraryKind)} • v{Markup.Escape(preset.Version)})[/]")
                .ToList();
            choices.Add("📂 Открыть папку пользовательских извечных хранителей");
            choices.Add(_loc.T("back"));

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Выберите извечного Хранителя:[/]\n[dim]Это библиотека именованных хранителей, всегда доступных душе. Для игрока это ролевой термин; в файлах и валидаторе они технически называются system guardian presets.[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .PageSize(12)
                    .AddChoices(choices));

            if (selected == _loc.T("back"))
                return null;

            if (selected.StartsWith("📂", StringComparison.Ordinal))
            {
                OpenFolderOrPrintPath(userDir);
                continue;
            }

            var presetIndex = choices.IndexOf(selected);
            if (presetIndex < 0 || presetIndex >= presets.Count)
                continue;

            var preset = presets[presetIndex];
            if (ConfirmSystemGuardianPresetSelection(preset))
                return preset;
        }
    }

    private bool ConfirmSystemGuardianPresetSelection(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset)
    {
        var dossier = preset.DossierMarkdown?.Trim() ?? "";
        var dossierLines = dossier
            .Replace("\r\n", "\n")
            .Split('\n')
            .ToList();

        var lines = new List<string>
        {
            $"[bold cyan]{Markup.Escape(preset.DisplayName)}[/]",
            "",
            $"[white]Домен:[/] {Markup.Escape(preset.Domain)}",
            $"[white]Архетип:[/] {Markup.Escape(preset.Archetype)}",
            $"[white]Тон:[/] {Markup.Escape(preset.Tone)}",
            $"[white]Обитель:[/] {Markup.Escape(preset.AbodeName)}",
            $"[white]Сводка:[/] {Markup.Escape(preset.Summary)}"
        };

        if (preset.CoreValues.Count > 0)
            lines.Add($"[white]Ценности:[/] {Markup.Escape(string.Join(", ", preset.CoreValues))}");

        if (dossierLines.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Досье:[/]");
            lines.AddRange(dossierLines.Select(line => Markup.Escape(line)));
        }

        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛡️ Системный хранитель ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(1, 1),
            Expand = true
        });

        WriteMainMenuJsonAuditPanel("Полный JSON system guardian preset", BuildSystemGuardianPresetAuditNode(preset), Color.Cyan1);

        return AnsiConsole.Confirm("[yellow]Выбрать этого хранителя для новой игры?[/]", true);
    }

    private async Task<JsonNode?> BuildIncarnationPendingSetupAfterPreviewAsync(string characterDescription, string worldDescription, string circumstances)
    {
        var pending = await _worldDirectiveService.ReadPendingSetupAsync();
        if (pending == null &&
            string.IsNullOrWhiteSpace(characterDescription) &&
            string.IsNullOrWhiteSpace(worldDescription) &&
            string.IsNullOrWhiteSpace(circumstances))
        {
            return null;
        }

        pending ??= new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectiveService.WorldDirectives()
        };

        if (!string.IsNullOrWhiteSpace(characterDescription))
            pending.CharacterDescription = characterDescription.Trim();
        if (!string.IsNullOrWhiteSpace(circumstances))
            pending.StartingCircumstances = circumstances.Trim();
        if (string.IsNullOrWhiteSpace(pending.WorldDirectives.SettingSummary) &&
            !string.IsNullOrWhiteSpace(worldDescription))
            pending.WorldDirectives.SettingSummary = worldDescription.Trim();
        if (string.IsNullOrWhiteSpace(pending.WorldDirectives.DetailedWorldDescription) &&
            !string.IsNullOrWhiteSpace(worldDescription))
            pending.WorldDirectives.DetailedWorldDescription = worldDescription.Trim();
        if (!string.IsNullOrWhiteSpace(circumstances))
        {
            var note = $"Стартовые обстоятельства: {circumstances.Trim()}";
            if (!pending.WorldDirectives.ContinuityNotes.Contains(note, StringComparer.OrdinalIgnoreCase))
                pending.WorldDirectives.ContinuityNotes.Add(note);
        }
        if (pending.Mode == "profile")
            pending.Mode = "mixed";

        return JsonSerializer.SerializeToNode(pending, JsonOpts);
    }

    private static string BuildPendingWorldSetupActionSummary(string? pendingSetupRaw)
    {
        if (string.IsNullOrWhiteSpace(pendingSetupRaw))
            return string.Empty;

        try
        {
            if (JsonNode.Parse(pendingSetupRaw) is not JsonObject root)
                return "pending setup exists but is not a JSON object";

            var parts = new List<string>();
            AddPendingSetupSummaryPart(parts, "sourceId", root["sourceId"]);
            AddPendingSetupSummaryPart(parts, "mode", root["mode"]);
            AddPendingSetupSummaryPart(parts, "character", root["characterDescription"]);
            AddPendingSetupSummaryPart(parts, "circumstances", root["startingCircumstances"]);

            if (root["worldDirectives"] is JsonObject worldDirectives)
            {
                AddPendingSetupSummaryPart(parts, "setting", worldDirectives["settingSummary"]);
                AddPendingSetupSummaryPart(parts, "genre", worldDirectives["genre"]);
                AddPendingSetupSummaryPart(parts, "tone", worldDirectives["tone"]);
                AddPendingSetupSummaryPart(parts, "detailedWorldDescription", worldDirectives["detailedWorldDescription"]);
            }

            return parts.Count == 0
                ? "pending setup exists; inspect game_state/control/pending_incarnation_world_setup.json"
                : string.Join("; ", parts);
        }
        catch
        {
            return "pending setup exists but is malformed; preserve it for repair instead of contradicting it";
        }
    }

    private static void AddPendingSetupSummaryPart(List<string> parts, string label, JsonNode? node)
    {
        var value = GetNodeString(node);
        if (string.IsNullOrWhiteSpace(value))
            return;

        const int maxLength = 220;
        value = value.Length > maxLength ? value[..maxLength] + "..." : value;
        parts.Add($"{label}={value}");
    }

    private bool ConfirmIncarnationContractPreview(
        string characterDescription,
        string worldDescription,
        string circumstances,
        string playerAction,
        string? pendingSetupBeforeRaw,
        JsonNode? pendingSetupAfterPreview)
    {
        var lines = new List<string>
        {
            "[bold yellow]⚔️ Предпросмотр воплощения через Врата Души[/]",
            "",
            "Это GM-authored lifecycle contract, а не локальный переход в смертный мир.",
            "GM должен записать только canonical TriggerIncarnation в game_state/control/incarnation_trigger.json.",
            $"Pending setup перед отправкой будет записан в [dim]{WorldDirectiveService.PendingSetupPath}[/].",
            "",
            "[bold]Accepted outcome:[/]",
            "  • game_state/control/incarnation_trigger.json содержит TriggerIncarnation для следующего mortal bootstrap.",
            "  • currentRealm остаётся Chaos Sea до клиентского bootstrap handoff.",
            "  • клиент сам создаёт первый Mortal World turn после принятого trigger.",
            "",
            "[bold]Rejected/repair outcome:[/]",
            "  • если GM не может закрыть trigger строго, он не должен переключать мир вручную.",
            "  • pending setup сохраняется как client-owned входной контракт для ремонта.",
            "",
            "[bold]Pending world setup before/after:[/]",
            $"  • before: {(string.IsNullOrWhiteSpace(pendingSetupBeforeRaw) ? "none" : WorldDirectiveService.PendingSetupPath)}",
            $"  • after: {(pendingSetupAfterPreview == null ? "none/new file not written by blank prompt" : "see full JSON audit below")}",
            "",
            "[bold]Запрещено в accepted turn:[/]",
            "  • TriggerLifeEnd, Life Evaluation rewards, Mortal World currentLocationData/worldEventsLog/UpdateNPCs.",
            "  • Создание первого mortal bootstrap в том же ответе.",
            "  • Ручная смена soul_state.currentRealm на Mortal World."
        };

        var audit = new JsonObject
        {
            ["playerAction"] = playerAction,
            ["requiredOutputFile"] = "game_state/control/incarnation_trigger.json",
            ["pendingSetupFile"] = WorldDirectiveService.PendingSetupPath,
            ["expectedResponseSurface"] = "TriggerIncarnation",
            ["characterDescription"] = characterDescription,
            ["worldDescription"] = worldDescription,
            ["circumstances"] = circumstances,
            ["pendingSetupBefore"] = TryParseJsonAuditNode(pendingSetupBeforeRaw) ?? JsonValue.Create(string.IsNullOrWhiteSpace(pendingSetupBeforeRaw) ? "absent" : pendingSetupBeforeRaw),
            ["pendingSetupAfterPreview"] = pendingSetupAfterPreview?.DeepClone(),
            ["affectedFiles"] = new JsonArray
            {
                "game_state/control/incarnation_trigger.json",
                WorldDirectiveService.PendingSetupPath,
                "game_state/meta/soul_state.json"
            },
            ["forbiddenSameTurnSurfaces"] = new JsonArray
            {
                "TriggerLifeEnd",
                "Life Evaluation rewards",
                "currentLocationData",
                "worldEventsLog",
                "UpdateNPCs",
                "Mortal World bootstrap state"
            }
        };

        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔️ Полный контракт /incarnate ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WriteMainMenuJsonAuditPanel("Полный JSON-аудит /incarnate contract", audit, Color.Yellow);

        return AnsiConsole.Confirm("[yellow]Отправить этот контракт GM?[/]", true);
    }

    private static JsonNode? TryParseJsonAuditNode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> BuildPendingFileBlockerAsync(string path, string title, string closure)
    {
        var raw = await _fs.ReadFileAsync(path);
        var identity = DescribePendingFileIdentity(raw);
        return $"{path}: {title}\n  identity: {identity}\n  закрытие: {closure}\n{DescribePendingFilePayload(raw)}";
    }

    private static string BuildArchiveConsultationBlocker(AfterlifeArchiveActionState.PendingArchiveConsultationRequest? request) =>
        request == null
            ? $"{AfterlifeArchiveActionState.ConsultationRequestPath}: unreadable; закрытие: archiveActionResolutions + soul_state.afterlifeArchive.actionReceipts[]."
            : $"{AfterlifeArchiveActionState.ConsultationRequestPath}: requestId={request.RequestId}, archiveId={request.ArchiveId}, requestedMode={request.RequestedMode}; закрытие: archiveActionResolutions + soul_state.afterlifeArchive.actionReceipts[].";

    private static string BuildArchiveProjectFuelBlocker(AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest? request) =>
        request == null
            ? $"{AfterlifeArchiveActionState.ProjectFuelRequestPath}: unreadable; закрытие: archiveActionResolutions + allowed project/log effect."
            : $"{AfterlifeArchiveActionState.ProjectFuelRequestPath}: requestId={request.RequestId}, archiveId={request.ArchiveId}, targetProjectId={request.TargetProjectId}, requestedMode={request.RequestedMode}; закрытие: archiveActionResolutions + allowed project/log effect.";

    private static string BuildGuardianTradeBlocker(GuardianTradeRequestState.PendingGuardianTradeRequest? request) =>
        request == null
            ? $"{GuardianTradeRequestState.PendingRequestPath}: unreadable; закрытие: UpdateGuardians + guardian tradeInventory + tradeInventoryReceipts[]."
            : $"{GuardianTradeRequestState.PendingRequestPath}: requestId={request.RequestId}, guardianId={request.GuardianId}, returnCycleId={request.ReturnCycleId}, derivedTradeSlotCount={request.DerivedTradeSlotCount}; закрытие: UpdateGuardians + guardian tradeInventory + tradeInventoryReceipts[].";

    private static string BuildFoundationBlocker(PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest? request) =>
        request == null
            ? $"{PlayerGuardianFoundationState.PendingRequestPath}: unreadable; закрытие: UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory."
            : $"{PlayerGuardianFoundationState.PendingRequestPath}: requestId={request.RequestId}, proposedDisplayName={request.ProposedDisplayName}, previousGuardianId={request.PreviousGuardianId}; закрытие: UpdateGuardians.create + guardians/activeGuardian + playerGuardianFoundationHistory.";

    private static string DescribePendingFileIdentity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "identity unavailable: file is empty or unreadable";

        try
        {
            var node = JsonNode.Parse(raw);
            var root = node as JsonObject;
            if (root == null)
                return "identity unavailable: root is not an object";

            var payloads = root["requests"] is JsonArray requests
                ? requests.OfType<JsonObject>().ToList()
                : new List<JsonObject> { root };
            if (payloads.Count == 0)
                payloads.Add(root);

            var summaries = new List<string>();
            for (var i = 0; i < payloads.Count; i++)
            {
                var source = payloads[i];
                var parts = new[]
                {
                    ("requestId", GetAuditNodeString(source["requestId"])),
                    ("actionType", GetAuditNodeString(source["actionType"])),
                    ("interactionType", GetAuditNodeString(source["interactionType"])),
                    ("requestedMode", GetAuditNodeString(source["requestedMode"])),
                    ("requestMode", GetAuditNodeString(source["requestMode"])),
                    ("offeringType", GetAuditNodeString(source["offeringType"])),
                    ("guardianId", GetAuditNodeString(source["guardianId"])),
                    ("abodeId", GetAuditNodeString(source["abodeId"])),
                    ("residentId", GetAuditNodeString(source["residentId"])),
                    ("sourceGuardianId", GetAuditNodeString(source["sourceGuardianId"])),
                    ("sourceAbodeId", GetAuditNodeString(source["sourceAbodeId"])),
                    ("targetGuardianId", GetAuditNodeString(source["targetGuardianId"])),
                    ("targetAbodeId", GetAuditNodeString(source["targetAbodeId"])),
                    ("targetProjectId", GetAuditNodeString(source["targetProjectId"])),
                    ("factionId", GetAuditNodeString(source["factionId"])),
                    ("projectId", GetAuditNodeString(source["projectId"])),
                    ("relicId", GetAuditNodeString(source["relicId"])),
                    ("archiveId", GetAuditNodeString(source["archiveId"])),
                    ("tradeCycleId", GetAuditNodeString(source["tradeCycleId"])),
                    ("returnCycleId", GetAuditNodeString(source["returnCycleId"])),
                    ("createdAtTurn", GetAuditNodeString(source["createdAtTurn"])),
                    ("costFeathers", GetAuditNodeString(source["costFeathers"])),
                    ("costLightSparks", GetAuditNodeString(source["costLightSparks"])),
                    ("quotedCostFeathers", GetAuditNodeString(source["quotedCostFeathers"])),
                    ("quotedCostLightSparks", GetAuditNodeString(source["quotedCostLightSparks"])),
                    ("inkFeathersOffered", GetAuditNodeString(source["inkFeathersOffered"])),
                    ("derivedTradeSlotCount", GetAuditNodeString(source["derivedTradeSlotCount"])),
                    ("derivedRarityCeiling", GetAuditNodeString(source["derivedRarityCeiling"]))
                }
                .Where(part => !string.IsNullOrWhiteSpace(part.Item2))
                .Select(part => $"{part.Item1}={part.Item2}")
                .ToArray();

                summaries.Add(parts.Length == 0
                    ? $"request[{i}]: identity fields not found; inspect full pending JSON"
                    : $"request[{i}]: {string.Join(", ", parts)}");
            }

            return string.Join("; ", summaries);
        }
        catch (Exception ex)
        {
            return $"identity unavailable: malformed JSON ({ex.GetType().Name})";
        }
    }

    private static string DescribePendingFilePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "  full payload: <empty>";

        try
        {
            var node = JsonNode.Parse(raw);
            if (node == null)
                return "  full payload: <unreadable>";

            var payloadLines = node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                .Split('\n')
                .Select(line => $"    {line.TrimEnd('\r')}");
            return "  full payload:\n" + string.Join("\n", payloadLines);
        }
        catch (Exception ex)
        {
            return $"  full payload: malformed JSON ({ex.GetType().Name})";
        }
    }

    private static string? GetAuditNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonArray array)
        {
            var values = array
                .Select(GetAuditNodeString)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(8)
                .ToArray();
            return values.Length == 0 ? null : $"[{string.Join(", ", values)}]";
        }

        if (node is JsonObject)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static JsonObject BuildSystemGuardianPresetAuditNode(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset) =>
        new()
        {
            ["presetId"] = preset.PresetId,
            ["displayName"] = preset.DisplayName,
            ["summary"] = preset.Summary,
            ["libraryKind"] = preset.LibraryKind,
            ["version"] = preset.Version,
            ["domain"] = preset.Domain,
            ["archetype"] = preset.Archetype,
            ["tone"] = preset.Tone,
            ["coreValues"] = new JsonArray(preset.CoreValues.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["defaultNameVariant"] = preset.DefaultNameVariant,
            ["feminineNameVariant"] = preset.FeminineNameVariant,
            ["masculineNameVariant"] = preset.MasculineNameVariant,
            ["neutralNameVariant"] = preset.NeutralNameVariant,
            ["formFlexibility"] = preset.FormFlexibility,
            ["defaultPresentationStyle"] = preset.DefaultPresentationStyle,
            ["defaultPronouns"] = preset.DefaultPronouns,
            ["defaultAppearanceDescription"] = preset.DefaultAppearanceDescription,
            ["abodeName"] = preset.AbodeName,
            ["abodeTheme"] = preset.AbodeTheme,
            ["mustPreserve"] = new JsonArray(preset.MustPreserve.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["canVary"] = new JsonArray(preset.CanVary.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["forbidden"] = new JsonArray(preset.Forbidden.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["searchLabel"] = preset.SearchLabel,
            ["searchKeywords"] = new JsonArray(preset.SearchKeywords.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["directoryName"] = preset.DirectoryName,
            ["directoryPath"] = preset.DirectoryPath,
            ["manifestPath"] = preset.ManifestPath,
            ["dossierPath"] = preset.DossierPath,
            ["dossierMarkdown"] = preset.DossierMarkdown,
            ["promptPackage"] = preset.PromptPackage
        };

    private static void WriteMainMenuJsonAuditPanel(string title, JsonNode node, Color borderColor)
    {
        var json = node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        AnsiConsole.Write(new Panel(new Text(json))
        {
            Header = new PanelHeader($" {Markup.Escape(title)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private static void OpenFolderOrPrintPath(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
        }
        catch
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(directoryPath)}[/]");
            AnsiConsole.MarkupLine("[dim]Не удалось открыть папку автоматически. Путь выведен выше.[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Handles the voluntary end of mortal life — returns the soul to the Chaos Sea.
    /// Collects a brief life summary for Guardian knowledge persistence.
    /// </summary>
    private async Task HandleEndOfLife()
    {
        var confirm = AnsiConsole.Confirm("[yellow]Вы уверены, что хотите завершить смертную жизнь?[/]", false);
        if (!confirm)
            return;

        // Ask for brief life summary (Guardian knowledge persistence)
        AnsiConsole.Write(new Rule("[gold1]📜 Итоги смертной жизни[/]").RuleStyle("gold1"));
        AnsiConsole.MarkupLine("[dim]Опишите кратко, чем запомнилась эта жизнь (или оставьте пустым):[/]");
        var lifeSummary = PromptTextInput("[cyan]Итог:[/]", allowEmpty: true, preserveNewlines: true);

        var autoSummary = BuildLifeSummary(lifeSummary);
        var action =
            "Я осознанно завершаю эту смертную жизнь. " +
            "В этом accepted turn НЕ проводи Оценку Жизни и НЕ переводи душу локально в итог afterlife narration. " +
            "Сначала выполни только canonical lifecycle trigger: запиши game_state/control/life_transitions.json с reason='Voluntary' и кратким summary завершённой жизни. " +
            "После принятого TriggerLifeEnd клиент сам запустит отдельный следующий ход для Оценки Жизни. " +
            $"Краткий итог жизни: {autoSummary}";
        await ProcessPlayerTurn(action);
    }

    private async Task HandleReenterShiningAbode()
    {
        if (!_stateManager.CurrentState.IsInChaosSea)
            return;

        var previousShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var shiningRoot = await TryReadShiningAbodeStateRootAsync();
        if (shiningRoot == null)
            return;

        var rawOwnerStateIssue = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawOwnerStateIssue))
        {
            AnsiConsole.MarkupLine("[red]Состояние Сияющей Обители повреждено; локальный возврат заблокирован fail-closed до repair.[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(rawOwnerStateIssue)}[/]");
            return;
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), "active", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Сияющая Обитель сейчас недоступна для возврата.[/]");
            return;
        }

        if (shiningRoot["preparedIncarnationPackage"] != null)
        {
            AnsiConsole.MarkupLine("[yellow]Возврат в Сияющую Обитель невозможен, пока ожидается bootstrap следующей жизни.[/]");
            return;
        }

        var rawReturnGuard = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
        var guardSemanticState = AfterlifeReturnGuardService.Classify(rawReturnGuard, out var returnGuard);
        if (guardSemanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid)
        {
            AnsiConsole.MarkupLine("[yellow]Возврат в Сияющую Обитель заблокирован, пока клиент не очистит повреждённый или семантически невалидный post-life guard.[/]");
            return;
        }

        if (guardSemanticState == AfterlifeReturnGuardSemanticState.ActiveValid && returnGuard != null)
        {
            AnsiConsole.MarkupLine("[yellow]Сначала должен пройти хотя бы один обычный ход в Море Хаоса после Оценки Жизни.[/]");
            return;
        }

        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        JsonObject? residentRoot = null;
        if (!string.IsNullOrWhiteSpace(residentJson))
        {
            try
            {
                residentRoot = JsonNode.Parse(residentJson) as JsonObject;
            }
            catch
            {
                residentRoot = null;
            }
        }

        JsonObject? guardiansRoot = null;
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansJson))
        {
            try
            {
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
            }
            catch
            {
                guardiansRoot = null;
            }
        }

        var normalizedRoot = ShiningAbodeState.ReenterOrdinaryActiveState(shiningRoot, residentRoot, guardiansRoot);
        var previousSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(previousSoulJson))
        {
            AnsiConsole.MarkupLine("[red]Не удалось подтвердить текущий realm души. Возврат в Сияющую Обитель отменён.[/]");
            return;
        }

        JsonObject soulRoot;
        try
        {
            soulRoot = JsonNode.Parse(previousSoulJson) as JsonObject
                ?? throw new InvalidOperationException("soul_state.json должен быть object root.");
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]soul_state.json повреждён и не позволяет безопасно вернуть душу в Сияющую Обитель.[/]");
            return;
        }

        soulRoot["currentRealm"] = "Shining Abode";
        var nextSoulJson = GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts);
        var reenterPreviewLines = new List<string>
        {
            "[bold yellow]Возврат в активную Сияющую Обитель[/]",
            "",
            "[bold]Тип изменения:[/] client-local coordinated write; GM turn не отправляется.",
            "[bold]Это НЕ Ascension, НЕ New Game+ и НЕ новое воплощение.[/]",
            "[bold]Affected files:[/]",
            $"  • {ShiningAbodeState.StatePath} [dim](ordinary active state normalization)[/]",
            "  • game_state/meta/soul_state.json [dim](currentRealm: Chaos Sea -> Shining Abode)[/]",
            "",
            "[bold]Блокеры уже проверены:[/]",
            "  • shining_abode_state.availability == active",
            "  • preparedIncarnationPackage отсутствует",
            $"  • {AfterlifeReturnGuardService.GuardPath} не содержит активный post-life guard",
            "",
            "[bold]Последствия подтверждения:[/] вы возвращаетесь в уже существующую Обитель; pending GM action не создаётся."
        };
        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", reenterPreviewLines)))
        {
            Header = new PanelHeader(" ✨ Предпросмотр reenter_shining_abode ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        AnsiConsole.Write(new Panel(new Text(new JsonObject
        {
            ["operation"] = "reenter_shining_abode",
            ["gmTurnInvolved"] = false,
            ["before"] = new JsonObject
            {
                ["soulCurrentRealm"] = "Chaos Sea",
                ["shiningAvailability"] = GetNodeString(shiningRoot["availability"]) ?? "unknown"
            },
            ["after"] = new JsonObject
            {
                ["soulCurrentRealm"] = "Shining Abode",
                ["shiningAvailability"] = GetNodeString(normalizedRoot["availability"]) ?? "active"
            },
            ["affectedFiles"] = new JsonArray(
                JsonValue.Create(ShiningAbodeState.StatePath),
                JsonValue.Create("game_state/meta/soul_state.json"))
        }.ToJsonString(JsonOpts)))
        {
            Header = new PanelHeader(" JSON audit ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(1, 1),
            Expand = true
        });

        var confirmReenter = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Подтвердить локальный возврат в Сияющую Обитель?[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("✅ Да, вернуться", "← Отмена"));
        if (!confirmReenter.Contains("Да", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (!await TryCommitCoordinatedGameStateWritesAsync(
                    new CoordinatedGameStateWrite(ShiningAbodeState.StatePath, previousShiningJson, normalizedRoot.ToJsonString(JsonOpts)),
                    new CoordinatedGameStateWrite("game_state/meta/soul_state.json", previousSoulJson, nextSoulJson)))
            {
                AnsiConsole.MarkupLine("[red]Не удалось безопасно зафиксировать возвращение в Сияющую Обитель. Состояние откатилось к предыдущей версии.[/]");
                return;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine("[red]Не удалось безопасно зафиксировать возвращение в Сияющую Обитель.[/]");
            return;
        }

        var returnSyncSummary = await SyncShiningReturnCycleLocalStateAsync();
        await RefreshRuntimeStateAsync();
        GameInterface.RenderShiningAbodeReturnTransition();
        AnsiConsole.MarkupLine("[yellow]✨ Вы возвращаетесь в активную Сияющую Обитель.[/]");
        if (!string.IsNullOrWhiteSpace(returnSyncSummary))
            AnsiConsole.MarkupLine($"[dim]{GameInterface.EscapeMarkup(returnSyncSummary)}[/]");
    }

    private async Task<bool> TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync()
    {
        if (!_stateManager.CurrentState.IsInShiningAbode)
            return false;

        var shiningRoot = await TryReadShiningAbodeStateRootAsync();
        if (shiningRoot == null)
            return false;

        var rawOwnerStateIssue = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawOwnerStateIssue))
        {
            AnsiConsole.MarkupLine("[red]Состояние Сияющей Обители повреждено; локальный выход в Море Хаоса заблокирован fail-closed до repair.[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(rawOwnerStateIssue)}[/]");
            return false;
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), "active", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Сияющая Обитель уже запечатана или недоступна для обычного выхода.[/]");
            return false;
        }

        if (shiningRoot["preparedIncarnationPackage"] != null)
        {
            AnsiConsole.MarkupLine("[yellow]Нельзя покинуть Сияющую Обитель, пока frozen package ожидает bootstrap.[/]");
            return false;
        }

        if (shiningRoot["pendingNativeFactionDiscovery"] is not null)
        {
            AnsiConsole.MarkupLine("[yellow]Нельзя запечатать Сияющую Обитель, пока legacy pendingNativeFactionDiscovery non-null или повреждён. Сначала дождитесь закрытия или repair/refund.[/]");
            AnsiConsole.MarkupLine($"[dim]• {Markup.Escape(ShiningAbodeState.StatePath)}.pendingNativeFactionDiscovery[/]");
            return false;
        }

        var blockingPendingContracts = await GetBlockingShiningPendingContractPathsAsync();
        if (blockingPendingContracts.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Нельзя запечатать Сияющую Обитель, пока есть активные Shining pending contracts. Сначала дождитесь их закрытия или repair.[/]");
            foreach (var path in blockingPendingContracts)
                AnsiConsole.MarkupLine($"[dim]• {Markup.Escape(path)}[/]");
            return false;
        }

        var previousShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var previousSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(previousSoulJson))
        {
            AnsiConsole.MarkupLine("[red]Не удалось подтвердить текущий realm души. Возврат в Море Хаоса отменён.[/]");
            return false;
        }

        JsonObject soulRoot;
        try
        {
            soulRoot = JsonNode.Parse(previousSoulJson) as JsonObject
                ?? throw new InvalidOperationException("soul_state.json должен быть object root.");
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]soul_state.json повреждён и не позволяет безопасно запечатать Сияющую Обитель.[/]");
            return false;
        }

        ShiningAbodeState.SealForChaosSeaReturn(shiningRoot);
        soulRoot["currentRealm"] = "Chaos Sea";
        var nextSoulJson = GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts);
        try
        {
            if (!await TryCommitCoordinatedGameStateWritesAsync(
                    new CoordinatedGameStateWrite(ShiningAbodeState.StatePath, previousShiningJson, shiningRoot.ToJsonString(JsonOpts)),
                    new CoordinatedGameStateWrite("game_state/meta/soul_state.json", previousSoulJson, nextSoulJson)))
            {
                AnsiConsole.MarkupLine("[red]Не удалось безопасно зафиксировать возвращение в Море Хаоса. Состояние откатилось к предыдущей версии.[/]");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine("[red]Не удалось безопасно зафиксировать возвращение в Море Хаоса.[/]");
            return false;
        }

        await RefreshRuntimeStateAsync();
        return true;
    }

    private async Task<IReadOnlyList<string>> GetBlockingShiningPendingContractPathsAsync()
    {
        var paths = new[]
        {
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath
        };

        var blockingPaths = new List<string>();
        foreach (var path in paths)
        {
            var state = await ClassifyRequestsPendingFileAsync(path);
            if (state == RequestsPendingFileState.ActiveOrMalformed)
                blockingPaths.Add(await DescribeBlockingShiningPendingContractAsync(path));
            else if (state == RequestsPendingFileState.ValidEmpty)
                _fs.DeleteFile(path);
        }

        return blockingPaths;
    }

    private async Task<string> DescribeBlockingShiningPendingContractAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return $"{path}: empty/malformed Shining pending contract\n  закрытие: repair or remove the malformed file before Soul Gates";

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return $"{path}: malformed Shining pending contract root\n  закрытие: repair JSON object before Soul Gates";

            var closure = DescribeShiningPendingClosure(path);
            var payloads = root["requests"] is JsonArray requests && requests.Count > 0
                ? requests.Select((node, index) => (Node: node as JsonObject, Index: (int?)index)).ToArray()
                : new[] { (Node: root, Index: (int?)null) };

            var lines = new List<string>
            {
                $"{path}: active Shining pending contract blocks Soul Gates",
                $"  закрытие: {closure}"
            };

            foreach (var (node, index) in payloads)
            {
                if (node == null)
                {
                    lines.Add($"  request[{index ?? 0}]: malformed request entry");
                    continue;
                }

                var requestLabel = index.HasValue ? $"requests[{index.Value}]" : "root";
                var summary = BuildShiningPendingBlockerIdentitySummary(node);
                lines.Add($"  {requestLabel}: {(string.IsNullOrWhiteSpace(summary) ? "inspect full payload" : summary)}");
                lines.Add($"  {requestLabel} full payload: {node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)}");
            }

            return string.Join("\n", lines);
        }
        catch
        {
            return $"{path} (malformed or unreadable)";
        }
    }

    private static string DescribeShiningPendingClosure(string path) =>
        path switch
        {
            _ when string.Equals(path, ShiningCoreActionRequestState.PendingActionsRequestPath, StringComparison.OrdinalIgnoreCase) =>
                "shining_abode_state.coreActionReceipts[] + exact canonical state projection; receipt echoes actionType, target ids, quoted costs, selected ids and generated ids",
            _ when string.Equals(path, ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) =>
                "faction tradeInventory + tradeInventoryReceipts[] with derived tier/slots/rarity/service multiplier and unique relic ids",
            _ when string.Equals(path, ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) =>
                "halls[]/factions[] materialization + factionFoundingReceipts[] + supporter resident alignment",
            _ when string.Equals(path, ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase) =>
                "resident Shining faction fields + factionRealignmentReceipts[]",
            _ when string.Equals(path, ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, StringComparison.OrdinalIgnoreCase) =>
                "faction.leadership + leadershipReceipts[] + leadershipHistory[] + radiant actor registry if applicable",
            _ => "matching Shining receipt/canonical state projection"
        };

    private static string BuildShiningPendingBlockerIdentitySummary(JsonObject payload)
    {
        var keys = new[]
        {
            "requestId", "actionType", "status", "factionId", "factionName", "sourceFactionId", "targetFactionId",
            "projectId", "projectDisplayName", "relicId", "relicName", "returnCycleId", "targetFormTag",
            "residentId", "residentName", "actorId", "candidateActorId", "transitionMode",
            "proposedFactionId", "proposedHallId", "proposedHallName", "supportingResidentIds",
            "quotedCostFeathers", "quotedCostLightSparks", "costFeathers", "costLightSparks",
            "derivedTradeTier", "derivedTradeSlotCount", "derivedRarityCeiling", "derivedServiceMultiplier",
            "radianceTierAtRequest", "projectedGachaBonusSteps", "sourceDraftVersion", "selectedCardIds",
            "createdAtTurn", "createdAtUtc"
        };

        var parts = new List<string>();
        foreach (var key in keys)
        {
            var value = FormatShiningPendingBlockerValue(payload[key]);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{key}={value}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatShiningPendingBlockerValue(JsonNode? node)
    {
        if (node == null)
            return string.Empty;

        if (node is JsonArray array)
        {
            var values = array
                .Select(FormatShiningPendingBlockerValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return values.Length == 0 ? string.Empty : $"[{string.Join(", ", values)}]";
        }

        if (node is JsonObject)
            return "{...}";

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
            if (value.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (value.TryGetValue<long>(out var longValue))
                return longValue.ToString();
            if (value.TryGetValue<double>(out var doubleValue))
                return doubleValue.ToString("0.###");
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue ? "true" : "false";
        }

        return string.Empty;
    }

    private enum RequestsPendingFileState
    {
        Absent,
        ValidEmpty,
        ActiveOrMalformed
    }

    private async Task<RequestsPendingFileState> ClassifyRequestsPendingFileAsync(string path)
    {
        if (!_fs.FileExists(path))
            return RequestsPendingFileState.Absent;

        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return RequestsPendingFileState.ActiveOrMalformed;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("requests", out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return RequestsPendingFileState.ActiveOrMalformed;
            }

            return requestsNode.GetArrayLength() == 0
                ? RequestsPendingFileState.ValidEmpty
                : RequestsPendingFileState.ActiveOrMalformed;
        }
        catch
        {
            return RequestsPendingFileState.ActiveOrMalformed;
        }
    }

    private async Task HandleReturnToChaosSeaFromShiningAbode()
    {
        if (!await TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync())
            return;

        GameInterface.RenderRealmTransition(true);
        AnsiConsole.MarkupLine("[blue]🌊 Сияющая Обитель запечатана. Вы возвращаетесь в Море Хаоса.[/]");
    }

    private async Task HandleNewGamePlus()
    {
        if (!_stateManager.CurrentState.IsInShiningAbode)
            return;

        var confirm = AnsiConsole.Confirm("[yellow]Начать Новый Цикл? Просветление и Чернильные Перья будут сброшены. Реликвии Души и Хранители сохранятся.[/]", false);
        if (!confirm)
            return;

        try
        {
            if (!await ExecuteNewGamePlusResetAsync())
            {
                AnsiConsole.MarkupLine("[red]Не удалось безопасно начать Новый Цикл. Исходное состояние восстановлено.[/]");
                return;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine("[red]Не удалось безопасно завершить подготовку Нового Цикла.[/]");
            return;
        }

        GameInterface.RenderRealmTransition(true);
        AnsiConsole.MarkupLine("[yellow]✨ Новый Цикл начался. Вы снова в Море Хаоса.[/]");
    }

    private async Task<bool> ExecuteNewGamePlusResetAsync()
    {
        var backupPath = CreateGameSessionSafetyBackup("new-game-plus");
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
            var achievementsJson = await _fs.ReadFileAsync("game_state/meta/achievements.json");
            var codexJson = await _fs.ReadFileAsync("lore/codex_entries.json");
            var chronicleJson = await _fs.ReadFileAsync("lore/chaos_sea/player_chronicle.json");
            var cosmologyJson = await _fs.ReadFileAsync("lore/chaos_sea/cosmology.json");
            var soulLoreJson = await _fs.ReadFileAsync("lore/chaos_sea/soul_system_lore.json");
            var guardiansLoreJson = await _fs.ReadFileAsync("lore/chaos_sea/guardians_lore.json");

            var soulName = _stateManager.CurrentState.SoulName;
            object previousSoulNames = Array.Empty<string>();
            object soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() };
            object afterlifeArchive = new { stored = Array.Empty<object>() };
            object livesHistory = Array.Empty<object>();
            object? soulImprint = null;

            if (!string.IsNullOrWhiteSpace(soulJson))
            {
                using var doc = JsonDocument.Parse(soulJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("soulName", out var sn) && sn.ValueKind == JsonValueKind.String)
                    soulName = sn.GetString() ?? soulName;
                if (root.TryGetProperty("previousSoulNames", out var prevSoulNames))
                    previousSoulNames = JsonSerializer.Deserialize<object>(prevSoulNames.GetRawText()) ?? previousSoulNames;
                if (root.TryGetProperty("soulRelics", out var relics))
                    soulRelics = JsonSerializer.Deserialize<object>(relics.GetRawText()) ?? soulRelics;
                if (root.TryGetProperty("afterlifeArchive", out var archive))
                    afterlifeArchive = JsonSerializer.Deserialize<object>(archive.GetRawText()) ?? afterlifeArchive;
                if (root.TryGetProperty("livesHistory", out var history))
                    livesHistory = JsonSerializer.Deserialize<object>(history.GetRawText()) ?? livesHistory;
                if (root.TryGetProperty("soulImprint", out var imprint))
                    soulImprint = JsonSerializer.Deserialize<object>(imprint.GetRawText());
            }

            _fs.ClearGameState();

            var newSessionId = Guid.NewGuid().ToString();
            var resetSoulState = new Dictionary<string, object?>
            {
                ["soulName"] = soulName,
                ["previousSoulNames"] = previousSoulNames,
                ["currentRealm"] = "Chaos Sea",
                ["currentIncarnation"] = 0,
                ["enlightenment"] = new { currentTier = "Новичок", experience = 0, level = 0 },
                ["inkFeathers"] = new { current = 0, total = 0 },
                ["soulRelics"] = soulRelics,
                ["afterlifeArchive"] = afterlifeArchive,
                ["livesHistory"] = livesHistory,
                ["pendingMemoryLegacy"] = null
            };
            if (soulImprint != null)
                resetSoulState["soulImprint"] = soulImprint;

            await WriteCanonicalSoulStateAsync(resetSoulState);
            _afterlifeArchiveCandidateService.Clear();
            if (!string.IsNullOrWhiteSpace(guardiansJson))
                await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansJson);
            if (!string.IsNullOrWhiteSpace(achievementsJson))
                await _fs.WriteFileAtomicAsync("game_state/meta/achievements.json", achievementsJson);
            if (!string.IsNullOrWhiteSpace(codexJson))
                await _fs.WriteFileAtomicAsync("lore/codex_entries.json", codexJson);
            if (!string.IsNullOrWhiteSpace(chronicleJson))
                await _fs.WriteFileAtomicAsync("lore/chaos_sea/player_chronicle.json", chronicleJson);
            if (!string.IsNullOrWhiteSpace(cosmologyJson))
                await _fs.WriteFileAtomicAsync("lore/chaos_sea/cosmology.json", cosmologyJson);
            if (!string.IsNullOrWhiteSpace(soulLoreJson))
                await _fs.WriteFileAtomicAsync("lore/chaos_sea/soul_system_lore.json", soulLoreJson);
            if (!string.IsNullOrWhiteSpace(guardiansLoreJson))
                await _fs.WriteFileAtomicAsync("lore/chaos_sea/guardians_lore.json", guardiansLoreJson);

            var chatLog = new
            {
                sessionId = newSessionId,
                startedAt = DateTime.UtcNow.ToString("o"),
                turns = Array.Empty<object>()
            };
            await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", JsonSerializer.Serialize(chatLog, JsonOpts));

            _gameLoop.SetSession(newSessionId, 0);
            await _storyService.AppendMarkerAsync("Chaos Sea", 0, "NEW_GAME_PLUS", "Начат Новый Цикл после Вознесения. Просветление сброшено, Реликвии Души и Хранители сохранены.");
            await RefreshRuntimeStateAsync();
            CleanupGameSessionSafetyBackup(backupPath);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex);
            try
            {
                RestoreGameSessionSafetyBackup(backupPath);
                await RefreshRuntimeStateAsync();
                return false;
            }
            catch (Exception restoreEx)
            {
                throw new InvalidOperationException("Не удалось восстановить исходное game_session после сбоя New Game Plus.", restoreEx);
            }
            finally
            {
                CleanupGameSessionSafetyBackup(backupPath);
            }
        }
    }

    private string CreateGameSessionSafetyBackup(string operationTag)
    {
        var backupRoot = Path.Combine(
            Path.GetTempPath(),
            $"boe-game-session-backup-{operationTag}-{Guid.NewGuid():N}");
        CopyDirectoryRecursive(_fs.GameSessionPath, backupRoot);
        return backupRoot;
    }

    private void RestoreGameSessionSafetyBackup(string backupRoot)
    {
        var targetRoot = _fs.GameSessionPath;
        if (Directory.Exists(targetRoot))
            Directory.Delete(targetRoot, recursive: true);

        CopyDirectoryRecursive(backupRoot, targetRoot);
    }

    private static void CleanupGameSessionSafetyBackup(string backupRoot)
    {
        try
        {
            if (Directory.Exists(backupRoot))
                Directory.Delete(backupRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destinationChild = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectoryRecursive(directory, destinationChild);
        }
    }

    private async Task<JsonObject?> TryReadShiningAbodeStateRootAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            AnsiConsole.MarkupLine("[yellow]Не найден state Сияющей Обители.[/]");
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]Состояние Сияющей Обители повреждено и не может быть прочитано.[/]");
            return null;
        }
    }

    private async Task<JsonObject?> TryReadSoulStateRootAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteJsonObjectAsync(string path, JsonObject root)
    {
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
    }

    private async Task<string?> SyncShiningReturnCycleLocalStateAsync()
    {
        var shiningRoot = await TryReadShiningAbodeStateRootAsync();
        var soulRoot = await TryReadSoulStateRootAsync();
        if (shiningRoot == null || soulRoot == null)
            return null;

        JsonObject? residentRoot = null;
        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (!string.IsNullOrWhiteSpace(residentJson))
        {
            try
            {
                residentRoot = JsonNode.Parse(residentJson) as JsonObject;
            }
            catch
            {
                residentRoot = null;
            }
        }

        JsonObject? guardiansRoot = null;
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(guardiansJson))
        {
            try
            {
                guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
            }
            catch
            {
                guardiansRoot = null;
            }
        }

        if (ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot) != null)
            return null;

        var preNormalizationShiningRoot = shiningRoot.DeepClone() as JsonObject;
        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var stateChanged = preNormalizationShiningRoot != null && !JsonNode.DeepEquals(preNormalizationShiningRoot, shiningRoot);
        var cycleChanged = false;
        stateChanged |= ShiningAbodeState.SyncShiningReturnCycle(
            shiningRoot,
            Math.Max(0, ReadIntNode(soulRoot["currentIncarnation"])),
            out cycleChanged);

        if (stateChanged)
            await WriteJsonObjectAsync(ShiningAbodeState.StatePath, shiningRoot);

        var autoRefresh = await ShiningTradeService.SyncAutoRefreshRequestsForCurrentCycleAsync(
            _fs,
            Math.Max(1, _gameLoop.TurnNumber + 1));
        var parts = new List<string>();
        if (cycleChanged)
            parts.Add($"Синхронизирован сияющий return-cycle {ShiningAbodeState.ResolveShiningReturnCycleId(shiningRoot, soulRoot)}: попытки banner-gacha сброшены.");
        if (autoRefresh.CreatedRequestCount > 0)
            parts.Add($"Автоматически запрошено сияющих витрин: {autoRefresh.CreatedRequestCount}.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text ?? "";

        return "";
    }

    private static int ReadIntNode(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
            return intValue;

        return 0;
    }

    /// <summary>
    /// Builds a GameResponse by reading from the individual output files that the GM daemon writes.
    /// Reads: output/narrative_response.json, output/interface_updates.json, output/debug_logs.json
    /// </summary>

    private async Task LoadGameFlow()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]📂 Загрузка игры[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        var allSaves = new List<SaveInfo>();

        // Collect from all save dirs
        foreach (var dir in new[] { "saves/manual_saves", "saves/autosaves", "saves/checkpoint_saves" })
        {
            allSaves.AddRange(await _saveLoad.GetAvailableSavesAsync(dir));
        }

        if (allSaves.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("no_saves")}[/]");
            AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);
            return;
        }

        var choices = allSaves.Select(s =>
        {
            var meta = s.Metadata;
            var name = meta?.SaveName ?? Path.GetFileNameWithoutExtension(s.FileName);
            var turn = meta?.TurnNumber ?? 0;
            var loc = meta?.CurrentLocation ?? "?";
            var date = meta?.Timestamp.ToString("dd.MM.yyyy HH:mm") ?? "?";
            var size = s.FileSize / 1024;
            return $"{name} | Ход {turn} | {loc} | {date} | {size}KB";
        }).Append(_loc.T("back")).ToArray();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Выберите сохранение:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .PageSize(15)
                .AddChoices(choices));

        if (selected == _loc.T("back")) return;

        var idx = Array.IndexOf(choices, selected);
        if (idx < 0 || idx >= allSaves.Count) return;

        var saveInfo = allSaves[idx];

        var success = await _saveLoad.LoadGameAsync(saveInfo.FileName);
        if (success)
        {
            AnsiConsole.MarkupLine($"[green]{_loc.T("load_success")}[/]");

            if (saveInfo.Metadata != null)
            {
                _gameLoop.SetSession(
                    _stateManager.CurrentState.SessionId,
                    saveInfo.Metadata.TurnNumber);
            }

            await Task.Delay(1000);

            // Ensure game settings (difficulty) are synced to game_state for GM
            await WriteGameSettingsForGm();
            await NormalizeRuntimeUiArtifactsAsync();

            // Build response from saved output files for initial display
            _lastResponse = await BuildGameResponseFromFiles();
            if (!await ValidateCurrentGameStateOrShowErrorsAsync("загрузки сохранения"))
                return;

            await EnterGameLoop();
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{_loc.T("load_failed")}[/]");
            Console.ReadKey(true);
        }
    }

}

