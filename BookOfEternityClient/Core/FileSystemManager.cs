using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

/// <summary>
/// Manages the game_session directory structure per CLI API specification.
/// Creates all required directories and validates file system integrity.
/// </summary>
public class FileSystemManager
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemManager> _logger;

    private static readonly string[] RequiredDirectories =
    {
        "game_session/input",
        "game_session/game_state/core",
        "game_session/game_state/player",
        "game_session/game_state/inventory",
        "game_session/game_state/world",
        "game_session/game_state/quests",
        "game_session/game_state/npcs",
        "game_session/game_state/combat",
        "game_session/game_state/factions",
        "game_session/game_state/meta",
        "game_session/game_state/misc",
        "game_session/game_state/control",
        "game_session/game_state/history",
        "game_session/lore/chaos_sea",
        "game_session/lore/shining_abode",
        "game_session/lore/current_world",
        "game_session/mods",
        "game_session/world_profiles",
        "game_session/output",
        "game_session/ready",
        "game_session/saves/manual_saves",
        "game_session/saves/autosaves",
        "game_session/saves/checkpoint_saves",
        "game_session/stories",
        "game_session/images"
    };

    public string BasePath => _basePath;
    public string GameSessionPath => Path.Combine(_basePath, "game_session");

    public FileSystemManager(string basePath, ILogger<FileSystemManager> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public void EnsureDirectoryStructure()
    {
        foreach (var dir in RequiredDirectories)
        {
            var fullPath = Path.Combine(_basePath, dir);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogDebug("Создана директория: {Path}", dir);
            }
        }
    }

    public string ResolvePath(string relativePath)
    {
        return Path.Combine(_basePath, "game_session", relativePath);
    }

    /// <summary>
    /// Atomic write: write to temp file then rename to prevent partial writes.
    /// </summary>
    public async Task WriteFileAtomicAsync(string relativePath, string content)
    {
        var fullPath = ResolvePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tempPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await File.WriteAllTextAsync(tempPath, content, System.Text.Encoding.UTF8);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public async Task<string?> ReadFileAsync(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;
        return await File.ReadAllTextAsync(fullPath, System.Text.Encoding.UTF8);
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(ResolvePath(relativePath));
    }

    public void DeleteFile(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Create a backup of a file before modification.
    /// </summary>
    public string? CreateBackup(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        var backupPath = fullPath + $".backup.{DateTime.UtcNow.Ticks}";
        File.Copy(fullPath, backupPath, overwrite: true);
        return backupPath;
    }

    public void RestoreBackup(string backupFullPath, string originalRelativePath)
    {
        var originalFullPath = ResolvePath(originalRelativePath);
        if (File.Exists(backupFullPath))
        {
            File.Copy(backupFullPath, originalFullPath, overwrite: true);
            File.Delete(backupFullPath);
        }
    }

    public void CleanupBackup(string backupFullPath)
    {
        if (File.Exists(backupFullPath))
            File.Delete(backupFullPath);
    }

    /// <summary>
    /// Get all JSON files in game_state directory.
    /// </summary>
    public string[] GetAllGameStateFiles()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (!Directory.Exists(gameStatePath))
            return Array.Empty<string>();
        return Directory.GetFiles(gameStatePath, "*.json", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Clear all game state for a new game.
    /// </summary>
    public void ClearGameState()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (Directory.Exists(gameStatePath))
        {
            foreach (var file in Directory.GetFiles(gameStatePath, "*.json", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }

        // Clear output and ready
        var outputPath = Path.Combine(_basePath, "game_session", "output");
        if (Directory.Exists(outputPath))
        {
            foreach (var file in Directory.GetFiles(outputPath, "*.json"))
                File.Delete(file);
        }

        var readyPath = Path.Combine(_basePath, "game_session", "ready");
        if (Directory.Exists(readyPath))
        {
            foreach (var file in Directory.GetFiles(readyPath, "*.json"))
                File.Delete(file);
        }

        var lorePath = Path.Combine(_basePath, "game_session", "lore");
        if (Directory.Exists(lorePath))
        {
            foreach (var file in Directory.GetFiles(lorePath, "*", SearchOption.AllDirectories))
                File.Delete(file);
        }

        var storiesPath = Path.Combine(_basePath, "game_session", "stories");
        if (Directory.Exists(storiesPath))
        {
            foreach (var file in Directory.GetFiles(storiesPath, "*", SearchOption.AllDirectories))
                File.Delete(file);
        }

        // Re-create structure
        EnsureDirectoryStructure();
    }

    public void ClearCurrentWorldLore()
    {
        var currentWorldPath = Path.Combine(_basePath, "game_session", "lore", "current_world");
        if (!Directory.Exists(currentWorldPath))
            return;

        foreach (var file in Directory.GetFiles(currentWorldPath, "*", SearchOption.AllDirectories))
            File.Delete(file);
    }
}
