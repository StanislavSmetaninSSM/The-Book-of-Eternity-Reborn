namespace BookOfEternityClient.Core;

internal sealed record CanonicalLocalImageChange(string RelativePath, TrustedLocalFileImage Before, TrustedLocalFileImage After);

public partial class FileSystemManager
{
    internal async Task<TrustedLocalPublicationOutcome> PublishLocalImageFilesAsync(CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalImageChange> changes, CancellationToken cancellationToken = default)
    {
        // The established helper resolves retained B1 debt before its ordinary
        // generation fence. Its name reflects the first migrated consumer;
        // the recovery authority and typed uncertainty are shared B1 behavior.
        ResolveBackupPublicationRecovery(lease);
        if (changes.Count == 0 || changes.Any(change => !UsesTrustedLocalWriter(lease, change.RelativePath)))
            throw new InvalidOperationException("An image publication requires ordinary declared canonical members.");
        var generation = ReadLocalGenerationSnapshot(lease);
        var members = changes.Select(change => new TrustedLocalImageChange(
            ResolvePath(change.RelativePath), change.Before, change.After)).ToList();
        if (!generation.Binding.Exists)
        {
            var created = GenerationCreation(Guid.NewGuid().ToString("N"));
            members.Add(new(created.Path, TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes(created.After)));
        }
        var scope = new TrustedLocalFileScope([BasePath]);
        var registrations = new List<InProcessMutationRegistration>();
        void Revalidate()
        {
            ResolveBackupPublicationRecovery(lease);
            if (ReadLocalGenerationSnapshot(lease).Binding != generation.Binding)
                throw new InvalidDataException("The session generation changed during image preparation.");
            foreach (var member in members)
                if (!member.Before.MatchesFile(scope, member.Path))
                    throw new InvalidDataException("An image member changed before publication.");
        }
        try
        {
            foreach (var member in members)
            {
                scope.EnsureDirectory(Path.GetDirectoryName(member.Path)!);
                scope.ValidateFile(member.Path);
                registrations.Add(new InProcessMutationRegistration(member.Path));
            }
            foreach (var change in changes)
            {
                var canonicalPath = ResolvePath(change.RelativePath);
                await InvokeBeforeCanonicalMutationBoundaryAsync(change.RelativePath);
                EnsureCanonicalMutationBoundary(change.RelativePath, canonicalPath);
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(change.RelativePath);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var publisher = new TrustedLocalFilePublication(this, scope);
            for (var attempt = 0; ; attempt++)
            {
                Revalidate();
                var outcome = publisher.PublishImagesWithOutcome(lease, generation.Binding, members, _hooks?.LocalPublicationObserver);
                if (outcome.Disposition == TrustedLocalPublicationDisposition.Committed && !generation.Binding.Exists)
                    CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
                if (outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack ||
                    outcome.Failure is InvalidDataException || outcome.Failure == null ||
                    !IsTransientFileAccessException(outcome.Failure) || attempt >= TransientFileAccessRetryCount - 1)
                    return outcome;
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
            }
        }
        finally { foreach (var registration in registrations) registration.Dispose(); }
    }
}
