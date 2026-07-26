using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

internal sealed class StateManagerHooks
{
    internal Func<Task>? AfterPlayerSoulProfileInputsReadAsync { get; init; }
}

/// <summary>
/// Central game state manager. Loads aggregated state from files,
/// manages settings, and coordinates between subsystems.
/// </summary>
public class StateManager
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<StateManager> _logger;
    private readonly StateManagerHooks? _hooks;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public AggregatedGameState CurrentState { get; private set; } = new();
    public GameSettings Settings { get; }

    public StateManager(FileSystemManager fs, GameSettings settings, ILogger<StateManager> logger)
        : this(fs, settings, logger, hooks: null)
    {
    }

    internal StateManager(
        FileSystemManager fs,
        GameSettings settings,
        ILogger<StateManager> logger,
        StateManagerHooks? hooks)
    {
        _fs = fs;
        Settings = settings;
        _logger = logger;
        _hooks = hooks;
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
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            await RepairClientOwnedProfileMirrorsAsync(writeLease);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось синхронизировать клиентские зеркала профилей перед refresh.");
        }
        await RefreshGameStateCoreAsync();
    }

    internal async Task RefreshGameStateAsync(FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        await RepairClientOwnedProfileMirrorsAsync(writeLease);
        await RefreshGameStateCoreAsync();
    }

    private async Task RefreshGameStateCoreAsync()
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
                CurrentConditionDescription = GetString(root, "currentConditionDescription", ""),
                ActiveConditions = GetStringArray(root, "activeConditions")
            };
            state.CharacterName = GetString(root, "characterName", state.CharacterName);
            state.CharacterClass = GetString(root, "characterClass", state.CharacterClass);
            state.CharacterRace = GetString(root, "characterRace", state.CharacterRace);
        });

        // Core: Narrative
        await TryLoadJson("output/narrative_response.json", (doc) =>
        {
            state.Narrative = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(
                GetString(doc.RootElement, "response", "")) ?? "";
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
            state.SoulFormDescription = GetString(root, "soulFormDescription", "");
            state.CurrentRealm = GetString(root, "currentRealm", "");
            if (root.TryGetProperty("currentIncarnation", out var inc))
                state.Incarnation = inc.GetInt32();
            if (root.TryGetProperty("inkFeathers", out var feathers))
            {
                if (feathers.ValueKind == JsonValueKind.Number &&
                    feathers.TryGetInt32(out var flatFeathers))
                {
                    state.InkFeathers = flatFeathers;
                }
                else if (feathers.ValueKind == JsonValueKind.Object &&
                         feathers.TryGetProperty("current", out var current) &&
                         current.ValueKind == JsonValueKind.Number &&
                         current.TryGetInt32(out var currentFeathers))
                {
                    state.InkFeathers = currentFeathers;
                }
            }
            if (root.TryGetProperty("enlightenment", out var enl) &&
                enl.TryGetProperty("currentTier", out var tier))
                state.EnlightenmentTier = tier.GetString() ?? "Новичок";
        });

        await TryLoadMortalIncarnationIdentityFallbackAsync(state);

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
            {
                var packageMode = ShiningAbodeState.PreparedIncarnationPackageMode.Absent;
                if (pkg.ValueKind == JsonValueKind.Object &&
                    JsonNode.Parse(root.GetRawText()) is JsonObject shiningRoot)
                {
                    packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
                }
                else if (pkg.ValueKind != JsonValueKind.Null)
                {
                    packageMode = ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault;
                }

                state.HasPendingShiningAbodeBootstrapPackage = packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff;
                state.HasInvalidShiningAbodeBootstrapPackage = packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault;
            }
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
                    ag.ValueKind == JsonValueKind.Object)
                    state.ActiveGuardianName = GuardianManifestation.GetDisplayName(ag);
            }
        });

        // History: Chat log for turn number
        await TryLoadJson("game_state/history/chat_log.json", (doc) =>
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("sessionId", out var sid))
                state.SessionId = sid.GetString() ?? "";
        });

        state.TurnNumber = await DetectCurrentSessionTurnNumberAsync();

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

    private async Task<int> DetectCurrentSessionTurnNumberAsync()
    {
        var storiesPath = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesPath))
            return 0;

        var maxTurn = 0;
        foreach (var file in Directory.EnumerateFiles(storiesPath, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                maxTurn = Math.Max(maxTurn, string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase)
                    ? await ReadMaxTurnFromJsonLinesStoryAsync(file)
                    : await ReadMaxTurnFromJsonStoryAsync(file));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Не удалось разобрать story history файл {Path}", file);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Не удалось прочитать story history файл {Path}", file);
            }
        }

        return maxTurn;
    }

    private static async Task<int> ReadMaxTurnFromJsonStoryAsync(string file)
    {
        var json = await File.ReadAllTextAsync(file);
        var root = JsonNode.Parse(json);
        var maxTurn = 0;
        if (root is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                maxTurn = Math.Max(maxTurn, GetJsonObjectInt(item, "turn", "turnNumber") ?? 0);
        }
        else if (root is JsonObject obj)
        {
            maxTurn = Math.Max(maxTurn, GetJsonObjectInt(obj, "turn", "turnNumber") ?? 0);
        }

        return maxTurn;
    }

    private static async Task<int> ReadMaxTurnFromJsonLinesStoryAsync(string file)
    {
        var maxTurn = 0;
        foreach (var line in await File.ReadAllLinesAsync(file))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (JsonNode.Parse(line) is JsonObject obj)
                maxTurn = Math.Max(maxTurn, GetJsonObjectInt(obj, "turn", "turnNumber") ?? 0);
        }

        return maxTurn;
    }

    private static int? GetJsonObjectInt(JsonObject root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                try
                {
                    return value.GetValue<int>();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return null;
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

    private async Task TryLoadMortalIncarnationIdentityFallbackAsync(AggregatedGameState state)
    {
        if (state.IsInAfterlifeRealm || !string.IsNullOrWhiteSpace(state.CharacterName))
            return;

        await TryLoadJson("game_state/control/next_life_scenario_core.json", doc =>
        {
            if (!doc.RootElement.TryGetProperty("scenarioCoreAssertions", out var assertions) ||
                assertions.ValueKind != JsonValueKind.Array)
                return;

            foreach (var assertion in assertions.EnumerateArray())
            {
                if (assertion.ValueKind != JsonValueKind.Object)
                    continue;

                var category = GetString(assertion, "category", "");
                if (!string.Equals(category, "identity_anchor", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = GetString(assertion, "value", "");
                var identityName = ExtractMortalIdentityName(value);
                if (string.IsNullOrWhiteSpace(identityName))
                    continue;

                state.CharacterName = identityName;
                return;
            }
        });
    }

    private static string ExtractMortalIdentityName(string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var boundary = value.IndexOfAny([',', ':', ';', '.', '\r', '\n']);
        var candidate = boundary > 0 ? value[..boundary] : value;
        candidate = candidate.Trim(' ', '—', '-', '–');
        if (candidate.Length <= 80)
            return candidate;

        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 4 ? candidate : string.Join(' ', words.Take(4));
    }

    private Task RepairClientOwnedProfileMirrorsAsync(FileSystemManager.CanonicalWriteLease writeLease) =>
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthorityAsync(
            _fs,
            writeLease,
            _hooks?.AfterPlayerSoulProfileInputsReadAsync);

    internal RuntimeSnapshot CaptureRuntimeSnapshot()
    {
        var settingsJson = JsonSerializer.Serialize(Settings, JsonOpts);
        var settingsCopy = JsonSerializer.Deserialize<GameSettings>(settingsJson, JsonOpts) ?? new GameSettings();
        return new RuntimeSnapshot(CurrentState, settingsCopy);
    }

    internal void RestoreRuntimeSnapshot(RuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CurrentState = snapshot.State;
        Settings.ApplyLoadedValues(snapshot.Settings);
    }

    internal sealed record RuntimeSnapshot(AggregatedGameState State, GameSettings Settings);

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
