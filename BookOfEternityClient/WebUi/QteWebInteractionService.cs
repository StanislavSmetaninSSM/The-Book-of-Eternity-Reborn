using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed record QteWebOfferDecisionRequest(
    string? Decision,
    string? InteractionToken = null);

public sealed record QteWebActionRequest(
    string? ActionId,
    string? Grade,
    string? InteractionToken = null);

public sealed record QtePracticeStartRequest(
    string? TypeId,
    string? DifficultyId,
    string? InteractionToken = null);

public sealed record QtePracticeActionRequest(
    string? ActionId,
    string? Grade,
    string? InteractionToken = null);

public sealed record DarenShowcaseActionRequest(
    string? ActionId,
    string? Grade,
    string? InteractionToken = null);

public sealed record QteInteractionRequest(string? InteractionToken);

public sealed class QteWebInteractionService
{
    private static readonly string[] GradeOptions = ["success", "partial", "fail"];
    private const string PracticeLocalScoreNotice =
        "Тренировочный счёт остаётся только в этой попытке: без наград, опыта, предметов, достижений, Ink Feathers и прогресса.";

    private readonly FileSystemManager _fs;
    private readonly QteSceneService _qteSceneService;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly QteInteractionTokenAuthority _interactionTokens;
    private QteSceneService.QtePracticeAttemptState? _practiceAttempt;
    private string? _practiceAttemptGeneration;
    private string? _practiceAttemptIdentity;
    private QteSceneService.DarenShowcaseAttemptState? _darenAttempt;
    private string? _darenAttemptGeneration;
    private string? _darenAttemptIdentity;

    public QteWebInteractionService(
        FileSystemManager fs,
        QteSceneService qteSceneService,
        BrowserLocalWriteCoordinator coordinator)
    {
        _fs = fs;
        _qteSceneService = qteSceneService;
        _coordinator = coordinator;
        _interactionTokens = new QteInteractionTokenAuthority(fs);
    }

    public async Task<QteWebStateDto> BuildStateAsync(
        string? stateOverride = null,
        QteSceneService.QteActionResolution? resolution = null,
        string? notification = null)
    {
        try
        {
            return await _coordinator.RunBoundTransactionAsync(
                async writeLease =>
                {
                    QteWebStateDto? state = null;
                    var result = await _coordinator.ExecuteAtomicWithinTransactionAsync(
                        writeLease,
                        BuildWriteRequest("QTE runtime normalization"),
                        QteSceneService.BrowserTransactionRollbackPaths,
                        async transactionLease =>
                        {
                            state = await BuildStateCoreAsync(
                                transactionLease,
                                normalizeRuntime: true,
                                stateOverride,
                                resolution,
                                notification);
                        });

                    return result.Success
                        ? state ?? Failed("QTE state transaction completed without a state.")
                        : Failed(result.Message);
                });
        }
        catch (SessionReplacedException)
        {
            return Failed("Игровая сессия изменилась во время подготовки QTE. Повторите действие.");
        }
    }

    public async Task<QteWebStateDto> BuildReadOnlyStateAsync(
        string? stateOverride = null,
        QteSceneService.QteActionResolution? resolution = null,
        string? notification = null) =>
        await _coordinator.RunBoundTransactionAsync(
            writeLease => BuildStateCoreAsync(
                writeLease,
                normalizeRuntime: false,
                stateOverride,
                resolution,
                notification));

