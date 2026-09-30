using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum EffectIdentityWriteKind
{
    CreateEntry,
    AppendTransition,
    InsertBeforeTransition
}

internal enum EffectIdentityAllocationKind
{
    Effect,
    Transition,
    Resolution
}

internal sealed record EffectIdentityAllocationReceipt(
    long Ordinal,
    EffectIdentityAllocationKind Kind,
    string Identity);

internal sealed record EffectIdentityWriteReceipt(
    long Ordinal,
    EffectIdentityWriteKind Kind,
    string EffectId,
    string? BeforeState,
    string? AfterState,
    string? AnchorTransitionId,
    int? AnchorIndex,
    string? AnchorJson,
    string PayloadJson);

internal sealed record EffectIdentityHistoryAnchor(
    string EffectId,
    string? TransitionId,
    string TransitionJson);

internal sealed class EffectIdentityReplacementAgreement
{
    private readonly EffectIdentityHistoryAnchor[] _anchors;

    internal EffectIdentityReplacementAgreement(
        string eventRef,
        EffectReplayIdentity result,
        EffectReplayIdentity? replaced,
        long writeCount,
        IEnumerable<EffectIdentityHistoryAnchor> anchors)
    {
        EventRef = eventRef;
        Result = result;
        Replaced = replaced;
        WriteCount = writeCount;
        _anchors = anchors.ToArray();
    }

    internal string EventRef { get; }
    internal EffectReplayIdentity Result { get; }
    internal EffectReplayIdentity? Replaced { get; }
    internal long WriteCount { get; }
    internal IReadOnlyList<EffectIdentityHistoryAnchor> Anchors =>
        Array.AsReadOnly(_anchors.ToArray());
}

// Owns only identity history and the effect-factory call stream.
// This is not a carrier/effect draft, source authority, or resumable turn.
internal sealed class EffectIdentityHistoryOwner : IDisposable
{
    private readonly JsonObject _root;
    private readonly string[] _baselineEffectIds;
    private readonly string[] _baselineTransitionIds;
    private readonly List<EffectIdentityWriteReceipt> _writes = new();
    private readonly List<EffectIdentityAllocationReceipt> _allocations = new();
    private readonly List<EffectIdentityReplacementAgreement> _agreements = new();
    private bool _published;
    private bool _disposed;
    private bool _faulted;

    internal EffectIdentityHistoryOwner(JsonObject baseline, EffectIdentityFactory factory)
        : this(baseline, factory, Array.Empty<string>(), Array.Empty<string>())
    {
    }

