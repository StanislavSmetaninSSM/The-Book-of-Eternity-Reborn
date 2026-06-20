namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerBridgeProfileTemplates
{
    public const string RunnerRelativePath = "BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1";
    public const string CodexBypassCommand = "codex --dangerously-bypass-approvals-and-sandbox";
    public const string GeminiCommand = "gemini";

    public static IReadOnlyList<WorkerBridgeProfile> CreateDefaultTemplates() =>
    [
        CreateValidationRepairCodexTemplate(),
        CreateNarrativeDraftGeminiTemplate(),
        CreateAnalysisCodexTemplate()
    ];

    public static WorkerBridgeProfile CreateValidationRepairCodexTemplate() => new()
    {
        WorkerId = "validation_repair_codex",
        DisplayName = "Codex validation repair",
        LaunchCommand = BuildRunnerLaunchCommand(CodexBypassCommand, timeoutSeconds: 180),
        Role = WorkerRole.ValidationRepair,
        Enabled = false,
        LaunchVisibility = WorkerLaunchVisibility.Hidden,
        TimeoutSeconds = 210,
        MaxConcurrentTasks = 1,
        Permissions = new WorkerScopePolicy
        {
            TaskTypes = [WorkerTaskType.ValidationRepair],
            ReadPaths = ["game_state/**", "lore/**", "input/**", "ready/**"],
            ProposalWritePaths = ["game_state/**", "lore/**", "ready/**"],
            ProposalOnly = false,
            RequiresValidation = true
        }
    };

    public static WorkerBridgeProfile CreateNarrativeDraftGeminiTemplate() => new()
    {
        WorkerId = "narrative_draft_gemini",
        DisplayName = "Gemini narrative drafter",
        LaunchCommand = BuildRunnerLaunchCommand(GeminiCommand, timeoutSeconds: 120),
        Role = WorkerRole.NarrativeDraft,
        Enabled = false,
        LaunchVisibility = WorkerLaunchVisibility.Hidden,
        TimeoutSeconds = 150,
        MaxConcurrentTasks = 1,
        Permissions = new WorkerScopePolicy
        {
            TaskTypes = [WorkerTaskType.NarrativeDraft],
            ReadPaths = ["game_state/**", "lore/**", "Rules/**", "TaskGuides/**"],
            ProposalWritePaths = [],
            ProposalOnly = true,
            RequiresValidation = false
        }
    };

    public static WorkerBridgeProfile CreateAnalysisCodexTemplate() => new()
    {
        WorkerId = "analysis_codex",
        DisplayName = "Codex analysis worker",
        LaunchCommand = BuildRunnerLaunchCommand(CodexBypassCommand, timeoutSeconds: 120),
        Role = WorkerRole.Analysis,
        Enabled = false,
        LaunchVisibility = WorkerLaunchVisibility.Hidden,
        TimeoutSeconds = 150,
        MaxConcurrentTasks = 1,
        Permissions = new WorkerScopePolicy
        {
            TaskTypes = [WorkerTaskType.Analysis],
            ReadPaths = ["game_state/**", "lore/**", "Rules/**", "TaskGuides/**"],
            ProposalWritePaths = [],
            ProposalOnly = true,
            RequiresValidation = false
        }
    };

    public static string BuildRunnerLaunchCommand(string agentCommand, int timeoutSeconds) =>
        $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{RunnerRelativePath}\" -AgentCommand \"{agentCommand}\" -TimeoutSeconds {timeoutSeconds}";
}
