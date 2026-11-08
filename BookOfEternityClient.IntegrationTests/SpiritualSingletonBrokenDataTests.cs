using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap independent clear→ordinaryIII→actualIV DATA; no world, live authority, runtime execution or native birth is simulated.</summary>
public sealed class SpiritualSingletonBrokenDataTests(ITestOutputHelper output)
{
    private const PlayerSingletonPressureContour Contour = PlayerSingletonPressureContour.ReachingBroken;

    /// <summary>Both complete existing parents prove all noncomparison prerequisites before the causal intrinsic gate.</summary>
    /// <param name="rank">Independent expected parent severity, never inferred from the issued proof.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void PlayerSingletonBroken_CompleteParentAndArithmeticPrerequisites(int rank) =>
        SpiritualSingletonIvPlayerDataTests.AssertPrerequisites(SpiritualSingletonIvPlayerDataTests.Create(rank, Contour), rank, Contour);

    /// <summary>Desired success at both parent ranks: the unchanged below-broken intrinsic inequality must fail naturally, after valid complete DATA.</summary>
    /// <param name="rank">Independent III/IV complete existing parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void PlayerSingletonBroken_VersionThreeWitnessAndIntrinsicSucceed(int rank)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(rank, Contour);
        SpiritualSingletonIvPlayerDataTests.AssertPrerequisites(fixture, rank, Contour);
        var source = fixture.Source; var before = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_broken_intrinsic_v3", rank, error = error?.ToString(),
            sourceJson = before, actual, comparisonReturned = retained is not null,
            rawInputBase64 = Convert.ToBase64String(fixture.RawInput) }));
        Assert.Equal(before, source.ToJsonString());
        if (error is not null) Assert.Null(retained);
        Assert.Null(error); // Desired success, not an expected-exception/refusal test.
        Assert.Equal(SpiritualSingletonIvPlayerDataTests.ActualCalculation(Contour), actual);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(3, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal(rank, data.ParentWound.Severity.Rank);
        Assert.Equal(SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(Contour), data.OrdinaryCalculation);
        Assert.Equal(actual, data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId);
        Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual);
        Assert.Equal(data.Version, direct.Version); Assert.Equal(rank, direct.ParentWound.Severity.Rank);
        Assert.Equal(data.ActualCalculation, direct.ActualCalculation);
        Assert.Equal(data.OrdinaryCalculation, direct.OrdinaryCalculation);
        data.ReadActualExchange()["after"]!["playerSideStrain"] = "clear";
        data.ReadOrdinaryAfter(fixture.Actual)["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Future GREEN only: every changed join first accepts the complete cap-positive baseline, preserving exact parent/History/log separation.</summary>
    /// <param name="rank">Independent complete retained parentIII/IV.</param>
    /// <param name="change">Exact control, History drift, otherwise-valid parent drift, or accepted-log drift.</param>
    [Theory]
    [InlineData(3, "exact")] [InlineData(3, "history-drift")] [InlineData(3, "parent-drift")] [InlineData(3, "log-drift")]
    [InlineData(4, "exact")] [InlineData(4, "history-drift")] [InlineData(4, "parent-drift")] [InlineData(4, "log-drift")]
    public void PlayerSingletonBrokenReceipt_PreservesExactParentHistoryAndLog(int rank, string change)
    {
        var fixture = SpiritualSingletonIvPlayerDataTests.Create(rank, Contour);
        SpiritualSingletonIvPlayerDataTests.AssertPrerequisites(fixture, rank, Contour);
        AssertSupportedSource(fixture.Source, rank);
        var root = SpiritualExistingPressureTestData.CreateLedger(fixture.Source).Root;
        // Reauthor the existing declined-ledger shape's start coordinates before sealing, as in the guardian receipt fixture.
        var instance = root["instances"]![0]!.AsObject(); var evidence = fixture.Source["turnEvidence"]!;
        instance["displayConflictId"] = fixture.Source["conflictId"]!.DeepClone();
        instance["realm"] = evidence["realm"]!.DeepClone();
        instance["startSessionId"] = evidence["sessionId"]!.DeepClone();
        instance["startRequestId"] = evidence["requestId"]!.DeepClone();
        instance["startSnapshotToken"] = evidence["snapshotToken"]!.DeepClone();
        instance["startTurn"] = evidence["turn"]!.DeepClone();
        instance["baselineConflictFingerprint"] = fixture.Source["conflictBeforeFingerprint"]!.DeepClone();
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..]; instance["instanceFingerprint"] = hash;
        root["sources"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        root["decisions"]![0]!["instanceId"] = instance["instanceId"]!.DeepClone();
        SealSourceAndDecision(root);
        // History and expected parent are independent pre-sign factory DATA, not read back from an issued proof.
        var history = fixture.Retained.History.DeepClone().AsObject();
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = fixture.Actual.DeepClone(); active["playerSideStrain"] = "broken";
        var state = ReadReceipt(root);
        Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        AssertJoins(state, fixture, active, logMatches: true);
        switch (change)
        {
            case "exact": break;
            case "history-drift": history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "parent-drift":
                var parent = Assert.Single(fixture.Retained.Parents).DeepClone().AsObject();
                parent["display"]!["description"] = "Иное полное описание той же существующей духовной раны.";
                var changed = WoundMaterializationContract.Parse(parent.ToJsonString(), "singletonBroken.changedParent");
                Assert.True(changed.IsValid, JsonSerializer.Serialize(changed.Issues));
                var originalParent = WoundMaterializationContract.Parse(Assert.Single(fixture.Retained.Parents).ToJsonString(), "singletonBroken.parent");
                Assert.True(originalParent.IsValid, JsonSerializer.Serialize(originalParent.Issues));
                Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(originalParent.Wound!), WoundIdentityState.ComputeSemanticFingerprint(changed.Wound!));
                root["sources"]![0]!["witness"]!["strainPreimage"]!["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
                SealSourceAndDecision(root);
                AssertSupportedSource(root["sources"]![0]!["witness"]!.AsObject(), rank);
                break;
            case "log-drift": active["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "overwhelmed"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var receiptBefore = root.ToJsonString(); var historyBefore = history.ToJsonString(); var activeBefore = active.ToJsonString();
        state = ReadReceipt(root);
        Assert.Equal(change is not ("history-drift" or "parent-drift"), state.HasExactStrainHistory(ReadHistory(history)));
        AssertJoins(state, fixture, active, logMatches: change != "log-drift");
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    private static void AssertJoins(SpiritualWoundOpportunityReceiptState state, SpiritualGuardianAttributionFixture fixture,
        JsonObject active, bool logMatches)
    {
        foreach (var turn in new[] { 46, 47 })
            Assert.Equal(logMatches ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
                SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch, state.MatchRetainedPressureHistory("chaos_sea", active, turn));
        foreach (var summaryDrift in new[] { false, true })
        {
            var recovered = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", fixture.Source["conflictId"]!.GetValue<string>(),
                active["exchangeLog"]![0]!.AsObject(), summaryDrift);
            if (logMatches) Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], recovered));
            else Assert.Null(recovered);
        }
    }

    private static void AssertSupportedSource(JsonObject source, int rank)
    {
        var before = source.ToJsonString();
        Assert.Equal(SpiritualSingletonIvPlayerDataTests.ActualCalculation(Contour),
            SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison));
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(comparison).Version);
        Assert.Equal(rank, comparison.ParentWound.Severity.Rank); Assert.Single(comparison.Parents);
        Assert.Equal(SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(Contour), comparison.OrdinaryCalculation);
        Assert.Equal(before, source.ToJsonString());
    }

    private static void SealSourceAndDecision(JsonObject root)
    {
        var source = root["sources"]![0]!["witness"]!.AsObject(); SpiritualExistingPressureTestData.Seal(source);
        var decision = root["decisions"]![0]!.AsObject();
        decision["sourceId"] = source["sourceId"]!.DeepClone(); decision["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
        decision["sourceWitness"] = source.DeepClone();
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
    }

    private static SpiritualWoundOpportunityReceiptState ReadReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }

    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<WoundHistoryState>(parsed.State);
    }
}
