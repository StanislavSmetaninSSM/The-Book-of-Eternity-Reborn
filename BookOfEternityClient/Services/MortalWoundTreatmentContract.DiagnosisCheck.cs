using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDiagnosisBand(string BandId, long? MinimumMargin, long? MaximumMargin, string Category);

internal sealed record MortalWoundDiagnosisCheck(MortalWoundProcedureResolution Resolution,
    ImmutableArray<MortalWoundDiagnosisBand> Bands, int DurationMinutes, MortalWoundTreatmentResourcePolicy ResourcePolicy);

internal static partial class MortalWoundTreatmentContract
{
    private static readonly IReadOnlySet<string> MortalDiagnosisCheckFields = Set(
        "schemaVersion", "kind", "resolution", "outcomes", "durationMinutes", "resourcePolicy");
    private static readonly IReadOnlySet<string> MortalDiagnosisBandFields = Set(
        "bandId", "minimumMargin", "maximumMargin", "category");

    private static bool ValidateMortalDiagnosisCheck(JsonElement check,
        IReadOnlyList<JsonElement> requirements, string path, List<ValidationIssue> issues)
    {
        var before = issues.Count;
        if (!ValidateObject(check, path, MortalDiagnosisCheckFields, issues)) return false;
        ValidateInt32(check, "schemaVersion", path, 1, 1, issues);
        ValidateExactString(check, "kind", path, "mortal_procedure_diagnosis", issues);
        ValidateInt32(check, "durationMinutes", path, 0, int.MaxValue, issues);
        if (check.TryGetProperty("resolution", out var resolution))
            ValidateProcedureResolutionPayload(resolution, requirements, path + ".resolution", issues);
        else AddMissing(issues, path + ".resolution");
        if (check.TryGetProperty("resourcePolicy", out var policy))
        {
            ValidateResourcePolicyPayload(policy, requirements, "procedure", path + ".resourcePolicy", issues);
            if (policy.ValueKind == JsonValueKind.Object)
                ValidateClosedStringArray(policy, "consumeOn", path + ".resourcePolicy", Set("success", "failed_attempt"), null, issues);
        }
        else AddMissing(issues, path + ".resourcePolicy");
        var rows = new List<ProcedureBand>();
        if (TryGetArray(check, "outcomes", out var bands))
        {
            if (bands.GetArrayLength() is < 2 or > MaxProcedureBands)
                AddInvalid(issues, path + ".outcomes", "2-16 complete ordered binary procedure bands", bands.GetArrayLength().ToString());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var band in bands.EnumerateArray().Take(MaxProcedureBands))
            {
                var bandPath = path + ".outcomes[" + index++ + "]";
                if (!ValidateObject(band, bandPath, MortalDiagnosisBandFields, issues)) continue;
                var id = ValidateIdentifier(band, "bandId", bandPath, issues);
                if (id.Length > 0 && !ids.Add(ConfusableKey(id)))
                    AddInvalid(issues, bandPath + ".bandId", "exact/confusable unique band ID", id);
                var minimum = ReadNullableInt64(band, "minimumMargin", bandPath, issues);
                var maximum = ReadNullableInt64(band, "maximumMargin", bandPath, issues);
                var category = ValidateClosedString(band, "category", bandPath, Set("success", "failed_attempt"), issues);
                rows.Add(new(bandPath, minimum, maximum, category));
            }
            ValidateProcedureBandCoverage(rows, issues);
        }
        else AddMissing(issues, path + ".outcomes");
        return before == issues.Count;
    }

    private static MortalWoundDiagnosisCheck BuildMortalDiagnosisCheck(JsonElement check) => new(
        BuildProcedureResolution(check.GetProperty("resolution")),
        check.GetProperty("outcomes").EnumerateArray().Select(band => new MortalWoundDiagnosisBand(
            Text(band, "bandId"), NullableInt64(band.GetProperty("minimumMargin")), NullableInt64(band.GetProperty("maximumMargin")), Text(band, "category"))).ToImmutableArray(),
        check.GetProperty("durationMinutes").GetInt32(), BuildResourcePolicy(check.GetProperty("resourcePolicy")));

    private static void WriteMortalDiagnosisCheck(Utf8JsonWriter writer, MortalWoundDiagnosisCheck check)
    {
        writer.WritePropertyName("check"); writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1); writer.WriteString("kind", "mortal_procedure_diagnosis");
        WriteProcedureResolutionPayload(writer, check.Resolution);
        writer.WritePropertyName("outcomes"); writer.WriteStartArray();
        foreach (var band in check.Bands)
        {
            writer.WriteStartObject(); writer.WriteString("bandId", band.BandId);
            WriteNullableInt64(writer, "minimumMargin", band.MinimumMargin);
            WriteNullableInt64(writer, "maximumMargin", band.MaximumMargin);
            writer.WriteString("category", band.Category); writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WriteNumber("durationMinutes", check.DurationMinutes);
        WriteResourcePolicy(writer, check.ResourcePolicy); writer.WriteEndObject();
    }
}
