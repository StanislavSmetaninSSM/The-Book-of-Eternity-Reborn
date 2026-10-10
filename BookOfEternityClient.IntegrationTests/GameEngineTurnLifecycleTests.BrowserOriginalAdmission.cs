using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserOriginalAdmission_ManifestCapturesGuardedQuiescentCondition()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync();
        var manifest = JsonNode.Parse(staged.ManifestJson)!;
        var original = manifest["browserOriginalMainCondition"];
        Assert.NotNull(original);
        Assert.Equal("quiescentAbsent", original!["kind"]!.GetValue<string>());
        Assert.Equal(_fs.BasePath, original["rootKey"]!.GetValue<string>());
        Assert.Equal(staged.Binding.Generation, original["generation"]!.GetValue<string>());
        Assert.Null(original["activeIdentity"]);
        Assert.Null(original["stoppedRecord"]);
    }

    [Theory]
    [InlineData("missing-slot")]
    [InlineData("downgraded-slot")]
    [InlineData("request")]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("snapshot")]
    public async Task BrowserOriginalAdmission_ColdAcquisitionRefusesChangedOriginalBeforeRecovery(string damage)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        DamageOriginalBrowserAdmission(staged, damage);
        var before = OriginalAdmissionTree();
        var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease = await cold.AcquireCanonicalWriteLeaseAsync();
        });
        AssertOriginalAdmissionTree(before);
    }

    [Fact]
    public async Task BrowserOriginalAdmission_AbsentCannotBecomeStoppedBetweenIdleOperations()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync();
        var identity = new GmSessionRunIdentity(_fs.BasePath, Guid.NewGuid().ToString("N"), staged.Binding.Generation,
            1, GmSessionRunBackend.LinuxSupervisor, Guid.NewGuid().ToString("N"), "test-original-boot");
        var record = new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Stopped,
            new(identity, GmSessionRunStopKind.OwnedScopeEmpty, identity.BootId));
        var directory = Path.Combine(_fs.BasePath, ".boe_runtime", "gm-runs");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "main.json"), GmSessionRunRecordCodec.Encode(record));
        var before = OriginalAdmissionTree();
        var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease = await cold.AcquireCanonicalWriteLeaseAsync();
        });
        AssertOriginalAdmissionTree(before);
        Assert.Equal(record, GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(_fs.BasePath)!));
    }

    [Fact]
    public async Task BrowserOriginalAdmission_PhysicalLockRechecksOriginalTupleBeforeRecovery()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var invoked = false;
        var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks: new FileSystemManagerHooks
        {
            AfterCanonicalWriteLockOpenedAsync = () =>
            {
                Assert.False(invoked); invoked = true;
                DamageOriginalBrowserAdmission(staged, "request");
                return Task.CompletedTask;
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var lease = await cold.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.True(invoked);
        Assert.Equal(staged.ManifestJson, File.ReadAllText(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")));
        Assert.Equal(staged.AuthorityJson, File.ReadAllText(_fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.Equal(staged.Json, File.ReadAllText(_fs.ResolvePath(PendingPlayerActionService.PendingPath)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserOriginalAdmission_ActualPendingRecoveryRequiresOriginalTupleUnderLock(bool substitute)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var probe = _fs.ResolvePath("game_state/control/c5_recovery_probe.json");
        await using(var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var publisher = new TrustedLocalFilePublication(_fs, new TrustedLocalFileScope([_fs.BasePath]));
            Assert.Throws<C5RecoveryInterruption>(() => publisher.Publish(lease,
                TrustedLocalGeneration.Existing(staged.Binding.Generation), [new(probe, null, [1, 2, 3])],
                (phase, _) => { if(phase == TrustedLocalPublicationPhase.MemberPublished) throw new C5RecoveryInterruption(); }));
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(probe));
        Dictionary<string, byte[]>? captured = null;
        var recoveryBoundaries = 0;
        var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalWriteLockOpenedAsync = () =>
                {
                    if(substitute) DamageOriginalBrowserAdmission(staged, "request");
                    captured = OriginalAdmissionTree();
                    Assert.Contains(captured.Keys, path => path.StartsWith(".boe_runtime/file-publication-transactions/", StringComparison.Ordinal));
                    return Task.CompletedTask;
                },
                LocalPublicationRecoveryObserver = (_, _) => recoveryBoundaries++
            });
        if(substitute)
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await cold.AcquireCanonicalWriteLeaseAsync(); });
            Assert.Equal(0, recoveryBoundaries);
            Assert.NotNull(captured);
            AssertOriginalAdmissionTree(captured!);
        }
        else
        {
            await using var lease = await cold.AcquireCanonicalWriteLeaseAsync();
            Assert.True(recoveryBoundaries > 0);
            Assert.False(File.Exists(probe));
        }
    }

    private sealed class C5RecoveryInterruption : Exception { }

    private Dictionary<string, byte[]> OriginalAdmissionTree() => Directory.EnumerateFiles(_fs.BasePath, "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(_fs.BasePath, path).Replace('\\', '/').StartsWith(".boe_runtime/locks/", StringComparison.Ordinal))
        .ToDictionary(path => Path.GetRelativePath(_fs.BasePath, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private void AssertOriginalAdmissionTree(Dictionary<string, byte[]> before)
    {
        var after = OriginalAdmissionTree();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach(var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    private void DamageOriginalBrowserAdmission(PendingPlayerActionService.Staged staged, string damage)
    {
        var pendingPath = _fs.ResolvePath(PendingPlayerActionService.PendingPath);
        switch(damage)
        {
            case "missing-slot": File.Delete(pendingPath); break;
            case "downgraded-slot": File.WriteAllText(pendingPath, staged.Binding.Json); break;
            case "request": File.WriteAllText(_fs.ResolvePath("input/turn_request.json"), staged.RequestJson + " "); break;
            case "manifest": File.WriteAllText(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json"), staged.ManifestJson + " "); break;
            case "authority": File.WriteAllText(_fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath), staged.AuthorityJson + " "); break;
            case "snapshot":
                var manifest = JsonNode.Parse(staged.ManifestJson)!;
                var path = manifest["files"]!.AsObject().First().Value!.GetValue<string>();
                File.AppendAllText(_fs.ResolvePath(path), " original changed"); break;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
    }
}
