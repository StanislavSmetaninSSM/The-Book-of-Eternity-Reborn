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
        if (!OperatingSystem.IsLinux() || lease.BrowserLocalAccess != null ||
            lease.ExternalPublicationContext != null || lease.MutationIntentRecorder != null ||
            lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Standalone Daren profile requires its original ordinary Linux lease.");
        new TrustedLocalFileScope([BasePath]).ValidateFile(RegisteredDarenProfilePath);
    }

    internal async Task PublishStandaloneDarenProfileAsync(CanonicalWriteLease lease, byte[]? content,
        CancellationToken token = default)
    {
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
