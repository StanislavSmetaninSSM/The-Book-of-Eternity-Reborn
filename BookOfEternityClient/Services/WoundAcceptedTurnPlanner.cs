using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class WoundEffectOperationEventRef
{
    private const string Domain = "book_of_eternity.wound.effect_operation_event_ref";
    private const string Version = "1";

    internal static string Create(
        string parentEventRef,
        int mechanicsOrdinal,
        int operationOrdinal,
        string operationKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentEventRef);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mechanicsOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operationOrdinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKind);

        var payload = new StringBuilder();
        Append(payload, Domain);
        Append(payload, Version);
        Append(payload, parentEventRef);
        Append(payload, mechanicsOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(payload, operationOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(payload, operationKind);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
        return "wound_effect:sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string? value)
    {
        if (value is null)
        {
            target.Append("-1:");
            return;
        }

        target.Append(Encoding.UTF8.GetByteCount(value));
        target.Append(':');
        target.Append(value);
    }
}

internal static class WoundEffectBatchPlanner
{
    internal static WoundEffectBatchPlanningResult Build(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput effectInput,
        EffectIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectInput);
        ArgumentNullException.ThrowIfNull(identityFactory);

        var preparedIssues = WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared);
        if (preparedIssues.Count != 0)
            return new WoundEffectBatchPlanningResult(null, preparedIssues);

        if (!WoundAcceptedTurnPlannerCore.EffectInputAgreesWithPrepared(
                prepared,
                effectInput))
        {
            return WoundAcceptedTurnPlannerCore.FailedEffectBatch(
                "wound_plan_prepared_seal_mismatch",
                "The composed effect input must carry the exact sealed wound source exports.",
                "exact prepared wound source graph",
                "changed or unresolved effect source authority");
        }

        var ordinary = EffectAcceptedTurnPlanner.BuildWoundBatch(
            effectInput,
            prepared,
            identityFactory);
        return AcceptEffectResult(prepared, effectInput, ordinary);
    }

    internal static WoundEffectBatchPlanningResult AcceptEffectResult(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput composedInput,
        EffectAcceptedTurnPlanningResult effectResult)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(composedInput);
        ArgumentNullException.ThrowIfNull(effectResult);

        var preparedIssues = WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared);
        if (preparedIssues.Count != 0)
            return new WoundEffectBatchPlanningResult(null, preparedIssues);

        var effectPlan = effectResult.Plan;
        var effectIssues = effectResult.Issues;
        if (effectIssues is null ||
            effectIssues.Any(static issue => issue is null) ||
            (effectPlan is not null && effectIssues.Count != 0) ||
            (effectPlan is null && effectIssues.Count == 0))
        {
            return WoundAcceptedTurnPlannerCore.FailedEffectBatch(
                "wound_plan_effect_handoff_invalid",
                "The ordinary effect planner returned a malformed result envelope.",
                "exactly one successful plan or a non-empty detached issue set",
                "malformed effect planning envelope");
        }

        if (effectPlan is null)
        {
            var upstream = effectIssues
                .Select(WoundAcceptedTurnPlannerCore.CloneIssue)
                .ToList();
            upstream.Add(WoundAcceptedTurnPlannerCore.NewIssue(
                "wound_plan_effect_stage_failed",
                "The ordinary effect stage failed before a wound batch could be accepted.",
                "successful typed effect stage",
                "ordinary effect planning failure"));
            return new WoundEffectBatchPlanningResult(null, upstream.ToArray());
        }

        if (!WoundAcceptedTurnPlannerCore.EffectInputAgreesWithPrepared(
                prepared,
                composedInput))
        {
            return WoundAcceptedTurnPlannerCore.FailedEffectBatch(
                "wound_plan_prepared_seal_mismatch",
                "The composed effect input no longer agrees with the sealed wound sources.",
                "exact prepared wound source graph",
                "changed effect source authority");
        }

        try
        {
            var actualEffectInput = WoundAcceptedTurnFingerprints.ComputeEffectInput(
                prepared,
                composedInput);
            if (!string.Equals(
                    effectPlan.InputFingerprint,
                    actualEffectInput,
                    StringComparison.Ordinal))
            {
                return WoundAcceptedTurnPlannerCore.FailedEffectBatch(
                    "wound_plan_effect_handoff_invalid",
                    "The ordinary effect plan belongs to a different composed effect input.",
                    actualEffectInput,
                    effectPlan.InputFingerprint ?? "null");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return WoundAcceptedTurnPlannerCore.FailedEffectBatch(
                "wound_plan_effect_handoff_invalid",
                "The ordinary effect plan input seal could not be recomputed safely.",
                "well-formed agreeing composed input and ordinary plan",
                exception.GetType().Name);
        }

        var derived = WoundAcceptedTurnPlannerCore.DeriveEffectResults(
            prepared,
            effectPlan);
        if (derived.Issues.Count != 0)
        {
            var issues = derived.Issues
                .Select(WoundAcceptedTurnPlannerCore.CloneIssue)
                .ToList();
            issues.Add(WoundAcceptedTurnPlannerCore.NewIssue(
                "wound_plan_effect_stage_failed",
                "The created effect after-images did not satisfy the sealed wound batch.",
                "one exact created effect result per prepared root",
                "effect result agreement failure"));
            return new WoundEffectBatchPlanningResult(null, issues.ToArray());
        }

        return new WoundEffectBatchPlanningResult(
            WoundEffectBatchAcceptedPlan.Create(
                prepared,
                composedInput,
                effectPlan,
                derived.Applications,
                derived.Terminations),
            Array.Empty<ValidationIssue>());
    }
}

internal static partial class WoundAcceptedTurnPlanner
{
    internal static WoundHistoryReplayResult ResolveAcceptedReplay(
        WoundHistoryState history,
        WoundHistoryReplayProbe probe)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(probe);

        var replay = history.ResolveReplay(probe);
        if (replay.Disposition != WoundHistoryReplayDisposition.Exact ||
            replay.Transition is null)
        {
            return replay;
        }

        var accepted = replay.Transition;
        return replay with
        {
            AlreadyAcceptedReceipt = new WoundAlreadyAcceptedReceipt(
                accepted.OperationKey,
                accepted.EventRef,
                accepted.AttemptId,
                accepted.CourseId,
                accepted.CourseMilestoneOrdinal,
                accepted.CycleKey,
                accepted.PaymentFingerprint,
                accepted.OutputFingerprint,
                accepted.ReadableSummary)
        };
    }

    internal static WoundAcceptedSourceBindingResult BindAcceptedSourceTargets(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        EffectTargetAuthority effectTargets) =>
        WoundAcceptedTurnPlannerCore.BindAcceptedSourceTargets(
            binding,
            opportunities,
            effectTargets);

    internal static WoundAcceptedTurnPreparationResult Prepare(
        WoundAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Prepare(input, new WoundAcceptedTurnIdentityAllocator());
    }

    internal static WoundAcceptedTurnPreparationResult Prepare(
        WoundAcceptedTurnInput input,
        IWoundAcceptedTurnIdentityAllocator identityAllocator)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityAllocator);

        var validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
        if (validation.Issues.Count != 0)
        {
            return new WoundAcceptedTurnPreparationResult(
                null,
                validation.Issues);
        }

        var bindingFingerprint = WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding);
        var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
        var allocatedWoundIds = new List<string>(validation.Transitions.Count);
        var allocatedTransitionIds = new List<string>(validation.Transitions.Count);
        var allocatedApplicationRefs = new List<string>();
        var preparedWounds = new List<WoundMaterializationEnvelope>(
            validation.Transitions.Count);
        var batches = new List<WoundEffectOperationBatch>(validation.Transitions.Count);

        foreach (var candidate in validation.Transitions)
        {
            var woundId = candidate.BeforeWound?.WoundId ??
                identityAllocator.CreateWoundId(candidate.Scope);
            var transitionId = identityAllocator.CreateTransitionId(
                candidate.Scope,
                candidate.Draft.LocalTransitionRef);
            var applicationRefs = new string[candidate.Graph.Roots.Count];
            for (var rootIndex = 0; rootIndex < candidate.Graph.Roots.Count; rootIndex++)
            {
                var root = candidate.Graph.Roots[rootIndex];
                applicationRefs[rootIndex] = identityAllocator.CreateApplicationRef(
                    candidate.Scope,
                    root.Draft.LocalApplicationRef,
                    root.DefinitionKey,
                    root.Draft.OperationKey);
            }

            allocatedWoundIds.Add(woundId);
            allocatedTransitionIds.Add(transitionId);
            allocatedApplicationRefs.AddRange(applicationRefs);
            candidate.Allocation = new WoundAcceptedTurnPlannerCore.Allocation(
                woundId,
                transitionId,
                applicationRefs);
        }

        var collisionIssues = WoundAcceptedTurnPlannerCore.ValidateAllocatedIdentities(
            validation,
            allocatedWoundIds,
            allocatedApplicationRefs,
            allocatedTransitionIds);
        if (collisionIssues.Count != 0)
            return new WoundAcceptedTurnPreparationResult(null, collisionIssues);

        foreach (var candidate in validation.Transitions)
        {
            var materialized = WoundAcceptedTurnPlannerCore.MaterializePreparedCandidate(
                input,
                inputFingerprint,
                candidate,
                validation.EffectIdentities);
            if (materialized.Issues.Count != 0)
                return new WoundAcceptedTurnPreparationResult(null, materialized.Issues);
            preparedWounds.Add(materialized.PreparedWound!);
            batches.Add(materialized.Batch!);
        }

        var baselineAuthority = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(
            inputFingerprint,
            input);
        var provisional = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            bindingFingerprint,
            inputFingerprint,
            string.Empty,
            allocatedWoundIds,
            allocatedTransitionIds,
            preparedWounds,
            batches,
            baselineAuthority);
        var preparationFingerprint =
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional);
        var prepared = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            bindingFingerprint,
            inputFingerprint,
            preparationFingerprint,
            allocatedWoundIds,
            allocatedTransitionIds,
            preparedWounds,
            batches,
            baselineAuthority);
        return new WoundAcceptedTurnPreparationResult(
            prepared,
            Array.Empty<ValidationIssue>());
    }

    internal static WoundAcceptedTurnPlanningResult Finalize(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(effectResult);

        return WoundAcceptedTurnPlannerCore.Finalize(prepared, effectResult);
    }
}

internal static class WoundAcceptedTurnPlannerCore
{
    private const string PlanPath = "acceptedTurn.wounds";
    private const int MaximumTransitions = 32;

    internal sealed record Allocation(
        string WoundId,
        string TransitionId,
        IReadOnlyList<string> ApplicationRefs);

    internal sealed record PreparedRootDraft(
        WoundAcceptedRootApplicationDraft Draft,
        WoundAcceptedEffectDefinitionDraft Definition,
        string DefinitionKey,
        IReadOnlyList<WoundAcceptedConsequenceSlotBinding> Slots);

    internal sealed record PreparedDraftGraph(
        IReadOnlyList<WoundAcceptedEffectDefinitionDraft> Definitions,
        IReadOnlyList<PreparedRootDraft> Roots);

    internal sealed class ValidatedTransition
    {
        internal ValidatedTransition(
            WoundAcceptedTransitionDraft draft,
            WoundOpportunityAuthority opportunity,
            WoundAcceptedEventAuthority acceptedEvent,
            WoundAcceptedTurnIdentityScope scope,
            PreparedDraftGraph graph,
            int mechanicsOrdinal,
            WoundMaterializationEnvelope? beforeWound)
        {
            Draft = draft;
            Opportunity = opportunity;
            AcceptedEvent = acceptedEvent;
            Scope = scope;
            Graph = graph;
            MechanicsOrdinal = mechanicsOrdinal;
            BeforeWound = WoundAcceptedTurnData.CloneWound(beforeWound);
        }

        internal WoundAcceptedTransitionDraft Draft { get; }
        internal WoundOpportunityAuthority Opportunity { get; }
        internal WoundAcceptedEventAuthority AcceptedEvent { get; }
        internal WoundAcceptedTurnIdentityScope Scope { get; }
        internal PreparedDraftGraph Graph { get; }
        internal int MechanicsOrdinal { get; }
        internal WoundMaterializationEnvelope? BeforeWound { get; }
        internal Allocation? Allocation { get; set; }
    }

