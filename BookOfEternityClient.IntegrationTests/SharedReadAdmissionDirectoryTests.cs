using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Measures shared directory DATA with real classifiers and independent minimal adopted sessions.
/// </summary>
/// <param name="output">
/// Receives all measured path observations, named scopes and owned fixture cleanup.
/// </param>
public sealed class SharedReadAdmissionDirectoryTests(ITestOutputHelper output)
{
    /// <summary>
    /// Establishes actual common ancestor overlap while keeping the guard file and its handle validation fresh.
    /// </summary>
    /// <returns>
    /// Completion after the independently owned guard streams and fixtures have closed.
    /// </returns>
    [Fact]
    public async Task ActualClassifierCompositionHasCommonAncestorsAndFreshGuardLeaves()
    {
        async Task<(long Queries, long RequiredFileQueries, int Overlap)> Measure(bool shared)
        {
            using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
            await fixture.InitializeAsync();
            using var guard = await GmMainOwnerGuard.AcquireAsync(fixture.Files.BasePath);
            var preparing = true;
            var preparation = new List<string>();
            var admission = new List<string>();
            TrustedLocalFileScope.EntryKind Classify(string path)
            {
                (preparing ? preparation : admission).Add(path);
                return TrustedLocalFileScope.Probe(path);
            }
            using var phase = new ReadPathObservationPhase(fixture.Files.GameSessionPath, Classify);
            TrustedLocalFileScope.CreateForOrdinaryRead(fixture.Files.GameSessionPath, phase);
            preparing = false;
            using var observations = new AdmissionDirectoryObservations(fixture.Files.BasePath,
                shared ? phase.ObserveTrusted : Classify);
            guard.Validate(observations);
            var required = admission.Count(path => Path.GetFileName(path) == "gm-main-owner.lock");
            var overlap = preparation.Intersect(admission, StringComparer.Ordinal).Count();
            output.WriteLine(JsonSerializer.Serialize(new { shared, preparation, admission, required, overlap }));
            Assert.Equal(1, required);
            return (admission.Count, required, overlap);
        }
        var original = await Measure(false);
        var shared = await Measure(true);
        Assert.True(original.Overlap > 0);
        Assert.True(shared.Queries < original.Queries);
        Assert.Equal(original.RequiredFileQueries, shared.RequiredFileQueries);
    }

