using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

public enum ImageExportFailureReason
{
    None,
    SourceMissing,
    DestinationExists,
    InvalidTarget,
    CopyFailed
}

internal sealed record StagedEntityImage(
    byte[] Content,
    string CanonicalRelativePath);

public sealed class ImageExportResult
{
    private ImageExportResult(
        bool success,
        ImageExportFailureReason failureReason,
        string? sourcePath,
        string? destinationPath,
        string errorMessage)
    {
        Success = success;
        FailureReason = failureReason;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public ImageExportFailureReason FailureReason { get; }
    public string? SourcePath { get; }
    public string? DestinationPath { get; }
    public string ErrorMessage { get; }

    public static ImageExportResult SuccessResult(string sourcePath, string destinationPath) =>
        new(true, ImageExportFailureReason.None, sourcePath, destinationPath, string.Empty);

    public static ImageExportResult Failure(
        ImageExportFailureReason failureReason,
        string errorMessage,
        string? sourcePath = null,
        string? destinationPath = null) =>
        new(false, failureReason, sourcePath, destinationPath, errorMessage);
}

/// <summary>
/// Image generation and display service.
/// Supports scene images (per-turn) and entity images (NPCs, items, locations, etc.)
/// via Pollinations.ai API with console or external viewer display.
/// </summary>
public class ImageService
{
    private const string VersionSeparator = "__img_";

    private readonly ILogger<ImageService> _logger;
    private readonly Configuration.GameSettings _settings;
    private readonly LocalizationManager _loc;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly string _imageBaseDir;

    // Entity type subdirectories
    private static readonly Dictionary<string, string> EntityDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scene"] = "scenes",
        ["npc"] = "npcs",
        ["item"] = "items",
        ["location"] = "locations",
        ["player"] = "player",
        ["faction"] = "factions",
        ["vehicle"] = "vehicles",
        ["guardian"] = "guardians",
        ["abode"] = "abodes",
        ["quest"] = "quests",
    };

    internal static bool IsSupportedEntityType(string? entityType) =>
        !string.IsNullOrWhiteSpace(entityType) &&
        EntityDirs.ContainsKey(entityType.Trim());

    private readonly Core.FileSystemManager _fs;

    public bool GenerateWithoutDisplay => _settings.GenerateImagesWithoutDisplay;

    public ImageService(Core.FileSystemManager fs, Configuration.GameSettings settings, LocalizationManager loc, ILogger<ImageService> logger)
    {
        _fs = fs;
        _settings = settings;
        _loc = loc;
        _logger = logger;
        _imageBaseDir = _fs.ResolvePath("images");
        Directory.CreateDirectory(_imageBaseDir);
    }

    /// <summary>
    /// Process a scene image prompt from the GM response (per-turn).
    /// </summary>
    public async Task ProcessSceneImagePrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        if (!_settings.GenerateSceneImages) return;

        _logger.LogDebug("Scene image prompt: {Prompt}", prompt);

