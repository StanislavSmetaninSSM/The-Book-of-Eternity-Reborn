using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Lease-local identities only. Fresh permission remains MainAdmission.Validate;
    // neither a retired registration nor a past successful observation grants it.
    internal sealed class RetiredWorkerRemoteAdmission
    {
        private readonly CanonicalWriteLease _lease;
        private readonly GmWorkerRootContext _registration;
        private readonly MainAdmission _main;
        private readonly object _access;
        private readonly GmMainOperationClient _remote;
        private readonly string _generation;
        internal RetiredWorkerRemoteAdmission(CanonicalWriteLease lease, GmWorkerRootContext registration,
            MainAdmission main, object access, GmMainOperationClient remote)
        {
            _lease = lease; _registration = registration; _main = main;
            _access = access; _remote = remote; _generation = remote.Identity.GenerationId;
        }
        internal void RequireLocalPair(FileSystemManager files, CanonicalWriteLease lease)
        {
            if (!ReferenceEquals(lease, _lease) || !ReferenceEquals(files, lease.Owner) || !lease.IsActive ||
                !ReferenceEquals(lease.MainAdmission, _main))
                throw GmSessionRunPersistence.Invalid();
            _registration.RequireRetiredRemoteCandidate(files);
            _main.RequireRetiredRemoteAccess(lease, _access, _remote, _generation);
        }
    }

    internal sealed partial class MainAdmission
    {
        internal bool TryBindRetiredWorker(CanonicalWriteLease lease, GmWorkerRootContext registration)
        {
            if (!registration.IsRetired) return false;
            registration.RequireRetiredRemoteCandidate(_files);
            var access = _access ?? throw GmSessionRunPersistence.Invalid();
            var remote = access.Remote ?? throw GmSessionRunPersistence.Invalid();
            RequireRetiredRemoteAccess(lease, access, remote, remote.Identity.GenerationId);
            var binding = new RetiredWorkerRemoteAdmission(lease, registration, this, access, remote);
            lease.RetiredWorkerAdmission = binding;
            // Identity registration is not a grant. The immediately following
            // ordinary recovery/finalization checkpoint performs the existing
            // fresh Validate, including both registration checks, before effects.
            return true;
        }
        internal void RequireRetiredRemoteAccess(CanonicalWriteLease lease, object access,
            GmMainOperationClient remote, string generation)
        {
            if (_closed || !ReferenceEquals(_access, access) || !ReferenceEquals(_access?.Remote, remote) ||
                _access?.WorkerObservationRequired != true || _workerPurpose != null || lease.WorkerPurpose != null ||
                !ReferenceEquals(lease.MainAdmission, this) || RootIdentity != lease.Owner.CanonicalRootAuthorityIdentity ||
                remote.Identity.GenerationId != generation)
                throw GmSessionRunPersistence.Invalid();
            if (lease.Purpose == CanonicalWritePurpose.SessionFinalization)
            {
                if (!BoundClosing || !Closing) throw GmSessionRunPersistence.Invalid();
            }
            else if (lease.Purpose is not (CanonicalWritePurpose.SessionMutation or CanonicalWritePurpose.PublicationReadQuiescence) ||
                     Closing || BoundClosing)
                throw GmSessionRunPersistence.Invalid();
        }
    }
}
