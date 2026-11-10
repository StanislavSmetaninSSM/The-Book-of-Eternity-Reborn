using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Strict guardian-v5 zero-ledger DATA over independently valid lifecycle premises, never a fabricated C3 owner.</summary>
public sealed class SpiritualGuardianZeroPressureReceiptTests(ITestOutputHelper output)
{
    /// <summary>Requires v5 comparison Parse/append/exact replay only after the core source, retained History and schema1 skeleton pass.</summary>
    /// <param name="rank">Independent complete retained guardian III/IV parent rank.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianZero_ReceiptAppendsWithoutDecision(int rank)
    {
        var fixture = SpiritualGuardianZeroPressureTestData.Create(rank);
        var sourceBefore = fixture.Source.ToJsonString(); var retainedBefore = fixture.Retained.Receipts.ToJsonString();
        var rawBefore = fixture.RawOrdinaryBytes.ToArray();
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        Assert.Equal(fixture.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var intrinsic));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(intrinsic);
        Assert.Equal(5, comparison.Version); Assert.Single(comparison.Parents);
        Assert.Equal(fixture.OrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.Equal((2L, 0, 7L, 0), (comparison.OrdinaryCalculation!.TraumaPressure, comparison.OrdinaryCalculation.MaximumSeverityRank,
            comparison.ActualCalculation!.TraumaPressure, comparison.ActualCalculation.MaximumSeverityRank));
        Assert.Equal(rank, comparison.ParentWound.Severity.Rank);
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.ParentWound))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        var before = fixture.Retained.Receipts.DeepClone().AsObject();
        var baseline = ReadReceipt(before);
        var history = WoundHistoryState.Parse(fixture.Retained.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.True(baseline.HasExactStrainHistory(history.State!));
        var evidence = fixture.Source["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(before.ToJsonString())),
            new CanonicalBeforeImage(true, fixture.RawOrdinaryBytes), "chaos_sea",
            evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
            evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.InstanceId);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        var skeleton = before.DeepClone().AsObject();
        skeleton["instances"]!.AsArray().Add(proposed.DeepClone());
        skeleton["nextInstanceOrdinal"] = proposed["ordinal"]!.GetValue<int>() + 1;
        var skeletonState = ReadReceipt(skeleton);
        Assert.Equal(1, skeleton["schemaVersion"]!.GetValue<int>());
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
            phase = "guardian_zero_receipt_parser_desired_success", rank,
            coreWitnessAccepted = true, retainedReceiptAndHistoryAccepted = true, schemaOneSkeletonAccepted = true,
            ordinary = fixture.OrdinaryCalculation, actual = fixture.ActualCalculation,
            proposedInstance = proposed, sourceJson = sourceBefore, beforeJson, nextJson,
            rawOrdinaryBytesBase64 = Convert.ToBase64String(fixture.RawOrdinaryBytes), parsed.IsValid, parsed.Issues
        }));
        Assert.Equal(sourceBefore, fixture.Source.ToJsonString()); Assert.Equal(retainedBefore, fixture.Retained.Receipts.ToJsonString());
        Assert.Equal(rawBefore, fixture.RawOrdinaryBytes); Assert.Equal(beforeJson, before.ToJsonString()); Assert.Equal(nextJson, next.ToJsonString());
        // Desired success is unconditional; the unchanged schema3 whitelist must supply the initial causal RED.
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var state = Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
        foreach (var (field, node) in before)
            if (field is not ("schemaVersion" or "instances" or "nextInstanceOrdinal"))
                Assert.True(JsonNode.DeepEquals(node, next[field]), field);
        var oldInstances = before["instances"]!.AsArray(); var instances = next["instances"]!.AsArray();
        Assert.Equal(oldInstances.Count + 1, instances.Count);
        for (var index = 0; index < oldInstances.Count; index++)
            Assert.True(JsonNode.DeepEquals(oldInstances[index], instances[index]));
        Assert.True(JsonNode.DeepEquals(proposed, instances[^1]));
        Assert.Empty(next["healingSources"]!.AsArray()); Assert.Empty(next["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (next["nextHealingSourceOrdinal"]!.GetValue<int>(), next["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        var row = Assert.Single(next["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), next["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        Assert.Equal(resolved.InstanceId, row["instanceId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(fixture.Source, row["witness"]));
        Assert.DoesNotContain(next["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == fixture.Source["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(next["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == fixture.Source["sourceId"]!.GetValue<string>());
        Assert.True(state.HasRetainedPressureComparisons); Assert.True(state.HasExactStrainHistory(history.State!));
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["oppositionSideStrain"] = "overwhelmed"; active["exchangeLog"]![0] = fixture.Actual.DeepClone();
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            state.MatchRetainedPressureHistory("chaos_sea", active, 46));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], state.ReadRetainedStrainOrdinaryAfter(
            "chaos_sea", active["conflictId"]!.GetValue<string>(), fixture.Actual, true)));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(baseline, state);
        Assert.Equal("appended", append.Disposition);
        var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, ReadReceipt(next)).Disposition);
        Assert.Equal(sourceBefore, fixture.Source.ToJsonString()); Assert.Equal(beforeJson, before.ToJsonString()); Assert.Equal(nextJson, next.ToJsonString());
        row["witness"]!["maximumSeverityRank"] = 4;
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(state));
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(state, state).Disposition);
    }

    /// <summary>Parses independent immutable receipt DATA without a source-only key, live owner or parser bypass.</summary>
    /// <param name="root">Complete independently authored schema1 or schema3 candidate.</param>
    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }
}
