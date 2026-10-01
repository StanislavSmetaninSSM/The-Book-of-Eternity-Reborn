using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public sealed partial class AudioService
{
    // Scoped settings source only; the audio backend is unchanged.
    internal IDisposable BeginSettingsPreview(GameSettings draft) => throw new NotImplementedException();
}
