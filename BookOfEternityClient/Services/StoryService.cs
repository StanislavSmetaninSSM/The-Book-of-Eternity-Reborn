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
        string playerAction, string? narrative, string? location = null, IReadOnlyCollection<StoryEntityRef>? entityRefs = null)
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
                Location = location,
                EntityRefs = entityRefs?.Where(reference => reference != null).ToList()
            };

            var line = JsonSerializer.Serialize(entry, JsonOpts);
            await _fs.AppendFileAtomicAsync(path, line + "\n");
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogWarning(ex, "Failed to append story entry for turn {Turn}", turnNumber);
        }
    }

    internal async Task<PendingPlayerActionService.StoryProof> AppendOriginalBrowserTurnAsync(
        FileSystemManager.CanonicalWriteLease lease, PendingPlayerActionService.Binding binding,
        int turnNumber, string realm, int incarnation, string? narrative, string? location,
        IReadOnlyCollection<StoryEntityRef>? entityRefs)
    {
        _fs.VerifyCurrentSessionOperation(lease);
        if (_fs.GetOrCreateSessionGeneration(lease) != binding.Generation)
            throw new SessionReplacedException("Сессия браузерного хода изменилась.", binding.Generation,
                _fs.GetOrCreateSessionGeneration(lease));
        var path = GetStoryPath(realm, incarnation);
        var before = await _fs.ReadFileBytesAsync(lease, path) ?? Encoding.UTF8.GetPreamble();
        var previousText = Encoding.UTF8.GetString(before).TrimStart('\uFEFF');
        foreach (var line in previousText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var previous = StrictJsonAuthority.Deserialize<StoryEntry>(line, JsonOpts, "story entry")!;
            if (previous.RequestId == binding.ActionId && previous.SessionGeneration == binding.Generation)
                throw new InvalidDataException("Исходный браузерный ход уже записан; повтор остановлен.");
        }
        var row = JsonSerializer.Serialize(new StoryEntry
        {
            Turn = turnNumber, Timestamp = DateTime.UtcNow.ToString("o"), Realm = realm,
            Player = binding.Action, Narrative = narrative ?? "", Location = location,
            EntityRefs = entityRefs?.ToList(), RequestId = binding.ActionId,
            SessionGeneration = binding.Generation
        }, JsonOpts);
        await _fs.AppendFileAtomicAsync(lease, path, row + "\n");
        var actual = await _fs.ReadFileBytesAsync(lease, path)
            ?? throw new IOException("Accepted story readback is absent.");
        var expected = before.Concat(Encoding.UTF8.GetBytes(row + "\n")).ToArray();
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new IOException("Accepted story readback did not preserve the original prefix and exact row.");
        return new(path, actual.Length, PendingPlayerActionService.Hash(actual), row);
    }

    /// <summary>
    /// Appends a special marker entry (death, incarnation, transition).
    /// </summary>
    public async Task AppendMarkerAsync(string realm, int incarnation, string markerType, string description, IReadOnlyCollection<StoryEntityRef>? entityRefs = null)
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
                Narrative = description,
                EntityRefs = entityRefs?.Where(reference => reference != null).ToList()
            };

            var line = JsonSerializer.Serialize(entry, JsonOpts);
            await _fs.AppendFileAtomicAsync(path, line + "\n");
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
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
    public List<StoryFileInfo> GetAvailableStories() => GetAvailableStoriesAsync().GetAwaiter().GetResult();

    private async Task<List<StoryFileInfo>> GetAvailableStoriesAsync()
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync().ConfigureAwait(false);
        _fs.VerifyCurrentSessionOperation(lease);
        var result = new List<StoryFileInfo>();
        var storiesDir = _fs.ResolvePath("stories");
        var scope = new TrustedLocalFileScope([storiesDir]);
        if (!Directory.Exists(storiesDir)) return result;

        foreach (var file in Directory.GetFiles(storiesDir, "*.jsonl").OrderBy(f => f))
        {
            scope.ValidateFile(file, allowMissing: false);
            var relativePath = $"stories/{Path.GetFileName(file)}";
            var name = Path.GetFileNameWithoutExtension(file);
            var lineCount = 0;
            try
            {
                lineCount = (await ReadStoryLinesAsync(lease, relativePath).ConfigureAwait(false)).Count;
            }
            catch (Exception ex) when (IsOrdinaryReadFailure(ex))
            {
                _logger.LogDebug(ex, "Не удалось подсчитать количество записей story file {StoryFile}.", file);
            }

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
                RelativePath = relativePath,
                DisplayName = displayName,
                EntryCount = lineCount
            });
        }

        _fs.VerifyCurrentSessionOperation(lease);
        return result;
    }

    /// <summary>
    /// Reads story entries from a file. Optionally only the last N entries.
    /// </summary>
    public async Task<List<StoryEntry>> ReadStoryAsync(string relativePath, int? lastN = null)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync().ConfigureAwait(false);
        return await ReadStoryAsync(lease, relativePath, lastN).ConfigureAwait(false);
    }

    internal async Task<List<StoryEntry>> ReadStoryAsync(
        FileSystemManager.CanonicalWriteLease lease, string relativePath, int? lastN = null)
    {
        _fs.VerifyCurrentSessionOperation(lease);
        var fullPath = _fs.ResolvePath(relativePath);
        new TrustedLocalFileScope([_fs.GameSessionPath]).ValidateFile(fullPath);
        var entries = new List<StoryEntry>();
        try
        {
            var lines = await ReadStoryLinesAsync(lease, relativePath).ConfigureAwait(false);
            var startIdx = lastN.HasValue ? Math.Max(0, lines.Count - lastN.Value) : 0;

            for (var i = startIdx; i < lines.Count; i++)
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
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Пропускается повреждённая строка story file {StoryPath} at line {LineIndex}.", relativePath, i);
                }
            }
        }
        catch (Exception ex) when (IsOrdinaryReadFailure(ex))
        {
            _logger.LogWarning(ex, "Failed to read story: {Path}", relativePath);
        }

        _fs.VerifyCurrentSessionOperation(lease);
        return entries;
    }

    private async Task<List<string>> ReadStoryLinesAsync(
        FileSystemManager.CanonicalWriteLease lease, string relativePath)
    {
        var bytes = await _fs.ReadLocalFileBytesAsync(lease, relativePath).ConfigureAwait(false);
        if (bytes == null) return [];
        using var reader = new StringReader(LocalSettingsPreparation.DecodeText(bytes));
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    private static bool IsOrdinaryReadFailure(Exception failure) =>
        failure is (IOException or UnauthorizedAccessException) &&
        failure is not (InvalidDataException or CoordinatedStatePublicationUncertainException or SessionReplacedException);

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
        sb.AppendLine("Curated actor memory also lives in npc_journals, npc_interaction_journal, guardian_thought_journal, guardian_social_journal, and guardian_abode_residents thought/history/interaction logs.");
        sb.AppendLine("If exact details are needed, use Tools/Search-GmMemory.ps1 against the current game_session. The script supports -Source and -Json for narrower or machine-readable lookup.");
        sb.AppendLine("The search tool now covers stories, actor journals, guardian project/power journals, world events, faction chronicles, and character chronicle.");
        sb.AppendLine("Resident structured closures require curated memory updates. Guardian/NPC memory remains advisory for freeform scenes, but explicit guardian/NPC social request pathways must also close canonically through their event journals.");
        sb.AppendLine("The GM can read these files for narrative continuity and character history.");
        return sb.ToString();
    }
}

public class StoryEntry
{
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("sessionGeneration")]
    public string? SessionGeneration { get; set; }

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

    [JsonPropertyName("entityRefs")]
    public List<StoryEntityRef>? EntityRefs { get; set; }
}

public class StoryEntityRef
{
    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = "";

    [JsonPropertyName("entityId")]
    public string EntityId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public class StoryFileInfo
{
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int EntryCount { get; set; }
}
