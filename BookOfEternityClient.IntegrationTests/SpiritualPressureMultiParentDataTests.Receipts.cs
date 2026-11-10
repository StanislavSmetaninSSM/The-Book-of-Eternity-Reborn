using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressureMultiParentDataTests
{
    /// <summary>Every N3/N4 parent has an independent exact History anchor, while actual-log agreement remains a separate check.</summary>
    /// <param name="count">Complete parent count retained even after actual strain saturates.</param>
    /// <param name="change">Exact baseline, a single valid History drift, a lawful parent drift, or accepted actual-log drift.</param>
    /// <param name="index">Independently changed parent position, or minus one for an unchanged parent list.</param>
    [Theory]
    [InlineData(3, "exact", -1)]
    [InlineData(3, "history", 0)] [InlineData(3, "history", 1)] [InlineData(3, "history", 2)]
    [InlineData(3, "parent", 0)] [InlineData(3, "parent", 1)] [InlineData(3, "parent", 2)]
    [InlineData(3, "log", -1)]
    [InlineData(4, "exact", -1)]
    [InlineData(4, "history", 0)] [InlineData(4, "history", 1)] [InlineData(4, "history", 2)] [InlineData(4, "history", 3)]
    [InlineData(4, "parent", 0)] [InlineData(4, "parent", 1)] [InlineData(4, "parent", 2)] [InlineData(4, "parent", 3)]
    [InlineData(4, "log", -1)]
    public void MultiParentReceipt_RequiresEveryHistoryAnchorAndActualLog(int count, string change, int index)
    {
        var fixture = SpiritualPressureMultiParentData.Create(count); _ = ReadSupportedMultiParent(fixture);
        var root = CreateMultiReceipt(fixture);
        var history = fixture.Retained.History.DeepClone().AsObject(); // Independent authored History, not issued comparison DATA.
        var active = fixture.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = fixture.Actual.DeepClone(); active["playerSideStrain"] = "broken";
        var state = ReadMultiReceipt(root);
        Assert.Single(root["sources"]!.AsArray()); Assert.Single(root["decisions"]!.AsArray());
        Assert.False(root.ContainsKey("strainComparisons")); Assert.True(state.HasRetainedPressureComparisons);
        Assert.True(state.HasExactStrainHistory(ReadMultiHistory(history)));
        AssertMultiReceiptJoins(state, fixture, active, true);
        switch (change)
        {
            case "exact": break;
            case "history": history["transitions"]![index]!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            case "parent":
                var parent = fixture.Retained.Parents[index].DeepClone().AsObject();
                parent["display"]!["description"] = "Иное полное допустимое описание существующей духовной раны.";
                AssertValidMultiParent(parent);
                var original = WoundMaterializationContract.Parse(fixture.Retained.Parents[index].ToJsonString(), "multiParent.receiptOriginal").Wound!;
                var changed = WoundMaterializationContract.Parse(parent.ToJsonString(), "multiParent.receiptChanged").Wound!;
                Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(original), WoundIdentityState.ComputeSemanticFingerprint(changed));
                SetMultiParent(root["sources"]![0]!["witness"]!.AsObject(), index, parent); SealMultiSourceAndDecision(root);
                Assert.Equal(SpiritualPressureMultiParentData.ActualCalculation,
                    SpiritualWoundSourceWitness.ValidateAndReadStrainData(root["sources"]![0]!["witness"]!.AsObject(), out var changedData));
                Assert.Equal(6, changedData!.Version); Assert.Equal(count, changedData.Parents.Count);
                Assert.Equal(SpiritualPressureMultiParentData.OrdinaryCalculation, changedData.OrdinaryCalculation);
                break;
            case "log": active["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "fractured"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var receiptBefore = root.ToJsonString(); var historyBefore = history.ToJsonString(); var activeBefore = active.ToJsonString();
        state = ReadMultiReceipt(root);
        Assert.Equal(change is "exact" or "log", state.HasExactStrainHistory(ReadMultiHistory(history)));
        AssertMultiReceiptJoins(state, fixture, active, change != "log");
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString()); Assert.Equal(activeBefore, active.ToJsonString());
    }

    /// <summary>Complete valid v6 DATA and all parent History anchors cannot counterfeit the private C3 source-owner pair.</summary>
    /// <param name="forgedPair">False omits the owner proof; true supplies a structurally constructed but unissued object.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MultiParentReceipt_NoneRequiresAuthenticatedC3Pair(bool forgedPair)
    {
        var fixture = SpiritualPressureMultiParentData.Create(4); _ = ReadSupportedMultiParent(fixture);
        var root = CreateMultiReceipt(fixture); var state = ReadMultiReceipt(root);
        Assert.True(state.HasExactStrainHistory(ReadMultiHistory(fixture.Retained.History)));
        var source = fixture.Source; var evidence = source["turnEvidence"]!;
        var staged = new JsonObject
        {
            ["opportunityRef"] = "opportunity_multi_parent_declined", ["sourceId"] = source["sourceId"]!.DeepClone(),
            ["decisionFingerprint"] = "", ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0, ["decision"] = "none",
            ["selectedSeverityRank"] = null, ["woundDraftBase64"] = null, ["woundDraftFingerprint"] = null
        };
        staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(staged, "staged_decision", "decisionFingerprint");
        var packet = new JsonObject
        {
            ["sources"] = new JsonArray(source.DeepClone()), ["stagedDecisions"] = new JsonArray(staged),
            ["cursor"] = new JsonObject { ["nextSourceOrdinal"] = 1 }, ["realm"] = evidence["realm"]!.DeepClone(),
            ["sessionId"] = evidence["sessionId"]!.DeepClone(), ["requestId"] = evidence["requestId"]!.DeepClone(),
            ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(), ["turn"] = evidence["turn"]!.DeepClone(),
            ["originalSnapshotFingerprint"] = evidence["originalSnapshotFingerprint"]!.DeepClone(),
            ["conflictInstanceRef"] = root["instances"]![0]!["instanceId"]!.DeepClone()
        };
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(new JsonObject
            {
                ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject { ["conflictId"] = "multi_parent_data" }
            }.ToJsonString())), evidence["originalSnapshotFingerprint"]!.GetValue<string>());
        // Arbitrary objects intentionally cannot satisfy the capture's private issuance key.
        var pair = forgedPair ? new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair(
            new object(), signed, packet, null, new object()) : null;
        var before = packet.ToJsonString(); var fixtureBefore = SpiritualPressureMultiParentData.Serialize(fixture);
        var result = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, authenticatedPair: pair);
        Assert.Null(result.AfterImage); Assert.Null(result.Proof);
        Assert.Equal("spiritual_c3_materialization_owner_pair_missing", Assert.Single(result.Issues).Code);
        Assert.Equal(before, packet.ToJsonString()); Assert.Equal(fixtureBefore, SpiritualPressureMultiParentData.Serialize(fixture));
    }

    /// <summary>Authors only the existing declined-ledger shape, then installs the complete independent v6 source without a singular accessor.</summary>
    private static JsonObject CreateMultiReceipt(SpiritualPressureMultiParentFixture fixture)
    {
        var shapeSource = fixture.Source.DeepClone().AsObject(); Assert.True(shapeSource.Remove("strainPreimage"));
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
        SealMultiSourceAndDecision(root); return root;
    }

    /// <summary>Checks same-turn and later retained actual-log identity plus the detached ordinary-after reader.</summary>
    private static void AssertMultiReceiptJoins(SpiritualWoundOpportunityReceiptState state,
        SpiritualPressureMultiParentFixture fixture, JsonObject active, bool logMatches)
    {
        var turn = fixture.Source["turnEvidence"]!["turn"]!.GetValue<int>();
        foreach (var signedTurn in new[] { turn, turn + 1 })
            Assert.Equal(logMatches ? SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact :
                SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Mismatch,
                state.MatchRetainedPressureHistory("chaos_sea", active, signedTurn));
        foreach (var summaryDrift in new[] { false, true })
        {
            var recovered = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", fixture.Source["conflictId"]!.GetValue<string>(),
                active["exchangeLog"]![0]!.AsObject(), summaryDrift);
            if (logMatches) Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], recovered)); else Assert.Null(recovered);
        }
    }

    /// <summary>Reseals one independently authored wire source and its unchanged none-decision join, without owner issuance.</summary>
    private static void SealMultiSourceAndDecision(JsonObject root)
    {
        var source = root["sources"]![0]!["witness"]!.AsObject(); SpiritualExistingPressureTestData.Seal(source);
        var decision = root["decisions"]![0]!.AsObject(); decision["sourceWitness"] = source.DeepClone();
        decision["sourceId"] = source["sourceId"]!.DeepClone(); decision["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
    }

    /// <summary>Requires the production immutable receipt parser before exercising History/log joins.</summary>
    private static SpiritualWoundOpportunityReceiptState ReadMultiReceipt(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
    }
    /// <summary>Requires individually parseable independent History DATA before testing semantic anchors.</summary>
    private static WoundHistoryState ReadMultiHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); return Assert.IsType<WoundHistoryState>(parsed.State);
    }
}
