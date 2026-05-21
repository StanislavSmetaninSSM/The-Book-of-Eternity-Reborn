using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed record QteWebOfferDecisionRequest(string? Decision);

public sealed record QteWebActionRequest(string? ActionId, string? Grade);

public sealed class QteWebInteractionService
{
    private static readonly string[] GradeOptions = ["success", "partial", "fail"];

    private readonly FileSystemManager _fs;
    private readonly QteSceneService _qteSceneService;

    public QteWebInteractionService(FileSystemManager fs, QteSceneService qteSceneService)
    {
        _fs = fs;
        _qteSceneService = qteSceneService;
    }

    public async Task<QteWebStateDto> BuildStateAsync(
        string? stateOverride = null,
        QteSceneService.QteActionResolution? resolution = null,
        string? notification = null)
    {
        await _qteSceneService.EnsureRuntimeStateHealthyAsync();

        var offer = await _qteSceneService.TryReadOfferAsync();
        if (offer != null)
        {
            return new QteWebStateDto
            {
                State = stateOverride ?? "Offer",
                Offer = BuildOffer(offer),
                AvailableOperations = ["accept", "decline"],
                Notification = notification
            };
        }

        var runtime = await _qteSceneService.ReadRuntimeStateAsync();
        if (runtime.ActiveScene is { Offer: not null } active)
        {
            return new QteWebStateDto
            {
                State = stateOverride ?? "Active",
                ActiveScene = BuildActiveScene(active),
                Resolution = resolution == null ? null : BuildResolution(resolution),
                Completion = resolution?.Completion == null ? null : BuildCompletion(resolution.Completion),
                AvailableOperations = ["submitAction"],
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
        var decision = request?.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("accept" or "decline"))
            return Failed("decision must be accept or decline.");

        var offer = await _qteSceneService.TryReadOfferAsync();
        if (offer == null)
            return Failed("No pending QTE offer is available.");

        var turnNumber = await ReadCurrentTurnNumberAsync();
        if (decision == "decline")
        {
            await _qteSceneService.RecordDeclineAsync(offer, turnNumber);
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            return await BuildStateAsync("Declined", notification: "QTE отклонено. Консольный режим повторно отправляет исходное действие GM; браузерный протокол фиксирует отказ и очищает offer.");
        }

        await _qteSceneService.BeginAcceptedSceneAsync(offer, turnNumber);
        return await BuildStateAsync("Active", notification: "QTE принято. Выберите действие текущей сцены.");
    }

    public async Task<QteWebStateDto> ResolveActionAsync(QteWebActionRequest? request)
    {
        var actionId = request?.ActionId?.Trim();
        if (string.IsNullOrWhiteSpace(actionId))
            return Failed("actionId is required.");

        try
        {
            var resolution = await _qteSceneService.ResolveActiveActionAsync(
                actionId,
                request?.Grade,
                await ReadCurrentTurnNumberAsync(),
                allowPreexistingStateIssues: true);

            return await BuildStateAsync(resolution.State, resolution, "QTE action resolved.");
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

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

    private static QteWebActiveSceneDto BuildActiveScene(QteSceneService.ActiveQteSceneState active)
    {
        var offer = active.Offer!;
        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));

        return new QteWebActiveSceneDto
        {
            QteId = offer.QteId,
            Title = offer.Title ?? "QTE событие",
            AcceptedAtTurn = active.AcceptedAtTurn,
            CurrentChapter = chapter == null ? null : BuildChapter(chapter)
        };
    }

    private static QteWebChapterDto BuildChapter(QteSceneService.QteChapter chapter) =>
        new()
        {
            ChapterId = chapter.ChapterId,
            Title = chapter.Title,
            Narrative = chapter.Narrative,
            ChapterImagePrompt = chapter.ChapterImagePrompt,
            Actions = chapter.Actions.Select(BuildAction).ToList()
        };

    private static QteWebActionDto BuildAction(QteSceneService.QteAction action)
    {
        var checkType = action.Check.Type;
        return new QteWebActionDto
        {
            ActionId = action.ActionId,
            Label = action.Label,
            CheckType = checkType,
            BaseDifficulty = action.Check.BaseDifficulty,
            PrimaryCharacteristic = action.Check.PrimaryCharacteristic,
            RequiresSubmittedGrade = !string.Equals(checkType, "BranchChoice", StringComparison.OrdinalIgnoreCase),
            GradeOptions = GradeOptions.ToList()
        };
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
            Summary = completion.Summary
        };

    private async Task<int> ReadCurrentTurnNumberAsync()
    {
        var json = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("turnNumber", out var turnNode) &&
                turnNode.ValueKind == System.Text.Json.JsonValueKind.Number &&
                turnNode.TryGetInt32(out var turnNumber))
            {
                return turnNumber;
            }
        }
        catch
        {
            return 0;
        }

        return 0;
    }

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
    public string? Notification { get; init; }
    public string? Error { get; init; }
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
}
