using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Theory]
    [InlineData("operation")]
    [InlineData("restoration")]
    [InlineData("committed_cleanup")]
    public async Task CleanupUnknown_OriginalBrowserBoundaryStopsContinuation(string mode)
    {
        using var ownedFixture = new CleanupOwnedFixture(_root, line => output.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        await files.WriteFileAtomicBytesAsync(Member, Before);
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
        var memberPath = files.ResolvePath(Member);
        var lockPath = files.ResolvePath(LocalUiSessionLockService.LockPath);
        byte[]? lockBeforeCut = null;
        byte[]? committedMarker = null;
        var rollbackCallback = 0;
        var callbackReached = false;
        var businessFailure = new InvalidOperationException("known browser callback failure");
        cut.Select = (path, member) => mode == "committed_cleanup"
            ? path.EndsWith(".rollback", StringComparison.Ordinal) && !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : path == memberPath;
        cut.BeforeCut = () =>
        {
            lockBeforeCut = File.ReadAllBytes(lockPath);
            if (mode == "committed_cleanup")
            {
                var marker = Directory.GetFiles(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_committed.marker", SearchOption.AllDirectories);
                Assert.Single(marker);
                committedMarker = File.ReadAllBytes(marker[0]);
            }
        };
        BrowserLocalWriteResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await coordinator.ExecuteAtomicAsync(
            Request, [Member], async lease =>
            {
                callbackReached = true;
                cut.Armed = mode == "operation";
                await files.WriteFileAtomicBytesAsync(lease, Member, After);
                cut.Armed = true;
                if (mode == "restoration") throw businessFailure;
            }, prepareAfterRollback: () => () => rollbackCallback++));
        output.WriteLine(JsonSerializer.Serialize(new { mode, callbackReached, result, Failure = failure?.ToString(),
            rollbackCallback, BusinessCauseRetained = cut.RetainsDiagnostic(businessFailure), lockBeforeCut, RetainedLock = CleanupPublicationCut.ReadOptional(lockPath), committedMarker,
            MemberBytes = CleanupPublicationCut.ReadOptional(memberPath), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped();
        Assert.True(callbackReached); Assert.Null(failure); Assert.NotNull(result);
        if (mode == "restoration") Assert.True(cut.RetainsDiagnostic(businessFailure), "The actual uncertainty must retain the exact original callback failure.");
        Assert.Equal(0, rollbackCallback); Assert.Equal(lockBeforeCut, File.ReadAllBytes(lockPath));
        Assert.True(result.NeedsFollowUp); Assert.True(result.ContinuationBlocked);
        Assert.Equal(mode == "committed_cleanup", result.Success);
        Assert.Equal(mode == "committed_cleanup" ? BrowserPreparedWriteDisposition.Committed : BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        if (mode == "committed_cleanup") { Assert.NotNull(committedMarker); Assert.Equal(After, File.ReadAllBytes(memberPath)); }
    }
}
