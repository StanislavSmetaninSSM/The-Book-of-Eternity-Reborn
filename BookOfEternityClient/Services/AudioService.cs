using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BookOfEternityClient.Services;

public enum AudioCue { MenuSelect, TurnReady, QteStart, QteSuccess, QteFail }
public enum MusicPlaylist { None, MainMenu, InGame }

public sealed partial class AudioService : IAsyncDisposable
{
    private const string MainTheme = "Main Theme.mp3";
    private const string MainThemeAlt = "Main Theme (alt).mp3";
    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly ILogger<AudioService> _logger;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _transitions = new(1, 1);
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<AudioCue, long> _lastCueTicks = new();
    private readonly HashSet<Operation> _operations = [];
    private readonly IAudioPlaybackBackend? _backend;
    private readonly bool _browserManaged;
    private Operation? _music;
    private MusicPlaylist _currentPlaylist;
    private string? _lastTrackPath;
    private bool _closing, _disposed;
    private AudioOutcome _outcome = AudioOutcome.NotRequested;
    private static readonly TimeSpan SettlementBound = TimeSpan.FromSeconds(2);

    public AudioService(FileSystemManager fs, GameSettings settings, ILogger<AudioService> logger)
        : this(fs, settings, logger, AudioBackendFactory.Create()) { }

    internal AudioService(FileSystemManager fs, GameSettings settings, ILogger<AudioService> logger,
        IAudioPlaybackBackend? backend, bool browserManaged = false)
    { _fs = fs; _settings = settings; _logger = logger; _backend = backend; _browserManaged = browserManaged; }

    internal static AudioService CreateBrowserManaged(FileSystemManager fs, GameSettings settings, ILogger<AudioService> logger)
        => new(fs, settings, logger, null, browserManaged: true);

    /// <summary>Reports capability/debt without performing any device query.</summary>
    public AudioStatus Status
    {
        get
        {
            lock (_sync)
            {
                RetireSettled();
                return new(HasDebt() ? AudioOutcome.CleanupUncertain : _disposed ? AudioOutcome.Disposed :
                    _browserManaged ? AudioOutcome.BrowserManaged : _outcome,
                    _browserManaged ? AudioBackendKind.Browser : _backend!.Kind);
            }
        }
    }

    public Task PlayMainMenuMusicAsync() => SetPlaylistAsync(MusicPlaylist.MainMenu);
    public Task PlayInGameMusicAsync() => SetPlaylistAsync(MusicPlaylist.InGame);
    public Task StopMusicAsync() => StopAsync(all: false, dispose: false);
    public Task StopAllAsync() => StopAsync(all: true, dispose: false);
    public ValueTask DisposeAsync() => new(StopAsync(all: true, dispose: true));

    public async Task ApplySettingsAsync()
    {
        await _transitions.WaitAsync();
        try
        {
            Operation[] stops;
            lock (_sync)
            {
                RetireSettled();
                if (_browserManaged || _disposed) return;
                _closing = true;
                stops = _operations.Where(o => o.IsMusic
                    ? !CurrentSettings.MusicEnabled || CurrentSettings.MusicVolume <= 0
                    : !CurrentSettings.SoundEnabled || CurrentSettings.SoundVolume <= 0).ToArray();
                foreach (var op in stops) op.Cancellation.Cancel();
            }
            await SettleAsync(stops);
            lock (_sync) { RetireSettled(); _closing = false; }
            // Live volume is read by original sessions; preview/source changes require no reopen.
        }
        finally { _transitions.Release(); }
    }

    public void PlayCue(AudioCue cue)
    {
        lock (_sync)
        {
            RetireSettled();
            if (!CanAdmit()) return;
            if (!CurrentSettings.SoundEnabled || CurrentSettings.SoundVolume <= 0) { _outcome = AudioOutcome.Muted; return; }
            if (!CanPlayCueNow(cue)) return;
            var path = ResolveCuePath(cue);
            if (path == null) { _outcome = AudioOutcome.NoAssets; return; }
            Publish(false, MusicPlaylist.None, path);
        }
    }

    private bool CanAdmit() => !_browserManaged && !_disposed && !_closing && !HasDebt();
    private bool HasDebt() => _operations.Any(o => (o.WaitTimedOut && !o.Task.IsCompleted) ||
        (o.Task.IsCompleted && !o.CleanupConfirmed));

