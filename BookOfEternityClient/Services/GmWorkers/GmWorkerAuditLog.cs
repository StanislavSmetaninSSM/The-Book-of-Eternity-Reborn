using System.Text.Json;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal enum GmWorkerAuditAppendDisposition
{
    Appended,
    SessionReplaced,
    CanonicalAuditUnavailable
}

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
        await AppendEventCoreAsync(auditEvent, writeLease: null);
    }

    internal async Task AppendEventAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        WorkerAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        await AppendEventCoreAsync(auditEvent, writeLease);
    }

    internal async Task<bool> AppendEventIfCurrentSessionAsync(
        string expectedSessionGeneration,
        WorkerAuditEvent auditEvent)
    {
        return await AppendEventIfCurrentSessionAsync(
            expectedSessionGeneration,
            auditEvent,
            CancellationToken.None);
    }

    internal async Task<bool> AppendEventIfCurrentSessionAsync(
        string expectedSessionGeneration,
        WorkerAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
        if (!_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            return false;

        await AppendEventCoreAsync(auditEvent, writeLease, cancellationToken);
        return true;
    }

    internal async Task<GmWorkerAuditAppendDisposition>
        AppendRequiredEventOnceIfCurrentSessionAsync(
            string expectedSessionGeneration,
            WorkerAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
    {
        ValidateAuditEvent(auditEvent);
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
        if (!_fs.IsCurrentSessionGeneration(
                writeLease,
                expectedSessionGeneration))
        {
            return GmWorkerAuditAppendDisposition.SessionReplaced;
        }

        if (await ContainsEquivalentEventAsync(
                writeLease,
                auditEvent))
        {
            return GmWorkerAuditAppendDisposition.Appended;
        }

        await AppendEventCoreAsync(
            auditEvent,
            writeLease,
            cancellationToken,
            suppressFailure: false);
        return GmWorkerAuditAppendDisposition.Appended;
    }

    private async Task AppendEventCoreAsync(
        WorkerAuditEvent auditEvent,
        FileSystemManager.CanonicalWriteLease? writeLease,
        CancellationToken cancellationToken = default,
        bool suppressFailure = true)
    {
        ValidateAuditEvent(auditEvent);

        var line = JsonSerializer.Serialize(auditEvent, CompactJsonOptions);
        try
        {
            if (writeLease == null)
                await _fs.AppendFileAtomicAsync(AuditLogPath, line + Environment.NewLine);
            else
                await _fs.AppendFileAtomicAsync(
                    writeLease,
                    AuditLogPath,
                    line + Environment.NewLine,
                    cancellationToken);
        }
        catch (Exception) when (suppressFailure)
        {
            // Audit is diagnostic telemetry and must not revoke an accepted canonical operation.
        }
    }

    private async Task<bool> ContainsEquivalentEventAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        WorkerAuditEvent auditEvent)
    {
        var bytes = await _fs.ReadFileBytesAsync(
            writeLease,
            AuditLogPath);
        if (bytes == null || bytes.Length == 0)
            return false;

        var jsonl = Encoding.UTF8.GetString(bytes);
        if (jsonl.Length > 0 && jsonl[0] == '\uFEFF')
            jsonl = jsonl[1..];

        WorkerAuditEvent? matchedEvent = null;
        foreach (var line in jsonl.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            WorkerAuditEvent existingEvent;
            try
            {
                existingEvent =
                    JsonSerializer.Deserialize<WorkerAuditEvent>(
                        line,
                        CompactJsonOptions)
                    ?? throw new InvalidDataException(
                        "Worker audit event line is null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "Worker audit log contains malformed JSON.",
                    ex);
            }

            if (!existingEvent.EventId.Equals(
                    auditEvent.EventId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (matchedEvent != null)
            {
                throw new InvalidDataException(
                    $"Worker audit event '{auditEvent.EventId}' is duplicated.");
            }

            matchedEvent = existingEvent;
        }

        if (matchedEvent == null)
            return false;
        if (!AuditEventsEqual(matchedEvent, auditEvent))
        {
            throw new InvalidDataException(
                $"Worker audit event '{auditEvent.EventId}' already exists with different content.");
        }

        return true;
    }

    private static bool AuditEventsEqual(
        WorkerAuditEvent left,
        WorkerAuditEvent right)
    {
        if (left.SchemaVersion != right.SchemaVersion ||
            !left.EventId.Equals(right.EventId, StringComparison.Ordinal) ||
            !left.EventType.Equals(right.EventType, StringComparison.Ordinal) ||
            !left.WorkerId.Equals(right.WorkerId, StringComparison.Ordinal) ||
            !string.Equals(left.TaskId, right.TaskId, StringComparison.Ordinal) ||
            !string.Equals(left.ProposalId, right.ProposalId, StringComparison.Ordinal) ||
            !left.TimestampUtc.Equals(right.TimestampUtc, StringComparison.Ordinal) ||
            !left.Summary.Equals(right.Summary, StringComparison.Ordinal) ||
            left.Details.Count != right.Details.Count)
        {
            return false;
        }

        foreach (var pair in left.Details)
        {
            if (!right.Details.TryGetValue(
                    pair.Key,
                    out var rightValues) ||
                !pair.Value.SequenceEqual(
                    rightValues,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateAuditEvent(WorkerAuditEvent auditEvent)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.EventId))
            throw new ArgumentException("Audit event id is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.EventType))
            throw new ArgumentException("Audit event type is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.WorkerId))
            throw new ArgumentException("Audit worker id is required.", nameof(auditEvent));
    }

    public Task RecordTaskDispatchedAsync(WorkerTaskPacket task) =>
        AppendEventAsync(BuildTaskDispatchedEvent(task));

    internal Task<bool> RecordTaskDispatchedIfCurrentSessionAsync(WorkerTaskPacket task) =>
        AppendEventIfCurrentSessionAsync(task.SessionGeneration, BuildTaskDispatchedEvent(task));

    internal Task<bool> RecordTaskDispatchedIfCurrentSessionAsync(
        WorkerTaskPacket task,
        CancellationToken cancellationToken) =>
        AppendEventIfCurrentSessionAsync(
            task.SessionGeneration,
            BuildTaskDispatchedEvent(task),
            cancellationToken);

    private static WorkerAuditEvent BuildTaskDispatchedEvent(WorkerTaskPacket task) =>
        new()
        {
            EventId = GmWorkerAuditEventIdGenerator.Create(),
            EventType = "task-dispatched",
            WorkerId = task.WorkerId,
            TaskId = task.TaskId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = $"Dispatched {task.TaskType} worker task.",
            Details = new Dictionary<string, IReadOnlyList<string>>
            {
                ["taskType"] = [ToKebabCase(task.TaskType)],
                ["responseContract"] = [task.ResponseContract],
                ["timeoutSeconds"] = [task.TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                ["allowedProposalPaths"] = task.AllowedProposalPaths,
                ["acceptanceCriteria"] = task.AcceptanceCriteria
            }
        };

    public Task RecordProposalReceivedAsync(WorkerProposal proposal) =>
        RecordProposalReceivedCoreAsync(proposal, writeLease: null);

    internal Task RecordProposalReceivedAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        WorkerProposal proposal) =>
        RecordProposalReceivedCoreAsync(proposal, writeLease);

    private Task RecordProposalReceivedCoreAsync(
        WorkerProposal proposal,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var auditEvent = new WorkerAuditEvent
        {
            EventId = GmWorkerAuditEventIdGenerator.Create(),
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
        };
        return writeLease == null
            ? AppendEventAsync(auditEvent)
            : AppendEventAsync(writeLease, auditEvent);
    }

    public Task RecordApplyDecisionAsync(WorkerProposal proposal, ApplyGateDecision decision) =>
        AppendEventAsync(BuildApplyDecisionEvent(proposal, decision));

    internal Task RecordApplyDecisionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        WorkerProposal proposal,
        ApplyGateDecision decision) =>
        AppendEventAsync(writeLease, BuildApplyDecisionEvent(proposal, decision));

    private static WorkerAuditEvent BuildApplyDecisionEvent(
        WorkerProposal proposal,
        ApplyGateDecision decision) =>
        new()
        {
            EventId = GmWorkerAuditEventIdGenerator.Create(),
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
        };

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

    private static string ToKebabCase(WorkerTaskType taskType) =>
        taskType switch
        {
            WorkerTaskType.ValidationRepair => "validation-repair",
            WorkerTaskType.NarrativeDraft => "narrative-draft",
            WorkerTaskType.LoreConsistency => "lore-consistency",
            WorkerTaskType.NpcAnalysis => "npc-analysis",
            WorkerTaskType.QteContent => "qte-content",
            _ => ToKebabCase(taskType.ToString())
        };

    private static string ToKebabCase(string value)
    {
        var result = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch))
            {
                if (i > 0)
                    result.Append('-');
                result.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }
}
