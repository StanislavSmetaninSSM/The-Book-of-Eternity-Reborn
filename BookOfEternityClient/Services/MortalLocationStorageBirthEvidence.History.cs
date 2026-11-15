using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLocationStorageBirthEvidence
{
    private static readonly string[] IdentityShapeFields =
    ["itemId", "id", "initialId", "existedId", "creationRef", "materializationReceipt", "materialization",
     "resourceMaterialization", "count", "quantity", "contentsPath", "isCarried", "currentLocationId", "currentLocationName"];

    /// <summary>Strictly checks a present historical storage payload using only its captured DATA.
    /// Absence on unrelated old paths is not a request to fabricate a birth record.</summary>
    /// <param name="transition">One existing location child history record.</param>
    /// <param name="locationId">Its exact parent identity.</param>
    /// <param name="storageId">Its exact child identity.</param>
    /// <param name="context">Historical diagnostic path.</param>
    /// <returns>Integrity diagnostics; never current item/access authority.</returns>
    internal static IReadOnlyList<ValidationIssue> ValidateHistorical(JsonObject transition, string locationId,
        string storageId, string context)
    {
        if (transition["afterState"] is not JsonObject after || after["cargoBirthEvidence"] is not JsonObject evidence ||
            Text(transition["kind"]) != "storage_addition" || Text(transition["entityId"]) != locationId ||
            Text(transition["sourceLocationId"]) != locationId || Text(transition["targetLocationId"]) != locationId ||
            Text(transition["childId"]) != storageId || Text(after["storageId"]) != storageId ||
            transition["beforeState"] is not JsonObject before || !Closed(before, "state") || Text(before["state"]) != "absent" ||
            after.ContainsKey("contents") || after.ContainsKey("cargoMeasurements") ||
            !Integer(transition["turn"], out var turn) ||
            !Closed(evidence, "schemaVersion", "carrierKind", "locationId", "storageId", "sourceTurn", "sourceAuthority", "cargoMeasurements") ||
            !Integer(evidence["schemaVersion"], out var version) || version != 1 ||
            Text(evidence["carrierKind"]) != "location_storage" || Text(evidence["locationId"]) != locationId ||
            Text(evidence["storageId"]) != storageId || !Integer(evidence["sourceTurn"], out var sourceTurn) || sourceTurn != turn ||
            evidence["sourceAuthority"] is not JsonObject source || !Closed(source, "sessionId", "requestId", "snapshotToken") ||
            source.Any(pair => !Exact(Text(pair.Value))) || evidence["cargoMeasurements"] is not JsonArray rows ||
            !Integer(after["capacity"], out var capacity) || capacity < 0 || rows.Count > capacity ||
            !MortalCargoBirthCapacity.Number(after["volume"], out var volumeLimit, out _) ||
            MortalLocationStorageMetadataContract.Validate(JsonSerializer.SerializeToElement(after), context + ".afterState").Count != 0)
            return Invalid();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var total = BigInteger.Zero;
        foreach (var node in rows)
        {
            if (node is not JsonObject row || !Closed(row, "itemId", "count", "externalVolumeDm3", "materializationId", "measuredConfiguration", "configurationFingerprint") ||
                !Observe(Text(row["itemId"])) || !Integer(row["count"], out var count) || count < 1 ||
                !Exact(Text(row["materializationId"])) ||
                !MortalCargoBirthCapacity.Number(row["externalVolumeDm3"], out var outer, out _) ||
                row["measuredConfiguration"] is not JsonObject basis || !Closed(basis, "schemaVersion", "item", "contents") ||
                !Integer(basis["schemaVersion"], out var basisVersion) || basisVersion != 1 ||
                Text(row["configurationFingerprint"]) != MortalCargoBirthCapacity.ConfigurationFingerprint(basis) ||
                !ValidateNode(basis, count, out var physical, out var container) ||
                count == 1 && !container && outer != physical)
                return Invalid();
            total += outer;
        }
        return total <= volumeLimit ? Array.Empty<ValidationIssue>() : Invalid();

        bool Observe(string? id) => Exact(id) && ids.Add(id!) && aliases.Add(ResourceMaterializationContract.BuildConfusableKey(id!));
        bool ValidateNode(JsonObject node, int count, out BigInteger physical, out bool container)
        {
            physical = BigInteger.Zero; container = false;
            if (node["item"] is not JsonObject item || node["contents"] is not JsonArray children ||
                item.Any(pair => IdentityShapeFields.Contains(pair.Key, StringComparer.Ordinal)) ||
                item["isContainer"] is not JsonValue flag || !flag.TryGetValue(out container) ||
                !MortalCargoBirthCapacity.Number(item["volume"], out physical, out _) || children.Count != 0 && !container)
                return false;
            foreach (var childNode in children)
            {
                if (childNode is not JsonObject child || !Closed(child, "itemId", "count", "item", "contents") ||
                    !Observe(Text(child["itemId"])) || !Integer(child["count"], out var childCount) || childCount < 1 ||
                    !ValidateNode(child, childCount, out _, out _)) return false;
            }
            return true;
        }
        IReadOnlyList<ValidationIssue> Invalid() => new[] { Issue(context + ".afterState.cargoBirthEvidence",
            "mortal_location_storage_birth_evidence_invalid", "complete immutable exact storage birth evidence at its original limits and source") };
    }

    private static bool Closed(JsonObject node, params string[] fields) => node.Count == fields.Length && fields.All(node.ContainsKey);
}
