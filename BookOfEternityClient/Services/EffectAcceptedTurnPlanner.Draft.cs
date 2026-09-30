using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    // Owns the ordinary completion's real mutable effect state and phase execution.
    // Begin captures the immutable base reference without reading/allocating its
    // materialized state, preserving the old transcript-rejection ordering.
    // No source admission, insertion or resumed-prefix API is exposed by Unit A.
    internal sealed partial class EffectAcceptedDraft : IDisposable
    {
        private readonly EffectAcceptedTurnPlan plan;
        private EffectIdentityFactory identityFactory;
        private readonly List<ValidationIssue> issues = new();
        private JsonObject eventInput = null!;
        private int turn;
        private CarrierWorkspace workspace = null!;
        private EffectIdentityHistoryOwner identityRoot = null!;
        private EffectIdentityParseResult identityState = null!;
        private HashSet<string> processedEventRefs = null!;
        private List<string> transitionIds = null!;
        private List<string> effectIds = null!;
        private List<JsonObject> activeEffects = null!;
        private List<EffectSourceAuthorityEntry> usedSources = null!;
        private List<EffectTargetKey> usedTargets = null!;
        private HashSet<string> executionKeys = null!;
        private List<EffectResourceTriggerExecution> acceptedExecutions = null!;
        private List<EffectReactionExecution> deferredReactions = null!;
        private HashSet<string> reactionEventRefs = null!;
        private Dictionary<EffectSourceKey, EffectSourceAuthorityEntry> reactionSourceBindings = null!;
        private ReleasedEffectReaction[] authoritativeReleasedReactions = null!;
        private Dictionary<EffectReactionExpansionKey, EffectReactionExpansionUsage> reactionExpansionUsage = null!;
        private EffectCarrierCatalog preMutationCatalog = null!;
        private WoundReactionLineageAuthority woundLineageAuthority = null!;
        private Dictionary<string, ReactionApplicationPlan> reactionApplicationPlans = null!;
        private Dictionary<string, ReactionApplicationResult> reactionApplicationResults = null!;
        private Dictionary<string, JsonObject> preReactionEffects = null!;
        private readonly List<EffectDraftPhaseReceipt> _phases = new();
        private EffectDraftPhase? _openPhase;
        private int _carrierStart;
        private int _applicationStart;
        private int _writeStart;
        private int _allocationStart;
        private int _agreementStart;
        private int _sourceStart;
        private int _targetStart;
        private HashSet<string> _processedBefore = null!;
        private int _busy;
        private bool _initializationAttempted;
        private bool _completed;
        private bool _faulted;
        private bool _disposed;
        private EffectAcceptedTurnPlan? _completedPlan;
        private AcceptedEffectBoundaryTranscript? _completionTranscript;

        private EffectAcceptedDraft(EffectAcceptedTurnPlan acceptedBase, EffectIdentityFactory factory)
        {
            plan = acceptedBase ?? throw new ArgumentNullException(nameof(acceptedBase));
            identityFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        internal static EffectAcceptedDraft Begin(
            EffectAcceptedTurnPlan acceptedBase, EffectIdentityFactory identityFactory) =>
            new(acceptedBase, identityFactory);

        internal IReadOnlyList<EffectDraftPhaseReceipt> Phases => Array.AsReadOnly(_phases.ToArray());

        /// <summary>
        /// Reads the accepted base effect plan's semantic payload fingerprint without completing the draft.
        /// </summary>
        /// <returns>
        /// The plan fingerprint that anchors this draft's later closed effect prefixes.
        /// </returns>
        internal string ReadBasePlanPayloadFingerprint()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EffectAcceptedDraft));
            return WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        }

        internal EffectAcceptedTurnPlanningResult Complete(AcceptedEffectBoundaryTranscript transcript)
            => CompleteOwned(transcript, routing: null);

        /// <summary>
        /// Completes this draft from the retained resource owner's final transcript and installed wound routing epoch.
        /// </summary>
        /// <param name="transcript">
        /// Complete transcript produced by the resource execution that owns this draft's accepted base.
        /// </param>
        /// <param name="routing">
        /// Exact base routing owner holding the latest inserted wound generation.
        /// </param>
        /// <returns>
        /// A completed unpublished effect plan, or validation diagnostics when the transcript or retained receipts disagree.
        /// </returns>
        internal EffectAcceptedTurnPlanningResult CompleteWithWoundRouting(
            AcceptedEffectBoundaryTranscript transcript,
            BaseResourceRouting routing)
        {
            ArgumentNullException.ThrowIfNull(routing);
            return CompleteOwned(transcript, routing);
        }

        /// <summary>
        /// Serializes one completion attempt and applies either the original authority or the installed wound routing epoch.
        /// </summary>
        /// <param name="transcript">
        /// Complete retained resource transcript for this draft's accepted base plan.
        /// </param>
        /// <param name="routing">
        /// Installed wound routing owner when the draft contains wound insertions; otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// A completed unpublished plan, or validation diagnostics that permanently fault this completion attempt.
        /// </returns>
        private EffectAcceptedTurnPlanningResult CompleteOwned(
            AcceptedEffectBoundaryTranscript transcript,
            BaseResourceRouting? routing)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Effect draft completion cannot be re-entered.");
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_completed || _faulted)
                    throw new InvalidOperationException("The effect draft is no longer writable.");
                try
                {
                    var result = CompleteCore(transcript, routing);
                    _completed = result.Success;
                    _faulted = !result.Success;
                    _completedPlan = result.Success ? result.Plan : null;
                    _completionTranscript = result.Success ? transcript : null;
                    return result;
                }
                catch
                {
                    _faulted = true;
                    throw;
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref _busy, 0);
            }
        }

        /// <summary>
        /// Checks whether this draft owns the exact accepted base plan.
        /// </summary>
        /// <param name="acceptedBase">
        /// Candidate accepted base plan.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only when this live draft was created for
        /// <paramref name="acceptedBase"/>; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool OwnsBase(EffectAcceptedTurnPlan acceptedBase)
        {
            ArgumentNullException.ThrowIfNull(acceptedBase);
            return !_disposed && ReferenceEquals(plan, acceptedBase);
        }

        /// <summary>
        /// Checks whether this exact draft issued a completed plan through the expected routing epoch
        /// from the exact retained resource transcript.
        /// </summary>
        /// <param name="completedPlan">
        /// Candidate completed plan, which must be the exact object returned by this draft.
        /// </param>
        /// <param name="routing">
        /// Exact installed wound routing for an inserted draft, or <see langword="null"/> for an ordinary draft.
        /// </param>
        /// <param name="transcript">
        /// Exact resource transcript supplied to this draft's successful completion.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for this draft's successful completion and matching routing ownership;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        internal bool OwnsCompletion(
            EffectAcceptedTurnPlan completedPlan,
            BaseResourceRouting? routing,
            AcceptedEffectBoundaryTranscript transcript)
        {
            ArgumentNullException.ThrowIfNull(completedPlan);
            ArgumentNullException.ThrowIfNull(transcript);
            if (_disposed || _faulted || !_completed ||
                !ReferenceEquals(_completedPlan, completedPlan) ||
                !ReferenceEquals(_completionTranscript, transcript))
                return false;
            if (_woundInsertions.Count == 0)
                return routing is null;
            return routing is not null && _lastWoundInsertion is not null &&
                routing.HasInstalled(_lastWoundInsertion);
        }

        internal JsonObject ReadIdentityIndex()
        {
            EnsureReadable();
            return identityRoot.ReadSnapshot();
        }

        internal EffectCarrierCatalogInput ReadCarriers()
        {
            EnsureReadable();
            var input = workspace.ToInput();
            return new EffectCarrierCatalogInput(
                input.PlayerEffects?.DeepClone().AsObject(),
                input.NpcEffects?.DeepClone().AsObject(),
                input.EnemyCombatants?.DeepClone().AsObject(),
                input.AllyCombatants?.DeepClone().AsObject(),
                input.AfterlifeProfiles?.DeepClone().AsObject(),
                input.SpiritualConflict?.DeepClone().AsObject());
        }

        private void EnsureReadable()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_completed || _faulted || System.Threading.Volatile.Read(ref _busy) != 0)
                throw new InvalidOperationException("Only a completed, unfaulted effect draft exposes its state image.");
        }

        private void BeginPhase(EffectDraftPhase phase)
        {
            if (_openPhase != null || (int)phase != _phases.Count)
                throw new InvalidOperationException("Effect draft phases must run exactly once in canonical order.");
            _openPhase = phase;
            _carrierStart = workspace.EditCount;
            _applicationStart = workspace.ApplicationCount;
            _writeStart = identityRoot.WriteCount;
            _allocationStart = identityRoot.AllocationCount;
            _agreementStart = identityRoot.ReplacementAgreementCount;
            _sourceStart = usedSources.Count;
            _targetStart = usedTargets.Count;
            _processedBefore = new HashSet<string>(processedEventRefs, StringComparer.Ordinal);
        }

        private void EndPhase()
        {
            if (_openPhase == null || issues.Count != 0)
                throw new InvalidOperationException("A failed or unopened effect phase cannot be recorded as applied.");
            _phases.Add(new EffectDraftPhaseReceipt(
                _openPhase.Value,
                workspace.ReadEditsFrom(_carrierStart),
                workspace.ReadApplicationsFrom(_applicationStart),
                identityRoot.ReadWritesFrom(_writeStart),
                identityRoot.ReadAllocationsFrom(_allocationStart),
                identityRoot.ReadReplacementAgreementsFrom(_agreementStart),
                processedEventRefs.Except(_processedBefore, StringComparer.Ordinal),
                usedSources.Skip(_sourceStart),
                usedTargets.Skip(_targetStart),
                plan.SkillScopeAuthority?.Fingerprint));
            _openPhase = null;
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Active effect draft completion cannot be disposed.");
            try
            {
                if (_disposed)
                    return;
                _disposed = true;
                identityRoot?.Dispose();
            }
            finally
            {
                System.Threading.Volatile.Write(ref _busy, 0);
            }
        }

        /// <summary>
        /// Reconciles the complete transcript with retained insertion, trigger, and reaction receipts before final phases run.
        /// </summary>
        /// <param name="transcript">
        /// Complete authoritative transcript, including operations already materialized by local wound cuts.
        /// </param>
        /// <param name="routing">
        /// Exact installed routing epoch for an inserted draft, or <see langword="null"/> for an ordinary draft.
        /// </param>
        /// <returns>
        /// A completed unpublished plan, or deterministic validation diagnostics without publishing canonical files.
        /// </returns>
        private EffectAcceptedTurnPlanningResult CompleteCore(
            AcceptedEffectBoundaryTranscript transcript,
            BaseResourceRouting? routing)
        {
            if (plan == null || transcript == null || identityFactory == null)
            {
                var missingIssues = new List<ValidationIssue>();
                Add(
                    missingIssues,
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_boundary_transcript_missing",
                    "one accepted plan, validated boundary transcript, and identity factory",
                    "null");
                return Failed(missingIssues);
            }
            if (plan.IsAcceptedBoundaryComplete)
            {
                var repeatedIssues = new List<ValidationIssue>();
                Add(
                    repeatedIssues,
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_boundary_completion_repeated",
                    "one initial effect plan completed exactly once",
                    "already completed");
                return Failed(repeatedIssues);
            }
            var expectedPlanAuthority =
                AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan);
            if (!transcript.IsComplete ||
                transcript.PlanAuthority != expectedPlanAuthority)
            {
                var mismatchIssues = new List<ValidationIssue>();
                Add(
                    mismatchIssues,
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_boundary_transcript_plan_mismatch",
                    "one complete transcript with the exact five-field accepted effect plan authority including skill scope",
                    transcript.IsComplete
                        ? transcript.PlanAuthority?.InputFingerprint ?? "missing"
                        : "pending-frontier");
                return Failed(mismatchIssues);
            }
            EffectSourceAuthority completionSources;
            IReadOnlyList<WoundApplicationRootEffectBinding> completionRoots;
            WoundReactionLineageAuthority? completionLineage;
            if (_woundInsertions.Count == 0)
            {
                if (routing != null)
                {
                    var unexpectedRouting = new List<ValidationIssue>();
                    Add(unexpectedRouting, "acceptedTurn.wounds", "spiritual_wound_routing_owner_mismatch",
                        "no wound routing for a draft without insertions", "routing supplied");
                    return Failed(unexpectedRouting);
                }
                completionSources = plan.SourceAuthority;
                completionRoots = plan.WoundApplicationRootEffectBindings;
                completionLineage = null;
            }
            else
            {
                if (routing == null || _lastWoundInsertion == null ||
                    !routing.HasInstalled(_lastWoundInsertion) ||
                    !routing.TryReadCurrentWoundReactionView(
                        out completionSources,
                        out completionRoots,
                        out completionLineage))
                {
                    var pendingIssues = new List<ValidationIssue>();
                    Add(pendingIssues, "acceptedTurn.wounds", "spiritual_wound_routing_required",
                        "the exact latest installed wound routing epoch", "missing, foreign or stale");
                    return Failed(pendingIssues);
                }
            }

            if (!InitializeState() || identityState.State == null)
                return Failed(issues);
            identityState = ParseIdentity(identityRoot.ReadSnapshot());
            issues.AddRange(identityState.Issues);
            if (identityState.State == null || issues.Count != 0)
                return Failed(issues);
            var completionCarriers = EffectCarrierCatalog.Build(workspace.ToInput());
            issues.AddRange(completionCarriers.Issues);
            if (issues.Count != 0)
                return Failed(issues);
            foreach (var insertion in _woundInsertions.Values)
            {
                if (insertion.ValidatesCompletionState(identityState.State, completionCarriers))
                    continue;
                Add(issues, "acceptedTurn.wounds", "spiritual_wound_insertion_agreement_mismatch",
                    "every retained wound insertion writer receipt and identity-history anchor",
                    insertion.Wound.WoundId);
            }
            if (issues.Count != 0)
                return Failed(issues);

            var pendingExecutions = new List<EffectResourceTriggerExecution>();
            var completeTriggerKeys = new HashSet<
                (EffectEventBoundaryStamp, EffectActivationCandidateIdentity)>();
            foreach (var accepted in transcript.AcceptedActivations
                         .OrderBy(static value => value.Activation.Stamp.ActivationOrdinal))
            {
                var key = (accepted.Boundary, accepted.Activation.Stamp.Identity);
                if (!completeTriggerKeys.Add(key))
                {
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                        "one complete-transcript activation per journal key", accepted.Activation.Stamp.Identity.EventRef);
                    continue;
                }
                if (_woundTriggerJournal.TryGetValue(key, out var receipt))
                {
                    if (!receipt.Matches(accepted, transcript.AppliedComponentEvidence,
                            identityState.State, _lastWoundInsertion, _woundReactionJournal))
                        Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                            "the exact journaled trigger and surviving identity history",
                            accepted.Activation.Stamp.Identity.EventRef);
                    continue;
                }
                if (processedEventRefs.Contains(accepted.Activation.Stamp.Identity.EventRef))
                {
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                        "one retained journal receipt for every trigger already applied by a wound cut",
                        accepted.Activation.Stamp.Identity.EventRef);
                    continue;
                }
                var evidence = transcript.AppliedComponentEvidence.Where(value =>
                    value.Boundary == accepted.Boundary &&
                    value.Activation == accepted.Activation.Stamp.Identity);
                pendingExecutions.Add(ProjectAcceptedTrigger(accepted, evidence));
            }
            foreach (var key in _woundTriggerJournal.Keys)
                if (!completeTriggerKeys.Contains(key))
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                        "every journaled trigger retained in the complete transcript", key.Item2.EventRef);

            var pendingReleases = new List<ReleasedEffectReaction>();
            var completeReactionEvents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var released in transcript.ReleasedReactions.OrderBy(static value => value.MechanicsOrdinal))
            {
                var eventRef = released.Reaction.EventRef;
                if (!completeReactionEvents.Add(eventRef))
                {
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                        "one complete-transcript release per reaction event", eventRef);
                    continue;
                }
                if (_woundReactionJournal.TryGetValue(eventRef, out var receipt))
                {
                    if (!receipt.Matches(released, identityState.State))
                        Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                            "the exact journaled reaction and surviving replacement anchors", eventRef);
                    continue;
                }
                pendingReleases.Add(released);
            }
            foreach (var eventRef in _woundReactionJournal.Keys)
                if (!completeReactionEvents.Contains(eventRef))
                    Add(issues, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch",
                        "every journaled reaction retained in the complete transcript", eventRef);
            if (issues.Count != 0)
                return Failed(issues);

            return RunCore(pendingExecutions, transcript, pendingReleases,
                completionSources, completionRoots, completionLineage);
        }

        /// <summary>
        /// Initializes the retained mutable state once, without advancing any effect phase.
        /// Failed initialization remains a failure and never replaces an existing state owner.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the state is initialized and has no issues; otherwise, <see langword="false"/>.
        /// </returns>
        private bool InitializeState()
        {
            if (_initializationAttempted)
                return identityState?.State != null && issues.Count == 0;
            _initializationAttempted = true;
            eventInput = plan.EventInput;
            if (!TryReadPositiveInt(eventInput["turn"], out turn))
            {
                Add(
                    issues,
                    "eventInput.turn",
                    "effect_plan_event_authority_invalid",
                    "positive accepted turn",
                    Describe(eventInput["turn"]));
                return false;
            }

            workspace = new CarrierWorkspace(plan.ResourceTriggerCarriers);
            identityRoot = new EffectIdentityHistoryOwner(
                plan.IdentityIndexAfterImage, identityFactory,
                plan.AllocatedEffectIds, plan.AllocatedTransitionIds);
            identityFactory = identityRoot.Factory;
            identityState = ParseIdentity(identityRoot.ReadSnapshot());
            issues.AddRange(identityState.Issues);
            if (identityState.State == null || issues.Count != 0)
                return false;
            processedEventRefs = identityState.State.Entries
                .SelectMany(static entry => entry.Transitions)
                .Select(static transition => transition.EventRef)
                .ToHashSet(StringComparer.Ordinal);
            transitionIds = plan.AllocatedTransitionIds.ToList();
            effectIds = plan.AllocatedEffectIds.ToList();
            activeEffects = plan.ActiveEffects
                .Select(static effect => effect.DeepClone().AsObject())
                .ToList();
            usedSources = plan.SourceBindings.ToList();
            usedTargets = plan.Targets.ToList();
            executionKeys = new HashSet<string>(StringComparer.Ordinal);
            acceptedExecutions = new List<EffectResourceTriggerExecution>();
            deferredReactions = new List<EffectReactionExecution>();
            reactionEventRefs = new HashSet<string>(StringComparer.Ordinal);
            reactionSourceBindings = new Dictionary<
                EffectSourceKey,
                EffectSourceAuthorityEntry>();
            return true;
        }

        /// <summary>
        /// Executes only transcript operations not already journaled by wound cuts, then performs the final lifecycle and terminal folds once.
        /// </summary>
        /// <param name="executedTriggers">
        /// Accepted trigger executions that remain pending after journal reconciliation.
        /// </param>
        /// <param name="authoritativeTranscript">
        /// Complete transcript used for release validation, expansion accounting, and the terminal fold.
        /// </param>
        /// <param name="releasedReactionsToApply">
        /// Released reactions that remain pending after journal reconciliation.
        /// </param>
        /// <param name="completionSources">
        /// Current source authority from the original base or latest installed wound routing epoch.
        /// </param>
        /// <param name="completionRoots">
        /// Current application-to-root bindings from the same completion authority epoch.
        /// </param>
        /// <param name="completionLineage">
        /// Prior validated routing lineage used only to retain historical definition epochs while rebuilding current lineage.
        /// </param>
        /// <returns>
        /// A completed unpublished effect plan, or validation diagnostics for the first invalid completion boundary.
        /// </returns>
        private EffectAcceptedTurnPlanningResult RunCore(
            IReadOnlyList<EffectResourceTriggerExecution> executedTriggers,
            AcceptedEffectBoundaryTranscript authoritativeTranscript,
            IReadOnlyList<ReleasedEffectReaction> releasedReactionsToApply,
            EffectSourceAuthority completionSources,
            IReadOnlyList<WoundApplicationRootEffectBinding> completionRoots,
            WoundReactionLineageAuthority? completionLineage)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(executedTriggers);
            ArgumentNullException.ThrowIfNull(identityFactory);
            ArgumentNullException.ThrowIfNull(authoritativeTranscript);
            ArgumentNullException.ThrowIfNull(releasedReactionsToApply);
            ArgumentNullException.ThrowIfNull(completionSources);
            ArgumentNullException.ThrowIfNull(completionRoots);

            if (!InitializeState() || identityState.State is null)
                return Failed(issues);
            authoritativeReleasedReactions = authoritativeTranscript
                .ReleasedReactions
                .OrderBy(static value => value.MechanicsOrdinal)
                .ToArray();
            var acceptedStamps = authoritativeTranscript.AcceptedActivations
                .Select(static accepted => accepted.Activation.Stamp)
                .ToHashSet();
            foreach (var released in authoritativeReleasedReactions)
            {
                var reaction = released.Reaction;
                if (!acceptedStamps.Contains(released.Activation) ||
                    !string.Equals(
                        released.Activation.Identity.EffectId,
                        reaction.EffectId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        released.Activation.Identity.TriggerId,
                        reaction.TriggerId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        released.Activation.Identity.EventKind,
                        reaction.EventKind,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        released.Activation.Identity.TriggerEventRef,
                        reaction.TriggerEventRef,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_activation_authority_invalid",
                        "one exact accepted activation stamp for every released reaction",
                        reaction.EventRef);
                    continue;
                }
                if (!ReactionStageMatchesDependency(
                        released.Stage,
                        reaction.Dependency))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_release_stage_invalid",
                        reaction.Dependency,
                        released.Stage.ToString());
                    continue;
                }
                if (!reactionEventRefs.Add(reaction.EventRef))
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_event_duplicate",
                        "one exact reaction execution per derived event identity",
                        reaction.EventRef);
                    continue;
                }
                deferredReactions.Add(reaction);
            }
            reactionExpansionUsage = authoritativeTranscript.ExpansionUsage
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);
            foreach (var group in deferredReactions.GroupBy(static reaction =>
                         new EffectReactionExpansionKey(
                             reaction.EffectId,
                             reaction.ComponentId)))
            {
                if (!reactionExpansionUsage.TryGetValue(
                        group.Key,
                        out var usage) ||
                    usage.Count != group.Count() ||
                    group.Any(reaction =>
                        reaction.MaxExpansion != usage.Maximum) ||
                    usage.Count > usage.Maximum)
                {
                    Add(
                        issues,
                        "effect.reactions",
                        "effect_reaction_expansion_authority_invalid",
                        "exact released-reaction count and source maximum",
                        group.Key.EffectId + "/" + group.Key.ComponentId);
                }
            }
            if (reactionExpansionUsage.Values.Sum(static usage => usage.Count) !=
                deferredReactions.Count)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_expansion_authority_invalid",
                    "expansion usage accounting for every released reaction exactly once",
                    authoritativeTranscript.ExpansionCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
            }
            var totalReactionExpansion = reactionExpansionUsage.Values.Sum(
                static usage => (long)usage.Count);
            if (totalReactionExpansion > EffectReactionContract.MaximumExpansion)
            {
                Add(
                    issues,
                    "effect.reactions",
                    "effect_reaction_expansion_exceeded",
                    $"at most {EffectReactionContract.MaximumExpansion} reaction operations in one accepted transition",
                    totalReactionExpansion.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
            }
            if (issues.Count != 0)
                return Failed(issues);

            foreach (var execution in executedTriggers)
            {
                var executionKey = string.Join(
                    "\0",
                    execution.EffectId,
                    execution.TriggerId,
                    execution.EventKind,
                    execution.EventRef);
                if (!executionKeys.Add(executionKey))
                {
                    Add(
                        issues,
                        "effect.resourceTriggerExecutions",
                        "effect_resource_trigger_execution_duplicate",
                        "one exact applied trigger activation",
                        executionKey.Replace('\0', '/'));
                    continue;
                }
                acceptedExecutions.Add(execution);
            }
            if (issues.Count != 0)
                return Failed(issues);

            preMutationCatalog = EffectCarrierCatalog.Build(
                workspace.ToInput());
            issues.AddRange(preMutationCatalog.Issues);
            woundLineageAuthority = WoundReactionLineageAuthority.Build(
                completionSources,
                identityState.State,
                preMutationCatalog,
                completionRoots,
                completionLineage,
                _lastWoundInsertion);
            issues.AddRange(woundLineageAuthority.Issues);
            reactionApplicationPlans =
                PrepareReleasedReactionApplicationPlans(
                    releasedReactionsToApply,
                    preMutationCatalog,
                    completionSources,
                    woundLineageAuthority,
                    plan.SkillScopeAuthority,
                    issues);
            if (issues.Count != 0)
                return Failed(issues);
            reactionApplicationResults = new Dictionary<
                string,
                ReactionApplicationResult>(StringComparer.Ordinal);

            BeginPhase(EffectDraftPhase.NonConsumingTrigger);
            foreach (var execution in acceptedExecutions.Where(static execution =>
                         !execution.RemainingUseBudget.HasValue))
            {
                ApplyNonConsumingTriggerEvidence(
                    execution,
                    turn,
                    workspace,
                    identityRoot,
                    identityFactory,
                    transitionIds,
                    activeEffects,
                    processedEventRefs,
                    issues);
            }
            if (issues.Count != 0)
                return Failed(issues);

            EndPhase();
            var preReactionCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
            issues.AddRange(preReactionCatalog.Issues);
            preReactionEffects = new Dictionary<string, JsonObject>(
                StringComparer.Ordinal);
            foreach (var occurrence in preReactionCatalog.Occurrences)
            {
                if (!preReactionEffects.TryAdd(
                        occurrence.EffectId,
                        occurrence.Effect.DeepClone().AsObject()))
                {
                    Add(
                        issues,
                        "effect.resourceTriggerExecutions",
                        "effect_resource_trigger_identity_target_ambiguous",
                        "one exact pre-reaction active effect per identity",
                        occurrence.EffectId);
                }
            }
            if (issues.Count != 0)
                return Failed(issues);

            BeginPhase(EffectDraftPhase.NonterminalReaction);
            var reactionsToApplyBeforeProjection = releasedReactionsToApply
                .Where(static released =>
                    !IsTerminalAvailabilityReaction(released.Reaction))
                .Select(static released => released.Reaction)
                .ToList();
            foreach (var reaction in reactionsToApplyBeforeProjection)
            {
                ApplyReactionExecution(
                    reaction,
                    reaction.Target.Realm,
                    eventInput,
                    workspace,
                    identityRoot,
                    identityFactory,
                    effectIds,
                    transitionIds,
                    activeEffects,
                    usedSources,
                    usedTargets,
                    processedEventRefs,
                    issues,
                    completionSources,
                    reactionSourceBindings,
                    reactionApplicationPlans,
                    reactionApplicationResults,
                    plan.SkillScopeAuthority);
                if (issues.Count != 0)
                    return Failed(issues);
            }
            EndPhase();
            BeginPhase(EffectDraftPhase.ReplacementAgreement);
            ValidateReleasedReplacementAuthority(
                releasedReactionsToApply,
                reactionApplicationPlans,
                reactionApplicationResults,
                identityRoot,
                issues);
            if (issues.Count != 0)
                return Failed(issues);

            EndPhase();
            BeginPhase(EffectDraftPhase.ConsumingTrigger);
            foreach (var execution in acceptedExecutions)
            {
                if (!execution.RemainingUseBudget.HasValue)
                    continue;

                var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
                issues.AddRange(catalog.Issues);
                if (!catalog.TryResolveOne(execution.EffectId, out var occurrence))
                {
                    if (TryApplyConsumingTriggerEvidenceBeforeReplacement(
                            execution,
                            turn,
                            preReactionEffects,
                            releasedReactionsToApply,
                            identityRoot,
                            identityFactory,
                            transitionIds,
                            processedEventRefs,
                            issues))
                    {
                        continue;
                    }
                    Add(
                        issues,
                        "effect.resourceTriggerExecutions",
                        "effect_resource_trigger_lifetime_target_unresolved",
                        "one exact active effect for an applied consuming trigger",
                        execution.EffectId);
                    continue;
                }
                var lifetime = occurrence.Effect["lifetime"] as JsonObject;
                if (lifetime == null ||
                    !string.Equals(
                        lifetime["mode"]?.GetValue<string>(),
                        "uses",
                        StringComparison.Ordinal) ||
                    !TryReadPositiveInt(
                        lifetime["remainingUses"],
                        out var currentRemainingUses) ||
                    currentRemainingUses != execution.RemainingUseBudget.Value)
                {
                    Add(
                        issues,
                        "effect.resourceTriggerExecutions",
                        "effect_activation_replay_budget_mismatch",
                        "the exact accepted uses-before stamp at transcript projection",
                        execution.EffectId + "/" +
                        execution.RemainingUseBudget.Value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) + "/" +
                        Describe(lifetime?["remainingUses"]));
                    continue;
                }
                ApplyLifecycleReduction(
                    occurrence,
                    new EffectLifecycleEvent(
                        execution.EventRef,
                        turn,
                        execution.EventKind,
                        execution.TriggerId),
                    workspace,
                    identityRoot,
                    identityFactory,
                    transitionIds,
                    activeEffects,
                    processedEventRefs,
                    issues);
            }
            if (issues.Count != 0)
                return Failed(issues);

            EndPhase();
            BeginPhase(EffectDraftPhase.FinalLifetime);
            ApplyDueLifecycleEvents(
                eventInput,
                sourceAuthority: null,
                workspace,
                identityRoot,
                identityFactory,
                transitionIds,
                activeEffects,
                processedEventRefs,
                issues,
                boundContinuationsOnly: false);
            if (issues.Count != 0)
                return Failed(issues);

            EndPhase();
            BeginPhase(EffectDraftPhase.FinalTerminal);
            ApplyAcceptedTerminalReactionFold(
                authoritativeReleasedReactions,
                eventInput,
                workspace,
                identityRoot,
                identityFactory,
                effectIds,
                transitionIds,
                activeEffects,
                usedSources,
                usedTargets,
                processedEventRefs,
                issues,
                completionSources,
                reactionSourceBindings);
            if (issues.Count != 0)
                return Failed(issues);
            EndPhase();
            workspace.FinalizeAcceptedSpiritualConflict(
                plan.AcceptedCarrierBaselines.SpiritualConflict);
            ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
            if (issues.Count != 0)
                return Failed(issues);

            var afterImages = plan.CarrierAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            foreach (var pair in workspace.AfterImages)
                afterImages[pair.Key] = pair.Value.DeepClone().AsObject();
            var touchedPaths = plan.TouchedPaths
                .Concat(afterImages.Keys)
                .Append(EffectAcceptedTurnPlan.IdentityIndexPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            var completionSourceBindings = _woundInsertions.Count == 0
                ? usedSources.DistinctBy(static entry => entry.Key).ToArray()
                : usedSources
                    .Where(static entry => !(entry.SameTurn &&
                        string.Equals(entry.Key.Kind, "wound", StringComparison.Ordinal)))
                    .Concat(completionSources.SnapshotSameTurnWoundEntries())
                    .DistinctBy(static entry => entry.Key)
                    .ToArray();

            return new EffectAcceptedTurnPlanningResult(
                new EffectAcceptedTurnPlan(
                    plan.InputFingerprint,
                    plan.CarrierAuthorityFingerprint,
                    _woundInsertions.Count == 0
                        ? plan.SourceAuthorityFingerprint
                        : completionSources.CanonicalFingerprint,
                    plan.TargetAuthorityFingerprint,
                    plan.AllocatedCombatantIds,
                    identityRoot.AllocatedEffectIds,
                    identityRoot.AllocatedTransitionIds,
                    completionSourceBindings
                        .Select(static entry => entry.Key)
                        .Distinct()
                        .ToArray(),
                    usedTargets
                        .Distinct()
                        .ToArray(),
                    completionSourceBindings,
                    Array.Empty<EffectReactionExecution>(),
                    checked((int)totalReactionExpansion),
                    reactionExpansionUsage,
                    activeEffects,
                    workspace.ToInput(),
                    completionSources,
                    plan.TargetAuthority,
                    eventInput,
                    plan.CarrierBeforeImages,
                    afterImages,
                    plan.IdentityIndexBeforeImage,
                    identityRoot.Publish(),
                    touchedPaths,
                    plan.DeletedPaths,
                    acceptedCarrierBaselines: plan.AcceptedCarrierBaselines,
                    acceptedBoundaryCompletionProof:
                        AcceptedBoundaryCompletionAuthority,
                    acceptedBoundaryBasePlanFingerprint:
                        WoundAcceptedTurnFingerprints
                            .ComputeAcceptedEffectPlanPayload(plan),
                    woundApplicationRootEffectBindings:
                        completionRoots,
                    skillScopeAuthority: plan.SkillScopeAuthority),
                Array.Empty<ValidationIssue>());
        }
    }
}
