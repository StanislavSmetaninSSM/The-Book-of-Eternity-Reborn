using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

/// <summary>Builds every required browser surface inside one exact-generation operation, never a partial load confirmation.</summary>
public sealed class BrowserLoadStateService(
    FileSystemManager files,
    LocalWebUiMainMenuService menu,
    LocalWebUiSessionStatusService session,
    BrowserGameScreenService game,
    BrowserClientSettingsService settings,
    BrowserAudioService audio)
{
    /// <summary>Confirms the requested replacement, or explicitly captures current authority for a known refusal.</summary>
    public async Task<BrowserLoadStateDto> BuildAsync(BrowserLoadStateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var generation = request.EstablishedGeneration;
        if (request.ReconcileCurrent)
        {
            if (generation != null) throw new InvalidDataException("Current reconciliation cannot replace an expected generation.");
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            generation = files.ReadExistingSessionGeneration(lease);
        }
        if (string.IsNullOrWhiteSpace(generation)) throw new InvalidDataException("An established generation is required.");
        // Builders acquire their own short leases in their existing lock order. Every authoritative
        // read and the final close must still match this exact binding; a rotation discards the bundle.
        return await SessionOperationContext.RunBoundAsync(files, generation, async () =>
        {
            // Menu options read the shared settings receiver, so refresh that receiver first.
            var settingsState = await settings.BuildAsync();
            var audioState = await audio.BuildSettingsAsync();
            var menuState = await menu.BuildAsync();
            var sessionState = await session.BuildStatusAsync();
            BrowserGameScreenDto? gameState = null;
            var noActiveSession = false;
            try { gameState = await game.BuildAsync(); }
            catch (BrowserNoActiveSessionException) { noActiveSession = true; }
            return new BrowserLoadStateDto(generation, menuState, sessionState, gameState,
                noActiveSession, settingsState, audioState);
        });
    }
}

/// <summary>Names an exact replacement generation, or requests an explicit current-state reconciliation after known non-loading.</summary>
public sealed record BrowserLoadStateRequest(string? EstablishedGeneration, bool ReconcileCurrent = false);

/// <summary>A complete generation-bound bundle; only a recognized absence of an active chapter permits a null game surface.</summary>
public sealed record BrowserLoadStateDto(string EstablishedGeneration, BrowserMainMenuDto Menu,
    LocalWebUiSessionStatus Session, BrowserGameScreenDto? Game, bool NoActiveSession,
    BrowserClientSettingsDto Settings, BrowserAudioSettingsDto Audio);
