using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserLocalWriteCoordinatorTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

    public BrowserLocalWriteCoordinatorTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildStatusAsync_PendingTurnArtifacts_BlockBrowserWrites()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{}");
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", "{}");
        var coordinator = CreateCoordinator();

        var status = await coordinator.BuildStatusAsync();

        Assert.False(status.CanStartBrowserWrite);
        Assert.True(status.PendingTurn.HasActiveGmTurn);
        Assert.Contains(status.PendingTurn.Artifacts, static item =>
            item.Exists && string.Equals(item.Path, "input/turn_request.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(status.PendingTurn.Artifacts, static item =>
            item.Exists && string.Equals(item.Path, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ActiveOtherOwner_BlocksWithoutRunningWrite()
    {
        var lockService = new LocalUiSessionLockService(_fs, _timeProvider);
        await lockService.AcquireOrRefreshAsync(Owner("console-owner", "Консоль"), "console write");
        var coordinator = CreateCoordinator(lockService);
        var ran = false;

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/test_state.json"],
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        Assert.False(result.Success);
        Assert.False(ran);
        Assert.Contains("заблокировано", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("game_state/meta/test_state.json"));
    }

    [Fact]
    public async Task BuildStatusAsync_FreshMalformedLock_BlocksBrowserWrites()
    {
        await _fs.WriteFileAtomicAsync(LocalUiSessionLockService.LockPath, "{ not-json");
        File.SetLastWriteTimeUtc(_fs.ResolvePath(LocalUiSessionLockService.LockPath), _timeProvider.GetUtcNow().UtcDateTime);
        var coordinator = CreateCoordinator();

        var status = await coordinator.BuildStatusAsync();

        Assert.False(status.CanStartBrowserWrite);
        Assert.True(status.LocalUiLock.Exists);
        Assert.False(status.LocalUiLock.IsReadable);
        Assert.False(status.LocalUiLock.IsStale);
    }

    [Fact]
    public async Task ExecuteAsync_StaleOtherOwner_TakesOverAndRunsWrite()
    {
        var lockService = new LocalUiSessionLockService(_fs, _timeProvider);
        await lockService.AcquireOrRefreshAsync(Owner("console-owner", "Консоль"), "console write");
        _timeProvider.Advance(TimeSpan.FromMinutes(3));
        var coordinator = CreateCoordinator(lockService);

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/test_state.json"],
            writeLease => _fs.WriteFileAtomicAsync(
                writeLease,
                "game_state/meta/test_state.json",
                "{\"ok\":true}"));

        Assert.True(result.Success, result.Message);
        Assert.Equal("{\"ok\":true}", await _fs.ReadFileAsync("game_state/meta/test_state.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_FailedWrite_RestoresRollbackFilesAndReleasesLock()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/existing.json", "{\"value\":1}");
        var coordinator = CreateCoordinator();

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/existing.json", "game_state/meta/new_file.json"],
            async writeLease =>
            {
                await _fs.WriteFileAtomicAsync(writeLease, "game_state/meta/existing.json", "{\"value\":2}");
                await _fs.WriteFileAtomicAsync(writeLease, "game_state/meta/new_file.json", "{\"created\":true}");
                throw new InvalidOperationException("simulated browser write failure");
            });

        Assert.False(result.Success);
        Assert.Contains("simulated browser write failure", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{\"value\":1}", await _fs.ReadFileAsync("game_state/meta/existing.json"));
        Assert.False(_fs.FileExists("game_state/meta/new_file.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAtomicAsync_StagesDurableBeforeImagesBeforeWriteAndCleansThemAfterCommit()
    {
        const string existingPath = "game_state/meta/durable_existing.json";
        const string absentPath = "game_state/meta/durable_absent.json";
        await _fs.WriteFileAtomicBytesAsync(existingPath, [0x7B, 0x22, 0x76, 0x22, 0x3A, 0x31, 0x7D]);
        var expectedBytes = await _fs.ReadFileBytesAsync(existingPath);
        var coordinator = CreateCoordinator();
        var observedExactBackup = false;
        var observedAbsenceMarker = false;

        var result = await coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "durable before-images"),
            [existingPath, absentPath],
            async writeLease =>
            {
                var backup = Assert.Single(
                    ExplorerLocalTurnRollbackArtifacts.DiscoverBackups(_fs, [existingPath]));
                Assert.Equal(
                    expectedBytes,
                    await _fs.ReadFileBytesAsync(writeLease, backup.BackupPath));
                observedExactBackup = true;

                var rollbackRoot = _fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
                var manifestPath = Assert.Single(Directory.GetFiles(
                    rollbackRoot,
                    "*browser_write_manifest.json",
                    SearchOption.AllDirectories));
                var manifest = await File.ReadAllTextAsync(manifestPath);
                observedAbsenceMarker =
                    manifest.Contains(absentPath, StringComparison.OrdinalIgnoreCase) &&
                    manifest.Contains("\"existed\": false", StringComparison.Ordinal);

                await _fs.WriteFileAtomicAsync(writeLease, existingPath, "{\"v\":2}");
                await _fs.WriteFileAtomicAsync(writeLease, absentPath, "{\"created\":true}");
            });

        Assert.True(result.Success, result.Message);
        Assert.True(observedExactBackup);
        Assert.True(observedAbsenceMarker);
        Assert.False(Directory.Exists(_fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_IsRecoveredBeforeNextCanonicalMutation()
    {
        const string trackedPath = "game_state/meta/interrupted_browser_write.json";
        byte[] originalBytes = [0xEF, 0xBB, 0xBF, 0x7B, 0x22, 0x76, 0x22, 0x3A, 0x31, 0x7D];
        await _fs.WriteFileAtomicBytesAsync(trackedPath, originalBytes);

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
        }

        Assert.True(_fs.FileExists(transaction.ManifestPath));
        Assert.NotEqual(originalBytes, await _fs.ReadFileBytesAsync(trackedPath));

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/recovery_trigger.json",
            "{\"ok\":true}");

        Assert.Equal(originalBytes, await restartedFs.ReadFileBytesAsync(trackedPath));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
        Assert.False(Directory.Exists(restartedFs.ResolvePath(transaction.TransactionRoot)));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_IntentWithoutPublication_PreservesBaselineAndEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_write_intent_without_publication.json";
        const string baseline = """{"value":"before"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var mutationBoundaryReached = false;
        var faultingFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (!path.Equals(
                            trackedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    mutationBoundaryReached = true;
                    throw new IOException(
                        "simulated crash before physical publication");
                }
            });

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await faultingFs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        faultingFs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await Assert.ThrowsAsync<IOException>(() =>
                faultingFs.WriteFileAtomicAsync(
                    writeLease,
                    trackedPath,
                    """{"value":"never-published"}"""));
        }

        Assert.True(mutationBoundaryReached);
        Assert.Equal(baseline, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_write_intent_without_publication_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(baseline, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(
            _fs.ResolvePath(Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_write_intent_without_publication_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserDeletion_IntentWithoutDeletion_PreservesBaselineAndEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_delete_intent_without_publication.json";
        const string baseline = """{"value":"before"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var mutationBoundaryReached = false;
        var faultingFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (!path.Equals(
                            trackedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    mutationBoundaryReached = true;
                    throw new IOException(
                        "simulated crash before physical deletion");
                }
            });

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await faultingFs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        faultingFs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            Assert.Throws<IOException>(() =>
                faultingFs.DeleteFile(writeLease, trackedPath));
        }

        Assert.True(mutationBoundaryReached);
        Assert.Equal(baseline, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_delete_intent_without_publication_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(baseline, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(
            _fs.ResolvePath(Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_delete_intent_without_publication_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_ByteIdenticalForeignReplacementPreservesIdentityAndEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_write_identical_foreign.json";
        await _fs.WriteFileAtomicAsync(trackedPath, """{"value":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"owned"}""");
        }

        var trackedFullPath = _fs.ResolvePath(trackedPath);
        var ownedIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath);
        var ownedBytes = await File.ReadAllBytesAsync(trackedFullPath);
        var foreignPath = trackedFullPath + ".foreign";
        await File.WriteAllBytesAsync(foreignPath, ownedBytes);
        File.Delete(trackedFullPath);
        File.Move(foreignPath, trackedFullPath);
        var foreignIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath);
        Assert.NotEqual(ownedIdentity, foreignIdentity);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_write_identical_foreign_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(ownedBytes, await File.ReadAllBytesAsync(trackedFullPath));
        Assert.Equal(
            foreignIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(
            _fs.ResolvePath(Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_write_identical_foreign_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserDeletion_ByteIdenticalForeignRecreationPreservesIdentityAndEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_delete_identical_foreign.json";
        const string baseline = """{"value":"before"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var trackedFullPath = _fs.ResolvePath(trackedPath);
        var baselineIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath);
        var baselineBytes = await File.ReadAllBytesAsync(trackedFullPath);
        var foreignPath = trackedFullPath + ".foreign";
        await File.WriteAllBytesAsync(foreignPath, baselineBytes);

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            _fs.DeleteFile(writeLease, trackedPath);
        }

        File.Move(foreignPath, trackedFullPath);
        var foreignIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath);
        Assert.NotEqual(baselineIdentity, foreignIdentity);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_delete_identical_foreign_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(baselineBytes, await File.ReadAllBytesAsync(trackedFullPath));
        Assert.Equal(
            foreignIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(trackedFullPath));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(
            _fs.ResolvePath(Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_delete_identical_foreign_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_WithExistingBaseline_PreservesForeignPostImageAndEvidence()
    {
        const string trackedPath = "game_state/meta/browser_write_foreign_existing.json";
        const string foreignPostImage = """{"value":"foreign"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, """{"value":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"owned"}""");
        }

        await File.WriteAllTextAsync(_fs.ResolvePath(trackedPath), foreignPostImage);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_write_foreign_existing_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(foreignPostImage, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(_fs.ResolvePath(Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_write_foreign_existing_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_ForeignRewriteAfterOwnershipCheckBlocksRestoreAndRetainsEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_write_foreign_at_restore_boundary.json";
        const string foreignPostImage = """{"value":"foreign-at-boundary"}""";
        await _fs.WriteFileAtomicAsync(
            trackedPath,
            """{"value":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"owned"}""");
        }

        var foreignWriteInjected = false;
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    if (foreignWriteInjected ||
                        !path.Equals(
                            trackedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    foreignWriteInjected = true;
                    await File.WriteAllTextAsync(
                        _fs.ResolvePath(trackedPath),
                        foreignPostImage);
                }
            });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_write_foreign_at_restore_boundary_trigger.json",
                """{"ok":true}"""));

        Assert.True(foreignWriteInjected);
        Assert.Equal(
            foreignPostImage,
            await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(
            File.Exists(
                _fs.ResolvePath(
                    Assert.Single(transaction.Entries).BackupPath!)));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_WithoutBaseline_PreservesForeignPostImageAndEvidence()
    {
        const string trackedPath = "game_state/meta/browser_write_foreign_missing.json";
        const string foreignPostImage = """{"value":"foreign"}""";

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"owned"}""");
        }

        await File.WriteAllTextAsync(_fs.ResolvePath(trackedPath), foreignPostImage);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_write_foreign_missing_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(foreignPostImage, await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/browser_write_foreign_missing_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserCreate_ForeignRewriteAfterOwnershipCheckBlocksDeleteAndRetainsEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_create_foreign_at_delete_boundary.json";
        const string foreignPostImage = """{"value":"foreign-at-boundary"}""";

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"owned"}""");
        }

        var foreignWriteInjected = false;
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    if (foreignWriteInjected ||
                        !path.Equals(
                            trackedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    foreignWriteInjected = true;
                    await File.WriteAllTextAsync(
                        _fs.ResolvePath(trackedPath),
                        foreignPostImage);
                }
            });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_create_foreign_at_delete_boundary_trigger.json",
                """{"ok":true}"""));

        Assert.True(foreignWriteInjected);
        Assert.Equal(
            foreignPostImage,
            await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
    }

    [Fact]
    public async Task InterruptedStagedBrowserDeletion_RestoresOwnedDeletion()
    {
        const string trackedPath =
            "game_state/meta/browser_delete_owned.json";
        const string baseline = """{"value":"before"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            _fs.DeleteFile(writeLease, trackedPath);
        }

        Assert.False(_fs.FileExists(trackedPath));
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/browser_delete_owned_trigger.json",
            """{"ok":true}""");

        Assert.Equal(
            baseline,
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.False(
            restartedFs.FileExists(transaction.ManifestPath));
    }

    [Fact]
    public async Task InterruptedStagedBrowserDeletion_PreservesForeignRecreationAndEvidence()
    {
        const string trackedPath =
            "game_state/meta/browser_delete_foreign.json";
        const string foreignRecreation = """{"value":"foreign"}""";
        await _fs.WriteFileAtomicAsync(
            trackedPath,
            """{"value":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            _fs.DeleteFile(writeLease, trackedPath);
        }

        await File.WriteAllTextAsync(
            _fs.ResolvePath(trackedPath),
            foreignRecreation);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/browser_delete_foreign_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(
            foreignRecreation,
            await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.True(
            File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(
            File.Exists(
                _fs.ResolvePath(
                    Assert.Single(transaction.Entries).BackupPath!)));
        Assert.False(
            restartedFs.FileExists(
                "game_state/meta/browser_delete_foreign_trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_RemovesDeclaredRollbackCleanupDirectories()
    {
        const string trackedPath = "game_state/meta/interrupted_dynamic_artifacts.json";
        const string snapshotRoot = "game_state/control/pending_turn_snapshot";
        var dynamicRollbackRoot =
            $"{ExplorerLocalTurnRollbackArtifacts.Root}/browser_direct_gacha";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write",
                [snapshotRoot, dynamicRollbackRoot]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                $"{snapshotRoot}/game_state/meta/soul_state.json",
                "{\"snapshot\":true}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                $"{dynamicRollbackRoot}/123_evidence/soul_state.rollback.1",
                "{\"rollback\":true}");
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/dynamic_cleanup_recovery_trigger.json",
            "{\"ok\":true}");

        Assert.Equal(
            "{\"value\":\"before\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.False(Directory.Exists(restartedFs.ResolvePath(snapshotRoot)));
        Assert.False(Directory.Exists(restartedFs.ResolvePath(dynamicRollbackRoot)));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_RestoresExternalDarenRewardProfile()
    {
        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, "{\"tier\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                ["game_state/meta/soul_state.json"],
                "browser_write",
                rollbackExternalFileIds:
                [
                    ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId
                ]);
        }
        await File.WriteAllTextAsync(profilePath, "{\"tier\":\"interrupted\"}");

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/external_profile_recovery_trigger.json",
            "{\"ok\":true}");

        Assert.Equal("{\"tier\":\"before\"}", await File.ReadAllTextAsync(profilePath));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
    }

    [Fact]
    public async Task LegacyDarenRollback_WithBaseline_DoesNotOverwriteNewerProfileOrTrackedState()
    {
        const string trackedPath = "game_state/meta/legacy-daren-existing.json";
        const string interruptedTracked = """{"value":"interrupted"}""";
        const string newerProfile = """{"tier":"newer"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, """{"value":"before"}""");
        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, """{"tier":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "legacy_daren_existing",
                        rollbackExternalFileIds:
                        [
                            ExplorerLocalTurnRollbackArtifacts
                                .DarenRewardProfileExternalFileId
                        ]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                interruptedTracked);
        }

        await DowngradeDarenExternalManifestToSchema3Async(
            transaction.ManifestPath);
        await File.WriteAllTextAsync(profilePath, newerProfile);

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/legacy-daren-existing-trigger.json",
                """{"ok":true}"""));

        Assert.Equal(
            interruptedTracked,
            await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.Equal(newerProfile, await File.ReadAllTextAsync(profilePath));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.True(File.Exists(_fs.ResolvePath(
            Assert.Single(transaction.ExternalEntries).BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/legacy-daren-existing-trigger.json"));
    }

    [Fact]
    public async Task LegacyDarenRollback_WithoutBaseline_DoesNotDeleteNewerProfileOrRestoreTrackedState()
    {
        const string trackedPath = "game_state/meta/legacy-daren-missing.json";
        const string interruptedTracked = """{"value":"interrupted"}""";
        const string newerProfile = """{"tier":"newer"}""";
        await _fs.WriteFileAtomicAsync(trackedPath, """{"value":"before"}""");
        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "legacy_daren_missing",
                        rollbackExternalFileIds:
                        [
                            ExplorerLocalTurnRollbackArtifacts
                                .DarenRewardProfileExternalFileId
                        ]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                interruptedTracked);
        }

        await DowngradeDarenExternalManifestToSchema3Async(
            transaction.ManifestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, newerProfile);

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/legacy-daren-missing-trigger.json",
                """{"ok":true}"""));

        Assert.Equal(
            interruptedTracked,
            await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
        Assert.Equal(newerProfile, await File.ReadAllTextAsync(profilePath));
        Assert.True(File.Exists(_fs.ResolvePath(transaction.ManifestPath)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/legacy-daren-missing-trigger.json"));
    }

    [Fact]
    public async Task InterruptedStagedBrowserWrite_CleanupFailureRetainsRestoredManifestForRetry()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string trackedPath = "game_state/meta/cleanup-retry.json";
        const string triggerPath = "game_state/meta/cleanup-retry-trigger.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "cleanup_retry");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
        }

        var backupPath = _fs.ResolvePath(
            Assert.Single(transaction.Entries).BackupPath!);
        var manifestPath = _fs.ResolvePath(transaction.ManifestPath);
        var transactionRoot = _fs.ResolvePath(transaction.TransactionRoot);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await using (var blocker = new FileStream(
                         backupPath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read))
        {
            await Assert.ThrowsAsync<IOException>(
                () => restartedFs.WriteFileAtomicAsync(
                    triggerPath,
                    "{\"ok\":true}"));

            Assert.Equal(
                "{\"value\":\"before\"}",
                await File.ReadAllTextAsync(_fs.ResolvePath(trackedPath)));
            Assert.True(File.Exists(manifestPath));
            Assert.Contains(
                "\"status\": \"restored\"",
                await File.ReadAllTextAsync(manifestPath),
                StringComparison.Ordinal);
            Assert.False(restartedFs.FileExists(triggerPath));
        }

        await restartedFs.WriteFileAtomicAsync(triggerPath, "{\"ok\":true}");

        Assert.True(restartedFs.FileExists(triggerPath));
        Assert.False(File.Exists(manifestPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task SessionReplacementLease_RecoversInterruptedExternalProfileBeforeReplacingSession()
    {
        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, "{\"tier\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                ["game_state/meta/soul_state.json"],
                "browser_write",
                rollbackExternalFileIds:
                [
                    ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId
                ]);
        }
        await File.WriteAllTextAsync(profilePath, "{\"tier\":\"interrupted\"}");

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await using var lifecycleLease =
            await restartedFs.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease =
            await restartedFs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);

        Assert.Equal("{\"tier\":\"before\"}", await File.ReadAllTextAsync(profilePath));
        Assert.False(restartedFs.FileExists(replacementLease, transaction.ManifestPath));
    }

    [Fact]
    public async Task InterruptedDeferredDarenPublication_RestoresExactBaselineIdentity()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        byte[] baseline = """{"tier":"before"}"""u8.ToArray();
        byte[] published = """{"tier":"published"}"""u8.ToArray();
        await File.WriteAllBytesAsync(profilePath, baseline);
        var baselineIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath);

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [],
                        "browser_write",
                        rollbackExternalFileIds:
                        [
                            ExplorerLocalTurnRollbackArtifacts
                                .DarenRewardProfileExternalFileId
                        ]);
            await transaction.DarenTransaction!.PublishAsync(
                published,
                CancellationToken.None);
            Assert.Equal(
                published,
                transaction.DarenTransaction.ReadCurrentBytes());
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/deferred_daren_rollback_trigger.json",
            "{\"ok\":true}");

        Assert.Equal(baseline, await File.ReadAllBytesAsync(profilePath));
        Assert.Equal(
            baselineIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
        Assert.Empty(Directory.GetDirectories(
            restartedFs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public async Task InterruptedCommittedDarenPublication_PreservesPublishedIdentity()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, "{\"tier\":\"before\"}");
        byte[] published = """{"tier":"published"}"""u8.ToArray();

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [],
                        "browser_write",
                        rollbackExternalFileIds:
                        [
                            ExplorerLocalTurnRollbackArtifacts
                                .DarenRewardProfileExternalFileId
                        ]);
            await transaction.DarenTransaction!.PublishAsync(
                published,
                CancellationToken.None);
            transaction.DarenTransaction.Commit();
        }
        var publishedIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath);

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/deferred_daren_commit_trigger.json",
            "{\"ok\":true}");

        Assert.Equal(published, await File.ReadAllBytesAsync(profilePath));
        Assert.Equal(
            publishedIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
        Assert.Empty(Directory.GetDirectories(
            restartedFs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public async Task DarenCapture_MissingReversibleBackendFailsBeforeRollbackEvidence()
    {
        var hooks = FileSystemManagerHookTestHelper.WithBooleanOverride(
            "SupportsReversibleFileReplacementOverride",
            false);
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        fs.EnsureDirectoryStructure();
        await using var writeLease =
            await fs.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => ExplorerLocalTurnRollbackArtifacts
                .StageBrowserWriteTransactionAsync(
                    fs,
                    writeLease,
                    [],
                    "browser_write",
                    rollbackExternalFileIds:
                    [
                        ExplorerLocalTurnRollbackArtifacts
                            .DarenRewardProfileExternalFileId
                    ]));

        Assert.False(Directory.Exists(
            fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task DarenAbsentBaseline_DanglingProfileLinkFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using var writeLease =
            await _fs.AcquireCanonicalWriteLeaseAsync();
        var transaction =
            await ExplorerLocalTurnRollbackArtifacts
                .StageBrowserWriteTransactionAsync(
                    _fs,
                    writeLease,
                    [],
                    "browser_write",
                    rollbackExternalFileIds:
                    [
                        ExplorerLocalTurnRollbackArtifacts
                            .DarenRewardProfileExternalFileId
                    ]);

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        var missingTarget = Path.Combine(
            _rootPath,
            "missing-daren-profile-target.json");
        if (!TryCreateFileLink(profilePath, missingTarget))
            return;

        try
        {
            Assert.Throws<InvalidDataException>(
                () => transaction.DarenTransaction!.ReadCurrentBytes());
            Assert.True(
                File.GetAttributes(profilePath)
                    .HasFlag(FileAttributes.ReparsePoint));
        }
        finally
        {
            try
            {
                File.Delete(profilePath);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }

    [Fact]
    public async Task ExecuteSessionReplacementAsync_FailedOldOperationDoesNotReleaseNewSameOwnerLock()
    {
        var lockService = new LocalUiSessionLockService(_fs, _timeProvider);
        var coordinator = CreateCoordinator(lockService);
        var replacementOwner = Owner(
            "browser-main-menu-load",
            "Browser main menu");
        byte[]? replacementLockBytes = null;

        var result = await coordinator.ExecuteSessionReplacementAsync(
            new BrowserLocalWriteRequest(
                replacementOwner.OwnerId,
                replacementOwner.OwnerLabel,
                "browser save load"),
            async () =>
            {
                await SessionReplacementTestHarness.RotateGenerationAsync(_fs);
                var replacementLock = await lockService.AcquireOrRefreshAsync(
                    replacementOwner,
                    "replacement browser write");
                Assert.True(
                    replacementLock.Acquired,
                    replacementLock.BlockerMessage);
                replacementLockBytes = await _fs.ReadFileBytesAsync(
                    LocalUiSessionLockService.LockPath);
                throw new InvalidOperationException(
                    "deterministic replacement failure");
            });

        Assert.False(result.Success);
        Assert.NotNull(replacementLockBytes);
        Assert.Equal(
            replacementLockBytes,
            await _fs.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task BrowserWriteRollbackCleanup_RejectsBroadCanonicalDirectory()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                ["game_state/meta/soul_state.json"],
                "browser_write",
                ["lore"]));
    }

    [Fact]
    public async Task BrowserWriteRollbackCleanup_RejectsUnownedRollbackSubtree()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                ["game_state/meta/soul_state.json"],
                "browser_write",
                [$"{ExplorerLocalTurnRollbackArtifacts.Root}/another_transaction"]));
    }

    [Fact]
    public async Task InterruptedCommittedBrowserWrite_PreservesCommittedBytesAndCleansEvidence()
    {
        const string trackedPath = "game_state/meta/committed_browser_write.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"committed\"}");
            await ExplorerLocalTurnRollbackArtifacts.MarkBrowserWriteTransactionCommittedAsync(
                _fs,
                writeLease,
                transaction);
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(
            "game_state/meta/recovery_trigger.json",
            "{\"ok\":true}");

        Assert.Equal(
            "{\"value\":\"committed\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.False(restartedFs.FileExists(transaction.ManifestPath));
        Assert.False(Directory.Exists(restartedFs.ResolvePath(transaction.TransactionRoot)));
    }

    [Fact]
    public async Task CommittedBrowserWrite_ManifestCleanupFailureNeverReentersRollback()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string trackedPath =
            "game_state/meta/committed_manifest_cleanup_failure.json";
        const string triggerPath =
            "game_state/meta/committed_manifest_cleanup_trigger.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts
                .StageBrowserWriteTransactionAsync(
                    _fs,
                    writeLease,
                    [trackedPath],
                    "committed_manifest_cleanup");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"committed\"}");
            await ExplorerLocalTurnRollbackArtifacts
                .MarkBrowserWriteTransactionCommittedAsync(
                    _fs,
                    writeLease,
                    transaction);

            await using var manifestBlocker = new FileStream(
                _fs.ResolvePath(transaction.ManifestPath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            Assert.False(
                ExplorerLocalTurnRollbackArtifacts
                    .TryDeleteBrowserWriteTransaction(
                        _fs,
                        writeLease,
                        transaction,
                        ExplorerLocalTurnRollbackArtifacts
                            .BrowserWriteCleanupOutcome.Committed,
                        out var cleanupFailure));
            Assert.NotNull(cleanupFailure);
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(triggerPath, "{\"ok\":true}");

        Assert.Equal(
            "{\"value\":\"committed\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.True(restartedFs.FileExists(triggerPath));
        Assert.False(Directory.Exists(
            restartedFs.ResolvePath(transaction.TransactionRoot)));
    }

    [Fact]
    public async Task CommittedBrowserWrite_CleanupIntentLastRecoversAfterManifestDeletion()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string trackedPath =
            "game_state/meta/committed_cleanup_intent_failure.json";
        const string triggerPath =
            "game_state/meta/committed_cleanup_intent_trigger.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts
                .StageBrowserWriteTransactionAsync(
                    _fs,
                    writeLease,
                    [trackedPath],
                    "committed_cleanup_intent");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"committed\"}");
            await ExplorerLocalTurnRollbackArtifacts
                .MarkBrowserWriteTransactionCommittedAsync(
                    _fs,
                    writeLease,
                    transaction);

            await using (var manifestBlocker = new FileStream(
                             _fs.ResolvePath(transaction.ManifestPath),
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.ReadWrite))
            {
                Assert.False(
                    ExplorerLocalTurnRollbackArtifacts
                        .TryDeleteBrowserWriteTransaction(
                            _fs,
                            writeLease,
                            transaction,
                            ExplorerLocalTurnRollbackArtifacts
                                .BrowserWriteCleanupOutcome.Committed,
                            out _));
            }

            var transactionRoot = _fs.ResolvePath(transaction.TransactionRoot);
            var cleanupIntentPath = Assert.Single(
                Directory.GetFiles(
                    transactionRoot,
                    "browser_write_cleanup_*.intent",
                    SearchOption.TopDirectoryOnly));
            await using var intentBlocker = new FileStream(
                cleanupIntentPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            Assert.False(
                ExplorerLocalTurnRollbackArtifacts
                    .TryDeleteBrowserWriteTransaction(
                        _fs,
                        writeLease,
                        transaction,
                        ExplorerLocalTurnRollbackArtifacts
                            .BrowserWriteCleanupOutcome.Committed,
                        out _));
            Assert.False(File.Exists(
                _fs.ResolvePath(transaction.ManifestPath)));
            Assert.True(File.Exists(cleanupIntentPath));
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await restartedFs.WriteFileAtomicAsync(triggerPath, "{\"ok\":true}");

        Assert.Equal(
            "{\"value\":\"committed\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.True(restartedFs.FileExists(triggerPath));
        Assert.False(Directory.Exists(
            restartedFs.ResolvePath(transaction.TransactionRoot)));
    }

    [Fact]
    public async Task BuildStatusAsync_RecoversInterruptedBrowserWriteBeforeReportingBlockers()
    {
        const string trackedPath = "game_state/meta/status_recovery.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            _ = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        var coordinator = new BrowserLocalWriteCoordinator(
            restartedFs,
            new LocalUiSessionLockService(restartedFs, _timeProvider),
            _timeProvider);

        var status = await coordinator.BuildStatusAsync();

        Assert.True(status.CanStartBrowserWrite);
        Assert.False(status.PendingTurn.HasActiveGmTurn);
        Assert.Equal(
            "{\"value\":\"before\"}",
            await restartedFs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task MalformedInterruptedBrowserManifest_BlocksWriterAndRetainsEvidence()
    {
        const string trackedPath = "game_state/meta/malformed_manifest_write.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                transaction.ManifestPath,
                "{ not-json");
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/malformed_recovery_trigger.json",
                "{\"ok\":true}"));

        Assert.Equal(
            "{\"value\":\"interrupted\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.True(restartedFs.FileExists(transaction.ManifestPath));
        Assert.All(
            transaction.Entries.Where(static entry => entry.Existed),
            entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
        Assert.False(restartedFs.FileExists("game_state/meta/malformed_recovery_trigger.json"));
    }

    [Fact]
    public async Task DuplicateBrowserManifestStatus_BlocksWriterAndRetainsEvidence()
    {
        const string trackedPath =
            "game_state/meta/duplicate_manifest_status.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        string ambiguousManifest;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
            ambiguousManifest =
                (await _fs.ReadFileAsync(writeLease, transaction.ManifestPath))!
                .Replace(
                    "\"status\": \"staged\",",
                    "\"status\": \"staged\",\n  \"Status\": \"committed\",",
                    StringComparison.Ordinal);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                transaction.ManifestPath,
                ambiguousManifest);
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/duplicate_manifest_trigger.json",
                "{\"ok\":true}"));

        Assert.Equal(
            "{\"value\":\"interrupted\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.Equal(
            ambiguousManifest,
            await restartedFs.ReadFileAsync(transaction.ManifestPath));
        Assert.All(
            transaction.Entries.Where(static entry => entry.Existed),
            entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/duplicate_manifest_trigger.json"));
    }

    [Theory]
    [InlineData("missingIdentity")]
    [InlineData("missingDigest")]
    [InlineData("incomplete")]
    [InlineData("operationMismatch")]
    public async Task PartialBrowserPublicationReceipt_BlocksWriterAndRetainsEvidence(
        string mutation)
    {
        const string trackedPath =
            "game_state/meta/partial_publication_receipt.json";
        await _fs.WriteFileAtomicAsync(
            trackedPath,
            """{"value":"before"}""");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction
            transaction;
        await using (var writeLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                """{"value":"interrupted"}""");
            var manifest = JsonNode.Parse(
                    (await _fs.ReadFileAsync(
                        writeLease,
                        transaction.ManifestPath))!)
                !.AsObject();
            var entry = Assert.Single(manifest["entries"]!.AsArray())
                !.AsObject();
            var receipt = entry["publicationReceipt"]!.AsObject();
            switch (mutation)
            {
                case "missingIdentity":
                    receipt.Remove("physicalIdentity");
                    break;
                case "missingDigest":
                    receipt.Remove("sha256");
                    break;
                case "incomplete":
                    receipt["completed"] = false;
                    break;
                case "operationMismatch":
                    receipt["operation"] = "delete";
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported receipt mutation '{mutation}'.");
            }

            await _fs.WriteFileAtomicAsync(
                writeLease,
                transaction.ManifestPath,
                manifest.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/partial_publication_receipt_trigger.json",
                """{"ok":true}"""));

        Assert.Equal(
            """{"value":"interrupted"}""",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.True(restartedFs.FileExists(transaction.ManifestPath));
        Assert.True(restartedFs.FileExists(
            Assert.Single(transaction.Entries).BackupPath!));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/partial_publication_receipt_trigger.json"));
    }

    [Fact]
    public async Task BrowserRollback_RegularFileAtCleanupDirectoryRetainsEvidence()
    {
        const string trackedPath =
            "game_state/meta/wrong-kind-cleanup-root.json";
        const string cleanupDirectory =
            "game_state/control/pending_turn_snapshot";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write",
                        [cleanupDirectory]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
        }

        var cleanupPath = _fs.ResolvePath(cleanupDirectory);
        byte[] wrongKindEvidence = [0x72, 0x83, 0x94];
        await File.WriteAllBytesAsync(cleanupPath, wrongKindEvidence);
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/wrong-kind-cleanup-trigger.json",
                "{\"ok\":true}"));

        Assert.Equal(
            "{\"value\":\"before\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.Equal(
            wrongKindEvidence,
            await File.ReadAllBytesAsync(cleanupPath));
        Assert.True(restartedFs.FileExists(transaction.ManifestPath));
        Assert.All(
            transaction.Entries.Where(static entry => entry.Existed),
            entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/wrong-kind-cleanup-trigger.json"));
    }

    [Fact]
    public async Task BrowserRollback_CleanupDirectoryReplacedByFileBeforeDeletionRetainsEvidence()
    {
        const string trackedPath =
            "game_state/meta/raced-cleanup-root.json";
        const string cleanupDirectory =
            "game_state/control/pending_turn_snapshot";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction =
                await ExplorerLocalTurnRollbackArtifacts
                    .StageBrowserWriteTransactionAsync(
                        _fs,
                        writeLease,
                        [trackedPath],
                        "browser_write",
                        [cleanupDirectory]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");
        }

        var cleanupPath = _fs.ResolvePath(cleanupDirectory);
        Directory.CreateDirectory(cleanupPath);
        byte[] replacementEvidence = [0x25, 0x36, 0x47];
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!swapped &&
                    path.Equals(
                        cleanupDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(cleanupPath, recursive: true);
                    File.WriteAllBytes(cleanupPath, replacementEvidence);
                    swapped = true;
                }

                return Task.CompletedTask;
            }
        };
        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/raced-cleanup-trigger.json",
                "{\"ok\":true}"));

        Assert.True(swapped);
        Assert.Equal(
            replacementEvidence,
            await File.ReadAllBytesAsync(cleanupPath));
        Assert.True(restartedFs.FileExists(transaction.ManifestPath));
        Assert.All(
            transaction.Entries.Where(static entry => entry.Existed),
            entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
        Assert.False(restartedFs.FileExists(
            "game_state/meta/raced-cleanup-trigger.json"));
    }

    [Fact]
    public async Task LegacyBrowserManifest_RejectsRestoredStatusAndRetainsEvidence()
    {
        const string trackedPath = "game_state/meta/legacy_restored_manifest.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [trackedPath],
                "browser_write");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                trackedPath,
                "{\"value\":\"interrupted\"}");

            var manifest = (await _fs.ReadFileAsync(writeLease, transaction.ManifestPath))!
                .Replace("\"schemaVersion\": 6", "\"schemaVersion\": 2", StringComparison.Ordinal)
                .Replace("\"status\": \"staged\"", "\"status\": \"restored\"", StringComparison.Ordinal);
            await _fs.WriteFileAtomicAsync(writeLease, transaction.ManifestPath, manifest);
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => restartedFs.WriteFileAtomicAsync(
                "game_state/meta/legacy_restored_trigger.json",
                "{\"ok\":true}"));

        Assert.Equal(
            "{\"value\":\"interrupted\"}",
            await restartedFs.ReadFileAsync(trackedPath));
        Assert.True(restartedFs.FileExists(transaction.ManifestPath));
        Assert.All(
            transaction.Entries.Where(static entry => entry.Existed),
            entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
        Assert.False(restartedFs.FileExists("game_state/meta/legacy_restored_trigger.json"));
    }

    [Fact]
    public async Task InterruptedBrowserRestoreFailure_RetainsEvidenceAndRestoresRemainingFiles()
    {
        const string blockedPath = "game_state/meta/interrupted_blocked.json";
        const string restorablePath = "game_state/meta/interrupted_restorable.json";
        const string snapshotRoot = "game_state/control/pending_turn_snapshot";
        var dynamicRollbackRoot =
            $"{ExplorerLocalTurnRollbackArtifacts.Root}/browser_direct_gacha";
        await _fs.WriteFileAtomicAsync(blockedPath, "{\"value\":\"before\"}");
        await _fs.WriteFileAtomicAsync(restorablePath, "{\"value\":\"before\"}");

        ExplorerLocalTurnRollbackArtifacts.BrowserWriteRollbackTransaction transaction;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                _fs,
                writeLease,
                [blockedPath, restorablePath],
                "browser_write",
                [snapshotRoot, dynamicRollbackRoot]);
            await _fs.WriteFileAtomicAsync(
                writeLease,
                blockedPath,
                "{\"value\":\"interrupted\"}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                restorablePath,
                "{\"value\":\"interrupted\"}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                $"{snapshotRoot}/game_state/meta/soul_state.json",
                "{\"snapshot\":true}");
            await _fs.WriteFileAtomicAsync(
                writeLease,
                $"{dynamicRollbackRoot}/123_evidence/soul_state.rollback.1",
                "{\"rollback\":true}");
        }

        var restartedFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        await using (var blocker = new FileStream(
                         _fs.ResolvePath(blockedPath),
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.None))
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => restartedFs.WriteFileAtomicAsync(
                    "game_state/meta/blocked_recovery_trigger.json",
                    "{\"ok\":true}"));

            Assert.Equal(
                "{\"value\":\"before\"}",
                await restartedFs.ReadFileAsync(restorablePath));
            Assert.True(restartedFs.FileExists(transaction.ManifestPath));
            Assert.All(
                transaction.Entries.Where(static entry => entry.Existed),
                entry => Assert.True(restartedFs.FileExists(entry.BackupPath!)));
            Assert.True(Directory.Exists(restartedFs.ResolvePath(snapshotRoot)));
            Assert.True(Directory.Exists(restartedFs.ResolvePath(dynamicRollbackRoot)));
            Assert.False(restartedFs.FileExists("game_state/meta/blocked_recovery_trigger.json"));
        }

        Assert.Equal(
            "{\"value\":\"interrupted\"}",
            await restartedFs.ReadFileAsync(blockedPath));
    }

    [Fact]
    public async Task ExecuteAsync_FirstRollbackFailureStillRestoresRemainingFiles()
    {
        const string blockedPath = "game_state/meta/blocked_restore.json";
        const string restorablePath = "game_state/meta/restorable.json";
        await _fs.WriteFileAtomicAsync(blockedPath, "{\"value\":1}");
        await _fs.WriteFileAtomicAsync(restorablePath, "{\"value\":1}");
        var blockedBefore = await _fs.ReadFileBytesAsync(blockedPath);
        var coordinator = CreateCoordinator();
        FileStream? restoreBlocker = null;

        try
        {
            var result = await coordinator.ExecuteAsync(
                new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
                [blockedPath, restorablePath],
                async writeLease =>
                {
                    await _fs.WriteFileAtomicAsync(writeLease, blockedPath, "{\"value\":2}");
                    await _fs.WriteFileAtomicAsync(writeLease, restorablePath, "{\"value\":2}");
                    restoreBlocker = new FileStream(
                        _fs.ResolvePath(blockedPath),
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None);
                    throw new InvalidOperationException("simulated multi-file write failure");
                });

            Assert.False(result.Success);
            Assert.Contains("не полностью", result.Message, StringComparison.OrdinalIgnoreCase);
            restoreBlocker!.Dispose();
            restoreBlocker = null;
            Assert.Equal("{\"value\":2}", await _fs.ReadFileAsync(blockedPath));
            Assert.Equal("{\"value\":1}", await _fs.ReadFileAsync(restorablePath));
            var retainedBackup = Assert.Single(
                ExplorerLocalTurnRollbackArtifacts.DiscoverBackups(_fs, [blockedPath]));
            Assert.Equal(
                blockedBefore,
                await _fs.ReadFileBytesAsync(retainedBackup.BackupPath));
            Assert.True(BrowserPendingTurnInspector.Build(_fs).HasActiveGmTurn);

            var laterWriteRan = false;
            var laterResult = await coordinator.ExecuteAsync(
                new BrowserLocalWriteRequest("browser-owner", "Browser", "write after partial rollback"),
                ["game_state/meta/later.json"],
                writeLease =>
                {
                    laterWriteRan = true;
                    return _fs.WriteFileAtomicAsync(
                        writeLease,
                        "game_state/meta/later.json",
                        "{\"ok\":true}");
                });
            Assert.True(laterResult.Success, laterResult.Message);
            Assert.True(laterWriteRan);
            Assert.Equal("{\"value\":1}", await _fs.ReadFileAsync(blockedPath));
            Assert.Equal("{\"value\":1}", await _fs.ReadFileAsync(restorablePath));
            Assert.False(BrowserPendingTurnInspector.Build(_fs).HasActiveGmTurn);
        }
        finally
        {
            restoreBlocker?.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction()
    {
        const string trackedPath = "game_state/meta/existing.json";
        const string stalePath = "game_state/meta/stale_browser_write.json";
        const string replacementMarkerPath = "game_state/meta/replacement_marker.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"session\":\"A\",\"value\":1}");
        var coordinator = CreateCoordinator();
        var firstWriteCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSecondWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var browserWrite = coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            [trackedPath, stalePath],
            async writeLease =>
            {
                await _fs.WriteFileAtomicAsync(writeLease, trackedPath, "{\"session\":\"A\",\"value\":2}");
                firstWriteCompleted.SetResult();
                await allowSecondWrite.Task;
                await _fs.WriteFileAtomicAsync(writeLease, stalePath, "{\"session\":\"A\"}");
            });

        await firstWriteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replacement = _fs.ClearGameStateAsync();
        Assert.False(replacement.IsCompleted);
        allowSecondWrite.SetResult();

        var result = await browserWrite.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"session\":\"B\",\"value\":9}");
        await _fs.WriteFileAtomicAsync(replacementMarkerPath, "{\"session\":\"B\"}");

        Assert.True(result.Success, result.Message);
        Assert.Equal(
            "{\"session\":\"B\",\"value\":9}",
            await _fs.ReadFileAsync(trackedPath));
        Assert.Equal(
            "{\"session\":\"B\"}",
            await _fs.ReadFileAsync(replacementMarkerPath));
        Assert.False(_fs.FileExists(stalePath));
    }

    [Fact]
    public async Task ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction()
    {
        var transactionRoot = Path.Combine(_rootPath, "atomic-transaction");
        Directory.CreateDirectory(transactionRoot);
        var transactionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowTransactionCommit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transactionFullyWritten = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowTransactionFinish = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = new FileSystemManager(
            transactionRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    replacementContended.TrySetResult();
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs, _timeProvider),
            _timeProvider);

        var transaction = coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "atomic browser write"),
            ["game_state/meta/spend.json", "input/turn_request.json"],
            async writeLease =>
            {
                await fs.WriteFileAtomicAsync(
                    writeLease,
                    "game_state/meta/spend.json",
                    "{\"spent\":true}");
                transactionStarted.SetResult();
                await allowTransactionCommit.Task;
                await fs.WriteFileAtomicAsync(
                    writeLease,
                    "input/turn_request.json",
                    "{\"queued\":true}");
                transactionFullyWritten.SetResult();
                await allowTransactionFinish.Task;
            });

        await transactionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        allowTransactionCommit.SetResult();
        await transactionFullyWritten.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            "{\"spent\":true}",
            await fs.ReadFileAsync("game_state/meta/spend.json"));
        Assert.Equal(
            "{\"queued\":true}",
            await fs.ReadFileAsync("input/turn_request.json"));
        Assert.False(replacement.IsCompleted);
        allowTransactionFinish.SetResult();
        var transactionResult = await transaction.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(transactionResult.Success, transactionResult.Message);
        Assert.False(fs.FileExists("game_state/meta/spend.json"));
    }

    [Fact]
    public async Task ExecuteAtomicAsync_DoesNotCreateUiLockBeforeCanonicalLease()
    {
        var transactionRoot = Path.Combine(_rootPath, "pre-lease-ui-lock");
        Directory.CreateDirectory(transactionRoot);
        var canonicalLeaseContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = new FileSystemManager(
            transactionRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    canonicalLeaseContended.TrySetResult();
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs, _timeProvider),
            _timeProvider);
        string generation;
        await using (var generationLease = await fs.AcquireCanonicalWriteLeaseAsync())
            generation = fs.GetOrCreateSessionGeneration(generationLease);

        var lockExistedBeforeCanonicalAuthority = false;
        BrowserLocalWriteResult? result = null;
        await SessionOperationContext.RunBoundAsync(
            fs,
            generation,
            async () =>
            {
                var blockingLease = await fs.AcquireCanonicalWriteLeaseAsync();
                var transaction = coordinator.ExecuteAtomicAsync(
                    new BrowserLocalWriteRequest("browser-owner", "Browser", "atomic browser write"),
                    Array.Empty<string>(),
                    _ => Task.CompletedTask);

                await canonicalLeaseContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
                lockExistedBeforeCanonicalAuthority = fs.FileExists(LocalUiSessionLockService.LockPath);
                await blockingLease.DisposeAsync();
                result = await transaction.WaitAsync(TimeSpan.FromSeconds(5));
            });

        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);
        Assert.False(lockExistedBeforeCanonicalAuthority);
        Assert.False(fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation()
    {
        const string trackedPath = "game_state/meta/committed.json";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"value\":\"before\"}");
        var coordinator = CreateCoordinator();
        FileStream? lockBlocker = null;

        try
        {
            var result = await coordinator.ExecuteAtomicAsync(
                new BrowserLocalWriteRequest("browser-owner", "Browser", "atomic browser write"),
                [trackedPath],
                async writeLease =>
                {
                    await _fs.WriteFileAtomicAsync(
                        writeLease,
                        trackedPath,
                        "{\"value\":\"committed\"}");
                    lockBlocker = new FileStream(
                        _fs.ResolvePath(LocalUiSessionLockService.LockPath),
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None);
                });

            Assert.True(result.Success, result.Message);
            Assert.Equal(
                "{\"value\":\"committed\"}",
                await _fs.ReadFileAsync(trackedPath));
        }
        finally
        {
            lockBlocker?.Dispose();
            if (_fs.FileExists(LocalUiSessionLockService.LockPath))
                _fs.DeleteFile(LocalUiSessionLockService.LockPath);
        }
    }

    [Fact]
    public async Task ExecuteAtomicAsync_RollbackPreparationFailureReleasesUiLock()
    {
        var coordinator = CreateCoordinator();
        var writeRan = false;

        var result = await coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest(
                "browser-owner",
                "Browser",
                "rollback preparation failure"),
            ["game_state/meta/not-written.json"],
            _ =>
            {
                writeRan = true;
                return Task.CompletedTask;
            },
            prepareAfterRollback: () => throw new InvalidOperationException("snapshot failed"));

        Assert.False(result.Success);
        Assert.False(writeRan);
        Assert.Contains("snapshot failed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority()
    {
        const string trackedPath = "game_state/meta/explicit-lease.json";
        var coordinator = CreateCoordinator();

        var result = await coordinator.ExecuteAtomicAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "explicit lease write"),
            [trackedPath],
            async writeLease =>
            {
                await _fs.WriteFileAtomicAsync(
                        writeLease,
                        trackedPath,
                        "{\"value\":\"committed\"}")
                    .WaitAsync(TimeSpan.FromSeconds(1));
            });

        Assert.True(result.Success, result.Message);
        Assert.Equal(
            "{\"value\":\"committed\"}",
            await _fs.ReadFileAsync(trackedPath));
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
            // ignored
        }
    }

    private BrowserLocalWriteCoordinator CreateCoordinator(LocalUiSessionLockService? lockService = null) =>
        new(_fs, lockService ?? new LocalUiSessionLockService(_fs, _timeProvider), _timeProvider);

    private static LocalUiSessionLockOwner Owner(string id, string label) =>
        new(id, "console", label, TimeSpan.FromMinutes(2));

    private async Task DowngradeDarenExternalManifestToSchema3Async(
        string manifestPath)
    {
        var fullPath = _fs.ResolvePath(manifestPath);
        var manifest = JsonNode.Parse(
                await File.ReadAllTextAsync(fullPath))!
            .AsObject();
        manifest["schemaVersion"] = 3;
        var externalEntry = Assert.Single(
                manifest["externalEntries"]!.AsArray())
            !.AsObject();
        externalEntry.Remove("parentIdentity");
        externalEntry.Remove("baselineIdentity");
        externalEntry.Remove("publishedIdentity");
        externalEntry.Remove("publishedSha256");
        externalEntry.Remove("publicationTransactionId");
        await File.WriteAllTextAsync(
            fullPath,
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    private static bool TryCreateFileLink(
        string linkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
                PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan offset) => _utcNow = _utcNow.Add(offset);
    }
}
