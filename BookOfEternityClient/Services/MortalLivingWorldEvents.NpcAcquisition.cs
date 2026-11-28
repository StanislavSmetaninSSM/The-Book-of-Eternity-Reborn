using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLivingWorldEvents
{
    private sealed record NpcAcquisitionIndexEvidence(
        IReadOnlyDictionary<string, JsonObject> Original,
        IReadOnlyDictionary<string, JsonObject> Current,
        MortalItemAcceptedRootCreationEvidence PriorRoots);

    private static NpcAcquisitionIndexEvidence? ReadNpcAcquisitionIndex(
        IReadOnlyDictionary<string, JsonObject> original, IReadOnlyDictionary<string, JsonObject> current)
    {
        if (!original.TryGetValue(MortalItemIdentityState.StatePath, out var before) ||
            !current.TryGetValue(MortalItemIdentityState.StatePath, out var after))
            return null;
        var beforeIndex = MortalItemIdentityState.Parse(before);
        var afterIndex = MortalItemIdentityState.Parse(after);
        return beforeIndex.Issues.Count == 0 && afterIndex.Issues.Count == 0
            ? new(beforeIndex.EntriesByItemId, afterIndex.EntriesByItemId,
                MortalItemIdentityState.BuildAcceptedRootCreationEvidence(beforeIndex))
            : null;
    }

    private static bool MatchesNpcAcquisition(Reference target, string creationRef, long turn,
        IReadOnlyDictionary<string, JsonObject> original, IReadOnlyDictionary<string, JsonObject> current,
        NpcAcquisitionIndexEvidence? index, out string refusal)
    {
        refusal = "Acquisition requires a settled NPC inventory and both item indexes.";
        if (index is null || Resolve(original, target) is null ||
            Resolve(current, target)?["inventory"] is not JsonArray inventory)
            return false;
        var items = inventory.OfType<JsonObject>().Where(item =>
            item[MortalItemMaterializationContract.ReceiptProperty] is JsonObject r &&
            Text(r["creationRef"]) == creationRef).Take(2).ToArray();
        refusal = "Acquisition requires one exact root item and its current receipt in the NPC inventory.";
        if (items.Length != 1 || Text(items[0]["itemId"]) is not { } itemId ||
            Text(items[0]["existedId"]) != itemId ||
            items[0][MortalItemMaterializationContract.ReceiptProperty] is not JsonObject receipt ||
            Text(receipt["itemId"]) != itemId || Text(receipt["receiptId"]) is not { } receiptId ||
            Text(receipt["instanceKind"]) != "root" || !Integer(receipt["acceptedAtTurn"], out var acceptedTurn) ||
            acceptedTurn != turn || Text(receipt["materializationId"]) is not { } materializationId ||
            Text(receipt["seal"]) != MortalItemMaterializationContract.ComputeSeal(items[0], receipt))
            return false;

        refusal = "Acquisition item and origin must be new in the identity index.";
        if (index.PriorRoots.Match(materializationId, creationRef) != MortalItemAcceptedCreationEvidenceMatch.None ||
            index.Original.ContainsKey(itemId) || !index.Current.TryGetValue(itemId, out var entry) ||
            Text(entry["receiptId"]) != receiptId || Text(entry["state"]) != "active" ||
            entry["originCreationRefs"] is not JsonArray { Count: 1 } origins || Text(origins[0]) != creationRef ||
            entry["originMaterializationIds"] is not JsonArray { Count: 1 } materializations ||
            Text(materializations[0]) != materializationId)
            return false;

        refusal = "Acquisition must retain the declared NPC as root inventory owner.";
        if (entry["currentCarrier"] is not JsonObject carrier || Text(carrier["kind"]) != "npc_inventory" ||
            Text(carrier["ownerId"]) != target.Id || carrier["containerId"] is not null ||
            carrier["containerPath"] is not JsonArray { Count: 0 } ||
            entry["transitions"] is not JsonArray { Count: 1 } transitions ||
            transitions[0] is not JsonObject transition || Text(transition["kind"]) != "create" ||
            !Integer(transition["turn"], out var createdTurn) || createdTurn != turn ||
            !JsonNode.DeepEquals(transition["destinationCarrier"], carrier))
            return false;

        refusal = "Acquisition must use the same dedicated NPC inventory authority in item and index.";
        if (items[0][MortalItemMaterializationContract.EnvelopeProperty] is not JsonObject envelope ||
            Text(envelope["route"]) != "npc_acquisition" || envelope["sourceAuthority"] is not JsonObject authority ||
            Text(authority["kind"]) != "npc_inventory_add" ||
            Text(transition["authorityKind"]) != "npc_inventory_add" ||
            Text(authority["authorityId"]) is not { } authorityId || Text(transition["authorityId"]) != authorityId)
            return false;

        return true; // The enclosing canonical item validator still owns the complete global item graph.
    }
}