    internal EffectIdentityHistoryOwner(
        JsonObject baseline,
        EffectIdentityFactory factory,
        IReadOnlyList<string> baselineEffectIds,
        IReadOnlyList<string> baselineTransitionIds)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(baselineEffectIds);
        ArgumentNullException.ThrowIfNull(baselineTransitionIds);
        _root = baseline.DeepClone().AsObject();
        _baselineEffectIds = baselineEffectIds.ToArray();
        _baselineTransitionIds = baselineTransitionIds.ToArray();
        Factory = new OwnedFactory(this, factory);
    }

    internal EffectIdentityFactory Factory { get; }
    internal IReadOnlyList<string> AllocatedEffectIds => Array.AsReadOnly(_baselineEffectIds.Concat(
        _allocations.Where(static value => value.Kind == EffectIdentityAllocationKind.Effect)
            .Select(static value => value.Identity)).ToArray());
    internal IReadOnlyList<string> AllocatedTransitionIds => Array.AsReadOnly(_baselineTransitionIds.Concat(
        _allocations.Where(static value => value.Kind == EffectIdentityAllocationKind.Transition)
            .Select(static value => value.Identity)).ToArray());
    internal IReadOnlyList<EffectIdentityWriteReceipt> Writes => Array.AsReadOnly(_writes.ToArray());
    internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations => Array.AsReadOnly(_allocations.ToArray());
    internal IReadOnlyList<EffectIdentityReplacementAgreement> ReplacementAgreements =>
        Array.AsReadOnly(_agreements.ToArray());

    // Bounded detached ranges for the enclosing materialization owner. These
    // do not change write, allocation, anchor or publication semantics.
    internal int WriteCount => _writes.Count;
    internal int AllocationCount => _allocations.Count;
    internal int ReplacementAgreementCount => _agreements.Count;

    internal IReadOnlyList<EffectIdentityWriteReceipt> ReadWritesFrom(int start) =>
        Array.AsReadOnly(_writes.GetRange(start, _writes.Count - start).ToArray());

    internal IReadOnlyList<EffectIdentityAllocationReceipt> ReadAllocationsFrom(int start) =>
        Array.AsReadOnly(_allocations.GetRange(start, _allocations.Count - start).ToArray());

    internal IReadOnlyList<EffectIdentityReplacementAgreement> ReadReplacementAgreementsFrom(int start) =>
        Array.AsReadOnly(_agreements.GetRange(start, _agreements.Count - start).ToArray());

    internal JsonObject ReadSnapshot()
    {
        EnsureNotDisposed();
        return _root.DeepClone().AsObject();
    }

    internal IReadOnlyList<JsonObject> ReadEntries()
    {
        EnsureNotDisposed();
        return Array.AsReadOnly(Entries().Select(static value => value.DeepClone().AsObject()).ToArray());
    }

    internal JsonObject[] FindEntries(string effectId)
    {
        EnsureNotDisposed();
        return Matches(effectId).Select(static value => value.DeepClone().AsObject()).ToArray();
    }

    internal void CreateEntry(JsonObject entry)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(entry);
        var owned = entry.DeepClone().AsObject();
        _root["entries"]!.AsArray().Add(owned);
        _writes.Add(new EffectIdentityWriteReceipt(
            _writes.Count, EffectIdentityWriteKind.CreateEntry,
            owned["effectId"]?.GetValue<string>() ?? string.Empty, null,
            owned["state"]?.GetValue<string>(), null, null, null, owned.ToJsonString()));
    }

    internal bool TryAppendTransition(string effectId, string state, JsonObject transition)
    {
        EnsureMutable();
        var matches = Matches(effectId).ToArray();
        if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions)
            return false;
        var before = matches[0]["state"]?.GetValue<string>();
        var owned = transition.DeepClone().AsObject();
        matches[0]["state"] = state;
        transitions.Add(owned);
        _writes.Add(new EffectIdentityWriteReceipt(
            _writes.Count, EffectIdentityWriteKind.AppendTransition,
            effectId, before, state, null, null, null, owned.ToJsonString()));
        return true;
    }

    // The caller supplies the exact last-transition position it just validated.
    // Position plus payload preserves old late-validation behavior even if a
    // deliberately colliding allocator has emitted duplicate transition IDs.
    internal void InsertBeforeTransition(
        string effectId, int anchorIndex, JsonObject expectedAnchor, JsonObject transition)
    {
        EnsureMutable();
        var matches = Matches(effectId).ToArray();
        if (matches.Length != 1 ||
            matches[0]["transitions"] is not JsonArray transitions ||
            anchorIndex < 0 || anchorIndex >= transitions.Count ||
            transitions[anchorIndex] is not JsonObject anchor ||
            !JsonNode.DeepEquals(anchor, expectedAnchor))
        {
            throw new InvalidOperationException("The validated identity-history insertion anchor changed.");
        }
        var state = matches[0]["state"]?.GetValue<string>();
        var owned = transition.DeepClone().AsObject();
        var anchorJson = anchor.ToJsonString();
        var anchorId = anchor["transitionId"]?.GetValue<string>();
        transitions.Insert(anchorIndex, owned);
        _writes.Add(new EffectIdentityWriteReceipt(
            _writes.Count, EffectIdentityWriteKind.InsertBeforeTransition,
            effectId, state, state, anchorId, anchorIndex, anchorJson, owned.ToJsonString()));
    }

    // Called only after the existing exact replacement checks succeed.
    // Retains their history anchors, not a caller-supplied validity flag.
    internal void RetainReplacementAgreement(
        string eventRef,
        EffectReplayIdentity result,
        EffectReplayIdentity? replaced,
        string createEventRef,
        string producerEffectId)
    {
        EnsureMutable();
        var anchors = new List<EffectIdentityHistoryAnchor>();
        anchors.Add(CaptureAnchor(result.EffectId, "create", createEventRef, producerEffectId, result.EffectId));
        if (replaced != null)
            anchors.Add(CaptureAnchor(replaced.EffectId, "replace", eventRef, replaced.EffectId, result.EffectId));
        _agreements.Add(new EffectIdentityReplacementAgreement(
            eventRef, result, replaced, _writes.Count, anchors));
    }

    internal JsonObject Publish()
    {
        EnsureMutable();
        foreach (var agreement in _agreements)
        {
            foreach (var expected in agreement.Anchors)
            {
                var matches = Matches(expected.EffectId).ToArray();
                if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions ||
                    transitions.OfType<JsonObject>().Count(value =>
                        string.Equals(value["transitionId"]?.GetValue<string>(),
                            expected.TransitionId, StringComparison.Ordinal) &&
                        JsonNode.DeepEquals(value, JsonNode.Parse(expected.TransitionJson))) != 1)
                {
                    throw new InvalidOperationException("An agreed replacement history anchor changed.");
                }
            }
        }
        _published = true;
        return _root.DeepClone().AsObject();
    }

    public void Dispose() => _disposed = true;

    private IEnumerable<JsonObject> Entries() =>
        _root["entries"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

    private IEnumerable<JsonObject> Matches(string effectId) =>
        Entries().Where(entry => string.Equals(
            entry["effectId"]?.GetValue<string>(), effectId, StringComparison.Ordinal));

    private EffectIdentityHistoryAnchor CaptureAnchor(
        string effectId, string kind, string eventRef, string sourceEffectId, string resultEffectId)
    {
        // Match the existing authority dictionary's first-entry selection. A
        // duplicate allocated effect ID must keep its existing diagnostic path.
        var entry = Matches(effectId).First();
        var candidates = entry["transitions"]!.AsArray().OfType<JsonObject>().Where(value =>
            string.Equals(value["kind"]?.GetValue<string>(), kind, StringComparison.Ordinal) &&
            string.Equals(value["eventRef"]?.GetValue<string>(), eventRef, StringComparison.Ordinal) &&
            value["sourceEffectIds"] is JsonArray { Count: 1 } sources &&
            string.Equals(sources[0]?.GetValue<string>(), sourceEffectId, StringComparison.Ordinal) &&
            value["resultEffectIds"] is JsonArray { Count: 1 } results &&
            string.Equals(results[0]?.GetValue<string>(), resultEffectId, StringComparison.Ordinal));
        // The existing create check requires one match; replacement checks the
        // last transition, not uniqueness among all historical replace payloads.
        var transition = kind == "create" ? candidates.Single() : candidates.Last();
        return new EffectIdentityHistoryAnchor(
            effectId, transition["transitionId"]?.GetValue<string>(), transition.ToJsonString());
    }

    private string Allocate(EffectIdentityAllocationKind kind, Func<string> allocate)
    {
        EnsureMutable();
        try
        {
            var identity = allocate();
            _allocations.Add(new EffectIdentityAllocationReceipt(_allocations.Count, kind, identity));
            return identity;
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EffectIdentityHistoryOwner));
    }

    /// <summary>
    /// Guards reference allocation with this owner's lifetime without adding an effect-history receipt.
    /// </summary>
    /// <param name="allocate">
    /// Underlying combatant or member callback; a failure faults this owner.
    /// </param>
    /// <returns>
    /// The underlying owner-generated reference identity.
    /// </returns>
    private string AllocateReference(Func<string> allocate)
    {
        EnsureMutable();
        try
        {
            return allocate();
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    private void EnsureMutable()
    {
        EnsureNotDisposed();
        if (_published || _faulted)
            throw new InvalidOperationException("The identity-history owner is no longer writable.");
    }

    private sealed class OwnedFactory(
        EffectIdentityHistoryOwner owner,
        EffectIdentityFactory underlying) : EffectIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateEffectId(EffectIdentityAllocationKey key) =>
            owner.Allocate(EffectIdentityAllocationKind.Effect, () => underlying.CreateEffectId(key));

        /// <inheritdoc/>
        internal override string CreateTransitionId(EffectIdentityAllocationKey key) =>
            owner.Allocate(EffectIdentityAllocationKind.Transition, () => underlying.CreateTransitionId(key));

        /// <inheritdoc/>
        internal override string CreateResolutionId(EffectIdentityAllocationKey key) =>
            owner.Allocate(EffectIdentityAllocationKind.Resolution, () => underlying.CreateResolutionId(key));

        /// <inheritdoc/>
        internal override string CreateCombatantId(string combatantRef) =>
            owner.AllocateReference(() => underlying.CreateCombatantId(combatantRef));

        /// <inheritdoc/>
        internal override string CreateMemberId(string memberRef) =>
            owner.AllocateReference(() => underlying.CreateMemberId(memberRef));

        /// <summary>
        /// Preserves the underlying combatant allocation policy and this owner's lifetime guard.
        /// </summary>
        /// <returns>
        /// Underlying identity, or rejection when that policy requires a typed reference.
        /// </returns>
        internal override string CreateCombatantId() => owner.AllocateReference(underlying.CreateCombatantId);

        /// <summary>
        /// Preserves the underlying member allocation policy and this owner's lifetime guard.
        /// </summary>
        /// <returns>
        /// Underlying identity, or rejection when that policy requires a typed reference.
        /// </returns>
        internal override string CreateMemberId() => owner.AllocateReference(underlying.CreateMemberId);

        internal override string CreateEffectId() =>
            owner.Allocate(EffectIdentityAllocationKind.Effect, underlying.CreateEffectId);
        internal override string CreateTransitionId() =>
            owner.Allocate(EffectIdentityAllocationKind.Transition, underlying.CreateTransitionId);
        internal override string CreateResolutionId() =>
            owner.Allocate(EffectIdentityAllocationKind.Resolution, underlying.CreateResolutionId);
    }
}
