namespace BookOfEternityClient.Services;

// A retained procedure outcome and knowledge projection, not an accepted-state
// authority or publication permit. Only Resolve consumes the actual live owner;
// historical reconstruction below validates DATA without reviving that owner.
internal sealed class MortalWoundDiagnosisOutcome
{
    private readonly WoundMaterializationEnvelope _knowledge;
    private readonly MortalWoundTreatmentAcceptedStateAuthority _owner;
    private MortalWoundDiagnosisOutcome(MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundTreatmentResolution resolution, WoundMaterializationEnvelope knowledge, long resultingMinute)
    { _owner = owner; Resolution = resolution; _knowledge = knowledge; ResultingMinute = resultingMinute; OriginalMinute = owner.CurrentGameMinute; }

    internal MortalWoundTreatmentResolution Resolution { get; }
    internal MortalWoundTreatmentAttemptRequest Request => Resolution.RequestAuthority;
    internal WoundMaterializationEnvelope KnowledgeAfter => WoundAcceptedTurnData.CloneWound(_knowledge)!;
    internal long OriginalMinute { get; }
    internal long ResultingMinute { get; }

    internal static MortalWoundDiagnosisOutcome Resolve(MortalWoundTreatmentAcceptedStateAuthority owner,
        WoundHistoryParseResult history, MortalWoundTreatmentAttemptRequest request)
    {
        if (request.Mode != "diagnosis" || request.Coordinates.Purpose != "diagnosis" ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            !owner.HasCurrentAdmissionAuthority() || !request.Coordinates.MatchesAcceptedState(owner) ||
            !owner.MatchesCompleteHistory(history) || !owner.MatchesCurrentWound(request.RouteSourceWound) ||
            request.ModeAuthority is not MortalWoundProcedureCheckAuthority check)
            throw new InvalidDataException("Diagnosis resolution requires its exact current accepted source, History and retained request.");
        var requirements = MortalWoundTreatmentRequirementAuthorityBundle.CreateForDiagnosis(owner, request.Coordinates, owner.CurrentWound);
        if (!requirements.IsValid || requirements.Authority is null ||
            requirements.Authority.AuthorityFingerprint != request.RequirementAuthority.AuthorityFingerprint ||
            !check.MatchesFreshDiagnosis(owner, request.Coordinates, owner.CurrentWound, requirements.Authority))
            throw new InvalidDataException("Diagnosis requirements or retained die/Fate agreement changed before resolution.");
        var subject = MortalWoundProcedureSubject.FromAccepted(owner, owner.CurrentWound, "diagnosis", request.Coordinates.MemberId);
        var method = subject.Diagnosis!;
        var total = checked((long)check.NaturalRoll + check.Modifier);
        var margin = checked(total - check.EffectiveDifficulty);
        var reaction = EffectAcceptedEventReportCatalog.ResolvePreparedMortalWoundCriticalReaction(request, owner);
        if (!reaction.IsValid) throw new InvalidDataException("Diagnosis critical reaction differs from its actual retained check.");
        var index = MortalWoundTreatmentPlanner.SelectDiagnosisBand(method.Check, check.NaturalRoll, margin, reaction.Intent is not null);
        if (index < 0) throw new InvalidDataException("No declared diagnostic band covers the retained procedure result.");
        var band = method.Check.Bands[index];
        var original = check.NaturalRoll switch { 20 => "critical_success", 1 => "critical_failure", _ => "ordinary" };
        var resolved = check.NaturalRoll == 1 && reaction.Intent is not null ? "failure" : original;
        var evidence = MortalWoundProcedureModeEvidence.CreateForDiagnosis(check, method.Check, total, margin, original, resolved, index, reaction.Intent);
        var resolution = MortalWoundTreatmentResolution.Create("diagnosis", request.Coordinates, "AcceptedTerminal",
            band.Category, index, false, Array.Empty<MortalWoundTreatmentOperation>(), Array.Empty<MortalWoundTreatmentOutcomeIntent>(),
            reaction.Intent, subject.ResourcePolicy.ConsumeOn.Contains(band.Category, StringComparer.Ordinal) ? band.Category : "none",
            null, null, null, request, evidence, subject.Fingerprint, "None");
        var knowledge = ReconstructKnowledge(resolution);
        return new(owner, resolution, knowledge, checked(owner.CurrentGameMinute + method.Check.DurationMinutes));
    }

    internal void RequireCurrent(MortalWoundTreatmentAcceptedStateAuthority owner, WoundHistoryParseResult history)
    {
        if (!ReferenceEquals(owner, _owner) || !owner.HasCurrentAdmissionAuthority() ||
            !Request.Coordinates.MatchesAcceptedState(owner) || !owner.MatchesCompleteHistory(history) ||
            !owner.MatchesCurrentWound(Request.RouteSourceWound) ||
            Request.ModeAuthority is not MortalWoundProcedureCheckAuthority check ||
            !check.MatchesFreshDiagnosis(owner, Request.Coordinates, owner.CurrentWound, Request.RequirementAuthority))
            throw new InvalidDataException("Retained diagnosis outcome has no current original owner or exact claim agreement.");
    }

