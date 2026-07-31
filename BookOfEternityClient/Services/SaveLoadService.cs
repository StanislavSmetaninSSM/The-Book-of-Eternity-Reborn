using System.IO.Compression;
using System.Security.Cryptography;
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
    private const string GameStateDirectory = "game_state";
    private const string GameStateArchivePrefix = GameStateDirectory + "/";
    private const string SoulStateArchivePath =
        "game_state/meta/soul_state.json";
    private const string SaveManifestArchivePath = "save_manifest.json";
    private const int SaveManifestSchemaVersion = 1;
    private const string SaveManifestHashAlgorithm = "SHA-256";

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
        LocalUiSessionLockService.LockPath,
        "output/ink_feather_action_result.json",
        ExplorerLocalTurnRollbackArtifacts.Root
    };

    private static readonly string[] EphemeralPathPrefixes =
    {
        "game_state/control/pending_turn_snapshot/",
        LocalUiSessionLockService.LockPath + "/",
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
    private static readonly JsonSerializerOptions SaveManifestJsonOptions =
        new(JsonOpts)
        {
            PropertyNameCaseInsensitive = true
        };
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
        FileSystemManager.RuntimeStagedFile? stagedFile = null;
        try
        {
            _fs.EnsureCanonicalWriteLeaseActive(canonicalSnapshotLease);
            if (!_fs.DirectoryExists(
                    canonicalSnapshotLease,
                    GameStateDirectory))
            {
                throw new InvalidDataException(
                    "The mandatory canonical game_state root is missing.");
            }

            var state = _stateManager.CurrentState;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = SanitizeFileName($"{saveName}_{timestamp}.zip");
            var destinationRelativePath = Path.Combine(saveDir, fileName)
                .Replace('\\', '/');
            _ = _fs.ResolvePath(destinationRelativePath);
            stagingRoot = _fs.CreateRuntimeSaveStagingRoot();
            temporaryPath = Path.Combine(stagingRoot, "save.zip");
            stagedFile = await _fs.CreateRuntimeStagedFileAsync(temporaryPath);

            using (var archive = new ZipArchive(
                       stagedFile.Stream,
                       ZipArchiveMode.Create,
                       leaveOpen: true))
            {
                var manifestEntries =
                    new List<SaveIntegrityManifestEntry>();

                // Add game_state directory
                var gameStateEntryCount = await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath(GameStateDirectory),
                    GameStateDirectory,
                    manifestEntries);
                if (gameStateEntryCount == 0)
                {
                    throw new InvalidDataException(
                        "The mandatory canonical game_state root contains no durable state.");
                }
                ValidateArchivedSoulState(manifestEntries);

                // Add lore directory
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("lore"),
                    "lore",
                    manifestEntries);

                // Add player-authored source layers that affect rules/world setup
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("mods"),
                    "mods",
                    manifestEntries);
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("world_profiles"),
                    "world_profiles",
                    manifestEntries);

                // Add stories (persistent conversation history)
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("stories"),
                    "stories",
                    manifestEntries);

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
                            $"images/{dirName}",
                            manifestEntries);
                    }
                }

                // Add output
                await AddDirectoryToArchive(
                    canonicalSnapshotLease,
                    archive,
                    _fs.ResolvePath("output"),
                    "output",
                    manifestEntries);

                // Add config
                var configBytes = await _fs.ReadFileBytesAsync(
                    canonicalSnapshotLease,
                    "config.json");
                if (configBytes != null)
                {
                    await AddManifestedBytesToArchiveAsync(
                        archive,
                        "config.json",
                        configBytes,
                        manifestEntries);
                }

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
                await AddManifestedBytesToArchiveAsync(
                    archive,
                    "save_metadata.json",
                    Encoding.UTF8.GetBytes(metadataJson),
                    manifestEntries);

                var manifest = new SaveIntegrityManifest(
                    SaveManifestSchemaVersion,
                    SaveManifestHashAlgorithm,
                    manifestEntries
                        .OrderBy(
                            entry => entry.Path,
                            StringComparer.Ordinal)
                        .ToArray());
                await AddBytesToArchiveAsync(
                    archive,
                    SaveManifestArchivePath,
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(
                            manifest,
                            SaveManifestJsonOptions)));
            }
            if (_hooks?.BeforeSaveCommitAsync != null)
                await _hooks.BeforeSaveCommitAsync();
            await _fs.MoveRuntimeFileIntoCanonicalSessionAsync(
                canonicalSnapshotLease,
                stagedFile,
                destinationRelativePath);
            stagedFile = null;
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
            if (stagedFile != null)
                await stagedFile.DisposeAsync();
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

            var openedArchive = _fs.OpenExactPhysicalReadFile(
                fullPath,
                "Selected save archive");
            if (openedArchive == null)
            {
                _logger.LogWarning("Файл сохранения не найден: {Path}", fullPath);
                return false;
            }

            var transactionId = Guid.NewGuid().ToString("N");
            transactionPaths = _fs.GetLoadTransactionPaths(transactionId);
            _fs.CreateLoadDirectory(transactionPaths.StagingSessionPath);

            await using (openedArchive)
            {
                try
                {
                    using (var archive = new ZipArchive(
                               openedArchive.Stream,
                               ZipArchiveMode.Read,
                               leaveOpen: true))
                    {
                        await ValidateArchiveStructureAsync(
                            archive,
                            transactionPaths.StagingSessionPath);

                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                                continue;

                            if (!TryResolveArchiveEntryTargetPath(
                                    transactionPaths.StagingSessionPath,
                                    entry.FullName,
                                    out var targetPath))
                            {
                                _logger.LogWarning(
                                    "Загрузка отклонена: zip entry выходит за пределы sandbox: {Entry}",
                                    entry.FullName);
                                openedArchive.Abandon();
                                return false;
                            }
                            var normalizedPath = Path
                                .GetRelativePath(
                                    transactionPaths.StagingSessionPath,
                                    targetPath)
                                .Replace('\\', '/');
                            if (normalizedPath.Equals(
                                    SaveManifestArchivePath,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
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

                    openedArchive.Complete();
                }
                catch
                {
                    openedArchive.Abandon();
                    throw;
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

    private async Task<SaveMetadata?> ReadSaveMetadataWithRetryAsync(string saveFile)
    {
        FileSystemManager.StableReadFile? openedFile = null;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                openedFile = _fs.OpenExactPhysicalReadFile(
                    saveFile,
                    "Save metadata archive");
                break;
            }
            catch (Exception ex) when (
                IsTransientSaveMetadataOpenException(ex) &&
                attempt < SaveMetadataReadAttempts)
            {
                await Task.Delay(SaveMetadataReadRetryDelay);
            }
        }

        return openedFile == null
            ? null
            : await ReadSaveMetadataAsync(openedFile);
    }

    private static async Task<SaveMetadata?> ReadSaveMetadataAsync(
        FileSystemManager.StableReadFile openedFile)
    {
        await using (openedFile)
        {
            try
            {
                SaveMetadata? metadata = null;
                using (var archive = new ZipArchive(
                           openedFile.Stream,
                           ZipArchiveMode.Read,
                           leaveOpen: true))
                {
                    var metadataEntry = archive.GetEntry("save_metadata.json");
                    if (metadataEntry != null)
                    {
                        using var stream = metadataEntry.Open();
                        using var reader = new StreamReader(stream);
                        var json = await reader.ReadToEndAsync();
                        metadata = JsonSerializer.Deserialize<SaveMetadata>(
                            json,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                    }
                }

                openedFile.Complete();
                return metadata;
            }
            catch
            {
                openedFile.Abandon();
                throw;
            }
        }
    }

    private static bool IsTransientSaveMetadataOpenException(Exception ex) =>
        ex is IOException &&
        (ex.HResult & 0xFFFF) is 32 or 33;

    private async Task<int> AddDirectoryToArchive(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        ZipArchive archive,
        string sourceDir,
        string entryPrefix,
        List<SaveIntegrityManifestEntry> manifestEntries)
    {
        if (!Directory.Exists(sourceDir) || FileSystemManager.IsReparsePoint(sourceDir))
            return 0;

        var archivedFileCount = 0;
        foreach (var file in FileSystemManager.EnumerateFilesWithoutFollowingReparsePoints(sourceDir, "*"))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var entryPath = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
            if (IsEphemeralArchivePath(entryPath))
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
            if (entryPath.Equals(
                    SoulStateArchivePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateSoulStateBytes(content);
            }

            await AddManifestedBytesToArchiveAsync(
                archive,
                entryPath,
                content,
                manifestEntries);
            archivedFileCount++;
        }

        return archivedFileCount;
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

    private static async Task AddManifestedBytesToArchiveAsync(
        ZipArchive archive,
        string entryPath,
        byte[] content,
        List<SaveIntegrityManifestEntry> manifestEntries)
    {
        var normalizedPath = entryPath.Replace('\\', '/');
        if (manifestEntries.Any(entry =>
                entry.Path.Equals(
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                $"Save payload contains duplicate archive path '{normalizedPath}'.");
        }

        await AddBytesToArchiveAsync(
            archive,
            normalizedPath,
            content);
        manifestEntries.Add(
            new SaveIntegrityManifestEntry(
                normalizedPath,
                content.LongLength,
                Convert.ToHexString(SHA256.HashData(content))));
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

    private static async Task ValidateArchiveStructureAsync(
        ZipArchive archive,
        string stagingSessionRoot)
    {
        var payloadEntries =
            new Dictionary<string, ZipArchiveEntry>(
                StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var normalizedPath = NormalizeArchiveEntryPath(
                stagingSessionRoot,
                entry.FullName);
            if (!payloadEntries.TryAdd(normalizedPath, entry))
            {
                throw new InvalidDataException(
                    $"Save archive contains duplicate normalized path '{normalizedPath}'.");
            }
        }

        if (!payloadEntries.TryGetValue(
                SoulStateArchivePath,
                out var soulStateEntry))
        {
            throw new InvalidDataException(
                $"Save archive is missing mandatory canonical state '{SoulStateArchivePath}'.");
        }

        await ValidateSoulStateEntryAsync(soulStateEntry);

        if (!payloadEntries.TryGetValue(
                SaveManifestArchivePath,
                out var manifestEntry))
        {
            return;
        }

        SaveIntegrityManifest manifest;
        await using (var manifestStream = manifestEntry.Open())
        using (var manifestBuffer = new MemoryStream())
        {
            await manifestStream.CopyToAsync(manifestBuffer);
            manifest =
                StrictJsonAuthority.Deserialize<SaveIntegrityManifest>(
                    StripUtf8Bom(manifestBuffer.ToArray()),
                    SaveManifestJsonOptions,
                    "Save integrity manifest")
                ?? throw new InvalidDataException(
                    "Save integrity manifest is null.");
        }

        if (manifest.SchemaVersion != SaveManifestSchemaVersion ||
            !manifest.Algorithm.Equals(
                SaveManifestHashAlgorithm,
                StringComparison.OrdinalIgnoreCase) ||
            manifest.Entries == null)
        {
            throw new InvalidDataException(
                "Save integrity manifest has an unsupported schema or hash algorithm.");
        }

        var expectedEntries =
            new Dictionary<string, SaveIntegrityManifestEntry>(
                StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPayload in manifest.Entries)
        {
            var normalizedPath = NormalizeArchiveEntryPath(
                stagingSessionRoot,
                manifestPayload.Path);
            if (normalizedPath.Equals(
                    SaveManifestArchivePath,
                    StringComparison.OrdinalIgnoreCase) ||
                IsEphemeralArchivePath(normalizedPath) ||
                manifestPayload.Length < 0 ||
                !IsSha256(manifestPayload.Sha256) ||
                !expectedEntries.TryAdd(
                    normalizedPath,
                    manifestPayload with { Path = normalizedPath }))
            {
                throw new InvalidDataException(
                    $"Save integrity manifest contains invalid or duplicate entry '{manifestPayload.Path}'.");
            }
        }

        var durablePayloadEntries = payloadEntries
            .Where(pair =>
                !pair.Key.Equals(
                    SaveManifestArchivePath,
                    StringComparison.OrdinalIgnoreCase) &&
                !IsEphemeralArchivePath(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        if (expectedEntries.Count != durablePayloadEntries.Count)
        {
            throw new InvalidDataException(
                "Save integrity manifest does not cover every archive payload.");
        }

        foreach (var (path, expected) in expectedEntries)
        {
            if (!durablePayloadEntries.TryGetValue(path, out var actualEntry) ||
                actualEntry.Length != expected.Length)
            {
                throw new InvalidDataException(
                    $"Save payload '{path}' does not match its manifested length.");
            }

            await using var payloadStream = actualEntry.Open();
            var digest = Convert.ToHexString(
                await SHA256.HashDataAsync(payloadStream));
            if (!digest.Equals(
                    expected.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Save payload '{path}' does not match its manifested SHA-256 digest.");
            }
        }
    }

    private static string NormalizeArchiveEntryPath(
        string stagingSessionRoot,
        string archiveEntryPath)
    {
        if (!TryResolveArchiveEntryTargetPath(
                stagingSessionRoot,
                archiveEntryPath,
                out var targetPath))
        {
            throw new InvalidDataException(
                $"Save archive entry escapes the session sandbox: {archiveEntryPath}");
        }

        return Path
            .GetRelativePath(stagingSessionRoot, targetPath)
            .Replace('\\', '/');
    }

    private static async Task ValidateSoulStateEntryAsync(
        ZipArchiveEntry soulStateEntry)
    {
        await using var stream = soulStateEntry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        ValidateSoulStateBytes(buffer.ToArray());
    }

    private static void ValidateArchivedSoulState(
        IReadOnlyList<SaveIntegrityManifestEntry> manifestEntries)
    {
        if (!manifestEntries.Any(entry =>
                entry.Path.Equals(
                    SoulStateArchivePath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                $"The mandatory canonical state '{SoulStateArchivePath}' is missing.");
        }
    }

    private static void ValidateSoulStateBytes(byte[] content)
    {
        var root = StrictJsonAuthority.Deserialize<JsonElement>(
            StripUtf8Bom(content),
            SaveManifestJsonOptions,
            "Canonical soul state");
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Canonical state '{SoulStateArchivePath}' must be a JSON object.");
        }

        var hasRealm = root
            .EnumerateObject()
            .Any(property =>
                property.Name.Equals(
                    "currentRealm",
                    StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(
                    property.Value.GetString()));
        if (!hasRealm)
        {
            throw new InvalidDataException(
                $"Canonical state '{SoulStateArchivePath}' requires non-empty currentRealm.");
        }
    }

    private static ReadOnlyMemory<byte> StripUtf8Bom(byte[] content) =>
        content.Length >= 3 &&
        content[0] == 0xEF &&
        content[1] == 0xBB &&
        content[2] == 0xBF
            ? content.AsMemory(3)
            : content;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(static character =>
            character is >= '0' and <= '9' or
                >= 'a' and <= 'f' or
                >= 'A' and <= 'F');

    private static bool IsEphemeralArchivePath(string entryPath) =>
        EphemeralControlFiles.Contains(entryPath) ||
        EphemeralPathPrefixes.Any(prefix =>
            entryPath.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase));

    private void DeleteEphemeralArtifacts(string sessionRoot)
    {
        foreach (var relativePath in EphemeralControlFiles)
        {
            var fullPath = Path.Combine(sessionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                _fs.DeleteLoadTransactionFile(fullPath);
            else if (Directory.Exists(fullPath))
                _fs.DeleteLoadTransactionDirectory(fullPath);
        }

        foreach (var relativePrefix in EphemeralPathPrefixes)
        {
            var cleanupPath = Path.Combine(sessionRoot, relativePrefix.TrimEnd('/', '\\').Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(cleanupPath))
                _fs.DeleteLoadTransactionDirectory(cleanupPath);
        }
    }

    private sealed record SaveIntegrityManifest(
        int SchemaVersion,
        string Algorithm,
        IReadOnlyList<SaveIntegrityManifestEntry> Entries);

    private sealed record SaveIntegrityManifestEntry(
        string Path,
        long Length,
        string Sha256);

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
