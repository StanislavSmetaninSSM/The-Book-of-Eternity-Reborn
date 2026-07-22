using System.Security.Cryptography;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Services.GmWorkers;

public enum GmWorkerValidationRepairOutcome
{
    SkippedNoWorker,
    SkippedNoIssues,
    TaskBuildFailed,
    WorkerFailed,
    WorkerTimedOut,
    ApplyRejected,
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

    public GmWorkerValidationRepairDelegator(
        FileSystemManager fs,
        GmWorkerBridgePool bridgePool,
        GmWorkerApplyGate applyGate,
        GmWorkerAuditLog auditLog)
    {
        _fs = fs;
        _bridgePool = bridgePool;
        _applyGate = applyGate;
        _auditLog = auditLog;
    }

    public async Task<GmWorkerValidationRepairDispatchResult> TryRunAsync(
        IReadOnlyList<WorkerBridgeProfile> profiles,
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        WorkerTurnReference sourceTurn,
        string createdAtUtc,
        int attempt,
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
            task = await BuildTaskAsync(routing.Profile, prioritizedErrors, sourceTurn, createdAtUtc, attempt);
            await _fs.WriteFileAtomicAsync(LatestValidationRepairTaskPath, GmWorkerJson.Serialize(task));
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
        if (run.Proposal == null)
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

        var decision = await _applyGate.ApplyAsync(run.Proposal, task, routing.Profile);
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

        var readyPublication = await TryWriteReadySignalAsync(sourceTurn, run.Proposal);
        return new GmWorkerValidationRepairDispatchResult
        {
            Outcome = GmWorkerValidationRepairOutcome.Applied,
            Task = task,
            RunResult = run,
            ApplyDecision = decision,
            ReadySignalCreated = readyPublication.Created,
            FallbackReason = readyPublication.Diagnostic
        };
    }

    private async Task<WorkerTaskPacket> BuildTaskAsync(
        WorkerBridgeProfile profile,
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        WorkerTurnReference sourceTurn,
        string createdAtUtc,
        int attempt)
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
        foreach (var path in contextPaths.Distinct(GmWorkerContractValidator.CanonicalPathComparer))
        {
            var content = await _fs.ReadFileBytesAsync(path);
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
            afterlifeContract);
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

    private async Task<(bool Created, string Diagnostic)> TryWriteReadySignalAsync(
        WorkerTurnReference sourceTurn,
        WorkerProposal proposal)
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
            await _fs.WriteFileAtomicAsync(ValidationRepairReadyPath, GmWorkerJson.Serialize(ready));
        }
        catch (Exception ex)
        {
            var diagnostic = $"Worker repair was applied, but ready signal publication failed: {ex.Message}";
            await TryRecordPostApplyDiagnosticAsync(
                "validation-repair-ready-failed",
                proposal,
                diagnostic);
            return (false, diagnostic);
        }

        try
        {
            await _auditLog.AppendEventAsync(new WorkerAuditEvent
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
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (true, $"Ready signal was created, but its audit event could not be recorded: {ex.Message}");
        }
    }

    private async Task TryRecordPostApplyDiagnosticAsync(
        string eventType,
        WorkerProposal proposal,
        string summary)
    {
        try
        {
            await _auditLog.AppendEventAsync(new WorkerAuditEvent
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
