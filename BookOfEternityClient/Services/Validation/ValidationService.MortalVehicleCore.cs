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

    /// <summary>Checks detached historical semantics using the same pure rules as current vehicles.
    /// Does not resolve historical actors, locations or cargo against current authority.</summary>
    /// <param name="item">Historical complete vehicle object.</param>
    /// <param name="context">Historical diagnostic path.</param>
    /// <param name="issues">Receives immutable-evidence diagnostics.</param>
    internal static void ValidateHistoricalVehicleSemantics(JsonElement item, string context, List<ValidationIssue> issues)
    {
        var availability = ValidateVehicleTypeAndAvailability(item, context, issues, mustLookLikeFullVehicleObject: true);
        ValidateVehicleLegacyResourceFields(item, context, issues);
        ValidateVehicleAvailabilityLocation(item, context, issues, availability);
        if (item.TryGetProperty("actions", out var actions))
            ValidateCombatActionArray(actions, context + ".actions", issues, section: "Vehicles");
        if (item.TryGetProperty("resistances", out var resistances))
            ValidateCombatResistanceArray(resistances, context + ".resistances", issues);
    }

    private static string? ValidateVehicleTypeAndAvailability(JsonElement item, string context,
        List<ValidationIssue> issues, bool mustLookLikeFullVehicleObject)
    {
        var vehicleType = GetFirstNonEmptyString(item, "type");
        if (!mustLookLikeFullVehicleObject)
            ValidateOptionalString(item, context, issues, "type");
        if (!string.IsNullOrWhiteSpace(vehicleType) && !AllowedVehicleTypes.Contains(vehicleType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.type",
                IssueSeverity.Error,
                "Vehicle type должен быть одним из canonical enum значений",
                code: "vehicle_invalid_type",
                section: "Vehicles",
                expected: string.Join(" | ", AllowedVehicleTypes),
                actual: vehicleType,
                repairHint: "Используй для vehicle.type только Mount, Vehicle или Summonable по Block 10 contract."));
        }

        var availability = GetFirstNonEmptyString(item, "availability");
        if (!mustLookLikeFullVehicleObject)
            ValidateOptionalString(item, context, issues, "availability");
        if (!string.IsNullOrWhiteSpace(availability) && !AllowedVehicleAvailabilities.Contains(availability))
        {
            issues.Add(new ValidationIssue(
                $"{context}.availability",
                IssueSeverity.Error,
                "Vehicle availability должен быть одним из canonical enum значений",
                code: "vehicle_invalid_availability",
                section: "Vehicles",
                expected: string.Join(" | ", AllowedVehicleAvailabilities),
                actual: availability,
                repairHint: "Используй для vehicle.availability только Active, Parked или Pocket по Block 10 contract."));
        }
        return availability;
    }

    private static void ValidateVehicleLegacyResourceFields(JsonElement item, string context, List<ValidationIssue> issues)
    {
        foreach (var legacyField in new[] { "maxHealth", "currentHealth" })
        {
            if (!item.TryGetProperty(legacyField, out var legacyValue))
                continue;

            issues.Add(new ValidationIssue(
                $"{context}.{legacyField}",
                IssueSeverity.Error,
                "Vehicle resource value нельзя хранить вне unified resource ledger",
                code: "resource_owner_legacy_value_forbidden",
                section: "Vehicles",
                expected: "field absent; use resourceMaterialization for creation or resource commands for changes",
                actual: legacyValue.GetRawText(),
                repairHint: "Удали maxHealth/currentHealth. Новый транспорт передаёт только maximum через resourceMaterialization; текущим значением владеет клиентский resource ledger."));
        }
    }

    private static void ValidateVehicleAvailabilityLocation(JsonElement item, string context,
        List<ValidationIssue> issues, string? availability)
    {
        var currentLocationId = item.TryGetProperty("currentLocationId", out var currentLocationNode) &&
                                currentLocationNode.ValueKind == JsonValueKind.String
            ? currentLocationNode.GetString() ?? string.Empty
            : string.Empty;
        if ((string.Equals(availability, "Active", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(availability, "Pocket", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(currentLocationId))
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentLocationId",
                IssueSeverity.Error,
                "Vehicle с availability=Active или Pocket должен иметь currentLocationId = null",
                code: "vehicle_active_or_pocket_requires_null_location",
                section: "Vehicles",
                expected: "null currentLocationId for Active/Pocket vehicle",
                actual: currentLocationId,
                repairHint: "Для availability=Active или Pocket оставляй currentLocationId = null. Указывай locationId только для Parked vehicle."));
        }

        var hasParkedLocationNode = item.TryGetProperty("currentLocationId", out var parkedLocationNode);
        if (string.Equals(availability, "Parked", StringComparison.OrdinalIgnoreCase) &&
            !hasParkedLocationNode)
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentLocationId",
                IssueSeverity.Error,
                "Vehicle с availability=Parked должен явно содержать currentLocationId",
                code: "vehicle_parked_missing_location",
                section: "Vehicles",
                expected: "parked vehicle location id",
                actual: "missing",
                repairHint: "Для availability=Parked передай currentLocationId локации, где транспорт припаркован."));
        }
        else if (string.Equals(availability, "Parked", StringComparison.OrdinalIgnoreCase) &&
                 hasParkedLocationNode &&
                 parkedLocationNode.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentLocationId",
                IssueSeverity.Error,
                "Vehicle с availability=Parked не может иметь currentLocationId = null",
                code: "vehicle_parked_null_location_forbidden",
                section: "Vehicles",
                expected: "non-null parked vehicle location id",
                actual: "null",
                repairHint: "Для availability=Parked передай currentLocationId конкретной локации, где оставлен транспорт."));
        }
    }
}
