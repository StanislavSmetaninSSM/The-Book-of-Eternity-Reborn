using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Manages save/load with ZIP archives, autosaves, and metadata.
/// </summary>
public class SaveLoadService
{
    private static readonly string[] ImportDirectoryRoots =
    {
        "game_state",
        "lore",
        "mods",
        "world_profiles",
        "stories",
        "images",
        "output"
    };

    private static readonly string[] ImportFileRoots =
    {
        "config.json"
    };

    private const string SaveMetadataEntryPath = "save_metadata.json";

    private static readonly HashSet<string> EphemeralControlFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "game_state/control/pending_turn_snapshot.json",
        "game_state/control/validation_repair_request.json",
        "game_state/control/validation_repair_ready.json",
        "game_state/control/terminal_protocol_failure_request.json",
        ProgressionScheduleService.ReportPath,
        "game_state/control/gm_cli_window_binding.json",
        "game_state/control/gm_bridge_status.json",
        "output/ink_feather_action_result.json"
    };

    private static readonly string[] EphemeralPathPrefixes =
    {
        "game_state/control/pending_turn_snapshot/",
        QteSceneService.QteNormalizerBackupDirectory + "/"
    };

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ILogger<SaveLoadService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public SaveLoadService(FileSystemManager fs, StateManager stateManager, ILogger<SaveLoadService> logger)
    {
        _fs = fs;
        _stateManager = stateManager;
        _logger = logger;
    }

    public async Task<bool> SaveGameAsync(string saveName, string description, string saveDir = "saves/manual_saves", int turnNumber = 0)
    {
        try
        {
            var state = _stateManager.CurrentState;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = SanitizeFileName($"{saveName}_{timestamp}.zip");
            var fullPath = _fs.ResolvePath(Path.Combine(saveDir, fileName));

            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var archive = ZipFile.Open(fullPath, ZipArchiveMode.Create))
            {
                // Add game_state directory
                await AddDirectoryToArchive(archive, _fs.ResolvePath("game_state"), "game_state");

                // Add lore directory
                await AddDirectoryToArchive(archive, _fs.ResolvePath("lore"), "lore");

                // Add player-authored source layers that affect rules/world setup
                await AddDirectoryToArchive(archive, _fs.ResolvePath("mods"), "mods");
                await AddDirectoryToArchive(archive, _fs.ResolvePath("world_profiles"), "world_profiles");

                // Add stories (persistent conversation history)
                await AddDirectoryToArchive(archive, _fs.ResolvePath("stories"), "stories");

                // Add entity images (NPCs, items, locations, player — NOT scenes)
                var imagesPath = _fs.ResolvePath("images");
                if (Directory.Exists(imagesPath))
                {
                    foreach (var subDir in Directory.GetDirectories(imagesPath))
                    {
                        var dirName = Path.GetFileName(subDir);
                        if (dirName == "scenes") continue; // Scene images are ephemeral, skip
                        await AddDirectoryToArchive(archive, subDir, $"images/{dirName}");
                    }
                }

                // Add output
                await AddDirectoryToArchive(archive, _fs.ResolvePath("output"), "output");

                // Add config
                var configPath = _fs.ResolvePath("config.json");
                if (File.Exists(configPath))
                    archive.CreateEntryFromFile(configPath, "config.json");

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

            _logger.LogInformation("Игра сохранена: {Name}", saveName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения: {Name}", saveName);
            return false;
        }
    }

    public async Task<bool> AutosaveAsync(int turnNumber)
    {
        // Rotate autosaves
        await CleanupOldSaves("saves/autosaves", _stateManager.Settings.MaxAutosaves);
        return await SaveGameAsync($"autosave_turn{turnNumber}", $"Автосохранение - ход {turnNumber}", "saves/autosaves", turnNumber);
    }

    public async Task<bool> LoadGameAsync(string saveFilePath)
    {
        string? stagingRoot = null;
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

            stagingRoot = CreateStagingRoot();

            using var archive = ZipFile.OpenRead(fullPath);
            ExtractArchiveToStaging(archive, stagingRoot);
            CleanupEphemeralArtifacts(stagingRoot);
            ApplyStagedImportWithRollback(stagingRoot);

            // Refresh state
            await _stateManager.RefreshGameStateAsync();
            await _stateManager.LoadSettingsAsync();

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
            TryDeleteDirectory(stagingRoot);
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
                using var archive = ZipFile.OpenRead(saveFile);
                var metadataEntry = archive.GetEntry("save_metadata.json");

                if (metadataEntry != null)
                {
                    using var stream = metadataEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    var metadata = JsonSerializer.Deserialize<SaveMetadata>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    saves.Add(new SaveInfo
                    {
                        FileName = saveFile,
                        Metadata = metadata,
                        FileSize = new FileInfo(saveFile).Length
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Повреждённое сохранение: {File}", Path.GetFileName(saveFile));
            }
        }

        return saves.OrderByDescending(s => s.Metadata?.Timestamp).ToList();
    }

    private Task AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryPrefix)
    {
        if (!Directory.Exists(sourceDir)) return Task.CompletedTask;

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var entryPath = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
            if (EphemeralControlFiles.Contains(entryPath) ||
                EphemeralPathPrefixes.Any(prefix => entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;
            archive.CreateEntryFromFile(file, entryPath);
        }
        return Task.CompletedTask;
    }

    private static bool IsImportEntryPathAllowed(string normalizedPath)
    {
        if (ImportFileRoots.Any(path => string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            return true;

        return ImportDirectoryRoots.Any(path =>
            normalizedPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));
    }

    private string CreateStagingRoot()
    {
        var stagingRoot = Path.Combine(_fs.BasePath, $"load_staging_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        return stagingRoot;
    }

    private string CreateBackupRoot()
    {
        var backupRoot = Path.Combine(_fs.BasePath, $"load_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupRoot);
        return backupRoot;
    }

    private static string NormalizeArchiveEntryPath(string entryPath)
    {
        var normalized = (entryPath ?? string.Empty).Replace('\\', '/').Trim();
        while (normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = normalized[1..];
        return normalized;
    }

    private static string ResolveConstrainedTargetPath(string rootPath, string archiveEntryPath)
    {
        var normalizedPath = NormalizeArchiveEntryPath(archiveEntryPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
            throw new InvalidDataException("ZIP entry path must not be empty.");
        if (Path.IsPathRooted(normalizedPath) ||
            normalizedPath.StartsWith(".", StringComparison.Ordinal) ||
            !IsImportEntryPathAllowed(normalizedPath))
        {
            throw new InvalidDataException($"Архив содержит недопустимый путь: {archiveEntryPath}");
        }

        var rootFullPath = Path.GetFullPath(rootPath);
        var resolvedPath = Path.GetFullPath(Path.Combine(rootFullPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var constrainedPrefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;
        if (!resolvedPath.StartsWith(constrainedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Архив пытается выйти за пределы целевой директории: {archiveEntryPath}");

        return resolvedPath;
    }

    private static void ExtractArchiveToStaging(ZipArchive archive, string stagingRoot)
    {
        foreach (var entry in archive.Entries)
        {
            var normalizedEntryPath = NormalizeArchiveEntryPath(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalizedEntryPath))
                continue;
            if (string.Equals(normalizedEntryPath, SaveMetadataEntryPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var targetPath = ResolveConstrainedTargetPath(stagingRoot, normalizedEntryPath);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void CleanupEphemeralArtifacts(string rootPath)
    {
        foreach (var relativePath in EphemeralControlFiles)
        {
            var fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        foreach (var relativePrefix in EphemeralPathPrefixes)
        {
            var fullPath = Path.Combine(rootPath, relativePrefix.TrimEnd('/', '\\').Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
    }

    private void ApplyStagedImportWithRollback(string stagingRoot)
    {
        string? backupRoot = null;
        try
        {
            backupRoot = CreateBackupRoot();
            BackupCurrentImportTargets(backupRoot);
            ClearImportTargets(_fs.GameSessionPath);
            CopyImportTargets(stagingRoot, _fs.GameSessionPath);
            _fs.EnsureDirectoryStructure();
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(backupRoot) && Directory.Exists(backupRoot))
            {
                ClearImportTargets(_fs.GameSessionPath);
                CopyImportTargets(backupRoot, _fs.GameSessionPath);
                _fs.EnsureDirectoryStructure();
            }

            throw;
        }
        finally
        {
            TryDeleteDirectory(backupRoot);
        }
    }

    private void BackupCurrentImportTargets(string backupRoot)
    {
        foreach (var relativeDir in ImportDirectoryRoots)
        {
            var sourceDir = Path.Combine(_fs.GameSessionPath, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(sourceDir))
                continue;

            var destinationDir = Path.Combine(backupRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            CopyDirectory(sourceDir, destinationDir);
        }

        foreach (var relativeFile in ImportFileRoots)
        {
            var sourceFile = Path.Combine(_fs.GameSessionPath, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourceFile))
                continue;

            var destinationFile = Path.Combine(backupRoot, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            var destinationDir = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);
            File.Copy(sourceFile, destinationFile, overwrite: true);
        }
    }

    private static void CopyImportTargets(string sourceRoot, string destinationRoot)
    {
        foreach (var relativeDir in ImportDirectoryRoots)
        {
            var sourceDir = Path.Combine(sourceRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(sourceDir))
                continue;

            var destinationDir = Path.Combine(destinationRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            CopyDirectory(sourceDir, destinationDir);
        }

        foreach (var relativeFile in ImportFileRoots)
        {
            var sourceFile = Path.Combine(sourceRoot, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourceFile))
                continue;

            var destinationFile = Path.Combine(destinationRoot, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            var destinationDir = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);
            File.Copy(sourceFile, destinationFile, overwrite: true);
        }
    }

    private static void ClearImportTargets(string rootPath)
    {
        foreach (var relativeDir in ImportDirectoryRoots)
        {
            var fullDir = Path.Combine(rootPath, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(fullDir))
                Directory.Delete(fullDir, recursive: true);
        }

        foreach (var relativeFile in ImportFileRoots)
        {
            var fullFile = Path.Combine(rootPath, relativeFile.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullFile))
                File.Delete(fullFile);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destinationSubdir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destinationSubdir);
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    private Task CleanupOldSaves(string saveDir, int maxSaves)
    {
        var fullDir = _fs.ResolvePath(saveDir);
        if (!Directory.Exists(fullDir)) return Task.CompletedTask;

        var files = Directory.GetFiles(fullDir, "*.zip")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Skip(maxSaves - 1)
            .ToArray();

        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch { /* ignore cleanup errors */ }
        }
        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
