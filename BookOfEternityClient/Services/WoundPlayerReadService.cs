using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>Coherent read-only wound status; never issues command or healing authority.</summary>
internal static class WoundPlayerReadService
{
    internal static Task<WoundPlayerProjectionResult> LoadAsync(FileSystemManager fs) =>
        Task.FromResult(WoundPlayerProjection.Unavailable());
}
