using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPairLowPressureDataTests
{
    /// <summary>Future GREEN only: every A–D receipt accepts first, then exact History anchors, either valid parent drift and actual-log drift remain distinguished.</summary>
    /// <param name="contour">A=0, B=1, C=2, D=3, with the four distributed canonical parent orders.</param>
    /// <param name="change">Exact baseline, first/second History, first/second complete parent, or actual log.</param>
    [Theory]
    [InlineData(0, "exact")]
    [InlineData(0, "history-first")]
    [InlineData(0, "history-second")]
    [InlineData(0, "parent-first")]
    [InlineData(0, "parent-second")]
    [InlineData(0, "log-drift")]
    [InlineData(1, "exact")]
    [InlineData(1, "history-first")]
    [InlineData(1, "history-second")]
    [InlineData(1, "parent-first")]
    [InlineData(1, "parent-second")]
    [InlineData(1, "log-drift")]
    [InlineData(2, "exact")]
    [InlineData(2, "history-first")]
    [InlineData(2, "history-second")]
    [InlineData(2, "parent-first")]
    [InlineData(2, "parent-second")]
    [InlineData(2, "log-drift")]
    [InlineData(3, "exact")]
    [InlineData(3, "history-first")]
    [InlineData(3, "history-second")]
    [InlineData(3, "parent-first")]
    [InlineData(3, "parent-second")]
    [InlineData(3, "log-drift")]
    public void PairLowReceipt_PreservesBothParentsHistoryAndActualLog(int contour, string change)
    {
        var kind = SpiritualPairLowPressureData.Boundary(contour);
        var ranks = contour switch { 0 => (3, 3), 1 => (3, 4), 2 => (4, 3), 3 => (4, 4), _ => throw new ArgumentOutOfRangeException(nameof(contour)) };
        var fixture = SpiritualPairLowPressureData.Create(kind, ranks.Item1, ranks.Item2);
        AssertPrerequisites(fixture, kind, ranks.Item1, ranks.Item2);
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var originalData);
        Assert.Equal(SpiritualPairLowPressureData.Expected(kind).Actual, calculation);
        AssertSupported(fixture, kind, ranks.Item1, ranks.Item2, originalData!);
        // The shared cheap ledger shape has a singular convenience parent reader. Remove v4 for shape only,
        // then install the complete independently authored witness; never call a v4 singular alias.
        var shapeSource = fixture.Source.DeepClone().AsObject(); shapeSource.Remove("strainPreimage");
        var root = SpiritualExistingPressureTestData.CreateLedger(shapeSource).Root;
        root["sources"]![0]!["witness"] = fixture.Source.DeepClone();
        var instance = root["instances"]![0]!.AsObject(); var evidence = fixture.Source["turnEvidence"]!;
        instance["displayConflictId"] = fixture.Source["conflictId"]!.DeepClone(); instance["realm"] = evidence["realm"]!.DeepClone();
        instance["startSessionId"] = evidence["sessionId"]!.DeepClone(); instance["startRequestId"] = evidence["requestId"]!.DeepClone();
        instance["startSnapshotToken"] = evidence["snapshotToken"]!.DeepClone(); instance["startTurn"] = evidence["turn"]!.DeepClone();
        instance["baselineConflictFingerprint"] = fixture.Source["conflictBeforeFingerprint"]!.DeepClone();
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..]; instance["instanceFingerprint"] = hash;
        root["sources"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        root["decisions"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        SealPairLowSourceAndDecision(root);
        var history = fixture.Retained.History.DeepClone().AsObject(); // Independent pre-sign factory History, not issued proof.
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = fixture.Actual.DeepClone();
        active["playerSideStrain"] = SpiritualPairLowPressureData.Expected(kind).Actual.Input.NewStrain;
        var state = ReadPairLowReceipt(root);
        Assert.Single(root["sources"]!.AsArray()); Assert.Single(root["decisions"]!.AsArray());
        Assert.False(root.ContainsKey("strainComparisons")); Assert.True(state.HasRetainedPressureComparisons);
        Assert.True(state.HasExactStrainHistory(ReadPairLowHistory(history)));
        AssertPairLowJoins(state, fixture, active, true);
        switch (change)
        {
            case "exact": break;
            case "history-first": history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "history-second": history["transitions"]![1]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "parent-first": case "parent-second":
                var index = change == "parent-first" ? 0 : 1;
                var parent = fixture.Retained.Parents[index].DeepClone().AsObject();
                parent["display"]!["description"] = "Иное полное описание той же существующей духовной раны.";
                var changed = WoundMaterializationContract.Parse(parent.ToJsonString(), "pairLow.changedParent");
                var original = WoundMaterializationContract.Parse(fixture.Retained.Parents[index].ToJsonString(), "pairLow.parent");
                Assert.True(changed.IsValid, JsonSerializer.Serialize(changed.Issues)); Assert.True(original.IsValid, JsonSerializer.Serialize(original.Issues));
                Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(original.Wound!), WoundIdentityState.ComputeSemanticFingerprint(changed.Wound!));
                root["sources"]![0]!["witness"]!["strainPreimage"]!["parents"]![index]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
                SealPairLowSourceAndDecision(root);
                var source = root["sources"]![0]!["witness"]!.AsObject();
                Assert.Equal(SpiritualPairLowPressureData.Expected(kind).Actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var changedData));
                Assert.Equal(4, changedData!.Version); Assert.Equal(2, changedData.Parents.Count);
                Assert.Equal(SpiritualPairLowPressureData.Expected(kind).Ordinary, changedData.OrdinaryCalculation);
                break;
            case "log-drift": active["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "fractured"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var receiptBefore = root.ToJsonString(); var historyBefore = history.ToJsonString(); var activeBefore = active.ToJsonString();
        state = ReadPairLowReceipt(root);
        Assert.Equal(change is "exact" or "log-drift", state.HasExactStrainHistory(ReadPairLowHistory(history)));
        AssertPairLowJoins(state, fixture, active, change != "log-drift");
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    private static void AssertPairLowJoins(SpiritualWoundOpportunityReceiptState state, PairLowPressureFixture fixture, JsonObject active, bool logMatches)
    {
        foreach (var turn in new[] { 46, 47 })
            Assert.Equal(logMatches ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact : SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
                state.MatchRetainedPressureHistory("chaos_sea", active, turn));
        foreach (var summaryDrift in new[] { false, true })
        {
            var recovered = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", fixture.Source["conflictId"]!.GetValue<string>(), active["exchangeLog"]![0]!.AsObject(), summaryDrift);
            if (logMatches) Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], recovered)); else Assert.Null(recovered);
        }
    }

    private static void SealPairLowSourceAndDecision(JsonObject root)
    {
        var source = root["sources"]![0]!["witness"]!.AsObject(); SpiritualExistingPressureTestData.Seal(source);
        var decision = root["decisions"]![0]!.AsObject(); decision["sourceWitness"] = source.DeepClone();
        decision["sourceId"] = source["sourceId"]!.DeepClone(); decision["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
    }

    private static SpiritualWoundOpportunityReceiptState ReadPairLowReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    private static WoundHistoryState ReadPairLowHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<WoundHistoryState>(parsed.State);
    }
}
