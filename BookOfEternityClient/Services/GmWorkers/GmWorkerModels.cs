namespace BookOfEternityClient.Services.GmWorkers;

public enum WorkerLaunchVisibility
{
    Hidden
}

public enum WorkerRole
{
    ValidationRepair,
    NarrativeDraft,
    Analysis,
    LoreConsistency,
    NpcAnalysis,
    QteContent
}

public enum WorkerTaskType
{
    ValidationRepair,
    NarrativeDraft,
    Analysis,
    LoreConsistency,
    NpcAnalysis,
    QteContent
}

public enum WorkerProposalStatus
{
    Completed,
    Failed,
    TimedOut,
    Rejected
}

public enum WorkerFileChangeKind
{
    Add,
    Replace,
    Delete
}

public enum WorkerBridgeState
{
    Disabled,
    Starting,
    Ready,
    Busy,
    Failed,
    TimedOut,
    Stopped
}

public enum ApplyGateResult
{
    Accepted,
    Rejected,
    ValidationFailed
}

public sealed record WorkerBridgeProfile
{
    public string WorkerId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string LaunchCommand { get; init; } = "";
    public WorkerRole Role { get; init; } = WorkerRole.ValidationRepair;
    public bool Enabled { get; init; } = true;
    public WorkerLaunchVisibility LaunchVisibility { get; init; } = WorkerLaunchVisibility.Hidden;
    public int TimeoutSeconds { get; init; } = 180;
    public int MaxConcurrentTasks { get; init; } = 1;
    public WorkerScopePolicy Permissions { get; init; } = new();
}

public sealed record WorkerScopePolicy
{
    public IReadOnlyList<WorkerTaskType> TaskTypes { get; init; } = [];
    public IReadOnlyList<string> ReadPaths { get; init; } = [];
    public IReadOnlyList<string> ProposalWritePaths { get; init; } = [];
    public bool ProposalOnly { get; init; } = true;
    public bool RequiresValidation { get; init; } = false;
}

public sealed record WorkerTaskPacket
{
    public int SchemaVersion { get; init; } = 1;
    public string TaskId { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public WorkerTaskType TaskType { get; init; } = WorkerTaskType.ValidationRepair;
    public string CreatedAtUtc { get; init; } = "";
    public WorkerTurnReference SourceTurn { get; init; } = new();
    public IReadOnlyList<WorkerValidationIssue> ValidationIssues { get; init; } = [];
    public WorkerDraftRequest? DraftRequest { get; init; }
    public IReadOnlyList<WorkerFileReference> ContextFiles { get; init; } = [];
    public IReadOnlyList<string> AllowedProposalPaths { get; init; } = [];
    public string ResponseContract { get; init; } = "worker-proposal-v1";
    public string Instructions { get; init; } = "";
}

public sealed record WorkerTurnReference
{
    public string SessionId { get; init; } = "";
    public string RequestId { get; init; } = "";
    public int TurnNumber { get; init; }
}

public sealed record WorkerValidationIssue
{
    public string Code { get; init; } = "";
    public string Path { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed record WorkerFileReference
{
    public string Path { get; init; } = "";
    public string Sha256 { get; init; } = "";
}

public sealed record WorkerDraftRequest
{
    public string SceneGoal { get; init; } = "";
    public string Tone { get; init; } = "";
    public IReadOnlyList<string> ContinuityNotes { get; init; } = [];
    public string TargetLength { get; init; } = "";
}

public sealed record WorkerProposal
{
    public int SchemaVersion { get; init; } = 1;
    public string ProposalId { get; init; } = "";
    public string TaskId { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public WorkerProposalStatus Status { get; init; } = WorkerProposalStatus.Completed;
    public string Summary { get; init; } = "";
    public IReadOnlyList<WorkerChangedFile> ChangedFiles { get; init; } = [];
    public IReadOnlyList<WorkerFinding> Findings { get; init; } = [];
    public string? DraftText { get; init; }
    public WorkerSelfCheck SelfCheck { get; init; } = new();
    public string CreatedAtUtc { get; init; } = "";
}

public sealed record WorkerChangedFile
{
    public string Path { get; init; } = "";
    public WorkerFileChangeKind ChangeKind { get; init; } = WorkerFileChangeKind.Replace;
    public string? BeforeSha256 { get; init; }
    public string? AfterSha256 { get; init; }
    public string? ContentRef { get; init; }
}

public sealed record WorkerFinding
{
    public string Kind { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed record WorkerSelfCheck
{
    public bool ScopeReviewed { get; init; }
    public bool ValidationExpectedToPass { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record ApplyGateDecision
{
    public int SchemaVersion { get; init; } = 1;
    public string DecisionId { get; init; } = "";
    public string ProposalId { get; init; } = "";
    public ApplyGateResult Result { get; init; } = ApplyGateResult.Rejected;
    public ApplyGateScopeCheck ScopeCheck { get; init; } = new();
    public ApplyGateValidationCheck ValidationCheck { get; init; } = new();
    public IReadOnlyList<string> AppliedFiles { get; init; } = [];
    public IReadOnlyList<string> RejectionReasons { get; init; } = [];
    public string DecidedAtUtc { get; init; } = "";
}

public sealed record ApplyGateScopeCheck
{
    public bool Passed { get; init; }
    public IReadOnlyList<string> CheckedPaths { get; init; } = [];
    public IReadOnlyList<string> Violations { get; init; } = [];
}

public sealed record ApplyGateValidationCheck
{
    public bool Required { get; init; }
    public bool Passed { get; init; }
    public string Command { get; init; } = "";
    public int IssueCount { get; init; }
}

public sealed record WorkerBridgeStatus
{
    public string WorkerId { get; init; } = "";
    public WorkerBridgeState State { get; init; } = WorkerBridgeState.Disabled;
    public bool Ready { get; init; }
    public int? ProcessId { get; init; }
    public string? CurrentTaskId { get; init; }
    public string? LastError { get; init; }
    public string UpdatedAtUtc { get; init; } = "";
}

public sealed record WorkerAuditEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string EventId { get; init; } = "";
    public string EventType { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public string? TaskId { get; init; }
    public string? ProposalId { get; init; }
    public string TimestampUtc { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Details { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}

public sealed record WorkerRoutingResult(bool Found, WorkerBridgeProfile? Profile, string Reason);

public sealed record WorkerContractValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static WorkerContractValidationResult Success { get; } = new(true, []);

    public static WorkerContractValidationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToArray());
}
