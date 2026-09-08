using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void SpiritualSourceEnvelopeDocumentation_DeclaresClosedSourceAndHarderBounds()
    {
        var guide = ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md");
        foreach (var token in new[]
        {
            "## spiritual_wound_source_envelope_v1",
            "profiles[].specialArts[].spiritualWoundEnvelope",
            "schemaVersion", "maximumSeverityRank", "guaranteedSeverityRank",
            "integer 0..4", "integer 1..maximumSeverityRank",
            "neutral IV", "no guarantee", "ordinary harmful-strain eligibility",
            "before the harmful event", "formula", "destination strain", "danger",
            "specialArtAudit", "not an admitted wound opportunity",
            "current wound decision", "including the player_soul profile",
            "never in soul_state.afterlifeCombatProfile.artTiers",
            "A present null or malformed declaration is invalid"
        })
            Assert.Contains(token, guide, StringComparison.Ordinal);
    }

    [Fact]
    public void SpiritualSourceEnvelopeDocumentation_RoutesBothRealmsToWorkedSource()
    {
        foreach (var entrypoint in new[]
        {
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt")
        })
        {
            Assert.Contains("spiritualWoundEnvelope", entrypoint, StringComparison.Ordinal);
            Assert.Contains("spiritual_wound_source_envelope_v1", entrypoint, StringComparison.Ordinal);
            Assert.Contains("OtherGuides/Wound_Materialization_Contract.md", entrypoint, StringComparison.Ordinal);
            Assert.Contains("neutral IV", entrypoint, StringComparison.Ordinal);
        }
    }
}
