namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerBridgeProfileTemplates
{
    public const string RunnerRelativePath = "BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1";
    public const string CodexWorkerExecCommand = "codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

    public static IReadOnlyList<WorkerBridgeProfile> CreateDefaultTemplates() =>
    [
        CreateValidationRepairCodexTemplate(),
        CreateNarrativeDraftCodexTemplate(),
        CreateAnalysisCodexTemplate(),
        CreateInventoryContentCodexTemplate(),
        CreateSkillContentCodexTemplate(),
        CreateNpcContentCodexTemplate()
    ];

    public static WorkerBridgeProfile CreateValidationRepairCodexTemplate() => new()
    {
        WorkerId = "validation_repair_codex",
        DisplayName = "Codex validation repair",
        LaunchCommand = BuildRunnerLaunchCommand(CodexWorkerExecCommand, timeoutSeconds: 180),
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

    public static WorkerBridgeProfile CreateNarrativeDraftCodexTemplate() => new()
    {
        WorkerId = "narrative_draft_codex",
        DisplayName = "Codex narrative drafter",
        LaunchCommand = BuildRunnerLaunchCommand(CodexWorkerExecCommand, timeoutSeconds: 120),
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
        LaunchCommand = BuildRunnerLaunchCommand(CodexWorkerExecCommand, timeoutSeconds: 120),
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

    public static WorkerBridgeProfile CreateInventoryContentCodexTemplate() =>
        CreateContentAuthoringCodexTemplate(
            "inventory_content_codex",
            "Codex inventory content author",
            WorkerRole.InventoryContent,
            WorkerTaskType.InventoryContent,
            [
                "game_state/core/**",
                "game_state/inventory/**",
                "game_state/world/**",
                "game_state/skills/**",
                "lore/**",
                "Rules/**",
                "TaskGuides/**"
            ]);

    public static WorkerBridgeProfile CreateSkillContentCodexTemplate() =>
        CreateContentAuthoringCodexTemplate(
            "skill_content_codex",
            "Codex skill content author",
            WorkerRole.SkillContent,
            WorkerTaskType.SkillContent,
            [
                "game_state/core/**",
                "game_state/player/**",
                "game_state/skills/**",
                "game_state/combat/**",
                "game_state/world/**",
                "lore/**",
                "Rules/**",
                "TaskGuides/**"
            ]);

    public static WorkerBridgeProfile CreateNpcContentCodexTemplate() =>
        CreateContentAuthoringCodexTemplate(
            "npc_content_codex",
            "Codex NPC content author",
            WorkerRole.NpcContent,
            WorkerTaskType.NpcContent,
            [
                "game_state/core/**",
                "game_state/npcs/**",
                "game_state/factions/**",
                "game_state/quests/**",
                "game_state/world/**",
                "lore/**",
                "Rules/**",
                "TaskGuides/**"
            ]);

    private static WorkerBridgeProfile CreateContentAuthoringCodexTemplate(
        string workerId,
        string displayName,
        WorkerRole role,
        WorkerTaskType taskType,
        IReadOnlyList<string> readPaths) => new()
    {
        WorkerId = workerId,
        DisplayName = displayName,
        LaunchCommand = BuildRunnerLaunchCommand(CodexWorkerExecCommand, timeoutSeconds: 120),
        Role = role,
        Enabled = false,
        LaunchVisibility = WorkerLaunchVisibility.Hidden,
        TimeoutSeconds = 150,
        MaxConcurrentTasks = 1,
        Permissions = new WorkerScopePolicy
        {
            TaskTypes = [taskType],
            ReadPaths = readPaths,
            ProposalWritePaths = [],
            ProposalOnly = true,
            RequiresValidation = false
        }
    };

    public static string BuildRunnerLaunchCommand(string agentCommand, int timeoutSeconds) =>
        $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{RunnerRelativePath}\" -AgentCommand \"{agentCommand}\" -TimeoutSeconds {timeoutSeconds}";
}
