using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualStrainComparisonTests
{
    [Fact]
    public void WorkedStrainExampleKeepsSeparateOwnedExchangeAndFinalEcho()
    {
        const string marker = "spiritual_wound_strain_result_v1";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            row => row.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, term => Assert.Contains(term, example));
        var start = example.IndexOf(marker + " — worked", StringComparison.Ordinal);
        var end = example.IndexOf("end_" + marker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var blocks = Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant);
        var data = JsonNode.Parse(Assert.Single(blocks.Cast<Match>()).Groups[1].Value)!;
        var exchange = new JsonObject { ["operationType"] = "pressure", ["outcome"] = "setback",
            ["before"] = data["before"]!.DeepClone(), ["after"] = data["ordinaryAfter"]!.DeepClone() };
        var correction = new ValidationService.SpiritualStrainDraftCorrection("/activeConflict/exchangeLog/1", exchange);
        exchange["after"] = data["correctedAfterA"]!.DeepClone();
        Assert.True(correction.IsCorrected(exchange));
        Assert.Equal(correction.ExpectedStrain, data["finalEchoB"]!["playerSideStrain"]!.GetValue<string>());
        var guide = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "OtherGuides/Wound_Materialization_Contract.md"));
        Assert.Contains("one newly selected applicable burden", guide);
        Assert.Contains("ordinary harmful-source formula", guide);
        Assert.Contains("The exact current player `pressure/success` route preserves the ordinary result", guide);
        Assert.Contains("Successful pressure needs no strain correction", example[start..end]);
        Assert.Contains("ordinary signed 12/4, margin8", example[start..end]);
        Assert.Contains("do not send A or B", example[start..end]);
        Assert.Contains("Failed maneuver uses the proved ordinary result", example[start..end]);
        Assert.Contains("Do not fabricate a strainPreimage", example[start..end]);
        Assert.Contains("Signed-existing parent maneuver", example[start..end]);
        Assert.Contains("Zero-ceiling comparison-only", example[start..end]);
        Assert.Contains("pressure7/max0", example[start..end]);
        Assert.Contains("There is no wound offer and no none decision", example[start..end]);
        Assert.Contains("comparison-only v3 row", guide);
        Assert.Contains("Even none needs owner-reconstructed C3", example[start..end]);
        Assert.Contains("signed pre-turn parent", guide);
        Assert.Contains("first-create History", guide);
        Assert.Contains("Capped failed guard keeps its ordinary source", example[start..end]);
        Assert.Contains("no editable A/B or terminal-echo repair", example[start..end]);
        Assert.Contains("exactly one real new player harm source", guide);
        Assert.Contains("For `partial_success`, the same two player operations are successful", guide);
        Assert.Contains("Partially successful player pressure or maneuver needs no strain correction", example[start..end]);
        Assert.Contains("ordinary maximum0 source", example[start..end]);
    }

    [Theory]
    [InlineData("clear", "strained")]
    [InlineData("strained", "fractured")]
    [InlineData("fractured", "overwhelmed")]
    [InlineData("overwhelmed", "broken")]
    [InlineData("broken", "broken")]
    public void OwnedComparisonDataPermitsOneStepAndPreservesEverySibling(string ordinary, string expected)
    {
        var exchange = new JsonObject
        {
            ["exchangeId"] = "strain_data", ["operationType"] = "pressure", ["outcome"] = "setback",
            ["before"] = new JsonObject { ["playerSideStrain"] = ordinary },
            ["after"] = new JsonObject { ["playerSideStrain"] = ordinary, ["oppositionSideStrain"] = "clear" }
        };
        var correction = new ValidationService.SpiritualStrainDraftCorrection("/activeConflict/exchangeLog/0", exchange);
        Assert.Equal(expected, correction.ExpectedStrain);
        var corrected = exchange.DeepClone().AsObject();
        corrected["after"]!["playerSideStrain"] = expected;
        Assert.True(correction.Allows(exchange));
        Assert.True(correction.IsCorrected(corrected));
        exchange["outcome"] = "success"; // Retained comparison must own an independent copy.
        Assert.False(correction.Allows(exchange));
        foreach (var mutation in new[] { "before", "opposition", "outcome", "operation", "id" })
        {
            var damaged = corrected.DeepClone().AsObject();
            switch (mutation)
            {
                case "before": damaged["before"]!["playerSideStrain"] = "untrusted"; break;
                case "opposition": damaged["after"]!["oppositionSideStrain"] = "strained"; break;
                case "outcome": damaged["outcome"] = "success"; break;
                case "operation": damaged["operationType"] = "guard"; break;
                case "id": damaged["exchangeId"] = "foreign"; break;
            }
            Assert.False(correction.Allows(damaged));
        }
        corrected["after"]!["playerSideStrain"] = "unregistered";
        Assert.False(correction.Allows(corrected));
    }
}
