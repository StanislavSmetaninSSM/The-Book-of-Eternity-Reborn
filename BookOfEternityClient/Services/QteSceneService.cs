using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Globalization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Services;

internal sealed class QteSceneServiceHooks
{
    internal Func<Task>? BeforeRuntimeWriteAsync { get; init; }
    internal Func<QteSceneService.QteRuntimeState, Task>? AfterRuntimeWrittenAsync { get; init; }
    internal Func<Task>? AfterHistoryWrittenAsync { get; init; }
    internal Func<Task>? BeforeDarenProfileWriteAsync { get; init; }
    internal Func<Task>? AfterDarenProfileWrittenAsync { get; init; }
    internal Func<Task>? BeforeQteCharacteristicReadAsync { get; init; }
}

public sealed partial class QteSceneService
{
    public const string QteOfferPath = "output/qte_offer.json";
    public const string QteRuntimePath = "game_state/control/qte_runtime.json";
    public const string QteHistoryPath = "game_state/history/qte_history.json";
    public const string OrdinaryPlayerTurnSourceLabel = "обработки хода";
    internal const string QteNormalizerBackupDirectory = "game_state/control/qte_normalizer_backups";
    internal const int MashInputMinDurationMs = 750;
    internal const int MashInputMaxDurationMs = 10000;
    internal const int MashInputMinTargetPresses = 1;
    internal const int MashInputMaxTargetPresses = 80;
    internal const int MashInputMaxPressesPerSecond = 12;
    internal const int PatternMemoryMinSequenceLength = 2;
    internal const int PatternMemoryMaxSequenceLength = 12;
    internal const int PatternMemoryMinRevealMs = 500;
    internal const int PatternMemoryMaxRevealMs = 15000;
    internal const int PatternMemoryMinInputTimeoutMs = 1000;
    internal const int PatternMemoryMaxInputTimeoutMs = 30000;
    internal const int PatternMemoryMinInputMsPerSymbol = 300;
    private const int BalanceMeterMovementStep = 10;
    internal const int RhythmPulseMinPulseCount = 2;
    internal const int RhythmPulseMaxPulseCount = 16;
    internal const int RhythmPulseMinBeatIntervalMs = 300;
    internal const int RhythmPulseMaxBeatIntervalMs = 3000;
    internal const int RhythmPulseMinHitWindowMs = 40;
    internal const int RhythmPulseMaxHitWindowMs = 1000;
    internal const int PrecisionChoiceMinChoices = 2;
    internal const int PrecisionChoiceMaxChoices = 8;
    internal const int PrecisionChoiceMinTimeoutMs = 1000;
    internal const int PrecisionChoiceMaxTimeoutMs = 30000;
    internal const int StealthNoiseMinDurationMs = 1000;
    internal const int StealthNoiseMaxDurationMs = 30000;
    internal const int StealthNoiseMinMeterValue = 0;
    internal const int StealthNoiseMaxMeterValue = 100;
    internal const int StealthNoiseMinDangerThreshold = 1;
    internal const int StealthNoiseMinPositiveValue = 1;
    internal const int LockPinSetMinPinCount = 2;
    internal const int LockPinSetMaxPinCount = 8;
    internal const int LockPinSetMinPosition = 0;
    internal const int LockPinSetMaxPosition = 100;
    internal const int LockPinSetMinTimerMs = 1000;
    internal const int LockPinSetMaxTimerMs = 60000;
    internal const int LockPinSetMinPickDurability = 1;
    internal const int LockPinSetMaxPickDurability = 20;
    internal const int LockPinSetMinPinDriftPerSecond = 0;
    internal const int LockPinSetMaxPinDriftPerSecond = 100;
    internal static readonly IReadOnlyCollection<string> BrowserTransactionRollbackPaths =
        CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(FileMapping.FieldToFile.Values)
            .Concat(FileMapping.OutputFiles.Values)
            .Concat(
            [
                QteOfferPath,
                QteRuntimePath,
                QteHistoryPath,
                "game_state/player/experience.json",
                "ready/turn_complete.json",
                "ready/turn_error.json"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    internal static readonly IReadOnlyList<string> RhythmPulsePatternVariations =
    [
        "steady",
        "accelerating",
        "swing"
    ];
    private const int ExperienceBaseXp = 100;
    private const double ExperienceExponent = 2.5;

    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly CharacteristicsService _charService;
    private readonly ImageService _imageService;
    private readonly AudioService _audioService;
    private readonly StateDistributor _stateDistributor;
    private readonly ValidationService _validator;
    private readonly CanonicalStateNormalizer _normalizer;
    private readonly StateManager _stateManager;
    private readonly IConsoleInputSource _inputSource;
    private readonly ILogger<QteSceneService> _logger;
    private readonly QteSceneServiceHooks? _hooks;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private sealed class QteNormalizationBaseline
    {
        public Dictionary<string, string?> RestoreBackupsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> NormalizerBackupsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal interface IQteMiniGameLiveRenderer
    {
        void Update(string body);
        void Update(string title, string instructions, string body);
        void Update(AgentConsoleQteFrame qteFrame, string terminalBody) =>
            Update(qteFrame.Title, qteFrame.Instructions, terminalBody);
    }

    internal interface IQteLiveClock
    {
        DateTime UtcNow { get; }
        Task DelayAsync(int milliseconds);
    }

    private sealed class SystemQteLiveClock : IQteLiveClock
    {
        public static readonly SystemQteLiveClock Instance = new();

        public DateTime UtcNow => DateTime.UtcNow;

        public Task DelayAsync(int milliseconds) => Task.Delay(milliseconds);
    }

    public QteSceneService(
        FileSystemManager fs,
        GameSettings settings,
        CharacteristicsService charService,
        ImageService imageService,
        AudioService audioService,
        StateDistributor stateDistributor,
        ValidationService validator,
        CanonicalStateNormalizer normalizer,
        StateManager stateManager,
        ILogger<QteSceneService> logger,
        IConsoleInputSource? inputSource = null)
        : this(
            fs,
            settings,
            charService,
            imageService,
            audioService,
            stateDistributor,
            validator,
            normalizer,
            stateManager,
            logger,
            inputSource,
            hooks: null)
    {
    }

    internal QteSceneService(
        FileSystemManager fs,
        GameSettings settings,
        CharacteristicsService charService,
        ImageService imageService,
        AudioService audioService,
        StateDistributor stateDistributor,
        ValidationService validator,
        CanonicalStateNormalizer normalizer,
        StateManager stateManager,
        ILogger<QteSceneService> logger,
        IConsoleInputSource? inputSource,
        QteSceneServiceHooks? hooks)
    {
        _fs = fs;
        _settings = settings;
        _charService = charService;
        _imageService = imageService;
        _audioService = audioService;
        _stateDistributor = stateDistributor;
        _validator = validator;
        _normalizer = normalizer;
        _stateManager = stateManager;
        _inputSource = inputSource ?? SystemConsoleInputSource.Instance;
        _logger = logger;
        _hooks = hooks;
    }

    public Task<QteOffer?> TryReadOfferAsync() =>
        TryReadOfferCoreAsync(writeLease: null);

    internal Task<QteOffer?> TryReadOfferAsync(
        FileSystemManager.CanonicalWriteLease writeLease) =>
        TryReadOfferCoreAsync(writeLease);

    private async Task<QteOffer?> TryReadOfferCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var json = await ReadCanonicalFileAsync(writeLease, QteOfferPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<QteOffer>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось разобрать qte_offer.json");
            return null;
        }
    }

    public void ClearOfferFile() =>
        ClearOfferFileCore(writeLease: null);

    internal void ClearOfferFile(FileSystemManager.CanonicalWriteLease writeLease) =>
        ClearOfferFileCore(writeLease);

    private void ClearOfferFileCore(FileSystemManager.CanonicalWriteLease? writeLease)
    {
        if (CanonicalFileExists(writeLease, QteOfferPath))
            DeleteCanonicalFile(writeLease, QteOfferPath);
    }

    public static bool IsEligibleOfferSourceLabel(string? sourceLabel) =>
        string.Equals(sourceLabel, OrdinaryPlayerTurnSourceLabel, StringComparison.OrdinalIgnoreCase);

    public async Task<QteOfferDecision> PromptOfferDecisionAsync(QteOffer offer)
    {
        var lines = new List<string>
        {
            $"[bold gold1]🎬 {Markup.Escape(offer.Title ?? "QTE событие")}[/]",
            ""
        };

        if (!string.IsNullOrWhiteSpace(offer.OfferText))
            lines.Add($"[white]{Markup.Escape(offer.OfferText)}[/]");
        if (!string.IsNullOrWhiteSpace(offer.IntroNarrative))
        {
            lines.Add("");
            lines.Add($"[dim]{Markup.Escape(offer.IntroNarrative)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(offer.CinematicJustification))
        {
            lines.Add("");
            lines.Add($"[italic]Почему QTE:[/] [dim]{Markup.Escape(offer.CinematicJustification)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(offer.DeclineHint))
        {
            lines.Add("");
            lines.Add($"[grey]{Markup.Escape(offer.DeclineHint)}[/]");
        }
        if (!string.IsNullOrWhiteSpace(offer.SceneImagePrompt))
        {
            lines.Add("");
            lines.Add("[dim]Изображение сцены будет доступно после завершения QTE.[/]");
        }

        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" QTE Offer ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var offerPlainText = BuildQteOfferPlainText(offer);
        var choice = PromptQteSelection(
            "qte-offer-decision",
            offer.Title ?? "QTE событие",
            offerPlainText,
            ["✅ Принять", "❌ Отклонить"],
            "[bold]Действие:[/]",
            "Действие:",
            Color.Gold1,
            new AgentConsoleQteFrame
            {
                QteId = offer.QteId,
                Type = "OfferDecision",
                Title = offer.Title ?? "QTE событие",
                Phase = "choice",
                Instructions = "Выберите, принимать ли QTE событие.",
                BodyText = offerPlainText,
                AwaitingInputKind = AgentConsoleInputKind.MenuSelection,
                Choices = ["✅ Принять", "❌ Отклонить"]
            });

        return choice.Contains("Принять", StringComparison.OrdinalIgnoreCase)
            ? QteOfferDecision.Accept
            : QteOfferDecision.Decline;
    }

    public Task RecordDeclineAsync(QteOffer offer, int sourceTurnNumber) =>
        RecordDeclineCoreAsync(writeLease: null, offer, sourceTurnNumber);

    internal Task RecordDeclineAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        QteOffer offer,
        int sourceTurnNumber) =>
        RecordDeclineCoreAsync(
            writeLease,
            offer,
            sourceTurnNumber);

    private async Task RecordDeclineCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteOffer offer,
        int sourceTurnNumber)
    {
        var state = await LoadRuntimeStateAsync(writeLease);
        state.PendingOffer = null;
        state.ActiveScene = null;
        state.LastDeclinedQteId = offer.QteId;
        state.LastDeclinedAtTurn = sourceTurnNumber;
        await SaveRuntimeStateAsync(writeLease, state);
        ClearOfferFileCore(writeLease);
    }

    public async Task ClearDeclineMarkerAsync()
    {
        var state = await LoadRuntimeStateAsync();
        if (string.IsNullOrWhiteSpace(state.LastDeclinedQteId) && !state.LastDeclinedAtTurn.HasValue)
            return;

        state.LastDeclinedQteId = null;
        state.LastDeclinedAtTurn = null;
        await SaveRuntimeStateAsync(state);
    }

    public async Task<string?> ConsumePendingReminderAsync()
    {
        var state = await LoadRuntimeStateAsync();
        var reminder = state.LastResolvedQteSummaryPendingReminder;
        if (string.IsNullOrWhiteSpace(reminder))
            return null;

        state.LastResolvedQteSummaryPendingReminder = null;
        await SaveRuntimeStateAsync(state);
        return reminder;
    }

    public Task EnsureRuntimeStateHealthyAsync() =>
        EnsureRuntimeStateHealthyCoreAsync(writeLease: null);

    internal Task EnsureRuntimeStateHealthyAsync(
        FileSystemManager.CanonicalWriteLease writeLease) =>
        EnsureRuntimeStateHealthyCoreAsync(writeLease);

    private async Task EnsureRuntimeStateHealthyCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var json = await ReadCanonicalFileAsync(writeLease, QteRuntimePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Найден невалидный qte_runtime.json. Удаление как повреждённого client-owned runtime state.");
            DeleteCanonicalFile(writeLease, QteRuntimePath);
            return;
        }

        if (parsed is not JsonObject root)
        {
            _logger.LogWarning("Найден qte_runtime.json с не-object корнем. Удаление как повреждённого runtime state.");
            DeleteCanonicalFile(writeLease, QteRuntimePath);
            return;
        }

        var changed = false;

        if (root.TryGetPropertyValue("activeScene", out var activeSceneNode) && activeSceneNode is not null)
        {
            if (activeSceneNode is not JsonObject activeScene ||
                activeScene["offer"] is not JsonObject ||
                !TryReadNodeString(activeScene["currentChapterId"], out var currentChapterId) ||
                string.IsNullOrWhiteSpace(currentChapterId) ||
                activeScene["acceptedAtTurn"] is null ||
                !TryReadNodeInt(activeScene["acceptedAtTurn"], out _))
            {
                root.Remove("activeScene");
                root.Remove("pendingOffer");
                changed = true;
            }
        }

        if (root.TryGetPropertyValue("pendingOffer", out var pendingOfferNode) &&
            pendingOfferNode is not null &&
            pendingOfferNode is not JsonObject)
        {
            root.Remove("pendingOffer");
            changed = true;
        }

        if (root["activeScene"] is null && root["pendingOffer"] is JsonObject)
        {
            root.Remove("pendingOffer");
            changed = true;
        }

        if (root.TryGetPropertyValue("lastDeclinedQteId", out var declinedIdNode) &&
            declinedIdNode is not null &&
            !TryReadNodeString(declinedIdNode, out _))
        {
            root.Remove("lastDeclinedQteId");
            changed = true;
        }

        if (root.TryGetPropertyValue("lastDeclinedAtTurn", out var declinedTurnNode) &&
            declinedTurnNode is not null &&
            !TryReadNodeInt(declinedTurnNode, out _))
        {
            root.Remove("lastDeclinedAtTurn");
            changed = true;
        }

        if (root.TryGetPropertyValue("lastResolvedQteSummaryPendingReminder", out var reminderNode) &&
            reminderNode is not null &&
            !TryReadNodeString(reminderNode, out _))
        {
            root.Remove("lastResolvedQteSummaryPendingReminder");
            changed = true;
        }

        if (!changed)
            return;

        if (!HasMeaningfulRuntimeState(root))
        {
            _logger.LogInformation("qte_runtime.json очищен как пустой/повреждённый runtime state без полезных данных.");
            DeleteCanonicalFile(writeLease, QteRuntimePath);
            return;
        }

        _logger.LogInformation("qte_runtime.json был нормализован после обнаружения повреждённого/stale runtime state.");
        await WriteCanonicalFileAtomicAsync(writeLease, QteRuntimePath, root.ToJsonString(JsonOpts));
    }

    public async Task<QteSceneCompletion?> ResumeActiveSceneIfAnyAsync(int currentTurnNumber)
    {
        var state = await LoadRuntimeStateAsync();
        if (state.ActiveScene?.Offer == null || string.IsNullOrWhiteSpace(state.ActiveScene.CurrentChapterId))
            return null;

        AnsiConsole.MarkupLine("[yellow]⚠ Обнаружена незавершённая QTE-сцена. Продолжение...[/]");
        await Task.Delay(800);
        return await ExecuteActiveSceneAsync(state, currentTurnNumber);
    }

    public async Task<QteSceneCompletion> StartAcceptedSceneAsync(QteOffer offer, int currentTurnNumber)
    {
        var state = await BeginAcceptedSceneAsync(offer, currentTurnNumber);
        return await ExecuteActiveSceneAsync(state, currentTurnNumber);
    }

    public Task<QteRuntimeState> ReadRuntimeStateAsync() => LoadRuntimeStateAsync(writeLease: null);

    internal Task<QteRuntimeState> ReadRuntimeStateAsync(
        FileSystemManager.CanonicalWriteLease writeLease) =>
        LoadRuntimeStateAsync(writeLease);

    public Task<QteRuntimeState> BeginAcceptedSceneAsync(
        QteOffer offer,
        int currentTurnNumber) =>
        BeginAcceptedSceneCoreAsync(writeLease: null, offer, currentTurnNumber);

    internal Task<QteRuntimeState> BeginAcceptedSceneAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        QteOffer offer,
        int currentTurnNumber) =>
        BeginAcceptedSceneCoreAsync(
            writeLease,
            offer,
            currentTurnNumber);

    private async Task<QteRuntimeState> BeginAcceptedSceneCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteOffer offer,
        int currentTurnNumber)
    {
        var state = await LoadRuntimeStateAsync(writeLease);
        state.PendingOffer = offer;
        state.ActiveScene = new ActiveQteSceneState
        {
            Offer = offer,
            CurrentChapterId = !string.IsNullOrWhiteSpace(offer.StartChapterId)
                ? offer.StartChapterId
                : offer.Chapters.FirstOrDefault()?.ChapterId ?? "",
            AcceptedAtTurn = currentTurnNumber,
            ScoreState = BuildInitialScoreState(offer.ScoreModel)
        };
        await SaveRuntimeStateAsync(writeLease, state);
        ClearOfferFileCore(writeLease);
        return state;
    }

    public Task<QteActionResolution> ResolveActiveActionAsync(
        string actionId,
        string? submittedGrade,
        int currentTurnNumber,
        bool allowPreexistingStateIssues = false) =>
        ResolveActiveActionCoreAsync(
            writeLease: null,
            actionId,
            submittedGrade,
            currentTurnNumber,
            allowPreexistingStateIssues);

    internal Task<QteActionResolution> ResolveActiveActionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string actionId,
        string? submittedGrade,
        int currentTurnNumber,
        bool allowPreexistingStateIssues = false) =>
        ResolveActiveActionCoreAsync(
            writeLease,
            actionId,
            submittedGrade,
            currentTurnNumber,
            allowPreexistingStateIssues);

