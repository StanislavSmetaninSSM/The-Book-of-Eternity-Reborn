using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentMemberFingerprint
{
    internal static string ComputeRoute(JsonNode? route) => Compute(
        "book_of_eternity.mortal_wound.alternative_treatment_route", route);

    internal static string ComputeDiagnosisPath(JsonNode? path) => Compute(
        "book_of_eternity.mortal_wound.diagnosis_path", path);

    private static string Compute(string domain, JsonNode? member) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            domain, "1", member is null ? null : WoundAcceptedTurnFingerprintWriter.CanonicalJson(member)
        });
}
