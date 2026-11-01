using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// One actually executed treatment/heal boundary. No recovery tick or admission
/// is fabricated; every image and effect result belongs to the existing workspace.
/// </summary>
internal sealed record MortalTreatmentExecutedBoundary(WoundMaterializationEnvelope Before,
    WoundMaterializationEnvelope After, string OperationKey, string EventRef,
    IReadOnlyList<EffectAcceptedApplicationResult> Applications,
    IReadOnlyList<EffectAcceptedTerminationResult> Terminations, IReadOnlyList<WoundTransitionIntent> Intents)
{
    internal MortalTreatmentExecutedBoundary DetachedCopy() => new(WoundAcceptedTurnData.CloneWound(Before)!,
        WoundAcceptedTurnData.CloneWound(After)!, OperationKey, EventRef,
        Applications.Select(WoundAcceptedTurnData.CloneApplicationResult).ToArray(),
        Terminations.Select(WoundAcceptedTurnData.CloneTerminationResult).ToArray(),
        Intents.Select(WoundAcceptedTurnData.CloneTransitionIntent).ToArray());
}

internal static partial class EffectAcceptedTurnPlanner
{
    private sealed class MortalTreatmentExecutionProof
    {
        private readonly object _continuation;
        private readonly string _preparedFingerprint;
        private readonly string _continuationFingerprint;
        private readonly MortalTreatmentExecutedBoundary[] _stages;
        private string? _payloadFingerprint;

