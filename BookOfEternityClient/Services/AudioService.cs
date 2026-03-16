using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace BookOfEternityClient.Services;

public enum AudioCue
{
    MenuSelect,
    TurnReady,
    QteStart,
    QteSuccess,
    QteFail
}

public enum MusicPlaylist
{
    None,
    MainMenu,
    InGame
}

public sealed class AudioService
{
    private const string MainTheme = "Main Theme.mp3";
    private const string MainThemeAlt = "Main Theme (alt).mp3";

    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly ILogger<AudioService> _logger;
    private readonly object _sync = new();
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<AudioCue, long> _lastCueTicks = new();

    private CancellationTokenSource? _musicCts;
    private Task? _musicLoopTask;
    private WaveOutEvent? _musicOutput;
    private AudioFileReader? _musicReader;
    private MusicPlaylist _currentPlaylist = MusicPlaylist.None;
    private string? _lastTrackPath;

    public AudioService(
        FileSystemManager fs,
        GameSettings settings,
        ILogger<AudioService> logger)
    {
        _fs = fs;
        _settings = settings;
        _logger = logger;
    }

    public Task PlayMainMenuMusicAsync() => SetPlaylistAsync(MusicPlaylist.MainMenu);

    public Task PlayInGameMusicAsync() => SetPlaylistAsync(MusicPlaylist.InGame);

    public async Task StopMusicAsync()
    {
        Task? loopTask;
        CancellationTokenSource? cts;
        lock (_sync)
        {
            _currentPlaylist = MusicPlaylist.None;
            loopTask = _musicLoopTask;
            _musicLoopTask = null;
            cts = _musicCts;
            _musicCts = null;
        }

        cts?.Cancel();
        StopCurrentMusicPlayback();
        if (loopTask != null)
        {
            try
            {
                await loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public async Task StopAllAsync()
    {
        await StopMusicAsync();
    }

    public async Task ApplySettingsAsync()
    {
        if (!_settings.MusicEnabled || _settings.MusicVolume <= 0)
        {
            await StopMusicAsync();
            return;
        }

        lock (_sync)
        {
            if (_musicReader != null)
                _musicReader.Volume = NormalizeVolume(_settings.MusicVolume);
        }
    }

    public void PlayCue(AudioCue cue)
    {
        if (!_settings.SoundEnabled || _settings.SoundVolume <= 0)
            return;
        if (!CanPlayCueNow(cue))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var path = ResolveCuePath(cue);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;

                using var reader = new AudioFileReader(path)
                {
                    Volume = NormalizeVolume(_settings.SoundVolume)
                };
                using var output = new WaveOutEvent();
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                output.PlaybackStopped += (_, _) => tcs.TrySetResult();
                output.Init(reader);
                output.Play();
                await tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось воспроизвести sound cue {Cue}", cue);
            }
        });
    }

    private bool CanPlayCueNow(AudioCue cue)
    {
        var minGap = cue switch
        {
            AudioCue.MenuSelect => TimeSpan.FromMilliseconds(120),
            AudioCue.QteStart => TimeSpan.FromMilliseconds(80),
            AudioCue.QteSuccess => TimeSpan.FromMilliseconds(80),
            AudioCue.QteFail => TimeSpan.FromMilliseconds(80),
            _ => TimeSpan.Zero
        };

        if (minGap == TimeSpan.Zero)
            return true;

        var now = DateTime.UtcNow.Ticks;
        while (true)
        {
            var previous = _lastCueTicks.GetOrAdd(cue, 0);
            if (previous != 0 && now - previous < minGap.Ticks)
                return false;
            if (_lastCueTicks.TryUpdate(cue, now, previous))
                return true;
        }
    }

