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

    private async Task<bool> WaitForContractRepairAsync(string source, List<ValidationIssue> errors,
        int attempt, RollbackSnapshot? rollbackSnapshot, string repairSessionGeneration,
        IReadOnlyList<MortalLocationRepairRetryObligation>? mortalLocationRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? mortalLocationRequiredResubmissionPaths = null,
        IReadOnlyList<EffectRepairRetryObligation>? effectRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? effectRequiredResubmissionPaths = null,
        IReadOnlyList<ResourceRepairRetryObligation>? resourceRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? resourceRequiredResubmissionPaths = null,
        IReadOnlyList<WoundRepairRetryObligation>? woundRepairRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? woundRequiredResubmissionPaths = null,
        bool boundedPendingResolutionResubmission = false,
        byte[]? pendingResolutionRepairCheckpoint = null)
    {
        await EnsureRepairSessionCurrentAsync(repairSessionGeneration);
        var isBoundedPendingResolutionResubmission =
            boundedPendingResolutionResubmission &&
            IsExactBoundedPendingResolutionResubmission(errors);
        if (boundedPendingResolutionResubmission &&
            !isBoundedPendingResolutionResubmission)
        {
            throw new InvalidOperationException(
                "The bounded pending repair route accepts only the exact client-authored pending resubmission issue.");
        }
        var rollbackAvailable = HasRollbackCapability(rollbackSnapshot);
        var requiresMortalItemFailClosed =
            MortalItemRepairPacketBuilder.RequiresFailClosedRollback(errors);
        var mortalLocationRepairPackets = MortalLocationRepairPacketBuilder.Build(errors);
        var hasActionableMortalLocationRepair = mortalLocationRepairPackets.Count > 0;
        var requiresMortalLocationFailClosed =
            MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(errors) ||
            (hasActionableMortalLocationRepair && !rollbackAvailable);
        var effectRepairPackets = EffectRepairPacketBuilder.Build(
            errors,
            rollbackAvailable);
        var hasActionableEffectRepair = effectRepairPackets.Count > 0;
        var requiresEffectFailClosed =
            EffectRepairPacketBuilder.RequiresFailClosedRollback(
                errors,
                rollbackAvailable);
        var resourceRepairPackets = ResourceRepairPacketBuilder.Build(errors);
        var hasActionableResourceRepair = resourceRepairPackets.Count > 0;
        var requiresResourceFailClosed =
            (!isBoundedPendingResolutionResubmission &&
             ResourceRepairPacketBuilder.RequiresFailClosedRollback(errors)) ||
            (hasActionableResourceRepair && !rollbackAvailable);
        var woundRepairPackets = WoundRepairPacketBuilder.Build(errors);
        var hasActionableWoundRepair = woundRepairPackets.Count > 0;
        var requiresWoundFailClosed = RequiresWoundRepairFailClosed(
            errors,
            rollbackAvailable);
        if (requiresMortalItemFailClosed ||
            requiresMortalLocationFailClosed ||
            requiresEffectFailClosed ||
            requiresResourceFailClosed ||
            requiresWoundFailClosed)
        {
            _logger.LogError(
                "Materialization repair after {Source} has protected or unresolved authority; rejecting the accepted state for caller-owned rollback instead of dispatching a broad GM repair.",
                source);
            await RunBestEffortFailClosedBookkeepingAsync(
                "delete the transient report after protected Mortal materialization rejection",
                async () =>
                {
                    if (!await _progressionSchedule.DeleteTransientReportIfCurrentSessionAsync(
                            repairSessionGeneration))
                    {
                        throw new GmWorkerSessionReplacedException(
                            "The protected Mortal materialization rejection belongs to a game session that is no longer current.");
                    }
                });

            if (requiresMortalLocationFailClosed)
            {
                await RunBestEffortFailClosedBookkeepingAsync(
                    "write the protected Mortal location diagnostic report",
                    () => WriteProtectedMortalLocationDiagnosticFailureReportAsync(
                        source,
                        errors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration));
            }
            if (requiresEffectFailClosed)
            {
                await RunBestEffortFailClosedBookkeepingAsync(
                    "write the protected effect diagnostic report",
                    () => WriteProtectedEffectDiagnosticFailureReportAsync(
                        source,
                        errors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration));
            }
            if (requiresWoundFailClosed)
            {
                await RunBestEffortFailClosedBookkeepingAsync(
                    "write the protected wound diagnostic report",
                    () => WriteProtectedWoundDiagnosticFailureReportAsync(
                        source,
                        errors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration));
            }
            await RunBestEffortFailClosedBookkeepingAsync(
                "clean validation-repair control files after protected Mortal materialization rejection",
                () => DeleteValidationRepairFilesForSessionAsync(repairSessionGeneration));
            AnsiConsole.MarkupLine(
                rollbackAvailable
                    ? "[yellow]↩ Изменения мира не были приняты; состояние до хода будет восстановлено.[/]"
                    : "[yellow]⚠ Изменения мира не были приняты. Продолжение этого хода остановлено.[/]");
            return false;
        }

        if (hasActionableMortalLocationRepair ||
            hasActionableEffectRepair ||
            hasActionableResourceRepair ||
            hasActionableWoundRepair)
        {
            await RestorePreTurnBaselineForRepairSessionAsync(
                rollbackSnapshot!,
                repairSessionGeneration,
                pendingResolutionRepairCheckpoint);
        }

        var dispatch = await WriteValidationRepairRequestForSessionAsync(
            source,
            errors,
            attempt,
            repairSessionGeneration,
            mortalLocationRetryObligations,
            mortalLocationRequiredResubmissionPaths,
            effectRetryObligations,
            effectRequiredResubmissionPaths,
            resourceRetryObligations,
            resourceRequiredResubmissionPaths,
            woundRepairRetryObligations,
            woundRequiredResubmissionPaths,
            effectRepairPackets,
            isBoundedPendingResolutionResubmission);
        ThrowIfValidationRepairDispatchSessionReplaced(dispatch);
        if (dispatch.MetadataDiagnosticOnly)
            return await FailClosedDiagnosticOnlyValidationRepairAsync(
                source,
                errors,
                attempt,
                rollbackSnapshot,
                repairSessionGeneration);

        if (dispatch.WorkerApplyAccepted && !dispatch.ReadySignalCreated)
        {
            await AppendWorkerAcceptedValidationRepairTrajectoryAsync(source, errors, attempt, dispatch);
            return true;
        }

        using var agentConsoleRepairInputBlock = BeginAgentConsoleInputBlockFromCurrentSnapshot(
            PlayerSafeInputBlockedText);

        while (true)
        {
            using var cts = new CancellationTokenSource();
            var startTime = DateTime.UtcNow;
            var harnessTerminalErrorPublished = false;

            var waitTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await EnsureRepairSessionCurrentAsync(repairSessionGeneration);
                    if (_fs.FileExists(ValidationRepairReadyPath))
                        return true;
                    if (_fs.FileExists(ValidationRepairArtifactStallReportPath))
                    {
                        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
                        if (pendingSnapshot is { Status: PendingTurnSnapshotResolutionStatus.Usable, Context: not null } &&
                            await TryPromoteValidationRepairArtifactStallToTerminalErrorAsync(
                                pendingSnapshot.Context,
                                repairSessionGeneration))
                        {
                            harnessTerminalErrorPublished = true;
                            return false;
                        }
                    }

                    await Task.Delay(500, cts.Token);
                }
                return false;
            }, cts.Token);

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots12)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync(rollbackAvailable
                    ? "[yellow]⏳ Мир ещё не готов продолжить ход... (Escape = вернуться к состоянию до хода)[/]"
                    : "[yellow]⏳ Мир ещё не готов продолжить ход... (Escape = прекратить ожидание)[/]", async ctx =>
                {
                    while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                    {
                        var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                        ctx.Status(rollbackAvailable
                            ? $"[yellow]⏳ Мир ещё не готов продолжить ход... ({elapsed}с) (Escape = вернуться)[/]"
                            : $"[yellow]⏳ Мир ещё не готов продолжить ход... ({elapsed}с) (Escape = выйти)[/]");

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
                    AnsiConsole.MarkupLine("[yellow]⏹ Изменения мира не были приняты; состояние до хода будет восстановлено.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⏹ Продолжение хода остановлено; дальнейшие изменения не принимаются.[/]");
                }

                await RunBestEffortFailClosedBookkeepingAsync(
                    "delete the transient report after validation-repair cancellation",
                    async () =>
                    {
                        if (!await _progressionSchedule.DeleteTransientReportIfCurrentSessionAsync(
                                repairSessionGeneration))
                        {
                            throw new GmWorkerSessionReplacedException(
                                "The validation-repair cycle belongs to a game session that is no longer current.");
                        }
                    });
                await RunBestEffortFailClosedBookkeepingAsync(
                    "clean validation-repair control files after cancellation",
                    () => DeleteValidationRepairFilesForSessionAsync(repairSessionGeneration));
                return false;
            }

            if (harnessTerminalErrorPublished)
            {
                AnsiConsole.MarkupLine("[yellow]⏹ Мир не смог завершить этот ход; продолжение остановлено.[/]");
                return false;
            }

            if (!result)
                continue;

            var readyJson = await ReadValidationRepairFileForSessionAsync(
                ValidationRepairReadyPath,
                repairSessionGeneration);
            var ready = ReadValidationRepairReady(readyJson);
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
                    BuildInvalidRepairReadyRepairHint(pendingSnapshot),
                    repairSessionGeneration);
                ThrowIfValidationRepairDispatchSessionReplaced(rejectedReadyRepair.Dispatch);
                if (rejectedReadyRepair.Dispatch.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(
                        source,
                        rejectedReadyRepair.ReportErrors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration);

                if (rejectedReadyRepair.Dispatch.WorkerApplyAccepted)
                {
                    if (!rejectedReadyRepair.Dispatch.ReadySignalCreated)
                    {
                        await AppendWorkerAcceptedValidationRepairTrajectoryAsync(
                            source,
                            rejectedReadyRepair.ReportErrors,
                            attempt,
                            rejectedReadyRepair.Dispatch);
                        return true;
                    }

                    continue;
                }

                await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
                AnsiConsole.MarkupLine("[yellow]⚠ Мир ещё не готов продолжить ход. Ожидание продолжается.[/]");
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
                    BuildMismatchedRepairReadyRepairHint(pendingSnapshot),
                    repairSessionGeneration);
                ThrowIfValidationRepairDispatchSessionReplaced(rejectedReadyRepair.Dispatch);
                if (rejectedReadyRepair.Dispatch.MetadataDiagnosticOnly)
                    return await FailClosedDiagnosticOnlyValidationRepairAsync(
                        source,
                        rejectedReadyRepair.ReportErrors,
                        attempt,
                        rollbackSnapshot,
                        repairSessionGeneration);

                if (rejectedReadyRepair.Dispatch.WorkerApplyAccepted)
                {
                    if (!rejectedReadyRepair.Dispatch.ReadySignalCreated)
                    {
                        await AppendWorkerAcceptedValidationRepairTrajectoryAsync(
                            source,
                            rejectedReadyRepair.ReportErrors,
                            attempt,
                            rejectedReadyRepair.Dispatch);
                        return true;
                    }

                    continue;
                }

                await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
                AnsiConsole.MarkupLine("[yellow]⚠ Мир ещё не готов продолжить ход. Ожидание продолжается.[/]");
                await Task.Delay(500);
                continue;
            }

            await AppendAcceptedValidationRepairTrajectoryAsync(
                source,
                errors,
                attempt,
                ready,
                pendingSnapshot,
                repairSessionGeneration);
            await DeleteValidationRepairReadyForSessionAsync(repairSessionGeneration);
            return true;
        }
    }

    private Task<bool> WaitForBoundedPendingResolutionResubmissionAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string repairSessionGeneration,
        byte[]? pendingResolutionRepairCheckpoint = null)
    {
        if (!IsExactBoundedPendingResolutionResubmission(errors))
        {
            throw new InvalidOperationException(
                "The bounded pending repair route requires exactly one trusted pending resubmission issue.");
        }

        return WaitForContractRepairAsync(
            source,
            errors,
            attempt,
            rollbackSnapshot,
            repairSessionGeneration,
            boundedPendingResolutionResubmission: true,
            pendingResolutionRepairCheckpoint: pendingResolutionRepairCheckpoint);
    }

    private static bool IsExactBoundedPendingResolutionResubmission(
        IReadOnlyList<ValidationIssue> errors)
    {
        return errors.Count == 1 &&
               errors[0].Severity == IssueSeverity.Error &&
               string.Equals(
                   errors[0].FilePath,
                   EffectAcceptedTurnPlan.CommandPath,
                   StringComparison.Ordinal) &&
               string.Equals(
                   errors[0].Code,
                   "resource_pending_full_turn_resubmission_required",
                   StringComparison.Ordinal) &&
               string.Equals(
                   errors[0].Section,
                   "AcceptedMechanicsPendingResolution",
                   StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(errors[0].Actual);
    }

    private static void ThrowIfValidationRepairDispatchSessionReplaced(
        ValidationRepairDispatchState dispatch)
    {
        if (dispatch.SessionReplaced)
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair worker belongs to a game session that is no longer current.");
        }
    }

    private async Task AppendAcceptedValidationRepairTrajectoryAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        ValidationRepairReady ready,
        PendingTurnSnapshotResolution pendingSnapshot,
        string expectedSessionGeneration,
        string acceptanceScope = "correlated_repair_ready",
        string terminalKind = "validation_repair_ready",
        string? signalPath = ValidationRepairReadyPath,
        string dispatchStatus = "client_observed_terminal")
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
                    status = dispatchStatus
                },
                validation = new
                {
                    status = "accepted",
                    source,
                    acceptanceScope,
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
                    kind = terminalKind,
                    signalPath
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

            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = false
            });
            if (!await _fs.AppendFileAtomicIfCurrentSessionAsync(
                    ledgerPath,
                    json + Environment.NewLine,
                    expectedSessionGeneration))
            {
                throw new GmWorkerSessionReplacedException(
                    "The accepted validation-repair trajectory belongs to a game session that is no longer current.");
            }
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append accepted validation repair trajectory after {Source}. Gameplay continues without ledger entry.",
                source);
        }
    }

    private async Task EnsureRepairSessionCurrentAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
        {
            throw new GmWorkerSessionReplacedException(
                "The validation-repair cycle belongs to a game session that is no longer current.");
        }
    }

    private async Task AppendWorkerAcceptedValidationRepairTrajectoryAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        ValidationRepairDispatchState dispatch)
    {
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var sourceTurn = dispatch.WorkerResult?.Task?.SourceTurn;
        var workerSessionGeneration = dispatch.WorkerResult?.Task?.SessionGeneration;
        if (string.IsNullOrWhiteSpace(workerSessionGeneration))
        {
            _logger.LogWarning(
                "Skipped worker accepted validation repair trajectory because its reserved task session generation is unavailable after {Source}.",
                source);
            return;
        }
        var ready = new ValidationRepairReady
        {
            SessionId = sourceTurn?.SessionId ?? pendingSnapshot.Context?.SessionId ?? string.Empty,
            RequestId = sourceTurn?.RequestId ?? pendingSnapshot.Context?.RequestId ?? string.Empty,
            TurnNumber = sourceTurn?.TurnNumber ?? pendingSnapshot.Context?.TurnNumber ?? 0,
            UpdatedAtUtc = DateTime.UtcNow.ToString("o"),
            Note = dispatch.WorkerResult?.FallbackReason
        };

        await AppendAcceptedValidationRepairTrajectoryAsync(
            source,
            errors,
            attempt,
            ready,
            pendingSnapshot,
            workerSessionGeneration,
            acceptanceScope: "worker_apply_gate",
            terminalKind: "worker_apply_gate_accepted",
            signalPath: null,
            dispatchStatus: "worker_applied_without_ready_signal");
    }
}
