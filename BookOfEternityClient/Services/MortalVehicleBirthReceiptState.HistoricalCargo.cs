using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalVehicleBirthReceiptState
{
    /// <summary>
    /// Reuses the existing item contract, carrier index and capacity algorithm on one
    /// owned historical birth copy. The catalog is never exported as active authority.
    /// No live cargo, actor, location or identity-index lookup is performed.
    /// </summary>
    /// <param name="birth">Detached owned birth copy; may receive reconstructed measurement input.</param>
    /// <param name="evidence">Original immutable cargo evidence, never mutated.</param>
    /// <param name="vehicleId">Receipt's permanent identity.</param>
    /// <param name="vehicleRef">Receipt's original transient source reference.</param>
    /// <param name="turn">Original source turn.</param>
    /// <returns>Whether the full historical cargo facts reproduce the exact existing evidence.</returns>
    private static bool ValidateHistoricalCargo(JsonObject birth, JsonObject evidence, string vehicleId,
        string vehicleRef, int turn)
    {
        if (!Closed(evidence, "schemaVersion", "vehicleId", "sourceTurn", "cargoMeasurements") ||
            !Integer(evidence["schemaVersion"], out var version) || version != 1 ||
            evidence["cargoMeasurements"] is not JsonArray rows || birth["inventory"] is not JsonArray)
            return false;
        // The caller already owns this single detached clone. Reuse the catalog's
        // own indexes instead of cloning the snapshot or constructing a second index.
        var historical = new JsonObject { ["vehicles"] = new JsonArray(birth) };
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, null, historical,
            new Dictionary<string, JsonObject>()));
        if (catalog.Issues.Count != 0) return false;
        var containerIssues = new List<ValidationIssue>();
        ValidationService.ValidateCanonicalContainerPaths(catalog, containerIssues);
        if (containerIssues.Count != 0) return false;
        foreach (var occurrence in catalog.Occurrences)
        {
            if (MortalItemMaterializationContract.Validate(JsonSerializer.SerializeToElement(occurrence.Item),
                    occurrence.JsonPath, MortalItemMaterializationPhase.CanonicalPostSeal).Count != 0 ||
                occurrence.Item[MortalItemMaterializationContract.ReceiptProperty] is not JsonObject itemReceipt ||
                !Integer(itemReceipt["acceptedAtTurn"], out var acceptedTurn) || acceptedTurn != turn ||
                occurrence.Item["materialization"] is not JsonObject envelope ||
                Exact(envelope["route"]) != "new_vehicle_inventory" ||
                !Integer(envelope["sourceTurn"], out var itemTurn) || itemTurn != turn ||
                envelope["sourceAuthority"] is not JsonObject source || Exact(source["kind"]) != "new_vehicle" ||
                Exact(source["authorityId"]) != vehicleRef)
                return false;
        }
        if (rows.Count != catalog.Occurrences.Count(item => item.Carrier.ContainerPath.Count == 0)) return false;
        var measurements = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in rows)
        {
            if (node is not JsonObject row || !Closed(row, "itemId", "count", "externalVolumeDm3", "materializationId",
                    "measuredConfiguration", "configurationFingerprint") ||
                Exact(row["itemId"]) is not { } id || !seen.Add(id) ||
                !catalog.ByItemId.TryGetValue(id, out var matches) || matches.Count != 1 ||
                matches[0].Carrier.ContainerPath.Count != 0 ||
                !Integer(matches[0].Item["count"], out var count) || count < 1 ||
                matches[0].Item["isContainer"] is not JsonValue flag || !flag.TryGetValue<bool>(out var container))
                return false;
            // Simple-unit footprint is derived. Stacks/containers reuse their original
            // declared whole-stack measurement and self-contained configuration.
            if (container || count != 1)
                measurements.Add(new JsonObject
                {
                    ["itemId"] = id, ["count"] = row["count"]?.DeepClone(),
                    ["externalVolumeDm3"] = row["externalVolumeDm3"]?.DeepClone(),
                    ["measuredConfiguration"] = row["measuredConfiguration"]?.DeepClone()
                });
        }
        birth["cargoMeasurements"] = measurements;
        var rebuilt = MortalCargoBirthCapacity.Validate(vehicleId, birth, catalog,
            new Dictionary<string, string>(), turn, "vehicles.birthReceipts.birthState");
        return rebuilt.IsValid && MortalItemMaterializationContract.ImmutableEvidenceEquals(rebuilt.Evidence!, evidence);
    }
}
