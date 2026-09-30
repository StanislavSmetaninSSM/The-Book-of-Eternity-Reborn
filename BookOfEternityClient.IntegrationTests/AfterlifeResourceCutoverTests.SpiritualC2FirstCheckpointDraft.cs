using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Builds a detached first checkpoint from the same owner that produced its C1 packet.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2FirstCheckpointDraft_ContainsExactOwnerOriginsAndPacket()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        const string dynamicPath = "game_state/custom/c2_checkpoint_original.json";
        await context.WriteExactJsonAsync(dynamicPath, "{\"original\":true}");
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);
        var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);
        AssertNoConflictFrameErrors(offered.Issues);

        var draft = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);

        AssertNoConflictFrameErrors(draft.Issues);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(draft.Checkpoint);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(draft.Pending);
        Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(offered.Pending!),
            SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
        var root = JsonNode.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint))!.AsObject();
        var body = Assert.IsType<JsonObject>(root["checkpoint"]);
        Assert.Equal(0, (int?)body["committedAdvance"]);
        Assert.Empty(Assert.IsType<JsonArray>(body["advances"]));
        Assert.Equal((string?)body["initialPendingPacketFingerprint"],
            (string?)body["expectedPendingPacketFingerprint"]);
        Assert.Equal(capture.ReadAllocationJournal(lease).Count,
            (int?)body["initialAllocationCount"]);
        Assert.Equal(capture.ReadVerifiedSignedC1Origin(lease).SnapshotFingerprint,
            (string?)body["originalSnapshotFingerprint"]);
        Assert.Contains(dynamicPath, checkpoint.ReadOriginalDraftInputs().PathInventory);
        Assert.Equal("{\"original\":true}",
            checkpoint.ReadOriginalDraftInputs().ReadText(dynamicPath));
        var originals = checkpoint.ReadOriginalDraftInputs();
        Assert.Contains(originals.PathInventory,
            path => !originals.ReadImage(path).Existed);
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));

        var originalConflict = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(
            lease, AfterlifeSpiritualConflictState.StatePath));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(originalConflict) + " "));
        var changedConflict = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        Assert.Null(changedConflict.Checkpoint);
        Assert.Contains(changedConflict.Issues,
            issue => issue.Code == "spiritual_first_checkpoint_conflict_changed");
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, originalConflict);

        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            dynamicPath, Encoding.UTF8.GetBytes("{\"original\":false}"));
        var changed = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        Assert.Null(changed.Checkpoint);
        Assert.Null(changed.Pending);
        Assert.NotEmpty(changed.Issues);
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
    }

    /// <summary>
    /// Refuses an initial checkpoint when a missing-audit continuation lacks a saved advance.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2FirstCheckpointDraft_RejectsConsumedMissingSide()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var full = await ReadSourceMissingCandidateAsync(context);
        full["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = 5;
        full["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["after"] = 2;
        var partial = full.DeepClone();
        partial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject()
            .Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            partial.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(1m, 0m).ToJsonString());
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(
            first.Step?.PendingExchange);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(full.ToJsonString()));
        var accepted = await capture.ResumeMissingAuditSideAsync(lease, wait);
        AssertNoConflictFrameErrors(accepted.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            accepted.Step?.Interval);
        var offer = await capture.ComposeC1FirstOfferAsync(lease, interval);
        AssertNoConflictFrameErrors(offer.Issues);
        Assert.NotNull(offer.Pending);

        var checkpoint = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);

        Assert.Null(checkpoint.Checkpoint);
        Assert.Null(checkpoint.Pending);
        Assert.Contains(checkpoint.Issues,
            issue => issue.Code == "spiritual_first_checkpoint_prior_continuation");
    }
}
