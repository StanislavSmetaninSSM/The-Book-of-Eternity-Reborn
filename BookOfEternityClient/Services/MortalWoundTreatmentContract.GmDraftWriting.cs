using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentContract
{
    internal static void WriteGmRouteDraft(Utf8JsonWriter writer, GmTreatmentRouteDraft route)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(route);
        using var stream = new MemoryStream();
        using (var buffer = new Utf8JsonWriter(stream))
            WriteGmRouteUnchecked(buffer, route);
        using var document = JsonDocument.Parse(stream.ToArray());
        var parsed = ParseGmRouteDraftShape(document.RootElement, "gm_route");
        if (!parsed.IsValid)
        {
            throw new InvalidOperationException(
                "Cannot write an invalid GM route: " +
                string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
        }
        document.RootElement.WriteTo(writer);
    }

    private static void WriteGmRouteUnchecked(Utf8JsonWriter writer, GmTreatmentRouteDraft route)
    {
        WriteRouteHeader(
            writer,
            route.RouteId,
            route.DisplayName,
            route.Visibility,
            route.Mode,
            route.Requirements,
            route.ResourcePolicy);
        switch (route)
        {
            case GmProcedureRouteDraft value:
                WriteProcedureResolutionPayload(writer, value.Resolution);
                writer.WritePropertyName("outcomes");
                writer.WriteStartArray();
                foreach (var band in value.Bands)
                {
                    WriteProcedureBandPayload(
                        writer,
                        band.BandId,
                        band.MinimumMargin,
                        band.MaximumMargin,
                        band.Category,
                        output => WriteResultArray(
                            output,
                            band.DeclaredResult,
                            WriteGmOperation));
                }
                writer.WriteEndArray();
                writer.WriteNull("interruption");
                break;
            case GmCourseRouteDraft value:
                WriteCourseResolutionPayload(writer, value.Resolution);
                writer.WritePropertyName("outcomes");
                writer.WriteStartArray();
                foreach (var milestone in value.Milestones)
                {
                    WriteCourseMilestonePayload(
                        writer,
                        milestone.Ordinal,
                        milestone.AfterMinutes,
                        milestone.Requirements,
                        milestone.Category,
                        milestone.Completion,
                        output => WriteResultArray(
                            output,
                            milestone.DeclaredResult,
                            WriteGmOperation));
                }
                writer.WriteEndArray();
                writer.WritePropertyName("interruption");
                WriteCategoryResultPayload(
                    writer,
                    value.Interruption.Category,
                    output => WriteResultArray(
                        output,
                        value.Interruption.DeclaredResult,
                        WriteGmOperation));
                break;
            case GmGuaranteedRouteDraft value:
                WriteGuaranteedResolutionPayload(writer, value.Resolution);
                writer.WritePropertyName("outcomes");
                writer.WriteStartArray();
                WriteCategoryResultPayload(
                    writer,
                    value.Outcome.Category,
                    output => WriteResultArray(
                        output,
                        value.Outcome.DeclaredResult,
                        WriteGmOperation));
                writer.WriteEndArray();
                writer.WriteNull("interruption");
                break;
            default:
                throw new InvalidOperationException("Unknown GM treatment route draft.");
        }
        writer.WriteEndObject();
    }

    private static void WriteGmOperation(
        Utf8JsonWriter writer,
        GmTreatmentOperationDraft operation)
    {
        if (operation is GmRemoveComplicationDraft remove)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "remove_complication");
            writer.WriteString("complicationRef", remove.ComplicationRef);
            writer.WriteEndObject();
            return;
        }

        MortalWoundTreatmentOperation payload = operation switch
        {
            GmNoImprovementDraft => new MortalWoundNoImprovementOperation(),
            GmStabilizeDraft => new MortalWoundStabilizeOperation(),
            GmAddRecoveryDraft value => new MortalWoundAddRecoveryOperation(value.Points),
            GmReduceSeverityDraft value => new MortalWoundReduceSeverityOperation(value.Steps),
            GmAddComplicationDraft value => new MortalWoundAddComplicationOperation(value.ComplicationDraft),
            GmApplyDeteriorationDraft value => new MortalWoundApplyDeteriorationOperation(value.PolicyRef),
            GmHealDraft value => new MortalWoundHealOperation(value.Legacies),
            _ => throw new InvalidOperationException("Unknown GM non-removal operation draft.")
        };
        WriteCanonicalOperation(writer, payload);
    }
}
