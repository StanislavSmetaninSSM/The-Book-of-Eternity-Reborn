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

internal enum SessionFinalizationCheckpoint
{
    TerminalWaitStarted,
    TerminalSignalInspectionLeaseAcquired,
    TerminalSignalSnapshotCapturedBeforeResolution,
    LateTerminalAndIdleOperationBound,
    IncarnationOperationBound,
    AcceptedOutcomeValidatedBeforeMaterialization,
    LifeEvaluationRequestDispatchedBeforeWait,
    RawAcceptedOutcomeValidatedBeforeLifeEvaluationFinalWrites
}

internal sealed class GameEngineSessionFinalizationHooks
{
    internal Func<SessionFinalizationCheckpoint, Task>? AtCheckpointAsync { get; init; }
}

internal sealed class GameEngineSnapshotPublicationHooks
{
    internal Func<string, Task>? AfterSnapshotFileCapturedAsync { get; init; }
}

/// <summary>
/// Main game orchestrator. Coordinates all subsystems:
/// Menu → Game Loop → UI → State Management → Save/Load
/// </summary>
public partial class GameEngine
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
    private readonly ActorMemoryService _actorMemoryService;
    private readonly AudioService _audioService;
    private readonly ConsoleAppearanceService _consoleAppearance;
    private readonly SystemModService _systemModService;
    private readonly SystemGuardianLibraryService _systemGuardianLibraryService;
    private readonly CriticalStateHealthService _criticalStateHealth;
    private readonly WorldDirectiveService _worldDirectiveService;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly AfterlifeArchiveCandidateService _afterlifeArchiveCandidateService;
    private readonly AfterlifeReturnGuardService _afterlifeReturnGuardService;
    private readonly RivalSoulArcService _rivalSoulArcService;
    private readonly GuardianCorrectionService _guardianCorrectionService;
    private readonly PendingTurnStateService _pendingTurnState;
    private readonly QteSceneService _qteSceneService;
    private readonly IClipboardService _clipboardService;
    private readonly IConsoleInputSource _inputSource;
    private readonly ITextComposerConsole _textComposerConsole;
    private readonly ILogger<GameEngine> _logger;
    private GameEngineSessionFinalizationHooks? _sessionFinalizationHooks;
    private GameEngineSnapshotPublicationHooks? _snapshotPublicationHooks;

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
    private const string ValidationDiagnosticFailureReportPath = "game_state/control/validation_diagnostic_failure_report.json";
    private const string ValidationRepairArtifactStallReportPath = "game_state/control/gm_validation_repair_artifact_stall_report.json";
    private const string TerminalProtocolFailureRequestPath = "game_state/control/terminal_protocol_failure_request.json";
    private const string OrdinaryPlayerTurnSourceLabel = "обработки хода";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
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
        StoryService storyService, ActorMemoryService actorMemoryService, AudioService audioService, ConsoleAppearanceService consoleAppearance,
        SystemModService systemModService,
        SystemGuardianLibraryService systemGuardianLibraryService,
        CriticalStateHealthService criticalStateHealth,
        WorldDirectiveService worldDirectiveService,
        ScenarioCoreService scenarioCoreService,
        AfterlifeArchiveCandidateService afterlifeArchiveCandidateService,
        AfterlifeReturnGuardService afterlifeReturnGuardService,
        RivalSoulArcService rivalSoulArcService,
        GuardianCorrectionService guardianCorrectionService,
        PendingTurnStateService pendingTurnState,
        QteSceneService qteSceneService,
        IClipboardService clipboardService,
        ILogger<GameEngine> logger,
        IConsoleInputSource? inputSource = null)
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
        _actorMemoryService = actorMemoryService;
        _audioService = audioService;
        _consoleAppearance = consoleAppearance;
        _systemModService = systemModService;
        _systemGuardianLibraryService = systemGuardianLibraryService;
        _criticalStateHealth = criticalStateHealth;
        _worldDirectiveService = worldDirectiveService;
        _scenarioCoreService = scenarioCoreService;
        _afterlifeArchiveCandidateService = afterlifeArchiveCandidateService;
        _afterlifeReturnGuardService = afterlifeReturnGuardService;
        _rivalSoulArcService = rivalSoulArcService;
        _guardianCorrectionService = guardianCorrectionService;
        _pendingTurnState = pendingTurnState;
        _qteSceneService = qteSceneService;
        _clipboardService = clipboardService;
        _inputSource = inputSource ?? SystemConsoleInputSource.Instance;
        _textComposerConsole = new StandardTextComposerConsole(_inputSource);
        _logger = logger;
    }

    internal void ConfigureSessionFinalizationHooksForTesting(
        GameEngineSessionFinalizationHooks? hooks)
    {
        _sessionFinalizationHooks = hooks;
    }

    internal void ConfigureSnapshotPublicationHooksForTesting(
        GameEngineSnapshotPublicationHooks? hooks)
    {
        _snapshotPublicationHooks = hooks;
    }

    private Task InvokeSessionFinalizationCheckpointAsync(
        SessionFinalizationCheckpoint checkpoint)
    {
        return _sessionFinalizationHooks?.AtCheckpointAsync?.Invoke(checkpoint)
               ?? Task.CompletedTask;
    }

    private Task InvokeSnapshotFileCapturedAsync(string relativePath) =>
        _snapshotPublicationHooks?.AfterSnapshotFileCapturedAsync?.Invoke(relativePath)
        ?? Task.CompletedTask;

    public async Task RunAsync()
    {
        _isRunning = true;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
            Console.InputEncoding = System.Text.Encoding.Unicode;
        else
            Console.InputEncoding = System.Text.Encoding.UTF8;

        _fs.EnsureDirectoryStructure();

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
            catch (Exception ex) when (_inputSource is ConsoleE2EScriptedInputSource scriptedInput)
            {
                scriptedInput.WriteExceptionObservation(
                    "Console E2E failure",
                    "The scripted console E2E run failed before the next player-visible screen could be reached.",
                    ex,
                    "error");
                throw;
            }
            catch (Exception ex)
            {
                LogError(ex);
                AnsiConsole.MarkupLine($"[red]❌ Ошибка: {GameInterface.EscapeMarkup(ex.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Ошибка сохранена в game_session/error_log.txt. Игра продолжает работу.[/]");
                AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
                _inputSource.ReadKey(intercept: true);
            }
        }

        await _audioService.StopAllAsync();
    }
}
