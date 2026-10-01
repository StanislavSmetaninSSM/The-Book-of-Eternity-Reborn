using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Core;

// Visual/audio preview lifetime only. Accepted settings stay owned by StateManager.
internal sealed class ConsoleSettingsPreview : IAsyncDisposable
{
    internal ConsoleSettingsPreview(GameSettings accepted, GameSettings draft, LocalizationManager localization,
        AudioService audio, ConsoleAppearanceService appearance, Func<GameSettings, Task> refreshPlayback)
        => throw new NotImplementedException();

    internal Task<bool> ApplyAsync() => throw new NotImplementedException();
    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
