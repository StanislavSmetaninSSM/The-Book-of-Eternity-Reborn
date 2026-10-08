using System.Runtime.InteropServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalUiGuard_BlocksOtherOwnerOrRefreshesExactOriginalToken(bool original)
    {
        var (files, coordinator) = await CreateAsync();
        var locks = new LocalUiSessionLockService(files);
        var acquired = await locks.AcquireOrRefreshAsync(new("existing-owner", "browser", "Fixture", TimeSpan.FromMinutes(2)), "original");
        Assert.True(acquired.Acquired); Assert.NotNull(acquired.Lease);
        var originalBytes = File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath));
        var ran = false;
        var request = original ? new BrowserLocalWriteRequest("existing-owner", "Fixture", "refresh", acquired.Lease) : Request;
        var result = await coordinator.ExecuteAtomicAsync(request, [Member], lease =>
        {
            ran = true;
            return files.WriteFileAtomicBytesAsync(lease, Member, After);
        });
        Assert.Equal(original, ran); Assert.Equal(original, result.Success);
        if (original) Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        else { Assert.True(result.IsBlocked); Assert.Equal(originalBytes, File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath))); }
        Assert.Equal(original ? After : Before, File.ReadAllBytes(files.ResolvePath(Member)));
        AssertNoBrowserEvidence(files);
    }

    [Theory]
    [InlineData((int)GmSessionRunDisposition.Running)]
    [InlineData((int)GmSessionRunDisposition.Stopping)]
    public async Task ColdMainNonterminal_RefusesBeforeBrowserRecoveryAndArtifacts(int state)
    {
        var (files, coordinator) = await CreateAsync();
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 1,
            GmSessionRunBackend.LinuxSupervisor, Guid.NewGuid().ToString("N"), "fixture-boot");
        var record = Path.Combine(files.RuntimeRootPath, "gm-runs", "main.json");
        Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        var bytes = GmSessionRunRecordCodec.Encode(new(1, identity, (GmSessionRunDisposition)state, null)); File.WriteAllBytes(record, bytes);
        var ran = false;
        var error = await Record.ExceptionAsync(() => coordinator.ExecuteAtomicAsync(Request, [Member], _ => { ran = true; return Task.CompletedTask; }));
        Assert.NotNull(error); Assert.False(ran); Assert.Equal(bytes, File.ReadAllBytes(record));
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        AssertNoBrowserEvidence(files);
    }

    [Fact]
    public async Task RetainedColdWorkerPrepared_RefusesBeforeBrowserEffects()
    {
        var (files, coordinator) = await CreateAsync();
        string generation;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.ReadExistingSessionGeneration(lease)!;
        var target = new WorkerLedgerTarget(files.BasePath);
        await using (var ledger = await GmWorkerRunLedger.OpenCoordinatorAsync(target))
        {
            Assert.NotNull(ledger);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await ledger.InitializeAsync());
            var prepared = await ledger.PrepareAsync(new(generation, "fixture-worker", "fixture-task", new string('0', 64),
                WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, Path.Combine(_root, "fixture-workspace")), ledger.Sequence);
            Assert.Equal(WorkerLedgerMutationKind.Applied, prepared.Kind);
        }
        var ledgerPath = Path.Combine(target.DirectoryPath, "state.json");
        var bytes = File.ReadAllBytes(ledgerPath);
        Assert.Equal(WorkerRunObservationKind.Uncertain, (await GmWorkerRunLedger.ObserveAsync(target)).Kind);
        var ran = false;
        var error = await Record.ExceptionAsync(() => coordinator.ExecuteAtomicAsync(Request, [Member], _ => { ran = true; return Task.CompletedTask; }));
        Assert.NotNull(error); Assert.False(ran); Assert.Equal(bytes, File.ReadAllBytes(ledgerPath));
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
        AssertNoBrowserEvidence(files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrustedLocalMember_RejectsSymlinkAndPreservesOrdinaryHardlinkAlias(bool hardlink)
    {
        var (files, coordinator) = await CreateAsync();
        var outside = Path.Combine(_root, "outside.bin"); File.WriteAllBytes(outside, Before);
        var member = files.ResolvePath(Member); File.Delete(member);
        if (hardlink) Assert.Equal(0, Link(outside, member)); else File.CreateSymbolicLink(member, outside);
        var ran = false;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], lease =>
        {
            ran = true; return files.WriteFileAtomicBytesAsync(lease, Member, After);
        });
        Assert.Equal(hardlink, result.Success); Assert.Equal(hardlink, ran);
        Assert.Equal(Before, File.ReadAllBytes(outside));
        if (hardlink) Assert.Equal(After, File.ReadAllBytes(member));
        else Assert.NotNull(new FileInfo(member).LinkTarget);
        AssertNoBrowserEvidence(files);
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string alias);
}
