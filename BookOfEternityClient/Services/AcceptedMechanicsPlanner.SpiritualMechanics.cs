using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        /// <summary>
        /// Captures current spiritual contributions through the exact retained source and capture owners.
        /// </summary>
        /// <param name="capture">
        /// Current original-turn capture owning this resource executor.
        /// </param>
        /// <param name="source">
        /// Capture-owned source supplying signed conflict membership.
        /// </param>
        /// <param name="issues">
        /// Receives ownership or mechanics projection failures.
        /// </param>
        /// <returns>
        /// A private epoch-bound context, or null on rejection.
        /// </returns>
        internal SpiritualMechanicsContext? CaptureSpiritualMechanics(
            ValidationService.SpiritualOriginalTurnCapture capture,
            ValidationService.SpiritualWoundSourceSession source, List<ValidationIssue> issues) =>
            SpiritualMechanicsContext.Capture(this, capture, source, issues);

        /// <summary>
        /// Retains source-validation contributions from an exact resource routing epoch and exchange ordinal.
        /// </summary>
        internal sealed class SpiritualMechanicsContext
        {
            private readonly ResourceExecutionSession _owner;
            private readonly ValidationService.SpiritualOriginalTurnCapture _capture;
            private readonly ValidationService.SpiritualWoundSourceSession _source;
            private readonly object _epoch;
            private readonly EffectAcceptedTurnPlanner.EffectAcceptedDraft _woundDraft;
            private readonly long _woundVersion;
            private readonly long _dependencyCutVersion;
            private WoundOperationBeforeData? _woundView;
            private readonly AfterlifeConflictActionPointProjection? _actionPointBefore;

            /// <summary>
            /// Retains the actual owner's routing epoch and currently available component projection.
            /// </summary>
            /// <param name="owner">
            /// Usable resource executor holding its capture gate.
            /// </param>
            /// <param name="capture">
            /// Original-turn capture that owns the executor and source.
            /// </param>
            /// <param name="source">
            /// Signed source owner used to authenticate conflict membership.
            /// </param>
            /// <param name="projection">
            /// Accepted installed-view projection before filtering consumed or reserved instances.
            /// </param>
            private SpiritualMechanicsContext(ResourceExecutionSession owner,
                ValidationService.SpiritualOriginalTurnCapture capture,
                ValidationService.SpiritualWoundSourceSession source,
                SpiritualWoundConflictContributionProjection projection)
            {
                _owner = owner;
                _capture = capture;
                _source = source;
                _epoch = owner._routing!.RoutingEpoch;
                _woundDraft = capture.ReadOwnedWoundDraft(owner, source) ??
                    throw new InvalidOperationException("The actual capture must own its wound draft.");
                _woundVersion = _woundDraft.WoundReadVersion;
                _dependencyCutVersion = _woundDraft.DependencyCutVersion;
                Ordinal = owner._nextOrdinal;
                if (source.ReadMechanicsConflict(capture)["activeConflict"] is JsonObject conflict)
                    _actionPointBefore = owner._state.ReadSpiritualActionPointBefore(conflict);
                Contributions = Array.AsReadOnly(projection.Contributions
                    .Where(row => owner._state.IsMechanicallyAvailable(row.EffectId)).ToArray());
            }

            /// <summary>
            /// Gets the actual next resource ordinal at capture time.
            /// </summary>
            internal int Ordinal { get; }
            /// <summary>
            /// Gets immutable contributions after filtering accepted use exhaustion.
            /// </summary>
            internal IReadOnlyList<SpiritualWoundConflictContribution> Contributions { get; }
            /// <summary>
            /// Gets whether the original owners, exchange ordinal and routing epoch remain current.
            /// </summary>
            internal bool IsCurrent => _capture.OwnsMechanicsOwner(_owner, _source) &&
                !_owner._disposed && !_owner._faulted && _owner.Result == null &&
                Ordinal == _owner._nextOrdinal && ReferenceEquals(_epoch, _owner._routing?.RoutingEpoch) &&
                ReferenceEquals(_woundDraft, _capture.ReadOwnedWoundDraft(_owner, _source)) &&
                _woundDraft.IsCurrentWoundRead(_woundVersion) && _dependencyCutVersion == _woundDraft.DependencyCutVersion;

            /// <summary>
            /// Checks whether a historical context came from the same actual capture, source and resource executor.
            /// </summary>
            /// <param name="other">
            /// Previously captured context; its ordinal and routing epoch may already be closed.
            /// </param>
            /// <returns>
            /// True for the exact same three owners, regardless of historical epoch freshness; otherwise false.
            /// </returns>
            internal bool HasSameOwner(SpiritualMechanicsContext other) =>
                ReferenceEquals(_owner, other._owner) && ReferenceEquals(_capture, other._capture) &&
                ReferenceEquals(_source, other._source);

            /// <summary>
            /// Reads the frozen exchange-start resource image from its exact current owner context.
            /// </summary>
            /// <param name="conflictId">
            /// Signed conflict identity expected by the source or producer.
            /// </param>
            /// <param name="ordinal">
            /// Global current-turn exchange ordinal being checked, including missing-side completion.
            /// </param>
            /// <returns>
            /// The immutable two-side image; stale or mismatched ownership throws.
            /// </returns>
            internal AfterlifeConflictActionPointProjection ReadActionPointBefore(string conflictId, int ordinal)
            {
                if (!IsCurrent || Ordinal != ordinal || _actionPointBefore?.ConflictId != conflictId)
                    throw new InvalidOperationException("Exact current exchange-start resource ownership is required.");
                return _actionPointBefore;
            }

            /// <summary>
            /// Lazily freezes the exact current wound view for an explicit re-trauma check.
            /// Ordinary exchanges do not read or initialize wound state through this method.
            /// </summary>
            /// <param name="issues">
            /// Receives stale-owner or wound-view validation failures.
            /// </param>
            /// <returns>
            /// Detached data for this captured version, or null when the context is no longer current.
            /// </returns>
            internal WoundOperationBeforeData? ReadCurrentWoundView(List<ValidationIssue> issues)
            {
                if (!IsCurrent)
                {
                    issues.AddRange(Issue("spiritual_wound_read_stale",
                        "the actual captured resource ordinal, draft version and routing epoch", "stale"));
                    return null;
                }
                return _woundView ??= _woundDraft.ReadCurrentWoundView(
                    _capture, _owner, _source, _woundVersion, issues);
            }

            /// <summary>
            /// Mints a context only from exact live owners at an unstaged resource frontier.
            /// </summary>
            /// <param name="owner">
            /// Executor whose exclusive gate protects the projection.
            /// </param>
            /// <param name="capture">
            /// Current capture owning the executor.
            /// </param>
            /// <param name="source">
            /// Actual source supplying signed conflict membership.
            /// </param>
            /// <param name="issues">
            /// Receives ownership or projection failures.
            /// </param>
            /// <returns>
            /// An owned context, or <see langword="null"/> on rejection.
            /// </returns>
            internal static SpiritualMechanicsContext? Capture(ResourceExecutionSession owner,
                ValidationService.SpiritualOriginalTurnCapture capture,
                ValidationService.SpiritualWoundSourceSession source, List<ValidationIssue> issues)
            {
                owner.Enter();
                try
                {
                    owner.EnsureUsable();
                    if (!capture.OwnsMechanicsOwner(owner, source) || owner._routing == null ||
                        owner._active != null || owner._staged != null || owner._pendingExchange)
                    {
                        issues.AddRange(Issue("spiritual_wound_mechanics_owner_mismatch",
                            "exact original capture at the next unstaged resource frontier", "unavailable"));
                        return null;
                    }
                    var projection = owner._routing.ProjectSpiritualMechanics(source.ReadMechanicsConflict(capture));
                    issues.AddRange(projection.Issues);
                    return projection.IsAccepted ? new(owner, capture, source, projection) : null;
                }
                finally { owner.Exit(); }
            }
        }
    }

    private sealed partial class ResourceExecutionState
    {
        /// <summary>
        /// Freezes the two live action-point entries using the signed conflict's owner bindings.
        /// </summary>
        /// <param name="conflict">
        /// Detached original active conflict with client-owned resource coordinates.
        /// </param>
        /// <returns>
        /// Immutable entries from the current ledger; missing or inactive coordinates throw.
        /// </returns>
        internal AfterlifeConflictActionPointProjection ReadSpiritualActionPointBefore(JsonObject conflict)
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(conflict["conflictId"]);
            var oppositionId = AfterlifeSpiritualConflictState.GetNodeString(
                conflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?["opposition"]?["resourceOwnerId"]);
            if (!ResourceMaterializationContract.IsExactIdentifier(conflictId) ||
                !ResourceMaterializationContract.IsExactIdentifier(oppositionId) ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                    AfterlifeSpiritualConflictState.GetNodeString(conflict["realm"]), out var realm))
                throw new InvalidOperationException("Signed conflict resource bindings are required.");
            var player = workingLedger.Resolve(new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeActor,
                "player_soul", "spiritual_action_points"));
            var opposition = workingLedger.Resolve(new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeConflictSide,
                oppositionId!, "spiritual_action_points"));
            if (player.State != ResourceLifecycleState.Active || opposition.State != ResourceLifecycleState.Active)
                throw new InvalidOperationException("Exchange-start action-point entries must be active.");
            return new(conflictId!, player, opposition);
        }

        /// <summary>
        /// Applies accepted use exhaustion and terminal availability reservations to mechanical projection.
        /// </summary>
        /// <param name="effectId">
        /// Canonical instance identity from the accepted mechanics snapshot.
        /// </param>
        /// <returns>
        /// True when the instance is neither exhausted nor reserved for terminal removal; otherwise false.
        /// </returns>
        internal bool IsMechanicallyAvailable(string effectId) =>
            effectTranscriptBuilder.IsAvailable(effectId) &&
            (!arbiter.TryGetRemainingUses(effectId, out var remaining) || remaining > 0);
    }
}
