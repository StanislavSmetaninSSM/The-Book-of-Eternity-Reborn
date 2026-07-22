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
    QteContent,
    InventoryContent,
    SkillContent,
    NpcContent,
    GuardianAbodeContent,
    SoulContent,
    SocialDialogueContent,
    FactionContent,
    LocationContent,
    QuestContent,
    BookDocumentContent,
    EconomyCraftingContent,
    WorldStateContent,
    EncounterContent
}

public enum WorkerTaskType
{
    ValidationRepair,
    NarrativeDraft,
    Analysis,
    LoreConsistency,
    NpcAnalysis,
    QteContent,
    InventoryContent,
    SkillContent,
    NpcContent,
    GuardianAbodeContent,
    SoulContent,
    SocialDialogueContent,
    FactionContent,
    LocationContent,
    QuestContent,
    BookDocumentContent,
    EconomyCraftingContent,
    WorldStateContent,
    EncounterContent
}

public enum WorkerAuthoringDomain
{
    Inventory,
    Skill,
    Npc,
    GuardianAbode,
    Soul,
    SocialDialogue,
    Faction,
    Location,
    Quest,
    BookDocument,
    EconomyCrafting,
    WorldState,
    Encounter,
    Qte
}

public enum WorkerAfterlifeRealmGate
{
    None,
    ChaosSea,
    ShiningAbode,
    ShiningAbodePendingBootstrap
}

public static class WorkerTaskTypes
{
    public static bool IsContentAuthoring(WorkerTaskType taskType) =>
        taskType is WorkerTaskType.InventoryContent or
            WorkerTaskType.SkillContent or
            WorkerTaskType.NpcContent or
            WorkerTaskType.GuardianAbodeContent or
            WorkerTaskType.SoulContent or
            WorkerTaskType.SocialDialogueContent or
            WorkerTaskType.FactionContent or
            WorkerTaskType.LocationContent or
            WorkerTaskType.QuestContent or
            WorkerTaskType.BookDocumentContent or
            WorkerTaskType.EconomyCraftingContent or
            WorkerTaskType.WorldStateContent or
            WorkerTaskType.EncounterContent or
            WorkerTaskType.QteContent;

    public static bool IsProposalOnlyReview(WorkerTaskType taskType) =>
        taskType is WorkerTaskType.NarrativeDraft or
            WorkerTaskType.Analysis or
            WorkerTaskType.LoreConsistency or
            WorkerTaskType.NpcAnalysis ||
            IsContentAuthoring(taskType);
}

public enum WorkerProposalStatus
{
    Unspecified = 0,
    Completed,
    Failed,
    TimedOut,
    Rejected
}

