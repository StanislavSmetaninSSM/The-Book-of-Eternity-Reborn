using System.Runtime.ExceptionServices;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Only the selected World validator can request this private read lifetime.
    // The reservation narrows an existing actual access; it grants no new owner.
    internal WorldValidationReadScope? BeginWorldValidationRead()
    {
        var original = CurrentBrowserOriginalScope();
        if (original is not { Processing: true, Cleanup: false, Staged: not null }) return null;
        var owner = MainAdmissions.Value;
        // Async disposal closes the shared child but cannot move the caller's
        // AsyncLocal head. Match existing admission's terminal-remote prohibition
        // before resolving the live original through closed/foreign descendants.
        for (var p = owner; p != null; p = p.Parent)
            if (p.Closed && p.OwnsRemote && p.RootIdentity == CanonicalRootAuthorityIdentity)
                throw GmSessionRunPersistence.Invalid();
        while (owner != null && (owner.Closed || owner.RootIdentity != CanonicalRootAuthorityIdentity || !owner.Acquired))
            owner = owner.Parent;
        // This validates the actual scope owner/access, not just a live ancestor.
        // A truly closed original refuses; it cannot become scalar eligibility.
        original.ValidateOwner(owner);
        if (owner!.Closing || owner.BoundClosing || owner.MetadataOnly || HasAmbientCanonicalLease()) return null;
        var scope = new WorldValidationReadScope(this, original, owner);
        owner.ReserveWorldRead(scope);
        try
        {
            // Synchronous issuer installs the real registration in this caller.
            scope.Acquisition = AcquireCanonicalWriteLeaseWithAmbientAsync(
                CanonicalWritePurpose.PublicationReadQuiescence, CancellationToken.None,
                worldIssuance: scope);
            return scope;
        }
        catch { owner.ReleaseWorldRead(scope); throw; }
    }

    private void RequireNoWorldValidationMutation()
    {
        var current = MainAdmissions.Value;
        var issuance = current?.WorldRecoveryIssuance;
        for (var p = current; p != null; p = p.Parent)
            if (!p.Closed && p.RootIdentity == CanonicalRootAuthorityIdentity) p.RequireNoWorldReadConflict(issuance);
    }

    internal byte[]? ReadWorldValidationBytesSync(CanonicalWriteLease lease, string path, bool notifyAttempt)
    {
        EnsureValidCanonicalWriteLease(lease);
        var snapshot = UsesTrustedLocalWriter(lease, path)
            ? ReadOrdinaryFileSnapshotCore(path, lease) : ReadFileSnapshotCore(path);
        if (notifyAttempt) InvokeAfterCanonicalReadAttempt(path);
#if DEBUG
        BrowserDiagnosticContent(path, "world-bytes-sync", "leaf", snapshot == null ? "missing" : "present", snapshot?.Content.Length ?? 0);
#endif
        return snapshot?.Content;
    }
    internal (byte[]? Bytes, ExceptionDispatchInfo? DataFailure) ReadWorldValidationNullableBytesSync(
        CanonicalWriteLease lease, string path, bool notifyAttempt)
    {
        EnsureValidCanonicalWriteLease(lease);
        // These concrete nullable leaves are ordinary current/control files.
        // A different reader family requires closure review, never a broad catch.
        if (!UsesTrustedLocalWriter(lease, path))
            throw new InvalidOperationException("World nullable read is outside ordinary storage.");
        var snapshot = ReadOrdinaryFileSnapshotCore(path, lease, out var failure, nullableDataFailure: true);
        if (failure == null && notifyAttempt)
        {
            // This is only the existing data-observation hook, not an authority boundary.
            try { InvokeAfterCanonicalReadAttempt(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { failure = ExceptionDispatchInfo.Capture(error); }
        }
#if DEBUG
        BrowserDiagnosticContent(path, "world-nullable-sync", "leaf", failure != null ? "data-fault" :
            snapshot == null ? "missing" : "present", snapshot?.Content.Length ?? 0);
#endif
        return (snapshot?.Content, failure);
    }

    internal async Task<byte[]?> ReadWorldValidationBytesAsync(CanonicalWriteLease lease, string path)
    {
        var bytes = await ReadFileBytesAsync(lease, path);
        await InvokeAfterCanonicalReadAttemptAsync(path);
#if DEBUG
        BrowserDiagnosticContent(path, "world-bytes-async", "leaf", bytes == null ? "missing" : "present", bytes?.Length ?? 0);
#endif
        return bytes;
    }

    internal async Task<(byte[]? Bytes, string? Text)> ReadWorldValidationTextAsync(CanonicalWriteLease lease, string path)
    {
        var snapshot = await ReadFileSnapshotAsync(lease, path);
        var text = DecodeFileSnapshot(snapshot);
        await InvokeAfterCanonicalReadAttemptAsync(path);
#if DEBUG
        BrowserDiagnosticContent(path, "world-text-async", "leaf", snapshot == null ? "missing" : "present", snapshot?.Content.Length ?? 0);
#endif
        return (snapshot?.Content, text);
    }

    /// <summary>
    /// Reserves an existing browser-original operation for its World or Meta read phase.
    /// </summary>
    /// <param name="files">
    /// Filesystem issuing the original physical lease.
    /// </param>
    /// <param name="original">
    /// Actual processing browser operation whose staged identity remains authoritative.
    /// </param>
    /// <param name="owner">
    /// Existing admitted main frame sharing the original operation's access.
    /// </param>
    internal sealed class WorldValidationReadScope(
        FileSystemManager files, BrowserOriginalOperationScope original, MainAdmission owner) : ValidationReadReservation, IAsyncDisposable
    {
        internal Task<CanonicalWriteLease> Acquisition { get; set; } = null!;
        internal CanonicalWriteLease? Lease { get; private set; }
        private bool _opening = true;
        /// <inheritdoc />
        internal override bool Opening => _opening;
        /// <inheritdoc />
        internal override bool Closed => _closed;
        private bool _closed;
        internal async Task OpenAsync()
        {
            Lease = await Acquisition;
            _opening = false;
            RequireCurrent();
        }
        internal void RequireCurrent()
        {
            if (_closed || owner.Closed || owner.Closing || owner.BoundClosing ||
                !ReferenceEquals(original, files.CurrentBrowserOriginalScope()) || original.Cleanup ||
                original.Staged == null || !original.Processing) throw BrowserOriginalMainCondition.Invalid();
            if (Lease != null) files.EnsureValidCanonicalWriteLease(Lease);
            original.ValidateOwner(Lease?.MainAdmission ?? owner);
            if (files.ReadOriginalBrowserText(PendingPlayerActionService.PendingPath) != original.ExpectedPendingJson ||
                files.ReadOriginalBrowserText("input/turn_request.json") != original.Staged.RequestJson ||
                files.ReadOriginalBrowserText(BrowserManifestPath) != original.Staged.ManifestJson ||
                files.ReadOriginalBrowserText(PendingTurnSnapshotAuthority.AuthorityPath) != original.Staged.AuthorityJson)
                throw BrowserOriginalMainCondition.Invalid();
        }
        internal void RequireFullEndProof()
        {
            RequireCurrent();
            _ = files.PreflightBrowserOriginalAdmission(Lease!.MainAdmission!, Lease, "world-validation-final");
            RequireCurrent();
        }
        public async ValueTask DisposeAsync()
        {
            if (_closed) return;
            try
            {
                if (Lease != null) await Lease.DisposeAsync();
            }
            finally { _closed = true; _opening = false; owner.ReleaseWorldRead(this); }
        }
        internal void RequireOwnerAfterClose()
        {
            // Lease teardown must succeed first. This uses only the retained
            // original owner/tuple, never reacquires a closing operation.
            if (!_closed || owner.Closed || owner.Closing || owner.BoundClosing ||
                !ReferenceEquals(original, files.CurrentBrowserOriginalScope())) throw BrowserOriginalMainCondition.Invalid();
            original.ValidateOwner(owner);
            if (files.ReadOriginalBrowserText(PendingPlayerActionService.PendingPath) != original.ExpectedPendingJson ||
                files.ReadOriginalBrowserText("input/turn_request.json") != original.Staged!.RequestJson ||
                files.ReadOriginalBrowserText(BrowserManifestPath) != original.Staged.ManifestJson ||
                files.ReadOriginalBrowserText(PendingTurnSnapshotAuthority.AuthorityPath) != original.Staged.AuthorityJson)
                throw BrowserOriginalMainCondition.Invalid();
        }
    }
}
