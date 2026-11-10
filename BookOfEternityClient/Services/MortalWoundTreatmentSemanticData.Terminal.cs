using System.Globalization;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentSemanticData
{
    private readonly object _terminalOutcomeIdentity = new();
    private readonly Func<OperationStructuralSnapshot?> _terminalOutcomeCapture;

    /// <summary>Recognizes only the audited guaranteed two-reduction/terminal-heal DATA shape.</summary>
    /// <param name="resolution">The exact resolution, including concrete operation and intent payloads.</param>
    /// <returns>Structural eligibility, not validation or an authority decision.</returns>
    internal static bool IsTerminalOutcomeShape(MortalWoundTreatmentResolution resolution) =>
        resolution.Mode == "guaranteed" && resolution.RequestAuthority.Mode == "guaranteed" &&
        resolution.RequestAuthority.ModeAuthority is MortalWoundTreatmentCapabilityProof &&
        resolution.ModeEvidence is MortalWoundGuaranteedModeEvidence &&
        resolution.CriticalReactionIntent is null &&
        resolution.DeclaredResult.Count == 3 && resolution.OutcomeIntents.Count == 3 &&
        resolution.DeclaredResult[0] is MortalWoundReduceSeverityOperation { Steps: 1 } &&
        resolution.DeclaredResult[1] is MortalWoundReduceSeverityOperation { Steps: 2 } &&
        resolution.DeclaredResult[2] is MortalWoundHealOperation &&
        resolution.OutcomeIntents[0] is MortalWoundReduceSeverityOutcomeIntent { Steps: 1 } &&
        resolution.OutcomeIntents[1] is MortalWoundReduceSeverityOutcomeIntent { Steps: 2 } &&
        resolution.OutcomeIntents[2] is MortalWoundHealOutcomeIntent;

    /// <summary>Limits terminal finalization reuse to detached empty-resource DATA, never held claims.</summary>
    /// <param name="resource">The actual outer or request resource companion.</param>
    /// <returns>Shape eligibility only, not validity or permission to publish or consume resources.</returns>
    internal static bool HasEmptyTerminalResources(MortalWoundTreatmentResourceReservationAuthority resource) =>
        resource.ReservationDisposition == "not_required" && resource.ReservationId is null &&
        resource.Claims.Count == 0 && resource.Policy.Mutations.IsEmpty;

    /// <summary>
    /// Captures terminal outcome DATA, also complete for the empty-resource finalization fingerprint.
    /// Stage replay retains its original stabilization-only policy. Retained aliases must match before any hit.
    /// </summary>
    /// <returns>Complete current DATA and companion identities, or null for the original fresh source.</returns>
    private OperationStructuralSnapshot? CaptureTerminalOutcomeCurrent()
    {
        if (FileSystemManager.HasCurrentOperationTimingObserver ||
            !_outcome.HasSameTerminalAgreementSources(_resolution)) return null;
        var request = _resolution.RequestAuthority;
        var wound = request.RouteSourceWound;
        var owned = wound.Consequences.OwnedEffectSources;
        var values = new List<string?>
        {
            "terminal-outcome-agreement-v1",
            JsonSerializer.Serialize(_resolution, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            WoundMaterializationContract.SerializeCanonical(wound),
            JsonSerializer.Serialize(owned, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            JsonSerializer.Serialize(owned.DefinitionFacts, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        };
        var identities = new List<object?>
        {
            _outcome, _resolution, request, request.Coordinates, request.ModeAuthority,
            request.RequirementAuthority, request.ResourceAuthority, _resolution.ModeEvidence,
            _resolution.RequirementAuthority, _resolution.ResourceAuthority
        };
        foreach (var path in wound.Treatment.DiagnosisPaths) values.Add(path.SourcePath);
        foreach (var route in wound.Treatment.Routes) values.Add(route.SourcePath);
        foreach (var claim in request.ResourceAuthority.Claims)
        {
            values.Add(claim.AvailableQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(claim.BindingFingerprint);
            identities.Add(claim);
        }
        // Public witness JSON does not expose this internal provenance vector.
        foreach (var scope in request.RequirementAuthority.Scopes)
            foreach (var binding in scope.Bindings)
            {
                values.Add(JsonSerializer.Serialize(binding.SuccessWitness.T060MechanicalFields));
                identities.Add(binding.SuccessWitness);
            }
        // The intent base class has no polymorphic serializer. Capture concrete payloads explicitly.
        for (var index = 0; index < 3; index++)
        {
            var intent = _resolution.OutcomeIntents[index];
            values.Add(intent switch
            {
                MortalWoundReduceSeverityOutcomeIntent reduce => JsonSerializer.Serialize(reduce,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                MortalWoundHealOutcomeIntent heal => JsonSerializer.Serialize(heal,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                _ => throw new InvalidOperationException("Terminal capture shape changed.")
            });
            identities.Add(_resolution.DeclaredResult[index]);
            identities.Add(intent);
        }
        // Legacy draft base JSON also omits mechanical definitions and application parameters.
        foreach (var legacy in ((MortalWoundHealOperation)_resolution.DeclaredResult[2]).Legacies)
        {
            switch (legacy)
            {
                case MortalWoundCosmeticLegacyDraft cosmetic:
                    values.Add(JsonSerializer.Serialize(cosmetic, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    break;
                case MortalWoundMechanicalEffectLegacyDraft mechanical:
                    values.Add(JsonSerializer.Serialize(mechanical, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    break;
                default: return null;
            }
            identities.Add(legacy);
        }
        return new(values, identities);
    }
}
