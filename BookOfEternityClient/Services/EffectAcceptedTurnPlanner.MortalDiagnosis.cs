using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    private sealed class MortalDiagnosisExecutionProof
    {
        private readonly WoundAcceptedTurnPlanner.MortalDiagnosisContinuation _owner;
        private readonly string _preparation;
        private readonly WoundExecutedBoundary[] _stages;
        private readonly MortalWoundDiagnosisPersistedResult _result;
        private string? _payload;
        internal MortalDiagnosisExecutionProof(WoundAcceptedTurnPlanner.MortalDiagnosisContinuation owner,
            WoundPreparedAcceptedTurnPlan prepared, IReadOnlyList<WoundExecutedBoundary> stages)
        {
            _owner = owner; _preparation = prepared.WoundPreparationFingerprint;
            _stages = stages.Select(stage => stage.DetachedCopy()).ToArray();
            if (!_stages.Select(stage => stage.TransitionId).SequenceEqual(owner.TransitionIds, StringComparer.Ordinal))
                throw new InvalidDataException("Diagnosis execution omitted or changed its ordered allocated boundaries.");
            var grouped = _stages.Skip(1).ToLookup(stage => stage.Before.WoundId, StringComparer.Ordinal);
            var published = owner.Working.Members.ToDictionary(member => member.Original.WoundId, member =>
                (IReadOnlyList<MortalWoundRecoveryPublishedStage>)grouped[member.Original.WoundId].Select(stage =>
                {
                    var before = stage.Before; var after = stage.After;
                    return new MortalWoundRecoveryPublishedStage(stage.TransitionId, stage.Kind,
                        WoundIdentityState.ComputeSemanticFingerprint(before), WoundIdentityState.ComputeSemanticFingerprint(after),
                        stage.Kind == "heal", after.LastTransition.Turn, stage.EventRef, stage.OperationKey, before, after);
                }).ToArray(), StringComparer.Ordinal);
            _result = MortalWoundDiagnosisPersistedResult.CreateComparisonEvidence(owner.Owner, owner.Outcome, owner.Working, owner.Progression, published, owner);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.mortal_diagnosis.effect_execution", "1", owner.Fingerprint, _preparation, _result.ResultFingerprint,
                JsonSerializer.Serialize(_stages.Select(stage => new
                {
                    Before = WoundMaterializationContract.SerializeCanonical(stage.Before),
                    After = WoundMaterializationContract.SerializeCanonical(stage.After), stage.OperationKey,
                    stage.EventRef, stage.TickKey, stage.Applications, stage.Terminations
                }))
            });
        }
        internal string Fingerprint { get; }
        internal void Seal(EffectAcceptedTurnPlan plan)
        {
            if (_payload is not null || !ReferenceEquals(plan.MortalDiagnosisExecutionAuthority, this))
                throw new InvalidDataException("Only the actual diagnosis executor may seal its complete effect result once.");
            _payload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        }
        internal bool Agrees(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnPlan plan) =>
            ReferenceEquals(prepared.MortalDiagnosisAuthority, _owner) && _owner.PreparedAgrees(prepared) &&
            prepared.WoundPreparationFingerprint == _preparation && ReferenceEquals(plan.MortalDiagnosisExecutionAuthority, this) &&
            _payload is not null && _payload == WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan);
        internal IReadOnlyList<WoundExecutedBoundary> Read() => _stages.Select(stage => stage.DetachedCopy()).ToArray();
        internal MortalWoundDiagnosisPersistedResult Result => _result;
    }

    internal static string? GetMortalDiagnosisExecutionFingerprint(EffectAcceptedTurnPlan plan) =>
        (plan.MortalDiagnosisExecutionAuthority as MortalDiagnosisExecutionProof)?.Fingerprint;

    internal static bool TryReadMortalDiagnosisExecution(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan, out IReadOnlyList<WoundExecutedBoundary> stages)
    {
        stages = Array.Empty<WoundExecutedBoundary>();
        if (plan.MortalDiagnosisExecutionAuthority is not MortalDiagnosisExecutionProof proof || !proof.Agrees(prepared, plan)) return false;
        stages = proof.Read(); return stages.Count != 0;
    }

    internal static MortalWoundDiagnosisPersistedResult ReadMortalDiagnosisExecutedResult(WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnPlan plan) => plan.MortalDiagnosisExecutionAuthority is MortalDiagnosisExecutionProof proof && proof.Agrees(prepared, plan)
        ? proof.Result : throw new InvalidDataException("Diagnosis durable result requires its exact actual sealed execution.");

    private static EffectAcceptedTurnPlanningResult BuildMortalDiagnosis(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, EffectIdentityFactory identityFactory)
    {
        try
        {
            var owner = prepared.MortalDiagnosisAuthority;
            if (owner is null || !WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(owner, prepared, input))
                throw new InvalidDataException("Diagnosis effect handoff differs from its exact private working program.");
            owner.RequireCurrent();
            return BuildCore(input, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input), identityFactory,
                Array.Empty<WoundApplicationRequest>(), Array.Empty<WoundTerminalRequest>(), mortalDiagnosisPrepared: prepared);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException))
        { return Failed(new List<ValidationIssue> { MortalDiagnosisIssue(failure.Message) }); }
    }

    private static MortalDiagnosisExecutionProof? ExecuteMortalDiagnosis(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot,
        EffectIdentityFactory identityFactory, int turn, List<string> effectIds, List<string> transitionIds,
        List<JsonObject> activeEffects, HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        List<ValidationIssue> issues, out EffectSourceAuthority finalSources)
    {
        finalSources = input.SourceAuthority;
        var owner = prepared.MortalDiagnosisAuthority ?? throw new InvalidDataException("Missing actual diagnosis continuation.");
        owner.RequireCurrent();
        var outcome = owner.Outcome;
        var request = outcome.Request; var coordinates = request.Coordinates;
        var before = request.RouteSourceWound; var after = outcome.KnowledgeAfter;
        var transition = MortalWoundTreatmentPlanner.CreateDiagnosisTransition(after.LastTransition.TransitionId,
            "mortal_diagnosis:" + request.RequestFingerprint, coordinates.OperationKey, coordinates.AttemptId,
            coordinates.EventRef, coordinates.Turn, before, after, coordinates.MemberId,
            outcome.Resolution.ResultCategory == "success" ? "success" : "failure",
            request.RequirementAuthority.AuthorityFingerprint, outcome.Resolution.ResultFingerprint);
        var reduced = WoundTransitionReducer.Reduce(transition);
        issues.AddRange(reduced.Issues);
        if (!reduced.IsValid) return null;
        var stages = new List<WoundExecutedBoundary> { new(before, reduced.ProposedAfter!, coordinates.OperationKey,
            coordinates.EventRef, string.Empty, Array.Empty<EffectAcceptedApplicationResult>(), Array.Empty<EffectAcceptedTerminationResult>(), reduced.Intents) };
        var current = owner.Working.Members.ToDictionary(member => member.Original.WoundId, member => member.Working, StringComparer.Ordinal);
        foreach (var member in owner.ReadRecoveryProgram())
        {
            var authority = member.Authority;
            var view = member.View;
            if (view.AllocatedTransitionIds.Count == 0) continue;
            var boundaries = RecoveryBoundaries(view, issues);
            if (issues.Count != 0 || boundaries.Count != view.AllocatedTransitionIds.Count) return null;
            var finalRecovery = boundaries.Select((value, index) => (value, index)).Last(pair => !pair.value.Heal).index;
            var wound = current[view.Before.WoundId];
            for (var index = 0; index < boundaries.Count; index++)
            {
                var tick = GetMortalRecoveryStageTick(view.Resolution.TickKey, index, finalRecovery, boundaries[index].Heal);
                var completed = ExecuteRecoveryBoundary(input, view, authority, wound, boundaries[index], index, tick,
                    workspace, identityRoot, identityFactory, turn, effectIds, transitionIds, activeEffects,
                    processedEvents, usedSources, usedTargets, rootBindings, issues);
                if (completed is null || issues.Count != 0) return null;
                stages.Add(completed); wound = completed.After;
            }
            current[wound.WoundId] = wound;
        }
        var roots = owner.SourceRoots.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        foreach (var wound in current.Values)
        {
            if (roots.GetValueOrDefault(wound.Owner.CarrierPath) is not JsonObject carrier ||
                !WoundCarrierCollectionAuthority.TryResolve(carrier, wound.Owner, out var collection, out _))
                throw new InvalidDataException("Diagnosis final wound carrier is absent.");
            var match = collection.Select((node, index) => (node, index)).Single(pair => pair.node?["woundId"]?.GetValue<string>() == wound.WoundId);
            if (wound.Lifecycle == "active") collection[match.index] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound));
            else collection.RemoveAt(match.index);
        }
        var finalCatalog = WoundCarrierCatalog.Build(new(roots.GetValueOrDefault(WoundCarrierCatalog.PlayerPath) as JsonObject,
            roots.GetValueOrDefault(WoundCarrierCatalog.NpcPath) as JsonObject, roots.GetValueOrDefault(WoundCarrierCatalog.EnemiesPath) as JsonObject,
            roots.GetValueOrDefault(WoundCarrierCatalog.AlliesPath) as JsonObject, null));
        issues.AddRange(finalCatalog.Issues);
        finalSources = input.SourceAuthority.WithCompletedMortalDiagnosisWounds(owner, current.Values.ToArray(),
            EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(finalCatalog));
        issues.AddRange(finalSources.Issues);
        if (issues.Count != 0) return null;
        var changed = current.Values.Select(wound => new EffectIdentitySourceGroup(wound.Owner.Realm, "wound", wound.WoundId)).ToHashSet();
        usedSources.RemoveAll(source => changed.Contains(new(source.Key.Realm, source.Key.Kind, source.Key.SourceId)));
        foreach (var wound in current.Values.Where(wound => wound.Lifecycle == "active"))
        foreach (var definition in wound.Consequences.OwnedEffectSources.Definitions)
        {
            var source = finalSources.ResolveCanonicalBinding(new(wound.Owner.Realm, "wound", wound.WoundId,
                definition.GetProperty("definitionKey").GetString()!), WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind));
            if (!source.Success) throw new InvalidDataException("Diagnosis final canonical wound source did not resolve.");
            usedSources.Add(source.Source!);
        }
        rootBindings.Clear();
        return new(owner, prepared, stages);
    }

    private static ValidationIssue MortalDiagnosisIssue(string message) => new(AcceptedMechanicsPlan.WoundCommandPath,
        IssueSeverity.Error, message, code: "mortal_diagnosis_execution_invalid", section: "wound_materialization");
}
