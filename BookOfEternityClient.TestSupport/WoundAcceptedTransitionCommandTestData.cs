using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class WoundAcceptedTransitionCommandTestData
{
    internal static (WoundAcceptedTurnBinding Binding, WoundTransitionRequest Request) Create(
        string variant, string suffix = "one")
    {
        var diagnosis = variant is "diagnose" or "diagnose_failure";
        var hidden = variant == "author_alternative_treatment_hidden";
        if (!diagnosis && variant is not ("author_alternative_treatment" or
            "author_alternative_treatment_hidden"))
            throw new ArgumentOutOfRangeException(nameof(variant));
        var kind = diagnosis ? "diagnose" : "author_alternative_treatment";
        var beforeRoot = WoundContractTestData.CreateActiveWound();
        var pathId = "diagnosis_" + suffix;
        if (diagnosis)
            beforeRoot["treatment"]!["diagnosisPaths"] = new JsonArray(Path(pathId, "clean_and_suture"));
        var afterRoot = beforeRoot.DeepClone().AsObject();
        var routeId = "alternative_" + suffix;
        if (!diagnosis)
        {
            var route = beforeRoot["treatment"]!["routes"]![0]!.DeepClone().AsObject();
            route["routeId"] = routeId;
            route["displayName"] = "New setting-specific treatment " + suffix;
            route["visibility"] = hidden ? "hidden" : "known_to_player";
            afterRoot["treatment"]!["routes"]!.AsArray().Add(route);
            if (hidden)
                afterRoot["treatment"]!["diagnosisPaths"]!.AsArray().Add(Path(pathId, routeId));
            else
                afterRoot["treatment"]!["knownRouteIds"]!.AsArray().Add(routeId);
        }
        var transitionId = "transition_command_" + suffix;
        afterRoot["lastTransition"] = new JsonObject
        {
            ["transitionId"] = transitionId,
            ["ordinal"] = beforeRoot["lastTransition"]!["ordinal"]!.GetValue<int>() + 1,
            ["turn"] = 43,
            ["kind"] = kind
        };
        var before = Parse(beforeRoot);
        var after = Parse(afterRoot);
        const string eventRef = "turn_43:accepted_command";
        var events = new[] { new WoundAcceptedEventAuthority(
            eventRef, "treatment_check", "event_authority_command", Seal('a')) };
        var binding = new WoundAcceptedTurnBinding("session_command", "request_command",
            "snapshot_command", "mortal_world", 43, events,
            WoundAcceptedEventSetFingerprint.Compute(events));
        var request = diagnosis
            ? MortalWoundTreatmentPlanner.CreateDiagnosisTransition(transitionId,
                "command_" + suffix, "operation_" + suffix, "attempt_" + suffix,
                eventRef, 43, before, after, pathId,
                variant == "diagnose_failure" ? "failure" : "success", Seal('6'), Seal('7'))
            : MortalWoundTreatmentPlanner.CreateAlternativeTreatmentTransition(transitionId,
                "authoring_" + suffix, Seal('1'), "operation_" + suffix,
                eventRef, 43, before, after, routeId, hidden ? pathId : null, Seal('6'), Seal('7'));
        return (binding, request);
    }

    private static JsonObject Path(string id, string routeId) => new()
    {
        ["diagnosisPathId"] = id,
        ["displayName"] = "Examine the wound",
        ["visibility"] = "known_to_player",
        ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray("route:" + routeId),
        ["failurePolicy"] = "no_reveal"
    };

    private static WoundMaterializationEnvelope Parse(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(root.ToJsonString(), "command_test.wound");
        return parsed.IsValid && parsed.Wound is not null ? parsed.Wound :
            throw new InvalidOperationException(string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    }

    private static string Seal(char digit) => "sha256:" + new string(digit, 64);
}
