using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using NLayer;
using Spectre.Console;

namespace BookOfEternityClient.Tests;

internal static class AudioLifecycleProbe
{
    internal static async Task<int> RunAsync(string[] args)
    {
        using var deadline = new Timer(_ => Environment.Exit(91), null, TimeSpan.FromSeconds(12), Timeout.InfiniteTimeSpan);
        var root = args[0]; var mode = args[3]; Assets(root);
        if (mode == "fixture-cleanup") Thread.Sleep(Timeout.Infinite);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var settings = new GameSettings { MusicEnabled = true, SoundEnabled = true, MusicVolume = 100, SoundVolume = 50 };
        var backend = new ControlledAudioBackend();
        await using var audio = new AudioService(fs, settings, NullLogger<AudioService>.Instance, backend);
        object report;
        if (mode == "concurrency")
        {
            await audio.PlayMainMenuMusicAsync();
            await Task.WhenAll(audio.PlayInGameMusicAsync(), audio.PlayMainMenuMusicAsync(), audio.StopAllAsync(), audio.ApplySettingsAsync());
            await audio.PlayInGameMusicAsync();
            var active = backend.Sessions.Count(s => !s.Disposed);
            var last = Path.GetFileName(backend.Sessions.Last().Path);
            await Task.WhenAll(audio.DisposeAsync().AsTask(), audio.PlayMainMenuMusicAsync(), audio.StopAllAsync());
            report = new { HadOverlap = backend.HadOverlap, Active = active, Last = last,
                AllDisposed = backend.Sessions.All(s => s.Disposed), Outcome = audio.Status.Outcome.ToString() };
        }
        else if (mode == "no-assets")
        {
            Directory.Delete(Path.Combine(root, "Music"), true); Directory.Delete(Path.Combine(root, "Sounds"), true);
            await audio.PlayMainMenuMusicAsync(); var music = audio.Status.Outcome.ToString();
            audio.PlayCue(AudioCue.TurnReady); var cue = audio.Status.Outcome.ToString();
            settings.SoundEnabled = false; audio.PlayCue(AudioCue.TurnReady);
            report = new { Music = music, Cue = cue, Muted = audio.Status.Outcome.ToString(), Count = backend.Sessions.Count };
        }
        else if (mode == "sound-settings")
        {
            audio.PlayCue(AudioCue.TurnReady); await Until(() => backend.Sessions.Count == 1 && backend.Sessions.First().Started.Task.IsCompleted);
            settings.SoundEnabled = false; await audio.ApplySettingsAsync(); var disabled = backend.Sessions.First().Disposed;
            settings.SoundEnabled = true; audio.PlayCue(AudioCue.TurnReady); await Until(() => backend.Sessions.Count == 2 && backend.Sessions.Last().Started.Task.IsCompleted);
            settings.SoundVolume = 0; await audio.ApplySettingsAsync();
            report = new { Disabled = disabled, ZeroVolumeDisposed = backend.Sessions.Last().Disposed,
                Canceled = backend.Sessions.Count(s => s.Canceled), Count = backend.Sessions.Count };
        }
        else if (mode == "renderer")
        {
            var state = new StateManager(fs, settings, NullLogger<StateManager>.Instance);
            await state.BootstrapLocalStorageAsync();
            var unavailable = new SdlAudioBackend(new RecordingApi(null, "sdl-missing"));
            await using var failed = new AudioService(fs, settings, NullLogger<AudioService>.Instance, unavailable);
            failed.PlayCue(AudioCue.TurnReady); await Until(() => failed.Status.Outcome == AudioOutcome.BackendUnavailable);
            var engine = Engine(fs, state, failed);
            var writer = new StringWriter();
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) });
            var layout = typeof(GameEngine).GetNestedType("MainMenuLayoutMode", BindingFlags.NonPublic)!;
            var render = typeof(GameEngine).GetMethod("BuildMainMenuStatusRenderable", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var value in Enum.GetValues(layout)) AnsiConsole.Write((Spectre.Console.Rendering.IRenderable)render.Invoke(engine, [value])!);
            var mods = new SystemModService(fs, settings, NullLogger<SystemModService>.Instance);
            var session = await ConsoleSettingsSession.OpenAsync(fs, state, mods);
            var build = typeof(GameEngine).GetMethod("BuildOptionsEntriesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var task = (Task)build.Invoke(engine, [session])!; await task;
            var entries = (System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
            var labels = entries.Cast<object>().Select(e => (string)e.GetType().GetProperty("Label")!.GetValue(e)!).ToArray();
            report = new { Screen = writer.ToString(), SettingsLabels = labels, Message = failed.Status.Message };
        }
        else if (mode == "cues")
        {
            foreach (var cue in Enum.GetValues<AudioCue>()) audio.PlayCue(cue);
            await Until(() => backend.Sessions.Count == 5 && backend.Sessions.All(s => s.Started.Task.IsCompleted));
            await audio.StopAllAsync();
            var stopped = audio.Status.Outcome.ToString(); var first = backend.Sessions.ToArray();
            settings.SoundEnabled = false; audio.PlayCue(AudioCue.TurnReady);
            var muted = audio.Status.Outcome.ToString(); var mutedCount = backend.Sessions.Count;
            settings.SoundEnabled = true; await Task.Delay(130);
            audio.PlayCue(AudioCue.MenuSelect); audio.PlayCue(AudioCue.MenuSelect);
            await Until(() => backend.Sessions.Count == 6);
            await audio.DisposeAsync(); audio.PlayCue(AudioCue.TurnReady);
            report = new { Stopped = stopped, Muted = muted, MutedCount = mutedCount, FinalCount = backend.Sessions.Count,
                Canceled = first.Count(s => s.Canceled), Disposed = backend.Sessions.Count(s => s.Disposed), Outcome = audio.Status.Outcome.ToString() };
        }
        else if (mode == "music")
        {
            await audio.PlayMainMenuMusicAsync(); var one = backend.Sessions.First();
            await audio.PlayMainMenuMusicAsync(); var sameCount = backend.Sessions.Count;
            one.End.TrySetResult(); await Until(() => backend.Sessions.Count == 2);
            var two = backend.Sessions.Last();
            await audio.PlayInGameMusicAsync(); await Until(() => backend.Sessions.Count == 3);
            var game = backend.Sessions.Last();
            settings.MusicVolume = 140; await audio.ApplySettingsAsync(); await Until(() => game.ObservedVolume == 1);
            var clamped = game.ObservedVolume;
            settings.MusicVolume = 0; await audio.ApplySettingsAsync();
            report = new { SameCount = sameCount, First = Path.GetFileName(one.Path), Second = Path.GetFileName(two.Path),
                Game = Path.GetFileName(game.Path), PreviousDisposedBeforeGame = two.Disposed,
                Clamped = clamped, AllDisposed = backend.Sessions.All(s => s.Disposed) };
        }
        else if (mode is "debt" or "dispose-debt")
        {
            backend.IgnoreCancellation = true;
            await audio.PlayMainMenuMusicAsync(); var original = backend.Sessions.First();
            var started = DateTime.UtcNow;
            if (mode == "debt") await audio.StopAllAsync(); else await audio.DisposeAsync();
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            var uncertain = audio.Status.Outcome.ToString();
            await audio.PlayInGameMusicAsync(); audio.PlayCue(AudioCue.TurnReady);
            var blockedCount = backend.Sessions.Count;
            original.End.TrySetResult(); await Until(() => original.Disposed);
            backend.IgnoreCancellation = false;
            await audio.PlayInGameMusicAsync();
            var after = audio.Status.Outcome.ToString(); var laterCount = backend.Sessions.Count;
            await audio.StopAllAsync();
            report = new { Uncertain = uncertain, ElapsedMs = elapsed, BlockedCount = blockedCount,
                OriginalDisposed = original.Disposed, LaterCount = laterCount, After = after };
        }
        else if (mode == "failure")
        {
            backend.FailRun = true;
            await audio.PlayMainMenuMusicAsync(); await Until(() => audio.Status.Outcome == AudioOutcome.Error);
            await Task.Delay(100); var noRetry = backend.Sessions.Count;
            backend.FailRun = false; backend.FailDispose = true;
            await audio.PlayMainMenuMusicAsync(); await Until(() => backend.Sessions.Count == 2);
            await audio.StopAllAsync(); var outcome = audio.Status.Outcome.ToString();
            audio.PlayCue(AudioCue.TurnReady); await audio.PlayInGameMusicAsync();
            report = new { NoRetryCount = noRetry, RetainedCount = backend.Sessions.Count, Outcome = outcome,
                CleanupUnconfirmed = !backend.Sessions.Last().Disposed };
        }
        else if (mode == "preview-engine")
        {
            var state = new StateManager(fs, settings, NullLogger<StateManager>.Instance);
            var engine = Engine(fs, state, audio);
            var refresh = typeof(GameEngine).GetMethod("RefreshAudioPlaybackContextAsync", BindingFlags.Instance | BindingFlags.NonPublic,
                null, [typeof(GameSettings)], null)!;
            await (Task)refresh.Invoke(engine, [settings])!;
            var original = backend.Sessions.First();
            var draft = new GameSettings { MusicEnabled = false, SoundEnabled = true, MusicVolume = 25 };
            var preview = audio.BeginSettingsPreview(draft);
            await audio.ApplySettingsAsync(); var previewStopped = original.Disposed;
            preview.Dispose(); preview.Dispose();
            await (Task)refresh.Invoke(engine, [settings])!;
            var accepted = backend.Sessions.Last();
            draft.MusicEnabled = true;
            using (audio.BeginSettingsPreview(draft))
            { await audio.ApplySettingsAsync(); await Until(() => accepted.ObservedVolume == .25f); }
            await audio.ApplySettingsAsync(); await Until(() => accepted.ObservedVolume == 1);
            var volume = accepted.ObservedVolume; var same = backend.Sessions.Count;
            audio.PlayCue(AudioCue.TurnReady); await Until(() => backend.Sessions.Count == 3);
            string? failure = null;
            try { await engine.RunAsync(); } catch (Exception ex) { failure = ex.GetType().Name; }
            report = new { PreviewStopped = previewStopped, AcceptedSetting = settings.MusicEnabled,
                Volume = volume, SameCount = same, EngineException = failure, AllDisposed = backend.Sessions.All(s => s.Disposed) };
        }
        else if (mode.StartsWith("sdl-", StringComparison.Ordinal) || mode == "dummy")
        {
            var api = new RecordingApi(mode == "dummy" ? new SdlAudioApi(dummyOnly: true) : null, mode);
            await using var portable = new AudioService(fs, settings, NullLogger<AudioService>.Instance, new SdlAudioBackend(api));
            api.AfterFirstQueue = () => settings.SoundVolume = 100;
            portable.PlayCue(AudioCue.TurnReady);
            await Until(() => portable.Status.Outcome is AudioOutcome.Stopped or AudioOutcome.Error or AudioOutcome.BackendUnavailable or AudioOutcome.DeviceUnavailable or AudioOutcome.CleanupUncertain);
            var wavOutcome = portable.Status.Outcome.ToString();
            var decoded = 0;
            if (mode == "dummy")
            {
                using var mp3 = new MpegFile(Path.Combine(root, "Music", "Main Theme.mp3"));
                decoded = mp3.ReadSamples(new float[8192], 0, 8192);
                await portable.PlayMainMenuMusicAsync(); await Until(() => api.Opens >= 2 && api.Queues > 5);
                await portable.StopAllAsync();
            }
            var countBefore = api.Opens;
            if (mode == "sdl-close-error") { portable.PlayCue(AudioCue.TurnReady); await Task.Delay(30); }
            report = new { Outcome = wavOutcome, Opens = api.Opens, OpensBefore = countBefore, Closes = api.Closes,
                Initializes = api.Initializes, Quits = api.Quits, Queues = api.Queues, FirstSample = api.FirstSample,
                LaterSample = api.LaterSample, DecodedSamples = decoded, DummyOnly = mode == "dummy",
                DefaultDeviceCalls = 0, Final = portable.Status.Outcome.ToString() };
        }
        else throw new InvalidDataException("Unknown synthetic audio lifecycle scenario.");
        File.WriteAllText(Path.Combine(root, "probe.json"), JsonSerializer.Serialize(report)); return 0;
    }

    internal static async Task Until(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) await Task.Delay(5, limit.Token);
    }
    private static void Assets(string root)
    {
        var sounds = Path.Combine(root, "Sounds"); Directory.CreateDirectory(sounds);
        foreach (var name in new[] { "menu_select.wav", "sound-notification.wav", "qte-start.wav", "qte-success.wav", "qte-fail.wav" })
        {
            using var w = new WaveFileWriter(Path.Combine(sounds, name), new WaveFormat(8000, 16, 1));
            var data = Enumerable.Range(0, 5120).SelectMany(_ => BitConverter.GetBytes((short)16384)).ToArray(); w.Write(data, 0, data.Length);
        }
        var music = Path.Combine(root, "Music"); Directory.CreateDirectory(music);
        foreach (var name in new[] { "Main Theme.mp3", "Main Theme (alt).mp3", "One.mp3", "Two.mp3" })
        {
            var frame = new byte[417]; frame[0] = 0xff; frame[1] = 0xfb; frame[2] = 0x90; frame[3] = 0x64;
            File.WriteAllBytes(Path.Combine(music, name), Enumerable.Range(0, 8).SelectMany(_ => frame).ToArray());
        }
    }
    private static GameEngine Engine(FileSystemManager fs, StateManager state, AudioService audio) => new(
        fs: fs, stateManager: state, gameLoop: null!, normalizer: null!, progressionSchedule: null!, ui: null!, explorer: null!,
        loc: new LocalizationManager(), saveLoad: null!, imageService: null!, validator: null!, charService: null!, storyService: null!,
        actorMemoryService: null!, audioService: audio, consoleAppearance: null!,
        systemModService: new SystemModService(fs, state.Settings, NullLogger<SystemModService>.Instance), systemGuardianLibraryService: null!,
        criticalStateHealth: null!, worldDirectiveService: null!, scenarioCoreService: null!, afterlifeArchiveCandidateService: null!,
        afterlifeReturnGuardService: null!, rivalSoulArcService: null!, guardianCorrectionService: null!, pendingTurnState: null!,
        qteSceneService: null!, clipboardService: null!, logger: NullLogger<GameEngine>.Instance);

    private sealed class RecordingApi(ISdlAudioApi? native, string mode) : ISdlAudioApi
    {
        internal int Initializes, Quits, Opens, Closes, Queues;
        internal float FirstSample, LaterSample;
        internal Action? AfterFirstQueue;
        public void Initialize() { Initializes++; if (mode == "sdl-missing") throw new AudioCapabilityException(AudioOutcome.BackendUnavailable); native?.Initialize(); }
        public void Quit() { Quits++; native?.Quit(); }
        public uint Open(int rate, int channels) { Opens++; if (mode == "sdl-no-device") throw new AudioCapabilityException(AudioOutcome.DeviceUnavailable); return native?.Open(rate, channels) ?? 1; }
        public void Pause(uint device, bool paused) => native?.Pause(device, paused);
        public void Queue(uint device, float[] samples, int count)
        {
            if (mode == "sdl-queue-error") throw new IOException("Synthetic queue failure.");
            Queues++; if (Queues == 1) FirstSample = samples[0]; else if (Queues == 2) LaterSample = samples[0];
            native?.Queue(device, samples, count); if (Queues == 1) AfterFirstQueue?.Invoke();
        }
        public uint QueuedBytes(uint device) => native?.QueuedBytes(device) ?? 0;
        public void Clear(uint device) => native?.Clear(device);
        public void Close(uint device) { Closes++; if (mode == "sdl-close-error") throw new IOException("Synthetic close failure."); native?.Close(device); }
    }
}
