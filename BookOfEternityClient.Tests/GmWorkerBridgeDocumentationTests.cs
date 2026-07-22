using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeDocumentationTests
{
    [Fact]
    public void GmWorkerBridgeGuide_IsReferencedByLauncherAndExamplesManifest()
    {
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/CLI_Launch_Script.md");
        var launcherGenerator = ReadRepoFile("BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1");
        var manifest = ReadRepoFile("Examples/example_validation_manifest.json");

        Assert.Contains("OtherGuides/GM_Worker_Bridges.md", launcher, StringComparison.Ordinal);
        Assert.Contains("OtherGuides/GM_Worker_Bridges.md", launcherGenerator, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Validation_Repair.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Narrative_Draft.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Content_Authoring.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Skill_Content.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Npc_Content.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Afterlife_Contract.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Guardian_Abode_Content.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Soul_Content.txt", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void GmLauncherScript_DoesNotContainStaleWorktreePaths()
    {
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/CLI_Launch_Script.md");

        Assert.Contains("{{REPO_ROOT}}", launcher, StringComparison.Ordinal);
        Assert.Contains("{{GAME_SESSION}}", launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("boe-worktrees", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("E:\\Games\\worktrees\\", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1127-agent-console-live-e2e", launcher, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GmLauncherGenerator_AcceptsExplicitGameSessionPathForSandboxedRuns()
    {
        var generator = ReadRepoFile("BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1");
        var wrapper = ReadRepoFile("BookOfEternityClient/Launcher/Start_GM_Daemon.ps1");
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("[string]$GameSessionPath", generator, StringComparison.Ordinal);
        Assert.Contains("[switch]$UsePlaceholders", generator, StringComparison.Ordinal);
        Assert.Contains("Resolve-Path $GameSessionPath", generator, StringComparison.Ordinal);
        Assert.Contains("CLI_Launch_Script.generated.md", wrapper, StringComparison.Ordinal);
        Assert.Contains("-OutputPath $generatedLaunchScriptPath", wrapper, StringComparison.Ordinal);
        Assert.Contains("-GameSessionPath $GameSessionPath", wrapper, StringComparison.Ordinal);
        Assert.Contains("LaunchScriptPath = $generatedLaunchScriptPath", wrapper, StringComparison.Ordinal);
        Assert.Contains("[string]$LaunchScriptPath", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void GmDaemonScript_InitializesUtf8ConsoleOutputForRussianDiagnostics()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("$OutputEncoding = [System.Text.UTF8Encoding]::new($false)", daemon, StringComparison.Ordinal);
        Assert.Contains("[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)", daemon, StringComparison.Ordinal);
        Assert.Contains("[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)", daemon, StringComparison.Ordinal);
        Assert.Contains("chcp 65001", daemon, StringComparison.Ordinal);
        Assert.Contains("Add-Content -Path $LogFile -Value $logLine -Encoding UTF8", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void MainGmPrompt_DocumentsExplicitWorkerDelegationFlow()
    {
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/CLI_Launch_Script.md");

        Assert.Contains("dispatchworkertask", launcher, StringComparison.Ordinal);
        Assert.Contains("validation-repair", launcher, StringComparison.Ordinal);
        Assert.Contains("narrative-draft", launcher, StringComparison.Ordinal);
        Assert.Contains("analysis", launcher, StringComparison.Ordinal);
        Assert.Contains("proposal-only", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apply gate", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("main GM remains", launcher, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GmWorkerBridgeExamples_DocumentHiddenWorkersProposalOnlyAndApplyGate()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var narrative = ReadRepoFile("Examples/E_CLI_GM_Worker_Narrative_Draft.txt");
        var contentAuthoring = ReadRepoFile("Examples/E_CLI_GM_Worker_Content_Authoring.txt");
        var skillContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Skill_Content.txt");
        var npcContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Npc_Content.txt");
        var afterlifeContract = ReadRepoFile("Examples/E_CLI_GM_Worker_Afterlife_Contract.txt");
        var guardianAbodeContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt");
        var soulContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Soul_Content.txt");

        Assert.Contains("hidden/background", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apply gate", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("writes a valid proposal and only then times out", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("proposal-received", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation-repair", repair, StringComparison.Ordinal);
        Assert.Contains("proposal-applied", repair, StringComparison.Ordinal);
        Assert.Contains("narrative-draft", narrative, StringComparison.Ordinal);
        Assert.Contains("proposal-only", narrative, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inventory-content", contentAuthoring, StringComparison.Ordinal);
        Assert.Contains("authoringProposal", contentAuthoring, StringComparison.Ordinal);
        Assert.Contains("proposal-only", contentAuthoring, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skill-content", skillContent, StringComparison.Ordinal);
        Assert.Contains("localizedScalingAttribute", skillContent, StringComparison.Ordinal);
        Assert.Contains("bonusExplanation", skillContent, StringComparison.Ordinal);
        Assert.Contains("npc-content", npcContent, StringComparison.Ordinal);
        Assert.Contains("thoughtJournal", npcContent, StringComparison.Ordinal);
        Assert.Contains("detailSurfaces", npcContent, StringComparison.Ordinal);
        Assert.Contains("afterlifeContract", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("afterlifeProposal", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("Afterlife_Contract_Matrix.md", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("worldStateFlags", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("worldEventsLog", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("Mortal NPC relationships", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("Mortal combat HP/status", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("Mortal factions or map files", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("realmGate", afterlifeContract, StringComparison.Ordinal);
        Assert.Contains("guardian-abode-content", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("guardianAbodeRequest", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("guardianAbodeProposal", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("system_guardian_attraction.json", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("afterlife_return_guard.json", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("guardian_projects.json", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("abode_power_journal.json", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("chaos_sea_guardian_politics.json", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("Mortal NPCs", guardianAbodeContent, StringComparison.Ordinal);
        Assert.Contains("soul-content", soulContent, StringComparison.Ordinal);
        Assert.Contains("soulContentRequest", soulContent, StringComparison.Ordinal);
        Assert.Contains("soulContentProposal", soulContent, StringComparison.Ordinal);
        Assert.Contains("soulName", soulContent, StringComparison.Ordinal);
        Assert.Contains("soulFormDescription", soulContent, StringComparison.Ordinal);
        Assert.Contains("Chaos Sea", soulContent, StringComparison.Ordinal);
        Assert.Contains("game_state/meta/soul_state.json", soulContent, StringComparison.Ordinal);
        Assert.Contains("Mortal inventory", soulContent, StringComparison.Ordinal);
    }

    [Fact]
    public void GmWorkerBridgeDocs_DocumentWorkerRuntimeEnvironmentProtocol()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var narrative = ReadRepoFile("Examples/E_CLI_GM_Worker_Narrative_Draft.txt");
        var contentAuthoring = ReadRepoFile("Examples/E_CLI_GM_Worker_Content_Authoring.txt");
        var skillContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Skill_Content.txt");
        var npcContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Npc_Content.txt");
        var afterlifeContract = ReadRepoFile("Examples/E_CLI_GM_Worker_Afterlife_Contract.txt");
        var guardianAbodeContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt");
        var soulContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Soul_Content.txt");

        foreach (var source in new[] { guide, contract, repair, narrative, contentAuthoring, skillContent, npcContent, afterlifeContract, guardianAbodeContent, soulContent })
        {
            Assert.Contains("BOE_WORKER_TASK_PATH", source, StringComparison.Ordinal);
            Assert.Contains("BOE_WORKER_PROPOSAL_PATH", source, StringComparison.Ordinal);
            Assert.Contains("BOE_WORKER_SESSION_PATH", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ValidationRepairDocs_DocumentExclusiveHandoffExactHashesAndAfterlifeRepairShape()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var afterlifeMatrix = ReadRepoFile("OtherGuides/Afterlife_Contract_Matrix.md");

        foreach (var source in new[] { guide, contract, repair })
        {
            Assert.Contains("64-character SHA-256", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("afterlifeProposal", source, StringComparison.Ordinal);
            Assert.Contains("optional", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("validation-repair", source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("before the legacy", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ready signal publication fails", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("worker_apply_gate_accepted", repair, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256-weather-before", repair, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256-weather-after", repair, StringComparison.Ordinal);
        Assert.Contains("guardian_thought_journal.json", afterlifeMatrix, StringComparison.Ordinal);
        Assert.Contains("append-only", afterlifeMatrix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationRepairDocs_DocumentExplicitChangeKindsAndPinnedAfterlifeRealmAuthority()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var afterlifeMatrix = ReadRepoFile("OtherGuides/Afterlife_Contract_Matrix.md");
        var runner = ReadRepoFile("BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1");
        var mainGuide = ReadRepoFile("TaskGuides/CLI_Step_Main.txt");
        var mainExample = ReadRepoFile("Examples/E_CLI_Step_Main.txt");

        foreach (var source in new[] { guide, contract, repair, runner })
        {
            Assert.Contains("changeKind is mandatory", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("`add`, `replace`, or `delete`", source, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var source in new[] { guide, contract, repair, afterlifeMatrix, runner })
        {
            Assert.Contains("hash-pinned read-only realm authority", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("game_state/meta/soul_state.json", source, StringComparison.Ordinal);
            Assert.Contains("must not appear in `changedFiles`", source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("finite JSON number", mainGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finite JSON number", mainExample, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationRepairDocs_DocumentDetachedRuntimeAndAtomicAuthorityLease()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var afterlifeMatrix = ReadRepoFile("OtherGuides/Afterlife_Contract_Matrix.md");
        var runner = ReadRepoFile("BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1");
        var mainGmPrompt = ReadRepoFile("BookOfEternityClient/Launcher/CLI_Launch_Script.md");
        var mainGmPromptGenerator = ReadRepoFile("BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1");
        var gitIgnore = ReadRepoFile(".gitignore");
        var bridgePool = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs");
        var proposalStore = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerProposalStore.cs");
        var applyGate = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs");
        var contractValidator = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerContractValidator.cs");
        var executionWorkspace = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerExecutionWorkspace.cs");
        var fileSystemManager = ReadRepoFile("BookOfEternityClient/Core/FileSystemManager.cs");
        var repairDelegator = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs");
        var saveLoadService = ReadRepoFile("BookOfEternityClient/Services/SaveLoadService.cs");

        foreach (var source in new[] { guide, contract, repair, runner })
        {
            Assert.Contains("detached execution snapshot", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonical write lease", source, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var source in new[] { guide, contract, repair })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(".worker_runtime", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("only pinned task context", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("only the validated proposal and its declared contentRef", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("read-only context", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("before importing any worker artifact", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("task and proposal identifiers are immutable", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timeout remains authoritative", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("case-insensitive canonical path identity", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cleanup failure is an audit diagnostic", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("publishes the complete proposal bundle through one create-only atomic directory rename", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("`maxConcurrentTasks`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("worker slot", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("globally unique per dispatch", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("save and load operations use the same canonical write lease", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("external durable journal under `.boe_runtime/load-transactions`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("kills and awaits the complete worker process tree", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reference-counted gates that retire when idle", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var source in new[] { guide, contract, repair, afterlifeMatrix })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("every `game_state/meta/`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("exact wildcard-free afterlife paths", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("`lore/current_world/**`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("`game_state/core/player_status.json`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("are Mortal", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("under `game_state/meta/`", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("GmWorkerExecutionWorkspace.CreateAsync", bridgePool, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateWorkerStartInfo(profile, _fs.GameSessionPath)", bridgePool, StringComparison.Ordinal);
        Assert.Contains("AcquireCanonicalWriteLeaseAsync", applyGate, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData(content)", bridgePool, StringComparison.Ordinal);
        Assert.Contains("Worker task id already exists and cannot overwrite", bridgePool, StringComparison.Ordinal);
        Assert.Contains("Worker proposal id already exists and cannot be overwritten", proposalStore, StringComparison.Ordinal);
        Assert.Contains("TimedOut = true", bridgePool, StringComparison.Ordinal);
        Assert.Contains("workspace-cleanup-failed", bridgePool, StringComparison.Ordinal);
        Assert.Contains("WorkerConcurrencyGates", bridgePool, StringComparison.Ordinal);
        Assert.Contains("TryReserveTaskAsync", bridgePool, StringComparison.Ordinal);
        Assert.Contains("ReferenceCount", bridgePool, StringComparison.Ordinal);
        Assert.Contains("TryRemove", bridgePool, StringComparison.Ordinal);
        Assert.Contains("Kill(entireProcessTree: true)", bridgePool, StringComparison.Ordinal);
        Assert.Contains("await waitTask", bridgePool, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReserveProposalIdAsync", bridgePool, StringComparison.Ordinal);
        Assert.DoesNotContain("ProposalClaimRoot", bridgePool, StringComparison.Ordinal);
        Assert.Contains("PublishBundleAsync", proposalStore, StringComparison.Ordinal);
        Assert.Contains("Directory.Move(stagingBundleRoot, finalBundleRoot)", proposalStore, StringComparison.Ordinal);
        Assert.Contains("current game session generation", proposalStore, StringComparison.Ordinal);
        Assert.DoesNotContain("gm_worker_apply.lock", applyGate, StringComparison.Ordinal);
        Assert.Contains("CanonicalPathComparer", contractValidator, StringComparison.Ordinal);
        Assert.Contains("StringComparer.OrdinalIgnoreCase", contractValidator, StringComparison.Ordinal);
        Assert.Contains("must not contain wildcard patterns", contractValidator, StringComparison.Ordinal);
        Assert.Contains("outside canonical afterlife state", contractValidator, StringComparison.Ordinal);
        Assert.Contains("identity collision repair is main GM only", contractValidator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Guid.NewGuid():N", repairDelegator, StringComparison.Ordinal);
        Assert.True(
            saveLoadService.Split("AcquireCanonicalWriteLeaseAsync", StringSplitOptions.None).Length - 1 >= 2,
            "Expected save and load to acquire the canonical write lease.");
        Assert.Contains("BeginLoadTransaction", saveLoadService, StringComparison.Ordinal);
        Assert.Contains("RecoverInterruptedLoadTransaction", saveLoadService, StringComparison.Ordinal);
        Assert.Contains(".boe_runtime", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("load-transactions", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("RecoverInterruptedLoadTransaction", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("SearchOption.TopDirectoryOnly", executionWorkspace, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", executionWorkspace, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(path, recursive: false)", executionWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchOption.AllDirectories", executionWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete(workspaceRoot, recursive: true)", executionWorkspace, StringComparison.Ordinal);
        foreach (var methodName in new[]
                 {
                     "CreateBackupAsync",
                     "RestoreBackupAsync",
                     "ClearGameStateAsync",
                     "ClearCurrentWorldLoreAsync"
                 })
        {
            var methodOffset = fileSystemManager.IndexOf(methodName, StringComparison.Ordinal);
            Assert.True(methodOffset >= 0, $"Expected FileSystemManager method {methodName}.");
            var leaseOffset = fileSystemManager.IndexOf(
                "AcquireCanonicalWriteLeaseAsync",
                methodOffset,
                StringComparison.Ordinal);
            Assert.True(
                leaseOffset >= 0 && leaseOffset - methodOffset < 300,
                $"Expected {methodName} to acquire the canonical write lease before mutating state.");
        }
        Assert.Contains("detached execution snapshot", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never copy direct snapshot edits", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("detached execution snapshot", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never copy direct snapshot edits", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**/.worker_runtime/", gitIgnore, StringComparison.Ordinal);
        Assert.Contains("**/.boe_runtime/", gitIgnore, StringComparison.Ordinal);
    }

    [Fact]
    public void GmWorkerBridgeDocs_DocumentCliRunnerEntrypoint()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var narrative = ReadRepoFile("Examples/E_CLI_GM_Worker_Narrative_Draft.txt");
        var contentAuthoring = ReadRepoFile("Examples/E_CLI_GM_Worker_Content_Authoring.txt");
        var skillContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Skill_Content.txt");
        var npcContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Npc_Content.txt");
        var afterlifeContract = ReadRepoFile("Examples/E_CLI_GM_Worker_Afterlife_Contract.txt");
        var guardianAbodeContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt");
        var soulContent = ReadRepoFile("Examples/E_CLI_GM_Worker_Soul_Content.txt");
        var runner = ReadRepoFile("BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1");

        foreach (var source in new[] { guide, contract, repair, narrative, contentAuthoring, skillContent, npcContent, afterlifeContract, guardianAbodeContent, soulContent })
        {
            Assert.Contains("gm_worker_cli_runner.ps1", source, StringComparison.Ordinal);
            Assert.Contains("-AgentCommand", source, StringComparison.Ordinal);
        }

        var normalizedGuide = guide
            .Replace("\\\\\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal);
        Assert.Contains("codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("worker-proposal-v1", runner, StringComparison.Ordinal);
        Assert.Contains("Required worker-proposal-v1 JSON shape", runner, StringComparison.Ordinal);
        Assert.Contains("Do not omit summary, status, changedFiles, findings, selfCheck, or createdAtUtc.", runner, StringComparison.Ordinal);
        Assert.Contains("Do not edit canonical game_session files directly.", runner, StringComparison.Ordinal);
        Assert.Contains("authoringProposal", runner, StringComparison.Ordinal);
        Assert.Contains("afterlifeProposal", runner, StringComparison.Ordinal);
        Assert.Contains("afterlifeContract", runner, StringComparison.Ordinal);
        Assert.Contains("guardianAbodeProposal", runner, StringComparison.Ordinal);
        Assert.Contains("guardianAbodeRequest", runner, StringComparison.Ordinal);
        Assert.Contains("soulContentProposal", runner, StringComparison.Ordinal);
        Assert.Contains("soulContentRequest", runner, StringComparison.Ordinal);
        Assert.Contains("taskType is not validation-repair", runner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("For validation-repair tasks", runner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterSha256", runner, StringComparison.Ordinal);
        Assert.Contains(
            "Delete changes require path, changeKind, beforeSha256, afterSha256 exactly 'missing', and no contentRef.",
            runner,
            StringComparison.Ordinal);
        const string terminalStatusRule =
            "Only status completed proposals can enter the apply gate. Status failed, timed-out, or rejected must use changedFiles: [].";
        foreach (var source in new[] { guide, contract, repair, runner })
            Assert.Contains(terminalStatusRule, source, StringComparison.Ordinal);
        const string mandatoryStatusRule =
            "Status is mandatory; omission is invalid and must never default to completed.";
        foreach (var source in new[] { guide, contract, repair, runner })
            Assert.Contains(mandatoryStatusRule, source, StringComparison.Ordinal);
        const string auditIdRule =
            "worker_audit_<UTC yyyyMMddHHmmssfff>_<32 lowercase hex GUID>";
        foreach (var source in new[] { guide, contract, repair })
            Assert.Contains(auditIdRule, source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "If the task contains afterlifeContract, keep changedFiles empty",
            runner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherAndDaemon_DefaultConfigExposeDisabledWorkerProfileTemplates()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        foreach (var source in new[] { daemon, launcher })
        {
            Assert.Contains("codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox", source, StringComparison.Ordinal);
            Assert.Contains("codex exec -m gpt-5.6-terra -c model_reasoning_effort", source, StringComparison.Ordinal);
            Assert.Contains("Convert-RetiredCodexLaunchDefaults", source, StringComparison.Ordinal);
            Assert.Contains("$retiredModel = 'gpt-5' + '.5'", source, StringComparison.Ordinal);
            Assert.Contains("$Config.GmCliLaunchCommand -ceq $retiredMainQuoted", source, StringComparison.Ordinal);
            Assert.Contains("GmWorkerBridgeProfiles", source, StringComparison.Ordinal);
            Assert.Contains("validation_repair_codex", source, StringComparison.Ordinal);
            Assert.Contains("narrative_draft_codex", source, StringComparison.Ordinal);
            Assert.Contains("analysis_codex", source, StringComparison.Ordinal);
            Assert.Contains("guardian_abode_content_codex", source, StringComparison.Ordinal);
            Assert.Contains("inventory_content_codex", source, StringComparison.Ordinal);
            Assert.Contains("skill_content_codex", source, StringComparison.Ordinal);
            Assert.Contains("npc_content_codex", source, StringComparison.Ordinal);
            Assert.Contains("soul_content_codex", source, StringComparison.Ordinal);
            Assert.Contains("gm_worker_cli_runner.ps1", source, StringComparison.Ordinal);
            Assert.Contains("enabled = $false", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ActiveWorkerGuidance_DoesNotAdvertiseDeprecatedGeminiCli()
    {
        var activeGuidance = new[]
        {
            "OtherGuides/GM_Worker_Bridges.md",
            "Examples/E_CLI_GM_Worker_Narrative_Draft.txt",
            "Examples/E_CLI_GM_Worker_Content_Authoring.txt",
            "Examples/E_CLI_GM_Worker_Skill_Content.txt",
            "Examples/E_CLI_GM_Worker_Npc_Content.txt",
            "Examples/E_CLI_GM_Worker_Afterlife_Contract.txt",
            "Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt",
            "Examples/E_CLI_GM_Worker_Soul_Content.txt",
            "BookOfEternityClient/Launcher/CLI_Daemon_Quickstart.md",
            "BookOfEternityClient/Launcher/CLI_Daemon_Window_Help.md",
            "BookOfEternityClient/Launcher/GM_Daemon_ConPTY_Proposal.md",
            "BookOfEternityClient/Launcher/bookofeternity.ps1",
            "BookOfEternityClient/game_master_daemon.ps1",
            "BookOfEternityClient/Services/GmWorkers/GmWorkerBridgeProfileTemplates.cs",
            "specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md",
            "specs/1151-gm-worker-profile-templates/contracts/gm-worker-profile-templates-contract.md",
            "specs/1151-gm-worker-profile-templates/spec.md"
        };

        foreach (var relativePath in activeGuidance)
        {
            var source = ReadRepoFile(relativePath);
            Assert.DoesNotContain("gemini", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("narrative_draft_gemini", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GmWorkerBridgeGuide_DocumentsDisabledProfileTemplates()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");

        Assert.Contains("disabled worker profile templates", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"enabled\": false", guide, StringComparison.Ordinal);
        Assert.Contains("analysis_codex", guide, StringComparison.Ordinal);
        Assert.Contains("guardian_abode_content_codex", guide, StringComparison.Ordinal);
        Assert.Contains("inventory_content_codex", guide, StringComparison.Ordinal);
        Assert.Contains("skill_content_codex", guide, StringComparison.Ordinal);
        Assert.Contains("npc_content_codex", guide, StringComparison.Ordinal);
        Assert.Contains("soul_content_codex", guide, StringComparison.Ordinal);
        Assert.Contains("enable one template explicitly", guide, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, Path.Combine(relativePath.Split('/')));
        return File.ReadAllText(path);
    }
}
