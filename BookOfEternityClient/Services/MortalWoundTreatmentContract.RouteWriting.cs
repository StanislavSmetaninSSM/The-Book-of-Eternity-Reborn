using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentContract
{
    private static void WriteRouteHeader(
        Utf8JsonWriter writer,
        string id,
        string name,
        string visibility,
        string mode,
        ImmutableArray<MortalWoundTreatmentRequirement> requirements,
        MortalWoundTreatmentResourcePolicy policy)
    {
        writer.WriteStartObject();
        writer.WriteString("routeId", id);
        writer.WriteString("displayName", name);
        writer.WriteString("visibility", visibility);
        writer.WriteString("mode", mode);
        WriteRequirements(writer, requirements);
        WriteResourcePolicy(writer, policy);
    }

    private static void WriteProcedureResolutionPayload(
        Utf8JsonWriter writer,
        MortalWoundProcedureResolution value)
    {
        writer.WritePropertyName("resolution");
        writer.WriteStartObject();
        writer.WriteString("formulaKey", value.FormulaKey);
        writer.WriteNumber("difficulty", value.Difficulty);
        writer.WriteString("rollSource", value.RollSource);
        writer.WriteString("criticalPolicy", value.CriticalPolicy);
        writer.WritePropertyName("modifierSource");
        writer.WriteStartObject();
        writer.WriteString("kind", value.ModifierSource.Kind);
        if (value.ModifierSource is MortalWoundResolvedSkillTierModifierSource skill)
            writer.WriteNumber("requirementIndex", skill.RequirementIndex);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteProcedureBandPayload(
        Utf8JsonWriter writer,
        string id,
        long? minimum,
        long? maximum,
        string category,
        Action<Utf8JsonWriter> writeResult)
    {
        writer.WriteStartObject();
        writer.WriteString("bandId", id);
        WriteNullableInt64(writer, "minimumMargin", minimum);
        WriteNullableInt64(writer, "maximumMargin", maximum);
        writer.WriteString("category", category);
        writeResult(writer);
        writer.WriteEndObject();
    }

    private static void WriteCourseResolutionPayload(
        Utf8JsonWriter writer,
        MortalWoundCourseResolution value)
    {
        writer.WritePropertyName("resolution");
        writer.WriteStartObject();
        writer.WriteString("clockKind", value.ClockKind);
        writer.WriteNumber("maximumGapMinutes", value.MaximumGapMinutes);
        writer.WriteEndObject();
    }

    private static void WriteCourseMilestonePayload(
        Utf8JsonWriter writer,
        int ordinal,
        long afterMinutes,
        ImmutableArray<MortalWoundTreatmentRequirement> requirements,
        string category,
        string completion,
        Action<Utf8JsonWriter> writeResult)
    {
        writer.WriteStartObject();
        writer.WriteNumber("ordinal", ordinal);
        writer.WriteNumber("afterMinutes", afterMinutes);
        WriteRequirements(writer, requirements);
        writer.WriteString("category", category);
        writer.WriteString("completion", completion);
        writeResult(writer);
        writer.WriteEndObject();
    }

    private static void WriteGuaranteedResolutionPayload(
        Utf8JsonWriter writer,
        MortalWoundGuaranteedResolution value)
    {
        writer.WritePropertyName("resolution");
        writer.WriteStartObject();
        writer.WriteString("capabilityRef", value.CapabilityRef);
        writer.WriteString("actorRole", value.ActorRole);
        writer.WriteEndObject();
    }

    private static void WriteCategoryResultPayload(
        Utf8JsonWriter writer,
        string category,
        Action<Utf8JsonWriter> writeResult)
    {
        writer.WriteStartObject();
        writer.WriteString("category", category);
        writeResult(writer);
        writer.WriteEndObject();
    }

    private static void WriteResultArray<T>(
        Utf8JsonWriter writer,
        ImmutableArray<T> operations,
        Action<Utf8JsonWriter, T> writeOperation)
    {
        writer.WritePropertyName("result");
        writer.WriteStartArray();
        foreach (var operation in operations)
            writeOperation(writer, operation);
        writer.WriteEndArray();
    }
}
