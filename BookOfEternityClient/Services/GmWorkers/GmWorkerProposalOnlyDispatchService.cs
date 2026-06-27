using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

public enum GmWorkerProposalOnlyDispatchOutcome
{
    Completed,
    SkippedNoWorker,
    InvalidRequest,
    WorkerFailed,
    WorkerTimedOut,
    ProposalRejected
}

public sealed record GmWorkerProposalOnlyDispatchRequest
{
    public WorkerTaskType TaskType { get; init; }
    public WorkerTurnReference SourceTurn { get; init; } = new();
    public string SceneGoal { get; init; } = "";
    public string Tone { get; init; } = "";
    public IReadOnlyList<string> ContinuityNotes { get; init; } = [];
    public string TargetLength { get; init; } = "";
    public string AnalysisGoal { get; init; } = "";
    public IReadOnlyList<string> Questions { get; init; } = [];
    public WorkerContentAuthoringRequest? AuthoringRequest { get; init; }
    public IReadOnlyList<string> ContextPaths { get; init; } = [];

    public static GmWorkerProposalOnlyDispatchRequest NarrativeDraft(
        WorkerTurnReference sourceTurn,
        string sceneGoal,
        string tone,
        IReadOnlyList<string> continuityNotes,
        string targetLength,
        IReadOnlyList<string> contextPaths) =>
        new()
        {
            TaskType = WorkerTaskType.NarrativeDraft,
            SourceTurn = sourceTurn,
            SceneGoal = sceneGoal,
            Tone = tone,
            ContinuityNotes = continuityNotes,
            TargetLength = targetLength,
            ContextPaths = contextPaths
        };

    public static GmWorkerProposalOnlyDispatchRequest Analysis(
        WorkerTurnReference sourceTurn,
        string analysisGoal,
        IReadOnlyList<string> questions,
        IReadOnlyList<string> contextPaths) =>
        new()
        {
            TaskType = WorkerTaskType.Analysis,
            SourceTurn = sourceTurn,
            AnalysisGoal = analysisGoal,
            Questions = questions,
            ContextPaths = contextPaths
        };

    public static GmWorkerProposalOnlyDispatchRequest ContentAuthoring(
        WorkerTaskType taskType,
        WorkerTurnReference sourceTurn,
        WorkerContentAuthoringRequest authoringRequest,
        IReadOnlyList<string> contextPaths) =>
        new()
        {
            TaskType = taskType,
            SourceTurn = sourceTurn,
            AuthoringRequest = authoringRequest,
            ContextPaths = contextPaths
        };
}

public sealed record GmWorkerProposalOnlyDispatchResult
{
    public GmWorkerProposalOnlyDispatchOutcome Outcome { get; init; }
    public string TaskId { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public WorkerTaskType? TaskType { get; init; }
    public string ProposalId { get; init; } = "";
    public string FallbackReason { get; init; } = "";
}

public sealed class GmWorkerProposalOnlyDispatchService
{
    private readonly FileSystemManager _fs;
    private readonly GmWorkerBridgePool _bridgePool;
    private readonly GmWorkerAuditLog _auditLog;

    public GmWorkerProposalOnlyDispatchService(
        FileSystemManager fs,
        GmWorkerBridgePool bridgePool,
        GmWorkerAuditLog auditLog)
    {
        _fs = fs;
        _bridgePool = bridgePool;
        _auditLog = auditLog;
    }

    public async Task<GmWorkerProposalOnlyDispatchResult> DispatchAsync(
        IReadOnlyList<WorkerBridgeProfile> profiles,
        GmWorkerProposalOnlyDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
            return Invalid(validationError, request.TaskType);

        var routing = GmWorkerBridgePool.SelectWorkerForTask(profiles, request.TaskType);
        if (!routing.Found || routing.Profile == null)
        {
            return new GmWorkerProposalOnlyDispatchResult
            {
                Outcome = GmWorkerProposalOnlyDispatchOutcome.SkippedNoWorker,
                TaskType = request.TaskType,
                FallbackReason = routing.Reason
            };
        }

        WorkerTaskPacket task;
        try
        {
            task = await BuildTaskAsync(routing.Profile, request);
        }
        catch (Exception ex)
        {
            await RecordDispatchFailureAsync(routing.Profile.WorkerId, "", ex.Message);
            return Invalid(ex.Message, request.TaskType, routing.Profile.WorkerId);
        }

        var run = await _bridgePool.RunTaskAsync(routing.Profile, task, cancellationToken);
        if (run.Proposal == null)
        {
            var lastError = run.Status.LastError ?? "Worker did not return a valid proposal.";
            var outcome = run.TimedOut || run.Status.State == WorkerBridgeState.TimedOut
                ? GmWorkerProposalOnlyDispatchOutcome.WorkerTimedOut
                : lastError.Contains("proposal-only", StringComparison.OrdinalIgnoreCase) ||
                  lastError.Contains("changedFiles", StringComparison.OrdinalIgnoreCase) ||
                  lastError.Contains("proposal JSON is malformed", StringComparison.OrdinalIgnoreCase)
                    ? GmWorkerProposalOnlyDispatchOutcome.ProposalRejected
                    : GmWorkerProposalOnlyDispatchOutcome.WorkerFailed;
            return new GmWorkerProposalOnlyDispatchResult
            {
                Outcome = outcome,
                TaskId = task.TaskId,
                WorkerId = routing.Profile.WorkerId,
                TaskType = task.TaskType,
                FallbackReason = lastError
            };
        }

        return new GmWorkerProposalOnlyDispatchResult
        {
            Outcome = GmWorkerProposalOnlyDispatchOutcome.Completed,
            TaskId = task.TaskId,
            WorkerId = routing.Profile.WorkerId,
            TaskType = task.TaskType,
            ProposalId = run.Proposal.ProposalId
        };
    }