    private async Task SetPlaylistAsync(MusicPlaylist playlist)
    {
        await _transitions.WaitAsync();
        try
        {
            Operation? previous;
            lock (_sync)
            {
                RetireSettled();
                if (!CanAdmit()) return;
                if (CurrentSettings.MusicEnabled && CurrentSettings.MusicVolume > 0 && _currentPlaylist == playlist && _music != null) return;
                _closing = true; previous = _music; previous?.Cancellation.Cancel();
            }
            await SettleAsync(previous == null ? [] : [previous]);
            Operation? next = null;
            lock (_sync)
            {
                RetireSettled(); _closing = false;
                if (!CanAdmit()) return;
                if (!CurrentSettings.MusicEnabled || CurrentSettings.MusicVolume <= 0) { _outcome = AudioOutcome.Muted; return; }
                var tracks = ResolvePlaylistTracks(playlist).ToArray();
                if (tracks.Length == 0) { _outcome = AudioOutcome.NoAssets; return; }
                _currentPlaylist = playlist; next = _music = Publish(true, playlist, null);
            }
            try { await next.Ready.Task.WaitAsync(SettlementBound); }
            catch (TimeoutException)
            { lock (_sync) { next.WaitTimedOut = true; next.Cancellation.Cancel(); } }
        }
        finally { _transitions.Release(); }
    }

    private Operation Publish(bool music, MusicPlaylist playlist, string? cuePath)
    {
        var op = new Operation(music);
        _operations.Add(op); // Original owner is published before asynchronous work can complete.
        op.Task = Task.Run(() => RunOperationAsync(op, playlist, cuePath));
        return op;
    }

    private async Task RunOperationAsync(Operation op, MusicPlaylist playlist, string? cuePath)
    {
        var outcome = AudioOutcome.Canceled;
        try
        {
            do
            {
                op.Cancellation.Token.ThrowIfCancellationRequested();
                var path = op.IsMusic ? PickNextTrack(ResolvePlaylistTracks(playlist).ToArray()) : cuePath;
                if (path == null) { outcome = AudioOutcome.NoAssets; break; }
                op.Session = _backend!.Create(path, () => NormalizeVolume(op.IsMusic ? CurrentSettings.MusicVolume : CurrentSettings.SoundVolume));
                try
                {
                    await op.Session.RunAsync(() =>
                    {
                        lock (_sync) { if (!op.Cancellation.IsCancellationRequested) _outcome = AudioOutcome.Playing; }
                        op.Ready.TrySetResult();
                    }, op.Cancellation.Token);
                    outcome = AudioOutcome.Stopped;
                }
                finally
                {
                    await op.Session.DisposeAsync();
                    op.Session = null;
                }
                if (op.IsMusic) _lastTrackPath = path;
            } while (op.IsMusic);
        }
        catch (OperationCanceledException) when (op.Cancellation.IsCancellationRequested) { outcome = AudioOutcome.Canceled; }
        catch (AudioCapabilityException ex) { outcome = ex.Outcome; }
        catch (Exception ex)
        { outcome = AudioOutcome.Error; _logger.LogDebug(ex, "Owned audio operation failed."); }
        finally
        {
            lock (_sync)
            {
                op.CleanupConfirmed = op.Session == null;
                _outcome = op.CleanupConfirmed ? outcome : AudioOutcome.CleanupUncertain;
            }
            op.Ready.TrySetResult();
        }
    }

    private async Task StopAsync(bool all, bool dispose)
    {
        await _transitions.WaitAsync();
        try
        {
            Operation[] stops;
            lock (_sync)
            {
                RetireSettled(); _closing = true; _disposed |= dispose;
                stops = _operations.Where(o => all || o.IsMusic).ToArray();
                foreach (var op in stops) op.Cancellation.Cancel();
            }
            await SettleAsync(stops);
            lock (_sync)
            {
                RetireSettled(); _closing = false;
                if (!HasDebt() && !_browserManaged) _outcome = AudioOutcome.Stopped;
            }
        }
        finally { _transitions.Release(); }
    }

    private async Task SettleAsync(Operation[] operations)
    {
        if (operations.Length == 0) return;
        try { await Task.WhenAll(operations.Select(o => o.Task)).WaitAsync(SettlementBound); }
        catch (TimeoutException)
        { lock (_sync) foreach (var op in operations.Where(o => !o.Task.IsCompleted)) op.WaitTimedOut = true; }
    }

    // Called under _sync only; completed original Task + disposal, never cancellation alone.
    private void RetireSettled()
    {
        foreach (var op in _operations.Where(o => o.Task.IsCompleted && o.CleanupConfirmed).ToArray())
        {
            _operations.Remove(op); op.Cancellation.Dispose();
            if (ReferenceEquals(_music, op)) { _music = null; _currentPlaylist = MusicPlaylist.None; }
        }
    }
    private sealed class Operation(bool music)
    {
        internal bool IsMusic { get; } = music;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Task = Task.CompletedTask;
        internal IAudioPlaybackSession? Session;
        internal bool CleanupConfirmed, WaitTimedOut;
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
