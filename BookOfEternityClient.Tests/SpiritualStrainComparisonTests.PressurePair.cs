using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualStrainComparisonTests
{
    /// <summary>Checks all canonical-ID rank configurations against both independently authored complete ordinary examples, not live authority.</summary>
    [Theory]
    [InlineData(3, 3, "fractured", 13L, 2)]
    [InlineData(3, 3, "overwhelmed", 18L, 3)]
    [InlineData(3, 4, "fractured", 13L, 2)]
    [InlineData(3, 4, "overwhelmed", 18L, 3)]
    [InlineData(4, 3, "fractured", 13L, 2)]
    [InlineData(4, 3, "overwhelmed", 18L, 3)]
    [InlineData(4, 4, "fractured", 13L, 2)]
    [InlineData(4, 4, "overwhelmed", 18L, 3)]
    public void WorkedPressurePairExamplePreservesCompleteOrdinaryInputAndClientProjection(
        int first, int second, string ordinaryStrain, long ordinaryPressure, int ordinaryMaximum)
    {
        var (data, section) = ReadPressurePairExample();
        Assert.Equal(new[] { "clientChangedPaths", "ordinaryCases", "parentRankConfigurations", "strainMagnitude" },
            data.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(1, data["strainMagnitude"]!.GetValue<int>());
        var ranks = data["parentRankConfigurations"]!.AsArray();
        Assert.Equal(new[] { "[3,3]", "[3,4]", "[4,3]", "[4,4]" },
            ranks.Select(row => row!.ToJsonString(new() { WriteIndented = false })));
        Assert.Contains(ranks, row => row![0]!.GetValue<int>() == first && row[1]!.GetValue<int>() == second);
        Assert.Equal(new[] { "/activeConflict/exchangeLog/N/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(path => path!.GetValue<string>()));
        var cases = data["ordinaryCases"]!.AsArray();
        Assert.Equal(2, cases.Count);
        var example = Assert.Single(cases, row =>
            row!["ordinaryExchange"]!["after"]!["playerSideStrain"]!.GetValue<string>() == ordinaryStrain)!.AsObject();
        Assert.Equal(new[] { "clientActualCalculation", "clientActualStrain", "ordinaryCalculation", "ordinaryExchange", "ordinaryFinalEcho", "uncappedStrainSum" },
            example.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));
        var ordinary = example["ordinaryExchange"]!.AsObject();
        var source = SpiritualPressurePairTestData.CreateSource(ordinaryStrain, new(first, second));
        var proof = source["strainPreimage"]!.AsObject();
        var expectedOrdinary = JsonNode.Parse(SpiritualWoundStateJson.DecodeJsonObjectBytes(proof["ordinaryExchangeJsonBase64"]))!;
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, ordinary)); // All actors, dice, costs, summaries and before/after siblings.
        Assert.True(JsonNode.DeepEquals(ordinary["after"], example["ordinaryFinalEcho"]));
        Assert.Equal("clear", ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(ordinaryMaximum + 2, example["uncappedStrainSum"]!.GetValue<int>());
        Assert.Equal("broken", example["clientActualStrain"]!.GetValue<string>());
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = example["clientActualStrain"]!.DeepClone();
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
        proof["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        proof["ordinaryCalculation"] = example["ordinaryCalculation"]!.DeepClone();
        SpiritualPressurePairTestData.Seal(source);
        var originalBytes = source.ToJsonString();

        var actualCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);

        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, comparison.Version);
        Assert.Equal(new[] { first, second }, comparison.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.Equal((ordinaryPressure, ordinaryMaximum),
            (comparison.OrdinaryCalculation!.TraumaPressure, comparison.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Equal((23L, 4, 4), (actualCalculation.TraumaPressure, actualCalculation.NewStrainRank, actualCalculation.MaximumSeverityRank));
        Assert.Equal((23L, 4), (example["clientActualCalculation"]!["traumaPressure"]!.GetValue<long>(),
            example["clientActualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        for (var index = 0; index < 2; index++)
        {
            var parent = comparison.Parents[index];
            var complete = JsonNode.Parse(SpiritualWoundStateJson.DecodeJsonObjectBytes(
                proof["parents"]![index]!["parentWoundJsonBase64"]))!;
            Assert.True(JsonNode.DeepEquals(complete,
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            var definition = complete["consequences"]!["ownedEffectSources"]!["definitions"]![0]!;
            Assert.Equal(1, definition["components"]![0]!["payload"]!["magnitude"]!.GetValue<int>());
            Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
        }
        // Returned DATA stays detached and cannot silently authorize changed authored siblings.
        comparison.ReadActualExchange()["summary"] = "Caller-mutated copy.";
        comparison.ReadOrdinaryAfter(actual)["oppositionSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        Assert.Equal(originalBytes, source.ToJsonString());
        actual["incomingAction"]!["summary"] = "Changed authored sibling.";
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
        SpiritualPressurePairTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Contains("uncapped5 is not a fifth strain rank", section);
    }

    /// <summary>Pins the bounded GM authoring lane, singleton containment, real opportunity rule and separate unresolved consumer.</summary>
    [Fact]
    public void PressurePairGuidancePinsScopeAndUnresolvedAttribution()
    {
        var (_, section) = ReadPressurePairExample();
        var guide = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "OtherGuides/Wound_Materialization_Contract.md"));
        var prompt = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "TaskGuides/CLI_Step_Main.txt"));
        var matrix = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "OtherGuides/Afterlife_Contract_Matrix.md"));
        Assert.Contains("one step per distinct active applicable wound", guide);
        Assert.Contains("Singleton pressure accepts rank III or IV", guide);
        Assert.Contains("exactly two rank III/IV parents", guide);
        Assert.Contains("Explicit otherwise-undetermined affected-action attribution remains a separate unresolved consumer", guide);
        Assert.Contains("outside the new guardian lane", guide);
        Assert.Contains("Player v1–v4 forbid `spiritualWoundFailedAction`", guide);
        Assert.Contains("Unsupported or ambiguous failure requires refusal/repair", guide);
        Assert.Contains("Other applicable IV cost, roll and art contributions remain in force", guide);
        Assert.Contains("<SpiritualWoundStrainCap>", prompt);
        Assert.Contains("<SpiritualExistingPressureOrdinary>", prompt);
        Assert.Contains("spiritual_existing_pressure_ordinary_v1", prompt);
        Assert.Contains("complete ordinary exchange and matching ordinary root echo", prompt);
        Assert.Contains("Unsupported or ambiguous failure requires refusal/repair", prompt);
        Assert.Contains("### Signed-existing wound strain pressure pair", matrix);
        Assert.Contains("ordinary13/II or18/III -> actual23/IV", matrix);
        Assert.Contains("Retained histories are DATA, not two executed native births", matrix);
        Assert.Contains("Only a genuinely issued current public opportunityRef permits a none response", section);
        Assert.Contains("Declining leaves actual broken and both existing parents unchanged", section);
        Assert.Contains("Broken is not terminal defeat", section);
        Assert.Contains("No response, witness, count or attribution fields are added", section);
    }

    /// <summary>Reads only the separately delimited pair family; the old singleton marker keeps its original two JSON fences.</summary>
    private static (JsonObject Data, string Section) ReadPressurePairExample()
    {
        const string marker = "spiritual_existing_pressure_pair_v1";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            row => row.ContractId == marker);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, term => Assert.Contains(term, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("end_" + marker, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var section = text[start..end];
        var block = Assert.Single(Regex.Matches(section, "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        return (JsonNode.Parse(block.Groups[1].Value)!.AsObject(), section);
    }
}
