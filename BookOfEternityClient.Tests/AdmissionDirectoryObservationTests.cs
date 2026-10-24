using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using EntryKind = BookOfEternityClient.Core.TrustedLocalFileScope.EntryKind;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks admission-local directory DATA reuse on independent tiny roots without a game world.
/// </summary>
public sealed class AdmissionDirectoryObservationTests : IDisposable
{
    private readonly string _sandbox = Path.GetFullPath(Path.Combine("C:/Temp", "boe-admission-directory-" + Guid.NewGuid().ToString("N")));
    private readonly ITestOutputHelper _output;
    private string Root => Path.Combine(_sandbox, "owned", "root");

    /// <summary>
    /// Creates only an owned directory and the diagnostic output receiver.
    /// </summary>
    /// <param name="output">
    /// The test's output receiver for named composed-path measurements.
    /// </param>
    public AdmissionDirectoryObservationTests(ITestOutputHelper output)
    {
        Assert.True(OperatingSystem.IsWindows());
        _output = output;
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Requires repeated directory DATA to use one source query and release every entry on close.
    /// </summary>
    [Fact]
    public void DirectoryDataIsReusedOnlyWithinOneInvocation()
    {
        var queries = 0;
        var context = new AdmissionDirectoryObservations(Root, _ => { queries++; return EntryKind.Directory; });
        Assert.Equal(EntryKind.Directory, context.Observe(Root));
        Assert.Equal(EntryKind.Directory, context.Observe(Root));
        Assert.Equal(1, queries);
        Assert.Equal(1, context.CaptureState().Entries);
        context.Dispose();
        Assert.Equal(0, context.CaptureState().Entries);
        Assert.True(context.CaptureState().Closed);
        Assert.Throws<ObjectDisposedException>(() => context.Observe(Root));
        using var next = new AdmissionDirectoryObservations(Root, _ => { queries++; return EntryKind.Link; });
        Assert.Equal(EntryKind.Link, next.Observe(Root));
        Assert.Equal(2, queries);
    }

    /// <summary>
    /// Keeps absence and non-directory leaves fresh even when they repeat within the invocation.
    /// </summary>
    /// <param name="value">
    /// A non-directory source classification which must never enter the directory cache.
    /// </param>
    [Theory]
    [InlineData((int)EntryKind.Missing)]
    [InlineData((int)EntryKind.RegularFile)]
    [InlineData((int)EntryKind.Link)]
    [InlineData((int)EntryKind.Other)]
    public void NonDirectoryObservationsRemainFresh(int value)
    {
        var queries = 0;
        using var context = new AdmissionDirectoryObservations(Root, _ => { queries++; return (EntryKind)value; });
        Assert.Equal((EntryKind)value, context.Observe(Root));
        Assert.Equal((EntryKind)value, context.Observe(Root));
        Assert.Equal(2, queries);
        Assert.Equal(0, context.CaptureState().Entries);
    }

    /// <summary>
    /// Measures actual composed guard, inventory and persistence helpers while every later admission remains fresh.
    /// </summary>
    [Fact]
    public void ComposedAdmissionReusesDirectoriesAndNextCallStillQueriesThem()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        files.AdmissionDirectoryReuseEnabled = false;
        var before = files.CaptureAdmissionDirectoryStatistics();
        main.Validate(null);
        var uncached = files.CaptureAdmissionDirectoryStatistics() - before;
        files.AdmissionDirectoryReuseEnabled = true;
        before = files.CaptureAdmissionDirectoryStatistics();
        main.Validate(null);
        var cached = files.CaptureAdmissionDirectoryStatistics() - before;
        Assert.True(cached.SourceQueries < uncached.SourceQueries, $"cached={cached}; uncached={uncached}");
        Assert.Equal(uncached.Requests, cached.Requests);
        Assert.Equal(uncached.SourceQueries - cached.SourceQueries, cached.Hits);
        before = files.CaptureAdmissionDirectoryStatistics();
        main.Validate(null);
        var next = files.CaptureAdmissionDirectoryStatistics() - before;
        Assert.Equal(cached.SourceQueries, next.SourceQueries);
        Assert.Equal(cached.Hits, next.Hits);
    }

    /// <summary>
    /// Requires current main-record bytes and namespace membership to be checked on every admission.
    /// </summary>
    [Fact]
    public void CurrentMainRecordAndUnexpectedNamespaceEntryRemainFresh()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"),
            Guid.Empty.ToString("N"), 1, GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "fixture");
        var stopped = new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Stopped,
            new(identity, GmSessionRunStopKind.OwnedScopeEmpty, identity.BootId));
        var directory = Path.Combine(files.BasePath, ".boe_runtime", "gm-runs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped));
        using var main = files.BeginMainAdmission();
        main.Validate(null);
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped with { Disposition = GmSessionRunDisposition.Running, StopEvidence = null }));
        Assert.Throws<InvalidDataException>(() => main.Validate(null));
    }

    /// <summary>
    /// Reports named uncached and cached costs on the genuine composed path, without flaky wall-time assertions.
    /// </summary>
    [Fact]
    public void ComposedPathMeasurementsPreserveTheSameNumberOfAdmissions()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        const int calls = 200;
        var queryCounts = new List<long>();
        foreach (var enabled in new[] { false, true })
        {
            files.AdmissionDirectoryReuseEnabled = enabled;
            var before = files.CaptureAdmissionDirectoryStatistics();
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < calls; i++) main.Validate(null);
            watch.Stop();
            var counts = files.CaptureAdmissionDirectoryStatistics() - before;
            queryCounts.Add(counts.SourceQueries);
            Assert.Equal(calls, counts.Invocations);
            _output.WriteLine($"admission_{(enabled ? "cached" : "uncached")}_{calls}_calls_seconds={watch.Elapsed.TotalSeconds:R}; counts={counts}");
        }
        Assert.True(queryCounts[1] < queryCounts[0]);
    }

    /// <summary>
    /// Deletes only this test's owned directory after all guard scopes have ended.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }
}
