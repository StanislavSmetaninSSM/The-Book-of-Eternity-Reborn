using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-linux-boundary-" + Guid.NewGuid().ToString("N"));
    private const string Member = "game_state/meta/browser-member.bin";
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
    private static readonly byte[] After = Encoding.UTF8.GetBytes("Привет 世界\n");
    private Func<string, Task>? _mutation;
    private Func<Task>? _closing;
    private Func<Task>? _contended;
    private Action<TrustedLocalPublicationPhase, int>? _publication;
    private BrowserLocalWriteRequest Request => new("linux-browser", "Fixture", "boundary");

    private async Task<(FileSystemManager Files, BrowserLocalWriteCoordinator Coordinator)> CreateAsync()
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux body required.");
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path => _mutation?.Invoke(path) ?? Task.CompletedTask,
                SessionOperationClosingAsync = () => _closing?.Invoke() ?? Task.CompletedTask,
                MainOwnerLockContendedAsync = () => _contended?.Invoke() ?? Task.CompletedTask,
                LocalPublicationObserver = (phase, index) => _publication?.Invoke(phase, index)
            });
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        await files.WriteFileAtomicBytesAsync(Member, Before);
        return (files, new(files, new LocalUiSessionLockService(files)));
    }

    [Fact]
    public async Task SharedWriteAppendCompareExchangeDelete_RecordAndRestoreFirstBaseline()
    {
        var (files, coordinator) = await CreateAsync();
        var ran = false;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            ran = true;
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            await files.AppendFileAtomicAsync(lease, Member, "尾");
            var appended = await files.ReadFileBytesAsync(lease, Member);
            Assert.Equal(CanonicalFileMutationResult.Applied, await files.CompareExchangeFileBytesAsync(lease, Member, appended, [9, 8]));
            files.DeleteFile(lease, Member);
            Assert.False(File.Exists(files.ResolvePath(Member)));
            throw new InvalidOperationException("rollback all ordinary APIs");
        });
        output.WriteLine(result.Message);
        Assert.True(ran); Assert.False(result.Success);
        Assert.Contains("rollback восстановлен", result.Message);
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
    }

    [Fact]
    public async Task SuccessfulFirstWriteThenFailedLaterWrite_PreservesCumulativeAuthority()
    {
        var (files, coordinator) = await CreateAsync();
        var failed = false;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            _mutation = path => path == Member ? Task.FromException(new InvalidOperationException("later write refused")) : Task.CompletedTask;
            await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicBytesAsync(lease, Member, [99]));
            failed = true; _mutation = null;
            throw new InvalidOperationException("rollback earlier successful write");
        });
        output.WriteLine(result.Message);
        Assert.True(failed); Assert.False(result.Success);
        Assert.Contains("rollback восстановлен", result.Message);
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
    }

    [Theory]
    [InlineData(false, "commit")]
    [InlineData(true, "commit")]
    [InlineData(false, "rollback")]
    [InlineData(true, "rollback")]
    [InlineData(false, "uncertain")]
    [InlineData(true, "uncertain")]
    public async Task ActualClosingFault_RetainsEstablishedDecisionThroughBothEntryRoutes(bool within, string decision)
    {
        var (files, coordinator) = await CreateAsync();
        _closing = () => Task.FromException(new IOException("actual closing cut"));
        async Task Write(FileSystemManager.CanonicalWriteLease lease)
        {
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            if (decision == "uncertain") File.WriteAllBytes(files.ResolvePath(Member), [111, 0]);
            if (decision != "commit") throw new InvalidOperationException("callback cut");
        }
        var result = within
            ? await coordinator.RunBoundTransactionAsync(lease => coordinator.ExecuteAtomicWithinTransactionAsync(lease, Request, [Member], Write))
            : await coordinator.ExecuteAtomicAsync(Request, [Member], Write);
        output.WriteLine(result.Message);
        Assert.Equal(decision == "commit", result.Success);
        Assert.Equal(decision == "commit" ? BrowserPreparedWriteDisposition.Committed : decision == "rollback"
            ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        Assert.True(result.NeedsFollowUp); Assert.True(result.ContinuationBlocked);
        Assert.Equal(decision == "rollback" ? Before : decision == "commit" ? After : new byte[] { 111, 0 }, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.Contains("подтверж", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InterruptedRealTransaction_FreshManagerRestoresBeforeReturningLease()
    {
        var (files, _) = await CreateAsync();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease, [Member], "browser_write");
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            Assert.True(File.Exists(files.ResolvePath(transaction.ManifestPath)));
        }
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await using var recovered = await fresh.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(Before, File.ReadAllBytes(fresh.ResolvePath(Member)));
        AssertNoBrowserEvidence(fresh);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedCleanupDebt_FreshManagerCleansManifestOrTypedOrphan(bool afterManifestRemoval)
    {
        var (files, coordinator) = await CreateAsync();
        var cuts = 0;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], lease =>
        {
            _mutation = path =>
            {
                var selected = afterManifestRemoval ? path.EndsWith("browser_write_cleanup_committed.intent", StringComparison.Ordinal)
                    : path.EndsWith(".rollback", StringComparison.Ordinal);
                // Intent creation is the first call; deletion follows manifest removal.
                if (selected && (!afterManifestRemoval || ++cuts == 2)) throw new IOException("cleanup cut");
                return Task.CompletedTask;
            };
            return files.WriteFileAtomicBytesAsync(lease, Member, After);
        });
        output.WriteLine(result.Message);
        Assert.True(result.Success, result.Message);
        Assert.Equal(After, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.True(Directory.EnumerateFiles(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "*.intent", SearchOption.AllDirectories).Any());
        _mutation = null;
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await fresh.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(After, File.ReadAllBytes(fresh.ResolvePath(Member)));
        AssertNoBrowserEvidence(fresh);
    }

    [Fact]
    public async Task DirectoryMemberDeletion_RestoresDeclaredBytesOnCallbackFailure()
    {
        var (files, coordinator) = await CreateAsync();
        var removed = false;
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], lease =>
        {
            files.DeleteOriginalDirectoryTree(lease, "game_state/meta");
            removed = !File.Exists(files.ResolvePath(Member));
            throw new InvalidOperationException("directory callback cut");
        });
        output.WriteLine(result.Message);
        Assert.True(removed, result.Message); Assert.False(result.Success);
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
    }

    private static void AssertNoBrowserEvidence(FileSystemManager files)
    {
        var root = files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any());
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        output.WriteLine("FixtureCleanup rootRemoved=" + !Directory.Exists(_root));
    }
}
