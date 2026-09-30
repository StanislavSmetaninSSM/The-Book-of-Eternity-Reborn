namespace BookOfEternityClient.Core;

internal sealed record CanonicalLocalFileChange(string RelativePath, byte[]? Before, byte[]? After);

public partial class FileSystemManager
{
    internal Task RunLegacyStorageRecoveryAsync(CanonicalWriteLease lease, Func<Task> recovery) =>
        throw new NotImplementedException();

    internal string BootstrapLocalStorage(CanonicalWriteLease lease, byte[]? beforeConfig, byte[] desiredConfig) =>
        throw new NotImplementedException();

    internal Task<TrustedLocalPublicationOutcome> PublishLocalFilesAsync(CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> changes, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
