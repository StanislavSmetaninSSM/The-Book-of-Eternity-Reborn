namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Reserves the caller's existing console access and installs acquisition in this execution flow.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation for the ordinary main and canonical lease acquisition.
    /// </param>
    /// <returns>
    /// The reservation, which must be opened and disposed even when acquisition fails.
    /// </returns>
    internal PendingSnapshotValidationReadScope BeginPendingSnapshotValidationRead(CancellationToken cancellationToken)
    {
        if (HasBrowserOriginalOperation || HasAmbientCanonicalLease())
            throw new InvalidOperationException("Pending snapshot validation requires an unleased console owner.");
        var owner = MainAdmissions.Value;
        for (var p = owner; p != null; p = p.Parent)
            if (p.Closed && p.OwnsRemote && p.RootIdentity == CanonicalRootAuthorityIdentity)
                throw new InvalidOperationException("The original main operation has closed.");
        while (owner != null && (owner.Closed || owner.RootIdentity != CanonicalRootAuthorityIdentity || !owner.Acquired))
            owner = owner.Parent;
        if (owner == null || owner.Closing || owner.BoundClosing || owner.MetadataOnly)
            throw new InvalidOperationException("Pending snapshot validation has no live console owner.");
        owner.Validate(null);
        var scope = new PendingSnapshotValidationReadScope(this, owner);
        owner.ReserveWorldRead(scope);
        try
        {
            scope.Acquisition = AcquireCanonicalWriteLeaseWithAmbientAsync(
                CanonicalWritePurpose.PublicationReadQuiescence, cancellationToken, worldIssuance: scope);
            return scope;
        }
        catch { owner.ReleaseWorldRead(scope); throw; }
    }

    /// <summary>
    /// Reserves the exact caller-owned lease without acquisition, recovery or ownership transfer.
    /// </summary>
    /// <param name="writeLease">
    /// Active ordinary console lease belonging to this manager and execution flow.
    /// </param>
    /// <returns>
    /// A scope that releases only its read reservation when disposed.
    /// </returns>
    internal PendingSnapshotValidationReadScope BeginBorrowedPendingSnapshotValidationRead(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        var owner = writeLease.MainAdmission;
        if (HasBrowserOriginalOperation || writeLease.BrowserLocalAccess != null ||
            writeLease.Purpose != CanonicalWritePurpose.SessionMutation ||
            writeLease.WorkerPurpose != null || writeLease.IsLegacyStorageRecovery ||
            owner == null || !owner.PhysicalScope || owner.Closed || owner.Closing || owner.BoundClosing || owner.MetadataOnly)
            throw new InvalidOperationException("Pending snapshot validation requires its original ordinary console lease.");
        writeLease.EnsureNoPendingLocalDecision();
        RequirePendingSnapshotLeaseFlow(writeLease, owner, writeLease.AmbientRegistration);
        var scope = new PendingSnapshotValidationReadScope(this, owner, writeLease);
        owner.ReserveWorldRead(scope);
        return scope;
    }

    /// <summary>
    /// Tries to reserve an ordinary physical lease through its exact live entry frame and original access ancestry.
    /// </summary>
    /// <param name="lease">
    /// Caller-owned session mutation lease; unsupported purposes, pending decisions and foreign flows are ineligible.
    /// </param>
    /// <returns>
    /// The unopened borrowed reservation, or <see langword="null"/> when entry is unsupported without changing authority.
    /// </returns>
    private PendingSnapshotValidationReadScope? TryBeginBorrowedValidationReservation(CanonicalWriteLease lease)
    {
        var owner = lease.MainAdmission;
        var entry = MainAdmissions.Value;
        if (!ReferenceEquals(lease.Owner, this) || !lease.IsActive || HasBrowserOriginalOperation ||
            lease.BrowserLocalAccess != null || lease.Purpose != CanonicalWritePurpose.SessionMutation ||
            lease.WorkerPurpose != null || lease.IsLegacyStorageRecovery || lease.MutationIntentRecorder != null ||
            lease.PendingLocalDecision != null || owner == null || !owner.PhysicalScope || owner.Closed ||
            owner.Closing || owner.BoundClosing || owner.MetadataOnly ||
            entry is null || !IsBorrowedValidationReadFlowCurrent(lease, entry)) return null;
        var scope = new PendingSnapshotValidationReadScope(this, entry, lease);
        return entry.TryReserveWorldRead(scope) ? scope : null;
    }

    /// <summary>
    /// Checks an exact captured entry frame and its live ancestry without performing or reusing physical admission.
    /// </summary>
    /// <param name="lease">
    /// Original active physical lease whose owner must occur in the entry frame's ancestry.
    /// </param>
    /// <param name="entry">
    /// Exact current frame captured at reservation entry; a later descendant or sibling is not interchangeable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for an uninterrupted live acquired same-root and same-access chain to the original
    /// physical owner, with the original exact ambient lease registration; otherwise, <see langword="false"/>.
    /// </returns>
    private bool IsBorrowedValidationReadFlowCurrent(CanonicalWriteLease lease, MainAdmission entry)
    {
        var physical = lease.MainAdmission;
        if (!ReferenceEquals(lease.Owner, this) || !lease.IsActive || physical is null ||
            !ReferenceEquals(MainAdmissions.Value, entry) || lease.AmbientRegistration?.Active != true ||
            !ReferenceEquals(_ambientCanonicalLease.Value, lease.AmbientRegistration)) return false;
        for (MainAdmission? current = entry; current is not null; current = current.Parent)
        {
            if (!current.Acquired || current.Closed || current.Closing || current.BoundClosing || current.MetadataOnly ||
                current.RootIdentity != CanonicalRootAuthorityIdentity || !current.SharesAccess(physical) ||
                current.PhysicalScope && !ReferenceEquals(current, physical)) return false;
            if (ReferenceEquals(current, physical)) return true;
        }
        return false;
    }

    /// <summary>
    /// Requires the exact active lease registration and physical admission in the current ancestry.
    /// </summary>
    /// <param name="lease">
    /// Original lease whose registration must remain unchanged.
    /// </param>
    /// <param name="admission">
    /// Original physical admission, which must still belong to the lease.
    /// </param>
    /// <param name="registration">
    /// Original active ambient registration; <see langword="null"/> is rejected.
    /// </param>
    private void RequirePendingSnapshotLeaseFlow(CanonicalWriteLease lease, MainAdmission admission,
        AmbientCanonicalLeaseRegistration? registration)
    {
        if (!ReferenceEquals(lease.MainAdmission, admission) || admission.Closed ||
            !ReferenceEquals(lease.AmbientRegistration, registration) || registration?.Active != true)
            throw new InvalidOperationException("Pending snapshot validation lost its original lease registration.");
        var current = MainAdmissions.Value;
        while (current != null && !ReferenceEquals(current, admission))
        {
            if (current.Closed && current.OwnsRemote && current.RootIdentity == CanonicalRootAuthorityIdentity)
                throw new InvalidOperationException("The original main operation has closed.");
            current = current.Parent;
        }
        var ambient = _ambientCanonicalLease.Value;
        while (ambient != null && !ReferenceEquals(ambient, registration)) ambient = ambient.Previous;
        if (current == null || ambient == null)
            throw new InvalidOperationException("Pending snapshot validation left its original execution flow.");
    }

    /// <summary>
    /// Holds the original console owner, canonical lease and actual session generation for one read lifetime.
    /// </summary>
    /// <param name="files">
    /// Manager that issued this reservation and owns its lease.
    /// </param>
    /// <param name="owner">
    /// Exact admitted entry frame that retains access and owns the read reservation.
    /// For a borrowed lease, its original physical owner remains separately retained by that lease.
    /// </param>
    /// <param name="borrowedLease">
    /// Original caller-owned lease, or <see langword="null"/> to own the separate acquisition.
    /// </param>
    internal sealed class PendingSnapshotValidationReadScope(FileSystemManager files, MainAdmission owner,
        CanonicalWriteLease? borrowedLease = null)
        : ValidationReadReservation, IAsyncDisposable
    {
        /// <summary>
        /// Gets the exact main frame through which this reservation was entered and will be released.
        /// It may be a participating frame above the retained original physical lease owner.
        /// </summary>
        internal MainAdmission EntryAdmission => owner;

        /// <summary>
        /// The one owned acquisition issued synchronously by the manager; unused for a borrowed lease.
        /// </summary>
        internal Task<CanonicalWriteLease> Acquisition { get; set; } = null!;
        /// <summary>
        /// The original acquired lease, or null before successful acquisition.
        /// </summary>
        internal CanonicalWriteLease? Lease { get; private set; } = borrowedLease;
        private readonly bool _borrowed = borrowedLease != null;
        private readonly MainAdmission? _retainedAdmission = borrowedLease?.MainAdmission;
        private AmbientCanonicalLeaseRegistration? _registration = borrowedLease?.AmbientRegistration;
        private bool _opening = true, _closed, _invalidated;
        private string? _generation;
        /// <inheritdoc />
        internal override bool Opening => !_borrowed && _opening;
        /// <inheritdoc />
        internal override bool Closed => _closed;
        /// <inheritdoc />
        internal override MainAdmission? RetainedPhysicalAdmission => _retainedAdmission;

        /// <summary>

        /// Generation captured by this read owner at opening and rechecked by RequireCurrent.

        /// </summary>
        internal string ObservedGeneration => _generation ?? throw new InvalidOperationException("Pending read generation is not observed.");

        /// <summary>
        /// Finishes owned acquisition when required and binds the existing generation before admitting readers.
        /// </summary>
        /// <returns>
        /// Completion once this scope is active and read-only.
        /// </returns>
        internal async Task OpenAsync()
        {
            if (_closed || !_opening) throw new InvalidOperationException("Validation read scope cannot reopen.");
            if (!_borrowed)
            {
                Lease = await Acquisition;
                _registration = Lease.AmbientRegistration;
            }
            _opening = false;
            _generation = files.ReadExistingSessionGeneration(Lease!)
                ?? throw new InvalidDataException("Validation source has no existing session generation.");
            RequireCurrent();
        }

        /// <summary>
        /// Refuses lost ownership, a foreign execution flow or changed generation without reacquiring authority.
        /// </summary>
        internal void RequireCurrent()
        {
            try
            {
                if (_invalidated || _closed || _opening || Lease == null || owner.Closed || owner.Closing || owner.BoundClosing ||
                    Lease.MainAdmission == null || !owner.SharesAccess(Lease.MainAdmission))
                    throw new InvalidOperationException("Pending snapshot validation lost its original owner.");
                files.RequirePendingSnapshotLeaseFlow(Lease, _retainedAdmission ?? Lease.MainAdmission, _registration);
                files.EnsureValidCanonicalWriteLease(Lease);
                if (files.ReadExistingSessionGeneration(Lease) != _generation)
                    throw new InvalidDataException("Pending snapshot validation generation changed.");
            }
            catch { _invalidated = true; throw; }
        }

        /// <summary>
        /// Releases this reservation and disposes the physical lease only when this scope acquired it.
        /// </summary>
        /// <returns>
        /// Completion after reservation release and any owned lease teardown, including failed opening.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            if (_closed) return;
            _closed = true;
            _opening = false;
            try
            {
                // Acquisition owns its registrations until it either fails or returns its lease.
                if (!_borrowed)
                {
                    var lease = Lease ?? await Acquisition;
                    await lease.DisposeAsync();
                }
            }
            finally { owner.ReleaseWorldRead(this); }
        }

        /// <summary>
        /// Checks original ownership after closing the read reservation without reopening it.
        /// </summary>
        internal void RequireOwnerAfterClose()
        {
            if (!_closed || _invalidated || owner.Closed || owner.Closing || owner.BoundClosing)
                throw new InvalidOperationException("Validation owner closed before issue publication.");
            owner.Validate(null);
            if (_borrowed)
            {
                files.RequirePendingSnapshotLeaseFlow(Lease!, _retainedAdmission!, _registration);
                files.EnsureValidCanonicalWriteLease(Lease!);
            }
        }
    }
}
