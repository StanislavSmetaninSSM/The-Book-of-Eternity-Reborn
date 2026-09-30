using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal sealed partial class EffectAcceptedDraft
    {
        private readonly Dictionary<EffectDraftWoundBeforeAuthority, EffectDraftWoundInsertion> _woundInsertions = new();
        private PendingWoundInsertion? _pendingWoundInsertion;
        private long _installedWoundVersion;

        /// <summary>
        /// Gets whether the current insertion version still requires installation in its actual resource routing owner.
        /// </summary>
        internal bool HasPendingWoundIntegration => _installedWoundVersion != _woundVersion;

        /// <summary>
        /// Acknowledges exact resource-owned routing installation without granting ordinary completion authority.
        /// </summary>
        /// <param name="insertion">
        /// Current registered insertion receipt of this draft.
        /// </param>
        /// <param name="resources">
        /// Actual owner that registered and installed this insertion at its closed interval.
        /// </param>
        internal void AcceptInstalledWoundRouting(EffectDraftWoundInsertion insertion,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Effect draft installation cannot be re-entered.");
            try
            {
                if (!_woundInsertions.Values.Any(value => ReferenceEquals(value, insertion)) ||
                    !insertion.TryReadRoutingState(plan, out _, out _, out _) ||
                    !resources.OwnsInsertionBoundary(insertion) || !resources.OwnsInstalledWound(insertion))
                    throw new InvalidOperationException("Exact current insertion and installed resource owner required.");
                _installedWoundVersion = insertion.VersionAfter;
            }
            finally { System.Threading.Volatile.Write(ref _busy, 0); }
        }

        /// <summary>
        /// Carries validated mutation results while the draft retains its insertion receipt.
        /// </summary>
        /// <param name="Before">
        /// Current owned state against which this insertion was prepared.
        /// </param>
        /// <param name="Prepared">
        /// Authenticated preparation applied to the draft.
        /// </param>
        /// <param name="State">
        /// Reduced wound state derived from the actual application results.
        /// </param>
        /// <param name="Wound">
        /// Materialized wound resolved from the reduced state.
        /// </param>
        /// <param name="Applications">
        /// Validated application results used by the wound reducer.
        /// </param>
        /// <param name="ApplicationStart">
        /// First application receipt index belonging to this insertion.
        /// </param>
        /// <param name="WriteStart">
        /// First identity write receipt index belonging to this insertion.
        /// </param>
        /// <param name="AllocationStart">
        /// First identity allocation receipt index belonging to this insertion.
        /// </param>
        /// <param name="CarrierStart">
        /// First carrier edit index belonging to this insertion.
        /// </param>
        private sealed record PendingWoundInsertion(EffectDraftWoundBeforeAuthority Before,
            WoundPreparedAcceptedTurnPlan Prepared, WoundStateReduction State, WoundMaterializationEnvelope Wound,
            IReadOnlyList<EffectAcceptedApplicationResult> Applications,
            int ApplicationStart, int WriteStart, int AllocationStart, int CarrierStart);

        /// <summary>
        /// Applies one prepared creation or worsening to the retained draft and records its real mutation ranges.
        /// The original capture caller must hold its gate across this operation and subsequent routing registration.
        /// This private mutation does not complete or publish the turn.
        /// </summary>
        /// <param name="before">
        /// Exact current source-bound before-state proof produced by this draft.
        /// </param>
        /// <param name="prepared">
        /// Retained preparation for that proof, including the exact allocated root batch.
        /// </param>
        /// <param name="failures">
        /// Receives validation or application failures; failed mutation faults this unpublished draft.
        /// </param>
        /// <returns>
        /// The owned insertion receipt, or <see langword="null"/> when validation or mutation fails.
        /// </returns>
        private EffectDraftWoundInsertion? ApplyPreparedWound(EffectDraftWoundBeforeAuthority before,
            WoundPreparedAcceptedTurnPlan prepared, List<ValidationIssue> failures)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Effect draft application cannot be re-entered.");
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_faulted || _completed)
                    throw new InvalidOperationException("The effect draft is no longer writable.");
                if (_woundInsertions.TryGetValue(before, out var previous))
                    return previous.Matches(prepared) ? previous : Reject("changed applied preparation");
                if (!before.IsCurrent || !before.Selection.IsCurrentFor(this) ||
                    !before.MatchesPrepared(prepared) || prepared.EffectOperationBatches.Count != 1 ||
                    prepared.EffectOperationBatches.Any(batch =>
                        batch.TransitionAuthority.TransitionKind is not ("create" or "worsen")))
                    return Reject("stale, foreign or unsupported preparation");
                failures.AddRange(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared));
                if (failures.Count != 0)
                    return null;
                var sourceAuthority = EffectAcceptedTurnInputComposer.BuildPreparedWoundOperationAuthority(prepared);
                failures.AddRange(sourceAuthority.Issues);
                if (failures.Count != 0)
                    return null;
                // Keep accepted time, scene and condition context used by lifetime
                // materialization; replace only the scoped accepted wound event set.
                var operationEvents = plan.EventInput;
                operationEvents["events"] = new JsonArray(prepared.Binding.AcceptedEvents.Select(value =>
                    (JsonNode)new JsonObject
                    {
                        ["eventRef"] = value.EventRef, ["kind"] = value.Kind, ["authorityId"] = value.AuthorityId
                    }).ToArray());
                var operationInput = new EffectAcceptedTurnInput(prepared.Binding.SessionId,
                    prepared.Binding.SnapshotToken, new JsonObject(), sourceAuthority, plan.TargetAuthority,
                    operationEvents, Realm: prepared.Binding.Realm, SkillScopeAuthority: plan.SkillScopeAuthority,
                    WoundApplicationLocations: before.Selection.Locations?.BindPreparedSources(prepared));
                var operations = PrepareWoundOperations(operationInput, prepared, failures);
                if (failures.Count != 0)
                    return null;
                ValidateWoundReplayAuthority(operations.Applications, operations.Terminations,
                    Array.Empty<Application>(), Array.Empty<TerminalOperation>(),
                    ParseAcceptedEvents(operationEvents["events"], failures), processedEventRefs, failures);
                var beforeCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
                failures.AddRange(beforeCatalog.Issues);
                ValidateWoundTerminalRequests(operations.Terminations, beforeCatalog, identityState.State!, failures, before.RetirementHistory);
                foreach (var request in operations.Applications)
                {
                    var components = request.Application.Source.Definition["components"]!.DeepClone().AsArray();
                    BindParameters(components, request.Application.Parameters);
                    failures.AddRange(ValidateBoundApplicationComponents(request.Application, components, plan.SkillScopeAuthority));
                }
                if (failures.Count != 0)
                    return null;
                var applicationStart = workspace.ApplicationCount;
                var writeStart = identityRoot.WriteCount;
                var allocationStart = identityRoot.AllocationCount;
                var carrierStart = workspace.EditCount;
                try
                {
                    foreach (var terminal in operations.Terminations.OrderBy(value => value.Operation.OperationOrdinal))
                        ApplyWoundTerminalOperation(terminal, workspace, identityState.State!, identityRoot,
                            identityFactory, turn, transitionIds, activeEffects, processedEventRefs, failures);
                    if (failures.Count != 0)
                    {
                        _faulted = true;
                        return null;
                    }
                    var executed = new List<(WoundApplicationRequest Request, ApplicationExecutionFacts Facts)>();
                    foreach (var request in operations.Applications)
                    {
                        var result = ApplyApplication(request.Application, operationInput.Realm, operationEvents,
                            workspace, identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects,
                            processedEventRefs, failures, request.Root.PriorRootEffectId is null
                                ? ApplicationProvenance.Direct
                                : new ApplicationProvenance.SeverityGeneration(request.Root.PriorRootEffectId),
                            skillScopeAuthority: plan.SkillScopeAuthority);
                        if (result == null)
                        {
                            if (failures.Count == 0)
                                AddWoundBatchIssue(failures, "woundApplication", "wound_plan_effect_result_agreement_mismatch",
                                    "one actual typed application", request.Root.ApplicationRef);
                            break;
                        }
                        ValidateWoundApplicationExecution(request, result, failures);
                        if (failures.Count != 0)
                            break;
                        executed.Add((request, result));
                        usedSources.Add(request.Application.Source);
                        usedTargets.Add(request.Application.Target);
                    }
                    identityState = ParseIdentity(identityRoot.ReadSnapshot());
                    failures.AddRange(identityState.Issues);
                    var catalog = EffectCarrierCatalog.Build(workspace.ToInput());
                    failures.AddRange(catalog.Issues);
                    if (failures.Count == 0 && identityState.State != null)
                    {
                        // A sibling application may have replaced an earlier root.
                        // Validate every result against the final current carrier and
                        // identity images before any wound reduction is accepted.
                        foreach (var (request, facts) in executed)
                        {
                            if (!catalog.TryResolveOne(facts.EffectId, out var occurrence) ||
                                !identityState.State.TryGetEntry(facts.EffectId, out var identity) ||
                                !JsonNode.DeepEquals(occurrence.Effect, facts.CreatedEffect) ||
                                !JsonNode.DeepEquals(identity.Raw, facts.CreatedIdentityEntry))
                            {
                                AddWoundBatchIssue(failures, "woundApplication", "wound_plan_effect_result_agreement_mismatch",
                                    "every created root remains its exact active carrier and identity", facts.EffectId);
                                break;
                            }
                            ValidateWoundApplicationExecution(request, facts, failures);
                        }
                    }
                    if (failures.Count == 0)
                        ValidateAfterImages(workspace, identityRoot, activeEffects, failures);
                    if (failures.Count != 0)
                    {
                        _faulted = true;
                        return null;
                    }
                    var nextSlot = 1;
                    var slots = executed.OrderBy(row => row.Facts.EffectId, StringComparer.Ordinal).ToDictionary(
                        row => row.Request.Root.ApplicationRef,
                        row => (IReadOnlyList<WoundEffectSlotAgreement>)row.Request.Root.SlotBindings.Select(slot =>
                            new WoundEffectSlotAgreement(nextSlot++, slot.ProfileKey, slot.ReadableSummary)).ToArray(),
                        StringComparer.Ordinal);
                    var applications = executed.Select(row => new EffectAcceptedApplicationResult(
                        row.Request.Root.ApplicationRef, row.Facts.Disposition, row.Facts.EffectId,
                        row.Facts.TransitionId, row.Facts.CreatedEventRef!, row.Facts.CausalEventRef,
                        row.Facts.Source, row.Facts.Target, row.Facts.CarrierCoordinate,
                        new WoundEffectMaterializationAgreement(slots[row.Request.Root.ApplicationRef],
                            row.Request.Root.ExpectedComponentCount, row.Request.Root.ExpectedMaterializationFingerprint))).ToArray();
                    var reduced = WoundAcceptedTurnPlannerCore.ReduceWoundState(prepared, before.Data, applications);
                    failures.AddRange(reduced.Issues);
                    if (reduced.State == null || failures.Count != 0)
                    {
                        _faulted = true;
                        return null;
                    }
                    var wounds = WoundCarrierCatalog.Build(reduced.State.Carriers);
                    failures.AddRange(wounds.Issues);
                    if (failures.Count != 0 || !wounds.TryResolveOne(prepared.PreparedWounds.Single().WoundId, out var wound))
                    {
                        _faulted = true;
                        return Reject("reduced wound does not resolve to its allocated identity");
                    }
                    _pendingWoundInsertion = new(before, prepared, reduced.State, wound.Wound, applications,
                        applicationStart, writeStart, allocationStart, carrierStart);
                    return EffectDraftWoundInsertion.Retain(this);
                }
                catch
                {
                    _faulted = true;
                    throw;
                }
                finally { _pendingWoundInsertion = null; }
            }
            finally { System.Threading.Volatile.Write(ref _busy, 0); }

            EffectDraftWoundInsertion? Reject(string actual)
            {
                Add(failures, "acceptedTurn.wounds", "spiritual_wound_insertion_mismatch",
                    "one exact owned wound creation or worsening", actual);
                return null;
            }
        }

        /// <summary>
        /// Retains the actual wound and effect mutations of an unpublished insertion.
        /// Routing registration and common final publication require this registered object, not its copied fields.
        /// </summary>
        internal sealed class EffectDraftWoundInsertion
        {
            private readonly EffectAcceptedDraft _owner;
            private readonly WoundMaterializationEnvelope _wound;
            private readonly string _preparationFingerprint;
            private readonly EffectDraftWoundBeforeAuthority _before;
            private readonly WoundPreparedAcceptedTurnPlan _prepared;
            private readonly WoundStateReduction _reducedState;
            private readonly EffectAcceptedApplicationResult[] _applicationResults;
            private readonly WoundOperationBeforeData _routingState;
            private readonly WoundApplicationRootEffectBinding[] _rootBindings;
            private readonly Dictionary<string, (EffectIdentitySourceGroup Group, string Fingerprint)> _retiredIdentities;

            /// <summary>
            /// Copies the actual pending insertion and reads its owner-recorded mutation ranges.
            /// </summary>
            /// <param name="owner">
            /// Actual draft that has advanced to the insertion's resulting version.
            /// </param>
            /// <param name="pending">
            /// Private validated application result created by that draft's mutation method.
            /// </param>
            private EffectDraftWoundInsertion(EffectAcceptedDraft owner, PendingWoundInsertion pending)
            {
                _owner = owner;
                _before = pending.Before;
                _prepared = pending.Prepared.ClonePreservingCacheAuthority();
                _reducedState = pending.State;
                _applicationResults = pending.Applications.Select(
                    WoundAcceptedTurnData.CloneApplicationResult).ToArray();
                _routingState = owner._currentWoundState!;
                _rootBindings = pending.Applications.Select(application =>
                    new WoundApplicationRootEffectBinding(application.ApplicationRef, application.EffectId)).ToArray();
                _preparationFingerprint = pending.Prepared.WoundPreparationFingerprint;
                _wound = WoundAcceptedTurnData.CloneWound(pending.Wound)!;
                VersionAfter = owner._woundVersion;
                Applications = owner.workspace.ReadApplicationsFrom(pending.ApplicationStart);
                IdentityWrites = owner.identityRoot.ReadWritesFrom(pending.WriteStart);
                Allocations = owner.identityRoot.ReadAllocationsFrom(pending.AllocationStart);
                CarrierEdits = owner.workspace.ReadEditsFrom(pending.CarrierStart);
                _retiredIdentities = pending.Before.RetirementHistory is { } previous
                    ? new(previous._retiredIdentities, StringComparer.Ordinal)
                    : new(StringComparer.Ordinal);
                foreach (var operation in pending.Prepared.EffectOperationBatches.SelectMany(batch => batch.TerminalOperations))
                {
                    if (!owner.identityState.State!.TryGetEntry(operation.EffectId, out var retired) ||
                        retired.State != "expired" || retired.Transitions.Last().Kind != "expire" ||
                        retired.Transitions.Last().EventRef != operation.OperationRef)
                        throw new InvalidOperationException("Retirement history requires the actual completed prepared terminal operation.");
                    _retiredIdentities.Add(operation.EffectId, (
                        new EffectIdentitySourceGroup(operation.ExpectedSourceKey.Realm,
                            operation.ExpectedSourceKey.Kind, operation.ExpectedSourceKey.SourceId),
                        WoundAcceptedTurnFingerprintWriter.Compute(new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(retired.Raw) })));
                }
                // A last-use cut can remove a current root before wound terminal
                // planning. Import only real writer receipts of this wound's
                // superseded generation; other wounds keep their current roots.
                if (pending.Prepared.EffectOperationBatches.Any(batch => batch.TransitionAuthority.TransitionKind == "worsen"))
                {
                    var group = new EffectIdentitySourceGroup(_wound.Owner.Realm, "wound", _wound.WoundId);
                    foreach (var receipt in owner._woundTriggerJournal.Values)
                    {
                        if (!receipt.TryReadTerminalIdentity(group, owner.identityState.State!, out var terminal, out var known))
                        {
                            if (known)
                                throw new InvalidOperationException("Known cut retirement must retain its exact terminal writer image.");
                            continue;
                        }
                        var fact = (Group: group, Fingerprint: WoundAcceptedTurnFingerprintWriter.Compute(
                            new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(terminal!.Raw) }));
                        if (_retiredIdentities.TryGetValue(terminal.EffectId, out var previousFact) && previousFact != fact)
                            throw new InvalidOperationException("An existing retirement cannot change its group or terminal history.");
                        _retiredIdentities.TryAdd(terminal.EffectId, fact);
                    }
                }
            }

            /// <summary>
            /// Validates historical membership using an actual registered insertion's completed terminal image.
            /// This does not make the retired effect current or grant publication authority.
            /// </summary>
            /// <param name="group">
            /// Exact wound source group requesting retired membership.
            /// </param>
            /// <param name="identity">
            /// Parsed identity to compare with the completed terminal image.
            /// </param>
            /// <param name="known">
            /// True when this insertion history recorded the identity; mismatches must not fall back to weaker validation.
            /// </param>
            /// <returns>
            /// True only for unchanged terminal facts held by this live registered insertion.
            /// </returns>
            internal bool AuthenticatesRetiredIdentity(EffectIdentitySourceGroup group, EffectIdentityEntry identity, out bool known)
            {
                known = _retiredIdentities.TryGetValue(identity.EffectId, out var fact);
                return known && !_owner._disposed && !_owner._faulted &&
                    _owner._woundInsertions.TryGetValue(_before, out var retained) && ReferenceEquals(retained, this) &&
                    fact.Group == group && identity.State is not ("active" or "suspended") &&
                    fact.Fingerprint == WoundAcceptedTurnFingerprintWriter.Compute(new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(identity.Raw) });
            }

            /// <summary>
            /// Checks that every retirement retained for a source group remains present and unchanged.
            /// </summary>
            /// <param name="group">
            /// Expected source group, independent of the supplied entries' current group fields.
            /// </param>
            /// <param name="identities">
            /// Complete parsed identity state to check by the privately retained effect identities.
            /// </param>
            /// <returns>
            /// True when every retained terminal image for the group is still authenticated; otherwise false.
            /// </returns>
            internal bool ValidatesRetiredContinuity(EffectIdentitySourceGroup group, EffectIdentityState identities) =>
                _retiredIdentities.Where(pair => pair.Value.Group == group).All(pair =>
                    identities.TryGetEntry(pair.Key, out var identity) &&
                    AuthenticatesRetiredIdentity(group, identity, out _));

            /// <summary>
            /// Authenticates this insertion's retained writer receipts against the current unpublished draft state.
            /// Later phases may extend an identity or carrier, but they cannot replace an insertion's immutable
            /// identity header, transition payload, allocation, application, edit, or terminal retirement image.
            /// </summary>
            /// <param name="identities">
            /// Freshly parsed identity state from the draft's current owned identity root.
            /// </param>
            /// <param name="carriers">
            /// Freshly parsed carrier catalog from the draft's current owned workspace.
            /// </param>
            /// <returns>
            /// <see langword="true"/> when every retained receipt still belongs to the owner and every identity-history
            /// anchor remains exact; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool ValidatesCompletionState(EffectIdentityState identities, EffectCarrierCatalog carriers)
            {
                ArgumentNullException.ThrowIfNull(identities);
                ArgumentNullException.ThrowIfNull(carriers);
                if (_owner._disposed || _owner._faulted ||
                    !_owner._woundInsertions.TryGetValue(_before, out var retained) ||
                    !ReferenceEquals(retained, this) || VersionAfter > _owner._woundVersion ||
                    !HasExactPredecessor() || carriers.Issues.Count != 0)
                    return false;

                var ownerWrites = _owner.identityRoot.Writes;
                var ownerAllocations = _owner.identityRoot.Allocations;
                var ownerEdits = _owner.workspace.ReadEditsFrom(0);
                var ownerApplications = _owner.workspace.ReadApplicationsFrom(0);
                foreach (var write in IdentityWrites)
                {
                    if (write.Ordinal < 0 || write.Ordinal >= ownerWrites.Count ||
                        ownerWrites[(int)write.Ordinal] != write ||
                        !identities.TryGetEntry(write.EffectId, out var entry) ||
                        JsonNode.Parse(write.PayloadJson) is not JsonObject payload)
                        return false;

                    if (write.Kind == EffectIdentityWriteKind.CreateEntry)
                    {
                        var createdHeader = payload.DeepClone().AsObject();
                        createdHeader.Remove("state");
                        createdHeader.Remove("transitions");
                        var currentHeader = entry.Raw.DeepClone().AsObject();
                        currentHeader.Remove("state");
                        currentHeader.Remove("transitions");
                        if (!JsonNode.DeepEquals(createdHeader, currentHeader) ||
                            payload["transitions"] is not JsonArray createdTransitions ||
                            createdTransitions.OfType<JsonObject>().Any(expected =>
                                entry.Transitions.Count(value => JsonNode.DeepEquals(value.Raw, expected)) != 1))
                            return false;
                    }
                    else if (entry.Transitions.Count(value => JsonNode.DeepEquals(value.Raw, payload)) != 1)
                    {
                        return false;
                    }

                    if (write.AnchorJson != null &&
                        (JsonNode.Parse(write.AnchorJson) is not JsonObject anchor ||
                         entry.Transitions.Count(value => JsonNode.DeepEquals(value.Raw, anchor)) != 1))
                        return false;
                }

                foreach (var allocation in Allocations)
                {
                    if (allocation.Ordinal < 0 || allocation.Ordinal >= ownerAllocations.Count ||
                        ownerAllocations[(int)allocation.Ordinal] != allocation ||
                        allocation.Kind == EffectIdentityAllocationKind.Effect &&
                        !identities.TryGetEntry(allocation.Identity, out _) ||
                        allocation.Kind == EffectIdentityAllocationKind.Transition &&
                        identities.Entries.SelectMany(static entry => entry.Transitions).Count(transition =>
                            transition.TransitionId == allocation.Identity) != 1)
                        return false;
                }

                foreach (var edit in CarrierEdits)
                    if (edit.Ordinal < 0 || edit.Ordinal >= ownerEdits.Count || ownerEdits[edit.Ordinal] != edit)
                        return false;
                foreach (var effectId in CarrierEdits.Select(static edit => edit.EffectId).Distinct(StringComparer.Ordinal))
                {
                    var latest = ownerEdits.Last(edit => string.Equals(edit.EffectId, effectId, StringComparison.Ordinal));
                    var current = carriers.Occurrences.Where(occurrence =>
                        string.Equals(occurrence.EffectId, effectId, StringComparison.Ordinal)).ToArray();
                    if (latest.AfterJson == null)
                    {
                        if (current.Length != 0)
                            return false;
                        continue;
                    }
                    if (current.Length != 1 || current[0].Coordinate != latest.Carrier ||
                        JsonNode.Parse(latest.AfterJson) is not JsonObject expected ||
                        !JsonNode.DeepEquals(current[0].Effect, expected))
                        return false;
                }
                foreach (var application in Applications)
                {
                    if (!ownerApplications.Any(actual => ReferenceEquals(actual, application)) ||
                        application.IdentityWrites.Any(write => !IdentityWrites.Contains(write)) ||
                        application.Allocations.Any(allocation => !Allocations.Contains(allocation)) ||
                        application.CarrierEdits.Any(edit => !CarrierEdits.Contains(edit)))
                        return false;
                }

                return _retiredIdentities.All(pair =>
                    identities.TryGetEntry(pair.Key, out var identity) &&
                    AuthenticatesRetiredIdentity(pair.Value.Group, identity, out _));
            }

            /// <summary>
            /// Checks that this insertion directly follows the exact retained insertion named by its
            /// operation-before proof, with only the first draft version allowed to omit a predecessor.
            /// </summary>
            /// <returns>
            /// <see langword="true"/> when the predecessor object and adjacent versions preserve the
            /// owner's insertion order; otherwise, <see langword="false"/>.
            /// </returns>
            private bool HasExactPredecessor()
            {
                if (VersionAfter <= 0 || _before.Version != VersionAfter - 1)
                    return false;
                var predecessor = _before.RetirementHistory;
                if (_before.Version == 0)
                    return predecessor == null;
                return predecessor != null && ReferenceEquals(predecessor._owner, _owner) &&
                    predecessor.VersionAfter == _before.Version &&
                    _owner._woundInsertions.Values.Any(value => ReferenceEquals(value, predecessor));
            }

            /// <summary>
            /// Gets a detached wound with actual allocated effect root bindings.
            /// </summary>
            internal WoundMaterializationEnvelope Wound => WoundAcceptedTurnData.CloneWound(_wound)!;
            /// <summary>
            /// Gets the exact source-bound selection that produced this insertion.
            /// </summary>
            internal EffectDraftWoundSelection Selection => _before.Selection;
            /// <summary>
            /// Gets detached operation-before wound and effect snapshots retained by the real preparation.
            /// </summary>
            internal WoundOperationBeforeData OperationBefore => new(
                _before.Data.WoundCarriers, _before.Data.WoundIdentity, _before.Data.WoundHistory,
                _before.Data.EffectCarriers, _before.Data.EffectIdentity);
            /// <summary>
            /// Gets the actual prepared stage with its original cache authority preserved.
            /// </summary>
            internal WoundPreparedAcceptedTurnPlan Prepared => _prepared.ClonePreservingCacheAuthority();
            /// <summary>
            /// Gets the detached output of the actual wound reducer.
            /// </summary>
            internal WoundStateReduction ReducedState => new(_reducedState.Carriers,
                _reducedState.Identity, _reducedState.History, _reducedState.Contributions,
                _reducedState.Intents);
            /// <summary>
            /// Gets the typed effect application results used by the actual wound reducer.
            /// </summary>
            internal IReadOnlyList<EffectAcceptedApplicationResult> ApplicationResults =>
                Array.AsReadOnly(_applicationResults.Select(
                    WoundAcceptedTurnData.CloneApplicationResult).ToArray());
            /// <summary>
            /// Gets the draft version after this insertion.
            /// </summary>
            internal long VersionAfter { get; }
            /// <summary>
            /// Gets whether this actual insertion created a wound rather than replacing an earlier generation.
            /// </summary>
            internal bool IsCreation => _before.Selection.Input.Transitions[0].Kind == "create";
            /// <summary>
            /// Gets the exact closed spiritual interval that admitted this insertion, or null for a Mortal insertion.
            /// </summary>
            internal AcceptedMechanicsPlanner.SpiritualExchangeInterval? Interval => _before.Selection.Interval;
            /// <summary>
            /// Gets the actual ordinary insertion checkpoint, or null for a spiritual insertion.
            /// </summary>
            internal AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint? Checkpoint => _before.Selection.Checkpoint;
            /// <summary>
            /// Gets the actual ordered application receipts from the retained workspace.
            /// </summary>
            internal IReadOnlyList<EffectDraftApplicationReceipt> Applications { get; }
            /// <summary>
            /// Gets the actual ordered identity writes made by the insertion.
            /// </summary>
            internal IReadOnlyList<EffectIdentityWriteReceipt> IdentityWrites { get; }
            /// <summary>
            /// Gets actual allocated identities and their owner-assigned ordinals.
            /// </summary>
            internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations { get; }
            /// <summary>
            /// Gets actual ordered carrier edits made by the insertion.
            /// </summary>
            internal IReadOnlyList<EffectDraftCarrierEdit> CarrierEdits { get; }

            /// <summary>
            /// Reads insertion-time routing data only for this registered receipt at its current draft version.
            /// This read neither registers instances nor advances resources.
            /// </summary>
            /// <param name="acceptedBase">
            /// Exact original base plan owned by the requesting routing owner.
            /// </param>
            /// <param name="state">
            /// Receives immutable insertion-time snapshots, or <see langword="null"/> on rejection.
            /// </param>
            /// <param name="prepared">
            /// Receives the detached authenticated preparation, or <see langword="null"/> on rejection.
            /// </param>
            /// <param name="roots">
            /// Receives actual application-to-root bindings; empty on rejection.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for an exact current insertion of the base; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool TryReadRoutingState(EffectAcceptedTurnPlan acceptedBase,
                out WoundOperationBeforeData? state, out WoundPreparedAcceptedTurnPlan? prepared,
                out IReadOnlyList<WoundApplicationRootEffectBinding> roots)
            {
                state = null;
                prepared = null;
                roots = Array.Empty<WoundApplicationRootEffectBinding>();
                if (!ReferenceEquals(_owner.plan, acceptedBase) || _owner._completed ||
                    _owner._woundVersion != VersionAfter || !_before.Selection.IsCurrentFor(_owner) ||
                    !Matches(_prepared))
                    return false;
                state = _routingState;
                prepared = _prepared.ClonePreservingCacheAuthority();
                roots = Array.AsReadOnly(_rootBindings.ToArray());
                return true;
            }

            /// <summary>
            /// Checks exact retained insertion ownership and its preparation seal for retry.
            /// </summary>
            /// <param name="prepared">
            /// Preparation whose original before proof and allocation seal must match this applied insertion.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for the retained insertion and matching preparation; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool Matches(WoundPreparedAcceptedTurnPlan prepared) =>
                !_owner._disposed && !_owner._faulted &&
                _owner._woundInsertions.TryGetValue(_before, out var retained) && ReferenceEquals(retained, this) &&
                ReferenceEquals(prepared.DraftBefore, _before) && _before.MatchesPrepared(prepared) &&
                prepared.WoundPreparationFingerprint == _preparationFingerprint &&
                WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared).Count == 0;

            /// <summary>
            /// Registers the actual pending mutation after all carrier, identity and wound checks pass.
            /// </summary>
            /// <param name="owner">
            /// Draft holding its application guard with a validated pending insertion.
            /// </param>
            /// <returns>
            /// The registered receipt after advancing the draft wound version exactly once.
            /// </returns>
            internal static EffectDraftWoundInsertion Retain(EffectAcceptedDraft owner)
            {
                if (owner._busy != 1 || owner._disposed || owner._faulted || owner._completed ||
                    owner._pendingWoundInsertion is not { } pending || !pending.Before.IsCurrent)
                    throw new InvalidOperationException("Only the actual pending draft insertion can be retained.");
                owner._currentWoundState = new(pending.State.Carriers, pending.State.Identity, pending.State.History,
                    owner.workspace.ToInput(), owner.identityRoot.ReadSnapshot());
                owner._woundVersion = checked(owner._woundVersion + 1);
                var result = new EffectDraftWoundInsertion(owner, pending);
                owner._woundInsertions.Add(pending.Before, result);
                owner._lastWoundInsertion = result;
                return result;
            }
        }
    }
}
