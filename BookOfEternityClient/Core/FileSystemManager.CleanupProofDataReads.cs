using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<CleanupProofReadContext?> _cleanupProofReadContext = new();

    /// <summary>
    /// Retains the actual publisher identities for one prepared callback attempt, never its read DATA.
    /// </summary>
    /// <param name="Lease">
    /// The exact caller-owned physical lease.
    /// </param>
    /// <param name="Outer">
    /// Actual outer publication mutation token, retained until publication finishes.
    /// </param>
    /// <param name="Core">
    /// Actual inner publication mutation token, retained through compensation and cleanup.
    /// </param>
    /// <param name="Generation">
    /// Existing generation declared by this publication.
    /// </param>
    private sealed record CleanupProofReadContext(CanonicalWriteLease Lease, MainMutationReservation Outer,
        MainMutationReservation Core, string? Generation)
    {
        /// <summary>
        /// Whether this exact callback attempt is still running.
        /// </summary>
        internal volatile bool Active = true;
    }

    /// <summary>
    /// Runs a post-recovery completed-proof reader with independently proved temporary DATA reuse.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result produced by the original read-only proof algorithm.
    /// </typeparam>
    /// <param name="lease">
    /// The active original physical lease, retained and disposed by its caller.
    /// </param>
    /// <param name="read">
    /// The original read-only proof after recovery and the recovered marker read.
    /// It must finish before another recovery or effect begins.
    /// </param>
    /// <returns>
    /// The original proof DATA after the selected reader's source contract succeeds.
    /// </returns>
    internal async Task<TResult> ReadCleanupCompletedProofDataAsync<TResult>(CanonicalWriteLease lease,
        Func<Task<TResult>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        ReservedValidationReadSet? readSet = null;
        CleanupProofReadWindow? activeWindow = null;
        if (_validationReadSet.Value == null && ValidationReuseSuppression.Value == 0 && CanReuseSourceData &&
            _cleanupProofReadContext.Value is { Active: true } context && ReferenceEquals(context.Lease, lease) &&
            MainAdmissions.Value is { } entry && IsBorrowedValidationReadFlowCurrent(lease, entry))
        {
            lock (CanonicalRootAuthorityIdentity.WorkerContextGate)
            {
                var window = new CleanupProofReadWindow(this, lease, entry);
                if (window.TryOpen())
                {
                    var reservation = new PendingSnapshotValidationReadScope(this, entry, lease);
                    if (entry.TryReserveWorldRead(reservation, window))
                    {
                        window.Reservation = reservation;
                        try
                        {
                            readSet = new ReservedValidationReadSet(this, lease, reservation,
                                preserveOriginalAdmission: true, cleanupWindow: window);
                        }
                        catch
                        {
                            entry.ReleaseWorldRead(reservation);
                            throw;
                        }
                        activeWindow = window;
                        _validationReadSet.Value = readSet;
                        ValidationDiagnosticScopes.Value++;
                    }
                }
            }
        }
        if (readSet == null)
        {
            using var fallback = MeasureOperation("CleanupProofData.Fallback");
            return await ReadStableRecoveryDataAsync(lease, read);
        }
        Exception? primaryFailure = null;
        try
        {
            using var fresh = ForceFreshRecoveryValidation();
            TResult result;
            using (MeasureOperation("CleanupProofData.Window"))
            {
                await readSet.OpenAsync();
                result = await read();
                await readSet.CompleteAsync();
            }
            activeWindow!.RequireCurrent();
            return result;
        }
        catch (Exception error) { primaryFailure = error; throw; }
        finally
        {
            try { await readSet.DisposeAsync(); }
            catch when (primaryFailure != null) { /* Preserve the original proof refusal. */ }
        }
    }

    /// <summary>
    /// Provides one exact post-recovery version while retaining the publisher's two active mutations.
    /// </summary>
    /// <param name="files">
    /// Manager whose private active callback must issue the window.
    /// </param>
    /// <param name="lease">
    /// Exact original physical lease, never acquired or owned here.
    /// </param>
    /// <param name="entry">
    /// Exact live frame at entry to this independent read window.
    /// </param>
    internal sealed class CleanupProofReadWindow(FileSystemManager files, CanonicalWriteLease lease,
        MainAdmission entry) : IOperationDependency
    {
        private readonly CleanupProofReadContext? _context = files._cleanupProofReadContext.Value;
        private object? _version;

        /// <summary>
        /// Gets or sets the exact read reservation installed for this window.
        /// </summary>
        internal ValidationReadReservation? Reservation { get; set; }

        /// <summary>
        /// Captures eligibility after recovery without changing ordinary root quiescence.
        /// </summary>
        /// <returns>
        /// True for the live issuer and exact two-token root and local set; false selects fresh fallback.
        /// </returns>
        internal bool TryOpen() => TryCurrent(out _version);

        /// <summary>
        /// Checks the complete local mutation set and the captured root version during atomic reservation.
        /// </summary>
        /// <param name="mutations">
        /// Actual shared-access mutation identities inspected under its state lock.
        /// </param>
        /// <returns>
        /// True only for the exact two identities and unchanged opaque root version.
        /// </returns>
        internal bool MatchesMutations(HashSet<MainMutationReservation> mutations) =>
            _context is { Active: true } && mutations.Count == 2 && mutations.Contains(_context.Outer) &&
            mutations.Contains(_context.Core) && files.CanonicalRootAuthorityIdentity.ValidationEpoch
                .TryCapturePreparedPublisher(_context.Outer, _context.Core, out var version) &&
            ReferenceEquals(_version, version);

        /// <summary>
        /// Refuses a lost publisher lifetime, changed epoch or changed original flow before DATA can escape.
        /// </summary>
        internal void RequireCurrent()
        {
            if (!TryCurrent(out var current) || !ReferenceEquals(current, _version))
                throw new InvalidOperationException("Cleanup proof DATA left its exact prepared publisher window.");
        }

        /// <summary>
        /// Captures the current no-IO identity through the original worker and main state gates.
        /// </summary>
        /// <param name="version">
        /// Exact current root identity on success, otherwise null.
        /// </param>
        /// <returns>
        /// True only while the original issuer, registration, entry and complete mutation sets remain current.
        /// </returns>
        private bool TryCurrent(out object? version)
        {
            version = null;
            if (_context is not { Active: true } || !ReferenceEquals(files._cleanupProofReadContext.Value, _context) ||
                !ReferenceEquals(_context.Lease, lease) || string.IsNullOrEmpty(_context.Generation) ||
                !files.IsBorrowedValidationReadFlowCurrent(lease, entry)) return false;
            lock (files.CanonicalRootAuthorityIdentity.WorkerContextGate)
                return lease.MainAdmission != null && lease.MainAdmission.TryCapturePreparedProofVersion(
                    lease, _context.Outer, _context.Core, Reservation, out version);
        }

        /// <inheritdoc />
        public bool TryCaptureVersion(out object? version)
        {
            RequireCurrent();
            version = _version;
            return true;
        }

        /// <summary>
        /// Requires the fresh original generation to match the publisher's declared binding.
        /// </summary>
        /// <param name="generation">
        /// Actual generation read by the existing reserved source proof.
        /// </param>
        internal void RequireGeneration(LocalSessionGenerationSnapshot generation)
        {
            RequireCurrent();
            if (generation.Binding.Id != _context!.Generation)
                throw new InvalidDataException("Cleanup proof generation differs from its prepared publisher.");
        }
    }
}
