using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PromptDocumentationCoverageTests
{
    [Fact]
    public void InventoryMechanicalBonusAuthorityContract_IsDocumentedForGm()
    {
        var block10 = ReadRepoFile("Rules", "Block_10.txt");
        var example = ReadRepoFile("Examples", "E_Block_10.txt");

        foreach (var requiredText in new[]
        {
            "mechanicalSummaryAuthority",
            "mechanicalSummaryUnresolvedReason",
            "NarrativeOnly",
            "Unresolved",
            "structuredBonuses",
            "combatEffect",
            "customProperties",
            "display summaries only",
            "matching structured authority"
        })
        {
            Assert.Contains(requiredText, block10, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "StructuredInventoryBonusAuthority_Example",
            "Репутация среди аристократов +3",
            "matching structured authority",
            "\"mechanicalSummaryAuthority\": \"NarrativeOnly\"",
            "\"mechanicalSummaryAuthority\": \"Unresolved\"",
            "\"mechanicalSummaryUnresolvedReason\""
        })
        {
            Assert.Contains(requiredText, example, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DaemonSpecDocumentsQteOfferRuntimeContract()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");

        Assert.Contains("QTE OFFERS", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("output/qte_offer.json", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("qteEventsEnabled", lifecyclePrompt, StringComparison.Ordinal);

        foreach (var requiredText in new[]
        {
            "Examples/E_CLI_QTE_Offer.txt",
            "output/qte_offer.json",
            "qteEventsEnabled",
            "ordinary player-driven Mortal World turn",
            "QTE-offer turn не должен одновременно",
            "responseFragment"
        })
        {
            Assert.Contains(requiredText, daemonSpec, StringComparison.Ordinal);
        }

        Assert.Contains("output/qte_offer.json", apiSpec, StringComparison.Ordinal);
        Assert.Contains("output/qte_offer.json", qteExample, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));
}
