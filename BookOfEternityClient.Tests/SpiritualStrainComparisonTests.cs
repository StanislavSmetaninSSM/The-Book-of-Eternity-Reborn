using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualStrainComparisonTests
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
        Assert.Equal(2, blocks.Count);
        var data = JsonNode.Parse(blocks[0].Groups[1].Value)!;
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
        Assert.Contains("The exact current player `guard/success` and `guard/partial_success` routes", guide);
        Assert.Contains("Successful player guard needs no strain correction", example[start..end]);
        Assert.Contains("overwhelmed->fractured", example[start..end]);
        Assert.Contains("Missing required full-success", example[start..end]);
        Assert.Contains("The exact current player `binding`, `force_binding` and `recover_spiritual_power`", guide);
        Assert.Contains("Successful player binding, force_binding or recovery needs no strain correction", example[start..end]);
        Assert.Contains("player3->6 for success or3->5 for partial_success", example[start..end]);
        Assert.Contains("force-binding with only one restriction", example[start..end]);
        Assert.Contains("The exact current player `counter`, `break_binding` and `incarnation_resistance`", guide);
        Assert.Contains("Successful player counter or control removal needs no strain correction", example[start..end]);
        Assert.Contains("pre-sign counter tier1 in both", example[start..end]);
        Assert.Contains("do not invent a later control origin", example[start..end]);
        AssertExistingPressureOrdinaryGuidance(JsonNode.Parse(blocks[1].Groups[1].Value)!, example[start..end], guide);
    }

    /// <summary>Checks singleton worked input through the real intrinsic reader, without claiming live projection or accepted provenance.</summary>
    /// <param name="data">Parsed illustrative ordinary exchange and client-derived comparison values.</param>
    /// <param name="example">The strain example section containing both distinct worked fragments.</param>
    /// <param name="guide">The current guide with the explicit singleton boundary.</param>
    private static void AssertExistingPressureOrdinaryGuidance(JsonNode data, string example, string guide)
    {
        Assert.Contains("Signed-existing pressure ordinary input (singleton III)", example);
        Assert.Contains("This fragment is not runtime qualification", example);
        Assert.Contains("ordinary10/maxI and actual15/maxII", example);
        Assert.Contains("do not send A or B for this lane", example);
        Assert.Contains("ordinary mechanical value as final", example);
        Assert.Contains("spiritual_existing_pressure_ordinary_v1", guide);
        Assert.Contains("before < ordinary < actual < broken", guide);
        Assert.Contains("This fragment is not runtime qualification", guide);
        Assert.Contains("Explicit otherwise-undetermined affected-action attribution remains a separate unresolved consumer", guide);
        Assert.Contains("Singleton pressure remains rank III only", guide);

        var ordinary = data["ordinaryExchange"]!.AsObject();
        Assert.Equal("pressure", ordinary["operationType"]!.GetValue<string>());
        Assert.Equal("setback", ordinary["outcome"]!.GetValue<string>());
        Assert.Equal("strained", ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", ordinary["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("fractured", data["ordinaryFinalEcho"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", data["clientActualStrain"]!.GetValue<string>());
        Assert.Equal(new[] { "/activeConflict/exchangeLog/N/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(path => path!.GetValue<string>()).ToArray());

        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = data["clientActualStrain"]!.DeepClone();
        var source = SpiritualExistingPressureTestData.CreateData();
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(actual)));
        var proof = source["strainPreimage"]!.AsObject();
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(ordinary)));
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone();
        SpiritualExistingPressureTestData.Seal(source);
        var actualCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        var ordinaryCalculation = Assert.IsType<SpiritualWoundCalculation>(comparison.OrdinaryCalculation);
        Assert.Equal((10L, 1), (ordinaryCalculation.TraumaPressure, ordinaryCalculation.MaximumSeverityRank));
        Assert.Equal((15L, 2), (actualCalculation.TraumaPressure, actualCalculation.MaximumSeverityRank));
        Assert.Equal(data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(), actualCalculation.TraumaPressure);
        Assert.Equal(data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>(), actualCalculation.MaximumSeverityRank);
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        // Changed authored siblings must still fail after resealing otherwise consistent comparison DATA.
        actual["summary"] = "An unauthorized summary change.";
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(actual)));
        SpiritualExistingPressureTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
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
