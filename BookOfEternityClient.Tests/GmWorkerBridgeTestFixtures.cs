using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static class GmWorkerBridgeTestFixtures
{
    public static WorkerBridgeProfile ValidationRepairCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate() with { Enabled = true };

    public static WorkerBridgeProfile NarrativeDraftGeminiProfile() =>
        GmWorkerBridgeProfileTemplates.CreateNarrativeDraftGeminiTemplate() with { Enabled = true };

    public static WorkerBridgeProfile AnalysisCodexProfile() =>
        GmWorkerBridgeProfileTemplates.CreateAnalysisCodexTemplate() with { Enabled = true };


    public static WorkerTaskPacket ValidationRepairTask() => new()
    {
        TaskId = "worker_task_20260620_0001",
        WorkerId = "validation_repair_codex",
        TaskType = WorkerTaskType.ValidationRepair,
        CreatedAtUtc = "2026-06-20T00:00:00Z",
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
        Instructions = "Return a minimal repair proposal. Do not change files outside allowedProposalPaths."
    };

    public static WorkerTaskPacket NarrativeDraftTask() => new()
    {
        TaskId = "worker_task_20260620_0002",
        WorkerId = "narrative_draft_gemini",
        TaskType = WorkerTaskType.NarrativeDraft,
        CreatedAtUtc = "2026-06-20T00:05:00Z",
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
        Instructions = "Return draftText and optional findings only. Do not include changedFiles."
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
        WorkerId = "narrative_draft_gemini",
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
}
