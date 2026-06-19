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
    public void LauncherAndDaemon_DefaultConfigExposeEmptyWorkerProfiles()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var launcher = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("GmWorkerBridgeProfiles", daemon, StringComparison.Ordinal);
        Assert.Contains("GmWorkerBridgeProfiles", launcher, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, Path.Combine(relativePath.Split('/')));
        return File.ReadAllText(path);
    }
}
