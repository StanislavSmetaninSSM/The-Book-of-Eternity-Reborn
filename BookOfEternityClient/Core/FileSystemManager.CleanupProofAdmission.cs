namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    internal sealed partial class MainAdmission
    {
        /// <summary>
        /// Captures an exact prepared publisher version without making generic root capture eligible.
        /// </summary>
        /// <param name="lease">
        /// Original ordinary physical lease belonging to this admission.
        /// </param>
        /// <param name="outer">
        /// Actual outer publisher mutation token.
        /// </param>
        /// <param name="core">
        /// Actual inner publisher mutation token.
        /// </param>
        /// <param name="reservation">
        /// This window's installed reservation, or <see langword="null"/> before its atomic installation.
        /// </param>
        /// <param name="version">
        /// Current exact root version on success, otherwise <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for the exclusive live ancestry and exact complete mutation sets; <see langword="false"/> grants no authority.
        /// </returns>
        internal bool TryCapturePreparedProofVersion(CanonicalWriteLease lease, MainMutationReservation outer,
            MainMutationReservation core, ValidationReadReservation? reservation, out object? version)
        {
            var ancestors = new HashSet<MainAdmission>(ReferenceEqualityComparer.Instance);
            for (var current = MainAdmissions.Value; current != null; current = current.Parent) ancestors.Add(current);
            version = null;
            return ancestors.Contains(this) && _access != null && _access.TryCapturePreparedProofVersion(
                this, _files, lease, outer, core, reservation, ancestors, out version);
        }
    }

    private sealed partial class MainAccess
    {
        /// <summary>
        /// Checks the narrow prepared-publisher predicate under the original shared state lock.
        /// </summary>
        /// <param name="admission">
        /// Exact physical owner retained by the original lease.
        /// </param>
        /// <param name="files">
        /// Manager owning the lease and root.
        /// </param>
        /// <param name="lease">
        /// Active ordinary lease with no special authority or pending decisions.
        /// </param>
        /// <param name="outer">
        /// Outer publisher's actual retained token.
        /// </param>
        /// <param name="core">
        /// Core publisher's distinct retained token.
        /// </param>
        /// <param name="reservation">
        /// Exact compatible reservation, or <see langword="null"/> before installation.
        /// </param>
        /// <param name="ancestors">
        /// Complete live entry ancestry; unrelated borrowers are ineligible.
        /// </param>
        /// <param name="version">
        /// Exact opaque root version on success, otherwise <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for eligible ordinary access with the exact two local and root mutation identities;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        internal bool TryCapturePreparedProofVersion(MainAdmission admission, FileSystemManager files,
            CanonicalWriteLease lease, MainMutationReservation outer, MainMutationReservation core,
            ValidationReadReservation? reservation, HashSet<MainAdmission> ancestors, out object? version)
        {
            lock (_state)
            {
                version = null;
                if (_frozen || _references == 0 || owner.Closed || !_admissions.Contains(admission) || !admission.PhysicalScope ||
                    !ReferenceEquals(lease.Owner, files) || !ReferenceEquals(lease.MainAdmission, admission) ||
                    !lease.IsActive || lease.Purpose != CanonicalWritePurpose.SessionMutation ||
                    lease.WorkerPurpose != null || lease.WorkerRootPin != null || lease.PendingLocalDecision != null ||
                    lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery || lease.BrowserLocalAccess != null ||
                    files.HasBrowserOriginalOperation || remote != null || Workers != null || workerObservationRequired ||
                    original?.MetadataOnly == true || original?.Owner.AdmissionClosed == true ||
                    owner.RootIdentity.WorkerContext != null || _physicalClosing.Count != 0 ||
                    ReferenceEquals(outer, core) || _mutations.Count != 2 || !_mutations.Contains(outer) || !_mutations.Contains(core) ||
                    (reservation == null ? _worldRead != null :
                        !ReferenceEquals(_worldRead, reservation) || reservation.Closed ||
                        !ReferenceEquals(reservation.RetainedPhysicalAdmission, admission)) ||
                    _admissions.Any(p => p.Closed || p.Closing || p.BoundClosing || p.MetadataOnly || !ancestors.Contains(p) ||
                        p.PhysicalScope && !ReferenceEquals(p, admission))) return false;
                return owner.RootIdentity.ValidationEpoch.TryCapturePreparedPublisher(outer, core, out version);
            }
        }
    }
}
