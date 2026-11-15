using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record MortalCargoBirthCapacityResult(IReadOnlyList<ValidationIssue> Issues, JsonObject? Evidence)
{
    internal bool IsValid => Issues.Count == 0 && Evidence is not null;
}

/// <summary>Pure first-birth policy over an admitted projected graph. No I/O, cache,
/// identity allocation or publication authority. Returned evidence is detached historical DATA.</summary>
internal static class MortalCargoBirthCapacity
{
    private static readonly string[] Excluded =
    ["itemId", "id", "initialId", "existedId", "creationRef", "materializationReceipt", "materialization",
     "resourceMaterialization", "count", "quantity", "contentsPath", "isCarried", "currentLocationId", "currentLocationName"];

    /// <summary>Validates vehicle cargo using the unchanged vehicle carrier and evidence shape.</summary>
    internal static MortalCargoBirthCapacityResult Validate(string vehicleId, JsonObject birth,
        MortalItemCarrierCatalog catalog, IReadOnlyDictionary<string, string> creationIds, int sourceTurn, string context) =>
        ValidateCore(new("vehicle_inventory", vehicleId, null, Array.Empty<string>()), birth, "inventory", catalog,
            creationIds, sourceTurn, context, "resource_owner_vehicle_cargo_capacity_invalid", "Vehicles", new JsonObject
            { ["schemaVersion"] = 1, ["vehicleId"] = vehicleId, ["sourceTurn"] = sourceTurn });

    /// <summary>Validates first-birth storage contents at one exact real location/storage coordinate.
    /// The supplied graph must already be admitted; this pure operation grants no publication permission.</summary>
    /// <param name="locationId">Permanent accepted location identity.</param>
    /// <param name="storageId">Exact storage identity within that location.</param>
    /// <param name="birth">Detached birth declaration, including contents and optional measurements.</param>
    /// <param name="catalog">The operation's already projected item catalog.</param>
    /// <param name="creationIds">Frozen current item allocations.</param>
    /// <param name="sourceTurn">Validated birth turn.</param>
    /// <param name="context">Exact raw storage diagnostic path.</param>
    /// <returns>Detached historical measurements, never current authority.</returns>
    internal static MortalCargoBirthCapacityResult ValidateStorage(string locationId, string storageId, JsonObject birth,
        MortalItemCarrierCatalog catalog, IReadOnlyDictionary<string, string> creationIds, int sourceTurn, string context) =>
        ValidateCore(new("location_storage", locationId, storageId, Array.Empty<string>()), birth, "contents", catalog,
            creationIds, sourceTurn, context, "mortal_location_storage_cargo_capacity_invalid", "mortal_location_materialization", new JsonObject
            { ["schemaVersion"] = 1, ["carrierKind"] = "location_storage", ["locationId"] = locationId,
              ["storageId"] = storageId, ["sourceTurn"] = sourceTurn });

