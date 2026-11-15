using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static bool AttachMortalVehicleBirthReceipts(ResourceOwnerCompositionResult composition,
        MortalVehicleBirthPreparation preparation, string sessionId, string requestId, string snapshotToken,
        int turn, List<ValidationIssue> issues)
    {
        if (preparation.Ids.Count == 0) return true;
        if (!composition.OwnerCompanionAfterImages.TryGetValue(StorageTransportMoveService.VehiclesPath, out var root) ||
            root["vehicles"] is not JsonArray vehicles || root["birthReceipts"] is not JsonArray existing)
        {
            issues.Add(ResourceIssue(StorageTransportMoveService.VehiclesPath, "resource_owner_vehicle_receipt_invalid",
                "complete client-owned vehicle companion with receipt collection", "missing companion"));
            return false;
        }
        var receipts = existing.DeepClone().AsArray();
        foreach (var pair in preparation.Ids)
        {
            var matches = vehicles.OfType<JsonObject>().Where(v => v["vehicleId"] is JsonValue id &&
                id.TryGetValue<string>(out var text) && text == pair.Value).ToArray();
            if (matches.Length != 1 || receipts.OfType<JsonObject>().Any(r => r["vehicleId"]?.GetValue<string>() == pair.Value))
            {
                issues.Add(ResourceIssue(StorageTransportMoveService.VehiclesPath, "resource_owner_vehicle_receipt_invalid",
                    "one newly resolved vehicle with no prior birth receipt", pair.Value));
                return false;
            }
            receipts.Add(MortalVehicleBirthReceiptState.CreateReceipt(matches[0], pair.Key, turn,
                sessionId, requestId, snapshotToken, composition.CapacityDrafts));
        }
        root["birthReceipts"] = receipts;
        issues.AddRange(MortalVehicleBirthReceiptState.ValidateLocal(root));
        return !issues.Any(issue => issue.Severity == IssueSeverity.Error);
    }
}
