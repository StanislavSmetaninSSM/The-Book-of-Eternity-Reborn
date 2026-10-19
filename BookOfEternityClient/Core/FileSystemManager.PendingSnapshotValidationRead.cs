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
    /// Holds the original console owner, read-only canonical lease and actual session generation.
    /// </summary>
    /// <param name="files">
    /// Manager that issued this reservation and owns its lease.
    /// </param>
    /// <param name="owner">
    /// The already admitted original main frame retained by acquisition.
    /// </param>
    internal sealed class PendingSnapshotValidationReadScope(FileSystemManager files, MainAdmission owner)
        : ValidationReadReservation, IAsyncDisposable
    {
        /// <summary>
        /// The one acquisition issued synchronously by the owning manager.
        /// </summary>
        internal Task<CanonicalWriteLease> Acquisition { get; set; } = null!;
        /// <summary>
        /// The original acquired lease, or null before successful acquisition.
        /// </summary>
        internal CanonicalWriteLease? Lease { get; private set; }
        private bool _opening = true, _closed, _invalidated;
        private string? _generation;
        /// <inheritdoc />
        internal override bool Opening => _opening;
        /// <inheritdoc />
        internal override bool Closed => _closed;

        /// <summary>
        /// Finishes unchanged recovery and binds the existing generation before readers are admitted.
        /// </summary>
        /// <returns>
        /// Completion once this scope is active and read-only.
        /// </returns>
        internal async Task OpenAsync()
        {
            if (_closed || !_opening) throw new InvalidOperationException("Validation read scope cannot reopen.");
            Lease = await Acquisition;
            _opening = false;
            _generation = files.ReadExistingSessionGeneration(Lease)
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
                var current = MainAdmissions.Value;
                while (current != null && !ReferenceEquals(current, Lease.MainAdmission)) current = current.Parent;
                if (current == null || !files.HasAmbientCanonicalLease())
                    throw new InvalidOperationException("Pending snapshot validation left its original execution flow.");
                files.EnsureValidCanonicalWriteLease(Lease);
                if (files.ReadExistingSessionGeneration(Lease) != _generation)
                    throw new InvalidDataException("Pending snapshot validation generation changed.");
            }
            catch { _invalidated = true; throw; }
        }

        /// <summary>
        /// Closes only this acquisition and releases the main reservation, including failed opening.
        /// </summary>
        /// <returns>
        /// Completion after the original owned lease has been disposed.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            if (_closed) return;
            _closed = true;
            _opening = false;
            try
            {
                // Acquisition owns its registrations until it either fails or returns its lease.
                var lease = Lease ?? await Acquisition;
                await lease.DisposeAsync();
            }
            finally { owner.ReleaseWorldRead(this); }
        }

        /// <summary>
        /// Checks retained original ownership after successful lease teardown, without reopening it.
        /// </summary>
        internal void RequireOwnerAfterClose()
        {
            if (!_closed || _invalidated || owner.Closed || owner.Closing || owner.BoundClosing)
                throw new InvalidOperationException("Validation owner closed before issue publication.");
            owner.Validate(null);
        }
    }
}