    internal static MortalWoundProcedureModeEvidence RecomputeEvidence(MortalWoundTreatmentResolution resolution)
    {
        var request = resolution.RequestAuthority;
        if (resolution.Mode != "diagnosis" || request.Mode != "diagnosis" ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            resolution.DeclaredResult.Count != 0 || resolution.OutcomeIntents.Count != 0 || resolution.RouteCompletion != "None" ||
            resolution.Interruption || resolution.AttemptDisposition != "AcceptedTerminal" ||
            request.ModeAuthority is not MortalWoundProcedureCheckAuthority check)
            throw new InvalidDataException("A diagnosis result cannot contain treatment operations, completion or foreign check evidence.");
        var subject = MortalWoundProcedureSubject.FromDetached(request.RouteSourceWound, "diagnosis", request.Coordinates.MemberId);
        var method = subject.Diagnosis!;
        var total = checked((long)check.NaturalRoll + check.Modifier);
        var margin = checked(total - check.EffectiveDifficulty);
        var reaction = check.PreparedCriticalReaction is null ? null : MortalWoundCriticalReactionIntent.Create(request, check.PreparedCriticalReaction);
        var index = MortalWoundTreatmentPlanner.SelectDiagnosisBand(method.Check, check.NaturalRoll, margin, reaction is not null);
        if (index < 0 || resolution.SelectedOutcomeIndex != index || resolution.ResultCategory != method.Check.Bands[index].Category ||
            resolution.CriticalReactionIntent?.IntentFingerprint != reaction?.IntentFingerprint)
            throw new InvalidDataException("The diagnostic band/result differs from its retained natural roll, margin or Fate decision.");
        var original = check.NaturalRoll switch { 20 => "critical_success", 1 => "critical_failure", _ => "ordinary" };
        return MortalWoundProcedureModeEvidence.CreateForDiagnosis(check, method.Check, total, margin, original,
            check.NaturalRoll == 1 && reaction is not null ? "failure" : original, index, reaction);
    }

    internal static WoundMaterializationEnvelope ReconstructKnowledge(MortalWoundTreatmentResolution resolution)
    {
        _ = RecomputeEvidence(resolution);
        var request = resolution.RequestAuthority; var coordinates = request.Coordinates;
        var before = request.RouteSourceWound;
        var path = before.Treatment.DiagnosisPaths.Single(value => value.DiagnosisPathId == coordinates.MemberId);
        var success = resolution.ResultCategory == "success";
        var routes = success ? path.Reveals.Where(value => value.StartsWith("route:", StringComparison.Ordinal)).Select(value => value[6..]) : Enumerable.Empty<string>();
        var complications = (success ? path.Reveals : Array.Empty<string>()).Where(value => value.StartsWith("complication:", StringComparison.Ordinal))
            .Select(value => value[13..]).ToHashSet(StringComparer.Ordinal);
        var transitionId = TransitionId(request);
        var proposed = before with
        {
            Treatment = before.Treatment with { KnownRouteIds = before.Treatment.KnownRouteIds.Concat(routes.Where(value => !before.Treatment.KnownRouteIds.Contains(value, StringComparer.Ordinal))).ToArray() },
            Complications = before.Complications.Select(value => complications.Contains(value.ComplicationId) && value.Visibility is not ("public" or "known_to_player")
                ? value with { Visibility = "known_to_player" } : value).ToArray(),
            LastTransition = new(transitionId, checked(before.LastTransition.Ordinal + 1), coordinates.Turn, "diagnose")
        };
        var transition = MortalWoundTreatmentPlanner.CreateDiagnosisTransition(transitionId, "mortal_diagnosis:" + request.RequestFingerprint,
            coordinates.OperationKey, coordinates.AttemptId, coordinates.EventRef, coordinates.Turn, before, proposed,
            coordinates.MemberId, success ? "success" : "failure", request.RequirementAuthority.AuthorityFingerprint, resolution.ResultFingerprint);
        var reduced = WoundTransitionReducer.Reduce(transition);
        if (!reduced.IsValid || reduced.ProposedAfter is null)
            throw new InvalidDataException("Knowledge-only diagnosis reducer refused the retained complete declared facts: " + string.Join(";", reduced.Issues.Select(value => value.Code)));
        return reduced.ProposedAfter;
    }

    internal static string TransitionId(MortalWoundTreatmentAttemptRequest request) => "wound_diagnosis_transition_" +
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { "mortal_diagnosis.transition", "1", request.RequestFingerprint })["sha256:".Length..];
}
