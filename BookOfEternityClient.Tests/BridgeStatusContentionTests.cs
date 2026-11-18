using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityGMBridge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BridgeStatusContentionTests : IDisposable
{
    private const string Status = "game_state/control/gm_bridge_status.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-status-contention-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _before = "{\"ready\":true,\"marker\":\"before\"}"u8.ToArray();
    private readonly byte[] _after = "{\"ready\":false,\"marker\":\"after\"}"u8.ToArray();
    private FileSystemManager Create(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    private async Task<FileSystemManager> SeedAsync(FileSystemManagerHooks? hooks = null)
    {
        var files = Create(hooks); files.EnsureDirectoryStructure();
        await files.WriteFileAtomicBytesAsync(Status, _before);
        return files;
    }

    [Fact]
    public async Task RealHeldLockRefusal_IsOriginBoundAndCannotBeReusedAfterAcquisition()
    {
        Exception? lateFailure = null;
        var files = await SeedAsync(new FileSystemManagerHooks {
            AfterCanonicalWriteLockOpenedAsync = () => lateFailure == null ? Task.CompletedTask : Task.FromException(lateFailure)
        });
        Exception failure;
        using (var held = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            failure = (await Record.ExceptionAsync(async () => { await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); }))!;
            Assert.NotNull(failure);
            // The genuine constructor control establishes the supported platform sharing code.
            var constructorFailure = failure.InnerException ?? failure;
            if (OperatingSystem.IsLinux()) Assert.Equal(11, constructorFailure.HResult);
            else
            {
                Assert.True(OperatingSystem.IsWindows(), "Sharing classification is qualified only for Linux and Windows.");
                Assert.Contains(constructorFailure.HResult & 0xFFFF, new[] { 32, 33 });
            }
            Assert.Equal(_before, File.ReadAllBytes(files.ResolvePath(Status)));
        }
        // Later code surfacing the original failure must not be treated as an acquisition deferral.
        lateFailure = failure;
        Assert.Same(failure, await Record.ExceptionAsync(() => files.TryPublishGmBridgeStatusAsync(_after)));
        lateFailure = null;
        await using (var fresh = await files.AcquireCanonicalWriteLeaseAsync()) Assert.True(fresh.IsActive);
        Assert.True(files.IsOriginalCanonicalLockContention(failure));
        Assert.False(Create().IsOriginalCanonicalLockContention(failure));
        Assert.False(files.IsOriginalCanonicalLockContention(new IOException("wrapped", failure)));
        Assert.Equal(_before, File.ReadAllBytes(files.ResolvePath(Status)));
    }

    [Fact]
    public async Task RealHeldLock_StatusWriterReturnsNoWriteOnlyAfterOriginalAcquisitionUnwinds()
    {
        var files = await SeedAsync();
        using (var held = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(await files.TryPublishGmBridgeStatusAsync(_after));
            Assert.Equal(_before, File.ReadAllBytes(files.ResolvePath(Status)));
        }
        Assert.True(await files.TryPublishGmBridgeStatusAsync(_after));
        Assert.Equal(_after, File.ReadAllBytes(files.ResolvePath(Status)));
    }

    [Theory]
    [InlineData("before-open")]
    [InlineData("after-open")]
    [InlineData("publication")]
    [InlineData("close")]
    public async Task OtherBoundaryFailures_AreNeverReportedAsNoWriteContention(string stage)
    {
        var armed = false;
        var sentinel = new SharingShapedFailure();
        var closer = new ThrowingClose(sentinel);
        var files = await SeedAsync(new FileSystemManagerHooks {
            BeforeCanonicalWriteLockOpenAsync = () => armed && stage == "before-open" ? Task.FromException(sentinel) : Task.CompletedTask,
            AfterCanonicalWriteLockOpenedAsync = () => armed && stage == "after-open" ? Task.FromException(sentinel) : Task.CompletedTask,
            LocalPublicationObserver = (phase, _) => { if (armed && stage == "publication" && phase == TrustedLocalPublicationPhase.MemberPublished) throw sentinel; },
            OrdinaryWriteLeaseCloseContextFactory = () => armed && stage == "close" ? closer : null
        });
        armed = true;
        var failure = await Record.ExceptionAsync(() => files.TryPublishGmBridgeStatusAsync(_after));
        Assert.NotNull(failure);
        Assert.True(ContainsOriginal(failure, sentinel));
        Assert.False(files.IsOriginalCanonicalLockContention(failure));
        if (stage == "before-open" || stage == "after-open") Assert.Equal(_before, File.ReadAllBytes(files.ResolvePath(Status)));
        if (stage == "close") { Assert.Equal(1, closer.Calls); Assert.Equal(_after, File.ReadAllBytes(files.ResolvePath(Status))); }
    }

    [Fact]
    public async Task CancelledAcquisition_IsNotContentionAndLeavesOriginalBytes()
    {
        var files = await SeedAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = await Record.ExceptionAsync(async () => {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync(cancellationToken: cancellation.Token);
        });
        Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.False(files.IsOriginalCanonicalLockContention(failure!));
        Assert.Equal(_before, File.ReadAllBytes(files.ResolvePath(Status)));
    }

    [Theory]
    [InlineData(false, false, false, (int)BridgeHost.StatusBackpressureDecision.Refuse)]
    [InlineData(false, false, true, (int)BridgeHost.StatusBackpressureDecision.Refuse)]
    [InlineData(true, true, false, (int)BridgeHost.StatusBackpressureDecision.DiscardSealed)]
    [InlineData(true, true, true, (int)BridgeHost.StatusBackpressureDecision.DiscardSealed)]
    [InlineData(true, false, true, (int)BridgeHost.StatusBackpressureDecision.KeepNewer)]
    [InlineData(true, false, false, (int)BridgeHost.StatusBackpressureDecision.RestoreRefused)]
    public void DeferredSnapshotDecision_IsOnlyBookkeeping(bool succeeded, bool sealedPublication, bool pending,
        int expected) =>
        Assert.Equal((BridgeHost.StatusBackpressureDecision)expected, BridgeHost.DecideStatusBackpressure(succeeded, sealedPublication, pending));

    private static bool ContainsOriginal(Exception? error, Exception original) =>
        ReferenceEquals(error, original) || error is AggregateException aggregate && aggregate.InnerExceptions.Any(item => ContainsOriginal(item, original)) ||
        error?.InnerException != null && ContainsOriginal(error.InnerException, original);
    private sealed class SharingShapedFailure : IOException { internal SharingShapedFailure() : base("non-acquisition sentinel") { HResult = unchecked((int)0x80070020); } }
    private sealed class ThrowingClose(Exception failure) : IDisposable { internal int Calls; public void Dispose() { Calls++; throw failure; } }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
