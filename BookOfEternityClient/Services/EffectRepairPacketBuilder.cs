using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectRepairContext(
    string Actor,
    string Route,
    string RawCoordinate,
    JsonObject ExpectedSource,
    JsonObject ExpectedTarget,
    string ExpectedDefinitionKey,
    JsonObject ExpectedEventRef,
    string? ExpectedValueJson);

internal sealed record EffectRepairExactFieldCorrection(
    string Path,
    string ExpectedValueJson,
    string Code,
    string RepairHint);

internal sealed class EffectRepairPacket
{
    internal string Kind { get; init; } = "effect_materialization_repair";
    internal string Actor { get; init; } = "";
    internal string Route { get; init; } = "effectChanges";
    internal string RawCoordinate { get; init; } = "";
    internal JsonObject ExpectedSource { get; init; } = new();
    internal JsonObject ExpectedTarget { get; init; } = new();
    internal string ExpectedDefinitionKey { get; init; } = "";
    internal JsonObject ExpectedEventRef { get; init; } = new();
    internal IReadOnlyList<EffectRepairExactFieldCorrection> ExactFieldCorrections { get; init; } =
        Array.Empty<EffectRepairExactFieldCorrection>();
    internal IReadOnlyList<string> TargetFiles { get; init; } = Array.Empty<string>();
    internal bool FullTurnResubmissionRequired { get; init; } = true;
    internal IReadOnlyList<string> ResubmissionObligations { get; init; } = Array.Empty<string>();

    internal JsonObject ToJsonObject() => new()
    {
        ["kind"] = Kind,
        ["actor"] = Actor,
        ["route"] = Route,
        ["rawCoordinate"] = RawCoordinate,
        ["expectedSource"] = ExpectedSource.DeepClone(),
        ["expectedTarget"] = ExpectedTarget.DeepClone(),
        ["expectedDefinitionKey"] = ExpectedDefinitionKey,
        ["expectedEventRef"] = ExpectedEventRef.DeepClone(),
        ["exactFieldCorrections"] = new JsonArray(ExactFieldCorrections
            .Select(correction => (JsonNode)new JsonObject
            {
                ["path"] = correction.Path,
                ["expectedValue"] = JsonNode.Parse(correction.ExpectedValueJson),
                ["code"] = correction.Code,
                ["repairHint"] = correction.RepairHint
            })
            .ToArray()),
        ["targetFiles"] = new JsonArray(TargetFiles
            .Select(static path => (JsonNode)path)
            .ToArray()),
        ["fullTurnResubmissionRequired"] = FullTurnResubmissionRequired,
        ["resubmissionObligations"] = new JsonArray(ResubmissionObligations
            .Select(static obligation => (JsonNode)obligation)
            .ToArray())
    };
}

/// <summary>
/// Converts one exact, source-bounded GM semantic omission into a repair
/// packet. Every identity, selector, lifecycle, stack, receipt, history,
/// carrier, pending-state, resource, and wound boundary remains fail-closed.
/// </summary>
internal static class EffectRepairPacketBuilder
{
    private const string RepairableCode = "effect_source_parameter_required";