    private async Task<QteActionResolution> ResolveActiveActionCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string actionId,
        string? submittedGrade,
        int currentTurnNumber,
        bool allowPreexistingStateIssues)
    {
        var state = await LoadRuntimeStateAsync(writeLease);
        var active = state.ActiveScene ?? throw new InvalidOperationException("QTE scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("QTE offer is missing.");

        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
            throw new InvalidOperationException($"QTE chapter '{active.CurrentChapterId}' not found.");

        var action = chapter.Actions.FirstOrDefault(item =>
            string.Equals(item.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            throw new InvalidOperationException($"QTE action '{actionId}' not found.");

        var grade = ResolveBrowserSubmittedGrade(action, submittedGrade);
        var target = grade switch
        {
            QteGrade.Success => action.Routing.Success,
            QteGrade.Partial => action.Routing.Partial,
            _ => action.Routing.Fail
        };
        var resultText = ResolveResultText(action, grade);
        ApplyScoreDeltas(active.ScoreState, action, grade);

        if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
        {
            var outcome = offer.TerminalOutcomes.FirstOrDefault(item =>
                string.Equals(item.OutcomeId, target.TerminalOutcomeId, StringComparison.OrdinalIgnoreCase));
            if (outcome == null)
                throw new InvalidOperationException($"QTE outcome '{target.TerminalOutcomeId}' not found.");

            var finalResponse = await ApplyTerminalOutcomeValidatedStateChangesAsync(
                writeLease,
                outcome,
                allowPreexistingStateIssues);
            var scoreSummary = BuildFinalScoreSummary(offer.ScoreModel, active.ScoreState);
            var summary = BuildCompletionSummary(offer, outcome, grade, scoreSummary);
            await AppendHistoryAsync(
                writeLease,
                offer,
                outcome,
                grade,
                active.AcceptedAtTurn,
                currentTurnNumber,
                summary,
                scoreSummary,
                active.ScoreState?.Audit);

            state.PendingOffer = null;
            state.ActiveScene = null;
            state.LastResolvedQteSummaryPendingReminder = $"{summary}. GM summary: {outcome.GmSummary}";
            await SaveRuntimeStateAsync(writeLease, state);

            return new QteActionResolution
            {
                State = "Completed",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                Completion = new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = outcome.OutcomeId,
                    Summary = summary,
                    Response = finalResponse,
                    ScoreSummary = scoreSummary
                }
            };
        }

        if (string.IsNullOrWhiteSpace(target.NextChapterId))
            throw new InvalidOperationException($"QTE action '{action.ActionId}' has no nextChapterId or terminalOutcomeId.");

        active.CurrentChapterId = target.NextChapterId;
        await SaveRuntimeStateAsync(writeLease, state);

        return new QteActionResolution
        {
            State = "Active",
            QteId = offer.QteId,
            ChapterId = chapter.ChapterId,
            ActionId = action.ActionId,
            Grade = grade.ToString().ToLowerInvariant(),
            ResultText = resultText,
            NextChapterId = target.NextChapterId
        };
    }

    internal async Task<QteActionResolution> ResolveDarenShowcaseActionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        DarenShowcaseAttemptState attempt,
        string actionId,
        string? submittedGrade,
        DateTime? completedAtUtc = null)
    {
        EnsureCanonicalWriteLease(writeLease);
        if (!string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Daren showcase attempt is not active.");

        var active = attempt.ActiveScene ?? throw new InvalidOperationException("Daren showcase scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("Daren showcase route is missing.");
        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
            throw new InvalidOperationException($"Daren showcase chapter '{active.CurrentChapterId}' not found.");

        var action = chapter.Actions.FirstOrDefault(item =>
            string.Equals(item.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            throw new InvalidOperationException($"Daren showcase action '{actionId}' not found.");

        var grade = ResolveBrowserSubmittedGrade(action, submittedGrade);
        var target = grade switch
        {
            QteGrade.Success => action.Routing.Success,
            QteGrade.Partial => action.Routing.Partial,
            _ => action.Routing.Fail
        };
        var resultText = ResolveResultText(action, grade);
        ApplyScoreDeltas(active.ScoreState, action, grade);

        QteActionResolution resolution;
        if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
        {
            var normalizedScore = ResolveDarenNormalizedScore(active.ScoreState);
            var ending = DarenQteRewardProfileService.ResolveEnding(
                reachedHideout: true,
                normalizedScore);
            var scoreSummary = BuildDarenFinalScoreSummary(
                offer.ScoreModel,
                active.ScoreState,
                ending);
            var profileResult = await RecordDarenCompletionAsync(
                writeLease,
                ending,
                completedAtUtc ?? DateTime.UtcNow);
            var rewardMessage = ending.GrantsReward
                ? profileResult.Message
                : ending.RewardExplanation;
            var rewardProfileSummary = profileResult.RewardProfileSummary;
            var summary = BuildDarenCompletionSummary(
                ending,
                rewardMessage,
                rewardProfileSummary,
                scoreSummary);
            var response = BuildDarenCompletionResponse(
                ending,
                rewardProfileSummary);

            resolution = new QteActionResolution
            {
                State = "Completed",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                Completion = new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = ending.OutcomeId,
                    Summary = summary,
                    Response = new GameResponse { Response = response },
                    ScoreSummary = scoreSummary
                }
            };

            attempt.State = "Completed";
            attempt.LastCompletion = resolution.Completion;
            attempt.Ending = new DarenShowcaseEnding(
                ending.TierId,
                ending.DisplayName,
                ending.NormalizedScore,
                ending.InkFeatherBonus,
                ending.GrantsReward,
                ending.Epilogue,
                ending.RewardExplanation,
                rewardMessage,
                rewardProfileSummary);
            attempt.FeedbackTitle = ending.DisplayName;
            attempt.Feedback = response;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"Daren showcase action '{action.ActionId}' has no next chapter or terminal outcome.");

            active.CurrentChapterId = target.NextChapterId;
            resolution = new QteActionResolution
            {
                State = "Active",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                NextChapterId = target.NextChapterId
            };

            attempt.FeedbackTitle = "Следующий участок";
            attempt.Feedback = resultText;
        }

        attempt.LastResolution = resolution;
        return resolution;
    }

    internal async Task<DarenRewardProfileState> ReadDarenRewardProfileAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        EnsureCanonicalWriteLease(writeLease);
        var path = ResolveDarenProfilePath(_fs);
        if (!File.Exists(path))
            return new DarenRewardProfileState();

        try
        {
            var raw = await File.ReadAllTextAsync(path);
            return NormalizeDarenProfile(raw);
        }
        catch
        {
            return new DarenRewardProfileState();
        }
    }

    internal Action PrepareStateManagerRollback()
    {
        var snapshot = _stateManager.CaptureRuntimeSnapshot();
        return () => _stateManager.RestoreRuntimeSnapshot(snapshot);
    }

    private async Task<DarenRewardProfileWriteResult> RecordDarenCompletionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        DarenEndingResult ending,
        DateTime completedAtUtc)
    {
        var profile = await ReadDarenRewardProfileAsync(writeLease);
        if (!ending.GrantsReward || string.IsNullOrWhiteSpace(ending.TierId))
        {
            return new DarenRewardProfileWriteResult(
                false,
                profile,
                ending.RewardExplanation);
        }

        var tier = DarenQteRewardProfileService.EndingTiers.FirstOrDefault(item =>
            string.Equals(item.TierId, ending.TierId, StringComparison.OrdinalIgnoreCase));
        if (tier == null)
        {
            return new DarenRewardProfileWriteResult(
                false,
                profile,
                "Постоянная награда Дарена не записана: итог не распознан и будущая новая игра не получает Чернильных Перьев.");
        }

        var existing = profile.DarenShowcase;
        if (existing != null &&
            ResolveDarenTierRank(existing.BestTierId) >= ResolveDarenTierRank(tier.TierId))
        {
            return new DarenRewardProfileWriteResult(
                false,
                profile,
                $"Книга уже хранит постоянный итог Дарена: {existing.BestTierName}. Будущая новая игра пойдёт за лучшей тенью и не обменяет её на более слабый след; Чернильные Перья не складываются от повторной вылазки.",
                DarenQteRewardProfileService.BuildProfileSummary(existing, ending));
        }

        profile = new DarenRewardProfileState
        {
            SchemaVersion = DarenQteRewardProfileService.SchemaVersion,
            DarenShowcase = new DarenRewardRecord
            {
                BestTierId = tier.TierId,
                BestTierName = tier.DisplayName,
                InkFeatherBonus = tier.InkFeatherBonus,
                BestScore = ending.NormalizedScore,
                CompletedAtUtc = completedAtUtc.ToUniversalTime(),
                Source = DarenQteRewardProfileService.Source
            }
        };

        if (_hooks?.BeforeDarenProfileWriteAsync != null)
            await _hooks.BeforeDarenProfileWriteAsync();
        await WriteDarenProfileAsync(writeLease, profile);
        if (_hooks?.AfterDarenProfileWrittenAsync != null)
            await _hooks.AfterDarenProfileWrittenAsync();
        return new DarenRewardProfileWriteResult(
            true,
            profile,
            tier.RewardExplanation,
            DarenQteRewardProfileService.BuildProfileSummary(
                profile.DarenShowcase,
                ending));
    }

    private async Task WriteDarenProfileAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        DarenRewardProfileState profile)
    {
        EnsureCanonicalWriteLease(writeLease);
        var path = ResolveDarenProfilePath(_fs);
        var content = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(profile, JsonOpts));
        await Task.Run(() => WriteExternalFileAtomic(path, content));
    }

    private DarenRewardProfileState NormalizeDarenProfile(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new DarenRewardProfileState();

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return new DarenRewardProfileState();
        }

        if (root == null)
            return new DarenRewardProfileState();

        var candidates = new List<DarenRewardRecord>();
        if (root["darenShowcase"] is JsonObject single &&
            TryNormalizeDarenRecord(single, out var normalizedSingle))
        {
            candidates.Add(normalizedSingle);
        }

        if (root["darenShowcases"] is JsonArray legacy)
        {
            foreach (var item in legacy.OfType<JsonObject>())
            {
                if (TryNormalizeDarenRecord(item, out var normalizedLegacy))
                    candidates.Add(normalizedLegacy);
            }
        }

        return new DarenRewardProfileState
        {
            SchemaVersion = DarenQteRewardProfileService.SchemaVersion,
            DarenShowcase = candidates
                .OrderByDescending(item => ResolveDarenTierRank(item.BestTierId))
                .ThenByDescending(item => item.BestScore)
                .ThenByDescending(item => item.CompletedAtUtc)
                .FirstOrDefault()
        };
    }

    private static bool TryNormalizeDarenRecord(
        JsonObject source,
        out DarenRewardRecord record)
    {
        record = new DarenRewardRecord();
        var tierId = source["bestTierId"]?.GetValue<string>();
        var tier = DarenQteRewardProfileService.EndingTiers.FirstOrDefault(item =>
            string.Equals(item.TierId, tierId, StringComparison.OrdinalIgnoreCase));
        if (tier == null ||
            !TryReadNodeInt(source["bestScore"], out var score) ||
            score < tier.MinimumNormalizedScore)
        {
            return false;
        }

        var completedAt = DateTime.UtcNow;
        if (source["completedAtUtc"] is JsonValue completedNode &&
            completedNode.TryGetValue<DateTime>(out var parsedCompletedAt))
        {
            completedAt = parsedCompletedAt.ToUniversalTime();
        }

        record = new DarenRewardRecord
        {
            BestTierId = tier.TierId,
            BestTierName = tier.DisplayName,
            InkFeatherBonus = tier.InkFeatherBonus,
            BestScore = Math.Clamp(score, 0, 100),
            CompletedAtUtc = completedAt,
            Source = DarenQteRewardProfileService.Source
        };
        return true;
    }

    private static int ResolveDarenTierRank(string? tierId)
    {
        var tiers = DarenQteRewardProfileService.EndingTiers;
        for (var index = 0; index < tiers.Count; index++)
        {
            if (string.Equals(
                    tiers[index].TierId,
                    tierId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    internal static string ResolveDarenProfilePath(FileSystemManager fs) =>
        Path.Combine(
            fs.BasePath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));

    internal static byte[]? ReadDarenProfileRollbackBytes(FileSystemManager fs)
    {
        var path = ResolveDarenProfilePath(fs);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    internal static void RestoreDarenProfileRollbackBytes(
        FileSystemManager fs,
        byte[]? content)
    {
        var path = ResolveDarenProfilePath(fs);
        if (content == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        WriteExternalFileAtomic(path, content);
    }

    private static void WriteExternalFileAtomic(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllBytes(tempPath, content);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private void EnsureCanonicalWriteLease(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        if (!ReferenceEquals(writeLease.Owner, _fs) || !writeLease.IsActive)
            throw new InvalidOperationException("The QTE canonical write lease is not active for this game session.");
    }

    private async Task<QteSceneCompletion> ExecuteActiveSceneAsync(QteRuntimeState state, int currentTurnNumber)
    {
        var active = state.ActiveScene ?? throw new InvalidOperationException("QTE scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("QTE offer is missing.");

        while (true)
        {
            var chapter = offer.Chapters.FirstOrDefault(item =>
                string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            if (chapter == null)
                throw new InvalidOperationException($"QTE chapter '{active.CurrentChapterId}' not found.");

            ShowChapterPrelude(offer, chapter, active.ScoreState);

            var actionOptions = BuildUniqueOptions(chapter.Actions, action =>
                ConsoleLayout.PlainChoiceLabel($"⚡ {action.Label}", action.Check.Type, $"Сложность {Math.Clamp(action.Check.BaseDifficulty, 1, 5)}"));
            var actionLabels = actionOptions.Select(item => item.Label).ToArray();
            var selected = PromptQteSelection(
                $"qte-action-{ToAgentConsoleScreenPart(offer.QteId)}-{ToAgentConsoleScreenPart(chapter.ChapterId)}",
                chapter.Title ?? offer.Title ?? "QTE действие",
                BuildQteChapterPlainText(offer, chapter, active.ScoreState),
                actionLabels,
                "[bold]Выберите действие:[/]",
                "Выберите действие:",
                Color.Cyan1);
            var action = actionOptions.First(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;

            var grade = await RunCheckAsync(action);
            var target = grade switch
            {
                QteGrade.Success => action.Routing.Success,
                QteGrade.Partial => action.Routing.Partial,
                _ => action.Routing.Fail
            };
            ApplyScoreDeltas(active.ScoreState, action, grade);

            await ShowIntermediateResultAsync(offer, chapter, action, grade);

            if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
            {
                var outcome = offer.TerminalOutcomes.FirstOrDefault(item =>
                    string.Equals(item.OutcomeId, target.TerminalOutcomeId, StringComparison.OrdinalIgnoreCase));
                if (outcome == null)
                    throw new InvalidOperationException($"QTE outcome '{target.TerminalOutcomeId}' not found.");

                var scoreSummary = BuildFinalScoreSummary(offer.ScoreModel, active.ScoreState);
                var finalResponse = await ApplyTerminalOutcomeAsync(outcome, scoreSummary);
                var summary = BuildCompletionSummary(offer, outcome, grade, scoreSummary);
                await AppendHistoryAsync(offer, outcome, grade, active.AcceptedAtTurn, currentTurnNumber, summary, scoreSummary, active.ScoreState?.Audit);

                state.PendingOffer = null;
                state.ActiveScene = null;
                state.LastResolvedQteSummaryPendingReminder = $"{summary}. GM summary: {outcome.GmSummary}";
                await SaveRuntimeStateAsync(state);

                return new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = outcome.OutcomeId,
                    Summary = summary,
                    Response = finalResponse,
                    ScoreSummary = scoreSummary
                };
            }

            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"QTE action '{action.ActionId}' has no nextChapterId or terminalOutcomeId.");

            active.CurrentChapterId = target.NextChapterId;
            await SaveRuntimeStateAsync(state);
        }
    }

    private static QteScoreState? BuildInitialScoreState(QteScoreModel? scoreModel)
    {
        if (scoreModel?.Metrics is not { Count: > 0 })
            return null;

        return new QteScoreState
        {
            Metrics = scoreModel.Metrics.Select(metric => new QteScoreMetricState
            {
                Id = metric.Id,
                Label = metric.Label,
                Value = Clamp(metric.Initial, metric.Min, metric.Max),
                Min = metric.Min,
                Max = metric.Max,
                Visibility = string.IsNullOrWhiteSpace(metric.Visibility) ? "always" : metric.Visibility.Trim().ToLowerInvariant()
            }).ToList()
        };
    }

    private static void ApplyScoreDeltas(QteScoreState? scoreState, QteAction action, QteGrade grade)
    {
        if (scoreState == null || action.ScoreDeltas == null || action.ScoreDeltas.Count == 0)
            return;

        var gradeKey = GradeKey(grade);
        var deltas = action.ScoreDeltas.FirstOrDefault(pair =>
            string.Equals(pair.Key, gradeKey, StringComparison.OrdinalIgnoreCase)).Value;
        if (deltas == null || deltas.Count == 0)
            return;

        foreach (var delta in deltas)
        {
            var metric = scoreState.Metrics.FirstOrDefault(item =>
                string.Equals(item.Id, delta.Metric, StringComparison.OrdinalIgnoreCase));
            if (metric == null)
                continue;

            var previous = metric.Value;
            metric.Value = Clamp(metric.Value + delta.Delta, metric.Min, metric.Max);
            scoreState.Audit.Add(new QteScoreAuditEntry
            {
                ActionId = action.ActionId,
                ActionLabel = action.Label,
                Grade = gradeKey,
                Metric = metric.Id,
                MetricLabel = metric.Label,
                PreviousValue = previous,
                Delta = delta.Delta,
                NewValue = metric.Value
            });
        }
    }

    private static QteScoreSummary? BuildFinalScoreSummary(QteScoreModel? scoreModel, QteScoreState? scoreState)
    {
        if (scoreModel == null || scoreState == null)
            return null;

        var rank = SelectFinalRank(scoreModel, scoreState);
        return new QteScoreSummary
        {
            Rank = rank == null
                ? null
                : new QteScoreRankSummary
                {
                    Id = rank.Id,
                    Label = rank.Label,
                    Summary = rank.Summary
                },
            Metrics = scoreState.Metrics.Select(CloneScoreMetric).ToList()
        };
    }

    private static QteScoreRankDefinition? SelectFinalRank(QteScoreModel scoreModel, QteScoreState scoreState)
    {
        foreach (var rank in EnumerateRankEvaluationOrder(scoreModel).Where(rank => !rank.Fallback))
        {
            if (rank.AllOf.Count > 0 && rank.AllOf.All(threshold => MatchesThreshold(scoreState, threshold)))
                return rank;
        }

        return scoreModel.Ranks.FirstOrDefault(rank => rank.Fallback) ?? scoreModel.Ranks.FirstOrDefault();
    }

    private static IEnumerable<QteScoreRankDefinition> EnumerateRankEvaluationOrder(QteScoreModel scoreModel)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (scoreModel.RankOrder is { Count: > 0 })
        {
            foreach (var rankId in scoreModel.RankOrder)
            {
                var rank = scoreModel.Ranks.FirstOrDefault(item =>
                    string.Equals(item.Id, rankId, StringComparison.OrdinalIgnoreCase));
                if (rank != null && emitted.Add(rank.Id))
                    yield return rank;
            }
        }

        foreach (var rank in scoreModel.Ranks)
        {
            if (emitted.Add(rank.Id))
                yield return rank;
        }
    }

    private static bool MatchesThreshold(QteScoreState scoreState, QteScoreThreshold threshold)
    {
        var metric = scoreState.Metrics.FirstOrDefault(item =>
            string.Equals(item.Id, threshold.Metric, StringComparison.OrdinalIgnoreCase));
        if (metric == null)
            return false;

        return threshold.Op switch
        {
            ">=" => metric.Value >= threshold.Value,
            ">" => metric.Value > threshold.Value,
            "<=" => metric.Value <= threshold.Value,
            "<" => metric.Value < threshold.Value,
            "==" => Math.Abs(metric.Value - threshold.Value) < 0.000001d,
            _ => false
        };
    }

    private static string BuildCompletionSummary(
        QteOffer offer,
        QteTerminalOutcome outcome,
        QteGrade grade,
        QteScoreSummary? scoreSummary)
    {
        var summary = $"QTE[{offer.QteId}] -> {outcome.Title} ({DisplayGrade(grade)})";
        if (!string.IsNullOrWhiteSpace(scoreSummary?.Rank?.Label))
            summary += $". Ранг: {scoreSummary.Rank.Label}";
        return summary;
    }

    private static IEnumerable<QteScoreMetricState> GetVisibleActiveScoreMetrics(QteScoreState? scoreState) =>
        scoreState?.Metrics.Where(metric =>
            string.Equals(metric.Visibility, "always", StringComparison.OrdinalIgnoreCase)) ??
        Enumerable.Empty<QteScoreMetricState>();

    private static IEnumerable<QteScoreMetricState> GetVisibleFinalScoreMetrics(QteScoreSummary? scoreSummary) =>
        scoreSummary?.Metrics.Where(metric =>
            !string.Equals(metric.Visibility, "hidden", StringComparison.OrdinalIgnoreCase)) ??
        Enumerable.Empty<QteScoreMetricState>();

    private static List<string> BuildFinalScoreMarkupLines(QteScoreSummary? scoreSummary)
    {
        var lines = new List<string>();
        var rank = scoreSummary?.Rank;
        var metrics = GetVisibleFinalScoreMetrics(scoreSummary).ToList();
        if (rank == null && metrics.Count == 0)
            return lines;

        lines.Add("[bold]Итоговый счёт:[/]");
        if (!string.IsNullOrWhiteSpace(rank?.Label))
            lines.Add($"[green]Ранг: {Markup.Escape(rank.Label)}[/]");
        if (!string.IsNullOrWhiteSpace(rank?.Summary))
            lines.Add($"[grey]{Markup.Escape(rank.Summary!)}[/]");

        foreach (var metric in metrics)
            lines.Add($"[grey]• {Markup.Escape(metric.Label)}: {FormatScoreValue(metric.Value)}[/]");

        return lines;
    }

    private static string? BuildFinalScorePlainText(QteScoreSummary? scoreSummary)
    {
        var lines = new List<string>();
        var rank = scoreSummary?.Rank;
        var metrics = GetVisibleFinalScoreMetrics(scoreSummary).ToList();
        if (rank == null && metrics.Count == 0)
            return null;

        lines.Add("Итоговый счёт:");
        if (!string.IsNullOrWhiteSpace(rank?.Label))
            lines.Add($"Ранг: {rank.Label}");
        if (!string.IsNullOrWhiteSpace(rank?.Summary))
            lines.Add(rank.Summary!);

        foreach (var metric in metrics)
            lines.Add($"{metric.Label}: {FormatScoreValue(metric.Value)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendFinalScoreToResponse(GameResponse response, QteScoreSummary? scoreSummary)
    {
        var scoreText = BuildFinalScorePlainText(scoreSummary);
        if (string.IsNullOrWhiteSpace(scoreText))
            return;

        response.Response = string.IsNullOrWhiteSpace(response.Response)
            ? scoreText
            : $"{response.Response.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{scoreText}";
    }

    private static QteScoreMetricState CloneScoreMetric(QteScoreMetricState metric) =>
        new()
        {
            Id = metric.Id,
            Label = metric.Label,
            Value = metric.Value,
            Min = metric.Min,
            Max = metric.Max,
            Visibility = metric.Visibility
        };

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private static string GradeKey(QteGrade grade) => grade.ToString().ToLowerInvariant();

    private static string FormatScoreValue(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.000001d
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private void ShowChapterPrelude(QteOffer offer, QteChapter chapter, QteScoreState? scoreState)
    {
        var lines = new List<string>
        {
            $"[bold cyan]🎬 {Markup.Escape(chapter.Title ?? offer.Title ?? "QTE сцена")}[/]",
            ""
        };

        if (!string.IsNullOrWhiteSpace(chapter.Narrative))
            lines.Add($"[white]{Markup.Escape(chapter.Narrative)}[/]");

        var visibleMetrics = GetVisibleActiveScoreMetrics(scoreState).ToList();
        if (visibleMetrics.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Счёт сцены:[/]");
            foreach (var metric in visibleMetrics)
                lines.Add($"[grey]• {Markup.Escape(metric.Label)}: {FormatScoreValue(metric.Value)}[/]");
        }

        lines.Add("");
        lines.Add("[yellow]Нажмите любую клавишу, когда будете готовы продолжить...[/]");

        SpectreConsoleSafe.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForQteContinueKey(
            $"qte-chapter-{ToAgentConsoleScreenPart(offer.QteId)}-{ToAgentConsoleScreenPart(chapter.ChapterId)}",
            chapter.Title ?? offer.Title ?? "QTE сцена",
            BuildQteChapterPlainText(offer, chapter, scoreState) +
            Environment.NewLine +
            Environment.NewLine +
            "Нажмите любую клавишу, когда будете готовы продолжить...");
    }

    private async Task ShowIntermediateResultAsync(QteOffer offer, QteChapter chapter, QteAction action, QteGrade grade)
    {
        _audioService.PlayCue(grade == QteGrade.Fail ? AudioCue.QteFail : AudioCue.QteSuccess);
        var resultText = grade switch
        {
            QteGrade.Success => action.SuccessText ?? "Успешное выполнение.",
            QteGrade.Partial => action.PartialText ?? "Частичный успех.",
            _ => action.FailText ?? "Неудача."
        };
        var imagePrompt = chapter.ChapterImagePrompt ?? offer.SceneImagePrompt;
        var imageOffered = false;

        while (true)
        {
            var choices = new List<string>();
            if (!imageOffered && !string.IsNullOrWhiteSpace(imagePrompt))
                choices.Add(_imageService.GenerateWithoutDisplay
                    ? "🖼 Сгенерировать изображение"
                    : "🖼 Сгенерировать и показать изображение");
            choices.Add("➡ Перейти к следующей сцене");

            SpectreConsoleSafe.Clear();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", new[]
            {
                $"[bold yellow]Результат: {Markup.Escape(DisplayGrade(grade))}[/]",
                "",
                $"[white]{Markup.Escape(resultText)}[/]"
            })))
            {
                Header = new PanelHeader(" Промежуточный результат ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var choice = PromptQteSelection(
                $"qte-result-{ToAgentConsoleScreenPart(offer.QteId)}-{ToAgentConsoleScreenPart(chapter.ChapterId)}",
                "Промежуточный результат",
                $"Результат: {DisplayGrade(grade)}{Environment.NewLine}{Environment.NewLine}{resultText}",
                choices,
                "[bold]Действие:[/]",
                "Действие:",
                Color.Gold1);

            if (choice.StartsWith("🖼", StringComparison.Ordinal))
            {
                imageOffered = await ShowSceneImageAsync(imagePrompt, offer.QteId, chapter.ChapterId);
                continue;
            }

            return;
        }
    }

    private async Task<GameResponse> ApplyTerminalOutcomeAsync(
        QteTerminalOutcome outcome,
        QteScoreSummary? scoreSummary)
    {
        var response = BuildTerminalOutcomeResponse(outcome);
        AppendFinalScoreToResponse(response, scoreSummary);
        response = await ApplyTerminalOutcomeValidatedStateChangesAsync(
            writeLease: null,
            response);
        await ShowTerminalOutcomeScreenAsync(outcome, scoreSummary);
        return response;
    }

    internal async Task<GameResponse> ApplyTerminalOutcomeValidatedStateChangesAsync(
        QteTerminalOutcome outcome,
        bool allowPreexistingStateIssues = false)
    {
        var response = BuildTerminalOutcomeResponse(outcome);
        return await ApplyTerminalOutcomeValidatedStateChangesAsync(
            writeLease: null,
            response,
            allowPreexistingStateIssues);
    }

    private Task<GameResponse> ApplyTerminalOutcomeValidatedStateChangesAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteTerminalOutcome outcome,
        bool allowPreexistingStateIssues)
    {
        var response = BuildTerminalOutcomeResponse(outcome);
        return ApplyTerminalOutcomeValidatedStateChangesAsync(
            writeLease,
            response,
            allowPreexistingStateIssues);
    }

    private async Task<GameResponse> ApplyTerminalOutcomeValidatedStateChangesAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        GameResponse response,
        bool allowPreexistingStateIssues = false)
    {
        var baseline = await CaptureQteNormalizationBaselineAsync(writeLease, response);
        HashSet<string>? preexistingErrorFingerprints = null;
        if (allowPreexistingStateIssues)
        {
            await RefreshGameStateAsync(writeLease);
            preexistingErrorFingerprints = (await _validator.ValidateGameStateAsync())
                .Where(issue => issue.Severity == IssueSeverity.Error)
                .Select(BuildValidationIssueFingerprint)
                .ToHashSet(StringComparer.Ordinal);
        }

        try
        {
            await ApplyTerminalOutcomeStateChangesCoreAsync(
                writeLease,
                response,
                baseline.NormalizerBackupsByPath);
            await RefreshGameStateAsync(writeLease);

            var issues = await _validator.ValidateGameStateAsync();
            var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToList();
            if (preexistingErrorFingerprints is { Count: > 0 })
            {
                errors = errors
                    .Where(issue => !preexistingErrorFingerprints.Contains(BuildValidationIssueFingerprint(issue)))
                    .ToList();
            }

            if (errors.Count > 0)
            {
                var summary = string.Join("; ", errors.Take(5).Select(issue =>
                    string.IsNullOrWhiteSpace(issue.Code)
                        ? $"{issue.FilePath}: {issue.Message}"
                        : $"{issue.Code} at {issue.FilePath}"));
                throw new InvalidOperationException($"Локальный QTE outcome нарушил контракт состояния: {summary}");
            }

            return response;
        }
        catch
        {
            await RestoreQteNormalizationBaselineAsync(writeLease, baseline);
            await RefreshGameStateAsync(writeLease);
            throw;
        }
        finally
        {
            CleanupQteNormalizationBaseline(writeLease, baseline);
        }
    }

    private static string BuildValidationIssueFingerprint(ValidationIssue issue) =>
        string.Join('\u001f',
            issue.FilePath ?? string.Empty,
            issue.Code ?? string.Empty,
            issue.Section ?? string.Empty,
            issue.Message ?? string.Empty);

    internal async Task<GameResponse> ApplyTerminalOutcomeStateChangesAsync(QteTerminalOutcome outcome)
    {
        var response = BuildTerminalOutcomeResponse(outcome);
        var baseline = await CaptureQteNormalizationBaselineAsync(writeLease: null, response);
        try
        {
            await ApplyTerminalOutcomeStateChangesCoreAsync(
                writeLease: null,
                response,
                baseline.NormalizerBackupsByPath);
            return response;
        }
        catch
        {
            await RestoreQteNormalizationBaselineAsync(writeLease: null, baseline);
            throw;
        }
        finally
        {
            CleanupQteNormalizationBaseline(writeLease: null, baseline);
        }
    }

    private GameResponse BuildTerminalOutcomeResponse(QteTerminalOutcome outcome)
    {
        var response = outcome.ResponseFragment != null
            ? JsonSerializer.Deserialize<GameResponse>(outcome.ResponseFragment.ToJsonString(), JsonOpts)
            : new GameResponse();

        response ??= new GameResponse();
        if (string.IsNullOrWhiteSpace(response.Response))
            response.Response = outcome.FinalNarrative;
        response.ImagePrompt = null;
        return response;
    }

    private async Task ApplyTerminalOutcomeStateChangesCoreAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        GameResponse response,
        IReadOnlyDictionary<string, string> normalizerBackups)
    {
        if (writeLease == null)
        {
            await _stateDistributor.DistributeAsync(response);
        }
        else
        {
            await _stateDistributor.DistributeAsync(writeLease, response);
        }

        await ApplyAuthoritativeExperienceAsync(writeLease, response.ExperienceGained);
        var normalizer = writeLease == null ? _normalizer : _normalizer.BindTo(writeLease);
        await normalizer.NormalizeAccumulatedStateAsync(normalizerBackups);
    }

    private async Task<QteNormalizationBaseline> CaptureQteNormalizationBaselineAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        GameResponse response)
    {
        var baseline = new QteNormalizationBaseline();
        var runId = Guid.NewGuid().ToString("N");
        var fileIndex = 0;

        foreach (var relativePath in CollectQteTrackedPaths(response))
        {
            var content = await ReadCanonicalFileAsync(writeLease, relativePath);
            if (content == null)
            {
                baseline.RestoreBackupsByPath[relativePath] = null;
                continue;
            }

            var sanitizedPath = relativePath.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            var backupPath = $"{QteNormalizerBackupDirectory}/{runId}/{fileIndex:D2}_{sanitizedPath}";
            await WriteCanonicalFileAtomicAsync(writeLease, backupPath, content);
            baseline.RestoreBackupsByPath[relativePath] = backupPath;
            if (CanonicalStateNormalizer.NormalizerBackupInputFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
                baseline.NormalizerBackupsByPath[relativePath] = backupPath;
            fileIndex++;
        }

        return baseline;
    }

    private static HashSet<string> CollectQteTrackedPaths(GameResponse response)
    {
        var trackedPaths = new HashSet<string>(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles, StringComparer.OrdinalIgnoreCase);
        var responseElement = JsonSerializer.SerializeToElement(response, JsonOpts);
        if (responseElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in responseElement.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;

                if (FileMapping.FieldToFile.TryGetValue(property.Name, out var targetPath))
                    trackedPaths.Add(targetPath);
            }
        }

        if (response.Response != null &&
            FileMapping.OutputFiles.TryGetValue("narrative", out var narrativePath))
        {
            trackedPaths.Add(narrativePath);
        }

        if ((response.DialogueOptions != null || response.ImagePrompt != null) &&
            FileMapping.OutputFiles.TryGetValue("interface", out var interfacePath))
        {
            trackedPaths.Add(interfacePath);
        }

        if (response.GmThoughtsMarkdown != null &&
            FileMapping.OutputFiles.TryGetValue("debug", out var debugPath))
        {
            trackedPaths.Add(debugPath);
        }

        return trackedPaths;
    }

    private async Task RestoreQteNormalizationBaselineAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteNormalizationBaseline baseline)
    {
        foreach (var (relativePath, backupPath) in baseline.RestoreBackupsByPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                DeleteCanonicalFile(writeLease, relativePath);
                continue;
            }

            var content = await ReadCanonicalFileAsync(writeLease, backupPath);
            if (content == null)
            {
                DeleteCanonicalFile(writeLease, relativePath);
                continue;
            }

            await WriteCanonicalFileAtomicAsync(writeLease, relativePath, content);
        }
    }

    private void CleanupQteNormalizationBaseline(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteNormalizationBaseline? baseline)
    {
        if (baseline == null)
            return;

        var runDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var backupPath in baseline.RestoreBackupsByPath.Values)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                continue;

            var absoluteBackupPath = _fs.ResolvePath(backupPath);
            var runDirectory = Path.GetDirectoryName(absoluteBackupPath);
            if (!string.IsNullOrWhiteSpace(runDirectory))
                runDirectories.Add(runDirectory);

            DeleteCanonicalFile(writeLease, backupPath);
        }

        foreach (var runDirectory in runDirectories.OrderByDescending(path => path.Length))
            TryDeleteDirectoryIfEmpty(runDirectory);

        TryDeleteDirectoryIfEmpty(_fs.ResolvePath(QteNormalizerBackupDirectory));
    }

    private void TryDeleteDirectoryIfEmpty(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !Directory.Exists(absolutePath))
            return;

        try
        {
            if (Directory.EnumerateFileSystemEntries(absolutePath).Any())
                return;

            Directory.Delete(absolutePath, recursive: false);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Не удалось удалить пустой каталог временных backup-артефактов QTE: {Path}", absolutePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Нет доступа к удалению пустого каталога временных backup-артефактов QTE: {Path}", absolutePath);
        }
    }

    private async Task ShowTerminalOutcomeScreenAsync(QteTerminalOutcome outcome, QteScoreSummary? scoreSummary)
    {
        var imageOffered = false;
        var lines = new List<string>
        {
            $"[bold green]{Markup.Escape(outcome.Title)}[/]",
            "",
            $"[white]{Markup.Escape(outcome.FinalNarrative)}[/]"
        };
        var scoreLines = BuildFinalScoreMarkupLines(scoreSummary);
        if (scoreLines.Count > 0)
        {
            lines.Add("");
            lines.AddRange(scoreLines);
        }

        while (true)
        {
            var choices = new List<string>();
            if (!imageOffered && !string.IsNullOrWhiteSpace(outcome.OutcomeImagePrompt))
                choices.Add(_imageService.GenerateWithoutDisplay
                    ? "🖼 Сгенерировать изображение"
                    : "🖼 Сгенерировать и показать изображение");
            choices.Add("✅ Завершить сцену");

            SpectreConsoleSafe.Clear();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" Финал QTE ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var choice = PromptQteSelection(
                $"qte-final-{ToAgentConsoleScreenPart(outcome.OutcomeId)}",
                "Финал QTE",
                string.Join(Environment.NewLine, lines.Select(StripSpectreMarkup)),
                choices,
                "[bold]Действие:[/]",
                "Действие:",
                Color.Green);

            if (choice.StartsWith("🖼", StringComparison.Ordinal))
            {
                imageOffered = await ShowSceneImageAsync(outcome.OutcomeImagePrompt, "qte_outcome", outcome.OutcomeId);
                continue;
            }

            return;
        }
    }

    private async Task ApplyAuthoritativeExperienceAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        int? experienceDelta)
    {
        if (!experienceDelta.HasValue || experienceDelta.Value <= 0)
            return;

        const string experiencePath = "game_state/player/experience.json";
        var previousCounter = await ReadAuthoritativeExperienceCounterAsync(writeLease, experiencePath);
        var currentJson = await ReadCanonicalFileAsync(writeLease, experiencePath);

        JsonObject root;
        try
        {
            root = !string.IsNullOrWhiteSpace(currentJson)
                ? JsonNode.Parse(currentJson!) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        var updatedCounter = false;
        var hadTotalExperience = TryReadInt(root["totalExperience"], out var totalExperience);
        var hadCurrentExperience = TryReadInt(root["currentExperience"], out var currentExperience);
        var hadExperience = TryReadInt(root["experience"], out var experience);

        if (hadTotalExperience)
        {
            root["totalExperience"] = totalExperience + experienceDelta.Value;
            updatedCounter = true;
        }

        var progressHandled = TryApplyLevelProgression(root, experienceDelta.Value, hadCurrentExperience, currentExperience, hadExperience, experience);
        updatedCounter |= progressHandled;

        if (!hadTotalExperience)
        {
            if (TryCalculateTotalExperience(root, out var derivedTotalExperience))
            {
                root["totalExperience"] = derivedTotalExperience;
                updatedCounter = true;
            }
            else if (!hadCurrentExperience && !hadExperience)
            {
                root["totalExperience"] = experienceDelta.Value;
                updatedCounter = true;
            }
        }

        root["experienceGained"] = experienceDelta.Value;
        root["_lastUpdated"] = DateTime.UtcNow.ToString("o");

        await WriteCanonicalFileAtomicAsync(writeLease, experiencePath, root.ToJsonString(JsonOpts));

        var currentCounter = await ReadAuthoritativeExperienceCounterAsync(writeLease, experiencePath);
        if (!currentCounter.HasValue || currentCounter.Value <= (previousCounter ?? 0))
        {
            throw new InvalidOperationException(
                "QTE outcome не смог авторитетно увеличить XP counter в game_state/player/experience.json.");
        }
    }

    private static bool TryApplyLevelProgression(
        JsonObject root,
        int experienceDelta,
        bool hadCurrentExperience,
        int currentExperience,
        bool hadExperience,
        int experience)
    {
        var hasProgressField = hadCurrentExperience || hadExperience;
        if (!hasProgressField)
            return false;

        if (!TryReadInt(root["experienceForNextLevel"], out var experienceForNextLevel) ||
            experienceForNextLevel <= 0)
        {
            if (hadCurrentExperience)
                root["currentExperience"] = currentExperience + experienceDelta;
            if (hadExperience)
                root["experience"] = experience + experienceDelta;
            return true;
        }

        if (!TryReadLevel(root, out var currentLevel))
        {
            if (hadCurrentExperience)
                root["currentExperience"] = currentExperience + experienceDelta;
            if (hadExperience)
                root["experience"] = experience + experienceDelta;
            return true;
        }

        var progress = hadCurrentExperience ? currentExperience : experience;
        progress += experienceDelta;

        while (progress >= experienceForNextLevel)
        {
            progress -= experienceForNextLevel;
            currentLevel += 1;
            experienceForNextLevel = CalculateExperienceForNextLevel(currentLevel);
        }

        if (hadCurrentExperience)
            root["currentExperience"] = progress;
        if (hadExperience)
            root["experience"] = progress;

        if (root["level"] != null)
            root["level"] = currentLevel;
        if (root["playerLevel"] != null)
            root["playerLevel"] = currentLevel;
        root["experienceForNextLevel"] = experienceForNextLevel;

        return true;
    }

    private async Task<int?> ReadAuthoritativeExperienceCounterAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath)
    {
        var json = await ReadCanonicalFileAsync(writeLease, relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            if (root == null)
                return null;

            foreach (var propertyName in new[] { "totalExperience", "currentExperience", "experience" })
            {
                if (TryReadInt(root[propertyName], out var value))
                    return value;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryReadLevel(JsonObject root, out int level)
    {
        if (TryReadInt(root["level"], out level))
            return true;
        if (TryReadInt(root["playerLevel"], out level))
            return true;

        level = 0;
        return false;
    }

    private static int CalculateExperienceForNextLevel(int currentLevel)
    {
        var safeLevel = Math.Max(1, currentLevel);
        return (int)Math.Floor(ExperienceBaseXp * Math.Pow(safeLevel, ExperienceExponent));
    }

    private static bool TryCalculateTotalExperience(JsonObject root, out int totalExperience)
    {
        totalExperience = 0;
        if (!TryReadLevel(root, out var currentLevel))
            return false;

        if (!TryReadInt(root["currentExperience"], out var progress) &&
            !TryReadInt(root["experience"], out progress))
        {
            return false;
        }

        long total = Math.Max(0, progress);
        for (var level = 1; level < Math.Max(1, currentLevel); level++)
            total += CalculateExperienceForNextLevel(level);

        totalExperience = total > int.MaxValue ? int.MaxValue : (int)total;
        return true;
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node == null)
            return false;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out value))
                return true;

            if (jsonValue.TryGetValue<string>(out var text) &&
                int.TryParse(text, out value))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<QteGrade> RunCheckAsync(QteAction action)
    {
        _audioService.PlayCue(AudioCue.QteStart);
        return action.Check.Type switch
        {
            "BranchChoice" => ResolveBranchChoiceGrade(action),
            "TimingBar" => await RunTimingBarAsync(action.Check),
            "PromptChain" => await RunPromptChainAsync(action.Check),
            "BalanceMeter" => await RunBalanceMeterAsync(action.Check),
            "ChargeRelease" => await RunChargeReleaseAsync(action.Check),
            "MashInput" => await RunMashInputAsync(action.Check),
            "PatternMemory" => await RunPatternMemoryAsync(action),
            "RhythmPulse" => await RunRhythmPulseAsync(action.Check),
            "PrecisionChoice" => await RunPrecisionChoiceAsync(action.Check),
            "StealthNoise" => await RunStealthNoiseAsync(action.Check),
            "LockPinSet" => await RunLockPinSetAsync(action.Check),
            _ => QteGrade.Fail
        };
    }

    private static QteGrade ResolveBranchChoiceGrade(QteAction action) =>
        ParseGrade(GetConfigString(action.Check.Config, "choiceGrade"));

    private static QteGrade ResolveBrowserSubmittedGrade(QteAction action, string? submittedGrade)
    {
        if (string.Equals(action.Check.Type, "BranchChoice", StringComparison.OrdinalIgnoreCase))
            return ResolveBranchChoiceGrade(action);

        return ParseGrade(submittedGrade);
    }

    private static string ResolveResultText(QteAction action, QteGrade grade) => grade switch
    {
        QteGrade.Success => action.SuccessText ?? "Успешное выполнение.",
        QteGrade.Partial => action.PartialText ?? "Частичный успех.",
        _ => action.FailText ?? "Неудача."
    };

    private async Task<QteGrade> RunMashInputAsync(QteCheck check)
    {
        if (!TryReadMashInputConfig(
                check.Config,
                out var acceptedTokens,
                out var durationMs,
                out var targetPresses,
                out var partialThreshold))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var successTarget = ComputeMashInputEffectiveTargetPresses(targetPresses, check.BaseDifficulty, statTier);
        var partialTarget = ComputeMashInputPartialTargetPresses(successTarget, partialThreshold);
        var accepted = acceptedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keyLabels = FormatMashInputKeyLabels(acceptedTokens);
        var matchedPresses = 0;
        var started = DateTime.UtcNow;

        return await RunMiniGameLiveAsync(
            "Рывок на усилие",
            $"Быстро нажимайте {keyLabels}. Esc - безопасный отказ считается провалом.",
            BuildMashInputProgress(matchedPresses, successTarget, partialTarget, durationMs),
            async renderer =>
        {
            while (true)
            {
                var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                var remainingMs = Math.Max(0, durationMs - elapsedMs);
                if (remainingMs <= 0)
                    break;

                while (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return QteGrade.Fail;

                    var token = QteKeyInput.NormalizeConsoleInput(key);
                    if (token != null && accepted.Contains(token))
                    {
                        matchedPresses++;
                        if (matchedPresses >= successTarget)
                            return QteGrade.Success;
                    }
                }

                renderer.Update(BuildMashInputProgress(matchedPresses, successTarget, partialTarget, remainingMs));

                await Task.Delay(20);
            }

            return ParseGrade(ResolveMashInputGradeFromCount(matchedPresses, successTarget, partialTarget));
        });
    }

    internal static int ComputeMashInputEffectiveTargetPresses(int targetPresses, int baseDifficulty, int statTier)
    {
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var adjusted = targetPresses + difficultyOffset - statTier;
        return Math.Clamp(adjusted, MashInputMinTargetPresses, MashInputMaxTargetPresses);
    }

    internal static int ComputeMashInputPartialTargetPresses(int successTarget, double partialThreshold)
    {
        var clampedTarget = Math.Clamp(successTarget, MashInputMinTargetPresses, MashInputMaxTargetPresses);
        var partial = (int)Math.Ceiling(clampedTarget * partialThreshold);
        return Math.Clamp(partial, MashInputMinTargetPresses, clampedTarget);
    }

    internal static int ComputeMashInputMaxTargetPressesForDuration(int durationMs) =>
        Math.Max(MashInputMinTargetPresses, (int)Math.Floor(durationMs / 1000d * MashInputMaxPressesPerSecond));

    internal static string ResolveMashInputGrade(
        IReadOnlyCollection<string> acceptedTokens,
        int successTarget,
        int partialTarget,
        IEnumerable<ConsoleKeyInfo> inputs)
    {
        var accepted = acceptedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedPresses = 0;

        foreach (var input in inputs)
        {
            if (input.Key == ConsoleKey.Escape)
                return "fail";

            var token = QteKeyInput.NormalizeConsoleInput(input);
            if (token != null && accepted.Contains(token))
                matchedPresses++;
        }

        return ResolveMashInputGradeFromCount(matchedPresses, successTarget, partialTarget);
    }

    private static string ResolveMashInputGradeFromCount(int matchedPresses, int successTarget, int partialTarget)
    {
        if (matchedPresses >= successTarget)
            return "success";

        return matchedPresses >= partialTarget ? "partial" : "fail";
    }

    private static bool TryReadMashInputConfig(
        JsonObject? config,
        out IReadOnlyList<string> acceptedTokens,
        out int durationMs,
        out int targetPresses,
        out double partialThreshold)
    {
        acceptedTokens = [];
        durationMs = 0;
        targetPresses = 0;
        partialThreshold = 0;

        if (config == null ||
            config["keys"] is not JsonArray keys ||
            config["durationMs"] is not JsonValue durationNode ||
            config["targetPresses"] is not JsonValue targetNode ||
            config["partialThreshold"] is not JsonValue thresholdNode)
        {
            return false;
        }

        var tokens = new List<string>();
        foreach (var key in keys)
        {
            if (key is not JsonValue keyValue ||
                !keyValue.TryGetValue<string>(out var token) ||
                string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            tokens.Add(token.Trim().ToLowerInvariant());
        }

        if (tokens.Count == 0 ||
            !tokens.All(QteKeyInput.IsSupportedToken) ||
            !durationNode.TryGetValue<int>(out durationMs) ||
            !targetNode.TryGetValue<int>(out targetPresses) ||
            !thresholdNode.TryGetValue<double>(out partialThreshold))
        {
            return false;
        }

        acceptedTokens = tokens;
        return true;
    }

    private async Task<QteGrade> RunPatternMemoryAsync(QteAction action)
    {
        if (!TryReadPatternMemoryConfig(
                action.Check.Config,
                out var alphabet,
                out var sequenceLength,
                out var revealMs,
                out var inputTimeoutMs,
                out var allowedMistakes))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(action.Check.PrimaryCharacteristic);
        var effective = ComputePatternMemoryEffectiveRequirement(
            sequenceLength,
            revealMs,
            inputTimeoutMs,
            allowedMistakes,
            action.Check.BaseDifficulty,
            statTier);
        var sequence = GeneratePatternMemorySequence(
            alphabet,
            effective.SequenceLength,
            $"{action.ActionId}:{action.Check.BaseDifficulty}:{action.Check.PrimaryCharacteristic}:{string.Join(",", alphabet)}");

        return await RunMiniGameLiveAsync(
            "Память рун: фаза показа",
            "Запомните порядок знаков. Ввод начнётся после показа. Esc - безопасный отказ считается провалом.",
            BuildPatternMemoryRevealFrame(sequence, effective.RevealMs),
            async renderer =>
                ParseGrade(await RunPatternMemoryLiveLoopAsync(
                    sequence,
                    effective,
                    _inputSource,
                    renderer,
                    SystemQteLiveClock.Instance)));
    }

    internal static async Task<string> RunPatternMemoryLiveLoopAsync(
        IReadOnlyList<string> sequence,
        PatternMemoryEffectiveRequirement effective,
        IConsoleInputSource inputSource,
        IQteMiniGameLiveRenderer renderer,
        IQteLiveClock clock)
    {
        var revealStarted = clock.UtcNow;
        var inputs = new List<ConsoleKeyInfo>();

        while ((clock.UtcNow - revealStarted).TotalMilliseconds < effective.RevealMs)
        {
            while (TryReadImmediateKey(inputSource, out var revealKey))
            {
                if (revealKey.Key == ConsoleKey.Escape)
                    return "fail";
            }

            var remainingMs = Math.Max(0, effective.RevealMs - (int)(clock.UtcNow - revealStarted).TotalMilliseconds);
            renderer.Update(
                BuildPatternMemoryRevealAgentConsoleFrame(sequence, remainingMs, effective.RevealMs),
                BuildPatternMemoryRevealFrame(sequence, remainingMs));

            await clock.DelayAsync(20);
        }

        var inputStarted = clock.UtcNow;
        renderer.Update(
            BuildPatternMemoryInputAgentConsoleFrame(
                sequence.Count,
                inputs,
                effective.AllowedMistakes,
                effective.InputTimeoutMs,
                effective.InputTimeoutMs),
            BuildPatternMemoryInputLiveFrame(sequence.Count, inputs, effective.AllowedMistakes, effective.InputTimeoutMs));

        while ((clock.UtcNow - inputStarted).TotalMilliseconds < effective.InputTimeoutMs)
        {
            while (TryReadImmediateKey(inputSource, out var inputKey))
            {
                if (inputKey.Key == ConsoleKey.Escape)
                    return "fail";

                inputs.Add(inputKey);
                if (inputs.Count >= sequence.Count)
                {
                    return ResolvePatternMemoryGrade(
                        sequence,
                        effective.AllowedMistakes,
                        inputs);
                }
            }

            var remainingMs = Math.Max(0, effective.InputTimeoutMs - (int)(clock.UtcNow - inputStarted).TotalMilliseconds);
            renderer.Update(
                BuildPatternMemoryInputAgentConsoleFrame(
                    sequence.Count,
                    inputs,
                    effective.AllowedMistakes,
                    remainingMs,
                    effective.InputTimeoutMs),
                BuildPatternMemoryInputLiveFrame(sequence.Count, inputs, effective.AllowedMistakes, remainingMs));

            await clock.DelayAsync(20);
        }

        return ResolvePatternMemoryGrade(
            sequence,
            effective.AllowedMistakes,
            inputs,
            timedOut: true);
    }

    internal sealed record PatternMemoryEffectiveRequirement(
        int SequenceLength,
        int RevealMs,
        int InputTimeoutMs,
        int AllowedMistakes);

    internal static PatternMemoryEffectiveRequirement ComputePatternMemoryEffectiveRequirement(
        int sequenceLength,
        int revealMs,
        int inputTimeoutMs,
        int allowedMistakes,
        int baseDifficulty,
        int statTier)
    {
        var baseSequenceLength = Math.Clamp(
            sequenceLength,
            PatternMemoryMinSequenceLength,
            PatternMemoryMaxSequenceLength);
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var difficultyPenalty = Math.Max(0, difficultyOffset);
        var statBonus = Math.Max(0, statTier / 2);
        var minimumAdjustedLength = Math.Max(PatternMemoryMinSequenceLength, baseSequenceLength - 2);
        var effectiveSequenceLength = Math.Clamp(
            baseSequenceLength + difficultyPenalty - statBonus,
            minimumAdjustedLength,
            PatternMemoryMaxSequenceLength);
        var effectiveRevealMs = Math.Clamp(
            revealMs - (difficultyOffset * 150) + (statTier * 100),
            PatternMemoryMinRevealMs,
            PatternMemoryMaxRevealMs);
        var effectiveInputTimeoutMs = Math.Clamp(
            inputTimeoutMs - (difficultyOffset * 250) + (statTier * 150),
            PatternMemoryMinInputTimeoutMs,
            PatternMemoryMaxInputTimeoutMs);
        effectiveInputTimeoutMs = Math.Max(
            effectiveInputTimeoutMs,
            effectiveSequenceLength * PatternMemoryMinInputMsPerSymbol);
        var effectiveAllowedMistakes = Math.Clamp(
            allowedMistakes - difficultyPenalty + statBonus,
            0,
            effectiveSequenceLength - 1);

        return new PatternMemoryEffectiveRequirement(
            effectiveSequenceLength,
            effectiveRevealMs,
            effectiveInputTimeoutMs,
            effectiveAllowedMistakes);
    }

    internal static IReadOnlyList<string> GeneratePatternMemorySequence(
        IReadOnlyList<string> alphabet,
        int sequenceLength,
        string seed)
    {
        var tokens = alphabet
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(QteKeyInput.IsSupportedToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (tokens.Length == 0 || sequenceLength <= 0)
            return [];

        var length = Math.Clamp(sequenceLength, PatternMemoryMinSequenceLength, PatternMemoryMaxSequenceLength);
        var sequence = new List<string>(length);
        for (var i = 0; i < length; i++)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{i}"));
            var value = BitConverter.ToUInt32(bytes, 0);
            sequence.Add(tokens[(int)(value % tokens.Length)]);
        }

        return sequence;
    }

    internal static string ResolvePatternMemoryGrade(
        IReadOnlyList<string> expectedSequence,
        int allowedMistakes,
        IEnumerable<ConsoleKeyInfo> inputs,
        bool timedOut = false)
    {
        if (timedOut || expectedSequence.Count == 0)
            return "fail";

        var normalizedExpected = expectedSequence
            .Select(token => token.Trim().ToLowerInvariant())
            .ToArray();
        var mistakes = 0;
        var matches = 0;
        var index = 0;

        foreach (var input in inputs)
        {
            if (input.Key == ConsoleKey.Escape)
                return "fail";
            if (index >= normalizedExpected.Length)
                break;

            var token = QteKeyInput.NormalizeConsoleInput(input);
            if (string.Equals(token, normalizedExpected[index], StringComparison.Ordinal))
                matches++;
            else
                mistakes++;

            index++;
        }

        if (index < normalizedExpected.Length)
            return "fail";
        if (mistakes == 0 && matches == normalizedExpected.Length)
            return "success";

        var effectiveAllowedMistakes = Math.Clamp(allowedMistakes, 0, normalizedExpected.Length - 1);
        var partialMatchTarget = Math.Max(1, (int)Math.Ceiling(normalizedExpected.Length / 2d));
        return mistakes <= effectiveAllowedMistakes && matches >= partialMatchTarget
            ? "partial"
            : "fail";
    }

    private static bool TryReadPatternMemoryConfig(
        JsonObject? config,
        out IReadOnlyList<string> alphabet,
        out int sequenceLength,
        out int revealMs,
        out int inputTimeoutMs,
        out int allowedMistakes)
    {
        alphabet = [];
        sequenceLength = 0;
        revealMs = 0;
        inputTimeoutMs = 0;
        allowedMistakes = 0;

        if (config == null ||
            config["alphabet"] is not JsonArray alphabetArray ||
            config["sequenceLength"] is not JsonValue sequenceNode ||
            config["revealMs"] is not JsonValue revealNode ||
            config["inputTimeoutMs"] is not JsonValue timeoutNode ||
            config["allowedMistakes"] is not JsonValue mistakesNode)
        {
            return false;
        }

        var tokens = new List<string>();
        foreach (var key in alphabetArray)
        {
            if (key is not JsonValue keyValue ||
                !keyValue.TryGetValue<string>(out var token) ||
                string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            tokens.Add(token.Trim().ToLowerInvariant());
        }

        if (tokens.Count == 0 ||
            tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Count ||
            !tokens.All(QteKeyInput.IsSupportedToken) ||
            !sequenceNode.TryGetValue<int>(out sequenceLength) ||
            !revealNode.TryGetValue<int>(out revealMs) ||
            !timeoutNode.TryGetValue<int>(out inputTimeoutMs) ||
            !mistakesNode.TryGetValue<int>(out allowedMistakes) ||
            sequenceLength < PatternMemoryMinSequenceLength ||
            sequenceLength > PatternMemoryMaxSequenceLength ||
            revealMs < PatternMemoryMinRevealMs ||
            revealMs > PatternMemoryMaxRevealMs ||
            inputTimeoutMs < PatternMemoryMinInputTimeoutMs ||
            inputTimeoutMs > PatternMemoryMaxInputTimeoutMs ||
            inputTimeoutMs < sequenceLength * PatternMemoryMinInputMsPerSymbol ||
            allowedMistakes < 0 ||
            allowedMistakes >= sequenceLength)
        {
            return false;
        }

        alphabet = tokens;
        return true;
    }

    private async Task<QteGrade> RunRhythmPulseAsync(QteCheck check)
    {
        if (!TryReadRhythmPulseConfig(
                check.Config,
                out var pulseCount,
                out var beatIntervalMs,
                out var hitWindowMs,
                out var allowedMisses,
                out var patternVariation))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var effective = ComputeRhythmPulseEffectiveRequirement(
            pulseCount,
            beatIntervalMs,
            hitWindowMs,
            allowedMisses,
            check.BaseDifficulty,
            statTier);
        var schedule = GenerateRhythmPulseSchedule(
            effective.PulseCount,
            effective.BeatIntervalMs,
            patternVariation);
        if (schedule.Count == 0)
            return QteGrade.Fail;

        var inputs = new List<RhythmPulseInput>();
        var totalDurationMs = schedule[^1] + effective.HitWindowMs;
        var started = DateTime.UtcNow;

        return await RunMiniGameLiveAsync(
            "Ритм пульса",
            $"Нажимайте {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)} в момент вспышки. Esc - безопасный отказ считается провалом.",
            BuildRhythmPulseProgress(schedule, effective.HitWindowMs, effective.AllowedMisses, inputs, elapsedMs: 0, remainingMs: totalDurationMs),
            async renderer =>
        {
            while (true)
            {
                var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                if (elapsedMs > totalDurationMs)
                    break;

                while (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return QteGrade.Fail;

                    if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.Spacebar))
                        inputs.Add(new RhythmPulseInput(elapsedMs, key));
                }

                var remainingMs = Math.Max(0, totalDurationMs - elapsedMs);
                renderer.Update(BuildRhythmPulseProgress(schedule, effective.HitWindowMs, effective.AllowedMisses, inputs, elapsedMs, remainingMs));

                await Task.Delay(20);
            }

            return ParseGrade(ResolveRhythmPulseGrade(schedule, effective.HitWindowMs, effective.AllowedMisses, inputs));
        });
    }

    internal sealed record RhythmPulseInput(int OffsetMs, ConsoleKeyInfo KeyInfo);

    internal sealed record RhythmPulseEffectiveRequirement(
        int PulseCount,
        int BeatIntervalMs,
        int HitWindowMs,
        int AllowedMisses);

    internal static RhythmPulseEffectiveRequirement ComputeRhythmPulseEffectiveRequirement(
        int pulseCount,
        int beatIntervalMs,
        int hitWindowMs,
        int allowedMisses,
        int baseDifficulty,
        int statTier)
    {
        var basePulseCount = Math.Clamp(
            pulseCount,
            RhythmPulseMinPulseCount,
            RhythmPulseMaxPulseCount);
        var effectiveBeatIntervalMs = Math.Clamp(
            beatIntervalMs,
            RhythmPulseMinBeatIntervalMs,
            RhythmPulseMaxBeatIntervalMs);
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var difficultyPenalty = Math.Max(0, difficultyOffset);
        var statBonus = Math.Max(0, statTier / 2);
        var minimumAdjustedPulseCount = Math.Max(RhythmPulseMinPulseCount, basePulseCount - 2);
        var effectivePulseCount = Math.Clamp(
            basePulseCount + difficultyPenalty - statBonus,
            minimumAdjustedPulseCount,
            RhythmPulseMaxPulseCount);
        var effectiveHitWindowMs = Math.Clamp(
            hitWindowMs - (difficultyOffset * 10) + (statTier * 8),
            RhythmPulseMinHitWindowMs,
            RhythmPulseMaxHitWindowMs);
        var maxNonOverlappingWindowMs = Math.Max(
            RhythmPulseMinHitWindowMs,
            (effectiveBeatIntervalMs - 1) / 2);
        effectiveHitWindowMs = Math.Min(effectiveHitWindowMs, maxNonOverlappingWindowMs);
        var effectiveAllowedMisses = Math.Clamp(
            allowedMisses - difficultyPenalty + statBonus,
            0,
            effectivePulseCount - 1);

        return new RhythmPulseEffectiveRequirement(
            effectivePulseCount,
            effectiveBeatIntervalMs,
            effectiveHitWindowMs,
            effectiveAllowedMisses);
    }

    internal static IReadOnlyList<int> GenerateRhythmPulseSchedule(
        int pulseCount,
        int beatIntervalMs,
        string? patternVariation)
    {
        if (pulseCount <= 0 || beatIntervalMs <= 0)
            return [];

        var count = Math.Clamp(pulseCount, RhythmPulseMinPulseCount, RhythmPulseMaxPulseCount);
        var baseInterval = Math.Clamp(beatIntervalMs, RhythmPulseMinBeatIntervalMs, RhythmPulseMaxBeatIntervalMs);
        var variation = NormalizeRhythmPulsePatternVariation(patternVariation);
        var offsets = new List<int>(count);
        var currentOffset = 0;

        for (var i = 0; i < count; i++)
        {
            var interval = variation switch
            {
                "accelerating" => Math.Max(
                    RhythmPulseMinBeatIntervalMs,
                    (int)Math.Round(baseInterval * (1d - Math.Min(i, 4) * 0.08d))),
                "swing" => Math.Max(
                    RhythmPulseMinBeatIntervalMs,
                    (int)Math.Round(baseInterval * (i % 2 == 0 ? 1.2d : 0.8d))),
                _ => baseInterval
            };
            currentOffset += interval;
            offsets.Add(currentOffset);
        }

        return offsets;
    }

    internal static string ResolveRhythmPulseGrade(
        IReadOnlyList<int> pulseOffsetsMs,
        int hitWindowMs,
        int allowedMisses,
        IEnumerable<RhythmPulseInput> inputs)
    {
        if (pulseOffsetsMs.Count == 0)
            return "fail";

        var inputList = inputs.ToArray();
        if (inputList.Any(input => input.KeyInfo.Key == ConsoleKey.Escape))
            return "fail";

        var hits = CountRhythmPulseHits(pulseOffsetsMs, hitWindowMs, inputList);
        var misses = pulseOffsetsMs.Count - hits;
        var effectiveAllowedMisses = Math.Clamp(allowedMisses, 0, pulseOffsetsMs.Count - 1);
        if (misses <= effectiveAllowedMisses)
            return "success";

        var partialHitTarget = Math.Max(1, (int)Math.Ceiling(pulseOffsetsMs.Count / 2d));
        return hits >= partialHitTarget ? "partial" : "fail";
    }

    private async Task<QteGrade> RunPrecisionChoiceAsync(QteCheck check)
    {
        if (!TryReadPrecisionChoiceConfig(
                check.Config,
                out var choices,
                out var timeoutMs,
                out var timeoutGrade,
                out var decoyHints))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var availableHintCount = decoyHints.Count + choices.Count(choice => !string.IsNullOrWhiteSpace(choice.Hint));
        var effective = ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs,
            check.BaseDifficulty,
            statTier,
            availableHintCount);
        var gradeChoices = choices
            .Select(choice => new PrecisionChoiceChoice(choice.Id, choice.Grade))
            .ToArray();
        var started = DateTime.UtcNow;

        return await RunMiniGameLiveAsync(
            "Точный выбор",
            "Нажмите номер варианта до истечения таймера. Esc - безопасный отказ считается провалом.",
            BuildPrecisionChoiceProgress(choices, decoyHints, effective.RevealedDecoyHintCount, effective.TimeoutMs),
            async renderer =>
        {
            while (true)
            {
                var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                var remainingMs = Math.Max(0, effective.TimeoutMs - elapsedMs);
                if (remainingMs <= 0)
                {
                    return ParseGrade(ResolvePrecisionChoiceGrade(
                        gradeChoices,
                        selectedChoiceId: null,
                        elapsedMs,
                        effective.TimeoutMs,
                        timeoutGrade));
                }

                while (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                    {
                        return ParseGrade(ResolvePrecisionChoiceGrade(
                            gradeChoices,
                            selectedChoiceId: null,
                            elapsedMs,
                            effective.TimeoutMs,
                            timeoutGrade,
                            canceled: true));
                    }

                    if (TryGetPrecisionChoiceIndex(key, choices.Count, out var choiceIndex))
                    {
                        return ParseGrade(ResolvePrecisionChoiceGrade(
                            gradeChoices,
                            choices[choiceIndex].Id,
                            elapsedMs,
                            effective.TimeoutMs,
                            timeoutGrade));
                    }
                }

                renderer.Update(BuildPrecisionChoiceProgress(choices, decoyHints, effective.RevealedDecoyHintCount, remainingMs));

                await Task.Delay(20);
            }
        });
    }

    internal sealed record PrecisionChoiceChoice(string Id, string Grade);

    internal sealed record PrecisionChoiceEffectiveRequirement(
        int TimeoutMs,
        int RevealedDecoyHintCount);

    private sealed record PrecisionChoiceDisplayChoice(
        string Id,
        string Label,
        string? Description,
        string? Hint,
        string Grade);

    private sealed record PrecisionChoiceDecoyHint(string ChoiceId, string Hint);

    internal static PrecisionChoiceEffectiveRequirement ComputePrecisionChoiceEffectiveRequirement(
        int timeoutMs,
        int baseDifficulty,
        int statTier,
        int decoyHintCount)
    {
        var authoredTimeoutMs = Math.Clamp(
            timeoutMs,
            PrecisionChoiceMinTimeoutMs,
            PrecisionChoiceMaxTimeoutMs);
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var adjustedTimeoutMs = authoredTimeoutMs - (difficultyOffset * 300) + (statTier * 250);
        adjustedTimeoutMs = Math.Clamp(
            adjustedTimeoutMs,
            PrecisionChoiceMinTimeoutMs,
            PrecisionChoiceMaxTimeoutMs);
        adjustedTimeoutMs = Math.Max(
            adjustedTimeoutMs,
            Math.Max(PrecisionChoiceMinTimeoutMs, (int)Math.Ceiling(authoredTimeoutMs / 2d)));

        var hintCount = Math.Clamp(decoyHintCount, 0, PrecisionChoiceMaxChoices - 1);
        var revealedHints = 0;
        if (hintCount > 0)
        {
            var easyBonus = Math.Max(0, -difficultyOffset);
            var difficultyPenalty = Math.Max(0, difficultyOffset);
            var statBonus = Math.Max(0, statTier / 2);
            var weakStatPenalty = statTier < 0 ? 1 : 0;
            revealedHints = Math.Clamp(
                1 + easyBonus + statBonus - difficultyPenalty - weakStatPenalty,
                0,
                hintCount);
        }

        return new PrecisionChoiceEffectiveRequirement(adjustedTimeoutMs, revealedHints);
    }

    internal static string ResolvePrecisionChoiceGrade(
        IReadOnlyList<PrecisionChoiceChoice> choices,
        string? selectedChoiceId,
        int elapsedMs,
        int timeoutMs,
        string? timeoutGrade = null,
        bool canceled = false)
    {
        if (canceled)
            return "fail";

        var effectiveTimeoutMs = Math.Clamp(
            timeoutMs,
            PrecisionChoiceMinTimeoutMs,
            PrecisionChoiceMaxTimeoutMs);
        if (elapsedMs >= effectiveTimeoutMs || string.IsNullOrWhiteSpace(selectedChoiceId))
            return NormalizePrecisionChoiceTimeoutGrade(timeoutGrade);

        var choice = choices.FirstOrDefault(item =>
            string.Equals(item.Id, selectedChoiceId, StringComparison.Ordinal));
        return choice == null
            ? "fail"
            : NormalizePrecisionChoiceChoiceGrade(choice.Grade);
    }

    private static string NormalizePrecisionChoiceChoiceGrade(string? grade) => grade switch
    {
        "success" => "success",
        "partial" => "partial",
        _ => "fail"
    };

    private static string NormalizePrecisionChoiceTimeoutGrade(string? timeoutGrade) =>
        string.Equals(timeoutGrade, "partial", StringComparison.Ordinal) ? "partial" : "fail";

    private async Task<QteGrade> RunStealthNoiseAsync(QteCheck check)
    {
        if (!TryReadStealthNoiseConfig(
                check.Config,
                out var durationMs,
                out var startingNoise,
                out var dangerThreshold,
                out var noiseDriftPerSecond,
                out var recoveryPerInput,
                out var allowedOverThresholdMs,
                out var gradeThresholds,
                out var recoveryKey,
                out var recoveryLabel,
                out var warningLabel))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var effective = ComputeStealthNoiseEffectiveRequirement(
            durationMs,
            startingNoise,
            dangerThreshold,
            noiseDriftPerSecond,
            recoveryPerInput,
            allowedOverThresholdMs,
            gradeThresholds,
            check.BaseDifficulty,
            statTier,
            recoveryKey);
        var inputs = new List<StealthNoiseInput>();
        var started = DateTime.UtcNow;
        var recoveryPrompt = string.IsNullOrWhiteSpace(recoveryLabel)
            ? "снизить шум"
            : recoveryLabel;

        return await RunMiniGameLiveAsync(
            "Тихий проход",
            $"Нажимайте {QteKeyInput.FormatPromptLabel(effective.RecoveryKey)}, чтобы {recoveryPrompt}. Esc - безопасный отказ считается провалом.",
            BuildStealthNoiseProgress(effective, inputs, elapsedMs: 0, remainingMs: effective.DurationMs, warningLabel),
            async renderer =>
        {
            while (true)
            {
                var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                if (elapsedMs >= effective.DurationMs)
                    break;

                while (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return QteGrade.Fail;

                    var token = QteKeyInput.NormalizeConsoleInput(key);
                    if (string.Equals(token, effective.RecoveryKey, StringComparison.Ordinal))
                        inputs.Add(new StealthNoiseInput(elapsedMs, key));
                }

                var sample = SimulateStealthNoise(effective, inputs, elapsedMs);
                if (sample.OverThresholdMs > effective.GradeThresholds.PartialMaxOverThresholdMs)
                    return QteGrade.Fail;

                var remainingMs = Math.Max(0, effective.DurationMs - elapsedMs);
                renderer.Update(BuildStealthNoiseProgress(effective, inputs, elapsedMs, remainingMs, warningLabel));

                await Task.Delay(20);
            }

            return ParseGrade(ResolveStealthNoiseGrade(effective, inputs));
        });
    }

    internal sealed record StealthNoiseInput(int OffsetMs, ConsoleKeyInfo KeyInfo);

    internal sealed record StealthNoiseGradeThresholds(
        double SuccessMaxNoise,
        int SuccessMaxOverThresholdMs,
        double PartialMaxNoise,
        int PartialMaxOverThresholdMs);

    internal sealed record StealthNoiseEffectiveRequirement(
        int DurationMs,
        double StartingNoise,
        double DangerThreshold,
        double NoiseDriftPerSecond,
        double RecoveryPerInput,
        int AllowedOverThresholdMs,
        StealthNoiseGradeThresholds GradeThresholds,
        string RecoveryKey);

    private sealed record StealthNoiseSample(double Noise, int OverThresholdMs);

    internal static StealthNoiseEffectiveRequirement ComputeStealthNoiseEffectiveRequirement(
        int durationMs,
        double startingNoise,
        double dangerThreshold,
        double noiseDriftPerSecond,
        double recoveryPerInput,
        int allowedOverThresholdMs,
        StealthNoiseGradeThresholds gradeThresholds,
        int baseDifficulty,
        int statTier,
        string recoveryKey)
    {
        var effectiveDurationMs = Math.Clamp(durationMs, StealthNoiseMinDurationMs, StealthNoiseMaxDurationMs);
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var effectiveDrift = Math.Clamp(
            noiseDriftPerSecond + (difficultyOffset * 1.5d) - (statTier * 0.5d),
            StealthNoiseMinPositiveValue,
            StealthNoiseMaxMeterValue);
        var effectiveRecovery = Math.Clamp(
            recoveryPerInput + statTier - Math.Max(0, difficultyOffset),
            StealthNoiseMinPositiveValue,
            StealthNoiseMaxMeterValue);
        var effectiveAllowedOverThresholdMs = Math.Clamp(
            allowedOverThresholdMs - (difficultyOffset * 100) + (statTier * 100),
            0,
            effectiveDurationMs);

        return new StealthNoiseEffectiveRequirement(
            effectiveDurationMs,
            ClampStealthNoiseMeter(startingNoise),
            Math.Clamp(dangerThreshold, StealthNoiseMinDangerThreshold, StealthNoiseMaxMeterValue),
            effectiveDrift,
            effectiveRecovery,
            effectiveAllowedOverThresholdMs,
            gradeThresholds,
            NormalizeStealthNoiseRecoveryKey(recoveryKey));
    }

    internal static string ResolveStealthNoiseGrade(
        StealthNoiseEffectiveRequirement effective,
        IEnumerable<StealthNoiseInput> inputs,
        bool canceled = false)
    {
        var inputList = inputs.ToArray();
        if (canceled || inputList.Any(input => input.KeyInfo.Key == ConsoleKey.Escape))
            return "fail";

        var sample = SimulateStealthNoise(effective, inputList, effective.DurationMs);
        var thresholds = effective.GradeThresholds;
        var successOverThresholdLimit = Math.Min(
            effective.AllowedOverThresholdMs,
            thresholds.SuccessMaxOverThresholdMs);
        if (sample.Noise <= thresholds.SuccessMaxNoise &&
            sample.OverThresholdMs <= successOverThresholdLimit)
        {
            return "success";
        }

        var partialOverThresholdLimit = Math.Min(
            effective.AllowedOverThresholdMs,
            thresholds.PartialMaxOverThresholdMs);
        return sample.Noise <= thresholds.PartialMaxNoise &&
               sample.OverThresholdMs <= partialOverThresholdLimit
            ? "partial"
            : "fail";
    }

    internal static string ResolveStealthNoiseGrade(
        JsonObject? config,
        int baseDifficulty,
        int statTier,
        IEnumerable<StealthNoiseInput> inputs,
        bool canceled = false)
    {
        if (!TryReadStealthNoiseConfig(
                config,
                out var durationMs,
                out var startingNoise,
                out var dangerThreshold,
                out var noiseDriftPerSecond,
                out var recoveryPerInput,
                out var allowedOverThresholdMs,
                out var gradeThresholds,
                out var recoveryKey,
                out _,
                out _))
        {
            return "fail";
        }

        var effective = ComputeStealthNoiseEffectiveRequirement(
            durationMs,
            startingNoise,
            dangerThreshold,
            noiseDriftPerSecond,
            recoveryPerInput,
            allowedOverThresholdMs,
            gradeThresholds,
            baseDifficulty,
            statTier,
            recoveryKey);
        return ResolveStealthNoiseGrade(effective, inputs, canceled);
    }

    private static StealthNoiseSample SimulateStealthNoise(
        StealthNoiseEffectiveRequirement effective,
        IEnumerable<StealthNoiseInput> inputs,
        int elapsedMs)
    {
        var endMs = Math.Clamp(elapsedMs, 0, effective.DurationMs);
        var noise = ClampStealthNoiseMeter(effective.StartingNoise);
        var currentOffsetMs = 0;
        var overThresholdMs = 0d;
        var recoveryInputs = inputs
            .Where(input => input.OffsetMs >= 0 && input.OffsetMs <= endMs)
            .OrderBy(input => input.OffsetMs)
            .ToArray();

        foreach (var input in recoveryInputs)
        {
            AdvanceStealthNoise(
                effective,
                input.OffsetMs - currentOffsetMs,
                ref noise,
                ref overThresholdMs);
            currentOffsetMs = input.OffsetMs;

            var token = QteKeyInput.NormalizeConsoleInput(input.KeyInfo);
            if (string.Equals(token, effective.RecoveryKey, StringComparison.Ordinal))
                noise = ClampStealthNoiseMeter(noise - effective.RecoveryPerInput);
        }

        AdvanceStealthNoise(effective, endMs - currentOffsetMs, ref noise, ref overThresholdMs);
        return new StealthNoiseSample(noise, (int)Math.Round(overThresholdMs, MidpointRounding.AwayFromZero));
    }

    private static void AdvanceStealthNoise(
        StealthNoiseEffectiveRequirement effective,
        int deltaMs,
        ref double noise,
        ref double overThresholdMs)
    {
        if (deltaMs <= 0)
            return;

        overThresholdMs += CalculateStealthNoiseOverThresholdMs(
            noise,
            effective.NoiseDriftPerSecond,
            effective.DangerThreshold,
            deltaMs);
        noise = ClampStealthNoiseMeter(noise + (effective.NoiseDriftPerSecond * deltaMs / 1000d));
    }

    private static double CalculateStealthNoiseOverThresholdMs(
        double startingNoise,
        double driftPerSecond,
        double dangerThreshold,
        int deltaMs)
    {
        if (startingNoise >= dangerThreshold)
            return deltaMs;

        if (driftPerSecond <= 0)
            return 0;

        var timeToThresholdMs = (dangerThreshold - startingNoise) / driftPerSecond * 1000d;
        return timeToThresholdMs >= deltaMs ? 0 : deltaMs - timeToThresholdMs;
    }

    private static bool TryReadStealthNoiseConfig(
        JsonObject? config,
        out int durationMs,
        out double startingNoise,
        out double dangerThreshold,
        out double noiseDriftPerSecond,
        out double recoveryPerInput,
        out int allowedOverThresholdMs,
        out StealthNoiseGradeThresholds gradeThresholds,
        out string recoveryKey,
        out string? recoveryLabel,
        out string? warningLabel)
    {
        durationMs = 0;
        startingNoise = 0;
        dangerThreshold = 0;
        noiseDriftPerSecond = 0;
        recoveryPerInput = 0;
        allowedOverThresholdMs = 0;
        gradeThresholds = new StealthNoiseGradeThresholds(0, 0, 0, 0);
        recoveryKey = "space";
        recoveryLabel = null;
        warningLabel = null;

        if (config == null ||
            !TryGetStealthNoiseInt(config, "durationMs", out durationMs) ||
            !TryGetStealthNoiseDouble(config, "startingNoise", out startingNoise) ||
            !TryGetStealthNoiseDouble(config, "dangerThreshold", out dangerThreshold) ||
            !TryGetStealthNoiseDouble(config, "noiseDriftPerSecond", out noiseDriftPerSecond) ||
            !TryGetStealthNoiseDouble(config, "recoveryPerInput", out recoveryPerInput) ||
            !TryGetStealthNoiseInt(config, "allowedOverThresholdMs", out allowedOverThresholdMs) ||
            config["gradeThresholds"] is not JsonObject thresholds ||
            !TryGetStealthNoiseDouble(thresholds, "successMaxNoise", out var successMaxNoise) ||
            !TryGetStealthNoiseInt(thresholds, "successMaxOverThresholdMs", out var successMaxOverThresholdMs) ||
            !TryGetStealthNoiseDouble(thresholds, "partialMaxNoise", out var partialMaxNoise) ||
            !TryGetStealthNoiseInt(thresholds, "partialMaxOverThresholdMs", out var partialMaxOverThresholdMs))
        {
            return false;
        }

        if (durationMs < StealthNoiseMinDurationMs ||
            durationMs > StealthNoiseMaxDurationMs ||
            startingNoise < StealthNoiseMinMeterValue ||
            startingNoise > StealthNoiseMaxMeterValue ||
            dangerThreshold < StealthNoiseMinDangerThreshold ||
            dangerThreshold > StealthNoiseMaxMeterValue ||
            startingNoise > dangerThreshold ||
            noiseDriftPerSecond < StealthNoiseMinPositiveValue ||
            noiseDriftPerSecond > StealthNoiseMaxMeterValue ||
            recoveryPerInput < StealthNoiseMinPositiveValue ||
            recoveryPerInput > StealthNoiseMaxMeterValue ||
            allowedOverThresholdMs < 0 ||
            allowedOverThresholdMs > durationMs ||
            successMaxNoise < StealthNoiseMinMeterValue ||
            successMaxNoise > dangerThreshold ||
            successMaxOverThresholdMs < 0 ||
            successMaxOverThresholdMs > allowedOverThresholdMs ||
            partialMaxNoise < successMaxNoise ||
            partialMaxNoise > StealthNoiseMaxMeterValue ||
            partialMaxOverThresholdMs < successMaxOverThresholdMs ||
            partialMaxOverThresholdMs > durationMs)
        {
            return false;
        }

        if (config["recoveryKey"] is JsonValue recoveryKeyNode)
        {
            if (!recoveryKeyNode.TryGetValue<string>(out var recoveryKeyValue) ||
                !QteKeyInput.IsSupportedToken(recoveryKeyValue.Trim().ToLowerInvariant()))
            {
                return false;
            }

            recoveryKey = recoveryKeyValue.Trim().ToLowerInvariant();
        }
        else if (config["recoveryKey"] is not null)
        {
            return false;
        }

        if (!TryReadStealthNoiseOptionalText(config, "recoveryLabel", out recoveryLabel) ||
            !TryReadStealthNoiseOptionalText(config, "warningLabel", out warningLabel))
        {
            return false;
        }

        gradeThresholds = new StealthNoiseGradeThresholds(
            successMaxNoise,
            successMaxOverThresholdMs,
            partialMaxNoise,
            partialMaxOverThresholdMs);
        return true;
    }

    private static bool TryGetStealthNoiseInt(JsonObject root, string propertyName, out int value)
    {
        value = 0;
        return root[propertyName] is JsonValue node && node.TryGetValue<int>(out value);
    }

    private static bool TryGetStealthNoiseDouble(JsonObject root, string propertyName, out double value)
    {
        value = 0;
        if (root[propertyName] is not JsonValue node)
            return false;

        if (node.TryGetValue<double>(out value))
            return true;
        if (node.TryGetValue<int>(out var intValue))
        {
            value = intValue;
            return true;
        }

        return false;
    }

    private static bool TryReadStealthNoiseOptionalText(JsonObject root, string propertyName, out string? value)
    {
        value = null;
        if (root[propertyName] is null)
            return true;
        if (root[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static double ClampStealthNoiseMeter(double noise) =>
        Math.Clamp(noise, StealthNoiseMinMeterValue, StealthNoiseMaxMeterValue);

    private static string NormalizeStealthNoiseRecoveryKey(string recoveryKey) =>
        QteKeyInput.IsSupportedToken(recoveryKey.Trim().ToLowerInvariant())
            ? recoveryKey.Trim().ToLowerInvariant()
            : "space";

    private async Task<QteGrade> RunLockPinSetAsync(QteCheck check)
    {
        if (!TryReadLockPinSetConfig(
                check.Config,
                out var pinCount,
                out var pinWindows,
                out var timerMs,
                out var pickDurability,
                out var maxMistakes,
                out var pinDriftPerSecond,
                out var gradeThresholds,
                out var adjustKey,
                out var setKey,
                out var pinLabel,
                out var durabilityLabel,
                out var warningLabel))
        {
            return QteGrade.Fail;
        }

        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var effective = ComputeLockPinSetEffectiveRequirement(
            pinCount,
            pinWindows,
            timerMs,
            pickDurability,
            maxMistakes,
            pinDriftPerSecond,
            gradeThresholds,
            check.BaseDifficulty,
            statTier,
            adjustKey,
            setKey);
        var opened = new bool[effective.PinCount];
        var inputs = new List<LockPinSetInput>();
        var currentPinIndex = 0;
        var currentPosition = 50d;
        var mistakes = 0;
        var durabilityRemaining = effective.PickDurability;
        var started = DateTime.UtcNow;
        var lastTick = started;
        var pinName = string.IsNullOrWhiteSpace(pinLabel) ? "штифт" : pinLabel;
        var durabilityPrompt = string.IsNullOrWhiteSpace(durabilityLabel)
            ? "беречь отмычку"
            : durabilityLabel;

        return await RunMiniGameLiveAsync(
            "Штифты замка",
            $"{QteKeyInput.FormatPromptLabel(effective.AdjustKey)} поднимает текущий {pinName}; Shift+{QteKeyInput.FormatPromptLabel(effective.AdjustKey)} опускает; {QteKeyInput.FormatPromptLabel(effective.SetKey)} фиксирует. Нужно {durabilityPrompt}. Esc - безопасный отказ считается провалом.",
            BuildLockPinSetProgress(
                effective,
                opened,
                currentPinIndex,
                currentPosition,
                mistakes,
                durabilityRemaining,
                effective.TimerMs,
                pinName,
                warningLabel),
            async renderer =>
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                var elapsedMs = (int)(now - started).TotalMilliseconds;
                if (elapsedMs >= effective.TimerMs)
                    break;

                var deltaMs = (int)(now - lastTick).TotalMilliseconds;
                lastTick = now;
                currentPosition = ApplyLockPinSetDrift(currentPosition, effective.PinDriftPerSecond, deltaMs, currentPinIndex);

                while (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return QteGrade.Fail;

                    if (TryGetLockPinSetAdjustmentDirection(key, effective.AdjustKey, out var adjustmentDirection))
                    {
                        currentPosition = ApplyLockPinSetAdjustment(currentPosition, adjustmentDirection);
                        continue;
                    }

                    var token = QteKeyInput.NormalizeConsoleInput(key);
                    if (!string.Equals(token, effective.SetKey, StringComparison.Ordinal))
                        continue;

                    var attempt = new LockPinSetInput(elapsedMs, currentPinIndex, currentPosition);
                    inputs.Add(attempt);
                    if (IsLockPinInputInsideWindow(effective, attempt))
                    {
                        opened[currentPinIndex] = true;
                        if (opened.All(value => value))
                            return ParseGrade(ResolveLockPinSetGrade(effective, inputs));

                        currentPinIndex = FindNextClosedLockPin(opened, currentPinIndex);
                        currentPosition = 50d;
                        continue;
                    }

                    mistakes++;
                    durabilityRemaining--;
                    if (mistakes > effective.MaxMistakes || durabilityRemaining <= 0)
                        return QteGrade.Fail;
                }

                var remainingMs = Math.Max(0, effective.TimerMs - elapsedMs);
                renderer.Update(BuildLockPinSetProgress(
                    effective,
                    opened,
                    currentPinIndex,
                    currentPosition,
                    mistakes,
                    durabilityRemaining,
                    remainingMs,
                    pinName,
                    warningLabel));

                await Task.Delay(20);
            }

            return ParseGrade(ResolveLockPinSetGrade(effective, inputs));
        });
    }

    internal sealed record LockPinWindow(int Pin, double Min, double Max, string? Label);

    internal sealed record LockPinSetGradeThresholds(
        int SuccessMaxTimeMs,
        int SuccessMaxMistakes,
        int PartialMaxTimeMs,
        int PartialMaxMistakes);

    internal sealed record LockPinSetEffectiveRequirement(
        int PinCount,
        IReadOnlyList<LockPinWindow> PinWindows,
        int TimerMs,
        int PickDurability,
        int MaxMistakes,
        double PinDriftPerSecond,
        LockPinSetGradeThresholds GradeThresholds,
        string AdjustKey,
        string SetKey);

    internal sealed record LockPinSetInput(int OffsetMs, int PinIndex, double Position, bool Canceled = false);

    internal static LockPinSetEffectiveRequirement ComputeLockPinSetEffectiveRequirement(
        int pinCount,
        IReadOnlyList<LockPinWindow> pinWindows,
        int timerMs,
        int pickDurability,
        int maxMistakes,
        double pinDriftPerSecond,
        LockPinSetGradeThresholds gradeThresholds,
        int baseDifficulty,
        int statTier,
        string adjustKey,
        string setKey)
    {
        var effectivePinCount = Math.Clamp(pinCount, LockPinSetMinPinCount, LockPinSetMaxPinCount);
        var difficultyOffset = Math.Clamp(baseDifficulty, 1, 5) - 3;
        var windowPadding = statTier - Math.Max(0, difficultyOffset);
        var effectiveWindows = pinWindows
            .Take(effectivePinCount)
            .Select(window => AdjustLockPinWindow(window, windowPadding))
            .ToArray();
        var effectiveTimerMs = Math.Clamp(
            timerMs - (difficultyOffset * 500) + (statTier * 250),
            LockPinSetMinTimerMs,
            LockPinSetMaxTimerMs);
        var effectivePickDurability = Math.Clamp(
            pickDurability,
            LockPinSetMinPickDurability,
            LockPinSetMaxPickDurability);
        var statMistakeBonus = Math.Min(2, Math.Max(0, statTier) / 2);
        var effectiveMaxMistakes = Math.Clamp(
            maxMistakes - Math.Max(0, difficultyOffset) + statMistakeBonus,
            0,
            effectivePickDurability);
        var effectiveDrift = Math.Clamp(
            pinDriftPerSecond + (difficultyOffset * 0.5d) - (statTier * 0.25d),
            LockPinSetMinPinDriftPerSecond,
            LockPinSetMaxPinDriftPerSecond);
        var effectiveGradeThresholds = ClampLockPinSetGradeThresholds(
            gradeThresholds,
            effectiveTimerMs,
            effectiveMaxMistakes);

        return new LockPinSetEffectiveRequirement(
            effectivePinCount,
            effectiveWindows,
            effectiveTimerMs,
            effectivePickDurability,
            effectiveMaxMistakes,
            effectiveDrift,
            effectiveGradeThresholds,
            NormalizeLockPinSetKey(adjustKey, "q"),
            NormalizeLockPinSetKey(setKey, "space"));
    }

    internal static bool TryGetLockPinSetAdjustmentDirection(
        ConsoleKeyInfo key,
        string adjustKey,
        out int direction)
    {
        direction = 0;
        var token = QteKeyInput.NormalizeConsoleInput(key);
        if (!string.Equals(token, NormalizeLockPinSetKey(adjustKey, "q"), StringComparison.Ordinal))
            return false;

        direction = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -1 : 1;
        return true;
    }

    internal static double ApplyLockPinSetAdjustment(double position, int direction)
    {
        if (direction == 0)
            return ClampLockPinPosition(position);

        return ClampLockPinPosition(position + (Math.Sign(direction) * 5d));
    }

    internal static string ResolveLockPinSetGrade(
        LockPinSetEffectiveRequirement effective,
        IEnumerable<LockPinSetInput> inputs,
        bool canceled = false)
    {
        if (canceled || !IsLockPinSetRequirementUsable(effective))
            return "fail";

        var opened = new bool[effective.PinCount];
        var mistakes = 0;
        var durabilityRemaining = effective.PickDurability;
        int? openedAtMs = null;

        foreach (var input in inputs.OrderBy(input => input.OffsetMs))
        {
            if (input.Canceled)
                return "fail";
            if (input.OffsetMs < 0)
                continue;
            if (input.OffsetMs > effective.TimerMs)
                break;

            if (input.PinIndex < 0 || input.PinIndex >= effective.PinCount)
            {
                mistakes++;
                durabilityRemaining--;
            }
            else if (!opened[input.PinIndex] && IsLockPinInputInsideWindow(effective, input))
            {
                opened[input.PinIndex] = true;
                if (opened.All(value => value))
                {
                    openedAtMs = input.OffsetMs;
                    break;
                }
            }
            else if (!opened[input.PinIndex])
            {
                mistakes++;
                durabilityRemaining--;
            }

            if (durabilityRemaining <= 0 || mistakes > effective.MaxMistakes)
                return "fail";
        }

        if (!opened.All(value => value) || openedAtMs == null)
            return "fail";

        var thresholds = effective.GradeThresholds;
        if (openedAtMs.Value <= thresholds.SuccessMaxTimeMs &&
            mistakes <= thresholds.SuccessMaxMistakes)
        {
            return "success";
        }

        return openedAtMs.Value <= thresholds.PartialMaxTimeMs &&
               mistakes <= thresholds.PartialMaxMistakes
            ? "partial"
            : "fail";
    }

    internal static string ResolveLockPinSetGrade(
        JsonObject? config,
        int baseDifficulty,
        int statTier,
        IEnumerable<LockPinSetInput> inputs,
        bool canceled = false)
    {
        if (!TryReadLockPinSetConfig(
                config,
                out var pinCount,
                out var pinWindows,
                out var timerMs,
                out var pickDurability,
                out var maxMistakes,
                out var pinDriftPerSecond,
                out var gradeThresholds,
                out var adjustKey,
                out var setKey,
                out _,
                out _,
                out _))
        {
            return "fail";
        }

        var effective = ComputeLockPinSetEffectiveRequirement(
            pinCount,
            pinWindows,
            timerMs,
            pickDurability,
            maxMistakes,
            pinDriftPerSecond,
            gradeThresholds,
            baseDifficulty,
            statTier,
            adjustKey,
            setKey);
        return ResolveLockPinSetGrade(effective, inputs, canceled);
    }

    private static bool TryReadLockPinSetConfig(
        JsonObject? config,
        out int pinCount,
        out IReadOnlyList<LockPinWindow> pinWindows,
        out int timerMs,
        out int pickDurability,
        out int maxMistakes,
        out double pinDriftPerSecond,
        out LockPinSetGradeThresholds gradeThresholds,
        out string adjustKey,
        out string setKey,
        out string? pinLabel,
        out string? durabilityLabel,
        out string? warningLabel)
    {
        pinCount = 0;
        pinWindows = [];
        timerMs = 0;
        pickDurability = 0;
        maxMistakes = 0;
        pinDriftPerSecond = 0;
        gradeThresholds = new LockPinSetGradeThresholds(0, 0, 0, 0);
        adjustKey = "q";
        setKey = "space";
        pinLabel = null;
        durabilityLabel = null;
        warningLabel = null;

        if (config == null ||
            !TryGetLockPinSetInt(config, "pinCount", out pinCount) ||
            config["pinWindows"] is not JsonArray windows ||
            !TryGetLockPinSetInt(config, "timerMs", out timerMs) ||
            !TryGetLockPinSetInt(config, "pickDurability", out pickDurability) ||
            !TryGetLockPinSetInt(config, "maxMistakes", out maxMistakes) ||
            !TryGetLockPinSetDouble(config, "pinDriftPerSecond", out pinDriftPerSecond) ||
            config["gradeThresholds"] is not JsonObject thresholds ||
            !TryGetLockPinSetInt(thresholds, "successMaxTimeMs", out var successMaxTimeMs) ||
            !TryGetLockPinSetInt(thresholds, "successMaxMistakes", out var successMaxMistakes) ||
            !TryGetLockPinSetInt(thresholds, "partialMaxTimeMs", out var partialMaxTimeMs) ||
            !TryGetLockPinSetInt(thresholds, "partialMaxMistakes", out var partialMaxMistakes))
        {
            return false;
        }

        if (pinCount < LockPinSetMinPinCount ||
            pinCount > LockPinSetMaxPinCount ||
            windows.Count != pinCount ||
            timerMs < LockPinSetMinTimerMs ||
            timerMs > LockPinSetMaxTimerMs ||
            pickDurability < LockPinSetMinPickDurability ||
            pickDurability > LockPinSetMaxPickDurability ||
            maxMistakes < 0 ||
            maxMistakes > pickDurability ||
            pinDriftPerSecond < LockPinSetMinPinDriftPerSecond ||
            pinDriftPerSecond > LockPinSetMaxPinDriftPerSecond ||
            successMaxTimeMs < 0 ||
            successMaxTimeMs > timerMs ||
            successMaxMistakes < 0 ||
            successMaxMistakes > maxMistakes ||
            partialMaxTimeMs < successMaxTimeMs ||
            partialMaxTimeMs > timerMs ||
            partialMaxMistakes < successMaxMistakes ||
            partialMaxMistakes > maxMistakes)
        {
            return false;
        }

        var parsedWindows = new List<LockPinWindow>(pinCount);
        for (var index = 0; index < windows.Count; index++)
        {
            if (windows[index] is not JsonObject window ||
                !TryGetLockPinSetDouble(window, "min", out var min) ||
                !TryGetLockPinSetDouble(window, "max", out var max) ||
                min < LockPinSetMinPosition ||
                max > LockPinSetMaxPosition ||
                min >= max)
            {
                return false;
            }

            if (window["pin"] is not null &&
                (!TryGetLockPinSetInt(window, "pin", out var pin) || pin != index + 1))
            {
                return false;
            }

            if (!TryReadLockPinSetOptionalText(window, "label", out var label))
                return false;

            parsedWindows.Add(new LockPinWindow(index + 1, min, max, label));
        }

        if (!TryReadLockPinSetOptionalKey(config, "adjustKey", "q", out adjustKey) ||
            !TryReadLockPinSetOptionalKey(config, "setKey", "space", out setKey) ||
            !TryReadLockPinSetOptionalText(config, "pinLabel", out pinLabel) ||
            !TryReadLockPinSetOptionalText(config, "durabilityLabel", out durabilityLabel) ||
            !TryReadLockPinSetOptionalText(config, "warningLabel", out warningLabel))
        {
            return false;
        }

        pinWindows = parsedWindows;
        gradeThresholds = new LockPinSetGradeThresholds(
            successMaxTimeMs,
            successMaxMistakes,
            partialMaxTimeMs,
            partialMaxMistakes);
        return true;
    }

    private static bool IsLockPinSetRequirementUsable(LockPinSetEffectiveRequirement effective) =>
        effective.PinCount >= LockPinSetMinPinCount &&
        effective.PinCount <= LockPinSetMaxPinCount &&
        effective.PinWindows.Count == effective.PinCount &&
        effective.TimerMs is >= LockPinSetMinTimerMs and <= LockPinSetMaxTimerMs &&
        effective.PickDurability is >= LockPinSetMinPickDurability and <= LockPinSetMaxPickDurability &&
        effective.MaxMistakes >= 0 &&
        effective.MaxMistakes <= effective.PickDurability &&
        effective.PinDriftPerSecond is >= LockPinSetMinPinDriftPerSecond and <= LockPinSetMaxPinDriftPerSecond &&
        effective.GradeThresholds.SuccessMaxTimeMs >= 0 &&
        effective.GradeThresholds.SuccessMaxTimeMs <= effective.TimerMs &&
        effective.GradeThresholds.SuccessMaxMistakes >= 0 &&
        effective.GradeThresholds.SuccessMaxMistakes <= effective.MaxMistakes &&
        effective.GradeThresholds.PartialMaxTimeMs >= effective.GradeThresholds.SuccessMaxTimeMs &&
        effective.GradeThresholds.PartialMaxTimeMs <= effective.TimerMs &&
        effective.GradeThresholds.PartialMaxMistakes >= effective.GradeThresholds.SuccessMaxMistakes &&
        effective.GradeThresholds.PartialMaxMistakes <= effective.MaxMistakes;

    private static LockPinSetGradeThresholds ClampLockPinSetGradeThresholds(
        LockPinSetGradeThresholds thresholds,
        int effectiveTimerMs,
        int effectiveMaxMistakes)
    {
        var successMaxTimeMs = Math.Clamp(thresholds.SuccessMaxTimeMs, 0, effectiveTimerMs);
        var partialMaxTimeMs = Math.Clamp(thresholds.PartialMaxTimeMs, successMaxTimeMs, effectiveTimerMs);
        var successMaxMistakes = Math.Clamp(thresholds.SuccessMaxMistakes, 0, effectiveMaxMistakes);
        var partialMaxMistakes = Math.Clamp(thresholds.PartialMaxMistakes, successMaxMistakes, effectiveMaxMistakes);

        return new LockPinSetGradeThresholds(
            successMaxTimeMs,
            successMaxMistakes,
            partialMaxTimeMs,
            partialMaxMistakes);
    }

    private static LockPinWindow AdjustLockPinWindow(LockPinWindow window, int padding)
    {
        var min = ClampLockPinPosition(window.Min - padding);
        var max = ClampLockPinPosition(window.Max + padding);
        if (max > min)
            return new LockPinWindow(window.Pin, min, max, window.Label);

        var center = ClampLockPinPosition((window.Min + window.Max) / 2d);
        min = ClampLockPinPosition(center - 0.5d);
        max = ClampLockPinPosition(center + 0.5d);
        if (max <= min)
            max = Math.Min(LockPinSetMaxPosition, min + 1);

        return new LockPinWindow(window.Pin, min, max, window.Label);
    }

    private static bool IsLockPinInputInsideWindow(LockPinSetEffectiveRequirement effective, LockPinSetInput input)
    {
        if (input.PinIndex < 0 || input.PinIndex >= effective.PinWindows.Count)
            return false;

        var window = effective.PinWindows[input.PinIndex];
        var position = ClampLockPinPosition(input.Position);
        return position >= window.Min && position <= window.Max;
    }

    private static int FindNextClosedLockPin(IReadOnlyList<bool> opened, int currentIndex)
    {
        for (var offset = 1; offset <= opened.Count; offset++)
        {
            var candidate = (currentIndex + offset) % opened.Count;
            if (!opened[candidate])
                return candidate;
        }

        return currentIndex;
    }

    private static double ApplyLockPinSetDrift(double position, double driftPerSecond, int deltaMs, int pinIndex)
    {
        if (deltaMs <= 0 || driftPerSecond <= 0)
            return ClampLockPinPosition(position);

        var direction = pinIndex % 2 == 0 ? 1 : -1;
        return ClampLockPinPosition(position + (direction * driftPerSecond * deltaMs / 1000d));
    }

    private static bool TryGetLockPinSetInt(JsonObject root, string propertyName, out int value)
    {
        value = 0;
        return root[propertyName] is JsonValue node && node.TryGetValue<int>(out value);
    }

    private static bool TryGetLockPinSetDouble(JsonObject root, string propertyName, out double value)
    {
        value = 0;
        if (root[propertyName] is not JsonValue node)
            return false;

        if (node.TryGetValue<double>(out value))
            return true;
        if (node.TryGetValue<int>(out var intValue))
        {
            value = intValue;
            return true;
        }

        return false;
    }

    private static bool TryReadLockPinSetOptionalText(JsonObject root, string propertyName, out string? value)
    {
        value = null;
        if (root[propertyName] is null)
            return true;
        if (root[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static bool TryReadLockPinSetOptionalKey(
        JsonObject root,
        string propertyName,
        string fallback,
        out string value)
    {
        value = fallback;
        if (root[propertyName] is null)
            return true;
        if (root[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().ToLowerInvariant();
        if (!QteKeyInput.IsSupportedToken(normalized))
            return false;

        value = normalized;
        return true;
    }

    private static string NormalizeLockPinSetKey(string token, string fallback)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return QteKeyInput.IsSupportedToken(normalized) ? normalized : fallback;
    }

    private static double ClampLockPinPosition(double position) =>
        Math.Clamp(position, LockPinSetMinPosition, LockPinSetMaxPosition);

    private static int CountRhythmPulseHits(
        IReadOnlyList<int> pulseOffsetsMs,
        int hitWindowMs,
        IEnumerable<RhythmPulseInput> inputs)
    {
        if (pulseOffsetsMs.Count == 0)
            return 0;

        var window = Math.Max(0, hitWindowMs);
        var matched = new bool[pulseOffsetsMs.Count];
        foreach (var input in inputs.OrderBy(input => input.OffsetMs))
        {
            if (!QteKeyInput.MatchesConsoleKey(input.KeyInfo, ConsoleKey.Spacebar))
                continue;

            var bestIndex = -1;
            var bestDistance = window + 1;
            for (var i = 0; i < pulseOffsetsMs.Count; i++)
            {
                if (matched[i])
                    continue;

                var distance = Math.Abs(input.OffsetMs - pulseOffsetsMs[i]);
                if (distance <= window && distance < bestDistance)
                {
                    bestIndex = i;
                    bestDistance = distance;
                }
            }

            if (bestIndex >= 0)
                matched[bestIndex] = true;
        }

        return matched.Count(value => value);
    }

    private static bool TryReadRhythmPulseConfig(
        JsonObject? config,
        out int pulseCount,
        out int beatIntervalMs,
        out int hitWindowMs,
        out int allowedMisses,
        out string patternVariation)
    {
        pulseCount = 0;
        beatIntervalMs = 0;
        hitWindowMs = 0;
        allowedMisses = 0;
        patternVariation = "steady";

        if (config == null ||
            config["pulseCount"] is not JsonValue pulseNode ||
            config["beatIntervalMs"] is not JsonValue beatNode ||
            config["hitWindowMs"] is not JsonValue windowNode ||
            config["allowedMisses"] is not JsonValue missesNode)
        {
            return false;
        }

        if (!pulseNode.TryGetValue<int>(out pulseCount) ||
            !beatNode.TryGetValue<int>(out beatIntervalMs) ||
            !windowNode.TryGetValue<int>(out hitWindowMs) ||
            !missesNode.TryGetValue<int>(out allowedMisses) ||
            pulseCount < RhythmPulseMinPulseCount ||
            pulseCount > RhythmPulseMaxPulseCount ||
            beatIntervalMs < RhythmPulseMinBeatIntervalMs ||
            beatIntervalMs > RhythmPulseMaxBeatIntervalMs ||
            hitWindowMs < RhythmPulseMinHitWindowMs ||
            hitWindowMs > RhythmPulseMaxHitWindowMs ||
            hitWindowMs * 2 >= beatIntervalMs ||
            allowedMisses < 0 ||
            allowedMisses >= pulseCount)
        {
            return false;
        }

        if (config["patternVariation"] is null)
            return true;

        if (config["patternVariation"] is not JsonValue variationNode ||
            !variationNode.TryGetValue<string>(out var variation) ||
            string.IsNullOrWhiteSpace(variation))
        {
            return false;
        }

        variation = variation.Trim();
        if (!RhythmPulsePatternVariations.Contains(variation, StringComparer.Ordinal))
            return false;

        patternVariation = variation;
        return true;
    }

    private static bool TryReadPrecisionChoiceConfig(
        JsonObject? config,
        out IReadOnlyList<PrecisionChoiceDisplayChoice> choices,
        out int timeoutMs,
        out string timeoutGrade,
        out IReadOnlyList<PrecisionChoiceDecoyHint> decoyHints)
    {
        choices = [];
        timeoutMs = 0;
        timeoutGrade = "fail";
        decoyHints = [];

        if (config == null ||
            config["choices"] is not JsonArray choicesArray ||
            config["correctChoiceId"] is not JsonValue correctChoiceNode ||
            config["timeoutMs"] is not JsonValue timeoutNode ||
            !correctChoiceNode.TryGetValue<string>(out var correctChoiceId) ||
            string.IsNullOrWhiteSpace(correctChoiceId) ||
            !timeoutNode.TryGetValue<int>(out timeoutMs) ||
            timeoutMs < PrecisionChoiceMinTimeoutMs ||
            timeoutMs > PrecisionChoiceMaxTimeoutMs ||
            choicesArray.Count < PrecisionChoiceMinChoices ||
            choicesArray.Count > PrecisionChoiceMaxChoices)
        {
            return false;
        }

        if (config["timeoutGrade"] is not null)
        {
            if (config["timeoutGrade"] is not JsonValue timeoutGradeNode ||
                !timeoutGradeNode.TryGetValue<string>(out var timeoutGradeValue) ||
                string.IsNullOrWhiteSpace(timeoutGradeValue))
            {
                return false;
            }

            timeoutGradeValue = timeoutGradeValue.Trim();
            if (!string.Equals(timeoutGradeValue, "fail", StringComparison.Ordinal) &&
                !string.Equals(timeoutGradeValue, "partial", StringComparison.Ordinal))
            {
                return false;
            }

            timeoutGrade = timeoutGradeValue;
        }

        var parsedChoices = new List<PrecisionChoiceDisplayChoice>(choicesArray.Count);
        var gradesById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var choiceNode in choicesArray)
        {
            if (choiceNode is not JsonObject choiceObject ||
                !TryGetPrecisionChoiceString(choiceObject, "id", out var id) ||
                !TryGetPrecisionChoiceString(choiceObject, "label", out var label) ||
                !TryGetPrecisionChoiceString(choiceObject, "grade", out var grade) ||
                !AllowedPrecisionChoiceGrade(grade) ||
                gradesById.ContainsKey(id))
            {
                return false;
            }

            var description = TryGetPrecisionChoiceString(choiceObject, "description", out var descriptionValue)
                ? descriptionValue
                : null;
            var hint = TryGetPrecisionChoiceString(choiceObject, "hint", out var hintValue)
                ? hintValue
                : null;

            gradesById[id] = grade;
            parsedChoices.Add(new PrecisionChoiceDisplayChoice(id, label, description, hint, grade));
        }

        if (!gradesById.TryGetValue(correctChoiceId.Trim(), out var correctGrade) ||
            !string.Equals(correctGrade, "success", StringComparison.Ordinal) ||
            gradesById.Count(pair => string.Equals(pair.Value, "success", StringComparison.Ordinal)) != 1 ||
            gradesById.All(pair => string.Equals(pair.Value, "success", StringComparison.Ordinal)))
        {
            return false;
        }

        if (config["decoyHints"] is JsonArray decoyHintArray)
        {
            var parsedDecoyHints = new List<PrecisionChoiceDecoyHint>(decoyHintArray.Count);
            foreach (var decoyHintNode in decoyHintArray)
            {
                if (decoyHintNode is not JsonObject decoyHintObject ||
                    !TryGetPrecisionChoiceString(decoyHintObject, "choiceId", out var choiceId) ||
                    !TryGetPrecisionChoiceString(decoyHintObject, "hint", out var hint) ||
                    !gradesById.TryGetValue(choiceId, out var choiceGrade) ||
                    string.Equals(choiceGrade, "success", StringComparison.Ordinal))
                {
                    return false;
                }

                parsedDecoyHints.Add(new PrecisionChoiceDecoyHint(choiceId, hint));
            }

            decoyHints = parsedDecoyHints;
        }
        else if (config["decoyHints"] is not null)
        {
            return false;
        }

        choices = parsedChoices;
        return true;
    }

    private static bool TryGetPrecisionChoiceString(JsonObject root, string propertyName, out string value)
    {
        value = "";
        if (root[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static bool AllowedPrecisionChoiceGrade(string grade) =>
        string.Equals(grade, "success", StringComparison.Ordinal) ||
        string.Equals(grade, "partial", StringComparison.Ordinal) ||
        string.Equals(grade, "fail", StringComparison.Ordinal);

    private static string NormalizeRhythmPulsePatternVariation(string? patternVariation)
    {
        if (string.IsNullOrWhiteSpace(patternVariation))
            return "steady";

        var normalized = patternVariation.Trim();
        return RhythmPulsePatternVariations.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : "steady";
    }

    private async Task<QteGrade> RunTimingBarAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var requirement = ComputeTimingBarLiveRequirement(check.BaseDifficulty, statTier);

        return await RunMiniGameLiveAsync(
            "Полоса реакции",
            $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, когда маркер будет в центральной зоне. Esc - безопасный отказ считается провалом.",
            BuildTimingBarLiveFrame(requirement, position: 0, requirement.TimeoutMs),
            async renderer =>
                ParseGrade(await RunTimingBarLiveLoopAsync(
                    requirement,
                    _inputSource,
                    renderer,
                    SystemQteLiveClock.Instance)));
    }

    internal static async Task<string> RunTimingBarLiveLoopAsync(
        TimingBarLiveRequirement requirement,
        IConsoleInputSource inputSource,
        IQteMiniGameLiveRenderer renderer,
        IQteLiveClock clock)
    {
        var position = 0;
        var direction = 1;
        var started = clock.UtcNow;

        while (true)
        {
            var remainingMs = Math.Max(0, requirement.TimeoutMs - (int)(clock.UtcNow - started).TotalMilliseconds);
            if (remainingMs <= 0)
                return "fail";

            renderer.Update(
                BuildTimingBarAgentConsoleFrame(requirement, position, remainingMs),
                BuildTimingBarLiveFrame(requirement, position, remainingMs));

            if (TryReadImmediateKey(inputSource, out var key))
            {
                if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.Spacebar))
                {
                    if (position >= requirement.SuccessStart && position < requirement.SuccessStart + requirement.SuccessWidth)
                        return "success";
                    if (position >= requirement.PartialStart && position < requirement.PartialStart + requirement.PartialWidth)
                        return "partial";
                    return "fail";
                }

                if (key.Key == ConsoleKey.Escape)
                    return "fail";
            }

            await clock.DelayAsync(requirement.TickMs);
            position += direction;
            if (position >= requirement.Width - 1 || position <= 0)
                direction *= -1;
        }
    }

    internal sealed record TimingBarLiveRequirement(
        int Width,
        int SuccessStart,
        int SuccessWidth,
        int PartialStart,
        int PartialWidth,
        int TickMs,
        int TimeoutMs)
    {
        public int SuccessWindowMs => SuccessWidth * TickMs;
    }

    internal static TimingBarLiveRequirement ComputeTimingBarLiveRequirement(int baseDifficulty, int statTier)
    {
        var difficulty = Math.Clamp(baseDifficulty, 1, 5);
        const int width = 32;
        var statWindowBonus = Math.Clamp(statTier, 0, 3) / 2;
        var successWidth = Math.Clamp(7 - difficulty + statWindowBonus, 3, 10);
        if (difficulty >= 5)
            successWidth = Math.Min(successWidth, 4);

        var partialWidth = Math.Clamp(successWidth + (difficulty >= 4 ? 3 : 4), successWidth + 1, 15);
        var tickMs = Math.Clamp(120 - (difficulty * 18) + (statTier * 5), 35, 170);
        if (difficulty >= 5)
            tickMs = Math.Min(tickMs, 55);

        var passCount = difficulty >= 5
            ? 1
            : Math.Clamp(5 - difficulty + Math.Max(0, statTier / 3), 1, 4);
        var timeoutMs = Math.Clamp((width - 1) * tickMs * passCount, 1200, 6000);
        var successStart = (width - successWidth) / 2;
        var partialStart = (width - partialWidth) / 2;

        return new TimingBarLiveRequirement(
            width,
            successStart,
            successWidth,
            partialStart,
            partialWidth,
            tickMs,
            timeoutMs);
    }

    private async Task<QteGrade> RunPromptChainAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var requirement = ComputePromptChainLiveRequirement(check.BaseDifficulty, statTier);
        var steps = requirement.Steps;
        var allowedMistakes = requirement.AllowedMistakes;
        var prompts = new[] { ConsoleKey.W, ConsoleKey.A, ConsoleKey.S, ConsoleKey.D, ConsoleKey.E, ConsoleKey.Spacebar };
        var random = new Random();
        var sequence = Enumerable.Range(0, steps)
            .Select(_ => prompts[random.Next(prompts.Length)])
            .ToArray();
        var mistakes = 0;

        return await RunMiniGameLiveAsync(
            "Цепь знаков",
            "Нажимайте показанную физическую клавишу до истечения таймера. Esc - безопасный отказ считается провалом.",
            BuildPromptChainProgress(sequence[0], currentStep: 1, steps, mistakes, allowedMistakes, requirement.FirstPromptTimeoutMs),
            async renderer =>
                ParseGrade(await RunPromptChainLiveLoopAsync(
                    requirement,
                    sequence,
                    _inputSource,
                    renderer,
                    SystemQteLiveClock.Instance)));
    }

    internal static async Task<string> RunPromptChainLiveLoopAsync(
        PromptChainLiveRequirement requirement,
        IReadOnlyList<ConsoleKey> sequence,
        IConsoleInputSource inputSource,
        IQteMiniGameLiveRenderer renderer,
        IQteLiveClock clock)
    {
        var mistakes = 0;

        for (var i = 0; i < sequence.Count; i++)
        {
            var prompt = sequence[i];
            var promptTimeoutMs = requirement.PerPromptTimeoutMs + (i == 0 ? requirement.FirstPromptGraceMs : 0);
            var started = clock.UtcNow;
            ConsoleKeyInfo? pressed = null;

            while ((clock.UtcNow - started).TotalMilliseconds < promptTimeoutMs)
            {
                var remainingMs = Math.Max(0, promptTimeoutMs - (int)(clock.UtcNow - started).TotalMilliseconds);
                renderer.Update(
                    BuildPromptChainAgentConsoleFrame(
                        prompt,
                        i + 1,
                        sequence.Count,
                        mistakes,
                        requirement.AllowedMistakes,
                        remainingMs,
                        promptTimeoutMs),
                    BuildPromptChainProgress(
                        prompt,
                        i + 1,
                        sequence.Count,
                        mistakes,
                        requirement.AllowedMistakes,
                        remainingMs));

                if (TryReadImmediateKey(inputSource, out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return "fail";

                    pressed = key;
                    break;
                }

                await clock.DelayAsync(20);
            }

            if (pressed == null || !QteKeyInput.MatchesConsoleKey(pressed.Value, prompt))
                mistakes++;

            if (mistakes > requirement.AllowedMistakes)
                return "fail";
        }

        return mistakes == 0 ? "success" : "partial";
    }

    internal sealed record PromptChainLiveRequirement(
        int Steps,
        int AllowedMistakes,
        int PerPromptTimeoutMs,
        int FirstPromptGraceMs)
    {
        public int FirstPromptTimeoutMs => PerPromptTimeoutMs + FirstPromptGraceMs;
    }

    internal static PromptChainLiveRequirement ComputePromptChainLiveRequirement(int baseDifficulty, int statTier)
    {
        var difficulty = Math.Clamp(baseDifficulty, 1, 5);
        var steps = Math.Clamp(3 + difficulty - Math.Max(0, statTier - 1), 2, 7);
        var allowedMistakes = statTier >= 2 ? 1 : 0;
        var perPromptTimeoutMs = Math.Clamp(1350 + (statTier * 150) - (difficulty * 115), 750, 1800);
        var firstPromptGraceMs = Math.Clamp(420 + (Math.Max(0, -statTier) * 60) - (difficulty * 20), 250, 600);

        return new PromptChainLiveRequirement(steps, allowedMistakes, perPromptTimeoutMs, firstPromptGraceMs);
    }

    private async Task<QteGrade> RunBalanceMeterAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var requirement = ComputeBalanceMeterLiveRequirement(check.BaseDifficulty, statTier);
        var random = new Random();

        return await RunMiniGameLiveAsync(
            "Равновесие",
            $"Удерживайте индикатор в центральной зоне: {FormatCompactPhysicalKeyLabel(ConsoleKey.A)} или ← ведёт влево, {FormatCompactPhysicalKeyLabel(ConsoleKey.D)} или → ведёт вправо.",
            BuildBalanceMeter(
                value: 50,
                requirement.SafeHalfWidth,
                currentTick: 1,
                requirement.Ticks,
                requirement.MovementStep,
                playerDelta: null,
                driftDelta: null),
            async renderer =>
                ParseGrade(await RunBalanceMeterLiveLoopAsync(
                    requirement,
                    _inputSource,
                    renderer,
                    SystemQteLiveClock.Instance,
                    random.Next)));
    }

    internal sealed record BalanceMeterLiveRequirement(
        int SafeHalfWidth,
        int TickMs,
        int Ticks,
        int MovementStep,
        int DriftMinInclusive,
        int DriftMaxExclusive);

    internal static BalanceMeterLiveRequirement ComputeBalanceMeterLiveRequirement(int baseDifficulty, int statTier)
    {
        var difficulty = Math.Clamp(baseDifficulty, 1, 5);
        return new BalanceMeterLiveRequirement(
            SafeHalfWidth: Math.Clamp(18 - (difficulty * 2) + (statTier * 2), 8, 24),
            TickMs: Math.Clamp(140 - (statTier * 5) + (difficulty * 10), 70, 220),
            Ticks: 18 + (difficulty * 2),
            MovementStep: BalanceMeterMovementStep,
            DriftMinInclusive: -7 - difficulty,
            DriftMaxExclusive: 8 + difficulty);
    }

    internal static async Task<string> RunBalanceMeterLiveLoopAsync(
        BalanceMeterLiveRequirement requirement,
        IConsoleInputSource inputSource,
        IQteMiniGameLiveRenderer renderer,
        IQteLiveClock clock,
        Func<int, int, int> nextDrift)
    {
        var value = 50;
        var safeTicks = 0;

        for (var i = 0; i < requirement.Ticks; i++)
        {
            var playerDelta = 0;
            if (TryReadImmediateKey(inputSource, out var key))
            {
                if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.A) || key.Key == ConsoleKey.LeftArrow)
                {
                    var before = value;
                    value = Math.Max(0, value - requirement.MovementStep);
                    playerDelta = value - before;
                }
                else if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.D) || key.Key == ConsoleKey.RightArrow)
                {
                    var before = value;
                    value = Math.Min(100, value + requirement.MovementStep);
                    playerDelta = value - before;
                }
                else if (key.Key == ConsoleKey.Escape)
                    return "fail";
            }

            var beforeDrift = value;
            var rawDrift = nextDrift(requirement.DriftMinInclusive, requirement.DriftMaxExclusive);
            value = Math.Clamp(
                value + rawDrift,
                0,
                100);
            var driftDelta = value - beforeDrift;
            if (Math.Abs(value - 50) <= requirement.SafeHalfWidth)
                safeTicks++;

            var body = BuildBalanceMeter(
                value,
                requirement.SafeHalfWidth,
                i + 1,
                requirement.Ticks,
                requirement.MovementStep,
                playerDelta,
                driftDelta);
            renderer.Update(
                BuildBalanceMeterAgentConsoleFrame(
                    value,
                    requirement.SafeHalfWidth,
                    i + 1,
                    requirement.Ticks,
                    requirement.MovementStep,
                    playerDelta,
                    driftDelta),
                body);

            await clock.DelayAsync(requirement.TickMs);
        }

        var ratio = (double)safeTicks / requirement.Ticks;
        return ratio switch
        {
            >= 0.70 => "success",
            >= 0.45 => "partial",
            _ => "fail"
        };
    }

    private async Task<QteGrade> RunChargeReleaseAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        var targetStart = Math.Clamp(50 - (difficulty * 5) - (statTier * 2), 20, 70);
        var targetWidth = Math.Clamp(20 - (difficulty * 2) + (statTier * 2), 8, 26);
        var tickMs = Math.Clamp(85 - (statTier * 5) + (difficulty * 8), 40, 140);
        var charge = 0;
        var charging = false;

        return await RunMiniGameLiveAsync(
            "Накопление силы",
            $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, чтобы начать заряд.",
            BuildChargeReleaseLiveFrame(charge, targetStart, targetWidth, charging),
            async renderer =>
        {
            while (true)
            {
                renderer.Update(
                    "Накопление силы",
                    charging
                        ? $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)} ещё раз, чтобы отпустить заряд."
                        : $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, чтобы начать заряд.",
                    BuildChargeReleaseLiveFrame(charge, targetStart, targetWidth, charging));

                if (TryReadImmediateKey(out var key))
                {
                    if (key.Key == ConsoleKey.Escape)
                        return QteGrade.Fail;

                    if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.Spacebar))
                    {
                        if (!charging)
                        {
                            charging = true;
                        }
                        else
                        {
                            if (charge >= targetStart && charge <= targetStart + targetWidth)
                                return QteGrade.Success;
                            if (charge >= Math.Max(0, targetStart - 10) && charge <= Math.Min(100, targetStart + targetWidth + 10))
                                return QteGrade.Partial;
                            return QteGrade.Fail;
                        }
                    }
                }

                if (charging)
                {
                    charge += 4 + difficulty;
                    if (charge >= 100)
                        return QteGrade.Fail;
                }

                await Task.Delay(tickMs);
            }
        });
    }

    public Task<int> ResolveQteStatTierAsync(string characteristic) => ResolveStatTierAsync(characteristic);

    internal async Task<int> ResolveQteStatTierAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string characteristic)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        if (_hooks?.BeforeQteCharacteristicReadAsync != null)
            await _hooks.BeforeQteCharacteristicReadAsync();
        var modified = await TryReadQteCharacteristicAsync(
            writeLease,
            "game_state/player/computed_characteristics.json",
            "modifiedCharacteristics",
            characteristic);
        modified ??= await TryReadQteCharacteristicAsync(
            writeLease,
            "game_state/misc/characteristics.json",
            parentProperty: null,
            characteristic);
        return modified.HasValue ? ResolveStatTier(modified.Value) : 0;
    }

    private async Task<int> ResolveStatTierAsync(string characteristic)
    {
        try
        {
            var computed = await _charService.ComputeAsync();
            if (computed.Stats.TryGetValue(characteristic, out var stat))
            {
                return ResolveStatTier(stat.Modified);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось вычислить характеристику для QTE");
        }

        return 0;
    }

    private async Task<int?> TryReadQteCharacteristicAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath,
        string? parentProperty,
        string characteristic)
    {
        var json = await _fs.ReadFileAsync(writeLease, relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var values = string.IsNullOrWhiteSpace(parentProperty)
                ? root
                : root?[parentProperty] as JsonObject;
            return TryReadNodeInt(values?[characteristic], out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ResolveStatTier(int modified) =>
        modified switch
        {
            <= 10 => -2,
            <= 20 => -1,
            <= 40 => 0,
            <= 60 => 1,
            <= 80 => 2,
            _ => 3
        };

    private async Task<bool> ShowSceneImageAsync(string? imagePrompt, string qteId, string segmentId)
    {
        if (string.IsNullOrWhiteSpace(imagePrompt))
            return false;

        var imageKey = $"qte_{qteId}_{segmentId}";
        return await _imageService.GenerateSceneImageOnceAsync(imagePrompt, imageKey);
    }

    private Task AppendHistoryAsync(
        QteOffer offer,
        QteTerminalOutcome outcome,
        QteGrade grade,
        int acceptedAtTurn,
        int finishedAtTurn,
        string summary,
        QteScoreSummary? finalScore,
        IReadOnlyList<QteScoreAuditEntry>? scoreAudit) =>
        AppendHistoryAsync(
            writeLease: null,
            offer,
            outcome,
            grade,
            acceptedAtTurn,
            finishedAtTurn,
            summary,
            finalScore,
            scoreAudit);

    private async Task AppendHistoryAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteOffer offer,
        QteTerminalOutcome outcome,
        QteGrade grade,
        int acceptedAtTurn,
        int finishedAtTurn,
        string summary,
        QteScoreSummary? finalScore,
        IReadOnlyList<QteScoreAuditEntry>? scoreAudit)
    {
        var history = await LoadHistoryAsync(writeLease);
        history.Add(new QteHistoryEntry
        {
            QteId = offer.QteId,
            Title = offer.Title,
            AcceptedAtTurn = acceptedAtTurn,
            FinishedAtTurn = finishedAtTurn,
            OutcomeId = outcome.OutcomeId,
            Grade = grade.ToString().ToLowerInvariant(),
            Summary = summary,
            FinalScore = finalScore,
            ScoreAudit = scoreAudit is { Count: > 0 } ? scoreAudit.ToList() : null
        });

        await WriteCanonicalFileAtomicAsync(
            writeLease,
            QteHistoryPath,
            JsonSerializer.Serialize(history, JsonOpts));
        if (_hooks?.AfterHistoryWrittenAsync != null)
            await _hooks.AfterHistoryWrittenAsync();
    }

    private async Task<List<QteHistoryEntry>> LoadHistoryAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var json = await ReadCanonicalFileAsync(writeLease, QteHistoryPath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<QteHistoryEntry>();

        try
        {
            return JsonSerializer.Deserialize<List<QteHistoryEntry>>(json, JsonOpts) ?? new List<QteHistoryEntry>();
        }
        catch
        {
            return new List<QteHistoryEntry>();
        }
    }

    private async Task<QteRuntimeState> LoadRuntimeStateAsync(
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var json = await ReadCanonicalFileAsync(writeLease, QteRuntimePath);
        if (string.IsNullOrWhiteSpace(json))
            return new QteRuntimeState();

        try
        {
            return JsonSerializer.Deserialize<QteRuntimeState>(json, JsonOpts) ?? new QteRuntimeState();
        }
        catch
        {
            return new QteRuntimeState();
        }
    }

    private Task SaveRuntimeStateAsync(QteRuntimeState state) =>
        SaveRuntimeStateAsync(writeLease: null, state);

    private async Task SaveRuntimeStateAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        QteRuntimeState state)
    {
        if (_hooks?.BeforeRuntimeWriteAsync != null)
            await _hooks.BeforeRuntimeWriteAsync();
        await WriteCanonicalFileAtomicAsync(
            writeLease,
            QteRuntimePath,
            JsonSerializer.Serialize(state, JsonOpts));
        if (_hooks?.AfterRuntimeWrittenAsync != null)
            await _hooks.AfterRuntimeWrittenAsync(state);
    }

    private Task<string?> ReadCanonicalFileAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath) =>
        writeLease == null
            ? _fs.ReadFileAsync(relativePath)
            : _fs.ReadFileAsync(writeLease, relativePath);

    private Task WriteCanonicalFileAtomicAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath,
        string content) =>
        writeLease == null
            ? _fs.WriteFileAtomicAsync(relativePath, content)
            : _fs.WriteFileAtomicAsync(writeLease, relativePath, content);

    private bool CanonicalFileExists(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath) =>
        writeLease == null
            ? _fs.FileExists(relativePath)
            : _fs.FileExists(writeLease, relativePath);

    private void DeleteCanonicalFile(
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath)
    {
        if (writeLease == null)
            _fs.DeleteFile(relativePath);
        else
            _fs.DeleteFile(writeLease, relativePath);
    }

    private Task RefreshGameStateAsync(
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? _stateManager.RefreshGameStateAsync()
            : _stateManager.RefreshGameStateAsync(writeLease);

    private static bool HasMeaningfulRuntimeState(JsonObject root)
    {
        return root["pendingOffer"] is JsonObject ||
               root["activeScene"] is JsonObject ||
               (TryReadNodeString(root["lastDeclinedQteId"], out var declinedId) && !string.IsNullOrWhiteSpace(declinedId)) ||
               TryReadNodeInt(root["lastDeclinedAtTurn"], out _) ||
               (TryReadNodeString(root["lastResolvedQteSummaryPendingReminder"], out var reminder) && !string.IsNullOrWhiteSpace(reminder));
    }

    private static bool TryReadNodeString(JsonNode? node, out string? value)
    {
        value = null;
        if (node is null)
            return false;

        try
        {
            value = node.GetValue<string?>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadNodeInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is null)
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

    private static List<(string Label, T Value)> BuildUniqueOptions<T>(IEnumerable<T> values, Func<T, string> labelFactory)
        where T : class
    {
        var result = new List<(string Label, T Value)>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var baseLabel = labelFactory(value);
            counts.TryGetValue(baseLabel, out var count);
            count++;
            counts[baseLabel] = count;
            result.Add((count == 1 ? baseLabel : $"{baseLabel} #{count}", value));
        }

        return result;
    }

    private static string DisplayGrade(QteGrade grade) => grade switch
    {
        QteGrade.Success => "Успех",
        QteGrade.Partial => "Частичный успех",
        _ => "Провал"
    };

    private static QteGrade ParseGrade(string? grade) => grade?.ToLowerInvariant() switch
    {
        "success" => QteGrade.Success,
        "partial" => QteGrade.Partial,
        _ => QteGrade.Fail
    };

    private static void RenderMiniGamePanel(string title, string instructions, string body)
    {
        AnsiConsole.Write(BuildMiniGamePanel(title, instructions, body));
    }

    private async Task<T> RunMiniGameLiveAsync<T>(
        string title,
        string instructions,
        string initialBody,
        Func<IQteMiniGameLiveRenderer, Task<T>> runAsync)
    {
        var liveInput = _inputSource as AgentConsoleLiveInputSource;
        var renderer = new QteMiniGameLiveRenderer(title, instructions, initialBody, liveInput);
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            RenderMiniGamePanel(title, instructions, initialBody);
            return await runAsync(renderer);
        }

        return await AnsiConsole.Live(renderer.Renderable)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async context =>
            {
                renderer.Attach(context);
                context.Refresh();
                return await runAsync(renderer);
            });
    }

    private sealed class QteMiniGameLiveRenderer(
        string title,
        string instructions,
        string body,
        AgentConsoleLiveInputSource? liveInput) : IQteMiniGameLiveRenderer
    {
        private LiveDisplayContext? _context;
        private string _title = title;
        private string _instructions = instructions;
        private string _body = body;

        public Panel Renderable => BuildMiniGamePanel(_title, _instructions, _body);

        public void Attach(LiveDisplayContext context)
        {
            _context = context;
        }

        public void Update(string body)
        {
            Update(_title, _instructions, body);
        }

        public void Update(string title, string instructions, string body)
        {
            _title = title;
            _instructions = instructions;
            _body = body;
            PublishAgentConsoleQteFrame(BuildGenericAgentConsoleQteFrame(title, instructions, body), body);

            if (_context == null)
                return;

            _context.UpdateTarget(Renderable);
            _context.Refresh();
        }

        public void Update(AgentConsoleQteFrame qteFrame, string terminalBody)
        {
            _title = qteFrame.Title;
            _instructions = qteFrame.Instructions;
            _body = terminalBody;
            PublishAgentConsoleQteFrame(qteFrame, terminalBody);

            if (_context == null)
                return;

            _context.UpdateTarget(Renderable);
            _context.Refresh();
        }

        private void PublishAgentConsoleQteFrame(AgentConsoleQteFrame qteFrame, string terminalBody)
        {
            if (liveInput == null)
                return;

            var now = DateTimeOffset.UtcNow;
            var normalizedFrame = qteFrame with
            {
                BodyText = string.IsNullOrWhiteSpace(qteFrame.BodyText)
                    ? StripSpectreMarkup(terminalBody)
                    : qteFrame.BodyText
            };
            var snapshot = new AgentConsoleSnapshot
            {
                ScreenId = "qte-live-" + ToAgentConsoleScreenPart(normalizedFrame.QteId ?? normalizedFrame.Type),
                Mode = AgentConsoleMode.QteLive,
                Title = normalizedFrame.Title,
                PlainText = BuildAgentConsoleQtePlainText(normalizedFrame),
                AwaitingInput = normalizedFrame.AwaitingInputKind != AgentConsoleInputKind.None,
                InputKind = normalizedFrame.AwaitingInputKind,
                Actions = BuildAgentConsoleQteActions(normalizedFrame),
                Prompt = BuildAgentConsoleQtePrompt(normalizedFrame),
                QteFrame = normalizedFrame,
                RenderedAtUtc = now,
                UpdatedAtUtc = now,
                Diagnostics = []
            };

            liveInput.PublishSnapshot(snapshot, $"Rendered QTE frame {normalizedFrame.Type}.");
        }
    }

    private static AgentConsoleQteFrame BuildGenericAgentConsoleQteFrame(
        string title,
        string instructions,
        string terminalBody) =>
        new()
        {
            Type = InferQteFrameType(title),
            Title = title,
            Phase = "running",
            Instructions = instructions,
            BodyText = StripSpectreMarkup(terminalBody),
            AwaitingInputKind = AgentConsoleInputKind.Key
        };

    private static string InferQteFrameType(string title)
    {
        if (title.Contains("Полоса реакции", StringComparison.OrdinalIgnoreCase))
            return "TimingBar";
        if (title.Contains("Цепь знаков", StringComparison.OrdinalIgnoreCase))
            return "PromptChain";
        if (title.Contains("Равновесие", StringComparison.OrdinalIgnoreCase))
            return "BalanceMeter";
        if (title.Contains("Память рун", StringComparison.OrdinalIgnoreCase))
            return "PatternMemory";
        if (title.Contains("Накопление силы", StringComparison.OrdinalIgnoreCase))
            return "ChargeRelease";
        if (title.Contains("Тихий проход", StringComparison.OrdinalIgnoreCase))
            return "StealthNoise";
        if (title.Contains("Штифты", StringComparison.OrdinalIgnoreCase))
            return "LockPinSet";

        return "QteLive";
    }

    private static string BuildAgentConsoleQtePlainText(AgentConsoleQteFrame frame)
    {
        var lines = new List<string> { frame.Title };
        if (!string.IsNullOrWhiteSpace(frame.Instructions))
            lines.Add(frame.Instructions);
        if (!string.IsNullOrWhiteSpace(frame.BodyText))
            lines.Add(frame.BodyText);
        if (frame.RequiredInputs.Count > 0)
            lines.Add("Ожидаемый ввод: " + string.Join(", ", frame.RequiredInputs));
        if (frame.Choices.Count > 0)
            lines.Add("Варианты: " + string.Join(" | ", frame.Choices));
        if (frame.Feedback.Count > 0)
            lines.Add("Подсказка: " + string.Join(" | ", frame.Feedback));

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<AgentConsoleAction> BuildAgentConsoleQteActions(AgentConsoleQteFrame frame)
    {
        if (frame.Choices.Count > 0)
        {
            return frame.Choices.Select((choice, index) => new AgentConsoleAction
            {
                Id = $"choice-{index}",
                Label = choice,
                Shortcut = (index + 1).ToString(CultureInfo.InvariantCulture),
                IsDefault = index == 0
            }).ToArray();
        }

        if (frame.RequiredInputs.Count > 0)
        {
            return frame.RequiredInputs.Select(input => new AgentConsoleAction
            {
                Id = $"key-{input}",
                Label = input,
                Shortcut = input,
                IsDefault = frame.RequiredInputs.Count == 1
            }).ToArray();
        }

        return [];
    }

    private static AgentConsolePrompt? BuildAgentConsoleQtePrompt(AgentConsoleQteFrame frame)
    {
        if (frame.AwaitingInputKind == AgentConsoleInputKind.None)
            return null;

        return new AgentConsolePrompt
        {
            PromptId = $"qte-{ToAgentConsoleScreenPart(frame.Type)}",
            Text = frame.Instructions,
            InputKind = frame.AwaitingInputKind,
            Choices = frame.Choices
        };
    }

    private static Panel BuildMiniGamePanel(string title, string instructions, string body)
    {
        return new Panel(new Markup(string.Join("\n", new[]
        {
            $"[bold cyan]{Markup.Escape(title)}[/]",
            "",
            $"[white]{Markup.Escape(instructions)}[/]",
            $"[dim]{Markup.Escape(QteKeyInput.LayoutSupportNote)}[/]",
            "",
            body
        })))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static string BuildTimingBar(int width, int position, int successStart, int successWidth, int partialStart, int partialWidth)
    {
        var parts = new List<string>();
        for (var i = 0; i < width; i++)
        {
            if (i == position)
                parts.Add("[bold yellow]●[/]");
            else if (i >= successStart && i < successStart + successWidth)
                parts.Add("[green]█[/]");
            else if (i >= partialStart && i < partialStart + partialWidth)
                parts.Add("[yellow]▓[/]");
            else
                parts.Add("[dim]░[/]");
        }

        return string.Join("", parts);
    }

    private static string BuildTimingBarLiveFrame(TimingBarLiveRequirement requirement, int position, int remainingMs)
    {
        var remainingSeconds = remainingMs / 1000d;
        return string.Join("\n", new[]
        {
            BuildTimingBar(
                requirement.Width,
                position,
                requirement.SuccessStart,
                requirement.SuccessWidth,
                requirement.PartialStart,
                requirement.PartialWidth),
            $"[dim]Окно успеха: {requirement.SuccessWindowMs} мс | Осталось: {remainingSeconds:0.0} с[/]"
        });
    }

    private static AgentConsoleQteFrame BuildTimingBarAgentConsoleFrame(
        TimingBarLiveRequirement requirement,
        int position,
        int remainingMs)
    {
        var body = BuildTimingBarLiveFrame(requirement, position, remainingMs);
        return new AgentConsoleQteFrame
        {
            Type = "TimingBar",
            Title = "Полоса реакции",
            Phase = "running",
            Instructions = $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, когда маркер будет в центральной зоне.",
            BodyText = StripSpectreMarkup(body),
            AwaitingInputKind = AgentConsoleInputKind.Key,
            RequiredInputs = ["space"],
            RemainingMs = remainingMs,
            TimeoutMs = requirement.TimeoutMs,
            MarkerValue = position,
            MarkerMin = 0,
            MarkerMax = requirement.Width - 1,
            TargetStart = requirement.SuccessStart,
            TargetEnd = requirement.SuccessStart + requirement.SuccessWidth - 1,
            PartialStart = requirement.PartialStart,
            PartialEnd = requirement.PartialStart + requirement.PartialWidth - 1,
            Feedback = [DescribeTimingBarMarker(position, requirement)]
        };
    }

    private static string DescribeTimingBarMarker(int position, TimingBarLiveRequirement requirement)
    {
        if (position >= requirement.SuccessStart && position < requirement.SuccessStart + requirement.SuccessWidth)
            return "маркер в зоне успеха";
        if (position >= requirement.PartialStart && position < requirement.PartialStart + requirement.PartialWidth)
            return "маркер в зоне частичного успеха";
        if (position < requirement.PartialStart)
            return "маркер перед зоной";

        return "маркер после зоны";
    }

    private static string BuildPromptChainProgress(
        ConsoleKey prompt,
        int currentStep,
        int totalSteps,
        int mistakes,
        int allowedMistakes,
        int remainingMs)
    {
        var remainingSeconds = remainingMs / 1000d;
        return string.Join("\n", new[]
        {
            $"[white]Текущий знак: [bold yellow]{DisplayKey(prompt)}[/][/]",
            $"[dim]Шаг {currentStep}/{totalSteps} | Ошибки: {mistakes}/{allowedMistakes} | Осталось: {remainingSeconds:0.0} с[/]"
        });
    }

    private static AgentConsoleQteFrame BuildPromptChainAgentConsoleFrame(
        ConsoleKey prompt,
        int currentStep,
        int totalSteps,
        int mistakes,
        int allowedMistakes,
        int remainingMs,
        int timeoutMs)
    {
        var body = BuildPromptChainProgress(prompt, currentStep, totalSteps, mistakes, allowedMistakes, remainingMs);
        var token = QteKeyInput.NormalizeConsoleKey(prompt) ?? prompt.ToString().ToLowerInvariant();
        return new AgentConsoleQteFrame
        {
            Type = "PromptChain",
            Title = "Цепь знаков",
            Phase = "running",
            Instructions = "Нажимайте показанную физическую клавишу до истечения таймера.",
            BodyText = StripSpectreMarkup(body),
            AwaitingInputKind = AgentConsoleInputKind.Key,
            RequiredInputs = [token],
            RemainingMs = remainingMs,
            TimeoutMs = timeoutMs,
            ProgressValue = currentStep,
            ProgressMax = totalSteps,
            Feedback = [$"ошибки: {mistakes}/{allowedMistakes}"]
        };
    }

    internal static string BuildBalanceMeterLiveFrame(
        int value,
        int safeHalfWidth,
        int currentTick,
        int totalTicks,
        int movementStep,
        int? playerDelta = null,
        int? driftDelta = null) =>
        BuildBalanceMeter(value, safeHalfWidth, currentTick, totalTicks, movementStep, playerDelta, driftDelta);

    private static string BuildBalanceMeter(
        int value,
        int safeHalfWidth,
        int currentTick,
        int totalTicks,
        int movementStep,
        int? playerDelta = null,
        int? driftDelta = null)
    {
        var parts = new List<string>();
        for (var i = 0; i <= 100; i += 5)
        {
            if (Math.Abs(i - value) < 3)
                parts.Add("[bold yellow]●[/]");
            else if (Math.Abs(i - 50) <= safeHalfWidth)
                parts.Add("[green]█[/]");
            else
                parts.Add("[dim]░[/]");
        }

        var safeStart = Math.Max(0, 50 - safeHalfWidth);
        var safeEnd = Math.Min(100, 50 + safeHalfWidth);

        return string.Join("\n", new[]
        {
            string.Join("", parts),
            $"[white]Позиция: {value}/100 | цель: 50 | безопасная зона: {safeStart}-{safeEnd}[/]",
            $"[dim]{FormatCompactPhysicalKeyLabel(ConsoleKey.A)} или ←: влево на {movementStep} | {FormatCompactPhysicalKeyLabel(ConsoleKey.D)} или →: вправо на {movementStep}[/]",
            $"[dim]Игрок: {FormatBalancePlayerDelta(playerDelta)} | Помеха: {FormatSignedDelta(driftDelta)} | Итог: {FormatSignedDelta((playerDelta ?? 0) + (driftDelta ?? 0))}[/]",
            $"[dim]Шаг управления: {movementStep} | Такт {currentTick}/{totalTicks}[/]"
        });
    }

    private static AgentConsoleQteFrame BuildBalanceMeterAgentConsoleFrame(
        int value,
        int safeHalfWidth,
        int currentTick,
        int totalTicks,
        int movementStep,
        int? playerDelta,
        int? driftDelta)
    {
        var body = BuildBalanceMeter(value, safeHalfWidth, currentTick, totalTicks, movementStep, playerDelta, driftDelta);
        var safeStart = Math.Max(0, 50 - safeHalfWidth);
        var safeEnd = Math.Min(100, 50 + safeHalfWidth);
        return new AgentConsoleQteFrame
        {
            Type = "BalanceMeter",
            Title = "Равновесие",
            Phase = "running",
            Instructions = $"Удерживайте индикатор в центральной зоне: {FormatCompactPhysicalKeyLabel(ConsoleKey.A)} или ← влево, {FormatCompactPhysicalKeyLabel(ConsoleKey.D)} или → вправо.",
            BodyText = StripSpectreMarkup(body),
            AwaitingInputKind = AgentConsoleInputKind.Key,
            RequiredInputs = ["a", "d", "leftArrow", "rightArrow"],
            ProgressValue = currentTick,
            ProgressMax = totalTicks,
            MarkerValue = value,
            MarkerMin = 0,
            MarkerMax = 100,
            TargetStart = 50,
            TargetEnd = 50,
            SafeStart = safeStart,
            SafeEnd = safeEnd,
            Feedback =
            [
                $"игрок: {FormatBalancePlayerDelta(playerDelta)}",
                $"помеха: {FormatSignedDelta(driftDelta)}",
                $"шаг управления: {movementStep}"
            ]
        };
    }

    private static string FormatBalancePlayerDelta(int? delta) =>
        delta switch
        {
            null => "нет ввода",
            > 0 => $"вправо {FormatSignedDelta(delta)}",
            < 0 => $"влево {FormatSignedDelta(delta)}",
            _ => "без сдвига"
        };

    private static string FormatSignedDelta(int? delta)
    {
        var value = delta ?? 0;
        return value > 0
            ? $"+{value.ToString(CultureInfo.InvariantCulture)}"
            : value.ToString(CultureInfo.InvariantCulture);
    }

    internal static string BuildChargeReleaseLiveFrame(int charge, int targetStart, int targetWidth, bool charging)
    {
        const int step = 5;
        var targetEnd = Math.Min(100, targetStart + targetWidth);
        var parts = new List<string>();
        for (var i = 0; i <= 100; i += step)
        {
            if (i >= targetStart && i <= targetEnd)
                parts.Add(i <= charge ? "[bold green]█[/]" : "[green]▒[/]");
            else if (i <= charge)
                parts.Add("[cyan]█[/]");
            else
                parts.Add("[dim]·[/]");
        }

        var markerOffset = Math.Clamp((int)Math.Round(charge / (double)step), 0, parts.Count - 1);
        var marker = string.Concat(Enumerable.Repeat(" ", markerOffset)) + "[bold yellow]▲[/]";
        var instruction = charging
            ? $"[dim]Отпустите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)} в зелёном диапазоне.[/]"
            : $"[dim]Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, чтобы начать накопление.[/]";

        return string.Join("\n", new[]
        {
            $"[white]Заряд: {charge}/100[/]",
            string.Join("", parts),
            marker,
            $"[white]Целевая сила: {targetStart}-{targetEnd}[/]",
            instruction
        });
    }

    private static string BuildMashInputProgress(int matchedPresses, int successTarget, int partialTarget, int remainingMs)
    {
        const int width = 24;
        var filled = Math.Clamp((int)Math.Round(width * Math.Min(matchedPresses, successTarget) / (double)successTarget), 0, width);
        var bar = string.Concat(Enumerable.Range(0, width).Select(index =>
            index < filled ? "[green]█[/]" : "[dim]░[/]"));
        var remainingSeconds = remainingMs / 1000d;

        return string.Join("\n", new[]
        {
            bar,
            $"[white]Прогресс: {matchedPresses}/{successTarget}[/]",
            $"[dim]Частичный успех: {partialTarget} | Осталось: {remainingSeconds:0.0} с[/]"
        });
    }

    internal static string BuildPatternMemoryRevealFrame(IReadOnlyList<string> sequence, int remainingMs) =>
        BuildPatternMemoryReveal(sequence, remainingMs);

    private static string BuildPatternMemoryReveal(IReadOnlyList<string> sequence, int remainingMs)
    {
        var labels = FormatPatternMemorySequence(sequence);
        var remainingSeconds = remainingMs / 1000d;
        return string.Join("\n", new[]
        {
            $"[white]Показ: {labels}[/]",
            $"[dim]Запомните {sequence.Count} знака. До ввода: {remainingSeconds:0.0} с[/]"
        });
    }

    private static AgentConsoleQteFrame BuildPatternMemoryRevealAgentConsoleFrame(
        IReadOnlyList<string> sequence,
        int remainingMs,
        int timeoutMs)
    {
        var body = BuildPatternMemoryReveal(sequence, remainingMs);
        return new AgentConsoleQteFrame
        {
            Type = "PatternMemory",
            Title = "Память рун: фаза показа",
            Phase = "reveal",
            Instructions = "Запомните порядок знаков. Ввод начнётся после показа.",
            BodyText = StripSpectreMarkup(body),
            AwaitingInputKind = AgentConsoleInputKind.None,
            RequiredInputs = sequence,
            RemainingMs = remainingMs,
            TimeoutMs = timeoutMs,
            ProgressValue = 0,
            ProgressMax = sequence.Count
        };
    }

    internal static string BuildPatternMemoryInputLiveFrame(
        int sequenceLength,
        IReadOnlyList<ConsoleKeyInfo> inputs,
        int allowedMistakes,
        int remainingMs) =>
        BuildPatternMemoryInputProgress(sequenceLength, inputs, allowedMistakes, remainingMs);

    private static string BuildPatternMemoryInputProgress(
        int sequenceLength,
        IReadOnlyList<ConsoleKeyInfo> inputs,
        int allowedMistakes,
        int remainingMs)
    {
        var entered = inputs
            .Select(input => QteKeyInput.NormalizeConsoleInput(input))
            .Select(token => token == null ? "?" : QteKeyInput.FormatPromptLabel(token));
        var enteredText = inputs.Count == 0 ? "[dim]пока нет ввода[/]" : Markup.Escape(string.Join("  ", entered));
        var remainingSeconds = remainingMs / 1000d;

        return string.Join("\n", new[]
        {
            $"[white]Введено: {enteredText}[/]",
            $"[dim]Шаг {Math.Min(inputs.Count, sequenceLength)}/{sequenceLength} | Ошибок можно: {allowedMistakes} | Осталось: {remainingSeconds:0.0} с[/]"
        });
    }

    private static AgentConsoleQteFrame BuildPatternMemoryInputAgentConsoleFrame(
        int sequenceLength,
        IReadOnlyList<ConsoleKeyInfo> inputs,
        int allowedMistakes,
        int remainingMs,
        int timeoutMs)
    {
        var inputBuffer = inputs
            .Select(QteKeyInput.NormalizeConsoleInput)
            .Select(token => token ?? "?")
            .ToArray();
        var body = BuildPatternMemoryInputProgress(sequenceLength, inputs, allowedMistakes, remainingMs);
        return new AgentConsoleQteFrame
        {
            Type = "PatternMemory",
            Title = "Память рун: фаза ввода",
            Phase = "input",
            Instructions = "Повторите показанную последовательность по памяти теми же физическими клавишами.",
            BodyText = StripSpectreMarkup(body),
            AwaitingInputKind = AgentConsoleInputKind.Key,
            RequiredInputs = QteKeyInput.SupportedTokens,
            InputBuffer = inputBuffer,
            RemainingMs = remainingMs,
            TimeoutMs = timeoutMs,
            ProgressValue = Math.Min(inputs.Count, sequenceLength),
            ProgressMax = sequenceLength,
            Feedback = [$"ошибок можно: {allowedMistakes}"]
        };
    }

    private static string BuildRhythmPulseProgress(
        IReadOnlyList<int> pulseOffsetsMs,
        int hitWindowMs,
        int allowedMisses,
        IReadOnlyList<RhythmPulseInput> inputs,
        int elapsedMs,
        int remainingMs)
    {
        const int width = 32;
        var totalDurationMs = pulseOffsetsMs.Count == 0
            ? 1
            : pulseOffsetsMs[^1] + hitWindowMs;
        var markerPosition = Math.Clamp(
            (int)Math.Round((width - 1) * elapsedMs / (double)Math.Max(1, totalDurationMs)),
            0,
            width - 1);
        var hitCount = CountRhythmPulseHits(pulseOffsetsMs, hitWindowMs, inputs);
        var currentPulse = pulseOffsetsMs.Count(offset => offset + hitWindowMs < elapsedMs) + 1;
        currentPulse = Math.Clamp(currentPulse, 1, Math.Max(1, pulseOffsetsMs.Count));
        var track = new StringBuilder(width);
        for (var i = 0; i < width; i++)
        {
            if (i == markerPosition)
            {
                track.Append("[bold yellow]●[/]");
                continue;
            }

            var isPulse = pulseOffsetsMs.Any(offset =>
                Math.Abs(i - (int)Math.Round((width - 1) * offset / (double)Math.Max(1, totalDurationMs))) <= 0);
            track.Append(isPulse ? "[cyan]│[/]" : "[dim]░[/]");
        }

        var misses = Math.Max(0, pulseOffsetsMs.Count - hitCount);
        var remainingSeconds = remainingMs / 1000d;
        return string.Join("\n", new[]
        {
            track.ToString(),
            $"[white]Пульс: {currentPulse}/{pulseOffsetsMs.Count} | Попадания: {hitCount} | Промахи: {misses}[/]",
            $"[dim]Окно: ±{hitWindowMs} мс | Допустимые промахи: {allowedMisses} | Осталось: {remainingSeconds:0.0} с[/]",
            "[dim]Смотрите на вспышку дорожки; звук не обязателен для прохождения.[/]"
        });
    }

    private static string BuildPrecisionChoiceProgress(
        IReadOnlyList<PrecisionChoiceDisplayChoice> choices,
        IReadOnlyList<PrecisionChoiceDecoyHint> decoyHints,
        int revealedHintCount,
        int remainingMs)
    {
        var lines = new List<string>();
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            lines.Add($"[bold cyan]{index + 1}.[/] [white]{Markup.Escape(choice.Label)}[/]");
            if (!string.IsNullOrWhiteSpace(choice.Description))
                lines.Add($"   [dim]{Markup.Escape(choice.Description)}[/]");
        }

        var availableHints = choices
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Hint))
            .Select(choice => choice.Hint!)
            .Concat(decoyHints.Select(hint => hint.Hint))
            .ToArray();
        var revealedHints = availableHints
            .Take(Math.Clamp(revealedHintCount, 0, availableHints.Length))
            .ToArray();
        if (revealedHints.Length > 0)
        {
            lines.Add("");
            lines.Add("[yellow]Наблюдения:[/]");
            foreach (var hint in revealedHints)
                lines.Add($"[dim]- {Markup.Escape(hint)}[/]");
        }

        lines.Add("");
        lines.Add($"[dim]Осталось: {remainingMs / 1000d:0.0} с | Введите 1-{choices.Count}[/]");
        return string.Join("\n", lines);
    }

    private static string BuildStealthNoiseProgress(
        StealthNoiseEffectiveRequirement effective,
        IReadOnlyList<StealthNoiseInput> inputs,
        int elapsedMs,
        int remainingMs,
        string? warningLabel)
    {
        const int width = 24;
        var sample = SimulateStealthNoise(effective, inputs, elapsedMs);
        var filled = Math.Clamp(
            (int)Math.Round(width * sample.Noise / StealthNoiseMaxMeterValue),
            0,
            width);
        var thresholdIndex = Math.Clamp(
            (int)Math.Round((width - 1) * effective.DangerThreshold / StealthNoiseMaxMeterValue),
            0,
            width - 1);
        var parts = new List<string>(width);
        for (var index = 0; index < width; index++)
        {
            if (index == thresholdIndex)
            {
                parts.Add("[bold red]│[/]");
                continue;
            }

            if (index < filled)
                parts.Add(sample.Noise > effective.DangerThreshold ? "[red]█[/]" : "[cyan]█[/]");
            else
                parts.Add("[dim]░[/]");
        }

        var remainingSeconds = remainingMs / 1000d;
        var lines = new List<string>
        {
            string.Join("", parts),
            $"[white]Шум: {sample.Noise:0.0}/100 | Опасный порог: {effective.DangerThreshold:0.0}[/]",
            $"[dim]Осталось: {remainingSeconds:0.0} с | Над порогом: {sample.OverThresholdMs}/{effective.AllowedOverThresholdMs} мс | Сбросов: {inputs.Count}[/]"
        };

        if (sample.Noise > effective.DangerThreshold)
        {
            var warning = string.IsNullOrWhiteSpace(warningLabel)
                ? "Шум выше опасного порога."
                : warningLabel;
            lines.Add($"[bold red]{Markup.Escape(warning)}[/]");
        }

        return string.Join("\n", lines);
    }

    private static string BuildLockPinSetProgress(
        LockPinSetEffectiveRequirement effective,
        IReadOnlyList<bool> opened,
        int currentPinIndex,
        double currentPosition,
        int mistakes,
        int durabilityRemaining,
        int remainingMs,
        string pinLabel,
        string? warningLabel)
    {
        var lines = new List<string>();
        for (var index = 0; index < effective.PinWindows.Count; index++)
        {
            var window = effective.PinWindows[index];
            var label = string.IsNullOrWhiteSpace(window.Label)
                ? $"{pinLabel} {index + 1}"
                : window.Label;
            var isCurrent = index == currentPinIndex;
            var state = opened[index]
                ? "[green]открыт[/]"
                : isCurrent
                    ? "[yellow]выставляется[/]"
                    : "[dim]ожидает[/]";
            var position = isCurrent ? currentPosition : (window.Min + window.Max) / 2d;
            var inWindow = position >= window.Min && position <= window.Max;
            var marker = isCurrent
                ? inWindow ? "[green]в окне[/]" : "[red]мимо окна[/]"
                : "[dim]целевое окно[/]";
            lines.Add(
                $"[white]{Markup.Escape(label)}[/]: {state} | позиция {position:0.0} | окно {window.Min:0.0}..{window.Max:0.0} | {marker}");
        }

        lines.Add("");
        lines.Add($"[dim]Осталось: {remainingMs / 1000d:0.0} с | Ошибки: {mistakes}/{effective.MaxMistakes} | Прочность отмычки: {durabilityRemaining}/{effective.PickDurability}[/]");
        lines.Add($"[dim]Дрейф штифтов: {effective.PinDriftPerSecond:0.0}/с | Чистый успех до {effective.GradeThresholds.SuccessMaxTimeMs / 1000d:0.0} с без ошибок; частичный до {effective.GradeThresholds.PartialMaxTimeMs / 1000d:0.0} с.[/]");

        if (durabilityRemaining <= Math.Max(1, effective.PickDurability / 2) || mistakes > 0)
        {
            var warning = string.IsNullOrWhiteSpace(warningLabel)
                ? "Отмычка и замок уже выдали шум или сопротивление."
                : warningLabel;
            lines.Add($"[bold red]{Markup.Escape(warning)}[/]");
        }

        return string.Join("\n", lines);
    }

    private static string FormatPatternMemorySequence(IEnumerable<string> sequence) =>
        Markup.Escape(string.Join("  ", sequence.Select(QteKeyInput.FormatPromptLabel)));

    private static string FormatMashInputKeyLabels(IEnumerable<string> acceptedTokens) =>
        string.Join(" или ", acceptedTokens.Select(QteKeyInput.FormatPromptLabel));

    private static string FormatCompactPhysicalKeyLabel(ConsoleKey key) =>
        QteKeyInput.FormatPromptLabel(key).Replace(" / ", "/", StringComparison.Ordinal);

    private bool TryReadImmediateKey(out ConsoleKeyInfo key)
    {
        return TryReadImmediateKey(_inputSource, out key);
    }

    private static bool TryReadImmediateKey(IConsoleInputSource inputSource, out ConsoleKeyInfo key)
    {
        key = default;
        if (!inputSource.KeyAvailable)
            return false;

        key = inputSource.ReadKey(intercept: true);
        return true;
    }

    private async Task<ConsoleKeyInfo?> ReadKeyWithTimeoutAsync(int timeoutMs)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (_inputSource.KeyAvailable)
                return _inputSource.ReadKey(intercept: true);
            await Task.Delay(20);
        }

        return null;
    }

    private static bool TryGetPrecisionChoiceIndex(ConsoleKeyInfo key, int choiceCount, out int index)
    {
        index = -1;
        var digit = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            _ => char.IsDigit(key.KeyChar) ? key.KeyChar - '0' : 0
        };

        if (digit <= 0 || digit > choiceCount)
            return false;

        index = digit - 1;
        return true;
    }

    private static string DisplayKey(ConsoleKey key) => QteKeyInput.FormatPromptLabel(key);

    private static string? GetConfigString(JsonObject? config, string propertyName)
    {
        if (config == null || config[propertyName] is not JsonValue value)
            return null;

        if (value.TryGetValue<string>(out var str))
            return str;

        return value.ToJsonString().Trim('"');
    }

    public sealed class QteOffer
    {
        [JsonPropertyName("qteId")]
        public string QteId { get; set; } = "";

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("offerText")]
        public string? OfferText { get; set; }

        [JsonPropertyName("introNarrative")]
        public string? IntroNarrative { get; set; }

        [JsonPropertyName("declineHint")]
        public string? DeclineHint { get; set; }

        [JsonPropertyName("cinematicJustification")]
        public string? CinematicJustification { get; set; }

        [JsonPropertyName("sceneImagePrompt")]
        public string? SceneImagePrompt { get; set; }

        [JsonPropertyName("startChapterId")]
        public string StartChapterId { get; set; } = "";

        [JsonPropertyName("chapters")]
        public List<QteChapter> Chapters { get; set; } = new();

        [JsonPropertyName("terminalOutcomes")]
        public List<QteTerminalOutcome> TerminalOutcomes { get; set; } = new();

        [JsonPropertyName("scoreModel")]
        public QteScoreModel? ScoreModel { get; set; }
    }

    public sealed class QteChapter
    {
        [JsonPropertyName("chapterId")]
        public string ChapterId { get; set; } = "";

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("narrative")]
        public string? Narrative { get; set; }

        [JsonPropertyName("chapterImagePrompt")]
        public string? ChapterImagePrompt { get; set; }

        [JsonPropertyName("actions")]
        public List<QteAction> Actions { get; set; } = new();
    }

    public sealed class QteAction
    {
        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("check")]
        public QteCheck Check { get; set; } = new();

        [JsonPropertyName("routing")]
        public QteRouting Routing { get; set; } = new();

        [JsonPropertyName("successText")]
        public string? SuccessText { get; set; }

        [JsonPropertyName("partialText")]
        public string? PartialText { get; set; }

        [JsonPropertyName("failText")]
        public string? FailText { get; set; }

        [JsonPropertyName("scoreDeltas")]
        public Dictionary<string, List<QteScoreDelta>>? ScoreDeltas { get; set; }
    }

    public sealed class QteCheck
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "TimingBar";

        [JsonPropertyName("baseDifficulty")]
        public int BaseDifficulty { get; set; } = 1;

        [JsonPropertyName("primaryCharacteristic")]
        public string PrimaryCharacteristic { get; set; } = Characteristics.Dexterity;

        [JsonPropertyName("config")]
        public JsonObject? Config { get; set; }
    }

    public sealed class QteRouting
    {
        [JsonPropertyName("success")]
        public QteBranchTarget Success { get; set; } = new();

        [JsonPropertyName("partial")]
        public QteBranchTarget Partial { get; set; } = new();

        [JsonPropertyName("fail")]
        public QteBranchTarget Fail { get; set; } = new();
    }

    public sealed class QteBranchTarget
    {
        [JsonPropertyName("nextChapterId")]
        public string? NextChapterId { get; set; }

        [JsonPropertyName("terminalOutcomeId")]
        public string? TerminalOutcomeId { get; set; }
    }

    public sealed class QteTerminalOutcome
    {
        [JsonPropertyName("outcomeId")]
        public string OutcomeId { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("finalNarrative")]
        public string FinalNarrative { get; set; } = "";

        [JsonPropertyName("gmSummary")]
        public string GmSummary { get; set; } = "";

        [JsonPropertyName("outcomeImagePrompt")]
        public string? OutcomeImagePrompt { get; set; }

        [JsonPropertyName("responseFragment")]
        public JsonObject? ResponseFragment { get; set; }
    }

    public sealed class QteScoreModel
    {
        [JsonPropertyName("metrics")]
        public List<QteScoreMetricDefinition> Metrics { get; set; } = new();

        [JsonPropertyName("rankOrder")]
        public List<string>? RankOrder { get; set; }

        [JsonPropertyName("ranks")]
        public List<QteScoreRankDefinition> Ranks { get; set; } = new();
    }

    public sealed class QteScoreMetricDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("initial")]
        public double Initial { get; set; }

        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "always";
    }

    public sealed class QteScoreRankDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("allOf")]
        public List<QteScoreThreshold> AllOf { get; set; } = new();

        [JsonPropertyName("fallback")]
        public bool Fallback { get; set; }
    }

    public sealed class QteScoreThreshold
    {
        [JsonPropertyName("metric")]
        public string Metric { get; set; } = "";

        [JsonPropertyName("op")]
        public string Op { get; set; } = "";

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    public sealed class QteScoreDelta
    {
        [JsonPropertyName("metric")]
        public string Metric { get; set; } = "";

        [JsonPropertyName("delta")]
        public double Delta { get; set; }
    }

    public sealed class QteScoreState
    {
        [JsonPropertyName("metrics")]
        public List<QteScoreMetricState> Metrics { get; set; } = new();

        [JsonPropertyName("audit")]
        public List<QteScoreAuditEntry> Audit { get; set; } = new();
    }

    public sealed class QteScoreMetricState
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "always";
    }

    public sealed class QteScoreAuditEntry
    {
        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = "";

        [JsonPropertyName("actionLabel")]
        public string? ActionLabel { get; set; }

        [JsonPropertyName("grade")]
        public string Grade { get; set; } = "";

        [JsonPropertyName("metric")]
        public string Metric { get; set; } = "";

        [JsonPropertyName("metricLabel")]
        public string? MetricLabel { get; set; }

        [JsonPropertyName("previousValue")]
        public double PreviousValue { get; set; }

        [JsonPropertyName("delta")]
        public double Delta { get; set; }

        [JsonPropertyName("newValue")]
        public double NewValue { get; set; }
    }

    public sealed class QteScoreSummary
    {
        [JsonPropertyName("rank")]
        public QteScoreRankSummary? Rank { get; set; }

        [JsonPropertyName("metrics")]
        public List<QteScoreMetricState> Metrics { get; set; } = new();
    }

    public sealed class QteScoreRankSummary
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }

    public sealed class QteRuntimeState
    {
        [JsonPropertyName("pendingOffer")]
        public QteOffer? PendingOffer { get; set; }

        [JsonPropertyName("activeScene")]
        public ActiveQteSceneState? ActiveScene { get; set; }

        [JsonPropertyName("lastDeclinedQteId")]
        public string? LastDeclinedQteId { get; set; }

        [JsonPropertyName("lastDeclinedAtTurn")]
        public int? LastDeclinedAtTurn { get; set; }

        [JsonPropertyName("lastResolvedQteSummaryPendingReminder")]
        public string? LastResolvedQteSummaryPendingReminder { get; set; }
    }

    public sealed class ActiveQteSceneState
    {
        [JsonPropertyName("offer")]
        public QteOffer? Offer { get; set; }

        [JsonPropertyName("currentChapterId")]
        public string CurrentChapterId { get; set; } = "";

        [JsonPropertyName("acceptedAtTurn")]
        public int AcceptedAtTurn { get; set; }

        [JsonPropertyName("scoreState")]
        public QteScoreState? ScoreState { get; set; }
    }

    public sealed class QteHistoryEntry
    {
        [JsonPropertyName("qteId")]
        public string QteId { get; set; } = "";

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("acceptedAtTurn")]
        public int AcceptedAtTurn { get; set; }

        [JsonPropertyName("finishedAtTurn")]
        public int FinishedAtTurn { get; set; }

        [JsonPropertyName("outcomeId")]
        public string OutcomeId { get; set; } = "";

        [JsonPropertyName("grade")]
        public string Grade { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("finalScore")]
        public QteScoreSummary? FinalScore { get; set; }

        [JsonPropertyName("scoreAudit")]
        public List<QteScoreAuditEntry>? ScoreAudit { get; set; }
    }

    public sealed class QteSceneCompletion
    {
        public string QteId { get; set; } = "";
        public string OutcomeId { get; set; } = "";
        public string Summary { get; set; } = "";
        public GameResponse Response { get; set; } = new();
        public QteScoreSummary? ScoreSummary { get; set; }
    }

    public sealed class QteActionResolution
    {
        public string State { get; set; } = "Active";
        public string QteId { get; set; } = "";
        public string ChapterId { get; set; } = "";
        public string ActionId { get; set; } = "";
        public string Grade { get; set; } = "";
        public string ResultText { get; set; } = "";
        public string? NextChapterId { get; set; }
        public QteSceneCompletion? Completion { get; set; }
    }

    public enum QteOfferDecision
    {
        Accept,
        Decline
    }

    private enum QteGrade
    {
        Success,
        Partial,
        Fail
    }
}
