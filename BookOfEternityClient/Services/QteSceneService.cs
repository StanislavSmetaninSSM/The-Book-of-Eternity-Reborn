using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public sealed class QteSceneService
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

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private sealed class QteNormalizationBaseline
    {
        public Dictionary<string, string?> RestoreBackupsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> NormalizerBackupsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
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
    }

    public async Task<QteOffer?> TryReadOfferAsync()
    {
        var json = await _fs.ReadFileAsync(QteOfferPath);
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

    public void ClearOfferFile()
    {
        if (_fs.FileExists(QteOfferPath))
            _fs.DeleteFile(QteOfferPath);
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

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" QTE Offer ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("[bold]Действие:[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("✅ Принять", "❌ Отклонить"));

        return choice.Contains("Принять", StringComparison.OrdinalIgnoreCase)
            ? QteOfferDecision.Accept
            : QteOfferDecision.Decline;
    }

    public async Task RecordDeclineAsync(QteOffer offer, int sourceTurnNumber)
    {
        var state = await LoadRuntimeStateAsync();
        state.PendingOffer = null;
        state.ActiveScene = null;
        state.LastDeclinedQteId = offer.QteId;
        state.LastDeclinedAtTurn = sourceTurnNumber;
        await SaveRuntimeStateAsync(state);
        ClearOfferFile();
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

    public async Task EnsureRuntimeStateHealthyAsync()
    {
        var json = await _fs.ReadFileAsync(QteRuntimePath);
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
            _fs.DeleteFile(QteRuntimePath);
            return;
        }

        if (parsed is not JsonObject root)
        {
            _logger.LogWarning("Найден qte_runtime.json с не-object корнем. Удаление как повреждённого runtime state.");
            _fs.DeleteFile(QteRuntimePath);
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
            _fs.DeleteFile(QteRuntimePath);
            return;
        }

        _logger.LogInformation("qte_runtime.json был нормализован после обнаружения повреждённого/stale runtime state.");
        await _fs.WriteFileAtomicAsync(QteRuntimePath, root.ToJsonString(JsonOpts));
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

    public Task<QteRuntimeState> ReadRuntimeStateAsync() => LoadRuntimeStateAsync();

    public async Task<QteRuntimeState> BeginAcceptedSceneAsync(QteOffer offer, int currentTurnNumber)
    {
        var state = await LoadRuntimeStateAsync();
        state.PendingOffer = offer;
        state.ActiveScene = new ActiveQteSceneState
        {
            Offer = offer,
            CurrentChapterId = !string.IsNullOrWhiteSpace(offer.StartChapterId)
                ? offer.StartChapterId
                : offer.Chapters.FirstOrDefault()?.ChapterId ?? "",
            AcceptedAtTurn = currentTurnNumber
        };
        await SaveRuntimeStateAsync(state);
        ClearOfferFile();
        return state;
    }

    public async Task<QteActionResolution> ResolveActiveActionAsync(
        string actionId,
        string? submittedGrade,
        int currentTurnNumber,
        bool allowPreexistingStateIssues = false)
    {
        var state = await LoadRuntimeStateAsync();
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

        if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
        {
            var outcome = offer.TerminalOutcomes.FirstOrDefault(item =>
                string.Equals(item.OutcomeId, target.TerminalOutcomeId, StringComparison.OrdinalIgnoreCase));
            if (outcome == null)
                throw new InvalidOperationException($"QTE outcome '{target.TerminalOutcomeId}' not found.");

            var finalResponse = await ApplyTerminalOutcomeValidatedStateChangesAsync(
                outcome,
                allowPreexistingStateIssues);
            var summary = $"QTE[{offer.QteId}] -> {outcome.Title} ({DisplayGrade(grade)})";
            await AppendHistoryAsync(offer, outcome, grade, active.AcceptedAtTurn, currentTurnNumber, summary);

            state.PendingOffer = null;
            state.ActiveScene = null;
            state.LastResolvedQteSummaryPendingReminder = $"{summary}. GM summary: {outcome.GmSummary}";
            await SaveRuntimeStateAsync(state);

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
                    Response = finalResponse
                }
            };
        }

        if (string.IsNullOrWhiteSpace(target.NextChapterId))
            throw new InvalidOperationException($"QTE action '{action.ActionId}' has no nextChapterId or terminalOutcomeId.");

        active.CurrentChapterId = target.NextChapterId;
        await SaveRuntimeStateAsync(state);

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

            ShowChapterPrelude(offer, chapter);

            var actionOptions = BuildUniqueOptions(chapter.Actions, action =>
                ConsoleLayout.PlainChoiceLabel($"⚡ {action.Label}", action.Check.Type, $"Сложность {Math.Clamp(action.Check.BaseDifficulty, 1, 5)}"));
            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(actionOptions.Select(item => item.Label).ToList()));
            var action = actionOptions.First(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;

            var grade = await RunCheckAsync(action);
            var target = grade switch
            {
                QteGrade.Success => action.Routing.Success,
                QteGrade.Partial => action.Routing.Partial,
                _ => action.Routing.Fail
            };

            await ShowIntermediateResultAsync(offer, chapter, action, grade);

            if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
            {
                var outcome = offer.TerminalOutcomes.FirstOrDefault(item =>
                    string.Equals(item.OutcomeId, target.TerminalOutcomeId, StringComparison.OrdinalIgnoreCase));
                if (outcome == null)
                    throw new InvalidOperationException($"QTE outcome '{target.TerminalOutcomeId}' not found.");

                var finalResponse = await ApplyTerminalOutcomeAsync(outcome);
                var summary = $"QTE[{offer.QteId}] -> {outcome.Title} ({DisplayGrade(grade)})";
                await AppendHistoryAsync(offer, outcome, grade, active.AcceptedAtTurn, currentTurnNumber, summary);

                state.PendingOffer = null;
                state.ActiveScene = null;
                state.LastResolvedQteSummaryPendingReminder = $"{summary}. GM summary: {outcome.GmSummary}";
                await SaveRuntimeStateAsync(state);

                return new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = outcome.OutcomeId,
                    Summary = summary,
                    Response = finalResponse
                };
            }

            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"QTE action '{action.ActionId}' has no nextChapterId or terminalOutcomeId.");

            active.CurrentChapterId = target.NextChapterId;
            await SaveRuntimeStateAsync(state);
        }
    }

    private void ShowChapterPrelude(QteOffer offer, QteChapter chapter)
    {
        var lines = new List<string>
        {
            $"[bold cyan]🎬 {Markup.Escape(chapter.Title ?? offer.Title ?? "QTE сцена")}[/]",
            ""
        };

        if (!string.IsNullOrWhiteSpace(chapter.Narrative))
            lines.Add($"[white]{Markup.Escape(chapter.Narrative)}[/]");

        lines.Add("");
        lines.Add("[yellow]Нажмите любую клавишу, когда будете готовы продолжить...[/]");

        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        _inputSource.ReadKey(intercept: true);
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

            AnsiConsole.Clear();
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

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(choices));

            if (choice.StartsWith("🖼", StringComparison.Ordinal))
            {
                imageOffered = await ShowSceneImageAsync(imagePrompt, offer.QteId, chapter.ChapterId);
                continue;
            }

            return;
        }
    }

    private async Task<GameResponse> ApplyTerminalOutcomeAsync(QteTerminalOutcome outcome)
    {
        var response = await ApplyTerminalOutcomeValidatedStateChangesAsync(outcome);
        await ShowTerminalOutcomeScreenAsync(outcome);
        return response;
    }

    internal async Task<GameResponse> ApplyTerminalOutcomeValidatedStateChangesAsync(
        QteTerminalOutcome outcome,
        bool allowPreexistingStateIssues = false)
    {
        var response = BuildTerminalOutcomeResponse(outcome);
        var baseline = await CaptureQteNormalizationBaselineAsync(response);
        HashSet<string>? preexistingErrorFingerprints = null;
        if (allowPreexistingStateIssues)
        {
            await _stateManager.RefreshGameStateAsync();
            preexistingErrorFingerprints = (await _validator.ValidateGameStateAsync())
                .Where(issue => issue.Severity == IssueSeverity.Error)
                .Select(BuildValidationIssueFingerprint)
                .ToHashSet(StringComparer.Ordinal);
        }

        try
        {
            await ApplyTerminalOutcomeStateChangesCoreAsync(response, baseline.NormalizerBackupsByPath);
            await _stateManager.RefreshGameStateAsync();

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
            await RestoreQteNormalizationBaselineAsync(baseline);
            await _stateManager.RefreshGameStateAsync();
            throw;
        }
        finally
        {
            CleanupQteNormalizationBaseline(baseline);
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
        var baseline = await CaptureQteNormalizationBaselineAsync(response);
        try
        {
            await ApplyTerminalOutcomeStateChangesCoreAsync(response, baseline.NormalizerBackupsByPath);
            return response;
        }
        catch
        {
            await RestoreQteNormalizationBaselineAsync(baseline);
            throw;
        }
        finally
        {
            CleanupQteNormalizationBaseline(baseline);
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
        GameResponse response,
        IReadOnlyDictionary<string, string> normalizerBackups)
    {
        await _stateDistributor.DistributeAsync(response);
        await ApplyAuthoritativeExperienceAsync(response.ExperienceGained);
        await _normalizer.NormalizeAccumulatedStateAsync(normalizerBackups);
    }

    private async Task<QteNormalizationBaseline> CaptureQteNormalizationBaselineAsync(GameResponse response)
    {
        var baseline = new QteNormalizationBaseline();
        var runId = Guid.NewGuid().ToString("N");
        var fileIndex = 0;

        foreach (var relativePath in CollectQteTrackedPaths(response))
        {
            var content = await _fs.ReadFileAsync(relativePath);
            if (content == null)
            {
                baseline.RestoreBackupsByPath[relativePath] = null;
                continue;
            }

            var sanitizedPath = relativePath.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            var backupPath = $"{QteNormalizerBackupDirectory}/{runId}/{fileIndex:D2}_{sanitizedPath}";
            await _fs.WriteFileAtomicAsync(backupPath, content);
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

    private async Task RestoreQteNormalizationBaselineAsync(QteNormalizationBaseline baseline)
    {
        foreach (var (relativePath, backupPath) in baseline.RestoreBackupsByPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                _fs.DeleteFile(relativePath);
                continue;
            }

            var content = await _fs.ReadFileAsync(backupPath);
            if (content == null)
            {
                _fs.DeleteFile(relativePath);
                continue;
            }

            await _fs.WriteFileAtomicAsync(relativePath, content);
        }
    }

    private void CleanupQteNormalizationBaseline(QteNormalizationBaseline? baseline)
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

            _fs.DeleteFile(backupPath);
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

    private async Task ShowTerminalOutcomeScreenAsync(QteTerminalOutcome outcome)
    {
        var imageOffered = false;

        while (true)
        {
            var choices = new List<string>();
            if (!imageOffered && !string.IsNullOrWhiteSpace(outcome.OutcomeImagePrompt))
                choices.Add(_imageService.GenerateWithoutDisplay
                    ? "🖼 Сгенерировать изображение"
                    : "🖼 Сгенерировать и показать изображение");
            choices.Add("✅ Завершить сцену");

            AnsiConsole.Clear();
            AnsiConsole.Write(new Panel(new Markup(string.Join("\n", new[]
            {
                $"[bold green]{Markup.Escape(outcome.Title)}[/]",
                "",
                $"[white]{Markup.Escape(outcome.FinalNarrative)}[/]"
            })))
            {
                Header = new PanelHeader(" Финал QTE ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Green))
                .AddChoices(choices));

            if (choice.StartsWith("🖼", StringComparison.Ordinal))
            {
                imageOffered = await ShowSceneImageAsync(outcome.OutcomeImagePrompt, "qte_outcome", outcome.OutcomeId);
                continue;
            }

            return;
        }
    }

    private async Task ApplyAuthoritativeExperienceAsync(int? experienceDelta)
    {
        if (!experienceDelta.HasValue || experienceDelta.Value <= 0)
            return;

        const string experiencePath = "game_state/player/experience.json";
        var previousCounter = await ReadAuthoritativeExperienceCounterAsync(experiencePath);
        var currentJson = await _fs.ReadFileAsync(experiencePath);

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

        await _fs.WriteFileAtomicAsync(experiencePath, root.ToJsonString(JsonOpts));

        var currentCounter = await ReadAuthoritativeExperienceCounterAsync(experiencePath);
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

    private async Task<int?> ReadAuthoritativeExperienceCounterAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
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

            RenderMiniGamePanel(
                "Рывок на усилие",
                $"Быстро нажимайте {keyLabels}. Esc - безопасный отказ считается провалом.",
                BuildMashInputProgress(matchedPresses, successTarget, partialTarget, remainingMs));

            await Task.Delay(20);
        }

        return ParseGrade(ResolveMashInputGradeFromCount(matchedPresses, successTarget, partialTarget));
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

    private async Task<QteGrade> RunTimingBarAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        var width = 32;
        var successWidth = Math.Clamp(8 - difficulty + statTier, 3, 12);
        var partialWidth = Math.Clamp(successWidth + 4, successWidth + 1, 16);
        var tickMs = Math.Clamp(110 - (statTier * 5) + (difficulty * 10), 50, 180);
        var successStart = (width - successWidth) / 2;
        var partialStart = (width - partialWidth) / 2;
        var position = 0;
        var direction = 1;

        while (true)
        {
            RenderMiniGamePanel(
                "Timing Bar",
                $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, когда маркер будет в центральной зоне.",
                BuildTimingBar(width, position, successStart, successWidth, partialStart, partialWidth));

            if (TryReadImmediateKey(out var key))
            {
                if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.Spacebar))
                {
                    if (position >= successStart && position < successStart + successWidth)
                        return QteGrade.Success;
                    if (position >= partialStart && position < partialStart + partialWidth)
                        return QteGrade.Partial;
                    return QteGrade.Fail;
                }

                if (key.Key == ConsoleKey.Escape)
                    return QteGrade.Fail;
            }

            await Task.Delay(tickMs);
            position += direction;
            if (position >= width - 1 || position <= 0)
                direction *= -1;
        }
    }

    private async Task<QteGrade> RunPromptChainAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        var steps = Math.Clamp(3 + difficulty - Math.Max(0, statTier - 1), 2, 7);
        var allowedMistakes = statTier >= 2 ? 1 : 0;
        var timeoutMs = Math.Clamp(1100 + (statTier * 150) - (difficulty * 120), 450, 1600);
        var prompts = new[] { ConsoleKey.W, ConsoleKey.A, ConsoleKey.S, ConsoleKey.D, ConsoleKey.E, ConsoleKey.Spacebar };
        var random = new Random();
        var mistakes = 0;

        for (var i = 0; i < steps; i++)
        {
            var prompt = prompts[random.Next(prompts.Length)];
            RenderMiniGamePanel(
                "Prompt Chain",
                $"Нажмите {DisplayKey(prompt)}. Шаг {i + 1} из {steps}.",
                $"Тайм-аут: {timeoutMs} мс | Ошибок допустимо: {allowedMistakes}");

            var pressed = await ReadKeyWithTimeoutAsync(timeoutMs);
            if (pressed == null || !QteKeyInput.MatchesConsoleKey(pressed.Value, prompt))
                mistakes++;

            if (mistakes > allowedMistakes)
                return QteGrade.Fail;
        }

        return mistakes == 0 ? QteGrade.Success : QteGrade.Partial;
    }

    private async Task<QteGrade> RunBalanceMeterAsync(QteCheck check)
    {
        var statTier = await ResolveStatTierAsync(check.PrimaryCharacteristic);
        var difficulty = Math.Clamp(check.BaseDifficulty, 1, 5);
        var random = new Random();
        var value = 50;
        var safeHalfWidth = Math.Clamp(18 - (difficulty * 2) + (statTier * 2), 8, 24);
        var tickMs = Math.Clamp(140 - (statTier * 5) + (difficulty * 10), 70, 220);
        var ticks = 18 + (difficulty * 2);
        var safeTicks = 0;

        for (var i = 0; i < ticks; i++)
        {
            if (TryReadImmediateKey(out var key))
            {
                if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.A) || key.Key == ConsoleKey.LeftArrow)
                    value = Math.Max(0, value - 10);
                else if (QteKeyInput.MatchesConsoleKey(key, ConsoleKey.D) || key.Key == ConsoleKey.RightArrow)
                    value = Math.Min(100, value + 10);
                else if (key.Key == ConsoleKey.Escape)
                    return QteGrade.Fail;
            }

            value = Math.Clamp(value + random.Next(-7 - difficulty, 8 + difficulty), 0, 100);
            if (Math.Abs(value - 50) <= safeHalfWidth)
                safeTicks++;

            RenderMiniGamePanel(
                "Balance Meter",
                $"Удерживайте индикатор в центральной зоне клавишами {QteKeyInput.FormatPromptLabel(ConsoleKey.A)} или {QteKeyInput.FormatPromptLabel(ConsoleKey.D)}.",
                BuildBalanceMeter(value, safeHalfWidth, i + 1, ticks));

            await Task.Delay(tickMs);
        }

        var ratio = (double)safeTicks / ticks;
        return ratio switch
        {
            >= 0.70 => QteGrade.Success,
            >= 0.45 => QteGrade.Partial,
            _ => QteGrade.Fail
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

        while (true)
        {
            RenderMiniGamePanel(
                "Charge Release",
                charging
                    ? $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)} ещё раз, чтобы отпустить заряд."
                    : $"Нажмите {QteKeyInput.FormatPromptLabel(ConsoleKey.Spacebar)}, чтобы начать заряд.",
                BuildChargeMeter(charge, targetStart, targetWidth));

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
    }

    private async Task<int> ResolveStatTierAsync(string characteristic)
    {
        try
        {
            var computed = await _charService.ComputeAsync();
            if (computed.Stats.TryGetValue(characteristic, out var stat))
            {
                return stat.Modified switch
                {
                    <= 10 => -2,
                    <= 20 => -1,
                    <= 40 => 0,
                    <= 60 => 1,
                    <= 80 => 2,
                    _ => 3
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось вычислить характеристику для QTE");
        }

        return 0;
    }

    private async Task<bool> ShowSceneImageAsync(string? imagePrompt, string qteId, string segmentId)
    {
        if (string.IsNullOrWhiteSpace(imagePrompt))
            return false;

        var imageKey = $"qte_{qteId}_{segmentId}";
        return await _imageService.GenerateSceneImageOnceAsync(imagePrompt, imageKey);
    }

    private async Task AppendHistoryAsync(QteOffer offer, QteTerminalOutcome outcome, QteGrade grade, int acceptedAtTurn, int finishedAtTurn, string summary)
    {
        var history = await LoadHistoryAsync();
        history.Add(new QteHistoryEntry
        {
            QteId = offer.QteId,
            Title = offer.Title,
            AcceptedAtTurn = acceptedAtTurn,
            FinishedAtTurn = finishedAtTurn,
            OutcomeId = outcome.OutcomeId,
            Grade = grade.ToString().ToLowerInvariant(),
            Summary = summary
        });

        await _fs.WriteFileAtomicAsync(QteHistoryPath, JsonSerializer.Serialize(history, JsonOpts));
    }

    private async Task<List<QteHistoryEntry>> LoadHistoryAsync()
    {
        var json = await _fs.ReadFileAsync(QteHistoryPath);
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

    private async Task<QteRuntimeState> LoadRuntimeStateAsync()
    {
        var json = await _fs.ReadFileAsync(QteRuntimePath);
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

    private async Task SaveRuntimeStateAsync(QteRuntimeState state)
    {
        await _fs.WriteFileAtomicAsync(QteRuntimePath, JsonSerializer.Serialize(state, JsonOpts));
    }

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
        AnsiConsole.Clear();
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", new[]
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
        });
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

    private static string BuildBalanceMeter(int value, int safeHalfWidth, int currentTick, int totalTicks)
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

        return $"{string.Join("", parts)}\n[dim]Такт {currentTick}/{totalTicks}[/]";
    }

    private static string BuildChargeMeter(int charge, int targetStart, int targetWidth)
    {
        var parts = new List<string>();
        for (var i = 0; i <= 100; i += 5)
        {
            if (Math.Abs(i - charge) < 3)
                parts.Add("[bold yellow]●[/]");
            else if (i >= targetStart && i <= targetStart + targetWidth)
                parts.Add("[green]█[/]");
            else
                parts.Add("[dim]░[/]");
        }

        return string.Join("", parts);
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

    private static string FormatMashInputKeyLabels(IEnumerable<string> acceptedTokens) =>
        string.Join(" или ", acceptedTokens.Select(QteKeyInput.FormatPromptLabel));

    private bool TryReadImmediateKey(out ConsoleKeyInfo key)
    {
        key = default;
        if (!_inputSource.KeyAvailable)
            return false;

        key = _inputSource.ReadKey(intercept: true);
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
    }

    public sealed class QteSceneCompletion
    {
        public string QteId { get; set; } = "";
        public string OutcomeId { get; set; } = "";
        public string Summary { get; set; } = "";
        public GameResponse Response { get; set; } = new();
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
