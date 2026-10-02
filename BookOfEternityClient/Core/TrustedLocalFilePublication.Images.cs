namespace BookOfEternityClient.Core;

internal sealed record TrustedLocalImageChange(string Path, TrustedLocalFileImage Before, TrustedLocalFileImage After);

internal sealed partial class TrustedLocalFilePublication
{
    internal TrustedLocalPublicationOutcome PublishImagesWithOutcome(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalImageChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null) =>
        throw new NotImplementedException("T032-A1 image publication scaffold");
}
