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

    internal static IReadOnlyList<ValidationIssue> ValidateContainers(JsonElement root, string path, bool shining)
    {
        var issues = new List<ValidationIssue>();
        if (root.ValueKind != JsonValueKind.Object) return issues;
        foreach (var collectionName in shining ? new[] { "halls" } : new[] { "guardians", "UpdateGuardians" })
        {
            if (!root.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array) continue;
            var index = 0;
            foreach (var row in collection.EnumerateArray())
            {
                var container = row; var context = path + "." + collectionName + "[" + index++ + "]";
                if (row.ValueKind != JsonValueKind.Object) continue;
                if (!shining)
                {
                    if (!row.TryGetProperty("abode", out container) || container.ValueKind != JsonValueKind.Object) continue;
                    context += ".abode";
                }
                var keys = container.EnumerateObject().Where(property =>
                    ExactIdentifierConfusableKey.Build(property.Name) == ExactIdentifierConfusableKey.Build("customStates")).ToArray();
                if (keys.Length == 0) continue;
                if (keys.Length != 1 || keys[0].Name != "customStates")
                    issues.Add(new(context + ".customStates", IssueSeverity.Error, "Spiritual diagnosis scene container key is ambiguous.",
                        code: "spiritual_diagnosis_scene_state_invalid", section: "afterlife_spiritual_diagnosis"));
                else issues.AddRange(Parse(keys[0].Value, context + ".customStates").Issues);
            }
        }
        return issues.AsReadOnly();
    }

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
