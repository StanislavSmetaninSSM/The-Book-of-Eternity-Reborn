using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Saves finite-use effect expiration in the final C2 step and replays the entire common plan.
    /// </summary>
    /// <param name="materialize">
    /// Whether completion also seals a newly inserted wound through live routing.
    /// </param>
    /// <param name="mutation">
    /// Optional removal or reorder of final retained allocations after a valid completion.
    /// </param>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "remove")]
    [InlineData(true, "reorder")]
    public async Task OriginalSpiritualC2Completion_RetainsFinalAllocationsAndRejectsTampering(
        bool materialize, string? mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var submitted = await offered.SubmitDecisionAsync(lease,
            materialize ? OriginalSpiritualWoundDecision(offered.Offer!.OpportunityRef) :
                JsonSerializer.SerializeToElement(new
                {
                    opportunityRef = offered.Offer!.OpportunityRef, decision = "none"
                }), materialize ? "Чужое давление надломило волю хранителя." : null);
        AssertNoConflictFrameErrors(submitted.Issues);
        Assert.Equal("completed_unpublished", submitted.Disposition);
        using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(submitted.Session);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(completed, "_capture"));
        var journal = capture.ReadAllocationJournal(lease).ToJsonString();
        var cursor = capture.ReadAllocationCursor(lease);
        var checkpointBytes = (await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!;
        var pendingBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var checkpoint = JsonNode.Parse(checkpointBytes)!.AsObject();
        var body = checkpoint["checkpoint"]!.AsObject();
        Assert.Equal(cursor, body["allocations"]!.AsArray().Count);
        Assert.Equal(cursor, body["advances"]!.AsArray().Last()!["allocationCount"]!.GetValue<int>());
        var reduced = await completed.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(reduced.Issues);
        var ordinary = reduced.Reduction!;
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(ordinary, ordinary.Resources));
        AssertNoConflictFrameErrors(planned.Issues);
        var expired = Assert.Single(planned.Plan!.EffectIdentityAfterImage["entries"]!.AsArray(),
            row => row!["effectId"]!.GetValue<string>() == "effect_test_bleeding")!;
        Assert.Equal("expired", expired["state"]!.GetValue<string>());
        Assert.Equal("expire", expired["transitions"]!.AsArray().Last()!["kind"]!.GetValue<string>());
        Assert.Contains(expired["transitions"]!.AsArray().Last()!["transitionId"]!.GetValue<string>(), journal);
        var raw = await capture.CompleteOrdinaryReductionAsync(lease);
        Assert.Same(raw.Reduction, (await capture.CompleteOrdinaryReductionAsync(lease)).Reduction);
        Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString());
        Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
        var registeredPaths = Assert.IsType<SpiritualOriginalDraftInputs>(
            OriginalCaptureField(capture, "_draftInputs")).PathInventory;
        completed.Dispose();
        if (mutation is not null)
        {
            var rows = body["allocations"]!.AsArray();
            var initialCount = body["initialAllocationCount"]!.GetValue<int>();
            Assert.True(rows.Count >= initialCount + 2);
            if (mutation == "remove")
                rows.RemoveAt(rows.Count - 1);
            else
            {
                var last = rows[^1]!.DeepClone();
                rows[^1] = rows[^2]!.DeepClone();
                rows[^2] = last;
            }
            for (var index = 0; index < rows.Count; index++)
                rows[index]!["ordinal"] = index;
            body["advances"]!.AsArray().Last()!["allocationCount"] = rows.Count;
            body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
            var parsed = SpiritualWoundCaptureCheckpointState.Parse(checkpoint.ToJsonString(),
                SpiritualWoundCaptureCheckpointState.StatePath,
                registeredPaths);
            Assert.True(parsed.IsValid);
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath, Encoding.UTF8.GetBytes(checkpoint.ToJsonString()));
            var rejected = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .ReplaySavedSpiritualCheckpointAsync(lease);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue => issue.Code is
                "spiritual_c2_saved_layer_replay_failed" or "spiritual_c2_saved_step_mismatch");
            Assert.Equal(pendingBytes, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            return;
        }
        var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(reopened.Session);
        var coldCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(OriginalCaptureField(cold, "_capture"));
        Assert.Equal(journal, coldCapture.ReadAllocationJournal(lease).ToJsonString());
        var replay = await cold.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(replay.Issues);
        var coldPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(replay.Reduction!, replay.Reduction!.Resources));
        AssertNoConflictFrameErrors(coldPlan.Issues);
        Assert.Equal(planned.Plan.PreparedPlanFingerprint, coldPlan.Plan!.PreparedPlanFingerprint);
        var repeated = await cold.ReduceCompletedDecisionsAsync(lease);
        AssertNoConflictFrameErrors(repeated.Issues);
        Assert.True(JsonNode.DeepEquals(replay.ReceiptAfterImage, repeated.ReceiptAfterImage));
        Assert.Equal(journal, coldCapture.ReadAllocationJournal(lease).ToJsonString());
        Assert.Equal(cursor, coldCapture.ReadAllocationCursor(lease));
        Assert.Equal(checkpointBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pendingBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }
}
