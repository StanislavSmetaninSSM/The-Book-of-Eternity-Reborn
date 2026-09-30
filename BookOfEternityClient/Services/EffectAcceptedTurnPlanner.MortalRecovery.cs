using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains detached results of one actual unpublished recovery boundary.
/// Its existence alone does not grant execution or publication authority.
/// </summary>
internal sealed class MortalWoundRecoveryExecutedStage
{
    private readonly WoundMaterializationEnvelope _before;
    private readonly WoundMaterializationEnvelope _after;
    private readonly EffectAcceptedApplicationResult[] _applications;
    private readonly EffectAcceptedTerminationResult[] _terminations;
    private readonly WoundTransitionIntent[] _intents;

    /// <summary>
    /// Detaches a completed reduction and its actual effect results.
    /// </summary>
    /// <param name="before">
    /// Exact wound at the start of this boundary.
    /// </param>
    /// <param name="after">
    /// Canonical wound produced by the reducer after actual effect allocation.
    /// </param>
    /// <param name="operationKey">
    /// Unique client-owned operation coordinate within the logical evaluation.
    /// </param>
    /// <param name="eventRef">
    /// Original accepted event shared by the ordered boundaries.
    /// </param>
    /// <param name="tickKey">
    /// Derived boundary tick, or the final logical recovery tick.
    /// </param>
    /// <param name="applications">
    /// Verified effect creations at this boundary, including subsequently retired generations.
    /// </param>
    /// <param name="terminations">
    /// Verified terminal writes at this boundary.
    /// </param>
    /// <param name="intents">
    /// Ordered output from the existing wound transition reducer.
    /// </param>
    internal MortalWoundRecoveryExecutedStage(WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after, string operationKey, string eventRef, string tickKey,
        IReadOnlyList<EffectAcceptedApplicationResult> applications,
        IReadOnlyList<EffectAcceptedTerminationResult> terminations,
        IReadOnlyList<WoundTransitionIntent> intents)
    {
        _before = WoundAcceptedTurnData.CloneWound(before)!;
        _after = WoundAcceptedTurnData.CloneWound(after)!;
        OperationKey = operationKey;
        EventRef = eventRef;
        TickKey = tickKey;
        _applications = applications.Select(WoundAcceptedTurnData.CloneApplicationResult).ToArray();
        _terminations = terminations.Select(WoundAcceptedTurnData.CloneTerminationResult).ToArray();
        _intents = intents.Select(WoundAcceptedTurnData.CloneTransitionIntent).ToArray();
    }

    /// <summary>
    /// Gets a detached stage source wound.
    /// </summary>
    internal WoundMaterializationEnvelope Before => WoundAcceptedTurnData.CloneWound(_before)!;
    /// <summary>
    /// Gets a detached stage result wound.
    /// </summary>
    internal WoundMaterializationEnvelope After => WoundAcceptedTurnData.CloneWound(_after)!;
    /// <summary>
    /// Gets the allocated transition identity of this boundary.
    /// </summary>
    internal string TransitionId => _after.LastTransition.TransitionId;
    /// <summary>
    /// Gets the exact reducer operation kind.
    /// </summary>
    internal string Kind => _after.LastTransition.Kind;
    /// <summary>
    /// Gets the unique operation coordinate of this boundary.
    /// </summary>
    internal string OperationKey { get; }
    /// <summary>
    /// Gets the accepted causal event.
    /// </summary>
    internal string EventRef { get; }
    /// <summary>
    /// Gets the tick retained at this boundary.
    /// </summary>
    internal string TickKey { get; }
    /// <summary>
    /// Gets detached creation results in actual execution order.
    /// </summary>
    internal IReadOnlyList<EffectAcceptedApplicationResult> Applications =>
        Array.AsReadOnly(_applications.Select(WoundAcceptedTurnData.CloneApplicationResult).ToArray());
    /// <summary>
    /// Gets detached terminal results in actual execution order.
    /// </summary>
    internal IReadOnlyList<EffectAcceptedTerminationResult> Terminations =>
        Array.AsReadOnly(_terminations.Select(WoundAcceptedTurnData.CloneTerminationResult).ToArray());
    /// <summary>
    /// Gets detached reducer intents in boundary order.
    /// </summary>
    internal IReadOnlyList<WoundTransitionIntent> TransitionIntents =>
        Array.AsReadOnly(_intents.Select(WoundAcceptedTurnData.CloneTransitionIntent).ToArray());

    /// <summary>
    /// Copies the stage without granting its caller execution authority.
    /// </summary>
    /// <returns>
    /// A detached copy of all wound, effect and reducer facts.
    /// </returns>
    internal MortalWoundRecoveryExecutedStage DetachedCopy() => new(_before, _after,
        OperationKey, EventRef, TickKey, _applications, _terminations, _intents);
}

internal static partial class EffectAcceptedTurnPlanner
{
    private sealed record RecoveryScalarBoundary(int Rank, long Progress, bool Heal,
        bool PreparesHeal, MortalWoundDeteriorationPolicyDefinition? Deterioration);

    private sealed class MortalRecoveryExecutionProof
    {
        private readonly object _continuation;
        private readonly string _preparationFingerprint;
        private readonly MortalWoundRecoveryExecutedStage[] _stages;
        private readonly JsonObject _identity;
        private readonly string _identityFingerprint;
        private string? _payloadFingerprint;

