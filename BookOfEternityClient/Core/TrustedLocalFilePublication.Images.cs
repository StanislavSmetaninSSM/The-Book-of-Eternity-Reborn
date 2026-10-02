namespace BookOfEternityClient.Core;

internal sealed record TrustedLocalImageChange(string Path, TrustedLocalFileImage Before, TrustedLocalFileImage After);

internal sealed partial class TrustedLocalFilePublication
{
    internal TrustedLocalPublicationOutcome PublishImagesWithOutcome(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalImageChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null) =>
        PublishWithOutcome(lease, attempt =>
        {
            BeginPublication(lease, generation, changes.Count);
            var members = changes.Select(change => new Member
            {
                Path = ValidateMemberPath(change.Path), Before = change.Before, After = change.After
            }).ToArray();
            var format = members.Any(member => member.Before.IsFileBacked || member.After.IsFileBacked) ? 2 : 1;
            return PublishMembers(lease, generation, members, format, observer, attempt);
        });
}
