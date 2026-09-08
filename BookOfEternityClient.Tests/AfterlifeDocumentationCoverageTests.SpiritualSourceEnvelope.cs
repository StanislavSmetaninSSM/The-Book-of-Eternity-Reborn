using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void SpiritualSourceActionDocumentation_DeclaresExactTargetAndTerminalWitness()
    {
        var guide = NormalizeSpiritualSourceActionDocumentation(
            ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md"));
        foreach (var token in new[]
        {
            "## spiritual_wound_source_action_v1",
            "player exchange object", "opposition incomingAction",
            "spiritualWoundTarget", "actorType", "actorId", "retraumaWoundRef",
            "closed object", "raw non-empty exact strings", "no surrounding whitespace",
            "original affected-side lead", "original member of the affected side",
            "canonical prior active spiritual wound", "immutable history",
            "traumaPressure", "sourceSeverityCap", "maximumSeverityRank",
            "guaranteedSeverityRank", "resolution.terminalExchange",
            "complete existing exchange witness", "matching complete diceAudit",
            "not a totals summary",
            "not an admitted wound, opportunity, decision, receipt"
        })
            Assert.Contains(token, guide, StringComparison.Ordinal);
    }

    [Fact]
    public void SpiritualSourceActionDocumentation_RoutesBothRealmsAndPendingContours()
    {
        var files = new[]
        {
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt"),
            ReadRepoFile("Examples", "example_validation_manifest.json")
        };
        foreach (var text in files)
        {
            Assert.Contains("spiritual_wound_source_action_v1", text, StringComparison.Ordinal);
            Assert.Contains("spiritualWoundTarget", text, StringComparison.Ordinal);
            Assert.Contains("terminalExchange", text, StringComparison.Ordinal);
            Assert.Contains("Chaos Sea", text, StringComparison.Ordinal);
            Assert.Contains("Shining Abode", text, StringComparison.Ordinal);
        }

        var combined = string.Join(Environment.NewLine, files);
        foreach (var token in new[]
        {
            "start", "escalation", "prefix", "terminal", "passive",
            "champion", "dice-free voluntary", "optional dissipation",
            "ValidateSpiritualWoundSourceActionShape",
            "BeginSpiritualWoundSourceSessionAsync", "TerminalClosure",
            "source-local", "C/D/E"
        })
            Assert.Contains(token, combined, StringComparison.Ordinal);
    }

    private static string NormalizeSpiritualSourceActionDocumentation(string text) =>
        Regex.Replace(text.Replace("`", string.Empty, StringComparison.Ordinal), @"\s+", " ");

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
