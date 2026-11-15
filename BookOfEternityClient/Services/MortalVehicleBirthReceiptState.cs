using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Immutable vehicle birth DATA in the existing vehicle companion.
/// Seals prove internal consistency, never publication permission or current cargo/access.</summary>
internal static partial class MortalVehicleBirthReceiptState
{
    internal static JsonObject CreateEmptyRoot() => new()
    { ["vehicles"] = new JsonArray(), ["activeVehicleId"] = null, ["birthReceipts"] = new JsonArray() };

    internal static IReadOnlyList<ValidationIssue> ValidateContinuity(JsonObject original, JsonObject raw)
    {
        if (original.ContainsKey("birthReceipts") == raw.ContainsKey("birthReceipts") &&
            JsonNode.DeepEquals(original["birthReceipts"], raw["birthReceipts"]))
            return Array.Empty<ValidationIssue>();
        return new[] { Issue("birthReceipts", "Preserve the exact signed-original client-owned birth receipts.") };
    }

    internal static JsonObject CreateReceipt(JsonObject vehicle, string vehicleRef, int turn,
        string sessionId, string requestId, string snapshotToken, IReadOnlyList<ResourceOwnerCapacityDraft> drafts)
    {
        var id = Exact(vehicle["vehicleId"]) ?? throw new InvalidDataException("Resolved vehicle identity required.");
        var health = drafts.Where(d => d.Coordinate.Realm == "mortal_world" &&
            d.Coordinate.OwnerKind == ResourceOwnerKind.Vehicle && d.Coordinate.ResourceOwnerId == id).ToArray();
        if (health.Length != 1 || health[0].Coordinate.ResourceKey != "health")
            throw new InvalidDataException("One admitted health initialization is required per vehicle birth.");
        var source = health[0].SourceEvidence;
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1, ["vehicleId"] = id, ["vehicleRef"] = vehicleRef, ["sourceTurn"] = turn,
            ["sourceAuthority"] = new JsonObject
            { ["sessionId"] = sessionId, ["requestId"] = requestId, ["snapshotToken"] = snapshotToken },
            ["birthState"] = vehicle.DeepClone(),
            ["resourceInitialization"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "health", ["maximum"] = health[0].AcceptedMaximum,
                ["sourceKind"] = source.SourceKind, ["sourceId"] = source.SourceId,
                ["authorityFingerprint"] = source.AuthorityFingerprint
            })
        };
        receipt["seal"] = ComputeSeal(receipt);
        return receipt;
    }

    internal static string ComputeSeal(JsonObject receipt)
    {
        var copy = receipt.DeepClone().AsObject();
        copy.Remove("seal");
        using var fingerprint = new ResourceFingerprintBuilder("mortal-vehicle-birth-receipt-v1");
        fingerprint.Append(WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy)!);
        return fingerprint.Build();
    }

    internal static IReadOnlyList<ValidationIssue> ValidateLocal(JsonObject root)
    {
        var issues = new List<ValidationIssue>();
        if (root["vehicles"] is not JsonArray vehicles || root["birthReceipts"] is not JsonArray receipts)
            return new[] { Issue("birthReceipts", "Current vehicle object with explicit vehicles and birthReceipts arrays.") };
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in receipts)
        {
            if (node is not JsonObject receipt || !Closed(receipt, "schemaVersion", "vehicleId", "vehicleRef", "sourceTurn", "sourceAuthority", "birthState", "resourceInitialization", "seal") ||
                !Integer(receipt["schemaVersion"], out var version) || version != 1 ||
                Exact(receipt["vehicleId"]) is not { } id || !ids.Add(id) ||
                !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(id)) ||
                Exact(receipt["vehicleRef"]) is not { } reference || !Integer(receipt["sourceTurn"], out var turn) || turn < 1 ||
                receipt["sourceAuthority"] is not JsonObject source || !Closed(source, "sessionId", "requestId", "snapshotToken") ||
                source.Any(pair => Exact(pair.Value) is null) ||
                receipt["birthState"] is not JsonObject birth || Exact(birth["vehicleId"]) != id ||
                birth.ContainsKey("vehicleRef") || birth.ContainsKey("resourceMaterialization") || birth.ContainsKey("cargoMeasurements") ||
                receipt["resourceInitialization"] is not JsonArray { Count: 1 } initial || initial[0] is not JsonObject health ||
                !Closed(health, "resourceKey", "maximum", "sourceKind", "sourceId", "authorityFingerprint") ||
                Exact(health["resourceKey"]) != "health" || !Number(health["maximum"], out var maximum) || maximum < 0 ||
                Exact(health["sourceKind"]) != "owner_materialization" || Exact(health["sourceId"]) != reference ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Exact(health["authorityFingerprint"])) || Exact(receipt["seal"]) != ComputeSeal(receipt))
            { issues.Add(Issue("birthReceipts", "Unique complete internally sealed version-1 vehicle birth evidence.")); continue; }
            var structuralBirth = birth.DeepClone().AsObject();
            structuralBirth.Remove("cargoBirthEvidence");
            var birthElement = System.Text.Json.JsonSerializer.SerializeToElement(structuralBirth);
            const string birthPath = StorageTransportMoveService.VehiclesPath + ".birthReceipts.birthState";
            var birthIssues = MortalTransportStorageContract.ValidateVehicleBirth(birthElement, birthPath).ToList();
            ValidationService.ValidateVehicleCoreFields(birthElement, birthPath, birthIssues);
            ValidationService.ValidateHistoricalVehicleSemantics(birthElement, birthPath, birthIssues);
            if (birthIssues.Count != 0)
                issues.Add(Issue("birthReceipts.birthState", "Complete historical vehicle birth facts."));
            if (birth["cargoBirthEvidence"] is not JsonObject cargo || Exact(cargo["vehicleId"]) != id ||
                !Integer(cargo["sourceTurn"], out var cargoTurn) || cargoTurn != turn ||
                !ValidateHistoricalCargo(structuralBirth, cargo, id, reference, turn))
                issues.Add(Issue("birthReceipts.birthState.cargoBirthEvidence", "Matching immutable historical cargo evidence."));
        }
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vehicle in vehicles)
            if (vehicle is not JsonObject item || Exact(item["vehicleId"]) is not { } id || !live.Add(id) || !ids.Contains(id))
                issues.Add(Issue("vehicles", "Exactly one birth receipt for every exact live vehicle identity."));
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateResourceAgreement(JsonObject? root,
        ResourceStateLedger? state, ResourceHistoryState? history)
    {
        var issues = root is null ? new List<ValidationIssue>() : ValidateLocal(root).ToList();
        var transitions = history?.Transitions.Where(t => t.Coordinate.Realm == "mortal_world" &&
            t.Coordinate.OwnerKind == ResourceOwnerKind.Vehicle).ToArray() ?? Array.Empty<ResourceTransition>();
        var liveResources = state?.Entries.Where(e => e.Coordinate.Realm == "mortal_world" &&
            e.Coordinate.OwnerKind == ResourceOwnerKind.Vehicle).ToArray() ?? Array.Empty<ResourceStateEntry>();
        if (root is null)
        {
            if (transitions.Length != 0 || liveResources.Length != 0)
                issues.Add(Issue("birthReceipts", "Vehicle evidence cannot be absent while live or retired vehicle resources exist."));
            return issues;
        }
        if (issues.Count != 0) return issues;
        var receipts = root["birthReceipts"]!.AsArray().Cast<JsonObject>().ToArray();
        if ((receipts.Length != 0 || root["vehicles"]!.AsArray().Count != 0) && (state is null || history is null))
            return new[] { Issue("birthReceipts", "Vehicle-bearing canonical documents require current resource state and history.") };
        var receiptIds = receipts.Select(r => Exact(r["vehicleId"])!).ToHashSet(StringComparer.Ordinal);
        if (transitions.Any(t => !receiptIds.Contains(t.Coordinate.ResourceOwnerId)) ||
            liveResources.Any(e => !receiptIds.Contains(e.Coordinate.ResourceOwnerId)))
            issues.Add(Issue("birthReceipts", "Every live or retired vehicle resource identity retains its birth receipt."));
        var liveIds = root["vehicles"]!.AsArray().Select(v => Exact(v!["vehicleId"])!).ToHashSet(StringComparer.Ordinal);
        foreach (var receipt in receipts)
        {
            var id = Exact(receipt["vehicleId"])!;
            var initial = receipt["resourceInitialization"]![0]!;
            var own = transitions.Where(t => t.Coordinate.ResourceOwnerId == id).ToArray();
            var births = own.Where(t => t.Operation == ResourceTransitionOperation.Initialize).ToArray();
            if (births.Length != 1 || births[0].Coordinate.ResourceKey != "health" ||
                births[0].Turn != receipt["sourceTurn"]!.GetValue<int>() ||
                births[0].AfterState?.Maximum != initial["maximum"]!.GetValue<decimal>() ||
                births[0].SourceEvidence.SourceKind != Exact(initial["sourceKind"]) ||
                births[0].SourceEvidence.SourceId != Exact(initial["sourceId"]) ||
                births[0].SourceEvidence.AuthorityFingerprint != Exact(initial["authorityFingerprint"]))
                issues.Add(Issue("birthReceipts", "Exact original health initialization history for each vehicle receipt."));
            if (!liveIds.Contains(id) && (own.Length == 0 || liveResources.Any(e => e.Coordinate.ResourceOwnerId == id) ||
                own.GroupBy(t => t.Coordinate).Any(g => g.OrderBy(t => t.Turn).ThenBy(t => t.ExecutionSequence).Last().Operation != ResourceTransitionOperation.Retire)))
                issues.Add(Issue("birthReceipts", "A removed vehicle receipt requires actual retirement history and no live resources."));
        }
        return issues;
    }

    private static bool Closed(JsonObject node, params string[] fields) => node.Count == fields.Length && fields.All(node.ContainsKey);
    private static string? Exact(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text) ? text : null;
    private static bool Integer(JsonNode? node, out int result) { result = 0; return node is JsonValue value && value.TryGetValue(out result); }
    private static bool Number(JsonNode? node, out decimal result) { result = 0; return node is JsonValue value && value.TryGetValue(out result); }
    private static ValidationIssue Issue(string field, string expected) => new(StorageTransportMoveService.VehiclesPath + "." + field,
        IssueSeverity.Error, "Vehicle birth evidence is missing or inconsistent.", code: "resource_owner_vehicle_receipt_invalid",
        section: "Vehicles", expected: expected, actual: "invalid birth evidence",
        repairHint: "Restore client-owned evidence from accepted state; do not ask the GM to author or rewrite receipts.");
}