    private async Task SetPlaylistAsync(MusicPlaylist playlist)
    {
        if (!_settings.MusicEnabled || _settings.MusicVolume <= 0)
        {
            await StopMusicAsync();
            return;
        }

        lock (_sync)
        {
            if (_currentPlaylist == playlist && _musicLoopTask != null && !_musicLoopTask.IsCompleted)
                return;
        }

        await StopMusicAsync();

        var candidates = ResolvePlaylistTracks(playlist).ToList();
        if (candidates.Count == 0)
        {
            _logger.LogDebug("Для плейлиста {Playlist} не найдено аудиофайлов", playlist);
            return;
        }

        var cts = new CancellationTokenSource();
        var loopTask = Task.Run(() => MusicLoopAsync(playlist, cts.Token), cts.Token);

        lock (_sync)
        {
            _currentPlaylist = playlist;
            _musicCts = cts;
            _musicLoopTask = loopTask;
        }
    }

    private async Task MusicLoopAsync(MusicPlaylist playlist, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var candidates = ResolvePlaylistTracks(playlist).ToList();
            if (candidates.Count == 0)
                return;

            var trackPath = PickNextTrack(candidates);
            if (string.IsNullOrWhiteSpace(trackPath))
                return;

            try
            {
                await PlayTrackAsync(trackPath, cancellationToken);
                _lastTrackPath = trackPath;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось воспроизвести музыкальный трек {Track}", trackPath);
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    private async Task PlayTrackAsync(string trackPath, CancellationToken cancellationToken)
    {
        var reader = new AudioFileReader(trackPath)
        {
            Volume = NormalizeVolume(_settings.MusicVolume)
        };
        var output = new WaveOutEvent();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => tcs.TrySetResult();
        output.Init(reader);

        lock (_sync)
        {
            _musicReader = reader;
            _musicOutput = output;
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                output.Stop();
            }
            catch
            {
            }
        });

        output.Play();
        await tcs.Task.WaitAsync(cancellationToken);

        lock (_sync)
        {
            if (ReferenceEquals(_musicReader, reader))
                _musicReader = null;
            if (ReferenceEquals(_musicOutput, output))
                _musicOutput = null;
        }

        output.Dispose();
        reader.Dispose();
    }

    private void StopCurrentMusicPlayback()
    {
        WaveOutEvent? output;
        AudioFileReader? reader;
        lock (_sync)
        {
            output = _musicOutput;
            reader = _musicReader;
            _musicOutput = null;
            _musicReader = null;
        }

        try
        {
            output?.Stop();
        }
        catch
        {
        }
        finally
        {
            output?.Dispose();
            reader?.Dispose();
        }
    }

    private string? PickNextTrack(IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        var filtered = candidates
            .Where(path => !string.Equals(path, _lastTrackPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
            filtered = candidates.ToList();

        return filtered[_random.Next(filtered.Count)];
    }

    private IEnumerable<string> ResolvePlaylistTracks(MusicPlaylist playlist)
    {
        var musicDir = ResolveMusicDirectory();
        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
            yield break;

        var files = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (playlist == MusicPlaylist.MainMenu)
            {
                if (string.Equals(fileName, MainTheme, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, MainThemeAlt, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }

                continue;
            }

            if (playlist == MusicPlaylist.InGame)
            {
                if (!string.Equals(fileName, MainTheme, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(fileName, MainThemeAlt, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }

    private string? ResolveCuePath(AudioCue cue)
    {
        var soundsDir = ResolveSoundsDirectory();
        if (string.IsNullOrWhiteSpace(soundsDir) || !Directory.Exists(soundsDir))
            return null;

        var candidates = cue switch
        {
            AudioCue.MenuSelect => new[] { "menu_select.wav" },
            AudioCue.TurnReady => new[] { "sound-notification.wav" },
            AudioCue.QteStart => new[] { "qte-start.wav", "qte_start.wav", "qte-start.mp3", "qte_start.mp3", "menu_select.wav" },
            AudioCue.QteSuccess => new[] { "qte-success.wav", "qte_success.wav", "qte-success.mp3", "qte_success.mp3", "sound-notification.wav" },
            AudioCue.QteFail => new[] { "qte-fail.wav", "qte_fail.wav", "qte-fail.mp3", "qte_fail.mp3", "sound-notification.wav" },
            _ => Array.Empty<string>()
        };

        foreach (var fileName in candidates)
        {
            var path = Path.Combine(soundsDir, fileName);
            if (File.Exists(path))
                return path;
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

    private static float NormalizeVolume(int value) => Math.Clamp(value / 100f, 0f, 1f);
}
