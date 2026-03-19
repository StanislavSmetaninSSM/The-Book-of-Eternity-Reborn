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

/// <summary>
/// Main game orchestrator. Coordinates all subsystems:
/// Menu → Game Loop → UI → State Management → Save/Load
/// </summary>
public class GameEngine
{
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly GameLoop _gameLoop;
    private readonly CanonicalStateNormalizer _normalizer;
    private readonly ProgressionScheduleService _progressionSchedule;
    private readonly GameInterface _ui;
    private readonly ExplorerMode _explorer;
    private readonly LocalizationManager _loc;
    private readonly SaveLoadService _saveLoad;
    private readonly ImageService _imageService;
    private readonly ValidationService _validator;
    private readonly CharacteristicsService _charService;
    private readonly StoryService _storyService;
    private readonly AudioService _audioService;
    private readonly ConsoleAppearanceService _consoleAppearance;
    private readonly SystemModService _systemModService;
    private readonly CriticalStateHealthService _criticalStateHealth;
    private readonly WorldDirectiveService _worldDirectiveService;
    private readonly AfterlifeReturnGuardService _afterlifeReturnGuardService;
    private readonly PendingTurnStateService _pendingTurnState;
    private readonly QteSceneService _qteSceneService;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<GameEngine> _logger;

    private bool _isRunning;
    private bool _inGame;
    private GameResponse? _lastResponse;
    private string? _pendingImagePrompt;
    private int _lastConsoleWidth;
    private int _lastKnownLevel = 1;
    private bool _pendingMemoryLegacyAwaitingConsumption;
    private string? _mainMenuSessionWarning;

    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string ValidationRepairRequestPath = "game_state/control/validation_repair_request.json";
    private const string ValidationRepairReadyPath = "game_state/control/validation_repair_ready.json";
    private const string TerminalProtocolFailureRequestPath = "game_state/control/terminal_protocol_failure_request.json";
    private const string OrdinaryPlayerTurnSourceLabel = "обработки хода";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public GameEngine(
        FileSystemManager fs, StateManager stateManager, GameLoop gameLoop,
        CanonicalStateNormalizer normalizer, ProgressionScheduleService progressionSchedule,
        GameInterface ui, ExplorerMode explorer,
        LocalizationManager loc, SaveLoadService saveLoad, ImageService imageService,
        ValidationService validator, CharacteristicsService charService,
        StoryService storyService, AudioService audioService, ConsoleAppearanceService consoleAppearance,
        SystemModService systemModService,
        CriticalStateHealthService criticalStateHealth,
        WorldDirectiveService worldDirectiveService,
        AfterlifeReturnGuardService afterlifeReturnGuardService,
        PendingTurnStateService pendingTurnState,
        QteSceneService qteSceneService,
        IClipboardService clipboardService,
        ILogger<GameEngine> logger)
    {
        _fs = fs;
        _stateManager = stateManager;
        _gameLoop = gameLoop;
        _normalizer = normalizer;
        _progressionSchedule = progressionSchedule;
        _ui = ui;
        _explorer = explorer;
        _loc = loc;
        _saveLoad = saveLoad;
        _imageService = imageService;
        _validator = validator;
        _charService = charService;
        _storyService = storyService;
        _audioService = audioService;
        _consoleAppearance = consoleAppearance;
        _systemModService = systemModService;
        _criticalStateHealth = criticalStateHealth;
        _worldDirectiveService = worldDirectiveService;
        _afterlifeReturnGuardService = afterlifeReturnGuardService;
        _pendingTurnState = pendingTurnState;
        _qteSceneService = qteSceneService;
        _clipboardService = clipboardService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _isRunning = true;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
            Console.InputEncoding = System.Text.Encoding.Unicode;
        else
            Console.InputEncoding = System.Text.Encoding.UTF8;

        // Ensure file system structure
        _fs.EnsureDirectoryStructure();

        // Load settings
        await _stateManager.LoadSettingsAsync();
        await _stateManager.EnsureSettingsFileExistsAsync();
        _loc.CurrentLanguage = _stateManager.Settings.Language;
        _consoleAppearance.ApplyConfiguredFontSize();
        await _audioService.ApplySettingsAsync();
        await EnsureClientOwnedSystemFilesHealthyAsync();

        while (_isRunning)
        {
            try
            {
                await ShowMainMenu();
            }
            catch (Exception ex)
            {
                LogError(ex);
                AnsiConsole.MarkupLine($"[red]❌ Ошибка: {GameInterface.EscapeMarkup(ex.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Ошибка сохранена в game_session/error_log.txt. Игра продолжает работу.[/]");
                AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
                Console.ReadKey(true);
            }
        }

        await _audioService.StopAllAsync();
    }

    // ═══════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════

    private sealed record MainMenuOption(string Key, string Title, string Description, string AccentColor, int Index);
    private sealed record OptionsMenuEntry(string Key, string Label);
    private sealed record MenuChoiceItem(string Key, string Label, string? Description = null, string AccentColor = "cyan1");
    private enum MainMenuLayoutMode { VeryCompact, Compact, Medium, Wide }

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
        await RefreshCanonicalStateAsync();
        var state = _stateManager.CurrentState;
        var turnNumber = await DetectCurrentSessionTurnNumberAsync();

        var primaryName = !string.IsNullOrWhiteSpace(state.CharacterName)
            ? state.CharacterName
            : !string.IsNullOrWhiteSpace(state.SoulName)
                ? state.SoulName
                : _loc.T("main_menu_continue_desc");

        var realm = state.IsInShiningAbode
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
        var pendingManifest = await LoadPendingTurnSnapshotManifestAsync();
        var hasPendingTerminalSignal = _fs.FileExists("ready/turn_complete.json") || _fs.FileExists("ready/turn_error.json");

        await RefreshCanonicalStateAsync();
        var state = _stateManager.CurrentState;
        var sessionId = !string.IsNullOrWhiteSpace(state.SessionId)
            ? state.SessionId
            : Guid.NewGuid().ToString();
        var turnNumber = await DetectCurrentSessionTurnNumberAsync();
        _gameLoop.SetSession(sessionId, turnNumber);

        if (string.IsNullOrWhiteSpace(_lastResponse?.Response) &&
            (pendingManifest != null || _fs.FileExists("ready/turn_complete.json")))
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

