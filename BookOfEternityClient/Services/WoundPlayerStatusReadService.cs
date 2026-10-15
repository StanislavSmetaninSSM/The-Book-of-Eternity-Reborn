using BookOfEternityClient.Core;
using BookOfEternityClient.Models.GameState;

namespace BookOfEternityClient.Services;

internal sealed record WoundPlayerStatusSnapshot(AggregatedGameState State, WoundPlayerProjectionResult Wounds);

/// <summary>Captures the status header and wound view on one existing read-only publication boundary.</summary>
internal static class WoundPlayerStatusReadService
{
    internal static async Task<WoundPlayerStatusSnapshot> LoadAsync(StateManager stateManager, FileSystemManager fs)
    {
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(fs);
        await using var lease = await fs.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence);
        await stateManager.RefreshGameStateReadOnlyAsync(lease);
        var state = stateManager.CurrentState;
        var wounds = await WoundPlayerReadService.LoadAsync(fs, lease);
        return new(state, wounds);
    }
}
