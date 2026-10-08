using System.Buffers.Binary;
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
    internal Func<string, Task>? AfterLoadArchiveExtractedAsync { get; init; }
    internal Func<string, Task>? BeforeLoadPreparationCleanupAsync { get; init; }
    internal Func<Task>? AfterLoadPublicationValidatedAsync { get; init; }
    internal Func<Task>? BeforeAutosaveCleanupLeaseAcquisitionAsync { get; init; }
    internal Func<Task>? BeforeAutosaveDeletionAsync { get; init; }
    /// <summary>
    /// Observes the held retention lease before deletion; null leaves production behavior unchanged.
    /// </summary>
    internal Func<FileSystemManager.CanonicalWriteLease, Task>? BeforeAutosaveRetentionAsync { get; init; }
    internal Func<Task>? BeforeSaveCommitAsync { get; init; }
}

/// <summary>
/// Manages save/load with ZIP archives, autosaves, and metadata.
/// </summary>
public partial class SaveLoadService
{
    internal sealed record SaveArchiveBudget(
        int MaxEntryCount,
        long MaxTotalEntryNameUtf8Bytes,
        long MaxManifestExpandedBytes,
        long MaxSoulStateExpandedBytes,
        long MaxEntryExpandedBytes,
        long MaxTotalExpandedBytes,
        long CompressionRatioGraceExpandedBytes,
        long MaxCompressionRatio);

    internal sealed record SaveArchiveEntryDescriptor(
        string Path,
        bool IsDirectory,
        long Length,
        long CompressedLength);

