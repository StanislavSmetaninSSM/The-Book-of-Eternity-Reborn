using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerAuditLog
{
    public const string AuditLogPath = "game_state/control/gm_worker_audit.jsonl";

    private static readonly JsonSerializerOptions CompactJsonOptions = new(GmWorkerJson.Options)
    {
        WriteIndented = false
    };

    private readonly FileSystemManager _fs;

    public GmWorkerAuditLog(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task AppendEventAsync(WorkerAuditEvent auditEvent)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.EventId))
            throw new ArgumentException("Audit event id is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.EventType))
            throw new ArgumentException("Audit event type is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.WorkerId))
            throw new ArgumentException("Audit worker id is required.", nameof(auditEvent));

        var current = await _fs.ReadFileAsync(AuditLogPath) ?? "";
        var line = JsonSerializer.Serialize(auditEvent, CompactJsonOptions);
        var next = string.IsNullOrWhiteSpace(current)
            ? line + Environment.NewLine
            : current.TrimEnd() + Environment.NewLine + line + Environment.NewLine;
        await _fs.WriteFileAtomicAsync(AuditLogPath, next);
    }

    public Task RecordTaskDispatchedAsync(WorkerTaskPacket task) =>
        AppendEventAsync(new WorkerAuditEvent
        {
            EventId = CreateEventId(),
            EventType = "task-dispatched",
            WorkerId = task.WorkerId,
            TaskId = task.TaskId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = $"Dispatched {task.TaskType} worker task.",
            Details = new Dictionary<string, IReadOnlyList<string>>
            {
                ["allowedProposalPaths"] = task.AllowedProposalPaths
            }
        });

    public Task RecordProposalReceivedAsync(WorkerProposal proposal) =>
        AppendEventAsync(new WorkerAuditEvent
        {
            EventId = CreateEventId(),
            EventType = "proposal-received",
            WorkerId = proposal.WorkerId,
            TaskId = proposal.TaskId,
            ProposalId = proposal.ProposalId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = proposal.Summary,
            Details = new Dictionary<string, IReadOnlyList<string>>
            {
                ["changedFiles"] = proposal.ChangedFiles.Select(file => file.Path).ToArray()
            }
        });

    public Task RecordApplyDecisionAsync(WorkerProposal proposal, ApplyGateDecision decision) =>
        AppendEventAsync(new WorkerAuditEvent
        {
            EventId = CreateEventId(),
            EventType = decision.Result switch
            {
                ApplyGateResult.Accepted => "proposal-applied",
                ApplyGateResult.ValidationFailed => "proposal-validation-failed",
                _ => "proposal-rejected"
            },
            WorkerId = proposal.WorkerId,
            TaskId = proposal.TaskId,
            ProposalId = proposal.ProposalId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = $"Apply gate decision: {decision.Result}.",
            Details = new Dictionary<string, IReadOnlyList<string>>
            {
                ["appliedFiles"] = decision.AppliedFiles,
                ["rejectionReasons"] = decision.RejectionReasons
            }
        });

    public async Task<IReadOnlyList<WorkerAuditEvent>> ReadEventsAsync()
    {
        var jsonl = await _fs.ReadFileAsync(AuditLogPath);
        if (string.IsNullOrWhiteSpace(jsonl))
            return [];

        var events = new List<WorkerAuditEvent>();
        foreach (var line in jsonl.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var auditEvent = JsonSerializer.Deserialize<WorkerAuditEvent>(line, CompactJsonOptions);
            if (auditEvent != null)
                events.Add(auditEvent);
        }

        return events;
    }

    private static string CreateEventId() =>
        "worker_audit_" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
}
