using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Persists the owner-derived first checkpoint before its matching pending projection.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialTransport_CommitsExactTwoRoots()
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
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);

        var committed = await capture.CommitC2FirstTransportAsync(lease, interval);

        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Equal("committed", committed.Disposition);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(committed.Checkpoint);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(committed.Pending);
        Assert.Equal(Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint)),
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(pending)),
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Preserves exact signed empty-root bytes as the before-image for both private paths.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialTransport_AcceptsSignedEmptyRoots()
    {
        async Task SeedEmptyRootsAsync(ResourceMaterializationTestContext context)
        {
            await SeedOriginalIntakeBaselinesAsync(context);
            await context.WriteExactJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath,
                "{\"schemaVersion\":1,\"checkpoint\":null}");
            await context.WriteExactJsonAsync(SpiritualWoundDecisionPendingState.StatePath,
                "{\"schemaVersion\":1,\"pending\":null}");
        }

        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedEmptyRootsAsync);
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
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var inventory = capture.ReadC1ImageInventory(lease);
        Assert.Equal("{\"schemaVersion\":1,\"checkpoint\":null}",
            Encoding.UTF8.GetString(inventory.BeforeImages[
                SpiritualWoundCaptureCheckpointState.StatePath].Bytes!));
        Assert.Equal("{\"schemaVersion\":1,\"pending\":null}",
            Encoding.UTF8.GetString(inventory.BeforeImages[
                SpiritualWoundDecisionPendingState.StatePath].Bytes!));

        var result = await capture.CommitC2FirstTransportAsync(lease, interval);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Equal("committed", result.Disposition);
        Assert.True(context.FileSystem.FileExists(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.True(context.FileSystem.FileExists(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Accepts signed empty roots written by the normal BOM-bearing text writer.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialTransport_AcceptsBomEmptyRoots()
    {
        async Task SeedEmptyRootsAsync(ResourceMaterializationTestContext context)
        {
            await SeedOriginalIntakeBaselinesAsync(context);
            await context.FileSystem.WriteFileAtomicAsync(
                SpiritualWoundCaptureCheckpointState.StatePath,
                "{\"schemaVersion\":1,\"checkpoint\":null}");
            await context.FileSystem.WriteFileAtomicAsync(
                SpiritualWoundDecisionPendingState.StatePath,
                "{\"schemaVersion\":1,\"pending\":null}");
        }

        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedEmptyRootsAsync);
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
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        var before = capture.ReadC1ImageInventory(lease);
        Assert.Equal([0xEF, 0xBB, 0xBF], before.BeforeImages[
            SpiritualWoundCaptureCheckpointState.StatePath].Bytes![..3]);

        var result = await capture.CommitC2FirstTransportAsync(lease, interval);

        AssertNoConflictFrameErrors(result.Issues);
        Assert.Equal("committed", result.Disposition);
    }

    /// <summary>
    /// Refuses a private-root edit after capture without writing its derived packet.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialTransport_RejectsChangedBaseline()
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
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        const string changed = "{\"foreign\":true}";
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath, Encoding.UTF8.GetBytes(changed));

        var result = await capture.CommitC2FirstTransportAsync(lease, interval);

        Assert.Equal("blocked", result.Disposition);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(changed, Encoding.UTF8.GetString((await context.FileSystem.ReadFileBytesAsync(
            lease, SpiritualWoundCaptureCheckpointState.StatePath))!));
        Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Refuses to write either private root after its capture is revoked during a baseline read.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2InitialTransport_RevocationDuringReadCannotCommit()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false;
        var pendingReads = 0;
        var targetRead = int.MaxValue;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = async path =>
            {
                if (!armed || path != SpiritualWoundDecisionPendingState.StatePath)
                    return;
                if (Interlocked.Increment(ref pendingReads) != targetRead)
                    return;
                entered.TrySetResult();
                await release.Task;
            }
        };
        async Task SeedEmptyRootsAsync(ResourceMaterializationTestContext context)
        {
            await SeedOriginalIntakeBaselinesAsync(context);
            await context.WriteExactJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath,
                "{\"schemaVersion\":1,\"checkpoint\":null}");
            await context.WriteExactJsonAsync(SpiritualWoundDecisionPendingState.StatePath,
                "{\"schemaVersion\":1,\"pending\":null}");
        }

        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedEmptyRootsAsync);
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
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
        armed = true;
        var preflight = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);
        AssertNoConflictFrameErrors(preflight.Issues);
        Assert.NotNull(preflight.Checkpoint);
        targetRead = pendingReads + 1;
        pendingReads = 0;
        var committing = capture.CommitC2FirstTransportAsync(lease, interval);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            capture.Dispose();
        }
        finally { release.TrySetResult(); }

        var result = await committing;
        Assert.NotEqual("committed", result.Disposition);
        Assert.Equal("{\"schemaVersion\":1,\"checkpoint\":null}",
            Encoding.UTF8.GetString((await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!));
        Assert.Equal("{\"schemaVersion\":1,\"pending\":null}",
            Encoding.UTF8.GetString((await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath))!));
    }

    /// <summary>
    /// Classifies uncertain atomic writes by exact bytes and never advances a later decision on a gap.
    /// </summary>
    /// <param name="failure">
    /// The injected checkpoint or pending transport outcome.
    /// </param>
    /// <param name="expected">
    /// Expected disposition after exact read-back.
    /// </param>
    [Theory]
    [InlineData("checkpoint_old", "not_committed")]
    [InlineData("checkpoint_new_then_throw", "committed")]
    [InlineData("checkpoint_revoked_after_write", "repair_required")]
    [InlineData("checkpoint_third", "blocked")]
    [InlineData("pending_old", "repair_required")]
    [InlineData("pending_new_then_throw", "committed")]
    public async Task OriginalSpiritualC2InitialTransport_ResolvesInjectedFailures(
        string failure, string expected)
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
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step?.Interval);
        var originalCount = capture.ReadAllocationCursor(lease);

        async Task FaultingWriteAsync(BookOfEternityClient.Core.FileSystemManager.CanonicalWriteLease held,
            string path, byte[] bytes)
        {
            if (path == SpiritualWoundCaptureCheckpointState.StatePath)
            {
                if (failure == "checkpoint_old")
                    throw new IOException("injected before checkpoint replacement");
                if (failure == "checkpoint_third")
                {
                    await context.FileSystem.WriteFileAtomicBytesAsync(held, path,
                        Encoding.UTF8.GetBytes("{\"unrelated\":true}"));
                    throw new IOException("injected third checkpoint image");
                }
                await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
                if (failure == "checkpoint_new_then_throw")
                    throw new IOException("injected after checkpoint replacement");
                if (failure == "checkpoint_revoked_after_write")
                    capture.Dispose();
                return;
            }
            if (failure == "pending_old")
                throw new IOException("injected before pending replacement");
            await context.FileSystem.WriteFileAtomicBytesAsync(held, path, bytes);
            if (failure == "pending_new_then_throw")
                throw new IOException("injected after pending replacement");
        }

        var result = await capture.CommitC2FirstTransportAsync(lease, interval, FaultingWriteAsync);

        Assert.Equal(expected, result.Disposition);
        var checkpointBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBytes = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        if (expected == "not_committed")
        {
            Assert.Null(checkpointBytes);
            Assert.Null(pendingBytes);
        }
        else if (expected == "blocked")
        {
            Assert.Equal("{\"unrelated\":true}", Encoding.UTF8.GetString(checkpointBytes!));
            Assert.Null(pendingBytes);
        }
        else
        {
            Assert.Equal(Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(
                Assert.IsType<SpiritualWoundCaptureCheckpointState>(result.Checkpoint))),
                checkpointBytes);
            if (expected == "repair_required")
                Assert.Null(pendingBytes);
            else
                Assert.Equal(Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(
                    Assert.IsType<SpiritualWoundDecisionPendingState>(result.Pending))),
                    pendingBytes);
        }
        if (expected == "committed")
            Assert.Equal(originalCount, capture.ReadAllocationCursor(lease));
        else
            Assert.False(capture.IsCurrentOwner);
        if (expected == "repair_required")
        {
            var fresh = new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance);
            var replayed = await fresh.ReplayInitialSpiritualCheckpointAsync(lease,
                Assert.IsType<SpiritualWoundCaptureCheckpointState>(result.Checkpoint));
            AssertNoConflictFrameErrors(replayed.Issues);
            using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
            Assert.Equal(originalCount, cold.ReadAllocationCursor(lease));
            Assert.Equal(SpiritualWoundDecisionPendingState.SerializeCanonical(
                    Assert.IsType<SpiritualWoundDecisionPendingState>(result.Pending)),
                SpiritualWoundDecisionPendingState.SerializeCanonical(
                    Assert.IsType<SpiritualWoundDecisionPendingState>(replayed.Pending)));
        }
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