    private static MortalCargoBirthCapacityResult ValidateCore(MortalItemCarrierCoordinate coordinate, JsonObject birth,
        string contentsField, MortalItemCarrierCatalog catalog, IReadOnlyDictionary<string, string> creationIds,
        int sourceTurn, string context, string code, string section, JsonObject evidenceRoot)
    {
        var issues = new List<ValidationIssue>();
        void Error(string field, string expected) => issues.Add(new ValidationIssue(context + field,
            IssueSeverity.Error, "Invalid first-birth cargo capacity or measurement.",
            code: code, section: section,
            expected: expected, actual: "invalid birth cargo"));
        MortalCargoBirthCapacityResult Invalid() => new(issues.ToArray(), null);
        if (catalog.Issues.Count != 0)
        { Error("." + contentsField, "already admitted unambiguous projected item graph"); return Invalid(); }
        var cargo = catalog.Occurrences.Where(o => o.Carrier.Kind == coordinate.Kind &&
            o.Carrier.OwnerId == coordinate.OwnerId && o.Carrier.ContainerId == coordinate.ContainerId).ToArray();
        if (birth[contentsField] is not JsonArray inventory || inventory.Count != cargo.Length || inventory.Any(n => n is not JsonObject))
        { Error("." + contentsField, "complete projected coverage of every birth cargo object"); return Invalid(); }
        var byId = new Dictionary<string, MortalItemCarrierOccurrence>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<MortalItemCarrierOccurrence>>(StringComparer.Ordinal);
        var roots = new List<MortalItemCarrierOccurrence>();
        foreach (var item in cargo)
        {
            if (item.ItemId is null || !byId.TryAdd(item.ItemId, item))
            { Error("." + contentsField, "unique resolved item IDs"); continue; }
            if (item.Carrier.ContainerPath.Count == 0) roots.Add(item);
            else
            {
                var parent = item.Carrier.ContainerPath[^1];
                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new();
                list.Add(item);
            }
        }
        if (cargo.Length > 0 && roots.Count == 0) Error("." + contentsField, "at least one direct root for nonempty cargo");
        if (!Integer(birth["capacity"], out var slots) || slots < 0 || roots.Count > slots)
            Error(".capacity", "nonnegative direct-stack limit covering all direct roots");
        if (!Number(birth["volume"], out var limit, out _)) Error(".volume", "finite nonnegative cargo limit");
        if (issues.Count != 0) return Invalid();
        if (birth.ContainsKey("cargoMeasurements") && birth["cargoMeasurements"] is not JsonArray)
        { Error(".cargoMeasurements", "measurement array or absent when unnecessary"); return Invalid(); }
        var rows = new Dictionary<string, (JsonObject Row, int Index)>(StringComparer.Ordinal);
        var rawRows = birth["cargoMeasurements"] as JsonArray ?? new JsonArray();
        for (var i = 0; i < rawRows.Count; i++)
        {
            if (rawRows[i] is not JsonObject row || !Closed(row, "itemId", "creationRef", "count", "externalVolumeDm3", "measuredConfiguration") ||
                Resolve(row) is not string id || !byId.TryGetValue(id, out var item) || item.Carrier.ContainerPath.Count != 0 || !rows.TryAdd(id, (row, i)))
                Error($".cargoMeasurements[{i}]", "one exact closed row for a direct cargo root");
        }
        var evidenceRows = new JsonArray();
        var sum = BigInteger.Zero;
        foreach (var root in roots)
        {
            var id = root.ItemId!;
            if (!Integer(root.Item["count"], out var count) || count <= 0 || root.Item["isContainer"] is not JsonValue flag || !flag.TryGetValue<bool>(out var container))
            { Error("." + contentsField, "admitted positive item count and container flag"); continue; }
            var requiresRow = container || count != 1;
            var hasRow = rows.TryGetValue(id, out var provided);
            if (requiresRow != hasRow)
            { Error(".cargoMeasurements", requiresRow ? "explicit whole-stack measurement" : "no redundant measurement for a single noncontainer"); continue; }
            BigInteger volume;
            double physical;
            JsonObject? basis = null;
            if (!requiresRow)
            {
                if (!Number(root.Item["volume"], out volume, out physical))
                { Error("." + contentsField, "finite nonnegative physical volume"); continue; }
            }
            else
            {
                var row = provided.Row;
                var path = $".cargoMeasurements[{provided.Index}]";
                if (!Integer(row["count"], out var declared) || declared != count ||
                    !Number(row["externalVolumeDm3"], out volume, out physical) ||
                    row["measuredConfiguration"] is not JsonObject asserted ||
                    !Closed(asserted, "schemaVersion", "item", "contents") ||
                    !Integer(asserted["schemaVersion"], out var version) || version != 1)
                { Error(path, "exact count, finite whole-stack volume and closed v1 configuration"); continue; }
                var visited = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    if (!Match(root, asserted, visited))
                    { Error(path + ".measuredConfiguration", "exact current semantic shape and descendant identity/count graph"); continue; }
                    basis = BuildBasis(root, isRoot: true);
                }
                catch (InvalidDataException)
                { Error(path + ".measuredConfiguration", "nonconflicting exact semantic references"); continue; }
            }
            basis ??= BuildBasis(root, isRoot: true);
            sum += volume;
            var evidence = new JsonObject { ["itemId"] = id, ["count"] = count, ["externalVolumeDm3"] = physical,
                ["materializationId"] = root.Item["materialization"]?["materializationId"]?.DeepClone() };
            if (basis is not null)
            {
                evidence["measuredConfiguration"] = basis;
                evidence["configurationFingerprint"] = ConfigurationFingerprint(basis);
            }
            evidenceRows.Add(evidence);
        }
        if (sum > limit) Error(".volume", "whole-stack outer volume no greater than cargo limit");
        if (issues.Count != 0) return Invalid();
        evidenceRoot["cargoMeasurements"] = evidenceRows;
        return new(Array.Empty<ValidationIssue>(), evidenceRoot);

