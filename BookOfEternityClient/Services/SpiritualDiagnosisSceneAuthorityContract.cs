using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>Afterlife dialect of the shared setting-scene grammar; never permission by itself.</summary>
internal static class SpiritualDiagnosisSceneAuthorityContract
{
    internal const string FacilityKind = "spiritual_diagnosis_facility";
    internal const string EnvironmentKind = "spiritual_diagnosis_environment";
    internal const string ConsentKind = "spiritual_diagnosis_consent";

    internal static bool IsRecognized(JsonElement row) =>
        MortalWoundTreatmentSceneAuthorityContract.IsSpiritualDiagnosisRecognized(row);

    internal static MortalWoundTreatmentSceneAuthorityResult Parse(JsonElement rows, string path)
    {
        var result = MortalWoundTreatmentSceneAuthorityContract.ParseSpiritualDiagnosis(rows, path);
        return result with
        {
            Issues = Array.AsReadOnly(result.Issues.Select(issue => new ValidationIssue(issue.FilePath,
                issue.Severity, issue.Message.Replace("Mortal wound treatment", "Spiritual diagnosis", StringComparison.Ordinal),
                code: issue.Code?.Replace("mortal_wound_treatment_scene", "spiritual_diagnosis_scene", StringComparison.Ordinal),
                section: "afterlife_spiritual_diagnosis", expected: issue.Expected, actual: issue.Actual)).ToArray())
        };
    }
}