    /// <summary>
    /// Requires the production declared reader to share initial admission DATA and reduce real metadata calls.
    /// </summary>
    /// <param name="synchronous">
    /// Whether to exercise the original synchronous reader rather than the asynchronous family.
    /// </param>
    /// <returns>
    /// Completion after both isolated ten-file controls and their owned cleanup.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredOrdinaryReadsReducePhysicalQueriesWithoutChangingData(bool synchronous)
    {
        async Task<(long Sources, long PhaseHits, long AdmissionQueries, long BorrowedHits, long MainCalls, long NativeCalls, long LeaseValidations)> Measure(bool enabled)
        {
            var recorder = new OperationTimingAccumulator();
            using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false, recorder);
            await fixture.InitializeAsync();
            var sources = Enumerable.Range(0, 10).Select(index => $"source-{index}.txt").ToArray();
            foreach (var path in sources)
                File.WriteAllText(Path.Combine(fixture.Files.GameSessionPath, path), path);
            var timestamps = sources.Select(path => File.GetLastWriteTimeUtc(Path.Combine(fixture.Files.GameSessionPath, path))).ToArray();
            fixture.Files.OrdinaryReadAdmissionReuseEnabled = enabled;
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            var phaseBefore = fixture.Files.CaptureOrdinaryReadPathStatistics();
            var admissionBefore = fixture.Files.CaptureAdmissionDirectoryStatistics();
            var borrowedBefore = fixture.Files.CaptureSharedReadAdmissionHits();
            using var window = recorder.StartWindow();
            foreach (var path in sources)
                Assert.Equal(path, synchronous ? fixture.Files.ReadFileSync(lease, path) :
                    await fixture.Files.ReadFileAsync(lease, path));
            window.Dispose();
            var snapshot = window.Capture();
            Assert.True(snapshot.Complete);
            var phase = fixture.Files.CaptureOrdinaryReadPathStatistics() - phaseBefore;
            var admission = fixture.Files.CaptureAdmissionDirectoryStatistics() - admissionBefore;
            var queries = snapshot.Rows.Where(row => row.Name == "TrustedScope.Probe.GetAttributes").Sum(row => row.Completed);
            var borrowed = fixture.Files.CaptureSharedReadAdmissionHits() - borrowedBefore;
            var mainCalls = snapshot.Rows.Where(row => row.Name == "MainAdmission.Validate.GuardCheck").Sum(row => row.Completed);
            var nativeCalls = snapshot.Rows.Where(row => row.Name == "PhysicalFileAuthority.ValidateHandlePath").Sum(row => row.Completed);
            var leaseValidations = snapshot.Rows.Where(row => row.Name == "Lease.Validate.Total").Sum(row => row.Completed);
            Assert.Equal(timestamps, sources.Select(path => File.GetLastWriteTimeUtc(Path.Combine(fixture.Files.GameSessionPath, path))).ToArray());
            output.WriteLine(JsonSerializer.Serialize(new { enabled, synchronous, queries, phase, admission, borrowed, mainCalls, nativeCalls, leaseValidations, snapshot }));
            return (queries, phase.Hits, admission.SourceQueries, borrowed, mainCalls, nativeCalls, leaseValidations);
        }
        var original = await Measure(false);
        var shared = await Measure(true);
        Assert.True(shared.Sources < original.Sources, $"Physical queries were not reduced: off={original.Sources}, on={shared.Sources}.");
        Assert.True(shared.PhaseHits > original.PhaseHits);
        Assert.True(shared.AdmissionQueries < original.AdmissionQueries);
        Assert.Equal(0, original.BorrowedHits);
        Assert.True(shared.BorrowedHits > 0);
        Assert.True(shared.MainCalls > 0);
        Assert.True(shared.LeaseValidations > 0);
        if (OperatingSystem.IsWindows()) Assert.True(shared.NativeCalls > 0);
        else Assert.Equal(0, shared.NativeCalls);
        Assert.Equal(original.MainCalls, shared.MainCalls);
        Assert.Equal(original.NativeCalls, shared.NativeCalls);
        Assert.Equal(original.LeaseValidations, shared.LeaseValidations);
    }

    /// <summary>
    /// Prevents borrowed DATA from escaping through the inner cache after the outer preparation closes.
    /// </summary>
    /// <returns>
    /// Completion after the minimal session has been initialized and the changed leaf observed freshly.
    /// </returns>
    [Fact]
    public async Task BorrowedDirectoryCannotSurviveOuterCloseInTheInnerCache()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var path = Path.Combine(fixture.Files.GameSessionPath, "shared-directory");
        Directory.CreateDirectory(path);
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, phase.ObserveTrusted(path));
        using var admission = fixture.Files.BeginAdmissionDirectoryObservations(phase);
        Assert.NotNull(admission);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, admission.Observe(path));
        Assert.Equal(1, admission.SharedHits);
        Assert.Equal(0, admission.CaptureState().Entries);
        phase.Dispose();
        Directory.Delete(path);
        File.WriteAllText(path, "new file kind");
        Assert.Equal(TrustedLocalFileScope.EntryKind.RegularFile, admission.Observe(path));
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
        Assert.Equal(1, admission.SharedHits);
    }

    /// <summary>
    /// Rejects a foreign owner and disabled policies before selecting shared directory DATA.
    /// </summary>
    /// <param name="policy">
    /// Foreign manager, closed phase, disabled sharing, disabled admission reuse or disabled preparation reuse.
    /// </param>
    /// <returns>
    /// Completion after independent owners are initialized and one original classification is required.
    /// </returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ForeignClosedOrDisabledPhasesRetainFreshClassification(int policy)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        using var other = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        await other.InitializeAsync();
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        var path = Path.GetPathRoot(fixture.Files.GameSessionPath)!;
        phase.ObserveTrusted(path);
        if (policy == 1) phase.Dispose();
        if (policy == 2) fixture.Files.OrdinaryReadAdmissionReuseEnabled = false;
        if (policy == 3) fixture.Files.AdmissionDirectoryReuseEnabled = false;
        if (policy == 4) fixture.Files.OrdinaryReadPathReuseEnabled = false;
        using var admission = (policy == 0 ? other.Files : fixture.Files).BeginAdmissionDirectoryObservations(phase);
        Assert.NotNull(admission);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, admission.Observe(path));
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
        Assert.Equal(0, admission.SharedHits);
    }

    /// <summary>
    /// Checks policy changes on a live inner context without retaining a previously borrowed observation.
    /// </summary>
    /// <returns>
    /// Completion after the disabled sharing policy forces another original classifier attempt.
    /// </returns>
    [Fact]
    public async Task DisablingSharingAfterAHitForcesFreshClassification()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        var path = fixture.Files.GameSessionPath;
        phase.ObserveTrusted(path);
        using var admission = fixture.Files.BeginAdmissionDirectoryObservations(phase);
        Assert.NotNull(admission);
        admission.Observe(path);
        Assert.Equal(1, admission.SharedHits);
        fixture.Files.OrdinaryReadAdmissionReuseEnabled = false;
        admission.Observe(path);
        Assert.Equal(1, admission.SharedHits);
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
    }

    /// <summary>
    /// Keeps a custom classifier's DATA separate from the outer phase's different source contract.
    /// </summary>
    [Fact]
    public void CustomClassifierCannotBorrowAnotherProvidersDirectory()
    {
        var root = Path.GetTempPath();
        using var phase = new ReadPathObservationPhase(root, _ => TrustedLocalFileScope.EntryKind.Directory);
        phase.ObserveTrusted(root);
        using var admission = new AdmissionDirectoryObservations(root, _ => TrustedLocalFileScope.EntryKind.Other,
            sharedReadPhase: phase);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Other, admission.Observe(root));
        Assert.Equal(0, admission.SharedHits);
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
    }

    /// <summary>
    /// Preserves the original source error and counts its attempt rather than reporting a borrowed hit.
    /// </summary>
    [Fact]
    public void SharedSourceFailureRetiresBothContextsAndKeepsItsIdentity()
    {
        var failure = new IOException("original classifier failure");
        using var phase = new ReadPathObservationPhase(Path.GetTempPath(), _ => throw failure);
        using var admission = new AdmissionDirectoryObservations(Path.GetTempPath(), sharedReadPhase: phase);
        Assert.Same(failure, Record.Exception(() => admission.Observe(Path.GetTempPath())));
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
        Assert.Equal(0, admission.SharedHits);
        Assert.True(admission.CaptureState().Closed);
        Assert.True(phase.CaptureState().Closed);
    }

    /// <summary>
    /// Requires fresh Windows native guard authentication after common directory DATA has been borrowed.
    /// </summary>
    /// <returns>
    /// Completion after independent guard streams and the tiny owned session have closed.
    /// </returns>
    [Fact]
    public async Task BorrowedDirectoriesCannotAuthenticateTheWrongGuardHandle()
    {
        Assert.True(OperatingSystem.IsWindows(), "Native guard-handle binding is a Windows contract.");
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var expected = Path.Combine(fixture.Files.GameSessionPath, "expected.lock");
        var other = Path.Combine(fixture.Files.GameSessionPath, "other.lock");
        File.WriteAllText(expected, "expected");
        File.WriteAllText(other, "other");
        using var valid = new GmMainOwnerGuard(fixture.Files.BasePath, expected, File.OpenRead(expected));
        using var wrong = new GmMainOwnerGuard(fixture.Files.BasePath, expected, File.OpenRead(other));
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        TrustedLocalFileScope.CreateForOrdinaryRead(fixture.Files.GameSessionPath, phase);
        using var admission = fixture.Files.BeginAdmissionDirectoryObservations(phase);
        Assert.NotNull(admission);
        valid.Validate(admission);
        Assert.True(admission.SharedHits > 0);
        Assert.Throws<InvalidDataException>(() => wrong.Validate(admission));
    }

    /// <summary>
    /// Keeps Windows persisted main-record bytes and newly appearing worker inventory fresh on the fused route.
    /// </summary>
    /// <returns>
    /// Completion after current main admission and the independent fixture have closed.
    /// </returns>
    [Fact]
    public async Task SharedMainAdmissionStillRefusesChangedRecordAndNewWorkerInventory()
    {
        Assert.True(OperatingSystem.IsWindows(), "This worker-inventory contract requires Windows.");
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var files = fixture.Files;
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"),
            Guid.Empty.ToString("N"), 1, GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "fixture");
        var stopped = new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Stopped,
            new(identity, GmSessionRunStopKind.OwnedScopeEmpty, identity.BootId));
        var directory = Path.Combine(files.BasePath, ".boe_runtime", "gm-runs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        var bytes = GmSessionRunRecordCodec.Encode(stopped);
        File.WriteAllBytes(path, bytes);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        void ValidateShared()
        {
            using var phase = files.BeginOrdinaryReadPathPhase();
            Assert.NotNull(phase);
            TrustedLocalFileScope.CreateForOrdinaryRead(files.GameSessionPath, phase);
            main.Validate(null, phase);
        }
        ValidateShared();
        Assert.True(files.CaptureSharedReadAdmissionHits() > 0);
        try
        {
            File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped with
                { Disposition = GmSessionRunDisposition.Running, StopEvidence = null }));
            Assert.Throws<IOException>(ValidateShared);
            File.WriteAllText(path, "{");
            Assert.Throws<InvalidDataException>(ValidateShared);
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(ValidateShared);
        }
        finally { File.WriteAllBytes(path, bytes); }
        var inventory = Path.Combine(files.BasePath, ".boe_runtime", "worker-runs-v1");
        Directory.CreateDirectory(inventory);
        try { Assert.Throws<IOException>(ValidateShared); }
        finally { Directory.Delete(inventory); }
        ValidateShared();
    }

    /// <summary>
    /// Keeps main-record bytes fresh on the shared route with the current platform's supported backend.
    /// </summary>
    /// <returns>
    /// Completion after valid admission, running/malformed/missing refusals and restored valid admission.
    /// </returns>
    [Fact]
    public async Task SharedMainAdmissionRefusesChangedRecordOnCurrentBackend()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        var files = fixture.Files;
        var backend = OperatingSystem.IsWindows() ? GmSessionRunBackend.WindowsJob : GmSessionRunBackend.LinuxSupervisor;
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"),
            Guid.Empty.ToString("N"), 1, backend, Guid.NewGuid().ToString("N"), "fixture");
        var stopped = new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Stopped,
            new(identity, GmSessionRunStopKind.OwnedScopeEmpty, identity.BootId));
        var directory = Path.Combine(files.BasePath, ".boe_runtime", "gm-runs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        var bytes = GmSessionRunRecordCodec.Encode(stopped);
        File.WriteAllBytes(path, bytes);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        void ValidateShared()
        {
            using var phase = files.BeginOrdinaryReadPathPhase();
            Assert.NotNull(phase);
            TrustedLocalFileScope.CreateForOrdinaryRead(files.GameSessionPath, phase);
            main.Validate(null, phase);
        }
        ValidateShared();
        Assert.True(files.CaptureSharedReadAdmissionHits() > 0);
        try
        {
            File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped with
                { Disposition = GmSessionRunDisposition.Running, StopEvidence = null }));
            Assert.Throws<IOException>(ValidateShared);
            File.WriteAllText(path, "{");
            Assert.Throws<InvalidDataException>(ValidateShared);
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(ValidateShared);
        }
        finally { File.WriteAllBytes(path, bytes); }
        ValidateShared();
    }

    /// <summary>
    /// Retires outer directory DATA after a supported mutation before the inner context can borrow again.
    /// </summary>
    /// <returns>
    /// Completion after initialization and the real manager-owned mutation have settled.
    /// </returns>
    [Fact]
    public async Task SupportedMutationRetiresBorrowedDirectoryData()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        var path = fixture.Files.GameSessionPath;
        phase.ObserveTrusted(path);
        using var admission = fixture.Files.BeginAdmissionDirectoryObservations(phase);
        Assert.NotNull(admission);
        admission.Observe(path);
        Assert.Equal(1, admission.SharedHits);
        fixture.Files.WriteFileAtomicAsync("other.txt", "supported mutation").GetAwaiter().GetResult();
        admission.Observe(path);
        Assert.True(phase.CaptureState().Closed);
        Assert.Equal(1, admission.SharedHits);
        Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
    }

    /// <summary>
    /// Keeps a foreign thread's main directory context on its original classifier route.
    /// </summary>
    /// <returns>
    /// Completion after the independent thread's classification and cleanup have settled.
    /// </returns>
    [Fact]
    public async Task ForeignThreadCannotBorrowThePreparation()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        using var phase = fixture.Files.BeginOrdinaryReadPathPhase();
        Assert.NotNull(phase);
        var path = fixture.Files.GameSessionPath;
        phase.ObserveTrusted(path);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var admission = fixture.Files.BeginAdmissionDirectoryObservations(phase);
                Assert.NotNull(admission);
                admission.Observe(path);
                Assert.Equal(0, admission.SharedHits);
                Assert.Equal(1, admission.CaptureStatistics().SourceQueries);
                completed.SetResult();
            }
            catch (Exception error) { completed.SetException(error); }
        }) { IsBackground = true };
        thread.Start();
        await completed.Task;
        thread.Join();
    }

    /// <summary>
    /// Observes changed same-timestamp bytes, absence and an invalid new leaf on later declared reads.
    /// </summary>
    /// <param name="synchronous">
    /// Whether every request uses the synchronous declared family.
    /// </param>
    /// <returns>
    /// Completion after fresh later requests and original lease cleanup.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterDeclaredReadsRemainFreshAfterDirectorySharing(bool synchronous)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        Task<string?> Read() => synchronous ? Task.FromResult(fixture.Files.ReadFileSync(lease, "present.txt")) :
            fixture.Files.ReadFileAsync(lease, "present.txt");
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
}
