using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateAfterlifeStoryOutlineStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                AfterlifeStoryOutlineState.StatePath,
                IssueSeverity.Error,
                "afterlife_story_outline.json должен быть JSON object.",
                code: "afterlife_story_outline_invalid_root",
                section: "AfterlifeStoryOutline",
                expected: "object with afterlifeStoryOutline or canonical Writer's Room fields",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "AfterlifeStoryOutline");

        var outline = root.TryGetProperty(AfterlifeStoryOutlineState.ResponseField, out var responseOutline)
            ? responseOutline
            : root;
        var outlineContext = root.TryGetProperty(AfterlifeStoryOutlineState.ResponseField, out _)
            ? $"{contextPrefix}.{AfterlifeStoryOutlineState.ResponseField}"
            : contextPrefix;

        if (outline.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                outlineContext,
                IssueSeverity.Error,
                "afterlifeStoryOutline должен быть object.",
                code: "afterlife_story_outline_response_not_object",
                section: "AfterlifeStoryOutline",
                expected: "object",
                actual: outline.ValueKind.ToString()));
            return;
        }

        ValidateAfterlifeStoryOutlinePayload(outline, outlineContext, issues);
    }

    private void ValidateAfterlifeStoryOutlinePayload(JsonElement outline, string context, List<ValidationIssue> issues)
    {
        RequireStoryOutlineScalarOrObject(outline, context, "mainArc", "afterlife_story_outline_missing_main_arc", issues);
        RequireStoryOutlineScalarOrObject(outline, context, "realmArc", "afterlife_story_outline_missing_realm_arc", issues);
        RequireStoryOutlineArray(outline, context, "actorSubplots", issues);
        RequireStoryOutlineArray(outline, context, "factionOrInstitutionArcs", issues);
        RequireStoryOutlineArray(outline, context, "loomingThreatsOrOpportunities", issues);
        RequireStoryOutlineArray(outline, context, "pendingRevelations", issues);
        RequireStoryOutlineArray(outline, context, "nextLikelySceneBeats", issues);
        RequireStoryOutlineScalarOrObject(outline, context, "playerAgencyNotes", "afterlife_story_outline_missing_player_agency_notes", issues);
        RequireStoryOutlineNonNegativeInteger(outline, context, "lastUpdatedTurn", "afterlife_story_outline_missing_last_updated_turn", issues);
        ValidateNoPlayerVisibleStoryOutlineFields(outline, context, issues);
    }

    private static void RequireStoryOutlineScalarOrObject(
        JsonElement outline,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!outline.TryGetProperty(propertyName, out var property))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"afterlife Writer's Room должен содержать {propertyName}.",
                code: code,
                section: "AfterlifeStoryOutline",
                expected: propertyName));
            return;
        }

        if (property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return;
        }

        if (property.ValueKind == JsonValueKind.Object)
            return;

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть non-empty string или object.",
            code: $"afterlife_story_outline_invalid_{ToSnakeCase(propertyName)}",
            section: "AfterlifeStoryOutline",
            expected: "non-empty string or object",
            actual: property.ValueKind.ToString()));
    }

    private static void RequireStoryOutlineArray(
        JsonElement outline,
        string context,
        string propertyName,
        List<ValidationIssue> issues)
    {
        if (!outline.TryGetProperty(propertyName, out var property))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"afterlife Writer's Room должен содержать {propertyName}[].",
                code: $"afterlife_story_outline_missing_{ToSnakeCase(propertyName)}",
                section: "AfterlifeStoryOutline",
                expected: $"{propertyName}[]"));
            return;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть array.",
                code: $"afterlife_story_outline_{ToSnakeCase(propertyName)}_not_array",
                section: "AfterlifeStoryOutline",
                expected: "array",
                actual: property.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(item.GetString()))
            {
                index++;
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                index++;
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}[{index}]",
                IssueSeverity.Error,
                $"{propertyName}[] должен содержать non-empty strings или objects.",
                code: $"afterlife_story_outline_invalid_{ToSnakeCase(propertyName)}_entry",
                section: "AfterlifeStoryOutline",
                expected: "non-empty string or object",
                actual: item.ValueKind.ToString()));
            index++;
        }
    }

    private static void ValidateNoPlayerVisibleStoryOutlineFields(
        JsonElement outline,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var prop in outline.EnumerateObject())
        {
            if (!IsPlayerVisibleStoryOutlineField(prop.Name))
                continue;

            issues.Add(new ValidationIssue(
                $"{context}.{prop.Name}",
                IssueSeverity.Error,
                "Afterlife Writer's Room является приватной комнатой писателя ГМа и не должен содержать player-visible response text.",
                code: "afterlife_story_outline_player_visible_text_forbidden",
                section: "AfterlifeStoryOutline",
                expected: "private planning fields only",
                actual: prop.Name,
                repairHint: "Перенеси текст для игрока в обычный response/interface output, а в afterlifeStoryOutline оставь только гибкий приватный план ГМа."));
        }
    }

    private void RequireStoryOutlineNonNegativeInteger(
        JsonElement outline,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!outline.TryGetProperty(propertyName, out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"afterlife Writer's Room должен содержать {propertyName}.",
                code: code,
                section: "AfterlifeStoryOutline",
                expected: "non-negative integer",
                actual: "missing"));
            return;
        }

        ValidateNonNegativeIntegerField(outline, context, issues, propertyName, "AfterlifeStoryOutline");
    }

    private static bool IsPlayerVisibleStoryOutlineField(string propertyName) =>
        propertyName.Equals("playerVisibleText", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("publicSummary", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("narrativeResponse", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("interfaceText", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("visibleToPlayer", StringComparison.OrdinalIgnoreCase);
}
