using System.Text;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private static bool IsWorkerCleanupAudit(CanonicalWriteLease lease) =>
        lease.WorkerPurpose?.Operation == GmWorkerCanonicalOperation.ConfirmedCleanupAudit;

    // A cleanup lease is an operation capability, never a general canonical
    // writer. Check at participating mutation entrypoints before preparation.
    internal void EnsureWorkerGeneralMutationAllowed(CanonicalWriteLease lease)
    {
        EnsureValidCanonicalWriteLease(lease);
        if (IsWorkerCleanupAudit(lease))
            throw new InvalidOperationException("Cleanup audit authority cannot perform this canonical mutation.");
    }

    private void EnsureWorkerAuditAppend(CanonicalWriteLease lease, string path, ReadOnlySpan<byte> suffix)
    {
        if (!IsWorkerCleanupAudit(lease)) return;
        EnsureWorkerRecoveryAdmission(lease);
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery ||
            !string.Equals(path, GmWorkerAuditLog.AuditLogPath, StringComparison.Ordinal))
            throw new InvalidOperationException("Cleanup audit requires its exact ordinary audit path.");
        lease.WorkerPurpose!.Execution.ValidateCleanupAppend(lease.WorkerPurpose, suffix);
    }

    internal void EnsureWorkerAuditPublication(CanonicalWriteLease lease, TrustedLocalGeneration generation,
        IReadOnlyList<TrustedLocalFileChange> changes)
    {
        if (!IsWorkerCleanupAudit(lease)) return;
        EnsureWorkerRecoveryAdmission(lease);
        var purpose = lease.WorkerPurpose!;
        if (changes.Count != 1 || generation != TrustedLocalGeneration.Existing(purpose.Execution.Identity.GenerationId))
            throw new InvalidOperationException("Cleanup audit cannot publish another member or generation.");
        var change = changes[0];
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(change.Path), ResolvePath(GmWorkerAuditLog.AuditLogPath), comparison))
            throw new InvalidOperationException("Cleanup publication is outside the original audit path.");
        var prefix = change.Before ?? Encoding.UTF8.GetPreamble();
        if (change.After == null || change.After.Length < prefix.Length ||
            !change.After.AsSpan(0, prefix.Length).SequenceEqual(prefix))
            throw new InvalidOperationException("Cleanup audit must preserve every original prefix byte.");
        EnsureWorkerAuditAppend(lease, GmWorkerAuditLog.AuditLogPath, change.After.AsSpan(prefix.Length));
    }
}
