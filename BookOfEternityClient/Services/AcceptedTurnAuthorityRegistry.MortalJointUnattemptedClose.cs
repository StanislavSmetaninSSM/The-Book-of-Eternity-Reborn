using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    private static readonly object JointUnattemptedCloseIssuer = new();

    private sealed record JointNoNativeAttemptReceipt(GameEngine.MortalJointPublicationAttempt Attempt,
        object Continuation, object Reservation);

    internal sealed class JointUnattemptedCloseResult
    {
        private readonly JointNoNativeAttemptReceipt? _receipt;
        internal JointUnattemptedCloseResult(object issuer, string disposition, object? receipt = null)
        {
            if (!ReferenceEquals(issuer, JointUnattemptedCloseIssuer) ||
                disposition == "ClosedNoNativeAttempt" && receipt is not JointNoNativeAttemptReceipt)
                throw new InvalidOperationException("Joint close result requires its actual registry owner.");
            Disposition = disposition; _receipt = receipt as JointNoNativeAttemptReceipt;
        }
        internal string Disposition { get; }
    }

    private static JointUnattemptedCloseResult UnattemptedUnavailable(string disposition) =>
        new(JointUnattemptedCloseIssuer, disposition);

    internal static JointUnattemptedCloseResult CloseOwnedJointAttemptUnattempted(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, GameEngine.MortalJointPublicationAttempt attempt)
    {
        if (attempt.Delegated) return UnattemptedUnavailable("UnavailableDelegated");
        if (attempt.Continuation is null || attempt.Reservation is null)
            return UnattemptedUnavailable("UnavailableNotReserved");
        try
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (lease.PendingLocalDecision is not null) return UnattemptedUnavailable("UnavailablePendingDecision");
            lease.EnsureNoPendingLocalDecision();
            if (lease.IsLegacyStorageRecovery || lease.MutationIntentRecorder is not null || lease.BrowserLocalAccess is not null ||
                lease.MainAdmission is null) return UnattemptedUnavailable("UnavailableForeignOwner");
            lease.MainAdmission.Validate(lease);
            if (!attempt.IsCurrentOwner(files, lease)) return UnattemptedUnavailable("UnavailableForeignOwner");
            attempt.RequireActualOwner(files, lease);
            // Inspect an existing slot only. A failed close must never mint G or
            // replace a registry state, even when the old owner is absent.
            var generation = files.ReadExistingSessionGeneration(lease);
            if (generation is null || generation != attempt.Generation ||
                !RootSlots.TryGetValue(files.CanonicalRootAuthorityIdentity, out var slot))
                return UnattemptedUnavailable("UnavailableOwnerLost");
            return slot.CloseOwnedJointAttemptUnattempted(files, lease, generation,
                files.CanonicalRootAuthorityIdentity.SessionGenerationRevision, attempt);
        }
        catch (Exception error) when (error is not SessionReplacedException &&
            error is not CoordinatedStatePublicationUncertainException &&
            error is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
        { return UnattemptedUnavailable("UnavailableSourceFault"); }
    }

    private sealed partial class AcceptedTurnAuthorityState
    {
        internal JointUnattemptedCloseResult CloseOwnedJointAttemptUnattempted(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, GameEngine.MortalJointPublicationAttempt attempt)
        {
            lock (_gate)
            {
                if (attempt.Delegated) return UnattemptedUnavailable("UnavailableDelegated");
                if (!attempt.IsCurrentOwner(files, lease)) return UnattemptedUnavailable("UnavailableForeignOwner");
                if (_treatmentPublicationRestartBlockerFingerprint is not null ||
                    _openTreatmentPublicationReceipt is not null || _openJointRepairPublicationReceipt is not null)
                    return UnattemptedUnavailable("UnavailableOwnerLost");
                if (attempt.Continuation is not { } continuation || attempt.Reservation is not { } reservation)
                    return UnattemptedUnavailable("UnavailableNotReserved");
                if (!JointRepairPublicationReservationAgrees(continuation, reservation))
                    return UnattemptedUnavailable("UnavailableForeignOwner");
                var receipt = new JointNoNativeAttemptReceipt(attempt, continuation, reservation);
                ClearMortalWoundTreatmentPublicationReservationCore();
                return new(JointUnattemptedCloseIssuer, "ClosedNoNativeAttempt", receipt);
            }
        }
    }
}
