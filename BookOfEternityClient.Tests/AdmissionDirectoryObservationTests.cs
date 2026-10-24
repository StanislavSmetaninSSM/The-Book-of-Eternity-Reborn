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
    /// <summary>
    /// Gets this test's absolute independent root.
    /// </summary>
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
    /// Keeps unrelated names and differently spelled identities outside the selected root's entries.
    /// </summary>
    [Fact]
    public void UnrelatedAndOrdinalNamesCannotBorrowDirectoryData()
    {
        var queries = 0;
        using var context = new AdmissionDirectoryObservations(Root, _ => { queries++; return EntryKind.Directory; });
        context.Observe(Root);
        context.Observe(Root.ToUpperInvariant());
        var unrelated = Path.Combine(_sandbox, "sibling");
        context.Observe(unrelated);
        context.Observe(unrelated);
        Assert.Equal(4, queries);
        Assert.Equal(1, context.CaptureState().Entries);
    }

    /// <summary>
    /// Requires original classifier failure to close and clear every previously retained result.
    /// </summary>
    [Fact]
    public void SourceFailureClosesAndClearsTheInvocation()
    {
        var error = new IOException("owned fixture failure");
        using var context = new AdmissionDirectoryObservations(Root,
            path => path == Root ? EntryKind.Directory : throw error);
        context.Observe(Root);
        Assert.Same(error, Record.Exception(() => context.Observe(Path.Combine(Root, "leaf"))));
        Assert.True(context.CaptureState().Closed);
        Assert.Equal(0, context.CaptureState().Entries);
        Assert.Throws<ObjectDisposedException>(() => context.Observe(Root));
    }

    /// <summary>
    /// Rejects reentry from an injected source instead of sharing partially observed directory DATA.
    /// </summary>
    [Fact]
    public void SourceReentryClosesTheInvocation()
    {
        AdmissionDirectoryObservations context = null!;
        using var owned = context = new AdmissionDirectoryObservations(Root, path => context.Observe(path));
        Assert.Throws<InvalidOperationException>(() => context.Observe(Root));
        Assert.True(context.CaptureState().Closed);
        Assert.Equal(0, context.CaptureState().Entries);
    }

    /// <summary>
    /// Leaves every composed helper fresh when an arbitrary detailed diagnostic callback is installed.
    /// </summary>
    /// <returns>
    /// Completion after actual guard acquisition and callback-route validation.
    /// </returns>
    [Fact]
    public async Task DetailedCallbacksUseTheOriginalUnsharedRoute()
    {
        var calls = 0;
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            { OperationTimingObserver = (_, _, _) => calls++ });
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        var before = calls;
        main.Validate(null);
        Assert.True(calls > before);
        Assert.Equal(0, files.CaptureAdmissionDirectoryStatistics().Invocations);
    }

    /// <summary>
    /// Disables sharing for an enclosing thread receiver even when this manager has no local hooks.
    /// </summary>
    /// <returns>
    /// Completion after guard acquisition and a synchronous ambient-observer check.
    /// </returns>
    [Fact]
    public async Task AmbientCallbacksUseTheOriginalUnsharedRoute()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        var before = files.CaptureAdmissionDirectoryStatistics();
        var calls = 0;
        using (FileSystemManager.EnterOperationTimingContext((_, _, _) => calls++)) main.Validate(null);
        Assert.True(calls > 0);
        Assert.Equal(0, (files.CaptureAdmissionDirectoryStatistics() - before).Invocations);
    }

    /// <summary>
    /// Refuses newly appearing worker inventory after a successful cached admission without adopting it.
    /// </summary>
    /// <param name="file">
    /// True creates an ordinary file; false creates an empty inventory directory.
    /// </param>
    /// <returns>
    /// Completion after the next actual composed admission refuses unchanged inventory evidence.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppearingWorkerInventoryIsFreshlyRefused(bool file)
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        main.Validate(null);
        Assert.True(files.CaptureAdmissionDirectoryStatistics().Hits > 0);
        var path = Path.Combine(Root, ".boe_runtime", "worker-runs-v1");
        if (file) File.WriteAllText(path, "owned inventory evidence"); else Directory.CreateDirectory(path);
        Assert.Throws<IOException>(() => main.Validate(null));
        if (file) Assert.Equal("owned inventory evidence", File.ReadAllText(path));
        else Assert.Empty(Directory.GetFileSystemEntries(path));
        Assert.Null(files.CanonicalRootAuthorityIdentity.WorkerContext);
    }

    /// <summary>
    /// Requires native handle authentication even when every shared ancestor is already cached.
    /// </summary>
    [Fact]
    public void CachedDirectoriesCannotAuthenticateTheWrongGuardHandle()
    {
        var expected = Path.Combine(Root, "expected.lock");
        var other = Path.Combine(Root, "other.lock");
        File.WriteAllText(expected, "expected");
        File.WriteAllText(other, "other");
        using var valid = new GmMainOwnerGuard(Root, expected, File.OpenRead(expected));
        using var wrong = new GmMainOwnerGuard(Root, expected, File.OpenRead(other));
        using var observations = new AdmissionDirectoryObservations(Root);
        valid.Validate(observations);
        valid.Validate(observations);
        Assert.True(observations.CaptureStatistics().Hits > 0);
        Assert.Throws<InvalidDataException>(() => wrong.Validate(observations));
    }

    /// <summary>
    /// Checks changed directory kinds on the next actual admission after prior positive directory DATA.
    /// </summary>
    /// <param name="link">
    /// True installs an owned junction; false replaces the directory with an ordinary file.
    /// </param>
    /// <returns>
    /// Completion after actual refusal and removal of the owned junction when one was created.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NextAdmissionRefusesReplacedMetadataDirectory(bool link)
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"),
            Guid.Empty.ToString("N"), 1, GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "fixture");
        var bytes = GmSessionRunRecordCodec.Encode(new(1, identity, GmSessionRunDisposition.Stopped,
            new(identity, GmSessionRunStopKind.OwnedScopeEmpty, identity.BootId)));
        var directory = Path.Combine(Root, ".boe_runtime", "gm-runs");
        Directory.CreateDirectory(directory);
        var record = Path.Combine(directory, "main.json");
        File.WriteAllBytes(record, bytes);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
        main.Validate(null);
        Assert.True(files.CaptureAdmissionDirectoryStatistics().Hits > 0);
        File.Delete(record);
        Directory.Delete(directory);
        var target = Path.Combine(_sandbox, "link-target");
        Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "main.json"), bytes);
        try
        {
            if (link)
            {
                var start = new ProcessStartInfo("cmd.exe")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("/d"); start.ArgumentList.Add("/c");
                start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J");
                start.ArgumentList.Add(directory); start.ArgumentList.Add(target);
                using var child = Process.Start(start)!;
                var stdout = child.StandardOutput.ReadToEndAsync();
                var stderr = child.StandardError.ReadToEndAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await child.WaitForExitAsync(deadline.Token); }
                finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
                Assert.True(child.ExitCode == 0, await stdout + await stderr);
            }
            else File.WriteAllText(directory, "owned blocker");
            Assert.Throws<InvalidDataException>(() => main.Validate(null));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(target, "main.json")));
        }
        finally { if (link && Directory.Exists(directory)) Directory.Delete(directory); }
    }

    /// <summary>
    /// Measures actual composed guard, inventory and persistence helpers while every later admission remains fresh.
    /// </summary>
    /// <returns>
    /// Completion after acquiring and checking the actual ordinary guard.
    /// </returns>
    [Fact]
    public async Task ComposedAdmissionReusesDirectoriesAndNextCallStillQueriesThem()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
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
    /// <returns>
    /// Completion after acquiring and checking the actual ordinary guard.
    /// </returns>
    [Fact]
    public async Task CurrentMainRecordAndUnexpectedNamespaceEntryRemainFresh()
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
        await main.AcquireAsync();
        main.Validate(null);
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped with { Disposition = GmSessionRunDisposition.Running, StopEvidence = null }));
        Assert.Throws<IOException>(() => main.Validate(null));
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped));
        main.Validate(null);
        File.WriteAllText(path, "{");
        Assert.Throws<InvalidDataException>(() => main.Validate(null));
        File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => main.Validate(null));
        File.WriteAllBytes(path, GmSessionRunRecordCodec.Encode(stopped));
        File.WriteAllText(Path.Combine(directory, "unexpected.json"), "{}");
        Assert.Throws<IOException>(() => main.Validate(null));
    }

    /// <summary>
    /// Reports named uncached and cached costs on the genuine composed path, without flaky wall-time assertions.
    /// </summary>
    /// <returns>
    /// Completion after acquiring and checking the actual ordinary guard.
    /// </returns>
    [Fact]
    public async Task ComposedPathMeasurementsPreserveTheSameNumberOfAdmissions()
    {
        var files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        using var main = files.BeginMainAdmission();
        await main.AcquireAsync();
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