    internal static IReadOnlyList<EffectRepairPacket> Build(
        IEnumerable<ValidationIssue> issues,
        bool rollbackAvailable)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var effectIssues = issues.Where(IsEffectIssue).ToArray();
        if (!rollbackAvailable || effectIssues.Length != 1)
            return Array.Empty<EffectRepairPacket>();
        if (!TryBuildPacket(effectIssues[0], out var packet))
            return Array.Empty<EffectRepairPacket>();
        return new[] { packet };
    }

    internal static bool RequiresFailClosedRollback(
        IEnumerable<ValidationIssue> issues,
        bool rollbackAvailable)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var materialized = issues.ToArray();
        return materialized.Any(IsEffectIssue) &&
               Build(materialized, rollbackAvailable).Count == 0;
    }

    internal static bool IsProtectedClientOwnedTarget(string path) =>
        !string.Equals(
            Normalize(path),
            Normalize(EffectAcceptedTurnPlan.CommandPath),
            StringComparison.OrdinalIgnoreCase);

    private static bool TryBuildPacket(
        ValidationIssue issue,
        out EffectRepairPacket packet)
    {
        packet = null!;
        if (!string.Equals(issue.Code, RepairableCode, StringComparison.Ordinal) ||
            !string.Equals(issue.Actual, "missing", StringComparison.OrdinalIgnoreCase) ||
            issue.EffectRepairContext is not { } context ||
            !TryParseActor(context.Actor, out var operationCoordinate) ||
            !string.Equals(issue.Actor, context.Actor, StringComparison.Ordinal) ||
            !string.Equals(context.Route, "effectChanges", StringComparison.Ordinal) ||
            !string.Equals(issue.FilePath, context.RawCoordinate, StringComparison.Ordinal) ||
            !TryReadParameterCoordinate(
                context.RawCoordinate,
                operationCoordinate,
                out _) ||
            !TryValidateClosedSource(
                context.ExpectedSource,
                context.ExpectedDefinitionKey) ||
            !TryValidateClosedTarget(context.ExpectedTarget) ||
            !TryValidateClosedEvent(context.ExpectedEventRef) ||
            !TryCanonicalScalar(
                context.ExpectedValueJson,
                out var expectedValueJson) ||
            issue.RepairTargetFiles.Any(IsProtectedClientOwnedTarget))
        {
            return false;
        }

        var correction = new EffectRepairExactFieldCorrection(
            context.RawCoordinate,
            expectedValueJson,
            issue.Code!,
            Bound(issue.RepairHint ??
                  "Resubmit the complete accepted turn with this exact source-owned value."));
        packet = new EffectRepairPacket
        {
            Actor = context.Actor,
            Route = context.Route,
            RawCoordinate = context.RawCoordinate,
            ExpectedSource = context.ExpectedSource.DeepClone().AsObject(),
            ExpectedTarget = context.ExpectedTarget.DeepClone().AsObject(),
            ExpectedDefinitionKey = context.ExpectedDefinitionKey,
            ExpectedEventRef = context.ExpectedEventRef.DeepClone().AsObject(),
            ExactFieldCorrections = new[] { correction },
            TargetFiles = new[] { EffectAcceptedTurnPlan.CommandPath },
            FullTurnResubmissionRequired = true,
            ResubmissionObligations = new[]
            {
                operationCoordinate,
                "complete_rejected_turn_response",
                "canonical_semantic_change"
            }
        };
        return true;
    }

    private static bool IsEffectIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("effect_", StringComparison.Ordinal) == true ||
        string.Equals(
            issue.Section,
            "effect_materialization",
            StringComparison.OrdinalIgnoreCase);

    private static bool TryParseActor(string actor, out string operationCoordinate)
    {
        const string prefix = "effect-apply:";
        operationCoordinate = string.Empty;
        if (!actor.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var coordinate = actor[prefix.Length..];
        if (!TryParseEffectChangeCoordinate(coordinate, out _))
            return false;
        operationCoordinate = coordinate;
        return true;
    }

    private static bool TryReadParameterCoordinate(
        string rawCoordinate,
        string operationCoordinate,
        out string parameter)
    {
        parameter = string.Empty;
        var prefix = operationCoordinate + ".parameters.";
        if (!rawCoordinate.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        parameter = rawCoordinate[prefix.Length..];
        return IsExactIdentifier(parameter) &&
               parameter.IndexOfAny(new[] { '.', '[', ']' }) < 0;
    }

    private static bool TryParseEffectChangeCoordinate(
        string coordinate,
        out int index)
    {
        index = -1;
        const string prefix = "effectChanges[";
        if (!coordinate.StartsWith(prefix, StringComparison.Ordinal) ||
            !coordinate.EndsWith(']'))
        {
            return false;
        }
        var token = coordinate[prefix.Length..^1];
        return int.TryParse(token, out index) &&
               index >= 0 &&
               string.Equals(token, index.ToString(), StringComparison.Ordinal);
    }

    private static bool TryValidateClosedSource(
        JsonObject source,
        string expectedDefinitionKey)
    {
        if (!IsExactIdentifier(expectedDefinitionKey) ||
            source.Count != 3 ||
            !TryReadExact(source, "kind", out _) ||
            !TryReadExact(source, "definitionKey", out var definitionKey) ||
            !string.Equals(
                definitionKey,
                expectedDefinitionKey,
                StringComparison.Ordinal))
        {
            return false;
        }
        var hasId = TryReadExact(source, "sourceId", out _);
        var hasRef = TryReadExact(source, "sourceRef", out _);
        return hasId != hasRef &&
               source.All(pair => pair.Key is
                   "kind" or "sourceId" or "sourceRef" or "definitionKey");
    }

    private static bool TryValidateClosedTarget(JsonObject target)
    {
        if (target.Count != 2 || !TryReadExact(target, "kind", out _))
            return false;
        var hasId = TryReadExact(target, "targetId", out _);
        var hasRef = TryReadExact(target, "targetRef", out _);
        return hasId != hasRef &&
               target.All(pair => pair.Key is "kind" or "targetId" or "targetRef");
    }

    private static bool TryValidateClosedEvent(JsonObject eventRef) =>
        eventRef.Count == 2 &&
        TryReadExact(eventRef, "kind", out var kind) &&
        string.Equals(kind, "accepted_turn", StringComparison.Ordinal) &&
        TryReadExact(eventRef, "authorityId", out _);

    private static bool TryReadExact(
        JsonObject source,
        string property,
        out string value)
    {
        value = source[property] is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return IsExactIdentifier(value);
    }

    private static bool IsExactIdentifier(string? value) =>
        !string.IsNullOrEmpty(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool TryCanonicalScalar(
        string? valueJson,
        out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(valueJson))
            return false;
        try
        {
            var node = JsonNode.Parse(valueJson);
            if (node is not JsonValue)
                return false;
            canonical = node.ToJsonString();
            return canonical != "null";
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string Normalize(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');

    private static string Bound(string value) =>
        value.Length <= 512 ? value : value[..512];
}
