using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task ConsoleRollback_AbsentOnlyPreparationRemainsVisibleToColdAdmission()
    {
        const string original = "lore/current_world/console_originally_absent.json";
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        Assert.False(_fs.FileExists(original));
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        await _fs.WriteFileAtomicBytesAsync(original, [0x41]);

        var cold = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await cold.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(new byte[] { 0x41 }, File.ReadAllBytes(_fs.ResolvePath(original)));
        await explorer.RestoreStagedLocalTurnRollbackSnapshotAsync();
        Assert.False(File.Exists(_fs.ResolvePath(original)));
    }

    [Fact]
    public async Task ConsoleRollback_UnadoptedColdInstanceRefusesAndRetainsExactEvidence()
    {
        const string original = "lore/current_world/console_cold.json";
        byte[] before = [0xEF, 0xBB, 0xBF, 0xFF, 0];
        await _fs.WriteFileAtomicBytesAsync(original, before);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        var snapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        var backup = Assert.Single(snapshot.BackupFiles).Value;
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(backup)));

        var cold = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await cold.AcquireCanonicalWriteLeaseAsync();
        });

        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(backup)));
        await explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(snapshot);
        Assert.False(File.Exists(_fs.ResolvePath(backup)));
    }

    [Fact]
    public async Task ConsoleRollback_ReachedPreparationFailureCanRetryExactBeforeImage()
    {
        const string original = "lore/current_world/console_retry.json";
        byte[] before = [0xFE, 0xFF, 0x41, 0];
        await _fs.WriteFileAtomicBytesAsync(original, before);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        var cutHits = 0;
        _consolePublicationObserver = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            cutHits++;
            throw new IOException("Reached console before-image publication cut.");
        };
        await Assert.ThrowsAsync<IOException>(() => explorer.StagePendingLocalTurnRollbackSnapshotAsync(original));
        _consolePublicationObserver = null;
        Assert.Equal(1, cutHits);
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));

        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        var snapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        Assert.Contains(original, snapshot.BaselineFiles);
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(snapshot.BackupFiles[original])));
        await _fs.WriteFileAtomicBytesAsync(original, [0x42]);
        await explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(snapshot);
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));
        Assert.False(File.Exists(_fs.ResolvePath(snapshot.BackupFiles[original])));
    }

    [Fact]
    public async Task ConsoleRollback_ChangedBeforeImageRefusesWarmAdmissionWithoutMutation()
    {
        const string original = "lore/current_world/console_changed.json";
        byte[] before = [0x41];
        byte[] unknown = [0x99, 0x00];
        await _fs.WriteFileAtomicBytesAsync(original, before);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        var snapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        var backup = Assert.Single(snapshot.BackupFiles).Value;
        File.WriteAllBytes(_fs.ResolvePath(backup), unknown); // Explicit external conflict fixture.

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await _fs.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));
        Assert.Equal(unknown, File.ReadAllBytes(_fs.ResolvePath(backup)));
    }
}
