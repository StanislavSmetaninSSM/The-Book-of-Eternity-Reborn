using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Scans global system mods, persists enabled-file selection through GameSettings,
/// and writes a client-authored manifest for the GM.
/// </summary>
public sealed class SystemModService
{
    public const string ModsDirectory = "mods";
    public const string ManifestPath = "game_state/core/system_mods.json";

    private static readonly string[] SupportedExtensions = { ".json", ".txt", ".md" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly ILogger<SystemModService> _logger;

    public sealed record SystemModDescriptor
    {
        public string FileName { get; init; } = "";
        public string RelativePath { get; init; } = "";
        public string ModId { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Extension { get; init; } = "";
        public bool IsJson { get; init; }
        public bool Enabled { get; init; }
        public string? Content { get; init; }
        public string? LastModifiedUtc { get; init; }
    }

    public SystemModService(
        FileSystemManager fs,
        GameSettings settings,
        ILogger<SystemModService> logger)
    {
        _fs = fs;
        _settings = settings;
        _logger = logger;
    }

    public string GetModsDirectoryPath() => _fs.ResolvePath(ModsDirectory);

    public async Task<List<SystemModDescriptor>> GetAvailableModsAsync(bool includeContent = false)
    {
        var modsDir = GetModsDirectoryPath();
        Directory.CreateDirectory(modsDir);

        var enabled = new HashSet<string>(_settings.EnabledSystemMods, StringComparer.OrdinalIgnoreCase);
        var files = Directory
            .EnumerateFiles(modsDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<SystemModDescriptor>(files.Count);
        foreach (var file in files)
            result.Add(await BuildDescriptorAsync(file, includeContent, enabled.Contains(Path.GetFileName(file))));

        return result;
    }

    public async Task<bool> WriteManifestForGmAsync()
    {
        var mods = await GetAvailableModsAsync(includeContent: true);
        var normalizedEnabled = mods
            .Where(mod => mod.Enabled)
            .Select(mod => mod.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var changed = !_settings.EnabledSystemMods.SequenceEqual(normalizedEnabled, StringComparer.OrdinalIgnoreCase);
        _settings.EnabledSystemMods = normalizedEnabled;

        var manifest = BuildManifestNode(mods, normalizedEnabled);
        var currentJson = await _fs.ReadFileAsync(ManifestPath);
        if (!SemanticallyMatchesExistingManifest(currentJson, manifest))
        {
            manifest["_lastUpdated"] = DateTime.UtcNow.ToString("o");
            await _fs.WriteFileAtomicAsync(ManifestPath, manifest.ToJsonString(JsonOpts));
        }

        return changed;
    }

    private static JsonObject BuildManifestNode(
        IReadOnlyCollection<SystemModDescriptor> mods,
        IReadOnlyList<string> normalizedEnabled)
    {
        var activeMods = new JsonArray();
        foreach (var mod in mods.Where(mod => mod.Enabled))
        {
            activeMods.Add(new JsonObject
            {
                ["fileName"] = mod.FileName,
                ["relativePath"] = mod.RelativePath,
                ["modId"] = mod.ModId,
                ["name"] = mod.Name,
                ["description"] = mod.Description,
                ["extension"] = mod.Extension,
                ["lastModifiedUtc"] = mod.LastModifiedUtc,
                ["content"] = mod.Content
            });
        }

        var availableMods = new JsonArray();
        foreach (var mod in mods)
        {
            availableMods.Add(new JsonObject
            {
                ["fileName"] = mod.FileName,
                ["relativePath"] = mod.RelativePath,
                ["modId"] = mod.ModId,
                ["name"] = mod.Name,
                ["description"] = mod.Description,
                ["extension"] = mod.Extension,
                ["lastModifiedUtc"] = mod.LastModifiedUtc,
                ["enabled"] = mod.Enabled
            });
        }

        var enabledMods = new JsonArray();
        foreach (var fileName in normalizedEnabled)
            enabledMods.Add(fileName);

        return new JsonObject
        {
            ["modsDirectory"] = ModsDirectory,
            ["activeCount"] = mods.Count(mod => mod.Enabled),
            ["totalCount"] = mods.Count,
            ["enabledSystemMods"] = enabledMods,
            ["activeMods"] = activeMods,
            ["availableMods"] = availableMods
        };
    }

    private static bool SemanticallyMatchesExistingManifest(string? currentJson, JsonObject expectedManifest)
    {
        if (string.IsNullOrWhiteSpace(currentJson))
            return false;

        try
        {
            if (JsonNode.Parse(currentJson) is not JsonObject currentManifest)
                return false;

            currentManifest.Remove("_lastUpdated");
            return JsonNode.DeepEquals(currentManifest, expectedManifest);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> BuildSystemReminderFragmentAsync()
    {
        var mods = await GetAvailableModsAsync(includeContent: false);
        var activeMods = mods.Where(mod => mod.Enabled).ToList();

        if (activeMods.Count == 0)
        {
            return @"SYSTEM MODS:
  - No active global system mods are enabled for this session.
  - Ignore files in game_session/mods unless they appear in game_state/core/system_mods.json.activeMods[].";
        }

        var lines = new List<string>
        {
            "SYSTEM MODS:",
            "  - Read game_state/core/system_mods.json before authoring the turn.",
            "  - Only activeMods[] are canonical; ignore disabled files in game_session/mods.",
            "  - Each active mod applies globally unless the mod text narrows its own scope.",
            "  - Active mods for this session:"
        };

        foreach (var mod in activeMods)
        {
            var line = $"    • {mod.Name} ({mod.FileName})";
            if (!string.IsNullOrWhiteSpace(mod.Description))
                line += $" — {mod.Description}";
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string GetStatusSummary(IReadOnlyCollection<SystemModDescriptor> mods)
    {
        var active = mods.Count(mod => mod.Enabled);
        return $"{active}/{mods.Count}";
    }

    private async Task<SystemModDescriptor> BuildDescriptorAsync(string fullPath, bool includeContent, bool enabled)
    {
        var fileName = Path.GetFileName(fullPath);
        var relativePath = $"{ModsDirectory}/{fileName}".Replace('\\', '/');
        var extension = Path.GetExtension(fullPath);
        var content = await File.ReadAllTextAsync(fullPath);

        var descriptor = new SystemModDescriptor
        {
            FileName = fileName,
            RelativePath = relativePath,
            Extension = extension,
            IsJson = string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase),
            Enabled = enabled,
            Content = includeContent ? content : null,
            LastModifiedUtc = File.GetLastWriteTimeUtc(fullPath).ToString("o")
        };

        if (descriptor.IsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var root = doc.RootElement;
                    var title = GetFirstNonEmptyString(root, "name", "title", "modName");
                    var description = GetFirstNonEmptyString(root, "description", "summary", "overview");
                    var modId = GetFirstNonEmptyString(root, "modId", "id");

                    return descriptor with
                    {
                        Name = string.IsNullOrWhiteSpace(title) ? FriendlyNameFromFile(fileName) : title,
                        Description = description,
                        ModId = string.IsNullOrWhiteSpace(modId) ? Slugify(Path.GetFileNameWithoutExtension(fileName)) : Slugify(modId)
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Не удалось распарсить JSON-мод {FileName}; используется fallback metadata", fileName);
            }
        }

        var nonEmptyLines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var heading = nonEmptyLines.Count > 0 ? SanitizeHeading(nonEmptyLines[0]) : FriendlyNameFromFile(fileName);
        var descriptionFallback = nonEmptyLines.Count > 1 ? SanitizeHeading(nonEmptyLines[1]) : "";

        return descriptor with
        {
            Name = string.IsNullOrWhiteSpace(heading) ? FriendlyNameFromFile(fileName) : heading,
            Description = descriptionFallback,
            ModId = Slugify(Path.GetFileNameWithoutExtension(fileName))
        };
    }

    private static string GetFirstNonEmptyString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString()?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return "";
    }

    private static string FriendlyNameFromFile(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(name))
            return fileName;

        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string SanitizeHeading(string line)
    {
        var trimmed = line.Trim();
        trimmed = trimmed.TrimStart('#', '-', '*', '>', ' ');
        return trimmed.Trim();
    }

    private static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var replaced = Regex.Replace(lower, @"[^a-z0-9]+", "-");
        return replaced.Trim('-');
    }
}
