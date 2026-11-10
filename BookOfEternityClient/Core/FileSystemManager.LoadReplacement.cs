using System.Text.Json;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal string CreateRuntimeLoadStagingRoot() => CreateRuntimeStagingRoot("load-staging");

    internal IReadOnlyList<string> EnumerateLoadReplacementFiles(CanonicalWriteLease lease, string selectedSource)
    {
        EnsureValidSessionReplacementLease(lease);
        EnsureNoLegacyStorageEvidence();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var scope = new TrustedLocalFileScope([BasePath]);
        var library = TrustedLocalFilePublication.NormalizeAuthorityPath(ResolvePath("saves"), OperatingSystem.IsWindows());
        var source = TrustedLocalFilePublication.NormalizeAuthorityPath(selectedSource, OperatingSystem.IsWindows());
        return EnumerateLocalTreeFiles(scope, GameSessionPath, entry =>
        {
            var path = TrustedLocalFilePublication.NormalizeAuthorityPath(entry, OperatingSystem.IsWindows());
            // The entire canonical library is opaque to replacement, including unknown files.
            return path.Equals(library, comparison) || path.Equals(source, comparison);
        });
    }

    /// <summary>
    /// Publishes the complete prepared load image set under one tracked mutation lifetime.
    /// </summary>
    /// <param name="lease">
    /// Original session-replacement lease retained through preparation, retries and cleanup.
    /// </param>
    /// <param name="expectedGeneration">
    /// The exact generation image against which the original source must still agree.
    /// </param>
    /// <param name="completeChanges">
    /// Complete validated image cohort passed unchanged to the original publisher.
    /// </param>
    /// <param name="replacementGeneration">
    /// New canonical generation identifier required by the existing load contract.
    /// </param>
    /// <param name="validatePreparedNamespace">
    /// Existing synchronous source revalidator; it must not mutate the session.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before an established publication decision; default requests no cancellation.
    /// </param>
    /// <returns>
    /// The original established publication outcome, including rollback, uncertainty and cleanup failures.
    /// </returns>
    internal async Task<TrustedLocalPublicationOutcome> PublishLoadReplacementImagesAsync(
        CanonicalWriteLease lease, LocalSessionGenerationSnapshot expectedGeneration,
        IReadOnlyList<CanonicalLocalImageChange> completeChanges, string replacementGeneration,
        Action validatePreparedNamespace, CancellationToken cancellationToken = default)
    {
        if (SessionOperationContext.TryGetExpectedGeneration(BasePath, out _))
            throw new InvalidOperationException("Load cannot run inside a generation-bound session operation.");
        EnsureValidSessionReplacementLease(lease);
        using var validationMutation = BeginValidationMutation(lease);
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Ordinary load cannot use a legacy transaction lease.");
        ResolveBackupPublicationRecovery(lease);
        if (!Guid.TryParseExact(replacementGeneration, "N", out var parsed) || parsed.ToString("N") != replacementGeneration)
            throw new InvalidDataException("The replacement generation is invalid.");
        if (completeChanges.Count == 0 || completeChanges.Any(change => !UsesTrustedLocalWriter(lease, change.RelativePath)))
            throw new InvalidOperationException("Load requires a complete ordinary canonical replacement set.");

        var scope = new TrustedLocalFileScope([BasePath]);
        var members = completeChanges.Select(change => new TrustedLocalImageChange(
            ResolvePath(change.RelativePath), change.Before, change.After)).ToList();
        members.Add(new(SessionGenerationPath, TrustedLocalFileImage.FromBytes(expectedGeneration.Bytes),
            TrustedLocalFileImage.FromBytes(JsonSerializer.SerializeToUtf8Bytes(new SessionGenerationDocument(1, replacementGeneration)))));

        void Revalidate()
        {
            ResolveBackupPublicationRecovery(lease);
            var generation = ReadLocalGenerationSnapshot(lease);
            if (generation.Binding != expectedGeneration.Binding || !ExactBytesEqual(generation.Bytes, expectedGeneration.Bytes))
                throw new InvalidDataException("The generation changed during load preparation.");
            validatePreparedNamespace();
            // T032-B1 is same-shape only. Full topology conversion is the required T032-B2 gate.
            foreach (var directory in RequiredDirectories)
                scope.ValidateDirectory(Path.Combine(BasePath, directory.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var member in members)
            {
                scope.ValidateDirectory(Path.GetDirectoryName(member.Path)!);
                if (!member.Before.MatchesFile(scope, member.Path))
                    throw new InvalidDataException("A load member changed before publication.");
                member.After.Validate();
            }
        }

        var registrations = new List<InProcessMutationRegistration>();
        try
        {
            Revalidate();
            // Only harmless directory structure may precede the journal, after complete preflight.
            foreach (var directory in RequiredDirectories)
                scope.EnsureDirectory(Path.Combine(BasePath, directory.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var member in members)
            {
                scope.EnsureDirectory(Path.GetDirectoryName(member.Path)!);
                registrations.Add(new InProcessMutationRegistration(member.Path));
            }
            foreach (var change in completeChanges)
            {
                await InvokeBeforeCanonicalMutationBoundaryAsync(change.RelativePath);
                EnsureCanonicalMutationBoundary(change.RelativePath, ResolvePath(change.RelativePath));
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(change.RelativePath);
            }
            var publisher = new TrustedLocalFilePublication(this, scope);
            for (var attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Revalidate();
                var outcome = publisher.PublishImagesWithOutcome(lease, expectedGeneration.Binding, members, _hooks?.LocalPublicationObserver);
                if (outcome.Disposition == TrustedLocalPublicationDisposition.Committed)
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
