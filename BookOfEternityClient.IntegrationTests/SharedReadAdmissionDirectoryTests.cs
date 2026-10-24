using System.Text.Json;
using BookOfEternityClient.Core;
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
        async Task<(long Sources, long PhaseHits, long AdmissionQueries)> Measure(bool enabled)
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
            Assert.Equal(timestamps, sources.Select(path => File.GetLastWriteTimeUtc(Path.Combine(fixture.Files.GameSessionPath, path))).ToArray());
            output.WriteLine(JsonSerializer.Serialize(new { enabled, synchronous, queries, phase, admission, snapshot }));
            return (queries, phase.Hits, admission.SourceQueries);
        }
        var original = await Measure(false);
        var shared = await Measure(true);
        Assert.True(shared.Sources < original.Sources, $"Physical queries were not reduced: off={original.Sources}, on={shared.Sources}.");
        Assert.True(shared.PhaseHits > original.PhaseHits);
        Assert.True(shared.AdmissionQueries < original.AdmissionQueries);
    }
}
