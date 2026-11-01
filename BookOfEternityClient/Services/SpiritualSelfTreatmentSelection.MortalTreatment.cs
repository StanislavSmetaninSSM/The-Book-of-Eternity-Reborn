using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Closed physical selection DATA; later original and current-owner checks must
/// independently prove target, provider, mode and complete source requirements.
/// </summary>
internal sealed record MortalWoundGuidedTreatmentSelection(WoundOwnerCoordinate Target,
    WoundOwnerCoordinate Provider, string RouteId, string Mode, int? MilestoneOrdinal,
    string ExpectedRouteFingerprint, string ExpectedContextFingerprint);

internal sealed partial record SpiritualSelfTreatmentSelection
{
    /// <summary>
    /// Reads closed physical selection DATA without granting attempt or publication authority.
    /// </summary>
    internal MortalWoundGuidedTreatmentSelection? MortalTreatment { get; init; }

    private JsonObject MortalTreatmentJson()
    {
        if (MortalTreatment is not { } selected || MortalDiagnosis is not null || Diagnosis is not null ||
            Aid is not null || Combat is not null || Service is not null)
            throw new InvalidDataException("Physical treatment selection cannot mix execution purposes.");
        var root = new JsonObject
        {
            ["schemaVersion"] = 11, ["mode"] = "mortal_treat", ["woundId"] = WoundId,
            ["expectedWoundFingerprint"] = ExpectedWoundFingerprint,
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(selected.Target),
            ["provider"] = SpiritualHealingRequestProjection.OwnerJson(selected.Provider),
            ["routeId"] = selected.RouteId, ["treatmentMode"] = selected.Mode,
            ["milestoneOrdinal"] = selected.MilestoneOrdinal,
            ["expectedRouteFingerprint"] = selected.ExpectedRouteFingerprint,
            ["expectedContextFingerprint"] = selected.ExpectedContextFingerprint
        };
        _ = ParseMortalTreatment(this, root);
        return root;
    }

    private static SpiritualSelfTreatmentSelection ParseMortalTreatment(SpiritualSelfTreatmentSelection selection, JsonObject root)
    {
        var fields = new[] { "schemaVersion", "mode", "woundId", "expectedWoundFingerprint", "target", "provider",
            "routeId", "treatmentMode", "milestoneOrdinal", "expectedRouteFingerprint", "expectedContextFingerprint" };
        string Text(string key) => root[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        int? ordinal = root["milestoneOrdinal"] is JsonValue number && number.TryGetValue<int>(out var parsed) ? parsed : null;
        if (root.Count != fields.Length || fields.Any(field => !root.ContainsKey(field)) ||
            root["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 11 ||
            Text("mode") != "mortal_treat" || !ResourceMaterializationContract.IsExactIdentifier(Text("routeId")) ||
            Text("treatmentMode") is not ("procedure" or "course" or "guaranteed") ||
            (Text("treatmentMode") == "course" ? ordinal is null or < 1 : root["milestoneOrdinal"] is not null) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text("expectedRouteFingerprint")) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text("expectedContextFingerprint")))
            throw new InvalidDataException("Invalid closed physical treatment selection.");
        return selection with { MortalTreatment = new(ReadMortalDiagnosisOwner(root["target"]),
            ReadMortalDiagnosisOwner(root["provider"]), Text("routeId"), Text("treatmentMode"), ordinal,
            Text("expectedRouteFingerprint"), Text("expectedContextFingerprint")) };
    }
}
