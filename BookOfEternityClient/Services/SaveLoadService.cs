using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

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
        "game_state/control/terminal_protocol_failure_request.json",
        "game_state/control/gm_cli_window_binding.json",
        "output/ink_feather_action_result.json"
    };

    private static readonly string[] EphemeralPathPrefixes =
    {
        "game_state/control/pending_turn_snapshot/"
    };

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ILogger<SaveLoadService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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

            // Clear current state
            _fs.ClearGameState();
            ClearAuthoredSourceDirectoriesForLoad();

            // Extract archive
            using var archive = ZipFile.OpenRead(fullPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directory entries

                var targetPath = _fs.ResolvePath(entry.FullName);
                var targetDir = Path.GetDirectoryName(targetPath);
                if (targetDir != null && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                entry.ExtractToFile(targetPath, overwrite: true);
            }

            foreach (var relativePath in EphemeralControlFiles)
                _fs.DeleteFile(relativePath);

            foreach (var relativePrefix in EphemeralPathPrefixes)
            {
                var cleanupPath = _fs.ResolvePath(relativePrefix.TrimEnd('/', '\\'));
                if (Directory.Exists(cleanupPath))
                    Directory.Delete(cleanupPath, recursive: true);
            }

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

    private void ClearAuthoredSourceDirectoriesForLoad()
    {
        foreach (var relativeDir in new[] { "mods", "world_profiles" })
        {
            var fullDir = _fs.ResolvePath(relativeDir);
            if (!Directory.Exists(fullDir))
                continue;

            foreach (var file in Directory.GetFiles(fullDir, "*", SearchOption.AllDirectories))
                File.Delete(file);
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
