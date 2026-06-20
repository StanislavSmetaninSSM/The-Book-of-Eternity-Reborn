using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeDocumentationTests
{
    [Fact]
    public void GmWorkerBridgeGuide_IsReferencedByLauncherAndExamplesManifest()
    {
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/CLI_Launch_Script.md");
        var manifest = ReadRepoFile("Examples/example_validation_manifest.json");

        Assert.Contains("OtherGuides/GM_Worker_Bridges.md", launcher, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Validation_Repair.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_GM_Worker_Narrative_Draft.txt", manifest, StringComparison.Ordinal);
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

        Assert.Contains("hidden/background", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("apply gate", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation-repair", repair, StringComparison.Ordinal);
        Assert.Contains("proposal-applied", repair, StringComparison.Ordinal);
        Assert.Contains("narrative-draft", narrative, StringComparison.Ordinal);
        Assert.Contains("proposal-only", narrative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GmWorkerBridgeDocs_DocumentWorkerRuntimeEnvironmentProtocol()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var narrative = ReadRepoFile("Examples/E_CLI_GM_Worker_Narrative_Draft.txt");

        foreach (var source in new[] { guide, contract, repair, narrative })
        {
            Assert.Contains("BOE_WORKER_TASK_PATH", source, StringComparison.Ordinal);
            Assert.Contains("BOE_WORKER_PROPOSAL_PATH", source, StringComparison.Ordinal);
            Assert.Contains("BOE_WORKER_SESSION_PATH", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GmWorkerBridgeDocs_DocumentCliRunnerEntrypoint()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");
        var contract = ReadRepoFile("specs/1113-gm-worker-bridges/contracts/gm-worker-bridge-contract.md");
        var repair = ReadRepoFile("Examples/E_CLI_GM_Worker_Validation_Repair.txt");
        var narrative = ReadRepoFile("Examples/E_CLI_GM_Worker_Narrative_Draft.txt");
        var runner = ReadRepoFile("BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1");

        foreach (var source in new[] { guide, contract, repair, narrative })
        {
            Assert.Contains("gm_worker_cli_runner.ps1", source, StringComparison.Ordinal);
            Assert.Contains("-AgentCommand", source, StringComparison.Ordinal);
        }

        Assert.Contains("codex --dangerously-bypass-approvals-and-sandbox", guide, StringComparison.Ordinal);
        Assert.Contains("worker-proposal-v1", runner, StringComparison.Ordinal);
        Assert.Contains("Do not edit canonical game_session files directly.", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherAndDaemon_DefaultConfigExposeDisabledWorkerProfileTemplates()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        foreach (var source in new[] { daemon, launcher })
        {
            Assert.Contains("GmWorkerBridgeProfiles", source, StringComparison.Ordinal);
            Assert.Contains("validation_repair_codex", source, StringComparison.Ordinal);
            Assert.Contains("narrative_draft_gemini", source, StringComparison.Ordinal);
            Assert.Contains("analysis_codex", source, StringComparison.Ordinal);
            Assert.Contains("gm_worker_cli_runner.ps1", source, StringComparison.Ordinal);
            Assert.Contains("enabled = $false", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GmWorkerBridgeGuide_DocumentsDisabledProfileTemplates()
    {
        var guide = ReadRepoFile("OtherGuides/GM_Worker_Bridges.md");

        Assert.Contains("disabled worker profile templates", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"enabled\": false", guide, StringComparison.Ordinal);
        Assert.Contains("analysis_codex", guide, StringComparison.Ordinal);
        Assert.Contains("enable one template explicitly", guide, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, Path.Combine(relativePath.Split('/')));
        return File.ReadAllText(path);
    }
}
