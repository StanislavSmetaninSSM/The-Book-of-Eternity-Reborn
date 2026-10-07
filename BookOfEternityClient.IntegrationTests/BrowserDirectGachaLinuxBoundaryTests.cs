using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserDirectGachaLinuxBoundaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("stage-staged")]
    [InlineData("stage-published")]
    [InlineData("queue")]
    public async Task RealPublicationCut_RestoresExactFilesRuntimeAndAbsence(string cut)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var reached = false;
        fixture.Mutation = path =>
        {
            if (cut == "queue" && path == BrowserPendingTurnInspector.TurnRequestPath)
            {
                reached = true; fixture.Mutation = null;
                throw new InvalidOperationException("actual queue cut");
            }
            if (cut != "queue" && ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup(path))
                fixture.Publication = (phase, _) =>
                {
                    if (phase != (cut == "stage-staged" ? TrustedLocalPublicationPhase.MemberStaged : TrustedLocalPublicationPhase.MemberPublished)) return;
                    reached = true; fixture.Publication = null;
                    throw new InvalidOperationException("actual Stage publication cut");
                };
            return Task.CompletedTask;
        };
        var result = await fixture.PullAsync();
        Assert.True(reached); Assert.False(result.Success); Assert.Equal(CommandExecutionState.Failed, result.State);
        AssertBefore(fixture); Assert.Equal(18, fixture.State.CurrentState.InkFeathers);
        AssertNoTurnOrRollback(fixture.Files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedActualStage_AuthenticatedScratchAndOriginalTransactionRecover(bool published)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        string? target = null, blocker = null, stage = null;
        var reached = false;
        fixture.Mutation = path =>
        {
            if (!ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup(path)) return Task.CompletedTask;
            target = fixture.Files.ResolvePath(path);
            fixture.Publication = (phase, _) =>
            {
                if (phase == TrustedLocalPublicationPhase.MemberStaged)
                    stage = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(target)!, ".boe-local-*.stage"));
                if (phase != (published ? TrustedLocalPublicationPhase.MemberPublished : TrustedLocalPublicationPhase.MemberStaged)) return;
                reached = true;
                // Observe the real staged path before its move; never fabricate
                // or parse another intent to replace the publisher's authority.
                Assert.NotNull(stage);
                blocker = stage[..^"stage".Length] + "undo";
                Directory.CreateDirectory(blocker); // Controlled namespace fault prevents eager B1 settlement.
                File.WriteAllBytes(Path.Combine(blocker, "fixture-cut"), [1]); // Retain through original cosmetic empty-directory cleanup.
                fixture.Publication = null; fixture.Mutation = null;
                Assert.Equal(published, File.Exists(target));
                if (!published) Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(stage));
                throw new InvalidOperationException("retain actual Stage intent");
            };
            return Task.CompletedTask;
        };
        fixture.Closing = () => Task.FromException(new IOException("retain original Uncertain carrier"));
        var failure = await Assert.ThrowsAsync<MainOperationContinuationException<BrowserPromptWriteResult>>(() => fixture.PullAsync());
        Assert.True(reached); Assert.Equal(MainOperationOutcome.Uncertain, failure.EstablishedOutcome);
        Assert.False(failure.EstablishedResult.Success); AssertBefore(fixture);
        var journalPath = Path.Combine(fixture.Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        Assert.True(File.Exists(journalPath)); Assert.Equal(published, File.Exists(target));
        if (!published) Assert.Single(Directory.GetFiles(Path.GetDirectoryName(target)!, ".boe-local-*.stage"));
        Assert.True(Directory.Exists(blocker));
        var retainedUiLease = File.ReadAllBytes(fixture.Files.ResolvePath(LocalUiSessionLockService.LockPath));
        fixture.Closing = null; File.Delete(Path.Combine(blocker!, "fixture-cut")); Directory.Delete(blocker!); // Remove only the fixture's known fault.
        var fresh = Fresh(fixture);
        await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync()) Assert.False(File.Exists(journalPath));
        AssertBefore(fixture); AssertNoTurnOrRollback(fresh, requireUiReleased: false);
        // Recovery owns storage settlement, not the original UI lease whose release failed.
        Assert.Equal(retainedUiLease, File.ReadAllBytes(fresh.ResolvePath(LocalUiSessionLockService.LockPath)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedSchemaCleanupDebt_OriginalRecoveryRetainsPendingBeforeImage(bool afterManifest)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var cuts = 0;
        var cleanup = false;
        fixture.Mutation = path =>
        {
            if (path.EndsWith("browser_write_committed.marker", StringComparison.Ordinal)) cleanup = true;
            var selected = cleanup && (afterManifest ? path.EndsWith("browser_write_cleanup_committed.intent", StringComparison.Ordinal)
                : path.EndsWith(".rollback", StringComparison.Ordinal));
            if (selected && (!afterManifest || ++cuts == 2)) throw new InvalidOperationException("schema7 committed cleanup debt");
            return Task.CompletedTask;
        };
        fixture.Closing = () => Task.FromException(new IOException("observe original committed carrier"));
        var failure = await Assert.ThrowsAsync<MainOperationContinuationException<BrowserPromptWriteResult>>(() => fixture.PullAsync());
        Assert.Equal(MainOperationOutcome.Committed, failure.EstablishedOutcome); Assert.True(failure.EstablishedResult.Success);
        Assert.Equal(11, fixture.Feathers());
        var path = fixture.BackupPath(); var request = File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath));
        var pendingManifest = File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        var authority = File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(path));
        Assert.NotEmpty(Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "*.intent", SearchOption.AllDirectories));
        fixture.Mutation = null; fixture.Closing = null;
        var fresh = Fresh(fixture);
        await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync()) Assert.Equal(11, fixture.Feathers());
        Assert.Equal(path, fixture.BackupPath()); Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(path));
        Assert.Equal(request, File.ReadAllBytes(fresh.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath)));
        Assert.Equal(pendingManifest, File.ReadAllBytes(fresh.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)));
        Assert.Equal(authority, File.ReadAllBytes(fresh.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.Empty(Directory.GetFiles(fresh.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "*.intent", SearchOption.AllDirectories));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fresh.ResolvePath("game_state/history/chat_log.json")));
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    public async Task RealBrowserClosing_RetainsTypedOriginalDecisionWithoutRetry(string decision)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        byte[] unknown = [0, 255, 41];
        var cutReached = false;
        if (decision != "commit") fixture.Mutation = path =>
        {
            if (path != BrowserPendingTurnInspector.TurnRequestPath) return Task.CompletedTask;
            cutReached = true; fixture.Mutation = null;
            if (decision == "uncertain") File.WriteAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul), unknown);
            throw new InvalidOperationException("actual original queue decision cut");
        };
        fixture.Closing = () => Task.FromException(new IOException("original connection closing cut"));
        var failure = await Assert.ThrowsAsync<MainOperationContinuationException<BrowserPromptWriteResult>>(() => fixture.PullAsync());
        Assert.Equal(decision != "commit", cutReached);
        Assert.Equal(decision == "commit" ? MainOperationOutcome.Committed : decision == "rollback" ? MainOperationOutcome.RolledBack : MainOperationOutcome.Uncertain,
            failure.EstablishedOutcome);
        Assert.Equal(decision == "commit", failure.EstablishedResult.Success);
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
        if (decision == "rollback") { AssertBefore(fixture); AssertNoTurnOrRollback(fixture.Files); }
        else if (decision == "commit") { Assert.Equal(11, fixture.Feathers()); Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath())); }
        else
        {
            Assert.Equal(unknown, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
            Assert.NotEmpty(Directory.GetFiles(fixture.Files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
            fixture.Closing = null;
            Assert.NotNull(await Record.ExceptionAsync(() => Fresh(fixture).AcquireCanonicalWriteLeaseAsync()));
            Assert.Equal(unknown, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
        }
    }

    [Theory]
    [InlineData("scratch")]
    [InlineData("backup")]
    public async Task RealBrowserCreatedEvidence_UnknownResidueRefusesAndRetainsBytes(string kind)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync(); Assert.True((await fixture.PullAsync()).Success);
        var backup = fixture.BackupPath();
        var request = File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath));
        var manifest = File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        var authority = File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath));
        var path = Path.Combine(Path.GetDirectoryName(backup)!, kind == "scratch" ? ".boe-local-" + Guid.NewGuid().ToString("N") + "-0.stage" : "unknown.rollback.backup");
        byte[] bytes = [255, 0, 12]; File.WriteAllBytes(path, bytes);
        var error = await Record.ExceptionAsync(() => fixture.PullAsync()); // Actual browser entrypoint, not an unavailable-backend oracle.
        Assert.IsType<InvalidDataException>(error); Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(backup)); Assert.Equal(11, fixture.Feathers());
        Assert.Equal(request, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath)));
        Assert.Equal(manifest, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)));
        Assert.Equal(authority, File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
    }

    private static FileSystemManager Fresh(BrowserDirectGachaLinuxFixture fixture) => new(fixture.Root, NullLogger<FileSystemManager>.Instance);
    private static void AssertBefore(BrowserDirectGachaLinuxFixture fixture)
    {
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
        Assert.Equal(fixture.BeforeProfile, File.ReadAllBytes(fixture.Files.ResolvePath(AfterlifeEntityProfileState.StatePath)));
        Assert.Equal(fixture.BeforeDice, File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnStateService.PendingDiceStatePath)));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
    }
    private static void AssertNoTurnOrRollback(FileSystemManager files, bool requireUiReleased = true)
    {
        foreach (var path in new[] { BrowserPendingTurnInspector.TurnRequestPath, BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
            Assert.False(File.Exists(files.ResolvePath(path)));
        Assert.False(Directory.Exists(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)) && Directory.EnumerateFileSystemEntries(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)).Any());
        if (requireUiReleased) Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
    }
}
