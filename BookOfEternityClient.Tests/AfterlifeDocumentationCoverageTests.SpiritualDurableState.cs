using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    /// <summary>
    /// Requires private ownership, exact decline guidance and honest non-live coverage.
    /// </summary>
    [Fact]
    public void SpiritualDurableStateDocumentsPrivateOwnershipAndCurrentBoundary()
    {
        foreach (var text in new[] { ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
                     ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt") })
        foreach (var token in new[] { "spiritual_wound_private_roots_v1",
                     "game_state/control/spiritual_wound_capture_checkpoint.json",
                     "game_state/control/pending_spiritual_wound_decisions.json",
                     "game_state/wounds/spiritual_wound_opportunity_receipts.json",
                     "GM must not write", "not player pending requests", "hashes do not grant authority",
                     "does not enable live continuation or publication",
                     "receipt after-image", "none", "materialize", "guarantee_satisfied",
                     "satisfiedSeverityRank", "transitionId",
                     "woundId and transitionId", "Live GameEngine/daemon" })
            Assert.Contains(token, text, StringComparison.Ordinal);

        var guide = ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md");
        foreach (var token in new[] { "## spiritual_wound_explicit_decline_receipt_v1",
                     "opportunityRef", "decision", "none", "full source witness",
                     "selected severity", "wound ID", "transition ID",
                     "## spiritual_wound_materialize_receipt_v1",
                     "actual source-bound wound insertion", "unpublished",
                     "## spiritual_wound_guarantee_satisfied_receipt_v1",
                     "satisfiedSeverityRank", "zero maximum",
                     "Live GameEngine/daemon dispatch" })
            Assert.Contains(token, guide, StringComparison.Ordinal);
        Assert.Contains("For two sources in the same original turn", guide, StringComparison.Ordinal);
        Assert.Contains("worsening target", guide, StringComparison.Ordinal);
        Assert.Contains("already meets that source's maximum", guide, StringComparison.Ordinal);
        Assert.Contains("предлагает только `none`",
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"), StringComparison.Ordinal);
        Assert.Contains("Worked capped same-side continuation",
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt"), StringComparison.Ordinal);
        Assert.Contains("Worked simultaneous-side continuation",
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt"), StringComparison.Ordinal);
        Assert.Contains("ране каждой стороне",
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"), StringComparison.Ordinal);
        Assert.Contains("shared exchange coordinate does not merge those wounds",
            guide, StringComparison.Ordinal);
        Assert.Contains("neither side has a newly created",
            guide, StringComparison.Ordinal);
        Assert.Contains("один woundId", ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            StringComparison.Ordinal);
        Assert.Contains("The same complete response also illustrates a later source",
            ReadRepoFile("Examples", "E_CLI_Effect_Materialization.txt"), StringComparison.Ordinal);
    }
}
