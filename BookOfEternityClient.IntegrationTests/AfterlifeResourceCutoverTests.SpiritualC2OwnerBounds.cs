using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Rejects a rehashed saved allocation tail that no decision owner requested.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedReplay_RejectsRehashedExtraAllocationTail()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var root = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        var allocations = body["allocations"]!.AsArray();
        allocations.Add(new JsonObject
        {
            ["ordinal"] = allocations.Count,
            ["kind"] = "utc_time",
            ["owner"] = "cold_replay_forgery",
            ["coordinate"] = "extra_saved",
            ["value"] = "2026-01-01T00:00:00.0000000+00:00"
        });
        body["advances"]![0]!["allocationCount"] = allocations.Count;
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            staged.Checkpoint!.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(root.ToJsonString()));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replay = await fresh.ReplaySavedSpiritualCheckpointAsync(lease);

        Assert.Null(replay.Capture);
        Assert.Contains(replay.Issues,
            issue => issue.Code is "spiritual_c2_saved_layer_replay_failed" or
                "spiritual_c2_saved_step_mismatch");
    }

    /// <summary>
    /// Rejects a rehashed decision suffix larger than the owner-produced step.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedReplay_RejectsRehashedExcessStepDecisions()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var root = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        body["advances"]![0]!["newDecisionFingerprints"]!.AsArray().Add(
            "sha256:" + new string('e', 64));
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            staged.Checkpoint!.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(root.ToJsonString()));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replay = await fresh.ReplaySavedSpiritualCheckpointAsync(lease);

        Assert.Null(replay.Capture);
        Assert.NotEmpty(replay.Issues);
    }

    /// <summary>
    /// Rejects a structurally valid checkpoint claiming more decisions than the real source owner can offer.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedReplay_RejectsRehashedExcessDecisionSuffix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var root = JsonNode.Parse(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        var advances = body["advances"]!.AsArray();
        var priorFingerprint = advances[0]!["resultPendingPacketFingerprint"]!.GetValue<string>();
        var allocationCount = advances[0]!["allocationCount"]!.GetValue<int>();
        for (var ordinal = 2; ordinal <= 65; ordinal++)
        {
            var resultFingerprint = "sha256:" + ordinal.ToString("x64");
            advances.Add(new JsonObject
            {
                ["ordinal"] = ordinal,
                ["priorPendingPacketFingerprint"] = priorFingerprint,
                ["inputChanges"] = new JsonArray(),
                ["newDecisionFingerprints"] = new JsonArray(
                    JsonValue.Create("sha256:" + (ordinal + 1000).ToString("x64"))),
                ["allocationCount"] = allocationCount,
                ["resultPendingPacketFingerprint"] = resultFingerprint
            });
            priorFingerprint = resultFingerprint;
        }
        body["committedAdvance"] = advances.Count;
        body["expectedPendingPacketFingerprint"] = priorFingerprint;
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            staged.Checkpoint!.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        var forgedBytes = Encoding.UTF8.GetBytes(root.ToJsonString());
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath, forgedBytes);
        var oldPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replay = await fresh.ReplaySavedSpiritualCheckpointAsync(lease);
        var repair = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).RepairSpiritualPendingAsync(lease);

        Assert.Null(replay.Capture);
        Assert.Contains(replay.Issues,
            issue => issue.Code == "spiritual_c2_next_source_missing");
        Assert.Equal("blocked", repair.Disposition);
        Assert.Null(repair.Capture);
        Assert.Equal(forgedBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(oldPending, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }
}
