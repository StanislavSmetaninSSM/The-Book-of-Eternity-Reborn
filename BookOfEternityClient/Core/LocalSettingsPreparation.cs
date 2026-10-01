using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

internal sealed record LocalSettingsBaseline(string Generation, byte[] ConfigBytes);
internal sealed record PreparedLocalSettings(GameSettings Settings, IReadOnlyList<CanonicalLocalFileChange> Changes);

/// <summary>Prepares the complete settings/projection/mod-manifest set without publishing or changing runtime settings.</summary>
internal sealed class LocalSettingsPreparation
{
    internal const string ProjectionPath = "game_state/core/game_settings.json";
    private readonly FileSystemManager _files;
    private readonly StateManager _state;
    private readonly SystemModService _mods;

    internal LocalSettingsPreparation(FileSystemManager files, StateManager state, SystemModService mods)
    {
        _files = files;
        _state = state;
        _mods = mods;
    }

    internal Task<PreparedLocalSettings> PrepareAsync(FileSystemManager.CanonicalWriteLease lease,
        LocalSettingsBaseline baseline, GameSettings requested) =>
        throw new NotImplementedException("Complete local settings preparation is not implemented.");
}
