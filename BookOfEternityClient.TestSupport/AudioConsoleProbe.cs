using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace BookOfEternityClient.Tests;

/// <summary>Own child denies all default audio native bindings before device initialization.</summary>
public static class AudioConsoleProbe
{
    /// <summary>Executes original public service and real browser registration on synthetic WAV only.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args[3] is not ("console" or "browser" or "browser-settings")) return await AudioLifecycleProbe.RunAsync(args);
        using var deadline = new Timer(_ => Environment.Exit(91), null, TimeSpan.FromSeconds(12), Timeout.InfiniteTimeSpan);
        var root = args[0]; var browser = args[3].StartsWith("browser", StringComparison.Ordinal);
        var windows = 0; var sdl = 0;
        NativeLibrary.SetDllImportResolver(typeof(WaveOutEvent).Assembly, (name, _, _) =>
        {
            Interlocked.Increment(ref windows);
            throw new DllNotFoundException("Owned synthetic fixture denies Windows audio binding.");
        });
        NativeLibrary.SetDllImportResolver(typeof(AudioService).Assembly, (name, _, _) =>
        {
            if (!name.Contains("SDL", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
            Interlocked.Increment(ref sdl);
            throw new DllNotFoundException("Owned synthetic fixture denies SDL initialization.");
        });
        var sounds = Path.Combine(root, "Sounds"); Directory.CreateDirectory(sounds);
        using (var w = new WaveFileWriter(Path.Combine(sounds, "sound-notification.wav"), new WaveFormat(8000, 16, 1)))
            w.Write(new byte[1600], 0, 1600);
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var settings = new GameSettings { MusicEnabled = false, SoundEnabled = true, SoundVolume = 50 };
        await using var host = browser ? LocalWebUiHost.Build([], new(root, "http://127.0.0.1:0")) : null;
        AudioService audio;
        if (browser)
        {
            var live = host!.Services.GetRequiredService<GameSettings>();
            live.MusicEnabled = false; live.SoundEnabled = true; live.SoundVolume = 50;
            audio = host.Services.GetRequiredService<AudioService>();
        }
        else audio = new AudioService(fs, settings, NullLogger<AudioService>.Instance);
        await audio.ApplySettingsAsync();
        bool committed = false;
        if (args[3] == "browser-settings")
        {
            await host!.Services.GetRequiredService<StateManager>().BootstrapLocalStorageAsync();
            var service = host.Services.GetRequiredService<BrowserAudioService>();
            var response = await service.UpdateSettingsAsync(new(true, 77, true, 35));
            var saved = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(Path.Combine(root, "config.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            committed = response.MusicVolume == 77 && saved.MusicVolume == 77;
            await audio.PlayMainMenuMusicAsync(); await audio.PlayInGameMusicAsync();
        }
        audio.PlayCue(AudioCue.TurnReady);
        string? outcome = null;
        for (var i = 0; i < 200; i++)
        {
            var status = typeof(AudioService).GetProperty("Status")?.GetValue(audio);
            outcome = status?.GetType().GetProperty("Outcome")?.GetValue(status)?.ToString();
            if (windows != 0 || outcome is "BackendUnavailable" or "BrowserManaged") break;
            await Task.Delay(10);
        }
        await audio.StopAllAsync();
        File.WriteAllText(Path.Combine(root, "probe.json"), JsonSerializer.Serialize(new
        {
            Mode = args[3], WindowsAttempts = windows, SdlAttempts = sdl, Outcome = outcome,
            HostPid = Environment.ProcessId, DefaultDeviceCalls = 0, SyntheticOnly = true, Committed = committed
        }));
        return 0;
    }
}
