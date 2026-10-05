using System.Runtime.InteropServices;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Synthetic filesystem/evidence only; no process, host, worker or GM is started.
public sealed class GmWorkerQuarantineAuditReceiptTests
{
    private const string Generation = "generation-original";
    private static WorkerAuditEvent Event => new()
    {
        EventId = "cleanup-stable", EventType = "process-tree-cleanup-confirmed",
        WorkerId = "worker", TaskId = "task", TimestampUtc = "2026-10-05T00:00:00Z",
        Summary = "Снег <ready>\n", Details = new Dictionary<string, IReadOnlyList<string>> { ["reason"] = ["old", "new"] }
    };
    private static byte[] Expected => Encoding.UTF8.GetBytes(
        """{"schemaVersion":1,"sessionGeneration":"generation-original","auditEvent":{"schemaVersion":1,"eventId":"cleanup-stable","eventType":"process-tree-cleanup-confirmed","workerId":"worker","taskId":"task","timestampUtc":"2026-10-05T00:00:00Z","summary":"Снег <ready>\n","details":{"reason":["old","new"]}}}""");

    [Fact]
    public async Task ExactReceiptAndStableRetrySurviveDetachedWorkspaceDeletion()
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        var sibling = Path.Combine(fixture.RuntimeRoot, "unrelated");
        File.WriteAllText(sibling, "keep");
        await workspace.DeleteDetachedSessionRetainingRuntimeAuthorityAsync();
        Assert.False(Directory.Exists(workspace.GameSessionPath));
        await workspace.PersistQuarantineAuditReceiptAsync(Generation, Event);
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(fixture.Receipt, timestamp);
        await workspace.PersistQuarantineAuditReceiptAsync(Generation, Event);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(fixture.Receipt));
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        Assert.Single(Directory.GetFiles(fixture.ReceiptDirectory));
        await workspace.DisposeAsync();
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        Assert.Equal("keep", File.ReadAllText(sibling));
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("event")]
    [InlineData("whitespace")]
    [InlineData("longer")]
    public async Task ExistingIdentityRejectsDifferentExactBytesWithoutReplacement(string kind)
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        Directory.CreateDirectory(fixture.ReceiptDirectory);
        var prior = kind == "whitespace" ? Expected.Concat(new byte[] { 10 }).ToArray()
            : kind == "longer" ? new byte[128 * 1024] : Expected;
        File.WriteAllBytes(fixture.Receipt, prior);
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(
            kind == "generation" ? "replacement" : Generation,
            kind == "event" ? Event with { Summary = "different" } : Event));
        Assert.Equal(prior, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingGenerationCannotCreateReceipt(string generation)
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.PersistQuarantineAuditReceiptAsync(generation, Event));
        Assert.False(Directory.Exists(fixture.ReceiptDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../escape")]
    [InlineData("nested/name")]
    [InlineData("bad\0name")]
    public async Task UnsafeEventIdentityCannotCreateReceipt(string eventId)
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event with { EventId = eventId }));
        Assert.False(Directory.Exists(fixture.ReceiptDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialTemporaryFailurePreservesExceptionCleansTempAndRetries(bool cancelled)
    {
        if (!OperatingSystem.IsLinux()) return; // Native Linux portable stream/cleanup contract.
        using var fixture = new Fixture();
        var expected = Failure(cancelled);
        var fail = true;
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditTempCreatedAsync = path =>
            {
                if (!fail) return Task.CompletedTask;
                using (var partial = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    partial.Write([1, 2, 3]);
                return Task.FromException(expected);
            }
        });
        Assert.Same(expected, await Record.ExceptionAsync(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event)));
        Assert.False(File.Exists(fixture.Receipt));
        fixture.AssertNoTemps();
        fail = false;
        await workspace.PersistQuarantineAuditReceiptAsync(Generation, Event);
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StagedOrPublishedFailureRecoversExactReceipt(bool afterPublication, bool cancelled)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var expected = Failure(cancelled);
        var fail = true;
        Task Fail(string _) => fail ? Task.FromException(expected) : Task.CompletedTask;
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditStagedAsync = afterPublication ? null : Fail,
            AfterQuarantineAuditPublishedAsync = afterPublication ? Fail : null
        });
        Assert.Same(expected, await Record.ExceptionAsync(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event)));
        Assert.Equal(afterPublication, File.Exists(fixture.Receipt));
        if (afterPublication) Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
        fail = false;
        await workspace.PersistQuarantineAuditReceiptAsync(Generation, Event);
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        Assert.Single(Directory.GetFiles(fixture.ReceiptDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationAppearingBeforePublishIsComparedNeverOverwritten(bool same)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var raced = same ? Expected : Encoding.UTF8.GetBytes("conflicting receipt");
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditStagedAsync = _ => { File.WriteAllBytes(fixture.Receipt, raced); return Task.CompletedTask; }
        });
        var failure = await Record.ExceptionAsync(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
        if (same) Assert.Null(failure); else Assert.IsType<InvalidDataException>(failure);
        Assert.Equal(raced, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StagedBytesAreCheckedAgainstSuppliedReceiptBeforePublish(bool truncate)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditStagedAsync = path =>
            {
                var changed = truncate ? new byte[] { 0 } : Expected.ToArray();
                changed[0] ^= 1;
                File.WriteAllBytes(path, changed);
                return Task.CompletedTask;
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
        Assert.False(File.Exists(fixture.Receipt));
        fixture.AssertNoTemps();
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("symlink")]
    [InlineData("fifo")]
    public async Task NonRegularReceiptIsRejectedBeforeOpening(string kind)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        Directory.CreateDirectory(fixture.ReceiptDirectory);
        fixture.CreateUnsafe(fixture.Receipt, kind);
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
        fixture.AssertOutside();
        fixture.AssertNoTemps();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiptDirectoryOrRuntimeAncestorLinkCannotRedirectEvidence(bool ancestor)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        var path = ancestor ? fixture.RuntimeRoot : fixture.ReceiptDirectory;
        var moved = path + ".retained";
        if (ancestor) Directory.Move(path, moved);
        Directory.CreateSymbolicLink(path, fixture.OutsideDirectory);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
            fixture.AssertOutside();
            Assert.Single(Directory.GetFiles(fixture.OutsideDirectory));
        }
        finally { Directory.Delete(path); if (ancestor) Directory.Move(moved, path); }
    }

    [Fact]
    public async Task TemporaryReplacedWithLinkBeforeWriteCannotModifyOutsideBytes()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        string? temp = null;
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditTempCreatedAsync = path =>
            {
                temp = path; File.Delete(path); File.CreateSymbolicLink(path, fixture.OutsideFile); return Task.CompletedTask;
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
        Assert.False(File.Exists(fixture.Receipt));
        Assert.NotNull(new FileInfo(temp!).LinkTarget); // Unsafe evidence is retained, never followed/removed.
        fixture.AssertOutside();
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("symlink")]
    [InlineData("fifo")]
    public async Task StagedReplacementKindIsRetainedWithoutPublication(string kind)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        string? temp = null;
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditStagedAsync = path =>
            {
                temp = path; File.Delete(path); fixture.CreateUnsafe(path, kind); return Task.CompletedTask;
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
        Assert.False(File.Exists(fixture.Receipt));
        Assert.Contains(temp!, Directory.GetFileSystemEntries(fixture.ReceiptDirectory));
        fixture.AssertOutside();
    }

    [Fact]
    public async Task ExistingOrdinaryHardlinkAllowsSameBytesAndNeverChangesOutsideAlias()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        Directory.CreateDirectory(fixture.ReceiptDirectory);
        var outside = Path.Combine(fixture.Root, "exact-receipt-outside");
        File.WriteAllBytes(outside, Expected);
        Assert.Equal(0, Link(outside, fixture.Receipt));
        await workspace.PersistQuarantineAuditReceiptAsync(Generation, Event);
        await Assert.ThrowsAsync<InvalidDataException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event with { Summary = "different" }));
        Assert.Equal(Expected, File.ReadAllBytes(outside));
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public async Task ReaperRetainsAuthorityCapacityAndSlotUntilExactTerminalReceiptRetry(
        bool cancelled, bool afterPublication, bool canonicalUnavailable)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var fail = true;
        var expected = Failure(cancelled);
        Task Fail(string _) => fail ? Task.FromException(expected) : Task.CompletedTask;
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditTempCreatedAsync = afterPublication ? null : Fail,
            AfterQuarantineAuditPublishedAsync = afterPublication ? Fail : null
        });
        var slot = new Slot();
        var failures = new List<Exception>();
        var appendCalls = 0;
        var owner = Owner(workspace, slot, true,
            () => { appendCalls++; return Task.FromResult(canonicalUnavailable
                ? GmWorkerAuditAppendDisposition.CanonicalAuditUnavailable : GmWorkerAuditAppendDisposition.SessionReplaced); },
            error => { failures.Add(error); return Task.CompletedTask; });
        var reaper = new GmWorkerQuarantineReaper(1, [], runInBackground: false);
        using var reservation = reaper.TryReserve();
        Assert.NotNull(reservation);
        reservation.Transfer(owner);
        await reaper.RunPassAsync();
        Assert.Same(expected, Assert.Single(failures));
        Assert.False(Directory.Exists(workspace.GameSessionPath));
        Assert.Equal(0, slot.Disposals);
        Assert.Equal(1, reaper.EntryCount);
        Assert.Equal(1, reaper.OwnedCapacity);
        Assert.Equal(afterPublication, File.Exists(fixture.Receipt));
        if (afterPublication) Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
        fail = false;
        await reaper.RunPassAsync();
        await reaper.RunPassAsync();
        Assert.Equal(2, appendCalls);
        Assert.Equal(1, slot.Disposals);
        Assert.Equal(0, reaper.EntryCount);
        Assert.Equal(0, reaper.OwnedCapacity);
        Assert.Equal(Expected, File.ReadAllBytes(fixture.Receipt));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => workspace.PersistQuarantineAuditReceiptAsync(Generation, Event));
    }

    [Fact]
    public async Task PublishedTamperRemainsConflictAndCannotReleaseRetainedSlot()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var corrupted = Encoding.UTF8.GetBytes("conflicting published evidence");
        await using var workspace = await fixture.Create(new()
        {
            AfterQuarantineAuditPublishedAsync = path => { File.WriteAllBytes(path, corrupted); return Task.CompletedTask; }
        });
        var slot = new Slot();
        var owner = Owner(workspace, slot, true,
            () => Task.FromResult(GmWorkerAuditAppendDisposition.SessionReplaced), _ => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidDataException>(owner.CleanupConfirmedAsync);
        await Assert.ThrowsAsync<InvalidDataException>(owner.CleanupConfirmedAsync);
        Assert.Equal(0, slot.Disposals);
        Assert.Equal(corrupted, File.ReadAllBytes(fixture.Receipt));
        fixture.AssertNoTemps();
    }

    [Fact]
    public async Task CanonicalAppendSuccessNeedsNoFallbackAndReleasesExactlyOnce()
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        var slot = new Slot();
        var calls = 0;
        var owner = Owner(workspace, slot, true,
            () => { calls++; return Task.FromResult(GmWorkerAuditAppendDisposition.Appended); }, _ => Task.CompletedTask);
        await owner.CleanupConfirmedAsync();
        await owner.CleanupConfirmedAsync();
        Assert.Equal(1, calls);
        Assert.Equal(1, slot.Disposals);
        Assert.False(Directory.Exists(workspace.GameSessionPath));
        Assert.False(Directory.Exists(fixture.ReceiptDirectory));
    }

    [Fact]
    public async Task UnconfirmedOwnerCannotCleanWorkspaceRecordReceiptOrReleaseSlot()
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create();
        var slot = new Slot();
        var appendCalls = 0;
        var owner = Owner(workspace, slot, false,
            () => { appendCalls++; return Task.FromResult(GmWorkerAuditAppendDisposition.SessionReplaced); },
            _ => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(owner.CleanupConfirmedAsync);
        Assert.True(Directory.Exists(workspace.GameSessionPath));
        Assert.Equal(0, appendCalls);
        Assert.Equal(0, slot.Disposals);
        Assert.False(Directory.Exists(fixture.ReceiptDirectory));
    }

    private static Exception Failure(bool cancelled) => cancelled
        ? new OperationCanceledException("synthetic receipt cancellation") : new IOException("synthetic receipt failure");
    private static GmWorkerQuarantinedExecution Owner(GmWorkerExecutionWorkspace workspace, Slot slot, bool confirmed,
        Func<Task<GmWorkerAuditAppendDisposition>> append, Func<Exception, Task> failure) => new(
        "synthetic-owner", confirmed
            ? GmWorkerExecutionAuthority.NoLaunch(GmWorkerBridgeTestFixtures.AnalysisTask())
            : new GmWorkerExecutionAuthority(new("unconfirmed-fixture", GmWorkerBackend.NativeLineage,
                GmWorkerBackendSelector.NativeGuarantee), GmWorkerBridgeTestFixtures.AnalysisTask()),
        null, null, workspace, slot, null, null, Generation, Event, append, failure);
    private sealed class Slot : IDisposable
    {
        internal int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly List<int> _fifoKeepers = [];
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-quarantine-receipt-" + Guid.NewGuid().ToString("N"));
        internal FileSystemManager Fs { get; }
        private string RuntimeBase => Path.Combine(Root, "runtime");
        internal string RuntimeRoot => GmWorkerExecutionWorkspace.ResolveRuntimeRoot(Fs.BasePath, RuntimeBase);
        internal string ReceiptDirectory => Path.Combine(RuntimeRoot, GmWorkerExecutionWorkspace.QuarantineAuditDirectoryName);
        internal string Receipt => Path.Combine(ReceiptDirectory, Event.EventId + ".json");
        internal string OutsideDirectory => Path.Combine(Root, "outside");
        internal string OutsideFile => Path.Combine(OutsideDirectory, "unchanged");
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            Fs = new FileSystemManager(Path.Combine(Root, "canonical"), NullLogger<FileSystemManager>.Instance);
            Fs.EnsureDirectoryStructure();
            File.WriteAllText(Path.Combine(Fs.GameSessionPath, "canonical-marker"), "canonical-before");
            Directory.CreateDirectory(OutsideDirectory);
            File.WriteAllText(OutsideFile, "outside-before");
        }
        internal Task<GmWorkerExecutionWorkspace> Create(GmWorkerExecutionWorkspaceHooks? hooks = null) =>
            GmWorkerExecutionWorkspace.CreateAsync(Fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { ContextFiles = [] }, CancellationToken.None, hooks, RuntimeBase);
        internal void AssertNoTemps() => Assert.Empty(Directory.GetFileSystemEntries(ReceiptDirectory, "*.tmp.*"));
        internal void AssertOutside() => Assert.Equal("outside-before", File.ReadAllText(OutsideFile));
        internal void CreateUnsafe(string path, string kind)
        {
            if (kind == "directory") Directory.CreateDirectory(path);
            else if (kind == "symlink") File.CreateSymbolicLink(path, OutsideFile);
            else
            {
                Assert.Equal(0, MkFifo(path, 0x180));
                // Keep both FIFO ends open so the old unsafe reader fails on
                // non-seekable length instead of hanging before causal RED.
                var fd = Open(path, 0x802); // O_RDWR | O_NONBLOCK, this owned FIFO only.
                Assert.True(fd >= 0);
                _fifoKeepers.Add(fd);
            }
        }
        public void Dispose()
        {
            try { Assert.Equal("canonical-before", File.ReadAllText(Path.Combine(Fs.GameSessionPath, "canonical-marker"))); }
            finally
            {
                foreach (var fd in _fifoKeepers) Close(fd);
                if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
            }
        }
    }
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MkFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int fd);
}