        string guardianDescription;
        if (guardianChoice == _loc.T("create_guardian"))
        {
            guardianDescription = PromptTextInput($"[cyan]{_loc.T("guardian_prompt")}[/]",
                allowEmpty: false,
                emptyError: "Описание не может быть пустым",
                preserveNewlines: true);
        }
        else
        {
            guardianDescription = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Выберите тип Хранителя:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(
                        "🔮 Хранитель Магии — мудрый маг, знаток тайных искусств",
                        "⚔️ Хранитель Битвы — закалённый воин, мастер клинка",
                        "🔨 Хранитель Ремесла — искусный мастер, создатель чудес",
                        "🎭 Хранитель Социума — дипломат и знаток душ",
                        "✨ Хранитель Света — духовный наставник, целитель",
                        "🌑 Хранитель Тьмы — загадочный проводник через тени"
                    ));
        }

        // Step 3: Enter the Chaos Sea — NO character/world description at this point
        // The mortal world is NOT described at the start. Player enters it later through incarnation.
        await InitializeChaosSea(soulName, guardianDescription);

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
    private async Task InitializeChaosSea(string soulName, string guardianDesc)
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
                    livesHistory = Array.Empty<object>(),
                    pendingMemoryLegacy = (object?)null
                };
                _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
                    JsonSerializer.Serialize(soulState, JsonOpts)).Wait();

                // Initialize guardian
                var guardian = new
                {
                    guardians = Array.Empty<object>(),
                    pendingGuardianCreation = new
                    {
                        description = guardianDesc,
                        soulName
                    }
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
        await RefreshCanonicalStateAsync();

        // Write game settings (difficulty flags) for GM
        await WriteGameSettingsForGm();

        // Send initial turn to GM — soul awakens in the Chaos Sea, not in a mortal world
        var firstAction = $"Душа по имени «{soulName}» пробуждается в Море Хаоса. " +
                          $"Хранитель: {guardianDesc}. " +
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

        // Character description
        AnsiConsole.MarkupLine("[cyan]Опишите персонажа в смертной жизни:[/]");
        AnsiConsole.MarkupLine("[dim](Раса, класс, внешность, предыстория... или оставьте пустым)[/]");
        var charDesc = PromptTextInput("[cyan]Персонаж:[/]", allowEmpty: true, preserveNewlines: true);

        AnsiConsole.WriteLine();

        // Each incarnation must create a fresh mortal-world lore set.
        _fs.ClearCurrentWorldLore();

        // World description
        AnsiConsole.MarkupLine("[cyan]Опишите мир, в который хотите воплотиться:[/]");
        AnsiConsole.MarkupLine("[dim](Жанр, сеттинг, особенности... или оставьте пустым — Хранитель выберет)[/]");
        var worldDesc = PromptTextInput("[cyan]Мир:[/]", allowEmpty: true, preserveNewlines: true);

        AnsiConsole.WriteLine();

        // Starting circumstances
        AnsiConsole.MarkupLine("[cyan]Обстоятельства начала (необязательно):[/]");
        AnsiConsole.MarkupLine("[dim](Где вы появляетесь? Что происходит вокруг?)[/]");
        var circumstances = PromptTextInput("[cyan]Обстоятельства:[/]", allowEmpty: true, preserveNewlines: true);

        // Build incarnation action
        var parts = new List<string> { "Душа входит через Врата Души и воплощается в смертную жизнь." };

        if (!string.IsNullOrWhiteSpace(charDesc))
            parts.Add($"Персонаж: {charDesc}.");
        if (!string.IsNullOrWhiteSpace(worldDesc))
            parts.Add($"Мир: {worldDesc}.");
        if (!string.IsNullOrWhiteSpace(circumstances))
            parts.Add($"Обстоятельства начала: {circumstances}.");
        if (string.IsNullOrWhiteSpace(charDesc) && string.IsNullOrWhiteSpace(worldDesc))
            parts.Add("Хранитель выбирает мир и обстоятельства рождения для души.");

        await _worldDirectiveService.UpsertPendingSetupFromIncarnationPromptAsync(worldDesc, circumstances);

        var action =
            string.Join(" ", parts) +
            " В этом accepted turn не переключай душу локально в Mortal World и не создавай первый mortal bootstrap. " +
            "Сначала выполни только canonical TriggerIncarnation в game_state/control/incarnation_trigger.json, используя pending incarnation_world_setup как входной контракт. " +
            "После принятого TriggerIncarnation клиент сам выполнит локальный переход и запустит отдельный следующий ход для первого Mortal World bootstrap.";

        await ProcessPlayerTurn(action);
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

    private async Task HandleNewGamePlus()
    {
        if (!_stateManager.CurrentState.IsInShiningAbode)
            return;

        var confirm = AnsiConsole.Confirm("[yellow]Начать Новый Цикл? Просветление и Чернильные Перья будут сброшены. Реликвии Души и Хранители сохранятся.[/]", false);
        if (!confirm)
            return;

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
        object livesHistory = Array.Empty<object>();
        object? crossIncarnationData = null;
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
            if (root.TryGetProperty("livesHistory", out var history))
                livesHistory = JsonSerializer.Deserialize<object>(history.GetRawText()) ?? livesHistory;
            if (root.TryGetProperty("crossIncarnationData", out var crossData))
                crossIncarnationData = JsonSerializer.Deserialize<object>(crossData.GetRawText());
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
            ["livesHistory"] = livesHistory,
            ["pendingMemoryLegacy"] = null
        };
        if (crossIncarnationData != null)
            resetSoulState["crossIncarnationData"] = crossIncarnationData;
        if (soulImprint != null)
            resetSoulState["soulImprint"] = soulImprint;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", JsonSerializer.Serialize(resetSoulState, JsonOpts));
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
        await RefreshCanonicalStateAsync();
        GameInterface.RenderRealmTransition(true);
        AnsiConsole.MarkupLine("[yellow]✨ Новый Цикл начался. Вы снова в Море Хаоса.[/]");
    }

    /// <summary>
    /// Builds a GameResponse by reading from the individual output files that the GM daemon writes.
    /// Reads: output/narrative_response.json, output/interface_updates.json, output/debug_logs.json
    /// </summary>
    private async Task<GameResponse> BuildGameResponseFromFiles()
    {
        var response = new GameResponse();

        // 1. Read narrative from output/narrative_response.json (primary source per API spec)
        var narrativeJson = await _fs.ReadFileAsync("output/narrative_response.json");
        if (narrativeJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(narrativeJson);
                if (doc.RootElement.TryGetProperty("response", out var r))
                    response.Response = r.GetString();
            }
            catch { }
        }

        // Fallback: use narrative from state if not in output file
        if (string.IsNullOrEmpty(response.Response))
            response.Response = _stateManager.CurrentState.Narrative;

        // 2. Read dialogue options and image prompt from output/interface_updates.json
        var uiJson = await _fs.ReadFileAsync("output/interface_updates.json");
        if (uiJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(uiJson);
                if (doc.RootElement.TryGetProperty("dialogueOptions", out var opts) &&
                    opts.ValueKind == JsonValueKind.Array)
                {
                    response.DialogueOptions = JsonSerializer.Deserialize<DialogueOption[]>(opts.GetRawText());
                }
                if (doc.RootElement.TryGetProperty("image_prompt", out var img))
                    response.ImagePrompt = img.GetString();
            }
            catch { }
        }

        // 3. Read GM thoughts from output/debug_logs.json
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (debugJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(debugJson);
                if (doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gm))
                    response.GmThoughtsMarkdown = gm.GetString();
            }
            catch { }
        }

        // 4. Read combat log from distributed combat state if exists
        var combatJson = await _fs.ReadFileAsync("game_state/combat/combat_log.json");
        if (combatJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(combatJson);
                if (doc.RootElement.TryGetProperty("combat_log_markdown", out var cl))
                    response.CombatLogMarkdown = cl.GetString();
            }
            catch { }
        }

        // 5. Populate status from state
        var st = _stateManager.CurrentState.PlayerStatus;
        response.PlayerStatus = new PlayerStatus
        {
            HealthPercentage = st.HealthPercentage,
            EnergyPercentage = st.EnergyPercentage,
            PoisePercentage = st.PoisePercentage,
            CurrentCondition = st.CurrentCondition
        };

        return response;
    }

    private GameResponse MergeWithLastResponse(GameResponse? refreshed)
    {
        return GameResponseRefreshMerger.Merge(_lastResponse, refreshed);
    }

    private async Task RefreshCanonicalStateAsync(IReadOnlyDictionary<string, string>? backups = null)
    {
        await _normalizer.NormalizeAccumulatedStateAsync(backups);
        await _stateManager.RefreshGameStateAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private sealed class PendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private sealed class RollbackSnapshot
    {
        public Dictionary<string, string> BackupFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BaselineFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ValidationRepairRequest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string Source { get; set; } = "";
        public string DetectedAtUtc { get; set; } = "";
        public int RevalidationAttempt { get; set; }
        public string GmInstructions { get; set; } = "";
        public List<string> SummaryGroups { get; set; } = new();
        public List<ValidationRepairIssue> Errors { get; set; } = new();
    }

    private sealed class ValidationRepairIssue
    {
        public string Code { get; set; } = "validation_error";
        public string FilePath { get; set; } = "";
        public string Severity { get; set; } = "Error";
        public string Category { get; set; } = IssueCategory.StateConsistency.ToString();
        public string Message { get; set; } = "";
        public string? Actor { get; set; }
        public string? Section { get; set; }
        public string? Expected { get; set; }
        public string? Actual { get; set; }
        public string? RepairHint { get; set; }
    }

    private sealed class ValidationRepairReady
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string UpdatedAtUtc { get; set; } = "";
        public string? Note { get; set; }
    }

    private sealed class TerminalProtocolFailureRequest
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string Source { get; set; } = "";
        public string DetectedAtUtc { get; set; } = "";
        public string GmInstructions { get; set; } = "";
        public List<string> SummaryGroups { get; set; } = new();
        public List<ValidationRepairIssue> Errors { get; set; } = new();
    }

    private sealed class ReadySignalMetadata
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string Status { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string? Error { get; set; }
        public bool HasFilesModified { get; set; }
        public bool FilesModifiedValid { get; set; }
    }

    private sealed class ActiveTerminalOutcomeResolution
    {
        public string Kind { get; set; } = "failure";
        public ReadySignalMetadata? Signal { get; set; }
    }

    private async Task<Dictionary<string, string>> CreateCanonicalBaselineSnapshotAsync(TurnRequest request,
        RollbackSnapshot? rollbackSnapshot = null,
        string? sourceLabel = null)
    {
        await DeleteTerminalProtocolFailureRequestAsync();
        await CleanupPendingTurnSnapshotAsync();

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync();
        foreach (var file in CanonicalStateNormalizer.CanonicalAccumulatedFiles)
        {
            var content = await _fs.ReadFileAsync(file);
            if (content == null) continue;

            var snapshotPath = $"{PendingTurnSnapshotDirectory}/{file}";
            await _fs.WriteFileAtomicAsync(snapshotPath, content);
            files[file] = snapshotPath;
            snapshotHashes[file] = ComputeSha256(content);
        }

        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            TurnNumber = request.TurnNumber,
            RequestTimestamp = request.Timestamp,
            PlayerAction = request.PlayerAction,
            ProgressionControl = request.ProgressionControl,
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = clientOwnedValidationHashes,
            RollbackBackups = rollbackSnapshot != null
                ? new Dictionary<string, string>(rollbackSnapshot.BackupFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = rollbackSnapshot?.BaselineFiles.ToList() ?? new List<string>(),
            SourceLabel = sourceLabel
        };
        manifest.ManifestPayloadHash = ComputePendingTurnManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, JsonOpts));

        return files;
    }

    private async Task<Dictionary<string, string>> CaptureClientOwnedValidationHashesAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/history/chat_log.json"] = await ReadFileHashOrEmptyAsync("game_state/history/chat_log.json")
        };

        foreach (var storyPath in EnumerateStoryContinuityFiles())
            result[storyPath] = await ReadFileHashOrEmptyAsync(storyPath);

        return result;
    }

    private async Task<string> ReadFileHashOrEmptyAsync(string relativePath)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        return content == null ? string.Empty : ComputeSha256(content);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles()
    {
        var sessionRoot = _fs.ResolvePath("");
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            yield break;

        foreach (var absoluteFile in Directory.EnumerateFiles(storiesRoot, "*.jsonl", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(sessionRoot, absoluteFile).Replace('\\', '/');
    }

    private async Task<Dictionary<string, string>?> LoadCanonicalBaselineSnapshotAsync(int expectedTurnNumber)
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        if (manifest == null)
            return null;

        if (!string.Equals(manifest.SessionId, _gameLoop.SessionId, StringComparison.OrdinalIgnoreCase))
            return null;

        if (manifest.TurnNumber != expectedTurnNumber)
            return null;

        return manifest.Files
            .Where(kv => _fs.FileExists(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PendingTurnSnapshotManifest?> LoadPendingTurnSnapshotManifestAsync()
    {
        var json = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось загрузить pending turn snapshot manifest");
            return null;
        }
    }

    private string ComputePendingTurnManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = "";
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private async Task CleanupPendingTurnSnapshotAsync()
    {
        var json = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(json, JsonOpts);
                if (manifest?.Files != null)
                {
                    foreach (var snapshotPath in manifest.Files.Values)
                    {
                        if (_fs.FileExists(snapshotPath))
                            _fs.DeleteFile(snapshotPath);
                    }
                }

                if (manifest?.RollbackBackups != null)
                {
                    foreach (var rollbackPath in manifest.RollbackBackups.Values)
                    {
                        if (_fs.FileExists(rollbackPath))
                            _fs.DeleteFile(rollbackPath);
                    }
                }
            }
            catch { }
        }

        if (_fs.FileExists(PendingTurnSnapshotManifestPath))
            _fs.DeleteFile(PendingTurnSnapshotManifestPath);
    }

    private static bool HasRollbackCapability(RollbackSnapshot? snapshot) =>
        snapshot != null && (snapshot.BackupFiles.Count > 0 || snapshot.BaselineFiles.Count > 0);

    private RollbackSnapshot? GetRollbackSnapshot(PendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return null;

        var snapshot = new RollbackSnapshot
        {
            BackupFiles = manifest.RollbackBackups
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) &&
                             !string.IsNullOrWhiteSpace(kv.Value) &&
                             _fs.FileExists(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            BaselineFiles = new HashSet<string>(manifest.RollbackBaselineFiles ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase)
        };

        return HasRollbackCapability(snapshot) ? snapshot : null;
    }

    private async Task NormalizePendingRepairArtifactsAsync()
    {
        var repairRequestExists = _fs.FileExists(ValidationRepairRequestPath);
        var repairReadyExists = _fs.FileExists(ValidationRepairReadyPath);
        if (!repairRequestExists && !repairReadyExists)
            return;

        var manifest = await LoadPendingTurnSnapshotManifestAsync();

        if (manifest == null)
        {
            _logger.LogWarning("Найдены repair-файлы без pending snapshot manifest. Очистка как stale state.");
            await DeleteValidationRepairFilesAsync();
            return;
        }

        if (repairReadyExists && !repairRequestExists)
        {
            _logger.LogWarning(
                "Найден orphaned validation_repair_ready для pending turn(session={Session}, request={Request}, turn={Turn}). Удаление ready-файла без затрагивания основного pending turn state.",
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber);
            await DeleteValidationRepairReadyAsync();
            return;
        }

        if (repairRequestExists)
        {
            if (!_fs.FileExists("ready/turn_complete.json"))
            {
                _logger.LogWarning(
                    "Найден validation_repair_request без correlated ready/turn_complete.json для pending turn(session={Session}, request={Request}, turn={Turn}). Очистка stale repair artifacts.",
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.TurnNumber);
                await DeleteValidationRepairFilesAsync();
                return;
            }

            var turnCompleteMetadata = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
            if (turnCompleteMetadata == null ||
                !string.Equals(turnCompleteMetadata.SessionId, manifest.SessionId, StringComparison.Ordinal) ||
                !string.Equals(turnCompleteMetadata.RequestId, manifest.RequestId, StringComparison.Ordinal) ||
                turnCompleteMetadata.TurnNumber != manifest.TurnNumber)
            {
                _logger.LogWarning(
                    "Найден validation_repair_request с некоррелированным ready/turn_complete.json. Очистка stale repair artifacts для pending turn(session={Session}, request={Request}, turn={Turn}).",
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.TurnNumber);
                _fs.DeleteFile("ready/turn_complete.json");
                await DeleteValidationRepairFilesAsync();
                return;
            }

            _logger.LogInformation(
                "Обнаружен активный repair cycle для pending turn(session={Session}, request={Request}, turn={Turn}). Он будет продолжен через correlated late-response validation.",
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber);
        }
    }

    private async Task NormalizePendingTerminalProtocolFailureArtifactsAsync()
    {
        if (!_fs.FileExists(TerminalProtocolFailureRequestPath))
            return;

        var json = await _fs.ReadFileAsync(TerminalProtocolFailureRequestPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Найден пустой terminal_protocol_failure_request.json. Удаление как невалидного stale artifact.");
            await DeleteTerminalProtocolFailureRequestAsync();
            return;
        }

        try
        {
            JsonSerializer.Deserialize<TerminalProtocolFailureRequest>(json, JsonOpts);
            _logger.LogInformation("Обнаружен сохранённый terminal protocol failure request. Он будет сохранён через рестарт и доступен daemon для повторного пинга GM.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Найден невалидный terminal_protocol_failure_request.json. Удаление как stale artifact.");
            await DeleteTerminalProtocolFailureRequestAsync();
        }
    }

    private async Task NormalizeRuntimeUiArtifactsAsync()
    {
        await NormalizePendingRepairArtifactsAsync();
        await NormalizePendingTerminalProtocolFailureArtifactsAsync();
        await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        await _qteSceneService.EnsureRuntimeStateHealthyAsync();

        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var hasReadySignals = _fs.FileExists("ready/turn_complete.json") || _fs.FileExists("ready/turn_error.json");

        if (manifest == null && hasReadySignals)
        {
            _logger.LogWarning("Найдены ready-сигналы без pending snapshot manifest. Очистка как stale runtime artifacts.");
            ClearReadySignals();
        }

        if (manifest != null)
            return;

        if (_fs.FileExists("input/turn_request.json"))
        {
            _logger.LogWarning("Найден orphaned input/turn_request.json без pending snapshot manifest. Удаление как stale runtime artifact.");
            _fs.DeleteFile("input/turn_request.json");
        }
    }

    private async Task<int?> ReadReadySignalTurnNumberAsync()
    {
        var metadata = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
        return metadata?.TurnNumber;
    }

    private async Task<ReadySignalMetadata?> ReadReadySignalMetadataAsync(string relativePath, int maxAttempts = 3, int delayMs = 150)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var metadata = await TryReadReadySignalMetadataOnceAsync(relativePath);
            if (metadata != null)
                return metadata;

            if (!_fs.FileExists(relativePath) || attempt == maxAttempts - 1)
                break;

            await Task.Delay(delayMs);
        }

        return null;
    }

    private async Task<ReadySignalMetadata?> TryReadReadySignalMetadataOnceAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (doc.RootElement.TryGetProperty("turnNumber", out var turnNumber) &&
                turnNumber.ValueKind == JsonValueKind.Number &&
                turnNumber.TryGetInt32(out var parsed))
            {
                var hasFilesModified = doc.RootElement.TryGetProperty("filesModified", out var filesModified);
                var filesModifiedValid = false;
                if (hasFilesModified && filesModified.ValueKind == JsonValueKind.Array)
                {
                    filesModifiedValid = true;
                    foreach (var item in filesModified.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String || !IsValidRelativeFilesModifiedEntry(item.GetString()))
                        {
                            filesModifiedValid = false;
                            break;
                        }
                    }
                }

                return new ReadySignalMetadata
                {
                    SessionId = doc.RootElement.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String
                        ? sid.GetString() ?? ""
                        : "",
                    RequestId = doc.RootElement.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String
                        ? rid.GetString() ?? ""
                        : "",
                    TurnNumber = parsed,
                    Status = doc.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                        ? status.GetString() ?? ""
                        : "",
                    Timestamp = doc.RootElement.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String
                        ? timestamp.GetString() ?? ""
                        : "",
                    Error = doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                        ? error.GetString()
                        : null,
                    HasFilesModified = hasFilesModified,
                    FilesModifiedValid = filesModifiedValid
                };
            }
        }
        catch { }

        return null;
    }

    private static bool IsValidRelativeFilesModifiedEntry(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed) || trimmed.StartsWith('/') || trimmed.StartsWith('\\'))
            return false;

        var normalized = trimmed.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                return false;
        }

        return true;
    }

    private bool IsMatchingReadySignal(ReadySignalMetadata signal, PendingTurnSnapshotManifest manifest) =>
        signal.TurnNumber == manifest.TurnNumber &&
        !string.IsNullOrWhiteSpace(signal.RequestId) &&
        string.Equals(signal.RequestId, manifest.RequestId, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(signal.SessionId) &&
        string.Equals(signal.SessionId, manifest.SessionId, StringComparison.OrdinalIgnoreCase);

    private static bool HasValidTerminalSignalContract(string sourceLabel, ReadySignalMetadata signal)
    {
        var expectsError = sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase);
        var expectedStatus = expectsError ? "error" : "success";
        if (!string.Equals(signal.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(signal.Timestamp) || !DateTimeOffset.TryParse(signal.Timestamp, out _))
            return false;

        if (!expectsError && (!signal.HasFilesModified || !signal.FilesModifiedValid))
            return false;

        return !expectsError || !string.IsNullOrWhiteSpace(signal.Error);
    }

    private async Task<bool> DiscardMismatchedReadySignalAsync(string sourceLabel, ReadySignalMetadata? signal,
        PendingTurnSnapshotManifest? manifest, bool preservePendingSnapshot = false)
    {
        if (signal == null)
        {
            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot && manifest != null)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил повреждённый ответ GM и запросил корректную повторную обработку.[/]");
            return true;
        }

        if (manifest == null)
        {
            _logger.LogWarning(
                "Отклонён {SourceLabel}: отсутствует pending snapshot manifest для signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn})",
                sourceLabel,
                signal.SessionId,
                signal.RequestId,
                signal.TurnNumber);

            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot && manifest != null)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил несогласованный ответ GM и восстановил безопасное ожидание.[/]");
            return true;
        }

        if (IsMatchingReadySignal(signal, manifest))
            return false;

        _logger.LogWarning(
            "Отклонён {SourceLabel}: signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn}) ожидался (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
            sourceLabel,
            signal.SessionId,
            signal.RequestId,
            signal.TurnNumber,
            manifest.SessionId,
            manifest.RequestId,
            manifest.TurnNumber);

        ClearReadySignals();
        ClearTransientOutputFiles();
        if (!preservePendingSnapshot)
            await CleanupPendingTurnSnapshotAsync();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент проигнорировал устаревший или несвязанный ответ GM.[/]");
        return true;
    }

    private void ClearReadySignals()
    {
        if (_fs.FileExists("ready/turn_complete.json"))
            _fs.DeleteFile("ready/turn_complete.json");
        if (_fs.FileExists("ready/turn_error.json"))
            _fs.DeleteFile("ready/turn_error.json");
    }

    private void ClearTransientOutputFiles()
    {
        foreach (var file in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            "output/ink_feather_action_result.json",
            QteSceneService.QteOfferPath,
            ProgressionScheduleService.ReportPath
        })
        {
            if (_fs.FileExists(file))
                _fs.DeleteFile(file);
        }
    }

    private async Task EnsureClientOwnedSystemFilesHealthyAsync()
    {
        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task<bool> ValidateCurrentGameStateOrShowErrorsAsync(string source,
        RollbackSnapshot? rollbackSnapshot = null,
        ProgressionControl? progressionControl = null,
        bool allowRepairLoop = false)
    {
        var repairAttempt = 0;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var issues = await _validator.ValidateGameStateAsync();
            if (RequiresAcceptedTurnPayloadValidation(source))
            {
                if (RequiresFreshNarrativePayload(source))
                    issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
            }
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (progressionControl != null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(progressionControl));
            var errors = PrioritizeValidationErrors(issues.Where(i => i.Severity == IssueSeverity.Error)).ToList();

            if (errors.Count == 0)
            {
                await DeleteValidationRepairFilesAsync();
                if (progressionControl != null)
                    await _progressionSchedule.ApplyAcceptedTurnOutcomeAsync(progressionControl);
                return true;
            }

            _logger.LogError("Нарушение контракта состояния после {Source}: {Count} ошибок", source, errors.Count);

            if (!allowRepairLoop)
            {
                if (HasRollbackCapability(rollbackSnapshot))
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                }

                await _progressionSchedule.DeleteTransientReportAsync();
                ShowContractValidationErrors(source, errors);
                return false;
            }

            repairAttempt++;
            if (!await WaitForContractRepairAsync(source, errors, repairAttempt, rollbackSnapshot))
                return false;
        }
    }

    private async Task<bool> ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        var criticalRepairAttempt = 0;

        while (true)
        {
            await EnsureClientOwnedSystemFilesHealthyAsync();
            var rawIssues = await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync();
            var rawErrors = PrioritizeValidationErrors(rawIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (rawErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn raw state corruption after {Source}: {Count} errors",
                    source,
                    rawErrors.Count);

                if (!await WaitForContractRepairAsync(source, rawErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn);
            await RefreshCanonicalStateAsync(snapshot);

            await EnsureClientOwnedSystemFilesHealthyAsync();
            var canonicalIssues = await _criticalStateHealth.ValidateCriticalCanonicalStateAsync();
            var canonicalErrors = PrioritizeValidationErrors(canonicalIssues.Where(i => i.Severity == IssueSeverity.Error)).ToList();
            if (canonicalErrors.Count > 0)
            {
                criticalRepairAttempt++;
                _logger.LogError(
                    "Critical accepted-turn canonical state corruption after {Source}: {Count} errors",
                    source,
                    canonicalErrors.Count);

                if (!await WaitForContractRepairAsync(source, canonicalErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            if (!await ValidateCurrentGameStateOrShowErrorsAsync(source, rollbackSnapshot, progressionControl, allowRepairLoop: true))
                return false;

            snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn);
            await RefreshCanonicalStateAsync(snapshot);
            return true;
        }
    }

    private static bool RequiresFreshNarrativePayload(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private static bool RequiresAcceptedTurnPayloadValidation(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private void ShowContractValidationErrors(string source, List<ValidationIssue> errors)
    {
        var summaryLines = BuildValidationSummaryLines(errors, 5);
        var lines = new List<string>
        {
            $"[bold red]Нарушение контракта GM после {GameInterface.EscapeMarkup(source)}[/]",
            "[red]Клиент отклонил состояние как несовместимое с Rules/API.[/]",
            ""
        };

        if (summaryLines.Count > 0)
        {
            lines.Add("[bold yellow]Основные группы ошибок:[/]");
            foreach (var summary in summaryLines)
                lines.Add($"[yellow]• {GameInterface.EscapeMarkup(summary)}[/]");
            lines.Add("");
        }

        foreach (var issue in errors.Take(10))
        {
            var label = BuildIssueDisplayLabel(issue);
            lines.Add($"[red]• {GameInterface.EscapeMarkup(label)}[/]");
            if (!string.IsNullOrWhiteSpace(issue.RepairHint))
                lines.Add($"  [grey]Исправление:[/] {GameInterface.EscapeMarkup(issue.RepairHint)}");
        }

        if (errors.Count > 10)
            lines.Add($"[yellow]... и ещё {errors.Count - 10} ошибок[/]");

        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Contract Error ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1),
            Expand = true
        });
        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
        Console.ReadKey(true);
    }

    private async Task<bool> WaitForContractRepairAsync(string source, List<ValidationIssue> errors,
        int attempt, RollbackSnapshot? rollbackSnapshot)
    {
        await WriteValidationRepairRequestAsync(source, errors, attempt);
        var rollbackAvailable = HasRollbackCapability(rollbackSnapshot);
        while (true)
        {
            using var cts = new CancellationTokenSource();
            var startTime = DateTime.UtcNow;

            var waitTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (_fs.FileExists(ValidationRepairReadyPath))
                        return true;
                    await Task.Delay(500, cts.Token);
                }
                return false;
            }, cts.Token);

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots12)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync(rollbackAvailable
                    ? "[yellow]⛏ GM исправляет невалидное состояние... (Escape = откатить изменения)[/]"
                    : "[yellow]⛏ GM исправляет невалидное состояние... (Escape = выйти из ожидания)[/]", async ctx =>
                {
                    while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                    {
                        var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                        ctx.Status(rollbackAvailable
                            ? $"[yellow]⛏ Ожидание исправления GM... попытка проверки #{attempt} ({elapsed}с) (Escape = откатить)[/]"
                            : $"[yellow]⛏ Ожидание исправления GM... попытка проверки #{attempt} ({elapsed}с) (Escape = выйти)[/]");

                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape)
                                cts.Cancel();
                        }

                        await Task.Delay(1000);
                    }

                    try { return await waitTask; }
                    catch (OperationCanceledException) { return false; }
                });

            if (cts.IsCancellationRequested)
            {
                if (rollbackAvailable)
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Состояние откатилось к последней стабильной версии.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⏹ Ремонтный цикл прерван. Автоматический откат для этого режима недоступен; текущее состояние оставлено как есть.[/]");
                }

                await _progressionSchedule.DeleteTransientReportAsync();
                await DeleteValidationRepairFilesAsync();
                return false;
            }

            if (!result)
                continue;

            var readyJson = await _fs.ReadFileAsync(ValidationRepairReadyPath);
            var ready = await ReadValidationRepairReadyAsync();
            if (ready == null)
            {
                _logger.LogWarning("Отклонён validation_repair_ready: файл не читается как валидный JSON");
                await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "invalid_repair_ready_json",
                    "Клиент отклонил validation_repair_ready.json: файл не читается как валидный JSON.",
                    "Valid JSON object with matching sessionId/requestId/turnNumber for the active repair cycle",
                    string.IsNullOrWhiteSpace(readyJson) ? "missing or empty file" : TruncateDiagnosticValue(readyJson),
                    "Перезапиши validation_repair_ready.json валидным JSON и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json.");
                await DeleteValidationRepairReadyAsync();
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            var manifest = await LoadPendingTurnSnapshotManifestAsync();
            if (!IsMatchingRepairReady(ready, manifest))
            {
                _logger.LogWarning(
                    "Отклонён validation_repair_ready(session={Session}, request={Request}, turn={Turn}) — ожидается (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
                    ready.SessionId,
                    ready.RequestId,
                    ready.TurnNumber,
                    manifest?.SessionId,
                    manifest?.RequestId,
                    manifest?.TurnNumber);

                await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "mismatched_repair_ready_context",
                    "Клиент отклонил validation_repair_ready.json: metadata не совпадает с активным repair cycle.",
                    BuildExpectedRepairContext(manifest),
                    BuildActualRepairContext(ready, manifest),
                    "Пересоздай validation_repair_ready.json и скопируй sessionId/requestId/turnNumber ровно из validation_repair_request.json.");
                await DeleteValidationRepairReadyAsync();
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            await DeleteValidationRepairReadyAsync();
            return true;
        }
    }

    private async Task WriteValidationRepairRequestAsync(string source, List<ValidationIssue> errors, int attempt)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var existingRequest = await ReadValidationRepairRequestAsync();
        var request = new ValidationRepairRequest
        {
            SessionId = manifest?.SessionId ?? existingRequest?.SessionId ?? _gameLoop.SessionId,
            RequestId = manifest?.RequestId ?? existingRequest?.RequestId ?? "",
            TurnNumber = manifest?.TurnNumber ?? existingRequest?.TurnNumber ?? (_gameLoop.TurnNumber + 1),
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            RevalidationAttempt = attempt,
            GmInstructions =
                "Текущий ответ/состояние отклонены клиентом. Исправь уже записанные файлы in place, ориентируясь на список ошибок ниже. " +
                "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt. После исправлений создай game_state/control/validation_repair_ready.json с sessionId/requestId/turnNumber. " +
                "Если клиент переписал этот repair request повторно, используй ТОЛЬКО самые свежие metadata из текущего файла.",
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            Errors = prioritizedErrors.Select(e => new ValidationRepairIssue
            {
                Code = e.Code ?? "validation_error",
                FilePath = e.FilePath,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                Message = e.Message,
                Actor = e.Actor,
                Section = e.Section,
                Expected = e.Expected,
                Actual = e.Actual,
                RepairHint = e.RepairHint ?? "Исправь состояние/структуру так, чтобы оно соответствовало Rules/API contract."
            }).ToList()
        };

        await _fs.WriteFileAtomicAsync(ValidationRepairRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    private async Task WriteTerminalProtocolFailureRequestAsync(string source, List<ValidationIssue> errors)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var request = new TerminalProtocolFailureRequest
        {
            SessionId = manifest?.SessionId ?? _gameLoop.SessionId,
            RequestId = manifest?.RequestId ?? "",
            TurnNumber = manifest?.TurnNumber ?? (_gameLoop.TurnNumber + 1),
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            GmInstructions =
                "Клиент отклонил terminal ready signal как protocol failure. Это НЕ validation_repair_request.json и НЕ repair loop. " +
                "Не создавай validation_repair_ready.json и не пытайся продолжать этот уже закрытый wait cycle. " +
                "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt, разберись с terminal protocol problem по списку ошибок ниже и исправь логику для следующего корректного хода.",
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            Errors = prioritizedErrors.Select(e => new ValidationRepairIssue
            {
                Code = e.Code ?? "terminal_protocol_failure",
                FilePath = e.FilePath,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                Message = e.Message,
                Actor = e.Actor,
                Section = e.Section,
                Expected = e.Expected,
                Actual = e.Actual,
                RepairHint = e.RepairHint ?? "Исправь terminal completion protocol так, чтобы клиент получил ровно один корректный terminal signal."
            }).ToList()
        };

        await _fs.WriteFileAtomicAsync(TerminalProtocolFailureRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    private static List<string> BuildValidationSummaryLines(IEnumerable<ValidationIssue> issues, int maxGroups)
    {
        return issues
            .GroupBy(issue => new
            {
                issue.Category,
                Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
            })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Category.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Section, StringComparer.OrdinalIgnoreCase)
            .Take(maxGroups)
            .Select(group => $"{FormatIssueCategory(group.Key.Category)} / {group.Key.Section}: {group.Count()}")
            .ToList();
    }

    private static IEnumerable<ValidationIssue> PrioritizeValidationErrors(IEnumerable<ValidationIssue> errors)
    {
        return errors
            .OrderByDescending(GetValidationIssuePriority)
            .ThenBy(issue => string.IsNullOrWhiteSpace(issue.Section) ? "zzzz" : issue.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Code ?? "zzzz", StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Message, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetValidationIssuePriority(ValidationIssue issue)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(issue.RepairHint))
            score += 40;
        if (!string.IsNullOrWhiteSpace(issue.Code))
            score += 30;
        if (!string.IsNullOrWhiteSpace(issue.Expected) || !string.IsNullOrWhiteSpace(issue.Actual))
            score += 20;
        if (issue.Category == IssueCategory.ProtocolViolation)
            score += 10;

        if (IsGenericShapeError(issue))
            score -= 60;

        return score;
    }

    private static bool IsGenericShapeError(ValidationIssue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.Code))
            return false;

        var message = issue.Message ?? string.Empty;
        return message.StartsWith("Отсутствует обязательное поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательное строковое поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательное числовое или строковое поле", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Отсутствует обязательный объект", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Поле должно быть", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Элемент должен быть", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Требуется хотя бы одно поле", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIssueDisplayLabel(ValidationIssue issue)
    {
        var prefix = $"[{FormatIssueCategory(issue.Category)}";
        if (!string.IsNullOrWhiteSpace(issue.Section))
            prefix += $" / {issue.Section}";
        if (!string.IsNullOrWhiteSpace(issue.Code))
            prefix += $" / {issue.Code}";
        prefix += "]";

        return $"{prefix} {issue.Message}";
    }

    private static string FormatIssueCategory(IssueCategory category) => category switch
    {
        IssueCategory.ProtocolViolation => "Protocol",
        IssueCategory.ClientOwnedSurface => "Client-Owned",
        _ => "State"
    };

    private async Task<ValidationRepairRequest?> ReadValidationRepairRequestAsync()
    {
        var json = await _fs.ReadFileAsync(ValidationRepairRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationRepairRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private async Task ReportRejectedRepairReadyAsync(string source, List<ValidationIssue> baseErrors, int attempt,
        string code, string message, string expected, string actual, string repairHint)
    {
        var reportErrors = new List<ValidationIssue>
        {
            new(
                ValidationRepairReadyPath,
                IssueSeverity.Error,
                message,
                code: code,
                section: "validation_repair_ready",
                expected: expected,
                actual: actual,
                repairHint: repairHint)
        };

        await WriteValidationRepairRequestAsync(source, reportErrors, attempt);
    }

    private async Task<ValidationRepairReady?> ReadValidationRepairReadyAsync()
    {
        var json = await _fs.ReadFileAsync(ValidationRepairReadyPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationRepairReady>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private bool IsMatchingRepairReady(ValidationRepairReady ready, PendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return false;

        return ready.TurnNumber == manifest.TurnNumber &&
               !string.IsNullOrWhiteSpace(ready.RequestId) &&
               string.Equals(ready.RequestId, manifest.RequestId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(ready.SessionId) &&
               string.Equals(ready.SessionId, manifest.SessionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedRepairContext(PendingTurnSnapshotManifest? manifest)
    {
        return manifest == null
            ? "Existing pending turn snapshot manifest with the active sessionId/requestId/turnNumber"
            : $"sessionId={manifest.SessionId}, requestId={manifest.RequestId}, turnNumber={manifest.TurnNumber}";
    }

    private static string BuildActualRepairContext(ValidationRepairReady ready, PendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}; pending snapshot manifest is missing";

        return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}";
    }

    private static string TruncateDiagnosticValue(string value, int maxLength = 280)
    {
        var normalized = value.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength)
            return normalized;
        return normalized[..maxLength] + "...";
    }

    private async Task DeleteValidationRepairFilesAsync()
    {
        await DeleteValidationRepairReadyAsync();
        if (_fs.FileExists(ValidationRepairRequestPath))
            _fs.DeleteFile(ValidationRepairRequestPath);
    }

    private Task DeleteTerminalProtocolFailureRequestAsync()
    {
        if (_fs.FileExists(TerminalProtocolFailureRequestPath))
            _fs.DeleteFile(TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteValidationRepairReadyAsync()
    {
        if (_fs.FileExists(ValidationRepairReadyPath))
            _fs.DeleteFile(ValidationRepairReadyPath);
        return Task.CompletedTask;
    }

    private async Task ShowTurnErrorMessageAsync(string readyErrorPath)
    {
        var errorJson = await _fs.ReadFileAsync(readyErrorPath);
        if (errorJson == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Ошибка ожидания ответа GM[/]");
            return;
        }

        try
        {
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMsg = errorDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : errorJson;
            AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorMsg ?? errorJson)}[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorJson)}[/]");
        }
    }

    private async Task CleanupUndispatchedTransitionPrepAsync(RollbackSnapshot? rollbackSnapshot,
        bool localStateMutated, bool manifestCreated)
    {
        if (HasRollbackCapability(rollbackSnapshot))
        {
            if (localStateMutated)
                await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
        }

        if (manifestCreated)
            await CleanupPendingTurnSnapshotAsync();
    }

    private async Task<bool> HandleRejectedActiveReadySignalAsync(string sourceLabel,
        ReadySignalMetadata? signal,
        PendingTurnSnapshotManifest? manifest,
        RollbackSnapshot? rollbackSnapshot)
    {
        var protocolErrors = BuildRejectedActiveReadySignalIssues(sourceLabel, signal, manifest);
        if (!await DiscardMismatchedReadySignalAsync(sourceLabel, signal, manifest, preservePendingSnapshot: true))
            return false;

        await WriteTerminalProtocolFailureRequestAsync($"terminal protocol failure: {sourceLabel}", protocolErrors);
        _fs.DeleteFile("input/turn_request.json");

        AnsiConsole.MarkupLine("[yellow]⚠ Текущий ответ GM отклонён клиентом. Состояние возвращено к последней стабильной версии.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после отклонения ответа GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return true;
    }

    private async Task HandleMissingActiveTerminalOutcomeAsync(PendingTurnSnapshotManifest? manifest,
        RollbackSnapshot? rollbackSnapshot)
    {
        var errors = new List<ValidationIssue>
        {
            new(
                "ready/turn_complete.json",
                IssueSeverity.Error,
                "После завершения ожидания не осталось ни одного коррелированного terminal signal для активного хода",
                code: "missing_correlated_terminal_signal_after_wait",
                section: "terminal_ready",
                expected: "Exactly one correlated ready/turn_complete.json or ready/turn_error.json for the active turn",
                actual: BuildMissingActiveTerminalOutcomeActual(manifest),
                repairHint: "Записывай ровно один terminal signal с точными sessionId/requestId/turnNumber, не удаляй и не перезаписывай его после записи и не смешивай terminal protocol failure с validation repair loop.")
        };

        await WriteTerminalProtocolFailureRequestAsync("missing correlated terminal signal after wait", errors);
        _fs.DeleteFile("input/turn_request.json");
        ClearReadySignals();
        ClearTransientOutputFiles();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент не смог безопасно принять ответ GM и восстановил последнюю стабильную версию состояния.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после потери корректного ответа GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
    }

    private async Task<bool> ResolveConcurrentActiveTerminalSignalsAsync(PendingTurnSnapshotManifest? manifest,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (!_fs.FileExists("ready/turn_complete.json") || !_fs.FileExists("ready/turn_error.json"))
            return false;

        if (manifest == null)
            return false;

        var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
        var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
        if (completionSignal != null &&
            IsMatchingReadySignal(completionSignal, manifest) &&
            !HasValidTerminalSignalContract("turn_complete", completionSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, manifest, rollbackSnapshot);
        }

        if (errorSignal != null &&
            IsMatchingReadySignal(errorSignal, manifest) &&
            !HasValidTerminalSignalContract("turn_error", errorSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, manifest, rollbackSnapshot);
        }

        var completionMatches = completionSignal != null &&
                                IsMatchingReadySignal(completionSignal, manifest) &&
                                HasValidTerminalSignalContract("turn_complete", completionSignal);
        var errorMatches = errorSignal != null &&
                           IsMatchingReadySignal(errorSignal, manifest) &&
                           HasValidTerminalSignalContract("turn_error", errorSignal);

        if (completionMatches && !errorMatches)
        {
            _logger.LogWarning("Удаляется competing terminal error signal во время active wait; success signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_error.json");
            return false;
        }

        if (errorMatches && !completionMatches)
        {
            _logger.LogWarning("Удаляется competing terminal success signal во время active wait; error signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_complete.json");
            return false;
        }

        var errors = new List<ValidationIssue>
        {
            new(
                "ready/turn_complete.json",
                IssueSeverity.Error,
                completionMatches && errorMatches
                    ? "Для одного и того же sessionId/requestId/turnNumber одновременно обнаружены ready/turn_complete.json и ready/turn_error.json"
                    : "Одновременное наличие competing terminal signals не удалось однозначно сопоставить активному ходу",
                code: "dual_terminal_ready_signals",
                section: "terminal_ready",
                expected: "Exactly one terminal signal for the active turn",
                actual: BuildConcurrentTerminalSignalActual(completionSignal, errorSignal),
                repairHint: "Для одного хода записывай ровно один terminal signal: либо ready/turn_complete.json, либо ready/turn_error.json. Не оставляй второй ready-файл как запасной вариант и не запускай repair loop для terminal conflict.")
        };

        await WriteTerminalProtocolFailureRequestAsync("dual terminal ready signals", errors);
        _fs.DeleteFile("input/turn_request.json");
        ClearReadySignals();
        ClearTransientOutputFiles();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент обнаружил внутреннюю несогласованность в ответе GM и восстановил последнюю стабильную версию состояния.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последняя стабильная версия состояния восстановлена после конфликтующих ответов GM.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return true;
    }

    private static string BuildConcurrentTerminalSignalActual(ReadySignalMetadata? completionSignal,
        ReadySignalMetadata? errorSignal)
    {
        static string Describe(string label, ReadySignalMetadata? signal)
        {
            return signal == null
                ? $"{label}=missing_or_unreadable"
                : $"{label}(sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}, status={signal.Status})";
        }

        return $"{Describe("turn_complete", completionSignal)}; {Describe("turn_error", errorSignal)}";
    }

    private string BuildMissingActiveTerminalOutcomeActual(PendingTurnSnapshotManifest? manifest)
    {
        var turnCompleteExists = _fs.FileExists("ready/turn_complete.json");
        var turnErrorExists = _fs.FileExists("ready/turn_error.json");
        var manifestDescription = manifest == null
            ? "pendingSnapshot=missing"
            : $"pendingSnapshot=sessionId={manifest.SessionId}, requestId={manifest.RequestId}, turnNumber={manifest.TurnNumber}";
        return $"turn_complete_exists={turnCompleteExists}; turn_error_exists={turnErrorExists}; {manifestDescription}";
    }

    private async Task<ActiveTerminalOutcomeResolution> ResolveFinalActiveTerminalOutcomeAsync(
        PendingTurnSnapshotManifest? manifest,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (await ResolveConcurrentActiveTerminalSignalsAsync(manifest, rollbackSnapshot))
            return new ActiveTerminalOutcomeResolution { Kind = "failure" };

        if (_fs.FileExists("ready/turn_complete.json"))
        {
            var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
            if (completionSignal != null &&
                manifest != null &&
                IsMatchingReadySignal(completionSignal, manifest) &&
                HasValidTerminalSignalContract("turn_complete", completionSignal))
            {
                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "success",
                    Signal = completionSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, manifest, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        if (_fs.FileExists("ready/turn_error.json"))
        {
            var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
            if (errorSignal != null &&
                manifest != null &&
                IsMatchingReadySignal(errorSignal, manifest) &&
                HasValidTerminalSignalContract("turn_error", errorSignal))
            {
                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "error",
                    Signal = errorSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, manifest, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        await HandleMissingActiveTerminalOutcomeAsync(manifest, rollbackSnapshot);
        return new ActiveTerminalOutcomeResolution { Kind = "failure" };
    }

    private List<ValidationIssue> BuildRejectedActiveReadySignalIssues(string sourceLabel,
        ReadySignalMetadata? signal, PendingTurnSnapshotManifest? manifest)
    {
        if (signal == null)
        {
            return
            [
                new ValidationIssue(
                    sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                        ? "ready/turn_error.json"
                        : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal не читается как валидный JSON с полными metadata",
                    code: "invalid_terminal_ready_json",
                    section: "terminal_ready",
                    expected: "Valid JSON with sessionId/requestId/turnNumber",
                    actual: "missing, empty, unreadable or incomplete ready signal metadata",
                    repairHint: "Перезапиши terminal ready file валидным JSON, скопируй точные sessionId/requestId/turnNumber из текущего turn_request.json и записывай terminal signal самым последним шагом хода.")
            ];
        }

        if (!HasValidTerminalSignalContract(sourceLabel, signal))
        {
            var expectsError = sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase);
            var expectedStatus = expectsError ? "error" : "success";
            var issues = new List<ValidationIssue>();

            if (!string.Equals(signal.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    expectsError ? "ready/turn_error.json" : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal содержит неверный status для этого terminal channel",
                    code: "invalid_terminal_ready_status",
                    section: "terminal_ready",
                    expected: expectedStatus,
                    actual: string.IsNullOrWhiteSpace(signal.Status) ? "missing/empty" : signal.Status,
                    repairHint: expectsError
                        ? "Для ready/turn_error.json указывай status=\"error\" и заполняй error с описанием терминальной причины."
                        : "Для ready/turn_complete.json указывай status=\"success\" и не смешивай success signal с error channel."));
            }

            if (string.IsNullOrWhiteSpace(signal.Timestamp) || !DateTimeOffset.TryParse(signal.Timestamp, out _))
            {
                issues.Add(new ValidationIssue(
                    expectsError ? "ready/turn_error.json.timestamp" : "ready/turn_complete.json.timestamp",
                    IssueSeverity.Error,
                    "Terminal ready signal обязан содержать валидный ISO 8601 timestamp",
                    code: "terminal_ready_missing_or_invalid_timestamp",
                    section: "terminal_ready",
                    expected: "ISO 8601 timestamp",
                    actual: string.IsNullOrWhiteSpace(signal.Timestamp) ? "missing/empty" : signal.Timestamp,
                    repairHint: "Добавь в terminal ready signal поле timestamp в ISO 8601 формате и записывай ready-файл только после завершения всех остальных файлов хода."));
            }

            if (expectsError && string.IsNullOrWhiteSpace(signal.Error))
            {
                issues.Add(new ValidationIssue(
                    "ready/turn_error.json.error",
                    IssueSeverity.Error,
                    "ready/turn_error.json обязан содержать непустое поле error",
                    code: "terminal_error_missing_error_message",
                    section: "terminal_ready",
                    expected: "non-empty error string",
                    actual: "missing/empty",
                    repairHint: "Добавь в ready/turn_error.json краткое непустое описание терминальной ошибки в поле error."));
            }

            if (!expectsError && (!signal.HasFilesModified || !signal.FilesModifiedValid))
            {
                issues.Add(new ValidationIssue(
                    "ready/turn_complete.json.filesModified",
                    IssueSeverity.Error,
                    "ready/turn_complete.json обязан содержать filesModified как массив непустых путей",
                    code: "terminal_success_missing_or_invalid_files_modified",
                    section: "terminal_ready",
                    expected: "filesModified array of non-empty relative file paths",
                    actual: signal.HasFilesModified ? "invalid filesModified payload" : "missing",
                    repairHint: "Добавь в ready/turn_complete.json поле filesModified как массив относительных путей файлов, которые были записаны для этого хода."));
            }

            if (issues.Count > 0)
                return issues;
        }

        if (manifest == null)
        {
            return
            [
                new ValidationIssue(
                    sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                        ? "ready/turn_error.json"
                        : "ready/turn_complete.json",
                    IssueSeverity.Error,
                    "Terminal ready signal не удалось сопоставить активному pending turn context",
                    code: "missing_pending_context_for_terminal_ready",
                    section: "terminal_ready",
                    expected: "Existing pending turn snapshot manifest for the active request",
                    actual: $"ready signal sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}; pending snapshot manifest is missing",
                    repairHint: "Не пиши terminal ready signal вне активного correlated turn context, не переиспользуй stale ready files и не пытайся чинить terminal failure через validation_repair_ready.json.")
            ];
        }

        return
        [
            new ValidationIssue(
                sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase)
                    ? "ready/turn_error.json"
                    : "ready/turn_complete.json",
                IssueSeverity.Error,
                "Terminal ready signal содержит metadata, не совпадающие с активным ходом",
                code: "mismatched_terminal_ready_context",
                section: "terminal_ready",
                expected: $"sessionId={manifest.SessionId}, requestId={manifest.RequestId}, turnNumber={manifest.TurnNumber}",
                actual: $"sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}",
                repairHint: "Копируй sessionId/requestId/turnNumber в terminal ready signal ровно из текущего turn_request.json и записывай ready-файл только после завершения всех остальных файлов хода.")
        ];
    }

    /// <summary>
    /// Builds a life summary from current game state for Guardian knowledge persistence.
    /// </summary>
    private string BuildLifeSummary(string? playerSummary)
    {
        var state = _stateManager.CurrentState;
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(state.CharacterName))
            parts.Add($"Имя: {state.CharacterName}");
        if (!string.IsNullOrEmpty(state.CharacterRace))
            parts.Add($"Раса: {state.CharacterRace}");
        if (!string.IsNullOrEmpty(state.CharacterClass))
            parts.Add($"Класс: {state.CharacterClass}");
        if (!string.IsNullOrEmpty(state.CurrentLocation))
            parts.Add($"Последнее местоположение: {state.CurrentLocation}");
        parts.Add($"Ходов прожито: {_gameLoop.TurnNumber}");

        if (!string.IsNullOrWhiteSpace(playerSummary))
            parts.Add($"Заметка игрока: {playerSummary}");

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Updates the soul state realm and optionally appends a life entry to livesHistory.
    /// Eliminates code duplication across HandleEndOfLife, CheckLifeTransitions, HandleIncarnation.
    /// </summary>
    private async Task UpdateSoulStateRealm(string newRealm, string? lifeSummaryToAppend = null, bool incrementIncarnation = false)
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (soulJson == null) return;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;

            // Reconstruct all existing properties
            var dict = new Dictionary<string, object?>();

            dict["soulName"] = root.TryGetProperty("soulName", out var sn) ? sn.GetString() : "";
            dict["previousSoulNames"] = root.TryGetProperty("previousSoulNames", out var previousSoulNames)
                ? JsonSerializer.Deserialize<object>(previousSoulNames.GetRawText())
                : Array.Empty<string>();
            dict["currentRealm"] = newRealm;
            var existingInc = root.TryGetProperty("currentIncarnation", out var inc) && inc.TryGetInt32(out var incVal) ? incVal : 0;
            dict["currentIncarnation"] = incrementIncarnation ? existingInc + 1 : existingInc;

            // Preserve complex objects
            dict["enlightenment"] = root.TryGetProperty("enlightenment", out var enl)
                ? JsonSerializer.Deserialize<object>(enl.GetRawText())
                : new { currentTier = "Новичок", experience = 0, level = 0 };
            dict["inkFeathers"] = root.TryGetProperty("inkFeathers", out var f)
                ? JsonSerializer.Deserialize<object>(f.GetRawText())
                : new { current = 0, total = 0 };
            dict["soulRelics"] = root.TryGetProperty("soulRelics", out var sr)
                ? JsonSerializer.Deserialize<object>(sr.GetRawText())
                : new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() };

            // Handle livesHistory — optionally append a new life entry
            var existingHistory = new List<object>();
            if (root.TryGetProperty("livesHistory", out var lh) &&
                lh.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in lh.EnumerateArray())
                    existingHistory.Add(JsonSerializer.Deserialize<object>(entry.GetRawText())!);
            }

            if (!string.IsNullOrWhiteSpace(lifeSummaryToAppend))
            {
                var lifeEntry = new
                {
                    incarnation = dict["currentIncarnation"],
                    summary = lifeSummaryToAppend,
                    endedAt = DateTime.UtcNow.ToString("o"),
                    turnsLived = _gameLoop.TurnNumber
                };
                existingHistory.Add(lifeEntry);
            }

            dict["livesHistory"] = existingHistory;

            foreach (var prop in root.EnumerateObject())
            {
                if (!dict.ContainsKey(prop.Name))
                    dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
                JsonSerializer.Serialize(dict, JsonOpts));

            if (string.Equals(newRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(lifeSummaryToAppend))
            {
                _fs.ClearCurrentWorldLore();
                await ResetGuardianGachaChargesForNewReturn();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка обновления soul_state.json");
        }
    }

    private async Task<string?> ApplyPendingMemoryLegacyForIncarnationAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            if (legacy == null)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return null;
            }

            var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
            var applicationState = legacy["applicationState"]?.GetValue<string>() ?? "pending";
            string? summary = null;

            if (string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
            {
                var survives = await PendingMemoryLegacyEffectStillPresentAsync(legacy);
                summary = BuildPendingMemoryLegacySummary(legacy);
                if (!survives)
                {
                    legacy["applicationState"] = "pending";
                    legacy.Remove("applicationAudit");
                    await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root!.ToJsonString(JsonOpts));
                }
                else
                {
                    _pendingMemoryLegacyAwaitingConsumption = !string.IsNullOrWhiteSpace(summary);
                    return summary;
                }
            }

            if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
                var bonus = legacy["bonus"]?.GetValue<int>() ?? 0;
                if (Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase) && bonus > 0)
                {
                    await ApplyMemoryLegacyCharacteristicBonusAsync(characteristic, bonus);
                    var statName = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
                    summary = $"+{bonus} к характеристике «{statName}» в этой инкарнации";
                }
            }
            else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                await ApplyMemoryLegacyPassiveSkillAsync(legacy);
                var skillName = legacy["skillName"]?.GetValue<string>() ?? "Неизвестный навык";
                summary = $"получен пассивный навык «{skillName}» для этой инкарнации";
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                legacy["applicationState"] = "applied-awaiting-turn-accept";
                await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root!.ToJsonString(JsonOpts));
            }

            _pendingMemoryLegacyAwaitingConsumption = !string.IsNullOrWhiteSpace(summary);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось применить pendingMemoryLegacy при начале новой инкарнации");
            _pendingMemoryLegacyAwaitingConsumption = false;
            return null;
        }
    }

    private async Task ApplyMemoryLegacyCharacteristicBonusAsync(string characteristic, int bonus)
    {
        const string path = "game_state/misc/characteristics.json";
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Characteristics.All)
            stats[name] = 1;

        var json = await _fs.ReadFileAsync(path);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var name in Characteristics.All)
                {
                    if (doc.RootElement.TryGetProperty(name, out var value) &&
                        value.ValueKind == JsonValueKind.Number &&
                        value.TryGetInt32(out var parsed))
                    {
                        stats[name] = parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось прочитать characteristics.json перед применением Наследия Памяти");
            }
        }

        stats[characteristic] = Math.Min(100, stats.GetValueOrDefault(characteristic, 1) + bonus);
        var payload = new Dictionary<string, object>(stats.Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value)));
        await _fs.WriteFileAtomicAsync(path, JsonSerializer.Serialize(payload, JsonOpts));
    }

    private async Task ApplyMemoryLegacyPassiveSkillAsync(JsonObject legacy)
    {
        const string path = "game_state/player/skills_passive.json";
        JsonObject root;

        var json = await _fs.ReadFileAsync(path);
        try
        {
            root = !string.IsNullOrWhiteSpace(json)
                ? JsonNode.Parse(json) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        var skills = root["passiveSkillChanges"] as JsonArray ?? new JsonArray();
        root["passiveSkillChanges"] = skills;

        var skillName = legacy["skillName"]?.GetValue<string>() ?? "Наследие Памяти";
        for (var i = skills.Count - 1; i >= 0; i--)
        {
            if (skills[i] is JsonObject existing &&
                string.Equals(existing["skillName"]?.GetValue<string>(), skillName, StringComparison.OrdinalIgnoreCase))
            {
                skills.RemoveAt(i);
            }
        }

        var skill = new JsonObject
        {
            ["skillName"] = skillName,
            ["skillDescription"] = legacy["skillDescription"]?.GetValue<string>() ?? "",
            ["rarity"] = legacy["rarity"]?.GetValue<string>() ?? "Uncommon",
            ["type"] = legacy["type"]?.GetValue<string>() ?? "MemoryLegacy",
            ["group"] = legacy["group"]?.GetValue<string>() ?? "Knowledge",
            ["playerStatBonus"] = legacy["playerStatBonus"]?.GetValue<string>() ?? "",
            ["masteryLevel"] = legacy["masteryLevel"]?.GetValue<int>() ?? 1,
            ["maxMasteryLevel"] = legacy["maxMasteryLevel"]?.GetValue<int>() ?? 1,
            ["structuredBonuses"] = legacy["structuredBonuses"]?.DeepClone() ?? new JsonArray()
        };

        skills.Add(skill);
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
    }

    private async Task CapturePendingMemoryLegacyApplicationAuditAsync()
    {
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
                return;

            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            if (legacy == null)
                return;

            var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
            var audit = legacy["applicationAudit"] as JsonObject ?? new JsonObject();

            if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(characteristic))
                {
                    var currentValue = await ReadCurrentCharacteristicValueAsync(characteristic);
                    if (currentValue.HasValue)
                        audit["expectedCharacteristicValue"] = currentValue.Value;
                }
            }
            else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                var skillName = legacy["skillName"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(skillName))
                {
                    audit["expectedPassiveSkillName"] = skillName;
                    audit["expectedGroup"] = legacy["group"]?.GetValue<string>() ?? "Knowledge";
                    audit["expectedPlayerStatBonus"] = legacy["playerStatBonus"]?.GetValue<string>() ?? "";
                    if (legacy["structuredBonuses"] is JsonArray bonusArr)
                    {
                        audit["expectedStructuredBonusesCount"] = bonusArr.Count;
                        audit["expectedStructuredBonusesCanonical"] = StructuredBonusCanonicalizer.Canonicalize(bonusArr);
                    }
                }
            }

            if (audit.Count > 0)
            {
                legacy["applicationAudit"] = audit;
                await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root!.ToJsonString(JsonOpts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось записать applicationAudit для pendingMemoryLegacy");
        }
    }

    private async Task<int?> ReadCurrentCharacteristicValueAsync(string characteristic)
    {
        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(characteristic, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать characteristics.json для проверки Наследия Памяти");
        }

        return null;
    }

    private static string? BuildPendingMemoryLegacySummary(JsonObject legacy)
    {
        var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
            var bonus = legacy["bonus"]?.GetValue<int>() ?? 0;
            if (string.IsNullOrWhiteSpace(characteristic) || bonus <= 0)
                return null;

            var statName = Characteristics.RussianNames.GetValueOrDefault(characteristic, characteristic);
            return $"+{bonus} к характеристике «{statName}» в этой инкарнации";
        }

        if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var skillName = legacy["skillName"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(skillName))
                return null;

            return $"получен пассивный навык «{skillName}» для этой инкарнации";
        }

        return null;
    }

    private async Task<bool> PendingMemoryLegacyEffectStillPresentAsync(JsonObject legacy)
    {
        var legacyType = legacy["legacyType"]?.GetValue<string>() ?? string.Empty;
        var audit = legacy["applicationAudit"] as JsonObject;
        if (audit == null)
            return false;

        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            var characteristic = legacy["characteristic"]?.GetValue<string>() ?? string.Empty;
            var expectedValue = audit["expectedCharacteristicValue"]?.GetValue<int?>() ?? null;
            if (string.IsNullOrWhiteSpace(characteristic) || !expectedValue.HasValue)
                return false;

            var currentValue = await ReadCurrentCharacteristicValueAsync(characteristic);
            return currentValue.HasValue && currentValue.Value >= expectedValue.Value;
        }

        if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            var expectedSkillName = audit["expectedPassiveSkillName"]?.GetValue<string>() ?? string.Empty;
            var expectedGroup = audit["expectedGroup"]?.GetValue<string>() ?? "Knowledge";
            var expectedPlayerStatBonus = audit["expectedPlayerStatBonus"]?.GetValue<string>() ?? string.Empty;
            var expectedStructuredBonusesCount = audit["expectedStructuredBonusesCount"]?.GetValue<int?>() ?? null;
            var expectedStructuredBonusesCanonical = audit["expectedStructuredBonusesCanonical"]?.GetValue<string>() ?? string.Empty;
            return await PassiveSkillMatchesExpectedShapeAsync(expectedSkillName, expectedGroup, expectedPlayerStatBonus, expectedStructuredBonusesCount, expectedStructuredBonusesCanonical);
        }

        return false;
    }

    private async Task<bool> PassiveSkillMatchesExpectedShapeAsync(
        string skillName,
        string expectedGroup,
        string expectedPlayerStatBonus,
        int? expectedStructuredBonusesCount,
        string expectedStructuredBonusesCanonical)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return false;

        var json = await _fs.ReadFileAsync("game_state/player/skills_passive.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var skills = root?["passiveSkillChanges"] as JsonArray;
            if (skills == null)
                return false;

            foreach (var item in skills.OfType<JsonObject>())
            {
                if (!string.Equals(item["skillName"]?.GetValue<string>(), skillName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(item["group"]?.GetValue<string>(), expectedGroup, StringComparison.OrdinalIgnoreCase))
                    return false;

                var playerStatBonus = item["playerStatBonus"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerStatBonus) ||
                    !string.Equals(playerStatBonus, expectedPlayerStatBonus, StringComparison.Ordinal))
                    return false;

                var structuredBonuses = item["structuredBonuses"] as JsonArray;
                if (structuredBonuses == null || structuredBonuses.Count == 0)
                    return false;

                if (expectedStructuredBonusesCount.HasValue && structuredBonuses.Count < expectedStructuredBonusesCount.Value)
                    return false;

                if (!string.IsNullOrWhiteSpace(expectedStructuredBonusesCanonical) &&
                    !string.Equals(StructuredBonusCanonicalizer.Canonicalize(structuredBonuses), expectedStructuredBonusesCanonical, StringComparison.Ordinal))
                    return false;

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить форму пассивного навыка для Наследия Памяти");
        }

        return false;
    }

    private async Task FinalizePendingMemoryLegacyConsumptionAsync()
    {
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            var root = JsonNode.Parse(soulJson) as JsonObject;
            if (root == null)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            if (root["pendingMemoryLegacy"] is not JsonObject legacy)
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            var applicationState = legacy["applicationState"]?.GetValue<string>() ?? string.Empty;
            if (!string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                return;
            }

            root["pendingMemoryLegacy"] = null;
            await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString(JsonOpts));
            await RefreshCanonicalStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось очистить pendingMemoryLegacy после успешного воплощения");
        }
        finally
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
        }
    }

    private async Task<bool> HasPendingMemoryLegacyAwaitingConsumptionAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            var root = JsonNode.Parse(soulJson) as JsonObject;
            var legacy = root?["pendingMemoryLegacy"] as JsonObject;
            var applicationState = legacy?["applicationState"]?.GetValue<string>() ?? string.Empty;
            return string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось проверить applicationState pendingMemoryLegacy");
            return false;
        }
    }

    private async Task ResetGuardianGachaChargesForNewReturn()
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject root)
                return;

            var changed = false;

            void ResetGuardian(JsonObject guardian)
            {
                var existingGachaSystem = guardian["gachaSystem"] as JsonObject;
                var hadChargesPerReturn = existingGachaSystem?["chargesPerReturn"] != null;
                var hadChargesUsedThisReturn = existingGachaSystem?["chargesUsedThisReturn"] != null;
                var hadReadableChargesPerReturn = TryReadInt(existingGachaSystem?["chargesPerReturn"], out var previousChargesPerReturn);
                var (chargesPerReturn, currentUsedCharges) = GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                var gachaSystem = guardian["gachaSystem"] as JsonObject ?? new JsonObject();
                if (!hadChargesPerReturn || !hadChargesUsedThisReturn)
                    changed = true;
                if (currentUsedCharges != 0)
                    changed = true;
                if (!hadReadableChargesPerReturn || previousChargesPerReturn != chargesPerReturn)
                {
                    changed = true;
                }

                gachaSystem["chargesPerReturn"] = chargesPerReturn;
                gachaSystem["chargesUsedThisReturn"] = 0;
                guardian["gachaSystem"] = gachaSystem;
            }

            if (root["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians.OfType<JsonObject>())
                    ResetGuardian(guardian);
            }

            if (root["activeGuardian"] is JsonObject activeGuardian)
                ResetGuardian(activeGuardian);

            if (changed)
            {
                await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json",
                    root.ToJsonString(JsonOpts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка сброса guardian gacha charges после возвращения в Море Хаоса");
        }
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for the GM to respond (reused for incarnation/end-of-life transitions).
    /// Waits indefinitely — only Escape cancels. No hard timeout.
    /// </summary>
    private async Task<bool> WaitForGmResponse()
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var rollbackSnapshot = GetRollbackSnapshot(manifest);
        using var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        var waitTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (_fs.FileExists("ready/turn_complete.json"))
                    return true;
                if (_fs.FileExists("ready/turn_error.json"))
                    return false;
                await Task.Delay(500, cts.Token);
            }
            return false;
        }, cts.Token);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(_loc.T("thinking"), async ctx =>
            {
                var keyTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                return;
                            }
                        }
                        Thread.Sleep(100);
                    }
                });

                while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                {
                    var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                    if (elapsed < 15)
                        ctx.Status($"[cyan]{_loc.T("thinking")}[/]");
                    else if (elapsed < 120)
                        ctx.Status($"[yellow]⏳ Ожидание GM-демона... ({elapsed}с) (Escape = отменить)[/]");
                    else
                        ctx.Status($"[yellow]⏳ GM обрабатывает ход... ({elapsed / 60}мин {elapsed % 60}с) (Escape = отменить)[/]");
                    await Task.Delay(1000);
                }

                try { return await waitTask; }
                catch (OperationCanceledException) { return false; }
            });

        if (cts.IsCancellationRequested)
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён, состояние восстановлено из rollback backup. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён. Rollback backup для этого режима недоступен; если GM завершит уже отправленный ход позже, он всё равно придёт как отложенный ответ.[/]");
            }
            return false;
        }

        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(manifest, rollbackSnapshot);
        if (terminalOutcome.Kind == "failure")
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            return false;
        }

        if (terminalOutcome.Kind == "success")
        {
            var signal = terminalOutcome.Signal;
            var expectedTurn = signal?.TurnNumber ?? manifest?.TurnNumber ?? (_gameLoop.TurnNumber + 1);
            if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                    "ответа GM",
                    rollbackSnapshot,
                    expectedTurn,
                    manifest?.ProgressionControl))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                _fs.DeleteFile("ready/turn_complete.json");
                _fs.DeleteFile("ready/turn_error.json");
                await CleanupPendingTurnSnapshotAsync();
                return false;
            }

            _audioService.PlayCue(AudioCue.TurnReady);
            var response = await BuildGameResponseFromFiles();
            _gameLoop.IncrementTurn();

            // Debug: log narrative length to help diagnose rendering issues
            if (string.IsNullOrEmpty(response?.Response))
                AnsiConsole.MarkupLine("[yellow dim]⚠ Нарратив пуст в ответе GM[/]");

            _lastResponse = response;
            _pendingImagePrompt = null;

            await CheckLifeTransitions();
            await CheckAscensionTrigger();

            if (await HasPendingMemoryLegacyAwaitingConsumptionAsync())
                await FinalizePendingMemoryLegacyConsumptionAsync();

            _pendingMemoryLegacyAwaitingConsumption = false;

            var qteHandling = await HandleAcceptedQteOfferAsync(response, manifest);
            if (qteHandling.EarlyExit)
            {
                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
                return true;
            }

            _lastResponse = qteHandling.Response;
            _pendingImagePrompt = qteHandling.Response?.ImagePrompt;

            if (IsIncarnationSourceLabel(manifest?.SourceLabel))
                await _worldDirectiveService.MaterializePendingToActiveAsync();

            await ConsumeAfterlifeReturnProtectionIfNeededAsync(manifest);

            _fs.DeleteFile("ready/turn_complete.json");
            await CleanupPendingTurnSnapshotAsync();
            return true;
        }

        _pendingMemoryLegacyAwaitingConsumption = false;
        await ShowTurnErrorMessageAsync("ready/turn_error.json");
        _fs.DeleteFile("ready/turn_error.json");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Переходный ход завершился ошибкой GM. Состояние откатилось к последней стабильной версии.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return false;
    }

    /// <summary>
    /// Waits for GM response without side effects (no turn increment, no CheckLifeTransitions).
    /// Used by transition methods that manage their own state.
    /// Returns true if response received, false if cancelled/error.
    /// </summary>
    private async Task<bool> WaitForGmResponseRaw()
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var rollbackSnapshot = GetRollbackSnapshot(manifest);
        using var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        var waitTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (_fs.FileExists("ready/turn_complete.json"))
                    return true;
                if (_fs.FileExists("ready/turn_error.json"))
                    return false;
                await Task.Delay(500, cts.Token);
            }
            return false;
        }, cts.Token);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(_loc.T("thinking"), async ctx =>
            {
                var keyTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                return;
                            }
                        }
                        Thread.Sleep(100);
                    }
                });

                while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                {
                    var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                    if (elapsed < 15)
                        ctx.Status($"[cyan]{_loc.T("thinking")}[/]");
                    else if (elapsed < 120)
                        ctx.Status($"[yellow]⏳ Ожидание GM-демона... ({elapsed}с) (Escape = отменить)[/]");
                    else
                        ctx.Status($"[yellow]⏳ GM обрабатывает ход... ({elapsed / 60}мин {elapsed % 60}с) (Escape = отменить)[/]");
                    await Task.Delay(1000);
                }

                try { return await waitTask; }
                catch (OperationCanceledException) { return false; }
            });

        if (cts.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён, состояние восстановлено из rollback backup. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён. Rollback backup для этого режима недоступен; если GM завершит уже отправленный ход позже, он всё равно придёт как отложенный ответ.[/]");
            }
            return false;
        }

        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(manifest, rollbackSnapshot);
        if (terminalOutcome.Kind == "failure")
            return false;

        if (terminalOutcome.Kind == "error")
        {
            await ShowTurnErrorMessageAsync("ready/turn_error.json");
            _fs.DeleteFile("ready/turn_error.json");

            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[yellow]↩ Переходный ход завершился ошибкой GM. Состояние откатилось к последней стабильной версии.[/]");
            }

            await CleanupPendingTurnSnapshotAsync();
            return false;
        }

        _audioService.PlayCue(AudioCue.TurnReady);

        return true;
    }

    // ═══════════════════════════════════════════════
    // GAME LOOP
    // ═══════════════════════════════════════════════

    private async Task EnterGameLoop()
    {
        _inGame = true;
        await _audioService.PlayInGameMusicAsync();
        await NormalizePendingRepairArtifactsAsync();
        await NormalizePendingTerminalProtocolFailureArtifactsAsync();

        // Check if there's already a correlated completion signal waiting
        if (_fs.FileExists("ready/turn_complete.json"))
        {
            await RefreshCanonicalStateAsync();
        }

        while (_inGame)
        {
            try
            {
            // Pick up late responses (agent finished after cancel/timeout, or response from previous turn)
            var manifest = await LoadPendingTurnSnapshotManifestAsync();
            var rollbackSnapshot = GetRollbackSnapshot(manifest);
            if (await ResolveConcurrentActiveTerminalSignalsAsync(manifest, rollbackSnapshot))
                continue;

            if (_fs.FileExists("ready/turn_error.json"))
            {
                var signal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
                if (await DiscardMismatchedReadySignalAsync("late turn_error", signal, manifest))
                    continue;

                var signalTurn = signal?.TurnNumber;
                var expectedTurn = _gameLoop.TurnNumber + 1;
                if (signalTurn.HasValue && signalTurn.Value != expectedTurn)
                {
                    _logger.LogWarning("Игнорируется late error для хода {Turn}, ожидался ход {ExpectedTurn}", signalTurn.Value, expectedTurn);
                    _fs.DeleteFile("ready/turn_error.json");
                    ClearTransientOutputFiles();
                    await CleanupPendingTurnSnapshotAsync();
                    continue;
                }

                await ShowTurnErrorMessageAsync("ready/turn_error.json");
                if (HasRollbackCapability(rollbackSnapshot))
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                    AnsiConsole.MarkupLine("[yellow]↩ Поздний сигнал ошибки GM восстановил последнюю стабильную версию состояния.[/]");
                }

                _fs.DeleteFile("ready/turn_error.json");
                await CleanupPendingTurnSnapshotAsync();
                continue;
            }

        if (_fs.FileExists("ready/turn_complete.json"))
        {
            var signal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
            if (await DiscardMismatchedReadySignalAsync("late turn_complete", signal, manifest))
                continue;

                var signalTurn = signal?.TurnNumber;
                var expectedTurn = _gameLoop.TurnNumber + 1;
                if (signalTurn.HasValue && signalTurn.Value != expectedTurn)
                {
                    _logger.LogWarning("Игнорируется late response для хода {Turn}, ожидался ход {ExpectedTurn}", signalTurn.Value, expectedTurn);
                    _fs.DeleteFile("ready/turn_complete.json");
                    ClearTransientOutputFiles();
                    await CleanupPendingTurnSnapshotAsync();
                    continue;
                }

                if (await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                        "late response GM",
                        GetRollbackSnapshot(manifest),
                        signalTurn ?? expectedTurn,
                        manifest?.ProgressionControl))
                {
                    var lateResponse = await BuildGameResponseFromFiles();
                    if (lateResponse == null || string.IsNullOrEmpty(lateResponse.Response))
                        AnsiConsole.MarkupLine("[yellow dim]⚠ Нарратив пуст в late response GM[/]");
                    else
                    _lastResponse = lateResponse;
                    _pendingImagePrompt = null;
                    _gameLoop.IncrementTurn();
                    await CheckLifeTransitions();
                    await CheckAscensionTrigger();
                    if (await HasPendingMemoryLegacyAwaitingConsumptionAsync())
                        await FinalizePendingMemoryLegacyConsumptionAsync();

                    var qteHandling = await HandleAcceptedQteOfferAsync(lateResponse, manifest);
                    if (!qteHandling.EarlyExit)
                    {
                        _lastResponse = qteHandling.Response;
                        _pendingImagePrompt = qteHandling.Response?.ImagePrompt;
                    }

                    if (IsIncarnationSourceLabel(manifest?.SourceLabel))
                        await _worldDirectiveService.MaterializePendingToActiveAsync();

                    await ConsumeAfterlifeReturnProtectionIfNeededAsync(manifest);
                }
                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
            }

            // Check for GM-initiated incarnation (GM sends player to Mortal World)
            await CheckAscensionTrigger();
            await CheckGmIncarnationTrigger();

            var resumedQte = await _qteSceneService.ResumeActiveSceneIfAnyAsync(_gameLoop.TurnNumber);
            if (resumedQte != null)
            {
                _lastResponse = resumedQte.Response;
                _pendingImagePrompt = resumedQte.Response?.ImagePrompt;
                await ProcessStatsIncreasedAsync();
                await _charService.ComputeAndWriteAsync();
                await CheckLevelUpAsync();
                await CheckLifeTransitions();
                await CheckAscensionTrigger();
                continue;
            }

            // Detect console resize — if width changed, just re-render (loop continues)
            try
            {
                var currentWidth = Console.WindowWidth;
                if (_lastConsoleWidth > 0 && currentWidth != _lastConsoleWidth)
                {
                    await NormalizeRuntimeUiArtifactsAsync();
                    await RefreshCanonicalStateAsync();
                }
                _lastConsoleWidth = currentWidth;
            }
            catch { }

            // Render current state (preserve last response for dialogue options etc.)
            _ui.RenderGameScreen(_stateManager.CurrentState, _lastResponse, _gameLoop.TurnNumber);

            // Show scene image if pending (after game screen so it stays visible during input)
            if (!string.IsNullOrEmpty(_pendingImagePrompt))
            {
                await _imageService.ProcessSceneImagePrompt(_pendingImagePrompt);
                _pendingImagePrompt = null;
            }

            // Get player input (with Shift+Enter for multiline)
            var input = await GetPlayerInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Check for in-game menu commands
            if (input.Equals("/refresh", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("/обновить", StringComparison.OrdinalIgnoreCase))
            {
                await NormalizeRuntimeUiArtifactsAsync();
                await RefreshCanonicalStateAsync();
                var refreshedResponse = MergeWithLastResponse(await BuildGameResponseFromFiles());
                if (!await ValidateCurrentGameStateOrShowErrorsAsync("ручного обновления"))
                    continue;
                _lastResponse = refreshedResponse;
                _pendingImagePrompt = null; // Don't re-trigger image on refresh
                AnsiConsole.MarkupLine("[green]✔ Состояние игры обновлено из файлов[/]");
                await Task.Delay(600);
                continue; // Re-renders on next loop iteration
            }

            if (input.Equals("/options", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("/опции", StringComparison.OrdinalIgnoreCase))
            {
                var shouldContinue = await InGameOptionsMenu();
                if (!shouldContinue)
                {
                    _inGame = false;
                    continue;
                }
                continue;
            }

            // Check for incarnation command (Chaos Sea → Mortal Life)
            if ((input.Equals("/incarnate", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/воплотиться", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInChaosSea)
            {
                await HandleIncarnation();
                await WaitForGmResponse();
                continue;
            }

            if ((input.Equals("/new_game_plus", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/новая_игра+", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInShiningAbode)
            {
                await HandleNewGamePlus();
                continue;
            }

            // Check for end of life command (Mortal Life → Chaos Sea)
            if ((input.Equals("/end_of_life", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/конец_жизни", StringComparison.OrdinalIgnoreCase)) &&
                !_stateManager.CurrentState.IsInAfterlifeRealm)
            {
                await HandleEndOfLife();
                continue;
            }

            // Check for local explorer commands
            if (_explorer.IsCommand(input))
            {
                var result = await _explorer.TryProcessCommand(input);
                if (result != null)
                {
                    // If the command produced a GM action (e.g., equip/unequip), send it
                    if (result.Length > 0)
                        await ProcessPlayerTurn(result);
                    continue;
                }

                // Recognized slash prefix but unknown command
                var cmd = input.Trim().Split(' ')[0];
                AnsiConsole.MarkupLine($"[yellow]⚠️ Неизвестная команда: {GameInterface.EscapeMarkup(cmd)}[/]");
                AnsiConsole.MarkupLine("[dim]Введите /help для списка доступных команд.[/]");
                continue;
            }

            // Send to GM
            await ProcessPlayerTurn(input);

            }
            catch (Exception ex)
            {
                LogError(ex);
                AnsiConsole.MarkupLine($"\n[red]❌ Ошибка в игровом цикле: {GameInterface.EscapeMarkup(ex.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Ошибка сохранена в game_session/error_log.txt. Данные не потеряны.[/]");
                AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
                Console.ReadKey(true);
            }
        }
    }

    private async Task ProcessPlayerTurn(string action, string? extraSystemReminder = null)
    {
        // Create backup of game state files before sending turn (for escape-rollback)
        var backupId = DateTime.UtcNow.Ticks.ToString();
        var backedUpFiles = await CreatePreTurnBackup(backupId);

        // Write turn request
        var request = new TurnRequest
        {
            SessionId = _gameLoop.SessionId,
            TurnNumber = _gameLoop.TurnNumber + 1,
            PlayerAction = action,
            Timestamp = DateTime.UtcNow.ToString("o"),
            GameMode = _stateManager.Settings.AllowHistoryManipulation ? "debug" : "normal",
            SystemReminder = await BuildTurnSystemReminderAsync(extraSystemReminder)
        };
        await AttachPendingDiceAndGachaAsync(request);
        request.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync();
        var canonicalSnapshot = await CreateCanonicalBaselineSnapshotAsync(request, backedUpFiles, OrdinaryPlayerTurnSourceLabel);

        // Attach computed characteristics for GM reference
        try
        {
            var computed = await _charService.ComputeAsync();
            var charContext = new Dictionary<string, object>();
            foreach (var (name, stat) in computed.Stats)
            {
                charContext[name] = new
                {
                    standard = stat.BaseValue,
                    permanentlyModified = stat.PermanentlyModified,
                    modified = stat.Modified
                };
            }
            request.ComputedCharacteristics = new
            {
                playerLevel = computed.PlayerLevel,
                unspentStatPoints = computed.UnspentStatPoints,
                stats = charContext
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось вычислить характеристики для контекста");
        }

        ClearTransientOutputFiles();
        await _fs.WriteFileAtomicAsync("input/turn_request.json",
            JsonSerializer.Serialize(request, JsonOpts));

        // Show waiting status — no hard timeout, only Escape cancels
        using var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        var waitTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (_fs.FileExists("ready/turn_complete.json"))
                    return true;
                if (_fs.FileExists("ready/turn_error.json"))
                    return false;
                await Task.Delay(500, cts.Token);
            }
            return false;
        }, cts.Token);

        // Show spinner while waiting, allow Escape to cancel
        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(_loc.T("thinking"), async ctx =>
            {
                var keyTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                return;
                            }
                        }
                        Thread.Sleep(100);
                    }
                });

                while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                {
                    var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                    if (elapsed < 15)
                        ctx.Status($"[cyan]{_loc.T("thinking")}[/]");
                    else if (elapsed < 120)
                        ctx.Status($"[yellow]⏳ Ожидание GM-демона... ({elapsed}с) (Escape = отменить)[/]");
                    else
                        ctx.Status($"[yellow]⏳ GM обрабатывает ход... ({elapsed / 60}мин {elapsed % 60}с) (Escape = отменить)[/]");
                    await Task.Delay(1000);
                }

                try
                {
                    return await waitTask;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            });

        if (cts.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            // Delete the turn request, clean ready signals, and rollback game state
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            await RestorePreTurnBackup(backedUpFiles);
            AnsiConsole.MarkupLine("[dim]Изменения локально отменены, состояние восстановлено. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            CleanupBackup(backedUpFiles);
            return;
        }

        var activeManifest = await LoadPendingTurnSnapshotManifestAsync();
        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(activeManifest, backedUpFiles);
        if (terminalOutcome.Kind == "failure")
            return;

        if (terminalOutcome.Kind == "error")
        {
            await ShowTurnErrorMessageAsync("ready/turn_error.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            await CleanupPendingTurnSnapshotAsync();
            CleanupBackup(backedUpFiles);
            return;
        }

        // Read and validate the response before accepting the turn
        if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                "обработки хода",
                backedUpFiles,
                request.TurnNumber,
                request.ProgressionControl))
        {
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            _fs.DeleteFile("input/turn_request.json");
            await CleanupPendingTurnSnapshotAsync();
            return;
        }
        var response = await BuildGameResponseFromFiles();

        // Turn accepted — backup no longer needed
        CleanupBackup(backedUpFiles);
        await CleanupPendingTurnSnapshotAsync();

        _gameLoop.IncrementTurn();
        await _pendingTurnState.RotateAfterAcceptedTurnAsync();
        _lastResponse = response;
        _pendingImagePrompt = null;

        // Persist turn to story file
        var state = _stateManager.CurrentState;
        await _storyService.AppendTurnAsync(
            _gameLoop.TurnNumber,
            state.CurrentRealm ?? "Chaos Sea",
            state.Incarnation,
            action,
            response?.Response,
            state.CurrentLocation);

        // Process statsIncreased and recompute modified characteristics
        await ProcessStatsIncreasedAsync();
        await _charService.ComputeAndWriteAsync();

        // Check for level-up: if level increased, grant 5 stat points
        await CheckLevelUpAsync();

        // Check for GM-triggered life transitions
        await CheckLifeTransitions();
        await CheckAscensionTrigger();

        var qteHandling = await HandleAcceptedQteOfferAsync(response, activeManifest);
        if (qteHandling.EarlyExit)
            return;

        _lastResponse = qteHandling.Response;
        _pendingImagePrompt = qteHandling.Response?.ImagePrompt;

        // Autosave
        if (_stateManager.Settings.AutosaveIntervalTurns > 0 &&
            _gameLoop.TurnNumber % _stateManager.Settings.AutosaveIntervalTurns == 0)
        {
            await _saveLoad.AutosaveAsync(_gameLoop.TurnNumber);
        }

        // Cleanup ready signal
        _fs.DeleteFile("ready/turn_complete.json");
    }

    private async Task<(bool EarlyExit, GameResponse Response)> HandleAcceptedQteOfferAsync(
        GameResponse? response,
        PendingTurnSnapshotManifest? manifest)
    {
        response ??= new GameResponse();
        var offer = await _qteSceneService.TryReadOfferAsync();
        if (offer == null)
        {
            await _qteSceneService.ClearDeclineMarkerAsync();
            return (false, response);
        }

        if (!QteSceneService.IsEligibleOfferSourceLabel(manifest?.SourceLabel))
        {
            _logger.LogError(
                "QTE offer {QteId} получен вне обычного игрокского хода (SourceLabel={SourceLabel}) и будет проигнорирован.",
                offer.QteId,
                manifest?.SourceLabel ?? "<missing>");
            _qteSceneService.ClearOfferFile();
            return (false, response);
        }

        var decision = await _qteSceneService.PromptOfferDecisionAsync(offer);
        if (decision == QteSceneService.QteOfferDecision.Decline)
        {
            await _qteSceneService.RecordDeclineAsync(offer, _gameLoop.TurnNumber);
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");

            var originalAction = manifest?.PlayerAction;
            if (!string.IsNullOrWhiteSpace(originalAction) &&
                QteSceneService.IsEligibleOfferSourceLabel(manifest?.SourceLabel))
            {
                var declineReminder =
                    $"[QTE_DECLINED:{offer.QteId}] Игрок отклонил QTE-сценарий. Разреши ту же ситуацию обычными игровыми механиками. Повторно предлагать этот qteId запрещено.";
                await ProcessPlayerTurn(originalAction, declineReminder);
            }

            return (true, response);
        }

        var completion = await _qteSceneService.StartAcceptedSceneAsync(offer, _gameLoop.TurnNumber);
        await ProcessStatsIncreasedAsync();
        await _charService.ComputeAndWriteAsync();
        await CheckLevelUpAsync();
        await CheckLifeTransitions();
        await CheckAscensionTrigger();
        return (false, completion.Response);
    }

    /// <summary>
    /// Reads statsIncreased from status_changes.json, applies +1 with Training Cap,
    /// awards XP compensation if blocked, then clears the statsIncreased field.
    /// </summary>
    private async Task ProcessStatsIncreasedAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync("game_state/player/status_changes.json");
            if (json == null) return;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("statsIncreased", out var si)) return;

            // Parse stats array
            var statsToIncrease = new List<string>();
            if (si.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in si.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        statsToIncrease.Add(item.GetString() ?? "");
                }
            }

            if (statsToIncrease.Count == 0) return;

            var (applied, blocked) = await _charService.ApplyStatsIncreasedAsync(statsToIncrease.ToArray());

            // Award XP compensation for blocked stats
            if (blocked.Count > 0)
            {
                var expForNext = 100; // default
                var expJson = await _fs.ReadFileAsync("game_state/player/experience.json");
                if (expJson != null)
                {
                    try
                    {
                        using var expDoc = JsonDocument.Parse(expJson);
                        if (expDoc.RootElement.TryGetProperty("experienceForNextLevel", out var efn) &&
                            efn.ValueKind == JsonValueKind.Number)
                            expForNext = efn.GetInt32();
                    }
                    catch { /* use default */ }
                }
                var xpComp = Math.Max(25, (int)Math.Round(expForNext * 0.05));

                // Write compensation XP (will be picked up by state refresh)
                var compObj = new { experienceCompensation = xpComp * blocked.Count, reason = "Training Cap compensation" };
                _logger.LogInformation("Training Cap: {Count} стат заблокировано, XP компенсация: {XP}",
                    blocked.Count, xpComp * blocked.Count);
            }

            // Show notifications
            foreach (var stat in applied)
            {
                var ruName = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
                AnsiConsole.MarkupLine($"  [green]📈 {Markup.Escape(ruName)} +1 (тренировка)[/]");
            }
            foreach (var stat in blocked)
            {
                var ruName = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
                AnsiConsole.MarkupLine($"  [yellow]⚠ {Markup.Escape(ruName)}: Training Cap достигнут[/]");
            }

            // Clear statsIncreased after processing
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "statsIncreased") continue;
                dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            await _fs.WriteFileAtomicAsync("game_state/player/status_changes.json",
                JsonSerializer.Serialize(dict, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка обработки statsIncreased");
        }
    }

    /// <summary>
    /// Detects level-up by reading current level from player_status or experience.json
    /// and comparing with last known level. Grants 5 stat points on level-up.
    /// </summary>
    private async Task CheckLevelUpAsync()
    {
        try
        {
            var currentLevel = 1;

            // Check experience.json for level info (GM writes this)
            var expJson = await _fs.ReadFileAsync("game_state/player/experience.json");
            if (expJson != null)
            {
                using var doc = JsonDocument.Parse(expJson);
                if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.Number)
                    currentLevel = lvl.GetInt32();
                else if (doc.RootElement.TryGetProperty("playerLevel", out var pl) &&
                    pl.ValueKind == JsonValueKind.Number)
                    currentLevel = pl.GetInt32();
            }

            // Also check player_status for level
            if (currentLevel <= 1)
            {
                var statusJson = await _fs.ReadFileAsync("game_state/core/player_status.json");
                if (statusJson != null)
                {
                    using var doc = JsonDocument.Parse(statusJson);
                    if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                        lvl.ValueKind == JsonValueKind.Number)
                        currentLevel = lvl.GetInt32();
                }
            }

            if (currentLevel > _lastKnownLevel)
            {
                var levelsGained = currentLevel - _lastKnownLevel;
                var totalPoints = levelsGained * 5;

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[gold1]⭐ ПОВЫШЕНИЕ УРОВНЯ![/]").RuleStyle("gold1"));
                AnsiConsole.MarkupLine($"  [bold yellow]Уровень {_lastKnownLevel} → {currentLevel}[/]");
                AnsiConsole.MarkupLine($"  [green]+{totalPoints} очков характеристик![/]");
                AnsiConsole.WriteLine();

                await _charService.AddStatPoints(totalPoints);
                _lastKnownLevel = currentLevel;

                // Offer stat distribution
                await ShowStatDistribution($"Повышение уровня! +{totalPoints} очков характеристик");
            }
            else
            {
                _lastKnownLevel = currentLevel;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка проверки уровня");
        }
    }

    /// <summary>
    /// Checks for GM-triggered life transitions (death in mortal world → Chaos Sea).
    /// Sends life evaluation request to GM, waits for response with rewards, shows reward screen.
    /// </summary>
    private async Task CheckLifeTransitions()
    {
        var transJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        if (transJson == null) return;

        var currentRealm = _stateManager.CurrentState.CurrentRealm ?? string.Empty;
        if (string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RollbackSnapshot? rollbackBackups = null;
        var localStateMutated = false;
        var manifestCreated = false;
        var requestDispatched = false;

        try
        {
            using var doc = JsonDocument.Parse(transJson);
            var root = doc.RootElement;

            // If TriggerLifeEnd is present, transition back to Chaos Sea
            if (!TryReadLifeTransitionPayload(root, out var reason, out var summary))
                return;

            rollbackBackups = await CreatePreTurnBackup(DateTime.UtcNow.Ticks.ToString());

            // === PHASE 1: Death screen ===
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new FigletText("Death").Color(Color.DarkRed).Centered());
            AnsiConsole.Write(new Rule("[yellow]💀 Конец смертной жизни[/]").RuleStyle("yellow"));

            if (!string.IsNullOrEmpty(reason))
                AnsiConsole.MarkupLine($"[yellow]Причина: {GameInterface.EscapeMarkup(reason)}[/]");
            if (!string.IsNullOrEmpty(summary))
                AnsiConsole.MarkupLine($"[dim]{GameInterface.EscapeMarkup(summary)}[/]");

            AnsiConsole.MarkupLine($"\n[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);

            // === PHASE 2: Capture pre-death state for reward comparison ===
            var preDeathInkFeathers = _stateManager.CurrentState.InkFeathers;
            var preDeathEnlightenment = _stateManager.CurrentState.EnlightenmentTier;
            var preDeathSoulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

            // Build life summary for Guardian knowledge persistence
            var lifeSummary = BuildLifeSummary(summary);

            // Mark end of mortal life in story
            localStateMutated = true;
            var deathState = _stateManager.CurrentState;
            var lifecycleMarker = string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase)
                ? "VOLUNTARY_END"
                : "DEATH";
            await _storyService.AppendMarkerAsync(
                "Mortal World", deathState.Incarnation,
                lifecycleMarker, $"Конец смертной жизни. Причина: {reason}. {summary}");

            // === PHASE 3: Update realm and send life evaluation to GM ===
            await UpdateSoulStateRealm("Chaos Sea", lifeSummary);
            _fs.ClearCurrentWorldLore();

            // Clean up transition signal BEFORE sending turn (avoid re-trigger)
            _fs.DeleteFile("game_state/control/life_transitions.json");

            // Send life evaluation request to GM
            var evalRequest = new TurnRequest
            {
                SessionId = _gameLoop.SessionId,
                TurnNumber = _gameLoop.TurnNumber + 1,
                PlayerAction = "Душа покидает смертную оболочку. Начинается Оценка Жизни (Block 31.1). " +
                               "Рассчитай награду за прожитую жизнь: Чернильные Перья (формула из Block 31.1.2), " +
                               "обнови просветление (Block 31.1.3), запиши завершённую инкарнацию в metaStateUpdates. " +
                               "Создай Реликвии Души из значимых моментов жизни (Block 31.2). " +
                               "После оценки опиши возвращение в Море Хаоса к Хранителю. " +
                               $"Краткий итог жизни: {lifeSummary}",
                Timestamp = DateTime.UtcNow.ToString("o"),
                GameMode = "normal",
                SystemReminder = await BuildTurnSystemReminderAsync()
            };
            AttachFreshDiceAndGacha(evalRequest);
            evalRequest.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync("Chaos Sea");
            await CreateCanonicalBaselineSnapshotAsync(evalRequest, rollbackBackups, LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel);
            manifestCreated = true;

            ClearTransientOutputFiles();
            await _fs.WriteFileAtomicAsync("input/turn_request.json",
                JsonSerializer.Serialize(evalRequest, JsonOpts));
            requestDispatched = true;

            // Visual transition to Chaos Sea
            GameInterface.RenderRealmTransition(true);

            // === PHASE 4: Wait for GM response with life evaluation ===
            // Use raw wait — no turn increment, no recursive CheckLifeTransitions
            if (await WaitForGmResponseRaw())
            {
                var manifest = await LoadPendingTurnSnapshotManifestAsync();
                if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                        "оценки жизни",
                        GetRollbackSnapshot(manifest),
                        _gameLoop.TurnNumber + 1,
                        evalRequest.ProgressionControl))
                {
                    _fs.DeleteFile("ready/turn_complete.json");
                    await CleanupPendingTurnSnapshotAsync();
                    return;
                }

                var evalResponse = await BuildGameResponseFromFiles();
                _gameLoop.IncrementTurn();
                _lastResponse = evalResponse;
                _pendingImagePrompt = evalResponse?.ImagePrompt;

                // Log the evaluation turn to story
                await _storyService.AppendTurnAsync(
                    _gameLoop.TurnNumber,
                    "Chaos Sea", 0,
                    "[LIFE_EVALUATION] Оценка прожитой жизни",
                    evalResponse?.Response,
                    "Море Хаоса");

                // === PHASE 5: Show reward screen ===
                await ShowLifeEvaluationRewards(preDeathInkFeathers, preDeathEnlightenment, preDeathSoulStateJson);
                var guardianContext = await _afterlifeReturnGuardService.ReadActiveGuardianContextAsync();
                await _afterlifeReturnGuardService.ActivatePostLifeReturnAsync(
                    guardianContext.GuardianId,
                    guardianContext.GuardianName,
                    _gameLoop.TurnNumber);

                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
            }
        }
        catch (Exception ex)
        {
            if (!requestDispatched)
                await CleanupUndispatchedTransitionPrepAsync(rollbackBackups, localStateMutated, manifestCreated);
            _logger.LogWarning(ex, "Ошибка обработки перехода жизни");
        }
    }

    /// <summary>
    /// Displays life evaluation rewards — comparing before/after soul state.
    /// </summary>
    private async Task ShowLifeEvaluationRewards(int preDeathInkFeathers, string preDeathEnlightenment, string? preDeathSoulStateJson)
    {
        // Re-read soul state for latest values (GM should have updated it)
        await RefreshCanonicalStateAsync();
        var state = _stateManager.CurrentState;

        var newInkFeathers = state.InkFeathers;
        var newEnlightenment = state.EnlightenmentTier;
        var feathersEarned = newInkFeathers - preDeathInkFeathers;

        var relicCount = 0;
        var newRelics = new List<string>();
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (LifeEvaluationRewardAnalyzer.TryComputeDelta(preDeathSoulStateJson, soulJson, out var rewardDelta, out _) &&
            rewardDelta != null)
        {
            feathersEarned = rewardDelta.InkFeathersEarned;
            newRelics = rewardDelta.NewRelics
                .Select(relic => string.IsNullOrWhiteSpace(relic.Rarity)
                    ? relic.Name
                    : $"{relic.Name} ({relic.Rarity})")
                .ToList();
            relicCount = rewardDelta.NewRelics.Count;
        }
        else if (soulJson != null)
        {
            try
            {
                using var soulDoc = JsonDocument.Parse(soulJson);
                if (soulDoc.RootElement.TryGetProperty("soulRelics", out var relics))
                {
                    if (relics.TryGetProperty("stored", out var stored) && stored.ValueKind == JsonValueKind.Array)
                        relicCount += stored.GetArrayLength();
                    if (relics.TryGetProperty("equipped", out var equipped) && equipped.ValueKind == JsonValueKind.Array)
                        relicCount += equipped.GetArrayLength();

                    // Try to find recently acquired relics
                    foreach (var arr in new[] { "stored", "equipped" })
                    {
                        if (relics.TryGetProperty(arr, out var ra) && ra.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var relic in ra.EnumerateArray())
                            {
                                if (relic.TryGetProperty("acquisitionData", out var acq) &&
                                    acq.TryGetProperty("acquisitionStory", out _))
                                {
                                    var name = relic.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
                                    var rarity = relic.TryGetProperty("rarity", out var rar) ? rar.GetString() ?? "" : "";
                                    newRelics.Add($"{name} ({rarity})");
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // Build reward panel
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Life Evaluation").Color(Color.Gold1).Centered());
        AnsiConsole.Write(new Rule("[gold1]✦ Оценка Прожитой Жизни ✦[/]").RuleStyle("gold1"));
        AnsiConsole.WriteLine();

        // Show narrative response first (GM's evaluation text)
        if (_lastResponse != null && !string.IsNullOrEmpty(_lastResponse.Response))
        {
            AnsiConsole.Write(new Panel(new Markup(GameInterface.EscapeMarkup(_lastResponse.Response)))
            {
                Header = new PanelHeader(" 📜 Слова Высших Сил ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();
        }

        // Rewards table
        var rewardsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Gold1)
            .Expand()
            .AddColumn(new TableColumn("[bold gold1]Награда[/]").NoWrap())
            .AddColumn(new TableColumn("[bold gold1]Значение[/]"));

        // Ink Feathers
        var featherColor = feathersEarned > 0 ? "green" : "yellow";
        var featherSign = feathersEarned > 0 ? "+" : "";
        rewardsTable.AddRow(
            "🪶 Чернильные Перья",
            $"[{featherColor}]{featherSign}{feathersEarned}[/]  [dim]({preDeathInkFeathers} → {newInkFeathers})[/]");

        // Enlightenment
        var enlChanged = !string.Equals(preDeathEnlightenment, newEnlightenment, StringComparison.OrdinalIgnoreCase);
        rewardsTable.AddRow(
            "✨ Просветление",
            enlChanged
                ? $"[green]{GameInterface.EscapeMarkup(preDeathEnlightenment)} → {GameInterface.EscapeMarkup(newEnlightenment)}[/]"
                : $"[dim]{GameInterface.EscapeMarkup(newEnlightenment)}[/]");

        // Soul Relics
        rewardsTable.AddRow(
            "💎 Реликвии Души",
            relicCount > 0 ? $"[cyan]+{relicCount} новых[/]" : "[dim]Новых реликвий нет[/]");

        // Lives lived
        rewardsTable.AddRow(
            "🔄 Инкарнация",
            $"[white]#{state.Incarnation}[/]  [dim]({_gameLoop.TurnNumber} ходов прожито)[/]");

        AnsiConsole.Write(rewardsTable);

        // Show new relics if any
        if (newRelics.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[cyan]💎 Реликвии[/]").RuleStyle("cyan"));
            foreach (var relic in newRelics)
                AnsiConsole.MarkupLine($"  [cyan]✦[/] {GameInterface.EscapeMarkup(relic)}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Вы вернулись в Море Хаоса. Ваш путь продолжается...[/]");
        AnsiConsole.MarkupLine($"\n[grey]{_loc.T("press_any_key")}[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Checks for GM-initiated incarnation trigger.
    /// GM can write game_state/control/incarnation_trigger.json to send the player to Mortal World.
    /// </summary>
    private async Task CheckGmIncarnationTrigger()
    {
        var triggerJson = await _fs.ReadFileAsync("game_state/control/incarnation_trigger.json");
        if (triggerJson == null) return;
        if (!_stateManager.CurrentState.IsInChaosSea) 
        {
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");
            return;
        }

        RollbackSnapshot? rollbackBackups = null;
        var localStateMutated = false;
        var manifestCreated = false;
        var requestDispatched = false;

        try
        {
            if (!IncarnationTriggerContract.TryParse(triggerJson, out var payload))
            {
                _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                return;
            }

            var rawReturnGuard = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
            if (payload.IsGuardianForced && !string.IsNullOrWhiteSpace(rawReturnGuard))
            {
                if (!AfterlifeReturnGuardService.TryParse(rawReturnGuard, out var activeReturnGuard))
                {
                    _logger.LogWarning(
                        "guardian_forced incarnation trigger ignored because afterlife_return_guard is invalid. Failing closed to preserve the protected return turn.");
                    _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                    return;
                }

                if (activeReturnGuard.RemainingProtectedTurns > 0)
                {
                    _logger.LogWarning(
                        "guardian_forced incarnation trigger ignored because afterlife_return_guard is still active (remainingProtectedTurns={Turns}).",
                        activeReturnGuard.RemainingProtectedTurns);
                    _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                    return;
                }
            }
            var worldDesc = payload.WorldDescription;
            var charDesc = payload.CharacterDescription;
            var circumstances = payload.Circumstances;

            // Show the GM-initiated incarnation banner
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("Soul Gates").Color(Color.Gold1).Centered());
            AnsiConsole.Write(payload.IsGuardianForced
                ? new Rule("[darkred]✦ Хранитель насильно распахивает Врата Души ✦[/]").RuleStyle("darkred")
                : new Rule("[gold1]✦ Врата Души открываются ✦[/]").RuleStyle("gold1"));
            AnsiConsole.WriteLine();
            if (payload.IsGuardianForced)
            {
                AnsiConsole.MarkupLine("[red]Враждебный Хранитель навязывает душе новое смертное воплощение.[/]");
                if (!string.IsNullOrWhiteSpace(payload.Reason))
                    AnsiConsole.MarkupLine($"[yellow]Причина санкции:[/] {GameInterface.EscapeMarkup(payload.Reason)}");
                if (!string.IsNullOrWhiteSpace(payload.ProvocationSummary))
                    AnsiConsole.MarkupLine($"[dim]Повод: {GameInterface.EscapeMarkup(payload.ProvocationSummary)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Хранитель направляет вас через Врата Души в мир смертных...[/]");
            }

            if (!string.IsNullOrWhiteSpace(worldDesc))
                AnsiConsole.MarkupLine($"[dim]Мир: {GameInterface.EscapeMarkup(worldDesc)}[/]");
            if (!string.IsNullOrWhiteSpace(charDesc))
                AnsiConsole.MarkupLine($"[dim]Персонаж: {GameInterface.EscapeMarkup(charDesc)}[/]");
            if (!string.IsNullOrWhiteSpace(circumstances))
                AnsiConsole.MarkupLine($"[dim]Обстоятельства: {GameInterface.EscapeMarkup(circumstances)}[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);

            // Build incarnation action from GM-provided data
            var parts = new List<string>
            {
                payload.IsGuardianForced
                    ? "Враждебный Хранитель насильно отправляет душу через Врата Души в тяжёлую смертную жизнь."
                    : "Хранитель направляет душу через Врата Души в мир смертных."
            };
            if (payload.IsGuardianForced)
            {
                if (!string.IsNullOrWhiteSpace(payload.GuardianId))
                    parts.Add($"Источник санкции: guardianId={payload.GuardianId}.");
                if (!string.IsNullOrWhiteSpace(payload.Reason))
                    parts.Add($"Причина: {payload.Reason}.");
                if (!string.IsNullOrWhiteSpace(payload.ProvocationSummary))
                    parts.Add($"Провокация игрока: {payload.ProvocationSummary}.");
                if (!string.IsNullOrWhiteSpace(payload.SeverityBand))
                    parts.Add($"Тяжесть старта: {payload.SeverityBand}.");
            }
            if (!string.IsNullOrWhiteSpace(charDesc))
                parts.Add($"Персонаж: {charDesc}.");
            if (!string.IsNullOrWhiteSpace(worldDesc))
                parts.Add($"Мир: {worldDesc}.");
            if (!string.IsNullOrWhiteSpace(circumstances))
                parts.Add($"Обстоятельства: {circumstances}.");

            rollbackBackups = await CreatePreTurnBackup(DateTime.UtcNow.Ticks.ToString());

            // Each incarnation must create a fresh mortal-world lore set.
            _fs.ClearCurrentWorldLore();
            await _afterlifeReturnGuardService.ClearAsync();

            // Update soul state: switch realm to Mortal World and increment incarnation
            localStateMutated = true;
            await UpdateSoulStateRealm("Mortal World", incrementIncarnation: true);

            // Initialize fresh mortal status
            var status = new
            {
                healthPercentage = "100%",
                energyPercentage = "100%",
                poisePercentage = "100%",
                currentCondition = "Здоров",
                activeConditions = Array.Empty<string>(),
                money = 0
            };
            await _fs.WriteFileAtomicAsync("game_state/core/player_status.json",
                JsonSerializer.Serialize(status, JsonOpts));

            // Initialize empty mortal inventory
            var inventory = new
            {
                items = Array.Empty<object>(),
                equipment = new
                {
                    head = (object?)null, body = (object?)null, hands = (object?)null,
                    feet = (object?)null, mainHand = (object?)null, offHand = (object?)null,
                    neck = (object?)null, ring1 = (object?)null, ring2 = (object?)null
                },
                totalWeight = 0,
                maxWeight = 45
            };
            await _fs.WriteFileAtomicAsync("game_state/inventory/items.json",
                JsonSerializer.Serialize(inventory, JsonOpts));

            // Mark new incarnation in story
            await _storyService.AppendMarkerAsync(
                "Chaos Sea", 0,
                "INCARNATION", $"Душа воплощается в новую смертную жизнь. Инкарнация #{_stateManager.CurrentState.Incarnation + 1}.");

            // Initialize characteristics for new incarnation
            await _charService.InitializeForNewIncarnation();
            var memoryLegacySummary = await ApplyPendingMemoryLegacyForIncarnationAsync();
            if (!string.IsNullOrWhiteSpace(memoryLegacySummary))
            {
                AnsiConsole.MarkupLine($"[magenta]🧠 Наследие Памяти:[/] {Markup.Escape(memoryLegacySummary)}");
                AnsiConsole.WriteLine();
                parts.Add($"Активировано Наследие Памяти: {memoryLegacySummary}.");
            }
            await ShowStatDistribution("Новая инкарнация — распределите начальные очки характеристик");
            await CapturePendingMemoryLegacyApplicationAuditAsync();

            // Send incarnation turn to GM
            var request = new TurnRequest
            {
                SessionId = _gameLoop.SessionId,
                TurnNumber = _gameLoop.TurnNumber + 1,
                PlayerAction = string.Join(" ", parts),
                Timestamp = DateTime.UtcNow.ToString("o"),
                GameMode = "normal",
                SystemReminder = await BuildTurnSystemReminderAsync()
            };
            AttachFreshDiceAndGacha(request);
            request.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync("Mortal World");
            await CreateCanonicalBaselineSnapshotAsync(request, rollbackBackups, "GM-инициированного воплощения");
            manifestCreated = true;
            ClearTransientOutputFiles();
            await _fs.WriteFileAtomicAsync("input/turn_request.json",
                JsonSerializer.Serialize(request, JsonOpts));
            requestDispatched = true;

            // Clean up trigger file
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");

            // Visual transition
            GameInterface.RenderRealmTransition(false);

            // Wait for GM response describing the new mortal world
            if (await WaitForGmResponse())
            {
                await RefreshCanonicalStateAsync();
                await _worldDirectiveService.MaterializePendingToActiveAsync(worldDesc, circumstances);
            }
        }
        catch (Exception ex)
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            if (!requestDispatched)
                await CleanupUndispatchedTransitionPrepAsync(rollbackBackups, localStateMutated, manifestCreated);
            LogError(ex);
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");
        }
    }

    private async Task CheckAscensionTrigger()
    {
        var ascensionJson = await _fs.ReadFileAsync("game_state/control/ascension.json");
        if (string.IsNullOrWhiteSpace(ascensionJson))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm || _stateManager.CurrentState.IsInShiningAbode)
        {
            _fs.DeleteFile("game_state/control/ascension.json");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(ascensionJson);
            var root = doc.RootElement;
            var triggered =
                root.TryGetProperty("AscensionTrigger", out var legacyTrigger) &&
                legacyTrigger.ValueKind == JsonValueKind.True;
            var playerChoice = root.TryGetProperty("playerChoice", out var playerChoiceProp) &&
                               playerChoiceProp.ValueKind == JsonValueKind.String
                ? playerChoiceProp.GetString() ?? ""
                : "";

            if (!triggered || !string.Equals(playerChoice, "Ascension", StringComparison.OrdinalIgnoreCase))
            {
                _fs.DeleteFile("game_state/control/ascension.json");
                return;
            }

            if (_fs.FileExists("game_state/control/life_transitions.json") ||
                !await HasMaximumEnlightenmentAsync())
            {
                _fs.DeleteFile("game_state/control/ascension.json");
                return;
            }

            await UpdateSoulStateRealm("Shining Abode");
            await _storyService.AppendMarkerAsync("Shining Abode", _stateManager.CurrentState.Incarnation, "ASCENSION", "Душа достигла Сияющей Обители.");

            var shiningLorePath = "lore/shining_abode/realm_lore.json";
            if (!_fs.FileExists(shiningLorePath))
            {
                var defaultLore = new
                {
                    title = "Сияющая Обитель",
                    description = "Обитель вознесённых над Морем Хаоса. Место покоя, свободного ролеплея и встреч с Хранителями после завершения великого цикла."
                };
                await _fs.WriteFileAtomicAsync(shiningLorePath, JsonSerializer.Serialize(defaultLore, JsonOpts));
            }

            _fs.DeleteFile("game_state/control/ascension.json");
            GameInterface.RenderAscensionTransition();
            await RefreshCanonicalStateAsync();
        }
        catch (Exception ex)
        {
            LogError(ex);
            _fs.DeleteFile("game_state/control/ascension.json");
        }
    }

    private static bool TryReadLifeTransitionPayload(JsonElement root, out string reason, out string summary)
    {
        reason = "";
        summary = "";

        var payload = root;
        if (root.TryGetProperty("TriggerLifeEnd", out var nested) && nested.ValueKind == JsonValueKind.Object)
            payload = nested;

        if (!payload.TryGetProperty("reason", out var reasonEl) || reasonEl.ValueKind != JsonValueKind.String)
            return false;
        if (!payload.TryGetProperty("summary", out var summaryEl) || summaryEl.ValueKind != JsonValueKind.String)
            return false;

        reason = reasonEl.GetString() ?? "";
        summary = summaryEl.GetString() ?? "";
        if (!string.Equals(reason, "Death", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(summary);
    }

    /// <summary>
     /// Logs an error to game_session/error_log.txt for diagnostics.
     /// </summary>
    private void LogError(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
            var entry = $"[{DateTime.UtcNow:O}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logPath, entry, System.Text.Encoding.UTF8);
        }
        catch { }
    }

    private async Task<bool> HasMaximumEnlightenmentAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("soulProgression", out var progression) &&
                progression.ValueKind == JsonValueKind.Object)
            {
                if (progression.TryGetProperty("progressPercent", out var progressPercent) &&
                    progressPercent.ValueKind == JsonValueKind.Number &&
                    progressPercent.TryGetDouble(out var parsedPercent) &&
                    parsedPercent >= 100)
                {
                    return true;
                }

                if (progression.TryGetProperty("tier", out var tier) &&
                    tier.ValueKind == JsonValueKind.Number &&
                    tier.TryGetInt32(out var parsedTier) &&
                    parsedTier >= 4)
                {
                    return true;
                }

                if (progression.TryGetProperty("tierName", out var tierNameProp) &&
                    tierNameProp.ValueKind == JsonValueKind.String &&
                    IsTranscendenceTierName(tierNameProp.GetString()))
                {
                    return true;
                }
            }

            if (root.TryGetProperty("enlightenment", out var enlightenment))
            {
                if (enlightenment.ValueKind == JsonValueKind.Object)
                {
                    if (enlightenment.TryGetProperty("currentTier", out var currentTierProp) &&
                        currentTierProp.ValueKind == JsonValueKind.String &&
                        IsTranscendenceTierName(currentTierProp.GetString()))
                    {
                        return true;
                    }

                    if (enlightenment.TryGetProperty("level", out var levelProp) &&
                        levelProp.ValueKind == JsonValueKind.Number &&
                        levelProp.TryGetInt32(out var parsedLevel) &&
                        parsedLevel >= 4)
                    {
                        return true;
                    }

                    if (enlightenment.TryGetProperty("progressPercent", out var progressPercent) &&
                        progressPercent.ValueKind == JsonValueKind.Number &&
                        progressPercent.TryGetDouble(out var parsedPercent) &&
                        parsedPercent >= 100)
                    {
                        return true;
                    }
                }
                else if (enlightenment.ValueKind == JsonValueKind.Number &&
                         enlightenment.TryGetDouble(out var numericEnlightenment) &&
                         numericEnlightenment >= 100)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsTranscendenceTierName(string? tierName)
    {
        return string.Equals(tierName, "Transcendence", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tierName, "Трансценденция", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetPlayerInput()
    {
        AnsiConsole.WriteLine();

        // Show realm-aware prompt
        var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
        var isShiningAbode = _stateManager.CurrentState.IsInShiningAbode;
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;
        var accentColor = isShiningAbode ? "yellow" : (isAfterlife ? "blue" : "green3");
        var realmLabel = isShiningAbode ? _loc.T("realm_shining_abode") : (isAfterlife ? _loc.T("realm_chaos_sea") : _loc.T("realm_mortal"));
        AnsiConsole.Write(new Rule($"[bold {accentColor}]✦ Ваш ход ✦[/]").RuleStyle(accentColor));

        if (isShiningAbode)
        {
            AnsiConsole.MarkupLine("[dim]  Свободный ролеплей с Хранителями в Сияющей Обители[/]");
            AnsiConsole.MarkupLine("[dim]  /реликвии /хранители /душа │ /новая_игра+ │ /help[/]");
        }
        else if (isChaosSea)
        {
            AnsiConsole.MarkupLine("[dim]  Говорите с Хранителем: торговать, квесты, реликвии души, вытягивание реликвий, сменить хранителя[/]");
            AnsiConsole.MarkupLine("[dim]  /реликвии /хранители /гача /душа │ /воплотиться │ /help[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]  /инв /квесты /карта /статус │ /конец_жизни │ /help[/]");
        }

        // Show option hint if dialogue options are available
        if (_lastResponse?.DialogueOptions != null && _lastResponse.DialogueOptions.Length > 0)
            AnsiConsole.MarkupLine("[dim]  Введите [cyan]номер[/] опции или свой текст. Большую вставку можно вставить прямо сюда; [cyan]\\m[/] открывает текстовый редактор, [cyan]\\p[/] остаётся fallback-вставкой[/]");
        else
            AnsiConsole.MarkupLine("[dim]  Enter = отправить │ большую вставку можно вставить прямо сюда │ \\m = текстовый редактор │ \\p = fallback-вставка[/]");

        // Single-line mode by default: Enter sends immediately
        var promptChar = isChaosSea ? "🌊" : "⚔️";
        var firstLine = TextComposer.Read(
            StandardTextComposerConsole.Instance,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = $"[bold {accentColor}] {promptChar} > [/]",
                PreserveNewlines = true
            });

        if (IsClipboardPasteShortcut(firstLine))
            return ResolveClipboardPlayerInput();

        // Check for slash commands — always single-line, send immediately
        if (!firstLine.Contains('\n') && firstLine.TrimStart().StartsWith('/'))
            return firstLine.Trim();

        // Check for multiline trigger (Ctrl+M marker)
        if (firstLine.Equals("\\m", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Equals("/multiline", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Equals("/мульти", StringComparison.OrdinalIgnoreCase))
        {
            return await GetMultilineInput();
        }

        // Check for dialogue option number shortcuts
        if (!firstLine.Contains('\n') && int.TryParse(firstLine.Trim(), out var optNum))
        {
            // Try to resolve to actual dialogue option text
            if (_lastResponse?.DialogueOptions != null && optNum >= 1 && optNum <= _lastResponse.DialogueOptions.Length)
            {
                var optionText = _lastResponse.DialogueOptions[optNum - 1].Text;
                if (!string.IsNullOrEmpty(optionText))
                    return optionText;
            }
            // If no matching option, send as-is
            return firstLine.Trim();
        }

        // Regular single-line input — send directly on Enter
        return firstLine.Trim();
    }

    /// <summary>
    /// Multiline input mode: type multiple lines, empty line sends.
    /// Activated by typing \m or /multiline.
    /// </summary>
    private Task<string> GetMultilineInput()
    {
        var value = TextComposer.Read(
            StandardTextComposerConsole.Instance,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = "[cyan]│[/]",
                PreserveNewlines = true,
                Mode = TextComposerMode.MultilineEditor,
                HelpMarkup = "[dim](Многострочный режим. Вставка из буфера работает напрямую. Две пустые строки подряд = отправить. \\p = fallback из буфера.)[/]"
            });

        return Task.FromResult(value);
    }

    private static bool IsClipboardPasteShortcut(string input)
    {
        var trimmed = input.Trim();
        return trimmed.Equals("\\p", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("/paste", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("/вставить", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveClipboardPlayerInput()
    {
        var result = _clipboardService.TryReadText();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            AnsiConsole.MarkupLine($"[yellow]{GameInterface.EscapeMarkup(result.Error ?? "Не удалось прочитать буфер обмена.")}[/]");
            return string.Empty;
        }

        return result.Text!;
    }

    // ═══════════════════════════════════════════════
    // LOAD GAME
    // ═══════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════
    // OPTIONS
    // ═══════════════════════════════════════════════

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
                lastWidth = currentWidth;
                lastHeight = currentHeight;
            }

            var key = Console.ReadKey(true);
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
                    Console.ReadKey(true);
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

                Console.ReadKey(true);
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

                Console.ReadKey(true);
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
            Console.ReadKey(true);
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
            var key = Console.ReadKey(true);
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
            var key = Console.ReadKey(true);
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
            Console.ReadKey(true);
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
        Console.ReadKey(true);
    }

    /// <summary>
    /// Creates backup copies of critical game state files before sending a turn.
    /// Returns a dictionary of original→backup file paths for rollback.
    /// </summary>
    private IEnumerable<string> EnumerateRollbackTrackedFiles()
    {
        var gameSessionRoot = _fs.ResolvePath("");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var absoluteFile in _fs.GetAllGameStateFiles())
        {
            var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
            if (string.Equals(relative, ValidationRepairReadyPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, ValidationRepairRequestPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, "game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith($"{PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files.Add(relative);
        }

        foreach (var relativeDir in new[] { "lore" })
        {
            var absoluteDir = _fs.ResolvePath(relativeDir);
            if (!Directory.Exists(absoluteDir))
                continue;

            foreach (var absoluteFile in Directory.GetFiles(absoluteDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
                if (relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(relative);
            }
        }

        foreach (var outputFile in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            QteSceneService.QteOfferPath
        })
        {
            if (_fs.FileExists(outputFile))
                files.Add(outputFile);
        }

        return files;
    }

    private async Task<RollbackSnapshot> CreatePreTurnBackup(string backupId)
    {
        var snapshot = new RollbackSnapshot
        {
            BaselineFiles = new HashSet<string>(EnumerateRollbackTrackedFiles(), StringComparer.OrdinalIgnoreCase)
        };

        foreach (var file in snapshot.BaselineFiles)
        {
            if (_fs.FileExists(file))
            {
                var backupPath = file + $".rollback.{backupId}";
                try
                {
                    var content = await _fs.ReadFileAsync(file);
                    if (content != null)
                    {
                        await _fs.WriteFileAtomicAsync(backupPath, content);
                        snapshot.BackupFiles[file] = backupPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Не удалось создать backup для {File}", file);
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Restores game state files from pre-turn backups (escape-rollback).
    /// </summary>
    private async Task RestorePreTurnBackup(RollbackSnapshot snapshot)
    {
        foreach (var trackedFile in EnumerateRollbackTrackedFiles())
        {
            if (snapshot.BaselineFiles.Contains(trackedFile))
                continue;

            try
            {
                if (_fs.FileExists(trackedFile))
                    _fs.DeleteFile(trackedFile);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось удалить новый файл {File} при rollback", trackedFile);
            }
        }

        foreach (var (original, backup) in snapshot.BackupFiles)
        {
            try
            {
                var content = await _fs.ReadFileAsync(backup);
                if (content != null)
                    await _fs.WriteFileAtomicAsync(original, content);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось восстановить {File}", original);
            }
        }

        await RefreshCanonicalStateAsync();
    }

    /// <summary>
    /// Cleans up temporary rollback backup files.
    /// </summary>
    private void CleanupBackup(RollbackSnapshot snapshot)
    {
        foreach (var backup in snapshot.BackupFiles.Values)
        {
            try { _fs.DeleteFile(backup); }
            catch { }
        }
    }

    private static int[] GenerateSecureDice() => GameLoop.GenerateSecureRandomDice();

    /// <summary>
    /// Generates a fresh GM-facing dice pool and a separate client-computed gacha base.
    /// </summary>
    private static void AttachFreshDiceAndGacha(TurnRequest request)
    {
        var visibleDice = GenerateSecureDice();
        var hiddenGachaDice = GameLoop.GenerateSecureRandomDice(4);
        request.PreGeneratedDices1d20 = visibleDice;
        request.GachaBaseResult = GameLoop.ComputeGachaBase(hiddenGachaDice);
    }

    /// <summary>
    /// Attaches the persistent pending fate state to the next ordinary player turn.
    /// </summary>
    private async Task AttachPendingDiceAndGachaAsync(TurnRequest request)
    {
        var pending = await _pendingTurnState.GetOrCreateAsync();
        request.PreGeneratedDices1d20 = pending.PreGeneratedDices1d20;
        request.GachaBaseResult = pending.GachaBaseResult;
    }

    /// <summary>
    /// Builds a system reminder for the GM, reinforcing game-specific rules.
    /// </summary>
    private string BuildSystemReminder()
    {
        return @"CRITICAL SYSTEM REMINDER — This is NOT a D&D system!

CHARACTERISTICS: Range 1-100 (not 3-18). Base stats start at 1 per characteristic. Players distribute points (8 at incarnation + 5 per level). Do NOT use D&D-style modifiers like (stat-10)/2.

ACTION CHECK FORMULA (Block 12):
  StatModificator = CappedStatValue + LevelScaling
  where CappedStatValue = min(CharacterLevel*0.5+20, StatValueWithBonuses)
  and LevelScaling = floor(CharacterLevel * 0.8)
  Difference = (PlayerDiceResult + StatModificator) - (GMDice + ActionDifficultModificator)

The 'computedCharacteristics' field in this request contains the client-computed standard, permanently modified, and fully modified values for each characteristic. USE THESE VALUES for action checks — do not recalculate from scratch.

statsIncreased: The client automatically applies +1 to base stats with Training Cap enforcement (stat < PlayerLevel*2). You do NOT need to use setCharacteristics for training increases.

setCharacteristics: Use ONLY for extraordinary events (divine intervention, meta-commands). It bypasses the Training Cap.

REALM SEGREGATION — ABSOLUTE LAW:
Read Context.worldState.currentRealm (projected from game_state/meta/soul_state.json.currentRealm) BEFORE applying any mechanic.

IF REALM = Chaos Sea OR Shining Abode:
  FORBIDDEN: experienceGained, statsIncreased, statsDecreased, currentPoiseChange, currentEnergyChange, currentHealthChange, moneyChange, activeSkillChanges, passiveSkillChanges, skillMasteryChanges, UpdateInventory, UpdateNPCs, NPCsInScene, UpdateQuests, worldEventsLog, factionDataChanges, currentLocationData, timeChange, setWorldTime, weatherChange, enemiesData, alliesData, combat_log_markdown.
  ALLOWED: UpdateGuardians, Soul Relic systems, Ink Feather spending, Gacha, Abode/Guardian interactions, Life Evaluation, Incarnation setup.
  CHAOS-SEA INK FEATHER EXCEPTIONS: Donate to Guardian, Cultivate Enlightenment, Guardian Favor, Memory Gates, Soul Imprint.
  Sell Relic is a separate guardian trade interaction, not an Ink Feather action.
  Shining Abode is the ascended endgame free-roleplay zone above the Chaos Sea. It still uses afterlife/guardian systems, not Mortal World systems.
  If the player chooses New Game+ from Shining Abode, the new cycle returns to Chaos Sea with Enlightenment and Ink Feathers reset while Soul Relics and Guardians are preserved.
  LIFE EVALUATION REWARD GUARANTEE:
    - Every completed mortal life MUST grant at least 10 Ink Feathers.
    - Every completed mortal life MUST grant at least one NEW Soul Relic with a new relicId.
    - Reward quality may vary by achievements, but zero-reward life evaluation is a protocol violation.

IF REALM = Mortal World:
  FORBIDDEN: UpdateGuardians, Guardian-specific reputation/project/musings/lore commands, Abode navigation, Soul Relic Gacha, Chaos-Sea-only spending of Ink Feathers.
  ALLOWED: combat, NPCs, quests, inventory, factions, weather, time, world progression.
  MORTAL-WORLD INK FEATHER EXCEPTIONS: Reveal Fate, Rewrite Fate, Sacrifice to Chaos, Absorb Feathers, Learn Skill, Fate Shield, Seal in Ink.
  LOCAL NPC TRADE: Some NPCs may have a client-side Buy/Sell panel for mortal-world goods only. This panel does NOT create turn_request.json, does NOT use Ink Feathers, and does NOT trade Soul Relics.
  If the player later asks a merchant NPC about an item just bought from that merchant's local stock, treat the item as known to that merchant and do not act surprised by its existence.
  QTE OFFERS: game_state/core/game_settings.json.qteEventsEnabled controls whether QTE is allowed.
    - If qteEventsEnabled = false, DO NOT write output/qte_offer.json.
    - QTE is a rare cinematic tool, not a replacement for normal action checks.
    - QTE is allowed only in Mortal World and only on an ordinary player-driven turn (not incarnation, life evaluation, repair, transition, or other system flow).
    - QTE offer turn MUST NOT also resolve ordinary state changes for the same situation; leave game_state/lore/stories untouched and write only output/qte_offer.json plus narrative/interface/debug outputs.
    - QTE offer is delivered through output/qte_offer.json and then resolved locally by the client after player Accept/Decline.
    - qte_offer.json MUST define startChapterId; chapter array order does not define the scene start.
    - QTE primaryCharacteristic MUST use canonical lowercase stat ids (strength, dexterity, constitution, intelligence, wisdom, faith, attractiveness, trade, persuasion, perception, luck, speed).
    - For BranchChoice, check.config.choiceGrade MUST be exactly success, partial, or fail.
    - Every terminal outcome MUST carry a complete responseFragment for local application.
    - declineHint and cinematicJustification, if provided, are shown to the player in the offer prompt; keep them concise.
    - responseFragment MUST NOT use ordinary image_prompt; use sceneImagePrompt / chapterImagePrompt / outcomeImagePrompt instead.
    - Successful QTE terminal outcomes MUST grant positive experienceGained at minimum; the client will locally add it to the authoritative XP counter in experience.json.
    - If experience.json already contains level/progress metadata (level or playerLevel, experience or currentExperience, experienceForNextLevel), the client will also process the local level-up transition.

The Mortal-World and Chaos-Sea Ink Feather whitelists are mutually exclusive.

LORE / META BOOTSTRAP — HARD REQUIREMENT:
  - On the first Chaos Sea turn of a new game, create:
    lore/chaos_sea/cosmology.json
    lore/chaos_sea/soul_system_lore.json
    lore/chaos_sea/guardians_lore.json
    lore/codex_entries.json
    game_state/meta/achievements.json
  - On every new Mortal World incarnation, create:
    lore/current_world/world_setting.json
    lore/current_world/geography.json
    lore/current_world/history.json
    lore/current_world/cultures.json
    lore/current_world/threats.json
  - Optional supplemental Mortal-World lore: lore/current_world/npcs_lore.json when this life needs persistent NPC backstory/world-lore support.
  - Missing bootstrap lore/codex/achievement files will cause client validation failure.

QUEST UPDATE PROTOCOL — HARD REQUIREMENT:
  - On quest creation, send the full quest object with detailsLog.
  - On quest-log updates, send questId + newDetailsLogEntry instead of resending the whole detailsLog array.
  - quest_history.json is canonically stored as questHistory + questRewards + questChains; legacy questLog is only shorthand input.

PROGRESSION CONTROL — CLIENT-AUTHORITATIVE SCHEDULER:
This request contains a 'progressionControl' object. Treat it as authoritative system control, not optional advice.
  - In Mortal World, it defines the baseline world time and mandatory 240-minute world cycles / 1440-minute faction cycles.
  - In Chaos Sea or Shining Abode, it defines the mandatory hub / guardian-project-cycle processing for this turn.
  - If a mustEvaluate* flag is true, that contour MUST be processed this turn.
  - If a mustEvaluate* flag is false, there is no mandatory progression debt for that contour this turn.
You MUST evaluate and process all required cycles for the active realm.
If progression is processed, you MUST write progressionProcessingReport to game_state/control/progression_report.json and report the exact processed cycle counts and new last-* markers.
If no cycles are due, you may write zero counts or omit the report.

If a forbidden key appears in your draft response for the active realm, REMOVE it before finalizing.

NPC AGENCY — HARD REQUIREMENT:
You MUST declare NPC reasoning scope BEFORE narration instead of silently skipping or guessing it.
Your gm_thoughts_markdown MUST contain:
## Охват NPC-анализа
- Режим / Mode: [Scene-local | World-progression | Guardian-centric | Mixed]
- Релевантные акторы / Relevant actors: [...]
- Почему они релевантны / Why they are relevant: ...
- Акторы вне охвата / Actors outside scope: [...]
- Почему они вне охвата / Why they are outside scope: ...
Scene-local MAY use `Relevant actors: нет` only when the turn truly has no actor that must reason or react with agency.
Then, for every declared relevant actor, you MUST provide a reasoning block:
### [Actor Name]
- Текущая локация / Current location
- Ситуация / Current situation
- Мысли / Internal thoughts
- Действия / Intended actions
For EVERY relevant NPC block, the current-location line is mandatory: explicitly state where the NPC is now and whether they stay there or relocate this turn.
Missing scope declaration or missing/empty actor reasoning blocks will cause client rejection.
If you narrate a meaningful NPC reaction or introduce a new named NPC, you MUST also register/update the relevant NPC state. Narrative-only NPCs without state consequences are protocol violations.
If you emit structured actor updates such as UpdateNPCs, NPCGoalUpdates, NPCActivityUpdates, or UpdateGuardians, those actors MUST appear in Relevant actors and MUST have full reasoning blocks. Scene-local with `Relevant actors: нет` is valid only when no structured actor updates are emitted.

GUARDIAN AGENCY — HARD REQUIREMENT:
In Chaos Sea, use the same declared-scope model for relevant Guardians.
For Guardian-centric turns, the active Guardian MUST appear in the declared relevant actors and MUST have a full reasoning block before narration if activeGuardian is explicitly set in state.
Do NOT skip Guardian reasoning just because the player is the current conversational focus.

GUARDIAN-FORCED INCARNATION — HARD REQUIREMENT:
If game_state/control/afterlife_return_guard.json exists with remainingProtectedTurns > 0, the soul has just returned from a mortal life and MUST receive at least one ordinary afterlife turn before any Guardian-forced incarnation.
Do NOT immediately kick the soul back into a new life on that protected return turn.
Guardian-forced incarnation is legal only on an ordinary player-driven Chaos Sea turn as a response to explicit player provocation against the current active Guardian.
If you write game_state/control/incarnation_trigger.json in this forced mode, include:
  - source = guardian_forced
  - guardianId
  - severityBand = harsh | severe
  - reason
  - provocationSummary
  - worldDescription, characterDescription, circumstances
The resulting start must be harsh but survivable. Do NOT create an unwinnable deathtrap.

SOUL IDENTITY CONTINUITY:
If game_state/meta/soul_state.json contains previousSoulNames, they are former names of the SAME soul.
Do NOT treat a renamed soul as a different person and do NOT reset Guardian continuity because of a soul rename.

SOUL RELIC GACHA — ANTI-CHEAT PROTOCOL:
The 'preGeneratedDices1d20' field is the authoritative dice pool for your normal checks. Start from the FIRST die in that list.
The 'gachaBaseResult' field is a SEPARATE client-computed gacha outcome. Do NOT assume any dice were consumed from preGeneratedDices1d20 to produce it.
Its thresholds remain: 4-48=Common, 49-67=Uncommon, 68-75=Rare, 76-79=Epic, 80=Legendary.
If playerAction contains [CHAOS_SEA_DIRECT_GACHA], this is a DIRECT pull from the Chaos Sea, not a Guardian-mediated pull.
  - Do NOT apply Guardian reputation bonuses, penalties, discounts, jealousy/social effects, or other Guardian modifiers.
  - Treat gachaBaseResult.baseRarity as the neutral final rarity baseline with NO extra modifiers.
  - Add the relic directly to soul state via metaStateUpdates.soulRelicOperations.addRelic.
If the pull is Guardian-mediated, the 'baseRarity' from gachaBaseResult is the MINIMUM rarity. You may ONLY upgrade it using documented modifiers:
  - Guardian reputation bonus (Block 32): Friendly(50-129) +15%, Devoted(130-229) +30%, Legendary(230-300) +50% better rates
  - Hard Mode (Block 0.5): +1 tier upgrade at 50% chance
  - Impossible Mode (Block 0.6): +1 tier guaranteed, +1 more at 25% chance
Guardian-mediated pulls are LIMITED per Guardian per return from mortal life:
  - Hostile(-100..-51): blocked
  - Wary/Neutral(-50..49): 1 attempt
  - Friendly(50..129): 2 attempts
  - Devoted/Legendary(130..300): 3 attempts
  - If chargesUsedThisReturn already equals chargesPerReturn for that Guardian, DO NOT emit processGacha for them.
Direct /gacha remains neutral and does NOT consume Guardian charges.
You MUST NOT downgrade or ignore the client-computed baseRarity. Log the full calculation in gm_thoughts_markdown.

" + _storyService.BuildStoryContext();
    }

    private async Task<string> BuildTurnSystemReminderAsync(string? extraReminder = null)
    {
        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        var parts = new List<string> { BuildSystemReminder() };
        var modReminder = await _systemModService.BuildSystemReminderFragmentAsync();
        if (!string.IsNullOrWhiteSpace(modReminder))
            parts.Add(modReminder);
        var worldReminder = _worldDirectiveService.BuildReminderFragment(
            _stateManager.CurrentState.CurrentRealm,
            await _worldDirectiveService.ReadPendingSetupAsync(),
            await _worldDirectiveService.ReadActiveWorldDirectivesAsync());
        if (!string.IsNullOrWhiteSpace(worldReminder))
            parts.Add(worldReminder);
        var afterlifeGuardReminder = await _afterlifeReturnGuardService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(afterlifeGuardReminder))
            parts.Add(afterlifeGuardReminder);
        var qteReminder = await _qteSceneService.ConsumePendingReminderAsync();
        if (!string.IsNullOrWhiteSpace(qteReminder))
            parts.Add($"QTE SUMMARY FROM PREVIOUS LOCAL SCENE: {qteReminder}");
        if (!string.IsNullOrWhiteSpace(extraReminder))
            parts.Add(extraReminder);

        return string.Join("\n\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static bool IsIncarnationSourceLabel(string? sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceLabel))
            return false;

        return sourceLabel.Contains("воплощ", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ConsumeAfterlifeReturnProtectionIfNeededAsync(PendingTurnSnapshotManifest? manifest)
    {
        if (!string.Equals(manifest?.SourceLabel, OrdinaryPlayerTurnSourceLabel, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
            return;

        await _afterlifeReturnGuardService.ConsumeAfterAcceptedAfterlifeTurnAsync(_gameLoop.TurnNumber);
    }

    /// <summary>
     /// Interactive stat distribution UI. Shows all 12 characteristics and lets the player
    /// allocate available points. Used at incarnation (8 pts) and level-up (5 pts).
    /// </summary>
    private async Task ShowStatDistribution(string title)
    {
        var available = await _charService.GetUnspentStatPoints();
        if (available <= 0) return;

        var baseStats = new Dictionary<string, int>();
        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var name in Characteristics.All)
                {
                    if (doc.RootElement.TryGetProperty(name, out var val) &&
                        val.ValueKind == JsonValueKind.Number)
                        baseStats[name] = val.GetInt32();
                    else
                        baseStats[name] = 1;
                }
            }
            catch { foreach (var n in Characteristics.All) baseStats[n] = 1; }
        }
        else
        {
            foreach (var n in Characteristics.All) baseStats[n] = 1;
        }

        var allocations = new Dictionary<string, int>();
        foreach (var n in Characteristics.All) allocations[n] = 0;
        var remaining = available;
        var statList = Characteristics.All;
        var selectedIdx = 0;

        while (remaining > 0)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[gold1]⭐ {title}[/]").RuleStyle("gold1"));
            AnsiConsole.MarkupLine($"\n  [bold yellow]Доступно очков: {remaining}[/]  [dim](↑↓ выбрать, → добавить, ← убрать, Enter подтвердить)[/]\n");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Gold1)
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(3))
                .AddColumn(new TableColumn("[bold]Характеристика[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Текущая[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]+ Очки[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]= Итог[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]Шкала[/]").NoWrap());

            for (int i = 0; i < statList.Length; i++)
            {
                var name = statList[i];
                var ruName = Characteristics.RussianNames[name];
                var baseVal = baseStats[name];
                var alloc = allocations[name];
                var total = baseVal + alloc;
                var cursor = i == selectedIdx ? "[bold cyan]►[/]" : " ";

                int filled = Math.Clamp(total / 5, 0, 20);
                int empty = 20 - filled;
                var barColor = total switch { >= 80 => "gold1", >= 50 => "green", >= 25 => "yellow", _ => "grey" };
                var bar = $"[{barColor}]{new string('█', filled)}[/][dim]{new string('░', empty)}[/]";

                var allocStr = alloc > 0 ? $"[green]+{alloc}[/]" : "[dim]—[/]";
                var totalColor = alloc > 0 ? "green" : "white";
                var nameColor = i == selectedIdx ? "cyan bold" : "white";

                table.AddRow(cursor, $"[{nameColor}]{ruName}[/]",
                    $"{baseVal}", allocStr, $"[{totalColor}]{total}[/]", bar);
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(remaining > 0
                ? $"\n  [dim]Осталось распределить: [yellow]{remaining}[/] очков[/]"
                : "\n  [green]✅ Все очки распределены![/]");

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIdx = (selectedIdx - 1 + statList.Length) % statList.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIdx = (selectedIdx + 1) % statList.Length;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.OemPlus:
                case ConsoleKey.Add:
                    if (remaining > 0 && baseStats[statList[selectedIdx]] + allocations[statList[selectedIdx]] < 100)
                    {
                        allocations[statList[selectedIdx]]++;
                        remaining--;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.OemMinus:
                case ConsoleKey.Subtract:
                    if (allocations[statList[selectedIdx]] > 0)
                    {
                        allocations[statList[selectedIdx]]--;
                        remaining++;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (remaining == 0)
                        goto done;
                    // If some points remain, ask for confirmation
                    if (AnsiConsole.Confirm($"[yellow]У вас ещё {remaining} нераспределённых очков. Подтвердить?[/]", false))
                        goto done;
                    break;
            }
        }

        done:
        // Apply allocations
        var toApply = allocations.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (toApply.Count > 0)
        {
            await _charService.DistributePointsAsync(toApply);
            AnsiConsole.MarkupLine("[green]✅ Очки характеристик распределены![/]");
        }
        else
        {
            // Save remaining points for later
            await _charService.AddStatPoints(0); // no-op if nothing to add, just ensures file exists
        }

        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
        Console.ReadKey(true);
    }
}

