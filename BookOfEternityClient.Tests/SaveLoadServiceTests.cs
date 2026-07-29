using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SaveLoadServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly SaveLoadService _service;

    public SaveLoadServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-save-load-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        stateManager.RefreshGameStateAsync().GetAwaiter().GetResult();
        _service = new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
    }

    [Fact]
    public async Task SaveGameAsync_FailureBeforeCommitLeavesNoPartialSaveOrTemporaryFile()
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var service = new SaveLoadService(
            _fs,
            stateManager,
            NullLogger<SaveLoadService>.Instance,
            new SaveLoadServiceHooks
            {
                BeforeSaveCommitAsync = () =>
                    throw new InvalidOperationException("simulated save commit failure")
            });

        var saved = await service.SaveGameAsync(
            "atomic_failure",
            "must not publish a partial archive");

        Assert.False(saved);
        var saveDirectory = _fs.ResolvePath("saves/manual_saves");
        Assert.Empty(Directory.GetFiles(saveDirectory, "*.zip"));
        Assert.Empty(Directory.GetFiles(saveDirectory, "*.tmp.*"));
    }

    [Fact]
    public async Task SaveGameAsync_SaveDirectoryReplacedAtCommitCannotPublishOutsideSession()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var saveDirectory = _fs.ResolvePath("saves/manual_saves");
        var displacedSaveDirectory = _fs.ResolvePath("saves/manual_saves-original");
        var outsideDirectory = Path.Combine(
            Path.GetTempPath(),
            "boe-save-publish-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);
        var swapped = false;
        var service = new SaveLoadService(
            _fs,
            new StateManager(
                _fs,
                new GameSettings(),
                NullLogger<StateManager>.Instance),
            NullLogger<SaveLoadService>.Instance,
            new SaveLoadServiceHooks
            {
                BeforeSaveCommitAsync = () =>
                {
                    Directory.Move(saveDirectory, displacedSaveDirectory);
                    CreateDirectoryJunction(saveDirectory, outsideDirectory);
                    swapped = true;
                    var stagedSave = Directory
                        .GetFiles(displacedSaveDirectory, "*.tmp.*")
                        .SingleOrDefault();
                    if (stagedSave != null)
                    {
                        File.Copy(
                            stagedSave,
                            Path.Combine(outsideDirectory, Path.GetFileName(stagedSave)));
                    }
                    return Task.CompletedTask;
                }
            });

        try
        {
            Assert.False(await service.SaveGameAsync(
                "save_destination_race",
                "save destination race regression"));
            Assert.True(swapped);
            Assert.Empty(Directory.GetFiles(outsideDirectory, "*.zip"));
        }
        finally
        {
            if (Directory.Exists(saveDirectory) && FileSystemManager.IsReparsePoint(saveDirectory))
                Directory.Delete(saveDirectory, recursive: false);
            if (Directory.Exists(displacedSaveDirectory))
                Directory.Move(displacedSaveDirectory, saveDirectory);
            if (Directory.Exists(outsideDirectory))
                Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveGameAsync_RuntimeStagingParentSwapCannotCreateExternalArchive()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var outsideDirectory = Path.Combine(
            Path.GetTempPath(),
            "boe-save-staging-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);
        var outsideArchive = Path.Combine(outsideDirectory, "save.zip");
        var outsideSentinel = new byte[] { 0x31, 0x42, 0x53, 0x64 };
        await File.WriteAllBytesAsync(outsideArchive, outsideSentinel);

        var stagingRoot = string.Empty;
        var displacedStagingRoot = string.Empty;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeRuntimeFileCreateAsync = path =>
                {
                    if (swapAttempted ||
                        !path.EndsWith(
                            $"{Path.DirectorySeparatorChar}save.zip",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    swapAttempted = true;
                    stagingRoot = Path.GetDirectoryName(path)!;
                    displacedStagingRoot = stagingRoot + "-displaced";
                    try
                    {
                        Directory.Move(stagingRoot, displacedStagingRoot);
                        CreateDirectoryJunction(stagingRoot, outsideDirectory);
                        swapped = true;
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException)
                    {
                        swapBlocked = true;
                    }

                    return Task.CompletedTask;
                }
            });
        var service = new SaveLoadService(
            raceFs,
            new StateManager(
                raceFs,
                new GameSettings(),
                NullLogger<StateManager>.Instance),
            NullLogger<SaveLoadService>.Instance);

        try
        {
            Assert.True(await service.SaveGameAsync(
                "runtime_staging_authority",
                "runtime staging authority regression"));
            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                outsideSentinel,
                await File.ReadAllBytesAsync(outsideArchive));
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(stagingRoot) &&
                    Directory.Exists(stagingRoot) &&
                    FileSystemManager.IsReparsePoint(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: false);
                }

                if (!string.IsNullOrWhiteSpace(displacedStagingRoot) &&
                    Directory.Exists(displacedStagingRoot) &&
                    !Directory.Exists(stagingRoot))
                {
                    Directory.Move(displacedStagingRoot, stagingRoot);
                }
            }
            catch
            {
                // Best effort cleanup for the intentionally raced staging tree.
            }

            if (Directory.Exists(outsideDirectory))
                Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AutosaveAsync_DirectoryReplacedAfterEnumerationCannotDeleteOutsideFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string oldAutosaveRelativePath = "saves/autosaves/old.zip";
        await _fs.WriteFileAtomicBytesAsync(oldAutosaveRelativePath, [1, 2, 3]);
        var oldAutosavePath = _fs.ResolvePath(oldAutosaveRelativePath);
        File.SetCreationTimeUtc(oldAutosavePath, DateTime.UtcNow.AddDays(-2));

        var autosaveDirectory = _fs.ResolvePath("saves/autosaves");
        var displacedAutosaveDirectory = _fs.ResolvePath("saves/autosaves-original");
        var outsideDirectory = Path.Combine(
            Path.GetTempPath(),
            "boe-autosave-delete-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);
        var outsideOldAutosave = Path.Combine(outsideDirectory, "old.zip");
        await File.WriteAllBytesAsync(outsideOldAutosave, [9, 8, 7]);
        var swapped = false;
        var service = new SaveLoadService(
            _fs,
            new StateManager(
                _fs,
                new GameSettings { MaxAutosaves = 1 },
                NullLogger<StateManager>.Instance),
            NullLogger<SaveLoadService>.Instance,
            new SaveLoadServiceHooks
            {
                BeforeAutosaveDeletionAsync = () =>
                {
                    Directory.Move(autosaveDirectory, displacedAutosaveDirectory);
                    CreateDirectoryJunction(autosaveDirectory, outsideDirectory);
                    swapped = true;
                    return Task.CompletedTask;
                }
            });

        try
        {
            Assert.True(await service.AutosaveAsync(12));
            Assert.True(swapped);
            Assert.True(File.Exists(outsideOldAutosave));
            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(outsideOldAutosave));
        }
        finally
        {
            if (Directory.Exists(autosaveDirectory) &&
                FileSystemManager.IsReparsePoint(autosaveDirectory))
            {
                Directory.Delete(autosaveDirectory, recursive: false);
            }
            if (Directory.Exists(displacedAutosaveDirectory))
                Directory.Move(displacedAutosaveDirectory, autosaveDirectory);
            if (Directory.Exists(outsideDirectory))
                Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_TreatsProgressionReportAsEphemeralControlFile()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3
        }
        """);
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 4,
            "newLastGuardianProjectCycleOrdinal": 4
          }
        }
        """);

        Assert.True(await _service.SaveGameAsync("ephemeral_progression_report", "save/load ephemeral regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
            Assert.Null(archive.GetEntry(ProgressionScheduleService.ReportPath));

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    [Fact]
    public async Task SaveAndLoad_TreatsLatestWorkerRepairTaskAsEphemeralControlFile()
    {
        const string path = "game_state/control/gm_worker_latest_validation_repair_task.json";
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        await _fs.WriteFileAtomicAsync(path, "{\"taskId\":\"stale_worker_task\"}");

        Assert.True(await _service.SaveGameAsync("ephemeral_worker_task", "worker task save/load regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
            Assert.Null(archive.GetEntry(path));

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.False(_fs.FileExists(path));
    }

    [Fact]
    public async Task SaveAndLoad_TreatsLocalUiSessionLockAsEphemeralControlFile()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Chaos Sea\"}");
        await _fs.WriteFileAtomicAsync(
            LocalUiSessionLockService.LockPath,
            """
            {
              "schemaVersion": 1,
              "ownerId": "stale-browser-owner",
              "ownerKind": "browser",
              "ownerLabel": "Stale browser",
              "acquiredAtUtc": "2026-07-29T00:00:00.0000000Z",
              "heartbeatAtUtc": "2026-07-29T00:00:00.0000000Z",
              "leaseSeconds": 120,
              "lastOperation": "stale form"
            }
            """);

        Assert.True(await _service.SaveGameAsync(
            "ephemeral_local_ui_lock",
            "local UI lock save/load regression"));

        var savePath = Directory
            .GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip")
            .Single();
        using (var archive = ZipFile.OpenRead(savePath))
            Assert.Null(archive.GetEntry(LocalUiSessionLockService.LockPath));

        using (var archive = ZipFile.Open(savePath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry(LocalUiSessionLockService.LockPath);
            await using var stream = entry.Open();
            await stream.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes(
                    """{ "ownerId": "crafted-restored-owner" }"""));
        }

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task SaveGameAsync_ExcludesBrowserRollbackTransactions()
    {
        var stalePath =
            $"{ExplorerLocalTurnRollbackArtifacts.Root}/browser_write/stale_evidence/marker.json";
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        await _fs.WriteFileAtomicAsync(stalePath, "{\"stale\":true}");

        Assert.True(await _service.SaveGameAsync(
            "ephemeral_browser_rollback",
            "browser rollback save regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using var archive = ZipFile.OpenRead(savePath);
        Assert.Null(archive.GetEntry(stalePath));
    }

    [Fact]
    public async Task LoadGameAsync_StripsBrowserRollbackTransactionsFromLegacyArchive()
    {
        var stalePath =
            $"{ExplorerLocalTurnRollbackArtifacts.Root}/browser_write/legacy_evidence/marker.json";
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        Assert.True(await _service.SaveGameAsync(
            "legacy_browser_rollback",
            "legacy browser rollback load regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.Open(savePath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry(stalePath);
            await using var stream = entry.Open();
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("{\"stale\":true}"));
        }

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.False(Directory.Exists(_fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task LoadGameAsync_StripsExactBrowserRollbackRootFileFromCraftedArchive()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Chaos Sea\"}");
        Assert.True(await _service.SaveGameAsync(
            "exact_browser_rollback_file",
            "exact browser rollback root load regression"));

        var savePath = Directory
            .GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip")
            .Single();
        using (var archive = ZipFile.Open(savePath, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry(
                ExplorerLocalTurnRollbackArtifacts.Root);
            await using var stream = entry.Open();
            await stream.WriteAsync(
                System.Text.Encoding.UTF8.GetBytes("{\"stale\":true}"));
        }

        Assert.True(await _service.LoadGameAsync(savePath));
        var rollbackRoot = _fs.ResolvePath(
            ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.False(File.Exists(rollbackRoot));
        Assert.False(Directory.Exists(rollbackRoot));
    }

    [Fact]
    public async Task GetAvailableSavesAsync_RetriesWhenSaveMetadataIsTemporarilyLocked()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        Assert.True(await _service.SaveGameAsync("temporarily_locked_metadata", "save list transient lock regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        await using var lockStream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None);
        var readTask = _service.GetAvailableSavesAsync("saves/manual_saves");

        await Task.Delay(75);
        await lockStream.DisposeAsync();
        var saves = await readTask;

        var save = Assert.Single(saves);
        Assert.Equal(savePath, save.FileName);
        Assert.Equal("temporarily_locked_metadata", save.Metadata?.SaveName);
    }

    [Fact]
    public async Task SaveGameAsync_WaitsForCanonicalWriteLeaseBeforeReadingSessionSnapshot()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", "{\"state\":\"stable\"}");
        Task<bool> saveTask;

        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            saveTask = _service.SaveGameAsync("leased_save", "canonical save snapshot regression");
            await Task.Delay(200);
            Assert.False(saveTask.IsCompleted);
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        Assert.True(await saveTask);
    }

    [Fact]
    public async Task SaveGameAsync_DoesNotArchiveDirectoryJunctionTarget()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-save-load-outside-" + Guid.NewGuid().ToString("N"));
        var outsideFile = Path.Combine(outsideRoot, "external-secret.txt");
        var junctionPath = _fs.ResolvePath("game_state/world/external-link");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(outsideFile, "must-not-enter-save");
        try
        {
            CreateDirectoryJunction(junctionPath, outsideRoot);

            Assert.True(await _service.SaveGameAsync("no_reparse_traversal", "junction regression"));

            var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
            using var archive = ZipFile.OpenRead(savePath);
            Assert.DoesNotContain(
                archive.Entries,
                entry => entry.FullName.Contains("external-secret", StringComparison.OrdinalIgnoreCase));
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
    public async Task SaveGameAsync_ParentReplacedByJunctionBeforeReadFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/world/save-race/state.json";
        var parentPath = _fs.ResolvePath("game_state/world/save-race");
        var displacedParentPath = _fs.ResolvePath("game_state/world/save-race-original");
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-save-load-race-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        await _fs.WriteFileAtomicAsync(relativePath, "{\"canonical\":true}");
        await File.WriteAllTextAsync(
            Path.Combine(outsideRoot, "state.json"),
            "{\"externalSecret\":true}");

        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (swapped || !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                        return Task.CompletedTask;

                    swapped = true;
                    Directory.Move(parentPath, displacedParentPath);
                    CreateDirectoryJunction(parentPath, outsideRoot);
                    return Task.CompletedTask;
                }
            });
        var raceStateManager = new StateManager(
            raceFs,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var raceService = new SaveLoadService(
            raceFs,
            raceStateManager,
            NullLogger<SaveLoadService>.Instance);

        try
        {
            Assert.False(await raceService.SaveGameAsync(
                "save_read_race",
                "canonical read race regression"));
            Assert.True(swapped);
            var saveDirectory = raceFs.ResolvePath("saves/manual_saves");
            Assert.True(
                !Directory.Exists(saveDirectory) ||
                Directory.GetFiles(saveDirectory, "*.zip").Length == 0);
        }
        finally
        {
            if (Directory.Exists(parentPath) && FileSystemManager.IsReparsePoint(parentPath))
                Directory.Delete(parentPath, recursive: false);
            if (Directory.Exists(displacedParentPath))
                Directory.Move(displacedParentPath, parentPath);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AutosaveAsync_CleanupWaitsForCanonicalWriteLeaseAfterSavePublication()
    {
        var cleanupReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = new GameSettings { MaxAutosaves = 1 };
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var service = new SaveLoadService(
            _fs,
            stateManager,
            NullLogger<SaveLoadService>.Instance,
            new SaveLoadServiceHooks
            {
                BeforeAutosaveCleanupLeaseAcquisitionAsync = async () =>
                {
                    cleanupReached.TrySetResult();
                    await releaseCleanup.Task;
                }
            });

        var autosaveTask = service.AutosaveAsync(1);
        await cleanupReached.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            releaseCleanup.TrySetResult();
            await Task.Delay(200);
            Assert.False(autosaveTask.IsCompleted);
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        Assert.True(await autosaveTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(Directory.GetFiles(_fs.ResolvePath("saves/autosaves"), "*.zip"));
    }

    [Fact]
    public async Task LoadGameAsync_WaitsForCanonicalWriteLeaseBeforeReplacingLiveSession()
    {
        const string weatherPath = "game_state/world/weather.json";
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"saved\"}");
        Assert.True(await _service.SaveGameAsync("leased_load", "canonical load swap regression"));
        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"live\"}");
        Task<bool> loadTask;

        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            loadTask = _service.LoadGameAsync(savePath);
            await Task.Delay(250);
            Assert.False(loadTask.IsCompleted);
            Assert.Equal("{\"state\":\"live\"}", await _fs.ReadFileAsync(weatherPath));
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        Assert.True(await loadTask);
        Assert.Equal("{\"state\":\"saved\"}", await _fs.ReadFileAsync(weatherPath));
    }

    [Fact]
    public async Task LoadGameAsync_WaitsForSessionLifecycleLeaseBeforeReplacingLiveSession()
    {
        const string weatherPath = "game_state/world/weather.json";
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"saved\"}");
        Assert.True(await _service.SaveGameAsync("lifecycle_load", "session lifecycle regression"));
        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"live\"}");

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
        var settings = new GameSettings();
        var stateManager = new StateManager(
            competingFs,
            settings,
            NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var service = new SaveLoadService(
            competingFs,
            stateManager,
            NullLogger<SaveLoadService>.Instance);

        var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        Task<bool> loadTask;
        try
        {
            loadTask = service.LoadGameAsync(savePath);
            await lifecycleContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(loadTask.IsCompleted);
            Assert.Equal("{\"state\":\"live\"}", await _fs.ReadFileAsync(weatherPath));
        }
        finally
        {
            await lifecycleLease.DisposeAsync();
        }

        Assert.True(await loadTask);
        Assert.Equal("{\"state\":\"saved\"}", await _fs.ReadFileAsync(weatherPath));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesLivePendingContracts()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, """
        {
          "requests": [
            {
              "requestId": "abode_roster_req_1",
              "guardianId": "guardian_test",
              "guardianName": "Тестовый Хранитель",
              "abodeId": "abode_test",
              "abodeName": "Тестовая Обитель",
              "currentReputation": 10,
              "requestMode": "standard_roster",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, """
        {
          "requests": [
            {
              "requestId": "guardian_social_req_1",
              "guardianId": "guardian_test",
              "guardianName": "Тестовый Хранитель",
              "interactionType": "talk",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "preset_test",
          "targetPresetDisplayName": "Тестовый Хранитель",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "summary",
          "renderedPromptPackage": "dossier",
          "lastUpdated": "2026-04-22T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, """
        {
          "requests": [
            {
              "requestId": "shining_trade_req_1",
              "factionId": "faction_test",
              "factionName": "Хор Теста",
              "tradeCycleId": "shining_return_3",
              "derivedTradeTier": 2,
              "derivedTradeSlotCount": 6,
              "derivedRarityCeiling": "rare",
              "derivedServiceMultiplier": 1.25,
              "merchantProfile": "shining_faction",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "archive_consult_1",
          "archiveId": "archive_entry_1",
          "archiveTitle": "Запись Памяти",
          "requestedMode": "consultation",
          "createdAtTurn": 3,
          "createdAtUtc": "2026-04-22T00:00:00Z"
        }
        """);

        Assert.True(await _service.SaveGameAsync("ephemeral_chaos_pending", "save/load chaos pending regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
        {
            Assert.NotNull(archive.GetEntry(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
            Assert.NotNull(archive.GetEntry(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
            Assert.NotNull(archive.GetEntry(SystemGuardianLibraryService.AttractionRequestPath));
            Assert.NotNull(archive.GetEntry(ShiningTradeRequestState.PendingRequestsPath));
            Assert.NotNull(archive.GetEntry(AfterlifeArchiveActionState.ConsultationRequestPath));
        }

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.True(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
        Assert.True(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
    }

    [Fact]
    public async Task LoadGameAsync_RejectsZipSlipEntriesAndPreservesLiveSession()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(soulStatePath, """
        {
          "soulName": "Живая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 7
        }
        """);

        var outsidePath = Path.Combine(_rootPath, "outside.txt");
        var maliciousZip = Path.Combine(_rootPath, "zip_slip.zip");
        using (var archive = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
        {
            using var entryWriter = new StreamWriter(archive.CreateEntry("../../outside.txt").Open());
            await entryWriter.WriteAsync("owned");
        }

        Assert.False(await _service.LoadGameAsync(maliciousZip));
        Assert.False(File.Exists(outsidePath));

        var soulState = await _fs.ReadFileAsync(soulStatePath);
        Assert.NotNull(soulState);
        Assert.Contains("Живая Душа", soulState);
    }

    [Fact]
    public async Task LoadGameAsync_CorruptArchiveDoesNotDestroyLiveSession()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(soulStatePath, """
        {
          "soulName": "Неприкосновенная Душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 2
        }
        """);
        await _fs.WriteFileAtomicAsync("mods/custom_rule.json", """
        { "enabled": true }
        """);

        var corruptZip = Path.Combine(_rootPath, "corrupt.zip");
        await File.WriteAllTextAsync(corruptZip, "this is not a zip archive");

        Assert.False(await _service.LoadGameAsync(corruptZip));
        Assert.True(_fs.FileExists(soulStatePath));
        Assert.True(_fs.FileExists("mods/custom_rule.json"));

        var soulState = await _fs.ReadFileAsync(soulStatePath);
        Assert.NotNull(soulState);
        Assert.Contains("Неприкосновенная Душа", soulState);
    }

    [Fact]
    public async Task LoadGameAsync_RepairsClientOwnedProfileMirrorWithoutReacquiringCanonicalLease()
    {
        var archivePath = Path.Combine(_rootPath, "stale_player_soul_mirror.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            await WriteArchiveEntryAsync(archive, "game_state/meta/soul_state.json", """
            {
              "soulName": "Пепельная Искра",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 7,
              "inkFeathers": { "current": 0, "total": 0 },
              "enlightenment": { "experience": 0, "level": 0 },
              "afterlifeCombatProfile": {
                "spiritFocusTier": 0,
                "artTiers": { "guard": 0, "recover_spiritual_power": 0 }
              }
            }
            """);
            await WriteArchiveEntryAsync(archive, "game_state/meta/afterlife_entity_profiles.json", """
            {
              "schemaVersion": 1,
              "profiles": [
                {
                  "actorType": "player_soul",
                  "actorId": "player_soul",
                  "displayName": "Пепельная Искра",
                  "realm": "Chaos Sea",
                  "currencies": { "inkFeathers": 4, "lightSparks": 0 },
                  "progression": {
                    "enlightenment": { "experience": 0, "tier": 0 },
                    "radiance": { "experience": 0, "tier": 0 }
                  },
                  "standardArts": { "guard": 1, "recover_spiritual_power": 1 },
                  "progressionStrategy": {
                    "strategyId": "strategy_player",
                    "priorityOrder": [ "guard" ],
                    "lastAutoProgressionCycleKey": "chaos:14"
                  },
                  "progressionLedger": []
                }
              ]
            }
            """);
        }

        var stopwatch = Stopwatch.StartNew();
        Assert.True(await _service.LoadGameAsync(archivePath));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Load took {stopwatch.Elapsed}.");
        using var doc = JsonDocument.Parse(
            await _fs.ReadFileAsync("game_state/meta/afterlife_entity_profiles.json") ?? "{}");
        var player = doc.RootElement.GetProperty("profiles").EnumerateArray().Single();
        Assert.Equal(0, player.GetProperty("currencies").GetProperty("inkFeathers").GetInt32());
        Assert.Equal(0, player.GetProperty("standardArts").GetProperty("guard").GetInt32());
        Assert.False(player.GetProperty("progressionStrategy").TryGetProperty("lastAutoProgressionCycleKey", out _));
    }

    [Fact]
    public async Task LoadGameAsync_WhenCommitJournalWriteFails_RestoresDiskAndRuntimeSnapshot()
    {
        var root = Path.Combine(_rootPath, "commit-journal-failure");
        Directory.CreateDirectory(root);
        var operations = new FaultInjectingLoadTransactionOperations();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, operations);
        fs.EnsureDirectoryStructure();
        var settings = new GameSettings { Language = "ru" };
        var stateManager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);
        await fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        { "characterName": "Старый герой" }
        """);
        await fs.WriteFileAtomicAsync("config.json", """
        { "language": "ru" }
        """);
        const string workerTaskPath = "worker_tasks/actor-materialization/task.json";
        const string workerProposalPath = "worker_proposals/actor-materialization/proposal.json";
        await fs.WriteFileAtomicAsync(workerTaskPath, "{\"owner\":\"old-session\"}");
        await fs.WriteFileAtomicAsync(workerProposalPath, "{\"owner\":\"old-session\"}");
        string previousGeneration;
        await using (var generationLease = await fs.AcquireCanonicalWriteLeaseAsync())
            previousGeneration = fs.GetOrCreateSessionGeneration(generationLease);
        await stateManager.RefreshGameStateAsync();
        await stateManager.LoadSettingsAsync();

        var archivePath = Path.Combine(root, "new-session.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            await WriteArchiveEntryAsync(archive, "game_state/core/player_status.json", """
            { "characterName": "Новый герой" }
            """);
            await WriteArchiveEntryAsync(archive, "config.json", """
            { "language": "en" }
            """);
        }

        operations.FailCommittedJournalWrites = true;
        var service = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);

        Assert.False(await service.LoadGameAsync(archivePath));
        Assert.Equal("Старый герой", stateManager.CurrentState.CharacterName);
        Assert.Equal("ru", stateManager.Settings.Language);
        Assert.Contains("Старый герой", await fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal("{\"owner\":\"old-session\"}", await fs.ReadFileAsync(workerTaskPath));
        Assert.Equal("{\"owner\":\"old-session\"}", await fs.ReadFileAsync(workerProposalPath));
        await using (var generationLease = await fs.AcquireCanonicalWriteLeaseAsync())
            Assert.True(fs.IsCurrentSessionGeneration(generationLease, previousGeneration));
        Assert.False(File.Exists(fs.ActiveLoadTransactionJournalPath));
    }

    [Fact]
    public async Task LoadGameAsync_WhenRollbackMoveFails_PreservesBackupForStartupRecovery()
    {
        var root = Path.Combine(_rootPath, "rollback-move-failure");
        Directory.CreateDirectory(root);
        var operations = new FaultInjectingLoadTransactionOperations();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, operations);
        fs.EnsureDirectoryStructure();
        const string markerPath = "game_state/world/recovery_marker.json";
        await fs.WriteFileAtomicAsync(markerPath, "{\"state\":\"last-valid\"}");
        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();

        var archivePath = Path.Combine(root, "activation-failure.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await WriteArchiveEntryAsync(archive, markerPath, "{\"state\":\"replacement\"}");

        operations.FailStagedActivationMove = true;
        operations.FailBackupRestoreMove = true;
        var service = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);

        Assert.False(await service.LoadGameAsync(archivePath));
        Assert.True(File.Exists(fs.ActiveLoadTransactionJournalPath));
        var backupMarker = Directory.GetFiles(
                Path.Combine(root, ".boe_runtime", "load-transactions"),
                "recovery_marker.json",
                SearchOption.AllDirectories)
            .Single(path => path.Contains($"{Path.DirectorySeparatorChar}backup{Path.DirectorySeparatorChar}"));
        Assert.Equal("{\"state\":\"last-valid\"}", await File.ReadAllTextAsync(backupMarker));

        operations.FailStagedActivationMove = false;
        operations.FailBackupRestoreMove = false;
        fs.EnsureDirectoryStructure();

        Assert.Equal("{\"state\":\"last-valid\"}", await fs.ReadFileAsync(markerPath));
        Assert.False(File.Exists(fs.ActiveLoadTransactionJournalPath));
    }

    [Fact]
    public async Task UnresolvedLoadRollback_FencesCanonicalWritersUntilRecoverySucceeds()
    {
        var root = Path.Combine(_rootPath, "rollback-writer-fence");
        Directory.CreateDirectory(root);
        var operations = new FaultInjectingLoadTransactionOperations();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, operations);
        fs.EnsureDirectoryStructure();
        const string markerPath = "game_state/world/recovery_marker.json";
        const string laterWritePath = "game_state/world/later_write.json";
        await fs.WriteFileAtomicAsync(markerPath, "{\"state\":\"last-valid\"}");
        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();

        var archivePath = Path.Combine(root, "activation-failure.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            await WriteArchiveEntryAsync(archive, markerPath, "{\"state\":\"replacement\"}");

        operations.FailStagedActivationMove = true;
        operations.FailBackupRestoreMove = true;
        var service = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);

        Assert.False(await service.LoadGameAsync(archivePath));
        Assert.True(File.Exists(fs.ActiveLoadTransactionJournalPath));

        await Assert.ThrowsAsync<IOException>(() =>
            fs.WriteFileAtomicAsync(laterWritePath, "{\"state\":\"must-not-commit\"}"));
        await Assert.ThrowsAsync<IOException>(() => fs.ClearGameStateAsync());
        Assert.False(await service.SaveGameAsync("must-not-save", "unresolved rollback fence"));
        Assert.False(fs.FileExists(laterWritePath));
        var saveDirectory = fs.ResolvePath("saves/manual_saves");
        Assert.True(!Directory.Exists(saveDirectory) || Directory.GetFiles(saveDirectory, "*.zip").Length == 0);
        Assert.True(File.Exists(fs.ActiveLoadTransactionJournalPath));

        operations.FailStagedActivationMove = false;
        operations.FailBackupRestoreMove = false;
        await fs.WriteFileAtomicAsync(laterWritePath, "{\"state\":\"after-recovery\"}");

        Assert.Equal("{\"state\":\"last-valid\"}", await fs.ReadFileAsync(markerPath));
        Assert.Equal("{\"state\":\"after-recovery\"}", await fs.ReadFileAsync(laterWritePath));
        Assert.False(File.Exists(fs.ActiveLoadTransactionJournalPath));
    }

    [Fact]
    public async Task SaveGameAsync_ExcludesLifecycleTriggers_AndLoadRemovesLegacyTriggerFiles()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 5
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        { "transitions": [{ "transitionId": "life_transition_1" }] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        { "triggeredAtTurn": 10 }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/ascension.json", """
        { "triggeredAtTurn": 11 }
        """);

        Assert.True(await _service.SaveGameAsync("no_lifecycle_triggers", "save/load lifecycle regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
        {
            Assert.Null(archive.GetEntry("game_state/control/life_transitions.json"));
            Assert.Null(archive.GetEntry("game_state/control/incarnation_trigger.json"));
            Assert.Null(archive.GetEntry("game_state/control/ascension.json"));
        }

        var legacyZip = Path.Combine(_rootPath, "legacy_with_triggers.zip");
        using (var archive = ZipFile.Open(legacyZip, ZipArchiveMode.Create))
        {
            var soulEntry = archive.CreateEntry("game_state/meta/soul_state.json");
            await using (var soulStream = soulEntry.Open())
            await using (var writer = new StreamWriter(soulStream))
            {
                await writer.WriteAsync("""
                {
                  "soulName": "Загруженная Душа",
                  "currentRealm": "Chaos Sea",
                  "currentIncarnation": 6
                }
                """);
            }

            var transitionsEntry = archive.CreateEntry("game_state/control/life_transitions.json");
            await using (var transitionStream = transitionsEntry.Open())
            await using (var writer = new StreamWriter(transitionStream))
            {
                await writer.WriteAsync("""{ "transitions": [{ "transitionId": "stale" }] }""");
            }

            var incarnationEntry = archive.CreateEntry("game_state/control/incarnation_trigger.json");
            await using (var incarnationStream = incarnationEntry.Open())
            await using (var writer = new StreamWriter(incarnationStream))
            {
                await writer.WriteAsync("""{ "triggeredAtTurn": 42 }""");
            }

            var ascensionEntry = archive.CreateEntry("game_state/control/ascension.json");
            await using (var ascensionStream = ascensionEntry.Open())
            await using (var writer = new StreamWriter(ascensionStream))
            {
                await writer.WriteAsync("""{ "triggeredAtTurn": 43 }""");
            }
        }

        Assert.True(await _service.LoadGameAsync(legacyZip));
        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));
        Assert.False(_fs.FileExists("game_state/control/incarnation_trigger.json"));
        Assert.False(_fs.FileExists("game_state/control/ascension.json"));
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
            // ignore temp cleanup failures
        }
    }

    private static async Task WriteArchiveEntryAsync(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    private sealed class FaultInjectingLoadTransactionOperations : ILoadTransactionOperations
    {
        public bool FailCommittedJournalWrites { get; set; }
        public bool FailStagedActivationMove { get; set; }
        public bool FailBackupRestoreMove { get; set; }

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);

        public void BeforeMoveDirectory(
            string sourcePath,
            string destinationPath)
        {
            if (FailStagedActivationMove &&
                sourcePath.Contains($"{Path.DirectorySeparatorChar}stage{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected staged-session activation failure.");
            }

            if (FailBackupRestoreMove &&
                sourcePath.Contains($"{Path.DirectorySeparatorChar}backup{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected backup restore failure.");
            }
        }

        public void BeforeWriteAllTextAtomic(string path, string content)
        {
            if (FailCommittedJournalWrites &&
                content.Contains("\"Committed\":true", StringComparison.Ordinal))
            {
                throw new IOException("Injected committed-journal write failure.");
            }
        }
    }
}
