using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private async Task<T> WithOwnedBackupLeaseAsync<T>(Func<CanonicalWriteLease, Task<T>> operation)
    {
        CanonicalWriteLease? lease = null;
        Exception? failure = null;
        var completed = false;
        try
        {
            lease = await AcquireCanonicalWriteLeaseAsync();
            var result = await operation(lease);
            completed = true;
            return result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (lease != null)
                await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(this, lease, completed, failure);
        }
    }

    // Call at immediate consumer boundaries before further reads or mutations.
    // Committed cleanup debt on this same lease must retain its classification
    // if recovery fails; ordinary input/schema errors are not converted here.
    internal void ResolveBackupPublicationRecovery(CanonicalWriteLease lease)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        lease.EnsureNoPendingLocalDecision();
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery)
        {
            VerifyCurrentSessionOperation(lease);
            return;
        }
        EnsureNoLegacyStorageEvidence(lease);
        try { RecoverTrustedLocalStorage(lease); }
        catch (Exception failure) { throw new CoordinatedStatePublicationUncertainException(failure); }
        // Retained debt has its own generation authority. Classify its conflict
        // before the ordinary bound-scope fence can hide it. With no debt, the
        // existing known session-replacement rejection remains unchanged.
        VerifyCurrentSessionOperation(lease);
    }

    private string GetLocalBackupRelativePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new InvalidDataException("Canonical backup path is required.");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var path = scope.ValidateFile(Path.GetFullPath(fullPath));
        var relative = GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows());
        EnsureSafeCanonicalRelativePath(relative);
        return relative;
    }

    private async Task<string?> CreateBackupWithLeaseAsync(CanonicalWriteLease lease, string relativePath)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        if (!UsesTrustedLocalWriter(lease, relativePath)) return CreateBackupCore(relativePath);
        var source = GetLocalBackupRelativePath(ResolvePath(relativePath));
        ResolveBackupPublicationRecovery(lease);
        var generation = ReadLocalGenerationSnapshot(lease);
        var bytes = await ReadLocalFileBytesAsync(lease, source);
        if (bytes == null) return null;
        var backup = source + $".backup.{Guid.NewGuid():N}";
        await PublishBackupMembersAsync(lease, generation,
            [new(ResolvePath(backup), null, bytes)],
            new(ResolvePath(source), bytes, bytes));
        return ResolvePath(backup);
    }

    private async Task RestoreBackupWithLeaseAsync(CanonicalWriteLease lease, string backupFullPath, string originalRelativePath)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        var backup = GetLocalBackupRelativePath(backupFullPath);
        var ordinaryBackup = UsesTrustedLocalWriter(lease, backup);
        var ordinaryTarget = UsesTrustedLocalWriter(lease, originalRelativePath);
        if (ordinaryBackup != ordinaryTarget)
            throw new InvalidOperationException("Backup restoration cannot mix original evidence and ordinary publication.");
        if (!ordinaryBackup) { RestoreBackupCore(backupFullPath, originalRelativePath); return; }
        var target = GetLocalBackupRelativePath(ResolvePath(originalRelativePath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(backup, target, comparison))
            throw new InvalidDataException("Backup and restoration target must be different names.");
        ResolveBackupPublicationRecovery(lease);
        var generation = ReadLocalGenerationSnapshot(lease);
        var bytes = await ReadLocalFileBytesAsync(lease, backup)
            ?? throw new FileNotFoundException("Canonical rollback before-image is missing.", backupFullPath);
        var before = await ReadLocalFileBytesAsync(lease, target);
        await PublishBackupMembersAsync(lease, generation,
            [new(ResolvePath(target), before, bytes), new(ResolvePath(backup), bytes, null)]);
    }

    private async Task CleanupBackupWithLeaseAsync(CanonicalWriteLease lease, string backupFullPath)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        var backup = GetLocalBackupRelativePath(backupFullPath);
        if (!UsesTrustedLocalWriter(lease, backup)) { CleanupBackupCore(backupFullPath); return; }
        ResolveBackupPublicationRecovery(lease);
        var generation = ReadLocalGenerationSnapshot(lease);
        var bytes = await ReadLocalFileBytesAsync(lease, backup);
        if (bytes == null) return;
        await PublishBackupMembersAsync(lease, generation, [new(ResolvePath(backup), bytes, null)]);
    }

    private async Task PublishBackupMembersAsync(CanonicalWriteLease lease, LocalSessionGenerationSnapshot generation,
        List<TrustedLocalFileChange> members, TrustedLocalFileChange? sourceGuard = null)
    {
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var guards = sourceGuard == null ? members.ToArray() : members.Append(sourceGuard).ToArray();
        void Revalidate()
        {
            VerifyCurrentSessionOperation(lease);
            if (ReadLocalGenerationSnapshot(lease).Binding != generation.Binding)
                throw new InvalidDataException("The session generation changed during backup preparation.");
            // Validate every path/type and exact capture after all boundary hooks,
            // before B1 can publish an intent. This includes source-only guards.
            foreach (var guard in guards)
            {
                var path = scope.ValidateFile(guard.Path);
                var exists = File.Exists(path);
                if (exists != (guard.Before != null) ||
                    (exists && !File.ReadAllBytes(path).AsSpan().SequenceEqual(guard.Before)))
                    throw new InvalidDataException("A backup member changed before publication.");
            }
        }
        Revalidate();
        if (!generation.Binding.Exists) members.Add(GenerationCreation(Guid.NewGuid().ToString("N")));
        var outcome = await PublishLocalCoreAsync(lease, generation.Binding, members, CancellationToken.None, () => { Revalidate(); return Task.CompletedTask; });
        if (outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain)
            throw new CoordinatedStatePublicationUncertainException(outcome.Failure);
        if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
        {
            RequireCommittedLocalPublication(outcome);
            return;
        }
        if (!generation.Binding.Exists) CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        if (outcome.Failure != null)
        {
            try { _logger.LogWarning(outcome.Failure, "Backup publication committed; journal cleanup remains pending."); }
            catch { /* Diagnostics cannot change the durable disposition. */ }
        }
    }
}
