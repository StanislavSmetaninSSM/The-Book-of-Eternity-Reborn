namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Standalone console/profile operations own only this registered member.
    // Browser and legacy transactions retain their original participant scope.
    internal void RequireStandaloneDarenProfile(CanonicalWriteLease lease)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        EnsureWorkerGeneralMutationAllowed(lease);
        lease.EnsureNoPendingLocalDecision();
        if (lease.BrowserLocalAccess != null ||
            lease.ExternalPublicationContext != null || lease.MutationIntentRecorder != null ||
            lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Standalone Daren profile requires its original ordinary lease.");
        new TrustedLocalFileScope([BasePath]).ValidateFile(RegisteredDarenProfilePath);
    }

    /// <summary>
    /// Publishes the registered standalone profile while preserving the absent-to-absent no-op behavior.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="content">
    /// New profile bytes, or <see langword="null"/> to request deletion.
    /// </param>
    /// <param name="token">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task PublishStandaloneDarenProfileAsync(CanonicalWriteLease lease, byte[]? content,
        CancellationToken token = default)
    {
        using var validationMutation = BeginValidationMutation(lease);
        RequireStandaloneDarenProfile(lease);
        token.ThrowIfCancellationRequested();
        var before = await ReadLocalDarenProfileAsync(lease, token);
        // Original absent -> absent rollback is a no-op, including generation,
        // parent, journal and mutation hooks. Admission itself still applies.
        if (before == null && content == null) return;
        var generation = GetOrCreateSessionGeneration(lease);
        RequireCommittedLocalPublication(await PublishLocalCoreAsync(lease,
            TrustedLocalGeneration.Existing(generation),
            [new(RegisteredDarenProfilePath, before, content)], token,
            standaloneDarenProfile: true));
    }
}
