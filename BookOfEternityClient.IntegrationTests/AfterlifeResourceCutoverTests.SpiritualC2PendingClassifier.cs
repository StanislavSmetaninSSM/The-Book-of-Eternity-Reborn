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
    /// Commits one valid private first pair for cold classification controls.
    /// </summary>
    /// <param name="context">
    /// Fresh signed conflict frame receiving the first checkpoint and pending pair.
    /// </param>
    /// <param name="adjustConflict">
    /// Optional fixture correction applied before original capture.
    /// </param>
    private static async Task CommitInitialC2PairAsync(ResourceMaterializationTestContext context,
        Action<JsonObject>? adjustConflict = null)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (adjustConflict is not null)
        {
            var conflict = await ReadProjectedSourceContinuationCandidateAsync(context);
            adjustConflict(conflict);
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
                conflict.ToJsonString());
        }
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
        var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
        Assert.Equal("committed", committed.Disposition);
        AssertNoConflictFrameErrors(committed.Issues);
    }

    /// <summary>
    /// Replays a physically committed first checkpoint and matches its derived pending packet.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_MatchesCommittedPair()
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
        var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
        AssertNoConflictFrameErrors(committed.Issues);
        warm.Dispose();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(classified.Issues);
        Assert.Equal("match", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(committed.Pending!),
            SpiritualWoundDecisionPendingState.SerializeCanonical(classified.Pending!));
        Assert.Equal(committed.Checkpoint!.InitialAllocationCount, cold.ReadAllocationCursor(lease));
    }

    /// <summary>
    /// Derives pending from the committed checkpoint after a simulated second-write failure.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_ReportsMissingProjection()
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
        var committed = await warm.CommitC2FirstTransportAsync(lease, interval,
            async (held, path, bytes) =>
            {
                if (path == SpiritualWoundDecisionPendingState.StatePath)
                    throw new IOException("injected pending gap");
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            });
        Assert.Equal("repair_required", committed.Disposition);
        var checkpointBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(classified.Issues);
        Assert.Equal("repair_required", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(committed.Pending!),
            SpiritualWoundDecisionPendingState.SerializeCanonical(classified.Pending!));
        Assert.Equal(checkpointBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Refuses to create an original owner from a physically present pending root alone.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_BlocksPendingWithoutCheckpoint()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await using var foreign = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(foreign);
        await using var foreignLease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var activePending = Assert.IsType<byte[]>(await foreign.FileSystem.ReadFileBytesAsync(
            foreignLease, SpiritualWoundDecisionPendingState.StatePath));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        Assert.Equal("no_checkpoint", (await fresh.ClassifyInitialSpiritualPendingAsync(lease)).Disposition);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath,
            Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"pending\":null}"));
        Assert.Equal("no_checkpoint", (await fresh.ClassifyInitialSpiritualPendingAsync(lease)).Disposition);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath, activePending);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        Assert.Equal("blocked", classified.Disposition);
        Assert.Contains(classified.Issues, issue => issue.Code == "spiritual_pending_without_checkpoint");
        Assert.Null(classified.Capture);
    }

    /// <summary>
    /// Rejects an independently valid pending packet from another signed original turn.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_DoesNotTrustForgedValidPending()
    {
        await using var target = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await using var foreign = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(target);
        await CommitInitialC2PairAsync(foreign);
        await using var foreignLease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var foreignPending = Assert.IsType<byte[]>(await foreign.FileSystem.ReadFileBytesAsync(
            foreignLease, SpiritualWoundDecisionPendingState.StatePath));
        await using var lease = await target.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var targetPending = Assert.IsType<byte[]>(await target.FileSystem.ReadFileBytesAsync(
            lease, SpiritualWoundDecisionPendingState.StatePath));
        Assert.NotEqual(targetPending, foreignPending);
        await target.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath, foreignPending);
        var fresh = new ValidationService(target.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(classified.Issues);
        Assert.Equal("repair_required", classified.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        Assert.Equal(targetPending, Encoding.UTF8.GetBytes(
            SpiritualWoundDecisionPendingState.SerializeCanonical(classified.Pending!)));
        Assert.Equal(foreignPending, await target.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Blocks replay when the signed request witness no longer matches the committed checkpoint.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_BlocksChangedSignedOrigin()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            LiveTurnPreparationService.TurnRequestPath, Encoding.UTF8.GetBytes("{}"));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        Assert.Equal("blocked", classified.Disposition);
        Assert.NotEmpty(classified.Issues);
        Assert.Null(classified.Capture);
    }

    /// <summary>
    /// Converts a transient original-witness read failure into a blocked result and permits retry.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_BlocksReplayReadFailureThenRetries()
    {
        var injectReadFailure = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injectReadFailure && path == LiveTurnPreparationService.TurnRequestPath)
                {
                    injectReadFailure = false;
                    throw new IOException("injected original witness read failure");
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var checkpointBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        injectReadFailure = true;

        var blocked = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        Assert.Equal("blocked", blocked.Disposition);
        Assert.Contains(blocked.Issues, issue => issue.Code == "spiritual_checkpoint_replay_read_failed");
        Assert.Null(blocked.Capture);
        Assert.Equal(checkpointBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pendingBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
        var retried = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        AssertNoConflictFrameErrors(retried.Issues);
        Assert.Equal("match", retried.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(retried.Capture);
    }

    /// <summary>
    /// Rejects a checkpoint whose self-declared draft inventory contains an aliased path.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingClassifier_BlocksAliasedDraftInventory()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var bytes = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        var root = Assert.IsType<JsonObject>(JsonNode.Parse(bytes));
        var checkpoint = root["checkpoint"]!.AsObject();
        var image = checkpoint["originalDraftImages"]!.AsArray()[0]!.AsObject();
        image["path"] = image["path"]!.GetValue<string>().ToUpperInvariant();
        checkpoint["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(checkpoint,
            "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(root.ToJsonString()));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);

        Assert.Equal("blocked", classified.Disposition);
        Assert.Contains(classified.Issues, issue => issue.Code == "spiritual_checkpoint_physical_invalid");
        Assert.Null(classified.Capture);
    }
}
