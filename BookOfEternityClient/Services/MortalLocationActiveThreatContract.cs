using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalLocationActiveThreatContract
{
    private static readonly HashSet<string> AllowedMotivations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Domination", "Consumption", "Preservation", "Corruption", "Accumulation", "Execution", "Custom"
    };

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Overt", "Covert", "Deceptive", "Opportunistic", "Systemic", "Custom"
    };

    private static readonly HashSet<string> AllowedTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Faction", "Location", "Resource"
    };

    private static readonly HashSet<string> AllowedImpacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Military", "Economic", "Social", "Covert", "Stability", "Environment"
    };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement threat,
        string context,
        bool requireNullThreatId = false)
    {
        var issues = new List<ValidationIssue>();
        if (threat.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context, "object", threat.ValueKind.ToString());
            return issues;
        }

        if (!threat.TryGetProperty("threatId", out var threatId) ||
            requireNullThreatId && threatId.ValueKind != JsonValueKind.Null ||
            !requireNullThreatId && !IsNonEmptyString(threatId))
        {
            Add(
                issues,
                context + ".threatId",
                requireNullThreatId ? "null for a new threat" : "exact non-empty permanent threatId",
                Describe(threat, "threatId"));
        }

        RequireString(threat, context, "name", issues);
        ValidateOptionalString(threat, context, "description", issues);
        RequireNonNegativeInt(threat, context, "intensity", issues);
        RequireString(threat, context, "longTermGoal", issues);
        ValidateCurrentActivity(threat, context, issues);
        ValidateArchetype(threat, context, issues);
        ValidateImpactProfile(threat, context, issues);
        return issues;
    }

    private static void ValidateCurrentActivity(
        JsonElement threat,
        string context,
        List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("currentActivity", out var activity))
        {
            Add(issues, context + ".currentActivity", "object or null", "missing");
            return;
        }
        if (activity.ValueKind == JsonValueKind.Null)
            return;
        if (activity.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context + ".currentActivity", "object or null", activity.ValueKind.ToString());
            return;
        }

        var activityContext = context + ".currentActivity";
        RequireString(activity, activityContext, "activityName", issues);
        RequireString(activity, activityContext, "description", issues);
        RequireInt(activity, activityContext, "totalTimeCostMinutes", issues);
        RequireInt(activity, activityContext, "timeSpentMinutes", issues);
        RequireInt(activity, activityContext, "currentStepNumber", issues);
        RequireInt(activity, activityContext, "totalStepsInActivity", issues);
        ValidateOptionalNullableString(activity, activityContext, "linkedQuestId", issues);
        ValidateOptionalNullableString(activity, activityContext, "linkedPlotOutlineNode", issues);
        if (activity.TryGetProperty("activeState", out var activeState))
        {
            if (!IsNonEmptyString(activeState))
            {
                Add(issues, activityContext + ".activeState", "non-empty string", Describe(activity, "activeState"));
            }
            else if (activeState.GetString() is "Completed" or "Abandoned")
            {
                Add(
                    issues,
                    activityContext + ".activeState",
                    "non-terminal activity state",
                    activeState.GetString()!);
            }
        }
    }

    private static void ValidateArchetype(
        JsonElement threat,
        string context,
        List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("threatArchetype", out var archetype) ||
            archetype.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context + ".threatArchetype", "object", Describe(threat, "threatArchetype"));
            return;
        }

        var archetypeContext = context + ".threatArchetype";
        var motivation = RequireString(archetype, archetypeContext, "motivation", issues);
        var method = RequireString(archetype, archetypeContext, "method", issues);
        if (motivation != null && !AllowedMotivations.Contains(motivation))
            Add(issues, archetypeContext + ".motivation", string.Join(" | ", AllowedMotivations), motivation);
        if (method != null && !AllowedMethods.Contains(method))
            Add(issues, archetypeContext + ".method", string.Join(" | ", AllowedMethods), method);

        if (string.Equals(motivation, "Custom", StringComparison.OrdinalIgnoreCase))
            RequireString(archetype, archetypeContext, "customMotivation", issues);
        else
            ValidateOptionalNullableString(archetype, archetypeContext, "customMotivation", issues);
        if (string.Equals(method, "Custom", StringComparison.OrdinalIgnoreCase))
            RequireString(archetype, archetypeContext, "customMethod", issues);
        else
            ValidateOptionalNullableString(archetype, archetypeContext, "customMethod", issues);
    }

    private static void ValidateImpactProfile(
        JsonElement threat,
        string context,
        List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("impactProfile", out var impact) ||
            impact.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context + ".impactProfile", "object", Describe(threat, "impactProfile"));
            return;
        }

        var impactContext = context + ".impactProfile";
        var targetType = RequireString(impact, impactContext, "primaryTargetType", issues);
        if (targetType != null && !AllowedTargetTypes.Contains(targetType))
            Add(issues, impactContext + ".primaryTargetType", string.Join(" | ", AllowedTargetTypes), targetType);
        RequireNullableString(impact, impactContext, "primaryTargetId", issues);
        RequireString(impact, impactContext, "primaryTargetName", issues);
        var primaryImpact = RequireString(impact, impactContext, "primaryImpact", issues);
        if (primaryImpact != null && !AllowedImpacts.Contains(primaryImpact))
            Add(issues, impactContext + ".primaryImpact", string.Join(" | ", AllowedImpacts), primaryImpact);
        RequireInt(impact, impactContext, "baseImpactValue", issues);
    }

    private static string? RequireString(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && IsNonEmptyString(value))
            return value.GetString();
        Add(issues, context + "." + field, "non-empty string", Describe(root, field));
        return null;
    }

    private static void ValidateOptionalString(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.String)
            Add(issues, context + "." + field, "string", value.ValueKind.ToString());
    }

    private static void RequireInt(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out _))
        {
            Add(issues, context + "." + field, "integer", Describe(root, field));
        }
    }

    private static void RequireNonNegativeInt(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) ||
            number < 0)
        {
            Add(issues, context + "." + field, "non-negative integer", Describe(root, field));
        }
    }

    private static void RequireNullableString(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Null && !IsNonEmptyString(value))
        {
            Add(issues, context + "." + field, "non-empty string or null", Describe(root, field));
        }
    }

    private static void ValidateOptionalNullableString(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind != JsonValueKind.Null &&
            !IsNonEmptyString(value))
        {
            Add(issues, context + "." + field, "non-empty string or null", value.GetRawText());
        }
    }

    private static bool IsNonEmptyString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());

    private static string Describe(JsonElement root, string field) =>
        !root.TryGetProperty(field, out var value) ? "missing" : value.GetRawText();

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal location active threat violates the complete canonical threat contract.",
            code: "mortal_location_threat_semantic_invalid",
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete, type-correct Active Threat object through its governed location command."));
}
