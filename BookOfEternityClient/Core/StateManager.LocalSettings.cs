using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Core;

internal sealed record LocalSettingsSnapshot(byte[] Bytes, GameSettings Settings);

public partial class StateManager
{
    internal Task<LocalSettingsSnapshot> ReadLocalSettingsAsync(FileSystemManager.CanonicalWriteLease lease) =>
        throw new NotImplementedException();

    internal byte[] EncodeLocalSettings(GameSettings settings) => throw new NotImplementedException();
}
