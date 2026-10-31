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
    /// Owns only an invocation-local parsed-root observation and one immutable completed comparison.
    /// </summary>
    internal sealed class CompletedTreatmentItemValidationScope : IDisposable
    {
        private readonly ValidationService _service;
        private readonly CompletedTreatmentItemValidationScope? _previous;
        // Opaque identity only; no dispatcher, caller authority or invocation API is exposed.
        internal MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation? Completed;
        internal object? InvocationIdentity;
        internal JsonObject? ParsedRoot;
        internal bool Observed, Invalidated;

        /// <summary>Installs DATA in this validator's existing completed-publication scope chain.</summary>
        /// <param name="service">Owning validator.</param>
        /// <param name="previous">Previous scope restored on disposal.</param>
        /// <param name="completed">Genuine completed comparison, or null.</param>
        internal CompletedTreatmentItemValidationScope(ValidationService service,
            CompletedTreatmentItemValidationScope? previous,
            MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation? completed)
        { _service = service; _previous = previous; Completed = completed; }

        /// <summary>
        /// Discards every observation and comparison permanently before repair or disposal.
        /// </summary>
        internal void Invalidate()
        { Invalidated = true; Completed = null; ParsedRoot = null; InvocationIdentity = null; }

        /// <summary>
        /// Ends the current scope without making any prior observation eligible again.
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
        var scope = _completedTreatmentItems.Value;
        if (contextPrefix != NpcCoreChangesContract.NpcCorePath || scope is null ||
            scope.Invalidated || scope.Completed is null) return;
        var invocation = _worldValidationInvocation.Value;
        if (scope.Observed || invocation is null || invocation.Closed)
        { scope.Invalidate(); return; }
        scope.Observed = true; scope.InvocationIdentity = invocation;
        scope.ParsedRoot = JsonNode.Parse(root.GetRawText()) as JsonObject;
        if (scope.ParsedRoot is null) scope.Invalidate();
    }

    private bool CanCompareCompletedTreatmentItems(CompletedTreatmentItemValidationScope? scope)
    {
        var invocation = _worldValidationInvocation.Value;
        return scope is not null && !scope.Invalidated && scope.Completed is not null && scope.ParsedRoot is not null &&
            invocation is not null && !invocation.Closed && ReferenceEquals(invocation, scope.InvocationIdentity);
    }

    private int ConsumeCompletedTreatmentItemComparison(CompletedTreatmentItemValidationScope scope,
        ValidationPendingTurnSnapshotManifest manifest, byte[]? bytes, IReadOnlyList<ValidationIssue> issues)
    {
        var completed = scope.Completed!;
        var root = scope.ParsedRoot!;
        scope.Invalidate(); // One invocation comparison; later roots cannot reactivate it.
        return completed.Matches(_fs, manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
            manifest.ManifestPayloadHash, root, bytes) ? completed.FindUniqueIssue(issues) : -1;
    }

    private async Task CompareCompletedTreatmentItemsAsync(
        PendingSnapshotValidationInvocation invocation, List<ValidationIssue> issues)
    {
        var scope = _completedTreatmentItems.Value;
        if (!CanCompareCompletedTreatmentItems(scope)) return;
        invocation.RequireCurrent();
        var manifest = CurrentPendingSnapshotLookup(invocation.Physical!.Lease)?.Manifest;
        if (manifest is null) return;
        var bytes = await _fs.ReadFileBytesAsync(invocation.Physical.Lease!, NpcCoreChangesContract.NpcCorePath);
        invocation.RequireCurrent();
        var index = ConsumeCompletedTreatmentItemComparison(scope!, manifest, bytes, issues);
        if (index >= 0) issues.RemoveAt(index);
    }

    private async Task<int> CompareCompletedTreatmentItemsAsync(
        WorldReadPhase phase, IReadOnlyList<ValidationIssue> issues)
    {
        var scope = _completedTreatmentItems.Value;
        if (!CanCompareCompletedTreatmentItems(scope)) return -1;
        phase.RequireCurrent();
        var bytes = await phase.ReadBytesAsync(NpcCoreChangesContract.NpcCorePath);
        phase.RequireCurrent();
        return ConsumeCompletedTreatmentItemComparison(scope!, phase.Manifest, bytes, issues);
    }
}
