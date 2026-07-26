using System.Security.Cryptography;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed class GmWorkerValidationRepairDelegatorHooks
{
    internal Func<Task>? BeforeReadyPublicationAsync { get; init; }
}

public enum GmWorkerValidationRepairOutcome
{
    SkippedNoWorker,
    SkippedNoIssues,
    TaskBuildFailed,
    WorkerFailed,
    WorkerTimedOut,
    ApplyRejected,
    SessionReplaced,
    Applied
}

public sealed record GmWorkerValidationRepairDispatchResult
{
    public GmWorkerValidationRepairOutcome Outcome { get; init; }
    public WorkerTaskPacket? Task { get; init; }
    public GmWorkerTaskRunResult? RunResult { get; init; }
    public ApplyGateDecision? ApplyDecision { get; init; }
    public bool ReadySignalCreated { get; init; }
    public string FallbackReason { get; init; } = "";
}

public sealed class GmWorkerValidationRepairDelegator
{
    public const string LatestValidationRepairTaskPath =
        "game_state/control/gm_worker_latest_validation_repair_task.json";

    public const string ValidationRepairReadyPath =
        "game_state/control/validation_repair_ready.json";

    private readonly FileSystemManager _fs;
    private readonly GmWorkerBridgePool _bridgePool;
    private readonly GmWorkerApplyGate _applyGate;
    private readonly GmWorkerAuditLog _auditLog;
    private readonly GmWorkerValidationRepairDelegatorHooks? _hooks;

    public GmWorkerValidationRepairDelegator(
        FileSystemManager fs,
        GmWorkerBridgePool bridgePool,
        GmWorkerApplyGate applyGate,
        GmWorkerAuditLog auditLog)
        : this(fs, bridgePool, applyGate, auditLog, hooks: null)
    {
    }

    internal GmWorkerValidationRepairDelegator(
        FileSystemManager fs,
        GmWorkerBridgePool bridgePool,
        GmWorkerApplyGate applyGate,
        GmWorkerAuditLog auditLog,
        GmWorkerValidationRepairDelegatorHooks? hooks)
    {
        _fs = fs;
        _bridgePool = bridgePool;
        _applyGate = applyGate;
        _auditLog = auditLog;
        _hooks = hooks;
    }

    public async Task<GmWorkerValidationRepairDispatchResult> TryRunAsync(
        IReadOnlyList<WorkerBridgeProfile> profiles,
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        WorkerTurnReference sourceTurn,
        string createdAtUtc,
        int attempt,
        string? expectedSessionGeneration = null,
        CancellationToken cancellationToken = default)
    {
        if (prioritizedErrors.Count == 0)
        {
            await RecordRouterEventAsync("validation-repair-skipped", null, null, "No validation issues were provided.");
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SkippedNoIssues,
                FallbackReason = "No validation issues were provided."
            };
        }

