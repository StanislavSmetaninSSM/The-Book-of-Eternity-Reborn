using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualStrainComparisonTests
{
    /// <summary>Checks the complete capped example and retained v3 DATA without constructing live source or publication authority.</summary>
    /// <param name="rank">Independent complete retained III or IV parent alternative.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedCappedSingletonExamplePreservesEqualOrdinaryAndActual(int rank)
    {
        const string marker = "spiritual_existing_pressure_capped_singleton_v3";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            row => row.ContractId == marker);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        Assert.Equal(new[] { "Chaos Sea" }, entry.Realms);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, term => Assert.Contains(term, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("end_" + marker, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var section = text[start..end];
        var block = Assert.Single(Regex.Matches(section, "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        var data = JsonNode.Parse(block.Groups[1].Value)!.AsObject();
        Assert.Equal(new[] { "actualCalculation", "appliedArtTier", "clientActualEqualsOrdinary", "clientActualStrain",
            "clientChangedPaths", "clientManagedPaths", "ordinaryCalculation", "ordinaryConflict", "ordinaryFinalEcho",
            "parentRankVariants", "targetResilienceTier" }, data.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(new[] { 3, 4 }, data["parentRankVariants"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(0, data["appliedArtTier"]!.GetValue<int>());
        Assert.Equal(0, data["targetResilienceTier"]!.GetValue<int>());
        Assert.True(data["clientActualEqualsOrdinary"]!.GetValue<bool>());
        Assert.Empty(data["clientChangedPaths"]!.AsArray());
        Assert.Equal(new[] { "/activeConflict/exchangeLog/N/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientManagedPaths"]!.AsArray().Select(value => value!.GetValue<string>()));

        var ordinaryRoot = data["ordinaryConflict"]!.AsObject();
        var ordinary = Assert.Single(ordinaryRoot["activeConflict"]!["exchangeLog"]!.AsArray())!.AsObject();
        var source = SpiritualExistingPressureTestData.CreateData(rank);
        var expectedExchange = SpiritualExistingPressureTestData.ReadOrdinary(source);
        expectedExchange["before"]!["playerSideStrain"] = "overwhelmed";
        expectedExchange["after"]!["playerSideStrain"] = "broken";
        // The independent fixture pins every actor, dice, cost, snapshot and summary sibling.
        Assert.True(JsonNode.DeepEquals(expectedExchange, ordinary));
        var expectedRoot = JsonNode.Parse("""
            {"schemaVersion":1,"activeConflict":{"conflictId":"conflict-a","realm":"Chaos Sea",
            "sideModel":"direct_duel","dangerMode":"hostile","resolutionState":"active",
            "playerSideStrain":"broken","oppositionSideStrain":"clear","conflictPosition":"contested",
            "controlState":null,"tempoAdvantage":null,"exchangeLog":[],"combatConditions":[],
            "playerSide":{"leadContestant":{"actorType":"player","actorId":"player_soul"},"supporters":[]},
            "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian-a"},"supporters":[]}},
            "recentConflicts":[]}
            """)!.AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]!.AsArray().Add(expectedExchange.DeepClone());
        Assert.True(JsonNode.DeepEquals(expectedRoot, ordinaryRoot));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["playerSideStrain"] = "broken" }, data["ordinaryFinalEcho"]));
        Assert.Equal("broken", data["clientActualStrain"]!.GetValue<string>());
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = data["clientActualStrain"]!.DeepClone();
        var actualRoot = ordinaryRoot.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = actual.DeepClone();
        actualRoot["activeConflict"]!["playerSideStrain"] = data["clientActualStrain"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(ordinary, actual));
        Assert.True(JsonNode.DeepEquals(ordinaryRoot, actualRoot));

        var expected = new SpiritualWoundCalculation(new(8, 0, 0, "overwhelmed", "broken", "hostile", 4),
            3, 4, 0, 14, 2, 4, 4, true, 2);
        Assert.Equal(expected, SpiritualWoundOpportunityMath.Calculate(expected.Input));
        foreach (var name in new[] { "ordinaryCalculation", "actualCalculation" })
        {
            Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" },
                data[name]!.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal((14L, 2), (data[name]!["traumaPressure"]!.GetValue<long>(), data[name]!["maximumSeverityRank"]!.GetValue<int>()));
        }
        var proof = source["strainPreimage"]!.AsObject();
        var parent = proof["parentWoundJsonBase64"]!.GetValue<string>();
        source["coordinate"] = "activeConflict.exchangeLog[0].player";
        source["exchangeJsonBase64"] = Encode(actual);
        source["priorStrainRank"] = 3; source["destinationStrainRank"] = 4;
        source["extraStrainJumps"] = 0; source["maximumSeverityRank"] = 2;
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone();
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + ordinaryRoot.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(ordinaryRoot.ToJsonString())).Fingerprint, fingerprint);
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/0";
        var standalone = source.DeepClone().AsObject(); standalone.Remove("strainPreimage");
        SpiritualExistingPressureTestData.Seal(standalone);
        Assert.Equal(expected, SpiritualWoundSourceWitness.Validate(standalone));
        SpiritualExistingPressureTestData.Seal(source);
        var original = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, comparison.Version);
        Assert.Equal(expected, comparison.OrdinaryCalculation);
        Assert.Equal(expected, calculation);
        Assert.Equal(expected, SpiritualStrainPreimageData.ReadIntrinsic(source, actual).ActualCalculation);
        Assert.Equal(fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        var retainedParent = Assert.Single(comparison.Parents).ParentWound;
        Assert.Equal((rank, rank, rank), (retainedParent.Severity.Rank, retainedParent.Consequences.SlotBudget, retainedParent.Consequences.SlotsUsed));
        var expectedParent = WoundMaterializationContract.Parse(
            Encoding.UTF8.GetString(Convert.FromBase64String(parent)), "guidance.capped.parent");
        Assert.True(expectedParent.IsValid, string.Join("\n", expectedParent.Issues));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(expectedParent.Wound!),
            WoundMaterializationContract.SerializeCanonical(retainedParent));
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        comparison.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.Equal(original, source.ToJsonString());
        actual["incomingAction"]!["summary"] = "Changed authored sibling.";
        source["exchangeJsonBase64"] = Encode(actual);
        SpiritualExistingPressureTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Contains("ordinaryIV=actualIV", section);
        Assert.Contains("no_effect snapshot invariance remains unchanged", section);
        Assert.Contains("not a real source-only publication claim", section);
        Assert.Contains("This fragment is not runtime qualification", section);

        static string Encode(JsonNode value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
    }
}
