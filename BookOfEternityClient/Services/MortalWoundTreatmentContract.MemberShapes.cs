using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentRouteShapeParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    MortalWoundTreatmentRouteDefinition? Route);

internal sealed record MortalWoundDiagnosisPathShapeParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    MortalWoundDiagnosisPathDefinition? DiagnosisPath);

internal static partial class MortalWoundTreatmentContract
{
    /// <summary>
    /// Parses a complete detached Mortal route, without granting real wound, owner,
    /// severity or policy authority. Full-context validation is still mandatory.
    /// </summary>
    internal static MortalWoundTreatmentRouteShapeParseResult ParseRouteShape(
        JsonElement value,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        var route = ReadValidatedRouteShape(
            value,
            path,
            issues,
            RemovalSelectorDialect.CanonicalId,
            BuildRoute);
        return new(route is not null && issues.Count == 0, issues.ToImmutableArray(), route);
    }

    /// <summary>
    /// Parses one complete diagnosis path. Exact fact grammar is local; membership
    /// and least-fixed-point discovery require the actual whole wound.
    /// </summary>
    internal static MortalWoundDiagnosisPathShapeParseResult ParseDiagnosisPathShape(
        JsonElement value,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        try
        {
            WoundMaterializationContract.FindDuplicateProperties(value, path, issues);
            if (issues.Count == 0)
            {
                var diagnosis = WoundMaterializationContract.ReadDiagnosisPath(value, path, issues);
                if (diagnosis is not null && issues.Count == 0)
                {
                    ValidateDiagnosisPath(diagnosis, path, routeIds: null, complicationIds: null, issues);
                    if (issues.Count == 0)
                        return new(true, ImmutableArray<ValidationIssue>.Empty, BuildDiagnosisPath(diagnosis));
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or
                                          InvalidOperationException or
                                          FormatException or
                                          OverflowException)
        {
            AddInvalid(issues, path, "one complete typed Mortal wound diagnosis path", exception.GetType().Name);
        }
        return new(false, issues.ToImmutableArray(), null);
    }
}