    internal static SaveArchiveBudget TrustedArchiveBudget { get; } =
        new(
            MaxEntryCount: 8_192,
            MaxTotalEntryNameUtf8Bytes: 2L * 1024 * 1024,
            MaxManifestExpandedBytes: 4L * 1024 * 1024,
            MaxSoulStateExpandedBytes: 8L * 1024 * 1024,
            MaxEntryExpandedBytes: 64L * 1024 * 1024,
            MaxTotalExpandedBytes: 512L * 1024 * 1024,
            CompressionRatioGraceExpandedBytes: 1L * 1024 * 1024,
            MaxCompressionRatio: 200);

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
        ResourceMaterializationContract.CommandPath,
        "output/ink_feather_action_result.json",
        ExplorerLocalTurnRollbackArtifacts.Root,
        ConsoleLocalTurnRollbackArtifacts.Root
    };

    private static readonly string[] EphemeralPathPrefixes =
    {
        "game_state/control/pending_turn_snapshot/",
        LocalUiSessionLockService.LockPath + "/",
        ExplorerLocalTurnRollbackArtifacts.Root + "/",
        ConsoleLocalTurnRollbackArtifacts.Root + "/",
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
    private static readonly JsonSerializerOptions SaveMetadataJsonOptions =
        new()
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

    /// <summary>
    /// Prepares one complete closed archive under the caller's snapshot lease without publishing it.
    /// </summary>
    /// <param name="canonicalSnapshotLease">
    /// The active canonical lease retained throughout preparation and the later publication decision.
    /// </param>
    /// <param name="saveName">
    /// The player-visible name used in metadata and the sanitized destination filename.
    /// </param>
    /// <param name="description">
    /// The player-visible description stored unchanged in metadata.
    /// </param>
    /// <param name="saveDir">
    /// The canonical relative destination directory, defaulting to manual saves.
    /// </param>
    /// <param name="turnNumber">
    /// A positive explicit turn number, or zero to use the current aggregated state.
    /// </param>
    /// <returns>
    /// An owned closed candidate whose caller must dispose after publication or abandonment.
    /// </returns>
    internal async Task<PreparedSaveArchive> PrepareSaveArchiveAsync(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        string saveName,
        string description,
        string saveDir = "saves/manual_saves",
        int turnNumber = 0)
    {
        string? stagingRoot = null;
        string? temporaryPath = null;
        FileSystemManager.RuntimeStagedFile? stagedFile = null;
        Exception? preparationFailure = null;
        try
        {
            _fs.ResolveBackupPublicationRecovery(canonicalSnapshotLease);
            var generation = _fs.ReadLocalGenerationSnapshot(canonicalSnapshotLease).Binding;
            if (!_fs.DirectoryExists(
                    canonicalSnapshotLease,
                    GameStateDirectory))
            {
                throw new InvalidDataException(
                    "The mandatory canonical game_state root is missing.");
            }

            var canonicalOwnerAuthorityJson =
                await ValidateCanonicalResourceStateForSaveAsync(
                canonicalSnapshotLease);

            var state = _stateManager.CurrentState;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = SanitizeFileName($"{saveName}_{timestamp}.zip");
            var destinationRelativePath = Path.Combine(saveDir, fileName)
                .Replace('\\', '/');
            var destination = _fs.ResolvePath(destinationRelativePath);
            if (!_fs.UsesTrustedLocalWriter(canonicalSnapshotLease, destinationRelativePath))
                throw new InvalidOperationException("Ordinary save preparation cannot run inside an original physical publication recorder.");
            var destinationScope = new TrustedLocalFileScope([_fs.GameSessionPath]);
            if (File.Exists(destinationScope.ValidateFile(destination)))
                throw new IOException("The save destination already exists; create-only save cannot replace it.");
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
                if (!manifestEntries.Any(entry => entry.Path.Equals(
                        CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    await AddManifestedBytesToArchiveAsync(
                        archive,
                        CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                        Encoding.UTF8.GetBytes(canonicalOwnerAuthorityJson),
                        manifestEntries);
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
            await stagedFile.Stream.FlushAsync();
            stagedFile.Stream.Flush(flushToDisk: true);
            await stagedFile.DisposeAsync();
            stagedFile = null;
            if (_hooks?.BeforeSaveCommitAsync != null)
                await _hooks.BeforeSaveCommitAsync();
            var candidate = new PreparedSaveArchive(_fs, canonicalSnapshotLease, destinationRelativePath, stagingRoot,
                TrustedLocalFileImage.CaptureFile(new TrustedLocalFileScope([stagingRoot]), temporaryPath), generation);
            stagingRoot = null;
            temporaryPath = null;
            return candidate;
        }
        catch (Exception ex)
        {
            preparationFailure = ex;
            throw;
        }
        finally
        {
            await ReleaseFailedSavePreparationAsync(stagedFile, stagingRoot, preparationFailure);
        }
    }

    /// <summary>
    /// Loads through the portable typed operation and reports only whether replacement committed.
    /// Player-facing callers must use <see cref="LoadGameWithOutcomeAsync"/> instead.
    /// </summary>
    /// <param name="saveFilePath">
    /// The selected archive's absolute path or canonical session-relative path.
    /// </param>
    /// <returns>
    /// True only for a confirmed commit, including a commit whose follow-up blocks continuation.
    /// False combines admission refusal, confirmed rollback and unresolved uncertainty; it is not
    /// permission to retry. This compatibility result does not establish safe continuation.
    /// </returns>
    public async Task<bool> LoadGameAsync(string saveFilePath) =>
        (await LoadGameWithOutcomeAsync(saveFilePath)).Disposition == LoadReplacementDisposition.Committed;

    /// <summary>
    /// Lists readable save metadata while holding one ordinary canonical lease.
    /// </summary>
    /// <param name="saveDir">
    /// The session-relative save directory; defaults to manual saves.
    /// </param>
    /// <returns>
    /// Valid bounded archives ordered by descending metadata timestamp.
    /// </returns>
    public async Task<List<SaveInfo>> GetAvailableSavesAsync(string saveDir = "saves/manual_saves")
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await GetAvailableSavesAsync(lease, saveDir);
    }

    /// <summary>
    /// Reads bounded metadata on the caller's existing lease without loading complete ZIP images into memory.
    /// </summary>
    /// <param name="lease">
    /// The active lease held across listing and stream completion.
    /// </param>
    /// <param name="saveDir">
    /// The session-relative directory containing save archives.
    /// </param>
    /// <returns>
    /// Successfully validated metadata and opened file lengths, ordered by descending timestamp.
    /// </returns>
    internal async Task<List<SaveInfo>> GetAvailableSavesAsync(FileSystemManager.CanonicalWriteLease lease,
        string saveDir = "saves/manual_saves")
    {
        _fs.ResolveBackupPublicationRecovery(lease);
        var saves = new List<SaveInfo>();
        var fullDir = _fs.ResolvePath(saveDir);
        var ordinary = _fs.UsesTrustedLocalWriter(lease, saveDir);
        if (ordinary) new TrustedLocalFileScope([_fs.GameSessionPath]).ValidateDirectory(fullDir);

        if (!Directory.Exists(fullDir))
            return saves;

        foreach (var saveFile in Directory.GetFiles(fullDir, "*.zip"))
        {
            try
            {
                var relativePath = Path.GetRelativePath(_fs.GameSessionPath, saveFile).Replace('\\', '/');
                var info = ordinary
                    ? await ReadOrdinarySaveMetadataWithRetryAsync(lease, relativePath)
                    : await ReadOriginalSaveMetadataWithRetryAsync(saveFile);
                if (info?.Metadata == null)
                    continue;

                saves.Add(info);
            }
            catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException && ex is not SessionReplacedException)
            {
                _logger.LogWarning(ex, "Повреждённое сохранение: {File}", Path.GetFileName(saveFile));
            }
        }

        _fs.ResolveBackupPublicationRecovery(lease);
        return saves.OrderByDescending(s => s.Metadata?.Timestamp).ToList();
    }

    /// <summary>
    /// Retries only transient open failures and completes a validated ordinary archive stream.
    /// </summary>
    /// <param name="lease">
    /// The lease retained by the enclosing listing operation.
    /// </param>
    /// <param name="relativePath">
    /// The session-relative ZIP path.
    /// </param>
    /// <returns>
    /// Its bounded metadata and opened length, or null when the validated file is absent.
    /// </returns>
    private async Task<SaveInfo?> ReadOrdinarySaveMetadataWithRetryAsync(FileSystemManager.CanonicalWriteLease lease, string relativePath)
    {
        FileSystemManager.OrdinaryReadFile? openedFile;
        for (var attempt = 1; ; attempt++)
        {
            try { openedFile = await _fs.OpenOrdinaryReadFileAsync(lease, relativePath); break; }
            catch (Exception ex) when (IsTransientSaveMetadataOpenException(ex) && attempt < SaveMetadataReadAttempts)
            { await Task.Delay(SaveMetadataReadRetryDelay); }
        }
        if (openedFile == null) return null;
        await using (openedFile)
        {
            try
            {
                var metadata = await ReadSaveMetadataStreamAsync(openedFile.Stream);
                openedFile.Complete();
                return new SaveInfo { FileName = _fs.ResolvePath(relativePath), Metadata = metadata, FileSize = openedFile.Length };
            }
            catch { openedFile.Abandon(); throw; }
        }
    }

    /// <summary>
    /// Retains original physical read authority for explicit original transaction callers.
    /// </summary>
    /// <param name="saveFile">
    /// The original route's exact archive path.
    /// </param>
    /// <returns>
    /// Its bounded metadata and opened length, or null for absence.
    /// </returns>
    private async Task<SaveInfo?> ReadOriginalSaveMetadataWithRetryAsync(string saveFile)
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

        if (openedFile == null) return null;
        await using (openedFile)
        {
            try
            {
                var metadata = await ReadSaveMetadataStreamAsync(openedFile.Stream);
                var length = openedFile.Stream.Length;
                openedFile.Complete();
                return new SaveInfo { FileName = saveFile, Metadata = metadata, FileSize = length };
            }
            catch
            {
                openedFile.Abandon();
                throw;
            }
        }
    }

    /// <summary>
    /// Validates raw ZIP structure and budgets before reading the bounded metadata entry.
    /// </summary>
    /// <param name="stream">
    /// The readable seekable archive stream, retained by its owning read wrapper.
    /// </param>
    /// <returns>
    /// The decoded metadata, or null when the archive has no metadata entry.
    /// </returns>
    private static async Task<SaveMetadata?> ReadSaveMetadataStreamAsync(Stream stream)
    {
        ValidateTrustedArchiveBeforeMaterialization(stream);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        ValidateTrustedArchiveBudget(archive);
        var entry = archive.GetEntry("save_metadata.json");
        if (entry == null) return null;
        var content = await ReadArchiveEntryBytesAsync(entry, TrustedArchiveBudget.MaxEntryExpandedBytes, "Save metadata");
        return JsonSerializer.Deserialize<SaveMetadata>(StripUtf8Bom(content).Span, SaveMetadataJsonOptions);
    }

    private static bool IsTransientSaveMetadataOpenException(Exception ex) =>
        ex is IOException &&
        (ex.HResult & 0xFFFF) is 11 or 32 or 33;

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
            if (IsEphemeralArchivePath(entryPath) ||
                entryPath.Equals(
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                    StringComparison.OrdinalIgnoreCase))
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
        // Arbitrary Linux payloads retain native identity, while fixed whole-file
        // authorities must remain unambiguous under the loader's declared mapping.
        var nameComparison = OperatingSystem.IsLinux() && !FixedLoadStatePaths.ContainsKey(normalizedPath)
            ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (manifestEntries.Any(entry =>
                entry.Path.Equals(
                    normalizedPath,
                    nameComparison)))
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

    internal static void ValidateTrustedArchiveBudget(
        IReadOnlyCollection<SaveArchiveEntryDescriptor> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var budget = TrustedArchiveBudget;
        if (entries.Count > budget.MaxEntryCount)
        {
            throw new InvalidDataException(
                $"Save archive contains more than {budget.MaxEntryCount} entries.");
        }

        long totalNameBytes = 0;
        long totalExpandedBytes = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Path) ||
                entry.Path.Contains(':'))
            {
                throw new InvalidDataException(
                    "Save archive contains an empty path or alternate-data-stream syntax.");
            }

            totalNameBytes += Encoding.UTF8.GetByteCount(entry.Path);
            if (totalNameBytes >
                budget.MaxTotalEntryNameUtf8Bytes)
            {
                throw new InvalidDataException(
                    "Save archive entry names exceed the trusted UTF-8 budget.");
            }

            if (entry.Length < 0 || entry.CompressedLength < 0)
            {
                throw new InvalidDataException(
                    $"Save archive entry '{entry.Path}' has a negative length.");
            }

            if (entry.IsDirectory)
            {
                if (entry.Length != 0)
                {
                    throw new InvalidDataException(
                        $"Save archive directory '{entry.Path}' contains payload bytes.");
                }

                continue;
            }

            var normalizedPath = entry.Path.Replace('\\', '/');
            var expandedLimit = normalizedPath.Equals(
                    SaveManifestArchivePath,
                    StringComparison.OrdinalIgnoreCase)
                ? budget.MaxManifestExpandedBytes
                : normalizedPath.Equals(
                    SoulStateArchivePath,
                    StringComparison.OrdinalIgnoreCase)
                    ? budget.MaxSoulStateExpandedBytes
                    : budget.MaxEntryExpandedBytes;
            if (entry.Length > expandedLimit)
            {
                throw new InvalidDataException(
                    $"Save archive entry '{entry.Path}' exceeds its trusted expanded-size budget.");
            }

            if (!normalizedPath.Equals(
                    SaveManifestArchivePath,
                    StringComparison.OrdinalIgnoreCase) &&
                !IsEphemeralArchivePath(normalizedPath))
            {
                totalExpandedBytes += entry.Length;
                if (totalExpandedBytes >
                    budget.MaxTotalExpandedBytes)
                {
                    throw new InvalidDataException(
                        "Save archive exceeds the trusted aggregate durable expanded-size budget.");
                }
            }

            if (entry.Length >
                budget.CompressionRatioGraceExpandedBytes)
            {
                var minimumCompressedLength =
                    entry.Length / budget.MaxCompressionRatio +
                    (entry.Length % budget.MaxCompressionRatio == 0
                        ? 0
                        : 1);
                if (entry.CompressedLength < minimumCompressedLength)
                {
                    throw new InvalidDataException(
                        $"Save archive entry '{entry.Path}' exceeds the trusted compression ratio.");
                }
            }
        }
    }

    internal static void ValidateTrustedArchiveBeforeMaterialization(
        Stream archiveStream)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        if (!archiveStream.CanRead ||
            !archiveStream.CanSeek)
        {
            throw new InvalidDataException(
                "Save archive preflight requires a seekable readable stream.");
        }

        const uint endOfCentralDirectorySignature =
            0x06054b50;
        const uint centralDirectoryHeaderSignature =
            0x02014b50;
        const int endOfCentralDirectoryLength = 22;
        const int maximumZipCommentLength = ushort.MaxValue;
        const int centralDirectoryHeaderLength = 46;
        const ushort utf8NameFlag = 0x0800;
        var originalPosition = archiveStream.Position;
        try
        {
            var archiveLength = archiveStream.Length;
            if (archiveLength <
                endOfCentralDirectoryLength)
            {
                throw new InvalidDataException(
                    "Save archive is missing its end-of-central-directory record.");
            }

            var tailLength = checked((int)Math.Min(
                archiveLength,
                endOfCentralDirectoryLength +
                maximumZipCommentLength));
            var tail = new byte[tailLength];
            archiveStream.Position =
                archiveLength - tailLength;
            archiveStream.ReadExactly(tail);

            var endRecordOffset = -1;
            for (var offset =
                     tailLength -
                     endOfCentralDirectoryLength;
                 offset >= 0;
                 offset--)
            {
                var candidate = tail.AsSpan(offset);
                if (BinaryPrimitives
                        .ReadUInt32LittleEndian(candidate) !=
                    endOfCentralDirectorySignature)
                {
                    continue;
                }

                var commentLength = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        candidate.Slice(20, 2));
                if (offset +
                    endOfCentralDirectoryLength +
                    commentLength ==
                    tailLength)
                {
                    endRecordOffset = offset;
                    break;
                }
            }

            if (endRecordOffset < 0)
            {
                throw new InvalidDataException(
                    "Save archive has no bounded end-of-central-directory record.");
            }

            var endRecord =
                tail.AsSpan(
                    endRecordOffset,
                    endOfCentralDirectoryLength);
            var diskNumber = BinaryPrimitives
                .ReadUInt16LittleEndian(
                    endRecord.Slice(4, 2));
            var centralDirectoryDisk = BinaryPrimitives
                .ReadUInt16LittleEndian(
                    endRecord.Slice(6, 2));
            var diskEntryCount = BinaryPrimitives
                .ReadUInt16LittleEndian(
                    endRecord.Slice(8, 2));
            var totalEntryCount = BinaryPrimitives
                .ReadUInt16LittleEndian(
                    endRecord.Slice(10, 2));
            var centralDirectorySize = BinaryPrimitives
                .ReadUInt32LittleEndian(
                    endRecord.Slice(12, 4));
            var centralDirectoryOffset = BinaryPrimitives
                .ReadUInt32LittleEndian(
                    endRecord.Slice(16, 4));
            if (diskNumber != 0 ||
                centralDirectoryDisk != 0 ||
                diskEntryCount != totalEntryCount)
            {
                throw new InvalidDataException(
                    "Multi-disk save archives are unsupported.");
            }
            if (totalEntryCount == ushort.MaxValue ||
                centralDirectorySize == uint.MaxValue ||
                centralDirectoryOffset == uint.MaxValue)
            {
                throw new InvalidDataException(
                    "ZIP64 save archives exceed the trusted client-owned envelope.");
            }
            if (totalEntryCount >
                TrustedArchiveBudget.MaxEntryCount)
            {
                throw new InvalidDataException(
                    $"Save archive contains more than {TrustedArchiveBudget.MaxEntryCount} entries.");
            }

            var absoluteEndRecordOffset =
                checked(
                    archiveLength -
                    tailLength +
                    endRecordOffset);
            var centralDirectoryEnd = checked(
                (long)centralDirectoryOffset +
                centralDirectorySize);
            if (centralDirectoryEnd >
                absoluteEndRecordOffset)
            {
                throw new InvalidDataException(
                    "Save archive central-directory bounds are invalid.");
            }

            archiveStream.Position =
                centralDirectoryOffset;
            var descriptors =
                new List<SaveArchiveEntryDescriptor>(
                    totalEntryCount);
            long consumedCentralDirectoryBytes = 0;
            long totalNameUtf8Bytes = 0;
            var header =
                new byte[centralDirectoryHeaderLength];
            var strictUtf8 =
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true);
            for (var index = 0;
                 index < totalEntryCount;
                 index++)
            {
                if (consumedCentralDirectoryBytes +
                    centralDirectoryHeaderLength >
                    centralDirectorySize)
                {
                    throw new InvalidDataException(
                        "Save archive central directory ended before its declared entry count.");
                }

                archiveStream.ReadExactly(header);
                var headerSpan = header.AsSpan();
                if (BinaryPrimitives
                        .ReadUInt32LittleEndian(headerSpan) !=
                    centralDirectoryHeaderSignature)
                {
                    throw new InvalidDataException(
                        "Save archive central directory contains an invalid entry header.");
                }

                var flags = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        headerSpan.Slice(8, 2));
                var compressedLength = BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        headerSpan.Slice(20, 4));
                var expandedLength = BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        headerSpan.Slice(24, 4));
                var nameLength = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        headerSpan.Slice(28, 2));
                var extraLength = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        headerSpan.Slice(30, 2));
                var commentLength = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        headerSpan.Slice(32, 2));
                var entryDisk = BinaryPrimitives
                    .ReadUInt16LittleEndian(
                        headerSpan.Slice(34, 2));
                var localHeaderOffset = BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        headerSpan.Slice(42, 4));
                if (entryDisk != 0 ||
                    compressedLength == uint.MaxValue ||
                    expandedLength == uint.MaxValue ||
                    localHeaderOffset == uint.MaxValue)
                {
                    throw new InvalidDataException(
                        "ZIP64 or multi-disk save entries exceed the trusted client-owned envelope.");
                }

                var recordLength = checked(
                    (long)centralDirectoryHeaderLength +
                    nameLength +
                    extraLength +
                    commentLength);
                if (consumedCentralDirectoryBytes +
                    recordLength >
                    centralDirectorySize)
                {
                    throw new InvalidDataException(
                        "Save archive central-directory entry exceeds its declared bounds.");
                }

                var nameBytes = new byte[nameLength];
                archiveStream.ReadExactly(nameBytes);
                string path;
                try
                {
                    path = (flags & utf8NameFlag) != 0
                        ? strictUtf8.GetString(nameBytes)
                        : Encoding.UTF8.GetString(nameBytes);
                }
                catch (DecoderFallbackException ex)
                {
                    throw new InvalidDataException(
                        "Save archive entry name is not valid UTF-8.",
                        ex);
                }

                totalNameUtf8Bytes = checked(
                    totalNameUtf8Bytes +
                    Encoding.UTF8.GetByteCount(path));
                if (totalNameUtf8Bytes >
                    TrustedArchiveBudget
                        .MaxTotalEntryNameUtf8Bytes)
                {
                    throw new InvalidDataException(
                        "Save archive entry names exceed the trusted UTF-8 budget.");
                }

                archiveStream.Seek(
                    checked((long)extraLength +
                            commentLength),
                    SeekOrigin.Current);
                consumedCentralDirectoryBytes +=
                    recordLength;
                descriptors.Add(
                    new SaveArchiveEntryDescriptor(
                        path,
                        path.EndsWith('/') ||
                        path.EndsWith('\\'),
                        expandedLength,
                        compressedLength));
            }

            ValidateTrustedArchiveBudget(
                descriptors);
        }
        finally
        {
            archiveStream.Position =
                originalPosition;
        }
    }

    private static bool TryResolveArchiveEntryTargetPath(string sessionRoot, string archiveEntryPath, out string targetPath)
    {
        targetPath = string.Empty;
        if (string.IsNullOrWhiteSpace(archiveEntryPath) ||
            archiveEntryPath.Contains(':'))
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

    /// <summary>
    /// Validates original archive schema, inventory and hashes before portable fixed-path materialization.
    /// </summary>
    /// <param name="archive">
    /// The admitted original ZIP, whose entry keys and payload bytes remain unchanged during validation.
    /// </param>
    /// <param name="stagingSessionRoot">
    /// The owned future extraction root used only for normalized path admission.
    /// </param>
    /// <param name="preserveNativePayloadNames">
    /// Enables distinct original Linux payload names for typed load while retaining the original public reader contract.
    /// </param>
    /// <returns>
    /// Completion only after each original durable payload has a unique validated manifest claim when a manifest exists.
    /// </returns>
    private static async Task ValidateArchiveStructureAsync(
        ZipArchive archive,
        string stagingSessionRoot,
        bool preserveNativePayloadNames = false)
    {
        ValidateTrustedArchiveBudget(archive);

        var originalNameComparer = preserveNativePayloadNames && OperatingSystem.IsLinux()
            ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var payloadEntries =
            new Dictionary<string, ZipArchiveEntry>(originalNameComparer);
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

        if (payloadEntries.Keys.Count(path => path.Equals(SaveManifestArchivePath, StringComparison.OrdinalIgnoreCase)) > 1)
            throw new InvalidDataException("Save archive contains duplicate integrity manifests.");

        var soulStateEntry = FindOriginalArchiveEntry(payloadEntries, SoulStateArchivePath);
        if (soulStateEntry == null)
        {
            throw new InvalidDataException(
                $"Save archive is missing mandatory canonical state '{SoulStateArchivePath}'.");
        }

        await ValidateSoulStateEntryAsync(soulStateEntry);
        await ValidateArchivedResourceStateAsync(payloadEntries);

        var manifestEntry = FindOriginalArchiveEntry(payloadEntries, SaveManifestArchivePath);
        if (manifestEntry == null)
        {
            return;
        }

        var manifestBytes = await ReadArchiveEntryBytesAsync(
            manifestEntry,
            TrustedArchiveBudget.MaxManifestExpandedBytes,
            "Save integrity manifest");
        var manifest =
            StrictJsonAuthority.Deserialize<SaveIntegrityManifest>(
                StripUtf8Bom(manifestBytes),
                SaveManifestJsonOptions,
                "Save integrity manifest")
            ?? throw new InvalidDataException(
                "Save integrity manifest is null.");

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
            new Dictionary<string, SaveIntegrityManifestEntry>(originalNameComparer);
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
                originalNameComparer);
        if (expectedEntries.Count != durablePayloadEntries.Count)
        {
            throw new InvalidDataException(
                "Save integrity manifest does not cover every archive payload.");
        }

        var claimedPayloads = new HashSet<ZipArchiveEntry>(ReferenceEqualityComparer.Instance);
        foreach (var (path, expected) in expectedEntries)
        {
            var actualEntry = FindOriginalArchiveEntry(durablePayloadEntries, path);
            if (actualEntry == null || actualEntry.Length != expected.Length)
            {
                throw new InvalidDataException(
                    $"Save payload '{path}' does not match its manifested length.");
            }

            if (!claimedPayloads.Add(actualEntry))
                throw new InvalidDataException("Save integrity manifest claims one original payload more than once.");

            var digest = await ComputeArchiveEntrySha256Async(
                actualEntry,
                expected.Length);
            if (!digest.Equals(
                    expected.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Save payload '{path}' does not match its manifested SHA-256 digest.");
            }
        }
    }

    private static void ValidateTrustedArchiveBudget(
        ZipArchive archive)
    {
        if (archive.Entries.Count >
            TrustedArchiveBudget.MaxEntryCount)
        {
            throw new InvalidDataException(
                $"Save archive contains more than {TrustedArchiveBudget.MaxEntryCount} entries.");
        }

        ValidateTrustedArchiveBudget(
            archive.Entries
                .Select(entry =>
                    new SaveArchiveEntryDescriptor(
                        entry.FullName,
                        string.IsNullOrEmpty(entry.Name),
                        entry.Length,
                        entry.CompressedLength))
                .ToArray());
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
        var content = await ReadArchiveEntryBytesAsync(
            soulStateEntry,
            TrustedArchiveBudget.MaxSoulStateExpandedBytes,
            "Canonical soul state");
        ValidateSoulStateBytes(content);
    }

    private async Task<string> ValidateCanonicalResourceStateForSaveAsync(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease)
    {
        var definitionsJson = await ReadCanonicalResourceFileAsync(
            canonicalSnapshotLease,
            ResourceMaterializationContract.DefinitionsPath);
        var stateJson = await ReadCanonicalResourceFileAsync(
            canonicalSnapshotLease,
            ResourceMaterializationContract.StatePath);
        var historyJson = await ReadCanonicalResourceFileAsync(
            canonicalSnapshotLease,
            ResourceMaterializationContract.HistoryPath);
        return await ValidateResourceDocumentsAsync(
            definitionsJson,
            stateJson,
            historyJson,
            "Canonical save resource authority",
            path => ReadCanonicalResourceFileAsync(canonicalSnapshotLease, path),
            requirePersistedAuthorityRoot: true);
    }

    /// <summary>
    /// Resolves original entry identity exactly first, preserving an existing single case alias without folding distinct payloads.
    /// </summary>
    /// <param name="entries">
    /// The original inventory; typed Linux admission keeps its ordinal keys, while the original reader retains its comparer.
    /// </param>
    /// <param name="path">
    /// The original manifest or schema reference to resolve without renaming an entry.
    /// </param>
    /// <returns>
    /// The exact original entry, its sole case alias, or null when absent; ambiguous aliases are invalid evidence.
    /// </returns>
    private static ZipArchiveEntry? FindOriginalArchiveEntry(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string path)
    {
        if (entries.TryGetValue(path, out var exact)) return exact;
        ZipArchiveEntry? alias = null;
        foreach (var (candidate, entry) in entries)
        {
            if (!candidate.Equals(path, StringComparison.OrdinalIgnoreCase)) continue;
            if (alias != null)
                throw new InvalidDataException($"Save original reference '{path}' has ambiguous case aliases.");
            alias = entry;
        }
        return alias;
    }

    private static async Task ValidateArchivedResourceStateAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> payloadEntries)
    {
        var definitionsJson = await ReadRequiredResourceEntryAsync(
            payloadEntries,
            ResourceMaterializationContract.DefinitionsPath);
        var stateJson = await ReadRequiredResourceEntryAsync(
            payloadEntries,
            ResourceMaterializationContract.StatePath);
        var historyJson = await ReadRequiredResourceEntryAsync(
            payloadEntries,
            ResourceMaterializationContract.HistoryPath);
        _ = await ValidateResourceDocumentsAsync(
            definitionsJson,
            stateJson,
            historyJson,
            "Save archive resource authority",
            path => ReadOptionalArchiveEntryAsync(payloadEntries, path),
            requirePersistedAuthorityRoot: true);
    }

    private static async Task<string> ReadRequiredResourceEntryAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> payloadEntries,
        string path)
    {
        if (FindOriginalArchiveEntry(payloadEntries, path) == null)
        {
            throw new InvalidDataException(
                $"Save archive is missing mandatory canonical state '{path}'.");
        }

        var json = await ReadOptionalArchiveEntryAsync(payloadEntries, path);
        return json ?? throw new InvalidDataException(
            $"Save archive is missing mandatory canonical state '{path}'.");
    }

    private static async Task<string?> ReadOptionalArchiveEntryAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> payloadEntries,
        string path)
    {
        var entry = FindOriginalArchiveEntry(payloadEntries, path);
        if (entry == null)
            return null;

        var bytes = await ReadArchiveEntryBytesAsync(
            entry,
            TrustedArchiveBudget.MaxEntryExpandedBytes,
            $"Canonical state '{path}'");
        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(StripUtf8Bom(bytes).Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Canonical resource state '{path}' is not valid UTF-8.",
                exception);
        }
    }

    private static async Task<string> ValidateResourceDocumentsAsync(
        string? definitionsJson,
        string? stateJson,
        string? historyJson,
        string authorityName,
        Func<string, Task<string?>> readOwnerDocumentAsync,
        bool requirePersistedAuthorityRoot = false)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (definitions.Catalog == null || definitions.Issues.Count != 0)
            ThrowInvalidResourceAuthority(authorityName, definitions.Issues);

        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog!,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog!,
            allowMissingPristine: false);
        var issues = state.Issues
            .Concat(history.Issues)
            .ToList();
        var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions.Catalog!,
            readOwnerDocumentAsync,
            state.Ledger,
            history.History,
            requirePersistedAuthorityRoot
                ? CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation
                : CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        issues.AddRange(owners.Issues);
        if (state.Ledger != null && history.History != null)
        {
            issues.AddRange(history.History.ValidateStateAgreement(state.Ledger));
            if (owners.Authority != null)
                issues.AddRange(owners.Authority.ValidateCanonicalAgreement(
                state.Ledger,
                history.History));
        }
        if (state.Ledger == null || history.History == null || issues.Count != 0)
            ThrowInvalidResourceAuthority(authorityName, issues);
        return owners.CanonicalAuthorityJson ?? throw new InvalidDataException(
            $"{authorityName} did not produce canonical owner authority.");
    }

    private async Task<string?> ReadCanonicalResourceFileAsync(
        FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        string path)
    {
        var bytes = await _fs.ReadFileBytesAsync(canonicalSnapshotLease, path);
        if (bytes == null)
            return null;
        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(StripUtf8Bom(bytes).Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                $"Canonical resource state '{path}' is not valid UTF-8.",
                exception);
        }
    }

    private static void ThrowInvalidResourceAuthority(
        string authorityName,
        IReadOnlyList<ValidationIssue> issues)
    {
        var codes = issues.Count == 0
            ? "invalid canonical resource root"
            : string.Join(", ", issues.Select(issue => issue.Code).Distinct());
        throw new InvalidDataException($"{authorityName} is incompatible: {codes}.");
    }

    private static async Task<byte[]> ReadArchiveEntryBytesAsync(
        ZipArchiveEntry entry,
        long maximumLength,
        string authorityName)
    {
        if (entry.Length < 0 || entry.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{authorityName} exceeds its trusted expanded-size budget.");
        }

        await using var stream = entry.Open();
        using var buffer = new MemoryStream(
            checked((int)entry.Length));
        var chunk = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk);
            if (read == 0)
                break;

            total += read;
            if (total > maximumLength || total > entry.Length)
            {
                throw new InvalidDataException(
                    $"{authorityName} exceeded its advertised expanded length.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }

        if (total != entry.Length)
        {
            throw new InvalidDataException(
                $"{authorityName} did not match its advertised expanded length.");
        }

        return buffer.ToArray();
    }

    private static async Task<string> ComputeArchiveEntrySha256Async(
        ZipArchiveEntry entry,
        long expectedLength)
    {
        await using var stream = entry.Open();
        using var hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
                break;

            total += read;
            if (total > expectedLength)
            {
                throw new InvalidDataException(
                    $"Save payload '{entry.FullName}' exceeded its advertised expanded length.");
            }

            hash.AppendData(buffer, 0, read);
        }

        if (total != expectedLength)
        {
            throw new InvalidDataException(
                $"Save payload '{entry.FullName}' did not match its advertised expanded length.");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
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

    /// <summary>
    /// Applies autosave retention after confirmed creation, stopping on unresolved publication evidence.
    /// </summary>
    /// <param name="saveDir">
    /// The session-relative autosave directory.
    /// </param>
    /// <param name="maxSaves">
    /// The number of newest archives to retain; negative values are treated as zero.
    /// </param>
    /// <returns>
    /// Completion after bounded image deletions, or an explicit failure for the committed save's follow-up.
    /// </returns>
    private async Task CleanupOldSaves(string saveDir, int maxSaves)
    {
        FileSystemManager.CanonicalWriteLease writeLease;
        try { writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(); }
        catch (SessionReplacedException) { throw; }
        catch (Exception failure) { throw new CoordinatedStatePublicationUncertainException(failure); }
        Exception? retentionFailure = null;
        try
        {
            _fs.ResolveBackupPublicationRecovery(writeLease);
            if (_hooks?.BeforeAutosaveRetentionAsync != null)
                await _hooks.BeforeAutosaveRetentionAsync(writeLease);
            var scope = new TrustedLocalFileScope([_fs.GameSessionPath]);
            var fullDir = _fs.ResolvePath(saveDir);
            scope.ValidateDirectory(fullDir);
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
                _fs.ResolveBackupPublicationRecovery(writeLease);
                var before = TrustedLocalFileImage.CaptureFile(scope, _fs.ResolvePath(file));
                var outcome = await _fs.PublishLocalImageFilesAsync(writeLease,
                    [new CanonicalLocalImageChange(file, before, TrustedLocalFileImage.FromBytes(null))]);
                if (outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain)
                    throw new CoordinatedStatePublicationUncertainException(outcome.Failure);
                _fs.RequireCommittedLocalPublication(outcome);
                _fs.ResolveBackupPublicationRecovery(writeLease);
            }
        }
        catch (Exception failure)
        {
            retentionFailure = failure;
            throw;
        }
        finally
        {
            try { await writeLease.DisposeAsync(); }
            catch (Exception releaseFailure)
            {
                // Preserve a primary stop decision; a failed release also stops
                // continuation when retention otherwise completed or rolled back.
                if (retentionFailure is CoordinatedStatePublicationUncertainException or SessionReplacedException)
                    retentionFailure.Data["AutosaveRetentionLeaseReleaseFailure"] = releaseFailure;
                else
                    throw new CoordinatedStatePublicationUncertainException(retentionFailure == null
                        ? releaseFailure : new AggregateException(retentionFailure, releaseFailure));
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
