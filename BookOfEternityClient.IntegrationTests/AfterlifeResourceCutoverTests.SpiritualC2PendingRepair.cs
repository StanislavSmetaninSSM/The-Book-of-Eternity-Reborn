using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Restores only the derived projection, then treats the exact pair as a no-write replay.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingRepair_RepairsMissingProjectionAndMatchesWithoutWrite()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var repaired = await fresh.RepairInitialSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Equal("repaired", repaired.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(repaired.Capture);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(Encoding.UTF8.GetBytes(
            SpiritualWoundDecisionPendingState.SerializeCanonical(repaired.Pending!)),
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
        AssertNoConflictFrameErrors(await cold.CheckRetainedInputsAsync(lease));
        cold.Dispose();
        var replay = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var writes = 0;

        var matched = await replay.RepairInitialSpiritualPendingAsync(lease,
            (_, _, _, _) => { writes++; throw new InvalidOperationException("must not write"); });

        AssertNoConflictFrameErrors(matched.Issues);
        Assert.Equal("match", matched.Disposition);
        Assert.Equal(0, writes);
        using var matchedCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(matched.Capture);
    }

    /// <summary>
    /// Replaces a malformed pending root only after deriving the first packet from the checkpoint.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingRepair_ReplacesMalformedProjection()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath, Encoding.UTF8.GetBytes("{broken"));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var repaired = await fresh.RepairInitialSpiritualPendingAsync(lease);

        AssertNoConflictFrameErrors(repaired.Issues);
        Assert.Equal("repaired", repaired.Disposition);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(repaired.Capture);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(Encoding.UTF8.GetBytes(
            SpiritualWoundDecisionPendingState.SerializeCanonical(repaired.Pending!)),
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Resolves old, new and third-image atomic write outcomes by physical read-back.
    /// </summary>
    /// <param name="writeOutcome">
    /// Simulated old, new or unrelated third image left by the pending transport.
    /// </param>
    [Theory]
    [InlineData("old")]
    [InlineData("new")]
    [InlineData("third")]
    public async Task OriginalSpiritualC2PendingRepair_ClassifiesAtomicWriteOutcome(string writeOutcome)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var oldPending = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"pending\":null}");
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath, oldPending);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.RepairInitialSpiritualPendingAsync(lease,
            async (held, _, path, bytes) =>
            {
                Assert.Equal(SpiritualWoundDecisionPendingState.StatePath, path);
                if (writeOutcome == "new")
                    await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                else if (writeOutcome == "third")
                    await context.FileSystem.WriteFileAtomicBytesAsync(held, path,
                        Encoding.UTF8.GetBytes("{}"));
                throw new IOException("injected uncertain write");
            });

        Assert.Equal(writeOutcome == "old" ? "not_committed" :
            writeOutcome == "new" ? "repaired" : "blocked", result.Disposition);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        if (writeOutcome == "new")
        {
            AssertNoConflictFrameErrors(result.Issues);
            using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(result.Capture);
            Assert.Equal(Encoding.UTF8.GetBytes(
                SpiritualWoundDecisionPendingState.SerializeCanonical(result.Pending!)),
                await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath));
        }
        else
        {
            Assert.NotEmpty(result.Issues);
            Assert.Null(result.Capture);
            Assert.Equal(writeOutcome == "old" ? oldPending : Encoding.UTF8.GetBytes("{}"),
                await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath));
        }
    }

    /// <summary>
    /// Blocks a changed checkpoint, original request or revoked capture after pending replacement.
    /// </summary>
    /// <param name="interference">
    /// Whether the writer changes checkpoint/request bytes or disposes the replay owner.
    /// </param>
    [Theory]
    [InlineData("checkpoint")]
    [InlineData("request")]
    [InlineData("owner")]
    public async Task OriginalSpiritualC2PendingRepair_BlocksPostWriteInterference(string interference)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.RepairInitialSpiritualPendingAsync(lease,
            async (held, capture, path, bytes) =>
            {
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                if (interference == "checkpoint")
                    await context.FileSystem.WriteFileAtomicBytesAsync(held,
                        SpiritualWoundCaptureCheckpointState.StatePath, Encoding.UTF8.GetBytes("{}"));
                else if (interference == "request")
                    await context.FileSystem.WriteFileAtomicBytesAsync(held,
                        LiveTurnPreparationService.TurnRequestPath, Encoding.UTF8.GetBytes("{}"));
                else
                    capture.Dispose();
            });

        Assert.Equal("blocked", result.Disposition);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.Capture);
    }

    /// <summary>
    /// Rechecks checkpoint after the pending read, which can race an external physical edit.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingRepair_BlocksCheckpointChangeDuringPendingRead()
    {
        ResourceMaterializationTestContext? observed = null;
        var changeOnPendingRead = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (changeOnPendingRead && path == SpiritualWoundDecisionPendingState.StatePath)
                {
                    changeOnPendingRead = false;
                    File.WriteAllBytes(Path.Combine(observed!.RootPath,
                        SpiritualWoundCaptureCheckpointState.StatePath.Replace('/',
                            Path.DirectorySeparatorChar)), Encoding.UTF8.GetBytes("{}"));
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        observed = context;
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.RepairInitialSpiritualPendingAsync(lease,
            async (held, _, path, bytes) =>
            {
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                changeOnPendingRead = true;
            });

        Assert.Equal("blocked", result.Disposition);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.Capture);
        Assert.False(changeOnPendingRead);
    }

    /// <summary>
    /// Rejects byte-different pending adopted only by the post-write cold replay.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PendingRepair_BlocksChangedReplayBaseline()
    {
        ResourceMaterializationTestContext? observed = null;
        FileSystemManager.CanonicalWriteLease? activeLease = null;
        byte[]? writtenPending = null;
        var countAfterWrite = false;
        var checkpointReads = 0;
        var changed = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (countAfterWrite && path == SpiritualWoundCaptureCheckpointState.StatePath &&
                    ++checkpointReads == 3)
                {
                    await observed!.FileSystem.WriteFileAtomicBytesAsync(activeLease!,
                        SpiritualWoundDecisionPendingState.StatePath,
                        writtenPending!.Concat(new byte[] { 0x20 }).ToArray());
                    changed = true;
                }
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        observed = context;
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        activeLease = lease;
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.RepairInitialSpiritualPendingAsync(lease,
            async (held, _, path, bytes) =>
            {
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                writtenPending = bytes;
                countAfterWrite = true;
            });

        Assert.True(changed);
        Assert.Equal("blocked", result.Disposition);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.Capture);
    }
}
