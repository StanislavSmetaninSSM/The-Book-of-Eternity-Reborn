using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundCriticalReactionPublicationResult
{
    private readonly ReadOnlyCollection<JsonObject> _lifecycleEvents;
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal MortalWoundCriticalReactionPublicationResult(
        IEnumerable<JsonObject> lifecycleEvents,
        string? fingerprint,
        IEnumerable<ValidationIssue> issues)
    {
        _lifecycleEvents = Array.AsReadOnly(lifecycleEvents
            .Select(static value => value.DeepClone().AsObject())
            .ToArray());
        Fingerprint = fingerprint;
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    internal IReadOnlyList<JsonObject> LifecycleEvents => Array.AsReadOnly(
        _lifecycleEvents
            .Select(static value => value.DeepClone().AsObject())
            .ToArray());

    internal string? Fingerprint { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal bool IsValid => Fingerprint is not null && _issues.Count == 0;
}

/// <summary>
/// Projects the exact typed procedure critical-reaction intent into the accepted
/// effect lifecycle event consumed by the ordinary effect planner. It never reads
/// or accepts the legacy GM effect-event report surface.
/// </summary>
internal static class MortalWoundCriticalReactionPublicationPlanner
{
    private const string IssuePath = "treatmentPublication.criticalReaction";

    internal static MortalWoundCriticalReactionPublicationResult Compose(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);

        if (request.Mode is "guaranteed")
        {
            return resolution.CriticalReactionIntent is null
                ? Valid(request, resolution, intent: null, lifecycleEvent: null)
                : Invalid(
                    "mortal_wound_treatment_critical_reaction_unexpected",
                    "no critical reaction for guaranteed treatment",
                    resolution.CriticalReactionIntent.IntentFingerprint);
        }

        if (request.Mode is not "procedure" ||
            resolution.Mode is not "procedure" ||
            request.ModeAuthority is not MortalWoundProcedureCheckAuthority procedure ||
            !acceptedState.HasCurrentAdmissionAuthority() ||
            !request.HasMatchingFingerprint() ||
            !request.Coordinates.MatchesAcceptedState(acceptedState) ||
            !acceptedState.HasLiveProcedureReservationAgreement(procedure) ||
            !string.Equals(
                resolution.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal) ||
            !MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(
                resolution,
                out _))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_publication_authority_invalid",
                "one current sealed procedure request, result, and live reservation agreement",
                "missing, stale, foreign, or changed authority");
        }

        var recomposed = EffectAcceptedEventReportCatalog
            .ResolvePreparedMortalWoundCriticalReaction(request, acceptedState);
        if (!recomposed.IsValid)
        {
            return new MortalWoundCriticalReactionPublicationResult(
                Array.Empty<JsonObject>(),
                null,
                recomposed.Issues);
        }

        var expected = recomposed.Intent;
        var actual = resolution.CriticalReactionIntent;
        if (!IntentsAgree(actual, expected))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_publication_mismatch",
                "the exact independently recomposed typed critical-reaction intent",
                actual?.IntentFingerprint ?? "missing");
        }
        if (actual is null)
            return Valid(request, resolution, intent: null, lifecycleEvent: null);

        var lifecycleEvent = CreateLifecycleEvent(actual);
        return Valid(request, resolution, actual, lifecycleEvent);
    }

    internal static bool HasMatchingFingerprint(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        IReadOnlyList<JsonObject> lifecycleEvents,
        string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(lifecycleEvents);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        var intent = resolution.CriticalReactionIntent;
        JsonObject? expectedEvent = null;
        if (intent is null)
        {
            if (lifecycleEvents.Count != 0)
                return false;
        }
        else
        {
            if (lifecycleEvents.Count != 1)
                return false;
            expectedEvent = CreateLifecycleEvent(intent);
            if (!JsonNode.DeepEquals(expectedEvent, lifecycleEvents[0]))
                return false;
        }
        return string.Equals(
            fingerprint,
            ComputeFingerprint(request, resolution, intent, expectedEvent),
            StringComparison.Ordinal);
    }

    internal static string ComputeFingerprint(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        MortalWoundCriticalReactionIntent? intent,
        JsonObject? lifecycleEvent) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.critical_reaction_publication",
            "1",
            request.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            intent?.IntentFingerprint,
            intent?.AcceptedEffectFingerprint,
            intent?.PreparedReactionFingerprint,
            lifecycleEvent is null
                ? null
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(lifecycleEvent)
        });

    private static MortalWoundCriticalReactionPublicationResult Valid(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        MortalWoundCriticalReactionIntent? intent,
        JsonObject? lifecycleEvent)
    {
        var events = lifecycleEvent is null
            ? Array.Empty<JsonObject>()
            : new[] { lifecycleEvent };
        return new MortalWoundCriticalReactionPublicationResult(
            events,
            ComputeFingerprint(request, resolution, intent, lifecycleEvent),
            Array.Empty<ValidationIssue>());
    }

    private static bool IntentsAgree(
        MortalWoundCriticalReactionIntent? actual,
        MortalWoundCriticalReactionIntent? expected)
    {
        if (actual is null || expected is null)
            return actual is null && expected is null;
        return string.Equals(actual.EventType, expected.EventType, StringComparison.Ordinal) &&
               string.Equals(actual.EventRef, expected.EventRef, StringComparison.Ordinal) &&
               string.Equals(
                   actual.CausalEventRef,
                   expected.CausalEventRef,
                   StringComparison.Ordinal) &&
               actual.Turn == expected.Turn &&
               string.Equals(actual.Realm, expected.Realm, StringComparison.Ordinal) &&
               string.Equals(actual.TargetKind, expected.TargetKind, StringComparison.Ordinal) &&
               string.Equals(actual.TargetId, expected.TargetId, StringComparison.Ordinal) &&
               string.Equals(actual.EffectId, expected.EffectId, StringComparison.Ordinal) &&
               string.Equals(actual.TriggerId, expected.TriggerId, StringComparison.Ordinal) &&
               string.Equals(
                   actual.AcceptedEffectFingerprint,
                   expected.AcceptedEffectFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   actual.PreparedReactionFingerprint,
                   expected.PreparedReactionFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   actual.RequestFingerprint,
                   expected.RequestFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   actual.IntentFingerprint,
                   expected.IntentFingerprint,
                   StringComparison.Ordinal);
    }

    private static JsonObject CreateLifecycleEvent(
        MortalWoundCriticalReactionIntent intent) => new()
    {
        ["eventRef"] = intent.EventRef,
        ["causalEventRef"] = intent.CausalEventRef,
        ["turn"] = intent.Turn,
        ["phase"] = intent.EventType,
        ["realm"] = intent.Realm,
        ["target"] = new JsonObject
        {
            ["kind"] = intent.TargetKind,
            ["targetId"] = intent.TargetId
        },
        ["effectId"] = intent.EffectId,
        ["triggerId"] = intent.TriggerId,
        ["currentTime"] = null,
        ["currentSceneId"] = null,
        ["sceneClosed"] = false,
        ["sourceSatisfied"] = null,
        ["conditionSatisfied"] = null,
        ["targetSatisfied"] = true,
        ["currentRealm"] = intent.Realm
    };

    private static MortalWoundCriticalReactionPublicationResult Invalid(
        string code,
        string expected,
        string actual) => new(
        Array.Empty<JsonObject>(),
        null,
        new[]
        {
            new ValidationIssue(
                IssuePath,
                IssueSeverity.Error,
                "The accepted Mortal wound critical reaction could not be published.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                    "Reuse the exact typed reaction sealed by the accepted procedure request and result.")
        });
}
