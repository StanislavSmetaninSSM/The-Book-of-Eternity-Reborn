using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifePlayerFacingSourceGuardTests
{
    [Fact]
    public void ChaosSeaHighCostActionsExposeFullContractPreviews()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var inkFeathers = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");
        var foundation = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.PlayerGuardianFoundation.cs");

        Assert.Contains("Полный контракт /incarnate", mainMenu, StringComparison.Ordinal);
        Assert.Contains("game_state/control/incarnation_trigger.json", mainMenu, StringComparison.Ordinal);
        Assert.Contains("ConfirmIncarnationContractPreview", mainMenu, StringComparison.Ordinal);
        Assert.True(
            mainMenu.IndexOf("if (!ConfirmIncarnationContractPreview", StringComparison.Ordinal) <
            mainMenu.IndexOf("_fs.ClearCurrentWorldLore();", StringComparison.Ordinal));

        Assert.Contains("BuildInkFeatherActionAuditNode", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("output/ink_feather_action_result.json", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("alreadyDeductedByClient", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("stateEvidence обязан содержать affectedFiles", inkFeathers, StringComparison.Ordinal);

        Assert.Contains("Полный pending contract основания Хранителя", foundation, StringComparison.Ordinal);
        Assert.Contains("UpdateGuardians.create", foundation, StringComparison.Ordinal);
        Assert.Contains("PlayerGuardianFoundationState.PendingRequestPath", foundation, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaosSeaBlockersHelpAndLocalAuditsStayExplicit()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var help = ReadSource("UI", "ExplorerMode", "ExplorerMode.MetaStoryAndStatus.cs");
        var lifecycle = ReadSource("Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var trade = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");
        var inbox = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs");

        Assert.Contains("BuildPendingFileBlockerAsync", mainMenu, StringComparison.Ordinal);
        Assert.Contains("requestId=", mainMenu, StringComparison.Ordinal);
        Assert.Contains("закрытие:", mainMenu, StringComparison.Ordinal);

        Assert.Contains("/abodes", help, StringComparison.Ordinal);
        Assert.Contains("/обители", help, StringComparison.Ordinal);
        Assert.Contains("/обители", lifecycle, StringComparison.Ordinal);

        Assert.Contains("BuildGuardianSellAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("generatedBuybackEntryFields", trade, StringComparison.Ordinal);
        Assert.Contains("GM turn не отправляется: это client-local coordinated write with full audit JSON", trade, StringComparison.Ordinal);

        Assert.Contains("Полный JSON afterlife notification", inbox, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemGuardianPresetScreensExposeFullDossierAndJson()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var explorerPrivate = ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs");

        Assert.Contains("Полный JSON system guardian preset", mainMenu, StringComparison.Ordinal);
        Assert.Contains("Полный JSON system guardian preset", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("dossierMarkdown", mainMenu, StringComparison.Ordinal);
        Assert.Contains("dossierMarkdown", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(18)", mainMenu, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(18)", explorerPrivate, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            new[] { TestRepoPaths.RepoRoot, "BookOfEternityClient" }.Concat(pathParts).ToArray()));
}
