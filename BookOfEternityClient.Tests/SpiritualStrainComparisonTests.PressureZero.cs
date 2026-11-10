using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualStrainComparisonTests
{
    /// <summary>Checks complete same-family ordinary input and both full parent alternatives through real v3 DATA readers, never live authority.</summary>
    /// <param name="rank">The independent III or IV signed-existing parent alternative.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void WorkedPressureZeroExampleRetainsComparisonWithoutOffer(int rank)
    {
        const string marker = "spiritual_wound_strain_result_v1";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            row => row.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, term => Assert.Contains(term, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("end_" + marker, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var section = text[start..end];
        var blocks = Regex.Matches(section, "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant);
        Assert.Equal(3, blocks.Count);
        var data = JsonNode.Parse(blocks[2].Groups[1].Value)!.AsObject();
        Assert.Equal(new[] { "actualCalculation", "appliedArtTier", "clientActualStrain", "clientChangedPaths",
            "clientComparisonOnly", "ordinaryCalculation", "ordinaryConflict", "ordinaryFinalEcho",
            "parentConsequenceVariants", "parentRankVariants", "targetResilienceTier" },
            data.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(new[] { 3, 4 }, data["parentRankVariants"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(new[] { 3, 4 }, data["parentConsequenceVariants"]!.AsArray().Select(row => row!["rank"]!.GetValue<int>()));
        Assert.Equal(0, data["appliedArtTier"]!.GetValue<int>());
        Assert.Equal(3, data["targetResilienceTier"]!.GetValue<int>());
        Assert.Equal("fractured", data["clientActualStrain"]!.GetValue<string>());
        Assert.Equal(new[] { "/activeConflict/exchangeLog/N/after/playerSideStrain", "/activeConflict/playerSideStrain" },
            data["clientChangedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));

        // Each invocation gets fresh complete DATA, not a cached graph, owner, accepted receipt or signature.
        var fixture = SpiritualPressureZeroTestData.CreateData(rank);
        var ordinaryRoot = data["ordinaryConflict"]!.AsObject();
        var conflict = ordinaryRoot["activeConflict"]!.AsObject();
        var ordinary = Assert.Single(conflict["exchangeLog"]!.AsArray())!.AsObject();
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary, ordinary));
        var expectedRoot = new JsonObject
        {
            ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict-a", ["realm"] = "Chaos Sea", ["sideModel"] = "direct_duel",
                ["dangerMode"] = "hostile", ["resolutionState"] = "active",
                ["playerSideStrain"] = "strained", ["oppositionSideStrain"] = "clear",
                ["conflictPosition"] = "contested", ["controlState"] = null, ["tempoAdvantage"] = null,
                ["exchangeLog"] = new JsonArray(fixture.Ordinary.DeepClone()), ["combatConditions"] = new JsonArray(),
                ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject
                    { ["actorType"] = "player", ["actorId"] = "player_soul" }, ["supporters"] = new JsonArray() },
                ["oppositionSide"] = new JsonObject { ["leadContestant"] = new JsonObject
                    { ["actorType"] = "guardian", ["actorId"] = "guardian-a" }, ["supporters"] = new JsonArray() }
            },
            ["recentConflicts"] = new JsonArray()
        };
        Assert.True(JsonNode.DeepEquals(expectedRoot, ordinaryRoot));
        Assert.Equal("strained", data["ordinaryFinalEcho"]!["playerSideStrain"]!.GetValue<string>());
        foreach (var field in new[] { "playerSideStrain", "oppositionSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Assert.True(JsonNode.DeepEquals(ordinary["after"]![field], conflict[field]));
        Assert.Equal((4, 12, -8), (ordinary["diceAudit"]!["playerTotal"]!.GetValue<int>(),
            ordinary["diceAudit"]!["oppositionTotal"]!.GetValue<int>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        foreach (var side in new[] { "player", "opposition" })
            Assert.Equal((0, 3, 3, 0), (ordinary["actionCostAudit"]![side]!["artTier"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>(),
                ordinary["actionCostAudit"]![side]!["before"]!.GetValue<int>(), ordinary["actionCostAudit"]![side]!["after"]!.GetValue<int>()));
        Assert.False(ordinary.ContainsKey(SpiritualWoundFailedAction.Property));
        var actual = ordinary.DeepClone().AsObject();
        actual["after"]!["playerSideStrain"] = data["clientActualStrain"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(fixture.Actual, actual));
        var actualRoot = ordinaryRoot.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = actual.DeepClone();
        actualRoot["activeConflict"]!["playerSideStrain"] = "fractured";
        var restored = actualRoot.DeepClone().AsObject();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "strained";
        restored["activeConflict"]!["playerSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(ordinaryRoot, restored));

        var source = fixture.Source;
        var proof = source["strainPreimage"]!.AsObject();
        // Verify the helper's independent exact input before rebinding to this complete illustrative root.
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint,
            proof["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + ordinaryRoot.ToJsonString() + "\n")).ToArray();
        var fingerprint = new CanonicalBeforeImage(true, raw).Fingerprint;
        Assert.NotEqual(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(ordinaryRoot.ToJsonString())).Fingerprint, fingerprint);
        Assert.True(JsonNode.DeepEquals(ordinaryRoot, JsonNode.Parse(Encoding.UTF8.GetString(raw.AsSpan(3)))));
        source["coordinate"] = "activeConflict.exchangeLog[0].player";
        // Retained semantic payloads are canonical; the original input fingerprint keeps its exact raw bytes above.
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(actual)));
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(ordinary)));
        proof["ordinaryCalculation"] = data["ordinaryCalculation"]!.DeepClone();
        proof["originalInput"]!["contentFingerprint"] = fingerprint;
        proof["originalInput"]!["exchangePointer"] = "/activeConflict/exchangeLog/0";
        SpiritualExistingPressureTestData.Seal(source);
        var input = source.ToJsonString();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var retained);
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(retained);
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        Assert.Equal(3, comparison.Version);
        Assert.Equal(SpiritualPressureZeroTestData.OrdinaryCalculation(), comparison.OrdinaryCalculation);
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(), calculation);
        Assert.Equal(comparison.OrdinaryCalculation, direct.OrdinaryCalculation);
        Assert.Equal(calculation, direct.ActualCalculation);
        Assert.Equal((7L, 0), (data["actualCalculation"]!["traumaPressure"]!.GetValue<long>(),
            data["actualCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal(fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64",
            "originalInput", "parentWoundJsonBase64", "version" }, proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("existing_pressure_ordinary_v1", proof["inputRoleRevision"]!.GetValue<string>());
        AssertPressureZeroParentGuidance(data, fixture.Parent, comparison, rank);
        AssertPressureZeroComparisonOnlyGuidance(data, source);
        comparison.ReadActualExchange()["summary"] = "Caller edit cannot change retained DATA.";
        Assert.True(JsonNode.DeepEquals(actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], comparison.ReadOrdinaryAfter(actual)));
        Assert.Equal(input, source.ToJsonString());
        Assert.Contains("ordinary2/max0 and actual7/max0", section);
        Assert.Contains("No opportunity, private decision packet, none or extra cost", section);
        Assert.Contains("preimage-v3 is not receipt-schema3", section);
        Assert.Contains("There is no C2 packet to reopen", section);
        // Preserve the existing complete-player family cross-reference while updating only the obsolete guardian exclusion.
        Assert.Contains("spiritual_existing_pressure_group_zero_v1", section);
        Assert.Contains("complete-player v4/v6 maximum0", section);
        Assert.Contains("Guardian-v5 maximum0 is comparison-only", section);
        Assert.Contains("This fragment is not runtime qualification", section);
        actual["actionCostAudit"]!["player"]!["after"] = 1;
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(actual)));
        SpiritualExistingPressureTestData.Seal(source);
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
    }

    /// <summary>Checks every documented parent slot against a full independently authored graph and its production-parsed retained image.</summary>
    /// <param name="data">The worked explanatory wrapper.</param>
    /// <param name="parent">Independent complete parent DATA, never current authority.</param>
    /// <param name="comparison">Production-parsed v3 comparison.</param>
    /// <param name="rank">The III or IV alternative under test.</param>
    private static void AssertPressureZeroParentGuidance(JsonObject data, JsonObject parent,
        SpiritualStrainPreimageData comparison, int rank)
    {
        var expected = WoundMaterializationContract.Parse(parent.ToJsonString(), "guidance.pressureZero.parent");
        Assert.True(expected.IsValid, string.Join("\n", expected.Issues));
        var retained = Assert.Single(comparison.Parents).ParentWound;
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(expected.Wound!), WoundMaterializationContract.SerializeCanonical(retained));
        Assert.Equal((rank, rank, rank), (retained.Severity.Rank, retained.Consequences.SlotBudget, retained.Consequences.SlotsUsed));
        var variant = Assert.Single(data["parentConsequenceVariants"]!.AsArray(), row => row!["rank"]!.GetValue<int>() == rank)!;
        Assert.Equal(rank, variant["slotBudget"]!.GetValue<int>());
        Assert.Equal(rank, variant["slotsUsed"]!.GetValue<int>());
        Assert.Equal(2 * rank, variant["recoveryThreshold"]!.GetValue<int>());
        Assert.Equal(2 * rank, parent["recovery"]!["currentStepThreshold"]!.GetValue<int>());
        var entries = variant["entries"]!.AsArray();
        var definitions = parent["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray();
        Assert.Equal(rank, entries.Count);
        Assert.Equal(rank, definitions.Count);
        Assert.Equal(rank, parent["consequences"]!["entries"]!.AsArray().Count);
        Assert.Equal(rank, parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Count);
        for (var index = 0; index < rank; index++)
        {
            var component = Assert.Single(definitions[index]!["components"]!.AsArray())!;
            Assert.Equal(entries[index]!["profile"]!.GetValue<string>(), component["profile"]!.GetValue<string>());
            Assert.Equal(entries[index]!["operation"]!.GetValue<string>(), component["payload"]!["operation"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(entries[index]!["magnitude"], component["payload"]!["magnitude"]));
            Assert.Equal(1, definitions[index]!["stacking"]!["maxStacks"]!.GetValue<int>());
        }
    }

    /// <summary>Reads an illustrative zero-only receipt as DATA and checks no offer/decision semantics without constructing a packet or owner.</summary>
    /// <param name="data">The worked explanatory wrapper.</param>
    /// <param name="source">The independently validated exact zero-pressure witness.</param>
    private static void AssertPressureZeroComparisonOnlyGuidance(JsonObject data, JsonObject source)
    {
        var expected = JsonNode.Parse("""
            {"preimageVersion":3,"receiptSchemaVersion":3,"comparisonCount":1,"opportunityCount":0,
             "decisionCount":0,"privateDecisionPacket":false,"extraCost":0}
            """)!;
        Assert.True(JsonNode.DeepEquals(expected, data["clientComparisonOnly"]));
        Assert.Equal(0, source["maximumSeverityRank"]!.GetValue<int>());
        Assert.Null(source["guaranteedSeverityRank"]);
        var instance = new JsonObject
        {
            ["instanceId"] = "", ["ordinal"] = 1, ["displayConflictId"] = "conflict-a", ["realm"] = "chaos_sea",
            ["startSessionId"] = "session-a", ["startRequestId"] = "request-a", ["startSnapshotToken"] = "snapshot-a",
            ["startTurn"] = 42, ["baselineConflictFingerprint"] = "sha256:" + new string('a', 64), ["instanceFingerprint"] = ""
        };
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..]; instance["instanceFingerprint"] = hash;
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 3, ["nextInstanceOrdinal"] = 2, ["nextClosureOrdinal"] = 1,
            ["nextSourceOrdinal"] = 1, ["nextDecisionOrdinal"] = 1,
            ["nextHealingSourceOrdinal"] = 1, ["nextHealingDecisionOrdinal"] = 1, ["nextStrainComparisonOrdinal"] = 2,
            ["instances"] = new JsonArray(instance), ["closures"] = new JsonArray(),
            ["sources"] = new JsonArray(), ["decisions"] = new JsonArray(),
            ["healingSources"] = new JsonArray(), ["healingDecisions"] = new JsonArray(),
            ["strainComparisons"] = new JsonArray(new JsonObject
            {
                ["ordinal"] = 1, ["instanceId"] = instance["instanceId"]!.DeepClone(),
                ["instanceComparisonOrdinal"] = 1, ["witness"] = source.DeepClone()
            })
        };
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), "guidance.pressureZero.receipt");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.True(parsed.State!.HasRetainedPressureComparisons);
        var roundTrip = JsonNode.Parse(SpiritualWoundOpportunityReceiptState.SerializeCanonical(parsed.State))!;
        Assert.Empty(roundTrip["sources"]!.AsArray());
        Assert.Empty(roundTrip["decisions"]!.AsArray());
        var row = Assert.Single(roundTrip["strainComparisons"]!.AsArray())!.AsObject();
        Assert.Equal(new[] { "instanceComparisonOrdinal", "instanceId", "ordinal", "witness" },
            row.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.True(JsonNode.DeepEquals(source, row["witness"]));
    }
}
