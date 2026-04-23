using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.Services;
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

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

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
        if (json == null)
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<GameSettings>(json, JsonOpts);
            if (loaded != null)
            {
                Settings.ApplyLoadedValues(loaded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось загрузить config.json. Сохраняются текущие defaults.");
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
        var state = new AggregatedGameState();

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
            state.CurrentRealm = GetString(root, "currentRealm", "");
            if (root.TryGetProperty("currentIncarnation", out var inc))
                state.Incarnation = inc.GetInt32();
            if (root.TryGetProperty("inkFeathers", out var feathers) &&
                feathers.TryGetProperty("current", out var current))
                state.InkFeathers = current.GetInt32();
            if (root.TryGetProperty("enlightenment", out var enl) &&
                enl.TryGetProperty("currentTier", out var tier))
                state.EnlightenmentTier = tier.GetString() ?? "Новичок";
        });

        // Meta: Shining Abode lifecycle handoff
        await TryLoadJson("game_state/meta/shining_abode_state.json", (doc) =>
        {
            var root = doc.RootElement;
            state.ShiningAbodeAvailability = GetString(root, "availability", "");
            if (root.TryGetProperty("radiance", out var radiance) &&
                radiance.ValueKind == JsonValueKind.Object)
            {
                if (radiance.TryGetProperty("experience", out var experience) &&
                    experience.ValueKind == JsonValueKind.Number &&
                    experience.TryGetInt32(out var parsedExperience))
                {
                    state.ShiningRadianceExperience = parsedExperience;
                }

                if (radiance.TryGetProperty("tier", out var tier) &&
                    tier.ValueKind == JsonValueKind.Number &&
                    tier.TryGetInt32(out var parsedTier))
                {
                    state.ShiningRadianceTier = parsedTier;
                }
            }

            if (root.TryGetProperty("lightSparks", out var lightSparks) &&
                lightSparks.ValueKind == JsonValueKind.Number &&
                lightSparks.TryGetInt32(out var parsedLightSparks))
            {
                state.ShiningLightSparks = parsedLightSparks;
            }

            if (root.TryGetProperty("halls", out var halls) && halls.ValueKind == JsonValueKind.Array)
                state.ShiningHallCount = halls.GetArrayLength();
            if (root.TryGetProperty("factions", out var factions) && factions.ValueKind == JsonValueKind.Array)
                state.ShiningFactionCount = factions.GetArrayLength();
            if (root.TryGetProperty("gates", out var gates) && gates.ValueKind == JsonValueKind.Object)
            {
                if (gates.TryGetProperty("hasOpenDraft", out var hasOpenDraft) &&
                    (hasOpenDraft.ValueKind == JsonValueKind.True || hasOpenDraft.ValueKind == JsonValueKind.False))
                {
                    state.HasOpenShiningGatesDraft = hasOpenDraft.GetBoolean();
                }

                if (gates.TryGetProperty("isStale", out var isStale) &&
                    (isStale.ValueKind == JsonValueKind.True || isStale.ValueKind == JsonValueKind.False))
                {
                    state.IsShiningGatesDraftStale = isStale.GetBoolean();
                }
            }

            if (root.TryGetProperty("preparedIncarnationPackage", out var pkg))
                state.HasPendingShiningAbodeBootstrapPackage = pkg.ValueKind != JsonValueKind.Null;
        });

        // Control: Post-life guard for the first ordinary afterlife turn.
        // A malformed or semantically invalid guard still blocks re-entry until runtime normalization clears it.
        var rawReturnGuard = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
        var guardSemanticState = AfterlifeReturnGuardService.Classify(rawReturnGuard, out _);
        state.HasBlockingAfterlifeReturnGuard =
            guardSemanticState is AfterlifeReturnGuardSemanticState.ActiveValid or
            AfterlifeReturnGuardSemanticState.BlockingInvalid;

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
