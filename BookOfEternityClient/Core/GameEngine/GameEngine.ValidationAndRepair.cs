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
    private const string GmBridgeIdleWithoutTerminalSignalHarnessSource = "gm_bridge_idle_without_terminal_signal";
    private const string ClientRecoveredMissingTerminalSignalHarnessSource = "client_recovered_gm_output_without_terminal_signal";
    private static readonly string[] RecoverableGmOutputRequiredFiles =
    [
        "output/narrative_response.json",
        "output/debug_logs.json"
    ];

    private async Task EnsureClientOwnedSystemFilesHealthyAsync()
    {
        await CleanupOrphanedTurnRequestBeforeValidationAsync();
        await _stateManager.RefreshGameStateAsync();
        var preserveControlFilesForTerminalValidation =
            await ShouldPreserveClientOwnedControlFilesForTerminalValidationAsync();

        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        if (!preserveControlFilesForTerminalValidation)
        {
            await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
            await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        }

        await _qteSceneService.EnsureRuntimeStateHealthyAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task CleanupOrphanedTurnRequestBeforeValidationAsync()
    {
        if (!_fs.FileExists("input/turn_request.json"))
            return;

        if (HasTerminalReadySignal() ||
            _fs.FileExists(ValidationRepairRequestPath) ||
            _fs.FileExists(ValidationRepairReadyPath))
            return;

        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable)
            return;

        _logger.LogWarning(
            pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing
                ? "Найден orphaned input/turn_request.json без pending snapshot manifest перед validation. Удаление как stale runtime artifact."
                : "Найден input/turn_request.json с unreadable/invalid validated pending snapshot authority перед validation. Удаление как stale runtime artifact.");
        _fs.DeleteFile("input/turn_request.json");
    }

    private async Task<bool> ValidateCurrentGameStateOrShowErrorsAsync(string source,
        RollbackSnapshot? rollbackSnapshot = null,
        ProgressionControl? progressionControl = null,
        bool allowRepairLoop = false)
    {
        var repairAttempt = 0;
        List<ValidationIssue>? lastRepairErrors = null;
        var lastRepairAttempt = 0;

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
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
                issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
            }
            if (allowRepairLoop && lastRepairErrors is { Count: > 0 })
                issues.AddRange(CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(lastRepairErrors));
            issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
            if (progressionControl != null)
                issues.AddRange(await _progressionSchedule.ValidateAcceptedTurnOutcomeAsync(progressionControl));
            var errors = PrioritizeValidationErrors(issues.Where(i => i.Severity == IssueSeverity.Error)).ToList();

            if (allowRepairLoop)
                errors = await FilterRestoredForbiddenRealmBaselineErrorsAsync(source, errors);

            if (errors.Count == 0)
            {
                if (allowRepairLoop && lastRepairErrors is { Count: > 0 })
                    await AppendClearedValidationRepairTrajectoryAsync(source, lastRepairErrors, lastRepairAttempt);
                await DeleteValidationRepairFilesAsync();
                if (progressionControl != null)
                    await _progressionSchedule.ApplyAcceptedTurnOutcomeAsync(progressionControl);
                return true;
            }

            if (allowRepairLoop && await TryAutoRollbackRealmSegregationViolationsAsync(source, errors))
            {
                await RefreshRuntimeStateAsync();
                continue;
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
            lastRepairErrors = errors;
            lastRepairAttempt = repairAttempt;
            if (!await WaitForContractRepairAsync(source, errors, repairAttempt, rollbackSnapshot))
                return false;
        }
    }

    private async Task<bool> ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
        string source,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext,
        RollbackSnapshot? rollbackSnapshot,
        int expectedTurn,
        ProgressionControl? progressionControl)
    {
        var criticalRepairAttempt = 0;
        List<ValidationIssue>? lastCriticalRepairErrors = null;
        var lastCriticalRepairAttempt = 0;
        using var pendingSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest);

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

                lastCriticalRepairErrors = rawErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                if (!await WaitForContractRepairAsync(source, rawErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            if (!await RefreshAcceptedTurnCanonicalStateForValidationAsync(expectedTurn, activeSnapshotContext))
            {
                criticalRepairAttempt++;
                var baselineErrors = new List<ValidationIssue>
                {
                    new(
                        "game_state/control/pending_turn_snapshot.json",
                        IssueSeverity.Error,
                        "Accepted-turn canonical materialization requires a readable validated pending-turn snapshot baseline.",
                        code: "accepted_turn_invalid_snapshot_baseline",
                        section: "AcceptedTurnCanonicalState",
                        expected: "usable current pending turn snapshot manifest with detached authority and hash-validated canonical baseline files",
                        actual: "validated snapshot baseline is missing, detached-authority-invalid, modified, structurally invalid, or mismatched to the active request context",
                        repairHint: "Восстанови current pending_turn_snapshot.json, detached snapshot authority и canonical snapshot files без tampering; accepted-turn canonical validation должна читать pre-turn baseline только из validated snapshot authority.")
                };

                _logger.LogError(
                    "Critical accepted-turn canonical baseline authority failure after {Source}: {Count} errors",
                    source,
                    baselineErrors.Count);

                lastCriticalRepairErrors = baselineErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                if (!await WaitForContractRepairAsync(source, baselineErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

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

                lastCriticalRepairErrors = canonicalErrors;
                lastCriticalRepairAttempt = criticalRepairAttempt;
                if (!await WaitForContractRepairAsync(source, canonicalErrors, criticalRepairAttempt, rollbackSnapshot))
                    return false;

                continue;
            }

            if (!await ValidateCurrentGameStateOrShowErrorsAsync(source, rollbackSnapshot, progressionControl, allowRepairLoop: true))
                return false;

            if (lastCriticalRepairErrors is { Count: > 0 })
                await AppendClearedValidationRepairTrajectoryAsync(source, lastCriticalRepairErrors, lastCriticalRepairAttempt);
            await CleanupAcceptedTurnCommandSurfacesAsync();
            await RefreshRuntimeStateAsync();
            return true;
        }
    }

    private async Task<bool> ValidatePostAcceptedMaterializedStateWithRepairLoopAsync(
        RollbackSnapshot? rollbackSnapshot)
    {
        var accepted = await ValidateCurrentGameStateOrShowErrorsAsync(
            PostAcceptedMaterializedStateValidationSource,
            rollbackSnapshot,
            progressionControl: null,
            allowRepairLoop: true);

        if (accepted)
            await RefreshRuntimeStateAsync();

        return accepted;
    }

    private async Task<bool> RefreshAcceptedTurnCanonicalStateForValidationAsync(
        int expectedTurn,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext)
    {
        var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn, activeSnapshotContext);
        if (snapshot == null)
            return false;

        await RefreshCanonicalStateAsync(snapshot);
        return true;
    }

    private async Task CleanupAcceptedTurnCommandSurfacesAsync()
    {
        await RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync();
    }

    private async Task RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync()
    {
        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                !root.Remove(GuardianProjectState.QuestProgressUpdatesProperty))
            {
                return;
            }

            await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove accepted-turn Guardian quest progress command surface.");
        }
    }

    private static bool RequiresFreshNarrativePayload(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private List<ValidationIssue> CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(
        IReadOnlyCollection<ValidationIssue> repairedErrors)
    {
        var canonicalRepairCodes = repairedErrors
            .Where(IsCanonicalStateRepairIssue)
            .Select(issue => string.IsNullOrWhiteSpace(issue.Code) ? issue.Category.ToString() : issue.Code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (canonicalRepairCodes.Length == 0)
            return [];

        var requestFullPath = _fs.ResolvePath(ValidationRepairRequestPath);
        if (!File.Exists(requestFullPath))
            return [];

        var requestWrittenAtUtc = File.GetLastWriteTimeUtc(requestFullPath);
        var issues = new List<ValidationIssue>();
        foreach (var outputPath in new[]
                 {
                     "output/narrative_response.json",
                     "output/interface_updates.json"
                 })
        {
            var outputFullPath = _fs.ResolvePath(outputPath);
            if (!File.Exists(outputFullPath))
                continue;

            var outputWrittenAtUtc = File.GetLastWriteTimeUtc(outputFullPath);
            if (outputWrittenAtUtc >= requestWrittenAtUtc)
                continue;

            issues.Add(new ValidationIssue(
                outputPath,
                IssueSeverity.Error,
                $"{outputPath} был записан до canonical validation repair и может противоречить исправленному состоянию.",
                code: "accepted_turn_stale_player_facing_output_after_canonical_repair",
                section: "PlayerFacingOutput",
                expected: $"player-facing output rewritten after canonical state repair request at {requestWrittenAtUtc:o}",
                actual: $"{outputPath} last write {outputWrittenAtUtc:o}; repaired canonical issues: {string.Join(", ", canonicalRepairCodes)}",
                repairHint: "Перепиши player-facing output под уже исправленное canonical state: обнови output/narrative_response.json.response и, если есть варианты выбора, output/interface_updates.json.dialogueOptions. Не меняй canonical state повторно, если validation_repair_request.json не перечисляет новые canonical ошибки."));
        }

        return issues;
    }

    private static bool IsCanonicalStateRepairIssue(ValidationIssue issue)
    {
        var path = NormalizeRepairTargetPath(issue.FilePath);
        return path.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("lore/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryAutoRollbackRealmSegregationViolationsAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors)
    {
        var realmSegregationErrors = errors
            .Where(error => string.Equals(error.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (realmSegregationErrors.Count == 0)
            return false;

        var forbiddenPaths = realmSegregationErrors
            .SelectMany(ExtractRealmSegregationPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (forbiddenPaths.Count == 0)
            return false;

        var sourceRealm = realmSegregationErrors
            .Select(ExtractRealmSegregationSourceRealm)
            .FirstOrDefault(realm => !string.IsNullOrWhiteSpace(realm));

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.TryRollbackForbiddenRealmMutationsAsync(
            sourceRealm,
            forbiddenPaths,
            source);

        if (!result.RolledBack)
            return false;

        _logger.LogWarning(
            "Client auto-rolled back {Count} forbidden realm mutations after {Source}; report: {ReportPath}",
            result.Actions.Count,
            source,
            result.ReportPath);
        return true;
    }

    private async Task<List<ValidationIssue>> FilterRestoredForbiddenRealmBaselineErrorsAsync(
        string source,
        List<ValidationIssue> errors)
    {
        if (errors.Count == 0)
            return errors;

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.FilterRestoredForbiddenBaselineIssuesAsync(
            _stateManager.CurrentState.CurrentRealm,
            errors);

        if (result.SuppressedIssues.Count == 0)
            return errors;

        _logger.LogWarning(
            "Suppressed {Count} restored forbidden-realm baseline validation errors after {Source}; these files match the validated pending-turn snapshot and remain outside GM repair scope for the current realm.",
            result.SuppressedIssues.Count,
            source);

        return PrioritizeValidationErrors(result.RemainingIssues).ToList();
    }

    private static IEnumerable<string> ExtractRealmSegregationPaths(ValidationIssue issue)
    {
        var actual = issue.Actual ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actual))
            yield break;

        var pathList = actual.Split(" | surfaces:", 2, StringSplitOptions.None)[0];
        foreach (var candidate in pathList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = candidate.Replace('\\', '/');
            if (normalized.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase))
                yield return normalized;
        }
    }

    private static string? ExtractRealmSegregationSourceRealm(ValidationIssue issue)
    {
        var match = Regex.Match(
            issue.Message,
            @"pre-turn realm '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
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

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var plainLines = new List<string>
            {
                $"Нарушение контракта GM после {source}",
                "Клиент отклонил состояние как несовместимое с Rules/API.",
                ""
            };

            if (summaryLines.Count > 0)
            {
                plainLines.Add("Основные группы ошибок:");
                plainLines.AddRange(summaryLines.Select(summary => "• " + summary));
                plainLines.Add("");
            }

            foreach (var issue in errors.Take(10))
            {
                plainLines.Add("• " + BuildIssueDisplayLabel(issue));
                if (!string.IsNullOrWhiteSpace(issue.RepairHint))
                    plainLines.Add("  Исправление: " + issue.RepairHint);
            }

            if (errors.Count > 10)
                plainLines.Add($"... и ещё {errors.Count - 10} ошибок");

            plainLines.Add("");
            plainLines.Add(_loc.T("press_any_key"));

            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = "contract-validation-error",
                Mode = AgentConsoleMode.Error,
                Title = "Нарушение контракта GM",
                PlainText = string.Join(Environment.NewLine, plainLines),
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "enter",
                        IsDefault = true
                    }
                ],
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics =
                [
                    new AgentConsoleDiagnostic
                    {
                        Severity = AgentConsoleDiagnosticSeverity.Error,
                        Code = "contract-validation-error",
                        Message = $"Клиент отклонил состояние после {source}."
                    }
                ]
            }, "Rendered contract validation error.");
        }

        _inputSource.ReadKey(intercept: true);
    }

    private async Task<bool> WaitForContractRepairAsync(string source, List<ValidationIssue> errors,
        int attempt, RollbackSnapshot? rollbackSnapshot)
    {
        var metadataDiagnosticOnly = await WriteValidationRepairRequestAsync(source, errors, attempt);
        if (metadataDiagnosticOnly)
            return await FailClosedDiagnosticOnlyValidationRepairAsync(source, errors, attempt, rollbackSnapshot);

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

                        if (_inputSource.KeyAvailable)
                        {
                            var key = _inputSource.ReadKey(intercept: true);
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
            var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
            if (ready == null)
            {
                _logger.LogWarning("Отклонён validation_repair_ready: файл не читается как валидный JSON");
                var rejectedReadyRepair = await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "invalid_repair_ready_json",
                    "Клиент отклонил validation_repair_ready.json: файл не читается как валидный JSON.",
                    "Valid JSON object with matching sessionId/requestId/turnNumber for the active repair cycle",
                    string.IsNullOrWhiteSpace(readyJson) ? "missing or empty file" : TruncateDiagnosticValue(readyJson),
                    BuildInvalidRepairReadyRepairHint(pendingSnapshot));
                if (rejectedReadyRepair.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(source, rejectedReadyRepair.ReportErrors, attempt, rollbackSnapshot);

                await DeleteValidationRepairReadyAsync();
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            if (!IsMatchingRepairReady(ready, pendingSnapshot.Context))
            {
                _logger.LogWarning(
                    "Отклонён validation_repair_ready(session={Session}, request={Request}, turn={Turn}) — ожидается (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
                    ready.SessionId,
                    ready.RequestId,
                    ready.TurnNumber,
                    pendingSnapshot.Context?.SessionId,
                    pendingSnapshot.Context?.RequestId,
                    pendingSnapshot.Context?.TurnNumber);

                var rejectedReadyRepair = await ReportRejectedRepairReadyAsync(
                    source,
                    errors,
                    attempt,
                    "mismatched_repair_ready_context",
                    "Клиент отклонил validation_repair_ready.json: metadata не совпадает с активным repair cycle.",
                    BuildExpectedRepairContext(pendingSnapshot),
                    BuildActualRepairContext(ready, pendingSnapshot),
                    BuildMismatchedRepairReadyRepairHint(pendingSnapshot));
                if (rejectedReadyRepair.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(source, rejectedReadyRepair.ReportErrors, attempt, rollbackSnapshot);

                await DeleteValidationRepairReadyAsync();
                AnsiConsole.MarkupLine("[yellow]⚠ Клиент запросил новую попытку исправления. GM продолжает корректировать данные.[/]");
                await Task.Delay(500);
                continue;
            }

            await AppendAcceptedValidationRepairTrajectoryAsync(source, errors, attempt, ready, pendingSnapshot);
            await DeleteValidationRepairReadyAsync();
            return true;
        }
    }

    private async Task AppendAcceptedValidationRepairTrajectoryAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        ValidationRepairReady ready,
        PendingTurnSnapshotResolution pendingSnapshot)
    {
        const string ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl";

        try
        {
            var context = pendingSnapshot.Context;
            var realmResolution = BuildValidationRepairTrajectoryRealmResolution(context);
            var repairPacketRefs = await BuildValidationRepairTrajectoryRepairPacketRefsAsync(errors);
            var record = new
            {
                recordId = "gmtraj_" + Guid.NewGuid().ToString("N"),
                kind = "repair",
                sessionId = ready.SessionId,
                turnId = ready.RequestId,
                requestId = ready.RequestId,
                turnNumber = ready.TurnNumber,
                realm = realmResolution.Realm,
                realmResolution,
                mode = "validation_repair",
                actionSummary = BuildValidationRepairTrajectoryActionSummary(context),
                contextPackPath = "game_state/control/gm_context_pack",
                templateVersions = new
                {
                    turnOutput = "v1",
                    validationRepair = "v1",
                    progressionReport = "v1",
                    actorReasoning = "v1",
                    tempoAdvantage = "v1"
                },
                outputFiles = Array.Empty<string>(),
                dispatch = new
                {
                    attempts = 0,
                    busyRetries = 0,
                    timeout = false,
                    status = "client_observed_terminal"
                },
                validation = new
                {
                    status = "accepted",
                    source,
                    acceptanceScope = "correlated_repair_ready",
                    fullCanonicalStateAccepted = false,
                    issueKinds = BuildValidationRepairTrajectoryIssueKinds(errors),
                    repairPacketRefs
                },
                repair = new
                {
                    attempts = attempt,
                    status = "accepted"
                },
                workerEvents = Array.Empty<object>(),
                rollbackEvents = Array.Empty<object>(),
                terminal = new
                {
                    kind = "validation_repair_ready",
                    signalPath = ValidationRepairReadyPath
                },
                durationSeconds = (double?)null,
                rubric = new
                {
                    validTurn = true,
                    playerFacingOutputPresent = _fs.FileExists("output/narrative_response.json"),
                    implementationSourceRead = false,
                    rawWrongRealmWrite = false,
                    manualReasoningNeeded = false,
                    missingHarnessTool = (string?)null
                },
                createdAt = DateTime.UtcNow.ToString("o"),
                observedBy = "client"
            };

            var fullPath = _fs.ResolvePath(ledgerPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = false
            });
            await File.AppendAllTextAsync(fullPath, json + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append accepted validation repair trajectory after {Source}. Gameplay continues without ledger entry.",
                source);
        }
    }

    private async Task AppendClearedValidationRepairTrajectoryAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors,
        int attempt)
    {
        const string ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl";

        try
        {
            var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
            var context = pendingSnapshot.Context;
            var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
            var realmResolution = BuildValidationRepairTrajectoryRealmResolution(context);
            var repairPacketRefs = await BuildValidationRepairTrajectoryRepairPacketRefsAsync(errors);
            var record = new
            {
                recordId = "gmtraj_" + Guid.NewGuid().ToString("N"),
                kind = "repair",
                sessionId = requestMetadata.SessionId,
                turnId = requestMetadata.RequestId,
                requestId = requestMetadata.RequestId,
                turnNumber = requestMetadata.TurnNumber,
                realm = realmResolution.Realm,
                realmResolution,
                mode = "validation_repair",
                actionSummary = BuildValidationRepairTrajectoryActionSummary(context),
                contextPackPath = "game_state/control/gm_context_pack",
                templateVersions = new
                {
                    turnOutput = "v1",
                    validationRepair = "v1",
                    progressionReport = "v1",
                    actorReasoning = "v1",
                    tempoAdvantage = "v1"
                },
                outputFiles = Array.Empty<string>(),
                dispatch = new
                {
                    attempts = 0,
                    busyRetries = 0,
                    timeout = false,
                    status = "client_revalidated_terminal"
                },
                validation = new
                {
                    status = "accepted",
                    source,
                    acceptanceScope = "full_canonical_state_after_repair",
                    fullCanonicalStateAccepted = true,
                    issueKinds = BuildValidationRepairTrajectoryIssueKinds(errors),
                    repairPacketRefs
                },
                repair = new
                {
                    attempts = attempt,
                    status = "cleared"
                },
                workerEvents = Array.Empty<object>(),
                rollbackEvents = Array.Empty<object>(),
                terminal = new
                {
                    kind = "validation_repair_cleared",
                    signalPath = (string?)null
                },
                durationSeconds = (double?)null,
                rubric = new
                {
                    validTurn = true,
                    playerFacingOutputPresent = _fs.FileExists("output/narrative_response.json"),
                    implementationSourceRead = false,
                    rawWrongRealmWrite = false,
                    manualReasoningNeeded = false,
                    missingHarnessTool = (string?)null
                },
                createdAt = DateTime.UtcNow.ToString("o"),
                observedBy = "client"
            };

            var fullPath = _fs.ResolvePath(ledgerPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = false
            });
            await File.AppendAllTextAsync(fullPath, json + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append cleared validation repair trajectory after {Source}. Gameplay continues without ledger entry.",
                source);
        }
    }

    private static string[] BuildValidationRepairTrajectoryIssueKinds(IEnumerable<ValidationIssue> errors)
    {
        return errors
            .Select(error => string.IsNullOrWhiteSpace(error.Code)
                ? error.Category.ToString()
                : error.Code!)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<string[]> BuildValidationRepairTrajectoryRepairPacketRefsAsync(IEnumerable<ValidationIssue> errors)
    {
        var refs = new List<string>();

        if (_fs.FileExists(ValidationRepairRequestPath))
        {
            try
            {
                var json = await _fs.ReadFileAsync(ValidationRepairRequestPath);
                var request = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<ValidationRepairRequest>(json, JsonOpts);

                if (request?.HarnessRepairPackets is { Count: > 0 })
                {
                    refs.AddRange(request.HarnessRepairPackets
                        .Select(packet => packet.Kind)
                        .Where(kind => !string.IsNullOrWhiteSpace(kind))!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read validation repair packet refs from active repair request for trajectory ledger.");
            }
        }

        if (refs.Count == 0)
        {
            refs.AddRange(BuildValidationRepairHarnessPackets(
                    PrioritizeValidationErrors(errors).ToList(),
                    await ReadCurrentGuardianRepairActorNameHintsAsync())
                .Select(packet => packet.Kind)
                .Where(kind => !string.IsNullOrWhiteSpace(kind))!);
        }

        return refs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildValidationRepairTrajectoryActionSummary(ValidatedPendingTurnSnapshotContext? context)
    {
        var text = context?.PlayerAction?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        return text.Length <= 160 ? text : text[..157] + "...";
    }

    private sealed class ValidationRepairTrajectoryRealmResolution
    {
        public string Realm { get; init; } = "Unknown";
        public string Source { get; init; } = "unavailable";
        public string RawValue { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }

    private static ValidationRepairTrajectoryRealmResolution BuildValidationRepairTrajectoryRealmResolution(
        ValidatedPendingTurnSnapshotContext? context)
    {
        var rawRealm = context?.ProgressionControl?.CurrentRealm;
        if (!string.IsNullOrWhiteSpace(rawRealm))
        {
            var realm = NormalizeValidationRepairTrajectoryRealm(rawRealm);
            return new ValidationRepairTrajectoryRealmResolution
            {
                Realm = realm,
                Source = "pending_turn_snapshot.progressionControl.currentRealm",
                RawValue = rawRealm,
                Reason = realm == "Unknown" ? "unrecognized_current_realm" : string.Empty
            };
        }

        return new ValidationRepairTrajectoryRealmResolution
        {
            Realm = "Unknown",
            Source = context == null ? "pending_turn_snapshot.unavailable" : "pending_turn_snapshot.progressionControl.currentRealm",
            RawValue = string.Empty,
            Reason = context == null ? "missing_pending_turn_snapshot_context" : "missing_current_realm"
        };
    }

    private static string NormalizeValidationRepairTrajectoryRealm(string realm)
    {
        var normalized = realm.Trim();
        if (string.Equals(normalized, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Море Хаоса", StringComparison.OrdinalIgnoreCase))
        {
            return "ChaosSea";
        }

        if (string.Equals(normalized, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "ShiningAbode";
        }

        if (string.Equals(normalized, "Mortal World", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "MortalWorld", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Смертный мир", StringComparison.OrdinalIgnoreCase))
        {
            return "MortalWorld";
        }

        return "Unknown";
    }

    private async Task<bool> WriteValidationRepairRequestAsync(string source, List<ValidationIssue> errors, int attempt)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var gmInstructions = BuildValidationRepairRequestInstructions(pendingSnapshot);

        var request = new ValidationRepairRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            RevalidationAttempt = attempt,
            GmInstructions = gmInstructions,
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            HarnessRepairPackets = BuildValidationRepairHarnessPackets(
                prioritizedErrors,
                await ReadCurrentGuardianRepairActorNameHintsAsync()),
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
        if (!metadataDiagnosticOnly)
            await RunWorkerValidationRepairIfAvailableAsync(prioritizedErrors, requestMetadata, request.DetectedAtUtc, attempt);
        return metadataDiagnosticOnly;
    }

    private async Task<bool> FailClosedDiagnosticOnlyValidationRepairAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        _logger.LogError(
            "Diagnostic-only validation repair request cannot be completed by GM after {Source}; failing closed instead of waiting for validation_repair_ready.json.",
            source);

        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            attempt,
            reason = "Diagnostic-only validation repair request cannot be completed by GM because active pending-turn metadata is missing or unusable.",
            rollbackAvailable = HasRollbackCapability(rollbackSnapshot),
            summaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            errors = prioritizedErrors.Select(e => new
            {
                code = e.Code ?? "validation_error",
                filePath = e.FilePath,
                severity = e.Severity.ToString(),
                category = e.Category.ToString(),
                message = e.Message,
                actor = e.Actor,
                section = e.Section,
                expected = e.Expected,
                actual = e.Actual,
                repairHint = e.RepairHint
            }).ToList()
        };

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Клиент остановил неремонтопригодный diagnostic-only repair и восстановил состояние из rollback backup.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Клиент остановил неремонтопригодный diagnostic-only repair. Автоматический rollback для этого режима недоступен.[/]");
        }

        await _fs.WriteFileAtomicAsync(ValidationDiagnosticFailureReportPath, JsonSerializer.Serialize(report, JsonOpts));
        await _progressionSchedule.DeleteTransientReportAsync();
        await DeleteValidationRepairFilesAsync();
        return false;
    }

    private static List<ValidationRepairHarnessPacket> BuildValidationRepairHarnessPackets(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var packets = new List<ValidationRepairHarnessPacket>();
        var guardianScopeErrors = errors.Where(IsGuardianScopeRepairIssue).ToList();
        var guardianScopeActorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints);
        var actorReasoningSubpointErrors = errors
            .Where(IsActorReasoningSubpointRepairIssue)
            .Where(issue => !guardianScopeActorNames.Contains(NormalizeRepairActorName(issue.Actor)))
            .ToList();
        var factionIdentityErrors = errors.Where(IsFactionIdentityRepairIssue).ToList();
        var mortalFactionResourceErrors = errors.Where(IsMortalFactionResourceRepairIssue).ToList();
        var mortalBootstrapMaterializationErrors = errors.Where(IsMortalBootstrapMaterializationRepairIssue).ToList();
        var mortalWorldMapAdjacencyErrors = errors.Where(IsMortalWorldMapAdjacencyRepairIssue).ToList();
        var mortalLocationTransitionErrors = errors.Where(IsMortalLocationTransitionRepairIssue).ToList();
        var mortalNpcScopeErrors = errors.Where(IsMortalNpcScopeRepairIssue).ToList();
        var mortalNpcLocationErrors = errors.Where(IsMortalNpcLocationRepairIssue).ToList();
        var mortalNpcInventoryUpdateErrors = errors.Where(IsMortalNpcInventoryUpdateRepairIssue).ToList();
        var mortalNpcFullObjectErrors = errors.Where(IsMortalNpcFullObjectRepairIssue).ToList();
        var mortalNpcRelationshipEnumErrors = errors.Where(IsMortalNpcRelationshipEnumRepairIssue).ToList();
        var mortalNpcReferenceErrors = errors.Where(IsMortalNpcReferenceRepairIssue).ToList();
        var mortalCombatStateErrors = errors.Where(IsMortalCombatStateRepairIssue).ToList();
        var afterlifeChronicleStringArrayErrors = errors.Where(IsAfterlifeChronicleStringArrayRepairIssue).ToList();
        var afterlifeActionCostErrors = errors.Where(IsAfterlifeSpiritualConflictActionCostRepairIssue).ToList();
        var afterlifeConflictRewardErrors = errors.Where(IsAfterlifeSpiritualConflictRewardRepairIssue).ToList();
        var afterlifeEntityProfileScaffoldErrors = errors.Where(IsAfterlifeEntityProfileScaffoldRepairIssue).ToList();
        var npcScopeDeclarationErrors = errors.Where(IsNpcScopeDeclarationRepairIssue).ToList();
        var acceptedTurnOutputArtifactErrors = errors.Where(IsAcceptedTurnOutputArtifactRepairIssue).ToList();

        if (guardianScopeErrors.Count > 0)
            packets.Add(BuildGuardianScopeRepairPacket(guardianScopeErrors, guardianActorNameHints));

        if (npcScopeDeclarationErrors.Count > 0)
            packets.Add(BuildNpcScopeDeclarationRepairPacket(npcScopeDeclarationErrors));

        if (acceptedTurnOutputArtifactErrors.Count > 0)
            packets.Add(BuildAcceptedTurnOutputArtifactRepairPacket(acceptedTurnOutputArtifactErrors));

        if (actorReasoningSubpointErrors.Count > 0)
            packets.Add(BuildActorReasoningSubpointRepairPacket(actorReasoningSubpointErrors));

        if (factionIdentityErrors.Count > 0)
            packets.Add(BuildFactionIdentityRepairPacket(factionIdentityErrors));

        if (mortalFactionResourceErrors.Count > 0)
            packets.Add(BuildMortalFactionResourceRepairPacket(mortalFactionResourceErrors));

        if (mortalBootstrapMaterializationErrors.Count > 0)
            packets.Add(BuildMortalBootstrapMaterializationRepairPacket(mortalBootstrapMaterializationErrors));

        if (mortalWorldMapAdjacencyErrors.Count > 0)
            packets.Add(BuildMortalWorldMapAdjacencyRepairPacket(mortalWorldMapAdjacencyErrors));

        if (mortalLocationTransitionErrors.Count > 0)
            packets.Add(BuildMortalLocationTransitionRepairPacket(mortalLocationTransitionErrors));

        if (mortalNpcScopeErrors.Count > 0)
            packets.Add(BuildMortalNpcScopeRepairPacket(mortalNpcScopeErrors));

        if (mortalNpcLocationErrors.Count > 0)
            packets.Add(BuildMortalNpcLocationRepairPacket(mortalNpcLocationErrors));

        if (mortalNpcInventoryUpdateErrors.Count > 0)
            packets.Add(BuildMortalNpcInventoryUpdateRepairPacket(mortalNpcInventoryUpdateErrors));

        if (mortalNpcFullObjectErrors.Count > 0)
            packets.Add(BuildMortalNpcFullObjectRepairPacket(mortalNpcFullObjectErrors));

        if (mortalNpcRelationshipEnumErrors.Count > 0)
            packets.Add(BuildMortalNpcRelationshipEnumRepairPacket(mortalNpcRelationshipEnumErrors));

        if (mortalNpcReferenceErrors.Count > 0)
            packets.Add(BuildMortalNpcReferenceRepairPacket(mortalNpcReferenceErrors));

        if (mortalCombatStateErrors.Count > 0)
            packets.Add(BuildMortalCombatStateRepairPacket(mortalCombatStateErrors));

        if (afterlifeChronicleStringArrayErrors.Count > 0)
            packets.Add(BuildAfterlifeChronicleStringArrayRepairPacket(afterlifeChronicleStringArrayErrors));

        if (afterlifeActionCostErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictActionCostRepairPacket(afterlifeActionCostErrors));

        if (afterlifeConflictRewardErrors.Count > 0)
            packets.Add(BuildAfterlifeSpiritualConflictRewardRepairPacket(afterlifeConflictRewardErrors));

        if (afterlifeEntityProfileScaffoldErrors.Count > 0)
            packets.Add(BuildAfterlifeEntityProfileScaffoldRepairPacket(afterlifeEntityProfileScaffoldErrors));

        return packets;
    }

    private static bool IsGuardianScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "structured_guardian_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "active_guardian_missing_from_scope", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActorReasoningSubpointRepairIssue(ValidationIssue issue)
    {
        return IsGuardianReasoningSection(issue.Section) &&
               (string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_situation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_thoughts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "missing_actor_actions", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Section, "AfterlifeChronicles", StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_entry_invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_persistent_consequences_not_array", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "afterlife_chronicle_open_threads_not_array", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFactionIdentityRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "faction_full_object_existing_requires_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_conflicting_identity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_null_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_requires_explicit_create_flag", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_command_unknown_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "canonical_faction_sidecar_unknown_faction_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcLocationRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
               (IsMortalNpcRepairPath(issue.FilePath) &&
                (string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "missing_actor_current_location", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsMortalFactionResourceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "canonical_faction_resource_entry_missing_required_fields", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_meta_resources", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "faction_full_object_missing_strategic_goods", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalLocationTransitionRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "current_location_unknown_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "npc_unknown_current_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_active_threat_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_adjacency_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_difficulty_profile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "location_missing_storage_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_link_missing_required_fields", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_coordinates_duplicate_same_turn", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_coordinates_conflict_existing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_requires_null_location_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "world_map_new_location_missing_description", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalWorldMapAdjacencyRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "world_map_adjacency_unknown_target" => true,
            "world_map_link_update_unknown_source" => true,
            "world_map_link_remove_unknown_source" => true,
            "world_map_storage_update_unknown_target" => true,
            "world_map_storage_remove_unknown_target" => true,
            "world_map_threat_add_unknown_target" => true,
            "world_map_threat_add_unknown_same_turn_initial_id" => true,
            "world_map_threat_update_unknown_target" => true,
            "world_map_threat_remove_unknown_target" => true,
            "world_map_threat_complete_unknown_target" => true,
            _ => false
        };
    }

    private static bool IsMortalNpcFullObjectRepairIssue(ValidationIssue issue)
    {
        if (!IsMortalNpcRepairPath(issue.FilePath))
            return false;

        if (IsMortalNpcLocationRepairIssue(issue) || IsMortalNpcInventoryUpdateRepairIssue(issue) || IsMortalNpcRelationshipEnumRepairIssue(issue))
            return false;

        if (string.Equals(issue.Code, "npc_full_object_missing_required_fields", StringComparison.OrdinalIgnoreCase))
            return true;

        var code = issue.Code ?? string.Empty;
        return code.StartsWith("npc_", StringComparison.OrdinalIgnoreCase) &&
               (code.Contains("object", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("shape", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "structured_npc_update_out_of_scope", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNpcScopeDeclarationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "missing_scope_mode" => true,
            "invalid_scope_mode" => true,
            "missing_relevant_actors_field" => true,
            "empty_relevant_actors_for_mode" => true,
            "missing_scope_relevance_reason" => true,
            "missing_out_of_scope_actors_field" => true,
            "missing_scope_out_of_scope_reason" => true,
            "missing_actor_reasoning_section" => true,
            _ => false
        };
    }

    private static bool IsMortalNpcInventoryUpdateRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               string.Equals(issue.Code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapMaterializationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "bootstrap_codex_missing_current_world_entries" => true,
            "mortal_bootstrap_reused_previous_world_lore" => true,
            "canonical_faction_custom_state_missing_required_fields" => true,
            "canonical_faction_custom_state_missing_progression_rule" => true,
            "readable_document_missing_detail_authority" => true,
            "item_invalid_quality" => true,
            "item_missing_accessory_for_slot" => true,
            "item_missing_equipment_slot" => true,
            "current_location_coordinates_mismatch" => true,
            "location_faction_control_invalid_type" => true,
            "world_map_active_threat_missing_archetype" => true,
            "codex_related_entry_unknown_target" => true,
            "npc_contract_unknown_top_level_key" => true,
            "flexible_state_unknown_top_level_key" => true,
            "mortal_relevant_actor_missing_persistence" => true,
            "missing_required_string" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_boolean_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "expected_string_array" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_string_array_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "item_invalid_accessory_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "item_invalid_equipment_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "validation_error" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            _ => false
        };
    }

    private static bool IsMortalBootstrapGenericShapeRepairIssue(ValidationIssue issue)
    {
        return IsMortalBootstrapItemRepairPath(issue.FilePath) ||
               IsMortalBootstrapCodexRepairPath(issue.FilePath) ||
               IsMortalBootstrapCurrentWorldRepairPath(issue.FilePath);
    }

    private static bool IsMortalBootstrapItemRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("game_state/inventory/items.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCodexRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/codex_entries.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCurrentWorldRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRelationshipEnumRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               (string.Equals(issue.Code, "npc_attitude_relationship_tier_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_invalid_cultural_stance", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcReferenceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "npc_journal_unknown_npc_reference", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalCombatStateRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "mortal_combat_state_missing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAfterlifeSpiritualConflictActionCostRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if (!code.StartsWith("afterlife_conflict_", StringComparison.OrdinalIgnoreCase))
            return false;

        return code.Contains("action_cost", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("action_economy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_dice_value_not_authorized", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_maneuver_changes_strain", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAfterlifeSpiritualConflictRewardRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return code.StartsWith("afterlife_conflict_reward_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(code, "afterlife_entity_profile_agency_goals_not_object", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_agency_goal_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_progression_strategy", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_strategy_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_ledger", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_ledger_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipts_not_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipt_not_object", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "incomplete_special_art_learning_receipt", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("invalid_special_art_learning_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("special_art_learning_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAcceptedTurnOutputArtifactRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "accepted_turn_missing_narrative_response" => true,
            "accepted_turn_empty_narrative_response" => true,
            "accepted_turn_stale_narrative_response" => true,
            "accepted_turn_stale_player_facing_output_after_canonical_repair" => true,
            "accepted_turn_invalid_narrative_json_root" => true,
            "accepted_turn_invalid_narrative_json" => true,
            "narrative_response_missing_timestamp" => true,
            "narrative_response_invalid_timestamp" => true,
            "accepted_turn_stale_interface_updates" => true,
            "accepted_turn_invalid_interface_updates_root" => true,
            "accepted_turn_invalid_interface_updates_json" => true,
            "interface_updates_missing_timestamp" => true,
            "interface_updates_invalid_timestamp" => true,
            "interface_updates_missing_payload" => true,
            "interface_updates_unknown_field" => true,
            "missing_gm_thoughts" => true,
            "accepted_turn_stale_debug_logs" => true,
            "invalid_debug_logs_json_root" => true,
            "invalid_debug_logs_json" => true,
            "debug_logs_missing_timestamp" => true,
            "debug_logs_invalid_timestamp" => true,
            _ => false
        };
    }

    private static ValidationRepairHarnessPacket BuildAcceptedTurnOutputArtifactRepairPacket(
        IReadOnlyList<ValidationIssue> outputArtifactErrors)
    {
        var targetFiles = outputArtifactErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/narrative_response.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/narrative_response.json");
        if (outputArtifactErrors.Any(issue =>
                string.Equals(NormalizeRepairTargetPath(issue.FilePath), "output/interface_updates.json", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("output/interface_updates.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("output/interface_updates.json");
        }
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "accepted_turn_output_artifact_repair",
            Priority = "high",
            Title = "Accepted turn output artifact repair",
            TargetFiles = targetFiles,
            ExpectedShape = new List<string>
            {
                "output/narrative_response.json must be a fresh JSON object for the current accepted turn: { \"response\": \"player-facing narrative text\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.",
                "If output/interface_updates.json exists or validation_repair_request.json lists it, rewrite it as a fresh JSON object for the same accepted turn: { \"dialogueOptions\": [ { \"text\": \"visible option\", \"inputValue\": \"player input\" } ], \"timestamp\": \"ISO-8601 UTC timestamp\" }.",
                "output/debug_logs.json must be a fresh JSON object for the current accepted turn: { \"gm_thoughts_markdown\": \"## Охват NPC-анализа\\n...\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.",
                "gm_thoughts_markdown must contain a separate `## Охват NPC-анализа` / `## NPC Scope` section before detailed actor reasoning blocks.",
                "If no NPC, Guardian, faction, or other actor meaningfully acts or changes, explicitly say that the relevant-actor list is empty and why; do not omit gm_thoughts_markdown."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed accepted-turn output artifact errors.",
                "Rewrite output/narrative_response.json with a fresh non-empty response for this same player action; preserve the already accepted narrative meaning instead of inventing a new turn.",
                "If validation_repair_request.json says player-facing output is stale after canonical state repair, base the rewritten narrative/options on the current canonical game_state files, not the pre-repair wording.",
                "If output/interface_updates.json is listed, rewrite its dialogueOptions/inputValue choices so they match the repaired canonical state and current player-facing narrative.",
                "Rewrite output/debug_logs.json.gm_thoughts_markdown with timestamp in output/debug_logs.json. Include `## Охват NPC-анализа`, scope mode, relevant actors, actors outside scope, and short reasoning for every relevant actor when any actor is involved.",
                "Do not touch canonical game_state files unless validation_repair_request.json lists a canonical state error as well.",
                "After both output artifacts are repaired, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## Охват NPC-анализа",
                "Режим: None | Mortal-centric | Guardian-centric | Mixed",
                "Релевантные акторы: <имена через запятую или нет>",
                "Почему они релевантны: <коротко>",
                "Акторы вне охвата: <имена или нет>",
                "Почему они вне охвата: <коротко>",
                "",
                "## Размышления акторов",
                "### <имя актора>",
                "- Текущая локация: <если применимо>",
                "- Ситуация: <кратко>",
                "- Мысли: <кратко>",
                "- Действия: <кратко>"),
            DoNotDo = new List<string>
            {
                "Do not write ready/turn_complete.json for validation repair.",
                "Do not create a new turn, reroll, advance time, or change player choice while repairing missing output artifacts.",
                "Do not leave output/debug_logs.json empty just because no visible NPC changed; write an explicit empty-scope explanation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, GM docs, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildNpcScopeDeclarationRepairPacket(
        IReadOnlyList<ValidationIssue> npcScopeErrors)
    {
        var missingFields = npcScopeErrors
            .Select(issue => issue.Code ?? "npc_scope_validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetFiles = npcScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "npc_scope_declaration_repair",
            Priority = "high",
            Title = "NPC Scope declaration repair",
            TargetFiles = targetFiles,
            MissingFields = missingFields,
            ExpectedShape = new List<string>
            {
                "Repair only output/debug_logs.json.gm_thoughts_markdown unless validation_repair_request.json lists additional state errors.",
                "The NPC Scope block must declare Mode, Relevant actors, Why relevant, Actors outside scope, and Why outside scope.",
                "World-progression, Guardian-centric, and Mixed modes require at least one actor in Relevant actors.",
                "Scene-local may use Relevant actors: нет / none only when no NPC, Guardian, faction, resident, opponent, or other actor-specific structured state changed.",
                "If Relevant actors is not empty, add a reasoning section with a `### <actor name>` block for every listed actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use actor names that are visible in the current turn, current state, pending snapshot, or listed validation errors.",
                "For afterlife spiritual conflict turns, include the player soul, active Guardian or teacher, and current opponent/trial actor when they act, anchor, or receive structured state.",
                "Use Scene-local with empty actors only for purely player/internal output with no structured actor mutations.",
                "Keep the accepted player action and already written canonical state; repair the reasoning declaration instead of inventing a new event."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and output/debug_logs.json.",
                "Replace or patch only gm_thoughts_markdown so it starts with a complete `## NPC Scope` block.",
                "Choose the narrowest correct mode: Scene-local, World-progression, Guardian-centric, or Mixed.",
                "Fill Relevant actors as comma-separated canonical names, or `нет` only if Scene-local and no actor-specific state changed.",
                "Add one reasoning block per relevant actor with current location when applicable, situation, thoughts, and actions.",
                "After the markdown is repaired, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## NPC Scope",
                "- Mode: Scene-local | World-progression | Guardian-centric | Mixed",
                "- Relevant actors: <actor name 1>, <actor name 2> OR нет",
                "- Why relevant: <why these actors directly act, react, anchor the scene, or receive structured state>",
                "- Actors outside scope: <names or нет>",
                "- Why outside scope: <why other mentioned actors do not receive structured updates>",
                "",
                "## Reasoning",
                "### <actor name>",
                "- Current location: <where the actor is now, if applicable>",
                "- Situation: <what changed or what the actor faces this turn>",
                "- Thoughts: <short internal motive or reaction>",
                "- Actions: <what the actor does or preserves this turn>"),
            DoNotDo = new List<string>
            {
                "Do not create a new turn, reroll dice, advance time, or change the player choice while repairing NPC Scope.",
                "Do not delete meaningful actor state just to make Scene-local with empty actors pass.",
                "Do not list an actor in Relevant actors without a matching `### <actor name>` reasoning block.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, templates, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildGuardianScopeRepairPacket(
        IReadOnlyList<ValidationIssue> guardianScopeErrors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var actorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("активный Хранитель из game_state/meta/guardians.json");

        var targetFiles = guardianScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");
        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (guardianScopeErrors.Any(issue => string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/meta/guardian_projects.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/meta/guardian_projects.json");
        }

        var actorList = string.Join(", ", actorNames);
        var headingExamples = string.Join(", ", actorNames.Select(actor => $"### {actor}"));
        var template = BuildGuardianScopeDebugLogTemplate(actorList, actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_scope_repair",
            Priority = "high",
            Title = "Guardian scope and materialized mirror repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                "In game_state/meta/guardians.json, do not treat current activeGuardian or current guardians[] as authority. Rewrite activeGuardian and guardians[] to the kernel-authoritative Guardian state reconstructed from the validated pre-turn snapshot plus authorized same-turn Guardian mutations.",
                "If game_state/meta/guardian_projects.json is listed, rewrite activeProjects to the kernel-authoritative project tracker state from the validated pre-turn snapshot plus authorized same-turn project commands; do not invent or start a project just because the current materialized file contains it.",
                $"In output/debug_logs.json.gm_thoughts_markdown, set NPC scope mode to Guardian-centric or Mixed and include exactly these Guardian names in Relevant actors: {actorList}.",
                $"Add a reasoning block for every listed Guardian. Minimal required heading shapes: {headingExamples}; use client-recognized bullet labels exactly like - Ситуация:, - Мысли:, and - Действия:.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = template,
            DoNotDo = new List<string>
            {
                "Do not copy stale mirror-only activeGuardian aliases into Relevant actors.",
                "Do not use raw guardianId as the actor name in Relevant actors or reasoning headings.",
                "Do not remove the Guardian mutation from the turn just to silence scope validation unless the mutation was actually unintended.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, GM docs, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorReasoningSubpointRepairPacket(
        IReadOnlyList<ValidationIssue> actorReasoningErrors)
    {
        var actorNames = actorReasoningErrors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("актор из ошибки validation_repair_request.json");

        var targetFiles = actorReasoningErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        var actorList = string.Join(", ", actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_reasoning_subpoint_repair",
            Priority = "high",
            Title = "Actor reasoning block/subpoint repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                $"In output/debug_logs.json.gm_thoughts_markdown, repair or create a missing reasoning block for exactly these actors: {actorList}.",
                "Inside every listed actor block, include separate bullet subpoints with these exact client-recognized labels: - Текущая локация:, - Ситуация:, - Мысли:, and - Действия:.",
                "Use - Текущая локация: to state where the NPC is now, whether it remains there, or whether it moves to a known current/same-turn location.",
                "Keep the actor heading shape as ### <actor name>. Do not merge the required subpoints into one paragraph or one bullet.",
                "Preserve unrelated accepted debug log content and do not create a new turn.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = BuildActorReasoningSubpointDebugLogTemplate(actorNames),
            DoNotDo = new List<string>
            {
                "Do not write ready/turn_complete.json for validation repair.",
                "Do not use slash-only labels or a single combined sentence when the validator requested separate subpoints.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildFactionIdentityRepairPacket(
        IReadOnlyList<ValidationIssue> factionIdentityErrors)
    {
        var targetFiles = factionIdentityErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");
        if (!targetFiles.Contains("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "faction_identity_repair",
            Priority = "high",
            Title = "Mortal faction identity repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "game_state/factions/faction_core.json contains canonical factions[] with stable factionId, name/displayName, description, status, reputation/influence, ranks/rankBranches, relations, projects, chronicle, resources, and custom states preserved.",
                "Every faction sidecar entry references an existing canonical factionId from factions[] unless the sidecar entry is removed as invalid speculative data.",
                "Same-turn temporary faction identity remains factionId = null, initialId = <stable temporary id>, isNewFaction = true until the canonical promotion path creates a permanent faction."
            },
            SafeCorrectionRules = new List<string>
            {
                "If the bad id was meant to update an existing faction, replace it with the exact existing canonical factionId from game_state/factions/faction_core.json.",
                "If the story truly introduced a durable missing faction, create the missing faction as a complete factions[] object before any sidecar references it.",
                "If the sidecar is speculative, duplicate, or belongs to a faction that should not exist, remove the invalid sidecar entry or retarget it to an existing canonical factionId.",
                "Preserve unrelated faction ranks, rankBranches, chronicles, relations, projects, resources, reputation, and custom states while repairing identity."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
                "Use game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json as the authority for whether the faction was already permanent before this turn or was still a same-turn temporary faction.",
                "If the matching faction in pending_turn_snapshot has factionId = null plus a non-empty initialId, restore the current object to the same temporary identity shape: factionId = null, the exact initialId, and isNewFaction = true.",
                "If the matching faction in pending_turn_snapshot has a non-empty factionId, use that exact permanent factionId and remove initialId/isNewFaction from the existing faction object.",
                "For unknown faction ids, choose exactly one correction path: reference an existing canonical factionId, create the missing faction as a complete factions[] object, or remove/retarget the invalid sidecar entry.",
                "After restoring the identity shape, preserve the intended turn content updates around the faction instead of replacing the whole file with a minimal skeleton.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent a permanent factionId from the faction name.",
                "Do not convert a same-turn temporary faction into an existing permanent faction just because validation mentions existing faction wording.",
                "Do not leave faction sidecar entries pointing at missing faction ids.",
                "Do not delete unrelated faction ranks, resources, projects, chronicles, or reputation details to silence identity validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalFactionResourceRepairPacket(
        IReadOnlyList<ValidationIssue> factionResourceErrors)
    {
        var targetFiles = factionResourceErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_resources.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_resources.json");
        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");

        var missingFields = CollectRepairMissingFields(factionResourceErrors);
        var expectedShape = new List<string>
        {
            "canonical_faction_resource_entry_missing_required_fields means a canonical faction resource entry was written as a partial delta; repair it into a full resource object.",
            "metaResources entries require resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle.",
            "strategicGoods entries require resourceName, currentStockpile, and incomePerCycle.",
            "Keep faction resources linked to the existing canonical faction and preserve unrelated ranks, branches, projects, chronicles, relations, and custom states."
        };

        if (missingFields.Count > 0)
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");

        var steps = new List<string>
        {
            "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
            "Find the resource entry named by validation_repair_request.json.errors[].filePath and expand it to a full resource object instead of deleting the faction or replacing the file with a minimal skeleton.",
            "If the entry belongs to metaResources, include resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle; if it belongs to strategicGoods, include resourceName, currentStockpile, and incomePerCycle.",
            "Use numeric values for currentStockpile/incomePerCycle/upkeepPerCycle and keep player-facing resource names meaningful in the current Mortal World scene.",
            "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
        };

        if (missingFields.Count > 0)
            steps.Insert(2, $"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_faction_resource_repair",
            Priority = "high",
            Title = "Mortal faction resource entry repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the resource entry in place whenever it represents a real faction resource introduced or updated by the turn.",
                "Remove the entry only if it is speculative, duplicate, or not supported by the accepted scene.",
                "Preserve all unrelated faction state while repairing resource shape."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete the whole faction to silence a resource-entry validation error.",
                "Do not leave resource entries as identity-only stubs or partial deltas.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer resource rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalLocationTransitionRepairPacket(
        IReadOnlyList<ValidationIssue> locationTransitionErrors)
    {
        var issueCodes = locationTransitionErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var actorNames = CollectRepairActorNames(locationTransitionErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = locationTransitionErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/world/current_location.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/current_location.json");
        if (!targetFiles.Contains("game_state/world/world_map.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/world_map.json");
        if (locationTransitionErrors.Any(issue =>
                string.Equals(issue.Code, "npc_unknown_current_location_id", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/npcs/npc_core.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_location_transition_repair",
            Priority = "high",
            Title = "Mortal location transition repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                $"Observed location/map issue codes: {string.Join(", ", issueCodes)}.",
                "Register any durable new location in game_state/world/world_map.json before current_location.json or NPC currentLocationId references it.",
                "game_state/world/current_location.json must reference a known world_map location id, name, region, description, exits, and last-events summary.",
                "Durable location objects must carry required arrays/collections: knownExits, adjacencyMap, factionControl, locationStorages, and activeThreats. Use [] for empty locationStorages/activeThreats/adjacencyMap when no entries exist.",
                "Durable location objects must carry both internalDifficultyProfile and externalDifficultyProfile with combat/environment/social/exploration facets.",
                "World-map link previews must include targetName, targetCoordinates, estimatedInternalDifficultyProfile, and estimatedExternalDifficultyProfile.",
                "NPC currentLocationId values must point only to known ids from world_map; same-turn scene color should not invent canonical ids.",
                "Same-turn world_map new location coordinates must be unique and must not conflict with existing map coordinates."
            },
            SafeCorrectionRules = new List<string>
            {
                "Register the destination in world_map first, then update current_location.json and NPC currentLocationId/currentLocationName to that known id.",
                "Use one stable location id for the destination across world_map, current_location, NPCs, exits, and debug reasoning; do not alternate ids for the same room.",
                "Fix duplicate coordinates by moving one same-turn new location to unique adjacent coordinates or by merging duplicate entries that describe the same place.",
                "If the new place is narrative color inside the current room rather than a durable location, keep current_location unchanged and phrase the action as happening within the existing location.",
                "Do not repair an unknown location by deleting NPCs, quests, faction links, exits, or map history that should still reference the canonical destination."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md before editing game_state/world/current_location.json, game_state/world/world_map.json, or NPC location ids.",
                "Decide whether the destination is a durable location or only narrative color inside the current location.",
                "For a durable destination, create or repair the world_map entry first, including stable id, visible name, description, region, exits, and unique coordinates.",
                "Patch required arrays on every durable/current location object: knownExits, adjacencyMap, factionControl, locationStorages, and activeThreats.",
                "Patch difficulty profile objects on every durable/current location object before completing repair.",
                "Patch each world-map link preview with targetName, targetCoordinates, and both estimated difficulty profiles.",
                "After the map entry exists, update current_location.json and any moved NPC currentLocationId/currentLocationName to the known id/name.",
                "For duplicate same-turn coordinates, assign unique coordinates before completing repair.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not point current_location.json to a location id that is absent from world_map.",
                "Do not point NPC currentLocationId to an unknown location.",
                "Do not create two same-turn locations with identical coordinates unless they are merged into one canonical location.",
                "Do not turn a purely descriptive corner of the current room into a new canonical location just to satisfy a narrative sentence."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalWorldMapAdjacencyRepairPacket(
        IReadOnlyList<ValidationIssue> worldMapAdjacencyErrors)
    {
        var issueCodes = worldMapAdjacencyErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = worldMapAdjacencyErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/world/world_map.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/world_map.json");
        if (!targetFiles.Contains("game_state/world/current_location.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/world/current_location.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_world_map_adjacency_repair",
            Priority = "high",
            Title = "Mortal world-map adjacency/link repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                $"Observed world-map reference codes: {string.Join(", ", issueCodes)}.",
                "world_map adjacency/link/storage/threat references must point to an existing locationId in world_map or to a same-turn newLocations.initialId that is fully materialized in the same repair.",
                "If the target is a durable place the player or NPC can later reach, materialize it as a full world_map location with stable id, visible name, description, region, exits, and coordinates before linking to it.",
                "If the target is only a clue, direction, route hint, or descriptive corner inside the current location, do not create an adjacency/link; keep it in current_location summary/knownExits/point text until it becomes reachable."
            },
            SafeCorrectionRules = new List<string>
            {
                "Materialize the missing location only when the accepted narrative made it a real reachable place or a moved actor's durable destination.",
                "Remove or downgrade a speculative link when the prose only mentioned a route clue, hidden panel, service passage, storage hint, or offscreen direction.",
                "Preserve existing valid locations, coordinates, exits, storages, threats, and current_location text while fixing only the unknown target/source references.",
                "When creating a new location for a link, keep one stable id across world_map, current_location exits, NPC locations, debug reasoning, quests, and map history."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md and game_state/world/world_map.json before repairing unknown target/source world-map links.",
                "For each validation error, inspect the exact path and actual unknown target/source id from validation_repair_request.json.",
                "Decide whether the unknown target is a durable reachable location or only a narrative hint inside the current scene.",
                "For a durable location, add/repair the full world_map location first, then add or correct the adjacency/link/storage/threat reference to that known id.",
                "For a narrative hint, remove the invalid adjacency/link/storage/threat command and preserve the clue in current_location narrative fields or quest log instead.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave adjacency, linkUpdates, storageUpdates, or threat updates pointing to unknown target/source ids.",
                "Do not create a bare id-only location just to satisfy a link; a durable location must be a full location object.",
                "Do not delete unrelated map history or valid exits to silence one unknown target.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer map rules; use this packet, the template, validation request, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalBootstrapMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalBootstrapErrors)
    {
        var issueCodes = mortalBootstrapErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = mortalBootstrapErrors
            .Select(issue => NormalizeMortalBootstrapRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/control/mortal_bootstrap_scaffold.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Insert(0, "game_state/control/mortal_bootstrap_scaffold.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_bootstrap_materialization_repair",
            Priority = "high",
            Title = "Mortal bootstrap materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md",
                "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md",
                "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md"
            },
            ExpectedShape = new List<string>
            {
                $"Repair these bootstrap issue codes first: {string.Join(", ", issueCodes)}.",
                "The first Mortal World turn must materialize the player-facing anchors from game_state/control/mortal_bootstrap_scaffold.json into canonical current-world files.",
                "Current-world codex entries must describe this Mortal World, not afterlife lore or a previous world; sourceFile must start with current_world/ (for example current_world/world_setting.json), not lore/current_world/.",
                "Readable document/book inventory items need matching detail authority so /книги or document-reading surfaces can show contents.",
                "Starting items need canonical quality/rarity, durability when inspectable, and a valid equipment/accessory slot when item type implies one.",
                "Starting inventory items need the full canonical item shape: itemId/existedId, image_prompt, isConsumption, isContainer, requiresTwoHands, contentsPath, durability as a percentage string such as 100% (never bare number 100), and array-shaped text/bonus fields when present.",
                "Item journalEntries must be an array of non-empty string notes, not objects; do not write { text, turn, summary } objects into journalEntries[].",
                "equipmentSlot and accessoryForSlot must use canonical slot names, arrays of canonical slot names, or null; do not invent slots such as Pocket/Hands when the contract expects a fixed enum.",
                "Mortal World Relevant actors must be backed by a persistent NPC/faction/quest/inventory surface, or moved to Actors outside scope when they are only background scenery.",
                "The player character is not an NPC persistence target. If the current protagonist is named in Relevant actors, mark them as player character and do not create NPCsInScene/UpdateNPCs for them.",
                "NPCsInScene is only for actors physically present in currentLocationData. Offscreen voices, people behind a door, nearbyExitLocationId actors, and route pressure do not belong in NPCsInScene for the current room.",
                "Faction custom sidecars must carry full Custom State Objects: stateId/name, currentValue, minValue, maxValue, description, progressionRule { changePerTurn, description }, and thresholds[]; if you only need a narrative note, use faction_core chronicle instead.",
                "Active threats must be full objects, not strings: threatId/name/longTermGoal plus threatArchetype { motivation, method } and impactProfile { primaryTargetType, primaryTargetId, primaryTargetName, primaryImpact, baseImpactValue }. Use canonical enum values or keep activeThreats empty for vague pressure.",
                "current_location coordinates/factionControl must match world_map and use object-shaped faction-control data."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use mortal_bootstrap_scaffold.json as the checklist and patch only targetFiles unless validation errors explicitly name another file.",
                "Preserve the accepted narrative and player-facing hooks; repair their canonical backing data instead of deleting the hooks.",
                "Prefer adding missing detail records, sidecar fields, codex links, and canonical enum values over replacing whole files with minimal skeletons."
            },
            Steps = new List<string>
            {
                "Open game_state/control/mortal_bootstrap_scaffold.json first and compare it with the listed targetFiles.",
                "Patch current-world lore/codex first: add at least one current-world codex entry with sourceFile starting current_world/ and remove stale reused-world references if validation names them.",
                "Patch readable document authority: every readable book/document item needs a concrete text/detail surface linked to that item.",
                "Patch inventory items: canonicalize quality/rarity, write durability as a percentage string such as 100% for intact inspectable items, and use valid equipment/accessory slots for wearable items.",
                "Patch complete item shape fields exactly where validation names them: image_prompt, existedId, isConsumption, isContainer, requiresTwoHands, contentsPath, durability, equipmentSlot, and accessoryForSlot.",
                "Patch item journalEntries as a JSON array of non-empty strings, not objects; preserve useful text by flattening each malformed object into one player-facing note string.",
                "Patch string-array fields as JSON arrays of strings, not scalar text or semicolon-delimited strings.",
                "Patch output/debug_logs.json Relevant actors: keep the current protagonist as player character, materialize real non-player Mortal actors through NPC/faction/quest/inventory surfaces, or move background objects to Actors outside scope with a clear reason.",
                "Patch NPCsInScene location scope: if an actor is behind a door, near nearbyExitLocationId, in another corridor, or only heard offscreen, remove them from NPCsInScene and represent them through narrative/location/quest/faction memory or UpdateNPCs at their actual location only when they are durable known actors.",
                "Patch factions: complete faction custom/progression sidecar fields with full Custom State Objects, or move narrative-only pressure into faction_core chronicle and leave faction_custom customStates empty.",
                "Patch active threats: either write complete Active Threat Objects with canonical enum values, or remove vague string-only threats and represent pressure through location events/faction chronicle.",
                "Patch location/map data: synchronize current_location coordinates with world_map and make factionControl an object-shaped authority.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete the opening book, letter, NPC, faction, map exit, or codex hook just to silence validation.",
                "Do not copy afterlife lore or previous-world lore into current-world bootstrap files.",
                "Do not write item durability as bare numbers such as 100; use percentage strings such as 100%.",
                "Do not write item journalEntries as objects; journalEntries[] entries must be non-empty strings.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer bootstrap rules; use this packet, the scaffold, templates, and validation errors."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcLocationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcLocationErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcLocationErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcLocationErrors, includeNpcCoreWhenMissing: false);
        var touchesNpcCore = targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase);
        var touchesDebugLog = targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase);
        var knownSceneLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sameTurnNewLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC location validation errors."
        };
        if (touchesNpcCore)
        {
            if (knownSceneLocationErrors.Count > 0)
            {
                var expectedRules = knownSceneLocationErrors
                    .Select(issue => string.IsNullOrWhiteSpace(issue.Expected) ? "currentLocationId = currentLocationData.locationId" : issue.Expected.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                steps.Add($"For NPCsInScene entries in a known current scene location, set {string.Join("; ", expectedRules)} and keep initialLocationId as JSON null unless validation separately reports a same-turn new-location initialId for that entry.");
            }
            if (sameTurnNewLocationErrors.Count > 0)
            {
                steps.Add("For NPCsInScene entries in a same-turn new location, set initialLocationId to currentLocationData.initialId or the matching newLocations.initialId, set currentLocationId to JSON null, and keep currentLocationName as the visible location name.");
            }
            steps.Add("If a persistent NPC location is missing in game_state/npcs/npc_core.json, set currentLocationId/currentLocationName from the intended scene or move command; do not leave both location ids empty.");
            steps.Add("Keep NPCsInScene and NPCs entries as full canonical objects; do not replace them with short display-only rows.");
        }
        if (touchesDebugLog)
        {
            steps.Add("In output/debug_logs.json.gm_thoughts_markdown, add a current location / Текущая локация subpoint inside each listed NPC reasoning block.");
            steps.Add("The current-location subpoint must say where the NPC is now and whether the NPC stays there or moves during this turn.");
        }
        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_location_repair",
            Priority = "high",
            Title = "Mortal NPC same-turn location repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Same-turn scene NPCs live in game_state/npcs/npc_core.json under NPCsInScene and/or NPCs as full objects, not as partial name-only stubs.",
                "NPCsInScene is only for actors physically present in currentLocationData; actors heard behind a door, placed near a nearbyExitLocationId, or waiting in another corridor are offscreen for the current scene.",
                "For a known current location: NPCsInScene.currentLocationId = currentLocationData.locationId, initialLocationId = JSON null, currentLocationName = visible current location name.",
                "For a same-turn new location: NPCsInScene.initialLocationId = currentLocationData.initialId or matching newLocations.initialId, currentLocationId = JSON null, currentLocationName = visible current location name.",
                "For an already persistent NPC moved by the turn: keep permanent npcId, set currentLocationId/currentLocationName to the destination, and do not invent a same-turn initialId."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only game_state/npcs/npc_core.json unless another file is explicitly listed by validation_repair_request.json.",
                "Use the current turn/request location authority already present in session state or pending snapshot; do not invent a location id from prose.",
                "Preserve intended NPC personality, goals, relationshipLock, journals, and unrelated NPC entries while repairing only the location fields."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC location rules.",
                "Do not silence the issue by deleting a meaningful NPC from Relevant actors or NPCsInScene.",
                "Do not keep an offscreen voice, nearbyExitLocationId actor, or corridor/door pressure in NPCsInScene for the current room.",
                "Do not set currentLocationId to JSON null for NPCsInScene when validation expects currentLocationId for a known current location.",
                "Do not set currentLocationId to a non-null value for a same-turn new location NPC that validator says must use initialLocationId."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcScopeRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcScopeErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcScopeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcScopeErrors, includeNpcCoreWhenMissing: true);
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_scope_repair",
            Priority = "high",
            Title = "Mortal NPC relevant-actor scope repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every structured Mortal NPC update must be covered by output/debug_logs.json NPC scope: the actor name appears in Relevant actors and has a reasoning block.",
                "The reasoning block must include current location, situation, thoughts, and actions for that actor in the accepted turn.",
                "If the NPC is only offscreen continuity, route color, background pressure, or unchanged existing state, do not emit UpdateNPCs/NPCsInScene/other structured NPC updates for that actor this turn.",
                "Actors outside scope is for named actors not structurally changed this turn; it is not enough when UpdateNPCs changes that actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Add the actor to Relevant actors and a full reasoning block only when the accepted player action truly changes, addresses, observes, or depends on that actor this turn.",
                "Remove the structured NPC update when the actor is merely offscreen, unchanged, or mentioned only as context; preserve the information in narrative, quest log, location summary, or Actors outside scope instead.",
                "Keep canonical NPC names identical across Relevant actors, reasoning headings, and game_state/npcs/npc_core.json.",
                "Preserve unrelated NPC state while repairing only the out-of-scope update or its missing reasoning coverage."
            },
            Steps = new List<string>
            {
                "Open output/debug_logs.json and game_state/npcs/npc_core.json before repairing structured_npc_update_out_of_scope.",
                "For each named actor, decide whether the accepted turn really changed or directly used that NPC.",
                "If yes, add the exact canonical name to Relevant actors and add a matching reasoning block with current location, situation, thoughts, and actions.",
                "If no, remove that actor's structured NPC update from this turn and keep any useful clue in non-NPC structured surfaces such as quest log or current_location summary.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not silence the error by deleting the NPC permanently when only the current turn update is out of scope.",
                "Do not add an actor to Relevant actors without a matching reasoning block.",
                "Do not keep a structured NPC update for an offscreen unchanged actor just because the actor exists in npc_core.json.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer scope rules; use this packet, the validation request, and GM docs."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcInventoryUpdateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcInventoryUpdateErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcInventoryUpdateErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcInventoryUpdateErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_inventory_update_repair",
            Priority = "high",
            Title = "Mortal NPC existing-inventory update repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "UpdateNPCs for an existing NPC must not resend an inventory array/object.",
                "Existing NPC inventory changes must use NPCInventoryAdds, NPCInventoryUpdates, NPCInventoryRemovals, equipment/resource commands, or no inventory command when nothing changed.",
                "Only a genuinely new NPC with NPCId = JSON null and a non-empty initialId may carry initial inventory inside the full UpdateNPCs/NPCsInScene object."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only the listed NPC entries and inventory command surfaces.",
                "If the NPC is existing, remove inventory from UpdateNPCs and keep unrelated profile/location/relationship fields intact.",
                "If the NPC is genuinely new, change identity to NPCId = JSON null plus initialId and keep inventory only as that new NPC's initial carried inventory."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC inventory update validation errors.",
                "Remove inventory from UpdateNPCs for every existing NPC named by validation_repair_request.json.",
                "If an existing NPC's inventory really changed this turn, express the delta through NPCInventoryAdds, NPCInventoryUpdates, NPCInventoryRemovals, equipment/resource commands, or another documented inventory command surface.",
                "If there was no inventory change, delete only the forbidden inventory field and preserve the rest of the NPC object.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete a meaningful NPC to silence an inventory resend error.",
                "Do not keep inventory: [] inside UpdateNPCs for an existing NPC.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC inventory rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcFullObjectRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcFullObjectErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcFullObjectErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcFullObjectErrors, includeNpcCoreWhenMissing: true);
        var missingFields = CollectRepairMissingFields(mortalNpcFullObjectErrors);
        var expectedShape = new List<string>
        {
            "Every meaningful Mortal World NPC update must materialize a full NPC object in game_state/npcs/npc_core.json.",
            "Required profile/social fields include display identity, rarity, worldview, personalityArchetype, culturalStance, race, class, appearanceDescription, history, progressionType, relationshipLevel, attitude, relationshipLock, goals, inventory, and personalityTraits.",
            "Collections such as relationshipLock, goals, inventory, personalityTraits, customProperties, journalEntries, and related arrays stay JSON arrays/objects even when they contain one item."
        };
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before editing game_state/npcs/npc_core.json.",
            "Find each NPC named by the validation errors and expand it to the canonical full NPC object shape instead of leaving a partial row.",
            "Ensure relationshipLock is an object/array in the expected canonical shape, goals is a collection of concrete goals, and personalityTraits is a collection of concrete traits.",
            "Ensure attitude is synchronized with relationshipLevel and culturalStance uses Conformist, Pragmatist, or Dissident."
        };

        if (missingFields.Count > 0)
        {
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");
            steps.Add($"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");
        }

        if (missingFields.Contains("inventory", StringComparer.OrdinalIgnoreCase))
            steps.Add("For a newly created NPC without carried items, add inventory: [] rather than omitting the inventory field.");

        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_full_object_repair",
            Priority = "high",
            Title = "Mortal NPC full object shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the existing NPC object instead of replacing the file with a minimal skeleton.",
                "For background-only names that should not persist, remove them from structured actor/NPC updates instead of creating a partial NPC.",
                "Keep all user-visible NPC prose meaningful; required fields should not be filled with placeholders like unknown/TBD unless the story explicitly supports uncertainty."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete meaningful NPCs or story hooks to avoid filling required fields.",
                "Do not use raw nulls for required strings or collapse single-item arrays into scalars.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC object rules."
            }
        };
    }

    private static List<string> CollectRepairMissingFields(IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            foreach (var part in (issue.Actual ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var field = part;
                const string MissingPrefix = "missing ";
                if (field.StartsWith(MissingPrefix, StringComparison.OrdinalIgnoreCase))
                    field = field[MissingPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(field))
                    fields.Add(field);
            }
        }

        return fields.ToList();
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcRelationshipEnumRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcRelationshipEnumErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcRelationshipEnumErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcRelationshipEnumErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_relationship_enum_repair",
            Priority = "high",
            Title = "Mortal NPC relationship tier and cultural stance repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPC attitude is a canonical Russian relationship tier derived from relationshipLevel.",
                "Allowed attitude tiers: Непримиримый Враг, Противник, Неприязнь, Нейтралитет, Доверие и Расположение, Глубокая Связь, Легендарная Преданность.",
                "NPC culturalStance is one of the canonical enum values: Conformist, Pragmatist, Dissident."
            },
            SafeCorrectionRules = new List<string>
            {
                "Change attitude and/or relationshipLevel together so the tier and score agree.",
                "Use culturalStance enum values exactly as written; put localized prose in separate display/description fields, not in culturalStance.",
                "Preserve the NPC's existing story relationship meaning; do not reset relationshipLevel to zero unless neutral is the intended repair."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md and patch the listed NPC entries in game_state/npcs/npc_core.json.",
                "Map relationshipLevel to one canonical attitude tier: Непримиримый Враг / Противник / Неприязнь / Нейтралитет / Доверие и Расположение / Глубокая Связь / Легендарная Преданность.",
                "Set culturalStance only to Conformist, Pragmatist, or Dissident.",
                "If the relationship meaning in prose conflicts with the numeric level, adjust the smallest necessary field pair and keep a coherent repair note in debug output.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not write English attitude labels such as Friendly/Hostile into NPC attitude.",
                "Do not localize culturalStance into Russian inside the enum field.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer relationship enum rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcReferenceRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcReferenceErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcReferenceErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcReferenceErrors, includeNpcCoreWhenMissing: true);

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_reference_repair",
            Priority = "high",
            Title = "Mortal NPC reference repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "NPCJournals and other NPC sidecar updates must reference an existing NPCId or NPCName from canonical game_state/npcs/npc_core.json.",
                "If the actor became meaningful this turn, materialize a full NPC object first; if the actor was only background color, remove the orphan sidecar/journal update."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not create orphan NPCJournals entries for names that are absent from npc_core.json.",
                "Preserve journal prose only after it is attached to an existing or newly materialized full NPC.",
                "Prefer correcting the reference to an existing NPC over inventing a new NPC when the name clearly points to a current character."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPCJournals or other NPC references.",
                "Compare the referenced NPCId/NPCName with game_state/npcs/npc_core.json.",
                "If the NPC exists, correct the journal/reference to the exact existing NPCId or NPCName.",
                "If the NPC is genuinely introduced by this turn, add/complete the full NPC object in game_state/npcs/npc_core.json before keeping NPCJournals.",
                "If the reference was only background-only color, remove the orphan NPCJournals/reference update instead of creating a partial NPC.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave NPCJournals pointing at unknown names or ids.",
                "Do not create a partial NPC object just to satisfy the reference.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC reference rules."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalCombatStateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalCombatStateErrors)
    {
        var targetFiles = mortalCombatStateErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/combat/enemies.json",
                     "game_state/combat/allies.json",
                     "game_state/combat/combat_log.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_combat_state_repair",
            Priority = "high",
            Title = "Mortal combat state materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_COMBAT_STATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "A Mortal World turn that explicitly resolves open combat and updates XP, active skill mastery, or combat resources must leave a player-inspectable combat surface.",
                "At minimum, write game_state/combat/combat_log.json with a recent combat summary that /бой can show.",
                "If enemies or allies remain tactically relevant after the turn, also write game_state/combat/enemies.json and game_state/combat/allies.json.",
                "If the fight fully ended in the same turn, enemies may be absent or marked defeated, but combat_log.json must still explain what happened."
            },
            SafeCorrectionRules = new List<string>
            {
                "Repair the already accepted combat scene; do not invent a different enemy, reroll dice, or rewrite the player action.",
                "Preserve the XP, skill mastery, and resource changes already written unless validation explicitly rejects them.",
                "Use player-facing Russian summaries in combat_log.json, while keeping canonical JSON keys and enum values valid.",
                "If there is no ongoing combat after the exchange, make that explicit in the combat log instead of leaving /бой empty."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_COMBAT_STATE_TEMPLATE.md and validation_repair_request.json first.",
                "Write or patch game_state/combat/combat_log.json so it summarizes the same combat exchange described in output/narrative_response.json.",
                "If the enemy still exists or its defeated state matters, write game_state/combat/enemies.json with the opponent, health/poise state, actions, and defeated/retreated status.",
                "If named allies participated tactically, write game_state/combat/allies.json with their roles and current state.",
                "Include every touched combat file in the repair completion evidence.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete XP, skill mastery, or player_status changes just to silence this repair.",
                "Do not leave /бой empty after a player-facing open combat scene.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer combat shape; use this packet, the compact template, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictActionCostRepairPacket(
        IReadOnlyList<ValidationIssue> actionCostErrors)
    {
        var targetFiles = actionCostErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = actionCostErrors
            .Select(DescribeAfterlifeActionCostRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);
        var exactFieldCorrections = actionCostErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_action_cost_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict action-cost sequence repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "For every current exchange, actionCostAudit.<side>.before must equal the previous current exchange's actionCostAudit.<side>.after; for the first current exchange, before must equal pre-turn activeConflict.actionEconomy.<side>.current.",
                "For paid actions, actionCostAudit.<side>.after must equal before - effectiveCost; for recovery, after must follow the documented recovery formula and stay within max.",
                "activeConflict.actionEconomy.<side>.current must equal the last current exchange actionCostAudit.<side>.after, or remain at the pre-turn value when the side has no current audit.",
                "Dice values, operationType/finalOperationType, incomingAction, maneuver outcome, specialArtAudit, and matchupAudit must remain authority-bound; repair arithmetic and audit fields without inventing a new exchange."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate repair checklist; its expected/actual values are authoritative for the listed fields.",
                "Recompute actionCostAudit sequentially across activeConflict.exchangeLog from the pre-turn actionEconomy baseline and the previous exchange result; do not copy a later current value backward.",
                "When fixing a before value, also recompute the same side's after and activeConflict.actionEconomy.<side>.current if they depend on that audit.",
                "If the request includes dice authorization errors, replace only the unauthorized dice/audit value with a pre-generated value from pending-turn authority; do not roll new dice manually.",
                "If the request includes maneuver strain errors, keep strain changes out of activeConflict unless the player action was the documented strain-conversion maneuver."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these action-cost/audit fields exactly: {issueSummary}.",
                "Use exactFieldCorrections[] as the machine-readable checklist: set each listed path to expected, then recompute dependent actionEconomy current values.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect activeConflict.exchangeLog in order and recompute actionCostAudit.player/actionCostAudit.opposition before/after values from the previous current exchange.",
                "For every listed exchangeLog[n], patch actionCostAudit.<side>.before to the expected value, then recompute that side's after using effectiveCost or the recovery rule; update activeConflict.actionEconomy.<side>.current to the final audited after value.",
                "Use pending_turn_snapshot and control authority only as read-only baselines for pre-turn action economy, dice, and authorized operations.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not edit game_state/control/pending_turn_snapshot or other authority snapshot files; use pending_turn_snapshot only as a read-only baseline.",
                "Do not change player prose, operation choices, dice rolls, special art ids, or exchange outcomes just to silence arithmetic validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictRewardRepairPacket(
        IReadOnlyList<ValidationIssue> rewardErrors)
    {
        var targetFiles = rewardErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = rewardErrors
            .Select(DescribeAfterlifeRewardRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact rewardAudit paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_reward_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict reward eligibility repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "Currency rewardAudit is allowed only for a resolved contested player victory with diceAudit.outcomeBand = player_success or decisive_player_success.",
                "Negotiated, withdrawn, failed, training-only, stalemate, teaching, or non-contested spiritual conflict outcomes must not grant currency rewards.",
                "If the conflict did not qualify for a reward, preserve the narrative/training outcome and remove rewardAudit/currency deltas instead of upgrading the outcome.",
                "If a reward is legitimately allowed, rewardAudit.realm, currency, conflictId, anti-farm uniqueness, and authority realm must match the resolved conflict proof."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; expected/actual values are authoritative for reward fields.",
                "For afterlife_conflict_reward_not_allowed, remove the currency reward path and any matching currency delta; keep non-currency learning, chronicle, relationship, or narrative consequences if they are otherwise valid.",
                "For reward realm/currency/conflictId errors on a valid victory, patch only rewardAudit identity/currency fields and preserve the resolved exchange outcome.",
                "Do not convert negotiated training into a victory just to keep feathers or light-spark rewards."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed afterlife conflict reward errors in place.",
                $"Patch these rewardAudit fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect the listed recentConflicts[] or activeConflict terminal proof and decide whether diceAudit.outcomeBand is player_success/decisive_player_success.",
                "If the outcome is negotiated/training/withdrawn/non-victory, remove rewardAudit and matching currency reward deltas; keep the conflict resolution, learning evidence, chronicle, and prose unchanged.",
                "If the outcome is a valid reward-bearing victory, patch rewardAudit.realm/currency/conflictId to match the conflict proof and realm authority without changing dice or outcome.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not upgrade a negotiated, training-only, withdrawn, or failed conflict into a victory to preserve a reward.",
                "Do not reroll dice, change exchangeLog outcomes, or invent a new conflictId just to satisfy reward validation.",
                "Do not delete valid non-reward consequences such as special-art learning evidence, relationship changes, or afterlife chronicles unless validation explicitly rejects them.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer reward rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeEntityProfileScaffoldRepairPacket(
        IReadOnlyList<ValidationIssue> profileErrors)
    {
        var targetFiles = profileErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_entity_profiles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_entity_profiles.json");

        var actorNames = CollectRepairActorNames(profileErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (actorNames.Count == 0)
            actorNames.Add("afterlife entity profile from validation_repair_request.json");

        var issueDetails = profileErrors
            .Select(DescribeAfterlifeEntityProfileScaffoldRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact profile fields and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_entity_profile_scaffold_repair",
            Priority = "high",
            Title = "Afterlife entity profile scaffold and special-art learning repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every significant afterlife entity profile must keep goals as an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn.",
                "progressionStrategy is a required object with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and optional lastUpdatedAtTurn.",
                "ledger is a required array; use [] when no visible profile events exist yet. progressionLedger is optional but must be an array of complete entries if present.",
                "profileCommands.specialArtLearningReceipts[] entries require receiptId, artId, teacherActorType, teacherActorId, playerActorId, trainingConditionSatisfied=true, learnedAtTurn, roleplayEvidence, summary, and initialTier absent or 0.",
                "A special-art learning receipt must not grant a higher tier; make sure the source teacher art canTeachPlayer=true."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; patch only the listed profile/receipt fields unless adjacent minimal scaffold is required to validate.",
                "Repair missing profile scaffold in place; do not delete the profile, teacher, learned art, or relationship evidence to silence shape errors.",
                "Use player-facing Russian prose in summaries/goals while keeping canonical JSON keys and enum-like values valid.",
                "If special-art learning happened, preserve the learning proof and complete the receipt shape instead of converting it into unrelated narrative only."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and game_state/meta/afterlife_entity_profiles.json first.",
                $"Patch this minimum profile scaffold and receipt checklist: {issueSummary}.",
                "For every listed profile, make goals an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn; do not use an array for goals.",
                "Add or repair progressionStrategy with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and lastUpdatedAtTurn when known.",
                "Add ledger: [] when missing, or repair each ledger[] entry to an object with entryId, summary, and optional valid turnNumber.",
                "For profileCommands.specialArtLearningReceipts[], complete receiptId/artId/teacherActorType/teacherActorId/playerActorId/trainingConditionSatisfied/learnedAtTurn/roleplayEvidence/summary and keep initialTier absent or 0.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not move afterlife profile state into Mortal World NPC/player files.",
                "Do not delete learned special art data, teacher proof, goals, or relationship hooks just to avoid completing the scaffold.",
                "Do not grant initialTier > 0 through specialArtLearningReceipts; upgrades must happen through the documented progression/upgrade path.",
                "Do not collapse arrays such as priorityOrder, allowedSpends, forbiddenSpends, ledger, or progressionLedger into scalar strings.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer profile scaffold rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeChronicleStringArrayRepairPacket(
        IReadOnlyList<ValidationIssue> chronicleErrors)
    {
        var targetFiles = chronicleErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_chronicles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_chronicles.json");

        var issueDetails = chronicleErrors
            .Select(DescribeAfterlifeChronicleStringArrayRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_chronicle_string_array_repair",
            Priority = "high",
            Title = "Afterlife chronicle string-array shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "GM-authored changes use afterlifeChronicleUpdates[]; canonical game_state/meta/afterlife_chronicles.json stores chronicles[].",
                "persistentConsequences[] must be an array of non-empty strings. Each string is one durable consequence; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "openThreads[] must be an array of non-empty strings. Each string is one unresolved hook; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "eventDescriptions[] is canonical archive memory and must not be added to afterlifeChronicleUpdates[]. Keep lastEventsDescription as the current readable summary."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; its file paths and expected/actual values are authoritative.",
                "Convert each invalid persistentConsequences/openThreads element into one concise player-meaningful string, preserving the same narrative meaning.",
                "If the invalid value is an object, summarize its meaning into a single sentence string instead of keeping nested keys.",
                "Patch only afterlife chronicle fields named by the errors unless the same chronicle entry needs a minimal adjacent string-array correction to validate."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these afterlife chronicle string-array fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_chronicles.json, inspect each listed chronicles[n].persistentConsequences/openThreads path and replace every invalid element with a non-empty string.",
                "Remove afterlifeChronicleUpdates from output/narrative_response.json if it is present there; output/narrative_response.json may contain only response and timestamp.",
                "Keep afterlifeChronicleUpdates only on the accepted afterlife chronicle update surface for game_state/meta/afterlife_chronicles.json, then repair the listed canonical chronicle fields.",
                "Preserve chronicleId, scopeType, scopeId, displayName, lastEventsDescription, and lastUpdatedTurn unless those exact fields are listed as validation errors.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not put afterlifeChronicleUpdates inside output/narrative_response.json; use the afterlife chronicle surface for game_state/meta/afterlife_chronicles.json.",
                "Do not put eventDescriptions[] inside afterlifeChronicleUpdates[]; archive eventDescriptions are read-only canonical memory.",
                "Do not replace the afterlife chronicle with Mortal worldEventsLog, NPC journals, faction chronicles, or location news.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }

    private static string DescribeAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"chronicles\[(?<index>\d+)\]\.(?<field>persistentConsequences|openThreads)(?:\[(?<entry>\d+)\])?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["entry"].Value)
                ? $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
                : $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}[{match.Groups["entry"].Value}]"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeAfterlifeRewardRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:recentConflicts|resolvedConflicts)\[(?<index>\d+)\](?:\.(?<field>rewardAudit(?:\.[A-Za-z0-9_]+)?|conflictId|diceAudit(?:\.[A-Za-z0-9_]+)?))?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["field"].Value)
                ? $"recentConflicts[{match.Groups["index"].Value}]"
                : $"recentConflicts[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:profiles\[(?<profileIndex>\d+)\]\.(?<profileField>[A-Za-z0-9_.\[\]]+)|profileCommands\.specialArtLearningReceipts\[(?<receiptIndex>\d+)\](?:\.(?<receiptField>[A-Za-z0-9_]+))?)",
            RegexOptions.IgnoreCase);
        string location;
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["profileIndex"].Value))
        {
            location = $"profiles[{match.Groups["profileIndex"].Value}].{match.Groups["profileField"].Value}";
        }
        else if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["receiptIndex"].Value))
        {
            location = string.IsNullOrWhiteSpace(match.Groups["receiptField"].Value)
                ? $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}]"
                : $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}].{match.Groups["receiptField"].Value}";
        }
        else
        {
            location = NormalizeRepairTargetPath(normalizedPath);
        }

        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static ValidationRepairExactFieldCorrection BuildExactFieldCorrection(ValidationIssue issue)
    {
        return new ValidationRepairExactFieldCorrection
        {
            Path = issue.FilePath?.Trim() ?? "",
            Expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim(),
            Actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim(),
            Code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim()
        };
    }

    private static string DescribeAfterlifeActionCostRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"exchangeLog\[(?<index>\d+)\]\.actionCostAudit\.(?<side>player|opposition)\.(?<field>[A-Za-z0-9_]+)",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? $"exchangeLog[{match.Groups["index"].Value}] {match.Groups["side"].Value}.{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static List<string> BuildMortalNpcTargetFiles(
        IReadOnlyList<ValidationIssue> errors,
        bool includeNpcCoreWhenMissing)
    {
        var targetFiles = errors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeNpcCoreWhenMissing &&
            !targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/npcs/npc_core.json");
        }

        return targetFiles;
    }

    private async Task<IReadOnlyCollection<string>> ReadCurrentGuardianRepairActorNameHintsAsync()
    {
        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return Array.Empty<string>();

            var names = new List<string>();
            AddGuardianRepairActorNameHint(names, root["activeGuardian"]);
            if (root["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians)
                    AddGuardianRepairActorNameHint(names, guardian);
            }

            return names
                .Select(NormalizeRepairActorName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static HashSet<string> CollectRepairActorNames(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? fallbackHints = null)
    {
        var names = errors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0 && fallbackHints is { Count: > 0 })
        {
            foreach (var actor in fallbackHints.Select(NormalizeRepairActorName).Where(actor => !string.IsNullOrWhiteSpace(actor)))
                names.Add(actor);
        }

        return names;
    }

    private static void AddGuardianRepairActorNameHint(List<string> names, JsonNode? node)
    {
        if (node is not JsonObject guardian)
            return;

        foreach (var key in new[] { "displayName", "name", "guardianName" })
        {
            var value = guardian[key]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                names.Add(value);
                return;
            }
        }
    }

    private static bool IsGuardianReasoningSection(string? section)
    {
        return string.Equals(section, "npc_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "npc_reasoning", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "Guardians", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "UpdateGuardians", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRepairActorName(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return string.Empty;

        return actor.Trim().TrimEnd('.', ',', ';', ':', '!', '?').Trim();
    }

    private static string NormalizeMortalBootstrapRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var bootstrapFile in new[]
                 {
                     "game_state/control/mortal_bootstrap_scaffold.json",
                     "lore/codex_entries.json",
                     "game_state/inventory/items.json",
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json"
                 })
        {
            if (normalized.StartsWith(bootstrapFile, StringComparison.OrdinalIgnoreCase))
                return bootstrapFile;
        }

        if (normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase))
        {
            var jsonIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
            return jsonIndex >= 0 ? normalized[..(jsonIndex + ".json".Length)] : normalized;
        }

        return NormalizeRepairTargetPath(path);
    }

    private static string NormalizeRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardians.json";
        if (normalized.StartsWith("game_state/meta/guardian_projects.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardian_projects.json";
        if (normalized.StartsWith("game_state/meta/afterlife_chronicles.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_chronicles.json";
        if (normalized.StartsWith("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_spiritual_conflict_state.json";
        if (normalized.StartsWith("game_state/world/current_location.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/current_location.json";
        if (normalized.StartsWith("game_state/world/world_map.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/world_map.json";
        if (normalized.StartsWith("output/narrative_response.json", StringComparison.OrdinalIgnoreCase))
            return "output/narrative_response.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";
        foreach (var npcFile in new[]
                 {
                     "game_state/npcs/npc_core.json",
                     "game_state/npcs/npc_interaction_journal.json",
                     "game_state/npcs/npc_masks.json",
                     "game_state/npcs/npc_memory.json"
                 })
        {
            if (normalized.StartsWith(npcFile, StringComparison.OrdinalIgnoreCase))
                return npcFile;
        }
        foreach (var factionFile in new[]
                 {
                     "game_state/factions/faction_core.json",
                     "game_state/factions/faction_structure.json",
                     "game_state/factions/faction_resources.json",
                     "game_state/factions/faction_projects.json",
                     "game_state/factions/faction_custom.json",
                     "game_state/factions/faction_chronicles.json"
                 })
        {
            if (normalized.StartsWith(factionFile, StringComparison.OrdinalIgnoreCase))
                return factionFile;
        }
        return normalized;
    }

    private static string BuildGuardianScopeDebugLogTemplate(string actorList, IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Охват NPC-анализа");
        builder.AppendLine("Режим: Guardian-centric");
        builder.AppendLine($"Релевантные акторы: {actorList}");
        builder.AppendLine("Почему они релевантны: ход меняет или использует активного Хранителя, поэтому его нужно явно покрыть reasoning scope.");
        builder.AppendLine("Акторы вне охвата: нет");
        builder.AppendLine("Почему они вне охвата: все измененные Guardian-сущности перечислены выше.");
        builder.AppendLine();
        builder.AppendLine("## Guardian Thoughts");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Ситуация: кратко опиши текущую ситуацию Хранителя в этом ходе.");
            builder.AppendLine("- Мысли: кратко опиши мотивы/оценку Хранителя.");
            builder.AppendLine("- Действия: кратко опиши, что Хранитель делает или меняет в состоянии.");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildActorReasoningSubpointDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Размышления акторов");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: кратко укажи, где NPC находится сейчас и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: кратко опиши, в каком положении актор находится в этом ходе.");
            builder.AppendLine("- Мысли: кратко опиши мотивы, оценку или внутреннюю реакцию актора.");
            builder.AppendLine("- Действия: кратко опиши, что актор делает, решает или меняет в состоянии.");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private async Task RunWorkerValidationRepairIfAvailableAsync(
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        (string SessionId, string RequestId, int TurnNumber) requestMetadata,
        string createdAtUtc,
        int attempt)
    {
        try
        {
            var audit = new GmWorkerAuditLog(_fs);
            var delegator = new GmWorkerValidationRepairDelegator(
                _fs,
                new GmWorkerBridgePool(_fs, new GmWorkerProposalStore(_fs), audit),
                new GmWorkerApplyGate(
                    _fs,
                    async () => (IReadOnlyList<ValidationIssue>)await _validator.ValidateGameStateAsync(),
                    audit),
                audit);
            var result = await delegator.TryRunAsync(
                _stateManager.Settings.GmWorkerBridgeProfiles,
                prioritizedErrors,
                new WorkerTurnReference
                {
                    SessionId = requestMetadata.SessionId,
                    RequestId = requestMetadata.RequestId,
                    TurnNumber = requestMetadata.TurnNumber
                },
                createdAtUtc,
                attempt);

            if (result.Outcome is not GmWorkerValidationRepairOutcome.SkippedNoWorker and
                not GmWorkerValidationRepairOutcome.Applied)
            {
                _logger.LogWarning(
                    "GM worker validation repair ended with {Outcome}: {Reason}. Legacy repair loop remains active.",
                    result.Outcome,
                    result.FallbackReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run GM worker validation repair. Legacy repair loop remains active.");
        }
    }

    private async Task WriteTerminalProtocolFailureRequestAsync(string source, List<ValidationIssue> errors)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var degradedMetadataWarning = BuildProtocolRequestMetadataWarning(pendingSnapshot);
        var gmInstructions =
            "Клиент отклонил terminal ready signal как protocol failure. Это НЕ validation_repair_request.json и НЕ repair loop. " +
            "Не создавай validation_repair_ready.json и не пытайся продолжать этот уже закрытый wait cycle. " +
            "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt, разберись с terminal protocol problem по списку ошибок ниже и исправь логику для следующего корректного хода.";
        if (!string.IsNullOrWhiteSpace(degradedMetadataWarning))
            gmInstructions += " " + degradedMetadataWarning;

        var request = new TerminalProtocolFailureRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            GmInstructions = gmInstructions,
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

    private static string BuildValidationRepairRequestInstructions(PendingTurnSnapshotResolution pendingSnapshot)
    {
        const string commonPrefix =
            "Текущий ответ/состояние отклонены клиентом. Исправь уже записанные файлы in place, ориентируясь на список ошибок ниже. " +
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

    private async Task<(bool MetadataDiagnosticOnly, List<ValidationIssue> ReportErrors)> ReportRejectedRepairReadyAsync(
        string source, List<ValidationIssue> baseErrors, int attempt,
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

        var metadataDiagnosticOnly = await WriteValidationRepairRequestAsync(source, reportErrors, attempt);
        return (metadataDiagnosticOnly, reportErrors);
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

        var recoveryText = "Действие не было применено. Состояние возвращается к последней стабильной версии; после возврата к ходу можно повторить действие или выбрать другой путь.";
        var pressAnyKey = _loc.T("press_any_key");
        AnsiConsole.MarkupLine($"[red]❌ Ошибка GM: {GameInterface.EscapeMarkup(errorMsg)}[/]");
        AnsiConsole.MarkupLine($"[yellow]{GameInterface.EscapeMarkup(recoveryText)}[/]");
        AnsiConsole.MarkupLine($"[grey]{GameInterface.EscapeMarkup(pressAnyKey)}[/]");

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            var plainText = string.Join(Environment.NewLine, new[]
            {
                "Ошибка GM",
                errorMsg,
                "",
                recoveryText,
                pressAnyKey
            });
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = "gm-turn-error",
                Mode = AgentConsoleMode.Error,
                Title = "Ошибка GM",
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
                Diagnostics =
                [
                    new AgentConsoleDiagnostic
                    {
                        Severity = AgentConsoleDiagnosticSeverity.Error,
                        Code = "gm-turn-error",
                        Message = "GM turn ended with a terminal error.",
                        Detail = errorMsg
                    }
                ]
            }, "Rendered GM turn error.");
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

    private async Task HandleMissingActiveTerminalOutcomeAsync(ValidatedPendingTurnSnapshotContext? snapshotContext,
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
                actual: BuildMissingActiveTerminalOutcomeActual(snapshotContext),
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

    private async Task<bool> ResolveConcurrentActiveTerminalSignalsAsync(ValidatedPendingTurnSnapshotContext? snapshotContext,
        RollbackSnapshot? rollbackSnapshot)
    {
        if (!_fs.FileExists("ready/turn_complete.json") || !_fs.FileExists("ready/turn_error.json"))
            return false;

        if (snapshotContext == null)
            return false;

        var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
        var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
        if (completionSignal != null &&
            IsMatchingReadySignal(completionSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_complete", completionSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_complete", completionSignal, snapshotContext, rollbackSnapshot);
        }

        if (errorSignal != null &&
            IsMatchingReadySignal(errorSignal, snapshotContext) &&
            !HasValidTerminalSignalContract("turn_error", errorSignal))
        {
            return await HandleRejectedActiveReadySignalAsync("turn_error", errorSignal, snapshotContext, rollbackSnapshot);
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

    private string BuildMissingActiveTerminalOutcomeActual(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        var turnCompleteExists = _fs.FileExists("ready/turn_complete.json");
        var turnErrorExists = _fs.FileExists("ready/turn_error.json");
        var manifestDescription = snapshotContext == null
            ? "pendingSnapshot=missing"
            : $"pendingSnapshot=sessionId={snapshotContext.SessionId}, requestId={snapshotContext.RequestId}, turnNumber={snapshotContext.TurnNumber}";
        return $"turn_complete_exists={turnCompleteExists}; turn_error_exists={turnErrorExists}; {manifestDescription}";
    }

    private async Task<ReadySignalMetadata?> TryRecoverIdleBridgeOutputWithoutTerminalSignalAsync(
        ReadySignalMetadata errorSignal,
        ValidatedPendingTurnSnapshotContext snapshotContext)
    {
        if (!IsRecoverableIdleBridgeMissingTerminalSignal(errorSignal))
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
            recoveredFrom = GmBridgeIdleWithoutTerminalSignalHarnessSource,
            originalError = errorSignal.Error,
            filesModified
        };

        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", JsonSerializer.Serialize(recoveredSignal, JsonOpts));
        _fs.DeleteFile("ready/turn_error.json");
        _logger.LogWarning(
            "Recovered GM output for turn {TurnNumber} after daemon emitted {HarnessSource}; synthesized ready/turn_complete.json with {FileCount} modified files.",
            snapshotContext.TurnNumber,
            GmBridgeIdleWithoutTerminalSignalHarnessSource,
            filesModified.Length);

        return await ReadReadySignalMetadataAsync("ready/turn_complete.json");
    }

    private static bool IsRecoverableIdleBridgeMissingTerminalSignal(ReadySignalMetadata signal) =>
        string.Equals(signal.HarnessSource, GmBridgeIdleWithoutTerminalSignalHarnessSource, StringComparison.OrdinalIgnoreCase);

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
        RollbackSnapshot? rollbackSnapshot)
    {
        if (await ResolveConcurrentActiveTerminalSignalsAsync(snapshotContext, rollbackSnapshot))
            return new ActiveTerminalOutcomeResolution { Kind = "failure" };

        if (_fs.FileExists("ready/turn_complete.json"))
        {
            var completionSignal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
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

        if (_fs.FileExists("ready/turn_error.json"))
        {
            var errorSignal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
            if (errorSignal != null &&
                snapshotContext != null &&
                IsMatchingReadySignal(errorSignal, snapshotContext) &&
                HasValidTerminalSignalContract("turn_error", errorSignal))
            {
                var recoveredSignal = await TryRecoverIdleBridgeOutputWithoutTerminalSignalAsync(errorSignal, snapshotContext);
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

        await HandleMissingActiveTerminalOutcomeAsync(snapshotContext, rollbackSnapshot);
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

