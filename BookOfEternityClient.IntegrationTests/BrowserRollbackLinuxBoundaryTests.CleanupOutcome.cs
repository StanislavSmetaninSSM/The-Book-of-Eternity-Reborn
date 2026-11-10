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
    public Task CleanupUnknown_OriginalBrowserBoundaryStopsContinuation(string mode) => RunCleanupBrowserAsync(mode);

    [Theory]
    [InlineData("committed_release")]
    [InlineData("restored_release")]
    public Task CleanupUnknown_OriginalBrowserReleaseRetainsEstablishedOutcome(string mode) => RunCleanupBrowserAsync(mode);

    [Fact]
    public async Task CleanupUnknown_OriginalPreparedOutcomeStopsLockRelease()
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
        byte[]? lockBefore = null;
        var applied = false;
        cut.Select = (path, _) => path == memberPath;
        var result = await coordinator.ExecutePreparedAsync(Request, _ =>
        {
            lockBefore = File.ReadAllBytes(lockPath);
            cut.Armed = true;
            return Task.FromResult(new PreparedBrowserLocalWrite(
                [new(Member, Before, After)], () => { applied = true; return Task.CompletedTask; }));
        });
        output.WriteLine(JsonSerializer.Serialize(new { mode = "prepared_unknown", result, applied,
            lockBefore, RetainedLock = CleanupPublicationCut.ReadOptional(lockPath), Cut = cut.Evidence() }));
        // This original consumer uses the actual returned publication outcome, not RequireCommitted.
        cut.AssertReachedAndStopped(requireTypedUncertainty: false);
        Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        Assert.True(result.NeedsFollowUp); Assert.False(applied);
        Assert.NotNull(lockBefore); Assert.Equal(lockBefore, File.ReadAllBytes(lockPath));
    }

    private async Task RunCleanupBrowserAsync(string mode)
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
        var releaseCut = mode.EndsWith("_release", StringComparison.Ordinal);
        var committed = mode.StartsWith("committed_", StringComparison.Ordinal);
        var rollbackCallbackAtCut = 0;
        if (mode == "committed_release")
            cut.ObserveBeforeCut = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var metadata = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                Assert.True(metadata.RootElement.GetProperty("Committed").GetBoolean());
                foreach (var member in metadata.RootElement.GetProperty("Members").EnumerateArray())
                {
                    var path = member.GetProperty("Path").GetString()!;
                    if (path.EndsWith("browser_write_committed.marker", StringComparison.Ordinal) && member.GetProperty("After").GetProperty("Exists").GetBoolean())
                        committedMarker = File.ReadAllBytes(path);
                }
            };
        cut.Select = (path, member) => releaseCut
            ? path == lockPath && !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : mode == "committed_cleanup"
            ? path.EndsWith(".rollback", StringComparison.Ordinal) && !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : path == memberPath;
        cut.BeforeCut = () =>
        {
            rollbackCallbackAtCut = rollbackCallback;
            if (!releaseCut) lockBeforeCut = File.ReadAllBytes(lockPath);
            else
            {
                Assert.False(File.Exists(lockPath));
                Assert.Equal(committed ? After : Before, File.ReadAllBytes(memberPath));
                Assert.Equal(committed ? 0 : 1, rollbackCallbackAtCut);
                if (committed) Assert.NotNull(committedMarker);
            }
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
                lockBeforeCut = File.ReadAllBytes(lockPath);
                cut.Armed = mode == "operation";
                await files.WriteFileAtomicBytesAsync(lease, Member, After);
                cut.Armed = true;
                if (mode is "restoration" or "restored_release") throw businessFailure;
            }, prepareAfterRollback: () => () => rollbackCallback++));
        output.WriteLine(JsonSerializer.Serialize(new { mode, callbackReached, result, Failure = failure?.ToString(),
            rollbackCallback, rollbackCallbackAtCut, BusinessCauseRetained = cut.RetainsDiagnostic(businessFailure), lockBeforeCut, RetainedLock = CleanupPublicationCut.ReadOptional(lockPath), committedMarker,
            MemberBytes = CleanupPublicationCut.ReadOptional(memberPath), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped();
        Assert.True(callbackReached); Assert.Null(failure); Assert.NotNull(result);
        if (mode is "restoration" or "restored_release") Assert.True(cut.RetainsDiagnostic(businessFailure), "The actual uncertainty must retain the exact original callback failure.");
        Assert.Equal(mode == "restored_release" ? 1 : 0, rollbackCallbackAtCut);
        Assert.Equal(rollbackCallbackAtCut, rollbackCallback);
        Assert.Equal(releaseCut ? CleanupPublicationCut.Foreign : lockBeforeCut, File.ReadAllBytes(lockPath));
        Assert.True(result.NeedsFollowUp); Assert.True(result.ContinuationBlocked);
        Assert.Equal(committed, result.Success);
        Assert.Equal(committed ? BrowserPreparedWriteDisposition.Committed : mode == "restored_release"
            ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        if (committed) { Assert.NotNull(committedMarker); Assert.Equal(After, File.ReadAllBytes(memberPath)); }
        if (mode == "restored_release") Assert.Equal(Before, File.ReadAllBytes(memberPath));
    }
}
