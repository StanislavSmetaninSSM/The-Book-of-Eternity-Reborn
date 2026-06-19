using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed record GmWorkerProposalInboxEntry
{
    public string ProposalId { get; init; } = "";
    public string ProposalPath { get; init; } = "";
    public bool IsReadable { get; init; }
    public string UnreadableReason { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public string TaskId { get; init; } = "";
    public WorkerTaskType? TaskType { get; init; }
    public WorkerProposalStatus? Status { get; init; }
    public string Summary { get; init; } = "";
    public string CreatedAtUtc { get; init; } = "";
    public string ReviewMode { get; init; } = "review";
    public bool HasDraftText { get; init; }
    public string DraftText { get; init; } = "";
    public int ChangedFileCount { get; init; }
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
    public int FindingCount { get; init; }
    public IReadOnlyList<WorkerFinding> Findings { get; init; } = [];
    public IReadOnlyList<string> SelfCheckNotes { get; init; } = [];
    public string ApplyState { get; init; } = "";
    public IReadOnlyList<string> RelatedAuditEventTypes { get; init; } = [];
    public IReadOnlyList<string> RelatedAuditSummaries { get; init; } = [];
}

public sealed class GmWorkerProposalInboxService
{
    private const string ProposalFileName = "proposal.json";

    private readonly FileSystemManager _fs;

    public GmWorkerProposalInboxService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task<IReadOnlyList<GmWorkerProposalInboxEntry>> ListAsync()
    {
        var proposalRoot = _fs.ResolvePath(GmWorkerProposalStore.ProposalRoot);
        if (!Directory.Exists(proposalRoot))
            return [];

        var auditEvents = await ReadAuditEventsSafeAsync();
        var entries = new List<GmWorkerProposalInboxEntry>();
        foreach (var proposalDirectory in Directory.EnumerateDirectories(proposalRoot))
        {
            var proposalPath = Path.Combine(proposalDirectory, ProposalFileName);
            if (!File.Exists(proposalPath))
                continue;

            var proposalId = Path.GetFileName(proposalDirectory);
            var relativePath = $"{GmWorkerProposalStore.ProposalRoot}/{proposalId}/{ProposalFileName}";
            entries.Add(await ReadEntryAsync(proposalId, relativePath, auditEvents));
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAtUtc, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProposalId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<GmWorkerProposalInboxEntry?> ReadAsync(string proposalId)
    {
        if (!IsSafeId(proposalId))
            return null;

        var relativePath = GmWorkerProposalStore.GetProposalPath(proposalId);
        if (!_fs.FileExists(relativePath))
            return null;

        var auditEvents = await ReadAuditEventsSafeAsync();
        return await ReadEntryAsync(proposalId, relativePath, auditEvents);
    }

    private async Task<GmWorkerProposalInboxEntry> ReadEntryAsync(
        string proposalId,
        string relativePath,
        IReadOnlyList<WorkerAuditEvent> auditEvents)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return BuildUnreadableEntry(proposalId, relativePath, "Proposal JSON is missing or empty.");

        WorkerProposal? proposal;
        try
        {
            proposal = GmWorkerJson.Deserialize<WorkerProposal>(json);
        }
        catch (Exception ex)
        {
            return BuildUnreadableEntry(
                proposalId,
                relativePath,
                $"Proposal JSON is malformed: {ex.Message}");
        }

        if (proposal == null)
            return BuildUnreadableEntry(proposalId, relativePath, "Proposal JSON is malformed: empty object.");

        var task = await ReadTaskSafeAsync(proposal.TaskId);
        var relatedAudit = auditEvents
            .Where(auditEvent =>
                string.Equals(auditEvent.ProposalId, proposal.ProposalId, StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(proposal.TaskId) &&
                 string.Equals(auditEvent.TaskId, proposal.TaskId, StringComparison.Ordinal)))
            .OrderBy(auditEvent => auditEvent.TimestampUtc, StringComparer.Ordinal)
            .ToArray();

        return new GmWorkerProposalInboxEntry
        {
            ProposalId = proposal.ProposalId,
            ProposalPath = relativePath,
            IsReadable = true,
            WorkerId = proposal.WorkerId,
            TaskId = proposal.TaskId,
            TaskType = task?.TaskType,
            Status = proposal.Status,
            Summary = proposal.Summary,
            CreatedAtUtc = proposal.CreatedAtUtc,
            ReviewMode = ResolveReviewMode(proposal, task),
            HasDraftText = !string.IsNullOrWhiteSpace(proposal.DraftText),
            DraftText = proposal.DraftText ?? "",
            ChangedFileCount = proposal.ChangedFiles.Count,
            ChangedFiles = proposal.ChangedFiles.Select(file => file.Path).ToArray(),
            FindingCount = proposal.Findings.Count,
            Findings = proposal.Findings,
            SelfCheckNotes = proposal.SelfCheck.Notes,
            ApplyState = ResolveApplyState(relatedAudit),
            RelatedAuditEventTypes = relatedAudit.Select(auditEvent => auditEvent.EventType).Distinct(StringComparer.Ordinal).ToArray(),
            RelatedAuditSummaries = relatedAudit.Select(auditEvent => $"{auditEvent.EventType}: {auditEvent.Summary}").ToArray()
        };
    }

    private async Task<WorkerTaskPacket?> ReadTaskSafeAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return null;

        var taskPath = GmWorkerBridgePool.GetTaskPacketPath(taskId);
        var json = await _fs.ReadFileAsync(taskPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return GmWorkerJson.Deserialize<WorkerTaskPacket>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<WorkerAuditEvent>> ReadAuditEventsSafeAsync()
    {
        try
        {
            return await new GmWorkerAuditLog(_fs).ReadEventsAsync();
        }
        catch
        {
            return [];
        }
    }

    private static GmWorkerProposalInboxEntry BuildUnreadableEntry(
        string proposalId,
        string relativePath,
        string reason) =>
        new()
        {
            ProposalId = proposalId,
            ProposalPath = relativePath,
            IsReadable = false,
            UnreadableReason = reason,
            ReviewMode = "unreadable"
        };

    private static string ResolveReviewMode(WorkerProposal proposal, WorkerTaskPacket? task)
    {
        if (task?.TaskType is WorkerTaskType.NarrativeDraft or WorkerTaskType.Analysis or
            WorkerTaskType.LoreConsistency or WorkerTaskType.NpcAnalysis or WorkerTaskType.QteContent)
        {
            return "review-only";
        }

        return proposal.ChangedFiles.Count > 0 ? "apply-gate" : "review";
    }

    private static string ResolveApplyState(IReadOnlyList<WorkerAuditEvent> relatedAudit)
    {
        if (relatedAudit.Any(evt => evt.EventType == "proposal-applied"))
            return "applied";
        if (relatedAudit.Any(evt => evt.EventType == "proposal-validation-failed"))
            return "validation-failed";
        if (relatedAudit.Any(evt => evt.EventType == "proposal-rejected"))
            return "rejected";
        if (relatedAudit.Any(evt => evt.EventType == "proposal-received"))
            return "received";
        return "";
    }

    private static bool IsSafeId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '_' or '-');
}
