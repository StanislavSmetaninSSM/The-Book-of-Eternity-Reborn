using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static class GmWorkerBridgeTestFixtures
{
    public static WorkerBridgeProfile ValidationRepairCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile NarrativeDraftCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateNarrativeDraftCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile AnalysisCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateAnalysisCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile InventoryContentCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateInventoryContentCodexTemplate() with { Enabled = true };

    public static WorkerTaskPacket ValidationRepairTask() => new()
    {
        TaskId = "worker_task_20260620_0001",
        WorkerId = "validation_repair_codex",
        Role = WorkerRole.ValidationRepair,
        TaskType = WorkerTaskType.ValidationRepair,
        CreatedAtUtc = "2026-06-20T00:00:00Z",
        TimeoutSeconds = 210,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        },
        ValidationIssues =
        [
            new WorkerValidationIssue
            {
                Code = "normalized_weather_missing_description",
                Path = "game_state/world/weather.json",
                Message = "normalizedWeatherState.description is required."
            }
        ],
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/weather.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = ["game_state/world/weather.json"],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Validation must pass after the apply gate applies proposed changes."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not create terminal signals manually."
        ],
        Instructions = "Return a minimal repair proposal. Do not change files outside allowedProposalPaths."
    };

    public static WorkerTaskPacket NarrativeDraftTask() => new()
    {
        TaskId = "worker_task_20260620_0002",
        WorkerId = "narrative_draft_codex",
        Role = WorkerRole.NarrativeDraft,
        TaskType = WorkerTaskType.NarrativeDraft,
        CreatedAtUtc = "2026-06-20T00:05:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        },
        DraftRequest = new WorkerDraftRequest
        {
            SceneGoal = "Draft a tense description of the locked manor corridor before the player chooses how to proceed.",
            Tone = "dark fantasy, concise, natural Russian prose",
            ContinuityNotes =
            [
                "The player is currently inside the mortal world.",
                "Do not resolve the player's action.",
                "Do not introduce canonical state changes."
            ],
            TargetLength = "120-180 words"
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/current_location.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include draftText for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return draftText and optional findings only. Do not include changedFiles."
    };

    public static WorkerTaskPacket InventoryContentTask() => new()
    {
        TaskId = "worker_task_20260620_0003",
        WorkerId = "inventory_content_codex",
        Role = WorkerRole.InventoryContent,
        TaskType = WorkerTaskType.InventoryContent,
        CreatedAtUtc = "2026-06-20T00:45:00Z",
        TimeoutSeconds = 150,
        SourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 14
        },
        AuthoringRequest = new WorkerContentAuthoringRequest
        {
            Domain = WorkerAuthoringDomain.Inventory,
            Goal = "Prepare stealth inventory item proposals for the current manor scene.",
            EntityHints = ["lockpick set"],
            RequiredLinks = ["player inventory"],
            OutputNotes = ["Return structured proposal only."]
        },
        ContextFiles =
        [
            new WorkerFileReference
            {
                Path = "game_state/world/current_location.json",
                Sha256 = "example"
            }
        ],
        AllowedProposalPaths = [],
        AcceptanceCriteria =
        [
            "Return a worker-proposal-v1 JSON proposal.",
            "Include authoringProposal for main-GM review."
        ],
        ForbiddenActions =
        [
            "Do not edit canonical game_session files directly.",
            "Do not include changedFiles."
        ],
        Instructions = "Return authoringProposal only. Do not include changedFiles."
    };

    public static WorkerProposal ValidationRepairProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0001",
        TaskId = "worker_task_20260620_0001",
        WorkerId = "validation_repair_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Added the missing normalized weather description.",
        ChangedFiles =
        [
            new WorkerChangedFile
            {
                Path = "game_state/world/weather.json",
                ChangeKind = WorkerFileChangeKind.Replace,
                BeforeSha256 = "example",
                AfterSha256 = "example-after",
                ContentRef = "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json"
            }
        ],
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = []
        },
        CreatedAtUtc = "2026-06-20T00:00:15Z"
    };

    public static WorkerProposal NarrativeDraftProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0002",
        TaskId = "worker_task_20260620_0002",
        WorkerId = "narrative_draft_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Drafted corridor narration for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "continuity-note",
                Message = "Draft avoids resolving the player's next action."
            }
        ],
        DraftText = "Черновик сцены для главного ГМа. Этот текст не показывается игроку автоматически.",
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T00:05:20Z"
    };

    public static WorkerProposal InventoryContentProposal() => new()
    {
        ProposalId = "worker_proposal_20260620_0003",
        TaskId = "worker_task_20260620_0003",
        WorkerId = "inventory_content_codex",
        Status = WorkerProposalStatus.Completed,
        Summary = "Prepared stealth inventory item proposals for main-GM review.",
        ChangedFiles = [],
        Findings =
        [
            new WorkerFinding
            {
                Kind = "validator-risk",
                Message = "Accepted items must be linked to an inventory container by the main GM."
            }
        ],
        AuthoringProposal = new WorkerContentAuthoringProposal
        {
            Domain = WorkerAuthoringDomain.Inventory,
            Goal = "Prepare stealth inventory item proposals for the current manor scene.",
            CreatedEntities =
            [
                new WorkerAuthoredEntity
                {
                    EntityType = "item",
                    EntityId = "item_valmont_lockpick_set",
                    DisplayName = "Набор тонких отмычек Вальмонта",
                    Summary = "Компактный набор для тихого вскрытия простых замков.",
                    RequiredFields =
                    [
                        new WorkerAuthoredField
                        {
                            Name = "slot",
                            Value = "hands"
                        }
                    ],
                    Relationships = ["player inventory", "lockpicking QTE"]
                }
            ],
            RequiredLinks =
            [
                new WorkerRequiredEntityLink
                {
                    Source = "item_valmont_lockpick_set",
                    Target = "player_inventory",
                    Reason = "Main GM must decide whether the item is discovered or already carried."
                }
            ],
            ValidatorRisks =
            [
                new WorkerValidatorRisk
                {
                    Code = "inventory_storage_link_required",
                    Message = "Item proposal is useless unless linked to an inventory container.",
                    Mitigation = "Main GM should add accepted items through the normal inventory state surface."
                }
            ],
            GmReviewNotes = ["Review balance before adding bonuses."]
        },
        SelfCheck = new WorkerSelfCheck
        {
            ScopeReviewed = true,
            ValidationExpectedToPass = true,
            Notes = ["Proposal-only authoring task; no file changes included."]
        },
        CreatedAtUtc = "2026-06-20T00:45:20Z"
    };
}
