namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        private WoundOperationBeforeData? _currentWoundState;
        private readonly Dictionary<EffectDraftWoundSelection,
            EffectDraftWoundBeforeAuthority> _woundBeforeStates = new();
        private long _woundVersion = 0;
        private EffectDraftWoundInsertion? _lastWoundInsertion;
        private EffectDraftWoundSelection? _capturingWoundBefore;

        /// <summary>
        /// Admits a concrete Spiritual selection to the shared wound draft.
        /// </summary>
        /// <param name="selection">
        /// Actual registered selection; copied or stale ownership is rejected by the draft.
        /// </param>
        /// <param name="failures">
        /// Receives validation and ownership diagnostics.
        /// </param>
        /// <returns>
        /// Current operation-before authority, or null when insertion cannot proceed.
        /// </returns>
        internal EffectDraftWoundBeforeAuthority? AdvanceForWoundInsertion(
            ValidationService.SpiritualOriginalTurnCapture.WoundSelection selection, List<ValidationIssue> failures) =>
            AdvanceForWoundInsertionCore(EffectDraftWoundSelection.From(selection), failures);

        /// <summary>
        /// Admits a concrete Mortal selection to the shared wound draft.
        /// </summary>
        /// <param name="selection">
        /// Actual registered selection; copied or stale ownership is rejected by the draft.
        /// </param>
        /// <param name="failures">
        /// Receives validation and ownership diagnostics.
        /// </param>
        /// <returns>
        /// Current operation-before authority, or null when insertion cannot proceed.
        /// </returns>
        internal EffectDraftWoundBeforeAuthority? AdvanceForWoundInsertion(
            ValidationService.MortalOriginalTurnCapture.WoundSelection selection, List<ValidationIssue> failures) =>
            AdvanceForWoundInsertionCore(EffectDraftWoundSelection.From(selection), failures);

        /// <summary>
        /// Applies supported local trigger and replacement-reaction work and freezes the operation-before image for an owned creation or worsening.
        /// It does not run unrelated effects or any global lifetime or terminal phase.
        /// </summary>
        /// <param name="selection">
        /// Actual registered source-bound decision. Unrelated reaction work remains deferred.
        /// </param>
        /// <param name="failures">
        /// Receives ownership, unsupported-operation or state validation failures.
        /// </param>
        /// <returns>
        /// The retained current-before capability, or <see langword="null"/> if admission or state validation fails.
        /// </returns>
        private EffectDraftWoundBeforeAuthority? AdvanceForWoundInsertionCore(
            EffectDraftWoundSelection selection, List<ValidationIssue> failures)
        {
            ArgumentNullException.ThrowIfNull(selection);
            ArgumentNullException.ThrowIfNull(failures);
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Effect draft insertion cannot be re-entered.");
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_completed || _faulted)
                    throw new InvalidOperationException("The effect draft is no longer writable.");
                if (HasPendingWoundIntegration)
                    return Reject("spiritual_wound_routing_required", "complete owned routing integration before another insertion");
                if (!selection.IsCurrentFor(this))
                    return Reject("spiritual_wound_selection_stale", "the exact current source-bound wound selection");
                var selectedInput = selection.Input;
                if (selectedInput.Transitions.Count != 1 || selectedInput.Transitions[0].Kind is not ("create" or "worsen"))
                    return Reject("spiritual_wound_generation_required", "one source-bound create or worsen transition");
                if (_woundBeforeStates.TryGetValue(selection, out var retained))
                    return retained.IsCurrent ? retained : Reject("spiritual_wound_before_stale", "the unchanged draft version");
                var beforeState = _currentWoundState ?? selection.ReadInitialState(this, failures);
                if (beforeState == null || failures.Count != 0)
                    return null;
                IReadOnlyList<AcceptedEffectBoundaryActivation> pending = Array.Empty<AcceptedEffectBoundaryActivation>();
                var reactions = WoundReactionCutPreparation.Empty;
                if (selectedInput.Transitions[0].Kind == "worsen" &&
                    !TryPrepareWoundDependencyCut(selection, selectedInput, beforeState, failures, out pending, out reactions))
                    return Reject("spiritual_wound_generation_required",
                        "materialize the selected wound's actual pending effect dependency closure before worsening");
                _currentWoundState ??= beforeState;
                try
                {
                    if (!InitializeState())
                    {
                        _faulted = true;
                        failures.AddRange(issues);
                        return null;
                    }
                    if (!ApplyWoundDependencyCut(selection, pending, reactions, failures))
                        return null;
                    // Actual local chronology now precedes the retained before image.
                    // Unrelated work and all global phases remain deferred.
                    _capturingWoundBefore = selection;
                    return EffectDraftWoundBeforeAuthority.Retain(this, selection);
                }
                catch
                {
                    _faulted = true;
                    throw;
                }
            }
            finally
            {
                _capturingWoundBefore = null;
                System.Threading.Volatile.Write(ref _busy, 0);
            }

            EffectDraftWoundBeforeAuthority? Reject(string code, string expected)
            {
                Add(failures, "acceptedTurn.wounds", code, expected, "unavailable");
                return null;
            }
        }

        /// <summary>
        /// Binds detached operation-before data to the actual owning draft, source selection and retained version.
        /// Copying its fields or serialized images cannot recreate its registered identity.
        /// </summary>
        internal sealed class EffectDraftWoundBeforeAuthority
        {
            private readonly EffectAcceptedDraft _owner;
            private readonly WoundOperationBeforeData _data;
            private readonly long _cutVersion;
            private WoundAcceptedTurnPreparationResult? _prepared;
            private bool _preparing;

            /// <summary>
            /// Captures one registered draft version and its already detached state.
            /// </summary>
            /// <param name="owner">
            /// Actual draft supplying both the wound and effect images.
            /// </param>
            /// <param name="selection">
            /// Actual source-bound decision at that draft's current frontier.
            /// </param>
            /// <param name="data">
            /// Detached state read internally from the owning draft.
            /// </param>
            private EffectDraftWoundBeforeAuthority(EffectAcceptedDraft owner,
                EffectDraftWoundSelection selection, WoundOperationBeforeData data)
            {
                _owner = owner;
                Selection = selection;
                _data = data;
                RetirementHistory = owner._lastWoundInsertion;
                Version = owner._woundVersion;
                _cutVersion = owner._dependencyCutVersion;
                Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.wound.draft_before", "1", owner.plan.InputFingerprint,
                    WoundAcceptedTurnFingerprints.ComputeInput(selection.Input), selection.Locations?.Fingerprint,
                    Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _cutVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    WoundAcceptedTurnFingerprints.ComputeOperationBefore(data)
                });
            }

            /// <summary>
            /// Gets the real registered source selection whose insertion reads this state.
            /// </summary>
            internal EffectDraftWoundSelection Selection { get; }
            /// <summary>
            /// Gets the draft wound generation version before this operation.
            /// </summary>
            internal long Version { get; }
            /// <summary>
            /// Gets the image, original-plan, selection and version fingerprint; it is not a reconstructible capability.
            /// </summary>
            internal string Fingerprint { get; }
            /// <summary>
            /// Gets immutable detached data; each mutable snapshot getter returns a fresh copy.
            /// </summary>
            internal WoundOperationBeforeData Data => _data;
            /// <summary>
            /// Gets prior registered insertion evidence for identities actually retired by this draft, or null before any insertion.
            /// </summary>
            internal EffectDraftWoundInsertion? RetirementHistory { get; }
            /// <summary>
            /// Gets whether this exact retained proof still permits preparation at the current source frontier and draft version.
            /// </summary>
            internal bool IsCurrent => !_owner._disposed && !_owner._faulted && !_owner._completed &&
                _owner._woundVersion == Version && _owner._dependencyCutVersion == _cutVersion && Selection.IsCurrentFor(_owner) &&
                _owner._woundBeforeStates.TryGetValue(Selection, out var retained) && ReferenceEquals(retained, this);

            /// <summary>
            /// Gets whether this exact proof is currently executing preparation under its draft's mutation guard.
            /// </summary>
            internal bool IsPreparing => _preparing && _owner._busy == 1 && IsCurrent;

            /// <summary>
            /// Authenticates a current root's terminal cut using its sealed old wound and exact real writer image.
            /// </summary>
            /// <param name="wound">
            /// Old wound whose current root is being considered for a new generation.
            /// </param>
            /// <param name="identity">
            /// Terminal identity from that same operation-before image.
            /// </param>
            /// <returns>
            /// True only under this live proof's mutation gate for an exact owned last-use expiry; otherwise false.
            /// </returns>
            internal bool AuthenticatesTerminalCut(WoundMaterializationEnvelope wound, EffectIdentityEntry identity)
            {
                if (_owner._busy != 1 || !IsCurrent || _data.WoundCarriers == null || _data.EffectIdentity == null ||
                    !WoundCarrierCatalog.Build(_data.WoundCarriers).TryResolveOne(wound.WoundId, out var retained) ||
                    WoundMaterializationContract.SerializeCanonical(retained.Wound) != WoundMaterializationContract.SerializeCanonical(wound) ||
                    !wound.Consequences.OwnedEffectSources.RootBindings.Any(root => root.EffectId == identity.EffectId))
                    return false;
                var parsed = ParseIdentity(_data.EffectIdentity);
                if (parsed.Issues.Count != 0 || parsed.State == null ||
                    !parsed.State.TryGetEntry(identity.EffectId, out var exact) ||
                    !System.Text.Json.Nodes.JsonNode.DeepEquals(exact.Raw, identity.Raw))
                    return false;
                var group = new EffectIdentitySourceGroup(wound.Owner.Realm, "wound", wound.WoundId);
                return _owner._woundTriggerJournal.Values.Any(receipt =>
                    receipt.TryReadTerminalIdentity(group, parsed.State, out var terminal, out _) && terminal!.EffectId == identity.EffectId);
            }

            /// <summary>
            /// Checks historical registration and the retained preparation seal without requiring the insertion frontier to stay current.
            /// </summary>
            /// <param name="prepared">
            /// Prepared result or trusted clone whose fingerprint must match the retained allocation result.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for the exact live proof and its retained preparation; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool MatchesPrepared(WoundPreparedAcceptedTurnPlan prepared) =>
                !_owner._disposed && !_owner._faulted &&
                _owner._woundBeforeStates.TryGetValue(Selection, out var retained) && ReferenceEquals(retained, this) &&
                ReferenceEquals(prepared.DraftBefore, this) && _prepared?.Plan is { } owned &&
                prepared.WoundPreparationFingerprint == owned.WoundPreparationFingerprint &&
                prepared.BaselineAuthority.AuthoritySeal == owned.BaselineAuthority.AuthoritySeal;

            /// <summary>
            /// Prepares the actual current selection once, retaining the allocation result for identical retries.
            /// </summary>
            /// <returns>
            /// The retained preparation or stale-proof issues; a failed allocation attempt faults the draft.
            /// </returns>
            internal WoundAcceptedTurnPreparationResult Prepare()
            {
                if (System.Threading.Interlocked.CompareExchange(ref _owner._busy, 1, 0) != 0)
                    throw new InvalidOperationException("Effect draft preparation cannot be re-entered.");
                try
                {
                    if (!IsCurrent)
                    {
                        var failures = new List<ValidationIssue>();
                        Add(failures, "acceptedTurn.wounds", "spiritual_wound_before_stale",
                            "the exact current draft-before proof", "stale or unowned");
                        return new WoundAcceptedTurnPreparationResult(null, failures);
                    }
                    if (_prepared != null)
                        return _prepared;
                    _preparing = true;
                    try
                    {
                        _prepared = WoundAcceptedTurnPlanner.PrepareOwnedDraft(this);
                        if (!_prepared.Success)
                            _owner._faulted = true;
                        return _prepared;
                    }
                    catch
                    {
                        _owner._faulted = true;
                        throw;
                    }
                    finally { _preparing = false; }
                }
                finally { System.Threading.Volatile.Write(ref _owner._busy, 0); }
            }

            /// <summary>
            /// Applies this proof's retained preparation to its own draft without accepting caller-authored batches.
            /// The original capture must hold its gate through application and resource registration.
            /// </summary>
            /// <param name="failures">
            /// Receives preparation or application validation failures.
            /// </param>
            /// <returns>
            /// The actual unpublished insertion, or <see langword="null"/> when no valid preparation is retained.
            /// </returns>
            internal EffectDraftWoundInsertion? Apply(List<ValidationIssue> failures)
            {
                if (_prepared?.Plan is not { } prepared)
                {
                    Add(failures, "acceptedTurn.wounds", "spiritual_wound_preparation_required",
                        "this proof's retained preparation", "missing");
                    return null;
                }
                return _owner.ApplyPreparedWound(this, prepared, failures);
            }

            /// <summary>
            /// Registers an operation-before proof after the owning draft has prepared its real state.
            /// </summary>
            /// <param name="owner">
            /// Draft currently holding its exclusive mutation guard.
            /// </param>
            /// <param name="selection">
            /// Exact source-bound selection already checked by the draft.
            /// </param>
            /// <returns>
            /// The newly registered proof, which can be consumed only while its version remains current.
            /// </returns>
            internal static EffectDraftWoundBeforeAuthority Retain(EffectAcceptedDraft owner,
                EffectDraftWoundSelection selection)
            {
                if (!selection.IsCurrentFor(owner) || owner._busy != 1 || owner._faulted || owner._completed ||
                    owner._disposed || !owner._initializationAttempted || owner._currentWoundState == null ||
                    !ReferenceEquals(owner._capturingWoundBefore, selection))
                    throw new InvalidOperationException("Only the active owning draft can retain its wound-before state.");
                var data = new WoundOperationBeforeData(owner._currentWoundState.WoundCarriers,
                    owner._currentWoundState.WoundIdentity, owner._currentWoundState.WoundHistory,
                    owner.workspace.ToInput(), owner.identityRoot.ReadSnapshot());
                var result = new EffectDraftWoundBeforeAuthority(owner, selection, data);
                owner._woundBeforeStates.Add(selection, result);
                return result;
            }
        }
    }
}
