using System.Text.Json;
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
            await _stateManager.LoadSettingsAsync();
            return BuildDto();
        }
        finally
        {
            BrowserAudioService.SettingsWriteGate.Release();
        }
    }

    public async Task<BrowserClientSettingsUpdateResult> UpdateAsync(BrowserClientSettingsUpdateRequest request)
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            await _stateManager.LoadSettingsAsync();
            var shouldWriteGmProjection = request.Difficulty is not null;
            var result = await _coordinator.ExecuteAsync(
                new BrowserLocalWriteRequest(
                    OwnerId: $"browser-settings:{Environment.MachineName}:{Environment.ProcessId}",
                    OwnerLabel: "Browser Client settings",
                    OperationLabel: "Browser Client settings update"),
                ["config.json", "game_state/core/game_settings.json"],
                async () =>
                {
                    ApplyRequest(request);
                    await _stateManager.SaveSettingsAsync();
                    if (shouldWriteGmProjection)
                        await WriteGmSettingsProjectionAsync();
                    await _audioService.ApplySettingsAsync();
                });

            return result.Success
                ? BrowserClientSettingsUpdateResult.Completed(BuildDto())
                : BrowserClientSettingsUpdateResult.Blocked(result.Message);
        }
        finally
        {
            BrowserAudioService.SettingsWriteGate.Release();
        }
    }

    private void ApplyRequest(BrowserClientSettingsUpdateRequest request)
    {
        var settings = _stateManager.Settings;

        if (request.Language is not null)
        {
            settings.Language = NormalizeLanguage(request.Language);
            _localization.CurrentLanguage = settings.Language;
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
            settings.BrowserUiScalePercent = Math.Clamp(request.BrowserUiScalePercent.Value, 80, 140);
        if (request.BrowserReducedMotion.HasValue)
            settings.BrowserReducedMotion = request.BrowserReducedMotion.Value;
        if (request.BrowserContrastFriendly.HasValue)
            settings.BrowserContrastFriendly = request.BrowserContrastFriendly.Value;
    }

    private BrowserClientSettingsDto BuildDto()
    {
        var settings = _stateManager.Settings;
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
                UiScalePercent: Math.Clamp(settings.BrowserUiScalePercent, 80, 140),
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

    private async Task WriteGmSettingsProjectionAsync()
    {
        var settings = _stateManager.Settings;
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

        await _fs.WriteFileAtomicAsync(
            "game_state/core/game_settings.json",
            JsonSerializer.Serialize(gameSettings, JsonOpts));
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
    public static BrowserClientSettingsUpdateResult Completed(BrowserClientSettingsDto settings) =>
        new(true, false, string.Empty, settings);

    public static BrowserClientSettingsUpdateResult Blocked(string message) =>
        new(false, true, message, null);
}

public sealed record BrowserClientSettingsDto(
    int SchemaVersion,
    BrowserSettingsChoiceGroupDto Language,
    BrowserSettingsChoiceGroupDto Difficulty,
    bool ShowGmThoughts,
    BrowserClientAudioSettingsDto Audio,
    BrowserClientAccessibilitySettingsDto Accessibility,
    BrowserClientLocalityDto Locality);

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
