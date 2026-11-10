using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianGroupPressureReceiptTests
{
    /// <summary>Valid complete v7 source/receipt DATA and an all-handled none packet cannot replace the capture's private C3 issuance key.</summary>
    /// <param name="forgedPair">Whether an arbitrary-key constructor value is supplied instead of a null pair.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardianGroupReceipt_NoneRequiresAuthenticatedC3Pair(bool forgedPair)
    {
        var inputs = CreateInputs();
        var frame = CreateFrame(inputs, retainAncestry: true);
        var joined = frame.State.HasExactStrainHistory(inputs.History);
        Log("authentication_parsed_receipt_premise", new { joined, receipt = frame.Root });
        Assert.True(joined);
        var source = inputs.Source;
        var staged = new JsonObject
        {
            ["opportunityRef"] = frame.Root["decisions"]!.AsArray().Last()!["opportunityRef"]!.DeepClone(),
            ["sourceId"] = source["sourceId"]!.DeepClone(), ["decisionFingerprint"] = "",
            ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0, ["decision"] = "none", ["selectedSeverityRank"] = null,
            ["woundDraftBase64"] = null, ["woundDraftFingerprint"] = null
        };
        staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(staged, "staged_decision", "decisionFingerprint");
        var evidence = source["turnEvidence"]!;
        var packet = new JsonObject
        {
            ["sources"] = new JsonArray(source.DeepClone()), ["stagedDecisions"] = new JsonArray(staged),
            ["cursor"] = new JsonObject { ["nextSourceOrdinal"] = 1 }, ["realm"] = evidence["realm"]!.DeepClone(),
            ["sessionId"] = evidence["sessionId"]!.DeepClone(), ["requestId"] = evidence["requestId"]!.DeepClone(),
            ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(), ["turn"] = evidence["turn"]!.DeepClone(),
            ["originalSnapshotFingerprint"] = evidence["originalSnapshotFingerprint"]!.DeepClone(),
            ["conflictInstanceRef"] = frame.InstanceId
        };
        var receiptBytes = Encoding.UTF8.GetBytes(inputs.PriorRoot.ToJsonString());
        var conflictBytes = Convert.FromBase64String(inputs.Fixture.RawOrdinaryBase64);
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(true, receiptBytes),
            new CanonicalBeforeImage(true, conflictBytes), Text(evidence["originalSnapshotFingerprint"]));
        // An arbitrary object is intentionally not the capture's private issuance key. No positive authority is forged.
        var pair = forgedPair ? new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair(
            new object(), signed, packet, null, new object()) : null;
        var allHandled = SpiritualWoundDecisionPendingState.AllSourcesHandled(packet);
        Log("authentication_completed_none_packet", new { forgedPair, allHandled, packet,
            receiptBytesBase64 = Convert.ToBase64String(receiptBytes), conflictBytesBase64 = Convert.ToBase64String(conflictBytes),
            signed.SnapshotFingerprint, forgedPairMatches = pair?.Matches(signed, packet) });
        Assert.True(allHandled);
        if (pair is not null) Assert.False(pair.Matches(signed, packet));
        var packetBefore = packet.ToJsonString(); var sourceBefore = source.ToJsonString();
        var priorBefore = inputs.PriorRoot.ToJsonString(); var containingBefore = frame.Root.ToJsonString();
        var signedReceiptBefore = Convert.ToBase64String(signed.Receipt.Bytes!);
        var signedConflictBefore = Convert.ToBase64String(signed.Conflict.Bytes!);
        var result = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, authenticatedPair: pair);
        Log("authentication_strict_refusal", new { forgedPair, result.AfterImage, result.Issues, proofReturned = result.Proof is not null,
            packet, originalReceipt = inputs.PriorRoot, containingReceipt = frame.Root });
        Assert.Null(result.AfterImage); Assert.Null(result.Proof);
        Assert.Equal("spiritual_c3_materialization_owner_pair_missing", Assert.Single(result.Issues).Code);
        Assert.Equal(packetBefore, packet.ToJsonString()); Assert.Equal(sourceBefore, source.ToJsonString());
        Assert.Equal(priorBefore, inputs.PriorRoot.ToJsonString()); Assert.Equal(containingBefore, frame.Root.ToJsonString());
        Assert.Equal(signedReceiptBefore, Convert.ToBase64String(signed.Receipt.Bytes!));
        Assert.Equal(signedConflictBefore, Convert.ToBase64String(signed.Conflict.Bytes!));
    }
}