        try
        {
            // Check if prompt is actually a file path
            if (File.Exists(prompt) && IsImageFile(prompt))
            {
                if (!_settings.GenerateImagesWithoutDisplay)
                    DisplayImageFile(prompt);
                return;
            }

            // Check for recent image files in output directory
            var outputDir = _fs.ResolvePath("output");
            if (Directory.Exists(outputDir))
            {
                var imageFiles = Directory.GetFiles(outputDir, "*.*")
                    .Where(IsImageFile)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                if (imageFiles.Length > 0)
                {
                    var newest = imageFiles[0];
                    if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(newest)).TotalSeconds < 30)
                    {
                        if (!_settings.GenerateImagesWithoutDisplay)
                            DisplayImageFile(newest);
                        return;
                    }
                }
            }

            // Generate scene image
            var provider = (_settings.ImageProvider ?? "placeholder").ToLowerInvariant();
            if (provider != "placeholder" && provider != "none" && provider != "off")
            {
                var filePath = await GenerateImageAsync(prompt, "scene", $"scene_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}");
                if (filePath != null)
                {
                    if (!_settings.GenerateImagesWithoutDisplay)
                        DisplayImageFile(filePath);
                    return;
                }
            }

            if (!_settings.GenerateImagesWithoutDisplay)
                DisplayPromptPanel(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing scene image");
            if (!_settings.GenerateImagesWithoutDisplay)
                DisplayPromptPanel(prompt);
        }
    }

    /// <summary>
    /// Legacy entry point — redirects to scene image processing.
    /// </summary>
    public async Task ProcessImagePrompt(string? prompt) => await ProcessSceneImagePrompt(prompt);

    /// <summary>
    /// Show an entity's current image if it exists.
    /// </summary>
    public bool ShowEntityImage(string entityType, string entityKeyOrName, bool forceDisplay = false)
    {
        var path = GetEntityImagePath(entityType, entityKeyOrName);
        if (path == null || !File.Exists(path))
            return false;

        DisplayImageFile(path, forceDisplay);
        return true;
    }

    /// <summary>
    /// Show an existing entity image or generate a fresh one if it does not exist yet.
    /// </summary>
    public async Task<bool> ShowOrGenerateEntityImageAsync(string imagePrompt, string entityType, string entityKeyOrName, bool forceDisplay = false)
    {
        if (ShowEntityImage(entityType, entityKeyOrName, forceDisplay))
            return true;

        return await GenerateEntityImageAsync(imagePrompt, entityType, entityKeyOrName,
            displayAfterGenerate: true, forceDisplay: forceDisplay);
    }

    /// <summary>
    /// Generate (or regenerate) an entity image from its image_prompt.
    /// Old files are preserved; the newest version becomes the current image.
    /// </summary>
    public async Task<bool> GenerateEntityImageAsync(string imagePrompt, string entityType, string entityKeyOrName,
        bool displayAfterGenerate = true, bool forceDisplay = false)
    {
        if (!IsSupportedEntityType(entityType))
            return false;

        if (string.IsNullOrWhiteSpace(imagePrompt))
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("image_no_prompt"))}[/]");
            return false;
        }

        var provider = (_settings.ImageProvider ?? "placeholder").ToLowerInvariant();
        if (provider == "placeholder" || provider == "none" || provider == "off")
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(_loc.T("image_provider_disabled"))}[/]");
            return false;
        }

        var safeKey = SanitizeFileName(entityKeyOrName);
        if (string.IsNullOrWhiteSpace(safeKey))
            safeKey = "entity";

        var versionedFileName = $"{safeKey}{VersionSeparator}{DateTime.UtcNow:yyyyMMdd_HHmmssfff}";
        var filePath = await GenerateImageAsync(imagePrompt, entityType, versionedFileName);
        if (filePath != null)
        {
            if (displayAfterGenerate)
                DisplayImageFile(filePath, forceDisplay);
            return true;
        }

        return false;
    }

    internal async Task<StagedEntityImage?> StageEntityImageAsync(
        string imagePrompt,
        string entityType,
        string entityKeyOrName)
    {
        if (string.IsNullOrWhiteSpace(imagePrompt))
            return null;
        if (!IsSupportedEntityType(entityType))
            return null;

        var provider = (_settings.ImageProvider ?? "placeholder").ToLowerInvariant();
        if (provider is "placeholder" or "none" or "off")
            return null;

        var safeKey = SanitizeFileName(entityKeyOrName);
        if (string.IsNullOrWhiteSpace(safeKey))
            safeKey = "entity";

        var versionedFileName = $"{safeKey}{VersionSeparator}{DateTime.UtcNow:yyyyMMdd_HHmmssfff}";
        var finalPath = Path.Combine(GetEntityDir(entityType), versionedFileName + ".png");
        var canonicalRelativePath = Path.GetRelativePath(_fs.GameSessionPath, finalPath).Replace('\\', '/');
        if (canonicalRelativePath.StartsWith("../", StringComparison.Ordinal) ||
            Path.IsPathRooted(canonicalRelativePath))
        {
            throw new InvalidOperationException("Generated media target escapes game_session.");
        }

        var bytes = await DownloadImageBytesAsync(imagePrompt, entityType, versionedFileName);
        if (bytes == null)
            return null;

        _logger.LogInformation(
            "Image downloaded for generation-fenced commit: {Path} ({Size} KB)",
            canonicalRelativePath,
            bytes.Length / 1024);
        return new StagedEntityImage(bytes, canonicalRelativePath);
    }

    /// <summary>
    /// Generate a scene/QTE image at most once for the given key.
    /// Existing files are not reopened or regenerated inside the client.
    /// </summary>
    public async Task<bool> GenerateSceneImageOnceAsync(string imagePrompt, string sceneKey)
    {
        if (string.IsNullOrWhiteSpace(imagePrompt))
            return false;

        if (EntityImageExists("scene", sceneKey))
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(_loc.T("scene_image_locked"))}[/]");
            return true;
        }

        return await GenerateEntityImageAsync(imagePrompt, "scene", sceneKey,
            displayAfterGenerate: !_settings.GenerateImagesWithoutDisplay);
    }

    /// <summary>
    /// Check if an entity already has a generated image.
    /// </summary>
    public bool EntityImageExists(string entityType, string entityKeyOrName)
    {
        return GetEntityImagePath(entityType, entityKeyOrName) != null;
    }

    /// <summary>
    /// Get full path to an entity's current image, or null if not found.
    /// The latest version wins; legacy single-file images are also supported.
    /// </summary>
    public string? GetEntityImagePath(string entityType, string entityKeyOrName)
    {
        if (!IsSupportedEntityType(entityType))
            return null;

        var dir = GetEntityDir(entityType);
        if (!Directory.Exists(dir)) return null;

        var safeKey = SanitizeFileName(entityKeyOrName);
        if (string.IsNullOrWhiteSpace(safeKey)) return null;

        return EnumerateEntityImageCandidates(dir, safeKey)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Copy the current saved entity image to an external folder or explicit file path.
    /// The source image is never modified; existing destination files require explicit overwrite.
    /// </summary>
    public ImageExportResult ExportEntityImage(
        string entityType,
        string entityKeyOrName,
        string targetDirectoryOrFilePath,
        bool overwrite = false)
    {
        var sourcePath = GetEntityImagePath(entityType, entityKeyOrName);
        if (sourcePath == null || !File.Exists(sourcePath))
        {
            return ImageExportResult.Failure(
                ImageExportFailureReason.SourceMissing,
                "Сохранённое изображение не найдено.");
        }

        if (string.IsNullOrWhiteSpace(targetDirectoryOrFilePath))
        {
            return ImageExportResult.Failure(
                ImageExportFailureReason.InvalidTarget,
                "Не указан путь для сохранения изображения.",
                sourcePath);
        }

        try
        {
            var destinationPath = ResolveExportDestinationPath(sourcePath, targetDirectoryOrFilePath);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDir))
            {
                return ImageExportResult.Failure(
                    ImageExportFailureReason.InvalidTarget,
                    "Не удалось определить папку для сохранения изображения.",
                    sourcePath,
                    destinationPath);
            }

            Directory.CreateDirectory(destinationDir);

            if (File.Exists(destinationPath) && !overwrite)
            {
                return ImageExportResult.Failure(
                    ImageExportFailureReason.DestinationExists,
                    "Файл уже существует. Подтвердите перезапись или выберите другой путь.",
                    sourcePath,
                    destinationPath);
            }

            File.Copy(sourcePath, destinationPath, overwrite);
            return ImageExportResult.SuccessResult(sourcePath, destinationPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return ImageExportResult.Failure(
                ImageExportFailureReason.CopyFailed,
                $"Не удалось сохранить изображение: {ex.Message}",
                sourcePath);
        }
    }

    /// <summary>
    /// Delete stale image versions. Keeps only the current image for each entity and removes all scene images.
    /// </summary>
    public ImageCleanupResult CleanupExtraImages()
    {
        var result = new ImageCleanupResult();

        foreach (var entityType in EntityDirs.Keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = GetEntityDir(entityType);
            if (!Directory.Exists(dir))
                continue;

            var files = Directory.GetFiles(dir, "*.*")
                .Where(IsImageFile)
                .ToArray();

            if (entityType.Equals("scene", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var file in files)
                {
                    if (TryDeleteFile(file))
                        result.DeletedSceneImages++;
                }
                continue;
            }

            foreach (var group in files.GroupBy(file => ExtractEntityGroupKey(Path.GetFileNameWithoutExtension(file))))
            {
                var keep = group
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                foreach (var file in group)
                {
                    if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (TryDeleteFile(file))
                        result.DeletedEntityImages++;
                }
            }
        }

        var outputDir = _fs.ResolvePath("output");
        if (Directory.Exists(outputDir))
        {
            foreach (var file in Directory.GetFiles(outputDir, "*.*")
                         .Where(IsImageFile))
            {
                if (TryDeleteFile(file))
                    result.DeletedSceneImages++;
            }
        }

        return result;
    }

    /// <summary>
    /// Generate an image from a prompt and save to the appropriate entity directory.
    /// </summary>
    private async Task<string?> GenerateImageAsync(string prompt, string entityType, string fileName)
    {
        if (!IsSupportedEntityType(entityType))
            return null;

        var dir = GetEntityDir(entityType);
        var filePath = Path.Combine(dir, fileName + ".png");
        var canonicalRelativePath = Path.GetRelativePath(
                _fs.GameSessionPath,
                filePath)
            .Replace('\\', '/');

        string generation;
        if (!SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out generation))
        {
            await using var generationLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            generation = _fs.GetOrCreateSessionGeneration(generationLease);
        }

        try
        {
            return await SessionOperationContext.RunBoundAsync(
                _fs,
                generation,
                async () =>
                {
                    var bytes = await DownloadImageBytesAsync(prompt, entityType, fileName);
                    if (bytes == null)
                        return null;

                    await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                    await _fs.WriteFileAtomicBytesAsync(
                        writeLease,
                        canonicalRelativePath,
                        bytes);
                    _logger.LogInformation(
                        "Image saved through generation fence: {Path} ({Size} KB)",
                        filePath,
                        bytes.Length / 1024);
                    return filePath;
                });
        }
        catch (SessionReplacedException ex)
        {
            _logger.LogInformation(
                ex,
                "Generated image was discarded because the game session changed.");
            return null;
        }
    }

    private async Task<byte[]?> DownloadImageBytesAsync(
        string prompt,
        string entityType,
        string fileName)
    {
        var width = _settings.ImageWidth > 0 ? _settings.ImageWidth : 768;
        var height = _settings.ImageHeight > 0 ? _settings.ImageHeight : 512;

        var provider = (_settings.ImageProvider ?? "pollinations").ToLowerInvariant();
        if (!provider.StartsWith("pollinations", StringComparison.Ordinal))
        {
            _logger.LogWarning("Unknown image provider: {Provider}", provider);
            AnsiConsole.MarkupLine($"[yellow dim]  Unknown provider: {Markup.Escape(provider)}. Available: pollinations[/]");
            return null;
        }

        var model = !string.IsNullOrWhiteSpace(_settings.PollinationsImageModel)
            ? _settings.PollinationsImageModel : "flux";
        var encodedPrompt = Uri.EscapeDataString(prompt);
        var url = $"https://gen.pollinations.ai/image/{encodedPrompt}?model={Uri.EscapeDataString(model)}&width={width}&height={height}&nologo=true";

        var apiKey = _settings.PollinationsApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
            url += $"&key={Uri.EscapeDataString(apiKey)}";

        _logger.LogInformation("Generating image: {Provider} [{Type}/{File}]", provider, entityType, fileName);

        byte[]? resultBytes = null;
        const int maxRetries = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            string? errorMessage = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Aesthetic)
                .SpinnerStyle(Style.Parse("purple"))
                .StartAsync(attempt == 1 ? "🎨 Генерация изображения..." : $"🎨 Повторная попытка ({attempt}/{maxRetries})...", async _ =>
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            if (bytes.Length > 1000)
                            {
                                resultBytes = bytes;
                            }
                            else
                            {
                                errorMessage = $"Response too small ({bytes.Length} bytes)";
                            }
                        }
                        else
                        {
                            errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        errorMessage = "Timeout (120s)";
                    }
                    catch (HttpRequestException ex)
                    {
                        errorMessage = $"Network: {ex.Message}";
                    }
                });

            if (resultBytes != null) break;

            if (errorMessage != null)
            {
                _logger.LogWarning("Generation error (attempt {Attempt}): {Error}", attempt, errorMessage);
                if (attempt < maxRetries)
                    AnsiConsole.MarkupLine($"[yellow dim]  ⚠ {Markup.Escape(errorMessage)}, retrying...[/]");
                else
                    AnsiConsole.MarkupLine($"[yellow dim]  ⚠ Generation failed: {Markup.Escape(errorMessage)}[/]");
            }
        }

        return resultBytes;
    }

    /// <summary>
    /// Display an image file in console or external viewer based on settings.
    /// </summary>
    public void DisplayImageFile(string imagePath, bool forceDisplay = false)
    {
        if (!File.Exists(imagePath)) return;

        if (_settings.GenerateImagesWithoutDisplay && !forceDisplay)
            return;

        if (_settings.ShowImagesInConsole)
        {
            try
            {
                var image = new CanvasImage(imagePath);
                var maxW = Math.Max(20, Console.WindowWidth - 6);
                image.MaxWidth(maxW);

                var panel = new Panel(image)
                {
                    Header = new PanelHeader(" 🎨 ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Purple),
                    Padding = new Padding(1, 0)
                };

                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Console image display failed, opening in viewer");
                OpenImageInViewer(imagePath, forceDisplay);
            }
        }
        else
        {
            OpenImageInViewer(imagePath, forceDisplay);
        }
    }

    /// <summary>
    /// Display an image prompt as styled text panel when no image file exists.
    /// </summary>
    private static void DisplayPromptPanel(string prompt)
    {
        var escapedPrompt = Markup.Escape(prompt);
        var content = new Markup($"[dim italic]🖼 {escapedPrompt}[/]");

        var panel = new Panel(content)
        {
            Header = new PanelHeader(" 🎨 Визуализация сцены ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1),
            Expand = true
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void OpenImageInViewer(string imagePath, bool forceDisplay = false)
    {
        if (!File.Exists(imagePath)) return;

        if (_settings.GenerateImagesWithoutDisplay && !forceDisplay)
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = imagePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open image in viewer");
        }
    }

    /// <summary>
    /// Open the entity images folder in file explorer.
    /// </summary>
    public void OpenImagesFolder(string? entityType = null)
    {
        if (entityType != null && !IsSupportedEntityType(entityType))
            return;

        var dir = entityType != null ? GetEntityDir(entityType) : _imageBaseDir;
        Directory.CreateDirectory(dir);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open images folder");
        }
    }

    private IEnumerable<string> EnumerateEntityImageCandidates(string dir, string safeKey)
    {
        var versionedPrefix = safeKey + VersionSeparator;

        return Directory.GetFiles(dir, "*.*")
            .Where(IsImageFile)
            .Where(path =>
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                return stem.Equals(safeKey, StringComparison.OrdinalIgnoreCase) ||
                       stem.StartsWith(versionedPrefix, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string ResolveExportDestinationPath(string sourcePath, string targetDirectoryOrFilePath)
    {
        var trimmedTarget = targetDirectoryOrFilePath.Trim().Trim('"');
        var expandedTarget = Environment.ExpandEnvironmentVariables(trimmedTarget);
        var fullTarget = Path.GetFullPath(expandedTarget);

        var targetLooksLikeDirectory =
            Directory.Exists(fullTarget) ||
            trimmedTarget.EndsWith(Path.DirectorySeparatorChar) ||
            trimmedTarget.EndsWith(Path.AltDirectorySeparatorChar) ||
            string.IsNullOrWhiteSpace(Path.GetExtension(fullTarget));

        return targetLooksLikeDirectory
            ? Path.Combine(fullTarget, Path.GetFileName(sourcePath))
            : fullTarget;
    }

    private string GetEntityDir(string entityType)
    {
        if (!EntityDirs.TryGetValue(entityType.Trim(), out var subDir))
            throw new InvalidDataException($"Unsupported image entity type: {entityType}");

        return _fs.ResolvePath($"images/{subDir}");
    }

    private bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete image file {Path}", path);
            return false;
        }
    }

    private static string ExtractEntityGroupKey(string stem)
    {
        var versionSepIndex = stem.IndexOf(VersionSeparator, StringComparison.OrdinalIgnoreCase);
        return versionSepIndex >= 0 ? stem[..versionSepIndex] : stem;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        if (sanitized.Length > 80) sanitized = sanitized[..80];
        sanitized = sanitized.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "entity" : sanitized;
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    public sealed class ImageCleanupResult
    {
        public int DeletedSceneImages { get; set; }
        public int DeletedEntityImages { get; set; }
    }
}
