namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerBridgeProfileTemplates
{
    public const string RunnerRelativePath = "BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1";
    public const string CodexWorkerExecCommand = "codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

    private static readonly string RetiredCodexWorkerExecCommand =
        $"codex exec -m {"gpt-5" + ".5"} -c model_reasoning_effort=\"high\" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

    private static readonly string RetiredUnquotedCodexWorkerExecCommand =
        $"codex exec -m {"gpt-5" + ".5"} -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

    public static IReadOnlyList<WorkerBridgeProfile> CreateDefaultTemplates() =>
    [
        CreateValidationRepairCodexTemplate(),
        CreateNarrativeDraftCodexTemplate(),
        CreateAnalysisCodexTemplate(),
        CreateGuardianAbodeContentCodexTemplate(),
        CreateSoulContentCodexTemplate(),
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

    public static WorkerBridgeProfile CreateGuardianAbodeContentCodexTemplate() =>
        CreateContentAuthoringCodexTemplate(
            "guardian_abode_content_codex",
            "Codex Guardian/Abode content author",
            WorkerRole.GuardianAbodeContent,
            WorkerTaskType.GuardianAbodeContent,
            [
                "game_state/meta/guardians.json",
                "game_state/meta/guardian_projects.json",
                "game_state/meta/guardian_abode_residents.json",
                "game_state/meta/abode_power_journal.json",
                "game_state/meta/chaos_sea_guardian_politics.json",
                "game_state/meta/afterlife_chronicles.json",
                "game_state/control/system_guardian_attraction.json",
                "game_state/control/afterlife_return_guard.json",
                "game_state/control/progression_schedule.json",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            ]);

    public static WorkerBridgeProfile CreateSoulContentCodexTemplate() =>
        CreateContentAuthoringCodexTemplate(
            "soul_content_codex",
            "Codex soul content author",
            WorkerRole.SoulContent,
            WorkerTaskType.SoulContent,
            [
                "game_state/meta/soul_state.json",
                "game_state/meta/afterlife_chronicles.json",
                "game_state/meta/afterlife_global_flags.json",
                "game_state/control/progression_schedule.json",
                "game_state/control/pending_dice_state.json",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
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
        $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{RunnerRelativePath}\" -AgentCommand \"{EscapeForDoubleQuotedArgument(agentCommand)}\" -TimeoutSeconds {timeoutSeconds}";

    public static string MigrateRetiredCodexLaunchCommand(string? workerId, string? launchCommand)
    {
        if (string.IsNullOrEmpty(launchCommand))
            return launchCommand ?? string.Empty;

        var runnerTimeoutSeconds = GetBuiltInRunnerTimeoutSeconds(workerId);
        if (!runnerTimeoutSeconds.HasValue)
            return launchCommand;

        var retiredQuotedTemplate = BuildRunnerLaunchCommand(RetiredCodexWorkerExecCommand, runnerTimeoutSeconds.Value);
        var retiredUnquotedTemplate = BuildRunnerLaunchCommand(RetiredUnquotedCodexWorkerExecCommand, runnerTimeoutSeconds.Value);
        if (!string.Equals(launchCommand, retiredQuotedTemplate, StringComparison.Ordinal) &&
            !string.Equals(launchCommand, retiredUnquotedTemplate, StringComparison.Ordinal))
        {
            return launchCommand;
        }

        return BuildRunnerLaunchCommand(CodexWorkerExecCommand, runnerTimeoutSeconds.Value);
    }

    private static int? GetBuiltInRunnerTimeoutSeconds(string? workerId) => workerId switch
    {
        "validation_repair_codex" => 180,
        "analysis_codex" or
        "guardian_abode_content_codex" or
        "inventory_content_codex" or
        "narrative_draft_codex" or
        "npc_content_codex" or
        "skill_content_codex" or
        "soul_content_codex" => 120,
        _ => null
    };

    private static string EscapeForDoubleQuotedArgument(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
