using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableCoordinatedLeaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-coordinated-lease-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;

    public PortableCoordinatedLeaseTests()
    {
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _files.EnsureDirectoryStructure();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ZeroWriteBranchesRejectForeignOrDisposedLease(bool foreign, bool guardOnly)
    {
        var owner = foreign
            ? new FileSystemManager(Path.Combine(_root, "foreign"), NullLogger<FileSystemManager>.Instance)
            : _files;
        await using var lease = await owner.AcquireCanonicalWriteLeaseAsync();
        if (!foreign) await lease.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoordinatedStateWriteHelper.TryCommitAsync(_files, lease, NoWrites(guardOnly)));

        Assert.False(File.Exists(_files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidZeroWriteBranchesDoNotCreateGenerationOrPublication(bool guardOnly)
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();

        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files, lease, NoWrites(guardOnly)));

        Assert.False(File.Exists(_files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1")));
    }

    [Fact]
    public async Task RealLeaseContextFailureStillClosesLockAndAllowsReacquisition()
    {
        var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var retainedStream = RetainOwnedStreamForFailureCleanup(lease);
        var failure = new IOException("Injected owned context release failure.");
        lease.ExternalPublicationContext = new ThrowingContext(failure);
        try
        {
            var error = await Record.ExceptionAsync(() => lease.DisposeAsync().AsTask());
            Assert.Same(failure, error);
            Assert.False(lease.IsActive);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var reacquired = await _files.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
            Assert.True(reacquired.IsActive);
        }
        finally
        {
            // Current context-first disposal can drop its stream reference.
            // Close that real test-owned handle even when the RED assertion fails.
            await retainedStream.DisposeAsync();
            await lease.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedOwnedReleaseCannotReplacePrimaryOperationFailure(bool uncertain)
    {
        var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var retainedStream = RetainOwnedStreamForFailureCleanup(lease);
        Exception primary = uncertain
            ? new CoordinatedStatePublicationUncertainException(new IOException("Unknown member bytes."))
            : new InvalidDataException("Admission failed before publication.");
        var releaseFailure = new IOException("Injected owned context release failure.");
        lease.ExternalPublicationContext = new ThrowingContext(releaseFailure);
        try
        {
            var error = await Record.ExceptionAsync(async () =>
            {
                try { throw primary; }
                finally
                {
                    // The exact release routine used by the owned helper path;
                    // this does not simulate an OS FileStream disposal fault.
                    await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_files, lease,
                        completed: false, operationFailure: primary);
                }
            });
            Assert.Same(primary, error);
            Assert.Same(releaseFailure, primary.Data["CoordinatedLeaseReleaseFailure"]);
        }
        finally
        {
            await retainedStream.DisposeAsync();
            await lease.DisposeAsync();
        }
    }

    [Fact]
    public async Task SharedOwnedReleaseKeepsCompletedDispositionOnContextFailure()
    {
        var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var retainedStream = RetainOwnedStreamForFailureCleanup(lease);
        lease.ExternalPublicationContext = new ThrowingContext(new IOException("Injected completed release failure."));
        try
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_files, lease,
                completed: true, operationFailure: null);
        }
        finally
        {
            await retainedStream.DisposeAsync();
            await lease.DisposeAsync();
        }
    }

    private static CoordinatedStateWriteHelper.PlannedWrite[] NoWrites(bool guardOnly) => guardOnly
        ? [new("game_state/meta/unused_guard.json", null, null, GuardOnly: true)]
        : [];

    private static FileStream RetainOwnedStreamForFailureCleanup(FileSystemManager.CanonicalWriteLease lease) =>
        Assert.IsType<FileStream>(typeof(FileSystemManager.CanonicalWriteLease)
            .GetField("_stream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lease));

    private sealed class ThrowingContext(Exception failure) : IDisposable
    {
        public void Dispose() => throw failure;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
