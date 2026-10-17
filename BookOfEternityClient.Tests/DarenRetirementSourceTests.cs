using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class DarenRetirementSourceTests
{
    [Fact]
    public void FreshBuildCopiesCurrentCliApiBytesWithoutClaimingSessionMembership()
    {
        var source = File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, "CLI_API_Specification.md"));
        var packaged = Path.Combine(AppContext.BaseDirectory, "operational-resources", "CLI_API_Specification.md");
        Assert.True(File.Exists(packaged), "Fresh selected build must carry the csproj CLI wildcard resource.");
        Assert.Equal(source, File.ReadAllBytes(packaged));
        // This output-resource equality is not a claim that CLI API is one of the52 session docs.
    }

    [Fact]
    public void CampaignResourcesAndDispatchAreRemovedWhileCommonScenarioAndPracticeRemain()
    {
        string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
        var qte = Read("BookOfEternityClient/Services/QteSceneService.cs");
        var practice = Read("BookOfEternityClient/Services/QteSceneService.Practice.cs");
        var scenario = Read("BookOfEternityClient/Services/ScenarioCoreService.cs");
        Assert.Contains("RunCheckAsync", qte, StringComparison.Ordinal);
        Assert.Contains("StartPracticeAttempt", practice, StringComparison.Ordinal);
        Assert.Contains("ResolvePracticeAction", practice, StringComparison.Ordinal);
        Assert.Contains("next_life_scenario_core.json", scenario, StringComparison.Ordinal);
        Assert.Contains("WorldDirectiveService.PendingSetupPath", scenario, StringComparison.Ordinal);
        foreach (var path in new[] { "BookOfEternityClient/Content/DarenQteNarrativeSpine.json", "BookOfEternityClient/Services/QteSceneService.Daren.cs",
            "BookOfEternityClient/Services/DarenQteRewardProfileService.cs", "BookOfEternityClient/Services/DarenRewardProfileFileStore.cs",
            "BookOfEternityClient/Services/DarenRewardProfileRollbackTransaction.cs", "BookOfEternityClient/Core/FileSystemManager.DarenLocalStorage.cs",
            "BookOfEternityClient.WebFrontend/src/components/DarenShowcaseView.tsx", "BookOfEternityClient.WebFrontend/src/api/contract-fixtures/qte-daren-state.json" })
            Assert.False(File.Exists(Path.Combine(TestRepoPaths.RepoRoot, path)), "Retired active campaign resource remains: " + path);
        Assert.DoesNotContain("_darenProfileStore", qte, StringComparison.Ordinal);
        Assert.DoesNotContain("RunDarenShowcaseModeAsync", Read("BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("/api/qte/daren", Read("BookOfEternityClient/WebUi/LocalWebUiHost.cs"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("Rules/Block_CLI_QTE.txt")]
    [InlineData("Examples/E_CLI_QTE_Offer.txt")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    public void ActiveGuidancePreservesOrdinaryPracticeWithoutRetiredCampaignPromises(string path)
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
        Assert.Contains("QTE", text, StringComparison.Ordinal);
        Assert.Contains("practice", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("client-owned", text, StringComparison.OrdinalIgnoreCase);
        foreach (var oldPromise in new[] { "fixed Daren manor theft route", "best permanent Daren tier", "best Daren tier",
            "Daren showcase is client-owned authored content", "Daren showcase work in #919", "Daren rewards/endings/adventure content" })
            Assert.DoesNotContain(oldPromise, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex("may grant a New Game Ink Feather bonus", RegexOptions.IgnoreCase), text);
    }
}
