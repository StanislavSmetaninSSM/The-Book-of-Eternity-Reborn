using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    // Pure reducer facts, not accepted/cache/publication authority. Joint finalization
    // must collect both independently admitted contributions BEFORE building roots.
    private sealed record WoundTransitionContributions(
        IReadOnlyList<FinalizedWoundTransition> Transitions,
        IReadOnlyList<WoundTransitionIntent> Intents,
        IReadOnlyList<WoundHistoryTransition> HistoryRows);
    private sealed record WoundTransitionContributionsResult(
        WoundTransitionContributions? Value, IReadOnlyList<ValidationIssue> Issues);

    private static WoundTransitionContributionsResult ReduceAcquisitionContributions(
        WoundPreparedAcceptedTurnPlan prepared, WoundCarrierCatalog carrierCatalog,
        WoundIdentityState identityBefore, WoundHistoryState historyBefore,
        IReadOnlyList<EffectAcceptedApplicationResult> applications, int acquisitionCount)
    {
        var applicationByRef = applications.ToDictionary(
            static value => value.ApplicationRef,
            StringComparer.Ordinal);
        var finalTransitions = new List<FinalizedWoundTransition>(
            acquisitionCount);
        var intents = new List<WoundTransitionIntent>();
        var historyRows = new List<WoundHistoryTransition>();
        for (var index = 0; index < acquisitionCount; index++)
        {
            var preparedWound = prepared.PreparedWounds[index];
            var batch = prepared.EffectOperationBatches[index];
            var finalized = BuildFinalWound(preparedWound, batch, applicationByRef);
            if (finalized.Wound is null || finalized.Issues.Count != 0)
                return new WoundTransitionContributionsResult(null, finalized.Issues);

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
                    !identityBefore.TryGetEntry(
                        finalized.Wound.WoundId,
                        out var identityEntry) ||
                    WoundIdentityState.ValidateActiveAgreement(
                        identityEntry,
                        matches[0].Wound,
                        PlanPath + ".beforeWound").Count != 0)
                {
                    return FailedContributions(
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
                return FailedContributions(
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
                return new WoundTransitionContributionsResult(
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
                historyBefore.NextOrdinal + historyRows.Count,
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

        return new(new WoundTransitionContributions(finalTransitions.ToArray(),
            intents.ToArray(), historyRows.ToArray()), Array.Empty<ValidationIssue>());
    }

    private static WoundTransitionContributionsResult ReduceTreatmentContribution(
        MortalWoundTreatmentOutcomePreparation outcomePreparation,
        MortalWoundTreatmentResolution resolution, string transitionId,
        MortalWoundTreatmentPersistedResult persistedResult,
        WoundEffectOperationBatch? reductionBatch,
        IReadOnlyList<EffectAcceptedApplicationResult> applications,
        WoundMaterializationEnvelope before, int historyOrdinal,
        Action<string>? setStage = null)
    {
        setStage?.Invoke("transition_reduction");
        if (outcomePreparation.PreparesTerminalHeal)
            return new(null, new[] { NewIssue("wound_plan_terminal_execution_required",
                "A terminal treatment contribution must consume its actual ordered treat/heal execution.",
                "sealed linked terminal stages", "scalar-only contribution") });
        var coordinates = resolution.Coordinates;
        var applicationByRef = new Dictionary<
            string,
            EffectAcceptedApplicationResult>(StringComparer.Ordinal);
        foreach (var application in applications)
            applicationByRef.Add(application.ApplicationRef, application);
        var outcomePublication =
            MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
                outcomePreparation,
                resolution,
                reductionBatch,
                applicationByRef);
        if (!outcomePublication.IsValid)
        {
            return new WoundTransitionContributionsResult(
                null,
                outcomePublication.Issues.Select(CloneIssue).ToArray());
        }
        var after = outcomePublication.After!;
        var outcome = outcomePublication.DeclaredOutcome!;
        var reduction = WoundTransitionReducer.Reduce(
            new WoundTransitionRequest(
                "treat",
                transitionId,
                coordinates.OperationKey,
                coordinates.EventRef,
                coordinates.Turn,
                before,
                after,
                new WoundTreatmentEvidence(
                    resolution.ResolutionAuthorityFingerprint,
                    WoundIdentityState.ComputeSemanticFingerprint(before),
                    WoundIdentityState.ComputeSemanticFingerprint(after),
                    coordinates.RouteId,
                    coordinates.AttemptId,
                    outcome)));
        if (!reduction.IsValid)
        {
            return new WoundTransitionContributionsResult(
                null,
                reduction.Issues.Select(CloneIssue).ToArray());
        }

        setStage?.Invoke("history_intent");
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
            historyOrdinal,
            proposed.LastTransition.Ordinal,
            historyIntent.Kind,
            historyIntent.Turn,
            historyIntent.EventRef,
            historyIntent.OperationKey,
            historyIntent.BeforeFingerprint,
            historyIntent.AfterFingerprint,
            resolution.RequestFingerprint,
            historyIntent.AttemptId,
            CourseId: resolution.CourseId,
            CourseMilestoneOrdinal: resolution.CourseMilestoneOrdinal,
            CycleKey: null,
            PaymentFingerprint: null,
            WoundHistoryState.ComputeOutputFingerprint(
                historyIntent.OperationKey,
                historyIntent.EventRef,
                WoundAcceptedTurnPlanner.TreatmentPublicationSummary),
            WoundAcceptedTurnPlanner.TreatmentPublicationSummary,
            Terminal: false,
            TransitionResult: persistedResult);
        return new(new WoundTransitionContributions(transitions, intents, [historyRow]),
            Array.Empty<ValidationIssue>());
    }

    private static WoundStateReductionResult AssembleWoundContributions(
        WoundOperationBeforeData operationBefore, WoundHistoryState historyBefore,
        WoundTransitionContributions contribution)
    {
        var finalTransitions = contribution.Transitions;
        var intents = contribution.Intents;
        var historyRows = contribution.HistoryRows;
        var contributions = BuildCarrierContributions(
            operationBefore.WoundCarriers!,
            finalTransitions);
        var identityAfter = BuildIdentityAfterImage(
            operationBefore.WoundIdentity!,
            finalTransitions);
        if (identityAfter.State is null || identityAfter.Issues.Count != 0)
        {
            return new WoundStateReductionResult(
                null,
                identityAfter.Issues.Select(CloneIssue).ToArray());
        }
        var identityAfterJson = JsonNode.Parse(
            WoundIdentityState.SerializeCanonical(identityAfter.State))!
            .AsObject();

        var historyAfter = WoundHistoryState.CreateValidated(
            historyBefore.NextOrdinal + historyRows.Count,
            historyBefore.Transitions.Concat(historyRows));
        if (historyAfter.State is null || historyAfter.Issues.Count != 0)
        {
            return new WoundStateReductionResult(
                null,
                historyAfter.Issues.Select(CloneIssue).ToArray());
        }
        var historyAfterJson = JsonNode.Parse(
            WoundHistoryState.SerializeCanonical(historyAfter.State))!
            .AsObject();

        var carriersAfter = ApplyFinalWounds(
            operationBefore.WoundCarriers!,
            finalTransitions);
        var carrierAfterCatalog = WoundCarrierCatalog.Build(carriersAfter);
        var afterAgreement = historyAfter.State.ValidateAgreement(
            identityAfter.State,
            carrierAfterCatalog);
        if (carrierAfterCatalog.Issues.Count != 0 || afterAgreement.Count != 0)
        {
            return new WoundStateReductionResult(
                null,
                carrierAfterCatalog.Issues.Concat(afterAgreement)
                    .Select(CloneIssue)
                    .ToArray());
        }

        return new WoundStateReductionResult(new WoundStateReduction(
            carriersAfter, identityAfterJson, historyAfterJson, contributions, intents),
            Array.Empty<ValidationIssue>());
    }

    private static WoundTransitionContributionsResult CombineJointRepairContributions(
        WoundTransitionContributions acquisition, WoundTransitionContributions treatment,
        int originalNextOrdinal)
    {
        if (acquisition.Transitions.Count != 1 || acquisition.HistoryRows.Count != 1 ||
            treatment.Transitions.Count != 1 || treatment.HistoryRows.Count != 1)
            return FailedContributions("wound_plan_joint_contribution_set_invalid",
                "Joint repair requires one independently reduced CREATE and one treatment.",
                "one typed contribution from each owner", "missing or repeated contribution");
        var create = acquisition.Transitions[0];
        var treat = treatment.Transitions[0];
        var createRow = acquisition.HistoryRows[0];
        var treatRow = treatment.HistoryRows[0];
        if (create.Before is not null || createRow.Kind != "create" ||
            create.After.LastTransition.Kind != "create" || treat.Before is null ||
            treatRow.Kind != "treat" || treat.After.LastTransition.Kind != "treat" ||
            create.After.WoundId == treat.After.WoundId ||
            createRow.TransitionId == treatRow.TransitionId ||
            createRow.TransitionId != create.After.LastTransition.TransitionId ||
            treatRow.TransitionId != treat.After.LastTransition.TransitionId ||
            createRow.Ordinal != originalNextOrdinal || treatRow.Ordinal != checked(originalNextOrdinal + 1) ||
            createRow.Turn != treatRow.Turn || createRow.WoundId != create.After.WoundId ||
            treatRow.WoundId != treat.After.WoundId || treatRow.TreatmentResult is null ||
            create.After.Owner != treat.After.Owner || create.After.Owner.Realm != "mortal_world" ||
            create.After.Classification.Domain != "physical" ||
            create.After.Owner.OwnerKind != "player" || create.After.Owner.OwnerId != "player_current")
            return FailedContributions("wound_plan_joint_contribution_set_invalid",
                "Joint repair must preserve distinct player wounds and acquisition-then-treatment history.",
                "exact independent typed transitions and consecutive original ordinals", "changed contribution graph");
        return new(new WoundTransitionContributions(
            acquisition.Transitions.Concat(treatment.Transitions).ToArray(),
            acquisition.Intents.Concat(treatment.Intents).ToArray(),
            acquisition.HistoryRows.Concat(treatment.HistoryRows).ToArray()), Array.Empty<ValidationIssue>());
    }

    private static WoundTransitionContributionsResult FailedContributions(
        string code, string message, string expected, string actual) =>
        new(null, FailedReduction(code, message, expected, actual).Issues);
}
