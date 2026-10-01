using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserClientSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly AudioService _audioService;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly LocalizationManager _localization;

    public BrowserClientSettingsService(
        FileSystemManager fs,
        StateManager stateManager,
        AudioService audioService,
        BrowserLocalWriteCoordinator coordinator,
        LocalizationManager localization)
    {
        _fs = fs;
        _stateManager = stateManager;
        _audioService = audioService;
        _coordinator = coordinator;
        _localization = localization;
    }

    public async Task<BrowserClientSettingsDto> BuildAsync()
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            return await _coordinator.RunBoundTransactionAsync(
                async writeLease =>
                {
                    var snapshot = await _stateManager.ReadLocalSettingsAsync(writeLease);
                    _stateManager.Settings.ApplyLoadedValues(snapshot.Settings);
                    _localization.CurrentLanguage = NormalizeLanguage(snapshot.Settings.Language);
                    return BuildDto();
                });
        }
        finally
        {
            BrowserAudioService.SettingsWriteGate.Release();
        }
    }

    public async Task<BrowserClientSettingsUpdateResult> UpdateAsync(BrowserClientSettingsUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            BrowserClientSettingsDto? preparedResponse = null;
            var result = await _coordinator.ExecutePreparedAsync(
                new BrowserLocalWriteRequest(
                    OwnerId: $"browser-settings:{Environment.MachineName}:{Environment.ProcessId}",
                    OwnerLabel: "Browser Client settings",
                    OperationLabel: "Browser Client settings update"),
                async writeLease =>
                {
                    var snapshot = await _stateManager.ReadLocalSettingsAsync(writeLease);
                    var candidate = snapshot.Settings;
                    ApplyRequest(candidate, request);
                    var projectionBefore = await _fs.ReadLocalFileBytesAsync(writeLease, "game_state/core/game_settings.json");
                    var projectionAfter = request.Difficulty == null ? projectionBefore : BuildGmSettingsProjection(candidate);
                    preparedResponse = BuildDto(candidate);
                    return new PreparedBrowserLocalWrite(
                        [new("config.json", snapshot.Bytes, _stateManager.EncodeLocalSettings(candidate)),
                         new("game_state/core/game_settings.json", projectionBefore, projectionAfter)],
                        async () =>
                        {
                            _stateManager.Settings.ApplyLoadedValues(candidate);
                            _localization.CurrentLanguage = NormalizeLanguage(candidate.Language);
                            await _audioService.ApplySettingsAsync();
                        });
                });
            if (result.Disposition != BrowserPreparedWriteDisposition.Committed)
                return BrowserClientSettingsUpdateResult.NotCompleted(result.Disposition, result.Message);
            if (preparedResponse == null)
                return BrowserClientSettingsUpdateResult.NotCompleted(BrowserPreparedWriteDisposition.Committed,
                    "Настройки сохранены, но ответ не удалось подготовить. Обновите состояние книги.");
            return BrowserClientSettingsUpdateResult.Completed(preparedResponse with
            {
                PersistenceWarning = result.NeedsFollowUp ? result.Message : null
            });
        }
        finally
        {
            BrowserAudioService.SettingsWriteGate.Release();
        }
    }

    private static void ApplyRequest(GameSettings settings, BrowserClientSettingsUpdateRequest request)
    {

        if (request.Language is not null)
        {
            settings.Language = NormalizeLanguage(request.Language);
        }
        if (request.Difficulty is not null)
            settings.Difficulty = NormalizeDifficulty(request.Difficulty);
        if (request.ShowGmThoughts.HasValue)
            settings.ShowGmThoughts = request.ShowGmThoughts.Value;
        if (request.MusicEnabled.HasValue)
            settings.MusicEnabled = request.MusicEnabled.Value;
        if (request.MusicVolume.HasValue)
            settings.MusicVolume = Math.Clamp(request.MusicVolume.Value, 0, 100);
        if (request.SoundEnabled.HasValue)
            settings.SoundEnabled = request.SoundEnabled.Value;
        if (request.SoundVolume.HasValue)
            settings.SoundVolume = Math.Clamp(request.SoundVolume.Value, 0, 100);
        if (request.BrowserFontScalePercent.HasValue)
            settings.BrowserFontScalePercent = Math.Clamp(request.BrowserFontScalePercent.Value, 80, 200);
        if (request.BrowserUiScalePercent.HasValue)
            settings.BrowserUiScalePercent = Math.Clamp(request.BrowserUiScalePercent.Value, 80, 200);
        if (request.BrowserReducedMotion.HasValue)
            settings.BrowserReducedMotion = request.BrowserReducedMotion.Value;
        if (request.BrowserContrastFriendly.HasValue)
            settings.BrowserContrastFriendly = request.BrowserContrastFriendly.Value;
    }

    private BrowserClientSettingsDto BuildDto(GameSettings? candidate = null)
    {
        var settings = candidate ?? _stateManager.Settings;
        var language = NormalizeLanguage(settings.Language);
        var difficulty = NormalizeDifficulty(settings.Difficulty);
        var gameSessionExists = Directory.Exists(_fs.GameSessionPath);
        var sessionLabel = gameSessionExists
            ? "Текущая глава книги"
            : "Глава ещё не выбрана";

        return new BrowserClientSettingsDto(
            SchemaVersion: 1,
            Language: new BrowserSettingsChoiceGroupDto(
                Value: language,
                Label: LanguageLabel(language),
                Choices: LanguageChoices),
            Difficulty: new BrowserSettingsChoiceGroupDto(
                Value: difficulty,
                Label: DifficultyLabel(difficulty),
                Choices: DifficultyChoices),
            ShowGmThoughts: settings.ShowGmThoughts,
            Audio: new BrowserClientAudioSettingsDto(
                MusicEnabled: settings.MusicEnabled,
                MusicVolume: Math.Clamp(settings.MusicVolume, 0, 100),
                SoundEnabled: settings.SoundEnabled,
                SoundVolume: Math.Clamp(settings.SoundVolume, 0, 100)),
            Accessibility: new BrowserClientAccessibilitySettingsDto(
                FontScalePercent: Math.Clamp(settings.BrowserFontScalePercent, 80, 200),
                UiScalePercent: Math.Clamp(settings.BrowserUiScalePercent, 80, 200),
                ReducedMotion: settings.BrowserReducedMotion,
                ContrastFriendly: settings.BrowserContrastFriendly),
            Locality: new BrowserClientLocalityDto(
                LocalhostOnly: true,
                SessionLabel: sessionLabel,
                GameSessionExists: gameSessionExists,
                GmBridgeEnabled: settings.GmBridgeEnabled,
                GmBridgeLabel: settings.GmBridgeEnabled
                    ? "Локальный мост ГМа включён"
                    : "Локальный мост ГМа выключен",
                SafetySummary: "Книга открыта только на этом устройстве и хранит настройки вместе с вашим прохождением."));
    }

    private static byte[] BuildGmSettingsProjection(GameSettings settings)
    {
        var activeMods = settings.EnabledSystemMods
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new
            {
                fileName = name,
                modId = Path.GetFileNameWithoutExtension(name),
                name = Path.GetFileNameWithoutExtension(name)
            })
            .ToArray();

        var gameSettings = new
        {
            hardMode = string.Equals(settings.Difficulty, "hard", StringComparison.OrdinalIgnoreCase),
            impossibleMode = string.Equals(settings.Difficulty, "impossible", StringComparison.OrdinalIgnoreCase),
            difficulty = NormalizeDifficulty(settings.Difficulty),
            qteEventsEnabled = settings.EnableQteEvents,
            enabledSystemMods = activeMods,
            _lastUpdated = DateTime.UtcNow.ToString("o")
        };

        return System.Text.Encoding.UTF8.GetPreamble()
            .Concat(JsonSerializer.SerializeToUtf8Bytes(gameSettings, JsonOpts)).ToArray();
    }

    private static string NormalizeLanguage(string? value) =>
        string.Equals(value?.Trim(), "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";

    private static string NormalizeDifficulty(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "hard" => "hard",
        "impossible" => "impossible",
        _ => "normal"
    };

    private static string LanguageLabel(string value) => value switch
    {
        "en" => "English",
        _ => "Русский"
    };

    private static string DifficultyLabel(string value) => value switch
    {
        "hard" => "Сложно",
        "impossible" => "Невозможно",
        _ => "Обычная"
    };

    private static readonly BrowserSettingsChoiceDto[] LanguageChoices =
    {
        new("ru", "Русский", "Основной язык текущих игровых подсказок."),
        new("en", "English", "English client labels where supported.")
    };

    private static readonly BrowserSettingsChoiceDto[] DifficultyChoices =
    {
        new("normal", "Обычная", "Базовый уровень сложности."),
        new("hard", "Сложно", "Более опасные проверки и конфликты."),
        new("impossible", "Невозможно", "Предельная сложность для рискованного прохождения.")
    };
}

