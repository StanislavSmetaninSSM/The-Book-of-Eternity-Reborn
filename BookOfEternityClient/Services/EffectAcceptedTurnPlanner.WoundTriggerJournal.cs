using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        private long _dependencyCutVersion;
        private readonly Dictionary<(EffectEventBoundaryStamp, EffectActivationCandidateIdentity), WoundTriggerReceipt>
            _woundTriggerJournal = new();
        private readonly Dictionary<string, WoundReactionReceipt> _woundReactionJournal =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Applies a fully preflighted N, R then U cut through the draft's real writers and retains their exact receipts.
        /// </summary>
        /// <param name="selection">
        /// Registered selection whose owned prefix supplied every pending activation.
        /// </param>
        /// <param name="pending">
        /// Ordered related activations, none of which has a completed journal entry.
        /// </param>
        /// <param name="reactions">
        /// Preflighted related replacement reactions, none of which has a completed journal entry.
        /// </param>
        /// <param name="failures">
        /// Receives mutation and receipt-agreement failures; any failed write faults the private draft.
        /// </param>
        /// <returns>
        /// True after all selected writes are retained, or false after faulting an unsuccessful mutation.
        /// </returns>
        private bool ApplyWoundDependencyCut(
            EffectDraftWoundSelection selection,
            IReadOnlyList<AcceptedEffectBoundaryActivation> pending,
            WoundReactionCutPreparation reactions, List<ValidationIssue> failures)
        {
            if (pending.Count == 0 && reactions.Releases.Count == 0)
                return true;
            var preReactionEffects = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var accepted in pending.Where(value => !value.Activation.Stamp.ConsumesUse))
                if (!ApplyTrigger(accepted))
                    return false;
            if (reactions.Releases.Count != 0)
            {
                var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
                failures.AddRange(catalog.Issues);
                foreach (var occurrence in catalog.Occurrences)
                    if (!preReactionEffects.TryAdd(occurrence.EffectId, occurrence.Effect.DeepClone().AsObject()))
                        return Fail("one exact pre-reaction effect per identity", occurrence.EffectId);
                if (failures.Count != 0)
                    return Fail("one valid pre-reaction carrier catalog", "invalid catalog");
            }
            if (!ApplyWoundReactionCut(reactions, failures))
                return false;
            foreach (var accepted in pending.Where(value => value.Activation.Stamp.ConsumesUse))
                if (!ApplyTrigger(accepted))
                    return false;
            var before = _currentWoundState!;
            _currentWoundState = new(before.WoundCarriers, before.WoundIdentity, before.WoundHistory,
                workspace.ToInput(), identityRoot.ReadSnapshot());
            _dependencyCutVersion = checked(_dependencyCutVersion + 1);
            return true;

            bool ApplyTrigger(AcceptedEffectBoundaryActivation accepted)
            {
                var evidence = selection.Prefix.AppliedComponentEvidence
                    .Where(value => value.Boundary == accepted.Boundary && value.Activation == accepted.Activation.Stamp.Identity)
                    .ToArray();
                var execution = ProjectAcceptedTrigger(accepted, evidence);
                var carrierStart = workspace.EditCount;
                var writeStart = identityRoot.WriteCount;
                var allocationStart = identityRoot.AllocationCount;
                if (processedEventRefs.Contains(execution.EventRef))
                    return Fail("an unapplied exact trigger event", execution.EventRef);
                var consumes = accepted.Activation.Stamp.ConsumesUse;
                var terminal = accepted.Activation.EffectTerminal;
                var anchoredBeforeReplacement = false;
                if (consumes)
                {
                    var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
                    failures.AddRange(catalog.Issues);
                    if (!catalog.TryResolveOne(execution.EffectId, out var occurrence))
                    {
                        if (reactions.Releases.Count == 0 ||
                            !TryApplyConsumingTriggerEvidenceBeforeReplacement(execution, turn, preReactionEffects,
                                reactions.Releases, identityRoot, identityFactory, transitionIds,
                                processedEventRefs, failures))
                            return Fail("the exact accepted uses-before budget", execution.EffectId);
                        anchoredBeforeReplacement = failures.Count == 0;
                    }
                    else
                    {
                        if (occurrence.Effect["lifetime"]?["remainingUses"]?.GetValue<int>() != execution.RemainingUseBudget)
                            return Fail("the exact accepted uses-before budget", execution.EffectId);
                        ApplyLifecycleReduction(occurrence,
                            new EffectLifecycleEvent(execution.EventRef, turn, execution.EventKind, execution.TriggerId),
                            workspace, identityRoot, identityFactory, transitionIds, activeEffects, processedEventRefs, failures);
                    }
                }
                else
                    ApplyNonConsumingTriggerEvidence(execution, turn, workspace, identityRoot, identityFactory,
                        transitionIds, activeEffects, processedEventRefs, failures);
                if (failures.Count != 0)
                {
                    _faulted = true;
                    return false;
                }
                var edits = workspace.ReadEditsFrom(carrierStart);
                var writes = identityRoot.ReadWritesFrom(writeStart);
                var allocations = identityRoot.ReadAllocationsFrom(allocationStart);
                identityState = ParseIdentity(identityRoot.ReadSnapshot());
                failures.AddRange(identityState.Issues);
                if (failures.Count != 0 || identityState.State == null ||
                    !identityState.State.TryGetEntry(execution.EffectId, out var entry))
                    return Fail("one valid identity after the trigger cut", execution.EffectId);
                if (anchoredBeforeReplacement)
                {
                    if (!ValidateAnchoredReplacement())
                        return false;
                }
                else if (edits.Count != 1 || edits[0].EffectId != execution.EffectId ||
                         edits[0].BeforeJson == null || (edits[0].AfterJson == null) != terminal ||
                         writes.Count != 1 || writes[0].Kind != EffectIdentityWriteKind.AppendTransition ||
                         writes[0].EffectId != execution.EffectId || allocations.Count != 1 ||
                         allocations[0].Kind != EffectIdentityAllocationKind.Transition ||
                         entry.Transitions.Last().TransitionId != allocations[0].Identity ||
                         entry.Transitions.Last().EventRef != execution.EventRef || entry.Transitions.Last().Kind !=
                         (terminal ? "expire" : consumes ? "consume" : "trigger") ||
                         terminal && (entry.State != "expired" || !consumes || accepted.Activation.UsesAfter != 0 ||
                             EffectCarrierCatalog.Build(workspace.ToInput()).Occurrences.Any(value => value.EffectId == execution.EffectId)) ||
                         consumes && !terminal && (entry.State != "active" ||
                             edits[0].ReadAfter()?["lifetime"]?["remainingUses"]?.GetValue<int>() != accepted.Activation.UsesAfter) ||
                         !JsonNode.DeepEquals(entry.Transitions.Last().Raw, JsonNode.Parse(writes[0].PayloadJson)))
                    return Fail("one actual trigger edit, append and allocation", execution.EffectId);
                var receipt = new WoundTriggerReceipt(accepted, evidence,
                    anchoredBeforeReplacement ? null : edits[0], writes[0], allocations[0], entry);
                _woundTriggerJournal.Add((accepted.Boundary, accepted.Activation.Stamp.Identity), receipt);
                return true;

                bool ValidateAnchoredReplacement()
                {
                    if (!anchoredBeforeReplacement || edits.Count != 0 || writes.Count != 1 ||
                        writes[0].Kind != EffectIdentityWriteKind.InsertBeforeTransition ||
                        writes[0].EffectId != execution.EffectId || allocations.Count != 1 ||
                        allocations[0].Kind != EffectIdentityAllocationKind.Transition || entry.State != "replaced" ||
                        entry.Transitions.Count(value => JsonNode.DeepEquals(value.Raw,
                            JsonNode.Parse(writes[0].PayloadJson))) != 1 ||
                        !_woundReactionJournal.Values.Any(value => value.AuthenticatesReplacement(entry)))
                        return Fail("one anchored consume before the exact reaction replacement", execution.EffectId);
                    return true;
                }
            }

            bool Fail(string expected, string actual)
            {
                _faulted = true;
                Add(failures, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch", expected, actual);
                return false;
            }
        }

        /// <summary>
        /// Applies preflighted source-owned replacement reactions and journals the exact application and agreement writers.
        /// </summary>
        /// <param name="preparation">
        /// Related releases and immutable replacement plans computed from the operation-before image.
        /// </param>
        /// <param name="failures">
        /// Receives writer and replacement-agreement failures.
        /// </param>
        /// <returns>
        /// <see langword="true"/> after every reaction has one authenticated application receipt; otherwise, <see langword="false"/>.
        /// </returns>
        private bool ApplyWoundReactionCut(WoundReactionCutPreparation preparation, List<ValidationIssue> failures)
        {
            if (preparation.Releases.Count == 0)
                return true;
            var applicationStart = workspace.ApplicationCount;
            var carrierStart = workspace.EditCount;
            var writeStart = identityRoot.WriteCount;
            var allocationStart = identityRoot.AllocationCount;
            var agreementStart = identityRoot.ReplacementAgreementCount;
            var results = new Dictionary<string, ReactionApplicationResult>(StringComparer.Ordinal);
            foreach (var released in preparation.Releases)
            {
                ApplyReactionExecution(released.Reaction, released.Reaction.Target.Realm, eventInput,
                    workspace, identityRoot, identityFactory, effectIds, transitionIds, activeEffects,
                    usedSources, usedTargets, processedEventRefs, failures, preparation.Sources ?? plan.SourceAuthority,
                    reactionSourceBindings, preparation.Plans, results, plan.SkillScopeAuthority);
                if (failures.Count != 0)
                    return Fail("one successful source-owned replacement application", released.Reaction.EventRef);
            }
            ValidateReleasedReplacementAuthority(preparation.Releases, preparation.Plans, results,
                identityRoot, failures);
            if (failures.Count != 0)
                return Fail("one exact replacement agreement for every source-owned reaction", "invalid agreement");
            var applications = workspace.ReadApplicationsFrom(applicationStart);
            var edits = workspace.ReadEditsFrom(carrierStart);
            var writes = identityRoot.ReadWritesFrom(writeStart);
            var allocations = identityRoot.ReadAllocationsFrom(allocationStart);
            var agreements = identityRoot.ReadReplacementAgreementsFrom(agreementStart);
            if (applications.Count != preparation.Releases.Count ||
                agreements.Count != preparation.Releases.Count || edits.Count == 0 || writes.Count == 0 || allocations.Count == 0)
                return Fail("one bounded real writer range for every replacement reaction", applications.Count.ToString());
            var staged = new List<(string EventRef, WoundReactionReceipt Receipt)>();
            foreach (var released in preparation.Releases)
            {
                var reaction = released.Reaction;
                var application = applications.SingleOrDefault(value => value.EventRef == reaction.EventRef);
                var agreement = agreements.SingleOrDefault(value => value.EventRef == reaction.EventRef);
                if (application == null || agreement == null || application.ProvenanceKind != EffectDraftApplicationProvenanceKind.Reaction ||
                    application.ProducerEffectId != reaction.EffectId || application.ResultIdentity == null ||
                    application.ReplacedIdentity != reaction.ReplacementTarget || agreement.Result != application.ResultIdentity ||
                    agreement.Replaced != application.ReplacedIdentity || !processedEventRefs.Contains(reaction.EventRef) ||
                    _woundReactionJournal.ContainsKey(reaction.EventRef))
                    return Fail("one exact application, result and replacement agreement", reaction.EventRef);
                staged.Add((reaction.EventRef, new WoundReactionReceipt(released, application, agreement)));
            }
            foreach (var item in staged)
                _woundReactionJournal.Add(item.EventRef, item.Receipt);
            identityState = ParseIdentity(identityRoot.ReadSnapshot());
            failures.AddRange(identityState.Issues);
            return failures.Count == 0 || Fail("one valid identity image after the reaction cut", "invalid identity");

            bool Fail(string expected, string actual)
            {
                _faulted = true;
                Add(failures, "acceptedTurn.wounds", "spiritual_wound_cut_agreement_mismatch", expected, actual);
                return false;
            }
        }

        /// <summary>
        /// Projects the same accepted activation and applied components used by ordinary completion into a writer input.
        /// </summary>
        /// <param name="accepted">
        /// Actual accepted boundary activation, including its immutable uses-before stamp.
        /// </param>
        /// <param name="evidence">
        /// Applied components belonging to this exact activation; empty for a trigger with no resource output.
        /// </param>
        /// <returns>
        /// Trigger evidence for the existing mutation helpers, without replaying resource execution.
        /// </returns>
        private static EffectResourceTriggerExecution ProjectAcceptedTrigger(
            AcceptedEffectBoundaryActivation accepted, IEnumerable<AppliedEffectComponentEvidence> evidence)
        {
            var componentMap = evidence.ToDictionary(value => value.Mutation, value => value.ComponentId);
            var stamp = accepted.Activation.Stamp;
            return new(stamp.Identity.EffectId, stamp.Identity.TriggerId, stamp.Identity.EventKind, stamp.Identity.EventRef,
                componentMap.Keys.ToArray(), stamp.UsesBefore,
                componentMap.Values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                stamp.Identity.TriggerEventRef, componentMap);
        }

        /// <summary>
        /// Retains a fully preflighted related reaction subset and its immutable application plans.
        /// </summary>
        private sealed class WoundReactionCutPreparation
        {
            private static readonly WoundReactionCutPreparation EmptyValue = new(
                Array.Empty<ReleasedEffectReaction>(),
                new Dictionary<string, ReactionApplicationPlan>(StringComparer.Ordinal), null);
            private readonly ReleasedEffectReaction[] _releases;
            private readonly Dictionary<string, ReactionApplicationPlan> _plans;

            /// <summary>
            /// Copies the exact releases and plans that were validated against one operation-before image.
            /// </summary>
            /// <param name="releases">
            /// Related source-owned releases in mechanics order.
            /// </param>
            /// <param name="plans">
            /// One replacement plan keyed by each release event reference.
            /// </param>
            /// <param name="sources">
            /// Current owner-installed source catalog used to construct the plans, or null for an empty preparation.
            /// </param>
            internal WoundReactionCutPreparation(IEnumerable<ReleasedEffectReaction> releases,
                IReadOnlyDictionary<string, ReactionApplicationPlan> plans, EffectSourceAuthority? sources)
            {
                _releases = releases.ToArray();
                _plans = new Dictionary<string, ReactionApplicationPlan>(plans, StringComparer.Ordinal);
                Sources = sources;
            }

            /// <summary>
            /// Gets an immutable empty preparation for creation and reaction-free worsening.
            /// </summary>
            internal static WoundReactionCutPreparation Empty => EmptyValue;
            /// <summary>
            /// Gets related releases in their accepted mechanics order.
            /// </summary>
            internal IReadOnlyList<ReleasedEffectReaction> Releases => Array.AsReadOnly(_releases);
            /// <summary>
            /// Gets the exact preflighted plan for each related reaction event.
            /// </summary>
            internal IReadOnlyDictionary<string, ReactionApplicationPlan> Plans => _plans;
            /// <summary>
            /// Gets the exact source catalog used by preflight, or null for an empty preparation.
            /// </summary>
            internal EffectSourceAuthority? Sources { get; }
        }

        /// <summary>
        /// Retains one real source-owned reaction application and its replacement agreement for cumulative-prefix replay checks.
        /// </summary>
        private sealed class WoundReactionReceipt
        {
            private readonly ReleasedEffectReaction _released;
            private readonly EffectDraftApplicationReceipt _application;
            private readonly EffectIdentityReplacementAgreement _agreement;

            /// <summary>
            /// Retains the exact writer receipts after the local cut validates their common reaction event.
            /// </summary>
            /// <param name="released">
            /// Original authoritative release from the owned prefix.
            /// </param>
            /// <param name="application">
            /// Actual application receipt created by the shared effect writer.
            /// </param>
            /// <param name="agreement">
            /// Actual replacement agreement retained by the identity owner.
            /// </param>
            internal WoundReactionReceipt(ReleasedEffectReaction released,
                EffectDraftApplicationReceipt application, EffectIdentityReplacementAgreement agreement)
            {
                _released = released;
                _application = application;
                _agreement = agreement;
            }

            /// <summary>
            /// Authenticates a repeated cumulative release against the exact retained writer history.
            /// </summary>
            /// <param name="released">
            /// Release occupying the same reaction event in a later cumulative prefix.
            /// </param>
            /// <param name="identities">
            /// Current draft identity state, including identities retired by later wound insertion.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only when the release, application transitions and replacement anchors remain exact.
            /// </returns>
            internal bool Matches(ReleasedEffectReaction released, EffectIdentityState identities)
            {
                if (released.Boundary != _released.Boundary || released.Activation != _released.Activation ||
                    released.Stage != _released.Stage || released.MechanicsOrdinal != _released.MechanicsOrdinal ||
                    !string.Equals(
                        AcceptedMechanicsPlanner.CreateReactionCandidateOutputFingerprint(released.Reaction),
                        AcceptedMechanicsPlanner.CreateReactionCandidateOutputFingerprint(_released.Reaction),
                        StringComparison.Ordinal) ||
                    _application.EventRef != released.Reaction.EventRef ||
                    _application.ProvenanceKind != EffectDraftApplicationProvenanceKind.Reaction ||
                    _application.ProducerEffectId != released.Reaction.EffectId ||
                    _application.ResultIdentity != _agreement.Result ||
                    _application.ReplacedIdentity != _agreement.Replaced)
                    return false;
                foreach (var write in _application.IdentityWrites)
                {
                    if (!identities.TryGetEntry(write.EffectId, out var entry))
                        return false;
                    if (write.Kind == EffectIdentityWriteKind.CreateEntry)
                    {
                        var created = JsonNode.Parse(write.PayloadJson)?.AsObject();
                        if (created == null || ReadStableHeader(created) != ReadStableHeader(entry.Raw))
                            return false;
                    }
                    else if (entry.Transitions.Count(value =>
                                 JsonNode.DeepEquals(value.Raw, JsonNode.Parse(write.PayloadJson))) != 1)
                        return false;
                }
                foreach (var anchor in _agreement.Anchors)
                    if (!identities.TryGetEntry(anchor.EffectId, out var entry) ||
                        entry.Transitions.Count(value => JsonNode.DeepEquals(value.Raw,
                            JsonNode.Parse(anchor.TransitionJson))) != 1)
                        return false;
                return identities.TryGetEntry(_agreement.Result.EffectId, out _);
            }

            /// <summary>
            /// Authenticates the replaced identity retained by this reaction's exact application and agreement.
            /// </summary>
            /// <param name="identity">
            /// Current identity whose replacement state and immutable history must match the real writer receipt.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for this reaction's unchanged replaced target and agreement anchors.
            /// </returns>
            internal bool AuthenticatesReplacement(EffectIdentityEntry identity)
            {
                if (_agreement.Replaced is not { } replaced || replaced.EffectId != identity.EffectId ||
                    identity.State != "replaced" || _application.ReplacedIdentity != replaced)
                    return false;
                foreach (var anchor in _agreement.Anchors)
                    if (anchor.EffectId == identity.EffectId && identity.Transitions.Count(value =>
                            JsonNode.DeepEquals(value.Raw, JsonNode.Parse(anchor.TransitionJson))) != 1)
                        return false;
                return identity.Transitions.Any(value =>
                    value.Kind == "replace" && value.EventRef == _released.Reaction.EventRef);
            }

            /// <summary>
            /// Canonicalizes immutable identity fields while allowing later state and transition changes.
            /// </summary>
            /// <param name="entry">
            /// Created or current identity entry.
            /// </param>
            /// <returns>
            /// Canonical identity header without mutable state and transitions.
            /// </returns>
            private static string ReadStableHeader(JsonObject entry)
            {
                var header = entry.DeepClone().AsObject();
                header.Remove("state");
                header.Remove("transitions");
                return WoundAcceptedTurnFingerprintWriter.CanonicalJson(header)!;
            }
        }

        /// <summary>
        /// Retains one real trigger, consume or last-use expiry and its accepted evidence independently of later source generations.
        /// </summary>
        private sealed class WoundTriggerReceipt
        {
            private readonly AcceptedEffectBoundaryActivation _accepted;
            private readonly AppliedEffectComponentEvidence[] _evidence;
            private readonly string _identityHeader;
            private readonly EffectIdentitySourceGroup _group;
            private readonly string? _terminalIdentity;
            /// <summary>
            /// Retains the actual source-generation carrier edit, or null for a consume anchored before a completed replacement.
            /// </summary>
            internal readonly EffectDraftCarrierEdit? CarrierEdit;
            /// <summary>
            /// Retains the exact appended or anchored trigger, consume or expire transition and its writer ordinal.
            /// </summary>
            internal readonly EffectIdentityWriteReceipt IdentityWrite;
            /// <summary>
            /// Retains the allocation associated with the appended or anchored transition.
            /// </summary>
            internal readonly EffectIdentityAllocationReceipt Allocation;

            /// <summary>
            /// Copies successful writer evidence after the owning draft verifies exact mutation counts and history.
            /// </summary>
            /// <param name="accepted">
            /// Original owned activation, whose candidate and boundary must remain exact in cumulative prefixes.
            /// </param>
            /// <param name="evidence">
            /// Actual component evidence for that boundary and activation.
            /// </param>
            /// <param name="edit">
            /// Actual carrier before and after snapshots, or null when the real writer inserted a consume before replacement.
            /// </param>
            /// <param name="write">
            /// Actual appended or anchored trigger, consume or expire transition with its immutable payload.
            /// </param>
            /// <param name="allocation">
            /// Actual transition allocation associated with the append or anchored insertion.
            /// </param>
            /// <param name="entry">
            /// Parsed identity after the append or anchored insertion, supplying immutable identity headers and wound source group.
            /// </param>
            internal WoundTriggerReceipt(AcceptedEffectBoundaryActivation accepted, AppliedEffectComponentEvidence[] evidence,
                EffectDraftCarrierEdit? edit, EffectIdentityWriteReceipt write, EffectIdentityAllocationReceipt allocation,
                EffectIdentityEntry entry)
            {
                _accepted = accepted;
                _evidence = evidence.ToArray();
                CarrierEdit = edit;
                IdentityWrite = write;
                Allocation = allocation;
                _identityHeader = ReadHeader(entry);
                _group = new(entry.Realm, entry.Source["kind"]!.GetValue<string>(), entry.Source["sourceId"]!.GetValue<string>());
                _terminalIdentity = accepted.Activation.EffectTerminal && entry.State == "expired"
                    ? WoundAcceptedTurnFingerprintWriter.CanonicalJson(entry.Raw) : null;
            }

            /// <summary>
            /// Authenticates repeated cumulative evidence and its retained history anchor before resolving current carriers.
            /// </summary>
            /// <param name="accepted">
            /// Activation occupying the same journal key in the later owned prefix.
            /// </param>
            /// <param name="evidence">
            /// Complete later component evidence; only this exact boundary and activation are compared.
            /// </param>
            /// <param name="identities">
            /// Current parsed index, including terminal identities removed from carriers.
            /// </param>
            /// <param name="retirement">
            /// Insertion-chain proof for later wound retirement; a last-use receipt also retains its own exact terminal writer image.
            /// </param>
            /// <param name="reactions">
            /// Real reaction receipts that may authenticate this trigger's target as exactly replaced.
            /// </param>
            /// <returns>
            /// True only for unchanged accepted evidence and an exact surviving journal anchor.
            /// </returns>
            internal bool Matches(AcceptedEffectBoundaryActivation accepted,
                IReadOnlyList<AppliedEffectComponentEvidence> evidence, EffectIdentityState identities,
                EffectDraftWoundInsertion? retirement, IReadOnlyDictionary<string, WoundReactionReceipt> reactions)
            {
                if (accepted != _accepted || !_evidence.SequenceEqual(evidence.Where(value =>
                        value.Boundary == accepted.Boundary && value.Activation == accepted.Activation.Stamp.Identity)) ||
                    !identities.TryGetEntry(IdentityWrite.EffectId, out var entry) || ReadHeader(entry) != _identityHeader ||
                    entry.Transitions.Count(value => value.TransitionId == Allocation.Identity &&
                        JsonNode.DeepEquals(value.Raw, JsonNode.Parse(IdentityWrite.PayloadJson))) != 1)
                    return false;
                return entry.State is "active" or "suspended" ||
                    _terminalIdentity != null && WoundAcceptedTurnFingerprintWriter.CanonicalJson(entry.Raw) == _terminalIdentity ||
                    entry.State == "replaced" && reactions.Values.Any(value => value.AuthenticatesReplacement(entry)) ||
                    retirement?.AuthenticatesRetiredIdentity(_group, entry, out _) == true;
            }

            /// <summary>
            /// Reads a terminal image only when it still exactly matches this draft's actual last-use writer receipt.
            /// </summary>
            /// <param name="group">
            /// Wound source group whose superseded generation is being imported into retirement history.
            /// </param>
            /// <param name="identities">
            /// Current parsed identity index after the associated wound insertion.
            /// </param>
            /// <param name="entry">
            /// Receives the exact terminal entry on success; otherwise null.
            /// </param>
            /// <param name="known">
            /// True for a terminal receipt in the requested group, including when its current image is missing or changed.
            /// </param>
            /// <returns>
            /// True only for an unchanged owned terminal image; false must not silently discard a known receipt.
            /// </returns>
            internal bool TryReadTerminalIdentity(EffectIdentitySourceGroup group, EffectIdentityState identities,
                out EffectIdentityEntry? entry, out bool known)
            {
                entry = null;
                known = _group == group && _terminalIdentity != null;
                if (!known || !identities.TryGetEntry(IdentityWrite.EffectId, out var current) ||
                    current.State != "expired" || WoundAcceptedTurnFingerprintWriter.CanonicalJson(current.Raw) != _terminalIdentity)
                    return false;
                entry = current;
                return true;
            }

            /// <summary>
            /// Freezes identity headers while permitting legitimate later state and transition changes.
            /// </summary>
            /// <param name="entry">
            /// Parsed identity whose owner, source and complete stack coordinate must stay unchanged.
            /// </param>
            /// <returns>
            /// Canonical header JSON excluding only mutable state and transition history.
            /// </returns>
            private static string ReadHeader(EffectIdentityEntry entry)
            {
                var header = entry.Raw.DeepClone().AsObject();
                header.Remove("state");
                header.Remove("transitions");
                return WoundAcceptedTurnFingerprintWriter.CanonicalJson(header)!;
            }
        }
    }
}
