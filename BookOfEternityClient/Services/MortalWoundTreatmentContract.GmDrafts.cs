using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentContract
{
    internal static GmTreatmentRouteShapeParseResult ParseGmRouteDraftShape(
        JsonElement value,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        var route = ReadValidatedRouteShape(
            value,
            path,
            issues,
            RemovalSelectorDialect.GmComplicationRef,
            BuildGmRoute);
        return new(route is not null && issues.Count == 0, issues.ToImmutableArray(), route);
    }

    private static T? ReadValidatedRouteShape<T>(
        JsonElement value,
        string path,
        List<ValidationIssue> issues,
        RemovalSelectorDialect dialect,
        Func<WoundTreatmentRoute, T> build)
        where T : class
    {
        try
        {
            WoundMaterializationContract.FindDuplicateProperties(value, path, issues);
            if (issues.Count == 0)
            {
                var route = WoundMaterializationContract.ReadTreatmentRoute(value, path, issues);
                if (route is not null && issues.Count == 0)
                {
                    ValidateRoute(
                        route,
                        path,
                        new ValidationContext("mortal_world", null, dialect),
                        issues);
                    if (issues.Count == 0)
                        return build(route);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or
                                          InvalidOperationException or
                                          FormatException or
                                          OverflowException)
        {
            AddInvalid(
                issues,
                path,
                "one complete typed Mortal wound treatment route",
                exception.GetType().Name);
        }

        return null;
    }

    private static GmTreatmentRouteDraft BuildGmRoute(WoundTreatmentRoute route)
    {
        var requirements = BuildRequirements(route.Requirements);
        var policy = BuildResourcePolicy(route.ResourcePolicy);
        return route.Mode switch
        {
            "procedure" => new GmProcedureRouteDraft(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildProcedureResolution(route.Resolution),
                route.Outcomes.Select(value => new GmProcedureBandDraft(
                    Text(value, "bandId"),
                    NullableInt64(value.GetProperty("minimumMargin")),
                    NullableInt64(value.GetProperty("maximumMargin")),
                    Text(value, "category"),
                    BuildGmOperations(value.GetProperty("result")))).ToImmutableArray(),
                route.SourcePath),
            "course" => new GmCourseRouteDraft(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildCourseResolution(route.Resolution),
                route.Outcomes.Select(value => new GmCourseMilestoneDraft(
                    value.GetProperty("ordinal").GetInt32(),
                    value.GetProperty("afterMinutes").GetInt64(),
                    BuildRequirements(value.GetProperty("requirements").EnumerateArray().ToArray()),
                    Text(value, "category"),
                    Text(value, "completion"),
                    BuildGmOperations(value.GetProperty("result")))).ToImmutableArray(),
                BuildGmCategoryResult(route.Interruption!.Value),
                route.SourcePath),
            "guaranteed" => new GmGuaranteedRouteDraft(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildGuaranteedResolution(route.Resolution),
                BuildGmCategoryResult(route.Outcomes[0]),
                route.SourcePath),
            _ => throw new InvalidOperationException("Validated GM route has an unknown mode.")
        };
    }

    private static GmCategoryResultDraft BuildGmCategoryResult(JsonElement value) =>
        new(Text(value, "category"), BuildGmOperations(value.GetProperty("result")));

    private static ImmutableArray<GmTreatmentOperationDraft> BuildGmOperations(
        JsonElement values) => values.EnumerateArray().Select(BuildGmOperation).ToImmutableArray();

    private static GmTreatmentOperationDraft BuildGmOperation(JsonElement value)
    {
        if (Text(value, "kind") == "remove_complication")
            return new GmRemoveComplicationDraft(Text(value, "complicationRef"));

        return BuildOperation(value) switch
        {
            MortalWoundNoImprovementOperation => new GmNoImprovementDraft(),
            MortalWoundStabilizeOperation => new GmStabilizeDraft(),
            MortalWoundAddRecoveryOperation operation => new GmAddRecoveryDraft(operation.Points),
            MortalWoundReduceSeverityOperation operation => new GmReduceSeverityDraft(operation.Steps),
            MortalWoundAddComplicationOperation operation => new GmAddComplicationDraft(operation.ComplicationDraft),
            MortalWoundApplyDeteriorationOperation operation => new GmApplyDeteriorationDraft(operation.PolicyRef),
            MortalWoundHealOperation operation => new GmHealDraft(operation.Legacies),
            _ => throw new InvalidOperationException("Validated GM operation has an unknown non-removal kind.")
        };
    }
}