        /// <summary>
        /// Freezes facts recorded by the sole recovery workspace owner.
        /// </summary>
        /// <param name="prepared">
        /// Exact private preparation that admitted this execution.
        /// </param>
        /// <param name="stages">
        /// Completed boundaries in actual execution order.
        /// </param>
        /// <param name="identity">
        /// Actual complete identity image after the ordered execution.
        /// </param>
        internal MortalRecoveryExecutionProof(WoundPreparedAcceptedTurnPlan prepared,
            IReadOnlyList<MortalWoundRecoveryExecutedStage> stages, JsonObject identity)
        {
            _continuation = prepared.RecoveryContinuationAuthority!;
            _preparationFingerprint = prepared.WoundPreparationFingerprint;
            _stages = stages.Select(static stage => stage.DetachedCopy()).ToArray();
            _identity = identity.DeepClone().AsObject();
            _identityFingerprint = RecoveryJsonFingerprint(_identity);
            Fingerprint = ComputeFingerprint();
        }

        /// <summary>
        /// Gets the immutable fingerprint of all private execution facts.
        /// </summary>
        internal string Fingerprint { get; }

        /// <summary>
        /// Binds these immutable facts once to their owner's complete effect payload.
        /// </summary>
        /// <param name="plan">
        /// Actual resulting plan retaining this exact private object.
        /// </param>
        internal void Seal(EffectAcceptedTurnPlan plan)
        {
            if (_payloadFingerprint is not null || !ReferenceEquals(plan.MortalRecoveryExecutionAuthority, this))
                throw new InvalidOperationException("Recovery effect evidence can be sealed only once by its owner.");
            _payloadFingerprint = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        }