        string? Resolve(JsonObject node)
        {
            if (node.ContainsKey("itemId") == node.ContainsKey("creationRef")) return null;
            if (node["itemId"] is JsonValue id && id.TryGetValue<string>(out var text) && ResourceMaterializationContract.IsExactIdentifier(text)) return text;
            return node["creationRef"] is JsonValue reference && reference.TryGetValue<string>(out var key) &&
                ResourceMaterializationContract.IsExactIdentifier(key) && creationIds.TryGetValue(key, out var resolved) ? resolved : null;
        }
        JsonObject Shape(JsonObject item)
        {
            var shape = item.DeepClone().AsObject();
            foreach (var field in Excluded) shape.Remove(field);
            CanonicalStateNormalizer.RewriteMortalItemCreationReferences(shape, creationIds);
            return shape;
        }
        bool Match(MortalItemCarrierOccurrence actual, JsonObject assertion, HashSet<string> visited)
        {
            if (!visited.Add(actual.ItemId!) || assertion["item"] is not JsonObject supplied || assertion["contents"] is not JsonArray contents) return false;
            var normalized = supplied.DeepClone();
            CanonicalStateNormalizer.RewriteMortalItemCreationReferences(normalized, creationIds);
            if (!MortalItemMaterializationContract.ImmutableEvidenceEquals(Shape(actual.Item), normalized)) return false;
            var expected = children.GetValueOrDefault(actual.ItemId!) ?? new List<MortalItemCarrierOccurrence>();
            if (expected.Count != contents.Count) return false;
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in contents)
            {
                if (node is not JsonObject child || !Closed(child, "itemId", "creationRef", "count", "item", "contents") ||
                    Resolve(child) is not string id || !found.Add(id) || !byId.TryGetValue(id, out var descendant) ||
                    descendant.Carrier.ContainerPath.Count == 0 || descendant.Carrier.ContainerPath[^1] != actual.ItemId ||
                    !Integer(child["count"], out var declared) || !Integer(descendant.Item["count"], out var actualCount) || declared != actualCount ||
                    !Match(descendant, child, visited)) return false;
            }
            return true;
        }
        JsonObject BuildBasis(MortalItemCarrierOccurrence actual, bool isRoot)
        {
            var basis = new JsonObject { ["item"] = Shape(actual.Item), ["contents"] = new JsonArray() };
            if (isRoot) basis["schemaVersion"] = 1;
            else { basis["itemId"] = actual.ItemId; basis["count"] = actual.Item["count"]!.DeepClone(); }
            if (children.TryGetValue(actual.ItemId!, out var nested))
                foreach (var child in nested.OrderBy(n => n.ItemId, StringComparer.Ordinal))
                    basis["contents"]!.AsArray().Add(BuildBasis(child, isRoot: false));
            return basis;
        }
    }

    private static bool Closed(JsonObject node, params string[] allowed) => node.All(p => allowed.Contains(p.Key, StringComparer.Ordinal));
    private static bool Integer(JsonNode? node, out int value)
    { value = 0; return node is JsonValue scalar && scalar.TryGetValue<int>(out value); }

    // Exact arithmetic over finite double roundtrip strings, NOT raw JSON token arithmetic.
    // 10^-324 is a common integer unit covering every finite double roundtrip value.
    /// <summary>Converts one finite nonnegative double to the shared exact round-trip integer units.</summary>
    internal static bool Number(JsonNode? node, out BigInteger units, out double value)
    {
        units = BigInteger.Zero; value = 0;
        if (node is not JsonValue scalar || scalar.GetValueKind() != JsonValueKind.Number ||
            !double.TryParse(scalar.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value) || value < 0) return false;
        if (value == 0) { value = 0; return true; }
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var parts = text.Split('E', 'e');
        var exponent = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var point = parts[0].IndexOf('.');
        var fraction = point < 0 ? 0 : parts[0].Length - point - 1;
        var digits = parts[0].Replace(".", "", StringComparison.Ordinal);
        units = BigInteger.Parse(digits, CultureInfo.InvariantCulture) * BigInteger.Pow(10, 324 + exponent - fraction);
        return true;
    }

    /// <summary>Fingerprints the exact detached configuration using the established cargo convention.</summary>
    internal static string ConfigurationFingerprint(JsonObject basis) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(basis).ToJsonString())));

    private static JsonNode Canonical(JsonNode node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value is null ? null : Canonical(p.Value)))),
        JsonArray array => new JsonArray(array.Select(n => n is null ? null : Canonical(n)).ToArray()),
        _ => node.DeepClone()
    };
}
