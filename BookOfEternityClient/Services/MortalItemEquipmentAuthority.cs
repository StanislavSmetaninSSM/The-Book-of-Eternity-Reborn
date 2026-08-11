using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalItemEquipmentAuthority
{
    internal static bool TryRead(
        JsonObject owner,
        JsonArray? inventory,
        string context,
        out MortalItemEquipmentSnapshot snapshot,
        out string error)
    {
        snapshot = MortalItemEquipmentSnapshot.Empty;
        error = string.Empty;

        if (inventory == null)
        {
            error = $"{context}.items должен быть current-schema array до чтения equippedItems.";
            return false;
        }

        var acceptedItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in inventory.OfType<JsonObject>())
        {
            if (!MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var itemId))
                continue;
            if (!acceptedItemIds.Add(itemId))
            {
                error = $"{context}.items содержит неоднозначный exact itemId {itemId}; equippedItems не может быть разрешён безопасно.";
                return false;
            }
        }

        if (!owner.TryGetPropertyValue("equippedItems", out var equippedItemsNode))
            return true;

        if (equippedItemsNode is not JsonObject equippedItems)
        {
            error = $"{context}.equippedItems должен быть current-schema object, если поле присутствует.";
            return false;
        }

        var normalizedSlots = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<MortalItemEquipmentSlot>();
        foreach (var property in equippedItems)
        {
            if (!string.Equals(property.Key, property.Key.Trim(), StringComparison.Ordinal) ||
                !InventoryEquipmentService.TryNormalizeSlot(property.Key, out var canonicalSlot))
            {
                error = $"{context}.equippedItems.{property.Key} использует неизвестный equipment slot.";
                return false;
            }
            if (!normalizedSlots.Add(canonicalSlot))
            {
                error = $"{context}.equippedItems содержит дублирующий normalized slot {canonicalSlot}.";
                return false;
            }

            if (property.Value == null)
            {
                slots.Add(new MortalItemEquipmentSlot(property.Key, canonicalSlot, null));
                continue;
            }
            if (property.Value is not JsonValue value ||
                !value.TryGetValue<string>(out var itemId) ||
                string.IsNullOrWhiteSpace(itemId) ||
                !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
            {
                error = $"{context}.equippedItems.{property.Key} должен хранить exact scalar itemId или null.";
                return false;
            }
            if (!acceptedItemIds.Contains(itemId))
            {
                error = $"{context}.equippedItems.{property.Key} не разрешается в один exact receipt-bearing itemId {itemId}.";
                return false;
            }

            slots.Add(new MortalItemEquipmentSlot(property.Key, canonicalSlot, itemId));
        }

        snapshot = new MortalItemEquipmentSnapshot(equippedItems, slots);
        return true;
    }
}

internal sealed record MortalItemEquipmentSnapshot(
    JsonObject Node,
    IReadOnlyList<MortalItemEquipmentSlot> Slots)
{
    internal static MortalItemEquipmentSnapshot Empty { get; } =
        new(new JsonObject(), Array.Empty<MortalItemEquipmentSlot>());

    internal HashSet<string> EquippedItemIds() =>
        Slots
            .Where(static slot => slot.ItemId != null)
            .Select(static slot => slot.ItemId!)
            .ToHashSet(StringComparer.Ordinal);
}

internal sealed record MortalItemEquipmentSlot(
    string StoredSlot,
    string CanonicalSlot,
    string? ItemId);
