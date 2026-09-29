using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    /// <summary>
    /// Executes the empty private roots and checks the exact GM decline fragment.
    /// </summary>
    [Fact]
    public void SpiritualDurableStateExample_ParsesEmptyPrivateRoots()
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            value => value.ContractId == "spiritual_wound_private_roots_v1");
        Assert.Equal("E_CLI_Afterlife_Turns.txt", entry.File);
        AssertTruthfulValidationMetadata(entry);
        var roots = ParseNamedJsonFences(entry.File, entry.ContractId);
        Assert.Equal(3, roots.Count);
        Assert.Null(roots[0]["pending"]);
        Assert.True(SpiritualWoundDecisionPendingState.Parse(roots[0].ToJsonString(),
            "game_state/control/pending_spiritual_wound_decisions.json",
            WoundAcceptedTurnSnapshotContract.RequiredPaths).IsValid);
        Assert.True(SpiritualWoundOpportunityReceiptState.Parse(roots[1].ToJsonString(),
            "game_state/wounds/spiritual_wound_opportunity_receipts.json").IsValid);
        var response = roots[2];
        Assert.Equal(new[] { "woundDecisions" }, response.Select(property => property.Key));
        var decision = Assert.Single(response["woundDecisions"]!.AsArray());
        Assert.Equal(new[] { "opportunityRef", "decision" },
            decision!.AsObject().Select(property => property.Key));
        Assert.Equal("spiritual_wound_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            decision["opportunityRef"]!.GetValue<string>());
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
    }
}