public sealed record BrowserClientSettingsUpdateResult(
    bool Success,
    bool IsBlocked,
    string Message,
    BrowserClientSettingsDto? Settings)
{
    public BrowserPreparedWriteDisposition Disposition { get; init; } = BrowserPreparedWriteDisposition.Blocked;

    public static BrowserClientSettingsUpdateResult Completed(BrowserClientSettingsDto settings) =>
        new(true, false, string.Empty, settings) { Disposition = BrowserPreparedWriteDisposition.Committed };

    public static BrowserClientSettingsUpdateResult Blocked(string message) =>
        new(false, true, message, null);

    internal static BrowserClientSettingsUpdateResult NotCompleted(BrowserPreparedWriteDisposition disposition, string message) =>
        new(false, disposition == BrowserPreparedWriteDisposition.Blocked, message, null) { Disposition = disposition };
}

public sealed record BrowserClientSettingsDto(
    int SchemaVersion,
    BrowserSettingsChoiceGroupDto Language,
    BrowserSettingsChoiceGroupDto Difficulty,
    bool ShowGmThoughts,
    BrowserClientAudioSettingsDto Audio,
    BrowserClientAccessibilitySettingsDto Accessibility,
    BrowserClientLocalityDto Locality,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PersistenceWarning = null);

public sealed record BrowserSettingsChoiceGroupDto(
    string Value,
    string Label,
    IReadOnlyList<BrowserSettingsChoiceDto> Choices);

public sealed record BrowserSettingsChoiceDto(
    string Value,
    string Label,
    string Description);

public sealed record BrowserClientAudioSettingsDto(
    bool MusicEnabled,
    int MusicVolume,
    bool SoundEnabled,
    int SoundVolume);

public sealed record BrowserClientAccessibilitySettingsDto(
    int FontScalePercent,
    int UiScalePercent,
    bool ReducedMotion,
    bool ContrastFriendly);

public sealed record BrowserClientLocalityDto(
    bool LocalhostOnly,
    string SessionLabel,
    bool GameSessionExists,
    bool GmBridgeEnabled,
    string GmBridgeLabel,
    string SafetySummary);

public sealed record BrowserClientSettingsUpdateRequest(
    string? Language,
    string? Difficulty,
    bool? ShowGmThoughts,
    bool? MusicEnabled,
    int? MusicVolume,
    bool? SoundEnabled,
    int? SoundVolume,
    int? BrowserFontScalePercent,
    int? BrowserUiScalePercent,
    bool? BrowserReducedMotion,
    bool? BrowserContrastFriendly);
