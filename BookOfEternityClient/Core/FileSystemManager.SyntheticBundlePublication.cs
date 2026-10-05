namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal Task MoveSyntheticRuntimeBundleIntoCanonicalSessionAsync(CanonicalWriteLease lease,
        string admittedFixtureRoot, string sourceDirectory, string destinationRelativePath) =>
        throw new PlatformNotSupportedException("Synthetic bundle publication is not qualified.");
}
