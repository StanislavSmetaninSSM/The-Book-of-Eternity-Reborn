using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<CompletedTreatmentItemValidationScope?> _completedTreatmentItems = new();

    /// <summary>
    /// Installs a consume-once completed comparison for the next exact post-materialization invocation.
    /// It does not create a reader, reservation or publication authority.
    /// </summary>
    /// <param name="completed">DATA issued only by genuinely finalized and closed publication.</param>
    /// <returns>A scope that is permanently invalidated before any repair.</returns>
    internal CompletedTreatmentItemValidationScope UseCompletedTreatmentItemValidationScope(
        MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation? completed)
    {
        var scope = new CompletedTreatmentItemValidationScope(this, _completedTreatmentItems.Value, completed);
        _completedTreatmentItems.Value = scope;
        return scope;
    }

    /// <summary>
    /// Owns only one invocation-local root observation and one immutable completed comparison.
    /// All state changes are private; invalidation cannot be reversed by a caller or an awaiting reader.
    /// </summary>
    internal sealed class CompletedTreatmentItemValidationScope : IDisposable
    {
        private readonly object _gate = new();
        private readonly ValidationService _service;
        private readonly CompletedTreatmentItemValidationScope? _previous;
        private MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation? _completed;
        private WorldValidationInvocation? _invocation;
        private JsonObject? _parsedRoot;
        private bool _observed, _invalidated;

        /// <summary>

        /// Installs DATA in this validator's existing completed-publication scope chain.

        /// </summary>
        /// <param name="service">Owning validator.</param>
        /// <param name="previous">Previous scope restored on disposal.</param>
        /// <param name="completed">Genuine completed comparison, or null.</param>
        internal CompletedTreatmentItemValidationScope(ValidationService service,
            CompletedTreatmentItemValidationScope? previous,
            MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation? completed)
        { _service = service; _previous = previous; _completed = completed; }

        /// <summary>

        /// Whether an actual invocation's parsed root has been observed.

        /// </summary>
        internal bool Observed { get { lock (_gate) return _observed; } }

        /// <summary>

        /// Whether comparison eligibility has permanently ended.

        /// </summary>
        internal bool Invalidated { get { lock (_gate) return _invalidated; } }

        /// <summary>

        /// Discards all eligibility permanently before repair, disposal or a refusing consumption.

        /// </summary>
        internal void Invalidate()
        { lock (_gate) InvalidateCore(); }

        private void InvalidateCore()
        { _invalidated = true; _completed = null; _parsedRoot = null; _invocation = null; }

        /// <summary>

        /// Captures one actual parsed root under this service's private current invocation.

        /// </summary>
        /// <param name="root">The actual NPC root parsed by the ordinary guard.</param>
        internal void Observe(JsonElement root)
        {
            lock (_gate)
            {
                if (_invalidated || _completed is null) return;
                var invocation = _service._worldValidationInvocation.Value;
                if (_observed || invocation is null || invocation.Closed)
                { InvalidateCore(); return; }
                _observed = true; _invocation = invocation;
                _parsedRoot = JsonNode.Parse(root.GetRawText()) as JsonObject;
                if (_parsedRoot is null) InvalidateCore();
            }
        }

        private bool CanCompareCore()
        {
            var invocation = _service._worldValidationInvocation.Value;
            return !_invalidated && _completed is not null && _parsedRoot is not null &&
                invocation is not null && !invocation.Closed && ReferenceEquals(invocation, _invocation);
        }

        /// <summary>

        /// Checks current private invocation identity without exporting mutable scope state.

        /// </summary>
        /// <returns>True only while the one root observation remains eligible.</returns>
        internal bool CanCompare()
        { lock (_gate) return CanCompareCore(); }

        /// <summary>
        /// Atomically rechecks eligibility after awaiting a read, consumes once and compares the whole frame/image.
        /// </summary>
        /// <param name="sessionId">Authenticated original session.</param>
        /// <param name="requestId">Authenticated original request.</param>
        /// <param name="turn">Authenticated original turn.</param>
        /// <param name="snapshotToken">Authenticated full original seal.</param>
        /// <param name="generation">Generation already observed by the same read owner.</param>
        /// <param name="bytes">Whole current NPC bytes from that owner's read.</param>
        /// <param name="issues">Ordered actual invocation issues.</param>
        /// <returns>The unique exact removable issue index, or -1 for any refusal.</returns>
        internal int TryConsume(string sessionId, string requestId, int turn, string snapshotToken,
            string generation, byte[]? bytes, IReadOnlyList<ValidationIssue> issues)
        {
            lock (_gate)
            {
                if (!CanCompareCore()) return -1;
                var completed = _completed!;
                var root = _parsedRoot!;
                InvalidateCore();
                return completed.Matches(_service._fs, generation, sessionId, requestId, turn, snapshotToken, root, bytes)
                    ? completed.FindUniqueIssue(issues) : -1;
            }
        }

        /// <summary>

        /// Ends the scope without making any observation eligible again.

        /// </summary>
        public void Dispose()
        {
            Invalidate();
            if (ReferenceEquals(_service._completedTreatmentItems.Value, this))
                _service._completedTreatmentItems.Value = _previous;
        }
    }

    private void ObserveCompletedTreatmentNpcRoot(JsonElement root, string contextPrefix)
    {
        if (contextPrefix == NpcCoreChangesContract.NpcCorePath)
            _completedTreatmentItems.Value?.Observe(root);
    }

    private static bool HasCompletedTreatmentInventoryCandidate(IReadOnlyList<ValidationIssue> issues) =>
        issues.Any(static issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "npc_existing_inventory_resend_forbidden" && issue.Section == "NPCInventory" &&
            MortalWoundTreatmentItemPublicationAuthority.TryParseNpcInventoryIssuePath(issue.FilePath, out _, out _));

    private async Task CompareCompletedTreatmentItemsAsync(
        PendingSnapshotValidationInvocation invocation, List<ValidationIssue> issues)
    {
        var scope = _completedTreatmentItems.Value;
        if (scope is null || !scope.CanCompare()) return;
        if (!HasCompletedTreatmentInventoryCandidate(issues)) { scope.Invalidate(); return; }
        invocation.RequireCurrent();
        var physical = invocation.Physical!;
        var lookup = CurrentPendingSnapshotLookup(physical.Lease);
        if (lookup is null || lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is not { } manifest) return;
        var bytes = await _fs.ReadFileBytesAsync(physical.Lease!, NpcCoreChangesContract.NpcCorePath);
        invocation.RequireCurrent();
        var index = scope.TryConsume(manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
            manifest.ManifestPayloadHash, physical.ObservedGeneration, bytes, issues);
        if (index >= 0) issues.RemoveAt(index);
    }

    private async Task<int> CompareCompletedTreatmentItemsAsync(WorldReadPhase phase, IReadOnlyList<ValidationIssue> issues)
    {
        var scope = _completedTreatmentItems.Value;
        if (scope is null || !scope.CanCompare()) return -1;
        if (!HasCompletedTreatmentInventoryCandidate(issues)) { scope.Invalidate(); return -1; }
        phase.RequireCurrent();
        if (!phase.Physical.MatchesOriginalFrame(phase.Manifest.SessionId, phase.Manifest.RequestId,
                phase.Manifest.TurnNumber, phase.Manifest.ManifestPayloadHash))
        { scope.Invalidate(); return -1; }
        var bytes = await phase.ReadBytesAsync(NpcCoreChangesContract.NpcCorePath);
        phase.RequireCurrent();
        return scope.TryConsume(phase.Manifest.SessionId, phase.Manifest.RequestId, phase.Manifest.TurnNumber,
            phase.Manifest.ManifestPayloadHash, phase.Physical.ObservedGeneration, bytes, issues);
    }
}
