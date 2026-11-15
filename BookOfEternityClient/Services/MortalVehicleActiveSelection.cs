using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Resolves explicit active selection against the already composed vehicle identities.</summary>
internal static class MortalVehicleActiveSelection
{
    /// <summary>Checks selection without allocating identities or mutating the composed state.</summary>
    /// <param name="selection">Exact permanent ID, current-turn reference, or explicit null.</param>
    /// <param name="vehicles">Final vehicle objects from the existing owner composer.</param>
    /// <param name="sameTurnOwners">Already allocated owners; never a second identity registry.</param>
    /// <param name="issues">Bounded validation failures.</param>
    /// <param name="selectedVehicleId">Resolved permanent identity, or null for an empty selection.</param>
    /// <returns>True only when selection agrees with exactly the final Active vehicle.</returns>
    internal static bool TryResolve(
        JsonNode? selection,
        JsonArray vehicles,
        IReadOnlyList<ResourceOwnerExport> sameTurnOwners,
        List<ValidationIssue> issues,
        out string? selectedVehicleId)
    {
        selectedVehicleId = null;
        var active = vehicles.OfType<JsonObject>()
            .Where(v => v["availability"] is JsonValue value &&
                        value.TryGetValue<string>(out var text) && text == "Active")
            .ToArray();
        if (selection is null)
        {
            if (active.Length == 0)
                return true;
            return Invalid(issues, "null requires no final Active vehicle", "Active vehicle remains");
        }
        if (selection is not JsonValue selected ||
            !selected.TryGetValue<string>(out var identity) ||
            !ResourceMaterializationContract.IsExactIdentifier(identity))
            return Invalid(issues, "exact vehicleId or same-turn vehicleRef", selection.ToJsonString());

        var refs = sameTurnOwners.Where(owner => owner.Key.OwnerKind == ResourceOwnerKind.Vehicle &&
            string.Equals(owner.OwnerRef, identity, StringComparison.Ordinal)).ToArray();
        var ids = vehicles.OfType<JsonObject>().Where(v =>
            v["vehicleId"] is JsonValue id && id.TryGetValue<string>(out var text) &&
            string.Equals(text, identity, StringComparison.Ordinal)).ToArray();
        if (refs.Length + ids.Length != 1)
            return Invalid(issues, "one unambiguous final vehicle identity", identity);

        var resolved = refs.Length == 1 ? refs[0].Key.ResourceOwnerId : identity;
        if (active.Length != 1 ||
            active[0]["vehicleId"] is not JsonValue activeId ||
            !activeId.TryGetValue<string>(out var activeText) ||
            !string.Equals(activeText, resolved, StringComparison.Ordinal))
            return Invalid(issues, "selected vehicle is the only final Active vehicle", identity);
        selectedVehicleId = resolved;
        return true;
    }

    private static bool Invalid(List<ValidationIssue> issues, string expected, string actual)
    {
        issues.Add(new ValidationIssue(
            StorageTransportMoveService.VehiclesPath + ".activeVehicleChange",
            IssueSeverity.Error,
            "Active vehicle selection must agree with the accepted vehicle state.",
            code: "vehicle_active_selection_invalid", section: "Vehicles",
            expected: expected, actual: actual,
            repairHint: "Choose one exact existing vehicleId or same-turn vehicleRef and explicitly set its availability to Active. Park the previous vehicle with its exact location; null requires no Active vehicle."));
        return false;
    }
}