    private async Task<WorkerTaskPacket> BuildTaskAsync(
        WorkerBridgeProfile profile,
        GmWorkerProposalOnlyDispatchRequest request)
    {
        var taskId = $"worker_task_{TaskTypeSegment(request.TaskType)}_{Guid.NewGuid():N}";
        var createdAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var contextFiles = await BuildContextFilesAsync(profile, request.ContextPaths);

        return request.TaskType switch
        {
            WorkerTaskType.NarrativeDraft => GmWorkerTaskPacketBuilder.BuildNarrativeDraftTask(
                profile,
                taskId,
                request.SourceTurn,
                new WorkerDraftRequest
                {
                    SceneGoal = request.SceneGoal,
                    Tone = request.Tone,
                    ContinuityNotes = request.ContinuityNotes,
                    TargetLength = request.TargetLength
                },
                contextFiles,
                createdAtUtc),
            WorkerTaskType.Analysis => GmWorkerTaskPacketBuilder.BuildAnalysisTask(
                profile,
                taskId,
                request.SourceTurn,
                request.AnalysisGoal,
                request.Questions,
                contextFiles,
                createdAtUtc),
            _ when WorkerTaskTypes.IsContentAuthoring(request.TaskType) =>
                GmWorkerTaskPacketBuilder.BuildContentAuthoringTask(
                    profile,
                    request.TaskType,
                    taskId,
                    request.SourceTurn,
                    request.AuthoringRequest ?? new WorkerContentAuthoringRequest(),
                    contextFiles,
                    createdAtUtc),
            _ => throw new InvalidOperationException($"Unsupported proposal-only task type: {request.TaskType}.")
        };
    }

    private async Task<IReadOnlyList<WorkerFileReference>> BuildContextFilesAsync(
        WorkerBridgeProfile profile,
        IReadOnlyList<string> contextPaths)
    {
        var result = new List<WorkerFileReference>();
        foreach (var path in contextPaths
                     .Select(path => path.Replace('\\', '/'))
                     .Where(GmWorkerContractValidator.IsSafeRelativePath)
                     .Where(path => profile.Permissions.ReadPaths.Any(pattern => GmWorkerContractValidator.PathMatches(pattern, path)))
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var content = await _fs.ReadFileAsync(path);
            result.Add(new WorkerFileReference
            {
                Path = path,
                Sha256 = content == null ? "missing" : ComputeSha256(content)
            });
        }

        return result;
    }

    private Task RecordDispatchFailureAsync(string workerId, string taskId, string summary) =>
        _auditLog.AppendEventAsync(new WorkerAuditEvent
        {
            EventId = "worker_audit_" + Guid.NewGuid().ToString("N"),
            EventType = "proposal-only-dispatch-failed",
            WorkerId = workerId,
            TaskId = string.IsNullOrWhiteSpace(taskId) ? null : taskId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = summary
        });

    private static string ValidateRequest(GmWorkerProposalOnlyDispatchRequest request)
    {
        return request.TaskType switch
        {
            WorkerTaskType.NarrativeDraft when string.IsNullOrWhiteSpace(request.SceneGoal) =>
                "Narrative draft dispatch requires sceneGoal.",
            WorkerTaskType.NarrativeDraft when string.IsNullOrWhiteSpace(request.Tone) =>
                "Narrative draft dispatch requires tone.",
            WorkerTaskType.NarrativeDraft when string.IsNullOrWhiteSpace(request.TargetLength) =>
                "Narrative draft dispatch requires targetLength.",
            WorkerTaskType.Analysis when string.IsNullOrWhiteSpace(request.AnalysisGoal) =>
                "Analysis dispatch requires analysisGoal.",
            WorkerTaskType.NarrativeDraft or WorkerTaskType.Analysis => "",
            _ when WorkerTaskTypes.IsContentAuthoring(request.TaskType) && request.AuthoringRequest == null =>
                "Content authoring dispatch requires authoringRequest.",
            _ when WorkerTaskTypes.IsContentAuthoring(request.TaskType) && string.IsNullOrWhiteSpace(request.AuthoringRequest?.Goal) =>
                "Content authoring dispatch requires authoringRequest.goal.",
            _ when WorkerTaskTypes.IsContentAuthoring(request.TaskType) => "",
            _ => $"Unsupported proposal-only task type: {request.TaskType}."
        };
    }

    private static GmWorkerProposalOnlyDispatchResult Invalid(
        string reason,
        WorkerTaskType taskType,
        string workerId = "") =>
        new()
        {
            Outcome = GmWorkerProposalOnlyDispatchOutcome.InvalidRequest,
            WorkerId = workerId,
            TaskType = taskType,
            FallbackReason = reason
        };

    private static string TaskTypeSegment(WorkerTaskType taskType) =>
        taskType switch
        {
            WorkerTaskType.NarrativeDraft => "narrative_draft",
            WorkerTaskType.Analysis => "analysis",
            _ => taskType.ToString().ToLowerInvariant()
        };

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
