using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models.GameState;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

/// <summary>
/// Central game state manager. Loads aggregated state from files,
/// manages settings, and coordinates between subsystems.
/// </summary>
public class StateManager
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<StateManager> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AggregatedGameState CurrentState { get; private set; } = new();
    public GameSettings Settings { get; }

    public StateManager(FileSystemManager fs, GameSettings settings, ILogger<StateManager> logger)
    {
        _fs = fs;
        Settings = settings;
        _logger = logger;
    }

    public async Task LoadSettingsAsync()
    {
        var json = await _fs.ReadFileAsync("config.json");
        if (json != null)
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<GameSettings>(json, JsonOpts);
                if (loaded != null)
                {
                    Settings.Language = loaded.Language;
                    Settings.AllowHistoryManipulation = loaded.AllowHistoryManipulation;
                    Settings.ShowGmThoughts = loaded.ShowGmThoughts;
                    Settings.ShowImagesInConsole = loaded.ShowImagesInConsole;
                    Settings.ImageProvider = loaded.ImageProvider;
                    Settings.ImageWidth = loaded.ImageWidth;
                    Settings.ImageHeight = loaded.ImageHeight;
                    Settings.PollinationsApiKey = loaded.PollinationsApiKey;
                    Settings.PollinationsImageModel = loaded.PollinationsImageModel;
                    Settings.OpenRouterApiKey = loaded.OpenRouterApiKey;
                    Settings.OpenRouterImageModel = loaded.OpenRouterImageModel;
                    Settings.GameSessionPath = loaded.GameSessionPath;
                    Settings.AutosaveIntervalTurns = loaded.AutosaveIntervalTurns;
                    Settings.MaxAutosaves = loaded.MaxAutosaves;
                    Settings.MaxManualSaves = loaded.MaxManualSaves;
                    Settings.GmTimeoutSeconds = loaded.GmTimeoutSeconds;
                    Settings.GmBridgeEnabled = loaded.GmBridgeEnabled;
                    Settings.GmBridgeBackend = loaded.GmBridgeBackend;
                    Settings.GmCliLaunchCommand = loaded.GmCliLaunchCommand;
                    Settings.GmBridgeAutoStart = loaded.GmBridgeAutoStart;
                    Settings.GmBridgePipeNameOverride = loaded.GmBridgePipeNameOverride;
                    Settings.GameVersion = loaded.GameVersion;
                    Settings.Difficulty = loaded.Difficulty;
                    Settings.AutoDiscardBrokenItems = loaded.AutoDiscardBrokenItems;
                    Settings.GenerateSceneImages = loaded.GenerateSceneImages;
                    Settings.GenerateImagesWithoutDisplay = loaded.GenerateImagesWithoutDisplay;
                    Settings.EnableQteEvents = loaded.EnableQteEvents;
                    Settings.MusicEnabled = loaded.MusicEnabled;
                    Settings.MusicVolume = Math.Clamp(loaded.MusicVolume, 0, 100);
                    Settings.SoundEnabled = loaded.SoundEnabled;
                    Settings.SoundVolume = Math.Clamp(loaded.SoundVolume, 0, 100);
                    Settings.ConsoleFontSize = loaded.ConsoleFontSize > 0
                        ? Math.Clamp(loaded.ConsoleFontSize, 14, 32)
                        : Settings.ConsoleFontSize;
                    Settings.EnabledSystemMods = loaded.EnabledSystemMods?
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? new List<string>();
                }
            }
            catch
            {
                // Keep existing defaults on parse failure
            }
        }
    }

    public async Task EnsureSettingsFileExistsAsync()
    {
        if (_fs.FileExists("config.json"))
            return;

        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        var json = JsonSerializer.Serialize(Settings, JsonOpts);
        await _fs.WriteFileAtomicAsync("config.json", json);
    }

    /// <summary>
    /// Load aggregated game state from all game_state/ files for UI display.
    /// </summary>
    public async Task RefreshGameStateAsync()
    {
        var state = new AggregatedGameState
        {
            CurrentRealm = CurrentState.CurrentRealm
        };

        // Core: Player status
        await TryLoadJson("game_state/core/player_status.json", (doc) =>
        {
            var root = doc.RootElement;
            state.PlayerStatus = new PlayerStatusState
            {
                HealthPercentage = GetString(root, "healthPercentage", "100%"),
                EnergyPercentage = GetString(root, "energyPercentage", "100%"),
                PoisePercentage = GetString(root, "poisePercentage", "100%"),
                CurrentCondition = GetString(root, "currentCondition", "Здоров"),
                ActiveConditions = GetStringArray(root, "activeConditions")
            };
        });

        // Core: Narrative
        await TryLoadJson("output/narrative_response.json", (doc) =>
        {
            state.Narrative = GetString(doc.RootElement, "response", "");
        });

        // Core: GM Debug
        await TryLoadJson("output/debug_logs.json", (doc) =>
        {
            state.GmDebug = GetString(doc.RootElement, "gm_thoughts_markdown", "");
        });

        // World: Location
        await TryLoadJson("game_state/world/current_location.json", (doc) =>
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("currentLocationData", out var locationData) &&
                locationData.ValueKind == JsonValueKind.Object)
                root = locationData;

            state.CurrentLocation = GetString(root, "name", "Неизвестно");
        });

        // World: Time
        await TryLoadJson("game_state/world/world_time.json", (doc) =>
        {
            state.WorldTime = FormatWorldTime(doc.RootElement);
        });

        // Player: Transformation (name, class, race)
        await TryLoadJson("game_state/player/transformation.json", (doc) =>
        {
            var root = doc.RootElement;
            state.CharacterName = GetString(root, "playerCharacterNameChange", state.CharacterName);
            state.CharacterClass = GetString(root, "playerClassChange", state.CharacterClass);
            state.CharacterRace = GetString(root, "playerRaceChange", state.CharacterRace);
        });

        // Meta: Soul state
        await TryLoadJson("game_state/meta/soul_state.json", (doc) =>
        {
            var root = doc.RootElement;
            state.SoulName = GetString(root, "soulName", "");
            state.CurrentRealm = GetString(root, "currentRealm", state.CurrentRealm);
            if (root.TryGetProperty("currentIncarnation", out var inc))
                state.Incarnation = inc.GetInt32();
            if (root.TryGetProperty("inkFeathers", out var feathers) &&
                feathers.TryGetProperty("current", out var current))
                state.InkFeathers = current.GetInt32();
            if (root.TryGetProperty("enlightenment", out var enl) &&
                enl.TryGetProperty("currentTier", out var tier))
                state.EnlightenmentTier = tier.GetString() ?? "Новичок";
        });

        // Meta: Guardians (active guardian name)
        await TryLoadJson("game_state/meta/guardians.json", (doc) =>
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("activeGuardian", out var ag) &&
                    ag.TryGetProperty("name", out var name))
                    state.ActiveGuardianName = name.GetString() ?? "";
            }
        });

        // History: Chat log for turn number
        await TryLoadJson("game_state/history/chat_log.json", (doc) =>
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("sessionId", out var sid))
                state.SessionId = sid.GetString() ?? "";
        });

        state.LastUpdated = DateTime.UtcNow;
        CurrentState = state;
    }

    /// <summary>
    /// Load a raw JSON file from game_state for explorer mode display.
    /// </summary>
    public async Task<JsonDocument?> LoadGameStateFileAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось разобрать {Path}", relativePath);
            return null;
        }
    }

    private async Task TryLoadJson(string path, Action<JsonDocument> handler)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            handler(doc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения {Path}", path);
        }
    }

    private static string GetString(JsonElement el, string prop, string def)
    {
        if (el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString() ?? def;
        return def;
    }

    private static string[] GetStringArray(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in val.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                    list.Add(item.GetString() ?? "");
            return list.ToArray();
        }
        return Array.Empty<string>();
    }

    private static string FormatWorldTime(JsonElement root)
    {
        if (TryFormatAbsoluteWorldTime(root, out var absolute))
            return absolute;

        if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
            TryFormatAbsoluteWorldTime(setWorldTime, out absolute))
            return absolute;

        if (TryGetIntLike(root, "timeChange", out var deltaMinutes) && deltaMinutes != 0)
            return $"Прошло {deltaMinutes} мин. за ход";

        return "";
    }

    private static bool TryFormatAbsoluteWorldTime(JsonElement source, out string formatted)
    {
        formatted = "";
        if (source.ValueKind != JsonValueKind.Object)
            return false;

        var year = GetString(source, "year", "");
        var month = GetString(source, "monthName", "");
        var day = GetString(source, "dayOfMonth", "");
        var tod = GetString(source, "timeOfDay", "");

        if (string.IsNullOrWhiteSpace(year) &&
            string.IsNullOrWhiteSpace(month) &&
            string.IsNullOrWhiteSpace(day) &&
            string.IsNullOrWhiteSpace(tod))
            return false;

        var datePart = string.Join(" ", new[] { day, month, year }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (!string.IsNullOrWhiteSpace(datePart) && !string.IsNullOrWhiteSpace(tod))
            formatted = $"{datePart}, {tod}";
        else
            formatted = !string.IsNullOrWhiteSpace(datePart) ? datePart : tod;

        return !string.IsNullOrWhiteSpace(formatted);
    }

    private static bool TryGetIntLike(JsonElement root, string prop, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(prop, out var field))
            return false;

        if (field.ValueKind == JsonValueKind.Number)
            return field.TryGetInt32(out value);

        return field.ValueKind == JsonValueKind.String &&
               int.TryParse(field.GetString(), out value);
    }
}
