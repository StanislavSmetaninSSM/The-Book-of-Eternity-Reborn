using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.UI;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsoleRollback_NewPreparationAfterCleanupDebtCapturesFreshBaseline(bool newTrackedFile)
    {
        const string first = "lore/current_world/console_restage_first.json";
        const string second = "lore/current_world/console_restage_second.json";
        const string other = "lore/current_world/console_restage_other.json";
        await _fs.WriteFileAtomicBytesAsync(first, [0x41]);
        await _fs.WriteFileAtomicBytesAsync(second, [0x42]);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(first, second);
        var previous = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        ArmCanonicalWriteFailure(previous.BackupFiles[second]);
        await Assert.ThrowsAsync<IOException>(() => explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(previous));
        Assert.Null(_armedCanonicalWriteFailurePath);
        Assert.False(File.Exists(_fs.ResolvePath(previous.BackupFiles[first])));
        Assert.True(File.Exists(_fs.ResolvePath(Assert.Single(previous.TechnicalArtifacts))));

        var target = newTrackedFile ? other : first;
        await _fs.WriteFileAtomicBytesAsync(target, [0x61]);
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(target);
        await _fs.WriteFileAtomicBytesAsync(target, [0x71]);
        await explorer.RestoreStagedLocalTurnRollbackSnapshotAsync();

        Assert.Equal(new byte[] { 0x61 }, File.ReadAllBytes(_fs.ResolvePath(target)));
        Assert.False(Directory.Exists(_fs.ResolvePath(ConsoleLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task ConsoleRollback_PartialCleanupRetainsMarkerAndRetryDoesNotRestoreTwice()
    {
        const string first = "lore/current_world/console_cleanup_first.json";
        const string second = "lore/current_world/console_cleanup_second.json";
        await _fs.WriteFileAtomicBytesAsync(first, [0x41]);
        await _fs.WriteFileAtomicBytesAsync(second, [0x42]);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(first, second);
        var staged = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        await _fs.WriteFileAtomicBytesAsync(first, [0x51]);
        await _fs.WriteFileAtomicBytesAsync(second, [0x52]);
        ArmCanonicalWriteFailure(staged.BackupFiles[second]);

        await Assert.ThrowsAsync<IOException>(() => explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(staged));

        Assert.Null(_armedCanonicalWriteFailurePath); // Actual pre-delete boundary reached.
        Assert.False(File.Exists(_fs.ResolvePath(staged.BackupFiles[first])));
        Assert.True(File.Exists(_fs.ResolvePath(staged.BackupFiles[second])));
        Assert.True(File.Exists(_fs.ResolvePath(Assert.Single(staged.TechnicalArtifacts))));
        Assert.Equal(new byte[] { 0x41 }, File.ReadAllBytes(_fs.ResolvePath(first)));
        Assert.Equal(new byte[] { 0x42 }, File.ReadAllBytes(_fs.ResolvePath(second)));
        await _fs.WriteFileAtomicBytesAsync(first, [0x61]);
        await explorer.RestoreStagedLocalTurnRollbackSnapshotAsync();
        Assert.Equal(new byte[] { 0x61 }, File.ReadAllBytes(_fs.ResolvePath(first)));
        Assert.False(Directory.Exists(_fs.ResolvePath(ConsoleLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task ConsoleRollback_ColdSignedMarkerMustMatchLiveBytes()
    {
        const string original = "lore/current_world/console_marker.json";
        await _fs.WriteFileAtomicBytesAsync(original, [0x7B, 0x7D]);
        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        var staged = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "console_marker");
        InvokePrivate(engine, "OverlayExplorerLocalRollbackSnapshot", rollback, staged);
        var request = CreateSnapshotByteContractRequest("console_marker");
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "test");
        await _fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var marker = _fs.ResolvePath(Assert.Single(staged.TechnicalArtifacts));
        var changed = File.ReadAllBytes(marker).Concat(new byte[] { 0x0A }).ToArray();
        File.WriteAllBytes(marker, changed); // Same JSON identity; different exact live bytes.

        var cold = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await cold.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(changed, File.ReadAllBytes(marker));
        Assert.True(File.Exists(_fs.ResolvePath("input/turn_request.json")));
    }

    [Fact]
    public async Task ConsoleRollback_ColdSignedCancellationTransfersOwnershipBeforeDeletingRequest()
    {
        const string original = "lore/current_world/console_cancel.json";
        const string created = "lore/current_world/console_cancel_new.json";
        byte[] before = [0x7B, 0x7D];
        await _fs.WriteFileAtomicBytesAsync(original, before);
        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original, created);
        await _fs.WriteFileAtomicBytesAsync(original, [0x41]);
        await _fs.WriteFileAtomicBytesAsync(created, [0x42]);
        var staged = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "console_cold_cancel");
        InvokePrivate(engine, "OverlayExplorerLocalRollbackSnapshot", rollback, staged);
        var request = CreateSnapshotByteContractRequest("console_cold_cancel");
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "test");
        await _fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var cold = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        var restarted = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]), fileSystem: cold);
        Assert.False(await InvokePrivateAsync<bool>(restarted, "WaitForGmResponse"));

        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));
        Assert.False(File.Exists(_fs.ResolvePath(created)));
        Assert.False(File.Exists(_fs.ResolvePath("input/turn_request.json")));
        Assert.False(File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")));
        foreach (var backup in staged.BackupFiles.Values) Assert.False(File.Exists(_fs.ResolvePath(backup)));
        await using var cleanCold = await new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance)
            .AcquireCanonicalWriteLeaseAsync();
    }

    [Fact]
    public async Task ConsoleRollback_GenerationReplacementCannotReuseWarmOwnership()
    {
        const string original = "lore/current_world/console_generation.json";
        byte[] before = [0x41];
        await _fs.WriteFileAtomicBytesAsync(original, before);
        var explorer = GetPrivateField<ExplorerMode>(CreateGameEngine(), "_explorer");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(original);
        var snapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot()!;
        var backup = Assert.Single(snapshot.BackupFiles).Value;
        File.WriteAllBytes(_fs.SessionGenerationPath,
            JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await _fs.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(original)));
        Assert.Equal(before, File.ReadAllBytes(_fs.ResolvePath(backup)));
    }

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
            throw new InvalidOperationException("Reached console before-image publication cut.");
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => explorer.StagePendingLocalTurnRollbackSnapshotAsync(original));
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