        internal MortalTreatmentExecutionProof(WoundPreparedAcceptedTurnPlan prepared,
            IReadOnlyList<MortalTreatmentExecutedBoundary> stages)
        {
            if (!WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var treatment))
                throw new InvalidDataException("Terminal execution requires its actual private family.");
            _continuation = treatment.Continuation;
            _continuationFingerprint = treatment.ContinuationFingerprint;
            _preparedFingerprint = prepared.WoundPreparationFingerprint;
            _stages = stages.Select(row => row.DetachedCopy()).ToArray();
            Fingerprint = ComputeFingerprint();
        }
        internal string Fingerprint { get; }
        internal void Seal(EffectAcceptedTurnPlan plan)
        {
            if (_payloadFingerprint is not null || !ReferenceEquals(plan.MortalTreatmentExecutionAuthority, this))
                throw new InvalidOperationException("Treatment execution can be sealed only by its actual owner.");
            _payloadFingerprint = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        }
        internal bool Agrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnPlan plan) =>
            WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var treatment) &&
            ReferenceEquals(_continuation, treatment.Continuation) && _continuationFingerprint == treatment.ContinuationFingerprint &&
            ReferenceEquals(plan.MortalTreatmentExecutionAuthority, this) &&
            prepared.WoundPreparationFingerprint == _preparedFingerprint &&
            _stages.Length == 2 && Fingerprint == ComputeFingerprint() && _payloadFingerprint is not null &&
            _payloadFingerprint == WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        internal IReadOnlyList<MortalTreatmentExecutedBoundary> Read() => _stages.Select(row => row.DetachedCopy()).ToArray();
        private string ComputeFingerprint()
        {
            var fields = new List<string?> { "book_of_eternity.mortal_wound.treatment_execution", "1",
                _continuationFingerprint, _preparedFingerprint };
            foreach (var stage in _stages)
            {
                fields.Add(WoundMaterializationContract.SerializeCanonical(stage.Before));
                fields.Add(WoundMaterializationContract.SerializeCanonical(stage.After));
                fields.Add(stage.OperationKey); fields.Add(stage.EventRef);
                foreach (var row in stage.Applications)
                {
                    fields.Add(JsonSerializer.Serialize(row));
                    fields.Add(row.Materialization.ComponentCount.ToString(CultureInfo.InvariantCulture));
                    fields.Add(row.Materialization.MaterializationFingerprint);
                    fields.Add(JsonSerializer.Serialize(row.Materialization.SlotBindings));
                }
                fields.Add(JsonSerializer.Serialize(stage.Terminations));
                foreach (var intent in stage.Intents) fields.Add(JsonSerializer.Serialize(intent, intent.GetType()));
            }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
    }

    /// <summary>
    /// Reads detached facts only when the exact private treatment, prepared stage
    /// and complete effect payload still match their original execution seal.
    /// </summary>
    /// <param name="prepared">The same actual private standalone or joint preparation.</param>
    /// <param name="plan">The completed effect payload carrying the private execution proof.</param>
    /// <param name="stages">Receives copied snapshots and results only on exact complete agreement.</param>
    /// <returns>Whether both private family and immutable payload seals still agree.</returns>
    internal static bool TryReadMortalTreatmentExecution(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan, out IReadOnlyList<MortalTreatmentExecutedBoundary> stages)
    {
        stages = Array.Empty<MortalTreatmentExecutedBoundary>();
        if (plan.MortalTreatmentExecutionAuthority is not MortalTreatmentExecutionProof proof || !proof.Agrees(prepared, plan)) return false;
        stages = proof.Read(); return true;
    }

    /// <summary>
    /// Supplies immutable execution DATA to the complete effect payload fingerprint.
    /// </summary>
    /// <param name="plan">The existing effect plan, without acquiring or reactivating any owner.</param>
    /// <returns>The retained comparison fingerprint, or null for another effect family.</returns>
    internal static string? GetMortalTreatmentExecutionFingerprint(EffectAcceptedTurnPlan plan) =>
        (plan.MortalTreatmentExecutionAuthority as MortalTreatmentExecutionProof)?.Fingerprint;

    private static EffectAcceptedApplicationResult CaptureTreatmentApplication(WoundApplicationRequest request,
        ApplicationExecutionFacts actual) => new(request.Root.ApplicationRef, actual.Disposition, actual.EffectId,
        actual.TransitionId, actual.CreatedEventRef!, actual.CausalEventRef, actual.Source, actual.Target,
        actual.CarrierCoordinate, new WoundEffectMaterializationAgreement(request.Root.SlotBindings,
            request.Root.ExpectedComponentCount, request.Root.ExpectedMaterializationFingerprint));

    private static IReadOnlyList<EffectAcceptedTerminationResult> CaptureTreatmentTerminations(
        IReadOnlyList<WoundTerminalRequest> requests, EffectIdentityHistoryOwner identityRoot)
    {
        var parsed = ParseIdentity(identityRoot.ReadSnapshot());
        if (parsed.State is null || parsed.Issues.Count != 0) throw new InvalidDataException("Treatment prefix identity is invalid.");
        return requests.Select(request =>
        {
            var op = request.Operation;
            if (!parsed.State.TryGetEntry(op.EffectId, out var entry)) throw new InvalidDataException("Treatment prefix terminal identity disappeared.");
            var rows = entry.Transitions.Where(row => row.EventRef == op.OperationRef).ToArray();
            if (entry.State != "expired" || rows.Length != 1 || rows[0].Kind != "expire")
                throw new InvalidDataException("Treatment prefix terminal result differs from its actual operation.");
            return new EffectAcceptedTerminationResult(op.OperationRef, "expired", op.EffectId, rows[0].TransitionId,
                rows[0].EventRef, op.CausalEventRef, op.ExpectedSourceKey, op.ExpectedTargetKey, op.ExpectedCarrierCoordinate);
        }).ToArray();
    }

    private static MortalTreatmentExecutionProof? ExecuteMortalTreatmentTerminal(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        IReadOnlyList<EffectAcceptedApplicationResult> prefixApplications,
        IReadOnlyList<EffectAcceptedTerminationResult> prefixTerminations, List<ValidationIssue> issues,
        out EffectSourceAuthority finalSourceAuthority)
    {
        finalSourceAuthority = input.SourceAuthority;
        if (!WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var continuation) ||
            continuation.Outcome.ReadTerminalHealIntent() is not { } heal)
            throw new InvalidDataException("Terminal treatment lost its exact private continuation.");
        var prefixBatch = continuation.TreatmentBatch;
        var refs = (prefixBatch?.RootApplications ?? Array.Empty<WoundRootEffectApplication>())
            .Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
        var publication = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(continuation.Outcome,
            continuation.Resolution, prefixBatch, prefixApplications.Where(row => refs.Contains(row.ApplicationRef))
                .ToDictionary(row => row.ApplicationRef, StringComparer.Ordinal));
        if (!publication.IsValid || publication.After is null || publication.DeclaredOutcome is null)
        {
            issues.AddRange(publication.Issues);
            if (issues.Count == 0) AddWoundBatchIssue(issues, "treatment.terminal", "mortal_treatment_terminal_preparation_invalid",
                "one exact finalized active treatment image", "incomplete treatment publication");
            return null;
        }
        var coordinates = continuation.Request.Coordinates;
        var treat = WoundTransitionReducer.Reduce(new WoundTransitionRequest("treat", continuation.Outcome.TransitionId,
            coordinates.OperationKey, coordinates.EventRef, turn, continuation.Outcome.Before, publication.After,
            new WoundTreatmentEvidence(continuation.Resolution.ResolutionAuthorityFingerprint,
                WoundIdentityState.ComputeSemanticFingerprint(continuation.Outcome.Before), WoundIdentityState.ComputeSemanticFingerprint(publication.After),
                coordinates.RouteId, coordinates.AttemptId, publication.DeclaredOutcome)));
        issues.AddRange(treat.Issues);
        if (!treat.IsValid || treat.ProposedAfter is not { } beforeHeal ||
            treat.Intents.OfType<WoundFollowUpHealIntent>().Count() != 1 || beforeHeal.Severity.Rank != 1)
        {
            if (issues.Count == 0) AddWoundBatchIssue(issues, "treatment.terminal", "mortal_treatment_terminal_reduction_invalid",
                "one active severity-I primary transition with exactly one follow-up heal", "missing or changed treatment reduction");
            return null;
        }
        var graph = MortalWoundTreatmentWorkingGraphProjection.FromCanonical(beforeHeal) ??
            throw new InvalidDataException("Terminal treatment graph cannot be projected.");
        var facts = ExecuteWoundBoundaryEffects(input, beforeHeal, graph.Graph, graph.Scalars.SlotBudget,
            new(heal.HealChildCoordinates.OperationKey, 1, coordinates.EventRef, heal.HealChildCoordinates.OperationKey,
                heal.IntentFingerprint, "heal", "Mortal wound treatment healing", continuation.TreatmentBinding.AcceptedEvents,
                IsMortalRecovery: false, IsMortalTreatment: true), changedSeverity: false, terminalHeal: true,
            workspace, identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects, processedEvents,
            usedSources, usedTargets, rootBindings, issues);
        if (facts is null || issues.Count != 0) return null;
        var declarations = continuation.Legacies.Declarations;
        var healing = MortalWoundTreatmentOutcomePublicationPlanner.ReduceTerminalHeal(beforeHeal, heal, declarations, turn);
        issues.AddRange(healing.Issues);
        if (!healing.IsValid || healing.ProposedAfter is null) return null;
        // Retire only actual newly allocated roots of this healed treatment.
        // Joint acquisition aliases and any unexplained aliases remain subject
        // to the unchanged complete current-wound lineage guard.
        foreach (var root in prefixBatch?.RootApplications ?? Array.Empty<WoundRootEffectApplication>())
        {
            var applications = prefixApplications.Where(row => row.ApplicationRef == root.ApplicationRef).ToArray();
            var aliases = rootBindings.Where(row => row.ApplicationRef == root.ApplicationRef).ToArray();
            if (applications.Length != 1 || aliases.Length != 1 ||
                applications[0].SourceKey != root.ExpectedSourceKey ||
                applications[0].TargetKey != root.ExpectedTargetKey ||
                applications[0].CarrierCoordinate != root.ExpectedCarrierCoordinate ||
                aliases[0].EffectId != applications[0].EffectId ||
                facts.Terminations.Count(row => row.EffectId == applications[0].EffectId &&
                    row.Disposition == "expired" && row.SourceKey == applications[0].SourceKey &&
                    row.TargetKey == applications[0].TargetKey && row.CarrierCoordinate == applications[0].CarrierCoordinate) != 1)
            {
                AddWoundBatchIssue(issues, "treatment.terminal", "mortal_treatment_terminal_root_retirement_invalid",
                    "one exact allocated alias and actual terminal result per selected prefix root", root.ApplicationRef);
                return null;
            }
            rootBindings.Remove(aliases[0]);
        }
        finalSourceAuthority = input.SourceAuthority.WithoutHealedWoundSource(beforeHeal.Owner.Realm, beforeHeal.WoundId);
        issues.AddRange(finalSourceAuthority.Issues);
        if (issues.Count != 0) return null;
        return new(prepared, new[]
        {
            new MortalTreatmentExecutedBoundary(continuation.Outcome.Before, beforeHeal, coordinates.OperationKey, coordinates.EventRef,
                prefixApplications, prefixTerminations, treat.Intents),
            new MortalTreatmentExecutedBoundary(beforeHeal, healing.ProposedAfter, heal.HealChildCoordinates.OperationKey,
                heal.HealChildCoordinates.EventRef, facts.Applications, facts.Terminations, healing.Intents)
        });
    }
}
