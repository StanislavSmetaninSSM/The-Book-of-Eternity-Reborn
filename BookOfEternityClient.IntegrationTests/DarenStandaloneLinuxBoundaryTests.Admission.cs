using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class DarenStandaloneLinuxBoundaryTests
{
    [Theory]
    [InlineData("inactive")] [InlineData("foreign")] [InlineData("pending")] [InlineData("external")]
    public async Task ActualStoreUnsupportedOriginalAuthority_RefusesBeforePreparation(string mode)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        using var other = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync(); await other.InitializeAsync();
        await using var lease = await (mode == "foreign" ? other.Files : fixture.Files).AcquireCanonicalWriteLeaseAsync();
        if (mode == "inactive") await lease.DisposeAsync();
        if (mode == "pending") lease.PendingLocalDecision = new object();
        if (mode == "external") lease.ExternalPublicationContext = new object();
        var store = new DarenRewardProfileFileStore(fixture.Files);
        Assert.ThrowsAny<InvalidOperationException>(() => store.EnsureWriteSupported(lease));
        Assert.NotNull(await Record.ExceptionAsync(() => store.WriteExactBytesAtomicAsync(lease, After)));
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath))); Assert.Equal(0, fixture.ProfileMutationBoundaries);
        fixture.AssertPublisherClean(); other.AssertPublisherClean();
    }

    [Theory]
    [InlineData((int)GmSessionRunDisposition.Running, false)]
    [InlineData((int)GmSessionRunDisposition.Stopping, false)]
    [InlineData((int)GmSessionRunDisposition.Stopping, true)]
    public async Task ActualOriginalMainFence_RefusesColdOrHeldLeaseBeforeProfilePreparation(int state, bool held)
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        await using var lease = held ? await fixture.Files.AcquireCanonicalWriteLeaseAsync() : null;
        var identity = new GmSessionRunIdentity(fixture.Files.BasePath, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 1,
            GmSessionRunBackend.LinuxSupervisor, Guid.NewGuid().ToString("N"), "fixture-boot");
        var path = Path.Combine(fixture.Files.RuntimeRootPath, "gm-runs", "main.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var record = GmSessionRunRecordCodec.Encode(new(1, identity, (GmSessionRunDisposition)state, null)); File.WriteAllBytes(path, record);
        var failure = held ? await Record.ExceptionAsync(() => new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease!, After))
            : await Record.ExceptionAsync(() => fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90), DateTime.UtcNow));
        Assert.NotNull(failure); Assert.Equal(record, File.ReadAllBytes(path)); Assert.Equal(0, fixture.ProfileMutationBoundaries);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath))); fixture.AssertPublisherClean();
    }

    [Fact]
    public async Task ActualOriginalPreparedWorker_RefusesStandaloneCompletion()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        var target = new WorkerLedgerTarget(fixture.Files.BasePath);
        await using (var ledger = await GmWorkerRunLedger.OpenCoordinatorAsync(target))
        {
            Assert.NotNull(ledger); Assert.Equal(WorkerLedgerMutationKind.Applied, await ledger.InitializeAsync());
            Assert.Equal(WorkerLedgerMutationKind.Applied, (await ledger.PrepareAsync(new(generation, "fixture-worker", "fixture-task", new string('0', 64),
                WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, Path.Combine(fixture.Root, "fixture-workspace")), ledger.Sequence)).Kind);
        }
        var path = Path.Combine(target.DirectoryPath, "state.json"); var bytes = File.ReadAllBytes(path);
        Assert.Equal(WorkerRunObservationKind.Uncertain, (await GmWorkerRunLedger.ObserveAsync(target)).Kind);
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90), DateTime.UtcNow)));
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(0, fixture.ProfileMutationBoundaries);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath))); fixture.AssertPublisherClean();
    }

    [Fact]
    public async Task OriginalTaskPurpose_CannotWriteStandaloneProfile()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        var context = GmWorkerRootContext.Attach(fixture.Files, durable: true, observer: null);
        try
        {
            using var root = context.Enter();
            var task = new WorkerTaskPacket { SessionGeneration = generation, WorkerId = "fixture-worker", TaskId = "fixture-task" };
            using var dispatch = context.CreateDispatch(root, task, JsonSerializer.SerializeToUtf8Bytes(task));
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync(workerPurpose: dispatch.ColdPurpose);
            var store = new DarenRewardProfileFileStore(fixture.Files);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteExactBytesAtomicAsync(lease, After));
            Assert.Contains("purpose", failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, fixture.ProfileMutationBoundaries); Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath)));
            fixture.AssertPublisherClean();
        }
        finally { context.ReleaseClient(); }
    }

    [Fact]
    public async Task OriginalBoundGenerationRevokedBeforeWrite_RefusesBeforeIntent()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        var entered = false;
        var replacementId = Guid.NewGuid().ToString("N");
        var replacement = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = replacementId });
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(fixture.Files, generation, lease, async () =>
        {
            entered = true; File.WriteAllBytes(fixture.Files.SessionGenerationPath, replacement);
            await new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease, After);
            return true;
        }));
        Assert.True(entered);
        var replaced = Assert.IsType<SessionReplacedException>(failure);
        Assert.Equal(generation, replaced.ExpectedGeneration); Assert.Equal(replacementId, replaced.ActualGeneration);
        Assert.Equal(replacement, File.ReadAllBytes(fixture.Files.SessionGenerationPath)); Assert.Equal(0, fixture.ProfileMutationBoundaries);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath))); Assert.False(File.Exists(fixture.ActiveJournal));
        fixture.AssertPublisherClean();
    }

    [Fact]
    public async Task HeldRealProfileWrite_SerializesActualSessionReplacement()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Mutation = async path => { if (path == "@daren_reward_profile") { reached.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); } };
        var replacementFiles = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { MainOwnerLockContendedAsync = () => { contended.TrySetResult(); return Task.CompletedTask; } });
        var write = fixture.Profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90), DateTime.UtcNow);
        Task<string>? replacement = null;
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            replacement = ReplaceAsync();
            await contended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(write.IsCompleted); Assert.False(replacement.IsCompleted);
        }
        finally { release.TrySetResult(); await Task.WhenAll(write, replacement ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(15)); }
        Assert.True((await write.WaitAsync(TimeSpan.FromSeconds(10))).Updated);
        var newGeneration = await replacement!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotEqual(generation, newGeneration);
        Assert.Equal("perfect_shadow", (await new DarenQteRewardProfileService(replacementFiles).ReadProfileAsync()).DarenShowcase!.BestTierId);
        fixture.AssertPublisherClean();
        async Task<string> ReplaceAsync()
        {
            await using var lifecycle = await replacementFiles.AcquireSessionLifecycleLeaseAsync();
            await using var lease = await replacementFiles.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
            return replacementFiles.RotateSessionGeneration(lease);
        }
    }

    [Fact]
    public async Task ActualUndeclaredBrowserProfile_RefusesWithoutStandaloneEscape()
    {
        using var fixture = new DarenStandaloneLinuxFixture(output);
        await fixture.InitializeAsync();
        const string member = "game_state/meta/daren-declaration-test.bin";
        await fixture.Files.WriteFileAtomicBytesAsync(member, Before);
        var coordinator = new BrowserLocalWriteCoordinator(fixture.Files, new LocalUiSessionLockService(fixture.Files));
        var entered = false;
        var result = await coordinator.ExecuteAtomicAsync(new("fixture", "Fixture", "undeclared profile"), [member], async lease =>
        {
            entered = true;
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new DarenRewardProfileFileStore(fixture.Files).WriteExactBytesAtomicAsync(lease, After));
            Assert.Contains("not a declared member", failure.Message);
            throw new InvalidOperationException("original undeclared callback refused");
        });
        Assert.True(entered, result.Message); Assert.False(result.Success); Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
        Assert.Equal(Before, File.ReadAllBytes(fixture.Files.ResolvePath(member)));
        Assert.Equal(0, fixture.ProfileMutationBoundaries); Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.ProfilePath)));
        fixture.AssertPublisherClean();
    }
}
