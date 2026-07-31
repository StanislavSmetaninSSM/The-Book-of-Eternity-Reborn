using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

internal static class SessionReplacementTestHarness
{
    internal static async Task<string> RotateGenerationAsync(FileSystemManager fileSystem)
    {
        await using var lifecycleLease = await fileSystem.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease =
            await fileSystem.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);
        return fileSystem.RotateSessionGeneration(replacementLease);
    }
}
