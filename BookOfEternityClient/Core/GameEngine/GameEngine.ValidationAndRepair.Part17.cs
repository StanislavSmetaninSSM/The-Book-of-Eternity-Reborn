using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{

    private static string BuildValidationRepairRequestInstructions(
        PendingTurnSnapshotResolution pendingSnapshot,
        bool fullTurnResubmissionRequired = false,
        bool boundedPendingResolutionResubmission = false)
    {
        var commonPrefix = boundedPendingResolutionResubmission
            ? "Клиент сохранил принятый client-owned pending-запрос и ещё не публиковал механику или ответ игроку. Не изменяй pending-файл и canonical state напрямую. " +
              "Полностью повтори ответ на исходный input/turn_request.json для того же sessionId/requestId/turnNumber, сохрани все ранее авторизованные изменения и добавь закрывающие effectResolutionReceipts[] по безопасному packet из errors[].actual. " +
              "Answer the current safe packet only. Resubmit the same complete semantic turn with receipts only for that packet; the client carries earlier-wave terminal bindings. The original candidate, mutation authority, and source authority remain immutable/client-owned. " +
              "Пересоздай согласованные command/output surfaces и только затем отправь validation_repair_ready.json. "
            : fullTurnResubmissionRequired
            ? "Отклонённая попытка полностью удалена и восстановлено состояние до хода. Не исправляй baseline-файлы in place. " +
              "Для effect_materialization_repair, resource_semantic_omission_repair и wound_materialization_repair значение fullTurnResubmissionRequired=true означает полный повтор того же связного ответа, а не точечный patch. " +
              "Для wound_materialization_repair выбирай рецепт по обязательному candidateKind. Для construct_wound, repair_wound и narrate_acquisition используй requiredResponseShape.woundDecisions[0].proposal.correctOnly и включи display.acquisitionNarration дословно в полный response. " +
              "Для author_alternative_treatment используй requiredResponseShape.woundTreatmentAuthorings[0].route.correctOnly и requiredResponseShape.woundTreatmentAuthorings[0].diagnosisPath.correctOnly; сохрани authoringRequestRef, decision=author, все неизменяемые поля и позиции массивов. Новое лечение не требует acquisitionNarration нового ранения. NeedsAnotherRepair означает только локальный промежуточный результат, не принятие хода; private alternative repair authority остаётся обязательной, no live alternative repair adapter. " +
              "Сохрани preservedProposal без изменений вне разрешённых исправлений; sealed event/target/opportunity и unrelated response content менять нельзя. " +
              "Заново обработай исходный input/turn_request.json как один полный ответ на тот же ход: пересоздай все изменённые в отклонённой попытке command/output surfaces, обязательно повтори каждую exact actor+route из resubmissionObligations и исправь перечисленные поля. " +
              "requiredResubmissionPaths перечисляет только фактически изменённые GM-authored command/output surfaces, которые должны быть заново записаны до сигнала готовности. " +
              "Client-owned preparation/publication roots клиент восстанавливает или пересоздаёт сам: не пиши и не ожидай в requiredResubmissionPaths system_mods.json, progression_schedule.json, resource_definitions.json, resource_state.json, resource_history.json, resource_owner_authority.json, pending_effect_resolutions.json или effect_identity_index.json. " +
              "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt. "
            : "Текущий ответ/состояние отклонены клиентом. Исправь уже записанные файлы in place, ориентируясь на список ошибок ниже. " +
              "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt. ";
        const string commonSuffix =
            "Если ошибка касается canonical accumulated state (guardians/quests/factions/rival_soul_arcs и т.п.), правь итоговое canonical состояние явно: нужное удаление или замена должны остаться и после повторной нормализации. " +
            "Если клиент переписал этот repair request повторно, используй ТОЛЬКО самые свежие metadata из текущего файла.";

        var repairReadyInstruction = pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Текущий validation_repair_request.json использует diagnostic-only sentinel metadata. Не создавай validation_repair_ready.json по этим sessionId/requestId/turnNumber. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом используй его ids для validation_repair_ready.json. ",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Текущий validation_repair_request.json использует diagnostic-only sentinel metadata. Не создавай validation_repair_ready.json по этим sessionId/requestId/turnNumber. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом используй его ids для validation_repair_ready.json. ",
            _ =>
                "После исправлений создай game_state/control/validation_repair_ready.json с sessionId/requestId/turnNumber. "
        };

        return commonPrefix + repairReadyInstruction + commonSuffix;
    }

    private static (string SessionId, string RequestId, int TurnNumber) BuildProtocolRequestMetadata(
        PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot is
        {
            Status: PendingTurnSnapshotResolutionStatus.Usable,
            Context: not null
        }
            ? (pendingSnapshot.Context.SessionId, pendingSnapshot.Context.RequestId, pendingSnapshot.Context.TurnNumber)
            : (string.Empty, string.Empty, 0);
    }

    private static bool BuildProtocolRequestMetadataDiagnosticOnly(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status is PendingTurnSnapshotResolutionStatus.Missing or PendingTurnSnapshotResolutionStatus.Unusable;
    }

    private static string BuildProtocolRequestMetadataWarning(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Validated pending snapshot context сейчас отсутствует, поэтому sessionId/requestId/turnNumber в этом файле заполнены sentinel-значениями и служат только для диагностики. Не копируй их в validation_repair_ready.json и не используй как active correlation metadata; сначала восстанови pending snapshot context и затем используй metadata из самого свежего client-authored request.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Validated pending snapshot context сейчас unreadable или invalid, поэтому sessionId/requestId/turnNumber в этом файле заполнены sentinel-значениями и служат только для диагностики. Не копируй их в validation_repair_ready.json и не используй как active correlation metadata; сначала восстанови pending snapshot authority/integrity и затем используй metadata из самого свежего client-authored request.",
            _ => string.Empty
        };
    }

    private static string BuildInvalidRepairReadyRepairHint(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом перепиши validation_repair_ready.json валидным JSON.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом перепиши validation_repair_ready.json валидным JSON.",
            _ =>
                "Перезапиши validation_repair_ready.json валидным JSON и скопируй в него точные sessionId/requestId/turnNumber из validation_repair_request.json."
        };
    }

    private static string BuildMismatchedRepairReadyRepairHint(PendingTurnSnapshotResolution pendingSnapshot)
    {
        return pendingSnapshot.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot context, дождись самого свежего client-authored repair request с authoritative metadata и только потом пересоздай validation_repair_ready.json.",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Не копируй sentinel metadata из текущего validation_repair_request.json. Сначала восстанови pending snapshot authority/integrity, дождись самого свежего client-authored repair request с authoritative metadata и только потом пересоздай validation_repair_ready.json.",
            _ =>
                "Пересоздай validation_repair_ready.json и скопируй sessionId/requestId/turnNumber ровно из validation_repair_request.json."
        };
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

    private async Task<(ValidationRepairDispatchState Dispatch, List<ValidationIssue> ReportErrors)> ReportRejectedRepairReadyAsync(
        string source, List<ValidationIssue> baseErrors, int attempt,
        string code, string message, string expected, string actual, string repairHint,
        string expectedSessionGeneration)
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

        var dispatch = await WriteValidationRepairRequestForSessionAsync(
            source,
            reportErrors,
            attempt,
            expectedSessionGeneration);
        return (dispatch, reportErrors);
    }

    /// <summary>
    /// Reads ordinary repair metadata while rejecting an unsolicited continuation envelope.
    /// </summary>
    /// <param name="json">
    /// Ready JSON, or <see langword="null"/> when absent.
    /// </param>
    /// <returns>
    /// Ordinary metadata, or <see langword="null"/> for absent, malformed or continuation-bearing Ready.
    /// </returns>
    private static ValidationRepairReady? ReadValidationRepairReady(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.EnumerateObject().Any(property => string.Equals(
                    property.Name, SpiritualWoundContinuationProtocol.EnvelopeName, StringComparison.OrdinalIgnoreCase)))
                return null;
            return JsonSerializer.Deserialize<ValidationRepairReady>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private bool IsMatchingRepairReady(ValidationRepairReady ready, ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (snapshotContext == null)
            return false;

        return ready.TurnNumber == snapshotContext.TurnNumber &&
               !string.IsNullOrWhiteSpace(ready.RequestId) &&
               string.Equals(ready.RequestId, snapshotContext.RequestId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(ready.SessionId) &&
               string.Equals(ready.SessionId, snapshotContext.SessionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedRepairContext(PendingTurnSnapshotResolution resolution)
    {
        return resolution.Status switch
        {
            PendingTurnSnapshotResolutionStatus.Missing =>
                "Existing validated pending turn snapshot context for the active repair cycle",
            PendingTurnSnapshotResolutionStatus.Unusable =>
                "Readable and validated pending turn snapshot context for the active repair cycle",
            _ when resolution.Context != null =>
                $"sessionId={resolution.Context.SessionId}, requestId={resolution.Context.RequestId}, turnNumber={resolution.Context.TurnNumber}",
            _ => "Readable and validated pending turn snapshot context for the active repair cycle"
        };
    }

    private static string BuildActualRepairContext(ValidationRepairReady ready, PendingTurnSnapshotResolution resolution)
    {
        if (resolution.Status == PendingTurnSnapshotResolutionStatus.Missing)
        {
            return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}; validated pending snapshot context is missing";
        }

        if (resolution.Status == PendingTurnSnapshotResolutionStatus.Unusable)
        {
            return $"ready signal sessionId={ready.SessionId}, requestId={ready.RequestId}, turnNumber={ready.TurnNumber}; validated pending snapshot context is unreadable or invalid";
        }

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
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        DeleteValidationRepairFiles(writeLease);
    }

    private async Task DeleteValidationRepairFilesForSessionAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        DeleteValidationRepairFiles(writeLease);
    }

    private void DeleteValidationRepairFiles(FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
        _fs.DeleteFile(writeLease, ValidationRepairRequestPath);
        _fs.DeleteFile(writeLease, ValidationRepairArtifactStallReportPath);
    }

    private Task DeleteTerminalProtocolFailureRequestAsync()
    {
        if (_fs.FileExists(TerminalProtocolFailureRequestPath))
            _fs.DeleteFile(TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteTerminalProtocolFailureRequestAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        _fs.DeleteFile(writeLease, TerminalProtocolFailureRequestPath);
        return Task.CompletedTask;
    }

    private Task DeleteValidationRepairReadyAsync()
    {
        return DeleteValidationRepairReadyCoreAsync();
    }

    private async Task DeleteValidationRepairReadyCoreAsync()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
    }

    private async Task DeleteValidationRepairReadyForSessionAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        _fs.DeleteFile(writeLease, ValidationRepairReadyPath);
    }

    private async Task WriteValidationRepairFileForSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        await _fs.WriteFileAtomicAsync(writeLease, relativePath, content);
    }

    private async Task<string?> ReadValidationRepairFileForSessionAsync(
        string relativePath,
        string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        return await _fs.ReadFileAsync(writeLease, relativePath);
    }

    private void ThrowIfRepairSessionReplaced(
        FileSystemManager.CanonicalWriteLease writeLease,
        string expectedSessionGeneration)
    {
        if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair cycle belongs to a game session that is no longer current.");
        }
    }

    private async Task ShowTurnErrorMessageAsync(string readyErrorPath)
    {
        ShowTurnErrorMessage(await _fs.ReadFileAsync(readyErrorPath));
    }

    private void ShowTurnErrorMessage(string? errorJson)
    {
        string errorMsg;
        if (errorJson == null)
        {
            errorMsg = "Ошибка ожидания ответа GM";
        }
        else
        {
            try
            {
                using var errorDoc = JsonDocument.Parse(errorJson);
                errorMsg = errorDoc.RootElement.TryGetProperty("error", out var e)
                    ? e.GetString() ?? errorJson
                    : errorJson;
            }
            catch
            {
                errorMsg = errorJson;
            }
        }

        _logger.LogError("Turn ended with an operator-side terminal error: {ErrorMessage}", errorMsg);
        var pressAnyKey = _loc.T("press_any_key");
        AnsiConsole.MarkupLine($"[red]❌ {GameInterface.EscapeMarkup(PlayerSafeTurnErrorTitle)}[/]");
        AnsiConsole.MarkupLine($"[yellow]{GameInterface.EscapeMarkup(PlayerSafeTurnErrorText)}[/]");
        AnsiConsole.MarkupLine($"[grey]{GameInterface.EscapeMarkup(pressAnyKey)}[/]");

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var plainText = string.Join(Environment.NewLine, new[]
            {
                PlayerSafeTurnErrorText,
                pressAnyKey
            });
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = PlayerSafePausedScreenId,
                Mode = AgentConsoleMode.Error,
                Title = PlayerSafeTurnErrorTitle,
                PlainText = plainText,
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "Enter",
                        IsDefault = true
                    }
                ],
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics = []
            }, "Течение мира временно приостановлено.");
        }

        _inputSource.ReadKey(intercept: true);
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
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        var protocolErrors = BuildRejectedActiveReadySignalIssues(sourceLabel, signal, snapshotContext);
        if (!await DiscardMismatchedReadySignalAsync(sourceLabel, signal, snapshotContext, preservePendingSnapshot: true))
            return false;

        await WriteTerminalProtocolFailureRequestAsync($"terminal protocol failure: {sourceLabel}", protocolErrors);
        _fs.DeleteFile("input/turn_request.json");

        AnsiConsole.MarkupLine("[yellow]⚠ Текущий ход не был принят. Мир возвращается к последнему устойчивому состоянию.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последнее устойчивое состояние мира восстановлено.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return true;
    }

    private async Task HandleMissingActiveTerminalOutcomeAsync(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        bool turnCompleteExists,
        bool turnErrorExists)
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
                actual: BuildMissingActiveTerminalOutcomeActual(
                    snapshotContext,
                    turnCompleteExists,
                    turnErrorExists),
                repairHint: "Записывай ровно один terminal signal с точными sessionId/requestId/turnNumber, не удаляй и не перезаписывай его после записи и не смешивай terminal protocol failure с validation repair loop.")
        };

        await WriteTerminalProtocolFailureRequestAsync("missing correlated terminal signal after wait", errors);
        _fs.DeleteFile("input/turn_request.json");
        ClearReadySignals();
        ClearTransientOutputFiles();

        AnsiConsole.MarkupLine("[yellow]⚠ Текущий ход не удалось безопасно завершить. Мир возвращается к последнему устойчивому состоянию.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последнее устойчивое состояние мира восстановлено.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
    }
}
