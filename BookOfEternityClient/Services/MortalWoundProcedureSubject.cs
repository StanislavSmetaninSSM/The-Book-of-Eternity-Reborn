using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

// One operation's typed method DATA. This projection does not issue ownership:
// every consumer must still prove the actual accepted state and coordinates.
internal sealed class MortalWoundProcedureSubject
{
    private MortalWoundProcedureSubject(string purpose, string memberId, string fingerprint,
        IReadOnlyList<JsonElement> requirements, ImmutableArray<MortalWoundTreatmentRequirement> typedRequirements, string sourcePath, MortalWoundProcedureResolution resolution,
        MortalWoundTreatmentResourcePolicy resourcePolicy, MortalWoundDiagnosisPathDefinition? diagnosis)
    {
        Purpose = purpose; MemberId = memberId; Fingerprint = fingerprint;
        Requirements = requirements.Select(value => value.Clone()).ToImmutableArray(); TypedRequirements = typedRequirements; SourcePath = sourcePath;
        Resolution = resolution; ResourcePolicy = resourcePolicy; Diagnosis = diagnosis;
    }

    internal string Purpose { get; }
    internal string MemberId { get; }
    internal string Fingerprint { get; }
    internal ImmutableArray<JsonElement> Requirements { get; }
    internal ImmutableArray<MortalWoundTreatmentRequirement> TypedRequirements { get; }
    internal string SourcePath { get; }
    internal MortalWoundProcedureResolution Resolution { get; }
    internal MortalWoundTreatmentResourcePolicy ResourcePolicy { get; }
    internal MortalWoundDiagnosisPathDefinition? Diagnosis { get; }

    internal static MortalWoundProcedureSubject FromAccepted(MortalWoundTreatmentAcceptedStateAuthority accepted,
        WoundMaterializationEnvelope before, string purpose, string memberId)
    {
        if (!accepted.HasCurrentAdmissionAuthority() || !accepted.MatchesCurrentWound(before))
            throw new InvalidDataException("Procedure subject requires its actual current accepted wound owner.");
        var definition = accepted.TreatmentDefinition;
        if (purpose == "diagnosis")
        {
            var method = definition.DiagnosisPaths.SingleOrDefault(value => value.DiagnosisPathId == memberId)
                ?? throw new InvalidDataException("The current diagnosis method is absent or ambiguous.");
            if (!IsKnown(before, method)) throw new InvalidDataException("The diagnosis method is not currently known and reachable.");
            var raw = before.Treatment.DiagnosisPaths.Single(value => value.DiagnosisPathId == memberId);
            return new("diagnosis", memberId, WoundDiagnosisEvidence.ComputePathFingerprint(before, memberId),
                raw.Requirements, method.Requirements, method.SourcePath, method.Check.Resolution, method.Check.ResourcePolicy, method);
        }
        if (purpose != "treatment") throw new InvalidDataException("Unknown procedure purpose.");
        var route = definition.Routes.SingleOrDefault(value => value.RouteId == memberId) as MortalWoundProcedureRouteDefinition
            ?? throw new InvalidDataException("The current procedure route is absent or has a different mode.");
        if (!definition.KnownRouteIds.Contains(memberId)) throw new InvalidDataException("The procedure route is not known.");
        var rawRoute = before.Treatment.Routes.Single(value => value.RouteId == memberId);
        return new("treatment", memberId, MortalWoundTreatmentRouteFingerprint.Compute(before, memberId),
            rawRoute.Requirements, route.Requirements, route.SourcePath, route.Resolution, route.ResourcePolicy, null);
    }

    internal static bool IsKnown(WoundMaterializationEnvelope wound, MortalWoundDiagnosisPathDefinition method)
    {
        if (wound.Owner.Realm != "mortal_world" || wound.Classification.Domain != "physical" || wound.Lifecycle != "active" ||
            wound.Display.Visibility is not ("public" or "known_to_player") || method.Visibility == "gm_only" ||
            method.Visibility == "hidden" && method.RequiresKnownFacts.Length == 0) return false;
        var facts = wound.Treatment.KnownRouteIds.Select(id => "route:" + id).Concat(wound.Complications
            .Where(value => value.Visibility is "public" or "known_to_player").Select(value => "complication:" + value.ComplicationId))
            .ToHashSet(StringComparer.Ordinal);
        return method.RequiresKnownFacts.All(fact => facts.Contains(fact.CanonicalValue));
    }
}
