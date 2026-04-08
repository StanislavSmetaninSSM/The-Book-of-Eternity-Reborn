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
    private async Task EnsureClientOwnedSystemFilesHealthyAsync()
    {
        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        await _qteSceneService.EnsureRuntimeStateHealthyAsync();
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

            await RefreshAcceptedTurnCanonicalStateForValidationAsync(expectedTurn);

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

            await RefreshRuntimeStateAsync();
            return true;
        }
    }

    private async Task RefreshAcceptedTurnCanonicalStateForValidationAsync(int expectedTurn)
    {
        var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn);
        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "Accepted-turn canonical materialization requires a readable pending-turn snapshot baseline.");
        }

        await RefreshCanonicalStateAsync(snapshot);
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
                "Если ошибка касается canonical accumulated state (guardians/quests/factions/rival_soul_arcs и т.п.), правь итоговое canonical состояние явно: нужное удаление или замена должны остаться и после повторной нормализации. " +
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
}

