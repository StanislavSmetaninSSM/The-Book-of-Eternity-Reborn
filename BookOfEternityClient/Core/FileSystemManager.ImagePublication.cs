namespace BookOfEternityClient.Core;

internal sealed record CanonicalLocalImageChange(string RelativePath, TrustedLocalFileImage Before, TrustedLocalFileImage After);

public partial class FileSystemManager
{
    internal Task<TrustedLocalPublicationOutcome> PublishLocalImageFilesAsync(CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalImageChange> changes, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("T032-A1 canonical image adapter scaffold");
}
