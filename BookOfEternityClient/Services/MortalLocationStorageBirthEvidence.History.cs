using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLocationStorageBirthEvidence
{
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
        var items = new JsonArray();
        var measurements = new JsonArray();
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
            Capture(basis, Text(row["itemId"])!, count, Text(row["materializationId"])!, Array.Empty<string>());
            if (container || count != 1)
                measurements.Add(new JsonObject { ["itemId"] = row["itemId"]!.DeepClone(), ["count"] = count,
                    ["externalVolumeDm3"] = row["externalVolumeDm3"]!.DeepClone(), ["measuredConfiguration"] = basis.DeepClone() });
        }
        var storage = Metadata(after);
        storage["contents"] = items;
        storage["cargoMeasurements"] = measurements;
        var current = new JsonObject { ["locationId"] = locationId, ["locationStorages"] = new JsonArray(storage.DeepClone()) };
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, current, null, new Dictionary<string, JsonObject>()));
        var graphIssues = new List<ValidationIssue>();
        ValidationService.ValidateCanonicalContainerPaths(catalog, graphIssues);
        if (catalog.Issues.Count != 0 || graphIssues.Count != 0) return Invalid();
        var rebuilt = MortalCargoBirthCapacity.ValidateStorage(locationId, storageId, storage, catalog,
            new Dictionary<string, string>(), turn, context);
        var captured = evidence.DeepClone().AsObject(); captured.Remove("sourceAuthority");
        return rebuilt.IsValid && MortalItemMaterializationContract.ImmutableEvidenceEquals(rebuilt.Evidence!, captured)
            ? Array.Empty<ValidationIssue>() : Invalid();

        void Capture(JsonObject basis, string id, int count, string materializationId, IReadOnlyList<string> parents)
        {
            var item = basis["item"]!.DeepClone().AsObject();
            item["itemId"] = id; item["count"] = count;
            item["contentsPath"] = parents.Count == 0 ? null : new JsonArray(parents.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray());
            item["materialization"] = new JsonObject { ["materializationId"] = materializationId };
            items.Add(item);
            foreach (var child in basis["contents"]!.AsArray().OfType<JsonObject>())
                Capture(child, Text(child["itemId"])!, child["count"]!.GetValue<int>(), "historical_nested", parents.Append(id).ToArray());
        }

        bool Observe(string? id) => Exact(id) && ids.Add(id!) && aliases.Add(ResourceMaterializationContract.BuildConfusableKey(id!));
        bool ValidateNode(JsonObject node, int count, out BigInteger physical, out bool container)
        {
            physical = BigInteger.Zero; container = false;
            if (node["item"] is not JsonObject item || node["contents"] is not JsonArray children ||
                item.Any(pair => MortalCargoBirthCapacity.IsIdentityShapeField(pair.Key)) ||
                item["isContainer"] is not JsonValue flag || !flag.TryGetValue(out container) ||
                !MortalCargoBirthCapacity.Number(item["volume"], out physical, out _) || children.Count != 0 && !container ||
                MortalItemMaterializationContract.ValidateCapturedConfiguration(item, count, context).Count != 0)
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
