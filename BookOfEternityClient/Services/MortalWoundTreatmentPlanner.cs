using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentAttemptCoordinatesResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentAttemptCoordinates? Coordinates);

internal sealed class MortalWoundTreatmentAttemptCoordinates
{
    private const string AttemptIdentityDomain =
        "book_of_eternity.mortal_wound_treatment.attempt_identity";
    private const string CoordinatesDomain =
        "book_of_eternity.mortal_wound_treatment.attempt_coordinates";

    private MortalWoundTreatmentAttemptCoordinates(
        int schemaVersion,
        string sessionId,
        string sessionGeneration,
        string requestId,
        string snapshotToken,
        string operationKey,
        string attemptId,
        string woundId,
        string routeId,
        string expectedBeforeFingerprint,
        string eventRef,
        string eventKind,
        string eventAuthorityId,
        string eventSemanticFingerprint,
        int turn,
        string realm,
        string providerKind,
        string providerId,
        string targetKind,
        string targetId,
        string locationId,
        string contextFingerprint,
        string acceptedStateFingerprint,
        string coordinatesFingerprint)
    {
        SchemaVersion = schemaVersion;
        SessionId = sessionId;
        SessionGeneration = sessionGeneration;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        OperationKey = operationKey;
        AttemptId = attemptId;
        WoundId = woundId;
        RouteId = routeId;
        ExpectedBeforeFingerprint = expectedBeforeFingerprint;
        EventRef = eventRef;
        EventKind = eventKind;
        EventAuthorityId = eventAuthorityId;
        EventSemanticFingerprint = eventSemanticFingerprint;
        Turn = turn;
        Realm = realm;
        ProviderKind = providerKind;
        ProviderId = providerId;
        TargetKind = targetKind;
        TargetId = targetId;
        LocationId = locationId;
        ContextFingerprint = contextFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
    }

    public int SchemaVersion { get; }
    public string SessionId { get; }
    public string SessionGeneration { get; }
    public string RequestId { get; }
    public string SnapshotToken { get; }
    public string OperationKey { get; }
    public string AttemptId { get; }
    public string WoundId { get; }
    public string RouteId { get; }
    public string ExpectedBeforeFingerprint { get; }
    public string EventRef { get; }
    public string EventKind { get; }
    public string EventAuthorityId { get; }
    public string EventSemanticFingerprint { get; }
    public int Turn { get; }
    public string Realm { get; }
    public string ProviderKind { get; }
    public string ProviderId { get; }
    public string TargetKind { get; }
    public string TargetId { get; }
    public string LocationId { get; }
    public string ContextFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string CoordinatesFingerprint { get; }