    private async Task<QteWebStateDto> BuildStateCoreAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        bool normalizeRuntime,
        string? stateOverride,
        QteSceneService.QteActionResolution? resolution,
        string? notification)
    {
        if (normalizeRuntime)
            await _qteSceneService.EnsureRuntimeStateHealthyAsync(writeLease);

        var offer = await _qteSceneService.TryReadOfferAsync(writeLease);
        if (offer != null)
        {
            var offerIdentity = Fingerprint(offer);
            return new QteWebStateDto
            {
                State = stateOverride ?? "Offer",
                Offer = BuildOffer(offer),
                AvailableOperations = ["accept", "decline"],
                InteractionToken = _interactionTokens.Publish(
                    writeLease,
                    "offer",
                    offerIdentity,
                    offerIdentity),
                Notification = notification
            };
        }

        var runtime = await _qteSceneService.ReadRuntimeStateAsync(writeLease);
        if (runtime.ActiveScene is { Offer: not null } active)
        {
            var offerIdentity = Fingerprint(active.Offer);
            return new QteWebStateDto
            {
                State = stateOverride ?? "Active",
                ActiveScene = await BuildActiveSceneAsync(active, writeLease),
                Resolution = resolution == null ? null : BuildResolution(resolution),
                Completion = resolution?.Completion == null ? null : BuildCompletion(resolution.Completion),
                AvailableOperations = ["submitAction"],
                InteractionToken = _interactionTokens.Publish(
                    writeLease,
                    "offer",
                    offerIdentity,
                    Fingerprint(active)),
                Notification = notification
            };
        }

        return new QteWebStateDto
        {
            State = stateOverride ?? (resolution?.Completion == null ? "NoScene" : "Completed"),
            Resolution = resolution == null ? null : BuildResolution(resolution),
            Completion = resolution?.Completion == null ? null : BuildCompletion(resolution.Completion),
            LastResolvedReminder = runtime.LastResolvedQteSummaryPendingReminder,
            LastDeclinedQteId = runtime.LastDeclinedQteId,
            AvailableOperations = [],
            Notification = notification
        };
    }

    public async Task<QteWebStateDto> ResolveOfferDecisionAsync(QteWebOfferDecisionRequest? request)
    {
        try
        {
            return await _coordinator.RunBoundTransactionAsync(
                writeLease => ResolveOfferDecisionBoundAsync(writeLease, request));
        }
        catch (SessionReplacedException)
        {
            return Failed("Игровая сессия изменилась во время обработки QTE. Повторите действие.");
        }
    }

    private async Task<QteWebStateDto> ResolveOfferDecisionBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        QteWebOfferDecisionRequest? request)
    {
        var tokenValidation = _interactionTokens.ValidatePresented(
            writeLease,
            "offer",
            request?.InteractionToken);
        if (!tokenValidation.IsValid)
            return Failed(tokenValidation);

        var currentOffer = await _qteSceneService.TryReadOfferAsync(writeLease);
        if (currentOffer == null)
        {
            return Failed(QteInteractionTokenAuthority.Validation.Stale(
                "Предложение QTE уже недоступно. Обновите страницу."));
        }

        var offerIdentity = Fingerprint(currentOffer);
        tokenValidation = QteInteractionTokenAuthority.ValidateCurrentState(
            tokenValidation,
            offerIdentity,
            offerIdentity);
        if (!tokenValidation.IsValid)
            return Failed(tokenValidation);

        if (currentOffer.SourceTurnNumber is not > 0)
            return Failed("QTE offer has no positive source turn authority.");

        var decision = request?.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("accept" or "decline"))
            return Failed("decision must be accept or decline.");

        QteWebStateDto? state = null;
        var result = await _coordinator.ExecuteAtomicWithinTransactionAsync(
            writeLease,
            BuildWriteRequest($"QTE offer: {decision}"),
            QteSceneService.BrowserTransactionRollbackPaths,
            async transactionLease =>
            {
                var offer = await _qteSceneService.TryReadOfferAsync(transactionLease);
                if (offer == null)
                    throw new InvalidOperationException("No pending QTE offer is available.");

                if (offer.SourceTurnNumber is not > 0)
                {
                    throw new InvalidOperationException(
                        "QTE offer has no positive source turn authority.");
                }

                var turnNumber = offer.SourceTurnNumber.Value;
                if (decision == "decline")
                {
                    await _qteSceneService.RecordDeclineAsync(
                        transactionLease,
                        offer,
                        turnNumber);
                    DeleteIfExists(transactionLease, "ready/turn_complete.json");
                    DeleteIfExists(transactionLease, "ready/turn_error.json");
                    state = await BuildStateCoreAsync(
                        transactionLease,
                        normalizeRuntime: false,
                        stateOverride: "Declined",
                        resolution: null,
                        notification: "QTE отклонено. Консольный режим повторно отправляет исходное действие GM; браузерный протокол фиксирует отказ и очищает offer.");
                    return;
                }

                await _qteSceneService.BeginAcceptedSceneAsync(
                    transactionLease,
                    offer,
                    turnNumber);
                state = await BuildStateCoreAsync(
                    transactionLease,
                    normalizeRuntime: false,
                    stateOverride: "Active",
                    resolution: null,
                    notification: "QTE принято. Выберите действие текущей сцены.");
            });

        if (!result.Success)
        {
            return Failed(result.Message);
        }

        return state ?? Failed("QTE offer transaction completed without a state.");
    }

    public async Task<QteWebStateDto> ResolveActionAsync(QteWebActionRequest? request)
    {
        try
        {
            return await _coordinator.RunBoundTransactionAsync(
                writeLease => ResolveActionBoundAsync(writeLease, request));
        }
        catch (SessionReplacedException)
        {
            return Failed("Игровая сессия изменилась во время обработки QTE. Повторите действие.");
        }
    }

    private async Task<QteWebStateDto> ResolveActionBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        QteWebActionRequest? request)
    {
        var tokenValidation = _interactionTokens.ValidatePresented(
            writeLease,
            "offer",
            request?.InteractionToken);
        if (!tokenValidation.IsValid)
            return Failed(tokenValidation);

        var currentRuntime =
            await _qteSceneService.ReadRuntimeStateAsync(writeLease);
        if (currentRuntime.ActiveScene is not { Offer: not null } activeScene)
        {
            return Failed(QteInteractionTokenAuthority.Validation.Stale(
                "Активная сцена QTE уже недоступна. Обновите страницу."));
        }

        tokenValidation = QteInteractionTokenAuthority.ValidateCurrentState(
            tokenValidation,
            Fingerprint(activeScene.Offer),
            Fingerprint(activeScene));
        if (!tokenValidation.IsValid)
            return Failed(tokenValidation);

        if (activeScene.AcceptedAtTurn <= 0 ||
            activeScene.Offer.SourceTurnNumber != activeScene.AcceptedAtTurn)
        {
            return Failed(
                "Active QTE scene has no matching positive turn authority.");
        }

        var actionId = request?.ActionId?.Trim();
        if (string.IsNullOrWhiteSpace(actionId))
            return Failed("actionId is required.");

        QteWebStateDto? state = null;
        var result = await _coordinator.ExecuteAtomicWithinTransactionAsync(
            writeLease,
            BuildWriteRequest("QTE action"),
            QteSceneService.BrowserTransactionRollbackPaths,
            async transactionLease =>
            {
                var resolution = await _qteSceneService.ResolveActiveActionAsync(
                    transactionLease,
                    actionId,
                    request?.Grade,
                    activeScene.AcceptedAtTurn,
                    allowPreexistingStateIssues: true);

                state = await BuildStateCoreAsync(
                    transactionLease,
                    normalizeRuntime: false,
                    resolution.State,
                    resolution,
                    "QTE action resolved.");
            },
            prepareAfterRollback: _qteSceneService.PrepareStateManagerRollback);

        if (!result.Success)
        {
            return Failed(result.Message);
        }

        return state ?? Failed("QTE action transaction completed without a state.");
    }

    public Task<QtePracticeWebStateDto> BuildPracticeStateAsync() =>
        _coordinator.RunBoundTransactionAsync(
            writeLease =>
            {
                var discardedStaleAttempt = DiscardStalePracticeAttempt(writeLease);
                return BuildPracticeStateCoreAsync(
                    writeLease,
                    notification: discardedStaleAttempt
                        ? "Предыдущая тренировка закрыта после смены игровой сессии."
                        : null,
                    error: null);
            });

    public Task<DarenShowcaseWebStateDto> BuildDarenShowcaseStateAsync() =>
        _coordinator.RunBoundTransactionAsync(
            writeLease =>
            {
                var discardedStaleAttempt = DiscardStaleDarenAttempt(writeLease);
                return BuildDarenShowcaseStateCoreAsync(
                    writeLease,
                    notification: discardedStaleAttempt
                        ? "Предыдущая вылазка закрыта после смены игровой сессии."
                        : null,
                    error: null);
            });

    public async Task<QtePracticeWebStateDto> StartPracticeAttemptAsync(QtePracticeStartRequest? request)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidatePracticeInteraction(
                    writeLease,
                    request?.InteractionToken);
                if (!tokenValidation.IsValid)
                    return FailedPractice(tokenValidation);

                DiscardStalePracticeAttempt(writeLease);
                var attemptBefore = _practiceAttempt == null
                    ? null
                    : ClonePracticeAttempt(_practiceAttempt);
                var generationBefore = _practiceAttemptGeneration;
                var identityBefore = _practiceAttemptIdentity;
                try
                {
                    _practiceAttemptGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
                    _practiceAttempt = _qteSceneService.StartPracticeAttempt(
                        request?.TypeId,
                        request?.DifficultyId);
                    _practiceAttemptIdentity = Guid.NewGuid().ToString("N");
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        "Тренировка началась.",
                        error: null);
                }
                catch (Exception ex)
                {
                    _practiceAttempt = attemptBefore;
                    _practiceAttemptGeneration = generationBefore;
                    _practiceAttemptIdentity = identityBefore;
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: ex.Message,
                        stateOverride: "Failed");
                }
            });
    }

    public async Task<QtePracticeWebStateDto> ResolvePracticeActionAsync(QtePracticeActionRequest? request)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidatePracticeInteraction(
                    writeLease,
                    request?.InteractionToken);
                if (!tokenValidation.IsValid)
                    return FailedPractice(tokenValidation);

                if (DiscardStalePracticeAttempt(writeLease))
                {
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "Игровая сессия изменилась. Начните тренировку заново.",
                        stateOverride: "Failed");
                }

                var actionId = request?.ActionId?.Trim();
                if (string.IsNullOrWhiteSpace(actionId))
                {
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "actionId is required.",
                        stateOverride: "Failed");
                }

                if (_practiceAttempt == null ||
                    !string.Equals(
                        _practiceAttempt.State,
                        "Active",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "Тренировка не запущена.",
                        stateOverride: "Failed");
                }

                var attemptBefore = ClonePracticeAttempt(_practiceAttempt);
                try
                {
                    _qteSceneService.ResolvePracticeAction(
                        _practiceAttempt,
                        actionId,
                        request?.Grade);
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        "Попытка завершена.",
                        error: null);
                }
                catch (Exception ex)
                {
                    _practiceAttempt = attemptBefore;
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: ex.Message,
                        stateOverride: "Failed");
                }
            });
    }

    public async Task<QtePracticeWebStateDto> RetryPracticeAttemptAsync(
        string? interactionToken = null)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidatePracticeInteraction(
                    writeLease,
                    interactionToken);
                if (!tokenValidation.IsValid)
                    return FailedPractice(tokenValidation);

                if (DiscardStalePracticeAttempt(writeLease))
                {
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "Игровая сессия изменилась. Начните тренировку заново.",
                        stateOverride: "Failed");
                }

                if (_practiceAttempt == null)
                {
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "Сначала выберите QTE для тренировки.",
                        stateOverride: "Failed");
                }

                var attemptBefore = ClonePracticeAttempt(_practiceAttempt);
                var generationBefore = _practiceAttemptGeneration;
                var identityBefore = _practiceAttemptIdentity;
                try
                {
                    _practiceAttemptGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
                    _practiceAttempt = _qteSceneService.StartPracticeAttempt(
                        attemptBefore.TypeId,
                        attemptBefore.DifficultyId);
                    _practiceAttemptIdentity = Guid.NewGuid().ToString("N");
                    return await BuildPracticeStateCoreAsync(
                        writeLease,
                        "Тренировка повторена.",
                        error: null);
                }
                catch
                {
                    _practiceAttempt = attemptBefore;
                    _practiceAttemptGeneration = generationBefore;
                    _practiceAttemptIdentity = identityBefore;
                    throw;
                }
            });
    }

    public async Task<QtePracticeWebStateDto> ExitPracticeAttemptAsync(
        string? interactionToken = null)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidatePracticeInteraction(
                    writeLease,
                    interactionToken);
                if (!tokenValidation.IsValid)
                    return FailedPractice(tokenValidation);

                _practiceAttempt = null;
                _practiceAttemptGeneration = null;
                _practiceAttemptIdentity = null;
                return await BuildPracticeStateCoreAsync(
                    writeLease,
                    "Тренировка закрыта.",
                    error: null);
            });
    }

    public async Task<DarenShowcaseWebStateDto> StartDarenShowcaseAsync(
        string? interactionToken = null)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidateDarenInteraction(
                    writeLease,
                    interactionToken);
                if (!tokenValidation.IsValid)
                    return FailedDaren(tokenValidation);

                DiscardStaleDarenAttempt(writeLease);
                var attemptBefore = _darenAttempt == null
                    ? null
                    : CloneDarenAttempt(_darenAttempt);
                var generationBefore = _darenAttemptGeneration;
                var identityBefore = _darenAttemptIdentity;
                try
                {
                    _darenAttemptGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
                    _darenAttempt = _qteSceneService.StartDarenShowcaseAttempt();
                    _darenAttemptIdentity = Guid.NewGuid().ToString("N");
                    return await BuildDarenShowcaseStateCoreAsync(
                        writeLease,
                        "Вылазка Дарена началась.",
                        error: null);
                }
                catch
                {
                    _darenAttempt = attemptBefore;
                    _darenAttemptGeneration = generationBefore;
                    _darenAttemptIdentity = identityBefore;
                    throw;
                }
            });
    }

    public async Task<DarenShowcaseWebStateDto> ResolveDarenShowcaseActionAsync(DarenShowcaseActionRequest? request)
    {
        return await _coordinator.RunBoundTransactionAsync(
            writeLease => ResolveDarenShowcaseActionBoundAsync(
                writeLease,
                request));
    }

    private async Task<DarenShowcaseWebStateDto> ResolveDarenShowcaseActionBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        DarenShowcaseActionRequest? request)
    {
        var tokenValidation = ValidateDarenInteraction(
            writeLease,
            request?.InteractionToken);
        if (!tokenValidation.IsValid)
            return FailedDaren(tokenValidation);

        if (DiscardStaleDarenAttempt(writeLease))
        {
            return await BuildDarenShowcaseStateCoreAsync(
                writeLease,
                notification: null,
                error: "Игровая сессия изменилась. Начните вылазку Дарена заново.",
                stateOverride: "Failed");
        }

        var actionId = request?.ActionId?.Trim();
        if (string.IsNullOrWhiteSpace(actionId))
            return await BuildDarenShowcaseStateCoreAsync(
                writeLease,
                notification: null,
                error: "actionId is required.",
                stateOverride: "Failed");

        if (_darenAttempt == null || !string.Equals(_darenAttempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            return await BuildDarenShowcaseStateCoreAsync(
                writeLease,
                notification: null,
                error: "Вылазка Дарена не запущена.",
                stateOverride: "Failed");

        var attemptBefore = CloneDarenAttempt(_darenAttempt);
        DarenShowcaseWebStateDto? state = null;
        var result = await _coordinator.ExecuteAtomicWithinTransactionAsync(
            writeLease,
            BuildWriteRequest("Daren QTE action"),
            QteSceneService.BrowserTransactionRollbackPaths,
            async transactionLease =>
            {
                var resolution = await _qteSceneService.ResolveDarenShowcaseActionAsync(
                    transactionLease,
                    _darenAttempt,
                    actionId,
                    request?.Grade);
                state = await BuildDarenShowcaseStateCoreAsync(
                    transactionLease,
                    resolution.Completion == null
                        ? "Вылазка продолжается."
                        : "Вылазка завершена.",
                    error: null);
            },
            rollbackExternalFileIds:
            [
                ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId
            ]);

        if (!result.Success)
        {
            _darenAttempt = attemptBefore;
            return await BuildDarenShowcaseStateCoreAsync(
                writeLease,
                notification: null,
                error: result.Message,
                stateOverride: "Failed");
        }

        return state ?? await BuildDarenShowcaseStateCoreAsync(
            writeLease,
            notification: null,
            error: "Daren QTE transaction completed without a state.",
            stateOverride: "Failed");
    }

    public async Task<DarenShowcaseWebStateDto> RetryDarenShowcaseAsync(
        string? interactionToken = null)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidateDarenInteraction(
                    writeLease,
                    interactionToken);
                if (!tokenValidation.IsValid)
                    return FailedDaren(tokenValidation);

                if (DiscardStaleDarenAttempt(writeLease))
                {
                    return await BuildDarenShowcaseStateCoreAsync(
                        writeLease,
                        notification: null,
                        error: "Игровая сессия изменилась. Начните вылазку Дарена заново.",
                        stateOverride: "Failed");
                }

                var attemptBefore = _darenAttempt == null
                    ? null
                    : CloneDarenAttempt(_darenAttempt);
                var generationBefore = _darenAttemptGeneration;
                var identityBefore = _darenAttemptIdentity;
                try
                {
                    _darenAttemptGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
                    _darenAttempt = _qteSceneService.StartDarenShowcaseAttempt();
                    _darenAttemptIdentity = Guid.NewGuid().ToString("N");
                    return await BuildDarenShowcaseStateCoreAsync(
                        writeLease,
                        "Вылазка Дарена началась заново.",
                        error: null);
                }
                catch
                {
                    _darenAttempt = attemptBefore;
                    _darenAttemptGeneration = generationBefore;
                    _darenAttemptIdentity = identityBefore;
                    throw;
                }
            });
    }

    public async Task<DarenShowcaseWebStateDto> ExitDarenShowcaseAsync(
        string? interactionToken = null)
    {
        return await _coordinator.RunBoundTransactionAsync(
            async writeLease =>
            {
                var tokenValidation = ValidateDarenInteraction(
                    writeLease,
                    interactionToken);
                if (!tokenValidation.IsValid)
                    return FailedDaren(tokenValidation);

                _darenAttempt = null;
                _darenAttemptGeneration = null;
                _darenAttemptIdentity = null;
                return await BuildDarenShowcaseStateCoreAsync(
                    writeLease,
                    "Вылазка Дарена закрыта.",
                    error: null);
            });
    }

    private async Task<QtePracticeWebStateDto> BuildPracticeStateCoreAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? notification,
        string? error,
        string? stateOverride = null)
    {
        var attempt = _practiceAttempt;
        var catalog = QteSceneService.GetPracticeCatalog().Select(BuildPracticeCatalogEntry).ToList();
        var state = stateOverride ?? attempt?.State ?? "Catalog";
        var activeScene = attempt != null && string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase)
            ? await BuildActiveSceneAsync(attempt.ActiveScene, writeLease)
            : null;
        var interactionIdentity = attempt == null
            ? "practice-catalog"
            : _practiceAttemptIdentity ??= Guid.NewGuid().ToString("N");
        var interactionFingerprint = attempt == null
            ? "practice-catalog"
            : Fingerprint(attempt);

        return new QtePracticeWebStateDto
        {
            State = state,
            Catalog = catalog,
            SelectedTypeId = attempt?.TypeId,
            SelectedDifficultyId = attempt?.DifficultyId,
            ActiveScene = activeScene,
            Resolution = attempt?.LastResolution == null ? null : BuildResolution(attempt.LastResolution),
            Completion = attempt?.LastCompletion == null ? null : BuildCompletion(attempt.LastCompletion),
            FeedbackTitle = attempt?.FeedbackTitle ?? "Свободная тренировка",
            Feedback = attempt?.Feedback ?? "Выберите тип QTE. Тренировка не меняет сюжет и не выдаёт награды.",
            LocalScoreNotice = attempt?.LocalScoreNotice ?? PracticeLocalScoreNotice,
            AvailableOperations = BuildPracticeOperations(state).ToList(),
            InteractionToken = _interactionTokens.Publish(
                writeLease,
                "practice",
                interactionIdentity,
                interactionFingerprint),
            Notification = notification,
            Error = error
        };
    }

    private static IEnumerable<string> BuildPracticeOperations(string state)
    {
        if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
            return ["submitAction", "retry", "changeDifficulty", "chooseAnother", "exit"];

        if (string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
            return ["retry", "changeDifficulty", "chooseAnother", "exit"];

        return ["startAttempt", "exit"];
    }

    private async Task<DarenShowcaseWebStateDto> BuildDarenShowcaseStateCoreAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? notification,
        string? error,
        string? stateOverride = null)
    {
        var attempt = _darenAttempt;
        var state = stateOverride ?? attempt?.State ?? "Intro";
        var activeScene = attempt != null && string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase)
            ? await BuildActiveSceneAsync(attempt.ActiveScene, writeLease)
            : null;
        var interactionIdentity = attempt == null
            ? "daren-intro"
            : _darenAttemptIdentity ??= Guid.NewGuid().ToString("N");
        var interactionFingerprint = attempt == null
            ? "daren-intro"
            : Fingerprint(attempt);
        DarenRewardRecord? bestReward = null;
        try
        {
            bestReward = (await _qteSceneService.ReadDarenRewardProfileAsync(writeLease))
                .DarenShowcase;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            state = "Failed";
            const string profileError =
                "Профиль постоянной награды Дарена недоступен: физическая целостность хранилища нарушена.";
            error = string.IsNullOrWhiteSpace(error)
                ? profileError
                : $"{error} {profileError}";
        }

        return new DarenShowcaseWebStateDto
        {
            State = state,
            IntroTitle = "Ограбление поместья Дареном",
            IntroText = "Хитрый вор Дарен проникает в запертое поместье, крадёт магический посох, уходит от погони и возвращается в убежище.",
            BoundaryNotice = attempt?.BoundaryNotice ?? DarenShowcaseWebStateDto.DefaultBoundaryNotice,
            RewardNotice = attempt?.RewardNotice ?? DarenShowcaseWebStateDto.DefaultRewardNotice,
            BestReward = bestReward == null ? null : BuildDarenBestReward(bestReward),
            ActiveScene = activeScene,
            Resolution = attempt?.LastResolution == null ? null : BuildResolution(attempt.LastResolution),
            Completion = attempt?.LastCompletion == null ? null : BuildCompletion(attempt.LastCompletion),
            Ending = attempt?.Ending == null ? null : BuildDarenEnding(attempt.Ending),
            AvailableOperations = BuildDarenOperations(state).ToList(),
            InteractionToken = _interactionTokens.Publish(
                writeLease,
                "daren",
                interactionIdentity,
                interactionFingerprint),
            Notification = notification,
            Error = error
        };
    }

    private static IEnumerable<string> BuildDarenOperations(string state)
    {
        if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
            return ["submitAction", "exit"];

        if (string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
            return ["retry", "exit"];

        return ["start", "exit"];
    }

    private static DarenRewardProfileDto BuildDarenBestReward(DarenRewardRecord record) =>
        new()
        {
            TierId = record.BestTierId,
            TierName = record.BestTierName,
            InkFeatherBonus = record.InkFeatherBonus,
            BestScore = record.BestScore,
            CompletedAtUtc = record.CompletedAtUtc,
            Summary = DarenQteRewardProfileService.BuildProfileSummary(record)
        };

    private static DarenShowcaseEndingDto BuildDarenEnding(QteSceneService.DarenShowcaseEnding ending) =>
        new()
        {
            TierId = ending.TierId,
            DisplayName = ending.DisplayName,
            NormalizedScore = ending.NormalizedScore,
            InkFeatherBonus = ending.InkFeatherBonus,
            GrantsReward = ending.GrantsReward,
            Epilogue = ending.Epilogue,
            RewardExplanation = ending.RewardExplanation,
            RewardMessage = ending.RewardMessage,
            RewardProfileSummary = ending.RewardProfileSummary
        };

    private static QtePracticeCatalogEntryDto BuildPracticeCatalogEntry(QteSceneService.QtePracticeCatalogEntry entry) =>
        new()
        {
            TypeId = entry.TypeId,
            Title = entry.Title,
            Description = entry.Description,
            Instructions = entry.Instructions,
            Available = entry.Available,
            UnavailableReason = entry.UnavailableReason,
            SupportedSurfaces = entry.SupportedSurfaces.ToList(),
            Difficulties = entry.Difficulties.Select(difficulty => new QtePracticeDifficultyDto
            {
                DifficultyId = difficulty.DifficultyId,
                Label = difficulty.Label,
                Description = difficulty.Description
            }).ToList()
        };

    private static QteWebOfferDto BuildOffer(QteSceneService.QteOffer offer) =>
        new()
        {
            QteId = offer.QteId,
            Title = offer.Title ?? "QTE событие",
            OfferText = offer.OfferText,
            IntroNarrative = offer.IntroNarrative,
            DeclineHint = offer.DeclineHint,
            CinematicJustification = offer.CinematicJustification,
            SceneImagePrompt = offer.SceneImagePrompt,
            StartChapterId = offer.StartChapterId
        };

    private async Task<QteWebActiveSceneDto> BuildActiveSceneAsync(
        QteSceneService.ActiveQteSceneState active,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var offer = active.Offer!;
        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));

        return new QteWebActiveSceneDto
        {
            QteId = offer.QteId,
            Title = offer.Title ?? "QTE событие",
            AcceptedAtTurn = active.AcceptedAtTurn,
            CurrentChapter = chapter == null
                ? null
                : await BuildChapterAsync(chapter, writeLease),
            ScoreState = BuildActiveScoreState(active.ScoreState)
        };
    }

    private async Task<QteWebChapterDto> BuildChapterAsync(
        QteSceneService.QteChapter chapter,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var actions = await Task.WhenAll(
            chapter.Actions.Select(action => BuildActionAsync(action, writeLease)));
        return new QteWebChapterDto
        {
            ChapterId = chapter.ChapterId,
            Title = chapter.Title,
            Narrative = chapter.Narrative,
            ChapterImagePrompt = chapter.ChapterImagePrompt,
            Actions = actions.ToList()
        };
    }

    private async Task<QteWebActionDto> BuildActionAsync(
        QteSceneService.QteAction action,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var checkType = action.Check.Type;
        var statTier = await _qteSceneService.ResolveQteStatTierAsync(
            writeLease,
            action.Check.PrimaryCharacteristic);
        var checkConfig = BuildCheckConfig(action, statTier);
        return new QteWebActionDto
        {
            ActionId = action.ActionId,
            Label = action.Label,
            CheckType = checkType,
            BaseDifficulty = action.Check.BaseDifficulty,
            PrimaryCharacteristic = action.Check.PrimaryCharacteristic,
            RequiresSubmittedGrade = IsInteractiveSupportedCheck(checkConfig),
            GradeOptions = GradeOptions.ToList(),
            CheckConfig = checkConfig
        };
    }

    private static bool IsInteractiveSupportedCheck(JsonObject checkConfig)
    {
        if (checkConfig["supported"]?.GetValue<bool>() != true)
            return false;

        return !string.Equals(
            checkConfig["kind"]?.GetValue<string>(),
            "BranchChoice",
            StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject BuildCheckConfig(QteSceneService.QteAction action, int statTier)
    {
        var check = action.Check;
        return check.Type.Trim() switch
        {
            "TimingBar" => BuildTimingBarConfig(check, statTier),
            "PromptChain" => BuildPromptChainConfig(action, statTier),
            "BalanceMeter" => BuildBalanceMeterConfig(check, statTier),
            "ChargeRelease" => BuildChargeReleaseConfig(check, statTier),
            "BranchChoice" => BuildBranchChoiceConfig(check),
            "MashInput" => BuildMashInputConfig(check, statTier),
            "PatternMemory" => BuildPatternMemoryConfig(action, statTier),
            "RhythmPulse" => BuildRhythmPulseConfig(check, statTier),
            "PrecisionChoice" => BuildPrecisionChoiceConfig(check, statTier),
            "StealthNoise" => BuildStealthNoiseConfig(check, statTier),
            "LockPinSet" => BuildLockPinSetConfig(check, statTier),
            _ => UnsupportedCheckConfig(check.Type)
        };
    }

    private static JsonObject BuildTimingBarConfig(QteSceneService.QteCheck check, int statTier)
    {
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        const int width = 32;
        var successWidth = Math.Clamp(8 - difficulty + statTier, 3, 12);
        var partialWidth = Math.Clamp(successWidth + 4, successWidth + 1, 16);
        var tickMs = Math.Clamp(110 - (statTier * 5) + (difficulty * 10), 50, 180);
        var successStart = (width - successWidth) / 2;
        var partialStart = (width - partialWidth) / 2;

        return SupportedCheckConfig("TimingBar", new JsonObject
        {
            ["width"] = width,
            ["successStart"] = successStart,
            ["successWidth"] = successWidth,
            ["partialStart"] = partialStart,
            ["partialWidth"] = partialWidth,
            ["tickMs"] = tickMs
        });
    }

    private static JsonObject BuildPromptChainConfig(QteSceneService.QteAction action, int statTier)
    {
        var difficulty = Math.Clamp(action.Check.BaseDifficulty, 1, 5);
        var steps = Math.Clamp(3 + difficulty - Math.Max(0, statTier - 1), 2, 7);
        var allowedMistakes = statTier >= 2 ? 1 : 0;
        var timeoutMs = Math.Clamp(1100 + (statTier * 150) - (difficulty * 120), 450, 1600);
        var tokenCycle = new[] { "w", "a", "s", "d", "e", "space", "q" };
        var offset = Math.Abs(action.ActionId.Sum(character => character)) % tokenCycle.Length;
        var sequence = Enumerable.Range(0, steps)
            .Select(index => tokenCycle[(offset + index) % tokenCycle.Length])
            .ToArray();

        return SupportedCheckConfig("PromptChain", new JsonObject
        {
            ["sequence"] = StringArray(sequence),
            ["allowedMistakes"] = allowedMistakes,
            ["timeoutMs"] = timeoutMs
        });
    }

    private static JsonObject BuildBalanceMeterConfig(QteSceneService.QteCheck check, int statTier)
    {
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        return SupportedCheckConfig("BalanceMeter", new JsonObject
        {
            ["safeHalfWidth"] = Math.Clamp(18 - (difficulty * 2) + (statTier * 2), 8, 24),
            ["tickMs"] = Math.Clamp(140 - (statTier * 5) + (difficulty * 10), 70, 220),
            ["ticks"] = 18 + (difficulty * 2)
        });
    }

    private static JsonObject BuildChargeReleaseConfig(QteSceneService.QteCheck check, int statTier)
    {
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        return SupportedCheckConfig("ChargeRelease", new JsonObject
        {
            ["targetStart"] = Math.Clamp(50 - (difficulty * 5) - (statTier * 2), 20, 70),
            ["targetWidth"] = Math.Clamp(20 - (difficulty * 2) + (statTier * 2), 8, 26),
            ["tickMs"] = Math.Clamp(85 - (statTier * 5) + (difficulty * 8), 40, 140),
            ["partialPadding"] = 10
        });
    }

    private static JsonObject BuildBranchChoiceConfig(QteSceneService.QteCheck check) =>
        SupportedCheckConfig("BranchChoice", new JsonObject
        {
            ["choiceGrade"] = NormalizeGrade(GetConfigString(check.Config, "choiceGrade"))
        });

    private static JsonObject BuildMashInputConfig(QteSceneService.QteCheck check, int statTier)
    {
        if (!TryGetStringArray(check.Config, "keys", out var keys) ||
            !TryGetInt(check.Config, "durationMs", out var durationMs) ||
            !TryGetInt(check.Config, "targetPresses", out var targetPresses) ||
            !TryGetDouble(check.Config, "partialThreshold", out var partialThreshold))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var supportedKeys = keys
            .Select(static key => key.Trim().ToLowerInvariant())
            .Where(QteKeyInput.IsSupportedToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (supportedKeys.Length == 0)
            return UnsupportedCheckConfig(check.Type);

        var successTarget = QteSceneService.ComputeMashInputEffectiveTargetPresses(
            targetPresses,
            check.BaseDifficulty,
            statTier);
        var partialTarget = QteSceneService.ComputeMashInputPartialTargetPresses(successTarget, partialThreshold);

        return SupportedCheckConfig("MashInput", new JsonObject
        {
            ["keys"] = StringArray(supportedKeys),
            ["durationMs"] = durationMs,
            ["targetPresses"] = targetPresses,
            ["partialThreshold"] = partialThreshold,
            ["successTarget"] = successTarget,
            ["partialTarget"] = partialTarget
        });
    }

    private static JsonObject BuildPatternMemoryConfig(QteSceneService.QteAction action, int statTier)
    {
        var check = action.Check;
        if (!TryGetStringArray(check.Config, "alphabet", out var alphabet) ||
            !TryGetInt(check.Config, "sequenceLength", out var sequenceLength) ||
            !TryGetInt(check.Config, "revealMs", out var revealMs) ||
            !TryGetInt(check.Config, "inputTimeoutMs", out var inputTimeoutMs) ||
            !TryGetInt(check.Config, "allowedMistakes", out var allowedMistakes))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var supportedAlphabet = alphabet
            .Select(static token => token.Trim().ToLowerInvariant())
            .Where(QteKeyInput.IsSupportedToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (supportedAlphabet.Length == 0)
            return UnsupportedCheckConfig(check.Type);

        var effective = QteSceneService.ComputePatternMemoryEffectiveRequirement(
            sequenceLength,
            revealMs,
            inputTimeoutMs,
            allowedMistakes,
            check.BaseDifficulty,
            statTier);
        var sequence = QteSceneService.GeneratePatternMemorySequence(
            supportedAlphabet,
            effective.SequenceLength,
            $"{action.ActionId}:{check.BaseDifficulty}:{check.PrimaryCharacteristic}:{string.Join(",", supportedAlphabet)}");

        return SupportedCheckConfig("PatternMemory", new JsonObject
        {
            ["alphabet"] = StringArray(supportedAlphabet),
            ["sequence"] = StringArray(sequence),
            ["sequenceLength"] = effective.SequenceLength,
            ["revealMs"] = effective.RevealMs,
            ["inputTimeoutMs"] = effective.InputTimeoutMs,
            ["allowedMistakes"] = effective.AllowedMistakes
        });
    }

    private static JsonObject BuildRhythmPulseConfig(QteSceneService.QteCheck check, int statTier)
    {
        if (!TryGetInt(check.Config, "pulseCount", out var pulseCount) ||
            !TryGetInt(check.Config, "beatIntervalMs", out var beatIntervalMs) ||
            !TryGetInt(check.Config, "hitWindowMs", out var hitWindowMs) ||
            !TryGetInt(check.Config, "allowedMisses", out var allowedMisses))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var patternVariation = GetConfigString(check.Config, "patternVariation") ?? "steady";
        var effective = QteSceneService.ComputeRhythmPulseEffectiveRequirement(
            pulseCount,
            beatIntervalMs,
            hitWindowMs,
            allowedMisses,
            check.BaseDifficulty,
            statTier);
        var schedule = QteSceneService.GenerateRhythmPulseSchedule(
            effective.PulseCount,
            effective.BeatIntervalMs,
            patternVariation);

        return SupportedCheckConfig("RhythmPulse", new JsonObject
        {
            ["pulseCount"] = effective.PulseCount,
            ["beatIntervalMs"] = effective.BeatIntervalMs,
            ["hitWindowMs"] = effective.HitWindowMs,
            ["allowedMisses"] = effective.AllowedMisses,
            ["patternVariation"] = patternVariation,
            ["pulseOffsetsMs"] = IntArray(schedule)
        });
    }

    private static JsonObject BuildPrecisionChoiceConfig(QteSceneService.QteCheck check, int statTier)
    {
        if (check.Config == null ||
            check.Config["choices"] is not JsonArray choices ||
            !TryGetInt(check.Config, "timeoutMs", out var timeoutMs))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var choiceConfigs = new JsonArray();
        var availableHintCount = 0;
        foreach (var node in choices)
        {
            if (node is not JsonObject choice ||
                !TryGetString(choice, "id", out var id) ||
                !TryGetString(choice, "label", out var label) ||
                !TryGetString(choice, "grade", out var grade))
            {
                return UnsupportedCheckConfig(check.Type);
            }

            var choiceConfig = new JsonObject
            {
                ["id"] = id,
                ["label"] = label,
                ["grade"] = NormalizeGrade(grade)
            };
            if (TryGetString(choice, "description", out var description))
                choiceConfig["description"] = description;
            if (TryGetString(choice, "hint", out var hint))
            {
                choiceConfig["hint"] = hint;
                availableHintCount++;
            }

            choiceConfigs.Add(choiceConfig);
        }

        var decoyHints = new JsonArray();
        if (check.Config["decoyHints"] is JsonArray decoyHintNodes)
        {
            foreach (var node in decoyHintNodes)
            {
                if (node is not JsonObject hint ||
                    !TryGetString(hint, "choiceId", out var choiceId) ||
                    !TryGetString(hint, "hint", out var hintText))
                {
                    return UnsupportedCheckConfig(check.Type);
                }

                decoyHints.Add(new JsonObject
                {
                    ["choiceId"] = choiceId,
                    ["hint"] = hintText
                });
                availableHintCount++;
            }
        }

        var effective = QteSceneService.ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs,
            check.BaseDifficulty,
            statTier,
            availableHintCount);

        return SupportedCheckConfig("PrecisionChoice", new JsonObject
        {
            ["choices"] = choiceConfigs,
            ["correctChoiceId"] = GetConfigString(check.Config, "correctChoiceId") ?? string.Empty,
            ["timeoutMs"] = effective.TimeoutMs,
            ["timeoutGrade"] = NormalizeTimeoutGrade(GetConfigString(check.Config, "timeoutGrade")),
            ["revealedDecoyHintCount"] = effective.RevealedDecoyHintCount,
            ["decoyHints"] = decoyHints
        });
    }

    private static JsonObject BuildStealthNoiseConfig(QteSceneService.QteCheck check, int statTier)
    {
        if (check.Config == null ||
            !TryGetInt(check.Config, "durationMs", out var durationMs) ||
            !TryGetDouble(check.Config, "startingNoise", out var startingNoise) ||
            !TryGetDouble(check.Config, "dangerThreshold", out var dangerThreshold) ||
            !TryGetDouble(check.Config, "noiseDriftPerSecond", out var noiseDriftPerSecond) ||
            !TryGetDouble(check.Config, "recoveryPerInput", out var recoveryPerInput) ||
            !TryGetInt(check.Config, "allowedOverThresholdMs", out var allowedOverThresholdMs) ||
            check.Config["gradeThresholds"] is not JsonObject thresholds ||
            !TryReadStealthNoiseThresholds(thresholds, out var gradeThresholds))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var recoveryKey = GetConfigString(check.Config, "recoveryKey") ?? "space";
        recoveryKey = QteKeyInput.IsSupportedToken(recoveryKey.Trim().ToLowerInvariant())
            ? recoveryKey.Trim().ToLowerInvariant()
            : "space";
        var effective = QteSceneService.ComputeStealthNoiseEffectiveRequirement(
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

        var config = SupportedCheckConfig("StealthNoise", new JsonObject
        {
            ["durationMs"] = effective.DurationMs,
            ["startingNoise"] = effective.StartingNoise,
            ["dangerThreshold"] = effective.DangerThreshold,
            ["noiseDriftPerSecond"] = effective.NoiseDriftPerSecond,
            ["recoveryPerInput"] = effective.RecoveryPerInput,
            ["allowedOverThresholdMs"] = effective.AllowedOverThresholdMs,
            ["recoveryKey"] = effective.RecoveryKey,
            ["gradeThresholds"] = StealthNoiseThresholdsJson(effective.GradeThresholds)
        });
        AddOptionalString(config, "recoveryLabel", GetConfigString(check.Config, "recoveryLabel"));
        AddOptionalString(config, "warningLabel", GetConfigString(check.Config, "warningLabel"));
        return config;
    }

    private static JsonObject BuildLockPinSetConfig(QteSceneService.QteCheck check, int statTier)
    {
        if (check.Config == null ||
            !TryGetInt(check.Config, "pinCount", out var pinCount) ||
            check.Config["pinWindows"] is not JsonArray windows ||
            !TryGetInt(check.Config, "timerMs", out var timerMs) ||
            !TryGetInt(check.Config, "pickDurability", out var pickDurability) ||
            !TryGetInt(check.Config, "maxMistakes", out var maxMistakes) ||
            !TryGetDouble(check.Config, "pinDriftPerSecond", out var pinDriftPerSecond) ||
            check.Config["gradeThresholds"] is not JsonObject thresholds ||
            !TryReadLockPinSetThresholds(thresholds, out var gradeThresholds))
        {
            return UnsupportedCheckConfig(check.Type);
        }

        var authoredPinWindows = new List<QteSceneService.LockPinWindow>();
        foreach (var node in windows)
        {
            if (node is not JsonObject window ||
                !TryGetDouble(window, "min", out var min) ||
                !TryGetDouble(window, "max", out var max))
            {
                return UnsupportedCheckConfig(check.Type);
            }

            authoredPinWindows.Add(new QteSceneService.LockPinWindow(
                TryGetInt(window, "pin", out var pin) ? pin : authoredPinWindows.Count + 1,
                min,
                max,
                GetConfigString(window, "label")));
        }

        var adjustKey = NormalizeSupportedKey(GetConfigString(check.Config, "adjustKey"), "q");
        var setKey = NormalizeSupportedKey(GetConfigString(check.Config, "setKey"), "space");
        var effective = QteSceneService.ComputeLockPinSetEffectiveRequirement(
            pinCount,
            authoredPinWindows,
            timerMs,
            pickDurability,
            maxMistakes,
            pinDriftPerSecond,
            gradeThresholds,
            check.BaseDifficulty,
            statTier,
            adjustKey,
            setKey);
        var config = SupportedCheckConfig("LockPinSet", new JsonObject
        {
            ["pinCount"] = effective.PinCount,
            ["pinWindows"] = LockPinWindowsJson(effective.PinWindows),
            ["timerMs"] = effective.TimerMs,
            ["pickDurability"] = effective.PickDurability,
            ["maxMistakes"] = effective.MaxMistakes,
            ["pinDriftPerSecond"] = effective.PinDriftPerSecond,
            ["adjustKey"] = effective.AdjustKey,
            ["setKey"] = effective.SetKey,
            ["gradeThresholds"] = LockPinSetThresholdsJson(effective.GradeThresholds)
        });
        AddOptionalString(config, "pinLabel", GetConfigString(check.Config, "pinLabel"));
        AddOptionalString(config, "durabilityLabel", GetConfigString(check.Config, "durabilityLabel"));
        AddOptionalString(config, "warningLabel", GetConfigString(check.Config, "warningLabel"));
        return config;
    }

    private static JsonObject SupportedCheckConfig(string kind, JsonObject fields)
    {
        fields["kind"] = kind;
        fields["supported"] = true;
        return fields;
    }

    private static JsonObject UnsupportedCheckConfig(string? checkType) =>
        new()
        {
            ["kind"] = "Unsupported",
            ["supported"] = false,
            ["checkType"] = checkType ?? string.Empty
        };

    private static string NormalizeGrade(string? grade) => grade?.Trim().ToLowerInvariant() switch
    {
        "success" => "success",
        "partial" => "partial",
        _ => "fail"
    };

    private static string NormalizeTimeoutGrade(string? grade) =>
        string.Equals(grade?.Trim(), "partial", StringComparison.OrdinalIgnoreCase) ? "partial" : "fail";

    private static string NormalizeSupportedKey(string? key, string fallback)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(normalized) && QteKeyInput.IsSupportedToken(normalized)
            ? normalized
            : fallback;
    }

    private static void AddOptionalString(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[propertyName] = value.Trim();
    }

    private static JsonArray StringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static JsonArray IntArray(IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static bool TryReadStealthNoiseThresholds(
        JsonObject source,
        out QteSceneService.StealthNoiseGradeThresholds thresholds)
    {
        thresholds = new QteSceneService.StealthNoiseGradeThresholds(0, 0, 0, 0);
        if (!TryGetDouble(source, "successMaxNoise", out var successMaxNoise) ||
            !TryGetInt(source, "successMaxOverThresholdMs", out var successMaxOverThresholdMs) ||
            !TryGetDouble(source, "partialMaxNoise", out var partialMaxNoise) ||
            !TryGetInt(source, "partialMaxOverThresholdMs", out var partialMaxOverThresholdMs))
        {
            return false;
        }

        thresholds = new QteSceneService.StealthNoiseGradeThresholds(
            successMaxNoise,
            successMaxOverThresholdMs,
            partialMaxNoise,
            partialMaxOverThresholdMs);
        return true;
    }

    private static JsonObject StealthNoiseThresholdsJson(QteSceneService.StealthNoiseGradeThresholds thresholds) =>
        new()
        {
            ["successMaxNoise"] = thresholds.SuccessMaxNoise,
            ["successMaxOverThresholdMs"] = thresholds.SuccessMaxOverThresholdMs,
            ["partialMaxNoise"] = thresholds.PartialMaxNoise,
            ["partialMaxOverThresholdMs"] = thresholds.PartialMaxOverThresholdMs
        };

    private static bool TryReadLockPinSetThresholds(
        JsonObject source,
        out QteSceneService.LockPinSetGradeThresholds thresholds)
    {
        thresholds = new QteSceneService.LockPinSetGradeThresholds(0, 0, 0, 0);
        if (!TryGetInt(source, "successMaxTimeMs", out var successMaxTimeMs) ||
            !TryGetInt(source, "successMaxMistakes", out var successMaxMistakes) ||
            !TryGetInt(source, "partialMaxTimeMs", out var partialMaxTimeMs) ||
            !TryGetInt(source, "partialMaxMistakes", out var partialMaxMistakes))
        {
            return false;
        }

        thresholds = new QteSceneService.LockPinSetGradeThresholds(
            successMaxTimeMs,
            successMaxMistakes,
            partialMaxTimeMs,
            partialMaxMistakes);
        return true;
    }

    private static JsonObject LockPinSetThresholdsJson(QteSceneService.LockPinSetGradeThresholds thresholds) =>
        new()
        {
            ["successMaxTimeMs"] = thresholds.SuccessMaxTimeMs,
            ["successMaxMistakes"] = thresholds.SuccessMaxMistakes,
            ["partialMaxTimeMs"] = thresholds.PartialMaxTimeMs,
            ["partialMaxMistakes"] = thresholds.PartialMaxMistakes
        };

    private static JsonArray LockPinWindowsJson(IEnumerable<QteSceneService.LockPinWindow> windows)
    {
        var array = new JsonArray();
        foreach (var window in windows)
        {
            var node = new JsonObject
            {
                ["pin"] = window.Pin,
                ["min"] = window.Min,
                ["max"] = window.Max
            };
            AddOptionalString(node, "label", window.Label);
            array.Add(node);
        }

        return array;
    }

    private static bool TryGetStringArray(JsonObject? root, string propertyName, out string[] values)
    {
        values = [];
        if (root?[propertyName] is not JsonArray array)
            return false;

        var parsed = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node is not JsonValue value ||
                !value.TryGetValue<string>(out var text) ||
                string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            parsed.Add(text.Trim());
        }

        values = parsed.ToArray();
        return true;
    }

    private static bool TryGetString(JsonObject root, string propertyName, out string value)
    {
        value = string.Empty;
        if (root[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static string? GetConfigString(JsonObject? config, string propertyName)
    {
        if (config == null || config[propertyName] is not JsonValue value)
            return null;

        return value.TryGetValue<string>(out var text) ? text : null;
    }

    private static bool TryGetInt(JsonObject? root, string propertyName, out int value)
    {
        value = 0;
        return root?[propertyName] is JsonValue node && node.TryGetValue<int>(out value);
    }

    private static bool TryGetDouble(JsonObject? root, string propertyName, out double value)
    {
        value = 0;
        if (root?[propertyName] is not JsonValue node)
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

    private static QteWebResolutionDto BuildResolution(QteSceneService.QteActionResolution resolution) =>
        new()
        {
            State = resolution.State,
            QteId = resolution.QteId,
            ChapterId = resolution.ChapterId,
            ActionId = resolution.ActionId,
            Grade = resolution.Grade,
            ResultText = resolution.ResultText,
            NextChapterId = resolution.NextChapterId
        };

    private static QteWebCompletionDto BuildCompletion(QteSceneService.QteSceneCompletion completion) =>
        new()
        {
            QteId = completion.QteId,
            OutcomeId = completion.OutcomeId,
            Summary = completion.Summary,
            ScoreSummary = BuildScoreSummary(completion.ScoreSummary)
        };

    private static QteWebScoreStateDto? BuildActiveScoreState(QteSceneService.QteScoreState? scoreState)
    {
        if (scoreState == null)
            return null;

        var metrics = scoreState.Metrics
            .Where(static metric => string.Equals(metric.Visibility, "always", StringComparison.OrdinalIgnoreCase))
            .Select(BuildScoreMetric)
            .ToList();
        return metrics.Count == 0 ? null : new QteWebScoreStateDto { Metrics = metrics };
    }

    private static QteWebScoreSummaryDto? BuildScoreSummary(QteSceneService.QteScoreSummary? scoreSummary)
    {
        if (scoreSummary == null)
            return null;

        return new QteWebScoreSummaryDto
        {
            Rank = scoreSummary.Rank == null
                ? null
                : new QteWebScoreRankDto
                {
                    Id = scoreSummary.Rank.Id,
                    Label = scoreSummary.Rank.Label,
                    Summary = scoreSummary.Rank.Summary
                },
            Metrics = scoreSummary.Metrics.Select(BuildScoreMetric).ToList()
        };
    }

    private static QteWebScoreMetricDto BuildScoreMetric(QteSceneService.QteScoreMetricState metric) =>
        new()
        {
            Id = metric.Id,
            Label = metric.Label,
            Value = metric.Value,
            Min = metric.Min,
            Max = metric.Max,
            Visibility = metric.Visibility
        };

    private static BrowserLocalWriteRequest BuildWriteRequest(string operationLabel) =>
        new(
            OwnerId: $"browser-qte:{Environment.ProcessId}",
            OwnerLabel: "Browser QTE",
            OperationLabel: operationLabel);

    private void DeleteIfExists(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath)
    {
        if (_fs.FileExists(writeLease, relativePath))
            _fs.DeleteFile(writeLease, relativePath);
    }

    private QteInteractionTokenAuthority.Validation ValidatePracticeInteraction(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? interactionToken)
    {
        var validation = _interactionTokens.ValidatePresented(
            writeLease,
            "practice",
            interactionToken);
        if (!validation.IsValid)
            return validation;

        var identity = _practiceAttempt == null
            ? "practice-catalog"
            : _practiceAttemptIdentity ?? string.Empty;
        var fingerprint = _practiceAttempt == null
            ? "practice-catalog"
            : Fingerprint(_practiceAttempt);
        return QteInteractionTokenAuthority.ValidateCurrentState(
            validation,
            identity,
            fingerprint);
    }

    private QteInteractionTokenAuthority.Validation ValidateDarenInteraction(
        FileSystemManager.CanonicalWriteLease writeLease,
        string? interactionToken)
    {
        var validation = _interactionTokens.ValidatePresented(
            writeLease,
            "daren",
            interactionToken);
        if (!validation.IsValid)
            return validation;

        var identity = _darenAttempt == null
            ? "daren-intro"
            : _darenAttemptIdentity ?? string.Empty;
        var fingerprint = _darenAttempt == null
            ? "daren-intro"
            : Fingerprint(_darenAttempt);
        return QteInteractionTokenAuthority.ValidateCurrentState(
            validation,
            identity,
            fingerprint);
    }

    private bool DiscardStalePracticeAttempt(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_practiceAttempt == null)
        {
            _practiceAttemptGeneration = null;
            _practiceAttemptIdentity = null;
            return false;
        }

        if (_fs.IsCurrentSessionGeneration(writeLease, _practiceAttemptGeneration))
            return false;

        _practiceAttempt = null;
        _practiceAttemptGeneration = null;
        _practiceAttemptIdentity = null;
        return true;
    }

    private bool DiscardStaleDarenAttempt(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_darenAttempt == null)
        {
            _darenAttemptGeneration = null;
            _darenAttemptIdentity = null;
            return false;
        }

        if (_fs.IsCurrentSessionGeneration(writeLease, _darenAttemptGeneration))
            return false;

        _darenAttempt = null;
        _darenAttemptGeneration = null;
        _darenAttemptIdentity = null;
        return true;
    }

    private static QteSceneService.QtePracticeAttemptState ClonePracticeAttempt(
        QteSceneService.QtePracticeAttemptState attempt)
    {
        var json = JsonSerializer.Serialize(
            attempt,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        return JsonSerializer.Deserialize<QteSceneService.QtePracticeAttemptState>(
                   json,
                   SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
               ?? throw new InvalidOperationException("Не удалось создать rollback snapshot тренировки QTE.");
    }

    private static QteSceneService.DarenShowcaseAttemptState CloneDarenAttempt(
        QteSceneService.DarenShowcaseAttemptState attempt)
    {
        var json = JsonSerializer.Serialize(
            attempt,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        return JsonSerializer.Deserialize<QteSceneService.DarenShowcaseAttemptState>(
                   json,
                   SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
               ?? throw new InvalidOperationException("Не удалось создать rollback snapshot вылазки Дарена.");
    }

    private static string Fingerprint<T>(T value) =>
        Convert.ToHexString(
                SHA256.HashData(
                    JsonSerializer.SerializeToUtf8Bytes(
                        value,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)))
            .ToLowerInvariant();

    private static QteWebStateDto Failed(
        QteInteractionTokenAuthority.Validation validation) =>
        new()
        {
            State = "Failed",
            ErrorCode = validation.ErrorCode,
            Error = validation.Error,
            AvailableOperations = []
        };

    private static QtePracticeWebStateDto FailedPractice(
        QteInteractionTokenAuthority.Validation validation) =>
        new()
        {
            State = "Failed",
            Catalog = QteSceneService.GetPracticeCatalog()
                .Select(BuildPracticeCatalogEntry)
                .ToList(),
            ErrorCode = validation.ErrorCode,
            Error = validation.Error,
            AvailableOperations = []
        };

    private static DarenShowcaseWebStateDto FailedDaren(
        QteInteractionTokenAuthority.Validation validation) =>
        new()
        {
            State = "Failed",
            ErrorCode = validation.ErrorCode,
            Error = validation.Error,
            AvailableOperations = []
        };

    private static QteWebStateDto Failed(string message) =>
        new()
        {
            State = "Failed",
            Error = message,
            AvailableOperations = []
        };
}

public sealed class QteWebStateDto
{
    public string State { get; init; } = "NoScene";
    public QteWebOfferDto? Offer { get; init; }
    public QteWebActiveSceneDto? ActiveScene { get; init; }
    public QteWebResolutionDto? Resolution { get; init; }
    public QteWebCompletionDto? Completion { get; init; }
    public string? LastResolvedReminder { get; init; }
    public string? LastDeclinedQteId { get; init; }
    public List<string> AvailableOperations { get; init; } = [];
    public string? InteractionToken { get; init; }
    public string? Notification { get; init; }
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }
}

public sealed class QtePracticeWebStateDto
{
    public string State { get; init; } = "Catalog";
    public List<QtePracticeCatalogEntryDto> Catalog { get; init; } = [];
    public string? SelectedTypeId { get; init; }
    public string? SelectedDifficultyId { get; init; }
    public QteWebActiveSceneDto? ActiveScene { get; init; }
    public QteWebResolutionDto? Resolution { get; init; }
    public QteWebCompletionDto? Completion { get; init; }
    public string FeedbackTitle { get; init; } = "";
    public string Feedback { get; init; } = "";
    public string LocalScoreNotice { get; init; } = "";
    public List<string> AvailableOperations { get; init; } = [];
    public string? InteractionToken { get; init; }
    public string? Notification { get; init; }
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }
}

public sealed class DarenShowcaseWebStateDto
{
    public const string DefaultBoundaryNotice =
        "Это отдельная авторская QTE-вылазка: обычная глава, обычные ходы и свободная тренировка QTE не меняются.";

    public const string DefaultRewardNotice =
        "Лучший итог Дарена запоминается книгой и даёт Чернильные Перья только при создании будущей новой игры.";

    public string State { get; init; } = "Intro";
    public string IntroTitle { get; init; } = "Ограбление поместья Дареном";
    public string IntroText { get; init; } = "";
    public string BoundaryNotice { get; init; } = DefaultBoundaryNotice;
    public string RewardNotice { get; init; } = DefaultRewardNotice;
    public DarenRewardProfileDto? BestReward { get; init; }
    public QteWebActiveSceneDto? ActiveScene { get; init; }
    public QteWebResolutionDto? Resolution { get; init; }
    public QteWebCompletionDto? Completion { get; init; }
    public DarenShowcaseEndingDto? Ending { get; init; }
    public List<string> AvailableOperations { get; init; } = [];
    public string? InteractionToken { get; init; }
    public string? Notification { get; init; }
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }
}

public sealed class DarenRewardProfileDto
{
    public string TierId { get; init; } = "";
    public string TierName { get; init; } = "";
    public int InkFeatherBonus { get; init; }
    public int BestScore { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class DarenShowcaseEndingDto
{
    public string? TierId { get; init; }
    public string DisplayName { get; init; } = "";
    public int NormalizedScore { get; init; }
    public int InkFeatherBonus { get; init; }
    public bool GrantsReward { get; init; }
    public string Epilogue { get; init; } = "";
    public string RewardExplanation { get; init; } = "";
    public string RewardMessage { get; init; } = "";
    public string RewardProfileSummary { get; init; } = "";
}

public sealed class QtePracticeCatalogEntryDto
{
    public string TypeId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Instructions { get; init; } = "";
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public List<string> SupportedSurfaces { get; init; } = [];
    public List<QtePracticeDifficultyDto> Difficulties { get; init; } = [];
}

public sealed class QtePracticeDifficultyDto
{
    public string DifficultyId { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
}

public sealed class QteWebOfferDto
{
    public string QteId { get; init; } = "";
    public string Title { get; init; } = "";
    public string? OfferText { get; init; }
    public string? IntroNarrative { get; init; }
    public string? DeclineHint { get; init; }
    public string? CinematicJustification { get; init; }
    public string? SceneImagePrompt { get; init; }
    public string StartChapterId { get; init; } = "";
}

public sealed class QteWebActiveSceneDto
{
    public string QteId { get; init; } = "";
    public string Title { get; init; } = "";
    public int AcceptedAtTurn { get; init; }
    public QteWebChapterDto? CurrentChapter { get; init; }
    public QteWebScoreStateDto? ScoreState { get; init; }
}

public sealed class QteWebChapterDto
{
    public string ChapterId { get; init; } = "";
    public string? Title { get; init; }
    public string? Narrative { get; init; }
    public string? ChapterImagePrompt { get; init; }
    public List<QteWebActionDto> Actions { get; init; } = [];
}

public sealed class QteWebActionDto
{
    public string ActionId { get; init; } = "";
    public string Label { get; init; } = "";
    public string CheckType { get; init; } = "";
    public int BaseDifficulty { get; init; }
    public string PrimaryCharacteristic { get; init; } = "";
    public bool RequiresSubmittedGrade { get; init; }
    public List<string> GradeOptions { get; init; } = [];
    public JsonObject CheckConfig { get; init; } = new()
    {
        ["kind"] = "Unsupported",
        ["supported"] = false,
        ["checkType"] = ""
    };
}

public sealed class QteWebResolutionDto
{
    public string State { get; init; } = "";
    public string QteId { get; init; } = "";
    public string ChapterId { get; init; } = "";
    public string ActionId { get; init; } = "";
    public string Grade { get; init; } = "";
    public string ResultText { get; init; } = "";
    public string? NextChapterId { get; init; }
}

public sealed class QteWebCompletionDto
{
    public string QteId { get; init; } = "";
    public string OutcomeId { get; init; } = "";
    public string Summary { get; init; } = "";
    public QteWebScoreSummaryDto? ScoreSummary { get; init; }
}

public sealed class QteWebScoreStateDto
{
    public List<QteWebScoreMetricDto> Metrics { get; init; } = [];
}

public sealed class QteWebScoreSummaryDto
{
    public QteWebScoreRankDto? Rank { get; init; }
    public List<QteWebScoreMetricDto> Metrics { get; init; } = [];
}

public sealed class QteWebScoreRankDto
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Summary { get; init; }
}

public sealed class QteWebScoreMetricDto
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public double Value { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public string Visibility { get; init; } = "";
}
