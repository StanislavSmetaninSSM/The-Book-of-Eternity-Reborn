using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGroupZeroPressureDataTests
{
    /// <summary>Catches the zero-ledger parser whitelist after core-valid v4/v6 source and independently lawful schema1 premises.</summary>
    /// <param name="version">Exact-two v4 low or complete N3 v6 near-cap DATA; no live authority is fabricated.</param>
    [Theory]
    [InlineData(4)] [InlineData(6)]
    public void GroupZero_ReceiptAppendsWithoutDecision(int version)
    {
        var contour = version switch
        {
            4 => GroupZeroPressureContour.Low, 6 => GroupZeroPressureContour.NearCap,
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        var fixture = SpiritualGroupZeroPressureData.Create(contour);
        AssertPrerequisites(fixture, contour);
        var fixtureBefore = SpiritualGroupZeroPressureData.Serialize(fixture);
        var expected = SpiritualGroupZeroPressureData.Expected(contour);
        Assert.Equal(expected.Actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var comparison));
        AssertSupported(fixture, contour, Assert.IsType<SpiritualStrainPreimageData>(comparison));
        var before = fixture.Retained.Receipts.DeepClone().AsObject();
        var baseline = ReadGroupZeroReceipt(before);
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawInput), "chaos_sea",
            evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
            evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        Assert.NotNull(resolved.InstanceId);
        var skeleton = before.DeepClone().AsObject();
        skeleton["instances"]!.AsArray().Add(proposed.DeepClone());
        skeleton["nextInstanceOrdinal"] = proposed["ordinal"]!.GetValue<int>() + 1;
        // This accepted schema1 skeleton isolates the new comparison table from lifecycle/setup errors.
        var skeletonState = ReadGroupZeroReceipt(skeleton);
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, skeletonState).Disposition);
        var next = skeleton.DeepClone().AsObject();
        next["schemaVersion"] = 3;
        next["healingSources"] = new JsonArray(); next["healingDecisions"] = new JsonArray();
        next["nextHealingSourceOrdinal"] = 1; next["nextHealingDecisionOrdinal"] = 1;
        next["nextStrainComparisonOrdinal"] = 2;
        next["strainComparisons"] = new JsonArray(new JsonObject
        {
            ["ordinal"] = 1, ["instanceId"] = resolved.InstanceId,
            ["instanceComparisonOrdinal"] = 1, ["witness"] = fixture.Source.DeepClone()
        });
        var beforeJson = before.ToJsonString(); var nextJson = next.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(nextJson, SpiritualWoundOpportunityReceiptState.StatePath);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "group_zero_receipt_parser_desired_success", version, expected.Parents,
            coreWitnessAccepted = true, retainedReceiptAccepted = true, schemaOneSkeletonAccepted = true,
            expected, proposedInstance = proposed.ToJsonString(), before = beforeJson, candidate = nextJson,
            parsed.IsValid, parsed.Issues, fixture = fixtureBefore
        }));
        Assert.Equal(fixtureBefore, SpiritualGroupZeroPressureData.Serialize(fixture));
        Assert.Equal(beforeJson, before.ToJsonString()); Assert.Equal(nextJson, next.ToJsonString());
        // Desired success stays unconditional. The old v2/v3-only table guard is the expected causal RED.
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var state = Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
        foreach (var field in new[] { "sources", "decisions", "closures" })
            Assert.True(JsonNode.DeepEquals(before[field], next[field]), field);
        Assert.Equal(expected.Parents + 1, next["instances"]!.AsArray().Count);
        for (var index = 0; index < expected.Parents; index++)
            Assert.True(JsonNode.DeepEquals(before["instances"]![index], next["instances"]![index]));
        Assert.True(JsonNode.DeepEquals(proposed, next["instances"]![expected.Parents]));
        Assert.Empty(next["healingSources"]!.AsArray()); Assert.Empty(next["healingDecisions"]!.AsArray());
        var row = Assert.Single(next["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), next["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(fixture.Source, row["witness"]));
        Assert.DoesNotContain(next["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == fixture.Source["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(next["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == fixture.Source["sourceId"]!.GetValue<string>());
        var history = WoundHistoryState.Parse(fixture.Retained.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.True(state.HasRetainedPressureComparisons); Assert.True(state.HasExactStrainHistory(history.State!));
        var actual = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        actual["playerSideStrain"] = expected.Actual.Input.NewStrain; actual["exchangeLog"]![0] = fixture.Actual.DeepClone();
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", actual, 46));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state);
        Assert.Equal("appended", append.Disposition);
        var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadGroupZeroReceipt(next)).Disposition);
        Assert.Equal(fixtureBefore, SpiritualGroupZeroPressureData.Serialize(fixture));
        Assert.Equal(beforeJson, before.ToJsonString()); Assert.Equal(nextJson, next.ToJsonString());
        row["witness"]!["maximumSeverityRank"] = 4;
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(state));
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, state).Disposition);
    }

    /// <summary>Uses the existing strict immutable parser, without creating reducer or publication authority.</summary>
    /// <param name="root">Independently authored complete receipt DATA.</param>
    /// <returns>Strictly accepted detached receipt state.</returns>
    private static SpiritualWoundOpportunityReceiptState ReadGroupZeroReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }
}
