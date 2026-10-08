using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserLocalWriteCoordinatorTests
{
    [Fact]
    public async Task ExecuteAtomicAsync_WarmRollbackDynamicDirectoryUsesOriginalCleanupAfterRecorderCleared()
    {
        Assert.True(OperatingSystem.IsWindows(), "This fixture qualifies the original physical handler on native Windows.");
        const string tracked = "game_state/meta/warm_directory_rollback.bin";
        const string snapshot = BrowserPendingTurnInspector.PendingTurnSnapshotDirectory;
        byte[] before = [0xEF, 0xBB, 0xBF, 0xFF, 0];
        FileSystemManager.CanonicalWriteLease? captured = null;
        FileSystemManager? files = null;
        var reached = false; var treePublications = 0;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = relative =>
                {
                    if (relative != snapshot) return Task.CompletedTask;
                    Assert.NotNull(captured); Assert.True(captured.IsActive);
                    Assert.Null(captured.MutationIntentRecorder); Assert.False(captured.IsLegacyStorageRecovery);
                    reached = true; return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                    using var journal = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
                    if (journal.RootElement.GetProperty("Members").EnumerateArray().Any(member =>
                        member.GetProperty("Path").GetString()!.StartsWith(files.ResolvePath(snapshot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                        treePublications++;
                }
            });
        await files.WriteFileAtomicBytesAsync(tracked, before);
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files, _timeProvider), _timeProvider);
        var result = await coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "warm dynamic directory rollback"), [tracked],
            async lease =>
            {
                captured = lease; Assert.NotNull(lease.MutationIntentRecorder);
                await files.WriteFileAtomicBytesAsync(lease, tracked, [2]);
                await files.WriteFileAtomicBytesAsync(lease, snapshot + "/nested/member.bin", [42]);
                throw new InvalidOperationException("deliberate warm directory rollback");
            }, rollbackCleanupDirectories: [snapshot]);
        Assert.False(result.Success); Assert.Contains("deliberate warm directory rollback", result.Message);
        Assert.True(reached); Assert.Equal(0, treePublications);
        Assert.Equal(before, File.ReadAllBytes(files.ResolvePath(tracked)));
        Assert.False(Directory.Exists(files.ResolvePath(snapshot)));
        Assert.False(Directory.Exists(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
        Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
    }
}