    internal sealed record ValidatedInput(
        IReadOnlyList<ValidatedTransition> Transitions,
        WoundCarrierCatalog? Carriers,
        WoundIdentityState? Identities,
        WoundHistoryState? History,
        EffectIdentityState? EffectIdentities,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed record EffectDerivation(
        IReadOnlyList<EffectAcceptedApplicationResult> Applications,
        IReadOnlyList<EffectAcceptedTerminationResult> Terminations,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed record PreparedCandidateResult(
        WoundMaterializationEnvelope? PreparedWound,
        WoundEffectOperationBatch? Batch,
        IReadOnlyList<ValidationIssue> Issues);

    internal static ValidationIssue NewIssue(
        string code,
        string message,
        string expected,
        string actual,
        string? path = null) =>
        new(
            path ?? PlanPath,
            IssueSeverity.Error,
            message,
            code,
            actor: "accepted_turn",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Recompose the accepted wound turn from its sealed authority inputs.");

    internal static ValidationIssue CloneIssue(ValidationIssue issue) =>
        WoundAcceptedTurnData.CloneIssue(issue);

    internal static WoundEffectBatchPlanningResult FailedEffectBatch(
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            null,
            new[] { NewIssue(code, message, expected, actual) });

    internal static WoundAcceptedTurnPlanningResult Finalize(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchPlanningResult effectResult)
    {
        if (effectResult.Issues is null ||
            effectResult.Issues.Any(static issue => issue is null) ||
            (effectResult.Plan is not null && effectResult.Issues.Count != 0) ||
            (effectResult.Plan is null && effectResult.Issues.Count == 0))
        {
            return FailedFinal(
                "wound_plan_effect_handoff_invalid",
                "The wound effect stage returned a malformed result envelope.",
                "exactly one successful plan or a non-empty detached issue set",
                "malformed effect-stage envelope");
        }

        if (effectResult.Plan is null)
        {
            var upstream = effectResult.Issues.Select(CloneIssue).ToList();
            upstream.Add(NewIssue(
                "wound_plan_effect_stage_failed",
                "Wound finalization was skipped because the effect stage failed.",
                "successful sealed effect batch",
                "upstream effect-stage failure"));
            return new WoundAcceptedTurnPlanningResult(null, upstream.ToArray());
        }

        var preparedIssues = ValidatePreparedAuthority(prepared);
        if (preparedIssues.Count != 0)
            return new WoundAcceptedTurnPlanningResult(null, preparedIssues);

        var accepted = effectResult.Plan;
        try
        {
            var actualPreparation = WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
            if (!string.Equals(
                    actualPreparation,
                    accepted.WoundPreparationFingerprint,
                    StringComparison.Ordinal))
            {
                return FailedFinal(
                    "wound_plan_prepared_seal_mismatch",
                    "The effect batch belongs to a different prepared wound plan.",
                    actualPreparation,
                    accepted.WoundPreparationFingerprint ?? "null");
            }

            var actualEffectInput = WoundAcceptedTurnFingerprints.ComputeEffectInput(
                prepared,
                accepted.EffectInput);
            var actualEffectPlan = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
                prepared,
                accepted.EffectInput,
                accepted.EffectPlan,
                accepted.ApplicationResults,
                accepted.TerminationResults);
            if (!string.Equals(
                    actualEffectInput,
                    accepted.EffectInputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actualEffectInput,
                    accepted.EffectPlan.InputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actualEffectPlan,
                    accepted.EffectAcceptedTurnPlanFingerprint,
                    StringComparison.Ordinal))
            {
                return FailedFinal(
                    "wound_plan_effect_handoff_invalid",
                    "The accepted effect batch no longer matches its detached effect seals.",
                    "exact recomputed effect-input and effect-plan fingerprints",
                    "stale or changed effect batch payload");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return FailedFinal(
                "wound_plan_effect_handoff_invalid",
                "The accepted effect batch could not be recomputed safely.",
                "well-formed detached effect batch",
                exception.GetType().Name);
        }

        var shapeIssue = ValidateAcceptedResultShape(accepted);
        if (shapeIssue is not null)
            return new WoundAcceptedTurnPlanningResult(null, new[] { shapeIssue });

        var expectedApplications = prepared.EffectOperationBatches
            .SelectMany(static batch => batch.RootApplications)
            .ToArray();
        var expectedTerminations = prepared.EffectOperationBatches
            .SelectMany(static batch => batch.TerminalOperations)
            .ToArray();
        var applications = accepted.ApplicationResults;
        var terminations = accepted.TerminationResults;
        if (applications.Count != expectedApplications.Length ||
            terminations.Count != expectedTerminations.Length ||
            !ExactAndConfusableUnique(applications.Select(static value => value.ApplicationRef)) ||
            !ExactAndConfusableUnique(applications.Select(static value => value.EffectId)) ||
            !ExactAndConfusableUnique(applications.Select(static value => value.CreateTransitionId)) ||
            !ExactAndConfusableUnique(applications.Select(static value => value.CreatedEventRef)) ||
            !ExactAndConfusableUnique(terminations.Select(static value => value.OperationRef)) ||
            !ExactAndConfusableUnique(terminations.Select(static value => value.EffectId)) ||
            !ExactAndConfusableUnique(terminations.Select(static value =>
                value.TerminalTransitionId)) ||
            !applications.Select(static value => value.ApplicationRef)
                .SequenceEqual(
                    expectedApplications.Select(static value => value.ApplicationRef),
                    StringComparer.Ordinal) ||
            !terminations.Select(static value => value.OperationRef)
                .SequenceEqual(
                    expectedTerminations.Select(static value => value.OperationRef),
                    StringComparer.Ordinal) ||
            applications.Select(static value => value.EffectId).Intersect(
                terminations.Select(static value => value.EffectId),
                StringComparer.Ordinal).Any())
        {
            return FailedFinal(
                "wound_plan_effect_result_set_mismatch",
                "The accepted application/termination result set is not the exact ordered prepared set.",
                "one exact/confusable-unique ordered result per prepared operation",
                "missing, extra, duplicate, confusable, or reordered result");
        }

        if (applications.Any(static value => !string.Equals(
                value.Disposition,
                "created_new_identity",
                StringComparison.Ordinal)))
        {
            return FailedFinal(
                "wound_plan_effect_result_disposition_mismatch",
                "Every wound root must create a new independent effect identity.",
                "created_new_identity",
                string.Join(",", applications.Select(static value => value.Disposition)));
        }
        if (terminations.Any(static value => !string.Equals(
                value.Disposition,
                "expired",
                StringComparison.Ordinal)))
        {
            return FailedFinal(
                "wound_plan_effect_result_disposition_mismatch",
                "Every typed wound terminal operation must expire its exact effect identity.",
                "expired",
                string.Join(",", terminations.Select(static value => value.Disposition)));
        }

        var derived = DeriveEffectResults(prepared, accepted.EffectPlan);
        if (derived.Issues.Count != 0)
            return new WoundAcceptedTurnPlanningResult(null, derived.Issues);
        if (!ApplicationsAgree(expectedApplications, applications, derived.Applications))
        {
            return FailedFinal(
                "wound_plan_effect_result_agreement_mismatch",
                "A carried wound application result disagrees with its prepared root or detached after-images.",
                "exact application, event, source, target, carrier, and materialization agreement",
                "well-formed but divergent application result");
        }
        if (!TerminationsAgree(
                expectedTerminations,
                terminations,
                derived.Terminations))
        {
            return FailedFinal(
                "wound_plan_effect_result_agreement_mismatch",
                "A carried wound termination result disagrees with its prepared operation or detached before/after evidence.",
                "exact operation, terminal transition, event, source, target, and carrier agreement",
                "well-formed but divergent termination result");
        }

        return ComposeFinalPlan(prepared, accepted, applications);
    }

    private static ValidationIssue? ValidateAcceptedResultShape(
        WoundEffectBatchAcceptedPlan accepted)
    {
        try
        {
            if (accepted.EffectInput is null ||
                accepted.EffectPlan is null ||
                accepted.ApplicationResults is null ||
                accepted.TerminationResults is null ||
                !Fingerprint(accepted.WoundPreparationFingerprint) ||
                !Fingerprint(accepted.EffectInputFingerprint) ||
                !Fingerprint(accepted.EffectAcceptedTurnPlanFingerprint) ||
                accepted.ApplicationResults.Any(static value => value is null) ||
                accepted.TerminationResults.Any(static value => value is null))
            {
                return InvalidHandoffIssue("missing or malformed accepted effect-batch member");
            }

            foreach (var application in accepted.ApplicationResults)
            {
                if (!Exact(application.ApplicationRef) ||
                    !Exact(application.Disposition) ||
                    !Exact(application.EffectId) ||
                    !Exact(application.CreateTransitionId) ||
                    !Exact(application.CreatedEventRef) ||
                    !Exact(application.CausalEventRef) ||
                    application.SourceKey is null ||
                    application.TargetKey is null ||
                    application.CarrierCoordinate is null ||
                    !SourceKeyShapeIsValid(application.SourceKey) ||
                    !TargetKeyShapeIsValid(application.TargetKey) ||
                    !CarrierCoordinateShapeIsValid(application.CarrierCoordinate) ||
                    application.Materialization is null ||
                    application.Materialization.SlotBindings is null ||
                    application.Materialization.SlotBindings.Any(static value => value is null) ||
                    application.Materialization.ComponentCount is < 1 or >
                        WoundConsequenceEnvelopeCatalog.MaximumComponentsPerEffect ||
                    !Fingerprint(application.Materialization.MaterializationFingerprint) ||
                    application.Materialization.SlotBindings.Any(static slot =>
                        slot.Slot is < 1 or > WoundMaterializationContract.MaxConsequences ||
                        !Exact(slot.ProfileKey) ||
                        string.IsNullOrWhiteSpace(slot.ReadableSummary)))
                {
                    return InvalidHandoffIssue("malformed nested application materialization");
                }
            }

            foreach (var termination in accepted.TerminationResults)
            {
                if (!Exact(termination.OperationRef) ||
                    !Exact(termination.Disposition) ||
                    !Exact(termination.EffectId) ||
                    !Exact(termination.TerminalTransitionId) ||
                    !Exact(termination.TransitionEventRef) ||
                    !Exact(termination.CausalEventRef) ||
                    termination.SourceKey is null ||
                    termination.TargetKey is null ||
                    termination.CarrierCoordinate is null ||
                    !SourceKeyShapeIsValid(termination.SourceKey) ||
                    !TargetKeyShapeIsValid(termination.TargetKey) ||
                    !CarrierCoordinateShapeIsValid(termination.CarrierCoordinate))
                {
                    return InvalidHandoffIssue("malformed nested termination result");
                }
            }

            return null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return InvalidHandoffIssue(exception.GetType().Name);
        }
    }

    private static ValidationIssue InvalidHandoffIssue(string actual) =>
        NewIssue(
            "wound_plan_effect_handoff_invalid",
            "The accepted wound effect handoff contains malformed nested data.",
            "complete exact effect results with bounded canonical materialization",
            actual);

    private static bool SourceKeyShapeIsValid(EffectSourceKey value) =>
        Exact(value.Realm) &&
        Exact(value.Kind) &&
        Exact(value.SourceId) &&
        Exact(value.DefinitionKey);

    private static bool TargetKeyShapeIsValid(EffectTargetKey value) =>
        Exact(value.Realm) && Exact(value.Kind) && Exact(value.TargetId);

    private static bool CarrierCoordinateShapeIsValid(
        EffectCarrierCoordinate value) =>
        Exact(value.Kind) &&
        Exact(value.OwnerId) &&
        Exact(value.Path) &&
        (value.Category is null || Exact(value.Category));

    private static bool ApplicationsAgree(
        IReadOnlyList<WoundRootEffectApplication> expected,
        IReadOnlyList<EffectAcceptedApplicationResult> carried,
        IReadOnlyList<EffectAcceptedApplicationResult> derived)
    {
        if (expected.Count != carried.Count || carried.Count != derived.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
        {
            var root = expected[index];
            var value = carried[index];
            var actual = derived[index];
            var expectedEvent = WoundEffectOperationEventRef.Create(
                root.CausalEventRef,
                root.MechanicsOrdinal,
                root.OperationOrdinal,
                root.OperationKind);
            if (!string.Equals(value.ApplicationRef, root.ApplicationRef, StringComparison.Ordinal) ||
                !string.Equals(value.CreatedEventRef, expectedEvent, StringComparison.Ordinal) ||
                !string.Equals(value.CausalEventRef, root.CausalEventRef, StringComparison.Ordinal) ||
                value.SourceKey != root.ExpectedSourceKey ||
                value.TargetKey != root.ExpectedTargetKey ||
                value.CarrierCoordinate != root.ExpectedCarrierCoordinate ||
                value.Materialization.ComponentCount != root.ExpectedComponentCount ||
                !string.Equals(
                    value.Materialization.MaterializationFingerprint,
                    root.ExpectedMaterializationFingerprint,
                    StringComparison.Ordinal) ||
                !SlotSemanticsEqual(
                    value.Materialization.SlotBindings,
                    root.SlotBindings) ||
                !ApplicationResultsEqual(value, actual))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TerminationsAgree(
        IReadOnlyList<WoundTerminalEffectOperation> expected,
        IReadOnlyList<EffectAcceptedTerminationResult> carried,
        IReadOnlyList<EffectAcceptedTerminationResult> derived)
    {
        if (expected.Count != carried.Count || carried.Count != derived.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
        {
            var operation = expected[index];
            var value = carried[index];
            if (!string.Equals(
                    value.OperationRef,
                    operation.OperationRef,
                    StringComparison.Ordinal) ||
                !string.Equals(value.Disposition, "expired", StringComparison.Ordinal) ||
                !string.Equals(
                    value.EffectId,
                    operation.EffectId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.TransitionEventRef,
                    operation.OperationRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.CausalEventRef,
                    operation.CausalEventRef,
                    StringComparison.Ordinal) ||
                value.SourceKey != operation.ExpectedSourceKey ||
                value.TargetKey != operation.ExpectedTargetKey ||
                value.CarrierCoordinate != operation.ExpectedCarrierCoordinate ||
                value != derived[index])
            {
                return false;
            }
        }
        return true;
    }

    private static bool ApplicationResultsEqual(
        EffectAcceptedApplicationResult first,
        EffectAcceptedApplicationResult second) =>
        string.Equals(first.ApplicationRef, second.ApplicationRef, StringComparison.Ordinal) &&
        string.Equals(first.Disposition, second.Disposition, StringComparison.Ordinal) &&
        string.Equals(first.EffectId, second.EffectId, StringComparison.Ordinal) &&
        string.Equals(first.CreateTransitionId, second.CreateTransitionId, StringComparison.Ordinal) &&
        string.Equals(first.CreatedEventRef, second.CreatedEventRef, StringComparison.Ordinal) &&
        string.Equals(first.CausalEventRef, second.CausalEventRef, StringComparison.Ordinal) &&
        first.SourceKey == second.SourceKey &&
        first.TargetKey == second.TargetKey &&
        first.CarrierCoordinate == second.CarrierCoordinate &&
        first.Materialization.ComponentCount == second.Materialization.ComponentCount &&
        string.Equals(
            first.Materialization.MaterializationFingerprint,
            second.Materialization.MaterializationFingerprint,
            StringComparison.Ordinal) &&
        SlotAgreementsEqual(
            first.Materialization.SlotBindings,
            second.Materialization.SlotBindings);

    private static bool SlotAgreementsEqual(
        IReadOnlyList<WoundEffectSlotAgreement> first,
        IReadOnlyList<WoundEffectSlotAgreement> second) =>
        first.Count == second.Count && first.SequenceEqual(second);

    private static bool SlotSemanticsEqual(
        IReadOnlyList<WoundEffectSlotAgreement> canonical,
        IReadOnlyList<WoundEffectSlotAgreement> provisional)
    {
        if (canonical.Count != provisional.Count)
            return false;
        for (var index = 0; index < canonical.Count; index++)
        {
            if (!string.Equals(
                    canonical[index].ProfileKey,
                    provisional[index].ProfileKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    canonical[index].ReadableSummary,
                    provisional[index].ReadableSummary,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static WoundAcceptedTurnPlanningResult ComposeFinalPlan(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan accepted,
        IReadOnlyList<EffectAcceptedApplicationResult> applications)
    {
        if (prepared.TreatmentContinuationAuthority is not null)
            return ComposeTreatmentContinuationFinalPlan(prepared, accepted);

        try
        {
            var baseline = prepared.BaselineAuthority;
            var carrierCatalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identityBefore = WoundIdentityState.Parse(
                baseline.PreTurnIdentityIndex.ToJsonString(),
                WoundIdentityState.StatePath);
            var historyBefore = WoundHistoryState.Parse(
                baseline.PreTurnHistory.ToJsonString(),
                WoundHistoryState.HistoryPath);
            if (carrierCatalog.Issues.Count != 0 ||
                identityBefore.State is null || identityBefore.Issues.Count != 0 ||
                historyBefore.State is null || historyBefore.Issues.Count != 0 ||
                historyBefore.State.ValidateAgreement(
                    identityBefore.State,
                    carrierCatalog).Count != 0)
            {
                return FailedFinal(
                    "wound_plan_prepared_seal_mismatch",
                    "The sealed pre-turn wound baseline is not internally valid.",
                    "valid agreeing carrier, identity, and history baseline",
                    "invalid sealed baseline");
            }

            var applicationByRef = applications.ToDictionary(
                static value => value.ApplicationRef,
                StringComparer.Ordinal);
            var finalTransitions = new List<FinalizedWoundTransition>(
                prepared.PreparedWounds.Count);
            var intents = new List<WoundTransitionIntent>();
            var historyRows = new List<WoundHistoryTransition>();
            for (var index = 0; index < prepared.PreparedWounds.Count; index++)
            {
                var preparedWound = prepared.PreparedWounds[index];
                var batch = prepared.EffectOperationBatches[index];
                var finalized = BuildFinalWound(preparedWound, batch, applicationByRef);
                if (finalized.Wound is null || finalized.Issues.Count != 0)
                    return new WoundAcceptedTurnPlanningResult(null, finalized.Issues);

                var authority = batch.TransitionAuthority;
                WoundMaterializationEnvelope? beforeWound = null;
                if (string.Equals(
                        authority.TransitionKind,
                        "worsen",
                        StringComparison.Ordinal))
                {
                    var matches = carrierCatalog.Occurrences.Where(value =>
                            string.Equals(
                                value.WoundId,
                                finalized.Wound.WoundId,
                                StringComparison.Ordinal))
                        .ToArray();
                    if (matches.Length != 1 ||
                        !string.Equals(
                            WoundIdentityState.ComputeSemanticFingerprint(
                                matches[0].Wound),
                            authority.ExpectedBeforeFingerprint,
                            StringComparison.Ordinal) ||
                        !identityBefore.State.TryGetEntry(
                            finalized.Wound.WoundId,
                            out var identityEntry) ||
                        WoundIdentityState.ValidateActiveAgreement(
                            identityEntry,
                            matches[0].Wound,
                            PlanPath + ".beforeWound").Count != 0)
                    {
                        return FailedFinal(
                            "wound_plan_prepared_seal_mismatch",
                            "The worsening transition no longer resolves to its exact sealed active wound.",
                            authority.ExpectedBeforeFingerprint ??
                                "sealed active wound fingerprint",
                            matches.Length == 1
                                ? WoundIdentityState.ComputeSemanticFingerprint(
                                    matches[0].Wound)
                                : $"matches={matches.Length}");
                    }
                    beforeWound = matches[0].Wound;
                }
                else if (!string.Equals(
                             authority.TransitionKind,
                             "create",
                             StringComparison.Ordinal) ||
                         carrierCatalog.Occurrences.Any(value => string.Equals(
                             value.WoundId,
                             finalized.Wound.WoundId,
                             StringComparison.Ordinal)))
                {
                    return FailedFinal(
                        "wound_plan_prepared_seal_mismatch",
                        "The prepared transition kind does not agree with its sealed before-state.",
                        "create with no prior wound or worsen with one exact prior wound",
                        authority.TransitionKind);
                }

                var beforeFingerprint = beforeWound is null
                    ? WoundHistoryState.ComputeNonexistentBeforeFingerprint(
                        finalized.Wound.WoundId)
                    : WoundIdentityState.ComputeSemanticFingerprint(beforeWound);
                var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                    finalized.Wound);
                WoundTransitionEvidence evidence = beforeWound is null
                    ? new WoundCreateEvidence(
                        authority.OpportunityAuthorityFingerprint,
                        beforeFingerprint,
                        afterFingerprint,
                        authority.OpportunityId,
                        authority.MaximumSeverityRank,
                        finalized.Wound.Owner,
                        finalized.Wound.Classification.Domain)
                    : new WoundWorseningEvidence(
                        authority.OpportunityAuthorityFingerprint,
                        beforeFingerprint,
                        afterFingerprint,
                        authority.CauseKind!,
                        authority.MaximumSeverityRank,
                        beforeWound.Care.ActiveCourseId is not null ||
                        string.Equals(
                            beforeWound.Care.State,
                            "recovering",
                            StringComparison.Ordinal));
                var reduction = WoundTransitionReducer.Reduce(new WoundTransitionRequest(
                    authority.TransitionKind,
                    finalized.Wound.LastTransition.TransitionId,
                    authority.OperationKey,
                    batch.SourceExport.CausalEventRef,
                    prepared.Binding.Turn,
                    beforeWound,
                    finalized.Wound,
                    evidence));
                if (!reduction.IsValid)
                {
                    return new WoundAcceptedTurnPlanningResult(
                        null,
                        reduction.Issues.Select(CloneIssue).ToArray());
                }

                finalTransitions.Add(new FinalizedWoundTransition(
                    beforeWound,
                    reduction.ProposedAfter!));
                intents.AddRange(reduction.Intents);
                var historyIntent = reduction.Intents
                    .OfType<WoundTransitionHistoryIntent>()
                    .Single();
                historyRows.Add(new WoundHistoryTransition(
                    historyIntent.TransitionId,
                    historyIntent.WoundId,
                    historyBefore.State.NextOrdinal + historyRows.Count,
                    reduction.ProposedAfter!.LastTransition.Ordinal,
                    historyIntent.Kind,
                    historyIntent.Turn,
                    historyIntent.EventRef,
                    historyIntent.OperationKey,
                    historyIntent.BeforeFingerprint,
                    historyIntent.AfterFingerprint,
                    authority.OpportunityAuthorityFingerprint,
                    historyIntent.AttemptId,
                    reduction.ProposedAfter.Care.ActiveCourseId,
                    null,
                    historyIntent.TickKey,
                    null,
                    WoundHistoryState.ComputeOutputFingerprint(
                        historyIntent.OperationKey,
                        historyIntent.EventRef,
                        authority.ReadableSummary),
                    authority.ReadableSummary,
                    historyIntent.Terminal));
            }

            var contributions = BuildCarrierContributions(
                baseline.PreTurnCarriers,
                finalTransitions);
            var identityAfter = BuildIdentityAfterImage(
                baseline.PreTurnIdentityIndex,
                finalTransitions);
            if (identityAfter.State is null || identityAfter.Issues.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    identityAfter.Issues.Select(CloneIssue).ToArray());
            }
            var identityAfterJson = JsonNode.Parse(
                WoundIdentityState.SerializeCanonical(identityAfter.State))!
                .AsObject();

            var historyAfter = WoundHistoryState.CreateValidated(
                historyBefore.State.NextOrdinal + historyRows.Count,
                historyBefore.State.Transitions.Concat(historyRows));
            if (historyAfter.State is null || historyAfter.Issues.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    historyAfter.Issues.Select(CloneIssue).ToArray());
            }
            var historyAfterJson = JsonNode.Parse(
                WoundHistoryState.SerializeCanonical(historyAfter.State))!
                .AsObject();

            var carriersAfter = ApplyFinalWounds(
                baseline.PreTurnCarriers,
                finalTransitions);
            var carrierAfterCatalog = WoundCarrierCatalog.Build(carriersAfter);
            var afterAgreement = historyAfter.State.ValidateAgreement(
                identityAfter.State,
                carrierAfterCatalog);
            if (carrierAfterCatalog.Issues.Count != 0 || afterAgreement.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    carrierAfterCatalog.Issues.Concat(afterAgreement)
                        .Select(CloneIssue)
                        .ToArray());
            }

            var finalFingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(
                prepared,
                accepted,
                contributions,
                identityAfterJson,
                historyAfterJson,
                intents);
            var plan = new WoundAcceptedTurnPlan(
                prepared.Binding,
                prepared.BindingFingerprint,
                prepared.InputFingerprint,
                accepted.WoundPreparationFingerprint,
                accepted.EffectInputFingerprint,
                accepted.EffectAcceptedTurnPlanFingerprint,
                finalFingerprint,
                prepared.AllocatedWoundIds,
                prepared.AllocatedTransitionIds,
                contributions,
                identityAfterJson,
                historyAfterJson,
                intents);
            return new WoundAcceptedTurnPlanningResult(
                plan,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return FailedFinal(
                "wound_plan_effect_handoff_invalid",
                "The detached wound/effect payload could not be finalized atomically.",
                "complete agreeing prepared/effect payload",
                exception.GetType().Name);
        }
    }

    private static WoundAcceptedTurnPlanningResult
        ComposeTreatmentContinuationFinalPlan(
            WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchAcceptedPlan accepted)
    {
        var stage = "authority_seal";
        try
        {
            if (!WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                    prepared.TreatmentContinuationAuthority,
                    out var continuation) ||
                !WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(
                    prepared))
            {
                return FailedFinal(
                    "wound_plan_treatment_continuation_invalid",
                    "The private treatment continuation must remain sealed through finalization.",
                    "one exact treatment continuation authority",
                    "missing or changed authority");
            }

            stage = "baseline_agreement";
            var baseline = prepared.BaselineAuthority;
            var carrierCatalog = WoundCarrierCatalog.Build(baseline.PreTurnCarriers);
            var identityBefore = WoundIdentityState.Parse(
                baseline.PreTurnIdentityIndex.ToJsonString(),
                WoundIdentityState.StatePath);
            var historyBefore = WoundHistoryState.Parse(
                baseline.PreTurnHistory.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var matches = carrierCatalog.Occurrences.Where(value => string.Equals(
                    value.WoundId,
                    continuation.Before.WoundId,
                    StringComparison.Ordinal))
                .ToArray();
            if (carrierCatalog.Issues.Count != 0 ||
                identityBefore.State is null || identityBefore.Issues.Count != 0 ||
                historyBefore.State is null || historyBefore.Issues.Count != 0 ||
                historyBefore.State.ValidateAgreement(
                    identityBefore.State,
                    carrierCatalog).Count != 0 ||
                matches.Length != 1 ||
                !string.Equals(
                    WoundIdentityState.ComputeSemanticFingerprint(matches[0].Wound),
                    WoundIdentityState.ComputeSemanticFingerprint(
                        continuation.Before),
                    StringComparison.Ordinal))
            {
                return FailedFinal(
                    "wound_plan_prepared_seal_mismatch",
                    "The treatment continuation baseline must contain its exact active wound, identity, and history.",
                    continuation.Before.WoundId,
                    $"matches={matches.Length}");
            }

            stage = "transition_reduction";
            var before = matches[0].Wound;
            var after = continuation.After;
            var coordinates = continuation.Resolution.Coordinates;
            var outcome = continuation.DeclaredOutcome;
            var reduction = WoundTransitionReducer.Reduce(
                new WoundTransitionRequest(
                    "treat",
                    continuation.TransitionId,
                    coordinates.OperationKey,
                    coordinates.EventRef,
                    coordinates.Turn,
                    before,
                    after,
                    new WoundTreatmentEvidence(
                        continuation.Resolution.ResolutionAuthorityFingerprint,
                        WoundIdentityState.ComputeSemanticFingerprint(before),
                        WoundIdentityState.ComputeSemanticFingerprint(after),
                        coordinates.RouteId,
                        coordinates.AttemptId,
                        outcome)));
            if (!reduction.IsValid)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    reduction.Issues.Select(CloneIssue).ToArray());
            }

            stage = "history_intent";
            var proposed = reduction.ProposedAfter!;
            var transitions = new[]
            {
                new FinalizedWoundTransition(before, proposed)
            };
            var intents = reduction.Intents.ToArray();
            var historyIntent = intents.OfType<WoundTransitionHistoryIntent>()
                .Single();
            var historyRow = new WoundHistoryTransition(
                historyIntent.TransitionId,
                historyIntent.WoundId,
                historyBefore.State.NextOrdinal,
                proposed.LastTransition.Ordinal,
                historyIntent.Kind,
                historyIntent.Turn,
                historyIntent.EventRef,
                historyIntent.OperationKey,
                historyIntent.BeforeFingerprint,
                historyIntent.AfterFingerprint,
                continuation.Resolution.RequestFingerprint,
                historyIntent.AttemptId,
                CourseId: null,
                CourseMilestoneOrdinal: null,
                CycleKey: null,
                PaymentFingerprint: null,
                WoundHistoryState.ComputeOutputFingerprint(
                    historyIntent.OperationKey,
                    historyIntent.EventRef,
                    WoundAcceptedTurnPlanner.TreatmentPublicationSummary),
                WoundAcceptedTurnPlanner.TreatmentPublicationSummary,
                Terminal: false,
                TreatmentResult: continuation.PersistedResult);
            stage = "carrier_contributions";
            var contributions = BuildCarrierContributions(
                baseline.PreTurnCarriers,
                transitions);
            stage = "identity_after_image";
            var identityAfter = BuildIdentityAfterImage(
                baseline.PreTurnIdentityIndex,
                transitions);
            if (identityAfter.State is null || identityAfter.Issues.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    identityAfter.Issues.Select(CloneIssue).ToArray());
            }
            var identityAfterJson = JsonNode.Parse(
                WoundIdentityState.SerializeCanonical(identityAfter.State))!
                .AsObject();
            stage = "history_after_image";
            var historyAfter = WoundHistoryState.CreateValidated(
                historyBefore.State.NextOrdinal + 1,
                historyBefore.State.Transitions.Append(historyRow));
            if (historyAfter.State is null || historyAfter.Issues.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    historyAfter.Issues.Select(CloneIssue).ToArray());
            }
            var historyAfterJson = JsonNode.Parse(
                WoundHistoryState.SerializeCanonical(historyAfter.State))!
                .AsObject();
            stage = "apply_final_wounds";
            var carriersAfter = ApplyFinalWounds(
                baseline.PreTurnCarriers,
                transitions);
            stage = "carrier_after_catalog";
            var carrierAfterCatalog = WoundCarrierCatalog.Build(carriersAfter);
            stage = "history_carrier_agreement";
            var agreement = historyAfter.State.ValidateAgreement(
                identityAfter.State,
                carrierAfterCatalog);
            if (carrierAfterCatalog.Issues.Count != 0 || agreement.Count != 0)
            {
                return new WoundAcceptedTurnPlanningResult(
                    null,
                    carrierAfterCatalog.Issues.Concat(agreement)
                        .Select(CloneIssue)
                        .ToArray());
            }

            stage = "final_fingerprint";
            var finalFingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(
                prepared,
                accepted,
                contributions,
                identityAfterJson,
                historyAfterJson,
                intents);
            return new WoundAcceptedTurnPlanningResult(
                new WoundAcceptedTurnPlan(
                    prepared.Binding,
                    prepared.BindingFingerprint,
                    prepared.InputFingerprint,
                    accepted.WoundPreparationFingerprint,
                    accepted.EffectInputFingerprint,
                    accepted.EffectAcceptedTurnPlanFingerprint,
                    finalFingerprint,
                    prepared.AllocatedWoundIds,
                    prepared.AllocatedTransitionIds,
                    contributions,
                    identityAfterJson,
                    historyAfterJson,
                    intents),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException or OverflowException)
        {
            return FailedFinal(
                "wound_plan_treatment_continuation_invalid",
                "The treatment continuation could not be finalized atomically.",
                "one complete sealed treatment continuation",
                stage + ":" + exception.GetType().Name + ":" + exception.Message);
        }
    }

    private sealed record FinalWoundResult(
        WoundMaterializationEnvelope? Wound,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed record FinalizedWoundTransition(
        WoundMaterializationEnvelope? Before,
        WoundMaterializationEnvelope After);

    private static FinalWoundResult BuildFinalWound(
        WoundMaterializationEnvelope preparedWound,
        WoundEffectOperationBatch batch,
        IReadOnlyDictionary<string, EffectAcceptedApplicationResult> applicationByRef)
    {
        var root = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(preparedWound))!
            .AsObject();
        var consequences = root["consequences"]!.AsObject();
        var definitions = new JsonArray(batch.SourceExport.Definitions.Select(static value =>
            (JsonNode)value.Definition).ToArray());
        var rootBindings = new JsonArray();
        var entries = new List<JsonObject>();
        foreach (var application in batch.RootApplications)
        {
            if (!applicationByRef.TryGetValue(application.ApplicationRef, out var result))
            {
                return new FinalWoundResult(
                    null,
                    new[]
                    {
                        NewIssue(
                            "wound_plan_effect_result_set_mismatch",
                            "A finalized wound root has no accepted effect result.",
                            application.ApplicationRef,
                            "missing")
                    });
            }
            rootBindings.Add(new JsonObject
            {
                ["effectId"] = result.EffectId,
                ["definitionKey"] = application.DefinitionKey
            });
            foreach (var slot in result.Materialization.SlotBindings)
            {
                entries.Add(new JsonObject
                {
                    ["slot"] = slot.Slot,
                    ["profileKey"] = slot.ProfileKey,
                    ["effectId"] = result.EffectId,
                    ["readableSummary"] = slot.ReadableSummary
                });
            }
        }
        consequences["slotsUsed"] = entries.Count;
        consequences["entries"] = new JsonArray(entries
            .OrderBy(static entry => entry["slot"]!.GetValue<int>())
            .Select(static entry => (JsonNode)entry)
            .ToArray());
        consequences["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = definitions,
            ["rootBindings"] = rootBindings
        };

        if (root["complications"] is JsonArray complications)
        {
            foreach (var application in batch.RootApplications)
            {
                if (!string.Equals(
                        application.OwnershipDomain.Kind,
                        "complication",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                var complication = complications.OfType<JsonObject>().SingleOrDefault(value =>
                    string.Equals(
                        value["complicationId"]?.GetValue<string>(),
                        application.OwnershipDomain.ComplicationId,
                        StringComparison.Ordinal));
                if (complication?["ownedEffectIds"] is JsonArray owned)
                    owned.Add(applicationByRef[application.ApplicationRef].EffectId);
            }
        }

        var parsed = WoundMaterializationContract.Parse(
            root.ToJsonString(),
            PlanPath + ".finalizedWound");
        return parsed.IsValid
            ? new FinalWoundResult(parsed.Wound, Array.Empty<ValidationIssue>())
            : new FinalWoundResult(
                null,
                new[]
                {
                    NewIssue(
                        "wound_plan_effect_result_agreement_mismatch",
                        "The exact accepted effect set did not form a canonical finalized wound graph.",
                        "canonical wound-owned definitions, roots, and slots",
                        string.Join(",", parsed.Issues.Select(static issue => issue.Code)))
                });
    }

    private static IReadOnlyList<WoundCarrierContribution> BuildCarrierContributions(
        WoundCarrierCatalogInput baseline,
        IReadOnlyList<FinalizedWoundTransition> transitions)
    {
        var result = new List<WoundCarrierContribution>();
        foreach (var group in transitions.GroupBy(
                     static transition => transition.After.Owner))
        {
            var owner = group.Key;
            result.Add(new WoundCarrierContribution(
                owner,
                ComputeWoundCollectionFingerprint(baseline, owner),
                group.Select(static transition => new WoundCarrierMutation(
                    transition.Before is null ? "add" : "update",
                    transition.After.WoundId,
                    transition.Before,
                    transition.After)).ToArray()));
        }
        return result;
    }

    private static WoundIdentityParseResult BuildIdentityAfterImage(
        JsonObject baseline,
        IReadOnlyList<FinalizedWoundTransition> transitions)
    {
        var candidate = baseline.DeepClone().AsObject();
        if (candidate["entries"] is not JsonArray entries)
        {
            return WoundIdentityState.Parse(
                candidate.ToJsonString(),
                WoundIdentityState.StatePath);
        }
        foreach (var transition in transitions)
        {
            var wound = transition.After;
            var afterEntry = new JsonObject
            {
                ["woundId"] = wound.WoundId,
                ["realm"] = wound.Owner.Realm,
                ["ownerKind"] = wound.Owner.OwnerKind,
                ["ownerId"] = wound.Owner.OwnerId,
                ["carrierPath"] = wound.Owner.CarrierPath,
                ["domain"] = wound.Classification.Domain,
                ["status"] = "active",
                ["createdAtTurn"] = wound.Origin.CreatedAtTurn,
                ["createdEventRef"] = wound.Origin.EventRef,
                ["lastTransitionOrdinal"] = wound.LastTransition.Ordinal,
                ["terminalTransitionId"] = null,
                ["semanticFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(wound)
            };
            if (transition.Before is null)
            {
                entries.Add(afterEntry);
                continue;
            }

            var matches = entries
                .Select((node, index) => (node, index))
                .Where(value => value.node is JsonObject entry &&
                    string.Equals(
                        entry["woundId"]?.GetValue<string>(),
                        wound.WoundId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                return new WoundIdentityParseResult(
                    null,
                    new[]
                    {
                        NewIssue(
                            "wound_plan_prepared_seal_mismatch",
                            "The worsening identity before-image is absent or ambiguous.",
                            "one exact active wound identity row",
                            $"{wound.WoundId}/matches={matches.Length}")
                    });
            }
            entries[matches[0].index] = afterEntry;
        }
        return WoundIdentityState.Parse(
            candidate.ToJsonString(),
            WoundIdentityState.StatePath);
    }

    private static string ComputeWoundCollectionFingerprint(
        WoundCarrierCatalogInput baseline,
        WoundOwnerCoordinate owner) =>
        WoundCarrierCollectionAuthority.ComputeFingerprint(baseline, owner);

    private static WoundCarrierCatalogInput ApplyFinalWounds(
        WoundCarrierCatalogInput baseline,
        IReadOnlyList<FinalizedWoundTransition> transitions)
    {
        var result = new WoundCarrierCatalogInput(
            baseline.PlayerWounds?.DeepClone().AsObject(),
            baseline.NpcWounds?.DeepClone().AsObject(),
            baseline.EnemyCombatants?.DeepClone().AsObject(),
            baseline.AllyCombatants?.DeepClone().AsObject(),
            baseline.AfterlifeProfiles?.DeepClone().AsObject());
        foreach (var transition in transitions)
        {
            var wound = transition.After;
            var collection = WoundCarrierCollectionAuthority.Resolve(
                result,
                wound.Owner);
            var afterNode = JsonNode.Parse(
                WoundMaterializationContract.SerializeCanonical(wound));
            if (transition.Before is null)
            {
                collection.Add(afterNode);
                continue;
            }

            var matches = collection
                .Select((node, index) => (node, index))
                .Where(value =>
                    WoundCarrierCollectionAuthority.MatchesSemanticBeforeImage(
                        value.node,
                        transition.Before,
                        wound.Owner,
                        PlanPath + ".finalizedWoundBefore"))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "The sealed wound before-image is absent or ambiguous.");
            }
            collection[matches[0].index] = afterNode;
        }
        return result;
    }

    private static WoundAcceptedTurnPlanningResult FailedFinal(
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            null,
            new[] { NewIssue(code, message, expected, actual) });

    internal static ValidatedInput ValidateInput(WoundAcceptedTurnInput input)
    {
        var issues = new List<ValidationIssue>();
        if (input.Binding is null ||
            input.Opportunities is null ||
            input.Transitions is null ||
            input.PreTurnCarriers is null ||
            input.PreTurnIdentityIndex is null ||
            input.PreTurnHistory is null)
        {
            issues.Add(NewIssue(
                "wound_plan_input_invalid",
                "The typed wound input contains a missing required value.",
                "complete non-null binding, collections, and pre-turn snapshots",
                "null top-level member"));
            return new ValidatedInput(
                Array.Empty<ValidatedTransition>(),
                null,
                null,
                null,
                null,
                issues.ToArray());
        }

        ValidateBinding(
            input.Binding,
            allowEmptyEvents:
                input.Opportunities.Count == 0 && input.Transitions.Count == 0,
            issues: issues);
        if (input.Transitions.Count > MaximumTransitions)
        {
            issues.Add(NewIssue(
                "wound_plan_transition_limit_exceeded",
                "The accepted wound turn exceeds its bounded transition count.",
                $"at most {MaximumTransitions} transitions",
                input.Transitions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var hasNullNested = input.Opportunities.Any(static value =>
                                value is null || value.Owner is null) ||
                            input.Transitions.Any(static value => value is null) ||
                            input.Transitions.Any(static transition =>
                                transition is not null &&
                                (transition.EffectDefinitions is null ||
                                 transition.RootApplications is null ||
                                 transition.SlotBindings is null ||
                                 transition.ProposedAfter is null ||
                                 HasIncompleteEnvelope(transition.ProposedAfter) ||
                                 transition.EffectDefinitions.Any(static value => value is null) ||
                                 transition.RootApplications.Any(static value => value is null) ||
                                 transition.SlotBindings.Any(static value => value is null)));
        if (hasNullNested)
        {
            issues.Add(NewIssue(
                "wound_plan_input_invalid",
                "The typed wound input contains a null nested candidate.",
                "non-null opportunities, transitions, definitions, roots, and slots",
                "null nested member"));
        }

        if (issues.Any(static issue =>
                issue.Code is "wound_plan_input_invalid" or
                    "wound_plan_transition_limit_exceeded"))
        {
            return new ValidatedInput(
                Array.Empty<ValidatedTransition>(),
                null,
                null,
                null,
                null,
                issues.ToArray());
        }

        ValidateOpportunityBijection(input, issues);
        var validOpportunityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var opportunity in input.Opportunities)
        {
            if (OpportunityShapeIsValid(opportunity) &&
                OpportunityEventBindingIsValid(input.Binding, opportunity))
            {
                validOpportunityIds.Add(opportunity.OpportunityId);
                continue;
            }
            issues.Add(NewIssue(
                "wound_plan_binding_invalid",
                "A wound opportunity authority is malformed.",
                "exact opportunity identity, source, fingerprint, owner, and severity bounds",
                opportunity?.OpportunityId ?? "null"));
        }

        WoundCarrierCatalog? carrierCatalog = null;
        WoundIdentityState? identityState = null;
        WoundHistoryState? historyState = null;
        EffectIdentityState? effectIdentityState = null;
        try
        {
            carrierCatalog = WoundCarrierCatalog.Build(input.PreTurnCarriers);
            issues.AddRange(carrierCatalog.Issues.Select(CloneIssue));
            var identity = WoundIdentityState.Parse(
                input.PreTurnIdentityIndex.ToJsonString(),
                WoundIdentityState.StatePath);
            issues.AddRange(identity.Issues.Select(CloneIssue));
            identityState = identity.State;
            var history = WoundHistoryState.Parse(
                input.PreTurnHistory.ToJsonString(),
                WoundHistoryState.HistoryPath);
            issues.AddRange(history.Issues.Select(CloneIssue));
            historyState = history.State;
            if (carrierCatalog.Issues.Count == 0 &&
                identityState is not null &&
                historyState is not null)
            {
                issues.AddRange(historyState.ValidateAgreement(
                    identityState,
                    carrierCatalog).Select(CloneIssue));
            }
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            issues.Add(NewIssue(
                "wound_plan_input_invalid",
                "A pre-turn wound snapshot could not be read as detached canonical data.",
                "valid carrier, identity, and history snapshots",
                exception.GetType().Name));
        }

        if (input.Opportunities.Any(static value =>
                value.WorseningTarget is not null))
        {
            if (input.PreTurnEffectCarriers is null ||
                input.PreTurnEffectIdentityIndex is null)
            {
                issues.Add(NewIssue(
                    "wound_plan_effect_baseline_missing",
                    "Wound worsening requires the exact pre-turn effect carrier and identity baselines.",
                    "canonical effect carriers and effect identity index",
                    "missing effect baseline"));
            }
            else
            {
                try
                {
                    var effectCatalog = EffectCarrierCatalog.Build(
                        input.PreTurnEffectCarriers);
                    issues.AddRange(effectCatalog.Issues.Select(CloneIssue));
                    using var document = JsonDocument.Parse(
                        input.PreTurnEffectIdentityIndex.ToJsonString());
                    var effectIdentity = EffectIdentityState.Parse(
                        document.RootElement,
                        EffectIdentityState.StatePath);
                    issues.AddRange(effectIdentity.Issues.Select(CloneIssue));
                    effectIdentityState = effectIdentity.State;
                }
                catch (Exception exception) when (
                    exception is JsonException or InvalidOperationException or
                        ArgumentException)
                {
                    issues.Add(NewIssue(
                        "wound_plan_effect_baseline_invalid",
                        "The pre-turn effect baseline for wound worsening is malformed.",
                        "canonical effect carrier and identity state",
                        exception.GetType().Name));
                }
            }
        }

        var opportunities = input.Opportunities
            .Where(value => value is not null &&
                validOpportunityIds.Contains(value.OpportunityId))
            .GroupBy(static value => value.OpportunityId, StringComparer.Ordinal)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single(), StringComparer.Ordinal);
        var acceptedEvents = input.Binding.AcceptedEvents is null
            ? new Dictionary<string, WoundAcceptedEventAuthority>(StringComparer.Ordinal)
            : input.Binding.AcceptedEvents
                .Where(static value => value is not null && Exact(value.EventRef))
                .GroupBy(static value => value.EventRef, StringComparer.Ordinal)
                .Where(static group => group.Count() == 1)
                .ToDictionary(static group => group.Key, static group => group.Single(), StringComparer.Ordinal);
        var validated = new List<ValidatedTransition>(input.Transitions.Count);
        for (var index = 0; index < input.Transitions.Count; index++)
        {
            var draft = input.Transitions[index];
            if (draft is null)
            {
                continue;
            }
            if (!Exact(draft.OpportunityId))
            {
                issues.Add(NewIssue(
                    "wound_plan_input_invalid",
                    "A wound transition has a malformed opportunity correlation.",
                    "one exact opportunityId",
                    draft.OpportunityId ?? "null",
                    $"{PlanPath}.transitions[{index}].opportunityId"));
                continue;
            }
            if (!opportunities.TryGetValue(draft.OpportunityId, out var opportunity))
                continue;
            if (!acceptedEvents.TryGetValue(opportunity.EventRef, out var acceptedEvent))
            {
                if (!issues.Any(static issue => issue.Code is
                        "wound_plan_event_authority_invalid" or
                        "wound_plan_event_authority_ambiguous"))
                {
                    issues.Add(NewIssue(
                        "wound_plan_binding_mismatch",
                        "The wound opportunity does not resolve to one accepted event authority.",
                        "one exact accepted event matching the opportunity eventRef",
                        opportunity.EventRef,
                        $"{PlanPath}.opportunities[{index}].eventRef"));
                }
                continue;
            }

            var beforeWound = ResolveWorseningBeforeWound(
                opportunity,
                carrierCatalog,
                identityState,
                index,
                issues);
            ValidateTransitionBinding(
                input.Binding,
                draft,
                opportunity,
                acceptedEvent,
                beforeWound,
                issues);
            var graph = ValidateDraftGraph(draft, index, issues);
            ValidateEffectCarrierAuthority(draft, graph, index, issues);
            if (carrierCatalog is { Issues.Count: 0 } &&
                !OwnerCarrierExists(
                    input.PreTurnCarriers,
                    draft.ProposedAfter.Owner))
            {
                issues.Add(NewIssue(
                    "wound_plan_binding_mismatch",
                    "The proposed wound owner has no exact pre-turn carrier.",
                    "one carrier at the proposed owner coordinate",
                    draft.ProposedAfter.Owner.ToString(),
                    $"{PlanPath}.transitions[{index}].proposedAfter.owner"));
            }

            validated.Add(new ValidatedTransition(
                draft,
                opportunity,
                acceptedEvent,
                new WoundAcceptedTurnIdentityScope(
                    input.Binding.SessionId,
                    input.Binding.RequestId,
                    input.Binding.SnapshotToken,
                    input.Binding.Realm,
                    input.Binding.Turn,
                    input.Binding.AcceptedEventsFingerprint,
                    opportunity.EventRef,
                    draft.OpportunityId,
                    draft.ProposedAfter.Owner,
                    draft.Kind,
                    draft.OperationKey,
                    draft.LocalWoundRef),
                graph,
                index + 1,
                beforeWound));
        }

        ValidateDraftIdentitySets(
            input,
            carrierCatalog,
            identityState,
            historyState,
            issues);
        return new ValidatedInput(
            validated,
            carrierCatalog,
            identityState,
            historyState,
            effectIdentityState,
            issues.ToArray());
    }

    private static bool HasIncompleteEnvelope(WoundMaterializationEnvelope value) =>
        value.Owner is null ||
        value.Origin is null ||
        value.Classification is null ||
        value.Classification.LocationProfile is null ||
        value.Display is null ||
        value.Display.VisibleSymptoms is null ||
        value.Severity is null ||
        value.Care is null ||
        value.Complications is null ||
        value.Complications.Any(static complication =>
            complication is null || complication.OwnedEffectIds is null) ||
        value.Consequences is null ||
        value.Consequences.Entries is null ||
        value.Consequences.Entries.Any(static entry => entry is null) ||
        value.Consequences.OwnedEffectSources is null ||
        value.Consequences.OwnedEffectSources.Definitions is null ||
        value.Consequences.OwnedEffectSources.RootBindings is null ||
        value.Consequences.OwnedEffectSources.RootBindings.Any(
            static binding => binding is null) ||
        value.Consequences.OwnedEffectSources.DefinitionFacts is null ||
        value.Consequences.OwnedEffectSources.DefinitionFacts.Any(static fact =>
            fact is null || fact.ApplyDefinitionTargets.IsDefault) ||
        value.Treatment is null ||
        value.Treatment.DiagnosisPaths is null ||
        value.Treatment.DiagnosisPaths.Any(static path =>
            path is null || path.Requirements is null || path.Reveals is null) ||
        value.Treatment.Routes is null ||
        value.Treatment.Routes.Any(static route =>
            route is null || route.Requirements is null || route.Outcomes is null) ||
        value.Treatment.KnownRouteIds is null ||
        value.Treatment.CompletedRouteIds is null ||
        value.Recovery is null ||
        value.Recovery.Blockers is null ||
        value.Relations is null ||
        value.Relations.LegacyRefs is null ||
        value.Relations.IndependentEffectRefs is null ||
        value.LastTransition is null;

    private static void ValidateBinding(
        WoundAcceptedTurnBinding binding,
        bool allowEmptyEvents,
        ICollection<ValidationIssue> issues)
    {
        if (!Exact(binding.SessionId) ||
            !Exact(binding.RequestId) ||
            !Exact(binding.SnapshotToken) ||
            !Exact(binding.Realm) ||
            binding.Realm is not ("mortal_world" or "chaos_sea" or "shining_abode") ||
            binding.Turn <= 0 ||
            binding.AcceptedEvents is null)
        {
            issues.Add(NewIssue(
                "wound_plan_binding_invalid",
                "The accepted wound binding is incomplete or outside the closed realm/turn domain.",
                "exact session, request, snapshot, realm, positive turn, and ordered events",
                "invalid binding"));
            return;
        }

        if ((!allowEmptyEvents && binding.AcceptedEvents.Count == 0) ||
            binding.AcceptedEvents.Any(static value =>
                value is null ||
                !Exact(value.EventRef) ||
                !Exact(value.Kind) ||
                !Exact(value.AuthorityId) ||
                !Fingerprint(value.SemanticFingerprint)))
        {
            issues.Add(NewIssue(
                "wound_plan_event_authority_invalid",
                "The accepted event set contains a malformed authority row.",
                allowEmptyEvents
                    ? "an empty or exact four-field accepted event authority set"
                    : "one or more exact four-field accepted event authorities",
                "empty or malformed event authority"));
            return;
        }

        if (!ExactAndConfusableUnique(binding.AcceptedEvents.Select(static value => value.EventRef)) ||
            !ExactAndConfusableUnique(binding.AcceptedEvents.Select(static value => value.AuthorityId)))
        {
            issues.Add(NewIssue(
                "wound_plan_event_authority_ambiguous",
                "Accepted event references and authority identities must be exact/confusable unique.",
                "pairwise-disjoint event and authority identities",
                "duplicate or confusable accepted event authority"));
        }

        var actual = WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents);
        if (!string.Equals(actual, binding.AcceptedEventsFingerprint, StringComparison.Ordinal))
        {
            issues.Add(NewIssue(
                "wound_plan_event_set_fingerprint_mismatch",
                "The accepted event set does not match its ordered authority seal.",
                actual,
                binding.AcceptedEventsFingerprint ?? "null"));
        }
    }

    private static void ValidateOpportunityBijection(
        WoundAcceptedTurnInput input,
        ICollection<ValidationIssue> issues)
    {
        var opportunityIds = input.Opportunities.Select(static value => value.OpportunityId).ToArray();
        var transitionOpportunityIds = input.Transitions.Select(static value => value.OpportunityId).ToArray();
        if (opportunityIds.Length != transitionOpportunityIds.Length ||
            !ExactAndConfusableUnique(opportunityIds) ||
            !ExactAndConfusableUnique(transitionOpportunityIds) ||
            opportunityIds.Any(id => transitionOpportunityIds.Count(value =>
                string.Equals(value, id, StringComparison.Ordinal)) != 1))
        {
            issues.Add(NewIssue(
                "wound_plan_opportunity_binding_mismatch",
                "Accepted transitions and opportunity authorities must form an exact bijection.",
                "one exact opportunity per transition and one transition per opportunity",
                "missing, duplicate, or confusable opportunity binding"));
        }
    }

    private static bool OpportunityShapeIsValid(WoundOpportunityAuthority value) =>
        WoundOpportunityAuthority.HasCompleteShape(value);

    private static bool OpportunityEventBindingIsValid(
        WoundAcceptedTurnBinding binding,
        WoundOpportunityAuthority opportunity)
    {
        if (binding.AcceptedEvents is null)
            return false;
        var matches = binding.AcceptedEvents.Where(value =>
            value is not null && string.Equals(
            value.EventRef,
            opportunity.EventRef,
            StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 &&
               string.Equals(
                   matches[0].Kind,
                   opportunity.EventKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   matches[0].AuthorityId,
                   opportunity.EventAuthorityId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   matches[0].SemanticFingerprint,
                   opportunity.InputEvidenceFingerprint,
                   StringComparison.Ordinal);
    }

    internal static WoundAcceptedSourceBindingResult BindAcceptedSourceTargets(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        EffectTargetAuthority effectTargets) =>
        WoundSourceAuthority.BindAcceptedOpportunities(
            binding,
            opportunities,
            effectTargets);

    private static WoundMaterializationEnvelope? ResolveWorseningBeforeWound(
        WoundOpportunityAuthority opportunity,
        WoundCarrierCatalog? carriers,
        WoundIdentityState? identities,
        int transitionIndex,
        ICollection<ValidationIssue> issues)
    {
        var target = opportunity.WorseningTarget;
        if (target is null)
            return null;

        var path = $"{PlanPath}.transitions[{transitionIndex}].worseningTarget";
        if (carriers is null || carriers.Issues.Count != 0 || identities is null)
        {
            issues.Add(NewIssue(
                "wound_plan_worsening_target_unresolved",
                "The sealed worsening target cannot be resolved without valid wound carrier and identity baselines.",
                "one exact active canonical wound and identity row",
                target.Wound.WoundId,
                path));
            return null;
        }

        var matches = carriers.Occurrences.Where(value => string.Equals(
                value.WoundId,
                target.Wound.WoundId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            matches[0].Wound.Owner != opportunity.Owner ||
            !string.Equals(
                matches[0].Wound.Classification.Domain,
                opportunity.Domain,
                StringComparison.Ordinal) ||
            !string.Equals(
                WoundIdentityState.ComputeSemanticFingerprint(matches[0].Wound),
                target.ExpectedBeforeFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                WoundMaterializationContract.SerializeCanonical(matches[0].Wound),
                WoundMaterializationContract.SerializeCanonical(target.Wound),
                StringComparison.Ordinal) ||
            !identities.TryGetEntry(target.Wound.WoundId, out var identity) ||
            WoundIdentityState.ValidateActiveAgreement(
                identity,
                matches[0].Wound,
                path + ".identity").Count != 0)
        {
            issues.Add(NewIssue(
                "wound_plan_worsening_target_stale",
                "The sealed worsening target must equal the exact active pre-turn wound and identity state.",
                target.ExpectedBeforeFingerprint,
                matches.Length == 1
                    ? WoundIdentityState.ComputeSemanticFingerprint(matches[0].Wound)
                    : $"matches={matches.Length}",
                path));
            return null;
        }

        return WoundAcceptedTurnData.CloneWound(matches[0].Wound);
    }

    private static void ValidateTransitionBinding(
        WoundAcceptedTurnBinding binding,
        WoundAcceptedTransitionDraft draft,
        WoundOpportunityAuthority opportunity,
        WoundAcceptedEventAuthority acceptedEvent,
        WoundMaterializationEnvelope? beforeWound,
        ICollection<ValidationIssue> issues)
    {
        var proposed = draft.ProposedAfter;
        var worsening = opportunity.WorseningTarget is not null;
        var expectedKind = worsening ? "worsen" : "create";
        if (!Exact(draft.Kind) ||
            !string.Equals(draft.Kind, expectedKind, StringComparison.Ordinal) ||
            !Exact(draft.OperationKey) ||
            !Exact(draft.LocalWoundRef) ||
            !Exact(draft.LocalTransitionRef) ||
            !Exact(draft.OpportunityId) ||
            !ReadableSummary(draft.ReadableSummary))
        {
            issues.Add(NewIssue(
                "wound_plan_input_invalid",
                "A wound transition draft contains malformed local authority.",
                $"exact {expectedKind} draft identifiers and readable summary",
                "invalid transition draft"));
        }

        var bindingMismatch =
            !string.Equals(opportunity.SessionId, binding.SessionId, StringComparison.Ordinal) ||
            !string.Equals(opportunity.RequestId, binding.RequestId, StringComparison.Ordinal) ||
            !string.Equals(opportunity.SnapshotToken, binding.SnapshotToken, StringComparison.Ordinal) ||
            !string.Equals(opportunity.EventRef, acceptedEvent.EventRef, StringComparison.Ordinal) ||
            proposed.Owner != opportunity.Owner ||
            !string.Equals(proposed.Owner.Realm, binding.Realm, StringComparison.Ordinal) ||
            !string.Equals(proposed.Classification.Domain, opportunity.Domain, StringComparison.Ordinal) ||
            !string.Equals(proposed.LastTransition.TransitionId, draft.LocalTransitionRef, StringComparison.Ordinal) ||
            proposed.LastTransition.Turn != binding.Turn ||
            !string.Equals(proposed.LastTransition.Kind, draft.Kind, StringComparison.Ordinal) ||
            proposed.Severity.Rank > opportunity.MaximumSeverityRank ||
            (opportunity.MinimumSeverityRank.HasValue &&
             proposed.Severity.Rank < opportunity.MinimumSeverityRank.Value);

        if (!worsening)
        {
            bindingMismatch = bindingMismatch ||
                !string.Equals(
                    proposed.WoundId,
                    draft.LocalWoundRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    proposed.Origin.EventRef,
                    opportunity.EventRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    proposed.Origin.SourceKind,
                    opportunity.SourceKind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    proposed.Origin.SourceId,
                    opportunity.SourceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    proposed.Origin.SourceState,
                    opportunity.SourceState,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    proposed.Origin.OpportunityId,
                    opportunity.OpportunityId,
                    StringComparison.Ordinal) ||
                proposed.Origin.CreatedAtTurn != binding.Turn ||
                proposed.LastTransition.Ordinal != 1 ||
                !string.Equals(
                    proposed.Severity.MaximumAtCreation,
                    SeverityValue(opportunity.MaximumSeverityRank),
                    StringComparison.Ordinal);
        }
        else if (beforeWound is null)
        {
            bindingMismatch = true;
        }
        else
        {
            bindingMismatch = bindingMismatch ||
                !string.Equals(
                    proposed.WoundId,
                    beforeWound.WoundId,
                    StringComparison.Ordinal) ||
                proposed.Owner != beforeWound.Owner ||
                proposed.Origin != beforeWound.Origin ||
                proposed.Classification != beforeWound.Classification ||
                proposed.Severity.Rank <= beforeWound.Severity.Rank ||
                !string.Equals(
                    proposed.Severity.MaximumAtCreation,
                    beforeWound.Severity.MaximumAtCreation,
                    StringComparison.Ordinal) ||
                proposed.LastTransition.Ordinal !=
                    beforeWound.LastTransition.Ordinal + 1;
        }
        if (bindingMismatch)
        {
            issues.Add(NewIssue(
                "wound_plan_binding_mismatch",
                "The proposed wound does not match its accepted binding and opportunity authority.",
                $"exact {expectedKind} session/event/owner/source/domain/turn/severity agreement",
                draft.LocalWoundRef));
        }
    }

    private static PreparedDraftGraph ValidateDraftGraph(
        WoundAcceptedTransitionDraft draft,
        int transitionIndex,
        ICollection<ValidationIssue> issues)
    {
        var path = $"{PlanPath}.transitions[{transitionIndex}]";
        if (draft.EffectDefinitions.Count >
                WoundMaterializationContract.MaxOwnedEffectDefinitions ||
            draft.RootApplications.Count >
                WoundMaterializationContract.MaxOwnedEffectRootBindings)
        {
            issues.Add(NewIssue(
                "wound_plan_root_application_limit_exceeded",
                "The draft wound-owned graph exceeds its bounded definition/root set.",
                $"at most {WoundMaterializationContract.MaxOwnedEffectDefinitions} definitions and {WoundMaterializationContract.MaxOwnedEffectRootBindings} roots",
                $"definitions={draft.EffectDefinitions.Count};roots={draft.RootApplications.Count}",
                path + ".rootApplications"));
            return new PreparedDraftGraph(
                Array.Empty<WoundAcceptedEffectDefinitionDraft>(),
                Array.Empty<PreparedRootDraft>());
        }

        if (draft.SlotBindings.Count > WoundMaterializationContract.MaxConsequences)
        {
            issues.Add(NewIssue(
                "wound_plan_consequence_envelope_invalid",
                "The draft wound graph exceeds its consequence-slot bound.",
                $"at most {WoundMaterializationContract.MaxConsequences} slots",
                draft.SlotBindings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                path + ".slotBindings"));
        }

        var definitions = new Dictionary<string, (WoundAcceptedEffectDefinitionDraft Draft, string Key)>(
            StringComparer.Ordinal);
        var localEffectRefs = new List<string>();
        var definitionKeys = new List<string>();
        var proposedConsequences = draft.ProposedAfter.Consequences;
        var graphMalformed =
            proposedConsequences.SlotsUsed != 0 ||
            proposedConsequences.Entries.Count != 0 ||
            proposedConsequences.OwnedEffectSources.Definitions.Count != 0 ||
            proposedConsequences.OwnedEffectSources.RootBindings.Count != 0 ||
            draft.ProposedAfter.Complications.Any(static complication =>
                complication.OwnedEffectIds.Count != 0);
        for (var index = 0; index < draft.EffectDefinitions.Count; index++)
        {
            var definition = draft.EffectDefinitions[index];
            if (!Exact(definition.LocalEffectRef) ||
                definition.Definition is null ||
                !TryString(definition.Definition["definitionKey"], out var definitionKey) ||
                !Exact(definitionKey) ||
                definition.Definition["links"] is not JsonArray)
            {
                graphMalformed = true;
                continue;
            }

            localEffectRefs.Add(definition.LocalEffectRef);
            definitionKeys.Add(definitionKey);
            if (!definitions.TryAdd(
                    definition.LocalEffectRef,
                    (definition, definitionKey)))
            {
                graphMalformed = true;
            }
        }

        if (!ExactAndConfusableUnique(localEffectRefs) ||
            !ExactAndConfusableUnique(definitionKeys))
        {
            graphMalformed = true;
        }

        var localApplicationRefs = new List<string>();
        var rootOperationKeys = new List<string>();
        var roots = new List<PreparedRootDraft>(draft.RootApplications.Count);
        for (var index = 0; index < draft.RootApplications.Count; index++)
        {
            var root = draft.RootApplications[index];
            if (!Exact(root.LocalApplicationRef) ||
                !Exact(root.LocalEffectRef) ||
                !Exact(root.OperationKey) ||
                root.OwnershipDomain is null ||
                root.OwnershipDomain.Kind is not ("base_wound" or "complication") ||
                (root.OwnershipDomain.Kind == "base_wound" &&
                 root.OwnershipDomain.ComplicationId is not null) ||
                (root.OwnershipDomain.Kind == "complication" &&
                 !Exact(root.OwnershipDomain.ComplicationId)) ||
                (root.OwnershipDomain.Kind == "complication" &&
                 draft.ProposedAfter.Complications.Count(complication =>
                     string.Equals(
                         complication.ComplicationId,
                         root.OwnershipDomain.ComplicationId,
                         StringComparison.Ordinal)) != 1) ||
                !definitions.TryGetValue(root.LocalEffectRef, out var definition))
            {
                graphMalformed = true;
                continue;
            }

            localApplicationRefs.Add(root.LocalApplicationRef);
            rootOperationKeys.Add(root.OperationKey);
            var slots = draft.SlotBindings
                .Where(slot => string.Equals(
                    slot.LocalApplicationRef,
                    root.LocalApplicationRef,
                    StringComparison.Ordinal))
                .ToArray();
            roots.Add(new PreparedRootDraft(
                root,
                definition.Draft,
                definition.Key,
                slots));
        }

        if (!ExactAndConfusableUnique(localApplicationRefs) ||
            !ExactAndConfusableUnique(rootOperationKeys))
        {
            graphMalformed = true;
        }

        var slotNumbers = new List<int>();
        foreach (var slot in draft.SlotBindings)
        {
            if (slot.Slot is < 1 or > WoundMaterializationContract.MaxConsequences ||
                !Exact(slot.ProfileKey) ||
                !Exact(slot.LocalApplicationRef) ||
                !ReadableSummary(slot.ReadableSummary) ||
                roots.Count(root => string.Equals(
                    root.Draft.LocalApplicationRef,
                    slot.LocalApplicationRef,
                    StringComparison.Ordinal)) != 1)
            {
                graphMalformed = true;
            }
            slotNumbers.Add(slot.Slot);
        }

        if (slotNumbers.Distinct().Count() != slotNumbers.Count ||
            !slotNumbers.Order().SequenceEqual(Enumerable.Range(1, slotNumbers.Count)))
        {
            graphMalformed = true;
        }

        if (roots.Count != draft.RootApplications.Count ||
            definitions.Count != draft.EffectDefinitions.Count)
        {
            graphMalformed = true;
        }

        if (graphMalformed)
        {
            issues.Add(NewIssue(
                "wound_plan_consequence_envelope_invalid",
                "The draft wound-owned definition/root/slot graph is malformed or non-bijective.",
                "exact/confusable-unique definitions and roots with total slot ownership",
                "malformed draft graph",
                path + ".effectDefinitions"));
            return new PreparedDraftGraph(
                draft.EffectDefinitions,
                roots);
        }

        try
        {
            var ephemeral = BuildWoundJson(
                draft.ProposedAfter,
                draft.LocalWoundRef,
                draft.LocalTransitionRef,
                draft.LocalWoundRef,
                draft.EffectDefinitions,
                roots,
                roots.Select(static root => root.Draft.LocalApplicationRef).ToArray());
            var parsed = WoundMaterializationContract.Parse(
                ephemeral.ToJsonString(),
                path + ".proposedAfter");
            if (!parsed.IsValid)
            {
                issues.Add(NewIssue(
                    "wound_plan_consequence_envelope_invalid",
                    "The complete ephemeral wound graph failed the canonical wound contract.",
                    "valid complete wound-owned definition/root/slot graph",
                    string.Join(",", parsed.Issues.Select(static issue =>
                        $"{issue.Code}@{issue.FilePath}:expected={issue.Expected};actual={issue.Actual}")),
                    path + ".proposedAfter.consequences"));
            }
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            issues.Add(NewIssue(
                "wound_plan_consequence_envelope_invalid",
                "The complete ephemeral wound graph could not be validated.",
                "valid detached canonical wound graph",
                exception.GetType().Name,
                path + ".proposedAfter.consequences"));
        }

        return new PreparedDraftGraph(draft.EffectDefinitions, roots);
    }

    private static void ValidateEffectCarrierAuthority(
        WoundAcceptedTransitionDraft draft,
        PreparedDraftGraph graph,
        int transitionIndex,
        ICollection<ValidationIssue> issues)
    {
        var owner = draft.ProposedAfter.Owner;
        var path = $"{PlanPath}.transitions[{transitionIndex}].effectDefinitions";
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out var target))
        {
            issues.Add(NewIssue(
                "wound_plan_consequence_envelope_invalid",
                "The proposed wound owner cannot resolve to one closed effect target.",
                "one exact realm/owner-kind effect target mapping",
                owner.ToString(),
                path));
            return;
        }

        for (var index = 0; index < graph.Definitions.Count; index++)
        {
            var definition = graph.Definitions[index];
            if (definition is not null &&
                WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                    owner,
                    target,
                    definition.Definition,
                    out _))
            {
                continue;
            }

            issues.Add(NewIssue(
                "wound_plan_consequence_envelope_invalid",
                "A wound-owned definition cannot resolve to the owner's exact effect carrier.",
                "one closed wound-owner to effect-carrier mapping per definition",
                definition?.LocalEffectRef ?? "null",
                $"{path}[{index}]"));
        }
    }

    private static void ValidateDraftIdentitySets(
        WoundAcceptedTurnInput input,
        WoundCarrierCatalog? carriers,
        WoundIdentityState? identities,
        WoundHistoryState? history,
        ICollection<ValidationIssue> issues)
    {
        var localWounds = input.Transitions.Select(static value => value.LocalWoundRef).ToArray();
        var localTransitions = input.Transitions
            .Select(static value => value.LocalTransitionRef)
            .ToArray();
        var localApplications = input.Transitions
            .SelectMany(static value => value.RootApplications.Select(
                static root => root.LocalApplicationRef))
            .ToArray();
        var woundOperations = input.Transitions.Select(static value => value.OperationKey).ToArray();
        if (!ExactAndConfusableUnique(localWounds) ||
            !ExactAndConfusableUnique(localTransitions) ||
            !ExactAndConfusableUnique(localApplications) ||
            !ExactAndConfusableUnique(
                localWounds.Concat(localTransitions).Concat(localApplications)) ||
            !ExactAndConfusableUnique(woundOperations))
        {
            issues.Add(NewIssue(
                "wound_plan_opportunity_binding_mismatch",
                "Sibling draft identities and wound operation keys must be exact/confusable unique.",
                "pairwise-disjoint local wound, transition, and operation authority",
                "duplicate or confusable draft identity"));
        }

        var retainedWounds = (carriers?.Occurrences.Select(static value => value.WoundId) ??
                Enumerable.Empty<string>())
            .Concat(identities?.Entries.Select(static value => value.WoundId) ??
                Enumerable.Empty<string>());
        var historicalOperations = history?.Transitions.Select(
                static value => value.OperationKey) ??
            Enumerable.Empty<string>();
        var historicalTransitions = history?.Transitions.Select(
                static value => value.TransitionId) ??
            Enumerable.Empty<string>();
        var localCorrelations = localWounds.Concat(localTransitions).Concat(localApplications);
        if (ConfusableIntersects(woundOperations, historicalOperations) ||
            ConfusableIntersects(localTransitions, historicalTransitions) ||
            ConfusableIntersects(
                localCorrelations,
                retainedWounds.Concat(historicalTransitions).Concat(historicalOperations)))
        {
            issues.Add(NewIssue(
                "wound_plan_allocated_identity_conflict",
                "Draft transition authority collides with retained wound history.",
                "new exact/confusable operation and transition identities",
                "retained history collision"));
        }
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAllocatedIdentities(
        ValidatedInput validation,
        IReadOnlyList<string> woundIds,
        IReadOnlyList<string> applicationRefs,
        IReadOnlyList<string> transitionIds)
    {
        if (woundIds.Count != validation.Transitions.Count)
        {
            return new[]
            {
                NewIssue(
                    "wound_plan_allocated_identity_conflict",
                    "Tentative wound allocation count does not match the validated transition set.",
                    validation.Transitions.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    woundIds.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
            };
        }

        var allocated = woundIds.Concat(applicationRefs).Concat(transitionIds).ToArray();
        var localAndHistorical = new List<string>();
        localAndHistorical.AddRange(validation.Transitions.Select(
            static value => value.Draft.LocalWoundRef));
        localAndHistorical.AddRange(validation.Transitions.Select(
            static value => value.Draft.LocalTransitionRef));
        localAndHistorical.AddRange(validation.Transitions.SelectMany(static value =>
            value.Draft.RootApplications.Select(static root => root.LocalApplicationRef)));
        var retainedWoundIds = new List<string>();
        if (validation.Carriers is not null)
            retainedWoundIds.AddRange(validation.Carriers.Occurrences.Select(
                static value => value.WoundId));
        if (validation.Identities is not null)
            retainedWoundIds.AddRange(validation.Identities.Entries.Select(
                static value => value.WoundId));
        if (validation.History is not null)
        {
            localAndHistorical.AddRange(validation.History.Transitions.Select(
                static value => value.TransitionId));
            localAndHistorical.AddRange(validation.History.Transitions.Select(
                static value => value.OperationKey));
        }

        var newlyAllocatedWoundIds = validation.Transitions
            .Select((transition, index) => (transition, woundId: woundIds[index]))
            .Where(static value => value.transition.BeforeWound is null)
            .Select(static value => value.woundId)
            .ToArray();
        var reusedWoundIdsAgree = validation.Transitions
            .Select((transition, index) => (transition, woundId: woundIds[index]))
            .Where(static value => value.transition.BeforeWound is not null)
            .All(static value => string.Equals(
                value.woundId,
                value.transition.BeforeWound!.WoundId,
                StringComparison.Ordinal));
        var freshTransientIds = applicationRefs.Concat(transitionIds).ToArray();
        if (allocated.Any(static value => !Exact(value)) ||
            !ExactAndConfusableUnique(allocated) ||
            !reusedWoundIdsAgree ||
            ConfusableIntersects(
                newlyAllocatedWoundIds,
                localAndHistorical.Concat(retainedWoundIds)) ||
            ConfusableIntersects(
                freshTransientIds,
                localAndHistorical.Concat(retainedWoundIds)))
        {
            return new[]
            {
                NewIssue(
                    "wound_plan_allocated_identity_conflict",
                    "Tentative wound/application/transition allocation is malformed or collides with retained authority.",
                    "new exact/confusable-disjoint identities across the whole accepted turn",
                    "invalid, duplicate, or retained identity collision")
            };
        }

        return Array.Empty<ValidationIssue>();
    }

    internal static PreparedCandidateResult MaterializePreparedCandidate(
        WoundAcceptedTurnInput input,
        string inputFingerprint,
        ValidatedTransition candidate,
        EffectIdentityState? effectIdentities)
    {
        if (candidate.Allocation is null)
        {
            return new PreparedCandidateResult(
                null,
                null,
                new[]
                {
                    NewIssue(
                        "wound_plan_allocated_identity_conflict",
                        "A validated wound candidate has no tentative allocation.",
                        "complete wound/application/transition allocation",
                        "missing allocation")
                });
        }

        try
        {
            var allocation = candidate.Allocation;
            var proposed = candidate.Draft.ProposedAfter;
            var preparedWound = proposed with
            {
                WoundId = allocation.WoundId,
                LastTransition = proposed.LastTransition with
                {
                    TransitionId = allocation.TransitionId
                }
            };

            var definitions = candidate.Graph.Definitions
                .Select(definition =>
                {
                    var key = definition.Definition["definitionKey"]!.GetValue<string>();
                    return new WoundEffectSourceDefinition(
                        key,
                        PrepareDefinition(
                            definition.Definition,
                            candidate.Draft.LocalWoundRef,
                            allocation.WoundId));
                })
                .ToArray();
            var export = new WoundEffectSourceExport(
                1,
                "wound",
                allocation.WoundId,
                candidate.Draft.LocalWoundRef,
                "active",
                false,
                candidate.Draft.ProposedAfter.Owner.Realm,
                candidate.Draft.ProposedAfter.Owner,
                candidate.AcceptedEvent.EventRef,
                candidate.AcceptedEvent.SemanticFingerprint,
                candidate.Opportunity.OpportunityId,
                candidate.Opportunity.AuthorityFingerprint,
                definitions);

            var applications = new List<WoundRootEffectApplication>(candidate.Graph.Roots.Count);
            var lineage = new List<WoundRootLineageAuthorityRow>(candidate.Graph.Roots.Count);
            for (var rootIndex = 0; rootIndex < candidate.Graph.Roots.Count; rootIndex++)
            {
                var root = candidate.Graph.Roots[rootIndex];
                var applicationRef = allocation.ApplicationRefs[rootIndex];
                var definition = definitions.Single(value => string.Equals(
                    value.DefinitionKey,
                    root.DefinitionKey,
                    StringComparison.Ordinal));
                var definitionJson = definition.Definition;
                var components = definitionJson["components"]!.AsArray();
                var parameters = new JsonObject();
                var sourceKey = new EffectSourceKey(
                    candidate.Draft.ProposedAfter.Owner.Realm,
                    "wound",
                    allocation.WoundId,
                    root.DefinitionKey);
                var targetKey = CreateTargetKey(candidate.Draft.ProposedAfter.Owner);
                applications.Add(new WoundRootEffectApplication(
                    applicationRef,
                    candidate.MechanicsOrdinal,
                    rootIndex + 1,
                    "apply",
                    root.Draft.OperationKey,
                    root.DefinitionKey,
                    new WoundEffectTargetSelector(
                        targetKey.Kind,
                        targetKey.TargetId,
                        null),
                    targetKey,
                    new WoundEffectSourceSelector(
                        sourceKey.Realm,
                        sourceKey.Kind,
                        null,
                        candidate.Draft.LocalWoundRef,
                        sourceKey.DefinitionKey),
                    sourceKey,
                    parameters,
                    root.Slots.Select(static slot => new WoundEffectSlotAgreement(
                        slot.Slot,
                        slot.ProfileKey,
                        slot.ReadableSummary)).ToArray(),
                    components.Count,
                    WoundEffectMaterializationFingerprint.Compute(
                        sourceKey,
                        definitionJson["schemaVersion"]!.GetValue<int>(),
                        parameters,
                        components),
                    root.Draft.OwnershipDomain,
                    candidate.AcceptedEvent.EventRef,
                    CreateEffectCarrierCoordinate(
                        candidate.Draft.ProposedAfter.Owner,
                        definitionJson)));
                lineage.Add(new WoundRootLineageAuthorityRow(
                    applicationRef,
                    null,
                    root.DefinitionKey,
                    root.Draft.OwnershipDomain));
            }

            IReadOnlyList<WoundTerminalEffectOperation> terminalOperations =
                Array.Empty<WoundTerminalEffectOperation>();
            if (candidate.BeforeWound is { } beforeWound)
            {
                if (input.PreTurnEffectCarriers is null ||
                    effectIdentities is null)
                {
                    return FailedPreparedCandidate(
                        "A worsening transition has no validated pre-turn effect baseline.",
                        "missing effect carriers or identity state");
                }

                var terminal = WoundEffectTerminalOperationPlanner.Plan(
                    beforeWound,
                    input.PreTurnEffectCarriers,
                    effectIdentities,
                    beforeWound.Consequences.OwnedEffectSources.RootBindings
                        .Select(static value => value.EffectId)
                        .ToArray(),
                    candidate.AcceptedEvent.EventRef,
                    candidate.MechanicsOrdinal,
                    candidate.Graph.Roots.Count,
                    candidate.Draft.OperationKey);
                if (!terminal.Success)
                {
                    return new PreparedCandidateResult(
                        null,
                        null,
                        terminal.Issues.Select(CloneIssue).ToArray());
                }
                terminalOperations = terminal.Operations;
                lineage.AddRange(BuildRetainedExistingRootLineage(
                    beforeWound,
                    definitions.Select(static value => value.DefinitionKey)
                        .ToHashSet(StringComparer.Ordinal)));
            }

            var authoritySeal = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
                inputFingerprint,
                candidate.Draft.LocalWoundRef,
                allocation.WoundId,
                candidate.Opportunity.OpportunityId,
                candidate.Opportunity.AuthorityFingerprint,
                candidate.Draft.OperationKey,
                candidate.Draft.ReadableSummary,
                candidate.Opportunity.MaximumSeverityRank,
                candidate.Draft.Kind,
                candidate.Opportunity.WorseningTarget?.CauseKind,
                candidate.Opportunity.WorseningTarget?.ExpectedBeforeFingerprint);
            var transitionAuthority = new WoundPreparedTransitionAuthority(
                inputFingerprint,
                candidate.Opportunity.OpportunityId,
                candidate.Opportunity.AuthorityFingerprint,
                candidate.Draft.OperationKey,
                candidate.Draft.ReadableSummary,
                candidate.Opportunity.MaximumSeverityRank,
                candidate.Draft.Kind,
                candidate.Opportunity.WorseningTarget?.CauseKind,
                candidate.Opportunity.WorseningTarget?.ExpectedBeforeFingerprint,
                authoritySeal);
            var provisional = new WoundEffectOperationBatch(
                candidate.Draft.LocalWoundRef,
                allocation.WoundId,
                export,
                applications,
                terminalOperations,
                lineage,
                string.Empty,
                transitionAuthority);
            var sourceFingerprint =
                WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional);
            var batch = new WoundEffectOperationBatch(
                candidate.Draft.LocalWoundRef,
                allocation.WoundId,
                export,
                applications,
                terminalOperations,
                lineage,
                sourceFingerprint,
                transitionAuthority);
            return new PreparedCandidateResult(
                preparedWound,
                batch,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return FailedPreparedCandidate(
                "The accepted wound candidate could not be remapped into detached authority.",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static PreparedCandidateResult FailedPreparedCandidate(
        string message,
        string actual) =>
        new(
            null,
            null,
            new[]
            {
                NewIssue(
                    "wound_plan_consequence_envelope_invalid",
                    message,
                    "valid canonical remapped wound and source export",
                    actual)
            });

    private static IReadOnlyList<WoundRootLineageAuthorityRow>
        BuildRetainedExistingRootLineage(
            WoundMaterializationEnvelope beforeWound,
            IReadOnlySet<string> retainedDefinitionKeys)
    {
        var ownership = beforeWound.Consequences.OwnedEffectSources.RootBindings
            .ToDictionary(
                static value => value.EffectId,
                static _ => WoundRootOwnershipDomain.BaseWound,
                StringComparer.Ordinal);
        foreach (var complication in beforeWound.Complications)
        {
            foreach (var effectId in complication.OwnedEffectIds)
            {
                if (ownership.ContainsKey(effectId))
                {
                    ownership[effectId] =
                        WoundRootOwnershipDomain.ForComplication(
                            complication.ComplicationId);
                }
            }
        }

        return beforeWound.Consequences.OwnedEffectSources.RootBindings
            .Where(value => retainedDefinitionKeys.Contains(
                value.DefinitionKey))
            .OrderBy(static value => value.EffectId, StringComparer.Ordinal)
            .ThenBy(static value => value.DefinitionKey, StringComparer.Ordinal)
            .Select(value => new WoundRootLineageAuthorityRow(
                null,
                value.EffectId,
                value.DefinitionKey,
                ownership[value.EffectId]))
            .ToArray();
    }

    internal static WoundPreparedBaselineAuthority CreateBaselineAuthority(
        string inputFingerprint,
        WoundAcceptedTurnInput input)
    {
        var seal = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
            inputFingerprint,
            input.PreTurnCarriers,
            input.PreTurnIdentityIndex,
            input.PreTurnHistory);
        return new WoundPreparedBaselineAuthority(
            inputFingerprint,
            input.PreTurnCarriers,
            input.PreTurnIdentityIndex,
            input.PreTurnHistory,
            seal);
    }

    internal static IReadOnlyList<ValidationIssue> ValidatePreparedAuthority(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        try
        {
            var isTreatmentContinuation =
                prepared.TreatmentContinuationAuthority is not null;
            if (prepared.Binding is null ||
                prepared.AllocatedWoundIds is null ||
                prepared.AllocatedTransitionIds is null ||
                prepared.PreparedWounds is null ||
                prepared.EffectOperationBatches is null ||
                prepared.BaselineAuthority is null ||
                (!isTreatmentContinuation &&
                 (prepared.AllocatedWoundIds.Count != prepared.PreparedWounds.Count ||
                  prepared.AllocatedTransitionIds.Count != prepared.PreparedWounds.Count ||
                  prepared.EffectOperationBatches.Count != prepared.PreparedWounds.Count)) ||
                (isTreatmentContinuation &&
                 !WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(prepared)) ||
                !string.Equals(
                    WoundAcceptedTurnFingerprints.ComputeBinding(prepared.Binding),
                    prepared.BindingFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    WoundAcceptedTurnFingerprints.ComputePreparation(prepared),
                    prepared.WoundPreparationFingerprint,
                    StringComparison.Ordinal))
            {
                return PreparedSealFailure("changed prepared plan envelope or fingerprint");
            }

            var baseline = prepared.BaselineAuthority;
            if (!string.Equals(
                    baseline.PreparedInputFingerprint,
                    prepared.InputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                        baseline.PreparedInputFingerprint,
                        baseline.PreTurnCarriers,
                        baseline.PreTurnIdentityIndex,
                        baseline.PreTurnHistory),
                    baseline.AuthoritySeal,
                    StringComparison.Ordinal))
            {
                return PreparedSealFailure("changed prepared baseline authority");
            }

            if (isTreatmentContinuation)
                return Array.Empty<ValidationIssue>();

            for (var index = 0; index < prepared.EffectOperationBatches.Count; index++)
            {
                var batch = prepared.EffectOperationBatches[index];
                var wound = prepared.PreparedWounds[index];
                if (batch is null ||
                    wound is null ||
                    batch.SourceExport is null ||
                    batch.TransitionAuthority is null ||
                    !string.Equals(batch.PreparedWoundId, wound.WoundId, StringComparison.Ordinal) ||
                    !string.Equals(batch.PreparedWoundId, prepared.AllocatedWoundIds[index], StringComparison.Ordinal) ||
                    !string.Equals(wound.LastTransition.TransitionId,
                        prepared.AllocatedTransitionIds[index], StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
                        batch.SourceExportFingerprint,
                        StringComparison.Ordinal))
                {
                    return PreparedSealFailure("changed prepared wound/source association");
                }

                var authority = batch.TransitionAuthority;
                var createAuthority = string.Equals(
                    authority.TransitionKind,
                    "create",
                    StringComparison.Ordinal) &&
                    authority.CauseKind is null &&
                    authority.ExpectedBeforeFingerprint is null;
                var worseningAuthority = string.Equals(
                    authority.TransitionKind,
                    "worsen",
                    StringComparison.Ordinal) &&
                    authority.CauseKind is
                        "deterioration" or "retrauma" or "same_conflict" &&
                    Fingerprint(authority.ExpectedBeforeFingerprint);
                if (!string.Equals(
                        authority.PreparedInputFingerprint,
                        prepared.InputFingerprint,
                        StringComparison.Ordinal) ||
                    !(createAuthority || worseningAuthority) ||
                    !string.Equals(
                        wound.LastTransition.Kind,
                        authority.TransitionKind,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        wound.Severity.LastChangeEventRef,
                        batch.SourceExport.CausalEventRef,
                        StringComparison.Ordinal) ||
                    createAuthority && !string.Equals(
                        wound.Origin.EventRef,
                        batch.SourceExport.CausalEventRef,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
                            authority.PreparedInputFingerprint,
                            batch.LocalWoundRef,
                            batch.PreparedWoundId,
                            authority.OpportunityId,
                            authority.OpportunityAuthorityFingerprint,
                            authority.OperationKey,
                            authority.ReadableSummary,
                            authority.MaximumSeverityRank,
                            authority.TransitionKind,
                            authority.CauseKind,
                            authority.ExpectedBeforeFingerprint),
                        authority.AuthoritySeal,
                        StringComparison.Ordinal))
                {
                    return PreparedSealFailure("changed prepared transition authority");
                }
            }

            return Array.Empty<ValidationIssue>();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return PreparedSealFailure(exception.GetType().Name);
        }
    }

    private static IReadOnlyList<ValidationIssue> PreparedSealFailure(string actual) =>
        new[]
        {
            NewIssue(
                "wound_plan_prepared_seal_mismatch",
                "The detached prepared wound plan no longer matches its local authority seals.",
                "exact binding, input, baseline, source, transition, and preparation seals",
                actual)
        };

    internal static bool EffectInputAgreesWithPrepared(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input)
    {
        try
        {
            if (!string.Equals(input.SessionId, prepared.Binding.SessionId, StringComparison.Ordinal) ||
                !string.Equals(input.SnapshotToken, prepared.Binding.SnapshotToken, StringComparison.Ordinal) ||
                !string.Equals(input.Realm, prepared.Binding.Realm, StringComparison.Ordinal) ||
                input.SourceAuthority is null ||
                input.TargetAuthority is null ||
                input.EventInput is null ||
                input.SourceAuthority.Issues.Count != 0 ||
                 input.TargetAuthority.Issues.Count != 0 ||
                 !EventInputAgrees(prepared, input.EventInput) ||
                 !SameTurnWoundAuthorityAgrees(prepared, input.SourceAuthority))
            {
                return false;
            }

            foreach (var batch in prepared.EffectOperationBatches)
            {
                var targetKind = CreateTargetKey(batch.SourceExport.Owner).Kind;
                foreach (var expectedDefinition in batch.SourceExport.Definitions)
                {
                    var key = new EffectSourceKey(
                        batch.SourceExport.Realm,
                        batch.SourceExport.Kind,
                        batch.SourceExport.SourceId,
                        expectedDefinition.DefinitionKey);
                    var resolved = input.SourceAuthority.ResolveCanonicalBinding(key, targetKind);
                    if (!resolved.Success ||
                        resolved.Source is null ||
                        resolved.Source.Materializable ||
                        !resolved.Source.Active ||
                        !resolved.Source.SameTurn ||
                        !string.Equals(
                            resolved.Source.SourceRef,
                            batch.LocalWoundRef,
                            StringComparison.Ordinal) ||
                        !JsonNode.DeepEquals(
                            resolved.Source.Definition,
                            expectedDefinition.Definition))
                    {
                        return false;
                    }
                }

                foreach (var application in batch.RootApplications)
                {
                    if (!input.TargetAuthority.TryResolveAcceptedTarget(
                            application.ExpectedTargetKey,
                            out var target) ||
                        target is null ||
                        target.SameTurn ||
                        application.SourceSelector.SourceId is not null ||
                        !string.Equals(
                            application.SourceSelector.SourceRef,
                            batch.LocalWoundRef,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return false;
        }
    }

    internal static bool SameTurnWoundAuthorityAgrees(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectSourceAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(authority);

        if (authority.Issues.Count != 0 ||
            ValidatePreparedAuthority(prepared).Count != 0)
        {
            return false;
        }

        var expected = new List<(
            EffectSourceKey Key,
            string SourceRef,
            JsonObject Definition)>();
        var expectedGroups = new List<WoundSourceGroupAuthority>();
        foreach (var batch in prepared.EffectOperationBatches)
        {
            var export = batch.SourceExport;
            if (!string.Equals(export.Kind, "wound", StringComparison.Ordinal) ||
                export.Materializable ||
                !string.Equals(
                    export.SourceRef,
                    batch.LocalWoundRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
                    batch.SourceExportFingerprint,
                    StringComparison.Ordinal) ||
                !WoundEffectCarrierAdapter.TryCreateTargetKey(
                    export.Owner,
                    out var target))
            {
                return false;
            }

            var definitions = export.Definitions
                .Select(static definition => new WoundEffectSourceDefinition(
                    definition.DefinitionKey,
                    definition.Definition))
                .ToArray();
            expectedGroups.Add(new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    export.Realm,
                    export.Kind,
                    export.SourceId),
                export.Owner,
                target,
                sameTurn: true,
                export.SourceRef,
                batch.SourceExportFingerprint,
                definitions,
                batch.RootLineageAuthority
                    .Where(static row => row.ApplicationRef is not null)
                    .ToArray(),
                batch.RootLineageAuthority
                    .Where(static row => row.EffectId is not null)
                    .ToArray()));

            foreach (var definition in export.Definitions)
            {
                expected.Add((
                    new EffectSourceKey(
                        export.Realm,
                        export.Kind,
                        export.SourceId,
                        definition.DefinitionKey),
                    batch.LocalWoundRef,
                    definition.Definition));
            }
        }

        var orderedExpectedGroups = expectedGroups
            .OrderBy(static group => group.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.SourceId, StringComparer.Ordinal)
            .ToArray();
        var actualGroups = authority.SnapshotWoundGroupAuthorities()
            .Where(static group => group.SameTurn)
            .OrderBy(static group => group.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (actualGroups.Length != orderedExpectedGroups.Length)
            return false;
        for (var index = 0; index < orderedExpectedGroups.Length; index++)
        {
            if (!WoundGroupsAgree(
                    orderedExpectedGroups[index],
                    actualGroups[index]))
            {
                return false;
            }
        }

        var orderedExpected = expected
            .OrderBy(static entry => entry.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.SourceId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
        var actual = authority.SnapshotSameTurnWoundEntries();
        if (actual.Count != orderedExpected.Length)
            return false;

        for (var index = 0; index < orderedExpected.Length; index++)
        {
            var expectedEntry = orderedExpected[index];
            var actualEntry = actual[index];
            if (!Equals(actualEntry.Key, expectedEntry.Key) ||
                actualEntry.Materializable ||
                !actualEntry.Active ||
                !actualEntry.SameTurn ||
                !string.Equals(
                    actualEntry.SourceRef,
                    expectedEntry.SourceRef,
                    StringComparison.Ordinal) ||
                actualEntry.RequiredApplicationAuthority is not null ||
                actualEntry.SatisfiedPredicates.Count != 1 ||
                !actualEntry.SatisfiedPredicates.Contains("active") ||
                !JsonNode.DeepEquals(
                    actualEntry.Definition,
                    expectedEntry.Definition))
            {
                return false;
            }
        }

        return true;
    }

    private static bool WoundGroupsAgree(
        WoundSourceGroupAuthority expected,
        WoundSourceGroupAuthority actual)
    {
        if (expected.Key != actual.Key ||
            expected.Owner != actual.Owner ||
            expected.Target != actual.Target ||
            expected.SameTurn != actual.SameTurn ||
            !string.Equals(
                expected.SourceRef,
                actual.SourceRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.PreparedSourceExportFingerprint,
                actual.PreparedSourceExportFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                actual.GraphAuthorityFingerprint,
                actual.RecomputeGraphAuthorityFingerprint(),
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.GraphAuthorityFingerprint,
                actual.GraphAuthorityFingerprint,
                StringComparison.Ordinal) ||
            expected.Definitions.Count != actual.Definitions.Count ||
            expected.ApplicationRootLineage.Count !=
                actual.ApplicationRootLineage.Count ||
            expected.ExistingRootLineage.Count !=
                actual.ExistingRootLineage.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Definitions.Count; index++)
        {
            if (!string.Equals(
                    expected.Definitions[index].DefinitionKey,
                    actual.Definitions[index].DefinitionKey,
                    StringComparison.Ordinal) ||
                !JsonNode.DeepEquals(
                    expected.Definitions[index].Definition,
                    actual.Definitions[index].Definition))
            {
                return false;
            }
        }

        return expected.ApplicationRootLineage.SequenceEqual(
                   actual.ApplicationRootLineage) &&
               expected.ExistingRootLineage.SequenceEqual(
                   actual.ExistingRootLineage);
    }

    internal static bool IsEmptyWoundStage(WoundPreparedAcceptedTurnPlan prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        return prepared.Binding.AcceptedEvents.Count == 0 &&
               prepared.AllocatedWoundIds.Count == 0 &&
               prepared.AllocatedTransitionIds.Count == 0 &&
               prepared.PreparedWounds.Count == 0 &&
               prepared.EffectOperationBatches.Count == 0;
    }

    private static bool EventInputAgrees(
        WoundPreparedAcceptedTurnPlan prepared,
        JsonObject eventInput)
    {
        var binding = prepared.Binding;
        if (eventInput.Count < 2 ||
            !eventInput.ContainsKey("turn") ||
            !eventInput.ContainsKey("events") ||
            !TryInt(eventInput["turn"], out var turn) || turn != binding.Turn ||
            eventInput["events"] is not JsonArray events)
        {
            return false;
        }

        if (IsEmptyWoundStage(prepared))
            return true;

        if (events.Count != binding.AcceptedEvents.Count)
            return false;

        for (var index = 0; index < events.Count; index++)
        {
            if (events[index] is not JsonObject value ||
                value.Count != 3 ||
                !value.ContainsKey("eventRef") ||
                !value.ContainsKey("kind") ||
                !value.ContainsKey("authorityId") ||
                !TryString(value["eventRef"], out var eventRef) ||
                !TryString(value["kind"], out var kind) ||
                !TryString(value["authorityId"], out var authorityId) ||
                !string.Equals(eventRef, binding.AcceptedEvents[index].EventRef, StringComparison.Ordinal) ||
                !string.Equals(kind, binding.AcceptedEvents[index].Kind, StringComparison.Ordinal) ||
                !string.Equals(authorityId, binding.AcceptedEvents[index].AuthorityId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    internal static EffectDerivation DeriveEffectResults(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan effectPlan)
    {
        try
        {
            var carrierCatalog = EffectCarrierCatalog.Build(effectPlan.ResourceTriggerCarriers);
            if (carrierCatalog.Issues.Count != 0)
                return FailedDerivation("The detached effect carrier after-images are invalid.");
            var publicationCarrierCatalog = BuildPublicationCarrierCatalog(effectPlan);
            if (publicationCarrierCatalog.Issues.Count != 0)
                return FailedDerivation("The detached publication carrier after-images are invalid.");
            var beforeCarrierCatalog = BuildBeforeCarrierCatalog(effectPlan);
            if (beforeCarrierCatalog.Issues.Count != 0)
                return FailedDerivation("The detached effect carrier before-images are invalid.");

            EffectIdentityParseResult identityParse;
            using (var document = JsonDocument.Parse(
                       effectPlan.IdentityIndexAfterImage.ToJsonString()))
            {
                identityParse = EffectIdentityState.Parse(
                    document.RootElement,
                    EffectAcceptedTurnPlan.IdentityIndexPath);
            }
            if (identityParse.State is null || identityParse.Issues.Count != 0)
                return FailedDerivation("The detached effect identity after-image is invalid.");

            EffectIdentityParseResult identityBeforeParse;
            using (var document = JsonDocument.Parse(
                       (effectPlan.IdentityIndexBeforeImage ?? new JsonObject
                       {
                           ["schemaVersion"] = 1,
                           ["entries"] = new JsonArray()
                       }).ToJsonString()))
            {
                identityBeforeParse = EffectIdentityState.Parse(
                    document.RootElement,
                    EffectAcceptedTurnPlan.IdentityIndexPath);
            }
            if (identityBeforeParse.State is null ||
                identityBeforeParse.Issues.Count != 0)
            {
                return FailedDerivation(
                    "The detached effect identity before-image is invalid.");
            }

            var applications = new List<EffectAcceptedApplicationResult>();
            foreach (var batch in prepared.EffectOperationBatches)
            {
                var batchApplications = new List<(
                    WoundRootEffectApplication Expected,
                    EffectAcceptedApplicationResult Result)>();
                foreach (var expected in batch.RootApplications)
                {
                    var createdEventRef = WoundEffectOperationEventRef.Create(
                        expected.CausalEventRef,
                        expected.MechanicsOrdinal,
                        expected.OperationOrdinal,
                        expected.OperationKind);
                    var matches = effectPlan.ActiveEffects
                        .Where(effect => TryReadChronologyString(
                            effect,
                            "createdEventRef",
                            out var candidate) &&
                            string.Equals(candidate, createdEventRef, StringComparison.Ordinal))
                        .ToArray();
                    if (matches.Length != 1)
                        return FailedDerivation("A prepared root did not resolve to one exact created event.");

                    var effect = matches[0];
                    using (var document = JsonDocument.Parse(effect.ToJsonString()))
                    {
                        if (EffectMaterializationContract.Validate(
                                document.RootElement,
                                PlanPath + ".effectPlan.activeEffects",
                                EffectMaterializationPhase.CanonicalActive).Count != 0)
                        {
                            return FailedDerivation("A matched active effect is not canonical.");
                        }
                    }

                    if (!TryString(effect["effectId"], out var effectId) ||
                        !effectPlan.AllocatedEffectIds.Contains(effectId, StringComparer.Ordinal) ||
                        effect["source"] is not JsonObject source ||
                        effect["target"] is not JsonObject target ||
                        effect["components"] is not JsonArray components ||
                        !TryInt(effect["schemaVersion"], out var schemaVersion) ||
                        !TryString(effect["realm"], out var realm) ||
                        !TryString(source["kind"], out var sourceKind) ||
                        !TryString(source["sourceId"], out var sourceId) ||
                        !TryString(source["definitionKey"], out var definitionKey) ||
                        !TryString(target["kind"], out var targetKind) ||
                        !TryString(target["targetId"], out var targetId) ||
                        !TryReadChronologyString(effect, "causalEventRef", out var causalEventRef) ||
                        !TryReadChronologyString(effect, "lastTransitionId", out var lastTransitionId) ||
                        !effectPlan.AllocatedTransitionIds.Contains(
                            lastTransitionId,
                            StringComparer.Ordinal))
                    {
                        return FailedDerivation("A matched active effect is missing create evidence.");
                    }

                    var sourceKey = new EffectSourceKey(
                        realm,
                        sourceKind,
                        sourceId,
                        definitionKey);
                    var targetKey = new EffectTargetKey(realm, targetKind, targetId);
                    if (!carrierCatalog.TryResolveOne(effectId, out var occurrence) ||
                        !publicationCarrierCatalog.TryResolveOne(
                            effectId,
                            out var publicationOccurrence) ||
                        !JsonNode.DeepEquals(occurrence.Effect, effect) ||
                        !JsonNode.DeepEquals(publicationOccurrence.Effect, effect) ||
                        publicationOccurrence.Coordinate != occurrence.Coordinate ||
                        !identityParse.State.TryGetEntry(effectId, out var identity) ||
                        !IdentityAgrees(
                            identity,
                            effect,
                            occurrence.Coordinate,
                            prepared.Binding.Turn))
                    {
                        return FailedDerivation("Carrier or identity evidence disagrees with the created active effect.");
                    }

                    var createTransitions = identity.Transitions.Where(transition =>
                            string.Equals(transition.Kind, "create", StringComparison.Ordinal) &&
                            string.Equals(transition.EventRef, createdEventRef, StringComparison.Ordinal) &&
                            transition.ResultEffectIds.Count == 1 &&
                            string.Equals(
                                transition.ResultEffectIds[0],
                                effectId,
                                StringComparison.Ordinal))
                        .ToArray();
                    if (identity.Transitions.Count != 1 ||
                        createTransitions.Length != 1 ||
                        createTransitions[0].Turn != prepared.Binding.Turn ||
                        createTransitions[0].SourceEffectIds.Count != 0 ||
                        createTransitions[0].ReceiptId is not null ||
                        !string.Equals(
                            createTransitions[0].TransitionId,
                            lastTransitionId,
                            StringComparison.Ordinal))
                    {
                        return FailedDerivation("Effect identity chronology lacks one exact create transition.");
                    }

                    var actualFingerprint = WoundEffectMaterializationFingerprint.Compute(
                        sourceKey,
                        schemaVersion,
                        expected.Parameters,
                        components);
                    if (sourceKey != expected.ExpectedSourceKey ||
                        targetKey != expected.ExpectedTargetKey ||
                        occurrence.Coordinate != expected.ExpectedCarrierCoordinate ||
                        !string.Equals(causalEventRef, expected.CausalEventRef, StringComparison.Ordinal) ||
                        components.Count != expected.ExpectedComponentCount ||
                        !string.Equals(
                            actualFingerprint,
                            expected.ExpectedMaterializationFingerprint,
                            StringComparison.Ordinal))
                    {
                        return FailedDerivation(
                            "A created active effect disagrees with its prepared source, target, carrier, chronology, or components.");
                    }

                    batchApplications.Add((
                        expected,
                        new EffectAcceptedApplicationResult(
                            expected.ApplicationRef,
                            "created_new_identity",
                            effectId,
                            createTransitions[0].TransitionId,
                            createdEventRef,
                            causalEventRef,
                            sourceKey,
                            targetKey,
                            occurrence.Coordinate,
                            new WoundEffectMaterializationAgreement(
                                expected.SlotBindings,
                                components.Count,
                                actualFingerprint))));
                }

                var nextSlot = 1;
                var canonicalSlotsByApplication = new Dictionary<
                    string,
                    IReadOnlyList<WoundEffectSlotAgreement>>(StringComparer.Ordinal);
                foreach (var derived in batchApplications.OrderBy(
                             static value => value.Result.EffectId,
                             StringComparer.Ordinal))
                {
                    canonicalSlotsByApplication.Add(
                        derived.Expected.ApplicationRef,
                        derived.Expected.SlotBindings.Select(slot =>
                            new WoundEffectSlotAgreement(
                                nextSlot++,
                                slot.ProfileKey,
                                slot.ReadableSummary)).ToArray());
                }

                foreach (var derived in batchApplications)
                {
                    var result = derived.Result;
                    applications.Add(new EffectAcceptedApplicationResult(
                        result.ApplicationRef,
                        result.Disposition,
                        result.EffectId,
                        result.CreateTransitionId,
                        result.CreatedEventRef,
                        result.CausalEventRef,
                        result.SourceKey,
                        result.TargetKey,
                        result.CarrierCoordinate,
                        new WoundEffectMaterializationAgreement(
                            canonicalSlotsByApplication[result.ApplicationRef],
                            result.Materialization.ComponentCount,
                            result.Materialization.MaterializationFingerprint)));
                }
            }

            var terminations = new List<EffectAcceptedTerminationResult>();
            foreach (var expected in prepared.EffectOperationBatches
                         .SelectMany(static batch => batch.TerminalOperations))
            {
                if (!beforeCarrierCatalog.TryResolveOne(
                        expected.EffectId,
                        out var beforeOccurrence) ||
                    !identityBeforeParse.State.TryGetEntry(
                        expected.EffectId,
                        out var beforeIdentity) ||
                    !WoundEffectTerminalOperationPlanner.OccurrenceAndIdentityAgree(
                        beforeOccurrence,
                        beforeIdentity,
                        expected.ExpectedSourceKey,
                        expected.ExpectedTargetKey,
                        expected.ExpectedCarrierCoordinate,
                        expected.ExpectedIdentityOwner,
                        expected.ExpectedStackCoordinate) ||
                    !string.Equals(
                        beforeOccurrence.FilePath,
                        expected.ExpectedCarrierFilePath,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        beforeOccurrence.JsonPath,
                        expected.ExpectedCarrierJsonPath,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundEffectTerminalOperationPlanner.ComputeEffectFingerprint(
                            beforeOccurrence),
                        expected.ExpectedEffectFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        WoundEffectTerminalOperationPlanner.ComputeIdentityFingerprint(
                            beforeIdentity),
                        expected.ExpectedIdentityFingerprint,
                        StringComparison.Ordinal))
                {
                    return FailedDerivation(
                        "A prepared terminal operation disagrees with its sealed before-image occurrence or identity.");
                }

                if (carrierCatalog.TryResolveOne(expected.EffectId, out _) ||
                    publicationCarrierCatalog.TryResolveOne(expected.EffectId, out _) ||
                    effectPlan.ActiveEffects.Any(effect => string.Equals(
                        effect["effectId"]?.GetValue<string>(),
                        expected.EffectId,
                        StringComparison.Ordinal)) ||
                    !identityParse.State.TryGetEntry(
                        expected.EffectId,
                        out var afterIdentity))
                {
                    return FailedDerivation(
                        "A terminalized wound-owned effect remains active or lacks retained identity history.");
                }

                var transitions = afterIdentity.Transitions.Where(transition =>
                        string.Equals(
                            transition.EventRef,
                            expected.OperationRef,
                            StringComparison.Ordinal))
                    .ToArray();
                if (transitions.Length != 1)
                {
                    return FailedDerivation(
                        "A terminalized wound-owned effect lacks one exact terminal transition event.");
                }
                var transition = transitions[0];
                if (!string.Equals(afterIdentity.State, "expired", StringComparison.Ordinal) ||
                    !string.Equals(transition.Kind, "expire", StringComparison.Ordinal) ||
                    transition.Turn != prepared.Binding.Turn ||
                    transition.SourceEffectIds.Count != 1 ||
                    !string.Equals(
                        transition.SourceEffectIds[0],
                        expected.EffectId,
                        StringComparison.Ordinal) ||
                    transition.ResultEffectIds.Count != 0 ||
                    transition.ReceiptId is not null ||
                    !effectPlan.AllocatedTransitionIds.Contains(
                        transition.TransitionId,
                        StringComparer.Ordinal) ||
                    beforeIdentity.Transitions.Any(value => string.Equals(
                        value.EventRef,
                        expected.OperationRef,
                        StringComparison.Ordinal)))
                {
                    return FailedDerivation(
                        "A terminalized wound-owned effect has divergent terminal chronology.");
                }

                var expectedAfterIdentity = beforeIdentity.Raw.DeepClone().AsObject();
                expectedAfterIdentity["state"] = "expired";
                expectedAfterIdentity["transitions"]!.AsArray().Add(
                    transition.Raw.DeepClone());
                if (!JsonNode.DeepEquals(
                        expectedAfterIdentity,
                        afterIdentity.Raw))
                {
                    return FailedDerivation(
                        "A terminal operation changed effect identity authority beyond the exact appended transition.");
                }

                terminations.Add(new EffectAcceptedTerminationResult(
                    expected.OperationRef,
                    "expired",
                    expected.EffectId,
                    transition.TransitionId,
                    transition.EventRef,
                    expected.CausalEventRef,
                    expected.ExpectedSourceKey,
                    expected.ExpectedTargetKey,
                    expected.ExpectedCarrierCoordinate));
            }

            if (!ExactAndConfusableUnique(applications.Select(static value => value.ApplicationRef)) ||
                !ExactAndConfusableUnique(applications.Select(static value => value.EffectId)) ||
                !ExactAndConfusableUnique(applications.Select(static value => value.CreateTransitionId)) ||
                !ExactAndConfusableUnique(applications.Select(static value => value.CreatedEventRef)) ||
                !ExactAndConfusableUnique(terminations.Select(static value => value.OperationRef)) ||
                !ExactAndConfusableUnique(terminations.Select(static value => value.EffectId)) ||
                !ExactAndConfusableUnique(terminations.Select(static value =>
                    value.TerminalTransitionId)) ||
                applications.Select(static value => value.EffectId).Intersect(
                    terminations.Select(static value => value.EffectId),
                    StringComparer.Ordinal).Any())
            {
                return FailedDerivation(
                    "Created or terminal effect result identities are duplicate, confusable, or overlapping.");
            }

            if (!PreparedWoundEffectViewsAgree(
                    prepared,
                    applications,
                    terminations,
                    identityBeforeParse.State,
                    beforeCarrierCatalog,
                    effectPlan.ActiveEffects,
                    carrierCatalog,
                    publicationCarrierCatalog,
                    identityParse.State,
                    out var woundEffectDisagreement))
            {
                return FailedDerivation(woundEffectDisagreement);
            }

            return new EffectDerivation(
                applications,
                terminations,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or
                NullReferenceException)
        {
            return FailedDerivation(exception.GetType().Name);
        }
    }

    private static bool PreparedWoundEffectViewsAgree(
        WoundPreparedAcceptedTurnPlan prepared,
        IReadOnlyList<EffectAcceptedApplicationResult> applications,
        IReadOnlyList<EffectAcceptedTerminationResult> terminations,
        EffectIdentityState identityBefore,
        EffectCarrierCatalog beforeCarrierCatalog,
        IEnumerable<JsonObject> activeEffects,
        EffectCarrierCatalog carrierCatalog,
        EffectCarrierCatalog publicationCarrierCatalog,
        EffectIdentityState identityAfter,
        out string disagreement)
    {
        disagreement = string.Empty;
        var preparedWoundIds = prepared.AllocatedWoundIds.ToHashSet(StringComparer.Ordinal);
        var preparedWoundAliases = preparedWoundIds
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        var applicationIds = applications
            .Select(static application => application.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        var terminationIds = terminations
            .Select(static termination => termination.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        if (!TryCollectPreparedWoundOwnedEffectIds(
                identityBefore.Entries.Select(static entry => entry.Raw),
                preparedWoundIds,
                preparedWoundAliases,
                out var beforeIdentityIds))
        {
            disagreement = "The pre-turn wound-owned identity set is malformed or ambiguous.";
            return false;
        }
        if (!TryCollectPreparedWoundOwnedEffectIds(
                identityBefore.Entries
                    .Where(static entry => entry.State is "active" or "suspended")
                    .Select(static entry => entry.Raw),
                preparedWoundIds,
                preparedWoundAliases,
                out var beforeActiveIdentityIds))
        {
            disagreement = "The pre-turn active wound-owned identity set is malformed or ambiguous.";
            return false;
        }
        if (!terminationIds.IsSubsetOf(beforeActiveIdentityIds))
        {
            disagreement = "Terminal wound-owned effects are not a subset of the pre-turn active identity set.";
            return false;
        }
        var expectedActiveIds = beforeActiveIdentityIds
            .Except(terminationIds, StringComparer.Ordinal)
            .Concat(applicationIds)
            .ToHashSet(StringComparer.Ordinal);
        var expectedIdentityIds = beforeIdentityIds
            .Concat(applicationIds)
            .ToHashSet(StringComparer.Ordinal);
        var afterIdentityEntries = identityAfter.Entries
            .Select(static entry => entry.Raw)
            .ToArray();

        var exactViewFailure = string.Empty;
        bool ExactView(
            IEnumerable<JsonObject> values,
            IReadOnlySet<string> expected,
            string view,
            out HashSet<string> actual)
        {
            var materialized = values.ToArray();
            if (!TryCollectPreparedWoundOwnedEffectIds(
                    materialized,
                    preparedWoundIds,
                    preparedWoundAliases,
                    out actual))
            {
                exactViewFailure = view +
                    " contains malformed, duplicate, or confusable wound-owned identities.";
                return false;
            }
            if (actual.SetEquals(expected))
                return true;
            exactViewFailure = view + " set mismatch; expected=" +
                               string.Join(",", expected.OrderBy(static value => value, StringComparer.Ordinal)) +
                               "; actual=" +
                               string.Join(",", actual.OrderBy(static value => value, StringComparer.Ordinal)) +
                               "; observed=" + string.Join(",", materialized.Select(static value =>
                                   (value["effectId"]?.GetValue<string>() ?? "missing") + "/" +
                                   (value["source"]?["kind"]?.GetValue<string>() ?? "missing") + "/" +
                                   (value["source"]?["sourceId"]?.GetValue<string>() ?? "missing")));
            return false;
        }

        if (!ExactView(
                activeEffects,
                applicationIds,
                "active effect result",
                out _) ||
            !ExactView(
                carrierCatalog.Occurrences.Select(static occurrence => occurrence.Effect),
                expectedActiveIds,
                "runtime carrier",
                out _) ||
            !ExactView(
                publicationCarrierCatalog.Occurrences.Select(static occurrence =>
                    occurrence.Effect),
                expectedActiveIds,
                "publication carrier",
                out _) ||
            !ExactView(
                afterIdentityEntries,
                expectedIdentityIds,
                "retained identity",
                out _) ||
            !ExactView(
                afterIdentityEntries.Where(static entry =>
                    entry["state"]?.GetValue<string>() is "active" or "suspended"),
                expectedActiveIds,
                "active identity",
                out _))
        {
            disagreement = exactViewFailure;
            return false;
        }
        if (!SurvivingWoundEffectViewsAgree(
                beforeActiveIdentityIds,
                terminationIds,
                identityBefore,
                identityAfter,
                beforeCarrierCatalog,
                carrierCatalog,
                publicationCarrierCatalog))
        {
            disagreement = "A surviving wound-owned effect changed across identity, runtime carrier, or publication carrier views.";
            return false;
        }

        return true;
    }

    private static bool SurvivingWoundEffectViewsAgree(
        IReadOnlySet<string> beforeActiveIdentityIds,
        IReadOnlySet<string> terminationIds,
        EffectIdentityState identityBefore,
        EffectIdentityState identityAfter,
        EffectCarrierCatalog beforeCarrierCatalog,
        EffectCarrierCatalog carrierCatalog,
        EffectCarrierCatalog publicationCarrierCatalog)
    {
        foreach (var effectId in beforeActiveIdentityIds)
        {
            if (terminationIds.Contains(effectId))
                continue;

            if (!identityBefore.TryGetEntry(effectId, out var beforeIdentity) ||
                !identityAfter.TryGetEntry(effectId, out var afterIdentity) ||
                !JsonNode.DeepEquals(beforeIdentity.Raw, afterIdentity.Raw) ||
                !beforeCarrierCatalog.TryResolveOne(
                    effectId,
                    out var beforeOccurrence) ||
                !carrierCatalog.TryResolveOne(effectId, out var afterOccurrence) ||
                !publicationCarrierCatalog.TryResolveOne(
                    effectId,
                    out var publicationOccurrence) ||
                beforeOccurrence.Coordinate != afterOccurrence.Coordinate ||
                beforeOccurrence.Coordinate != publicationOccurrence.Coordinate ||
                !string.Equals(
                    beforeOccurrence.FilePath,
                    afterOccurrence.FilePath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    beforeOccurrence.FilePath,
                    publicationOccurrence.FilePath,
                    StringComparison.Ordinal) ||
                !JsonNode.DeepEquals(
                    beforeOccurrence.Effect,
                    afterOccurrence.Effect) ||
                !JsonNode.DeepEquals(
                    beforeOccurrence.Effect,
                    publicationOccurrence.Effect))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectPreparedWoundOwnedEffectIds(
        IEnumerable<JsonObject> values,
        IReadOnlySet<string> preparedWoundIds,
        IReadOnlySet<string> preparedWoundAliases,
        out HashSet<string> effectIds)
    {
        effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value["source"] is not JsonObject source ||
                !TryString(source["kind"], out var sourceKind) ||
                !string.Equals(sourceKind, "wound", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryString(source["sourceId"], out var sourceId) ||
                !Exact(sourceId))
            {
                return false;
            }

            var sourceAlias = MortalLocationIdentityState.BuildConfusableKey(sourceId);
            if (!preparedWoundAliases.Contains(sourceAlias))
                continue;
            if (!preparedWoundIds.Contains(sourceId) ||
                !TryString(value["effectId"], out var effectId) ||
                !Exact(effectId) ||
                !effectIds.Add(effectId) ||
                !effectAliases.Add(MortalLocationIdentityState.BuildConfusableKey(effectId)))
            {
                return false;
            }
        }

        return true;
    }

    private static EffectCarrierCatalog BuildPublicationCarrierCatalog(
        EffectAcceptedTurnPlan effectPlan)
    {
        var afterImages = effectPlan.CarrierAfterImages;
        var baselines = effectPlan.AcceptedCarrierBaselines;
        JsonObject? Read(string path)
        {
            if (afterImages.TryGetValue(path, out var root))
                return root.DeepClone().AsObject();
            return path switch
            {
                EffectCarrierCatalog.PlayerPath =>
                    baselines.PlayerEffects?.DeepClone().AsObject(),
                EffectCarrierCatalog.NpcPath =>
                    baselines.NpcEffects?.DeepClone().AsObject(),
                EffectCarrierCatalog.EnemiesPath =>
                    baselines.EnemyCombatants?.DeepClone().AsObject(),
                EffectCarrierCatalog.AlliesPath =>
                    baselines.AllyCombatants?.DeepClone().AsObject(),
                EffectCarrierCatalog.AfterlifeProfilesPath =>
                    baselines.AfterlifeProfiles?.DeepClone().AsObject(),
                EffectCarrierCatalog.SpiritualConflictPath =>
                    baselines.SpiritualConflict?.DeepClone().AsObject(),
                _ => null
            };
        }

        return EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            Read(EffectCarrierCatalog.PlayerPath),
            Read(EffectCarrierCatalog.NpcPath),
            Read(EffectCarrierCatalog.EnemiesPath),
            Read(EffectCarrierCatalog.AlliesPath),
            Read(EffectCarrierCatalog.AfterlifeProfilesPath),
            Read(EffectCarrierCatalog.SpiritualConflictPath)));
    }

    private static EffectCarrierCatalog BuildBeforeCarrierCatalog(
        EffectAcceptedTurnPlan effectPlan)
    {
        var beforeImages = effectPlan.CarrierBeforeImages;
        JsonObject? Read(string path) =>
            beforeImages.TryGetValue(path, out var root)
                ? root?.DeepClone().AsObject()
                : null;

        return EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
            Read(EffectCarrierCatalog.PlayerPath),
            Read(EffectCarrierCatalog.NpcPath),
            Read(EffectCarrierCatalog.EnemiesPath),
            Read(EffectCarrierCatalog.AlliesPath),
            Read(EffectCarrierCatalog.AfterlifeProfilesPath),
            Read(EffectCarrierCatalog.SpiritualConflictPath)));
    }

    private static EffectDerivation FailedDerivation(string actual) =>
        new(
            Array.Empty<EffectAcceptedApplicationResult>(),
            Array.Empty<EffectAcceptedTerminationResult>(),
            new[]
            {
                NewIssue(
                    "wound_plan_effect_result_agreement_mismatch",
                    "The detached created effect evidence does not agree with the prepared wound roots.",
                    "one canonical created identity/carrier/active-effect after-image per prepared root",
                    actual)
            });

    internal static bool IdentityAgrees(
        EffectIdentityEntry identity,
        JsonObject effect,
        EffectCarrierCoordinate coordinate,
        int acceptedTurn)
    {
        if (effect["target"] is not JsonObject target ||
            effect["source"] is not JsonObject source ||
            effect["stacking"] is not JsonObject stacking ||
            effect["chronology"] is not JsonObject chronology ||
            !TryString(effect["realm"], out var realm) ||
            !TryString(target["kind"], out var targetKind) ||
            !TryString(target["targetId"], out var targetId) ||
            !TryString(source["kind"], out var sourceKind) ||
            !TryString(source["sourceId"], out var sourceId) ||
            !TryString(stacking["stackKey"], out var stackKey) ||
            !TryInt(chronology["createdAtTurn"], out var createdAtTurn) ||
            !TryInt(chronology["lastTransitionTurn"], out var lastTransitionTurn))
        {
            return false;
        }

        var expectedCollection = coordinate.Kind switch
        {
            "combatant" when string.Equals(
                coordinate.Category,
                "buff",
                StringComparison.Ordinal) => "activeBuffs",
            "combatant" when string.Equals(
                coordinate.Category,
                "debuff",
                StringComparison.Ordinal) => "activeDebuffs",
            "spiritual_conflict" => "combatConditions",
            "combatant" => string.Empty,
            _ => "activeEffects"
        };

        return string.Equals(identity.State, "active", StringComparison.Ordinal) &&
               string.Equals(identity.Realm, realm, StringComparison.Ordinal) &&
               identity.CreatedAtTurn == acceptedTurn &&
               createdAtTurn == acceptedTurn &&
               lastTransitionTurn == acceptedTurn &&
               string.Equals(identity.Owner.OwnerId, coordinate.OwnerId, StringComparison.Ordinal) &&
               string.Equals(identity.Owner.CarrierPath, coordinate.Path, StringComparison.Ordinal) &&
               string.Equals(
                   identity.Owner.Collection,
                   expectedCollection,
                   StringComparison.Ordinal) &&
               string.Equals(identity.Owner.Kind, targetKind, StringComparison.Ordinal) &&
               string.Equals(identity.Owner.OwnerId, targetId, StringComparison.Ordinal) &&
               string.Equals(identity.StackCoordinate.Realm, realm, StringComparison.Ordinal) &&
               string.Equals(
                   identity.StackCoordinate.TargetKind,
                   targetKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   identity.StackCoordinate.TargetId,
                   targetId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   identity.StackCoordinate.SourceKind,
                   sourceKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   identity.StackCoordinate.SourceId,
                   sourceId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   identity.StackCoordinate.StackKey,
                   stackKey,
                   StringComparison.Ordinal) &&
               JsonNode.DeepEquals(identity.Target, target) &&
               JsonNode.DeepEquals(identity.Source, source);
    }

    private static JsonObject BuildWoundJson(
        WoundMaterializationEnvelope source,
        string woundId,
        string transitionId,
        string definitionWoundId,
        IReadOnlyList<WoundAcceptedEffectDefinitionDraft> definitions,
        IReadOnlyList<PreparedRootDraft> roots,
        IReadOnlyList<string> rootEffectIds)
    {
        var root = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(source))!
            .AsObject();
        root["woundId"] = woundId;
        root["lastTransition"]!["transitionId"] = transitionId;
        var consequences = root["consequences"]!.AsObject();
        consequences["slotsUsed"] = roots.Sum(static value => value.Slots.Count);
        consequences["entries"] = new JsonArray(roots
            .SelectMany((preparedRoot, index) => preparedRoot.Slots.Select(slot =>
                (JsonNode)new JsonObject
                {
                    ["slot"] = slot.Slot,
                    ["profileKey"] = slot.ProfileKey,
                    ["effectId"] = rootEffectIds[index],
                    ["readableSummary"] = slot.ReadableSummary
                }))
            .ToArray());
        consequences["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = new JsonArray(definitions.Select(definition =>
                (JsonNode)PrepareDefinition(
                    definition.Definition,
                    source.WoundId,
                    definitionWoundId)).ToArray()),
            ["rootBindings"] = new JsonArray(roots.Select((preparedRoot, index) =>
                (JsonNode)new JsonObject
                {
                    ["effectId"] = rootEffectIds[index],
                    ["definitionKey"] = preparedRoot.DefinitionKey
                }).ToArray())
        };

        if (root["complications"] is JsonArray complications)
        {
            for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                var ownership = roots[rootIndex].Draft.OwnershipDomain;
                if (!string.Equals(ownership.Kind, "complication", StringComparison.Ordinal))
                    continue;
                var complication = complications.OfType<JsonObject>().SingleOrDefault(value =>
                    string.Equals(
                        value["complicationId"]?.GetValue<string>(),
                        ownership.ComplicationId,
                        StringComparison.Ordinal));
                if (complication?["ownedEffectIds"] is JsonArray owned)
                    owned.Add(rootEffectIds[rootIndex]);
            }
        }

        return root;
    }

    private static JsonObject PrepareDefinition(
        JsonObject source,
        string localWoundRef,
        string permanentWoundId)
    {
        var result = source.DeepClone().AsObject();
        if (result["links"] is not JsonArray links)
            throw new InvalidOperationException("A wound-owned definition must have a links array.");
        links.Add(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = permanentWoundId,
            ["role"] = "source"
        });
        if (result["components"] is JsonArray components)
        {
            foreach (var component in components.OfType<JsonObject>())
            {
                if (!string.Equals(
                        component["profile"]?.GetValue<string>(),
                        "wound_consequence",
                        StringComparison.Ordinal) ||
                    component["payload"] is not JsonObject payload ||
                    !TryString(payload["woundId"], out var markerWoundId))
                {
                    continue;
                }

                if (string.Equals(markerWoundId, localWoundRef, StringComparison.Ordinal))
                    payload["woundId"] = permanentWoundId;
            }
        }
        return result;
    }

    private static EffectTargetKey CreateTargetKey(WoundOwnerCoordinate owner)
    {
        if (WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out var target))
            return target;
        throw new InvalidOperationException("Unsupported wound owner target kind.");
    }

    private static EffectCarrierCoordinate CreateEffectCarrierCoordinate(
        WoundOwnerCoordinate owner,
        JsonObject definition)
    {
        var target = CreateTargetKey(owner);
        if (WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                owner,
                target,
                definition,
                out var coordinate))
        {
            return coordinate;
        }
        throw new InvalidOperationException(
            "The wound owner does not resolve to a supported effect carrier.");
    }

    private static bool OwnerCarrierExists(
        WoundCarrierCatalogInput carriers,
        WoundOwnerCoordinate owner)
    {
        if (owner is null)
            return false;
        var root = WoundCarrierCollectionAuthority.GetRoot(
            carriers,
            owner.CarrierPath);
        return root is not null &&
               WoundCarrierCollectionAuthority.TryResolve(
                   root,
                   owner,
                   out _,
                   out _);
    }

    private static bool TryReadChronologyString(
        JsonObject effect,
        string field,
        out string value)
    {
        value = string.Empty;
        return effect["chronology"] is JsonObject chronology &&
               TryString(chronology[field], out value);
    }

    private static bool TryString(JsonNode? node, out string value)
    {
        value = string.Empty;
        try
        {
            if (node is null)
                return false;
            value = node.GetValue<string>();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        try
        {
            return node is not null && node.AsValue().TryGetValue(out value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool ReadableSummary(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= WoundMaterializationContract.MaxReadableTextLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool Fingerprint(string? value) =>
        ResourceMaterializationContract.IsAuthorityFingerprint(value);

    private static string SeverityValue(int rank) => rank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => string.Empty
    };

    private static bool ExactAndConfusableUnique(IEnumerable<string> values)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!Exact(value) ||
                !exact.Add(value) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ConfusableIntersects(
        IEnumerable<string> first,
        IEnumerable<string> second)
    {
        var aliases = second
            .Where(Exact)
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        return first.Where(Exact).Any(value => aliases.Contains(
            MortalLocationIdentityState.BuildConfusableKey(value)));
    }
}
