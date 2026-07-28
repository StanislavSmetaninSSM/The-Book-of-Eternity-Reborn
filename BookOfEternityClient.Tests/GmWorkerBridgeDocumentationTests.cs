using System.Text.Json;
using System.Text.RegularExpressions;
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
        Assert.Contains(
            "A proposal becomes applyable only after confirmed zero exit and confirmed process-tree termination.",
            guide,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "preserves the proposal and records it as",
            guide,
            StringComparison.OrdinalIgnoreCase);
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

        foreach (var source in new[] { guide, contract })
        {
            Assert.Contains(
                "must not be equal to or nested under the canonical `game_session`",
                source,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reparse-point alias", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WorkerExecutionDocs_RequireConfirmedSuccessfulProcessBeforeProposalImport()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        const string successRule =
            "A proposal becomes applyable only after confirmed zero exit and confirmed process-tree termination.";

        foreach (var source in new[] { guide, contract, repair })
        {
            Assert.Contains(successRule, source, StringComparison.Ordinal);
            Assert.Contains("diagnostic-only", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("must not be imported", source, StringComparison.OrdinalIgnoreCase);
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
    public void GmWorkerTaskExamples_BindEveryTaskPacketToSessionGeneration()
    {
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var normalizedGuide = string.Join(
            ' ',
            guide.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        foreach (var taskId in new[]
                 {
                     "worker_task_20260620_0001",
                     "worker_task_20260620_0002",
                     "worker_task_afterlife_contract_0001",
                     "worker_task_guardian_abode_content_0001"
                 })
        {
            Assert.Contains(
                $"\"taskId\": \"{taskId}\",\n  \"sessionGeneration\": \"11111111111111111111111111111111\"",
                contract,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "\"taskId\": \"worker_task_20260620_0001\",\n  \"sessionGeneration\": \"11111111111111111111111111111111\"",
            repair,
            StringComparison.Ordinal);
        Assert.Contains("`sessionGeneration` is mandatory", normalizedGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("32 lowercase hexadecimal characters", normalizedGuide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GmWorkerTaskExamples_UseCanonicalSessionGenerationInEveryTaskPacket()
    {
        var examplesDirectory = Path.Combine(TestRepoPaths.RepoRoot, "Examples");
        var examplePaths = Directory.GetFiles(
            examplesDirectory,
            "E_CLI_GM_Worker_*.txt",
            SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(examplePaths);

        var taskPacketExampleCount = 0;
        foreach (var examplePath in examplePaths)
        {
            var source = File.ReadAllText(examplePath);
            if (!source.Contains("Task packet", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            taskPacketExampleCount++;
            var taskPackets = ExtractTaskPackets(source);
            var taskPacket = Assert.Single(taskPackets);
            var exampleName = Path.GetFileName(examplePath);

            Assert.True(
                taskPacket.TryGetProperty("sessionGeneration", out var generationElement),
                $"{exampleName} task packet must declare sessionGeneration.");

            var generation = generationElement.GetString();
            Assert.True(
                Guid.TryParseExact(generation, "N", out var parsedGeneration) &&
                generation == parsedGeneration.ToString("N"),
                $"{exampleName} sessionGeneration must be 32 lowercase hexadecimal characters in GUID N format.");
        }

        Assert.Equal(7, taskPacketExampleCount);
    }

    [Fact]
    public void ValidationRepairDocs_DescribeActualAuthenticatedProcessHostHandshake()
    {
        var sources = new[]
        {
            ReadRepoFile("OtherGuides/GM_Worker_Bridges.md"),
            ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md"),
            ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt")
        };

        foreach (var source in sources)
        {
            var normalized = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            Assert.Contains(
                "parent creates private current-user named control/status pipe servers",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "only the two endpoint names and the per-launch nonce",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "authenticates both connected pipe clients as the exact hidden-host PID",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "only after authentication sends a typed `Launch` frame",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "executable, arguments, working directory, and environment",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "configured worker payload never appears in the hidden-host command line",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "configured worker receives neither channel nor any pipe handle",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "unknown, duplicate, or missing frame fields are rejected",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "typed `Ready`",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "typed `Release`",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "typed `Completed`",
                normalized,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "typed `OutputDrained`",
                normalized,
                StringComparison.OrdinalIgnoreCase);
        }
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
    public void ValidationRepairDocs_DocumentPrivateRuntimeAndRecoverableAuthorityTransactions()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var manifest = ReadRepoFile("Examples/example_validation_manifest.json");
        var afterlifeMatrix = ReadRepoFile("OtherGuides/Afterlife_Contract_Matrix.md");
        var featureSpec = ReadRepoFile("specs/1500-complete-actor-materialization/spec.md");
        var featurePlan = ReadRepoFile("specs/1500-complete-actor-materialization/plan.md");
        var featureResearch = ReadRepoFile("specs/1500-complete-actor-materialization/research.md");
        var featureDataModel = ReadRepoFile("specs/1500-complete-actor-materialization/data-model.md");
        var featureQuickstart = ReadRepoFile("specs/1500-complete-actor-materialization/quickstart.md");
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
        var stateManager = ReadRepoFile("BookOfEternityClient/Core/StateManager.cs");
        var processHost = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerProcessHost.cs");
        var processTree = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerProcessTree.cs");
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
            Assert.Contains("durable session generation lives under `.boe_runtime/session-generation/current.json`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rotates on load and New Game", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("recover an interrupted load transaction immediately after acquiring the canonical write lease", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("one lease-scoped read/modify/write", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("configured worker command cannot start until process-tree ownership is attached", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("private current-user named control/status pipe servers", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("parent-side client PID authentication", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("per-launch nonce", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no worker-accessible marker files", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("complete typed frames", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("non-null exit code", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("before output pipes are drained", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("explicit `OutputDrained` acknowledgement", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Windows Job Object is the supported complete descendant boundary", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fail closed before worker release", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timeout and cancellation remain authoritative", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cleanup uncertainty quarantines the worker slot", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unattached-host cleanup is bounded", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("external durable journal under `.boe_runtime/worker-apply-transactions`", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("every canonical writer recovers an interrupted worker apply transaction", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("committed journal cleanup cannot roll back accepted bytes", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("one atomic transition", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ValidationService", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("applyable proposal", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        var normalizedAfterlifeMatrix = string.Join(
            ' ',
            afterlifeMatrix.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("private current-user named control/status pipe servers", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parent-side client PID authentication", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit `OutputDrained` acknowledgement", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Job Object is the supported complete descendant boundary", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external durable journal under `.boe_runtime/worker-apply-transactions`", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("changes no Chaos Sea or Shining Abode pending/control file", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one atomic transition", normalizedAfterlifeMatrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ValidationService", normalizedAfterlifeMatrix, StringComparison.Ordinal);

        foreach (var source in new[]
                 {
                     featureSpec,
                     featurePlan,
                     featureResearch,
                     featureDataModel,
                     featureQuickstart
                 })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("worker-apply-transactions", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("private current-user named control/status", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("parent-side client PID authentication", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OutputDrained", normalizedSource, StringComparison.Ordinal);
            Assert.DoesNotContain("PGID anchor", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Unix process-group fallback", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("private current-user named control/status pipe servers", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parent-side client PID authentication", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OutputDrained", manifest, StringComparison.Ordinal);
        Assert.Contains("worker-apply recovery journals", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail-closed unsupported platforms", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unix process-group", manifest, StringComparison.OrdinalIgnoreCase);

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
        Assert.Contains("MoveRuntimeDirectoryIntoCanonicalSessionAsync", proposalStore, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Move(stagingBundleRoot, finalBundleRoot)", proposalStore, StringComparison.Ordinal);
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
        Assert.Contains("SessionGenerationPath", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("RotateSessionGeneration", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync()", stateManager, StringComparison.Ordinal);
        Assert.Contains("GmWorkerProcessHostLaunch.Create", bridgePool, StringComparison.Ordinal);
        Assert.Contains("WaitUntilReadyAsync", bridgePool, StringComparison.Ordinal);
        Assert.Contains("processHostLaunch.ReleaseAsync", bridgePool, StringComparison.Ordinal);
        Assert.Contains("WaitForWorkerCompletionAsync", bridgePool, StringComparison.Ordinal);
        Assert.Contains("workerSlot.Quarantine()", bridgePool, StringComparison.Ordinal);
        Assert.Contains("process-tree-cleanup-unconfirmed", bridgePool, StringComparison.Ordinal);
        Assert.Contains("StopUnattachedProcessTreeAsync", bridgePool, StringComparison.Ordinal);
        Assert.Contains("--gm-worker-process-host", processHost, StringComparison.Ordinal);
        Assert.Contains("NamedPipeServerStream", processHost, StringComparison.Ordinal);
        Assert.Contains("NamedPipeClientStream", processHost, StringComparison.Ordinal);
        Assert.Contains("PipeOptions.CurrentUserOnly", processHost, StringComparison.Ordinal);
        Assert.Contains("GetNamedPipeClientProcessId", processHost, StringComparison.Ordinal);
        Assert.Contains("CryptographicOperations.FixedTimeEquals", processHost, StringComparison.Ordinal);
        Assert.Contains("HandleInheritability.None", processHost, StringComparison.Ordinal);
        Assert.Contains("GmWorkerProcessHostStatusKind.OutputDrained", processHost, StringComparison.Ordinal);
        Assert.DoesNotContain("AnonymousPipeServerStream", processHost, StringComparison.Ordinal);
        Assert.Contains("completed status requires exitCode", processHost, StringComparison.Ordinal);
        Assert.DoesNotContain(".ready", processHost, StringComparison.Ordinal);
        Assert.DoesNotContain(".release", processHost, StringComparison.Ordinal);
        Assert.DoesNotContain(".completed", processHost, StringComparison.Ordinal);
        Assert.Contains("JobObjectLimitKillOnJobClose", processTree, StringComparison.Ordinal);
        Assert.Contains("PlatformNotSupportedException", processTree, StringComparison.Ordinal);
        Assert.DoesNotContain("UnixProcessGroupController", processTree, StringComparison.Ordinal);
        Assert.Contains("ProcessTreeTerminationConfirmation.WaitAsync", processTree, StringComparison.Ordinal);
        Assert.Contains("throw new TimeoutException", bridgePool, StringComparison.Ordinal);
        Assert.Contains("worker-apply-transactions", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("BeginWorkerApplyTransactionAsync", applyGate, StringComparison.Ordinal);
        Assert.Contains("CommitWorkerApplyTransaction", applyGate, StringComparison.Ordinal);
        Assert.Contains("RecoverInterruptedWorkerApplyTransactionAsync", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("committed journal makes cleanup retryable without revoking accepted canonical bytes", fileSystemManager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SearchOption.TopDirectoryOnly", executionWorkspace, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", executionWorkspace, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(path, recursive: false)", executionWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchOption.AllDirectories", executionWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete(workspaceRoot, recursive: true)", executionWorkspace, StringComparison.Ordinal);
        foreach (var methodName in new[]
                 {
                     "CreateBackupAsync",
                     "RestoreBackupAsync",
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
        var clearWrapperOffset = fileSystemManager.IndexOf(
            "public async Task ClearGameStateAsync()",
            StringComparison.Ordinal);
        var lifecycleLeaseOffset = fileSystemManager.IndexOf(
            "AcquireSessionLifecycleLeaseAsync",
            clearWrapperOffset,
            StringComparison.Ordinal);
        var clearOverloadOffset = fileSystemManager.IndexOf(
            "internal async Task<string> ClearGameStateAsync(SessionLifecycleLease lifecycleLease)",
            lifecycleLeaseOffset,
            StringComparison.Ordinal);
        var lifecycleValidationOffset = fileSystemManager.IndexOf(
            "EnsureValidSessionLifecycleLease(lifecycleLease)",
            clearOverloadOffset,
            StringComparison.Ordinal);
        var replacementLeaseOffset = fileSystemManager.IndexOf(
            "AcquireSessionReplacementWriteLeaseAsync(lifecycleLease)",
            lifecycleValidationOffset,
            StringComparison.Ordinal);
        Assert.True(
            clearWrapperOffset >= 0 &&
            lifecycleLeaseOffset > clearWrapperOffset &&
            clearOverloadOffset > lifecycleLeaseOffset &&
            lifecycleValidationOffset > clearOverloadOffset &&
            replacementLeaseOffset > lifecycleValidationOffset,
            "ClearGameStateAsync must acquire the lifecycle lease first and the replacement canonical lease only inside the validated overload.");
        Assert.Contains(
            "EnsureValidSessionReplacementLease(writeLease)",
            fileSystemManager,
            StringComparison.Ordinal);
        Assert.Contains("detached execution snapshot", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never copy direct snapshot edits", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("detached execution snapshot", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never copy direct snapshot edits", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private current-user named control/status", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parent-side client PID authentication", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OutputDrained", mainGmPrompt, StringComparison.Ordinal);
        Assert.Contains("worker-apply-transactions", mainGmPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("private current-user named control/status", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parent-side client PID authentication", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OutputDrained", mainGmPromptGenerator, StringComparison.Ordinal);
        Assert.Contains("worker-apply-transactions", mainGmPromptGenerator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**/.worker_runtime/", gitIgnore, StringComparison.Ordinal);
        Assert.Contains("**/.boe_runtime/", gitIgnore, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationRepairDocs_DocumentReservedAuthorityAndSessionReplacementAbort()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var afterlifeMatrix = ReadRepoFile("OtherGuides/Afterlife_Contract_Matrix.md");
        var featureSpec = ReadRepoFile("specs/1500-complete-actor-materialization/spec.md");
        var featurePlan = ReadRepoFile("specs/1500-complete-actor-materialization/plan.md");
        var featureResearch = ReadRepoFile("specs/1500-complete-actor-materialization/research.md");
        var featureDataModel = ReadRepoFile("specs/1500-complete-actor-materialization/data-model.md");
        var featureQuickstart = ReadRepoFile("specs/1500-complete-actor-materialization/quickstart.md");
        var applyGate = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs");
        var bridgePool = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs");
        var delegator = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs");
        var gameEngine = ReadRepoFile("BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs");
        var fileSystemManager = ReadRepoFile("BookOfEternityClient/Core/FileSystemManager.cs");
        var saveLoadService = ReadRepoFile("BookOfEternityClient/Services/SaveLoadService.cs");

        foreach (var source in new[] { guide, contract, repair, afterlifeMatrix })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("exact durable reserved task is the sole apply authority", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("`SessionReplaced`", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("aborts the old repair", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no legacy fallback or rollback may write into the replacement session", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("generation-bound atomic append", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("latest validation-repair task is ephemeral and excluded from saves", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lowercase canonical GUID text", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("proposal id `inbox` is reserved", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var source in new[]
                 {
                     featureSpec,
                     featurePlan,
                     featureResearch,
                     featureDataModel,
                     featureQuickstart
                 })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("exact durable reserved task", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SessionReplaced", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("generation-bound atomic append", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("latest validation-repair task", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("proposal id `inbox` is reserved", normalizedSource, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("ApplyReservedAsync", applyGate, StringComparison.Ordinal);
        Assert.Contains("Canonical worker task reservation is malformed", applyGate, StringComparison.Ordinal);
        Assert.Contains("GmWorkerJson.Deserialize<WorkerTaskPacket>", applyGate, StringComparison.Ordinal);
        Assert.Contains("GmWorkerJson.Deserialize<WorkerTaskPacket>", bridgePool, StringComparison.Ordinal);
        Assert.Contains("ApplyReservedAsync", delegator, StringComparison.Ordinal);
        Assert.DoesNotContain("run.BoundTask ?? task", delegator, StringComparison.Ordinal);
        Assert.Contains("run.BoundTask == null", delegator, StringComparison.Ordinal);
        Assert.Contains(
            "var workerFileSystem = _validator.CanonicalFileSystem;",
            gameEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "new GmWorkerAuditLog(workerFileSystem)",
            gameEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "new GmWorkerBridgePool(",
            gameEngine,
            StringComparison.Ordinal);
        Assert.Contains(
            "new GmWorkerProposalStore(workerFileSystem)",
            gameEngine,
            StringComparison.Ordinal);
        Assert.DoesNotContain("new GmWorkerAuditLog(_fs)", gameEngine, StringComparison.Ordinal);
        Assert.DoesNotContain("new GmWorkerBridgePool(_fs", gameEngine, StringComparison.Ordinal);
        Assert.Contains("GmWorkerValidationRepairOutcome.SessionReplaced", delegator, StringComparison.Ordinal);
        Assert.Contains("GmWorkerSessionReplacedException", gameEngine, StringComparison.Ordinal);
        Assert.Contains("AppendFileAtomicIfCurrentSessionAsync", gameEngine, StringComparison.Ordinal);
        Assert.Contains("AppendFileAtomicIfCurrentSessionAsync", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("parsedGeneration.ToString(\"N\")", fileSystemManager, StringComparison.Ordinal);
        Assert.Contains("LatestValidationRepairTaskPath", saveLoadService, StringComparison.Ordinal);

        var cleanupMethod = fileSystemManager.IndexOf(
            "private void CleanupWorkerApplyTransaction",
            StringComparison.Ordinal);
        Assert.True(cleanupMethod >= 0, "Expected worker apply cleanup implementation.");
        var transactionRootDelete = fileSystemManager.IndexOf(
            "DeleteDirectory(transactionRoot, recursive: true)",
            cleanupMethod,
            StringComparison.Ordinal);
        var journalDelete = fileSystemManager.IndexOf(
            "DeleteFile(ActiveWorkerApplyTransactionJournalPath)",
            cleanupMethod,
            StringComparison.Ordinal);
        Assert.True(
            transactionRootDelete >= 0 && journalDelete > transactionRootDelete,
            "Committed apply cleanup must delete the transaction directory before its active journal so failure remains retryable.");
    }

    [Fact]
    public void GmWorkerBridgeDocs_DocumentExternalRuntimeAndArtifactBudgets()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var bridgePool = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs");
        var executionWorkspace = ReadRepoFile("BookOfEternityClient/Services/GmWorkers/GmWorkerExecutionWorkspace.cs");

        foreach (var source in new[] { guide, contract })
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("BOE_WORKER_RUNTIME_BASE_PATH", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("outside the replaceable game session", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1 MiB", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("4 MiB", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("16 MiB", normalizedSource, StringComparison.Ordinal);
            Assert.Contains("65,536 characters", normalizedSource, StringComparison.Ordinal);
        }

        Assert.Contains("MaxProposalBytes = 1024 * 1024", bridgePool, StringComparison.Ordinal);
        Assert.Contains("MaxContentRefBytes = 4 * 1024 * 1024", bridgePool, StringComparison.Ordinal);
        Assert.Contains("MaxImportedContentBytes = 16 * 1024 * 1024", bridgePool, StringComparison.Ordinal);
        Assert.Contains("MaxCapturedProcessOutputCharacters = 64 * 1024", bridgePool, StringComparison.Ordinal);
        Assert.Contains("ResolveDefaultRuntimeBase", executionWorkspace, StringComparison.Ordinal);
        Assert.Contains("ResolveConfiguredRuntimeBase", executionWorkspace, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorMaterializationSpec_DocumentsImmutableSessionOperationFence()
    {
        var sources = new[]
        {
            ReadRepoFile("specs/1500-complete-actor-materialization/spec.md"),
            ReadRepoFile("specs/1500-complete-actor-materialization/plan.md"),
            ReadRepoFile("specs/1500-complete-actor-materialization/research.md"),
            ReadRepoFile("specs/1500-complete-actor-materialization/data-model.md"),
            ReadRepoFile("specs/1500-complete-actor-materialization/quickstart.md")
        };

        foreach (var source in sources)
        {
            var normalizedSource = string.Join(
                ' ',
                source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("immutable session operation", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("after recovery and under the canonical write lease", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("must not hold the lifecycle lease while waiting for the GM", normalizedSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("typed `SessionReplaced`", normalizedSource, StringComparison.Ordinal);
        }
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
    public void ValidationRepairLoop_AbortsEveryDispatchThatBelongsToReplacedSession()
    {
        var gameEngine = ReadRepoFile(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs");

        Assert.Contains(
            "ThrowIfValidationRepairDispatchSessionReplaced(dispatch);",
            gameEngine,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            gameEngine.Split(
                "ThrowIfValidationRepairDispatchSessionReplaced(rejectedReadyRepair.Dispatch);",
                StringSplitOptions.None).Length - 1);
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
    public void LiveTurnQuickstart_DocumentsGenerationBoundPreparationTransaction()
    {
        var guide = ReadRepoFile(
            "BookOfEternityClient/Launcher/CLI_Daemon_Quickstart.md");

        Assert.Contains(
            "одной generation-bound транзакцией",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "SessionReplaced",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "собирайте и не очищайте эти файлы вручную",
            guide,
            StringComparison.Ordinal);
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

    private static IReadOnlyList<JsonElement> ExtractTaskPackets(string source)
    {
        var taskPackets = new List<JsonElement>();
        var fencedJsonBlocks = Regex.Matches(
            source,
            @"```json\s*(\{.*?\})\s*```",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        foreach (Match match in fencedJsonBlocks)
        {
            using var document = JsonDocument.Parse(match.Groups[1].Value);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("taskId", out _) &&
                root.TryGetProperty("workerId", out _) &&
                root.TryGetProperty("role", out _) &&
                root.TryGetProperty("taskType", out _))
            {
                taskPackets.Add(root.Clone());
            }
        }

        return taskPackets;
    }
}
