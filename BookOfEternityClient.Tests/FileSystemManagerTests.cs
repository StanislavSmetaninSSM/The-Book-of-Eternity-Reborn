using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemManagerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public FileSystemManagerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ReadFileAsync_WhenFileIsBrieflyLocked_RetriesUntilContentIsReadable()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "stable content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var readTask = _fs.ReadFileAsync("input/turn_request.json");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        var content = await readTask;

        Assert.Equal("stable content", content);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_WhenTargetIsBrieflyLocked_RetriesUntilReplacementSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "old content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var writeTask = _fs.WriteFileAtomicAsync("input/turn_request.json", "new content");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await writeTask;

        Assert.Equal("new content", await _fs.ReadFileAsync("input/turn_request.json"));
    }

    [Fact]
    public async Task WriteFileAtomicAsync_PreservesUtf8BomContract()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");

        var bytes = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        var preamble = System.Text.Encoding.UTF8.GetPreamble();

        Assert.Equal(preamble, bytes.Take(preamble.Length).ToArray());
    }

    [Fact]
    public async Task ClearGameStateAsync_DoesNotTraverseDirectoryJunction()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-manager-outside-" + Guid.NewGuid().ToString("N"));
        var outsideFile = Path.Combine(outsideRoot, "external.json");
        var junctionPath = _fs.ResolvePath("game_state/world/external-link");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(outsideFile, "{\"mustRemain\":true}");
        try
        {
            CreateDirectoryJunction(junctionPath, outsideRoot);

            await _fs.ClearGameStateAsync();

            Assert.True(File.Exists(outsideFile));
            Assert.Equal("{\"mustRemain\":true}", await File.ReadAllTextAsync(outsideFile));
        }
        finally
        {
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath, recursive: false);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFileAtomicBytesAsync_PreservesExactBytes()
    {
        byte[] expected = [0xEF, 0xBB, 0xBF, 0x00, 0xFF, 0x41];

        await _fs.WriteFileAtomicBytesAsync("input/turn_request.json", expected);

        var actual = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RejectsStaleExpectedBytesWithoutWriting()
    {
        byte[] baseline = [0x01, 0x02, 0x03];
        byte[] concurrent = [0x04, 0x05, 0x06];
        byte[] proposed = [0x07, 0x08, 0x09];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", concurrent);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            baseline,
            proposed);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(concurrent, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_ConcurrentReplacementsAllowExactlyOneWinner()
    {
        byte[] baseline = [0x10];
        byte[] first = [0x20];
        byte[] second = [0x30];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);

        var results = await Task.WhenAll(
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, first),
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, second));

        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Applied));
        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Conflict));
        var actual = await _fs.ReadFileBytesAsync("game_state/world/weather.json");
        Assert.NotNull(actual);
        Assert.True(actual.SequenceEqual(first) || actual.SequenceEqual(second));
    }

    [Fact]
    public async Task TryAcquireSessionLifecycleLeaseAsync_StaleGenerationReturnsNull()
    {
        string staleGeneration;
        await using (var canonicalLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            staleGeneration = _fs.GetOrCreateSessionGeneration(canonicalLease);
        await using (var replacementLifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await _fs.AcquireSessionReplacementWriteLeaseAsync(replacementLifecycleLease))
            _fs.RotateSessionGeneration(replacementLease);

        var lifecycleLease = await _fs.TryAcquireSessionLifecycleLeaseAsync(staleGeneration);

        Assert.Null(lifecycleLease);
    }

    [Fact]
    public async Task SessionMutationLease_CannotRotateSessionGeneration()
    {
        await using var canonicalLease = await _fs.AcquireCanonicalWriteLeaseAsync();

        var exception = Assert.Throws<InvalidOperationException>(
            () => _fs.RotateSessionGeneration(canonicalLease));

        Assert.Contains("replacement", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionReplacementLease_RequiresActiveLifecycleAuthority()
    {
        var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        await lifecycleLease.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fs.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.SessionReplacement));
    }

    [Fact]
    public async Task SessionReplacementLease_CanRotateGenerationWhileLifecycleAuthorityIsActive()
    {
        string previousGeneration;
        await using (var mutationLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            previousGeneration = _fs.GetOrCreateSessionGeneration(mutationLease);

        await using var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease =
            await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);

        var replacementGeneration = _fs.RotateSessionGeneration(replacementLease);

        Assert.NotEqual(previousGeneration, replacementGeneration);
        Assert.True(_fs.IsCurrentSessionGeneration(replacementLease, replacementGeneration));
    }

    [Fact]
    public async Task ClearGameStateAsync_WaitsForActiveSessionLifecycleLease()
    {
        var lifecycleContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var competingFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                SessionLifecycleLockContendedAsync = () =>
                {
                    lifecycleContended.TrySetResult(true);
                    return Task.CompletedTask;
                }
            });
        competingFs.EnsureDirectoryStructure();

        var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        Task clearTask;
        try
        {
            clearTask = competingFs.ClearGameStateAsync();
            await lifecycleContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(clearTask.IsCompleted);
        }
        finally
        {
            await lifecycleLease.DisposeAsync();
        }

        await clearTask;
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RollbackDoesNotOverwriteNewerBytes()
    {
        byte[] baseline = [0x41];
        byte[] worker = [0x42];
        byte[] newer = [0x43];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", worker);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", newer);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            worker,
            baseline);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(newer, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task DeleteFile_WhenTargetIsBrieflyLocked_RetriesUntilDeleteSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        await using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var deleteTask = Task.Run(() => _fs.DeleteFile("input/turn_request.json"));
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await deleteTask;

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task CreateBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "before");
        Task<string?> backupTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            backupTask = Task.Run(() => _fs.CreateBackup(path));
            await Task.Delay(150);
            Assert.False(backupTask.IsCompleted);
        }

        var backupPath = await backupTask;
        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
    }

    [Fact]
    public async Task RestoreBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "current");
        var backupPath = _fs.ResolvePath(path) + ".test-backup";
        await File.WriteAllTextAsync(backupPath, "restored");
        Task restoreTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            restoreTask = Task.Run(() => _fs.RestoreBackup(backupPath, path));
            await Task.Delay(150);
            Assert.False(restoreTask.IsCompleted);
            Assert.Equal("current", await _fs.ReadFileAsync(path));
        }

        await restoreTask;
        Assert.Equal("restored", await _fs.ReadFileAsync(path));
    }

    [Fact]
    public async Task CleanupBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "current");
        var backupPath = _fs.ResolvePath(path) + ".test-backup";
        await File.WriteAllTextAsync(backupPath, "backup");
        Task cleanupTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            cleanupTask = Task.Run(() => _fs.CleanupBackup(backupPath));
            await Task.Delay(150);
            Assert.False(cleanupTask.IsCompleted);
            Assert.True(File.Exists(backupPath));
        }

        await cleanupTask;
        Assert.False(File.Exists(backupPath));
    }

    [Fact]
    public async Task ClearGameState_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "state");
        Task clearTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            clearTask = Task.Run(_fs.ClearGameState);
            await Task.Delay(150);
            Assert.False(clearTask.IsCompleted);
            Assert.True(_fs.FileExists(path));
        }

        await clearTask;
        Assert.False(_fs.FileExists(path));
    }

    [Fact]
    public async Task ClearCurrentWorldLore_WaitsForCanonicalWriteLease()
    {
        const string path = "lore/current_world/setting.md";
        await _fs.WriteFileAtomicAsync(path, "setting");
        Task clearTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            clearTask = Task.Run(_fs.ClearCurrentWorldLore);
            await Task.Delay(150);
            Assert.False(clearTask.IsCompleted);
            Assert.True(_fs.FileExists(path));
        }

        await clearTask;
        Assert.False(_fs.FileExists(path));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmBridgeRuntimeStatus()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_bridge_status.json", """{"ready":true}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.Equal("""{"ready":true}""", await _fs.ReadFileAsync("game_state/control/gm_bridge_status.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmContextPackJsonArtifacts()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/context_pack_manifest.json", """{"schemaVersion":1}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json", """{"progressionProcessingReport":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", """{"tempoAdvantage":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json", """{"probes":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json", """{"dimensions":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/context_pack_manifest.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task EnsureDirectoryStructure_RecoversInterruptedLoadBeforeCreatingEmptySession()
    {
        const string markerPath = "game_state/world/recovery_marker.json";
        await _fs.WriteFileAtomicAsync(markerPath, "{\"state\":\"last-valid\"}");

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "load-transactions",
            transactionId);
        var backupSessionPath = Path.Combine(transactionRoot, "backup", "game_session");
        Directory.CreateDirectory(Path.GetDirectoryName(backupSessionPath)!);
        Directory.Move(_fs.GameSessionPath, backupSessionPath);

        var journalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "load-transactions",
            "active.json");
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        await File.WriteAllTextAsync(
            journalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));

        _fs.EnsureDirectoryStructure();

        Assert.Equal("{\"state\":\"last-valid\"}", await _fs.ReadFileAsync(markerPath));
        Assert.False(File.Exists(journalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task CanonicalWriter_RecoversInterruptedWorkerApplyBeforeWriting()
    {
        const string firstPath = "game_state/world/weather.json";
        const string secondPath = "game_state/world/current_location.json";
        byte[] firstBaseline = [0x10, 0x11];
        byte[] firstApplied = [0x20, 0x21];
        byte[] secondBaseline = [0x30, 0x31];
        byte[] secondApplied = [0x40, 0x41];
        await _fs.WriteFileAtomicBytesAsync(firstPath, firstBaseline);
        await _fs.WriteFileAtomicBytesAsync(secondPath, secondBaseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), firstBaseline);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0001.bin"), secondBaseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = firstPath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(firstBaseline),
                        appliedSha256 = Sha256(firstApplied)
                    },
                    new
                    {
                        path = secondPath,
                        baselineExists = true,
                        beforeImage = "before/0001.bin",
                        beforeSha256 = Sha256(secondBaseline),
                        appliedSha256 = Sha256(secondApplied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));

        await File.WriteAllBytesAsync(_fs.ResolvePath(firstPath), firstApplied);

        await _fs.WriteFileAtomicBytesAsync("game_state/world/after_recovery.json", [0x55]);

        Assert.Equal(firstBaseline, await _fs.ReadFileBytesAsync(firstPath));
        Assert.Equal(secondBaseline, await _fs.ReadFileBytesAsync(secondPath));
        Assert.Equal([0x55], await _fs.ReadFileBytesAsync("game_state/world/after_recovery.json"));
        Assert.False(File.Exists(activeJournalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task CanonicalWriter_CompletesEveryRecoverableRollbackEntryBeforeFailingClosed()
    {
        const string restorablePath = "game_state/world/weather.json";
        const string unownedPath = "game_state/world/current_location.json";
        byte[] restorableBaseline = [0x10];
        byte[] restorableApplied = [0x20];
        byte[] unownedBaseline = [0x30];
        byte[] unownedApplied = [0x40];
        byte[] unownedNewer = [0x50];
        await _fs.WriteFileAtomicBytesAsync(restorablePath, restorableBaseline);
        await _fs.WriteFileAtomicBytesAsync(unownedPath, unownedBaseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), restorableBaseline);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0001.bin"), unownedBaseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = restorablePath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(restorableBaseline),
                        appliedSha256 = Sha256(restorableApplied)
                    },
                    new
                    {
                        path = unownedPath,
                        baselineExists = true,
                        beforeImage = "before/0001.bin",
                        beforeSha256 = Sha256(unownedBaseline),
                        appliedSha256 = Sha256(unownedApplied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, transactionId, committed = false }));
        await File.WriteAllBytesAsync(_fs.ResolvePath(restorablePath), restorableApplied);
        await File.WriteAllBytesAsync(_fs.ResolvePath(unownedPath), unownedNewer);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.WriteFileAtomicBytesAsync("game_state/world/must_not_write.json", [0x60]));

        Assert.Contains("unowned canonical bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(restorableBaseline, await _fs.ReadFileBytesAsync(restorablePath));
        Assert.Equal(unownedNewer, await _fs.ReadFileBytesAsync(unownedPath));
        Assert.False(_fs.FileExists("game_state/world/must_not_write.json"));
        Assert.True(File.Exists(activeJournalPath));
    }

    [Fact]
    public async Task CanonicalWriter_CleansCommittedWorkerApplyWithoutRollingItBack()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        await _fs.WriteFileAtomicBytesAsync(path, applied);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), baseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(baseline),
                        appliedSha256 = Sha256(applied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, transactionId, committed = true }));

        await _fs.WriteFileAtomicBytesAsync("game_state/world/after_commit.json", [0x70]);

        Assert.Equal(applied, await _fs.ReadFileBytesAsync(path));
        Assert.Equal([0x70], await _fs.ReadFileBytesAsync("game_state/world/after_commit.json"));
        Assert.False(File.Exists(activeJournalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task CommittedWorkerApply_CleanupFailureRetainsJournalForNextWriterRetry()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        var operations = new WorkerCleanupFaultOperations();
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            operations);
        fs.EnsureDirectoryStructure();
        await fs.WriteFileAtomicBytesAsync(path, baseline);

        CanonicalWorkerApplyTransaction transaction;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await fs.BeginWorkerApplyTransactionAsync(
                lease,
                [new CanonicalWorkerApplyChange(path, baseline, applied)]);
            Assert.Equal(
                CanonicalFileMutationResult.Applied,
                await fs.CompareExchangeFileBytesAsync(lease, path, baseline, applied));
            operations.FailWorkerTransactionDelete = true;
            fs.CommitWorkerApplyTransaction(lease, transaction);
        }

        Assert.True(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.True(Directory.Exists(transaction.TransactionRoot));
        Assert.Equal(applied, await fs.ReadFileBytesAsync(path));

        operations.FailWorkerTransactionDelete = false;
        await fs.WriteFileAtomicBytesAsync("game_state/world/after_retry.json", [0x30]);

        Assert.Equal(applied, await fs.ReadFileBytesAsync(path));
        Assert.Equal([0x30], await fs.ReadFileBytesAsync("game_state/world/after_retry.json"));
        Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.False(Directory.Exists(transaction.TransactionRoot));
    }

    [Fact]
    public async Task RolledBackWorkerApply_JournalDeleteFailureDoesNotPoisonNextWriter()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        var operations = new WorkerCleanupFaultOperations();
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            operations);
        fs.EnsureDirectoryStructure();
        await fs.WriteFileAtomicBytesAsync(path, baseline);

        CanonicalWorkerApplyTransaction transaction;
        IReadOnlyList<string> rollbackErrors;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await fs.BeginWorkerApplyTransactionAsync(
                lease,
                [new CanonicalWorkerApplyChange(path, baseline, applied)]);
            Assert.Equal(
                CanonicalFileMutationResult.Applied,
                await fs.CompareExchangeFileBytesAsync(lease, path, baseline, applied));
            operations.FailWorkerTransactionJournalDelete = true;
            rollbackErrors = await fs.RollbackWorkerApplyTransactionAsync(lease, transaction);
        }

        Assert.NotEmpty(rollbackErrors);
        Assert.Equal(baseline, await fs.ReadFileBytesAsync(path));
        Assert.False(Directory.Exists(transaction.TransactionRoot));
        Assert.True(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));

        operations.FailWorkerTransactionJournalDelete = false;
        await fs.WriteFileAtomicBytesAsync("game_state/world/after_rollback_retry.json", [0x30]);

        Assert.Equal(baseline, await fs.ReadFileBytesAsync(path));
        Assert.Equal([0x30], await fs.ReadFileBytesAsync("game_state/world/after_rollback_retry.json"));
        Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
    }

    [Fact]
    public async Task AppendFileAtomicIfCurrentSessionAsync_DropsStaleSessionTelemetry()
    {
        const string path = "game_state/control/gm_trajectory_ledger.jsonl";
        string capturedGeneration;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            capturedGeneration = _fs.GetOrCreateSessionGeneration(lease);

        Assert.True(await _fs.AppendFileAtomicIfCurrentSessionAsync(
            path,
            "{\"record\":1}" + Environment.NewLine,
            capturedGeneration));
        await using (var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease))
            _fs.RotateSessionGeneration(replacementLease);

        Assert.False(await _fs.AppendFileAtomicIfCurrentSessionAsync(
            path,
            "{\"record\":\"stale\"}" + Environment.NewLine,
            capturedGeneration));

        var ledger = await _fs.ReadFileAsync(path);
        Assert.Contains("\"record\":1", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("stale", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionGeneration_RejectsUppercaseNonCanonicalGuidText()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(
            _fs.SessionGenerationPath,
            "{\"schemaVersion\":1,\"generationId\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}");

        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var exception = Assert.Throws<InvalidDataException>(() => _fs.GetOrCreateSessionGeneration(lease));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanonicalFileOperations_RejectTraversalOutsideGameSession()
    {
        var outsidePath = Path.Combine(_rootPath, "outside.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicAsync("../outside.json", "{\"escaped\":true}"));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileBytesAsync("../outside.json"));

        Assert.False(File.Exists(outsidePath));
    }

    [Fact]
    public async Task CanonicalFileOperations_RejectExistingReparsePointAncestor()
    {
        var outsideDirectory = Path.Combine(_rootPath, "outside-target");
        Directory.CreateDirectory(outsideDirectory);
        var linkPath = _fs.ResolvePath("game_state/world/linked");
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        if (!TryCreateDirectoryLink(linkPath, outsideDirectory))
            return;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicAsync(
                "game_state/world/linked/escaped.json",
                "{\"escaped\":true}"));

        Assert.False(File.Exists(Path.Combine(outsideDirectory, "escaped.json")));
    }

    [Fact]
    public async Task AtomicWrite_RechecksReparseConfinementAtMutationBoundary()
    {
        const string relativePath = "game_state/world/mutation-boundary/state.json";
        var parentPath = _fs.ResolvePath("game_state/world/mutation-boundary");
        var displacedParentPath = _fs.ResolvePath("game_state/world/mutation-boundary-original");
        var outsideDirectory = Path.Combine(_rootPath, "mutation-boundary-outside");
        var probeLink = Path.Combine(_rootPath, "mutation-boundary-link-probe");
        Directory.CreateDirectory(parentPath);
        Directory.CreateDirectory(outsideDirectory);
        if (!TryCreateDirectoryLink(probeLink, outsideDirectory))
            return;
        Directory.Delete(probeLink);

        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (swapped || !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                        return Task.CompletedTask;

                    swapped = true;
                    Directory.Move(parentPath, displacedParentPath);
                    Directory.CreateSymbolicLink(parentPath, outsideDirectory);
                    return Task.CompletedTask;
                }
            });

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.WriteFileAtomicAsync(relativePath, "{\"escaped\":true}"));

            Assert.True(swapped);
            Assert.False(File.Exists(Path.Combine(outsideDirectory, "state.json")));
        }
        finally
        {
            if (Directory.Exists(parentPath) && FileSystemManager.IsReparsePoint(parentPath))
                Directory.Delete(parentPath);
            if (Directory.Exists(displacedParentPath))
                Directory.Move(displacedParentPath, parentPath);
        }
    }

    [Fact]
    public async Task RestoreBackup_RejectsBackupOutsideCanonicalSession()
    {
        const string originalPath = "game_state/world/restore-target.json";
        var outsideBackupPath = Path.Combine(_rootPath, "outside-restore.backup");
        await _fs.WriteFileAtomicAsync(originalPath, "{\"marker\":\"canonical\"}");
        await File.WriteAllTextAsync(outsideBackupPath, "{\"marker\":\"external\"}");

        Assert.Throws<InvalidDataException>(
            () => _fs.RestoreBackup(outsideBackupPath, originalPath));

        Assert.Contains(
            "canonical",
            await _fs.ReadFileAsync(originalPath),
            StringComparison.Ordinal);
        Assert.True(File.Exists(outsideBackupPath));
    }

    [Fact]
    public void FileSystemManagers_ForPhysicalRootAliases_ShareCanonicalRuntimeIdentity()
    {
        var aliasPath = Path.Combine(
            Path.GetDirectoryName(_rootPath)!,
            "boe-fs-alias-" + Guid.NewGuid().ToString("N"));
        if (!TryCreateDirectoryLink(aliasPath, _rootPath))
            return;

        try
        {
            var aliased = new FileSystemManager(
                aliasPath,
                NullLogger<FileSystemManager>.Instance);

            Assert.Equal(_fs.BasePath, aliased.BasePath, ignoreCase: true);
            Assert.Equal(
                _fs.CanonicalWriteLockPath,
                aliased.CanonicalWriteLockPath,
                ignoreCase: true);
        }
        finally
        {
            Directory.Delete(aliasPath);
        }
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class WorkerCleanupFaultOperations : ILoadTransactionOperations
    {
        internal bool FailWorkerTransactionDelete { get; set; }
        internal bool FailWorkerTransactionJournalDelete { get; set; }

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void MoveDirectory(string sourcePath, string destinationPath) =>
            Directory.Move(sourcePath, destinationPath);
        public void DeleteDirectory(string path, bool recursive)
        {
            if (FailWorkerTransactionDelete &&
                path.Contains(
                    $"{Path.DirectorySeparatorChar}worker-apply-transactions{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected worker apply transaction cleanup failure.");
            }

            Directory.Delete(path, recursive);
        }

        public bool FileExists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllTextAtomic(string path, string content) =>
            PhysicalLoadTransactionOperations.Instance.WriteAllTextAtomic(path, content);
        public void DeleteFile(string path)
        {
            if (FailWorkerTransactionJournalDelete &&
                path.EndsWith(
                    $"{Path.DirectorySeparatorChar}worker-apply-transactions{Path.DirectorySeparatorChar}active.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected worker apply active journal cleanup failure.");
            }

            File.Delete(path);
        }
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\""
        });
        if (process == null)
            throw new InvalidOperationException("Failed to start junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create test junction: exit code {process.ExitCode}.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }
}