        var routing = GmWorkerBridgePool.SelectWorkerForTask(profiles, WorkerTaskType.ValidationRepair);
        if (!routing.Found || routing.Profile == null)
        {
            if (profiles.Any(profile => profile.Enabled))
                await RecordRouterEventAsync("validation-repair-skipped", null, null, routing.Reason);
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SkippedNoWorker,
                FallbackReason = routing.Reason
            };
        }

        WorkerTaskPacket task;
        try
        {
            task = await BuildTaskAsync(
                routing.Profile,
                prioritizedErrors,
                sourceTurn,
                createdAtUtc,
                attempt,
                expectedSessionGeneration);
            if (!await WriteLatestTaskIfCurrentAsync(task))
            {
                return new GmWorkerValidationRepairDispatchResult
                {
                    Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                    Task = task,
                    FallbackReason = "Worker task context belongs to a replaced game session generation."
                };
            }
        }
        catch (GmWorkerSessionReplacedException ex)
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                FallbackReason = ex.Message
            };
        }
        catch (Exception ex)
        {
            await RecordRouterEventAsync("validation-repair-task-build-failed", routing.Profile.WorkerId, null, ex.Message);
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.TaskBuildFailed,
                FallbackReason = ex.Message
            };
        }

        var run = await _bridgePool.RunTaskAsync(routing.Profile, task, cancellationToken);
        if (run.SessionReplaced || !await IsCurrentSessionGenerationAsync(task.SessionGeneration))
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                Task = task,
                RunResult = run,
                FallbackReason = run.Status.LastError ??
                                 "Worker task belongs to a replaced game session generation."
            };
        }

        var workerExecutionSucceeded =
            !run.TimedOut &&
            run.ExitCode == 0 &&
            run.Status.State == WorkerBridgeState.Stopped;
        if (!workerExecutionSucceeded || run.Proposal == null)
        {
            var outcome = run.TimedOut || run.Status.State == WorkerBridgeState.TimedOut
                ? GmWorkerValidationRepairOutcome.WorkerTimedOut
                : GmWorkerValidationRepairOutcome.WorkerFailed;
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = outcome,
                Task = task,
                RunResult = run,
                FallbackReason = run.Status.LastError ?? "Worker did not return a valid proposal."
            };
        }

        var boundTask = run.BoundTask ?? task;
        var decision = await _applyGate.ApplyReservedAsync(
            run.Proposal,
            routing.Profile,
            boundTask.SessionGeneration);
        if (decision.Result == ApplyGateResult.SessionReplaced)
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                Task = task,
                RunResult = run,
                ApplyDecision = decision,
                FallbackReason = decision.RejectionReasons.Count == 0
                    ? "Worker task belongs to a replaced game session generation."
                    : string.Join(Environment.NewLine, decision.RejectionReasons)
            };
        }

        if (decision.Result != ApplyGateResult.Accepted)
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.ApplyRejected,
                Task = task,
                RunResult = run,
                ApplyDecision = decision,
                FallbackReason = decision.RejectionReasons.Count == 0
                    ? $"Apply gate result: {decision.Result}."
                    : string.Join(Environment.NewLine, decision.RejectionReasons)
            };
        }

        if (_hooks?.BeforeReadyPublicationAsync != null)
            await _hooks.BeforeReadyPublicationAsync();
        var readyPublication = await TryWriteReadySignalAsync(
            sourceTurn,
            run.Proposal,
            boundTask.SessionGeneration);
        return new GmWorkerValidationRepairDispatchResult
        {
            Outcome = readyPublication.SessionReplaced
                ? GmWorkerValidationRepairOutcome.SessionReplaced
                : GmWorkerValidationRepairOutcome.Applied,
            Task = task,
            RunResult = run,
            ApplyDecision = decision,
            ReadySignalCreated = readyPublication.Created,
            FallbackReason = readyPublication.Diagnostic
        };
    }

    private async Task<bool> IsCurrentSessionGenerationAsync(string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return _fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration);
    }

    private async Task<WorkerTaskPacket> BuildTaskAsync(
        WorkerBridgeProfile profile,
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        WorkerTurnReference sourceTurn,
        string createdAtUtc,
        int attempt,
        string? expectedSessionGeneration)
    {
        var targetPaths = prioritizedErrors
            .Select(GmWorkerTaskPacketBuilder.ResolveValidationTargetPath)
            .Where(GmWorkerContractValidator.IsSafeRelativePath)
            .Distinct(GmWorkerContractValidator.CanonicalPathComparer)
            .ToArray();
        var requiresCharacteristicAuthority = prioritizedErrors.Any(issue => string.Equals(
            issue.Code,
            "npc_characteristics_empty",
            StringComparison.OrdinalIgnoreCase));
        var requiresAfterlifeRealmAuthority =
            targetPaths.Any(AfterlifeRealmAuthorityContract.IsAfterlifeStatePath) ||
            prioritizedErrors.Any(IsAfterlifeActorMaterializationIssue);
        var contextPaths = targetPaths.AsEnumerable();
        if (requiresCharacteristicAuthority)
            contextPaths = contextPaths.Append(MortalCharacteristicAuthorityContract.StatePath);
        if (requiresAfterlifeRealmAuthority)
            contextPaths = contextPaths.Append(AfterlifeRealmAuthorityContract.StatePath);

        var contextHashes = new Dictionary<string, string>(GmWorkerContractValidator.CanonicalPathComparer);
        WorkerAfterlifeRealmGate realmGate = WorkerAfterlifeRealmGate.None;
        var currentRealm = string.Empty;
        string sessionGeneration;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            sessionGeneration = _fs.GetOrCreateSessionGeneration(writeLease);
            if (expectedSessionGeneration != null &&
                !_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            {
                throw new GmWorkerSessionReplacedException(
                    "The validation-repair task belongs to a game session that is no longer current.");
            }

            foreach (var path in contextPaths.Distinct(GmWorkerContractValidator.CanonicalPathComparer))
            {
                var content = await _fs.ReadFileBytesAsync(writeLease, path);
                if (path == MortalCharacteristicAuthorityContract.StatePath)
                {
                    var authorityJson = DecodeUtf8(content);
                    if (!MortalCharacteristicAuthorityContract.TryReadKeys(authorityJson, out _, out var error))
                        throw new InvalidOperationException(error);
                }
                else if (path == AfterlifeRealmAuthorityContract.StatePath)
                {
                    var authorityJson = DecodeUtf8(content);
                    if (!AfterlifeRealmAuthorityContract.TryRead(
                            authorityJson,
                            out realmGate,
                            out currentRealm,
                            out var error))
                    {
                        throw new InvalidOperationException(error);
                    }
                }

                contextHashes[path] = content == null ? "missing" : ComputeSha256(content);
            }
        }

        var afterlifeContract = requiresAfterlifeRealmAuthority
            ? BuildAfterlifeRepairContract(realmGate, currentRealm, targetPaths)
            : null;
        return GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            $"worker_task_validation_repair_{attempt:D4}_{Guid.NewGuid():N}",
            sourceTurn,
            prioritizedErrors,
            contextHashes,
            createdAtUtc,
            sessionGeneration,
            afterlifeContract);
    }

    private async Task<bool> WriteLatestTaskIfCurrentAsync(WorkerTaskPacket task)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!_fs.IsCurrentSessionGeneration(writeLease, task.SessionGeneration))
            return false;

        await _fs.WriteFileAtomicAsync(
            writeLease,
            LatestValidationRepairTaskPath,
            GmWorkerJson.Serialize(task));
        return true;
    }

    private static WorkerAfterlifeTaskContract BuildAfterlifeRepairContract(
        WorkerAfterlifeRealmGate realmGate,
        string currentRealm,
        IEnumerable<string> targetPaths)
    {
        return new WorkerAfterlifeTaskContract
        {
            RealmGate = realmGate,
            CurrentRealm = currentRealm,
            AllowedAfterlifeSurfaces = targetPaths
                .Where(AfterlifeRealmAuthorityContract.IsAfterlifeStatePath)
                .Where(path => !path.Equals(AfterlifeRealmAuthorityContract.StatePath, StringComparison.OrdinalIgnoreCase))
                .Distinct(GmWorkerContractValidator.CanonicalPathComparer)
                .Order(GmWorkerContractValidator.CanonicalPathComparer)
                .ToArray(),
            RequiredReceipts = ["No new receipt is required for this bounded validation repair."],
            RequiredReports = ["The apply-gate validation decision is the required repair report."],
            ForbiddenMortalSubstitutes =
            [
                "worldStateFlags",
                "worldEventsLog",
                "Mortal NPC relationships",
                "Mortal combat HP/status",
                "Mortal factions or map files"
            ]
        };
    }

    private static bool IsAfterlifeActorMaterializationIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        var actor = issue.Actor ?? string.Empty;
        return code.StartsWith("afterlife_actor_materialization_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_actor_binding_", StringComparison.OrdinalIgnoreCase) ||
               (!actor.StartsWith("mortal_npc:", StringComparison.Ordinal) &&
                actor.Contains(':', StringComparison.Ordinal) &&
                code.Contains("actor_materialization", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(bool Created, string Diagnostic, bool SessionReplaced)> TryWriteReadySignalAsync(
        WorkerTurnReference sourceTurn,
        WorkerProposal proposal,
        string sessionGeneration)
    {
        var ready = new ValidationRepairReadySignal
        {
            SessionId = sourceTurn.SessionId,
            RequestId = sourceTurn.RequestId,
            TurnNumber = sourceTurn.TurnNumber,
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Note = $"GM worker proposal {proposal.ProposalId} accepted by apply gate."
        };

        try
        {
            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            if (!_fs.IsCurrentSessionGeneration(writeLease, sessionGeneration))
            {
                return (
                    false,
                    "Worker repair belonged to a replaced game session generation; no ready signal was published.",
                    true);
            }

            await _fs.WriteFileAtomicAsync(
                writeLease,
                ValidationRepairReadyPath,
                GmWorkerJson.Serialize(ready));

            await _auditLog.AppendEventAsync(writeLease, new WorkerAuditEvent
            {
                EventId = GmWorkerAuditEventIdGenerator.Create(),
                EventType = "validation-repair-ready-created",
                WorkerId = proposal.WorkerId,
                TaskId = proposal.TaskId,
                ProposalId = proposal.ProposalId,
                TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                Summary = "Created validation_repair_ready.json after accepted GM worker repair proposal.",
                Details = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["readyPath"] = [ValidationRepairReadyPath]
                }
            });
        }
        catch (Exception ex)
        {
            var diagnostic = $"Worker repair was applied, but ready signal publication failed: {ex.Message}";
            await TryRecordPostApplyDiagnosticAsync(
                "validation-repair-ready-failed",
                proposal,
                sessionGeneration,
                diagnostic);
            return (false, diagnostic, false);
        }

        return (true, string.Empty, false);
    }

    private async Task TryRecordPostApplyDiagnosticAsync(
        string eventType,
        WorkerProposal proposal,
        string expectedSessionGeneration,
        string summary)
    {
        try
        {
            _ = await _auditLog.AppendEventIfCurrentSessionAsync(
                expectedSessionGeneration,
                new WorkerAuditEvent
                {
                    EventId = GmWorkerAuditEventIdGenerator.Create(),
                    EventType = eventType,
                    WorkerId = proposal.WorkerId,
                    TaskId = proposal.TaskId,
                    ProposalId = proposal.ProposalId,
                    TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                    Summary = summary
                });
        }
        catch
        {
            // The canonical repair already succeeded; diagnostics must not reclassify its ownership.
        }
    }

    private Task RecordRouterEventAsync(string eventType, string? workerId, string? taskId, string summary) =>
        _auditLog.AppendEventAsync(new WorkerAuditEvent
        {
            EventId = GmWorkerAuditEventIdGenerator.Create(),
            EventType = eventType,
            WorkerId = string.IsNullOrWhiteSpace(workerId) ? "validation_repair_router" : workerId,
            TaskId = taskId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = summary
        });

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string? DecodeUtf8(byte[]? content)
    {
        if (content == null)
            return null;
        var text = System.Text.Encoding.UTF8.GetString(content);
        return text.Length > 0 && text[0] == '\ufeff' ? text[1..] : text;
    }

    private sealed record ValidationRepairReadySignal
    {
        public string SessionId { get; init; } = "";
        public string RequestId { get; init; } = "";
        public int TurnNumber { get; init; }
        public string UpdatedAtUtc { get; init; } = "";
        public string? Note { get; init; }
    }
}
