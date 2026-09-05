using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record WoundTransitionRequest(
    string Kind,
    string TransitionId,
    string OperationKey,
    string EventRef,
    int Turn,
    WoundMaterializationEnvelope? Before,
    WoundMaterializationEnvelope? ProposedAfter,
    WoundTransitionEvidence Evidence);

internal abstract record WoundTransitionEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint);

internal sealed record WoundCreateEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string OpportunityId,
    int MaximumSeverityRank,
    WoundOwnerCoordinate ExpectedOwner,
    string ExpectedDomain)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundWorseningEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string CauseKind,
    int MaximumSeverityRank,
    bool HasPendingTreatmentOrRecovery)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundComplicationEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string ComplicationId,
    string CauseRef,
    bool AllowsWorsening,
    int MaximumSeverityRank,
    bool HasPendingTreatmentOrRecovery)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundStabilizationEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string AttemptId,
    IReadOnlyList<string> RemovedComplicationIds,
    IReadOnlyList<string> RemovedEffectIds,
    IReadOnlyList<string> RemovedRecoveryBlockers)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundDeclaredTransitionOutcome(
    int ResultingSeverityRank,
    string ResultingCareState,
    long ResultingRecoveryProgress,
    bool Heals,
    bool TerminalAttempt,
    bool AllowsWorsening,
    IReadOnlyList<string> ResultingComplicationIds,
    IReadOnlyList<string> ResultingEffectIds,
    IReadOnlyList<string> ResultingRecoveryBlockers,
    IReadOnlyList<string> ResultingCompletedRouteIds);

internal sealed record WoundTreatmentEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string RouteOrGateRef,
    string AttemptId,
    WoundDeclaredTransitionOutcome Outcome)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundRecoveryEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string TickKey,
    string ClockOrCycleRef,
    WoundDeclaredTransitionOutcome Outcome)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundHealingEvidence(
    string AuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    string AcceptedResultRef,
    string? AttemptId,
    IReadOnlyList<WoundLegacyDeclaration> Legacies)
    : WoundTransitionEvidence(AuthorityRef, ExpectedBeforeFingerprint, ExpectedAfterFingerprint);

internal sealed record WoundLegacyEvidence(
    string TerminalAuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint,
    IReadOnlyList<WoundLegacyDeclaration> Legacies)
    : WoundTransitionEvidence(
        TerminalAuthorityRef,
        ExpectedBeforeFingerprint,
        ExpectedAfterFingerprint);

internal sealed record WoundArchiveEvidence(
    string TerminalAuthorityRef,
    string ExpectedBeforeFingerprint,
    string ExpectedAfterFingerprint)
    : WoundTransitionEvidence(
        TerminalAuthorityRef,
        ExpectedBeforeFingerprint,
        ExpectedAfterFingerprint);

internal sealed record WoundLegacyDeclaration(
    string LegacyId,
    string Kind,
    string? EntityKind,
    string ProvenanceWoundId,
    string ReadableSummary);

internal abstract record WoundTransitionIntent;

internal sealed record WoundCarrierTransitionIntent(
    string Operation,
    WoundOwnerCoordinate Owner,
    string WoundId) : WoundTransitionIntent;

internal sealed record WoundEffectTransitionIntent(
    string Operation,
    string WoundId,
    IReadOnlyList<string> BeforeEffectIds,
    IReadOnlyList<string> AfterEffectIds) : WoundTransitionIntent;

internal sealed record WoundTransitionHistoryIntent(
    string TransitionId,
    string WoundId,
    string Kind,
    string OperationKey,
    string EventRef,
    int Turn,
    string BeforeFingerprint,
    string AfterFingerprint,
    string? AttemptId,
    string? TickKey,
    bool Terminal,
    WoundTransitionResult? TransitionResult = null) : WoundTransitionIntent;

internal sealed record WoundAttemptTerminalIntent(
    string AttemptId,
    string RouteOrGateRef,
    string WoundId) : WoundTransitionIntent;

internal sealed record WoundRecoverySealIntent(
    string TickKey,
    string ClockOrCycleRef,
    string WoundId) : WoundTransitionIntent;

internal sealed record WoundFollowUpHealIntent(
    string WoundId,
    string AuthorityRef) : WoundTransitionIntent;

internal sealed record WoundCosmeticLegacyIntent(
    string LegacyId,
    string ProvenanceWoundId,
    string ReadableSummary) : WoundTransitionIntent;

internal sealed record WoundIndependentMechanicalLegacyIntent(
    string LegacyId,
    string EntityKind,
    string ProvenanceWoundId,
    string ReadableSummary) : WoundTransitionIntent;

internal sealed record WoundArchiveProjectionIntent(
    string WoundId,
    string TerminalAuthorityRef) : WoundTransitionIntent;

internal sealed record WoundTransitionReductionResult(
    WoundMaterializationEnvelope? ProposedAfter,
    IReadOnlyList<WoundTransitionIntent> Intents,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => ProposedAfter is not null && Issues.Count == 0;
}

internal static partial class WoundTransitionReducer
{
    private sealed record OwnedSourceTransitionView(
        ImmutableArray<string> RootEffectIds,
        ImmutableDictionary<string, string> DefinitionKeyByEffectId,
        ImmutableDictionary<string, string> EffectIdByRootDefinitionKey,
        ImmutableDictionary<string, WoundOwnedEffectDefinitionFact> DefinitionByKey,
        ImmutableDictionary<string, ImmutableArray<string>> ReachableDefinitionKeysByEffectId,
        ImmutableDictionary<string, ImmutableArray<WoundConsequenceEntry>> SlotsByEffectId,
        ImmutableDictionary<string, string> OwnershipDomainByEffectId);

