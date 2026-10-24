using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises directory DATA lifetimes and actual ordinary readers on minimal independent files.
/// </summary>
/// <param name="output">
/// Receives uncapped timing counts and verified fixture cleanup.
/// </param>
public sealed class OrdinaryReadPathPhaseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Gets an absolute synthetic classifier root; pure tests perform no I/O there.
    /// </summary>
    private static string Root => Path.Combine(Path.GetTempPath(), "boe-path-phase-source");

    /// <summary>
    /// Requires repeated positive classifications to use the existing operation service.
    /// </summary>
    [Fact]
    public void PositiveTrustedDirectoriesHaveOneSourceAndReleaseServiceEntries()
    {
        var sources = 0;
        using var phase = new ReadPathObservationPhase(Root, _ => { sources++; return TrustedLocalFileScope.EntryKind.Directory; });
        for (var index = 0; index < 3; index++) Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, phase.ObserveTrusted(Root));
        Assert.Equal(1, sources);
        Assert.Equal(2, phase.CaptureStatistics().Hits);
        Assert.Equal(1, phase.CaptureState().Entries);
        phase.Dispose();
        Assert.True(phase.CaptureState().Closed);
        Assert.Equal(0, phase.CaptureState().Entries);
        phase.ObserveTrusted(Root);
        Assert.Equal(2, sources);
    }

    /// <summary>
    /// Keeps API families and root/segment roles separate while reusing each exact positive observation.
    /// </summary>
    [Fact]
    public void WindowsAndTrustedFamiliesAndWindowsRolesNeverAlias()
    {
        var trusted = 0;
        var provider = new Provider();
        using var phase = new ReadPathObservationPhase(Root, _ => { trusted++; return TrustedLocalFileScope.EntryKind.Directory; }, provider);
        for (var index = 0; index < 2; index++)
        {
            phase.ObserveTrusted(Root);
            phase.ObserveDirectory(Root, PathMetadataRole.Root);
            phase.ObserveDirectory(Root, PathMetadataRole.Segment);
        }
        Assert.Equal(1, trusted);
        Assert.Equal(2, provider.DirectoryCalls);
        Assert.Equal(3, phase.CaptureStatistics().Hits);
        Assert.Equal(3, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Keeps every non-directory leaf or absence fresh even within the phase.
    /// </summary>
    /// <param name="kind">
    /// The original non-directory classifier result.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void NonDirectoryTrustedDataIsNeverRetained(int kind)
    {
        var sources = 0;
        using var phase = new ReadPathObservationPhase(Root, _ => { sources++; return (TrustedLocalFileScope.EntryKind)kind; });
        phase.ObserveTrusted(Root);
        phase.ObserveTrusted(Root);
        Assert.Equal(2, sources);
        Assert.Equal(0, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Refuses to retain absence, nullable attributes, a file kind or a linked directory as positive directory DATA.
    /// </summary>
    /// <param name="shape">
    /// The independent provider observation shape.
    /// </param>
    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("file")]
    [InlineData("link")]
    public void WindowsNonOrdinaryDirectoryObservationsRemainFresh(string shape)
    {
        var provider = new Provider { Observation = shape switch
        {
            "missing" => new(false, null), "unknown" => new(true, null),
            "file" => new(true, FileAttributes.Normal),
            _ => new(true, FileAttributes.Directory | FileAttributes.ReparsePoint)
        } };
        using var phase = new ReadPathObservationPhase(Root, windows: provider);
        phase.ObserveDirectory(Root, PathMetadataRole.Root);
        phase.ObserveDirectory(Root, PathMetadataRole.Root);
        Assert.Equal(2, provider.DirectoryCalls);
        Assert.Equal(0, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Preserves the original failure object and retires the entire failed phase.
    /// </summary>
    [Fact]
    public void SourceFailureClosesThePhaseWithoutReplacingTheException()
    {
        var failure = new IOException("original classifier failure");
        using var phase = new ReadPathObservationPhase(Root, _ => throw failure);
        Assert.Same(failure, Record.Exception(() => phase.ObserveTrusted(Root)));
        Assert.True(phase.CaptureState().Closed);
        Assert.Equal(0, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Allows the original Windows fallback after the new observation has failed and retired its DATA.
    /// </summary>
    [Fact]
    public void WindowsObservationFailureStillExecutesOriginalFallback()
    {
        var provider = new Provider { Failure = new IOException("refresh failed") };
        using var phase = new ReadPathObservationPhase(Root, windows: provider);
        WindowsPathMetadataGuard.Ensure(Root, Root, "fixture", phase);
        Assert.Equal(1, provider.LegacyDirectoryCalls);
        Assert.True(phase.CaptureState().Closed);
    }

    /// <summary>
    /// Requires the real manager to reduce physical classification work while returning identical ordinary bytes.
    /// </summary>
    /// <param name="synchronous">
    /// Whether to exercise the synchronous source reader.
    /// </param>
    /// <returns>
    /// Completion after both isolated timing windows and the original fixture cleanup.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualOrdinaryPreparationUsesFewerPhysicalClassifications(bool synchronous)
    {
        var recorder = new OperationTimingAccumulator();
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
        await fixture.InitializeAsync();
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath);
        async Task<long> SourceAttempts(bool enabled)
        {
            fixture.Files.OrdinaryReadPathReuseEnabled = enabled;
            using var window = recorder.StartWindow();
            Assert.Equal("original", synchronous ? fixture.Files.ReadFileSync("present.txt") :
                await fixture.Files.ReadFileAsync("present.txt"));
            window.Dispose();
            var snapshot = window.Capture();
            Assert.True(snapshot.Complete);
            return snapshot.Rows.Where(row => row.Name is "TrustedScope.Probe.GetAttributes" or
                "WindowsMetadata.DirectoryRefreshAndCachedRead").Sum(row => row.Completed);
        }
        var original = await SourceAttempts(false);
        var before = fixture.Files.CaptureOrdinaryReadPathStatistics();
        var reused = await SourceAttempts(true);
        var counts = fixture.Files.CaptureOrdinaryReadPathStatistics() - before;
        output.WriteLine($"path-phase original={original} reused={reused} counts={counts}");
        Assert.True(counts.Invocations > 0);
        Assert.True(counts.Hits > 0);
        Assert.True(original > reused);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(fixture.RawPath));
    }

    /// <summary>
    /// Invalidates real manager-owned directory DATA after a supported mutation changes the root token.
    /// </summary>
    /// <returns>
    /// Completion after initialization and the owned mutation have settled.
    /// </returns>
    [Fact]
    public async Task SupportedMutationRetiresTheManagerOwnedPhase()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        phase.ObserveTrusted(Path.GetDirectoryName(fixture.RawPath)!);
        Assert.Equal(1, phase.CaptureState().Entries);
        // Stay on the phase's original synchronous thread while an independent writer settles.
        fixture.Files.WriteFileAtomicAsync("other.txt", "supported effect").GetAwaiter().GetResult();
        Assert.Null(phase.Active);
        Assert.True(phase.CaptureState().Closed);
        Assert.Equal(0, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Makes actual later reads observe changed bytes, absence and a directory replacing the file.
    /// </summary>
    /// <param name="synchronous">
    /// Whether to use the synchronous reader for every fresh request.
    /// </param>
    /// <returns>
    /// Completion after the independent requests and owned cleanup.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterOrdinaryReadsCannotReusePriorBytesOrLeafKinds(bool synchronous)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        Task<string?> Read() => synchronous ? Task.FromResult(fixture.Files.ReadFileSync("present.txt")) :
            fixture.Files.ReadFileAsync("present.txt");
        Assert.Equal("original", await Read());
        var timestamp = File.GetLastWriteTimeUtc(fixture.RawPath);
        File.WriteAllText(fixture.RawPath, "changed!");
        File.SetLastWriteTimeUtc(fixture.RawPath, timestamp);
        Assert.Equal("changed!", await Read());
        File.Delete(fixture.RawPath);
        Assert.Null(await Read());
        Directory.CreateDirectory(fixture.RawPath);
        await Assert.ThrowsAsync<InvalidDataException>(Read);
    }

    /// <summary>
    /// Discards previous positive directory observations before a later reparse refusal.
    /// </summary>
    [Fact]
    public void LaterPhasePreservesReparseRefusal()
    {
        var provider = new Provider();
        using (var phase = new ReadPathObservationPhase(Root, windows: provider))
            WindowsPathMetadataGuard.Ensure(Root, Root, "fixture", phase);
        provider.Observation = new(true, FileAttributes.Directory | FileAttributes.ReparsePoint);
        provider.Reparse = true;
        using var next = new ReadPathObservationPhase(Root, windows: provider);
        Assert.Throws<InvalidDataException>(() => WindowsPathMetadataGuard.Ensure(Root, Root, "fixture", next));
    }

    /// <summary>
    /// Keeps an already retained observation out of a different thread's original source request.
    /// </summary>
    /// <returns>
    /// Completion after the independent classifier call has settled.
    /// </returns>
    [Fact]
    public async Task ForeignThreadUsesFreshClassification()
    {
        var sources = 0;
        using var phase = new ReadPathObservationPhase(Root, _ =>
        {
            Interlocked.Increment(ref sources);
            return TrustedLocalFileScope.EntryKind.Directory;
        });
        phase.ObserveTrusted(Root);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { phase.ObserveTrusted(Root); completed.SetResult(); }
            catch (Exception error) { completed.SetException(error); }
        }) { IsBackground = true };
        thread.Start();
        await completed.Task;
        thread.Join();
        Assert.Equal(2, sources);
        phase.Dispose();
        Assert.Equal(0, phase.CaptureState().Entries);
    }

    /// <summary>
    /// Refuses recursive source execution before it can reuse partially constructed DATA.
    /// </summary>
    [Fact]
    public void ReentrantClassifierRetiresAllData()
    {
        ReadPathObservationPhase? phase = null;
        using (phase = new ReadPathObservationPhase(Root, path => phase!.ObserveTrusted(path)))
        {
            Assert.Throws<InvalidOperationException>(() => phase.ObserveTrusted(Root));
            Assert.True(phase.CaptureState().Closed);
            Assert.Equal(0, phase.CaptureState().Entries);
        }
    }

    /// <summary>
    /// Preserves the existing arbitrary-read-hook fallback in the actual adopted fixture.
    /// </summary>
    /// <returns>
    /// Completion after a fresh hooked source and verified cleanup.
    /// </returns>
    [Fact]
    public async Task ActualReadHooksRetainTheOriginalUncachedRoute()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, true);
        await fixture.InitializeAsync();
        var before = fixture.Files.CaptureOrdinaryReadPathStatistics();
        Assert.Equal("original", await fixture.Files.ReadFileAsync("present.txt"));
        Assert.Equal(0, (fixture.Files.CaptureOrdinaryReadPathStatistics() - before).Invocations);
    }

    /// <summary>
    /// Supplies only metadata DATA for isolated role and fallback contract tests.
    /// </summary>
    private sealed class Provider : IWindowsPathMetadataProvider
    {
        /// <summary>
        /// Counts original directory observation calls.
        /// </summary>
        internal int DirectoryCalls;

        /// <summary>
        /// Counts original fallback directory checks.
        /// </summary>
        internal int LegacyDirectoryCalls;
        /// <summary>
        /// Supplies the selected detached directory DATA.
        /// </summary>
        internal PathMetadataObservation Observation = new(true, FileAttributes.Directory);
        /// <summary>
        /// Supplies an optional original refresh failure.
        /// </summary>
        internal IOException? Failure;

        /// <summary>
        /// Supplies the original final reparse query result.
        /// </summary>
        internal bool Reparse;

        /// <inheritdoc />
        public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role)
        {
            DirectoryCalls++;
            if (Failure is { } error) throw error;
            return Observation;
        }

        /// <inheritdoc />
        public PathMetadataObservation ObserveFile(string path, PathMetadataRole role) => new(false, null);

        /// <inheritdoc />
        public bool LegacyDirectoryExists(string path, PathMetadataRole role) { LegacyDirectoryCalls++; return true; }

        /// <inheritdoc />
        public bool LegacyFileExists(string path, PathMetadataRole role) => false;

        /// <inheritdoc />
        public bool IsReparsePoint(string path, PathMetadataRole role) => Reparse;
    }
}
