using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Core;

// Visual/audio preview lifetime only. Accepted settings stay owned by StateManager.
internal sealed class ConsoleSettingsPreview : IAsyncDisposable
{
    private readonly GameSettings _accepted;
    private readonly GameSettings _draft;
    private readonly LocalizationManager _localization;
    private readonly AudioService _audio;
    private readonly ConsoleAppearanceService _appearance;
    private readonly Func<GameSettings, Task> _refreshPlayback;
    private IDisposable? _audioPreview;
    private bool _disposed;

    internal ConsoleSettingsPreview(GameSettings accepted, GameSettings draft, LocalizationManager localization,
        AudioService audio, ConsoleAppearanceService appearance, Func<GameSettings, Task> refreshPlayback)
    {
        _accepted = accepted;
        _draft = draft;
        _localization = localization;
        _audio = audio;
        _appearance = appearance;
        _refreshPlayback = refreshPlayback;
        _audioPreview = audio.BeginSettingsPreview(draft);
    }

    internal async Task<bool> ApplyAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _audioPreview ??= _audio.BeginSettingsPreview(_draft);
        _localization.CurrentLanguage = _draft.Language;
        var fontApplied = _appearance.TryPreviewFontSize(_draft.ConsoleFontSize);
        await _audio.ApplySettingsAsync();
        await _refreshPlayback(_draft);
        return fontApplied;
    }

    internal Task RestoreLastAcceptedEffectsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return RestoreEffectsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await RestoreEffectsAsync();
    }

    private async Task RestoreEffectsAsync()
    {
        // This restores the last accepted runtime effects, not a proof of the
        // current disk state. Keep the draft available for explicit later use.
        _audioPreview?.Dispose();
        _audioPreview = null;
        _localization.CurrentLanguage = _accepted.Language;
        _appearance.ApplyConfiguredFontSize();
        // Even if a backend cannot stop cleanly, still attempt to restore the
        // accepted playlist context. File acceptance is unaffected by either.
        try { await _audio.ApplySettingsAsync(); }
        finally { await _refreshPlayback(_accepted); }
    }
}
