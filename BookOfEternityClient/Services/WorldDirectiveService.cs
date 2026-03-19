using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Manages reusable world profiles, pending pre-incarnation world setup,
/// and the active player-authored directive dossier for the current mortal world.
/// </summary>
public sealed class WorldDirectiveService
{
    public const string ProfilesDirectory = "world_profiles";
    public const string PendingSetupPath = "game_state/control/incarnation_world_setup.json";
    public const string ActiveDirectivesPath = "lore/current_world/world_directives.json";

    private static readonly string[] SupportedProfileExtensions = { ".json", ".txt", ".md" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<WorldDirectiveService> _logger;

    public sealed class WorldDirectives
    {
        [JsonPropertyName("worldTitle")]
        public string WorldTitle { get; set; } = "";

        [JsonPropertyName("genre")]
        public string Genre { get; set; } = "";

        [JsonPropertyName("era")]
        public string Era { get; set; } = "";

        [JsonPropertyName("tone")]
        public string Tone { get; set; } = "";

        [JsonPropertyName("settingSummary")]
        public string SettingSummary { get; set; } = "";

        [JsonPropertyName("detailedWorldDescription")]
        public string DetailedWorldDescription { get; set; } = "";

        [JsonPropertyName("hardRules")]
        public List<string> HardRules { get; set; } = new();

        [JsonPropertyName("requiredElements")]
        public List<string> RequiredElements { get; set; } = new();

        [JsonPropertyName("forbiddenElements")]
        public List<string> ForbiddenElements { get; set; } = new();

        [JsonPropertyName("specialMechanics")]
        public List<string> SpecialMechanics { get; set; } = new();

        [JsonPropertyName("continuityNotes")]
        public List<string> ContinuityNotes { get; set; } = new();

        [JsonPropertyName("playerAmendments")]
        public List<string> PlayerAmendments { get; set; } = new();

        [JsonPropertyName("sourceProfileId")]
        public string? SourceProfileId { get; set; }

        [JsonPropertyName("sourceProfileName")]
        public string? SourceProfileName { get; set; }

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingWorldSetup
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "manual";

        [JsonPropertyName("profileId")]
        public string? ProfileId { get; set; }

        [JsonPropertyName("profileName")]
        public string? ProfileName { get; set; }

        [JsonPropertyName("worldDirectives")]
        public WorldDirectives WorldDirectives { get; set; } = new();

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed record WorldProfileDescriptor
    {
        public string FileName { get; init; } = "";
        public string RelativePath { get; init; } = "";
        public string ProfileId { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Extension { get; init; } = "";
        public string? LastModifiedUtc { get; init; }
        public WorldDirectives Directives { get; init; } = new();
    }

    public WorldDirectiveService(FileSystemManager fs, ILogger<WorldDirectiveService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public string GetProfilesDirectoryPath() => _fs.ResolvePath(ProfilesDirectory);

    public async Task<List<WorldProfileDescriptor>> GetAvailableProfilesAsync()
    {
        var profilesDir = GetProfilesDirectoryPath();
        Directory.CreateDirectory(profilesDir);

        var files = Directory
            .EnumerateFiles(profilesDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedProfileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<WorldProfileDescriptor>(files.Count);
        foreach (var file in files)
            result.Add(await BuildProfileDescriptorAsync(file));

        return result;
    }

    public async Task<PendingWorldSetup?> ReadPendingSetupAsync()
    {
        var json = await _fs.ReadFileAsync(PendingSetupPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingWorldSetup>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать pending incarnation world setup");
            return null;
        }
    }

    public async Task<WorldDirectives?> ReadActiveWorldDirectivesAsync()
    {
        var json = await _fs.ReadFileAsync(ActiveDirectivesPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WorldDirectives>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать active world directives");
            return null;
        }
    }

    public async Task WritePendingSetupAsync(PendingWorldSetup setup)
    {
        setup.LastUpdated = DateTime.UtcNow.ToString("o");
        setup.WorldDirectives.LastUpdated = setup.LastUpdated;
        await _fs.WriteFileAtomicAsync(PendingSetupPath, JsonSerializer.Serialize(setup, JsonOpts));
    }

    public async Task WriteActiveWorldDirectivesAsync(WorldDirectives directives)
    {
        directives.LastUpdated = DateTime.UtcNow.ToString("o");
        await _fs.WriteFileAtomicAsync(ActiveDirectivesPath, JsonSerializer.Serialize(directives, JsonOpts));
    }

    public void ClearPendingSetup() => _fs.DeleteFile(PendingSetupPath);

    public PendingWorldSetup CreatePendingSetupFromProfile(WorldProfileDescriptor profile)
    {
        var directives = CloneDirectives(profile.Directives);
        directives.SourceProfileId = profile.ProfileId;
        directives.SourceProfileName = profile.Name;
        directives.LastUpdated = DateTime.UtcNow.ToString("o");

        return new PendingWorldSetup
        {
            Mode = "profile",
            ProfileId = profile.ProfileId,
            ProfileName = profile.Name,
            WorldDirectives = directives,
            LastUpdated = directives.LastUpdated
        };
    }

    public async Task UpsertPendingSetupFromIncarnationPromptAsync(string? worldDescription, string? circumstances)
    {
        if (string.IsNullOrWhiteSpace(worldDescription) && string.IsNullOrWhiteSpace(circumstances))
            return;

        var pending = await ReadPendingSetupAsync() ?? new PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectives()
        };

        if (string.IsNullOrWhiteSpace(pending.WorldDirectives.SettingSummary) &&
            !string.IsNullOrWhiteSpace(worldDescription))
        {
            pending.WorldDirectives.SettingSummary = worldDescription.Trim();
        }

        if (string.IsNullOrWhiteSpace(pending.WorldDirectives.DetailedWorldDescription) &&
            !string.IsNullOrWhiteSpace(worldDescription))
        {
            pending.WorldDirectives.DetailedWorldDescription = worldDescription.Trim();
        }

        if (!string.IsNullOrWhiteSpace(circumstances))
        {
            var note = $"Стартовые обстоятельства: {circumstances.Trim()}";
            if (!pending.WorldDirectives.ContinuityNotes.Contains(note, StringComparer.OrdinalIgnoreCase))
                pending.WorldDirectives.ContinuityNotes.Add(note);
        }

        if (pending.Mode == "profile")
            pending.Mode = "mixed";

        await WritePendingSetupAsync(pending);
    }

    public async Task MaterializePendingToActiveAsync(string? fallbackWorldDescription = null, string? fallbackCircumstances = null)
    {
        var pending = await ReadPendingSetupAsync();
        if (pending != null)
        {
            var directives = CloneDirectives(pending.WorldDirectives);
            directives.LastUpdated = DateTime.UtcNow.ToString("o");
            await WriteActiveWorldDirectivesAsync(directives);
            ClearPendingSetup();
            return;
        }

        if (string.IsNullOrWhiteSpace(fallbackWorldDescription) && string.IsNullOrWhiteSpace(fallbackCircumstances))
            return;

        var directivesFromPrompt = new WorldDirectives
        {
            SettingSummary = fallbackWorldDescription?.Trim() ?? "",
            DetailedWorldDescription = fallbackWorldDescription?.Trim() ?? "",
            ContinuityNotes = string.IsNullOrWhiteSpace(fallbackCircumstances)
                ? new List<string>()
                : new List<string> { $"Стартовые обстоятельства: {fallbackCircumstances.Trim()}" },
            LastUpdated = DateTime.UtcNow.ToString("o")
        };

        await WriteActiveWorldDirectivesAsync(directivesFromPrompt);
    }

    public string BuildReminderFragment(string? currentRealm, PendingWorldSetup? pendingSetup, WorldDirectives? activeDirectives)
    {
        var realm = currentRealm ?? "Chaos Sea";
        var isAfterlife = string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

        var parts = new List<string>();

        if (isAfterlife && pendingSetup != null)
        {
            parts.Add("WORLD SETUP:");
            parts.Add($"  - A client-authored pending setup exists at {PendingSetupPath}.");
            parts.Add("  - Read it when authoring TriggerIncarnation and the first Mortal World bootstrap.");
            parts.Add($"  - Mode: {pendingSetup.Mode}");
            if (!string.IsNullOrWhiteSpace(pendingSetup.ProfileName))
                parts.Add($"  - Source profile: {pendingSetup.ProfileName} ({pendingSetup.ProfileId})");
            AppendDirectiveSummary(parts, pendingSetup.WorldDirectives, indent: "  ");
        }

        if (!isAfterlife && activeDirectives != null)
        {
            parts.Add("WORLD DIRECTIVES:");
            parts.Add($"  - A persistent player-authored world dossier exists at {ActiveDirectivesPath}.");
            parts.Add("  - Read it every Mortal World turn and do not silently ignore it on later turns.");
            AppendDirectiveSummary(parts, activeDirectives, indent: "  ");
        }

        return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private async Task<WorldProfileDescriptor> BuildProfileDescriptorAsync(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var relativePath = $"{ProfilesDirectory}/{fileName}".Replace('\\', '/');
        var extension = Path.GetExtension(fullPath);
        var content = await File.ReadAllTextAsync(fullPath);

        if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var root = doc.RootElement;
                    var profileId = GetString(root, "profileId", "") is { Length: > 0 } explicitId
                        ? explicitId
                        : Slugify(Path.GetFileNameWithoutExtension(fileName));
                    var name = GetString(root, "name", FriendlyNameFromFile(fileName));
                    var description = GetString(root, "description", "");
                    var directives = root.TryGetProperty("worldDirectives", out var wd) && wd.ValueKind == JsonValueKind.Object
                        ? DeserializeDirectives(wd)
                        : DeserializeDirectives(root);

                    return new WorldProfileDescriptor
                    {
                        FileName = fileName,
                        RelativePath = relativePath,
                        ProfileId = profileId,
                        Name = name,
                        Description = description,
                        Extension = extension,
                        LastModifiedUtc = File.GetLastWriteTimeUtc(fullPath).ToString("o"),
                        Directives = directives
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось распарсить world profile {FileName}", fileName);
            }
        }

        var lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        var nameFallback = lines.Count > 0 ? SanitizeHeading(lines[0]) : FriendlyNameFromFile(fileName);
        var descriptionFallback = lines.Count > 1 ? SanitizeHeading(lines[1]) : "";
        var directivesFallback = new WorldDirectives
        {
            WorldTitle = nameFallback,
            SettingSummary = descriptionFallback,
            DetailedWorldDescription = content.Trim()
        };

        return new WorldProfileDescriptor
        {
            FileName = fileName,
            RelativePath = relativePath,
            ProfileId = Slugify(Path.GetFileNameWithoutExtension(fileName)),
            Name = nameFallback,
            Description = descriptionFallback,
            Extension = extension,
            LastModifiedUtc = File.GetLastWriteTimeUtc(fullPath).ToString("o"),
            Directives = directivesFallback
        };
    }

    private static void AppendDirectiveSummary(List<string> lines, WorldDirectives directives, string indent)
    {
        if (!string.IsNullOrWhiteSpace(directives.WorldTitle))
            lines.Add($"{indent}- World title: {directives.WorldTitle}");
        if (!string.IsNullOrWhiteSpace(directives.Genre))
            lines.Add($"{indent}- Genre: {directives.Genre}");
        if (!string.IsNullOrWhiteSpace(directives.Era))
            lines.Add($"{indent}- Era: {directives.Era}");
        if (!string.IsNullOrWhiteSpace(directives.Tone))
            lines.Add($"{indent}- Tone: {directives.Tone}");
        if (!string.IsNullOrWhiteSpace(directives.SettingSummary))
            lines.Add($"{indent}- Summary: {Truncate(directives.SettingSummary, 220)}");
        if (!string.IsNullOrWhiteSpace(directives.DetailedWorldDescription))
            lines.Add($"{indent}- Detailed dossier: present ({directives.DetailedWorldDescription.Length} chars). Read full text from the file.");
        if (directives.HardRules.Count > 0)
            lines.Add($"{indent}- Hard rules: {string.Join("; ", directives.HardRules.Take(4))}{SuffixForRemainder(directives.HardRules.Count, 4)}");
        if (directives.RequiredElements.Count > 0)
            lines.Add($"{indent}- Required elements: {string.Join("; ", directives.RequiredElements.Take(4))}{SuffixForRemainder(directives.RequiredElements.Count, 4)}");
        if (directives.ForbiddenElements.Count > 0)
            lines.Add($"{indent}- Forbidden elements: {string.Join("; ", directives.ForbiddenElements.Take(4))}{SuffixForRemainder(directives.ForbiddenElements.Count, 4)}");
        if (directives.SpecialMechanics.Count > 0)
            lines.Add($"{indent}- Special mechanics: {string.Join("; ", directives.SpecialMechanics.Take(4))}{SuffixForRemainder(directives.SpecialMechanics.Count, 4)}");
    }

    private static string SuffixForRemainder(int total, int shown) => total > shown ? $" (+{total - shown} more)" : "";

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
            return value;
        return value[..max] + "...";
    }

    private static WorldDirectives DeserializeDirectives(JsonElement root)
    {
        try
        {
            var directives = JsonSerializer.Deserialize<WorldDirectives>(root.GetRawText(), JsonOpts);
            return directives ?? new WorldDirectives();
        }
        catch
        {
            return new WorldDirectives
            {
                WorldTitle = GetString(root, "worldTitle", GetString(root, "name", "")),
                Genre = GetString(root, "genre", ""),
                Era = GetString(root, "era", ""),
                Tone = GetString(root, "tone", ""),
                SettingSummary = GetString(root, "settingSummary", GetString(root, "description", "")),
                DetailedWorldDescription = GetString(root, "detailedWorldDescription", GetString(root, "worldDescription", ""))
            };
        }
    }

    public static WorldDirectives CloneDirectives(WorldDirectives source) => new()
    {
        WorldTitle = source.WorldTitle,
        Genre = source.Genre,
        Era = source.Era,
        Tone = source.Tone,
        SettingSummary = source.SettingSummary,
        DetailedWorldDescription = source.DetailedWorldDescription,
        HardRules = source.HardRules.ToList(),
        RequiredElements = source.RequiredElements.ToList(),
        ForbiddenElements = source.ForbiddenElements.ToList(),
        SpecialMechanics = source.SpecialMechanics.ToList(),
        ContinuityNotes = source.ContinuityNotes.ToList(),
        PlayerAmendments = source.PlayerAmendments.ToList(),
        SourceProfileId = source.SourceProfileId,
        SourceProfileName = source.SourceProfileName,
        LastUpdated = source.LastUpdated
    };

    private static string GetString(JsonElement root, string propertyName, string fallback)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString()?.Trim() ?? fallback;
        return fallback;
    }

    private static string FriendlyNameFromFile(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? fileName : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string SanitizeHeading(string line)
    {
        var trimmed = line.Trim();
        trimmed = trimmed.TrimStart('#', '-', '*', '>', ' ');
        return trimmed.Trim();
    }

    private static string Slugify(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }
}
