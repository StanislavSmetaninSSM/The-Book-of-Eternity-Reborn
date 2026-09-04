using System.Globalization;
using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("realm")]
    [InlineData("negative_turn")]
    [InlineData("session_id")]
    [InlineData("session_generation")]
    [InlineData("request_id")]
    [InlineData("snapshot_token")]
    [InlineData("wound_id")]
    [InlineData("route_id")]
    [InlineData("event_ref")]
    [InlineData("event_kind")]
    [InlineData("event_authority_id")]
    [InlineData("provider_kind")]
    [InlineData("provider_id")]
    [InlineData("target_kind")]
    [InlineData("target_id")]
    [InlineData("location_id")]
    [InlineData("expected_before_fingerprint")]
    [InlineData("event_semantic_fingerprint")]
    [InlineData("context_fingerprint")]
    [InlineData("accepted_state_fingerprint")]
    public void DetachedSeal_CoordinatesRejectSelfConsistentInvalidIdentityTopology(
        string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_coordinate_" + axis,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var coordinates = MutateAndResealCoordinates(request.Coordinates, axis);
        var requestFingerprint = MortalWoundTreatmentAttemptRequest.ComputeFingerprint(
            request.Mode,
            coordinates,
            request.MilestoneOrdinal,
            request.RouteSourceWoundFingerprint,
            request.ModeAuthority,
            request.RequirementAuthority,
            request.ResourceAuthority);
        var restored = MortalWoundTreatmentAttemptRequest.RestoreDetached(
            request.Mode,
            coordinates,
            request.MilestoneOrdinal,
            request.RouteSourceWound,
            request.RouteSourceWoundFingerprint,
            request.ModeAuthority,
            request.RequirementAuthority,
            request.ResourceAuthority,
            requestFingerprint);

        Assert.NotNull(restored);
        Assert.Equal(
            "coordinates",
            MortalWoundTreatmentDetachedSealValidator.FindMismatch(restored!));
    }

    private static MortalWoundTreatmentAttemptCoordinates MutateAndResealCoordinates(
        MortalWoundTreatmentAttemptCoordinates source,
        string axis)
    {
        var values = typeof(MortalWoundTreatmentAttemptCoordinates)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(
                static property => property.Name,
                property => property.GetValue(source),
                StringComparer.OrdinalIgnoreCase);

        switch (axis)
        {
            case "realm":
                values["Realm"] = "afterlife";
                break;
            case "negative_turn":
                values["Turn"] = -1;
                break;
            case "provider_kind":
                values["ProviderKind"] = "guardian";
                break;
            case "target_kind":
                values["TargetKind"] = "guardian";
                break;
            case "expected_before_fingerprint":
                values["ExpectedBeforeFingerprint"] = "not_a_fingerprint";
                break;
            case "event_semantic_fingerprint":
                values["EventSemanticFingerprint"] = "not_a_fingerprint";
                break;
            case "context_fingerprint":
                values["ContextFingerprint"] = "not_a_fingerprint";
                break;
            case "accepted_state_fingerprint":
                values["AcceptedStateFingerprint"] = "not_a_fingerprint";
                break;
            default:
                values[CoordinatePropertyName(axis)] = " invalid identity ";
                break;
        }

        var semanticNames = new[]
        {
            "SchemaVersion", "SessionId", "SessionGeneration", "RequestId",
            "SnapshotToken", "OperationKey", "WoundId", "RouteId",
            "ExpectedBeforeFingerprint", "EventRef", "EventKind",
            "EventAuthorityId", "EventSemanticFingerprint", "Turn", "Realm",
            "ProviderKind", "ProviderId", "TargetKind", "TargetId", "LocationId",
            "ContextFingerprint", "AcceptedStateFingerprint"
        };
        var semanticFields = semanticNames.Select(name => values[name] switch
        {
            int number => number.ToString(CultureInfo.InvariantCulture),
            string text => text,
            null => null,
            var value => Convert.ToString(value, CultureInfo.InvariantCulture)
        }).ToArray();
        var attemptFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.attempt_identity",
                "1"
            }.Concat(semanticFields));
        values["AttemptId"] = "wound_treatment_attempt_" +
                              attemptFingerprint["sha256:".Length..];
        values["CoordinatesFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.attempt_coordinates",
                "1"
            }.Concat(semanticFields).Concat(new[] { (string?)values["AttemptId"] }));

        var constructor = Assert.Single(
            typeof(MortalWoundTreatmentAttemptCoordinates).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            constructor.Invoke(constructor.GetParameters()
                .Select(parameter => values[parameter.Name!])
                .ToArray()));
    }

    private static string CoordinatePropertyName(string axis) => axis switch
    {
        "session_id" => "SessionId",
        "session_generation" => "SessionGeneration",
        "request_id" => "RequestId",
        "snapshot_token" => "SnapshotToken",
        "wound_id" => "WoundId",
        "route_id" => "RouteId",
        "event_ref" => "EventRef",
        "event_kind" => "EventKind",
        "event_authority_id" => "EventAuthorityId",
        "provider_id" => "ProviderId",
        "target_id" => "TargetId",
        "location_id" => "LocationId",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
    };
}
