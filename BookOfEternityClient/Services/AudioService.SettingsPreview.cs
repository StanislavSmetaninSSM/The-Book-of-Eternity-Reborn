using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public sealed partial class AudioService
{
    private SettingsPreview? _settingsPreview;
    private GameSettings CurrentSettings => Volatile.Read(ref _settingsPreview)?.Settings ?? _settings;

    // Scoped settings source only; the audio backend is unchanged. A menu must
    // dispose its preview before another menu can become the preview owner.
    internal IDisposable BeginSettingsPreview(GameSettings draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var preview = new SettingsPreview(this, draft);
        if (Interlocked.CompareExchange(ref _settingsPreview, preview, null) != null)
            throw new InvalidOperationException("An audio settings preview is already active.");
        return preview;
    }

    private sealed class SettingsPreview(AudioService owner, GameSettings settings) : IDisposable
    {
        internal GameSettings Settings { get; } = settings;
        public void Dispose() => Interlocked.CompareExchange(ref owner._settingsPreview, null, this);
    }
}
