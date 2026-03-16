using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Persists the full game story as JSONL files — one entry per turn.
/// Stories survive realm/world transitions inside a run, but are cleared on a fresh New Game.
///
/// File structure:
///   stories/chaos_sea.jsonl          — all Chaos Sea turns (continuous across incarnations)
///   stories/shining_abode.jsonl      — ascended endgame free roleplay
///   stories/mortal_life_1.jsonl      — first mortal incarnation
///   stories/mortal_life_2.jsonl      — second mortal incarnation
///   stories/mortal_life_N.jsonl      — Nth mortal incarnation
/// </summary>
public class StoryService
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<StoryService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public StoryService(FileSystemManager fs, ILogger<StoryService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    /// <summary>
    /// Appends a turn entry to the appropriate story file.
    /// </summary>
    public async Task AppendTurnAsync(int turnNumber, string realm, int incarnation,
        string playerAction, string? narrative, string? location = null)
    {
        try
        {
            var path = GetStoryPath(realm, incarnation);
            var entry = new StoryEntry
            {
                Turn = turnNumber,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Realm = realm,
                Player = playerAction,
                Narrative = narrative ?? "",
                Location = location
            };

            var line = JsonSerializer.Serialize(entry, JsonOpts);
            var fullPath = _fs.ResolvePath(path);

            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.AppendAllTextAsync(fullPath, line + "\n", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append story entry for turn {Turn}", turnNumber);
        }
    }

    /// <summary>
    /// Appends a special marker entry (death, incarnation, transition).
    /// </summary>
    public async Task AppendMarkerAsync(string realm, int incarnation, string markerType, string description)
    {
        try
        {
            var path = GetStoryPath(realm, incarnation);
            var entry = new StoryEntry
            {
                Turn = -1,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Realm = realm,
                Player = $"[{markerType}]",
                Narrative = description
            };

            var line = JsonSerializer.Serialize(entry, JsonOpts);
            var fullPath = _fs.ResolvePath(path);

            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.AppendAllTextAsync(fullPath, line + "\n", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append story marker: {Type}", markerType);
        }
    }

    /// <summary>
    /// Returns the relative path for the story file based on realm and incarnation.
    /// </summary>
    public static string GetStoryPath(string realm, int incarnation)
    {
        if (string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(realm))
        {
            return "stories/chaos_sea.jsonl";
        }

        if (string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "stories/shining_abode.jsonl";
        }

        var num = incarnation > 0 ? incarnation : 1;
        return $"stories/mortal_life_{num}.jsonl";
    }

    /// <summary>
    /// Lists all available story files with basic info.
    /// </summary>
    public List<StoryFileInfo> GetAvailableStories()
    {
        var result = new List<StoryFileInfo>();
        var storiesDir = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesDir)) return result;

        foreach (var file in Directory.GetFiles(storiesDir, "*.jsonl").OrderBy(f => f))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var lineCount = 0;
            try { lineCount = File.ReadLines(file, Encoding.UTF8).Count(); } catch { }

            var displayName = name switch
            {
                "chaos_sea" => "Море Хаоса (загробная жизнь)",
                "shining_abode" => "Сияющая Обитель (вознесение)",
                _ when name.StartsWith("mortal_life_") =>
                    $"Смертная жизнь #{name.Replace("mortal_life_", "")}",
                _ => name
            };

            result.Add(new StoryFileInfo
            {
                FileName = Path.GetFileName(file),
                RelativePath = $"stories/{Path.GetFileName(file)}",
                DisplayName = displayName,
                EntryCount = lineCount
            });
        }

        return result;
    }

    /// <summary>
    /// Reads story entries from a file. Optionally only the last N entries.
    /// </summary>
    public async Task<List<StoryEntry>> ReadStoryAsync(string relativePath, int? lastN = null)
    {
        var entries = new List<StoryEntry>();
        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath)) return entries;

        try
        {
            var lines = await File.ReadAllLinesAsync(fullPath, Encoding.UTF8);
            var startIdx = lastN.HasValue ? Math.Max(0, lines.Length - lastN.Value) : 0;

            for (var i = startIdx; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<StoryEntry>(lines[i], new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (entry != null) entries.Add(entry);
                }
                catch { /* skip malformed lines */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read story: {Path}", relativePath);
        }

        return entries;
    }

    /// <summary>
    /// Returns a summary of recent story for the GM system reminder.
    /// Includes paths to all story files so GM knows they exist.
    /// </summary>
    public string BuildStoryContext()
    {
        var stories = GetAvailableStories();
        if (stories.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("STORY FILES (full conversation history, JSONL format — one JSON object per line):");
        foreach (var s in stories)
            sb.AppendLine($"  {s.RelativePath} — {s.DisplayName} ({s.EntryCount} entries)");
        sb.AppendLine("The GM can read these files for narrative continuity and character history.");
        return sb.ToString();
    }
}

public class StoryEntry
{
    [JsonPropertyName("turn")]
    public int Turn { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("realm")]
    public string? Realm { get; set; }

    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = "";

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

public class StoryFileInfo
{
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int EntryCount { get; set; }
}
