using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<PendingSnapshotValidationInvocation?> _pendingSnapshotInvocation = new();
    private PendingSnapshotValidationInvocation? _pendingSnapshotReservation;
    private static readonly string[] PendingSnapshotRequestPaths =
        ["game_state/control/validation_repair_request.json", "input/turn_request.json", "ready/turn_complete.json"];

    /// <summary>
    /// Validates one console post-materialization state using fresh, invocation-local source authority.
    /// Browser and retained-input callers keep their existing validation route.
    /// </summary>
    /// <returns>
    /// Ordered issues after final physical proof and owned lease teardown.
    /// </returns>
    internal Task<List<ValidationIssue>> ValidatePostMaterializedGameStateAsync()
    {
        if (_fs.HasBrowserOriginalOperation || _sameTurnOwnerInputs != null || _prevalidatedPendingTurnSnapshotOverride != null)
            return ValidateGameStateAsync();
        return RunPendingSnapshotValidationAsync(() => ValidateGameStateCoreAsync(
            GameStateValidationSelection.All, false, null, null));
    }

    /// <summary>
    /// Reserves one validator invocation, authenticates its source twice and closes before publication.
    /// </summary>
    /// <param name="validate">
    /// Read-only validation consumers whose issues are withheld until final proof succeeds.
    /// </param>
    /// <returns>
    /// The consumer's ordered issues after proof and close; failures retain their primary exception.
    /// </returns>
    private async Task<List<ValidationIssue>> RunPendingSnapshotValidationAsync(Func<Task<List<ValidationIssue>>> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        _pendingSnapshotInvocation.Value?.RequireCurrent();
        var admitted = new WorldValidationInvocation(false, false, pendingReserved: true);
        var invocation = new PendingSnapshotValidationInvocation(this, admitted);
        lock (_worldReadGate)
        {
            if (_worldReservation != null || _worldValidationCalls != 0 || _worldOverrideMutations != 0 ||
                _pendingSnapshotReservation != null || _prevalidatedPendingTurnSnapshotOverride != null || _sameTurnOwnerInputs != null)
                throw new InvalidOperationException("Pending snapshot validation conflicts with another validator consumer.");
            _worldValidationCalls++;
            _worldReservation = admitted;
            _pendingSnapshotReservation = invocation;
        }
        var previous = _pendingSnapshotInvocation.Value;
        var previousWorld = _worldValidationInvocation.Value;
        _pendingSnapshotInvocation.Value = invocation;
        _worldValidationInvocation.Value = admitted;
        Exception? primary = null;
        List<ValidationIssue>? issues = null;
        try
        {
            // The synchronous issuer installs ambient ownership in this flow before OpenAsync yields.
            invocation.Physical = _fs.BeginPendingSnapshotValidationRead(CancellationToken.None);
            await invocation.Physical.OpenAsync();
            invocation.Evidence = CapturePendingSnapshotEvidence(invocation.Physical);
            invocation.Active = true;
            invocation.RequireCurrent();
            issues = await validate();
            invocation.RequireCurrent();
            invocation.Evidence.RequireSame(CapturePendingSnapshotEvidence(invocation.Physical));
            invocation.RequireCurrent();
        }
        catch (Exception error) { primary = error; }
        finally
        {
            invocation.Closed = true;
            invocation.Active = false;
            try { if (invocation.Physical != null) await invocation.Physical.DisposeAsync(); }
            catch (Exception close)
            {
                if (primary == null) primary = close;
                else try { primary.Data["PendingSnapshotValidation.CloseFailure"] = close; } catch { }
            }
            finally
            {
                lock (_worldReadGate)
                    if (ReferenceEquals(_pendingSnapshotReservation, invocation)) _pendingSnapshotReservation = null;
                ReleaseWorldValidation(admitted);
                _pendingSnapshotInvocation.Value = previous;
                _worldValidationInvocation.Value = previousWorld;
            }
        }
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        invocation.Physical!.RequireOwnerAfterClose();
        LogValidationErrors(issues!);
        return issues!;
    }

    /// <summary>
    /// Retrieves a fresh DTO copy from the current source proof, refusing foreign or retired contexts.
    /// </summary>
    /// <param name="writeLease">
    /// Optional explicit lease; only the invocation's exact original lease is accepted.
    /// </param>
    /// <returns>
    /// The invocation lookup, or null when no pending-snapshot invocation exists.
    /// </returns>
    private ValidatedPendingTurnSnapshotLookup? CurrentPendingSnapshotLookup(FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var current = _pendingSnapshotInvocation.Value;
        lock (_worldReadGate)
            if (_pendingSnapshotReservation != null && !ReferenceEquals(current, _pendingSnapshotReservation))
                throw new InvalidOperationException("Foreign validation cannot consume reserved pending snapshot evidence.");
        if (current == null) return null;
        current.RequireCurrent();
        if (writeLease != null && !ReferenceEquals(writeLease, current.Physical!.Lease))
        {
            current.Invalidated = true;
            throw new InvalidOperationException("Pending snapshot lookup supplied a foreign lease.");
        }
        return current.Evidence!.CreateLookup();
    }

    /// <summary>
    /// Authenticates physical source bytes with the unchanged authority checker and captures its actual reads.
    /// </summary>
    /// <param name="physical">
    /// The original active read-only scope; no new lease or owner is acquired.
    /// </param>
    /// <returns>
    /// Immutable serialized manifest, original status and exact physical dependency bytes.
    /// </returns>
    private PendingSnapshotEvidence CapturePendingSnapshotEvidence(FileSystemManager.PendingSnapshotValidationReadScope physical)
    {
        physical.RequireCurrent();
        var reads = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        byte[]? Read(string path)
        {
            if (reads.TryGetValue(path, out var existing)) return existing;
            var bytes = _fs.ReadFileBytesSync(physical.Lease!, path);
            reads.Add(path, bytes);
            return bytes;
        }
        string? Text(string path) => Read(path) is { } bytes ? DecodePendingSnapshotText(bytes) : null;
        var manifestJson = Text(PendingTurnSnapshotManifestPath);
        var manifest = ParseValidationPendingTurnSnapshotManifest(manifestJson);
        var authority = Text(PendingTurnSnapshotAuthority.AuthorityPath);
        // Include absent alternatives even when the first request matches and the old checker short-circuits.
        foreach (var path in PendingSnapshotRequestPaths) _ = Read(path);
        var status = manifest == null
            ? manifestJson == null ? ValidatedPendingTurnSnapshotStatus.Missing : ValidatedPendingTurnSnapshotStatus.Unusable
            : IsValidatedPendingTurnSnapshotManifestUsable(manifest, authority, physical.Lease, Read, Text)
                ? ValidatedPendingTurnSnapshotStatus.Usable : ValidatedPendingTurnSnapshotStatus.Unusable;
        physical.RequireCurrent();
        return new(status, manifest == null ? null : JsonSerializer.Serialize(manifest, ManifestHashJsonOpts), reads);
    }

    /// <summary>
    /// Keeps one invocation's source immutable while allowing independent consumer DTOs.
    /// </summary>
    /// <param name="status">
    /// Actual reader authentication status, including Missing and Unusable.
    /// </param>
    /// <param name="manifestJson">
    /// Private manifest serialization, or null when parsing produced no manifest.
    /// </param>
    /// <param name="reads">
    /// Exact bytes and absences observed by this authentication pass.
    /// </param>
    private sealed class PendingSnapshotEvidence(ValidatedPendingTurnSnapshotStatus status, string? manifestJson,
        Dictionary<string, byte[]?> reads)
    {
        /// <summary>
        /// Creates an independent DTO without sharing mutable entry authority.
        /// </summary>
        /// <returns>
        /// Original status and a freshly deserialized manifest, when present.
        /// </returns>
        internal ValidatedPendingTurnSnapshotLookup CreateLookup() => new(status,
            manifestJson == null ? null : JsonSerializer.Deserialize<ValidationPendingTurnSnapshotManifest>(manifestJson, ManifestJsonOpts));

        /// <summary>
        /// Rejects any changed dependency, absence, status or serialized source before issue publication.
        /// </summary>
        /// <param name="fresh">
        /// A separate physical authentication pass under the same held lease.
        /// </param>
        internal void RequireSame(PendingSnapshotEvidence fresh)
        {
            if (status != fresh.Status || manifestJson != fresh.ManifestJson || reads.Count != fresh.Reads.Count ||
                reads.Any(pair => !fresh.Reads.TryGetValue(pair.Key, out var bytes) ||
                    (pair.Value == null ? bytes != null : bytes == null || !pair.Value.AsSpan().SequenceEqual(bytes))))
                throw new InvalidDataException("Pending snapshot dependencies changed during validation.");
        }
        private ValidatedPendingTurnSnapshotStatus Status => status;
        private string? ManifestJson => manifestJson;
        private Dictionary<string, byte[]?> Reads => reads;
    }

    /// <summary>
    /// Binds one validator reservation to its original flow, lease and immutable source evidence.
    /// </summary>
    /// <param name="service">
    /// Validator instance exclusively reserved by this invocation.
    /// </param>
    /// <param name="admitted">
    /// Shared instance-admission identity used by existing World and Meta cache guards.
    /// </param>
    private sealed class PendingSnapshotValidationInvocation(ValidationService service, WorldValidationInvocation admitted)
    {
        internal FileSystemManager.PendingSnapshotValidationReadScope? Physical;
        internal PendingSnapshotEvidence? Evidence;
        internal bool Active, Closed, Invalidated;

        /// <summary>
        /// Requires the exact active invocation and preserves invalidation irreversibly.
        /// </summary>
        internal void RequireCurrent()
        {
            try
            {
                lock (service._worldReadGate)
                    if (!Active || Closed || Invalidated || admitted.Closed ||
                        !ReferenceEquals(service._pendingSnapshotInvocation.Value, this) ||
                        !ReferenceEquals(service._pendingSnapshotReservation, this) ||
                        !ReferenceEquals(service._worldValidationInvocation.Value, admitted) ||
                        !ReferenceEquals(service._worldReservation, admitted))
                        throw new InvalidOperationException("Pending snapshot invocation is no longer current.");
                Physical!.RequireCurrent();
            }
            catch { Invalidated = true; throw; }
        }
    }
}
