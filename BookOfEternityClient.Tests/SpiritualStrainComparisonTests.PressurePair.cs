using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualStrainComparisonTests
{
    /// <summary>Checks all four ordered parent ranks against the two original rows and complete low-pressure A–D examples.</summary>
    /// <param name="first">Severity at the first exact wound-ID coordinate.</param>
    /// <param name="second">Severity at the second exact wound-ID coordinate.</param>
    /// <param name="row">Original boundary0/overflow1, then A2/B3/C4/D5.</param>
    [Theory]
    [InlineData(3, 3, 0)]
    [InlineData(3, 4, 0)]
    [InlineData(4, 3, 0)]
    [InlineData(4, 4, 0)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 3, 1)]
    [InlineData(4, 4, 1)]
    [InlineData(3, 3, 2)]
    [InlineData(3, 4, 2)]
    [InlineData(4, 3, 2)]
    [InlineData(4, 4, 2)]
    [InlineData(3, 3, 3)]
    [InlineData(3, 4, 3)]
    [InlineData(4, 3, 3)]
    [InlineData(4, 4, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(3, 4, 4)]
    [InlineData(4, 3, 4)]
    [InlineData(4, 4, 4)]
    [InlineData(3, 3, 5)]
    [InlineData(3, 4, 5)]
    [InlineData(4, 3, 5)]
    [InlineData(4, 4, 5)]
    public void WorkedPressurePairExamplePreservesCompleteOrdinaryInputAndClientProjection(int first, int second, int row)
    {
        var (data, section) = ReadPressurePairExample();
        Assert.Equal(new[] { "clientChangedPaths", "ordinaryCases", "parentRankConfigurations", "strainMagnitude" },
            data.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(1, data["strainMagnitude"]!.GetValue<int>());
        var ranks = data["parentRankConfigurations"]!.AsArray();
        Assert.Equal(new[] { "[3,3]", "[3,4]", "[4,3]", "[4,4]" },
            ranks.Select(value => value!.ToJsonString(new() { WriteIndented = false })));
        Assert.Contains(ranks, value => value![0]!.GetValue<int>() == first && value[1]!.GetValue<int>() == second);
        Assert.Equal(new[] { "/activeConflict/exchangeLog/N/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(path => path!.GetValue<string>()));
        var cases = data["ordinaryCases"]!.AsArray();
        Assert.Equal(6, cases.Count);
        var example = cases[row]!.AsObject();
        var fields = new[] { "clientActualCalculation", "clientActualStrain", "ordinaryCalculation", "ordinaryExchange",
            "ordinaryFinalEcho", "uncappedStrainSum" };
        Assert.Equal((row < 2 ? fields : fields.Concat(new[] { "ordinaryConflict", "targetResilienceTier" }))
            .OrderBy(key => key, StringComparer.Ordinal), example.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));
        // Independent literal full records, never calculated from the example or a returned proof.
        var (die, resilience, expectedOrdinary, expectedActual) = PairGuidanceCalculations(row);
        var ordinary = example["ordinaryExchange"]!.AsObject();
        var source = SpiritualPressurePairTestData.CreateSource("fractured", new(first, second));
        var proof = source["strainPreimage"]!.AsObject();
        var expectedExchange = JsonNode.Parse(SpiritualWoundStateJson.DecodeJsonObjectBytes(proof["ordinaryExchangeJsonBase64"]))!.AsObject();
        expectedExchange["after"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        expectedExchange["diceAudit"]!["diceUsed"]![0]!["value"] = die;
        expectedExchange["diceAudit"]!["playerTotal"] = die;
        expectedExchange["diceAudit"]!["margin"] = die - 12;
        expectedExchange["diceAudit"]!["outcomeBand"] = die == 6 ? "opposition_success" : "decisive_opposition_success";
        Assert.True(JsonNode.DeepEquals(expectedExchange, ordinary)); // Complete actors, dice, costs, summaries and all snapshots.
        Assert.True(JsonNode.DeepEquals(ordinary["after"], example["ordinaryFinalEcho"]));
        Assert.Equal("clear", ordinary["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(expectedOrdinary.NewStrainRank + 2, example["uncappedStrainSum"]!.GetValue<int>());
        Assert.Equal(expectedActual.Input.NewStrain, example["clientActualStrain"]!.GetValue<string>());
        if (row >= 2) Assert.Equal(resilience, example["targetResilienceTier"]!.GetValue<int>());
        var ordinaryRoot = new JsonObject
        {
            ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict-a", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
                ["dangerMode"] = "hostile", ["resolutionState"] = "active",
                ["playerSideStrain"] = expectedOrdinary.Input.NewStrain, ["oppositionSideStrain"] = "clear",
                ["conflictPosition"] = "contested", ["controlState"] = null, ["tempoAdvantage"] = null,
                ["exchangeLog"] = new JsonArray(expectedExchange.DeepClone()), ["combatConditions"] = new JsonArray(),
                ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
                ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" }, ["supporters"] = new JsonArray() }
            }, ["recentConflicts"] = new JsonArray()
        };
        if (row >= 2) Assert.True(JsonNode.DeepEquals(ordinaryRoot, example["ordinaryConflict"]));
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = example["clientActualStrain"]!.DeepClone();
        var actualRoot = ordinaryRoot.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = actual.DeepClone();
        actualRoot["activeConflict"]!["playerSideStrain"] = expectedActual.Input.NewStrain;
        var restored = actualRoot.DeepClone().AsObject();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        restored["activeConflict"]!["playerSideStrain"] = expectedOrdinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(ordinaryRoot, restored));
        var raw = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(" \n" + ordinaryRoot.ToJsonString() + "\n")).ToArray();
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
        source["harmfulMargin"] = 12 - die; source["targetResilienceTier"] = resilience;
        source["destinationStrainRank"] = expectedActual.NewStrainRank; source["extraStrainJumps"] = expectedActual.ExtraJumpSteps;
        source["maximumSeverityRank"] = expectedActual.MaximumSeverityRank;
        var evidence = source["turnEvidence"]!;
        evidence["acceptedD20Values"] = new JsonArray(die, 12);
        var claim = evidence["diceClaims"]![0]!.AsObject();
        claim["value"] = die; claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
        proof["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        proof["ordinaryCalculation"] = example["ordinaryCalculation"]!.DeepClone();
        proof["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint;
        proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/0";
        // Prove both complete standalone calculations before invoking the comparison reader.
        var standalone = source.DeepClone().AsObject(); standalone.Remove("strainPreimage");
        SpiritualPressurePairTestData.Seal(standalone);
        Assert.Equal(expectedActual, SpiritualWoundSourceWitness.Validate(standalone));
        standalone["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        standalone["destinationStrainRank"] = expectedOrdinary.NewStrainRank;
        standalone["extraStrainJumps"] = expectedOrdinary.ExtraJumpSteps;
        standalone["maximumSeverityRank"] = expectedOrdinary.MaximumSeverityRank;
        SpiritualPressurePairTestData.Seal(standalone);
        Assert.Equal(expectedOrdinary, SpiritualWoundSourceWitness.Validate(standalone));
        SpiritualPressurePairTestData.Seal(source);
        var originalBytes = source.ToJsonString();
        var actualCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(4, comparison.Version);
        Assert.Equal(new[] { first, second }, comparison.Parents.Select(parent => parent.ParentWound.Severity.Rank));
        Assert.Equal(expectedOrdinary, comparison.OrdinaryCalculation);
        Assert.Equal(expectedActual, actualCalculation);
        Assert.Equal((expectedActual.TraumaPressure, expectedActual.MaximumSeverityRank),
            (example["clientActualCalculation"]!["traumaPressure"]!.GetValue<long>(), example["clientActualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal((expectedOrdinary.TraumaPressure, expectedOrdinary.MaximumSeverityRank),
            (example["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), example["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        for (var index = 0; index < 2; index++)
        {
            var parent = comparison.Parents[index]; var rank = index == 0 ? first : second;
            var complete = JsonNode.Parse(SpiritualWoundStateJson.DecodeJsonObjectBytes(proof["parents"]![index]!["parentWoundJsonBase64"]))!;
            Assert.True(JsonNode.DeepEquals(complete, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal(rank, parent.ParentWound.Consequences.SlotsUsed); Assert.Equal(rank, parent.ParentWound.Consequences.SlotBudget);
            var definition = complete["consequences"]!["ownedEffectSources"]!["definitions"]![0]!;
            var components = definition["components"]!.AsArray();
            Assert.Equal(rank, components.Count);
            Assert.Equal(1, components[0]!["payload"]!["magnitude"]!.GetValue<int>());
            Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
            Assert.Equal("counter", components[1]!["payload"]!["operation"]!.GetValue<string>());
            Assert.Equal("guard", components[2]!["payload"]!["operation"]!.GetValue<string>());
            Assert.Equal(rank == 4 ? 3 : 2, components[2]!["payload"]!["magnitude"]!.GetValue<int>());
            if (rank == 4) Assert.Equal("counter", components[3]!["payload"]!["operation"]!.GetValue<string>());
        }
        comparison.ReadActualExchange()["summary"] = "Caller-mutated copy.";
        comparison.ReadOrdinaryAfter(actual)["oppositionSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        Assert.Equal(originalBytes, source.ToJsonString());
        actual["incomingAction"]!["summary"] = "Changed authored sibling.";
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual); SpiritualPressurePairTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Contains("uncapped5 is not a fifth strain rank", section);
    }

    /// <summary>Six independent full literal example calculations; ordinary strain and wound maximum are distinct.</summary>
    /// <param name="row">Only the two original rows and A–D are supported.</param>
    /// <returns>Independent signed dice, resilience and complete ordinary/actual calculation records.</returns>
    private static (int Die, int Resilience, SpiritualWoundCalculation Ordinary, SpiritualWoundCalculation Actual) PairGuidanceCalculations(int row) => row switch
    {
        0 => (4, 0, new(new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2),
            new(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4)),
        1 => (4, 0, new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3),
            new(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4)),
        2 => (4, 0, new(new(8, 0, 0, "clear", "strained", "hostile", 4), 0, 1, 0, 8, 1, 1, 4, true, 1),
            new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3)),
        3 => (4, 1, new(new(8, 0, 1, "clear", "strained", "hostile", 4), 0, 1, 0, 6, 0, 1, 4, true, 0),
            new(new(8, 0, 1, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 16, 2, 3, 4, true, 2)),
        4 => (4, 3, new(new(8, 0, 3, "clear", "fractured", "hostile", 4), 0, 2, 1, 7, 0, 2, 4, true, 0),
            new(new(8, 0, 3, "clear", "broken", "hostile", 4), 0, 4, 3, 17, 2, 4, 4, true, 2)),
        5 => (6, 5, new(new(6, 0, 5, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 6, 0, 3, 4, true, 0),
            new(new(6, 0, 5, "clear", "broken", "hostile", 4), 0, 4, 3, 11, 1, 4, 4, true, 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };

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
        Assert.Contains("before < ordinary < actual <= broken", guide);
        Assert.Contains("ordinary strictly below broken", guide);
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
        Assert.Contains("before < ordinary < actual <= broken", prompt);
        Assert.Contains("ordinary4, no increase and actual-max0 remain unsupported", prompt);
        Assert.Contains("Unsupported or ambiguous failure requires refusal/repair", prompt);
        Assert.Contains("### Signed-existing wound strain pressure pair", matrix);
        Assert.Contains("ordinary13/II or18/III -> actual23/IV", matrix);
        Assert.Contains("ordinary18/maxIII and actual23/maxIV", matrix);
        Assert.Contains("before < ordinary < actual <= broken", section);
        Assert.Contains("Retained histories are DATA, not two executed native births", matrix);
        Assert.Contains("actual=min(4,ordinary+2)", guide);
        Assert.Contains("Ordinary maximum may be zero, but actual wound maximum must be positive", guide);
        Assert.Contains("Strain rank is not wound maximum", guide);
        Assert.Contains("ordinary I–III", prompt);
        Assert.Contains("actual=min(4,ordinary+2)", prompt);
        Assert.Contains("actual=min(4,ordinary+2)", matrix);
        Assert.Contains("Ordinary I–III gives actual=min(4,ordinary+2)", section);
        Assert.Contains("True actual-max0 still refuses", section);
        Assert.Contains("Only a genuinely issued current public opportunityRef permits a none response", section);
        Assert.Contains("Declining leaves actual III or IV and both existing parents unchanged", section);
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
