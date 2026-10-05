using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

// Only the explicit, fixture-bound native admission constructs this adapter.
// The default Store still uses its original descriptor-bound publication method.
internal sealed class GmWorkerSyntheticBundlePublication(string fixtureRoot)
{
    internal Task PublishAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string sourceDirectory, string destinationRelativePath) =>
        fs.MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(lease, fixtureRoot,
            sourceDirectory, destinationRelativePath);

    internal void Cleanup(FileSystemManager fs, string stagingRoot) =>
        fs.DeleteSyntheticRuntimeProposalStagingRoot(fixtureRoot, stagingRoot);
}
