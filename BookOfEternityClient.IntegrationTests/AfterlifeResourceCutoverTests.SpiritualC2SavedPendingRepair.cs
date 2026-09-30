using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Commits a GM decision checkpoint before its derived pending projection.
    /// </summary>
    /// <param name="materialize">
    /// Whether the saved decision creates a wound or explicitly declines it.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC2SavedTransport_CommitsOwnedSuccessorPair(bool materialize)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var oldCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var oldPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var prepared = await StageC2SavedDecisionAsync(context, lease, materialize);
        Assert.NotNull(prepared.Checkpoint);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var classified = await fresh.ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);

        var result = await owner.CommitC2SavedTransportAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Equal("committed", result.Disposition);
        Assert.Equal(1, result.Checkpoint!.CommittedAdvance);
        Assert.NotEqual(oldCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.NotEqual(oldPending, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
        var reopened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifySpiritualPendingAsync(lease);
        Assert.Equal("match", reopened.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(reopened.Capture);
    }

    /// <summary>
    /// Resolves an uncertain checkpoint replacement only as its exact old or successor bytes.
    /// An uncommitted command blocks a new offer until the exact committed input is restored.
    /// </summary>
    /// <param name="writeOutcome">
    /// Old, new or unrelated third checkpoint bytes left after an injected write failure.
    /// </param>
    [Theory]
    [InlineData("old")]
    [InlineData("new")]
    [InlineData("third")]
    public async Task OriginalSpiritualC2SavedTransport_ClassifiesCheckpointWriteOutcome(
        string writeOutcome)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var oldCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var oldPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var committedCommand = await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath);
        _ = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var classified = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);

        var result = await owner.CommitC2SavedTransportAsync(lease,
            async (held, _, path, bytes) =>
            {
                if (path == SpiritualWoundCaptureCheckpointState.StatePath)
                {
                    if (writeOutcome == "new")
                        await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                    else if (writeOutcome == "third")
                        await context.FileSystem.WriteFileAtomicBytesAsync(held, path,
                            Encoding.UTF8.GetBytes("{}"));
                    throw new IOException("injected uncertain checkpoint write");
                }
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            });

        Assert.Equal(writeOutcome == "new" ? "committed" :
            writeOutcome == "old" ? "not_committed" : "blocked", result.Disposition);
        if (writeOutcome == "old")
        {
            Assert.Equal(oldCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(oldPending, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            var reopened = await new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            using var unexpectedSession = reopened.Session;
            Assert.Equal("blocked", reopened.Disposition);
            Assert.Null(reopened.Session);
            Assert.NotEmpty(reopened.Issues);
            if (committedCommand is null)
                context.FileSystem.DeleteFile(lease, AcceptedMechanicsPlan.WoundCommandPath);
            else
                await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                    AcceptedMechanicsPlan.WoundCommandPath, committedCommand);
            var restored = await new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(restored.Issues);
            Assert.Equal("offer", restored.Disposition);
            using var retry = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
        }
        else if (writeOutcome == "new")
        {
            var reopened = await new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(reopened.Issues);
            Assert.Equal("completed_unpublished", reopened.Disposition);
            using var cold = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(reopened.Session);
            AssertNoConflictFrameErrors((await cold.ReduceCompletedDecisionsAsync(lease)).Issues);
        }
        else
            Assert.NotEmpty(result.Issues);
    }

    /// <summary>
    /// Treats a failed pending replacement as projection repair from the committed checkpoint.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedTransport_RepairsAfterPendingWriteFailure()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var oldPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        _ = await StageC2SavedDecisionAsync(context, lease, materialize: true);
        var classified = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);

        var result = await owner.CommitC2SavedTransportAsync(lease,
            async (held, _, path, bytes) =>
            {
                if (path == SpiritualWoundDecisionPendingState.StatePath)
                    throw new IOException("injected pending failure");
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            });

        Assert.Equal("repair_required", result.Disposition);
        Assert.Equal(oldPending, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
        var repaired = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).RepairSpiritualPendingAsync(lease);
        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Equal("repaired", repaired.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(repaired.Capture);
        Assert.Equal(1, result.Checkpoint!.CommittedAdvance);
        AssertNoConflictFrameErrors(await cold.CheckC2CompletedFrontierAsync(lease));
        var journal = cold.ReadAllocationJournal(lease).ToJsonString();
        var reduced = await cold.ReduceCompletedDecisionsCoreAsync(lease, requireDeclines: false);
        AssertNoConflictFrameErrors(reduced.Issues);
        Assert.Equal(journal, cold.ReadAllocationJournal(lease).ToJsonString());
    }

    /// <summary>
    /// Refuses to overwrite a changed pending root after the checkpoint commits.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedTransport_DetectsPendingChangeBeforeProjection()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        _ = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var classified = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
        var changedPending = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"pending\":null}");
        var pendingWrites = 0;

        var result = await owner.CommitC2SavedTransportAsync(lease,
            async (held, _, path, bytes) =>
            {
                if (path == SpiritualWoundDecisionPendingState.StatePath)
                    pendingWrites++;
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                if (path == SpiritualWoundCaptureCheckpointState.StatePath)
                    await context.FileSystem.WriteFileAtomicBytesAsync(held,
                        SpiritualWoundDecisionPendingState.StatePath, changedPending);
            });

        Assert.Equal("repair_required", result.Disposition);
        Assert.Equal(0, pendingWrites);
        Assert.Equal(changedPending, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
        var repaired = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).RepairSpiritualPendingAsync(lease);
        Assert.Equal("repaired", repaired.Disposition);
        AssertNoConflictFrameErrors(repaired.Issues);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(repaired.Capture);
    }

    /// <summary>
    /// Refuses a confirmed private pair when an immutable origin witness changed during transport.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedTransport_BlocksChangedOriginAfterPendingWrite()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        _ = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var classified = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifyInitialSpiritualPendingAsync(lease);
        Assert.Equal("match", classified.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);

        var result = await owner.CommitC2SavedTransportAsync(lease,
            async (held, _, path, bytes) =>
            {
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                if (path == SpiritualWoundDecisionPendingState.StatePath)
                    await context.FileSystem.WriteFileAtomicBytesAsync(held,
                        LiveTurnPreparationService.TurnRequestPath, Encoding.UTF8.GetBytes("{}"));
            });

        Assert.Equal("blocked", result.Disposition);
        Assert.NotEmpty(result.Issues);
    }

    /// <summary>
    /// Replays a saved checkpoint to restore its missing derived pending packet.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2SavedPendingRepair_RestoresAdvancedProjection()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var staged = await StageC2SavedDecisionAsync(context, lease, materialize: false);
        var checkpointBytes = Encoding.UTF8.GetBytes(
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(staged.Checkpoint!));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath, checkpointBytes);
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var repaired = await fresh.RepairSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Equal("repaired", repaired.Disposition);
        using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(repaired.Capture);
        Assert.Equal(checkpointBytes, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(Encoding.UTF8.GetBytes(
            SpiritualWoundDecisionPendingState.SerializeCanonical(repaired.Pending!)),
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
        var matched = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).ClassifySpiritualPendingAsync(lease);
        Assert.Equal("match", matched.Disposition);
        using var reopened = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(matched.Capture);
    }
}
