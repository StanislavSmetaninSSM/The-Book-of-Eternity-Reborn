using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Replays the first C1 offer from checkpoint A after the physical draft changes.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialReplay_RederivesFirstPacketFromOwners()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var step = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var draft = await warm.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        AssertNoConflictFrameErrors(draft.Issues);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(draft.Checkpoint);
        var first = Assert.IsType<SpiritualWoundDecisionPendingState>(draft.Pending);
        warm.Dispose();

        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes("{malformed later draft"));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var replayed = await fresh.ReplayInitialSpiritualCheckpointAsync(lease, checkpoint);

        AssertNoConflictFrameErrors(replayed.Issues);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(replayed.Pending);
        Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(first),
            SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
        Assert.Equal(checkpoint.InitialAllocationCount, cold.ReadAllocationCursor(lease));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Refuses a structurally valid checkpoint whose initial packet marker was forged.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialReplay_RejectsForgedPacketMarker()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var step = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var draft = await warm.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        AssertNoConflictFrameErrors(draft.Issues);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(draft.Checkpoint);
        warm.Dispose();

        var root = JsonNode.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        const string forged = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        body["initialPendingPacketFingerprint"] = forged;
        body["expectedPendingPacketFingerprint"] = forged;
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            checkpoint.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await fresh.ReplayInitialSpiritualCheckpointAsync(lease, parsed.State!);

        Assert.Null(replayed.Pending);
        Assert.Null(replayed.Capture);
        Assert.Contains(replayed.Issues,
            issue => issue.Code == "spiritual_checkpoint_first_packet_mismatch");
    }

    /// <summary>
    /// Refuses an extra self-consistent retained allocation that real owners never requested.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialReplay_RejectsExtraSavedAllocation()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var step = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var draft = await warm.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        AssertNoConflictFrameErrors(draft.Issues);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(draft.Checkpoint);
        warm.Dispose();

        var root = JsonNode.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        var rows = body["allocations"]!.AsArray();
        rows.Add(new JsonObject
        {
            ["ordinal"] = rows.Count,
            ["kind"] = "utc_time",
            ["owner"] = "cold_replay_forgery",
            ["coordinate"] = "extra_initial",
            ["value"] = "2026-01-01T00:00:00.0000000+00:00"
        });
        body["initialAllocationCount"] = rows.Count;
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            checkpoint.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await fresh.ReplayInitialSpiritualCheckpointAsync(lease, parsed.State!);

        Assert.Null(replayed.Capture);
        Assert.Null(replayed.Pending);
        Assert.Contains(replayed.Issues,
            issue => issue.Code == "spiritual_checkpoint_first_packet_mismatch");
    }

    /// <summary>
    /// Reports a missing retained allocation as a replay failure without leaking an owner.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialReplay_RejectsMissingSavedAllocation()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var step = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var draft = await warm.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        AssertNoConflictFrameErrors(draft.Issues);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(draft.Checkpoint);
        warm.Dispose();

        var root = JsonNode.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint))!.AsObject();
        var body = root["checkpoint"]!.AsObject();
        var rows = body["allocations"]!.AsArray();
        Assert.NotEmpty(rows);
        rows.RemoveAt(rows.Count - 1);
        body["initialAllocationCount"] = rows.Count;
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath,
            checkpoint.ReadOriginalDraftInputs().PathInventory);
        Assert.True(parsed.IsValid);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var replayed = await fresh.ReplayInitialSpiritualCheckpointAsync(lease, parsed.State!);

        Assert.Null(replayed.Capture);
        Assert.Null(replayed.Pending);
        Assert.NotEmpty(replayed.Issues);
    }
}