    internal static MortalWoundTreatmentAttemptCoordinatesResult CreateFromAcceptedState(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef)
    {
        var issues = new List<ValidationIssue>();
        if (acceptedState is null || !acceptedState.HasCurrentAdmissionAuthority())
        {
            Add(
                issues,
                "treatmentAttempt.acceptedState",
                "mortal_wound_treatment_coordinates_accepted_state_invalid",
                "one current registry-owned accepted-state authority",
                acceptedState is null ? "missing" : "stale or detached");
            return Failure(issues);
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(operationKey))
        {
            Add(
                issues,
                "treatmentAttempt.operationKey",
                "mortal_wound_treatment_coordinates_operation_key_invalid",
                "one exact new operation key",
                operationKey ?? "missing");
        }
        else if (HasOperationConflict(acceptedState.History, operationKey!))
        {
            Add(
                issues,
                "treatmentAttempt.operationKey",
                "mortal_wound_treatment_coordinates_operation_key_conflict",
                "an operation key absent from complete accepted history",
                operationKey!);
        }

        if (!acceptedState.MatchesCurrentWound(before) ||
            before is not { SchemaVersion: 1 } ||
            !string.Equals(
                before.Classification.Domain,
                "physical",
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "treatmentAttempt.before",
                "mortal_wound_treatment_coordinates_before_mismatch",
                "the exact selected current version-1 physical wound",
                before?.WoundId ?? "missing");
        }

        var context = acceptedState.RequirementContext;
        if (before is not null &&
            (!string.Equals(before.Owner.Realm, "mortal_world", StringComparison.Ordinal) ||
             !string.Equals(before.Owner.Realm, context.Realm, StringComparison.Ordinal) ||
             !string.Equals(before.Owner.OwnerKind, context.TargetKind, StringComparison.Ordinal) ||
             !string.Equals(before.Owner.OwnerId, context.TargetId, StringComparison.Ordinal)))
        {
            Add(
                issues,
                "treatmentAttempt.target",
                "mortal_wound_treatment_coordinates_target_mismatch",
                "the exact Mortal wound owner selected by treatment context",
                $"{before.Owner.Realm}/{before.Owner.OwnerKind}/{before.Owner.OwnerId}");
        }

        var routeMatches = ResourceMaterializationContract.IsExactIdentifier(routeId)
            ? acceptedState.TreatmentDefinition.Routes
                .Where(route => string.Equals(
                    route.RouteId,
                    routeId,
                    StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<MortalWoundTreatmentRouteDefinition>();
        if (routeMatches.Length != 1 ||
            !acceptedState.TreatmentDefinition.KnownRouteIds.Contains(
                routeId ?? string.Empty,
                StringComparer.Ordinal))
        {
            Add(
                issues,
                "treatmentAttempt.routeId",
                "mortal_wound_treatment_coordinates_route_invalid",
                "one exact known route from the selected wound",
                routeId ?? "missing");
        }

        var binding = acceptedState.Binding;
        if (!string.Equals(
                binding.AcceptedEventsFingerprint,
                WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "treatmentAttempt.eventRef",
                "mortal_wound_treatment_coordinates_event_set_invalid",
                "the complete recomputable accepted event set",
                binding.AcceptedEventsFingerprint);
        }
        var eventMatches = ResourceMaterializationContract.IsExactIdentifier(eventRef)
            ? binding.AcceptedEvents.Where(acceptedEvent => string.Equals(
                acceptedEvent.EventRef,
                eventRef,
                StringComparison.Ordinal)).ToArray()
            : Array.Empty<WoundAcceptedEventAuthority>();
        if (eventMatches.Length != 1)
        {
            Add(
                issues,
                "treatmentAttempt.eventRef",
                "mortal_wound_treatment_coordinates_event_invalid",
                "one exact event from the accepted binding",
                eventRef ?? "missing");
        }

        if (issues.Count != 0 || before is null || eventMatches.Length != 1)
            return Failure(issues);

        var acceptedEvent = eventMatches[0];
        var semanticFields = SemanticFields(
            schemaVersion: 1,
            binding.SessionId,
            acceptedState.SessionGeneration,
            binding.RequestId,
            binding.SnapshotToken,
            operationKey!,
            before.WoundId,
            routeId!,
            acceptedState.WoundFingerprint,
            acceptedEvent.EventRef,
            acceptedEvent.Kind,
            acceptedEvent.AuthorityId,
            acceptedEvent.SemanticFingerprint,
            binding.Turn,
            binding.Realm,
            context.ProviderKind,
            context.ProviderId,
            context.TargetKind,
            context.TargetId,
            context.CurrentLocationId,
            acceptedState.ContextFingerprint,
            acceptedState.AcceptedStateFingerprint);
        var attemptId = "wound_treatment_attempt_" +
            Compute(AttemptIdentityDomain, semanticFields)["sha256:".Length..];
        if (HasAttemptConflict(acceptedState.History, attemptId))
        {
            Add(
                issues,
                "treatmentAttempt.attemptId",
                "mortal_wound_treatment_coordinates_attempt_id_conflict",
                "a derived attempt ID absent from complete accepted history",
                attemptId);
            return Failure(issues);
        }
        var coordinatesFingerprint = Compute(
            CoordinatesDomain,
            semanticFields.Concat(new[] { attemptId }));
        var coordinates = new MortalWoundTreatmentAttemptCoordinates(
            1,
            binding.SessionId,
            acceptedState.SessionGeneration,
            binding.RequestId,
            binding.SnapshotToken,
            operationKey!,
            attemptId,
            before.WoundId,
            routeId!,
            acceptedState.WoundFingerprint,
            acceptedEvent.EventRef,
            acceptedEvent.Kind,
            acceptedEvent.AuthorityId,
            acceptedEvent.SemanticFingerprint,
            binding.Turn,
            binding.Realm,
            context.ProviderKind,
            context.ProviderId,
            context.TargetKind,
            context.TargetId,
            context.CurrentLocationId,
            acceptedState.ContextFingerprint,
            acceptedState.AcceptedStateFingerprint,
            coordinatesFingerprint);
        return new MortalWoundTreatmentAttemptCoordinatesResult(
            true,
            Array.Empty<ValidationIssue>(),
            coordinates);
    }

    internal bool MatchesAcceptedState(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
    {
        if (acceptedState is null || !acceptedState.HasCurrentAdmissionAuthority())
            return false;
        return AgreesWithAcceptedStateSemantics(acceptedState);
    }

    internal bool AgreesWithAcceptedStateSemantics(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        var binding = acceptedState.Binding;
        var context = acceptedState.RequirementContext;
        var currentWound = acceptedState.CurrentWound;
        var eventMatches = binding.AcceptedEvents.Where(acceptedEvent => string.Equals(
            acceptedEvent.EventRef,
            EventRef,
            StringComparison.Ordinal)).ToArray();
        if (eventMatches.Length != 1)
            return false;
        var acceptedEvent = eventMatches[0];
        var routeMatches = acceptedState.TreatmentDefinition.Routes.Count(route =>
            string.Equals(route.RouteId, RouteId, StringComparison.Ordinal));
        if (routeMatches != 1 ||
            !acceptedState.TreatmentDefinition.KnownRouteIds.Contains(
                RouteId,
                StringComparer.Ordinal) ||
            HasOperationConflict(acceptedState.History, OperationKey) ||
            !string.Equals(
                binding.AcceptedEventsFingerprint,
                WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents),
                StringComparison.Ordinal))
        {
            return false;
        }

        var semanticFields = SemanticFields(
            SchemaVersion,
            SessionId,
            SessionGeneration,
            RequestId,
            SnapshotToken,
            OperationKey,
            WoundId,
            RouteId,
            ExpectedBeforeFingerprint,
            EventRef,
            EventKind,
            EventAuthorityId,
            EventSemanticFingerprint,
            Turn,
            Realm,
            ProviderKind,
            ProviderId,
            TargetKind,
            TargetId,
            LocationId,
            ContextFingerprint,
            AcceptedStateFingerprint);
        var expectedAttemptId = "wound_treatment_attempt_" +
            Compute(AttemptIdentityDomain, semanticFields)["sha256:".Length..];
        var expectedCoordinatesFingerprint = Compute(
            CoordinatesDomain,
            semanticFields.Concat(new[] { expectedAttemptId }));
        return SchemaVersion == 1 &&
               ResourceMaterializationContract.IsExactIdentifier(OperationKey) &&
               string.Equals(SessionId, binding.SessionId, StringComparison.Ordinal) &&
               string.Equals(SessionGeneration, acceptedState.SessionGeneration, StringComparison.Ordinal) &&
               string.Equals(RequestId, binding.RequestId, StringComparison.Ordinal) &&
               string.Equals(SnapshotToken, binding.SnapshotToken, StringComparison.Ordinal) &&
               currentWound.SchemaVersion == 1 &&
               string.Equals(currentWound.Classification.Domain, "physical", StringComparison.Ordinal) &&
               string.Equals(WoundId, currentWound.WoundId, StringComparison.Ordinal) &&
               string.Equals(ExpectedBeforeFingerprint, acceptedState.WoundFingerprint, StringComparison.Ordinal) &&
               string.Equals(EventKind, acceptedEvent.Kind, StringComparison.Ordinal) &&
               string.Equals(EventAuthorityId, acceptedEvent.AuthorityId, StringComparison.Ordinal) &&
               string.Equals(EventSemanticFingerprint, acceptedEvent.SemanticFingerprint, StringComparison.Ordinal) &&
               Turn == binding.Turn &&
               string.Equals(Realm, "mortal_world", StringComparison.Ordinal) &&
               string.Equals(Realm, binding.Realm, StringComparison.Ordinal) &&
               string.Equals(currentWound.Owner.Realm, Realm, StringComparison.Ordinal) &&
               string.Equals(ProviderKind, context.ProviderKind, StringComparison.Ordinal) &&
               string.Equals(ProviderId, context.ProviderId, StringComparison.Ordinal) &&
               string.Equals(TargetKind, context.TargetKind, StringComparison.Ordinal) &&
               string.Equals(TargetId, context.TargetId, StringComparison.Ordinal) &&
               string.Equals(currentWound.Owner.OwnerKind, TargetKind, StringComparison.Ordinal) &&
               string.Equals(currentWound.Owner.OwnerId, TargetId, StringComparison.Ordinal) &&
               string.Equals(LocationId, context.CurrentLocationId, StringComparison.Ordinal) &&
               string.Equals(ContextFingerprint, acceptedState.ContextFingerprint, StringComparison.Ordinal) &&
               string.Equals(AcceptedStateFingerprint, acceptedState.AcceptedStateFingerprint, StringComparison.Ordinal) &&
               string.Equals(AttemptId, expectedAttemptId, StringComparison.Ordinal) &&
               !HasAttemptConflict(acceptedState.History, AttemptId) &&
               string.Equals(CoordinatesFingerprint, expectedCoordinatesFingerprint, StringComparison.Ordinal);
    }

    private static string?[] SemanticFields(
        int schemaVersion,
        string sessionId,
        string sessionGeneration,
        string requestId,
        string snapshotToken,
        string operationKey,
        string woundId,
        string routeId,
        string expectedBeforeFingerprint,
        string eventRef,
        string eventKind,
        string eventAuthorityId,
        string eventSemanticFingerprint,
        int turn,
        string realm,
        string providerKind,
        string providerId,
        string targetKind,
        string targetId,
        string locationId,
        string contextFingerprint,
        string acceptedStateFingerprint) =>
        new string?[]
        {
            schemaVersion.ToString(CultureInfo.InvariantCulture),
            sessionId,
            sessionGeneration,
            requestId,
            snapshotToken,
            operationKey,
            woundId,
            routeId,
            expectedBeforeFingerprint,
            eventRef,
            eventKind,
            eventAuthorityId,
            eventSemanticFingerprint,
            turn.ToString(CultureInfo.InvariantCulture),
            realm,
            providerKind,
            providerId,
            targetKind,
            targetId,
            locationId,
            contextFingerprint,
            acceptedStateFingerprint
        };

    private static bool HasOperationConflict(
        WoundHistoryState history,
        string operationKey)
    {
        var confusable = ExactIdentifierConfusableKey.Build(operationKey);
        return history.Transitions.Any(transition => string.Equals(
            ExactIdentifierConfusableKey.Build(transition.OperationKey),
            confusable,
            StringComparison.Ordinal));
    }

    private static bool HasAttemptConflict(
        WoundHistoryState history,
        string attemptId)
    {
        var confusable = ExactIdentifierConfusableKey.Build(attemptId);
        return history.Transitions.Any(transition =>
            transition.AttemptId is { } existing && string.Equals(
                ExactIdentifierConfusableKey.Build(existing),
                confusable,
                StringComparison.Ordinal));
    }

    private static string Compute(string domain, IEnumerable<string?> fields) =>
        WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[] { domain, "1" }.Concat(fields));

    private static MortalWoundTreatmentAttemptCoordinatesResult Failure(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()),
        null);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The Mortal wound-treatment attempt coordinates cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate the attempt from the current accepted-state authority and exact wound, route, event, and operation coordinates."));
}

internal static partial class MortalWoundTreatmentPlanner
{
    internal static MortalWoundTreatmentAttemptCoordinatesResult CreateAttemptCoordinates(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        WoundMaterializationEnvelope? before,
        string? operationKey,
        string? routeId,
        string? eventRef) =>
        MortalWoundTreatmentAttemptCoordinates.CreateFromAcceptedState(
            acceptedState,
            before,
            operationKey,
            routeId,
            eventRef);
}
