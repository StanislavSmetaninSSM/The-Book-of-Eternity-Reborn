using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.AspNetCore.Http;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserAudioService
{
    private const string MainMenuPlaylistId = "main-menu";
    private const string InGamePlaylistId = "in-game";
    private const string MainTheme = "Main Theme.mp3";
    private const string MainThemeAlt = "Main Theme (alt).mp3";

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly AudioService _audioService;
    internal static readonly SemaphoreSlim SettingsWriteGate = new SemaphoreSlim(1, 1);

    public BrowserAudioService(FileSystemManager fs, StateManager stateManager, AudioService audioService)
    {
        _fs = fs;
        _stateManager = stateManager;
        _audioService = audioService;
    }

    public async Task<BrowserAudioSettingsDto> BuildSettingsAsync()
    {
        await SettingsWriteGate.WaitAsync();
        try
        {
            await _stateManager.LoadSettingsAsync();
            return BuildSettings();
        }
        finally
        {
            SettingsWriteGate.Release();
        }
    }

    public async Task<BrowserAudioSettingsDto> UpdateSettingsAsync(BrowserAudioSettingsUpdateRequest request)
    {
        await SettingsWriteGate.WaitAsync();
        try
        {
            await _stateManager.LoadSettingsAsync();
            var settings = _stateManager.Settings;

            if (request.MusicEnabled.HasValue)
                settings.MusicEnabled = request.MusicEnabled.Value;
            if (request.MusicVolume.HasValue)
                settings.MusicVolume = Math.Clamp(request.MusicVolume.Value, 0, 100);
            if (request.SoundEnabled.HasValue)
                settings.SoundEnabled = request.SoundEnabled.Value;
            if (request.SoundVolume.HasValue)
                settings.SoundVolume = Math.Clamp(request.SoundVolume.Value, 0, 100);

            await _stateManager.SaveSettingsAsync();
            await _audioService.ApplySettingsAsync();
            return BuildSettings();
        }
        finally
        {
            SettingsWriteGate.Release();
        }
    }

    public IResult ServeAsset(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId) || assetId.Contains('/') || assetId.Contains('\\'))
            return Results.NotFound();

        var asset = EnumerateAssets().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assetId, StringComparison.Ordinal));
        if (asset is null || !File.Exists(asset.FullPath))
            return Results.NotFound();

        return Results.File(
            asset.FullPath,
            asset.ContentType,
            fileDownloadName: null,
            enableRangeProcessing: true);
    }

    private BrowserAudioSettingsDto BuildSettings()
    {
        var settings = _stateManager.Settings;
        var playlists = BuildPlaylists();
        var cues = BuildCues();
        var hasAssets = playlists.Any(playlist => playlist.Available) || cues.Any(cue => cue.Available);

        return new BrowserAudioSettingsDto(
            SchemaVersion: 1,
            MusicEnabled: settings.MusicEnabled,
            MusicVolume: Math.Clamp(settings.MusicVolume, 0, 100),
            SoundEnabled: settings.SoundEnabled,
            SoundVolume: Math.Clamp(settings.SoundVolume, 0, 100),
            AutoplayGuidance: "Звук запускается после вашего нажатия: нажмите «Включить музыку», чтобы разрешить музыку и звуковые подсказки для этой вкладки.",
            MissingAssetsMessage: hasAssets
                ? string.Empty
                : "Локальные аудиофайлы не найдены. Игра продолжит работать без музыки и звуковых подсказок.",
            Playlists: playlists,
            Cues: cues);
    }

    private IReadOnlyList<BrowserAudioPlaylistDto> BuildPlaylists()
    {
        return new[]
        {
            BuildPlaylist(
                MainMenuPlaylistId,
                "Главное меню",
                "Тихая тема книги до входа в активную сцену.",
                ResolvePlaylistTracks(MainMenuPlaylistId)),
            BuildPlaylist(
                InGamePlaylistId,
                "Игра",
                "Фоновая музыка для текущей сцены и переходов между мирами.",
                ResolvePlaylistTracks(InGamePlaylistId))
        };
    }

    private BrowserAudioPlaylistDto BuildPlaylist(
        string id,
        string label,
        string usage,
        IEnumerable<BrowserAudioAssetCatalogEntry> tracks)
    {
        var assets = tracks
            .Select(ToAssetDto)
            .ToArray();
        return new BrowserAudioPlaylistDto(
            Id: id,
            Label: label,
            Usage: usage,
            Available: assets.Length > 0,
            Tracks: assets);
    }

    private IReadOnlyList<BrowserAudioCueDto> BuildCues()
    {
        return CueDefinitions
            .Select(definition =>
            {
                var asset = ResolveCueAsset(definition.Id);
                return new BrowserAudioCueDto(
                    Id: definition.Id,
                    Label: definition.Label,
                    Usage: definition.Usage,
                    Available: asset is not null,
                    Asset: asset is null ? null : ToAssetDto(asset));
            })
            .ToArray();
    }

    private IEnumerable<BrowserAudioAssetCatalogEntry> EnumerateAssets()
    {
        foreach (var asset in ResolvePlaylistTracks(MainMenuPlaylistId))
            yield return asset;
        foreach (var asset in ResolvePlaylistTracks(InGamePlaylistId))
            yield return asset;
        foreach (var definition in CueDefinitions)
        {
            var asset = ResolveCueAsset(definition.Id);
            if (asset is not null)
                yield return asset;
        }
    }

    private IEnumerable<BrowserAudioAssetCatalogEntry> ResolvePlaylistTracks(string playlistId)
    {
        var musicDir = ResolveMusicDirectory();
        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
            yield break;

        var files = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var isMainTheme = string.Equals(fileName, MainTheme, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, MainThemeAlt, StringComparison.OrdinalIgnoreCase);
            if (playlistId == MainMenuPlaylistId && !isMainTheme)
                continue;
            if (playlistId == InGamePlaylistId && isMainTheme)
                continue;

            yield return new BrowserAudioAssetCatalogEntry(
                Id: $"music:{playlistId}:{fileName}",
                Label: Path.GetFileNameWithoutExtension(fileName),
                FullPath: file,
                ContentType: "audio/mpeg");
        }
    }

    private BrowserAudioAssetCatalogEntry? ResolveCueAsset(string cueId)
    {
        var soundsDir = ResolveSoundsDirectory();
        if (string.IsNullOrWhiteSpace(soundsDir) || !Directory.Exists(soundsDir))
            return null;

        foreach (var fileName in ResolveCueCandidates(cueId))
        {
            var path = Path.Combine(soundsDir, fileName);
            if (!File.Exists(path))
                continue;

            return new BrowserAudioAssetCatalogEntry(
                Id: $"cue:{cueId}:{fileName}",
                Label: Path.GetFileNameWithoutExtension(fileName),
                FullPath: path,
                ContentType: ResolveContentType(fileName));
        }

        return null;
    }

    private string? ResolveMusicDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(_fs.BasePath, "BookOfEternityClient", "Music"),
            Path.Combine(_fs.BasePath, "Music")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private string? ResolveSoundsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(_fs.BasePath, "BookOfEternityClient", "Sounds"),
            Path.Combine(_fs.BasePath, "Sounds")
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static IEnumerable<string> ResolveCueCandidates(string cueId) => cueId switch
    {
        "menu-select" => new[] { "menu_select.wav" },
        "turn-ready" => new[] { "sound-notification.wav" },
        "qte-start" => new[] { "qte-start.wav", "qte_start.wav", "qte-start.mp3", "qte_start.mp3", "menu_select.wav" },
        "qte-success" => new[] { "qte-success.wav", "qte_success.wav", "qte-success.mp3", "qte_success.mp3", "sound-notification.wav" },
        "qte-fail" => new[] { "qte-fail.wav", "qte_fail.wav", "qte-fail.mp3", "qte_fail.mp3", "sound-notification.wav" },
        _ => Array.Empty<string>()
    };

    private static string ResolveContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };

    private static BrowserAudioAssetDto ToAssetDto(BrowserAudioAssetCatalogEntry entry) =>
        new(
            Id: entry.Id,
            Label: entry.Label,
            Url: $"/api/audio/assets/{Uri.EscapeDataString(entry.Id)}",
            ContentType: entry.ContentType);

    private static readonly BrowserAudioCueDefinition[] CueDefinitions =
    {
        new("menu-select", "Выбор в меню", "Короткий отклик на игровое меню."),
        new("turn-ready", "Ответ ГМа готов", "Уведомление, что ход принят или готов к чтению."),
        new("qte-start", "Начало QTE", "Напряжённый сигнал быстрого события."),
        new("qte-success", "Успех QTE", "Подтверждение удачного исхода быстрой сцены."),
        new("qte-fail", "Провал QTE", "Мягкий сигнал неудачного исхода быстрой сцены.")
    };

    private sealed record BrowserAudioCueDefinition(string Id, string Label, string Usage);

    private sealed record BrowserAudioAssetCatalogEntry(
        string Id,
        string Label,
        string FullPath,
        string ContentType);
}

public sealed record BrowserAudioSettingsDto(
    int SchemaVersion,
    bool MusicEnabled,
    int MusicVolume,
    bool SoundEnabled,
    int SoundVolume,
    string AutoplayGuidance,
    string MissingAssetsMessage,
    IReadOnlyList<BrowserAudioPlaylistDto> Playlists,
    IReadOnlyList<BrowserAudioCueDto> Cues);

public sealed record BrowserAudioPlaylistDto(
    string Id,
    string Label,
    string Usage,
    bool Available,
    IReadOnlyList<BrowserAudioAssetDto> Tracks);

public sealed record BrowserAudioCueDto(
    string Id,
    string Label,
    string Usage,
    bool Available,
    BrowserAudioAssetDto? Asset);

public sealed record BrowserAudioAssetDto(
    string Id,
    string Label,
    string Url,
    string ContentType);

public sealed record BrowserAudioSettingsUpdateRequest(
    bool? MusicEnabled,
    int? MusicVolume,
    bool? SoundEnabled,
    int? SoundVolume);