    private static readonly IReadOnlySet<string> Kinds = new HashSet<string>(
        new[]
        {
            "create", "worsen", "complicate", "diagnose", "stabilize", "treat",
            "recover", "heal", "legacy", "archive", "author_alternative_treatment"
        },
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> WorseningCauseKinds = new HashSet<string>(
        new[] { "deterioration", "retrauma", "same_conflict" },
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> MechanicalLegacyEntityKinds =
        new HashSet<string>(
            new[] { "effect", "skill", "trait", "other" },
            StringComparer.Ordinal);

    internal static WoundTransitionReductionResult Reduce(WoundTransitionRequest? request)
    {
        var issues = new List<ValidationIssue>();
        if (request is null)
        {
            Add(
                issues,
                "wound_transition_request_invalid",
                "non-null typed wound transition request",
                "null");
            return Failure(issues);
        }
        if (!Kinds.Contains(request.Kind))
        {
            Add(issues, "wound_transition_kind_unknown", "one exact closed transition kind", request.Kind);
            return Failure(issues);
        }

        if (request.Kind == "create" && request.Before is not null)
        {
            Add(
                issues,
                "wound_transition_create_prior_exists",
                "sealed nonexistent before-state",
                request.Before.WoundId);
            return Failure(issues);
        }

        if (request.Kind == "complicate" &&
            request.Evidence is WoundComplicationEvidence rawComplication &&
            request.ProposedAfter?.Complications is not null &&
            request.ProposedAfter.Complications.Count(value =>
                value is not null &&
                string.Equals(
                    value.ComplicationId,
                    rawComplication.ComplicationId,
                    StringComparison.Ordinal)) > 1)
        {
            Add(
                issues,
                "wound_transition_complication_duplicate",
                "one exact new complication occurrence",
                rawComplication.ComplicationId);
            return Failure(issues);
        }

        ValidateRequestAuthority(request, issues);
        if (issues.Count != 0)
            return Failure(issues);
        if (request.Kind == "diagnose")
        {
            ValidateDiagnosisEvidenceIntegrity(request, issues);
            if (issues.Count != 0)
                return Failure(issues);
        }
        if (request.Kind == "author_alternative_treatment")
        {
            // Alternative evidence diagnostics precede generic public-seal checks.
            ValidateAlternativeEvidenceIntegrity(request, issues);
            if (issues.Count != 0)
                return Failure(issues);
        }
        ValidateRawCoordinateAndProvenancePreflight(request, issues);
        if (issues.Count != 0)
            return Failure(issues);
        ValidateRetainedRemovedComplicationRootPreflight(request, issues);
        if (issues.Count != 0)
            return Failure(issues);
        var before = Normalize(request.Before, "before", required: request.Kind != "create", issues);
        var after = Normalize(request.ProposedAfter, "after", required: true, issues);
        if (issues.Count != 0 || after is null)
            return Failure(issues);

        ValidateHistoryAppendPreconditions(request, before, issues);
        if (issues.Count != 0)
            return Failure(issues);
        ValidateEvidenceSeal(request, before, after, issues);
        ValidateTransitionMetadata(request, before, after, issues);
        ValidateLegalCoordinate(after, "after", issues);
        if (before is not null)
        {
            ValidateLegalCoordinate(before, "before", issues);
            ValidateStableIdentityAndProvenance(before, after, issues);
        }
        if (issues.Count != 0)
            return Failure(issues);

        switch (request.Kind)
        {
            case "create":
                ValidateCreate(request, after, issues);
                break;
            case "worsen":
                ValidateWorsen(request, before!, after, issues);
                break;
            case "complicate":
                ValidateComplicate(request, before!, after, issues);
                break;
            case "diagnose":
                ValidateDiagnose(request, before!, after, issues);
                break;
            case "author_alternative_treatment":
                ValidateAlternativeAppend(request, before!, after, issues);
                break;
            case "stabilize":
                ValidateStabilize(request, before!, after, issues);
                break;
            case "treat":
                ValidateTreat(request, before!, after, issues);
                break;
            case "recover":
                ValidateRecover(request, before!, after, issues);
                break;
            case "heal":
                ValidateHeal(request, before!, after, issues);
                break;
            case "legacy":
                ValidateLegacy(request, before!, after, issues);
                break;
            case "archive":
                ValidateArchive(request, before!, after, issues);
                break;
        }

        if (issues.Count != 0)
            return Failure(issues);
        return Success(request, before, after);
    }

    private static void ValidateRequestAuthority(
        WoundTransitionRequest request,
        List<ValidationIssue> issues)
    {
        if (!Exact(request.TransitionId) ||
            !Exact(request.OperationKey) ||
            !Exact(request.EventRef) ||
            request.Turn < 0)
        {
            Add(
                issues,
                "wound_transition_request_invalid",
                "exact client transition/operation/event authority and non-negative turn",
                $"transition={request.TransitionId};operation={request.OperationKey};event={request.EventRef};turn={request.Turn}");
        }
        if (request.Evidence is null)
        {
            Add(
                issues,
                "wound_transition_evidence_missing",
                "non-null exact typed transition evidence",
                "null");
        }
    }

    private static void ValidateRawCoordinateAndProvenancePreflight(
        WoundTransitionRequest request,
        List<ValidationIssue> issues)
    {
        var before = request.Before;
        var after = request.ProposedAfter;
        if (after is null ||
            !RawEnvelopeCollectionsAreBounded(after))
        {
            return;
        }
        if (before is not null && !RawEnvelopeCollectionsAreBounded(before))
            return;

        var coordinateIssues = new List<ValidationIssue>();
        ValidateLegalCoordinate(after, "after", coordinateIssues);
        if (before is not null)
        {
            ValidateLegalCoordinate(before, "before", coordinateIssues);
            ValidateStableIdentityAndProvenance(before, after, coordinateIssues);
        }
        if (coordinateIssues.Count == 0)
            return;

        var earlierIssues = ValidateRawEarlierGates(request, before, after);
        issues.AddRange(earlierIssues.Count == 0 ? coordinateIssues : earlierIssues);
    }

    private static void ValidateRetainedRemovedComplicationRootPreflight(
        WoundTransitionRequest request,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(request.Kind, "stabilize", StringComparison.Ordinal) ||
            request.Evidence is not WoundStabilizationEvidence stabilization ||
            request.Before is not { } before ||
            request.ProposedAfter is not { } after ||
            stabilization.RemovedComplicationIds is null ||
            stabilization.RemovedEffectIds is null ||
            stabilization.RemovedRecoveryBlockers is null ||
            stabilization.RemovedComplicationIds.Count != 1 ||
            stabilization.RemovedEffectIds.Count != 1 ||
            stabilization.RemovedRecoveryBlockers.Count >
                WoundMaterializationContract.MaxTreatmentRoutes ||
            !ExactUnique(stabilization.RemovedComplicationIds) ||
            !ExactUnique(stabilization.RemovedEffectIds) ||
            !ExactUnique(stabilization.RemovedRecoveryBlockers) ||
            !RawEnvelopeCollectionsAreBounded(before) ||
            !RawEnvelopeCollectionsAreBounded(after))
        {
            return;
        }

        var normalizedBefore = NormalizeForRawProof(before);
        if (normalizedBefore is null)
            return;

        var complicationId = stabilization.RemovedComplicationIds[0];
        var effectId = stabilization.RemovedEffectIds[0];
        var priorComplications = normalizedBefore.Complications.Where(complication =>
                string.Equals(complication.ComplicationId, complicationId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (priorComplications.Length != 1 ||
            priorComplications[0].OwnedEffectIds.Count != 1 ||
            !string.Equals(
                priorComplications[0].OwnedEffectIds[0],
                effectId,
                StringComparison.Ordinal) ||
            after.Complications.Any(complication => string.Equals(
                complication.ComplicationId,
                complicationId,
                StringComparison.Ordinal)) ||
            after.Consequences.Entries.Any(entry => string.Equals(
                entry.EffectId,
                effectId,
                StringComparison.Ordinal)))
        {
            return;
        }

        var currentBindings = after.Consequences.OwnedEffectSources.RootBindings
            .Where(binding => string.Equals(binding.EffectId, effectId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (currentBindings.Length != 1)
            return;

        var branchProof = normalizedBefore with
        {
            Consequences = normalizedBefore.Consequences with
            {
                OwnedEffectSources = after.Consequences.OwnedEffectSources
            }
        };
        var normalizedBranchProof = NormalizeForRawProof(branchProof);
        if (normalizedBranchProof is null)
            return;

        var proofView = BuildOwnedSourceTransitionView(normalizedBranchProof);
        if (!proofView.ReachableDefinitionKeysByEffectId.TryGetValue(
                effectId,
                out var targetReachability) ||
            RootReachabilityContainsWoundMarker(proofView, effectId))
        {
            return;
        }

        var retainedReachability = proofView.RootEffectIds
            .Where(rootEffectId => !string.Equals(
                rootEffectId,
                effectId,
                StringComparison.Ordinal))
            .SelectMany(rootEffectId => proofView.ReachableDefinitionKeysByEffectId[rootEffectId])
            .ToHashSet(StringComparer.Ordinal);
        var prunedDefinitionKeys = targetReachability
            .Where(definitionKey => !retainedReachability.Contains(definitionKey))
            .ToHashSet(StringComparer.Ordinal);
        if (prunedDefinitionKeys.Count == 0)
            return;

        var proofSources = normalizedBranchProof.Consequences.OwnedEffectSources;
        var retainedDefinitions = proofSources.Definitions.Where(definition =>
                !prunedDefinitionKeys.Contains(
                    definition.GetProperty("definitionKey").GetString()!))
            .ToImmutableArray();
        var retainedFacts = proofSources.DefinitionFacts.Where(fact =>
                !prunedDefinitionKeys.Contains(fact.DefinitionKey))
            .ToImmutableArray();
        var retainedBindings = proofSources.RootBindings.Where(binding =>
                !string.Equals(binding.EffectId, effectId, StringComparison.Ordinal))
            .ToImmutableArray();
        var repairedAfter = after with
        {
            Consequences = after.Consequences with
            {
                OwnedEffectSources = new WoundOwnedEffectSources(
                    retainedDefinitions,
                    retainedBindings)
                {
                    DefinitionFacts = retainedFacts
                }
            }
        };
        var normalizedRepairedAfter = NormalizeForRawProof(repairedAfter);
        if (normalizedRepairedAfter is null)
            return;

        var earlierIssues = ValidateRawEarlierGates(request, normalizedBefore, after);
        if (earlierIssues.Count != 0)
        {
            issues.AddRange(earlierIssues);
            return;
        }

        ValidateLegalCoordinate(after, "after", issues);
        ValidateLegalCoordinate(normalizedBefore, "before", issues);
        ValidateStableIdentityAndProvenance(normalizedBefore, after, issues);
        if (issues.Count != 0)
            return;

        if (ValidateStabilizePreOwnedSourceGates(
                request,
                normalizedBefore,
                after,
                issues) is null)
        {
            return;
        }

        ValidateSameRankOwnedSourceDelta(
            normalizedBefore,
            normalizedBranchProof,
            null,
            issues);
        if (issues.Count != 0)
            return;

        ValidateStabilize(request, normalizedBefore, normalizedRepairedAfter, issues);
        if (issues.Count != 0)
            return;

        AddOwnedSourceIssue(
            issues,
            "wound_transition_owned_source_graph_invalid",
            "removed complication root absent with its induced slots and unreachable definition branch",
            effectId);
    }

    private static List<ValidationIssue> ValidateRawEarlierGates(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope after)
    {
        var earlierIssues = new List<ValidationIssue>();
        ValidateHistoryAppendPreconditions(request, before, earlierIssues);
        if (earlierIssues.Count == 0)
            ValidateEvidenceSeal(request, before, after, earlierIssues);
        if (earlierIssues.Count == 0)
            ValidateTransitionMetadata(request, before, after, earlierIssues);
        return earlierIssues;
    }

    private static WoundMaterializationEnvelope? NormalizeForRawProof(
        WoundMaterializationEnvelope wound)
    {
        var proofIssues = new List<ValidationIssue>();
        var normalized = Normalize(wound, "rawProof", required: true, proofIssues);
        return proofIssues.Count == 0 ? normalized : null;
    }

    private static bool RawEnvelopeCollectionsAreBounded(WoundMaterializationEnvelope wound)
    {
        if (wound.Owner is null ||
            wound.Origin is null ||
            wound.Classification?.LocationProfile is null ||
            wound.Display is null ||
            wound.Severity is null ||
            wound.Care is null ||
            wound.Consequences?.OwnedEffectSources is null ||
            wound.Treatment is null ||
            wound.Recovery is null ||
            wound.Relations is null ||
            wound.LastTransition is null ||
            !BoundedReferences(
                wound.Display.VisibleSymptoms,
                WoundMaterializationContract.MaxRequirementsPerTreatmentMember) ||
            !BoundedReferences(
                wound.Complications,
                WoundMaterializationContract.MaxComplications) ||
            wound.Complications.Any(complication => !BoundedReferences(
                complication.OwnedEffectIds,
                WoundMaterializationContract.MaxOwnedEffectRootBindings)) ||
            !BoundedReferences(
                wound.Consequences.Entries,
                WoundMaterializationContract.MaxConsequences) ||
            !BoundedJsonElements(
                wound.Consequences.OwnedEffectSources.Definitions,
                WoundMaterializationContract.MaxOwnedEffectDefinitions) ||
            !BoundedReferences(
                wound.Consequences.OwnedEffectSources.RootBindings,
                WoundMaterializationContract.MaxOwnedEffectRootBindings) ||
            !BoundedReferences(
                wound.Consequences.OwnedEffectSources.DefinitionFacts,
                WoundMaterializationContract.MaxOwnedEffectDefinitions) ||
            wound.Consequences.OwnedEffectSources.DefinitionFacts.Any(fact =>
                fact.ApplyDefinitionTargets.IsDefault ||
                fact.ApplyDefinitionTargets.Length >
                    WoundMaterializationContract.MaxOwnedEffectDefinitions) ||
            !BoundedReferences(
                wound.Treatment.DiagnosisPaths,
                WoundMaterializationContract.MaxDiagnosisPaths) ||
            !BoundedReferences(
                wound.Treatment.Routes,
                WoundMaterializationContract.MaxTreatmentRoutes) ||
            !BoundedReferences(
                wound.Treatment.KnownRouteIds,
                WoundMaterializationContract.MaxTreatmentRoutes) ||
            !BoundedReferences(
                wound.Treatment.CompletedRouteIds,
                WoundMaterializationContract.MaxTreatmentRoutes) ||
            wound.Treatment.DiagnosisPaths.Any(path =>
                !BoundedReferences(
                    path.RequiresKnownFacts,
                    WoundMaterializationContract.MaxRequirementsPerTreatmentMember) ||
                !BoundedJsonElements(
                    path.Requirements,
                    WoundMaterializationContract.MaxRequirementsPerTreatmentMember) ||
                !BoundedReferences(
                    path.Reveals,
                    WoundMaterializationContract.MaxRequirementsPerTreatmentMember) ||
                path.Check.ValueKind == JsonValueKind.Undefined) ||
            wound.Treatment.Routes.Any(route =>
                !BoundedJsonElements(
                    route.Requirements,
                    WoundMaterializationContract.MaxRequirementsPerTreatmentMember) ||
                !BoundedJsonElements(
                    route.Outcomes,
                    WoundMaterializationContract.MaxTreatmentOutcomeMembers) ||
                route.ResourcePolicy.ValueKind == JsonValueKind.Undefined ||
                route.Resolution.ValueKind == JsonValueKind.Undefined ||
                route.Interruption is { ValueKind: JsonValueKind.Undefined }) ||
            !BoundedReferences(
                wound.Recovery.Blockers,
                WoundMaterializationContract.MaxTreatmentRoutes) ||
            wound.Recovery.DeteriorationPolicy is { ValueKind: JsonValueKind.Undefined } ||
            !BoundedReferences(
                wound.Relations.LegacyRefs,
                WoundMaterializationContract.MaxTreatmentRoutes) ||
            !BoundedReferences(
                wound.Relations.IndependentEffectRefs,
                WoundMaterializationContract.MaxTreatmentRoutes))
        {
            return false;
        }

        return true;
    }

    private static bool BoundedReferences<T>(IReadOnlyList<T>? values, int maximum)
        where T : class =>
        values is not null &&
        values.Count <= maximum &&
        values.All(static value => value is not null);

    private static bool BoundedJsonElements(
        IReadOnlyList<JsonElement>? values,
        int maximum) =>
        values is not null &&
        values.Count <= maximum &&
        values.All(static value => value.ValueKind != JsonValueKind.Undefined);

    private static WoundMaterializationEnvelope? Normalize(
        WoundMaterializationEnvelope? wound,
        string member,
        bool required,
        List<ValidationIssue> issues)
    {
        if (wound is null)
        {
            if (required)
            {
                Add(
                    issues,
                    "wound_transition_envelope_missing",
                    $"complete {member} wound envelope",
                    "missing");
            }
            return null;
        }

        try
        {
            var canonical = WoundMaterializationContract.SerializeCanonical(wound);
            var parsed = WoundMaterializationContract.Parse(canonical, Path(member));
            if (parsed.IsValid)
                return parsed.Wound;
            Add(
                issues,
                "wound_transition_envelope_invalid",
                $"valid complete {member} wound envelope",
                string.Join(",", parsed.Issues.Select(static issue => issue.Code)));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            Add(
                issues,
                "wound_transition_envelope_invalid",
                $"serializable complete {member} wound envelope",
                exception.GetType().Name);
        }
        return null;
    }

    private static void ValidateHistoryAppendPreconditions(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope? before,
        List<ValidationIssue> issues)
    {
        if (before is null)
            return;
        if (before.LastTransition.Ordinal >= WoundHistoryState.MaxTransitions)
        {
            Add(
                issues,
                "wound_transition_history_capacity_exhausted",
                $"prior wound transition ordinal below {WoundHistoryState.MaxTransitions}",
                before.LastTransition.Ordinal.ToString());
            return;
        }
        if (request.Turn < before.LastTransition.Turn)
        {
            Add(
                issues,
                "wound_transition_turn_regression",
                $"turn at least {before.LastTransition.Turn}",
                request.Turn.ToString());
            return;
        }
        if (string.Equals(
                request.TransitionId,
                before.LastTransition.TransitionId,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_duplicate_transition_id",
                "transition identity distinct from the immediate prior transition",
                request.TransitionId);
            return;
        }
        if (string.Equals(
                MortalLocationIdentityState.BuildConfusableKey(request.TransitionId),
                MortalLocationIdentityState.BuildConfusableKey(
                    before.LastTransition.TransitionId),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_confusable_transition_id",
                "transition identity distinct from the immediate prior confusable key",
                request.TransitionId);
        }
    }

    private static void ValidateEvidenceSeal(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        var evidence = request.Evidence;
        if (!Exact(evidence.AuthorityRef) ||
            !Fingerprint(evidence.ExpectedBeforeFingerprint) ||
            !Fingerprint(evidence.ExpectedAfterFingerprint))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact evidence authority and lowercase SHA-256 state seals",
                evidence.AuthorityRef);
            return;
        }

        var actualBefore = before is null
            ? WoundHistoryState.ComputeNonexistentBeforeFingerprint(after.WoundId)
            : WoundIdentityState.ComputeSemanticFingerprint(before);
        var actualAfter = WoundIdentityState.ComputeSemanticFingerprint(after);
        if (!string.Equals(
                evidence.ExpectedBeforeFingerprint,
                actualBefore,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_before_fingerprint_mismatch",
                actualBefore,
                evidence.ExpectedBeforeFingerprint);
        }
        if (!string.Equals(
                evidence.ExpectedAfterFingerprint,
                actualAfter,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_after_fingerprint_mismatch",
                actualAfter,
                evidence.ExpectedAfterFingerprint);
        }
    }

    private static void ValidateTransitionMetadata(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (before is not null &&
            request.Kind is "legacy" or "archive" &&
            TerminalPair(before, after))
        {
            if (after.LastTransition != before.LastTransition)
            {
                Add(
                    issues,
                    "wound_transition_metadata_mismatch",
                    "post-terminal audit preserves the sealed wound lastTransition",
                    $"{after.LastTransition.TransitionId}/{after.LastTransition.Kind}/" +
                    $"{after.LastTransition.Turn}/{after.LastTransition.Ordinal}");
            }
            return;
        }

        var expectedOrdinal = before is null ? 1 : before.LastTransition.Ordinal + 1;
        if (!string.Equals(
                after.LastTransition.TransitionId,
                request.TransitionId,
                StringComparison.Ordinal) ||
            !string.Equals(after.LastTransition.Kind, request.Kind, StringComparison.Ordinal) ||
            after.LastTransition.Turn != request.Turn ||
            after.LastTransition.Ordinal != expectedOrdinal)
        {
            Add(
                issues,
                "wound_transition_metadata_mismatch",
                $"{request.TransitionId}/{request.Kind}/{request.Turn}/{expectedOrdinal}",
                $"{after.LastTransition.TransitionId}/{after.LastTransition.Kind}/{after.LastTransition.Turn}/{after.LastTransition.Ordinal}");
        }

        if (before is null)
        {
            if (!string.Equals(after.Origin.EventRef, request.EventRef, StringComparison.Ordinal) ||
                !string.Equals(
                    after.Severity.LastChangeEventRef,
                    request.EventRef,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    "wound_transition_event_mismatch",
                    "creation origin and severity bound to exact request event",
                    request.EventRef);
            }
            return;
        }

        var severityChanged = before.Severity.Rank != after.Severity.Rank;
        var expectedSeverityEvent = severityChanged
            ? request.EventRef
            : before.Severity.LastChangeEventRef;
        if (!string.Equals(
                after.Severity.LastChangeEventRef,
                expectedSeverityEvent,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_event_mismatch",
                expectedSeverityEvent,
                after.Severity.LastChangeEventRef);
        }
    }

    private static void ValidateStableIdentityAndProvenance(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(before.WoundId, after.WoundId, StringComparison.Ordinal) ||
            before.Owner != after.Owner ||
            before.Origin != after.Origin ||
            before.Classification != after.Classification ||
            !string.Equals(
                before.Severity.MaximumAtCreation,
                after.Severity.MaximumAtCreation,
                StringComparison.Ordinal) ||
            !string.Equals(
                before.Relations.PriorWoundId,
                after.Relations.PriorWoundId,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_identity_or_provenance_changed",
                "exact wound/owner/realm/domain/origin/classification/prior identity continuity",
                after.WoundId);
        }
    }

    private static void ValidateCreate(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Before is not null)
        {
            Add(
                issues,
                "wound_transition_create_prior_exists",
                "sealed nonexistent before-state",
                request.Before.WoundId);
        }
        if (request.Evidence is not WoundCreateEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundCreateEvidence", request.Evidence);
            return;
        }
        if (!Exact(evidence.OpportunityId) ||
            evidence.MaximumSeverityRank is < 1 or > 4 ||
            evidence.ExpectedOwner is null ||
            !Exact(evidence.ExpectedDomain))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact create opportunity/domain and severity cap I-IV",
                evidence.OpportunityId);
            return;
        }
        if (!string.Equals(
                evidence.OpportunityId,
                after.Origin.OpportunityId,
                StringComparison.Ordinal) ||
            evidence.ExpectedOwner != after.Owner ||
            !string.Equals(
                evidence.ExpectedDomain,
                after.Classification.Domain,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_create_authority_mismatch",
                "exact sealed opportunity, owner, and domain",
                after.WoundId);
        }
        if (!string.Equals(after.Lifecycle, "active", StringComparison.Ordinal) ||
            after.Care.State is not ("fresh" or "untreated") ||
            after.Care.StabilizedAtTurn is not null ||
            after.Care.ActiveCourseId is not null ||
            after.Care.LastAttemptId is not null)
        {
            Add(
                issues,
                "wound_transition_create_state_invalid",
                "active fresh or untreated wound",
                $"{after.Lifecycle}/{after.Care.State}");
        }
        if (after.Origin.CreatedAtTurn != request.Turn)
        {
            Add(
                issues,
                "wound_transition_create_chronology_invalid",
                $"origin createdAtTurn equals sealed request turn {request.Turn}",
                after.Origin.CreatedAtTurn.ToString());
        }
        if (after.Severity.Rank > evidence.MaximumSeverityRank ||
            SeverityRank(after.Severity.MaximumAtCreation) != evidence.MaximumSeverityRank)
        {
            Add(
                issues,
                "wound_transition_create_severity_forbidden",
                $"selected severity I-{evidence.MaximumSeverityRank} and maximumAtCreation exactly equal to the sealed cap",
                $"selected={after.Severity.Rank};maximumAtCreation={after.Severity.MaximumAtCreation}");
        }
    }

    private static void ValidateWorsen(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundWorseningEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundWorseningEvidence", request.Evidence);
            return;
        }
        if (!ActivePair(before, after))
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        if (!Exact(evidence.CauseKind) ||
            !WorseningCauseKinds.Contains(evidence.CauseKind) ||
            evidence.MaximumSeverityRank is < 1 or > 4)
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "sealed deterioration, retrauma, or same-conflict evidence capped at I-IV",
                evidence.CauseKind);
        }
        if (before.Severity.Rank is < 1 or >= 4 ||
            after.Severity.Rank <= before.Severity.Rank ||
            after.Severity.Rank > Math.Min(4, evidence.MaximumSeverityRank))
        {
            Add(
                issues,
                "wound_transition_worsen_severity_invalid",
                "strictly higher active severity up to sealed maximum IV",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (evidence.HasPendingTreatmentOrRecovery ||
            before.Care.ActiveCourseId is not null ||
            string.Equals(before.Care.State, "recovering", StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_pending_conflict",
                "no pending treatment or recovery transition",
                before.Care.ActiveCourseId ?? before.Care.State);
        }
        if (after.Recovery.CurrentStepProgress != 0)
        {
            Add(
                issues,
                "wound_transition_worsen_progress_not_reset",
                "zero current-step recovery progress",
                after.Recovery.CurrentStepProgress.ToString());
        }
        if (after.Care.ActiveCourseId is not null)
        {
            Add(
                issues,
                "wound_transition_pending_conflict",
                "no active course after worsening",
                after.Care.ActiveCourseId);
        }
        if (issues.Count == 0)
            ValidateFreshSeverityRootSet(before, after, issues);
        if (issues.Count == 0 &&
            (!SameRecoveryPolicyExceptStep(before.Recovery, after.Recovery) ||
            !SameComplicationFacts(before.Complications, after.Complications) ||
            !CanonicalEqual(
                before,
                after with
                {
                    Severity = before.Severity,
                    Care = before.Care,
                    Complications = before.Complications,
                    Consequences = before.Consequences,
                    Recovery = before.Recovery,
                    Display = before.Display,
                    LastTransition = before.LastTransition
                })))
        {
            Add(
                issues,
                "wound_transition_worsen_scope_invalid",
                "only severity, care reset, current recovery step, display, and consequence rematerialization",
                after.WoundId);
        }
    }

    private static bool SameComplicationFacts(
        IReadOnlyList<WoundComplication> before,
        IReadOnlyList<WoundComplication> after)
    {
        if (before.Count != after.Count)
            return false;
        for (var index = 0; index < before.Count; index++)
        {
            if (before[index] != after[index] with
                {
                    OwnedEffectIds = before[index].OwnedEffectIds
                })
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidateComplicate(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundComplicationEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundComplicationEvidence", request.Evidence);
            return;
        }
        if (!ActivePair(before, after))
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        if (!Exact(evidence.ComplicationId) ||
            !Exact(evidence.CauseRef) ||
            evidence.MaximumSeverityRank is < 1 or > 4)
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact new complication/cause and severity cap I-IV",
                $"{evidence.ComplicationId}/{evidence.CauseRef}");
            return;
        }
        if (before.Complications.Any(value =>
                string.Equals(
                    value.ComplicationId,
                    evidence.ComplicationId,
                    StringComparison.Ordinal)))
        {
            Add(
                issues,
                "wound_transition_complication_duplicate",
                "new exact complication identity",
                evidence.ComplicationId);
        }

        var added = after.Complications.Where(value => !before.Complications.Any(old =>
                string.Equals(old.ComplicationId, value.ComplicationId, StringComparison.Ordinal)))
            .ToArray();
        var removed = before.Complications.Where(value => !after.Complications.Any(current =>
                string.Equals(current.ComplicationId, value.ComplicationId, StringComparison.Ordinal)))
            .ToArray();
        if (removed.Length != 0 ||
            added.Length != 1 ||
            !string.Equals(
                added.SingleOrDefault()?.ComplicationId,
                evidence.ComplicationId,
                StringComparison.Ordinal) ||
            !RetainedComplicationsUnchanged(before, after))
        {
            Add(
                issues,
                "wound_transition_complication_replaced",
                "all prior complications unchanged plus one exact new complication",
                evidence.ComplicationId);
        }

        var worsened = after.Severity.Rank > before.Severity.Rank;
        if (after.Severity.Rank < before.Severity.Rank)
        {
            Add(
                issues,
                "wound_transition_complicate_severity_invalid",
                "unchanged or explicitly worsened severity",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (worsened && (!evidence.AllowsWorsening ||
                         after.Severity.Rank > evidence.MaximumSeverityRank ||
                         after.Severity.Rank > 4))
        {
            Add(
                issues,
                "wound_transition_complicate_worsening_forbidden",
                "explicit bounded worsening outcome",
                after.Severity.Rank.ToString());
        }
        if (worsened && (evidence.HasPendingTreatmentOrRecovery ||
                         before.Care.ActiveCourseId is not null ||
                         string.Equals(before.Care.State, "recovering", StringComparison.Ordinal)))
        {
            Add(
                issues,
                "wound_transition_pending_conflict",
                "no pending treatment or recovery while worsening",
                before.Care.State);
        }
        if (worsened && after.Recovery.CurrentStepProgress != 0)
        {
            Add(
                issues,
                "wound_transition_worsen_progress_not_reset",
                "zero current-step recovery progress",
                after.Recovery.CurrentStepProgress.ToString());
        }
        if (!worsened && !RecoveryEqual(before.Recovery, after.Recovery))
        {
            Add(
                issues,
                "wound_transition_complicate_recovery_changed",
                "unchanged recovery without worsening",
                after.WoundId);
        }
        if (!worsened && after.Care != before.Care)
        {
            Add(
                issues,
                "wound_transition_complicate_care_changed",
                "unchanged care without a sealed worsening outcome",
                after.WoundId);
        }
        if (worsened && after.Care.ActiveCourseId is not null)
        {
            Add(
                issues,
                "wound_transition_pending_conflict",
                "no active course after complication worsening",
                after.Care.ActiveCourseId);
        }
        if (worsened && !SameRecoveryPolicyExceptStep(before.Recovery, after.Recovery))
        {
            Add(
                issues,
                "wound_transition_complicate_recovery_policy_changed",
                "worsening resets only the current recovery step and preserves its sealed policy",
                after.WoundId);
        }
        if (issues.Count == 0)
        {
            if (worsened)
                ValidateFreshSeverityRootSet(before, after, issues);
            else
                ValidateSameRankOwnedSourceDelta(
                    before,
                    after,
                    added.SingleOrDefault(),
                    issues);
        }
        if (issues.Count == 0 &&
            !CanonicalEqual(
                before,
                after with
                {
                    Severity = before.Severity,
                    Care = before.Care,
                    Complications = before.Complications,
                    Consequences = before.Consequences,
                    Recovery = before.Recovery,
                    Display = before.Display,
                    LastTransition = before.LastTransition
                }))
        {
            Add(
                issues,
                "wound_transition_complicate_scope_invalid",
                "only exact complication, declared worsening, consequences, care/recovery reset, and display",
                after.WoundId);
        }
    }

    private static void ValidateDiagnose(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundDiagnosisEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundDiagnosisEvidence", request.Evidence);
            return;
        }
        if (!ActivePair(before, after) ||
            before.Classification.Domain != "physical" || after.Classification.Domain != "physical" ||
            before.Owner.Realm != "mortal_world" || after.Owner.Realm != "mortal_world")
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        var diagnosisPath = before.Treatment.DiagnosisPaths.SingleOrDefault(path =>
            string.Equals(
                path.DiagnosisPathId,
                evidence.DiagnosisPathId,
                StringComparison.Ordinal));
        var knownFacts = before.Treatment.KnownRouteIds.Select(static id => "route:" + id)
            .Concat(before.Complications.Where(static complication =>
                    complication.Visibility is "public" or "known_to_player")
                .Select(static complication => "complication:" + complication.ComplicationId))
            .ToHashSet(StringComparer.Ordinal);
        if (diagnosisPath is null ||
            diagnosisPath.Visibility == "gm_only" ||
            diagnosisPath.Visibility == "hidden" && diagnosisPath.RequiresKnownFacts.Count == 0 ||
            !diagnosisPath.RequiresKnownFacts.All(knownFacts.Contains))
        {
            Add(issues, "wound_transition_diagnosis_path_unavailable",
                "one exact player-selectable path whose prerequisites are already known",
                evidence.DiagnosisPathId);
            return;
        }
        if (!SequenceEqual(evidence.ResultKind == "success" ? diagnosisPath.Reveals :
                ImmutableArray<string>.Empty, evidence.RevealedFacts) ||
            evidence.RevealedFacts.Any(static fact => !TryParseDiagnosisReveal(
                fact,
                out _,
                out _)))
        {
            Add(
                issues,
                "wound_transition_diagnosis_fact_unauthorized",
                "exact declared reveals of one accepted diagnosis path",
                evidence.AuthorityRef);
            return;
        }

        var definedRoutes = before.Treatment.Routes.Select(static route => route.RouteId)
            .ToHashSet(StringComparer.Ordinal);
        var definedComplications = before.Complications
            .Select(static complication => complication.ComplicationId)
            .ToHashSet(StringComparer.Ordinal);
        var revealedRoutes = ImmutableArray.CreateBuilder<string>();
        var revealedComplications = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in evidence.RevealedFacts)
        {
            TryParseDiagnosisReveal(fact, out var kind, out var identifier);
            if (kind == "route")
                revealedRoutes.Add(identifier);
            else
                revealedComplications.Add(identifier);
        }
        if (revealedRoutes.Any(routeId => !definedRoutes.Contains(routeId)) ||
            revealedComplications.Any(complicationId =>
                !definedComplications.Contains(complicationId)))
        {
            Add(
                issues,
                "wound_transition_diagnosis_fact_unauthorized",
                "every declared route/complication reveal resolves in the sealed wound",
                string.Join(",", evidence.RevealedFacts));
            return;
        }

        var expectedKnownRoutes = before.Treatment.KnownRouteIds
            .Concat(revealedRoutes.Where(routeId => !before.Treatment.KnownRouteIds.Contains(
                routeId,
                StringComparer.Ordinal)))
            .ToImmutableArray();
        var expectedComplications = before.Complications.Select(complication =>
                revealedComplications.Contains(complication.ComplicationId) &&
                complication.Visibility is not ("public" or "known_to_player")
                    ? complication with { Visibility = "known_to_player" }
                    : complication)
            .ToImmutableArray();
        if (!SequenceEqual(after.Treatment.KnownRouteIds, expectedKnownRoutes) ||
            !ComplicationSequenceEqual(after.Complications, expectedComplications))
        {
            Add(
                issues,
                "wound_transition_diagnosis_fact_unauthorized",
                "only exact route and complication facts declared by the accepted diagnosis path",
                evidence.AuthorityRef);
        }
        if (!DisplayEqual(after.Display, before.Display))
        {
            Add(
                issues,
                "wound_transition_diagnosis_display_changed",
                "diagnosis cannot rewrite undeclared display text",
                after.WoundId);
        }

        var normalizedAfter = after with
        {
            Display = before.Display,
            Complications = before.Complications,
            Treatment = after.Treatment with { KnownRouteIds = before.Treatment.KnownRouteIds },
            LastTransition = before.LastTransition
        };
        if (!CanonicalEqual(before, normalizedAfter))
        {
            Add(
                issues,
                "wound_transition_diagnosis_mechanics_changed",
                "no mechanical, care, severity, recovery, consequence, or relation change",
                after.WoundId);
        }
    }

    private static void ValidateStabilize(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        var evidence = ValidateStabilizePreOwnedSourceGates(
            request,
            before,
            after,
            issues);
        if (evidence is null)
            return;

        if (issues.Count == 0)
            ValidateSameRankOwnedSourceDelta(before, after, null, issues);

        var removedComplications = RemovedComplicationIds(before, after);
        var removedEffects = RemovedValues(EffectIds(before), EffectIds(after));
        var removedBlockers = RemovedValues(before.Recovery.Blockers, after.Recovery.Blockers);
        var addedComplications = AddedComplicationIds(before, after);
        var addedEffects = RemovedValues(EffectIds(after), EffectIds(before));
        var addedBlockers = RemovedValues(after.Recovery.Blockers, before.Recovery.Blockers);
        if (issues.Count == 0 &&
            (addedComplications.Count != 0 ||
            addedEffects.Count != 0 ||
            addedBlockers.Count != 0 ||
            !SameSet(removedComplications, evidence.RemovedComplicationIds) ||
            !SameSet(removedEffects, evidence.RemovedEffectIds) ||
            !SameSet(removedBlockers, evidence.RemovedRecoveryBlockers) ||
            !RetainedComplicationsUnchanged(before, after)))
        {
            Add(
                issues,
                "wound_transition_stabilize_removal_undeclared",
                "only the exact declared complication/effect/blocker removals",
                $"comp={string.Join(',', removedComplications)}/{string.Join(',', evidence.RemovedComplicationIds)};" +
                $"effects={string.Join(',', removedEffects)}/{string.Join(',', evidence.RemovedEffectIds)};" +
                $"blockers={string.Join(',', removedBlockers)}/{string.Join(',', evidence.RemovedRecoveryBlockers)};" +
                $"added={string.Join(',', addedComplications)}|{string.Join(',', addedEffects)}|{string.Join(',', addedBlockers)}");
        }
        if (issues.Count == 0 &&
            (!SameRecoveryExceptBlockers(before.Recovery, after.Recovery) ||
            !CanonicalEqual(
                before,
                after with
                {
                    Care = before.Care,
                    Complications = before.Complications,
                    Consequences = before.Consequences,
                    Recovery = before.Recovery,
                    Display = before.Display,
                    LastTransition = before.LastTransition
                })))
        {
            Add(
                issues,
                "wound_transition_stabilize_scope_invalid",
                "only care stabilization, declared removals/unlocks, and display",
                after.WoundId);
        }
    }

    private static WoundStabilizationEvidence? ValidateStabilizePreOwnedSourceGates(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundStabilizationEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundStabilizationEvidence", request.Evidence);
            return null;
        }
        if (!ActivePair(before, after) ||
            before.Care.State is "stabilized" or "healed")
        {
            Add(
                issues,
                "wound_transition_stabilize_source_invalid",
                "active unstabilized wound",
                $"{before.Lifecycle}/{before.Care.State}");
            return null;
        }
        if (!Exact(evidence.AttemptId) ||
            !ExactUnique(evidence.RemovedComplicationIds) ||
            !ExactUnique(evidence.RemovedEffectIds) ||
            !ExactUnique(evidence.RemovedRecoveryBlockers))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact stabilization attempt and unique declared removals",
                evidence.AttemptId);
            return null;
        }
        if (after.Severity.Rank != before.Severity.Rank)
        {
            Add(
                issues,
                "wound_transition_stabilize_severity_changed",
                "unchanged severity",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (!string.Equals(after.Care.State, "stabilized", StringComparison.Ordinal) ||
            after.Care.StabilizedAtTurn != request.Turn ||
            !string.Equals(after.Care.LastAttemptId, evidence.AttemptId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_stabilize_state_invalid",
                "stabilized care bound to exact turn and attempt",
                $"{after.Care.State}/{after.Care.StabilizedAtTurn}/{after.Care.LastAttemptId}");
        }
        return issues.Count == 0 ? evidence : null;
    }

    private static void ValidateTreat(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundTreatmentEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundTreatmentEvidence", request.Evidence);
            return;
        }
        if (!ActivePair(before, after))
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        if (!Exact(evidence.RouteOrGateRef) || !Exact(evidence.AttemptId))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact sealed route/gate and attempt identity",
                $"{evidence.RouteOrGateRef}/{evidence.AttemptId}");
        }
        if (evidence.Outcome is null)
        {
            DeclaredOutcomeInvalid(issues, "null");
            return;
        }
        if (!string.Equals(after.Care.LastAttemptId, evidence.AttemptId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_treatment_attempt_mismatch",
                evidence.AttemptId,
                after.Care.LastAttemptId ?? "null");
        }
        if (!evidence.Outcome.TerminalAttempt)
        {
            Add(
                issues,
                "wound_transition_treatment_attempt_not_terminal",
                "terminal accepted attempt intent even when no improvement occurs",
                "false");
        }
        ValidateDeclaredOutcome(after, evidence.Outcome, issues);
        ValidateNonHealingSeverityReduction(before, after, evidence.Outcome, issues);
        if (after.Severity.Rank > before.Severity.Rank &&
            !evidence.Outcome.AllowsWorsening)
        {
            Add(
                issues,
                "wound_transition_treatment_worsening_forbidden",
                "explicit typed worsening result",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (evidence.Outcome.Heals &&
            !FollowUpHealStageIsLegal(before, after))
        {
            Add(
                issues,
                "wound_transition_follow_up_heal_invalid",
                "severity-I source and staging state for explicit heal",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (issues.Count == 0)
        {
            if (before.Severity.Rank != after.Severity.Rank)
                ValidateFreshSeverityRootSet(before, after, issues);
            else
                ValidateSameRankOwnedSourceDelta(before, after, null, issues);
        }
        if (issues.Count == 0 && !AllowedTreatmentScope(before, after))
        {
            Add(
                issues,
                "wound_transition_treatment_scope_invalid",
                "only sealed treatment mechanical/display result",
                after.WoundId);
        }
    }

    private static void ValidateRecover(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundRecoveryEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundRecoveryEvidence", request.Evidence);
            return;
        }
        if (!ActivePair(before, after))
        {
            ActiveSourceInvalid(issues, before, after);
            return;
        }
        if (!Exact(evidence.TickKey) || !Exact(evidence.ClockOrCycleRef))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact fresh tick/cycle and clock authority",
                $"{evidence.TickKey}/{evidence.ClockOrCycleRef}");
        }
        if (evidence.Outcome is null)
        {
            DeclaredOutcomeInvalid(issues, "null");
            return;
        }
        if (string.Equals(
                before.Recovery.LastTickKey,
                evidence.TickKey,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_recovery_tick_replayed",
                "fresh tick/cycle key",
                evidence.TickKey);
        }
        if (!string.Equals(
                after.Recovery.LastTickKey,
                evidence.TickKey,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_recovery_tick_mismatch",
                evidence.TickKey,
                after.Recovery.LastTickKey ?? "null");
        }
        ValidateDeclaredOutcome(after, evidence.Outcome, issues);
        ValidateNonHealingSeverityReduction(before, after, evidence.Outcome, issues);
        if (after.Severity.Rank > before.Severity.Rank &&
            !evidence.Outcome.AllowsWorsening)
        {
            Add(
                issues,
                "wound_transition_recovery_worsening_forbidden",
                "explicit deterioration/worsening outcome",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (after.Severity.Rank == before.Severity.Rank &&
            after.Recovery.CurrentStepProgress < before.Recovery.CurrentStepProgress)
        {
            Add(
                issues,
                "wound_transition_recovery_regression",
                "non-decreasing same-step progress",
                $"{before.Recovery.CurrentStepProgress}->{after.Recovery.CurrentStepProgress}");
        }
        if (evidence.Outcome.Heals &&
            !FollowUpHealStageIsLegal(before, after))
        {
            Add(
                issues,
                "wound_transition_follow_up_heal_invalid",
                "severity-I source and staging state for explicit heal",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
        if (issues.Count == 0)
        {
            if (before.Severity.Rank != after.Severity.Rank)
                ValidateFreshSeverityRootSet(before, after, issues);
            else
                ValidateSameRankOwnedSourceDelta(before, after, null, issues);
        }
        if (issues.Count == 0 && !AllowedRecoveryScope(before, after))
        {
            Add(
                issues,
                "wound_transition_recovery_scope_invalid",
                "only sealed recovery/deterioration mechanical and display result",
                after.WoundId);
        }
    }

    private static void ValidateDeclaredOutcome(
        WoundMaterializationEnvelope after,
        WoundDeclaredTransitionOutcome outcome,
        List<ValidationIssue> issues)
    {
        if (outcome is null ||
            outcome.ResultingSeverityRank is < 1 or > 4 ||
            !Exact(outcome.ResultingCareState) ||
            outcome.ResultingRecoveryProgress < 0 ||
            !ExactUnique(outcome.ResultingComplicationIds) ||
            !ExactUnique(outcome.ResultingEffectIds) ||
            !ExactUnique(outcome.ResultingRecoveryBlockers) ||
            !ExactUnique(outcome.ResultingCompletedRouteIds))
        {
            DeclaredOutcomeInvalid(issues, after.WoundId);
            return;
        }

        if (outcome.ResultingSeverityRank != after.Severity.Rank ||
            !string.Equals(
                outcome.ResultingCareState,
                after.Care.State,
                StringComparison.Ordinal) ||
            outcome.ResultingRecoveryProgress != after.Recovery.CurrentStepProgress ||
            !SequenceEqual(outcome.ResultingComplicationIds, ComplicationIds(after)) ||
            !SequenceEqual(outcome.ResultingEffectIds, EffectIds(after)) ||
            !SequenceEqual(outcome.ResultingRecoveryBlockers, after.Recovery.Blockers) ||
            !SequenceEqual(
                outcome.ResultingCompletedRouteIds,
                after.Treatment.CompletedRouteIds))
        {
            Add(
                issues,
                "wound_transition_declared_outcome_mismatch",
                "exact sealed resulting severity/care/progress/complications/effects/blockers/routes",
                after.WoundId);
        }
    }

    private static void ValidateHeal(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundHealingEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundHealingEvidence", request.Evidence);
            return;
        }
        if (!string.Equals(before.Lifecycle, "active", StringComparison.Ordinal) ||
            before.Severity.Rank != 1 ||
            !string.Equals(after.Lifecycle, "healed", StringComparison.Ordinal) ||
            !string.Equals(after.Care.State, "healed", StringComparison.Ordinal) ||
            after.Care.ActiveCourseId is not null ||
            after.Severity.Rank != 1)
        {
            Add(
                issues,
                "wound_transition_heal_source_invalid",
                "active severity-I source and terminal healed care state",
                $"{before.Lifecycle}/{before.Severity.Rank}->{after.Lifecycle}/{after.Care.State}");
            return;
        }
        if (!Exact(evidence.AcceptedResultRef) ||
            evidence.AttemptId is not null && !Exact(evidence.AttemptId))
        {
            Add(
                issues,
                "wound_transition_evidence_invalid",
                "exact accepted healing result and optional attempt",
                evidence.AcceptedResultRef);
        }
        if (evidence.AttemptId is not null &&
            !string.Equals(after.Care.LastAttemptId, evidence.AttemptId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "wound_transition_treatment_attempt_mismatch",
                evidence.AttemptId,
                after.Care.LastAttemptId ?? "null");
        }
        ValidateLegacyDeclarations(
            before,
            after,
            evidence.Legacies,
            appendToRelations: true,
            issues);
        var normalizedAfter = after with
        {
            Lifecycle = before.Lifecycle,
            Care = before.Care,
            Relations = before.Relations,
            Display = before.Display,
            LastTransition = before.LastTransition
        };
        if (!CanonicalEqual(before, normalizedAfter))
        {
            Add(
                issues,
                "wound_transition_heal_scope_invalid",
                "only terminal care/lifecycle, declared legacies, and display",
                after.WoundId);
        }
    }

    private static void ValidateLegacy(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundLegacyEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundLegacyEvidence", request.Evidence);
            return;
        }
        if (!ExactTerminalHealAuthority(before, after, evidence.TerminalAuthorityRef))
        {
            Add(
                issues,
                "wound_transition_legacy_source_invalid",
                "healed before/after state bound to the exact immediate heal transition",
                $"{before.Lifecycle}/{after.Lifecycle}/{before.LastTransition.Kind}/" +
                $"{before.LastTransition.TransitionId}/{evidence.TerminalAuthorityRef}");
            return;
        }
        ValidateLegacyDeclarations(
            before,
            after,
            evidence.Legacies,
            appendToRelations: false,
            issues);
        if (!CanonicalEqual(before, after))
        {
            Add(
                issues,
                "wound_transition_legacy_scope_invalid",
                "post-terminal legacy audit preserves the sealed wound fingerprint",
                after.WoundId);
        }
    }

    private static void ValidateArchive(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (request.Evidence is not WoundArchiveEvidence evidence)
        {
            EvidenceKindMismatch(issues, "WoundArchiveEvidence", request.Evidence);
            return;
        }
        if (!ExactTerminalHealAuthority(before, after, evidence.TerminalAuthorityRef))
        {
            Add(
                issues,
                "wound_transition_archive_source_invalid",
                "healed before/after state bound to the exact immediate heal transition",
                $"{before.Lifecycle}/{after.Lifecycle}/{before.LastTransition.Kind}/" +
                $"{before.LastTransition.TransitionId}/{evidence.TerminalAuthorityRef}");
            return;
        }
        if (!CanonicalEqual(before, after))
        {
            Add(
                issues,
                "wound_transition_archive_scope_invalid",
                "projection-only archive with preserved terminal wound evidence",
                after.WoundId);
        }
    }

    private static void ValidateLegacyDeclarations(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        IReadOnlyList<WoundLegacyDeclaration>? declarations,
        bool appendToRelations,
        List<ValidationIssue> issues)
    {
        if (declarations is null || declarations.Any(static declaration => declaration is null))
        {
            Add(
                issues,
                "wound_transition_legacy_invalid",
                "non-null bounded typed legacy declarations",
                "null");
            return;
        }

        if (declarations.Count == 0)
        {
            if (!SequenceEqual(before.Relations.LegacyRefs, after.Relations.LegacyRefs) ||
                !SequenceEqual(
                    before.Relations.IndependentEffectRefs,
                    after.Relations.IndependentEffectRefs))
            {
                Add(
                    issues,
                    "wound_transition_legacy_invalid",
                    "no relation change without a declared legacy",
                    after.WoundId);
            }
            return;
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            var cosmetic = string.Equals(declaration.Kind, "cosmetic", StringComparison.Ordinal);
            var mechanical = string.Equals(
                declaration.Kind,
                "independent_mechanical",
                StringComparison.Ordinal);
            if (!Exact(declaration.LegacyId) ||
                !identifiers.Add(declaration.LegacyId) ||
                !Exact(declaration.ProvenanceWoundId) ||
                !string.Equals(
                    declaration.ProvenanceWoundId,
                    before.WoundId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(declaration.ReadableSummary) ||
                declaration.ReadableSummary.Length > WoundMaterializationContract.MaxReadableTextLength ||
                cosmetic && declaration.EntityKind is not null ||
                mechanical && (declaration.EntityKind is null ||
                               !MechanicalLegacyEntityKinds.Contains(declaration.EntityKind)) ||
                !cosmetic && !mechanical)
            {
                Add(
                    issues,
                    "wound_transition_legacy_invalid",
                    "unique exact cosmetic history legacy or independent mechanical entity with exact wound provenance",
                    declaration.LegacyId);
            }
        }

        if (!appendToRelations)
        {
            if (!SequenceEqual(before.Relations.LegacyRefs, after.Relations.LegacyRefs) ||
                !SequenceEqual(
                    before.Relations.IndependentEffectRefs,
                    after.Relations.IndependentEffectRefs))
            {
                Add(
                    issues,
                    "wound_transition_legacy_invalid",
                    "post-terminal audit declarations do not mutate sealed wound relations",
                    after.WoundId);
            }
            return;
        }

        var expectedCosmetic = before.Relations.LegacyRefs.Concat(
                declarations.Where(static value => value.Kind == "cosmetic")
                    .Select(static value => value.LegacyId))
            .ToArray();
        var expectedMechanical = before.Relations.IndependentEffectRefs.Concat(
                declarations.Where(static value => value.Kind == "independent_mechanical")
                    .Select(static value => value.LegacyId))
            .ToArray();
        if (!SequenceEqual(expectedCosmetic, after.Relations.LegacyRefs) ||
            !SequenceEqual(expectedMechanical, after.Relations.IndependentEffectRefs))
        {
            Add(
                issues,
                "wound_transition_legacy_invalid",
                "exact append-only cosmetic and independent-mechanical relation refs",
                after.WoundId);
        }
    }

    private static WoundTransitionReductionResult Success(
        WoundTransitionRequest request,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope after)
    {
        var intents = ImmutableArray.CreateBuilder<WoundTransitionIntent>();
        switch (request.Kind)
        {
            case "create":
                intents.Add(new WoundCarrierTransitionIntent("add", after.Owner, after.WoundId));
                break;
            case "heal":
                intents.Add(new WoundCarrierTransitionIntent("remove", before!.Owner, before.WoundId));
                break;
            case "legacy":
            case "archive":
                break;
            default:
                intents.Add(new WoundCarrierTransitionIntent("replace", after.Owner, after.WoundId));
                break;
        }

        var beforeEffects = before is null
            ? ImmutableArray<string>.Empty
            : EffectIds(before);
        var afterEffects = EffectIds(after);
        if (request.Kind == "create" && afterEffects.Length != 0)
        {
            intents.Add(new WoundEffectTransitionIntent(
                "apply",
                after.WoundId,
                beforeEffects,
                afterEffects));
        }
        else if (request.Kind == "heal" && beforeEffects.Length != 0)
        {
            intents.Add(new WoundEffectTransitionIntent(
                "remove",
                after.WoundId,
                beforeEffects,
                ImmutableArray<string>.Empty));
        }
        else if (before is not null &&
                 request.Kind is not ("legacy" or "archive" or "diagnose" or "author_alternative_treatment") &&
                 (beforeEffects.Length != 0 || afterEffects.Length != 0) &&
                 (request.Kind == "worsen" ||
                  before.Severity.Rank != after.Severity.Rank ||
                  !SequenceEqual(beforeEffects, afterEffects)))
        {
            var operation = request.Kind == "worsen" ||
                            before.Severity.Rank != after.Severity.Rank
                ? "replace"
                : "update";
            intents.Add(new WoundEffectTransitionIntent(
                operation,
                after.WoundId,
                beforeEffects,
                afterEffects));
        }

        string? attemptId = null;
        string? tickKey = null;
        WoundTransitionResult? transitionResult = null;
        switch (request.Evidence)
        {
            case WoundAlternativeTreatmentEvidence alternative:
                transitionResult = alternative.TransitionResult;
                break;
            case WoundDiagnosisEvidence diagnosis:
                attemptId = diagnosis.AttemptId;
                transitionResult = diagnosis.TransitionResult;
                intents.Add(new WoundAttemptTerminalIntent(
                    diagnosis.AttemptId, diagnosis.DiagnosisPathId, after.WoundId));
                break;
            case WoundTreatmentEvidence treatment:
                attemptId = treatment.AttemptId;
                intents.Add(new WoundAttemptTerminalIntent(
                    treatment.AttemptId,
                    treatment.RouteOrGateRef,
                    after.WoundId));
                if (treatment.Outcome.Heals)
                    intents.Add(new WoundFollowUpHealIntent(after.WoundId, treatment.AuthorityRef));
                break;
            case WoundStabilizationEvidence stabilization:
                attemptId = stabilization.AttemptId;
                break;
            case WoundRecoveryEvidence recovery:
                tickKey = recovery.TickKey;
                intents.Add(new WoundRecoverySealIntent(
                    recovery.TickKey,
                    recovery.ClockOrCycleRef,
                    after.WoundId));
                if (recovery.Outcome.Heals)
                    intents.Add(new WoundFollowUpHealIntent(after.WoundId, recovery.AuthorityRef));
                break;
            case WoundHealingEvidence healing:
                attemptId = healing.AttemptId;
                AddLegacyIntents(intents, healing.Legacies);
                break;
            case WoundLegacyEvidence legacy:
                AddLegacyIntents(intents, legacy.Legacies);
                break;
            case WoundArchiveEvidence archive:
                intents.Add(new WoundArchiveProjectionIntent(
                    after.WoundId,
                    archive.TerminalAuthorityRef));
                break;
        }

        var beforeFingerprint = before is null
            ? WoundHistoryState.ComputeNonexistentBeforeFingerprint(after.WoundId)
            : WoundIdentityState.ComputeSemanticFingerprint(before);
        var afterFingerprint = WoundIdentityState.ComputeSemanticFingerprint(after);
        intents.Add(new WoundTransitionHistoryIntent(
            request.TransitionId,
            after.WoundId,
            request.Kind,
            request.OperationKey,
            request.EventRef,
            request.Turn,
            beforeFingerprint,
            afterFingerprint,
            attemptId,
            tickKey,
            request.Kind == "heal",
            transitionResult));
        return new WoundTransitionReductionResult(
            after,
            intents.ToImmutable(),
            ImmutableArray<ValidationIssue>.Empty);
    }

    private static void AddLegacyIntents(
        ImmutableArray<WoundTransitionIntent>.Builder intents,
        IReadOnlyList<WoundLegacyDeclaration>? declarations)
    {
        if (declarations is null)
            return;
        foreach (var declaration in declarations)
        {
            if (declaration is null)
                continue;
            if (string.Equals(declaration.Kind, "cosmetic", StringComparison.Ordinal))
            {
                intents.Add(new WoundCosmeticLegacyIntent(
                    declaration.LegacyId,
                    declaration.ProvenanceWoundId,
                    declaration.ReadableSummary));
            }
            else
            {
                intents.Add(new WoundIndependentMechanicalLegacyIntent(
                    declaration.LegacyId,
                    declaration.EntityKind!,
                    declaration.ProvenanceWoundId,
                    declaration.ReadableSummary));
            }
        }
    }

    private static bool AllowedTreatmentScope(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after)
    {
        if (!SameRecoveryPolicyForOutcome(before.Recovery, after.Recovery))
            return false;
        var normalizedAfter = after with
        {
            Severity = before.Severity,
            Care = before.Care,
            Complications = before.Complications,
            Consequences = before.Consequences,
            Treatment = after.Treatment with
            {
                CompletedRouteIds = before.Treatment.CompletedRouteIds
            },
            Recovery = before.Recovery,
            Display = before.Display,
            LastTransition = before.LastTransition
        };
        return CanonicalEqual(before, normalizedAfter);
    }

    private static bool AllowedRecoveryScope(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after)
    {
        if (!SameRecoveryPolicyForOutcome(before.Recovery, after.Recovery))
            return false;
        var normalizedAfter = after with
        {
            Severity = before.Severity,
            Care = before.Care,
            Complications = before.Complications,
            Consequences = before.Consequences,
            Recovery = before.Recovery,
            Display = before.Display,
            LastTransition = before.LastTransition
        };
        return CanonicalEqual(before, normalizedAfter);
    }

    private static bool SameRecoveryPolicyExceptStep(WoundRecovery before, WoundRecovery after) =>
        string.Equals(before.Mode, after.Mode, StringComparison.Ordinal) &&
        string.Equals(before.ClockKind, after.ClockKind, StringComparison.Ordinal) &&
        before.Cadence == after.Cadence &&
        string.Equals(before.LastTickKey, after.LastTickKey, StringComparison.Ordinal) &&
        SequenceEqual(before.Blockers, after.Blockers) &&
        before.CarryOverflow == after.CarryOverflow &&
        JsonEqual(before.DeteriorationPolicy, after.DeteriorationPolicy);

    private static bool RecoveryEqual(WoundRecovery before, WoundRecovery after) =>
        SameRecoveryExceptBlockers(before, after) &&
        SequenceEqual(before.Blockers, after.Blockers);

    private static bool SameRecoveryPolicyForOutcome(WoundRecovery before, WoundRecovery after) =>
        string.Equals(before.Mode, after.Mode, StringComparison.Ordinal) &&
        string.Equals(before.ClockKind, after.ClockKind, StringComparison.Ordinal) &&
        before.Cadence == after.Cadence &&
        before.CarryOverflow == after.CarryOverflow &&
        JsonEqual(before.DeteriorationPolicy, after.DeteriorationPolicy);

    private static bool SameRecoveryExceptBlockers(WoundRecovery before, WoundRecovery after) =>
        string.Equals(before.Mode, after.Mode, StringComparison.Ordinal) &&
        string.Equals(before.ClockKind, after.ClockKind, StringComparison.Ordinal) &&
        before.Cadence == after.Cadence &&
        before.CurrentStepProgress == after.CurrentStepProgress &&
        before.CurrentStepThreshold == after.CurrentStepThreshold &&
        string.Equals(before.LastTickKey, after.LastTickKey, StringComparison.Ordinal) &&
        before.CarryOverflow == after.CarryOverflow &&
        JsonEqual(before.DeteriorationPolicy, after.DeteriorationPolicy);

    private static bool JsonEqual(
        System.Text.Json.JsonElement? left,
        System.Text.Json.JsonElement? right) =>
        left.HasValue == right.HasValue &&
        (!left.HasValue || System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(left.Value.GetRawText()),
            System.Text.Json.Nodes.JsonNode.Parse(right!.Value.GetRawText())));

    private static bool RetainedComplicationsUnchanged(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after)
    {
        foreach (var prior in before.Complications)
        {
            var current = after.Complications.SingleOrDefault(value =>
                string.Equals(value.ComplicationId, prior.ComplicationId, StringComparison.Ordinal));
            if (current is not null && !ComplicationEqual(prior, current))
                return false;
        }
        return true;
    }

    private static bool ComplicationEqual(WoundComplication left, WoundComplication right) =>
        string.Equals(left.ComplicationId, right.ComplicationId, StringComparison.Ordinal) &&
        string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
        string.Equals(left.State, right.State, StringComparison.Ordinal) &&
        string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
        left.TreatmentDifficultyModifier == right.TreatmentDifficultyModifier &&
        SequenceEqual(left.OwnedEffectIds, right.OwnedEffectIds) &&
        string.Equals(left.Visibility, right.Visibility, StringComparison.Ordinal);

    private static bool ComplicationSequenceEqual(
        IReadOnlyList<WoundComplication>? left,
        IReadOnlyList<WoundComplication>? right)
    {
        if (left is null || right is null || left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index] is null ||
                right[index] is null ||
                !ComplicationEqual(left[index], right[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidateSameRankOwnedSourceDelta(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        WoundComplication? addedComplication,
        List<ValidationIssue> issues)
    {
        var prior = BuildOwnedSourceTransitionView(before);
        var current = BuildOwnedSourceTransitionView(after);
        var priorRoots = prior.RootEffectIds.ToHashSet(StringComparer.Ordinal);
        var currentRoots = current.RootEffectIds.ToHashSet(StringComparer.Ordinal);
        var retainedRoots = priorRoots
            .Intersect(currentRoots, StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        foreach (var effectId in retainedRoots)
        {
            if (!string.Equals(
                    prior.DefinitionKeyByEffectId[effectId],
                    current.DefinitionKeyByEffectId[effectId],
                    StringComparison.Ordinal) ||
                !string.Equals(
                    prior.OwnershipDomainByEffectId[effectId],
                    current.OwnershipDomainByEffectId[effectId],
                    StringComparison.Ordinal))
            {
                AddOwnedSourceIssue(
                    issues,
                    "wound_transition_effect_binding_changed",
                    "retained root preserves its exact definition and base_wound or complication ownership domain",
                    effectId);
                return;
            }
        }

        foreach (var pair in prior.EffectIdByRootDefinitionKey.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (current.EffectIdByRootDefinitionKey.TryGetValue(pair.Key, out var currentEffectId) &&
                !string.Equals(pair.Value, currentEffectId, StringComparison.Ordinal))
            {
                AddOwnedSourceIssue(
                    issues,
                    "wound_transition_effect_binding_changed",
                    "one retained root definition remains bound to its exact effectId",
                    $"{pair.Value}->{currentEffectId}");
                return;
            }
        }

        if (addedComplication is not null)
        {
            var missingPriorRoot = prior.RootEffectIds.FirstOrDefault(effectId =>
                !currentRoots.Contains(effectId));
            if (missingPriorRoot is not null)
            {
                AddOwnedSourceIssue(
                    issues,
                    "wound_transition_effect_binding_changed",
                    "same-rank complication addition preserves every prior root binding",
                    missingPriorRoot);
                return;
            }
        }

        var priorRetainedEntries = before.Consequences.Entries
            .Where(entry => retainedRoots.Contains(entry.EffectId, StringComparer.Ordinal))
            .ToArray();
        var currentRetainedEntries = after.Consequences.Entries
            .Where(entry => retainedRoots.Contains(entry.EffectId, StringComparer.Ordinal))
            .ToArray();
        if (priorRetainedEntries.Length != currentRetainedEntries.Length ||
            !priorRetainedEntries.Select(static entry => entry.EffectId).SequenceEqual(
                currentRetainedEntries.Select(static entry => entry.EffectId),
                StringComparer.Ordinal))
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_effect_binding_changed",
                "retained roots preserve reciprocal consequence multiplicity and relative ownership order",
                string.Join(',', retainedRoots));
            return;
        }

        foreach (var definitionKey in prior.DefinitionByKey.Keys
                     .Intersect(current.DefinitionByKey.Keys, StringComparer.Ordinal)
                     .OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!string.Equals(
                    prior.DefinitionByKey[definitionKey].CanonicalJson,
                    current.DefinitionByKey[definitionKey].CanonicalJson,
                    StringComparison.Ordinal))
            {
                AddOwnedSourceIssue(
                    issues,
                    "wound_transition_owned_source_graph_changed",
                    "retained definition preserves its exact canonical source graph body",
                    definitionKey);
                return;
            }
        }

        if (addedComplication is not null)
        {
            if (!ValidateAddedComplicationOwnedSourceDelta(
                    prior,
                    current,
                    addedComplication,
                    issues))
            {
                return;
            }
        }
        else if (!ValidateRemovalOwnedSourceDelta(prior, current, before, after, issues))
        {
            return;
        }

        if (before.Consequences.SlotBudget != after.Consequences.SlotBudget ||
            after.Consequences.SlotsUsed != after.Consequences.Entries.Count ||
            after.Consequences.Entries
                .Select(static entry => entry.Slot)
                .Where((slot, index) => slot != index + 1)
                .Any())
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_owned_source_graph_invalid",
                "same-rank source delta preserves slot budget and emits contiguous one-based recomputed slotsUsed",
                $"budget={before.Consequences.SlotBudget}->{after.Consequences.SlotBudget};" +
                $"slotsUsed={after.Consequences.SlotsUsed};entries={after.Consequences.Entries.Count}");
            return;
        }

        for (var index = 0; index < priorRetainedEntries.Length; index++)
        {
            if (!ConsequencePayloadEqual(priorRetainedEntries[index], currentRetainedEntries[index]))
            {
                AddOwnedSourceIssue(
                    issues,
                    "wound_transition_retained_consequence_changed",
                    "retained consequence preserves exact effectId/profile/summary payload and relative order",
                    priorRetainedEntries[index].EffectId);
                return;
            }
        }
    }

    private static bool ValidateAddedComplicationOwnedSourceDelta(
        OwnedSourceTransitionView prior,
        OwnedSourceTransitionView current,
        WoundComplication addedComplication,
        List<ValidationIssue> issues)
    {
        var priorRoots = prior.RootEffectIds.ToHashSet(StringComparer.Ordinal);
        var addedRoots = current.RootEffectIds
            .Where(effectId => !priorRoots.Contains(effectId))
            .ToImmutableArray();
        if (!SameSet(addedRoots, addedComplication.OwnedEffectIds) ||
            addedRoots.Any(effectId =>
                !string.Equals(
                    current.OwnershipDomainByEffectId[effectId],
                    addedComplication.ComplicationId,
                    StringComparison.Ordinal)))
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_complication_effect_binding_invalid",
                "new root set equals the declared complication-owned set in one exact ownership domain",
                $"roots={string.Join(',', addedRoots)};owned={string.Join(',', addedComplication.OwnedEffectIds)}");
            return false;
        }

        var expectedDefinitions = prior.DefinitionByKey.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var effectId in addedRoots)
            expectedDefinitions.UnionWith(current.ReachableDefinitionKeysByEffectId[effectId]);
        if (!expectedDefinitions.SetEquals(current.DefinitionByKey.Keys) ||
            addedRoots.Any(effectId =>
                current.SlotsByEffectId[effectId].Length == 0 &&
                !RootReachabilityContainsWoundMarker(current, effectId)))
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_complication_effect_binding_invalid",
                "new complication contributes only its complete reachable branches and every non-marker root has reciprocal slots",
                addedComplication.ComplicationId);
            return false;
        }
        return true;
    }

    private static bool ValidateRemovalOwnedSourceDelta(
        OwnedSourceTransitionView prior,
        OwnedSourceTransitionView current,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        var priorRoots = prior.RootEffectIds.ToHashSet(StringComparer.Ordinal);
        var addedRoot = current.RootEffectIds.FirstOrDefault(effectId =>
            !priorRoots.Contains(effectId));
        if (addedRoot is not null)
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_owned_source_graph_invalid",
                "unchanged-severity stabilization/treatment/recovery is removal-only",
                addedRoot);
            return false;
        }

        var expectedDefinitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effectId in current.RootEffectIds)
            expectedDefinitions.UnionWith(prior.ReachableDefinitionKeysByEffectId[effectId]);
        if (!expectedDefinitions.SetEquals(current.DefinitionByKey.Keys))
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_owned_source_graph_invalid",
                "after definitions equal the exact prior graph subset reachable from remaining roots",
                string.Join(',', current.DefinitionByKey.Keys.OrderBy(
                    static value => value,
                    StringComparer.Ordinal)));
            return false;
        }

        var currentRoots = current.RootEffectIds.ToHashSet(StringComparer.Ordinal);
        var expectedEntries = before.Consequences.Entries
            .Where(entry => currentRoots.Contains(entry.EffectId))
            .ToArray();
        if (expectedEntries.Length != after.Consequences.Entries.Count ||
            !expectedEntries.Select(static entry => entry.EffectId).SequenceEqual(
                after.Consequences.Entries.Select(static entry => entry.EffectId),
                StringComparer.Ordinal))
        {
            AddOwnedSourceIssue(
                issues,
                "wound_transition_owned_source_graph_invalid",
                "after slots equal the exact retained-root projection of prior slots",
                string.Join(',', after.Consequences.Entries.Select(static entry => entry.EffectId)));
            return false;
        }
        return true;
    }

    private static void ValidateFreshSeverityRootSet(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        List<ValidationIssue> issues)
    {
        if (before.Severity.Rank == after.Severity.Rank)
            return;
        var priorRoots = EffectIds(before);
        var currentRoots = EffectIds(after);
        var exactCollision = priorRoots.Intersect(currentRoots, StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        var priorConfusable = priorRoots.ToDictionary(
            MortalLocationIdentityState.BuildConfusableKey,
            static value => value,
            StringComparer.Ordinal);
        var confusableCollision = currentRoots
            .Select(effectId => (
                EffectId: effectId,
                Key: MortalLocationIdentityState.BuildConfusableKey(effectId)))
            .Where(pair => priorConfusable.ContainsKey(pair.Key))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (exactCollision is null && confusableCollision.EffectId is null)
            return;

        AddOwnedSourceIssue(
            issues,
            "wound_transition_severity_root_identity_reused",
            "severity change uses a fresh exact and confusable root effect identity set",
            exactCollision ??
            $"{priorConfusable[confusableCollision.Key]}~{confusableCollision.EffectId}");
    }

    private static OwnedSourceTransitionView BuildOwnedSourceTransitionView(
        WoundMaterializationEnvelope wound)
    {
        var definitions = ImmutableDictionary.CreateBuilder<
            string,
            WoundOwnedEffectDefinitionFact>(
            StringComparer.Ordinal);
        foreach (var fact in wound.Consequences.OwnedEffectSources.DefinitionFacts)
            definitions.Add(fact.DefinitionKey, fact);

        var rootDefinitions = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);
        var effectsByRootDefinition = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            rootDefinitions.Add(binding.EffectId, binding.DefinitionKey);
            effectsByRootDefinition.Add(binding.DefinitionKey, binding.EffectId);
        }

        var rootEffectIds = rootDefinitions.Keys
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        var slots = ImmutableDictionary.CreateBuilder<
            string,
            ImmutableArray<WoundConsequenceEntry>>(StringComparer.Ordinal);
        var ownershipDomains = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);
        foreach (var effectId in rootEffectIds)
        {
            slots.Add(
                effectId,
                wound.Consequences.Entries.Where(entry => string.Equals(
                        entry.EffectId,
                        effectId,
                        StringComparison.Ordinal))
                    .ToImmutableArray());
            ownershipDomains.Add(effectId, "base_wound");
        }
        foreach (var complication in wound.Complications)
        {
            foreach (var effectId in complication.OwnedEffectIds)
                ownershipDomains[effectId] = complication.ComplicationId;
        }

        var immutableDefinitions = definitions.ToImmutable();
        var reachable = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
            StringComparer.Ordinal);
        foreach (var effectId in rootEffectIds)
        {
            reachable.Add(
                effectId,
                ReadReachableDefinitionKeys(
                    rootDefinitions[effectId],
                    immutableDefinitions));
        }

        return new OwnedSourceTransitionView(
            rootEffectIds,
            rootDefinitions.ToImmutable(),
            effectsByRootDefinition.ToImmutable(),
            immutableDefinitions,
            reachable.ToImmutable(),
            slots.ToImmutable(),
            ownershipDomains.ToImmutable());
    }

    private static ImmutableArray<string> ReadReachableDefinitionKeys(
        string rootDefinitionKey,
        IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> definitions)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(rootDefinitionKey);
        while (pending.Count != 0)
        {
            var definitionKey = pending.Pop();
            if (!reachable.Add(definitionKey) ||
                !definitions.TryGetValue(definitionKey, out var definition))
            {
                continue;
            }
            foreach (var targetDefinitionKey in definition.ApplyDefinitionTargets)
                pending.Push(targetDefinitionKey);
        }
        return reachable.OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool RootReachabilityContainsWoundMarker(
        OwnedSourceTransitionView view,
        string effectId) =>
        view.ReachableDefinitionKeysByEffectId.TryGetValue(effectId, out var reachable) &&
        reachable.Any(definitionKey =>
            view.DefinitionByKey.TryGetValue(definitionKey, out var definition) &&
            definition.ContainsWoundConsequenceMarker);

    private static bool ConsequencePayloadEqual(
        WoundConsequenceEntry left,
        WoundConsequenceEntry right) =>
        string.Equals(left.ProfileKey, right.ProfileKey, StringComparison.Ordinal) &&
        string.Equals(left.EffectId, right.EffectId, StringComparison.Ordinal) &&
        string.Equals(left.ReadableSummary, right.ReadableSummary, StringComparison.Ordinal);

    private static void AddOwnedSourceIssue(
        ICollection<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        Add(issues, code, expected, actual);

    private static bool DisplayEqual(WoundDisplay left, WoundDisplay right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
        SequenceEqual(left.VisibleSymptoms, right.VisibleSymptoms) &&
        string.Equals(left.Prognosis, right.Prognosis, StringComparison.Ordinal) &&
        string.Equals(left.Visibility, right.Visibility, StringComparison.Ordinal) &&
        string.Equals(
            left.AcquisitionNarration,
            right.AcquisitionNarration,
            StringComparison.Ordinal);

    private static bool TryParseDiagnosisReveal(
        string? value,
        out string kind,
        out string identifier)
    {
        kind = string.Empty;
        identifier = string.Empty;
        if (value is null)
            return false;
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            return false;
        kind = value[..separator];
        identifier = value[(separator + 1)..];
        return kind is "route" or "complication" && Exact(identifier);
    }

    private static bool FollowUpHealStageIsLegal(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        before.Severity.Rank is 1 or 2 && after.Severity.Rank == 1;

    private static void ValidateNonHealingSeverityReduction(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        WoundDeclaredTransitionOutcome outcome,
        List<ValidationIssue> issues)
    {
        if (!outcome.Heals && before.Severity.Rank - after.Severity.Rank > 2)
        {
            Add(
                issues,
                "wound_transition_severity_reduction_exceeds_limit",
                "non-healing treatment or recovery reduces severity by at most two steps",
                $"{before.Severity.Rank}->{after.Severity.Rank}");
        }
    }

    private static ImmutableArray<string> ComplicationIds(WoundMaterializationEnvelope wound) =>
        wound.Complications.Select(static value => value.ComplicationId).ToImmutableArray();

    private static ImmutableArray<string> EffectIds(WoundMaterializationEnvelope wound) =>
        wound.Consequences.OwnedEffectSources.RootBindings
            .Select(static value => value.EffectId)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();

    private static IReadOnlyList<string> RemovedComplicationIds(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        before.Complications.Select(static value => value.ComplicationId)
            .Except(
                after.Complications.Select(static value => value.ComplicationId),
                StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> AddedComplicationIds(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        after.Complications.Select(static value => value.ComplicationId)
            .Except(
                before.Complications.Select(static value => value.ComplicationId),
                StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> RemovedValues(
        IEnumerable<string> before,
        IEnumerable<string> after) =>
        before.Except(after, StringComparer.Ordinal).ToArray();

    private static bool CanonicalEqual(
        WoundMaterializationEnvelope left,
        WoundMaterializationEnvelope right) =>
        string.Equals(
            WoundMaterializationContract.SerializeCanonical(left),
            WoundMaterializationContract.SerializeCanonical(right),
            StringComparison.Ordinal);

    private static bool ActivePair(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        string.Equals(before.Lifecycle, "active", StringComparison.Ordinal) &&
        string.Equals(after.Lifecycle, "active", StringComparison.Ordinal) &&
        !string.Equals(before.Care.State, "healed", StringComparison.Ordinal) &&
        !string.Equals(after.Care.State, "healed", StringComparison.Ordinal);

    private static bool TerminalPair(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        string.Equals(before.Lifecycle, "healed", StringComparison.Ordinal) &&
        string.Equals(after.Lifecycle, "healed", StringComparison.Ordinal) &&
        string.Equals(before.Care.State, "healed", StringComparison.Ordinal) &&
        string.Equals(after.Care.State, "healed", StringComparison.Ordinal);

    private static bool ExactTerminalHealAuthority(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        string? terminalAuthorityRef) =>
        TerminalPair(before, after) &&
        string.Equals(before.LastTransition.Kind, "heal", StringComparison.Ordinal) &&
        string.Equals(
            terminalAuthorityRef,
            before.LastTransition.TransitionId,
            StringComparison.Ordinal);

    private static void ActiveSourceInvalid(
        List<ValidationIssue> issues,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        Add(
            issues,
            "wound_transition_active_source_invalid",
            "active before and active after wound state",
            $"{before.Lifecycle}/{before.Care.State}->{after.Lifecycle}/{after.Care.State}");

    private static void ValidateLegalCoordinate(
        WoundMaterializationEnvelope wound,
        string member,
        List<ValidationIssue> issues)
    {
        var realmDomainLegal = wound.Classification.Domain switch
        {
            "physical" => string.Equals(wound.Owner.Realm, "mortal_world", StringComparison.Ordinal),
            "spiritual" => wound.Owner.Realm is "chaos_sea" or "shining_abode",
            _ => false
        };
        if (!realmDomainLegal)
        {
            Add(
                issues,
                "wound_transition_realm_domain_invalid",
                "physical Mortal or spiritual Chaos Sea/Shining Abode coordinate",
                $"{member}:{wound.Owner.Realm}/{wound.Classification.Domain}");
            return;
        }

        var ownerFamilyLegal = wound.Owner.Realm switch
        {
            "mortal_world" => wound.Owner.OwnerKind is
                "player" or "npc" or "combatant" or "combatant_member",
            "chaos_sea" or "shining_abode" => wound.Owner.OwnerKind is
                "player_soul" or "guardian" or "resident" or "radiant_actor" or
                "afterlife_actor",
            _ => false
        };
        if (!ownerFamilyLegal)
        {
            Add(
                issues,
                "wound_transition_owner_coordinate_invalid",
                "owner kind belongs to the exact Mortal or afterlife realm family",
                $"{member}:{wound.Owner.Realm}/{wound.Owner.OwnerKind}");
        }
    }

    private static int SeverityRank(string value) => value switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => 0
    };

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool Fingerprint(string? value) =>
        ResourceMaterializationContract.IsAuthorityFingerprint(value);

    private static bool ExactUnique(IEnumerable<string>? values)
    {
        if (values is null)
            return false;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value => Exact(value) && unique.Add(value));
    }

    private static bool SameSet(IEnumerable<string>? left, IEnumerable<string>? right) =>
        left is not null &&
        right is not null &&
        left.ToHashSet(StringComparer.Ordinal).SetEquals(right);

    private static bool SequenceEqual(IEnumerable<string>? left, IEnumerable<string>? right) =>
        left is not null &&
        right is not null &&
        left.SequenceEqual(right, StringComparer.Ordinal);

    private static void DeclaredOutcomeInvalid(
        List<ValidationIssue> issues,
        string actual) =>
        Add(
            issues,
            "wound_transition_declared_outcome_invalid",
            "non-null bounded typed outcome with exact unique result lists",
            actual);

    private static void EvidenceKindMismatch(
        List<ValidationIssue> issues,
        string expected,
        WoundTransitionEvidence actual) =>
        Add(
            issues,
            "wound_transition_evidence_kind_mismatch",
            expected,
            actual.GetType().Name);

    private static WoundTransitionReductionResult Failure(IEnumerable<ValidationIssue> issues) =>
        new(
            null,
            ImmutableArray<WoundTransitionIntent>.Empty,
            issues.ToImmutableArray());

    private static string Path(string member) => $"woundTransition.{member}";

    private static void Add(
        ICollection<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            Path("request"),
            IssueSeverity.Error,
            "Wound transition violates the closed deterministic reducer contract.",
            code: code,
            actor: "Client",
            section: "wound_transition",
            expected: expected,
            actual: actual,
            repairHint: "Rebuild one exact sealed version-1 wound transition without implicit lifecycle or authority changes."));
}
