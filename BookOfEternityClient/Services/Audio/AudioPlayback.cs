namespace BookOfEternityClient.Services;

/// <summary>Result of the latest owned playback request; cleanup debt overrides later requests.</summary>
public enum AudioOutcome { NotRequested, Muted, NoAssets, BackendUnavailable, DeviceUnavailable, Playing, Stopped, Canceled, Error, CleanupUncertain, Disposed, BrowserManaged }
/// <summary>Declared output route, separate from physical playback qualification.</summary>
public enum AudioBackendKind { Unavailable, WindowsWaveOut, LinuxSdl, Browser }
/// <summary>Immutable, safe capability status for existing console presentation.</summary>
public sealed record AudioStatus(AudioOutcome Outcome, AudioBackendKind Backend)
{
    /// <summary>Short diagnostic without native errors, paths or asset content.</summary>
    public string Message => Outcome switch
    {
        AudioOutcome.BackendUnavailable => "Аудио недоступно: совместимый backend не найден.",
        AudioOutcome.DeviceUnavailable => "Аудио недоступно: нет доступного аудиовыхода.",
        AudioOutcome.Error => "Не удалось воспроизвести аудио.",
        AudioOutcome.CleanupUncertain => "Завершение аудио не подтверждено; новое воспроизведение приостановлено.",
        _ => ""
    };
}

internal interface IAudioPlaybackBackend
{
    AudioBackendKind Kind { get; }
    // Construction is inert; native work and disposal are performed by the original operation.
    IAudioPlaybackSession Create(string path, Func<float> volume);
}
internal interface IAudioPlaybackSession : IAsyncDisposable
{
    Task RunAsync(Action playing, CancellationToken cancellationToken);
}
internal sealed class AudioCapabilityException(AudioOutcome outcome) : Exception
{
    internal AudioOutcome Outcome { get; } = outcome;
}
internal static class AudioBackendFactory
{
    internal static IAudioPlaybackBackend Create() => OperatingSystem.IsWindows()
        ? new WindowsAudioBackend() : OperatingSystem.IsLinux() ? new SdlAudioBackend(new SdlAudioApi()) : new UnavailableAudioBackend();
    private sealed class UnavailableAudioBackend : IAudioPlaybackBackend
    {
        public AudioBackendKind Kind => AudioBackendKind.Unavailable;
        public IAudioPlaybackSession Create(string path, Func<float> volume) => throw new AudioCapabilityException(AudioOutcome.BackendUnavailable);
    }
}
