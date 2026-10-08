namespace BookOfEternityClient.Core;

internal sealed class WorkerApplyAdmissionConflictException(string path)
    : InvalidOperationException($"canonical file changed concurrently before worker apply: {path}.");

public partial class FileSystemManager
{
    /// <summary>Applies the whole ordinary worker set under a pending B1 decision, retaining the caller's lease for validation.</summary>
    internal async Task<CanonicalWorkerApplyTransaction> BeginWorkerApplyTransactionAsync(
        CanonicalWriteLease writeLease, IReadOnlyList<CanonicalWorkerApplyChange> changes)
    {
        EnsureWorkerGeneralMutationAllowed(writeLease);
        writeLease.EnsureNoPendingLocalDecision();
        if (writeLease.IsLegacyStorageRecovery || writeLease.MutationIntentRecorder != null)
            throw new InvalidOperationException("An ordinary worker cannot begin inside an original recovery or recorder transaction.");
        if (changes.Count == 0) throw new ArgumentException("Worker apply requires a complete nonempty member set.", nameof(changes));
        VerifyCurrentSessionOperation(writeLease);
        EnsureNoLegacyStorageEvidence();
        var generation = ReadExistingSessionGeneration(writeLease)
            ?? throw new InvalidOperationException("An ordinary worker requires an existing session generation.");
        var scope = new TrustedLocalFileScope([BasePath]);
        var registrations = new List<InProcessMutationRegistration>();
        var members = new List<TrustedLocalFileChange>(changes.Count);
        try
        {
            foreach (var change in changes)
            {
                EnsureSafeCanonicalRelativePath(change.Path);
                if (!UsesTrustedLocalWriter(writeLease, change.Path))
                    throw new InvalidOperationException("A worker member belongs to an original transaction boundary.");
                var path = ResolvePath(change.Path);
                scope.EnsureDirectory(Path.GetDirectoryName(path)!);
                scope.ValidateFile(path);
                registrations.Add(new InProcessMutationRegistration(path));
                members.Add(new(path, change.BaselineBytes, change.AppliedBytes));
            }
            foreach (var change in changes)
            {
                await InvokeBeforeCanonicalMutationBoundaryAsync(change.Path);
                EnsureCanonicalMutationBoundary(change.Path, ResolvePath(change.Path));
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(change.Path);
            }
            VerifyCurrentSessionOperation(writeLease);
            // Only this complete, pre-intent comparison establishes a safe rejection.
            // Publication/recovery errors after intent remain distinct and may be uncertain.
            foreach (var change in changes)
                if (!ExactBytesEqual(await ReadLocalFileBytesAsync(writeLease, change.Path), change.BaselineBytes))
                    throw new WorkerApplyAdmissionConflictException(change.Path);
            var publisher = new TrustedLocalFilePublication(this, scope);
            var decision = publisher.BeginDeferred(writeLease, TrustedLocalGeneration.Existing(generation), members,
                _hooks?.LocalPublicationObserver);
            return new(decision.TransactionId, LocalPublicationRoot, decision);
        }
        finally { foreach (var registration in registrations) registration.Dispose(); }
    }
}
