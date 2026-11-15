using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Validates required full-vehicle core fields against the same rules for new,
    /// current and historical birth DATA. Performs no reads or authority checks.
    /// Nested semantics remain with the existing vehicle/item validators.
    /// </summary>
    /// <param name="item">A vehicle object already admitted as an object.</param>
    /// <param name="context">Its diagnostic path.</param>
    /// <param name="issues">Receives existing shape diagnostics without mutation of the object.</param>
    internal static void ValidateVehicleCoreFields(JsonElement item, string context, List<ValidationIssue> issues)
    {
        RequireString(item, context, issues, "name");
        RequireString(item, context, issues, "description");
        RequireString(item, context, issues, "image_prompt");
        RequireString(item, context, issues, "type");
        RequireBooleanField(item, context, issues, "isSentient");
        RequireString(item, context, issues, "availability");
        ValidateRequiredNullableStringField(item, context, issues, "currentLocationId");
        if (!item.TryGetProperty("speedBonus", out _))
            issues.Add(new ValidationIssue(context + ".speedBonus", IssueSeverity.Error,
                "Full vehicle object requires an explicit integer speedBonus.", code: "vehicle_missing_speed_bonus",
                section: "Vehicles", expected: "integer speedBonus", actual: "missing"));
        else
            ValidateIntegerField(item, context, issues, "speedBonus");
        RequireObjectArrayField(item, context, issues, "actions");
        RequireObjectArrayField(item, context, issues, "resistances");
        RequireObjectArrayField(item, context, issues, "inventory");
    }
}
