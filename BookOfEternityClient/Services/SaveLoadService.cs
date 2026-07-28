using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

internal sealed class SaveLoadServiceHooks
{
    internal Func<Task>? BeforeLoadLeaseAcquisitionAsync { get; init; }
    internal Func<Task>? BeforeAutosaveCleanupLeaseAcquisitionAsync { get; init; }
    internal Func<Task>? BeforeAutosaveDeletionAsync { get; init; }
    internal Func<Task>? BeforeSaveCommitAsync { get; init; }
}

/// <summary>
/// Manages save/load with ZIP archives, autosaves, and metadata.
/// </summary>
public class SaveLoadService
{
    private static readonly HashSet<string> EphemeralControlFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "game_state/control/pending_turn_snapshot.json",
        "game_state/control/validation_repair_request.json",
        "game_state/control/validation_repair_ready.json",
        GmWorkers.GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath,
        RealmSegregationAutoRollbackService.ReportPath,
        "game_state/control/terminal_protocol_failure_request.json",
        "game_state/control/life_transitions.json",
        "game_state/control/incarnation_trigger.json",
        "game_state/control/ascension.json",
        ProgressionScheduleService.ReportPath,
        "game_state/control/gm_cli_window_binding.json",
        "game_state/control/gm_bridge_status.json",
        "output/ink_feather_action_result.json",
        ExplorerLocalTurnRollbackArtifacts.Root
    };

    private static readonly string[] EphemeralPathPrefixes =
    {
        "game_state/control/pending_turn_snapshot/",
        ExplorerLocalTurnRollbackArtifacts.Root + "/",
        QteSceneService.QteNormalizerBackupDirectory + "/",
        "worker_tasks/",
        "worker_proposals/"
    };

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ILogger<SaveLoadService> _logger;
    private readonly SaveLoadServiceHooks? _hooks;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private const int SaveMetadataReadAttempts = 10;
    private static readonly TimeSpan SaveMetadataReadRetryDelay = TimeSpan.FromMilliseconds(50);

    public SaveLoadService(FileSystemManager fs, StateManager stateManager, ILogger<SaveLoadService> logger)
        : this(fs, stateManager, logger, hooks: null)
    {
    }

    internal SaveLoadService(
        FileSystemManager fs,
        StateManager stateManager,
        ILogger<SaveLoadService> logger,
        SaveLoadServiceHooks? hooks)
    {
        _fs = fs;
        _stateManager = stateManager;
        _logger = logger;
        _hooks = hooks;
    }

    public async Task<bool> SaveGameAsync(string saveName, string description, string saveDir = "saves/manual_saves", int turnNumber = 0)
    {
        try
        {
            await using var canonicalSnapshotLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await SaveGameAsync(canonicalSnapshotLease, saveName, description, saveDir, turnNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения: {Name}", saveName);
            return false;
        }
    }

    internal async Task<bool> SaveGameAsync(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        string saveName,
        string description,
        string saveDir = "saves/manual_saves",
        int turnNumber = 0)
    {
        string? stagingRoot = null;
        string? temporaryPath = null;
        try
        {
            _fs.EnsureCanonicalWriteLeaseActive(canonicalSnapshotLease);
            var state = _stateManager.CurrentState;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = SanitizeFileName($"{saveName}_{timestamp}.zip");
            var destinationRelativePath = Path.Combine(saveDir, fileName)
                .Replace('\\', '/');
            _ = _fs.ResolvePath(destinationRelativePath);
            stagingRoot = _fs.CreateRuntimeSaveStagingRoot();
            temporaryPath = Path.Combine(stagingRoot, "save.zip");

            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                // Add game_state directory
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("game_state"),
                    "game_state");

                // Add lore directory
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("lore"),
                    "lore");

                // Add player-authored source layers that affect rules/world setup
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("mods"),
                    "mods");
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("world_profiles"),
                    "world_profiles");

                // Add stories (persistent conversation history)
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("stories"),
                    "stories");

                // Add entity images (NPCs, items, locations, player — NOT scenes)
                var imagesPath = _fs.ResolvePath("images");
                if (Directory.Exists(imagesPath))
                {
                    foreach (var subDir in Directory.GetDirectories(imagesPath))
                    {
                        var dirName = Path.GetFileName(subDir);
                        if (dirName == "scenes") continue; // Scene images are ephemeral, skip
                        await AddDirectoryToArchive(
                            canonicalSnapshotLease,
                            archive,
                            subDir,
                            $"images/{dirName}");
                    }
                }

                // Add output
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("output"),
                    "output");

                // Add config
                var configBytes = await _fs.ReadFileBytesAsync(
                    canonicalSnapshotLease,
                    "config.json");
                if (configBytes != null)
                    await AddBytesToArchiveAsync(archive, "config.json", configBytes);

                // Add metadata
                var metadata = new SaveMetadata
                {
                    SaveName = saveName,
                    Description = description,
                    Timestamp = DateTime.UtcNow,
                    GameVersion = _stateManager.Settings.GameVersion,
                    TurnNumber = turnNumber > 0 ? turnNumber : state.TurnNumber,
                    CurrentLocation = state.CurrentLocation,
                    WorldName = "",
                    Incarnation = state.Incarnation,
                    InkFeathers = state.InkFeathers,
                    CharacterName = state.CharacterName,
                };

                var metadataJson = JsonSerializer.Serialize(metadata, JsonOpts);
                var entry = archive.CreateEntry("save_metadata.json");
                using var stream = entry.Open();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(metadataJson));
            }
            if (_hooks?.BeforeSaveCommitAsync != null)
                await _hooks.BeforeSaveCommitAsync();
            await _fs.MoveRuntimeFileIntoCanonicalSessionAsync(
                canonicalSnapshotLease,
                temporaryPath,
                destinationRelativePath);
            temporaryPath = null;

            _logger.LogInformation("Игра сохранена: {Name}", saveName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения: {Name}", saveName);
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stagingRoot))
            {
                try
                {
                    _fs.DeleteRuntimeSaveStagingRoot(stagingRoot);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(
                        cleanupEx,
                        "Не удалось удалить staging-директорию сохранения: {Path}",
                        stagingRoot);
                }
            }
        }
    }

    public async Task<bool> AutosaveAsync(int turnNumber)
    {
        const string autosaveDirectory = "saves/autosaves";
        var saved = await SaveGameAsync(
            $"autosave_turn{turnNumber}",
            $"Автосохранение - ход {turnNumber}",
            autosaveDirectory,
            turnNumber);
        if (!saved)
            return false;

        if (_hooks?.BeforeAutosaveCleanupLeaseAcquisitionAsync != null)
            await _hooks.BeforeAutosaveCleanupLeaseAcquisitionAsync();
        await CleanupOldSaves(autosaveDirectory, _stateManager.Settings.MaxAutosaves);
        return true;
    }

    public async Task<bool> LoadGameAsync(string saveFilePath)
    {
        CanonicalLoadTransactionPaths? transactionPaths = null;
        try
        {
            var fullPath = saveFilePath;
            if (!Path.IsPathRooted(fullPath))
                fullPath = _fs.ResolvePath(saveFilePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Файл сохранения не найден: {Path}", fullPath);
                return false;
            }

            var transactionId = Guid.NewGuid().ToString("N");
            transactionPaths = _fs.GetLoadTransactionPaths(transactionId);
            _fs.CreateLoadDirectory(transactionPaths.StagingSessionPath);

            using (var archive = ZipFile.OpenRead(fullPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    if (!TryResolveArchiveEntryTargetPath(transactionPaths.StagingSessionPath, entry.FullName, out var targetPath))
                    {
                        _logger.LogWarning("Загрузка отклонена: zip entry выходит за пределы sandbox: {Entry}", entry.FullName);
                        return false;
                    }

                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (targetDir != null)
                        _fs.CreateLoadDirectory(targetDir);

                    await using var entryStream = entry.Open();
                    await _fs.WriteLoadTransactionFileAsync(
                        targetPath,
                        entryStream);
                }
            }

            DeleteEphemeralArtifacts(transactionPaths.StagingSessionPath);

            var liveSessionPath = _fs.GameSessionPath;

            if (_hooks?.BeforeLoadLeaseAcquisitionAsync != null)
                await _hooks.BeforeLoadLeaseAcquisitionAsync();
            await using var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
            var runtimeSnapshot = _stateManager.CaptureRuntimeSnapshot();
            await using (var writeLease =
                         await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease))
            {
                _fs.BeginLoadTransaction(writeLease, transactionId);
                try
                {
                    if (_fs.LoadDirectoryExists(liveSessionPath))
                    {
                        _fs.CreateLoadDirectory(Path.GetDirectoryName(transactionPaths.BackupSessionPath)!);
                        _fs.MoveLoadDirectory(liveSessionPath, transactionPaths.BackupSessionPath);
                    }

                    _fs.MoveLoadDirectory(transactionPaths.StagingSessionPath, liveSessionPath);
                    _fs.ActivateLoadTransactionSession(writeLease, transactionId);
                    _fs.EnsureDirectoryStructure(writeLease);
                    await _stateManager.RefreshGameStateAsync(writeLease);
                    await _stateManager.LoadSettingsAsync();
                    _fs.CommitLoadTransaction(writeLease, transactionId);
                }
                catch (Exception loadException)
                {
                    try
                    {
                        _fs.RecoverInterruptedLoadTransaction(writeLease);
                        _stateManager.RestoreRuntimeSnapshot(runtimeSnapshot);
                        await _stateManager.RefreshGameStateAsync(writeLease);
                        await _stateManager.LoadSettingsAsync();
                    }
                    catch (Exception recoveryException)
                    {
                        _stateManager.RestoreRuntimeSnapshot(runtimeSnapshot);
                        throw new AggregateException(
                            "Load failed and automatic rollback could not restore the last valid session. " +
                            "Recovery journal and backup were preserved for startup retry.",
                            loadException,
                            recoveryException);
                    }

                    throw;
                }
            }

            _logger.LogInformation("Игра загружена: {Path}", saveFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки: {Path}", saveFilePath);
            return false;
        }
        finally
        {
            if (transactionPaths != null)
            {
                try
                {
                    _fs.CleanupInactiveLoadTransaction(transactionPaths);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Не удалось очистить неактивную staging-директорию транзакции загрузки {TransactionId}.",
                        transactionPaths.TransactionId);
                }
            }
        }
    }

    public async Task<List<SaveInfo>> GetAvailableSavesAsync(string saveDir = "saves/manual_saves")
    {
        var saves = new List<SaveInfo>();
        var fullDir = _fs.ResolvePath(saveDir);

        if (!Directory.Exists(fullDir))
            return saves;

        foreach (var saveFile in Directory.GetFiles(fullDir, "*.zip"))
        {
            try
            {
                var metadata = await ReadSaveMetadataWithRetryAsync(saveFile);
                if (metadata == null)
                    continue;

                saves.Add(new SaveInfo
                {
                    FileName = saveFile,
                    Metadata = metadata,
                    FileSize = new FileInfo(saveFile).Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Повреждённое сохранение: {File}", Path.GetFileName(saveFile));
            }
        }

        return saves.OrderByDescending(s => s.Metadata?.Timestamp).ToList();
    }

    private static async Task<SaveMetadata?> ReadSaveMetadataWithRetryAsync(string saveFile)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReadSaveMetadataAsync(saveFile);
            }
            catch (Exception ex) when (IsTransientSaveMetadataReadException(ex) && attempt < SaveMetadataReadAttempts)
            {
                await Task.Delay(SaveMetadataReadRetryDelay);
            }
        }
    }

    private static async Task<SaveMetadata?> ReadSaveMetadataAsync(string saveFile)
    {
        using var archive = ZipFile.OpenRead(saveFile);
        var metadataEntry = archive.GetEntry("save_metadata.json");
        if (metadataEntry == null)
            return null;

        using var stream = metadataEntry.Open();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<SaveMetadata>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static bool IsTransientSaveMetadataReadException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    private async Task AddDirectoryToArchive(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        ZipArchive archive,
        string sourceDir,
        string entryPrefix)
    {
        if (!Directory.Exists(sourceDir) || FileSystemManager.IsReparsePoint(sourceDir))
            return;

        foreach (var file in FileSystemManager.EnumerateFilesWithoutFollowingReparsePoints(sourceDir, "*"))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var entryPath = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
            if (EphemeralControlFiles.Contains(entryPath) ||
                EphemeralPathPrefixes.Any(prefix => entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            var canonicalRelativePath = Path.GetRelativePath(_fs.GameSessionPath, file)
                .Replace('\\', '/');
            var content = await _fs.ReadFileBytesAsync(
                canonicalSnapshotLease,
                canonicalRelativePath);
            if (content == null)
            {
                throw new FileNotFoundException(
                    "Canonical save-snapshot file disappeared before verified read.",
                    file);
            }

            await AddBytesToArchiveAsync(archive, entryPath, content);
        }
    }

    private static async Task AddBytesToArchiveAsync(
        ZipArchive archive,
        string entryPath,
        byte[] content)
    {
        var entry = archive.CreateEntry(entryPath);
        await using var stream = entry.Open();
        await stream.WriteAsync(content);
    }

    private static bool TryResolveArchiveEntryTargetPath(string sessionRoot, string archiveEntryPath, out string targetPath)
    {
        targetPath = string.Empty;
        if (string.IsNullOrWhiteSpace(archiveEntryPath))
            return false;

        var normalizedRelativePath = archiveEntryPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelativePath))
            return false;

        var rootFullPath = Path.GetFullPath(sessionRoot);
        var candidateFullPath = Path.GetFullPath(Path.Combine(rootFullPath, normalizedRelativePath));
        var rootPrefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        if (!candidateFullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        targetPath = candidateFullPath;
        return true;
    }

    private static void DeleteEphemeralArtifacts(string sessionRoot)
    {
        foreach (var relativePath in EphemeralControlFiles)
        {
            var fullPath = Path.Combine(sessionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        foreach (var relativePrefix in EphemeralPathPrefixes)
        {
            var cleanupPath = Path.Combine(sessionRoot, relativePrefix.TrimEnd('/', '\\').Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(cleanupPath))
                Directory.Delete(cleanupPath, recursive: true);
        }
    }

    private async Task CleanupOldSaves(string saveDir, int maxSaves)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var fullDir = _fs.ResolvePath(saveDir);
        if (!Directory.Exists(fullDir))
            return;

        var files = Directory.GetFiles(fullDir, "*.zip")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Skip(Math.Max(maxSaves, 0))
            .Select(file => Path.GetRelativePath(_fs.GameSessionPath, file)
                .Replace('\\', '/'))
            .ToArray();

        if (_hooks?.BeforeAutosaveDeletionAsync != null)
            await _hooks.BeforeAutosaveDeletionAsync();

        foreach (var file in files)
        {
            try { _fs.DeleteFile(writeLease, file); }
            catch { /* ignore cleanup errors */ }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
