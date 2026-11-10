using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LifecycleSealedReadObservationTests
{
    /// <summary>An actual old pass cannot import its read origin into a new observation epoch on the same timer window.</summary>
    [Fact]
    public async Task ActualNarrativeOriginCannotMoveToAnotherObservationEpoch()
    {
        using var ordinary = new OrdinaryOutputFixture(_output);
        await ordinary.SeedValidAsync();
        var reads = new LifecycleFixtureReadObservers(false, false);
        var timing = new OperationTimingAccumulator();
        var files = ordinary.CreateFiles(new FileSystemManagerHooks { PassiveCanonicalReads = reads.CanonicalSources, PassiveOperationTimings = timing });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var window = timing.StartWindow();
        using var first = reads.CanonicalSources.BeginObservation(files, window);
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Null(pass.CaptureDiagnostic().Fault);
        first.CloseCapture();
        using var next = reads.CanonicalSources.BeginObservation(files, window);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(files, lease);
        next.CloseCapture();
        Assert.False(next.Capture().Available);
        Assert.DoesNotContain(next.Capture().Events, row => row.Phase is CanonicalReadSourcePhase.NarrativeEvaluation or CanonicalReadSourcePhase.NarrativeRecheck);
        Assert.Contains(next.Capture().Events, row => row.Phase == CanonicalReadSourcePhase.Unavailable);
        Assert.Null(pass.CaptureDiagnostic().Fault); // diagnostic expiry does not change N authority semantics
    }

    /// <summary>Actual producer identity survives fresh leased rechecks without appending to its sealed authority ledger.</summary>
    [Fact]
    public async Task ActualNarrativeCaptureAndFreshRechecksKeepOneExplicitOrigin()
    {
        using var ordinary = new OrdinaryOutputFixture(_output);
        await ordinary.SeedValidAsync();
        var expectedBytes = File.ReadAllBytes(ordinary.Physical(Source));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0x7B, 0x7D }, expectedBytes);
        var reads = new LifecycleFixtureReadObservers(false, false);
        var timing = new OperationTimingAccumulator();
        var files = ordinary.CreateFiles(new FileSystemManagerHooks { PassiveCanonicalReads = reads.CanonicalSources, PassiveOperationTimings = timing });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var window = timing.StartWindow();
        using var observed = reads.CanonicalSources.BeginObservation(files, window);
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync(); // outside DATA owner
        Assert.Null(pass.CaptureDiagnostic().Fault);
        var originalOccurrences = pass.CaptureDiagnostic().Occurrences.Length;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var sourceOwner = files.BeginSourceDataOperationIfNeeded();
        await pass.RequireCurrentAsync(files, lease);
        await pass.RequireCurrentAsync(files, lease);
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.True(capture.Available, capture.Fault);
        var actual = capture.Events.Where(row => row.Path == Source && row.Kind == CanonicalReadEventKind.PhysicalSnapshot).ToArray();
        Assert.Single(actual, row => row.Phase == CanonicalReadSourcePhase.NarrativeEvaluation);
        Assert.Equal(2, actual.Count(row => row.Phase == CanonicalReadSourcePhase.NarrativeRecheck));
        Assert.Single(actual.Select(row => row.InvocationId).Distinct());
        Assert.All(actual, row => { Assert.NotNull(row.InvocationId); Assert.True(row.Physical); Assert.Equal(expectedBytes, row.Bytes); });
        Assert.DoesNotContain(capture.Events, row => row.Phase == CanonicalReadSourcePhase.NarrativeRecheck &&
            row.Kind is CanonicalReadEventKind.PublicInitialAttemptReturned or CanonicalReadEventKind.PublicFinalReturned);
        Assert.Equal(originalOccurrences, pass.CaptureDiagnostic().Occurrences.Length);
        Assert.Null(pass.CaptureDiagnostic().Fault);
    }

    /// <summary>A concurrent ordinary same-path read cannot borrow a real active N producer's identity from its timer.</summary>
    [Fact]
    public async Task ConcurrentUnrelatedReadDuringActualNarrativeRemainsOrdinary()
    {
        using var ordinary = new OrdinaryOutputFixture(_output);
        await ordinary.SeedValidAsync();
        var reads = new LifecycleFixtureReadObservers(false, false);
        var timing = new OperationTimingAccumulator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = 0;
        var files = ordinary.CreateFiles(new FileSystemManagerHooks
        {
            PassiveCanonicalReads = reads.CanonicalSources, PassiveOperationTimings = timing,
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (path == Source && Interlocked.Exchange(ref blocked, 1) == 0) { entered.SetResult(); await release.Task; }
            }
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var window = timing.StartWindow();
        using var observed = reads.CanonicalSources.BeginObservation(files, window);
        var actual = validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Exception? primary = null;
        ValidationService.NarrativePolicyPass? pass = null;
        try
        {
            await Task.WhenAny(entered.Task, actual);
            Assert.True(entered.Task.IsCompleted, "The real N source-open cut must be reached before completion.");
            await entered.Task;
            Assert.Equal("{}", await files.ReadFileAsync(Source));
            var unrelated = observed.Capture().Events.Where(row => row.Path == Source && row.Kind == CanonicalReadEventKind.PhysicalSnapshot).ToArray();
            var row = Assert.Single(unrelated);
            Assert.Equal(CanonicalReadSourcePhase.Ordinary, row.Phase); Assert.Null(row.InvocationId);
            Assert.True(row.EvaluationStarted > row.EvaluationCompleted);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            release.TrySetResult();
            try { pass = await actual; }
            catch (Exception error) { primary ??= error; }
            finally { pass?.Dispose(); }
        }
        if (primary is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        Assert.NotNull(pass);
        Assert.Null(pass.CaptureDiagnostic().Fault);
        observed.CloseCapture();
        Assert.Contains(observed.Capture().Events, row => row.Path == Source && row.Kind == CanonicalReadEventKind.PhysicalSnapshot &&
            row.Phase == CanonicalReadSourcePhase.NarrativeEvaluation && row.InvocationId is not null);
    }

    /// <summary>A copied physical snapshot is not a successful final public return when its existing callback fails.</summary>
    [Fact]
    public async Task SnapshotThenCallbackFaultDoesNotInventFinalReturn()
    {
        var reads = new LifecycleFixtureReadObservers();
        var failure = new IOException("actual post-read callback failure");
        reads.ReturnedObserver = _ => throw failure;
        using var fixture = new ReadFixture(reads, _output);
        using var window = fixture.Timings.StartWindow();
        using var observed = reads.CanonicalSources.BeginObservation(fixture.Files, window);
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Files.ReadFileAsync(Source)));
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.Contains(capture.Events, row => row.Kind == CanonicalReadEventKind.PhysicalSnapshot);
        Assert.Contains(capture.Events, row => row.Kind == CanonicalReadEventKind.ReaderReturned);
        Assert.Contains(capture.Events, row => row.Kind == CanonicalReadEventKind.Fault);
        Assert.DoesNotContain(capture.Events, row => row.Kind is CanonicalReadEventKind.PublicInitialAttemptReturned or CanonicalReadEventKind.PublicFinalReturned);
    }

    /// <summary>Pre-cancelled reads produce cancellation DATA without an open, snapshot or successful return.</summary>
    [Fact]
    public async Task CancelledReadHasNoSuccessfulSourceOrPublicReturn()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Files.ReadFileBytesAsync(Source, cancelled.Token));
        observed.CloseCapture();
        var events = observed.Capture().Events;
        Assert.Contains(events, row => row.Kind == CanonicalReadEventKind.Cancelled);
        Assert.DoesNotContain(events, row => row.Kind is CanonicalReadEventKind.InitialOpened or CanonicalReadEventKind.PhysicalSnapshot or CanonicalReadEventKind.PublicFinalReturned);
    }

    /// <summary>Already registered reads and delayed followups retain their late tail without joining a new epoch.</summary>
    /// <param name="followup">Whether the public missing-file followup starts only after the observation cut.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingReadAtCutRetainsLateEventsWithoutJoiningAnotherEpoch(bool followup)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new LifecycleFixtureReadObservers(!followup ? false : true, false);
        if (followup) reads.ReturnedObserver = async _ => { entered.TrySetResult(); await release.Task; };
        using var fixture = new ReadFixture(reads, _output,
            followup ? null : new FileSystemManagerHooks
            { BeforeCanonicalReadOpenAsync = async _ => { entered.TrySetResult(); await release.Task; } });
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        var reading = fixture.Files.ReadFileAsync(followup ? "missing.txt" : Source);
        CanonicalReadActivityAccumulator.ObservationWindow? next = null;
        CanonicalReadObservationSnapshot? nextCapture = null;
        Exception? primary = null;
        string? returned = null;
        try
        {
            await Task.WhenAny(entered.Task, reading);
            Assert.True(entered.Task.IsCompleted, "The actual reader must reach the owned cut before returning.");
            await entered.Task;
            observed.CloseCapture();
            Assert.False(observed.Capture().Available);
            next = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            release.TrySetResult();
            try { returned = await reading; }
            catch (Exception error) { primary ??= error; }
            finally { nextCapture = next?.Capture(); next?.Dispose(); }
        }
        if (primary is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        Assert.Equal(followup ? null : "original", returned);
        Assert.Contains(observed.Capture().Events, row => row.Kind ==
            (followup ? CanonicalReadEventKind.PhysicalAbsence : CanonicalReadEventKind.PhysicalSnapshot));
        Assert.False(observed.Capture().Available);
        Assert.NotNull(nextCapture); Assert.Empty(nextCapture.Events);
    }
}
