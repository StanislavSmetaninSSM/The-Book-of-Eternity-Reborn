using System.ComponentModel;
using System.Runtime.InteropServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerSyntheticBundlePublicationTests
{
    private GmWorkerProposalStore AdmittedStore() =>
        new GmWorkerNativePoolAdmission(Path.Combine(_root, "unused-package"), _root).CreateProposalStore(_fs);

    private string Stage()
    {
        var source = Path.Combine(_fs.CreateRuntimeProposalStagingRoot(), _proposal.ProposalId);
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "proposal.json"), ProposalBytes);
        return source;
    }

    [Fact]
    public async Task WrongFixtureRoot_CannotPublishIntoThisManager()
    {
        var other = Path.Combine(_root, "other"); Directory.CreateDirectory(other);
        Assert.Throws<InvalidOperationException>(() => new GmWorkerNativePoolAdmission(other, other).CreateProposalStore(_fs));
        var source = Stage();
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, other, source, BundlePath));
        AssertSourceRetained(source);
    }

    [Theory]
    [InlineData("game_state/escape")]
    [InlineData("worker_proposals/inbox")]
    [InlineData("worker_proposals/nested/escape")]
    [InlineData("worker_proposals/Upper")]
    public async Task DestinationOutsideSingleSafeBundle_IsRejected(string destination)
    {
        var source = Stage();
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, _root, source, destination));
        AssertSourceRetained(source);
    }

    [Theory]
    [InlineData("area")]
    [InlineData("outside")]
    [InlineData("different-bundle")]
    [InlineData("traversal")]
    public async Task SourceMustBeMatchingBundleInsidePrivateStaging(string fault)
    {
        var source = Stage();
        var candidate = fault switch
        {
            "area" => Path.Combine(_fs.RuntimeRootPath, "proposal-staging"),
            "outside" => Path.Combine(_root, "outside"),
            "different-bundle" => Path.Combine(Path.GetDirectoryName(source)!, "different_bundle"),
            _ => Path.Combine(source, "..", _proposal.ProposalId)
        };
        if (fault is "outside" or "different-bundle") Directory.CreateDirectory(candidate);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, _root, candidate, BundlePath));
        AssertSourceRetained(source);
    }

    [Theory]
    [InlineData("file-link")]
    [InlineData("directory-link")]
    [InlineData("fifo")]
    public async Task InvalidNestedTreeEntryAtFinalBoundary_IsRejectedWithoutFollowingIt(string kind)
    {
        var source = Stage();
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel.bin"); File.WriteAllBytes(sentinel, _content);
        _afterBoundary = () =>
        {
            var nested = Path.Combine(source, "nested"); Directory.CreateDirectory(nested);
            var entry = Path.Combine(nested, "entry");
            if (kind == "file-link") File.CreateSymbolicLink(entry, sentinel);
            else if (kind == "directory-link") Directory.CreateSymbolicLink(entry, outside);
            else Assert.Equal(0, MkFifo(entry, 384));
            return Task.CompletedTask;
        };
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, _root, source, BundlePath));
        AssertSourceRetained(source);
        Assert.Equal(_content, File.ReadAllBytes(sentinel));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationArrivingAtFinalStoreBoundary_IsNeverReplaced(bool nonempty)
    {
        var destination = _fs.ResolvePath(BundlePath);
        _afterBoundary = () =>
        {
            Directory.CreateDirectory(destination);
            if (nonempty) File.WriteAllBytes(Path.Combine(destination, "sentinel.bin"), _content);
            return Task.CompletedTask;
        };
        var result = await Publish(AdmittedStore());
        Assert.False(result.Published);
        Assert.True(Directory.Exists(destination));
        if (nonempty) Assert.Equal(_content, File.ReadAllBytes(Path.Combine(destination, "sentinel.bin")));
        else Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        Assert.False(File.Exists(_fs.ResolvePath(InboxPath)));
        Assert.False(File.Exists(_fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
    }

    [Theory]
    [InlineData("disposed")]
    [InlineData("pending")]
    [InlineData("legacy")]
    public async Task InvalidatedLeaseAfterAwait_CannotReachNativeMove(string fault)
    {
        var source = Stage();
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        _afterBoundary = async () =>
        {
            if (fault == "disposed") await lease.DisposeAsync();
            else if (fault == "pending") lease.PendingLocalDecision = new object();
            else lease.IsLegacyStorageRecovery = true;
        };
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, _root, source, BundlePath));
            AssertSourceRetained(source);
        }
        finally { lease.PendingLocalDecision = null; lease.IsLegacyStorageRecovery = false; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleTaskOrGeneration_RetainsExistingEmptyStructure(bool generation)
    {
        var empty = Path.Combine(_fs.ResolvePath(BundlePath), "empty"); Directory.CreateDirectory(empty);
        var generationBytes = File.ReadAllBytes(_fs.SessionGenerationPath);
        var result = await Publish(AdmittedStore(), generation ? Guid.NewGuid().ToString("N") : null,
            generation ? null : [99]);
        Assert.False(result.Published);
        Assert.Equal(generation, result.SessionReplaced);
        Assert.True(Directory.Exists(empty));
        Assert.Equal(generationBytes, File.ReadAllBytes(_fs.SessionGenerationPath));
        Assert.Equal(_taskBytes, File.ReadAllBytes(_fs.ResolvePath(TaskPath)));
        Assert.False(File.Exists(_fs.ResolvePath(InboxPath)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeRename_ExistingDirectoryActuallyFailsWithoutOverwrite(bool nonempty)
    {
        var source = Stage();
        var destination = _fs.ResolvePath(BundlePath); Directory.CreateDirectory(destination);
        if (nonempty) File.WriteAllBytes(Path.Combine(destination, "sentinel.bin"), _content);
        var error = Assert.Throws<IOException>(() => LinuxCreateOnlyDirectoryRename.Move(source, destination));
        Assert.Equal(17, Assert.IsType<Win32Exception>(error.InnerException).NativeErrorCode);
        Assert.Equal(ProposalBytes, File.ReadAllBytes(Path.Combine(source, "proposal.json")));
        if (nonempty) Assert.Equal(_content, File.ReadAllBytes(Path.Combine(destination, "sentinel.bin")));
        else Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Fact]
    public async Task ForeignManagerLease_DoesNotAuthorizeThisFixture()
    {
        var source = Stage();
        var other = new FileSystemManager(Path.Combine(_root, "other"), NullLogger<FileSystemManager>.Instance);
        other.EnsureDirectoryStructure();
        await using var lease = await other.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, _root, source, BundlePath));
        AssertSourceRetained(source);
    }

    [Fact]
    public async Task CancellationWhileWaitingForCanonicalLease_PublishesNothing()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () => { reached.TrySetResult(); return Task.CompletedTask; }
            });
        var store = new GmWorkerNativePoolAdmission(Path.Combine(_root, "unused-package"), _root).CreateProposalStore(waiting);
        using var cancellation = new CancellationTokenSource();
        await using var blocking = await _fs.AcquireCanonicalWriteLeaseAsync();
        var publication = Publish(store, cancellationToken: cancellation.Token);
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publication.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            cancellation.Cancel();
            await blocking.DisposeAsync();
            try { await publication.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
        }
        Assert.False(Directory.Exists(_fs.ResolvePath(BundlePath)));
        Assert.False(File.Exists(_fs.ResolvePath(InboxPath)));
        Assert.Equal(_taskBytes, File.ReadAllBytes(_fs.ResolvePath(TaskPath)));
    }

    [Fact]
    public void NativeRename_MissingSourceReportsErrnoWithoutCreatingDestination()
    {
        var destination = Path.Combine(_root, "never-published");
        var error = Assert.Throws<IOException>(() => LinuxCreateOnlyDirectoryRename.Move(Path.Combine(_root, "missing"), destination));
        Assert.Equal(2, Assert.IsType<Win32Exception>(error.InnerException).NativeErrorCode);
        Assert.False(Directory.Exists(destination));
    }

    private void AssertSourceRetained(string source)
    {
        Assert.Equal(ProposalBytes, File.ReadAllBytes(Path.Combine(source, "proposal.json")));
        Assert.False(Directory.Exists(_fs.ResolvePath(BundlePath)));
        Assert.False(File.Exists(_fs.ResolvePath(InboxPath)));
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int MkFifo(string path, uint mode);
}