        /// <summary>
        /// Revalidates private provenance, frozen facts and the complete payload.
        /// </summary>
        /// <param name="prepared">
        /// Candidate original recovery preparation.
        /// </param>
        /// <param name="plan">
        /// Candidate effect result or detached copy.
        /// </param>
        /// <returns>
        /// True only for an unchanged preparation and exact sealed execution result.
        /// </returns>
        internal bool Agrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnPlan plan) =>
            ReferenceEquals(prepared.RecoveryContinuationAuthority, _continuation) &&
            ReferenceEquals(plan.MortalRecoveryExecutionAuthority, this) &&
            WoundAcceptedTurnPlanner.RecoveryContinuationPreparedAgrees(prepared) &&
            prepared.WoundPreparationFingerprint == _preparationFingerprint &&
            Fingerprint == ComputeFingerprint() &&
            _payloadFingerprint is not null &&
            _payloadFingerprint == WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan) &&
            _identityFingerprint == RecoveryJsonFingerprint(plan.IdentityIndexAfterImage) &&
            _stages.Length != 0;

        /// <summary>
        /// Detaches the frozen execution boundaries.
        /// </summary>
        /// <returns>
        /// Read-only detached stage facts in execution order.
        /// </returns>
        internal IReadOnlyList<MortalWoundRecoveryExecutedStage> Read() =>
            Array.AsReadOnly(_stages.Select(static stage => stage.DetachedCopy()).ToArray());

        /// <summary>
        /// Hashes the private continuation, full wounds, effect results and identity image.
        /// </summary>
        /// <returns>
        /// The canonical private execution fingerprint.
        /// </returns>
        private string ComputeFingerprint()
        {
            var fields = new List<string?>
            {
                "book_of_eternity.mortal_wound.recovery_effect_execution", "1",
                WoundAcceptedTurnPlanner.GetRecoveryContinuationFingerprint(_continuation),
                _preparationFingerprint, _identityFingerprint,
                _stages.Length.ToString(CultureInfo.InvariantCulture)
            };
            foreach (var stage in _stages)
            {
                fields.Add(WoundMaterializationContract.SerializeCanonical(stage.Before));
                fields.Add(WoundMaterializationContract.SerializeCanonical(stage.After));
                fields.Add(stage.OperationKey); fields.Add(stage.EventRef); fields.Add(stage.TickKey);
                fields.Add(JsonSerializer.Serialize(stage.Applications));
                foreach (var application in stage.Applications)
                {
                    fields.Add(application.Materialization.ComponentCount.ToString(CultureInfo.InvariantCulture));
                    fields.Add(application.Materialization.MaterializationFingerprint);
                    fields.Add(JsonSerializer.Serialize(application.Materialization.SlotBindings));
                }
                fields.Add(JsonSerializer.Serialize(stage.Terminations));
                foreach (var intent in stage.TransitionIntents)
                    fields.Add(JsonSerializer.Serialize(intent, intent.GetType()));
            }
            fields.Add(RecoveryJsonFingerprint(_identity));
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
    }

    /// <summary>
    /// Authenticates and reads the exact ordered recovery execution retained by a
    /// prepared continuation and its resulting effect plan.
    /// </summary>
    /// <param name="prepared">
    /// Registry-owned prepared recovery continuation.
    /// </param>
    /// <param name="plan">
    /// Actual aggregate effect plan, including its private execution evidence.
    /// </param>
    /// <param name="stages">
    /// Detached completed boundaries on success; an empty list on failure.
    /// </param>
    /// <returns>
    /// True only when the private provenance and complete payload seal agree.
    /// </returns>
    internal static bool TryReadMortalRecoveryExecution(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan, out IReadOnlyList<MortalWoundRecoveryExecutedStage> stages)
    {
        stages = Array.Empty<MortalWoundRecoveryExecutedStage>();
        if (plan.MortalRecoveryExecutionAuthority is not MortalRecoveryExecutionProof proof ||
            !proof.Agrees(prepared, plan))
            return false;
        stages = proof.Read();
        return true;
    }

    /// <summary>
    /// Reads the immutable private execution fingerprint for effect payload sealing.
    /// </summary>
    /// <param name="plan">
    /// Candidate plan whose private recovery evidence is inspected.
    /// </param>
    /// <returns>
    /// The private execution fingerprint, or null for an ordinary or counterfeit plan.
    /// </returns>
    internal static string? GetMortalRecoveryExecutionFingerprint(EffectAcceptedTurnPlan plan) =>
        (plan.MortalRecoveryExecutionAuthority as MortalRecoveryExecutionProof)?.Fingerprint;

    /// <summary>
    /// Flattens already authenticated boundary results without pretending retired
    /// intermediate creations are active in the final carrier.
    /// </summary>
    /// <param name="prepared">
    /// Registry-owned prepared recovery continuation.
    /// </param>
    /// <param name="plan">
    /// Actual aggregate effect result.
    /// </param>
    /// <param name="applications">
    /// Detached creations in boundary order, or an empty list on failure.
    /// </param>
    /// <param name="terminations">
    /// Detached terminal writes in boundary order, or an empty list on failure.
    /// </param>
    /// <returns>
    /// True only for the exact sealed execution belonging to the preparation.
    /// </returns>
    internal static bool TryGetMortalRecoveryAcceptedResults(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan, out IReadOnlyList<EffectAcceptedApplicationResult> applications,
        out IReadOnlyList<EffectAcceptedTerminationResult> terminations)
    {
        applications = Array.Empty<EffectAcceptedApplicationResult>();
        terminations = Array.Empty<EffectAcceptedTerminationResult>();
        if (!TryReadMortalRecoveryExecution(prepared, plan, out var stages)) return false;
        applications = Array.AsReadOnly(stages.SelectMany(static stage => stage.Applications).ToArray());
        terminations = Array.AsReadOnly(stages.SelectMany(static stage => stage.Terminations).ToArray());
        return true;
    }

    /// <summary>
    /// Validates private recovery input before entering the existing single-workspace planner.
    /// </summary>
    /// <param name="input">
    /// Complete admitted effect input.
    /// </param>
    /// <param name="prepared">
    /// Privately admitted recovery preparation.
    /// </param>
    /// <param name="identityFactory">
    /// Factory used by the shared effect execution.
    /// </param>
    /// <returns>
    /// A genuine effect plan, or structured validation failures.
    /// </returns>
    private static EffectAcceptedTurnPlanningResult BuildMortalRecovery(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, EffectIdentityFactory identityFactory)
    {
        var issues = new List<ValidationIssue>();
        try
        {
            if (!WoundAcceptedTurnPlanner.RecoveryContinuationPreparedAgrees(prepared) ||
                !WoundAcceptedTurnPlanner.RecoveryContinuationEffectInputAgrees(prepared, input) ||
                !WoundAcceptedTurnPlanner.TryReadRecoveryContinuation(prepared.RecoveryContinuationAuthority, out var view) ||
                input.SessionId != prepared.Binding.SessionId || input.SnapshotToken != prepared.Binding.SnapshotToken ||
                input.Realm != prepared.Binding.Realm ||
                EffectCarrierCatalog.CreateAuthorityFingerprint(input.PreTurnCarriers!) !=
                EffectCarrierCatalog.CreateAuthorityFingerprint(view.Input.PreTurnEffectCarriers!) ||
                !JsonNode.DeepEquals(input.PreTurnIdentityIndex, view.Input.PreTurnEffectIdentityIndex))
                throw new InvalidOperationException("Recovery effect input must retain its exact private source baseline and empty commands.");
            return BuildCore(input, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input),
                identityFactory, Array.Empty<WoundApplicationRequest>(), Array.Empty<WoundTerminalRequest>(), prepared);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           JsonException or OverflowException or InvalidDataException)
        {
            AddRecoveryIssue(issues, exception.Message);
            return Failed(issues);
        }
    }

    /// <summary>
    /// Executes the bounded program in the existing workspace and records actual intermediate generations.
    /// </summary>
    /// <param name="input">
    /// Admitted effect input.
    /// </param>
    /// <param name="prepared">
    /// Sealed recovery preparation.
    /// </param>
    /// <param name="workspace">
    /// Shared mutable effect workspace.
    /// </param>
    /// <param name="identityRoot">
    /// Sole identity-history owner.
    /// </param>
    /// <param name="identityFactory">
    /// Owned allocation factory.
    /// </param>
    /// <param name="turn">
    /// Accepted turn number.
    /// </param>
    /// <param name="effectIds">
    /// Accumulated allocated effect identities.
    /// </param>
    /// <param name="transitionIds">
    /// Accumulated effect transition identities.
    /// </param>
    /// <param name="activeEffects">
    /// Shared active effect results.
    /// </param>
    /// <param name="processedEvents">
    /// Shared consumed effect event references.
    /// </param>
    /// <param name="usedSources">
    /// Used source bindings, reduced to final canonical bindings after execution.
    /// </param>
    /// <param name="usedTargets">
    /// Shared resolved targets.
    /// </param>
    /// <param name="rootBindings">
    /// Same-turn root aliases, cleared after final canonical binding.
    /// </param>
    /// <param name="issues">
    /// Validation failures collected by the owner.
    /// </param>
    /// <param name="finalSources">
    /// Final canonical catalog, or the original catalog on early failure.
    /// </param>
    /// <returns>
    /// Private actual execution evidence, or null after a failed boundary.
    /// </returns>
    private static MortalRecoveryExecutionProof? ExecuteMortalRecovery(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        List<ValidationIssue> issues, out EffectSourceAuthority finalSources)
    {
        finalSources = input.SourceAuthority;
        if (!WoundAcceptedTurnPlanner.TryReadRecoveryContinuation(prepared.RecoveryContinuationAuthority, out var view))
        {
            AddRecoveryIssue(issues, "Recovery continuation is absent.");
            return null;
        }
        var boundaries = RecoveryBoundaries(view, issues);
        if (issues.Count != 0) return null;
        if (boundaries.Count != view.AllocatedTransitionIds.Count || boundaries.Count != view.OperationKeys.Count)
        {
            AddRecoveryIssue(issues, "Recovery boundary count differs from allocated transition coordinates.");
            return null;
        }
        var stages = new List<MortalWoundRecoveryExecutedStage>();
        var wound = view.Before;
        var finalRecoverIndex = boundaries.Select((row, index) => (row, index)).Last(pair => !pair.row.Heal).index;
        for (var index = 0; index < boundaries.Count; index++)
        {
            var boundary = boundaries[index];
            var tick = GetMortalRecoveryStageTick(view.Resolution.TickKey, index, finalRecoverIndex, boundary.Heal);
            var completed = ExecuteRecoveryBoundary(input, view, prepared.RecoveryContinuationAuthority!, wound, boundary, index, tick, workspace,
                identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects, processedEvents,
                usedSources, usedTargets, rootBindings, issues);
            if (completed is null || issues.Count != 0) return null;
            stages.Add(completed);
            wound = completed.After;
        }
        finalSources = BuildRecoveryFinalSources(view, wound);
        issues.AddRange(finalSources.Issues);
        if (issues.Count != 0) return null;
        var selected = new EffectIdentitySourceGroup(wound.Owner.Realm, "wound", wound.WoundId);
        usedSources.RemoveAll(entry => new EffectIdentitySourceGroup(entry.Key.Realm, entry.Key.Kind, entry.Key.SourceId) == selected);
        foreach (var definition in wound.Lifecycle == "active" ? wound.Consequences.OwnedEffectSources.Definitions : Array.Empty<JsonElement>())
        {
            var key = new EffectSourceKey(wound.Owner.Realm, "wound", wound.WoundId,
                definition.GetProperty("definitionKey").GetString()!);
            var sourceResult = finalSources.ResolveCanonicalBinding(key,
                WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind));
            if (!sourceResult.Success || sourceResult.Source is not { } source)
            {
                AddRecoveryIssue(issues, "Final wound definition did not resolve in its canonical source catalog.");
                return null;
            }
            usedSources.Add(source);
        }
        // Retired roots remain in the private boundary proof and identity history.
        // Runtime routing sees only the actual final active root bindings.
        // The rebuilt canonical catalog binds final roots by their permanent identities,
        // so no same-turn application aliases escape into final runtime routing.
        rootBindings.Clear();
        return new MortalRecoveryExecutionProof(prepared, stages, identityRoot.ReadSnapshot());
    }

    /// <summary>
    /// Executes one recovery boundary using exact terminal writes and actual allocations before reduction.
    /// </summary>
    /// <param name="input">
    /// Admitted effect event and target context.
    /// </param>
    /// <param name="view">
    /// Detached private recovery continuation view.
    /// </param>
    /// <param name="continuation">
    /// Private capability required by narrow recovery reducer rules.
    /// </param>
    /// <param name="before">
    /// Actual wound produced by the preceding boundary.
    /// </param>
    /// <param name="boundary">
    /// Exact mathematical or typed adverse boundary.
    /// </param>
    /// <param name="index">
    /// Zero-based boundary index.
    /// </param>
    /// <param name="tick">
    /// Deterministic persisted stage tick.
    /// </param>
    /// <param name="workspace">
    /// Shared mutable effect workspace.
    /// </param>
    /// <param name="identityRoot">
    /// Sole effect identity-history owner.
    /// </param>
    /// <param name="identityFactory">
    /// Owned allocation factory.
    /// </param>
    /// <param name="turn">
    /// Accepted turn number.
    /// </param>
    /// <param name="effectIds">
    /// Shared effect allocation list.
    /// </param>
    /// <param name="transitionIds">
    /// Shared effect transition allocation list.
    /// </param>
    /// <param name="activeEffects">
    /// Shared current effect results.
    /// </param>
    /// <param name="processedEvents">
    /// Shared consumed effect event references.
    /// </param>
    /// <param name="usedSources">
    /// Bindings used by actual applications.
    /// </param>
    /// <param name="usedTargets">
    /// Targets used by actual applications.
    /// </param>
    /// <param name="rootBindings">
    /// Aliases recorded during actual application.
    /// </param>
    /// <param name="issues">
    /// Validation failures collected at this boundary.
    /// </param>
    /// <returns>
    /// Detached actual stage facts, or null when validation fails.
    /// </returns>
    private static MortalWoundRecoveryExecutedStage? ExecuteRecoveryBoundary(
        EffectAcceptedTurnInput input, WoundAcceptedTurnPlanner.RecoveryContinuationView view,
        object continuation, WoundMaterializationEnvelope before, RecoveryScalarBoundary boundary,
        int index, string tick, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        List<ValidationIssue> issues)
    {
        var working = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(before) ??
            throw new InvalidOperationException("Recovery requires an exact canonical stage wound.");
        var changedSeverity = boundary.Rank != before.Severity.Rank;
        if (changedSeverity)
        {
            var scalars = MortalWoundTreatmentSeverityReductionPlanner.ApplyReductionScalars(
                before.Severity, boundary.Rank, view.EventRef);
            working = working.WithScalars(working.Scalars with
            {
                Severity = scalars.Severity,
                SlotBudget = boundary.Rank < before.Severity.Rank ? scalars.SlotBudget : working.Scalars.SlotBudget
            });
        }
        if (boundary.Deterioration?.ResultKind == MortalWoundDeteriorationResultKind.AddComplication)
        {
            var draft = MortalWoundTreatmentContract.BuildValidatedComplicationDraft(
                boundary.Deterioration.Result.GetProperty("complicationDraft"));
            if (!working.TryAppendComplication(draft, WoundWorkingReferenceOrigin.PolicyAddition,
                    new WoundWorkingOperationAddress(0, index), out var appended) || appended is null)
                throw new InvalidOperationException("Recovery complication does not fit its exact current wound graph.");
            working = appended;
        }
        issues.AddRange(working.ValidateGraph("mortalRecovery.stageGraph"));
        if (issues.Count != 0) return null;
        var graph = working.Graph;
        var complicationIds = graph.Complications.ToDictionary(row => row.Reference, row =>
            row.Reference.Origin == WoundWorkingReferenceOrigin.Existing ? row.Reference.Value :
                RecoveryCoordinate("wound_complication", view.Resolution.TickKey, index, row.Reference.Value));
        WoundRootOwnershipDomain Domain(WoundWorkingReference root)
        {
            var owner = graph.Complications.SingleOrDefault(row => row.OwnedRoots.Contains(root));
            return owner is null ? WoundRootOwnershipDomain.BaseWound :
                WoundRootOwnershipDomain.ForComplication(complicationIds[owner.Reference]);
        }
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(before.Owner, out var target))
            throw new InvalidOperationException("Recovery wound owner has no exact effect target.");
        var localRef = RecoveryCoordinate("wound_ref", view.Resolution.TickKey, index, before.WoundId);
        var definitions = graph.Definitions.Select(row =>
        {
            var body = JsonNode.Parse(row.Definition.GetRawText())!.AsObject();
            return new WoundEffectSourceDefinition(body["definitionKey"]!.GetValue<string>(), body);
        }).ToArray();
        var definitionMap = definitions.ToDictionary(row => row.DefinitionKey, StringComparer.Ordinal);
        var selectedRoots = boundary.Heal ? Array.Empty<WoundWorkingRoot>() : graph.Roots
            .Where(row => changedSeverity || row.Reference.Origin != WoundWorkingReferenceOrigin.Existing).ToArray();
        var applications = new List<WoundRootEffectApplication>();
        var applicationReferences = new Dictionary<WoundWorkingReference, string>();
        foreach (var root in selectedRoots)
        {
            var definition = definitionMap[root.DefinitionKey].Definition;
            if (definition["components"] is not JsonArray components ||
                !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(before.Owner, target, definition, out var coordinate))
                throw new InvalidOperationException("Recovery root has no exact materialization coordinate.");
            var applicationRef = RecoveryCoordinate("wound_application", view.Resolution.TickKey, index, root.Reference.Value);
            applicationReferences.Add(root.Reference, applicationRef);
            var key = new EffectSourceKey(before.Owner.Realm, "wound", before.WoundId, root.DefinitionKey);
            var parameters = new JsonObject();
            applications.Add(new WoundRootEffectApplication(applicationRef, index + 1, applications.Count + 1,
                "apply", RecoveryCoordinate("wound_operation", view.Resolution.TickKey, index, root.Reference.Value),
                root.DefinitionKey, new WoundEffectTargetSelector(target.Kind, target.TargetId, null), target,
                new WoundEffectSourceSelector(key.Realm, key.Kind, null, localRef, key.DefinitionKey), key, parameters,
                graph.Entries.Where(row => row.Root == root.Reference).Select(row =>
                    new WoundEffectSlotAgreement(row.Slot, row.ProfileKey, row.ReadableSummary)).ToArray(),
                components.Count, WoundEffectMaterializationFingerprint.Compute(key,
                    definition["schemaVersion"]!.GetValue<int>(), parameters, components),
                Domain(root.Reference), view.EventRef, coordinate,
                root.Reference.Origin == WoundWorkingReferenceOrigin.Existing ? root.Reference.Value : null));
        }
        var acceptedEvent = view.Input.Binding.AcceptedEvents.Single(row => row.EventRef == view.EventRef);
        var export = new WoundEffectSourceExport(1, "wound", before.WoundId, localRef, "active", false,
            before.Owner.Realm, before.Owner, view.EventRef, acceptedEvent.SemanticFingerprint,
            view.OperationKeys[index], view.Fingerprint, definitions);
        var currentIdentity = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(currentIdentity.Issues);
        if (currentIdentity.State is null || issues.Count != 0) return null;
        var terminalRoots = changedSeverity || boundary.Heal
            ? before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray()
            : Array.Empty<string>();
        var terminal = WoundEffectTerminalOperationPlanner.Plan(before, workspace.ToInput(), currentIdentity.State,
            terminalRoots, view.EventRef, index + 1, applications.Count, view.OperationKeys[index]);
        issues.AddRange(terminal.Issues);
        if (issues.Count != 0) return null;
        var lineage = graph.Roots.Select(root => applicationReferences.TryGetValue(root.Reference, out var app)
            ? new WoundRootLineageAuthorityRow(app, null, root.DefinitionKey, Domain(root.Reference))
            : new WoundRootLineageAuthorityRow(null, root.Reference.Value, root.DefinitionKey, Domain(root.Reference))).ToArray();
        var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(before);
        var structural = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(view.Fingerprint, localRef,
            before.WoundId, view.OperationKeys[index], view.Fingerprint, view.OperationKeys[index],
            "Mortal wound recovery", before.Severity.Rank, boundary.Heal ? "heal" : "recover", null, beforeFingerprint);
        var transition = new WoundPreparedTransitionAuthority(view.Fingerprint, view.OperationKeys[index],
            view.Fingerprint, view.OperationKeys[index], "Mortal wound recovery", before.Severity.Rank,
            boundary.Heal ? "heal" : "recover", null, beforeFingerprint, structural);
        var provisional = new WoundEffectOperationBatch(localRef, before.WoundId, export, applications,
            terminal.Operations, lineage, string.Empty, transition);
        var batch = new WoundEffectOperationBatch(localRef, before.WoundId, export, applications,
            terminal.Operations, lineage, WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional), transition);
        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput([], [new EffectSourceExport(
            export.Realm, "wound", before.WoundId,
            new JsonArray(definitions.Select(row => (JsonNode)row.Definition).ToArray()),
            Materializable: false, Active: true, SameTurn: true, SourceRef: localRef,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "active" })],
            new HashSet<string>(StringComparer.Ordinal), WoundGroups: [new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(export.Realm, "wound", before.WoundId), before.Owner, target, true,
                localRef, batch.SourceExportFingerprint, definitions,
                lineage.Where(row => row.ApplicationRef is not null).ToArray(),
                lineage.Where(row => row.EffectId is not null).ToArray())]));
        issues.AddRange(sourceAuthority.Issues);
        if (issues.Count != 0) return null;
        var stageInput = input with { SourceAuthority = sourceAuthority };
        var events = view.Input.Binding.AcceptedEvents.ToDictionary(row => row.EventRef, StringComparer.Ordinal);
        var requests = applications.Select(root => PrepareSelectedWoundApplication(stageInput, batch, export,
            definitionMap[root.DefinitionKey], root, events, "mortalRecovery.stageEffects", issues, null, ownedRecovery: true)).ToArray();
        var terminalRequests = terminal.Operations.Select(operation =>
            new WoundTerminalRequest(batch, before, operation, terminalRoots.ToImmutableArray())).ToArray();
        ValidateWoundTerminalRequests(terminalRequests, EffectCarrierCatalog.Build(workspace.ToInput()),
            currentIdentity.State, issues);
        if (issues.Count != 0) return null;
        var terminations = new List<EffectAcceptedTerminationResult>();
        foreach (var request in terminalRequests.OrderBy(row => row.Operation.OperationOrdinal))
        {
            ApplyWoundTerminalOperation(request, workspace, currentIdentity.State, identityRoot, identityFactory,
                turn, transitionIds, activeEffects, processedEvents, issues);
            if (issues.Count != 0) return null;
            var parsed = ParseIdentity(identityRoot.ReadSnapshot());
            issues.AddRange(parsed.Issues);
            if (parsed.State is null || !parsed.State.TryGetEntry(request.Operation.EffectId, out var retired))
                throw new InvalidOperationException("Recovery terminal identity disappeared.");
            var actual = retired.Transitions.Last();
            if (retired.State != "expired" || actual.Kind != "expire" || actual.EventRef != request.Operation.OperationRef)
                throw new InvalidOperationException("Recovery terminal write differs from its operation.");
            var operation = request.Operation;
            terminations.Add(new(operation.OperationRef, "expired", operation.EffectId, actual.TransitionId,
                actual.EventRef, operation.CausalEventRef, operation.ExpectedSourceKey,
                operation.ExpectedTargetKey, operation.ExpectedCarrierCoordinate));
        }
        var executed = new List<(WoundApplicationRequest Request, ApplicationExecutionFacts Facts)>();
        foreach (var request in requests)
        {
            if (request is null) throw new InvalidOperationException("Recovery application did not validate.");
            var facts = ApplyApplication(request.Application, input.Realm, input.EventInput, workspace, identityRoot,
                identityFactory, turn, effectIds, transitionIds, activeEffects, processedEvents, issues,
                request.Root.PriorRootEffectId is null ? ApplicationProvenance.Direct :
                    new ApplicationProvenance.SeverityGeneration(request.Root.PriorRootEffectId),
                skillScopeAuthority: input.SkillScopeAuthority);
            if (facts is null || issues.Count != 0) return null;
            ValidateWoundApplicationExecution(request, facts, issues);
            executed.Add((request, facts));
            usedSources.Add(request.Application.Source);
            usedTargets.Add(request.Application.Target);
            rootBindings.Add(new(request.Root.ApplicationRef, facts.EffectId));
        }
        ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
        var resultingCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
        var resultingIdentity = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(resultingCatalog.Issues);
        issues.AddRange(resultingIdentity.Issues);
        if (issues.Count != 0 || resultingIdentity.State is null) return null;
        foreach (var (_, facts) in executed)
            if (!resultingCatalog.TryResolveOne(facts.EffectId, out var occurrence) ||
                !resultingIdentity.State.TryGetEntry(facts.EffectId, out var actualIdentity) ||
                !JsonNode.DeepEquals(occurrence.Effect, facts.CreatedEffect) ||
                !JsonNode.DeepEquals(actualIdentity.Raw, facts.CreatedIdentityEntry))
                throw new InvalidOperationException("Recovery created root did not survive its own boundary exactly.");
        var allocatedByRef = executed.ToDictionary(row => row.Request.Root.ApplicationRef, row => row.Facts.EffectId, StringComparer.Ordinal);
        string RootId(WoundWorkingReference reference) => applicationReferences.TryGetValue(reference, out var app)
            ? allocatedByRef[app] : reference.Value;
        var entries = graph.Entries.OrderBy(row => RootId(row.Root), StringComparer.Ordinal).ThenBy(row => row.Slot)
            .Select((row, slot) => new WoundConsequenceEntry(slot + 1, row.ProfileKey, RootId(row.Root), row.ReadableSummary)).ToArray();
        var applicationResults = executed.Select(row => new EffectAcceptedApplicationResult(row.Request.Root.ApplicationRef,
            row.Facts.Disposition, row.Facts.EffectId, row.Facts.TransitionId, row.Facts.CreatedEventRef!, row.Facts.CausalEventRef,
            row.Facts.Source, row.Facts.Target, row.Facts.CarrierCoordinate, new WoundEffectMaterializationAgreement(
                entries.Where(entry => entry.EffectId == row.Facts.EffectId).Select(entry =>
                    new WoundEffectSlotAgreement(entry.Slot, entry.ProfileKey, entry.ReadableSummary)).ToArray(),
                row.Request.Root.ExpectedComponentCount, row.Request.Root.ExpectedMaterializationFingerprint))).ToArray();
        var definitionBodies = graph.Definitions.Select(row => row.Definition.Clone()).ToArray();
        var after = before with
        {
            Severity = working.Scalars.Severity,
            Recovery = before.Recovery with { CurrentStepProgress = boundary.Progress, LastTickKey = tick },
            LastTransition = new(view.AllocatedTransitionIds[index], checked(before.LastTransition.Ordinal + 1), turn,
                boundary.Heal ? "heal" : "recover")
        };
        if (boundary.Heal)
            after = after with { Lifecycle = "healed", Care = before.Care with { State = "healed", ActiveCourseId = null } };
        else
            after = after with
            {
                Complications = graph.Complications.Select(row => new WoundComplication(complicationIds[row.Reference],
                    row.Kind, row.State, row.DisplayName, row.TreatmentDifficultyModifier,
                    row.OwnedRoots.Select(RootId).ToArray(), row.Visibility)).ToArray(),
                Consequences = new WoundConsequences(working.Scalars.SlotBudget, entries.Length, entries)
                {
                    OwnedEffectSources = new WoundOwnedEffectSources(definitionBodies,
                        graph.Roots.Select(row => new WoundRootEffectBinding(RootId(row.Reference), row.DefinitionKey)).ToArray())
                    { DefinitionFacts = WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(definitionBodies) }
                }
            };
        var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(after);
        WoundTransitionEvidence evidence = boundary.Heal
            ? new WoundHealingEvidence(view.Resolution.AuthorityFingerprint, beforeFingerprint, afterFingerprint,
                view.Resolution.TickKey, null, Array.Empty<WoundLegacyDeclaration>())
            : new WoundRecoveryEvidence(view.Resolution.AuthorityFingerprint, beforeFingerprint, afterFingerprint, tick,
                view.Resolution.ClockKind, new WoundDeclaredTransitionOutcome(after.Severity.Rank, after.Care.State,
                    after.Recovery.CurrentStepProgress, boundary.PreparesHeal, false, boundary.Deterioration is not null,
                    after.Complications.Select(row => row.ComplicationId).ToArray(),
                    after.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).Order(StringComparer.Ordinal).ToArray(),
                    after.Recovery.Blockers, after.Treatment.CompletedRouteIds), continuation);
        var reduced = WoundTransitionReducer.Reduce(new WoundTransitionRequest(after.LastTransition.Kind,
            after.LastTransition.TransitionId, view.OperationKeys[index], view.EventRef, turn, before, after, evidence));
        issues.AddRange(reduced.Issues);
        return reduced.IsValid ? new(before, reduced.ProposedAfter!, view.OperationKeys[index], view.EventRef,
            tick, applicationResults, terminations, reduced.Intents) : null;
    }

    /// <summary>
    /// Rebuilds source authority from original source roots and the actual final wound.
    /// </summary>
    /// <param name="view">
    /// Private continuation with complete source roots.
    /// </param>
    /// <param name="wound">
    /// Actual final reduced wound.
    /// </param>
    /// <returns>
    /// Canonical final sources, excluding the active source of a healed wound.
    /// </returns>
    private static EffectSourceAuthority BuildRecoveryFinalSources(
        WoundAcceptedTurnPlanner.RecoveryContinuationView view, WoundMaterializationEnvelope wound)
    {
        var roots = view.SourceRoots.ToDictionary(row => row.Key, row => row.Value?.DeepClone(), StringComparer.Ordinal);
        if (roots.GetValueOrDefault(wound.Owner.CarrierPath) is not JsonObject carrier ||
            !WoundCarrierCollectionAuthority.TryResolve(carrier, wound.Owner, out var collection, out _))
            throw new InvalidOperationException("Recovery final source owner carrier is absent.");
        var matches = collection.Select((node, index) => (node, index))
            .Where(row => row.node?["woundId"]?.GetValue<string>() == wound.WoundId).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Recovery source wound is absent or ambiguous.");
        if (wound.Lifecycle == "active")
            collection[matches[0].index] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound));
        else
            collection.RemoveAt(matches[0].index);
        return EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
    }

    /// <summary>
    /// Derives the bounded scalar program from authoritative intents and the original wound policy.
    /// </summary>
    /// <param name="view">
    /// Private continuation whose resolution was rederived by the registry.
    /// </param>
    /// <param name="issues">
    /// Invalid program diagnostics.
    /// </param>
    /// <returns>
    /// Ordered boundaries, including a default evaluation boundary when no mutation is due.
    /// </returns>
    private static IReadOnlyList<RecoveryScalarBoundary> RecoveryBoundaries(
        WoundAcceptedTurnPlanner.RecoveryContinuationView view, List<ValidationIssue> issues)
    {
        var result = new List<RecoveryScalarBoundary>();
        var rank = view.Before.Severity.Rank;
        var progress = view.Before.Recovery.CurrentStepProgress;
        foreach (var intent in view.Resolution.TransitionIntents)
        {
            if (intent is MortalWoundRecoveryProgressIntent recovery)
            {
                var sequence = MortalWoundRecoveryProgressProgram.Create(rank, progress,
                    view.Before.Recovery.CurrentStepThreshold, recovery.ElapsedCadences, view.Before.Recovery.CarryOverflow);
                for (var i = 0; i < sequence.Length; i++)
                {
                    var row = sequence[i];
                    result.Add(new(row.SeverityRank, row.Progress, row.FullHeal,
                        !row.FullHeal && i + 1 < sequence.Length && sequence[i + 1].FullHeal, null));
                }
                if (sequence.Length != 0) { rank = sequence[^1].SeverityRank; progress = sequence[^1].Progress; }
            }
            else if (intent is MortalWoundRecoveryDeteriorationIntent deterioration)
            {
                if (result.Any(static row => row.Heal) || view.Before.Recovery.DeteriorationPolicy is not { } policyJson)
                {
                    AddRecoveryIssue(issues, "Deterioration cannot follow full healing or omit its original policy.");
                    break;
                }
                var parsed = MortalWoundDeteriorationPolicyContract.Parse(policyJson,
                    "mortalRecovery.deteriorationPolicy", view.Before.Owner.Realm,
                    WoundMaterializationContract.ResolveEffectTargetKind(view.Before.Owner.OwnerKind),
                    view.Before.Severity.Rank);
                issues.AddRange(parsed.Issues);
                if (parsed.Policy is not { } policy || policy.PolicyRef != deterioration.PolicyRef ||
                    policy.Classification != MortalWoundDeteriorationPolicyClassification.StrictlyWorsening)
                {
                    AddRecoveryIssue(issues, "Deterioration differs from its sealed original policy.");
                    break;
                }
                if (policy.ResultKind == MortalWoundDeteriorationResultKind.IncreaseSeverity)
                {
                    rank = checked(rank + 1);
                    progress = 0;
                    if (rank > 4) { AddRecoveryIssue(issues, "The ordered adverse result exceeds severity IV."); break; }
                }
                else if (policy.ResultKind != MortalWoundDeteriorationResultKind.AddComplication)
                {
                    AddRecoveryIssue(issues, "An adverse mutation must increase severity or add its exact complication.");
                    break;
                }
                result.Add(new(rank, progress, false, false, policy));
            }
            else if (intent is not MortalWoundDeathHandoffIntent)
            {
                AddRecoveryIssue(issues, "Unknown recovery intent.");
                break;
            }
        }
        if (result.Count == 0) result.Add(new(rank, progress, false, false, null));
        if (result.Count > 6) AddRecoveryIssue(issues, "Recovery exceeds its bounded ordered stage count.");
        return result;
    }

    /// <summary>
    /// Computes the persisted tick of one ordered recovery boundary.
    /// </summary>
    /// <param name="logicalTick">
    /// Exact logical tick retained by the closed recovery resolution.
    /// </param>
    /// <param name="stageIndex">
    /// Zero-based boundary index in the validated recovery program.
    /// </param>
    /// <param name="finalRecoverIndex">
    /// Zero-based final recover boundary, immediately preceding a separate heal when present.
    /// </param>
    /// <param name="heal">
    /// Whether the boundary is the separate full-healing transition.
    /// </param>
    /// <returns>
    /// The logical tick for final recovery and healing, or a deterministic intermediate tick.
    /// </returns>
    internal static string GetMortalRecoveryStageTick(string logicalTick, int stageIndex, int finalRecoverIndex, bool heal) =>
        stageIndex == finalRecoverIndex || heal ? logicalTick :
            RecoveryCoordinate("wound_recovery_stage_tick", logicalTick, stageIndex, "tick");

    private static string RecoveryCoordinate(string prefix, string tick, int stage, string local) =>
        prefix + "_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound.recovery_effect_coordinate", "1", prefix, tick,
            stage.ToString(CultureInfo.InvariantCulture), local
        })["sha256:".Length..][..32];

    private static string RecoveryJsonFingerprint(JsonNode node) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(node) });

    private static void AddRecoveryIssue(List<ValidationIssue> issues, string actual) =>
        AddWoundBatchIssue(issues, "mortalRecovery.effectExecution", "mortal_wound_recovery_effect_execution_invalid",
            "one exact private bounded recovery execution with canonical intermediate generations", actual);
}
