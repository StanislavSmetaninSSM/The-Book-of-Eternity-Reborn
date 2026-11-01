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

    private async Task AppendClearedValidationRepairTrajectoryAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors,
        int attempt,
        string expectedSessionGeneration)
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
                    "The cleared validation-repair trajectory belongs to a game session that is no longer current.");
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
                        .Select(ReadValidationRepairPacketKind)
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
                .Select(ReadValidationRepairPacketKind)
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

    private async Task<ValidationRepairDispatchState> WriteValidationRepairRequestAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt)
    {
        var sessionGeneration = await CaptureCurrentSessionGenerationAsync();
        return await WriteValidationRepairRequestForSessionAsync(
            source,
            errors,
            attempt,
            sessionGeneration);
    }

    private async Task<ValidationRepairDispatchState> WriteValidationRepairRequestForSessionAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        string expectedSessionGeneration,
        IReadOnlyList<MortalLocationRepairRetryObligation>? mortalLocationRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? mortalLocationRequiredResubmissionPaths = null,
        IReadOnlyList<EffectRepairRetryObligation>? effectRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? effectRequiredResubmissionPaths = null,
        IReadOnlyList<ResourceRepairRetryObligation>? resourceRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? resourceRequiredResubmissionPaths = null,
        IReadOnlyList<WoundRepairRetryObligation>? woundRepairRetryObligations = null,
        IReadOnlyList<RepairResubmissionPathObligation>? woundRequiredResubmissionPaths = null,
        IReadOnlyList<EffectRepairPacket>? effectRepairPackets = null,
        bool boundedPendingResolutionResubmission = false)
    {
        await DeleteValidationRepairFilesForSessionAsync(expectedSessionGeneration);
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var resourceRepairPackets = ResourceRepairPacketBuilder.Build(prioritizedErrors);
        var fullTurnResubmissionRequired =
            boundedPendingResolutionResubmission ||
            mortalLocationRetryObligations is { Count: > 0 } ||
            effectRetryObligations is { Count: > 0 } ||
            effectRepairPackets is { Count: > 0 } ||
            woundRepairRetryObligations is { Count: > 0 } ||
            resourceRepairPackets.Count > 0;
        var woundRepairPackets = WoundRepairPacketBuilder.Build(prioritizedErrors);
        fullTurnResubmissionRequired =
            fullTurnResubmissionRequired || woundRepairPackets.Count > 0;
        var gmInstructions = BuildValidationRepairRequestInstructions(
            pendingSnapshot,
            fullTurnResubmissionRequired,
            boundedPendingResolutionResubmission);

        var request = new ValidationRepairRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            RevalidationAttempt = attempt,
            FullTurnResubmissionRequired = fullTurnResubmissionRequired,
            GmInstructions = gmInstructions,
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            HarnessRepairPackets = BuildValidationRepairHarnessPackets(
                prioritizedErrors,
                await ReadCurrentGuardianRepairActorNameHintsAsync(),
                effectRepairPackets),
            ResubmissionObligations = (mortalLocationRetryObligations?
                .Select(obligation => new ValidationRepairResubmissionObligation
                {
                    Actor = obligation.Actor,
                    Route = obligation.Route,
                    RawCarrier = obligation.Route is "current_scene_creation" or "current_selection"
                        ? "currentLocationData"
                        : "worldMapUpdates"
                }) ?? Enumerable.Empty<ValidationRepairResubmissionObligation>())
                .Concat(effectRetryObligations?
                    .Select(obligation => new ValidationRepairResubmissionObligation
                    {
                        Actor = obligation.Actor,
                        Route = "effectChanges",
                        RawCarrier = EffectAcceptedTurnPlan.CommandPath
                    }) ?? Enumerable.Empty<ValidationRepairResubmissionObligation>())
                .Concat(resourceRetryObligations?
                    .Select(obligation => new ValidationRepairResubmissionObligation
                    {
                        Actor = $"ordinary resource change #{obligation.CommandOrdinal + 1}",
                        Route = "resourceChanges",
                        RawCarrier = "accepted turn response"
                    }) ?? Enumerable.Empty<ValidationRepairResubmissionObligation>())
                .Concat((woundRepairRetryObligations?
                        .Select(static obligation => obligation.Packet) ??
                    woundRepairPackets).Select(packet =>
                    new ValidationRepairResubmissionObligation
                    {
                        Actor = packet.CandidateRef,
                        Route = packet.ResubmissionRoute,
                        RawCarrier = "accepted turn response"
                    }))
                .ToList(),
            RequiredResubmissionPaths = (mortalLocationRequiredResubmissionPaths ??
                    Array.Empty<RepairResubmissionPathObligation>())
                .Concat(effectRequiredResubmissionPaths ??
                        Array.Empty<RepairResubmissionPathObligation>())
                .Concat(resourceRequiredResubmissionPaths ??
                        Array.Empty<RepairResubmissionPathObligation>())
                .Concat(woundRequiredResubmissionPaths ??
                        Array.Empty<RepairResubmissionPathObligation>())
                .Select(static obligation => obligation.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
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

        PublishAgentConsoleValidationRepairSnapshot();
        GmWorkerValidationRepairDispatchResult? workerResult = null;
        if (!metadataDiagnosticOnly && !fullTurnResubmissionRequired)
        {
            workerResult = await RunWorkerValidationRepairIfAvailableAsync(
                prioritizedErrors,
                requestMetadata,
                request.DetectedAtUtc,
                attempt,
                expectedSessionGeneration);
            if (workerResult.Outcome == GmWorkerValidationRepairOutcome.Applied)
            {
                return new ValidationRepairDispatchState
                {
                    WorkerApplyAccepted = true,
                    ReadySignalCreated = workerResult.ReadySignalCreated,
                    WorkerResult = workerResult
                };
            }
            if (workerResult.Outcome == GmWorkerValidationRepairOutcome.SessionReplaced)
            {
                return new ValidationRepairDispatchState
                {
                    SessionReplaced = true,
                    WorkerResult = workerResult
                };
            }
        }

        await WriteValidationRepairFileForSessionAsync(
            ValidationRepairRequestPath,
            JsonSerializer.Serialize(request, JsonOpts),
            expectedSessionGeneration);
        return new ValidationRepairDispatchState
        {
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            WorkerResult = workerResult
        };
    }

    private async Task<bool> FailClosedDiagnosticOnlyValidationRepairAsync(
        string source,
        List<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string expectedSessionGeneration)
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

        // Keep rollback caller-owned: accepted-turn callers perform it once. Consuming backup
        // evidence here would make their mandatory rollback run a second time.
        AnsiConsole.MarkupLine(
            HasRollbackCapability(rollbackSnapshot)
                ? "[yellow]↩ Изменения мира не были приняты; состояние до хода будет восстановлено.[/]"
                : "[yellow]⚠ Изменения мира не были приняты. Продолжение этого хода остановлено.[/]");

        await RunBestEffortFailClosedBookkeepingAsync(
            "write the diagnostic-only validation failure report",
            () => WriteValidationRepairFileForSessionAsync(
                ValidationDiagnosticFailureReportPath,
                JsonSerializer.Serialize(report, JsonOpts),
                expectedSessionGeneration));
        await RunBestEffortFailClosedBookkeepingAsync(
            "delete the transient report after diagnostic-only validation failure",
            async () =>
            {
                if (!await _progressionSchedule.DeleteTransientReportIfCurrentSessionAsync(
                        expectedSessionGeneration))
                {
                    throw new GmWorkerSessionReplacedException(
                        "The validation-repair cycle belongs to a game session that is no longer current.");
                }
            });
        await RunBestEffortFailClosedBookkeepingAsync(
            "clean validation-repair control files after diagnostic-only validation failure",
            () => DeleteValidationRepairFilesForSessionAsync(expectedSessionGeneration));
        return false;
    }

    private async Task WriteProtectedMortalLocationDiagnosticFailureReportAsync(
        string source,
        IReadOnlyList<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string expectedSessionGeneration)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            attempt,
            reason = "Protected or unresolved Mortal location authority requires fail-closed rollback before GM repair dispatch.",
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

        await WriteValidationRepairFileForSessionAsync(
            ValidationDiagnosticFailureReportPath,
            JsonSerializer.Serialize(report, JsonOpts),
            expectedSessionGeneration);
    }

    private async Task WriteProtectedEffectDiagnosticFailureReportAsync(
        string source,
        IReadOnlyList<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string expectedSessionGeneration)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            attempt,
            reason = "Protected or unresolved effect authority requires fail-closed rollback before GM repair dispatch.",
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

        await WriteValidationRepairFileForSessionAsync(
            ValidationDiagnosticFailureReportPath,
            JsonSerializer.Serialize(report, JsonOpts),
            expectedSessionGeneration);
    }

    private async Task WriteProtectedWoundDiagnosticFailureReportAsync(
        string source,
        IReadOnlyList<ValidationIssue> errors,
        int attempt,
        RollbackSnapshot? rollbackSnapshot,
        string expectedSessionGeneration)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            attempt,
            reason = "Protected or unresolved wound authority requires fail-closed rollback before GM repair dispatch.",
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

        await WriteValidationRepairFileForSessionAsync(
            ValidationDiagnosticFailureReportPath,
            JsonSerializer.Serialize(report, JsonOpts),
            expectedSessionGeneration);
    }
}
