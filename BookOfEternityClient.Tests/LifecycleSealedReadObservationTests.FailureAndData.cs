using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LifecycleSealedReadObservationTests
{
    /// <summary>The existing absent-public followup remains two actual absences and one initial-attempt notification.</summary>
    [Fact]
    public async Task MissingPublicReadKeepsOriginalFollowupAndFinalReturn()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        Assert.Null(await fixture.Files.ReadFileAsync("missing.txt"));
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.True(capture.Available, capture.Fault);
        Assert.Equal(2, capture.Events.Count(row => row.Kind == CanonicalReadEventKind.PhysicalAbsence));
        Assert.Equal(2, capture.Events.Where(row => row.Kind == CanonicalReadEventKind.PhysicalAbsence).Select(row => row.ReadId).Distinct().Count());
        Assert.DoesNotContain(capture.Events, row => row.Kind == CanonicalReadEventKind.InitialOpened);
        Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.OriginalAttemptReturned));
        Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.PublicInitialAttemptReturned));
        Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.PublicFinalReturned));
    }

    /// <summary>Routed DATA cannot invent physical reads, and changed raw source still refuses the real owner's final fence.</summary>
    [Fact]
    public async Task SupportedSourceDataAndRawDriftKeepPhysicalEvidenceHonest()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        using var owner = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(owner);
        Assert.Equal(fixture.Bytes, await fixture.Files.ReadFileBytesAsync(lease, Source));
        Assert.Equal(fixture.Bytes, await fixture.Files.ReadFileBytesAsync(lease, Source));
        var before = observed.Capture();
        Assert.Single(before.Events.Where(row => row.Path == Source && row.Kind == CanonicalReadEventKind.InitialOpened));
        Assert.Contains(before.Events, row => row.Kind == CanonicalReadEventKind.NonPhysicalData && !row.Physical);
        var changed = System.Text.Encoding.UTF8.GetBytes("changed");
        File.WriteAllBytes(fixture.Files.ResolvePath(Source), changed);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompleteAsync());
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.Contains(capture.Events, row => row.Path == Source && row.Kind == CanonicalReadEventKind.PhysicalSnapshot && row.Bytes is { } bytes && bytes.SequenceEqual(changed));
        Assert.All(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.NonPhysicalData), row => Assert.False(row.Physical));
        Assert.Equal(changed, File.ReadAllBytes(fixture.Files.ResolvePath(Source)));
    }

    /// <summary>A manually created or expired authority sink cannot erase actual copied bytes or acquire N phase.</summary>
    [Fact]
    public async Task ManualExpiredNarrativeSinkRetainsPassiveBytesWithoutAttestation()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        using var manual = new ValidationService.NarrativeInterfaceSourceSession(fixture.Files);
        manual.Dispose();
        Assert.Equal("original", await manual.ReadTextAsync(Source, Source, "manual-unattested"));
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.False(capture.Available);
        Assert.Equal(fixture.Bytes, Assert.Single(capture.Events.Where(row => row.Kind == CanonicalReadEventKind.PhysicalSnapshot)).Bytes);
        Assert.DoesNotContain(capture.Events, row => row.Phase is CanonicalReadSourcePhase.NarrativeEvaluation or CanonicalReadSourcePhase.NarrativeRecheck);
        Assert.NotNull(manual.CaptureDiagnostic().Fault);
    }

    /// <summary>Supported writes invalidate functional DATA but do not masquerade as session-generation replacement.</summary>
    [Fact]
    public async Task OrdinaryMutationDoesNotExpireObservationGeneration()
    {
        using var fixture = new ReadFixture(new LifecycleFixtureReadObservers(false, false), _output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var window = fixture.Timings.StartWindow();
        using var observed = fixture.Reads.CanonicalSources.BeginObservation(fixture.Files, window);
        Assert.Equal("original", await fixture.Files.ReadFileAsync(lease, Source));
        await fixture.Files.WriteFileAtomicAsync(lease, Source, "changed");
        Assert.Equal("changed", await fixture.Files.ReadFileAsync(lease, Source));
        observed.CloseCapture();
        Assert.True(observed.Capture().Available, observed.Capture().Fault);
        Assert.Equal(2, observed.Capture().Events.Count(row => row.Kind == CanonicalReadEventKind.InitialOpened));
    }

    /// <summary>The actual already-fresh DFR producer remains physical inside an eligible DATA owner and never acquires N ownership.</summary>
    [Fact]
    public async Task ActualDebugPrefixInsideEligibleOwnerRetainsPhysicalSources()
    {
        using var ordinary = new OrdinaryReasoningFixture(_output);
        await ordinary.SeedValidAsync(); await ordinary.PrepareOriginalAsync();
        var reads = new LifecycleFixtureReadObservers(false, false);
        var timing = new OperationTimingAccumulator();
        var files = ordinary.CreateFiles(new FileSystemManagerHooks { PassiveCanonicalReads = reads.CanonicalSources, PassiveOperationTimings = timing });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var owner = files.BeginSourceDataOperationIfNeeded();
        using var window = timing.StartWindow();
        using var observed = reads.CanonicalSources.BeginObservation(files, window);
        using var pass = await validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Null(pass.CaptureDiagnostic().Fault);
        Assert.Equal("NeedsActorDependencies", pass.Disposition);
        observed.CloseCapture();
        var capture = observed.Capture();
        Assert.True(capture.Available, capture.Fault);
        Assert.Equal(2, capture.Events.Count(row => row.Path == OrdinaryReasoningFixture.DebugPath && row.Kind == CanonicalReadEventKind.PhysicalSnapshot));
        Assert.DoesNotContain(capture.Events, row => row.Phase is CanonicalReadSourcePhase.NarrativeEvaluation or CanonicalReadSourcePhase.NarrativeRecheck);
    }
}