public enum WorkerFileChangeKind
{
    Unspecified = 0,
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
    public WorkerRole Role { get; init; } = WorkerRole.ValidationRepair;
    public WorkerTaskType TaskType { get; init; } = WorkerTaskType.ValidationRepair;
    public string CreatedAtUtc { get; init; } = "";
    public int TimeoutSeconds { get; init; }
    public WorkerTurnReference SourceTurn { get; init; } = new();
    public IReadOnlyList<WorkerValidationIssue> ValidationIssues { get; init; } = [];
    public WorkerDraftRequest? DraftRequest { get; init; }
    public WorkerContentAuthoringRequest? AuthoringRequest { get; init; }
    public WorkerGuardianAbodeRequest? GuardianAbodeRequest { get; init; }
    public WorkerSoulContentRequest? SoulContentRequest { get; init; }
    public IReadOnlyList<WorkerFileReference> ContextFiles { get; init; } = [];
    public WorkerAfterlifeTaskContract? AfterlifeContract { get; init; }
    public IReadOnlyList<string> AllowedProposalPaths { get; init; } = [];
    public string ResponseContract { get; init; } = "worker-proposal-v1";
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public IReadOnlyList<string> ForbiddenActions { get; init; } = [];
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
    public string? Actor { get; init; }
    public string? Section { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
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

public sealed record WorkerContentAuthoringRequest
{
    public WorkerAuthoringDomain Domain { get; init; }
    public string Goal { get; init; } = "";
    public IReadOnlyList<string> EntityHints { get; init; } = [];
    public IReadOnlyList<string> RequiredLinks { get; init; } = [];
    public IReadOnlyList<string> OutputNotes { get; init; } = [];
}

public sealed record WorkerAfterlifeTaskContract
{
    public WorkerAfterlifeRealmGate RealmGate { get; init; } = WorkerAfterlifeRealmGate.None;
    public string CurrentRealm { get; init; } = "";
    public IReadOnlyList<string> ProgressionControlPaths { get; init; } = [];
    public IReadOnlyList<string> PendingControlFiles { get; init; } = [];
    public IReadOnlyList<string> AllowedAfterlifeSurfaces { get; init; } = [];
    public IReadOnlyList<string> RequiredReceipts { get; init; } = [];
    public IReadOnlyList<string> RequiredReports { get; init; } = [];
    public IReadOnlyList<string> ForbiddenMortalSubstitutes { get; init; } = [];
}

public sealed record WorkerGuardianAbodeRequest
{
    public string Realm { get; init; } = "";
    public IReadOnlyList<string> GuardianIds { get; init; } = [];
    public IReadOnlyList<string> AbodeIds { get; init; } = [];
    public IReadOnlyList<string> PendingControlFiles { get; init; } = [];
    public IReadOnlyList<string> FocusAreas { get; init; } = [];
    public IReadOnlyList<string> ReadScope { get; init; } = [];
}

public sealed record WorkerSoulContentRequest
{
    public string Realm { get; init; } = "";
    public string SoulContext { get; init; } = "";
    public IReadOnlyList<string> RequestedScope { get; init; } = [];
    public IReadOnlyList<string> ProgressionConstraints { get; init; } = [];
    public IReadOnlyList<string> ReadScope { get; init; } = [];
    public IReadOnlyList<string> PlayerOwnedIdentityFields { get; init; } = [];
}

public sealed record WorkerProposal
{
    public int SchemaVersion { get; init; } = 1;
    public string ProposalId { get; init; } = "";
    public string TaskId { get; init; } = "";
    public string WorkerId { get; init; } = "";
    public required WorkerProposalStatus Status { get; init; }
    public string Summary { get; init; } = "";
    public IReadOnlyList<WorkerChangedFile> ChangedFiles { get; init; } = [];
    public IReadOnlyList<WorkerFinding> Findings { get; init; } = [];
    public string? DraftText { get; init; }
    public WorkerContentAuthoringProposal? AuthoringProposal { get; init; }
    public WorkerAfterlifeProposalContract? AfterlifeProposal { get; init; }
    public WorkerGuardianAbodeProposal? GuardianAbodeProposal { get; init; }
    public WorkerSoulContentProposal? SoulContentProposal { get; init; }
    public WorkerSelfCheck SelfCheck { get; init; } = new();
    public string CreatedAtUtc { get; init; } = "";
}

public sealed record WorkerContentAuthoringProposal
{
    public WorkerAuthoringDomain Domain { get; init; }
    public string Goal { get; init; } = "";
    public IReadOnlyList<WorkerAuthoredEntity> CreatedEntities { get; init; } = [];
    public IReadOnlyList<WorkerAuthoredEntity> UpdatedEntities { get; init; } = [];
    public IReadOnlyList<WorkerRequiredEntityLink> RequiredLinks { get; init; } = [];
    public IReadOnlyList<WorkerValidatorRisk> ValidatorRisks { get; init; } = [];
    public IReadOnlyList<string> GmReviewNotes { get; init; } = [];
}

public sealed record WorkerAfterlifeProposalContract
{
    public WorkerAfterlifeRealmGate RealmGate { get; init; } = WorkerAfterlifeRealmGate.None;
    public IReadOnlyList<string> TargetSurfaces { get; init; } = [];
    public IReadOnlyList<string> RequiredReceipts { get; init; } = [];
    public IReadOnlyList<string> RequiredReports { get; init; } = [];
    public string PlayerVisibleSummary { get; init; } = "";
    public IReadOnlyList<string> GmReviewNotes { get; init; } = [];
    public IReadOnlyList<WorkerValidatorRisk> ValidatorRisks { get; init; } = [];
}

public sealed record WorkerGuardianAbodeProposal
{
    public string PlayerVisibleSummary { get; init; } = "";
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> GuardianUpdates { get; init; } = [];
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> AbodeUpdates { get; init; } = [];
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> ProjectSuggestions { get; init; } = [];
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> PowerReputationConsequences { get; init; } = [];
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> TradeFavorHooks { get; init; } = [];
    public IReadOnlyList<WorkerGuardianAbodeProposalItem> DossierNotes { get; init; } = [];
    public IReadOnlyList<string> RequiredReceipts { get; init; } = [];
    public IReadOnlyList<string> RequiredReports { get; init; } = [];
    public IReadOnlyList<WorkerValidatorRisk> ValidatorRisks { get; init; } = [];
    public IReadOnlyList<string> GmReviewNotes { get; init; } = [];
}

public sealed record WorkerGuardianAbodeProposalItem
{
    public string ItemId { get; init; } = "";
    public string TargetId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Visibility { get; init; } = "";
    public IReadOnlyList<string> TargetSurfaces { get; init; } = [];
    public IReadOnlyList<WorkerAuthoredField> Fields { get; init; } = [];
}

public sealed record WorkerSoulContentProposal
{
    public string PlayerVisibleSummary { get; init; } = "";
    public IReadOnlyList<WorkerSoulContentProposalItem> SafeSoulSummaries { get; init; } = [];
    public IReadOnlyList<WorkerSoulContentProposalItem> ProgressionSuggestions { get; init; } = [];
    public IReadOnlyList<WorkerSoulContentProposalItem> RewardNotes { get; init; } = [];
    public IReadOnlyList<WorkerSoulContentProposalItem> NextLifePreparationHooks { get; init; } = [];
    public IReadOnlyList<string> RequiredReceipts { get; init; } = [];
    public IReadOnlyList<string> RequiredReports { get; init; } = [];
    public IReadOnlyList<string> ForbiddenReadonlyFields { get; init; } = [];
    public IReadOnlyList<WorkerValidatorRisk> ValidatorRisks { get; init; } = [];
    public IReadOnlyList<string> GmReviewNotes { get; init; } = [];
}

public sealed record WorkerSoulContentProposalItem
{
    public string ItemId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Visibility { get; init; } = "";
    public IReadOnlyList<string> TargetSurfaces { get; init; } = [];
    public IReadOnlyList<WorkerAuthoredField> Fields { get; init; } = [];
}

public sealed record WorkerAuthoredEntity
{
    public string EntityType { get; init; } = "";
    public string EntityId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<WorkerAuthoredField> RequiredFields { get; init; } = [];
    public IReadOnlyList<string> Relationships { get; init; } = [];
}

public sealed record WorkerAuthoredField
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}

public sealed record WorkerRequiredEntityLink
{
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed record WorkerValidatorRisk
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Mitigation { get; init; } = "";
}

public sealed record WorkerChangedFile
{
    public string Path { get; init; } = "";
    public WorkerFileChangeKind ChangeKind { get; init; } = WorkerFileChangeKind.Unspecified;
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

public sealed record GmWorkerTaskRunResult
{
    public WorkerBridgeStatus Status { get; init; } = new();
    public IReadOnlyList<WorkerBridgeStatus> StatusHistory { get; init; } = [];
    public WorkerProposal? Proposal { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public bool TimedOut { get; init; }
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
