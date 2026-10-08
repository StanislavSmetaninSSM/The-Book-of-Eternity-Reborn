using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserDirectGachaLinuxBoundaryTests
{
    [Fact]
    public async Task ActualStageCommittedPublisherDebt_SettlesWithoutUndoBeforeQueue()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var armed = false; var committed = false; var debt = false; var nextBoundary = false;
        var active = Path.Combine(fixture.Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        fixture.Mutation = path =>
        {
            if (debt && !nextBoundary)
            {
                nextBoundary = true; Assert.True(File.Exists(active));
                Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
            }
            if (ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup(path)) armed = true;
            return Task.CompletedTask;
        };
        fixture.Publication = (phase, _) =>
        {
            if (!armed) return;
            if (phase == TrustedLocalPublicationPhase.Committed) committed = true;
            if (phase == TrustedLocalPublicationPhase.CleanupMember && committed)
            {
                Assert.True(File.Exists(active)); Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
                debt = true; fixture.Publication = null;
                throw new InvalidOperationException("actual Stage B1 durable committed cleanup cut");
            }
        };
        var result = await fixture.PullAsync();
        Assert.True(committed); Assert.True(debt); Assert.True(nextBoundary); Assert.True(result.Success, result.Message);
        Assert.Equal(11, fixture.Feathers()); Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
        Assert.False(File.Exists(active)); Assert.True(File.Exists(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
    }

    [Theory]
    [InlineData((int)GmSessionRunDisposition.Running)]
    [InlineData((int)GmSessionRunDisposition.Stopping)]
    public async Task ActualBrowserColdMain_RefusesBeforeSideEffects(int state)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var identity = new GmSessionRunIdentity(fixture.Files.BasePath, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 1,
            GmSessionRunBackend.LinuxSupervisor, Guid.NewGuid().ToString("N"), "fixture-boot");
        var path = Path.Combine(fixture.Files.RuntimeRootPath, "gm-runs", "main.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = GmSessionRunRecordCodec.Encode(new(1, identity, (GmSessionRunDisposition)state, null)); File.WriteAllBytes(path, bytes);
        var writes = 0; fixture.Mutation = _ => { writes++; return Task.CompletedTask; };
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.PullAsync()));
        Assert.Equal(0, writes); Assert.Equal(bytes, File.ReadAllBytes(path));
        AssertBefore(fixture); AssertNoTurnOrRollback(fixture.Files);
    }

    [Fact]
    public async Task ActualBrowserOriginalPreparedWorker_RefusesBeforeSideEffects()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        var target = new WorkerLedgerTarget(fixture.Files.BasePath);
        await using (var ledger = await GmWorkerRunLedger.OpenCoordinatorAsync(target))
        {
            Assert.NotNull(ledger); Assert.Equal(WorkerLedgerMutationKind.Applied, await ledger.InitializeAsync());
            var prepared = await ledger.PrepareAsync(new(generation, "fixture-worker", "fixture-task", new string('0', 64),
                WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, Path.Combine(fixture.Root, "fixture-workspace")), ledger.Sequence);
            Assert.Equal(WorkerLedgerMutationKind.Applied, prepared.Kind);
        }
        var path = Path.Combine(target.DirectoryPath, "state.json"); var bytes = File.ReadAllBytes(path);
        Assert.Equal(WorkerRunObservationKind.Uncertain, (await GmWorkerRunLedger.ObserveAsync(target)).Kind);
        var writes = 0; fixture.Mutation = _ => { writes++; return Task.CompletedTask; };
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.PullAsync()));
        Assert.Equal(0, writes); Assert.Equal(bytes, File.ReadAllBytes(path)); AssertBefore(fixture); AssertNoTurnOrRollback(fixture.Files);
    }

    [Theory]
    [InlineData("original")]
    [InlineData("other-owner")]
    [InlineData("stale-token")]
    public async Task ActualBrowserUiGuard_OriginalLeaseOrRefusal(string mode)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var owner = new LocalUiSessionLockOwner("original", "browser", "Fixture", TimeSpan.FromMinutes(2));
        var held = await new LocalUiSessionLockService(fixture.Files).AcquireOrRefreshAsync(owner, "original browser guard");
        Assert.True(held.Acquired); Assert.NotNull(held.Lease);
        var before = File.ReadAllBytes(fixture.Files.ResolvePath(LocalUiSessionLockService.LockPath));
        var caller = mode == "other-owner" ? owner with { OwnerId = "other" } : owner with
        { Lease = mode == "original" ? held.Lease : held.Lease with { LeaseToken = Guid.NewGuid().ToString("N") } };
        var result = await fixture.PullAsync(caller);
        Assert.Equal(mode == "original", result.Success);
        if (mode == "original") { Assert.Equal(11, fixture.Feathers()); Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath())); }
        else
        {
            Assert.Equal(CommandExecutionState.Blocked, result.State); AssertBefore(fixture);
            Assert.Equal(before, File.ReadAllBytes(fixture.Files.ResolvePath(LocalUiSessionLockService.LockPath)));
            Assert.False(Directory.Exists(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)) &&
                Directory.EnumerateFileSystemEntries(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)).Any());
        }
    }

    [Fact]
    public async Task ActualBrowserRevokedGeneration_AfterQueuePublicationRetainsUncertainEvidence()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var revoked = false; byte[]? newGeneration = null;
        fixture.Mutation = path =>
        {
            if (path == BrowserPendingTurnInspector.TurnRequestPath) fixture.Publication = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                revoked = true; fixture.Publication = null;
                newGeneration = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") });
                File.WriteAllBytes(fixture.Files.SessionGenerationPath, newGeneration); // Controlled original authority-loss cut.
            };
            return Task.CompletedTask;
        };
        BrowserPromptWriteResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture.PullAsync());
        Assert.True(revoked);
        if (failure == null)
        {
            Assert.NotNull(result); Assert.False(result.Success); Assert.Equal(CommandExecutionState.Failed, result.State);
        }
        else
        {
            var carrier = Assert.IsType<MainOperationContinuationException<BrowserPromptWriteResult>>(failure);
            Assert.Equal(MainOperationOutcome.Uncertain, carrier.EstablishedOutcome); Assert.False(carrier.EstablishedResult.Success);
        }
        Assert.Equal(newGeneration, File.ReadAllBytes(fixture.Files.SessionGenerationPath)); Assert.Equal(11, fixture.Feathers());
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
        Assert.NotEmpty(Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
        var retained = Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "*", SearchOption.AllDirectories)
            .Concat(new[] { fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath),
                fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath), fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                Path.Combine(fixture.Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json") })
            .Where(File.Exists).Distinct(StringComparer.Ordinal).ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        Assert.NotNull(await Record.ExceptionAsync(() => Fresh(fixture).AcquireCanonicalWriteLeaseAsync()));
        Assert.Equal(newGeneration, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
        foreach (var evidence in retained) Assert.Equal(evidence.Value, File.ReadAllBytes(evidence.Key));
    }
}
