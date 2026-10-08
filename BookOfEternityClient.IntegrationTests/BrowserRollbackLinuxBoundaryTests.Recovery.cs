using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Theory]
    [InlineData("missing-baseline")]
    [InlineData("stale-generation")]
    [InlineData("malformed")]
    [InlineData("old-schema")]
    [InlineData("unknown-evidence")]
    public async Task InvalidOrUnsupportedEvidence_RefusesBeforeRecoveryAndRetainsExactBytes(string mode)
    {
        var (files, _) = await CreateAsync();
        string manifest;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease, [Member], "browser_write");
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            manifest = files.ResolvePath(transaction.ManifestPath);
            if (mode == "missing-baseline") File.Delete(files.ResolvePath(Assert.Single(transaction.Entries).BackupPath!));
            else if (mode == "stale-generation")
            {
                var json = JsonNode.Parse(File.ReadAllBytes(manifest))!;
                json["generation"] = Guid.NewGuid().ToString("N"); File.WriteAllText(manifest, json.ToJsonString());
            }
            else if (mode == "old-schema")
            {
                var json = JsonNode.Parse(File.ReadAllBytes(manifest))!;
                json["schemaVersion"] = 6; File.WriteAllText(manifest, json.ToJsonString());
            }
            else if (mode == "malformed") File.WriteAllText(manifest, "{broken");
            else File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(manifest)!, "unknown.bin"), [77, 0]);
        }
        var evidence = File.ReadAllBytes(manifest);
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await fresh.AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(evidence, File.ReadAllBytes(manifest));
        Assert.Equal(After, File.ReadAllBytes(fresh.ResolvePath(Member)));
    }

    [Fact]
    public async Task OldEmptyOrphanCleanupIntent_IsRetainedWithoutMutation()
    {
        var (files, _) = await CreateAsync();
        var root = files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root + "/browser_write/" + DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "browser_write_cleanup_committed.intent"); File.WriteAllBytes(path, []);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); });
        Assert.True(File.Exists(path)); Assert.Empty(File.ReadAllBytes(path));
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
    }

    [Fact]
    public async Task PendingMemberDecision_SettlesBeforeBrowserRollback()
    {
        var (files, _) = await CreateAsync();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease, [Member], "browser_write");
            var selected = false;
            var cutHits = 0;
            _mutation = path => { selected = path == Member; return Task.CompletedTask; };
            _publication = (phase, _) =>
            {
                if (selected && phase == TrustedLocalPublicationPhase.MemberPublished)
                {
                    cutHits++;
                    File.WriteAllBytes(files.ResolvePath(Member), [110]);
                    throw new InvalidOperationException("owned member publication cut");
                }
            };
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => files.WriteFileAtomicBytesAsync(lease, Member, After));
            Assert.Equal(1, cutHits);
            Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            File.WriteAllBytes(files.ResolvePath(Member), After); // Restore the declared image for the controlled continuation.
        }
        var recovered = 0;
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationRecoveryObserver = (phase, _) => { if (phase == TrustedLocalPublicationPhase.MemberRestored) recovered++; } });
        await using var recoveredLease = await fresh.AcquireCanonicalWriteLeaseAsync();
        Assert.True(recovered > 0);
        Assert.Equal(Before, File.ReadAllBytes(fresh.ResolvePath(Member)));
        AssertNoBrowserEvidence(fresh);
    }
    [Fact]
    public async Task PendingManifestScratch_OriginalPublisherSettlesBeforeBrowserRecovery()
    {
        var (files, _) = await CreateAsync();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease, [Member], "browser_write");
            var manifest = files.ResolvePath(transaction.ManifestPath);
            var before = File.ReadAllBytes(manifest);
            var selected = false;
            var cutHits = 0;
            _mutation = path => { selected = path == transaction.ManifestPath; return Task.CompletedTask; };
            _publication = (phase, _) =>
            {
                if (selected && phase == TrustedLocalPublicationPhase.MemberStaged)
                {
                    cutHits++;
                    File.WriteAllBytes(manifest, [42]);
                    throw new InvalidOperationException("owned metadata stage cut");
                }
            };
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => files.WriteFileAtomicBytesAsync(lease, Member, After));
            Assert.Equal(1, cutHits);
            Assert.True(Directory.EnumerateFiles(files.ResolvePath(transaction.TransactionRoot), ".boe-local-*.stage").Any());
            File.WriteAllBytes(manifest, before); // Restore declared current image while retaining exact owned scratch.
        }
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await using var recovered = await fresh.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(Before, File.ReadAllBytes(fresh.ResolvePath(Member)));
        AssertNoBrowserEvidence(fresh);
    }

    [Fact]
    public async Task ConfirmedRollbackWithCleanupDebt_RemainsRolledBackAndFreshManagerCleans()
    {
        var (files, coordinator) = await CreateAsync();
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            _mutation = path => path.EndsWith(".rollback", StringComparison.Ordinal)
                ? Task.FromException(new IOException("rollback cleanup cut")) : Task.CompletedTask;
            throw new InvalidOperationException("callback cut");
        });
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.Equal(BookOfEternityClient.WebUi.BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
        Assert.True(result.NeedsFollowUp); Assert.False(result.Success);
        _mutation = null;
        var fresh = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        await using var recovered = await fresh.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(Before, File.ReadAllBytes(fresh.ResolvePath(Member)));
        AssertNoBrowserEvidence(fresh);
    }

}
