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

    private async Task<ConcurrentTerminalSignalResolution> ResolveConcurrentActiveTerminalSignalsAsync(
        TerminalSignalSnapshot terminalSignals,
        ReadySignalMetadata? completionSignal,
        ReadySignalMetadata? errorSignal,
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (!terminalSignals.CompletionExists || !terminalSignals.ErrorExists)
        {
            return new ConcurrentTerminalSignalResolution(
                Failed: false,
                UseCompletion: terminalSignals.CompletionExists,
                UseError: terminalSignals.ErrorExists);
        }

        if (snapshotContext == null)
            return new ConcurrentTerminalSignalResolution(false, true, true);

        if (completionSignal != null &&
            IsMatchingReadySignal(completionSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_complete", completionSignal))
        {
            var failed = await HandleRejectedActiveReadySignalAsync(
                "turn_complete",
                completionSignal,
                snapshotContext,
                rollbackSnapshot);
            return new ConcurrentTerminalSignalResolution(failed, false, false);
        }

        if (errorSignal != null &&
            IsMatchingReadySignal(errorSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_error", errorSignal))
        {
            var failed = await HandleRejectedActiveReadySignalAsync(
                "turn_error",
                errorSignal,
                snapshotContext,
                rollbackSnapshot);
            return new ConcurrentTerminalSignalResolution(failed, false, false);
        }

        var completionMatches = completionSignal != null &&
                                IsMatchingReadySignal(completionSignal, snapshotContext) &&
                                HasValidTerminalSignalContract("turn_complete", completionSignal);
        var errorMatches = errorSignal != null &&
                           IsMatchingReadySignal(errorSignal, snapshotContext) &&
                           HasValidTerminalSignalContract("turn_error", errorSignal);

        if (completionMatches && !errorMatches)
        {
            _logger.LogWarning("Удаляется competing terminal error signal во время active wait; success signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_error.json");
            return new ConcurrentTerminalSignalResolution(false, true, false);
        }

        if (errorMatches && !completionMatches)
        {
            _logger.LogWarning("Удаляется competing terminal success signal во время active wait; error signal остаётся authoritative.");
            _fs.DeleteFile("ready/turn_complete.json");
            return new ConcurrentTerminalSignalResolution(false, false, true);
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

        AnsiConsole.MarkupLine("[yellow]⚠ Текущий ход завершился неустойчиво. Мир возвращается к последнему устойчивому состоянию.[/]");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Последнее устойчивое состояние мира восстановлено.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return new ConcurrentTerminalSignalResolution(true, false, false);
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

    private static string BuildMissingActiveTerminalOutcomeActual(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        bool turnCompleteExists,
        bool turnErrorExists)
    {
        var manifestDescription = snapshotContext == null
            ? "pendingSnapshot=missing"
            : $"pendingSnapshot=sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}";
        return $"turn_complete_exists={turnCompleteExists}; turn_error_exists={turnErrorExists}; {manifestDescription}";
    }

    private async Task<ReadySignalMetadata?> TryRecoverGmOutputWithoutTerminalSignalAsync(
        ReadySignalMetadata errorSignal,
        ValidatedPendingTurnSnapshotContext snapshotContext)
    {
        if (!IsRecoverableMissingTerminalSignal(errorSignal))
            return null;

        if (!IsMatchingReadySignal(errorSignal, snapshotContext))
            return null;

        if (!TryResolveTurnRequestTimestampUtc(snapshotContext, out var requestTimestampUtc))
            return null;

        foreach (var requiredFile in RecoverableGmOutputRequiredFiles)
        {
            if (!await HasFreshRecoverableGmOutputArtifactAsync(requiredFile, requestTimestampUtc))
                return null;
        }

        var filesModified = EnumerateRecoverableGmModifiedFiles(requestTimestampUtc).ToArray();
        if (filesModified.Length == 0)
            return null;

        var recoveredSignal = new
        {
            sessionId = snapshotContext.SessionId,
            requestId = snapshotContext.RequestId,
            turnNumber = snapshotContext.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("o"),
            status = "success",
            harnessSource = ClientRecoveredMissingTerminalSignalHarnessSource,
            recoveredFrom = errorSignal.HarnessSource,
            originalError = errorSignal.Error,
            filesModified
        };

        var recoveredSignalJson = JsonSerializer.Serialize(recoveredSignal, JsonOpts);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", recoveredSignalJson);
        _fs.DeleteFile("ready/turn_error.json");
        _logger.LogWarning(
            "Recovered GM output for turn {TurnNumber} after daemon emitted {HarnessSource}; synthesized ready/turn_complete.json with {FileCount} modified files.",
            snapshotContext.TurnNumber,
            errorSignal.HarnessSource,
            filesModified.Length);

        return ParseReadySignalMetadata(recoveredSignalJson, "ready/turn_complete.json");
    }

    private static bool IsRecoverableMissingTerminalSignal(ReadySignalMetadata signal) =>
        string.Equals(signal.HarnessSource, GmBridgeIdleWithoutTerminalSignalHarnessSource, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(signal.HarnessSource, GmOutputWithoutTerminalSignalHarnessSource, StringComparison.OrdinalIgnoreCase);

    private bool TryResolveTurnRequestTimestampUtc(
        ValidatedPendingTurnSnapshotContext snapshotContext,
        out DateTime requestTimestampUtc)
    {
        requestTimestampUtc = DateTime.MinValue;
        var requestPath = _fs.ResolvePath("input/turn_request.json");
        if (File.Exists(requestPath))
        {
            requestTimestampUtc = File.GetLastWriteTimeUtc(requestPath);
            if (requestTimestampUtc > DateTime.MinValue)
                return true;
        }

        if (DateTimeOffset.TryParse(snapshotContext.Manifest.RequestTimestamp, out var requestTimestamp))
        {
            requestTimestampUtc = requestTimestamp.UtcDateTime;
            return true;
        }

        return false;
    }

    private async Task<bool> HasFreshRecoverableGmOutputArtifactAsync(string relativePath, DateTime requestTimestampUtc)
    {
        if (!_fs.FileExists(relativePath))
            return false;

        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var requiredTextProperty = relativePath.EndsWith("narrative_response.json", StringComparison.OrdinalIgnoreCase)
                ? "response"
                : "gm_thoughts_markdown";
            if (!doc.RootElement.TryGetProperty(requiredTextProperty, out var requiredText) ||
                requiredText.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(requiredText.GetString()))
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("timestamp", out var timestamp) ||
                timestamp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(timestamp.GetString(), out var parsedTimestamp))
            {
                return false;
            }

            return parsedTimestamp.UtcDateTime >= requestTimestampUtc;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private IEnumerable<string> EnumerateRecoverableGmModifiedFiles(DateTime requestTimestampUtc)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requiredFile in RecoverableGmOutputRequiredFiles)
        {
            if (_fs.FileExists(requiredFile))
                files.Add(requiredFile);
        }

        if (_fs.FileExists("output/interface_updates.json"))
            files.Add("output/interface_updates.json");

        var sessionRoot = _fs.ResolvePath("");
        foreach (var root in new[] { "output", "game_state", "lore" })
        {
            var absoluteRoot = _fs.ResolvePath(root);
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (var absolutePath in Directory.GetFiles(absoluteRoot, "*.json", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sessionRoot, absolutePath).Replace('\\', '/');
                if (!IsRecoverableFilesModifiedPath(relativePath))
                    continue;

                if (File.GetLastWriteTimeUtc(absolutePath) >= requestTimestampUtc)
                    files.Add(relativePath);
            }
        }

        return files;
    }

    private static bool IsRecoverableFilesModifiedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Contains(".rollback.", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("game_state/control/pending_turn_snapshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return PendingTurnSnapshotAuthority.IsSafeRelativePath(normalized);
    }

    private async Task<ActiveTerminalOutcomeResolution> ResolveFinalActiveTerminalOutcomeAsync(
        ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        TerminalSignalSnapshot terminalSignals)
    {
        var completionSignal = ParseReadySignalMetadata(
            terminalSignals.CompletionJson,
            "ready/turn_complete.json");
        var errorSignal = ParseReadySignalMetadata(
            terminalSignals.ErrorJson,
            "ready/turn_error.json");
        var concurrentResolution = await ResolveConcurrentActiveTerminalSignalsAsync(
            terminalSignals,
            completionSignal,
            errorSignal,
            snapshotContext,
            rollbackSnapshot);
        if (concurrentResolution.Failed)
            return new ActiveTerminalOutcomeResolution { Kind = "failure" };

        if (concurrentResolution.UseCompletion)
        {
            if (completionSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(completionSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_complete", completionSignal))
            {
                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "success",
                    Signal = completionSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, snapshotContext, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        if (concurrentResolution.UseError)
        {
            if (errorSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(errorSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_error", errorSignal))
            {
                var recoveredSignal = await TryRecoverGmOutputWithoutTerminalSignalAsync(errorSignal, snapshotContext);
                if (recoveredSignal != null &&
                    HasValidTerminalSignalContract("turn_complete", recoveredSignal))
                {
                    return new ActiveTerminalOutcomeResolution
                    {
                        Kind = "success",
                        Signal = recoveredSignal
                    };
                }

                return new ActiveTerminalOutcomeResolution
                {
                    Kind = "error",
                    Signal = errorSignal
                };
            }

            if (await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, snapshotContext, rollbackSnapshot))
                return new ActiveTerminalOutcomeResolution { Kind = "failure" };
        }

        await HandleMissingActiveTerminalOutcomeAsync(
            snapshotContext,
            rollbackSnapshot,
            concurrentResolution.UseCompletion,
            concurrentResolution.UseError);
        return new ActiveTerminalOutcomeResolution { Kind = "failure" };
    }

    private List<ValidationIssue> BuildRejectedActiveReadySignalIssues(string sourceLabel,
        ReadySignalMetadata? signal, ValidatedPendingTurnSnapshotContext? snapshotContext)
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

        if (snapshotContext == null)
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
                expected: $"sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}",
                actual: $"sessionId={signal.SessionId}, requestId={signal.RequestId}, turnNumber={signal.TurnNumber}",
                repairHint: "Копируй sessionId/requestId/turnNumber в terminal ready signal ровно из текущего turn_request.json и записывай ready-файл только после завершения всех остальных файлов хода.")
        ];
    }
}
